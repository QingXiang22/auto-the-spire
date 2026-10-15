"""Remove the last non-animation presentation surface: camera offsets and pool colours.

After the animation/SFX/VFX/netcode extraction, what remains of the Godot surface is purely
cosmetic bookkeeping that never affects combat resolution:

* `GetCameraOffset()` overrides on encounters (camera framing),
* `DeckEntryCardColor` / `EnergyOutlineColor` / `LabOutlineColor` (card-back and lab tinting),
* `Osty`'s visual offset/scale ranges,
* `TestSubject.SetColor`, `ToughEgg.HatchPos`, `DoomPower.WaitForTween`,
* orphaned `Vector2? x = null;` locals left behind where a bone-positioning block was cut,
* the two helper files `MathHelper.cs` (tween easing maths) and `FontControlUtils.cs`.

Each member is removed with the corrected brace handling (expression-bodied members are
bounded by their own `;`), and every file is delimiter-checked and reverted on failure.

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
from finalize import (  # noqa: E402
    code_of,
    delimiters_balanced,
    match_block,
    starts_brace_block,
    statement_end,
)

# Whole members (base declarations and every override) that are presentational.
REMOVE_MEMBERS = re.compile(
    r"^\s*(?:\[[^\]]*\]\s*)*(?:public|protected|private|internal)\b[^;]*?\b(?:"
    r"GetCameraOffset|DeckEntryCardColor|EnergyOutlineColor|LabOutlineColor"
    r"|MinOffset|MaxOffset|ScaleRange|HatchPos|_hatchPos|SetColor|WaitForTween"
    r"|PlayChannelSfx|PlayEvokeSfx|PlayPassiveSfx|ChannelSfx|EvokeSfx|PassiveSfx"
    r"|DebugChannelSfx|DebugEvokeSfx|DebugPassiveSfx|UpdateTexture|RelicIconChanged"
    r"|HoverTipsExcludingRelic|AmountLabelColor|GetOptionTitle|VfxOffset"
    r"|AnimSegmentsAttack|ChangePhobiaModeTexture|BossNodeSpineResource"
    r"|CreateOverlay|OverlayPath|AllPortraitPaths|GetPortraitFilename|GetPortraitPath"
    r"|HealedAmount|StringName)\b"
)

# Orphaned locals whose visual consumer was already cut.
REMOVE_LOCALS = re.compile(r"^\s*Vector2\??\s+\w+\s*=\s*null\s*;")

# Files that are nothing but presentation plumbing.
DELETE_FILES = [
    "Core/Helpers/MathHelper.cs",
    "Core/Localization/Fonts/FontControlUtils.cs",
]


def process(lines: list[str]) -> tuple[list[str], list[str]]:
    edits: list[str] = []
    out = list(lines)

    changed = True
    while changed:
        changed = False
        i = 0
        while i < len(out):
            code = code_of(out[i])

            if REMOVE_LOCALS.match(code):
                edits.append("orphan-local")
                del out[i]
                changed = True
                continue

            if REMOVE_MEMBERS.match(code):
                if "=>" in code and "{" not in code:
                    end = statement_end(out, i)
                elif "{" in code or starts_brace_block(out, i):
                    end = match_block(out, i) or statement_end(out, i)
                else:
                    end = statement_end(out, i)
                edits.append("member")
                del out[i:end]
                changed = True
                continue

            i += 1

    # tidy blank runs left behind
    text = re.sub(r"\n{4,}", "\n\n\n", "".join(out))
    text = re.sub(r"[ \t]+\n", "\n", text)
    return text.splitlines(keepends=True), edits


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args()

    total = Counter()
    files_changed = 0
    reverted: list[str] = []

    for rel in DELETE_FILES:
        path = os.path.join(OUT, rel.replace("/", os.sep))
        if os.path.exists(path):
            if not args.check:
                os.remove(path)
            total["file-deleted"] += 1
            print(f"deleted: {rel}")

    for dirpath, _dirs, filenames in os.walk(OUT):
        for fn in filenames:
            if not fn.endswith(".cs"):
                continue
            path = os.path.join(dirpath, fn)
            with open(path, "r", encoding="utf-8", errors="replace") as fh:
                lines = fh.readlines()
            if not any(
                REMOVE_MEMBERS.match(code_of(l)) or REMOVE_LOCALS.match(code_of(l))
                for l in lines
            ):
                continue

            new_lines, edits = process(lines)
            if not edits:
                continue

            text = "".join(new_lines)
            rel = os.path.relpath(path, OUT).replace("\\", "/")
            if not delimiters_balanced(text):
                reverted.append(rel)
                continue

            files_changed += 1
            for kind in edits:
                total[kind] += 1
            if not args.check:
                with open(path, "w", encoding="utf-8", newline="\n") as fh:
                    fh.write(text)

    print(f"\nfiles changed : {files_changed}")
    for k, v in total.most_common():
        print(f"  {k:<14} {v}")
    if reverted:
        print(f"\nreverted (delimiters would break) : {len(reverted)}")
        for rel in reverted:
            print(f"  {rel}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
