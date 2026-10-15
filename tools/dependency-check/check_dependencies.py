#!/usr/bin/env python3
"""K-09a 第一步：**画出项目依赖**并把"新通用路径不引用 Legacy"从叙述变成检查。

做四件事：

1. 从所有 `.csproj` 建**项目依赖图**（`ProjectReference`），并对图做检查：
   无环；`core-the-spire` 不依赖 `base-spire`（否则就是 base↔core 环）。
2. 从 `Core.TheSpire/Legacy/**` 收集**声明的类型名**，再扫 `Core.TheSpire/Engine/**`
   与 `mod-the-spire/base-spire/BaseSpire/Slice/**`：新通用路径**不得**出现这些类型名
   （"Engine 不依赖 STS"、"切片不引用旧后端"的可机械核对版本）。
3. 反查：`Engine/**` 与 `Slice/**` 不得出现 `Legacy` 目录里的**文件名**引用（`using`/类型限定）。
4. 输出依赖图与结论（`--json <path>` 可落盘机器产物）。

用法：
    python tools/dependency-check/check_dependencies.py [--root .] [--json out.json]

退出码：0 = 全部通过；1 = 有违规；2 = 用法错误。
"""

from __future__ import annotations

import argparse
import json
import pathlib
import re
import sys
import xml.etree.ElementTree as ElementTree

# 新通用路径：这些目录里的代码不得引用旧后端（Legacy）的任何类型名。
NEW_PATH_GLOBS = (
    "core-the-spire/Core.TheSpire/Engine/**/*.cs",
    "mod-the-spire/base-spire/BaseSpire/Slice/**/*.cs",
    # 新的通用消费方（生产模块）：它们同样不应依赖 STS 旧后端。
    "net-the-spire/Net.TheSpire/**/*.cs",
    "net-the-spire/Net.TheSpire.Contracts/**/*.cs",
    "ai-the-spire/ai_the_spire/**/*.cs",
)

LEGACY_GLOB = "archive/legacy-sts/core-the-spire/Core.TheSpire.Legacy/Legacy/**/*.cs"

# 类型声明：class/record/struct/interface/enum（含 file-scoped 与常见修饰符）。
TYPE_DECLARATION = re.compile(
    r"^\s*(?:public|internal|private|protected)?\s*"
    r"(?:static\s+|sealed\s+|abstract\s+|partial\s+|readonly\s+|unsafe\s+)*"
    r"(class|record|struct|interface|enum)\s+"
    r"(?:struct\s+|class\s+)?"          # `record struct X` / `record class X`
    r"([A-Za-z_][A-Za-z0-9_]*)",
    re.MULTILINE,
)

# `record struct X` 里紧跟 kind 的 `struct` 会被当成名字，这里把语言关键字滤掉。
CSHARP_KEYWORDS = {
    "struct", "class", "record", "interface", "enum", "where", "readonly", "partial",
    "static", "sealed", "abstract", "public", "internal", "private", "protected", "unsafe",
}


class Checker:
    def __init__(self) -> None:
        self.passed = 0
        self.failed = 0
        self.details: list[str] = []

    def check(self, case: str, ok: bool, detail: str = "") -> bool:
        if ok:
            self.passed += 1
        else:
            self.failed += 1
            self.details.append(f"  - {case}: {detail}")
        return ok


def project_references(csproj: pathlib.Path) -> list[str]:
    """该 csproj 的 ProjectReference（规范化成仓库相对路径的 stem）。"""
    try:
        tree = ElementTree.parse(csproj)
    except ElementTree.ParseError:
        return []

    references: list[str] = []
    for node in tree.iter():
        if node.tag.endswith("ProjectReference"):
            include = node.attrib.get("Include", "")
            if include:
                resolved = (csproj.parent / include.replace("\\", "/")).resolve()
                references.append(resolved.stem)
    return references


def scan_project_graph(root: pathlib.Path) -> dict[str, list[str]]:
    projects = sorted(
        path
        for path in root.rglob("*.csproj")
        if not any(part in {"obj", "bin", "node_modules", "archive", ".audit"} for part in path.parts)
    )
    return {path.stem: project_references(path) for path in projects}


def find_cycles(graph: dict[str, list[str]]) -> list[list[str]]:
    """返回所有（去重后的）环，形如 [a, b, a]。"""
    cycles: list[list[str]] = []
    seen: set[tuple[str, ...]] = set()

    def walk(node: str, path: list[str]) -> None:
        for neighbour in graph.get(node, []):
            if neighbour in path:
                cycle = path[path.index(neighbour):] + [neighbour]
                key = tuple(sorted(set(cycle)))
                if key not in seen:
                    seen.add(key)
                    cycles.append(cycle)
                continue
            walk(neighbour, path + [neighbour])

    for project in graph:
        walk(project, [project])
    return cycles


# 新通用路径里声明的类型名（在 main 里填充）：同名于 Legacy 的类型会被单独报成 shadowing 风险。
NEW_PATH_TYPES: set[str] = set()


