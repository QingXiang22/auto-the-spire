# Content DSL design (draft)

## Status and direction

This document describes the target content language. The current JSON enemy and encounter loader is an experiment that revealed runtime fields; its one-file-per-definition shape is not the design target and should not be extended by mechanically moving more C# records into JSON.

Design in this order:

1. Specify reusable concepts and author-facing syntax.
2. Express representative original content with that syntax.
3. Identify the runtime semantics those examples require.
4. Refine the state machine and compiler IR only for those demonstrated needs.
5. Implement the compiler, then migrate the rest of `base-spire` by content domain.

The content language describes *what rules mean*. The core owns *when rules run*, random-stream access, action legality, and observation projection. Compilation happens once when a rule package is assembled. A combat step must not parse JSON, resolve names, or walk a generic syntax tree.

## Authoring principles

- **Describe behavior, not runtime records.** Authors declare a move's effects and a behavior pattern; they do not manually wire `NextActionId` edges or repeat the same transition table on every action.
- **Compose reusable definitions.** Packages can import, extend, and refer to namespaced values, actions, effects, powers, cards, enemies, encounters, and maps. Shared definitions replace copy/pasted JSON and C# constructors.
- **Keep mechanics and presentation separate.** A move's effect is authoritative. Intent is a view hint derived from the effect where possible, with an explicit hint for ambiguous cases such as multi-hit, delayed, or conditional damage.
- **Make randomness explicit and auditable.** A `choose` expression identifies its random stream, candidate order, weighting, and whether it is a single correlated draw or several independent draws.
- **Use typed, bounded expressions.** Conditions read a documented set of state facts. Effects mutate state only through core operations. Content cannot execute arbitrary C#.
- **Prefer compile-time work.** Resolve names, validate references, expand reusable definitions, normalize difficulty values, and lower behaviors to execution plans before a session starts.
- **Provide a controlled escape hatch.** Mod-specific code uses named C# handlers/hooks with a typed input/output contract; it does not inject code into the DSL.

## Proposed package syntax

The concrete parser/format remains open. The examples use an indentation-based notation to show the intended abstractions, not a promise that this exact punctuation is final. The language should be readable in code review and should not require authors to understand JSON polymorphism.

A package is organized into domain modules (for example `combat/ironclad`, `enemies/overgrowth`, and `acts/overgrowth`) rather than one file per runtime record. A module can declare shared values and related definitions together. Imports and namespaces make dependencies explicit. Duplicate IDs are errors by default; mods must state whether they add a definition, replace one, or patch named fields/actions. Patch precedence and provenance are resolved once during package composition and exposed in diagnostics.

The source language has four author-facing declaration kinds, regardless of eventual file syntax: reusable values/profiles, content definitions, behavior/effect expressions, and package patches. It does not expose runtime fields such as action-array indexes, current action indexes, `NextActionId`, or internal enum names. A source module may contain many related definitions.

### Semantic model and compilation boundary

The compiler should lower source through three named stages:

1. **Package graph:** parse declarations, imports, IDs, source locations, and explicit add/replace/patch operations.
2. **Typed rule model:** resolve names and types; normalize difficulty/upgrade values; expand reusable definitions; validate targets, behavior branches, and effect capabilities. This model keeps behavior expressions distinct from effect expressions and observation hints.
3. **Execution plan:** lower the typed model to compact immutable plans consumed by the core. No DSL parser, JSON node, content name lookup, or reflection runs inside `step`.

Conceptually, the rule model has `Effect = Sequence | Attack | ApplyPower | AddCard | ...`, `Behavior = Cycle | StartThen | Choose | Branch | ...`, and `Projection = InferredIntent | ExplicitHint`. A package build fails with source-located diagnostics if an expression cannot be typed or compiled. The execution plan is data plus typed operation tags/delegates; the final representation is selected by representative throughput and allocation benchmarks.

Difficulty selectors are orthogonal to card upgrades. A value may provide a default and named overrides; profile resolution picks the most specific applicable override once at package compilation, with a documented fallback to default. Upgraded card values resolve from card instance upgrade level and must not be confused with difficulty profiles.

Random expressions declare candidate order, weighting, stream category, and draw grouping. Evaluating a lineup in listed order means each selection is encountered in that order; a correlated formation is one selection over whole lineups. The compiler may not silently reorder or merge random draws. Whether a one-candidate selection consumes a draw is defined by the named random operation and pinned by a call-count test.

For the source model, `DeadlyEnemies` and `ToughEnemies` are profile facts. A conditional value lists `default` plus zero or more `when <condition>` branches. At compile time, matching branches with the greatest number of constrained facts win; equally specific matches are a compile error unless they provide the same value. Thus an explicit `when DeadlyEnemies and ToughEnemies` handles the combined profile, otherwise a single-flag value may be used before `default`. Each random expression has a semantic stream default (`enemy_behavior`, `encounter_lineup`, `card_selection`, etc.); authors only override it when source evidence requires a different stream. The final names map to actual core random streams during lowering.

