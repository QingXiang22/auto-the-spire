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
| Reactive damage/block | Colossus, Stone Armor, Unmovable | Conditional incoming multipliers; Plating's early block and subsequent-turn decrement; first qualifying block plays doubled. Block/damage properties and source timing matter. |
| Stateful Powers | Crimson Mantle, Inferno, Vicious | Independent application count for self damage; player-turn HP-loss response; draw after owner applies positive Vulnerable stacks. |
| Card-instance mutation | Thrash | Preserve fractional dynamic damage; sample an Attack, resolve its modified damage, then exhaust it. Rampage's integer growth uses declared instance variables and the shared played/selected context. |
| Start-of-turn movement/cloning | Aggression | Shuffle discard Attacks into hand and upgrade them before draw. |
| Card replay | One-Two Punch | Next configured Attack plays execute twice and consume a stack; expiry at owner side-end. |
| Fatal outcome and run resource | Feed | Award permanent max HP only when that attack kills and target Powers permit Fatal; disable combat generation. |
| Multiplayer-only content | Demonic Shield, Tank | Preserve multiplayer-only metadata; exclude from single-player rewards and generation. Full ally targeting, damage sharing, and Guarded rules require multiplayer modeling. |

## Implemented slice: card rules and counted composition

Corruption, Unrelenting, Expect A Fight, Tear Asunder, Cascade, Havoc, One-Two Punch, Drum of Battle, Rampage, Stomp, Juggling, Flame Barrier, Juggernaut, and Rupture now have executable DSL. The source pool covers 71/87 definitions, with 16 remaining (including the two multiplayer-only cards). There are 74 card declarations in total, including Entrench, Slimed, and Giant Rock outside this pool.

Power DSL gains `cardRule = cardRule(type=skill, cost=0, resultPile=exhaust)` and `consumeOnPlay=true` for a rule that spends one stack when a matching card begins. Cost override applies to fixed costs only. One core resolver supplies observations, legal actions, energy payment, and the snapshotted result pile. Rules compose in active Power order; exact original model Hook ordering remains a separate fidelity task.

Default result placement follows `CardModel.GetResultPileTypeForCardPlay`: Power cards leave combat, Exhaust cards move to exhaust, and other cards move to discard after effects and after-play triggers. The card remains in play while effects or choices are running. A run's permanent deck is unaffected by combat result placement.

`repeatForCount(base, perCount, count=..., effect=...)` captures a count and schedules the existing effect node that many times. `pileCount(pile, type=attack)` filters by definition type; `playerHpLossCount()` reads the combat-wide positive HP-loss summary. This shared composition works with energy, damage, draws, or other effects without adding a scaled version of each command.

For a calculated amount that should be one command, `modifyEnergy(scaled(base, perCount, count=...))` shares the count resolver. Expect A Fight uses one resource command so it emits one energy event; repeated resource commands are a different behavior. Calculated amounts are exposed on energy, automatic/generation counts, and selected-card cost modifiers; expanding the value vocabulary is separate from adding content-specific runtime branches.

`preventEnergyGain = true` is Power metadata checked by the common energy-gain command. It blocks positive gains, allows spending/loss, and does not prevent next-turn energy reset. Expect A Fight gains energy before applying its lock.

## Implemented slice: shared card lifecycle

`CombatCardPlay` creates nested card frames and puts initialize, before-play, effects, after-play, and placement continuations on the existing effect stack. Manual and automatic cards use the same path. Replay resolves the current instance's upgraded effect before each execution, so an upgrade applied during an earlier choice affects subsequent executions. Frame completion preserves that instance's current upgrade level.

`autoPlay` reserves a draw-pile batch before executing it, including shuffle as needed. Cascade uses the captured `energyX()` count; Havoc requests forced exhaustion. Tests distinguish this reservation from sequential draw-and-play, prove child X values and parent context restoration through three nested card frames, and cover automatic X replay without extra payment. One-Two Punch consumes a stack during count resolution; Free Attack consumption remains before each execution. Result placement and exhaustion dispatch happen once.

Drum of Battle uses `onExhaust` / `upgradedOnExhaust`, shared with direct pile moves, played exhaustion, and forced automatic exhaustion. An upgrade can change only the callback. Own-entry and before-/after-play card declarations are implemented; hand/exhaust automatic selection, post-play phase triggers, deferred Ethereal draw, and exact model Hook ordering remain open.

## Implemented slice: captured counts and combat generation

Fiend Fire and Stoke are authored entirely in DSL. Both bind the pre-exhaust hand count, snapshot the original instances through `forEachCard`, and finish all exhaust callbacks before their follow-up. Fiend Fire attacks 7 (upgraded 10) per original card, then exhausts itself. Stoke generates that original count from its owner's character pool, with replacement; the upgraded body upgrades generated instances. Callback draws survive in hand and do not increase either count.

`withCount` / `savedCount` bindings are immutable lexical execution context. Ordinary choices, single/multiple card choices, nested scopes, and nested automatic card plays are tested. Unbound, self-referencing, and out-of-scope names fail registry validation. Queued entries remain value types to avoid allocating an object for every scheduled node.

