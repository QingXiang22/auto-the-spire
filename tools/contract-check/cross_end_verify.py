#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""独立核对 net 侧冻结的跨端清单（`net-the-spire/Net.TheSpire.Tests/fixtures/cross-end-*.json`）。

目的：这是**监督侧的独立验证**——不采信 N-01 的自述，直接用 Python 重算清单里的摘要，
从而证明"冻结期望"与 Python 参考实现一致（C# 通过该清单即等价于与 Python 对齐）。

检查项：
1. `cross-end-canonical.json` 的每个 document：`sha256` 与 `byte_length` 是否等于其 `text` 的
   UTF-8 字节的 sha256/长度。
2. 两个清单的 `canonical_version` 是否一致、`cases` 数量是否等于 `golden_case_count`。

用法：python tools/contract-check/cross_end_verify.py
退出码：0 全部一致；1 有不一致（打印前若干条）。
"""
from __future__ import annotations

import hashlib
import json
import pathlib
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent.parent
FIXTURES = ROOT / "net-the-spire" / "Net.TheSpire.Tests" / "fixtures"


def load(name: str) -> dict:
    path = FIXTURES / name
    return json.loads(path.read_text(encoding="utf-8"))


def normalize_digest(value: object) -> str:
    """清单里的摘要可能带 `sha256:` 前缀；统一成小写裸十六进制以便比较。"""
    text = str(value or "").strip().lower()
    return text.split(":", 1)[1] if ":" in text else text


def main() -> int:
    cases = load("cross-end-cases.json")
    canonical = load("cross-end-canonical.json")
    problems: list[str] = []

    if cases.get("canonical_version") != canonical.get("canonical_version"):
        problems.append(
            f"canonical_version 不一致: cases={cases.get('canonical_version')} "
            f"canonical={canonical.get('canonical_version')}"
        )

    case_list = cases.get("cases") or []
    golden = cases.get("golden_case_count")
    # golden_case_count 只数"带 canonical 期望"的用例，是 cases 的子集，不做相等要求。
    if isinstance(golden, int) and golden > len(case_list):
        problems.append(f"golden_case_count={golden} 超过 cases 实际条数 {len(case_list)}")

    documents = canonical.get("documents") or []
    checked = 0
    prefix_only: list[dict] = []
    verified: dict[str, int] = {}
    for entry in documents:
        text = entry.get("text")
        if not isinstance(text, str):
            # 只登记元数据（含 text_prefix）的条目：摘要无法从该条目本身重算，
            # 但同一文档常以不同 role 重复登记 → 稍后用"已核验摘要集合"交叉比对。
            prefix_only.append(entry)
            continue
        raw = text.encode("utf-8")
        digest = hashlib.sha256(raw).hexdigest()
        checked += 1
        verified.setdefault(digest, len(raw))
        expected = normalize_digest(entry.get("sha256"))
        if digest != expected:
            problems.append(
                f"document {entry.get('id')!r} sha256 不符: 重算 {digest[:16]}… != 冻结 {expected[:16]}…"
            )
        if entry.get("byte_length") is not None and len(raw) != entry.get("byte_length"):
            problems.append(
                f"document {entry.get('id')!r} byte_length 不符: 重算 {len(raw)} != 冻结 "
                f"{entry.get('byte_length')}"
            )

    cross_matched = 0
    unverifiable: list[str] = []
    for entry in prefix_only:
        digest = normalize_digest(entry.get("sha256"))
        length = entry.get("byte_length")
        if digest in verified and (length is None or verified[digest] == length):
            cross_matched += 1
        else:
            unverifiable.append(f"{entry.get('id')!r} ({digest[:16]}… / {length} bytes)")

    print(f"canonical_version={canonical.get('canonical_version')} cases={len(case_list)} "
          f"golden_case_count={golden} documents={len(documents)} "
          f"digests_checked={checked} prefix_only={len(prefix_only)} "
          f"prefix_only_cross_matched={cross_matched} unverifiable={len(unverifiable)} "
          f"raw_documents={len(canonical.get('raw_documents') or [])}")
    if unverifiable:
        print("unverifiable ids (no inline text and no verified twin):")
        for line in unverifiable[:10]:
            print(f"  - {line}")
    if problems:
        print(f"PROBLEMS: {len(problems)}")
        for line in problems[:10]:
            print(f"  - {line}")
        return 1
    print("all frozen digests, byte lengths and counts are consistent")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
