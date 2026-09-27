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

## Current source of truth

- [`ironclad-cards.spire`](../mod-the-spire/base-spire/BaseSpire/ContentSource/ironclad-cards.spire) declares 55 cards currently associated with Ironclad combat, including the Event card Entrench and status card Slimed. The Ironclad pool contains 53 of the source's 87 cards, each marked with `pools = [IroncladCardPool]`.
- [`ironclad-powers.spire`](../mod-the-spire/base-spire/BaseSpire/ContentSource/ironclad-powers.spire) declares the current Feel No Pain and Rage Power triggers the Pyre maximum-energy and Cruelty vulnerable-damage modifiers, and the Setup Strike/Mangle Power lifecycles.
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

The remaining Ironclad cards expose several shared language needs. Rampage and Thrash change values on a particular card instance, while Stomp changes its cost during a turn; these call for declared instance variables and cost modifiers with explicit reset rules. Rage's filtered card-play trigger is now modeled; Dark Embrace additionally distinguishes normal exhaustion from Ethereal exhaustion and defers draws until after the hand flush. Stone Armor's Plating uses a turn-boundary phase before damage effects, and Howl from Beyond automatically plays from the exhaust pile. Primal Force transforms card instances. Tank and Demonic Shield declare multiplayer constraints even though the current training slice runs single-player combat. These source cases should drive the next core contracts rather than receive individual native card handlers.

The original `IroncladCardPool` explicitly lists 87 members and remains the coverage reference. Explicit DSL pool membership is implemented and supports third-party extensions; mode eligibility and the full source pool remain open. Account unlock progress is outside this project's scope, so all otherwise eligible original cards are treated as unlocked.

Mod package composition, explicit replace/patch precedence, source provenance, and performance benchmarks remain open. The current compiler is a first semantic slice rather than the final mod API.
