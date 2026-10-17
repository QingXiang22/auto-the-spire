using System.Text.Json;
using BaseSpire.Slice;

namespace BaseSpire.SinglePlayer;

/// <summary>Frozen default act identities and source pools, independent of executable content support.</summary>
public sealed record RunActDefinition(string Id, string SourceName, int Index, int NormalRooms, int WeakRooms,
    int EliteRooms, IReadOnlyList<string> Events, IReadOnlyList<string> Ancients,
    IReadOnlyList<string> Weak, IReadOnlyList<string> Normal, IReadOnlyList<string> Elite, IReadOnlyList<string> Boss);

public sealed class RunActPlan
{
    public const string Resource = "BaseSpire.Content.base-spire.run.acts.json";
    public static RunActPlan Default { get; } = new(EncounterContent.ReadText(Resource));
    public IReadOnlyList<RunActDefinition> Acts { get; }
    public IReadOnlyList<string> SharedAncients { get; }
    public string Digest { get; }
    private readonly IReadOnlyDictionary<string, EncounterDeclaration> encounters;

    private sealed record Data(IReadOnlyList<RunActDefinition> Acts, IReadOnlyList<string> SharedAncients);
    private RunActPlan(string json)
    {
        var data = JsonSerializer.Deserialize<Data>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        Acts = data.Acts; SharedAncients = data.SharedAncients;
        if (!Acts.Select(a => a.Index).SequenceEqual(Enumerable.Range(0, Acts.Count)))
            throw new InvalidOperationException("Default act plan must have contiguous source indices");
        Digest = EncounterContent.Digest(json);
        var existing = EncounterContent.LoadEncounters().ToDictionary(e => e.Id, StringComparer.Ordinal);
        // Spawn programs have one owner. The versioned plan contributes identity/tag metadata;
        // reuse an existing formation rather than copying a second editable spawn declaration.
        encounters = EncounterContent.ParseEncounters(json).ToDictionary(e => e.Id,
            e => existing.TryGetValue(e.Id, out var declaration) ? declaration with { Tags = e.Tags } : e,
            StringComparer.Ordinal);
    }

    public SliceEncounterPool Pool(RunActDefinition act) => new(
        Load(act.Weak, SliceEncounterPool.WeakKind), Load(act.Normal, SliceEncounterPool.NormalKind),
        act.WeakRooms, act.NormalRooms, VanillaRandomLayout.EngineStreamName("UpFront"),
        VanillaRandomLayout.EngineStreamName("Shuffle"), Load(act.Boss, "boss"), act.EliteRooms,
        Load(act.Elite, SliceEncounterPool.EliteKind));

    public SliceEncounterPool Combined => Pool(Acts[0]) with
    {
        WeakEncounters = Acts.SelectMany(a => Pool(a).WeakEncounters).DistinctBy(e => e.EncounterId).ToArray(),
        RegularEncounters = Acts.SelectMany(a => Pool(a).RegularEncounters).DistinctBy(e => e.EncounterId).ToArray(),
        EliteEncounters = Acts.SelectMany(a => Pool(a).Elites).DistinctBy(e => e.EncounterId).ToArray(),
        BossEncounters = Acts.SelectMany(a => Pool(a).Bosses).DistinctBy(e => e.EncounterId).ToArray(),
    };

    private IReadOnlyList<SliceEncounter> Load(IReadOnlyList<string> ids, string kind) => ids.Select(id =>
    {
        var source = encounters[id];
        return new SliceEncounter(id, kind, source.EnemyIds, source.Tags, source.Formations, source.Slots)
            { StartingActionsByEnemyIndex = source.StartingActionsByEnemyIndex };
    }).ToArray();
}
