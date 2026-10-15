"""Collect inventory statistics for `sts2-core/` and emit them as markdown tables.

Run from the repository root:

    python tools/sts2-core-extract/inventory.py

Used to keep `sts2-core/README.md` numbers honest rather than hand-copied.
"""

from __future__ import annotations

import os
import re
from collections import Counter, defaultdict

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
OUT = os.path.join(REPO, "sts2-core")
SRC = os.path.join(REPO, "sts2", "src")

# Top-level areas of sts2-core/Core, in the order they matter for combat.
AREA_LABELS = {
    "Combat": "`Combat/` - turn loop, combat state, history",
    "Hooks": "`Hooks/` - the hook dispatch surface",
    "Commands": "`Commands/` - effect API (damage, powers, cards, orbs)",
    "GameActions": "`GameActions/` - action queue and player-choice plumbing",
    "MonsterMoves": "`MonsterMoves/` - intents and the move state machine",
    "Entities": "`Entities/` - Creature / Player / Card / Power / Orb / pile runtime",
    "Models": "`Models/` - model base classes and combat content",
    "Random": "`Random/` - RNG",
    "ValueProps": "`ValueProps/` - damage value properties",
    "CardSelection": "`CardSelection/` - card-select preferences",
    "Helpers": "`Helpers/` - retained non-visual helpers",
    "Localization": "`Localization/` - model text and dynamic vars",
    "Exceptions": "`Exceptions/`",
    "Extensions": "`Extensions/`",
    "Modding": "`Modding/` - mod loading surface",
}


def walk_cs(root: str):
    for dirpath, _dirs, filenames in os.walk(root):
        for fn in filenames:
            if fn.endswith(".cs"):
                yield os.path.join(dirpath, fn)


def main() -> int:
    core = os.path.join(OUT, "Core")
    files = sorted(walk_cs(core))

    per_area_files = Counter()
    per_area_lines = Counter()
    total_lines = 0
    for path in files:
        rel = os.path.relpath(path, core).replace("\\", "/")
        area = rel.split("/")[0]
        with open(path, "r", encoding="utf-8", errors="replace") as fh:
            n = sum(1 for _ in fh)
        per_area_files[area] += 1
        per_area_lines[area] += n
        total_lines += n

    print(f"### sts2-core inventory\n")
    print(f"- C# files: **{len(files)}**")
    print(f"- lines: **{total_lines:,}**")
    print()
    print("| area | files | lines |")
    print("|---|---:|---:|")
    for area, _ in sorted(per_area_files.items(), key=lambda kv: -per_area_lines[kv[0]]):
        label = AREA_LABELS.get(area, f"`{area}/`")
        print(f"| {label} | {per_area_files[area]} | {per_area_lines[area]:,} |")
    print()

    # Models breakdown, since it dominates
    models = os.path.join(core, "Models")
    sub_files = Counter()
    sub_lines = Counter()
    for path in walk_cs(models):
        rel = os.path.relpath(path, models).replace("\\", "/")
        sub = rel.split("/")[0] if "/" in rel else "(root)"
        with open(path, "r", encoding="utf-8", errors="replace") as fh:
            n = sum(1 for _ in fh)
        sub_files[sub] += 1
        sub_lines[sub] += n
    print("### Models breakdown\n")
    print("| group | files | lines |")
    print("|---|---:|---:|")
    for sub, _ in sorted(sub_files.items(), key=lambda kv: -sub_lines[kv[0]]):
        print(f"| `Models/{sub}` | {sub_files[sub]} | {sub_lines[sub]:,} |")
    print()

    # What was dropped relative to the source tree
    src_files = list(walk_cs(SRC))
    print("### Source vs extraction\n")
    print(f"- `sts2/src` C# files: **{len(src_files)}**")
    print(f"- copied into `sts2-core`: **{len(files)}**")
    print(f"- not copied (UI/animation/audio/out-of-scope content): **{len(src_files) - len(files)}**")
    print()

    # Residue that intentionally remains
    godot = Counter()
    for path in files:
        with open(path, "r", encoding="utf-8", errors="replace") as fh:
            text = fh.read()
        if re.search(r"^\s*using Godot;", text, re.MULTILINE):
            godot["files with 'using Godot;'"] += 1
        for m in re.finditer(r"\b(Vector2|Node2D|Control|Node|Tween|Color|Resource)\b", text):
            godot[m.group(1)] += 1
    print("### Residual Godot surface\n")
    for k, v in godot.most_common():
        print(f"- {k}: {v}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
