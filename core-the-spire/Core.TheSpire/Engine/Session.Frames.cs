namespace CoreTheSpire.Engine;

public sealed partial class Session
{
    private enum Outcome
    {
        Continue = 0,
        Completed = 1,
        Suspended = 2,
    }

    private abstract class Activation;

    // Pending entry points are siblings, not nested calls. Keep one cursor in the existing
    // scheduler so later initializers survive a decision without consuming call-depth budget.
    private sealed class StartupActivation(IReadOnlyList<ProcedureDefinition> procedures) : Activation
    {
        public IReadOnlyList<ProcedureDefinition> Procedures { get; } = procedures;

        public int Next { get; set; }
    }

    private sealed class CallActivation : Activation
    {
        public CallActivation(ProcedureDefinition procedure, ScopeId scope, int resultSlot, CallActivation? caller,
            IReadOnlyList<Value> arguments)
        {
            Procedure = procedure;
            Scope = scope;
            ResultSlot = resultSlot;
            Caller = caller;
            Slots = new Value[procedure.Parameters.Count + procedure.Locals.Count];
            for (var index = 0; index < procedure.Parameters.Count; index++)
                Slots[index] = index < arguments.Count ? arguments[index] : procedure.Parameters[index].DefaultValue;
            for (var index = 0; index < procedure.Locals.Count; index++)
                Slots[procedure.Parameters.Count + index] = procedure.Locals[index].DefaultValue;
            Cursors = [new BlockCursor(procedure.Body)];
        }

        public ProcedureDefinition Procedure { get; }

        public ScopeId Scope { get; set; }

        public int ResultSlot { get; }

        public CallActivation? Caller { get; }

        public Value[] Slots { get; }

        public List<BlockCursor> Cursors { get; }

        public Value ReturnValue { get; set; } = Value.None;
    }

    private sealed class DispatchActivation(EventDefinition eventType, SubscriptionDefinition[] listeners,
        Value[] payload, ScopeId inheritedScope) : Activation
    {
        public EventDefinition EventType { get; } = eventType;

        public SubscriptionDefinition[] Listeners { get; } = listeners;

        public Value[] Payload { get; } = payload;

        public ScopeId InheritedScope { get; } = inheritedScope;

        public int Next { get; set; }
    }

    private sealed class BlockCursor(Block block)
    {
        public Block Block { get; } = block;

        public int Pc { get; set; }

        public long RepeatRemaining { get; set; }

        public IReadOnlyList<InstanceId>? Items { get; init; }

        public int ItemIndex { get; set; }

        public int ItemSlot { get; init; } = -1;
    }

    private sealed record Pending(DecisionRequest Request, CallActivation Caller, int ResultSlot);

    /// <summary>Ordering used by <c>sort</c> queries: total, deterministic and independent of culture.</summary>
    private sealed class ValueComparer : IComparer<Value>
    {
        public static readonly ValueComparer Instance = new();

        public int Compare(Value left, Value right)
        {
            if (left.Kind != right.Kind) return left.Kind.CompareTo(right.Kind);
            return left.Kind switch
            {
                ValueKind.None => 0,
                ValueKind.Bool => left.AsBool().CompareTo(right.AsBool()),
                ValueKind.Int => left.AsInt().CompareTo(right.AsInt()),
                ValueKind.Fixed => left.AsDecimal().CompareTo(right.AsDecimal()),
                ValueKind.Enum => left.AsEnumOrdinal().CompareTo(right.AsEnumOrdinal()),
                ValueKind.String => string.CompareOrdinal(left.AsString(), right.AsString()),
                ValueKind.Ref => left.AsRef().CompareTo(right.AsRef()),
                ValueKind.Participant => left.AsParticipant().CompareTo(right.AsParticipant()),
                ValueKind.Scope => left.AsScope().CompareTo(right.AsScope()),
                _ => 0,
            };
        }
    }
}
