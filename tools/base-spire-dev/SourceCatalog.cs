using System.Text.Json;
using CoreTheSpire.Engine;

// Frozen source facts are a tool dependency, outside the production package and its digest.
internal static class SourceCatalog
{
    private static readonly Dictionary<string, int> Counts = new()
    {
        ["cards"] = 578, ["powers"] = 260, ["relics"] = 297, ["potions"] = 64,
        ["enemies"] = 121, ["events"] = 68, ["characters"] = 8,
        ["encounters"] = 88, ["run"] = 5, ["pools"] = 27
    };
    private static readonly JsonElement Domains = Load();
    private static JsonElement Load()
    {
        using var stream = typeof(SourceCatalog).Assembly.GetManifestResourceStream("BaseSpire.Dev.fixtures.source-catalogs.json")!;
        using var document = JsonDocument.Parse(stream);
        if (document.RootElement.GetProperty("format").GetString() != "base-spire/source-catalogs@1")
            throw new FormatException("Unsupported source fixture.");
        return document.RootElement.GetProperty("domains").Clone();
    }
    public static JsonElement[] Entries(string domain) => Domains.GetProperty(domain).GetProperty("entries").EnumerateArray().ToArray();
    public static void Check(DslPackage package)
    {
        if (!Counts.Keys.Order().SequenceEqual(Domains.EnumerateObject().Select(p => p.Name).Order()))
            throw new FormatException("Frozen source domains changed.");
        foreach (var (domain, count) in Counts)
        {
            var rows = Entries(domain);
            if (rows.Length != count || rows.Select(r => r.GetProperty("id").GetString()).Distinct().Count() != count)
                throw new FormatException($"Frozen {domain} source denominator must remain {count}.");
            foreach (var row in rows)
            {
                if (row.EnumerateObject().Any(p => p.Name is not ("id" or "name" or "source_path" or "source_sha256" or "pools" or "members" or "exclusion")))
                    throw new FormatException("Source fixtures contain only frozen source facts.");
                var hash = row.GetProperty("source_sha256").GetString()!;
                if (hash.Length != 64 || hash.Any(c => !char.IsAsciiHexDigit(c))) throw new FormatException("Invalid frozen source SHA256.");
            }
        }
        foreach (var path in package.CatalogPaths)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var catalog = document.RootElement;
            var source = Domains.EnumerateObject().Single(d => d.Value.GetProperty("kind").GetString() == catalog.GetProperty("kind").GetString()).Value;
            var byId = source.GetProperty("entries").EnumerateArray().ToDictionary(r => r.GetProperty("id").GetString()!);
            foreach (var row in catalog.GetProperty("entries").EnumerateArray())
                if (!byId.TryGetValue(row.GetProperty("id").GetString()!, out var fact) || fact.TryGetProperty("exclusion", out _))
                    throw new FormatException("A runtime definition must belong to the applicable frozen source roster.");
        }
    }
}
