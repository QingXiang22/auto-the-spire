"""在场景清单上做**有界采样**并给出成本报告（W-03）。

复用已有 D-02 `Collector`（显式限额、确定性种子派生、独立 worker 连接、进度索引），
本模块只加三件事：

1. **按簇换环境**：每个簇用自己的宿主开关建连接（`collect.py` 的 `--author-content` /
   `--test-headbutt`），并把簇标签写进记录（`scenario_variant`）；
2. **区分事实验证**：采完之后在**该簇真实录到的公开快照**上核对清单声明的事实——
   核对不过的簇，其数据不得用于该簇（避免"标签换了、环境没换"）；
3. **成本报告**：限额、计数（尝试/写入/中断/故障/截断/胜负）、墙钟、字节、停止原因，
   并显式声明"多会话是轮转不是并行吞吐""随机策略数据不是新策略的近期 PPO 数据"。
"""

from __future__ import annotations

import hashlib
import pathlib
import time
from dataclasses import dataclass, field
from typing import Any, Callable, Mapping, Optional, Sequence

from ..collector import Collector, CollectorLimits
from ..contracts import layers
from ..data.audit import SPLITS, plan_digest_of
from .manifest import (
    ScenarioFactError,
    ScenarioManifest,
    ScenarioSpec,
    check_distinguishing_fact,
)

#: 采样报告格式版本。
SAMPLING_REPORT_VERSION = "scenario-sampling-report@1"

#: `data.audit` 的"划分退化"标记：它是**全局**性质，逐身份分组审计时不算问题。
DEGENERATE_MARKER = "划分退化"

#: 采集策略的显式标签（随机基线；不是被训练的策略）。
BASELINE_POLICY_ID = "baseline:uniform-legal-candidates@1"


class SamplingError(RuntimeError):
    """采样或报告组装失败（限额非法、清单与目录不匹配等）。"""


@dataclass(frozen=True)
class ClusterSample:
    """一个簇的采样结果与成本。"""

    scenario_id: str
    host_flags: tuple
    limits: dict
    stop_reason: str
    stop_detail: str
    counts: dict
    wall_seconds: float
    written_bytes: int
    fact_ok: bool
    fact_detail: str
    episodes: int

    def to_wire(self) -> dict:
        return {
            "scenario_id": self.scenario_id,
            "host_flags": list(self.host_flags),
            # 限额里的秒数是浮点 ⇒ 规范 wire 要求写成十进制字符串（共享 L0 不接受浮点字面量）
            "limits": {key: (repr(float(value)) if isinstance(value, float) else value)
                       for key, value in sorted(self.limits.items())},
            "stop_reason": self.stop_reason,
            "stop_detail": self.stop_detail,
            "counts": {key: int(value) for key, value in sorted(self.counts.items())},
            "wall_seconds": repr(float(self.wall_seconds)),
            "written_bytes": int(self.written_bytes),
            "fact_ok": bool(self.fact_ok),
            "fact_detail": self.fact_detail,
            "episodes": int(self.episodes),
        }


@dataclass(frozen=True)
class SamplingReport:
    """一次有界采样的完整产物（清单身份 + 划分身份 + 逐簇成本 + 声明）。"""

    manifest: dict
    manifest_digest: str
    split_assignments: dict
    split_plan_digest: str
    clusters: tuple
    policy_id: str
    workers: int
    concurrency: str
    notes: tuple
    root: str

    # -- 汇总 ------------------------------------------------------------ #
    @property
    def attempts(self) -> int:
        return sum(int(sample.counts.get("attempted", 0)) for sample in self.clusters)

    @property
    def written(self) -> int:
        return sum(int(sample.counts.get("written", 0)) for sample in self.clusters)

    @property
    def interrupted(self) -> int:
        return sum(int(sample.counts.get("interrupted", 0)) for sample in self.clusters)

    @property
    def faulted(self) -> int:
        return sum(int(sample.counts.get("faulted", 0)) for sample in self.clusters)

    def to_wire(self) -> dict:
        return {
            "report_format_version": SAMPLING_REPORT_VERSION,
            "manifest": self.manifest,
            "manifest_digest": self.manifest_digest,
            "split_assignments": dict(sorted(self.split_assignments.items())),
            "split_plan_digest": self.split_plan_digest,
            "policy_id": self.policy_id,
            "workers": int(self.workers),
            "concurrency": self.concurrency,
            "notes": list(self.notes),
            "root": self.root,
            "totals": {
                "attempts": self.attempts,
                "written": self.written,
                "interrupted": self.interrupted,
                "faulted": self.faulted,
                "clusters": len(self.clusters),
                "wall_seconds": repr(float(sum(float(sample.wall_seconds)
                                               for sample in self.clusters))),
                "written_bytes": sum(int(sample.written_bytes) for sample in self.clusters),
            },
            "clusters": [sample.to_wire() for sample in self.clusters],
        }

    def digest(self) -> str:
        return "sha256:" + hashlib.sha256(layers.canonical_bytes(self.to_wire())).hexdigest()


