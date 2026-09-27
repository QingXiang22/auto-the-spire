# Combat content DSL

This document describes the current authoring boundary for `core-the-spire` and `base-spire`. The language declares combat content; the core owns execution order, legal actions, power timing, target resolution, and RNG streams. Source is parsed and compiled while packages are built. Combat steps consume typed definitions and do not interpret DSL syntax.

## Authoring model

The package DSL has three shared building blocks:

- **Declarations:** cards, Powers, enemies, and their stable package-scoped names.
- **Expressions:** effects such as `attack`, `applyPower`, `gainBlock`, and `addCards`.
- **Behavior composition:** named enemy moves connected by `cycle`, `loop`, `startWith`, and random choice expressions.

State declarations use the same `.spire` syntax and are compiled before a session starts. The first schema supports integer values in `run` and `combat` scopes; a variable's ID and scope jointly identify it, so one name may have distinct values in the two scopes:

```text
package training-mod {
  state PracticeCardUses { scope = run type = int default = 0 }
  state PracticeCardUses { scope = combat type = int default = 0 }
}
```

The core initializes declared defaults and rejects undeclared or wrong-scope accesses from mod effect and hook contexts. These declarations do not yet add variable reads or writes to the effect DSL; custom handlers provide the current bridge. Visibility, persistence, and more value types remain to be specified.

An enemy's effect and its behavior remain separate. For example, an attack expression says what the move does; `startWith(..., then=loop(...))` says when that move is selected. The compiler lowers both to the current typed `EffectNode`, `PowerDefinition`, and `EnemyDefinition` model. The combat state machine is refined only when a source behavior cannot be represented faithfully.

Effects may branch on a compiled predicate at execution time. For example, `if(cardExhaustedThisTurn(), repeat(2, gainBlock(8)), else=gainBlock(8))` models Evil Eye. `pileCountAtLeast(exhaust, 3)` models Pact's End. The first predicate reads a turn-scoped combat summary, and the second reads the current pile; neither requires a card-specific runtime branch.

`scaledAttack(base, perCount, count=pileCount(exhaust))` and `scaledAttack(base, perCount, count=targetPowerAmount(Vulnerable))` read combat data when damage resolves, then use the ordinary attack damage pipeline. `hasPower(Vulnerable, target=target)` lets a condition inspect the selected enemy rather than the acting player. Card metadata may set `canGenerateInCombat = false`; Not Yet records that source restriction, though a general combat card-generation operator has not been added yet.

Card definitions may declare `tags = [Strike]`. A tag is a namespaced content ID; a local name uses the current package, and a quoted full ID such as `"base-spire:strike"` lets a mod refer to another package's tag. `cardsWithTag(Strike)` counts matching card instances across hand, draw, discard, exhaust, and play piles. Perfected Strike uses this count, including its own card instance after it enters the play pile.

`applyScaledPower(Power, base, perCount, count=..., target=...)` reuses the same runtime count sources as `scaledAttack`. The count reads the selected combat target even when the Power is applied to `self`. Dominate uses this after adding Vulnerable to the enemy; Molten Fist uses it after damage only if the enemy survives and remains Vulnerable.

A Power declaration may use `trigger = afterCardPlayed` with `whenCardType = attack` and a compiled effect. Its effect runs after the played card finishes, before that card leaves the play pile. `removeAfterPlayerTurnEnd = true` expires the Power after player turn-end Power effects and before enemies act. Rage uses these rules; Feel No Pain now declares its exhaust trigger and stack-scaled block in `.spire` as well. A resumed card choice dispatches the played-card trigger once. Exhaust triggers receive the exhausted card instance as effect context, including cards moved by a bulk pile effect.

Difficulty values use `variants(default=..., deadly=..., tough=..., deadlyTough=...)` and `range(min, max)`. The configured profile resolves them during package compilation. For example:

```text
enemy DampCultist {
  hp = variants(default=range(52, 54), tough=range(51, 53))
  moves = actions(
    dampCultistIncantation = applyPower(Ritual, variants(default=5, deadly=6), target=self),
    dampCultistDarkStrike = attack(variants(default=1, deadly=3), target=player)
  )
  behavior = startWith(dampCultistIncantation, then=loop(dampCultistDarkStrike))
}
```

The move names produce stable action IDs. `intent` and displayed damage are derived from the effect tree when the compiler can determine them. This keeps attack intent damage aligned with the action executed by the core.

## Current source of truth

- [`ironclad-cards.spire`](../mod-the-spire/base-spire/BaseSpire/ContentSource/ironclad-cards.spire) declares 51 cards currently associated with Ironclad combat, including the Event card Entrench and status card Slimed. The Ironclad pool contains 49 of the source's 87 cards and is generated from compiled card definitions with eligible rarities.
- [`ironclad-powers.spire`](../mod-the-spire/base-spire/BaseSpire/ContentSource/ironclad-powers.spire) declares the current Feel No Pain and Rage Power triggers.
- [`enemies.spire`](../mod-the-spire/base-spire/BaseSpire/ContentSource/enemies.spire) declares Ritual and all nine enemy definitions currently registered by `base-spire`: Damp and Calcified Cultists, Fuzzy Wurm Crawler, Shrinker Beetle, Nibbit, and four Leaf/Twig Slimes.
- `base-spire.encounters.json` still declares encounter lineups and the Nibbit slot-specific opening actions. Encounter authoring is a separate migration step.

Enemy behaviors in the current roster exercise fixed cycles, a one-time opening move, repeated moves, random choices without immediate repeats, and consecutive-use limits. Gameplay tests exercise normal and Deadly/Tough profiles through the real compiled package. JSON enemy definitions and their separate loading path have been removed; encounters remain JSON.

## Scope still open

The nine enemies are the roster currently modeled by this repository; this is not a claim that the full original game's monster catalog has been imported. The source audit identifies substantial gameplay still to implement, including monsters not yet registered here, role-specific behavior in the encounter source, conditional action state, and move-log cooldown rules. Those should be authored in this DSL when their source semantics are implemented. They should not be approximated by changing the state machine to match one content fixture.

The remaining Ironclad cards expose several shared language needs. Rampage and Thrash change values on a particular card instance, while Stomp changes its cost during a turn; these call for declared instance variables and cost modifiers with explicit reset rules. Rage's filtered card-play trigger is now modeled; Dark Embrace additionally distinguishes normal exhaustion from Ethereal exhaustion and defers draws until after the hand flush. Stone Armor's Plating uses a turn-boundary phase before damage effects, and Howl from Beyond automatically plays from the exhaust pile. Primal Force transforms card instances. Tank and Demonic Shield declare multiplayer constraints even though the current training slice runs single-player combat. These source cases should drive the next core contracts rather than receive individual native card handlers.

The current Ironclad pool is derived from eligible card rarities in one dedicated source file. Before mixed-character content or third-party pool extensions are imported, the DSL needs explicit pool membership and mode eligibility; rarity alone cannot express either rule. The original `IroncladCardPool` explicitly lists 87 members, which remains the coverage reference. Account unlock progress is outside this project's scope, so all otherwise eligible original cards are treated as unlocked.

Mod package composition, explicit replace/patch precedence, source provenance, and performance benchmarks remain open. The current compiler is a first semantic slice rather than the final mod API.
