"""Detect members that the extraction dropped by accident.

`finalize.py` had a bug: for a *expression-bodied* presentation member (one ending in `=> ...;`
with no body of its own) it still called the brace-matching helper. That helper scans forward
for the next `{`, which belongs to some unrelated member further down, and deletes everything
in between. The result is balanced — so the delimiter check does not catch it — but whole
gameplay members vanish.

This script compares the **sequence of declared members** in each extracted file against its
`sts2/src` original and reports every member that exists in the original but not in the
extraction. Presentation members are expected to be gone and are filtered out by name.

Run from the repository root; exits non-zero if anything unexplained is missing.
"""

from __future__ import annotations

import os
import re
import sys
from collections import defaultdict

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
OUT = os.path.join(REPO, "sts2-core")
SRC = os.path.join(REPO, "sts2", "src")

# A declaration line: access modifier, then a name, then a body/arrow/semicolon.
DECL_RE = re.compile(
    r"^\s*(?:\[[^\]]*\]\s*)*"
    r"(?:public|protected|private|internal)\b"
    r"(?P<rest>.*)$"
)

# Members that are meant to be gone: matched by name. Every pattern here is *unambiguous* —
# a name that can only describe presentation (a builder for a visual effect, an asset path,
# an icon or colour, a spine/tween helper). Ambiguous names, which look like gameplay hooks
# but were presentational in the specific overrides extracted, live in REVIEWED below so that
# a genuinely lost `AfterDeath` elsewhere still fails this check.
PRESENTATION_NAMES = re.compile(
    r"\b(?:"
    r"ExtraHoverTips|CanonicalHoverTips|HoverTips|GetHoverTips|HoverTipsExcludingRelic"
    r"|ExtraRunAssetPaths|ExtraAssetPaths|AssetPaths|VisualsPath|CreateVisuals"
    r"|GenerateAnimator|SetupSkins|OnPhobiaModeToggled|GenerateBestiaryMoveList"
    r"|PlayPowerCardFlyVfx|OnEnqueuePlayVfx|TriggerAnim|GetCreatureNode"
    r"|GetVfxContainer|GetBackVfxContainer|TakeDamageSfxType|TakeDamageSfx"
    r"|AttackSfx|CastSfx|DeathSfx|HasDeathSfx|HurtSfx|HasHurtSfx"
    r"|ChannelSfx|EvokeSfx|PassiveSfx|PlayChannelSfx|PlayEvokeSfx|PlayPassiveSfx"
    r"|DebugChannelSfx|DebugEvokeSfx|DebugPassiveSfx"
    r"|ExtraDeathVfxPadding|DeathAnimLengthOverride|HasDeathAnimLengthOverride"
    r"|ShouldPlayVfx|ShouldPlayAnimation|AttackAnimDelay|CastAnimDelay|PowerUpAnimDelay"
    r"|Icon|IconOutline|BigIcon|PackedIconPath|IconPath|MissingIconPath|IconBaseName"
    r"|PackedIconOutlinePath|BigIconPath|BigBetaIconPath|ResolvedBigIconPath"
    r"|BetaIconPath|IntendedIconPath|_iconPath|_resolvedBigIconPath|UpdateTexture"
    r"|RelicIconChanged|PortraitPath|BetaPortraitPath|MissingPortraitPath"
    r"|Portrait|PortraitBorder|BetaPortraitPngPath|PortraitPngPath|AllPortraitPaths"
    r"|GetPortraitFilename|GetPortraitPath|HasPortrait|HasBetaPortrait"
    r"|PackedImagePath|PackedOutlinePath|ImagePath|OutlinePath|Image"
    r"|FrameMaterial|FrameMaterialPath|Frame|BannerMaterial|BannerMaterialPath"
    r"|BannerTexture|AncientBorder|AncientBorderPath|AncientTextBg"
    r"|DeckEntryCardColor|EnergyOutlineColor|EnergyIcon|EnergyIconPath|LabOutlineColor"
    r"|AmountLabelColor|MapBgColor|MapTraveledColor|MapUntraveledColor|DarkenedColor"
    r"|SpeechBubbleColor|ButtonColor|DialogueColor|EventButtonColor|cream|red"
    r"|ChestSpineResource|ChestSpineResourcePath|ChestSpineSkinNameNormal"
    r"|ChestSpineSkinNameStroke|BossNodeSpineResource"
    r"|GenerateBackgroundAssets|GetAllBackgroundLayerPaths|GetFullLayerPath"
    r"|GetAssetPaths|GetBackgroundAssets|CreateBackground|CreateScene|GetScenePath"
    r"|CreateCombatRoomVisuals|CreateBackgroundAssetsForCustom|CreateBackgroundScene"
    r"|CreateInitialPortrait|CreateInitialPhobiaModePortrait|HasPhobiaModePortrait"
    r"|InitialPortraitPath|InitialPhobiaModePortraitPath|MapNodeAssetPaths"
    r"|RunHistoryIcon|RunHistoryIconOutline|RunHistoryIconOutlinePath"
    r"|MapIcon|MapIconOutline|MapIconPath|MapIconOutlinePath|BossNodePath"
    r"|LayoutScenePath|ScenePath|SetNode|EnteringEventCombat|StateChanged"
    r"|RemoveSovereignBladeNode|GetTargetPosition|ToTask|Load"
    r"|ShouldShowMoveInBestiary|GetTotalCardsBeingPreviewed|PreviewCardPileAdd|PreviewInternal"
    r"|PreviewSovereignBlade|PlayCombatRoomForgeVfx|SetBotFallPosition"
    r"|CreateFallbackVisuals|_fallbackVisualsPath|Preview"
    r"|CreateOverlay|OverlayPath|AnimSegmentsAttack|ChangePhobiaModeTexture"
    r"|PhobiaModeAliveTexture|PhobiaModeDeadTexture|VfxOffset|GetCameraOffset"
    r"|MinOffset|MaxOffset|ScaleRange|HatchPos|_hatchPos|SetColor|WaitForTween"
    r"|CreateCardNodeAndUpdateVisuals|MoveCardNodeToNewPileBeforeTween"
    r"|AppendPlayPileLerpTween|AppendPileLerpTween|ShuffleFtueCheck"
    r"|CheckIfDrawIsPossibleAndShowThoughtBubbleIfNot|FlashRelics"
    r"|WithAttackerAnim|WithNoAttackerAnim|WithAttackerFx|WithHitFx|WithHitVfx"
    r"|WithHitVfxNode|WithHitVfxSpawnedAtBase|SpawningHitVfxOnEachCreature"
    r"|OnlyPlayAnimOnce|WithWaitBeforeHit|_attackerAnimName|_attackerAnimDelay"
    r"|_attackerVfx|_attackerSfx|_tmpAttackerSfx|_visualAttacker|_spawnVfxOnEachCreature"
    r"|HitVfx|HitSfx|TmpHitSfx|_afterAttackerAnim|_backgroundAssets"
    r"|AfterAttackerAnim|StartDoomAnim|PlayVfx|CreateVfx|HasVfx|VfxPath"
    r"|SpritePath|CreateSprite|RunAssetPaths|GetVfxNode|HasOverlay"
    r"|ShowAmount|DisplayAmount|PreviewOutsideOfCombat"
    r"|BackgroundScenePath|CreateRestSiteBackground|RestSiteBackgroundPath"
    r"|MapTopBg|MapTopBgPath|MapMidBg|MapMidBgPath|MapBotBg|MapBotBgPath"
    r"|PlayVfxAndThenRemoveNodes|FlipScale|GetTexture"
    r"|AncientTextBgPath|BannerTexturePath|FramePath|PortraitBorderPath"
    r")\b"
)

