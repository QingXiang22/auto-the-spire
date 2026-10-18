namespace CoreTheSpire.Engine;

/// <summary>
/// Authoring-layer type declarations. All of these are mutable on purpose: a package is authored as a graph of
/// symbols, then frozen by <see cref="EnginePackageBuilder.Build"/>. Linked, dense identifiers are assigned by
/// <see cref="RuleSetBuilder"/>, never by the authoring layer.
/// </summary>
public abstract class DeclarationSymbol : RuleAuthorObject
{
    protected DeclarationSymbol(string name)
    {
        Name = string.IsNullOrWhiteSpace(name) ? throw Faults.Schema("declaration name is required") : name;
    }

    /// <summary>Local name inside the declaring package.</summary>
    public string Name { get; }

    /// <summary>Qualified id (<c>package:Name</c>) assigned when the package is built.</summary>
    public string QualifiedId { get; internal set; } = string.Empty;

    public override string ToString() => QualifiedId.Length == 0 ? Name : QualifiedId;
}

/// <summary>Reference to a declaration of another package, resolved by qualified id at link time.</summary>
public sealed class ExternalSymbol : DeclarationSymbol
{
    public ExternalSymbol(string qualifiedId) : base(qualifiedId)
    {
        QualifiedId = qualifiedId;
    }

    public static ExternalSymbol Of(string qualifiedId) => new(qualifiedId);
}

public sealed class EnumTypeSymbol : DeclarationSymbol
{
    private readonly AuthoringList<string> _members = [];

    public EnumTypeSymbol(string name) : base(name)
    {
    }

    public IReadOnlyList<string> Members => _members;

    public EnumTypeSymbol Member(string name)
    {
        EnsureMutable();
        if (_members.Contains(name, StringComparer.Ordinal))
            throw Faults.Schema($"enum '{Name}' already declares member '{name}'");
        _members.Add(name);
        return this;
    }

    public int OrdinalOf(string member)
    {
        var index = _members.IndexOf(member);
        if (index < 0) throw Faults.Unresolved($"enum '{Name}' has no member '{member}'");
        return index;
    }
}

public sealed class EntityTypeSymbol : DeclarationSymbol
{
    private readonly AuthoringList<FieldSymbol> _fields = [];

    public EntityTypeSymbol(string name) : base(name)
    {
    }

    public IReadOnlyList<FieldSymbol> Fields => _fields;

    /// <summary>When true a later package may append fields (declaring the type in its <c>Extends</c> list).</summary>
    private bool _extensibleFields;
    public bool ExtensibleFields { get => _extensibleFields; set { EnsureMutable(); _extensibleFields = value; } }

    /// <summary>Field holding the owning participant, used by owner-private observation. Null means no owner field.</summary>
    private string? _ownerFieldName;
    public string? OwnerFieldName { get => _ownerFieldName; set { EnsureMutable(); _ownerFieldName = value; } }

    public FieldSymbol Field(string name) =>
        _fields.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.Ordinal))
        ?? throw Faults.Unresolved($"type '{Name}' has no field '{name}'");

    public FieldSymbol AddField(string name, FieldTypeSchema type, Value? defaultValue = null,
        FieldVisibility visibility = FieldVisibility.Public, FieldLifetime lifetime = FieldLifetime.Persistent,
        bool? optional = null)
    {
        EnsureMutable();
        if (_fields.Any(f => string.Equals(f.Name, name, StringComparison.Ordinal)))
            throw Faults.Schema($"type '{Name}' already declares field '{name}'");
        var field = new FieldSymbol(name, type, defaultValue, visibility, lifetime, _fields.Count)
        {
            Owner = this,
            // A gameplay field is required unless the author says otherwise. In particular a reference field is
            // required by default: "no target yet" is legal only while the instance is still being constructed, so an
            // implicit optional default would silently hide a reference the package never assigned. Content that
            // really means "may stay empty" must pass optional: true.
            Optional = optional ?? false,
        };
        _fields.Add(field);
        return field;
    }
}

