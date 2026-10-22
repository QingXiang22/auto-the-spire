using System.Globalization;

namespace CoreTheSpire.Engine;

/// <summary>Copied scalar or bounded list. Numeric scalars use invariant text; refs retain local IDs.</summary>
public sealed record SessionDebugValueSnapshot(ValueKind Kind, string? Scalar,
    IReadOnlyList<long>? Items, int OriginalLength, bool Truncated);
public sealed record SessionDebugFieldSnapshot(string Name, SessionDebugValueSnapshot Value);
public sealed record SessionDebugRelationSnapshot(InstanceId Owner, string Relation,
    IReadOnlyList<InstanceId> Targets, int TargetCount, bool TextTruncated)
{
    public bool Truncated => TextTruncated || Targets.Count != TargetCount;
}
public sealed record SessionDebugEntitySnapshot(InstanceId Instance, string? Type,
    IReadOnlyList<SessionDebugFieldSnapshot> Fields, int FieldCount, bool Missing)
{
    public bool Truncated => Fields.Count != FieldCount || Fields.Any(f => f.Value.Truncated);
}

/// <summary>Direct event values and referenced entities, or random candidates, copied at the trace point.</summary>
public sealed record SessionDebugSnapshot(IReadOnlyList<SessionDebugValueSnapshot> Values, int ValueCount,
    IReadOnlyList<SessionDebugEntitySnapshot> Entities, bool EntitiesTruncated)
{
    public int ReferenceDepth { get; init; }
    public bool RelationsCaptured { get; init; }
    public IReadOnlyList<SessionDebugRelationSnapshot> Relations { get; init; } = Array.Empty<SessionDebugRelationSnapshot>();
    public int RelationCount { get; init; }
    public bool Truncated => Values.Count != ValueCount || Values.Any(v => v.Truncated)
        || EntitiesTruncated || Entities.Any(e => e.Truncated) || Relations.Count != RelationCount || Relations.Any(r => r.Truncated);
    public bool Complete => !Truncated && !Entities.Any(e => e.Missing);
}

public sealed partial class SessionDebugTrace
{
    private SessionDebugSnapshot? CaptureSnapshot(IReadOnlyList<Value> payload, IEnumerable<InstanceId>? candidates = null,
        bool rootsTruncated = false, bool captureCandidateReferences = false, bool captureCandidateRelations = false)
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
        // Preserve the root candidate prefix before spending any budget on optional references.
        foreach (var instance in candidates ?? PayloadReferences())
            if (!CopyEntity(instance)) break;
        if (captureCandidateReferences)
        {
            // Freeze roots: referenced entities are never traversed again, even through cycles.
            var roots = entities.ToArray();
            foreach (var instance in roots.SelectMany(e => e.Fields).SelectMany(f => References(f.Value)))
                if (!CopyEntity(instance)) break;
        }
        var relations = new List<SessionDebugRelationSnapshot>(); var relationCount = 0;
        if (captureCandidateRelations)
        {
            // Freeze owners before copying relation targets: neither target relations nor
            // target field references expand this single outgoing relation boundary.
            var owners = entities.Where(e => !e.Missing).Select(e => e.Instance).ToArray();
            foreach (var owner in owners)
                foreach (var relation in _session.RuleSet.Relations)
                {
                    var targets = _session.State.RelationTargets(relation.Id, owner);
                    if (targets.Count == 0) continue;
                    relationCount++;
                    if (remaining == 0) continue;
                    remaining--; // One group header, followed by its copied target IDs.
                    var prefix = targets.Take(remaining).ToArray(); remaining -= prefix.Length;
                    relations.Add(new(owner, Limit(relation.QualifiedId)!, Array.AsReadOnly(prefix), targets.Count,
                        relation.QualifiedId.Length > _options.MaxTextLength));
                    foreach (var target in prefix)
                        if (!CopyEntity(target)) break;
                }
        }
        return new(values.AsReadOnly(), payload.Count, entities.AsReadOnly(), truncated)
        {
            ReferenceDepth = captureCandidateReferences ? 1 : 0, RelationsCaptured = captureCandidateRelations,
            Relations = relations.AsReadOnly(), RelationCount = relationCount
        };

        bool CopyEntity(InstanceId instance)
        {
            if (instance.IsNone || seen.Contains(instance)) return true;
            if (entities.Count == _options.MaxSnapshotEntities) { truncated = true; return false; }
            seen.Add(instance);
            if (!_session.State.Exists(instance))
            { entities.Add(new(instance, null, Array.Empty<SessionDebugFieldSnapshot>(), 0, true)); return true; }
            var type = _session.RuleSet.Type(_session.State.TypeIdOf(instance));
            var fields = new List<SessionDebugFieldSnapshot>();
            foreach (var field in type.Fields)
            {
                if (remaining == 0) break;
                fields.Add(new(field.Name, CopyValue(_session.State.GetField(instance, field.Index), ref remaining)));
            }
            entities.Add(new(instance, type.QualifiedId, fields.AsReadOnly(), type.Fields.Count, false));
            return true;
        }

        static IEnumerable<InstanceId> References(SessionDebugValueSnapshot value)
        {
            if (value.Kind == ValueKind.Ref) yield return new(int.Parse(value.Scalar!, CultureInfo.InvariantCulture));
            else if (value.Kind == ValueKind.RefList)
                foreach (var id in value.Items!) yield return new((int)id);
        }

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
