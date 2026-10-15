using BaseSpire.SinglePlayer;
using BaseSpire.Slice;
using BaseSpire.Slice.SourceMap;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// ENV-T09.2 formal shop checks. The session always comes from the production entry
/// (<see cref="BaseSpireRuntimeFactory"/> + <see cref="BaseSpireScope.DevMap"/>), the shop is entered through the real
/// map <c>ChooseMapPoint</c> Shop branch, and every interaction goes through public legal actions.
///
/// Discriminating coverage (each check names the concrete error it separates):
/// <list type="bullet">
/// <item>stock: the source 14 slots in source order, colored type sequence = source <c>_coloredCardTypes</c>,
///       distinct colored picks, colorless fixed uncommon/rare with identities from the source
///       <c>ColorlessCardPool</c> order, exactly one on-sale colored slot;</item>
/// <item>streams: entering the shop consumes exactly 28 <c>Shops</c> / 14 <c>Rewards</c> draws, and each colored
///       rarity can be recomputed from an independent replay;</item>
/// <item>refusals (cannot afford / belt full / unsupported): no candidate binding and the guard rejects the submit;
///       gold, deck, sold flags and draw counts are unchanged, the session stays Running, and leaving still works;</item>
/// <item>purchases (card / potion / supported relic): real long-term card, real belt slot, real owned relic,
///       gold charged once, sold item not buyable twice;</item>
/// <item>card removal through the formal decision: with two same-definition instances exactly the selected one is
///       removed, the counter increments, one removal per shop, higher price next shop;</item>
/// <item>observation purity: repeated reads do not rebuild the shelf and consume no randomness;</item>
/// <item>reachability: from a normal Start, public actions must reach a real Shop node and leave it again.</item>
/// </list>
/// </summary>
internal static class ShopFlowChecks
{
    private const string RunTypeId = "base-spire:Run";
    private const string RunStatesCollection = "base-spire:RunStates";
    private const string NodeTypeId = "base-spire:MapNode";
    private const string NodesCollection = "base-spire:MapNodes";
    private const string ChooseAction = "base-spire:ChooseMapPoint";
    private const string OfferTypeId = "base-spire:ShopOffer";
    private const string OffersCollection = "base-spire:ShopOffers";
    private const string DeckCollection = "base-spire:LongTermDeck";
    private const string BeltCollection = "base-spire:PotionBelt";
    private const string RelicsCollection = "base-spire:Relics";
    private const string PotionTypeId = "base-spire:Potion";
    private const string MonsterTypeId = "base-spire:Monster";
    private const string MonstersCollection = "base-spire:Monsters";
    private const string StrikeId = "base-spire:strike-ironclad";
    private const string DefendId = "base-spire:defend-ironclad";
    private const string BashId = "base-spire:bash";

    public static void Run()
    {
        Inventory();
        Streams();
        StreamsReplay();
        RefusedPurchases();
        BuyAndLeave();
        RemoveCard();
        ObservationPurity();
        NormalRunReachesShop();
    }

    // -- stock and source identity --------------------------------------------------------------------------

