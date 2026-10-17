using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Rooms;

/// <summary>
/// 事件级随机的**共享能力**：源 <c>EventModel.BeginEvent</c> 给每个事件实例一条独立 Rng，
/// 种子 = `(uint)((int)runSeed + slot + hash(Id.Entry))`（`Id.Entry` = `StringHelper.Slugify(类名)`，
/// 如 `SunkenStatue` → `SUNKEN_STATUE`；非共享事件用玩家槽位，本工厂固定单人槽位 0）。
///
/// 这条流**不属于**原版 12 条 run 级流，和 `act_{n}_map`、遭遇派生流一样是内容派生流，
/// 因此由 <see cref="BaseSpireRuntimeFactory"/> 在建局时按源公式显式种下
/// （见 <see cref="VanillaRandomLayout.EventStreamName"/> / <see cref="VanillaRandomLayout.EventSeed"/>）。
///
/// 抽取语义按源 <c>Rng</c>：`NextDouble()` = `(payload &gt;&gt; 11) * 2^-53`，
/// `NextInt(min,max)` = `(int)(NextDouble() * (max - min)) + min`（向零截断）。
/// 与地图/奖励同口径：IR 侧一笔 `SampleInt(0, 2^53)`，纯计算只做换算，不碰 RNG/Session/State。
///
/// 已知边界：源每次 `BeginEvent` **新建** `Rng`（计数器从 0 开始），因此同一事件在同一局被再次
/// 开始时仍得到同一串随机数；引擎流是有计数器的，第二次访问会前进。当前幕内不可能重复访问源身份
/// （31 个事件的队列在一个幕里走不完一轮），因此本条差异不可达；写在这里以免被误当成"已对齐"。
/// </summary>
public sealed record EventRandomAsset(ComputeAssetSymbol Asset, FieldSymbol Payload, FieldSymbol Min,
    FieldSymbol Max, FieldSymbol Result)
{
    public const string AssetName = "SourceEventNextInt";

    public const string Entry = "source-event-next-int";

    public const string SourcePath = "mod-the-spire/base-spire/BaseSpire/Rooms/Events/EventRandom.cs";

    public const string SourceHash =
        "Random/Rng.cs:NextInt/NextFloat/NextDouble+Random/MegaRandom.cs:NextDouble+"
        + "Models/EventModel.cs:BeginEvent@event-rng-v1";

    /// <summary>与地图/奖励一致：`SampleInt(stream, 0, 2^53)` 恰好是源 `NextDouble()` 的 53 位值。</summary>
    public const long Span = 1L << 53;

    private const double Unit = 1.0 / (double)Span;

    public static EventRandomAsset Declare(EnginePackageBuilder package)
    {
        var asset = package.ComputeAsset(AssetName);
        asset.BackendId = BaseSpireComputeBackend.BackendId;
        asset.SourcePath = SourcePath;
        asset.SourceHash = SourceHash;
        asset.Entry = Entry;
        return new EventRandomAsset(asset,
            asset.Input("Payload", FieldTypeSchema.Int(0, Span - 1)),
            asset.Input("Min", FieldTypeSchema.Int(-999999, 999999)),
            asset.Input("Max", FieldTypeSchema.Int(-999999, 999999)),
            asset.Output("Value", FieldTypeSchema.Int(-999999, 999999)));
    }

    /// <summary>本事件自己的具名流名（`base-spire:event:&lt;SLUG&gt;`）。</summary>
    public static string StreamName(string sourceName) =>
        VanillaRandomLayout.EventStreamName(VanillaRandomLayout.SourceEventEntry(sourceName));

    /// <summary>`Rng.NextInt(min, max)`：一笔 53 位抽取 + 纯计算换算，结果写进返回的 local slot。</summary>
    public (Inst[] Steps, int ValueSlot) NextInt(IrScope scope, string sourceName, int min, int max)
    {
        var payload = scope.Local("eventRandomPayload", FieldTypeSchema.Int(0, Span - 1));
        var value = scope.Local("eventRandomValue", FieldTypeSchema.Int(-999999, 999999));
        return (
        [
            scope.SampleInt(StreamName(sourceName), scope.Const(0L), scope.Const(Span), payload),
            scope.Compute(Asset, new (FieldSymbol, Expr)[]
            {
                (Payload, scope.LocalRef(payload)),
                (Min, scope.Const((long)min)),
                (Max, scope.Const((long)max)),
            }, new (FieldSymbol, int)[] { (Result, value) }),
        ], value);
    }

    /// <summary>后端按 `asset.Entry` 分派；不是本资产就返回 false（由调用方继续转发）。</summary>
    public static bool TryExecute(string entry, IReadOnlyList<Value> inputs, Value[] outputs)
    {
        if (!string.Equals(entry, Entry, StringComparison.Ordinal))
        {
            return false;
        }

        var bits = inputs[0].AsInt();
        var min = inputs[1].AsInt();
        var max = inputs[2].AsInt();
        var span = max - min;
        // 源 `NextInner(int maxValue)` = `(int)(NextDouble() * maxValue)`；`NextInt(min,max)` 再加 min。
        outputs[0] = Value.Of((long)(int)(bits * Unit * (double)span) + min);
        return true;
    }
}

