namespace CoreTheSpire.Engine;

/// <summary>
/// Authoring front end for one rule package. Declarations are symbols; instruction references are package-local
/// reference slots. <see cref="Build"/> freezes the package; a builder is single-use so that a package can never
/// be mutated after linking.
/// </summary>
public sealed class EnginePackageBuilder
{
    private readonly List<DeclarationSymbol> _declarations = [];
    private readonly List<object> _refTargets = [];
    private readonly Dictionary<object, int> _refSlots = [];
    private readonly List<SubscriptionSymbol> _subscriptions = [];
    private readonly List<ProcedureSymbol> _sessionStart = [];
    private readonly List<AnchorSymbol> _anchors = [];
    private bool _built;

    public EnginePackageBuilder(string packageId, string version = "0.1.0")
    {
        PackageId = string.IsNullOrWhiteSpace(packageId) ? throw Faults.Schema("package id is required") : packageId;
        Version = version;
    }

    public string PackageId { get; }

    public string Version { get; }

    public string EngineCompatibility { get; set; } = RuleSetBuilder.EngineVersion;
    public string CompilerIdentity { get; set; } = "csharp-sdk/0.2";
    internal IReadOnlyList<EngineDslTemplate> RuleTemplates { get; set; } = [];

    public List<string> Dependencies { get; } = [];

    /// <summary>Qualified ids this package explicitly replaces. Replacing without declaring fails at link time.</summary>
    public List<string> Overrides { get; } = [];

    /// <summary>Qualified ids of extensible entity types this package appends fields to.</summary>
    public List<string> Extends { get; } = [];

    public IReadOnlyList<DeclarationSymbol> Declarations => _declarations;

    public IReadOnlyList<object> RefTargets => _refTargets;

    public IReadOnlyList<SubscriptionSymbol> Subscriptions => _subscriptions;

    public IReadOnlyList<ProcedureSymbol> SessionStartProcedures => _sessionStart;

    internal void AddDeclaration(DeclarationSymbol symbol)
    {
        EnsureNotBuilt();
        _declarations.Add(symbol);
    }

    public void Subscribe(EventSymbol eventType, ProcedureSymbol reaction, int priority = 0,
        Expr? recheck = null, string? scopePayloadField = null, Query? instances = null, Expr? instanceOrder = null,
        int? evaluationLocalCount = null)
    {
        EnsureNotBuilt();
        _subscriptions.Add(new SubscriptionSymbol(eventType, reaction)
        {
            Priority = priority,
            Recheck = recheck,
            ScopePayloadField = scopePayloadField,
            Instances = instances,
            InstanceOrder = instanceOrder,
            EvaluationLocalCount = evaluationLocalCount,
        });
    }

    public void OnSessionStart(ProcedureSymbol procedure)
    {
        EnsureNotBuilt();
        _sessionStart.Add(procedure);
    }

    // -- declarations -------------------------------------------------------------------------------------------

    public EnumTypeSymbol Enum(string name) => Declare(new EnumTypeSymbol(name));

    public EntityTypeSymbol EntityType(string name) => Declare(new EntityTypeSymbol(name));

    public ScopeTypeSymbol ScopeType(string name, ScopeTypeSymbol? parent = null)
    {
        var symbol = Declare(new ScopeTypeSymbol(name));
        symbol.Parent = parent;
        return symbol;
    }

    public CollectionSymbol Collection(string name, EntityTypeSymbol elementType) =>
        Declare(new CollectionSymbol(name, elementType));

    public RelationSymbol Relation(string name, EntityTypeSymbol targetType) =>
        Declare(new RelationSymbol(name, targetType));

    public EventSymbol Event(string name) => Declare(new EventSymbol(name));

    public TerminalResultSymbol TerminalResult(string name) => Declare(new TerminalResultSymbol(name));

    public ComputeAssetSymbol ComputeAsset(string name) => Declare(new ComputeAssetSymbol(name));

    public ActionSymbol Action(string name) => Declare(new ActionSymbol(name));

    public ProjectionSymbol Projection(string name) => Declare(new ProjectionSymbol(name));