def _read_snapshots(store: Any, episode_ids: Sequence[str]) -> list:
    """从记录里取出**公开快照**序列（记录里的快照本来就是公开事实）。"""
    snapshots = []
    for episode_id in episode_ids:
        document = store.read(episode_id)
        for transition in document.get("transitions") or []:
            snapshot = transition.get("snapshot")
            if isinstance(snapshot, Mapping):
                snapshots.append(snapshot)
    return snapshots


def run_scenario_sampling(
    manifest: ScenarioManifest,
    root: "str | pathlib.Path",
    *,
    scenario_ids: Sequence[str],
    limits: CollectorLimits,
    base_seed: int,
    workers: int = 1,
    client_factory: Optional[Callable[[Sequence[str]], Any]] = None,
    split_assignments: Optional[Mapping[str, str]] = None,
    interruption_errors: tuple = (OSError,),
) -> SamplingReport:
    """在每个指定簇上做有界采样，返回含成本与事实验证的报告。

    `client_factory(host_flags)` 必须为每个簇返回**新**连接（默认用 `net_client.host_from_solution`）。
    采样是**逐簇串行 + 簇内 worker 轮转**：这不是并行吞吐，报告里如实标注。
    """
    if not isinstance(manifest, ScenarioManifest):
        raise TypeError(f"manifest 必须是 ScenarioManifest，得到 {type(manifest).__name__}")
    if not scenario_ids:
        raise SamplingError("必须显式给出要采样的簇（不得默认采全部）")
    if isinstance(base_seed, bool) or not isinstance(base_seed, int) or base_seed < 0:
        raise SamplingError(f"base_seed 必须是非负整数，得到 {base_seed!r}")
    if workers <= 0:
        raise SamplingError(f"workers 必须是显式正数，得到 {workers!r}")
    if client_factory is None:
        client_factory = _default_client_factory

    target = pathlib.Path(root)
    samples = []
    for scenario_id in scenario_ids:
        spec = manifest.by_id(scenario_id)
        # 按**身份分组**分目录存放：`--author-content` 的 ruleset/catalog 与默认切片不同，
        # 混在一个目录里会被 `data.audit` 正确地判为"混用身份"。
        # 同一组内仍由 audit 独立核对"只有一种身份"。
        collector = Collector(
            target / spec.identity_family,
            base_seed=base_seed,
            limits=limits,
            workers=workers,
            client_factory=lambda flags=tuple(spec.host_flags): client_factory(flags),
            interruption_errors=interruption_errors,
            scenario_variant=scenario_id,
        )
        started = time.monotonic()
        result = collector.run()
        wall = time.monotonic() - started
        snapshots = _read_snapshots(collector.store, result.episode_ids)
        try:
            fact_ok, fact_detail = check_distinguishing_fact(snapshots, spec)
        except ScenarioFactError as failure:
            # 没有公开快照就没法核对：如实记成"未通过"，并保留原因（不当作通过）。
            fact_ok, fact_detail = False, f"{failure}"
        samples.append(ClusterSample(
            scenario_id=scenario_id,
            host_flags=tuple(spec.host_flags),
            limits=limits.to_wire(),
            stop_reason=result.stop_reason,
            stop_detail=result.stop_detail,
            counts=dict(result.counts),
            wall_seconds=wall,
            written_bytes=result.written_bytes,
            fact_ok=fact_ok,
            fact_detail=fact_detail,
            episodes=len(result.episode_ids),
        ))

    assignments = dict(split_assignments) if split_assignments is not None else {}
    notes = [
        f"policy={BASELINE_POLICY_ID}：随机合法策略，数据用于**数据设施验证与基线**，"
        "不作为新策略的近期 PPO 数据",
        f"workers={workers}：多会话是**逐簇串行 + 簇内轮转**，不是并行吞吐",
        f"限额为显式配置（局数 {limits.episodes} / 每局步数 {limits.steps_per_episode} / "
        f"{limits.seconds}s / {limits.bytes} 字节 / 在飞 {limits.inflight_bytes} 字节）",
        "簇通过区分事实核对后才能用于该簇；核对失败的簇不得并入其它簇",
    ]
    return SamplingReport(
        manifest=manifest.to_wire(),
        manifest_digest=manifest.digest(),
        split_assignments=assignments,
        split_plan_digest=(plan_digest_of(assignments) if assignments else ""),
        clusters=tuple(samples),
        policy_id=BASELINE_POLICY_ID,
        workers=workers,
        concurrency="sequential_by_cluster_round_robin_within_cluster",
        notes=tuple(notes),
        root=str(target),
    )


