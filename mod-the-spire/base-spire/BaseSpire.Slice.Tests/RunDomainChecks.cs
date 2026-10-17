using BaseSpire.Slice;
using BaseSpire.Slice.Domain.Run;
using BaseSpire.Slice.Orchestration;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

internal static class RunDomainChecks
{
    public static void Run()
    {
        Check.Run("Run operations: source values captured once and spending floors at zero", () =>
        {
            var s = Build(); Submit(s, "Resources");
            Check.Equal(0L, Number(s, "Gold"), "lose beyond wallet floors at zero");
            Check.Equal(160L, Number(s, "MaxHp"), "max HP input captured before mutation");
            Check.Equal(130L, Number(s, "Hp"), "heal uses original gain once");
        });
        Check.Run("Run relics: obtained effects use shared hooks after both bags removed and instance added", () =>
        {
            var s = Build(); Submit(s, "Relics");
            Check.Equal(610L, Number(s, "Gold"), "OldCoin shares x2 gold modifier");
            Check.Equal(1L, Number(s, "HookWitness"), "gold hook observes already removed bags and owned relic");
            Check.Equal(90L, Number(s, "MaxHp"), "Pear maximum gain");
            Check.Equal(60L, Number(s, "Hp"), "Pear current health gain");
            Check.Equal(2, Members(s, "OwnedRelics").Count, "two composed obtain operations keep distinct locals");
        });
        Check.Run("Run deck: acquire preserves selected identity, hooks observe membership before removal", () =>
        {
            var s = Build(); var selected = Members(s, "Candidates").Single();
            Submit(s, "Acquire");
            Check.Equal(selected, Members(s, "Deck").Single(), "same candidate instance, no replacement clone");
            Check.Equal(2L, Number(s, "HookWitness"), "after-added observes deck member");
            Submit(s, "Remove");
            Check.Equal(0, Members(s, "Deck").Count, "removed from permanent deck");
            Check.Equal(3L, Number(s, "HookWitness"), "before-removed sees live membership");
        });
    }