public sealed class FieldSymbol : DeclarationSymbol
{
    internal FieldSymbol(string name, FieldTypeSchema type, Value? defaultValue, FieldVisibility visibility,
        FieldLifetime lifetime, int declarationIndex) : base(name)
    {
        Type = type;
        DefaultValue = defaultValue;
        Visibility = visibility;
        Lifetime = lifetime;
        DeclarationIndex = declarationIndex;
    }

    public FieldTypeSchema Type { get; }

    public Value? DefaultValue { get; }

    public FieldVisibility Visibility { get; }

    public FieldLifetime Lifetime { get; }

    public int DeclarationIndex { get; }

    /// <summary>
    /// When true the field accepts <see cref="Value.None"/> / an unset reference at any time. When false an unset
    /// value is legal <em>only</em> while the instance is still under construction (see
    /// <see cref="SessionState.RequireStableState"/>): the construction process must assign a live value before the
    /// first stable decision, terminal result or published snapshot, or the engine raises
    /// <see cref="EngineFaultCode.RequiredRefUnassigned"/>.
    /// </summary>
    public bool Optional { get; init; }

    internal EntityTypeSymbol? Owner { get; init; }

    internal EventSymbol? OwnerEvent { get; set; }

    internal TerminalResultSymbol? OwnerTerminal { get; set; }

    internal ComputeAssetSymbol? OwnerCompute { get; set; }

    internal bool IsComputeOutput { get; set; }

    internal bool IsPayloadField { get; init; }

    public override string ToString() => Owner is null ? Name : $"{Owner.Name}.{Name}";
}

public sealed class CollectionSymbol : DeclarationSymbol
{
    public CollectionSymbol(string name, EntityTypeSymbol elementType) : base(name)
    {
        ElementType = elementType ?? throw Faults.Schema("collection element type is required");
    }

    public EntityTypeSymbol ElementType { get; }

    private bool _ordered = true;
    public bool Ordered { get => _ordered; set { EnsureMutable(); _ordered = value; } }

    /// <summary>Collections sharing a group are mutually exclusive: an instance lives in at most one of them.</summary>
    private string? _membershipGroup;
    public string? MembershipGroup { get => _membershipGroup; set { EnsureMutable(); _membershipGroup = value; } }

    private int _capacity = int.MaxValue;
    public int Capacity { get => _capacity; set { EnsureMutable(); _capacity = value; } }

    private bool _identityVisible = true;
    public bool IdentityVisible { get => _identityVisible; set { EnsureMutable(); _identityVisible = value; } }

    private bool _sizeVisible = true;
    public bool SizeVisible { get => _sizeVisible; set { EnsureMutable(); _sizeVisible = value; } }

    private bool _orderVisible = true;
    public bool OrderVisible { get => _orderVisible; set { EnsureMutable(); _orderVisible = value; } }
}

public sealed class RelationSymbol : DeclarationSymbol
{
    public RelationSymbol(string name, EntityTypeSymbol targetType) : base(name)
    {
        TargetType = targetType ?? throw Faults.Schema("relation target type is required");
    }

    public EntityTypeSymbol TargetType { get; }

    /// <summary>True for a typed reference set; false for a single typed reference.</summary>
    private bool _many = true;
    public bool Many { get => _many; set { EnsureMutable(); _many = value; } }

    private bool _public = true;
    public bool Public { get => _public; set { EnsureMutable(); _public = value; } }
}

public sealed class ScopeTypeSymbol : DeclarationSymbol
{
    public ScopeTypeSymbol(string name) : base(name)
    {
    }

    /// <summary>Declared parent scope type. Null means the scope can be created at the session root.</summary>
    private ScopeTypeSymbol? _parent;
    public ScopeTypeSymbol? Parent { get => _parent; set { EnsureMutable(); _parent = value; } }
}

public sealed class ProcedureSymbol : DeclarationSymbol
{
    public ProcedureSymbol(string name) : base(name)
    {
    }

    public AuthoringList<FieldSymbol> Parameters { get; } = [];

    public AuthoringList<FieldSymbol> Locals { get; } = [];

    private Block? _body;
    public Block? Body { get => _body; set { EnsureMutable(); _body = value; } }

    /// <summary>A pure procedure may not write state, consume randomness, emit events or request decisions.</summary>
    private bool _isPure;
    public bool IsPure { get => _isPure; set { EnsureMutable(); _isPure = value; } }

