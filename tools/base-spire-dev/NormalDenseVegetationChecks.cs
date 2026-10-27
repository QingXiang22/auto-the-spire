using System.Text.Json;
using CoreTheSpire.Engine;

internal static partial class PackageChecks
{
    private static void RunNormalDenseVegetation(DslPackage package)
    {
        using var data = JsonDocument.Parse(typeof(PackageChecks).Assembly.GetManifestResourceStream("BaseSpire.Dev.fixtures.normal-dense-vegetation-vector.json")!);
        var vector = data.RootElement;
        foreach (var scenario in new[] { "trudge", "fight-skip", "fight-claim", "trudge-death", "fight-death" })
        {
            var branch = scenario == "trudge-death" ? "trudge" : scenario == "fight-death" ? "fight-skip" : scenario;
            var expected = vector.GetProperty("branches").EnumerateArray().Single(e => e.GetProperty("branch").GetString() == branch);
            var s = package.CreateSession(1, ["player"], vector.GetProperty("seed").GetString()); s.Start(); Submit(s, "StartNormalRun");
            var option = s.PendingDecision!.Candidates.Single(o => TextField(s, o, "NeowOption", "RelicId") == vector.GetProperty("neow_relic").GetString());
            s.Answer(new DecisionAnswer(s.PendingDecision.RequestId, Instances: [option]));
            s.Answer(new DecisionAnswer(s.PendingDecision!.RequestId, Flag: true));
            InstanceId Node(string name) => Members(s, "MapNodes").Single(n => IntField(s, n, "MapNode", "Col") == vector.GetProperty(name)[0].GetInt64() && IntField(s, n, "MapNode", "Row") == vector.GetProperty(name)[1].GetInt64());
            InstanceId Ref(InstanceId instance, string type, string field) => s.State.GetField(instance, s.RuleSet.TypeByQualifiedId("base-spire:" + type).FieldIndex(field)).AsRef();
            void Fight()
            {
                using var input = NormalCombatInput();
                var priorities = input.RootElement.GetProperty("combat_action_priority").EnumerateArray().Select(a => "base-spire:" + a.GetString()).ToArray();
                for (var step = 0; Active(s) && step < 200; step++)
                {
                    var actions = s.LegalActions(s.Participant(0));
                    var action = priorities.Select(id => actions.FirstOrDefault(a => a.Definition.QualifiedId == id)).First(a => a is not null)!;
                    s.Submit(action.Definition, s.Participant(0), action.Bindings.Count > 0 ? action.Bindings[0] : []); Healthy(s);
                }
                Equal("victory", Outcome(s), "real starter cards win without injected combat state");
            }
            Submit(s, "ChooseMapPoint", Node("first")); Fight(); Submit(s, "CloseDevelopmentRewards");
            var deck = Members(s, "RunDeck").ToArray();
            if (scenario == "trudge-death") Set(s, Player(s), "Player", "Hp", Value.Of(8));
            Submit(s, "ChooseMapPoint", Node("unknown"));
            var run = s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:Run").Id).Single();
            var parent = Members(s, "RunVisits").Last();
            var visit = Ref(parent, "RunVisit", "Event");
            void Reject(Action action)
            {
                var hash = s.State.StateHash(); var rng = JsonSerializer.Serialize(s.Random.Capture());
                Throws<EngineFaultException>(action);
                Equal(hash, s.State.StateHash(), "rejected event command preserves complete state");
                Equal(rng, JsonSerializer.Serialize(s.Random.Capture()), "rejected event command preserves all streams");
                Healthy(s);
            }
            void RejectVisitAction(string name) => Reject(() => s.Submit(s.RuleSet.Actions.Single(a => a.QualifiedId == "base-spire:" + name), s.Participant(0), [Value.OfRef(visit)]));
            void Defeated()
            {
                Equal("defeated", TextField(s, run, "Run", "Phase"), "death ends Run");
                Equal("defeated", TextField(s, parent, "RunVisit", "Status"), "death ends original parent");
                Equal("defeated", TextField(s, visit, "EventVisit", "Status"), "death ends original event");
                True(!Legal(s, "ChooseMapPoint") && !Legal(s, "CloseNormalEvent") && !Legal(s, "CloseDevelopmentRewards"), "death exposes no map, event continuation or rewards");
                Equal(0, Members(s, "CardRewardOptions").Count, "death offers no cards");
                Equal(0, Members(s, "PotionRewardOptions").Count, "death offers no potions");
                RejectVisitAction("CloseNormalEvent"); RejectVisitAction("DenseVegetationFight");
            }
            Equal("base-spire:dense-vegetation", TextField(s, visit, "EventVisit", "DefinitionId"), "full source pool naturally selects DenseVegetation");
            Equal(parent, Ref(visit, "EventVisit", "RunVisit"), "event retains its actual normal parent");
            void State(JsonElement e)
            {
                Equal(e.GetProperty("hp").GetInt64(), Hp(s), "native boundary HP");
                Equal(e.GetProperty("gold").GetInt64(), IntField(s, Player(s), "Player", "Gold"), "native boundary gold");
                var random = e.GetProperty("random");
                var names = new Dictionary<string, string> { ["UpFront"] = "up_front", ["Shuffle"] = "shuffle", ["UnknownMapPoint"] = "unknown_map_point", ["CombatCardGeneration"] = "combat_card_generation", ["CombatPotionGeneration"] = "combat_potion_generation", ["CombatCardSelection"] = "combat_card_selection", ["CombatEnergyCosts"] = "combat_energy_costs", ["CombatTargets"] = "combat_targets", ["MonsterAi"] = "monster_ai", ["Niche"] = "niche", ["CombatOrbs"] = "combat_orbs", ["TreasureRoomRelics"] = "treasure_room_relics" };
                void Stream(string name, JsonElement value)
                {
                    var snapshot = s.Random.Capture();
                    var actual = snapshot.Streams.SingleOrDefault(r => r.Name == name);
                    var words = actual?.BackendState;
                    if (actual is null)
                    {
                        Equal(0L, value.GetProperty("counter").GetInt64(), "only undrawn streams may remain lazy: " + name);
                        True(snapshot.ExplicitSeeds.ContainsKey(name), "lazy source stream has an explicit production seed: " + name);
                        words = new Xoshiro256StarStar(snapshot.ExplicitSeeds[name], protectZeroState: false).Capture();
                    }
                    Equal(value.GetProperty("counter").GetInt64(), s.Random.DrawCount(name), "native complete stream counter: " + name);
                    True(words!.SequenceEqual(Enumerable.Range(0, 4).Select(i => value.GetProperty("state" + i).GetUInt64())), "native four-word stream state: " + name);
                }
                foreach (var pair in random.GetProperty("run").GetProperty("rngs").EnumerateObject()) Stream("base-spire:run:" + names[pair.Name], pair.Value);
                foreach (var pair in random.GetProperty("player").GetProperty("rngs").EnumerateObject()) Stream("base-spire:player:" + pair.Name.ToLowerInvariant(), pair.Value);
                Stream("base-spire:dense-vegetation:visit:2", random.GetProperty("event_rng"));
            }
            if (scenario != "trudge-death") State(expected.GetProperty("entered"));
            var decision = s.PendingDecision!;
            s.Answer(new DecisionAnswer(decision.RequestId, Flag: branch != "trudge")); Healthy(s);
            Reject(() => s.Answer(new DecisionAnswer(decision.RequestId, Flag: branch != "trudge")));
            if (scenario == "trudge-death")
            {
                Equal(0L, Hp(s), "TRUDGE_ON can kill before granting its rolled gold");
                Equal(163L, IntField(s, Player(s), "Player", "Gold"), "source gold still follows lethal damage");
                Defeated();
                Console.WriteLine("ok   EVENT controlled DenseVegetation lethal trudge: source payment order, no map or continuation");
                continue;
            }
            State(expected.GetProperty("selected"));
            if (branch == "trudge") Submit(s, "CloseNormalEvent", visit);
            else
            {
                True(!Legal(s, "CloseNormalEvent") && !Legal(s, "ChooseMapPoint"), "rest cannot close before its required fight");
                RejectVisitAction("CloseNormalEvent");
                Set(s, visit, "EventVisit", "RunVisit", Value.OfRef(Members(s, "RunVisits").First()));
                RejectVisitAction("DenseVegetationFight");
                Set(s, visit, "EventVisit", "RunVisit", Value.OfRef(parent));
                Submit(s, "DenseVegetationFight", visit);
                RejectVisitAction("DenseVegetationFight");
                var child = Members(s, "RunVisits").Last();
                Equal(parent, Ref(child, "RunVisit", "Parent"), "child refers to original parent visit");
                Equal(child, Ref(visit, "EventVisit", "ChildVisit"), "same event owns the child");
                Equal(Ref(parent, "RunVisit", "Node"), Ref(child, "RunVisit", "Node"), "child retains the same map node");
                Equal(3L, IntField(s, run, "Run", "TotalFloor"), "forced combat adds no floor");
                Equal(1L, IntField(s, run, "Run", "NormalEncounterIndex"), "forced combat consumes no normal encounter");
                State(expected.GetProperty("combat_start").GetProperty("state"));
                var enemies = s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:Enemy").Id).ToArray();
                True(enemies.Select(e => IntField(s, e, "Enemy", "MaxHp")).SequenceEqual(expected.GetProperty("combat_start").GetProperty("monsters").EnumerateArray().Select(e => e.GetProperty("hp").GetInt64())), "native complete four-slot HP order");
                foreach (var enemy in enemies) Equal("base-spire:wriggler", TextField(s, enemy, "Enemy", "DefinitionId"), "real source Wriggler");
                if (scenario == "fight-death")
                {
                    var rewardsDraws = s.Random.DrawCount("base-spire:player:rewards");
                    for (var turn = 0; Active(s) && turn < 30; turn++)
                    {
                        Submit(s, "EndTurn");
                    }
                    Equal("defeat", Outcome(s), "real Wriggler attacks cause death");
                    Equal("defeated", TextField(s, child, "RunVisit", "Status"), "death ends child");
                    Equal(rewardsDraws, s.Random.DrawCount("base-spire:player:rewards"), "child death consumes no reward RNG");
                    Defeated();
                    Console.WriteLine("ok   EVENT normal DenseVegetation real child death: both visits defeated, no rewards or map");
                    continue;
                }
                Fight(); State(expected.GetProperty("reward").GetProperty("state"));
                Set(s, visit, "EventVisit", "ChildVisit", Value.OfRef(parent));
                RejectNormalAction(s, "CloseDevelopmentRewards"); Healthy(s);
                Set(s, visit, "EventVisit", "ChildVisit", Value.OfRef(child));
                True(!Legal(s, "CloseNormalEvent") && !Legal(s, "ChooseMapPoint"), "finished parent cannot bypass child rewards");
                var rewards = expected.GetProperty("reward");
                var cards = Members(s, "CardRewardOptions").ToArray();
                True(cards.Select(c => CardDefinition(s, c)).SequenceEqual(rewards.GetProperty("cards").EnumerateArray().Select(c => c.GetProperty("id").GetString())), "native ordered reward options");
                Equal(rewards.GetProperty("gold").GetInt64(), IntField(s, Player(s), "Player", "GoldRewardAmount"), "native reward amount");
                var potion = Members(s, "PotionRewardOptions").Single();
                Equal(rewards.GetProperty("potion").GetString(), TextField(s, potion, "Potion", "DefinitionId"), "native real potion offer");
                var random = JsonSerializer.Serialize(s.Random.Capture());
                if (branch == "fight-claim")
                {
                    Submit(s, "ClaimDevelopmentGoldReward"); Submit(s, "ClaimDevelopmentCardReward", cards[0]); Submit(s, "ClaimDevelopmentPotionReward");
                    True(Members(s, "RunDeck").Contains(cards[0]) && Members(s, "Potions").Contains(potion), "actual offered instances transfer permanently");
                }
                else { Submit(s, "SkipDevelopmentCardReward"); Submit(s, "SkipDevelopmentPotionReward"); }
                Submit(s, "CloseDevelopmentRewards");
                Equal(random, JsonSerializer.Serialize(s.Random.Capture()), "claims, skips and parent/child close never reroll");
                Equal("closed", TextField(s, child, "RunVisit", "Status"), "child closes once");
                Equal(parent, Ref(run, "Run", "CurrentVisit"), "closed child restores same parent");
                True(cards.Skip(branch == "fight-claim" ? 1 : 0).All(c => !s.State.Exists(c)), "unselected reward instances are cleaned");
                RejectNormalAction(s, "CloseDevelopmentRewards");
            }
            State(expected.GetProperty("final"));
            Equal("closed", TextField(s, parent, "RunVisit", "Status"), "parent closes exactly once");
            Equal("closed", TextField(s, visit, "EventVisit", "Status"), "original event closes without restarting its page");
            Equal("map", TextField(s, Player(s), "Player", "Room"), "real event returns to map");
            Equal(expected.GetProperty("cursor").GetInt64(), IntField(s, run, "Run", "NormalEventIndex"), "one source event selection");
            Equal(expected.GetProperty("total_floor").GetInt64(), IntField(s, run, "Run", "TotalFloor"), "child does not add a map floor");
            True(deck.SequenceEqual(Members(s, "RunDeck").Take(deck.Length)), "original permanent identities retain order");
            True(Members(s, "RunDeck").Select(c => CardDefinition(s, c)).SequenceEqual(expected.GetProperty("deck").EnumerateArray().Select(c => c.GetProperty("id").GetString())), "complete source final permanent deck");
            var choose = s.LegalActions(s.Participant(0)).Single(a => a.Definition.QualifiedId == "base-spire:ChooseMapPoint");
            var coords = choose.Bindings.Select(b => b[0].AsRef()).Select(n => new[] { IntField(s, n, "MapNode", "Col"), IntField(s, n, "MapNode", "Row") }).OrderBy(n => n[0]).ThenBy(n => n[1]);
            Equal(JsonSerializer.Serialize(expected.GetProperty("successors")), JsonSerializer.Serialize(coords), "exact native map successors");
            var state = s.State.StateHash(); var draws = s.Random.TotalDraws;
            foreach (var name in new[] { "CloseNormalEvent", "DenseVegetationFight" })
                Throws<EngineFaultException>(() => s.Submit(s.RuleSet.Actions.Single(a => a.QualifiedId == "base-spire:" + name), s.Participant(0), [Value.OfRef(visit)]));
            Equal(state, s.State.StateHash(), "repeat event actions cannot replay effects"); Equal(draws, s.Random.TotalDraws, "repeat event actions consume no random word");
            AssertNoDevelopmentActions(s);
            Console.WriteLine("ok   EVENT normal DenseVegetation " + branch + ": native full RNG, resources, real child combat/rewards and exact map return");
        }
    }
}