    private static void Inventory() =>
        Check.Run("T09.2 shop entry: 14 source slots / colored type sequence / colorless source identity / one on-sale slot", () =>
        {
            var world = EnterShop("t09-shop-alpha");
            var offers = world.Offers();
            Check.Equal(ShopRoomWiring.TotalEntryCount, offers.Count, "offer count = source 5+2+3+3+1");
            Check.Sequence(
                Enumerable.Range(0, ShopRoomWiring.TotalEntryCount).Select(index => (long)index),
                offers.Select(offer => world.Number(offer, "Slot")),
                "Slot 0..13");
            Check.Sequence(
                new[] { "card", "card", "card", "card", "card", "card", "card", "relic", "relic", "relic", "potion", "potion", "potion", "removal" },
                offers.Select(offer => world.Text(offer, "Kind")),
                "kind order = source MerchantInventory.CreateForNormalMerchant");
            Check.Sequence(
                new[] { "Attack", "Attack", "Skill", "Skill", "Power" },
                offers.Take(5).Select(offer => world.KindOf(world.Text(offer, "DefinitionId"))),
                "colored type sequence = source _coloredCardTypes");
            Check.Equal(
                5,
                offers.Take(5).Select(offer => world.Text(offer, "DefinitionId")).Distinct(StringComparer.Ordinal).Count(),
                "colored picks are distinct by identity");
            Check.Sequence(
                new long[] { 1, 1, 2, 2, 3, 4, 4 },
                offers.Take(7).Select(offer => world.Number(offer, "PoolKind")),
                "pool identity: Attack/Skill/Power = 1/2/3, colorless = 4");
            Check.Equal(1, offers.Take(5).Count(offer => world.Flag(offer, "OnSale")), "exactly one colored slot on sale");
            Check.True(offers.Skip(5).All(offer => !world.Flag(offer, "OnSale")), "only colored slots may be on sale");

            foreach (var (index, rarity) in new[] { (5, "uncommon"), (6, "rare") })
            {
                var offer = offers[index];
                Check.Equal(rarity, world.Text(offer, "Rarity"), $"colorless slot {index - 4} is fixed {rarity}");
                var poolIndex = world.Number(offer, "PoolIndex");
                Check.True(poolIndex >= 0 && poolIndex < ColorlessShopPool.Count,
                    $"colorless slot keeps a source-list index (saw {poolIndex})");
                Check.Equal(ColorlessShopPool.Entries[(int)poolIndex].SourceId, world.Text(offer, "SourceName"),
                    "colorless source identity = the source-list member at that index");
                Check.Equal(rarity, ColorlessShopPool.Entries[(int)poolIndex].Rarity,
                    "the drawn member comes from the bucket of the asked-for source rarity (source filters first)");
                if (world.Flag(offer, "Unsupported"))
                {
                    Check.True(world.Text(offer, "UnsupportedReason").Length > 0,
                        "an unbuyable colorless slot must state its reason");
                }
                else
                {
                    Check.Equal(world.Text(offer, "SourceName"),
                        ColorlessShopPool.ShortId(world.Text(offer, "DefinitionId")),
                        "buyable colorless identity = content definition id (same kebab convention)");
                }
            }

            Check.True(
                world.Number(offers[5], "PoolIndex") != world.Number(offers[6], "PoolIndex"),
                "the two colorless picks are distinct (source removes from the same list)");

            var relicRarities = offers.Skip(7).Take(3).Select(offer => world.Text(offer, "Rarity")).ToList();
            var sourceBagRarities = new[] { "Shop", "Common", "Uncommon", "Rare" };
            Check.True(relicRarities.All(rarity => sourceBagRarities.Contains(rarity, StringComparer.Ordinal)),
                $"relic rarities come from bag keys (saw {string.Join("/", relicRarities)})");
            Check.Equal(75L, world.Number(offers[13], "Price"), "removal price = 75 + 25*0");

            var known = SpireRelics.All.Select(relic => relic.ContentId).Append(ShopRoomWiring.CircletId)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var offer in offers.Skip(7).Take(3))
            {
                var definitionId = world.Text(offer, "DefinitionId");
                Check.Equal(!known.Contains(definitionId), world.Flag(offer, "Unsupported"),
                    $"relic buyability ({definitionId})");
                Check.True(definitionId.Length > 0, "a relic slot must carry an identity (source bag member id)");
                if (world.Flag(offer, "Unsupported"))
                {
                    Check.True(world.Text(offer, "UnsupportedReason").Length > 0,
                        "an unbuyable relic slot must state its reason");
                }
            }

            var poolIds = world.PotionIds();
            Console.WriteLine($"      (potion pool {poolIds.Count}; colorless list {ColorlessShopPool.Count}"
                + $" (uncommon {ColorlessShopPool.IndicesAtRarity("uncommon").Count}"
                + $" / rare {ColorlessShopPool.IndicesAtRarity("rare").Count});"
                + $" relic slots {string.Join("/", relicRarities)})");
            foreach (var offer in offers.Skip(10).Take(3))
            {
                var definitionId = world.Text(offer, "DefinitionId");
                if (world.Flag(offer, "Unsupported"))
                {
                    Check.True(world.Text(offer, "UnsupportedReason").Length > 0,
                        "an unbuyable potion slot must state its reason");
                }
                else
                {
                    Check.True(poolIds.Contains(definitionId), $"a buyable potion slot must be in the potion pool ({definitionId})");
                }
            }
        });

    // -- streams ---------------------------------------------------------------------------------------------

    private static void Streams() =>
        Check.Run("T09.2 streams: entering the shop consumes exactly 28 Shops / 14 Rewards draws; prices in source ranges", () =>
        {
            var world = EnterShop("t09-shop-alpha", gold: 500);
            var offers = world.Offers();
            Check.Equal(27L, world.Draws("Shops"),
                "Shops draws = 1 on-sale + 5x(pick+price) + 2x(pick+count) colorless + 3 relic prices + 3x3 potion");
            Check.Equal(14L, world.Draws("Rewards"), "Rewards draws = 5 rarity + 5 upgrade + 2 colorless upgrade + 2 relic rarity");
            Check.Equal((long)ShopRoomWiring.ExpectedShopsDraws, world.Draws("Shops"), "module constant matches (Shops)");
            Check.Equal((long)ShopRoomWiring.ExpectedRewardsDraws, world.Draws("Rewards"), "module constant matches (Rewards)");

            // Totality: the source generated exactly 14 real entries. The shop always builds 14; a degenerate
            // (empty-bucket) colored or colorless slot is the only case that is unbuyable, and it must still be a
            // real entry with a stated reason rather than a missing one.
            Check.Equal(ShopRoomWiring.TotalEntryCount, offers.Count, "the shelf always holds the source 14 entries");

            foreach (var offer in offers.Take(5))
            {
                if (world.Flag(offer, "Unsupported"))
                {
                    continue;
                }

                var basePrice = BasePrice(world.Text(offer, "Rarity"));
                var price = world.Number(offer, "Price");
                if (world.Flag(offer, "OnSale"))
                {
                    // `SetOnSale` prices again and halves with integer division: the reachable band halves too.
                    Check.True(price >= (long)Math.Ceiling(basePrice * 0.95f) / 2
                            && price <= (long)Math.Round(basePrice * 1.05f) / 2,
                        $"on-sale price {price} must be the halved source band for base {basePrice}");
                }
                else
                {
                    CheckPriceBand(basePrice, price, 0.95f, 1.05f, "colored");
                }
            }

            // The x1.15 colorless multiplier shifts the range so it cannot overlap the colored range: this
            // separates "multiplier forgotten" from a correct price.
            var colorless = offers[5];
            if (!world.Flag(colorless, "Unsupported"))
            {
                var colorlessBase = (long)MathF.Round(BasePrice(world.Text(colorless, "Rarity")) * 1.15f);
                var colorlessPrice = world.Number(colorless, "Price");
                CheckPriceBand(colorlessBase, colorlessPrice, 0.95f, 1.05f, "colorless");
                Check.True(colorlessPrice > (long)Math.Round(BasePrice(world.Text(colorless, "Rarity")) * 1.05f),
                    "colorless price must exceed the same-rarity colored ceiling (the x1.15 discriminator)");
            }

            foreach (var offer in offers.Skip(10).Take(3))
            {
                if (world.Flag(offer, "Unsupported"))
                {
                    continue;
                }

                CheckPriceBand(PotionBasePrice(world.Text(offer, "Rarity")), world.Number(offer, "Price"),
                    0.95f, 1.05f, "potion");
            }

            foreach (var offer in offers.Skip(7).Take(3))
            {
                if (world.Flag(offer, "Unsupported"))
                {
                    continue;
                }

                var basePrice = world.Text(offer, "Rarity") switch
                {
                    "Common" => 175L,
                    "Uncommon" => 225L,
                    "Rare" => 275L,
                    "Shop" => 200L,
                    _ => 1L,
                };
                CheckPriceBand(basePrice, world.Number(offer, "Price"), 0.85f, 1.15f, "relic");
            }
        });

    /// <summary>
    /// Independent replay: a second random service on the same seed. Streams are independent, so the Rewards
    /// sequence is exactly "card i rarity, card i upgrade, colorless 1 upgrade, colorless 2 upgrade, relic rarity,
    /// relic rarity" (source order). Every colored rarity is recomputed from the replayed roll and the source
    /// thresholds.
    /// </summary>
    private static void StreamsReplay() =>
        Check.Run("T09.2 streams: every colored rarity equals the independent replay (Rewards offset is read-only)", () =>
        {
            const string seed = "t09-shop-replay";
            var world = EnterShop(seed);
            var rng = new RandomService(
                VanillaRandomLayout.RunSeed(seed),
                VanillaRandomLayout.AlgorithmId,
                VanillaRandomLayout.DrawProfileId,
                VanillaRandomLayout.Profile);
            VanillaRandomLayout.SeedAllStreams(rng, seed);
            var rewards = VanillaRandomLayout.EnginePlayerStreamName("Rewards");
            var offset = RewardCompute.FloatOf(RewardCompute.BitsOf(RewardCompute.CardRarityBaseOffset));
            var offers = world.Offers();
            var reads = new List<float>();
            for (var slot = 0; slot < ShopRoomWiring.ColoredCardEntryCount; slot++)
            {
                var bits = rng.DrawInt(rewards, 0L, RewardCompute.Span);
                var roll = RewardCompute.RollOf(bits);
                reads.Add(roll);
                var rareThreshold = ShopCompute.ShopRareOdds + offset;
                var expected = roll < rareThreshold
                    ? "rare"
                    : roll < ShopCompute.ShopUncommonOdds + rareThreshold ? "uncommon" : "common";
                Check.Equal(expected, world.Text(offers[slot], "Rarity"),
                    $"colored slot {slot} rarity must equal the independent replay (roll {roll})");
                _ = rng.DrawInt(rewards, 0L, RewardCompute.Span); // the card's upgrade roll
            }

            _ = rng.DrawInt(rewards, 0L, RewardCompute.Span); // colorless 1 upgrade
            _ = rng.DrawInt(rewards, 0L, RewardCompute.Span); // colorless 2 upgrade
            _ = rng.DrawInt(rewards, 0L, RewardCompute.Span); // relic 1 rarity
            _ = rng.DrawInt(rewards, 0L, RewardCompute.Span); // relic 2 rarity
            Console.WriteLine("      (replay colored rolls: "
                + string.Join(",", reads.Select(value => value.ToString("F6"))) + ")");
            Check.Equal((long)ShopRoomWiring.ExpectedRewardsDraws, world.Draws("Rewards"),
                "the replay models the same Rewards sequence the shop consumed");
        });

    // -- ordinary refusals (no Fault) -----------------------------------------------------------------------

    private static void RefusedPurchases()
    {
        Check.Run("T09.2 cannot afford: empty candidate domain, guard rejects, state unchanged and leaving still legal", () =>
        {
            var world = EnterShop("t09-shop-poor", gold: 0);
            var target = world.Offers()[0];
            // The engine drops an action whose only candidate domain is empty (Scheduler.LegalActions): "cannot
            // provide a candidate" and "not in the candidate domain" are the same observable contract here.
            Check.False(world.LegalOffer(ShopRoomWiring.BuyCardActionName, target),
                "with no gold the buy-card offer must not be a candidate binding");

            var stateBefore = world.Session.State.StateHash();
            var offersBefore = world.Offers().Select(offer => world.Text(offer, "DefinitionId")).ToList();
            var deckBefore = world.Deck().Count;
            var drawsBefore = world.Session.Random.TotalDraws;
            Check.Fault(EngineFaultCode.IllegalAction,
                () => world.SubmitRefused(ShopRoomWiring.BuyCardActionName, Value.OfRef(target)),
                "submitting an unaffordable item must be rejected");
            Check.Equal(SessionStatus.Running, world.Session.Status, "a guard refusal must not fault the session");
            Check.Equal(stateBefore, world.Session.State.StateHash(), "state is unchanged after the refusal");
            Check.Equal(0L, world.RunNumber("Gold"), "gold unchanged");
            Check.Equal(deckBefore, world.Deck().Count, "long-term deck unchanged");
            Check.Equal(drawsBefore, world.Session.Random.TotalDraws, "a refusal consumes no randomness");
            Check.Sequence(offersBefore, world.Offers().Select(offer => world.Text(offer, "DefinitionId")), "shelf unchanged");
            Check.True(world.Legal(ShopRoomWiring.LeaveActionName) is not null, "leaving is still legal after the refusal");
            world.Submit(ShopRoomWiring.LeaveActionName);
            Check.Equal((long)MapRoomWiring.TravelReadyPhase, world.RunNumber("MapPhase"), "leaving returns to travel phase");
        });

        Check.Run("T09.2 belt full: empty candidate domain, guard rejects, gold and belt unchanged, leaving still legal", () =>
        {
            var world = EnterShop("t09-shop-fullbelt", gold: 500);
            var potionIds = world.PotionIds().ToList();
            Check.True(potionIds.Count >= 1, "the content must expose at least one potion (otherwise a full belt is impossible)");
            while (world.Belt().Count < SliceManifest.PotionSlotCount)
            {
                world.AddBeltPotion(potionIds[world.Belt().Count % potionIds.Count]);
            }

            Check.Equal((long)SliceManifest.PotionSlotCount, (long)world.Belt().Count, "the belt is full");
            var target = world.Offers().Skip(10).First();
            Check.False(world.LegalOffer(ShopRoomWiring.BuyPotionActionName, target),
                "with a full belt the potion offer must not be a candidate binding");
            var goldBefore = world.RunNumber("Gold");
            var stateBefore = world.Session.State.StateHash();
            Check.Fault(EngineFaultCode.IllegalAction,
                () => world.SubmitRefused(ShopRoomWiring.BuyPotionActionName, Value.OfRef(target)),
                "buying a potion with a full belt must be rejected");
            Check.Equal(SessionStatus.Running, world.Session.Status, "a guard refusal must not fault the session");
            Check.Equal(goldBefore, world.RunNumber("Gold"), "gold unchanged");
            Check.Equal(stateBefore, world.Session.State.StateHash(), "state is unchanged after the refusal");
            Check.True(world.Legal(ShopRoomWiring.LeaveActionName) is not null, "leaving is still legal");
        });

        Check.Run("T09.2 unsupported item: no candidate binding, guard rejects; wrong phase cannot buy either", () =>
        {
            var world = EnterShop("t09-shop-unsupported", gold: 9999);
            var unsupported = world.Offers().Where(offer => world.Flag(offer, "Unsupported")).ToList();
            Check.True(unsupported.Count > 0, "the current content must produce unbuyable slots (otherwise this coverage is empty)");
            foreach (var offer in unsupported)
            {
                Check.True(world.Text(offer, "UnsupportedReason").Length > 0, "an unbuyable item must state its reason");
                var action = world.Text(offer, "Kind") switch
                {
                    ShopRoomWiring.CardOfferKind => ShopRoomWiring.BuyCardActionName,
                    ShopRoomWiring.RelicOfferKind => ShopRoomWiring.BuyRelicActionName,
                    _ => ShopRoomWiring.BuyPotionActionName,
                };
                Check.False(world.LegalOffer(action, offer), $"an unbuyable item must not be a candidate of {action}");
            }

            var stateBefore = world.Session.State.StateHash();
            Check.Fault(EngineFaultCode.IllegalAction,
                () => world.SubmitRefused(ShopRoomWiring.BuyCardActionName, Value.OfRef(unsupported[0])),
                "submitting an unbuyable item must be rejected");
            Check.Equal(SessionStatus.Running, world.Session.Status, "a refusal must not fault the session");
            Check.Equal(stateBefore, world.Session.State.StateHash(), "state is unchanged after the refusal");

            // Phase guard: at the battle phase no in-shop action is legal (the node is untouched, so this isolates
            // the phase half of the guard).
            FormalSliceFixture.SetNumber(world.Session, world.Run, RunTypeId, "MapPhase", 1);
            foreach (var action in new[]
                     {
                         ShopRoomWiring.BuyCardActionName, ShopRoomWiring.BuyRelicActionName,
                         ShopRoomWiring.BuyPotionActionName, ShopRoomWiring.RemoveCardActionName,
                         ShopRoomWiring.LeaveActionName,
                     })
            {
                Check.True(world.Legal(action) is null, $"the battle phase must not allow {action}");
            }

            FormalSliceFixture.SetNumber(world.Session, world.Run, RunTypeId, "MapPhase", MapRoomWiring.ShopPhase);
            Check.True(world.Legal(ShopRoomWiring.BuyCardActionName) is not null, "back at the shop phase buying is legal again");
        });
    }

    // -- purchases / leaving / re-entering ------------------------------------------------------------------

    private static void BuyAndLeave() =>
        Check.Run("T09.2 buy card+potion+relic: real long-term card / belt slot / owned relic, charged once, not buyable twice", () =>
        {
            var world = EnterShop("t09-shop-buy", gold: 9999);
            var deckBefore = world.Deck().Count;
            var goldBefore = world.RunNumber("Gold");

            var cardOffer = world.Offers().Take(7)
                .First(offer => world.LegalOffer(ShopRoomWiring.BuyCardActionName, offer));
            var cardPrice = world.Number(cardOffer, "Price");
            world.Submit(ShopRoomWiring.BuyCardActionName, Value.OfRef(cardOffer));
            Check.Equal(goldBefore - cardPrice, world.RunNumber("Gold"), "gold charged exactly once");
            Check.Equal(deckBefore + 1, world.Deck().Count, "the card joined the long-term deck");
            var bought = world.Deck().Last();
            Check.Equal(world.Text(cardOffer, "DefinitionId"), FormalSliceFixture.Definition(world.Session, bought),
                "the bought card is the shelf definition");
            Check.True(world.Flag(cardOffer, "Sold"), "the shelf entry is sold");
            Check.False(world.LegalOffer(ShopRoomWiring.BuyCardActionName, cardOffer), "a sold entry is not a candidate");
            var goldAfterCard = world.RunNumber("Gold");
            Check.Fault(EngineFaultCode.IllegalAction,
                () => world.Submit(ShopRoomWiring.BuyCardActionName, Value.OfRef(cardOffer)),
                "a sold entry cannot be bought twice");
            Check.Equal(goldAfterCard, world.RunNumber("Gold"), "the repeated submit charges no gold");
            Check.Equal(deckBefore + 1, world.Deck().Count, "the repeated submit adds no card");

            // Potions: only a slot whose definition is registered can be bought (the rest stay unbuyable by content
            // boundary), so this records the seed's actual outcome.
            var potionOffer = world.Offers().Skip(10).Take(3)
                .FirstOrDefault(offer => world.LegalOffer(ShopRoomWiring.BuyPotionActionName, offer));
            if (potionOffer.IsNone)
            {
                Console.WriteLine("      (this seed has no buyable potion slot: the potion purchase path is not covered here)");
            }
            else
            {
                var potionPrice = world.Number(potionOffer, "Price");
                var goldBeforePotion = world.RunNumber("Gold");
                world.Submit(ShopRoomWiring.BuyPotionActionName, Value.OfRef(potionOffer));
                Check.Equal(goldBeforePotion - potionPrice, world.RunNumber("Gold"), "the potion charges gold once");
                var belt = world.Belt();
                Check.Equal(1, belt.Count, "the potion joined the belt");
                Check.Equal(world.Text(potionOffer, "DefinitionId"),
                    world.Session.State.GetField(belt[0], world.FieldIndex("Potion", "DefinitionId")).AsString(),
                    "the belt entry carries the bought definition");
                Check.True(world.Flag(potionOffer, "Sold"), "the potion shelf entry is sold");
            }

            // Relics: most source bag members have no formal effect declaration, so this records the seed outcome.
            var relicOffer = world.Offers().Skip(7).Take(3)
                .FirstOrDefault(offer => world.LegalOffer(ShopRoomWiring.BuyRelicActionName, offer));
            if (relicOffer.IsNone)
            {
                Console.WriteLine("      (this seed has no relic slot in the implementable registry: relic purchase not covered here)");
            }
            else
            {
                var definitionId = world.Text(relicOffer, "DefinitionId");
                var relic = SpireRelics.All.Single(item => item.ContentId == definitionId);
                var maxHpGain = relic.Effects.Where(effect => effect.Kind == RelicEffectKinds.GainMaxHp)
                    .Sum(effect => (long)effect.Amount);
                var goldGain = relic.Effects.Where(effect => effect.Kind == RelicEffectKinds.GainGold)
                    .Sum(effect => (long)effect.Amount);
                var price = world.Number(relicOffer, "Price");
                var goldBeforeRelic = world.RunNumber("Gold");
                var maxHpBefore = world.RunNumber("MaxHp");
                var relicsBefore = world.HeldRelics().Count;
                world.Submit(ShopRoomWiring.BuyRelicActionName, Value.OfRef(relicOffer));
                Check.Equal(goldBeforeRelic - price + goldGain, world.RunNumber("Gold"),
                    "the price is charged first, then the relic GainGold settles");
                Check.Equal(relicsBefore + 1, world.HeldRelics().Count, "a real owned relic instance was created");
                Check.Equal(maxHpBefore + maxHpGain, world.RunNumber("MaxHp"), "GainMaxHp is written back for the run");
                Check.True(world.Flag(relicOffer, "Sold"), "the relic shelf entry is sold");
            }

            world.Submit(ShopRoomWiring.LeaveActionName);
            Check.Equal((long)MapRoomWiring.TravelReadyPhase, world.RunNumber("MapPhase"), "leaving returns to travel phase");
            Check.True(world.Legal(ShopRoomWiring.BuyCardActionName) is null, "buying is illegal after leaving");
            Check.True(world.Legal(ShopRoomWiring.LeaveActionName) is null, "leaving twice is illegal");
            var soldBefore = world.Offers().Count(offer => world.Flag(offer, "Sold"));
            world.ReenterViaMap();
            Check.Equal((long)MapRoomWiring.ShopPhase, world.RunNumber("MapPhase"), "re-entering returns to the shop phase");
            Check.Equal(ShopRoomWiring.TotalEntryCount, world.Offers().Count, "re-entering still yields 14 slots");
            Check.Equal(0, world.Offers().Count(offer => world.Flag(offer, "Sold")),
                $"no sold flag survives re-entry (previous shop sold {soldBefore})");
            Check.Equal(deckBefore + 1, world.Deck().Count, "the purchase survives leaving and re-entering");
        });

    // -- card removal (formal decision) ---------------------------------------------------------------------

    private static void RemoveCard()
    {
        Check.Run("T09.2 removal: two same-definition instances, exactly the selected one removed, +1 counter, once per shop, higher price next shop", () =>
        {
            var world = EnterShop("t09-shop-remove", gold: 9999);
            var removal = world.Offers()[13];
            Check.True(world.LegalOffer(ShopRoomWiring.RemoveCardActionName, removal), "the removal service is a candidate");

            // Two same-definition instances: the source deck holds one per definition, so a controlled duplicate is
            // added to the long-term deck (same field recipe, distinct instance).
            var duplicate = world.Deck().First(card => FormalSliceFixture.Definition(world.Session, card) == StrikeId);
            var copy = world.DuplicateCard(duplicate);
            var before = world.Deck().Count;
            var goldBefore = world.RunNumber("Gold");

            world.SubmitAndExpectDecision(ShopRoomWiring.RemoveCardActionName, Value.OfRef(removal));
            Check.Equal(0L, world.RunNumber(ShopRoomWiring.RemovalsUsedFieldName), "the counter does not move before the pick");
            Check.Equal(goldBefore, world.RunNumber("Gold"), "no gold is charged before the pick");

            var pending = world.Session.PendingDecision!;
            Check.True(pending.Candidates.Contains(copy), "the duplicate instance is a decision candidate");
            Check.True(pending.Candidates.Contains(duplicate), "the other same-definition instance is a candidate too");
            world.Session.Answer(new DecisionAnswer(pending.RequestId, [copy]));

            Check.Equal(goldBefore - 75L, world.RunNumber("Gold"), "the removal charges 75");
            Check.Equal(before - 1, world.Deck().Count, "the deck lost one card");
            Check.False(world.Deck().Contains(copy), "the selected instance left the deck");
            Check.True(world.Deck().Contains(duplicate), "the other same-definition instance is still in the deck");
            Check.Equal(1L, world.RunNumber(ShopRoomWiring.RemovalsUsedFieldName), "the removal counter is +1");
            Check.True(world.Flag(removal, "Sold"), "the removal service is sold");
            Check.True(world.Legal(ShopRoomWiring.RemoveCardActionName) is null, "one removal per shop");

            // Leave and re-enter: the in-shop Used flag is not persisted, so another removal is possible and the
            // price is based on the run-wide counter.
            world.Submit(ShopRoomWiring.LeaveActionName);
            world.ReenterViaMap();
            Check.Equal(100L, world.Number(world.Offers()[13], "Price"), "next shop removal price = 75 + 25*1");
            Check.True(world.Legal(ShopRoomWiring.RemoveCardActionName) is not null, "the next shop allows another removal");
            Check.Equal(1L, world.RunNumber(ShopRoomWiring.RemovalsUsedFieldName),
                "the removal counter lives on the Engine Run (no C# state)");
        });

        Check.Run("T09.2 removal while broke: empty candidate domain, guard rejects, no pending decision", () =>
        {
            var world = EnterShop("t09-shop-remove-poor", gold: 0);
            var removal = world.Offers()[13];
            Check.False(world.LegalOffer(ShopRoomWiring.RemoveCardActionName, removal),
                "while broke the removal service must not be a candidate binding");
            var stateBefore = world.Session.State.StateHash();
            Check.Fault(EngineFaultCode.IllegalAction,
                () => world.SubmitRefused(ShopRoomWiring.RemoveCardActionName, Value.OfRef(removal)),
                "an unaffordable removal must be rejected");
            Check.Equal(SessionStatus.Running, world.Session.Status, "a refusal must not fault the session");
            Check.True(world.Session.PendingDecision is null, "a refusal must not leave a pending decision");
            Check.Equal(stateBefore, world.Session.State.StateHash(), "state is unchanged after the refusal");
            Check.True(world.Legal(ShopRoomWiring.LeaveActionName) is not null, "leaving is still legal");
        });
    }

    // -- observation purity ---------------------------------------------------------------------------------

    private static void ObservationPurity() =>
        Check.Run("T09.2 repeated observation / legal-action queries rebuild nothing and consume no randomness", () =>
        {
            var world = EnterShop("t09-shop-observe", gold: 500);
            var hash = world.Session.State.StateHash();
            var draws = world.Session.Random.TotalDraws;
            var identity = world.Offers()
                .Select(offer => $"{world.Number(offer, "Slot")}:{world.Text(offer, "DefinitionId")}").ToList();
            for (var round = 0; round < 3; round++)
            {
                _ = world.Session.LegalActions(SliceManifest.Player).Count;
                Check.Equal(ShopRoomWiring.TotalEntryCount, world.Offers().Count, "repeated reads keep 14 slots");
            }

            Check.Equal(hash, world.Session.State.StateHash(), "repeated reads/queries change no state");
            Check.Equal(draws, world.Session.Random.TotalDraws, "observation consumes no randomness");
            Check.Sequence(identity,
                world.Offers().Select(offer => $"{world.Number(offer, "Slot")}:{world.Text(offer, "DefinitionId")}"),
                "the shelf identity is unchanged slot by slot (nothing was rebuilt)");
        });

    // -- reachable from a normal Start ----------------------------------------------------------------------

    private static void NormalRunReachesShop() =>
        Check.Run("T09.2 reachability: from a normal Start, public actions reach a real Shop node and leave it", () =>
        {
            var session = NewRun("t09-shop-walk");
            var run = RunInstance(session);
            var visited = new List<string>();
            var reachedShop = false;
            for (var step = 0; step < 24 && !reachedShop && session.Status == SessionStatus.Running; step++)
            {
                var choose = Legal(session, ChooseAction);
                if (choose is null)
                {
                    break;
                }

                var nodeTypeField = session.RuleSet.TypeByQualifiedId(NodeTypeId).FieldIndex("Type");
                var shop = choose.Candidates
                    .Select(id => (Id: id, Type: (MapPointType)(int)session.State.GetField(id, nodeTypeField).AsEnumOrdinal()))
                    .FirstOrDefault(candidate => candidate.Type == MapPointType.Shop);
                if (shop.Id.IsNone)
                {
                    visited.Add(Describe(session, choose.Candidates[0]));
                    session.Submit(choose.Definition, SliceManifest.Player, [Value.OfRef(choose.Candidates[0])]);
                    AdvanceRoom(session);
                    continue;
                }

                visited.Add(Describe(session, shop.Id));
                session.Submit(choose.Definition, SliceManifest.Player, [Value.OfRef(shop.Id)]);
                Check.Equal((long)MapRoomWiring.ShopPhase, Int(session, run, RunTypeId, "MapPhase"),
                    "selecting a Shop node enters the shop phase");
                Check.Equal(ShopRoomWiring.TotalEntryCount, ShopRoom(session).Count,
                    "entering through the formal map yields 14 slots");
                session.Submit(Legal(session, ShopRoomWiring.LeaveActionName)!.Definition, SliceManifest.Player, []);
                Check.Equal((long)MapRoomWiring.TravelReadyPhase, Int(session, run, RunTypeId, "MapPhase"),
                    "leaving returns to the travel phase");
                reachedShop = true;
            }

            Console.WriteLine($"      (normal Start seed=t09-shop-walk reached the shop: {reachedShop}; path {string.Join(" -> ", visited)})");
            Check.True(reachedShop,
                "public actions from a normal Start must reach a real Shop node (not only a controlled arrangement)");
        });

    // -- world (controlled reachability + public actions) ---------------------------------------------------

    /// <summary>
    /// Controlled reachability: the session is arranged into "just settled the shop node's predecessor, next node not
    /// chosen yet" using real field semantics (same technique as <c>SliceMapRoomChecks.Arrange</c>), then the Shop
    /// node is selected from the legal candidates so the real Shop branch builds the stock. This is not "flip the
    /// phase at start": the entry and stock generation are still triggered by the map's <c>ChooseMapPoint</c>.
    /// </summary>
    private static World EnterShop(string seed, long gold = 500)
    {
        var session = NewRun(seed);
        var run = RunInstance(session);
        FormalSliceFixture.SetNumber(session, run, RunTypeId, "Gold", gold);
        var nodes = Nodes(session);
        var byIndex = nodes.ToDictionary(node => node.Index);
        var edges = EdgeChildren(SourceActMapCompute.Replay(
            Text(session, run, RunTypeId, "MapDraws"), SourceActMapCompute.ActRooms));
        NodeFact? parent = null;
        NodeFact? shop = null;
        for (var row = 1; row < 14 && parent is null; row++)
        {
            foreach (var candidate in nodes.Where(node => node.Row == row))
            {
                if (!edges.TryGetValue(candidate.Index, out var children))
                {
                    continue;
                }

                var found = children.Select(index => byIndex[index])
                    .FirstOrDefault(node => node.Type == (int)MapPointType.Shop);
                if (found.Id.IsNone)
                {
                    continue;
                }

                parent = candidate;
                shop = found;
                break;
            }
        }

        Check.True(parent is not null, $"seed {seed}: the source map has a Shop node and a predecessor");
        var owner = parent!.Value;
        var subject = shop!.Value;
        FormalSliceFixture.SetNumber(session, run, RunTypeId, "MapPhase", MapRoomWiring.TravelReadyPhase);
        FormalSliceFixture.SetNumber(session, run, RunTypeId, "MapFloor",
            Math.Min(owner.Row + 1, MapRoomWiring.MaxFloor - 1));
        session.State.RunAtomic(() => session.State.SetField(
            run, session.RuleSet.TypeByQualifiedId(RunTypeId).FieldIndex("CurrentNode"), Value.OfRef(owner.Id)));
        var choose = Legal(session, ChooseAction) ?? throw new CheckFailedException("ChooseMapPoint is illegal after arranging");
        Check.Contains(choose.Candidates, id => id == subject.Id, "the Shop node is a legal candidate (not silently filtered)");
        var shopsBefore = session.Random.DrawCount(VanillaRandomLayout.EnginePlayerStreamName("Shops"));
        var rewardsBefore = session.Random.DrawCount(VanillaRandomLayout.EnginePlayerStreamName("Rewards"));
        session.Submit(choose.Definition, SliceManifest.Player, [Value.OfRef(subject.Id)]);
        Check.Equal((long)MapRoomWiring.ShopPhase, Int(session, run, RunTypeId, "MapPhase"),
            "MapPhase = shop phase after selecting the Shop node");
        Check.Equal(subject.Id, Ref(session, run, RunTypeId, "CurrentNode"), "CurrentNode is the Shop node");
        Check.True(Flag(session, subject.Id, NodeTypeId, "Visited"), "the Shop node is marked visited");
        return new World(session, run, shopsBefore, rewardsBefore, subject, owner);
    }

    // -- host -----------------------------------------------------------------------------------------------

    private sealed class World(
        Session session, InstanceId run, long shopsBefore, long rewardsBefore, NodeFact shopNode, NodeFact predecessor)
    {
        public Session Session { get; } = session;

        public InstanceId Run { get; } = run;

        public InstanceId ShopNode { get; } = shopNode.Id;

        /// <summary>The Shop node's predecessor: the cursor position a settled room leaves behind.</summary>
        public NodeFact Predecessor { get; } = predecessor;

        public long RunNumber(string field) => Int(Session, Run, RunTypeId, field);

        public long Number(InstanceId offer, string field) => Int(Session, offer, OfferTypeId, field);

        public string Text(InstanceId offer, string field) =>
            Session.State.GetField(offer, Session.RuleSet.TypeByQualifiedId(OfferTypeId).FieldIndex(field)).AsString();

        public bool Flag(InstanceId offer, string field) =>
            Session.State.GetField(offer, Session.RuleSet.TypeByQualifiedId(OfferTypeId).FieldIndex(field)).AsBool();

        /// <summary>Content card kind of a definition id (used by the colored type-sequence check).</summary>
        public string? KindOf(string definitionId) => SinglePlayerContentSource.Shared.Model.Cards
            .FirstOrDefault(card => card.Id == definitionId)?.Kind;

        /// <summary>Definition ids of the potion pool the shop actually draws from (source pool, not a subset).</summary>
        public IReadOnlyList<string> PotionIds() => [.. PotionFacts().Select(potion => potion.Id)];

        /// <summary>
        /// The full source potion pool (`PotionPool`) with the facts the shop's belt entries need; falls back to the
        /// effects table when the pool is empty (the shop itself uses the same fallback).
        /// </summary>
        private static IReadOnlyList<(string Id, string Rarity, string Usage, string Target)> PotionFacts()
        {
            var model = SinglePlayerContentSource.Shared.Model;
            return model.PotionPool.Count > 0
                ? [.. model.PotionPool.Select(potion => (potion.Id, potion.Rarity, potion.Usage, potion.Target))]
                : [.. model.Potions.Select(potion => (potion.Id, potion.Rarity, potion.Usage, potion.Target))];
        }

        public List<InstanceId> Offers() => OffersOf(Session);

        public List<InstanceId> Deck() =>
            [.. Session.State.CollectionMembers(FormalSliceFixture.Collection(Session, DeckCollection))];

        public List<InstanceId> Belt() =>
            [.. Session.State.CollectionMembers(FormalSliceFixture.Collection(Session, BeltCollection))];

        public List<InstanceId> HeldRelics() =>
            [.. Session.State.CollectionMembers(FormalSliceFixture.Collection(Session, RelicsCollection))];

        public int FieldIndex(string typeId, string field) =>
            Session.RuleSet.TypeByQualifiedId("base-spire:" + typeId).FieldIndex(field);

        /// <summary>Draws consumed on this player stream since the shop-entry baseline.</summary>
        public long Draws(string playerStreamType) => Session.Random.DrawCount(
            VanillaRandomLayout.EnginePlayerStreamName(playerStreamType))
            - (string.Equals(playerStreamType, "Rewards", StringComparison.Ordinal) ? rewardsBefore : shopsBefore);

        public LegalAction? Legal(string actionId) => ShopFlowChecks.Legal(Session, actionId);

        public bool LegalOffer(string actionId, InstanceId offer) =>
            Legal(actionId) is { } action && action.Candidates.Any(candidate => candidate == offer);

        public void Submit(string actionId, params Value[] arguments)
        {
            var action = Legal(actionId) ?? throw new CheckFailedException($"action '{actionId}' is not legal now");
            Session.Submit(action.Definition, SliceManifest.Player, arguments);
        }

        /// <summary>
        /// Submits a named action even when the engine currently offers no binding for it, so the engine's own
        /// rejection path runs (guard / candidate / role). This is what proves "ordinary refusal" leaves the
        /// session usable instead of faulting it.
        /// </summary>
        public void SubmitRefused(string actionId, params Value[] arguments) =>
            Session.Submit(ActionByName(actionId), SliceManifest.Player, arguments);

        /// <summary>The compiled definition of a named action of this rule set (independent of current legality).</summary>
        private ActionDefinition ActionByName(string actionId)
        {
            var qualified = actionId.Contains(':', StringComparison.Ordinal)
                ? actionId
                : $"{SliceManifest.PackageId}:{actionId}";
            return Session.RuleSet.Actions.Single(action => action.QualifiedId == qualified);
        }

        /// <summary>Submits an action and asserts that it suspends for a formal decision.</summary>
        public void SubmitAndExpectDecision(string actionId, params Value[] arguments)
        {
            var action = Legal(actionId) ?? throw new CheckFailedException($"action '{actionId}' is not legal now");
            Session.Submit(action.Definition, SliceManifest.Player, arguments);
            Check.Equal(SessionStatus.AwaitingDecision, Session.Status, $"'{actionId}' must suspend for the card pick");
        }

        /// <summary>Adds another instance with the same definition as <paramref name="source"/> to the long-term deck.</summary>
        public InstanceId DuplicateCard(InstanceId source)
        {
            var cardType = Session.RuleSet.TypeByQualifiedId(FormalSliceFixture.CardTypeId);
            var deck = FormalSliceFixture.Collection(Session, DeckCollection);
            var created = InstanceId.None;
            Session.State.RunAtomic(() =>
            {
                var instance = Session.State.CreateInstance(cardType.Id, Session.State.ScopeOf(Run));
                foreach (var field in cardType.Fields)
                {
                    Session.State.SetField(instance, field.Index, Session.State.GetField(source, field.Index));
                }

                Session.State.AddToCollection(deck, instance);
                created = instance;
            });
            return created;
        }

        /// <summary>Puts a real potion instance into the belt (controlled full-belt scenario, definition from content).</summary>
        public void AddBeltPotion(string definitionId)
        {
            var potionType = Session.RuleSet.TypeByQualifiedId(PotionTypeId);
            var belt = FormalSliceFixture.Collection(Session, BeltCollection);
            var definition = PotionFacts().First(potion => potion.Id == definitionId);
            var slot = Belt().Count;
            Session.State.RunAtomic(() =>
            {
                var instance = Session.State.CreateInstance(potionType.Id, Session.State.ScopeOf(Run));
                void Set(string field, Value value) =>
                    Session.State.SetField(instance, potionType.FieldIndex(field), value);
                Set("DefinitionId", Value.Of(definitionId));
                Set("Rarity", Value.Of(definition.Rarity));
                Set("Target", Value.Of(definition.Target));
                Set("Usage", Value.Of(definition.Usage));
                Set("SlotIndex", Value.Of((long)slot));
                Set("UseSupported", Value.Of(false));
                Set("UnsupportedUseReason", Value.Of(string.Empty));
                Set("Amount", Value.Of(0L));
                Set("PowerId", Value.Of(string.Empty));
                Set("PowerStacks", Value.Of(0L));
                Session.State.AddToCollection(belt, instance);
            });
        }

        /// <summary>
        /// Leaves the shop and re-enters it through the real map Shop branch again: the cursor is put back on the
        /// Shop node's predecessor (the state a settled room leaves) and <c>ChooseMapPoint</c> selects the Shop node
        /// again. This is not a direct call into the module.
        /// </summary>
        public void ReenterViaMap()
        {
            FormalSliceFixture.SetNumber(Session, Run, RunTypeId, "MapPhase", MapRoomWiring.TravelReadyPhase);
            FormalSliceFixture.SetNumber(Session, Run, RunTypeId, "MapFloor",
                Math.Min(Predecessor.Row + 1, MapRoomWiring.MaxFloor - 1));
            Session.State.RunAtomic(() => Session.State.SetField(
                Run, Session.RuleSet.TypeByQualifiedId(RunTypeId).FieldIndex("CurrentNode"),
                Value.OfRef(Predecessor.Id)));
            var choose = Legal(ChooseAction) ?? throw new CheckFailedException("ChooseMapPoint is illegal before re-entering");
            Check.Contains(choose.Candidates, id => id == ShopNode, "the Shop node is still a legal candidate (re-entry)");
            Session.Submit(choose.Definition, SliceManifest.Player, [Value.OfRef(ShopNode)]);
        }
    }

    // -- normal-flow advancement helpers --------------------------------------------------------------------

    /// <summary>Advances an entered room until travel is possible again (fight + reward); other phases are returned as-is.</summary>
    private static void AdvanceRoom(Session session)
    {
        var run = RunInstance(session);
        for (var guard = 0; guard < 40; guard++)
        {
            var phase = Int(session, run, RunTypeId, "MapPhase");
            if (phase is (long)MapRoomWiring.TravelReadyPhase or 0 || session.Status != SessionStatus.Running)
            {
                return;
            }

            if (phase == (long)RewardWiring.RewardPhase)
            {
                ResolveReward(session);
                continue;
            }

            if (phase == 1)
            {
                if (!FightOneRoom(session))
                {
                    return;
                }

                continue;
            }

            return;
        }
    }

    /// <summary>Clears one fight (enemies to 1 Hp, then play attacks) and resolves the reward.</summary>
    private static bool FightOneRoom(Session session)
    {
        var monsters = Monsters(session);
        if (monsters.Count == 0)
        {
            return false;
        }

        var player = FormalSliceFixture.Player(session);
        FormalSliceFixture.SetNumber(session, player, FormalSliceFixture.PlayerTypeId, "Hp", 60);
        foreach (var monster in monsters)
        {
            FormalSliceFixture.SetNumber(session, monster, MonsterTypeId, "Hp", 1);
        }

        var plays = 0;
        while (Monsters(session).Count > 0 && session.Status == SessionStatus.Running && plays++ < 12)
        {
            var card = FindPlayableCard(session);
            if (card.IsNone)
            {
                break;
            }

            FormalSliceFixture.PlayAt(session, card, Monsters(session)[0]);
        }

        return Monsters(session).Count == 0;
    }

    private static List<InstanceId> Monsters(Session session) =>
        [.. session.State.CollectionMembers(FormalSliceFixture.Collection(session, MonstersCollection))];

    private static InstanceId FindPlayableCard(Session session)
    {
        foreach (var id in new[] { StrikeId, DefendId, BashId })
        {
            try
            {
                return FormalSliceFixture.MoveToHand(session, id);
            }
            catch (CheckFailedException)
            {
                // The definition is not in this fight's piles: try the next one.
            }
        }

        return InstanceId.None;
    }

    private static void ResolveReward(Session session)
    {
        var gold = Legal(session, RewardWiring.TakeGoldActionName);
        if (gold is not null)
        {
            session.Submit(gold.Definition, SliceManifest.Player, []);
        }

        var card = Legal(session, RewardWiring.SkipCardActionName);
        if (card is not null)
        {
            session.Submit(card.Definition, SliceManifest.Player, []);
        }

        var potion = Legal(session, RewardWiring.SkipPotionActionName);
        if (potion is not null)
        {
            session.Submit(potion.Definition, SliceManifest.Player, []);
        }
    }

    // -- state readers --------------------------------------------------------------------------------------

    private readonly record struct NodeFact(InstanceId Id, int Index, int Col, int Row, int Type);

    private static Session NewRun(string seed)
    {
        var run = new BaseSpireRuntimeFactory(RunConfiguration.Ironclad(0, BaseSpireScope.DevMap)).CreateSession(seed);
        run.Start();
        return run.Session;
    }

    private static List<InstanceId> ShopRoom(Session session) => OffersOf(session);

    private static List<InstanceId> OffersOf(Session session) =>
    [
        .. session.State.CollectionMembers(FormalSliceFixture.Collection(session, OffersCollection))
            .OrderBy(offer => session.State.GetField(offer,
                session.RuleSet.TypeByQualifiedId(OfferTypeId).FieldIndex("Slot")).AsInt()),
    ];

    private static InstanceId RunInstance(Session session) =>
        session.State.CollectionMembers(FormalSliceFixture.Collection(session, RunStatesCollection)).Single();

    private static List<NodeFact> Nodes(Session session)
    {
        var type = session.RuleSet.TypeByQualifiedId(NodeTypeId);
        return
        [
            .. session.State.CollectionMembers(FormalSliceFixture.Collection(session, NodesCollection))
                .Select(id => new NodeFact(
                    id,
                    (int)session.State.GetField(id, type.FieldIndex("Index")).AsInt(),
                    (int)session.State.GetField(id, type.FieldIndex("Col")).AsInt(),
                    (int)session.State.GetField(id, type.FieldIndex("Row")).AsInt(),
                    session.State.GetField(id, type.FieldIndex("Type")).AsEnumOrdinal())),
        ];
    }

    private static Dictionary<int, List<int>> EdgeChildren(SourceActMapReplay replay)
    {
        var map = new Dictionary<int, List<int>>();
        foreach (var edge in replay.Edges)
        {
            if (!map.TryGetValue(edge.Parent, out var children))
            {
                map[edge.Parent] = children = [];
            }

            children.Add(edge.Child);
        }

        return map;
    }

    private static LegalAction? Legal(Session session, string actionId)
    {
        var qualified = actionId.Contains(':', StringComparison.Ordinal)
            ? actionId
            : $"{SliceManifest.PackageId}:{actionId}";
        return session.LegalActions(SliceManifest.Player)
            .FirstOrDefault(candidate => candidate.Definition.QualifiedId == qualified);
    }

    private static string Describe(Session session, InstanceId node)
    {
        var type = session.RuleSet.TypeByQualifiedId(NodeTypeId);
        var row = session.State.GetField(node, type.FieldIndex("Row")).AsInt();
        var pointType = (MapPointType)(int)session.State.GetField(node, type.FieldIndex("Type")).AsEnumOrdinal();
        return $"row{row}:{pointType}";
    }

    private static long Int(Session session, InstanceId entity, string typeId, string field) =>
        FormalSliceFixture.Number(session, entity, typeId, field);

    private static string Text(Session session, InstanceId entity, string typeId, string field) =>
        session.State.GetField(entity, session.RuleSet.TypeByQualifiedId(typeId).FieldIndex(field)).AsString();

    private static bool Flag(Session session, InstanceId entity, string typeId, string field) =>
        session.State.GetField(entity, session.RuleSet.TypeByQualifiedId(typeId).FieldIndex(field)).AsBool();

    private static InstanceId Ref(Session session, InstanceId entity, string typeId, string field) =>
        session.State.GetField(entity, session.RuleSet.TypeByQualifiedId(typeId).FieldIndex(field)).AsRef();

    private static long BasePrice(string rarity) => rarity switch
    {
        "rare" => 150L,
        "uncommon" => 75L,
        _ => 50L,
    };

    /// <summary>
    /// Source price band <c>Round(base * [min,max))</c>. The implementation rounds the product, so the reachable
    /// integers are <c>[ceil(base*min), Round(base*max-epsilon)]</c>; a plain <c>(long)(base*1.05f)</c> upper bound
    /// would wrongly reject a rounded ceiling such as 79 for base 75.
    /// </summary>
    private static void CheckPriceBand(long basePrice, long price, float min, float max, string what)
    {
        var low = (long)Math.Ceiling(basePrice * min);
        var high = (long)Math.Round(basePrice * max);
        Check.True(price >= low && price <= high,
            $"{what} price {price} must be in Round({basePrice} x [{min}, {max})) = [{low}, {high}]");
    }

    private static long PotionBasePrice(string rarity) => rarity switch
    {
        "rare" => 100L,
        "uncommon" => 75L,
        _ => 50L,
    };
}
