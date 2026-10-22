using System.Text.Json;

namespace Differential;

internal static class ComparisonChecks
{
    public static int Run(string repository)
    {
        Repository.VerifyReference(repository);
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(repository, "tools/differential/suites/regression.json")));
        var fixture = CombatFixture.Read(document.RootElement.GetProperty("cases").EnumerateArray()
            .Single(c => c.GetProperty("id").GetString() == "strike-normal"));
        var left = DslBackend.Run(repository, fixture); var right = ReferenceBackend.Run(fixture);
        Assert(Comparison.Compare(fixture, left, right).Status == "passed", "real independent execution");
        Assert(Comparison.Compare(fixture, left, left).Status == "blocked_compare", "same backend cannot verify itself");
        Assert(Comparison.Compare(fixture, left, right with { Dimensions = ["actions", "decisions", "facts"] }).Status == "missing_dimension", "missing required dimension");
        Assert(Comparison.Compare(fixture, left, right with { Boundaries = [] }).Status == "missing_dimension", "zero execution");
        Assert(Comparison.Compare(fixture, left, right with { Terminal = "victory" }).Status == "wrong_terminal", "unexpected terminal");
        Assert(Comparison.Compare(fixture, left, right with { ScriptRandomCount = 1 }).Status == "unsupported_rng_profile", "unexpected reference RNG");
        // Dropping the same field on both sides must fail instead of agreeing on incomplete records.
        BackendRecord Drop(BackendRecord record) => record with { Boundaries = record.Boundaries.Select(b =>
        {
            var facts = new SortedDictionary<string, string>(b.Facts, StringComparer.Ordinal); facts.Remove("pile.hand.order");
            return b with { Facts = facts };
        }).ToArray() };
        Assert(Comparison.Compare(fixture, Drop(left), Drop(right)).Status == "missing_dimension", "both sides missing pile order");
        foreach (var dimension in CombatFixture.Dimensions)
        {
            var end = right.Boundaries[1];
            var facts = new SortedDictionary<string, string>(end.Facts, StringComparer.Ordinal);
            var changed = dimension switch
            {
                "actions" => end with { Actions = [new("base-spire:PlayCard", ["card:hand:0"], 1)] },
                "decisions" => end with { Decisions = [end.Decisions.Single() with { Chosen = "enemy:1" }] },
                _ => ChangeFact(),
            };
            Boundary ChangeFact()
            {
                facts[dimension == "facts" ? "enemy.base-spire:calcified-cultist.hp" : "pile.hand.order"] = "different";
                return end with { Facts = facts };
            }
            var result = Comparison.Compare(fixture, left, right with { Boundaries = [right.Boundaries[0], changed] });
            Assert(result.Status == "diverged" && result.FirstDifference is { BoundaryIndex: 1, LastConsistentBoundary: "0:start" }
                && result.FirstDifference.Dimension == dimension, dimension + " first difference");
        }
        CheckRandom(repository, document.RootElement);
        NormalPreflightChecks.Run();
        Console.WriteLine("Comparator checks passed: independent execution, missing data, terminal, RNG and four first-difference dimensions.");
        return 0;
    }

    private static void CheckRandom(string repository, JsonElement suite)
    {
        var fixture = CombatFixture.Read(suite.GetProperty("cases").EnumerateArray().Single(c => c.GetProperty("id").GetString() == "random-reshuffle-unsupported"));
        var left = DslBackend.Run(repository, fixture); var right = ReferenceBackend.Run(fixture);
        var actual = Comparison.Compare(fixture, left, right);
        Assert(actual.Status == "diverged" && actual.FirstDifference is not null && !actual.IndependentVerified,
            "original random case retains real independent differences");
        foreach (var backend in new[] { left, right })
        {
            Assert(backend.ScriptRandomCount == 9 && backend.Boundaries[^1].Random!.Calls.Length == 9, "real script shuffled nine times");
            Assert(backend.Boundaries.All(b => RandomComparison.Complete(b.Random)), "all stable boundaries have complete traces");
            Assert(backend.Boundaries[^1].Random!.Calls.All(c => c.Candidates is { Length: > 0 }), "actual ordered candidate extraction");
        }
        var trace = left.Boundaries[^1].Random!;
        Assert(trace.Calls.All(c => !string.IsNullOrWhiteSpace(c.Source)), "real DSL source locations");
        // The following are comparator fault injections, not independent gameplay evidence.
        Assert(RandomComparison.First(trace, trace) is null, "equal diagnostic records");
        RandomTrace Change(RandomDraw call) => trace with { Calls = [call, .. trace.Calls.Skip(1)] };
        var first = trace.Calls[0];
        var reordered = Change(first with { Candidates = first.Candidates!.Reverse().ToArray() });
        Assert(RandomComparison.First(trace, reordered)!.Path.Contains("candidates["), "same count, wrong candidate order");
        var numeric = trace with { Calls = trace.Calls.Select(c => c with { Operation = "int", Candidates = null }).ToArray() };
        var parameters = numeric with { Calls = [numeric.Calls[0] with { Maximum = "11" }, .. numeric.Calls.Skip(1)] };
        Assert(RandomComparison.First(numeric, parameters)!.Path == "random[0].maximum", "same consumption, different parameters");
        Assert(RandomComparison.First(trace, Change(first with { Result = first.Result == "0" ? "1" : "0" }))!.Path == "random[0].result", "result first difference");
        Assert(!RandomComparison.Complete(trace with { Calls = trace.Calls.Skip(1).ToArray() }), "missing prefix");
        Assert(!RandomComparison.Complete(trace with { Calls = [] }), "zero extracted records despite measured consumption");
        Assert(!RandomComparison.Complete(trace with { Positions = trace.Positions.Skip(1).ToArray() }), "missing stream snapshot");
        Assert(!RandomComparison.Complete(trace with { Dropped = 1 }), "capacity overflow");
        Assert(!RandomComparison.Complete(trace with { Truncated = true }), "candidate truncation");
        var empty = left.Boundaries[0].Random!;
        var wrongPosition = empty with { Positions = [empty.Positions[0] with { Start = 1, Cursor = 1 }, .. empty.Positions.Skip(1)] };
        Assert(RandomComparison.First(empty, wrongPosition)!.Path.EndsWith(".start", StringComparison.Ordinal), "stream position first difference");
        var missing = left with { Boundaries = left.Boundaries.Select(b => b with { Random = b.Random! with { Dropped = 1 } }).ToArray() };
        Assert(Comparison.Compare(fixture, missing, right).Status == "missing_dimension", "missing trace cannot pass streams");
        Assert(Comparison.Compare(fixture, left, right with { ScriptRandomCount = 10 }).Status == "missing_dimension", "unregistered reference draw cannot vanish");
#if REFERENCE_SIM
        // Independent locked_rng_oracle.py golden values for the unchanged original game_seed.
        Assert(new Sts2Sim.Core.Runs.RunRngSet(fixture.GameSeed).Seed == 10353177550043027975UL
            && ReferenceRandomTrace.SourceSeed(fixture.GameSeed, "shuffle") == 13438661683528184091UL, "independent native-source derivation vector");
#endif
        Console.WriteLine("Random diagnostic checks passed: real original reshuffle, ordered candidates, parameters, result, position and strict trace-loss failures.");
    }

    private static void Assert(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("Comparator check failed: " + name);
    }
}
