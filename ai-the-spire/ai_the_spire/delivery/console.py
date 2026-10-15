"""公开快照 → 可读文本（D1-A 的"人能看到发生了什么"）。

严格边界（任务包 §3）：

* **只读公开快照与公开 catalog**：不解析 DSL 推断效果、不展示内部规则或隐藏信息、
  不读隐藏状态、不用 AI 历史补洞；
* 显示名优先取 catalog 的公开 `description`；它与 id 相同时就显示**公开 id**（不编造名字）；
* 无动作的终止快照单独成段，**不计一个假动作**；
* "执行后的公开变化"只由前后两份公开快照**做差**得到，不做任何规则推断。
"""

from __future__ import annotations

from typing import Any, Mapping, Optional, Sequence

PLAYER_KIND = "net:kind:base-spire:Player"
MONSTER_KIND = "net:kind:base-spire:Monster"
CARD_KIND = "net:kind:base-spire:Card"
POWER_KIND = "net:kind:base-spire:Power"


# --------------------------------------------------------------------------- #
# 公开目录的显示名
# --------------------------------------------------------------------------- #
def catalog_labels(catalog: Mapping[str, Any]) -> dict[str, str]:
    """内容 id → 显示名：`description` 与 id 不同才用描述，否则显示公开 id（不编造）。"""
    labels: dict[str, str] = {}
    declarations = (catalog or {}).get("declarations") or {}
    for item in declarations.get("content_types") or []:
        content_id = item.get("id")
        description = item.get("description")
        labels[content_id] = description if isinstance(description, str) and description.strip() \
            and description != content_id else content_id
    return labels


def pile_kind_labels(catalog: Mapping[str, Any]) -> dict[str, str]:
    """牌堆实体 kind_id → 牌区短名（来自 `pile_types` 的公开声明）。"""
    labels: dict[str, str] = {}
    declarations = (catalog or {}).get("declarations") or {}
    for item in declarations.get("pile_types") or []:
        pile_type_id = str(item.get("id") or "")
        kind_id = item.get("kind_id")
        if kind_id:
            labels[str(kind_id)] = short_id(pile_type_id)
    return labels


def short_id(identifier: Any) -> str:
    """公开 id 的显示短名：只取最后一个分隔符之后的部分（纯显示，不改变身份）。"""
    text = str(identifier)
    for separator in (":", "/"):
        if separator in text:
            text = text.rsplit(separator, 1)[-1]
    return text


# --------------------------------------------------------------------------- #
# 快照读取助手
# --------------------------------------------------------------------------- #
def entities_of(snapshot: Mapping[str, Any]) -> list[dict]:
    return list(snapshot.get("entities") or [])


def entity_map(snapshot: Mapping[str, Any]) -> dict[str, dict]:
    return {entity["instance_id"]: entity for entity in entities_of(snapshot)}


def field_value(entity: Optional[Mapping[str, Any]], field_id: str) -> Optional[Any]:
    if not entity:
        return None
    for field in entity.get("fields") or []:
        if field.get("field_id") == field_id and field.get("state") == "known":
            return field.get("value")
    return None


def content_of(entity: Optional[Mapping[str, Any]]) -> Optional[str]:
    if not entity or entity.get("content_state") != "known":
        return None
    return entity.get("content_id")


def entity_label(entity: Mapping[str, Any], labels: Mapping[str, str]) -> str:
    """实体显示名：有公开内容 id 就用它（可查目录显示名）；否则退回 kind 短名（不编造内容）。"""
    content_id = content_of(entity)
    if content_id is None:
        return short_id(entity.get("kind_id"))
    return labels.get(content_id, content_id)


def piles_of(snapshot: Mapping[str, Any]) -> dict[str, dict]:
    return {pile["pile_ref"]: pile for pile in snapshot.get("piles") or []}


def _number(entity: Optional[Mapping[str, Any]], field_id: str) -> Optional[float]:
    value = field_value(entity, field_id)
    if value is None:
        return None
    try:
        return float(value)
    except (TypeError, ValueError):
        return None