    /// <summary>Returns a callable procedure iterator whose package index is resolved by the linker.</summary>
    public ProcedureBuilder Procedure(string name)
    {
        EnsureNotBuilt();
        var symbol = new ProcedureSymbol(name);
        _declarations.Add(symbol);
        return new ProcedureBuilder(this, symbol);
    }

    /// <summary>Returns a builder for an action whose body is compiled into a package-local program procedure.</summary>
    public ActionBuilder ActionBody(ActionSymbol action)
    {
        EnsureNotBuilt();
        return new ActionBuilder(this, action);
    }

    /// <summary>Reference slot for a declaration; deduplicated per symbol.</summary>
    public int Ref(DeclarationSymbol symbol)
    {
        EnsureNotBuilt();
        ArgumentNullException.ThrowIfNull(symbol);
        if (_refSlots.TryGetValue(symbol, out var existing)) return existing;
        var slot = _refTargets.Count;
        _refTargets.Add(symbol);
        _refSlots.Add(symbol, slot);
        return slot;
    }

    /// <summary>Reference to another package's declaration by qualified id, resolved at link time.</summary>
    public ExternalSymbol External(string qualifiedId)
    {
        EnsureNotBuilt();
        var existing = _declarations.OfType<ExternalSymbol>()
            .FirstOrDefault(s => string.Equals(s.QualifiedId, qualifiedId, StringComparison.Ordinal));
        if (existing is not null) return existing;
        var symbol = new ExternalSymbol(qualifiedId);
        _declarations.Add(symbol);
        return symbol;
    }

    /// <summary>Registers a symbol that is referenced but never declared (session-scoped markers, anchors).</summary>
    public AnchorSymbol Anchor(string name)
    {
        EnsureNotBuilt();
        var anchor = new AnchorSymbol(name);
        _anchors.Add(anchor);
        return anchor;
    }

    private T Declare<T>(T symbol) where T : DeclarationSymbol
    {
        EnsureNotBuilt();
        _declarations.Add(symbol);
        return symbol;
    }

    private void EnsureNotBuilt()
    {
        if (_built) throw Faults.Of(EngineFaultCode.PackageAlreadyBuilt,
            $"package '{PackageId}' has already been built; a package builder is single-use");
    }

    public EnginePackage Build()
    {
        EnsureNotBuilt();
        _built = true;
        return EnginePackage.Freeze(this);
    }

    /// <summary>Marker symbol used for session-level singletons referenced by IR but not declared as a type.</summary>
    public sealed class AnchorSymbol(string name) : DeclarationSymbol(name);
}

public sealed class EnginePackage
{
    private EnginePackage(
        string packageId,
        string version,
        string engineCompatibility,
        string compilerIdentity,
        IReadOnlyList<string> dependencies,
        IReadOnlyList<string> overrides,
        IReadOnlyList<string> extends,
        IReadOnlyList<DeclarationSymbol> declarations,
        IReadOnlyList<object> refTargets,
        IReadOnlyList<SubscriptionSymbol> subscriptions,
        IReadOnlyList<ProcedureSymbol> sessionStart,
        IReadOnlyList<EngineDslTemplate> ruleTemplates,
        string contentHash)
    {
        PackageId = packageId;
        Version = version;
        EngineCompatibility = engineCompatibility;
        CompilerIdentity = compilerIdentity;
        Dependencies = dependencies;
        Overrides = overrides;
        Extends = extends;
        Declarations = declarations;
        RefTargets = refTargets;
        Subscriptions = subscriptions;
        SessionStartProcedures = sessionStart;
        RuleTemplates = ruleTemplates;
        ContentHash = contentHash;
        Procedures = Array.AsReadOnly(declarations.OfType<ProcedureSymbol>().ToArray());
    }

    public string PackageId { get; }

    public string Version { get; }

    public string EngineCompatibility { get; }
    public string CompilerIdentity { get; }

    public IReadOnlyList<string> Dependencies { get; }

    public IReadOnlyList<string> Overrides { get; }

