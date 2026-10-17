using System.Globalization;
using BaseSpire.Slice.Rooms;
using BaseSpire.Slice.SourceMap;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice;

/// <summary>T05：一条地图边（源 `MapPoint.Children` 的实体化形式），`Parent`/`Child` 都是 cell 下标。</summary>
public readonly record struct SourceActMapEdge(int Parent, int Child);

/// <summary>
/// T05：把一串 53 位 payload（`raw64 &gt;&gt; 11`）重放成整幕地图的结果。
/// <see cref="Done"/> 为 false 表示这串词不足以让源算法跑完（前缀耗尽的判定，见 `SourceActMapGenerator.TryGenerate`）。
/// </summary>
public sealed class SourceActMapReplay
{
    public required bool Done { get; init; }

    /// <summary>源 `mapRng.NextGaussianInt(7, 1, 6, 7)`，仅 `Done` 时有意义。</summary>
    public int Rest { get; init; }

    /// <summary>源 `MapPointTypeCounts.StandardRandomUnknownCount(mapRng)`（`NextGaussianInt(12, 1, 10, 14)`）。</summary>
    public int Unknown { get; init; }

    /// <summary>重放这串词实际消费的 payload 数（rest+unknown 两笔高斯 + 地图阶段）。</summary>
    public int WordsUsed { get; init; }

    /// <summary>起始点（源 `StartingMapPoint`，列 3 行 0）在 <see cref="Cells"/> 中的下标。</summary>
    public int StartCell { get; init; }

    public IReadOnlyList<SourceActMapCell> Cells { get; init; } = Array.Empty<SourceActMapCell>();

    public IReadOnlyList<SourceActMapEdge> Edges { get; init; } = Array.Empty<SourceActMapEdge>();
}

/// <summary>
/// T05 地图重放的纯计算。**不接触 Session/State/RNG**：输入是"已经抽好的 payload 串"，输出是
/// 网格形状；随机数在 IR 侧逐笔抽、逐笔重放（见 `MapRoomWiring`），因此这里既不多抽也不少抽一个字。
///
/// 调用顺序逐字照抄 `scratch/SourceMap/README-port.md:278-285`：
/// <code>
/// var rng = new SourceMapRandom(payloads);                          // payloads = raw64 >> 11
/// int rests   = rng.NextGaussianInt(7, 1, 6, 7);                    // Overgrowth：rest 先
/// int unknown = MapPointTypeCounts.StandardRandomUnknownCount(rng);  // unknown 后
/// var counts  = new MapPointTypeCounts(unknownCount: unknown, restCount: rests);
/// var result  = SourceActMapGenerator.TryGenerate(payloads, rng.DrawsUsed, 15, counts, false, true, ...);
/// </code>
/// 「第一个能让生成跑完的前缀」= 源实际消费的字数：IR 每次只多抽一个 payload 再问 <see cref="Replay"/>，
/// 所以消费量逐字一致（`Done` 单调：更短的前缀必定跑不完）。
/// </summary>
public static class SourceActMapCompute
{
    public const string GenerateAssetName = "SourceActMapGenerate";
    public const string CellAssetName = "SourceActMapCell";
    public const string EdgeAssetName = "SourceActMapEdge";

    /// <summary>后端按 `asset.Entry` 分派（引擎每会话只装一个 backend）。</summary>
    public const string GenerateEntry = "act-map-generate";
    public const string CellEntry = "act-map-cell";
    public const string EdgeEntry = "act-map-edge";

    /// <summary>`SampleInt(stream, 0, 1 &lt;&lt; 53)` 取到的就是源 `NextDouble()` 的 53 位值。</summary>
    public const long Span = 1L << 53;

    /// <summary>Act 1（Overgrowth）单人的 `BaseNumberOfRooms =&gt; 15`（`_mapLength`/RowCount = 16）。</summary>
    public const int ActRooms = 15;

    /// <summary>重放预算：源生成路径的 payload 上限（简报给的 512 字上界）。</summary>
    public const int WordBudget = 512;

    public const string SourcePath = "mod-the-spire/base-spire/BaseSpire/Maps/Generation";
    public const string SourceHash =
        "sts2/src/Core/Map/StandardActMap.cs+MapPostProcessing.cs+MapPathPruning.cs+Overgrowth.cs@act-map-replay-v1";

    /// <summary>IR 侧词表 blob 的一个词（与 `MapRoomWiring` 的 `Concat` 逐字对应）。</summary>
    public static string AppendWord(string blob, long payload) =>
        blob + ";" + payload.ToString(CultureInfo.InvariantCulture);

