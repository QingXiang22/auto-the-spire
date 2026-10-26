#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""N-01 跨端对照夹具生成器（只读消费者，不修改 P-01 资产）。

本脚本复用 ``tools/contract-check`` 里的**唯一一份**规范实现
（``wire_canonical`` / ``wire_json`` / ``jsonschema_min`` / ``check_contracts``），
把 R-P01 的同一批正反例（``fixtures/cases/*.json``、``fixtures/documents/*.json``、
``fixtures/golden/canonical-vectors.json``）导出为 C# 端可复算的对照清单：

  * 每个用例每个角色的**规范文本与 sha256**（C# 必须逐字节复算相同）；
  * 每个用例的 **L0/L1** 判定（Python 侧），供 C# 的 schema 驱动 L1 校验比对；
  * 每个用例声明的 ``layers`` / ``expect`` / ``rule_ids``。

用法（仓库根目录）：

    python net-the-spire/tools/emit_cross_end_manifest.py

输出：``net-the-spire/Net.TheSpire.Tests/fixtures/cross-end-canonical.json``
（逐文档规范文本 + 摘要）与 ``.../cross-end-cases.json``（逐用例 L0/L1 判定）。
"""

from __future__ import annotations

import json
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
NET_ROOT = HERE.parent
REPO_ROOT = NET_ROOT.parent
CHECK_DIR = REPO_ROOT / "tools" / "contract-check"
CONTRACT_DIR = REPO_ROOT / "docs" / "specs" / "net" / "0.2"
FIXTURES = CHECK_DIR / "fixtures"
OUT_DIR = NET_ROOT / "Net.TheSpire.Tests" / "fixtures"

sys.path.insert(0, str(CHECK_DIR))

import check_contracts  # noqa: E402
import jsonschema_min  # noqa: E402
from wire_canonical import (  # noqa: E402
    CanonicalError,
    CANONICAL_VERSION,
    canonical_text,
    digest_bytes,
)
from wire_json import ParseError, parse_json_strict  # noqa: E402


def load_json(path: Path):
    with path.open("r", encoding="utf-8") as handle:
        return json.load(handle)


#: 小于该长度的文档在清单里连规范文本一并保存（便于逐字诊断）；
#: 更长的文档只保存摘要 + 字节长度（C# 侧仍按规范字节逐字节复算，摘要不符即失败）。
_INLINE_TEXT_LIMIT = 2048


def canonical_entry(doc):
    """规范文本失败时记录错误种类，而不是悄悄跳过。"""
    try:
        text = canonical_text(doc)
    except CanonicalError as exc:
        return {"canonical_error": str(exc)}
    payload = text.encode("utf-8")
    entry = {
        "sha256": digest_bytes(payload),
        "byte_length": len(payload),
    }
    if len(payload) <= _INLINE_TEXT_LIMIT:
        entry["text"] = text
    else:
        entry["text_prefix"] = text[:120]
    return entry


def schema_findings(role: str, document, schema, coverage_schema):
    """L1：与 ``check_contracts.run_case`` 同一入口、同一 message 文本。"""
    messages: list[str] = []
    if role == "coverage":
        if coverage_schema is not None:
            messages.extend(jsonschema_min.validate(document, coverage_schema))
        return messages
    pointer = check_contracts.DOC_ROLES.get(role)
    if pointer is None:
        return messages
    target = check_contracts.resolve_def(schema, pointer)
    messages.extend(jsonschema_min.validate(document, target, schema))
    return messages


def main() -> int:
    schema = load_json(CONTRACT_DIR / "wire.schema.json")
    coverage_schema_path = CONTRACT_DIR / "coverage.schema.json"
    coverage_schema = load_json(coverage_schema_path) if coverage_schema_path.exists() else None

    bases = {}
    for path in sorted((FIXTURES / "documents").glob("*.json")):
        bases[path.stem] = load_json(path)

    canonical_docs: list[dict] = []
    raw_docs: list[dict] = []
    cases_out: list[dict] = []

    for base_name, document in sorted(bases.items()):
        entry = {"id": f"base:{base_name}", "role": base_name}
        entry.update(canonical_entry(document))
        canonical_docs.append(entry)

    for path in sorted((FIXTURES / "cases").glob("*.json")):
        bundle = load_json(path)
        for case in bundle.get("cases", []):
            case_id = case["case_id"]
            layers = list(case.get("layers") or ["L0", "L1", "L2", "L3", "L4"])
            l1_findings: list[str] = []
            for role, spec in (case.get("documents") or {}).items():
                document = check_contracts.resolve_document(spec, bases)
                entry = {"id": f"{case_id}/{role}", "role": role}
                entry.update(canonical_entry(document))
                canonical_docs.append(entry)
                if "L1" in layers:
                    l1_findings.extend(
                        f"{role}{message[1:] if message.startswith('$') else message}"
                        for message in schema_findings(role, document, schema, coverage_schema)
                    )
            for role, raw in (case.get("raw_documents") or {}).items():
                entry = {"id": f"{case_id}/{role}", "role": role, "raw": True}
                try:
                    parsed = parse_json_strict(raw, what=role)
                except ParseError as exc:
                    entry["parse_error"] = {"rule_id": exc.rule_id, "message": exc.message}
                    raw_docs.append(entry)
                    continue
                entry.update(canonical_entry(parsed))
                raw_docs.append(entry)
            cases_out.append(
                {
                    "id": case_id,
                    "file": path.name,
                    "layers": layers,
                    "expect": case.get("expect", "accept"),
                    "rule_ids": list(case.get("rule_ids") or []),
                    "l1_findings": l1_findings,
                    "roles": sorted((case.get("documents") or {}).keys()),
                }
            )

    golden = load_json(FIXTURES / "golden" / "canonical-vectors.json")

    OUT_DIR.mkdir(parents=True, exist_ok=True)
    canonical_path = OUT_DIR / "cross-end-canonical.json"
    cases_path = OUT_DIR / "cross-end-cases.json"
    canonical_path.write_text(
        json.dumps(
            {
                "source": "tools/contract-check/fixtures (read-only)",
                "canonical_version": CANONICAL_VERSION,
                "documents": canonical_docs,
                "raw_documents": raw_docs,
            },
            ensure_ascii=False,
            indent=1,
            sort_keys=True,
        )
        + "\n",
        encoding="utf-8",
    )
    cases_path.write_text(
        json.dumps(
            {
                "source": "tools/contract-check/fixtures (read-only)",
                "canonical_version": CANONICAL_VERSION,
                "golden_case_count": len(golden.get("canonical", []))
                + len(golden.get("canonical_reject", []))
                + len(golden.get("decimal", []))
                + len(golden.get("decimal_accept", []))
                + len(golden.get("decimal_reject", []))
                + len(golden.get("action_key", []))
                + len(golden.get("identity", [])),
                "cases": cases_out,
            },
            ensure_ascii=False,
            indent=1,
            sort_keys=True,
        )
        + "\n",
        encoding="utf-8",
    )
    print(f"documents: {len(canonical_docs)} (raw {len(raw_docs)})")
    print(f"cases: {len(cases_out)}")
    print(f"wrote {canonical_path.relative_to(REPO_ROOT)} ({canonical_path.stat().st_size} bytes)")
    print(f"wrote {cases_path.relative_to(REPO_ROOT)} ({cases_path.stat().st_size} bytes)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