The minimum authoring forms used by the acceptance models are therefore:

```text
damage(default: number, when condition: number, ...)
apply Power with stacks(value) to target
attack target for damage-expression
choose [candidate, ...] [excluding prior-choice] [weighted by expression]
when condition -> behavior
```

Concrete delimiters and indentation rules can change without changing these semantic contracts.

```text
package base-spire

power Ritual {
  timing: enemy_turn_end
  effect: gain_power Strength by stacks
  first_tick: skip_when_applied_to_enemy
}

card Slimed {
  type: status
  cost: 1
  keywords: exhaust
  play: draw 1
}

card StrikeIronclad {
  type: attack
  cost: 1
  play: attack selected_enemy for damage(upgrade 0: 6, upgrade 1: 9)
}

value CultistHp {
  default: 52..54
  when ToughEnemies: 51..53
}

enemy DampCultist {
  hp: CultistHp
  moves: loop {
    incantation: apply Ritual with stacks(5) to self
    dark_strike: attack player for damage(default: 1, when DeadlyEnemies: 3)
  }
}
```

This models the original Damp Cultist cycle without explicitly encoding two action nodes and two transition edges. The Dark Strike's base damage is 1 normally and 3 with `DeadlyEnemies`; its HP range is independently changed by `ToughEnemies`. Named rule selectors express the original modifiers, and profile resolution is defined in one compiler stage rather than as untyped object substitution at arbitrary fields.

Strike and Slimed use the same effect vocabulary even though one is a player card and one is an enemy-created status card. Cost payment, target validation, pile movement, and the post-play window are core lifecycle rules; their content only declares cost, keywords, and effect.

A mod can patch a named semantic unit without copying the whole definition:

```text
package training-balance
depends base-spire

patch enemy base-spire:DampCultist {
  replace move dark_strike {
    attack player for damage(default: 2, when DeadlyEnemies: 4)
  }
}
```

The patch identifies both the target content and the move it replaces. Package composition must reject a missing target, an ambiguous patch, or two unordered replacements; it must not depend on file discovery order.

## Behavior patterns and action semantics

The DSL needs a small behavior algebra rather than a bespoke graph format for every enemy:

- `sequence { A; B; repeat }` for fixed cycles.
- `start A; then sequence { ... }` for fixed openings.
- `choose { A, B }` for weighted/random choice.
- `choose without_repeat { A, B }` for immediate-repeat prevention.
- `choose with_limits { A at_most 2_in_a_row; B at_most 1_in_a_row }` for bounded consecutive actions.
- `when condition -> behavior; otherwise -> behavior` for deterministic branches, evaluated in source order.
- `choose one formation from [...]` for one correlated draw over complete formations.

Examples:

```text
enemy LeafSlimeSmall {
  hp: default: 11..15, when ToughEnemies: 12..16
  behavior: choose without_repeat {
    tackle: attack player for damage(default: 3, when DeadlyEnemies: 4)
    goop: add 1 Slimed to player.discard
  }
}

enemy TwigSlimeMedium {
  hp: default: 26..29, when ToughEnemies: 27..29
  behavior: start sticky_shot; then choose with_limits {
    clump_shot at_most 2_in_a_row: attack player for damage(default: 11, when DeadlyEnemies: 12)
    sticky_shot at_most 1_in_a_row: add 1 Slimed to player.discard
  }
}

enemy Nibbit {
  hp: default: 42..46, when ToughEnemies: 44..48
  behavior:
    when living_enemies == 1 -> start butt
    when enemy_position == 1 -> start hiss
    when enemy_position == 0 -> start slice
    otherwise -> start butt
  moves: {
    butt: attack player for 12
    slice: attack player for damage(default: 6, when DeadlyEnemies: 7); gain 5 block
    hiss: apply Strength with stacks(default: 2, when DeadlyEnemies: 3) to self
  }
  after: cycle butt -> slice -> hiss -> butt
}
```

Behavior lowering must retain relevant history only. For the patterns above, the runtime needs a current move, prior move/count for repetition limits, and any explicitly declared counters; it should not retain an unbounded action log unless a rule asks for it.

## Effects, intent, and state-machine boundary

An effect is a typed tree/plan built from shared operations such as `attack`, `gain_block`, `apply_power`, `add_card`, `draw`, `heal`, `lose_hp`, and `choose`. Control constructs such as `sequence`, `if`, `repeat`, `random`, and player `choose` compose these operations. Conditions are read-only expressions over a documented state query API.

