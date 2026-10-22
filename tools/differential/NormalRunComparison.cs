using System.Text.Json;

namespace Differential;

// Normal-run evidence is deliberately separate from the controlled mechanism fixtures.
internal sealed record NormalAction(string Kind, string[] Arguments)
{
    public string Key => NormalPreflight.Json(new { Kind, Arguments });
}
internal sealed record NormalBoundary(string Kind, SortedDictionary<string, string> Values,
    string[] MissingDimensions, object Diagnostic);
internal sealed record NormalPair(NormalBoundary Left, NormalBoundary Right);
internal sealed record NormalDifference(int Boundary, string Kind, string Path, string? Left, string? Right);
internal sealed record NormalCase(string Id, string Status, string? Reason, NormalAction[] Prefix,
    NormalPair[] Boundaries, NormalDifference? FirstDifference, bool ReturnedToMap)
{
    public bool Complete => ReturnedToMap && Status == "passed";
    public bool IndependentVerified => Complete;
}

internal static class NormalRunComparison
{
    internal static readonly string[] Dimensions = ["actions", "decisions", "facts", "public", "visits", "permanent", "piles", "random", "events"];
    internal static readonly string[] RequiredPaths = ["actions.candidates", "decisions.current", "facts.player.hp", "facts.player.max_hp", "facts.player.gold",
        "public.snapshot", "visits.phase", "visits.act", "visits.total_floor", "visits.coordinates", "permanent.deck", "permanent.relics", "permanent.potions",
        "piles.Hand", "piles.DrawPile", "piles.DiscardPile", "piles.ExhaustPile", "piles.InPlay", "piles.RemovedFromCombat", "random.trace", "events.trace"];
    internal static NormalDifference? First(int index, NormalBoundary left, NormalBoundary right)
    {
        if (left.Kind != right.Kind) return new(index, left.Kind, "boundary.kind", left.Kind, right.Kind);
        foreach (var key in left.Values.Keys.Union(right.Values.Keys).Order(StringComparer.Ordinal))
        {
            left.Values.TryGetValue(key, out var a); right.Values.TryGetValue(key, out var b);
            if (a != b) return new(index, left.Kind, key, a, b);
        }
        return null;
    }
    internal static bool Certified(NormalCase item) => item.Reason is null && item.ReturnedToMap && item.Boundaries.Length > 4
        && item.FirstDifference is null && item.Boundaries.All(p => new[] { p.Left, p.Right }.All(b =>
            b.MissingDimensions.Length == 0 && RequiredPaths.All(b.Values.ContainsKey)))
        && item.Prefix.Length >= 5 && item.Prefix[0].Kind == "neow" && item.Prefix[1].Kind == "leave-neow" && item.Prefix[2].Kind == "map"
        && item.Prefix.Any(a => a.Kind == "play") && item.Prefix[^1].Kind == "close-rewards"
        && item.Boundaries[0].Left.Kind == "neow-ready" && item.Boundaries[^1].Left.Kind == "map-return";

