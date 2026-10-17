using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Rooms;

/// <summary>
/// 问号房共享模块（一）：**源赔率状态 + 一次房型判定 + 源黑名单**。
///
/// 归属边界（见 [横向扩展问号事件](../../../../../../docs/design/mod-the-spire/how-to-add-a-room.md#横向扩展问号事件)）：
/// <list type="bullet">
/// <item>本模块拥有四个非事件赔率（float32 位型，跨房间持久）与判定用的纯计算资产；</item>
/// <item>本模块拥有源 `RunManager.BuildRoomTypeBlacklist` 的规则（上一间房是 Shop，或当前节点的
///       全部子节点都是 Shop）；</item>
/// <item>**不**拥有事件池/访问生命周期（<see cref="EventVisitLifecycle"/>），也**不**拥有任何具体事件内容。</item>
/// </list>
///
/// 判定出的实际房型写在 <see cref="MapRoomWiring.RunActualRoomType"/>（源 `MapPointHistoryEntry.RoomType`），
/// 地图节点类型保持 `Unknown`，绝不改写节点来"假装"是别的房。
/// </summary>
public sealed class UnknownRoomWiring
{
    private readonly EntityTypeSymbol runType;
    private readonly MapRoomWiring map;
    private readonly FieldSymbol payloadInput;
    private readonly FieldSymbol monsterInput;
    private readonly FieldSymbol eliteInput;
    private readonly FieldSymbol treasureInput;
    private readonly FieldSymbol shopInput;
    private readonly FieldSymbol blacklistInput;
    private readonly FieldSymbol roomTypeOutput;
    private readonly FieldSymbol monsterOutput;
    private readonly FieldSymbol eliteOutput;
    private readonly FieldSymbol treasureOutput;
    private readonly FieldSymbol shopOutput;

    public UnknownRoomWiring(EnginePackageBuilder package, EntityTypeSymbol run, MapRoomWiring map)
    {
        ArgumentNullException.ThrowIfNull(package);
        this.runType = run ?? throw new ArgumentNullException(nameof(run));
        this.map = map ?? throw new ArgumentNullException(nameof(map));

        // 源 `_nonEventOdds` 的初值（`RunOddsSet` 构造期）。位型存进 Int 字段，因此比较与累加都在
        // float32 上做，不是十进制近似；默认值即"这一局还没判定过任何问号房"的状态。
        MonsterOdds = Declare("UnknownMonsterOdds", UnknownMapPointOdds.BaseMonsterOdds);
        EliteOdds = Declare("UnknownEliteOdds", UnknownMapPointOdds.BaseEliteOdds);
        TreasureOdds = Declare("UnknownTreasureOdds", UnknownMapPointOdds.BaseTreasureOdds);
        ShopOdds = Declare("UnknownShopOdds", UnknownMapPointOdds.BaseShopOdds);

        Roll = package.ComputeAsset(UnknownMapPointOdds.AssetName);
        Roll.BackendId = BaseSpireComputeBackend.BackendId;
        Roll.SourcePath = UnknownMapPointOdds.SourcePath;
        Roll.SourceHash = UnknownMapPointOdds.SourceHash;
        Roll.Entry = UnknownMapPointOdds.Entry;
        payloadInput = Roll.Input("Payload", FieldTypeSchema.Int(0, UnknownMapPointOdds.Span - 1));
        monsterInput = Roll.Input("MonsterBits", BitShape);
        eliteInput = Roll.Input("EliteBits", BitShape);
        treasureInput = Roll.Input("TreasureBits", BitShape);
        shopInput = Roll.Input("ShopBits", BitShape);
        blacklistInput = Roll.Input("BlacklistShop", FieldTypeSchema.Bool());
        roomTypeOutput = Roll.Output("RoomType", FieldTypeSchema.EnumOf(map.RoomType));
        monsterOutput = Roll.Output("NextMonsterBits", BitShape);
        eliteOutput = Roll.Output("NextEliteBits", BitShape);
        treasureOutput = Roll.Output("NextTreasureBits", BitShape);
        shopOutput = Roll.Output("NextShopBits", BitShape);

        FieldSymbol Declare(string name, float value) => run.AddField(name,
            FieldTypeSchema.Int(int.MinValue, int.MaxValue),
            Value.Of((long)UnknownMapPointOdds.BitsOf(value)), FieldVisibility.Hidden);
    }

    /// <summary>float32 位型字段的形状（`BitConverter.SingleToInt32Bits` 的完整 int32 值域）。</summary>
    private static FieldTypeSchema BitShape => FieldTypeSchema.Int(int.MinValue, int.MaxValue);

    /// <summary>四个非事件赔率的当前状态（float32 位型，源 `_nonEventOdds`）。</summary>
    public FieldSymbol MonsterOdds { get; }

    public FieldSymbol EliteOdds { get; }

