"""有界 Collector（D-02）：固定局内策略 + 独立 worker + 显式限额 + 停止与清理。

边界与承诺：

* **固定局内策略**：一局之内策略与执行器配置不变（这里是均匀随机基线）；跨局只换派生种子。
* **种子派生**：`derive_seed(base_seed, index)` 只由基种子与局序号决定——不含时钟、进程 ID 或全局随机源，
  因此同一配置可重复产出同一批记录。
* **限额是显式的**：局数、每局步数、总时长、写出字节、单局在飞快照字节。达到任一限额都**有明确原因**，
  不静默截断、不无限重试。
* **中断不混入普通终止**：宿主丢失/结果不确定的局标为 `interrupted`，保留其**已确认**的转移与那条
  结果不确定的转移；绝不写成胜负，也不补造下一状态。
* **不做并行**：多 worker 是"多个独立宿主进程 + 逐局轮转指派"，用于证明 worker 之间不串状态；
  真正的并行调度属 D-04 的监督入口。产物目录由调用方给，不写进源码树。
"""

from __future__ import annotations

import hashlib
import json
import pathlib
import time
from dataclasses import dataclass, field
from typing import Any, Callable, Optional, Sequence

from ..data import EpisodeStore, build_record, canonical_bytes
from ..runner import EpisodeRunner
from ..runner.baseline import EXECUTOR_PROFILE_ID

COLLECTOR_VERSION = "ai-the-spire/collector@1"

#: 停止原因（互斥；`error` 只有在实现缺陷/配置错误时才出现）。
STOP_REASONS = (
    "episodes_completed",
    "episode_limit",
    "time_limit",
    "disk_limit",
    "inflight_limit",
    "host_lost",
    "error",
)


@dataclass(frozen=True)
class CollectorLimits:
    """显式限额；全部是保守的开发用配置，不是产品性能门槛。"""

    episodes: int = 2
    steps_per_episode: int = 40
    seconds: float = 120.0
    bytes: int = 64 * 1024 * 1024
    inflight_bytes: int = 16 * 1024 * 1024

    def __post_init__(self) -> None:
        if self.episodes <= 0 or self.steps_per_episode <= 0:
            raise ValueError("局数与每局步数必须是显式正数")
        if self.seconds <= 0 or self.bytes <= 0 or self.inflight_bytes <= 0:
            raise ValueError("时长/磁盘/在飞上限必须是显式正数")

    def to_wire(self) -> dict:
        return dict(self.__dict__)


def derive_seed(base_seed: int, episode_index: int, variant: str = "") -> int:
    """确定性种子派生：sha256(base:variant:index:episode) → 53 位整数（协议要求安全十进制字符串）。

    `variant` 是场景簇标签：不同场景配置**不得共用同一个种子流**，否则同一份产物目录里会出现
    "同种子不同配置"的混淆记录（审计与去重都会受影响）。
    """
    if episode_index < 0:
        raise ValueError("局序号不能为负")
    material = f"{base_seed}:{variant}:{episode_index}:episode".encode("utf-8")
    return int.from_bytes(hashlib.sha256(material).digest()[:8], "big") & ((1 << 53) - 1)


@dataclass
class CollectionResult:
    stop_reason: str
    stop_detail: str
    episode_ids: Sequence[str] = field(default_factory=list)
    counts: dict = field(default_factory=dict)
    progress_path: Optional[pathlib.Path] = None
    written_bytes: int = 0
    #: 恢复时应从哪一号开始：中断的那一局为它自己（从头新开），否则是下一号。
    resume_from_index: int = 0

    def to_wire(self) -> dict:
        return {
            "collector_version": COLLECTOR_VERSION,
            "stop_reason": self.stop_reason,
            "stop_detail": self.stop_detail,
            "episode_ids": list(self.episode_ids),
            "counts": dict(self.counts),
            "written_bytes": self.written_bytes,
            "resume_from_index": self.resume_from_index,
        }


