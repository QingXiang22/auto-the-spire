namespace CoreTheSpire.Engine;

/// <summary>Dense identifier tables produced by <see cref="RuleSetBuilder"/>; consumed by the type checker.</summary>
internal sealed class LinkedTables
{
    public Dictionary<string, int> TypeIds { get; } = new(StringComparer.Ordinal);
    public List<EntityTypeDefinition> Types { get; } = [];
    public Dictionary<string, int> EnumIds { get; } = new(StringComparer.Ordinal);
    public List<EnumTypeDefinition> Enums { get; } = [];
    public Dictionary<string, int> CollectionIds { get; } = new(StringComparer.Ordinal);
    public List<CollectionDefinition> Collections { get; } = [];
    public Dictionary<string, int> RelationIds { get; } = new(StringComparer.Ordinal);
    public List<RelationDefinition> Relations { get; } = [];
    public Dictionary<string, int> ScopeIds { get; } = new(StringComparer.Ordinal);
    public List<ScopeTypeDefinition> ScopeTypes { get; } = [];
    public Dictionary<string, int> ProcedureIds { get; } = new(StringComparer.Ordinal);
    public List<ProcedureDefinition> Procedures { get; } = [];
    public Dictionary<string, int> EventIds { get; } = new(StringComparer.Ordinal);
    public List<EventDefinition> Events { get; } = [];
    public Dictionary<string, int> TerminalIds { get; } = new(StringComparer.Ordinal);
    public List<TerminalResultDefinition> Terminals { get; } = [];
    public Dictionary<string, int> ComputeIds { get; } = new(StringComparer.Ordinal);
    public List<ComputeAssetDefinition> ComputeAssets { get; } = [];
    public Dictionary<string, int> ProjectionIds { get; } = new(StringComparer.Ordinal);
    public List<ProjectionDefinition> Projections { get; } = [];
    public List<ActionDefinition> Actions { get; } = [];
    public Dictionary<string, int> RefBases { get; } = new(StringComparer.Ordinal);
    public List<SubscriptionDefinition> Subscriptions { get; } = [];
    public List<ProcedureDefinition> SessionStart { get; } = [];
}

/// <summary>
/// Immutable, linked rule set. This is the only thing the scheduler, storage and observation layers execute
/// against; it holds the dense schema, the IR, the resolution table and the reproducible identity.
/// </summary>
public sealed class RuleSet
{
    private readonly EntityTypeDefinition[] _typesById;
    private readonly CollectionDefinition[] _collectionsById;
    private readonly RelationDefinition[] _relationsById;
    private readonly ScopeTypeDefinition[] _scopeTypesById;
    private readonly ProcedureDefinition[] _proceduresById;
    private readonly EventDefinition[] _eventsById;
    private readonly TerminalResultDefinition[] _terminalsById;
    private readonly ComputeAssetDefinition[] _computeAssetsById;
    private readonly ProjectionDefinition[] _projectionsById;
    private readonly ActionDefinition[] _actionsById;
    private readonly ProcedureDefinition[] _refBaseProcedures;
    private readonly Resolution[] _resolutions;

    internal RuleSet(string identity, IReadOnlyList<EnginePackage> packages, LinkedTables tables,
        Resolution[] resolutions, NumericProfile numericProfile)
    {
        Identity = identity;
        Packages = Array.AsReadOnly(packages.ToArray());
        NumericProfile = numericProfile;
        Types = tables.Types.AsReadOnly();
        Enums = tables.Enums.AsReadOnly();
        Collections = tables.Collections.AsReadOnly();
        Relations = tables.Relations.AsReadOnly();
        ScopeTypes = tables.ScopeTypes.AsReadOnly();
        Procedures = tables.Procedures.AsReadOnly();
        Events = tables.Events.AsReadOnly();
        TerminalResults = tables.Terminals.AsReadOnly();
        ComputeAssets = tables.ComputeAssets.AsReadOnly();
        Projections = tables.Projections.AsReadOnly();
        Actions = tables.Actions.AsReadOnly();
        Subscriptions = tables.Subscriptions.AsReadOnly();
        SessionStart = tables.SessionStart.AsReadOnly();

        _typesById = tables.Types.ToArray();
        _collectionsById = tables.Collections.ToArray();
        _relationsById = tables.Relations.ToArray();
        _scopeTypesById = tables.ScopeTypes.ToArray();
        _proceduresById = tables.Procedures.ToArray();
        _eventsById = tables.Events.ToArray();
        _terminalsById = tables.Terminals.ToArray();
        _computeAssetsById = tables.ComputeAssets.ToArray();
        _projectionsById = tables.Projections.ToArray();
        _actionsById = tables.Actions.ToArray();
        _resolutions = resolutions;
        _refBaseProcedures = tables.Procedures.ToArray();

        var byQualified = new Dictionary<string, ProcedureDefinition>(StringComparer.Ordinal);
        foreach (var procedure in tables.Procedures) byQualified[procedure.QualifiedId] = procedure;
        ProceduresByQualifiedId = new System.Collections.ObjectModel.ReadOnlyDictionary<string, ProcedureDefinition>(byQualified);
    }

