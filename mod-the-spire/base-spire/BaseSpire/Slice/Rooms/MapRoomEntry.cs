using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Rooms;

/// <summary>A map-kind entry compiler. Captured symbols belong to package construction, not a running Session.</summary>
public sealed record MapRoomEntry(string PointType, Func<IrScope, Expr, Expr, Inst[]> Enter)
{
    public static Inst[] Visit(MapRoomWiring map, IrScope scope, Expr run, Expr node, int? phase = null)
    {
        var steps = new List<Inst>
        {
            scope.SetField(run, map.RunCurrentNode, node),
            scope.SetField(node, map.NodeVisited, scope.Const(true)),
            scope.SetField(run, map.RunMapFloor, scope.Add(scope.Field(node, map.NodeRow), scope.Const(1L))),
        };
        if (phase is { } value)
            steps.Add(scope.SetField(run, map.RunMapPhase, scope.Const((long)value)));
        return [.. steps];
    }

    /// <summary>Existing room entries in their original declaration order. New kinds compose additional entries.</summary>
    public static IReadOnlyList<MapRoomEntry> Standard(MapRoomWiring map, ProcedureBuilder startNextRoom,
        TreasureRoomWiring treasure, ShopRoomWiring shop) =>
    [
        new("RestSite", (scope, run, node) => Visit(map, scope, run, node, MapRoomWiring.RestSitePhase)),
        new("Monster", (scope, run, node) =>
            [.. Visit(map, scope, run, node, 1), scope.Call(startNextRoom.Symbol)]),
        new("Elite", (scope, run, node) =>
            [.. Visit(map, scope, run, node, 1), scope.Call(startNextRoom.Symbol)]),
        new("Treasure", (scope, run, node) =>
            [.. Visit(map, scope, run, node, MapRoomWiring.TreasurePhase), .. treasure.Enter(scope, run)]),
        new("Shop", (scope, run, node) =>
            [.. Visit(map, scope, run, node), .. shop.Enter(scope, run)]),
        new("Boss", (scope, run, node) =>
            [.. Visit(map, scope, run, node, 1), scope.Call(startNextRoom.Symbol)]),
    ];
}
