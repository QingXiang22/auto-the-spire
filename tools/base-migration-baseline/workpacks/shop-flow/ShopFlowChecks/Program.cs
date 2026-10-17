using System.Collections.Generic;
using System.Linq;
using BaseSpire.Slice.Effects;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.ShopFlowChecks;

/// <summary>
/// ENV-T09.2 商店模块的独立验证：只编译本工作包的 `ShopCompute.cs` / `ShopRoomWiring.cs`，
/// 通过**冻结二进制**（Core.TheSpire.dll + BaseSpire.dll）在真实 Engine 会话里跑商店流程。
///
/// 覆盖的判别行为（每条都对应一种具体错误）：
/// <list type="bullet">
/// <item>库存：14 格源顺序、彩卡类型序列 = 源 `_coloredCardTypes`、彩卡互不重复、无色固定 uncommon/rare、唯一折扣格；</item>
/// <item>流序：`Shops` 28 抽 / `Rewards` 14 抽、价格落在源区间、无色 ×1.15、首张彩卡稀有度可被独立重放；</item>
/// <item>金币不足：明确拒绝且金币/牌组/售出状态/抽数全部不变；</item>
/// <item>买卡：真实长期牌 + 扣金一次 + 售罄 + 不能重复买；</item>
/// <item>买遗物：先扣金、建持有实例、执行 `AfterObtained` 效果；未登记效果的商品明确不可买；</item>
/// <item>买药水：进药水栏最低空槽 + 扣金；满栏拒绝且不变；</item>
/// <item>删牌：同定义两实例只删选中的那个、扣金、计数 +1、一间一次、下一间按计数涨价；</item>
/// <item>离店回地图；战斗相位与错误节点都不能购买。</item>
/// </list>
/// </summary>
internal static class Program
{
    private static int Main()
    {
        Console.WriteLine("== ENV-T09.2 商店流程独立验证（冻结 0.4 二进制 + 真实 Engine 会话） ==");
        InventoryAndStreams();
        PriceFormula();
        InsufficientGold();
        BuyCard();
        BuyRelic();
        BuyPotion();
        RemoveCard();
        LeaveAndPhaseGuards();
        PotionPickPath();
        Console.WriteLine($"== passed={Check.Passed} failed={Check.Failed} ==");
        return Check.Failed == 0 ? 0 : 1;
    }

    // -- 库存与流序 -------------------------------------------------------------------------------------------

