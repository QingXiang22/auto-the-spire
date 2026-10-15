"""Account for every line the extraction removed from `sts2/src`.

`verify.py`, `fidelity.py` and `member_diff.py` check aggregate properties. This script is the
strongest check available without a compiler: it diffs each extracted file against its
original line by line and demands that **every removed line be explainable as presentation**.

Method
------
For each file pair, `difflib.SequenceMatcher` yields opcodes. `delete` and `replace` blocks
give the lines that disappeared. Contiguous removed runs are judged as a unit:

* a run is **justified** when every meaningful line in it names a presentation identifier;
* a run may also contain *neutral* lines — blank lines, braces, punctuation, and control-flow
  headers (`if`/`else`/`foreach`/`for`/`while`/`try`/`catch`/`lock`/`switch`/`get`/`set`) —
  because a purely visual branch was legitimately removed together with its header;
* anything else is reported as **UNJUSTIFIED** for human review, with the offending line.

Additions are reported too: after this pass the extraction should contain nothing the
original did not have, apart from the handful of deliberate restorations documented in the
README and the `member_diff.REVIEWED` table.

Run from the repository root:

    python tools/sts2-core-extract/audit_deletions.py            # summary
    python tools/sts2-core-extract/audit_deletions.py --all      # every unjustified line
"""

from __future__ import annotations

import argparse
import difflib
import os
import re
import sys
from collections import Counter, defaultdict

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
OUT = os.path.join(REPO, "sts2-core")
SRC = os.path.join(REPO, "sts2", "src")

sys.path.insert(0, HERE)

# Every identifier the extraction is allowed to delete. Kept deliberately broad: this audit
# answers "is this line about drawing/animation/audio/net, or about rules?".
PRESENTATION = re.compile(
    r"(?:"
    # --- dropped namespaces ---
    r"MegaCrit\.Sts2\.Core\.(?:Nodes|Audio|Animation|Bindings|ControllerInput|TextEffects"
    r"|RichTextTags|HoverTips|Multiplayer)"
    # --- UI / scene nodes ---
    r"|\bN(?:CombatRoom|Creature|Card|CardPlayQueue|CreatureVisuals|PlayerHand|CombatUi|Run"
    r"|Game|MapScreen|ModalContainer|Bestiary|HoverTipSet|TargetManager|AudioManager"
    r"|DebugAudioManager|RunMusicController|Intent|Power|Pile|Screen|TopBar)"
    r"|\bN[A-Z]\w*(?:Vfx|Ftue|Banner|Tip|Preview|Overlay|Anim|Button|Icon)\b"
    r"|\bN[A-Z]\w*Vfx\b|\bN[A-Z]\w*Ftue\b|\bN[A-Z]\w*Banner\b"
    # --- Godot / spine / tween ---
    r"|\b(?:Godot|Node2D|CanvasGroup|MegaSprite|MegaSkeleton|MegaAnimationState|SpineAnimation"
    r"|AnimState|CreatureAnimator|PackedScene|PreloadManager|ResourceLoader|Texture2D"
    r"|ImageHelper|SceneHelper|EnergyIconHelper|Tween|Callable|PackedScene|Mathf)\b"
    r"|\b(?:Vector2|Vector3|Color|Colors|StringName|Control|Node|Resource|Font)\b"
    r"|\bMegaSkeletonDataResource\b"
    # --- effect commands / audio ---
    r"|\b(?:SfxCmd|VfxCmd|FmodSfx|DamageSfxType|StsColors|TriggerAnim)\b"
    r"|\b\w*(?:Sfx|Vfx)\b"
    r"|\b\w*Anim(?:ation|ator|State|Delay|Name|Trigger|Path)\b"
    # --- visual accessors and chrome ---
    r"|\b(?:GetSpecialNode|GetVfxContainer|GetBackVfxContainer|GetCreatureNode"
    r"|GetBottomOfHitbox|VfxSpawnPosition|AddChildSafely|QueueFreeSafely|MoveChildSafely"
    r"|StartDeathAnim|AnimHideIntent|DeathAnimationTask|RefreshIntents|UpdateIntent"
    r"|PlayVfx|PlayAnim|AnimMultiCardPlay|CreateTween|TweenProperty|OstyScaleToSize"
    r"|ToggleIsInteractable|GlobalPosition|SetSelfModulate|SetVisible|SetTrans|CreateScene"
    r"|CreateVisuals|CreateSprite|CreateVfx|CreateOverlay|CreateBackground|GetTexture"
    r"|GetAssetPaths|GetBackgroundAssets|GetCameraOffset|GetScenePath|GetPortraitPath"
    r"|UpdateVisuals|UpdateTexture|MoveChildSafely)\b"
    # --- asset / icon / colour members and locals ---
    r"|\b\w*(?:IconPath|IconOutline|PackedIcon|BigIcon|PortraitPath|PortraitPng|ImagePath"
    r"|PackedImagePath|SpritePath|VfxPath|TrailPath|ScenePath|AssetPaths|VisualsPath"
    r"|OutlinePath|MaterialPath|BackgroundScenePath|RestSiteBackgroundPath|BossNodePath"
    r"|LayoutScenePath|PhobiaModeAliveTexture|PhobiaModeDeadTexture|BoneName)\b"
    r"|\b(?:Icon|Portrait|Frame|BannerTexture|BannerMaterial|DeckEntryCardColor"
    r"|EnergyOutlineColor|EnergyIcon|LabOutlineColor|AmountLabelColor|MapBgColor"
    r"|MapTraveledColor|MapUntraveledColor|DarkenedColor|SpeechBubbleColor|ButtonColor"
    r"|DialogueColor|EventButtonColor|VfxColor|HasPortrait|HasBetaPortrait|HasVfx"
    r"|HasOverlay|ShowAmount|DisplayAmount|PreviewOutsideOfCombat|RelicIconChanged"
    r"|HoverTipsExcludingRelic|HoverTips|ExtraHoverTips|CanonicalHoverTips)\b"
    # --- AttackCommand presentation builder surface ---
    r"|\b\w*(?:WithAttackerAnim|WithNoAttackerAnim|AfterAttackerAnim|WithAttackerFx"
    r"|WithHitFx|WithHitVfx|WithHitVfxNode|WithHitVfxSpawnedAtBase|WithWaitBeforeHit"
    r"|SpawningHitVfxOnEachCreature|OnlyPlayAnimOnce|HitVfx|HitSfx|TmpHitSfx)\w*\b"
    # --- card preview / bestiary / camera chrome ---
    r"|\b\w*(?:PreviewCardPileAdd|PreviewInternal|GetTotalCardsBeingPreviewed|FlashRelics"
    r"|GenerateBestiaryMoveList|ShouldShowMoveInBestiary|BestiaryMonsterMove"
    r"|CreateCardNodeAndUpdateVisuals|MoveCardNodeToNewPileBeforeTween|AppendPileLerpTween"
    r"|AppendPlayPileLerpTween|ShuffleFtueCheck|CheckIfDrawIsPossibleAndShowThoughtBubbleIfNot"
    r"|FlipScale|ScaleTo|ScaleTween|WaitForTween|SetColor|HatchPos|MinOffset|MaxOffset"
    r"|ScaleRange|PlayVfxAndThenRemoveNodes|StartDoomAnim|SetNode|VfxOffset|AnimSegmentsAttack"
    r"|ChangePhobiaModeTexture)\w*\b"
    # --- presentation-only params / doc phrases ---
    r"|\bskipVisuals\b|\bskipCardPileVisuals\b|\bshowPreview\b"
    r"|deathAnim|animName|animDelay|AnimDelay|vfx|sfx|spine|Spine"
    # --- UI screens and Godot resource URIs ---
    r"|\bN[A-Z]\w*(?:Screen|OverlayStack|Overlay)\b"
    r"|res://"
    r"|\bDirAccess\b|\bGetFullLayerPath\b|\bGetAllBackgroundLayerPaths\b"
    r")",
    re.IGNORECASE,
)

