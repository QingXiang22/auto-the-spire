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

An enemy's effect and its behavior remain separate. For example, an attack expression says what the move does; `startWith(..., then=loop(...))` says when that move is selected. `startWithChoice(choice, then=behavior)` gives a weighted opening choice its own candidate set, then switches to the follow-up behavior for subsequent moves. `graph(start=move, edges=[edge(from, toOrChoice), ...])` defines arbitrary fixed and random outgoing edges directly, including branches that later converge. `chooseByCondition(candidate(move, when=metric(...)), ...)` evaluates candidates in order and chooses the first matching predicate; its metric, Power, and `all`/`any`/`not` predicates reuse the typed enemy-condition model. A candidate may declare `maxUsesPerCombat` to cap a one-time move. The compiler lowers these forms to typed `EffectNode`, `PowerDefinition`, and `EnemyDefinition` values. The combat state machine is refined only when a source behavior cannot be represented faithfully.

Enemy declarations may set `startingPowers = [power(Ritual, 5)]` and `startingBlock = variants(default=14, tough=18)`. The core applies these values when it creates the enemy, before selecting its initial move. Amounts may use the configured difficulty variants. These are shared enemy data, not monster-specific setup handlers. Powers may declare `isDebuff = true`; a Power with the generic `Artifact` role consumes one stack to negate the next positive application of such a Power.

Powers may declare `role = Dexterity` to modify Move-based block in the common block-gain pipeline; `Unpowered` block sources bypass Dexterity. The role amount changes with ordinary Power application, decrement, and removal. Tender stores this turn's played-card count in a Power variable, applies Strength and Dexterity loss on `afterCardPlayed`, then restores both stats and clears its counter on `afterOwnerTurnEnd`.

Effects may branch on a compiled predicate at execution time. For example, `if(cardExhaustedThisTurn(), repeat(2, gainBlock(8)), else=gainBlock(8))` models Evil Eye. `pileCountAtLeast(exhaust, 3)` models Pact's End. The first predicate reads a turn-scoped combat summary, and the second reads the current pile; neither requires a card-specific runtime branch.

`playerLostHpThisTurn()` reads whether the player has lost positive HP to damage or direct HP loss during the current turn. Blocked hits do not set it. Spite uses this predicate to choose one hit or two hits (three when upgraded); it resets when the next player turn begins.

`scaledAttack(base, perCount, count=pileCount(exhaust))` and `scaledAttack(base, perCount, count=targetPowerAmount(Vulnerable))` read combat data when damage resolves, then use the ordinary attack damage pipeline. `hasPower(Vulnerable, target=target)` lets a condition inspect the selected enemy rather than the acting player. Card metadata may set `canGenerateInCombat = false`; Not Yet records that source restriction, though a general combat card-generation operator has not been added yet.

Cards may declare their source-level damage variable separately from their play effects: `damageVariable = Damage` reads a mutable instance variable; `damageValue = 7` declares a fixed canonical value; `damageValue = scaled(6, 3, count=pileCount(exhaust))` declares a calculated value; and `upgradedDamageValue` supplies its upgraded form. This is needed by effects such as Thrash, which reads the selected Attack's `Damage.BaseValue` or `CalculatedDamage.Calculate(null)` before exhausting it. The projection uses the source variable's calculation context, not the selected enemy's Vulnerable stacks or the card's full effect tree. Outgoing owner-side damage modifiers still apply before the value is added.

Card definitions may declare `tags = [Strike]`. A tag is a namespaced content ID; a local name uses the current package, and a quoted full ID such as `"base-spire:strike"` lets a mod refer to another package's tag. `cardsWithTag(Strike)` counts matching card instances across hand, draw, discard, exhaust, and play piles. Perfected Strike uses this count, including its own card instance after it enters the play pile.

Cards declare reward-pool membership with `pools = [IroncladCardPool]`. Pool references use the same local or quoted qualified ID syntax as tags; a mod may add a card to another package's pool with `pools = ["base-spire:ironclad-card-pool"]`. The registry resolves those declarations into the composite `CardPoolDefinition` when all packages are built, rejects unknown pools, and keeps cards without a pool declaration out of reward pools. Rarity remains card metadata and controls rarity selection within a pool; it does not imply membership.