def _fmt(value: Optional[float]) -> str:
    if value is None:
        return "未公开"
    return str(int(value)) if float(value) == int(value) else f"{value:g}"


# --------------------------------------------------------------------------- #
# 局面渲染
# --------------------------------------------------------------------------- #
def render_player(snapshot: Mapping[str, Any], labels: Mapping[str, str]) -> list[str]:
    player = next((entity for entity in entities_of(snapshot) if entity.get("kind_id") == PLAYER_KIND), None)
    if player is None:
        return ["玩家：当前快照没有公开 Player 实体"]
    hp = _number(player, "net:field:base-spire:Player.Hp")
    max_hp = _number(player, "net:field:base-spire:Player.MaxHp")
    block = _number(player, "net:field:base-spire:Player.Block")
    energy = _number(player, "net:field:base-spire:Player.Energy")
    max_energy = _number(player, "net:field:base-spire:Player.MaxEnergy")
    turn = _number(player, "net:field:base-spire:Player.Turn")
    powers = powers_of(snapshot, player["instance_id"], labels)
    line = (f"玩家 {player['instance_id']}：HP {_fmt(hp)}/{_fmt(max_hp)}  格挡 {_fmt(block)}  "
            f"能量 {_fmt(energy)}/{_fmt(max_energy)}  回合 {_fmt(turn)}")
    if powers:
        line += "  能力 [" + "、".join(powers) + "]"
    return [line]


def powers_of(snapshot: Mapping[str, Any], owner_ref: str, labels: Mapping[str, str]) -> list[str]:
    """按公开 `relations`（Powers）+ Power 实体公开字段渲染持有者的能力。"""
    by_id = entity_map(snapshot)
    rendered: list[str] = []
    for relation in snapshot.get("relations") or []:
        if relation.get("source_ref") != owner_ref:
            continue
        target = by_id.get(relation.get("target_ref"))
        if target is None or target.get("kind_id") != POWER_KIND:
            continue
        stacks = _number(target, "net:field:base-spire:Power.Stacks")
        ticks = field_value(target, "net:field:base-spire:Power.Ticks")
        suffix = f" x{_fmt(stacks)}" if stacks is not None else ""
        if ticks:
            suffix += "（回合末移除）"
        rendered.append(f"{entity_label(target, labels)}{suffix}")
    return sorted(rendered)


def render_monsters(snapshot: Mapping[str, Any], labels: Mapping[str, str]) -> list[str]:
    lines: list[str] = []
    for entity in entities_of(snapshot):
        if entity.get("kind_id") != MONSTER_KIND:
            continue
        hp = _number(entity, "net:field:base-spire:Monster.Hp")
        max_hp = _number(entity, "net:field:base-spire:Monster.MaxHp")
        block = _number(entity, "net:field:base-spire:Monster.Block")
        move = field_value(entity, "net:field:base-spire:Monster.NextMove")
        strike = _number(entity, "net:field:base-spire:Monster.StrikeDamage")
        powers = powers_of(snapshot, entity["instance_id"], labels)
        line = (f"  - {entity['instance_id']} {entity_label(entity, labels)}："
                f"HP {_fmt(hp)}/{_fmt(max_hp)}  格挡 {_fmt(block)}")
        if move is not None:
            line += f"  意图 {short_id(move)}"
        if strike is not None:
            line += f"（打击伤害 {_fmt(strike)}）"
        if powers:
            line += "  能力 [" + "、".join(powers) + "]"
        lines.append(line)
    return lines or ["  （没有公开 Monster 实体）"]


