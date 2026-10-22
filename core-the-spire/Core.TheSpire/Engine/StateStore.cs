using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace CoreTheSpire.Engine;

/// <summary>
/// Session storage (K-02): scopes, entity instances with typed gameplay fields, collections and relations.
///
/// Invariants:
/// <list type="bullet">
/// <item>instance and scope identities are session-unique and <em>never reused</em>, not even after a rollback, so a
/// stale handle can always be recognised instead of silently addressing a new object;</item>
/// <item>frame local slots are <em>not</em> stored here: locals live in the scheduler frame, gameplay fields live in
/// this store, and only <see cref="FieldLifetime.Persistent"/> fields participate in snapshots and the state hash;</item>
/// <item>an instance lives in at most one collection per declared membership group, every collection respects its
/// declared capacity, and every reference field points at a live instance of its declared target type;</item>
/// <item><b>a non-optional reference field is a construction obligation.</b> An instance whose declaration has a
/// non-optional <see cref="FieldTypeKind.Ref"/> field starts <em>under construction</em>: that field may hold
/// <see cref="Value.None"/> only while the construction process that created the instance is still open. The process
/// is the whole host command (or one <see cref="RunAtomic{T}"/> unit for direct store use), so it may span several
/// instructions, but it ends at the first stable boundary — command end, decision request, terminal result,
/// projection or state hash. Such a boundary must never publish the instance, so it raises
/// <see cref="EngineFaultCode.RequiredRefUnassigned"/> instead. The obligation is derived from the stored values, so
/// it follows rollbacks automatically and needs no bookkeeping of its own;</item>
/// <item>a single command is applied atomically: if any validation or mutation fails, the store is restored to the
/// exact pre-command state (values, memberships, relations and the change version) and the fault is rethrown;</item>
/// <item>destruction is total and deterministic: destroying an instance removes its memberships and both directions
/// of its relations, and clears inbound references (a non-optional inbound reference makes the destroy fail instead of
/// leaving a dangling handle).</item>
/// </list>
/// </summary>
public sealed class SessionState
{
    private readonly RuleSet _ruleSet;
    private readonly Dictionary<int, ScopeRecord> _scopes = [];
    private readonly Dictionary<int, InstanceRecord> _instances = [];
    private readonly Dictionary<int, SortedSet<InstanceId>> _instancesByType = [];
    private readonly Dictionary<int, HashSet<int>> _referrerTypes = [];
    private readonly Dictionary<int, List<InstanceId>> _collections = [];
    private readonly Dictionary<int, Dictionary<int, List<InstanceId>>> _relations = [];
    private readonly Dictionary<string, List<int>> _membershipGroups = new(StringComparer.Ordinal);
    private List<Action>? _journal;

    public SessionState(RuleSet ruleSet)
    {
        _ruleSet = ruleSet ?? throw new ArgumentNullException(nameof(ruleSet));
        RootScope = new ScopeId(1);
        NextScopeId = 2;
        NextInstanceId = 1;
        _scopes.Add(RootScope.Value, new ScopeRecord(RootScope, -1, ScopeId.None));

        foreach (var collection in ruleSet.Collections)
        {
            _collections[collection.Id] = [];
            if (collection.MembershipGroup is null) continue;
            if (!_membershipGroups.TryGetValue(collection.MembershipGroup, out var group))
            {
                group = [];
                _membershipGroups.Add(collection.MembershipGroup, group);
            }

            group.Add(collection.Id);
        }

        foreach (var relation in ruleSet.Relations) _relations[relation.Id] = [];
        foreach (var type in ruleSet.Types)
            foreach (var field in type.Fields.Where(field => field.Kind == FieldTypeKind.Ref))
            {
                if (!_referrerTypes.TryGetValue(field.TargetTypeId, out var owners))
                    _referrerTypes[field.TargetTypeId] = owners = [];
                owners.Add(type.Id);
            }
    }

    /// <summary>Collections declared by the linked rule set, in dense id order.</summary>
    public IReadOnlyList<CollectionDefinition> RuleSetCollections => _ruleSet.Collections;

    /// <summary>Relations declared by the linked rule set, in dense id order.</summary>
    public IReadOnlyList<RelationDefinition> RuleSetRelations => _ruleSet.Relations;

    /// <summary>Root scope of the session. It has no declared scope type.</summary>
    public ScopeId RootScope { get; }

    /// <summary>Monotonic change counter. Every committed mutation increments it; a rolled-back command does not.</summary>
    public long Version { get; private set; }

    /// <summary>Number of live instances (destroyed instances are not counted).</summary>
    public int InstanceCount => _instances.Count;

    /// <summary>Number of live scopes including the root scope.</summary>
    public int ScopeCount => _scopes.Count;

