# Single-combat DSL

## Scope

The first content language describes one combat: card definitions, enemy moves and their selection rules, Powers used during combat, and the effects that connect them. It does not describe maps, rewards, events, or run progression. Those belong to later content domains.

The DSL has one job: say what a combat action does and when content chooses it. The core remains responsible for the combat lifecycle: turn order, energy payment, legal targets, pile movement, pausing for player choices, effect resumption, Power trigger timing, random streams, and terminal combat results. Content expressions compile once into the existing typed combat definitions. Combat steps do not parse source or resolve source-language symbols; they execute typed nodes against registered IDs and bound execution context.

## Three source concepts

1. **Content declarations** give cards, Powers, and enemies stable names and combat metadata.
2. **Values** describe fixed amounts, upgrade variants, difficulty variants, and enemy HP ranges.
3. **Effects and behavior** compose reusable combat operations. Effects change combat state. Enemy behavior chooses named moves; it does not duplicate their effects.

Card targeting metadata controls which player actions are legal. An effect such as `attack(6)` uses the card's selected target by default; an explicit target such as `allEnemies` or `self` overrides it. This keeps target choice separate from damage execution.

## Effect algebra

The first Ironclad card set needs these shared operations:

- Damage and defense: `attack`, `scaledAttack`, `attackUsingBlock`, `randomAttack`, `gainBlock`, `gainBlockFromCurrentBlock`, `loseHp`.
- Powers and resources: `applyPower`, `draw`, `modifyEnergy`.
- Piles and cards: `addCards`, `autoPlay`, `chooseCard`, `chooseCards`, `randomMoveCard`, `forEachCard`, `moveSelectedCard`, `upgradeSelectedCard`, `upgradeCards`.
- Composition: `sequence`, `repeat`, `repeatForX`, `repeatForCount`, and `if(predicate, then, else=...)`.

The first combat predicates are `targetAlive()`, `hasPower(Power, target=...)`, `energyAtLeast(n)`, `cardExhaustedThisTurn()`, and `pileCountAtLeast(pile, n)`. Predicates are checked when the effect executes. `hasPower` defaults to the acting entity and can inspect the selected target. `cardExhaustedThisTurn()` reads a turn-scoped summary in combat state rather than scanning the entire event log on every play; `pileCountAtLeast` reads the current pile. Evil Eye, Forgotten Ritual, Pact's End, and Dismantle exercise these conditions in source-backed definitions.

`scaledAttack(base, perCount, count=pileCount(pile))` and `scaledAttack(base, perCount, count=targetPowerAmount(Power))` calculate base damage when the attack executes. The result then enters the same Strength, Weak, Vulnerable, and block calculation as `attack`. Ashen Strike and Bully use these count sources. The count expression is compiled to a typed node; it does not perform text lookup in combat.

Cards may declare namespaced `tags`; `count=cardsWithTag(Strike)` counts matching instances across all five combat piles at execution time. Perfected Strike demonstrates the count, including itself in the play pile. A quoted full tag ID lets mod content share a tag declared by another package. Fight Me composes `repeat`, `attack`, and `applyPower` so its Strength changes occur after both hits.

`applyScaledPower` shares the compiled count source with `scaledAttack`. The count reads the selected target while the Power destination remains an independent argument. Dominate applies Vulnerable before reading the updated amount to grant player Strength; Molten Fist reads surviving enemy Vulnerable after damage. Conflagration uses `repeat` over an area attack.

Power declarations now accept a compiled effect, a card-event trigger with optional `whenCardType`, and `removeAfterPlayerTurnEnd`. The core dispatches `afterCardPlayed` after the card effect completes and before the card leaves the play pile, including when the effect paused for a choice. Rage demonstrates an Attack filter and turn-end expiry. Feel No Pain uses a source declaration for its card-exhaustion trigger.

`chooseCard` and `chooseCards` are declarative suspension points. The core validates a choice, stores the continuation, and resumes the remaining effect sequence. The DSL does not define a second scheduler. Hooks that need custom native behavior remain named C# handlers attached to typed Power or card events.

