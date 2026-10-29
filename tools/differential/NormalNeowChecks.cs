using System.Text.Json;
using CoreTheSpire.Engine;

namespace Differential;

internal static class NormalNeowChecks
{
    internal static void Run(IReadOnlyList<SessionDebugRecord> records, NormalBoundary left, NormalBoundary right, Func<InstanceId, bool> exists)
    {
        var actual = JsonSerializer.Deserialize<NormalNeowCandidates>(left.Values["random.neow_candidates"])!;
        Require(left.Values["random.neow_candidates"] == right.Values["random.neow_candidates"], "independent Neow pools, draws and options agree");
        var parsed = NormalNeowTrace.Dsl(records, 0, actual.Options);
        Require(parsed.Sources.All(s => !exists(new InstanceId(s.Reference))), "the full compared pool really was destroyed after generating options");
        Require(NormalPreflight.Json(parsed.Candidates) == left.Values["random.neow_candidates"], "cached evidence agrees with original history");
        Require(actual.Calls.Length == 18 && actual.Calls.Count(c => c.Kind == "pair") == 3 && actual.Calls.Count(c => c.Kind == "shuffle") == 14
            && actual.BasePositives.Length == 13 && actual.Positives.Length == 15, "the original seed retains all actual Neow phases");
        Require(records.Count(r => r.Kind == SessionDebugKind.RandomCall && r.Subject == NormalNeowTrace.Stream) == actual.Calls.Length,
            "no unclassified or omitted actual Neow consumption");
        var golden = parsed.Sources.Single(s => s.Id == "base-spire:golden-pearl");
        Require(!golden.Eligible && !actual.Positives.Contains(golden.Id), "the actual pool excludes GoldenPearl for CursedPearl");
        using var raw = JsonDocument.Parse(NormalPreflight.Json(right.Diagnostic));
        Require(raw.RootElement.GetProperty("native_neow_sources").EnumerateArray().Any(e => e.GetProperty("Id").GetString() == "base-spire:massive-scroll"
            && !e.GetProperty("Eligible").GetBoolean()), "the native raw source retains the actual multiplayer exclusion");
        void RejectHistory(IReadOnlyList<SessionDebugRecord> history, long dropped = 0) => Reject(() => NormalNeowTrace.Dsl(history, dropped, actual.Options));
        var offer = records.First(r => r.Kind == SessionDebugKind.ProcedureEnter && r.Subject == NormalNeowTrace.OptionProcedure);
        RejectHistory(records.Select(r => r == offer ? r with { Snapshot = null } : r).ToArray());
        RejectHistory(records.Select(r => r == offer ? r with { Snapshot = r.Snapshot! with { Relations = [] } } : r).ToArray());
        var absent = offer.Snapshot! with { Entities = offer.Snapshot!.Entities.Where(e => e.Instance.Value != golden.Reference).ToArray() };
        RejectHistory(records.Select(r => r == offer ? r with { Snapshot = absent } : r).ToArray());
        SessionDebugRecord ChangePool(SessionDebugRecord r, int reference, string name, string value) => r.Kind == SessionDebugKind.ProcedureEnter && r.Subject == NormalNeowTrace.OptionProcedure
            ? r with { Snapshot = r.Snapshot! with { Entities = r.Snapshot!.Entities.Select(e => e.Instance.Value != reference ? e : e with
                { Fields = e.Fields.Select(f => f.Name != name ? f : f with { Value = f.Value with { Scalar = value } }).ToArray() }).ToArray() } } : r;
        RejectHistory(records.Select(r => ChangePool(r, golden.Reference, "Eligible", "true")).ToArray());
        var unselected = parsed.Sources.Where(s => s.Group == "positive" && s.Eligible && s.Position >= 2).Take(2).ToArray();
        RejectHistory(records.Select(r => ChangePool(ChangePool(r, unselected[0].Reference, "Position", unselected[1].Position.ToString()),
            unselected[1].Reference, "Position", unselected[0].Position.ToString())).ToArray());
        foreach (var subject in new[] { "base-spire:AddNeowPoolEntry", "base-spire:AddNeowPair", "base-spire:NeowSourceBool", "base-spire:GenerateSourceNeowOptions" })
        {
            var record = records.First(r => r.Kind == SessionDebugKind.ProcedureEnter && r.Subject == subject);
            RejectHistory(records.Where(r => r != record).ToArray());
            RejectHistory(records.Select(r => r == record ? r with { TextTruncated = true } : r).ToArray());
        }
        var draw = records.First(r => r.Kind == SessionDebugKind.RandomCall && r.Subject == NormalNeowTrace.Stream);
        RejectHistory(records.Where(r => r != draw).ToArray());
        RejectHistory(records.SelectMany(r => r == draw ? new[] { r, r } : new[] { r }).ToArray());
        RejectHistory(records.Select(r => r == draw ? r with { Random = r.Random! with { CandidatesTruncated = true } } : r).ToArray());
        RejectHistory(records, 1);
        Reject(() => NormalNeowTrace.Dsl(records, 0, actual.Options.Reverse().ToArray()));
        var changed = actual.Calls.ToArray(); changed[0] = changed[0] with { Candidates = changed[0].Candidates.Reverse().ToArray() };
        foreach (var altered in new[] { actual with { Calls = changed }, actual with { Calls = actual.Calls[..^1] },
            actual with { BasePositives = actual.BasePositives[..^1] }, actual with { Positives = actual.Positives.Reverse().ToArray() } })
        {
            var values = new SortedDictionary<string, string>(left.Values, StringComparer.Ordinal) { ["random.neow_candidates"] = NormalPreflight.Json(altered) };
            Require(NormalRunComparison.First(0, left, left with { Values = values })?.Path == "random.neow_candidates", "missing or changed Neow candidates must differ");
        }
        Console.WriteLine("Normal Neow candidates: 18 actual calls, full transient eligibility/order, and three independently verified options.");
    }
    private static void Reject(Action action)
    {
        var rejected = false;
        try { action(); } catch (NotSupportedException) { rejected = true; }
        Require(rejected, "incomplete or contradictory Neow evidence must fail");
    }
    private static void Require(bool condition, string reason)
    { if (!condition) throw new InvalidOperationException("Normal Neow check failed: " + reason); }
}