    public IReadOnlyList<string> Extends { get; }

    public IReadOnlyList<DeclarationSymbol> Declarations { get; }

    public IReadOnlyList<object> RefTargets { get; }

    public IReadOnlyList<SubscriptionSymbol> Subscriptions { get; }

    public IReadOnlyList<ProcedureSymbol> SessionStartProcedures { get; }

    public IReadOnlyList<ProcedureSymbol> Procedures { get; }
    internal IReadOnlyList<EngineDslTemplate> RuleTemplates { get; }

    /// <summary>Stable hash over the authored declaration and IR shape; part of the rule set identity.</summary>
    public string ContentHash { get; }

    internal static EnginePackage Freeze(EnginePackageBuilder builder)
    {
        var declarations = builder.Declarations.ToArray();
        // Declaration kinds are separate namespaces: an entity type and an action may legitimately share a name.
        var qualified = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var declaration in declarations)
        {
            if (declaration is ExternalSymbol) continue;
            if (declaration.QualifiedId.Length == 0)
            {
                // A name that already contains the separator is an absolute qualified id: that is how a package
                // declares another package's declaration in order to override or extend it explicitly.
                declaration.QualifiedId = declaration.Name.Contains(':')
                    ? declaration.Name
                    : builder.PackageId + ":" + declaration.Name;
            }

            var kind = declaration.GetType().Name;
            if (!qualified.TryGetValue(kind, out var names))
            {
                names = new HashSet<string>(StringComparer.Ordinal);
                qualified.Add(kind, names);
            }

            if (!names.Add(declaration.QualifiedId))
            {
                throw Faults.Of(EngineFaultCode.DuplicateDeclaration,
                    $"package '{builder.PackageId}' declares {kind} '{declaration.QualifiedId}' more than once");
            }
        }

        var hash = ContentHasher.HashPackage(builder);
        RuleFreezer.Freeze(builder);
        return new EnginePackage(builder.PackageId, builder.Version, builder.EngineCompatibility, builder.CompilerIdentity,
            Array.AsReadOnly(builder.Dependencies.ToArray()), Array.AsReadOnly(builder.Overrides.ToArray()),
            Array.AsReadOnly(builder.Extends.ToArray()), Array.AsReadOnly(declarations),
            Array.AsReadOnly(builder.RefTargets.ToArray()), Array.AsReadOnly(builder.Subscriptions.ToArray()),
            Array.AsReadOnly(builder.SessionStartProcedures.ToArray()), Array.AsReadOnly(builder.RuleTemplates.ToArray()), hash);
    }
}

/// <summary>
/// Authors an executable scope (a procedure body or an action body). Slots 0..parameterCount-1 are the declared
/// parameters; anonymous and named locals follow. This is the layer that turns authors' named declarations into
/// typed IR and resolves every declaration reference into a package-local slot.
/// </summary>
public abstract class IrScope
{
    private readonly List<FieldSymbol> _locals = [];

    protected IrScope(EnginePackageBuilder package)
    {
        Package = package ?? throw new ArgumentNullException(nameof(package));
        Guard = new ExprBuilder(this);
    }

    public EnginePackageBuilder Package { get; }

    public ExprBuilder Guard { get; }

    protected abstract IReadOnlyList<FieldSymbol> ParameterFields { get; }

    public int ParameterCount => ParameterFields.Count;

    public IReadOnlyList<FieldSymbol> LocalFields => _locals;

    protected int AllocateLocal(string name, FieldTypeSchema type)
    {
        var slot = ParameterCount + _locals.Count;
        _locals.Add(new FieldSymbol(name, type, null, FieldVisibility.Hidden, FieldLifetime.ExecutionOnly, slot));
        return slot;
    }

    /// <summary>Anonymous slot used for query item bindings and aggregates.</summary>
    public int Temp(FieldTypeSchema? type = null) =>
        AllocateLocal("$temp" + (ParameterCount + _locals.Count), type ?? FieldTypeSchema.Binding());

    public int Local(string name, FieldTypeSchema type) => AllocateLocal(name, type);

