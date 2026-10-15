# ENV-T12 Ready Content Handoff — 2026-10-10

> 2026-10-11：本交接已并入主清单并归档至records；负责人复核见[本轮推进](../2026-10-11/parallel-main-progress-2026-10-11.md)，当前状态与下一包只查主清单。

## Status and scope

This handoff records the current ENV-T12 content slice after the ready-card scan. It is a dated implementation snapshot, not an independent Slay the Spire 2 parity claim. The complete source denominator remains **577 original single-player entries**; multiplayer entries and non-STS2 inventions were not added.

Production changes were restricted to card DSL files under `mod-the-spire/base-spire/BaseSpire/ContentSource/` and `mod-the-spire/base-spire/BaseSpire.Slice.Tests/RemainingCardFamiliesChecks.cs`.

## Source-backed cards added in this session

All entries below were taken from the corresponding STS2 source classes under `sts2/src/Core/Models/Cards/`, with normal/upgraded variants, pool, type, rarity, keywords, namespace, and supported effect order represented in DSL:

- Silent: `LegSweep`, `Finisher`, `Malaise`, `DaggerThrow`, `Survivor`, `Suppress`.
- Defect: `Leap`, `Supercritical`.
- Necrobinder: `Eradicate`, `Fear`, `Reap`, `Graveblast`.
- Regent: `CelestialMight`, `KnowThyPlace`, `LunarBlast`, `Glimmer`, `CosmicIndifference`, `PhotonCut`.
- Colorless: `Volley`, `Restlessness`, `ThinkingAhead`.
- Token/event content: `MinionStrike`, `MinionDiveBomb`, `MinionSacrifice`, `ByrdSwoop`, `Peck`, `RipAndTear`, `Squash`, `Exterminate`.

This is 29 source-backed card definitions in the current session. The earlier f9 five-card delivery was not redone.

## First action and source evidence

The first implementation action was to claim ENV-T12 in `docs/review/tofix/README.md` and inspect the package checklist, role guidance, and base development guide. Each card was checked against its actual STS2 class before DSL entry. Important source-order examples retained in the implementation are:

- `sts2/src/Core/Models/Cards/LegSweep.cs`: gain block 11/14, then Weak 2/3.
- `sts2/src/Core/Models/Cards/Finisher.cs`: repeat damage 6/8 by this-turn finished attack plays; the card itself is not counted.
- `sts2/src/Core/Models/Cards/Malaise.cs`: one Strength application and one Weak application from paid X, with upgraded offsets.
- `sts2/src/Core/Models/Cards/Survivor.cs`: block 8/11, then manual hand discard one.
- `sts2/src/Core/Models/Cards/Suppress.cs`: innate 11/17 attack, then Weak 3/5.

## Actual validation commands and results

The required isolated build was run against the test project and separate artifacts:

```text
dotnet build mod-the-spire/base-spire/BaseSpire.Slice.Tests --artifacts-path F:/Scylier-Project/active/auto-the-spire/.artifacts/parallel/dsh-ready
```

Final `build-batch04.log`: exit 0, 0 warnings, 0 errors.

The per-card minimum semantic command was run against the isolated DLL for every card in batches 01 and 02, and for the final two cards in batch 04:

```text
dotnet F:/Scylier-Project/active/auto-the-spire/.artifacts/parallel/dsh-ready/bin/BaseSpire.Slice.Tests/debug/BaseSpire.Slice.Tests.dll --remaining-card-families-only --card CARD-ID
```

Batch 01 passed 11 of 12 card commands. `minion-strike` initially failed with `InvalidOperationException: Sequence contains no matching element` at `CombatSlicePackage.cs:3940` because `draw={count=1}` did not lower; changing the DSL to `draw={amount=1}` fixed it. Batch 02 then passed all 16 card commands, including the repaired MinionStrike. Batch 04 passed all three modes for both `survivor` and `suppress`:

```text
survivor: normal, created-upgraded, armaments-upgraded — all passed
suppress: normal, created-upgraded, armaments-upgraded — all passed
```

The first Survivor attempt had the test-fixture failure `Sequence contains no matching element` while searching for a discardable Wound. The fixture was corrected by putting a real Wound into hand; no assertion was weakened. The rebuilt final run passed.

Coverage was recorded with:

```text
dotnet F:/Scylier-Project/active/auto-the-spire/.artifacts/parallel/dsh-ready/bin/BaseSpire.Slice.Tests/debug/BaseSpire.Slice.Tests.dll --unblocked-card-coverage
```

Final `coverage-batch04.log` snapshot: **177 rows, 177 parsed, 175 lowered, 175 registered**. These are current loaded/registered source rows and include concurrent owner content; they are not the 577-card denominator and do not imply gameplay parity for every registered card. The earlier batch02 snapshot was 175/175/173/173.

## Unsupported and unexecuted scope

The remaining source scan was completed by classification, not by pretending every source row was ready. The following dependencies remain unimplemented or unverified and were not approximated:

- Exact named powers absent from current DSL: Poison, BlockNextTurn, DrawCardsNextTurn, FreeSkill, Blur, PiercingWail, Tracking, and other card-specific powers.
- Sly keyword behavior, generated Shiv/enchantment behavior, deterministic generated-card creation, autoplay/tag filtering, and generated-card upgrade semantics.
- Playability and conditional gates such as Grand Finale's empty draw pile requirement.
- Instance callbacks/scoped cost mutation such as Pinpoint and Up My Sleeve.
- Damage-result/kill aggregation such as Echoing Slash and Omnislice.
- Typed history and conditional amount/clamp gaps such as Precise Cut, Expertise, Flechettes, and several history cards.
- ORB, STAR/FORGE, summoned-entity, run-state, complete generation-pool, and source-specific history/resource mechanisms.
- Curse/status metadata remains correctly classified as unsupported where the frontend only maps Attack/Skill/Power/Status kinds; no Curse cards were falsely claimed ready.
- Collision Course was not added: its source creates a deterministic Debris singleton, while current `GenerateCards` samples even a singleton explicit pool. No duplicate Debris definition was introduced.

No full Engine/Slice/solution build, normal Boss flow, identity comparison, or external reference comparison was run. No independent original-game parity claim is made. The dated audit files under `.audit/dsh-ready-content-2026-10-10/` contain the source scan, build logs, per-card logs, and coverage snapshots.

## Maintainer next action

The next maintainer should use the fixed shared contracts for exact powers, generated-card destinations, callbacks, history scopes, and playability before adding the blocked source families. Continue from the 577-entry ledger and current coverage snapshots; do not treat 175 registered rows as complete 577-card support. The ready-card implementation and its minimum checks are complete for this handoff; stop this session rather than looping on unrelated shared mechanisms.
