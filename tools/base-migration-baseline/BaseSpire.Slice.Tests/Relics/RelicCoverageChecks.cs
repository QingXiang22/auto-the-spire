using System.Text.Json;
using BaseSpire.Slice;

namespace BaseSpire.Slice.Tests;

internal static class RelicCoverageChecks
{
    private static readonly Dictionary<string, HashSet<string>> Evidence = new(StringComparer.Ordinal);

    public static void Passed(string name, string scenario)
    {
        var id = SpireRelics.All.Single(r => r.Name == name).ContentId;
        if (!Evidence.TryGetValue(id, out var cases)) Evidence[id] = cases = new(StringComparer.Ordinal);
        cases.Add(scenario);
    }

    public static void Validate()
    {
        Check.Run("Relics CI: every executable definition has a passed behavior witness; full source denominator retained", () =>
        {
            Check.Equal(297, RelicSourceCatalog.All.Count, "frozen source denominator");
            var missing = SpireRelics.All.Where(r => !Evidence.ContainsKey(r.ContentId)).Select(r => r.Name).ToArray();
            Check.Equal(0, missing.Length, "unverified executable relics: " + string.Join(", ", missing));
            Check.True(RelicSourceCatalog.All.Where(r => r.Eligibility.StartsWith("excluded-", StringComparison.Ordinal))
                .All(r => r.EligibilityEvidence.Length > 0), "every exclusion retains source evidence");
        });
    }

    public static void Write(string directory)
    {
        var definitions = SpireRelics.All.ToDictionary(r => r.ContentId, StringComparer.Ordinal);
        var rows = RelicSourceCatalog.All.Select(source => new
        {
            source.ContentId, source.Name, source.Rarity, source.Pools, source.Eligibility, source.EligibilityEvidence,
            source.SourcePath, source.SourceSha256, source.Hooks,
            declared = definitions.ContainsKey(source.ContentId),
            behavior_checked = Evidence.ContainsKey(source.ContentId),
            passed_scenarios = Evidence.TryGetValue(source.ContentId, out var cases) ? cases.Order(StringComparer.Ordinal).ToArray() : [],
            status = definitions.ContainsKey(source.ContentId) ? Evidence.ContainsKey(source.ContentId) ? "behavior-checked" : "missing-behavior-evidence"
                : source.Eligibility.StartsWith("excluded-", StringComparison.Ordinal) ? source.Eligibility : "unimplemented",
            independent_verified = (bool?)null, public_run_verified = (bool?)null,
        }).ToArray();
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "relic-coverage.json"), JsonSerializer.Serialize(new
        {
            schema = "base-spire/relic-coverage@1", scope = "controlled-formal-source-behavior",
            source_count = rows.Length, declared_count = definitions.Count, behavior_checked_count = Evidence.Count,
            unimplemented_count = rows.Count(r => r.status == "unimplemented"),
            complete = rows.All(r => r.status is "behavior-checked" or "excluded-deprecated" or "excluded-multiplayer"),
            relics = rows,
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