    /// <summary>
    /// Live instances whose construction is still open, in ascending identity: each of them has at least one
    /// non-optional reference field without a live target yet. Empty at every stable boundary by construction; the
    /// list exists so a host, a check or a diagnostic can name the open instances.
    /// </summary>
    public IReadOnlyList<InstanceId> UnderConstruction
    {
        get
        {
            var result = new List<InstanceId>();
            foreach (var record in _instances.Values)
            {
                if (HasUnassignedRequiredReference(record)) result.Add(record.Id);
            }

            result.Sort();
            return result;
        }
    }

    /// <summary>
    /// Stable-boundary guard: refuses to publish a store that still holds an open construction.
    ///
    /// A construction process is the unit in which an instance is built — one host command
    /// (<c>Session.Start/Submit/Answer/Step</c>) or one <see cref="RunAtomic{T}"/> call for direct store use. It may
    /// span several instructions, so "assign the reference in the next instruction" is legal; it must not span a
    /// stable boundary, because a decision, a terminal result, a projection and a state hash are all read by a host
    /// that cannot tell "not assigned yet" from "assigned to nothing".
    /// </summary>
    /// <param name="publication">What is about to publish the state, used in the fault message.</param>
    public void RequireStableState(string publication)
    {
        var pending = UnderConstruction;
        if (pending.Count == 0) return;

        var described = string.Join("; ", pending.Take(4).Select(DescribeConstruction));
        if (pending.Count > 4) described += $"; … {pending.Count - 4} more";
        throw Faults.RequiredRef(
            $"{pending.Count} instance(s) are still under construction and cannot be published by {publication}: " +
            $"{described}. A non-optional reference may hold none only while the construction process that created " +
            "the instance is still open; assign every required reference before the first stable decision, terminal " +
            "result, projection or state hash.");
    }

    private string DescribeConstruction(InstanceId instance)
    {
        var record = _instances[instance.Value];
        var type = _ruleSet.Type(record.TypeId);
        var missing = new List<string>();
        for (var index = 0; index < type.Fields.Count; index++)
        {
            if (RequiresReference(type.Fields[index]) && IsUnassignedReference(record.Values[index]))
                missing.Add(type.Fields[index].Name);
        }

        return $"{instance} of '{type.QualifiedId}' still needs {string.Join("/", missing)}";
    }

    private int NextScopeId { get; set; }

    private int NextInstanceId { get; set; }

    // -- scopes -------------------------------------------------------------------------------------------------

    public bool ScopeExists(ScopeId scope) => _scopes.ContainsKey(scope.Value);

    public int ScopeTypeId(ScopeId scope) => RequireScope(scope).ScopeTypeId;

    public ScopeId ParentScope(ScopeId scope) => RequireScope(scope).Parent;

    /// <summary>Child scopes in creation order (stable).</summary>
    public IReadOnlyList<ScopeId> ChildScopes(ScopeId scope) => RequireScope(scope).Children;

    /// <summary>Creates a child scope. The declared parent scope type must match the parent scope's type.</summary>
    public ScopeId CreateScope(int scopeTypeId, ScopeId parent)
    {
        var parentRecord = RequireScope(parent);
        var definition = _ruleSet.ScopeType(scopeTypeId);
        if (definition.ParentScopeTypeId >= 0 && definition.ParentScopeTypeId != parentRecord.ScopeTypeId)
        {
            throw Faults.Schema(
                $"scope type '{definition.QualifiedId}' must be created inside " +
                $"'{_ruleSet.ScopeType(definition.ParentScopeTypeId).QualifiedId}' but the parent scope is " +
                $"{(parentRecord.ScopeTypeId < 0 ? "the root scope" : $"'{_ruleSet.ScopeType(parentRecord.ScopeTypeId).QualifiedId}'")}");
        }

        if (definition.ParentScopeTypeId < 0 && parent != RootScope)
        {
            throw Faults.Schema(
                $"scope type '{definition.QualifiedId}' is a root scope type and can only be created at the root");
        }

        var id = new ScopeId(NextScopeId++);
        var record = new ScopeRecord(id, scopeTypeId, parent);
        _scopes.Add(id.Value, record);
        parentRecord.Children.Add(id);
        Record(() =>
        {
            parentRecord.Children.Remove(id);
            _scopes.Remove(id.Value);
        });
        Version++;
        return id;
    }

    /// <summary>Destroys a scope, its whole subtree, every instance inside it, and all of their memberships.</summary>
    public void DestroyScope(ScopeId scope)
    {
        var record = RequireScope(scope);
        if (scope == RootScope) throw Faults.Schema("the root scope cannot be destroyed");

        foreach (var child in record.Children.ToArray()) DestroyScope(child);
        foreach (var instance in InstancesInScope(scope).ToArray()) DestroyInstance(instance);

        // A scope may still hold scope-less instances of the session; they are not affected.
        foreach (var instance in _instances.Values.ToArray())
        {
            if (instance.Scope == scope) DestroyInstance(instance.Id);
        }

        Record(() =>
        {
            _scopes[scope.Value] = record;
            RequireScope(record.Parent).Children.Add(scope);
        });
        _scopes.Remove(scope.Value);
        RequireScope(record.Parent).Children.Remove(scope);
        Version++;
    }