    /// <summary>Declared result shape for a pure modifier procedure. Null means the procedure returns nothing.</summary>
    private FieldTypeSchema? _resultType;
    public FieldTypeSchema? ResultType { get => _resultType; set { EnsureMutable(); _resultType = value; } }

    public FieldSymbol Parameter(string name, FieldTypeSchema type)
    {
        EnsureMutable();
        var field = new FieldSymbol(name, type, null, FieldVisibility.Hidden, FieldLifetime.ExecutionOnly,
            Parameters.Count) { Owner = null };
        Parameters.Add(field);
        return field;
    }

    public int DeclareLocal(string name, FieldTypeSchema type)
    {
        EnsureMutable();
        Locals.Add(new FieldSymbol(name, type, null, FieldVisibility.Hidden, FieldLifetime.ExecutionOnly, Locals.Count));
        return Locals.Count - 1;
    }
}

public sealed class EventSymbol : DeclarationSymbol
{
    private readonly AuthoringList<FieldSymbol> _payload = [];

    public EventSymbol(string name) : base(name)
    {
    }

    public IReadOnlyList<FieldSymbol> Payload => _payload;

    public FieldSymbol AddPayload(string name, FieldTypeSchema type)
    {
        EnsureMutable();
        if (_payload.Any(f => string.Equals(f.Name, name, StringComparison.Ordinal)))
            throw Faults.Schema($"event '{Name}' already declares payload '{name}'");
        var field = new FieldSymbol(name, type, null, FieldVisibility.Public, FieldLifetime.ExecutionOnly,
            _payload.Count)
        {
            IsPayloadField = true,
            Optional = true,
            OwnerEvent = this,
        };
        _payload.Add(field);
        return field;
    }

    public FieldSymbol PayloadField(string name) =>
        _payload.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.Ordinal))
        ?? throw Faults.Unresolved($"event '{Name}' has no payload field '{name}'");
}

public sealed class TerminalResultSymbol : DeclarationSymbol
{
    private readonly AuthoringList<FieldSymbol> _fields = [];

    public TerminalResultSymbol(string name) : base(name)
    {
    }

    public IReadOnlyList<FieldSymbol> Fields => _fields;

    public FieldSymbol AddField(string name, FieldTypeSchema type)
    {
        EnsureMutable();
        var field = new FieldSymbol(name, type, null, FieldVisibility.Public, FieldLifetime.ExecutionOnly,
            _fields.Count)
        {
            IsPayloadField = true,
            Optional = true,
            OwnerTerminal = this,
        };
        _fields.Add(field);
        return field;
    }
}

public sealed class ComputeAssetSymbol : DeclarationSymbol
{
    private readonly AuthoringList<FieldSymbol> _inputs = [];
    private readonly AuthoringList<FieldSymbol> _outputs = [];

    public ComputeAssetSymbol(string name) : base(name)
    {
    }

    private string _backendId = "lua";
    public string BackendId { get => _backendId; set { EnsureMutable(); _backendId = value; } }

    private string _sourcePath = string.Empty;
    public string SourcePath { get => _sourcePath; set { EnsureMutable(); _sourcePath = value; } }

    private string _sourceHash = string.Empty;
    public string SourceHash { get => _sourceHash; set { EnsureMutable(); _sourceHash = value; } }

    private string _entry = "main";
    public string Entry { get => _entry; set { EnsureMutable(); _entry = value; } }

    public IReadOnlyList<FieldSymbol> Inputs => _inputs;

    public IReadOnlyList<FieldSymbol> Outputs => _outputs;

    private ComputeLimits _limits = ComputeLimits.Default;
    public ComputeLimits Limits { get => _limits; set { EnsureMutable(); _limits = value; } }

    private NumericProfile _numericProfile = NumericProfile.Default;
    public NumericProfile NumericProfile { get => _numericProfile; set { EnsureMutable(); _numericProfile = value; } }

    public FieldSymbol Input(string name, FieldTypeSchema type) => Add(_inputs, name, type, false);

    public FieldSymbol Output(string name, FieldTypeSchema type) => Add(_outputs, name, type, true);

