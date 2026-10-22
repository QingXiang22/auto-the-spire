using System.Globalization;

namespace CoreTheSpire.Engine;

/// <summary>Copied scalar or bounded list. Numeric scalars use invariant text; refs retain local IDs.</summary>
public sealed record SessionDebugValueSnapshot(ValueKind Kind, string? Scalar,
    IReadOnlyList<long>? Items, int OriginalLength, bool Truncated);
public sealed record SessionDebugFieldSnapshot(string Name, SessionDebugValueSnapshot Value);
public sealed record SessionDebugEntitySnapshot(InstanceId Instance, string? Type,
    IReadOnlyList<SessionDebugFieldSnapshot> Fields, int FieldCount, bool Missing)
{
    public bool Truncated => Fields.Count != FieldCount || Fields.Any(f => f.Value.Truncated);
}

/// <summary>Direct event values and referenced entities, or random candidates, copied at the trace point.</summary>
public sealed record SessionDebugSnapshot(IReadOnlyList<SessionDebugValueSnapshot> Values, int ValueCount,
    IReadOnlyList<SessionDebugEntitySnapshot> Entities, bool EntitiesTruncated)
{
    public bool Truncated => Values.Count != ValueCount || Values.Any(v => v.Truncated)
        || EntitiesTruncated || Entities.Any(e => e.Truncated);
    public bool Complete => !Truncated && !Entities.Any(e => e.Missing);
}

public sealed partial class SessionDebugTrace
{
    private SessionDebugSnapshot? CaptureSnapshot(IReadOnlyList<Value> payload, IEnumerable<InstanceId>? candidates = null,
        bool rootsTruncated = false)
    {
        if (!_options.CaptureValues) return null;
        var remaining = _options.MaxSnapshotValues;
        var values = new List<SessionDebugValueSnapshot>();
        foreach (var value in payload)
        {
            if (remaining == 0) break;
            values.Add(CopyValue(value, ref remaining));
        }
        var entities = new List<SessionDebugEntitySnapshot>();
        var seen = new HashSet<InstanceId>();
        var truncated = rootsTruncated;
        // Follow only roots in the copied payload/candidate prefix. Fields keep references as IDs;
        // walking them recursively could retain an entire run and is deliberately unsupported.
        foreach (var instance in candidates ?? PayloadReferences())
        {
            if (instance.IsNone || !seen.Add(instance)) continue;
            if (entities.Count == _options.MaxSnapshotEntities) { truncated = true; break; }
            if (!_session.State.Exists(instance))
            { entities.Add(new(instance, null, Array.Empty<SessionDebugFieldSnapshot>(), 0, true)); continue; }
            var type = _session.RuleSet.Type(_session.State.TypeIdOf(instance));
            var fields = new List<SessionDebugFieldSnapshot>();
            foreach (var field in type.Fields)
            {
                if (remaining == 0) break;
                fields.Add(new(field.Name, CopyValue(_session.State.GetField(instance, field.Index), ref remaining)));
            }
            entities.Add(new(instance, type.QualifiedId, fields.AsReadOnly(), type.Fields.Count, false));
        }
        return new(values.AsReadOnly(), payload.Count, entities.AsReadOnly(), truncated);

        IEnumerable<InstanceId> PayloadReferences()
        {
            for (var i = 0; i < values.Count; i++)
            {
                var value = payload[i];
                if (value.Kind == ValueKind.Ref) yield return value.AsRef();
                else if (value.Kind == ValueKind.RefList)
                    foreach (var instance in value.AsRefList().Take(values[i].Items!.Count)) yield return instance;
            }
        }
    }

    private SessionDebugValueSnapshot CopyValue(Value value, ref int remaining)
    {
        remaining--;
        if (value.Kind == ValueKind.String)
        {
            var text = value.AsString();
            return new(value.Kind, Limit(text), null, text.Length, text.Length > _options.MaxTextLength);
        }
        if (value.Kind is ValueKind.RefList or ValueKind.IntList)
        {
            var count = value.Kind == ValueKind.RefList ? value.AsRefList().Count : value.AsIntList().Count;
            var length = Math.Min(count, remaining);
            var items = new long[length];
            for (var i = 0; i < length; i++)
                items[i] = value.Kind == ValueKind.RefList ? value.AsRefList()[i].Value : value.AsIntList()[i];
            remaining -= length;
            return new(value.Kind, null, Array.AsReadOnly(items), count, length != count);
        }
        var scalar = value.Kind switch
        {
            ValueKind.None => null,
            ValueKind.Bool => value.AsBool() ? "true" : "false",
            ValueKind.Int => value.AsInt().ToString(CultureInfo.InvariantCulture),
            ValueKind.Fixed => value.AsDecimal().ToString(CultureInfo.InvariantCulture),
            ValueKind.Enum => value.AsEnumOrdinal().ToString(CultureInfo.InvariantCulture),
            ValueKind.Ref => value.AsRef().Value.ToString(CultureInfo.InvariantCulture),
            ValueKind.Participant => value.AsParticipant().Value.ToString(CultureInfo.InvariantCulture),
            ValueKind.Scope => value.AsScope().Value.ToString(CultureInfo.InvariantCulture),
            _ => throw new InvalidOperationException("Unknown debug value kind: " + value.Kind),
        };
        return new(value.Kind, scalar, null, 0, false);
    }
}
