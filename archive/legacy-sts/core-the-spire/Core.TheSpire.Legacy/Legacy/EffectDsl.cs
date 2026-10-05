using System.Text.Json;

namespace CoreTheSpire;

public static class EffectDsl
{
    private static readonly JsonSerializerOptions Options = DslJson.CreateOptions();

    public static EffectNode Parse(string json) =>
        JsonSerializer.Deserialize<EffectNode>(json, Options)
        ?? throw new JsonException("Effect JSON produced no effect node.");

    public static RunEffectNode ParseRun(string json) =>
        JsonSerializer.Deserialize<RunEffectNode>(json, Options)
        ?? throw new JsonException("Run effect JSON produced no effect node.");

    public static EffectNode Parse(JsonElement json) => Parse(json.GetRawText());

    public static string Serialize(EffectNode effect) => JsonSerializer.Serialize(effect, Options);
    public static string Serialize(RunEffectNode effect) => JsonSerializer.Serialize(effect, Options);

}