## Source form

Related definitions live together in a package source file. Card upgrades may replace the effect tree or override selected values/costs. This makes structural upgrades explicit while keeping ordinary number upgrades concise.

```text
package base-spire {
  card Bash {
    type = attack
    rarity = basic
    cost = 2
    target = enemy
    play = sequence(
      attack(8),
      applyPower(Vulnerable, 2, target=target)
    )
    upgradedPlay = sequence(
      attack(10),
      applyPower(Vulnerable, 3, target=target)
    )
  }
}
```

The source parser produces a location-aware syntax tree. The package compiler checks each declaration and effect against the combat vocabulary, resolves local content names, and creates the same immutable definitions the core already consumes. Mods will later use package composition to add or replace named declarations; arbitrary C# injection is not part of this language.

## Implementation status

`ironclad-cards.spire` declares 93 card definitions associated with Ironclad combat, including all 87 source pool definitions plus Toxic, Infection, Entrench, Slimed, and Giant Rock; 85 belong to the single-player pool; the multiplayer-only Tank and Demonic Shield are excluded from it. `status-cards.spire` adds enemy-generated Beckon; all `*-cards.spire` files compile into the shared card registry. `ironclad-powers.spire` declares Rage, Feel No Pain, Pyre, Cruelty, Setup Strike, Mangle, Corruption, Free Attack, No Energy Gain, One-Two Punch, Juggling, Flame Barrier, Juggernaut, and Rupture Power behavior. The ten `enemies*.spire` content files compile all eighty-two enemy definitions currently registered by `base-spire`, including The Lost and The Forgotten with death-returned Strength/Dexterity theft, Phantasmal Gardener with card-source filtered, once-per-player-turn Skittish, Skulking Colony and Hardened Shell with its per-player-turn incoming HP loss budget, Soul Fysh with generated Beckon cards and Intangible, the shared combat Powers, Spectral Knight's Hex affliction lifecycle, Mysterious Knight's Flail Knight move cycle and starting Powers, Soul Nexus's no-repeat attacks and debuff, Torch Head Amalgam's repeated beam cycle, Entomancer's stack-scaled Personal Hive event, Gas Bomb's DeathBlow and self-kill, Living Fog's Smoggy card affliction and summon cycle, Corpse Slug's ally-death Ravenous and Stun behavior, Sludge Spinner's no-repeat random choices, Scroll of Biting and its Paper Cuts max-HP effect, Fossil Stalker and its hit-counted Suck Power, Globe Head's Galvanic Power, Toadpole, Slimed Berserker, Tunneler, Hunter Killer and its Tender Power, Slumbering Beetle, Spiny Toad, Tough Egg and its variable-driven hatch, Ovicopter and its capped Tough Egg summons, Myte, Exoskeleton, Phrog Parasite, Wriggler, Bygone Effigy, Fogmog and its dynamically summoned Eye with Teeth, Turret Operator/Living Shield coordination, Two-Tailed Rat's shared backup state, Fat Gremlin's wake/escape lifecycle, and Gremlin Merc's theft cycle, death summons, conditional escape reward adjustment, and recovery reward. `potions.spire` declares six combat potions, and `potion-powers.spire` declares Flex Potion's temporary Strength lifecycle. `SpireCombatDslCompiler` compiles card and potion declarations with shared effect expressions; `SpireCombatBehaviorDslCompiler` compiles Powers, enemies, afflictions, variants, and move behavior. Both produce the typed definitions consumed by the existing combat lifecycle. Parsing and compilation happen while building the content package, never during a combat step.

