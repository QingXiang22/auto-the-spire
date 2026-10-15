"""数据审计与冻结划分（D-03）。

三件事，彼此独立：

1. **冻结划分**：`SplitPlan` 把**场景簇**（`scenario_profile:scenario_variant`）映射到
   train/validation/final_eval。划分按簇而不是按局，因此"同场景簇跨 split 泄漏"在结构上不可能；
   簇数不足时**如实报告划分退化**，不靠随机切分造出一个看似可用的 split。
2. **原始事实与奖励分开**：记录里**没有**奖励/回报/优势/价值字段（本层刻意不发明奖励），
   审计会拒绝任何出现这类字段或被补造成"未选动作收益标签"的记录。
3. **完整性/可用性/异常/偏移**：悬空引用、身份混用、摘要不符、退化分布与异常局都要被报出来，
   且"总尝试 / 有效 / 中断 / 故障 / 截断"分别计数，不把中断局混进可用样本。
"""

from __future__ import annotations

import hashlib
import json
from dataclasses import dataclass, field
from typing import Any, Iterable, Mapping, Optional, Sequence

from .record import RecordError, canonical_bytes, digest_of
from .store import EpisodeStore

SPLIT_PLAN_VERSION = "split-plan@1"
SPLITS = ("train", "validation", "final_eval")

#: 记录里**不得**出现的奖励类键（原始事实与奖励必须分开）。
FORBIDDEN_REWARD_KEYS = (
    "reward", "rewards", "return", "returns", "advantage", "advantages",
    "q_value", "q_values", "gain", "gains", "score", "payoff",
    "reward_vector", "value_target", "discounted_return",
)

#: 公开 wire 的结构键：它们与奖励无关（例如 `field.value` 是字段值本身）。
#: 不排除这些键会把每一条正常记录都误判成"含奖励字段"——检查必须能区分结构键与标签。
PROTOCOL_STRUCTURAL_KEYS = frozenset({
    "value", "state", "value_type", "field_id", "fields", "position", "position_schema",
    "total_count", "visible_members", "members_complete", "known_positions", "card_ref", "pile_ref",
    "instance_id", "kind_id", "content_state", "content_id", "entities", "piles", "relations",
    "sequences", "groups", "legal_actions", "candidate_id", "action_type", "source_ref", "target_ref",
    "type_id", "owner_ref", "elements", "members", "multiplicity", "protocol_version", "ruleset_id",
    "catalog_id", "backend_profile_id", "session_id", "observation_id", "status", "terminal_result",
    "outcome", "reason_id", "facts", "snapshot", "identity", "capabilities",
})

#: 缺少场景簇标签时的占位（其存在本身就是要报出来的问题，而不是默认为同一簇）。
UNLABELLED_CLUSTER = "<unlabelled>"


def cluster_key(record: Mapping[str, Any]) -> str:
    """场景簇键：同一场景配置的局面必须落在同一个簇里。

    `scenario_variant` 缺失时返回 `UNLABELLED_CLUSTER` 而不是默认成同一个标签：否则不同场景配置会被
    静默合并成一个簇，划分看起来"没有跨 split 泄漏"却已经在簇内混了两种配置。
    """
    identities = record["identities"]
    variant = record["provenance"].get("scenario_variant") or UNLABELLED_CLUSTER
    return f"{identities['scenario_profile']}:{variant}"


@dataclass(frozen=True)
class SplitPlan:
    """冻结的划分：簇 → split。`digest` 是它的身份，改动即换身份。"""

    assignments: Mapping[str, str]
    version: str = SPLIT_PLAN_VERSION

    def __post_init__(self) -> None:
        for cluster, split in self.assignments.items():
            if not cluster:
                raise RecordError("划分里的场景簇键不能为空")
            if split not in SPLITS:
                raise RecordError(f"未知 split {split!r}（可用：{SPLITS}）")
        if len(set(self.assignments.values())) == 0:
            raise RecordError("划分至少要指派一个场景簇")

    @property
    def digest(self) -> str:
        return digest_of({"version": self.version, "assignments": dict(sorted(self.assignments.items()))})

    def split_of(self, cluster: str) -> Optional[str]:
        return self.assignments.get(cluster)

    def to_wire(self) -> dict:
        return {"version": self.version, "assignments": dict(sorted(self.assignments.items())),
                "digest": self.digest}


