"""版本化 EpisodeRecord 与公开产物索引（D-01）。

职责与边界：

* 记录**只包含当前公开快照**与真实行为概率——不保存隐藏状态、不保存历史缓存、不保存 `known_top`；
* 结束分类把**普通终止**、**显式截断**、**后端故障**与**结果不确定的中断**分开：未知结果不得被补造成
  失败，也不得补造下一状态；
* 每条转移以 `request_id` 为键：同一 request_id 不得重复计数（重复请求是账本去重，不是两个样本）；
* 记录自足：目录 + 全部身份都在记录里，因此可以独立重读、验摘要/引用并重编码（不需要活动宿主）。

**格式版本** `episode-record@1` 与 J-01 的联调轨迹 `j01-trace@1` 是两件事：前者是数据设施，后者是联调快照。
"""

from __future__ import annotations

import hashlib
import json
from dataclasses import dataclass, field
from typing import Any, Mapping, Optional, Sequence

RECORD_FORMAT_VERSION = "episode-record@1"
RUNNER_PROVENANCE = "ai-the-spire/runner@1"

#: 结束分类（互斥；`unknown` 只用于"结果不确定"的**转移**，不是结束分类）。
ENDING_CLASSES = ("terminal_win", "terminal_loss", "truncated", "faulted", "interrupted")

#: 单条转移的结果分类：确认执行 / 结果不确定 / 终止快照（没有动作）。
TRANSITION_OUTCOMES = ("confirmed", "unknown", "terminal")


class RecordError(RuntimeError):
    """记录不合法或不可复核；调用方不得把它当成游戏结果。"""


