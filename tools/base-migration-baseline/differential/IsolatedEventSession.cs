using BaseSpire.SinglePlayer;
using BaseSpire.Slice;
using BaseSpire.Slice.Domain.Run;
using BaseSpire.Slice.Orchestration;
using BaseSpire.Slice.Rooms;
using CoreTheSpire.Engine;

namespace Differential;

/// <summary>Tool-only controlled visits through the production event catalogue; no map generation, battle or Host.</summary>
internal sealed class IsolatedEventSession
{
    public RuleSet Rules { get; }
    public IReadOnlyList<string> Modules { get; }
    public string ContentDigest { get; }

    public IsolatedEventSession()
    {
        var configuration = RunConfiguration.Ironclad(0, BaseSpireScope.DevRun);
        var content = SinglePlayerContentVersions.Apply(SinglePlayerContentSource.Shared, configuration.ContentMetadataVersion);
        ContentDigest = content.ContentDigest;
        var package = new EnginePackageBuilder("base-spire", "0.4.0");
        var area = package.ScopeType("ControlledEventArea");
        var run = package.EntityType("Run");
        var hp = run.AddField("Hp", FieldTypeSchema.Int(0, 999999));
        var maxHp = run.AddField("MaxHp", FieldTypeSchema.Int(1, 999999));
        var gold = run.AddField("Gold", FieldTypeSchema.Int(0, 999999));
        var allowed = run.AddField("ProbeAllowed", FieldTypeSchema.Bool());
        var runs = package.Collection("RunStates", run);
        var kind = package.Enum("CardKind");
        foreach (var name in new[] { "Attack", "Skill", "Power", "Status", "Curse", "Quest" }) kind.Member(name);
        var cards = CardSchema.Declare(package, kind);
        var deckCards = package.Collection("LongTermDeck", cards.Type);
        var variables = new Dictionary<string, FieldSymbol>(StringComparer.Ordinal);
        var definitions = new CardDefinitions(cards, kind, content.Model, variables);
        foreach (var name in definitions.Compiled.SelectMany(c => c.Normal.Variables.Keys.Concat(c.Upgraded.Variables.Keys))
                     .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
            variables.Add(name, cards.Type.AddField("Variable_" + name, FieldTypeSchema.Int(-999999, 999999)));
        var deck = new RunDeck(definitions, deckCards);
        var view = package.Projection("PlayerView"); view.ParticipantRole = SliceManifest.PlayerRole;
        view.Type(run); view.Type(cards.Type);
        var map = MapRoomWiring.Declare(package, view, run);
        var room = new RoomCompileContext(run, runs, hp, maxHp, gold, deckCards, map);
        var state = EventVisitLifecycle.Declare(package, run, new RunInitializationWiring(package, run));
        var defeat = package.TerminalResult("Defeat");
        var turn = defeat.AddField("Turn", FieldTypeSchema.Int(0, 999999));
        var context = new EventCompileContext(room, state.CurrentEventId, 8)
        { Resources = new RunResources(room, defeat, turn), Deck = deck, SourceEvents = RunSourcePoolManifest.Events };
        var modules = EventModules.Create(state, package);
        var catalogue = new EventRoomCatalog(modules);
        Modules = catalogue.Ids;
        catalogue.RegisterActions(package, context);
        foreach (var module in modules)
        {
            var entry = package.ActionBody(package.Action("ProbeEnter" + module.Id));
            var owner = room.CurrentRun(entry);
            entry.Role(SliceManifest.PlayerRole);
            entry.Requires(entry.And(entry.Equal(entry.Field(owner, state.CurrentEventId), entry.Const("")),
                entry.Greater(entry.Field(owner, hp), entry.Const(0L))));
            entry.Body(catalogue.Enter(entry, context, owner, entry.Const(module.Id)));
        }
        var qualify = package.ActionBody(package.Action("ProbeMorphicEligibility"));
        var qualification = qualify.Local("allowed", FieldTypeSchema.Bool());
        var qualifyingRun = room.CurrentRun(qualify); qualify.Role(SliceManifest.PlayerRole);
        qualify.Body([.. SourceEventEligibility.Evaluate(qualify, context, qualifyingRun, "MorphicGrove", qualification),
            qualify.SetField(qualifyingRun, allowed, qualify.LocalRef(qualification))]);

        var setup = package.Procedure("SetupControlledEvent");
        var scope = setup.Local("area", FieldTypeSchema.Binding());
        var ownerSlot = setup.Local("run", FieldTypeSchema.RefTo(run));
        setup.Body([setup.CreateScope(area, scope),
            setup.CreateEntity(run, setup.LocalRef(scope), ownerSlot,
                [(hp, setup.Const(80L)), (maxHp, setup.Const(80L)), (gold, setup.Const(99L)),
                 (map.RunMapPhase, setup.Const(2L)), (state.CurrentEventId, setup.Const(""))], runs),
            .. BaseSpireCharacters.IroncladDeck.SelectMany(id => deck.Create(setup, setup.LocalRef(ownerSlot), definitions.Definition(id)))]);
        package.OnSessionStart(setup.Symbol);
        var builder = new RuleSetBuilder(); builder.Add(package.Build()); Rules = builder.Build();
    }

    public Session Create(string seed, long hp = 80, long maxHp = 80, long gold = 99, int deckCount = 10)
    {
        var layout = new RunRandomLayout(RunRandomLayout.V109);
        var root = layout.RootSeed(seed);
        var session = new Session(Rules, root, [SliceManifest.PlayerRole], randomAlgorithmId: VanillaRandomLayout.AlgorithmId,
            randomDrawProfileId: VanillaRandomLayout.DrawProfileId, randomProfile: VanillaRandomLayout.Profile,
            computeBackend: new BaseSpireComputeBackend());
        foreach (var stream in layout.Streams(seed)) session.Random.SeedStream(stream.Stream, stream.Seed);
        foreach (var name in RunSourcePoolManifest.Events)
            session.Random.SeedStream(EventRandomAsset.StreamName(name), layout.ContentSeed(root, 0, VanillaRandomLayout.SourceEventEntry(name)));
        session.Start();
        session.State.RunAtomic(() =>
        {
            var run = Members(session, "RunStates").Single();
            Set(session, run, "Hp", Value.Of(hp)); Set(session, run, "MaxHp", Value.Of(maxHp)); Set(session, run, "Gold", Value.Of(gold));
            foreach (var card in Members(session, "LongTermDeck").Skip(deckCount).ToArray()) session.State.DestroyInstance(card);
        });
        return session;
    }

    public static void Enter(Session session, string name) => Submit(session, "ProbeEnter" + name);
    public static void Submit(Session session, string name) => session.Submit(
        session.RuleSet.Actions.Single(a => a.QualifiedId == "base-spire:" + name), SliceManifest.Player, []);
    public static IReadOnlyList<InstanceId> Members(Session s, string name) =>
        s.State.CollectionMembers(s.RuleSet.Collections.Single(c => c.QualifiedId == "base-spire:" + name).Id);
    public static Value Get(Session s, InstanceId id, string name) => s.State.GetField(id, s.RuleSet.Type(s.State.TypeIdOf(id)).FieldIndex(name));
    public static void Set(Session s, InstanceId id, string name, Value value) => s.State.SetField(id, s.RuleSet.Type(s.State.TypeIdOf(id)).FieldIndex(name), value);
}
