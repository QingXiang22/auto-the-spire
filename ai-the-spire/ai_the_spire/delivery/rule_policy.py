"""D2 的**公开信息规则基线**：明确版本化、只用当前公开合法候选与已发布字段。

它是什么 / 不是什么：

* 是一个**规则基线**，用来回答"这些受控种子上到底有没有可获胜路径"；
* **不是**学习模型、**不是**训练标签、**不是**故障兜底：它不参与 `Trainer` 更新，
  也不在任何技术故障时被自动调用；
* 它**不调用规则内核**估算隐藏收益，只读当前快照里**已发布**的字段
  （牌的公开 `Kind`/`Amount`、敌人的公开 `Hp`）与当前合法候选；
* 决策确定性来自 one-hot 概率（选中项 1、其余 0），因此执行器选它不依赖随机数；
  同一执行器、同一限额，没有额外特权。

规则版本 ``d2:rule:public-info-greedy@1`` 的完整优先级（记录在产物里，便于复现与比较）：

1. 存在**决定类**候选（`…answer_decision`，例如选目标/选牌）时：只在已发布的选项里选；
   选项指向公开 `Monster` 实例时取公开 `Hp` **最低**者；其余按 canonical 语义键升序取第一项。
2. 否则存在**可打出的攻击牌**（`PlayCard` 且被引用牌的公开 `Kind == net:cat:Attack`）时：
   按公开 `Amount` **降序**；`Amount` 相同按 canonical 语义键升序取第一项。
3. 否则存在其它 `PlayCard` 候选时：按 canonical 语义键升序取第一项。
4. 否则结束回合（`EndTurn`；同类内按 canonical 语义键升序取第一项）。
5. 其它未知动作类型排在最后（按语义键升序），保证任何合法候选集合都有确定选择。

**已知边界**：它不评估"打这张牌值不值"，也不做跨回合规划；它只是可复现的上限参考。
"""

from __future__ import annotations

from dataclasses import dataclass
from typing import Any, Callable, Mapping, Sequence

from .console import entity_map, short_id

#: 规则基线身份（进轨迹与计划；**不是**模型身份）。
RULE_POLICY_ID = "d2:rule:public-info-greedy@1"

#: 规则版本（决策优先级变化必须换版本，否则历史对局不可比）。
RULE_VERSION = "1"

ATTACK_CATEGORY = "net:cat:Attack"
MONSTER_KIND = "net:kind:base-spire:Monster"
DECISION_ACTION_SUFFIX = "answer_decision"

CARD_AMOUNT_FIELD = "net:field:base-spire:Card.Amount"
CARD_KIND_FIELD = "net:field:base-spire:Card.Kind"
MONSTER_HP_FIELD = "net:field:base-spire:Monster.Hp"


def rule_spec() -> dict:
    """规则的完整版本与优先级（写进 `diagnosis.json`，可核对）。"""
    return {
        "rule_policy_id": RULE_POLICY_ID,
        "version": RULE_VERSION,
        "kind": "rule_baseline（公开信息，明确不是学习模型、不是训练标签、不是故障兜底）",
        "inputs": "当前快照的合法候选 + 已发布字段（Card.Kind / Card.Amount / Monster.Hp）",
        "does_not": ["调用规则内核估算隐藏收益", "读取隐藏状态", "使用历史或记忆", "使用随机数"],
        "priority": [
            "1. 决定类候选（answer_decision）：按被选项指向的公开 Monster.Hp 升序；其余按语义键升序",
            "2. 攻击牌（PlayCard 且被引用牌 Card.Kind == net:cat:Attack）：按公开 Card.Amount 降序",
            "3. 其它 PlayCard：按语义键升序",
            "4. EndTurn：按语义键升序",
            "5. 未知动作类型：按语义键升序（保证任何候选集合都有确定选择）",
        ],
        "tie_break": "同优先级内一律按 canonical 动作语义键（UTF-8 字节序）升序取第一项",
        "determinism": "返回选中项的 one-hot 概率（其余 0），覆盖且只覆盖当前合法候选",
    }


@dataclass(frozen=True)
class CandidateFacts:
    """一个候选上**已发布**的事实（缺失就是未知，不推断）。"""

    action_type: str
    semantic_key: str
    referenced_instance: str | None
    is_attack_card: bool
    card_amount: float | None
    target_hp: float | None


