"""Extract single-combat logic from the recovered `sts2/` project into `sts2-core/`.

Design
------
Source-level, faithful extraction (not a compiling port). For every file that belongs to the
single-combat logic core we:

  1. copy it to ``sts2-core/`` mirroring its path below ``sts2/src/``,
  2. drop ``using`` directives for removed namespaces (Nodes / Audio / Animation / Bindings /
     ControllerInput / TextEffects / RichTextTags / netcode Multiplayer),
  3. delete statement lines that only drive presentation (SFX, VFX, music, banners, FTUE,
     screen shake, animation triggers, scaled pacing waits),
  4. strip presentation-only fluent chain calls from otherwise-gameplay expressions
     (``.WithHitFx(...)``, ``.WithAttackerAnim(...)``, ...) using balanced-paren scanning,
  5. delete whole presentation-only members (``GenerateAnimator``, ``TakeDamageSfxType``,
     ``ExtraDeathVfxPadding``, ...) using brace matching,

and it records every edit in ``CHANGES.tsv`` plus a per-file summary.

Files that are *entirely* presentation or netcode are never copied at all.

Usage:  python extract.py [--check] [--only <substr>]
"""

from __future__ import annotations

import argparse
import os
import re
import sys
from collections import Counter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
REPO = os.path.dirname(ROOT)
SRC = os.path.join(REPO, "sts2", "src")
OUT = os.path.join(REPO, "sts2-core")
TOOLS = os.path.join(REPO, "tools", "sts2-core-extract")

# --------------------------------------------------------------------------------------
# 1. What gets copied
# --------------------------------------------------------------------------------------

# Directories (relative to sts2/src) that are copied wholesale, subject to per-file excludes.
INCLUDE_DIRS = {
    "Core/Combat",
    "Core/Hooks",
    "Core/Random",
    "Core/ValueProps",
    "Core/CardSelection",
    "Core/MonsterMoves",
    "Core/Entities",
    "Core/Commands",
    "Core/GameActions",
    "Core/Models",
    "Core/Helpers",
    "Core/Exceptions",
    "Core/Extensions",
    "Core/Localization",  # DynamicVars / model text are part of model semantics
}

# Individual files outside INCLUDE_DIRS that the combat logic needs.
INCLUDE_FILES = {
    "Core/Modding/ModHelper.cs",
    "Core/Modding/Mod.cs",
    "Core/Modding/IMod.cs",
}

# Subtrees that are presentation / netcode / out-of-scope content: never copied.
EXCLUDE_DIR_PREFIXES = (
    "Core/Entities/Multiplayer",
    "Core/Entities/UI",
    "Core/Entities/Ancients",
    "Core/Entities/Merchant",
    "Core/Entities/RestSite",
    "Core/Entities/Rewards",
    "Core/Entities/Gold",
    "Core/Entities/TreasureRelicPicking",
    "Core/Entities/Text",
    "Core/Entities/Intents",          # IntentAnimData: animation metadata
    "Core/GameActions/Multiplayer",   # netcode queue/synchronizer/NetCombatCardDb
    "Core/Models/Relics",             # relic *content* out of scope (base class kept)
    "Core/Models/Potions",
    "Core/Models/RelicPools",
    "Core/Models/PotionPools",
    "Core/Models/Events",
    "Core/Models/Acts",
    "Core/Models/Achievements",
    "Core/Models/Badges",
    "Core/Models/Modifiers",
    "Core/Models/Characters",         # character visual/animation definitions
    "Core/Models/Singleton",
    "Core/Models/Exceptions",
)

