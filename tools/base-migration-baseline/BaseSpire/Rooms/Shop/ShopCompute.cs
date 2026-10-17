using CoreTheSpire.Engine;

namespace BaseSpire.Slice;

/// <summary>
/// ENV-T09.2 商店的**纯计算**条目：源 `MerchantCardEntry` / `MerchantRelicEntry` / `MerchantPotionEntry` /
/// `CardFactory.CreateForMerchant` / `PotionFactory.CreateRandomPotion` 的稀有度与价格计算逐字移植。
///
/// 与 `RewardCompute` 同口径：不接触 RNG / Session / State。输入是 IR 侧已经抽好的 payload
/// （一笔对应源里的一笔 `NextFloat`/`NextInt`），输出是"这笔抽数落到了什么"。因此每笔抽数都能逐笔对账。
///
/// 与奖励路径的**必然差异**（源即如此，不能复用 `RewardCompute` 的对应条目）：
/// 商店稀有度走 `CardRarityOdds.RollWithoutChangingFutureOdds(Shop)`：
///   `num2 = 0.09f + offset`；`num &lt; num2 → Rare`；`num &lt; 0.37f + num2 → Uncommon`；否则 `Common`；
/// 并且**不回写** offset（`RewardCompute.ExecuteCardRarity` 用普通战斗赔率 0.03f 且回写 offset，故商店不能复用它）。
/// 返回的稀有度同样按 `CardFactory.GetNextAllowedRarity` 上卷到"当前候选集合（已排除本店已建卡）里非空"的档位；
/// 一圈回绕仍然全部为空时**不抛异常**：`Available=false` 让调用方把该商品标成不可买（源侧此处会抛
/// `InvalidOperationException`，那会让整局不可继续，不是普通"买不起"）。
/// </summary>
public static class ShopCompute
{
    // -- 资产名 / Entry 名 ---------------------------------------------------------------------------------------

    public const string CardRarityAssetName = "ShopCardRarity";
    public const string CardCountAssetName = "ShopCardCount";
    public const string CardPickAssetName = "ShopCardPick";
    public const string CardPriceAssetName = "ShopCardPrice";
    public const string CardUpgradeAssetName = "ShopCardUpgrade";
    public const string RelicRarityAssetName = "ShopRelicRarity";
    public const string RelicPriceAssetName = "ShopRelicPrice";
    public const string PotionRarityAssetName = "ShopPotionRarity";
    public const string PotionPriceAssetName = "ShopPotionPrice";

    public const string CardRarityEntry = "shop-card-rarity";
    public const string CardCountEntry = "shop-card-count";
    public const string CardPickEntry = "shop-card-pick";
    public const string CardPriceEntry = "shop-card-price";
    public const string CardUpgradeEntry = "shop-card-upgrade";
    public const string RelicRarityEntry = "shop-relic-rarity";
    public const string RelicPriceEntry = "shop-relic-price";
    public const string PotionRarityEntry = "shop-potion-rarity";
    public const string PotionPriceEntry = "shop-potion-price";

    public const string SourcePath = "sts2/src/Core/Entities/Merchant";
    public const string SourceHash =
        "MerchantInventory.CreateForNormalMerchant:cards=colored5+colorless2,price=150/75/50*[0.95,1.05)(colorless*1.15,onsale/2);" +
        "relics=2xRollRarity(Rewards)+Shop,price=175/225/275/200*[0.85,1.15);" +
        "potions=3x(rarity<=0.1/<=0.35+pick),price=100/75/50*[0.95,1.05);" +        "rarity=nextallowed(0.09+offset,0.37,0.54),available=false-when-all-buckets-excluded;card-upgrade=consume(Rewards),never-upgrade;" +
        "colorless=ColorlessCardPool.source-order(64)uniform-index,no-rarity-roll,no-nextallowed;" +
        "relic-pull=bagtail,empty-chain Shop/Common/Uncommon/Rare->None->circlet;shops-stream@1";

    /// <summary>本店"已建过的卡"黑名单容量：5 彩 + 2 无色（源 `CardEntries` 的前 7 件）。</summary>
    public const int ExclusionCount = 7;

