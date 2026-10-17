#if REFERENCE_SIM
using System.Text.Json;
using BaseSpire.Slice;
using CoreTheSpire.Engine;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Demo;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Enchantments;
using Sts2Sim.Core.Runs;

namespace Differential;

/// <summary>Controlled actual combat execution on both sides; no normal-run or initialization RNG parity claim.</summary>
internal static class SownCombatChecks
{
    public static int Run(string output)
    {
        Directory.CreateDirectory(output);
        var evidence = new List<object>();
        try
        {
            CombatReplayDemo.EnsureModelsRegistered();
            var rules = FormalSpirePackage.BuildRuleSet(out _);
            foreach (var upgraded in new[] { false, true })
            {
                var s = new Session(rules, 20261017UL, [SliceManifest.PlayerRole], computeBackend: SourceWeightedPickBackend.Instance); s.Start();
                var player = s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:Player").Id).Single();
                var card = AllCards(s).First(c => Get(s, c, "DefinitionId").AsString() == "base-spire:defend-ironclad");
                s.State.RunAtomic(() =>
                {
                    Set(s, card, "EnchantmentId", Value.Of("base-spire:sown")); Set(s, card, "EnchantmentAmount", Value.Of(1L));
                    if (upgraded) { Set(s, card, "Upgraded", Value.Of(true)); Set(s, card, "Amount", Value.Of(8L)); }
                });
                var run = new RunState("SOWN-COMBAT", new Overgrowth());
                var nativePlayer = Player.CreateForNewRun(ModelDb.Character<Sts2Sim.Core.Models.Characters.Ironclad>(), run); run.AddPlayer(nativePlayer);
                var combat = new CombatState(run);
                combat.AddMonster((MonsterModel)ModelDb.Get(typeof(Sts2Sim.Core.Models.Monsters.CalcifiedCultist)).MutableClone(), CombatSide.Enemy);
                var engine = new CombatEngine(combat); engine.StartCombatAsync().GetAwaiter().GetResult();
                var nativeState = nativePlayer.PlayerCombatState!;
                var nativeCard = nativeState.AllPiles.SelectMany(p => p.Cards).First(c => c is Sts2Sim.Core.Models.Cards.DefendIronclad);
                if (upgraded) CardCmd.Upgrade(nativeCard);
                CardCmd.Enchant<Sown>(nativeCard, 1m).GetAwaiter().GetResult();
                for (var play = 0; play < 2; play++)
                {
                    MoveToHand(s, card); nativeState.Hand.AddInternal(nativeCard);
                    var action = s.RuleSet.Actions.Single(a => a.QualifiedId == "base-spire:PlayCard");
                    s.Submit(action, SliceManifest.Player, [Value.OfRef(card)]);
                    while (s.Status == SessionStatus.AwaitingDecision) s.Answer(new(s.PendingDecision!.RequestId, [s.PendingDecision.Candidates[0]]));
                    engine.PlayCardAsync(nativePlayer, nativeCard, nativePlayer.Creature).GetAwaiter().GetResult();
                    Require(Get(s, player, "Energy").AsInt() == nativeState.Energy, "actual native energy differs");
                    Require(Get(s, player, "Block").AsInt() == nativePlayer.Creature.Block, "actual native block differs");
                    Require(Get(s, card, "EnchantmentDisabled").AsBool() == (nativeCard.Enchantments.Single().Status == EnchantmentStatus.Disabled), "attachment state differs");
                    evidence.Add(new { upgraded, play = play + 1, energy = nativeState.Energy, block = nativePlayer.Creature.Block, disabled = true, passed = true });
                }
                var nativeClone = nativeCard.CreateClone();
                Require(nativeClone.Enchantments.Single().Status == EnchantmentStatus.Disabled, "native CreateClone restores disabled attachment status");
                // Native generic MutableClone differs from CreateClone. Record this diagnostic rather than patching the oracle.
                var genericClone = (CardModel)nativeCard.MutableClone();
                evidence.Add(new { upgraded, scenario = "native-copy-paths", createCloneDisabled = nativeClone.Enchantments.Single().Status == EnchantmentStatus.Disabled,
                    mutableCloneDisabled = genericClone.Enchantments.Single().Status == EnchantmentStatus.Disabled,
                    note = "Source DeepCloneFields preserves Sown status; native generic MutableClone resets it, but CreateClone restores it" });
            }
            Write(0, null); Console.WriteLine("Sown actual combat: 4 native/base play boundaries matched; native clone paths recorded separately."); return 0;
        }
        catch (Exception e) { Write(1, e.ToString()); Console.Error.WriteLine(e); return 1; }
        void Write(int code, string? error) => File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new
        { exit_code = code, controlledCombat = true, normalVictoryEvidence = false, error, evidence }, new JsonSerializerOptions { WriteIndented = true }));
    }
    private static IEnumerable<InstanceId> AllCards(Session s) => new[] { "Hand", "DrawPile", "DiscardPile", "ExhaustPile" }.SelectMany(name => Members(s, name));
    private static void MoveToHand(Session s, InstanceId card) => s.State.RunAtomic(() =>
    { foreach (var name in new[] { "DrawPile", "DiscardPile", "ExhaustPile" }) { var pile = s.RuleSet.Collections.Single(c => c.QualifiedId == "base-spire:" + name); if (s.State.IsMember(pile.Id, card)) s.State.RemoveFromCollection(pile.Id, card); }
      var hand = s.RuleSet.Collections.Single(c => c.QualifiedId == "base-spire:Hand"); if (!s.State.IsMember(hand.Id, card)) s.State.AddToCollection(hand.Id, card); });
    private static IReadOnlyList<InstanceId> Members(Session s, string name) => s.State.CollectionMembers(s.RuleSet.Collections.Single(c => c.QualifiedId == "base-spire:" + name).Id);
    private static Value Get(Session s, InstanceId id, string name) => s.State.GetField(id, s.RuleSet.Type(s.State.TypeIdOf(id)).FieldIndex(name));
    private static void Set(Session s, InstanceId id, string name, Value value) => s.State.SetField(id, s.RuleSet.Type(s.State.TypeIdOf(id)).FieldIndex(name), value);
    private static void Require(bool condition, string message) { if (!condition) throw new CheckFailed(message); }
}
#endif
