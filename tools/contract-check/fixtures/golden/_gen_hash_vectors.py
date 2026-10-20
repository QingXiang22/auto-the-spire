#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""独立产出 golden 摘要期望值（开发工具，不是交付物）。

设计要点：canonical 文本按 contract-artifacts.md §2 手写在本文件里（不 import 本仓库的
canonical 实现），转成 base64（纯 ASCII）后交给 .NET SHA256 计算，避免 PowerShell 读取
非 ASCII 脚本字面量时用错编码（这正是第一版 _hash-vectors.ps1 出错的原因）。
"""

from __future__ import annotations

import base64
from pathlib import Path

TEXTS = {
    "canonical-v1": '{"a":[true,null,"x"],"b":1}',
    "canonical-v2": '{"s":"a\\"b\\\\c\\nd\\te\\u0001f"}',
    "canonical-v3": '{"z":2,"\u00e9":1}',
    "canonical-v4": '{"a":[],"big":9007199254740991,"n":-12,"o":{}}',
    "canonical-v5": '{"slash":"a/b","text":"\u4e2d\u6587"}',
    "identity-catalog-id": (
        '{"canonical_version":"net-wire-canon-v1","declarations":{"action_types":[],"content_types":[],'
        '"entity_kinds":[{"id":"demo:player","semantic_version":"1"}],"fields":[],"group_types":[],"pile_types":[],'
        '"relation_types":[],"sequence_types":[],"terminal_reasons":[]},"protocol_version":"0.2"}'
    ),
    "identity-request-describe": '{"canonical_version":"net-wire-canon-v1","operation":"describe","payload":{}}',
    "identity-ruleset": (
        '{"canonical_version":"net-wire-canon-v1","engine_versions":["core-1.0"],"ir_version":"ir-1",'
        '"numeric_config":{"rounding":"per-hook"},"package_digests":["sha256:aa"],"package_order":["base-spire"],'
        '"prng_profile":"prng-1","rule_config":{"difficulty":"normal"}}'
    ),
    "identity-backend-profile": (
        '{"action_mapping_version":"1","backend_kind":"simulation","canonical_version":"net-wire-canon-v1",'
        '"field_availability":{"demo:hp":"available"},"projection_semantics_version":"1",'
        '"public_identity_version":"1","supported_mechanism_ids":["demo:check_hp_visible"]}'
    ),
    "identity-snapshot-storage": '{"a":"x","z":[1,{"a":2,"b":null}]}',
}

EXPECTED_BYTES = {
    "canonical-v1": 27,
    "canonical-v2": 28,
    "canonical-v3": 14,
    "canonical-v4": 46,
    "canonical-v5": 31,
    "identity-catalog-id": 288,
    "identity-request-describe": 77,
    "identity-ruleset": 258,
    "identity-backend-profile": 260,
    "identity-snapshot-storage": 34,
}


def main() -> int:
    lines = [
        "# 由 _gen_hash_vectors.py 生成；内容为 base64（纯 ASCII），避免非 ASCII 字面量被错误解码。",
        "# 摘要由 .NET SHA256 计算，与 tools/contract-check 的 Python 实现无关。",
        "function Get-ShaFromB64([string]$b64) {",
        "    $bytes = [System.Convert]::FromBase64String($b64)",
        "    $sha = [System.Security.Cryptography.SHA256]::Create()",
        "    $hex = ($sha.ComputeHash($bytes) | ForEach-Object { $_.ToString('x2') }) -join ''",
        '    return "sha256:$hex"',
        "}",
        "$vectors = [ordered]@{}",
    ]
    for key, text in TEXTS.items():
        payload = text.encode("utf-8")
        assert len(payload) == EXPECTED_BYTES[key], (key, len(payload), EXPECTED_BYTES[key])
        lines.append(f"$vectors['{key}'] = '{base64.b64encode(payload).decode('ascii')}'")
    lines.append("foreach ($key in $vectors.Keys) { Write-Output ('{0}`t{1}' -f $key, (Get-ShaFromB64 $vectors[$key])) }")
    out = Path(__file__).with_name("_hash-vectors-b64.ps1")
    out.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(out)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
