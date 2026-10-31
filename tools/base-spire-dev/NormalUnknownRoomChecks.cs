using CoreTheSpire.Engine;
using System.Text.Json;

internal static partial class PackageChecks
{
    private static void RunNormalEventSelection(DslPackage package) => RunNormalUnknownRoom(package, eventSelection: true);
    private static void RunNormalUnknownRoom(DslPackage package, bool eventSelection = false)
    {
        using var data = JsonDocument.Parse(typeof(PackageChecks).Assembly.GetManifestResourceStream(eventSelection
            ? "BaseSpire.Dev.fixtures.normal-event-selection-vector.json" : "BaseSpire.Dev.fixtures.normal-unknown-room-vector.json")!);
        var expected = data.RootElement;
        var s = package.CreateSession(1, ["player"], expected.GetProperty("seed").GetString()); s.Start(); Submit(s, "StartNormalRun");
        var option = s.PendingDecision!.Candidates.Single(o => TextField(s, o, "NeowOption", "RelicId") == expected.GetProperty("neow_relic").GetString());
        s.Answer(new DecisionAnswer(s.PendingDecision.RequestId, Instances: [option]));
        s.Answer(new DecisionAnswer(s.PendingDecision!.RequestId, Flag: true));
        InstanceId Node(string name) => Members(s, "MapNodes").Single(n => IntField(s, n, "MapNode", "Col") == expected.GetProperty(name)[0].GetInt64()
            && IntField(s, n, "MapNode", "Row") == expected.GetProperty(name)[1].GetInt64());
        const string stream = "base-spire:run:unknown_map_point";
        var choose = s.RuleSet.Actions.Single(a => a.QualifiedId == "base-spire:ChooseMapPoint");
        void Reject(InstanceId node)
        {
            var state = s.State.StateHash(); var random = JsonSerializer.Serialize(s.Random.Capture());
            Throws<EngineFaultException>(() => s.Submit(choose, s.Participant(0), [Value.OfRef(node)]));
            Equal(state, s.State.StateHash(), "illegal map selection changes no state");
            Equal(random, JsonSerializer.Serialize(s.Random.Capture()), "illegal map selection changes no random stream");
        }
        Reject(Node("unknown")); // A real future Unknown cannot be entered early.
        Submit(s, "ChooseMapPoint", Node("first")); Equal(0L, s.Random.DrawCount(stream), "direct Monster does not roll unknown odds");
        using var input = NormalCombatInput();
        var priorities = input.RootElement.GetProperty("combat_action_priority").EnumerateArray().Select(a => "base-spire:" + a.GetString()).ToArray();
        for (var step = 0; Active(s) && step < 100; step++)
        {
            var actions = s.LegalActions(s.Participant(0));
            var action = priorities.Select(id => actions.FirstOrDefault(a => a.Definition.QualifiedId == id)).First(a => a is not null)!;
            s.Submit(action.Definition, s.Participant(0), action.Bindings.Count > 0 ? action.Bindings[0] : []); Healthy(s);
        }
        Equal("victory", Outcome(s), "starter deck naturally wins the first source encounter");
        Submit(s, "CloseDevelopmentRewards"); Reject(Node("first"));
        var run = s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:Run").Id).Single();
        Equal(1L, IntField(s, run, "Run", "NormalEventIndex"), "Neow advances the same event cursor once");
        var before = s.State.StateHash(); var draws = JsonSerializer.Serialize(s.Random.Capture());
        var view = s.Observe(s.Participant(0), "base-spire:PlayerView"); s.LegalActions(s.Participant(0));
        var hidden = s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:UnknownRoomOdds").Id);
        True(view.Instances.All(i => !hidden.Contains(i.Id)), "hidden odds have no public instance");
        Equal(before, s.State.StateHash(), "query does not change cumulative odds");
        Equal(draws, JsonSerializer.Serialize(s.Random.Capture()), "query does not consume RNG");
        if (eventSelection)
        {
            var randomBefore = s.Random.Capture();
            Submit(s, "ChooseMapPoint", Node("unknown"));
            True(s.PendingDecision is { Kind: DecisionResponseKind.Bool }, "the retained Aroma failure now reaches its real options");
            Equal(expected.GetProperty("selected").GetString(), TextField(s, run, "Run", "SelectedNormalEvent"), "real source selected identity");
            Equal(expected.GetProperty("cursor").GetInt64(), IntField(s, run, "Run", "NormalEventIndex"), "complete rejected and accepted source slots");
            var history = s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:NormalEventHistory").Id);
            True(history.Select(h => TextField(s, h, "NormalEventHistory", "DefinitionId")).Order(StringComparer.Ordinal)
                .SequenceEqual(expected.GetProperty("visited").EnumerateArray().Select(v => v.GetString())), "source final event history");
            Equal(expected.GetProperty("selection_floor").GetInt64() + 1, IntField(s, run, "Run", "TotalFloor"), "one destination is appended after source eligibility");
            Equal(expected.GetProperty("hp").GetInt64(), Hp(s), "native combat permanent HP");
            Equal(expected.GetProperty("gold").GetInt64(), IntField(s, Player(s), "Player", "Gold"), "no extra event payment");
            Equal(2, Members(s, "RunVisits").Count, "the formerly unsupported event owns one actual new visit");
            True(s.State.GetField(Node("unknown"), s.RuleSet.TypeByQualifiedId("base-spire:MapNode").FieldIndex("Visited")).AsBool(), "successful event entry visits the real source node");
            var remainingBefore = randomBefore.Streams.Where(r => r.Name != stream);
            var remainingAfter = s.Random.Capture().Streams.Where(r => r.Name != stream);
            Equal(JsonSerializer.Serialize(remainingBefore), JsonSerializer.Serialize(remainingAfter), "event selection adds no random consumption");
            var source = expected.GetProperty("random"); var actualRandom = s.Random.Capture().Streams.Single(r => r.Name == stream);
            Equal(source.GetProperty("counter").GetInt64(), s.Random.DrawCount(stream), "one actual Unknown classification draw");
            True(actualRandom.BackendState!.SequenceEqual(Enumerable.Range(0, 4).Select(i => source.GetProperty("state" + i).GetUInt64())), "complete native classification state");
            var currentOdds = Members(s, "UnknownRoomOddsOrder");
            for (var i = 0; i < currentOdds.Count; i++) Equal(expected.GetProperty("odds")[i].GetInt64(), IntField(s, currentOdds[i], "UnknownRoomOdds", "Current"), "source odds after Event roll");
            True(view.Instances.All(i => !history.Contains(i.Id)), "event selection history is hidden");
            Console.WriteLine("ok   EVENT normal Neow, real victory and Unknown→Event preserve source selection, cursor, history and RNG before the real Aroma branch decision");
            return;
        }
        Submit(s, "ChooseMapPoint", Node("unknown"));
        var visit = Members(s, "RunVisits").Last();
        Equal(1, s.State.GetField(visit, s.RuleSet.TypeByQualifiedId("base-spire:RunVisit").FieldIndex("MapType")).AsEnumOrdinal(), "actual Monster retains original Unknown MapType");
        Equal(1, s.State.GetField(Node("unknown"), s.RuleSet.TypeByQualifiedId("base-spire:MapNode").FieldIndex("Type")).AsEnumOrdinal(), "map node remains Unknown");
        Equal(expected.GetProperty("room").GetString(), TextField(s, visit, "RunVisit", "RoomType"), "actual source room type");
        Equal(expected.GetProperty("encounter").GetString(), TextField(s, visit, "RunVisit", "EncounterId"), "Unknown Monster consumes the next actual source encounter");
        Equal(2L, IntField(s, visit, "RunVisit", "StableId"), "next real visit increments once");
        Equal(expected.GetProperty("total_floor").GetInt64(), IntField(s, run, "Run", "TotalFloor"), "source total floor");
        Equal(expected.GetProperty("hp").GetInt64(), Hp(s), "source permanent HP after real starter-card combat");
        Equal(expected.GetProperty("gold").GetInt64(), IntField(s, Player(s), "Player", "Gold"), "source unclaimed rewards");
        var enemies = s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:Enemy").Id);
        Equal(expected.GetProperty("monsters").GetArrayLength(), enemies.Count, "complete real source formation");
        for (var i = 0; i < enemies.Count; i++)
        {
            var e = expected.GetProperty("monsters")[i];
            Equal(e.GetProperty("id").GetString(), TextField(s, enemies[i], "Enemy", "DefinitionId"), "source monster identity");
            Equal(e.GetProperty("hp").GetInt64(), IntField(s, enemies[i], "Enemy", "Hp"), "source niche HP");
            Equal(e.GetProperty("move").GetString(), TextField(s, enemies[i], "Enemy", "MoveId"), "source selected move");
        }
        var odds = Members(s, "UnknownRoomOddsOrder");
        for (var i = 0; i < odds.Count; i++) Equal(expected.GetProperty("odds")[i].GetInt64(), IntField(s, odds[i], "UnknownRoomOdds", "Current"), "source binary32 cumulative odds");
        var sourceRandom = expected.GetProperty("random"); var actual = s.Random.Capture().Streams.Single(r => r.Name == stream);
        Equal(sourceRandom.GetProperty("counter").GetInt64(), s.Random.DrawCount(stream), "exactly one real unknown draw");
        True(actual.BackendState is not null && actual.BackendState.SequenceEqual(Enumerable.Range(0, 4).Select(i => sourceRandom.GetProperty("state" + i).GetUInt64())), "complete native unknown stream state");
        Reject(Node("unknown")); AssertNoDevelopmentActions(s);
        Console.WriteLine("ok   UNKNOWN normal Neow, real victory, reward close and Unknown→Monster preserve source map, visit, odds and RNG");
    }
}
