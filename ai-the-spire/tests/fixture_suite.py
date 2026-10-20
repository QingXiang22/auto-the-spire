#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""A-01 fixture 套件：共享分层检查 + 独立期望比对 + 负例。

职责分离（"不要用被测实现生成期望"）：

* ``fixtures/<bundle>/catalog.json``、``snapshot*.json``、``coverage.json`` 是**手写输入**；
* ``fixtures/<bundle>/expected.json`` 是**手写期望**（对当前观察的独立读解）；
* 本模块从 contracts 视图提取事实并逐项比对期望；共享参考实现（``tools/contract-check``）
  另按 L0–L3 独立校验同一份输入。两者都不生成对方的期望。
* 期望中的 ``catalog_digest`` / ``snapshot_digest`` 由冻结的共享规范化参考实现产出
  （``python tools/contract-check/check_contracts.py`` 210/210 通过），作为固定字节回归向量，
  不在运行时改写。

负例在 ``fixtures/negative/cases.json``：对上面手写输入做声明式改动，独立写出期望命中的
共享规则 ID（``I-xx``）；规则 ID 词汇与 ``docs/contracts/net/0.2/invariants.md`` 一致。
"""

from __future__ import annotations

import copy
import json
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Callable, Optional

from ai_the_spire.contracts import layers

FIXTURE_ROOT = Path(__file__).resolve().parent / "fixtures"

#: 四类输入样例（战斗 / 选牌 / 场景 / 扩展变量）及其快照文件。
BUNDLES: dict[str, tuple[str, ...]] = {
    "combat": ("snapshot",),
    "card_choice": ("snapshot",),
    "scenario": ("snapshot", "snapshot-terminal"),
    "variables": ("snapshot",),
}

#: 保留派生声明：wire 不填，张量化时才派生（协议 §2 / I-79），因此不计入"省略"。
RESERVED_DECLARATIONS = frozenset(
    {"net:pile_member", "net:pile_total_count", "net:pile_members_complete"}
)


# --------------------------------------------------------------------------- #
# 结果类型
# --------------------------------------------------------------------------- #
@dataclass
class Outcome:
    suite: str
    case_id: str
    ok: bool
    detail: str = ""


def _read_json(path: Path) -> Any:
    with path.open("r", encoding="utf-8") as handle:
        return json.load(handle)


# --------------------------------------------------------------------------- #
# 事实提取（从 contracts 视图，纯协议事实）
# --------------------------------------------------------------------------- #
def _field_state_map(view) -> dict[str, list[list[str]]]:
    buckets: dict[str, list[list[str]]] = {"known": [], "unknown": [], "unavailable": []}
    for entity in view.entities:
        for field in entity.fields:
            buckets.setdefault(field.state, []).append([entity.instance_id, field.field_id])
    return {name: sorted(items) for name, items in buckets.items() if items}


def _is_number_field(catalog, field_id: str) -> bool:
    """number 判定只看目录声明（``Field.number_text`` 的约定）；category/entity_ref 也是 str。"""
    declaration = catalog.field(field_id)
    return declaration is not None and declaration.value_type == "number"


def _number_fields(view, catalog) -> list[tuple[str, str, str]]:
    return [
        (entity.instance_id, field.field_id, field.value)
        for entity in view.entities
        for field in entity.fields
        if field.is_known and isinstance(field.value, str) and _is_number_field(catalog, field.field_id)
    ]


def _boolean_fields(view) -> list[tuple[str, str, bool]]:
    return [
        (entity.instance_id, field.field_id, field.value)
        for entity in view.entities
        for field in entity.fields
        if field.is_known and isinstance(field.value, bool)
    ]


def _declared_optional_fields(catalog, kind_id: str) -> list[str]:
    return sorted(
        item.id
        for item in catalog.fields_applicable_to_kind(kind_id)
        if not item.required and item.id not in RESERVED_DECLARATIONS
    )


def _absent_optional_fields(view, catalog) -> list[list[str]]:
    out: list[list[str]] = []
    for entity in view.entities:
        optional = _declared_optional_fields(catalog, entity.kind_id)
        present = set(entity.field_ids)
        for field_id in optional:
            if field_id not in present:
                out.append([entity.instance_id, field_id])
    return sorted(out)


def _pile_facts(view) -> dict[str, dict]:
    return {
        pile.pile_ref: {
            "total_count": pile.total_count,
            "members_complete": pile.members_complete,
            "visible_members": list(pile.visible_members),
            "known_positions": [[item.card_ref, item.position] for item in pile.known_positions],
        }
        for pile in view.piles
    }


def _relation_facts(view) -> list[list[str]]:
    return [[item.type_id, item.source_ref, item.target_ref] for item in view.relations]


def _sequence_facts(view) -> dict[str, list[str]]:
    return {f"{item.type_id}@{item.owner_ref}": list(item.elements) for item in view.sequences}


def _group_facts(view) -> dict[str, list[str]]:
    """组内数组顺序没有语义（协议 §2）：期望按成员集合排序后的结果给出。"""
    return {
        f"{item.type_id}@{item.owner_ref}": sorted(member.entity_ref for member in item.members)
        for item in view.groups
    }


def _candidate_facts(view) -> list[dict]:
    out = []
    for candidate in view.legal_actions:
        out.append(
            {
                "candidate_id": candidate.candidate_id,
                "action_type": candidate.action_type,
                "known_fields": {
                    field.field_id: field.value
                    for field in candidate.fields
                    if field.is_known
                },
            }
        )
    return out


def _content_id_groups(view) -> dict[str, list[str]]:
    groups: dict[str, list[str]] = {}
    for entity in view.entities:
        if entity.content_state == "known" and entity.content_id is not None:
            groups.setdefault(entity.content_id, []).append(entity.instance_id)
    return {content_id: ids for content_id, ids in groups.items() if len(ids) > 1}


def _terminal_facts(view) -> Optional[dict]:
    if view.terminal_result is None:
        return None
    return {
        "outcome": view.terminal_result.outcome,
        "reason_id": view.terminal_result.reason_id,
        "facts": {
            field.field_id: field.value
            for field in view.terminal_result.facts
            if field.is_known
        },
    }


def _field_type_by_instance(view, catalog) -> dict[str, dict[str, list]]:
    out: dict[str, dict[str, list]] = {}
    for entity in view.entities:
        entry: dict[str, list] = {}
        for field in entity.fields:
            declaration = catalog.field(field.field_id)
            entry[field.field_id] = [
                None if declaration is None else declaration.value_type,
                None if declaration is None else declaration.number_format,
            ]
        if entry:
            out[entity.instance_id] = entry
    return out


def _shared_local_names(catalog) -> dict[str, list[str]]:
    groups: dict[str, list[str]] = {}
    for declaration in catalog.declarations["fields"]:
        if ":" not in declaration.id:
            continue
        groups.setdefault(declaration.id.split(":", 1)[1], []).append(declaration.id)
    return {name: sorted(ids) for name, ids in groups.items() if len(ids) > 1}


def _conditional_fields(catalog) -> dict[str, list[str]]:
    return {
        item.id: sorted(item.visibility_check_ids)
        for item in catalog.declarations["fields"]
        if item.visibility == "conditional"
    }


_CATALOG_FACTS: dict[str, Callable] = {
    "protocol_version": lambda b: b["catalog_view"].protocol_version,
    "ruleset_id": lambda b: b["catalog_view"].ruleset_id,
    "catalog_id": lambda b: b["catalog_view"].catalog_id,
    "declaration_counts": lambda b: {
        table: len(items) for table, items in b["catalog_view"].declarations.items()
    },
    "catalog_digest": lambda b: layers.catalog_digest(b["catalog_doc"]),
    "conditional_fields": lambda b: _conditional_fields(b["catalog_view"]),
    "shared_local_names": lambda b: _shared_local_names(b["catalog_view"]),
    "coverage_mechanism_ids": lambda b: sorted(
        row["mechanism_id"] for row in (b["coverage_doc"] or {}).get("mechanisms", [])
    ),
}

_SNAPSHOT_FACTS: dict[str, Callable] = {
    "session_id": lambda b: b["view"].session_id,
    "observation_id": lambda b: b["view"].observation_id,
    "backend_profile_id": lambda b: b["view"].backend_profile_id,
    "status": lambda b: b["view"].status,
    "is_terminal": lambda b: b["view"].is_terminal,
    "entity_ids": lambda b: [entity.instance_id for entity in b["view"].entities],
    "entity_kind_counts": lambda b: {
        kind: sum(1 for entity in b["view"].entities if entity.kind_id == kind)
        for kind in sorted({entity.kind_id for entity in b["view"].entities})
    },
    "content_state_counts": lambda b: {
        state: sum(1 for entity in b["view"].entities if entity.content_state == state)
        for state in sorted({entity.content_state for entity in b["view"].entities})
    },
    "field_state_counts": lambda b: {
        state: len(items) for state, items in _field_state_map(b["view"]).items()
    },
    "known_zero_number_fields": lambda b: sorted(
        [instance_id, field_id]
        for instance_id, field_id, value in _number_fields(b["view"], b["catalog_view"])
        if layers.normalize_decimal(value) == "0"
    ),
    "known_false_boolean_fields": lambda b: sorted(
        [instance_id, field_id] for instance_id, field_id, value in _boolean_fields(b["view"]) if value is False
    ),
    "unknown_fields": lambda b: _field_state_map(b["view"]).get("unknown", []),
    "unavailable_fields": lambda b: _field_state_map(b["view"]).get("unavailable", []),
    "absent_optional_fields": lambda b: _absent_optional_fields(b["view"], b["catalog_view"]),
    "piles": lambda b: _pile_facts(b["view"]),
    "relations": lambda b: _relation_facts(b["view"]),
    "relation_count": lambda b: len(b["view"].relations),
    "sequences": lambda b: _sequence_facts(b["view"]),
    "group_members_sorted": lambda b: _group_facts(b["view"]),
    "candidate_ids": lambda b: list(b["view"].candidate_ids),
    "candidates": lambda b: _candidate_facts(b["view"]),
    "content_id_groups": lambda b: _content_id_groups(b["view"]),
    "terminal_facts": lambda b: _terminal_facts(b["view"]),
    "field_type_by_instance": lambda b: _field_type_by_instance(b["view"], b["catalog_view"]),
    "snapshot_digest": lambda b: layers.snapshot_storage_digest(b["snapshot_doc"]),
}


# --------------------------------------------------------------------------- #
# 装载
# --------------------------------------------------------------------------- #
def load_bundle(name: str) -> dict:
    """读取一个 fixture bundle（不校验；校验由各检查步骤自己做）。"""
    root = FIXTURE_ROOT / name
    bundle: dict[str, Any] = {
        "name": name,
        "catalog_path": root / "catalog.json",
        "catalog_doc": _read_json(root / "catalog.json"),
        "expected": _read_json(root / "expected.json"),
        "coverage_path": root / "coverage.json",
        "coverage_doc": None,
        "snapshots": {},
    }
    if bundle["coverage_path"].exists():
        bundle["coverage_doc"] = _read_json(bundle["coverage_path"])
    for stem in BUNDLES[name]:
        path = root / f"{stem}.json"
        bundle["snapshots"][stem] = {"path": path, "doc": _read_json(path)}
    return bundle


def _context(bundle: dict, snapshot_stem: str) -> dict:
    """构建 facts 提取上下文：先按共享分层检查，通过后再建视图。"""
    coverage = bundle["coverage_doc"]
    catalog_view = layers.parse_catalog_document(bundle["catalog_doc"], coverage=coverage)
    entry = bundle["snapshots"][snapshot_stem]
    view = layers.parse_snapshot_document(entry["doc"], bundle["catalog_doc"])
    return {
        "bundle": bundle["name"],
        "snapshot_stem": snapshot_stem,
        "catalog_doc": bundle["catalog_doc"],
        "catalog_view": catalog_view,
        "coverage_doc": coverage,
        "snapshot_doc": entry["doc"],
        "view": view,
    }


# --------------------------------------------------------------------------- #
# 检查套件
# --------------------------------------------------------------------------- #
def check_shared_layers(bundle: dict) -> list[Outcome]:
    """共享 L0–L3 分层检查必须对四类样例零违规。"""
    outcomes: list[Outcome] = []
    coverage = bundle["coverage_doc"]
    findings = layers.document_findings(
        bundle["catalog_doc"], "catalog", layers=("L1", "L2"), coverage=coverage
    )
    if coverage is not None:
        findings += layers.document_findings(coverage, "coverage", layers=("L1", "L2"))
    outcomes.append(
        Outcome(
            "shared-layers",
            f"{bundle['name']}/catalog",
            not findings,
            "; ".join(item.render() for item in findings[:4]),
        )
    )
    for stem, entry in bundle["snapshots"].items():
        problems = layers.document_findings(
            entry["doc"], "snapshot", layers=("L1", "L3"), catalog=bundle["catalog_doc"]
        )
        outcomes.append(
            Outcome(
                "shared-layers",
                f"{bundle['name']}/{stem}",
                not problems,
                "; ".join(item.render() for item in problems[:4]),
            )
        )
    return outcomes


def check_expectations(bundle: dict) -> list[Outcome]:
    """手写期望 vs contracts 视图事实，逐项比对。"""
    outcomes: list[Outcome] = []
    expected = bundle["expected"]
    for key, value in (expected.get("catalog") or {}).items():
        context = _context(bundle, BUNDLES[bundle["name"]][0])
        extractor = _CATALOG_FACTS.get(key)
        if extractor is None:
            outcomes.append(Outcome("expectations", f"{bundle['name']}/catalog/{key}", False, "未知事实名"))
            continue
        got = extractor(context)
        outcomes.append(
            Outcome(
                "expectations",
                f"{bundle['name']}/catalog/{key}",
                got == value,
                "" if got == value else f"得到 {got!r}，期望 {value!r}",
            )
        )
    for stem, wanted in (expected.get("snapshots") or {}).items():
        context = _context(bundle, stem)
        for key, value in wanted.items():
            extractor = _SNAPSHOT_FACTS.get(key)
            if extractor is None:
                outcomes.append(Outcome("expectations", f"{bundle['name']}/{stem}/{key}", False, "未知事实名"))
                continue
            got = extractor(context)
            outcomes.append(
                Outcome(
                    "expectations",
                    f"{bundle['name']}/{stem}/{key}",
                    got == value,
                    "" if got == value else f"得到 {got!r}，期望 {value!r}",
                )
            )
    return outcomes


def check_roundtrip(bundle: dict) -> list[Outcome]:
    """视图必须与合法 wire 逐键往返（不静默丢信息、不虚构键）。"""
    outcomes: list[Outcome] = []
    for stem in BUNDLES[bundle["name"]]:
        context = _context(bundle, stem)
        rebuilt = context["view"].to_wire()
        outcomes.append(
            Outcome(
                "roundtrip",
                f"{bundle['name']}/{stem}",
                rebuilt == bundle["snapshots"][stem]["doc"],
                "" if rebuilt == bundle["snapshots"][stem]["doc"] else "视图往返与 wire 不一致",
            )
        )
    catalog_view = layers.parse_catalog_document(bundle["catalog_doc"], coverage=bundle["coverage_doc"])
    outcomes.append(
        Outcome(
            "roundtrip",
            f"{bundle['name']}/catalog",
            catalog_view.to_wire() == bundle["catalog_doc"],
            "" if catalog_view.to_wire() == bundle["catalog_doc"] else "目录视图往返与 wire 不一致",
        )
    )
    return outcomes


def check_negative() -> list[Outcome]:
    """负例：声明式改动 + 独立写出的期望共享规则 ID。

    两种期望（见 ``fixtures/negative/cases.json`` 的 ``_notes``）：

    * ``expect=reject``（默认）：必须被拒绝，且实际命中集合覆盖 ``expect_rule_ids``；
    * ``expect=accept``：登记型 canary，记录共享资产**当前未**拒绝的结构（已知契约缺口）。
      只有"未被拒绝"才算 canary 成立；一旦共享侧补上规则，本项转为失败，提示把该 case
      改成 ``expect=reject`` 并写上规则 ID。AI 侧不放宽规则、也不私改协议。
    """
    path = FIXTURE_ROOT / "negative" / "cases.json"
    spec = _read_json(path)
    bases: dict[str, Any] = {}
    for name in BUNDLES:
        bundle = load_bundle(name)
        bases[f"{name}.catalog"] = bundle["catalog_doc"]
        if bundle["coverage_doc"] is not None:
            bases[f"{name}.coverage"] = bundle["coverage_doc"]
        for stem, entry in bundle["snapshots"].items():
            bases[f"{name}.{stem}"] = entry["doc"]

    outcomes: list[Outcome] = []
    for case in spec["cases"]:
        case_id = case["case_id"]
        fixture = case["fixture"]
        stem = case.get("snapshot", BUNDLES[fixture][0])
        role = case.get("role", "snapshot")
        active = set(case.get("layers") or ["L0", "L1", "L2", "L3"])
        expect = set(case.get("expect_rule_ids") or [])

        target_base = f"{fixture}.{role}" if role != "snapshot" else f"{fixture}.{stem}"
        if role == "coverage":
            target_base = f"{fixture}.coverage"
        overlay = {"$base": target_base}
        for op in ("$set", "$del", "$append", "$insert", "$move"):
            if op in case:
                overlay[op] = case[op]
        document = layers.spec_assets().check_contracts.resolve_document(overlay, bases)

        catalog_doc = bases[f"{fixture}.catalog"]
        coverage_doc = bases.get(f"{fixture}.coverage")
        if role == "catalog":
            catalog_doc = document
        findings = layers.document_findings(
            document, role, layers=active, catalog=catalog_doc, coverage=coverage_doc
        )
        got = {item.rule_id for item in findings}
        if case.get("expect", "reject") == "accept":
            ok = not findings
            detail = (
                "登记缺口仍成立（共享分层未拒绝）"
                if ok
                else f"登记缺口似已关闭：共享分层开始拒绝，命中 {sorted(got)}；"
                "请核对该规则后把本 case 改为 expect=reject"
            )
            outcomes.append(Outcome("negative", case_id, ok, detail))
            continue
        missing = sorted(expect - got)
        ok = bool(findings) and not missing
        detail = ""
        if not findings:
            detail = "未被拒绝（期望拒绝）"
        elif missing:
            detail = f"缺少期望规则 {missing}；实际 {sorted(got)}"
        else:
            detail = f"命中 {sorted(got)}"
        outcomes.append(Outcome("negative", case_id, ok, detail))
    return outcomes


def run_all(only: Optional[str] = None) -> list[Outcome]:
    outcomes: list[Outcome] = []
    for name in BUNDLES:
        bundle = load_bundle(name)
        outcomes.extend(check_shared_layers(bundle))
        outcomes.extend(check_expectations(bundle))
        outcomes.extend(check_roundtrip(bundle))
    outcomes.extend(check_negative())
    if only:
        outcomes = [item for item in outcomes if item.suite == only]
    return outcomes


__all__ = [
    "BUNDLES",
    "FIXTURE_ROOT",
    "Outcome",
    "check_expectations",
    "check_negative",
    "check_roundtrip",
    "check_shared_layers",
    "load_bundle",
    "run_all",
]
