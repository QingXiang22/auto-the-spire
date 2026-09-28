# Ironclad DSL coverage and remaining semantics

The source snapshot's `IroncladCardPool.GenerateAllCards()` lists 87 definitions. Pool membership, executable DSL, single-player eligibility, and source fidelity are separate checks. A declaration alone does not prove faithful execution. Unlock epochs are deliberately ignored; multiplayer restrictions still apply to gameplay pools.

## Remaining shared semantics

The following review covers gameplay members in `sts2/src/Core/Models/Cards` and associated Powers, excluding presentation calls. These requirements guide shared DSL design; unimplemented rows must not be registered with empty effects or native card-specific handlers.

| Shared capability | Remaining source cards | Required semantics |
| --- | --- | --- |
| Automatic play on draw | Hellraiser | Play each drawn Attack through the common card lifecycle; bound loops that draw into another Attack and preserve the source cap. |
| Reactive block | Unmovable | Double the first qualifying block gains each turn up to the Power amount, with the same block-source and owner-turn rules as the game. |
| Card-instance mutation | Thrash | Preserve fractional dynamic damage; select an Attack, resolve its modified damage, add it to Thrash, then exhaust the selected card. |
| Multiplayer-only content | Demonic Shield, Tank | Preserve multiplayer-only metadata; exclude from single-player rewards and generation. Full ally targeting, damage sharing, and Guarded rules require multiplayer modeling. |

## Implemented slice: card rules and counted composition

Corruption, Unrelenting, Expect A Fight, Tear Asunder, Cascade, Havoc, One-Two Punch, Drum of Battle, Rampage, Stomp, Juggling, Flame Barrier, Juggernaut, Rupture, Dark Embrace, Vicious, Stone Armor, Pillage, Colossus, Aggression, Crimson Mantle, Inferno, Feed, Stampede, and Howl from Beyond now have executable DSL. The source pool covers 82/87 definitions, with 5 remaining (including the two multiplayer-only cards). There are 85 card declarations in total, including Entrench, Slimed, and Giant Rock outside this pool.

Power DSL gains `cardRule = cardRule(type=skill, cost=0, resultPile=exhaust)` and `consumeOnPlay=true` for a rule that spends one stack when a matching card begins. Cost override applies to fixed costs only. One core resolver supplies observations, legal actions, energy payment, and the snapshotted result pile. Rules compose in active Power order; exact original model Hook ordering remains a separate fidelity task.

Default result placement follows `CardModel.GetResultPileTypeForCardPlay`: Power cards leave combat, Exhaust cards move to exhaust, and other cards move to discard after effects and after-play triggers. The card remains in play while effects or choices are running. A run's permanent deck is unaffected by combat result placement.

`repeatForCount(base, perCount, count=..., effect=...)` captures a count and schedules the existing effect node that many times. `pileCount(pile, type=attack)` filters by definition type; `playerHpLossCount()` reads the combat-wide positive HP-loss summary. This shared composition works with energy, damage, draws, or other effects without adding a scaled version of each command.

`drawUntil(type=attack)` draws one card at a time, including the first card whose type differs, and resumes its loop only after draw-triggered effects resolve. It stops when the draw source is empty, drawing is prevented, or the hand reaches its 10-card limit.

`randomMoveCards` filters and shuffles the source pile once, reserves up to the requested count, then moves cards one by one and runs an optional effect with each moved card as context. Aggression uses this to shuffle the discard Attacks, move up to its Power amount into hand, and upgrade each card before processing the next.

For a calculated amount that should be one command, `modifyEnergy(scaled(base, perCount, count=...))` shares the count resolver. Expect A Fight uses one resource command so it emits one energy event; repeated resource commands are a different behavior. Calculated amounts are exposed on energy, automatic/generation counts, and selected-card cost modifiers; expanding the value vocabulary is separate from adding content-specific runtime branches.

`preventEnergyGain = true` is Power metadata checked by the common energy-gain command. It blocks positive gains, allows spending/loss, and does not prevent next-turn energy reset. Expect A Fight gains energy before applying its lock.

## Implemented slice: shared card lifecycle

