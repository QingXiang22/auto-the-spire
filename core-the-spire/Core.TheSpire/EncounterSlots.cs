namespace CoreTheSpire;

/// <summary>A fixed enemy or a random choice among enemies assigned to one encounter position.</summary>
public sealed class EncounterEnemySlot
{
    private EncounterEnemySlot(IEnumerable<ContentId> enemyChoices, bool chooseRandomly, ContentId? uniqueChoiceGroup)
    {
        ArgumentNullException.ThrowIfNull(enemyChoices);
        var choices = enemyChoices.ToArray();
        if (choices.Length == 0) throw new ArgumentException("An encounter slot needs at least one enemy choice.", nameof(enemyChoices));
        foreach (var enemyId in choices) enemyId.Validate();
        if (choices.Distinct().Count() != choices.Length)
            throw new ArgumentException("An encounter slot cannot repeat an enemy choice.", nameof(enemyChoices));
        if (uniqueChoiceGroup is { } group) group.Validate();
        if (!chooseRandomly && (choices.Length != 1 || uniqueChoiceGroup is not null))
            throw new ArgumentException("Fixed encounter slots need exactly one enemy and cannot use a choice group.", nameof(enemyChoices));
        EnemyChoices = Array.AsReadOnly(choices);
        ChooseRandomly = chooseRandomly;
        UniqueChoiceGroup = uniqueChoiceGroup;
    }

    public IReadOnlyList<ContentId> EnemyChoices { get; }
    public bool ChooseRandomly { get; }
    public ContentId? UniqueChoiceGroup { get; }

    public static EncounterEnemySlot Fixed(ContentId enemyId) =>
        new([enemyId], chooseRandomly: false, uniqueChoiceGroup: null);

    public static EncounterEnemySlot ChooseOne(IEnumerable<ContentId> enemyChoices, ContentId? uniqueChoiceGroup = null) =>
        new(enemyChoices, chooseRandomly: true, uniqueChoiceGroup);
}

public sealed record EncounterEnemySelection(ContentId EnemyId, ContentId? StartingActionOverride);

/// <summary>A complete ordered enemy lineup selected as one correlated encounter outcome.</summary>
public sealed record EncounterFormationChoice
{
    public EncounterFormationChoice(IEnumerable<ContentId> enemyIds, int weight = 1)
    {
        ArgumentNullException.ThrowIfNull(enemyIds);
        var copiedIds = enemyIds.ToArray();
        if (copiedIds.Length == 0) throw new ArgumentException("An encounter formation cannot be empty.", nameof(enemyIds));
        foreach (var enemyId in copiedIds) enemyId.Validate();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(weight);
        EnemyIds = Array.AsReadOnly(copiedIds);
        Weight = weight;
    }

    public IReadOnlyList<ContentId> EnemyIds { get; }
    public int Weight { get; }
}