@dataclass
class DataAudit:
    plan: Optional[SplitPlan] = None
    #: 计数：attempted 由 Collector 的进度提供（若给了），其余由记录统计。
    counts: dict = field(default_factory=dict)
    split_counts: dict = field(default_factory=dict)
    distributions: dict = field(default_factory=dict)
    problems: list = field(default_factory=list)
    notes: list = field(default_factory=list)

    @property
    def ok(self) -> bool:
        return not self.problems

    def to_wire(self) -> dict:
        return {
            "audit_version": "data-audit@1",
            "plan": self.plan.to_wire() if self.plan else None,
            "counts": dict(self.counts),
            "split_counts": dict(self.split_counts),
            "distributions": dict(self.distributions),
            "problems": list(self.problems),
            "notes": list(self.notes),
        }


# -- 单项检查 -----------------------------------------------------------------------------------------------


def _walk(value: Any, path: str = "") -> Iterable[tuple[str, str]]:
    """遍历所有键（含嵌套），产出 (路径, 键名)。"""
    if isinstance(value, Mapping):
        for key, item in value.items():
            yield f"{path}.{key}" if path else str(key), str(key)
            yield from _walk(item, f"{path}.{key}" if path else str(key))
    elif isinstance(value, (list, tuple)):
        for index, item in enumerate(value):
            yield from _walk(item, f"{path}[{index}]")


def find_reward_labels(document: Mapping[str, Any]) -> list[str]:
    """找出任何奖励类键；这些必须由训练侧（偏好/奖励向量）提供，不能混在原始事实里。

    公开 wire 的结构键（`field.value` 等）不算奖励标签——否则每条正常记录都会被误判。
    """
    found = []
    for path, key in _walk(document):
        lowered = key.lower()
        if lowered in PROTOCOL_STRUCTURAL_KEYS:
            continue
        if lowered in FORBIDDEN_REWARD_KEYS or lowered.endswith("_reward") or lowered.startswith("reward_"):
            found.append(path)
    return found


def dangling_references(document: Mapping[str, Any]) -> list[str]:
    """返回转移快照里的悬空引用（成员/已知位置/关系/候选字段必须落在本快照实体集合内）。"""
    problems: list[str] = []
    for transition in document["transitions"]:
        snapshot = transition["snapshot"]
        where = f"转移 {transition['request_id']}"
        instances = {entity["instance_id"] for entity in snapshot.get("entities") or []}
        for pile in snapshot.get("piles") or []:
            for member in pile.get("visible_members") or []:
                if member not in instances:
                    problems.append(f"{where}: visible_members 悬空引用 {member}")
            positions = pile.get("known_positions") or []
            for position in positions:
                if position["card_ref"] not in instances:
                    problems.append(f"{where}: known_positions 悬空引用 {position['card_ref']}")
                if position["card_ref"] not in (pile.get("visible_members") or []):
                    problems.append(f"{where}: known_positions 不在 visible_members 内 {position['card_ref']}")
            for index in range(1, len(positions)):
                if positions[index - 1]["position"] >= positions[index]["position"]:
                    problems.append(f"{where}: known_positions 未按 position 递增")
        for relation in snapshot.get("relations") or []:
            if relation["source_ref"] not in instances:
                problems.append(f"{where}: relations.source_ref 悬空引用 {relation['source_ref']}")
            if relation["target_ref"] not in instances:
                problems.append(f"{where}: relations.target_ref 悬空引用 {relation['target_ref']}")
        for action in snapshot.get("legal_actions") or []:
            for item in action.get("fields") or []:
                if item.get("state") == "known" and item.get("type") == "entity_ref":
                    if item["value"] not in instances:
                        problems.append(f"{where}: 候选字段悬空引用 {item['value']}")
    return problems