def candidate_facts(action: Mapping[str, Any]) -> dict:
    """从候选动作里取出规则需要的公开事实（只读字段，不推断效果）。"""
    action_type = short_id(action.get("action_type"))
    referenced = None
    for field in action.get("fields") or []:
        if field.get("state") != "known":
            continue
        field_id = str(field.get("field_id") or "")
        if field_id.endswith(".Card") or field_id.endswith(".Instance"):
            referenced = str(field.get("value"))
            break
    return {
        "action_type": action_type,
        "referenced_instance": referenced,
        "is_decision": action_type.endswith(DECISION_ACTION_SUFFIX),
        "is_play_card": action_type.endswith("PlayCard"),
        "is_end_turn": action_type.endswith("EndTurn"),
    }


def _fields_of(entity: Mapping[str, Any] | None) -> dict:
    if not entity:
        return {}
    return {field.get("field_id"): field.get("value")
            for field in entity.get("fields") or [] if field.get("state") == "known"}


def _number(entity: Mapping[str, Any] | None, field_id: str):
    value = _fields_of(entity).get(field_id)
    if value is None:
        return None
    try:
        return float(value)
    except (TypeError, ValueError):
        return None


def rank_of(action: Mapping[str, Any], semantic_key: str,
            by_id: Mapping[str, Mapping[str, Any]]) -> tuple:
    """候选的排序键（越小越先选）。返回值同时用于**记录规则为什么这么选**。"""
    facts = candidate_facts(action)
    referenced = by_id.get(facts["referenced_instance"]) if facts["referenced_instance"] else None
    amount = _number(referenced, CARD_AMOUNT_FIELD)
    kind = _fields_of(referenced).get(CARD_KIND_FIELD)
    target_hp = _number(referenced, MONSTER_HP_FIELD)
    is_attack = bool(kind == ATTACK_CATEGORY)

    if facts["is_decision"]:
        # 目标/选项类：优先公开 HP 最低者；无法解析 HP 的排在其后。
        return (0, 0.0 if target_hp is None else 1.0, target_hp if target_hp is not None else 0.0,
                semantic_key)
    if facts["is_play_card"] and is_attack:
        return (1, 0.0, -(amount if amount is not None else float("-inf")), semantic_key)
    if facts["is_play_card"]:
        return (2, 0.0, 0.0, semantic_key)
    if facts["is_end_turn"]:
        return (3, 0.0, 0.0, semantic_key)
    return (4, 0.0, 0.0, semantic_key)


def make_rule_policy() -> Callable[[Mapping[str, Any], Sequence[Any]], dict]:
    """构造规则策略：`(公开快照, 当前合法候选) -> {语义键: one-hot 概率}`。"""

    def policy(snapshot: Mapping[str, Any], candidates: Sequence[Any]) -> dict:
        if not candidates:
            raise ValueError("规则策略收到空候选集合：调用方不得为终止/故障快照伪造概率")
        by_id = entity_map(snapshot)
        ranked = sorted(
            candidates,
            key=lambda candidate: rank_of(candidate.action, candidate.semantic_key_text, by_id),
        )
        chosen = ranked[0]
        return {candidate.semantic_key_text: (1.0 if candidate is chosen else 0.0)
                for candidate in candidates}

    return policy


def explain(snapshot: Mapping[str, Any], candidates: Sequence[Any]) -> dict:
    """规则在某一局面的选择与理由（用于可读对局里的"为什么这么打"）。"""
    by_id = entity_map(snapshot)
    ranked = sorted(
        candidates,
        key=lambda candidate: rank_of(candidate.action, candidate.semantic_key_text, by_id),
    )
    chosen = ranked[0]
    facts = candidate_facts(chosen.action)
    reason = {
        "decision": "决定类候选：选公开 HP 最低的已发布选项",
        "attack": "攻击牌：选公开 Amount 最高的那张",
        "other_card": "其它可打出的牌：按语义键升序取第一项",
        "end_turn": "没有可打出的牌：结束回合",
        "unknown": "未知动作类型：按语义键升序取第一项",
    }
    kind = ("decision" if facts["is_decision"] else
            "attack" if (facts["is_play_card"] and rank_of(
                chosen.action, chosen.semantic_key_text, by_id)[0] == 1) else
            "other_card" if facts["is_play_card"] else
            "end_turn" if facts["is_end_turn"] else "unknown")
    return {
        "rule_policy_id": RULE_POLICY_ID,
        "rule_version": RULE_VERSION,
        "chosen_semantic_key": chosen.semantic_key_text,
        "chosen_action_type": facts["action_type"],
        "chosen_instance": facts["referenced_instance"],
        "reason": reason[kind],
        "candidates": len(candidates),
    }
