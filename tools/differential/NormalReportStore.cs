using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Differential;

// Repeated history prefixes share immutable, compressed chunks. Comparison still uses full values.
internal sealed class NormalReportStore : IDisposable
{
    private const int ChunkSize = 64;
    private readonly ZipArchive archive;
    private readonly HashSet<string> written = new(StringComparer.Ordinal);
    internal static readonly JsonSerializerOptions Options = new()
    {
        IncludeFields = true, PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    internal NormalReportStore(string path) => archive = ZipFile.Open(path, ZipArchiveMode.Create);

    internal JsonNode? Pack(object? value) => PackElement(JsonSerializer.SerializeToElement(value, Options));

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
