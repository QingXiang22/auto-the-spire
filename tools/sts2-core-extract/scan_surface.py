"""Scan the sts2 combat-logic candidate set for animation / SFX / VFX / UI / multiplayer surface.

Produces a frequency report so the extractor's stripping rules can be derived from evidence
rather than guesswork.
"""
import os
import re
import sys
from collections import Counter, defaultdict

ROOT = r"F:\Scylier-Project\active\auto-the-spire"
SRC = os.path.join(ROOT, "sts2", "src", "Core")

# Directories that make up the *logic* candidate set (before animation stripping).
LOGIC_DIRS = [
    "Combat", "Hooks", "Random", "ValueProps", "CardSelection", "MonsterMoves",
    "Entities", "Commands", "GameActions", "Models", "Helpers", "Exceptions",
    "Extensions", "Factories", "Modding", "Odds", "Localization",
]

# Patterns whose *lines* are candidate animation/audio/UI/MP removals.
PATTERNS = {
    "using:Nodes": r"^\s*using\s+MegaCrit\.Sts2\.Core\.Nodes[.\w]*\s*;",
    "using:Audio": r"^\s*using\s+MegaCrit\.Sts2\.Core\.Audio\s*;",
    "using:Animation": r"^\s*using\s+MegaCrit\.Sts2\.Core\.Animation\s*;",
    "using:Bindings": r"^\s*using\s+MegaCrit\.Sts2\.Core\.Bindings\s*;",
    "using:TextEffects": r"^\s*using\s+MegaCrit\.Sts2\.Core\.TextEffects\s*;",
    "using:ControllerInput": r"^\s*using\s+MegaCrit\.Sts2\.Core\.ControllerInput\s*;",
    "using:RichTextTags": r"^\s*using\s+MegaCrit\.Sts2\.Core\.RichTextTags\s*;",
    "using:CoreMultiplayer": r"^\s*using\s+MegaCrit\.Sts2\.Core\.Multiplayer[.\w]*\s*;",
    "call:TriggerAnim": r"\bTriggerAnim\s*\(",
    "call:WithAttackerAnim": r"\.WithAttackerAnim\s*\(",
    "call:WithTargetAnim": r"\.WithTargetAnim\s*\(",
    "call:WithHitFx": r"\.WithHitFx\s*\(",
    "call:WithAttackFx": r"\.WithAttackFx\s*\(",
    "call:WithFx": r"\.With\w*Fx\s*\(",
    "call:SfxCmd": r"\bSfxCmd\s*\.",
    "call:VfxCmd": r"\bVfxCmd\s*\.",
    "call:NRunMusicController": r"\bNRunMusicController\b",
    "call:NAudioManager": r"\bNAudioManager\b",
    "call:NCombatRoom": r"\bNCombatRoom\b",
    "call:NCreature": r"\bNCreature\b",
    "call:NPlayerHand": r"\bNPlayerHand\b",
    "call:NGame": r"\bNGame\b",
    "call:NModalContainer": r"\bNModalContainer\b",
    "call:NMapScreen": r"\bNMapScreen\b",
    "call:NHoverTipSet": r"\bNHoverTipSet\b",
    "call:NTargetManager": r"\bNTargetManager\b",
    "call:NFtue": r"\bN\w*Ftue\b",
    "call:NBanner": r"\bN\w*Banner\b",
    "member:AnimDelay": r"\b\w*AnimDelay\b",
    "member:DeathAnim": r"\bdeathAnim\w*\b",
    "type:DamageSfxType": r"\bDamageSfxType\b",
    "type:Sfx": r"\bSfx\b",
    "type:AnimState": r"\bAnimState\b",
    "type:CreatureAnimator": r"\bCreatureAnimator\b",
    "type:FmodSfx": r"\bFmodSfx\b",
    "param:anim": r"\banim\w*\b",
}


def iter_cs(dirs):
    for d in dirs:
        base = os.path.join(SRC, d)
        if not os.path.isdir(base):
            continue
        for dirpath, _dirnames, filenames in os.walk(base):
            for fn in filenames:
                if fn.endswith(".cs"):
                    yield os.path.join(dirpath, fn)


def main():
    files = sorted(iter_cs(LOGIC_DIRS))
    print(f"logic files scanned: {len(files)}")

    line_hits = Counter()
    file_hits = defaultdict(set)
    examples = defaultdict(list)

    for path in files:
        try:
            with open(path, "r", encoding="utf-8", errors="replace") as fh:
                lines = fh.readlines()
        except OSError as exc:
            print(f"  !! {path}: {exc}", file=sys.stderr)
            continue
        for i, line in enumerate(lines, 1):
            for name, pat in PATTERNS.items():
                if re.search(pat, line):
                    line_hits[name] += 1
                    file_hits[name].add(path)
                    if len(examples[name]) < 3:
                        rel = os.path.relpath(path, ROOT)
                        examples[name].append(f"{rel}:{i}: {line.strip()[:110]}")

    print(f"\n{'pattern':<26}{'lines':>7}{'files':>7}")
    print("-" * 40)
    for name, _ in sorted(line_hits.items(), key=lambda kv: -kv[1]):
        print(f"{name:<26}{line_hits[name]:>7}{len(file_hits[name]):>7}")

    print("\n--- examples ---")
    for name, _ in sorted(line_hits.items(), key=lambda kv: -kv[1]):
        print(f"\n## {name}  ({line_hits[name]} lines / {len(file_hits[name])} files)")
        for ex in examples[name]:
            print(f"   {ex}")


if __name__ == "__main__":
    main()
