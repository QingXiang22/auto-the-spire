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
        yield return ("RNG package host uses the source stream layout and isolates repeated seeds", SourceRandomSessions);
        yield return ("DB-A11 real stdio process runs the same package and public trajectory", () => StdioTrajectory("draw"));
        yield return ("RC07 real stdio start relics publish 14 block once in each development combat", () => StdioTrajectory("start"));
        yield return ("TC07 real stdio turn resources publish energy, HP, turn number and block", () => StdioTrajectory("turn"));
        yield return ("DD07 real stdio target takes the source damage through block absorption", () => StdioTrajectory("damage"));
        yield return ("CP08 real stdio card play spends energy and damages the target", () => StdioTrajectory("card"));
        yield return ("CP18 real stdio Iron Wave gains block and then damages the target", () => StdioTrajectory("ironwave"));
        yield return ("CP25 real stdio exhaust skill publishes its block and leaves no card in the discard pile", () => StdioTrajectory("exhaust"));
        yield return ("CP34 real stdio Bash publishes its damage and the vulnerable stacks it applies", () => StdioTrajectory("bash"));
        yield return ("CP38 real stdio power card grants strength and leaves play", () => StdioTrajectory("inflame"));
        yield return ("CP44 real stdio all-enemies attack hits the development target", () => StdioTrajectory("thunderclap"));
        yield return ("CP48 real stdio the last kill publishes the victory outcome", () => StdioTrajectory("kill"));
        yield return ("RW06 real stdio claims the visible gold reward and closes it once", () => StdioTrajectory("reward-claim"));
        yield return ("RW07 real stdio skips the visible gold reward without crediting gold", () => StdioTrajectory("reward-skip"));
        yield return ("PD09 real stdio smith upgrades a permanent card and its combat copy deals upgraded damage", () => StdioTrajectory("smith-select"));
        yield return ("PD10 real stdio smith cancellation preserves the room option and permanent deck", () => StdioTrajectory("smith-cancel"));
        yield return ("CP56 real stdio the HP-loss flag drives the extra Spite hit", () => StdioTrajectory("spite"));
        yield return ("CP61 real stdio the enemy phase damages the player and publishes its intent", () => StdioTrajectory("wurm"));
        yield return ("IC real stdio explicit target selects the second enemy", () => StdioTrajectory("explicit-target"));
        yield return ("IC real stdio mandatory hand choice resumes once after HP loss", () => StdioTrajectory("hand-exhaust"));
        yield return ("IC real stdio X card captures and pays three energy", () => StdioTrajectory("whirlwind"));
        yield return ("IC real stdio Rupture reacts after the damaging card", () => StdioTrajectory("rupture"));
        yield return ("IS real stdio initializes the source Ironclad deck and draws five combat copies", () => StdioTrajectory("ironclad-start"));
        yield return ("CR real stdio starter cards defeat an enemy and acquire one of three permanent rewards", () => StdioTrajectory("card-reward-claim"));
        yield return ("CR real stdio skips card rewards and closes without acquiring a card", () => StdioTrajectory("card-reward-skip"));
        yield return ("RL real stdio pickup relics publish permanent HP and gold gains", () => StdioTrajectory("relic-pickups"));
        yield return ("RL real stdio third attack triggers OrnamentalFan exactly once", () => StdioTrajectory("relic-counter"));
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

    private static void SourceRandomSessions()
    {
        var host = TextRuleScenario.CreatePackage(Manifest).Host;
        var source = host.SourceForTests!;
        Check.Equal(RandomService.VanillaDrawProfileId, source.RngProfileId, "describe provider reports actual source mapping");
        var one = source.CreateSession("BASE-DSL-STDIO-00");
        var two = source.CreateSession("BASE-DSL-STDIO-00");
        var other = source.CreateSession("abc");
        foreach (var provided in new[] { one, two, other })
        {
            var s = provided.Session; Check.Equal(0L, s.Random.TotalDraws, "factory returns unstarted session without random draws"); s.Start();
            foreach (var name in new[] { "Ironclad.Initialize", "BeginDevelopmentCombat", "BeginDevelopmentTurn" })
                s.Submit(s.RuleSet.Actions.Single(a => a.QualifiedId == "base-spire:" + name), s.Participant(0), []);
            Check.Equal(9L, s.Random.DrawCount("base-spire:run:shuffle"), "one initial shuffle");
            Check.Equal(RandomService.VanillaDrawProfileId, s.Random.DrawProfileId, "factory and provider agree");
        }
        string Hand(CoreTheSpire.Engine.Session s)
        {
            var cards = s.State.CollectionMembers(s.RuleSet.Collections.Single(c => c.QualifiedId == "base-spire:Hand").Id);
            var field = s.RuleSet.TypeByQualifiedId("base-spire:Card").FieldIndex("DefinitionId");
            return string.Join(",", cards.Select(c => s.State.GetField(c, field).AsString()));
        }
        Check.Equal(Hand(one.Session), Hand(two.Session), "same seed repeats source hand across independent sessions");
        Check.False(Hand(one.Session) == Hand(other.Session), "different source seed changes this fixed opening");
        Check.Equal(host.Profile.RulesetId, TextRuleScenario.CreatePackage(Manifest, defaultGameSeed: "abc").Host.Profile.RulesetId, "game seed is absent from static identity");
    }

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

    private static void StdioTrajectory(string mode)
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
            (string Action, string Block, bool Active, string Energy, string Turn, bool TurnOpen, string Target)[] sequence = mode switch
            {
                "relic-pickups" => [
                    ("Pear.Obtain", "0", false, "0", "0", false, ""),
                    ("Mango.Obtain", "0", false, "0", "0", false, ""),
                    ("OldCoin.Obtain", "0", false, "0", "0", false, ""),
                    ("LeesWaffle.Obtain", "0", false, "0", "0", false, "")],
                "relic-counter" => [
                    ("OrnamentalFan.Obtain", "0", false, "0", "0", false, ""),
                    ("AddDevelopmentTarget", "0", false, "0", "0", false, "40"),
                    ("BeginDevelopmentCombat", "0", true, "0", "0", false, "40"),
                    ("BeginDevelopmentTurn", "0", true, "3", "1", true, "40"),
                    ("Strike.AddToHand", "0", true, "3", "1", true, "40"),
                    ("PlayAttackFromHand", "0", true, "2", "1", true, "34"),
                    ("Strike.AddToHand", "0", true, "2", "1", true, "34"),
                    ("PlayAttackFromHand", "0", true, "1", "1", true, "28"),
                    ("Strike.AddToHand", "0", true, "1", "1", true, "28"),
                    ("PlayAttackFromHand", "4", true, "0", "1", true, "22")],
                "card-reward-claim" or "card-reward-skip" => [
                    ("Ironclad.Initialize", "0", false, "0", "0", false, ""),
                    ("TwigSlimeS.Spawn", "0", false, "0", "0", false, "7"),
                    ("BeginDevelopmentCombat", "0", true, "0", "0", false, "7"),
                    ("BeginDevelopmentTurn", "0", true, "3", "1", true, "7")],
                "ironclad-start" => [
                    ("Ironclad.Initialize", "0", false, "0", "0", false, ""),
                    ("AddDevelopmentTarget", "0", false, "0", "0", false, "40"),
                    ("BeginDevelopmentCombat", "0", true, "0", "0", false, "40"),
                    ("BeginDevelopmentTurn", "0", true, "3", "1", true, "40")],
                "smith-select" or "smith-cancel" => [
                    ("Strike.Obtain", "0", false, "0", "0", false, ""), ("Strike.Obtain", "0", false, "0", "0", false, ""),
                    ("EnterDevelopmentRestSite", "0", false, "0", "0", false, ""), ("ForgeAtDevelopmentCampfire", "0", false, "0", "0", false, "")],
                "explicit-target" => [
                    ("AddDevelopmentTarget", "0", false, "0", "0", false, ""), ("AddDevelopmentTarget", "0", false, "0", "0", false, ""),
                    ("Strike.AddToHand", "0", false, "0", "0", false, ""), ("BeginDevelopmentCombat", "0", true, "0", "0", false, ""),
                    ("BeginDevelopmentTurn", "0", true, "3", "1", true, ""), ("PlayTargetedCard", "0", true, "3", "1", true, "")],
                "hand-exhaust" => [
                    ("Brand.AddToHand", "0", false, "0", "0", false, ""), ("Strike.AddToHand", "0", false, "0", "0", false, ""),
                    ("Defend.AddToHand", "0", false, "0", "0", false, ""), ("AddDevelopmentTarget", "0", false, "0", "0", false, "40"),
                    ("BeginDevelopmentCombat", "0", true, "0", "0", false, "40"), ("BeginDevelopmentTurn", "0", true, "3", "1", true, "40"),
                    ("PlayCardFromHand", "0", true, "3", "1", true, "40")],
                "whirlwind" => [
                    ("Whirlwind.AddToHand", "0", false, "0", "0", false, ""), ("AddDevelopmentTarget", "0", false, "0", "0", false, "40"),
                    ("BeginDevelopmentCombat", "0", true, "0", "0", false, "40"), ("BeginDevelopmentTurn", "0", true, "3", "1", true, "40"),
                    ("PlayCardFromHand", "0", true, "0", "1", true, "25")],
                "rupture" => [
                    ("Rupture.AddToHand", "0", false, "0", "0", false, ""), ("Hemokinesis.AddToHand", "0", false, "0", "0", false, ""),
                    ("AddDevelopmentTarget", "0", false, "0", "0", false, "40"), ("BeginDevelopmentCombat", "0", true, "0", "0", false, "40"),
                    ("BeginDevelopmentTurn", "0", true, "3", "1", true, "40"), ("PlayCardFromHand", "0", true, "2", "1", true, "40"),
                    ("PlayAttackFromHand", "0", true, "1", "1", true, "25")],
                "start" => [
                    ("Anchor.Obtain", "0", false, "0", "0", false, ""), ("FakeAnchor.Obtain", "0", false, "0", "0", false, ""),
                    ("BeginDevelopmentCombat", "14", true, "0", "0", false, ""), ("EndDevelopmentCombat", "14", false, "0", "0", false, ""),
                    ("BeginDevelopmentCombat", "14", true, "0", "0", false, "")],
                "turn" => [
                    ("Lantern.Obtain", "0", false, "0", "0", false, ""), ("HornCleat.Obtain", "0", false, "0", "0", false, ""),
                    ("BeginDevelopmentCombat", "0", true, "0", "0", false, ""),
                    ("BeginDevelopmentTurn", "0", true, "4", "1", true, ""), ("EndDevelopmentTurn", "0", true, "4", "1", false, ""),
                    ("BeginDevelopmentTurn", "14", true, "3", "2", true, "")],
                "damage" => [
                    ("AddDevelopmentTarget", "0", false, "0", "0", false, "40"), ("MercuryHourglass.Obtain", "0", false, "0", "0", false, "40"),
                    ("BeginDevelopmentCombat", "0", true, "0", "0", false, "40"),
                    ("BeginDevelopmentTurn", "0", true, "3", "1", true, "37"), ("EndDevelopmentTurn", "0", true, "3", "1", false, "37"),
                    ("BeginDevelopmentTurn", "0", true, "3", "2", true, "34")],
                "card" => [
                    ("AddDevelopmentTarget", "0", false, "0", "0", false, "40"), ("Strike.AddToHand", "0", false, "0", "0", false, "40"),
                    ("BeginDevelopmentCombat", "0", true, "0", "0", false, "40"),
                    ("BeginDevelopmentTurn", "0", true, "3", "1", true, "40"),
                    ("PlayAttackFromHand", "0", true, "2", "1", true, "34")],
                "ironwave" => [
                    ("AddDevelopmentTarget", "0", false, "0", "0", false, "40"), ("IronWave.AddToHand", "0", false, "0", "0", false, "40"),
                    ("BeginDevelopmentCombat", "0", true, "0", "0", false, "40"),
                    ("BeginDevelopmentTurn", "0", true, "3", "1", true, "40"),
                    ("PlayAttackFromHand", "5", true, "2", "1", true, "35")],
                "exhaust" => [
                    ("Impervious.AddToHand", "0", false, "0", "0", false, ""),
                    ("BeginDevelopmentCombat", "0", true, "0", "0", false, ""),
                    ("BeginDevelopmentTurn", "0", true, "3", "1", true, ""),
                    ("PlayCardFromHand", "30", true, "1", "1", true, "")],
                "bash" => [
                    ("AddDevelopmentTarget", "0", false, "0", "0", false, "40"), ("Bash.AddToHand", "0", false, "0", "0", false, "40"),
                    ("BeginDevelopmentCombat", "0", true, "0", "0", false, "40"),
                    ("BeginDevelopmentTurn", "0", true, "3", "1", true, "40"),
                    ("PlayAttackFromHand", "0", true, "1", "1", true, "32")],
                "inflame" => [
                    ("Inflame.AddToHand", "0", false, "0", "0", false, ""),
                    ("BeginDevelopmentCombat", "0", true, "0", "0", false, ""),
                    ("BeginDevelopmentTurn", "0", true, "3", "1", true, ""),
                    ("PlayCardFromHand", "0", true, "2", "1", true, "")],
                "kill" or "reward-claim" or "reward-skip" => [
                    ("AddDevelopmentTarget", "0", false, "0", "0", false, "40"), ("Bludgeon.AddToHand", "0", false, "0", "0", false, "40"),
                    ("UpgradeCardInHand", "0", false, "0", "0", false, "40"),
                    ("BeginDevelopmentCombat", "0", true, "0", "0", false, "40"),
                    ("BeginDevelopmentTurn", "0", true, "3", "1", true, "40"),
                    ("PlayAttackFromHand", "0", false, "0", "1", false, "0")],
                "spite" => [
                    ("AddDevelopmentTarget", "0", false, "0", "0", false, "40"), ("Spite.AddToHand", "0", false, "0", "0", false, "40"),
                    ("Bloodletting.AddToHand", "0", false, "0", "0", false, "40"),
                    ("BeginDevelopmentCombat", "0", true, "0", "0", false, "40"),
                    ("BeginDevelopmentTurn", "0", true, "3", "1", true, "40"),
                    ("PlayCardFromHand", "0", true, "5", "1", true, "40"),
                    ("PlayAttackFromHand", "0", true, "5", "1", true, "30")],
                "wurm" => [
                    ("FuzzyWurmCrawler.Spawn", "0", false, "0", "0", false, "55"),
                    ("BeginDevelopmentCombat", "0", true, "0", "0", false, "55"),
                    ("BeginDevelopmentTurn", "0", true, "3", "1", true, "55"),
                    ("EndDevelopmentTurn", "0", true, "3", "1", false, "55")],
                "thunderclap" => [
                    ("AddDevelopmentTarget", "0", false, "0", "0", false, "40"), ("Thunderclap.AddToHand", "0", false, "0", "0", false, "40"),
                    ("BeginDevelopmentCombat", "0", true, "0", "0", false, "40"),
                    ("BeginDevelopmentTurn", "0", true, "3", "1", true, "40"),
                    ("PlayCardFromHand", "0", true, "2", "1", true, "36")],
                _ => [("TheAbacus.Obtain", "0", false, "0", "0", false, ""), ("Draw", "6", false, "0", "0", false, "")],
            };
            var stepNumber = 0;
            foreach (var step in sequence)
            {
                var candidates = snapshot.LegalActions.Where(a => a.ActionType.EndsWith(":" + step.Action, StringComparison.Ordinal)).ToArray();
                var candidate = mode == "hand-exhaust" && step.Action == "PlayCardFromHand"
                    ? candidates.Single(a => a.Fields.Any(f => f.Value is WireString value && value.Value == snapshot.Entities.Single(e => e.ContentId == "base-spire:brand").InstanceId))
                    : candidates.Single();
                var suffix = (++stepNumber).ToString(System.Globalization.CultureInfo.InvariantCulture);
                Send(Request("pipe-step-" + suffix, Operation.Step, ("session_id", WireValue.Str(sessionId)), ("expected_observation_id", WireValue.Integer(snapshot.ObservationId)), ("candidate_id", WireValue.Str(candidate.CandidateId))));
                snapshot = Snapshot(Send(Request("pipe-observe-" + suffix, Operation.Observe, ("session_id", WireValue.Str(sessionId)))));
                Check.Equal(step.Block, Public(snapshot, "Player.Block"), "real host exposes the expected block at every step");
                Check.Equal(step.Energy, Public(snapshot, "Player.Energy"), "public energy follows the DSL turn cycle");
                Check.Equal(step.Turn, Public(snapshot, "Player.TurnNumber"), "public turn counter follows the DSL turn cycle");
                if (step.Target.Length > 0) Check.Equal(step.Target, Public(snapshot, "Enemy.Hp"), "public development target HP follows the real damage result");
                var active = snapshot.Entities.SelectMany(e => e.Fields).Single(f => f.FieldId.EndsWith(":Player.CombatActive", StringComparison.Ordinal));
                Check.Equal(step.Active, ((WireBool)active.Value!).Value, "public lifecycle state");
                var open = snapshot.Entities.SelectMany(e => e.Fields).Single(f => f.FieldId.EndsWith(":Player.TurnOpen", StringComparison.Ordinal));
                Check.Equal(step.TurnOpen, ((WireBool)open.Value!).Value, "public turn gate");
                if (snapshot.LegalActions.All(a => a.ActionType == "net:action:net.answer_decision")) continue;
                Check.Equal(!step.Active && Public(snapshot, "Player.Room") == "none", snapshot.LegalActions.Any(a => a.ActionType.EndsWith(":BeginDevelopmentCombat", StringComparison.Ordinal)), "begin candidate follows real phase and room closure");
                Check.Equal(step.Active, snapshot.LegalActions.Any(a => a.ActionType.EndsWith(":EndDevelopmentCombat", StringComparison.Ordinal)), "end candidate follows real phase");
                Check.Equal(step.Active && !step.TurnOpen, snapshot.LegalActions.Any(a => a.ActionType.EndsWith(":BeginDevelopmentTurn", StringComparison.Ordinal)), "turn candidate follows the real turn gate");
                Check.Equal(step.TurnOpen, snapshot.LegalActions.Any(a => a.ActionType.EndsWith(":EndDevelopmentTurn", StringComparison.Ordinal)), "end turn candidate follows the real turn gate");
            }
            if (mode is "explicit-target" or "hand-exhaust")
            {
                Check.Equal(2, snapshot.LegalActions.Count, "two mandatory choices with no cancellation");
                Check.True(snapshot.LegalActions.All(a => a.ActionType == "net:action:net.answer_decision"), "generic native decision candidates");
                if (mode == "hand-exhaust") Check.Equal("79", Public(snapshot, "Player.Hp"), "HP loss before selection");
                var choice = snapshot.LegalActions.Last();
                var chosenId = ((WireString)choice.Fields.Single().Value!).Value;
                Send(Request("ic-answer", Operation.Step, ("session_id", WireValue.Str(sessionId)), ("expected_observation_id", WireValue.Integer(snapshot.ObservationId)), ("candidate_id", WireValue.Str(choice.CandidateId))));
                snapshot = Snapshot(Send(Request("ic-observe", Operation.Observe, ("session_id", WireValue.Str(sessionId)))));
                Check.Equal(mode == "explicit-target" ? "2" : "3", Public(snapshot, "Player.Energy"), "exactly one payment");
                if (mode == "explicit-target")
                {
                    foreach (var enemy in snapshot.Entities.Where(e => e.KindId == "net:kind:base-spire:Enemy"))
                    {
                        var hp = ((WireString)enemy.Fields.Single(f => f.FieldId.EndsWith(":Enemy.Hp", StringComparison.Ordinal)).Value!).Value;
                        Check.Equal(enemy.InstanceId == chosenId ? "34" : "40", hp, "only chosen enemy damaged");
                    }
                }
                else
                {
                    Check.Equal("79", Public(snapshot, "Player.Hp"), "resume never repeats HP loss");
                    Check.Equal("1", Public(snapshot, "Power.Stacks"), "strength after selected exhaust");
                    Check.True(snapshot.LegalActions.Any(a => a.ActionType == "net:action:base-spire:PlayAttackFromHand"), "other card remains playable");
                }
            }
            if (mode == "whirlwind")
            {
                var card = snapshot.Entities.Single(e => e.ContentId == "base-spire:whirlwind");
                Check.Equal("3", ((WireString)card.Fields.Single(f => f.FieldId.EndsWith(":Card.PaidX", StringComparison.Ordinal)).Value!).Value, "captured X public");
                Check.True(((WireBool)card.Fields.Single(f => f.FieldId.EndsWith(":Card.IsX", StringComparison.Ordinal)).Value!).Value, "X marker public");
            }
            if (mode == "relic-pickups")
            {
                Check.Equal("111", Public(snapshot, "Player.MaxHp"), "source 80 plus 10 plus 14 plus 7 maximum HP");
                Check.Equal("111", Public(snapshot, "Player.Hp"), "waffle heals to increased maximum");
                Check.Equal("399", Public(snapshot, "Player.Gold"), "source 300 gold obtained once");
                Check.Equal(4, snapshot.Entities.Count(e => e.KindId == "net:kind:base-spire:Relic"), "all four actual relic instances visible");
                Check.False(snapshot.LegalActions.Any(a => a.ActionType == "net:action:base-spire:OldCoin.Obtain"), "owned pickup cannot be repeated");
            }
            if (mode == "ironclad-start")
            {
                Check.Equal("base-spire:ironclad", Public(snapshot, "Player.CharacterId"), "actual character identity");
                Check.Equal("80", Public(snapshot, "Player.Hp"), "source HP");
                Check.Equal("99", Public(snapshot, "Player.Gold"), "source gold");
                var deck = snapshot.Groups.Single(g => g.TypeId.EndsWith(":RunDeck", StringComparison.Ordinal)).Members;
                var hand = snapshot.LegalActions.Where(a => a.ActionType is "net:action:base-spire:PlayCardFromHand" or "net:action:base-spire:PlayAttackFromHand")
                    .Select(a => ((WireString)a.Fields.Single().Value!).Value).Distinct().ToArray();
                Check.Equal(10, deck.Count, "ten permanent cards"); Check.Equal(5, hand.Length, "five automatic opening cards have actual play candidates");
                Check.False(hand.Any(id => deck.Any(permanent => permanent.EntityRef == id)), "combat copies have separate public handles");
                var definitions = deck.Select(member => snapshot.Entities.Single(e => e.InstanceId == member.EntityRef).ContentId).ToArray();
                Check.Equal(5, definitions.Count(id => id == "base-spire:strike-ironclad"), "source strikes");
                Check.Equal(4, definitions.Count(id => id == "base-spire:defend-ironclad"), "source defends");
                Check.Equal(1, definitions.Count(id => id == "base-spire:bash"), "source bash");
                Check.Equal(1, snapshot.Entities.Count(e => e.ContentId == "base-spire:burning-blood"), "real starter relic");
                Check.False(snapshot.LegalActions.Any(a => a.ActionType.EndsWith(":Ironclad.Initialize", StringComparison.Ordinal)), "initialization cannot repeat");
                Check.False(WireCanonical.Text(snapshot.ToWire()).Contains("Card.PermanentSource", StringComparison.Ordinal), "source references remain hidden");
            }
            if (mode is "card-reward-claim" or "card-reward-skip")
            {
                void StepCardReward(LegalActionDto candidate)
                {
                    var id = "card-reward-" + ++stepNumber;
                    Send(Request(id, Operation.Step, ("session_id", WireValue.Str(sessionId)),
                        ("expected_observation_id", WireValue.Integer(snapshot.ObservationId)), ("candidate_id", WireValue.Str(candidate.CandidateId))));
                    snapshot = Snapshot(Send(Request(id + "-observe", Operation.Observe, ("session_id", WireValue.Str(sessionId)))));
                }
                void Named(string name) => StepCardReward(snapshot.LegalActions.Single(a => a.ActionType == "net:action:base-spire:" + name));
                var moves = 0;
                while (PublicBool(snapshot, "Player.CombatActive"))
                {
                    Check.True(++moves <= 20, "starter combat completes within twenty public moves");
                    var attack = snapshot.LegalActions.FirstOrDefault(a => a.ActionType == "net:action:base-spire:PlayAttackFromHand");
                    if (attack is not null) StepCardReward(attack);
                    else { Named("EndDevelopmentTurn"); if (PublicBool(snapshot, "Player.CombatActive")) Named("BeginDevelopmentTurn"); }
                }
                Check.Equal("victory", Public(snapshot, "Player.CombatOutcome"), "real starter-card victory without HP injection");
                Check.Equal("rewards", Public(snapshot, "Player.Room"), "reward room opens");
                var offered = snapshot.Groups.Single(g => g.TypeId.EndsWith(":CardRewardOptions", StringComparison.Ordinal)).Members.Select(m => m.EntityRef).ToArray();
                Check.Equal(3, offered.Length, "three public reward options");
                Check.Equal(3, offered.Select(id => snapshot.Entities.Single(e => e.InstanceId == id).ContentId).Distinct().Count(), "three distinct definitions");
                Check.Equal(10, snapshot.Groups.Single(g => g.TypeId.EndsWith(":RunDeck", StringComparison.Ordinal)).Members.Count, "unclaimed cards are not permanent");
                Check.Equal(3, snapshot.LegalActions.Count(a => a.ActionType == "net:action:base-spire:ClaimDevelopmentCardReward"), "one claim per actual option");
                foreach (var id in offered)
                    Check.True(snapshot.Entities.Single(e => e.InstanceId == id).Fields.Any(f => f.FieldId.EndsWith(":Card.Rarity", StringComparison.Ordinal)), "reward rarity is public");
                Check.False(WireCanonical.Text(snapshot.ToWire()).Contains("CardRarityOffset", StringComparison.Ordinal), "future rarity odds remain hidden");
                var claim = mode == "card-reward-claim";
                var chosen = offered[1];
                if (claim)
                    StepCardReward(snapshot.LegalActions.Single(a => a.ActionType == "net:action:base-spire:ClaimDevelopmentCardReward" && a.Fields.Any(f => f.Value is WireString v && v.Value == chosen)));
                else Named("SkipDevelopmentCardReward");
                Check.False(PublicBool(snapshot, "Player.CardRewardPending"), "card reward spent");
                Check.True(PublicBool(snapshot, "Player.GoldRewardPending"), "gold reward remains independent");
                var deck = snapshot.Groups.Single(g => g.TypeId.EndsWith(":RunDeck", StringComparison.Ordinal)).Members;
                Check.Equal(claim ? 11 : 10, deck.Count, "only a selected card joins the permanent deck");
                if (claim) Check.True(deck.Any(m => m.EntityRef == chosen), "selected public handle becomes permanent");
                Check.True(offered.Where(id => !claim || id != chosen).All(id => snapshot.Entities.All(e => e.InstanceId != id)), "unselected reward instances destroyed");
                Check.False(snapshot.LegalActions.Any(a => a.ActionType == "net:action:base-spire:ClaimDevelopmentCardReward"), "no repeated claim candidates");
                Named("CloseDevelopmentRewards"); Check.Equal("none", Public(snapshot, "Player.Room"), "explicit reward close");
                Named("TwigSlimeS.Spawn"); Named("BeginDevelopmentCombat"); Named("BeginDevelopmentTurn");
                Check.Equal(claim ? 11 : 10, snapshot.Groups.Single(g => g.TypeId.EndsWith(":RunDeck", StringComparison.Ordinal)).Members.Count, "permanent deck survives next combat");
                Check.False(WireCanonical.Text(snapshot.ToWire()).Contains("Card.PermanentSource", StringComparison.Ordinal), "combat source references stay hidden");
            }
            if (mode == "rupture") { Check.Equal("78", Public(snapshot, "Player.Hp"), "real HP loss"); Check.Equal("2", Public(snapshot, "Player.PlaySequence"), "real play count"); }
            if (mode is "bash" or "inflame" or "thunderclap") Check.Equal(mode == "thunderclap" ? "1" : "2", Public(snapshot, "Power.Stacks"), "the applied power is public on its holder");
            if (mode is "kill" or "reward-claim" or "reward-skip") Check.Equal("victory", Public(snapshot, "Player.CombatOutcome"), "the public combat outcome follows the last kill");
            if (mode is "smith-select" or "smith-cancel")
            {
                var cancel = mode == "smith-cancel";
                bool Has(string name) => snapshot.LegalActions.Any(a => a.ActionType == "net:action:base-spire:" + name);
                bool Upgraded(EntityDto card) => ((WireBool)card.Fields.Single(f => f.FieldId.EndsWith(":Card.Upgraded", StringComparison.Ordinal)).Value!).Value;
                void StepSmith(LegalActionDto candidate)
                {
                    var id = "pipe-smith-" + ++stepNumber;
                    Send(Request(id, Operation.Step, ("session_id", WireValue.Str(sessionId)),
                        ("expected_observation_id", WireValue.Integer(snapshot.ObservationId)), ("candidate_id", WireValue.Str(candidate.CandidateId))));
                    snapshot = Snapshot(Send(Request(id + "-observe", Operation.Observe, ("session_id", WireValue.Str(sessionId)))));
                }
                void StepNamed(string name) => StepSmith(snapshot.LegalActions.Single(a => a.ActionType == "net:action:base-spire:" + name));
                Check.True(PublicBool(snapshot, "Player.PendingForge"), "smith is pending in the public engine snapshot");
                Check.False(PublicBool(snapshot, "Player.RoomUsed"), "the option has not been consumed");
                Check.Equal(3, snapshot.LegalActions.Count, "two permanent choices plus cancellation");
                Check.True(snapshot.LegalActions.All(a => a.ActionType == "net:action:net.answer_decision"), "only real decision answers are available while paused");
                Check.Equal(2, snapshot.Groups.Single(g => g.TypeId.EndsWith(":RunDeck", StringComparison.Ordinal)).Members.Count, "the permanent deck is publicly visible");
                var choice = cancel ? snapshot.LegalActions.Single(a => a.Fields.Count == 0) : snapshot.LegalActions.Last(a => a.Fields.Count > 0);
                var chosenId = cancel ? null : ((WireString)choice.Fields.Single().Value!).Value;
                StepSmith(choice);
                Check.False(PublicBool(snapshot, "Player.PendingForge"), "answer clears the pending marker");
                Check.Equal(!cancel, PublicBool(snapshot, "Player.RoomUsed"), "only successful smith spends the option");
                Check.Equal(cancel ? 0 : 1, snapshot.Entities.Count(e => e.KindId == "net:kind:base-spire:Card" && Upgraded(e)), "only the selected permanent instance upgraded");
                if (!cancel) Check.True(Upgraded(snapshot.Entities.Single(e => e.InstanceId == chosenId)), "the exact selected handle upgraded");
                if (cancel)
                {
                    Check.True(Has("RestAtDevelopmentCampfire") && Has("ForgeAtDevelopmentCampfire"), "cancellation leaves both options available");
                    StepNamed("RestAtDevelopmentCampfire");
                }
                StepNamed("LeaveDevelopmentRoom"); StepNamed("AddDevelopmentTarget"); StepNamed("BeginDevelopmentCombat");
                StepNamed("Draw"); StepNamed("Draw"); StepNamed("BeginDevelopmentTurn");
                var attack = snapshot.LegalActions.Where(a => a.ActionType == "net:action:base-spire:PlayAttackFromHand")
                    .First(a => Upgraded(snapshot.Entities.Single(e => e.InstanceId == ((WireString)a.Fields.Single().Value!).Value)) == !cancel);
                StepSmith(attack);
                Check.Equal(cancel ? "34" : "31", Public(snapshot, "Enemy.Hp"), "the actual combat copy uses its permanent upgrade state");
                Check.False(WireCanonical.Text(snapshot.ToWire()).Contains("Card.PermanentSource", StringComparison.Ordinal), "hidden source references stay out of the public payload");
            }
            if (mode is "reward-claim" or "reward-skip")
            {
                bool Has(string action) => snapshot.LegalActions.Any(a => a.ActionType == "net:action:base-spire:" + action);
                void StepReward(string action)
                {
                    var candidate = snapshot.LegalActions.Single(a => a.ActionType == "net:action:base-spire:" + action);
                    Send(Request("pipe-reward-" + action, Operation.Step, ("session_id", WireValue.Str(sessionId)),
                        ("expected_observation_id", WireValue.Integer(snapshot.ObservationId)), ("candidate_id", WireValue.Str(candidate.CandidateId))));
                    snapshot = Snapshot(Send(Request("pipe-reward-observe-" + action, Operation.Observe, ("session_id", WireValue.Str(sessionId)))));
                }
                var amount = long.Parse(Public(snapshot, "Player.GoldRewardAmount"), System.Globalization.CultureInfo.InvariantCulture);
                Check.True(amount is >= 10 and <= 20, "public reward amount is in the source range");
                Check.Equal("99", Public(snapshot, "Player.Gold"), "offering has not granted gold");
                Check.Equal("rewards", Public(snapshot, "Player.Room"), "the reward screen is open");
                Check.True(PublicBool(snapshot, "Player.GoldRewardPending"), "the pending entry is public");
                Check.True(Has("ClaimDevelopmentGoldReward") && Has("CloseDevelopmentRewards"), "claim and skip are real public candidates");
                Check.False(Has("BeginDevelopmentCombat") || Has("EnterDevelopmentRestSite") || Has("LeaveDevelopmentRoom"), "room transitions cannot bypass rewards");
                if (mode == "reward-claim")
                {
                    StepReward("ClaimDevelopmentGoldReward");
                    Check.False(Has("ClaimDevelopmentGoldReward"), "claim cannot be repeated");
                    Check.Equal("rewards", Public(snapshot, "Player.Room"), "claiming keeps the screen open");
                }
                StepReward("CloseDevelopmentRewards");
                Check.Equal((mode == "reward-claim" ? 99 + amount : 99).ToString(System.Globalization.CultureInfo.InvariantCulture), Public(snapshot, "Player.Gold"), "gold reflects claim or skip");
                Check.Equal("0", Public(snapshot, "Player.GoldRewardAmount"), "the saved amount is cleared");
                Check.False(PublicBool(snapshot, "Player.GoldRewardPending"), "the entry is cleared");
                Check.Equal("none", Public(snapshot, "Player.Room"), "explicit close leaves rewards");
                Check.False(Has("ClaimDevelopmentGoldReward") || Has("CloseDevelopmentRewards"), "no stale reward candidates");
                Check.True(Has("BeginDevelopmentCombat") && Has("EnterDevelopmentRestSite"), "next rooms become available");
            }
            if (mode == "spite") Check.True(PublicBool(snapshot, "Player.LostHpThisTurn"), "the per-turn HP-loss flag is public");
            if (mode == "wurm") { Check.Equal("76", Public(snapshot, "Player.Hp"), "the enemy attack reached the public player HP"); Check.Equal("buff", Public(snapshot, "Enemy.Intent"), "the next intent is public"); }
            process.StandardInput.Close(); Check.True(process.WaitForExit(10000), "host exits after EOF"); Check.Equal(0, process.ExitCode, "host exit");
        }
        finally { if (!process.HasExited) process.Kill(true); }
    }

    private static bool PublicBool(PublicSnapshotDto snapshot, string field) =>
        ((WireBool)snapshot.Entities.SelectMany(e => e.Fields).Single(f => f.FieldId.EndsWith(":" + field, StringComparison.Ordinal)).Value!).Value;

    private static string Public(PublicSnapshotDto snapshot, string field) =>
        ((WireString)snapshot.Entities.SelectMany(e => e.Fields).Single(f => f.FieldId.EndsWith(":" + field, StringComparison.Ordinal)).Value!).Value;

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