# Individual files excluded even though their directory is included.
EXCLUDE_FILES = {
    # pure presentation / audio / animation / UI helpers
    "Core/Models/CharacterModel.cs",   # visual + animator definition for playable characters
    "Core/Models/BestiaryMonsterMove.cs",
    # netcode action wrappers
    *{f"Core/GameActions/Net{f}.cs" for f in (
        "DiscardPotionGameAction", "EndPlayerTurnAction", "MoveToMapCoordAction",
        "PickRelicAction", "PlayCardAction", "ReadyToBeginEnemyTurnAction",
        "UndoEndPlayerTurnAction", "UsePotionAction", "VoteForMapCoordAction",
        "VoteToMoveToNextActAction",
    )},
    # out-of-combat / map / reward actions
    "Core/GameActions/MoveToMapCoordAction.cs",
    "Core/GameActions/PickRelicAction.cs",
    "Core/GameActions/VoteForMapCoordAction.cs",
    "Core/GameActions/VoteToMoveToNextActAction.cs",
    # presentation-only commands
    "Core/Commands/SfxCmd.cs",
    "Core/Commands/VfxCmd.cs",
    "Core/Commands/TalkCmd.cs",
    "Core/Commands/ThinkCmd.cs",
    "Core/Commands/MapCmd.cs",
    "Core/Commands/RewardsCmd.cs",
    "Core/Commands/RelicSelectCmd.cs",
    # presentation-only entities
    "Core/Entities/Creatures/HpDisplay.cs",
    "Core/Entities/Creatures/HpDisplayExtensions.cs",
    "Core/Entities/Cards/CardPreviewMode.cs",
    "Core/Entities/Cards/CardCostColor.cs",
    "Core/Entities/Potions/PotionOverlay.cs",
    "Core/Entities/Enchantments/EnchantmentOption.cs",
    "Core/Entities/CardRewardAlternatives/CardRewardAlternative.cs",
    # presentation-only helpers (tweening, node-tree scrolling, spine bindings, colours)
    "Core/Helpers/Ease.cs",
    "Core/Helpers/GodotTreeExtensions.cs",
    "Core/Helpers/HandLayoutHelper.cs",
    "Core/Helpers/HandPosHelper.cs",
    "Core/Helpers/SpineNodeExtensions.cs",
    "Core/Helpers/ScrollHelper.cs",
    "Core/Helpers/StsColors.cs",
    "Core/Helpers/OneTimeInitialization.cs",
    "Core/Helpers/CommandLineHelper.cs",
    # asset-path plumbing with no rules content
    "Core/Helpers/ImageHelper.cs",
    "Core/Helpers/SceneHelper.cs",
    "Core/Helpers/EnergyIconHelper.cs",
}

# Netcode player-choice plumbing lives in `GameActions/Multiplayer` but is *not* netcode:
# these are the effect-resolution contexts used by every OnPlay signature in single player.
KEEP_FROM_GAMEACTIONS_MULTIPLAYER = {
    "Core/GameActions/Multiplayer/PlayerChoiceContext.cs",
    "Core/GameActions/Multiplayer/ThrowingPlayerChoiceContext.cs",
    "Core/GameActions/Multiplayer/BlockingPlayerChoiceContext.cs",
    "Core/GameActions/Multiplayer/PlayerChoiceResult.cs",
    "Core/GameActions/Multiplayer/PlayerChoiceContinuation.cs",
    "Core/GameActions/Multiplayer/PlayerChoiceDelegate.cs",
    "Core/GameActions/Multiplayer/GameActionPlayerChoiceContext.cs",
    "Core/GameActions/Multiplayer/HookPlayerChoiceContext.cs",
    "Core/GameActions/Multiplayer/ActionQueueSet.cs",
    "Core/GameActions/Multiplayer/ActionTypes.cs",
    "Core/GameActions/Multiplayer/PlayerChoiceSynchronizer.cs",
}

# --------------------------------------------------------------------------------------
# 2. Presentation namespaces -> drop the `using` line
# --------------------------------------------------------------------------------------

