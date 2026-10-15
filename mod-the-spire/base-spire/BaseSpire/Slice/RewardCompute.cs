using System.Globalization;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice;

/// <summary>
/// ENV-T05：源 `CardRarity` 的 ordinal（逐字照抄 `sts2/src/Core/Entities/Cards/CardRarity.cs`）。
/// 奖励池的稀有度、稀有度 roll 的输出、`GetNextHighestRarityWithWrapping` 的上卷都用这一套编号。
/// </summary>
public static class RewardRarity
{
    public const int None = 0;
    public const int Basic = 1;
    public const int Common = 2;
    public const int Uncommon = 3;
    public const int Rare = 4;
    public const int Ancient = 5;
    public const int Event = 6;
    public const int Token = 7;
    public const int Status = 8;
    public const int Curse = 9;
    public const int Quest = 10;

    /// <summary>池规格里的稀有度字符（只用于把池成员表编进 IR 常量字符串）。</summary>
    public static char CharOf(int rarity) => rarity switch
    {
        Common => 'c',
        Uncommon => 'u',
        Rare => 'r',
        Basic => 'b',
        Ancient => 'a',
        _ => 'x',
    };

    public static int Of(char value) => value switch
    {
        'c' => Common,
        'u' => Uncommon,
        'r' => Rare,
        'b' => Basic,
        'a' => Ancient,
        _ => None,
    };

    /// <summary>
    /// 源 `CardRarityExtensions.GetNextHighestRarityWithWrapping`（`CardRarityExtensions.cs:14-31`）：
    /// `Common→Uncommon→Rare→Common` 成环；其余（含 `Basic`）不进环 ⇒ `None`（"没有下一个可用稀有度"）。
    /// </summary>
    public static int NextHighestWithWrapping(int rarity) => rarity switch
    {
        None => None,
        Basic => Common,
        Common => Uncommon,
        Uncommon => Rare,
        Rare => Common,
        _ => None,
    };
}

/// <summary>奖励池成员：`Index` 是它在**源声明顺序**（冻结清单）里的下标。</summary>
public readonly record struct RewardPoolEntry(int Index, int Rarity, bool Upgradeable);

/// <summary>
/// 奖励池规格：把"成员 + 源顺序 + 稀有度 + 可升级"编成一个字符串常量传给计算资产。
/// 引擎的 `IComputeBackend` 看不到内容模块的状态，所以池表必须以**输入**形式传进去
/// （与地图资产把 payload 串当输入同理）。
///
/// 编码：成员之间 `;` 分隔，每个成员 `&lt;稀有度字符&gt;&lt;下标&gt;&lt;u|n&gt;`，顺序即源顺序。
/// </summary>
public sealed class RewardPoolSpec
{
    private readonly List<RewardPoolEntry>[] byRarity = CreateBuckets();
    private readonly Dictionary<int, RewardPoolEntry> byIndex = [];

    private RewardPoolSpec()
    {
    }

    public IReadOnlyList<RewardPoolEntry> Entries { get; private set; } = [];

    public static string Encode(IEnumerable<RewardPoolEntry> entries)
    {
        var parts = new List<string>();
        foreach (var entry in entries)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture,
                $"{RewardRarity.CharOf(entry.Rarity)}{entry.Index}{(entry.Upgradeable ? 'u' : 'n')}"));
        }

        return string.Join(';', parts);
    }

    public static RewardPoolSpec Parse(string spec)
    {
        var parsed = new RewardPoolSpec();
        var entries = new List<RewardPoolEntry>();
        foreach (var part in spec.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.Length < 3)
            {
                continue;
            }

            var rarity = RewardRarity.Of(part[0]);
            var upgradeable = part[^1] == 'u';
            if (!int.TryParse(part.AsSpan(1, part.Length - 2), NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out var index))
            {
                continue;
            }

            var entry = new RewardPoolEntry(index, rarity, upgradeable);
            entries.Add(entry);
            parsed.byIndex[index] = entry;
            if (rarity >= 0 && rarity < parsed.byRarity.Length)
            {
                parsed.byRarity[rarity].Add(entry);
            }
        }

        parsed.Entries = entries;
        return parsed;
    }

    public IReadOnlyList<RewardPoolEntry> RarityBucket(int rarity) =>
        rarity >= 0 && rarity < byRarity.Length ? byRarity[rarity] : [];

    public RewardPoolEntry? EntryAt(int poolIndex) =>
        byIndex.TryGetValue(poolIndex, out var entry) ? entry : null;

    /// <summary>
    /// 源 `CardFactory.CreateForReward`：`options2.Except(blacklist).Where(c =&gt; c.Rarity == rarity)`。
    /// 已抽中的候选下标（`idxA`/`idxB`，`NoPoolIndex` 表示没有）被排除，其余**保持源顺序**。
    /// </summary>
    public List<RewardPoolEntry> Effective(int rarity, int idxA, int idxB)
    {
        var result = new List<RewardPoolEntry>();
        foreach (var entry in RarityBucket(rarity))
        {
            if (entry.Index == idxA || entry.Index == idxB)
            {
                continue;
            }

            result.Add(entry);
        }

        return result;
    }

    private static List<RewardPoolEntry>[] CreateBuckets()
    {
        var buckets = new List<RewardPoolEntry>[11];
        for (var i = 0; i < buckets.Length; i++)
        {
            buckets[i] = [];
        }

        return buckets;
    }
}