    private FieldSymbol Add(AuthoringList<FieldSymbol> list, string name, FieldTypeSchema type, bool output)
    {
        EnsureMutable();
        if (list.Any(f => string.Equals(f.Name, name, StringComparison.Ordinal)))
            throw Faults.Schema($"compute asset '{Name}' already declares '{name}'");
        var field = new FieldSymbol(name, type, null, FieldVisibility.Hidden, FieldLifetime.ExecutionOnly,
            list.Count)
        {
            IsPayloadField = true,
            Optional = true,
            OwnerCompute = this,
            IsComputeOutput = output,
        };
        list.Add(field);
        return field;
    }
}

public sealed class ActionSymbol : DeclarationSymbol
{
    private readonly AuthoringList<FieldSymbol> _parameters = [];
    private readonly Dictionary<string, AuthoringList<Expr>> _choices = new(StringComparer.Ordinal);

    public ActionSymbol(string name) : base(name)
    {
    }

    /// <summary>Participant role allowed to submit this action. Null means any participant.</summary>
    private string? _participantRole;
    public string? ParticipantRole { get => _participantRole; set { EnsureMutable(); _participantRole = value; } }

    public IReadOnlyList<FieldSymbol> Parameters => _parameters;

    private Expr? _guard;
    public Expr? Guard { get => _guard; set { EnsureMutable(); _guard = value; } }

    /// <summary>Parameter whose candidates come from <see cref="CandidateQuery"/>.</summary>
    private string? _candidateParameter;
    public string? CandidateParameter { get => _candidateParameter; set { EnsureMutable(); _candidateParameter = value; } }

    private Query? _candidateQuery;
    public Query? CandidateQuery { get => _candidateQuery; set { EnsureMutable(); _candidateQuery = value; } }

    public IReadOnlyDictionary<string, AuthoringList<Expr>> Choices =>
        new System.Collections.ObjectModel.ReadOnlyDictionary<string, AuthoringList<Expr>>(_choices);

    private Block? _body;
    public Block? Body { get => _body; set { EnsureMutable(); _body = value; } }

    /// <summary>Compiled program procedure, created by <see cref="ActionBuilder.Body"/>.</summary>
    internal ProcedureSymbol? ProgramSymbol { get; set; }

    public FieldSymbol Parameter(string name, FieldTypeSchema type)
    {
        EnsureMutable();
        if (_parameters.Any(f => string.Equals(f.Name, name, StringComparison.Ordinal)))
            throw Faults.Schema($"action '{Name}' already declares parameter '{name}'");
        var field = new FieldSymbol(name, type, null, FieldVisibility.Public, FieldLifetime.ExecutionOnly,
            _parameters.Count) { IsPayloadField = true };
        _parameters.Add(field);
        return field;
    }

    public void Choice(string parameterName, Expr value)
    {
        EnsureMutable();
        if (!_choices.TryGetValue(parameterName, out var list))
        {
            list = [];
            _choices.Add(parameterName, list);
        }
        list.Add(value);
    }
}

public sealed class SubscriptionSymbol : RuleAuthorObject
{
    public SubscriptionSymbol(EventSymbol eventType, ProcedureSymbol reaction)
    {
        EventType = eventType ?? throw Faults.Schema("subscription event type is required");
        Reaction = reaction ?? throw Faults.Schema("subscription reaction procedure is required");
    }

    public EventSymbol EventType { get; }

    public ProcedureSymbol Reaction { get; }

    private int _priority;
    public int Priority { get => _priority; set { EnsureMutable(); _priority = value; } }

    /// <summary>Pure predicate re-evaluated when the reaction actually runs (listener still valid).</summary>
    private Expr? _recheck;
    public Expr? Recheck { get => _recheck; set { EnsureMutable(); _recheck = value; } }

    /// <summary>Payload field bound to the reaction's scope. Null means the emitting frame scope is inherited.</summary>
    private string? _scopePayloadField;
    public string? ScopePayloadField { get => _scopePayloadField; set { EnsureMutable(); _scopePayloadField = value; } }
    private Query? _instances;
    public Query? Instances { get => _instances; set { EnsureMutable(); _instances = value; } }
    private Expr? _instanceOrder;
    public Expr? InstanceOrder { get => _instanceOrder; set { EnsureMutable(); _instanceOrder = value; } }
    private int? _evaluationLocalCount;
    public int? EvaluationLocalCount { get => _evaluationLocalCount; set { EnsureMutable(); _evaluationLocalCount = value; } }

