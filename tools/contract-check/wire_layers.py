#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""wire v0.1 分层语义校验（L2 目录内部 / L3 快照对目录 / L4 控制与账本）。

L0（解析）在 wire_json，L1（结构）在 jsonschema_min，本模块负责 schema 无法表达的
引用闭合、适用域、数量、唯一性、状态组合与排序。

规则 ID 与 `docs/contracts/net/0.1/invariants.json` 一一对应；出处见该文件与
`docs/contracts/net/0.1/invariants.md`。
"""

from __future__ import annotations

import re
from typing import Any, NamedTuple

from wire_canonical import (
    SAFE_INTEGER_MAX,
    SEED_MAX,
    action_semantic_key,
    card_position_sort_key,
    entity_sort_key,
    field_sort_key,
    group_member_sort_key,
    group_sort_key,
    is_canonical_decimal,
    is_integer_decimal,
    pile_sort_key,
    relation_sort_key,
    sequence_sort_key,
)

SNAPSHOT_KEYS = frozenset(
    {
        "protocol_version",
        "ruleset_id",
        "catalog_id",
        "backend_profile_id",
        "session_id",
        "observation_id",
        "status",
        "terminal_result",
        "entities",
        "relations",
        "piles",
        "sequences",
        "groups",
        "legal_actions",
    }
)

RESERVED_PILE_RELATION = "net:pile_member"
RESERVED_PILE_FIELDS = ("net:pile_total_count", "net:pile_members_complete")

_SEED_RE = re.compile(r"^0$|^[1-9][0-9]*$")

_PAYLOAD_ALLOWED = {
    "describe": frozenset(),
    "get_catalog": frozenset({"catalog_id"}),
    "start": frozenset({"expected_identity", "scenario_profile", "seed"}),
    "attach": frozenset({"expected_identity", "external_session_ref"}),
    "observe": frozenset({"session_id"}),
    "step": frozenset({"session_id", "expected_observation_id", "candidate_id"}),
    "request_status": frozenset({"session_id", "target_request_id"}),
    "close": frozenset({"session_id"}),
}

_PAYLOAD_REQUIRED = {
    "describe": frozenset(),
    "get_catalog": frozenset({"catalog_id"}),
    "start": frozenset({"expected_identity", "scenario_profile"}),
    "attach": frozenset({"expected_identity", "external_session_ref"}),
    "observe": frozenset({"session_id"}),
    "step": frozenset({"session_id", "expected_observation_id", "candidate_id"}),
    "request_status": frozenset({"target_request_id"}),
    "close": frozenset({"session_id"}),
}

_ATTACH_STATUS = frozenset({"active", "terminal", "settling"})


class Finding(NamedTuple):
    rule_id: str
    layer: str
    path: str
    message: str

    def render(self) -> str:
        return f"[{self.rule_id}/{self.layer}] {self.path}: {self.message}"


def _f(rule_id: str, layer: str, path: str, message: str) -> Finding:
    return Finding(rule_id, layer, path, message)


def _ids(items: Any) -> list[str]:
    return [item.get("id") for item in items or [] if isinstance(item, dict)]


def _by_id(items: Any) -> dict[str, dict]:
    return {
        item["id"]: item
        for item in items or []
        if isinstance(item, dict) and isinstance(item.get("id"), str)
    }


def _dup_ids(items: Any) -> list[str]:
    seen: set[str] = set()
    dups: list[str] = []
    for name in _ids(items):
        if name in seen:
            dups.append(name)
        seen.add(name)
    return dups


def _is_sorted(values: list, key) -> bool:
    keys = [key(value) for value in values]
    return keys == sorted(keys)


# --------------------------------------------------------------------------- #
# L2：目录内部
# --------------------------------------------------------------------------- #
def check_catalog(catalog: dict, coverage: dict | None = None) -> list[Finding]:
    out: list[Finding] = []
    if not isinstance(catalog, dict):
        return out
    decls = catalog.get("declarations", {})
    base = "catalog.declarations"

    for table in (
        "entity_kinds",
        "content_types",
        "fields",
        "relation_types",
        "pile_types",
        "sequence_types",
        "group_types",
        "action_types",
        "terminal_reasons",
    ):
        for dup in _dup_ids(decls.get(table)):
            out.append(_f("I-86", "L2", f"{base}.{table}", f"声明 id 重复: {dup!r}"))

    entity_kinds = _by_id(decls.get("entity_kinds"))
    fields = _by_id(decls.get("fields"))
    actions = _by_id(decls.get("action_types"))
    terminals = _by_id(decls.get("terminal_reasons"))

    def refs_closure(kind: str, rule: str, path: str, names: Any) -> None:
        for name in names or []:
            if name not in entity_kinds:
                out.append(_f(rule, "L2", path, f"引用未声明的 {kind}: {name!r}"))

    for item in decls.get("content_types", []) or []:
        refs_closure("entity_kind", "I-05", f"{base}.content_types[{item.get('id')}].kind_ids", item.get("kind_ids"))

    for field_id, field in fields.items():
        path = f"{base}.fields[{field_id}]"
        applies = field.get("applies_to") or {}
        refs_closure("entity_kind", "I-05", f"{path}.applies_to.entity_kinds", applies.get("entity_kinds"))
        for name in applies.get("action_types") or []:
            if name not in actions:
                out.append(_f("I-05", "L2", f"{path}.applies_to.action_types", f"引用未声明的动作类型: {name!r}"))
        for name in applies.get("terminal_reasons") or []:
            if name not in terminals:
                out.append(_f("I-05", "L2", f"{path}.applies_to.terminal_reasons", f"引用未声明的终局原因: {name!r}"))
        value_type = field.get("value_type")
        if value_type == "entity_ref":
            targets = field.get("target_kind_ids") or []
            if not targets:
                out.append(_f("I-22", "L2", f"{path}.target_kind_ids", "entity_ref 字段必须指定目标种类"))
            refs_closure("entity_kind", "I-05", f"{path}.target_kind_ids", targets)
        if value_type == "category" and not (field.get("category_ids") or []):
            out.append(_f("I-22", "L2", f"{path}.category_ids", "category 字段必须列出类别 ID"))
        if field.get("visibility") == "conditional":
            checks = field.get("visibility_check_ids") or []
            if not checks:
                out.append(_f("I-09", "L2", f"{path}.visibility_check_ids", "conditional 字段必须有非空机制检查 ID"))
            if coverage is not None:
                known = _coverage_mechanisms(coverage)
                for check in checks:
                    if check not in known:
                        out.append(
                            _f("I-75", "L2", f"{path}.visibility_check_ids", f"覆盖表中没有机制检查 {check!r}")
                        )

    for item in (decls.get("relation_types") or []):
        path = f"{base}.relation_types[{item.get('id')}]"
        refs_closure("entity_kind", "I-05", f"{path}.source_kind_ids", item.get("source_kind_ids"))
        refs_closure("entity_kind", "I-05", f"{path}.target_kind_ids", item.get("target_kind_ids"))
        out.extend(_check_position_schema(item.get("position_schema"), f"{path}.position_schema"))

    seen_kind: dict[str, str] = {}
    group_scope: dict[str, str] = {}
    for item in (decls.get("pile_types") or []):
        path = f"{base}.pile_types[{item.get('id')}]"
        kind_id = item.get("kind_id")
        refs_closure("entity_kind", "I-05", f"{path}.kind_id", [kind_id])
        if kind_id in seen_kind:
            out.append(
                _f("I-07", "L2", f"{path}.kind_id", f"kind_id {kind_id!r} 已被 {seen_kind[kind_id]!r} 占用")
            )
        else:
            seen_kind[kind_id] = item.get("id")
        refs_closure("entity_kind", "I-05", f"{path}.member_kind_ids", item.get("member_kind_ids"))
        out.extend(_check_position_schema(item.get("position_schema"), f"{path}.position_schema"))
        exclusive = item.get("exclusive_group_id")
        scope = item.get("membership_scope_field_id")
        if exclusive is not None and scope is None:
            out.append(
                _f("I-78", "L2", path, "有 exclusive_group_id 的 pile_type 必须指定 membership_scope_field_id")
            )
        if scope is not None:
            decl = fields.get(scope)
            if decl is None:
                out.append(_f("I-05", "L2", f"{path}.membership_scope_field_id", f"字段未声明: {scope!r}"))
            else:
                if decl.get("value_type") != "entity_ref":
                    out.append(
                        _f("I-31", "L2", f"{path}.membership_scope_field_id", f"{scope!r} 必须是 entity_ref 字段")
                    )
                if decl.get("required") is not True:
                    out.append(
                        _f("I-31", "L2", f"{path}.membership_scope_field_id", f"{scope!r} 必须是 required 字段")
                    )
            if exclusive is not None:
                group_scope[exclusive] = scope

    for table in ("sequence_types", "group_types"):
        for item in (decls.get(table) or []):
            path = f"{base}.{table}[{item.get('id')}]"
            owner_key = "owner_kind_ids"
            member_key = "element_kind_ids" if table == "sequence_types" else "member_kind_ids"
            refs_closure("entity_kind", "I-05", f"{path}.{owner_key}", item.get(owner_key))
            refs_closure("entity_kind", "I-05", f"{path}.{member_key}", item.get(member_key))
            minimum, maximum = item.get("min_count"), item.get("max_count")
            if isinstance(minimum, int) and isinstance(maximum, int) and minimum > maximum:
                out.append(_f("I-89", "L2", path, f"min_count {minimum} 大于 max_count {maximum}"))

    for table, owner in (("action_types", "动作"), ("terminal_reasons", "终局原因")):
        for item in (decls.get(table) or []):
            path = f"{base}.{table}[{item.get('id')}]"
            required = list(item.get("required_fields") or [])
            optional = list(item.get("optional_fields") or [])
            for field_id in required + optional:
                if field_id not in fields:
                    out.append(_f("I-05", "L2", path, f"引用未声明字段: {field_id!r}"))
            overlap = set(required) & set(optional)
            for field_id in sorted(overlap):
                out.append(_f("I-08", "L2", path, f"字段 {field_id!r} 同时在 required_fields 与 optional_fields"))
            if set(required) & set(optional) == set() and all(
                field_id in fields for field_id in required + optional
            ):
                applies_key = "action_types" if table == "action_types" else "terminal_reasons"
                expected_required = sorted(
                    field_id
                    for field_id, decl in fields.items()
                    if item.get("id") in ((decl.get("applies_to") or {}).get(applies_key) or [])
                    and decl.get("required") is True
                )
                expected_optional = sorted(
                    field_id
                    for field_id, decl in fields.items()
                    if item.get("id") in ((decl.get("applies_to") or {}).get(applies_key) or [])
                    and decl.get("required") is not True
                )
                if sorted(required) != expected_required:
                    out.append(
                        _f(
                            "I-87",
                            "L2",
                            f"{path}.required_fields",
                            f"{owner}声明的必需字段 {sorted(required)} 与字段 applies_to/required 推导的 "
                            f"{expected_required} 不一致",
                        )
                    )
                if sorted(optional) != expected_optional:
                    out.append(
                        _f(
                            "I-87",
                            "L2",
                            f"{path}.optional_fields",
                            f"{owner}声明的可选字段 {sorted(optional)} 与字段 applies_to/required 推导的 "
                            f"{expected_optional} 不一致",
                        )
                    )

    pile_types = decls.get("pile_types") or []
    if pile_types:
        relation_ids = _ids(decls.get("relation_types"))
        if RESERVED_PILE_RELATION not in relation_ids:
            out.append(
                _f("I-79", "L2", f"{base}.relation_types", f"使用 piles 的目录必须声明保留关系 {RESERVED_PILE_RELATION!r}")
            )
        for field_id in RESERVED_PILE_FIELDS:
            if field_id not in fields:
                out.append(
                    _f("I-79", "L2", f"{base}.fields", f"使用 piles 的目录必须声明保留字段 {field_id!r}")
                )
    return out


def _check_position_schema(schema: Any, path: str) -> list[Finding]:
    out: list[Finding] = []
    if not isinstance(schema, dict):
        return out
    minimum, maximum = schema.get("min"), schema.get("max")
    if isinstance(minimum, int) and isinstance(maximum, int) and maximum < minimum:
        out.append(_f("I-77", "L2", path, f"max {maximum} 小于 min {minimum}"))
    return out


def _coverage_mechanisms(coverage: Any) -> set[str]:
    rows = coverage.get("mechanisms", []) if isinstance(coverage, dict) else coverage
    return {
        row.get("mechanism_id")
        for row in rows or []
        if isinstance(row, dict) and isinstance(row.get("mechanism_id"), str)
    }


def check_coverage(coverage: Any) -> list[Finding]:
    out: list[Finding] = []
    rows = coverage.get("mechanisms", []) if isinstance(coverage, dict) else coverage
    seen: set[str] = set()
    for index, row in enumerate(rows or []):
        if not isinstance(row, dict):
            continue
        path = f"coverage.mechanisms[{index}]"
        mechanism_id = row.get("mechanism_id")
        if mechanism_id in seen:
            out.append(_f("I-75", "L2", path, f"mechanism_id 重复: {mechanism_id!r}"))
        seen.add(mechanism_id)
        if row.get("status") == "verified":
            if not (row.get("verification_ids") or []):
                out.append(
                    _f("I-85", "L2", path, "status=verified 必须有非空 verification_ids（没有运行证据不能写 verified）")
                )
            if not row.get("evidence_ref"):
                out.append(_f("I-85", "L2", path, "status=verified 必须有 evidence_ref"))
    return out


def check_catalog_coverage(catalog: dict, coverage: Any) -> list[Finding]:
    """I-75：目录 visibility_check_ids 必须能在覆盖表内闭合。"""
    out: list[Finding] = []
    if not isinstance(catalog, dict):
        return out
    known = _coverage_mechanisms(coverage)
    for field in (catalog.get("declarations") or {}).get("fields") or []:
        for check in field.get("visibility_check_ids") or []:
            if check not in known:
                out.append(
                    _f(
                        "I-75",
                        "L2",
                        f"catalog.declarations.fields[{field.get('id')}].visibility_check_ids",
                        f"覆盖表未声明机制检查 {check!r}",
                    )
                )
    return out


# --------------------------------------------------------------------------- #
# L3：快照对目录
# --------------------------------------------------------------------------- #
def _validate_field_value(
    field: dict,
    decl: dict,
    path: str,
    out: list[Finding],
    instance_kinds: dict[str, str],
) -> None:
    if field.get("state") != "known":
        return
    value = field.get("value")
    value_type = decl.get("value_type")
    if value_type == "number":
        if not isinstance(value, str):
            out.append(_f("I-22", "L3", f"{path}.value", f"number 字段必须是规范十进制字符串，得到 {value!r}"))
        elif not is_canonical_decimal(value):
            out.append(_f("I-21", "L3", f"{path}.value", f"非规范 decimal: {value!r}"))
        elif decl.get("number_format") == "integer" and not is_integer_decimal(value):
            out.append(_f("I-72", "L3", f"{path}.value", f"number_format=integer 不接受带小数部分的值: {value!r}"))
    elif value_type == "boolean":
        if not isinstance(value, bool):
            out.append(_f("I-22", "L3", f"{path}.value", f"boolean 字段必须是 JSON true/false，得到 {value!r}"))
    elif value_type == "category":
        if not isinstance(value, str) or value not in (decl.get("category_ids") or []):
            out.append(
                _f("I-22", "L3", f"{path}.value", f"category 值 {value!r} 不在目录声明 {decl.get('category_ids')!r}")
            )
    elif value_type == "entity_ref":
        if not isinstance(value, str):
            out.append(_f("I-22", "L3", f"{path}.value", f"entity_ref 字段必须是 instance_id 字符串，得到 {value!r}"))
        elif value not in instance_kinds:
            out.append(_f("I-39", "L3", f"{path}.value", f"entity_ref 悬空: {value!r} 不在当前快照"))
        else:
            targets = decl.get("target_kind_ids") or []
            if targets and instance_kinds[value] not in targets:
                out.append(
                    _f(
                        "I-22",
                        "L3",
                        f"{path}.value",
                        f"entity_ref 目标种类 {instance_kinds[value]!r} 不属于 {targets!r}",
                    )
                )


def check_snapshot(snapshot: dict, catalog: dict | None = None) -> list[Finding]:
    out: list[Finding] = []
    if not isinstance(snapshot, dict):
        return out
    decls = (catalog or {}).get("declarations") or {}
    entity_kinds = _by_id(decls.get("entity_kinds"))
    fields = _by_id(decls.get("fields"))
    relations = _by_id(decls.get("relation_types"))
    pile_types_by_kind = {
        item.get("kind_id"): item for item in (decls.get("pile_types") or []) if isinstance(item, dict)
    }
    sequences = _by_id(decls.get("sequence_types"))
    groups = _by_id(decls.get("group_types"))
    actions = _by_id(decls.get("action_types"))
    terminals = _by_id(decls.get("terminal_reasons"))

    entities = snapshot.get("entities") or []
    instance_kinds = {
        entity.get("instance_id"): entity.get("kind_id")
        for entity in entities
        if isinstance(entity, dict)
    }

    if catalog is not None:
        for key in ("protocol_version", "ruleset_id", "catalog_id"):
            if snapshot.get(key) != catalog.get(key):
                out.append(
                    _f(
                        "I-01",
                        "L3",
                        f"snapshot.{key}",
                        f"与目录身份不一致: {snapshot.get(key)!r} != {catalog.get(key)!r}",
                    )
                )

    # 实体
    if len(instance_kinds) != len(entities):
        out.append(_f("I-40", "L3", "snapshot.entities", "instance_id 重复"))
    if not _is_sorted(entities, entity_sort_key):
        out.append(_f("I-74", "L3", "snapshot.entities", "entities 未按 instance_id 排序"))
    for entity in entities:
        instance_id = entity.get("instance_id")
        path = f"snapshot.entities[{instance_id}]"
        kind_id = entity.get("kind_id")
        if catalog is not None and kind_id not in entity_kinds:
            out.append(_f("I-05", "L3", f"{path}.kind_id", f"未声明的实体种类: {kind_id!r}"))
        entity_fields = entity.get("fields") or []
        field_ids = [field.get("field_id") for field in entity_fields]
        if len(set(field_ids)) != len(field_ids):
            out.append(_f("I-88", "L3", f"{path}.fields", "同一实体重复声明 field_id"))
        if not _is_sorted(entity_fields, field_sort_key):
            out.append(_f("I-74", "L3", f"{path}.fields", "fields 未按 field_id 排序"))
        if catalog is not None:
            for field in entity_fields:
                field_id = field.get("field_id")
                decl = fields.get(field_id)
                fpath = f"{path}.fields[{field_id}]"
                if decl is None:
                    out.append(_f("I-05", "L3", fpath, f"未声明的字段: {field_id!r}"))
                    continue
                applies = decl.get("applies_to") or {}
                if applies.get("entity_kinds") and kind_id not in applies["entity_kinds"]:
                    out.append(_f("I-22", "L3", fpath, f"字段不适用于实体种类 {kind_id!r}"))
                _validate_field_value(field, decl, fpath, out, instance_kinds)
            for field_id, decl in fields.items():
                if not decl.get("required"):
                    continue
                applies = decl.get("applies_to") or {}
                if kind_id in (applies.get("entity_kinds") or []) and field_id not in field_ids:
                    out.append(
                        _f("I-20", "L3", f"{path}.fields", f"required 且适用的字段被省略: {field_id!r}")
                    )

    # 终局
    terminal = snapshot.get("terminal_result")
    if snapshot.get("status") == "terminal":
        if isinstance(terminal, dict):
            reason_id = terminal.get("reason_id")
            decl = terminals.get(reason_id)
            tpath = "snapshot.terminal_result"
            if catalog is not None and decl is None:
                out.append(_f("I-18", "L3", f"{tpath}.reason_id", f"未声明的终局原因: {reason_id!r}"))
            elif decl is not None:
                if terminal.get("outcome") != decl.get("outcome"):
                    out.append(
                        _f(
                            "I-18",
                            "L3",
                            f"{tpath}.outcome",
                            f"outcome {terminal.get('outcome')!r} 与声明 {decl.get('outcome')!r} 不一致",
                        )
                    )
                facts = terminal.get("facts") or []
                fact_ids = [fact.get("field_id") for fact in facts]
                if len(set(fact_ids)) != len(fact_ids):
                    out.append(_f("I-90", "L3", f"{tpath}.facts", "facts 存在重复 field_id"))
                if not _is_sorted(facts, field_sort_key):
                    out.append(_f("I-90", "L3", f"{tpath}.facts", "facts 未按 field_id 排序"))
                for fact in facts:
                    field_id = fact.get("field_id")
                    fdecl = fields.get(field_id)
                    fpath = f"{tpath}.facts[{field_id}]"
                    if fdecl is None:
                        out.append(_f("I-18", "L3", fpath, f"未声明的结果事实字段: {field_id!r}"))
                        continue
                    applies = fdecl.get("applies_to") or {}
                    if reason_id not in (applies.get("terminal_reasons") or []):
                        out.append(_f("I-18", "L3", fpath, f"字段不适用于终局原因 {reason_id!r}"))
                    _validate_field_value(fact, fdecl, fpath, out, instance_kinds)
                for field_id, fdecl in fields.items():
                    applies = fdecl.get("applies_to") or {}
                    if not fdecl.get("required"):
                        continue
                    if reason_id in (applies.get("terminal_reasons") or []) and field_id not in fact_ids:
                        out.append(
                            _f("I-18", "L3", f"{tpath}.facts", f"必需结果事实被省略: {field_id!r}")
                        )
                    elif reason_id in (applies.get("terminal_reasons") or []):
                        for fact in facts:
                            if fact.get("field_id") == field_id and fact.get("state") != "known":
                                out.append(
                                    _f("I-18", "L3", f"{tpath}.facts[{field_id}]", "必需结果事实必须为 known")
                                )

    # 关系
    relation_list = snapshot.get("relations") or []
    seen_relations: set[tuple] = set()
    if not _is_sorted(relation_list, relation_sort_key):
        out.append(_f("I-74", "L3", "snapshot.relations", "relations 未按 type_id/source_ref/target_ref/position 排序"))
    for relation in relation_list:
        type_id = relation.get("type_id")
        key = (
            type_id,
            relation.get("source_ref"),
            relation.get("target_ref"),
            relation.get("position"),
        )
        if key in seen_relations:
            out.append(_f("I-25", "L3", f"snapshot.relations[{type_id}]", f"完全重复的关系: {key!r}"))
        seen_relations.add(key)
        rpath = f"snapshot.relations[{type_id}]"
        if type_id == RESERVED_PILE_RELATION:
            out.append(
                _f("I-79", "L3", rpath, f"保留派生关系 {RESERVED_PILE_RELATION!r} 不得出现在 wire 中")
            )
        for ref_key in ("source_ref", "target_ref"):
            ref = relation.get(ref_key)
            if ref not in instance_kinds:
                out.append(_f("I-39", "L3", f"{rpath}.{ref_key}", f"悬空引用: {ref!r}"))
        if catalog is not None:
            decl = relations.get(type_id)
            if decl is None:
                out.append(_f("I-05", "L3", rpath, f"未声明的关系类型: {type_id!r}"))
            else:
                source_kind = instance_kinds.get(relation.get("source_ref"))
                target_kind = instance_kinds.get(relation.get("target_ref"))
                if source_kind is not None and decl.get("source_kind_ids") and source_kind not in decl["source_kind_ids"]:
                    out.append(_f("I-22", "L3", f"{rpath}.source_ref", f"来源种类 {source_kind!r} 不属于声明"))
                if target_kind is not None and decl.get("target_kind_ids") and target_kind not in decl["target_kind_ids"]:
                    out.append(_f("I-22", "L3", f"{rpath}.target_ref", f"目标种类 {target_kind!r} 不属于声明"))
                schema = decl.get("position_schema")
                position = relation.get("position")
                if schema is None and position is not None:
                    out.append(
                        _f("I-25", "L3", f"{rpath}.position", "position_schema=null 时必须省略 position")
                    )
                elif isinstance(schema, dict) and position is not None:
                    minimum = schema.get("min")
                    maximum = schema.get("max")
                    if isinstance(minimum, int) and position < minimum:
                        out.append(_f("I-25", "L3", f"{rpath}.position", f"position 小于声明 min {minimum}"))
                    if isinstance(maximum, int) and position > maximum:
                        out.append(_f("I-25", "L3", f"{rpath}.position", f"position 大于声明 max {maximum}"))

    # 牌堆
    pile_list = snapshot.get("piles") or []
    if len({pile.get("pile_ref") for pile in pile_list}) != len(pile_list):
        out.append(_f("I-26", "L3", "snapshot.piles", "pile_ref 重复"))
    if not _is_sorted(pile_list, pile_sort_key):
        out.append(_f("I-74", "L3", "snapshot.piles", "piles 未按 pile_ref 排序"))
    scope_members: dict[tuple, set] = {}
    for pile in pile_list:
        pile_ref = pile.get("pile_ref")
        path = f"snapshot.piles[{pile_ref}]"
        entity = instance_kinds.get(pile_ref)
        pile_type = None
        if entity is None:
            out.append(_f("I-26", "L3", f"{path}.pile_ref", f"悬空引用: {pile_ref!r}"))
        else:
            pile_type = pile_types_by_kind.get(entity)
            if catalog is not None and pile_type is None:
                out.append(
                    _f("I-26", "L3", f"{path}.pile_ref", f"实体种类 {entity!r} 不唯一匹配已声明 pile_type")
                )
        total = pile.get("total_count")
        members = pile.get("visible_members") or []
        if len(set(members)) != len(members):
            out.append(_f("I-28", "L3", f"{path}.visible_members", "visible_members 存在重复引用"))
        if not _is_sorted(members, lambda ref: str(ref).encode("utf-8")):
            out.append(_f("I-74", "L3", f"{path}.visible_members", "visible_members 未按公开引用排序"))
        for member in members:
            if member not in instance_kinds:
                out.append(_f("I-39", "L3", f"{path}.visible_members", f"悬空引用: {member!r}"))
            elif pile_type is not None and instance_kinds[member] not in (pile_type.get("member_kind_ids") or []):
                out.append(
                    _f("I-28", "L3", f"{path}.visible_members", f"成员种类 {instance_kinds[member]!r} 不属于 pile_type")
                )
        if isinstance(total, int) and len(members) > total:
            out.append(_f("I-28", "L3", f"{path}.visible_members", f"成员数 {len(members)} 超过 total_count {total}"))
        if pile.get("members_complete") is True and isinstance(total, int) and len(members) != total:
            out.append(
                _f("I-28", "L3", f"{path}.members_complete", f"members_complete=true 但成员数 {len(members)} != {total}")
            )
        positions = pile.get("known_positions") or []
        if not _is_sorted(positions, card_position_sort_key):
            out.append(_f("I-74", "L3", f"{path}.known_positions", "known_positions 未按 position/card_ref 排序"))
        seen_cards: set[str] = set()
        seen_positions: set[int] = set()
        for position in positions:
            card_ref = position.get("card_ref")
            index = position.get("position")
            ppath = f"{path}.known_positions[{card_ref}]"
            if card_ref in seen_cards:
                out.append(_f("I-29", "L3", ppath, f"同一张牌出现多个位置: {card_ref!r}"))
            seen_cards.add(card_ref)
            if index in seen_positions:
                out.append(_f("I-29", "L3", ppath, f"同一位置被多张牌占用: {index!r}"))
            seen_positions.add(index)
            if card_ref not in members:
                out.append(_f("I-29", "L3", ppath, f"card_ref {card_ref!r} 不是 visible_members 的子集成员"))
            if isinstance(total, int) and isinstance(index, int) and not (0 <= index < total):
                out.append(_f("I-27", "L3", ppath, f"position {index} 不满足 0 <= position < total_count {total}"))
        if pile_type is not None and pile_type.get("exclusive_group_id") is not None:
            scope_field = pile_type.get("membership_scope_field_id")
            scope_value = None
            for entity_obj in entities:
                if entity_obj.get("instance_id") == pile_ref:
                    for field in entity_obj.get("fields") or []:
                        if field.get("field_id") == scope_field:
                            scope_value = field.get("value") if field.get("state") == "known" else None
            if scope_value is None:
                out.append(
                    _f(
                        "I-31",
                        "L3",
                        f"{path}.{scope_field}",
                        "互斥组牌堆必须带 required/known 的 membership_scope_field_id",
                    )
                )
            else:
                bucket = scope_members.setdefault((pile_type.get("exclusive_group_id"), scope_value), set())
                overlap = bucket & set(members)
                if overlap:
                    out.append(
                        _f("I-31", "L3", f"{path}.visible_members", f"同互斥组作用域成员重复: {sorted(overlap)!r}")
                    )
                bucket |= set(members)

    if pile_list and catalog is not None:
        relation_ids = _ids(decls.get("relation_types"))
        if RESERVED_PILE_RELATION not in relation_ids:
            out.append(
                _f("I-79", "L3", "snapshot.piles", f"使用 piles 的目录缺少保留关系 {RESERVED_PILE_RELATION!r}")
            )
        for field_id in RESERVED_PILE_FIELDS:
            if field_id not in fields:
                out.append(
                    _f("I-79", "L3", "snapshot.piles", f"使用 piles 的目录缺少保留字段 {field_id!r}")
                )
        for entity in entities:
            for field in entity.get("fields") or []:
                if field.get("field_id") in RESERVED_PILE_FIELDS:
                    out.append(
                        _f(
                            "I-79",
                            "L3",
                            f"snapshot.entities[{entity.get('instance_id')}].fields",
                            f"保留派生字段 {field.get('field_id')!r} 不得出现在 wire 中",
                        )
                    )

    # 序列
    sequence_list = snapshot.get("sequences") or []
    keys = [(item.get("type_id"), item.get("owner_ref")) for item in sequence_list]
    if len(set(keys)) != len(keys):
        out.append(_f("I-34", "L3", "snapshot.sequences", "(type_id, owner_ref) 不唯一"))
    if not _is_sorted(sequence_list, sequence_sort_key):
        out.append(_f("I-74", "L3", "snapshot.sequences", "sequences 未按 type_id/owner_ref 排序"))
    for sequence in sequence_list:
        type_id = sequence.get("type_id")
        path = f"snapshot.sequences[{type_id}]"
        owner = sequence.get("owner_ref")
        if owner not in instance_kinds:
            out.append(_f("I-39", "L3", f"{path}.owner_ref", f"悬空引用: {owner!r}"))
        elements = sequence.get("elements") or []
        for element in elements:
            if element not in instance_kinds:
                out.append(_f("I-39", "L3", f"{path}.elements", f"悬空引用: {element!r}"))
        if catalog is not None:
            decl = sequences.get(type_id)
            if decl is None:
                out.append(_f("I-05", "L3", path, f"未声明的序列类型: {type_id!r}"))
            else:
                if owner in instance_kinds and decl.get("owner_kind_ids") and instance_kinds[owner] not in decl["owner_kind_ids"]:
                    out.append(_f("I-22", "L3", f"{path}.owner_ref", "owner 种类不属于声明"))
                for element in elements:
                    kind = instance_kinds.get(element)
                    if kind is not None and decl.get("element_kind_ids") and kind not in decl["element_kind_ids"]:
                        out.append(_f("I-22", "L3", f"{path}.elements", f"元素种类 {kind!r} 不属于声明"))
                minimum, maximum = decl.get("min_count"), decl.get("max_count")
                if isinstance(minimum, int) and len(elements) < minimum:
                    out.append(_f("I-34", "L3", path, f"元素数 {len(elements)} 小于 min_count {minimum}"))
                if isinstance(maximum, int) and len(elements) > maximum:
                    out.append(_f("I-34", "L3", path, f"元素数 {len(elements)} 大于 max_count {maximum}"))

    # 组
    group_list = snapshot.get("groups") or []
    keys = [(item.get("type_id"), item.get("owner_ref")) for item in group_list]
    if len(set(keys)) != len(keys):
        out.append(_f("I-35", "L3", "snapshot.groups", "(type_id, owner_ref) 不唯一"))
    if not _is_sorted(group_list, group_sort_key):
        out.append(_f("I-74", "L3", "snapshot.groups", "groups 未按 type_id/owner_ref 排序"))
    for group in group_list:
        type_id = group.get("type_id")
        path = f"snapshot.groups[{type_id}]"
        owner = group.get("owner_ref")
        if owner not in instance_kinds:
            out.append(_f("I-39", "L3", f"{path}.owner_ref", f"悬空引用: {owner!r}"))
        members = group.get("members") or []
        if len({member.get("entity_ref") for member in members}) != len(members):
            out.append(_f("I-35", "L3", f"{path}.members", "成员 entity_ref 不唯一"))
        if not _is_sorted(members, group_member_sort_key):
            out.append(_f("I-74", "L3", f"{path}.members", "成员未按 entity_ref 排序"))
        for member in members:
            ref = member.get("entity_ref")
            if ref not in instance_kinds:
                out.append(_f("I-39", "L3", f"{path}.members", f"悬空引用: {ref!r}"))
            elif member.get("multiplicity") != 1:
                out.append(
                    _f("I-35", "L3", f"{path}.members[{ref}].multiplicity", "具体实例的 multiplicity 必须为 1")
                )
        if catalog is not None:
            decl = groups.get(type_id)
            if decl is None:
                out.append(_f("I-05", "L3", path, f"未声明的组类型: {type_id!r}"))
            else:
                if owner in instance_kinds and decl.get("owner_kind_ids") and instance_kinds[owner] not in decl["owner_kind_ids"]:
                    out.append(_f("I-22", "L3", f"{path}.owner_ref", "owner 种类不属于声明"))
                for member in members:
                    kind = instance_kinds.get(member.get("entity_ref"))
                    if kind is not None and decl.get("member_kind_ids") and kind not in decl["member_kind_ids"]:
                        out.append(_f("I-22", "L3", f"{path}.members", f"成员种类 {kind!r} 不属于声明"))
                minimum, maximum = decl.get("min_count"), decl.get("max_count")
                if isinstance(minimum, int) and len(members) < minimum:
                    out.append(_f("I-35", "L3", path, f"成员数 {len(members)} 小于 min_count {minimum}"))
                if isinstance(maximum, int) and len(members) > maximum:
                    out.append(_f("I-35", "L3", path, f"成员数 {len(members)} 大于 max_count {maximum}"))

    # 合法动作
    action_list = snapshot.get("legal_actions") or []
    candidate_ids = [action.get("candidate_id") for action in action_list]
    if len(set(candidate_ids)) != len(candidate_ids):
        out.append(_f("I-40", "L3", "snapshot.legal_actions", "candidate_id 重复"))
    semantic_keys = [action_semantic_key(action) for action in action_list]
    if len(set(semantic_keys)) != len(semantic_keys):
        out.append(_f("I-37", "L3", "snapshot.legal_actions", "同一动作语义键以不同 candidate_id 重复发布"))
    if semantic_keys != sorted(semantic_keys):
        out.append(_f("I-80", "L3", "snapshot.legal_actions", "legal_actions 未按动作语义键的 UTF-8 字节序排序"))
    for action in action_list:
        candidate_id = action.get("candidate_id")
        path = f"snapshot.legal_actions[{candidate_id}]"
        action_fields = action.get("fields") or []
        field_ids = [field.get("field_id") for field in action_fields]
        if len(set(field_ids)) != len(field_ids):
            out.append(_f("I-88", "L3", f"{path}.fields", "动作重复声明 field_id"))
        if not _is_sorted(action_fields, field_sort_key):
            out.append(_f("I-74", "L3", f"{path}.fields", "fields 未按 field_id 排序"))
        for field in action_fields:
            if field.get("state") != "known":
                out.append(_f("I-36", "L3", f"{path}.fields[{field.get('field_id')}]", "执行所需字段必须 known"))
        if catalog is None:
            continue
        decl = actions.get(action.get("action_type"))
        if decl is None:
            out.append(_f("I-05", "L3", f"{path}.action_type", f"未声明的动作类型: {action.get('action_type')!r}"))
            continue
        required = set(decl.get("required_fields") or [])
        optional = set(decl.get("optional_fields") or [])
        missing = sorted(required - set(field_ids))
        if missing:
            out.append(_f("I-36", "L3", f"{path}.fields", f"缺少必需动作字段: {missing!r}"))
        for field in action_fields:
            field_id = field.get("field_id")
            fpath = f"{path}.fields[{field_id}]"
            fdecl = fields.get(field_id)
            if fdecl is None:
                out.append(_f("I-05", "L3", fpath, f"未声明的动作字段: {field_id!r}"))
                continue
            if field_id not in required | optional:
                out.append(_f("I-36", "L3", fpath, f"字段未在动作声明的 required/optional_fields 中"))
            applies = fdecl.get("applies_to") or {}
            if applies.get("action_types") and action.get("action_type") not in applies["action_types"]:
                out.append(_f("I-22", "L3", fpath, "字段不适用于该动作类型"))
            _validate_field_value(field, fdecl, fpath, out, instance_kinds)
    return out


# --------------------------------------------------------------------------- #
# L4：控制层
# --------------------------------------------------------------------------- #
def check_request(request: dict) -> list[Finding]:
    out: list[Finding] = []
    if not isinstance(request, dict):
        return out
    operation = request.get("operation")
    payload = request.get("payload")
    path = f"request[{request.get('request_id')}]"
    if operation not in _PAYLOAD_ALLOWED:
        out.append(_f("I-44", "L4", f"{path}.operation", f"未知操作: {operation!r}"))
        return out
    if not isinstance(payload, dict):
        out.append(_f("I-44", "L4", f"{path}.payload", "payload 必须是对象"))
        return out
    keys = set(payload)
    allowed = set(_PAYLOAD_ALLOWED[operation])
    required = set(_PAYLOAD_REQUIRED[operation])
    for key in sorted(keys - allowed):
        out.append(_f("I-44", "L4", f"{path}.payload", f"operation={operation!r} 不认识 payload 键 {key!r}"))
    for key in sorted(required - keys):
        out.append(_f("I-44", "L4", f"{path}.payload", f"operation={operation!r} 缺少必需键 {key!r}"))
    if operation == "start" and "seed" in payload:
        out.extend(_check_seed(payload["seed"], f"{path}.payload.seed"))
    return out


def _check_seed(seed: Any, path: str) -> list[Finding]:
    out: list[Finding] = []
    if not isinstance(seed, str):
        out.append(_f("I-76", "L4", path, "seed 必须是规范无符号 64 位十进制字符串，不接受 JSON number"))
        return out
    if not _SEED_RE.match(seed):
        out.append(_f("I-76", "L4", path, f"seed 非规范形式（仅允许 0 或无前导零正整数）: {seed!r}"))
        return out
    if int(seed) > SEED_MAX:
        out.append(_f("I-76", "L4", path, f"seed 超出 0..{SEED_MAX}: {seed!r}"))
    return out


def _result_matches(operation: str, result: Any) -> bool:
    if not isinstance(result, dict):
        return False
    keys = frozenset(result)
    if operation == "describe":
        return keys == frozenset({"identity", "capabilities"})
    if operation == "get_catalog":
        return keys == frozenset({"protocol_version", "ruleset_id", "catalog_id", "declarations"})
    if operation == "start":
        return keys == frozenset({"session_id", "snapshot"})
    if operation == "attach":
        if keys not in (frozenset({"session_id", "session_status"}), frozenset({"session_id", "session_status", "snapshot"})):
            return False
        return result.get("session_status") in _ATTACH_STATUS
    if operation == "observe":
        return keys in (frozenset({"session_status"}), frozenset({"session_status", "snapshot"}))
    if operation == "step":
        return keys == SNAPSHOT_KEYS
    if operation == "request_status":
        return keys in (frozenset({"status"}), frozenset({"status", "response"}))
    if operation == "close":
        return keys == frozenset({"session_id", "session_status"}) and result.get("session_status") == "closed"
    return False


def check_response(response: dict, ledger: dict | None = None, catalog: dict | None = None) -> list[Finding]:
    """ok/error 配对（I-43）、operation→result 配对（I-81）、缓存响应一致性（I-50）。"""
    out: list[Finding] = []
    if not isinstance(response, dict):
        return out
    request_id = response.get("request_id")
    path = f"response[{request_id}]"
    ok = response.get("ok")
    if ok is True:
        result = response.get("result")
        entry = (ledger or {}).get(request_id) if ledger else None
        if entry is None:
            if ledger:
                out.append(_f("I-81", "L4", path, f"响应 request_id 不在请求账本中: {request_id!r}"))
        else:
            operation = entry.get("operation")
            if not _result_matches(operation, result):
                out.append(
                    _f(
                        "I-81",
                        "L4",
                        f"{path}.result",
                        f"result 结构与 operation={operation!r} 不配对（keys={sorted(result) if isinstance(result, dict) else result!r}）",
                    )
                )
        if isinstance(result, dict):
            if "snapshot" in result and catalog is not None:
                out.extend(_relabel(check_snapshot(result["snapshot"], catalog), f"{path}.result.snapshot"))
            if "status" in result and "response" in result:
                inner = result["response"]
                if isinstance(inner, dict) and inner.get("request_id") != request_id:
                    target = entry.get("target_request_id") if entry else None
                    if target is not None and inner.get("request_id") != target:
                        out.append(
                            _f(
                                "I-50",
                                "L4",
                                f"{path}.result.response.request_id",
                                "completed 必须返回原响应（request_id 不匹配目标请求）",
                            )
                        )
                if isinstance(inner, dict):
                    out.extend(check_response(inner, ledger, catalog))
    elif ok is False:
        error = response.get("error")
        if isinstance(error, dict) and error.get("execution_state") == "completed":
            if error.get("retry_mode") == "stop":
                pass  # 语义组合本身合法；行为验证属后端运行时检查
    return out


def _relabel(findings: list[Finding], prefix: str) -> list[Finding]:
    return [Finding(item.rule_id, item.layer, f"{prefix}{item.path[len('snapshot'):]}", item.message) for item in findings]


def check_capabilities(capabilities: dict, catalog: dict | None = None) -> list[Finding]:
    out: list[Finding] = []
    if not isinstance(capabilities, dict):
        return out
    path = "capabilities"
    for name, state in (capabilities.get("field_availability") or {}).items():
        if catalog is not None:
            field_ids = _ids((catalog.get("declarations") or {}).get("fields"))
            if name not in field_ids:
                out.append(_f("I-83", "L4", f"{path}.field_availability[{name}]", "field_id 未在目录中声明"))
        if state not in ("available", "unavailable"):
            out.append(_f("I-83", "L4", f"{path}.field_availability[{name}]", f"未知可用性 {state!r}"))
    for index, structure in enumerate(capabilities.get("supported_structures") or []):
        if catalog is None:
            continue
        all_ids = set()
        for table in (catalog.get("declarations") or {}).values():
            all_ids |= set(_ids(table))
        for declaration_id in structure.get("declaration_ids") or []:
            if declaration_id not in all_ids:
                out.append(
                    _f(
                        "I-84",
                        "L4",
                        f"{path}.supported_structures[{index}].declaration_ids",
                        f"未声明的 declaration_id: {declaration_id!r}",
                    )
                )
    if catalog is not None:
        all_ids = set()
        for table in (catalog.get("declarations") or {}).values():
            all_ids |= set(_ids(table))
        for index, profile in enumerate(capabilities.get("scenario_profiles") or []):
            for declaration_id in profile.get("required_declarations") or []:
                if declaration_id not in all_ids:
                    out.append(
                        _f(
                            "I-84",
                            "L4",
                            f"{path}.scenario_profiles[{index}].required_declarations",
                            f"未声明的 required_declaration: {declaration_id!r}",
                        )
                    )
    guarantees = capabilities.get("execution_guarantees") or {}
    if guarantees.get("public_state_restore") is not False:
        out.append(_f("I-70", "L4", f"{path}.execution_guarantees.public_state_restore", "首版必须为 false"))
    if guarantees.get("public_fork") is not False:
        out.append(_f("I-70", "L4", f"{path}.execution_guarantees.public_fork", "首版必须为 false"))
    return out


def check_control(
    requests: list[dict] | None = None,
    responses: list[dict] | None = None,
    ledger_entries: list[dict] | None = None,
    capabilities: dict | None = None,
    catalog: dict | None = None,
) -> list[Finding]:
    out: list[Finding] = []
    ledger: dict[str, dict] = {}
    fingerprints: dict[str, tuple] = {}
    for entry in ledger_entries or []:
        request_id = entry.get("request_id")
        if request_id in ledger:
            previous = ledger[request_id]
            if (previous.get("operation"), previous.get("payload_fingerprint")) != (
                entry.get("operation"),
                entry.get("payload_fingerprint"),
            ):
                out.append(
                    _f(
                        "I-82",
                        "L4",
                        f"ledger[{request_id}]",
                        "同一 request_id 被复用于不同 operation/payload",
                    )
                )
        ledger[request_id] = entry
    for request in requests or []:
        out.extend(check_request(request))
        if request.get("request_id") is not None and capabilities is not None:
            if request.get("operation") == "start":
                payload = request.get("payload") or {}
                if "seed" in payload and capabilities.get("seed_control") is not True:
                    out.append(
                        _f(
                            "I-11",
                            "L4",
                            f"request[{request.get('request_id')}].payload.seed",
                            "后端声明 seed_control=false，不得接受指定 seed",
                        )
                    )
                profile = payload.get("scenario_profile")
                profiles = [item.get("id") for item in capabilities.get("scenario_profiles") or []]
                if profile is not None and profile not in profiles:
                    out.append(
                        _f(
                            "I-13",
                            "L4",
                            f"request[{request.get('request_id')}].payload.scenario_profile",
                            f"未在 capabilities.scenario_profiles 中声明: {profile!r}",
                        )
                    )
    for response in responses or []:
        out.extend(check_response(response, ledger, catalog))
    return out


def check_result_state_combinations(document: dict, role: str) -> list[Finding]:
    """observe/attach 结果的状态组合（I-48/I-47）在 schema 已表达；此处覆盖 schema 不表达的
    session_status 与快照 status 的一致性。"""
    out: list[Finding] = []
    if role not in ("observe_result", "attach_result"):
        return out
    session_status = document.get("session_status")
    snapshot = document.get("snapshot")
    if isinstance(snapshot, dict):
        if session_status == "active" and snapshot.get("status") != "decision":
            out.append(_f("I-48", "L4", "session_status", "active 会话的快照 status 必须是 decision"))
        if session_status == "terminal" and snapshot.get("status") != "terminal":
            out.append(_f("I-48", "L4", "session_status", "terminal 会话的快照 status 必须是 terminal"))
    return out
