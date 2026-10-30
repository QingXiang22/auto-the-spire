#if REFERENCE_SIM
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using Sts2Sim.Core.Map;

namespace Differential;

// A scratch graph, never the live run: consume recorded indices, not RNG calls.
// Native pure qualification, segment discovery/pruning and layout methods operate
// on that graph. The reconstructed result must equal the independently built map.
internal static class NormalReferenceMapCandidates
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    internal static NormalMapCandidates Capture(StandardActMap actual, NormalInitializationDraw[] draws)
    {
        var map = (StandardActMap)RuntimeHelpers.GetUninitializedObject(typeof(StandardActMap));
        var rows = actual.GetRowCount(); var grid = new MapPoint?[actual.GetColumnCount(), rows];
        var counts = (MapPointTypeCounts)typeof(StandardActMap).GetField("_pointTypeCounts", Private)!.GetValue(actual)!;
        var start = new MapPoint(3, 0); var boss = new MapPoint(3, rows); var starts = new HashSet<MapPoint>();
        void Set(string field, object value) => typeof(StandardActMap).GetField(field, Private)!.SetValue(map, value);
        Set("<Grid>k__BackingField", grid); Set("_mapLength", rows); Set("_pointTypeCounts", counts);
        Set("<StartingMapPoint>k__BackingField", start); Set("<BossMapPoint>k__BackingField", boss);
        typeof(ActMap).GetField("startMapPoints")!.SetValue(map, starts);
        T Native<T>(string name, params object[] args) => (T)typeof(StandardActMap).GetMethod(name, Private)!.Invoke(map, args)!;
        T Prune<T>(string name, params object[] args) => (T)typeof(MapPathPruning).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, args)!;
        var calls = new List<NormalMapCall>(); var gaussians = new List<NormalMapGaussian>(); var cursor = 0; var assignment = 0;
        NormalInitializationDraw Next(string operation, int arity)
        {
            if (cursor == draws.Length) throw NormalMapCandidatesTrace.Missing("missing native map draw");
            var d = draws[cursor++];
            if (d.Before != cursor - 1 || d.After != cursor || d.Operation != operation || d.Arity != arity)
                throw NormalMapCandidatesTrace.Missing("native map operation or cursor");
            return d;
        }
        int Integer(string kind, string context, string[] candidates, int arity)
        {
            var d = Next("NextInt", arity); var selected = int.Parse(d.Result, CultureInfo.InvariantCulture);
            if (d.Minimum != 0 || d.Maximum != candidates.Length || selected < 0 || selected >= candidates.Length)
                throw NormalMapCandidatesTrace.Missing("native map range or index");
            calls.Add(new(kind, context, d.Before, d.After, selected, candidates)); return selected;
        }
        void Shuffle<T>(string kind, string context, List<T> list, Func<T, string> id)
        {
            for (var i = list.Count - 1; i > 0; i--)
            {
                var selected = Integer(kind, context, list.Take(i + 1).Select(id).ToArray(), 1);
                (list[i], list[selected]) = (list[selected], list[i]);
            }
        }
        int Gaussian(int mean, int min, int max)
        {
            var before = cursor; int result;
            do
            {
                var values = new double[2];
                for (var i = 0; i < 2; i++)
                {
                    var d = Next("NextDouble", 0);
                    if (d.Minimum != 0 || d.Maximum != 0) throw NormalMapCandidatesTrace.Missing("native Gaussian scalar parameters");
                    values[i] = double.Parse(d.Result, CultureInfo.InvariantCulture);
                    calls.Add(new("gaussian", GaussianContext(mean, 1, min, max), d.Before, d.After, checked((long)(values[i] * 9007199254740992d)), []));
                }
                result = checked((int)Math.Round(mean + Math.Sqrt(-2d * Math.Log(1d - values[0])) * Math.Sin(Math.PI * 2d * (1d - values[1]))));
            } while (result < min || result > max);
            gaussians.Add(new(mean, 1, min, max, result, before, cursor)); return result;
        }
        if (Gaussian(7, 6, 7) != counts.NumOfRests || Gaussian(12, 10, 14) != counts.NumOfUnknowns)
            throw NormalMapCandidatesTrace.Missing("native Gaussian results disagree with actual map counts");
        for (var path = 0; path < 7; path++)
        {
            MapPoint point;
            do { point = Native<MapPoint>("GetOrCreatePoint", Integer("start", path == 1 ? "distinct" : "any", Enumerable.Range(0, 7).Select(i => i.ToString(CultureInfo.InvariantCulture)).ToArray(), 2), 1); }
            while (path == 1 && starts.Contains(point));
            starts.Add(point);
            while (point.coord.row < rows - 1)
            {
                var offsets = new List<int> { -1, 0, 1 }; Shuffle("offsets", Id(point), offsets, i => i.ToString(CultureInfo.InvariantCulture));
                var col = offsets.Select(o => Math.Clamp(point.coord.col + o, 0, 6)).First(c => !Native<bool>("HasInvalidCrossover", point, c));
                var next = Native<MapPoint>("GetOrCreatePoint", col, point.coord.row + 1); point.AddChildPoint(next); point = next;
            }
        }
        foreach (var p in map.GetPointsInRow(rows - 1)) p.AddChildPoint(boss);
        foreach (var p in map.GetPointsInRow(1)) start.AddChildPoint(p);
        foreach (var (row, type) in new[] { (rows - 1, MapPointType.RestSite), (rows - 7, MapPointType.Treasure), (1, MapPointType.Monster) })
            foreach (var p in map.GetPointsInRow(row)) { p.PointType = type; p.CanBeModified = false; }
        var queue = new Queue<MapPointType>(Enumerable.Repeat(MapPointType.RestSite, counts.NumOfRests)
            .Concat(Enumerable.Repeat(MapPointType.Shop, counts.NumOfShops)).Concat(Enumerable.Repeat(MapPointType.Elite, counts.NumOfElites))
            .Concat(Enumerable.Repeat(MapPointType.Unknown, counts.NumOfUnknowns)));
        for (var round = 0; round < 3 && queue.Count > 0; round++)
        {
            var unassigned = map.GetAllMapPoints().Where(p => p.PointType == MapPointType.Unassigned).Order().ToList();
            Shuffle("assign", (assignment++).ToString(CultureInfo.InvariantCulture), unassigned, Id);
            foreach (var p in unassigned) { if (queue.Count == 0) break; p.PointType = Native<MapPointType>("GetNextValidPointType", queue, p); }
        }
        foreach (var p in map.GetAllMapPoints().Where(p => p.PointType == MapPointType.Unassigned)) p.PointType = MapPointType.Monster;
        start.PointType = MapPointType.Ancient; boss.PointType = MapPointType.Boss;
        for (var repairRound = 0; repairRound < 3; repairRound++)
        {
            bool changed;
            do
            {
                changed = false;
                foreach (var matching in MapPathPruning.FindMatchingSegments(start))
                {
                    var key = Prune<string>("GenerateSegmentKey", (object)matching[0]);
                    Shuffle("prune", key, matching, segment => string.Join('>', segment.Select(Id)));
                    if (Prune<int>("PruneAllButLast", grid, starts, matching) != 0 || Prune<bool>("BreakAParentChildRelationshipInAnySegment", matching))
                    { changed = true; break; }
                }
            } while (changed);
            var repaired = false;
            foreach (var (type, target) in new[] { (MapPointType.Shop, counts.NumOfShops), (MapPointType.Elite, counts.NumOfElites),
                (MapPointType.RestSite, counts.NumOfRests), (MapPointType.Unknown, counts.NumOfUnknowns) })
            {
                var deficit = target - map.GetAllMapPoints().Count(p => p.PointType == type); if (deficit <= 0) continue;
                var candidates = map.GetAllMapPoints().Where(p => p.PointType == MapPointType.Monster && p.CanBeModified).Order().ToList();
                Shuffle("repair", ((int)type).ToString(CultureInfo.InvariantCulture), candidates, Id);
                foreach (var p in candidates)
                    if (deficit > 0 && map.IsValidPointType(type, p)) { p.PointType = type; deficit--; repaired = true; }
            }
            if (!repaired) break;
        }
        grid = MapPostProcessing.CenterGrid(grid); grid = MapPostProcessing.SpreadAdjacentMapPoints(grid); grid = MapPostProcessing.StraightenPaths(grid);
        Set("<Grid>k__BackingField", grid);
        if (cursor != draws.Length || !Signature(map).SequenceEqual(Signature(actual)))
            throw NormalMapCandidatesTrace.Missing("native map reconstruction differs from all actual calls or real map");
        return NormalMapCandidatesTrace.Validate(new(calls.ToArray(), gaussians.ToArray()));
    }
    internal static string GaussianContext(int mean, int deviation, int min, int max) => $"{mean}/{deviation}/{min}/{max}";
    private static string Id(MapPoint p) => NormalMapCandidatesTrace.Coordinate(p.coord.col, p.coord.row);
    private static string[] Signature(StandardActMap map) => map.GetAllMapPoints().Append(map.StartingMapPoint).Append(map.BossMapPoint)
        .Select(p => $"{Id(p)}:{(int)p.PointType}:{p.CanBeModified}:{string.Join(';', p.Children.Select(Id).Order(StringComparer.Ordinal))}")
        .Order(StringComparer.Ordinal).ToArray();
}
#endif