    public static int Run(string repository, string[] args)
    {
        if (args.Length != 2 || args[0] != "--output" || string.IsNullOrWhiteSpace(args[1]))
            throw new FormatException("Use normal-compare --output DIRECTORY.");
        var output = Path.GetFullPath(args[1]); Directory.CreateDirectory(output);
        var inputPath = Path.Combine(repository, "tools/base-spire-dev/fixtures/normal-first-combat.json");
        using var input = JsonDocument.Parse(File.ReadAllText(inputPath)); NormalPreflight.ValidateInput(input.RootElement);
        object? reference = null; string? unavailable = null;
        try { reference = Repository.VerifyReference(repository); }
        catch (Exception e) { unavailable = e.GetType().Name + ": " + e.Message; }
        var results = new List<NormalCase>();
        foreach (var path in new[] { "claim", "skip" })
        {
            var prefix = new List<NormalAction>(); var boundaries = new List<NormalPair>();
            NormalDifference? difference = null; var returned = false; string? error = null;
            try
            {
                if (unavailable is not null) throw new NotSupportedException(unavailable);
                using var left = new NormalDslRun(repository, Path.Combine(output, path + ".public.ndjson"));
                using var right = new NormalReferenceRun("777");
                left.Start("777");
                left.Step(left.Action("StartNormalRun")); right.Start();
                Capture("neow-ready");
                if (difference is null)
                {
                    Apply(new("neow", [input.RootElement.GetProperty("neow_relic").GetString()!])); Capture("neow-selected");
                }
                if (difference is null) { Apply(new("leave-neow", [])); Capture("map-ready"); }
                if (difference is null)
                {
                    Apply(left.Semantic(left.Action("ChooseMapPoint"))); Capture("combat-ready");
                }
                for (var moves = 0; difference is null && left.Phase == "combat" && moves < 100; moves++)
                {
                    var candidate = new[] { "PlayAttackFromHand", "PlayCardFromHand", "EndDevelopmentTurn" }
                        .Select(left.Find).FirstOrDefault(a => a is not null)
                        ?? throw new NotSupportedException("No registered public combat action.");
                    Apply(left.Semantic(candidate)); Capture("combat-action:" + moves);
                }
                if (difference is null && left.Phase != "rewards") throw new InvalidOperationException("Missing real victory within action limit.");
                if (difference is null)
                {
                    if (path == "claim") { Apply(new("claim-gold", [])); Capture("gold-claimed"); }
                    Apply(path == "claim" ? left.Semantic(left.Action("ClaimDevelopmentCardReward")) : new("skip-card", []));
                    Capture("card-resolved");
                    if (left.Find(path == "claim" ? "ClaimDevelopmentPotionReward" : "SkipDevelopmentPotionReward") is { } potion)
                    { Apply(left.Semantic(potion)); Capture("potion-resolved"); }
                    Apply(new("close-rewards", [])); Capture("map-return");
                    returned = left.Phase == "map" && right.Phase == "map";
                }
                void Apply(NormalAction action) { prefix.Add(action); left.Apply(action); right.Apply(action); }
                void Capture(string kind)
                {
                    var a = left.Capture(kind); var b = right.Capture(kind); boundaries.Add(new(a, b));
                    difference ??= First(boundaries.Count - 1, a, b);
                }
            }
            catch (Exception e) { error = e.GetType().Name + ": " + e.Message; }
            var item = new NormalCase("m01-" + path, difference is null ? "blocked_compare" : "diverged", error,
                prefix.ToArray(), boundaries.ToArray(), difference, returned);
            if (error is null && Certified(item)) item = item with { Status = "passed" };
            results.Add(item);
            Console.WriteLine($"{item.Id}: {item.Status}; paired boundaries={boundaries.Count}; first={difference?.Kind}/{difference?.Path}; {error}");
        }
        var passed = results.Count == 2 && results.All(r => r.IndependentVerified);
        File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new
        {
            schema = "normal-run/paired@1", status = passed ? "passed" : results.Any(r => r.FirstDifference is not null) ? "diverged" : "blocked_compare",
            complete = passed, independent_verified = passed, expected_case_ids = new[] { "m01-claim", "m01-skip" },
            executed = results.Count(r => r.Boundaries.Length > 0), input = input.RootElement,
            required_dimensions = Dimensions, input_sha256 = NormalPreflight.Hash(inputPath), reference_identity = reference,
            adapter_sha256 = NormalPreflight.Hash(typeof(NormalRunComparison).Assembly.Location),
            production_manifest_sha256 = NormalPreflight.Hash(Path.Combine(repository, "mod-the-spire/base-spire/package.json")),
            cases = results,
            reproduction = "dotnet run --project tools/differential -c Release -- normal-compare --output DIRECTORY"
        }, new JsonSerializerOptions { WriteIndented = true, IncludeFields = true, PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower }));
        return passed ? 0 : results.Any(r => r.Reason is not null || r.FirstDifference is null) ? 3 : 1;
    }
}
