using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Differential;

var jsonOptions = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
try
{
    if (args.FirstOrDefault() == "damage-compare") return DamageComparison.Run(Repository.Find(), args.Skip(1).ToArray());
    if (args.FirstOrDefault() == "enemy-birth-compare") return EnemyBirthComparison.Run(Repository.Find(), args.Skip(1).ToArray());
    if (args.FirstOrDefault() == "combat-boundary-compare") return CombatBoundaryComparison.Run(Repository.Find(), args.Skip(1).ToArray());
    if (args.SequenceEqual(new[] { "combat-boundary-self-test" })) { CombatBoundaryComparison.SelfTest(); return 0; }
    if (args.FirstOrDefault() == "battle-friend-compare") return BattleFriendComparison.Run(Repository.Find(), args.Skip(1).ToArray());
    if (args.FirstOrDefault() == "wriggler-compare") return WrigglerComparison.Run(Repository.Find(), args.Skip(1).ToArray());
    if (args.FirstOrDefault() == "whistle-compare") return WhistleComparison.Run(Repository.Find(), args.Skip(1).ToArray());
    if (args.FirstOrDefault() == "enemy-rotation-compare") return EnemyRotationComparison.Run(Repository.Find(), args.Skip(1).ToArray());
    if (args.FirstOrDefault() == "power-amount-compare") return PowerAmountComparison.Run(Repository.Find(), args.Skip(1).ToArray());
    if (args.FirstOrDefault() == "block-compare") return BlockComparison.Run(Repository.Find(), args.Skip(1).ToArray());
    if (args.FirstOrDefault() == "turn-start-compare") return TurnStartComparison.Run(Repository.Find(), args.Skip(1).ToArray());
    if (args.FirstOrDefault() == "turn-phase-compare") return TurnPhaseComparison.Run(Repository.Find(), args.Skip(1).ToArray());
    if (args.FirstOrDefault() == "exhaust-cause-compare") return ExhaustCauseComparison.Run(Repository.Find(), args.Skip(1).ToArray());
    if (args.FirstOrDefault() == "unknown-room-compare") return UnknownRoomComparison.Run(Repository.Find(), args.Skip(1).ToArray());
    if (args.FirstOrDefault() == "event-selection-compare") return EventSelectionComparison.Run(Repository.Find(), args.Skip(1).ToArray());
    if (args.FirstOrDefault() == "normal-compare") return NormalRunComparison.Run(Repository.Find(), args.Skip(1).ToArray());
    if (args.SequenceEqual(new[] { "normal-compare-self-test" })) { NormalRunChecks.Run(Repository.Find()); return 0; }
    if (args.FirstOrDefault() == "normal-preflight") return NormalPreflight.Run(Repository.Find(), args.Skip(1).ToArray());
    if (args.SequenceEqual(new[] { "normal-preflight-self-test" })) { NormalPreflightChecks.Run(); return 0; }
    if (args.SequenceEqual(new[] { "self-test" })) return ComparisonChecks.Run(Repository.Find());
    string? Option(string name)
    {
        var index = Array.IndexOf(args, name);
        if (index < 0) return null;
        if (index + 1 == args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            throw new FormatException($"Missing value for {name}.");
        return args[index + 1];
    }
    if (args.Length == 0 || args[0] != "test") throw new FormatException("Use test --suite NAME [--case ID] [--output DIRECTORY].");
    var allowedOptions = new[] { "--suite", "--case", "--output", "--suite-file" };
    if (args.Length % 2 != 1 || args.Skip(1).Where((_, index) => index % 2 == 0).Any(v => !allowedOptions.Contains(v))
        || args.Skip(1).Where((_, index) => index % 2 == 0).GroupBy(v => v).Any(g => g.Count() > 1))
        throw new FormatException("Unknown, repeated or incomplete test option.");
    var suite = Option("--suite") ?? "regression";
    if (Path.GetFileName(suite) != suite) throw new FormatException("Invalid suite name.");
    var canonicalPath = Path.Combine(AppContext.BaseDirectory, "suites", suite + ".json");
    var customSuite = Option("--suite-file");
    var suitePath = customSuite ?? canonicalPath;
    using var document = JsonDocument.Parse(File.ReadAllText(suitePath));
    var root = document.RootElement;
    if (root.GetProperty("suite").GetString() != suite) throw new FormatException("Suite name does not match its definition.");
    var ids = root.GetProperty("expected_case_ids").EnumerateArray().Select(v => v.GetString()!).ToArray();
    var cases = root.GetProperty("cases").EnumerateArray().ToArray();
    var caseIds = cases.Select(c => c.GetProperty("id").GetString()!).ToArray();
    if (ids.Length == 0 || ids.Any(string.IsNullOrWhiteSpace) || ids.Distinct().Count() != ids.Length
        || caseIds.Distinct().Count() != caseIds.Length || !ids.Order().SequenceEqual(caseIds.Order()))
        throw new FormatException("Suite requires nonempty, unique, matching expected_case_ids and cases.");
    using var canonical = JsonDocument.Parse(File.ReadAllText(canonicalPath));
    if (!ids.SequenceEqual(canonical.RootElement.GetProperty("expected_case_ids").EnumerateArray().Select(v => v.GetString())))
        throw new FormatException("Custom input cannot change the original suite's required case IDs.");
    var selected = Option("--case");
    if (selected is not null && !ids.Contains(selected)) throw new FormatException($"Unknown case '{selected}'.");
    var repository = Repository.Find();
    var results = new List<CaseResult>();
    foreach (var id in ids.Where(id => selected is null || selected == id))
    {
        if (suite != "regression" || id is not ("strike-normal" or "random-reshuffle-unsupported"))
        {
            results.Add(new(id, "unsupported", "The original case has no migrated DSL adapter."));
            continue;
        }
        try
        {
            var fixture = CombatFixture.Read(cases.Single(c => c.GetProperty("id").GetString() == id));
            var referenceIdentity = Repository.VerifyReference(repository);
            var left = DslBackend.Run(repository, fixture);
            var right = ReferenceBackend.Run(fixture);
            results.Add(Comparison.Compare(fixture, left, right) with
            {
                ReferenceIdentity = new { checkout = referenceIdentity,
                    adapter_sha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(typeof(CombatFixture).Assembly.Location))) },
                Input = fixture,
            });
        }
        catch (Exception error)
        {
            results.Add(new(id, "blocked_compare", error.GetType().Name + ": " + error.Message));
        }
    }
    var passed = results.All(r => r.Status == "passed");
    var complete = selected is null && customSuite is null;
    var report = new
    {
        schema = "dsl-migration/differential@2", suite,
        suite_definition_sha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(File.ReadAllText(suitePath).Replace("\r\n", "\n")))),
        status = passed ? complete ? "passed" : "partial" : results.Any(r => r.Status == "diverged") ? "diverged" : "blocked_compare",
        complete, independent_verified = passed && complete,
        executed = results.Count(r => r.Left is not null && r.Right is not null),
        passed = results.Count(r => r.Status == "passed"), expected_case_ids = ids,
        selected_case_ids = results.Select(r => r.Id), cases = results,
    };
    var json = JsonSerializer.Serialize(report, jsonOptions);
    if (Option("--output") is { } output)
    {
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "result.json"), json);
    }
    Console.WriteLine(json);
    return passed ? complete ? 0 : 1 : results.Any(r => r.Status is not ("passed" or "diverged")) ? 3 : 1;
}
catch (Exception error)
{
    Console.Error.WriteLine($"comparison failed: {error.GetType().Name}: {error.Message}");
    return 3;
}