    public FieldSymbol TreasureOdds { get; }

    public FieldSymbol ShopOdds { get; }

    /// <summary>问号房判定资产（`base-spire:UnknownMapPointOddsRoll`）。</summary>
    public ComputeAssetSymbol Roll { get; }

    /// <summary>问号房赔率流的引擎流名（`base-spire:run:unknown_map_point`）。</summary>
    public static string StreamName => VanillaRandomLayout.EngineStreamName(UnknownMapPointOdds.StreamType);

    /// <summary>
    /// 源 `RunManager.BuildRoomTypeBlacklist(previousEntry, nextMapPoints)`：只有 Shop 会被拉黑。
    /// `nextMapPoints` 是**当前所在节点**（还没被 `Visit` 改写的那个）的子节点集合 ⇒ 必须在
    /// <see cref="MapRoomEntry.Visit"/> 之前求值；`previousEntry.HasRoomOfType(Shop)` 是上一间房判定出的
    /// 实际房型（<see cref="MapRoomWiring.RunActualRoomType"/> 此刻仍是上一次访问的值）。
    /// </summary>
    public Expr BlacklistShop(IrScope scope, Expr run)
    {
        var children = scope.Relation(map.Children, scope.Field(run, map.RunCurrentNode));
        var everyChildIsShop = scope.And(
            scope.Greater(scope.Count(children), scope.Const(0L)),
            scope.Equal(
                scope.Count(scope.Where(children,
                    node => scope.Equal(scope.Field(node, map.NodeType), scope.EnumConst(map.PointType, "Shop")))),
                scope.Count(children)));
        return scope.Or(
            scope.Equal(scope.Field(run, map.RunActualRoomType), scope.EnumConst(map.RoomType, "Shop")),
            everyChildIsShop);
    }

    /// <summary>
    /// 一次判定：抽一笔 `unknown_map_point` payload，走纯计算资产，写回四个赔率字段；
    /// 返回命中房型所在的 local slot（`RoomType` 枚举值）。
    /// </summary>
    public (Inst[] Steps, int RoomTypeSlot) RollRoomType(IrScope scope, Expr run, Expr blacklistShop)
    {
        var payload = scope.Local("unknownOddsPayload", FieldTypeSchema.Int(0, UnknownMapPointOdds.Span - 1));
        var roomType = scope.Local("unknownRoomType", FieldTypeSchema.EnumOf(map.RoomType));
        var nextMonster = scope.Local("unknownMonsterNext", BitShape);
        var nextElite = scope.Local("unknownEliteNext", BitShape);
        var nextTreasure = scope.Local("unknownTreasureNext", BitShape);
        var nextShop = scope.Local("unknownShopNext", BitShape);

        return (
        [
            // 每个问号房判定恰好一笔抽取（源 `_rng.NextFloat()` 一次）。
            scope.SampleInt(StreamName, scope.Const(0L), scope.Const(UnknownMapPointOdds.Span), payload),
            scope.Compute(Roll, new (FieldSymbol, Expr)[]
            {
                (payloadInput, scope.LocalRef(payload)),
                (monsterInput, scope.Field(run, MonsterOdds)),
                (eliteInput, scope.Field(run, EliteOdds)),
                (treasureInput, scope.Field(run, TreasureOdds)),
                (shopInput, scope.Field(run, ShopOdds)),
                (blacklistInput, blacklistShop),
            }, new (FieldSymbol, int)[]
            {
                (roomTypeOutput, roomType),
                (monsterOutput, nextMonster),
                (eliteOutput, nextElite),
                (treasureOutput, nextTreasure),
                (shopOutput, nextShop),
            }),
            scope.SetField(run, MonsterOdds, scope.LocalRef(nextMonster)),
            scope.SetField(run, EliteOdds, scope.LocalRef(nextElite)),
            scope.SetField(run, TreasureOdds, scope.LocalRef(nextTreasure)),
            scope.SetField(run, ShopOdds, scope.LocalRef(nextShop)),
        ], roomType);
    }

    /// <summary>源 `RoomType` 的成员名（与 <see cref="MapRoomWiring.PointType"/> 同名成员一一对应）。</summary>
    public static string RoomTypeMember(int roomType) => roomType switch
    {
        UnknownMapPointOdds.Unassigned => "Unassigned",
        UnknownMapPointOdds.Monster => "Monster",
        UnknownMapPointOdds.Elite => "Elite",
        UnknownMapPointOdds.Boss => "Boss",
        UnknownMapPointOdds.Treasure => "Treasure",
        UnknownMapPointOdds.Shop => "Shop",
        UnknownMapPointOdds.Event => "Event",
        UnknownMapPointOdds.RestSite => "RestSite",
        UnknownMapPointOdds.Map => "Map",
        _ => throw new ArgumentOutOfRangeException(nameof(roomType), roomType, "不是源 RoomType 的成员"),
    };
}
