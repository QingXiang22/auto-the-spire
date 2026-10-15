using CoreTheSpire.Engine;
using BaseSpire.Slice.Domain.Run;

namespace BaseSpire.Slice.Rooms;

/// <summary>One independently authored event. Implementations compile choices and effects into Engine IR.</summary>
public interface IEventRoomModule
{
    string Id { get; }
    void RegisterActions(EnginePackageBuilder package, EventCompileContext context);
    Inst[] Enter(IrScope scope, EventCompileContext context, Expr run);
}

/// <summary>The event owner supplies its phase and Engine field; no runtime event state is stored here.</summary>
public sealed record EventCompileContext(RoomCompileContext Room, FieldSymbol CurrentEventId, int Phase)
{
    public RunResources? Resources { get; init; }

    public Expr Active(IrScope scope, Expr run, string eventId) => scope.And(
        scope.Equal(scope.Field(run, Room.Map.RunMapPhase), scope.Const((long)Phase)),
        scope.Equal(scope.Field(run, CurrentEventId), scope.Const(eventId)));

    public Inst[] Close(IrScope scope, Expr run) =>
    [
        scope.SetField(run, CurrentEventId, scope.Const("")),
        Room.CloseVisit(scope, run),
    ];
}

/// <summary>
/// Explicit, ordered compile-time registration. Source event-pool selection is separate from executable modules:
/// an unimplemented selected ID fails rather than being replaced with a supported event.
/// </summary>
public sealed class EventRoomCatalog
{
    private readonly IEventRoomModule[] _modules;

    public EventRoomCatalog(IEnumerable<IEventRoomModule> modules)
    {
        _modules = [.. modules];
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var module in _modules)
            if (string.IsNullOrWhiteSpace(module.Id) || !ids.Add(module.Id))
                throw new ArgumentException($"Empty or duplicate event module ID '{module.Id}'", nameof(modules));
    }

    public IReadOnlyList<string> Ids => Array.AsReadOnly(_modules.Select(module => module.Id).ToArray());

    public void RegisterActions(EnginePackageBuilder package, EventCompileContext context)
    {
        foreach (var module in _modules)
            module.RegisterActions(package, context);
    }

    public Inst Enter(IrScope scope, EventCompileContext context, Expr run, Expr selectedId)
    {
        Inst branch = scope.Fail("base-spire: selected event has no registered executable module");
        // Nest in reverse so the declared order is also the dispatch order.
        foreach (var module in _modules.Reverse())
            branch = scope.IfElse(scope.Equal(selectedId, scope.Const(module.Id)),
                [scope.SetField(run, context.CurrentEventId, scope.Const(module.Id)),
                 scope.SetField(run, context.Room.Map.RunMapPhase, scope.Const((long)context.Phase)),
                 .. module.Enter(scope, context, run)], [branch]);
        return branch;
    }
}
