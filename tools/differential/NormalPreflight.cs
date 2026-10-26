using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NetTheSpire.Contracts;
using NetTheSpire.Contracts.Dto;
using NetTheSpire.Host;

namespace Differential;

// This preflight deliberately cannot certify M01: it compares the first native normal boundary,
// and retains complete public DSL claim/skip trajectories for the subsequent two-sided adapter.
internal static class NormalPreflight
{
    internal static readonly string[] RequiredFacts = ["player.hp", "player.max_hp", "player.gold", "deck", "relics", "neow.options", "map"];
    internal sealed record Opening(string Backend, string Build, SortedDictionary<string, string> Facts, object? Diagnostic = null);
    internal sealed record Difference(string Path, string? Left, string? Right)
    {
        public string Boundary => "neow-ready";
        public string Dimension => Path == "neow.options" ? "decisions" : Path == "map" ? "map" : "facts";
    }
    internal sealed record Result(string Id, string Status, string? Reason, Opening? Left, Opening? Right,
        Difference? FirstDifference, string[] PublicActionPrefix, bool DslReturnedToMap)
    {
        public bool IndependentVerified => false;
        public bool Complete => false;
        public string[] UnverifiedDimensions => ["post-Neow paired actions/decisions", "visits", "permanent instance mapping", "piles", "per-call RNG", "events"];
    }

