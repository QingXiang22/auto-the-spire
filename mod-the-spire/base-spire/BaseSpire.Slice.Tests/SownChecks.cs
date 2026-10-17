using BaseSpire.SinglePlayer;
using BaseSpire.Slice;
using BaseSpire.Slice.Rooms;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>Source Sown/SapphireSeed witnesses; controlled initial visits and combat arrangements, not normal victories.</summary>
internal static class SownChecks
{
    private static readonly Lazy<BaseSpireRuntimeFactory> Factory = new(() => new(RunConfiguration.Ironclad(0, BaseSpireScope.DevRun)));
    public static void Run()
    {
        Check.Run("SapphireSeed: heal precedes upgrade choice; duplicate identity preserved, replay rejected", () =>
        {
            var s = Event(hp: 76); var deck = Members(s, "LongTermDeck").ToArray(); var draws = s.Random.TotalDraws;
            Submit(s, SapphireSeedEvent.Eat);
            Check.Equal(80L, Number(s, Owner(s), "Hp"), "healing occurs before the decision and caps");
            Check.Equal(SessionStatus.AwaitingDecision, s.Status, "real upgrade choice");
            Check.Sequence(deck, s.PendingDecision!.Candidates, "permanent order");
            s.Answer(new(s.PendingDecision.RequestId, [deck[1]]));
            Check.True(Get(s, deck[1], "Upgraded").AsBool(), "selected duplicate upgraded");
            Check.False(Get(s, deck[0], "Upgraded").AsBool(), "unselected duplicate unchanged");
            Check.Sequence(deck, Members(s, "LongTermDeck"), "same permanent instances");
            Closed(s); Check.Equal(draws, s.Random.TotalDraws, "no reward or event RNG");
            Check.Fault(EngineFaultCode.IllegalAction, () => Submit(s, SapphireSeedEvent.Eat), "closed choice cannot heal again");
        });
        Check.Run("SapphireSeed: Plant selects non-first real instance, excludes unplayable and already enchanted cards", () =>
        {
            var s = Event(); var deck = Members(s, "LongTermDeck").ToArray();
            s.State.RunAtomic(() => Set(s, deck[0], "Unplayable", Value.Of(true)));
            var draws = s.Random.TotalDraws; Submit(s, SapphireSeedEvent.Plant);
            Check.Sequence(deck.Skip(1), s.PendingDecision!.Candidates, "unplayable deck card excluded");
            s.Answer(new(s.PendingDecision.RequestId, [deck[2]])); Closed(s);
            Check.Equal("base-spire:sown", Get(s, deck[2], "EnchantmentId").AsString(), "selected actual instance enchanted");
            Check.Equal(1L, Number(s, deck[2], "EnchantmentAmount"), "source magnitude");
            Check.False(Get(s, deck[2], "EnchantmentDisabled").AsBool(), "initial normal status");
            Check.True(deck.Where(c => c != deck[2]).All(c => Get(s, c, "EnchantmentId").AsString() == ""), "no other duplicate polluted");
            Check.Equal(draws, s.Random.TotalDraws, "Plant does not sample");
            s.State.RunAtomic(() => Set(s, Owner(s), "CurrentEventId", Value.Of(SapphireSeedEvent.SourceName)));
            s.State.RunAtomic(() => Set(s, Owner(s), "MapPhase", Value.Of(8L)));
            Submit(s, SapphireSeedEvent.Plant);
            Check.False(s.PendingDecision!.Candidates.Contains(deck[2]), "Sown cannot stack");
        });
        Check.Run("SapphireSeed: empty and singleton upgrade/enchant domains follow source automatic selection", () =>
        {
            foreach (var action in new[] { SapphireSeedEvent.Plant, SapphireSeedEvent.Eat })
                foreach (var count in new[] { 0, 1 })
                {
                    var s = Event();
                    s.State.RunAtomic(() => { foreach (var card in Members(s, "LongTermDeck").Skip(count).ToArray()) s.State.DestroyInstance(card); });
                    var before = s.Random.TotalDraws; Submit(s, action); Closed(s);
                    Check.Equal(before, s.Random.TotalDraws, "automatic selection does not draw");
                    Check.Equal(count, Members(s, "LongTermDeck").Count, "no invented candidate");
                    if (count == 1) Check.True(action == SapphireSeedEvent.Plant
                        ? Get(s, Members(s, "LongTermDeck")[0], "EnchantmentId").AsString() == "base-spire:sown"
                        : Get(s, Members(s, "LongTermDeck")[0], "Upgraded").AsBool(), "unique candidate actually changed");
                }
        });
        Check.Run("Sown: card pays before benefit, once per copy, even blocked gain consumes attachment", () =>
        {
            foreach (var blocked in new[] { false, true })
            {
                var s = FormalSliceFixture.NewFormal(); var card = FormalSliceFixture.MoveToHand(s, "base-spire:defend-ironclad");
                Enchant(s, card); var owner = FormalSliceFixture.Player(s);
                if (blocked) FormalSliceFixture.GivePower(s, owner, "base-spire:no-energy-gain", 1);
                var before = Number(s, owner, "Energy");
                FormalSliceFixture.PlayAt(s, card, owner);
                Check.Equal(before - (blocked ? 1 : 0), Number(s, owner, "Energy"), "pay one then gain only when allowed");
                Check.True(Get(s, card, "EnchantmentDisabled").AsBool(), "consumed regardless of blocked gain");
                MoveToHand(s, card); FormalSliceFixture.PlayAt(s, card, owner);
                Check.Equal(before - (blocked ? 2 : 1), Number(s, owner, "Energy"), "same copy never gains twice");
            }
        });
        Check.Run("Sown: Anger clones preserve creation-time attachment status; clones are independent", () =>
        {
            var s = FormalSliceFixture.NewFormal(); var anger = FormalSliceFixture.MoveToHand(s, "base-spire:anger");
            Enchant(s, anger); var enemy = FormalSliceFixture.Cultist(s);
            FormalSliceFixture.SetNumber(s, enemy, FormalSliceFixture.MonsterTypeId, "Hp", 999);
            FormalSliceFixture.PlayAt(s, anger, enemy);
            var firstClone = Members(s, "DiscardPile").Single(c => c != anger && Get(s, c, "DefinitionId").AsString() == "base-spire:anger");
            Check.False(Get(s, firstClone, "EnchantmentDisabled").AsBool(), "body clone happens before original attachment trigger");
            Check.True(Get(s, anger, "EnchantmentDisabled").AsBool(), "original disabled after body");
            MoveToHand(s, anger); var before = Number(s, FormalSliceFixture.Player(s), "Energy");
            FormalSliceFixture.PlayAt(s, anger, enemy);
            var secondClone = Members(s, "DiscardPile").Single(c => c != anger && c != firstClone && Get(s, c, "DefinitionId").AsString() == "base-spire:anger");
            Check.True(Get(s, secondClone, "EnchantmentDisabled").AsBool(), "disabled source copied as disabled, no universal reset");
            MoveToHand(s, secondClone); FormalSliceFixture.PlayAt(s, secondClone, enemy);
            Check.Equal(before, Number(s, FormalSliceFixture.Player(s), "Energy"), "disabled clone provides no gain");
            MoveToHand(s, firstClone); FormalSliceFixture.PlayAt(s, firstClone, enemy);
            Check.Equal(before + 1, Number(s, FormalSliceFixture.Player(s), "Energy"), "normal clone gains once independently");
        });
        Check.Run("Sown: final kill triggers attachment, owner death before trigger suppresses it", () =>
        {
            var s = FormalSliceFixture.NewFormal(); var attack = FormalSliceFixture.MoveToHand(s, "base-spire:strike-ironclad"); Enchant(s, attack);
            s.State.RunAtomic(() => { foreach (var enemy in Members(s, "Monsters")) Set(s, enemy, "Hp", Value.Of(1L)); });
            var target = Members(s, "Monsters")[0];
            s.State.RunAtomic(() => { foreach (var enemy in Members(s, "Monsters").Where(c => c != target).ToArray()) s.State.DestroyInstance(enemy); });
            var energy = Number(s, FormalSliceFixture.Player(s), "Energy"); FormalSliceFixture.PlayAt(s, attack, target);
            Check.True(Get(s, attack, "EnchantmentDisabled").AsBool(), "lethal target does not skip attachment");
            Check.Equal(energy, Number(s, FormalSliceFixture.Player(s), "Energy"), "kill still refunds the cost");
            var dead = FormalSliceFixture.NewFormal(); var offering = FormalSliceFixture.MoveToHand(dead, "base-spire:offering"); Enchant(dead, offering);
            FormalSliceFixture.SetNumber(dead, FormalSliceFixture.Player(dead), FormalSliceFixture.PlayerTypeId, "Hp", 1);
            FormalSliceFixture.PlayAt(dead, offering, FormalSliceFixture.Player(dead));
            Check.Equal(SessionStatus.Terminal, dead.Status, "actual player death");
            Check.False(Get(dead, offering, "EnchantmentDisabled").AsBool(), "dead owner skips Sown trigger");
        });
        Check.Run("Sown: permanent random transform creates an unattached replacement and preserves other instances", () =>
        {
            var s = Event(); var deck = Members(s, "LongTermDeck").ToArray(); var original = deck[1];
            Submit(s, SapphireSeedEvent.Plant); s.Answer(new(s.PendingDecision!.RequestId, [original]));
            s.State.RunAtomic(() => { Set(s, Owner(s), "CurrentEventId", Value.Of("AromaOfChaos")); Set(s, Owner(s), "MapPhase", Value.Of(8L)); });
            Submit(s, AromaOfChaosEvent.LetGoAction); s.Answer(new(s.PendingDecision!.RequestId, [original]));
            var after = Members(s, "LongTermDeck");
            Check.Sequence(deck.Where(c => c != original), after.Take(deck.Length - 1), "unselected instances unchanged");
            Check.False(after.Contains(original), "old attached instance removed");
            Check.Equal("", Get(s, after.Last(), "EnchantmentId").AsString(), "new definition does not inherit attachment");
            Check.Equal(0L, Number(s, after.Last(), "EnchantmentAmount"), "new card has no stale magnitude");
            Check.False(Get(s, after.Last(), "EnchantmentDisabled").AsBool(), "new card has no stale status");
        });
        Check.Run("Sown: planted upgraded permanent instance survives settlement and refreshes only on next battle copy", () =>
        {
            var s = Event(); var deck = Members(s, "LongTermDeck").ToArray(); var chosen = deck[5];
            Submit(s, SapphireSeedEvent.Plant); s.Answer(new(s.PendingDecision!.RequestId, [chosen]));
            s.State.RunAtomic(() => { Set(s, Owner(s), "CurrentEventId", Value.Of(SapphireSeedEvent.SourceName)); Set(s, Owner(s), "MapPhase", Value.Of(8L)); });
            Submit(s, SapphireSeedEvent.Eat); s.Answer(new(s.PendingDecision!.RequestId, [chosen]));
            for (var battle = 0; battle < 2; battle++)
            {
                EnterMonster(s);
                var copy = s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:Card").Id).Single(c =>
                    Get(s, c, "LongTermSource").Kind == ValueKind.Ref && Get(s, c, "LongTermSource").AsRef() == chosen);
                Check.Equal("base-spire:sown", Get(s, copy, "EnchantmentId").AsString(), "attachment carried from permanent instance");
                Check.True(Get(s, copy, "Upgraded").AsBool(), "upgrade preserves attachment");
                Check.False(Get(s, copy, "EnchantmentDisabled").AsBool(), "new battle copy starts from unchanged permanent status");
                MoveToHand(s, copy); FormalSliceFixture.PlayAt(s, copy, FormalSliceFixture.Player(s));
                Check.True(Get(s, copy, "EnchantmentDisabled").AsBool(), "battle-local consumed status");
                FinishCombat(s);
                Check.True(Members(s, "LongTermDeck").Contains(chosen), "same permanent handle remains");
                Check.False(Get(s, chosen, "EnchantmentDisabled").AsBool(), "battle status never written to permanent card");
            }
        });
    }

    private static Session Event(long hp = 80)
    {
        var run = Factory.Value.CreateSession("ENV-T04-RNG-SHUFFLE-001"); run.Start(); var s = run.Session;
        s.State.RunAtomic(() => { Set(s, Owner(s), "CurrentEventId", Value.Of(SapphireSeedEvent.SourceName)); Set(s, Owner(s), "MapPhase", Value.Of(8L)); Set(s, Owner(s), "Hp", Value.Of(hp)); });
        return s;
    }
    private static void EnterMonster(Session s)
    {
        // Controlled map position: this witness tests actual entry/copy/settlement, not normal travel or victory.
        var relation = s.RuleSet.Relations.Single(r => r.QualifiedId == "base-spire:MapChildren");
        var parent = Members(s, "MapNodes").First(p => s.State.RelationTargets(relation.Id, p).Any(c => Get(s, c, "Type").AsEnumOrdinal() == 5));
        var target = s.State.RelationTargets(relation.Id, parent).First(c => Get(s, c, "Type").AsEnumOrdinal() == 5);
        s.State.RunAtomic(() => { Set(s, Owner(s), "CurrentNode", Value.OfRef(parent)); Set(s, Owner(s), "MapFloor", Value.Of(Number(s, parent, "Row") + 1)); Set(s, Owner(s), "MapPhase", Value.Of(2L)); });
        s.Submit(s.RuleSet.Actions.Single(a => a.QualifiedId == "base-spire:ChooseMapPoint"), SliceManifest.Player, [Value.OfRef(target)]);
        Check.True(s.Status != SessionStatus.Faulted, s.Fault?.Message ?? "battle entered");
    }
    private static void FinishCombat(Session s)
    {
        s.State.RunAtomic(() => { Set(s, FormalSliceFixture.Player(s), "Energy", Value.Of(99L)); foreach (var enemy in Members(s, "Monsters")) { Set(s, enemy, "Hp", Value.Of(1L)); Set(s, enemy, "Block", Value.Of(0L)); } });
        foreach (var card in s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:Card").Id).Where(c => Get(s, c, "LongTermSource").Kind == ValueKind.Ref && Get(s, c, "DefinitionId").AsString() == "base-spire:strike-ironclad").ToArray())
        {
            if (Members(s, "Monsters").Count == 0) break;
            MoveToHand(s, card); FormalSliceFixture.PlayAt(s, card, Members(s, "Monsters")[0]);
        }
        Check.Equal(0, Members(s, "Monsters").Count, "controlled actual kills complete combat");
        for (var i = 0; i < 8 && Number(s, Owner(s), "MapPhase") == 3; i++)
        {
            var names = s.LegalActions(SliceManifest.Player).Select(a => a.Definition.QualifiedId).ToArray();
            var name = new[] { "SkipRewardCard", "SkipRewardPotion", "SkipRewardRelic", "TakeRewardGold" }.First(n => names.Contains("base-spire:" + n)); Submit(s, name);
        }
        Check.Equal(2L, Number(s, Owner(s), "MapPhase"), "rewards actually closed");
    }
    private static void Enchant(Session s, InstanceId card) => s.State.RunAtomic(() => { Set(s, card, "EnchantmentId", Value.Of("base-spire:sown")); Set(s, card, "EnchantmentAmount", Value.Of(1L)); });
    private static void MoveToHand(Session s, InstanceId card) => s.State.RunAtomic(() =>
    { foreach (var name in new[] { "DrawPile", "DiscardPile", "ExhaustPile", "InPlay" }) { var collection = s.RuleSet.Collections.Single(c => c.QualifiedId == "base-spire:" + name); if (s.State.IsMember(collection.Id, card)) s.State.RemoveFromCollection(collection.Id, card); } var hand = s.RuleSet.Collections.Single(c => c.QualifiedId == "base-spire:Hand"); if (!s.State.IsMember(hand.Id, card)) s.State.AddToCollection(hand.Id, card); });
    private static void Closed(Session s) { Check.Equal(2L, Number(s, Owner(s), "MapPhase"), "returned to travel"); Check.Equal("", Get(s, Owner(s), "CurrentEventId").AsString(), "visit cleared"); }
    private static void Submit(Session s, string name) => s.Submit(s.RuleSet.Actions.Single(a => a.QualifiedId == "base-spire:" + name), SliceManifest.Player, []);
    private static InstanceId Owner(Session s) => Members(s, "RunStates").Single();
    private static IReadOnlyList<InstanceId> Members(Session s, string name) => s.State.CollectionMembers(s.RuleSet.Collections.Single(c => c.QualifiedId == "base-spire:" + name).Id);
    private static Value Get(Session s, InstanceId id, string name) => s.State.GetField(id, s.RuleSet.Type(s.State.TypeIdOf(id)).FieldIndex(name));
    private static long Number(Session s, InstanceId id, string name) => Get(s, id, name).AsInt();
    private static void Set(Session s, InstanceId id, string name, Value value) => s.State.SetField(id, s.RuleSet.Type(s.State.TypeIdOf(id)).FieldIndex(name), value);
}
