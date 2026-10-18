using System.Text.Json;
using CoreTheSpire.Engine;
using NetTheSpire.Host;

try
{
    var command = args.FirstOrDefault() ?? "describe";
    if (command is not ("validate" or "describe" or "check"))
    {
        Console.Error.WriteLine("unsupported_capability: legacy card probes were retired; use validate/describe <package.json> [--scope id].");
        return 3;
    }
    var manifest = "mod-the-spire/base-spire/package.json";
    string? scope = null, report = null;
    var ironcladOnly = false;
    for (var index = 1; index < args.Length; index++)
    {
        string Input() => index + 1 < args.Length ? args[++index] : throw new ArgumentException("Missing option value.");
        if (args[index] == "--ironclad-only") ironcladOnly = true;
        else if (args[index] == "--scope") scope = Input();
        else if (args[index] == "--report") report = Input();
        else if (index == 1 && !args[index].StartsWith("--", StringComparison.Ordinal)) manifest = args[index];
        else throw new ArgumentException("Unknown option: " + args[index]);
    }
    if (Path.GetExtension(manifest) != ".json") throw new NotSupportedException("unsupported_capability: the DSL package tool requires a package.json manifest; legacy card probes are retired.");
    var package = DslPackage.Load(manifest, scope);
    var metadata = TextRuleProviderSpec.Load(package.PublicMetadataPath);
    if (metadata.ScenarioProfile != package.Scope) throw new FormatException("Package and public scope disagree.");
    if (ironcladOnly && (command != "check" || report is not null)) throw new ArgumentException("--ironclad-only requires check without --report; run the full check to produce coverage evidence.");
    if (command == "check") return ironcladOnly ? PackageChecks.RunIroncladOnly(package) : PackageChecks.Run(manifest, report);
    if (command == "validate")
    {
        var session = new Session(package.Rules, 42, ["player"]);
        session.Start();
        if (session.Status == SessionStatus.Faulted) throw new InvalidOperationException(session.Fault?.ToString());
    }
    var catalogs = package.CatalogPaths.Select(path =>
    {
        using var data = JsonDocument.Parse(File.ReadAllText(path));
        var entries = data.RootElement.TryGetProperty("entries", out var rows) ? rows.EnumerateArray().ToArray() : [];
        return new { kind = data.RootElement.GetProperty("kind").GetString(),
            source_count = entries.Length,
            migrated_count = entries.Count(row => row.GetProperty("definition").ValueKind == JsonValueKind.String) };
    }).ToArray();
    Console.WriteLine(JsonSerializer.Serialize(new { package.Id, package.Version, package.Scope, package.RandomProfile,
        package.Digest, ruleset = package.Rules.Identity, entry = package.EntryPath, catalogs }));
    return 0;
}
catch (NotSupportedException error) { Console.Error.WriteLine(error.Message); return 3; }
catch (Exception error) when (error is FormatException or ArgumentException or IOException or KeyNotFoundException or InvalidOperationException)
{ Console.Error.WriteLine(error.Message); return 2; }