def new_path_type_names(root: pathlib.Path) -> set[str]:
    declared: set[str] = set()
    for pattern in NEW_PATH_GLOBS:
        for path in sorted(root.glob(pattern)):
            text = strip_comments_and_strings(path.read_text(encoding="utf-8", errors="replace"))
            declared.update(name for _, name in TYPE_DECLARATION.findall(text)
                           if name not in CSHARP_KEYWORDS)
    return declared


def legacy_type_names(root: pathlib.Path) -> dict[str, str]:
    """Legacy 目录里声明的类型名 → 声明文件（仓库相对路径）。"""
    names: dict[str, str] = {}
    for path in sorted(root.glob(LEGACY_GLOB)):
        text = path.read_text(encoding="utf-8", errors="replace")
        for kind, name in TYPE_DECLARATION.findall(text):
            if name in CSHARP_KEYWORDS:
                continue
            names.setdefault(name, str(path.relative_to(root)).replace("\\", "/"))
    return names


def strip_comments_and_strings(text: str) -> str:
    """去掉注释与字符串字面量：只保留**代码**里出现的名字，避免注释里的提及被当成依赖。"""
    text = re.sub(r"/\*.*?\*/", " ", text, flags=re.DOTALL)
    text = re.sub(r"//[^\n]*", " ", text)
    text = re.sub(r'"(?:[^"\\]|\\.)*"', '""', text)
    return text


# 值/成员名可能**同名**于 Legacy 类型（例如切片自己的 `SliceCard(string ContentId, …)`）。
# 因此只在**类型使用位置**才算依赖：`new X(`、`X.`、基类列表、`X ident`、`X?`/`X[]`/`List<X>`。
def usage_patterns(name: str) -> list[re.Pattern[str]]:
    escaped = re.escape(name)
    return [
        re.compile(rf"\bnew\s+{escaped}\s*[<(]"),                 # new X(...)
        re.compile(rf"(?<![\w.]){escaped}\s*\."),                  # X.Static
        re.compile(rf":\s*{escaped}\b"),                           # : X（基类/接口）
        # X ident（类型位置）：要求跟随的标识符**像声明一样收尾**（`, ) ; = < { }`），
        # 否则会把 `ContentId is not null` 这类"同名成员 + 关键字"误判成类型用法。
        re.compile(r"\b" + escaped + r"\s+[A-Za-z_]\w*\s*[,);=<{}]"),
        re.compile(rf"\b{escaped}\s*\?"),                           # X?（可空）
        re.compile(rf"\b{escaped}\s*\[\s*\]"),                      # X[]（数组，必须成对）
        re.compile(rf"<\s*{escaped}\s*[>,]"),                       # List<X>
        re.compile(r"\(\s*" + escaped + r"\s+[A-Za-z_]\w*\s*[,)]"),       # (X ident
    ]


def references_to(text: str, names: dict[str, str]) -> list[str]:
    """
    代码里以**类型使用位置**出现的 Legacy 类型名；排除本文件自己声明的同名类型与
    仅作为成员/参数名出现的同名标识符（值名不等于类型依赖）。
    """
    code = strip_comments_and_strings(text)
    local = {name for _, name in TYPE_DECLARATION.findall(code) if name not in CSHARP_KEYWORDS}
    hits: list[str] = []
    for name in names:
        if name in local:
            continue
        # 新路径自己声明的同名类型不是 Legacy 依赖；但"两处同名"是 shadowing 风险，
        # 由 new_path_type_names 的调用方单独报出，不在这里静默放过。
        if name in NEW_PATH_TYPES:
            continue
        if any(pattern.search(code) for pattern in usage_patterns(name)):
            hits.append(name)
    return sorted(hits)


