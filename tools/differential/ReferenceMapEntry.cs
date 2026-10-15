#if REFERENCE_SIM
using System.Text.Json;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Map;

namespace Differential;

/// <summary>
/// **参考侧地图生成入口**（ENV-T04 地图独立对拍子包）。
///
/// 用参考自己的 `Rng`/`MegaRandom`/`StandardActMap`（含 `MapPathPruning` 与 `MapPostProcessing`）
/// 真正生成幕 1 地图，不调用 base 的 `SourceMap`/`SourceActMapCompute`，不用 base 记录或 golden 答案。
///
/// 唯一被替换的是**命名流种子派生**：源 `StandardActMap.CreateFor`（只读
/// `sts2/src/Core/Map/StandardActMap.cs:111-114`）用 `new Rng(runState.Rng.Seed, $"act_{n}_map")`，
/// 这里用工具的 `RngSourceCompat` 按同一条推导算出同一个 uint 种子。地图算法/抽样/剪枝/后处理一律不动。
/// </summary>
internal static class ReferenceMapEntry
{
    public const string Name = "reference:sts2-sim";
    public const string Version = "env-t04-map-entry@1";

    private const string MapStreamType = "act_1_map";

    /// <summary>对照协议上界的抽数字数，与 base 侧 `SourceActMapCompute.WordBudget` 同值（不是被比较的形状）。</summary>
    private const int WordBudget = BaseSpire.Slice.SourceActMapCompute.WordBudget;

    public static CaseRecord Run(FixtureCase fixture)
    {
        var notes = new List<string>();
        var boundaries = new List<BoundaryRecord>();

        if (fixture.Script.Count > 0 || string.IsNullOrWhiteSpace(fixture.GameSeed))
        {
            return new CaseRecord(
                fixture.Id, Name, Version, fixture.RngProfile, "invalid_fixture",
                "参考地图入口只接受无脚本、且带字符串 game_seed 的 case", false, notes, boundaries);
        }

        var gameSeed = fixture.GameSeed!;
        Generated generated;
        try
        {
            generated = Build(gameSeed);
        }
        catch (Exception failure)
        {
            return new CaseRecord(
                fixture.Id, Name, Version, fixture.RngProfile, "faulted",
                $"{failure.GetType().Name}: {failure.Message}", false, notes, boundaries);
        }

        notes.Add("参考侧用自己的 Rng/MegaRandom/StandardActMap（含剪枝与后处理）真正生成幕 1 网格；"
            + "不调用 base 的 SourceMap/SourceActMapCompute，不使用 base 记录或 golden 答案。");
        notes.Add($"唯一被替换的是命名流种子派生：按只读 sts2/src/Core/Map/StandardActMap.cs:111-114 的 "
            + $"`new Rng(runState.Rng.Seed, \"{MapStreamType}\")`，用 RngSourceCompat.RunSeed + StreamSeed 得到同一个 uint 种子；"
            + "参考的地图算法、抽样、剪枝、后处理一律不动。");
        notes.Add("本入口不建 RunState：RunState 构造期会用参考自己的 ulong 派生再生成一张地图，那张图不在比较范围内；"
            + "这里只生成被比较的那一张，避免混入未声明的随机消耗。");
        notes.Add("只比较地图机制：事实只有地图节点/边/计数，流只有 act_1_map 一条。"
            + "base 正常 Start 还会消费 UpFront/洗牌等流，那些不属于本 case（由 rng-initial-shuffle 等覆盖）。");
        notes.Add("抽数单位是**底层 64 位字数**：参考 `Rng.Counter` 数的是 API 调用（一次高斯只 +1），"
            + "与 base 的 payload 数不是同一个单位；这里用工具自己的 xoshiro 模型把参考状态按字数对齐后求得。");
        notes.Add($"参考抽样 API 形态：{generated.Ops.Count} 次调用（"
            + $"{string.Join("，", generated.Ops.GroupBy(op => op).Select(group => $"{group.Key}×{group.Count()}"))}）；"
            + "该计数是 API 调用数，不是底层字数。");
        notes.Add("`map.second_boss=none`：本 case 用 hasSecondBoss:false，与 base 的幕 1 重放参数一致。");
        notes.Add("未验证范围：base 侧选中未实现房型仍具名 Fail；本 case 只停在 Start 后的地图相位边界，"
            + "不声称多房间/奖励/结算跑通。");

        boundaries.Add(new BoundaryRecord(
            0,
            "start",
            "参考独立地图生成（Overgrowth + act_1_map 命名流 + StandardActMap）",
            [],
            [],
            generated.Facts,
            [generated.Stream],
            null));

        return new CaseRecord(fixture.Id, Name, Version, fixture.RngProfile, "ok", null, false, notes, boundaries)
        {
            SessionStatus = "MapGenerated",
            ReferenceIdentity = Identity(gameSeed, generated),
        };
    }

    private sealed record Generated(
        uint MapSeed,
        long Words,
        IReadOnlyList<FactSnapshot> Facts,
        StreamSnapshot Stream,
        IReadOnlyList<string> Ops);