# Namespaces that only carry presentation or netcode. `Core.GameActions.Multiplayer` is
# deliberately absent: it holds the player-choice plumbing every OnPlay signature needs.
DROP_USING = re.compile(
    r"^\s*using\s+(?:static\s+)?(?:"
    r"MegaCrit\.Sts2\.Core\.Nodes(?:\.\w+)*"
    r"|MegaCrit\.Sts2\.Core\.Audio(?:\.\w+)*"
    r"|MegaCrit\.Sts2\.Core\.Animation(?:\.\w+)*"
    r"|MegaCrit\.Sts2\.Core\.Bindings(?:\.\w+)*"
    r"|MegaCrit\.Sts2\.Core\.ControllerInput(?:\.\w+)*"
    r"|MegaCrit\.Sts2\.Core\.TextEffects(?:\.\w+)*"
    r"|MegaCrit\.Sts2\.Core\.RichTextTags(?:\.\w+)*"
    r"|MegaCrit\.Sts2\.Core\.HoverTips(?:\.\w+)*"
    r"|MegaCrit\.Sts2\.Core\.Multiplayer(?:\.\w+)*"
    r")\s*;",
    re.MULTILINE,
)

# --------------------------------------------------------------------------------------
# 3. Statement lines that exist only to drive presentation
# --------------------------------------------------------------------------------------

STATEMENT_RULES: list[tuple[str, str]] = [
    ("anim-trigger", r"^\s*await\s+CreatureCmd\.TriggerAnim\s*\("),
    ("anim-trigger", r"^\s*await\s+TriggerAnim\s*\("),
    ("anim-trigger", r"^\s*TriggerAnim\s*\("),
    ("sfx", r"^\s*SfxCmd\.\w+\s*\("),
    ("sfx", r"^\s*await\s+SfxCmd\.\w+\s*\("),
    ("vfx", r"^\s*VfxCmd\.\w+\s*\("),
    ("vfx", r"^\s*await\s+VfxCmd\.\w+\s*\("),
    ("music", r"^\s*NRunMusicController\b"),
    ("audio", r"^\s*NAudioManager\b"),
    ("audio", r"^\s*NDebugAudioManager\b"),
    ("ui-node", r"^\s*NCombatRoom\b"),
    ("ui-node", r"^\s*NMapScreen\b"),
    ("ui-node", r"^\s*NModalContainer\b"),
    ("ui-node", r"^\s*NBestiary\b"),
    ("ui-node", r"^\s*NRun\b"),
    ("ui-node", r"^\s*NGame\b"),
    ("ui-node", r"^\s*NHoverTipSet\b"),
    ("ui-node", r"^\s*NTargetManager\b"),
    ("ui-node", r"^\s*NPlayerHand\b"),
    ("ui-node", r"^\s*NCombatUi\b"),
    ("ui-node", r"^\s*NSelectedHandCardContainer\b"),
    ("ui-node", r"^\s*NCard\b"),
    ("ui-node", r"^\s*NCreature\b"),
    ("ui-node", r"^\s*NPile\b"),
    ("ui-node", r"^\s*NPower\b"),
    ("ui-node", r"^\s*NIntent\b"),
    ("pacing", r"^\s*await\s+Cmd\.CustomScaledWait\s*\("),
    ("pacing", r"^\s*await\s+Cmd\.Wait\s*\("),
    ("pacing", r"^\s*await\s+Task\.Delay\s*\("),
    ("vfx", r"^\s*\w+\.GetVfxContainer\(\)"),
    ("vfx", r"^\s*\w+\.GetBackVfxContainer\(\)"),
    ("anim", r"^\s*\w+\.RefreshIntents\(\)"),
    ("anim", r"^\s*\w+\.StartDeathAnim\s*\("),
    ("anim", r"^\s*\w+\.PerformIntent\s*\("),
    ("anim", r"^\s*await\s+\w+\.PerformIntent\s*\("),
    ("anim", r"^\s*\w+\.AddChildSafely\s*\("),
    ("anim", r"^\s*\w+\.AnimMultiCardPlay\s*\("),
    ("anim", r"^\s*await\s+\w+\.AnimMultiCardPlay\s*\("),
    ("anim", r"^\s*await\s+\w+\.PlayAnim\s*\("),
    ("vfx", r"^\s*foreach\s*\(\s*Func<Node2D>\s"),
    ("vfx", r"^\s*foreach\s*\(\s*Func<Creature,\s*Node2D>\s"),
    ("vfx", r"^\s*\w+\.GlobalPosition\s*="),
    ("vfx", r"^\s*\w+\.UpdateVisuals\s*\("),
    ("anim", r"^\s*Tween\s+\w+\s*="),
    ("anim", r"^\s*\w+\.CreateTween\(\)"),
    ("anim", r"^\s*tween\b"),
]