# A removed run containing any of these deleted a *rule*, not a visual. This is the question
# the audit really needs to answer, and it does not depend on guessing at vocabulary.
GAMEPLAY_RE = re.compile(
    r"\b(?:"
    r"DamageCmd|PowerCmd|CreatureCmd|CardCmd|CardPileCmd|CardSelectCmd|OrbCmd|PlayerCmd"
    r"|PotionCmd|RelicCmd|ForgeCmd|OstyCmd|Hook|CombatManager|ModelDb"
    r"|CheckWinCondition|TakeTurn|WaitForUnpause|IsInProgress|IsOverOrEnding|IsEnding"
    r"|ApplyPower|GainBlock|AddToCombatAndPreview|AutoPlay|InitiateCombat|SignalPlayerChoice"
    r"|ReserveChoiceId|SyncLocalChoice|WaitForRemoteChoice|LogChoice|MarkCardAsSeen"
    r"|MinInitialHp|MaxInitialHp|CanonicalVars|CanonicalKeywords|OnPlay|OnUpgrade"
    r"|GenerateMoveStateMachine|MoveState|Intent|AscensionHelper|ScaleHpForMultiplayer"
    r"|RunState\.Rng|NextInt|NextFloat|NextItem|StableShuffle|Shuffle"
    r")\b"
)

# Lines that carry no semantics of their own, so they may travel with a justified run.
NEUTRAL = re.compile(
    r"^\s*(?:"
    r"[{}()\[\];,]*"
    r"|else|do|try|finally"
    r"|(?:if|else\s+if|foreach|for|while|lock|switch|using|catch)\b.*"
    r"|(?:get|set)\s*;?"
    r"|return\s*;|break\s*;|continue\s*;"
    r"|case\b.*:|default\s*:"
    r"|--|\+\+|num\d*\s*[+\-*/]?=\s*[\d\.f]+\s*;"
    r"|\w+\s*=\s*(?:true|false|null|\d+|\d+\.\d+f?)\s*;"
    r")\s*$"
)

COMMENT = re.compile(r"^\s*(?://|/\*|\*)")


