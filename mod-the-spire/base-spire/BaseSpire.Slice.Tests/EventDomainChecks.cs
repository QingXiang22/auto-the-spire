using BaseSpire.Slice.Domain.Run;
using BaseSpire.Slice.Rooms;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>Executable witnesses for event-domain/spec.md; controlled initial state, not normal Run evidence.</summary>
internal static class EventDomainChecks
{
    public static void Run()
    {
        Check.Run("RUN-GOLD-01: source hook order, single evaluation and distinct command snapshots", () =>
        {
            var fixture = Build((_, _) => new ProbeEvent(context =>
                [new("Gold", effects => effects.GainGold(effects.Read(context.Room.Gold)))]),
                modify: (scope, _, amount) => scope.Multiply(amount, scope.Const(2L)));
            fixture.Submit("Gold");
            Check.Equal(30L, fixture.Read("Gold").AsInt(), "read 10 once, modify to 20 and add once");
            Check.Equal(20L, fixture.Read("EffectiveAmount").AsInt(), "after hook sees snapshot, not changed Gold");
            Check.Equal(10L, fixture.Read("BeforeGold").AsInt(), "AfterModifying precedes state mutation");
            Check.Equal(30L, fixture.Read("AfterGold").AsInt(), "AfterGained follows state mutation");
            Check.Equal(12L, fixture.Read("HookOrder").AsInt(), "source gold phases run in order");

            var repeated = Build((_, _) => new ProbeEvent(_ =>
                [new("Twice", effects => [.. effects.GainGold(1), .. effects.GainGold(2)])]));
            repeated.Submit("Twice");
            Check.Equal(13L, repeated.Read("Gold").AsInt(), "two calls have separate locals and each executes once");
            Check.Equal(1212L, repeated.Read("HookOrder").AsInt(), "both complete hook chains execute");
        });
        Check.Run("RUN-GOLD-01: zero or negative final gain does not deduct or invoke AfterGained", () =>
        {
            foreach (var amount in new long[] { 0, -3 })
            {
                var fixture = Build((_, _) => new ProbeEvent(_ =>
                    [new("NoGain", effects => effects.GainGold(5))]), modify: (scope, _, _) => scope.Const(amount));
                fixture.Submit("NoGain");
                Check.Equal(10L, fixture.Read("Gold").AsInt(), $"final {amount} does not deduct");
                Check.Equal(1L, fixture.Read("HookOrder").AsInt(), "AfterModifying still runs, AfterGained does not");
            }
        });
        Check.Run("EVT-CHOICE-02/03 RUN-HEAL-01: continuation, eligibility, bounded heal and final closure", () =>
        {
            var fixture = Build((_, page) => new ProbeEvent(_ =>
            [
                new("Continue", effects =>
                    [.. effects.GainGold(1), effects.Scope.SetField(effects.Run, page, effects.Scope.Const(1L))],
                    effects => effects.Scope.Equal(effects.Read(page), effects.Scope.Const(0L)), EventCompletion.Continue),
                new("Finish", effects => effects.HealLivingPlayer(50),
                    effects => effects.Scope.Equal(effects.Read(page), effects.Scope.Const(1L))),
            ]));
            var previous = fixture.Submit("Continue");
            Check.Equal("Probe", fixture.Read("CurrentEventId").AsString(), "continued event remains active");
            Check.Equal(8L, fixture.Read("MapPhase").AsInt(), "continued event keeps its visit");
            Check.Fault(EngineFaultCode.IllegalAction,
                () => fixture.Session.Submit(previous, fixture.Player, []), "consumed page choice cannot repeat");
            fixture.Submit("Finish");
            Check.Equal(80L, fixture.Read("Hp").AsInt(), "heal capped by MaxHp");
            Check.Equal(11L, fixture.Read("Gold").AsInt(), "persistent changes survive next page");
            Check.Equal("", fixture.Read("CurrentEventId").AsString(), "finished event cleared");
            Check.Equal(2L, fixture.Read("MapPhase").AsInt(), "travel restored");
            Check.Equal(0, fixture.Session.LegalActions(fixture.Player).Count, "finished choices unavailable");
        });
        Check.Run("RUN-DAMAGE/DEATH-01: actual SunkenStatue uses shared commands and closes before defeat", () =>
        {
            foreach (var hp in new long[] { 8, 7, 3 })
            {
                var fixture = Build((state, _) => new SunkenStatueEvent(state), hp: hp);
                fixture.Submit(SunkenStatueEvent.DiveIntoWaterAction);
                Check.Equal(116L, fixture.Read("Gold").AsInt(), "source page gold 106 is gained before damage");
                Check.Equal(Math.Max(0L, hp - 7), fixture.Read("Hp").AsInt(), "source damage 7 clamps at zero");
                Check.Equal("", fixture.Read("CurrentEventId").AsString(), "cleanup completed before terminal");
                Check.Equal(2L, fixture.Read("MapPhase").AsInt(), "visit cleanup completed");
                Check.Equal(hp > 7 ? SessionStatus.Running : SessionStatus.Terminal, fixture.Session.Status,
                    "zero HP must be loss, not a running map");
                if (hp <= 7)
                {
                    Check.Equal("event-domain:Defeat", fixture.Session.Terminal?.Definition.QualifiedId, "real defeat");
                    Check.Equal(0L, fixture.Session.Terminal!.Fields[0].AsInt(), "noncombat has no turn");
                    Check.Equal(0, fixture.Session.LegalActions(fixture.Player).Count, "terminal has no actions");
                }
            }
        });
    }

