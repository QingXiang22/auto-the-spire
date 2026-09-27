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

- Damage and defense: `attack`, `attackUsingBlock`, `randomAttack`, `gainBlock`, `gainBlockFromCurrentBlock`, `loseHp`.
- Powers and resources: `applyPower`, `draw`, `modifyEnergy`.
- Piles and cards: `addCards`, `chooseCard`, `chooseCards`, `randomMoveCard`, `forEachCard`, `moveSelectedCard`, `upgradeSelectedCard`, `upgradeCards`.
- Composition: `sequence`, `repeat`, `repeatForX`.

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

`ironclad-cards.spire` is currently authoritative for the 30 Ironclad combat cards and Slimed. `SpireCardDslCompiler` compiles those declarations directly into the existing immutable `CardDefinition` and `EffectNode` types, and `BaseSpirePackage` loads the compiled results. Card effects therefore pass through the existing combat lifecycle, including legal targeting and suspended choices. The parser/compiler run during package construction, never during a combat step.

The older `combat.spire` file remains a syntax and enemy-content prototype; its card declarations were removed to avoid a second source for the same content. Enemy definitions and behavior are still loaded through the existing runtime content path. The card migration does not yet claim that all combat content is DSL-authored.

## Completion gate for the first combat package

- Every card currently registered by `IroncladCards` has a source declaration, including upgrades, target mode, cost kind, rarity, and keywords.
- Every used effect operator compiles to a typed core effect. Unknown operators, wrong argument types, and unresolved card/Power names fail with source locations.
- DSL-compiled card definitions drive the existing core combat lifecycle and pass the same gameplay checks as the previous definitions.
- Multi-enemy tests prove selected-target cards can hit either enemy; area and random-target cards retain their distinct target behavior.
- Enemy behavior examples compile from named moves and preserve ordering, repeat limits, and RNG draws. This is a later increment after the card package is stable.
