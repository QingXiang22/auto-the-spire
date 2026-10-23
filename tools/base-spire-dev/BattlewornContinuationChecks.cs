using System.Text.Json;
using CoreTheSpire.Engine;

internal static partial class PackageChecks
{
    private static Dictionary<string, string[]> RunBattlewornContinuationChecks(DslPackage package, Action<string, Action> check)
    {
        InstanceId Option(Session s, int index) => s.PendingDecision!.Candidates[index];
        void Choose(Session s, int index) { s.Answer(new DecisionAnswer(s.PendingDecision!.RequestId, [Option(s, index)])); Healthy(s); }
        const string name = "VISIT BattlewornDummy real upgrade-tier combat resumes same parent with source RNG and no ordinary rewards";
        check(name, () =>
        {
            using var fixture = JsonDocument.Parse(typeof(PackageChecks).Assembly.GetManifestResourceStream("BaseSpire.Dev.fixtures.battleworn-continuation-vector.json")!);
            using var copy = new Copy(package.Root);
            copy.Include("dummy-continuation-probe.spire", """
                action ControlledFeed { role = player locals = { card = Binding } body = {
                  call = { procedure = CreateCombatCard args = { owner = first(type(Player)) definition = "base-spire:feed" } into = card }
                  call = { procedure = AddGeneratedToHand args = { card = card } }
                } }
                """);
            var rules = copy.Load();
            foreach (var row in fixture.RootElement.GetProperty("vectors").EnumerateArray())
            {
                var s = rules.CreateSession(1, ["player"], row.GetProperty("seed").GetString()); s.Start(); Submit(s, "Ironclad.Initialize");
                Set(s, Player(s), "Player", "Hp", Value.Of(50));
                var deck = Members(s, "RunDeck").ToArray();
                Submit(s, "EnterDevelopmentBattlewornDummy");
                var visit = s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:EventVisit").Id).Single();
                var decision = s.PendingDecision!;
                Equal(DecisionResponseKind.EntityRefList, decision.Kind, "all three source settings are real option instances");
                Equal(3, decision.Candidates.Count, "all source settings retained");
                var selected = Option(s, 1);
                Choose(s, 1);
                var child = s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:EventCombatVisit").Id).Single();
                InstanceId Ref(InstanceId id, string type, string field) => s.State.GetField(id, s.RuleSet.TypeByQualifiedId("base-spire:" + type).FieldIndex(field)).AsRef();
                Equal(visit, Ref(child, "EventCombatVisit", "Parent"), "child points to original event");
                Equal(child, Ref(visit, "EventVisit", "CombatVisit"), "same event retains child");
                Equal("suspended", TextField(s, visit, "EventVisit", "Status"), "parent remains suspended while fighting");
                Equal(row.GetProperty("hp").GetInt64(), EnemyHp(s), "real tier-two enemy HP");
                Equal(row.GetProperty("move").GetString(), MoveText(s, FirstTarget(s), "Enemy", "MoveId"), "real idle move");
                RejectNormalAction(s, "EndDevelopmentCombat"); AssertNoDevelopmentActions(s);
                var hash = s.State.StateHash(); var rng = JsonSerializer.Serialize(s.Random.Capture());
                Throws<EngineFaultException>(() => s.Answer(new DecisionAnswer(decision.RequestId, [selected])));
                Equal(hash, s.State.StateHash(), "old setting cannot restart battle"); Equal(rng, JsonSerializer.Serialize(s.Random.Capture()), "old setting cannot redraw");
                void State(JsonElement expected)
                {
                    Equal(expected.GetProperty("hp").GetInt64(), Hp(s), "source healing boundary");
                    Equal(expected.GetProperty("max_hp").GetInt64(), Field(s, "MaxHp"), "source Feed max HP");
                    Equal(expected.GetProperty("gold").GetInt64(), Field(s, "Gold"), "no ordinary gold reward");
                    foreach (var pair in new[] { ("niche", "base-spire:run:niche"), ("shuffle", "base-spire:run:shuffle"), ("rewards", "base-spire:player:rewards"), ("event_rng", "base-spire:battleworn-dummy:visit:1") })
                    {
                        var e = expected.GetProperty(pair.Item1); var actual = s.Random.Capture();
                        var words = actual.Streams.SingleOrDefault(r => r.Name == pair.Item2)?.BackendState
                            ?? new Xoshiro256StarStar(actual.ExplicitSeeds[pair.Item2], protectZeroState: false).Capture();
                        Equal(e.GetProperty("counter").GetInt64(), s.Random.DrawCount(pair.Item2), "source cursor: " + pair.Item1);
                        True(words!.SequenceEqual(Enumerable.Range(0, 4).Select(i => e.GetProperty("state" + i).GetUInt64())), "source four-word state: " + pair.Item1);
                    }
                }
                State(row.GetProperty("started"));
                for (var turn = 1; turn <= 3; turn++)
                {
                    if (turn == 3 && row.GetProperty("mode").GetString() == "kill")
                    {
                        Set(s, FirstTarget(s), "Enemy", "Hp", Value.Of(5)); Submit(s, "ControlledFeed");
                        Submit(s, "PlayAttackFromHand", Hand(s).Single(c => CardDefinition(s, c) == "base-spire:feed"));
                    }
                    else Submit(s, "EndDevelopmentTurn");
                    if (Active(s)) Submit(s, "BeginDevelopmentTurn");
                }
                Equal("victory", Outcome(s), "real kill or source timeout completes combat");
                Equal(row.GetProperty("timed_out").GetBoolean(), s.State.GetField(child, s.RuleSet.TypeByQualifiedId("base-spire:EventCombatVisit").FieldIndex("TimedOut")).AsBool(), "actual timeout retained");
                State(row.GetProperty("resumed"));
                True(deck.SequenceEqual(Members(s, "RunDeck")), "permanent identities and order unchanged");
                True(deck.Select((c, i) => (c, i)).Where(p => CardUpgraded(s, p.c)).Select(p => p.i).SequenceEqual(row.GetProperty("resumed").GetProperty("upgraded").EnumerateArray().Select(x => x.GetInt32())), "exact source upgraded permanent instances");
                Equal("event", TextField(s, Player(s), "Player", "Room"), "combat restores original event room");
                Equal("finished", TextField(s, visit, "EventVisit", "Status"), "same parent finishes after continuation");
                Equal("closed", TextField(s, child, "EventCombatVisit", "Status"), "child commits once");
                True(!Legal(s, "CloseDevelopmentRewards") && !Legal(s, "ChooseMapPoint"), "event fight produces no ordinary rewards or fake map");
                Equal(10, s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:Card").Id).Count, "combat cards cleared before continuation");
                Submit(s, "CloseDevelopmentEvent", visit); Equal("none", TextField(s, Player(s), "Player", "Room"), "close returns to original development location");
                hash = s.State.StateHash(); rng = JsonSerializer.Serialize(s.Random.Capture());
                Throws<EngineFaultException>(() => s.Submit(s.RuleSet.Actions.Single(a => a.QualifiedId == "base-spire:CloseDevelopmentEvent"), s.Participant(0), [Value.OfRef(visit)]));
                Equal(hash, s.State.StateHash(), "repeated close is atomic"); Equal(rng, JsonSerializer.Serialize(s.Random.Capture()), "repeated close cannot replay upgrade");
                Submit(s, "EnterDevelopmentBattlewornDummy");
                Equal(0, s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:EventCombatVisit").Id).Count, "new visit removes old child before replacing parent");
                var next = s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:EventVisit").Id).Single();
                True(next != visit, "new entry creates a fresh parent");
                Equal(2L, IntField(s, next, "EventVisit", "Sequence"), "new entry advances event stream identity");
                Choose(s, 1);
                for (var turn = 0; turn < 3; turn++) { Submit(s, "EndDevelopmentTurn"); if (Active(s)) Submit(s, "BeginDevelopmentTurn"); }
                Equal("finished", TextField(s, next, "EventVisit", "Status"), "second visit resumes only its own parent");
                Equal(0L, s.Random.DrawCount("base-spire:battleworn-dummy:visit:2"), "second timeout never replays first upgrades");
            }
        });
        check("VISIT BattlewornDummy unsupported settings and invalid answers cannot create a fight or consume RNG", () =>
        {
            foreach (var ordinal in new[] { 0, 2 })
            {
                var s = Start(package); Submit(s, "Ironclad.Initialize"); Submit(s, "EnterDevelopmentBattlewornDummy");
                var decision = s.PendingDecision!; var state = s.State.StateHash(); var random = JsonSerializer.Serialize(s.Random.Capture());
                Throws<EngineFaultException>(() => s.Answer(new DecisionAnswer(decision.RequestId, [Player(s)])));
                Equal(state, s.State.StateHash(), "invalid instance rejected before writes"); Equal(random, JsonSerializer.Serialize(s.Random.Capture()), "invalid instance draws nothing");
                Throws<EngineFaultException>(() => s.Answer(new DecisionAnswer(decision.RequestId, [Option(s, ordinal)])));
                Equal(SessionStatus.Faulted, s.Status, "unimplemented source choice explicitly faults");
                True(s.Fault!.Message.Contains("unsupported_capability", StringComparison.Ordinal), "unsupported source reward is named");
                Equal(state, s.State.StateHash(), "unsupported option writes no combat state"); Equal(random, JsonSerializer.Serialize(s.Random.Capture()), "unsupported option draws nothing");
                Equal(0, s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:EventCombatVisit").Id).Count, "no unsupported child visit");
            }
            var dirty = Start(package); Submit(dirty, "Ironclad.Initialize"); Submit(dirty, "AddDevelopmentTarget");
            RejectNormalAction(dirty, "EnterDevelopmentBattlewornDummy");
        });
        check("VISIT BattlewornDummy death never resumes and empty or single upgrade domains preserve source draw count", () =>
        {
            using var copy = new Copy(package.Root); copy.Include("dummy-boundary-probe.spire", """
                action ControlledDeath { role = player body = { call = { procedure = LosePlayerHp args = { owner = first(type(Player)) amount = 999 } } } }
                action ControlledKill { role = player locals = { card = Binding } body = {
                  call = { procedure = CreateCombatCard args = { owner = first(type(Player)) definition = "base-spire:feed" } into = card }
                  call = { procedure = AddGeneratedToHand args = { card = card } }
                } }
                """);
            var rules = copy.Load();
            foreach (var available in new[] { -1, 0, 1 })
            {
                var s = Start(rules); Submit(s, "Ironclad.Initialize"); var deck = Members(s, "RunDeck").ToArray();
                if (available >= 0) foreach (var card in deck.Skip(available)) Set(s, card, "Card", "Upgraded", Value.Of(true));
                Submit(s, "EnterDevelopmentBattlewornDummy"); Choose(s, 1);
                var visit = s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:EventVisit").Id).Single();
                var child = s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:EventCombatVisit").Id).Single();
                if (available < 0)
                {
                    Submit(s, "ControlledDeath"); Equal("defeat", Outcome(s), "controlled HP-loss uses real defeat lifecycle");
                    Equal("defeated", TextField(s, visit, "EventVisit", "Status"), "dead parent never resumes");
                    Equal("defeated", TextField(s, child, "EventCombatVisit", "Status"), "dead child is committed");
                    True(!Legal(s, "CloseDevelopmentEvent") && !Legal(s, "ChooseMapPoint") && !Legal(s, "CloseDevelopmentRewards"), "death exposes no continuation");
                    True(deck.All(c => !CardUpgraded(s, c)), "death never upgrades");
                }
                else
                {
                    Set(s, FirstTarget(s), "Enemy", "Hp", Value.Of(5)); Submit(s, "ControlledKill");
                    Submit(s, "PlayAttackFromHand", Hand(s).Single(c => CardDefinition(s, c) == "base-spire:feed"));
                    Equal("finished", TextField(s, visit, "EventVisit", "Status"), "small domain still finishes");
                    True(deck.All(c => CardUpgraded(s, c)), "single eligible instance upgrades without replacing deck");
                }
                Equal(0L, s.Random.DrawCount("base-spire:battleworn-dummy:visit:1"), "zero or one candidate and death do not shuffle");
                Equal(0L, s.Random.DrawCount("base-spire:player:rewards"), "no ordinary rewards on boundary paths");
            }
        });
        return new() { ["base-spire:battleworn-dummy"] = [name] };
    }
}