    // -- 源常量 --------------------------------------------------------------------------------------------------

    /// <summary>源 `CardRarityOdds.GetBaseOdds(CardRarityOddsType.Shop)`（`CardRarityOdds.cs`）。</summary>
    public const float ShopCommonOdds = 0.54f;
    public const float ShopUncommonOdds = 0.37f;
    public const float ShopRareOdds = 0.09f;

    /// <summary>源 `PotionFactory.CreateRandomPotion` 的两条阈值（`&lt;=` 比较）。</summary>
    public const float PotionRareThreshold = 0.1f;
    public const float PotionUncommonThreshold = 0.35f;

    /// <summary>源 `MerchantCardEntry.GetCost`（`rarity switch { Rare=>150, Uncommon=>75, _=>50 }`）。</summary>
    public const int CardRarePrice = 150;
    public const int CardUncommonPrice = 75;
    public const int CardCommonPrice = 50;

    /// <summary>源 `MerchantCardEntry.GetCost`：无色池 `Mathf.RoundToInt(num * 1.15f)`。</summary>
    public const float ColorlessPriceMultiplier = 1.15f;

    /// <summary>源 `RelicModel.MerchantCost`（`RelicModel.cs:304-315`）。</summary>
    public const int RelicCommonPrice = 175;
    public const int RelicUncommonPrice = 225;
    public const int RelicRarePrice = 275;
    public const int RelicShopPrice = 200;
    public const int RelicAncientPrice = 999999999;

    /// <summary>源 `MerchantPotionEntry.GetCost`（`Rare=>100, Uncommon=>75, _=>50`）。</summary>
    public const int PotionRarePrice = 100;
    public const int PotionUncommonPrice = 75;
    public const int PotionCommonPrice = 50;

    /// <summary>源 `MerchantCardRemovalEntry`：`BaseCost=75`、`PriceIncrease=25`、`_cost = Base + Increase * used`。</summary>
    public const int RemovalBasePrice = 75;
    public const int RemovalPriceIncrease = 25;

    /// <summary>源 `Player.PlayerRng.Shops.NextFloat(0.95f, 1.05f)` 的区间（卡/药水）。</summary>
    public const float CardPriceMinFactor = 0.95f;
    public const float CardPriceMaxFactor = 1.05f;

    /// <summary>源 `Player.PlayerRng.Shops.NextFloat(0.85f, 1.15f)` 的区间（遗物）。</summary>
    public const float RelicPriceMinFactor = 0.85f;
    public const float RelicPriceMaxFactor = 1.15f;

    // -- 资产声明（输出顺序 = 下面 Execute 写入的顺序，二者只在本文件里对齐） ------------------------------------

    public static ShopCardRarityAsset DeclareCardRarity(EnginePackageBuilder package, string backendId) =>
        Asset(package, CardRarityAssetName, CardRarityEntry, backendId,
            [("Bits", FieldTypeSchema.Int(0, RewardCompute.Span - 1)),
             ("Spec", FieldTypeSchema.String(8192)),
             ("OffsetBits", FieldTypeSchema.Int(0, 4294967295L)),
             .. Enumerable.Range(0, ExclusionCount).Select(index => ("Ex" + (char)('A' + index), ExclusionSchema()))],
            [("Ordinal", FieldTypeSchema.Int(0, 10)), ("Rarity", FieldTypeSchema.String(16)), ("Available", FieldTypeSchema.Bool())],
            asset => new ShopCardRarityAsset(asset, asset.Inputs[0], asset.Inputs[1], asset.Inputs[2],
                [.. Enumerable.Range(0, ExclusionCount).Select(index => asset.Inputs[3 + index])],
                asset.Outputs[0], asset.Outputs[1], asset.Outputs[2]));

    public static ShopPoolAsset DeclareCardCount(EnginePackageBuilder package, string backendId) =>
        PoolAsset(package, CardCountAssetName, CardCountEntry, backendId, "Count");

    public static ShopPoolAsset DeclareCardPick(EnginePackageBuilder package, string backendId) =>
        PoolAsset(package, CardPickAssetName, CardPickEntry, backendId, "Index");