# Inline / multi-line balanced-paren removals: presentation calls embedded in gameplay code.
# `AttackCommand`'s fluent builder surface is entirely presentational, so the whole family is
# stripped from otherwise-gameplay attack chains. `WithHitCount` is deliberately absent: it
# changes how many times the attack hits, which is a rule.
CHAIN_CALLS = [
    "WithAttackerAnim", "WithNoAttackerAnim", "AfterAttackerAnim",
    "WithTargetAnim", "WithCastAnim",
    "WithHitFx", "WithAttackFx", "WithAttackerFx", "WithTargetFx", "WithCastFx",
    "WithHitVfx", "WithHitVfxNode", "WithAttackerVfx", "WithVfx", "WithSfx",
    "WithDeathFx", "WithStartFx",
    "SpawningHitVfxOnEachCreature", "WithHitVfxSpawnedAtBase", "OnlyPlayAnimOnce",
    "WithWaitBeforeHit", "WithAttackerVfxNode",
]

# Methods on these types are called as `await CreatureCmd.TriggerAnim(...)` style statements;
# kept separate so they can also be removed mid-expression if they ever appear there.
ANIM_CMD_CALLS = ["TriggerAnim", "PlayVfx", "PlayAnim"]

# --------------------------------------------------------------------------------------
# 4. Whole members (properties/methods) that exist only for presentation
# --------------------------------------------------------------------------------------

