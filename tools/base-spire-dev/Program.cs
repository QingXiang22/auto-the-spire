using System.Text.Json;
using CoreTheSpire.Engine;
using NetTheSpire.Host;

try
{
    var command = args.FirstOrDefault() ?? "describe";
    if (command is not ("validate" or "describe" or "check" or "sync-content"))
    {
        Console.Error.WriteLine("unsupported_capability: legacy card probes were retired; use validate/describe <package.json> [--scope id].");
        return 3;
    }
    var manifest = "mod-the-spire/base-spire/package.json";
    string? scope = null, report = null, filter = null;
    var ironcladOnly = false;
    var cardRewardsOnly = false;
    var rngOnly = false;
    var relicContentOnly = false;
    var contentModelOnly = false;
    var cardBehaviorOnly = false;
    var enemyMovesOnly = false;
    for (var index = 1; index < args.Length; index++)
    {
        string Input() => index + 1 < args.Length ? args[++index] : throw new ArgumentException("Missing option value.");
        if (args[index] == "--ironclad-only") ironcladOnly = true;
        else if (args[index] == "--enemy-moves-only") enemyMovesOnly = true;
        else if (args[index] == "--card-behavior-only") cardBehaviorOnly = true;
        else if (args[index] == "--content-model-only") contentModelOnly = true;
        else if (args[index] == "--relic-content-only") relicContentOnly = true;
        else if (args[index] == "--rng-only") rngOnly = true;
        else if (args[index] == "--card-rewards-only") cardRewardsOnly = true;
        else if (args[index] == "--scope") scope = Input();
        else if (args[index] == "--report") report = Input();
        else if (args[index] == "--filter") filter = Input();
        else if (index == 1 && !args[index].StartsWith("--", StringComparison.Ordinal)) manifest = args[index];
        else throw new ArgumentException("Unknown option: " + args[index]);
    }
    if (filter is not null && (string.IsNullOrWhiteSpace(filter) || command != "check" || !(relicContentOnly ^ cardBehaviorOnly) || report is not null || ironcladOnly || cardRewardsOnly || rngOnly || contentModelOnly || enemyMovesOnly))
        throw new ArgumentException("--filter requires a nonempty name and check --relic-content-only or --card-behavior-only without other focused flags or --report.");
    if (Path.GetExtension(manifest) != ".json") throw new NotSupportedException("unsupported_capability: the DSL package tool requires a package.json manifest; legacy card probes are retired.");
    var package = DslPackage.Load(manifest, scope);
    if (command == "sync-content")
    {
        if (report is not null || filter is not null || ironcladOnly || cardRewardsOnly || rngOnly || relicContentOnly || contentModelOnly || cardBehaviorOnly || enemyMovesOnly)
            throw new ArgumentException("sync-content accepts only the package manifest and optional --scope.");
        ContentRegistration.Check(package, synchronize: true);
        Console.WriteLine("Derived Card/Power public IDs from executable definition libraries and explicit startup fixtures; source catalogs and visibility policies unchanged.");
        return 0;
    }
    if (command is "validate" or "check") ContentRegistration.Check(package);
    var metadata = TextRuleProviderSpec.Load(package.PublicMetadataPath);
    if (enemyMovesOnly)
    {
        if (command != "check" || report is not null || filter is not null || ironcladOnly || cardRewardsOnly || rngOnly || relicContentOnly || contentModelOnly || cardBehaviorOnly)
            throw new ArgumentException("--enemy-moves-only requires check without other focused flags or report.");
        if (metadata.ScenarioProfile != package.Scope) throw new FormatException("Package and public scope disagree.");
        return PackageChecks.RunEnemyMovesOnly(package);
    }
    if (cardBehaviorOnly)
    {
        if (command != "check" || report is not null || ironcladOnly || cardRewardsOnly || rngOnly || relicContentOnly || contentModelOnly)
            throw new ArgumentException("--card-behavior-only requires check without other focused flags or report.");
        return PackageChecks.RunCardBehaviorOnly(package, filter);
    }
    if (metadata.ScenarioProfile != package.Scope) throw new FormatException("Package and public scope disagree.");
    if (contentModelOnly)
    {
        if (command != "check" || report is not null || filter is not null || ironcladOnly || cardRewardsOnly || rngOnly || relicContentOnly)
            throw new ArgumentException("--content-model-only requires check without other focused flags or report.");
        return PackageChecks.RunContentModelOnly(package);
    }
    if (ironcladOnly && (command != "check" || report is not null)) throw new ArgumentException("--ironclad-only requires check without --report; run the full check to produce coverage evidence.");
    if (cardRewardsOnly && (command != "check" || report is not null || ironcladOnly)) throw new ArgumentException("--card-rewards-only requires check without --report or --ironclad-only.");
    if (command == "check" && cardRewardsOnly) return PackageChecks.RunCardRewardsOnly(package);
    if (rngOnly && (command != "check" || report is not null || cardRewardsOnly || ironcladOnly)) throw new ArgumentException("--rng-only requires check without other focused flags or report.");
    if (command == "check" && rngOnly) return PackageChecks.RunSourceRandomOnly(package);
    if (relicContentOnly && (command != "check" || report is not null || ironcladOnly || rngOnly || cardRewardsOnly)) throw new ArgumentException("--relic-content-only requires check without other focused flags or report.");
    if (command == "check" && relicContentOnly) return PackageChecks.RunRelicContentOnly(package, filter);
    if (command == "check") return ironcladOnly ? PackageChecks.RunIroncladOnly(package) : PackageChecks.Run(manifest, report);
    if (command == "validate")
    {
        var session = package.CreateSession(42, ["player"]);
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
