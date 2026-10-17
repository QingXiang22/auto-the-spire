#if REFERENCE_SIM
using System.Text.Json;
using BaseSpire.Slice;
using BaseSpire.Slice.Rooms;
using CoreTheSpire.Engine;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Demo;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Runs;
using static Differential.IsolatedEventSession;

namespace Differential;

/// <summary>Event-layer execution independent of map paths, enemy support and public response shapes.</summary>
internal static class EventModuleChecks
{
    public static int Run(string output, string? only)
    {
        Directory.CreateDirectory(output);
        var evidence = new List<object>();
        var gaps = new List<object>();
        string? error = null;
        string? contentDigest = null;
        try
        {
            CombatReplayDemo.EnsureModelsRegistered();
            var harness = new IsolatedEventSession(); contentDigest = harness.ContentDigest;
            if (only is not null && !harness.Modules.Contains(only, StringComparer.Ordinal))
                throw new ArgumentException("--event requires a registered production event module");
            foreach (var seed in new[] { "ENV-T04-RNG-SHUFFLE-001", "ACT1-BASIC-10", "ENV-T03-SEED-L" })
            {
                    foreach (var id in harness.Modules.Where(id => only is null || only == id))
                    {
                        var s = harness.Create(seed, gold: 150);
                        Require(s.Random.TotalDraws == 0 && Members(s, "MapNodes").Count == 0,
                            "controlled event startup must not generate maps or consume battle/initialization RNG");
                        Enter(s, id);
                        Require(Get(s, Owner(s), "CurrentEventId").AsString() == id, "catalogue Enter must establish the actual visit");
                        Require(s.LegalActions(SliceManifest.Player).Any(a => !a.Definition.QualifiedId.Contains(":Probe", StringComparison.Ordinal)),
                            "real catalogue choices must be available");
                        evidence.Add(new { seed, scenario = "production-catalogue-entry", id, passed = true });
                    }
                if (only is null or "MorphicGrove")
                {
                    var loner = New<MorphicGrove>(seed, hp: 73, gold: 150);
                    Act(loner, MorphicGroveEvent.Loner, "LONER"); Compare(loner);
                    Require(Number(loner.Base, "MaxHp") == 85 && Number(loner.Base, "Hp") == 78, "maximum HP gain also heals exactly five");
                    evidence.Add(new { seed, scenario = "morphic-loner", passed = true });

                    foreach (var count in new[] { 10, 2, 1, 0 })
                    {
                        var pair = New<MorphicGrove>(seed, gold: 173, count: count);
                        Submit(pair.Base, "ProbeMorphicEligibility");
                        Require(Get(pair.Base, Owner(pair.Base), "ProbeAllowed").AsBool() == pair.Model.IsAllowed(pair.Native), "source qualification differs");
                        var originals = Members(pair.Base, "LongTermDeck").ToArray();
                        Submit(pair.Base, MorphicGroveEvent.Group);
                        Require(Number(pair.Base, "Gold") == 0, "all gold is lost before any card choice");
                        if (count > 2)
                        {
                            var decision = pair.Base.PendingDecision ?? throw new CheckFailed("simultaneous two-card choice missing");
                            Require(decision.Minimum == 2 && decision.Maximum == 2 && decision.Candidates.SequenceEqual(originals),
                                "one exact-count decision over actual ordered permanent instances");
                            Require(EventDraws(pair.Base, "MorphicGrove") == 0, "replacement randomness must wait for the complete answer");
                            var state = pair.Base.State.StateHash(); var draws = pair.Base.Random.TotalDraws;
                            try { pair.Base.Answer(new DecisionAnswer(decision.RequestId, [originals[0], originals[0]])); throw new CheckFailed("duplicate selected card accepted"); }
                            catch (EngineFaultException) { }
                            Require(pair.Base.State.StateHash() == state && pair.Base.Random.TotalDraws == draws, "invalid answer must not mutate state or RNG");
                            var selector = new Selection(pair.Player.Deck.Cards.Select(Card).ToArray(), [6, 0]);
                            _ = new RunDriver(pair.Native, selector);
                            pair.Model.ChooseOption(pair.Model.CurrentOptions.Single(o => o.Key == "GROUP")).GetAwaiter().GetResult();
                            pair.Base.Answer(new DecisionAnswer(decision.RequestId, [originals[6], originals[0]]));
                            Require(Members(pair.Base, "LongTermDeck").Take(count - 2).SequenceEqual(originals.Where((_, i) => i is not (0 or 6))),
                                "unselected duplicate instances retain their identity and order");
                        }
                        else
                        {
                            Require(pair.Base.Status == SessionStatus.Running, "at most requested count auto-selects without a decision");
                            pair.Model.ChooseOption(pair.Model.CurrentOptions.Single(o => o.Key == "GROUP")).GetAwaiter().GetResult();
                        }
                        Compare(pair);
                        Require(EventDraws(pair.Base, "MorphicGrove") == Math.Min(2, count), "one draw per selected original, no replacement re-enters the selection");
                        Require(Members(pair.Base, "LongTermDeck").Count == count && !Members(pair.Base, "LongTermDeck").Intersect(originals.Take(count <= 2 ? count : 0)).Any(),
                            "auto transformation replaces every selected original once");
                        evidence.Add(new { seed, scenario = "morphic-group", deckCount = count, passed = true });
                    }
                    foreach (var gold in new[] { 99, 100 })
                    {
                        var pair = New<MorphicGrove>(seed, gold: gold);
                        Submit(pair.Base, "ProbeMorphicEligibility");
                        Require(Get(pair.Base, Owner(pair.Base), "ProbeAllowed").AsBool() == pair.Model.IsAllowed(pair.Native), "100-gold qualification boundary differs");
                    }
                }
                if (only is null or "JungleMazeAdventure")
                {
                    foreach (var (solo, hp) in new[] { (false, 40), (true, 40), (true, 18) })
                    {
                        var pair = New<JungleMazeAdventure>(seed, hp: hp);
                        Require(EventDraws(pair.Base, "JungleMazeAdventure") == 2, "both cached gold amounts are rolled on entry");
                        MatchRandom(pair.Base, pair.Model.Rng, "JungleMazeAdventure");
                        Act(pair, solo ? JungleMazeAdventureEvent.SoloQuest : JungleMazeAdventureEvent.JoinForces,
                            solo ? "SOLO_QUEST" : "JOIN_FORCES");
                        Compare(pair, compareEventRandom: !solo);
                        if (solo)
                        {
                            // Separate source-guided witness, not a patched native model or a corrected comparison stream.
                            var vector = new Sts2Sim.Core.Random.Rng(unchecked(pair.Native.Rng.Seed
                                + Sts2Sim.Core.Helpers.StringHelper.GetDeterministicHashCode(pair.Model.Id.Entry)));
                            var expectedGold = (int)(150m + (decimal)vector.NextFloat(-15f, 15f));
                            _ = vector.NextFloat(-15f, 15f);
                            vector.Shuffle(new List<int> { 0, 1, 2 });
                            MatchRandom(pair.Base, vector, "JungleMazeAdventure");
                            Require(Number(pair.Base, "Gold") == 99 + expectedGold && pair.Model.Rng.Counter == 2 && vector.Counter == 4,
                                "source visual shuffle consumes two draws which the native event explicitly omits");
                            Require(pair.Base.Status == (hp <= 18 ? SessionStatus.Terminal : SessionStatus.Running)
                                && Number(pair.Base, "Hp") == Math.Max(0, hp - 18),
                                "solo grants source gold then closes as a living visit or defeat");
                            gaps.Add(new { seed, scenario = "jungle-solo", initialHp = hp, nativeDraws = pair.Model.Rng.Counter,
                                baseDraws = EventDraws(pair.Base, "JungleMazeAdventure"), resourcesMatched = true,
                                sourceGuidedVectorMatched = true, strictNativeComparison = "known-reference-divergence",
                                reason = "reference JungleMazeAdventure deviation #84 omits exactly two visual shuffle draws; reference unchanged" });
                        }
                        else evidence.Add(new { seed, scenario = "jungle-join", passed = true });
                    }
                }
                if (only is null or "SapphireSeed")
                {
                    foreach (var eat in new[] { true, false })
                    {
                        var pair = New<SapphireSeed>(seed, hp: 75);
                        var deck = Members(pair.Base, "LongTermDeck").ToArray();
                        var beforeDraws = pair.Base.Random.TotalDraws;
                        Submit(pair.Base, eat ? SapphireSeedEvent.Eat : SapphireSeedEvent.Plant);
                        var decision = pair.Base.PendingDecision ?? throw new CheckFailed("real permanent single selection missing");
                        Require(decision.Minimum == 1 && decision.Maximum == 1 && decision.Candidates.SequenceEqual(deck),
                            "complete actual permanent choice domain");
                        if (eat) _ = new RunDriver(pair.Native, new SingleSelection(pair.Player.Deck.Cards.Select(Card).ToArray(), 1));
                        pair.Model.ChooseOption(pair.Model.CurrentOptions.Single(o => o.Key == (eat ? "EAT" : "PLANT"))).GetAwaiter().GetResult();
                        pair.Base.Answer(new(decision.RequestId, [deck[eat ? 1 : 0]]));
                        Compare(pair);
                        Require(pair.Base.Random.TotalDraws == beforeDraws, "SapphireSeed must not consume RNG");
                        if (!eat)
                        {
                            var native = pair.Player.Deck.Cards[0].Enchantments.Single();
                            Require(native is Sts2Sim.Core.Models.Enchantments.Sown && native.Magnitude == 1
                                && native.Status == EnchantmentStatus.Normal, "native attached Sown and normal status");
                            Require(Get(pair.Base, deck[0], "EnchantmentId").AsString() == "base-spire:sown"
                                && Get(pair.Base, deck[0], "EnchantmentAmount").AsInt() == 1
                                && !Get(pair.Base, deck[0], "EnchantmentDisabled").AsBool(), "base attachment agrees with native");
                        }
                        evidence.Add(new { seed, scenario = eat ? "sapphire-eat-selected-duplicate" : "sapphire-plant-first-compatible", passed = true });
                    }
                    var selected = New<SapphireSeed>(seed);
                    var originals = Members(selected.Base, "LongTermDeck").ToArray();
                    Submit(selected.Base, SapphireSeedEvent.Plant);
                    selected.Base.Answer(new(selected.Base.PendingDecision!.RequestId, [originals[2]]));
                    selected.Model.ChooseOption(selected.Model.CurrentOptions.Single(o => o.Key == "PLANT")).GetAwaiter().GetResult();
                    var actualIndex = Array.FindIndex(originals, card => Get(selected.Base, card, "EnchantmentId").AsString() != "");
                    var nativeIndex = selected.Player.Deck.Cards.ToList().FindIndex(card => card.Enchantments.Count != 0);
                    Require(actualIndex == 2 && nativeIndex == 0, "known native auto-first selection gap must remain visible");
                    gaps.Add(new { seed, scenario = "sapphire-plant-nonfirst", baseSelectedIndex = actualIndex, nativeSelectedIndex = nativeIndex,
                        strictNativeComparison = "known-reference-divergence",
                        reason = "native SapphireSeed.Plant explicitly auto-selects first eligible; source allows actual player choice; reference unchanged" });
                }
            }

            Pair New<T>(string seed, int hp = 80, int gold = 99, int count = 10) where T : EventModel
            {
                var native = new RunState(seed, new Overgrowth());
                var player = Player.CreateForNewRun(ModelDb.Character<Sts2Sim.Core.Models.Characters.Ironclad>(), native);
                native.AddPlayer(player); player.Gold = gold; player.Creature.SetCurrentHpInternal(hp);
                foreach (var card in player.Deck.Cards.Skip(count).ToArray()) player.Deck.RemoveInternal(card);
                var model = (EventModel)ModelDb.Get(typeof(T)).MutableClone(); model.AssignOwner(player); model.BeginEvent(native);
                var s = harness.Create(seed, hp: hp, gold: gold, deckCount: count); Enter(s, typeof(T).Name);
                Require(Members(s, "MapNodes").Count == 0, "isolated event check cannot depend on a generated map");
                return new(s, native, player, model);
            }
        }
        catch (Exception e) { error = e.ToString(); }
        var exit = error is not null || gaps.Count != 0 ? 1 : 0;
        File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new
        {
            exit_code = exit, only, controlledEventVisits = true, normalVictoryEvidence = false, contentDigest,
            error, evidence, known_reference_divergences = gaps,
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Event modules: {evidence.Count} passed, {gaps.Count} known reference RNG divergences, error={error ?? "none"}.");
        return exit;
    }

    private static void Act(Pair pair, string action, string option)
    {
        Submit(pair.Base, action);
        pair.Model.ChooseOption(pair.Model.CurrentOptions.Single(o => o.Key == option)).GetAwaiter().GetResult();
    }
    private static void Compare(Pair pair, bool compareEventRandom = true)
    {
        Require(Number(pair.Base, "Hp") == pair.Player.Creature.CurrentHp && Number(pair.Base, "MaxHp") == pair.Player.Creature.MaxHp
            && Number(pair.Base, "Gold") == pair.Player.Gold, "event resources differ from native execution");
        Require(Members(pair.Base, "LongTermDeck").Select(card => Card(pair.Base, card)).SequenceEqual(pair.Player.Deck.Cards.Select(Card)),
            "ordered permanent definitions/upgrades differ from native execution");
        Require(Get(pair.Base, Owner(pair.Base), "CurrentEventId").AsString() == "" && pair.Model.IsFinished, "source event must finish and close");
        if (compareEventRandom) MatchRandom(pair.Base, pair.Model.Rng, pair.Model.GetType().Name);
    }
    private static void MatchRandom(Session s, Sts2Sim.Core.Random.Rng rng, string name)
    {
        Require(EventDraws(s, name) == rng.Counter, name + ": event draw count differs");
        if (rng.Counter == 0) return;
        var actual = EngineSnapshot.Streams(s).Single(r => r.Stream == EventRandomAsset.StreamName(name));
        var state = rng.ToSerializable();
        Require(actual.Fingerprint == RngSourceCompat.FingerprintText(state.state0, state.state1, state.state2, state.state3), name + ": event RNG state differs");
    }
    private static long EventDraws(Session s, string name) => s.Random.DrawCount(EventRandomAsset.StreamName(name));
    private static InstanceId Owner(Session s) => Members(s, "RunStates").Single();
    private static long Number(Session s, string name) => Get(s, Owner(s), name).AsInt();
    private static string Card(CardModel card) => ReferenceBackend.Project(card.Id) + (card.IsUpgraded ? "+" : "");
    private static string Card(Session s, InstanceId card) => Get(s, card, "DefinitionId").AsString() + (Get(s, card, "Upgraded").AsBool() ? "+" : "");
    private static void Require(bool condition, string message) { if (!condition) throw new CheckFailed(message); }
    private sealed record Pair(Session Base, RunState Native, Player Player, EventModel Model);
    private sealed class SingleSelection(string[] expected, int index) : IRunDecisionSource
    {
        public Task<MapPoint> ChooseMapPointAsync(IReadOnlyList<MapPoint> options) => throw new CheckFailed("unexpected map choice");
        public Task<CombatDecision> ChooseCombatActionAsync(CombatState state) => throw new CheckFailed("unexpected combat choice");
        public Task<IReadOnlyList<CardModel>> ChooseCardsAsync(CardSelectionRequest request)
        {
            Require(request.MinCount == 1 && request.MaxCount == 1 && request.Candidates.Select(Card).SequenceEqual(expected),
                "native permanent single-selection domain differs");
            return Task.FromResult<IReadOnlyList<CardModel>>([request.Candidates[index]]);
        }
    }
    private sealed class Selection(string[] expected, int[] indexes) : IRunDecisionSource
    {
        public Task<MapPoint> ChooseMapPointAsync(IReadOnlyList<MapPoint> options) => throw new CheckFailed("unexpected map decision");
        public Task<CombatDecision> ChooseCombatActionAsync(CombatState state) => throw new CheckFailed("unexpected combat decision");
        public Task<IReadOnlyList<CardModel>> ChooseCardsAsync(CardSelectionRequest request)
        {
            Require(request.MinCount == 2 && request.MaxCount == 2 && request.Candidates.Select(Card).SequenceEqual(expected),
                "native simultaneous selection candidates, order or bounds differ");
            return Task.FromResult<IReadOnlyList<CardModel>>(indexes.Select(i => request.Candidates[i]).ToArray());
        }
    }
}
#endif
