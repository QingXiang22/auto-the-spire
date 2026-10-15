"""Repair over-stripped files in `sts2-core` from a corrected pipeline output.

Background: the first `finalize.py` had a bug that deleted spans of *gameplay* members (it
brace-matched expression-bodied members and scanned forward to an unrelated block). The
delimiter check could not see it because the deletions were balanced. Four real casualties
were found by `fidelity.py` and `member_diff.py`: `BloodWall`, `DramaticEntrance`,
`MoltenFist`, `Queen`, `EnchantmentModel`, `CardModel.Type`.

This script regenerates nothing. It compares the current tree against a clean pipeline
output and decides per file:

* identical                                  -> nothing to do
* current lines are a *subsequence* of clean -> the current file only lost lines, so the
                                                clean version is a strict improvement: replace
* otherwise                                  -> someone edited this file by hand (restoring
                                                code the old pass swallowed, or rewriting a
                                                method). Report it; do not clobber it.

Run:

    python tools/sts2-core-extract/merge_repairs.py <clean-tree> [--apply]

Without ``--apply`` it only reports.
"""

from __future__ import annotations

import argparse
import os
import shutil
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
OUT = os.path.join(REPO, "sts2-core")


def norm(path: str) -> list[str]:
    with open(path, "r", encoding="utf-8", errors="replace") as fh:
        return [ln.rstrip() for ln in fh]


def is_subsequence(a: list[str], b: list[str]) -> bool:
    """True when every line of `a` appears in `b`, in order."""
    it = iter(b)
    return all(any(line == candidate for candidate in it) for line in a)


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("clean", help="path to the corrected pipeline output")
    ap.add_argument("--apply", action="store_true", help="write the repairs")
    args = ap.parse_args()

    clean_root = os.path.abspath(args.clean)
    if not os.path.isdir(clean_root):
        print(f"not a directory: {clean_root}", file=sys.stderr)
        return 2

    identical = 0
    repairable: list[str] = []
    diverged: list[str] = []
    missing: list[str] = []

    for dirpath, _dirs, filenames in os.walk(clean_root):
        for fn in filenames:
            if not fn.endswith(".cs"):
                continue
            clean_path = os.path.join(dirpath, fn)
            rel = os.path.relpath(clean_path, clean_root).replace("\\", "/")
            cur_path = os.path.join(OUT, rel.replace("/", os.sep))

            if not os.path.exists(cur_path):
                missing.append(rel)
                continue

            a, b = norm(cur_path), norm(clean_path)
            if a == b:
                identical += 1
            elif is_subsequence(a, b):
                repairable.append(rel)
            else:
                diverged.append(rel)

    print(f"identical                        : {identical}")
    print(f"repairable (current is subset)   : {len(repairable)}")
    print(f"diverged (hand-edited, review)   : {len(diverged)}")
    print(f"absent from current tree         : {len(missing)}")

    if repairable:
        print("\n--- will be replaced by the corrected version ---")
        for rel in repairable[:40]:
            print(f"  {rel}")
        if len(repairable) > 40:
            print(f"  ... and {len(repairable) - 40} more")

    if diverged:
        print("\n--- hand-edited, needs targeted repair check ---")
        for rel in diverged:
            print(f"  {rel}")

    if missing:
        print("\n--- only in corrected tree ---")
        for rel in missing[:40]:
            print(f"  {rel}")

    if args.apply:
        for rel in repairable:
            src = os.path.join(clean_root, rel.replace("/", os.sep))
            dst = os.path.join(OUT, rel.replace("/", os.sep))
            os.makedirs(os.path.dirname(dst), exist_ok=True)
            shutil.copyfile(src, dst)
        print(f"\napplied: {len(repairable)} files replaced")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