    public static int Run(string repository, string[] args)
    {
        if (args.Length != 2 || args[0] != "--output" || string.IsNullOrWhiteSpace(args[1]))
            throw new FormatException("Use normal-preflight --output DIRECTORY.");
        var output = Path.GetFullPath(args[1]); Directory.CreateDirectory(output);
        var inputPath = Path.Combine(repository, "tools/base-spire-dev/fixtures/normal-first-combat.json");
        using var input = JsonDocument.Parse(File.ReadAllText(inputPath));
        using var sourceCatalog = JsonDocument.Parse(File.ReadAllText(Path.Combine(repository,
            "tools/base-spire-dev/fixtures/source-catalogs.json")));
        var spec = input.RootElement;
        ValidateInput(spec);
        foreach (var path in new[] { "claim", "skip" }) File.Delete(Path.Combine(output, path + ".public.ndjson"));
        var results = new List<Result>();
        object? referenceIdentity = null; string? referenceError = null;
        try { referenceIdentity = Repository.VerifyReference(repository); }
        catch (Exception error) { referenceError = error.GetType().Name + ": " + error.Message; }
        foreach (var path in spec.GetProperty("reward_paths").EnumerateArray().Select(p => p.GetString()!))
        {
            Opening? left = null, right = null; var prefix = new List<string>(); var returned = false;
            try
            {
                if (referenceError is not null) throw new InvalidOperationException(referenceError);
                right = NormalReferenceOpening.Capture(spec.GetProperty("seed").GetString()!);
                using var host = new PublicRun(repository, Path.Combine(output, path + ".public.ndjson"));
                host.Start(spec.GetProperty("seed").GetString()!);
                host.Step(host.Action("StartNormalRun"), prefix);
                left = host.Opening();
                var difference = First(left, right);
                // Retain the production path independently. Never execute a different reference choice
                // after the native opening has diverged, or label this one-sided suffix a comparison.
                var relic = host.Snapshot.Entities.Single(e => e.KindId.EndsWith(":NeowOption", StringComparison.Ordinal)
                    && Field(e, "NeowOption.RelicId") == spec.GetProperty("neow_relic").GetString());
                host.Step(host.Snapshot.LegalActions.Single(a => a.Fields.Any(f => f.Value is WireString s && s.Value == relic.InstanceId)), prefix);
                host.Step(host.Snapshot.LegalActions.Single(a => a.Fields.Any(f => f.Value is WireBool b && b.Value)), prefix);
                host.Step(host.Snapshot.LegalActions.First(a => a.ActionType.EndsWith(":ChooseMapPoint", StringComparison.Ordinal)), prefix);
                for (var move = 0; host.Player("CombatOutcome") == "none" && move < spec.GetProperty("maximum_combat_actions").GetInt32(); move++)
                {
                    var next = spec.GetProperty("combat_action_priority").EnumerateArray()
                        .Select(a => host.Snapshot.LegalActions.FirstOrDefault(c => c.ActionType == "net:action:base-spire:" + a.GetString()))
                        .FirstOrDefault(a => a is not null) ?? throw new InvalidOperationException("Missing registered combat action.");
                    host.Step(next, prefix);
                }
                if (host.Player("CombatOutcome") != "victory" || host.Player("Room") != "rewards")
                    throw new InvalidOperationException("Normal public combat did not reach real victory rewards within its action limit.");
                if (path == "claim")
                {
                    host.Step(host.Action("ClaimDevelopmentGoldReward"), prefix);
                    host.Step(host.Action("ClaimDevelopmentCardReward"), prefix);
                    if (host.Find("ClaimDevelopmentPotionReward") is { } potion) host.Step(potion, prefix);
                }
                else
                {
                    host.Step(host.Action("SkipDevelopmentCardReward"), prefix);
                    if (host.Find("SkipDevelopmentPotionReward") is { } potion) host.Step(potion, prefix);
                }
                host.Step(host.Action("CloseDevelopmentRewards"), prefix);
                returned = host.Player("Room") == "map" && host.Find("ChooseMapPoint") is not null;
                if (!returned) throw new InvalidOperationException("Public rewards did not return to legal map successors.");
                results.Add(new("m01-" + path, difference is null ? "blocked_compare" : "diverged",
                    difference is null ? "Opening matches; paired post-Neow adapter and remaining comparison dimensions are not implemented." :
                    "Native normal openings differ. No normal RNG compatibility alignment or reference state injection was applied.",
                    left, right, difference, prefix.ToArray(), returned));
            }
            catch (Exception error)
            {
                results.Add(new("m01-" + path, "blocked_compare", error.GetType().Name + ": " + error.Message,
                    left, right, null, prefix.ToArray(), returned));
            }
        }
        var report = new
        {
            schema = "normal-run/preflight@1", status = results.Any(r => r.Status == "diverged") ? "diverged" : "blocked_compare",
            complete = false, independent_verified = false,
            expected_case_ids = new[] { "m01-claim", "m01-skip" },
            executed = results.Count(r => r.Left is not null && r.Right is not null),
            input = spec.Clone(), input_sha256 = Hash(inputPath), reference_identity = referenceIdentity,
            source_identity = new { target_rule_authority = referenceIdentity,
                legacy_catalog_source_baselines = sourceCatalog.RootElement.GetProperty("domains").EnumerateObject()
                    .Select(d => d.Value.GetProperty("source_baseline").GetString()).Distinct().ToArray(),
                current_rng_and_opening_authority = "reference/baselines/sts2-sim.json",
                production_manifest_sha256 = Hash(Path.Combine(repository, "mod-the-spire/base-spire/package.json")),
                opening_source_fixture_sha256 = Hash(Path.Combine(repository, "tools/base-spire-dev/fixtures/normal-opening-vectors.json")),
                production_rng_source_sha256 = Hash(Path.Combine(repository, "mod-the-spire/base-spire/run/random.spire")) },
            adapter_sha256 = Hash(typeof(NormalPreflight).Assembly.Location), cases = results,
            reproduction = "dotnet run --project tools/differential -c Release -- normal-preflight --output DIRECTORY"
        };
        File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(report,
            new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower }));
        foreach (var result in results) Console.WriteLine($"{result.Id}: {result.Status}; first={result.FirstDifference?.Path}; DSL map return={result.DslReturnedToMap}; {result.Reason}");
        return results.Any(r => r.Status == "blocked_compare") ? 3 : 1;
    }

    internal static Difference? First(Opening left, Opening right)
    {
        if (left.Backend != "dsl:public-stdio" || right.Backend != "reference:sts2-sim")
            throw new FormatException("Distinct public DSL and native reference backends are required.");
        foreach (var key in RequiredFacts)
            if (!left.Facts.ContainsKey(key) || !right.Facts.ContainsKey(key)) throw new FormatException("Missing opening dimension: " + key);
        foreach (var side in new[] { left, right })
            foreach (var (key, minimum, maximum) in new[] { ("deck", 10, 10), ("relics", 1, 99), ("neow.options", 3, 3), ("map", 1, 999) })
            {
                using var value = JsonDocument.Parse(side.Facts[key]);
                if (value.RootElement.ValueKind != JsonValueKind.Array || value.RootElement.GetArrayLength() < minimum || value.RootElement.GetArrayLength() > maximum)
                    throw new FormatException("Incomplete opening dimension: " + key);
            }
        foreach (var key in RequiredFacts)
            if (left.Facts[key] != right.Facts[key]) return new(key, left.Facts[key], right.Facts[key]);
        return null;
    }

    internal static void ValidateInput(JsonElement input)
    {
        if (input.GetProperty("format").GetString() != "normal-first-combat-input@1"
            || input.GetProperty("seed").GetString() != "777" || input.GetProperty("neow_relic").GetString() != "base-spire:booming-conch"
            || input.GetProperty("map_choice").GetString() != "first-legal-successor"
            || input.GetProperty("binding_choice").GetString() != "first-legal-binding"
            || !input.GetProperty("combat_action_priority").EnumerateArray().Select(v => v.GetString()).SequenceEqual(new[] { "PlayAttackFromHand", "PlayCardFromHand", "EndTurn" })
            || input.GetProperty("maximum_combat_actions").GetInt32() != 100
            || !input.GetProperty("reward_paths").EnumerateArray().Select(v => v.GetString()).SequenceEqual(new[] { "claim", "skip" }))
            throw new FormatException("Normal preflight requires the complete registered public policy and both reward paths.");
    }
    internal static string Hash(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
    internal static string Json<T>(T value) => JsonSerializer.Serialize(value);
    private static string Value(WireValue? value) => value is WireString s ? s.Value : value is null ? throw new FormatException("Unknown public value.") : WireCanonical.Text(value);
    private static string Field(EntityDto entity, string name) => Value(entity.Fields.Single(f => f.FieldId.EndsWith(":" + name, StringComparison.Ordinal)).Value);

    private sealed class PublicRun : IDisposable
    {
        private readonly Process process;
        private readonly Task<string> stderr;
        private readonly StreamWriter log;
        private int number;
        private string sessionId = "";
        private object? identity;
        public PublicSnapshotDto Snapshot { get; private set; } = null!;
        public PublicRun(string repository, string logPath)
        {
            var start = new ProcessStartInfo("dotnet") { WorkingDirectory = repository, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
            start.ArgumentList.Add(typeof(TextRuleProviderSpec).Assembly.Location);
            start.ArgumentList.Add("--package"); start.ArgumentList.Add(Path.Combine(repository, "mod-the-spire/base-spire/package.json"));
            process = Process.Start(start) ?? throw new InvalidOperationException("Public host failed to launch.");
            stderr = process.StandardError.ReadToEndAsync(); log = new StreamWriter(logPath, false, new UTF8Encoding(false));
        }
        private ResponseDto Send(Operation operation, params (string Key, WireValue Value)[] fields)
        {
            var request = new RequestDto("m01-" + ++number, operation, new WireObject(fields.Select(f => new KeyValuePair<string, WireValue>(f.Key, f.Value))));
            var requestText = WireCanonical.Text(request.ToWire()); log.WriteLine(requestText); log.Flush();
            process.StandardInput.WriteLine(requestText); process.StandardInput.Flush();
            var response = process.StandardOutput.ReadLineAsync();
            if (!response.Wait(60_000)) throw new TimeoutException("Public host did not reach a stable boundary within 60 seconds.");
            var text = response.Result ?? throw new IOException("Public host closed its output."); log.WriteLine(text); log.Flush();
            var result = ResponseDto.FromWire(WireParser.ParseUtf8Strict(Encoding.UTF8.GetBytes(text), "normal-preflight"));
            if (!result.Ok) throw new InvalidOperationException("Public host: " + result.Error?.Message);
            return result;
        }
        private static WireValue Get(ResponseDto response, string field) => ((WireObject)response.Result!).Get(field) ?? throw new FormatException("Missing response " + field);
        private void Observe() => Snapshot = PublicSnapshotDto.FromWire(Get(Send(Operation.Observe, ("session_id", WireValue.Str(sessionId))), "snapshot"), NetProtocol.V02);
        public void Start(string seed)
        {
            var describe = DescribeResultDto.FromWire(Send(Operation.Describe).Result!); identity = describe.Identity;
            var start = Send(Operation.Start, ("seed", WireValue.Str(seed)), ("expected_identity", describe.Identity.ToWire()),
                ("scenario_profile", WireValue.Str(describe.Capabilities.ScenarioProfiles.Single().Id)));
            sessionId = ((WireString)Get(start, "session_id")).Value;
            Snapshot = PublicSnapshotDto.FromWire(Get(start, "snapshot"), NetProtocol.V02);
        }
        public LegalActionDto? Find(string name) => Snapshot.LegalActions.FirstOrDefault(a => a.ActionType == "net:action:base-spire:" + name);
        public LegalActionDto Action(string name) => Find(name) ?? throw new InvalidOperationException("Missing public candidate " + name);
        public string Player(string field) => Field(Snapshot.Entities.Single(e => e.KindId.EndsWith(":Player", StringComparison.Ordinal)), "Player." + field);
        public void Step(LegalActionDto action, List<string> prefix)
        {
            object Binding(FieldDto field)
            {
                var value = Value(field.Value);
                var entity = Snapshot.Entities.SingleOrDefault(e => e.InstanceId == value);
                if (entity is null) return new { field = field.FieldId, value };
                if (entity.KindId.EndsWith(":NeowOption", StringComparison.Ordinal))
                    return new { field = field.FieldId, relic = Field(entity, "NeowOption.RelicId"), position = Field(entity, "NeowOption.Position") };
                if (entity.KindId.EndsWith(":MapNode", StringComparison.Ordinal))
                    return new { field = field.FieldId, col = Field(entity, "MapNode.Col"), row = Field(entity, "MapNode.Row") };
                // The current public profile does not publish ordered combat piles. Record the visible
                // definition and candidate ordinal, without inventing a hidden hand/deck instance slot.
                return new { field = field.FieldId, content = entity.ContentId,
                    candidate_ordinal = Snapshot.LegalActions.Where(a => a.ActionType == action.ActionType).Select(a => a.CandidateId).ToList().IndexOf(action.CandidateId) };
            }
            prefix.Add(Json(new { action = action.ActionType, bindings = action.Fields.Select(Binding) }));
            Send(Operation.Step, ("session_id", WireValue.Str(sessionId)), ("expected_observation_id", WireValue.Integer(Snapshot.ObservationId)), ("candidate_id", WireValue.Str(action.CandidateId)));
            Observe();
        }
        public Opening Opening()
        {
            var nodes = Snapshot.Entities.Where(e => e.KindId.EndsWith(":MapNode", StringComparison.Ordinal)).ToDictionary(e => e.InstanceId);
            string Coord(string id) => Field(nodes[id], "MapNode.Col") + "," + Field(nodes[id], "MapNode.Row");
            var deck = Snapshot.Piles.Single(p => Snapshot.Entities.Single(e => e.InstanceId == p.PileRef).KindId == "net:kind:base-spire:RunDeckView").VisibleMembers
                .Select(id => Snapshot.Entities.Single(e => e.InstanceId == id).ContentId).Order(StringComparer.Ordinal).ToArray();
            var facts = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["player.hp"] = Player("Hp"), ["player.max_hp"] = Player("MaxHp"), ["player.gold"] = Player("Gold"),
                ["deck"] = Json(deck), ["relics"] = Json(Snapshot.Entities.Where(e => e.KindId.EndsWith(":Relic", StringComparison.Ordinal)).Select(e => e.ContentId).Order(StringComparer.Ordinal)),
                ["neow.options"] = Json(Snapshot.Entities.Where(e => e.KindId.EndsWith(":NeowOption", StringComparison.Ordinal)).OrderBy(e => int.Parse(Field(e, "NeowOption.Position"), CultureInfo.InvariantCulture)).Select(e => Field(e, "NeowOption.RelicId"))),
                ["map"] = Json(nodes.Values.OrderBy(e => Coord(e.InstanceId), StringComparer.Ordinal).Select(e => new { coordinate = Coord(e.InstanceId), type = Field(e, "MapNode.Type").Split(':')[^1], children = Snapshot.Relations.Where(r => r.TypeId.EndsWith(":MapChildren", StringComparison.Ordinal) && r.SourceRef == e.InstanceId).Select(r => Coord(r.TargetRef)).Order(StringComparer.Ordinal).ToArray() }))
            };
            if (nodes.Count == 0 || deck.Length != 10 || Snapshot.LegalActions.Count != 3) throw new FormatException("Normal public opening is incomplete.");
            return new("dsl:public-stdio", Json(identity), facts);
        }
        public void Dispose()
        {
            process.StandardInput.Close();
            if (!process.WaitForExit(5000)) { process.Kill(entireProcessTree: true); process.WaitForExit(5000); }
            log.WriteLine(Json(new { stderr = stderr.GetAwaiter().GetResult(), exit_code = process.ExitCode })); log.Dispose(); process.Dispose();
        }
    }
}
