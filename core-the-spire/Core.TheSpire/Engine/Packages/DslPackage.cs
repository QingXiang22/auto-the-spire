using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CoreTheSpire.Engine;

/// <summary>A text-only rule package. No game rules or executable JSON are supplied by the loader.</summary>
public sealed class DslPackage
{
    public string Root { get; }
    public string Id { get; }
    public string Version { get; }
    public string Scope { get; }
    public string EntryPath { get; }
    public string PublicMetadataPath { get; }
    public string RandomProfile { get; }
    public string Digest { get; }
    public IReadOnlyList<string> CatalogPaths { get; }
    public RuleSet Rules { get; }

    private DslPackage(string manifestPath, string? requestedScope)
    {
        Root = Path.GetDirectoryName(Path.GetFullPath(manifestPath))!;
        using var document = Read(manifestPath);
        var manifest = document.RootElement;
        Keys(manifest, "format", "id", "version", "language", "entry", "public_metadata", "default_scope",
            "supported_scopes", "unsupported_scopes", "catalogs", "random_profile");
        if (Text(manifest, "format") != "engine-dsl/package@1" || Text(manifest, "language") != EngineDslCompiler.LanguageVersion)
            throw new FormatException("Unsupported DSL package format or language.");
        Id = Text(manifest, "id"); Version = Text(manifest, "version");
        var scopes = Strings(manifest.GetProperty("supported_scopes"));
        var unsupported = manifest.GetProperty("unsupported_scopes");
        if (unsupported.ValueKind != JsonValueKind.Object) throw new FormatException("unsupported_scopes must map names to reasons.");
        foreach (var reason in unsupported.EnumerateObject())
            if (reason.Value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(reason.Value.GetString()) || scopes.Contains(reason.Name))
                throw new FormatException("Invalid or contradictory unsupported scope.");
        var defaultScope = Text(manifest, "default_scope");
        if (!scopes.Contains(defaultScope)) throw new FormatException("The default scope must be supported.");
        Scope = requestedScope ?? defaultScope;
        if (!scopes.Contains(Scope))
            throw new NotSupportedException($"unsupported_capability: scope '{Scope}': " +
                (unsupported.TryGetProperty(Scope, out var why) ? why.GetString() : "This scope is not implemented by the DSL package."));
        RandomProfile = Text(manifest, "random_profile");
        if (RandomProfile != "engine-default") throw new NotSupportedException("unsupported_capability: package random profile is not implemented.");
        EntryPath = Resolve(Text(manifest, "entry"));
        PublicMetadataPath = Resolve(Text(manifest, "public_metadata"));
        CatalogPaths = Strings(manifest.GetProperty("catalogs")).Select(Resolve).ToArray();
        if (Path.GetExtension(EntryPath) != ".spire" || Path.GetExtension(PublicMetadataPath) != ".json"
            || CatalogPaths.Any(p => Path.GetExtension(p) != ".json")) throw new FormatException("Invalid package file roles.");
        var dataPaths = new HashSet<string>(CatalogPaths, PathComparer) { Path.GetFullPath(manifestPath), PublicMetadataPath };
        var all = Files(Root).Order(StringComparer.Ordinal).ToArray();
        foreach (var path in all)
        {
            if (Path.GetExtension(path) is not (".spire" or ".json")) throw new FormatException($"Non-text package file: {path}");
            if (Path.GetExtension(path) == ".json")
            {
                if (!dataPaths.Contains(path)) throw new FormatException($"Undeclared data file: {path}");
                using var json = Read(path);
                StaticData(json.RootElement, path);
            }
        }
        var referenced = new HashSet<string>(PathComparer);
        Visit(EntryPath, referenced);
        var unused = all.Where(p => p.EndsWith(".spire", StringComparison.Ordinal) && !referenced.Contains(p)).ToArray();
        if (unused.Length > 0) throw new FormatException("Unreferenced rule files: " + string.Join(", ", unused));
        foreach (var catalog in CatalogPaths) ValidateCatalog(catalog, referenced);
        // The transport owns its metadata schema. Package loading only enforces static, declared JSON.
        Rules = EngineDslCompiler.LoadFiles(EntryPath);
        if (!Rules.Packages.Any(p => p.PackageId == Id && p.Version == Version)) throw new FormatException("Manifest identity does not match the compiled source.");
        var material = all.Select(p => Path.GetRelativePath(Root, p).Replace('\\', '/') + "\n" + File.ReadAllText(p).Replace("\r\n", "\n"));
        Digest = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", material))));
    }

    public static DslPackage Load(string manifestPath, string? scope = null) => new(manifestPath, scope);

