# Combat content DSL

This document describes the current authoring boundary for `core-the-spire` and `base-spire`. The language declares combat content; the core owns execution order, legal actions, power timing, target resolution, and RNG streams. Source is parsed and compiled while packages are built. Combat steps consume typed definitions and do not interpret DSL syntax.

## Authoring model

The package DSL has three shared building blocks:

- **Declarations:** cards, combat potions, Powers, enemies, and their stable package-scoped names.
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

`playerLostHpThisTurn()` reads whether the player has lost positive HP to damage or direct HP loss during the current turn. Blocked hits do not set it. Spite uses this predicate to choose one hit or two hits (three when upgraded); it resets when the next player turn begins.

`scaledAttack(base, perCount, count=pileCount(exhaust))` and `scaledAttack(base, perCount, count=targetPowerAmount(Vulnerable))` read combat data when damage resolves, then use the ordinary attack damage pipeline. `hasPower(Vulnerable, target=target)` lets a condition inspect the selected enemy rather than the acting player. Card metadata may set `canGenerateInCombat = false`; Not Yet records that source restriction, though a general combat card-generation operator has not been added yet.

Card definitions may declare `tags = [Strike]`. A tag is a namespaced content ID; a local name uses the current package, and a quoted full ID such as `"base-spire:strike"` lets a mod refer to another package's tag. `cardsWithTag(Strike)` counts matching card instances across hand, draw, discard, exhaust, and play piles. Perfected Strike uses this count, including its own card instance after it enters the play pile.

Cards declare reward-pool membership with `pools = [IroncladCardPool]`. Pool references use the same local or quoted qualified ID syntax as tags; a mod may add a card to another package's pool with `pools = ["base-spire:ironclad-card-pool"]`. The registry resolves those declarations into the composite `CardPoolDefinition` when all packages are built, rejects unknown pools, and keeps cards without a pool declaration out of reward pools. Rarity remains card metadata and controls rarity selection within a pool; it does not imply membership.

`applyScaledPower(Power, base, perCount, count=..., target=...)` reuses the same runtime count sources as `scaledAttack`. The count reads the selected combat target even when the Power is applied to `self`. Dominate uses this after adding Vulnerable to the enemy; Molten Fist uses it after damage only if the enemy survives and remains Vulnerable.

A Power may declare `maxEnergyPerStack = 1`. The core sums the active stacks when resetting energy at the start of each player turn, and the combat observation exposes the resulting maximum. Pyre grants one stack, or two when upgraded; playing it changes the maximum immediately but does not refill current energy.

A Power may declare `vulnerableMultiplierPercentPerStack = 1`. Each stack adds one percentage point to its owner's powered attack multiplier against a Vulnerable opponent. Cruelty applies 25 stacks, or 50 when upgraded. The modifier composes with other Vulnerable multiplier hooks and is used by both actual damage and intent calculation.

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

## Card-play rules and calculated values

```text
power CorruptionPower {
  cardRule = cardRule(type=skill, cost=0, resultPile=exhaust)
}
power FreeAttackPower {
  cardRule = cardRule(type=attack, cost=0, consumeOnPlay=true)
}
power NoEnergyGainPower {
  preventEnergyGain = true
  removeAfterOwnerTurnEnd = true
}
```

A card rule matches a definition type and can override its fixed energy cost, change its result pile to `discard` or `exhaust`, or consume one Power stack before play. At least one operation is required; unknown arguments and negative costs fail during compilation. Active rules compose in Power insertion order, with later overrides winning. Exact original Hook ordering is still pending. Cost overrides do not change X-cost payment: an X card captures and spends available energy, but its matching consume rule still runs.

The same resolver produces `CombatObservation.HandPlayPreviews`, legal actions, and payment. It snapshots the destination before effects begin. The default is discard for ordinary cards, exhaust for Exhaust cards, and no combat pile for Power cards. After effects and after-play triggers complete, the instance leaves the play pile immediately. A suspended card remains in play until its continuation completes; changes to Powers during the choice do not recalculate its destination.

`modifyEnergy(scaled(base, perCount, count=...))` calculates one amount and performs one energy command. `pileCount(hand, type=attack)` filters cards by definition type; Expect A Fight uses it before applying the gain lock. Positive gains respect `preventEnergyGain`; spending and next-turn energy reset remain available. Numeric `modifyEnergy(n)` stays supported.