    private sealed class ProbeEvent(Func<EventCompileContext, IReadOnlyList<EventChoice>> choices) : ChoiceEventModule
    {
        public override string Id => "Probe";
        protected override IReadOnlyList<EventChoice> Choices(EventCompileContext context) => choices(context);
    }

    private sealed record Fixture(Session Session, ParticipantId Player, ProjectionDefinition Projection)
    {
        public Value Read(string name) => Session.Observe(Player, Projection).Instances
            .Single(instance => instance.TypeId == "event-domain:Run").Fields.Single(field => field.Name == name).Value;
        public ActionDefinition Submit(string name)
        {
            var definition = Session.LegalActions(Player).Single(action =>
                action.Definition.QualifiedId == $"event-domain:{name}").Definition;
            Session.Submit(definition, Player, []);
            return definition;
        }
    }

    private static Fixture Build(Func<EventVisitState, FieldSymbol, IEventRoomModule> module,
        long hp = 50, Func<IrScope, Expr, Expr, Expr>? modify = null)
    {
        var package = new EnginePackageBuilder("event-domain", "0.4.0");
        var area = package.ScopeType("Area");
        var runType = package.EntityType("Run");
        var health = runType.AddField("Hp", FieldTypeSchema.Int(0, 100));
        var maxHp = runType.AddField("MaxHp", FieldTypeSchema.Int(1, 100));
        var gold = runType.AddField("Gold", FieldTypeSchema.Int(0, 99999));
        var page = runType.AddField("Page", FieldTypeSchema.Int(0, 10));
        var before = runType.AddField("BeforeGold", FieldTypeSchema.Int(0, 99999));
        var after = runType.AddField("AfterGold", FieldTypeSchema.Int(0, 99999));
        var effective = runType.AddField("EffectiveAmount", FieldTypeSchema.Int(-99999, 99999));
        var order = runType.AddField("HookOrder", FieldTypeSchema.Int(0, 99999));
        var runs = package.Collection("Runs", runType);
        var card = package.EntityType("Card");
        var deck = package.Collection("Deck", card);
        var view = package.Projection("View");
        view.ParticipantRole = SliceManifest.PlayerRole;
        view.Type(runType);
        var map = MapRoomWiring.Declare(package, view, runType);
        var room = new RoomCompileContext(runType, runs, health, maxHp, gold, deck, map);
        var state = EventVisitLifecycle.Declare(package, runType, new RunInitializationWiring(package, runType));
        var defeat = package.TerminalResult("Defeat");
        var turn = defeat.AddField("Turn", FieldTypeSchema.Int(0, 9999));
        var hooks = new GoldGainHooks(modify ?? ((_, _, amount) => amount),
            (scope, run, _) =>
            [scope.SetField(run, before, scope.Field(run, gold)),
             scope.SetField(run, order, scope.Add(scope.Multiply(scope.Field(run, order), scope.Const(10L)), scope.Const(1L)))],
            (scope, run, amount) =>
            [scope.SetField(run, after, scope.Field(run, gold)), scope.SetField(run, effective, amount),
             scope.SetField(run, order, scope.Add(scope.Multiply(scope.Field(run, order), scope.Const(10L)), scope.Const(2L)))]);
        var context = new EventCompileContext(room, state.CurrentEventId, 8)
        { Resources = new RunResources(room, defeat, turn, hooks) };
        var content = module(state, page);
        new EventRoomCatalog([content]).RegisterActions(package, context);
        var setup = package.Procedure("Setup");
        var areaSlot = setup.Local("area", FieldTypeSchema.Binding());
        var runSlot = setup.Local("run", FieldTypeSchema.RefTo(runType));
        setup.Body(setup.CreateScope(area, areaSlot),
            setup.CreateEntity(runType, setup.LocalRef(areaSlot), runSlot,
                [(health, setup.Const(hp)), (maxHp, setup.Const(80L)), (gold, setup.Const(10L)),
                 (state.CurrentEventId, setup.Const(content.Id)), (map.RunMapPhase, setup.Const(8L)),
                 (state.EventPageGold, setup.Const(106L))], runs));
        package.OnSessionStart(setup.Symbol);
        var builder = new RuleSetBuilder();
        builder.Add(package.Build());
        var session = new Session(builder.Build(), seed: 42, participantRoles: [SliceManifest.PlayerRole]);
        var player = session.Participant(0);
        var projection = session.ProjectionsFor(player).Single();
        session.Start();
        return new Fixture(session, player, projection);
    }
}