    // -- instances ----------------------------------------------------------------------------------------------

    public bool Exists(InstanceId instance) => _instances.ContainsKey(instance.Value);

    public int TypeIdOf(InstanceId instance) => RequireInstance(instance).TypeId;

    public ScopeId ScopeOf(InstanceId instance) => RequireInstance(instance).Scope;

    /// <summary>Live instances of one type in a scope, in creation (ascending identity) order.</summary>
    public IReadOnlyList<InstanceId> InstancesInScope(ScopeId scope, int typeId = -1)
    {
        RequireScope(scope);
        var result = new List<InstanceId>();
        foreach (var record in _instances.Values)
        {
            if (record.Scope != scope) continue;
            if (typeId >= 0 && record.TypeId != typeId) continue;
            result.Add(record.Id);
        }

        result.Sort();
        return result;
    }

    /// <summary>All live instances of one type, in creation order. <paramref name="typeId"/> -1 means every type.</summary>
    public IReadOnlyList<InstanceId> AllInstances(int typeId = -1)
    {
        if (typeId >= 0) return _instancesByType.TryGetValue(typeId, out var instances) ? instances.ToArray() : [];
        return _instances.Values.Select(record => record.Id).Order().ToArray();
    }

    public InstanceId CreateInstance(int typeId, ScopeId scope)
    {
        RequireScope(scope);
        var definition = _ruleSet.Type(typeId);
        var id = new InstanceId(NextInstanceId++);
        var values = new Value[definition.Fields.Count];
        for (var index = 0; index < values.Length; index++) values[index] = definition.Fields[index].DefaultValue;
        var record = new InstanceRecord(id, typeId, scope, values);
        _instances.Add(id.Value, record);
        if (!_instancesByType.TryGetValue(typeId, out var typed)) _instancesByType[typeId] = typed = [];
        typed.Add(id);
        Record(() => { _instances.Remove(id.Value); typed.Remove(id); });
        Version++;
        return id;
    }

    /// <summary>
    /// Destroys an instance: memberships and relations are removed, inbound reference fields are cleared, and the
    /// identity is never reused. A non-optional inbound reference makes the operation fail.
    /// </summary>
    public void DestroyInstance(InstanceId instance)
    {
        var record = RequireInstance(instance);
        var typeId = record.TypeId;

        foreach (var collection in _collections.Values)
        {
            if (!collection.Contains(instance)) continue;
            var copy = collection.ToArray();
            Record(() => RestoreList(collection, copy));
            collection.Remove(instance);
        }

        foreach (var (relationId, byOwner) in _relations)
        {
            foreach (var (ownerValue, targets) in byOwner.ToArray())
            {
                var mentioned = targets.Contains(instance) ||
                                new InstanceId(ownerValue) == instance;
                if (!mentioned) continue;
                var copy = targets.ToArray();
                var owner = new InstanceId(ownerValue);
                Record(() => RestoreRelation(relationId, owner, copy));
                targets.Remove(instance);
                if (owner == instance) byOwner.Remove(ownerValue);
            }
        }

        if (_referrerTypes.TryGetValue(typeId, out var referrerTypes))
        foreach (var other in _instances.Values.Where(other => referrerTypes.Contains(other.TypeId)))
        {
            if (other.Id == instance) continue;
            var otherType = _ruleSet.Type(other.TypeId);
            for (var index = 0; index < otherType.Fields.Count; index++)
            {
                var field = otherType.Fields[index];
                if (field.Kind != FieldTypeKind.Ref || field.TargetTypeId != typeId) continue;
                if (other.Values[index].Kind != ValueKind.Ref || other.Values[index].AsRef() != instance) continue;
                if (!field.Optional)
                {
                    throw Faults.Of(EngineFaultCode.InvalidReference,
                        $"destroying {instance} would clear non-optional field '{otherType.QualifiedId}." +
                        $"{field.Name}' of {other.Id}");
                }

                var previous = other.Values[index];
                var target = other;
                Record(() => target.Values[index] = previous);
                other.Values[index] = Value.None;
            }
        }

        _instances.Remove(instance.Value);
        var typed = _instancesByType[typeId];
        typed.Remove(instance);
        Record(() => { _instances[instance.Value] = record; typed.Add(instance); });
        Version++;
    }

    public Value GetField(InstanceId instance, int fieldIndex)
    {
        var record = RequireInstance(instance);
        return record.Values[RequireFieldIndex(record, fieldIndex, "read")];
    }