    private static Generated Build(string gameSeed)
    {
        var runSeed = RngSourceCompat.RunSeed(gameSeed);
        var mapSeed = RngSourceCompat.StreamSeed(runSeed, MapStreamType);
        var mapRng = new Sts2Sim.Core.Random.Rng((ulong)mapSeed);

        // 只在局部观察参考自己的抽样 API 形态，不消费随机数；观察完立刻恢复全局钩子。
        var ops = new List<string>();
        var previous = Sts2Sim.Core.Random.RngDiagnostics.DrawObserver;
        Sts2Sim.Core.Random.RngDiagnostics.DrawObserver = (rng, op, _, _, _) =>
        {
            if (ReferenceEquals(rng, mapRng))
            {
                ops.Add(op);
            }
        };

        StandardActMap map;
        try
        {
            var act = new Overgrowth();
            var counts = act.GetMapPointTypes(mapRng, new AscensionManager(AscensionLevel.None));
            map = new StandardActMap(mapRng, act.BaseNumberOfRooms, counts, hasSecondBoss: false, enablePruning: true);
        }
        finally
        {
            Sts2Sim.Core.Random.RngDiagnostics.DrawObserver = previous;
        }

        var state = mapRng.ToSerializable();
        var words = RngSourceCompat.WordsForState(
            mapSeed, state.state0, state.state1, state.state2, state.state3, WordBudget);
        if (words < 0)
        {
            throw new InvalidOperationException(
                $"参考地图流状态在 {WordBudget} 个字内没有对齐到源兼容模型，给不出与 base 同单位的抽数");
        }

        var stream = new StreamSnapshot(
            MapSnapshot.StreamName,
            words,
            RngSourceCompat.FingerprintText(state.state0, state.state1, state.state2, state.state3));

        var secondBoss = map.SecondBossMapPoint is { } point
            ? new MapPointFact(point.coord.col, point.coord.row, (int)point.PointType)
            : (MapPointFact?)null;

        return new Generated(
            mapSeed,
            words,
            MapFacts.Build(Points(map), Edges(map), words, secondBoss),
            stream,
            ops);
    }

    private static List<MapPointFact> Points(ActMap map)
    {
        // GetAllMapPoints() 只枚举网格内的点，起点/BOSS/二王点是单独字段，必须自己补上。
        var points = map.GetAllMapPoints()
            .Select(Special)
            .ToList();
        points.Add(Special(map.StartingMapPoint));
        points.Add(Special(map.BossMapPoint));
        if (map.SecondBossMapPoint is { } secondBoss)
        {
            points.Add(Special(secondBoss));
        }

        return points;
    }

    private static List<MapEdgeFact> Edges(ActMap map)
    {
        var all = new List<MapPoint>(map.GetAllMapPoints()) { map.StartingMapPoint, map.BossMapPoint };
        if (map.SecondBossMapPoint is { } secondBoss)
        {
            all.Add(secondBoss);
        }

        return
        [
            .. all.SelectMany(parent => parent.Children.Select(child =>
                new MapEdgeFact(parent.coord.col, parent.coord.row, child.coord.col, child.coord.row))),
        ];
    }

    private static MapPointFact Special(MapPoint point) =>
        new(point.coord.col, point.coord.row, (int)point.PointType);

    private static ReferenceIdentity Identity(string gameSeed, Generated generated)
    {
        var (head, headSource) = Repo.GitHead("reference/sts2-sim");
        var profile = Repo.DigestFiles(["tools/differential/RngSourceCompat.cs"]);

        return new ReferenceIdentity(
            Name,
            RngSourceCompat.ProfileId,
            "显式：地图 case 直接用源派生种子构造参考自己的 Rng（new Rng((ulong)mapSeed)），不经 RunRngSet.MockRng",
            "仅 act_1_map 一条命名流的种子派生（源 hash32 + uint 加法回绕）",
            gameSeed,
            RngSourceCompat.RunSeed(gameSeed),
            [new ReferenceStreamSeed(MapStreamType, MapSnapshot.StreamName, generated.MapSeed)],
            RngSourceCompat.Replaces,
            RngSourceCompat.NotReplaced,
            profile.Count > 0 && profile[0].Exists ? profile[0].Sha256 : null,
            head,
            headSource,
            SourceFiles());
    }

    private static IReadOnlyList<ReferenceFileDigest> SourceFiles()
    {
        const string relative = ".audit/dsh-supervisor/review-2026-10-08-2153/map-source-inputs.json";
        var full = Path.Combine(Repo.Root(), relative.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(full))
        {
            return [];
        }

        var digests = new List<ReferenceFileDigest>();
        using var document = JsonDocument.Parse(File.ReadAllText(full));
        foreach (var entry in document.RootElement.EnumerateObject())
        {
            var registered = entry.Value.GetString() ?? string.Empty;
            var current = Repo.DigestFiles([entry.Name]);
            var actual = current.Count > 0 && current[0].Exists ? current[0].Sha256 : "<missing>";
            // 登记文件（.audit/dsh-supervisor/review-2026-10-08-2153/map-source-inputs.json）写的是裸十六进制，
            // 而 Repo.DigestFiles 的摘要有 `sha256:` 前缀：按十六进制比对，否则同一条摘要会被自己的前缀
            // 判成「不一致」，让源依据证据出现假警报。
            var actualHex = actual.StartsWith("sha256:", StringComparison.Ordinal)
                ? actual["sha256:".Length..]
                : actual;
            var registeredHex = registered.StartsWith("sha256:", StringComparison.Ordinal)
                ? registered["sha256:".Length..]
                : registered;
            digests.Add(new ReferenceFileDigest(
                entry.Name,
                string.Equals(actualHex, registeredHex, StringComparison.OrdinalIgnoreCase)
                    ? actual
                    : $"{actual}（登记={registered}，不一致）"));
        }

        return digests;
    }
}
#endif