`repeatForCount(base, perCount, count=..., effect=...)` captures its count once and schedules that many effects. `playerHpLossCount()` reads positive unblocked HP-loss events across the combat, including direct HP loss and excluding fully blocked attacks. Tear Asunder uses it for hit count. The core stores a combat summary rather than scanning the event log during a play. This first calculated-value slice supports `scaled` on energy and counted repetition; full arithmetic and calculated parameters for every primitive remain future DSL work.

## Automatic play and replay contract

`autoPlay(from=draw, count=1, position=top, forceExhaust=true)` reserves a batch into the play pile before executing its cards in order. Draw-pile exhaustion can shuffle discard while reserving, using the existing shuffle stream. This operation does not draw to hand and does not fire draw events. Top and bottom selection are supported in this slice; hand/exhaust selection and draw-triggered automatic play remain later extensions.

`count=scaled(0, 1, count=energyX())` reads the current card's captured X value; an upgraded Cascade adds one to the base. Automatically played cards do not spend energy and independently capture current energy for their own X effects. Selected-enemy targets use the existing `card-targets` stream (the core's CombatTargets domain); no target choice RNG is consumed for self or area targets. Unplayable cards move to discard or exhaust without executing effects; forced exhaustion applies to that fallback too. Successfully played Power cards still leave combat even when forced exhaustion is requested.

One combat card lifecycle owns both manual and automatic play: result-pile snapshot, replay-count resolution, before-play rules, effects, after-play triggers, and placement. Nested card frames retain each card's target and X value across choices. Lifecycle continuation nodes run on the existing effect stack; there is no separate automatic-play scheduler. Batch cards are reserved before those frames start, matching the source's selection timing.

`cardRule(type=attack, extraPlays=1, consumeOnPlayCount=true)` adds one execution to a matching card and consumes one Power stack when its total execution count is resolved. The amount of that Power controls how many future cards receive the rule, not the number of extra executions. `consumeOnPlay=true` remains a before-each-execution operation, as required by Free Attack. Card effects and after-play triggers run for each execution, while payment, X capture, and final placement happen once. One-Two Punch expires at owner turn-end.

Cards may declare `onExhaust` and `upgradedOnExhaust`. The exhausted instance supplies the effect context and upgrade level; an upgraded exhaust callback alone makes the definition upgradable. The same dispatch covers played Exhaust cards, forced automatic exhaustion, direct and selected pile moves, bulk moves, and Ethereal cleanup. Drum of Battle draws two when played and grants 2 energy (3 upgraded) when exhausted. Self callbacks execute once per exhaustion before authored exhaust-trigger Powers in the current implementation; original global model Hook ordering remains pending. General card draw/turn events and generated callback replay counts are not implemented by this slice.

## Combat potion contract

Potions use the same effect compiler and execution queue as cards and Powers:

```text
package base-spire {
  potion FirePotion {
    rarity = common
    usage = combatOnly
    target = enemy
    use = attack(20, target=target, properties=[unpowered])
  }
  potion FlexPotion {
    rarity = common
    usage = combatOnly
    target = self
    use = applyPower(FlexPotionPower, 5, target=self)
  }
}
```

`rarity`, `usage`, `target`, and `use` are required. This slice accepts only `usage = combatOnly`; unsupported usage is rejected during compilation. Declaration targets use `self`, `enemy`, `firstEnemy`, `allEnemies`, or `randomEnemy`, as for cards; effect targets use `target` for the selected enemy. Optional metadata is `inSharedPool` (default true), `characterPools` (local or quoted qualified character IDs; requires `inSharedPool = false`), and `canGenerateInCombat` (default true). The registry validates referenced characters and Powers. `applyPower` also accepts a quoted qualified Power ID. Original `AnyPlayer` targets project to `self` in the single-player model; multiplayer targeting is not implemented.

`attack` accepts an optional `properties` list of damage flags (`move`, `unpowered`, `unblockable`, `skipHurtAnimation`). Omission defaults to `[move]`; an empty list means no flags. Fire Potion explicitly uses `[unpowered]`, matching its source without adding the Move flag. Block Potion currently uses `gainBlock(12)`; source block properties and their full modifier pipeline remain outside this slice.

## Captured counts and lexical scope

`withCount("name", count=..., effect=...)` evaluates its count once before entering the body. `savedCount("name")` reads that immutable binding in any counted effect inside the body. Bindings follow effect composition and pending choices; nested bindings may shadow an outer name, and leaving the body restores the outer binding. They are execution context, not persistent mod variables. References outside their lexical scope fail registry validation. Independently triggered effects and automatically played cards start their own scope.

