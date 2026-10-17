#if REFERENCE_SIM
using System.Text.Json;
using BaseSpire.SinglePlayer;
using BaseSpire.Slice;
using BaseSpire.Slice.Rooms;
using CoreTheSpire.Engine;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Demo;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Runs;

namespace Differential;

/// <summary>Independent native event execution from declared controlled initial states; never normal-win evidence.</summary>
internal static class EventContentChecks
{
    public static int Run(string output)
    {
        Directory.CreateDirectory(output);
        var evidence = new List<object>();
        var boundaries = new List<object>();
        try
        {
            CombatReplayDemo.EnsureModelsRegistered();
            var events = new IsolatedEventSession();
            BaseSpireRuntimeFactory? mapFactory = null;
            foreach (var seed in new[] { "ENV-T04-RNG-SHUFFLE-001", "ACT1-BASIC-10", "ENV-T03-SEED-L" })
            {
                var all = New("TabletOfTruth", () => (EventModel)ModelDb.Get(typeof(TabletOfTruth)).MutableClone(), seed);
                var original = Members(all.Base, "LongTermDeck").ToArray();
                Capture(all, "initial");
                for (var page = 1; page <= 5; page++)
                {
                    Step(all, TabletOfTruthEvent.Decipher, "DECIPHER");
                    Capture(all, "decipher-" + page);
                    Require(Members(all.Base, "LongTermDeck").SequenceEqual(original), "random/all upgrades retain actual permanent instances");
                    Require(Number(all.Base, all.Owner, "TabletOfTruthDecipherCount") == page, "exact page count");
                }
                Require(Number(all.Base, all.Owner, "MaxHp") == 1, "fifth decipher leaves max HP one");
                Require(Members(all.Base, "LongTermDeck").All(card => Get(all.Base, card, "Upgraded").AsBool()), "fifth page upgrades every remaining card");
                evidence.Add(new { seed, scenario = "five-pages", passed = true });

                var heal = New("TabletOfTruth", () => (EventModel)ModelDb.Get(typeof(TabletOfTruth)).MutableClone(), seed, hp: 79);
                Step(heal, TabletOfTruthEvent.Smash, "SMASH"); Capture(heal, "heal-capped");
                Require(Number(heal.Base, heal.Owner, "Hp") == 80, "heal caps at maximum");
                var giveUp = New("TabletOfTruth", () => (EventModel)ModelDb.Get(typeof(TabletOfTruth)).MutableClone(), seed);
                Step(giveUp, TabletOfTruthEvent.Decipher, "DECIPHER");
                Step(giveUp, TabletOfTruthEvent.GiveUp, "GIVE_UP"); Capture(giveUp, "give-up");
                var dead = New("TabletOfTruth", () => (EventModel)ModelDb.Get(typeof(TabletOfTruth)).MutableClone(), seed, hp: 2, maxHp: 2);
                Step(dead, TabletOfTruthEvent.Decipher, "DECIPHER"); Capture(dead, "fatal-max-hp");
                Require(dead.Base.Status == SessionStatus.Terminal && Number(dead.Base, dead.Owner, "Hp") == 0,
                    "fatal decipher publishes terminal and does not upgrade");
                Require(!Members(dead.Base, "LongTermDeck").Any(card => Get(dead.Base, card, "Upgraded").AsBool()), "no upgrade after death");
                evidence.Add(new { seed, scenario = "heal-giveup-death", passed = true });

                if (seed == "ENV-T04-RNG-SHUFFLE-001")
                {
                    var sparse = New("TabletOfTruth", () => (EventModel)ModelDb.Get(typeof(TabletOfTruth)).MutableClone(), seed, hp: 8);
                    var keep = Members(sparse.Base, "LongTermDeck").First();
                    sparse.Base.State.RunAtomic(() =>
                    { foreach (var card in Members(sparse.Base, "LongTermDeck").Skip(1).ToArray()) sparse.Base.State.DestroyInstance(card); });
                    foreach (var card in sparse.Player.Deck.Cards.Skip(1).ToArray()) sparse.Player.Deck.RemoveInternal(card);
                    Step(sparse, TabletOfTruthEvent.Decipher, "DECIPHER"); Capture(sparse, "one-upgradable-card");
                    Step(sparse, TabletOfTruthEvent.Decipher, "DECIPHER"); Capture(sparse, "no-upgradable-cards");
                    Require(Members(sparse.Base, "LongTermDeck").Single() == keep, "singleton upgrade preserves instance");
                    Require(sparse.Model.Rng.Counter == 1 && Number(sparse.Base, sparse.Owner, "Hp") == 8,
                        "singleton draws once, empty upgrade domain draws zero, max-HP loss does not damage HP below new maximum");
                    evidence.Add(new { seed, scenario = "single-empty-upgrade-domain", passed = true });
                }

                var cheese = New("RoomFullOfCheese", () => (EventModel)ModelDb.Get(typeof(RoomFullOfCheese)).MutableClone(), seed);
                var before = Members(cheese.Base, "LongTermDeck").ToArray();
                var draws = cheese.Base.Random.DrawCount(VanillaRandomLayout.EnginePlayerStreamName("Rewards"));
                Submit(cheese.Base, RoomFullOfCheeseEvent.Gorge);
                Require(cheese.Base.Status == SessionStatus.AwaitingDecision, "Gorge pauses before permanent acquisition");
                var decision = cheese.Base.PendingDecision!;
                var offers = decision.Candidates.ToArray();
                Require(offers.Length == 8 && decision.Minimum == 2 && decision.Maximum == 2, "eight distinct offers, exactly two choices");
                Require(offers.Select(card => Get(cheese.Base, card, "DefinitionId").AsString()).Distinct().Count() == 8, "reward definitions do not repeat");
                Require(Members(cheese.Base, "LongTermDeck").SequenceEqual(before), "offers do not join deck before answer");
                var selector = new RewardSelection(offers.Select(card => Card(cheese.Base, card)).ToArray());
                _ = new RunDriver(cheese.Native, selector);
                cheese.Model.ChooseOption(cheese.Model.CurrentOptions.Single(o => o.Key == "GORGE")).GetAwaiter().GetResult();
                // Non-prefix order distinguishes answer order from source membership/order.
                cheese.Base.Answer(new DecisionAnswer(decision.RequestId, [offers[6], offers[1]]));
                Capture(cheese, "gorge-two-selected");
                Require(Members(cheese.Base, "LongTermDeck").SequenceEqual(before.Concat(new[] { offers[6], offers[1] })), "chosen instances append in answer order");
                Require(Members(cheese.Base, "EventCardOffers").Count == 0, "unselected offers cleaned up");
                Require(cheese.Base.Random.DrawCount(VanillaRandomLayout.EnginePlayerStreamName("Rewards")) - draws == 8,
                    "exactly eight definition draws; ForNonCombatWithUniformOdds disables upgrade rolls");
                var hash = cheese.Base.State.StateHash();
                try { Submit(cheese.Base, RoomFullOfCheeseEvent.Gorge); throw new CheckFailed("closed event accepted replay"); }
                catch (EngineFaultException e) when (e.Code == EngineFaultCode.IllegalAction) { }
                Require(cheese.Base.State.StateHash() == hash, "replay cannot acquire duplicate cards");
                evidence.Add(new { seed, scenario = "gorge-selection-order-cleanup", offers = selector.Offers, passed = true });
            }
            foreach (var hasBasic in new[] { true, false })
            {
                const string seed = "ACT1-BASIC-10";
                var wood = New("WoodCarvings", () => (EventModel)ModelDb.Get(typeof(WoodCarvings)).MutableClone(), seed);
                if (!hasBasic)
                {
                    wood.Base.State.RunAtomic(() => { foreach (var card in Members(wood.Base, "LongTermDeck").ToArray()) wood.Base.State.DestroyInstance(card); });
                    foreach (var card in wood.Player.Deck.Cards.ToArray()) wood.Player.Deck.RemoveInternal(card);
                }
                Require(wood.Model.IsAllowed(wood.Native) == hasBasic, "native source qualification");
                var s = wood.Base;
                var nodes = Members(s, "MapNodes");
                var children = s.RuleSet.Relations.Single(r => r.QualifiedId == "base-spire:MapChildren");
                var parent = nodes.First(p => s.State.RelationTargets(children.Id, p).Any(c => Get(s, c, "Type").AsEnumOrdinal() == 1));
                var target = s.State.RelationTargets(children.Id, parent).First(c => Get(s, c, "Type").AsEnumOrdinal() == 1);
                s.State.RunAtomic(() =>
                {
                    Set(s, wood.Owner, "CurrentEventId", Value.Of("")); Set(s, wood.Owner, "MapPhase", Value.Of(2L));
                    Set(s, wood.Owner, "CurrentNode", Value.OfRef(parent)); Set(s, wood.Owner, "MapFloor", Value.Of(Number(s, parent, "Row") + 1));
                    Set(s, wood.Owner, "EventsVisited", Value.Of(0L));
                    foreach (var field in new[] { "UnknownMonsterOdds", "UnknownEliteOdds", "UnknownTreasureOdds", "UnknownShopOdds" }) Set(s, wood.Owner, field, Value.Of(0L));
                    foreach (var entry in Members(s, "RunEventQueue").ToArray()) s.State.DestroyInstance(entry);
                    var type = s.RuleSet.TypeByQualifiedId("base-spire:RunRoomIdentity");
                    var queue = s.RuleSet.Collections.Single(c => c.QualifiedId == "base-spire:RunEventQueue");
                    foreach (var name in new[] { "WoodCarvings", "TabletOfTruth" })
                    {
                        var entry = s.State.CreateInstance(type.Id, s.State.ScopeOf(wood.Owner));
                        Set(s, entry, "SourceId", Value.Of(name)); s.State.AddToCollection(queue.Id, entry);
                    }
                });
                var choose = s.RuleSet.Actions.Single(a => a.QualifiedId == "base-spire:ChooseMapPoint");
                if (hasBasic)
                {
                    try { s.Submit(choose, SliceManifest.Player, [Value.OfRef(target)]); throw new CheckFailed("eligible missing WoodCarvings effects did not fail explicitly"); }
                    catch (EngineFaultException e) when (e.Message.Contains("selected event WoodCarvings has no registered executable module", StringComparison.Ordinal)) { }
                }
                else
                {
                    s.Submit(choose, SliceManifest.Player, [Value.OfRef(target)]);
                    Require(Get(s, wood.Owner, "CurrentEventId").AsString() == "TabletOfTruth", "no removable basic card skips WoodCarvings without needing its effects module");
                }
                evidence.Add(new { seed, scenario = "wood-source-qualification", hasBasic, passed = true });
            }
            Write(0, null);
            Console.WriteLine($"Event content: {evidence.Count} independent scenarios passed; controlled initial states, no normal victory claim.");
            return 0;

            Pair New(string name, Func<EventModel> create, string seed, int hp = 80, int maxHp = 80)
            {
                var native = new RunState(seed, new Sts2Sim.Core.Content.ActDefinition[] { new Overgrowth(), new Hive(), new Glory() });
                var player = Player.CreateForNewRun(ModelDb.Character<Sts2Sim.Core.Models.Characters.Ironclad>(), native);
                native.AddPlayer(player); _ = native.CurrentAncientEventType;
                player.Creature.SetMaxHpInternal(maxHp); player.Creature.SetCurrentHpInternal(hp);
                var model = create(); model.AssignOwner(player); model.BeginEvent(native);
                Session s;
                if (name == "WoodCarvings")
                {
                    // These two integration witnesses exercise real unknown-room qualification on an actual map.
                    mapFactory ??= new BaseSpireRuntimeFactory(RunConfiguration.Ironclad(0, BaseSpireScope.DevRun));
                    var run = mapFactory.CreateSession(seed); run.Start(); s = run.Session;
                }
                else
                {
                    s = events.Create(seed, hp: hp, maxHp: maxHp);
                    IsolatedEventSession.Enter(s, name);
                }
                var owner = Members(s, "RunStates").Single();
                return new(s, owner, native, player, model, seed);
            }

            void Step(Pair pair, string action, string option)
            {
                var expected = pair.Model.CurrentOptions.Where(o => !o.IsLocked).Select(o => o.Key).Order(StringComparer.Ordinal);
                string Key(string id) => id.EndsWith("Decipher", StringComparison.Ordinal) ? "DECIPHER"
                    : id.EndsWith("Smash", StringComparison.Ordinal) ? "SMASH" : "GIVE_UP";
                var actual = pair.Base.LegalActions(SliceManifest.Player).Where(a => a.Definition.QualifiedId.Contains("TabletOfTruth", StringComparison.Ordinal))
                    .Select(a => Key(a.Definition.QualifiedId)).Order(StringComparer.Ordinal);
                Require(expected.SequenceEqual(actual), "page option domains differ");
                Submit(pair.Base, action);
                pair.Model.ChooseOption(pair.Model.CurrentOptions.Single(o => o.Key == option)).GetAwaiter().GetResult();
            }

            void Capture(Pair pair, string label)
            {
                Require(Number(pair.Base, pair.Owner, "Hp") == pair.Player.Creature.CurrentHp, label + ": current HP differs");
                Require(Number(pair.Base, pair.Owner, "MaxHp") == pair.Player.Creature.MaxHp, label + ": max HP differs");
                var cards = Members(pair.Base, "LongTermDeck").Select(card => Card(pair.Base, card)).ToArray();
                Require(cards.SequenceEqual(pair.Player.Deck.Cards.Select(Card)), label + ": ordered permanent deck differs");
                var closed = Get(pair.Base, pair.Owner, "CurrentEventId").AsString().Length == 0;
                Require(closed == (pair.Model.IsFinished || pair.Player.Creature.CurrentHp == 0), label + ": event closure differs");
                CompareRandom(pair.Base, pair.Model.Rng, EventRandomAsset.StreamName(pair.Model.GetType().Name));
                CompareRandom(pair.Base, pair.Player.PlayerRng.Rewards, VanillaRandomLayout.EnginePlayerStreamName("Rewards"));
                boundaries.Add(new { pair.Seed, label, hp = pair.Player.Creature.CurrentHp, maxHp = pair.Player.Creature.MaxHp, cards, closed,
                    eventDraws = pair.Model.Rng.Counter, rewardsDraws = pair.Player.PlayerRng.Rewards.Counter });
            }
        }
        catch (Exception e) { Write(1, e.ToString()); Console.Error.WriteLine(e); return 1; }

        void Write(int code, string? error) => File.WriteAllText(Path.Combine(output, "result.json"),
            JsonSerializer.Serialize(new { exit_code = code, controlledInitialState = true, normalVictoryEvidence = false,
                error, evidence, boundaries }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void CompareRandom(Session s, Sts2Sim.Core.Random.Rng native, string name)
    {
        // Seeded streams are instantiated lazily. Do not create a stream by drawing merely to observe it.
        if (native.Counter == 0) { Require(s.Random.DrawCount(name) == 0, name + ": unexpected draw"); return; }
        var actual = EngineSnapshot.Streams(s).SingleOrDefault(x => x.Stream == name)
            ?? throw new CheckFailed("Missing event comparison stream " + name + "; declared=" + string.Join(",", s.Random.Streams));
        var state = native.ToSerializable();
        Require(actual.Draws == native.Counter && actual.Fingerprint == RngSourceCompat.FingerprintText(state.state0, state.state1, state.state2, state.state3),
            name + ": random draws/state differ");
    }
    private static void Submit(Session s, string name) => s.Submit(s.RuleSet.Actions.Single(a => a.QualifiedId == "base-spire:" + name), SliceManifest.Player, []);
    private static string Card(Session s, InstanceId card) => Get(s, card, "DefinitionId").AsString() + (Get(s, card, "Upgraded").AsBool() ? "+" : "");
    private static string Card(CardModel card) => ReferenceBackend.Project(card.Id) + (card.IsUpgraded ? "+" : "");
    private static IReadOnlyList<InstanceId> Members(Session s, string name) => s.State.CollectionMembers(s.RuleSet.Collections.Single(c => c.QualifiedId == "base-spire:" + name).Id);
    private static Value Get(Session s, InstanceId item, string field) => s.State.GetField(item, s.RuleSet.Type(s.State.TypeIdOf(item)).FieldIndex(field));
    private static long Number(Session s, InstanceId item, string field) => Get(s, item, field).AsInt();
    private static void Set(Session s, InstanceId item, string field, Value value) => s.State.SetField(item, s.RuleSet.Type(s.State.TypeIdOf(item)).FieldIndex(field), value);
    private static void Require(bool condition, string message) { if (!condition) throw new CheckFailed(message); }
    private sealed record Pair(Session Base, InstanceId Owner, RunState Native, Player Player, EventModel Model, string Seed);
    private sealed class RewardSelection(string[] expected) : IRunDecisionSource
    {
        public string[] Offers { get; private set; } = [];
        public Task<MapPoint> ChooseMapPointAsync(IReadOnlyList<MapPoint> options) => throw new CheckFailed("unexpected map decision");
        public Task<CombatDecision> ChooseCombatActionAsync(CombatState state) => throw new CheckFailed("unexpected combat decision");
        public Task<IReadOnlyList<CardModel>> ChooseCardsAsync(CardSelectionRequest request)
        {
            Offers = request.Candidates.Select(Card).ToArray();
            Require(Offers.SequenceEqual(expected) && request.MinCount == 2 && request.MaxCount == 2,
                "native offer definitions/order/upgrades or choice bounds differ: base=" + string.Join(",", expected)
                + "; native=" + string.Join(",", Offers) + $"; bounds={request.MinCount}..{request.MaxCount}");
            return Task.FromResult<IReadOnlyList<CardModel>>([request.Candidates[6], request.Candidates[1]]);
        }
    }
}
#endif