MEMBER_PATTERNS = [
    r"(?:public|protected|private|internal)\s+(?:static\s+|virtual\s+|override\s+|abstract\s+|sealed\s+|async\s+)*[\w<>?\[\],\.\s]+?\s+GenerateAnimator\s*\(",
    r"(?:public|protected|private|internal)\s+(?:static\s+|virtual\s+|override\s+|abstract\s+|sealed\s+|async\s+)*[\w<>?\[\],\.\s]+?\s+SetupSkins\s*\(",
    r"(?:public|protected|private|internal)\s+(?:static\s+|virtual\s+|override\s+|abstract\s+|sealed\s+|async\s+)*[\w<>?\[\],\.\s]+?\s+OnPhobiaModeToggled\s*\(",
    r"(?:public|protected|private|internal)\s+(?:static\s+|virtual\s+|override\s+|abstract\s+|sealed\s+|async\s+)*[\w<>?\[\],\.\s]+?\s+PlayPowerCardFlyVfx\s*\(",
    r"(?:public|protected|private|internal)\s+(?:static\s+|virtual\s+|override\s+|abstract\s+|sealed\s+|async\s+)*[\w<>?\[\],\.\s]+?\s+OnEnqueuePlayVfx\s*\(",
    r"(?:public|protected|private|internal)\s+(?:static\s+|virtual\s+|override\s+|abstract\s+|sealed\s+|async\s+)*[\w<>?\[\],\.\s]+?\s+TriggerAnim\s*\(",
    r"(?:public|protected|private|internal)\s+(?:static\s+|virtual\s+|override\s+|abstract\s+|sealed\s+|async\s+)*[\w<>?\[\],\.\s]+?\s+GetCreatureNode\s*\(",
    r"(?:public|protected|private|internal)\s+(?:static\s+|virtual\s+|override\s+|abstract\s+|sealed\s+|async\s+)*[\w<>?\[\],\.\s]+?\s+GetVfxContainer\s*\(",
    r"(?:public|protected|private|internal)\s+(?:static\s+|virtual\s+|override\s+|abstract\s+|sealed\s+|async\s+)*[\w<>?\[\],\.\s]+?\s+GetBackVfxContainer\s*\(",
    # presentation-only properties
    r"(?:public|protected|private|internal)\s+(?:static\s+|virtual\s+|override\s+|abstract\s+|sealed\s+)*(?:[\w<>?\[\],\.]+)\s+TakeDamageSfxType\b",
    r"(?:public|protected|private|internal)\s+(?:static\s+|virtual\s+|override\s+|abstract\s+|sealed\s+)*(?:[\w<>?\[\],\.]+)\s+TakeDamageSfx\b",
    r"(?:public|protected|private|internal)\s+(?:static\s+|virtual\s+|override\s+|abstract\s+|sealed\s+)*(?:[\w<>?\[\],\.]+)\s+AttackSfx\b",
    r"(?:public|protected|private|internal)\s+(?:static\s+|virtual\s+|override\s+|abstract\s+|sealed\s+)*(?:[\w<>?\[\],\.]+)\s+CastSfx\b",
    r"(?:public|protected|private|internal)\s+(?:static\s+|virtual\s+|override\s+|abstract\s+|sealed\s+)*(?:[\w<>?\[\],\.]+)\s+DeathSfx\b",
    r"(?:public|protected|private|internal)\s+(?:static\s+|virtual\s+|override\s+|abstract\s+|sealed\s+)*(?:[\w<>?\[\],\.]+)\s+HasDeathSfx\b",
    r"(?:public|protected|private|internal)\s+(?:static\s+|virtual\s+|override\s+|abstract\s+|sealed\s+)*(?:[\w<>?\[\],\.]+)\s+HurtSfx\b",
    r"(?:public|protected|private|internal)\s+(?:static\s+|virtual\s+|override\s+|abstract\s+|sealed\s+)*(?:[\w<>?\[\],\.]+)\s+HasHurtSfx\b",
    r"(?:public|protected|private|internal)\s+(?:static\s+|virtual\s+|override\s+|abstract\s+|sealed\s+)*(?:[\w<>?\[\],\.]+)\s+ExtraDeathVfxPadding\b",
    r"(?:public|protected|private|internal)\s+(?:static\s+|virtual\s+|override\s+|abstract\s+|sealed\s+)*(?:[\w<>?\[\],\.]+)\s+DeathAnimLengthOverride\b",
    r"(?:public|protected|private|internal)\s+(?:static\s+|virtual\s+|override\s+|abstract\s+|sealed\s+)*(?:[\w<>?\[\],\.]+)\s+HasDeathAnimLengthOverride\b",
    r"(?:public|protected|private|internal)\s+(?:static\s+|virtual\s+|override\s+|abstract\s+|sealed\s+)*(?:[\w<>?\[\],\.]+)\s+ShouldPlayVfx\b",
    r"(?:public|protected|private|internal)\s+(?:static\s+|virtual\s+|override\s+|abstract\s+|sealed\s+)*(?:[\w<>?\[\],\.]+)\s+ShouldPlayAnimation\b",
    r"(?:public|protected|private|internal)\s+(?:static\s+|virtual\s+|override\s+|abstract\s+|sealed\s+)*(?:[\w<>?\[\],\.]+)\s+AttackAnimDelay\b",
    r"(?:public|protected|private|internal)\s+(?:static\s+|virtual\s+|override\s+|abstract\s+|sealed\s+)*(?:[\w<>?\[\],\.]+)\s+CastAnimDelay\b",
    r"(?:public|protected|private|internal)\s+(?:static\s+|virtual\s+|override\s+|abstract\s+|sealed\s+)*(?:[\w<>?\[\],\.]+)\s+PowerUpAnimDelay\b",
    # tooltip metadata: declarative presentation, no effect on combat resolution
    r"(?:public|protected|private|internal)\s+(?:static\s+|virtual\s+|override\s+|abstract\s+|sealed\s+)*(?:[\w<>?\[\],\.]+)\s+ExtraHoverTips\b",
    r"(?:public|protected|private|internal)\s+(?:static\s+|virtual\s+|override\s+|abstract\s+|sealed\s+)*(?:[\w<>?\[\],\.]+)\s+CanonicalHoverTips\b",
    r"(?:public|protected|private|internal)\s+(?:static\s+|virtual\s+|override\s+|abstract\s+|sealed\s+)*(?:[\w<>?\[\],\.]+)\s+HoverTips\b",
    r"(?:public|protected|private|internal)\s+(?:static\s+|virtual\s+|override\s+|abstract\s+|sealed\s+)*(?:[\w<>?\[\],\.]+)\s+GetHoverTips\s*\(",
    # Godot asset / visual-scene plumbing: pure presentation
    r"(?:public|protected|private|internal)\s+(?:static\s+|virtual\s+|override\s+|abstract\s+|sealed\s+)*(?:[\w<>?\[\],\.]+)\s+ExtraRunAssetPaths\b",
    r"(?:public|protected|private|internal)\s+(?:static\s+|virtual\s+|override\s+|abstract\s+|sealed\s+)*(?:[\w<>?\[\],\.]+)\s+ExtraAssetPaths\b",
    r"(?:public|protected|private|internal)\s+(?:static\s+|virtual\s+|override\s+|abstract\s+|sealed\s+)*(?:[\w<>?\[\],\.]+)\s+CreateVisuals\s*\(",
    r"(?:public|protected|private|internal)\s+(?:static\s+|virtual\s+|override\s+|abstract\s+|sealed\s+)*(?:[\w<>?\[\],\.]+)\s+VisualsPath\b",
    # bestiary browsing metadata: needs the (non-extracted) BestiaryMonsterMove type
    r"(?:public|protected|private|internal)\s+(?:static\s+|virtual\s+|override\s+|abstract\s+|sealed\s+)*(?:[\w<>?\[\],\.]+)\s+GenerateBestiaryMoveList\s*\(",
    r"(?:public|protected|private|internal)\s+(?:static\s+|virtual\s+|override\s+|abstract\s+|sealed\s+)*(?:[\w<>?\[\],\.]+)\s+ShouldShowMoveInBestiary\b",
    # icon / portrait / material / background asset paths
    r"(?:public|protected|private|internal)\s+(?:static\s+|virtual\s+|override\s+|abstract\s+|sealed\s+)*(?:[\w<>?\[\],\.]+)\s+(?:Icon|IconOutline|BigIcon|PackedIconPath|IconPath|MissingIconPath|IconBaseName|BigBetaIconPath|PackedIconOutlinePath|BigIconPath|ResolvedBigIconPath|PortraitPath|BetaPortraitPath|MissingPortraitPath|PackedImagePath|PackedOutlinePath|ImagePath|OutlinePath|SpritePath|VfxPath|TrailPath|MerchantAnimPath|RestSiteAnimPath|RestSiteBackgroundPath|BackgroundScenePath|MapTopBgPath|MapMidBgPath|MapBotBgPath|MapTopBg|MapMidBg|MapBotBg|BossNodePath|LayoutScenePath|ScenePath|FrameMaterialPath|FrameMaterial|Frame|BannerTexture|BannerMaterial|Portrait|PortraitBorder|EnergyIcon|EnergyIconPath|EnergyOutlineColor|DeckEntryCardColor|DarkenedColor|MapBgColor|MapTraveledColor|MapUntraveledColor|SpeechBubbleColor|ButtonColor|DialogueColor|EventButtonColor|MapIcon|MapIconOutline|MapIconPath|MapIconOutlinePath|RunHistoryIcon|RunHistoryIconOutline|RunHistoryIconOutlinePath|MapNodeAssetPaths|RunAssetPaths|InitialPortraitPath|InitialPhobiaModePortraitPath|ChestSpineResource|ChestSpineResourcePath|ChestSpineSkinNameNormal|ChestSpineSkinNameStroke|AncientBorder|AncientTextBg)\b",
    r"(?:public|protected|private|internal)\s+(?:static\s+|virtual\s+|override\s+|abstract\s+|sealed\s+)*(?:[\w<>?\[\],\.]+)\s+(?:HasPortrait|HasBetaPortrait|HasVfx|HasOverlay|HasPhobiaModePortrait|PreviewOutsideOfCombat|ShowAmount|DisplayAmount)\b",
    r"(?:public|protected|private|internal)\s+(?:static\s+|virtual\s+|override\s+|abstract\s+|sealed\s+)*(?:[\w<>?\[\],\.]+)\s+(?:CreateVfx|CreateSprite|CreateScene|CreateBackground|CreateBackgroundScene|CreateCombatRoomVisuals|CreateBackgroundAssetsForCustom|CreateInitialPortrait|CreateInitialPhobiaModePortrait|GetAssetPaths|GetBackgroundAssets|GenerateBackgroundAssets|GetAllBackgroundLayerPaths|GetFullLayerPath)\s*\(",
]
MEMBER_RE = re.compile("|".join(f"(?:{p})" for p in MEMBER_PATTERNS))

