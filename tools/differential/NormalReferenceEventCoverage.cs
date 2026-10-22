#if REFERENCE_SIM
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models;

namespace Differential;

internal sealed record NormalNativeListener(int Sequence, int Instance, string Definition, string Hook, string Side,
    bool Starting, string DeclaringType, bool DefaultNoOp);
internal sealed record NormalNativeUnmappedEvent(int Sequence, string Name);
internal sealed record NormalNativeEventCoverage(int Total, int Mapped, NormalNativeListener[] Listeners, NormalNativeUnmappedEvent[] Unmapped);

internal sealed class NormalReferenceEventCoverage
{
    private readonly Dictionary<AbstractModel, int> identities = new(ReferenceEqualityComparer.Instance);
    private readonly List<NormalNativeListener> listeners = [];
    private readonly List<NormalNativeUnmappedEvent> unmapped = [];
    private (AbstractModel Model, string Hook, CombatSide Side)? pending;
    private int total;
    private int mapped;

    internal void Record(string name, object?[] arguments, bool projected)
    {
        var sequence = ++total;
        if (projected) { mapped++; return; }
        if (name != "SideTurnEndListener") { unmapped.Add(new(sequence, name)); return; }
        if (arguments is not [string hook, AbstractModel model, CombatSide side, bool starting]
            || hook is not ("BeforeSideTurnEndVeryEarly" or "BeforeSideTurnEndEarly" or "BeforeSideTurnEnd" or "AfterSideTurnEnd")
            || side is not (CombatSide.Player or CombatSide.Enemy))
            throw Missing("listener arguments");
        var method = model.GetType().GetMethod(hook, [typeof(CombatSide), typeof(IEnumerable<Creature>)])
            ?? throw Missing("listener virtual method");
        if (starting)
        {
            if (pending is not null) throw Missing("nested listener start");
            pending = (model, hook, side);
        }
        else
        {
            if (pending is not { } before || !ReferenceEquals(before.Model, model) || before.Hook != hook || before.Side != side)
                throw Missing("unmatched listener finish");
            pending = null;
        }
        if (!identities.TryGetValue(model, out var identity)) identities.Add(model, identity = identities.Count);
        // The four locked AbstractModel implementations return Task.CompletedTask.
        // An override is never assumed harmless merely because this invocation has no delta.
        var noOp = method.DeclaringType == typeof(AbstractModel);
        listeners.Add(new(sequence, identity, NormalReferenceRun.Id(model.Id), hook, side.ToString(), starting,
            method.DeclaringType!.FullName!, noOp));
        if (!noOp) unmapped.Add(new(sequence, name + ":" + hook + ":" + method.DeclaringType.FullName));
    }

    internal NormalNativeEventCoverage Capture()
    {
        if (pending is not null) throw Missing("unfinished listener at stable boundary");
        return new(total, mapped, listeners.ToArray(), unmapped.ToArray());
    }

    private static NotSupportedException Missing(string detail) => new("missing_dimension: native event coverage " + detail);
}
#endif
