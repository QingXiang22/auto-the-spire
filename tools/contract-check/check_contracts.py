#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""net-the-spire 契约检查入口（R-P01）。

用法（仓库根目录）：

    python tools/contract-check/check_contracts.py
    python tools/contract-check/check_contracts.py --verbose
    python tools/contract-check/check_contracts.py --only canonical

退出码：0 全部通过；1 存在失败用例；2 运行环境/资产缺失。

分层：
  L0  解析层（重复键、浮点字面量、非法 Unicode）
  L1  结构层（wire.schema.json，JSON Schema 2020-12 子集）
  L2  目录内部语义（引用闭合、kind_id 独占、能力层级、覆盖表）
  L3  快照对目录（字段值类型、终局 facts、V-06 牌堆、排序、动作语义键）
  L4  控制与账本（operation→payload/result 配对、响应状态组合、seed 上界）
"""

from __future__ import annotations

import argparse
import copy
import json
import sys
from dataclasses import dataclass, field
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO_ROOT = HERE.parents[1]
CONTRACT_DIR = REPO_ROOT / "docs" / "contracts" / "net" / "0.1"
SCHEMA_PATH = CONTRACT_DIR / "wire.schema.json"
COVERAGE_SCHEMA_PATH = CONTRACT_DIR / "coverage.schema.json"
FIXTURES = HERE / "fixtures"
CASES_DIR = FIXTURES / "cases"
DOCUMENTS_DIR = FIXTURES / "documents"
GOLDEN_PATH = FIXTURES / "golden" / "canonical-vectors.json"

sys.path.insert(0, str(HERE))

import jsonschema_min  # noqa: E402
import schema_policy  # noqa: E402
import wire_layers  # noqa: E402
from wire_canonical import (  # noqa: E402
    CanonicalError,
    action_semantic_key,
    canonical_bytes,
    canonical_text,
    digest_bytes,
    is_canonical_decimal,
    normalize_decimal,
)
from wire_json import ParseError, parse_json_strict  # noqa: E402

DOC_ROLES = {
    "catalog": "$defs/PublicCatalog",
    "snapshot": "$defs/PublicSnapshot",
    "request": "$defs/Request",
    "response": "$defs/Response",
    "capabilities": "$defs/Capabilities",
    "observe_result": "$defs/ObserveResult",
    "attach_result": "$defs/AttachResult",
    "close_result": "$defs/CloseResult",
    "start_result": "$defs/StartResult",
    "request_status_result": "$defs/RequestStatusResult",
    "describe_result": "$defs/DescribeResult",
    "error": "$defs/Error",
    "identity": "$defs/Identity",
    "start_payload": "$defs/StartPayload",
}


@dataclass
class Outcome:
    suite: str
    case_id: str
    ok: bool
    detail: str = ""
    rule_ids: list[str] = field(default_factory=list)


def load_json(path: Path):
    with path.open("r", encoding="utf-8") as handle:
        return json.load(handle)


def resolve_def(schema: dict, pointer: str):
    node = schema
    for token in pointer.split("/"):
        node = node[token]
    return node


# --------------------------------------------------------------------------- #
# 叠加（$base/$set/$del）：让反例只写差异，期望仍独立手写
# --------------------------------------------------------------------------- #
def _path_tokens(path: str) -> list:
    return [int(token) if token.isdigit() else token for token in path.split(".")]


def _set_path(document, path: str, value) -> None:
    tokens = _path_tokens(path)
    node = document
    for token in tokens[:-1]:
        node = node[token]
    node[tokens[-1]] = value


def _del_path(document, path: str) -> None:
    tokens = _path_tokens(path)
    node = document
    for token in tokens[:-1]:
        node = node[token]
    if isinstance(node, list):
        del node[tokens[-1]]
    else:
        node.pop(tokens[-1], None)


def _append_path(document, path: str, value) -> None:
    tokens = _path_tokens(path)
    node = document
    for token in tokens:
        node = node[token]
    node.append(value)


def _insert_path(document, path: str, spec) -> None:
    index, value = spec
    tokens = _path_tokens(path)
    node = document
    for token in tokens:
        node = node[token]
    node.insert(index, value)


def _move_path(document, path: str, spec) -> None:
    source_index, target_index = spec
    tokens = _path_tokens(path)
    node = document
    for token in tokens:
        node = node[token]
    node.insert(target_index, node.pop(source_index))


def _resolve_nested_refs(node, bases: dict):
    """把文档内部的 "doc:<name>" 字符串替换为基文档（便于组合 observe/describe 结果）。"""
    if isinstance(node, str) and node.startswith("doc:"):
        return copy.deepcopy(bases[node[4:]])
    if isinstance(node, dict):
        return {key: _resolve_nested_refs(value, bases) for key, value in node.items()}
    if isinstance(node, list):
        return [_resolve_nested_refs(item, bases) for item in node]
    return node


def resolve_document(spec, bases: dict):
    if isinstance(spec, dict) and "$base" in spec:
        document = copy.deepcopy(bases[spec["$base"]])
        for path, value in (spec.get("$set") or {}).items():
            _set_path(document, path, value)
        for path, value in (spec.get("$append") or {}).items():
            _append_path(document, path, value)
        for path, value in (spec.get("$insert") or {}).items():
            _insert_path(document, path, value)
        for path, value in (spec.get("$move") or {}).items():
            _move_path(document, path, value)
        for path in spec.get("$del") or []:
            _del_path(document, path)
        return _resolve_nested_refs(document, bases)
    if isinstance(spec, str) and spec.startswith("doc:"):
        return copy.deepcopy(bases[spec[4:]])
    return _resolve_nested_refs(copy.deepcopy(spec), bases)


# --------------------------------------------------------------------------- #
# 用例执行
# --------------------------------------------------------------------------- #
def run_case(case: dict, schema: dict, coverage_schema: dict | None, bases: dict, verbose: bool) -> Outcome:
    case_id = case["case_id"]
    layers = set(case.get("layers") or ["L0", "L1", "L2", "L3", "L4"])
    expected = case.get("expect", "accept")
    expected_rules = set(case.get("rule_ids") or [])
    findings: list[wire_layers.Finding] = []

    def add(rule_id: str, layer: str, path: str, message: str) -> None:
        findings.append(wire_layers.Finding(rule_id, layer, path, message))

    documents = {}
    for role, spec in (case.get("documents") or {}).items():
        documents[role] = resolve_document(spec, bases)
    for role, raw in (case.get("raw_documents") or {}).items():
        if "L0" in layers:
            try:
                parse_json_strict(raw, what=role)
            except ParseError as exc:
                add(exc.rule_id, "L0", role, exc.message)
                continue
        documents[role] = json.loads(raw)

    # L0/L1：逐文档解析与结构校验
    if "L0" in layers or "L1" in layers:
        for role, document in documents.items():
            if "L0" in layers:
                try:
                    parse_json_strict(json.dumps(document, ensure_ascii=False), what=role)
                except ParseError as exc:
                    add(exc.rule_id, "L0", role, exc.message)
                    continue
            if "L1" not in layers:
                continue
            if role == "coverage":
                if coverage_schema is not None:
                    for message in jsonschema_min.validate(document, coverage_schema):
                        # 覆盖表是 C-01 固定的机器资产；结构拒绝与 wire 文档一致记为 I-66，
                        # 语义（引用闭合/证据要求）另记 I-75/I-85。
                        add("I-66", "L1", f"{role}{message[1:]}", message)
                continue
            pointer = DOC_ROLES.get(role)
            if pointer is None:
                continue
            target = resolve_def(schema, pointer)
            for message in jsonschema_min.validate(document, target, schema):
                add("I-66", "L1", f"{role}{message[1:]}", message)

    catalog = documents.get("catalog")
    snapshot = documents.get("snapshot")
    coverage = documents.get("coverage")

    if "L2" in layers:
        if coverage is not None:
            findings.extend(wire_layers.check_coverage(coverage))
        if catalog is not None:
            findings.extend(wire_layers.check_catalog(catalog, coverage))
            if coverage is not None:
                findings.extend(wire_layers.check_catalog_coverage(catalog, coverage))

    if "L3" in layers and snapshot is not None:
        findings.extend(wire_layers.check_snapshot(snapshot, catalog))

    if "L4" in layers:
        requests = list(case.get("requests") or [])
        if documents.get("request") is not None:
            requests.insert(0, documents["request"])
        responses = list(case.get("responses") or [])
        if documents.get("response") is not None:
            responses.insert(0, documents["response"])
        findings.extend(
            wire_layers.check_control(
                requests=requests,
                responses=responses,
                ledger_entries=case.get("ledger"),
                capabilities=documents.get("capabilities"),
                catalog=catalog,
            )
        )
        if documents.get("capabilities") is not None:
            findings.extend(wire_layers.check_capabilities(documents["capabilities"], catalog))
        for role in ("observe_result", "attach_result"):
            if documents.get(role) is not None:
                findings.extend(wire_layers.check_result_state_combinations(documents[role], role))

    # 期望比对
    got_rule_ids = {finding.rule_id for finding in findings}
    if expected == "accept":
        ok = not findings
        detail = "无违规" if ok else "; ".join(finding.render() for finding in findings[:4])
    else:
        ok = bool(findings)
        if ok and expected_rules:
            ok = bool(got_rule_ids & expected_rules)
        detail = (
            "按要求拒绝: " + ", ".join(sorted(got_rule_ids))
            if ok
            else (
                "未被拒绝" if not findings else f"拒绝规则不匹配: 期望 {sorted(expected_rules)} 实际 {sorted(got_rule_ids)}"
            )
        )
    return Outcome("fixtures", case_id, ok, detail, sorted(got_rule_ids))


# --------------------------------------------------------------------------- #
# 单元向量
# --------------------------------------------------------------------------- #
def run_golden(golden: dict) -> list[Outcome]:
    outcomes: list[Outcome] = []

    for vector in golden.get("canonical", []):
        case_id = f"canonical:{vector['id']}"
        try:
            text = canonical_text(vector["document"])
            payload = canonical_bytes(vector["document"])
        except CanonicalError as exc:
            outcomes.append(Outcome("canonical", case_id, False, f"编码失败: {exc}"))
            continue
        problems = []
        if text != vector["expect_text"]:
            problems.append(f"文本 {text!r} != {vector['expect_text']!r}")
        if digest_bytes(payload) != vector["expect_sha256"]:
            problems.append(f"摘要 {digest_bytes(payload)} != {vector['expect_sha256']} (由 {vector.get('digest_tool', 'independent tool')} 产出)")
        outcomes.append(Outcome("canonical", case_id, not problems, "; ".join(problems)))

    for vector in golden.get("canonical_reject", []):
        case_id = f"canonical-reject:{vector['id']}"
        document = vector["document"]
        # 期望：无法生成规范字节或文本不等于任何合法规范形式
        try:
            text = canonical_text(document)
        except CanonicalError as exc:
            ok = bool(vector.get("expect_error"))
            outcomes.append(Outcome("canonical", case_id, ok, f"按预期拒绝: {exc}"))
            continue
        ok = False
        outcomes.append(Outcome("canonical", case_id, ok, f"本应拒绝却编码为 {text!r}"))

    for vector in golden.get("decimal", []):
        case_id = f"decimal:{vector['id']}"
        try:
            got = normalize_decimal(vector["source"])
        except CanonicalError as exc:
            outcomes.append(Outcome("decimal", case_id, False, f"规范化失败: {exc}"))
            continue
        ok = got == vector["expect"]
        outcomes.append(Outcome("decimal", case_id, ok, f"{vector['source']!r} -> {got!r}，期望 {vector['expect']!r}"))

    for vector in golden.get("decimal_accept", []):
        case_id = f"decimal-accept:{vector}"
        ok = is_canonical_decimal(vector)
        outcomes.append(Outcome("decimal", case_id, ok, f"应接受 {vector!r}"))

    for vector in golden.get("decimal_reject", []):
        case_id = f"decimal-reject:{vector}"
        ok = not is_canonical_decimal(vector)
        outcomes.append(Outcome("decimal", case_id, ok, f"应拒绝 {vector!r}"))

    for vector in golden.get("action_key", []):
        case_id = f"action-key:{vector['id']}"
        problems = []
        if "expect_equal" in vector:
            keys = [action_semantic_key(action) for action in vector["actions"]]
            if len(set(keys)) != 1:
                problems.append(f"语义键不相等: {[k.decode() for k in keys]}")
        if "expect_distinct" in vector:
            keys = [action_semantic_key(action) for action in vector["actions"]]
            if len(set(keys)) != len(keys):
                problems.append("语义键本应互不相同")
        if "expect_key_text" in vector:
            got = action_semantic_key(vector["actions"][0]).decode()
            if got != vector["expect_key_text"]:
                problems.append(f"{got!r} != {vector['expect_key_text']!r}")
        outcomes.append(Outcome("action-key", case_id, not problems, "; ".join(problems)))

    for vector in golden.get("identity", []):
        case_id = f"identity:{vector['id']}"
        from wire_canonical import (
            backend_profile_digest,
            catalog_digest,
            request_payload_fingerprint,
            ruleset_digest,
            snapshot_storage_digest,
        )

        table = {
            "catalog": catalog_digest,
            "ruleset": ruleset_digest,
            "backend_profile": backend_profile_digest,
            "snapshot": snapshot_storage_digest,
        }
        try:
            if vector["kind"] == "request":
                got = request_payload_fingerprint(vector["operation"], vector["payload"])
            else:
                got = table[vector["kind"]](vector["input"])
        except CanonicalError as exc:
            outcomes.append(Outcome("identity", case_id, False, f"摘要失败: {exc}"))
            continue
        if "expect_stable" in vector:
            again = None
            try:
                again = table[vector["kind"]](vector["input"]) if vector["kind"] != "request" else request_payload_fingerprint(
                    vector["operation"], vector["payload"]
                )
            except CanonicalError:
                again = got
            ok = got == again and got.startswith("sha256:")
            outcomes.append(Outcome("identity", case_id, ok, f"{got}（可复现）"))
        else:
            ok = got == vector["expect"]
            outcomes.append(Outcome("identity", case_id, ok, f"{got}，期望 {vector.get('expect')}"))
    return outcomes


def run_policy(schema: dict) -> list[Outcome]:
    outcomes = []
    for rule_id, ok, detail in schema_policy.policy_checks(schema):
        outcomes.append(Outcome("schema-policy", rule_id, ok, detail))
    # 结构复核：根 oneOf 必须覆盖四类顶层消息
    root = schema.get("oneOf") or []
    names = sorted(branch.get("$ref", "").rsplit("/", 1)[-1] for branch in root)
    outcomes.append(
        Outcome(
            "schema-policy",
            "SCHEMA-18",
            names == ["PublicCatalog", "PublicSnapshot", "Request", "Response"],
            f"根 oneOf={names}",
        )
    )
    return outcomes


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="net-the-spire 契约检查（R-P01）")
    parser.add_argument("--verbose", action="store_true", help="打印每个用例")
    parser.add_argument("--only", default=None, help="只运行指定 suite（canonical/decimal/action-key/identity/fixtures/schema-policy）")
    args = parser.parse_args(argv)

    if not SCHEMA_PATH.exists():
        print(f"缺少 schema: {SCHEMA_PATH}", file=sys.stderr)
        return 2
    schema = load_json(SCHEMA_PATH)
    coverage_schema = load_json(COVERAGE_SCHEMA_PATH) if COVERAGE_SCHEMA_PATH.exists() else None
    golden = load_json(GOLDEN_PATH)

    bases = {}
    for path in sorted(DOCUMENTS_DIR.glob("*.json")):
        bases[path.stem] = load_json(path)

    outcomes: list[Outcome] = []
    outcomes.extend(run_policy(schema))
    outcomes.extend(run_golden(golden))
    for path in sorted(CASES_DIR.glob("*.json")):
        bundle = load_json(path)
        for case in bundle.get("cases", []):
            outcomes.append(run_case(case, schema, coverage_schema, bases, args.verbose))

    if args.only:
        outcomes = [item for item in outcomes if item.suite == args.only]

    failures = [item for item in outcomes if not item.ok]
    suites: dict[str, list[Outcome]] = {}
    for item in outcomes:
        suites.setdefault(item.suite, []).append(item)

    print(f"wire schema: {SCHEMA_PATH.relative_to(REPO_ROOT)}")
    print(f"coverage schema: {'present' if coverage_schema else 'ABSENT'}")
    for name in sorted(suites):
        items = suites[name]
        passed = sum(1 for item in items if item.ok)
        print(f"  {name:<16} {passed}/{len(items)} passed")
    if args.verbose:
        for item in outcomes:
            mark = "ok  " if item.ok else "FAIL"
            print(f"    {mark} [{item.suite}] {item.case_id}: {item.detail}")

    print(f"TOTAL: {len(outcomes) - len(failures)} passed, {len(failures)} failed, {len(outcomes)} executed")
    for item in failures:
        print(f"FAIL [{item.suite}] {item.case_id}: {item.detail}", file=sys.stderr)
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