/// <summary>Source float32 variation -> decimal addition -> integer gold, with all RNG consumption emitted as Engine IR.</summary>
public sealed record EventFloatGoldAsset(ComputeAssetSymbol Asset, FieldSymbol Payload, FieldSymbol Base,
    FieldSymbol Min, FieldSymbol Max, FieldSymbol Result)
{
    public const string Entry = "source-event-float-gold";
    private int nextLocal;

    public static EventFloatGoldAsset Declare(EnginePackageBuilder package)
    {
        var asset = package.ComputeAsset("SourceEventFloatGold");
        asset.BackendId = BaseSpireComputeBackend.BackendId;
        asset.SourcePath = EventRandomAsset.SourcePath;
        asset.SourceHash = "Rng.NextFloat+JungleMazeAdventure.CalculateVars+PlayerCmd.GainGold:decimal-truncate@1";
        asset.Entry = Entry;
        return new(asset, asset.Input("Payload", FieldTypeSchema.Int(0, EventRandomAsset.Span - 1)),
            asset.Input("Base", FieldTypeSchema.Int(0, 999999)),
            asset.Input("Min", FieldTypeSchema.Int(-999999, 999999)),
            asset.Input("Max", FieldTypeSchema.Int(-999999, 999999)),
            asset.Output("Gold", FieldTypeSchema.Int(0, 999999)));
    }

    public Inst[] Roll(IrScope s, string sourceName, int baseGold, int min, int max, Expr run, FieldSymbol field)
    {
        var payload = s.Local($"eventFloatGoldPayload{nextLocal++}", FieldTypeSchema.Int(0, EventRandomAsset.Span - 1));
        var result = s.Local($"eventFloatGold{nextLocal++}", FieldTypeSchema.Int(0, 999999));
        return [s.SampleInt(EventRandomAsset.StreamName(sourceName), s.Const(0L), s.Const(EventRandomAsset.Span), payload),
            s.Compute(Asset, [(Payload, s.LocalRef(payload)), (Base, s.Const((long)baseGold)),
                (Min, s.Const((long)min)), (Max, s.Const((long)max))], [(Result, result)]),
            s.SetField(run, field, s.LocalRef(result))];
    }

    public static bool TryExecute(string entry, IReadOnlyList<Value> inputs, Value[] outputs)
    {
        if (entry != Entry) return false;
        var value = (float)(inputs[0].AsInt() * (1.0 / EventRandomAsset.Span)
            * (inputs[3].AsInt() - inputs[2].AsInt()) + inputs[2].AsInt());
        outputs[0] = Value.Of((long)(inputs[1].AsInt() + (decimal)value));
        return true;
    }
}
