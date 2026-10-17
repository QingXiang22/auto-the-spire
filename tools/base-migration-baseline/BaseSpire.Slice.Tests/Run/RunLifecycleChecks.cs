using BaseSpire.SinglePlayer;
using BaseSpire.Slice;
using BaseSpire.Slice.Domain.Run;
using BaseSpire.Slice.Orchestration;
using BaseSpire.Slice.Rooms;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

internal static class RunLifecycleChecks
{
    public static void Run()
    {
        Check.Run("Run lifecycle: enemy block expires on its own turn, Barricade preserves it", () =>
        {
            var session = FormalSliceFixture.NewFormal();
            var front = FormalSliceFixture.Cultist(session);
            var back = FormalSliceFixture.Cultist(session, SliceManifest.DampCultistId);
            foreach (var enemy in new[] { front, back })
                FormalSliceFixture.SetNumber(session, enemy, FormalSliceFixture.MonsterTypeId, "Block", 17);
            FormalSliceFixture.GivePower(session, back, "base-spire:barricade", 1);
            FormalSliceFixture.EndTurn(session);
            Check.Equal(0L, FormalSliceFixture.Number(session, front, FormalSliceFixture.MonsterTypeId, "Block"), "ordinary enemy turn clears old block");
            Check.Equal(17L, FormalSliceFixture.Number(session, back, FormalSliceFixture.MonsterTypeId, "Block"), "same prevention rule as player block");
        });
        Check.Run("Run event: SlipperyBridge previews nonbasic, pays/rerolls then removes exactly its preview", () =>
        {
            var fixture = Bridge(["base-spire:strike-ironclad", "base-spire:defend-ironclad", "base-spire:inflame"]);
            var s = fixture.Session; var first = fixture.Preview();
            Check.Equal("base-spire:inflame", fixture.Definition(first), "initial pool excludes basics while a nonbasic exists");
            Check.Equal(1L, s.Random.TotalDraws, "even singleton preview consumes one source NextItem");
            fixture.Submit(SlipperyBridgeEvent.HoldOn);
            Check.Equal(77L, fixture.Number("Hp"), "first hold costs three HP");
            Check.True(fixture.Preview() != first && fixture.Deck().Contains(first), "reroll excludes prior type without removing it");
            Check.Equal(8L, fixture.Number("MapPhase"), "hold retains event visit");
            var removed = fixture.Preview(); var others = fixture.Deck().Where(c => c != removed).ToArray();
            fixture.Submit(SlipperyBridgeEvent.Overcome);
            Check.Sequence(others, fixture.Deck(), "only the announced actual instance leaves the permanent deck");
            Check.Equal(2L, fixture.Number("MapPhase"), "overcome closes to travel");
            Check.Equal(2L, s.Random.TotalDraws, "removal never rerolls");
            Check.Equal(0, fixture.Members("SlipperyBridgeSkippedCards").Count, "event filtering history cleared");
        });
        Check.Run("Run event: all-basic repeated definitions use fallback and keep skipped identities unique", () =>
        {
            var fixture = Bridge(["base-spire:strike-ironclad", "base-spire:strike-ironclad"]);
            for (var i = 0; i < 5; i++)
            {
                fixture.Submit(SlipperyBridgeEvent.HoldOn);
                Check.True(fixture.Deck().Contains(fixture.Preview()), "fallback still previews a held instance");
            }
            Check.Equal(55L, fixture.Number("Hp"), "3+4+5+6+7 paid in order");
            Check.Equal(6L, fixture.Session.Random.TotalDraws, "one initial and one per reroll");
            Check.True(fixture.Members("SlipperyBridgeSkippedCards").Count <= 2, "HashSet semantics under repeated fallback");
        });
        Check.Run("Run event: lethal hold completes its effect then terminates without travel", () =>
        {
            var fixture = Bridge(["base-spire:strike-ironclad"], hp: 2);
            fixture.Submit(SlipperyBridgeEvent.HoldOn);
            Check.Equal(0L, fixture.Number("Hp"), "outside-combat damage floors at zero");
            Check.Equal(2L, fixture.Session.Random.TotalDraws, "source reroll occurs after the lethal damage operation");
            Check.Equal(SessionStatus.Terminal, fixture.Session.Status, "actual defeat, no playable travel frontier");
            Check.Equal(0, fixture.Session.LegalActions(SliceManifest.Player).Count, "terminal offers no actions");
        });
        Check.Run("Run eligibility: SlipperyBridge requires TotalFloor above six, counted from visited rooms", () =>
        {
            foreach (var floor in new[] { 6, 7 })
                Check.Equal(floor > 6, Bridge(["base-spire:strike-ironclad"], floor: floor).Allowed(), "source threshold at " + floor);
        });
    }

