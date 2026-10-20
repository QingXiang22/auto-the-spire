using System.Text.Json;

namespace Differential;

internal sealed record CombatFixture(string Id, string GameSeed, int Seed, int PlayerHp, int Energy, int[] EnemyHp,
    string Card, string Target, string[] RequiredDimensions, string ExpectedTerminal)
{
    public static readonly string[] Dimensions = ["actions", "decisions", "facts", "pile-order"];
    public bool Reshuffle => Id == "random-reshuffle-unsupported";
    public OrderedCard[] InitialCards { get; init; } = [];
    public static readonly string[] RandomDimensions = ["actions", "facts", "pile-order", "streams"];

    public static CombatFixture Read(JsonElement item)
    {
        if (item.GetProperty("id").GetString() == "random-reshuffle-unsupported") return ReadReshuffle(item);
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

    private static CombatFixture ReadReshuffle(JsonElement item)
    {
        var setup = item.GetProperty("setup");
        var piles = setup.GetProperty("initial_piles");
        var cards = piles.GetProperty("draw").EnumerateArray().Select(c => new OrderedCard(
            c.GetProperty("slot").GetString()!, c.GetProperty("card").GetString()!, c.GetProperty("upgraded").GetBoolean())).ToArray();
        string[] expected = [.. Enumerable.Repeat("base-spire:strike-ironclad", 5),
            .. Enumerable.Repeat("base-spire:defend-ironclad", 4), "base-spire:bash"];
        var dimensions = item.GetProperty("required_dimensions").EnumerateArray().Select(v => v.GetString()!).ToArray();
        var hp = setup.GetProperty("enemy_hp").EnumerateArray().Select(v => v.GetInt32()).ToArray();
        if (item.GetProperty("kind").GetString() != "mechanism" || item.GetProperty("base_source").GetString() != "controlled-slice-v1"
            || item.GetProperty("rng_profile").GetString() != "vanilla-compatible@1"
            || piles.GetProperty("version").GetString() != "ordered-starting-piles@1"
            || piles.GetProperty("source").GetString() != "Ironclad.StartingDeck+controlled-slice@1"
            || piles.GetProperty("rng_derivation").GetString() != "source-compat-sts2-frozen@1"
            || new[] { "hand", "discard", "exhaust" }.Any(p => piles.GetProperty(p).GetArrayLength() != 0)
            || cards.Any(c => string.IsNullOrWhiteSpace(c.Slot) || c.Upgraded) || cards.Select(c => c.Slot).Distinct().Count() != 10
            || !cards.Select(c => c.Card).Order().SequenceEqual(expected.Order())
            || piles.GetProperty("draw").EnumerateArray().Any(c => c.EnumerateObject().Any(p => p.Name is not ("slot" or "card" or "upgraded")))
            || piles.EnumerateObject().Any(p => p.Name is not ("version" or "source" or "rng_derivation" or "hand" or "draw" or "discard" or "exhaust"))
            || !dimensions.Order().SequenceEqual(RandomDimensions.Order()) || hp.Length != 2 || hp.Any(h => h <= 0)
            || item.GetProperty("script").GetArrayLength() != 3
            || item.GetProperty("script").EnumerateArray().Any(s => s.EnumerateObject().Count() != 1 || s.GetProperty("action").GetString() != "end_turn")
            || !item.GetProperty("semantic_actions").EnumerateArray().Select(v => v.GetString()).SequenceEqual(Enumerable.Repeat("end_turn", 3))
            || string.IsNullOrWhiteSpace(item.GetProperty("game_seed").GetString())
            || !item.GetProperty("contents").EnumerateArray().Select(v => v.GetString()).Order().SequenceEqual(expected.Distinct().Order())
            || !item.GetProperty("variants").EnumerateArray().Select(v => v.GetString()).SequenceEqual(new[] { "reshuffle", "end-turn-loop" })
            || setup.EnumerateObject().Any(p => p.Name is not ("initial_piles" or "enemy_hp" or "player_hp" or "energy")))
            throw new FormatException("Unsupported or incomplete original reshuffle fixture; refusing to ignore input.");
        return new(item.GetProperty("id").GetString()!, item.GetProperty("game_seed").GetString()!, 20261001,
            setup.GetProperty("player_hp").GetInt32(), setup.GetProperty("energy").GetInt32(), hp, "", "", dimensions,
            item.GetProperty("expected_terminal").GetString()!) { InitialCards = cards };
    }
}

internal sealed record OrderedCard(string Slot, string Card, bool Upgraded);

internal sealed record ActionOption(string Action, string[] Candidates, int Bindings);
internal sealed record DecisionSnapshot(string[] Candidates, long Minimum, long Maximum, string Chosen);
internal sealed record Boundary(string Kind, ActionOption[] Actions, DecisionSnapshot[] Decisions, SortedDictionary<string, string> Facts)
{
    public RandomTrace? Random { get; init; }
}
internal sealed record BackendRecord(string Backend, string Build, string Terminal, long InitializationRandomCount,
    long ScriptRandomCount, string[] Dimensions, Boundary[] Boundaries)
{
    public object? Initialization { get; init; }
}
internal sealed record Divergence(int BoundaryIndex, string BoundaryKind, string Dimension, string Path,
    string? Left, string? Right, string LastConsistentBoundary, string Reproduction);
internal sealed record CaseResult(string Id, string Status, string? Reason = null, BackendRecord? Left = null,
    BackendRecord? Right = null, Divergence? FirstDifference = null)
{
    public object? ReferenceIdentity { get; init; }
    public CombatFixture? Input { get; init; }
    public bool IndependentVerified => Status == "passed";
}

internal static class Comparison
{
    private static readonly string[] RequiredFacts = new[] { "player.hp", "player.max_hp", "player.block", "player.energy", "power.player.count" }
        .Concat(new[] { "base-spire:calcified-cultist", "base-spire:damp-cultist" }.SelectMany(enemy =>
            new[] { "hp", "max_hp", "block", "alive" }.Select(field => $"enemy.{enemy}.{field}").Append($"power.enemy.{enemy}.count")))
        .Concat(new[] { "draw", "hand", "discard", "exhaust" }.SelectMany(pile => new[] { $"pile.{pile}.size", $"pile.{pile}.sorted" }))
        .Concat(["pile.hand.order", "pile.exhaust.order"]).ToArray();

    public static CaseResult Compare(CombatFixture fixture, BackendRecord left, BackendRecord right)
    {
        CaseResult Reject(string status, string reason) => new(fixture.Id, status, reason, left, right);
        if (left.Backend != "dsl:base-spire" || right.Backend != "reference:sts2-sim")
            return Reject("blocked_compare", "Two distinct production DSL and independent reference backends are required.");
        foreach (var record in new[] { left, right })
        {
            var requiredFacts = fixture.Reshuffle ? RequiredFacts.Concat(new[] { "draw", "hand", "discard", "exhaust" }
                .SelectMany(p => new[] { $"pile.{p}.order", $"pile.{p}.slots" })) : RequiredFacts;
            if (record.Boundaries.Length != (fixture.Reshuffle ? 4 : 2) || fixture.RequiredDimensions.Except(record.Dimensions).Any()
                || record.Boundaries.Any(b => requiredFacts.Except(b.Facts.Keys).Any()) || (!fixture.Reshuffle && record.Boundaries[1].Decisions.Length == 0)
                || record.Boundaries[0].Actions.Length == 0)
                return Reject("missing_dimension", record.Backend + ": incomplete stable boundaries or comparison dimensions.");
            if (record.Terminal != fixture.ExpectedTerminal)
                return Reject("wrong_terminal", record.Backend + ": " + record.Terminal);
            if (fixture.Reshuffle && record.Boundaries.Any(b => !RandomComparison.Complete(b.Random)))
                return Reject("missing_dimension", record.Backend + ": missing, truncated, overflowed or discontinuous random trace.");
            if (fixture.Reshuffle && record.Boundaries[^1].Random!.Calls.Length != record.ScriptRandomCount)
                return Reject("missing_dimension", record.Backend + ": trace does not account for measured script random consumption.");
            if (!fixture.Reshuffle && record.ScriptRandomCount != 0)
                return Reject("unsupported_rng_profile", record.Backend + ": controlled-no-random script consumed RNG.");
        }
        var last = "before start";
        for (var index = 0; index < left.Boundaries.Length; index++)
        {
            var a = left.Boundaries[index]; var b = right.Boundaries[index];
            if (fixture.Reshuffle && RandomComparison.First(a.Random!, b.Random!) is { } randomDifference)
                return new(fixture.Id, "diverged", null, left, right,
                    new(index, a.Kind, "streams", randomDifference.Path, randomDifference.Left, randomDifference.Right, last,
                        "dotnet run --project tools/differential -- test --suite regression --case " + fixture.Id));
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