/// <summary>
/// ENV-T05：正规战斗奖励的**纯计算**部分（源 `CardFactory` / `CardRarityOdds` / `PotionRewardOdds` 的
/// 逐字移植）。**不接触 RNG / Session / State**：输入是 IR 侧已经抽好的 53 位 payload
/// （`raw64 &gt;&gt; 11`，与 `SourceWeightedPick` 同一口径），输出是"这次抽到了什么"。
///
/// 这样 IR 侧每一笔抽数都对应源里的一笔，能逐笔对账（见 `RewardWiring` 的抽取序列）。
/// </summary>
public static class RewardCompute
{
    public const string PotionDropAssetName = "RewardPotionDrop";
    public const string CardRarityAssetName = "RewardCardRarity";
    public const string CardCountAssetName = "RewardCardCount";
    public const string CardPickAssetName = "RewardCardPick";
    public const string CardUpgradeAssetName = "RewardCardUpgrade";

    public const string PotionDropEntry = "reward-potion-drop";
    public const string CardRarityEntry = "reward-card-rarity";
    public const string CardCountEntry = "reward-card-count";
    public const string CardPickEntry = "reward-card-pick";
    public const string CardUpgradeEntry = "reward-card-upgrade";

    /// <summary>
    /// 5 个奖励资产共用的**源身份**（审计用）。`SourcePath` 是单一位置（引擎只要求非空，且它会进包身份），
    /// 参与移植的**源文件清单**放在 `SourceHash` 里：`RewardsSet.GenerateRewardsFor/RollForPotionAndAddTo`、
    /// `GoldReward.Populate/OnSelect`、`CardReward.Populate/OnSelect/OnSkipped`、`CardFactory.CreateForReward/
    /// RollForRarity/RollForUpgrade`、`PotionReward.Populate`、`PotionFactory.CreateRandomPotionOutOfCombat`、
    /// `CardRarityOdds`、`PotionRewardOdds`、`IroncladCardPool.GenerateAllCards`。
    /// </summary>
    public const string SourcePath = "mod-the-spire/base-spire/BaseSpire/Slice/RewardCompute";

    /// <summary>源身份哈希（移植自 `sts2` 冻结源的位置链）。</summary>
    public const string SourceHash =
        "Rewards/RewardsSet.cs+Rewards/GoldReward.cs+Rewards/CardReward.cs+Rewards/PotionReward.cs+"
        + "Rewards/Reward.cs+Factories/CardFactory.cs+Factories/PotionFactory.cs+Odds/CardRarityOdds.cs+"
        + "Odds/PotionRewardOdds.cs+Runs/CardCreationOptions.cs+Models/CardPools/IroncladCardPool.cs@sts2-reward-chain-v1";

    /// <summary>与 `SourceWeightedPick.Span` 一致：`raw64 &gt;&gt; 11` 恰好是源的 `NextDouble()`。</summary>
    public const long Span = 1L << 53;

    private const double Unit = 1.0 / (double)Span;

    /// <summary>池下标里的"没有"（IR 侧 Int 不可为负，故用一个越界哨兵，与池规模无关）。</summary>
    public const int NoPoolIndex = 999;

    /// <summary>最大池下标（源单机 Ironclad 池 85 项）。</summary>
    public const int MaxPoolIndex = 254;

    // -- 源常量（逐字，Ascension 0） ----------------------------------------------------------------

    /// <summary>`PotionRewardOdds._basePotionRewardOdds`（`PotionRewardOdds.cs`，ctor `base(0.4f, rng)`）。</summary>
    public const float PotionBaseOdds = 0.4f;

    /// <summary>`PotionRewardOdds.eliteBonus`（`float num = roomType != RoomType.Elite ? 0f : 0.25f;`）。</summary>
    public const float PotionEliteBonus = 0.25f;

    /// <summary>`Roll` 命中/未命中时 `CurrentValue` 的步长（`-= 0.1f` / `+= 0.1f`）。</summary>
    public const float PotionOddsStep = 0.1f;

