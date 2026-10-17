using BaseSpire.Slice;

namespace BaseSpire.SinglePlayer;

/// <summary>Explicit content metadata migration; independent from the RNG algorithm version.</summary>
public static class SinglePlayerContentVersions
{
    public const string Frozen = "sts2-frozen-card-metadata@1";
    public const string Current = "sts2-card-metadata-2089659@1";

    public static SinglePlayerContent Apply(SinglePlayerContent source, string version)
    {
        if (version == Frozen) return source;
        if (version != Current) throw new ArgumentException($"Unsupported content metadata version '{version}'.", nameof(version));
        // Read-only source: Sts2Sim.Core/Models/Cards/{Bloodletting,Taunt,Dominate,Cruelty}.
        var rarities = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["base-spire:bloodletting"] = "uncommon", ["base-spire:taunt"] = "common",
            ["base-spire:dominate"] = "rare", ["base-spire:cruelty"] = "uncommon",
        };
        foreach (var id in rarities.Keys)
            if (source.Model.Cards.Count(c => c.Id == id) != 1)
                throw new InvalidOperationException($"Content metadata migration requires one definition of '{id}'.");
        return source with { Model = source.Model with { Cards = source.Model.Cards.Select(card =>
            rarities.TryGetValue(card.Id, out var rarity)
                ? card with { Rarity = rarity, UpgradedRarity = rarity } : card).ToArray() } };
    }
}