    /// <summary>解析 IR 侧词表 blob（`;w0;w1;…`，空串 = 空词表）。</summary>
    public static IReadOnlyList<ulong> ParseWords(string blob)
    {
        if (string.IsNullOrEmpty(blob))
        {
            return Array.Empty<ulong>();
        }

        var parts = blob.Split(';', StringSplitOptions.RemoveEmptyEntries);
        var words = new ulong[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            words[i] = ulong.Parse(parts[i], CultureInfo.InvariantCulture);
        }

        return words;
    }

    public static SourceActMapReplay Replay(string blob, int rooms) => Replay(ParseWords(blob), rooms);

    public static SourceActMapReplay Replay(IReadOnlyList<ulong> words, int rooms, int actIndex = 0)
    {
        var rng = new SourceMapRandom(words);
        int rest;
        int unknown;
        try
        {
            rest = actIndex switch { 0 => rng.NextGaussianInt(7, 1, 6, 7), 1 => rng.NextGaussianInt(6, 1, 6, 7),
                2 => rng.NextInt(5, 7), _ => throw new ArgumentOutOfRangeException(nameof(actIndex)) };
            unknown = MapPointTypeCounts.StandardRandomUnknownCount(rng) - (actIndex > 0 ? 1 : 0);
        }
        catch (SourceMapWordsExhaustedException)
        {
            return new SourceActMapReplay { Done = false, WordsUsed = rng.DrawsUsed };
        }

        var counts = new MapPointTypeCounts(unknownCount: unknown, restCount: rest);
        var mapStart = rng.DrawsUsed;
        if (!SourceActMapGenerator.TryGenerate(
                words, mapStart, rooms, counts, hasSecondBoss: false, enablePruning: true,
                out var result, out var consumed))
        {
            return new SourceActMapReplay
            {
                Done = false,
                Rest = rest,
                Unknown = unknown,
                WordsUsed = mapStart + consumed,
            };
        }

        var cells = result.Cells;
        return new SourceActMapReplay
        {
            Done = true,
            Rest = rest,
            Unknown = unknown,
            WordsUsed = mapStart + consumed,
            StartCell = FindStartCell(cells),
            Cells = System.Collections.Immutable.ImmutableArray.CreateRange(cells),
            Edges = System.Collections.Immutable.ImmutableArray.CreateRange(BuildEdges(cells)),
        };
    }

    public static int FindStartCell(IReadOnlyList<SourceActMapCell> cells)
    {
        for (var i = 0; i < cells.Count; i++)
        {
            if (cells[i].Col == 3 && cells[i].Row == 0)
            {
                return i;
            }
        }

        return 0;
    }

    /// <summary>
    /// 边 = 每个 cell 的 `ChildColsMask` 指向的下一行 cell（逐列）。刻意**不**用 `MapPoint.Children` 的
    /// `HashSet` 枚举序（移植件 §C.1 的 HIGH 分歧点），改用列掩码 + 列优先下标字典，跨进程完全确定。
    /// </summary>
    public static IReadOnlyList<SourceActMapEdge> BuildEdges(IReadOnlyList<SourceActMapCell> cells)
    {
        var index = new Dictionary<(int Col, int Row), int>(cells.Count);
        for (var i = 0; i < cells.Count; i++)
        {
            index[(cells[i].Col, cells[i].Row)] = i;
        }

        var edges = new List<SourceActMapEdge>();
        for (var i = 0; i < cells.Count; i++)
        {
            var mask = cells[i].ChildColsMask;
            for (var col = 0; col < 7; col++)
            {
                if ((mask & (1 << col)) == 0)
                {
                    continue;
                }

                if (index.TryGetValue((col, cells[i].Row + 1), out var child))
                {
                    edges.Add(new SourceActMapEdge(i, child));
                }
            }
        }

        return edges;
    }
}

/// <summary>
/// T05 会话用的组合计算后端：`SourceWeightedPickBackend` 的逐字转发 + 三个地图资产 + 五个奖励资产。
/// 引擎每会话只装一个 backend（`ComputeInst` 执行期要求 `backend.Id == asset.BackendId`），
/// 所以地图/奖励资产复用同一个 `BackendId`，在这里按 `asset.Entry` 分派。
/// </summary>
public sealed class BaseSpireComputeBackend : IComputeBackend
{
    public const string BackendId = SourceWeightedPickBackend.BackendId;

    public static BaseSpireComputeBackend Instance { get; } = new BaseSpireComputeBackend();

    public BaseSpireComputeBackend()
    {
    }

    private readonly object replayLock = new();
    private string? completedBlob;
    private int completedRooms;
    private int completedAct;
    private SourceActMapReplay? completedReplay;

    private SourceActMapReplay Replay(string blob, int rooms, int act = 0)
    {
        lock (replayLock)
        {
            if (completedReplay is not null && completedRooms == rooms && completedAct == act && completedBlob == blob)
                return completedReplay;
            var replay = SourceActMapCompute.Replay(SourceActMapCompute.ParseWords(blob), rooms, act);
            if (replay.Done)
            {
                completedBlob = blob;
                completedRooms = rooms;
                completedAct = act;
                completedReplay = replay;
            }
            return replay;
        }
    }