def usable_transitions(document: Mapping[str, Any]) -> tuple[int, list[str]]:
    """可用转移：已确认、概率恰覆盖真实候选、且记录的实际行为概率与候选表一致。"""
    usable = 0
    problems: list[str] = []
    for transition in document["transitions"]:
        if transition["outcome"] != "confirmed":
            continue
        candidates = transition["candidates"]
        if abs(sum(candidates.values()) - 1.0) > 1e-9:
            problems.append(f"转移 {transition['request_id']} 的概率和不为 1")
            continue
        if transition["semantic_key"] not in candidates:
            problems.append(f"转移 {transition['request_id']} 的选中项不在候选内")
            continue
        if abs(transition["behavior_probability"] - candidates[transition["semantic_key"]]) > 1e-12:
            problems.append(f"转移 {transition['request_id']} 的实际行为概率与候选表不一致")
            continue
        usable += 1
    return usable, problems


def identity_of(document: Mapping[str, Any]) -> tuple:
    """跨记录必须一致的身份（身份混用即不可合并）。"""
    identities = document["identities"]
    return (identities["ruleset_id"], identities["catalog_id"], identities["backend_profile_id"],
            identities["dictionary_version"], identities["manifest_digest"])


# -- 审计 ---------------------------------------------------------------------------------------------------