    /// <summary>
    /// Writes a gameplay field after validating its declared type, bounds and reference target. A non-optional
    /// reference field accepts <see cref="Value.None"/> only while the instance is still under construction; once the
    /// last required reference is assigned the window closes and clearing it again is a type error.
    /// </summary>
    public void SetField(InstanceId instance, int fieldIndex, Value value)
    {
        var record = RequireInstance(instance);
        var index = RequireFieldIndex(record, fieldIndex, "write");
        var field = _ruleSet.Type(record.TypeId).Fields[index];
        Validate(field, value, instance);
        var previous = record.Values[index];
        Record(() => record.Values[index] = previous);
        record.Values[index] = value;
        Version++;
    }

    // -- collections --------------------------------------------------------------------------------------------

    /// <summary>
    /// Members in the declared order, as a <b>snapshot</b>: a query must not observe later mutations, and a loop that
    /// moves cards between collections would otherwise iterate a shifting list. Unordered collections are exposed in
    /// ascending instance identity.
    /// </summary>
    public IReadOnlyList<InstanceId> CollectionMembers(int collectionId)
    {
        var members = RequireCollection(collectionId);
        var snapshot = members.ToArray();
        if (_ruleSet.Collection(collectionId).Ordered) return snapshot;
        Array.Sort(snapshot);
        return snapshot;
    }

    public bool IsMember(int collectionId, InstanceId instance) => RequireCollection(collectionId).Contains(instance);

    public int CollectionIndexOf(int collectionId, InstanceId instance) =>
        RequireCollection(collectionId).IndexOf(instance);

    public void AddToCollection(int collectionId, InstanceId instance, InsertPosition position = InsertPosition.End,
        int? index = null)
    {
        var definition = _ruleSet.Collection(collectionId);
        var members = RequireCollection(collectionId);
        RequireInstance(instance);
        if (!IsElementOf(definition, instance))
        {
            throw Faults.TypeError(
                $"collection '{definition.QualifiedId}' holds " +
                $"'{_ruleSet.Type(definition.ElementTypeId).QualifiedId}' but {instance} is " +
                $"'{_ruleSet.Type(TypeIdOf(instance)).QualifiedId}'");
        }

        if (members.Contains(instance))
            throw Faults.Of(EngineFaultCode.MembershipConflict,
                $"{instance} is already a member of '{definition.QualifiedId}'");

        if (definition.MembershipGroup is not null)
        {
            foreach (var siblingId in _membershipGroups[definition.MembershipGroup])
            {
                if (siblingId == collectionId) continue;
                if (!_collections[siblingId].Contains(instance)) continue;
                throw Faults.Of(EngineFaultCode.MembershipConflict,
                    $"{instance} is already a member of '{_ruleSet.Collection(siblingId).QualifiedId}', which " +
                    $"shares membership group '{definition.MembershipGroup}' with '{definition.QualifiedId}'");
            }
        }

        if (members.Count >= definition.Capacity)
        {
            throw Faults.Of(EngineFaultCode.CapacityExceeded,
                $"collection '{definition.QualifiedId}' is at capacity {definition.Capacity}");
        }

        var insertAt = ResolveIndex(members.Count, position, index, definition.QualifiedId);
        Record(() => members.Remove(instance));
        members.Insert(insertAt, instance);
        Version++;
    }

    public void RemoveFromCollection(int collectionId, InstanceId instance)
    {
        var definition = _ruleSet.Collection(collectionId);
        var members = RequireCollection(collectionId);
        var index = members.IndexOf(instance);
        if (index < 0)
        {
            throw Faults.Of(EngineFaultCode.MembershipConflict,
                $"{instance} is not a member of '{definition.QualifiedId}'");
        }

        Record(() => members.Insert(index, instance));
        members.RemoveAt(index);
        Version++;
    }

    public void MoveInCollection(int collectionId, InstanceId instance, InsertPosition position = InsertPosition.End,
        int? index = null)
    {
        var definition = _ruleSet.Collection(collectionId);
        var members = RequireCollection(collectionId);
        if (!definition.Ordered)
        {
            throw Faults.Schema(
                $"collection '{definition.QualifiedId}' is unordered; moving inside it has no defined meaning");
        }

        var current = members.IndexOf(instance);
        if (current < 0)
        {
            throw Faults.Of(EngineFaultCode.MembershipConflict,
                $"{instance} is not a member of '{definition.QualifiedId}'");
        }

        var copy = members.ToArray();
        Record(() => RestoreList(members, copy));
        members.RemoveAt(current);
        var insertAt = ResolveIndex(members.Count, position, index, definition.QualifiedId);
        members.Insert(insertAt, instance);
        Version++;
    }

    public void ShuffleCollection(int collectionId, RandomService random, string stream)
    {
        ArgumentNullException.ThrowIfNull(random);
        var definition = _ruleSet.Collection(collectionId);
        if (!definition.Ordered)
        {
            throw Faults.Schema(
                $"collection '{definition.QualifiedId}' is unordered; shuffling it has no defined meaning");
        }

        var members = RequireCollection(collectionId);
        var copy = members.ToArray();
        Record(() => RestoreList(members, copy));
        random.Shuffle(stream, members);
        Version++;
    }

