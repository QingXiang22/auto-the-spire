using CoreTheSpire.Engine;
using System.Text.Json;

internal static partial class PackageChecks
{
    private static Dictionary<string, string[]> RunEventDeckChecks(DslPackage package, Action<string, Action> check)
    {
        using var data = JsonDocument.Parse(typeof(PackageChecks).Assembly.GetManifestResourceStream("BaseSpire.Dev.fixtures.normal-event-deck-vector.json")!);
        var witnesses = new Dictionary<string, List<string>>();
        foreach (var row in data.RootElement.GetProperty("branches").EnumerateArray())
        {
            var definition = row.GetProperty("definition").GetString()!;
            var branch = row.GetProperty("branch").GetString()!;
            var name = "EVENT-DECK normal " + definition + " " + branch + ": native pool, ordered instances, RNG and map return";
            check(name, () => NormalEventDeck(package, row));
            if (!witnesses.TryGetValue(definition, out var names)) witnesses.Add(definition, names = []);
            names.Add(name);
        }
        check("EVENT-DECK automatic empty/single/pair selection and Eternal/Quest eligibility", () => EventDeckAutomatic(package));
        check("EVENT-DECK unimplemented source event still fails on its registered normal prefix", () => UnimplementedNormalEvent(package));
        return witnesses.ToDictionary(p => p.Key, p => p.Value.ToArray());
    }

    private static Session EventDeckPrefix(DslPackage package, string seed, string relic, int firstCol, bool claimGold, bool debug = false)
    {
        var s = package.CreateSession(1, ["player"], seed, debug: debug ? new SessionDebugOptions(MaxRecords: 200_000, MaxCandidates: 256,
            CaptureValues: true, CaptureCandidateReferences: true) : null);
        s.Start(); Submit(s, "StartNormalRun");
        var option = s.PendingDecision!.Candidates.Single(o => TextField(s, o, "NeowOption", "RelicId") == relic);
        s.Answer(new DecisionAnswer(s.PendingDecision.RequestId, Instances: [option]));
        s.Answer(new DecisionAnswer(s.PendingDecision!.RequestId, Flag: true));
        var node = Members(s, "MapNodes").Single(n => IntField(s, n, "MapNode", "Col") == firstCol && IntField(s, n, "MapNode", "Row") == 1);
        Submit(s, "ChooseMapPoint", node);
        using var input = NormalCombatInput();
        var priorities = input.RootElement.GetProperty("combat_action_priority").EnumerateArray().Select(a => "base-spire:" + a.GetString()).ToArray();
        for (var action = 0; Active(s) && action < 100; action++)
        {
            var legal = s.LegalActions(s.Participant(0));
            var selected = priorities.Select(id => legal.FirstOrDefault(a => a.Definition.QualifiedId == id)).First(a => a is not null)!;
            s.Submit(selected.Definition, s.Participant(0), selected.Bindings.Count > 0 ? selected.Bindings[0] : []); Healthy(s);
        }
        Equal("victory", Outcome(s), "registered starter-deck prefix wins without injected gameplay state");
        if (claimGold) Submit(s, "ClaimDevelopmentGoldReward");
        Submit(s, "CloseDevelopmentRewards");
        return s;
    }

