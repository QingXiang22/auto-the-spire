using System.Text.Json;
using CoreTheSpire.Engine;

internal static partial class PackageChecks
{
    private static void RunNormalSapphireSeed(DslPackage package)
    {
        using var data = JsonDocument.Parse(typeof(PackageChecks).Assembly.GetManifestResourceStream("BaseSpire.Dev.fixtures.normal-sapphire-seed-vector.json")!);
        var vector = data.RootElement;
        foreach (var expected in vector.GetProperty("branches").EnumerateArray())
        {
            var eat = expected.GetProperty("branch").GetString() == "eat";
            var s = package.CreateSession(1, ["player"], vector.GetProperty("seed").GetString()); s.Start(); Submit(s, "StartNormalRun");
            var option = s.PendingDecision!.Candidates.Single(o => TextField(s, o, "NeowOption", "RelicId") == vector.GetProperty("neow_relic").GetString());
            s.Answer(new DecisionAnswer(s.PendingDecision.RequestId, Instances: [option]));
            s.Answer(new DecisionAnswer(s.PendingDecision!.RequestId, Flag: true));
            InstanceId Node(string name) => Members(s, "MapNodes").Single(n => IntField(s, n, "MapNode", "Col") == vector.GetProperty(name)[0].GetInt64() && IntField(s, n, "MapNode", "Row") == vector.GetProperty(name)[1].GetInt64());
            Submit(s, "ChooseMapPoint", Node("first"));
            using var input = NormalCombatInput();
            var priorities = input.RootElement.GetProperty("combat_action_priority").EnumerateArray().Select(a => "base-spire:" + a.GetString()).ToArray();
            for (var step = 0; Active(s) && step < 100; step++)
            {
                var actions = s.LegalActions(s.Participant(0));
                var action = priorities.Select(id => actions.FirstOrDefault(a => a.Definition.QualifiedId == id)).First(a => a is not null)!;
                s.Submit(action.Definition, s.Participant(0), action.Bindings.Count > 0 ? action.Bindings[0] : []); Healthy(s);
            }
            Equal("victory", Outcome(s), "normal starter deck wins without injected state");
            Submit(s, "CloseDevelopmentRewards");
            Equal(expected.GetProperty("before_hp").GetInt64(), Hp(s), "native real combat prefix HP");
            var deck = Members(s, "RunDeck").ToArray();
            Submit(s, "ChooseMapPoint", Node("unknown"));
            var run = s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:Run").Id).Single();
            var visit = Members(s, "RunVisits").Last();
            var eventVisit = s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:EventVisit").Id).Single();
            InstanceId Ref(InstanceId instance, string type, string field) => s.State.GetField(instance, s.RuleSet.TypeByQualifiedId("base-spire:" + type).FieldIndex(field)).AsRef();
            Equal(visit, Ref(eventVisit, "EventVisit", "RunVisit"), "event owns the actual normal visit");
            Equal(eventVisit, Ref(visit, "RunVisit", "Event"), "normal visit owns the same event state");
            Equal("base-spire:sapphire-seed", TextField(s, eventVisit, "EventVisit", "DefinitionId"), "full source selector naturally reaches supported event");
            Equal(expected.GetProperty("cursor").GetInt64(), IntField(s, run, "Run", "NormalEventIndex"), "complete source scan cursor");
            Equal(2L, IntField(s, visit, "RunVisit", "StableId"), "one new visit after the actual combat");
            Equal(1, s.State.GetField(visit, s.RuleSet.TypeByQualifiedId("base-spire:RunVisit").FieldIndex("MapType")).AsEnumOrdinal(), "event keeps original Unknown map type");
            Equal("event", TextField(s, visit, "RunVisit", "RoomType"), "actual room is Event");
            Equal(expected.GetProperty("total_floor").GetInt64(), IntField(s, run, "Run", "TotalFloor"), "entry appends one floor before effects");
            var random = JsonSerializer.Serialize(s.Random.Capture());
            s.Answer(new DecisionAnswer(s.PendingDecision!.RequestId, Flag: eat)); Healthy(s);
            var selected = expected.GetProperty("selected_index").GetInt32();
            if (eat)
            {
                var decision = s.PendingDecision!;
                True(decision.Candidates.SequenceEqual(expected.GetProperty("upgrade_candidates").EnumerateArray().Select(i => deck[i.GetInt32()])), "complete ordered source upgrade candidates");
                Equal(expected.GetProperty("hp").GetInt64(), Hp(s), "healing precedes upgrade answer");
                var hash = s.State.StateHash();
                Throws<EngineFaultException>(() => s.Answer(new DecisionAnswer(decision.RequestId, Instances: [Player(s)])));
                Equal(hash, s.State.StateHash(), "invalid card answer cannot repeat healing");
                s.Answer(new DecisionAnswer(decision.RequestId, Instances: [deck[selected]])); Healthy(s);
            }
            else True(s.PendingDecision is null, "PLANT automatically enchants the first eligible source card");
            Equal(expected.GetProperty("hp").GetInt64(), Hp(s), "source branch HP");
            Equal(expected.GetProperty("gold").GetInt64(), IntField(s, Player(s), "Player", "Gold"), "event does not credit or charge extra gold");
            True(deck.SequenceEqual(Members(s, "RunDeck")), "event retains every permanent instance in order");
            for (var i = 0; i < deck.Length; i++)
            {
                var card = expected.GetProperty("deck")[i];
                Equal(card.GetProperty("id").GetString(), CardDefinition(s, deck[i]), "source permanent definition");
                Equal(card.GetProperty("upgraded").GetBoolean(), CardUpgraded(s, deck[i]), "source permanent upgrade");
                var attachments = s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:CardAttachment").Id).Where(a => Ref(a, "CardAttachment", "Card") == deck[i]).ToArray();
                Equal(card.GetProperty("enchantments").GetArrayLength(), attachments.Length, "source attachment count per permanent instance");
                if (attachments.Length > 0)
                {
                    Equal("base-spire:sown", TextField(s, attachments[0], "CardAttachment", "DefinitionId"), "real source Sown");
                    Equal(1L, IntField(s, attachments[0], "CardAttachment", "Amount"), "source magnitude");
                }
            }
            Equal("finished", TextField(s, visit, "RunVisit", "Phase"), "finished effect waits for close");
            var earlierVisit = Members(s, "RunVisits").First();
            Set(s, eventVisit, "EventVisit", "RunVisit", Value.OfRef(earlierVisit));
            True(!Legal(s, "CloseNormalEvent"), "event cannot close through another normal visit");
            Set(s, eventVisit, "EventVisit", "RunVisit", Value.OfRef(visit));
            True(!Legal(s, "ChooseMapPoint") && !Legal(s, "CloseDevelopmentEvent"), "only normal event close can resume the map");
            Submit(s, "CloseNormalEvent", eventVisit);
            Equal("closed", TextField(s, eventVisit, "EventVisit", "Status"), "event closes once");
            Equal("closed", TextField(s, visit, "RunVisit", "Status"), "same normal visit closes");
            Equal("map", TextField(s, Player(s), "Player", "Room"), "event returns to real map");
            var choose = s.LegalActions(s.Participant(0)).Single(a => a.Definition.QualifiedId == "base-spire:ChooseMapPoint");
            var coords = choose.Bindings.Select(b => b[0].AsRef()).Select(n => new[] { IntField(s, n, "MapNode", "Col"), IntField(s, n, "MapNode", "Row") }).OrderBy(n => n[0]).ThenBy(n => n[1]);
            Equal(JsonSerializer.Serialize(expected.GetProperty("successors")), JsonSerializer.Serialize(coords), "exact source successors after close");
            var close = s.RuleSet.Actions.Single(a => a.QualifiedId == "base-spire:CloseNormalEvent"); var state = s.State.StateHash();
            Throws<EngineFaultException>(() => s.Submit(close, s.Participant(0), [Value.OfRef(eventVisit)]));
            Equal(state, s.State.StateHash(), "duplicate close cannot replay event or visit commit");
            Equal(random, JsonSerializer.Serialize(s.Random.Capture()), "event effects, decisions, queries and close consume no random word");
            True(expected.GetProperty("random_unchanged").GetBoolean(), "native branch consumes no random word");
            var source = expected.GetProperty("random"); var actual = s.Random.Capture().Streams.Single(r => r.Name == "base-spire:run:unknown_map_point");
            True(actual.BackendState!.SequenceEqual(Enumerable.Range(0, 4).Select(i => source.GetProperty("state" + i).GetUInt64())), "native complete Unknown stream after natural Event");
            Equal(source.GetProperty("counter").GetInt64(), s.Random.DrawCount("base-spire:run:unknown_map_point"), "one real classification draw");
            AssertNoDevelopmentActions(s);
            Console.WriteLine("ok   EVENT normal SapphireSeed " + (eat ? "EAT" : "PLANT") + ": native resources, permanent instances, visit relations and exact map return");
        }
    }
}
