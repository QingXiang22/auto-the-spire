using System.Text.Json;
using System.Text.Json.Serialization;
using BaseSpire.SinglePlayer;

namespace BaseSpire.Slice;

public sealed record RelicSourceEntry(string Name, string ContentId, string Rarity,
    IReadOnlyList<string> Pools, string SourcePath, string SourceSha256, string Eligibility,
    string EligibilityEvidence, IReadOnlyList<string> Hooks);

/// <summary>Versioned source identities for CI without a proprietary source checkout. Not an effect registry.</summary>
public static class RelicSourceCatalog
{
    public const string Resource = "BaseSpire.ContentSource.relic-source-catalog.json";
    public const int SourceCount = 297;
    public static IReadOnlyList<RelicSourceEntry> All { get; } = Load();

    private static IReadOnlyList<RelicSourceEntry> Load()
    {
        var rows = JsonSerializer.Deserialize<RelicSourceEntry[]>(EncounterContent.ReadText(Resource),
            new JsonSerializerOptions { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
                RespectRequiredConstructorParameters = true })
            ?? throw new InvalidOperationException("Missing source relic catalog");
        if (rows.Length != SourceCount || rows.Select(r => r.Name).Distinct().Count() != SourceCount
            || rows.Select(r => r.ContentId).Distinct().Count() != SourceCount)
            throw new InvalidOperationException("Source relic denominator or identity mismatch");
        foreach (var row in rows)
            if (row.ContentId != "base-spire:" + SpireCardSource.ToId(row.Name) || row.Pools.Count == 0
                || row.SourceSha256.Length != 64 || row.Eligibility is not ("singleplayer" or "unreviewed" or "excluded-deprecated" or "excluded-multiplayer")
                || row.Eligibility != "unreviewed" && string.IsNullOrWhiteSpace(row.EligibilityEvidence))
                throw new InvalidOperationException($"Invalid source relic catalog entry: {row.Name}");
        foreach (var definition in SpireRelics.All)
            if (!rows.Any(row => row.ContentId == definition.ContentId && row.Eligibility == "singleplayer"))
                throw new InvalidOperationException($"Executable relic missing eligibility evidence: {definition.Name}");
        return Array.AsReadOnly(rows);
    }
}