    private static void NormalEventDeck(DslPackage package, JsonElement row)
    {
        var s = EventDeckPrefix(package, row.GetProperty("seed").GetString()!, "base-spire:booming-conch", row.GetProperty("first")[0].GetInt32(), row.GetProperty("claim_gold").GetBoolean(), debug: true);
        var deck = Members(s, "RunDeck").ToArray();
        var node = Members(s, "MapNodes").Single(n => IntField(s, n, "MapNode", "Col") == row.GetProperty("unknown")[0].GetInt32() && IntField(s, n, "MapNode", "Row") == 2);
        Submit(s, "ChooseMapPoint", node);
        var visit = s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:EventVisit").Id).Single();
        var definition = row.GetProperty("definition").GetString()!;
        Equal(definition, TextField(s, visit, "EventVisit", "DefinitionId"), "natural event selection");
        var stream = definition + ":visit:" + IntField(s, visit, "EventVisit", "Sequence");
        void CompareResources(JsonElement expected)
        {
            Equal(expected.GetProperty("hp").GetInt64(), Hp(s), "source HP");
            Equal(expected.GetProperty("max_hp").GetInt64(), IntField(s, Player(s), "Player", "MaxHp"), "source maximum HP");
            Equal(expected.GetProperty("gold").GetInt64(), IntField(s, Player(s), "Player", "Gold"), "source gold");
            var rng = expected.GetProperty("event_rng"); var snapshot = s.Random.Capture();
            Equal(row.GetProperty("event_seed").GetUInt64(), snapshot.ExplicitSeeds[stream], "native event seed, including unconsumed lazy streams");
            Equal(rng.GetProperty("counter").GetInt64(), s.Random.DrawCount(stream), "event draw count");
            var actual = snapshot.Streams.SingleOrDefault(r => r.Name == stream);
            if (actual is not null) True(actual.BackendState!.SequenceEqual(Enumerable.Range(0, 4).Select(i => rng.GetProperty("state" + i).GetUInt64())), "complete source event RNG state");
        }
        CompareResources(row.GetProperty("before"));
        var otherRandom = JsonSerializer.Serialize(s.Random.Capture().Streams.Where(r => r.Name != stream));
        var branch = row.GetProperty("branch").GetString();
        s.Answer(new DecisionAnswer(s.PendingDecision!.RequestId, Flag: branch is "LET_GO" or "GROUP")); Healthy(s);
        foreach (var selection in row.GetProperty("selections").EnumerateArray())
        {
            var pending = s.PendingDecision!;
            Equal(selection.GetProperty("min").GetInt32(), pending.Minimum, "one complete minimum cardinality");
            Equal(selection.GetProperty("max").GetInt32(), pending.Maximum, "one complete maximum cardinality");
            True(pending.Candidates.SequenceEqual(selection.GetProperty("candidates").EnumerateArray().Select(v => deck[v.GetInt32()])), "complete ordered actual source selection domain");
            Equal(selection.GetProperty("gold").GetInt64(), IntField(s, Player(s), "Player", "Gold"), "GROUP pays all gold before the single selection");
            var state = s.State.StateHash(); var random = JsonSerializer.Serialize(s.Random.Capture());
            Throws<EngineFaultException>(() => s.Answer(new DecisionAnswer(pending.RequestId, Instances: [Player(s)])));
            Equal(state, s.State.StateHash(), "foreign answer cannot mutate state or repeat payment");
            Equal(random, JsonSerializer.Serialize(s.Random.Capture()), "foreign answer cannot consume RNG");
            var chosen = selection.GetProperty("selected").EnumerateArray().Select(v => deck[v.GetInt32()]).ToArray();
            if (chosen.Length == 2)
            {
                Throws<EngineFaultException>(() => s.Answer(new DecisionAnswer(pending.RequestId, Instances: [chosen[0], chosen[0]])));
                Equal(state, s.State.StateHash(), "duplicate instances cannot split or resume the pair");
            }
            s.Answer(new DecisionAnswer(pending.RequestId, Instances: chosen)); Healthy(s);
            var after = s.State.StateHash();
            Throws<EngineFaultException>(() => s.Answer(new DecisionAnswer(pending.RequestId, Instances: chosen)));
            Equal(after, s.State.StateHash(), "stale selection cannot repeat transformation");
        }
        True(s.PendingDecision is null, "branch completes its real decision");
        CompareResources(row.GetProperty("after"));
        var result = Members(s, "RunDeck").ToArray(); var expectedDeck = row.GetProperty("after").GetProperty("deck");
        Equal(expectedDeck.GetArrayLength(), result.Length, "replacement preserves deck size");
        for (var i = 0; i < result.Length; i++)
        {
            var expected = expectedDeck[i]; var original = expected.GetProperty("original_index").GetInt32();
            if (original >= 0) Equal(deck[original], result[i], "unaffected permanent instance and order");
            else True(!deck.Contains(result[i]), "transformation produces a genuinely new permanent instance");
            Equal(expected.GetProperty("id").GetString(), TextField(s, result[i], "Card", "DefinitionId"), "source replacement definition");
            Equal(expected.GetProperty("upgraded").GetBoolean(), s.State.GetField(result[i], s.RuleSet.TypeByQualifiedId("base-spire:Card").FieldIndex("Upgraded")).AsBool(), "source upgrade/reset state");
        }
        var calls = s.Debug!.Capture().Where(r => r.Kind == SessionDebugKind.RandomCall && r.Subject == stream).ToArray();
        var pools = row.GetProperty("selections").EnumerateArray().SelectMany(x => x.GetProperty("pools").EnumerateArray()).ToArray();
        Equal(pools.Length, calls.Length, "one draw per actual source transformation");
        for (var i = 0; i < calls.Length; i++)
        {
            var call = calls[i].Random!; True(!call.CandidatesTruncated && call.Candidates is not null, "complete sampled candidate evidence");
            True(call.Candidates!.Select(c => TextField(s, c, "CardBlueprint", "DefinitionId")).SequenceEqual(pools[i].GetProperty("ids").EnumerateArray().Select(v => v.GetString())), "all source transformation candidates in source order");
        }
        Equal(0L, s.Debug.DroppedRecords, "normal event diagnostics are complete");
        True(row.GetProperty("after").GetProperty("other_random_unchanged").GetBoolean(), "native non-event streams unchanged");
        Equal(otherRandom, JsonSerializer.Serialize(s.Random.Capture().Streams.Where(r => r.Name != stream)), "effects do not consume other source streams");
        Submit(s, "CloseNormalEvent", visit);
        Equal("map", TextField(s, Player(s), "Player", "Room"), "same real event returns to map");
        var choices = s.LegalActions(s.Participant(0)).Single(a => a.Definition.QualifiedId == "base-spire:ChooseMapPoint");
        var coordinates = choices.Bindings.Select(b => b[0].AsRef()).Select(n => new[] { IntField(s, n, "MapNode", "Col"), IntField(s, n, "MapNode", "Row") }).OrderBy(c => c[0]).ThenBy(c => c[1]);
        Equal(JsonSerializer.Serialize(row.GetProperty("successors")), JsonSerializer.Serialize(coordinates), "exact native map successors");
        var final = s.State.StateHash();
        var close = s.RuleSet.Actions.Single(a => a.QualifiedId == "base-spire:CloseNormalEvent");
        Throws<EngineFaultException>(() => s.Submit(close, s.Participant(0), [Value.OfRef(visit)])); Equal(final, s.State.StateHash(), "close cannot repeat effects");
        AssertNoDevelopmentActions(s);
    }

