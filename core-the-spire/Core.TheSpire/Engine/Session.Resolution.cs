namespace CoreTheSpire.Engine;

public sealed partial class Session
{
    // -- resolution ---------------------------------------------------------------------------------------------

    private T Resolve<T>(CallActivation call, int slot) where T : Resolution
    {
        var resolution = RuleSet.Resolve(call.Procedure.RefBase, slot);
        return resolution is T typed
            ? typed
            : throw Faults.TypeError(
                $"reference slot {slot} of '{call.Procedure.QualifiedId}' is a {resolution.GetType().Name}, " +
                $"not a {typeof(T).Name}");
    }

    /// <summary>Resolves a field reference of any owner kind (entity, event payload, terminal, compute).</summary>
    private FieldResolution ResolveFieldRef(CallActivation call, int slot) => Resolve<FieldResolution>(call, slot);

    /// <summary>
    /// Resolves a field reference used as a member access (<c>target.field</c>). Only entity fields are addressable
    /// that way: payload, terminal and compute fields are bound by their own instruction.
    /// </summary>
    private FieldResolution ResolveEntityField(CallActivation call, int slot)
    {
        var resolution = ResolveFieldRef(call, slot);
        if (resolution.OwnerKind != FieldOwnerKind.Entity)
        {
            throw Faults.TypeError(
                $"field '{resolution.Field.Name}' is a {resolution.OwnerKind} field and cannot be read or written " +
                "as a member of an instance");
        }

        return resolution;
    }

    private static int IndexOf(IReadOnlyList<FieldDefinition> fields, string name)
    {
        for (var index = 0; index < fields.Count; index++)
        {
            if (string.Equals(fields[index].Name, name, StringComparison.Ordinal)) return index;
        }

        throw Faults.Unresolved($"compute asset has no declared field '{name}'");
    }

    private static InstanceId RequireReference(Value value, string what) => value.Kind switch
    {
        ValueKind.Ref => value.AsRef(),
        _ => throw Faults.TypeError($"{what} must be an instance reference but is {value.Kind}"),
    };

    private static ScopeId RequireScope(Value value) => value.Kind switch
    {
        ValueKind.Scope => value.AsScope(),
        _ => throw Faults.TypeError($"scope expression must be a scope but is {value.Kind}"),
    };

    private static string RequireStream(Value value) => value.Kind switch
    {
        ValueKind.String => value.AsString(),
        _ => throw Faults.TypeError($"random stream must be a named stream but is {value.Kind}"),
    };

    private int? EvalIndex(CallActivation call, Expr? expression)
    {
        if (expression is null) return null;
        return checked((int)Eval(call, expression).AsInt());
    }

}
