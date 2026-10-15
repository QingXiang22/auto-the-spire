#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""A-03 检查套件：校验层（``encoding/validate.py``）的正反例。

职责分离与 A-01/A-02 一致：

* 输入是**既有手写夹具**（``fixtures/combat``、``fixtures/scenario``）的声明式叠加
  （``$base``/``$set``/``$del``/``$append``/``$insert`` 由共享 ``check_contracts.resolve_document``
  解析，不自写第二套叠加语义）；
* 期望写在 ``fixtures/validate/cases.json``：**手写的 (错误码, 路径) 集合**，
  由读规范（protocol.md §2 第 83–90 行的牌堆四条不变量、§1 字段/内容状态、
  契约不变量清单）独立写出，**不由 validate.py 产出或反填**；
* 断言是集合等值（既查漏报也查虚报），并对 ``ValidationReport.checked/skipped``
  断言本层实际执行与明确跳过的检查名，用来证明"覆盖不是空的、没查的不算通过"；
* 本模块不放宽任何检查、不复制协议实现、不导入活动 client/core 或 .NET 侧。

组名：``validate``。与 A-03 原文的对应关系见
``docs/review/tofix/a-03-2026-10-01.md`` 的对照表。
"""

from __future__ import annotations

import json
import re
from pathlib import Path
from typing import Any, Optional

from ai_the_spire.contracts import layers
from ai_the_spire.encoding import (
    SHARED_RULE_CODES,
    SHARED_RULE_FALLBACK,
    CapacityLimits,
    EncodingConfig,
    EncodingError,
    GymManifest,
    NumericTransform,
    build_manifest,
    require_valid_snapshot,
    validate_snapshot,
)

from fixture_suite import FIXTURE_ROOT, Outcome

SUITE = "validate"
VALIDATE_ROOT = FIXTURE_ROOT / "validate"

#: 手写基文档：validate 用例只写与基文档的差异（路径语义见 cases.json 的 _notes）。
BASE_DOCUMENTS = {
    "combat.catalog": FIXTURE_ROOT / "combat" / "catalog.json",
    "combat.snapshot": FIXTURE_ROOT / "combat" / "snapshot.json",
    "scenario.catalog": FIXTURE_ROOT / "scenario" / "catalog.json",
    "scenario.snapshot": FIXTURE_ROOT / "scenario" / "snapshot.json",
    "scenario.snapshot-terminal": FIXTURE_ROOT / "scenario" / "snapshot-terminal.json",
}

#: 默认容量上限（**输入**）：与 ``encoding_suite.CAPACITIES`` 同值，便于跨组对照；
#: 用例用 ``capacity_overrides`` 只写差异。
CAPACITIES = {
    "max_entities": 64,
    "max_fields": 64,
    "max_relations": 64,
    "max_pile_positions": 32,
    "max_sequences": 8,
    "max_groups": 8,
    "max_candidates": 8,
    "max_candidate_fields": 32,
}


# --------------------------------------------------------------------------- #
# 装载
# --------------------------------------------------------------------------- #
def _read_json(path: Path) -> Any:
    with path.open("r", encoding="utf-8") as handle:
        return json.load(handle)


def _base_documents() -> dict[str, Any]:
    return {name: _read_json(path) for name, path in BASE_DOCUMENTS.items()}


def _cases() -> list[dict]:
    return _read_json(VALIDATE_ROOT / "cases.json")["cases"]


def _environment() -> dict:
    return _read_json(FIXTURE_ROOT / "manifest" / "expected.json")["environment"]


class _Context:
    """一次运行的共享上下文：基文档、环境身份与 Manifest 缓存。"""

    def __init__(self) -> None:
        self.bases = _base_documents()
        self.environment = _environment()
        self._manifests: dict[str, GymManifest] = {}

    def resolve(self, spec: Any) -> Any:
        return layers.spec_assets().check_contracts.resolve_document(spec, self.bases)

    def capacity_limits(self, case: dict) -> CapacityLimits:
        merged = dict(CAPACITIES)
        merged.update(case.get("capacity_overrides") or {})
        return CapacityLimits(**merged)

    def config(self, case: dict) -> EncodingConfig:
        transforms = {
            field_id: NumericTransform(offset=offset, scale=scale)
            for field_id, (offset, scale) in (case.get("transforms") or {}).items()
        }
        return EncodingConfig(
            transforms=transforms,
            capacities=self.capacity_limits(case),
            **(case.get("tolerances") or {}),
        )

    def manifest(self, case: dict, catalog_document: Any) -> GymManifest:
        config = self.config(case)
        key = json.dumps(
            {"catalog": catalog_document, "config": config.to_wire()},
            sort_keys=True,
            ensure_ascii=False,
        )
        if key not in self._manifests:
            self._manifests[key] = build_manifest(
                catalog_document,
                config,
                backend_profile_id=self.environment["backend_profile_id"],
                gym_id=self.environment["gym_id"],
            )
        return self._manifests[key]


# --------------------------------------------------------------------------- #
# 用例执行
# --------------------------------------------------------------------------- #
def _snapshot_input(case: dict, context: _Context, catalog_document: Any) -> Any:
    """按 ``input`` 构造被测入口的实参（document / PublicSnapshot 视图 / 非对象）。"""
    kind = case.get("input", "document")
    if kind == "document":
        return context.resolve(case["snapshot"])
    if kind == "view":
        document = context.resolve(case["snapshot"])
        return layers.parse_snapshot_document(document, catalog_document)
    if kind == "non-mapping":
        return ["not", "a", "snapshot"]
    raise ValueError(f"未知 input 类型 {kind!r}（可用 document/view/non-mapping）")


def _pairs(errors) -> list[list[str]]:
    return sorted([error.code, error.path] for error in errors)


def _expected_pairs(case: dict) -> list[list[str]]:
    return sorted([code, path] for code, path in case.get("errors", []))


def _compare_sets(label: str, got: set, wanted: set, problems: list[str]) -> None:
    if got == wanted:
        return
    missing = sorted(wanted - got)
    extra = sorted(got - wanted)
    problems.append(f"{label} 不符：缺少 {missing!r}，多出 {extra!r}；实际 {sorted(got)!r}")


def _prepare(case: dict, context: _Context) -> tuple[Any, GymManifest, Any]:
    """按用例构造 ``(snapshot 实参, manifest, catalog 实参)``。"""
    bundle = case["bundle"]
    catalog_document = (
        context.resolve(case["catalog"])
        if "catalog" in case
        else context.bases[f"{bundle}.catalog"]
    )
    passed_catalog = (
        context.resolve(case["passed_catalog"])
        if "passed_catalog" in case
        else catalog_document
    )
    manifest = context.manifest(case, catalog_document)
    snapshot = _snapshot_input(case, context, catalog_document)
    catalog_argument = passed_catalog if case.get("pass_catalog", True) else None
    return snapshot, manifest, catalog_argument


def _sweep(context: _Context):
    """逐例构造并校验；产出 ``(case, report)``。"""
    for case in _cases():
        snapshot, manifest, catalog_argument = _prepare(case, context)
        yield case, validate_snapshot(snapshot, manifest, catalog=catalog_argument)


def _run_case(case: dict, context: _Context) -> Outcome:
    case_id = case["case_id"]
    problems: list[str] = []
    try:
        snapshot, manifest, catalog_argument = _prepare(case, context)
        report = validate_snapshot(snapshot, manifest, catalog=catalog_argument)
    except Exception as exc:  # noqa: BLE001 - 套件异常逐例记账，不中止整套
        return Outcome(SUITE, case_id, False, f"夹具或构建失败 {type(exc).__name__}: {exc}")

    # 1) (错误码, 路径) 集合等值：既查漏报也查虚报。
    got_pairs = {tuple(pair) for pair in _pairs(report.errors)}
    wanted_pairs = {tuple(pair) for pair in _expected_pairs(case)}
    if got_pairs != wanted_pairs:
        missing = sorted(wanted_pairs - got_pairs)
        extra = sorted(got_pairs - wanted_pairs)
        problems.append(
            f"(code, path) 不匹配：缺少 {missing!r}，多出 {extra!r}；"
            f"实际 {sorted(got_pairs)!r}"
        )

    # 2) 实际执行 / 明确跳过的检查名。
    if "checked_exact" in case:
        _compare_sets("checked", set(report.checked), set(case["checked_exact"]), problems)
    for name in case.get("checked_contains", []):
        if name not in report.checked:
            problems.append(f"checked 缺少 {name!r}；实际 {list(report.checked)!r}")
    if "skipped_exact" in case:
        _compare_sets("skipped", set(report.skipped), set(case["skipped_exact"]), problems)
    for name in case.get("skipped_contains", []):
        if name not in report.skipped:
            problems.append(f"skipped 缺少 {name!r}；实际 {list(report.skipped)!r}")

    # 3) 失败不返回部分编码：首错抛出路径与报告一致。
    if case.get("raise_first"):
        if wanted_pairs:
            wanted_first = tuple(case["errors"][0])
            for label, run in (
                ("report.raise_first", report.raise_first),
                (
                    "require_valid_snapshot",
                    lambda: require_valid_snapshot(snapshot, manifest, catalog=catalog_argument),
                ),
            ):
                try:
                    run()
                except EncodingError as exc:
                    if (exc.code, exc.path) != wanted_first:
                        problems.append(
                            f"{label} 抛出 {(exc.code, exc.path)!r}，期望 {wanted_first!r}"
                        )
                else:
                    problems.append(f"{label} 未抛出首条错误 {wanted_first!r}")
        else:
            report.raise_first()
            require_valid_snapshot(snapshot, manifest, catalog=catalog_argument)

    detail = "；".join(problems)
    return Outcome(SUITE, case_id, not problems, detail)


def check_report_read_surface(context: _Context) -> Outcome:
    """``ValidationReport`` 的读取面：ok/first/codes/paths/真值/无错时不抛。"""
    case_id = "api/report-read-surface"
    problems: list[str] = []
    try:
        catalog_document = context.bases["combat.catalog"]
        manifest = context.manifest({"bundle": "combat"}, catalog_document)
        clean = validate_snapshot(
            context.bases["combat.snapshot"], manifest, catalog=catalog_document
        )
        broken_document = context.resolve(
            {
                "$base": "combat.snapshot",
                "$insert": {
                    "entities.0.fields": [
                        2,
                        {"field_id": "fixture:ghost", "state": "known", "value": "1"},
                    ]
                },
            }
        )
        broken = validate_snapshot(broken_document, manifest, catalog=catalog_document)
    except Exception as exc:  # noqa: BLE001
        return Outcome(SUITE, case_id, False, f"构建失败 {type(exc).__name__}: {exc}")

    if not clean.ok or clean.first() is not None:
        problems.append("通过的报告 ok/first 读取面不对")
    if clean.codes() or clean.paths() or clean.render():
        problems.append("通过的报告不该有错误码/路径/文本")
    if bool(clean) is not False:
        problems.append("通过的报告在布尔语境应为假")
    if clean.raise_first() is not None:
        problems.append("通过的报告 raise_first 不该有返回或有副作用")

    if broken.ok or bool(broken) is not True:
        problems.append("失败的报告 ok/布尔语境不对")
    if [error.code for error in broken.errors] != list(broken.codes()):
        problems.append("codes() 与 errors 顺序不一致")
    if [error.path for error in broken.errors] != list(broken.paths()):
        problems.append("paths() 与 errors 顺序不一致")
    if [error.render() for error in broken.errors] != broken.render():
        problems.append("render() 与 errors 不一致")
    try:
        broken.raise_first()
    except EncodingError as exc:
        if exc is not broken.first():
            problems.append("raise_first 抛出的不是 first()")
    else:
        problems.append("失败的报告 raise_first 未抛出")

    return Outcome(SUITE, case_id, not problems, "；".join(problems))


# --------------------------------------------------------------------------- #
# 组级金丝雀：检查名覆盖 与 共享规则映射
# --------------------------------------------------------------------------- #
#: 本层会宣布的检查名全集（手写，来自对 validate.py 的逐条通读；A-03 原文的每一项都必须
#: 至少有一个用例让它真正执行）。实现新增/改名检查名而不补用例时本项转失败。
CHECKED_UNIVERSE = frozenset(
    {
        "encoding/manifest-layout",
        "encoding/catalog-binding",
        "encoding/exact-identity",
        "structure/top-level+status-pairing",
        "content/content-state",
        "fields/dictionary+applicability+required+value",
        "references/entity-closure",
        "candidates/handles+semantics+fields",
        "piles/v06-1-position-bounds",
        "piles/v06-2-member-closure+count",
        "piles/v06-3-position-member-consistency",
        "piles/v06-4-no-fabricated-cards",
        "capacity/limits",
        "capacity/none-configured",
        "terminal/facts-fields+value",
        "shared/L1-structure",
        "shared/L3-snapshot-vs-catalog",
        "shared/L1+L3-aborted-by-shared-exception",
    }
)

#: 缺目录/缺身份键时**必须**出现在 skipped 里的检查名全集（手写）。
SKIPPED_UNIVERSE = frozenset(
    {
        "encoding/catalog-binding",
        "encoding/exact-identity(快照身份键缺失或非字符串)",
        "terminal/reason-declaration(reason_id 注册与 outcome 需目录)",
        "shared/L3-declaration-domain(成员kind·pile_type·关系种类与位置·序列组基数·终局facts·保留声明)",
    }
)


def check_check_name_coverage(context: _Context) -> Outcome:
    """所有检查名都真的被执行/明确跳过过：覆盖不是空的，没查的不算通过。"""
    case_id = "coverage/check-names-executed"
    checked: set[str] = set()
    skipped: set[str] = set()
    try:
        for _, report in _sweep(context):
            checked.update(report.checked)
            skipped.update(report.skipped)
    except Exception as exc:  # noqa: BLE001
        return Outcome(SUITE, case_id, False, f"扫描失败 {type(exc).__name__}: {exc}")

    problems: list[str] = []
    if checked != CHECKED_UNIVERSE:
        problems.append(
            f"checked 全集不符：未被执行 {sorted(CHECKED_UNIVERSE - checked)!r}，"
            f"多出 {sorted(checked - CHECKED_UNIVERSE)!r}"
        )
    if skipped != SKIPPED_UNIVERSE:
        problems.append(
            f"skipped 全集不符：未出现 {sorted(SKIPPED_UNIVERSE - skipped)!r}，"
            f"多出 {sorted(skipped - SKIPPED_UNIVERSE)!r}"
        )
    return Outcome(SUITE, case_id, not problems, "；".join(problems))


def check_shared_rule_mapping(context: _Context) -> Outcome:
    """共享规则 ID → 编码错误码：未登记规则必须显式落到兜底码，不能静默换码。

    只看**从共享层换算过来**的错误（消息带 ``[L?/I-xx]`` 前缀）；编码层自有检查也会在
    ``related_ids`` 里引用 ``I-xx`` 说明依据，那不是共享结论，不能混算。
    """
    case_id = "mapping/shared-rule-ids-registered-or-fallback"
    observed: set[str] = set()
    problems: list[str] = []
    try:
        for case, report in _sweep(context):
            for error in report.errors:
                match = re.match(r"^\[L\d/(I-\d+)\]", error.message)
                if match is None:
                    continue
                rule = match.group(1)
                observed.add(rule)
                if rule in SHARED_RULE_CODES:
                    continue
                if error.code != SHARED_RULE_FALLBACK:
                    problems.append(
                        f"{case['case_id']}: 未登记共享规则 {rule} 落到 {error.code!r}，"
                        f"期望兜底 {SHARED_RULE_FALLBACK!r}"
                    )
    except Exception as exc:  # noqa: BLE001
        return Outcome(SUITE, case_id, False, f"扫描失败 {type(exc).__name__}: {exc}")

    unmapped = sorted(rule for rule in observed if rule not in SHARED_RULE_CODES)
    detail = f"共享规则命中 {sorted(observed)}"
    if unmapped:
        detail += f"；其中未登记（按兜底码报告）: {unmapped}"
    if problems:
        detail = "；".join(problems) + "；" + detail
    return Outcome(SUITE, case_id, not problems, detail)


# --------------------------------------------------------------------------- #
# 入口
# --------------------------------------------------------------------------- #
def run_all(only: Optional[str] = None) -> list[Outcome]:
    outcomes: list[Outcome] = []
    if only is not None and only != SUITE:
        return outcomes
    context = _Context()
    outcomes.append(check_report_read_surface(context))
    outcomes.append(check_check_name_coverage(context))
    outcomes.append(check_shared_rule_mapping(context))
    for case in _cases():
        outcomes.append(_run_case(case, context))
    return outcomes


__all__ = [
    "BASE_DOCUMENTS",
    "CAPACITIES",
    "CHECKED_UNIVERSE",
    "SKIPPED_UNIVERSE",
    "SUITE",
    "VALIDATE_ROOT",
    "check_check_name_coverage",
    "check_report_read_surface",
    "check_shared_rule_mapping",
    "run_all",
]
