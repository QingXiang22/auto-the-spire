using System.Text.Json;

namespace Differential;

internal sealed record StrikeFixture(string Id, string GameSeed, int Seed, int PlayerHp, int Energy, int[] EnemyHp,
    string Card, string Target, string[] RequiredDimensions, string ExpectedTerminal)
{
    public static readonly string[] Dimensions = ["actions", "decisions", "facts", "pile-order"];

    public static StrikeFixture Read(JsonElement item)
    {
        var dimensions = item.GetProperty("required_dimensions").EnumerateArray().Select(v => v.GetString()!).ToArray();
        if (!dimensions.Order().SequenceEqual(Dimensions.Order()))
            throw new FormatException("strike-normal requires every original comparison dimension.");
        if (item.GetProperty("kind").GetString() != "mechanism"
            || item.GetProperty("base_source").GetString() != "controlled-slice-v1"
            || item.GetProperty("rng_profile").GetString() != "controlled-no-random@1")
            throw new FormatException("Unsupported strike-normal initialization or RNG profile.");
        var setup = item.GetProperty("setup");
        var hand = setup.GetProperty("hand").EnumerateArray().ToArray();
        var script = item.GetProperty("script").EnumerateArray().ToArray();
        if (hand.Length != 1 || script.Length != 1 || hand[0].GetProperty("card").GetString() != "base-spire:strike-ironclad"
            || hand[0].EnumerateObject().Any(p => p.Name != "card")
            || setup.EnumerateObject().Any(p => p.Name is not ("hand" or "enemy_hp" or "player_hp" or "energy" or "seed"))
            || script[0].GetProperty("action").GetString() != "play_card"
            || script[0].GetProperty("card").GetString() != hand[0].GetProperty("card").GetString()
            || script[0].EnumerateObject().Any(p => p.Name is not ("action" or "card" or "target")))
            throw new FormatException("Unsupported strike-normal setup or script; refusing to ignore input.");
        var hp = setup.GetProperty("enemy_hp").EnumerateArray().Select(v => v.GetInt32()).ToArray();
        var target = script[0].GetProperty("target").GetString()!;
        if (hp.Length != 2 || hp.Any(v => v <= 0) || target is not ("enemy:0" or "enemy:1"))
            throw new FormatException("strike-normal requires the original two living enemy slots.");
        if (string.IsNullOrWhiteSpace(item.GetProperty("game_seed").GetString())
            || !item.GetProperty("variants").EnumerateArray().Select(v => v.GetString()).SequenceEqual(new[] { "normal" })
            || !item.GetProperty("contents").EnumerateArray().Select(v => v.GetString()).SequenceEqual(new[] { "base-spire:strike-ironclad" })
            || !item.GetProperty("semantic_actions").EnumerateArray().Select(v => v.GetString())
                .SequenceEqual(new[] { "play_card:base-spire:strike-ironclad→" + target }))
            throw new FormatException("strike-normal registration does not match its executable input.");
        return new(item.GetProperty("id").GetString()!, item.GetProperty("game_seed").GetString()!,
            setup.TryGetProperty("seed", out var seed) ? seed.GetInt32() : 20261001,
            setup.GetProperty("player_hp").GetInt32(), setup.GetProperty("energy").GetInt32(), hp,
            hand[0].GetProperty("card").GetString()!, target, dimensions, item.GetProperty("expected_terminal").GetString()!);
    }
}

internal sealed record ActionOption(string Action, string[] Candidates, int Bindings);
internal sealed record DecisionSnapshot(string[] Candidates, long Minimum, long Maximum, string Chosen);
internal sealed record Boundary(string Kind, ActionOption[] Actions, DecisionSnapshot[] Decisions, SortedDictionary<string, string> Facts);
internal sealed record BackendRecord(string Backend, string Build, string Terminal, long InitializationRandomCount,
    long ScriptRandomCount, string[] Dimensions, Boundary[] Boundaries);
internal sealed record Divergence(int BoundaryIndex, string BoundaryKind, string Dimension, string Path,
    string? Left, string? Right, string LastConsistentBoundary, string Reproduction);
internal sealed record CaseResult(string Id, string Status, string? Reason = null, BackendRecord? Left = null,
    BackendRecord? Right = null, Divergence? FirstDifference = null)
{
    public object? ReferenceIdentity { get; init; }
    public StrikeFixture? Input { get; init; }
    public bool IndependentVerified => Status == "passed";
}

internal static class Comparison
{
    private static readonly string[] RequiredFacts = new[] { "player.hp", "player.max_hp", "player.block", "player.energy", "power.player.count" }
        .Concat(new[] { "base-spire:calcified-cultist", "base-spire:damp-cultist" }.SelectMany(enemy =>
            new[] { "hp", "max_hp", "block", "alive" }.Select(field => $"enemy.{enemy}.{field}").Append($"power.enemy.{enemy}.count")))
        .Concat(new[] { "draw", "hand", "discard", "exhaust" }.SelectMany(pile => new[] { $"pile.{pile}.size", $"pile.{pile}.sorted" }))
        .Concat(["pile.hand.order", "pile.exhaust.order"]).ToArray();

    public static CaseResult Compare(StrikeFixture fixture, BackendRecord left, BackendRecord right)
    {
        CaseResult Reject(string status, string reason) => new(fixture.Id, status, reason, left, right);
        if (left.Backend != "dsl:base-spire" || right.Backend != "reference:sts2-sim")
            return Reject("blocked_compare", "Two distinct production DSL and independent reference backends are required.");
        foreach (var record in new[] { left, right })
        {
            if (record.Boundaries.Length != 2 || fixture.RequiredDimensions.Except(record.Dimensions).Any()
                || record.Boundaries.Any(b => RequiredFacts.Except(b.Facts.Keys).Any()) || record.Boundaries[1].Decisions.Length == 0
                || record.Boundaries[0].Actions.Length == 0)
                return Reject("missing_dimension", record.Backend + ": incomplete stable boundaries or comparison dimensions.");
            if (record.Terminal != fixture.ExpectedTerminal)
                return Reject("wrong_terminal", record.Backend + ": " + record.Terminal);
            if (record.ScriptRandomCount != 0)
                return Reject("unsupported_rng_profile", record.Backend + ": controlled-no-random script consumed RNG.");
        }
        var last = "before start";
        for (var index = 0; index < left.Boundaries.Length; index++)
        {
            var a = left.Boundaries[index]; var b = right.Boundaries[index];
            var fields = new List<(string Dimension, string Path, string? Left, string? Right)>
            {
                ("facts", "kind", a.Kind, b.Kind),
                ("actions", "actions", JsonSerializer.Serialize(a.Actions), JsonSerializer.Serialize(b.Actions)),
                ("decisions", "decisions", JsonSerializer.Serialize(a.Decisions), JsonSerializer.Serialize(b.Decisions)),
            };
            foreach (var key in a.Facts.Keys.Union(b.Facts.Keys).Order(StringComparer.Ordinal))
                fields.Add((key.StartsWith("pile.", StringComparison.Ordinal) ? "pile-order" : "facts", key,
                    a.Facts.GetValueOrDefault(key), b.Facts.GetValueOrDefault(key)));
            foreach (var field in fields)
                if (field.Left != field.Right)
                    return new(fixture.Id, "diverged", null, left, right,
                        new(index, a.Kind, field.Dimension, field.Path, field.Left, field.Right, last,
                            "dotnet run --project tools/differential -- test --suite regression --case " + fixture.Id));
            last = index + ":" + a.Kind;
        }
        return new(fixture.Id, "passed", null, left, right);
    }
}