    private static void UnimplementedNormalEvent(DslPackage package)
    {
        var s = EventDeckPrefix(package, "morphic-normal-13", "base-spire:cursed-pearl", 1, claimGold: true);
        var node = Members(s, "MapNodes").Single(n => IntField(s, n, "MapNode", "Col") == 0 && IntField(s, n, "MapNode", "Row") == 2);
        Throws<EngineFaultException>(() => Submit(s, "ChooseMapPoint", node));
        True(s.Fault!.Message.Contains("selected source normal Event effect", StringComparison.Ordinal), "unimplemented source effects still fail explicitly");
        var run = s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:Run").Id).Single();
        Equal("base-spire:this-or-that", TextField(s, run, "Run", "SelectedNormalEvent"), "retained registered unsupported source input");
        Equal(1, Members(s, "RunVisits").Count, "unsupported effect cannot create an empty visit");
    }

    private static void EventDeckAutomatic(DslPackage package)
    {
        using var copy = new Copy(package.Root);
        copy.Include("event-deck-probe.spire", """
            action TrimDeck { role = player parameters = { keep = Int(min = 0, max = 2) } body = {
              forEach = { query = collection(RunDeck) item = card body = {
                if = { condition = greaterOrEqual(collectionIndex(RunDeck, card), keep) then = {
                  call = { procedure = RemovePermanentCard args = { owner = first(type(Player)) card = card } }
                } }
              } }
            } }
            action QuestCard { role = player locals = { card = Binding } body = {
              create = { type = Card scope = frameScope() into = card collection = RunDeck fields = { Owner = field(first(type(Player)), Player.Owner) DefinitionId = "probe:quest" Kind = "quest" } }
            } }
            action UpgradeAndEnchant { role = player body = {
              call = { procedure = UpgradeCard args = { card = first(collection(RunDeck)) } }
              call = { procedure = EnchantCard args = { owner = first(type(Player)) card = first(collection(RunDeck)) definition = "base-spire:sown" amount = 1 } }
            } }
            """);
        var loaded = copy.Load();
        void Trim(Session s, long count)
        { s.Submit(s.RuleSet.Actions.Single(a => a.QualifiedId == "base-spire:TrimDeck"), s.Participant(0), [Value.Of(count)]); Healthy(s); }
        foreach (var count in new[] { 0, 1, 2 })
        {
            var s = Start(loaded); Submit(s, "Ironclad.Initialize"); Trim(s, count);
            var old = Members(s, "RunDeck").ToArray();
            if (count > 0) Submit(s, "UpgradeAndEnchant");
            Submit(s, "EnterDevelopmentMorphicGrove");
            var stream = "base-spire:morphic-grove:visit:1";
            s.Answer(new DecisionAnswer(s.PendingDecision!.RequestId, Flag: true)); Healthy(s);
            True(s.PendingDecision is null, "zero/one/two candidates are automatic, never fake a 2..2 decision");
            Equal((long)count, s.Random.DrawCount(stream), "automatic selection draws only for actual cards");
            Equal(0L, IntField(s, Player(s), "Player", "Gold"), "source GROUP pays even for an automatic empty domain");
            var result = Members(s, "RunDeck"); Equal(count, result.Count, "automatic replacements preserve count");
            True(old.All(c => !s.State.Exists(c)) && result.All(c => !old.Contains(c)), "each old permanent identity is replaced");
            True(result.All(c => !s.State.GetField(c, s.RuleSet.TypeByQualifiedId("base-spire:Card").FieldIndex("Upgraded")).AsBool()), "upgraded original does not upgrade its replacement");
            Equal(0, s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:CardAttachment").Id).Count, "old enchantment is removed rather than copied");
        }
        var blocked = Start(loaded); Submit(blocked, "Ironclad.Initialize"); Trim(blocked, 0);
        Submit(blocked, "Greed.Obtain"); Submit(blocked, "QuestCard");
        var protectedCards = Members(blocked, "RunDeck").ToArray();
        Submit(blocked, "EnterDevelopmentAromaOfChaos");
        blocked.Answer(new DecisionAnswer(blocked.PendingDecision!.RequestId, Flag: true)); Healthy(blocked);
        True(blocked.PendingDecision is null && protectedCards.SequenceEqual(Members(blocked, "RunDeck")), "Eternal and Quest remain outside the transformation selection");
        Equal(0L, blocked.Random.DrawCount("base-spire:aroma-of-chaos:visit:1"), "excluded cards do not consume transformation RNG");
        var common = loaded.CreateSession(20261018, ["player"], debug: new SessionDebugOptions(MaxRecords: 40_000, CaptureValues: true));
        common.Start(); Submit(common, "Ironclad.Initialize"); Trim(common, 0); Submit(common, "Anger.Obtain");
        Submit(common, "EnterDevelopmentAromaOfChaos");
        common.Answer(new DecisionAnswer(common.PendingDecision!.RequestId, Flag: true)); Healthy(common);
        var pick = common.Debug!.Capture().Single(r => r.Kind == SessionDebugKind.RandomCall && r.Subject == "base-spire:aroma-of-chaos:visit:1").Random!;
        using var native = JsonDocument.Parse(typeof(PackageChecks).Assembly.GetManifestResourceStream("BaseSpire.Dev.fixtures.normal-event-deck-vector.json")!);
        // Derive this one exclusion from the independent full native Ironclad pool,
        // rather than calculating the expected pool from the DSL registration.
        var expected = native.RootElement.GetProperty("branches")[0].GetProperty("selections")[0].GetProperty("pools")[0].GetProperty("ids")
            .EnumerateArray().Select(v => v.GetString()).Where(id => id != "base-spire:anger").ToArray();
        Equal(79, expected.Length, "source common-card transformation excludes its own definition");
        True(pick.Candidates!.Select(c => TextField(common, c, "CardBlueprint", "DefinitionId")).SequenceEqual(expected), "a nonbasic original retains every other native candidate in order");
        var otherPool = Start(loaded); Submit(otherPool, "Ironclad.Initialize"); Trim(otherPool, 0); Submit(otherPool, "Whistle.Obtain");
        Submit(otherPool, "EnterDevelopmentAromaOfChaos");
        Throws<EngineFaultException>(() => otherPool.Answer(new DecisionAnswer(otherPool.PendingDecision!.RequestId, Flag: true)));
        True(otherPool.Fault!.Message.Contains("transformation source pool", StringComparison.Ordinal), "unmapped original pools fail instead of borrowing or shrinking Ironclad candidates");
        Equal(0L, otherPool.Random.DrawCount("base-spire:aroma-of-chaos:visit:1"), "unmapped source pool is rejected before random sampling");
    }
}