    public Expr Param(int slot) => new ParamExpr(slot);

    public Expr Param(string name)
    {
        for (var i = 0; i < ParameterFields.Count; i++)
        {
            if (string.Equals(ParameterFields[i].Name, name, StringComparison.Ordinal)) return new ParamExpr(i);
        }
        throw Faults.Unresolved($"scope has no parameter '{name}'");
    }

    public Expr LocalRef(int slot) => new LocalExpr(slot);

    public Expr LocalRef(string name)
    {
        for (var i = 0; i < _locals.Count; i++)
        {
            if (string.Equals(_locals[i].Name, name, StringComparison.Ordinal)) return new LocalExpr(ParameterCount + i);
        }
        throw Faults.Unresolved($"scope has no local '{name}'");
    }

    // -- expression / query / instruction helpers ----------------------------------------------------------------

    public Expr Const(long value) => new ConstExpr(Value.Of(value));

    public Expr Const(bool value) => new ConstExpr(Value.Of(value));

    public Expr Const(decimal value) => new ConstExpr(Value.Of(value));

    public Expr Const(string value) => new ConstExpr(Value.Of(value));

    public Expr Const(Value value) => new ConstExpr(value);

    public Expr EnumConst(EnumTypeSymbol type, string member) => new EnumConstExpr(Package.Ref(type), member);

    public Expr Field(Expr target, FieldSymbol field) => new FieldExpr(target, Package.Ref(field));

    /// <summary>The scope the referenced instance lives in.</summary>
    public Expr EntityScope(Expr entity) => new EntityScopeExpr(entity);

    public Expr Add(Expr left, Expr right) => new ArithExpr(ArithOp.Add, left, right);

    public Expr Subtract(Expr left, Expr right) => new ArithExpr(ArithOp.Subtract, left, right);

    public Expr Multiply(Expr left, Expr right) => new ArithExpr(ArithOp.Multiply, left, right);

    public Expr Divide(Expr left, Expr right) => new ArithExpr(ArithOp.Divide, left, right);

    public Expr Modulo(Expr left, Expr right) => new ArithExpr(ArithOp.Modulo, left, right);

    public Expr Minimum(Expr left, Expr right) => new ArithExpr(ArithOp.Minimum, left, right);

    public Expr Maximum(Expr left, Expr right) => new ArithExpr(ArithOp.Maximum, left, right);

    public Expr Equal(Expr left, Expr right) => new CompareExpr(CompareOp.Equal, left, right);

    public Expr NotEqual(Expr left, Expr right) => new CompareExpr(CompareOp.NotEqual, left, right);

    public Expr Less(Expr left, Expr right) => new CompareExpr(CompareOp.Less, left, right);

    public Expr LessOrEqual(Expr left, Expr right) => new CompareExpr(CompareOp.LessOrEqual, left, right);

    public Expr Greater(Expr left, Expr right) => new CompareExpr(CompareOp.Greater, left, right);

    public Expr GreaterOrEqual(Expr left, Expr right) => new CompareExpr(CompareOp.GreaterOrEqual, left, right);

    public Expr And(Expr left, Expr right) => new LogicExpr(true, left, right);

    public Expr Or(Expr left, Expr right) => new LogicExpr(false, left, right);

    public Expr Not(Expr operand) => new NotExpr(operand);

    public Expr IntToFixed(Expr operand) => new ConvertExpr(ConvertOp.IntToFixed, operand);

    public Expr FixedToInt(Expr operand, FixedRounding rounding = FixedRounding.Truncate) => new ConvertExpr(rounding switch
    {
        FixedRounding.Truncate => ConvertOp.FixedToIntTruncate,
        FixedRounding.Floor => ConvertOp.FixedToIntFloor,
        FixedRounding.Ceiling => ConvertOp.FixedToIntCeiling,
        FixedRounding.Round => ConvertOp.FixedToIntRound,
        _ => throw Faults.Schema("unknown rounding mode"),
    }, operand);

    public Expr Count(Query query) => new CountExpr(query);

