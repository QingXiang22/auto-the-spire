"""Restore constructor bodies lost to the old "tidy empty blocks" rule.

`finalize.py` used to delete any ``{`` immediately followed by ``}``. That is right for an
empty control-flow branch left behind by a visual removal, but wrong for a member body: a
card whose constructor only forwards to ``base(...)`` looks like

    public Claw()
        : base(0, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
    }

and the braces were stripped, leaving a declaration with no body.

For every affected file this checks the `sts2/src` original: if the original constructor body
really was empty, the two braces are re-inserted at the correct indentation. If the original
body had statements, the file is reported instead of guessed at.

Run with ``--check`` to report without writing.
"""

from __future__ import annotations

import argparse
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
OUT = os.path.join(REPO, "sts2-core")
SRC = os.path.join(REPO, "sts2", "src")

CTOR_TAIL_RE = re.compile(r"^\s*:\s*(?:base|this)\s*\(")


def original_body_is_empty(rel: str, signature: str) -> bool | None:
    """In the original file, is the body of the constructor named in `signature` empty?"""
    src = os.path.join(SRC, rel.replace("/", os.sep))
    if not os.path.exists(src):
        return None
    with open(src, "r", encoding="utf-8", errors="replace") as fh:
        lines = fh.read().splitlines()
    for i, line in enumerate(lines):
        if line.strip() == signature.strip():
            # walk to the opening brace, then check what follows
            j = i + 1
            while j < len(lines) and "{" not in lines[j]:
                j += 1
            if j >= len(lines):
                return None
            k = j + 1
            while k < len(lines) and not lines[k].strip():
                k += 1
            return k < len(lines) and lines[k].strip().startswith("}")
    return None


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args()

    fixed = 0
    manual: list[str] = []

    for dirpath, _dirs, filenames in os.walk(OUT):
        for fn in filenames:
            if not fn.endswith(".cs"):
                continue
            path = os.path.join(dirpath, fn)
            rel = os.path.relpath(path, OUT).replace("\\", "/")
            with open(path, "r", encoding="utf-8", errors="replace") as fh:
                lines = fh.read().splitlines(keepends=True)

            changed = False
            i = 0
            while i < len(lines):
                if not CTOR_TAIL_RE.match(lines[i]):
                    i += 1
                    continue
                j = i + 1
                while j < len(lines) and not lines[j].strip():
                    j += 1
                nxt = lines[j].strip() if j < len(lines) else ""
                if nxt.startswith("{"):
                    i += 1
                    continue

                # constructor signature is the nearest preceding declaration line
                sig_idx = i - 1
                while sig_idx > 0 and not lines[sig_idx].strip():
                    sig_idx -= 1
                signature = lines[sig_idx]

                verdict = original_body_is_empty(rel, signature)
                if verdict is not True:
                    manual.append(f"{rel}: {signature.strip()} (original body not verifiably empty)")
                    i += 1
                    continue

                indent = re.match(r"[ \t]*", signature).group(0)
                lines[j:j] = [f"{indent}{{\n", f"{indent}}}\n"]
                changed = True
                fixed += 1
                i = j + 2

            if changed and not args.check:
                with open(path, "w", encoding="utf-8", newline="\n") as fh:
                    fh.writelines(lines)

    print(f"constructor bodies restored : {fixed}")
    if manual:
        print(f"\nneeds review ({len(manual)}):")
        for item in manual:
            print(f"  {item}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
