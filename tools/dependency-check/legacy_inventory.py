#!/usr/bin/env python3
"""K-09a 第二步：把旧后端（`Core.TheSpire/Legacy/**`）的领域面**逐文件清点**，形成可分批迁移的清单。

为什么需要它：K-09a 的判据是"core 最终无 STS 依赖 / 专用公共契约"、"先迁切片需要的职责，再分批抽离其余"。
在动手搬文件之前，必须先回答三个可核对的问题：

1. 旧后端**有哪些文件、各自声明了什么类型、多少行**（不能只凭印象说"旧后端很大"）；
2. 每个文件里的类型**被谁引用**（引用计数按区域分组：Engine / 旧后端自身 / base-spire / 测试 / tools / net / ai）
   —— 这是"迁移影响面"的机械证据；
3. 每个文件按规则**分到哪一类领域职责**（STS 内容模式 / STS 作者前端 / STS 战斗流程 / STS run 流程 /
   疑似通用 / **未分类**）。分类必须显式，未分类项逐条列出交给规划者，不允许静默归类。

用法：
    python tools/dependency-check/legacy_inventory.py [--root .] [--json out.json]

退出码：0 = 生成成功（**不代表迁移完成**）；2 = 用法/路径错误。
"""

from __future__ import annotations

import argparse
import json
import pathlib
import re
import sys
from collections import Counter, defaultdict

LEGACY_GLOB = "archive/legacy-sts/core-the-spire/Core.TheSpire.Legacy/Legacy/**/*.cs"
SCAN_GLOBS = (
    "core-the-spire/**/*.cs",
    "mod-the-spire/**/*.cs",
    "net-the-spire/**/*.cs",
    "ai-the-spire/**/*.cs",
    "tools/**/*.cs",
)
EXCLUDED_PARTS = {"obj", "bin", "node_modules"}

TYPE_DECLARATION = re.compile(
    r"^\s*(?:public|internal|private|protected)?\s*"
    r"(?:static\s+|sealed\s+|abstract\s+|partial\s+|readonly\s+|unsafe\s+)*"
    r"(class|record|struct|interface|enum)\s+(?:struct\s+|class\s+)?"
    r"([A-Za-z_][A-Za-z0-9_]*)",
    re.MULTILINE,
)

# 分类规则：文件名/类型名里的模式 → 领域类别。顺序即优先级（先匹配先归类）。
CLASSIFICATION_RULES: list[tuple[str, str, str]] = [
    # (类别, 说明, 正则——匹配文件相对路径或该文件声明的类型名)
    ("sts-content-schema", "STS 内容模式（Card/Power/Enemy/Potion/Relic/ContentId…）",
     r"(Content\.cs$|ContentId|CardDefinition|PowerDefinition|EnemyDefinition|PotionDefinition|"
     r"CardCostModifier|CardCostOperation|EncounterEnemySlot|RelicDefinition|CardPoolDefinition|"
     r"CharacterDefinition|CardAfflictionDefinition|CardInstance|CombatCardVariables)"),
    ("sts-dsl", "STS 作者前端（DSL 源模型/解析/编译/效果 DSL）",
     r"(DslParser|DslCompiler|DslSource|ContentDsl|EffectDsl|EffectCountScope|Spire.*Compiler\.cs$)"),
    ("sts-combat", "STS 战斗流程/结算（State/Effects/Environment/Hooks/执行上下文）",
     r"(State\.cs$|Effects\.cs$|Environment\.cs$|Hooks?\.cs$|Combat|EffectExecutionContext|RunEffectExecutionContext)"),
    ("sts-run", "STS run 流程（地图/房间/奖励/事件）",
     r"(Runs?\.cs$|Map|Act|Room|Reward|Event)"),
    ("generic-candidate", "疑似通用（随机/数值/集合等，需人工确认才能判为通用）",
     r"(Random|Numeric|FixedPoint|Collection|Serial|ScopedIntState)"),
]


def declared_types(text: str) -> list[str]:
    return [name for _, name in TYPE_DECLARATION.findall(text)]


def area_of(relative: str) -> str:
    if relative.startswith("core-the-spire/Core.TheSpire/Engine/"):
        return "engine"
    if relative.startswith("archive/legacy-sts/core-the-spire/Core.TheSpire.Legacy/Legacy/"):
        return "legacy"
    if relative.startswith("core-the-spire/"):
        return "core-other"
    if relative.startswith("mod-the-spire/base-spire/BaseSpire/Slice/"):
        return "slice"
    if relative.startswith("mod-the-spire/"):
        return "base-spire"
    if relative.startswith("net-the-spire/"):
        return "net"
    if relative.startswith("ai-the-spire/"):
        return "ai"
    if relative.startswith("tools/"):
        return "tools"
    return "other"