```text
play = withCount("handSize", count=pileCount(hand), effect=sequence(
  forEachCard(from=hand, effect=moveSelectedCard(from=hand, to=exhaust)),
  repeatForCount(0, 1, count=savedCount("handSize"), effect=attack(7))
))
```

`forEachCard` snapshots its original card instances, then completes each item's effect and triggered continuations before advancing to the next item. In this example, exhaustion callbacks may draw cards, but those new cards are not exhausted and do not change the number of attacks. All original exhaustion callbacks finish before the attack sequence starts.

`choose(prompt="...", options=[option("...", effect=...)])` exposes an ordinary choice through the same scheduler; an option may omit its effect. Both ordinary and card choices preserve the lexical count scope.

`generateCards(count=number|scaled(...), pile=hand, upgraded=false)` samples with replacement from the holder's character pools, preserving pool order and deduplicating content IDs. Basic, Ancient, Event, and `canGenerateInCombat=false` cards are excluded, as in `CardFactory.FilterForCombat`. Each sample consumes one call on `combat-card-generation`. All samples are reserved before their generated instances are added sequentially; each `card_generated` event and its continuations finish before adding the next instance. Generated cards have fresh combat identities and do not alter the permanent deck. Zero requests with replacement consume no random calls. Distinct requests shuffle even for zero requested cards. Requests that sample require an explicit character identity; with-replacement requests require a nonempty eligible pool.

Run combat supplies the character identity automatically. Standalone `Start`/`StartEncounter` may supply `characterId` independently of `playerId`; a player ID is never interpreted as a character name. This slice uses the currently registered subset of the owner's pool. Full original pool order, multiplayer eligibility metadata, hand capacity/overflow, and all original generation hooks remain fidelity work.

## Card instance cost and transformation

`costModifier(amount, operation=set|add, expires=[turnEnd, played, combatEnd], reduceOnly=false)` describes an ordered local cost modifier. The expiration list means the first matching boundary clears it; `combatEnd` alone persists for the combat. `modifySelectedCardCost(costModifier(...))` appends it to the selected instance. Pile moves and upgrades preserve modifiers. Fixed cost is evaluated from upgraded base cost through local modifiers, then global Power rules, with final zero clamping. X cards still spend current energy. A cost-reducing upgrade caps existing absolute overrides at the new base cost, matching source `CardEnergyCost.UpgradeBy`.

Generation accepts `type=attack`, `distinct=true`, and `cost=costModifier(...)`. With `distinct=true`, the entire eligible pool is Fisher–Yates shuffled before taking up to the requested number, matching `GetDistinctForCombat` / `TakeRandom` and its random call count. A filtered empty distinct pool returns no cards. Local modifiers are installed before insertion and its generation events. Infernal Blade uses `expires=[turnEnd, played]`; cleanup after a completed play happens after placement/exhaust callbacks, while turn cleanup happens after flush callbacks and before subsequent side-end Power effects.

`transformSelectedCard(Card, from=hand, upgraded=false)` replaces the selected combat instance in the same pile position with a fresh instance. It does not inherit upgrade or cost modifiers, emit an exhaustion event, or alter the permanent deck. `forEachCard(..., filter=attack, ...)` snapshots hand Attacks for Primal Force. All original cards are transformable in combat under source `CardModel.IsTransformable`; Eternal only prevents transformation in the permanent deck. Transformation currently supports draw, hand, discard, and exhaust, and rejects an active play-pile source. It emits `card_transformed` with old/new instance IDs; the original instance selection is consumed by replacement. Source transformation and ordinary generation use different hooks, so transformation does not emit `card_generated`. Full source transformation hook and enchantment semantics remain open.

## Current source of truth

The remaining Ironclad source semantics and shared DSL work are tracked in [ironclad-dsl-coverage.md](ironclad-dsl-coverage.md). Card-play rules and counted composition extend this language through the existing executor; they do not add a second scheduler.