class Collector:
    """按显式限额收集 episode 记录；每个 worker 是一个独立的宿主连接。"""

    def __init__(
        self,
        root: "str | pathlib.Path",
        *,
        base_seed: int,
        limits: CollectorLimits,
        workers: int = 1,
        client_factory: Optional[Callable[[], Any]] = None,
        interruption_errors: tuple = (OSError,),
        fault_injection: Optional[Callable[[int, Any], None]] = None,
        scenario_variant: str = "baseline",
    ) -> None:
        if workers <= 0:
            raise ValueError("workers 必须是显式正数")
        if not scenario_variant:
            raise ValueError("scenario_variant 必须显式给出（它是数据划分的场景簇键的一部分）")
        self.root = pathlib.Path(root)
        self.base_seed = base_seed
        self.limits = limits
        self.workers = workers
        #: 场景簇标签：同一标签下的局面属于同一个场景配置。D-03 按它划分 train/validation/final_eval，
        #: 因此**同簇不得跨 split**。不同起始牌组/配置必须换标签（例如测试场景 `test-headbutt`）。
        self.scenario_variant = scenario_variant
        self._client_factory = client_factory
        self._interruption_errors = tuple(interruption_errors)
        #: **故障注入钩子（仅检查用）**：`(episode_index, client)`，在每局开始前调用。
        #: 生产路径不应传它；用它产生的证据必须标注为故障注入。
        self._fault_injection = fault_injection
        self.store = EpisodeStore(self.root)

    # -- 运行 ----------------------------------------------------------------------------------------------

    def run(self, *, start_index: int = 0) -> CollectionResult:
        """从 `start_index` 开始采样。

        `start_index` 由调用方从进度文件给出（D-04 的恢复语义）：**恢复的是采样进度，不是游戏中途状态**——
        被中断的那一局不会被"接着打"，而是从头新开一局（`resume_from_index` 指向它）。
        """
        if start_index < 0:
            raise ValueError("start_index 不能为负")
        started = time.monotonic()
        result = CollectionResult(stop_reason="episodes_completed", stop_detail="",
                                  progress_path=self.root / "progress.json")
        counts = {"attempted": 0, "written": 0, "skipped_duplicates": 0, "terminal_win": 0,
                  "terminal_loss": 0, "truncated": 0, "faulted": 0, "interrupted": 0,
                  "unknown_transitions": 0}
        #: 恢复时要重做的局序号：中断的那一局必须**从头新开**，所以它仍是"下一次要做"的序号。
        resume_from = start_index
        clients: list[Any] = []
        try:
            for index in range(start_index, self.limits.episodes):
                elapsed = time.monotonic() - started
                if elapsed >= self.limits.seconds:
                    result.stop_reason = "time_limit"
                    result.stop_detail = (
                        f"已达显式时长上限 {self.limits.seconds}s（已用 {elapsed:.2f}s）；"
                        f"完成 {counts['written']} 局"
                    )
                    break

                worker = index % self.workers
                while len(clients) <= worker:
                    clients.append(None)
                if clients[worker] is None:
                    clients[worker] = self._client_factory()
                client = clients[worker]
                if self._fault_injection is not None:
                    self._fault_injection(index, client)

                counts["attempted"] += 1
                seed = derive_seed(self.base_seed, index, self.scenario_variant)
                runner = EpisodeRunner(
                    client,
                    seed=seed,
                    max_steps=self.limits.steps_per_episode,
                    executor_seed=seed,
                    interruption_errors=self._interruption_errors,
                )
                try:
                    episode = runner.run(raise_on_failure=False)
                except self._interruption_errors as failure:
                    # 连 prepare/start 都没走通：这仍然是宿主丢失，**不是**实现错误。
                    # 该 worker 的连接已经不可用，必须换一条（否则后续局会在死管道上继续失败）。
                    result.stop_reason = "host_lost"
                    result.stop_detail = f"第 {index} 局在准备阶段丢失连接：{failure}"
                    resume_from = index
                    clients[worker] = _replace(clients[worker], self._client_factory)
                    break
                except Exception as failure:  # 实现/配置错误：不伪装成中断，直接记录原因
                    result.stop_reason = "error"
                    result.stop_detail = f"{type(failure).__name__}: {failure}"
                    counts["faulted"] += 1
                    clients[worker] = _replace(clients[worker], self._client_factory)
                    break

                record = build_record(
                    episode,
                    provenance={
                        "collector": COLLECTOR_VERSION,
                        "entry": "collector",
                        "worker": str(worker),
                        "episode_index": str(index),
                        "base_seed": str(self.base_seed),
                        "scenario_variant": self.scenario_variant,
                        "fault_injection": "yes" if self._fault_injection is not None else "no",
                    },
                )
                document = record.to_wire()
                size = len(canonical_bytes(document))
                inflight = sum(len(canonical_bytes(item["snapshot"])) for item in document["transitions"])

                if self.store.record_path(record.episode_id).exists():
                    # 同一配置重复跑出同一条记录：这是**可重放**的证明，不是错误；跳过不重复计样本。
                    result.episode_ids.append(record.episode_id)
                    counts["written"] += 1
                    counts[record.ending_class] += 1
                    counts["unknown_transitions"] += record.unknown_transitions
                    continue

                if result.written_bytes + size > self.limits.bytes:
                    result.stop_reason = "disk_limit"
                    result.stop_detail = (
                        f"写入本局会超过显式磁盘上限 {self.limits.bytes} 字节"
                        f"（已写 {result.written_bytes}，本局 {size}）；该局未写入"
                    )
                    break
                if inflight > self.limits.inflight_bytes:
                    result.stop_reason = "inflight_limit"
                    result.stop_detail = (
                        f"单局在飞快照 {inflight} 字节超过显式上限 {self.limits.inflight_bytes}"
                    )
                    break

                self.store.write(record)
                result.episode_ids.append(record.episode_id)
                result.written_bytes += size
                counts["written"] += 1
                counts[record.ending_class] += 1
                counts["unknown_transitions"] += record.unknown_transitions

                if episode.ending == "interrupted":
                    result.stop_reason = "host_lost"
                    result.stop_detail = (
                        f"第 {index} 局中断（{episode.ending_detail}）：已保存该局已确认的转移，停止控制"
                    )
                    # 中断的局保持 interrupted（不伪装成终止），并且**从头重做**：恢复时仍从这一号开始。
                    resume_from = index
                    # 连接已死：换一条，避免下一次在死管道上继续失败。
                    clients[worker] = _replace(clients[worker], self._client_factory)
                    break
                resume_from = index + 1
            else:
                if self.limits.episodes == counts["attempted"] + start_index:
                    result.stop_reason = "episodes_completed"
                    result.stop_detail = f"完成 {counts['written']} 局（未触及其它限额）"
                resume_from = self.limits.episodes
        finally:
            for client in clients:
                if client is None:
                    continue
                try:
                    client.shutdown()
                except Exception:  # noqa: BLE001 - 清理必须容错，且不掩盖真实结果
                    pass

        result.counts = counts
        result.resume_from_index = resume_from
        self._write_progress(result)
        return result

    # -- 进度/checkpoint ------------------------------------------------------------------------------------

    def _write_progress(self, result: CollectionResult) -> None:
        """进度与 checkpoint 索引：D-04 的监督入口据此重启（**恢复的是采样进度，不是游戏中途状态**）。

        `next_episode_index` 与 `resume_from_index` 的区别是**刻意的**：前者表示"已推进到哪一号"，
        后者表示"恢复时该从哪一号开始做"。中断的那一局算已推进，但恢复时要**从头新开**，
        因此 `resume_from_index` 指向它自己。
        """
        self.root.mkdir(parents=True, exist_ok=True)
        document = {
            "collector_version": COLLECTOR_VERSION,
            "base_seed": str(self.base_seed),
            "limits": self.limits.to_wire(),
            "workers": self.workers,
            "scenario_variant": self.scenario_variant,
            "executor_profile_id": EXECUTOR_PROFILE_ID,
            "attempted": result.counts.get("attempted", 0),
            "written": result.counts.get("written", 0),
            "next_episode_index": result.counts.get("attempted", 0),
            "resume_from_index": result.resume_from_index,
            "counts": result.counts,
            "stop_reason": result.stop_reason,
            "stop_detail": result.stop_detail,
            "episode_ids": list(result.episode_ids),
            "written_bytes": result.written_bytes,
        }
        (self.root / "progress.json").write_bytes(canonical_bytes(document))


def _replace(dead: Any, factory: Callable[[], Any]) -> Any:
    """换掉一条已失效的连接（中断后必须换，否则下一局会在死管道上继续失败）。"""
    if dead is not None:
        try:
            dead.shutdown()
        except Exception:  # noqa: BLE001 - 清理容错
            pass
    return factory()


def read_progress(root: "str | pathlib.Path") -> dict:
    """读进度/checkpoint 索引；`resume_from_index` 是恢复起点。"""
    path = pathlib.Path(root) / "progress.json"
    if not path.exists():
        raise FileNotFoundError(f"没有进度文件: {path}")
    return json.loads(path.read_text(encoding="utf-8"))