`CombatCardPlay` creates nested card frames and puts initialize, before-play, effects, after-play, and placement continuations on the existing effect stack. Manual and automatic cards use the same path. Replay resolves the current instance's upgraded effect before each execution, so an upgrade applied during an earlier choice affects subsequent executions. Frame completion preserves that instance's current upgrade level.

`autoPlay` reserves a draw-pile batch before executing it, including shuffle as needed. `autoPlayRandomCards` selects again from the live source pile after each nested play; `autoPlaySelectedCard` moves the selected instance from a chosen pile into the common card lifecycle. Cascade uses the captured `energyX()` count; Havoc requests forced exhaustion; Stampede uses live random hand selection; Howl from Beyond replays its selected exhausted instance at the after-auto-post-play phase. One-Two Punch consumes a stack during count resolution; Free Attack consumption remains before each execution. Result placement and exhaustion dispatch happen once.

Drum of Battle uses `onExhaust` / `upgradedOnExhaust`, shared with direct pile moves, played exhaustion, and forced automatic exhaustion. An upgrade can change only the callback. Own-entry, before-/after-play, and after-auto-post-play card declarations are implemented; draw-triggered automatic play, deferred Ethereal draw, and exact model Hook ordering remain open.

## Implemented slice: captured counts and combat generation

Fiend Fire and Stoke are authored entirely in DSL. Both bind the pre-exhaust hand count, snapshot the original instances through `forEachCard`, and finish all exhaust callbacks before their follow-up. Fiend Fire attacks 7 (upgraded 10) per original card, then exhausts itself. Stoke generates that original count from its owner's character pool, with replacement; the upgraded body upgrades generated instances. Callback draws survive in hand and do not increase either count.

`withCount` / `savedCount` bindings are immutable lexical execution context. Ordinary choices, single/multiple card choices, nested scopes, and nested automatic card plays are tested. Unbound, self-referencing, and out-of-scope names fail registry validation. Queued entries remain value types to avoid allocating an object for every scheduled node.

`generateCards` reserves all RNG results before insertion, deduplicates overlapping owner pools, applies `CanBeGeneratedInCombat` and source rarity exclusions, and dispatches insertion events sequentially through the existing queue. A generated-card event can pause for a choice without changing the reserved batch. Tests prove fresh instance identities, duplicate samples, upgraded generation, owner identity independent of player ID, precise random call count, zero-count behavior, and generation exclusions. Generation that samples requires a declared character. With-replacement generation requires an eligible pool; an empty distinct pool returns no cards. The complete pool and its source ordering, multiplayer eligibility, hand capacity/overflow, and full generation hooks remain open.

The current pool covers 82/87 source definitions; 5 remain: Demonic Shield, Hellraiser, Tank, Thrash, and Unmovable. Power event subscriptions now match event type, owner role, positive amount, damage properties, owner-turn constraints, ethereal-exhaustion cause, and affected Power ID, then queue ordinary effects with the other event actor as target context. Card-caused damage can be accumulated by Power and card instance, then consumed at that card's after-play trigger. Event counts can also be accumulated until the owner's turn-end trigger. Flame Barrier retaliates against powered hits, even when block absorbs them; Juggernaut deals random unpowered damage after positive block; Rupture defers card-caused Strength until the card completes and applies Strength immediately for other qualifying damage; Dark Embrace draws immediately for ordinary exhausts and defers ethereal exhaust draws until after hand cleanup; Vicious draws when its owner applies positive Vulnerable stacks. Power DSL lifecycle hooks run at owner turn boundaries and the after-auto-post-play phase; Stone Armor uses owner-turn hooks to decrement Plating at turn start and grant its current block at turn end, while Stampede plays from a live random hand selection at auto-post-play.

Power-local variables are declared with `variables = [variable(Name, initialValue)]`, read with `powerVariable(Power, Name)`, and changed with `modifyPowerVariable(Power, Name, amount)`. Values initialize when the Power becomes active, persist as its stack count changes, and clear when it is removed. Combat observations expose the active variables for the player and each enemy so policies can reason from the same state the engine uses.

Attack effects accept `onFatal=...`, which runs only when the hit kills and the victim has no active Power marked `preventsFatal=true`. The `gainMaxHp` combat effect updates current combat HP and max HP, and updates the owning Run when combat began from one. Feed uses this callback and remains excluded from combat generation.

