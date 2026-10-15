"""Third pass: strip fluent presentation-builder calls from otherwise-gameplay expressions.

`AttackCommand` exposes a fluent builder. After `extract.py` removed the builder methods
themselves, call sites still read:

    await DamageCmd.Attack(6).FromMonster(this)
        .WithHitVfxNode((Creature t) => NBigSlashVfx.Create(t))
        .OnlyPlayAnimOnce()
        .Execute(null);

Removing those lines leaves the gameplay chain intact. Some calls are single-line and
inline, so balanced-paren scanning is used rather than line matching.

Like `finalize.py`, this is balance-aware: a file whose delimiters would break is left
untouched and reported, so nothing is silently corrupted.

Run with ``--check`` to report without writing.
"""

from __future__ import annotations

import argparse
import os
import re
import sys
from collections import Counter

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
OUT = os.path.join(REPO, "sts2-core")

sys.path.insert(0, HERE)
from extract import CHAIN_CALLS, find_matching_paren  # noqa: E402
from finalize import delimiters_balanced  # noqa: E402


def strip_chains(text: str) -> tuple[str, int]:
    """Remove every presentation builder call from ``text``; return (new_text, count)."""
    removed = 0
    changed = True
    while changed:
        changed = False
        for name in CHAIN_CALLS:
            pattern = re.compile(r"\." + re.escape(name) + r"\s*\(")
            m = pattern.search(text)
            if not m:
                continue
            open_idx = text.index("(", m.start())
            try:
                end = find_matching_paren(text, open_idx)
            except ValueError:
                continue
            # swallow the line break + indentation if the call starts a continuation line
            start = m.start()
            line_start = text.rfind("\n", 0, start) + 1
            if text[line_start:start].strip() == "":
                start = line_start
            text = text[:start] + text[end:]
            removed += 1
            changed = True
    return text, removed


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--out", default=None, help="override the target directory")
    args = ap.parse_args()

    global OUT
    if args.out:
        OUT = os.path.abspath(args.out)

    total = 0
    files_changed = 0
    reverted: list[str] = []

    for dirpath, _dirs, filenames in os.walk(OUT):
        for fn in filenames:
            if not fn.endswith(".cs"):
                continue
            path = os.path.join(dirpath, fn)
            with open(path, "r", encoding="utf-8", errors="replace") as fh:
                text = fh.read()
            if not any("." + n in text for n in CHAIN_CALLS):
                continue

            new_text, removed = strip_chains(text)
            if not removed:
                continue

            new_text = re.sub(r"\n{4,}", "\n\n\n", new_text)
            new_text = re.sub(r"[ \t]+\n", "\n", new_text)
            rel = os.path.relpath(path, OUT).replace("\\", "/")

            if not delimiters_balanced(new_text):
                reverted.append(rel)
                continue

            files_changed += 1
            total += removed
            if not args.check:
                with open(path, "w", encoding="utf-8", newline="\n") as fh:
                    fh.write(new_text)

    print(f"files changed : {files_changed}")
    print(f"calls removed : {total}")
    if reverted:
        print(f"\nreverted (delimiters would break) : {len(reverted)}")
        for rel in reverted:
            print(f"  {rel}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