    // -- relations ----------------------------------------------------------------------------------------------

    /// <summary>Relation targets as a snapshot, so a reaction that rewrites the relation cannot disturb a loop.</summary>
    public IReadOnlyList<InstanceId> RelationTargets(int relationId, InstanceId owner)
    {
        var byOwner = RequireRelations(relationId);
        return byOwner.TryGetValue(owner.Value, out var targets) ? targets.ToArray() : [];
    }

    public void AddRelation(int relationId, InstanceId owner, InstanceId target)
    {
        var definition = _ruleSet.Relation(relationId);
        RequireInstance(owner);
        RequireInstance(target);
        if (TypeIdOf(target) != definition.TargetTypeId)
        {
            throw Faults.TypeError(
                $"relation '{definition.QualifiedId}' targets " +
                $"'{_ruleSet.Type(definition.TargetTypeId).QualifiedId}' but {target} is " +
                $"'{_ruleSet.Type(TypeIdOf(target)).QualifiedId}'");
        }

        var byOwner = RequireRelations(relationId);
        var targets = byOwner.TryGetValue(owner.Value, out var existing) ? existing : null;
        if (targets is not null && targets.Contains(target)) return;
        if (!definition.Many && targets is { Count: > 0 })
        {
            throw Faults.Of(EngineFaultCode.MembershipConflict,
                $"relation '{definition.QualifiedId}' is single-valued and {owner} already targets {targets[0]}");
        }

        if (targets is null)
        {
            targets = [];
            byOwner.Add(owner.Value, targets);
            var ownerKey = owner.Value;
            Record(() => byOwner.Remove(ownerKey));
        }
        else
        {
            Record(() => targets.Remove(target));
        }

        targets.Add(target);
        Version++;
    }

    public void RemoveRelation(int relationId, InstanceId owner, InstanceId target)
    {
        var byOwner = RequireRelations(relationId);
        if (!byOwner.TryGetValue(owner.Value, out var targets) || !targets.Contains(target))
        {
            throw Faults.Of(EngineFaultCode.MembershipConflict,
                $"{owner} has no '{_ruleSet.Relation(relationId).QualifiedId}' relation to {target}");
        }

        var copy = targets.ToArray();
        Record(() => RestoreRelation(relationId, owner, copy));
        targets.Remove(target);
        if (targets.Count == 0) byOwner.Remove(owner.Value);
        Version++;
    }

    public void ClearRelation(int relationId, InstanceId owner)
    {
        var byOwner = RequireRelations(relationId);
        if (!byOwner.TryGetValue(owner.Value, out var targets)) return;
        var copy = targets.ToArray();
        Record(() => RestoreRelation(relationId, owner, copy));
        byOwner.Remove(owner.Value);
        Version++;
    }

    // -- atomicity ----------------------------------------------------------------------------------------------

    /// <summary>
    /// Applies one command atomically. If <paramref name="command"/> throws, the store — values, memberships,
    /// relations and <see cref="Version"/> — is restored to the exact pre-command state and the fault is rethrown.
    /// Nested calls join the outer command.
    /// </summary>
    public void RunAtomic(Action command)
    {
        ArgumentNullException.ThrowIfNull(command);
        RunAtomic<object?>(() =>
        {
            command();
            return null;
        });
    }

    /// <summary>Result-returning form of <see cref="RunAtomic(Action)"/>.</summary>
    public T RunAtomic<T>(Func<T> command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var outermost = _journal is null;
        if (outermost) _journal = [];
        var mark = _journal!.Count;
        var version = Version;
        T result;
        try
        {
            result = command();
        }
        catch
        {
            for (var i = _journal.Count - 1; i >= mark; i--) _journal[i]();
            _journal.RemoveRange(mark, _journal.Count - mark);
            Version = version;
            if (outermost) _journal = null;
            throw;
        }

        if (outermost) _journal = null;
        return result;
    }

    /// <summary>Deep copy. Two sessions built from one rule set never share storage.</summary>
    public SessionState Clone()
    {
        var clone = new SessionState(_ruleSet);
        clone._scopes.Clear();
        foreach (var (key, value) in _scopes)
            clone._scopes.Add(key, new ScopeRecord(value.Id, value.ScopeTypeId, value.Parent, value.Children));

        clone._instances.Clear();
        foreach (var (key, value) in _instances)
        {
            clone._instances.Add(key,
                new InstanceRecord(value.Id, value.TypeId, value.Scope, (Value[])value.Values.Clone()));
        }

        foreach (var record in clone._instances.Values)
        {
            if (!clone._instancesByType.TryGetValue(record.TypeId, out var typed))
                clone._instancesByType[record.TypeId] = typed = [];
            typed.Add(record.Id);
        }

        clone._collections.Clear();
        foreach (var (key, value) in _collections) clone._collections.Add(key, [.. value]);

        clone._relations.Clear();
        foreach (var (key, value) in _relations)
        {
            var copy = new Dictionary<int, List<InstanceId>>();
            foreach (var (owner, targets) in value) copy.Add(owner, [.. targets]);
            clone._relations.Add(key, copy);
        }

        clone.Version = Version;
        clone.NextInstanceId = NextInstanceId;
        clone.NextScopeId = NextScopeId;
        return clone;
    }