Juggling uses the shared `afterCardPlayed` Attack trigger, an exact `cardPlaysThisTurn` history predicate, dynamic clone count from the owning Power, and `upgradedKeywords = [innate]`. Run combat setup moves upgraded Innate instances into the opening hand before filling remaining slots from the shuffled deck.

## Implemented slice: instance cost and transformation

Infernal Blade, Primal Force, Rampage, Crimson Mantle, Inferno, Feed, Stampede, and Howl from Beyond now have executable DSL. Giant Rock is a separate Token declaration outside Ironclad's pool. The package has 85 card declarations, 82 of the 87 source pool members. Infernal Blade filters owner generation to Attacks, shuffles the full eligible pool for distinct selection, and installs a zero energy-cost modifier before insertion. Its upgrade lowers its own cost from 1 to 0. An empty filtered pool creates no card while Infernal Blade still exhausts. Rampage reads and increases its declared ExtraDamage value after each hit; upgrades preserve accumulated growth, while downgrade clears only variables not marked for preservation. Crimson Mantle and Inferno express per-Power escalating self damage without content-specific runtime branches. Feed composes attack, Fatal callback, and permanent max-HP gain. Stampede and Howl from Beyond use shared live-random and selected-card autoplay operations.

Local cost modifiers are ordered immutable instance data, shared by generation and selected-card commands. Set/add operations, reduce-only behavior, turn/played/combat lifetimes, moves, upgrades, and source upgrade caps are implemented. Fixed costs resolve local modifiers before Power cost rules; X spending is preserved. Replay completes before after-play expiration; exhaustion choices also complete first. Turn expiration waits for flush choices, covers all piles, and can itself dispatch resumable event effects. Controlled C# effect handlers preserve selected-card context when invoking the same commands.

Primal Force snapshots hand Attacks and replaces each in place with a fresh Giant Rock, upgraded only when Primal Force is upgraded. The old card's upgrade and cost modifiers are discarded, retained-instance marks are removed, and non-Attacks are preserved. Transformations use a separate event and do not masquerade as ordinary generation or exhaustion. Tests verify selection identity, position, resulting damage, JSON round trips, expiration boundaries, precise full-pool shuffle consumption, and invalid definition rejection.

Own-entry callbacks are implemented; complete transformation/enchantment hooks, star-cost resources, mode eligibility, full pool order, and hand capacity remain open. Stomp uses these local modifiers through shared callbacks. Combat cloning copies current variable offsets, upgrade state, and local costs; Anger uses this shared operation. Clone ancestry, enchantments, and full cross-model listener ordering remain open.

## Implemented slice: card callbacks and started/finished history

Stomp is authored in DSL: 3 energy, 12 (upgraded 15) area damage; its own-entry callback lowers cost by finished Attacks this side-turn, except for clones, and its before-play listener lowers it by one for each Attack execution. The cost payment uses the preview before its own listener. Replays count each execution. Zero relative changes do not create no-op modifiers, matching source AddThisTurn.

The common lifecycle queues before-card callbacks, records the start after they finish, executes the body, records completion, then queues after-card callbacks. Choices and nested automatic cards preserve these boundaries. A dead player's interrupted card does not count as finished or run remaining replay phases. Listener IDs are snapshotted in source pile order and each live instance's current callback is resolved when dispatched; this supports an upgrade performed by an earlier listener, and ignores a listener that subsequently leaves combat. Existing Power consumption and after-play Power triggers precede card listeners; full source priority across other models remains open.

Own-entry callbacks cover initial instances, fixed/random generation and transformation, and run before final insertion events. Fixed card additions and combat clones use the same sequential placement path as random generation. Initial entry choices can pause before combat_started, with the run owner already attached. IsClone is immutable instance metadata preserved by upgrades and cost changes; Stomp skips clone initialization. A unified HasUpgrade predicate includes callback-only upgrades in combat, run choices and rewards. Registry validation includes callbacks, lexical names, variables and history filters.

Gameplay checks cover Stomp and instance variables, Rampage growth through replay/turn/upgrades, played/selected context across nested cards and choices, clone independence, JSON round trips and invalid definitions. The package covers 82/87 source pool cards and 85 declarations. All remaining source cards and full monster migration remain required for the active objective.