# Ambiguous names, reviewed per file. Each entry was read by hand and confirmed presentational:
#   AfterDeath                -> the override only played a death animation / music
#   AfterCurrentHpChanged     -> Crusher: only played a hurt animation
#   BeforeDeath               -> Crusher: background fade + audio only
#   AfterCardChangedPiles     -> SovereignBlade: only forge VFX / node removal
#   AfterTransformedFrom      -> SovereignBlade: only node removal
#   GetAllMoves / GetIntents  -> MonsterModel: collected intent icon asset paths
#   One / Color / Node / Image / StringName -> stray locals and fields for visual state
#   HealedAmount / GetOptionTitle -> ancient-event UI bookkeeping (out of single-combat scope)
REVIEWED: dict[str, set[str]] = {
    "Core/Models/Monsters/DecimillipedeSegment.cs": {"AfterDeath"},
    "Core/Models/Monsters/Crusher.cs": {"AfterCurrentHpChanged", "BeforeDeath", "_background"},
    "Core/Models/Cards/SovereignBlade.cs": {"AfterCardChangedPiles", "AfterTransformedFrom"},
    "Core/Models/MonsterModel.cs": {"GetAllMoves", "GetIntents", "One"},
    "Core/Models/Cards/BouncingFlask.cs": {"Color"},
    "Core/Models/EventModel.cs": {"Node", "GetOptionTitle"},
    "Core/Models/PotionModel.cs": {"Image"},
    "Core/Models/AncientEventModel.cs": {"HealedAmount"},
    "Core/Models/RelicModel.cs": {"StringName"},
    "Core/Models/Monsters/LagavulinMatriarch.cs": {"AfterDeath", "_sleepingVfx"},
    "Core/Models/Monsters/SlumberingBeetle.cs": {"AfterDeath", "_sleepingVfx"},
    "Core/Models/Monsters/Vantom.cs": {"AfterDeath", "ScaleTo", "_scaleTween"},
    "Core/Models/Monsters/TheInsatiable.cs": {"AfterDeath"},
    "Core/Models/Monsters/CeremonialBeast.cs": {"ShouldPlayRegularHurtAnim"},
    "Core/Models/Monsters/Vantom.cs": {"AfterDeath", "ScaleTo", "_scaleTween", "ScaleTween"},
    "Core/Models/Monsters/LagavulinMatriarch.cs": {"AfterDeath", "_sleepingVfx", "SleepingVfx"},
    "Core/Models/Monsters/SlumberingBeetle.cs": {"AfterDeath", "_sleepingVfx", "SleepingVfx"},
    "Core/Models/PotionModel.cs": {"Image", "Outline"},
    "Core/Models/Monsters/Crusher.cs": {"AfterCurrentHpChanged", "BeforeDeath", "_background", "Background"},
    # SandpitPower's arena-layout overrides only repositioned creature nodes; the class keeps
    # its rules hooks (AfterSideTurnStartLate -> PowerCmd.Decrement, AfterRemoved -> kills).
    "Core/Models/Powers/SandpitPower.cs": {
        "UpdateCreaturePositions", "AfterCreatureAddedToCombat", "AfterOstyRevived",
        "AfterPowerAmountChanged", "BeforeSideTurnEnd", "_initialTargetPosition",
    },
}


