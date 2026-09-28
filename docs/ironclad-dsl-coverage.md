# Ironclad DSL coverage and remaining semantics

The source snapshot's `IroncladCardPool.GenerateAllCards()` lists 87 definitions. Pool membership, executable DSL, single-player eligibility, and source fidelity are separate checks. A declaration alone does not prove faithful execution. Unlock epochs are deliberately ignored; multiplayer restrictions still apply to gameplay pools.

## Source review of the initial 34-card gap

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

Corruption, Unrelenting, Expect A Fight, Tear Asunder, Cascade, Havoc, One-Two Punch, and Drum of Battle now have executable DSL and gameplay checks. The source pool covers 61/87 definitions, with 26 remaining (including the two multiplayer-only cards). There are 63 card declarations in total, including Entrench and Slimed outside this pool.

Power DSL gains `cardRule = cardRule(type=skill, cost=0, resultPile=exhaust)` and `consumeOnPlay=true` for a rule that spends one stack when a matching card begins. Cost override applies to fixed costs only. One core resolver supplies observations, legal actions, energy payment, and the snapshotted result pile. Rules compose in active Power order; exact original model Hook ordering remains a separate fidelity task.

Default result placement follows `CardModel.GetResultPileTypeForCardPlay`: Power cards leave combat, Exhaust cards move to exhaust, and other cards move to discard after effects and after-play triggers. The card remains in play while effects or choices are running. A run's permanent deck is unaffected by combat result placement.

`repeatForCount(base, perCount, count=..., effect=...)` captures a count and schedules the existing effect node that many times. `pileCount(pile, type=attack)` filters by definition type; `playerHpLossCount()` reads the combat-wide positive HP-loss summary. This shared composition works with energy, damage, draws, or other effects without adding a scaled version of each command.

For a calculated amount that should be one command, `modifyEnergy(scaled(base, perCount, count=...))` shares the count resolver. Expect A Fight uses one resource command so it emits one energy event; repeated resource commands are a different behavior. Calculated amounts are currently exposed only on energy; expanding the value vocabulary is separate from adding content-specific runtime branches.

`preventEnergyGain = true` is Power metadata checked by the common energy-gain command. It blocks positive gains, allows spending/loss, and does not prevent next-turn energy reset. Expect A Fight gains energy before applying its lock.

## Implemented slice: shared card lifecycle

`CombatCardPlay` creates nested card frames and puts initialize, before-play, effects, after-play, and placement continuations on the existing effect stack. Manual and automatic cards use the same path. Replay resolves the current instance's upgraded effect before each execution, so an upgrade applied during an earlier choice affects subsequent executions. Frame completion preserves that instance's current upgrade level.

`autoPlay` reserves a draw-pile batch before executing it, including shuffle as needed. Cascade uses the captured `energyX()` count; Havoc requests forced exhaustion. Tests distinguish this reservation from sequential draw-and-play, prove child X values and parent context restoration through three nested card frames, and cover automatic X replay without extra payment. One-Two Punch consumes a stack during count resolution; Free Attack consumption remains before each execution. Result placement and exhaustion dispatch happen once.

Drum of Battle uses `onExhaust` / `upgradedOnExhaust`, shared with direct pile moves, played exhaustion, and forced automatic exhaustion. An upgrade can change only the callback. General card event declarations, hand/exhaust automatic selection, post-play phase triggers, deferred Ethereal draw, and exact model Hook ordering remain open.
