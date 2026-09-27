# Combat content DSL

This document describes the current authoring boundary for `core-the-spire` and `base-spire`. The language declares combat content; the core owns execution order, legal actions, power timing, target resolution, and RNG streams. Source is parsed and compiled while packages are built. Combat steps consume typed definitions and do not interpret DSL syntax.

## Authoring model

The package DSL has three shared building blocks:

- **Declarations:** cards, Powers, enemies, and their stable package-scoped names.
- **Expressions:** effects such as `attack`, `applyPower`, `gainBlock`, and `addCards`.
- **Behavior composition:** named enemy moves connected by `cycle`, `loop`, `startWith`, and random choice expressions.

An enemy's effect and its behavior remain separate. For example, an attack expression says what the move does; `startWith(..., then=loop(...))` says when that move is selected. The compiler lowers both to the current typed `EffectNode`, `PowerDefinition`, and `EnemyDefinition` model. The combat state machine is refined only when a source behavior cannot be represented faithfully.

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

- [`ironclad-cards.spire`](../mod-the-spire/base-spire/BaseSpire/ContentSource/ironclad-cards.spire) declares all 30 currently registered Ironclad combat cards, including upgrades, costs, targeting, keywords, and effects.
- [`enemies.spire`](../mod-the-spire/base-spire/BaseSpire/ContentSource/enemies.spire) declares Ritual and all nine enemy definitions currently registered by `base-spire`: Damp and Calcified Cultists, Fuzzy Wurm Crawler, Shrinker Beetle, Nibbit, and four Leaf/Twig Slimes.
- `base-spire.encounters.json` still declares encounter lineups and the Nibbit slot-specific opening actions. Encounter authoring is a separate migration step.

Enemy behaviors in the current roster exercise fixed cycles, a one-time opening move, repeated moves, random choices without immediate repeats, and consecutive-use limits. Gameplay tests exercise normal and Deadly/Tough profiles through the real compiled package. JSON enemy definitions and their separate loading path have been removed; encounters remain JSON.

## Scope still open

The nine enemies are the roster currently modeled by this repository; this is not a claim that the full original game's monster catalog has been imported. The source audit identifies substantial gameplay still to implement, including monsters not yet registered here, role-specific behavior in the encounter source, conditional action state, and move-log cooldown rules. Those should be authored in this DSL when their source semantics are implemented. They should not be approximated by changing the state machine to match one content fixture.

Mod package composition, explicit replace/patch precedence, source provenance, and performance benchmarks remain open. The current compiler is a first semantic slice rather than the final mod API.