def render_piles(snapshot: Mapping[str, Any], catalog: Mapping[str, Any],
                 labels: Mapping[str, str]) -> list[str]:
    by_id = entity_map(snapshot)
    kind_labels = pile_kind_labels(catalog)
    lines: list[str] = []
    for pile in snapshot.get("piles") or []:
        pile_ref = pile["pile_ref"]
        kind_id = (by_id.get(pile_ref) or {}).get("kind_id")
        name = kind_labels.get(str(kind_id), short_id(kind_id))
        visible = list(pile.get("visible_members") or [])
        total = pile.get("total_count")
        completeness = "全部可见" if pile.get("members_complete") else "成员不完整"
        line = f"  - {name}({pile_ref})：{_fmt(total)} 张 {completeness}"
        if visible:
            shown = "、".join(
                f"{member}({entity_label(by_id[member], labels)})" if member in by_id else member
                for member in visible
            )
            line += f" 可见 [{shown}]"
        known = pile.get("known_positions") or []
        if known:
            positions = "、".join(
                f"{short_id(item.get('member_ref'))}@{item.get('position')}" for item in known
            )
            line += f" 已知位置 [{positions}]"
        lines.append(line)
    return lines or ["  （当前快照没有公开牌区）"]


def render_hand(snapshot: Mapping[str, Any], catalog: Mapping[str, Any],
                labels: Mapping[str, str]) -> list[str]:
    """手牌：名称/公开 id + 实例属性（费用、数值、类别、升级、不可打出等公开字段）。"""
    by_id = entity_map(snapshot)
    kind_labels = pile_kind_labels(catalog)
    hand_ref = None
    for pile in snapshot.get("piles") or []:
        kind_id = (by_id.get(pile["pile_ref"]) or {}).get("kind_id")
        if kind_labels.get(str(kind_id)) == "hand":
            hand_ref = pile["pile_ref"]
            break
    if hand_ref is None:
        return ["  （当前快照没有公开手牌区）"]
    members = list((piles_of(snapshot).get(hand_ref) or {}).get("visible_members") or [])
    if not members:
        return ["  （手牌为空）"]
    lines: list[str] = []
    for member in members:
        entity = by_id.get(member)
        if entity is None:
            lines.append(f"  - {member}（快照里没有该实例的公开字段）")
            continue
        attributes: list[str] = []
        for field_id, name in (
            ("net:field:base-spire:Card.Cost", "费用"),
            ("net:field:base-spire:Card.Amount", "数值"),
            ("net:field:base-spire:Card.Kind", "类别"),
            ("net:field:base-spire:Card.CostModifier", "费用修正"),
            ("net:field:base-spire:Card.PowerStacks", "能力层数"),
        ):
            value = field_value(entity, field_id)
            if value is None:
                continue
            attributes.append(f"{name} {short_id(value) if 'Kind' in field_id else value}")
        for field_id, name in (("net:field:base-spire:Card.Upgraded", "已升级"),
                               ("net:field:base-spire:Card.Ethereal", "虚无"),
                               ("net:field:base-spire:Card.Unplayable", "不可打出")):
            value = field_value(entity, field_id)
            if value is True:
                attributes.append(name)
        suffix = ("  " + "  ".join(attributes)) if attributes else ""
        lines.append(f"  - {member} {entity_label(entity, labels)}{suffix}")
    return lines


def render_groups(snapshot: Mapping[str, Any], labels: Mapping[str, str]) -> list[str]:
    lines: list[str] = []
    for group in snapshot.get("groups") or []:
        members = list(group.get("members") or [])
        lines.append(f"  - {group.get('type_id')}：{len(members)} 项 {members}")
    return lines


def render_snapshot(snapshot: Mapping[str, Any], catalog: Mapping[str, Any],
                    labels: Mapping[str, str]) -> list[str]:
    """一份公开快照的完整可读局面（不含任何隐藏信息）。"""
    lines = [f"observation_id={snapshot.get('observation_id')}  status={snapshot.get('status')}"]
    lines.extend(render_player(snapshot, labels))
    lines.append("敌人：")
    lines.extend(render_monsters(snapshot, labels))
    lines.append("手牌：")
    lines.extend(render_hand(snapshot, catalog, labels))
    lines.append("牌区：")
    lines.extend(render_piles(snapshot, catalog, labels))
    groups = render_groups(snapshot, labels)
    if groups:
        lines.append("组：")
        lines.extend(groups)
    return lines