    private static void InventoryAndStreams() =>
        Check.Run("库存：14 格源顺序 / 彩卡类型序列 / 无色固定稀有度 / 唯一折扣格 / 遗物可买判据", () =>
        {
            var harness = ShopHarness.Create("SHOP-INVENTORY-20261011");
            var offers = harness.Offers();
            Check.Equal(ShopRoomWiring.TotalEntryCount, offers.Count, "货架条目数 = 源 5+2+3+3+1");
            Check.Sequence(
                Enumerable.Range(0, ShopRoomWiring.TotalEntryCount).Select(index => (long)index),
                offers.Select(offer => harness.OfferNumber(offer, "Slot")),
                "Slot 0..13");
            Check.Sequence(
                new[] { "card", "card", "card", "card", "card", "card", "card", "relic", "relic", "relic", "potion", "potion", "potion", "removal" },
                offers.Select(offer => harness.OfferText(offer, "Kind")),
                "商品种类顺序 = 源 MerchantInventory.CreateForNormalMerchant");
            Check.Sequence(
                new[] { "Attack", "Attack", "Skill", "Skill", "Power" },
                offers.Take(5).Select(offer => harness.Cards.Definition(harness.OfferText(offer, "DefinitionId")).Kind),
                "彩卡类型序列 = 源 _coloredCardTypes");
            Check.Equal(
                5,
                offers.Take(5).Select(offer => harness.OfferText(offer, "DefinitionId")).Distinct(StringComparer.Ordinal).Count(),
                "彩卡按 identity 互不重复（源用已建卡的 CanonicalInstance 做排除）");
            Check.Equal("uncommon", harness.OfferText(offers[5], "Rarity"), "无色第一格固定 uncommon");
            Check.Equal("rare", harness.OfferText(offers[6], "Rarity"), "无色第二格固定 rare");
            foreach (var index in new[] { 5, 6 })
            {
                var definition = harness.Cards.Definition(harness.OfferText(offers[index], "DefinitionId"));
                Check.True(definition.Pools.Contains("ColorlessCardPool"), "无色格成员必须来自无色池");
            }

            Check.Sequence(
                new long[] { 1, 1, 2, 2, 3, 4, 4 },
                offers.Take(7).Select(offer => harness.OfferNumber(offer, "PoolKind")),
                "池身份：Attack/Skill/Power = 1/2/3，无色 = 4");
            Check.Equal(1, offers.Take(5).Count(offer => harness.OfferBool(offer, "OnSale")), "彩卡区恰好一格在售");
            Check.True(offers.Skip(5).All(offer => !harness.OfferBool(offer, "OnSale")), "只有彩卡格可能在售");

            var potionIds = harness.Potions.Pool.Select(potion => potion.Id).ToHashSet(StringComparer.Ordinal);
            var potionRarities = harness.Potions.Pool
                .GroupBy(potion => potion.Rarity, StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => $"{group.Key}={group.Count()}");
            var supportedPotions = harness.Potions.Pool.Count(potion => string.IsNullOrEmpty(potion.UnsupportedUseReason));
            Console.WriteLine($"     （药水池 {harness.Potions.Pool.Count} 瓶：{string.Join(",", potionRarities)}；可用 {supportedPotions} 瓶）");
            foreach (var offer in offers.Skip(10).Take(3))
            {
                var definitionId = harness.OfferText(offer, "DefinitionId");
                Console.WriteLine(
                    $"     （药水格 {harness.OfferNumber(offer, "Slot")}：rarity={harness.OfferText(offer, "Rarity")} "
                    + $"id='{definitionId}' poolIndex={harness.OfferNumber(offer, "PoolIndex")} "
                    + $"unsupported={harness.OfferBool(offer, "Unsupported")}）");
                if (harness.OfferBool(offer, "Unsupported"))
                {
                    Check.True(harness.OfferText(offer, "UnsupportedReason").Length > 0, "不可买的药水格必须写明原因");
                }
                else
                {
                    Check.True(potionIds.Contains(definitionId), "可买的药水格定义必须在药水池里");
                }
            }
            Check.True(
                offers.Skip(7).Take(3).All(offer => ShopHarness.BagRarities.Contains(harness.OfferText(offer, "Rarity"))),
                "遗物格稀有度来自袋键");
            Check.Equal(75L, harness.OfferNumber(offers[13], "Price"), "删牌价 = 75 + 25*0");

            // 未登记效果的遗物：明确标记不可买 + 写明原因（不伪装可买）。
            var known = SpireRelics.All.Select(relic => relic.ContentId).Append(ShopRoomWiring.CircletId).ToHashSet(StringComparer.Ordinal);
            foreach (var offer in offers.Skip(7).Take(3))
            {
                var definitionId = harness.OfferText(offer, "DefinitionId");
                Check.Equal(!known.Contains(definitionId), harness.OfferBool(offer, "Unsupported"), $"遗物可买判据（{definitionId}）");
                if (harness.OfferBool(offer, "Unsupported"))
                {
                    Check.True(harness.OfferText(offer, "UnsupportedReason").Length > 0, "不可买商品必须写明原因");
                }
            }

            Check.True(
                offers.Skip(7).Take(3).Any(offer => harness.OfferBool(offer, "Unsupported")),
                "源清单袋里必然出现未登记效果的遗物（否则这条覆盖没意义）");
        });

    // -- 价格与抽数 -------------------------------------------------------------------------------------------

