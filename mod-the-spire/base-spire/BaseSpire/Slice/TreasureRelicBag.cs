namespace BaseSpire.Slice;

/// <summary>Engine-independent relic bag semantics. The caller supplies source-faithful shuffled order.</summary>
public sealed class TreasureRelicBag
{
    private readonly Dictionary<string, Queue<SharedRelicPoolEntry>> buckets;

    private TreasureRelicBag(Dictionary<string, Queue<SharedRelicPoolEntry>> buckets) => this.buckets = buckets;

    public static TreasureRelicBag FromOrder(IEnumerable<SharedRelicPoolEntry> shuffledOrder)
    {
        ArgumentNullException.ThrowIfNull(shuffledOrder);
        var buckets = new Dictionary<string, Queue<SharedRelicPoolEntry>>(StringComparer.Ordinal);
        foreach (var entry in shuffledOrder)
        {
            if (!SharedRelicPoolManifest.ForTreasure.Contains(entry))
            {
                throw new ArgumentException($"遗物 '{entry.SourceName}' 不在宝箱正式池中。", nameof(shuffledOrder));
            }
            if (!buckets.TryGetValue(entry.Rarity, out var bucket))
            {
                bucket = new Queue<SharedRelicPoolEntry>();
                buckets.Add(entry.Rarity, bucket);
            }
            bucket.Enqueue(entry);
        }
        return new TreasureRelicBag(buckets);
    }

    public SharedRelicPoolEntry? Pull(string requestedRarity)
    {
        ArgumentNullException.ThrowIfNull(requestedRarity);
        foreach (var rarity in RarityOrder(requestedRarity))
        {
            if (buckets.TryGetValue(rarity, out var bucket) && bucket.Count > 0)
            {
                return bucket.Dequeue();
            }
        }
        return null;
    }

    public int Remaining(string rarity) =>
        buckets.TryGetValue(rarity, out var bucket) ? bucket.Count : 0;

    private static IEnumerable<string> RarityOrder(string requested) => requested switch
    {
        "Common" => ["Common", "Uncommon", "Rare"],
        "Uncommon" => ["Uncommon", "Rare"],
        "Rare" => ["Rare"],
        _ => throw new ArgumentException($"未知遗物稀有度 '{requested}'。", nameof(requested)),
    };
}