    /// <summary>Reproducible rule set identity: engine version, package order, content, backends, numeric profile.</summary>
    public string Identity { get; }

    public IReadOnlyList<EnginePackage> Packages { get; }

    public NumericProfile NumericProfile { get; }

    public IReadOnlyList<EntityTypeDefinition> Types { get; }

    public IReadOnlyList<EnumTypeDefinition> Enums { get; }

    public IReadOnlyList<CollectionDefinition> Collections { get; }

    public IReadOnlyList<RelationDefinition> Relations { get; }

    public IReadOnlyList<ScopeTypeDefinition> ScopeTypes { get; }

    public IReadOnlyList<ProcedureDefinition> Procedures { get; }

    public IReadOnlyDictionary<string, ProcedureDefinition> ProceduresByQualifiedId { get; }

    public IReadOnlyList<EventDefinition> Events { get; }

    public IReadOnlyList<TerminalResultDefinition> TerminalResults { get; }

    public IReadOnlyList<ComputeAssetDefinition> ComputeAssets { get; }

    public IReadOnlyList<ProjectionDefinition> Projections { get; }

    public IReadOnlyList<ActionDefinition> Actions { get; }

    public IReadOnlyList<SubscriptionDefinition> Subscriptions { get; }

    public IReadOnlyList<ProcedureDefinition> SessionStart { get; }

    public EntityTypeDefinition Type(int id) => _typesById[id];

    public CollectionDefinition Collection(int id) => _collectionsById[id];

    public RelationDefinition Relation(int id) => _relationsById[id];

    public ScopeTypeDefinition ScopeType(int id) => _scopeTypesById[id];

    public ProcedureDefinition Procedure(int id) => _proceduresById[id];

    public EventDefinition Event(int id) => _eventsById[id];

    public TerminalResultDefinition TerminalResult(int id) => _terminalsById[id];

    public ComputeAssetDefinition ComputeAsset(int id) => _computeAssetsById[id];

    public ProjectionDefinition Projection(int id) => _projectionsById[id];

    public ActionDefinition Action(int id) => _actionsById[id];

    public Resolution Resolve(int refBase, int slot) => _resolutions[refBase + slot];

    public TypeResolution ResolveType(int refBase, int slot) => (TypeResolution)Resolve(refBase, slot);

    public FieldResolution ResolveField(int refBase, int slot) => (FieldResolution)Resolve(refBase, slot);

    public CollectionResolution ResolveCollection(int refBase, int slot) =>
        (CollectionResolution)Resolve(refBase, slot);

    public RelationResolution ResolveRelation(int refBase, int slot) => (RelationResolution)Resolve(refBase, slot);

    public ScopeResolution ResolveScopeType(int refBase, int slot) => (ScopeResolution)Resolve(refBase, slot);

    public ProcedureResolution ResolveProcedure(int refBase, int slot) => (ProcedureResolution)Resolve(refBase, slot);

    public EventResolution ResolveEvent(int refBase, int slot) => (EventResolution)Resolve(refBase, slot);

    public TerminalResolution ResolveTerminal(int refBase, int slot) => (TerminalResolution)Resolve(refBase, slot);

    public ComputeResolution ResolveCompute(int refBase, int slot) => (ComputeResolution)Resolve(refBase, slot);

    public EnumTypeResolution ResolveEnumType(int refBase, int slot) => (EnumTypeResolution)Resolve(refBase, slot);

    public EnumMemberResolution ResolveEnumMember(int refBase, int slot) => (EnumMemberResolution)Resolve(refBase, slot);

    public AnchorResolution ResolveAnchor(int refBase, int slot) => (AnchorResolution)Resolve(refBase, slot);

    public EntityTypeDefinition TypeByQualifiedId(string qualifiedId)
    {
        foreach (var type in _typesById)
        {
            if (string.Equals(type.QualifiedId, qualifiedId, StringComparison.Ordinal)) return type;
        }

        throw Faults.Unresolved($"type '{qualifiedId}' is not part of the rule set");
    }

    public int ProcedureIndex(ProcedureDefinition procedure) => procedure.Id;

    internal IReadOnlyList<ProcedureDefinition> RefBaseProcedures => _refBaseProcedures;
}
