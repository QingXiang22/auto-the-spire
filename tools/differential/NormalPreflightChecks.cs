using System.Text.Json;
using System.Text.Json.Nodes;

namespace Differential;

internal static class NormalPreflightChecks
{
    public static void Run()
    {
        // Comparator fault injections only; these are not independent gameplay results.
        var facts = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["player.hp"] = "80", ["player.max_hp"] = "80", ["player.gold"] = "99",
            ["deck"] = NormalPreflight.Json(Enumerable.Repeat("card", 10)), ["relics"] = "[\"relic\"]",
            ["neow.options"] = "[\"a\",\"b\",\"c\"]", ["map"] = "[{\"coordinate\":\"0,0\"}]"
        };
        var left = new NormalPreflight.Opening("dsl:public-stdio", "left", facts);
        var right = new NormalPreflight.Opening("reference:sts2-sim", "right", facts);
        Require(NormalPreflight.First(left, right) is null, "normalized complete opening can match");
        Throws(() => NormalPreflight.First(left, right with { Backend = left.Backend }), "same backend rejected");
        foreach (var key in NormalPreflight.RequiredFacts)
        {
            var missing = new SortedDictionary<string, string>(facts, StringComparer.Ordinal); missing.Remove(key);
            Throws(() => NormalPreflight.First(left with { Facts = missing }, right with { Facts = missing }), "both sides omit " + key);
        }
        foreach (var key in new[] { "deck", "relics", "neow.options", "map" })
        {
            var zero = new SortedDictionary<string, string>(facts, StringComparer.Ordinal) { [key] = "[]" };
            Throws(() => NormalPreflight.First(left with { Facts = zero }, right with { Facts = zero }), "empty " + key);
        }
        var reordered = new SortedDictionary<string, string>(facts, StringComparer.Ordinal) { ["neow.options"] = "[\"b\",\"a\",\"c\"]" };
        var difference = NormalPreflight.First(left, right with { Facts = reordered });
        Require(difference is { Path: "neow.options", Boundary: "neow-ready", Dimension: "decisions" }, "ordered choice first difference");
        var map = new SortedDictionary<string, string>(facts, StringComparer.Ordinal) { ["map"] = "[{\"coordinate\":\"1,0\"}]" };
        Require(NormalPreflight.First(left, right with { Facts = map })?.Path == "map", "map first difference");
        var partial = new NormalPreflight.Result("m01-claim", "blocked_compare", "post-Neow dimensions unavailable", left, right, null, [], true);
        Require(!partial.Complete && !partial.IndependentVerified && partial.UnverifiedDimensions.Length > 0,
            "matching prefix plus successful one-sided return cannot certify M01");
        var file = Path.Combine(Repository.Find(), "tools/base-spire-dev/fixtures/normal-first-combat.json");
        var input = JsonNode.Parse(File.ReadAllText(file))!.AsObject();
        using (var valid = JsonDocument.Parse(input.ToJsonString())) NormalPreflight.ValidateInput(valid.RootElement);
        foreach (var key in new[] { "reward_paths", "combat_action_priority" })
        {
            var invalid = input.DeepClone().AsObject(); invalid[key] = new JsonArray();
            using var document = JsonDocument.Parse(invalid.ToJsonString());
            Throws(() => NormalPreflight.ValidateInput(document.RootElement), "registered policy cannot lose " + key);
        }
        Console.WriteLine("Normal preflight checks passed: missing/empty dimensions, ordered choices, map, policy registration and incomplete M01 rejection.");
    }
    private static void Require(bool result, string name) { if (!result) throw new InvalidOperationException("Normal preflight check failed: " + name); }
    private static void Throws(Action body, string name)
    {
        try { body(); } catch (FormatException) { return; }
        throw new InvalidOperationException("Normal preflight check did not reject: " + name);
    }
}
