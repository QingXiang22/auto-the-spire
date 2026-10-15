"""Repair code left referencing a local variable the extraction removed.

`audit_dangling_locals.py` finds locals declared in `sts2/src` but absent from `sts2-core`
while still being named there. In practice these are always visual node handles — `creatureNode`,
`nCreature`, `vector`, `ostyNode` — left behind when a VFX branch was cut but its guard stayed.

This removes the leftover: a control-flow header naming such a name takes its block with it,
and a plain statement naming one is dropped. Each file is delimiter-checked and reverted if the
edit would break it, and the result is re-checked until no dangling name is referenced.

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
from audit_dangling_locals import declared_names, strip_code  # noqa: E402
from finalize import (  # noqa: E402
    code_of,
    delimiters_balanced,
    match_block,
    statement_end,
)

SRC = os.path.join(REPO, "sts2", "src")
CONTROL_OPEN_RE = re.compile(r"^\s*\}?\s*(?:if|else\s+if|else|foreach|for|while|do|try|lock)\b")

# A line naming any of these is never a presentation leftover: refusing to touch it is what
# keeps this pass from eating rules. (An earlier version deleted `await enemy.TakeTurn();`
# because it treated an orphaned `if (...)` header as owning the statement beneath it.)
GAMEPLAY_GUARD = re.compile(
    r"\b(?:DamageCmd|PowerCmd|CreatureCmd|CardCmd|CardPileCmd|CardSelectCmd|OrbCmd"
    r"|PlayerCmd|PotionCmd|RelicCmd|Hook|CombatManager|ModelDb|CheckWinCondition"
    r"|TakeTurn|WaitForUnpause|IsInProgress|Apply|Execute|AutoPlay|InitiateCombat)\b"
)


def next_code_index(lines: list[str], start: int) -> int:
    j = start + 1
    while j < len(lines):
        if code_of(lines[j]).strip():
            return j
        j += 1
    return -1


def header_owns_block(lines: list[str], i: int) -> bool:
    """True when the control header at `i` opens a brace body."""
    if "{" in code_of(lines[i]):
        return True
    nxt = next_code_index(lines, i)
    return nxt != -1 and code_of(lines[nxt]).strip().startswith("{")


def dangling_names(rel: str, ext_code: str) -> set[str]:
    src_path = os.path.join(SRC, rel.replace("/", os.sep))
    if not os.path.exists(src_path):
        return set()
    orig = strip_code(open(src_path, encoding="utf-8", errors="replace").read())
    lost = declared_names(orig) - declared_names(ext_code)
    return {
        n for n in lost
        if re.search(r"(?<![\w.])" + re.escape(n) + r"(?![\w])", ext_code)
    }


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args()

    total = Counter()
    files_changed = 0
    skipped: list[str] = []

    for dirpath, _dirs, filenames in os.walk(OUT):
        for fn in filenames:
            if not fn.endswith(".cs"):
                continue
            path = os.path.join(dirpath, fn)
            rel = os.path.relpath(path, OUT).replace("\\", "/")
            with open(path, "r", encoding="utf-8", errors="replace") as fh:
                lines = fh.readlines()

            ext_code = strip_code("".join(lines))
            names = dangling_names(rel, ext_code)
            if not names:
                continue

            pattern = re.compile(
                r"(?<![\w.])(?:" + "|".join(re.escape(n) for n in names) + r")(?![\w])"
            )
            working = list(lines)
            edits = 0
            i = 0
            while i < len(working):
                code = code_of(working[i])
                if not code.strip() or not pattern.search(code):
                    i += 1
                    continue
                if GAMEPLAY_GUARD.search(code):
                    i += 1
                    continue
                if CONTROL_OPEN_RE.match(code):
                    if header_owns_block(working, i):
                        blk = match_block(working, i)
                        if blk is None:
                            i += 1
                            continue
                        del working[i:blk]
                        edits += 1
                        continue
                    # body was already removed: drop only the orphaned header
                    del working[i]
                    edits += 1
                    continue
                del working[i:statement_end(working, i)]
                edits += 1

            if not edits:
                skipped.append(rel)
                continue

            text = re.sub(r"\n{4,}", "\n\n\n", "".join(working))
            text = re.sub(r"[ \t]+\n", "\n", text)
            if not delimiters_balanced(text):
                skipped.append(f"{rel} (delimiters)")
                continue

            leftover = strip_code(text)
            still = [
                n for n in names
                if re.search(r"(?<![\w.])" + re.escape(n) + r"(?![\w])", leftover)
            ]
            if still:
                skipped.append(f"{rel} (still references {', '.join(sorted(still))})")
                continue

            files_changed += 1
            total["removed-stmt-or-block"] += edits
            if not args.check:
                with open(path, "w", encoding="utf-8", newline="\n") as fh:
                    fh.write(text)

    print(f"files changed : {files_changed}")
    for k, v in total.most_common():
        print(f"  {k:<22} {v}")
    if skipped:
        print(f"\nleft for manual review ({len(skipped)}):")
        for item in skipped:
            print(f"  {item}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