# --------------------------------------------------------------------------- #
# 候选渲染
# --------------------------------------------------------------------------- #
def render_candidate(action: Mapping[str, Any], labels: Mapping[str, str]) -> str:
    """候选的短形式：`动作短名(字段=值)`；字段值若是公开实例句柄，附上它的内容显示名。"""
    parts: list[str] = []
    for field in action.get("fields") or []:
        field_name = short_id(field.get("field_id"))
        value = field.get("value") if field.get("state") == "known" else None
        parts.append(f"{field_name}={value}")
    return f"{short_id(action.get('action_type'))}({', '.join(parts)})"


def render_candidates(snapshot: Mapping[str, Any], probabilities: Mapping[str, float],
                      labels: Mapping[str, str]) -> list[str]:
    by_id = entity_map(snapshot)
    lines: list[str] = []
    for action in snapshot.get("legal_actions") or []:
        text = render_candidate(action, labels)
        reference = _referenced_instance(action)
        if reference and reference in by_id:
            text += f"  → {reference} {entity_label(by_id[reference], labels)}"
        probability = probabilities.get(_semantic_key_text(action))
        lines.append(f"  - [{action.get('candidate_id')}] {text}  p={probability!r}")
    return lines or ["  （该快照没有候选）"]


def _referenced_instance(action: Mapping[str, Any]) -> Optional[str]:
    for field in action.get("fields") or []:
        if field.get("state") == "known" and str(field.get("field_id", "")).endswith(".Card"):
            return str(field.get("value"))
    return None


def _semantic_key_text(action: Mapping[str, Any]) -> str:
    from ..contracts import layers

    return layers.action_semantic_key(dict(action)).decode("utf-8")


# --------------------------------------------------------------------------- #
# 前后快照的公开差异
# --------------------------------------------------------------------------- #
def public_diff(before: Mapping[str, Any], after: Mapping[str, Any],
                labels: Mapping[str, str]) -> list[str]:
    """两份公开快照的差：只报**公开字段/牌区/关系/终局**的变化，不推断规则。"""
    lines: list[str] = []
    before_entities = entity_map(before)
    after_entities = entity_map(after)

    for instance, entity in sorted(after_entities.items(), key=_instance_sort_key):
        old = before_entities.get(instance)
        label = entity_label(entity, labels)
        if old is None:
            lines.append(f"  + 新增实体 {instance} {label}")
            continue
        for field in entity.get("fields") or []:
            field_id = field.get("field_id")
            new_value = field.get("value") if field.get("state") == "known" else None
            old_value = None
            for previous in old.get("fields") or []:
                if previous.get("field_id") == field_id:
                    old_value = previous.get("value") if previous.get("state") == "known" else None
                    break
            if new_value != old_value:
                lines.append(
                    f"  ~ {instance} {label} {short_id(field_id)}: {old_value} → {new_value}"
                )
    for instance, entity in sorted(before_entities.items(), key=_instance_sort_key):
        if instance not in after_entities:
            lines.append(f"  - 移除实体 {instance} {entity_label(entity, labels)}")

    before_piles, after_piles = piles_of(before), piles_of(after)
    for reference in sorted(set(before_piles) | set(after_piles)):
        old_pile, new_pile = before_piles.get(reference), after_piles.get(reference)
        old_count = (old_pile or {}).get("total_count")
        new_count = (new_pile or {}).get("total_count")
        old_visible = list((old_pile or {}).get("visible_members") or [])
        new_visible = list((new_pile or {}).get("visible_members") or [])
        if old_count != new_count or old_visible != new_visible:
            delta = (f"  {old_count} → {new_count}" if old_count != new_count else "  成员变化")
            lines.append(f"  ~ 牌区 {reference}:{delta}  可见 {old_visible} → {new_visible}")

    before_relations = {_relation_key(item) for item in before.get("relations") or []}
    after_relations = {_relation_key(item) for item in after.get("relations") or []}
    for key in sorted(after_relations - before_relations):
        lines.append(f"  + 新增关系 {key}")
    for key in sorted(before_relations - after_relations):
        lines.append(f"  - 移除关系 {key}")

    if before.get("terminal_result") != after.get("terminal_result"):
        lines.append(f"  ~ 终局结果: {before.get('terminal_result')} → {after.get('terminal_result')}")
    if not lines:
        lines.append("  （公开快照没有可见变化）")
    return lines