`applyScaledPower(Power, base, perCount, count=..., target=...)` reuses the same runtime count sources as `scaledAttack`. The count reads the selected combat target even when the Power is applied to `self`. Dominate uses this after adding Vulnerable to the enemy; Molten Fist uses it after damage only if the enemy survives and remains Vulnerable.

A Power may declare `maxEnergyPerStack = 1`. The core sums the active stacks when resetting energy at the start of each player turn, and the combat observation exposes the resulting maximum. Pyre grants one stack, or two when upgraded; playing it changes the maximum immediately but does not refill current energy.

A Power may declare `vulnerableMultiplierPercentPerStack = 1`. Each stack adds one percentage point to its owner's powered attack multiplier against a Vulnerable opponent. Cruelty applies 25 stacks, or 50 when upgraded. The modifier composes with other Vulnerable multiplier hooks and is used by both actual damage and intent calculation.

A Power may declare `blockGainMultiplier`. The core applies it once to positive block gains with `Move` properties; ordinary non-move block sources bypass it. Frail uses `0.75`, expires after the enemy side turn, and skips its first player-side tick.

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

`cardRule(type=attack, costDelta=1)` applies an additive cost change to every matching non-X card while the Power is active; the delta is multiplied by current Power stacks and applied after any fixed `cost`. Tangled uses this rule to increase Attack costs for the turn. A Power lifecycle can use `scaledAttack(0, 1, count=targetPowerAmount(...), target=self, properties=[unpowered])` for stack-scaled, blockable damage without Strength; Constrict uses it at owner turn-end and is removed when its applier dies.

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

`attack` and `gainBlock` accept an optional `properties` list (`move`, `unpowered`, `unblockable`, `skipHurtAnimation`). Attack omission defaults to `[move]`; block omission keeps the existing source inference from player/enemy ownership and card context. An empty list means no flags. Fire Potion explicitly uses `[unpowered]`, matching its source without adding the Move flag; Plating-based enemy block can now preserve its source `unpowered` property.

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

## Card callbacks and play history

Cards may declare `onEnterCombat` / `upgradedOnEnterCombat` for their own insertion, and `beforeCardPlayed` / `afterCardPlayed` as `cardEvent(whenCardType=attack, effect=...)`. Upgraded listeners may replace either declaration. `addCards` also accepts quoted qualified card IDs. The callback's selected-card context is the listening instance, while the card-play frame remains the played card. Listener IDs are snapshotted in the source pile order (hand, draw, discard, exhaust, play); each callback resolves the still-present instance and its current upgrade before execution. A card with no pile no longer listens.

`cardPlaysThisTurn(type=attack, phase=started|finished)` reads a combat history summary, with `finished` as default and the type filter optional. Each replay execution is counted. Before-play listeners finish before the started count and play event; finished count advances after the card body but before after-play listeners. Both boundaries may pause or run nested automatic cards. Counts remain available during player flush and end callbacks, reset at the switch to the enemy side, and reset again before the next player's turn-start effects and draw. This follows source history's side and turn boundaries.

Local cost commands additionally accept `costModifier(scaled(...), operation=add, expires=[turnEnd])`. The expression resolves to a concrete modifier when the command executes; generated-card cost initialization remains a literal descriptor. `selectedCardIsClone()` can guard insertion initialization, as Stomp does. `IsClone` belongs to the card instance and survives moves, costs and upgrades; actual clone creation is a separate operation still to implement.

Own-entry callbacks run on initial combat instances, random/fixed generation, and transformation, before their final insertion event. A pile move is not a new entry. Initial callbacks may suspend before the `combat_started` event; run ownership is already available during them. The source's full cross-model listener ordering, affliction/enchantment callbacks and clone creation remain separate work.

## Declared card variables and combat clones

