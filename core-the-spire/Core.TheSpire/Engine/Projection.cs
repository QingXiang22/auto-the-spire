namespace CoreTheSpire.Engine;

/// <summary>One field as a viewer may see it. Values are copies; nothing here aliases live storage.</summary>
public sealed record ProjectedField(string Name, Value Value, FieldVisibility Visibility);

public sealed record ProjectedInstance(
    InstanceId Id,
    string TypeId,
    int PackageIndex,
    ScopeId Scope,
    IReadOnlyList<ProjectedField> Fields);

/// <summary>
/// A collection as a viewer may see it. <see cref="IdentityVisible"/> false hides the full membership,
/// <see cref="OrderVisible"/> false reports the members in ascending identity order instead of the internal order,
/// and <see cref="Size"/> is null when the size is not visible. <see cref="RevealedMembers"/> is the subset of the
/// membership this viewer is explicitly authorized to identify (see <see cref="ProjectionSymbol.Reveals"/>); it is
/// empty when nothing was authorized, and it never turns <see cref="IdentityVisible"/> into a completeness claim.
///
/// <see cref="Capacity"/> is the pack's **declared** membership limit (<see cref="CollectionSymbol.Capacity"/>),
/// which is a rule rather than hidden state: the engine already rejects the next insert at that limit
/// (<c>StateStore</c> raises <see cref="EngineFaultCode.CapacityExceeded"/>). It is reported verbatim;
/// <see cref="int.MaxValue"/> means "no declared limit". A host that needs to publish a limit must read it from
/// here instead of repeating the number.
/// </summary>
public sealed record ProjectedCollection(
    string QualifiedId,
    int? Size,
    IReadOnlyList<InstanceId> Members,
    bool IdentityVisible,
    bool SizeVisible,
    bool OrderVisible,
    IReadOnlyList<InstanceId> RevealedMembers,
    int Capacity);

public sealed record ProjectedRelation(string QualifiedId, InstanceId Owner, IReadOnlyList<InstanceId> Targets);

/// <summary>Immutable participant view. Producing one is a pure read: it never changes state, version or randomness.</summary>
public sealed record ParticipantView(
    ParticipantId Viewer,
    string ProjectionId,
    long StateVersion,
    IReadOnlyList<ProjectedInstance> Instances,
    IReadOnlyList<ProjectedCollection> Collections,
    IReadOnlyList<ProjectedRelation> Relations,
    bool IsTerminal,
    string? TerminalResultId);

/// <summary>
/// Participant projection (K-06).
///
/// The game pack decides what exists publicly: only entity types listed in the projection are visible at all, only
/// <see cref="FieldVisibility.Public"/> fields are visible to everyone, and an <see cref="FieldVisibility.OwnerPrivate"/>
/// field is visible only to the participant the entity names in its owner field. <see cref="FieldVisibility.Hidden"/>
/// fields become visible only when the projection sets <c>IncludeHiddenFields</c>. A collection hides its identity,
/// size or order independently, and hiding the order reports ascending identities so no internal order leaks.
///
/// "Known" information (for example a card the player has legitimately seen) is <em>not</em> engine knowledge: the
/// content pack declares a field for it and the pack keeps it correct. The engine never infers knowledge from history.
/// A pack may additionally declare which facts authorize which viewer to identify which instances
/// (<see cref="ProjectionSymbol.Reveals"/>); that authorization only lifts the identity hiding of the referenced
/// instances for the named viewer, never the field rules, and it is part of the rule set identity.
/// </summary>
public static class ProjectionObserver
{
    /// <summary>Pure read: repeated calls return equal views and leave state, version and randomness untouched.</summary>
    public static ParticipantView Observe(SessionState state, ParticipantId viewer, ProjectionDefinition projection,
        TerminalOutcome? terminal = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(projection);

        var revealed = CollectRevealed(state, viewer, projection);

        // An instance reachable through an area whose identity is hidden is not projected: listing it would leak that
        // area's contents and, with them, its internal order. A pack fact that names this viewer authorizes exactly
        // the instances it references and nothing else; a type outside the projection is never identifiable.
        var identifiable = new HashSet<InstanceId>();
        foreach (var (typeId, type) in projection.Types)
        {
            if (!type.Include) continue;
            foreach (var instance in state.AllInstances(typeId))
                if (!IsIdentityHidden(state, instance, revealed)) identifiable.Add(instance);
        }

        var instances = new List<ProjectedInstance>();
        foreach (var (typeId, type) in projection.Types.OrderBy(pair => pair.Key))
        {
            if (!type.Include) continue;
            foreach (var instance in state.AllInstances(typeId))
            {
                if (!identifiable.Contains(instance)) continue;
                var fields = new List<ProjectedField>();
                for (var index = 0; index < type.Type.Fields.Count; index++)
                {
                    var field = type.Type.Fields[index];
                    if (!IsVisible(state, type, instance, index, field.Visibility, viewer)) continue;
                    // A reference is only publishable when this viewer can identify its target; otherwise the value
                    // would name an instance the viewer may not know about.
                    if (!IsIdentifiable(state, instance, index, identifiable)) continue;
                    fields.Add(new ProjectedField(field.Name, state.GetField(instance, index), field.Visibility));
                }

                instances.Add(new ProjectedInstance(instance, type.Type.QualifiedId, type.Type.PackageIndex,
                    state.ScopeOf(instance), fields));
            }
        }

        var collections = new List<ProjectedCollection>();
        if (projection.IncludeCollections)
        {
            foreach (var collection in state.RuleSetCollections)
            {
                var members = state.CollectionMembers(collection.Id);
                var visible = collection.IdentityVisible
                    ? collection.OrderVisible
                        ? members.ToArray()
                        : members.OrderBy(member => member.Value).ToArray()
                    : [];
                // Ascending identity, never the internal order: the revealed subset must not carry pile order.
                var authorized = revealed.Count == 0
                    ? []
                    : members.Where(revealed.Contains).OrderBy(member => member.Value).ToArray();
                collections.Add(new ProjectedCollection(collection.QualifiedId,
                    collection.SizeVisible ? members.Count : null, visible, collection.IdentityVisible,
                    collection.SizeVisible, collection.OrderVisible, authorized, collection.Capacity));
            }
        }

        var relations = new List<ProjectedRelation>();
        if (projection.IncludeRelations)
        {
            foreach (var relation in state.RuleSetRelations)
            {
                if (!relation.Public) continue;
                foreach (var owner in state.AllInstances().OrderBy(instance => instance.Value))
                {
                    // An edge is only publishable between instances this viewer can already identify; publishing a
                    // target the viewer may not know would leak the hidden area through the relation.
                    if (!identifiable.Contains(owner)) continue;
                    var targets = state.RelationTargets(relation.Id, owner)
                        .Where(identifiable.Contains)
                        .OrderBy(target => target.Value)
                        .ToArray();
                    if (targets.Length == 0) continue;
                    relations.Add(new ProjectedRelation(relation.QualifiedId, owner, targets));
                }
            }
        }

        return new ParticipantView(viewer, projection.QualifiedId, state.Version, instances, collections, relations,
            terminal is not null, terminal?.Definition.QualifiedId);
    }