    /// <summary>Absolute index of an instance inside a collection, or -1 when it is not a member.</summary>
    public Expr CollectionIndex(CollectionSymbol collection, Expr item) =>
        new CollectionIndexExpr(Package.Ref(collection), item);

    public Expr Exists(Query query) => new ExistsExpr(query);

    public Expr First(Query query) => new FirstExpr(query);

    public Expr IsNone(Expr operand) => new IsNoneExpr(operand);

    public Expr CallPure(ProcedureSymbol procedure, params Expr[] arguments) =>
        new CallPureExpr(Package.Ref(procedure), arguments);

    public Expr Concat(params Expr[] parts) => new StringConcatExpr(parts);

    public Expr FoldEvent(EventSymbol eventType, params Expr[] arguments) =>
        new FoldEventExpr(Package.Ref(eventType), arguments);

    public Expr Aggregate(Query query, AggregateOp op, Func<Expr, Expr>? itemValue = null)
    {
        var slot = Temp();
        var value = itemValue?.Invoke(new LocalExpr(slot));
        return new AggregateExpr(query, op, slot, value);
    }

    public Query Type(EntityTypeSymbol type, ScopeTypeSymbol? scopeType = null) =>
        new TypeQuery(Package.Ref(type), scopeType is null ? -1 : Package.Ref(scopeType));

    public Query Collection(CollectionSymbol collection, ScopeTypeSymbol? scopeType = null) =>
        new CollectionQuery(Package.Ref(collection), scopeType is null ? -1 : Package.Ref(scopeType));

    public Query Relation(RelationSymbol relation, Expr owner) => new RelationQuery(Package.Ref(relation), owner);

    public Query Where(Query source, Func<Expr, Expr> predicate)
    {
        var slot = Temp();
        return new FilterQuery(source, predicate(new LocalExpr(slot)), slot);
    }

    public Query Sort(Query source, Func<Expr, Expr> key, bool descending = false)
    {
        var slot = Temp();
        return new SortQuery(source, key(new LocalExpr(slot)), descending, slot);
    }

    public Query Take(Query source, Expr count) => new TakeQuery(source, count);

    public Query Skip(Query source, Expr count) => new SkipQuery(source, count);

    // -- instructions --------------------------------------------------------------------------------------------

    public Inst SetLocal(int slot, Expr value) => new SetLocalInst(slot, value);

    public Inst SetField(Expr target, FieldSymbol field, Expr value) =>
        new SetFieldInst(target, Package.Ref(field), value);

    public Inst CreateEntity(EntityTypeSymbol type, Expr scope, int resultSlot,
        IReadOnlyList<(FieldSymbol Field, Expr Value)> fields, CollectionSymbol? addTo = null,
        InsertPosition position = InsertPosition.End) =>
        new CreateEntityInst(Package.Ref(type), scope,
            fields.Select(f => new FieldInit(Package.Ref(f.Field), f.Value)).ToArray(),
            resultSlot, addTo is null ? -1 : Package.Ref(addTo), position);

    public Inst CreateScope(ScopeTypeSymbol scopeType, int resultSlot, Expr? parent = null) =>
        new CreateScopeInst(Package.Ref(scopeType), parent, resultSlot);

    public Inst Destroy(Expr target) => new DestroyEntityInst(target);

    public Inst DestroyScope(Expr scope) => new DestroyEntityInst(scope, scopeSubtree: true);

    public Inst AddToCollection(CollectionSymbol collection, Expr item,
        InsertPosition position = InsertPosition.End, Expr? index = null) =>
        new AddToCollectionInst(Package.Ref(collection), item, position, index);

    public Inst RemoveFromCollection(CollectionSymbol collection, Expr item) =>
        new RemoveFromCollectionInst(Package.Ref(collection), item);

    public Inst MoveInCollection(CollectionSymbol collection, Expr item,
        InsertPosition position = InsertPosition.End, Expr? index = null) =>
        new MoveInCollectionInst(Package.Ref(collection), item, position, index);