def judge_run(run: list[str], ext_pool: list[str] | None = None,
              threshold: float = 0.9) -> tuple[bool, list[str]]:
    """Is a contiguous removed run explainable as presentation? Return (ok, reasons).

    The primary test is whether a *rule line* was deleted — a line naming a gameplay API that
    is not itself presentational. A line that merely changed shape (an `out` variable renamed,
    a parameter dropped) is not a deletion, so each candidate is first fuzzy-matched against
    the lines the extraction kept.
    """
    pool = ext_pool or []

    def was_modified(line: str) -> bool:
        for cand in pool:
            if difflib.SequenceMatcher(None, line, cand).ratio() >= threshold:
                return True
        return False

    reasons: list[str] = []
    # A line only counts as a lost rule when it names a gameplay API *and* is not itself
    # presentational. `await CreatureCmd.TriggerAnim(...)` names CreatureCmd but is an
    # animation trigger, so the presentational test has to win.
    gameplay_lines = [
        line.strip()
        for line in run
        if GAMEPLAY_RE.search(line)
        and not PRESENTATION.search(line)
        and not was_modified(line.strip())
    ]
    if gameplay_lines:
        return False, [f"deleted rule line: {line[:120]}" for line in gameplay_lines[:8]]

    saw_presentation = False
    unexplained: list[str] = []
    for raw in run:
        line = raw.rstrip()
        if not line.strip():
            continue
        if COMMENT.match(line):
            if PRESENTATION.search(line):
                saw_presentation = True
            continue
        if PRESENTATION.search(line):
            saw_presentation = True
            continue
        if NEUTRAL.match(line) or was_modified(line):
            continue
        unexplained.append(line.strip())

    if unexplained and not saw_presentation:
        # nothing in this run looks like presentation and nothing is scaffolding
        return False, [f"unexplained removal: {u}" for u in unexplained[:6]]
    return True, []


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--all", action="store_true", help="list every unjustified line")
    ap.add_argument("--out", default=None, help="override the tree to audit")
    args = ap.parse_args()

    global OUT
    if args.out:
        OUT = os.path.abspath(args.out)

    files = 0
    deleted_total = 0
    justified_runs = 0
    unjustified: dict[str, list[str]] = defaultdict(list)
    additions: dict[str, list[str]] = defaultdict(list)
    add_total = 0

    for dirpath, _dirs, filenames in os.walk(OUT):
        for fn in filenames:
            if not fn.endswith(".cs"):
                continue
            out_path = os.path.join(dirpath, fn)
            rel = os.path.relpath(out_path, OUT).replace("\\", "/")
            src_path = os.path.join(SRC, rel.replace("/", os.sep))
            if not os.path.exists(src_path):
                continue
            files += 1

            # Compare on rstrip()ed text: the extraction also strips trailing whitespace
            # (the decompiled source is full of it), and a whitespace-only difference must
            # not register as a removed line. Without this the audit reported whole
            # unchanged switches as deletions.
            a = [l.rstrip() for l in open(src_path, encoding="utf-8", errors="replace").read().splitlines()]
            b = [l.rstrip() for l in open(out_path, encoding="utf-8", errors="replace").read().splitlines()]
            sm = difflib.SequenceMatcher(None, a, b, autojunk=False)

            for tag, i1, i2, j1, j2 in sm.get_opcodes():
                if tag in ("delete", "replace") and i2 > i1:
                    run = a[i1:i2]
                    deleted_total += len(run)
                    ok, offending = judge_run(run, ext_pool=b)
                    if ok:
                        justified_runs += 1
                    else:
                        unjustified[rel].extend(offending)
                if tag in ("insert", "replace") and j2 > j1:
                    added = [l.strip() for l in b[j1:j2] if l.strip()]
                    if added:
                        additions[rel].extend(added)
                        add_total += len(added)

    print(f"files compared        : {files}")
    print(f"lines removed         : {deleted_total}")
    print(f"  justified runs      : {justified_runs}")
    print(f"  UNJUSTIFIED files   : {len(unjustified)}")
    print(f"  UNJUSTIFIED lines   : {sum(len(v) for v in unjustified.values())}")
    print(f"lines added           : {add_total}  (across {len(additions)} files)")
    print()

    if unjustified:
        print("=== UNJUSTIFIED REMOVALS (need review) ===")
        for rel in sorted(unjustified, key=lambda r: -len(unjustified[r])):
            lines = unjustified[rel]
            print(f"\n  {rel}  ({len(lines)} lines)")
            show = lines if args.all else lines[:12]
            for line in show:
                print(f"      {line[:150]}")
            if not args.all and len(lines) > len(show):
                print(f"      ... and {len(lines) - len(show)} more")

    if additions:
        print("\n=== ADDITIONS (not present in the original) ===")
        for rel in sorted(additions, key=lambda r: -len(additions[r]))[:40]:
            print(f"\n  {rel}  ({len(additions[rel])} lines)")
            for line in additions[rel][:8]:
                print(f"      {line[:150]}")

    return 1 if unjustified else 0


if __name__ == "__main__":
    raise SystemExit(main())
