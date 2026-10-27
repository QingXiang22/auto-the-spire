using CoreTheSpire.Engine;

namespace Differential;

internal static class NormalRandomStateChecks
{
    internal static void Run(IReadOnlyList<SessionDebugRecord> records, RandomSnapshot snapshot, NormalBoundary left, NormalBoundary right)
    {
        var states = NormalRandomStateTrace.Dsl(records, 0, snapshot);
        Require(states.Length == snapshot.TotalDraws && states.Length > 0, "all actual random draws have states");
        Require(left.Values["random.step_states"] == right.Values["random.step_states"], "actual native states match reconstructed DSL states");
        var draws = records.Where(r => r.Kind == SessionDebugKind.RandomCall).ToArray();
        var first = draws[0];
        Reject(draws[..^1], 0, snapshot);
        Reject([.. draws, first], 0, snapshot);
        Reject(draws.Reverse().ToArray(), 0, snapshot);
        Reject(draws, 1, snapshot);
        Reject([first with { Random = first.Random! with { SingleRawWord = null } }, .. draws[1..]], 0, snapshot);
        Reject([first with { Random = first.Random! with { SingleRawWord = first.Random.SingleRawWord!.Value ^ 1UL } }, .. draws[1..]], 0, snapshot);
        Reject([first with { Random = first.Random! with { After = first.Random.After + 1 } }, .. draws[1..]], 0, snapshot);
        var stream = snapshot.Streams.First(s => s.Draws > 0); var words = stream.BackendState!.ToArray(); words[0] ^= 1UL;
        var altered = stream with { S0 = words[0], BackendState = words };
        Reject(draws, 0, snapshot with { Streams = snapshot.Streams.Select(s => s.Name == stream.Name ? altered : s).ToArray() });
        Reject(draws, 0, snapshot with { AlgorithmId = "unknown" });
        RejectCanonical(states.Reverse());
        var invalid = states.ToArray(); invalid[0] = invalid[0] with { BeforeWords = ["bad", .. invalid[0].BeforeWords[1..]] };
        RejectCanonical(invalid);
        var modified = states.ToArray();
        var changedWord = (ulong.Parse(modified[0].BeforeWords[0], System.Globalization.NumberStyles.AllowHexSpecifier, System.Globalization.CultureInfo.InvariantCulture) ^ 1UL).ToString("x16", System.Globalization.CultureInfo.InvariantCulture);
        modified[0] = modified[0] with { BeforeWords = [changedWord, .. modified[0].BeforeWords[1..]] };
        var values = new SortedDictionary<string, string>(left.Values, StringComparer.Ordinal) { ["random.step_states"] = NormalPreflight.Json(modified) };
        Require(NormalRunComparison.First(0, left, left with { Values = values })?.Path == "random.step_states", "changed state must report a difference");
    }
    private static void Reject(SessionDebugRecord[] records, long dropped, RandomSnapshot snapshot)
    {
        var rejected = false;
        try { NormalRandomStateTrace.Dsl(records, dropped, snapshot); } catch (NotSupportedException) { rejected = true; }
        Require(rejected, "incomplete, corrupted or incompatible state evidence must fail");
    }
    private static void RejectCanonical(IEnumerable<NormalRandomStateStep> states)
    {
        var rejected = false;
        try { NormalRandomStateTrace.Canonical(states); } catch (NotSupportedException) { rejected = true; }
        Require(rejected, "invalid or reordered native state evidence must fail");
    }
    private static void Require(bool value, string reason)
    { if (!value) throw new InvalidOperationException("Normal random state check failed: " + reason); }
}
