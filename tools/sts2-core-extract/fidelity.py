"""Fidelity check: did any *gameplay* call get lost while stripping presentation code?

Presentation removal is safe only if it takes animation with it and nothing else. This
compares every extracted file against its `sts2/src` original and counts how many times each
gameplay API appears in each. A file where the extracted count is *lower* than the original
lost gameplay and is reported for review.

Counts are per-file and per-API, so a card that used to call `PowerCmd.Apply` twice and now
calls it once is flagged even though the file still compiles.

Note the reverse direction is expected and fine: presentation-heavy files legitimately keep
their gameplay counts identical, and nothing should ever *increase* except where a human
deliberately restored swallowed code (recorded in the report, not treated as an error).

Run from the repository root; exits non-zero if any loss is found.
"""

from __future__ import annotations

import os
import re
import sys
from collections import Counter, defaultdict

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
OUT = os.path.join(REPO, "sts2-core")
SRC = os.path.join(REPO, "sts2", "src")

# Verified exceptions. `ModelDb` is a lookup helper used by presentation *and* rules, so a
# card/monster lookup disappearing is usually the removal of a tooltip or asset-path member
# rather than a lost rule. Each entry below was read by hand and confirmed to sit inside a
# member that is itself presentation:
#   Fasten                    -> `ModelDb.Card<DefendIronclad>()` inside `ExtraHoverTips`
#   PhrogParasiteElite        -> `ModelDb.Card<Infection>().OverlayPath` inside `ExtraAssetPaths`
#   Queen                     -> `ModelDb.Monster<TorchHeadAmalgam>().AssetPaths` inside `AssetPaths`
#   WitheringPresencePower    -> both `ModelDb.Card<Wither>()` inside `ExtraHoverTips`
FALSE_POSITIVES = {
    ("Core/Models/Cards/Fasten.cs", "ModelDb.Card"),
    ("Core/Models/Encounters/PhrogParasiteElite.cs", "ModelDb.Card"),
    ("Core/Models/Monsters/Queen.cs", "ModelDb.Monster"),
    ("Core/Models/Powers/WitheringPresencePower.cs", "ModelDb.Card"),
}

# Rules surfaces that must survive the extraction verbatim in count.
GAMEPLAY_APIS = [
    "DamageCmd.Attack", "DamageCmd.Damage",
    "PowerCmd.Apply", "PowerCmd.Remove", "PowerCmd.ModifyAmount",
    "CreatureCmd.Damage", "CreatureCmd.GainBlock", "CreatureCmd.Kill",
    "CreatureCmd.Heal", "CreatureCmd.Summon", "CreatureCmd.SetHp",
    "CardCmd.Exhaust", "CardCmd.Discard", "CardCmd.Draw",
    "CardPileCmd.Add", "CardPileCmd.Draw", "CardPileCmd.Shuffle",
    "OrbCmd.Channel", "OrbCmd.Evoke",
    "PlayerCmd.SetEnergy", "PlayerCmd.SetStars", "PlayerCmd.GainEnergy",
    "Hook.BeforeAttack", "Hook.AfterAttack", "Hook.BeforeTurnEnd", "Hook.AfterTurnEnd",
    "Hook.ModifyDamage", "Hook.AfterCardDrawn", "Hook.AfterCardExhausted",
    "ModelDb.Card", "ModelDb.Power", "ModelDb.Monster",
]

# Tokens whose *whole-line* presence is structural: compare as multiset too.
GAMEPLAY_LINE_PATTERNS = [
    r"^\s*public override int MinInitialHp",
    r"^\s*public override int MaxInitialHp",
    r"^\s*protected override IEnumerable<DynamicVar> CanonicalVars",
    r"^\s*protected override MonsterMoveStateMachine GenerateMoveStateMachine",
    r"^\s*protected override async Task OnPlay",
    r"^\s*public override async Task AfterAddedToRoom",
    r"^\s*new MoveState\(",
    r"^\s*new (?:Single|Multi)?AttackIntent\(",
    r"^\s*new (?:Defend|Buff|Debuff|Status|Summon|Heal|Escape|Stun|Sleep|DeathBlow|Hidden|Unknown|CardDebuff)Intent\(",
]


def counts(text: str) -> tuple[Counter, Counter]:
    apis = Counter()
    for api in GAMEPLAY_APIS:
        apis[api] = len(re.findall(re.escape(api), text))
    lines = Counter()
    for pat in GAMEPLAY_LINE_PATTERNS:
        lines[pat] = len(re.findall(pat, text, re.MULTILINE))
    return apis, lines


def main() -> int:
    out_root = OUT
    if len(sys.argv) > 1 and not sys.argv[1].startswith("-"):
        out_root = os.path.abspath(sys.argv[1])
    losses: list[tuple[str, str, int, int]] = []
    gains: list[tuple[str, str, int, int]] = []
    checked = 0

    for dirpath, _dirs, filenames in os.walk(out_root):
        for fn in filenames:
            if not fn.endswith(".cs"):
                continue
            out_path = os.path.join(dirpath, fn)
            rel = os.path.relpath(out_path, out_root).replace("\\", "/")
            src_path = os.path.join(SRC, rel.replace("/", os.sep))
            if not os.path.exists(src_path):
                continue
            checked += 1

            out_text = open(out_path, encoding="utf-8", errors="replace").read()
            src_text = open(src_path, encoding="utf-8", errors="replace").read()
            out_apis, out_lines = counts(out_text)
            src_apis, src_lines = counts(src_text)

            for key in GAMEPLAY_APIS:
                if out_apis[key] < src_apis[key]:
                    if (rel, key) in FALSE_POSITIVES:
                        continue
                    losses.append((rel, key, src_apis[key], out_apis[key]))
                elif out_apis[key] > src_apis[key]:
                    gains.append((rel, key, src_apis[key], out_apis[key]))
            for pat in GAMEPLAY_LINE_PATTERNS:
                if out_lines[pat] < src_lines[pat]:
                    if (rel, pat) in FALSE_POSITIVES:
                        continue
                    losses.append((rel, pat, src_lines[pat], out_lines[pat]))
                elif out_lines[pat] > src_lines[pat]:
                    gains.append((rel, pat, src_lines[pat], out_lines[pat]))

    print(f"files compared : {checked}")
    print(f"gameplay losses: {len(losses)}")
    print(f"gameplay gains : {len(gains)}  (expected where a human restored swallowed code)")

    if losses:
        print("\n--- LOSSES (must all be justified) ---")
        by_file = defaultdict(list)
        for rel, key, a, b in losses:
            by_file[rel].append(f"{key} {a}->{b}")
        for rel in sorted(by_file):
            print(f"\n  {rel}")
            for item in by_file[rel]:
                print(f"      {item}")

    if gains:
        print("\n--- GAINS ---")
        by_file = defaultdict(list)
        for rel, key, a, b in gains:
            by_file[rel].append(f"{key} {a}->{b}")
        for rel in sorted(by_file)[:40]:
            print(f"  {rel}: {'; '.join(by_file[rel])}")

    return 1 if losses else 0


if __name__ == "__main__":
    raise SystemExit(main())
