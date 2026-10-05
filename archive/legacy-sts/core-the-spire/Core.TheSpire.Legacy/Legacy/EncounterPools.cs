namespace CoreTheSpire;

/// <summary>Act-level encounter catalog. Encounter properties live on EncounterDefinition, so pool data cannot drift.</summary>
public sealed class ActEncounterPoolDefinition
{
    public ActEncounterPoolDefinition(IEnumerable<ContentId> weakNormalEncounters,
        IEnumerable<ContentId> regularEncounters, IEnumerable<ContentId> eliteEncounters,
        IEnumerable<ContentId> bossEncounters, int normalRoomCount, int weakNormalRoomCount = 3,
        int eliteQueueCount = 15)
    {
        WeakNormalEncounters = Copy(weakNormalEncounters, nameof(weakNormalEncounters));
        RegularEncounters = Copy(regularEncounters, nameof(regularEncounters));
        EliteEncounters = Copy(eliteEncounters, nameof(eliteEncounters));
        BossEncounters = Copy(bossEncounters, nameof(bossEncounters));
        ArgumentOutOfRangeException.ThrowIfNegative(normalRoomCount);
        ArgumentOutOfRangeException.ThrowIfNegative(weakNormalRoomCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(eliteQueueCount);
        if (weakNormalRoomCount > normalRoomCount)
            throw new ArgumentOutOfRangeException(nameof(weakNormalRoomCount));
        RequirePool(weakNormalRoomCount, WeakNormalEncounters, nameof(weakNormalEncounters));
        RequirePool(normalRoomCount - weakNormalRoomCount, RegularEncounters, nameof(regularEncounters));
        RequirePool(eliteQueueCount, EliteEncounters, nameof(eliteEncounters));
        RequirePool(1, BossEncounters, nameof(bossEncounters));
        NormalRoomCount = normalRoomCount;
        WeakNormalRoomCount = weakNormalRoomCount;
        EliteQueueCount = eliteQueueCount;
    }

    public IReadOnlyList<ContentId> WeakNormalEncounters { get; }
    public IReadOnlyList<ContentId> RegularEncounters { get; }
    public IReadOnlyList<ContentId> EliteEncounters { get; }
    public IReadOnlyList<ContentId> BossEncounters { get; }
    public int NormalRoomCount { get; }
    public int WeakNormalRoomCount { get; }
    public int EliteQueueCount { get; }

    internal void Validate(IReadOnlyDictionary<ContentId, EncounterDefinition> encounters)
    {
        ValidatePool(WeakNormalEncounters, RunRoomType.Monster, isWeak: true, encounters);
        ValidatePool(RegularEncounters, RunRoomType.Monster, isWeak: false, encounters);
        ValidatePool(EliteEncounters, RunRoomType.Elite, isWeak: false, encounters);
        ValidatePool(BossEncounters, RunRoomType.Boss, isWeak: false, encounters);
    }

    internal ActEncounterState Generate(SeededRandom random,
        IReadOnlyDictionary<ContentId, EncounterDefinition> encounters)
    {
        var normal = GenerateQueue(WeakNormalEncounters, WeakNormalRoomCount, null, encounters, random);
        normal.AddRange(GenerateQueue(RegularEncounters, NormalRoomCount - WeakNormalRoomCount,
            normal.Count == 0 ? null : normal[^1], encounters, random));
        var elite = GenerateQueue(EliteEncounters, EliteQueueCount, null, encounters, random);
        var boss = BossEncounters[random.NextInt(BossEncounters.Count)];
        return new ActEncounterState(normal, elite, boss);
    }

    private static List<ContentId> GenerateQueue(IReadOnlyList<ContentId> pool, int count,
        ContentId? previous, IReadOnlyDictionary<ContentId, EncounterDefinition> encounters, SeededRandom random)
    {
        var bag = new List<ContentId>();
        var queue = new List<ContentId>(count);
        for (var i = 0; i < count; i++)
        {
            if (bag.Count == 0) bag.AddRange(pool);
            var previousTags = previous is { } id ? encounters[id].Tags : Array.Empty<ContentId>();
            var hasAllowedEntry = bag.Any(id => id != previous &&
                !encounters[id].Tags.Intersect(previousTags).Any());
            int selected;
            do
            {
                selected = (int)(random.NextDouble() * bag.Count);
            }
            while (hasAllowedEntry && (bag[selected] == previous ||
                encounters[bag[selected]].Tags.Intersect(previousTags).Any()));

            previous = bag[selected];
            queue.Add(previous.Value);
            bag.RemoveAt(selected);
        }
        return queue;
    }

    private static void ValidatePool(IReadOnlyList<ContentId> pool, RunRoomType roomType, bool isWeak,
        IReadOnlyDictionary<ContentId, EncounterDefinition> encounters)
    {
        foreach (var id in pool)
        {
            if (!encounters.TryGetValue(id, out var encounter))
                throw new InvalidOperationException($"Encounter pool references unknown encounter '{id}'.");
            if (encounter.RoomType != roomType || encounter.IsWeak != isWeak)
                throw new InvalidOperationException($"Encounter '{id}' does not match its {roomType} / weak={isWeak} pool.");
        }
    }

    private static IReadOnlyList<ContentId> Copy(IEnumerable<ContentId> source, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(source);
        var ids = source.ToArray();
        foreach (var id in ids) id.Validate();
        if (ids.Distinct().Count() != ids.Length)
            throw new ArgumentException("Encounter IDs must be unique within a pool.", parameterName);
        return Array.AsReadOnly(ids);
    }

    private static void RequirePool(int count, IReadOnlyList<ContentId> pool, string parameterName)
    {
        if (count > 0 && pool.Count == 0)
            throw new ArgumentException("The encounter pool must not be empty when entries are generated.", parameterName);
    }
}

internal sealed class ActEncounterState(IReadOnlyList<ContentId> normal, IReadOnlyList<ContentId> elite, ContentId boss)
{
    private int _normalVisited;
    private int _eliteVisited;

    public ContentId Next(RunRoomType roomType) => roomType switch
    {
        RunRoomType.Monster => normal[_normalVisited++ % normal.Count],
        RunRoomType.Elite => elite[_eliteVisited++ % elite.Count],
        RunRoomType.Boss => boss,
        _ => throw new ArgumentOutOfRangeException(nameof(roomType), "Only combat rooms have encounters.")
    };
}