    public static ShopCardPriceAsset DeclareCardPrice(EnginePackageBuilder package, string backendId) =>
        Asset(package, CardPriceAssetName, CardPriceEntry, backendId,
            [("Ordinal", FieldTypeSchema.Int(0, 10)),
             ("Colorless", FieldTypeSchema.Bool()),
             ("Bits", FieldTypeSchema.Int(0, RewardCompute.Span - 1)),
             ("OnSale", FieldTypeSchema.Bool())],
            [("Price", FieldTypeSchema.Int(0, 999999999))],
            asset => new ShopCardPriceAsset(asset, asset.Inputs[0], asset.Inputs[1], asset.Inputs[2], asset.Inputs[3],
                asset.Outputs[0]));

    /// <summary>
    /// 源 `CardFactory.CreateForMerchant` 里那句 `RollForUpgrade(player, cardModel, -999999999m)`
    /// （`CardFactory.cs:58,283-305`）：**无条件**消费一笔 `PlayerRng.Rewards` 的 `NextFloat`，
    /// 但 `num &lt;= -999999999m + actIndex*0.25m` 恒为 false ⇒ 商店卡绝不由这里升级。
    /// 本条目就是为了把那笔抽数如实消费掉（`Upgraded` 恒 false），使流序与源一致；
    /// 源里真正让商店卡升级的 `Hook.ModifyMerchantCardCreationResults`（MoltenEgg 等）本 Slice 未建模。
    /// </summary>
    public static ShopCardUpgradeAsset DeclareCardUpgrade(EnginePackageBuilder package, string backendId) =>
        Asset(package, CardUpgradeAssetName, CardUpgradeEntry, backendId,
            [("Bits", FieldTypeSchema.Int(0, RewardCompute.Span - 1))],
            [("Upgraded", FieldTypeSchema.Bool())],
            asset => new ShopCardUpgradeAsset(asset, asset.Inputs[0], asset.Outputs[0]));

    public static ShopRelicRarityAsset DeclareRelicRarity(EnginePackageBuilder package, string backendId) =>
        Asset(package, RelicRarityAssetName, RelicRarityEntry, backendId,
            [("Bits", FieldTypeSchema.Int(0, RewardCompute.Span - 1))],
            [("Rarity", FieldTypeSchema.String(16))],
            asset => new ShopRelicRarityAsset(asset, asset.Inputs[0], asset.Outputs[0]));

    public static ShopRelicPriceAsset DeclareRelicPrice(EnginePackageBuilder package, string backendId) =>
        Asset(package, RelicPriceAssetName, RelicPriceEntry, backendId,
            [("Rarity", FieldTypeSchema.String(16)),
             ("Bits", FieldTypeSchema.Int(0, RewardCompute.Span - 1))],
            [("Price", FieldTypeSchema.Int(0, 999999999))],
            asset => new ShopRelicPriceAsset(asset, asset.Inputs[0], asset.Inputs[1], asset.Outputs[0]));

    public static ShopPotionRarityAsset DeclarePotionRarity(EnginePackageBuilder package, string backendId) =>
        Asset(package, PotionRarityAssetName, PotionRarityEntry, backendId,
            [("Bits", FieldTypeSchema.Int(0, RewardCompute.Span - 1))],
            [("Ordinal", FieldTypeSchema.Int(0, 10)), ("Rarity", FieldTypeSchema.String(16))],
            asset => new ShopPotionRarityAsset(asset, asset.Inputs[0], asset.Outputs[0], asset.Outputs[1]));

    public static ShopPotionPriceAsset DeclarePotionPrice(EnginePackageBuilder package, string backendId) =>
        Asset(package, PotionPriceAssetName, PotionPriceEntry, backendId,
            [("Ordinal", FieldTypeSchema.Int(0, 10)),
             ("Bits", FieldTypeSchema.Int(0, RewardCompute.Span - 1))],
            [("Price", FieldTypeSchema.Int(0, 999999999))],
            asset => new ShopPotionPriceAsset(asset, asset.Inputs[0], asset.Inputs[1], asset.Outputs[0]));

    // -- 执行（后端入口；`entry` = `asset.Entry`） ---------------------------------------------------------------