    private static void PriceFormula()
    {
        Check.Run("流序：逐笔抽数对账（Shops 正常路径 28 / Rewards 14）+ 四类价格落在源区间", () =>
        {
            var harness = ShopHarness.Create("SHOP-STREAMS-20261011");
            var offers = harness.Offers();
            // 退化药水格（该稀有度在药水池里没有成员）会少抽一笔 pick；源侧同样的内容会抛异常。
            var degeneratePotions = offers.Skip(10).Take(3)
                .Count(offer => harness.OfferText(offer, "UnsupportedReason") == ShopRoomWiring.PotionBucketReason);
            Console.WriteLine(
                $"     （抽数实测：Shops={harness.DrawCount("Shops")}（退化药水格 {degeneratePotions}）"
                + $" Rewards={harness.DrawCount("Rewards")}）");
            Check.Equal(
                28L - degeneratePotions,
                harness.DrawCount("Shops"),
                $"Shops 抽数 = 1 折扣 + 5×(选卡+价) + 1 在售价 + 2×2 无色 + 3 遗物价 + 3×3 药水 − 退化药水格 {degeneratePotions}");
            Check.Equal(14L, harness.DrawCount("Rewards"), "Rewards 抽数 = 5 稀有度 + 5 升级 + 2 无色升级 + 2 遗物稀有度");
            Check.Equal(
                (long)ShopRoomWiring.ExpectedShopsDraws - degeneratePotions,
                harness.DrawCount("Shops"),
                "模块文档常量与实测一致（Shops）");
            Check.Equal((long)ShopRoomWiring.ExpectedRewardsDraws, harness.DrawCount("Rewards"), "模块文档常量与实测一致（Rewards）");

            foreach (var offer in offers.Take(5))
            {
                var basePrice = CardBasePrice(harness.Cards.Definition(harness.OfferText(offer, "DefinitionId")).Variant(false).Rarity);
                var price = harness.OfferNumber(offer, "Price");
                if (harness.OfferBool(offer, "OnSale"))
                {
                    Check.True(
                        price >= (long)(basePrice * 0.95f) / 2 && price <= (long)(basePrice * 1.05f) / 2,
                        $"在售价 {price} 应落在 {basePrice}×[0.95,1.05) 整除 2 的区间");
                }
                else
                {
                    Check.True(
                        price >= (long)(basePrice * 0.95f) && price <= (long)(basePrice * 1.05f),
                        $"彩卡价 {price} 应落在 {basePrice}×[0.95,1.05)");
                }
            }

            // 无色 ×1.15：区间与同稀有度彩卡区间不相交，因此这条能判别"乘数漏了"。
            var colorlessRarity = harness.Cards.Definition(harness.OfferText(offers[5], "DefinitionId")).Variant(false).Rarity;
            var colorlessBase = (long)MathF.Round(CardBasePrice(colorlessRarity) * 1.15f);
            var colorlessPrice = harness.OfferNumber(offers[5], "Price");
            Check.True(
                colorlessPrice >= (long)(colorlessBase * 0.95f) && colorlessPrice <= (long)(colorlessBase * 1.05f),
                $"无色价 {colorlessPrice} 应落在 {colorlessBase}×[0.95,1.05)");
            Check.True(
                colorlessPrice > (long)(CardBasePrice(colorlessRarity) * 1.05f),
                "无色价必须高于同稀有度彩卡上限（×1.15 的判别点）");

            foreach (var offer in offers.Skip(7).Take(3))
            {
                var basePrice = RelicBasePrice(harness.OfferText(offer, "Rarity"));
                var price = harness.OfferNumber(offer, "Price");
                if (basePrice >= 999999999L)
                {
                    Check.Equal(basePrice, price, "Ancient/Event/Starter 遗物价固定为 999999999");
                    continue;
                }

                Check.True(
                    price >= (long)(basePrice * 0.85f) && price <= (long)(basePrice * 1.15f),
                    $"遗物价 {price} 应落在 {basePrice}×[0.85,1.15)");
            }

            foreach (var offer in offers.Skip(10).Take(3))
            {
                var basePrice = harness.OfferText(offer, "Rarity") switch { "rare" => 100L, "uncommon" => 75L, _ => 50L };
                var price = harness.OfferNumber(offer, "Price");
                Check.True(
                    price >= (long)(basePrice * 0.95f) && price <= (long)(basePrice * 1.05f),
                    $"药水价 {price} 应落在 {basePrice}×[0.95,1.05)");
            }
        });

        Check.Run("流序：首张彩卡稀有度 = 独立重放的 Rewards 首笔 + 源阈值", () =>
        {
            const string seed = "SHOP-STREAMS-20261011";
            var harness = ShopHarness.Create(seed);
            var offers = harness.Offers();
            var rng = new RandomService(
                VanillaRandomLayout.RunSeed(seed),
                VanillaRandomLayout.AlgorithmId,
                VanillaRandomLayout.DrawProfileId,
                VanillaRandomLayout.Profile);
            VanillaRandomLayout.SeedAllStreams(rng, seed);
            // 源顺序：先抽折扣格（Shops），再抽第一张彩卡的稀有度（Rewards）。
            rng.DrawInt(VanillaRandomLayout.EnginePlayerStreamName("Shops"), 0, 5);
            var roll = (float)rng.DrawDouble(VanillaRandomLayout.EnginePlayerStreamName("Rewards"));
            var offset = RewardCompute.FloatOf(RewardCompute.BitsOf(RewardCompute.CardRarityBaseOffset));
            var rareThreshold = ShopCompute.ShopRareOdds + offset;
            var expected = roll < rareThreshold
                ? "rare"
                : roll < ShopCompute.ShopUncommonOdds + rareThreshold ? "uncommon" : "common";
            Check.Equal(expected, harness.OfferText(offers[0], "Rarity"), "首张彩卡稀有度必须等于独立重放结果");
        });
    }

