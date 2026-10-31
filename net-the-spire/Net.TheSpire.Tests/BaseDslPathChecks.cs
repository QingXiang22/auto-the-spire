using System.Diagnostics;
using System.Text;
using System.Text.Json;
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
        yield return ("PUBLIC-PILES normal opening publishes real counts and memberships without hidden draw identities", BaseDslPileChecks.NormalPiles);
        yield return ("DB-A11 pure DSL package describe/catalog/start/step/observe publishes the real effect", Trajectory);
        yield return ("RNG package host uses the source stream layout and isolates repeated seeds", SourceRandomSessions);
        yield return ("DB-A11 real stdio process runs the same package and public trajectory", () => StdioTrajectory("draw"));
        yield return ("ENV-T05 real stdio normal Neow, source map and first real encounter", () => StdioTrajectory("normal-opening"));
        foreach (var relic in new[] { "boots", "pearl" })
            yield return ("NEOW-EFFECT real stdio normal acquisition, permanent deck and first combat: " + relic, () => StdioTrajectory("normal-neow-" + relic));
        yield return ("DRAW-PERMISSION real stdio BattleTrance blocks effect draw, publishes expiry and cleanup", () => StdioTrajectory("draw-permission"));
        yield return ("NEOW-EFFECT real stdio development FlexPotion stacks, real attack, expiry and replay", () => StdioTrajectory("flex-potion"));
        foreach (var path in new[] { "claim", "skip" })
            yield return ("ENV-T05 real stdio normal combat rewards and map return: " + path, () => StdioTrajectory("normal-" + path));
        yield return ("UNKNOWN real stdio normal victory, map return and actual Unknown→Monster", () => StdioTrajectory("normal-unknown"));
        yield return ("EVENT real stdio normal victory and Aroma upgrade/map return", () => StdioTrajectory("normal-event"));
        yield return ("EVENT real stdio registered unimplemented ThisOrThat remains a cached failure", () => StdioTrajectory("normal-event-unsupported"));
        foreach (var branch in new[] { "eat", "plant" })
            yield return ("EVENT real stdio normal SapphireSeed and map return: " + branch, () => StdioTrajectory("normal-sapphire-" + branch));
        foreach (var branch in new[] { "trudge", "fight-skip", "fight-claim" })
            yield return ("EVENT real stdio normal DenseVegetation child visits and map return: " + branch, () => StdioTrajectory("normal-dense-" + branch));
        yield return ("EVENT real stdio development BattlewornDummy timeout resumes same parent once", () => StdioTrajectory("battleworn-event"));
        yield return ("RC07 real stdio start relics publish 14 block once in each development combat", () => StdioTrajectory("start"));
        yield return ("TC07 real stdio turn resources publish energy, HP, turn number and block", () => StdioTrajectory("turn"));
        yield return ("DD07 real stdio target takes the source damage through block absorption", () => StdioTrajectory("damage"));
        yield return ("CP08 real stdio card play spends energy and damages the target", () => StdioTrajectory("card"));
        yield return ("CP18 real stdio Iron Wave gains block and then damages the target", () => StdioTrajectory("ironwave"));
        yield return ("CP25 real stdio exhaust skill publishes its block and leaves no card in the discard pile", () => StdioTrajectory("exhaust"));
        yield return ("RL ToughBandages real stdio active discard gains block and turn-end flush stays silent", () => StdioTrajectory("tough-bandages"));

        yield return ("RL CharonsAshes real stdio exhaust reaction damages the public target", () => StdioTrajectory("charons-ashes"));
        yield return ("RL CharonsAshes real stdio exhaustion settles block and card movement before victory", () => StdioTrajectory("charons-ashes-kill"));
        yield return ("CP34 real stdio Bash publishes its damage and the vulnerable stacks it applies", () => StdioTrajectory("bash"));
        yield return ("CP38 real stdio power card grants strength and leaves play", () => StdioTrajectory("inflame"));
        yield return ("CP44 real stdio all-enemies attack hits the development target", () => StdioTrajectory("thunderclap"));
        yield return ("CP48 real stdio the last kill publishes the victory outcome", () => StdioTrajectory("kill"));
        yield return ("RW06 real stdio claims the visible gold reward and closes it once", () => StdioTrajectory("reward-claim"));
        yield return ("RW07 real stdio skips the visible gold reward without crediting gold", () => StdioTrajectory("reward-skip"));
        yield return ("PD09 real stdio smith upgrades a permanent card and its combat copy deals upgraded damage", () => StdioTrajectory("smith-select"));
        yield return ("PD10 real stdio smith cancellation preserves the room option and permanent deck", () => StdioTrajectory("smith-cancel"));
        yield return ("BEHAVIOR real stdio event choices publish Sown without leaking hidden combat copies", () => StdioTrajectory("sown-event"));
        yield return ("CP56 real stdio the HP-loss flag drives the extra Spite hit", () => StdioTrajectory("spite"));
        yield return ("CP61 real stdio the enemy phase damages the player and publishes its intent", () => StdioTrajectory("wurm"));
        yield return ("DUMMY real stdio publishes three-turn living escape and timeout outcome", () => StdioTrajectory("battle-friend"));
        yield return ("WRIGGLER real stdio publishes four slot moves, Infection -1 cost and real turn-end damage", () => StdioTrajectory("wriggler"));
        yield return ("MOVE Whistle real stdio pays two, exhausts, skips one action and restores the selected first move", () => StdioTrajectory("whistle"));
        yield return ("IC real stdio explicit target selects the second enemy", () => StdioTrajectory("explicit-target"));
        yield return ("IC real stdio mandatory hand choice resumes once after HP loss", () => StdioTrajectory("hand-exhaust"));
        yield return ("IC real stdio X card captures and pays three energy", () => StdioTrajectory("whirlwind"));
        yield return ("IC real stdio Rupture reacts after the damaging card", () => StdioTrajectory("rupture"));
        yield return ("MODEL real stdio SelfFormingClay publishes a deferred Power, then block and removal", () => StdioTrajectory("self-forming-clay"));
        yield return ("IS real stdio initializes the source Ironclad deck and draws five combat copies", () => StdioTrajectory("ironclad-start"));
        yield return ("CR real stdio starter cards defeat an enemy and acquire one of three permanent rewards", () => StdioTrajectory("card-reward-claim"));
        yield return ("CR real stdio skips card rewards and closes without acquiring a card", () => StdioTrajectory("card-reward-skip"));
        yield return ("RL real stdio pickup relics publish permanent HP and gold gains", () => StdioTrajectory("relic-pickups"));
        yield return ("RL real stdio five simple pickups expose exact resources and removal preserves permanent gains", () => StdioTrajectory("simple-resources"));
        yield return ("RL real stdio third attack triggers OrnamentalFan exactly once", () => StdioTrajectory("relic-counter"));
        yield return ("RM real stdio StrikeDummy and FakeStrikeDummy add damage before block", () => StdioTrajectory("relic-strike"));
        yield return ("RM real stdio IceCream carries energy and ArtOfWar rewards the previous empty turn", () => StdioTrajectory("relic-energy"));
        yield return ("TC real stdio five turn-start relics expose exact energy, block and cross-combat flower progress", () => StdioTrajectory("turn-start-resources"));
        yield return ("AM real stdio MiniatureCannon and PaperPhrog enhance a permanent upgraded card against real Vulnerable", () => StdioTrajectory("cannon-phrog"));
        yield return ("AM real stdio PaperKrane reduces actual weak enemy attacks before and after its Strength buff", () => StdioTrajectory("paper-krane"));
        yield return ("CX real stdio ChemicalX increases effect hits at three and zero energy without changing raw captured X", () => StdioTrajectory("chemical-x"));
        foreach (var relic in new[] { "whetstone", "war-paint", "sand-castle" })
            yield return ("UP real stdio permanent pickup upgrade and actual combat copy: " + relic, () => StdioTrajectory("pickup-upgrade-" + relic));
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
    private static PublicSnapshotDto Snapshot(ResponseDto response)
    {
        var snapshot = PublicSnapshotDto.FromWire(Field(response, "snapshot"), NetProtocol.V02);
        BaseDslPileChecks.CheckSnapshot(snapshot);
        return snapshot;
    }

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
        var published = PublicCatalogDto.FromWire(catalog.Result!, NetProtocol.V02);
        using var metadata = System.Text.Json.JsonDocument.Parse(File.ReadAllText(
            Path.Combine(Path.GetDirectoryName(Manifest)!, "public/metadata.json")));
        var expectedContent = metadata.RootElement.GetProperty("content").EnumerateObject()
            .SelectMany(kind => kind.Value.GetProperty("content_ids").EnumerateArray()
                .Select(id => (Id: id.GetString()!, Kind: "net:kind:" + kind.Name)))
            .ToArray();
        Check.Sequence(expectedContent.Select(row => row.Id).Distinct().Order(StringComparer.Ordinal),
            published.Declarations.ContentTypes.Select(row => row.Id).Order(StringComparer.Ordinal),
            "public catalog retains all declared content IDs");
        foreach (var row in published.Declarations.ContentTypes)
            Check.Sequence(expectedContent.Where(expected => expected.Id == row.Id).Select(expected => expected.Kind).Order(StringComparer.Ordinal),
                row.KindIds.Order(StringComparer.Ordinal), "content kind membership comes from public policy");
        var catalogJson = WireCanonical.Text(catalog.Result!);
        foreach (var privateKey in new[] { "source_path", "source_sha256", "source_baseline", "eligibility", "evidence", "definition" })
            Check.False(catalogJson.Contains("\"" + privateKey + "\"", StringComparison.Ordinal), "source and implementation fields stay outside public catalog");
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
            ResponseDto Send(RequestDto request, bool expectSuccess = true)
            {
                process.StandardInput.WriteLine(WireCanonical.Text(request.ToWire())); process.StandardInput.Flush();
                var line = process.StandardOutput.ReadLineAsync();
                Check.True(line.Wait(mode.StartsWith("normal-", StringComparison.Ordinal) ? 60000 : 10000), "stdio responds within the check timeout");
                Check.True(line.Result is not null, "stdio frame exists");
                var response = ResponseDto.FromWire(WireParser.ParseUtf8Strict(Encoding.UTF8.GetBytes(line.Result!), "stdio-test"));
                Check.Equal(expectSuccess, response.Ok, "stdio: " + response.Error?.Message); return response;
            }
            Send(Request("pipe-describe", Operation.Describe));
            Send(Request("pipe-catalog", Operation.GetCatalog, ("catalog_id", WireValue.Str(host.Profile.CatalogId))));
            var identity = new IdentityDto(NetProtocol.V02, host.Profile.RulesetId, host.Profile.CatalogId, host.Profile.BackendProfileId).ToWire();
            var start = Send(Request("pipe-start", Operation.Start, ("expected_identity", identity), ("scenario_profile", WireValue.Str(host.SupportedScenarioProfile)), ("seed", WireValue.Str(mode.StartsWith("normal-dense-", StringComparison.Ordinal) ? "dense-normal-30" : mode.StartsWith("normal-sapphire-", StringComparison.Ordinal) ? "sapphire-normal-79" : mode == "normal-event-unsupported" ? "morphic-normal-13" : mode == "normal-event" ? "event-normal-11" : mode == "normal-unknown" ? "unknown-normal-354" : mode.StartsWith("normal-", StringComparison.Ordinal) ? "777" : "BASE-DSL-STDIO-00"))));
            var sessionId = ((WireString)Field(start, "session_id")).Value; var snapshot = Snapshot(start);
            (string Action, string Block, bool Active, string Energy, string Turn, bool TurnOpen, string Target)[] sequence = mode switch
            {
                "normal-opening" or "normal-claim" or "normal-skip" or "normal-unknown" or "normal-event" or "normal-event-unsupported" or "normal-sapphire-eat" or "normal-sapphire-plant" or "normal-dense-trudge" or "normal-dense-fight-skip" or "normal-dense-fight-claim" or "battleworn-event" or "normal-neow-boots" or "normal-neow-pearl" or "flex-potion" or "draw-permission" => [],
                "whistle" => [
                    ("FuzzyWurmCrawler.Spawn", "0", false, "0", "0", false, "55"),
                    ("Whistle.AddToHand", "0", false, "0", "0", false, "55"),
                    ("BeginDevelopmentCombat", "0", true, "0", "0", false, "55"),
                    ("BeginDevelopmentTurn", "0", true, "3", "1", true, "55"),
                    ("PlayAttackFromHand", "0", true, "1", "1", true, "22"),
                    ("EndDevelopmentTurn", "0", true, "1", "1", false, "22"),
                    ("BeginDevelopmentTurn", "0", true, "3", "2", true, "22"),
                    ("EndDevelopmentTurn", "0", true, "3", "2", false, "22"),
                    ("BeginDevelopmentTurn", "0", true, "3", "3", true, "22")],
                "wriggler" => [
                    ("Wriggler.Spawn", "0", false, "0", "0", false, ""),
                    ("BeginDevelopmentCombat", "0", true, "0", "0", false, ""),
                    ("BeginDevelopmentTurn", "0", true, "3", "1", true, ""),
                    ("Infection.AddToHand", "0", true, "3", "1", true, ""),
                    ("EndDevelopmentTurn", "0", true, "3", "1", false, ""),
                    ("BeginDevelopmentTurn", "0", true, "3", "2", true, ""),
                    ("EndDevelopmentTurn", "0", true, "3", "2", false, ""),
                    ("BeginDevelopmentTurn", "0", true, "3", "3", true, ""),
                    ("EndDevelopmentTurn", "0", true, "3", "3", false, ""),
                    ("BeginDevelopmentTurn", "0", true, "3", "4", true, "")],
                "battle-friend" => [
                    ("BattleFriendV3.Spawn", "0", false, "0", "0", false, "300"),
                    ("BeginDevelopmentCombat", "0", true, "0", "0", false, "300"),
                    ("BeginDevelopmentTurn", "0", true, "3", "1", true, "300"),
                    ("EndDevelopmentTurn", "0", true, "3", "1", false, "300"),
                    ("BeginDevelopmentTurn", "0", true, "3", "2", true, "300"),
                    ("EndDevelopmentTurn", "0", true, "3", "2", false, "300"),
                    ("BeginDevelopmentTurn", "0", true, "3", "3", true, "300"),
                    ("EndDevelopmentTurn", "0", false, "3", "3", false, "300")],
                "sown-event" => [
                    ("Strike.Obtain", "0", false, "0", "0", false, ""),
                    ("Defend.Obtain", "0", false, "0", "0", false, ""),
                    ("EnterDevelopmentSapphireSeed", "0", false, "0", "0", false, "")],
                "self-forming-clay" => [
                    ("SelfFormingClay.Obtain", "0", false, "0", "0", false, ""),
                    ("Hemokinesis.AddToHand", "0", false, "0", "0", false, ""),
                    ("AddDevelopmentTarget", "0", false, "0", "0", false, "40"),
                    ("BeginDevelopmentCombat", "0", true, "0", "0", false, "40"),
                    ("BeginDevelopmentTurn", "0", true, "3", "1", true, "40"),
                    ("PlayAttackFromHand", "0", true, "2", "1", true, "25"),
                    ("EndDevelopmentTurn", "0", true, "2", "1", false, "25"),
                    ("BeginDevelopmentTurn", "3", true, "3", "2", true, "25")],
                "cannon-phrog" => [
                    ("MiniatureCannon.Obtain", "0", false, "0", "0", false, ""),
                    ("PaperPhrog.Obtain", "0", false, "0", "0", false, ""),
                    ("Strike.Obtain", "0", false, "0", "0", false, ""),
                    ("Whetstone.Obtain", "0", false, "0", "0", false, ""),
                    ("AddDevelopmentTarget", "0", false, "0", "0", false, "40"),
                    ("BeginDevelopmentCombat", "0", true, "0", "0", false, "40"),
                    ("BeginDevelopmentTurn", "0", true, "3", "1", true, "40"),
                    ("Bash.AddToHand", "0", true, "3", "1", true, "40"),
                    ("PlayAttackFromHand", "0", true, "1", "1", true, "32"),
                    ("Draw", "0", true, "1", "1", true, "32"),
                    ("PlayAttackFromHand", "0", true, "0", "1", true, "11")],
                "paper-krane" => [
                    ("PaperKrane.Obtain", "0", false, "0", "0", false, ""),
                    ("FuzzyWurmCrawler.Spawn", "0", false, "0", "0", false, "55"),
                    ("BeginDevelopmentCombat", "0", true, "0", "0", false, "55"),
                    ("BeginDevelopmentTurn", "0", true, "3", "1", true, "55"),
                    ("Shockwave.AddToHand", "0", true, "3", "1", true, "55"),
                    ("PlayCardFromHand", "0", true, "1", "1", true, "55"),
                    ("EndDevelopmentTurn", "0", true, "1", "1", false, "55"),
                    ("BeginDevelopmentTurn", "0", true, "3", "2", true, "55"),
                    ("EndDevelopmentTurn", "0", true, "3", "2", false, "55"),
                    ("BeginDevelopmentTurn", "0", true, "3", "3", true, "55"),
                    ("EndDevelopmentTurn", "0", true, "3", "3", false, "55")],
                "turn-start-resources" => [
                    ("Candelabra.Obtain", "0", false, "0", "0", false, ""),
                    ("Chandelier.Obtain", "0", false, "0", "0", false, ""),
                    ("VeryHotCocoa.Obtain", "0", false, "0", "0", false, ""),
                    ("FakeHappyFlower.Obtain", "0", false, "0", "0", false, ""),
                    ("Sai.Obtain", "0", false, "0", "0", false, ""),
                    ("AddDevelopmentTarget", "0", false, "0", "0", false, "40"),
                    ("BeginDevelopmentCombat", "0", true, "0", "0", false, "40"),
                    ("BeginDevelopmentTurn", "7", true, "7", "1", true, "40"),
                    ("EndDevelopmentTurn", "7", true, "7", "1", false, "40"),
                    ("BeginDevelopmentTurn", "7", true, "5", "2", true, "40"),
                    ("EndDevelopmentTurn", "7", true, "5", "2", false, "40"),
                    ("BeginDevelopmentTurn", "7", true, "6", "3", true, "40"),
                    ("EndDevelopmentTurn", "7", true, "6", "3", false, "40"),
                    ("BeginDevelopmentTurn", "7", true, "3", "4", true, "40"),
                    ("EndDevelopmentCombat", "7", false, "3", "4", false, "40"),
                    ("BeginDevelopmentCombat", "0", true, "0", "0", false, "40"),
                    ("BeginDevelopmentTurn", "7", true, "8", "1", true, "40"),
                    ("EndDevelopmentTurn", "7", true, "8", "1", false, "40"),
                    ("BeginDevelopmentTurn", "7", true, "5", "2", true, "40")],
                "simple-resources" => [
                    ("FakeMango.Obtain", "0", false, "0", "0", false, ""),
                    ("LoomingFruit.Obtain", "0", false, "0", "0", false, ""),
                    ("NutritiousOyster.Obtain", "0", false, "0", "0", false, ""),
                    ("SignetRing.Obtain", "0", false, "0", "0", false, ""),
                    ("GoldenPearl.Obtain", "0", false, "0", "0", false, "")],
                "pickup-upgrade-whetstone" or "pickup-upgrade-war-paint" or "pickup-upgrade-sand-castle" => [
                    ("Strike.Obtain", "0", false, "0", "0", false, ""),
                    ("Defend.Obtain", "0", false, "0", "0", false, ""),
                    (mode == "pickup-upgrade-whetstone" ? "Whetstone.Obtain" : mode == "pickup-upgrade-war-paint" ? "WarPaint.Obtain" : "SandCastle.Obtain", "0", false, "0", "0", false, ""),
                    ("AddDevelopmentTarget", "0", false, "0", "0", false, "40"),
                    ("BeginDevelopmentCombat", "0", true, "0", "0", false, "40"),
                    ("Draw", "0", true, "0", "0", false, "40"),
                    ("Draw", "0", true, "0", "0", false, "40"),
                    ("BeginDevelopmentTurn", "0", true, "3", "1", true, "40"),
                    ("PlayAttackFromHand", "0", true, "2", "1", true, mode == "pickup-upgrade-war-paint" ? "34" : "31"),
                    ("PlayCardFromHand", mode == "pickup-upgrade-whetstone" ? "5" : "8", true, "1", "1", true, mode == "pickup-upgrade-war-paint" ? "34" : "31")],
                "chemical-x" => [
                    ("ChemicalX.Obtain", "0", false, "0", "0", false, ""),
                    ("AddDevelopmentTarget", "0", false, "0", "0", false, "40"),
                    ("BeginDevelopmentCombat", "0", true, "0", "0", false, "40"),
                    ("BeginDevelopmentTurn", "0", true, "3", "1", true, "40"),
                    ("Whirlwind.AddToHand", "0", true, "3", "1", true, "40"),
                    ("PlayCardFromHand", "0", true, "0", "1", true, "15"),
                    ("Whirlwind.AddToHand", "0", true, "0", "1", true, "15"),
                    ("PlayCardFromHand", "0", true, "0", "1", true, "5")],
                "relic-strike" => [
                    ("StrikeDummy.Obtain", "0", false, "0", "0", false, ""),
                    ("FakeStrikeDummy.Obtain", "0", false, "0", "0", false, ""),
                    ("AddDevelopmentTarget", "0", false, "0", "0", false, "40"),
                    ("BeginDevelopmentCombat", "0", true, "0", "0", false, "40"),
                    ("BeginDevelopmentTurn", "0", true, "3", "1", true, "40"),
                    ("Strike.AddToHand", "0", true, "3", "1", true, "40"),
                    ("PlayAttackFromHand", "0", true, "2", "1", true, "30")],
                "relic-energy" => [
                    ("IceCream.Obtain", "0", false, "0", "0", false, ""),
                    ("ArtOfWar.Obtain", "0", false, "0", "0", false, ""),
                    ("AddDevelopmentTarget", "0", false, "0", "0", false, "40"),
                    ("BeginDevelopmentCombat", "0", true, "0", "0", false, "40"),
                    ("BeginDevelopmentTurn", "0", true, "3", "1", true, "40"),
                    ("EndDevelopmentTurn", "0", true, "3", "1", false, "40"),
                    ("BeginDevelopmentTurn", "0", true, "7", "2", true, "40"),
                    ("Strike.AddToHand", "0", true, "7", "2", true, "40"),
                    ("PlayAttackFromHand", "0", true, "6", "2", true, "34"),
                    ("EndDevelopmentTurn", "0", true, "6", "2", false, "34"),
                    ("BeginDevelopmentTurn", "0", true, "9", "3", true, "34")],
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
                "tough-bandages" => [
                    ("ToughBandages.Obtain", "0", false, "0", "0", false, ""),
                    ("BeginDevelopmentCombat", "0", true, "0", "0", false, ""),
                    ("BeginDevelopmentTurn", "0", true, "3", "1", true, ""),
                    ("Strike.AddToHand", "0", true, "3", "1", true, ""),
                    ("Defend.AddToHand", "0", true, "3", "1", true, ""),
                    ("DiscardHand", "6", true, "3", "1", true, ""),
                    ("Strike.AddToHand", "6", true, "3", "1", true, ""),
                    ("EndDevelopmentTurn", "6", true, "3", "1", false, ""),
                    ("BeginDevelopmentTurn", "0", true, "3", "2", true, "")],

                "charons-ashes" => [
                    ("AddDevelopmentTarget", "0", false, "0", "0", false, "40"),
                    ("CharonsAshes.Obtain", "0", false, "0", "0", false, "40"),
                    ("Impervious.AddToHand", "0", false, "0", "0", false, "40"),
                    ("BeginDevelopmentCombat", "0", true, "0", "0", false, "40"),
                    ("BeginDevelopmentTurn", "0", true, "3", "1", true, "40"),
                    ("PlayCardFromHand", "30", true, "1", "1", true, "37")],
                "charons-ashes-kill" => [
                    ("TwigSlimeS.Spawn", "0", false, "0", "0", false, "7"),
                    ("CharonsAshes.Obtain", "0", false, "0", "0", false, "7"),
                    ("Strike.AddToHand", "0", false, "0", "0", false, "7"),
                    ("Impervious.AddToHand", "0", false, "0", "0", false, "7"),
                    ("BeginDevelopmentCombat", "0", true, "0", "0", false, "7"),
                    ("BeginDevelopmentTurn", "0", true, "3", "1", true, "7"),
                    ("PlayAttackFromHand", "0", true, "2", "1", true, "1"),
                    ("PlayCardFromHand", "30", false, "0", "1", false, "0")],
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
                    ("EndDevelopmentTurn", "0", true, "3", "1", false, "55"),
                    ("BeginDevelopmentTurn", "0", true, "3", "2", true, "55")],
                "thunderclap" => [
                    ("AddDevelopmentTarget", "0", false, "0", "0", false, "40"), ("Thunderclap.AddToHand", "0", false, "0", "0", false, "40"),
                    ("BeginDevelopmentCombat", "0", true, "0", "0", false, "40"),
                    ("BeginDevelopmentTurn", "0", true, "3", "1", true, "40"),
                    ("PlayCardFromHand", "0", true, "2", "1", true, "36")],
                _ => [("TheAbacus.Obtain", "0", false, "0", "0", false, ""), ("Draw", "6", false, "0", "0", false, "")],
            };
            var stepNumber = 0;
            var wrigglerInitialMoves = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var step in sequence)
            {
                var candidates = snapshot.LegalActions.Where(a => a.ActionType.EndsWith(":" + step.Action, StringComparison.Ordinal)).ToArray();
                var candidate = mode == "hand-exhaust" && step.Action == "PlayCardFromHand"
                    ? candidates.Single(a => a.Fields.Any(f => f.Value is WireString value && value.Value == snapshot.Entities.Single(e => e.ContentId == "base-spire:brand").InstanceId))
                    : candidates.Single();
                var suffix = (++stepNumber).ToString(System.Globalization.CultureInfo.InvariantCulture);
                var priorObservation = snapshot.ObservationId;
                Send(Request("pipe-step-" + suffix, Operation.Step, ("session_id", WireValue.Str(sessionId)), ("expected_observation_id", WireValue.Integer(snapshot.ObservationId)), ("candidate_id", WireValue.Str(candidate.CandidateId))));
                snapshot = Snapshot(Send(Request("pipe-observe-" + suffix, Operation.Observe, ("session_id", WireValue.Str(sessionId)))));
                Check.Equal(priorObservation + 1, snapshot.ObservationId, "real stdio publishes a new version for every completed control boundary, including a pause without field writes");
                Check.Equal(step.Block, Public(snapshot, "Player.Block"), "real host exposes the expected block at every step");
                Check.Equal(step.Energy, Public(snapshot, "Player.Energy"), "public energy follows the DSL turn cycle");
                Check.Equal(step.Turn, Public(snapshot, "Player.TurnNumber"), "public turn counter follows the DSL turn cycle");
                if (step.Target.Length > 0) Check.Equal(step.Target, Public(snapshot, "Enemy.Hp"), "public development target HP follows the real damage result");
                if (mode == "wurm" && step.Action == "EndDevelopmentTurn") Check.Equal("attack", Public(snapshot, "Enemy.Intent"), "enemy end does not prepare the next move");
                if (mode == "whistle")
                {
                    var skipped = step.Action == "PlayAttackFromHand" || step.Action == "EndDevelopmentTurn" && step.Turn == "1";
                    var move = step.Turn == "0" ? "none" : skipped ? "STUNNED" : step.Turn == "3" ? "INHALE" : "FIRST_ACID_GOOP";
                    var intent = step.Turn == "0" ? "none" : skipped ? "stun" : step.Turn == "3" ? "buff" : "attack";
                    Check.Equal(move, Public(snapshot, "Enemy.MoveId"), "public move is prepared only at the player start boundary");
                    Check.Equal(intent, Public(snapshot, "Enemy.Intent"), "public intent follows the same saved move");
                    Check.Equal(step.Turn == "3" || step.Action == "EndDevelopmentTurn" && step.Turn == "2" ? "76" : "80", Public(snapshot, "Player.Hp"), "skipped turn does no damage; resumed attack deals four");
                    foreach (var card in snapshot.Entities.Where(e => e.ContentId == "base-spire:whistle"))
                        Check.Equal("2", ((WireString)card.Fields.Single(f => f.FieldId.EndsWith(":Card.Cost", StringComparison.Ordinal)).Value!).Value, "locked source cost is public");
                    BaseDslPileChecks.CheckSnapshot(snapshot);
                }
                if (mode == "wriggler")
                {
                    var enemies = snapshot.Entities.Where(e => e.ContentId == "base-spire:wriggler").ToArray();
                    Check.Equal(4, enemies.Length, "four actual public enemies");
                    string Text(EntityDto entity, string name) => ((WireString)entity.Fields.Single(f => f.FieldId.EndsWith(":" + name, StringComparison.Ordinal)).Value!).Value;
                    var completed = int.Parse(step.Turn, System.Globalization.CultureInfo.InvariantCulture) - (step.TurnOpen ? 1 : 0);
                    if (step.Turn == "0") completed = 0;
                    if (wrigglerInitialMoves.Count == 0 && step.Turn != "0")
                    {
                        foreach (var enemy in enemies) wrigglerInitialMoves.Add(enemy.InstanceId, Text(enemy, "Enemy.MoveId"));
                        Check.Equal(2, wrigglerInitialMoves.Values.Count(m => m == "NASTY_BITE_MOVE"), "two initial bites");
                        Check.Equal(2, wrigglerInitialMoves.Values.Count(m => m == "WRIGGLE_MOVE"), "two initial wriggles");
                    }
                    foreach (var enemy in enemies)
                    {
                        if (step.Turn == "0")
                        {
                            Check.Equal("none", Text(enemy, "Enemy.MoveId"), "batch creation and graph initialization do not choose the first move");
                            Check.Equal("none", Text(enemy, "Enemy.Intent"), "no intent before the first ordinary turn");
                            continue;
                        }
                        var preparedTurn = int.Parse(step.Turn, System.Globalization.CultureInfo.InvariantCulture) - 1;
                        var bite = (wrigglerInitialMoves[enemy.InstanceId] == "NASTY_BITE_MOVE") == (preparedTurn % 2 == 0);
                        Check.Equal(bite ? "NASTY_BITE_MOVE" : "WRIGGLE_MOVE", Text(enemy, "Enemy.MoveId"), "public instance keeps its saved move");
                        Check.Equal(bite ? "attack" : "buff,status", Text(enemy, "Enemy.Intent"), "source intent order");
                    }
                    foreach (var card in snapshot.Entities.Where(e => e.ContentId == "base-spire:infection"))
                    {
                        Check.Equal("-1", Text(card, "Card.Cost"), "public source negative cost");
                        Check.False(((WireBool)card.Fields.Single(f => f.FieldId.EndsWith(":Card.CanUpgrade", StringComparison.Ordinal)).Value!).Value, "no status upgrade");
                        Check.False(snapshot.LegalActions.Where(a => a.ActionType.EndsWith(":PlayCardFromHand", StringComparison.Ordinal) || a.ActionType.EndsWith(":UpgradeCardInHand", StringComparison.Ordinal))
                            .Any(a => a.Fields.Any(f => f.Value is WireString value && value.Value == card.InstanceId)), "no play or upgrade candidate for Infection");
                    }
                    Check.Equal(new[] { "80", "65", "49", "33" }[completed], Public(snapshot, "Player.Hp"), "real Infection and powered monster damage");
                    Check.False(WireCanonical.Text(snapshot.ToWire()).Contains("StartStunned", StringComparison.Ordinal), "birth condition state stays hidden");
                    BaseDslPileChecks.CheckSnapshot(snapshot);
                }
                if (mode == "battle-friend")
                {
                    var expired = step.Action == "EndDevelopmentTurn" && step.Turn == "3";
                    Check.Equal(expired, PublicBool(snapshot, "Enemy.Escaped"), "living escape is public at exact third side end");
                    Check.Equal(expired, PublicBool(snapshot, "Player.CombatTimedOut"), "timeout remains separate from victory");
                    Check.Equal(step.Turn == "0" ? "none" : "IDLE", Public(snapshot, "Enemy.MoveId"), "fixed move is prepared at the first player start and then remains visible");
                    var powers = snapshot.Entities.Where(e => e.ContentId == "base-spire:battleworn-dummy-time-limit-power").ToArray();
                    Check.Equal(expired || step.Action.EndsWith(".Spawn", StringComparison.Ordinal) ? 0 : 1, powers.Length, "room-entry timer removed before final escape snapshot");
                    if (powers.Length > 0)
                        Check.Equal((4 - int.Parse(step.Turn) - (step.TurnOpen ? 0 : 1)).ToString(System.Globalization.CultureInfo.InvariantCulture), Public(snapshot, "Power.Stacks"), "actual public countdown");
                    Check.False(WireCanonical.Text(snapshot.ToWire()).Contains("SpawnOrder", StringComparison.Ordinal), "listener order stays hidden");
                }
                if (mode == "self-forming-clay")
                {
                    var powers = snapshot.Entities.Where(e => e.Fields.Any(f => f.FieldId.EndsWith(":Power.Stacks", StringComparison.Ordinal))).ToArray();
                    var pending = step.Action is "PlayAttackFromHand" or "EndDevelopmentTurn";
                    Check.Equal(pending ? 1 : 0, powers.Length, "deferred Power appears and is removed through public output");
                    if (pending) { Check.Equal("base-spire:self-forming-clay", powers.Single().ContentId, "public Power content identity"); Check.Equal("3", Public(snapshot, "Power.Stacks"), "public pending block"); }
                    Check.False(WireCanonical.Text(snapshot.ToWire()).Contains("Power.Definition\"", StringComparison.Ordinal), "blueprint reference stays hidden");
                }
                if (mode == "paper-krane" && step.Action == "EndDevelopmentTurn") Check.Equal(step.Turn == "3" ? "72" : "78", Public(snapshot, "Player.Hp"), "actual weak attacks use .60, including Strength before the third-turn attack");
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
            if (mode is "exhaust" or "inflame")
            {
                var content = mode == "exhaust" ? "base-spire:impervious" : "base-spire:inflame";
                var resolved = snapshot.Entities.Single(e => e.ContentId == content).InstanceId;
                var destination = BaseDslPileChecks.Pile(snapshot, mode == "exhaust" ? "ExhaustPile" : "RemovedFromCombat");
                Check.Equal(1, destination.TotalCount, "the resolved card has exactly one destination membership");
                Check.True(destination.VisibleMembers.Contains(resolved), "the actual resolved instance is published in its destination");
                Check.False(BaseDslPileChecks.Pile(snapshot, "Hand").VisibleMembers.Contains(resolved)
                    || BaseDslPileChecks.Pile(snapshot, "DiscardPile").VisibleMembers.Contains(resolved)
                    || BaseDslPileChecks.Pile(snapshot, "InPlay").VisibleMembers.Contains(resolved), "resolution removes the card from its previous piles");
            }
            if (mode == "chemical-x")
            {
                Check.True(snapshot.Entities.Any(e => e.ContentId == "base-spire:chemical-x"), "real ChemicalX is public");
                var captured = snapshot.Entities.Where(e => e.ContentId == "base-spire:whirlwind")
                    .Select(e => ((WireString)e.Fields.Single(f => f.FieldId.EndsWith(":Card.PaidX", StringComparison.Ordinal)).Value!).Value).Order().ToArray();
                Check.True(captured.SequenceEqual(new[] { "0", "3" }), "public captures remain zero and three despite two additional hits each");
            }
            if (mode == "battleworn-event")
            {
                string EntityValue(EntityDto entity, string field) => ((WireString)entity.Fields.Single(f => f.FieldId.EndsWith(":" + field, StringComparison.Ordinal)).Value!).Value;
                void StepEvent(LegalActionDto candidate)
                {
                    var prior = snapshot.ObservationId;
                    var request = Request("dummy-" + ++stepNumber, Operation.Step, ("session_id", WireValue.Str(sessionId)),
                        ("expected_observation_id", WireValue.Integer(prior)), ("candidate_id", WireValue.Str(candidate.CandidateId)));
                    var response = Send(request);
                    Check.Equal(WireCanonical.Text(response.ToWire()), WireCanonical.Text(Send(request).ToWire()), "event request replays cached result without repeating effects");
                    snapshot = Snapshot(Send(Request("dummy-observe-" + stepNumber, Operation.Observe, ("session_id", WireValue.Str(sessionId)))));
                    Check.Equal(prior + 1, snapshot.ObservationId, "event boundary advances observation once");
                    Send(Request("dummy-stale-" + stepNumber, Operation.Step, ("session_id", WireValue.Str(sessionId)),
                        ("expected_observation_id", WireValue.Integer(prior)), ("candidate_id", WireValue.Str(candidate.CandidateId))), false);
                }
                void Action(string name) => StepEvent(snapshot.LegalActions.Single(a => a.ActionType == "net:action:base-spire:" + name));
                Action("Ironclad.Initialize"); Action("EnterDevelopmentBattlewornDummy");
                var parent = snapshot.Entities.Single(e => e.KindId == "net:kind:base-spire:EventVisit");
                var options = snapshot.Entities.Where(e => e.KindId == "net:kind:base-spire:BattlewornOption").ToArray();
                Check.Sequence(new[] { "SETTING_1", "SETTING_2", "SETTING_3" }, options.Select(e => EntityValue(e, "BattlewornOption.Setting")), "complete source options stay public");
                Check.Equal(3, snapshot.LegalActions.Count, "three real answers");
                var selected = options.Single(e => EntityValue(e, "BattlewornOption.Setting") == "SETTING_2");
                StepEvent(snapshot.LegalActions.Single(a => a.Fields.Any(f => f.Value is WireString v && v.Value == selected.InstanceId)));
                var child = snapshot.Entities.Single(e => e.KindId == "net:kind:base-spire:EventCombatVisit");
                Check.Equal(parent.InstanceId, EntityValue(child, "EventCombatVisit.Parent"), "child retains actual parent");
                Check.Equal(child.InstanceId, EntityValue(snapshot.Entities.Single(e => e.InstanceId == parent.InstanceId), "EventVisit.CombatVisit"), "parent retains actual child");
                Check.Equal("150", Public(snapshot, "Enemy.Hp"), "real tier-two source enemy");
                Check.False(snapshot.LegalActions.Any(a => a.ActionType is "net:action:base-spire:EndDevelopmentCombat" or "net:action:base-spire:Draw" or "net:action:base-spire:CloseDevelopmentEvent"), "cannot bypass real combat");
                for (var turn = 1; turn <= 3; turn++) Action("EndTurn");
                var finished = snapshot.Entities.Single(e => e.InstanceId == parent.InstanceId);
                child = snapshot.Entities.Single(e => e.InstanceId == child.InstanceId);
                Check.Equal("finished", EntityValue(finished, "EventVisit.Status"), "same parent finishes after timeout");
                Check.Equal("closed", EntityValue(child, "EventCombatVisit.Status"), "child settles once");
                Check.True(((WireBool)child.Fields.Single(f => f.FieldId.EndsWith(":EventCombatVisit.TimedOut", StringComparison.Ordinal)).Value!).Value, "real timeout published");
                Check.Equal("event", Public(snapshot, "Player.Room"), "returns to event before explicit close");
                Check.Equal(10, snapshot.Entities.Count(e => e.KindId == "net:kind:base-spire:Card"), "combat copies cleared, permanent deck retained");
                Check.False(snapshot.LegalActions.Any(a => a.ActionType is "net:action:base-spire:ChooseMapPoint" or "net:action:base-spire:CloseDevelopmentRewards"), "no ordinary rewards or fake map");
                Check.False(WireCanonical.Text(snapshot.ToWire()).Contains("EventVisit.Sequence", StringComparison.Ordinal), "hidden stream sequence stays private");
                Action("CloseDevelopmentEvent"); Check.Equal("none", Public(snapshot, "Player.Room"), "development close returns to its own location");
                Check.False(snapshot.LegalActions.Any(a => a.ActionType == "net:action:base-spire:CloseDevelopmentEvent"), "cannot repeat close");
            }
            if (mode == "draw-permission")
            {
                void Action(string name, string? content = null)
                {
                    var candidates = snapshot.LegalActions.Where(a => a.ActionType == "net:action:base-spire:" + name);
                    var candidate = content is null ? candidates.Single() : candidates.First(a => a.Fields.Any(f => f.Value is WireString v
                        && snapshot.Entities.Single(e => e.InstanceId == v.Value).ContentId == content));
                    var id = "pipe-draw-permission-" + ++stepNumber;
                    var request = Request(id, Operation.Step, ("session_id", WireValue.Str(sessionId)),
                        ("expected_observation_id", WireValue.Integer(snapshot.ObservationId)), ("candidate_id", WireValue.Str(candidate.CandidateId)));
                    var result = Send(request);
                    Check.Equal(WireCanonical.Text(result.ToWire()), WireCanonical.Text(Send(request).ToWire()), "draw step replay returns cached response");
                    snapshot = Snapshot(Send(Request(id + "-observe", Operation.Observe, ("session_id", WireValue.Str(sessionId)))));
                }
                Action("Ironclad.Initialize"); Action("AddDevelopmentTarget"); Action("BeginDevelopmentCombat"); Action("BeginDevelopmentTurn");
                Check.False(PublicBool(snapshot, "Player.DrawBlocked"), "ordinary opening allows effects");
                Action("BattleTrance.AddToHand"); Action("PlayCardFromHand", "base-spire:battle-trance");
                Check.Equal(8, BaseDslPileChecks.Pile(snapshot, "Hand").VisibleMembers.Count, "BattleTrance draws three before applying NoDraw");
                Check.True(PublicBool(snapshot, "Player.DrawBlocked"), "real NoDraw veto is public");
                Action("Draw"); Check.Equal(8, BaseDslPileChecks.Pile(snapshot, "Hand").VisibleMembers.Count, "effect draw is actually blocked");
                Action("EndDevelopmentTurn"); Check.False(PublicBool(snapshot, "Player.DrawBlocked"), "NoDraw expires at player-side end");
                Action("BeginDevelopmentTurn"); Check.Equal(5, BaseDslPileChecks.Pile(snapshot, "Hand").VisibleMembers.Count, "next actual turn draws five");
                Action("BattleTrance.AddToHand"); Action("PlayCardFromHand", "base-spire:battle-trance");
                Check.True(PublicBool(snapshot, "Player.DrawBlocked"), "second application publishes its veto");
                Action("EndDevelopmentCombat"); Check.False(PublicBool(snapshot, "Player.DrawBlocked"), "combat cleanup clears published veto");
            }
            if (mode.StartsWith("normal-neow-", StringComparison.Ordinal) || mode == "flex-potion")
            {
                string Text(EntityDto entity, string field) => ((WireString)entity.Fields.Single(f => f.FieldId.EndsWith(":" + field, StringComparison.Ordinal)).Value!).Value;
                void StepEffect(LegalActionDto candidate)
                {
                    var prior = snapshot.ObservationId;
                    var request = Request("effect-" + ++stepNumber, Operation.Step, ("session_id", WireValue.Str(sessionId)),
                        ("expected_observation_id", WireValue.Integer(prior)), ("candidate_id", WireValue.Str(candidate.CandidateId)));
                    var response = Send(request);
                    Check.Equal(WireCanonical.Text(response.ToWire()), WireCanonical.Text(Send(request).ToWire()), "replay does not repeat acquisition, consumption or expiry");
                    snapshot = Snapshot(Send(Request("effect-observe-" + stepNumber, Operation.Observe, ("session_id", WireValue.Str(sessionId)))));
                    Check.Equal(prior + 1, snapshot.ObservationId, "one public boundary per actual operation");
                    Send(Request("effect-stale-" + stepNumber, Operation.Step, ("session_id", WireValue.Str(sessionId)),
                        ("expected_observation_id", WireValue.Integer(prior)), ("candidate_id", WireValue.Str(candidate.CandidateId))), false);
                }
                void Action(string name) => StepEffect(snapshot.LegalActions.Single(a => a.ActionType == "net:action:base-spire:" + name));
                if (mode == "flex-potion")
                {
                    Action("Ironclad.Initialize"); Action("BattleFriendV2.Spawn"); Action("BeginDevelopmentCombat"); Action("BeginDevelopmentTurn");
                    for (var i = 1; i <= 2; i++)
                    {
                        Action("FlexPotion.Obtain"); Action("UsePotion");
                        foreach (var id in new[] { "base-spire:strength-power", "base-spire:flex-potion-power" })
                            Check.Equal((5 * i).ToString(), Text(snapshot.Entities.Single(e => e.ContentId == id), "Power.Stacks"), "public temporary and actual Strength stack together");
                        Check.False(snapshot.Entities.Any(e => e.ContentId == "base-spire:flex-potion"), "used potion is consumed once");
                    }
                    var strike = BaseDslPileChecks.Pile(snapshot, "Hand").VisibleMembers.First(id => snapshot.Entities.Single(e => e.InstanceId == id).ContentId == "base-spire:strike-ironclad");
                    StepEffect(snapshot.LegalActions.Single(a => a.ActionType == "net:action:base-spire:PlayAttackFromHand" && a.Fields.Any(f => f.Value is WireString v && v.Value == strike)));
                    Check.Equal("134", Public(snapshot, "Enemy.Hp"), "real Strike deals six plus ten temporary Strength");
                    Action("EndDevelopmentTurn");
                    Check.False(snapshot.Entities.Any(e => e.ContentId is "base-spire:strength-power" or "base-spire:flex-potion-power"), "side end withdraws temporary Strength and removes marker");
                }
                else
                {
                    var boots = mode.EndsWith("boots", StringComparison.Ordinal);
                    var relic = boots ? "base-spire:winged-boots" : "base-spire:cursed-pearl";
                    Action("StartNormalRun");
                    var option = snapshot.Entities.Single(e => e.KindId == "net:kind:base-spire:NeowOption" && Text(e, "NeowOption.RelicId") == relic);
                    Check.True(((WireBool)option.Fields.Single(f => f.FieldId.EndsWith(":NeowOption.EffectSupported", StringComparison.Ordinal)).Value!).Value, "real production effect is available");
                    StepEffect(snapshot.LegalActions.Single(a => a.Fields.Any(f => f.Value is WireString v && v.Value == option.InstanceId)));
                    Check.Equal(boots ? "99" : "432", Public(snapshot, "Player.Gold"), "source acquisition resources");
                    var permanent = BaseDslPileChecks.Pile(snapshot, "RunDeck").VisibleMembers.ToArray();
                    Check.Equal(boots ? 10 : 11, permanent.Length, "real Greed is a permanent acquisition");
                    if (!boots)
                    {
                        var greed = snapshot.Entities.Single(e => e.ContentId == "base-spire:greed");
                        Check.True(permanent.Contains(greed.InstanceId), "Greed is actually in the permanent deck");
                        Check.False(((WireBool)greed.Fields.Single(f => f.FieldId.EndsWith(":Card.CanUpgrade", StringComparison.Ordinal)).Value!).Value, "curse cannot be upgraded");
                    }
                    StepEffect(snapshot.LegalActions.Single(a => a.Fields.Any(f => f.Value is WireBool v && v.Value)));
                    StepEffect(snapshot.LegalActions.First(a => a.ActionType == "net:action:base-spire:ChooseMapPoint"));
                    Check.Equal("1", Public(snapshot, "Player.TurnNumber"), "normal first combat starts the real turn");
                    using var vectors = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(), "tools/base-spire-dev/fixtures/neow-effects-vector.json")));
                    var source = vectors.RootElement.GetProperty("openings").EnumerateArray().Single(r => r.GetProperty("choice").GetString() == (boots ? "WingedBoots" : "CursedPearl"));
                    // Public pile memberships do not claim known positions; internal native order is checked by base.
                    Check.Sequence(source.GetProperty("started").GetProperty("hand").EnumerateArray().Select(c => c.GetString()!).Order(StringComparer.Ordinal),
                        BaseDslPileChecks.Pile(snapshot, "Hand").VisibleMembers.Select(id => snapshot.Entities.Single(e => e.InstanceId == id).ContentId!).Order(StringComparer.Ordinal), "complete public hand membership equals the independent native first hand");
                    Check.Sequence(permanent.Order(StringComparer.Ordinal), BaseDslPileChecks.Pile(snapshot, "RunDeck").VisibleMembers.Order(StringComparer.Ordinal), "permanent instances survive combat copying");
                    Check.False(snapshot.LegalActions.Any(a => a.ActionType is "net:action:base-spire:EndDevelopmentCombat" or "net:action:base-spire:Draw"), "normal combat retains real action guards");
                }
            }
            if (mode.StartsWith("normal-", StringComparison.Ordinal) && !mode.StartsWith("normal-neow-", StringComparison.Ordinal))
            {
                var unknown = mode == "normal-unknown";
                var unsupportedEvent = mode == "normal-event-unsupported";
                var eventSelection = mode == "normal-event" || unsupportedEvent;
                var sapphire = mode.StartsWith("normal-sapphire-", StringComparison.Ordinal);
                var dense = mode.StartsWith("normal-dense-", StringComparison.Ordinal);
                var relic = unsupportedEvent ? "base-spire:cursed-pearl" : unknown ? "base-spire:golden-pearl" : "base-spire:booming-conch";
                var initialGold = unsupportedEvent ? 432 : unknown ? 249 : 99;
                string EntityValue(EntityDto entity, string suffix) => ((WireString)entity.Fields.Single(f => f.FieldId.EndsWith(":" + suffix, StringComparison.Ordinal)).Value!).Value;
                LegalActionDto MapAt(string col, string row)
                {
                    var node = snapshot.Entities.Single(e => e.KindId == "net:kind:base-spire:MapNode" && EntityValue(e, "MapNode.Col") == col && EntityValue(e, "MapNode.Row") == row);
                    return snapshot.LegalActions.Single(a => a.ActionType == "net:action:base-spire:ChooseMapPoint" && a.Fields.Any(f => f.Value is WireString value && value.Value == node.InstanceId));
                }
                void StepOpening(LegalActionDto candidate)
                {
                    var prior = snapshot.ObservationId;
                    var endingTurn = candidate.ActionType == "net:action:base-spire:EndTurn";
                    var priorTurn = endingTurn ? int.Parse(Public(snapshot, "Player.TurnNumber"), System.Globalization.CultureInfo.InvariantCulture) : 0;
                    var request = Request("normal-opening-" + ++stepNumber, Operation.Step,
                        ("session_id", WireValue.Str(sessionId)),
                        ("expected_observation_id", WireValue.Integer(snapshot.ObservationId)),
                        ("candidate_id", WireValue.Str(candidate.CandidateId)));
                    var first = Send(request); var repeated = Send(request);
                    Check.Equal(WireCanonical.Text(first.ToWire()), WireCanonical.Text(repeated.ToWire()), "same request repeats the cached result");
                    snapshot = Snapshot(Send(Request("normal-observe-" + stepNumber, Operation.Observe, ("session_id", WireValue.Str(sessionId)))));
                    Check.False(snapshot.LegalActions.Any(a => a.ActionType is "net:action:base-spire:BeginDevelopmentTurn" or "net:action:base-spire:EndDevelopmentTurn"), "normal flow exposes complete turns only");
                    if (endingTurn)
                    {
                        Send(Request("normal-stale-end-" + stepNumber, Operation.Step, ("session_id", WireValue.Str(sessionId)),
                            ("expected_observation_id", WireValue.Integer(prior)), ("candidate_id", WireValue.Str(candidate.CandidateId))), false);
                        var afterStale = Snapshot(Send(Request("normal-after-stale-" + stepNumber, Operation.Observe, ("session_id", WireValue.Str(sessionId)))));
                        Check.Equal(WireCanonical.Text(snapshot.ToWire()), WireCanonical.Text(afterStale.ToWire()), "cached and stale end requests cannot repeat the turn");
                        if (PublicBool(snapshot, "Player.CombatActive"))
                        {
                            Check.Equal((priorTurn + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), Public(snapshot, "Player.TurnNumber"), "one request advances one ordinary turn");
                            Check.True(PublicBool(snapshot, "Player.TurnOpen"), "next manual turn is open at the returned boundary");
                        }
                    }
                }
                StepOpening(snapshot.LegalActions.Single(a => a.ActionType == "net:action:base-spire:StartNormalRun"));
                Check.Equal("80", Public(snapshot, "Player.Hp"), "normal Ironclad source HP");
                Check.Equal("99", Public(snapshot, "Player.Gold"), "normal Ironclad source gold");
                Check.Equal(3, snapshot.LegalActions.Count, "three real Neow answers");
                var option = snapshot.Entities.Single(e => e.KindId == "net:kind:base-spire:NeowOption" && e.Fields.Any(f => f.FieldId.EndsWith(":NeowOption.RelicId", StringComparison.Ordinal) && f.Value is WireString value && value.Value == relic));
                StepOpening(snapshot.LegalActions.Single(a => a.Fields.Any(f => f.Value is WireString value && value.Value == option.InstanceId)));
                Check.Equal(1, snapshot.Entities.Count(e => e.ContentId == relic), "the chosen relic is acquired once");
                Check.Equal("neow", Public(snapshot, "Player.Room"), "event waits for leave confirmation");
                StepOpening(snapshot.LegalActions.Single(a => a.Fields.Any(f => f.Value is WireBool value && value.Value)));
                Check.Equal("map", Public(snapshot, "Player.Room"), "confirmed Neow returns to map");
                using var mapVectors = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(), "tools/base-spire-dev/fixtures/act-one-map-vectors.json")));
                var sourceMap = mapVectors.RootElement.GetProperty("vectors").EnumerateArray().Single(v => v.GetProperty("seed").GetString() == "777");
                if (!unknown && !eventSelection && !sapphire && !dense)
                {
                    Check.Equal(sourceMap.GetProperty("cells").GetArrayLength(), snapshot.Entities.Count(e => e.KindId == "net:kind:base-spire:MapNode"), "seed 777 complete locked source map");
                    var published = snapshot.Sequences.Single(s => s.TypeId == "net:sequence_type:base-spire:MapNodes").Elements;
                    var sourceOrder = sourceMap.GetProperty("publication").EnumerateArray().ToArray();
                    Check.Equal(sourceOrder.Length, published.Count, "actual native enumeration and public sequence have the same complete membership");
                    for (var i = 0; i < sourceOrder.Length; i++)
                    {
                        var node = snapshot.Entities.Single(e => e.InstanceId == published[i]);
                        Check.Equal(sourceOrder[i].GetProperty("index").ToString(), EntityValue(node, "MapNode.Index"), "public index equals native traversal position");
                        Check.Equal(sourceOrder[i].GetProperty("col").ToString(), EntityValue(node, "MapNode.Col"), "native public map column order");
                        Check.Equal(sourceOrder[i].GetProperty("row").ToString(), EntityValue(node, "MapNode.Row"), "native public map row order");
                    }
                }
                StepOpening(dense ? MapAt("1", "1") : sapphire ? MapAt("0", "1") : eventSelection ? MapAt(unsupportedEvent ? "1" : "2", "1") : unknown ? MapAt("3", "1") : snapshot.LegalActions.First(a => a.ActionType == "net:action:base-spire:ChooseMapPoint"));
                using var combatVectors = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(), "tools/base-spire-dev/fixtures/normal-combat-vectors.json")));
                var sourceMonsters = combatVectors.RootElement.GetProperty("vectors")[0].GetProperty("monsters");
                if (unsupportedEvent)
                {
                    // Independently inspected locked native seed morphic-normal-13,
                    // CursedPearl and first node (1,1): one FuzzyWurmCrawler at 55 HP.
                    var enemy = snapshot.Entities.Single(e => e.KindId == "net:kind:base-spire:Enemy");
                    Check.Equal("base-spire:fuzzy-wurm-crawler", enemy.ContentId, "registered unsupported-event prefix keeps its actual source encounter");
                    Check.Equal("55", EntityValue(enemy, "Enemy.Hp"), "locked native first encounter HP");
                }
                else Check.Equal(sourceMonsters.GetArrayLength(), snapshot.Entities.Count(e => e.KindId == "net:kind:base-spire:Enemy"), "complete actual SlimesWeak formation");
                foreach (var expected in sourceMonsters.EnumerateArray().Where(_ => !unknown && !eventSelection && !sapphire && !dense))
                {
                    var monster = snapshot.Entities.Single(e => e.KindId == "net:kind:base-spire:Enemy" && e.ContentId == expected.GetProperty("id").GetString());
                    string EnemyValue(string field) => ((WireString)monster.Fields.Single(f => f.FieldId.EndsWith(":Enemy." + field, StringComparison.Ordinal)).Value!).Value;
                    Check.Equal(expected.GetProperty("hp").GetInt64().ToString(System.Globalization.CultureInfo.InvariantCulture), EnemyValue("Hp"), "source Niche HP of actual weak encounter");
                    Check.Equal(expected.GetProperty("move").GetString(), EnemyValue("MoveId"), "actual initial saved move");
                }
                Check.Equal("1", Public(snapshot, "Player.TurnNumber"), "first real player turn starts automatically");
                Check.Equal("3", Public(snapshot, "Player.Energy"), "Monster room does not receive Conch Elite energy");
                var hand = snapshot.LegalActions.Where(a => a.ActionType is "net:action:base-spire:PlayCardFromHand" or "net:action:base-spire:PlayAttackFromHand")
                    .Select(a => ((WireString)a.Fields.Single().Value!).Value).Distinct().ToArray();
                var deck = BaseDslPileChecks.Pile(snapshot, "RunDeck").VisibleMembers;
                Check.Equal(5, hand.Length, "five actual combat copies have legal play candidates");
                Check.Equal(10, deck.Count, "ten permanent instances remain distinct");
                Check.False(hand.Any(id => deck.Contains(id)), "opening hand uses separate combat instances");
                Check.False(snapshot.LegalActions.Any(a => a.ActionType == "net:action:base-spire:ChooseMapPoint"), "unresolved combat blocks another map visit");
                var payload = WireCanonical.Text(snapshot.ToWire());
                Check.False(payload.Contains("OpeningDefinition", StringComparison.Ordinal) || payload.Contains("MapSegment", StringComparison.Ordinal), "private source pools and map work stay out of public output");
                if (mode != "normal-opening")
                {
                    Check.False(snapshot.LegalActions.Any(a => a.ActionType == "net:action:base-spire:EndDevelopmentCombat"), "normal combat cannot be force-finished");
                    for (var move = 0; Public(snapshot, "Player.CombatOutcome") == "none" && move < 100; move++)
                    {
                        var candidate = snapshot.LegalActions.FirstOrDefault(a => a.ActionType == "net:action:base-spire:PlayAttackFromHand")
                            ?? snapshot.LegalActions.FirstOrDefault(a => a.ActionType == "net:action:base-spire:PlayCardFromHand")
                            ?? snapshot.LegalActions.Single(a => a.ActionType == "net:action:base-spire:EndTurn");
                        StepOpening(candidate);
                    }
                    Check.Equal("victory", Public(snapshot, "Player.CombatOutcome"), "source starter deck wins without injected damage");
                    Check.Equal("rewards", Public(snapshot, "Player.Room"), "real victory offers rewards");
                    Check.Equal(initialGold.ToString(), Public(snapshot, "Player.Gold"), "gold remains unclaimed");
                    Check.False(snapshot.LegalActions.Any(a => a.ActionType == "net:action:base-spire:ChooseMapPoint"), "rewards block map travel");
                    var amount = long.Parse(Public(snapshot, "Player.GoldRewardAmount"), System.Globalization.CultureInfo.InvariantCulture);
                    var offered = snapshot.LegalActions.Where(a => a.ActionType == "net:action:base-spire:ClaimDevelopmentCardReward")
                        .Select(a => ((WireString)a.Fields.Single().Value!).Value).ToArray();
                    if (mode == "normal-claim")
                    {
                        StepOpening(snapshot.LegalActions.Single(a => a.ActionType == "net:action:base-spire:ClaimDevelopmentGoldReward"));
                        Check.False(snapshot.LegalActions.Any(a => a.ActionType == "net:action:base-spire:ClaimDevelopmentGoldReward"), "gold cannot be claimed twice");
                        StepOpening(snapshot.LegalActions.First(a => a.ActionType == "net:action:base-spire:ClaimDevelopmentCardReward"));
                        var potion = snapshot.LegalActions.FirstOrDefault(a => a.ActionType == "net:action:base-spire:ClaimDevelopmentPotionReward");
                        if (potion is not null)
                        {
                            StepOpening(potion);
                            var acquired = snapshot.Entities.Single(e => e.KindId == "net:kind:base-spire:Potion");
                            Check.Equal("base-spire:flex-potion", acquired.ContentId, "original seed777 normal reward reaches real FlexPotion");
                            Check.True(((WireBool)acquired.Fields.Single(f => f.FieldId.EndsWith(":Potion.EffectSupported", StringComparison.Ordinal)).Value!).Value, "normal reward has the same implemented production effect");
                        }
                    }
                    else
                    {
                        StepOpening(snapshot.LegalActions.Single(a => a.ActionType == "net:action:base-spire:SkipDevelopmentCardReward"));
                        var potion = snapshot.LegalActions.FirstOrDefault(a => a.ActionType == "net:action:base-spire:SkipDevelopmentPotionReward");
                        if (potion is not null) StepOpening(potion);
                    }
                    StepOpening(snapshot.LegalActions.Single(a => a.ActionType == "net:action:base-spire:CloseDevelopmentRewards"));
                    Check.Equal("map", Public(snapshot, "Player.Room"), "rewards close into the normal map");
                    Check.Equal((mode == "normal-claim" ? initialGold + amount : initialGold).ToString(System.Globalization.CultureInfo.InvariantCulture), Public(snapshot, "Player.Gold"), "claim or skip credits gold once");
                    var finalDeck = BaseDslPileChecks.Pile(snapshot, "RunDeck").VisibleMembers.ToHashSet();
                    Check.Equal(mode == "normal-claim" ? 11 : 10, finalDeck.Count, "reward changes only the permanent deck");
                    Check.True(deck.All(finalDeck.Contains), "permanent starter identities survive the actual combat");
                    Check.True(offered.Skip(mode == "normal-claim" ? 1 : 0).All(id => !snapshot.Entities.Any(e => e.InstanceId == id)), "unselected actual instances are removed");
                    Check.True(snapshot.LegalActions.Any(a => a.ActionType == "net:action:base-spire:ChooseMapPoint"), "legal source successors are published");
                    Check.False(snapshot.LegalActions.Any(a => a.ActionType == "net:action:base-spire:CloseDevelopmentRewards"), "the visit cannot close twice");
                    if (dense)
                    {
                        StepOpening(MapAt("0", "2"));
                        var parent = snapshot.Entities.Single(e => e.KindId == "net:kind:base-spire:RunVisit" && EntityValue(e, "RunVisit.StableId") == "2");
                        var eventVisit = snapshot.Entities.Single(e => e.ContentId == "base-spire:dense-vegetation" && e.KindId == "net:kind:base-spire:EventVisit");
                        Check.Equal(eventVisit.InstanceId, EntityValue(parent, "RunVisit.Event"), "public parent owns actual DenseVegetation");
                        Check.Equal(parent.InstanceId, EntityValue(eventVisit, "EventVisit.RunVisit"), "event retains its original parent");
                        var rest = mode != "normal-dense-trudge";
                        var beforeHp = long.Parse(Public(snapshot, "Player.Hp"), System.Globalization.CultureInfo.InvariantCulture);
                        StepOpening(snapshot.LegalActions.Single(a => a.Fields.Any(f => f.Value is WireBool value && value.Value == rest)));
                        if (!rest)
                        {
                            Check.Equal((beforeHp - 8).ToString(System.Globalization.CultureInfo.InvariantCulture), Public(snapshot, "Player.Hp"), "TRUDGE_ON loses eight HP once");
                            Check.Equal("163", Public(snapshot, "Player.Gold"), "independent source event roll grants 64 gold");
                            StepOpening(snapshot.LegalActions.Single(a => a.ActionType == "net:action:base-spire:CloseNormalEvent"));
                        }
                        else
                        {
                            Check.Equal(Math.Min(80, beforeHp + 24).ToString(System.Globalization.CultureInfo.InvariantCulture), Public(snapshot, "Player.Hp"), "REST heals once before FIGHT");
                            Check.False(snapshot.LegalActions.Any(a => a.ActionType is "net:action:base-spire:CloseNormalEvent" or "net:action:base-spire:ChooseMapPoint"), "rest requires the actual fight");
                            StepOpening(snapshot.LegalActions.Single(a => a.ActionType == "net:action:base-spire:DenseVegetationFight"));
                            var child = snapshot.Entities.Single(e => e.KindId == "net:kind:base-spire:RunVisit" && EntityValue(e, "RunVisit.StableId") == "3");
                            Check.Equal(parent.InstanceId, EntityValue(child, "RunVisit.Parent"), "public child points to original parent");
                            Check.Equal(child.InstanceId, EntityValue(snapshot.Entities.Single(e => e.InstanceId == eventVisit.InstanceId), "EventVisit.ChildVisit"), "original event owns child");
                            Check.Equal(EntityValue(parent, "RunVisit.Node"), EntityValue(child, "RunVisit.Node"), "child shares original map node");
                            Check.Equal(4, snapshot.Entities.Count(e => e.KindId == "net:kind:base-spire:Enemy" && e.ContentId == "base-spire:wriggler"), "four actual Wrigglers enter combat");
                            var normalRun = snapshot.Entities.Single(e => e.KindId == "net:kind:base-spire:Run");
                            Check.Equal("3", EntityValue(normalRun, "Run.TotalFloor"), "child does not add a floor");
                            for (var move = 0; Public(snapshot, "Player.CombatOutcome") == "none" && move < 200; move++)
                            {
                                var candidate = snapshot.LegalActions.FirstOrDefault(a => a.ActionType == "net:action:base-spire:PlayAttackFromHand")
                                    ?? snapshot.LegalActions.FirstOrDefault(a => a.ActionType == "net:action:base-spire:PlayCardFromHand")
                                    ?? snapshot.LegalActions.Single(a => a.ActionType == "net:action:base-spire:EndTurn");
                                StepOpening(candidate);
                            }
                            Check.Equal("victory", Public(snapshot, "Player.CombatOutcome"), "real public policy defeats four Wrigglers");
                            Check.Equal("rewards", Public(snapshot, "Player.Room"), "child victory offers real ordinary rewards");
                            Check.False(snapshot.LegalActions.Any(a => a.ActionType is "net:action:base-spire:CloseNormalEvent" or "net:action:base-spire:ChooseMapPoint"), "parent cannot bypass child rewards");
                            var claim = mode == "normal-dense-fight-claim";
                            var childGold = long.Parse(Public(snapshot, "Player.GoldRewardAmount"), System.Globalization.CultureInfo.InvariantCulture);
                            if (claim)
                            {
                                StepOpening(snapshot.LegalActions.Single(a => a.ActionType == "net:action:base-spire:ClaimDevelopmentGoldReward"));
                                StepOpening(snapshot.LegalActions.First(a => a.ActionType == "net:action:base-spire:ClaimDevelopmentCardReward"));
                                var potion = snapshot.LegalActions.FirstOrDefault(a => a.ActionType == "net:action:base-spire:ClaimDevelopmentPotionReward");
                                if (potion is not null) StepOpening(potion);
                            }
                            StepOpening(snapshot.LegalActions.Single(a => a.ActionType == "net:action:base-spire:CloseDevelopmentRewards"));
                            Check.Equal((claim ? 99 + childGold : 99).ToString(System.Globalization.CultureInfo.InvariantCulture), Public(snapshot, "Player.Gold"), "child reward credits once only when claimed");
                            Check.Equal(claim ? 11 : 10, BaseDslPileChecks.Pile(snapshot, "RunDeck").TotalCount, "actual child reward persists in permanent deck");
                            Check.Equal("closed", EntityValue(snapshot.Entities.Single(e => e.InstanceId == child.InstanceId), "RunVisit.Status"), "child closes once after rewards");
                            Check.Equal(parent.InstanceId, EntityValue(snapshot.Entities.Single(e => e.InstanceId == normalRun.InstanceId), "Run.CurrentVisit"), "closed child restores same parent");
                        }
                        Check.Equal("map", Public(snapshot, "Player.Room"), "normal event closes back to actual map");
                        Check.Equal("closed", EntityValue(snapshot.Entities.Single(e => e.InstanceId == parent.InstanceId), "RunVisit.Status"), "same parent closes once");
                        Check.Equal("closed", EntityValue(snapshot.Entities.Single(e => e.InstanceId == eventVisit.InstanceId), "EventVisit.Status"), "event never restarts after child");
                        Check.Equal(1, snapshot.LegalActions.Count(a => a.ActionType == "net:action:base-spire:ChooseMapPoint"), "exact source successor count");
                        MapAt("0", "3");
                        Check.False(snapshot.LegalActions.Any(a => a.ActionType is "net:action:base-spire:CloseNormalEvent" or "net:action:base-spire:DenseVegetationFight" or "net:action:base-spire:CloseDevelopmentRewards"), "closed visits expose no replay actions");
                        var visible = WireCanonical.Text(snapshot.ToWire());
                        Check.False(visible.Contains("NormalEventHistory", StringComparison.Ordinal) || visible.Contains("UnknownRoomOdds", StringComparison.Ordinal) || visible.Contains("NormalEncounterIndex", StringComparison.Ordinal), "hidden queues and odds remain private");
                    }
                    if (sapphire)
                    {
                        var beforeHp = long.Parse(Public(snapshot, "Player.Hp"), System.Globalization.CultureInfo.InvariantCulture);
                        StepOpening(MapAt("0", "2"));
                        var visit = snapshot.Entities.Single(e => e.KindId == "net:kind:base-spire:RunVisit" && EntityValue(e, "RunVisit.StableId") == "2");
                        var eventVisit = snapshot.Entities.Single(e => e.ContentId == "base-spire:sapphire-seed");
                        Check.Equal(eventVisit.InstanceId, EntityValue(visit, "RunVisit.Event"), "public normal visit points to actual event");
                        Check.Equal(visit.InstanceId, EntityValue(eventVisit, "EventVisit.RunVisit"), "public event points to same normal visit");
                        Check.Equal("net:cat:base-spire:MapPointType:Unknown", EntityValue(visit, "RunVisit.MapType"), "actual Event preserves Unknown map type");
                        Check.Equal("event", EntityValue(visit, "RunVisit.RoomType"), "actual room is Event");
                        var eat = mode.EndsWith("-eat", StringComparison.Ordinal);
                        StepOpening(snapshot.LegalActions.Single(a => a.Fields.Any(f => f.Value is WireBool value && value.Value == eat)));
                        if (eat)
                        {
                            Check.Equal(10, snapshot.LegalActions.Count, "all source permanent cards are upgrade candidates");
                            var selected = snapshot.LegalActions.Last();
                            var selectedId = ((WireString)selected.Fields.Single().Value!).Value;
                            StepOpening(selected);
                            var card = snapshot.Entities.Single(e => e.InstanceId == selectedId);
                            Check.True(((WireBool)card.Fields.Single(f => f.FieldId.EndsWith(":Card.Upgraded", StringComparison.Ordinal)).Value!).Value, "chosen permanent instance actually upgrades");
                        }
                        else
                        {
                            Check.False(snapshot.LegalActions.Any(a => a.ActionType == "net:action:net.answer_decision"), "source PLANT has no extra card decision");
                            Check.Equal(1, snapshot.Entities.Count(e => e.ContentId == "base-spire:sown"), "one real Sown attachment");
                        }
                        Check.Equal((eat ? Math.Min(80, beforeHp + 9) : beforeHp).ToString(System.Globalization.CultureInfo.InvariantCulture), Public(snapshot, "Player.Hp"), "real event heals once or preserves HP");
                        Check.Equal("99", Public(snapshot, "Player.Gold"), "event does not invent gold rewards");
                        Check.False(snapshot.LegalActions.Any(a => a.ActionType is "net:action:base-spire:ChooseMapPoint" or "net:action:base-spire:CloseDevelopmentEvent"), "finished normal event waits for its own close");
                        StepOpening(snapshot.LegalActions.Single(a => a.ActionType == "net:action:base-spire:CloseNormalEvent"));
                        Check.Equal("map", Public(snapshot, "Player.Room"), "normal event closes into map");
                        Check.Equal(10, BaseDslPileChecks.Pile(snapshot, "RunDeck").TotalCount, "permanent deck retains all instances");
                        Check.Equal("closed", EntityValue(snapshot.Entities.Single(e => e.InstanceId == visit.InstanceId), "RunVisit.Status"), "same visit closes once");
                        Check.Equal(1, snapshot.LegalActions.Count(a => a.ActionType == "net:action:base-spire:ChooseMapPoint"), "one real source successor");
                        MapAt("0", "3");
                        Check.False(snapshot.LegalActions.Any(a => a.ActionType == "net:action:base-spire:CloseNormalEvent"), "closed event cannot be closed again");
                        var visible = WireCanonical.Text(snapshot.ToWire());
                        Check.False(visible.Contains("NormalEventHistory", StringComparison.Ordinal) || visible.Contains("NormalEventIndex", StringComparison.Ordinal) || visible.Contains("UnknownRoomOdds", StringComparison.Ordinal), "hidden queues and odds remain private");
                    }
                    if (eventSelection)
                    {
                        Check.True(long.Parse(Public(snapshot, "Player.Hp"), System.Globalization.CultureInfo.InvariantCulture) > 0, "real public candidate strategy survives the combat");
                        var visible = WireCanonical.Text(snapshot.ToWire());
                        Check.False(visible.Contains("NormalEventHistory", StringComparison.Ordinal) || visible.Contains("NormalEventIndex", StringComparison.Ordinal) || visible.Contains("SelectedNormalEvent", StringComparison.Ordinal), "event queue and history are hidden");
                        if (unsupportedEvent)
                        {
                            var request = Request("normal-event-failure", Operation.Step,
                                ("session_id", WireValue.Str(sessionId)),
                                ("expected_observation_id", WireValue.Integer(snapshot.ObservationId)),
                                ("candidate_id", WireValue.Str(MapAt("0", "2").CandidateId)));
                            var failed = Send(request, expectSuccess: false);
                            Check.Equal("backend_fault", failed.Error!.Code, "unsupported source effect is a real protocol failure");
                            Check.True(failed.Error.Message.Contains("selected source normal Event effect", StringComparison.Ordinal), "effect gap is named");
                            Check.Equal(WireCanonical.Text(failed.ToWire()), WireCanonical.Text(Send(request, expectSuccess: false).ToWire()), "replayed failure cannot select or pay twice");
                            var observation = Send(Request("normal-event-fault-observe", Operation.Observe, ("session_id", WireValue.Str(sessionId))));
                            Check.Equal("faulted", ((WireString)Field(observation, "session_status")).Value, "unsupported effect stops the session");
                            Check.True(Field(observation, "snapshot") is WireNull, "faulted session exposes no hidden selection snapshot");
                        }
                        else
                        {
                            StepOpening(MapAt("1", "2"));
                            Check.Equal("base-spire:aroma-of-chaos", snapshot.Entities.Single(e => e.KindId == "net:kind:base-spire:EventVisit").ContentId, "retained seed naturally reaches Aroma");
                            StepOpening(snapshot.LegalActions.Single(a => a.Fields.Any(f => f.Value is WireBool b && !b.Value)));
                            var chosen = snapshot.LegalActions.First();
                            var chosenId = ((WireString)chosen.Fields.Single().Value!).Value;
                            StepOpening(chosen);
                            Check.True(((WireBool)snapshot.Entities.Single(e => e.InstanceId == chosenId).Fields.Single(f => f.FieldId.EndsWith(":Card.Upgraded", StringComparison.Ordinal)).Value!).Value, "real chosen permanent card upgrades");
                            StepOpening(snapshot.LegalActions.Single(a => a.ActionType == "net:action:base-spire:CloseNormalEvent"));
                            Check.Equal("map", Public(snapshot, "Player.Room"), "Aroma returns to map without replaying the upgrade");
                            Check.Equal(2, snapshot.LegalActions.Count(a => a.ActionType == "net:action:base-spire:ChooseMapPoint"), "both native successors remain available");
                        }
                    }
                    if (unknown)
                    {
                        StepOpening(MapAt("3", "2"));
                        var visit = snapshot.Entities.Single(e => e.KindId == "net:kind:base-spire:RunVisit" && EntityValue(e, "RunVisit.StableId") == "2");
                        Check.Equal("net:cat:base-spire:MapPointType:Unknown", EntityValue(visit, "RunVisit.MapType"), "original map type survives actual room classification");
                        Check.Equal("monster", EntityValue(visit, "RunVisit.RoomType"), "real unknown roll enters Monster");
                        Check.Equal("base-spire:fuzzy-wurm-crawler-weak", EntityValue(visit, "RunVisit.EncounterId"), "next source queue entry is actually consumed");
                        Check.Equal("69", Public(snapshot, "Player.Hp"), "native normal combat and permanent HP");
                        Check.Equal("1", Public(snapshot, "Player.TurnNumber"), "the next real combat starts its first turn");
                        var visible = WireCanonical.Text(snapshot.ToWire());
                        Check.False(visible.Contains("UnknownRoomOdds", StringComparison.Ordinal) || visible.Contains("CurrentPointHasShop", StringComparison.Ordinal) || visible.Contains("unknown_map_point", StringComparison.Ordinal), "hidden odds and random state stay out of stdio");
                    }
                }

            }
            if (mode.StartsWith("pickup-upgrade-", StringComparison.Ordinal))
            {
                var permanent = BaseDslPileChecks.Pile(snapshot, "RunDeck").VisibleMembers.ToHashSet();
                Check.Equal(2, permanent.Count, "original permanent instances remain in the deck");
                foreach (var card in snapshot.Entities.Where(e => e.KindId == "net:kind:base-spire:Card"))
                {
                    var upgraded = ((WireBool)card.Fields.Single(f => f.FieldId.EndsWith(":Card.Upgraded", StringComparison.Ordinal)).Value!).Value;
                    var expected = card.ContentId == "base-spire:strike-ironclad" ? mode != "pickup-upgrade-war-paint" : mode != "pickup-upgrade-whetstone";
                    Check.Equal(expected, upgraded, "both permanent and combat instances expose the real source upgrade");
                }
                Check.False(WireCanonical.Text(snapshot.ToWire()).Contains("PermanentUpgrade", StringComparison.Ordinal), "temporary selection state stays out of public snapshots");
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
            if (mode == "simple-resources")
            {
                Check.Equal("125", Public(snapshot, "Player.MaxHp"), "source 80 plus 3 plus 31 plus 11 maximum HP");
                Check.Equal("125", Public(snapshot, "Player.Hp"), "pickup healing matches max HP increments");
                Check.Equal("1248", Public(snapshot, "Player.Gold"), "source 99 plus 999 plus 150 gold");
                foreach (var slug in new[] { "fake-mango", "looming-fruit", "nutritious-oyster", "signet-ring", "golden-pearl" })
                {
                    var relic = snapshot.Entities.Single(e => e.ContentId == "base-spire:" + slug);
                    var candidate = snapshot.LegalActions.Single(a => a.ActionType == "net:action:base-spire:RemoveOwnedRelic" && a.Fields.Any(f => f.Value is WireString value && value.Value == relic.InstanceId));
                    var id = "pipe-resource-remove-" + ++stepNumber;
                    Send(Request(id, Operation.Step, ("session_id", WireValue.Str(sessionId)), ("expected_observation_id", WireValue.Integer(snapshot.ObservationId)), ("candidate_id", WireValue.Str(candidate.CandidateId))));
                    snapshot = Snapshot(Send(Request(id + "-observe", Operation.Observe, ("session_id", WireValue.Str(sessionId)))));
                    Check.Equal("125", Public(snapshot, "Player.MaxHp"), "removal never revokes permanent max HP");
                    Check.Equal("125", Public(snapshot, "Player.Hp"), "removal never revokes healing");
                    Check.Equal("1248", Public(snapshot, "Player.Gold"), "removal never revokes permanent gold");
                }
                Check.False(snapshot.Entities.Any(e => e.KindId == "net:kind:base-spire:Relic"), "actual removed instances disappear from the public state");
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
                var deck = BaseDslPileChecks.Pile(snapshot, "RunDeck").VisibleMembers;
                var hand = snapshot.LegalActions.Where(a => a.ActionType is "net:action:base-spire:PlayCardFromHand" or "net:action:base-spire:PlayAttackFromHand")
                    .Select(a => ((WireString)a.Fields.Single().Value!).Value).Distinct().ToArray();
                Check.Equal(10, deck.Count, "ten permanent cards"); Check.Equal(5, hand.Length, "five automatic opening cards have actual play candidates");
                Check.False(hand.Any(id => deck.Contains(id)), "combat copies have separate public handles");
                var definitions = deck.Select(member => snapshot.Entities.Single(e => e.InstanceId == member).ContentId).ToArray();
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
                    else Named("EndTurn");
                }
                Check.Equal("victory", Public(snapshot, "Player.CombatOutcome"), "real starter-card victory without HP injection");
                Check.Equal("rewards", Public(snapshot, "Player.Room"), "reward room opens");
                var offered = snapshot.Groups.Single(g => g.TypeId.EndsWith(":CardRewardOptions", StringComparison.Ordinal)).Members.Select(m => m.EntityRef).ToArray();
                Check.Equal(3, offered.Length, "three public reward options");
                Check.Equal(3, offered.Select(id => snapshot.Entities.Single(e => e.InstanceId == id).ContentId).Distinct().Count(), "three distinct definitions");
                Check.Equal(10, BaseDslPileChecks.Pile(snapshot, "RunDeck").VisibleMembers.Count, "unclaimed cards are not permanent");
                Check.Equal(3, snapshot.LegalActions.Count(a => a.ActionType == "net:action:base-spire:ClaimDevelopmentCardReward"), "one claim per actual option");
                foreach (var id in offered)
                    Check.True(snapshot.Entities.Single(e => e.InstanceId == id).Fields.Any(f => f.FieldId.EndsWith(":Card.Rarity", StringComparison.Ordinal)), "reward rarity is public");
                Check.False(WireCanonical.Text(snapshot.ToWire()).Contains("CardRarityOffset", StringComparison.Ordinal), "future rarity odds remain hidden");
                var claim = mode == "card-reward-claim";
                var potionOffer = snapshot.Groups.Single(g => g.TypeId.EndsWith(":PotionRewardOptions", StringComparison.Ordinal)).Members.Single().EntityRef;
                var potionEntity = snapshot.Entities.Single(e => e.InstanceId == potionOffer);
                using var rewardVectors = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(), "tools/base-spire-dev/fixtures/normal-reward-vectors.json")));
                var expectedPotion = rewardVectors.RootElement.GetProperty("vectors").EnumerateArray().Single(v => v.GetProperty("seed").GetString() == "BASE-DSL-STDIO-00").GetProperty("offers")[0].GetProperty("potion").GetString();
                Check.Equal(expectedPotion, potionEntity.ContentId, "independent current source vector includes the complete potion pool");
                Check.False(((WireBool)potionEntity.Fields.Single(f => f.FieldId.EndsWith(":Potion.EffectSupported", StringComparison.Ordinal)).Value!).Value, "unimplemented effect is explicit");
                Check.Equal(1, snapshot.Entities.Count(e => e.KindId == "net:kind:base-spire:CombatRewardSet"), "one public visit container");
                Check.False(WireCanonical.Text(snapshot.ToWire()).Contains("PotionRewardOdds", StringComparison.Ordinal), "future potion odds remain hidden");
                Named(claim ? "ClaimDevelopmentPotionReward" : "SkipDevelopmentPotionReward");
                Check.Equal(0, snapshot.Groups.Single(g => g.TypeId.EndsWith(":PotionRewardOptions", StringComparison.Ordinal)).Members.Count, "potion entry settled independently");
                Check.True(PublicBool(snapshot, "Player.GoldRewardPending") && PublicBool(snapshot, "Player.CardRewardPending"), "potion selection preserves gold and card offers");
                if (claim)
                    Check.True(snapshot.Groups.Single(g => g.TypeId.EndsWith(":Potions", StringComparison.Ordinal)).Members.Any(m => m.EntityRef == potionOffer), "same public potion handle becomes inventory");
                else Check.False(snapshot.Entities.Any(e => e.InstanceId == potionOffer), "skipped potion instance destroyed");
                Check.False(snapshot.LegalActions.Any(a => a.ActionType == "net:action:base-spire:ClaimDevelopmentPotionReward"), "no repeated potion claim");
                var chosen = offered[1];
                if (claim)
                    StepCardReward(snapshot.LegalActions.Single(a => a.ActionType == "net:action:base-spire:ClaimDevelopmentCardReward" && a.Fields.Any(f => f.Value is WireString v && v.Value == chosen)));
                else Named("SkipDevelopmentCardReward");
                Check.False(PublicBool(snapshot, "Player.CardRewardPending"), "card reward spent");
                Check.True(PublicBool(snapshot, "Player.GoldRewardPending"), "gold reward remains independent");
                var deck = BaseDslPileChecks.Pile(snapshot, "RunDeck").VisibleMembers;
                Check.Equal(claim ? 11 : 10, deck.Count, "only a selected card joins the permanent deck");
                if (claim) Check.True(deck.Contains(chosen), "selected public handle becomes permanent");
                Check.True(offered.Where(id => !claim || id != chosen).All(id => snapshot.Entities.All(e => e.InstanceId != id)), "unselected reward instances destroyed");
                Check.False(snapshot.LegalActions.Any(a => a.ActionType == "net:action:base-spire:ClaimDevelopmentCardReward"), "no repeated claim candidates");
                Named("CloseDevelopmentRewards"); Check.Equal("none", Public(snapshot, "Player.Room"), "explicit reward close");
                Check.Equal(claim, snapshot.Entities.Any(e => e.InstanceId == potionOffer), "close preserves only an acquired potion");
                Named("TwigSlimeS.Spawn"); Named("BeginDevelopmentCombat"); Named("BeginDevelopmentTurn");
                Check.Equal(claim ? 11 : 10, BaseDslPileChecks.Pile(snapshot, "RunDeck").VisibleMembers.Count, "permanent deck survives next combat");
                Check.False(WireCanonical.Text(snapshot.ToWire()).Contains("Card.PermanentSource", StringComparison.Ordinal), "combat source references stay hidden");
            }
            if (mode == "rupture") { Check.Equal("78", Public(snapshot, "Player.Hp"), "real HP loss"); Check.Equal("2", Public(snapshot, "Player.PlaySequence"), "real play count"); }
            if (mode is "bash" or "inflame" or "thunderclap") Check.Equal(mode == "thunderclap" ? "1" : "2", Public(snapshot, "Power.Stacks"), "the applied power is public on its holder");
            if (mode is "kill" or "reward-claim" or "reward-skip" or "charons-ashes-kill") Check.Equal("victory", Public(snapshot, "Player.CombatOutcome"), "the public combat outcome follows the last kill");
            if (mode == "sown-event")
            {
                void StepVisit(LegalActionDto candidate)
                {
                    var id = "visit-step-" + ++stepNumber;
                    Send(Request(id, Operation.Step, ("session_id", WireValue.Str(sessionId)), ("expected_observation_id", WireValue.Integer(snapshot.ObservationId)), ("candidate_id", WireValue.Str(candidate.CandidateId))));
                    snapshot = Snapshot(Send(Request(id + "-observe", Operation.Observe, ("session_id", WireValue.Str(sessionId)))));
                }
                void Named(string name) => StepVisit(snapshot.LegalActions.Single(a => a.ActionType == "net:action:base-spire:" + name));
                StepVisit(snapshot.LegalActions.Single(a => a.Fields.Any(f => f.Value is WireBool { Value: false })));
                var permanent = snapshot.Entities.Single(e => e.ContentId == "base-spire:strike-ironclad").InstanceId;
                Check.False(snapshot.LegalActions.Any(a => a.ActionType == "net:action:net.answer_decision"), "source PLANT needs no extra card decision");
                Check.Equal(1, snapshot.Entities.Count(e => e.ContentId == "base-spire:sown"), "one real permanent attachment after choice");
                Named("CloseDevelopmentEvent"); Named("AddDevelopmentTarget"); Named("BeginDevelopmentCombat");
                Check.Equal(1, snapshot.Entities.Count(e => e.ContentId == "base-spire:sown"), "hidden combat-copy attachment cannot reveal draw-pile identity");
                Named("Draw"); Check.Equal(2, snapshot.Entities.Count(e => e.ContentId == "base-spire:sown"), "drawing authorizes its combat attachment");
                Named("BeginDevelopmentTurn"); Named("PlayAttackFromHand");
                Check.Equal("3", Public(snapshot, "Player.Energy"), "one paid, one gained from the explicit enchantment phase");
                Check.Equal("34", Public(snapshot, "Enemy.Hp"), "actual card body executes once after the event");
                var statuses = snapshot.Entities.Where(e => e.ContentId == "base-spire:sown")
                    .Select(e => ((WireString)e.Fields.Single(f => f.FieldId.EndsWith(":CardAttachment.Status", StringComparison.Ordinal)).Value!).Value).Order().ToArray();
                Check.Equal("disabled,normal", string.Join(',', statuses), "combat spent state and permanent unspent state remain independent");
            }
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
                Check.Equal(2, BaseDslPileChecks.Pile(snapshot, "RunDeck").VisibleMembers.Count, "the permanent deck is publicly visible");
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
            if (mode == "battle-friend")
            {
                Check.Equal("victory", Public(snapshot, "Player.CombatOutcome"), "native victory and timeout coexist");
                Check.False(PublicBool(snapshot, "Player.GoldRewardPending"), "timeout does not offer monster gold");
                Check.False(snapshot.LegalActions.Any(a => a.ActionType.EndsWith(":PlayAttackFromHand", StringComparison.Ordinal)), "escaped target offers no attack candidate");
            }
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
