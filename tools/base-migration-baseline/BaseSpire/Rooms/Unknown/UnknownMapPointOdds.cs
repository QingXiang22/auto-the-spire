using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Rooms;

/// <summary>
/// ENV-T09-unknown-room-modules-20261012：源 <c>UnknownMapPointOdds</c> 的**问号房赔率与房型判定**，
/// 逐行照抄 <c>sts2/src/Core/Odds/UnknownMapPointOdds.cs</c>（Ascension 0、无遗物/修饰符接管）。
///
/// 三处最容易写成"近似"的地方，这里都按源来：
/// <list type="number">
/// <item>初值是 **float32 常量**（Monster 0.1f / Elite -1f / Treasure 0.02f / Shop 0.03f）；
///       赔率状态在整个幕内**跨房间持久**，命中即复位、未命中即累加，绝不重算。</item>
/// <item>抽取判据是 `_rng.NextFloat()`：`(float)(NextDouble() * 1.0 + 0.0)`，即
///       `(float)(payload * 2^-53)`。乘法在 **double** 里做、只向 float32 转换一次；输入是
///       `SampleInt(unknown_map_point, 0, 2^53)` 的 53 位 payload（与地图/奖励同一口径）。</item>
/// <item>累加器 `float num3 = 0f` 是**逐项 float32 加法**（不是 LINQ `Sum` 的 double 累加器，
///       与 <c>SourceWeightedPick</c> 的语义**不同**），命中判据是 `num2 &lt;= num3`；
///       `_nonEventOdds` / `_baseOdds` 的遍历顺序都是字典插入序 Monster→Elite→Treasure→Shop；
///       负赔率**不进累加**（因此永不被抽中），但未命中时仍按源 `+= base`（Elite 会越来越负）。</item>
/// </list>
///
/// 已知边界（本包明确登记，不静默替代）：
/// <list type="bullet">
/// <item>源 `Roll` 开头的"首次运行固定顺序"分支（`runState.UnlockState.NumberOfRuns == 0` 时
///       &lt;2 个已访问 Unknown → Event、==2 → Monster）**未建模**：本工厂固定"全解锁"策略、没有
///       profile 运行次数，因此一律走加权分支。这是范围边界，不是把该分支当成空实现。</item>
/// <item>`Hook.ModifyUnknownMapPointRoomTypes` / `Hook.ModifyOddsIncreaseForUnrolledRoomType` 的
///       接管者（DeadlyEvents 修饰符、GoldenCompass/JuzuBracelet/LanternKey 遗物、ZenWeaver 等）当前
///       既不在 dev-map 的可见遗物集合里、也没有效果实现，因此这里调用源的**默认实现**（恒等）。
///       一旦这些遗物/修饰符落地，必须从这里接进来，不能在房间叶里就地改判。</item>
/// </list>
/// </summary>
public static class UnknownMapPointOdds
{
    /// <summary>计算资产名（进规则身份）。</summary>
    public const string AssetName = "UnknownMapPointOddsRoll";

    /// <summary>后端按 `asset.Entry` 分派。</summary>
    public const string Entry = "unknown-map-point-roll";

    public const string SourcePath = "sts2/src/Core/Odds/UnknownMapPointOdds.cs";

    public const string SourceHash =
        "Odds/UnknownMapPointOdds.cs+Runs/RunManager.cs:BuildRoomTypeBlacklist/RollRoomTypeFor@"
        + "unknown-map-point-odds-v1";

    /// <summary>`SampleInt(stream, 0, 2^53)` 的跨度：恰好是源 `NextDouble()` 的 53 位值。</summary>
    public const long Span = 1L << 53;

    /// <summary>`Rng.NextDouble()` → `[0,1)` 的换算（`2^-53`，二进制精确）。</summary>
    private const double Unit = 1.0 / (double)Span;

    /// <summary>源 `UnknownMapPointOdds.baseMonsterOdds`。</summary>
    public const float BaseMonsterOdds = 0.1f;

    /// <summary>源 `UnknownMapPointOdds.baseEliteOdds`（负值 ⇒ 永不被抽中；DeadlyEvents 会把它抬到 0.1f）。</summary>
    public const float BaseEliteOdds = -1f;

    /// <summary>源 `UnknownMapPointOdds.baseTreasureOdds`。</summary>
    public const float BaseTreasureOdds = 0.02f;

    /// <summary>源 `UnknownMapPointOdds.baseShopOdds`。</summary>
    public const float BaseShopOdds = 0.03f;

    /// <summary>问号房赔率流的源流类型名（引擎流名 `base-spire:run:unknown_map_point`）。</summary>
    public const string StreamType = "UnknownMapPoint";