def canonical_bytes(document: Any) -> bytes:
    return json.dumps(document, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode("utf-8")


def digest_of(document: Any) -> str:
    return "sha256:" + hashlib.sha256(canonical_bytes(document)).hexdigest()


def classify_ending(ending: str, terminal: Optional[Mapping[str, Any]]) -> str:
    """把运行器的 `ending` 映射成互斥的结束分类；**不猜**输赢。"""
    if ending == "terminal":
        if not terminal:
            raise RecordError("terminal 结束必须带 terminal_result")
        outcome = terminal.get("outcome")
        if outcome == "win":
            return "terminal_win"
        if outcome == "loss":
            return "terminal_loss"
        return "faulted"  # outcome 不是 win/loss：不得硬塞进胜负，按故障记录
    if ending in ("truncated", "faulted", "interrupted"):
        return ending
    raise RecordError(f"未知结束状态 {ending!r}（不得默认成终局或失败）")


@dataclass(frozen=True)
class TransitionRecord:
    """一次转移：公开快照 + 实际行为概率 + 结果分类。"""

    index: int
    request_id: str
    observation_id: int
    candidate_id: str
    semantic_key: str
    behavior_probability: float
    candidates: Mapping[str, float]
    outcome: str
    next_observation_id: Optional[int]
    tensor_digest: str
    snapshot: Mapping[str, Any]

    def to_wire(self) -> dict:
        return {
            "index": self.index,
            "request_id": self.request_id,
            "observation_id": self.observation_id,
            "candidate_id": self.candidate_id,
            "semantic_key": self.semantic_key,
            "behavior_probability": self.behavior_probability,
            "candidates": dict(self.candidates),
            "outcome": self.outcome,
            "next_observation_id": self.next_observation_id,
            "tensor_digest": self.tensor_digest,
            "snapshot": dict(self.snapshot),
        }

    @classmethod
    def from_wire(cls, document: Mapping[str, Any]) -> "TransitionRecord":
        return cls(
            index=document["index"],
            request_id=document["request_id"],
            observation_id=document["observation_id"],
            candidate_id=document["candidate_id"],
            semantic_key=document["semantic_key"],
            behavior_probability=document["behavior_probability"],
            candidates=dict(document["candidates"]),
            outcome=document["outcome"],
            next_observation_id=document["next_observation_id"],
            tensor_digest=document["tensor_digest"],
            snapshot=document["snapshot"],
        )

    def digest_view(self) -> dict:
        """摘要输入：完整存储内容，但快照按控制元数据归一化（`session_id` 不进身份）。"""
        document = self.to_wire()
        document["snapshot"] = strip_control_metadata(self.snapshot)
        return document


@dataclass(frozen=True)
class EpisodeRecord:
    """一局的公开记录。`record_digest` 覆盖除它自身以外的全部内容。"""

    identities: Mapping[str, Any]
    provenance: Mapping[str, Any]
    catalog: Mapping[str, Any]
    ending_class: str
    ending_detail: str
    terminal: Optional[Mapping[str, Any]]
    transitions: Sequence[TransitionRecord]
    record_digest: str = ""

    # -- 派生 ----------------------------------------------------------------------------------------------

    @property
    def episode_id(self) -> str:
        """由**内容身份**派生（目录/字典/种子/结束/转移摘要），不含时间戳或进程信息。"""
        material = {
            "record_format_version": RECORD_FORMAT_VERSION,
            "ruleset_id": self.identities.get("ruleset_id"),
            "catalog_id": self.identities.get("catalog_id"),
            "dictionary_version": self.identities.get("dictionary_version"),
            "seed": self.identities.get("seed"),
            "executor_profile_id": self.identities.get("executor_profile_id"),
            "ending_class": self.ending_class,
            "transitions": [transition.tensor_digest for transition in self.transitions],
        }
        return "ep-" + hashlib.sha256(canonical_bytes(material)).hexdigest()[:24]

    @property
    def confirmed_transitions(self) -> int:
        return sum(1 for transition in self.transitions if transition.outcome == "confirmed")

    @property
    def unknown_transitions(self) -> int:
        return sum(1 for transition in self.transitions if transition.outcome == "unknown")

    def to_wire(self) -> dict:
        return {
            "record_format_version": RECORD_FORMAT_VERSION,
            "episode_id": self.episode_id,
            "identities": dict(self.identities),
            "provenance": dict(self.provenance),
            "catalog": dict(self.catalog),
            "ending": {
                "class": self.ending_class,
                "detail": self.ending_detail,
                "terminal": dict(self.terminal) if self.terminal else None,
            },
            "summary": {
                "steps": len(self.transitions),
                "confirmed_transitions": self.confirmed_transitions,
                "unknown_transitions": self.unknown_transitions,
            },
            "transitions": [transition.to_wire() for transition in self.transitions],
            "record_digest": self.digest(),
        }

    def digest(self) -> str:
        return digest_of(self.digest_material())

    def digest_material(self) -> dict:
        document = {
            "record_format_version": RECORD_FORMAT_VERSION,
            "episode_id": self.episode_id,
            "identities": dict(self.identities),
            "provenance": dict(self.provenance),
            "catalog": dict(self.catalog),
            "ending": {
                "class": self.ending_class,
                "detail": self.ending_detail,
                "terminal": dict(self.terminal) if self.terminal else None,
            },
            "transitions": [transition.digest_view() for transition in self.transitions],
        }
        return document

    @classmethod
    def from_wire(cls, document: Mapping[str, Any]) -> "EpisodeRecord":
        validate_document(document)
        return cls(
            identities=document["identities"],
            provenance=document["provenance"],
            catalog=document["catalog"],
            ending_class=document["ending"]["class"],
            ending_detail=document["ending"]["detail"],
            terminal=document["ending"]["terminal"],
            transitions=[TransitionRecord.from_wire(item) for item in document["transitions"]],
            record_digest=document["record_digest"],
        )


def validate_document(document: Mapping[str, Any]) -> None:
    """结构与语义校验：缺键、版本不符、分类非法、重复 request_id、未知结果被补造，全部拒绝。"""
    if not isinstance(document, Mapping):
        raise RecordError("记录必须是对象")
    for key in ("record_format_version", "episode_id", "identities", "provenance", "catalog", "ending",
                "summary", "transitions", "record_digest"):
        if key not in document:
            raise RecordError(f"记录缺少必需键: {key}")
    if document["record_format_version"] != RECORD_FORMAT_VERSION:
        raise RecordError(
            f"记录格式版本 {document['record_format_version']!r} 与当前 {RECORD_FORMAT_VERSION!r} 不同"
        )
    ending = document["ending"]
    if ending.get("class") not in ENDING_CLASSES:
        raise RecordError(f"结束分类非法: {ending.get('class')!r}")
    if ending["class"] in ("terminal_win", "terminal_loss") and not ending.get("terminal"):
        raise RecordError("终止类记录必须带 terminal_result")
    if ending["class"] not in ("terminal_win", "terminal_loss") and ending.get("terminal"):
        raise RecordError("非终止类记录不得带 terminal_result（不得把故障/截断伪装成胜负）")

    seen: set[str] = set()
    for index, transition in enumerate(document["transitions"]):
        request_id = transition.get("request_id")
        if not request_id:
            raise RecordError(f"转移 {index} 缺少 request_id")
        if request_id in seen:
            raise RecordError(f"重复的 request_id 会被重复计样本: {request_id}")
        seen.add(request_id)
        if transition.get("outcome") not in TRANSITION_OUTCOMES:
            raise RecordError(f"转移 {request_id} 的结果分类非法: {transition.get('outcome')!r}")
        if transition["outcome"] == "unknown":
            if transition.get("next_observation_id") is not None:
                raise RecordError(f"结果不确定的转移不得补造下一状态: {request_id}")
            if transition.get("candidate_id"):
                raise RecordError(f"结果不确定的转移不得声称已执行某个候选: {request_id}")
        if "snapshot" not in transition:
            raise RecordError(f"转移 {request_id} 必须保存完整公开快照")
        if transition.get("tensor_digest", "").find("sha256:") != 0:
            raise RecordError(f"转移 {request_id} 缺少张量摘要")


def strip_control_metadata(snapshot: Mapping[str, Any]) -> dict:
    """去掉**控制元数据**后的公开快照（当前只含 `session_id`）。

    `session_id` 是控制通道的会话句柄：它按进程随机，而 `contract-artifacts §3` 明确要求跨会话重放时
    "排除 session_id、observation_id 和不透明句柄的字符串差异"。它必须留在**存储**里（快照结构要求它，
    重编码也需要完整快照），但**不得进入记录摘要**——否则同一配置的两次重放会产出不同摘要，
    "重复 seed+动作的重放按声明能力成立"与数据去重都会被破坏。
    """
    document = dict(snapshot)
    document.pop("session_id", None)
    return document


def build_record(
    result: Any,
    *,
    provenance: Optional[Mapping[str, Any]] = None,
    ending_class: Optional[str] = None,
    ending_detail: Optional[str] = None,
) -> EpisodeRecord:
    """把 J-01 的 `EpisodeResult` 转成 D-01 记录（**不新增任何未观察到的信息**）。

    `result` 只需提供 `identities`/`catalog`/`ending`/`ending_detail`/`transitions`——
    因此这里不依赖 runner 的具体类型，避免数据模块反向依赖执行模块。
    """
    identities = result.identities.to_wire() if hasattr(result.identities, "to_wire") else dict(result.identities)
    terminal = None
    for transition in result.transitions:
        if getattr(transition, "terminal", None):
            terminal = transition.terminal
    resolved_class = ending_class or classify_ending(result.ending, terminal)
    if ending_class is None and resolved_class == "faulted" and result.ending == "faulted":
        # 后端故障既不是胜负也不是截断：分类保持 faulted，详情保留原始错误。
        pass

    transitions: list[TransitionRecord] = []
    for index, transition in enumerate(result.transitions):
        probability = transition.probabilities.get(transition.chosen_semantic_key) if transition.probabilities else None
        if transition.status == "terminal":
            outcome = "terminal"
        elif transition.status == "unknown":
            outcome = "unknown"
        else:
            outcome = "confirmed"
        transitions.append(
            TransitionRecord(
                index=index,
                request_id=transition.request_id,
                observation_id=transition.observation_id,
                # 结果不确定的转移**不声称**已执行某个候选（候选句柄是"执行过"的声明）。
                candidate_id="" if outcome == "unknown" else transition.chosen_candidate_id,
                semantic_key=transition.chosen_semantic_key,
                behavior_probability=float(probability) if probability is not None else 0.0,
                candidates=dict(transition.probabilities),
                outcome=outcome,
                next_observation_id=None if outcome == "unknown" else transition.next_observation_id,
                tensor_digest=transition.tensor_digest,
                snapshot=dict(transition.snapshot),
            )
        )

    document_provenance = dict(provenance or {})
    document_provenance.setdefault("runner", RUNNER_PROVENANCE)
    document_provenance.setdefault("policy", identities.get("model_id"))
    document_provenance.setdefault("executor_profile_id", identities.get("executor_profile_id"))
    return EpisodeRecord(
        identities=identities,
        provenance=document_provenance,
        catalog=dict(result.catalog),
        ending_class=resolved_class,
        ending_detail=ending_detail if ending_detail is not None else result.ending_detail,
        terminal=terminal,
        transitions=transitions,
    )