# --------------------------------------------------------------------------------------
# Helpers
# --------------------------------------------------------------------------------------


def find_matching_paren(text: str, open_idx: int) -> int:
    """Index just past the ')' matching the '(' at open_idx. Handles nesting, strings, chars."""
    assert text[open_idx] == "("
    depth = 0
    i = open_idx
    n = len(text)
    while i < n:
        ch = text[i]
        if ch == '"':
            i += 1
            while i < n and text[i] != '"':
                if text[i] == "\\":
                    i += 1
                i += 1
        elif ch == "'":
            i += 1
            while i < n and text[i] != "'":
                if text[i] == "\\":
                    i += 1
                i += 1
        elif ch == "(":
            depth += 1
        elif ch == ")":
            depth -= 1
            if depth == 0:
                return i + 1
        i += 1
    raise ValueError("unbalanced parentheses")


def remove_chain_calls(line: str, report: list[tuple[str, int, str]]) -> str:
    """Remove presentation-only fluent calls (e.g. ``.WithHitFx("a/b")``) from one line."""
    changed = True
    while changed:
        changed = False
        for name in CHAIN_CALLS:
            m = re.search(r"\." + name + r"\s*\(", line)
            if not m:
                continue
            open_idx = line.index("(", m.start())
            try:
                end = find_matching_paren(line, open_idx)
            except ValueError:
                continue
            # also swallow a preceding newline/indent if the call starts the (continuation) stmt
            start = m.start()
            line = line[:start] + line[end:]
            report.append(("chain:" + name, 0, ""))
            changed = True
    return line


