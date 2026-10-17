using System.Diagnostics;
using System.Text;
using CoreTheSpire.Engine;
using NetTheSpire.Contracts;
using NetTheSpire.Contracts.Dto;
using NetTheSpire.Control;
using NetTheSpire.Host;

namespace NetTheSpire.Tests;

internal static class BaseDslPathChecks
{
    public static IEnumerable<(string Name, Action Body)> Build()
    {
        yield return ("DB-A11 pure DSL package describe/catalog/start/step/observe publishes the real effect", Trajectory);
        yield return ("DB-A11 real stdio process runs the same package and public trajectory", StdioTrajectory);
        yield return ("DB-A05 CLI rejects retired scenarios and scopes with exit 3 before reading requests", RetiredPaths);
        yield return ("DB-A09 unknown nested public metadata fails explicitly", UnknownMetadata);
    }

    private static string Root()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "AutoTheSpire.sln"))) current = current.Parent;
        return current?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
    private static string Manifest => Path.Combine(Root(), "mod-the-spire/base-spire/package.json");
    private static RequestDto Request(string id, Operation operation, params (string Key, WireValue Value)[] fields) =>
        new(id, operation, new WireObject(fields.Select(p => new KeyValuePair<string, WireValue>(p.Key, p.Value))));
    private static WireValue Field(ResponseDto response, string name) => ((WireObject)response.Result!).Get(name)!;
    private static PublicSnapshotDto Snapshot(ResponseDto response) => PublicSnapshotDto.FromWire(Field(response, "snapshot"), NetProtocol.V02);

    private static void Trajectory()
    {
        var host = TextRuleScenario.CreatePackage(Manifest).Host;
        var describe = host.Dispatch(Request("db-describe", Operation.Describe));
        Check.True(describe.Ok, "describe succeeds");
        var catalog = host.Dispatch(Request("db-catalog", Operation.GetCatalog, ("catalog_id", WireValue.Str(host.Profile.CatalogId))));
        Check.True(catalog.Ok, "catalog succeeds");
        var identity = new IdentityDto(NetProtocol.V02, host.Profile.RulesetId, host.Profile.CatalogId, host.Profile.BackendProfileId).ToWire();
        var started = host.Dispatch(Request("db-start", Operation.Start, ("expected_identity", identity),
            ("scenario_profile", WireValue.Str(host.SupportedScenarioProfile)), ("seed", WireValue.Str("BASE-DSL-PUBLIC-00"))));
        Check.True(started.Ok, "start succeeds: " + started.Error?.Message);
        var sessionId = ((WireString)Field(started, "session_id")).Value;
        var snapshot = Snapshot(started);
        foreach (var action in new[] { "TheAbacus.Obtain", "Draw" })
        {
            var candidate = snapshot.LegalActions.Single(a => a.ActionType.EndsWith(":" + action, StringComparison.Ordinal));
            var result = host.Dispatch(Request("db-" + action, Operation.Step, ("session_id", WireValue.Str(sessionId)),
                ("expected_observation_id", WireValue.Integer(snapshot.ObservationId)), ("candidate_id", WireValue.Str(candidate.CandidateId))));
            Check.True(result.Ok, "step succeeds: " + result.Error?.Message);
            var observed = host.Dispatch(Request("db-observe-" + action, Operation.Observe, ("session_id", WireValue.Str(sessionId))));
            Check.True(observed.Ok, "observe succeeds"); snapshot = Snapshot(observed);
        }
        var block = snapshot.Entities.SelectMany(e => e.Fields).Single(f => f.FieldId.EndsWith(":Player.Block", StringComparison.Ordinal));
        Check.Equal("6", ((WireString)block.Value!).Value, "source block is visible after real DSL draw");
        var json = WireCanonical.Text(snapshot.ToWire());
        Check.False(json.Contains("Relic.Holder", StringComparison.Ordinal) || json.Contains("Relic.Counter", StringComparison.Ordinal), "hidden references and counters stay hidden");
    }

    private static void RetiredPaths()
    {
        var hostDll = typeof(TextRuleProviderSpec).Assembly.Location;
        foreach (var arguments in new[] { new[] { "--scenario", "legacy-slice" }, new[] { "--scenario", "formal-dev" }, new[] { "--scope", "base-spire-singleplayer-dev-map-v1" } })
        {
            var start = new ProcessStartInfo("dotnet") { WorkingDirectory = Root(), RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
            start.ArgumentList.Add(hostDll); foreach (var arg in arguments) start.ArgumentList.Add(arg);
            using var process = Process.Start(start)!;
            process.StandardInput.Close(); var output = process.StandardOutput.ReadToEnd(); var error = process.StandardError.ReadToEnd();
            Check.True(process.WaitForExit(10000), "host exits before accepting requests");
            Check.Equal(3, process.ExitCode, "unsupported exit code"); Check.Equal("", output, "no partial protocol session");
            Check.True(error.Contains("unsupported_capability", StringComparison.Ordinal), "explicit unsupported diagnostic");
        }
    }

    private static void StdioTrajectory()
    {
        var host = TextRuleScenario.CreatePackage(Manifest).Host;
        var info = new ProcessStartInfo("dotnet") { WorkingDirectory = Root(), RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        info.ArgumentList.Add(typeof(TextRuleProviderSpec).Assembly.Location);
        using var process = Process.Start(info)!;
        var stderr = process.StandardError.ReadToEndAsync();
        try
        {
            ResponseDto Send(RequestDto request)
            {
                process.StandardInput.WriteLine(WireCanonical.Text(request.ToWire())); process.StandardInput.Flush();
                var line = process.StandardOutput.ReadLineAsync();
                Check.True(line.Wait(10000), "stdio responds within the check timeout");
                Check.True(line.Result is not null, "stdio frame exists");
                var response = ResponseDto.FromWire(WireParser.ParseUtf8Strict(Encoding.UTF8.GetBytes(line.Result!), "stdio-test"));
                Check.True(response.Ok, "stdio: " + response.Error?.Message); return response;
            }
            Send(Request("pipe-describe", Operation.Describe));
            Send(Request("pipe-catalog", Operation.GetCatalog, ("catalog_id", WireValue.Str(host.Profile.CatalogId))));
            var identity = new IdentityDto(NetProtocol.V02, host.Profile.RulesetId, host.Profile.CatalogId, host.Profile.BackendProfileId).ToWire();
            var start = Send(Request("pipe-start", Operation.Start, ("expected_identity", identity), ("scenario_profile", WireValue.Str(host.SupportedScenarioProfile)), ("seed", WireValue.Str("BASE-DSL-STDIO-00"))));
            var sessionId = ((WireString)Field(start, "session_id")).Value; var snapshot = Snapshot(start);
            foreach (var action in new[] { "TheAbacus.Obtain", "Draw" })
            {
                var candidate = snapshot.LegalActions.Single(a => a.ActionType.EndsWith(":" + action, StringComparison.Ordinal));
                Send(Request("pipe-" + action, Operation.Step, ("session_id", WireValue.Str(sessionId)), ("expected_observation_id", WireValue.Integer(snapshot.ObservationId)), ("candidate_id", WireValue.Str(candidate.CandidateId))));
                snapshot = Snapshot(Send(Request("pipe-observe-" + action, Operation.Observe, ("session_id", WireValue.Str(sessionId)))));
            }
            var block = snapshot.Entities.SelectMany(e => e.Fields).Single(f => f.FieldId.EndsWith(":Player.Block", StringComparison.Ordinal));
            Check.Equal("6", ((WireString)block.Value!).Value, "real host exposes actual DSL block");
            process.StandardInput.Close(); Check.True(process.WaitForExit(10000), "host exits after EOF"); Check.Equal(0, process.ExitCode, "host exit");
        }
        finally { if (!process.HasExited) process.Kill(true); }
    }

    private static void UnknownMetadata()
    {
        var path = Path.Combine(Path.GetTempPath(), "base-metadata-" + Guid.NewGuid() + ".json");
        try
        {
            var package = DslPackage.Load(Manifest);
            var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(package.PublicMetadataPath))!;
            json["content"]!["base-spire:Relic"]!["unknown_effect"] = "hidden program";
            File.WriteAllText(path, json.ToJsonString());
            try { TextRuleProviderSpec.Load(path); throw new CheckFailedException("Unknown metadata was accepted"); }
            catch (NotSupportedException) { }
        }
        finally { File.Delete(path); }
    }
}
