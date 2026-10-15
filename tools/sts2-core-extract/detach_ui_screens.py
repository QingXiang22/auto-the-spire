"""Replace `CardSelectCmd`'s UI selection screens with the original's own "no choice" encoding.

`CardSelectCmd` obtains the local player's pick from a Godot screen
(`NChooseACardSelectionScreen`, `NSimpleCardSelectScreen`, `NCombatPileCardSelectScreen`,
`NDeck*SelectScreen`, `NChooseABundleSelectionScreen`). Those types live in `Core/Nodes`,
which this extraction does not include, so keeping the calls would leave dangling references.

Dropping the calls outright is what an earlier pass did — and it silently gutted the methods,
because the chosen value feeds `PlayerChoiceSynchronizer` bookkeeping and the return value.

So each site is replaced by the encoding the original itself uses for "the player chose
nothing": `null`, an empty list, or `PlayerChoiceResult.FromIndex(-1)`. The surrounding
choice plumbing is left untouched. Every substitution is marked with a `[sts2-core]` comment
naming the screen that would have supplied the input, and the headless path
(`CardSelectCmd.Selector`) is unaffected.

Run with ``--check`` to report without writing.
"""

from __future__ import annotations

import argparse
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
TARGET = os.path.join(REPO, "sts2-core", "Core", "Commands", "CardSelectCmd.cs")

NOTE = (
    "{i}// [sts2-core] The local player's choice normally comes from {screen}\n"
    "{i}// (Core.Nodes), which this extraction excludes. A driver supplies\n"
    "{i}// CardSelectCmd.Selector instead; with no UI present the choice is reported as\n"
    "{i}// \"nothing selected\", using the same encoding the original uses for a skipped pick.\n"
)

# (screen-create line regex, chosen-value line regex, replacement statement)
SITES = [
    (
        r"^\s*NChooseACardSelectionScreen nChooseACardSelectionScreen = NChooseACardSelectionScreen\.ShowScreen\(cards, canSkip\);\n",
        r"^(?P<i>\s*)result = \(await nChooseACardSelectionScreen\.CardsSelected\(\)\)\.FirstOrDefault\(\);\n",
        "result = null;",
        "NChooseACardSelectionScreen",
    ),
    (
        r"^\s*NSimpleCardSelectScreen nSimpleCardSelectScreen = NSimpleCardSelectScreen\.Create\(cards, prefs\);\n"
        r"^\s*NOverlayStack\.Instance\.Push\(nSimpleCardSelectScreen\);\n",
        r"^(?P<i>\s*)result = \(await nSimpleCardSelectScreen\.CardsSelected\(\)\)\.ToList\(\);\n",
        "result = new List<CardModel>();",
        "NSimpleCardSelectScreen",
    ),
    (
        r"^\s*NCombatPileCardSelectScreen nCombatPileCardSelectScreen = NCombatPileCardSelectScreen\.Create\(pile, prefs, filter\);\n"
        r"^\s*NOverlayStack\.Instance\.Push\(nCombatPileCardSelectScreen\);\n",
        r"^(?P<i>\s*)result = \(await nCombatPileCardSelectScreen\.CardsSelected\(\)\)\.ToList\(\);\n",
        "result = new List<CardModel>();",
        "NCombatPileCardSelectScreen",
    ),
    (
        r"^\s*NDeckUpgradeSelectScreen nDeckUpgradeSelectScreen = NDeckUpgradeSelectScreen\.ShowScreen\(list, prefs, player\.RunState\);\n",
        r"^(?P<i>\s*)enumerable = await nDeckUpgradeSelectScreen\.CardsSelected\(\);\n",
        "enumerable = Array.Empty<CardModel>();",
        "NDeckUpgradeSelectScreen",
    ),
    (
        r"^\s*NDeckTransformSelectScreen nDeckTransformSelectScreen = NDeckTransformSelectScreen\.ShowScreen\(list, cardToTransformation, prefs\);\n",
        r"^(?P<i>\s*)enumerable = await nDeckTransformSelectScreen\.CardsSelected\(\);\n",
        "enumerable = Array.Empty<CardModel>();",
        "NDeckTransformSelectScreen",
    ),
    (
        r"^\s*NDeckEnchantSelectScreen nDeckEnchantSelectScreen = NDeckEnchantSelectScreen\.ShowScreen\(list, enchantment, amount, prefs\);\n",
        r"^(?P<i>\s*)enumerable = await nDeckEnchantSelectScreen\.CardsSelected\(\);\n",
        "enumerable = Array.Empty<CardModel>();",
        "NDeckEnchantSelectScreen",
    ),
    (
        r"^\s*NDeckCardSelectScreen nDeckCardSelectScreen = NDeckCardSelectScreen\.Create\(list, prefs\);\n"
        r"^\s*NOverlayStack\.Instance\.Push\(nDeckCardSelectScreen\);\n",
        r"^(?P<i>\s*)enumerable = await nDeckCardSelectScreen\.CardsSelected\(\);\n",
        "enumerable = Array.Empty<CardModel>();",
        "NDeckCardSelectScreen",
    ),
    (
        r"^\s*NChooseABundleSelectionScreen nChooseABundleSelectionScreen = NChooseABundleSelectionScreen\.ShowScreen\(bundles\);\n",
        r"^(?P<i>\s*)readOnlyList = \(await nChooseABundleSelectionScreen\.CardsSelected\(\)\)\.FirstOrDefault\(\) \?\? Array\.Empty<CardModel>\(\);\n",
        "readOnlyList = Array.Empty<CardModel>();",
        "NChooseABundleSelectionScreen",
    ),
]


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args()

    with open(TARGET, "r", encoding="utf-8", errors="replace") as fh:
        text = fh.read()

    total = 0
    for create_re, value_re, replacement, screen in SITES:
        hits = len(re.findall(create_re, text, re.MULTILINE))
        if hits == 0:
            print(f"  !! no create-site found for {screen}")
            continue

        def repl_value(m: re.Match) -> str:
            indent = m.group("i")
            return NOTE.format(i=indent, screen=screen) + f"{indent}{replacement}\n"

        text, n = re.subn(create_re, "", text, flags=re.MULTILINE)
        text, n2 = re.subn(value_re, repl_value, text, flags=re.MULTILINE)
        print(f"  {screen}: removed {n} create line(s), rewrote {n2} value line(s)")
        total += n + n2

    if not args.check:
        with open(TARGET, "w", encoding="utf-8", newline="\n") as fh:
            fh.write(text)
    print(f"\ntotal edits: {total}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