Cards declare decimal combat variables with `variables = [variable(ExtraDamage, 0, preserveOnDowngrade=true)]`. A declaration may provide `upgraded=...`; the live value is the definition's base/upgraded value plus an instance-local offset. Definition values remain immutable. `cardVariable(ExtraDamage, card=played|selected)` is a numeric source for existing `scaled` / `scaledAttack` composition and is floored when used as an integer count. `modifyCardVariable(ExtraDamage, amount, card=played|selected, operation=add|set)` accepts a literal or a `scaled` amount. Played resolves the live innermost play frame; selected resolves the current listener or choice instance by stable ID. Reads occur at execution; `withCount` captures a value when a later continuation needs the earlier value.

Variables follow a combat instance through pile moves, replays and upgrades. `downgradeSelectedCard()` restores ordinary definition values and clears offsets unless the declaration explicitly preserves them; Rampage and Thrash preserve accumulated damage. They survive neither fresh generation nor transformation into a new instance. Permanent deck instances do not receive combat mutations. Instance offsets are immutable and included in ordinary card observations; no per-card store is allocated until a value changes. `card_variable_modified` events expose the exact decimal delta in `ExactAmount`. Integer resource/count domains remain integral; card damage values retain decimals until the normal hit pipeline floors final damage.

`attackVariable(Damage, hits=2)` attacks with a played card's variable through the normal hit pipeline. `accumulateCardDamage(Damage, target=played, source=selected)` resolves the selected card's current damage from its DSL attack expression, applies the card owner's outgoing modifiers without target-side Vulnerable or Block, and adds the unrounded result to the target card variable. The projection handles static, scaled, repeated, conditional, and variable-based attacks; an Attack with no damage expression contributes zero. Thrash combines these effects with `randomMoveCard` to select and exhaust a hand Attack.

`cloneCard(card=played|selected, pile=hand, count=1)` snapshots the live source's definition, upgrade, variable offsets and local cost modifiers, assigns fresh IDs, marks each copy as a clone, and inserts them sequentially through the shared generated-card/own-entry lifecycle. Source and copies can subsequently change independently. Cloning is distinct from `addCards` / `generateCards`, which start from definition defaults; Anger uses cloning. Forced exhaustion belongs to a play frame and is not inherited. Star-cost snapshots, enchantments, afflictions and source clone ancestry remain later work.

Rampage reads `ExtraDamage` for the current attack, then increments it by 5 (upgraded 9). Its declaration preserves that accumulated value through downgrade. Anger clones the live played card with its current upgrade and combat-local cost modifiers.

## Current source of truth

The remaining Ironclad source semantics and shared DSL work are tracked in [ironclad-dsl-coverage.md](ironclad-dsl-coverage.md). Card-play rules and counted composition extend this language through the existing executor; they do not add a second scheduler.

- [`ironclad-cards.spire`](../mod-the-spire/base-spire/BaseSpire/ContentSource/ironclad-cards.spire) declares 90 cards currently associated with Ironclad combat, including Toxic, Infection and Slimed status cards, the Event card Entrench, and token Giant Rock. The Ironclad pool contains 85 of the source's 87 cards, each marked with `pools = [IroncladCardPool]`.
- [`ironclad-powers.spire`](../mod-the-spire/base-spire/BaseSpire/ContentSource/ironclad-powers.spire) declares the current Feel No Pain and Rage Power triggers the Pyre maximum-energy and Cruelty vulnerable-damage modifiers, the Setup Strike/Mangle Power lifecycles, and Corruption/Free Attack/No Energy Gain card and resource rules.
- [`enemies.spire`](../mod-the-spire/base-spire/BaseSpire/ContentSource/enemies.spire) declares Ritual, Artifact, Territorial Power, Illusion Power, Soar, Tangled, Constrict, Slow, Infested, HardToKill, Thorns, Slumber, Plating, Tender, Burrowed, Dexterity, Galvanic Power, and all forty-nine enemy definitions currently registered by `base-spire`, including Globe Head's Galvanic damage on Power cards, Toadpole's encounter-specific opening actions and reusable Thorns behavior, Slimed Berserker's four-move cycle, 4-hit attack and ten generated Slimed cards, Tunneler's block-preserving Burrow state, Hunter Killer with its card-counted Tender effect, Slumbering Beetle with damage-triggered wake behavior, Spiny Toad and its reactive Thorns power, Myte and its Toxic status card, Exoskeleton, Phrog Parasite and its summoned Wrigglers, Bygone Effigy, Fogmog, and its dynamically summoned Eye with Teeth. These declarations use shared attacks, repeats, block, starting block, Power modifiers, damage caps, weighted move behavior, explicit action graphs, conditional branches, and enemy summons.
- [`potions.spire`](../mod-the-spire/base-spire/BaseSpire/ContentSource/potions.spire) declares Fire, Block, Strength, Blessing of the Forge, Beetle Juice, and Flex Potion; [`potion-powers.spire`](../mod-the-spire/base-spire/BaseSpire/ContentSource/potion-powers.spire) declares Flex Potion's temporary Strength lifecycle.
- `base-spire.encounters.json` declares the currently modeled lineups, including GlobeHeadNormal, ToadpolesWeak with distinct front/back opening actions, ChompersNormal with its second Chomper opening with Screech, Ruby Raiders, Devoted Sculptor, the Snapping Jaxfruit/Flyconid and Flyconid/medium-slime encounters, Bygone Effigy, and Phrog Parasite, plus Nibbit's slot-specific opening actions. Encounter definitions for most imported enemies and the full original encounter roster still need migration.