    /// <summary>
    /// 引擎把本包声明的商店条目交给这里执行；不认识就返回 false，让调用方继续转发给别的后端
    /// （正式集成点：`BaseSpireComputeBackend.Execute` 在药水/宝箱/地图资产之后委派这里）。
    /// </summary>
    public static bool TryExecute(string entry, IReadOnlyList<Value> inputs, Value[] outputs)
    {
        switch (entry)
        {
            case CardRarityEntry:
                ExecuteCardRarity(inputs, outputs);
                return true;
            case CardCountEntry:
                ExecuteCount(inputs, outputs);
                return true;
            case CardPickEntry:
                ExecutePick(inputs, outputs);
                return true;
            case CardPriceEntry:
                ExecuteCardPrice(inputs, outputs);
                return true;
            case CardUpgradeEntry:
                ExecuteCardUpgrade(inputs, outputs);
                return true;
            case RelicRarityEntry:
                ExecuteRelicRarity(inputs, outputs);
                return true;
            case RelicPriceEntry:
                ExecuteRelicPrice(inputs, outputs);
                return true;
            case PotionRarityEntry:
                ExecutePotionRarity(inputs, outputs);
                return true;
            case PotionPriceEntry:
                ExecutePotionPrice(inputs, outputs);
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// 源 `CardFactory.CreateForMerchant` + `CardRarityOdds.RollWithoutChangingFutureOdds(Shop)` +
    /// `CardFactory.GetNextAllowedRarity`（`CardFactory.cs:40-80,244-281`、`CardRarityOdds.cs:96-110`）。
    /// 输出 `Ordinal` 供 count/pick/price 复用，`Rarity` 供条目观察与遗物/药水同构文本，
    /// `Available` 表示上卷一圈后仍有候选成员（false = 该商品位没有可建内容，调用方标不可买）。
    /// </summary>
    private static void ExecuteCardRarity(IReadOnlyList<Value> inputs, Value[] outputs)
    {
        var bits = inputs[0].AsInt();
        var spec = RewardPoolSpec.Parse(inputs[1].AsString());
        var offset = RewardCompute.FloatOf(inputs[2].AsInt());
        var exclusions = ReadExclusions(inputs, 3);

        var roll = RewardCompute.RollOf(bits);
        var rareOdds = ShopRareOdds + offset;
        var rarity = roll < rareOdds
            ? RewardRarity.Rare
            : roll < ShopUncommonOdds + rareOdds
                ? RewardRarity.Uncommon
                : RewardRarity.Common;

        var allowed = NextAllowed(spec, rarity, exclusions);
        outputs[0] = Value.Of((long)allowed);
        outputs[1] = Value.Of(RarityText(allowed));
        outputs[2] = Value.Of(allowed != RewardRarity.None);
    }

    /// <summary>源 `options2.Except(blacklist).Where(rarity).Count`（黑名单＝本店已建过的卡）。</summary>
    private static void ExecuteCount(IReadOnlyList<Value> inputs, Value[] outputs)
    {
        var spec = RewardPoolSpec.Parse(inputs[0].AsString());
        var rarity = (int)inputs[1].AsInt();
        outputs[0] = Value.Of((long)CountOf(spec, rarity, ReadExclusions(inputs, 2)));
    }

    /// <summary>源 `rng.NextItem(items)`：`index = NextInt(0, items.Count)`，返回该成员的全局池下标。</summary>
    private static void ExecutePick(IReadOnlyList<Value> inputs, Value[] outputs)
    {
        var spec = RewardPoolSpec.Parse(inputs[0].AsString());
        var rarity = (int)inputs[1].AsInt();
        var pick = (int)inputs[2].AsInt();
        var exclusions = ReadExclusions(inputs, 3);
        var items = Bucket(spec, rarity, exclusions);
        outputs[0] = Value.Of(pick >= 0 && pick < items.Count ? items[pick] : (long)RewardCompute.NoPoolIndex);
    }

    /// <summary>
    /// 源 `MerchantCardEntry.GetCost` + `CalcCost`（`MerchantCardEntry.cs`）：
    /// `_cost = RoundToInt(GetCost(card) * Shops.NextFloat(0.95f, 1.05f))`；在售 `_cost /= 2`（整数除，截断）。
    /// `GetCost` 对无色池先 `RoundToInt(num * 1.15f)`。
    /// </summary>
    private static void ExecuteCardPrice(IReadOnlyList<Value> inputs, Value[] outputs)
    {
        var rarity = (int)inputs[0].AsInt();
        var colorless = inputs[1].AsBool();
        var bits = inputs[2].AsInt();
        var onSale = inputs[3].AsBool();

        var baseCost = rarity switch
        {
            RewardRarity.Rare => CardRarePrice,
            RewardRarity.Uncommon => CardUncommonPrice,
            _ => CardCommonPrice,
        };
        if (colorless)
        {
            baseCost = (int)MathF.Round(baseCost * ColorlessPriceMultiplier);
        }

        var cost = (int)MathF.Round(baseCost * Range(bits, CardPriceMinFactor, CardPriceMaxFactor));
        if (onSale)
        {
            cost /= 2;
        }

        outputs[0] = Value.Of((long)cost);
    }

    /// <summary>源 `CardFactory.RollForUpgrade(..., -999999999m)`：抽数照消费，结果恒 false（见 `DeclareCardUpgrade`）。</summary>
    private static void ExecuteCardUpgrade(IReadOnlyList<Value> inputs, Value[] outputs)
    {
        _ = inputs[0].AsInt();
        outputs[0] = Value.Of(false);
    }

    /// <summary>源 `RelicFactory.RollRarity(player.PlayerRng.Rewards)`（`RelicFactory.cs:80-94`），与宝箱同一张表。</summary>
    private static void ExecuteRelicRarity(IReadOnlyList<Value> inputs, Value[] outputs)
    {
        outputs[0] = Value.Of(TreasureRelicRules.RarityForPayload(inputs[0].AsInt()));
    }

    /// <summary>
    /// 源 `MerchantRelicEntry.CalcCost`：`_cost = (int)Math.Round((float)Model.MerchantCost * Shops.NextFloat(0.85f, 1.15f))`。
    /// 卖店三档 = `RollRarity`（Common/Uncommon/Rare）或固定 `RelicRarity.Shop`。
    /// </summary>
    private static void ExecuteRelicPrice(IReadOnlyList<Value> inputs, Value[] outputs)
    {
        var rarity = inputs[0].AsString();
        var baseCost = rarity switch
        {
            "Common" => RelicCommonPrice,
            "Uncommon" => RelicUncommonPrice,
            "Rare" => RelicRarePrice,
            "Shop" => RelicShopPrice,
            "Ancient" or "Event" or "Starter" => RelicAncientPrice,
            "None" => 1,
            _ => throw new InvalidOperationException($"base-spire: 商店遗物稀有度 '{rarity}' 不在源 `RelicModel.MerchantCost` 表里"),
        };

        var factor = Range(inputs[1].AsInt(), RelicPriceMinFactor, RelicPriceMaxFactor);
        outputs[0] = Value.Of((long)(int)Math.Round((double)(baseCost * factor)));
    }

    /// <summary>
    /// 源 `PotionFactory.CreateRandomPotion`（`PotionFactory.cs`）：`num &lt;= 0.1f → Rare`、
    /// `num &lt;= 0.35f → Uncommon`、否则 `Common`（**保留 `&lt;=`**）。输出还带 ordinal 供 pick/price 复用。
    /// </summary>
    private static void ExecutePotionRarity(IReadOnlyList<Value> inputs, Value[] outputs)
    {
        var roll = RewardCompute.RollOf(inputs[0].AsInt());
        var rarity = roll <= PotionRareThreshold
            ? RewardRarity.Rare
            : roll <= PotionUncommonThreshold
                ? RewardRarity.Uncommon
                : RewardRarity.Common;
        outputs[0] = Value.Of((long)rarity);
        outputs[1] = Value.Of(RarityText(rarity));
    }

    /// <summary>源 `MerchantPotionEntry.GetCost` + `CalcCost`：`Round(GetCost(rarity) * Shops.NextFloat(0.95f, 1.05f))`。</summary>
    private static void ExecutePotionPrice(IReadOnlyList<Value> inputs, Value[] outputs)
    {
        var baseCost = (int)inputs[0].AsInt() switch
        {
            RewardRarity.Rare => PotionRarePrice,
            RewardRarity.Uncommon => PotionUncommonPrice,
            _ => PotionCommonPrice,
        };

        var factor = Range(inputs[1].AsInt(), CardPriceMinFactor, CardPriceMaxFactor);
        outputs[0] = Value.Of((long)(int)MathF.Round(baseCost * factor));
    }

    // -- 池/稀有度公共逻辑 ---------------------------------------------------------------------------------------

    /// <summary>
    /// 源 `CardFactory.GetNextAllowedRarity`（`CardFactory.cs:263-281`）：`Common→Uncommon→Rare→Common` 成环上卷，
    /// 回到已见过的档位 ⇒ `None`（同 `RewardCompute.NextAllowed`，只是这里的黑名单是 n 元）。
    /// </summary>
    private static int NextAllowed(RewardPoolSpec spec, int rarity, IReadOnlyList<int> exclusions)
    {
        var seen = new HashSet<int> { rarity };
        while (CountOf(spec, rarity, exclusions) == 0 && rarity != RewardRarity.None)
        {
            rarity = RewardRarity.NextHighestWithWrapping(rarity);
            if (!seen.Add(rarity))
            {
                return RewardRarity.None;
            }
        }

        return rarity;
    }

    private static int CountOf(RewardPoolSpec spec, int rarity, IReadOnlyList<int> exclusions)
    {
        var count = 0;
        foreach (var entry in spec.RarityBucket(rarity))
        {
            if (!exclusions.Contains(entry.Index))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>`SourceWeightedPickBackend`/`RewardCompute` 的既有口径：保留池声明顺序，黑名单按全局下标排除。</summary>
    private static List<int> Bucket(RewardPoolSpec spec, int rarity, IReadOnlyList<int> exclusions)
    {
        var items = new List<int>();
        foreach (var entry in spec.RarityBucket(rarity))
        {
            if (!exclusions.Contains(entry.Index))
            {
                items.Add(entry.Index);
            }
        }

        return items;
    }

    private static int[] ReadExclusions(IReadOnlyList<Value> inputs, int start)
    {
        var exclusions = new int[ExclusionCount];
        for (var i = 0; i < ExclusionCount; i++)
        {
            exclusions[i] = (int)inputs[start + i].AsInt();
        }

        return exclusions;
    }

    /// <summary>源 `Rng.NextFloat(min, max)`：`(float)(NextDouble() * (double)(max - min) + (double)min)`。</summary>
    private static float Range(long bits, float min, float max)
    {
        var unit = (double)bits * (1.0 / (double)RewardCompute.Span);
        return (float)(unit * (double)(max - min) + (double)min);
    }

    private static string RarityText(int rarity) => rarity switch
    {
        RewardRarity.Rare => "rare",
        RewardRarity.Uncommon => "uncommon",
        RewardRarity.Common => "common",
        RewardRarity.Basic => "basic",
        _ => "none",
    };

    // -- 声明辅助 ------------------------------------------------------------------------------------------------

    /// <summary>
    /// 排除项（黑名单）必须容纳 `RewardCompute.NoPoolIndex`(999) 哨兵：`ExecutePick` 在池空/越界时把它写进
    /// `PoolIndex`，随后那张"未解析"的条目仍会把该值当排除项传给后面的条目，上界若只有 254 会在执行期边界检查里炸。
    /// </summary>
    private static FieldTypeSchema ExclusionSchema() => FieldTypeSchema.Int(-1, RewardCompute.NoPoolIndex);

    private static T Asset<T>(EnginePackageBuilder package, string name, string entry, string backendId,
        IReadOnlyList<(string Name, FieldTypeSchema Type)> inputs,
        IReadOnlyList<(string Name, FieldTypeSchema Type)> outputs,
        Func<ComputeAssetSymbol, T> build)
    {
        var asset = package.ComputeAsset(name);
        asset.BackendId = backendId;
        asset.Entry = entry;
        asset.SourcePath = SourcePath;
        asset.SourceHash = SourceHash;
        foreach (var input in inputs)
        {
            asset.Input(input.Name, input.Type);
        }

        foreach (var output in outputs)
        {
            asset.Output(output.Name, output.Type);
        }

        return build(asset);
    }

    /// <summary>`shop-card-count` / `shop-card-pick` 共用的输入形状（药水池 pick 复用 pick 条目）。</summary>
    private static ShopPoolAsset PoolAsset(EnginePackageBuilder package, string name, string entry, string backendId,
        string resultName)
    {
        var pick = string.Equals(entry, CardPickEntry, StringComparison.Ordinal);
        var inputs = new List<(string, FieldTypeSchema)>
        {
            ("Spec", FieldTypeSchema.String(8192)),
            ("Ordinal", FieldTypeSchema.Int(0, 10)),
        };
        if (pick)
        {
            inputs.Add(("Pick", FieldTypeSchema.Int(0, RewardCompute.NoPoolIndex)));
        }

        for (var i = 0; i < ExclusionCount; i++)
        {
            inputs.Add(("Ex" + (char)('A' + i), ExclusionSchema()));
        }

        return Asset(package, name, entry, backendId, inputs,
            [(resultName, FieldTypeSchema.Int(-1, RewardCompute.NoPoolIndex))],
            asset =>
            {
                var exclusions = new FieldSymbol[ExclusionCount];
                for (var i = 0; i < ExclusionCount; i++)
                {
                    exclusions[i] = asset.Inputs[pick ? 3 + i : 2 + i];
                }

                return new ShopPoolAsset(asset, asset.Inputs[0], asset.Inputs[1], exclusions, asset.Outputs[0]);
            });
    }
}

/// <summary>`shop-card-rarity` 的 IR 符号。`Available=false` 表示上卷一圈仍无可建成员。</summary>
public sealed record ShopCardRarityAsset(
    ComputeAssetSymbol Asset,
    FieldSymbol Bits,
    FieldSymbol Spec,
    FieldSymbol OffsetBits,
    IReadOnlyList<FieldSymbol> Exclusions,
    FieldSymbol Ordinal,
    FieldSymbol Rarity,
    FieldSymbol Available);

/// <summary>`shop-card-count` / `shop-card-pick`（= 药水池 pick）的 IR 符号。</summary>
public sealed record ShopPoolAsset(
    ComputeAssetSymbol Asset,
    FieldSymbol Spec,
    FieldSymbol Ordinal,
    IReadOnlyList<FieldSymbol> Exclusions,
    FieldSymbol Result);

/// <summary>`shop-card-price` 的 IR 符号。</summary>
public sealed record ShopCardPriceAsset(
    ComputeAssetSymbol Asset,
    FieldSymbol Ordinal,
    FieldSymbol Colorless,
    FieldSymbol Bits,
    FieldSymbol OnSale,
    FieldSymbol Price);

/// <summary>`shop-card-upgrade` 的 IR 符号（抽数照消费，结果恒 false）。</summary>
public sealed record ShopCardUpgradeAsset(ComputeAssetSymbol Asset, FieldSymbol Bits, FieldSymbol Upgraded);

/// <summary>`shop-relic-rarity` 的 IR 符号。</summary>
public sealed record ShopRelicRarityAsset(ComputeAssetSymbol Asset, FieldSymbol Bits, FieldSymbol Rarity);

/// <summary>`shop-relic-price` 的 IR 符号。</summary>
public sealed record ShopRelicPriceAsset(
    ComputeAssetSymbol Asset,
    FieldSymbol Rarity,
    FieldSymbol Bits,
    FieldSymbol Price);

/// <summary>`shop-potion-rarity` 的 IR 符号。</summary>
public sealed record ShopPotionRarityAsset(
    ComputeAssetSymbol Asset,
    FieldSymbol Bits,
    FieldSymbol Ordinal,
    FieldSymbol Rarity);

/// <summary>`shop-potion-price` 的 IR 符号。</summary>
public sealed record ShopPotionPriceAsset(
    ComputeAssetSymbol Asset,
    FieldSymbol Ordinal,
    FieldSymbol Bits,
    FieldSymbol Price);
