using System.Text.Json;
using System.Text.Json.Serialization;

namespace CoreTheSpire;

public static class EffectDsl
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    public static EffectNode Parse(string json) =>
        JsonSerializer.Deserialize<EffectNode>(json, Options)
        ?? throw new JsonException("Effect JSON produced no effect node.");

    public static RunEffectNode ParseRun(string json) =>
        JsonSerializer.Deserialize<RunEffectNode>(json, Options)
        ?? throw new JsonException("Run effect JSON produced no effect node.");

    public static EffectNode Parse(JsonElement json) => Parse(json.GetRawText());

    public static string Serialize(EffectNode effect) => JsonSerializer.Serialize(effect, Options);
    public static string Serialize(RunEffectNode effect) => JsonSerializer.Serialize(effect, Options);

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        options.Converters.Add(new ContentIdJsonConverter());
        return options;
    }

    private sealed class ContentIdJsonConverter : JsonConverter<ContentId>
    {
        public override ContentId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            ContentId.Parse(reader.GetString() ?? throw new JsonException("Content ID cannot be null."));

        public override void Write(Utf8JsonWriter writer, ContentId value, JsonSerializerOptions options) => writer.WriteStringValue(value.Value);
    }
}
