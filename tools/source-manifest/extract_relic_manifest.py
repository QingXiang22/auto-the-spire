#!/usr/bin/env python3
"""Read-only relic inventory. Source syntax is navigation evidence, never behavior verification."""
from __future__ import annotations

import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path
import re

from extract_source_manifest import block_after, strip_comments

WORKSPACE = Path(__file__).resolve().parents[2]
METHOD = re.compile(r"\b(?:public|protected)\s+override\s+(?:async\s+)?[^\n{;=]*?\b(\w+)\s*\(")
PROPERTY = re.compile(r"\b(?:public|protected)\s+override\s+[^\n{;=]*?\b(\w+)\s*(?:=>|\{)")


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def clean_source(path: Path) -> str:
    # Preserve source line locators when a block comment spans several lines.
    text = re.sub(r"/\*.*?\*/", lambda match: "\n" * match[0].count("\n"),
                  path.read_text(encoding="utf-8-sig"), flags=re.S)
    return strip_comments(text)


def manifest(source: Path, catalog_path: Path | None) -> dict:
    models = source / "src/Core/Models"
    relic_files = sorted((models / "Relics").glob("*.cs"))
    pool_files = sorted((models / "RelicPools").glob("*.cs"))
    if not relic_files or not pool_files:
        raise ValueError("missing relic classes or pool sources")
    pools = {}
    for path in pool_files:
        text = clean_source(path)
        body = block_after(text, "GenerateAllRelics()")
        names = re.findall(r"ModelDb\.Relic<(\w+)>\(\)", body)
        declared = re.search(r"new RelicModel\[(\d+)\]", body)
        if not names or len(names) != len(set(names)) or (declared and int(declared[1]) != len(names)):
            raise ValueError(f"unsupported or inconsistent pool: {path}")
        pools[path.stem] = names
    catalog = None
    implemented = {}
    if catalog_path:
        catalog = json.loads(catalog_path.read_text(encoding="utf-8-sig"))
        if catalog.get("scope") != "formal-relic-declarations":
            raise ValueError("catalog scope must be formal-relic-declarations")
        for row in catalog["definitions"]:
            if row["Name"] in implemented:
                raise ValueError(f"duplicate base declaration: {row['Name']}")
            implemented[row["Name"]] = row
    rows, method_counts, rarity_counts = [], Counter(), Counter()
    non_relic_files = []
    for path in relic_files:
        text = clean_source(path)
        name = path.stem
        if not re.search(rf"\bclass\s+{re.escape(name)}\s*:\s*RelicModel\b", text):
            non_relic_files.append(path.relative_to(source).as_posix())
            continue
        rarity = re.search(r"RelicRarity Rarity\s*=>\s*RelicRarity\.(\w+)\s*;", text)
        if not rarity:
            raise ValueError(f"unsupported rarity declaration: {path}")
        methods = [{"name": match[1], "line": text.count("\n", 0, match.start()) + 1}
                   for match in METHOD.finditer(text)]
        properties = [{"name": match[1], "line": text.count("\n", 0, match.start()) + 1}
                      for match in PROPERTY.finditer(text)]
        method_counts.update({m["name"] for m in methods})
        rarity_counts[rarity[1]] += 1
        saved = [{"line": text.count("\n", 0, match.start()) + 1, "name": match[1]}
                 for match in re.finditer(r"\[SavedProperty(?:\([^\]]*\))?\]\s*public\s+[\w<>?]+\s+(\w+)", text)]
        commands = sorted(set(".".join(match) for match in re.findall(r"\b(\w+Cmd)\.(\w+)\s*(?:<[^>]+>)?\s*\(", text)))
        row = {"name": name, "source": path.relative_to(source).as_posix(), "sha256": sha256(path),
               "rarity": rarity[1], "pools": [pool for pool, names in pools.items() if name in names],
               "overridden_methods": methods, "overridden_properties": properties, "saved_properties": saved,
               "command_navigation": commands,
               "eligibility": "requires-source-and-reachability-review",
               "eligibility_navigation": [item for item in methods + properties
                                          if item["name"].startswith("IsAllowed")],
               "base_declaration": implemented.get(name),
               "behavior_verified": None, "independent_verified": None, "public_verified": None}
        rows.append(row)
    source_names = {row["name"] for row in rows}
    pooled_names = set().union(*(set(names) for names in pools.values()))
    missing = (pooled_names | set(implemented)) - source_names
    if missing:
        raise ValueError(f"pool/base names missing from source: {sorted(missing)}")
    return {
        "schema": "base-spire/relic-source-inventory@1",
        "scope": "static-source-inventory; no runtime eligibility or behavior claim",
        "identity": {"source_root": str(source.resolve()),
                     "source_digests": {p.relative_to(source).as_posix(): sha256(p) for p in relic_files + pool_files + [models / "RelicModel.cs"]},
                     "base_catalog_sha256": sha256(catalog_path) if catalog_path else None,
                     "extractor_sha256": sha256(Path(__file__))},
        "summary": {"source_files": len(relic_files), "source_classes": len(rows), "non_relic_files": non_relic_files,
                    "pool_unique": len(pooled_names),
                    "pool_memberships": sum(len(names) for names in pools.values()),
                    "multiple_pools": {row["name"]: row["pools"] for row in rows if len(row["pools"]) > 1},
                    "unpooled": sorted(source_names - pooled_names), "pools": {p: len(n) for p, n in pools.items()},
                    "rarities": dict(sorted(rarity_counts.items())),
                    "base_declared": len(implemented) if catalog is not None else None,
                    "base_obtain_supported": sum(row["CanObtain"] for row in implemented.values()) if catalog is not None else None,
                    "base_undeclared": len(rows) - len(implemented) if catalog is not None else None,
                    "classes_with_saved_properties": sum(bool(row["saved_properties"]) for row in rows),
                    "classes_overriding_show_counter": sum(any(p["name"] == "ShowCounter" for p in row["overridden_properties"]) for row in rows),
                    "classes_with_eligibility_overrides": sum(bool(row["eligibility_navigation"]) for row in rows),
                    "override_methods_by_class_count": dict(sorted(method_counts.items(), key=lambda item: (-item[1], item[0])))},
        "pools": pools, "relics": rows,
    }