`generateCards` reserves all RNG results before insertion, deduplicates overlapping owner pools, applies `CanBeGeneratedInCombat` and source rarity exclusions, and dispatches insertion events sequentially through the existing queue. A generated-card event can pause for a choice without changing the reserved batch. Tests prove fresh instance identities, duplicate samples, upgraded generation, owner identity independent of player ID, precise random call count, zero-count behavior, and generation exclusions. Generation that samples requires a declared character. With-replacement generation requires an eligible pool; an empty distinct pool returns no cards. The complete pool and its source ordering, multiplayer eligibility, hand capacity/overflow, and full generation hooks remain open.

The current pool covers 71/87 source definitions; 16 remain: Aggression, Colossus, Crimson Mantle, Dark Embrace, Demonic Shield, Feed, Hellraiser, Howl from Beyond, Inferno, Pillage, Stampede, Stone Armor, Tank, Thrash, Unmovable, and Vicious. Power event subscriptions now match event type, owner role, positive amount, damage properties, and owner-turn constraints, then queue ordinary effects with the other event actor as target context. Card-caused damage can be accumulated by Power and card instance, then consumed at that card's after-play trigger. Flame Barrier retaliates against powered hits, even when block absorbs them; Juggernaut deals random unpowered damage after positive block; Rupture defers card-caused Strength until the card completes and applies Strength immediately for other qualifying damage. Remaining reactive cards need additional shared state and turn semantics.

Juggling uses the shared `afterCardPlayed` Attack trigger, an exact `cardPlaysThisTurn` history predicate, dynamic clone count from the owning Power, and `upgradedKeywords = [innate]`. Run combat setup moves upgraded Innate instances into the opening hand before filling remaining slots from the shuffled deck.

## Implemented slice: instance cost and transformation

Infernal Blade, Primal Force, and Rampage now have executable DSL. Giant Rock is a separate Token declaration outside Ironclad's pool. The package has 74 card declarations, 71 of the 87 source pool members. Infernal Blade filters owner generation to Attacks, shuffles the full eligible pool for distinct selection, and installs a zero energy-cost modifier before insertion. Its upgrade lowers its own cost from 1 to 0. An empty filtered pool creates no card while Infernal Blade still exhausts. Rampage reads and increases its declared ExtraDamage value after each hit; upgrades preserve accumulated growth, while downgrade clears only variables not marked for preservation.

Local cost modifiers are ordered immutable instance data, shared by generation and selected-card commands. Set/add operations, reduce-only behavior, turn/played/combat lifetimes, moves, upgrades, and source upgrade caps are implemented. Fixed costs resolve local modifiers before Power cost rules; X spending is preserved. Replay completes before after-play expiration; exhaustion choices also complete first. Turn expiration waits for flush choices, covers all piles, and can itself dispatch resumable event effects. Controlled C# effect handlers preserve selected-card context when invoking the same commands.

Primal Force snapshots hand Attacks and replaces each in place with a fresh Giant Rock, upgraded only when Primal Force is upgraded. The old card's upgrade and cost modifiers are discarded, retained-instance marks are removed, and non-Attacks are preserved. Transformations use a separate event and do not masquerade as ordinary generation or exhaustion. Tests verify selection identity, position, resulting damage, JSON round trips, expiration boundaries, precise full-pool shuffle consumption, and invalid definition rejection.

Own-entry callbacks are implemented; complete transformation/enchantment hooks, star-cost resources, mode eligibility, full pool order, and hand capacity remain open. Stomp uses these local modifiers through shared callbacks. Combat cloning copies current variable offsets, upgrade state, and local costs; Anger uses this shared operation. Clone ancestry, enchantments, and full cross-model listener ordering remain open.

## Implemented slice: card callbacks and started/finished history

Stomp is authored in DSL: 3 energy, 12 (upgraded 15) area damage; its own-entry callback lowers cost by finished Attacks this side-turn, except for clones, and its before-play listener lowers it by one for each Attack execution. The cost payment uses the preview before its own listener. Replays count each execution. Zero relative changes do not create no-op modifiers, matching source AddThisTurn.

The common lifecycle queues before-card callbacks, records the start after they finish, executes the body, records completion, then queues after-card callbacks. Choices and nested automatic cards preserve these boundaries. A dead player's interrupted card does not count as finished or run remaining replay phases. Listener IDs are snapshotted in source pile order and each live instance's current callback is resolved when dispatched; this supports an upgrade performed by an earlier listener, and ignores a listener that subsequently leaves combat. Existing Power consumption and after-play Power triggers precede card listeners; full source priority across other models remains open.

Own-entry callbacks cover initial instances, fixed/random generation and transformation, and run before final insertion events. Fixed card additions and combat clones use the same sequential placement path as random generation. Initial entry choices can pause before combat_started, with the run owner already attached. IsClone is immutable instance metadata preserved by upgrades and cost changes; Stomp skips clone initialization. A unified HasUpgrade predicate includes callback-only upgrades in combat, run choices and rewards. Registry validation includes callbacks, lexical names, variables and history filters.

Gameplay checks cover Stomp and instance variables, Rampage growth through replay/turn/upgrades, played/selected context across nested cards and choices, clone independence, JSON round trips and invalid definitions. The package covers 71/87 source pool cards and 74 declarations. All remaining source cards and full monster migration remain required for the active objective.