def member_span(lines: list[str], start: int) -> int:
    """Return the exclusive end index of the member declared at `lines[start]`.

    Handles both block bodies (``{ ... }``) and expression bodies (``=> expr;``).
    """
    text = "".join(lines[start:])
    # expression-bodied member or field: end at first ';' that is not inside parens/brackets
    brace = text.find("{")
    semi = text.find(";")
    if semi != -1 and (brace == -1 or semi < brace):
        consumed = 0
        for idx in range(start, len(lines)):
            consumed += len(lines[idx])
            if consumed > semi:
                return idx + 1
        return len(lines)
    if brace == -1:
        return start + 1
    depth = 0
    consumed = 0
    for idx in range(start, len(lines)):
        for ch in lines[idx]:
            if ch == "{":
                depth += 1
            elif ch == "}":
                depth -= 1
        consumed += len(lines[idx])
        if depth == 0 and consumed > brace:
            # include a trailing blank line for tidiness
            end = idx + 1
            if end < len(lines) and lines[end].strip() == "":
                end += 1
            return end
    return len(lines)


# --------------------------------------------------------------------------------------
# Main transform
# --------------------------------------------------------------------------------------


def transform(rel: str, text: str, changes: list[tuple[str, str, int, str]]) -> str:
    lines = text.splitlines(keepends=True)
    out: list[str] = []
    i = 0
    report: list[tuple[str, int, str]] = []
    while i < len(lines):
        line = lines[i]

        if DROP_USING.match(line):
            report.append(("using", i + 1, line.strip()))
            i += 1
            continue

        if MEMBER_RE.match(line.strip()) and not line.strip().startswith("//"):
            end = member_span(lines, i)
            report.append(("member", i + 1, line.strip()[:100]))
            i = end
            continue

        matched = None
        for name, pat in STATEMENT_RULES:
            if re.match(pat, line):
                matched = name
                break
        if matched:
            report.append(("stmt:" + matched, i + 1, line.strip()[:100]))
            i += 1
            continue

        new_line = remove_chain_calls(line, report)
        out.append(new_line)
        i += 1

    # collapse 3+ consecutive blank lines left behind by deletions
    result: list[str] = []
    blanks = 0
    for ln in out:
        if ln.strip() == "":
            blanks += 1
            if blanks > 2:
                continue
        else:
            blanks = 0
        result.append(ln)

    for kind, lineno, snippet in report:
        changes.append((rel, kind, lineno, snippet))
    return "".join(result)