    public override string ToString() => $"{EventType.Name} -> {Reaction.Name} (priority {Priority})";
}

public sealed class TypeProjectionSymbol : RuleAuthorObject
{
    public TypeProjectionSymbol(EntityTypeSymbol type, bool include)
    {
        Type = type;
        Include = include;
    }

    public EntityTypeSymbol Type { get; }

    public bool Include { get; }

    /// <summary>Field that must equal the viewing participant for owner-private fields to be visible.</summary>
    private string? _ownerField;
    public string? OwnerField { get => _ownerField; set { EnsureMutable(); _ownerField = value; } }

    private bool _includeHiddenFields;
    public bool IncludeHiddenFields { get => _includeHiddenFields; set { EnsureMutable(); _includeHiddenFields = value; } }
}

/// <summary>
/// A pack-declared authorization: one fact entity names a viewing participant and holds references to instances that
/// viewer is allowed to identify. The viewer field is resolved exactly like <see cref="TypeProjectionSymbol.OwnerField"/>
/// (ownership and reveal authorization therefore share one declaration and cannot drift apart).
///
/// The projection exposes, for one viewer, precisely the instances some fact naming that viewer references: the
/// default hiding rules still apply to every other instance, the revealed instance is exposed with only the fields the
/// viewer may already see, and no content-definition id ever stands in for a real instance reference.
/// </summary>
public sealed class RevealSymbol
{
    public RevealSymbol(EntityTypeSymbol authorityType, RelationSymbol relation)
    {
        AuthorityType = authorityType ?? throw Faults.Schema("reveal authority type is required");
        Relation = relation ?? throw Faults.Schema("reveal relation is required");
    }

    /// <summary>The fact entity that carries the viewer field and owns the reveal relation edges.</summary>
    public EntityTypeSymbol AuthorityType { get; }

    /// <summary>Relation holding the revealed instance references, owned by one authority fact.</summary>
    public RelationSymbol Relation { get; }
}

public sealed class ProjectionSymbol : DeclarationSymbol
{
    private readonly AuthoringList<TypeProjectionSymbol> _types = [];
    private readonly AuthoringList<RevealSymbol> _reveals = [];

    public ProjectionSymbol(string name) : base(name)
    {
    }

    private string _participantRole = string.Empty;
    public string ParticipantRole { get => _participantRole; set { EnsureMutable(); _participantRole = value; } }

    public IReadOnlyList<TypeProjectionSymbol> Types => _types;

    public IReadOnlyList<RevealSymbol> Reveals => _reveals;

    private bool _includeCollections = true;
    public bool IncludeCollections { get => _includeCollections; set { EnsureMutable(); _includeCollections = value; } }

    private bool _includeRelations = true;
    public bool IncludeRelations { get => _includeRelations; set { EnsureMutable(); _includeRelations = value; } }

    public TypeProjectionSymbol Type(EntityTypeSymbol type, bool include = true)
    {
        EnsureMutable();
        var projection = new TypeProjectionSymbol(type, include);
        _types.Add(projection);
        return projection;
    }

    /// <summary>Authorizes the viewers named by <paramref name="authorityType"/> to identify the referenced instances.</summary>
    public RevealSymbol Reveal(EntityTypeSymbol authorityType, RelationSymbol relation)
    {
        EnsureMutable();
        var reveal = new RevealSymbol(authorityType, relation);
        _reveals.Add(reveal);
        return reveal;
    }
}

public enum FieldTypeKind : byte
{
    Bool = 0,
    Int = 1,
    Fixed = 2,
    Enum = 3,
    String = 4,
    Ref = 5,
    Participant = 6,
    List = 7,

    /// <summary>Engine-allocated binding slot (query item, decision result, aggregate accumulator).</summary>
    Any = 8,
}

public enum FieldVisibility : byte
{
    Public = 0,
    OwnerPrivate = 1,
    Hidden = 2,
}