    // -- 金币不足 ---------------------------------------------------------------------------------------------

    private static void InsufficientGold() =>
        Check.Run("金币不足：明确拒绝且金币/牌组/售出状态/抽数全部不变", () =>
        {
            var harness = ShopHarness.Create("SHOP-GOLD-20261011", startGold: 0);
            var offer = harness.Offers()[0];
            var deckBefore = harness.Members("LongTermDeck").Count;
            var shopsBefore = harness.DrawCount("Shops");
            var fault = Check.Fault(
                () => harness.Submit(ShopRoomWiring.BuyCardActionName, Value.OfRef(offer)),
                "金币不足必须被拒绝");
            Console.WriteLine($"     （故障码 {fault.Code}：{fault.Message}）");
            Check.Equal(0L, harness.RunNumber("Gold"), "金币不变");
            Check.Equal(deckBefore, harness.Members("LongTermDeck").Count, "长期牌组不变");
            Check.False(harness.OfferBool(offer, "Sold"), "商品未售出");
            Check.Equal(shopsBefore, harness.DrawCount("Shops"), "拒绝不产生额外汇抽");
        });

    // -- 买卡 -------------------------------------------------------------------------------------------------

    private static void BuyCard() =>
        Check.Run("买卡：真实长期牌 + 扣金一次 + 售罄 + 不能重复买", () =>
        {
            var harness = ShopHarness.Create("SHOP-BUY-CARD-20261011", startGold: 9999);
            var offer = harness.Offers()[0];
            var price = harness.OfferNumber(offer, "Price");
            var deckBefore = harness.Members("LongTermDeck").Count;
            harness.Submit(ShopRoomWiring.BuyCardActionName, Value.OfRef(offer));
            Check.Equal(9999L - price, harness.RunNumber("Gold"), "扣金恰好一次");
            Check.Equal(deckBefore + 1, harness.Members("LongTermDeck").Count, "牌进了长期牌组");
            var bought = harness.Members("LongTermDeck").Last();
            Check.Equal(harness.OfferText(offer, "DefinitionId"), harness.Text(bought, "Card", "DefinitionId"), "买到的就是货架上的定义");
            Check.True(harness.OfferBool(offer, "Sold"), "货架条目售出");
            Check.Fault(
                () => harness.Submit(ShopRoomWiring.BuyCardActionName, Value.OfRef(offer)),
                "售罄的条目不可再买");
            Check.Equal(9999L - price, harness.RunNumber("Gold"), "重复提交不改金币");
            Check.Equal(deckBefore + 1, harness.Members("LongTermDeck").Count, "重复提交不加牌");
        });

