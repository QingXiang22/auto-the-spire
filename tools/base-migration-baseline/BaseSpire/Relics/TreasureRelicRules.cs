namespace BaseSpire.Slice;

/// <summary>Pure treasure-room selection rules; state mutation remains in Engine IR.</summary>
public static class TreasureRelicRules
{
    public const long RandomSpan = 1L << 53;

    public static string RarityForPayload(long payload)
    {
        if (payload < 0 || payload >= RandomSpan)
        {
            throw new ArgumentOutOfRangeException(nameof(payload));
        }

        var unit = (float)(payload / (double)RandomSpan);
        return unit < 0.5f ? "Common" : unit < 0.83f ? "Uncommon" : "Rare";
    }

    public static int GoldForPayload(long payload)
    {
        if (payload < 0 || payload >= RandomSpan)
        {
            throw new ArgumentOutOfRangeException(nameof(payload));
        }

        return 42 + (int)(payload * 11L / RandomSpan);
    }

    public static IReadOnlyList<SharedRelicPoolEntry> Candidates(string rarity) =>
        SharedRelicPoolManifest.ForTreasure
            .Where(entry => string.Equals(entry.Rarity, rarity, StringComparison.Ordinal))
            .ToArray();
}
