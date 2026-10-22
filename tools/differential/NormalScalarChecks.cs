using CoreTheSpire.Engine;

namespace Differential;

internal static class NormalScalarChecks
{
    internal static void Run(IReadOnlyList<SessionDebugRecord> records, NormalBoundary left, NormalBoundary right)
    {
        var projection = NormalScalarTrace.Dsl(records, 0);
        Require(projection.Calls.Length == 628 && projection.Calls.Count(c => c.Kind == "binary64") == 4,
            "fixed normal path compares 624 integer and four Gaussian input calls");
        Require(projection.Unmapped.Length == 97 && projection.Unmapped.Count(c => c.Reason == "opening weighted bag normalization") == 94,
            "94 weighted bag and three HP calls remain explicitly unmapped");
        using var diagnostic = System.Text.Json.JsonDocument.Parse(NormalPreflight.Json(right.Diagnostic));
        Require(diagnostic.RootElement.GetProperty("unmapped_scalar_calls").GetArrayLength() == 97,
            "reference also retains every unmapped scalar call");
        Require(left.Values["random.scalar_calls"] == right.Values["random.scalar_calls"], "actual bilateral scalar evidence agrees");
        var calls = projection.Calls;
        foreach (var injected in new[] { calls[..^1], calls.Concat(calls.Take(1)).ToArray(),
            new[] { calls[0] with { Maximum = "changed" } }.Concat(calls.Skip(1)).ToArray(),
            new[] { calls[0] with { Result = "changed" } }.Concat(calls.Skip(1)).ToArray(),
            new[] { calls[0] with { Before = calls[0].Before + 1 } }.Concat(calls.Skip(1)).ToArray() })
        {
            var values = new SortedDictionary<string, string>(left.Values, StringComparer.Ordinal)
                { ["random.scalar_calls"] = NormalPreflight.Json(injected) };
            Require(NormalRunComparison.First(0, left, left with { Values = values })?.Path == "random.scalar_calls",
                "missing, duplicate, changed bounds, result and cursor report a scalar difference");
        }
        var random = records.Where(r => r.Kind == SessionDebugKind.RandomCall).ToArray();
        var index = Array.FindIndex(random, r => r.Random!.Operation == "int");
        Reject(random.Select((r, i) => i == index ? r with { TextTruncated = true } : r).ToArray(), 0);
        Reject(random.Select((r, i) => i == index ? r with { Random = null } : r).ToArray(), 0);
        Reject(random.Select((r, i) => i == index ? r with { Random = r.Random! with { CandidatesTruncated = true } } : r).ToArray(), 0);
        Reject(random, 1);
        Reject([.. random, random[index]], 0);
        Reject(random.Reverse().ToArray(), 0);
        var regrouped = random.GroupBy(r => r.Subject).Reverse().SelectMany(g => g).ToArray();
        Require(projection.Calls.SequenceEqual(NormalScalarTrace.Dsl(regrouped, 0).Calls),
            "independent streams may initialize in a different global order");
    }

    private static void Reject(SessionDebugRecord[] records, long dropped)
    {
        var rejected = false;
        try { NormalScalarTrace.Dsl(records, dropped); } catch (NotSupportedException) { rejected = true; }
        Require(rejected, "invalid or incomplete actual scalar diagnostics fail");
    }

    private static void Require(bool value, string reason)
    { if (!value) throw new InvalidOperationException("Normal scalar check failed: " + reason); }
}