public enum FieldLifetime : byte
{
    /// <summary>
    /// Part of play state: snapshots and the state hash include it. <b>This is the only lifetime an entity gameplay
    /// field may declare today</b> (an execution-only entity field is rejected at link time, see
    /// <see cref="IrTypeChecker"/>), because "execution-only instance data" would otherwise be an invisible part of a
    /// live instance that no observer and no snapshot could account for.
    /// </summary>
    Persistent = 0,

    /// <summary>
    /// Execution-only data: never snapshotted, never observed. Reserved for frame slots and payloads — procedure
    /// parameters, locals, query/decision/compute bindings, event payloads, terminal result fields and compute
    /// inputs/outputs are all declared with this lifetime by construction. Temporary arithmetic belongs in a frame
    /// slot, not in an entity field.
    /// </summary>
    ExecutionOnly = 1,
}

/// <summary>Shape of a declared field, authored with symbols and resolved to dense ids at link time.</summary>
public sealed record FieldTypeSchema(
    FieldTypeKind Kind,
    EnumTypeSymbol? Enum = null,
    EntityTypeSymbol? Target = null,
    long Min = long.MinValue,
    long Max = long.MaxValue,
    decimal FixedMin = decimal.MinValue,
    decimal FixedMax = decimal.MaxValue,
    int FixedScale = 2,
    int MaxLength = 128,
    FieldTypeSchema? Element = null)
{
    public static FieldTypeSchema Bool() => new(FieldTypeKind.Bool);

    public static FieldTypeSchema Int(long min = long.MinValue, long max = long.MaxValue)
    {
        if (min > max) throw Faults.Schema($"int field bounds are inverted: {min}..{max}");
        return new FieldTypeSchema(FieldTypeKind.Int, Min: min, Max: max);
    }

    public static FieldTypeSchema Fixed(int scale = 2, decimal min = decimal.MinValue, decimal max = decimal.MaxValue)
    {
        if (min > max) throw Faults.Schema($"fixed field bounds are inverted: {min}..{max}");
        return new FieldTypeSchema(FieldTypeKind.Fixed, FixedMin: min, FixedMax: max, FixedScale: scale);
    }

    public static FieldTypeSchema EnumOf(EnumTypeSymbol type) =>
        new(FieldTypeKind.Enum, Enum: type ?? throw Faults.Schema("enum type is required"));

    public static FieldTypeSchema String(int maxLength = 128)
    {
        if (maxLength <= 0) throw Faults.Schema("string max length must be positive");
        return new FieldTypeSchema(FieldTypeKind.String, MaxLength: maxLength);
    }

    public static FieldTypeSchema RefTo(EntityTypeSymbol type) =>
        new(FieldTypeKind.Ref, Target: type ?? throw Faults.Schema("reference target type is required"));

    /// <summary>
    /// A reference with no declared target type. Used where a helper accepts any entity reference (for example a power
    /// helper that must take either a player or a monster). Runtime reference checks still apply at the call site.
    /// </summary>
    public static FieldTypeSchema AnyRef() => new(FieldTypeKind.Ref);

    public static FieldTypeSchema Participant() => new(FieldTypeKind.Participant);

    /// <summary>
    /// Engine-allocated binding slot: a query item, decision result, compute output or aggregate accumulator. The
    /// link-time type check refines it where it is bound and refuses arithmetic on it while it is still untyped.
    /// </summary>
    public static FieldTypeSchema Binding() => new(FieldTypeKind.Any);

    /// <summary>Bounded homogeneous list. Element types are validated by the same signature as scalar fields.</summary>
    public static FieldTypeSchema ListOf(FieldTypeSchema element, int maxLength = 256)
    {
        if (maxLength <= 0) throw Faults.Schema("list max length must be positive");
        return new FieldTypeSchema(FieldTypeKind.List, MaxLength: maxLength,
            Element: element ?? throw Faults.Schema("list element type is required"));
    }

    public static FieldTypeSchema IntList(int maxLength = 256) => ListOf(Int(), maxLength);

    public static FieldTypeSchema IntList(long min, long max, int maxLength = 256) => ListOf(Int(min, max), maxLength);
}