def markdown(data: dict) -> str:
    s = data["summary"]
    lines = ["# 遗物源码静态清单", "", "统计覆盖本地完整源目录；方法、属性与命令引用只作导航，不能推导单人可达性、效果完整度或运行验证。",
             "base声明来自同次构建的运行工具输出；没有把声明数当作实际注册或验收数。完整身份与逐文件摘要见JSON。", "",
             f"源文件 {s['source_files']}；直接遗物类 {s['source_classes']}；池成员记录 {s['pool_memberships']}，去重 {s['pool_unique']}；池外：{', '.join(s['unpooled']) or '无'}；base声明 {s['base_declared']}；可获得声明 {s['base_obtain_supported']}。",
             f"非直接遗物类文件：{', '.join(s['non_relic_files']) or '无'}。池引用或base声明未能匹配遗物类时工具直接失败。",
             "", "| 源池 | 声明数 |", "| --- | ---: |"]
    lines += [f"| {pool} | {count} |" for pool, count in s["pools"].items()]
    lines += ["", "跨池重复身份：" + "; ".join(f"{name}: {', '.join(pools)}" for name, pools in s["multiple_pools"].items()) + "。",
              "Deprecated、回退、事件及各角色保留原池；尚未逐件裁定单人资格，不从分母删除。", "",
              "| 覆写方法（每类计一次） | 类数 |", "| --- | ---: |"]
    lines += [f"| {name} | {count} |" for name, count in s["override_methods_by_class_count"].items()]
    lines += ["", "| 遗物 | 稀有度 | 池归属 | base声明 | 覆写方法 | 源码 |", "| --- | --- | --- | --- | --- | --- |"]
    for row in data["relics"]:
        methods = ", ".join(f"{m['name']}:{m['line']}" for m in row["overridden_methods"])
        lines.append(f"| {row['name']} | {row['rarity']} | {', '.join(row['pools']) or '池外'} | {'有' if row['base_declaration'] else '未登记' if s['base_declared'] is not None else '未知'} | {methods or '无直接覆写'} | {row['source']} |")
    return "\n".join(lines) + "\n"


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-root", type=Path, default=WORKSPACE / "sts2")
    parser.add_argument("--base-catalog", type=Path)
    parser.add_argument("--out", type=Path, required=True)
    parser.add_argument("--markdown", type=Path, required=True)
    args = parser.parse_args()
    data = manifest(args.source_root, args.base_catalog)
    for path in (args.out, args.markdown):
        path.parent.mkdir(parents=True, exist_ok=True)
    args.out.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    args.markdown.write_text(markdown(data), encoding="utf-8")
    print(json.dumps(data["summary"], ensure_ascii=False))


if __name__ == "__main__":
    main()