Encounter JSON `formations` select a weighted branch first; each formation may contain fixed or random `enemySlots`, and formations may produce different numbers of enemies. This preserves branch-specific random draws and lets an encounter choose a family (for example, one secondary, one random medium, or two independently selected small enemies) before building its ordered lineup. Slithering Strangler's three Overgrowth lineup branches now use this representation. `startingActionChoicesByEnemyIndex` independently selects a slot's opening move on the enemy-AI random stream, while `startingActionsByEnemyIndex` fixes it; Exoskeleton uses both for its three- and four-member lineups.

Enemy behaviors in the current roster exercise fixed cycles, a one-time opening move, repeated moves, random choices without immediate repeats, and weighted consecutive-use limits. `chooseWithLimits(candidate(move, weight=2, maxConsecutive=1), ...)` expresses a weighted move choice with a per-move streak cap; omitted weights default to one and omitted caps allow repetition. Gameplay tests exercise normal and Deadly/Tough profiles through the real compiled package. JSON enemy definitions and their separate loading path have been removed; encounters remain JSON.

Power variables can be changed by card-play triggers and lifecycle hooks using `modifyPowerVariable(Power, Variable, amount, operation=Set)`; the default operation adds. Incoming damage modifiers can read a variable declared by the same Power with `variable=..., perVariable=...`, adding the scaled variable value to the base multiplier. `AfterCardPlayed` is dispatched to Power owners on the player and every enemy; `AfterOwnerDeath` dispatches before combat state is observed as terminal. Cards can define `turnEndInHand` effects, which resolve after Ethereal cards are exhausted and before hand flush hooks. Enemy summons can specify `startingAction`; together with generic enemy-index conditions this models encounter minion openings without a monster-specific state-machine branch. Enemy observations expose each active Power's variable values.

## Power lifecycle contract

Temporary modifiers are authored as lifecycle declarations, with no card-specific runtime handler:

```text
power SetupStrikePower {
  onStackChange = grantPower(Strength, stackDelta)
  onExpire = grantPower(Strength, negativeSourceStacks)
  removeAfterOwnerTurnEnd = true
}
```

`onStackChange` receives the signed delta after amount-modifying hooks, including subsequent applications and decrementing stacks. Its first supported operation is `grantPower(Power, stackDelta)` or `negativeStackDelta`, applying the adjustment to the same owner. `onExpire` uses `sourceStacks` or `negativeSourceStacks`, capturing the remaining amount before removal. Expiration occurs during the relevant side-end Power queue, before subsequent enemy actions for a player and after enemy actions for a monster. The lifecycle runs for enemy Powers at player-side end and player Powers at enemy-side end as well. Ordinary `removePower` does not invoke expiration; this matches the distinct removal and side-end restoration operations in the recovered TemporaryStrengthPower source. Lifecycle references must resolve in the composite registry, and recursive stack-change links are rejected at build time.

