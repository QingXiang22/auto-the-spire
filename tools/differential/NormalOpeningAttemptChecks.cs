using CoreTheSpire.Engine;

namespace Differential;

internal static class NormalOpeningAttemptChecks
{
    internal static void Run(IReadOnlyList<SessionDebugRecord> records, NormalBoundary boundary)
    {
        var actual = NormalOpeningAttempts.Dsl(records, 0);
        // Source room counts are 15/14/13 normal encounters, plus 15 elites per act.
        Require(actual.Length == 94 && actual.Count(a => a.Accepted) == 87 && actual.Count(a => !a.Accepted) == 7,
            "all real attempts include 87 source queue picks and seven rejected draws");
        Require(actual.Any(a => a.Previous is not null) && actual.All(a => a.UsePredicate == a.CompatibleCandidates.Any(v => v)),
            "actual previous context and complete compatibility prechecks are retained");
        foreach (var changed in new[] { actual[..^1], actual.Concat(actual.Take(1)).ToArray(),
            new[] { actual[0] with { Previous = "base-spire:wrong-previous" } }.Concat(actual.Skip(1)).ToArray(),
            new[] { actual[0] with { Accepted = !actual[0].Accepted } }.Concat(actual.Skip(1)).ToArray(),
            new[] { actual[0] with { CompatibleCandidates = actual[0].CompatibleCandidates.Select(v => !v).ToArray() } }.Concat(actual.Skip(1)).ToArray() })
        {
            var values = new SortedDictionary<string, string>(boundary.Values, StringComparer.Ordinal)
                { ["random.weighted_attempts"] = NormalPreflight.Json(changed) };
            Require(NormalRunComparison.First(0, boundary, boundary with { Values = values })?.Path == "random.weighted_attempts",
                "missing/repeated attempt, wrong previous context, predicate and outcome produce a first difference");
        }
        foreach (var subject in new[] { "base-spire:PickOpeningEncounter", "base-spire:OpeningEncountersCompatible" })
            foreach (var kind in new[] { SessionDebugKind.ProcedureEnter, SessionDebugKind.ProcedureExit })
            {
                var record = records.First(r => r.Subject == subject && r.Kind == kind);
                Reject(records.Where(r => r != record).ToArray(), "missing " + subject + " " + kind);
                Reject(records.Select(r => r != record ? r : r with { TextTruncated = true }).ToArray(), "truncated " + subject);
                Reject(records.Select(r => r != record ? r : r with { Detail = "corrupted" }).ToArray(), "corrupted " + subject);
            }
        Reject(records, "dropped records", 1);
        var calls = NormalWeightedTrace.Dsl(records, 0);
        var queues = System.Text.Json.JsonSerializer.Deserialize<NormalOpeningQueueEntry[]>(boundary.Values["random.opening_queues"])!;
        Require(queues.Length == 87, "all actual retained normal and elite queues are compared");
        foreach (var changed in new[] { queues[..^1], queues.Concat(queues.Take(1)).ToArray(),
            new[] { queues[0] with { Position = 999 } }.Concat(queues.Skip(1)).ToArray(),
            new[] { queues[0] with { Id = "base-spire:wrong-commit" } }.Concat(queues.Skip(1)).ToArray() })
        {
            var rejected = false;
            try { NormalOpeningQueues.Validate(calls, actual, changed); } catch (NotSupportedException) { rejected = true; }
            Require(rejected, "missing, repeated, reordered or wrong queue commits fail");
        }
        var cleanupRejected = false;
        try { NormalOpeningQueues.ValidateCleanup(records, _ => true); } catch (NotSupportedException) { cleanupRejected = true; }
        Require(cleanupRejected, "retaining a temporary opening bag fails the cleanup invariant");
        void Reject(IReadOnlyList<SessionDebugRecord> changed, string name, long dropped = 0)
        {
            var rejected = false;
            try { NormalOpeningAttempts.Dsl(changed, dropped); } catch (NotSupportedException) { rejected = true; }
            Require(rejected, "opening attempt comparison rejects " + name);
        }
    }
    private static void Require(bool condition, string name)
    { if (!condition) throw new InvalidOperationException("Normal opening attempt check failed: " + name); }
}
