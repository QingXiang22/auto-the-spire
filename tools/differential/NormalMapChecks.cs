using System.Text.Json;
using CoreTheSpire.Engine;

namespace Differential;

internal static class NormalMapChecks
{
    internal static void Run(IReadOnlyList<SessionDebugRecord> records, NormalBoundary left, NormalBoundary right)
    {
        var value = JsonSerializer.Deserialize<NormalMapCandidates>(left.Values["random.map_candidates"])!;
        Require(left.Values["random.map_candidates"] == right.Values["random.map_candidates"], "independent map sources, contexts, indices and Gaussian results agree");
        Require(value.Calls.Length == 264 && value.Calls.Count(c => c.Kind == "gaussian") == 4 && value.Calls.Count(c => c.Kind == "start") == 7
            && value.Calls.Count(c => c.Kind == "offsets") == 196 && value.Calls.Count(c => c.Kind == "assign") == 51 && value.Calls.Count(c => c.Kind == "prune") == 6,
            "the original map retains all 264 actual calls and source phases");
        var rests = value.Gaussians[0].Result; var unknowns = value.Gaussians[1].Result;
        Require(NormalPreflight.Json(NormalDslMapCandidates.Capture(records, 0, rests, unknowns)) == left.Values["random.map_candidates"], "cached map evidence matches complete original history");
        void Bad(IReadOnlyList<SessionDebugRecord> history, long dropped = 0) => Reject(() => NormalDslMapCandidates.Capture(history, dropped, rests, unknowns));
        var random = records.First(r => r.Kind == SessionDebugKind.RandomCall && r.Subject == NormalMapCandidatesTrace.Stream);
        Bad(records.Where(r => r != random).ToArray());
        Bad(records.SelectMany(r => r == random ? new[] { r, r } : new[] { r }).ToArray());
        Bad(records.Select(r => r == random ? r with { Random = r.Random! with { Before = 3 } } : r).ToArray());
        Bad(records, 1);
        foreach (var name in new[] { "SourceGaussianInt", "SelectSourceMapStart", "NextSourceMapPoint", "AddMapWork", "ShuffleMapWork", "DestroyMapWork", "PruneSourceMapSegment" })
        {
            var entry = records.First(r => r.Kind == SessionDebugKind.ProcedureEnter && r.Subject == "base-spire:" + name);
            Bad(records.Select(r => r == entry ? r with { Snapshot = null } : r).ToArray());
            Bad(records.Select(r => r == entry ? r with { TextTruncated = true } : r).ToArray());
        }
        var shuffle = records.First(r => r.Kind == SessionDebugKind.ProcedureEnter && r.Subject == "base-spire:ShuffleMapWork");
        Bad(records.Select(r => r == shuffle ? r with { Snapshot = r.Snapshot! with { Relations = [] } } : r).ToArray());
        var segment = records.First(r => r.Kind == SessionDebugKind.ProcedureEnter && r.Subject == "base-spire:PruneSourceMapSegment");
        Bad(records.Select(r => r == segment ? r with { Snapshot = r.Snapshot! with { Relations = [] } } : r).ToArray());
        var entity = segment.Snapshot!.Entities.First(e => e.Type == "base-spire:MapSegment");
        Bad(records.Select(r => r == segment ? r with { Snapshot = r.Snapshot! with
        { Entities = r.Snapshot!.Entities.Where(e => e.Instance != entity.Instance).ToArray() } } : r).ToArray());
        Bad(records.Select(r => r == segment ? r with { Snapshot = r.Snapshot! with { Entities = r.Snapshot!.Entities.Select(e => e.Instance != entity.Instance ? e : e with
            { Fields = e.Fields.Select(f => f.Name != "Position" ? f : f with { Value = f.Value with { Scalar = "99" } }).ToArray() }).ToArray() } } : r).ToArray());
        Reject(() => NormalDslMapCandidates.Capture(records, 0, rests + 1, unknowns));
        var reversed = value.Calls.ToArray(); var pick = Array.FindIndex(reversed, c => c.Kind == "assign" && c.Candidates.Length > 3);
        // Swap two unselected candidates while retaining the actual index and selected node.
        var call = reversed[pick]; var candidates = call.Candidates.ToArray(); var unselected = Enumerable.Range(0, candidates.Length).Where(i => i != call.Selected).Take(2).ToArray();
        (candidates[unselected[0]], candidates[unselected[1]]) = (candidates[unselected[1]], candidates[unselected[0]]);
        reversed[pick] = call with { Candidates = candidates };
        foreach (var changed in new[] { value with { Calls = reversed }, value with { Calls = value.Calls[..^1] },
            value with { Gaussians = [value.Gaussians[0] with { Result = rests + 1 }, value.Gaussians[1]] } })
        {
            var values = new SortedDictionary<string, string>(left.Values, StringComparer.Ordinal) { ["random.map_candidates"] = NormalPreflight.Json(changed) };
            Require(NormalRunComparison.First(0, left, left with { Values = values })?.Path == "random.map_candidates", "changed map candidate or phase evidence must differ");
        }
        Console.WriteLine("Normal map candidates: all 264 actual calls, Gaussian attempts, ordered work lists and complete pruning segments agree.");
    }
    private static void Reject(Action action)
    { var rejected = false; try { action(); } catch (NotSupportedException) { rejected = true; } Require(rejected, "missing or contradictory map evidence must fail"); }
    private static void Require(bool value, string reason)
    { if (!value) throw new InvalidOperationException("Normal map check failed: " + reason); }
}