def classify(relative: str, types: list[str]) -> tuple[str, str]:
    haystack = relative + " " + " ".join(types)
    for category, description, pattern in CLASSIFICATION_RULES:
        if re.search(pattern, haystack):
            return category, description
    return "unclassified", "未分类：需要规划者/实现方显式归类"


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(description="旧后端领域面清点（K-09a 第二步）")
    parser.add_argument("--root", default=".")
    parser.add_argument("--json", default=None)
    args = parser.parse_args(argv)

    root = pathlib.Path(args.root).resolve()
    if not (root / "AutoTheSpire.sln").exists():
        print(f"不是仓库根目录：{root}", file=sys.stderr)
        return 2

    legacy_files = sorted(path for path in root.glob(LEGACY_GLOB)
                          if not any(part in EXCLUDED_PARTS for part in path.parts))
    if not legacy_files:
        print("没有找到 Legacy 源文件", file=sys.stderr)
        return 2

    # 逐个旧后端文件：行数、类型、分类
    entries: list[dict] = []
    type_to_file: dict[str, str] = {}
    for path in legacy_files:
        text = path.read_text(encoding="utf-8", errors="replace")
        relative = str(path.relative_to(root)).replace("\\", "/")
        types = declared_types(text)
        category, description = classify(relative, types)
        entries.append({
            "path": relative,
            "lines": text.count("\n") + 1,
            "types": types,
            "category": category,
            "category_description": description,
        })
        for name in types:
            type_to_file.setdefault(name, relative)

    # 反向引用：这些类型在**别处**被引用多少次，按区域分组。
    references: dict[str, Counter[str]] = defaultdict(Counter)
    scanned = 0
    for pattern in SCAN_GLOBS:
        for path in sorted(root.glob(pattern)):
            if any(part in EXCLUDED_PARTS for part in path.parts):
                continue
            scanned += 1
            relative = str(path.relative_to(root)).replace("\\", "/")
            area = area_of(relative)
            text = path.read_text(encoding="utf-8", errors="replace")
            for name in type_to_file:
                if type_to_file[name] == relative:
                    continue  # 声明它的那个文件不算"外部引用"
                if re.search(rf"\b{re.escape(name)}\b", text):
                    references[type_to_file[name]][area] += 1

    for entry in entries:
        counts = references.get(entry["path"], Counter())
        entry["referenced_by"] = dict(sorted(counts.items()))
        entry["external_reference_files"] = sum(counts.values())

    categories = Counter(entry["category"] for entry in entries)
    report = {
        "inventory_version": "k09a-legacy-inventory@1",
        "root": str(root.name),
        "legacy_files": len(entries),
        "legacy_lines": sum(entry["lines"] for entry in entries),
        "declared_types": len(type_to_file),
        "scanned_files": scanned,
        "category_counts": dict(sorted(categories.items())),
        "entries": sorted(entries, key=lambda entry: (-entry["external_reference_files"], entry["path"])),
        "unclassified": sorted(entry["path"] for entry in entries if entry["category"] == "unclassified"),
        "note": "本清单只做清点与分类，不代表任何迁移已完成；未分类项必须逐条显式处理。",
    }

    if args.json:
        pathlib.Path(args.json).write_text(
            json.dumps(report, ensure_ascii=False, indent=2, sort_keys=True), encoding="utf-8")
        print(f"legacy inventory written: {args.json}")

    print(f"旧后端文件: {len(entries)}，行数: {report['legacy_lines']}，声明类型: {len(type_to_file)}，"
          f"扫描文件: {scanned}")
    print("分类计数: " + ", ".join(f"{name}={count}" for name, count in sorted(categories.items())))
    print("被引用最多的 8 个文件：")
    for entry in report["entries"][:8]:
        by = ",".join(f"{area}:{count}" for area, count in entry["referenced_by"].items())
        print(f"  {entry['external_reference_files']:4d}  {entry['category']:20s} {entry['path']}  [{by}]")
    if report["unclassified"]:
        print(f"未分类（必须显式归类，不能静默留在清单里）: {len(report['unclassified'])} 个", file=sys.stderr)
        for path in report["unclassified"]:
            print(f"  - {path}", file=sys.stderr)
        print("TOTAL: 分类不完整 -> 退出码 1")
        return 1

    print("分类完整：无未分类文件")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
