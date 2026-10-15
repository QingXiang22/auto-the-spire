using CoreTheSpire.Engine;

namespace BaseSpire.Slice;

/// <summary>
/// Compile-time bindings shared by map rooms. These symbols generate Engine IR;
/// the context does not hold a Session or mutable Run state.
/// </summary>
public sealed record RoomCompileContext(
    EntityTypeSymbol RunType,
    CollectionSymbol Runs,
    FieldSymbol Hp,
    FieldSymbol MaxHp,
    FieldSymbol Gold,
    CollectionSymbol LongTermDeck,
    MapRoomWiring Map)
{
    public Expr CurrentRun(IrScope scope) => scope.First(scope.Collection(Runs));

    /// <summary>
    /// Checks the visit phase and the room type. The room type comes from
    /// <see cref="MapRoomWiring.AtRoomType"/>: the **actual** resolved room type (a `?` node keeps its
    /// `Unknown` map type while the visit is a real room) or the node's own type.
    /// </summary>
    public Expr At(IrScope scope, Expr run, int phase, string pointType) => scope.And(
        scope.Equal(scope.Field(run, Map.RunMapPhase), scope.Const((long)phase)),
        Map.AtRoomType(scope, run, pointType));

    /// <summary>
    /// Opens map travel after a resolved visit. The room must finish its own
    /// choices and commit persistent changes before emitting this instruction.
    /// </summary>
    public Inst CloseVisit(IrScope scope, Expr run) =>
        scope.SetField(run, Map.RunMapPhase, scope.Const((long)MapRoomWiring.TravelReadyPhase));
}
