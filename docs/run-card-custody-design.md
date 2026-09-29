# Run Card Custody DSL Design

## Scope

This design captures reusable run-level card operations required by the source-backed `ThievingHopperWeak` encounter. The encounter is a good test for the boundary between combat card instances and the persistent run deck: a card can leave both, remain attached to an enemy during combat, then return through a specific reward if that enemy dies.

## Source behavior

Sources: `ThievingHopper.cs`, `ThievingHopperWeak.cs`, `SwipePower.cs`, `FlutterPower.cs`, `EscapeArtistPower.cs`, and `SpecialCardReward.cs`.

- Thieving Hopper starts with `EscapeArtistPower(5)`. Its fixed move sequence is Thievery (steal one eligible card, then attack 17 / Deadly 19), Flutter (gain Flutter 5), Hat Trick (21 / Deadly 23), Nab (14 / Deadly 16), then Escape forever. HP is 79, or 84 with Tough Enemies.
- Thievery searches only the player's combat Draw and Discard piles, and only cards backed by a run-deck version. It checks four ordered priority groups: non-Imbued Uncommon; non-Imbued Common/Rare/Event; non-Imbued Basic/Quest; then Ancient or Imbued. It randomly selects within the first non-empty group on `combat-card-generation`.
- The selected card is removed from both its combat pile and the run deck. The exact card instance is stored on the Hopper's Swipe Power. Theft of a card with no run-deck version is not eligible.
- Flutter reduces powered attack damage against the Hopper by 50%. Each hit that causes unblocked damage consumes one stack. When its last stack is consumed, the Hopper is stunned for the next action and leaves hover mode.
- When the Hopper dies, Swipe returns its exact card as a specific-card reward and marks the current map history entry as loot returned. A successful theft marks the entry as loot stolen. The EscapeArtist counter is a display timer; its expiry does not itself escape the Hopper.

## Existing core boundary

The run stores persistent cards as `CardInstance` values with stable instance IDs. Combat receives those instances in its piles, while the run deck remains a separate list until room completion writes the combat result back. `CardDefinition` has rarity, and `CardInstance` carries arbitrary namespaced enchantment IDs. Generic add/remove effects and an effect predicate expose that metadata. The `stealRunCard` effect searches configured combat piles using ordered rarity/enchantment priority groups, selects from the first non-empty group on `combat-card-generation`, and removes the exact instance from both combat and the run deck. The owning Power stores the card instance, and `returnHeldRunCardReward` creates a one-option reward containing that exact card and source encounter ID. Map visit history exposes `LootStolen` and `LootReturned` flags on the source point. Effects update those flags only when custody changes; map points without a run visit remain unaffected.

These shared concepts are implemented without `if (enemy == ThievingHopper)` branches in combat or run completion. The current slice exercises them through one source-backed encounter.

## Proposed reusable semantics

1. **Card selection query**: a typed ordered list of fallback filters over cards in specified combat piles. Filters can inspect card rarity, run-deck backing, and enchantment metadata. A filter returns the first non-empty candidate group; the configured RNG stream selects within that group. Empty selection consumes no RNG call.
2. **Run-card custody**: a stable reference to an existing `CardInstance`, held by a combat entity or its Power instance. A single operation removes that instance from its combat pile and persistent run deck, then records the custody owner. Card identity, upgrade, and instance data survive custody.
3. **Specific-card reward**: a reward contains one exact card instance, a source encounter ID for presentation, and one-time claim state. Claiming transfers that instance into the post-combat run deck; it must not create a duplicate. If the holder dies and no reward can be queued, the card must remain recoverable according to the declared rule rather than silently disappear.
4. **Run history signals**: optional typed room-history events such as `loot_stolen` and `loot_returned`. They are observable run data; they must not control whether the card itself is removed or restored.
5. **Power data**: a Power can own a typed card reference as extension state, in addition to numeric counters. The owner-death trigger can pass the held card into a generic specific-card reward effect.

Current author-facing syntax:

```text
stealRunCard(
  SwipePower,
  from=[draw, discard],
  priorities=[
    priority(rarities=[uncommon], excludeEnchantments=[Imbued]),
    priority(rarities=[common, rare, event], excludeEnchantments=[Imbued]),
    priority(rarities=[basic, quest], excludeEnchantments=[Imbued]),
    priority(rarities=[ancient], requireEnchantments=[Imbued], matchAny=true)
  ]
)

returnHeldRunCardReward(SwipePower, sourceEncounter=ThievingHopperWeak)
```

The compiler lowers these declarations to validated typed effects at package load. Combat steps execute precompiled filters and mutate existing run/combat state. The Thieving Hopper definition exercises this syntax. The run observation exposes the source visit loot flags.

## Acceptance tests for this slice

- Repeated runs with the same seed and deck choose the same priority group and card instance; a higher-priority available card prevents lower groups from being considered.
- A stolen card disappears from its combat pile and run deck, retaining its stable instance ID in the Hopper's observable held-card state.
- Cards without a run-deck instance and cards outside the configured piles are not candidates.
- Killing the Hopper queues one specific-card reward. Claiming it restores exactly the original card instance to the run deck once; leaving the reward unclaimed does not duplicate it.
- Generated cards that lack a persistent run-card instance are never accidentally deleted from the run deck.
- Map history marks theft after a successful selection and return after a reward is queued, without affecting combat outcomes or RNG.

Flutter's damage reduction and stun timing are verified alongside the custody behavior using the existing damage, Power, and action-queue contracts.
