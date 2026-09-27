using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace CoreTheSpire;

public readonly record struct ContentVariantProfile(bool DeadlyEnemies = false, bool ToughEnemies = false);

/// <summary>Loads encounter configuration and compiles it into validated core definitions.</summary>
public static class ContentDsl
{
    private static readonly JsonSerializerOptions Options = DslJson.CreateOptions();

    private sealed record EncounterFormationData(IReadOnlyList<ContentId> EnemyIds, int Weight = 1)
    {
        public EncounterFormationChoice Compile() => new(EnemyIds, Weight);
    }

    private sealed record EncounterSlotData(IReadOnlyList<ContentId> EnemyChoices, bool ChooseRandomly = false,
        ContentId? UniqueChoiceGroup = null)
    {
        public EncounterEnemySlot Compile()
        {
            if (ChooseRandomly) return EncounterEnemySlot.ChooseOne(EnemyChoices, UniqueChoiceGroup);
            if (EnemyChoices.Count != 1 || UniqueChoiceGroup is not null)
                throw new JsonException("A fixed encounter slot needs exactly one enemy and no uniqueChoiceGroup.");
            return EncounterEnemySlot.Fixed(EnemyChoices[0]);
        }
    }

    private sealed record EncounterDocumentData(ContentId Id, RunRoomType RoomType,
        IReadOnlyList<ContentId>? EnemyIds = null, IReadOnlyList<EncounterSlotData>? EnemySlots = null,
        IReadOnlyList<EncounterFormationData>? Formations = null, bool IsWeak = false,
        IReadOnlyList<ContentId>? Tags = null,
        IReadOnlyDictionary<int, ContentId>? StartingActionsByEnemyIndex = null,
        bool ShouldGiveRewards = true, int? MinGoldReward = null, int? MaxGoldReward = null)
    {
        public EncounterDefinition Compile()
        {
            var contentSources = (EnemyIds is null ? 0 : 1) + (EnemySlots is null ? 0 : 1) + (Formations is null ? 0 : 1);
            if (contentSources != 1)
                throw new JsonException("An encounter must define exactly one of enemyIds, enemySlots, or formations.");
            if (EnemyIds is not null)
                return new EncounterDefinition(Id, RoomType, EnemyIds, IsWeak, Tags,
                    StartingActionsByEnemyIndex, ShouldGiveRewards, MinGoldReward, MaxGoldReward);
            if (EnemySlots is not null)
                return EncounterDefinition.WithEnemySlots(Id, RoomType, EnemySlots.Select(slot => slot.Compile()),
                    IsWeak, Tags, StartingActionsByEnemyIndex, ShouldGiveRewards, MinGoldReward, MaxGoldReward);
            return EncounterDefinition.WithFormations(Id, RoomType, Formations!.Select(formation => formation.Compile()),
                IsWeak, Tags, StartingActionsByEnemyIndex, ShouldGiveRewards, MinGoldReward, MaxGoldReward);
        }
    }

    public static IReadOnlyList<EncounterDefinition> ParseEncounters(string json)
    {
        var document = JsonNode.Parse(json) as JsonObject
            ?? throw new JsonException("Encounter content must be a JSON object.");
        foreach (var key in document.Select(property => property.Key))
            if (key is not ("schemaVersion" or "encounters"))
                throw new JsonException($"Unknown encounter content document field '{key}'.");
        if (document["schemaVersion"]?.GetValue<int>() != 1)
            throw new JsonException("Unsupported or missing content schemaVersion; expected 1.");
        if (document["encounters"] is not JsonArray encounters)
            throw new JsonException("Encounter content must contain an 'encounters' array.");

        var definitions = new EncounterDefinition[encounters.Count];
        for (var i = 0; i < encounters.Count; i++)
        {
            var node = encounters[i] as JsonObject
                ?? throw new JsonException($"Encounter entry {i} must be an object.");
            definitions[i] = (JsonSerializer.Deserialize<EncounterDocumentData>(node.ToJsonString(), Options)
                ?? throw new JsonException($"Encounter entry {i} produced no definition.")).Compile();
        }
        if (definitions.Select(encounter => encounter.Id).Distinct().Count() != definitions.Length)
            throw new JsonException("Encounter IDs must be unique within a document.");
        return Array.AsReadOnly(definitions);
    }

}

internal static class DslJson
{
    public static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        options.Converters.Add(new ContentIdJsonConverter());
        return options;
    }

    private sealed class ContentIdJsonConverter : JsonConverter<ContentId>
    {
        public override ContentId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            ContentId.Parse(reader.GetString() ?? throw new JsonException("Content ID cannot be null."));

        public override void Write(Utf8JsonWriter writer, ContentId value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.Value);
    }
}