    /// <summary>`CardRarityOdds._baseRarityOffset`（ctor `base(-0.05f, rng)`）。</summary>
    public const float CardRarityBaseOffset = -0.05f;

    /// <summary>`CardRarityOdds._maxRarityOffset` 与 `RarityGrowth`（Ascension 0 取 0.01f）。</summary>
    public const float CardRarityMaxOffset = 0.4f;

    public const float CardRarityGrowth = 0.01f;

    /// <summary>`CardRarityOdds.GetBaseOdds(RegularEncounter, …)`（普通战斗；`0.6f/0.37f/0.03f`）。</summary>
    public const float RegularCommonOdds = 0.6f;

    public const float RegularUncommonOdds = 0.37f;

    public const float RegularRareOdds = 0.03f;

    /// <summary>`CardFactory.UpgradedCardOddScaling`（Ascension 0 = 0.25m，`CardFactory.cs:23`）。</summary>
    public const decimal UpgradedCardOddScaling = 0.25m;

    /// <summary>金币奖励：`GoldReward.defaultMinGoldAmount/defaultMaxGoldAmount` = 10/20。</summary>
    public const int DefaultMinGold = 10;

    public const int DefaultMaxGold = 20;

    /// <summary>`Rng.NextFloat()` 的等价物：`(float)(NextDouble() * (max - min) + min)`，`min=0f/max=1f`。</summary>
    public static float RollOf(long bits) => (float)(bits * Unit);

    public static float FloatOf(long bits) => BitConverter.Int32BitsToSingle(unchecked((int)(uint)bits));

    public static long BitsOf(float value) => (uint)BitConverter.SingleToInt32Bits(value);

