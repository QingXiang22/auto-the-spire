using System.Text.Json;

namespace Differential;

internal static class ComparisonChecks
{
    public static int Run(string repository)
    {
        Repository.VerifyReference(repository);
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(repository, "tools/differential/suites/regression.json")));
        var fixture = StrikeFixture.Read(document.RootElement.GetProperty("cases").EnumerateArray()
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
        foreach (var dimension in StrikeFixture.Dimensions)
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
        Console.WriteLine("Comparator checks passed: independent execution, missing data, terminal, RNG and four first-difference dimensions.");
        return 0;
    }

    private static void Assert(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("Comparator check failed: " + name);
    }
}
