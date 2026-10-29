using CoreTheSpire.Engine;

namespace Differential;

internal static class NormalInitializationChecks
{
    internal static void Run(IReadOnlyList<SessionDebugRecord> records, NormalInitializationSources sources, NormalBoundary left, NormalBoundary right)
    {
        NormalInitializationCandidates Parse(IReadOnlyList<SessionDebugRecord> history, long dropped = 0) =>
            NormalInitializationTrace.Dsl(history, dropped, sources.Definitions, sources.Bags, sources.Plans);
        var actual = Parse(records);
        Require(left.Values["random.initialization_candidates"] == right.Values["random.initialization_candidates"], "independent initialization sources and actual draws agree");
        Require(NormalPreflight.Json(actual) == left.Values["random.initialization_candidates"], "cached initialization evidence agrees with its original history");
        Require(actual.Allocations.Length == 2 && actual.Picks.Length == 6
            && actual.Shuffles.Select(s => s.Key).Distinct().Count() == sources.Bags.Count(b => b.Ids.Length > 1) + 3,
            "every source relic bag and three event queues retain shuffle candidates; both allocations and all six direct picks are present");
        var raw = records.Where(r => r.Kind == SessionDebugKind.RandomCall && r.Subject == NormalInitializationTrace.Stream).ToArray();
        Require(raw.Length == actual.Shuffles.Length + actual.Allocations.Length + actual.Picks.Length + NormalWeightedTrace.Dsl(records, 0).Length,
            "all real UpFront calls belong to a compared initialization operation");
        Require(actual.Picks.Any(p => p.Room == "ancient" && p.Candidates.Length == 1), "the native single-candidate Ancient still consumes a real draw");
        foreach (var kind in new[] { "base-spire:AddOpeningItem", "base-spire:PopulateOpeningRelicBag", "base-spire:ShuffleOpeningList", "base-spire:InitializeSourceOpeningPools" })
        {
            var record = records.First(r => r.Kind == SessionDebugKind.ProcedureEnter && r.Subject == kind);
            Reject(() => Parse(records.Where(r => r != record).ToArray()));
            Reject(() => Parse(records.Select(r => r == record ? r with { TextTruncated = true } : r).ToArray()));
        }
        var first = raw[0];
        Reject(() => Parse(records.Where(r => r != first).ToArray()));
        Reject(() => Parse(records.SelectMany(r => r == first ? new[] { r, r } : new[] { r }).ToArray()));
        Reject(() => Parse(records.Select(r => r == first ? r with { Random = first.Random! with { Before = 1, After = 2 } } : r).ToArray()));
        Reject(() => Parse(records, 1));
        var direct = records.First(r => r.Subject == NormalInitializationTrace.Stream && r.Random is { Operation: "pick", Candidates: { Count: > 0 } }
            && r.Snapshot!.Entities.Single(e => e.Instance == r.Random.Candidates[0]).Type == "base-spire:OpeningDefinition");
        var root = direct.Random!.Candidates![0];
        foreach (var field in new[] { "Act", "Category", "Room", "AssignedAct" })
        {
            var snapshot = direct.Snapshot! with { Entities = direct.Snapshot!.Entities.Select(e => e.Instance != root ? e : e with
                { Fields = e.Fields.Select(f => f.Name != field ? f : f with { Value = f.Value with { Scalar = "corrupted" } }).ToArray() }).ToArray() };
            Reject(() => Parse(records.Select(r => r == direct ? r with { Snapshot = snapshot } : r).ToArray()));
        }
        var missingRoot = direct.Snapshot! with { Entities = direct.Snapshot!.Entities.Where(e => e.Instance != root).ToArray() };
        Reject(() => Parse(records.Select(r => r == direct ? r with { Snapshot = missingRoot } : r).ToArray()));
        var bag = sources.Bags.First(b => b.Ids.Length > 1);
        Reject(() => NormalInitializationTrace.Dsl(records, 0, sources.Definitions,
            sources.Bags.Select(b => b == bag ? b with { Ids = b.Ids.Reverse().ToArray() } : b).ToArray(), sources.Plans));
        var shared = sources.Definitions.First(d => d.Category == "shared-ancient");
        Reject(() => NormalInitializationTrace.Dsl(records, 0,
            sources.Definitions.Select(d => d == shared ? d with { AssignedAct = 99 } : d).ToArray(), sources.Bags, sources.Plans));
        var eventPlan = sources.Plans.First(p => p.Room == "event");
        Reject(() => NormalInitializationTrace.Dsl(records, 0, sources.Definitions, sources.Bags,
            sources.Plans.Select(p => p == eventPlan ? p with { Id = "base-spire:wrong-event" } : p).ToArray()));
        Reject(() => NormalInitializationTrace.ValidatePicks(actual.Picks[..^1], sources.Plans));
        Reject(() => NormalInitializationTrace.ValidatePicks(actual.Picks.Reverse(), sources.Plans));
        Reject(() => NormalInitializationTrace.ValidatePicks(actual.Picks,
            sources.Plans.Select(p => p.Room == "Boss" && p.Act == 0 ? p with { Id = "base-spire:wrong-boss" } : p).ToArray()));
        var changedShuffles = actual.Shuffles.ToArray(); changedShuffles[0] = changedShuffles[0] with { Candidates = changedShuffles[0].Candidates.Reverse().ToArray() };
        var changedAllocations = actual.Allocations.ToArray(); changedAllocations[0] = changedAllocations[0] with { Take = changedAllocations[0].Take + 1 };
        var changedPicks = actual.Picks.ToArray(); changedPicks[0] = changedPicks[0] with { Candidates = changedPicks[0].Candidates.Reverse().ToArray() };
        foreach (var altered in new[] { actual with { Shuffles = changedShuffles }, actual with { Allocations = changedAllocations },
            actual with { Picks = changedPicks }, actual with { Picks = actual.Picks[..^1] } })
        {
            var values = new SortedDictionary<string, string>(left.Values, StringComparer.Ordinal) { ["random.initialization_candidates"] = NormalPreflight.Json(altered) };
            Require(NormalRunComparison.First(0, left, left with { Values = values })?.Path == "random.initialization_candidates",
                "changed source order, assignment, direct candidates and missing picks must report a difference");
        }
        Console.WriteLine($"Normal initialization candidates: {actual.Shuffles.Length} shuffle draws, two shared Ancient allocations, six direct picks; all {raw.Length} UpFront calls covered with weighted encounters.");
    }
    private static void Reject(Action action)
    {
        var rejected = false;
        try { action(); } catch (NotSupportedException) { rejected = true; }
        Require(rejected, "incomplete or contradictory initialization evidence must fail");
    }
    private static void Require(bool condition, string reason)
    { if (!condition) throw new InvalidOperationException("Normal initialization check failed: " + reason); }
}
