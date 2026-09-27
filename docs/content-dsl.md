# Content DSL

## Goals and compilation boundary

Content files describe reusable game rules. They do not execute arbitrary C# and do not mutate combat state directly. The loader compiles files into the immutable definitions already consumed by `ContentRegistry`; the state machine remains responsible for ordering, random streams, validation, and player observations. This keeps mod content data-driven while keeping runtime rule execution typed and reviewable.

The first vertical slice covers enemy and encounter content. Enemy documents describe health ranges, action intent, effect trees, fixed/weighted/conditional action transitions, and difficulty-selected values. Encounter documents describe ordered fixed enemy lists, independently sampled slots with optional within-group uniqueness, and weighted correlated full formations. The loader compiles these documents into the same typed definitions used by hand-written packages, so the combat and RNG semantics remain centralized in the core.

## Enemy document, version 1

An enemy is a single JSON document with `schemaVersion: 1` and an `enemy` object. IDs always use the package namespace. An action keeps three concepts explicit:

- `intent` is the player-facing projection.
- `effect` is the rule operation, expressed using the existing Effect DSL.
- `nextActionId` or `transition` describes the state-machine edge. These are mutually exclusive.

`startingActionId` and `startingTransition` choose the initial state. Weighted transitions use the enemy AI random stream and can declare repeat restrictions. `firstMatchingCondition` transitions evaluate choices in file order and do not draw randomness; an unconditional final choice acts as a fallback.

Integer fields may use `{"$variant":{"default":N,"deadly":N,"tough":N,"deadlyTough":N}}`. The loader selects the most specific value available for the active difficulty profile, then compiles the document into ordinary integer-valued core definitions. Variant selection is a package-load operation, not a per-turn interpreter feature.

Conditions are typed data nodes (`metric`, `power`, `all`, `any`, `not`). Effects are typed data nodes (`sequence`, `attack`, `applyPower`, and the other registered operators). Unknown operators, unresolved IDs, unsupported schema versions, and invalid transition graphs fail during content loading or `ContentRegistry.Build()`.

## Example: Nibbit

```json
{
  "schemaVersion": 1,
  "enemy": {
    "id": "base-spire:nibbit",
    "minHp": {"$variant": {"default": 42, "tough": 44}},
    "maxHp": {"$variant": {"default": 46, "tough": 48}},
    "actions": [
      {
        "id": "base-spire:nibbit-butt",
        "effect": {"op": "attack", "amount": 12},
        "intent": "attack",
        "intentDamage": 12,
        "nextActionId": "base-spire:nibbit-slice"
      }
    ]
  }
}
```

The checked-in [Nibbit document](../mod-the-spire/base-spire/BaseSpire/Content/nibbit.enemy.json) contains its three-action cycle, difficulty variants, and Alone/Front starting conditions. Eight more documents define the Damp and Calcified Cultists, the small and medium Leaf/Twig Slimes, Fuzzy Wurm Crawler, and Shrinker Beetle. They exercise fixed action edges, random choices with immediate-repeat prevention, consecutive-use limits, starting actions, difficulty values, applied Powers and targets, attacks, and adding status cards to a pile. The combat tests load these through the same embedded-resource path used by `base-spire` and verify their action timing and normal, tough, and deadly values against the source audit. The cultists' ordered normal encounter is also data-driven; Ritual's delayed first trigger remains a core Power rule. Fuzzy Wurm weak, Shrinker Beetle weak, and Overgrowth Crawlers lineups also come from the encounter DSL.

## Encounter documents, version 1

An encounter document has `schemaVersion: 1` and an `encounters` array. Every encounter defines exactly one content source:

- `enemyIds` is an ordered, fixed lineup and permits repeated IDs.
- `enemySlots` defines one candidate set per position. `chooseRandomly` marks a sampled slot; `uniqueChoiceGroup` prevents duplicate picks among slots sharing that group. Each sampled slot uses the encounter RNG in slot order.
- `formations` defines correlated complete lineups. One weighted formation is selected with one encounter RNG draw, preserving relationships between positions.

All enemy references, encounter IDs, and room types are validated when the package content registry is built. The checked-in [base-spire encounter document](../mod-the-spire/base-spire/BaseSpire/Content/base-spire.encounters.json) demonstrates each source: Nibbit fixed lineups, the weak slime's random slots with distinct small slimes, and normal slimes' correlated formations. Existing gameplay checks assert lineup composition and random draw counts through the real package loader.

## Deliberate boundary

The DSL describes game content, not transport or UI details. External adapters map intents to observations and player decisions to actions. Rules that need behavior outside the registered operators use a named C# extension handler; a content document cannot inject code. JSON content assets and custom handlers are composed by the package before `ContentRegistry.Build()`.