    // -- 买遗物 -----------------------------------------------------------------------------------------------

    private static void BuyRelic()
    {
        Check.Run("买遗物：先扣金 + 建持有实例 + 执行 AfterObtained 效果", () =>
        {
            var harness = ShopHarness.Create("SHOP-BUY-RELIC-20261011", startGold: 9999, sourceRelicBags: false);
            var known = SpireRelics.All.ToDictionary(relic => relic.ContentId, StringComparer.Ordinal);
            var offer = harness.Offers().Skip(7).Take(3)
                .FirstOrDefault(candidate => known.ContainsKey(harness.OfferText(candidate, "DefinitionId")));
            Check.True(offer != default, "袋里只放可实现的遗物时必须出现可买遗物");
            var definitionId = harness.OfferText(offer, "DefinitionId");
            var price = harness.OfferNumber(offer, "Price");
            var relic = known[definitionId];
            var maxHpGain = relic.Effects.Where(effect => effect.Kind == RelicEffectKinds.GainMaxHp).Sum(effect => (long)effect.Amount);
            var goldGain = relic.Effects.Where(effect => effect.Kind == RelicEffectKinds.GainGold).Sum(effect => (long)effect.Amount);
            var relicsBefore = harness.Members("Relics").Count;
            harness.Submit(ShopRoomWiring.BuyRelicActionName, Value.OfRef(offer));
            Check.Equal(9999L - price + goldGain, harness.RunNumber("Gold"), "先扣价再结算遗物的 GainGold");
            Check.Equal(relicsBefore + 1, harness.Members("Relics").Count, "建立了真实持有实例");
            var owned = harness.Members("Relics").Last();
            Check.Equal(definitionId, harness.Text(owned, "Relic", "DefinitionId"), "持有身份 = 货架定义");
            Check.Equal(80L + maxHpGain, harness.RunNumber("MaxHp"), "GainMaxHp 生效且持久");
            Check.Equal(80L + maxHpGain, harness.RunNumber("Hp"), "GainMaxHp 同时回同样多的血");
            Check.True(harness.OfferBool(offer, "Sold"), "货架条目售出");
        });

        Check.Run("未登记效果的遗物：明确不可买、拒绝后状态不变", () =>
        {
            var harness = ShopHarness.Create("SHOP-BUY-RELIC-DENY-20261011", startGold: 9999);
            var offer = harness.Offers().Skip(7).Take(3).FirstOrDefault(candidate => harness.OfferBool(candidate, "Unsupported"));
            Check.True(offer != default, "源清单袋里必须有不可买的遗物格");
            harness.SetOfferNumber(offer, "Price", 1L);
            var goldBefore = harness.RunNumber("Gold");
            var relicsBefore = harness.Members("Relics").Count;
            var fault = Check.Fault(
                () => harness.Submit(ShopRoomWiring.BuyRelicActionName, Value.OfRef(offer)),
                "不可买的遗物必须被拒绝");
            Console.WriteLine($"     （故障码 {fault.Code}：{fault.Message}）");
            Check.Equal(goldBefore, harness.RunNumber("Gold"), "拒绝不改金币");
            Check.Equal(relicsBefore, harness.Members("Relics").Count, "拒绝不新增遗物");
            Check.False(harness.OfferBool(offer, "Sold"), "拒绝不改售出状态");
        });
    }

    // -- 买药水 -----------------------------------------------------------------------------------------------