    private string Resolve(string relative)
    {
        if (Path.IsPathRooted(relative)) throw new FormatException("Package references must be relative.");
        var full = Path.GetFullPath(relative, Root);
        if (!full.StartsWith(Root + Path.DirectorySeparatorChar, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new FormatException($"Package reference escapes its root: {relative}");
        for (var part = full; part != Root; part = Path.GetDirectoryName(part)!)
            if ((File.GetAttributes(part) & FileAttributes.ReparsePoint) != 0) throw new FormatException("Package references cannot traverse links.");
        if (!File.Exists(full)) throw new FileNotFoundException("Package file is missing.", full);
        return full;
    }

    private void Visit(string path, HashSet<string> visited)
    {
        if (!visited.Add(path)) return;
        var source = EngineDslParser.Parse(File.ReadAllText(path), path);
        foreach (var property in source.Members.OfType<DslPropertyNode>().Where(p => p.Name is "includes" or "imports"))
        {
            if (property.Value is not DslListNode list) throw new FormatException($"{property.Location}: source references must be lists.");
            foreach (var item in list.Items)
            {
                if (item is not DslStringNode reference) throw new FormatException($"{item.Location}: file reference must be a string.");
                var relative = Path.GetRelativePath(Root, Path.GetFullPath(reference.Value, Path.GetDirectoryName(path)!));
                var next = Resolve(relative);
                if (Path.GetExtension(next) != ".spire") throw new FormatException("Rule references must target DSL files.");
                Visit(next, visited);
            }
        }
    }

    private void ValidateCatalog(string path, HashSet<string> referenced)
    {
        using var document = Read(path);
        var catalog = document.RootElement;
        if (Text(catalog, "format") == "engine-dsl/source-data@1")
        {
            Keys(catalog, "format", "kind", "source_path", "source_sha256", "data");
            Text(catalog, "kind"); Text(catalog, "source_path");
            var sourceHash = Text(catalog, "source_sha256");
            if (sourceHash.Length != 64 || sourceHash.Any(c => !char.IsAsciiHexDigit(c))
                || catalog.GetProperty("data").ValueKind is not (JsonValueKind.Object or JsonValueKind.Array))
                throw new FormatException("Invalid static source snapshot.");
            return;
        }
        Keys(catalog, "format", "kind", "source_baseline", "entries");
        if (Text(catalog, "format") != "engine-dsl/source-catalog@1") throw new FormatException("Unsupported source catalog.");
        Text(catalog, "kind"); Text(catalog, "source_baseline");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in catalog.GetProperty("entries").EnumerateArray())
        {
            Keys(row, "id", "name", "source_path", "source_sha256", "pools", "eligibility", "evidence", "definition", "members");
            if (!ids.Add(Text(row, "id"))) throw new FormatException($"Duplicate catalog ID in {path}.");
            Text(row, "name"); Text(row, "source_path"); Text(row, "eligibility");
            var hash = Text(row, "source_sha256");
            if (hash.Length != 64 || hash.Any(c => !char.IsAsciiHexDigit(c))) throw new FormatException("Source hash must be SHA256.");
            Strings(row.GetProperty("pools"));
            if (row.GetProperty("evidence").ValueKind != JsonValueKind.String) throw new FormatException("Evidence must be a static string.");
            if (row.TryGetProperty("members", out var members)) Strings(members, false);
            var definition = row.GetProperty("definition");
            if (definition.ValueKind != JsonValueKind.Null)
            {
                if (definition.ValueKind != JsonValueKind.String) throw new FormatException("A migrated definition must reference a loaded DSL file.");
                var contentPath = Resolve(definition.GetString()!);
                if (!referenced.Contains(contentPath)) throw new FormatException("A migrated definition must reference a loaded DSL file.");
                var content = EngineDslParser.Parse(File.ReadAllText(contentPath), contentPath);
                if (!content.Members.OfType<DslDeclarationNode>().Any()
                    || content.Members.Any(member => member is not DslPropertyNode { Name: "dsl_version" } and not DslDeclarationNode { Kind: "use" }))
                    throw new FormatException($"{contentPath}: catalog definitions allow only DSL template uses; put shared declarations in rule files.");
            }
        }
    }

    private static IEnumerable<string> Files(string directory)
    {
        foreach (var path in Directory.EnumerateFileSystemEntries(directory))
        {
            var attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReparsePoint) != 0) throw new FormatException($"Package links are not allowed: {path}");
            if ((attributes & FileAttributes.Directory) != 0)
                foreach (var child in Files(path)) yield return child;
            else yield return Path.GetFullPath(path);
        }
    }

    private static JsonDocument Read(string path)
    {
        var document = JsonDocument.Parse(File.ReadAllText(path));
        try { UniqueKeys(document.RootElement); return document; }
        catch { document.Dispose(); throw; }
    }

    private static void UniqueKeys(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var member in value.EnumerateObject())
            { if (!names.Add(member.Name)) throw new FormatException($"Duplicate JSON key: {member.Name}"); UniqueKeys(member.Value); }
        }
        else if (value.ValueKind == JsonValueKind.Array) foreach (var item in value.EnumerateArray()) UniqueKeys(item);
    }

    private static void StaticData(JsonElement value, string path)
    {
        if (value.ValueKind == JsonValueKind.Object)
            foreach (var member in value.EnumerateObject())
            {
                if (member.Name.ToLowerInvariant() is "effects" or "conditions" or "hooks" or "script" or "code" or "body" or "dsl")
                    throw new FormatException($"Executable JSON is not allowed: {path}:{member.Name}");
                StaticData(member.Value, path);
            }
        else if (value.ValueKind == JsonValueKind.Array) foreach (var item in value.EnumerateArray()) StaticData(item, path);
    }

    private static void Keys(JsonElement value, params string[] allowed)
    {
        if (value.ValueKind != JsonValueKind.Object || value.EnumerateObject().Any(p => !allowed.Contains(p.Name, StringComparer.Ordinal)))
            throw new FormatException("Unknown field or invalid JSON object shape.");
    }
    private static string Text(JsonElement value, string key) => value.GetProperty(key) is { ValueKind: JsonValueKind.String } text
        && !string.IsNullOrWhiteSpace(text.GetString()) ? text.GetString()! : throw new FormatException($"{key} must be a nonempty string.");
    private static string[] Strings(JsonElement value, bool unique = true)
    {
        if (value.ValueKind != JsonValueKind.Array) throw new FormatException("Expected a string list.");
        var rows = value.EnumerateArray().Select(v => v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString())
            ? v.GetString()! : throw new FormatException("Expected a nonempty string.")).ToArray();
        if (unique && rows.Distinct(StringComparer.Ordinal).Count() != rows.Length) throw new FormatException("Duplicate list members.");
        return rows;
    }
    private static StringComparer PathComparer => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
}