/// <summary>&quot;Compute is not a script language special case&quot;: budgets are declared per asset.</summary>
public sealed record ComputeLimits(
    int MaxInputValues = 4096,
    int MaxOutputValues = 1024,
    long MaxInstructions = 1_000_000,
    long MaxHeapBytes = 16 * 1024 * 1024,
    int MaxCallDepth = 8)
{
    public static readonly ComputeLimits Default = new();
}

// ---------------------------------------------------------------------------------------------------------------
// Linked (frozen) definitions. These carry dense ids and are what the scheduler and storage execute against.
// ---------------------------------------------------------------------------------------------------------------

public sealed record EnumTypeDefinition(int Id, string QualifiedId, IReadOnlyList<string> Members)
{
    public int OrdinalOf(string member)
    {
        for (var i = 0; i < Members.Count; i++)
        {
            if (string.Equals(Members[i], member, StringComparison.Ordinal)) return i;
        }
        throw Faults.Unresolved($"enum '{QualifiedId}' has no member '{member}'");
    }

    public string NameOf(int ordinal) => ordinal >= 0 && ordinal < Members.Count
        ? Members[ordinal]
        : throw Faults.Range($"enum '{QualifiedId}' has no ordinal {ordinal}");
}

public sealed record FieldDefinition(
    string Name,
    FieldTypeSchema Shape,
    int Index,
    int EnumTypeId,
    int TargetTypeId,
    int ElementTargetTypeId,
    int ElementEnumTypeId,
    bool Optional,
    Value DefaultValue,
    FieldVisibility Visibility,
    FieldLifetime Lifetime)
{
    public FieldTypeKind Kind => Shape.Kind;

    public long Min => Shape.Min;

    public long Max => Shape.Max;

    public decimal FixedMin => Shape.FixedMin;

    public decimal FixedMax => Shape.FixedMax;

    public int FixedScale => Shape.FixedScale;

    public int MaxLength => Shape.MaxLength;

    public FieldTypeSchema? Element => Shape.Element;

    public override string ToString() => Name;
}

public sealed record EntityTypeDefinition(int Id, string QualifiedId, IReadOnlyList<FieldDefinition> Fields, int PackageIndex)
{
    private Dictionary<string, int>? _index;

    public int FieldIndex(string name)
    {
        _index ??= BuildIndex();
        return _index.TryGetValue(name, out var index)
            ? index
            : throw Faults.Unresolved($"type '{QualifiedId}' has no field '{name}'");
    }

    public bool TryFieldIndex(string name, out int index)
    {
        _index ??= BuildIndex();
        return _index.TryGetValue(name, out index);
    }

    private Dictionary<string, int> BuildIndex()
    {
        var index = new Dictionary<string, int>(Fields.Count, StringComparer.Ordinal);
        for (var i = 0; i < Fields.Count; i++) index[Fields[i].Name] = i;
        return index;
    }

    public override string ToString() => QualifiedId;
}

public sealed record CollectionDefinition(
    int Id,
    string QualifiedId,
    int ElementTypeId,
    bool Ordered,
    string? MembershipGroup,
    int Capacity,
    bool IdentityVisible,
    bool SizeVisible,
    bool OrderVisible);

public sealed record RelationDefinition(int Id, string QualifiedId, int TargetTypeId, bool Many, bool Public);

public sealed record ScopeTypeDefinition(int Id, string QualifiedId, int ParentScopeTypeId)
{
    public bool HasParent => ParentScopeTypeId >= 0;
}

public sealed record EventDefinition(int Id, string QualifiedId, IReadOnlyList<FieldDefinition> Payload)
{
    private Dictionary<string, int>? _index;

    public int PayloadIndex(string name)
    {
        _index ??= BuildIndex();
        return _index.TryGetValue(name, out var value)
            ? value
            : throw Faults.Unresolved($"event '{QualifiedId}' has no payload field '{name}'");
    }

    public bool TryPayloadIndex(string name, out int index)
    {
        _index ??= BuildIndex();
        return _index.TryGetValue(name, out index);
    }