Setup Strike and Mangle use opposite signs of this same contract. Strength remains a separate observable Power, so its own amount-modifying hooks still run. Copying temporary modifiers together with their already-applied Strength, as done by Misery, requires a separate suppression contract and remains open.

Power `events` subscribe to named game events with an owner role (`source` or `target`), optional positive-amount, owner-turn, damage-property, affected-Power, and `cardTag` filters, and an ordinary effect expression. The opposite creature becomes `selectedTarget`, so the response stays in the same effect language. A queued event reaction rechecks that its Power still has stacks before resolving. `damage` events preserve attack properties and card-instance source even when block absorbs all HP damage; this lets Flame Barrier respond to a powered hit while its own retaliation is marked `unpowered`. `draw` events preserve the drawn card instance, which is passed as the selected-card context to the reaction effect. Card-tag filters compare against that instance's content tags; Hellraiser uses `cardTag=Strike` with `autoPlaySelectedCard(from=hand)`. Multi-card draws are scheduled one card at a time, and the next card is drawn after the current card's event reactions and nested plays resolve. `block` events drive Juggernaut's scaled random retaliation. `deferToCardPlayed` accumulates the owner's current Power stacks by card instance when an event is caused by an active card; `takePowerEventAmount(Power)` consumes that value in a later card-play effect. Rupture uses this to apply Strength once after a card that caused HP loss finishes, while untracked damage applies Strength immediately. `removeAfterOpponentTurnEnd` expresses powers that survive the owner's turn and expire after the opposing side finishes.

Attack resolution also emits `before_damage` with the final incoming amount and damage properties before block is consumed, and `block_broken` when an attack depletes positive block. `power_removed` identifies the Power that reached zero, including removal caused by `decrementPower`. A Power may set `preventsBlockClear = true` to preserve its owner's block between turns. Event effects may use `setEnemyAction(ActionId)` to update an enemy's visible and next action immediately; Slumbering Beetle uses this to leave sleep when Slumber expires or damage removes its final stack; Tunneler uses block break to enter its dizzy action. These are shared combat-state operations available to content packages, not enemy-specific hooks.

Powers can declare `blockGainModifiers = [modifier(multiplier=2, require=[move], priorEntriesPerStack=1, playerOnly=true)]`. The modifier filters block gains by event properties, counts qualifying current-turn block entries from other card-play executions, and scales a gain while that count remains below the active Power amount times `priorEntriesPerStack`. Each block event retains its card-play execution ID, so multiple block commands inside one play do not consume one another's allowance. Unmovable uses this shared modifier.

## Remaining content

The forty-nine enemies are the roster currently modeled by this repository; this is not a claim that the full original game's monster catalog has been imported. The source audit identifies substantial gameplay still to implement, including monsters not yet registered here, role-specific behavior in the encounter source, stateful conditional action rules, and move-log cooldown rules. Those should be authored in this DSL when their source semantics are implemented. They should not be approximated by changing the state machine to match one content fixture.

The remaining Ironclad source cards are multiplayer-only: Tank and Demonic Shield require multiplayer eligibility metadata, ally targeting, damage sharing, and Guarded rules. Ordered local cost modifiers and own-entry/before-/after-play card callbacks are implemented; Stomp uses them with started/finished per-side-turn summaries. Rage's filtered card-play trigger is now modeled; Dark Embrace additionally distinguishes normal exhaustion from Ethereal exhaustion and defers draws until after the hand flush. Stone Armor's Plating uses a turn-boundary phase before damage effects, and Howl from Beyond automatically plays from the exhaust pile. Primal Force now uses shared instance transformation; full transformation hooks remain open. These source cases should drive the next core contracts rather than receive individual native card handlers.

The original `IroncladCardPool` explicitly lists 87 members and remains the coverage reference. Explicit DSL pool membership is implemented and supports third-party extensions; mode eligibility and the full source pool remain open. Account unlock progress is outside this project's scope, so all otherwise eligible original cards are treated as unlocked.

Mod package composition, explicit replace/patch precedence, source provenance, and performance benchmarks remain open. The current compiler is a first semantic slice rather than the final mod API.