    public Inst ShuffleCollection(CollectionSymbol collection, string stream) =>
        new ShuffleCollectionInst(Package.Ref(collection), Const(stream));

    public Inst ShuffleCollection(CollectionSymbol collection, Expr stream) =>
        new ShuffleCollectionInst(Package.Ref(collection), stream);

    public Inst AddRelation(RelationSymbol relation, Expr owner, Expr target) =>
        new SetRelationInst(Package.Ref(relation), owner, target, true);

    public Inst RemoveRelation(RelationSymbol relation, Expr owner, Expr target) =>
        new SetRelationInst(Package.Ref(relation), owner, target, false);

    public Inst ClearRelation(RelationSymbol relation, Expr owner) =>
        new ClearRelationInst(Package.Ref(relation), owner);

    public Inst SampleInt(string stream, Expr min, Expr max, int resultSlot) =>
        new SampleIntInst(Const(stream), min, max, resultSlot);

    public Inst SampleInt(Expr stream, Expr min, Expr max, int resultSlot) =>
        new SampleIntInst(stream, min, max, resultSlot);

    public Inst SamplePick(string stream, Query candidates, int resultSlot) =>
        new SamplePickInst(Const(stream), candidates, resultSlot);

    public Inst Emit(EventSymbol eventType, params (FieldSymbol Field, Expr Value)[] payload) =>
        new EmitEventInst(Package.Ref(eventType),
            payload.Select(p => new FieldInit(Package.Ref(p.Field), p.Value)).ToArray());

    public Inst Decide(Expr participant, DecisionResponseKind kind, string prompt, int resultSlot,
        Query? candidates = null, Expr? minimum = null, Expr? maximum = null) =>
        new RequestDecisionInst(participant, kind, candidates, minimum, maximum, prompt, resultSlot);

    public Inst Compute(ComputeAssetSymbol asset, IReadOnlyList<(FieldSymbol Field, Expr Value)> inputs,
        IReadOnlyList<(FieldSymbol Field, int Slot)> outputs) =>
        new ComputeInst(Package.Ref(asset),
            inputs.Select(i => new NamedExpr(i.Field.Name, i.Value)).ToArray(),
            outputs.Select(o => new NamedSlot(o.Field.Name, o.Slot)).ToArray());

    public Inst Call(ProcedureSymbol procedure, params Expr[] arguments) =>
        new CallInst(Package.Ref(procedure), arguments);

    public Inst Call(ProcedureSymbol procedure, int resultSlot, params Expr[] arguments) =>
        new CallInst(Package.Ref(procedure), arguments, resultSlot);

    public Inst If(Expr condition, params Inst[] then) => new IfInst(condition, new Block(then));

    public Inst IfElse(Expr condition, IEnumerable<Inst> then, IEnumerable<Inst> otherwise) =>
        new IfInst(condition, new Block(then), new Block(otherwise));

    public Inst Repeat(Expr count, params Inst[] body) => new RepeatInst(count, new Block(body));

    public Inst ForEach(Query query, Func<Expr, IEnumerable<Inst>> body)
    {
        var slot = Temp();
        return new ForEachInst(query, slot, new Block(body(new LocalExpr(slot))));
    }

    public Inst Return(Expr? value = null) => new ReturnInst(value);

    public Inst SetTerminal(TerminalResultSymbol result, params (FieldSymbol Field, Expr Value)[] fields) =>
        new SetTerminalInst(Package.Ref(result),
            fields.Select(f => new FieldInit(Package.Ref(f.Field), f.Value)).ToArray());

    public Inst Fail(string message) => new FailInst(message);

    internal IReadOnlyList<FieldSymbol> BuildLocalFields() => _locals;
}

/// <summary>Fluent expression helpers for building guards and predicates.</summary>
public sealed class ExprBuilder(IrScope scope)
{
    public IrScope Scope { get; } = scope;

    public Expr Const(long value) => Scope.Const(value);

    public Expr Const(bool value) => Scope.Const(value);

    public Expr Const(string value) => Scope.Const(value);

    public Expr Field(Expr target, FieldSymbol field) => Scope.Field(target, field);