    private Dictionary<string, int> BuildIndex()
    {
        var index = new Dictionary<string, int>(Payload.Count, StringComparer.Ordinal);
        for (var i = 0; i < Payload.Count; i++) index[Payload[i].Name] = i;
        return index;
    }
}

public sealed record TerminalResultDefinition(int Id, string QualifiedId, IReadOnlyList<FieldDefinition> Fields)
{
    public int FieldIndex(string name)
    {
        for (var i = 0; i < Fields.Count; i++)
        {
            if (string.Equals(Fields[i].Name, name, StringComparison.Ordinal)) return i;
        }
        throw Faults.Unresolved($"terminal result '{QualifiedId}' has no field '{name}'");
    }
}

public sealed record ProcedureDefinition(
    int Id,
    string QualifiedId,
    IReadOnlyList<FieldDefinition> Parameters,
    IReadOnlyList<FieldDefinition> Locals,
    Block Body,
    bool IsPure,
    FieldTypeSchema? ResultType,
    int PackageIndex,
    int RefBase)
{
    public override string ToString() => QualifiedId;
}

public sealed record ActionDefinition(
    int Id,
    string QualifiedId,
    string? ParticipantRole,
    IReadOnlyList<FieldDefinition> Parameters,
    Expr? Guard,
    int CandidateParameterIndex,
    Query? CandidateQuery,
    IReadOnlyDictionary<int, IReadOnlyList<Expr>> Choices,
    ProcedureDefinition Program,
    int PackageIndex,
    int RefBase);

public sealed record SubscriptionDefinition(
    EventDefinition EventType,
    ProcedureDefinition Reaction,
    int Priority,
    Expr? Recheck,
    int ScopePayloadIndex,
    int PackageIndex,
    int RefBase,
    int DeclarationOrder,
    Query? Instances = null,
    Expr? InstanceOrder = null,
    int? EvaluationLocalCount = null)
{
    internal ProcedureDefinition EvaluationProcedure()
    {
        var locals = EvaluationLocalCount is { } count
            ? Enumerable.Range(0, count).Select(i => new FieldDefinition("$subscription" + i,
                FieldTypeSchema.Binding(), i, -1, -1, -1, -1, true, Value.None,
                FieldVisibility.Hidden, FieldLifetime.ExecutionOnly)).ToArray()
            : Reaction.Locals;
        return Reaction with { PackageIndex = PackageIndex, RefBase = RefBase, Locals = locals, IsPure = true };
    }
}

public sealed record ComputeAssetDefinition(
    int Id,
    string QualifiedId,
    string BackendId,
    string SourcePath,
    string SourceHash,
    string Entry,
    IReadOnlyList<FieldDefinition> Inputs,
    IReadOnlyList<FieldDefinition> Outputs,
    ComputeLimits Limits,
    NumericProfile NumericProfile,
    int PackageIndex,
    int RefBase);

/// <summary>One compiled reveal authorization: authority facts of <paramref name="AuthorityTypeId"/> name the viewer in
/// field <paramref name="ViewerFieldIndex"/> and own edges of relation <paramref name="RelationId"/>.</summary>
public sealed record RevealProjection(int AuthorityTypeId, int ViewerFieldIndex, int RelationId);

public sealed record ProjectionDefinition(
    int Id,
    string QualifiedId,
    string ParticipantRole,
    IReadOnlyDictionary<int, TypeProjection> Types,
    bool IncludeCollections,
    bool IncludeRelations,
    IReadOnlyList<RevealProjection> Reveals);

public sealed record TypeProjection(
    EntityTypeDefinition Type,
    bool Include,
    int OwnerFieldIndex,
    bool IncludeHiddenFields);

/// <summary>Static description of one IR primitive: what it declares and which engine capabilities it uses.</summary>
public sealed record PrimitiveSignature(string Name, IReadOnlyList<EngineEffect> Effects)
{
    public bool Has(EngineEffect effect) => Effects.Contains(effect);
}

public enum EngineEffect
{
    PureExpression = 0,
    ReadsState = 1,
    WritesState = 2,
    ConsumesRandom = 3,
    RequestsDecision = 4,
    EmitsEvent = 5,
    CallsProcedure = 6,
    CallsCompute = 7,
    EndsSession = 8,
}