    /// <summary>
    /// 后端按 `asset.Entry` 分派奖励资产；不是奖励入口就返回 false（由调用方继续转发）。
    /// </summary>
    public static bool TryExecute(string entry, IReadOnlyList<Value> inputs, Value[] outputs)
    {
        switch (entry)
        {
            case PotionDropEntry:
                ExecutePotionDrop(inputs, outputs);
                return true;
            case CardRarityEntry:
                ExecuteCardRarity(inputs, outputs);
                return true;
            case CardCountEntry:
                ExecuteCardCount(inputs, outputs);
                return true;
            case CardPickEntry:
                ExecuteCardPick(inputs, outputs);
                return true;
            case CardUpgradeEntry:
                ExecuteCardUpgrade(inputs, outputs);
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// 源 `PotionRewardOdds.Roll(player, ascensionManager, roomType)`（`PotionRewardOdds.cs`）。
    /// 普通战斗：`num3 = CurrentValue + 0f * 0.5f`；`num4 = _rng.NextFloat()`；命中 `&lt; num3` 则
    /// `CurrentValue -= 0.1f` 且返回 true，否则 `CurrentValue += 0.1f` 且返回 false（**无夹取**）。
    /// </summary>
    private static void ExecutePotionDrop(IReadOnlyList<Value> inputs, Value[] outputs)
    {
        var bits = inputs[0].AsInt();
        var current = FloatOf(inputs[1].AsInt());
        var isElite = inputs[2].AsBool();
        var bonus = isElite ? PotionEliteBonus : 0f;
        var threshold = current + (bonus * 0.5f);
        var roll = RollOf(bits);
        var dropped = roll < threshold;
        var next = dropped ? current - PotionOddsStep : current + PotionOddsStep;
        outputs[0] = Value.Of(dropped);
        outputs[1] = Value.Of(BitsOf(next));
    }

    /// <summary>
    /// 源 `CardFactory.RollForRarity` + `CardRarityOdds.Roll` + `CardFactory.GetNextAllowedRarity`
    /// （`CardFactory.cs:244-281`、`CardRarityOdds.cs:69-131`）。
    ///
    /// 普通战斗（`CardCreationSource.Encounter` + `RegularEncounter`）走 `Roll`：**改未来赔率**；
    /// 赔率 offset 参与"稀有度阈值"（`num2 = RareOdds + offset`），`Rare` 命中后 offset 复位到
    /// `-0.05f`，否则 `min(offset + 0.01f, 0.4f)`。返回**未上卷**的稀有度，然后按
    /// `GetNextAllowedRarity` 上卷到"当前候选集合里非空"的稀有度（`None`＝没有可用稀有度）。
    /// </summary>
    private static void ExecuteCardRarity(IReadOnlyList<Value> inputs, Value[] outputs)
    {
        var bits = inputs[0].AsInt();
        var spec = RewardPoolSpec.Parse(inputs[1].AsString());
        var current = FloatOf(inputs[2].AsInt());
        var idxA = (int)inputs[3].AsInt();
        var idxB = (int)inputs[4].AsInt();

        var roll = RollOf(bits);
        var boss = inputs.Count > 5 && inputs[5].AsBool();
        var rareOdds = boss ? 1f : RegularRareOdds + current;
        int rarity;
        if (roll < rareOdds)
        {
            rarity = RewardRarity.Rare;
        }
        else if (roll < (boss ? 0f : RegularUncommonOdds) + rareOdds)
        {
            rarity = RewardRarity.Uncommon;
        }
        else
        {
            rarity = RewardRarity.Common;
        }

        var next = rarity == RewardRarity.Rare
            ? CardRarityBaseOffset
            : MathF.Min(current + CardRarityGrowth, CardRarityMaxOffset);

        outputs[0] = Value.Of((long)NextAllowed(spec, rarity, idxA, idxB));
        outputs[1] = Value.Of(BitsOf(next));
    }

    /// <summary>源 `CardFactory.GetNextAllowedRarity`（`CardFactory.cs:263-281`）。</summary>
    private static int NextAllowed(RewardPoolSpec spec, int rarity, int idxA, int idxB)
    {
        var seen = new HashSet<int> { rarity };
        while (spec.Effective(rarity, idxA, idxB).Count == 0 && rarity != RewardRarity.None)
        {
            rarity = RewardRarity.NextHighestWithWrapping(rarity);
            if (!seen.Add(rarity))
            {
                return RewardRarity.None;
            }
        }

        return rarity;
    }

    /// <summary>`options2.Except(blacklist).Where(rarity).Count`（黑名单＝已抽中的候选）。</summary>
    private static void ExecuteCardCount(IReadOnlyList<Value> inputs, Value[] outputs)
    {
        var spec = RewardPoolSpec.Parse(inputs[0].AsString());
        var rarity = (int)inputs[1].AsInt();
        var idxA = (int)inputs[2].AsInt();
        var idxB = (int)inputs[3].AsInt();
        outputs[0] = Value.Of((long)spec.Effective(rarity, idxA, idxB).Count);
    }

    /// <summary>`rng.NextItem(items)`：`index = NextInt(0, items.Count)`，返回该成员的池下标。</summary>
    private static void ExecuteCardPick(IReadOnlyList<Value> inputs, Value[] outputs)
    {
        var spec = RewardPoolSpec.Parse(inputs[0].AsString());
        var rarity = (int)inputs[1].AsInt();
        var pick = (int)inputs[2].AsInt();
        var idxA = (int)inputs[3].AsInt();
        var idxB = (int)inputs[4].AsInt();
        var items = spec.Effective(rarity, idxA, idxB);
        outputs[0] = Value.Of(pick >= 0 && pick < items.Count ? items[pick].Index : (long)NoPoolIndex);
    }

    /// <summary>
    /// 源 `CardFactory.RollForUpgrade(player, card, 0m, rng)`（`CardFactory.cs:288-305`）：
    /// **先无条件**抽一个 `NextFloat`，然后仅当 `card.IsUpgradable` 才比较：
    /// `originalOdds = 0m`；非 Rare 再加 `CurrentActIndex * 0.25m`；`num &lt;= originalOdds` 才升级。
    ///
    /// 第一幕 `CurrentActIndex == 0` ⇒ `originalOdds == 0m`，只有 `NextFloat() == 0f` 才升级。
    /// 这里刻意保留 `(decimal)` 转换与 `&lt;=`（**不改成 `&lt;`**）：`NextFloat()` 可以恰好为 0f。
    /// </summary>
    private static void ExecuteCardUpgrade(IReadOnlyList<Value> inputs, Value[] outputs)
    {
        var bits = inputs[0].AsInt();
        var spec = RewardPoolSpec.Parse(inputs[1].AsString());
        var poolIndex = (int)inputs[2].AsInt();
        var actIndex = (int)inputs[3].AsInt();

        var entry = spec.EntryAt(poolIndex);
        if (entry is null || !entry.Value.Upgradeable)
        {
            // 抽数照样发生（上面已经用了 bits 的语义），只是不升级。
            outputs[0] = Value.Of(false);
            return;
        }

        var num = (decimal)RollOf(bits);
        var originalOdds = 0m;
        if (entry.Value.Rarity != RewardRarity.Rare)
        {
            originalOdds += (decimal)actIndex * UpgradedCardOddScaling;
        }

        // base 侧没有遗物接管 `Hook.ModifyCardRewardUpgradeOdds`（正式内容 StartingRelics 里无此项）。
        outputs[0] = Value.Of(num <= originalOdds);
    }
}