For attacks, the core computes effective damage from the base operation and current powers/modifiers. The same calculation feeds execution and default intent projection. Authors may add `intent: attack` or a richer preview annotation when the displayed intent cannot be inferred safely; the annotation never changes game mechanics.

The state machine owns phase order and resumes paused effects. DSL hooks attach to named lifecycle events (for example, `player_turn_start`, `enemy_turn_end`, `card_exhausted`) and return effect plans. A hook's declared reads/writes are checked against the event contract. A mod cannot reorder core phases by defining a new scheduler.

## Encounter composition

```text
encounter SlimesWeak {
  room: monster
  weak: true
  formation {
    first_small = choose [LeafSlimeSmall, TwigSlimeSmall]
    medium = choose [LeafSlimeMedium, TwigSlimeMedium]
    second_small = choose [LeafSlimeSmall, TwigSlimeSmall] excluding first_small
    lineup: [first_small, medium, second_small]
  }
}

encounter SlimesNormal {
  room: monster
  choose one formation {
    [TwigSlimeMedium, LeafSlimeMedium, LeafSlimeSmall, TwigSlimeSmall]
    [TwigSlimeMedium, LeafSlimeMedium, TwigSlimeSmall, LeafSlimeSmall]
  }
}
```

The weak lineup expresses three sequential draws directly: small, medium, then a different small. Compilation must preserve this declared draw order and test its RNG call count. The normal lineup chooses a whole formation with one draw, so small-enemy ordering stays correlated.

## What the original-content models require from the machine

The examples above are the acceptance model, not a reason to immediately add more runtime fields. They imply these candidate machine semantics, which should be confirmed against source before implementation:

| DSL concept | Runtime question to answer before changing the state machine |
| --- | --- |
| `moves: loop` / `sequence` | What exact turn phase chooses a move, executes its effect, and advances the behavior cursor? |
| `choose without_repeat` / `with_limits` | Which compact history is required, what is the candidate filtering order, and when is the enemy RNG consumed? |
| `when ... -> start` | Which facts are visible when the initial move is chosen, especially for multi-enemy encounters? |
| `apply Ritual(5)` | When does a newly applied Power first trigger, and is the skip-first behavior part of Ritual or a generic Power lifetime rule? |
| `attack ... for damage(...)` | Which damage modifiers are applied, how are values rounded, and how do observation and execution share the result? |
| `choose ... excluding ...` | Is a choice made at encounter-room creation or combat start, which named RNG stream is used, and where is the result stored? |
| `play` card effect | In what order are legality, energy payment, target selection, card movement, effect execution, and hooks processed? |

Resolve these questions through source mapping and executable examples before making the machine more granular. A demonstrated need can then be added to a small typed runtime contract, with replay, observation, and legal-action tests.

## Content-domain coverage

The target package language should cover these domains through shared primitives, not independent mini-languages:

1. Definitions and pools: characters, cards, enemies, powers, relics, potions, statuses, events, encounters, acts, map generation settings.
2. Effect operations: combat resources, card movement/creation, damage, powers, choices, room/run changes.
3. Triggers and behaviors: player/enemy turns, card lifecycle, combat lifecycle, run lifecycle, deterministic/random enemy choices.
4. Composition: namespaced imports, reusable fragments, profiles, add/replace/patch precedence, validation and provenance.
5. Projection metadata: intent, labels, prompts, visibility, legal-action constraints, and the training observation contract.

This list is a design boundary, not a claim that all domains are implemented. The first acceptance set should model Damp Cultist, Nibbit, both Slime encounters, one attack card, Ritual, and one mod-added content override using the same language.

## Compiler and runtime performance

The source format is not the execution format. Package loading should parse and type-check once, resolve references, apply package precedence, specialize profile values, and lower behavior/effects to compact immutable plans. Execution should use typed opcodes or precompiled typed delegates with no reflection and no string-based operator lookup. Benchmark an `observe/step` loop on representative combats before choosing the final lowering strategy; do not trade mod expressiveness for a premature custom bytecode VM.

Measure separately: package compilation, state transition, effect execution, observation projection, and allocation per step. A correct DSL model is the prerequisite for those benchmarks because it defines the representative workload.

## Acceptance gates before state-machine expansion

- The examples above and the first acceptance set can be expressed without one runtime-record-shaped file per action/entity.
- Equivalent definitions compile to the same semantic IR regardless of file ordering where order is not meaningful.
- The compiler reports source locations for unresolved names, invalid types, unreachable behavior branches, and conflicting mod patches.
- Tests compare model observations, legal actions, resulting state, and RNG consumption for original and modded examples.
- Only a demonstrated content requirement may add a new state-machine capability; its author-facing DSL form and tests land first.
