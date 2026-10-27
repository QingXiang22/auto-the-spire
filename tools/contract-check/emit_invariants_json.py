#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""生成并核对 `docs/specs/net/0.2/invariants.json`（C-01 的"带稳定 ID 的跨结构检查清单"）。

出处与职责：
- `docs/areas/net/spec/contract-artifacts.md:11` 要求在该目录交付 `invariants.json`。
- `tools/contract-check/wire_layers.py:8` 声称"规则 ID 与该文件一一对应"，因此本生成器把
  **代码里实际使用的规则 ID**（`wire_layers.py` 的 `_f(id, layer, …)`；`wire_json.py` 的
  `ParseError(id, …)` 即 L0）与 `invariants.md` 的 ID 表合成该文件，并报告两侧漂移。

用法：
    python tools/contract-check/emit_invariants_json.py            # 写入 invariants.json
    python tools/contract-check/emit_invariants_json.py --check    # 只核对，不写；有漂移则退出码 1
"""
from __future__ import annotations

import argparse
import json
import re
import pathlib
import sys

ROOT = pathlib.Path(__file__).resolve().parent
REPO = ROOT.parent.parent
CONTRACTS = REPO / "docs" / "specs" / "net" / "0.2"
TARGET = CONTRACTS / "invariants.json"
INVARIANTS_MD = CONTRACTS / "invariants.md"

# 代码侧：_f("I-xx", "Ln", ...) 与 ParseError("I-xx", ...)（后者属 L0 文本解析）。
# 注意：调用可能跨行（`_f(\n    "I-87",\n    "L2"`），因此对整个文件文本做匹配并回算行号。
FINDING_CALL = re.compile(r'_f\(\s*"(I-\d+)"\s*,\s*"(L\d)"', re.S)
PARSE_ERROR = re.compile(r'ParseError\(\s*"(I-\d+)"', re.S)
# check_contracts.py 另有自己的助手 `add(rule_id, layer, path, message)`（例如 I-66 由覆盖表结构校验发出），
# 必须一并扫描，否则会把"代码已发码"的 ID 误列为 documented_but_not_emitted。
ADD_CALL = re.compile(r'\badd\(\s*"(I-\d+)"\s*,\s*"(L\d)"', re.S)
ANY_ID = re.compile(r"\bI-\d+\b")


def _line_of(text: str, offset: int) -> int:
    return text.count("\n", 0, offset) + 1


def code_ids() -> dict[str, dict]:
    """{id: {"layers": [...], "sources": ["file:line", ...]}}"""
    found: dict[str, dict] = {}
    for path in sorted(ROOT.glob("*.py")):
        if path.name == pathlib.Path(__file__).name:
            continue
        text = path.read_text(encoding="utf-8")
        pairs = [(m.group(1), m.group(2), m.start()) for m in FINDING_CALL.finditer(text)]
        pairs += [(m.group(1), m.group(2), m.start()) for m in ADD_CALL.finditer(text)]
        pairs += [(m.group(1), "L0", m.start()) for m in PARSE_ERROR.finditer(text)]
        for rule_id, layer, offset in pairs:
            entry = found.setdefault(rule_id, {"layers": [], "sources": []})
            if layer not in entry["layers"]:
                entry["layers"].append(layer)
            entry["sources"].append(f"{path.name}:{_line_of(text, offset)}")
    return found


def doc_ids() -> dict[str, dict]:
    """{id: {"line": int}} —— 文档侧只要求该 ID 在 `invariants.md` 中出现（任意格式）。"""
    found: dict[str, dict] = {}
    text = INVARIANTS_MD.read_text(encoding="utf-8")
    for match in ANY_ID.finditer(text):
        found.setdefault(match.group(0), {"line": _line_of(text, match.start())})
    return found


def build() -> tuple[dict, list[str], list[str], list[str]]:
    code = code_ids()
    doc = doc_ids()
    rules = []
    for rule_id in sorted(code, key=lambda value: int(value.split("-")[1])):
        entry = code[rule_id]
        documented = doc.get(rule_id)
        rules.append({
            "id": rule_id,
            "layers": sorted(entry["layers"]),
            "source": entry["sources"],
            "documented_at": f"invariants.md:{documented['line']}" if documented else None,
        })
    only_code = sorted(set(code) - set(doc), key=lambda v: int(v.split("-")[1]))
    only_doc = sorted(set(doc) - set(code), key=lambda v: int(v.split("-")[1]))
    undocumented = [rule["id"] for rule in rules if rule["documented_at"] is None]
    payload = {
        "protocol_version": "0.2",
        "asset": "cross-structure invariant checklist with stable ids (C-01)",
        "generated_by": "tools/contract-check/emit_invariants_json.py",
        "rule_count": len(rules),
        "notes": [
            "rules[] 只列出**代码实际发码**的规则 ID（wire_layers.py 的 _f、wire_json.py 的 ParseError），"
            "因此本文件与代码是精确一一对应；可用 --check 核对是否同步。",
            "invariants.md 是规范侧的完整不变量清单，其 ID 集合是代码发码集合的超集；"
            "documented_but_not_emitted 列出文档规定但当前代码不发码的 ID（可能由机器 schema/L1 承担，"
            "或尚未实现）——这是待裁定项，不是本生成器的错误。",
            "每个 ID 的出处与所在层见 source（文件:行）与 layers；documented_at 指向 invariants.md 首次提及处。",
        ],
        "documented_but_not_emitted": only_doc,
        "rules": rules,
    }
    return payload, only_code, only_doc, undocumented


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true", help="只核对，不写文件；有漂移时退出码 1")
    args = parser.parse_args()
    payload, only_code, only_doc, undocumented = build()
    text = json.dumps(payload, ensure_ascii=False, indent=2) + "\n"
    print(f"rules with code provenance: {payload['rule_count']}")
    print(f"in code but not in invariants.md: {only_code or 'none'}")
    print(f"in invariants.md but not in code: {only_doc or 'none'}")
    print(f"code rules without a documented summary: {undocumented or 'none'}")
    if args.check:
        current = TARGET.read_text(encoding="utf-8") if TARGET.exists() else ""
        if current != text:
            print(f"OUT OF SYNC with {TARGET.relative_to(REPO)}", file=sys.stderr)
            return 1
        print("in sync")
        return 0
    TARGET.write_text(text, encoding="utf-8")
    print(f"wrote {TARGET.relative_to(REPO)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
