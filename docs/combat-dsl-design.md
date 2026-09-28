# Single-combat DSL

## Scope

The first content language describes one combat: card definitions, enemy moves and their selection rules, Powers used during combat, and the effects that connect them. It does not describe maps, rewards, events, or run progression. Those belong to later content domains.

The DSL has one job: say what a combat action does and when content chooses it. The core remains responsible for the combat lifecycle: turn order, energy payment, legal targets, pile movement, pausing for player choices, effect resumption, Power trigger timing, random streams, and terminal combat results. Content expressions compile once into the existing typed combat definitions. No parser, string lookup, or generic syntax tree is used during a combat step.

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

`ironclad-cards.spire` is authoritative for 68 currently modeled card definitions associated with Ironclad combat, including Entrench, Slimed, and Giant Rock; 65 belong to the 87-card original Ironclad pool through explicit `pools` declarations. `ironclad-powers.spire` declares Rage, Feel No Pain, Pyre, Cruelty, Setup Strike, Mangle, Corruption, Free Attack, No Energy Gain, and One-Two Punch Power behavior. `enemies.spire` is authoritative for Ritual and the nine enemy definitions currently registered by `base-spire`. `potions.spire` declares six combat potions, and `potion-powers.spire` declares Flex Potion's temporary Strength lifecycle. `SpireCombatDslCompiler` compiles card and potion declarations with shared effect expressions; `SpireCombatBehaviorDslCompiler` compiles Powers, enemies, variants, and move behavior. Both produce the typed definitions consumed by the existing combat lifecycle. Parsing and compilation happen while building the content package, never during a combat step.

Enemy source exercises fixed cycles, an opening move followed by a loop, random choices without immediate repeats, and consecutive-use limits. Difficulty values resolve once from the configured profile. The old JSON enemy files and their parser have been removed. Encounter lineups and Nibbit slot-specific starting actions still use JSON. The nine registered enemies are only the current project roster, not the complete original game's monster catalog; see [content-dsl.md](content-dsl.md) for remaining scope.

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