- [`ironclad-cards.spire`](../mod-the-spire/base-spire/BaseSpire/ContentSource/ironclad-cards.spire) declares 68 cards currently associated with Ironclad combat, including the Event card Entrench, status card Slimed, and token Giant Rock. The Ironclad pool contains 65 of the source's 87 cards, each marked with `pools = [IroncladCardPool]`.
- [`ironclad-powers.spire`](../mod-the-spire/base-spire/BaseSpire/ContentSource/ironclad-powers.spire) declares the current Feel No Pain and Rage Power triggers the Pyre maximum-energy and Cruelty vulnerable-damage modifiers, the Setup Strike/Mangle Power lifecycles, and Corruption/Free Attack/No Energy Gain card and resource rules.
- [`enemies.spire`](../mod-the-spire/base-spire/BaseSpire/ContentSource/enemies.spire) declares Ritual and all nine enemy definitions currently registered by `base-spire`: Damp and Calcified Cultists, Fuzzy Wurm Crawler, Shrinker Beetle, Nibbit, and four Leaf/Twig Slimes.
- [`potions.spire`](../mod-the-spire/base-spire/BaseSpire/ContentSource/potions.spire) declares Fire, Block, Strength, Blessing of the Forge, Beetle Juice, and Flex Potion; [`potion-powers.spire`](../mod-the-spire/base-spire/BaseSpire/ContentSource/potion-powers.spire) declares Flex Potion's temporary Strength lifecycle.
- `base-spire.encounters.json` still declares encounter lineups and the Nibbit slot-specific opening actions. Encounter authoring is a separate migration step.

Enemy behaviors in the current roster exercise fixed cycles, a one-time opening move, repeated moves, random choices without immediate repeats, and consecutive-use limits. Gameplay tests exercise normal and Deadly/Tough profiles through the real compiled package. JSON enemy definitions and their separate loading path have been removed; encounters remain JSON.

## Power lifecycle contract

Temporary modifiers are authored as lifecycle declarations, with no card-specific runtime handler:

```text
power SetupStrikePower {
  onStackChange = grantPower(Strength, stackDelta)
  onExpire = grantPower(Strength, negativeSourceStacks)
  removeAfterOwnerTurnEnd = true
}
```

`onStackChange` receives the signed delta after amount-modifying hooks, including subsequent applications and decrementing stacks. Its first supported operation is `grantPower(Power, stackDelta)` or `negativeStackDelta`, applying the adjustment to the same owner. `onExpire` uses `sourceStacks` or `negativeSourceStacks`, capturing the remaining amount before removal. Expiration occurs during the holder's side-end Power queue, before subsequent enemy actions for a player and after enemy actions for a monster. Ordinary `removePower` does not invoke expiration; this matches the distinct removal and side-end restoration operations in the recovered TemporaryStrengthPower source. Lifecycle references must resolve in the composite registry, and recursive stack-change links are rejected at build time.

Setup Strike and Mangle use opposite signs of this same contract. Strength remains a separate observable Power, so its own amount-modifying hooks still run. Copying temporary modifiers together with their already-applied Strength, as done by Misery, requires a separate suppression contract and remains open.

## Remaining content

The nine enemies are the roster currently modeled by this repository; this is not a claim that the full original game's monster catalog has been imported. The source audit identifies substantial gameplay still to implement, including monsters not yet registered here, role-specific behavior in the encounter source, conditional action state, and move-log cooldown rules. Those should be authored in this DSL when their source semantics are implemented. They should not be approximated by changing the state machine to match one content fixture.

The remaining Ironclad cards expose several shared language needs. Rampage and Thrash change values on a particular card instance and still need declared damage variables. Ordered local cost modifiers are implemented; Stomp next needs card-entry and card-play event declarations plus a per-turn finished-play summary to drive them. Rage's filtered card-play trigger is now modeled; Dark Embrace additionally distinguishes normal exhaustion from Ethereal exhaustion and defers draws until after the hand flush. Stone Armor's Plating uses a turn-boundary phase before damage effects, and Howl from Beyond automatically plays from the exhaust pile. Primal Force now uses shared instance transformation; full transformation hooks remain open. Tank and Demonic Shield declare multiplayer constraints even though the current training slice runs single-player combat. These source cases should drive the next core contracts rather than receive individual native card handlers.

The original `IroncladCardPool` explicitly lists 87 members and remains the coverage reference. Explicit DSL pool membership is implemented and supports third-party extensions; mode eligibility and the full source pool remain open. Account unlock progress is outside this project's scope, so all otherwise eligible original cards are treated as unlocked.

Mod package composition, explicit replace/patch precedence, source provenance, and performance benchmarks remain open. The current compiler is a first semantic slice rather than the final mod API.