    /// <summary>
    /// Instances some pack fact naming this viewer references. A fact whose viewer field holds a different
    /// participant (or no participant at all) authorizes nothing for this viewer, so no global flag can open an
    /// instance to every viewer.
    /// </summary>
    private static HashSet<InstanceId> CollectRevealed(SessionState state, ParticipantId viewer,
        ProjectionDefinition projection)
    {
        var revealed = new HashSet<InstanceId>();
        foreach (var reveal in projection.Reveals)
        {
            foreach (var authority in state.AllInstances(reveal.AuthorityTypeId))
            {
                var viewerField = state.GetField(authority, reveal.ViewerFieldIndex);
                if (viewerField.Kind != ValueKind.Participant) continue;
                if (viewerField.AsParticipant() != viewer) continue;
                foreach (var target in state.RelationTargets(reveal.RelationId, authority)) revealed.Add(target);
            }
        }

        return revealed;
    }

    /// <summary>
    /// True when the field's value may be published to this viewer: a reference must point at an instance the viewer
    /// can identify. A cleared optional reference carries no target and stays publishable.
    /// </summary>
    private static bool IsIdentifiable(SessionState state, InstanceId instance, int fieldIndex,
        HashSet<InstanceId> identifiable)
    {
        var value = state.GetField(instance, fieldIndex);
        if (value.Kind != ValueKind.Ref) return true;
        var target = value.AsRef();
        if (target.IsNone) return true;
        return identifiable.Contains(target);
    }

    private static bool IsIdentityHidden(SessionState state, InstanceId instance, HashSet<InstanceId> revealed)
    {
        if (revealed.Contains(instance)) return false;
        foreach (var collection in state.RuleSetCollections)
        {
            if (collection.IdentityVisible) continue;
            if (state.IsMember(collection.Id, instance)) return true;
        }

        return false;
    }

    private static bool IsVisible(SessionState state, TypeProjection type, InstanceId instance, int fieldIndex,
        FieldVisibility visibility, ParticipantId viewer)
    {
        switch (visibility)
        {
            case FieldVisibility.Public:
                return true;
            case FieldVisibility.Hidden:
                return type.IncludeHiddenFields;
            case FieldVisibility.OwnerPrivate:
            {
                if (type.OwnerFieldIndex < 0) return false;
                var owner = state.GetField(instance, type.OwnerFieldIndex);
                if (owner.Kind != ValueKind.Participant) return false;
                return owner.AsParticipant() == viewer;
            }
            default:
                return false;
        }
    }
}