    // -- 源 `RoomType`（`Rooms/RoomType.cs` 的成员顺序即 ordinal） ------------------------------------
    public const int Unassigned = 0;
    public const int Monster = 1;
    public const int Elite = 2;
    public const int Boss = 3;
    public const int Treasure = 4;
    public const int Shop = 5;
    public const int Event = 6;
    public const int RestSite = 7;
    public const int Map = 8;

    /// <summary>`_nonEventOdds` / `_baseOdds` 的字典插入序（源里两张表的键集合与顺序一致）。</summary>
    public static readonly IReadOnlyList<(int RoomType, float Base)> Odds =
    [
        (Monster, BaseMonsterOdds),
        (Elite, BaseEliteOdds),
        (Treasure, BaseTreasureOdds),
        (Shop, BaseShopOdds),
    ];

    /// <summary>
    /// 5 个赔率字段共用的 float32 位型载体。刻意用**有符号 int32**（`SingleToInt32Bits`）而不是
    /// `(uint)`：负赔率的位型（-1f = 0xBF800000）在 uint 下是 3221225472，超出 `Int32` 字段域，
    /// 会被引擎的越界校验拒绝；有符号位型（-1082130432）与 `Int32BitsToSingle` 一一对应。
    /// </summary>
    public static long BitsOf(float value) => BitConverter.SingleToInt32Bits(value);

    public static float FloatOf(long bits) => BitConverter.Int32BitsToSingle((int)bits);

    /// <summary>源 `Rng.NextFloat()`：`(float)(NextDouble() * (max - min) + min)`，`min=0f/max=1f`。</summary>
    public static float RollOf(long payload) => (float)(payload * Unit);

    /// <summary>
    /// 源 `UnknownMapPointOdds.Roll` 的一次完整判定（含"命中复位 / 未命中累加"的赔率更新）。
    /// 纯函数：不接触 RNG / Session / State，输入是已经抽好的 payload 与当前赔率状态。
    /// </summary>
    /// <param name="payload">`SampleInt(stream, 0, 2^53)` 的 53 位值。</param>
    /// <param name="monster">当前 `_nonEventOdds[Monster]`（float32）。</param>
    /// <param name="blacklistShop">源 `BuildRoomTypeBlacklist` 的结果：只有 Shop 会被拉黑。</param>
    public static UnknownOddsRoll Roll(long payload, float monster, float elite, float treasure, float shop,
        bool blacklistShop)
    {
        var roll = RollOf(payload);
        var next = new[] { monster, elite, treasure, shop };
        var blacklisted = new[] { false, false, false, blacklistShop };

        // 源：`roomTypes` 恒含 Event（黑名单只可能含 Shop），且没有任何 hook 移除它
        // ⇒ 默认值就是 `RoomType.Event`（源里 `roomTypes.Contains(Event) ? Event : Order().First()`）。
        var roomType = Event;
        var sum = 0f;
        for (var index = 0; index < Odds.Count; index++)
        {
            if (blacklisted[index] || next[index] < 0f)
            {
                continue;
            }

            sum += next[index];
            if (roll <= sum)
            {
                roomType = Odds[index].RoomType;
                break;
            }
        }

        for (var index = 0; index < Odds.Count; index++)
        {
            if (roomType == Odds[index].RoomType)
            {
                next[index] = Odds[index].Base;
            }
            else if (!blacklisted[index])
            {
                next[index] += Odds[index].Base;
            }
        }

        return new UnknownOddsRoll(roomType, next[0], next[1], next[2], next[3]);
    }

    /// <summary>后端按 `asset.Entry` 分派；不是本资产就返回 false（由调用方继续转发）。</summary>
    public static bool TryExecute(string entry, IReadOnlyList<Value> inputs, Value[] outputs)
    {
        if (!string.Equals(entry, Entry, StringComparison.Ordinal))
        {
            return false;
        }

        var result = Roll(inputs[0].AsInt(), FloatOf(inputs[1].AsInt()), FloatOf(inputs[2].AsInt()),
            FloatOf(inputs[3].AsInt()), FloatOf(inputs[4].AsInt()), inputs[5].AsBool());
        outputs[0] = Value.OfEnum(result.RoomType);
        outputs[1] = Value.Of(BitsOf(result.Monster));
        outputs[2] = Value.Of(BitsOf(result.Elite));
        outputs[3] = Value.Of(BitsOf(result.Treasure));
        outputs[4] = Value.Of(BitsOf(result.Shop));
        return true;
    }
}

/// <summary>一次问号房判定的结果：命中房型（源 `RoomType` ordinal）+ 更新后的四个非事件赔率。</summary>
public readonly record struct UnknownOddsRoll(int RoomType, float Monster, float Elite, float Treasure, float Shop);