Enemy source exercises fixed cycles, explicit action graphs, ordered conditional branches, an opening move followed by a loop, separate weighted opening and follow-up choices, random choices without immediate repeats, per-action consecutive-use limits and cooldown windows, per-combat use limits, dynamic enemy summons, and ally-death reactions that stun the next action. Weighted branches use a float roll over eligible weights and subtract them in declaration order, matching the source runtime at boundaries. Repeated attack intents retain their hit count; observations apply Strength and other damage modifiers per hit before summing total damage. `otherAllies` targeting can be narrowed to an enemy definition with `onlyEnemy`; enemy Power player-side-start triggers express coordinated effects such as Living Shield granting block only to Turret Operator. Summons create registered enemy definitions, resolve starting Powers, then choose their initial action on the seeded enemy-AI stream unless the DSL supplies a starting action. Summoned enemies are not added to the current enemy-action cohort; they begin acting on the next enemy turn. Encounter formations may carry slot-specific opening actions so one formation draw can select a correlated lineup and action rotation. Difficulty values resolve once from the configured profile. The eighty-two registered enemies are only the current project roster, not the complete original game's monster catalog; see [content-dsl.md](content-dsl.md) for remaining scope.

The generic `kill(target=...)` effect resolves direct entity death without fabricating damage; it shares owner-death cleanup and ally-death reactions with lethal attacks. `DeathBlow` keeps a lethal attack's damage in the intent projection. Gas Bomb uses both as an ordinary content definition, and its `Minion` Power continues to suppress Fatal rewards when another actor kills it.

Enemies may declare instance-local integer state with `variables = [variable(Name, initialValue)]`. The same variable ID has a separate value in every enemy instance. `modifyEnemyVariable(Name, amount, operation=Add|Set)` changes the acting enemy's value, and `variable(Name, Equal|AtLeast|AtMost|GreaterThan|LessThan, value)` can guide its next-action transition. Enemy observations expose these values by content ID. This domain is for combat state owned by an enemy; package-wide run/combat variables and Power-owned variables keep their separate lifetimes and APIs.

Packages may declare shared integer combat state with `state Name { scope = combat type = int default = 0 }`. `modifyCombatState(Name, amount, operation=Add|Set)` updates it, and `combatState(Name, comparison, value)` reads it in enemy action transitions. The value is shared across every actor in a combat, resets at the next combat, and is exposed in `CombatObservation.CombatVariables`. The registry checks that effects and conditions refer to a declared combat-scope variable. Run-scope state remains run-owned and cannot be read through combat behavior.

Weighted enemy transitions may filter each candidate with `when=...`, and enemy declarations may name reusable predicates in `conditions`. `allyAction(Enemy, Move)` detects a living ally with that selected action; `metric(EnemyCount, ...)` counts combat enemy instances including defeated ones. Summon effects may apply both `maxLivingEnemies` and the independent total-instance cap `maxEnemies`. These are general behavior and encounter-capacity rules; they keep coordinated group logic in content data.

An encounter formation may preserve physical layout with `slotCount` and one `slotIndices` value per selected enemy. `EnemyCombatObservation.SlotIndex` exposes the position, and `summonEnemy(..., slot=LastAvailable)` fills the highest unoccupied position. Combat enemies are ordered by slot; the current enemy-turn cohort retains references to its original participants while summoned enemies are inserted, so new spawns begin acting next turn without shifting the active cohort.

The five Ruby Raiders are now source-authored enemy definitions. Their HP and attack values use the tough/deadly variant profile, with shared cycles for axe swings, the brute's strength setup, the crossbow's reload, and the tracker's Frail plus repeated attack. The Ruby Raiders encounter selects three distinct members from that five-enemy pool. Devoted Sculptor, Flail Knight, Sewer Clam, Snapping Jaxfruit, and Flyconid extend the same move graph. Weighted candidates support per-action consecutive limits and `cooldown=n`, which excludes a move selected among the last `n` actions; the runtime stores only each move's last selection index. `startWithChoice` preserves Flyconid's restricted opening pool before its full later pool. Sewer Clam's entry Plating and repeated Strength/attack cycle use shared Power and action definitions. Frail's block reduction is a Power-declared multiplier on powered block gains rather than a monster-specific branch.