    public Expr Eq(Expr left, Expr right) => Scope.Equal(left, right);

    public Expr Ne(Expr left, Expr right) => Scope.NotEqual(left, right);

    public Expr Lt(Expr left, Expr right) => Scope.Less(left, right);

    public Expr Le(Expr left, Expr right) => Scope.LessOrEqual(left, right);

    public Expr Gt(Expr left, Expr right) => Scope.Greater(left, right);

    public Expr Ge(Expr left, Expr right) => Scope.GreaterOrEqual(left, right);

    public Expr And(Expr left, Expr right) => Scope.And(left, right);

    public Expr Or(Expr left, Expr right) => Scope.Or(left, right);

    public Expr Not(Expr operand) => Scope.Not(operand);

    public Expr Add(Expr left, Expr right) => Scope.Add(left, right);

    public Expr Sub(Expr left, Expr right) => Scope.Subtract(left, right);

    public Expr Mul(Expr left, Expr right) => Scope.Multiply(left, right);
}

public sealed class ProcedureBuilder : IrScope
{
    public ProcedureBuilder(EnginePackageBuilder package, ProcedureSymbol symbol) : base(package)
    {
        Symbol = symbol;
    }

    public ProcedureSymbol Symbol { get; }

    protected override IReadOnlyList<FieldSymbol> ParameterFields => Symbol.Parameters;

    public int Param(string name, FieldTypeSchema type)
    {
        var field = Symbol.Parameter(name, type);
        return field.DeclarationIndex;
    }

    public ProcedureBuilder Pure()
    {
        Symbol.IsPure = true;
        return this;
    }

    public ProcedureBuilder Result(FieldTypeSchema type)
    {
        Symbol.ResultType = type;
        return this;
    }

    public ProcedureSymbol Body(params Inst[] instructions)
    {
        AttachLocalFields();
        Symbol.Body = new Block(instructions);
        return Symbol;
    }

    internal void AttachLocalFields()
    {
        Symbol.Locals.Clear();
        Symbol.Locals.AddRange(BuildLocalFields());
    }
}

public sealed class ActionBuilder : IrScope
{
    public ActionBuilder(EnginePackageBuilder package, ActionSymbol action) : base(package)
    {
        Action = action;
    }

    public ActionSymbol Action { get; }

    protected override IReadOnlyList<FieldSymbol> ParameterFields => Action.Parameters;

    /// <summary>Compiled program procedure created by <see cref="Body"/>.</summary>
    internal ProcedureSymbol? Program { get; private set; }

    public FieldSymbol Param(string name, FieldTypeSchema type) => Action.Parameter(name, type);

    public ActionBuilder Role(string participantRole)
    {
        Action.ParticipantRole = participantRole;
        return this;
    }

    public ActionBuilder Requires(Expr guard)
    {
        Action.Guard = guard;
        return this;
    }

    public ActionBuilder Candidates(string parameterName, Query query)
    {
        Action.CandidateParameter = parameterName;
        Action.CandidateQuery = query;
        return this;
    }

    public ActionBuilder Choose(string parameterName, Expr value)
    {
        Action.Choice(parameterName, value);
        return this;
    }

    public ActionSymbol Body(params Inst[] instructions)
    {
        Action.Body = new Block(instructions);

        // The action program is a first-class procedure so that the linker validates and schedules it uniformly.
        var program = new ProcedureSymbol(Action.Name + "$program");
        foreach (var parameter in Action.Parameters)
        {
            program.Parameters.Add(new FieldSymbol(parameter.Name, parameter.Type, null, FieldVisibility.Public,
                FieldLifetime.ExecutionOnly, parameter.DeclarationIndex) { IsPayloadField = true });
        }
        program.Body = Action.Body;
        Program = program;
        Action.ProgramSymbol = program;
        Package.AddDeclaration(program);
        AttachLocalFields(program);
        return Action;
    }

    internal void AttachLocalFields(ProcedureSymbol program)
    {
        program.Locals.Clear();
        program.Locals.AddRange(BuildLocalFields());
    }
}
