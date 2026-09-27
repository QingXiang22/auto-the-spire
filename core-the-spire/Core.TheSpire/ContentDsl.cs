using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace CoreTheSpire;

public readonly record struct ContentVariantProfile(bool DeadlyEnemies = false, bool ToughEnemies = false);

/// <summary>Loads declarative content documents and compiles them into validated core definitions.</summary>
public static class ContentDsl
{
    private static readonly JsonSerializerOptions Options = DslJson.CreateOptions();

    private sealed record EnemyTransitionData(IReadOnlyList<EnemyActionChoice> Choices,
        EnemyActionRepeatPolicy RepeatPolicy = EnemyActionRepeatPolicy.Allow,
        EnemyActionSelectionMode SelectionMode = EnemyActionSelectionMode.WeightedRandom)
    {
        public EnemyActionTransition Compile() => new(Choices, RepeatPolicy, SelectionMode);
    }

    private sealed record EnemyActionData(ContentId Id, EffectNode Effect, IntentKind Intent,
        int IntentDamage = 0, ContentId? AppliedPower = null, int AppliedPowerAmount = 0,
        ContentId? NextActionId = null, TargetSelector AppliedPowerTarget = TargetSelector.Self,
        EnemyTransitionData? Transition = null)
    {
        public EnemyActionDefinition Compile() => new(Id, Effect, Intent, IntentDamage,
            AppliedPower, AppliedPowerAmount, NextActionId, AppliedPowerTarget, Transition?.Compile());
    }

    private sealed record EnemyDocumentData(ContentId Id, int MinHp, int MaxHp,
        IReadOnlyList<EnemyActionData> Actions, bool LoopLastAction = true,
        ContentId? StartingActionId = null, EnemyTransitionData? StartingTransition = null)
    {
        public EnemyDefinition Compile() => new(Id, MinHp, MaxHp, Actions.Select(action => action.Compile()).ToArray(), LoopLastAction,
            StartingActionId, StartingTransition?.Compile());
    }

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

    public static EnemyDefinition ParseEnemy(string json, ContentVariantProfile profile = default)
    {
        var document = JsonNode.Parse(json) as JsonObject
            ?? throw new JsonException("Enemy content must be a JSON object.");
        foreach (var key in document.Select(property => property.Key))
            if (key is not ("schemaVersion" or "enemy"))
                throw new JsonException($"Unknown enemy content document field '{key}'.");
        if (document["schemaVersion"]?.GetValue<int>() != 1)
            throw new JsonException("Unsupported or missing content schemaVersion; expected 1.");
        if (document["enemy"] is not JsonObject enemy)
            throw new JsonException("Enemy content must contain an 'enemy' object.");

        var compiledEnemy = ResolveVariants(enemy, profile) as JsonObject
            ?? throw new JsonException("The compiled enemy content is not an object.");
        return (JsonSerializer.Deserialize<EnemyDocumentData>(compiledEnemy.ToJsonString(), Options)
            ?? throw new JsonException("Enemy content produced no definition.")).Compile();
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

    private static JsonNode? ResolveVariants(JsonNode? node, ContentVariantProfile profile)
    {
        if (node is JsonObject objectNode)
        {
            if (objectNode.Count == 1 && objectNode["$variant"] is JsonObject variants)
            {
                var selected = SelectVariant(variants, profile);
                return ResolveVariants(selected?.DeepClone(), profile);
            }

            var resolvedObject = new JsonObject();
            foreach (var (key, value) in objectNode)
                resolvedObject.Add(key, ResolveVariants(value?.DeepClone(), profile));
            return resolvedObject;
        }

        if (node is JsonArray arrayNode)
        {
            var resolvedArray = new JsonArray();
            foreach (var value in arrayNode)
                resolvedArray.Add(ResolveVariants(value?.DeepClone(), profile));
            return resolvedArray;
        }

        return node?.DeepClone();
    }

    private static JsonNode SelectVariant(JsonObject variants, ContentVariantProfile profile)
    {
        var allowed = new HashSet<string>(["default", "deadly", "tough", "deadlyTough"], StringComparer.Ordinal);
        foreach (var key in variants.Select(property => property.Key))
            if (!allowed.Contains(key)) throw new JsonException($"Unknown difficulty variant '{key}'.");

        var keys = profile switch
        {
            { DeadlyEnemies: true, ToughEnemies: true } => new[] { "deadlyTough", "deadly", "tough", "default" },
            { DeadlyEnemies: true } => ["deadly", "default"],
            { ToughEnemies: true } => ["tough", "default"],
            _ => ["default"]
        };
        foreach (var key in keys)
            if (variants[key] is { } value) return value;
        throw new JsonException($"No difficulty variant matches profile (deadly={profile.DeadlyEnemies}, tough={profile.ToughEnemies}).");
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
