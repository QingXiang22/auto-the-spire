#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""schema 复核断言（R-P01 对 wire.schema.json 的机器化复核）。

这些断言把评审结论固定成可重复执行的检查：能力层级、任意 result 兜底、decimal 模式、
seed 类型、终局 facts 容器、响应状态组合、operation 配对所需的精确键。
"""

from __future__ import annotations

import re
from typing import Any

from jsonschema_min import collect_refs

CAPABILITY_KEYS = {
    "backend_kind",
    "operations",
    "scenario_profiles",
    "seed_control",
    "field_availability",
    "supported_structures",
    "limits",
    "execution_guarantees",
}

LIMIT_KEYS = {
    "entities",
    "entity_fields",
    "relations",
    "piles",
    "visible_members_per_pile",
    "known_positions_per_pile",
    "sequences",
    "elements_per_sequence",
    "groups",
    "members_per_group",
    "legal_actions",
    "action_fields",
    "frame_bytes",
    "decimal_chars",
    "ledger_entries",
    "sessions",
}

GUARANTEE_KEYS = {
    "stable_snapshot",
    "request_status",
    "active_deduplication",
    "decision_pause",
    "internal_snapshot_restore",
    "seed_action_replay",
    "public_state_restore",
    "public_fork",
    "rng_profile_id",
}

SNAPSHOT_KEYS = {
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

SCENARIO_PROFILE_KEYS = {
    "id",
    "semantic_version",
    "required_declarations",
    "supported_mechanism_ids",
    "unsupported_constraints",
    "coverage_ref",
}

SUPPORTED_STRUCTURE_KEYS = {"structure", "declaration_ids", "unsupported_constraints"}


def _defs(schema: dict) -> dict:
    return schema.get("$defs", {})


def _ref_name(ref: str) -> str:
    return ref.rsplit("/", 1)[-1]


def _keys(node: Any) -> set:
    return set((node or {}).get("properties", {}))


def policy_checks(schema: dict) -> list[tuple[str, bool, str]]:
    """返回 (rule_id, ok, detail)。"""
    checks: list[tuple[str, bool, str]] = []
    defs = _defs(schema)

    def check(rule_id: str, ok: bool, detail: str) -> None:
        checks.append((rule_id, ok, detail))

    # SCHEMA-1：每个 $ref 都能解析
    refs = collect_refs(schema)
    unresolved = [ref for ref in refs if _ref_name(ref) not in defs]
    check("SCHEMA-1", not unresolved, f"未解析的 $ref: {unresolved}" if unresolved else f"{len(refs)} 个 $ref 全部解析")

    # SCHEMA-2：所有对象默认拒绝未知键
    offenders = []
    for name, node in defs.items():
        if isinstance(node, dict) and ("properties" in node or node.get("type") == "object"):
            if node.get("additionalProperties") is not False:
                offenders.append(name)
    check("SCHEMA-2", not offenders, f"未拒绝未知键的对象: {sorted(offenders)}" if offenders else "全部对象 additionalProperties=false")

    # SCHEMA-3/U-01：能力层级精确键
    caps = defs.get("Capabilities", {})
    check(
        "SCHEMA-3",
        _keys(caps) == CAPABILITY_KEYS and set(caps.get("required", [])) == CAPABILITY_KEYS,
        f"Capabilities 键={sorted(_keys(caps))}",
    )
    check("SCHEMA-4", _keys(defs.get("Limits")) == LIMIT_KEYS, "Limits 键集合")
    check("SCHEMA-5", _keys(defs.get("ExecutionGuarantees")) == GUARANTEE_KEYS, "execution_guarantees 键集合")

    # SCHEMA-6/U-02：重放/恢复能力分层
    guarantees = defs.get("ExecutionGuarantees", {}).get("properties", {})
    replay = guarantees.get("seed_action_replay", {}).get("enum")
    ok = replay == ["none", "public_trace"]
    ok = ok and guarantees.get("public_state_restore", {}).get("const") is False
    ok = ok and guarantees.get("public_fork", {}).get("const") is False
    ok = ok and all(
        guarantees.get(name, {}).get("type") == "boolean"
        for name in ("stable_snapshot", "request_status", "active_deduplication", "decision_pause", "internal_snapshot_restore")
    )
    check("SCHEMA-6", ok, "seed_action_replay 枚举、公开恢复/分叉 false、其余布尔")
    check(
        "SCHEMA-7",
        "execution_guarantees" in _keys(caps) and not ({"restore", "fork", "public_state_restore", "seed_action_replay"} & _keys(caps)),
        "能力不得在 capabilities 顶层重复",
    )
    check(
        "SCHEMA-8",
        _keys(defs.get("ScenarioProfile")) == SCENARIO_PROFILE_KEYS
        and _keys(defs.get("SupportedStructure")) == SUPPORTED_STRUCTURE_KEYS,
        "scenario_profile / supported_structure 键集合",
    )

    # SCHEMA-9/U-08：decimal 模式必须接受 -0.xxx、拒绝非规范形式（用 schema 自身求值，含 not 子句）
    pattern = defs.get("DecimalString", {}).get("pattern")
    samples_accept = ["0", "12.5", "-0.5", "-1", "-0.0001", "0.0001", "9007199254740991"]
    samples_reject = ["-0", "1.0", "01", "+1", "1e2", "0.50", ".5", "1.", " 1", "00.5", "-"]
    ok = bool(pattern)
    if ok:
        from jsonschema_min import validate as _validate

        ok = all(not _validate(sample, defs["DecimalString"], schema) for sample in samples_accept)
        ok = ok and all(bool(_validate(sample, defs["DecimalString"], schema)) for sample in samples_reject)
    check("SCHEMA-9", ok, f"DecimalString pattern={pattern!r}")

    # SCHEMA-10：游戏 seed 为非空字符串，原样保留。
    seed = defs.get("GameSeedString", {})
    ok = seed.get("type") == "string" and seed.get("minLength") == 1 and "pattern" not in seed
    check("SCHEMA-10", ok, "GameSeed 为非空字符串")

    # SCHEMA-11：不得保留接受任意 result 的兜底分支
    result = defs.get("Response", {}).get("properties", {}).get("result", {})
    branches = result.get("anyOf") or result.get("oneOf") or []
    ok = bool(branches) and all(isinstance(branch, dict) and "$ref" in branch for branch in branches)
    ok = ok and all(_ref_name(branch["$ref"]) in defs for branch in branches)
    check("SCHEMA-11", ok, f"Response.result 分支={[b.get('$ref') for b in branches]}")

    # SCHEMA-12：ok/result/error 与 operation 结果形状
    check(
        "SCHEMA-12",
        _keys(defs.get("ObserveResult")) == {"session_status", "snapshot"}
        and _keys(defs.get("AttachResult")) == {"session_id", "session_status", "snapshot"}
        and _keys(defs.get("CloseResult")) == {"session_id", "session_status"}
        and _keys(defs.get("StartResult")) == {"session_id", "seed", "snapshot"}
        and _keys(defs.get("RequestStatusResult")) == {"status", "response"},
        "成功包装键集合",
    )

    # SCHEMA-13/U-03：终局 facts 为 Field 数组
    facts = defs.get("TerminalResult", {}).get("properties", {}).get("facts", {})
    check(
        "SCHEMA-13",
        facts.get("type") == "array" and facts.get("items", {}).get("$ref") == "#/$defs/Field",
        "TerminalResult.facts 必须是 Field 数组",
    )

    # SCHEMA-14：快照 14 键
    check("SCHEMA-14", set(defs.get("PublicSnapshot", {}).get("required", [])) == SNAPSHOT_KEYS, "PublicSnapshot 必需键")

    # SCHEMA-15：声明表类型专用键条件
    field_decl = defs.get("FieldDeclaration", {})
    ok = field_decl.get("additionalProperties") is False and bool(field_decl.get("allOf"))
    ok = ok and _keys(field_decl) >= {
        "number_format",
        "category_ids",
        "target_kind_ids",
        "condition_description",
        "visibility_check_ids",
    }
    check("SCHEMA-15", ok, "FieldDeclaration 类型专用键与条件分支")

    # SCHEMA-16/U-12：conditional 字段的 visibility_check_ids 非空
    vis = field_decl.get("properties", {}).get("visibility_check_ids", {})
    check("SCHEMA-16", vis.get("minItems") == 1, "visibility_check_ids minItems=1")

    # SCHEMA-17：声明表未列的类型专用键不得混入其他值类型（schema 条件分支与 additionalProperties）
    check("SCHEMA-17", defs.get("Declarations", {}).get("additionalProperties") is False, "Declarations 拒绝未知表")

    return checks