def audit(
    store: EpisodeStore,
    *,
    plan: Optional[SplitPlan] = None,
    attempted: Optional[int] = None,
) -> DataAudit:
    """对一份产物目录做完整审计；所有问题都进 `problems`，不抛异常（除非读不出来）。"""
    result = DataAudit(plan=plan)
    # 审计的对象是**记录目录**，不是索引：磁盘上存在但没进索引的记录同样要被审计到，
    # 索引与记录目录不一致本身就是要报出来的漂移。
    entries = store.rebuild_index()
    disk_entries = store.index()
    if {entry["episode_id"] for entry in disk_entries} != {entry["episode_id"] for entry in entries}:
        result.problems.append("索引与记录目录不一致（有记录没有索引项，或有索引项没有记录）")
    documents: list[dict] = []
    for entry in entries:
        try:
            documents.append(store.read(entry["episode_id"]))
        except RecordError as failure:
            result.problems.append(f"记录不可读: {failure}")

    # 完整性：索引项里的摘要必须与记录一致。
    for entry in disk_entries:
        expected = next((item["record_digest"] for item in entries
                         if item["episode_id"] == entry["episode_id"]), None)
        if entry["record_digest"] != expected:
            result.problems.append(f"索引摘要与记录不一致: {entry['episode_id']}")
    for document in documents:
        for problem in dangling_references(document):
            result.problems.append(f"{document['episode_id']}: {problem}")

    # 身份混用
    identities = {identity_of(document) for document in documents}
    if len(identities) > 1:
        result.problems.append(
            f"同一产物目录里出现 {len(identities)} 种身份（ruleset/catalog/dictionary/backend/Manifest）"
            "：不得合并成一份数据"
        )

    # 奖励字段
    for document in documents:
        found = find_reward_labels(document)
        if found:
            result.problems.append(f"{document['episode_id']}: 记录里出现奖励类字段 {found[:3]}（原始事实与奖励必须分开）")

    # 计数与可用性
    counts = {"records": len(documents), "usable_episodes": 0, "usable_transitions": 0,
              "terminal_win": 0, "terminal_loss": 0, "truncated": 0, "interrupted": 0, "faulted": 0,
              "unknown_transitions": 0}
    for document in documents:
        ending = document["ending"]["class"]
        counts[ending] = counts.get(ending, 0) + 1
        counts["unknown_transitions"] += sum(1 for item in document["transitions"]
                                             if item["outcome"] == "unknown")
        usable, problems = usable_transitions(document)
        for problem in problems:
            result.problems.append(f"{document['episode_id']}: {problem}")
        counts["usable_transitions"] += usable
        # 只有"正常结束且没有不确定转移"的局才算可用样本；中断/故障不混进来。
        if usable > 0 and ending in ("terminal_win", "terminal_loss", "truncated") \
                and not any(item["outcome"] == "unknown" for item in document["transitions"]):
            counts["usable_episodes"] += 1
    counts["attempted"] = attempted if attempted is not None else counts["records"]
    result.counts = counts

    # 划分
    if plan is not None:
        per_split: dict[str, dict] = {split: {"clusters": set(), "episodes": 0, "endings": {}}
                                     for split in SPLITS}
        unassigned = []
        unlabelled = []
        for document in documents:
            cluster = cluster_key(document)
            if cluster.endswith(UNLABELLED_CLUSTER):
                unlabelled.append(document["episode_id"])
            split = plan.split_of(cluster)
            if split is None:
                unassigned.append(cluster)
                continue
            per_split[split]["clusters"].add(cluster)
            per_split[split]["episodes"] += 1
            ending = document["ending"]["class"]
            per_split[split]["endings"][ending] = per_split[split]["endings"].get(ending, 0) + 1
        if unassigned:
            result.problems.append(f"有场景簇未被划分覆盖: {sorted(set(unassigned))}")
        if unlabelled:
            result.problems.append(
                f"有 {len(unlabelled)} 条记录缺少场景簇标签（scenario_variant）："
                "无法判定它们是否与其它局同场景，按同一簇处理会把不同配置混在一起"
            )

        # 跨 split 同簇泄漏：划分是簇 → split 的映射，结构上不应发生；这里独立复核一遍。
        seen: dict[str, str] = {}
        for split, bucket in per_split.items():
            for cluster in bucket["clusters"]:
                if cluster in seen and seen[cluster] != split:
                    result.problems.append(f"场景簇 {cluster} 同时出现在 {seen[cluster]} 与 {split}")
                seen[cluster] = split

        result.split_counts = {
            split: {"episodes": bucket["episodes"], "clusters": sorted(bucket["clusters"]),
                    "endings": dict(bucket["endings"])}
            for split, bucket in per_split.items()
        }
        empty = [split for split, bucket in per_split.items() if bucket["episodes"] == 0]
        if empty:
            result.notes.append(
                f"划分退化：{empty} 没有样本。冻结场景集里的簇数不足以支撑这三个 split，"
                "在补足场景簇之前**不得**把这些数据当作训练/评估划分使用"
            )

    # 偏移：分项分布（报告用，不设未批准的门槛）
    result.distributions = {
        "endings": {ending: counts.get(ending, 0)
                    for ending in ("terminal_win", "terminal_loss", "truncated", "interrupted", "faulted")},
        "steps": _stats([len(document["transitions"]) for document in documents]),
        "candidates": _stats([len(transition["candidates"])
                              for document in documents for transition in document["transitions"]]),
        "seeds": sorted({document["identities"]["seed"] for document in documents}),
    }
    return result


def _stats(values: Sequence[int]) -> dict:
    if not values:
        return {"count": 0, "min": None, "max": None, "mean": None}
    ordered = sorted(int(value) for value in values)
    return {
        "count": len(ordered),
        "min": ordered[0],
        "max": ordered[-1],
        "mean": round(sum(ordered) / len(ordered), 4),
        "p50": ordered[len(ordered) // 2],
    }


def write_audit(audit_result: DataAudit, path: "str | Any") -> Any:
    import pathlib

    target = pathlib.Path(path)
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_bytes(canonical_bytes(audit_result.to_wire()))
    return target


def load_audit(path: "str | Any") -> dict:
    import pathlib

    return json.loads(pathlib.Path(path).read_text(encoding="utf-8"))


def plan_digest_of(assignments: Mapping[str, str]) -> str:
    return hashlib.sha256(canonical_bytes(dict(sorted(assignments.items())))).hexdigest()