def _instance_sort_key(instance: str) -> tuple:
    text = str(instance)
    digits = "".join(character for character in text if character.isdigit())
    return (0, int(digits)) if digits else (1, text)


def _relation_key(relation: Mapping[str, Any]) -> str:
    return f"{relation.get('source_ref')} -{short_id(relation.get('type_id'))}-> {relation.get('target_ref')}"


def render_terminal(snapshot: Mapping[str, Any], labels: Mapping[str, str]) -> list[str]:
    terminal = snapshot.get("terminal_result") or {}
    outcome = terminal.get("outcome")
    reason = terminal.get("reason_id")
    return [
        f"终止快照 observation_id={snapshot.get('observation_id')}",
        f"  结果 outcome={outcome}  reason={reason}  实体 {len(entities_of(snapshot))} 个",
        "  （终止快照没有动作，不计一个假动作）",
    ]


# --------------------------------------------------------------------------- #
# 整局渲染（demo 与 diagnose 共用：逐决策块 + 终止块）
# --------------------------------------------------------------------------- #
def confirmed_action_count(result: Any) -> int:
    """已确认的外部动作数（终止快照不是动作，结果不确定的转移不算已确认）。"""
    return sum(1 for transition in result.transitions
               if transition.status not in ("terminal", "unknown"))


def action_after_snapshot(result: Any, index: int) -> Optional[Mapping[str, Any]]:
    """第 `index` 个动作的**真实后快照**；没有就返回 None（不推断、不补造）。"""
    if index + 1 < len(result.transitions):
        return result.transitions[index + 1].snapshot
    return result.final_snapshot


def turn_of(snapshot: Mapping[str, Any]) -> Optional[str]:
    for entity in snapshot.get("entities") or []:
        if entity.get("kind_id") != PLAYER_KIND:
            continue
        for field in entity.get("fields") or []:
            if field.get("field_id") == "net:field:base-spire:Player.Turn" \
                    and field.get("state") == "known":
                return str(field.get("value"))
    return None


def render_episode_body(result: Any, catalog: Mapping[str, Any],
                        labels: Mapping[str, str]) -> list[str]:
    """逐决策可读块 + 终止块（**不含**头尾的身份/结论段，调用方自己拼）。"""
    lines: list[str] = []
    for index, transition in enumerate(result.transitions):
        if transition.status == "terminal":
            lines.append("## 终止")
            lines.extend(render_terminal(transition.snapshot, labels))
            lines.append("")
            continue
        if transition.status == "unknown":
            lines.append(f"## 动作 {index}（结果不确定，未确认执行）")
            lines.append("  该请求的结果不确定：只记录已发出的语义动作与实际概率，"
                         "不声称执行过，也不补造后状态。")
            lines.append("")
            continue

        snapshot = transition.snapshot
        turn = turn_of(snapshot)
        lines.append(f"## 动作 {index}" + (f" · 回合 {turn}" if turn is not None else ""))
        lines.append("")
        lines.extend(render_snapshot(snapshot, catalog, labels))
        lines.append("")
        lines.append(f"合法候选（{len(transition.candidates)}）：")
        lines.extend(render_candidates(snapshot, transition.probabilities, labels))
        lines.append("")
        chosen_probability = transition.probabilities.get(transition.chosen_semantic_key)
        lines.append(
            f"实际选择：`{transition.chosen_semantic_key}`"
            f"  π={chosen_probability!r}  μ={chosen_probability!r}（首版 μ=π，同一冻结模型）"
            f"  candidate_id={transition.chosen_candidate_id}"
        )
        after = action_after_snapshot(result, index)
        lines.append("")
        if after is None:
            lines.append("执行后变化：**没有可确认的后快照**（结果不确定或资源限额停止），"
                         "不推断、不补造。")
        else:
            lines.append("执行后公开变化：")
            lines.extend(public_diff(snapshot, after, labels))
        lines.append("")
    return lines
