using BaseSpire.Slice;
using BaseSpire.Slice.Rooms;
using BaseSpire.Slice.Domain.Run;
using CoreTheSpire.Engine;

// Controlled extension example, not original-game content or a real Unknown-room roll.
// Add a module file and one catalog entry. Neither dispatcher knows concrete event classes.
var catalog = new EventRoomCatalog([new GoldEvent(), new HealEvent()]);
Run("example-events:gold", "example-events:TakeEventGold", 50, 17);
Run("example-events:heal", "example-events:HealInEvent", 55, 10);
try
{
    _ = new EventRoomCatalog([new GoldEvent(), new GoldEvent()]);
    throw new InvalidOperationException("duplicate event ID was accepted");
}
catch (ArgumentException) { Console.WriteLine("duplicate event ID rejected"); }
Run("example-events:missing", "", 50, 10);
Console.WriteLine("independent modules, map entry, choice isolation and visit closure verified");

void Run(string selectedId, string expectedAction, long expectedHp, long expectedGold)
{
    var package = new EnginePackageBuilder("example-events", "0.4.0");
    var area = package.ScopeType("Area");
    var runType = package.EntityType("Run");
    var hp = runType.AddField("Hp", FieldTypeSchema.Int(0, 100));
    var maxHp = runType.AddField("MaxHp", FieldTypeSchema.Int(1, 100));
    var gold = runType.AddField("Gold", FieldTypeSchema.Int(0, 999));
    var eventId = runType.AddField("CurrentEventId", FieldTypeSchema.String(128));
    var runs = package.Collection("Runs", runType);
    var card = package.EntityType("Card");
    var deck = package.Collection("LongTermDeck", card);
    var view = package.Projection("PlayerView");
    view.ParticipantRole = "player";
    view.Type(runType);
    var map = MapRoomWiring.Declare(package, view, runType);
    var fixtureEventId = map.Node.AddField("FixtureEventId", FieldTypeSchema.String(128));
    var rooms = new RoomCompileContext(runType, runs, hp, maxHp, gold, deck, map);
    // Phase 5 is fixture-only; the production event owner declares its own phase and field bounds.
    var defeat = package.TerminalResult("Defeat");
    var turn = defeat.AddField("Turn", FieldTypeSchema.Int(0, 9999));
    var events = new EventCompileContext(rooms, eventId, 5)
    {
        Resources = new RunResources(rooms, defeat, turn),
    };
    catalog.RegisterActions(package, events);
    map.RegisterChooseMapPoint(package, runs,
    [
        new MapRoomEntry("Unknown", (scope, run, node) =>
        [
            .. MapRoomEntry.Visit(map, scope, run, node),
            catalog.Enter(scope, events, run, scope.Field(node, fixtureEventId)),
        ]),
    ]);

    var setup = package.Procedure("Setup");
    var areaSlot = setup.Local("area", FieldTypeSchema.Binding());
    var runSlot = setup.Local("run", FieldTypeSchema.RefTo(runType));
    var startSlot = setup.Local("start", FieldTypeSchema.RefTo(map.Node));
    var nodeSlot = setup.Local("event", FieldTypeSchema.RefTo(map.Node));
    setup.Body(
        setup.CreateScope(area, areaSlot),
        setup.CreateEntity(map.Node, setup.LocalRef(areaSlot), startSlot,
            [(map.NodeType, setup.EnumConst(map.PointType, "Ancient"))], map.Nodes),
        setup.CreateEntity(map.Node, setup.LocalRef(areaSlot), nodeSlot,
            [(map.NodeType, setup.EnumConst(map.PointType, "Unknown")), (map.NodeRow, setup.Const(1L)),
             (fixtureEventId, setup.Const(selectedId))], map.Nodes),
        setup.AddRelation(map.Children, setup.LocalRef(startSlot), setup.LocalRef(nodeSlot)),
        setup.CreateEntity(runType, setup.LocalRef(areaSlot), runSlot,
            [(hp, setup.Const(50L)), (maxHp, setup.Const(80L)), (gold, setup.Const(10L)),
             (eventId, setup.Const("")), (map.RunCurrentNode, setup.LocalRef(startSlot)),
             (map.RunMapPhase, setup.Const(0L))], runs));
    package.OnSessionStart(setup.Symbol);
    var builder = new RuleSetBuilder();
    builder.Add(package.Build());
    var session = new Session(builder.Build(), seed: 42, participantRoles: ["player"]);
    var player = session.Participant(0);
    var projection = session.ProjectionsFor(player).Single();
    session.Start();
    var choose = session.LegalActions(player).Single();
    if (expectedAction.Length == 0)
    {
        try
        {
            session.Submit(choose.Definition, player, [Value.OfRef(choose.Candidates.Single())]);
            throw new InvalidOperationException("unregistered event was silently accepted");
        }
        catch (EngineFaultException fault) when (fault.Message.Contains("no registered executable module"))
        { Console.WriteLine("unregistered selected event failed explicitly"); }
        return;
    }
    session.Submit(choose.Definition, player, [Value.OfRef(choose.Candidates.Single())]);
    var option = session.LegalActions(player).Single();
    if (option.Definition.QualifiedId != expectedAction)
        throw new InvalidOperationException($"wrong event choice: {option.Definition.QualifiedId}");
    session.Submit(option.Definition, player, []);
    var runView = session.Observe(player, projection).Instances.Single(i => i.TypeId == "example-events:Run");
    Value Read(string name) => runView.Fields.Single(field => field.Name == name).Value;
    if (Read("Hp").AsInt() != expectedHp || Read("Gold").AsInt() != expectedGold
        || Read("MapPhase").AsInt() != 2 || Read("CurrentEventId").AsString() != ""
        || session.LegalActions(player).Any(action => action.Definition.QualifiedId == expectedAction))
        throw new InvalidOperationException("event effects, closure or one-time choice failed");
    Console.WriteLine($"{selectedId}: Hp={Read("Hp")}, Gold={Read("Gold")}, phase=2, event cleared");
}
