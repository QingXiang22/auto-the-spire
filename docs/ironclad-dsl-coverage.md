# Ironclad DSL coverage and remaining semantics

The source snapshot's `IroncladCardPool.GenerateAllCards()` lists 87 definitions. Pool membership, executable DSL, single-player eligibility, and source fidelity are separate checks. A declaration alone does not prove faithful execution. Unlock epochs are deliberately ignored; multiplayer restrictions still apply to gameplay pools.

## Source review of the remaining 34 definitions

The following review covers gameplay members in `sts2/src/Core/Models/Cards` and associated Powers, excluding presentation calls. These requirements guide shared DSL design; unimplemented rows must not be registered with empty effects or native card-specific handlers.

| Shared capability | Remaining source cards | Required semantics |
| --- | --- | --- |
| Card-play rules | Corruption, Unrelenting | Skill cost override and exhaust destination; next Attack costs zero and consumes a Free Attack stack before effects. X costs still spend current energy, as `CardEnergyCost.GetAmountToSpend` bypasses fixed-cost overrides. |
| Counted composition | Expect A Fight, Tear Asunder | Gain energy per Attack currently in hand, then prevent further energy gains until owner turn-end; repeat attacks from combat-wide positive unblocked HP-loss events. Counts are evaluated once when entering the composition. |
| Card-instance events | Drum of Battle, Stomp | The exhausted card grants energy; every Attack started reduces Stomp's cost for this turn, with combat-entry initialization from finished Attack history. |
| Automatic plays | Cascade, Havoc, Hellraiser, Howl from Beyond, Stampede | Play cards from draw, hand, or exhaust through the same lifecycle; preserve context, choices, target RNG, X snapshots, forced exhaustion, source phase order, and Hellraiser's special infinite-target cap. |
| Draw/exhaust composition | Dark Embrace, Fiend Fire, Pillage | Draw per exhaust with Ethereal draws deferred until after hand flush; snapshot hand count and exhaust original instances before attacking; draw repeatedly until a non-Attack, empty deck, or full hand. |
| Card generation/transformation | Infernal Blade, Primal Force, Stoke | Character pool, generation exclusions and RNG; generated free cost for this turn; transform hand Attacks into Giant Rock; exhaust snapshot then generate the same count, optionally upgraded. |
| Reactive damage/block | Colossus, Flame Barrier, Juggernaut, Rupture, Stone Armor, Unmovable | Conditional incoming multipliers; powered-hit retaliation even when blocked; random damage after positive block; defer card-caused Rupture Strength until card completes; Plating's early block and subsequent-turn decrement; first qualifying block plays doubled. Block/damage properties and source timing matter. |
| Stateful Powers | Crimson Mantle, Inferno, Vicious | Independent application count for self damage; player-turn HP-loss response; draw after owner applies positive Vulnerable stacks. |
| Card-instance mutation | Rampage, Thrash | Combat-local accumulated damage persists through upgrade/downgrade; Thrash samples an Attack, computes its modified damage, then exhausts it. |
| Start-of-turn movement/cloning | Aggression, Juggling | Shuffle discard Attacks into hand and upgrade them before draw; copy the third Attack of the turn; upgrade can add Innate. |
| Card replay | One-Two Punch | Next configured Attack plays execute twice and consume a stack; expiry at owner side-end. |
| Fatal outcome and run resource | Feed | Award permanent max HP only when that attack kills and target Powers permit Fatal; disable combat generation. |
| Multiplayer-only content | Demonic Shield, Tank | Preserve multiplayer-only metadata; exclude from single-player rewards and generation. Full ally targeting, damage sharing, and Guarded rules require multiplayer modeling. |

## Implemented slice: card rules and counted composition

Corruption, Unrelenting, Expect A Fight, and Tear Asunder now have executable DSL and gameplay checks. This brings the source pool to 57/87 definitions, with 30 remaining (including the two multiplayer-only cards). There are 59 card declarations in total, including Entrench and Slimed outside this pool.

Power DSL gains `cardRule = cardRule(type=skill, cost=0, resultPile=exhaust)` and `consumeOnPlay=true` for a rule that spends one stack when a matching card begins. Cost override applies to fixed costs only. One core resolver supplies observations, legal actions, energy payment, and the snapshotted result pile. Rules compose in active Power order; exact original model Hook ordering remains a separate fidelity task.

Default result placement follows `CardModel.GetResultPileTypeForCardPlay`: Power cards leave combat, Exhaust cards move to exhaust, and other cards move to discard after effects and after-play triggers. The card remains in play while effects or choices are running. A run's permanent deck is unaffected by combat result placement.

`repeatForCount(base, perCount, count=..., effect=...)` captures a count and schedules the existing effect node that many times. `pileCount(pile, type=attack)` filters by definition type; `playerHpLossCount()` reads the combat-wide positive HP-loss summary. This shared composition works with energy, damage, draws, or other effects without adding a scaled version of each command.

For a calculated amount that should be one command, `modifyEnergy(scaled(base, perCount, count=...))` shares the count resolver. Expect A Fight uses one resource command so it emits one energy event; repeated resource commands are a different behavior. Calculated amounts are currently exposed only on energy; expanding the value vocabulary is separate from adding content-specific runtime branches.

`preventEnergyGain = true` is Power metadata checked by the common energy-gain command. It blocks positive gains, allows spending/loss, and does not prevent next-turn energy reset. Expect A Fight gains energy before applying its lock.