    /// <summary>
    /// Deterministic content hash over live scopes, instances, gameplay fields, memberships and relations. It covers
    /// only <see cref="FieldLifetime.Persistent"/> fields and excludes <see cref="Version"/>, so it is a pure function
    /// of the play state.
    ///
    /// It is a published snapshot, so an open construction (a non-optional reference without a live target) makes it
    /// fail with <see cref="EngineFaultCode.RequiredRefUnassigned"/> instead of hashing a state a host could not act
    /// on. <see cref="Clone"/> is the one exception: it copies an open construction without publishing it, and the
    /// clone keeps the same obligation.
    /// </summary>
    public string StateHash()
    {
        RequireStableState("the state hash");
        var text = new StringBuilder();
        text.Append("rules=").Append(_ruleSet.Identity).Append('\n');

        var scopes = _scopes.Values.OrderBy(s => s.Id).ToArray();
        foreach (var scope in scopes)
        {
            text.Append("scope=").Append(scope.Id.Value).Append(':').Append(scope.ScopeTypeId)
                .Append("<-").Append(scope.Parent.Value).Append('\n');
        }

        var instances = _instances.Values.OrderBy(i => i.Id).ToArray();
        foreach (var instance in instances)
        {
            var type = _ruleSet.Type(instance.TypeId);
            text.Append("inst=").Append(instance.Id.Value).Append(':').Append(type.QualifiedId)
                .Append('@').Append(instance.Scope.Value);
            for (var index = 0; index < type.Fields.Count; index++)
            {
                if (type.Fields[index].Lifetime != FieldLifetime.Persistent) continue;
                text.Append(' ').Append(index).Append('=').Append(Encode(instance.Values[index]));
            }

            text.Append('\n');
        }

        foreach (var collection in _ruleSet.Collections)
        {
            text.Append("coll=").Append(collection.QualifiedId).Append('[');
            foreach (var member in _collections[collection.Id]) text.Append(member.Value).Append(',');
            text.Append("]\n");
        }

        foreach (var relation in _ruleSet.Relations)
        {
            text.Append("rel=").Append(relation.QualifiedId).Append('{');
            foreach (var (owner, targets) in _relations[relation.Id].OrderBy(pair => pair.Key))
            {
                text.Append(owner).Append("->");
                foreach (var target in targets.OrderBy(t => t.Value)) text.Append(target.Value).Append(',');
                text.Append(';');
            }

            text.Append("}\n");
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())))
            .ToLowerInvariant()[..16];
    }

    // -- internals ----------------------------------------------------------------------------------------------

    private static string Encode(Value value) => value.Kind switch
    {
        ValueKind.None => "-",
        ValueKind.Bool => value.AsBool() ? "T" : "F",
        ValueKind.Int => value.AsInt().ToString(CultureInfo.InvariantCulture),
        ValueKind.Fixed => value.AsDecimal().ToString(CultureInfo.InvariantCulture),
        ValueKind.Enum => value.AsEnumOrdinal().ToString(CultureInfo.InvariantCulture),
        ValueKind.String => "'" + value.AsString() + "'",
        ValueKind.Ref => value.AsRef().Value.ToString(CultureInfo.InvariantCulture),
        ValueKind.Participant => value.AsParticipant().Value.ToString(CultureInfo.InvariantCulture),
        ValueKind.Scope => value.AsScope().Value.ToString(CultureInfo.InvariantCulture),
        ValueKind.RefList => "[" + string.Join(",", value.AsRefList().Select(r => r.Value)) + "]",
        ValueKind.IntList => "[" + string.Join(",", value.AsIntList()) + "]",
        _ => "?",
    };

    private void Validate(FieldDefinition field, Value value, InstanceId owner)
    {
        if (value.Kind == ValueKind.None)
        {
            if (AllowsUnassignedReference(field, owner)) return;
            throw Faults.TypeError(
                $"field '{field.Name}' of {owner} is not optional and cannot hold none outside its construction window");
        }

        switch (field.Kind)
        {
            case FieldTypeKind.Any:
                return;

            case FieldTypeKind.Bool:
                if (value.Kind != ValueKind.Bool) Mismatch(field, value, owner);
                return;

            case FieldTypeKind.Int:
            {
                if (value.Kind != ValueKind.Int) Mismatch(field, value, owner);
                var number = value.AsInt();
                if (number < field.Min || number > field.Max)
                {
                    throw Faults.Range(
                        $"field '{field.Name}' of {owner} is bounded to [{field.Min}, {field.Max}] but received {number}");
                }

                return;
            }

            case FieldTypeKind.Fixed:
            {
                if (value.Kind != ValueKind.Fixed) Mismatch(field, value, owner);
                var number = value.AsDecimal();
                if (number < field.FixedMin || number > field.FixedMax)
                {
                    throw Faults.Range(
                        $"field '{field.Name}' of {owner} is bounded to [{field.FixedMin}, {field.FixedMax}] but " +
                        $"received {number}");
                }

                return;
            }

            case FieldTypeKind.Enum:
            {
                if (value.Kind != ValueKind.Enum) Mismatch(field, value, owner);
                var ordinal = value.AsEnumOrdinal();
                var definition = _ruleSet.Enums[field.EnumTypeId];
                definition.NameOf(ordinal);
                return;
            }

            case FieldTypeKind.String:
            {
                if (value.Kind != ValueKind.String) Mismatch(field, value, owner);
                if (value.AsString().Length > field.MaxLength)
                {
                    throw Faults.Range(
                        $"field '{field.Name}' of {owner} allows {field.MaxLength} characters but received " +
                        $"{value.AsString().Length}");
                }

                return;
            }

            case FieldTypeKind.Ref:
            {
                if (value.Kind != ValueKind.Ref)
                {
                    // The explicit "no reference" spelling is the same thing as none for this field.
                    if (AllowsUnassignedReference(field, owner)) return;
                    Mismatch(field, value, owner);
                }

                var reference = value.AsRef();
                if (reference.IsNone)
                {
                    if (AllowsUnassignedReference(field, owner)) return;
                    throw Faults.TypeError(
                        $"field '{field.Name}' of {owner} is a non-optional reference and cannot be cleared " +
                        "outside its construction window");
                }

                if (!_instances.TryGetValue(reference.Value, out var target))
                {
                    throw Faults.Of(EngineFaultCode.InvalidReference,
                        $"field '{field.Name}' of {owner} references {reference}, which does not exist");
                }

                if (field.TargetTypeId >= 0 && target.TypeId != field.TargetTypeId)
                {
                    throw Faults.Of(EngineFaultCode.InvalidReference,
                        $"field '{field.Name}' of {owner} references '{_ruleSet.Type(target.TypeId).QualifiedId}' " +
                        $"but declares '{_ruleSet.Type(field.TargetTypeId).QualifiedId}'");
                }

                return;
            }

            case FieldTypeKind.Participant:
                if (value.Kind != ValueKind.Participant) Mismatch(field, value, owner);
                return;

            case FieldTypeKind.List:
            {
                var element = field.Element ?? throw Faults.Schema($"field '{field.Name}' has no list element type");
                if (element.Kind == FieldTypeKind.Int)
                {
                    if (value.Kind != ValueKind.IntList) Mismatch(field, value, owner);
                    var list = value.AsIntList();
                    if (list.Count > field.MaxLength)
                        throw Faults.Range($"field '{field.Name}' of {owner} allows {field.MaxLength} elements");
                    foreach (var item in list)
                    {
                        if (item < element.Min || item > element.Max)
                        {
                            throw Faults.Range(
                                $"field '{field.Name}' of {owner} bounds its elements to [{element.Min}, " +
                                $"{element.Max}] but received {item}");
                        }
                    }

                    return;
                }

                if (element.Kind == FieldTypeKind.Ref)
                {
                    if (value.Kind != ValueKind.RefList) Mismatch(field, value, owner);
                    var list = value.AsRefList();
                    if (list.Count > field.MaxLength)
                        throw Faults.Range($"field '{field.Name}' of {owner} allows {field.MaxLength} elements");
                    foreach (var item in list)
                    {
                        if (item.IsNone && (field.Optional || element.Kind == FieldTypeKind.Ref)) continue;
                        if (!_instances.TryGetValue(item.Value, out var target))
                        {
                            throw Faults.Of(EngineFaultCode.InvalidReference,
                                $"field '{field.Name}' of {owner} references {item}, which does not exist");
                        }

                        if (field.ElementTargetTypeId >= 0 && target.TypeId != field.ElementTargetTypeId)
                        {
                            throw Faults.Of(EngineFaultCode.InvalidReference,
                                $"field '{field.Name}' of {owner} references " +
                                $"'{_ruleSet.Type(target.TypeId).QualifiedId}' but declares elements of " +
                                $"'{_ruleSet.Type(field.ElementTargetTypeId).QualifiedId}'");
                        }
                    }

                    return;
                }

                throw Faults.Schema($"field '{field.Name}' declares an unsupported list element kind {element.Kind}");
            }

            default:
                throw Faults.Schema($"field '{field.Name}' declares an unsupported kind {field.Kind}");
        }
    }

    private static void Mismatch(FieldDefinition field, Value value, InstanceId owner) =>
        throw Faults.TypeError(
            $"field '{field.Name}' of {owner} is declared {field.Kind} but received a {value.Kind} value");

    /// <summary>A field that carries a construction obligation: an entity-declared, non-optional reference.</summary>
    private static bool RequiresReference(FieldDefinition field) =>
        field.Kind == FieldTypeKind.Ref && !field.Optional;

    /// <summary>An unset reference in either spelling: <see cref="Value.None"/> or an explicit <c>ref &lt;none&gt;</c>.</summary>
    private static bool IsUnassignedReference(Value value) =>
        value.Kind == ValueKind.None || (value.Kind == ValueKind.Ref && value.AsRef().IsNone);

    /// <summary>
    /// True when a field is allowed to hold no reference right now. An optional field always is. A required
    /// reference is allowed to be unset only while the instance is still under construction, i.e. while some required
    /// reference of the same instance has not been assigned yet; that window closes as soon as the last one is
    /// assigned. The answer is derived from the stored values, so a rolled-back command cannot leave a stale window.
    /// </summary>
    private bool AllowsUnassignedReference(FieldDefinition field, InstanceId owner)
    {
        if (field.Optional) return true;
        if (field.Kind == FieldTypeKind.Any) return true;
        if (field.Kind != FieldTypeKind.Ref) return false;
        return _instances.TryGetValue(owner.Value, out var record) && HasUnassignedRequiredReference(record);
    }

    private bool HasUnassignedRequiredReference(InstanceRecord record)
    {
        var type = _ruleSet.Type(record.TypeId);
        for (var index = 0; index < type.Fields.Count; index++)
        {
            if (RequiresReference(type.Fields[index]) && IsUnassignedReference(record.Values[index])) return true;
        }

        return false;
    }

    private bool IsElementOf(CollectionDefinition collection, InstanceId instance) =>
        TypeIdOf(instance) == collection.ElementTypeId;

    private static int ResolveIndex(int count, InsertPosition position, int? index, string owner) => position switch
    {
        InsertPosition.Start => 0,
        InsertPosition.End => count,
        InsertPosition.AtIndex => index is null
            ? throw Faults.Schema($"collection '{owner}' needs an explicit index for an at-index insert")
            : index < 0 || index > count
                ? throw Faults.Range($"collection '{owner}' insert index {index} is outside 0..{count}")
                : index.Value,
        _ => throw Faults.Schema($"collection '{owner}' declares an unknown insert position {position}"),
    };

    private ScopeRecord RequireScope(ScopeId scope) =>
        _scopes.TryGetValue(scope.Value, out var record)
            ? record
            : throw Faults.Of(EngineFaultCode.UnknownEntity, $"scope {scope} does not exist");

    private InstanceRecord RequireInstance(InstanceId instance) =>
        _instances.TryGetValue(instance.Value, out var record)
            ? record
            : throw Faults.Of(EngineFaultCode.UnknownEntity, $"instance {instance} does not exist");

    private int RequireFieldIndex(InstanceRecord record, int fieldIndex, string operation)
    {
        var type = _ruleSet.Type(record.TypeId);
        if (fieldIndex < 0 || fieldIndex >= type.Fields.Count)
        {
            throw Faults.Range($"field index {fieldIndex} is outside '{type.QualifiedId}' ({operation})");
        }

        return fieldIndex;
    }

    private List<InstanceId> RequireCollection(int collectionId)
    {
        if (collectionId < 0 || collectionId >= _ruleSet.Collections.Count)
            throw Faults.Unresolved($"collection id {collectionId} is not part of the rule set");
        return _collections[collectionId];
    }

    private Dictionary<int, List<InstanceId>> RequireRelations(int relationId)
    {
        if (relationId < 0 || relationId >= _ruleSet.Relations.Count)
            throw Faults.Unresolved($"relation id {relationId} is not part of the rule set");
        return _relations[relationId];
    }

    private void RestoreList(List<InstanceId> target, InstanceId[] copy)
    {
        target.Clear();
        target.AddRange(copy);
    }

    private void RestoreRelation(int relationId, InstanceId owner, InstanceId[] copy)
    {
        _relations[relationId][owner.Value] = [.. copy];
    }

    private void Record(Action undo) => _journal?.Add(undo);

    private sealed class ScopeRecord(ScopeId id, int scopeTypeId, ScopeId parent,
        IEnumerable<ScopeId>? children = null)
    {
        internal ScopeId Id { get; } = id;

        internal int ScopeTypeId { get; } = scopeTypeId;

        internal ScopeId Parent { get; } = parent;

        internal List<ScopeId> Children { get; } = children is null ? [] : [.. children];
    }

    private sealed class InstanceRecord(InstanceId id, int typeId, ScopeId scope, Value[] values)
    {
        internal InstanceId Id { get; } = id;

        internal int TypeId { get; } = typeId;

        internal ScopeId Scope { get; } = scope;

        internal Value[] Values { get; } = values;
    }
}