    private static Session Build()
    {
        var p = new EnginePackageBuilder("run-domain", "0.4.0");
        var area = p.ScopeType("Area"); var runType = p.EntityType("Run");
        var hp = runType.AddField("Hp", FieldTypeSchema.Int(0, 9999));
        var maxHp = runType.AddField("MaxHp", FieldTypeSchema.Int(0, 9999));
        var gold = runType.AddField("Gold", FieldTypeSchema.Int(0, 99999));
        var witness = runType.AddField("HookWitness", FieldTypeSchema.Int(0, 9));
        var runs = p.Collection("Runs", runType);
        var relicSchema = RelicSchema.Declare(p, "OwnedRelics");
        var owned = relicSchema.Owned;
        var inventory = new RunInitializationWiring(p, runType);
        var defeat = p.TerminalResult("Defeat"); var turn = defeat.AddField("Turn", FieldTypeSchema.Int(0, 999));
        var resources = new RunResources(hp, maxHp, gold, defeat, turn, new(
            (s, _, amount) => s.Multiply(amount, s.Const(2L)),
            (s, run, _) => [s.If(s.And(s.Equal(s.Count(s.Collection(owned)), s.Const(1L)),
                s.And(s.Equal(s.Count(s.Collection(inventory.PlayerBags["Rare"])), s.Const(0L)),
                    s.Equal(s.Count(s.Collection(inventory.SharedBags["Rare"])), s.Const(0L)))),
                s.SetField(run, witness, s.Const(1L)))], (_, _, _) => []));
        var relics = new RunRelics(relicSchema, inventory, resources, SpireRelics.All);
        var kinds = p.Enum("CardKind"); kinds.Member("Attack").Member("Skill").Member("Power").Member("Status");
        var schema = CardSchema.Declare(p, kinds);
        var deckCards = p.Collection("Deck", schema.Type); var candidates = p.Collection("Candidates", schema.Type);
        var model = FormalSpirePackage.Load().Model;
        var definitions = new CardDefinitions(schema, kinds, model, new Dictionary<string, FieldSymbol>());
        Expr Current(IrScope s) => s.First(s.Collection(runs));
        Inst[] Witness(IrScope s, Expr card, long value) => [s.If(s.GreaterOrEqual(s.CollectionIndex(deckCards, card), s.Const(0L)),
            s.SetField(Current(s), witness, s.Const(value)))];
        var deck = new RunDeck(definitions, deckCards)
        {
            AfterAdded = (s, card) => Witness(s, card, 2), BeforeRemoved = (s, card) => Witness(s, card, 3),
        };
        var setup = p.Procedure("Setup"); var scope = setup.Local("area", FieldTypeSchema.Binding());
        var runSlot = setup.Local("run", FieldTypeSchema.RefTo(runType)); var run = setup.LocalRef(runSlot);
        var bagSlot = setup.Local("bag", FieldTypeSchema.Binding()); var cardSlot = setup.Local("card", FieldTypeSchema.Binding());
        setup.Body(setup.CreateScope(area, scope), setup.CreateEntity(runType, setup.LocalRef(scope), runSlot,
            [(hp, setup.Const(50L)), (maxHp, setup.Const(80L)), (gold, setup.Const(10L))], runs),
            setup.CreateEntity(inventory.PlayerEntry, setup.EntityScope(run), bagSlot,
                [(inventory.PlayerId, setup.Const("base-spire:old-coin"))], inventory.PlayerBags["Rare"]),
            setup.CreateEntity(inventory.SharedEntry, setup.EntityScope(run), bagSlot,
                [(inventory.SharedId, setup.Const("base-spire:old-coin"))], inventory.SharedBags["Rare"]),
            setup.CreateEntity(schema.Type, setup.EntityScope(run), cardSlot,
                definitions.New(setup, definitions.Definition("base-spire:strike-silent"), false), candidates));
        p.OnSessionStart(setup.Symbol);
        var spend = p.ActionBody(p.Action("Resources")); spend.Role(SliceManifest.PlayerRole);
        spend.Body([.. resources.LoseGold(spend, Current(spend), spend.Multiply(spend.Field(Current(spend), gold), spend.Const(2L))),
            .. resources.GainMaxHp(spend, Current(spend), spend.Field(Current(spend), maxHp))]);
        var obtain = p.ActionBody(p.Action("Relics")); obtain.Role(SliceManifest.PlayerRole);
        obtain.Body([.. relics.Obtain(obtain, Current(obtain), obtain.Const("base-spire:old-coin")),
            .. relics.Obtain(obtain, Current(obtain), obtain.Const("base-spire:pear"))]);
        var acquire = p.ActionBody(p.Action("Acquire")); acquire.Role(SliceManifest.PlayerRole);
        var selected = acquire.Local("selected", FieldTypeSchema.RefTo(schema.Type));
        acquire.Body([acquire.SetLocal(selected, acquire.First(acquire.Collection(candidates))),
            acquire.RemoveFromCollection(candidates, acquire.LocalRef(selected)), .. deck.Acquire(acquire, acquire.LocalRef(selected))]);
        var remove = p.ActionBody(p.Action("Remove")); remove.Role(SliceManifest.PlayerRole);
        remove.Body(deck.Remove(remove, remove.First(remove.Collection(deckCards))));
        var builder = new RuleSetBuilder(); builder.Add(p.Build());
        var session = new Session(builder.Build(), 42, [SliceManifest.PlayerRole]); session.Start(); return session;
    }
    private static void Submit(Session s, string name)
    {
        s.Submit(s.LegalActions(SliceManifest.Player).Single(a => a.Definition.QualifiedId == "run-domain:" + name).Definition, SliceManifest.Player, []);
        Check.True(s.Status != SessionStatus.Faulted, $"{s.Fault}");
    }
    private static IReadOnlyList<InstanceId> Members(Session s, string name) => s.State.CollectionMembers(s.RuleSet.Collections.Single(c => c.QualifiedId == "run-domain:" + name).Id);
    private static long Number(Session s, string field)
    {
        var type = s.RuleSet.TypeByQualifiedId("run-domain:Run");
        return s.State.GetField(s.State.AllInstances(type.Id).Single(), type.FieldIndex(field)).AsInt();
    }
}
