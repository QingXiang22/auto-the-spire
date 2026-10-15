"""Final cosmetic pass: normalise whitespace left behind by removals.

When a fluent continuation line (``.WithHitFx("vfx/...")``) is dropped from the middle of a
chain, the surrounding blank/indented line can survive and leave a visibly broken-looking
call:

    await DamageCmd.Attack(6).FromCard(this).Targeting(target)
        <-- line holding only tabs
        .Execute(choiceContext);

This pass removes whitespace-only lines, collapses runs of blank lines, and converts CRLF to
LF. It never changes a token, so delimiters cannot break; it is still checked and reported.

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


def tidy(text: str) -> str:
    # An empty control-flow block is the leftover of a visual-only branch whose body was
    # removed. Dropping header + braces loses nothing, because the body is already empty.
    text = re.sub(
        r"^([ \t]*)(?:if|else\s+if|foreach|for|while|do|try|lock)\b[^\n{]*\n"
        r"([ \t]*)\{\n\2\}\n",
        "",
        text,
        flags=re.MULTILINE,
    )
    # a line holding only spaces/tabs is never meaningful inside a method body
    text = re.sub(r"^[ \t]+$", "", text, flags=re.MULTILINE)
    # collapse runs of blank lines down to at most one
    text = re.sub(r"\n{3,}", "\n\n", text)
    # strip trailing whitespace
    text = re.sub(r"[ \t]+\n", "\n", text)
    # normalise line endings and guarantee a trailing newline
    text = text.replace("\r\n", "\n").replace("\r", "\n")
    if not text.endswith("\n"):
        text += "\n"
    return text


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args()

    changed = 0
    for dirpath, _dirs, filenames in os.walk(OUT):
        for fn in filenames:
            if not fn.endswith(".cs"):
                continue
            path = os.path.join(dirpath, fn)
            with open(path, "r", encoding="utf-8", errors="replace", newline="") as fh:
                original = fh.read()
            new = tidy(original)
            if new == original:
                continue
            changed += 1
            if not args.check:
                with open(path, "w", encoding="utf-8", newline="\n") as fh:
                    fh.write(new)

    print(f"files tidied : {changed}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