    private static void BuyPotion()
    {
        Check.Run("买药水：进栏最低空槽 + 扣金", () =>
        {
            var harness = ShopHarness.Create("SHOP-BUY-POTION-20261011", startGold: 9999);
            var available = harness.Potions.Pool
                .Where(potion => string.IsNullOrEmpty(potion.UnsupportedUseReason))
                .ToList();
            Check.True(available.Count >= 1, "内容里至少有一瓶可用药水（否则无法验证药水购买路径）");
            // 药水栏允许同名药水并存，因此不足三瓶时循环取用（源也不禁止重复）。
            var chosen = Enumerable.Range(0, 3).Select(index => available[index % available.Count]).ToList();
            var offers = harness.Offers().Skip(10).Take(3).ToList();
            for (var index = 0; index < chosen.Count; index++)
            {
                InjectPotion(harness, offers[index], chosen[index], 50L);
                harness.Submit(ShopRoomWiring.BuyPotionActionName, Value.OfRef(offers[index]));
            }

            Check.Equal(9999L - (50L * chosen.Count), harness.RunNumber("Gold"), "每瓶各扣一次价");
            var belt = harness.Members("PotionBelt");
            Check.Equal(3, belt.Count, "三瓶都在药水栏里");
            Check.Sequence(new long[] { 0, 1, 2 }, belt.Select(potion => harness.Number(potion, "Potion", "SlotIndex")).OrderBy(slot => slot), "槽位 0/1/2");
            Check.Sequence(
                chosen.Select(potion => potion.Id).OrderBy(id => id, StringComparer.Ordinal),
                belt.Select(potion => harness.Text(potion, "Potion", "DefinitionId")).OrderBy(id => id, StringComparer.Ordinal),
                "栏里的定义 = 买的那三瓶");
        });

        Check.Run("满药水栏：拒绝购买且金币与药水栏都不变", () =>
        {
            var harness = ShopHarness.Create("SHOP-BUY-POTION-FULL-20261011", startGold: 9999);
            var available = harness.Potions.Pool
                .Where(potion => string.IsNullOrEmpty(potion.UnsupportedUseReason))
                .ToList();
            Check.True(available.Count >= 1, "内容里至少有一瓶可用药水");
            var chosen = Enumerable.Range(0, 3).Select(index => available[index % available.Count]).ToList();
            var offers = harness.Offers().Skip(10).Take(3).ToList();
            for (var index = 0; index < chosen.Count; index++)
            {
                InjectPotion(harness, offers[index], chosen[index], 50L);
                harness.Submit(ShopRoomWiring.BuyPotionActionName, Value.OfRef(offers[index]));
            }

            Check.Equal(3, harness.Members("PotionBelt").Count, "先填满药水栏");
            harness.EnterShop();
            var next = harness.Offers().Skip(10).First();
            InjectPotion(harness, next, chosen[0], 50L);
            var goldBefore = harness.RunNumber("Gold");
            var fault = Check.Fault(
                () => harness.Submit(ShopRoomWiring.BuyPotionActionName, Value.OfRef(next)),
                "满栏必须被拒绝");
            Console.WriteLine($"     （故障码 {fault.Code}：{fault.Message}）");
            Check.Equal(goldBefore, harness.RunNumber("Gold"), "拒绝不改金币");
            Check.Equal(3, harness.Members("PotionBelt").Count, "拒绝不改药水栏");
            Check.False(harness.OfferBool(next, "Sold"), "拒绝不改售出状态");
        });
    }

    // -- 删牌 -------------------------------------------------------------------------------------------------

