using CoreTheSpire.Engine;
using CoreTheSpire;

namespace Differential;

internal static class NormalScalarChecks
{
    internal static void Run(IReadOnlyList<SessionDebugRecord> records, NormalBoundary left, NormalBoundary right)
    {
        var projection = NormalScalarTrace.Dsl(records, 0);
        Require(projection.Calls.Length == 725 && projection.Calls.Count(c => c.Kind == "binary64") == 98,
            "fixed normal path compares 627 integer, four Gaussian input and 94 weighted unit calls");
        Require(projection.Unmapped.Length == 0, "all scalar calls have bilateral representations");
        using var diagnostic = System.Text.Json.JsonDocument.Parse(NormalPreflight.Json(right.Diagnostic));
        Require(diagnostic.RootElement.GetProperty("unmapped_scalar_calls").GetArrayLength() == 0,
            "reference maps every actual scalar call");
        var hp = projection.Calls.Where(c => c.Stream == "base-spire:run:niche").ToArray();
        Require(hp.Select(c => (c.Minimum, c.Maximum, c.Result)).SequenceEqual(new[] { ("0", "5", "3"), ("0", "3", "2"), ("0", "5", "4") }),
            "three real SlimesWeak HP draws compare actual available-count intervals and offsets");
        var origins = System.Text.Json.JsonSerializer.Deserialize<NormalHpOrigin[]>(diagnostic.RootElement.GetProperty("random").GetProperty("hp_initialization").GetRawText())!;
        Require(origins.Length == 3 && origins.Select(o => o.Hp).SequenceEqual(new long[] { 14, 28, 11 }), "native pre-setup origins remain in raw diagnostics");
        CheckHpRejections(origins, hp);
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
        var hpRecord = random.First(r => r.Subject == "base-spire:run:niche");
        Reject(random.Select(r => r == hpRecord ? r with { Source = new DslSourceLocation("unknown-hp-source.spire", 1, 1) } : r).ToArray(), 0);
        Reject([.. random, random[index]], 0);
        Reject(random.Reverse().ToArray(), 0);
        var weightedIndex = Array.FindIndex(random, r => r.Random!.Operation == "pick" && r.Subject == "base-spire:run:up_front"
            && r.Snapshot!.Entities.Any(e => e.Type == "base-spire:OpeningListItem"));
        var weighted = random[weightedIndex];
        foreach (var invalid in new[] { weighted.Random! with { SingleRawWord = null },
            weighted.Random! with { SingleRawWord = ulong.MaxValue, Result = "0" },
            weighted.Random! with { TotalAfter = weighted.Random.TotalAfter + 1 } })
            Reject(records.Select(r => r == weighted ? r with { Random = invalid } : r).ToArray(), 0);
        var changedWord = weighted.Random! with { SingleRawWord = weighted.Random.SingleRawWord!.Value ^ 2048UL };
        var changedCalls = NormalScalarTrace.Dsl(records.Select(r => r == weighted ? r with { Random = changedWord } : r).ToArray(), 0).Calls;
        Require(!projection.Calls.SequenceEqual(changedCalls), "same discrete index with a changed actual unit sample still differs");
        var regrouped = random.GroupBy(r => r.Subject).Reverse().SelectMany(g => g).ToArray();
        Require(projection.Calls.SequenceEqual(NormalScalarTrace.Dsl(regrouped, 0).Calls),
            "independent streams may initialize in a different global order");
    }

    private static void CheckHpRejections(NormalHpOrigin[] origins, NormalScalarCall[] hp)
    {
        var uniqueOrigins = new[] { 18L, 19L, 17L, 18L }.Select(h => new NormalHpOrigin("controlled-source-hp", 17, 20, h)).ToArray();
        var uniqueCalls = new[] { (0L, 3L, 1L), (0L, 2L, 1L), (0L, 1L, 0L), (17L, 20L, 18L) }
            .Select((c, i) => NormalScalarTrace.Integer("base-spire:run:niche", i, i + 1, c.Item1, c.Item2, c.Item3)).ToArray();
        Require(NormalHpTrace.Normalize(uniqueOrigins, uniqueCalls).SequenceEqual(uniqueCalls),
            "source uniqueness and exhausted-range draws retain their actual parameters and results");
        var offsets = hp;
        foreach (var invalid in new[] { origins[..^1], origins.Reverse().ToArray(),
            new[] { origins[0] with { Minimum = origins[0].Minimum + 1, Maximum = origins[0].Maximum + 1 } }.Concat(origins.Skip(1)).ToArray(),
            new[] { origins[0] with { Hp = origins[0].Hp - 1 } }.Concat(origins.Skip(1)).ToArray(),
            new[] { origins[0], origins[1] with { Minimum = 11, Maximum = 16, Hp = 15 }, origins[2] } })
        {
            var rejected = false;
            try { NormalHpTrace.Normalize(invalid, offsets); } catch (NotSupportedException) { rejected = true; }
            Require(rejected, "missing/reordered origins, moved interval, changed initial HP and uniqueness exclusions fail");
        }
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
