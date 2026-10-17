using BaseSpire.SinglePlayer;
using BaseSpire.Slice;
using BaseSpire.Slice.Rooms;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>Controlled event initial states distinguish persistent instance, selection and random semantics.</summary>
internal static class AromaOfChaosChecks
{
    public static void Run()
    {
        Check.Run("AromaOfChaos upgrade: selected duplicate alone changes; visit resumes after decision and rejects replay", () =>
        {
            var s = New(); var cards = Deck(s).Take(2).ToArray(); Keep(s, cards);
            var draws = s.Random.TotalDraws;
            var action = Submit(s, AromaOfChaosEvent.MaintainControlAction);
            Check.Equal(SessionStatus.AwaitingDecision, s.Status, "two distinct instances require selection");
            Check.Sequence(cards, s.PendingDecision!.Candidates, "persistent deck order");
            Check.Equal(8L, RunNumber(s, "MapPhase"), "visit remains active until selected effect completes");
            s.Answer(new DecisionAnswer(s.PendingDecision.RequestId, [cards[1]]));
            Check.Sequence(cards, Deck(s), "upgrade preserves both instances and order");
            Check.False(Field(s, cards[0], "Upgraded").AsBool(), "same-definition other instance unchanged");
            Check.True(Field(s, cards[1], "Upgraded").AsBool(), "chosen instance upgraded");
            Check.Equal(draws, s.Random.TotalDraws, "upgrade uses no random draw");
            Closed(s); var hash = s.State.StateHash();
            Check.Fault(EngineFaultCode.IllegalAction, () => s.Submit(action, SliceManifest.Player, []), "consumed option rejected");
            Check.Equal(hash, s.State.StateHash(), "rejected option cannot upgrade another instance");
        });
        Check.Run("AromaOfChaos transform: one event draw after selection, new ordinary instance appended, other duplicate survives", () =>
        {
            var s = New(); var cards = Deck(s).Take(2).ToArray(); Keep(s, cards);
            var draws = s.Random.TotalDraws;
            Submit(s, AromaOfChaosEvent.LetGoAction);
            Check.Equal(SessionStatus.AwaitingDecision, s.Status, "selection precedes transform RNG");
            Check.Equal(draws, s.Random.TotalDraws, "query and pause do not sample a replacement");
            s.Answer(new DecisionAnswer(s.PendingDecision!.RequestId, [cards[0]]));
            var after = Deck(s);
            Check.Equal(2, after.Count, "same permanent deck count");
            Check.Equal(cards[1], after[0], "other same-definition instance survives in original order");
            Check.False(after.Contains(cards[0]), "old instance leaves deck");
            Check.False(s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:Card").Id).Contains(cards[0]), "old entity destroyed");
            Check.False(cards.Contains(after[1]), "new instance appended");
            Check.False(Field(s, after[1], "Upgraded").AsBool(), "replacement starts ordinary");
            Check.Equal(0L, Field(s, after[1], "CostModifier").AsInt(), "no temporary cost inherited");
            Check.True(Field(s, after[1], "DefinitionId").AsString() != "base-spire:strike-ironclad", "source excludes original definition");
            Check.Equal(draws + 1, s.Random.TotalDraws, "exactly one event RNG draw");
            Closed(s);
        });
        Check.Run("AromaOfChaos selectors: empty domain closes without RNG, one eligible card executes without a decision", () =>
        {
            foreach (var action in new[] { AromaOfChaosEvent.MaintainControlAction, AromaOfChaosEvent.LetGoAction })
            {
                var empty = New(); Keep(empty, []); var draws = empty.Random.TotalDraws;
                Submit(empty, action); Closed(empty);
                Check.Equal(draws, empty.Random.TotalDraws, "empty domain does not draw or ask");
                var unique = New(); var card = Deck(unique).First(); Keep(unique, [card]);
                Submit(unique, action); Closed(unique);
                Check.Equal(1, Deck(unique).Count, "unique selection keeps deck count");
            }
        });
    }

    private static Session New()
    {
        var run = new BaseSpireRuntimeFactory(RunConfiguration.Ironclad(0, BaseSpireScope.DevMap))
            .CreateSession("ENV-T04-RNG-SHUFFLE-001"); run.Start();
        var s = run.Session; var entity = Members(s, "RunStates").Single();
        var type = s.RuleSet.TypeByQualifiedId("base-spire:Run");
        s.State.RunAtomic(() =>
        {
            s.State.SetField(entity, type.FieldIndex("CurrentEventId"), Value.Of(AromaOfChaosEvent.SourceName));
            s.State.SetField(entity, type.FieldIndex("MapPhase"), Value.Of(8L));
        });
        return s;
    }
    private static void Keep(Session s, IReadOnlyList<InstanceId> cards) => s.State.RunAtomic(() =>
    { foreach (var card in Deck(s).Where(card => !cards.Contains(card)).ToArray()) s.State.DestroyInstance(card); });
    private static ActionDefinition Submit(Session s, string name)
    {
        var action = s.LegalActions(SliceManifest.Player).Single(a => a.Definition.QualifiedId == "base-spire:" + name).Definition;
        s.Submit(action, SliceManifest.Player, []); return action;
    }
    private static void Closed(Session s)
    {
        Check.Equal(SessionStatus.Running, s.Status, "event completes without pending continuation");
        Check.Equal(2L, RunNumber(s, "MapPhase"), "travel restored");
        var run = Members(s, "RunStates").Single();
        Check.Equal("", s.State.GetField(run, s.RuleSet.TypeByQualifiedId("base-spire:Run").FieldIndex("CurrentEventId")).AsString(), "event identity cleared");
    }
    private static IReadOnlyList<InstanceId> Deck(Session s) => Members(s, "LongTermDeck");
    private static IReadOnlyList<InstanceId> Members(Session s, string name) => s.State.CollectionMembers(s.RuleSet.Collections.Single(c => c.QualifiedId == "base-spire:" + name).Id);
    private static Value Field(Session s, InstanceId card, string name) => s.State.GetField(card, s.RuleSet.TypeByQualifiedId("base-spire:Card").FieldIndex(name));
    private static long RunNumber(Session s, string name) => s.State.GetField(Members(s, "RunStates").Single(), s.RuleSet.TypeByQualifiedId("base-spire:Run").FieldIndex(name)).AsInt();
}