    private static void RemoveCard() =>
        Check.Run("删牌：同定义两实例只删选中的那个 + 扣金 + 计数 +1 + 一间一次 + 下间涨价", () =>
        {
            var harness = ShopHarness.Create("SHOP-REMOVE-20261011", startGold: 9999);
            var removal = harness.Offers()[13];
            var deck = harness.Members("LongTermDeck");
            var strikes = deck.Where(card => harness.Text(card, "Card", "DefinitionId") == SliceManifest.StrikeId).ToList();
            Check.Equal(2, strikes.Count, "牌组里有两件同定义实例");
            var victim = strikes[0];
            var survivor = strikes[1];
            harness.Submit(ShopRoomWiring.RemoveCardActionName, Value.OfRef(removal), Value.OfRef(victim));
            Check.Equal(9999L - 75L, harness.RunNumber("Gold"), "扣掉删牌价 75");
            var after = harness.Members("LongTermDeck");
            Check.False(after.Contains(victim), "被选中的那个实例被移出牌组");
            Check.True(after.Contains(survivor), "同定义的另一个实例仍在牌组里");
            Check.Equal(1L, harness.RunNumber(ShopRoomWiring.RemovalsUsedFieldName), "删牌计数 +1");
            Check.True(harness.OfferBool(removal, "Sold"), "删牌商品售罄");
            // 引擎把动作留着，但**售出商品已经不在任何候选域里**（提交仍会被 body 的售罄守卫拒绝，见买卡那条）。
            var leftover = harness.Legal(ShopRoomWiring.RemoveCardActionName);
            if (leftover is not null)
            {
                Console.WriteLine($"     （售罄后候选域大小：{string.Join("/", leftover.Domains.Values.Select(domain => domain.Count))}）");
            }

            Check.False(
                leftover is not null && leftover.Domains.Values.Any(domain => domain.Any(value => value.AsRef() == removal)),
                "售罄的删牌商品必须不在任何候选域里");
            Check.Equal(1L, harness.RunNumber(ShopRoomWiring.RemovalsUsedFieldName), "售罄后计数不变");

            harness.EnterShop();
            Check.Equal(100L, harness.OfferNumber(harness.Offers()[13], "Price"), "下一间删牌价 = 75 + 25*1");
            Check.True(harness.Legal(ShopRoomWiring.RemoveCardActionName) is not null, "下一间商店又可以删一张");
        });

    // -- 离店与相位守卫 ---------------------------------------------------------------------------------------

    private static void LeaveAndPhaseGuards()
    {
        Check.Run("离店：回到地图相位；离店/战斗/错误节点都不能买", () =>
        {
            var harness = ShopHarness.Create("SHOP-LEAVE-20261011");
            Check.True(harness.Legal(ShopRoomWiring.BuyCardActionName) is not null, "店内买卡合法");
            harness.Submit(ShopRoomWiring.LeaveActionName);
            Check.Equal(2L, harness.RunNumber("MapPhase"), "离店回到地图相位 2（TravelReady）");
            Check.True(harness.Legal(ShopRoomWiring.BuyCardActionName) is null, "离店后不能买卡");
            Check.True(harness.Legal(ShopRoomWiring.BuyRelicActionName) is null, "离店后不能买遗物");
            Check.True(harness.Legal(ShopRoomWiring.BuyPotionActionName) is null, "离店后不能买药水");
            Check.True(harness.Legal(ShopRoomWiring.RemoveCardActionName) is null, "离店后不能删牌");

            harness.SetRun("MapPhase", 1L);
            Check.True(harness.Legal(ShopRoomWiring.BuyCardActionName) is null, "战斗相位不能买卡");
            Check.True(harness.Legal(ShopRoomWiring.LeaveActionName) is null, "战斗相位不能离店");

            harness.SetRun("MapPhase", ShopHarness.ShopPhase);
            harness.SetShopNodeType(ShopHarness.MonsterPointOrdinal);
            Check.True(harness.Legal(ShopRoomWiring.BuyCardActionName) is null, "相位对但节点不是商店也不能买");
            harness.SetShopNodeType(ShopHarness.ShopPointOrdinal);
            Check.True(harness.Legal(ShopRoomWiring.BuyCardActionName) is not null, "回到商店节点又可买（守卫只挡错误相位/节点）");
        });

        Check.Run("重复进店：货架重建（旧条目清空、售出状态不残留）", () =>
        {
            var harness = ShopHarness.Create("SHOP-REENTER-20261011", startGold: 9999);
            var first = harness.Offers()[0];
            harness.Submit(ShopRoomWiring.BuyCardActionName, Value.OfRef(first));
            Check.Equal(ShopRoomWiring.TotalEntryCount, harness.Offers().Count, "买完仍然是 14 格");
            harness.EnterShop();
            var second = harness.Offers();
            Check.Equal(ShopRoomWiring.TotalEntryCount, second.Count, "重进后 14 格");
            Check.True(second.All(offer => !harness.OfferBool(offer, "Sold")), "重进后没有残留的售出状态");
            Check.Equal(0L, harness.RunNumber(ShopRoomWiring.RemovalsUsedFieldName), "删牌计数不受重进影响（本店没删过）");
        });
    }