    public string Id => BackendId;

    public void Execute(ComputeAssetDefinition asset, IReadOnlyList<Value> inputs, Value[] outputs)
    {
        switch (asset.Entry)
        {
            case "PotionRarity":
                var potionRoll = RewardCompute.RollOf(inputs[0].AsInt());
                outputs[0] = Value.Of((long)(potionRoll <= 0.1f ? RewardRarity.Rare
                    : potionRoll <= 0.1f + 0.25f ? RewardRarity.Uncommon : RewardRarity.Common));
                return;
            case "TreasureRelicRarity":
                outputs[0] = Value.Of(TreasureRelicRules.RarityForPayload(inputs[0].AsInt()));
                return;
            case SourceActMapCompute.GenerateEntry:
                ExecuteGenerate(inputs, outputs);
                return;
            case SourceActMapCompute.CellEntry:
                ExecuteCell(inputs, outputs);
                return;
            case SourceActMapCompute.EdgeEntry:
                ExecuteEdge(inputs, outputs);
                return;
            default:
                // ENV-T09.2：商店的纯计算条目（稀有度/数量/选卡/价格）与地图资产共用同一个 `BackendId`，
                // 因此按 `asset.Entry` 在这里分派；它们不接触 RNG / Session / State。
                if (ShopCompute.TryExecute(asset.Entry, inputs, outputs))
                {
                    return;
                }

                if (RewardCompute.TryExecute(asset.Entry, inputs, outputs))
                {
                    return;
                }

                // ENV-T09-unknown-room-modules-20261012：问号房判定（源 `UnknownMapPointOdds.Roll`）
                // 与事件级随机换算（源 `Rng.NextInt`）两条纯计算条目。
                if (UnknownMapPointOdds.TryExecute(asset.Entry, inputs, outputs))
                {
                    return;
                }

                if (EventRandomAsset.TryExecute(asset.Entry, inputs, outputs))
                {
                    return;
                }

                if (EventFloatGoldAsset.TryExecute(asset.Entry, inputs, outputs))
                {
                    return;
                }

                SourceWeightedPickBackend.Instance.Execute(asset, inputs, outputs);
                return;
        }
    }

    private void ExecuteGenerate(IReadOnlyList<Value> inputs, Value[] outputs)
    {
        var replay = Replay(inputs[0].AsString(), (int)inputs[1].AsInt(), inputs.Count > 2 ? (int)inputs[2].AsInt() : 0);
        outputs[0] = Value.Of(replay.Done);
        outputs[1] = Value.Of((long)replay.Rest);
        outputs[2] = Value.Of((long)replay.Unknown);
        outputs[3] = Value.Of((long)replay.Cells.Count);
        outputs[4] = Value.Of((long)replay.Edges.Count);
        outputs[5] = Value.Of((long)replay.WordsUsed);
        outputs[6] = Value.Of((long)replay.StartCell);
    }

    private void ExecuteCell(IReadOnlyList<Value> inputs, Value[] outputs)
    {
        var cells = Replay(inputs[0].AsString(), inputs.Count > 2 ? (int)inputs[2].AsInt() : SourceActMapCompute.ActRooms,
            inputs.Count > 3 ? (int)inputs[3].AsInt() : 0).Cells;
        var index = (int)inputs[1].AsInt();
        if (index < 0 || index >= cells.Count)
        {
            outputs[0] = Value.Of(false);
            outputs[1] = Value.Of(0L);
            outputs[2] = Value.Of(0L);
            outputs[3] = Value.OfEnum(0);
            return;
        }

        var cell = cells[index];
        outputs[0] = Value.Of(true);
        outputs[1] = Value.Of((long)cell.Col);
        outputs[2] = Value.Of((long)cell.Row);
        outputs[3] = Value.OfEnum(cell.Type);
    }

    private void ExecuteEdge(IReadOnlyList<Value> inputs, Value[] outputs)
    {
        var edges = Replay(inputs[0].AsString(), inputs.Count > 2 ? (int)inputs[2].AsInt() : SourceActMapCompute.ActRooms,
            inputs.Count > 3 ? (int)inputs[3].AsInt() : 0).Edges;
        var index = (int)inputs[1].AsInt();
        if (index < 0 || index >= edges.Count)
        {
            outputs[0] = Value.Of(false);
            outputs[1] = Value.Of(0L);
            outputs[2] = Value.Of(0L);
            return;
        }

        var edge = edges[index];
        outputs[0] = Value.Of(true);
        outputs[1] = Value.Of((long)edge.Parent);
        outputs[2] = Value.Of((long)edge.Child);
    }
}
