using BaseSpire.Slice;
using CoreTheSpire.Engine;

// A room author needs only the bindings and this module, not CombatSlicePackage.
static class ExampleRoom
{
    public static void Register(EnginePackageBuilder package, RoomCompileContext room)
    {
        var body = package.ActionBody(package.Action("TakeExampleGold"));
        var run = room.CurrentRun(body);
        body.Role("player");
        body.Requires(room.At(body, run, MapRoomWiring.TreasurePhase, "Treasure"));
        body.Body(
            body.SetField(run, room.Gold, body.Add(body.Field(run, room.Gold), body.Const(7L))),
            room.CloseVisit(body, run));
    }
}