def self_test() -> list[str]:
    """检查器自身的自检：真的抓得到 Legacy 用法，且不误报同名成员。"""
    failures: list[str] = []
    names = {"ContentId": "Legacy/Content.cs", "ScaledCombatAmount": "Legacy/Content.cs"}

    real = [
        "var id = new ContentId(\"x\");",
        "ContentId.Parse(\"x\");",
        "public sealed record Wrapper(ContentId Id);",
        "List<ScaledCombatAmount> values = [];",
        "ScaledCombatAmount? maybe = null;",
        "public void Use(ScaledCombatAmount amount) { }",
    ]
    for snippet in real:
        if not references_to(snippet, names):
            failures.append(f"漏报：{snippet}")

    benign = [
        "public sealed record SliceCard(string ContentId, int Cost);",
        "var x = card.ContentId;",
        "var contentId = card.ContentId;",
        "// 说明：旧后端的 ContentId 不在新路径使用",
        "string contentId = \"ContentId\";",
        # 以下三类都是本轮真实修掉的误报，必须留在自检里防回归：
        "public const string CardType = \"base-spire:Card\";",        # 同名字段
        "[CardType] = new PublicKindRule(",                            # 同名字段当字典键
        "if (ContentId is not null) members.Add(item);",               # 同名成员 + 关键字
        "var known = KnownCardType;",                                  # 更长名字里包含同一子串
    ]
    for snippet in benign:
        hits = references_to(snippet, names)
        if hits:
            failures.append(f"误报：{snippet} -> {hits}")

    return failures


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(description="项目依赖图与旧后端引用检查（K-09a）")
    parser.add_argument("--root", default=".")
    parser.add_argument("--json", default=None, help="把依赖图与结论写成 JSON")
    args = parser.parse_args(argv)

    root = pathlib.Path(args.root).resolve()
    if not (root / "AutoTheSpire.sln").exists():
        print(f"不是仓库根目录（找不到 AutoTheSpire.sln）: {root}", file=sys.stderr)
        return 2

    checker = Checker()
    graph = scan_project_graph(root)

    # -- 0. 检查器自检（抓得到真用法、不误报同名成员） ----------------------------------------------------
    self_failures = self_test()
    checker.check("checker/self-test", not self_failures, f"{self_failures}")

    # -- 1. 项目依赖图 -------------------------------------------------------------------------------------
    checker.check("graph/non-empty", len(graph) > 0, f"{len(graph)} 个项目")
    cycles = find_cycles(graph)
    checker.check("graph/acyclic", not cycles, f"发现环：{cycles}")

    archive_references = []
    for path in root.rglob("*.csproj"):
        if any(part in {"obj", "bin", "node_modules", "archive", ".audit"} for part in path.parts):
            continue
        for node in ElementTree.parse(path).iter():
            if node.tag.endswith("ProjectReference"):
                target = (path.parent / node.attrib.get("Include", "").replace("\\", "/")).resolve()
                if (root / "archive").resolve() in target.parents:
                    archive_references.append(str(path.relative_to(root)))
    checker.check("graph/active-does-not-reference-archive", not archive_references,
                  f"活动工程引用归档：{archive_references}")

    # core 不得依赖 base-spire（否则与 base-spire → core 形成环）。
    core = graph.get("Core.TheSpire", [])
    checker.check("graph/core-does-not-reference-base-spire",
                  "BaseSpire" not in core, f"Core.TheSpire → {core}")
    base = graph.get("BaseSpire", [])
    checker.check("graph/base-spire-references-core", "Core.TheSpire" in base, f"BaseSpire → {base}")

    # -- 2. 新通用路径不得引用 Legacy 类型 ----------------------------------------------------------------
    global NEW_PATH_TYPES
    NEW_PATH_TYPES = new_path_type_names(root)
    legacy = legacy_type_names(root)

    # 同名遮蔽：新路径与 Legacy 都声明同一个类型名——不一定是依赖，但一定是**歧义风险**，显式报出来。
    shadowed = sorted(NEW_PATH_TYPES & set(legacy))
    checker.check("new-path/no-shadowed-legacy-type-names", not shadowed,
                  f"新路径与 Legacy 同名（建议改名）：{shadowed}")
    checker.check("legacy/types-collected", len(legacy) > 50, f"收集到 {len(legacy)} 个 Legacy 类型名")

    violations: dict[str, dict[str, list[str]]] = {}
    scanned = 0
    for pattern in NEW_PATH_GLOBS:
        for path in sorted(root.glob(pattern)):
            scanned += 1
            text = path.read_text(encoding="utf-8", errors="replace")
            hits = references_to(text, legacy)
            if hits:
                relative = str(path.relative_to(root)).replace("\\", "/")
                violations[relative] = {name: legacy[name] for name in hits}

    checker.check("engine-and-slice/scan-non-empty", scanned > 0, f"扫描 {scanned} 个文件")
    checker.check("engine-and-slice/no-legacy-types", not violations,
                  f"{len(violations)} 个文件引用了 Legacy 类型：{sorted(violations)}")

    # -- 3. 机器产物 --------------------------------------------------------------------------------------
    report = {
        "check_version": "k09a-dependency-map@1",
        "projects": {name: sorted(refs) for name, refs in sorted(graph.items())},
        "cycles": cycles,
        "legacy_type_count": len(legacy),
        "new_path_types": len(NEW_PATH_TYPES),
        "shadowed_type_names": shadowed,
        "new_path_files_scanned": scanned,
        "legacy_reference_violations": violations,
        "checks": {"passed": checker.passed, "failed": checker.failed},
    }
    if args.json:
        pathlib.Path(args.json).write_text(
            json.dumps(report, ensure_ascii=False, indent=2, sort_keys=True), encoding="utf-8")
        print(f"dependency map written: {args.json}")

    print(f"projects: {len(graph)}，cycles: {len(cycles)}，Legacy 类型: {len(legacy)}，"
          f"新路径文件: {scanned}，违规: {len(violations)}")
    for detail in checker.details:
        print(detail, file=sys.stderr)
    print(f"TOTAL: {checker.passed} passed, {checker.failed} failed, "
          f"{checker.passed + checker.failed} executed")
    return 1 if checker.failed else 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