def audit_sampling(root: "str | pathlib.Path", manifest: ScenarioManifest,
                   split_assignments: Mapping[str, str]) -> "AggregatedAudit":
    """对采样产物做**整份**审计：分组内的身份一致 + 簇不跨 split + 全局三个 split 非空。

    为什么按身份分组审计：不同 Profile 的 ruleset/catalog/Manifest 不同，`data.audit` 会（正确地）
    把混在一个目录里的两组记录判为"混用身份"。因此逐组审计，并把**全局**的划分非空性在聚合层判定——
    单个组里某个 split 没有样本是正常的，不算问题（`划分退化` 只在全局为空时报）。
    """
    from ..data.audit import SplitPlan, audit  # 延迟导入：审计只在需要时构建
    from ..data.store import EpisodeStore

    if not split_assignments:
        raise SamplingError("审计必须给出簇 → split 的指派（否则无法判断划分是否退化）")
    keys = manifest.cluster_keys()
    families: dict[str, dict] = {}
    problems: list = []
    split_counts: dict = {split: {"episodes": 0, "clusters": []} for split in SPLITS}
    for scenario_id in manifest.scenario_ids:
        family = manifest.by_id(scenario_id).identity_family
        families.setdefault(family, {"scenarios": [], "problems": []})
        families[family]["scenarios"].append(scenario_id)
    for family, bucket in sorted(families.items()):
        family_assignments = {keys[scenario_id]: split_assignments[keys[scenario_id]]
                              for scenario_id in bucket["scenarios"]
                              if keys[scenario_id] in split_assignments}
        if not family_assignments:
            continue
        store = EpisodeStore(pathlib.Path(root) / family)
        result = audit(store, plan=SplitPlan(assignments=family_assignments))
        kept = [problem for problem in result.problems if DEGENERATE_MARKER not in problem]
        bucket["problems"] = kept
        problems.extend(f"{family}: {problem}" for problem in kept)
        for split, info in result.split_counts.items():
            split_counts[split]["episodes"] += int(info["episodes"])
            split_counts[split]["clusters"].extend(info["clusters"])
    return AggregatedAudit(
        problems=tuple(problems),
        split_counts={split: {"episodes": info["episodes"],
                              "clusters": sorted(set(info["clusters"]))}
                      for split, info in split_counts.items()},
        families={family: {"scenarios": tuple(bucket["scenarios"]),
                           "problems": tuple(bucket["problems"])}
                  for family, bucket in sorted(families.items())},
        degenerate_globally=tuple(split for split, info in split_counts.items()
                                  if info["episodes"] == 0),
    )


@dataclass(frozen=True)
class AggregatedAudit:
    """按身份分组审计后的聚合结果（全局划分非空性在这里判定）。"""

    problems: tuple
    split_counts: dict
    families: dict
    degenerate_globally: tuple

    def to_wire(self) -> dict:
        return {
            "problems": list(self.problems),
            "split_counts": {split: {"episodes": info["episodes"],
                                     "clusters": list(info["clusters"])}
                             for split, info in sorted(self.split_counts.items())},
            "families": {family: {"scenarios": list(bucket["scenarios"]),
                                  "problems": list(bucket["problems"])}
                         for family, bucket in sorted(self.families.items())},
            "degenerate_globally": list(self.degenerate_globally),
        }


def _default_client_factory(host_flags: Sequence[str]) -> Any:
    """默认连接工厂：按簇的宿主开关起**新**宿主进程（延迟导入，离线检查不需要宿主）。"""
    import sys

    repo = pathlib.Path(__file__).resolve().parents[3]
    client_dir = repo / "net-the-spire" / "client"
    if str(client_dir) not in sys.path:
        sys.path.insert(0, str(client_dir))
    from net_client import NetClient  # noqa: PLC0415 - 延迟导入

    return NetClient.host_from_solution(extra_args=list(host_flags))