def collect() -> list[str]:
    files: list[str] = []
    for dirpath, _dirnames, filenames in os.walk(SRC):
        for fn in filenames:
            if not fn.endswith(".cs"):
                continue
            full = os.path.join(dirpath, fn)
            rel = os.path.relpath(full, SRC).replace("\\", "/")
            if rel.startswith("Core/"):
                rel = rel
            include = False
            for d in INCLUDE_DIRS:
                if rel.startswith(d + "/"):
                    include = True
                    break
            if rel in INCLUDE_FILES:
                include = True
            if rel.startswith("Core/GameActions/Multiplayer/") and rel in KEEP_FROM_GAMEACTIONS_MULTIPLAYER:
                include = True
            if not include:
                continue
            if any(rel.startswith(p) for p in EXCLUDE_DIR_PREFIXES):
                continue
            if rel in EXCLUDE_FILES:
                continue
            files.append(rel)
    return sorted(files)


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true", help="report only, do not write")
    ap.add_argument("--only", default=None, help="process only paths containing this substring")
    ap.add_argument("--out", default=None, help="override the output directory")
    args = ap.parse_args()

    global OUT
    if args.out:
        OUT = os.path.abspath(args.out)

    files = collect()
    if args.only:
        files = [f for f in files if args.only in f]

    changes: list[tuple[str, str, int, str]] = []
    written = 0
    for rel in files:
        src_path = os.path.join(SRC, rel.replace("/", os.sep))
        try:
            with open(src_path, "r", encoding="utf-8", errors="replace") as fh:
                text = fh.read()
        except OSError as exc:
            print(f"!! cannot read {rel}: {exc}", file=sys.stderr)
            continue
        new_text = transform(rel, text, changes)
        if args.check:
            continue
        dst_path = os.path.join(OUT, rel.replace("/", os.sep))
        os.makedirs(os.path.dirname(dst_path), exist_ok=True)
        with open(dst_path, "w", encoding="utf-8", newline="\n") as fh:
            fh.write(new_text)
        written += 1

    kinds = Counter(c[1].split(":")[0] for c in changes)
    print(f"files selected : {len(files)}")
    print(f"files written  : {written}")
    print(f"edits recorded : {len(changes)}")
    for k, v in kinds.most_common():
        print(f"  {k:<12} {v}")

    if not args.check:
        os.makedirs(TOOLS, exist_ok=True)
        with open(os.path.join(TOOLS, "CHANGES.tsv"), "w", encoding="utf-8", newline="\n") as fh:
            fh.write("file\tkind\tline\tsnippet\n")
            for rel, kind, lineno, snippet in changes:
                fh.write(f"{rel}\t{kind}\t{lineno}\t{snippet}\n")
        print(f"change log     : {os.path.join(TOOLS, 'CHANGES.tsv')}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