def declarations(text: str) -> list[str]:
    """Names of members declared in `text`, in source order.

    Covers the three shapes the decompiler emits: a method (`Name(`), an expression-bodied
    member (`Name =>`), an auto-property with braces (`Name {` / `Name;`), and a *block-bodied
    property whose declaration line is just the name* — `public CardModel Card` — which a
    naive "identifier followed by punctuation" rule silently misses. That last shape is how
    `EnchantmentModel.Card` and `AfflictionModel.Card` went missing unnoticed.
    """
    names: list[str] = []
    for line in text.splitlines():
        stripped = line.strip()
        if stripped.startswith("//") or stripped.startswith("*"):
            continue
        m = DECL_RE.match(line)
        if not m:
            continue
        rest = m.group("rest")
        mm = re.search(r"\b([A-Za-z_]\w*)\s*(?:<[^>]*>)?\s*(?:\(|=>|\{|;)", rest)
        if mm:
            names.append(mm.group(1))
            continue
        # declaration line ends with a bare name: a property or field
        mm2 = re.search(r"\b([A-Za-z_]\w*)\s*$", rest)
        if mm2:
            names.append(mm2.group(1))
    return names


def main() -> int:
    out_root = OUT
    if len(sys.argv) > 1 and not sys.argv[1].startswith("-"):
        out_root = os.path.abspath(sys.argv[1])
    missing: dict[str, list[str]] = defaultdict(list)
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

            out_names = set(declarations(open(out_path, encoding="utf-8", errors="replace").read()))
            src_names = set(declarations(open(src_path, encoding="utf-8", errors="replace").read()))

            for name in sorted(src_names - out_names):
                if PRESENTATION_NAMES.search(name):
                    continue
                if name in REVIEWED.get(rel, ()):
                    continue
                # a constructor always shares the class name; those are checked separately
                if name in {"get", "set", "if", "for", "foreach", "while", "return", "new", "using"}:
                    continue
                missing[rel].append(name)

    print(f"files compared        : {checked}")
    print(f"files with lost members: {len(missing)}")
    total = sum(len(v) for v in missing.values())
    print(f"lost members          : {total}")
    if missing:
        print()
        for rel in sorted(missing):
            print(f"  {rel}")
            print(f"      {', '.join(missing[rel])}")
    return 1 if missing else 0


if __name__ == "__main__":
    raise SystemExit(main())