Enemy definitions can declare `startingPowers` and `startingBlock`; the combat environment applies these after constructing the state and before resolving starting-action conditions. This gives the content model reusable ways to express source `AfterAddedToRoom` Power and block setup. The generic `Artifact` Power role consumes one stack to negate the next positive application of a Power marked `isDebuff = true`.

`candidate(move, maxUsesPerCombat=1)` caps a move's total selections for the encounter. The runtime records only action selection counts, alongside the last action and its consecutive count. Mawler uses this to make Roar a one-time move while keeping Rip and Tear and Claw from repeating immediately.

Manual play, automatic draw-pile play, and replay share nested card frames with lifecycle continuations on the existing effect stack. Cascade and Havoc author batch reservation; One-Two Punch authors a count modifier. Drum of Battle authors its own exhaust callback and upgrade override. The engine resolves the current instance before each execution rather than caching its upgraded effect across a choice. See [content-dsl.md](content-dsl.md) for phase and callback boundaries.

## Completion gate for the first combat package

- Every card currently registered by `IroncladCards` has a source declaration, including upgrades, target mode, cost kind, rarity, and keywords.
- Every used effect operator compiles to a typed core effect. Unknown operators, wrong argument types, and unresolved card/Power names fail with source locations.
- DSL-compiled card definitions drive the existing core combat lifecycle and pass the same gameplay checks as the previous definitions.
- Multi-enemy tests prove selected-target cards can hit either enemy; area and random-target cards retain their distinct target behavior.
- Enemy behavior compiles from named moves and preserves the distinction between a one-time opening move, a loop, and a cycle. The full monster roster still needs migration and behavior coverage.

### Captured values and generated batches

`withCount` evaluates a shared count source once and introduces an immutable lexical binding, read through `savedCount`. The binding belongs to a queued continuation rather than a card class or global scratch field. Choices preserve it; nested card plays start independent scopes. This permits Fiend Fire and Stoke to exhaust an original hand snapshot before using its original count, even when callbacks draw new cards.

`generateCards` uses the explicit combat character identity to select owner pools. It applies generation exclusions, samples with replacement on a dedicated stream, and reserves all new instances before insertion. Each insertion and generated-card event uses the existing effect stack, so hooks may pause between additions. Complete pool order, eligibility and capacity rules remain source-fidelity work; no card-specific executor branch is introduced.

### Card callbacks and history boundaries

Card definitions may register own-entry and filtered before-/after-play callbacks, including upgraded variants. A callback selects the listener while retaining the played card frame. Listener IDs follow source pile order, with live instance/upgrade resolution at dispatch. The same effect stack suspends callbacks and nested plays. Static flags skip whole listener phases when the composite definition has none.

Started and finished counts are separate authoritative summaries. Before callbacks precede started; the body precedes finished; after callbacks follow finished. Replays contribute per execution. Summaries retain player flush history and clear on side/turn transitions, matching source HappenedThisTurn. Fixed/random generation and transformation invoke own-entry before their completion events. This provides Stomp's DSL without a card-specific handler and establishes callback-only upgrade handling across combat, run and rewards.

Combat cards declare decimal variables as immutable definition defaults plus sparse instance offsets. Upgrade values come from the definition; upgrade preserves offsets, while downgrade clears offsets except for variables opted into preservation. Integer count contexts floor decimal values; damage contexts retain them until the ordinary hit pipeline floors final damage. `damageVariable`, `damageValue`, and `upgradedDamageValue` describe the source card's canonical damage value independently from its play effect. `attackVariable` and `accumulateCardDamage` apply owner-side outgoing modifiers to this projection while excluding target-side Vulnerable and Block. Random pile movement exposes the moved card to an `after` effect before Exhaust listeners. Thrash establishes these shared rules without a native card handler. A live combat clone snapshots upgrade state, variables and local costs, receives a new ID, then uses ordinary queued insertion and own-entry behavior. Anger and Rampage establish these shared rules.
