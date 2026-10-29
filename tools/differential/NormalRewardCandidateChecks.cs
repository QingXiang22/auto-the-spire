using System.Text.Json;
using CoreTheSpire.Engine;

namespace Differential;

internal static class NormalRewardCandidateChecks
{
    internal static void Run(IReadOnlyList<SessionDebugRecord> records, NormalBoundary left, NormalBoundary right)
    {
        var calls = NormalRewardCandidateTrace.Dsl(records, 0);
        Require(calls.Length == 4 && calls[0].Kind == "potion" && calls.Skip(1).All(c => c.Kind == "card"),
            "the actual normal reward has one potion and three card picks");
        Require(left.Values["random.reward_candidates"] == right.Values["random.reward_candidates"], "independent ordered reward candidates agree");
        using var diagnostic = JsonDocument.Parse(NormalPreflight.Json(right.Diagnostic));
        var sources = diagnostic.RootElement.GetProperty("native_reward_candidate_sources");
        Require(sources.GetProperty("cards").GetArrayLength() == 85 && sources.GetProperty("potions").GetArrayLength() == 48,
            "raw source copies retain the full native unlocked pools");
        var reversed = calls.ToArray(); reversed[1] = reversed[1] with { Candidates = reversed[1].Candidates.Reverse().ToArray() };
        foreach (var altered in new[] { calls[..^1], calls.Skip(1).ToArray(), calls.Reverse().ToArray(), reversed,
            new[] { calls[0] with { Selected = (calls[0].Selected + 1) % calls[0].Candidates.Length } }.Concat(calls.Skip(1)).ToArray() })
        {
            var values = new SortedDictionary<string, string>(left.Values, StringComparer.Ordinal)
                { ["random.reward_candidates"] = NormalPreflight.Json(altered) };
            Require(NormalRunComparison.First(0, left, left with { Values = values })?.Path == "random.reward_candidates",
                "missing or reordered calls, wrong candidate order and wrong result must differ");
        }
        foreach (var first in new[] { calls[0] with { Selected = -1 }, calls[0] with { Selected = calls[0].Candidates.Length },
            calls[0] with { After = calls[0].Before + 2 }, calls[0] with { Candidates = [calls[0].Candidates[0], calls[0].Candidates[0]] } })
            Reject(() => NormalRewardCandidateTrace.Canonical([first, .. calls.Skip(1)]));
        var repeated = calls.ToArray();
        repeated[2] = repeated[2] with { Candidates = [calls[1].Candidates[calls[1].Selected], .. calls[2].Candidates] };
        Reject(() => NormalRewardCandidateTrace.Canonical(repeated));
        Reject(() => NormalRewardCandidateTrace.Canonical(calls.Reverse()));
        var pick = records.First(r => r.Subject == NormalRewardCandidateTrace.Stream && r.Random?.Operation == "pick");
        foreach (var invalid in new[] { pick with { TextTruncated = true }, pick with { Snapshot = null },
            pick with { Random = pick.Random! with { CandidatesTruncated = true } },
            pick with { Random = pick.Random! with { Candidates = [] } },
            pick with { Random = pick.Random! with { Maximum = "999" } } })
            Reject(() => NormalRewardCandidateTrace.Dsl(records.Select(r => r == pick ? invalid : r).ToArray(), 0));
        Reject(() => NormalRewardCandidateTrace.Dsl(records, 1));
    }
    private static void Reject(Action action)
    {
        var rejected = false;
        try { action(); } catch (NotSupportedException) { rejected = true; }
        Require(rejected, "incomplete or invalid reward candidate evidence must fail");
    }
    private static void Require(bool condition, string reason)
    { if (!condition) throw new InvalidOperationException("Normal reward candidate check failed: " + reason); }
}