    private sealed record BridgeFixture(Session Session)
    {
        public IReadOnlyList<InstanceId> Members(string name) => Session.State.CollectionMembers(Session.RuleSet.Collections.Single(c => c.QualifiedId == "base-spire:" + name).Id);
        private InstanceId Run => Members("RunStates").Single();
        private Value Read(string name) => Session.State.GetField(Run, Session.RuleSet.TypeByQualifiedId("base-spire:Run").FieldIndex(name));
        public long Number(string name) => Read(name).AsInt();
        public bool Allowed() => Read("EligibilityAllowed").AsBool();
        public InstanceId Preview() => Read("SlipperyBridgeCard").AsRef();
        public IReadOnlyList<InstanceId> Deck() => Members("LongTermDeck");
        public string Definition(InstanceId card) => FormalSliceFixture.Definition(Session, card);
        public void Submit(string name)
        {
            var action = Session.LegalActions(SliceManifest.Player).Single(a => a.Definition.QualifiedId == "base-spire:" + name);
            Session.Submit(action.Definition, SliceManifest.Player, []);
        }
    }

    private static BridgeFixture Bridge(IReadOnlyList<string> deckIds, long hp = 80, int floor = 7)
    {
        var p = new EnginePackageBuilder("base-spire", "run-event-witness");
        var area = p.ScopeType("Area"); var run = p.EntityType("Run"); var runs = p.Collection("RunStates", run);
        var health = run.AddField("Hp", FieldTypeSchema.Int(0, 100));
        var maxHp = run.AddField("MaxHp", FieldTypeSchema.Int(0, 100)); var gold = run.AddField("Gold", FieldTypeSchema.Int(0, 9999));
        var kinds = p.Enum("CardKind"); foreach (var name in new[] { "Attack", "Skill", "Power", "Status", "Curse" }) kinds.Member(name);
        var schema = CardSchema.Declare(p, kinds); var cards = p.Collection("LongTermDeck", schema.Type);
        var definitions = new CardDefinitions(schema, kinds, SinglePlayerContentSource.Shared.Model, new Dictionary<string, FieldSymbol>());
        var view = p.Projection("View"); view.ParticipantRole = SliceManifest.PlayerRole; view.Type(run);
        var map = MapRoomWiring.Declare(p, view, run); var room = new RoomCompileContext(run, runs, health, maxHp, gold, cards, map);
        var current = run.AddField("CurrentEventId", FieldTypeSchema.String(64));
        var allowed = run.AddField("EligibilityAllowed", FieldTypeSchema.Bool());
        var defeat = p.TerminalResult("Defeat"); var turn = defeat.AddField("Turn", FieldTypeSchema.Int(0, 9999));
        var context = new EventCompileContext(room, current, 8) { Deck = new RunDeck(definitions, cards), Resources = new RunResources(room, defeat, turn) };
        var content = new SlipperyBridgeEvent(p); content.RegisterActions(p, context);
        var setup = p.Procedure("Setup"); var scope = setup.Local("scope", FieldTypeSchema.Binding());
        var owner = setup.Local("run", FieldTypeSchema.RefTo(run)); var card = setup.Local("card", FieldTypeSchema.Binding());
        var steps = new List<Inst> { setup.CreateScope(area, scope), setup.CreateEntity(run, setup.LocalRef(scope), owner,
            [(health, setup.Const(hp)), (maxHp, setup.Const(80L)), (gold, setup.Const(0L)),
             (current, setup.Const(content.Id)), (map.RunMapPhase, setup.Const(8L))], runs) };
        foreach (var id in deckIds) steps.Add(setup.CreateEntity(schema.Type, setup.LocalRef(scope), card,
            definitions.New(setup, definitions.Definition(id), false), cards));
        var node = setup.Local("node", FieldTypeSchema.Binding());
        steps.Add(setup.Repeat(setup.Const((long)floor), setup.CreateEntity(map.Node, setup.LocalRef(scope), node,
            [(map.NodeVisited, setup.Const(true))], map.Nodes)));
        var eligibility = setup.Local("eligibility", FieldTypeSchema.Bool());
        steps.AddRange(SourceEventEligibility.Evaluate(setup, context, setup.LocalRef(owner), SlipperyBridgeEvent.SourceName, eligibility));
        steps.Add(setup.SetField(setup.LocalRef(owner), allowed, setup.LocalRef(eligibility)));
        steps.AddRange(content.Enter(setup, context, setup.LocalRef(owner))); setup.Body(steps.ToArray()); p.OnSessionStart(setup.Symbol);
        var builder = new RuleSetBuilder(); builder.Add(p.Build());
        var session = new Session(builder.Build(), 20261017UL, [SliceManifest.PlayerRole], computeBackend: BaseSpireComputeBackend.Instance);
        session.Start(); return new(session);
    }
}