    // -- 辅助 -------------------------------------------------------------------------------------------------

    /// <summary>
    /// 药水 pick 路径：本轮内容只有 2 瓶药水（rare/uncommon），因此多数 seed 的三个药水格都是退化格。
    /// 换 seed 找出**非退化**格，才能覆盖"按掷出的稀有度取池成员 + 取该稀有度的价格基准"这条路径。
    /// </summary>
    private static void PotionPickPath() =>
        Check.Run("药水 pick 路径（非退化格）：定义来自药水池、可买、价格按掷出的稀有度", () =>
        {
            var poolIds = ShopHarness.Model.PotionPool.Count > 0
                ? ShopHarness.Model.PotionPool.Select(potion => potion.Id).ToHashSet(StringComparer.Ordinal)
                : ShopHarness.Model.Potions.Select(potion => potion.Id).ToHashSet(StringComparer.Ordinal);
            for (var attempt = 0; attempt < 40; attempt++)
            {
                var harness = ShopHarness.Create($"SHOP-POTION-LIVE-{attempt}");
                var live = harness.Offers().Skip(10).Take(3)
                    .Where(offer => !harness.OfferBool(offer, "Unsupported"))
                    .ToList();
                if (live.Count == 0)
                {
                    continue;
                }

                foreach (var offer in live)
                {
                    var definitionId = harness.OfferText(offer, "DefinitionId");
                    Check.True(poolIds.Contains(definitionId), $"可买药水格的定义必须来自药水池（{definitionId}）");
                    var definition = harness.Potions.Pool.Single(potion => potion.Id == definitionId);
                    Check.Equal(definition.Rarity, harness.OfferText(offer, "Rarity"), "货架稀有度 = 该药水的稀有度");
                    var basePrice = definition.Rarity switch { "rare" => 100L, "uncommon" => 75L, _ => 50L };
                    var price = harness.OfferNumber(offer, "Price");
                    Check.True(
                        price >= (long)(basePrice * 0.95f) && price <= (long)(basePrice * 1.05f),
                        $"药水价 {price} 应落在掷出稀有度的基准 {basePrice}×[0.95,1.05)");
                }

                Console.WriteLine($"     （非退化药水格出现在 seed SHOP-POTION-LIVE-{attempt}：{live.Count} 格）");
                return;
            }

            throw new InvalidOperationException("40 个 seed 内没有出现可买药水格，pick 路径未被覆盖");
        });

    private static void InjectPotion(ShopHarness harness, InstanceId offer, PotionDefinition potion, long price)
    {
        var index = harness.Potions.Pool.ToList().FindIndex(candidate => candidate.Id == potion.Id);
        harness.SetOfferText(offer, "DefinitionId", potion.Id);
        harness.SetOfferText(offer, "Rarity", potion.Rarity);
        harness.SetOfferNumber(offer, "PoolIndex", index);
        harness.SetOfferNumber(offer, "Price", price);
        harness.SetOfferFlag(offer, "Unsupported", false);
        harness.SetOfferText(offer, "UnsupportedReason", string.Empty);
    }

    private static long CardBasePrice(string rarity) => rarity switch
    {
        "rare" => 150L,
        "uncommon" => 75L,
        _ => 50L,
    };

    private static long RelicBasePrice(string rarity) => rarity switch
    {
        "Common" => 175L,
        "Uncommon" => 225L,
        "Rare" => 275L,
        "Shop" => 200L,
        "None" => 1L,
        _ => 999999999L,
    };
}
