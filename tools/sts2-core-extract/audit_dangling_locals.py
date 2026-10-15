"""Find code that no longer hangs together after the extraction: dangling local variables.

The audit in `audit_deletions.py` asks "was every removed line presentation?" — useful, but its
verdict on a removed line is a judgement call, so it produces noise.

This check is sharper and needs no judgement. A local variable that the extraction deleted is
fine *if nothing refers to it any more* (the whole member that owned it went away). It is a
defect if the extracted file still names it, because that statement now references nothing.

So: for each file, take the locals declared in the `sts2/src` original, subtract the locals
declared in the extracted file, and report every subtracted name that the extracted file still
mentions. That is exactly the collateral damage a whole-file taint scope caused — a convenience
name like `result` tainted in one method used to delete every other method's `result`.

Run from the repository root; exits non-zero if any dangling local is found.
"""

from __future__ import annotations

import argparse
import os
import re
import sys
from collections import defaultdict

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
OUT = os.path.join(REPO, "sts2-core")
SRC = os.path.join(REPO, "sts2", "src")

# `<Type> name =` / `<Type> name;` / `var name =` / `out <Type> name` / `(<T> a, <T> b) =`
DECL_RE = re.compile(
    r"(?:^|[;{}(,\s])(?:[A-Za-z_][\w\.<>?\[\],\s]*?)\s+([a-z_]\w*)\s*(?:=|;|\)|,)"
)
VAR_RE = re.compile(r"\bvar\s+([a-z_]\w*)\s*=")
OUT_RE = re.compile(r"\bout\s+(?:[A-Za-z_][\w\.<>?\[\],]*\s+)?([a-z_]\w*)\b")
FOREACH_RE = re.compile(r"\bforeach\s*\(\s*[^)]*?\s+([a-z_]\w*)\s+in\b")
LAMBDA_ARG_RE = re.compile(r"\(([^()]*)\)\s*=>")

# Names that are not locals we should chase.
IGNORE = {
    "get", "set", "if", "else", "for", "foreach", "while", "return", "new", "using", "var",
    "case", "switch", "break", "continue", "throw", "await", "async", "public", "private",
    "protected", "internal", "static", "readonly", "const", "this", "base", "null", "true",
    "false", "value", "type", "int", "float", "bool", "string", "decimal", "double", "char",
    "object", "Task", "in", "out", "ref", "params", "is", "as", "default", "delegate", "where",
    "void", "class", "struct", "enum", "interface", "record", "namespace", "try", "catch",
    "finally", "lock", "do", "yield", "checked", "unchecked", "unsafe", "fixed", "goto",
}


def strip_code(text: str) -> str:
    """Remove comments and string/char literals so names inside them are ignored."""
    out = []
    i, n = 0, len(text)
    while i < n:
        ch = text[i]
        if ch == "/" and i + 1 < n and text[i + 1] == "/":
            while i < n and text[i] != "\n":
                i += 1
        elif ch == "/" and i + 1 < n and text[i + 1] == "*":
            i += 2
            while i + 1 < n and not (text[i] == "*" and text[i + 1] == "/"):
                i += 1
            i += 2
        elif ch == '"':
            i += 1
            while i < n and text[i] != '"':
                if text[i] == "\\":
                    i += 1
                i += 1
            i += 1
        elif ch == "'":
            i += 1
            while i < n and text[i] != "'":
                if text[i] == "\\":
                    i += 1
                i += 1
            i += 1
        else:
            out.append(ch)
            i += 1
    return "".join(out)


def declared_names(code: str) -> set[str]:
    names: set[str] = set()
    for m in DECL_RE.finditer(code):
        names.add(m.group(1))
    for m in VAR_RE.finditer(code):
        names.add(m.group(1))
    for m in OUT_RE.finditer(code):
        names.add(m.group(1))
    for m in FOREACH_RE.finditer(code):
        names.add(m.group(1))
    for m in LAMBDA_ARG_RE.finditer(code):
        for piece in m.group(1).split(","):
            piece = piece.strip()
            m2 = re.search(r"([a-z_]\w*)$", piece)
            if m2:
                names.add(m2.group(1))
    # parameter lists of declared members
    for m in re.finditer(r"\b[A-Za-z_]\w*\s*\(([^)]*)\)", code):
        for piece in m.group(1).split(","):
            piece = piece.strip()
            if not piece or piece.startswith("//"):
                continue
            m2 = re.search(r"([a-z_]\w*)\s*(?:=|$)", piece)
            if m2:
                names.add(m2.group(1))
    return {n for n in names if n not in IGNORE and len(n) > 1}


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default=None)
    ap.add_argument("--all", action="store_true")
    args = ap.parse_args()

    out_root = os.path.abspath(args.out) if args.out else OUT

    compared = 0
    findings: dict[str, list[str]] = defaultdict(list)

    for dirpath, _dirs, filenames in os.walk(out_root):
        for fn in filenames:
            if not fn.endswith(".cs"):
                continue
            out_path = os.path.join(dirpath, fn)
            rel = os.path.relpath(out_path, out_root).replace("\\", "/")
            src_path = os.path.join(SRC, rel.replace("/", os.sep))
            if not os.path.exists(src_path):
                continue
            compared += 1

            orig = strip_code(open(src_path, encoding="utf-8", errors="replace").read())
            ext = strip_code(open(out_path, encoding="utf-8", errors="replace").read())

            lost = declared_names(orig) - declared_names(ext)
            ext_body = ext
            for name in sorted(lost):
                # still referenced anywhere in the extracted file?
                if re.search(r"(?<![\w.])" + re.escape(name) + r"(?![\w])", ext_body):
                    # count references to keep the signal meaningful
                    hits = len(re.findall(r"(?<![\w.])" + re.escape(name) + r"(?![\w])", ext_body))
                    findings[rel].append(f"{name} (still referenced {hits}x)")

    print(f"files compared          : {compared}")
    print(f"files with dangling refs: {len(findings)}")
    print(f"dangling locals         : {sum(len(v) for v in findings.values())}")
    print()
    for rel in sorted(findings, key=lambda r: -len(findings[r])):
        print(f"  {rel}  ({len(findings[rel])})")
        show = findings[rel] if args.all else findings[rel][:8]
        for item in show:
            print(f"      {item}")
        if not args.all and len(findings[rel]) > len(show):
            print(f"      ... and {len(findings[rel]) - len(show)} more")
    return 1 if findings else 0


if __name__ == "__main__":
    raise SystemExit(main())
