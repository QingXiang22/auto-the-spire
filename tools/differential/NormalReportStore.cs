using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CoreTheSpire.Engine;

namespace Differential;

// Repeated history prefixes share immutable, compressed chunks. Comparison still uses full values.
internal sealed class NormalReportStore : IDisposable
{
    private const int ChunkSize = 64;
    private readonly ZipArchive archive;
    private readonly HashSet<string> written = new(StringComparer.Ordinal);
    private readonly JsonSerializerOptions historyOptions;
    internal static readonly JsonSerializerOptions Options = new()
    {
        IncludeFields = true, PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    internal NormalReportStore(string path)
    {
        archive = ZipFile.Open(path, ZipArchiveMode.Create);
        historyOptions = new(Options);
        historyOptions.Converters.Add(new HistoryConverter<SessionDebugRecord>(this));
#if REFERENCE_SIM
        historyOptions.Converters.Add(new HistoryConverter<NormalNativeHookRecord>(this));
        historyOptions.Converters.Add(new HistoryConverter<NormalHookMethod>(this));
#endif
    }

    internal JsonNode? Pack(object? value) => PackElement(JsonSerializer.SerializeToElement(value, historyOptions));

    // These arrays contain copied, append-only diagnostic records. Reuse a stored
    // segment only while every record object in it is unchanged. The remaining
    // partial segment is saved with its actual length at each boundary.
    private sealed class HistoryConverter<T>(NormalReportStore store) : JsonConverter<T[]> where T : class
    {
        private readonly Dictionary<T, (T[] Records, JsonNode Reference)> chunks = new(ReferenceEqualityComparer.Instance);
        public override T[] Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) => throw new NotSupportedException();
        public override void Write(Utf8JsonWriter writer, T[] value, JsonSerializerOptions options)
        {
            if (value.Length <= ChunkSize) { JsonSerializer.Serialize(writer, value, Options); return; }
            writer.WriteStartObject(); writer.WritePropertyName("$chunks"); writer.WriteStartArray();
            foreach (var chunk in value.Chunk(ChunkSize))
            {
                if (!chunks.TryGetValue(chunk[0], out var cached) || cached.Records.Length != chunk.Length
                    || chunk.Where((record, index) => !ReferenceEquals(record, cached.Records[index])).Any())
                {
                    cached = (chunk, store.Store(JsonSerializer.SerializeToElement(chunk, Options)));
                    chunks[chunk[0]] = cached;
                }
                cached.Reference.WriteTo(writer);
            }
            writer.WriteEndArray(); writer.WriteNumber("$count", value.Length); writer.WriteEndObject();
        }
    }

    private JsonNode? PackElement(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            // A diagnostic may already have been packed at its execution boundary.
            if (value.TryGetProperty("$artifact", out _) || value.TryGetProperty("$chunks", out _))
                return JsonNode.Parse(value.GetRawText());
            var result = new JsonObject();
            foreach (var property in value.EnumerateObject()) result[property.Name] = PackElement(property.Value);
            return result;
        }
        if (value.ValueKind == JsonValueKind.Array)
        {
            var items = value.EnumerateArray().ToArray();
            if (items.Length <= ChunkSize) return new JsonArray(items.Select(PackElement).ToArray());
            var chunks = new JsonArray();
            foreach (var chunk in items.Chunk(ChunkSize)) chunks.Add(Store(JsonSerializer.SerializeToElement(chunk)));
            return new JsonObject { ["$chunks"] = chunks, ["$count"] = items.Length };
        }
        if (value.ValueKind == JsonValueKind.String && value.GetString()!.Length > 2048) return Store(value);
        return JsonNode.Parse(value.GetRawText());
    }

    private JsonObject Store(JsonElement value)
    {
        var bytes = Encoding.UTF8.GetBytes(value.GetRawText());
        var name = Convert.ToHexStringLower(SHA256.HashData(bytes)) + ".json";
        if (written.Add(name))
        {
            using var output = archive.CreateEntry(name, CompressionLevel.Optimal).Open();
            output.Write(bytes);
        }
        return new JsonObject { ["$artifact"] = name };
    }

    public void Dispose() => archive.Dispose();
}
