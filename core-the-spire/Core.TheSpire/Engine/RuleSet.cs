using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace CoreTheSpire.Engine;

public abstract record Resolution;

public sealed record TypeResolution(EntityTypeDefinition Type) : Resolution;

public enum FieldOwnerKind
{
    Entity = 0,
    EventPayload = 1,
    TerminalField = 2,
    ComputeInput = 3,
    ComputeOutput = 4,
}

public sealed record FieldResolution(FieldOwnerKind OwnerKind, int OwnerId, int Index, FieldDefinition Field) : Resolution;

public sealed record CollectionResolution(CollectionDefinition Collection) : Resolution;

public sealed record RelationResolution(RelationDefinition Relation) : Resolution;

public sealed record ScopeResolution(ScopeTypeDefinition ScopeType) : Resolution;

public sealed record ProcedureResolution(ProcedureDefinition Procedure) : Resolution;

public sealed record EventResolution(EventDefinition Event) : Resolution;

public sealed record TerminalResolution(TerminalResultDefinition Terminal) : Resolution;

public sealed record ComputeResolution(ComputeAssetDefinition Asset) : Resolution;

public sealed record EnumTypeResolution(EnumTypeDefinition Enum) : Resolution;

public sealed record EnumMemberResolution(EnumTypeDefinition Enum, int Ordinal) : Resolution;

public sealed record AnchorResolution(string Name) : Resolution;

/// <summary>
/// Links built packages into one immutable rule set: validates dependencies and duplicates, applies explicit
/// overrides and field extensions, assigns dense ids and the field layout, fills the resolution table, runs the
/// IR type check, and produces a reproducible rule set identity.
/// </summary>
public sealed class RuleSetBuilder
{
    public const string EngineVersion = "engine/ir@0.3";

    private readonly List<EnginePackage> _packages = [];
    private readonly Dictionary<int, string> _backendDescriptors = [];
    private bool _built;

    public NumericProfile NumericProfile { get; set; } = NumericProfile.Default;

    /// <summary>Compute backend versions participate in the identity: script or backend changes are visible.</summary>
    public void DescribeBackend(int backendId, string descriptor)
    {
        EnsureNotBuilt();
        _backendDescriptors[backendId] = descriptor;
    }

    public RuleSetBuilder Add(EnginePackage package)
    {
        EnsureNotBuilt();
        ArgumentNullException.ThrowIfNull(package);
        if (_packages.Any(p => string.Equals(p.PackageId, package.PackageId, StringComparison.Ordinal)))
            throw Faults.Of(EngineFaultCode.DuplicateDeclaration,
                $"package id '{package.PackageId}' was added more than once");
        _packages.Add(package);
        return this;
    }

    private void EnsureNotBuilt()
    {
        if (_built) throw Faults.Schema("rule set builder is single-use");
    }

    public RuleSet Build()
    {
        EnsureNotBuilt();
        _built = true;

        ValidateDependencies();

        var builder = new Linker(_packages, NumericProfile, _backendDescriptors);
        return builder.Link();
    }

    private void ValidateDependencies()
    {
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < _packages.Count; i++) seen[_packages[i].PackageId] = i;

        for (var i = 0; i < _packages.Count; i++)
        {
            var package = _packages[i];
            if (!string.Equals(package.EngineCompatibility, EngineVersion, StringComparison.Ordinal))
            {
                throw Faults.Schema(
                    $"package '{package.PackageId}' declares engine compatibility '{package.EngineCompatibility}' " +
                    $"but this engine implements '{EngineVersion}'");
            }

            foreach (var dependency in package.Dependencies)
            {
                var id = dependency.Split('@')[0];
                if (!seen.TryGetValue(id, out var index))
                    throw Faults.Of(EngineFaultCode.MissingDependency,
                        $"package '{package.PackageId}' depends on '{id}' which is not part of the rule set");
                if (index >= i)
                    throw Faults.Of(EngineFaultCode.DependencyCycle,
                        $"package '{package.PackageId}' is declared before its dependency '{id}'; " +
                        "package order must be a topological order");
            }
        }
    }

    private sealed class Linker
    {
        private readonly List<EnginePackage> _packages;
        private readonly NumericProfile _numericProfile;
        private readonly Dictionary<int, string> _backendDescriptors;
        private readonly Dictionary<string, EntityTypeSymbol> _typeWinners = new(StringComparer.Ordinal);
        private readonly Dictionary<string, EnumTypeSymbol> _enumWinners = new(StringComparer.Ordinal);
        private readonly Dictionary<string, CollectionSymbol> _collectionWinners = new(StringComparer.Ordinal);
        private readonly Dictionary<string, RelationSymbol> _relationWinners = new(StringComparer.Ordinal);
        private readonly Dictionary<string, ScopeTypeSymbol> _scopeWinners = new(StringComparer.Ordinal);
        private readonly Dictionary<string, ProcedureSymbol> _procedureWinners = new(StringComparer.Ordinal);
        private readonly Dictionary<string, EventSymbol> _eventWinners = new(StringComparer.Ordinal);
        private readonly Dictionary<string, TerminalResultSymbol> _terminalWinners = new(StringComparer.Ordinal);
        private readonly Dictionary<string, ComputeAssetSymbol> _computeWinners = new(StringComparer.Ordinal);
        private readonly Dictionary<string, ProjectionSymbol> _projectionWinners = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<FieldSymbol>> _mergedFields = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _declaringPackage = new(StringComparer.Ordinal);
        private readonly Dictionary<EnginePackage, int> _packageIndices = [];
        private readonly Dictionary<string, string> _ownerFields = new(StringComparer.Ordinal);

        public Linker(List<EnginePackage> packages, NumericProfile numericProfile,
            Dictionary<int, string> backendDescriptors)
        {
            _packages = packages;
            _numericProfile = numericProfile;
            _backendDescriptors = backendDescriptors;
            for (var i = 0; i < packages.Count; i++) _packageIndices.Add(packages[i], i);
        }

        private int PackageIndex(EnginePackage package) => _packageIndices[package];

        public RuleSet Link()
        {
            CollectWinners();
            var tables = AssignIds();
            var resolutions = BuildResolutions(tables);
            IrTypeChecker.Check(tables, resolutions, _packages);
            var identity = ComputeIdentity(tables, resolutions);
            return new RuleSet(identity, _packages, tables, resolutions, _numericProfile);
        }

        // -- declaration collection -----------------------------------------------------------------------------

        private void CollectWinners()
        {
            foreach (var package in _packages)
            {
                foreach (var declaration in package.Declarations)
                {
                    switch (declaration)
                    {
                        case ExternalSymbol:
                            break;
                        case EnumTypeSymbol enumType:
                            Win(_enumWinners, enumType, package);
                            break;
                        case EntityTypeSymbol entityType:
                            WinEntityType(entityType, package);
                            break;
                        case CollectionSymbol collection:
                            Win(_collectionWinners, collection, package);
                            break;
                        case RelationSymbol relation:
                            Win(_relationWinners, relation, package);
                            break;
                        case ScopeTypeSymbol scope:
                            Win(_scopeWinners, scope, package);
                            break;
                        case ProcedureSymbol procedure:
                            Win(_procedureWinners, procedure, package);
                            break;
                        case EventSymbol eventType:
                            Win(_eventWinners, eventType, package);
                            break;
                        case TerminalResultSymbol terminal:
                            Win(_terminalWinners, terminal, package);
                            break;
                        case ComputeAssetSymbol compute:
                            Win(_computeWinners, compute, package);
                            break;
                        case ProjectionSymbol projection:
                            Win(_projectionWinners, projection, package);
                            break;

                        // Actions are addressed through the action table, never through a reference slot.
                        case ActionSymbol:
                            break;
                        default:
                            throw Faults.Schema($"unsupported declaration '{declaration.GetType().Name}'");
                    }
                }
            }
        }

        private void WinEntityType(EntityTypeSymbol symbol, EnginePackage package)
        {
            var id = symbol.QualifiedId;
            if (!_typeWinners.TryGetValue(id, out var existing))
            {
                _typeWinners.Add(id, symbol);
                _declaringPackage[id] = PackageIndex(package);
                _mergedFields[id] = [.. symbol.Fields];
                return;
            }

            if (package.Overrides.Contains(id, StringComparer.Ordinal))
            {
                _typeWinners[id] = symbol;
                _declaringPackage[id] = PackageIndex(package);
                _mergedFields[id] = [.. symbol.Fields];
                return;
            }

            if (package.Extends.Contains(id, StringComparer.Ordinal))
            {
                if (!existing.ExtensibleFields)
                {
                    throw Faults.Of(EngineFaultCode.SchemaInvalid,
                        $"package '{package.PackageId}' extends '{id}' but the declaring package did not mark it extensible");
                }

                var merged = _mergedFields[id];
                foreach (var field in symbol.Fields)
                {
                    if (merged.Any(f => string.Equals(f.Name, field.Name, StringComparison.Ordinal)))
                    {
                        throw Faults.Of(EngineFaultCode.DuplicateDeclaration,
                            $"extension of '{id}' redeclares field '{field.Name}'");
                    }
                    merged.Add(field);
                }

                if (symbol.OwnerFieldName is not null) _ownerFields[id] = symbol.OwnerFieldName;
                return;
            }

            throw Faults.Of(EngineFaultCode.UnauthorizedOverride,
                $"package '{package.PackageId}' redeclares '{id}' without listing it in Overrides or Extends");
        }

        private void Win<T>(Dictionary<string, T> winners, T symbol, EnginePackage package) where T : DeclarationSymbol
        {
            var id = symbol.QualifiedId;
            if (!winners.ContainsKey(id))
            {
                winners.Add(id, symbol);
                _declaringPackage[id] = PackageIndex(package);
                return;
            }

            if (package.Overrides.Contains(id, StringComparer.Ordinal))
            {
                winners[id] = symbol;
                _declaringPackage[id] = PackageIndex(package);
                return;
            }

            throw Faults.Of(EngineFaultCode.UnauthorizedOverride,
                $"package '{package.PackageId}' redeclares '{id}' without listing it in Overrides");
        }

        // -- dense id assignment ---------------------------------------------------------------------------------

        private LinkedTables AssignIds()
        {
            var tables = new LinkedTables();
            var refBase = 0;
            foreach (var package in _packages)
            {
                tables.RefBases[package.PackageId] = refBase;
                refBase += package.RefTargets.Count;
            }

            foreach (var package in _packages)
            {
                var enums = package.Declarations.OfType<EnumTypeSymbol>()
                    .Where(s => ReferenceEquals(_enumWinners[s.QualifiedId], s));
                foreach (var symbol in enums)
                {
                    var id = tables.Enums.Count;
                    tables.EnumIds[symbol.QualifiedId] = id;
                    tables.Enums.Add(new EnumTypeDefinition(id, symbol.QualifiedId, Array.AsReadOnly(symbol.Members.ToArray())));
                }
            }

            // First pass: every entity type gets its dense id before any field shape is resolved, so a field may
            // reference a type declared later in the same package. Without this, only backward references linked and
            // mutually recursive types were impossible to declare.
            foreach (var package in _packages)
            {
                var types = package.Declarations.OfType<EntityTypeSymbol>()
                    .Where(s => ReferenceEquals(_typeWinners[s.QualifiedId], s));
                foreach (var symbol in types)
                {
                    var id = tables.Types.Count;
                    tables.TypeIds[symbol.QualifiedId] = id;
                    tables.Types.Add(new EntityTypeDefinition(id, symbol.QualifiedId, [], PackageIndex(package)));
                }
            }

            // Second pass: resolve every field shape now that all type ids exist.
            foreach (var package in _packages)
            {
                var types = package.Declarations.OfType<EntityTypeSymbol>()
                    .Where(s => ReferenceEquals(_typeWinners[s.QualifiedId], s));
                foreach (var symbol in types)
                {
                    var id = tables.TypeIds[symbol.QualifiedId];
                    tables.Types[id] = tables.Types[id] with
                    {
                        Fields = BuildFields(symbol.QualifiedId, _mergedFields[symbol.QualifiedId], tables),
                    };
                }
            }

            foreach (var package in _packages)
            {
                foreach (var symbol in package.Declarations.OfType<ScopeTypeSymbol>()
                             .Where(s => ReferenceEquals(_scopeWinners[s.QualifiedId], s)))
                {
                    var id = tables.ScopeTypes.Count;
                    tables.ScopeIds[symbol.QualifiedId] = id;
                    tables.ScopeTypes.Add(new ScopeTypeDefinition(id, symbol.QualifiedId, -1));
                }
            }

            // Scope types may name a parent declared later in the package, so parents resolve in a second pass.
            foreach (var (qualifiedId, id) in tables.ScopeIds)
            {
                var symbol = _scopeWinners[qualifiedId];
                if (symbol.Parent is null) continue;
                if (!tables.ScopeIds.TryGetValue(symbol.Parent.QualifiedId, out var parentId))
                {
                    throw Faults.Unresolved(
                        $"scope type '{qualifiedId}' declares parent '{symbol.Parent.QualifiedId}' which is not " +
                        "part of the rule set");
                }

                tables.ScopeTypes[id] = tables.ScopeTypes[id] with { ParentScopeTypeId = parentId };
            }

            foreach (var package in _packages)
            {
                foreach (var symbol in package.Declarations.OfType<CollectionSymbol>()
                             .Where(s => ReferenceEquals(_collectionWinners[s.QualifiedId], s)))
                {
                    var id = tables.Collections.Count;
                    tables.CollectionIds[symbol.QualifiedId] = id;
                    tables.Collections.Add(new CollectionDefinition(id, symbol.QualifiedId,
                        RequireTypeId(tables, symbol.ElementType), symbol.Ordered, symbol.MembershipGroup,
                        symbol.Capacity, symbol.IdentityVisible, symbol.SizeVisible, symbol.OrderVisible));
                }
            }

            foreach (var package in _packages)
            {
                foreach (var symbol in package.Declarations.OfType<RelationSymbol>()
                             .Where(s => ReferenceEquals(_relationWinners[s.QualifiedId], s)))
                {
                    var id = tables.Relations.Count;
                    tables.RelationIds[symbol.QualifiedId] = id;
                    tables.Relations.Add(new RelationDefinition(id, symbol.QualifiedId,
                        RequireTypeId(tables, symbol.TargetType), symbol.Many, symbol.Public));
                }
            }

            foreach (var package in _packages)
            {
                foreach (var symbol in package.Declarations.OfType<EventSymbol>()
                             .Where(s => ReferenceEquals(_eventWinners[s.QualifiedId], s)))
                {
                    var id = tables.Events.Count;
                    tables.EventIds[symbol.QualifiedId] = id;
                    tables.Events.Add(new EventDefinition(id, symbol.QualifiedId,
                        BuildPayloadFields(symbol.Payload, tables, symbol.QualifiedId, 0)));
                }
            }

            foreach (var package in _packages)
            {
                foreach (var symbol in package.Declarations.OfType<TerminalResultSymbol>()
                             .Where(s => ReferenceEquals(_terminalWinners[s.QualifiedId], s)))
                {
                    var id = tables.Terminals.Count;
                    tables.TerminalIds[symbol.QualifiedId] = id;
                    tables.Terminals.Add(new TerminalResultDefinition(id, symbol.QualifiedId,
                        BuildPayloadFields(symbol.Fields, tables, symbol.QualifiedId, 0)));
                }
            }

            foreach (var package in _packages)
            {
                foreach (var symbol in package.Declarations.OfType<ComputeAssetSymbol>()
                             .Where(s => ReferenceEquals(_computeWinners[s.QualifiedId], s)))
                {
                    var id = tables.ComputeAssets.Count;
                    tables.ComputeIds[symbol.QualifiedId] = id;
                    tables.ComputeAssets.Add(new ComputeAssetDefinition(id, symbol.QualifiedId, symbol.BackendId,
                        symbol.SourcePath, symbol.SourceHash, symbol.Entry,
                        BuildPayloadFields(symbol.Inputs, tables, symbol.QualifiedId, 0),
                        BuildPayloadFields(symbol.Outputs, tables, symbol.QualifiedId, 0),
                        symbol.Limits, symbol.NumericProfile, PackageIndex(package),
                        tables.RefBases[package.PackageId]));
                }
            }

            foreach (var package in _packages)
            {
                foreach (var symbol in package.Procedures.Where(s => ReferenceEquals(_procedureWinners[s.QualifiedId], s)))
                {
                    if (symbol.Body is null)
                        throw Faults.Schema($"procedure '{symbol.QualifiedId}' has no body");
                    var id = tables.Procedures.Count;
                    tables.ProcedureIds[symbol.QualifiedId] = id;
                    tables.Procedures.Add(new ProcedureDefinition(id, symbol.QualifiedId,
                        BuildPayloadFields(symbol.Parameters, tables, symbol.QualifiedId, 0),
                        BuildPayloadFields(symbol.Locals, tables, symbol.QualifiedId, 0),
                        symbol.Body, symbol.IsPure, symbol.ResultType, PackageIndex(package),
                        tables.RefBases[package.PackageId]));
                }
            }

            foreach (var package in _packages)
            {
                foreach (var symbol in package.Declarations.OfType<ActionSymbol>())
                {
                    var program = symbol.ProgramSymbol
                        ?? throw Faults.Schema($"action '{symbol.QualifiedId}' has no compiled program");
                    if (!tables.ProcedureIds.TryGetValue(program.QualifiedId, out var programId))
                        throw Faults.Unresolved($"action '{symbol.QualifiedId}' program was not linked");
                    var programDefinition = tables.Procedures[programId];
                    var parameters = programDefinition.Parameters;
                    var candidateIndex = -1;
                    if (symbol.CandidateParameter is not null)
                    {
                        candidateIndex = IndexOfParameter(parameters, symbol.CandidateParameter, symbol.QualifiedId);
                        if (symbol.CandidateQuery is null)
                            throw Faults.Schema($"action '{symbol.QualifiedId}' declares a candidate parameter without a query");
                    }

                    var choices = new Dictionary<int, IReadOnlyList<Expr>>();
                    foreach (var (name, values) in symbol.Choices)
                    {
                        var index = IndexOfParameter(parameters, name, symbol.QualifiedId);
                        choices[index] = Array.AsReadOnly(values.ToArray());
                    }

                    tables.Actions.Add(new ActionDefinition(tables.Actions.Count, symbol.QualifiedId,
                        symbol.ParticipantRole, parameters, symbol.Guard, candidateIndex, symbol.CandidateQuery,
                        new System.Collections.ObjectModel.ReadOnlyDictionary<int, IReadOnlyList<Expr>>(choices), programDefinition, PackageIndex(package), tables.RefBases[package.PackageId]));
                }
            }

            foreach (var package in _packages)
            {
                foreach (var symbol in package.Declarations.OfType<ProjectionSymbol>()
                             .Where(s => ReferenceEquals(_projectionWinners[s.QualifiedId], s)))
                {
                    var projections = new Dictionary<int, TypeProjection>();
                    foreach (var type in symbol.Types)
                    {
                        var typeId = RequireTypeId(tables, type.Type);
                        var definition = tables.Types[typeId];
                        var ownerField = type.OwnerField ?? _ownerFields.GetValueOrDefault(type.Type.QualifiedId)
                            ?? type.Type.OwnerFieldName;
                        var ownerIndex = -1;
                        if (ownerField is not null && !definition.TryFieldIndex(ownerField, out ownerIndex))
                        {
                            throw Faults.Unresolved(
                                $"projection '{symbol.QualifiedId}' names owner field '{ownerField}' which " +
                                $"'{definition.QualifiedId}' does not declare");
                        }
                        projections[typeId] = new TypeProjection(definition, type.Include, ownerIndex,
                            type.IncludeHiddenFields);
                    }

                    var reveals = new List<RevealProjection>();
                    foreach (var reveal in symbol.Reveals)
                    {
                        var authorityId = RequireTypeId(tables, reveal.AuthorityType);
                        if (!projections.TryGetValue(authorityId, out var authority) || !authority.Include)
                        {
                            throw Faults.Schema(
                                $"projection '{symbol.QualifiedId}' declares a reveal through " +
                                $"'{reveal.AuthorityType.QualifiedId}', which it does not include");
                        }

                        if (authority.OwnerFieldIndex < 0)
                        {
                            throw Faults.Schema(
                                $"projection '{symbol.QualifiedId}' declares a reveal through " +
                                $"'{reveal.AuthorityType.QualifiedId}', which declares no viewer field");
                        }

                        var relationId = RequireId(tables.RelationIds, reveal.Relation.QualifiedId, "relation");
                        var targetTypeId = tables.Relations[relationId].TargetTypeId;
                        if (!projections.TryGetValue(targetTypeId, out var target) || !target.Include)
                        {
                            throw Faults.Schema(
                                $"projection '{symbol.QualifiedId}' reveals targets of " +
                                $"'{reveal.Relation.QualifiedId}', whose type it does not include");
                        }

                        reveals.Add(new RevealProjection(authorityId, authority.OwnerFieldIndex, relationId));
                    }

                    var id = tables.Projections.Count;
                    tables.ProjectionIds[symbol.QualifiedId] = id;
                    tables.Projections.Add(new ProjectionDefinition(id, symbol.QualifiedId, symbol.ParticipantRole,
                        new System.Collections.ObjectModel.ReadOnlyDictionary<int, TypeProjection>(projections),
                        symbol.IncludeCollections, symbol.IncludeRelations, reveals.AsReadOnly()));
                }
            }

            var order = 0;
            foreach (var package in _packages)
            {
                foreach (var subscription in package.Subscriptions)
                {
                    var reaction = RequireProcedure(tables, subscription.Reaction);
                    var eventType = RequireEvent(tables, subscription.EventType);
                    var scopeIndex = -1;
                    if (subscription.ScopePayloadField is not null && !eventType.TryPayloadIndex(subscription.ScopePayloadField, out scopeIndex))
                    {
                        throw Faults.Unresolved(
                            $"subscription on '{eventType.QualifiedId}' names scope payload " +
                            $"'{subscription.ScopePayloadField}' which the event does not declare");
                    }

                    tables.Subscriptions.Add(new SubscriptionDefinition(eventType, reaction, subscription.Priority,
                        subscription.Recheck, scopeIndex, PackageIndex(package),
                        tables.RefBases[package.PackageId], order++));
                }
            }

            foreach (var package in _packages)
            {
                foreach (var procedure in package.SessionStartProcedures)
                    tables.SessionStart.Add(RequireProcedure(tables, procedure));
            }

            return tables;
        }

        private static int IndexOfParameter(IReadOnlyList<FieldDefinition> parameters, string name, string owner)
        {
            for (var i = 0; i < parameters.Count; i++)
            {
                if (string.Equals(parameters[i].Name, name, StringComparison.Ordinal)) return i;
            }
            throw Faults.Unresolved($"'{owner}' has no parameter '{name}'");
        }

        private static IReadOnlyList<FieldDefinition> BuildFields(string qualifiedId,
            IReadOnlyList<FieldSymbol> symbols, LinkedTables tables)
        {
            var fields = new List<FieldDefinition>(symbols.Count);
            foreach (var symbol in symbols)
            {
                var (enumId, targetId, elementTargetId, elementEnumId) = ResolveReferenceIds(symbol.Type, tables);
                var defaultValue = symbol.DefaultValue ?? DefaultFor(symbol.Type);
                fields.Add(new FieldDefinition(symbol.Name, symbol.Type, fields.Count, enumId, targetId,
                    elementTargetId, elementEnumId, symbol.Optional, defaultValue, symbol.Visibility,
                    symbol.Lifetime));
            }

            return fields.AsReadOnly();
        }

        private static IReadOnlyList<FieldDefinition> BuildPayloadFields(IReadOnlyList<FieldSymbol> symbols,
            LinkedTables tables, string owner, int _)
        {
            var fields = new List<FieldDefinition>(symbols.Count);
            foreach (var symbol in symbols)
            {
                var (enumId, targetId, elementTargetId, elementEnumId) = ResolveReferenceIds(symbol.Type, tables);
                fields.Add(new FieldDefinition(symbol.Name, symbol.Type, fields.Count, enumId, targetId,
                    elementTargetId, elementEnumId, true, symbol.DefaultValue ?? DefaultFor(symbol.Type),
                    symbol.Visibility, symbol.Lifetime));
            }

            return fields.AsReadOnly();
        }

        private static (int EnumId, int TargetId, int ElementTargetId, int ElementEnumId) ResolveReferenceIds(
            FieldTypeSchema type, LinkedTables tables)
        {
            var enumId = -1;
            var targetId = -1;
            var elementTargetId = -1;
            var elementEnumId = -1;
            if (type.Enum is not null) enumId = RequireEnumId(tables, type.Enum);
            if (type.Target is not null) targetId = RequireTypeId(tables, type.Target);
            if (type.Element is not null)
            {
                if (type.Element.Enum is not null) elementEnumId = RequireEnumId(tables, type.Element.Enum);
                if (type.Element.Target is not null) elementTargetId = RequireTypeId(tables, type.Element.Target);
            }

            return (enumId, targetId, elementTargetId, elementEnumId);
        }

        private static Value DefaultFor(FieldTypeSchema type) => type.Kind switch
        {
            FieldTypeKind.Bool => Value.Of(false),
            FieldTypeKind.Int => Value.Of(0L),
            FieldTypeKind.Fixed => Value.Of(0m),
            FieldTypeKind.Enum => Value.OfEnum(0),
            FieldTypeKind.String => Value.Of(string.Empty),
            FieldTypeKind.Ref => Value.None,
            FieldTypeKind.Participant => Value.OfParticipant(ParticipantId.None),
            FieldTypeKind.List => Value.OfInts(),
            _ => Value.None,
        };

        private static int RequireTypeId(LinkedTables tables, DeclarationSymbol symbol)
        {
            if (tables.TypeIds.TryGetValue(symbol.QualifiedId, out var id)) return id;
            throw Faults.Unresolved($"entity type '{symbol.QualifiedId}' is not part of the rule set");
        }

        private static int RequireEnumId(LinkedTables tables, DeclarationSymbol symbol)
        {
            if (tables.EnumIds.TryGetValue(symbol.QualifiedId, out var id)) return id;
            throw Faults.Unresolved($"enum '{symbol.QualifiedId}' is not part of the rule set");
        }

        private static ProcedureDefinition RequireProcedure(LinkedTables tables, ProcedureSymbol symbol)
        {
            if (tables.ProcedureIds.TryGetValue(symbol.QualifiedId, out var id)) return tables.Procedures[id];
            throw Faults.Unresolved($"procedure '{symbol.QualifiedId}' is not part of the rule set");
        }

        private static EventDefinition RequireEvent(LinkedTables tables, EventSymbol symbol)
        {
            if (tables.EventIds.TryGetValue(symbol.QualifiedId, out var id)) return tables.Events[id];
            throw Faults.Unresolved($"event '{symbol.QualifiedId}' is not part of the rule set");
        }

        // -- resolution table ------------------------------------------------------------------------------------

        private Resolution[] BuildResolutions(LinkedTables tables)
        {
            var total = _packages.Sum(p => p.RefTargets.Count);
            var resolutions = new Resolution[total];

            foreach (var package in _packages)
            {
                var refBase = tables.RefBases[package.PackageId];
                for (var slot = 0; slot < package.RefTargets.Count; slot++)
                {
                    resolutions[refBase + slot] = ResolveTarget(package.RefTargets[slot], tables, package);
                }
            }

            return resolutions;
        }

        private Resolution ResolveTarget(object target, LinkedTables tables, EnginePackage declaringPackage)
        {
            switch (target)
            {
                case EntityTypeSymbol type:
                    return new TypeResolution(
                        tables.Types[RequireTypeId(tables, type)]);

                case FieldSymbol field:
                    return ResolveField(field, tables);

                case CollectionSymbol collection:
                    return new CollectionResolution(
                        tables.Collections[RequireId(tables.CollectionIds, collection.QualifiedId, "collection")]);

                case RelationSymbol relation:
                    return new RelationResolution(
                        tables.Relations[RequireId(tables.RelationIds, relation.QualifiedId, "relation")]);

                case ScopeTypeSymbol scope:
                    return new ScopeResolution(
                        tables.ScopeTypes[RequireId(tables.ScopeIds, scope.QualifiedId, "scope type")]);

                case ProcedureSymbol procedure:
                    return new ProcedureResolution(RequireProcedure(tables, procedure));

                case EventSymbol eventType:
                    return new EventResolution(RequireEvent(tables, eventType));

                case TerminalResultSymbol terminal:
                    return new TerminalResolution(
                        tables.Terminals[RequireId(tables.TerminalIds, terminal.QualifiedId, "terminal result")]);

                case ComputeAssetSymbol compute:
                    return new ComputeResolution(
                        tables.ComputeAssets[RequireId(tables.ComputeIds, compute.QualifiedId, "compute asset")]);

                case EnumTypeSymbol enumType:
                    return new EnumTypeResolution(tables.Enums[RequireEnumId(tables, enumType)]);

                case EnginePackageBuilder.AnchorSymbol anchor:
                    return new AnchorResolution(anchor.Name);

                case ExternalSymbol external:
                    return ResolveExternal(external, tables);

                default:
                    throw Faults.Unresolved(
                        $"package '{declaringPackage.PackageId}' references an unsupported declaration " +
                        $"'{target.GetType().Name}'");
            }
        }

        /// <summary>Resolves a reference to another package's declaration by qualified id.</summary>
        private Resolution ResolveExternal(ExternalSymbol external, LinkedTables tables)
        {
            var id = external.QualifiedId;
            if (_typeWinners.TryGetValue(id, out var type))
                return new TypeResolution(tables.Types[RequireTypeId(tables, type)]);
            if (_enumWinners.TryGetValue(id, out var enumType))
                return new EnumTypeResolution(tables.Enums[RequireEnumId(tables, enumType)]);
            if (_collectionWinners.ContainsKey(id))
                return new CollectionResolution(tables.Collections[RequireId(tables.CollectionIds, id, "collection")]);
            if (_relationWinners.ContainsKey(id))
                return new RelationResolution(tables.Relations[RequireId(tables.RelationIds, id, "relation")]);
            if (_scopeWinners.ContainsKey(id))
                return new ScopeResolution(tables.ScopeTypes[RequireId(tables.ScopeIds, id, "scope type")]);
            if (_procedureWinners.TryGetValue(id, out var procedure))
                return new ProcedureResolution(RequireProcedure(tables, procedure));
            if (_eventWinners.TryGetValue(id, out var eventType))
                return new EventResolution(RequireEvent(tables, eventType));
            if (_terminalWinners.ContainsKey(id))
                return new TerminalResolution(tables.Terminals[RequireId(tables.TerminalIds, id, "terminal result")]);
            if (_computeWinners.ContainsKey(id))
                return new ComputeResolution(tables.ComputeAssets[RequireId(tables.ComputeIds, id, "compute asset")]);
            throw Faults.Unresolved($"external declaration '{id}' is not part of the rule set");
        }

        private Resolution ResolveField(FieldSymbol field, LinkedTables tables)
        {
            if (field.Owner is EntityTypeSymbol owner)
            {
                var typeId = RequireTypeId(tables, owner);
                var type = tables.Types[typeId];
                if (!type.TryFieldIndex(field.Name, out var index))
                {
                    throw Faults.Unresolved(
                        $"field '{field.Name}' does not exist on linked type '{type.QualifiedId}'");
                }

                return new FieldResolution(FieldOwnerKind.Entity, typeId, index, type.Fields[index]);
            }

            foreach (var (qualifiedId, id) in tables.EventIds)
            {
                var eventType = tables.Events[id];
                if (eventType.TryPayloadIndex(field.Name, out var index) && ReferenceEquals(
                        _eventWinners[qualifiedId], field.OwnerEvent))
                {
                    return new FieldResolution(FieldOwnerKind.EventPayload, id, index, eventType.Payload[index]);
                }
            }

            foreach (var (qualifiedId, id) in tables.TerminalIds)
            {
                if (ReferenceEquals(_terminalWinners[qualifiedId], field.OwnerTerminal))
                {
                    var terminal = tables.Terminals[id];
                    for (var index = 0; index < terminal.Fields.Count; index++)
                    {
                        if (string.Equals(terminal.Fields[index].Name, field.Name, StringComparison.Ordinal))
                            return new FieldResolution(FieldOwnerKind.TerminalField, id, index, terminal.Fields[index]);
                    }
                }
            }

            foreach (var (qualifiedId, id) in tables.ComputeIds)
            {
                if (!ReferenceEquals(_computeWinners[qualifiedId], field.OwnerCompute)) continue;
                var asset = tables.ComputeAssets[id];
                var inputs = field.IsComputeOutput ? null : asset.Inputs;
                var outputs = field.IsComputeOutput ? asset.Outputs : null;
                var list = inputs ?? outputs!;
                for (var index = 0; index < list.Count; index++)
                {
                    if (string.Equals(list[index].Name, field.Name, StringComparison.Ordinal))
                    {
                        return new FieldResolution(
                            field.IsComputeOutput ? FieldOwnerKind.ComputeOutput : FieldOwnerKind.ComputeInput,
                            id, index, list[index]);
                    }
                }
            }

            throw Faults.Unresolved($"field '{field.Name}' does not belong to any linked declaration");
        }

        private static int RequireId(Dictionary<string, int> ids, string qualifiedId, string kind)
        {
            if (ids.TryGetValue(qualifiedId, out var id)) return id;
            throw Faults.Unresolved($"{kind} '{qualifiedId}' is not part of the rule set");
        }

        // -- identity --------------------------------------------------------------------------------------------

        private string ComputeIdentity(LinkedTables tables, Resolution[] resolutions)
        {
            var text = new StringBuilder();
            text.Append("engine=").Append(EngineVersion).Append('\n');
            text.Append("numeric=").Append(_numericProfile).Append('\n');
            foreach (var (backend, descriptor) in _backendDescriptors.OrderBy(pair => pair.Key))
                text.Append("backend:").Append(backend).Append('=').Append(descriptor).Append('\n');
            foreach (var package in _packages)
            {
                text.Append("package=").Append(package.PackageId).Append('@').Append(package.Version)
                    .Append(" compiler=").Append(package.CompilerIdentity)
                    .Append('#').Append(package.ContentHash)
                    .Append(" overrides=").Append(string.Join(",", package.Overrides.OrderBy(o => o, StringComparer.Ordinal)))
                    .Append(" extends=").Append(string.Join(",", package.Extends.OrderBy(o => o, StringComparer.Ordinal)))
                    .Append('\n');
            }

            // The identity covers the frozen linked tables, not a summary of them: every semantic dimension that can
            // change execution or observation must move the identity. K-01's "a semantic change changes the identity"
            // is only true if this dump is complete, so each table is dumped field by field instead of by name.
            foreach (var type in tables.Types)
                text.Append("type=").Append(type.Id).Append(':').Append(type.QualifiedId)
                    .Append('@').Append(type.PackageIndex).Append('(')
                    .Append(string.Join("|", type.Fields.Select(DumpField))).Append(")\n");

            foreach (var enumType in tables.Enums)
                text.Append("enum=").Append(enumType.Id).Append(':').Append(enumType.QualifiedId).Append('(')
                    .Append(string.Join(",", enumType.Members)).Append(")\n");

            foreach (var collection in tables.Collections)
                text.Append("coll=").Append(collection.Id).Append(':').Append(collection.QualifiedId)
                    .Append(" element=").Append(collection.ElementTypeId)
                    .Append(" ordered=").Append(collection.Ordered)
                    .Append(" group=").Append(collection.MembershipGroup ?? "-")
                    .Append(" capacity=").Append(collection.Capacity)
                    .Append(" identity=").Append(collection.IdentityVisible)
                    .Append(" size=").Append(collection.SizeVisible)
                    .Append(" order=").Append(collection.OrderVisible).Append('\n');

            foreach (var relation in tables.Relations)
                text.Append("rel=").Append(relation.Id).Append(':').Append(relation.QualifiedId)
                    .Append(" target=").Append(relation.TargetTypeId)
                    .Append(" many=").Append(relation.Many)
                    .Append(" public=").Append(relation.Public).Append('\n');

            foreach (var scope in tables.ScopeTypes)
                text.Append("scope=").Append(scope.Id).Append(':').Append(scope.QualifiedId)
                    .Append(" parent=").Append(scope.ParentScopeTypeId).Append('\n');

            foreach (var eventType in tables.Events)
                text.Append("event=").Append(eventType.Id).Append(':').Append(eventType.QualifiedId).Append('(')
                    .Append(string.Join("|", eventType.Payload.Select(DumpField))).Append(")\n");

            foreach (var terminal in tables.Terminals)
                text.Append("terminal=").Append(terminal.Id).Append(':').Append(terminal.QualifiedId).Append('(')
                    .Append(string.Join("|", terminal.Fields.Select(DumpField))).Append(")\n");

            foreach (var asset in tables.ComputeAssets)
                text.Append("compute=").Append(asset.Id).Append(':').Append(asset.QualifiedId)
                    .Append('@').Append(asset.BackendId).Append('#').Append(asset.SourceHash)
                    .Append('!').Append(asset.SourcePath).Append('!').Append(asset.Entry)
                    .Append(" limits=").Append(asset.Limits)
                    .Append(" numeric=").Append(asset.NumericProfile)
                    .Append(" in(").Append(string.Join("|", asset.Inputs.Select(DumpField))).Append(')')
                    .Append(" out(").Append(string.Join("|", asset.Outputs.Select(DumpField))).Append(")\n");

            foreach (var procedure in tables.Procedures)
                text.Append("proc=").Append(procedure.Id).Append(':').Append(procedure.QualifiedId)
                    .Append(" pure=").Append(procedure.IsPure)
                    .Append(" result=").Append(procedure.ResultType is null ? "-" : DumpType(procedure.ResultType))
                    .Append(" params(").Append(string.Join("|", procedure.Parameters.Select(DumpField))).Append(')')
                    .Append(" locals(").Append(string.Join("|", procedure.Locals.Select(DumpField))).Append(')')
                    .Append('=').Append(IrDumper.Dump(procedure.Body)).Append('\n');

            foreach (var action in tables.Actions)
            {
                text.Append("action=").Append(action.Id).Append(':').Append(action.QualifiedId)
                    .Append(" role=").Append(action.ParticipantRole ?? "-")
                    .Append(" params(").Append(string.Join("|", action.Parameters.Select(DumpField))).Append(')')
                    .Append(" candidateParam=").Append(action.CandidateParameterIndex)
                    .Append(" candidateQuery=")
                    .Append(action.CandidateQuery is null ? "-" : IrDumper.Dump(action.CandidateQuery))
                    .Append(" guard=").Append(action.Guard is null ? "-" : IrDumper.Dump(action.Guard))
                    .Append(" program=").Append(action.Program.Id)
                    .Append(" choices=");
                foreach (var (index, values) in action.Choices.OrderBy(pair => pair.Key))
                    text.Append(index).Append('[').Append(string.Join(",", values.Select(expression => IrDumper.Dump(expression)))).Append(']');
                text.Append('\n');
            }

            foreach (var projection in tables.Projections)
            {
                text.Append("projection=").Append(projection.Id).Append(':').Append(projection.QualifiedId)
                    .Append(" role=").Append(projection.ParticipantRole)
                    .Append(" collections=").Append(projection.IncludeCollections)
                    .Append(" relations=").Append(projection.IncludeRelations).Append(" types=");
                foreach (var (typeId, type) in projection.Types.OrderBy(pair => pair.Key))
                    text.Append(typeId).Append(':').Append(type.Include).Append(':').Append(type.OwnerFieldIndex)
                        .Append(':').Append(type.IncludeHiddenFields).Append(';');
                text.Append(" reveals=");
                foreach (var reveal in projection.Reveals)
                    text.Append(reveal.AuthorityTypeId).Append(':').Append(reveal.ViewerFieldIndex)
                        .Append("->").Append(reveal.RelationId).Append(';');
                text.Append('\n');
            }

            foreach (var subscription in tables.Subscriptions)
                text.Append("sub=").Append(subscription.EventType.Id).Append("->").Append(subscription.Reaction.Id)
                    .Append(" priority=").Append(subscription.Priority)
                    .Append(" order=").Append(subscription.DeclarationOrder)
                    .Append(" scopePayload=").Append(subscription.ScopePayloadIndex)
                    .Append(" recheck=")
                    .Append(subscription.Recheck is null ? "-" : IrDumper.Dump(subscription.Recheck))
                    .Append('\n');

            text.Append("sessionStart=")
                .Append(string.Join(",", tables.SessionStart.Select(procedure => procedure.Id))).Append('\n');

            // The resolution table is semantics, not bookkeeping: a reference slot that binds to a different
            // declaration is a different rule set even when every schema table looks identical. Without this, two
            // rule sets whose IR differs only in what a slot resolves to shared an identity.
            for (var index = 0; index < resolutions.Length; index++)
                text.Append("ref=").Append(index).Append(':').Append(DumpResolution(resolutions[index])).Append('\n');

            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())))
                .ToLowerInvariant()[..16];
        }

        private static string DumpResolution(Resolution resolution) => resolution switch
        {
            TypeResolution type => "type:" + type.Type.Id,
            FieldResolution field => $"field:{field.OwnerKind}:{field.OwnerId}:{field.Index}",
            CollectionResolution collection => "coll:" + collection.Collection.Id,
            RelationResolution relation => "rel:" + relation.Relation.Id,
            ScopeResolution scope => "scope:" + scope.ScopeType.Id,
            ProcedureResolution procedure => "proc:" + procedure.Procedure.Id,
            EventResolution eventType => "event:" + eventType.Event.Id,
            TerminalResolution terminal => "terminal:" + terminal.Terminal.Id,
            ComputeResolution compute => "compute:" + compute.Asset.Id,
            EnumTypeResolution enumType => "enum:" + enumType.Enum.Id,
            EnumMemberResolution member => $"member:{member.Enum.Id}:{member.Ordinal}",
            AnchorResolution anchor => "anchor:" + anchor.Name,
            _ => "?" + resolution.GetType().Name,
        };

        private static string DumpField(FieldDefinition field) =>
            $"{field.Name}:{DumpType(field.Shape)}:{field.Index}:{field.EnumTypeId}:{field.TargetTypeId}:" +
            $"{field.ElementTargetTypeId}:{field.ElementEnumTypeId}:{field.Optional}:{field.DefaultValue}:" +
            $"{field.Visibility}:{field.Lifetime}";

        private static string DumpType(FieldTypeSchema type) =>
            $"{type.Kind}/{type.Min}/{type.Max}/{type.FixedMin}/{type.FixedMax}/{type.FixedScale}/" +
            $"{type.MaxLength}/{type.Enum?.QualifiedId ?? "-"}/{type.Target?.QualifiedId ?? "-"}/" +
            $"{(type.Element is null ? "-" : DumpType(type.Element))}";
    }
}

/// <summary>Deterministic textual dump of IR used for content hashing and diagnostics.</summary>
public static class IrDumper
{
    public static string Dump(IrNode node)
    {
        var text = new StringBuilder();
        Write(node, text);
        return text.ToString();
    }

    private static void Write(IrNode node, StringBuilder text)
    {
        text.Append(node.Primitive);
        switch (node)
        {
            case ConstExpr constant:
                text.Append('(').Append(constant.Value).Append(')');
                return;
            case LocalExpr local:
                text.Append('[').Append(local.Slot).Append(']');
                return;
            case ParamExpr parameter:
                text.Append('[').Append(parameter.Slot).Append(']');
                return;
            case FieldExpr field:
                text.Append("@").Append(field.FieldRef.ToString(CultureInfo.InvariantCulture)).Append('(');
                Write(field.Target, text);
                text.Append(')');
                return;
            case EntityScopeExpr entityScope:
                text.Append('(');
                Write(entityScope.Entity, text);
                text.Append(')');
                return;
            case FrameScopeExpr:
                return;
            case ArithExpr arith:
                text.Append('(').Append(arith.Op).Append(' ');
                Write(arith.Left, text);
                text.Append(' ');
                Write(arith.Right, text);
                text.Append(')');
                return;
            case CompareExpr compare:
                text.Append('(').Append(compare.Op).Append(' ');
                Write(compare.Left, text);
                text.Append(' ');
                Write(compare.Right, text);
                text.Append(')');
                return;
            case LogicExpr logic:
                text.Append(logic.IsAnd ? "(and " : "(or ");
                Write(logic.Left, text);
                text.Append(' ');
                Write(logic.Right, text);
                text.Append(')');
                return;
            case NotExpr not:
                text.Append('(');
                Write(not.Operand, text);
                text.Append(')');
                return;
            case ConvertExpr convert:
                text.Append('(').Append(convert.Op).Append(' ');
                Write(convert.Operand, text);
                text.Append(')');
                return;
            case EnumConstExpr enumConstant:
                text.Append("@").Append(enumConstant.EnumRef).Append('.').Append(enumConstant.Member);
                return;
            case CallPureExpr call:
                text.Append("@").Append(call.ProcedureRef).Append('(');
                WriteList(call.Arguments, text);
                text.Append(')');
                return;
            case StringConcatExpr concat:
                text.Append('(');
                WriteList(concat.Parts, text);
                text.Append(')');
                return;
            case AggregateExpr aggregate:
                text.Append('(').Append(aggregate.Op).Append(' ').Append(aggregate.ItemSlot).Append(' ');
                Write(aggregate.Source, text);
                if (aggregate.ItemValue is not null)
                {
                    text.Append(' ');
                    Write(aggregate.ItemValue, text);
                }
                text.Append(')');
                return;
            case CountExpr count:
                Write(count.Source, text);
                return;
            case CollectionIndexExpr collectionIndex:
                text.Append('@').Append(collectionIndex.CollectionRef).Append(' ');
                Write(collectionIndex.Item, text);
                return;
            case ExistsExpr exists:
                Write(exists.Source, text);
                return;
            case FirstExpr first:
                Write(first.Source, text);
                return;
            case IsNoneExpr isNone:
                text.Append('(');
                Write(isNone.Operand, text);
                text.Append(')');
                return;
            case TypeQuery type:
                text.Append("@").Append(type.TypeRef).Append('/').Append(type.ScopeTypeRef);
                return;
            case CollectionQuery collection:
                text.Append("@").Append(collection.CollectionRef).Append('/').Append(collection.ScopeTypeRef);
                return;
            case RefListQuery refList:
                // 新节点必须让转储器知道，否则身份文本会漏掉"遍历引用列表"这一语义（RuleSet.cs:1262）。
                text.Append("list[").Append(refList.ListSlot).Append(']');
                return;
            case RelationQuery relation:
                text.Append('@').Append(relation.RelationRef).Append('(');
                Write(relation.Owner, text);
                text.Append(')');
                return;
            case FilterQuery filter:
                text.Append('(').Append(filter.ItemSlot).Append(' ');
                Write(filter.Source, text);
                text.Append(' ');
                Write(filter.Predicate, text);
                text.Append(')');
                return;
            case SortQuery sort:
                text.Append('(').Append(sort.ItemSlot).Append(' ').Append(sort.Descending ? "desc " : "asc ");
                Write(sort.Source, text);
                text.Append(' ');
                Write(sort.Key, text);
                text.Append(')');
                return;
            case TakeQuery take:
                text.Append('(');
                Write(take.Source, text);
                text.Append(' ');
                Write(take.Count, text);
                text.Append(')');
                return;
            case SkipQuery skip:
                text.Append('(');
                Write(skip.Source, text);
                text.Append(' ');
                Write(skip.Count, text);
                text.Append(')');
                return;
            case Block block:
                text.Append('{');
                WriteList(block.Instructions, text);
                text.Append('}');
                return;
            case SetLocalInst setLocal:
                text.Append('[').Append(setLocal.Slot).Append("]=");
                Write(setLocal.Value, text);
                return;
            case SetFieldInst setField:
                text.Append('@').Append(setField.FieldRef).Append('(');
                Write(setField.Target, text);
                text.Append(")=");
                Write(setField.Value, text);
                return;
            case CreateEntityInst create:
                text.Append('@').Append(create.TypeRef).Append('[').Append(create.ResultSlot).Append(']');
                WriteFieldInits(create.Fields, text);
                return;
            case CreateScopeInst createScope:
                text.Append('@').Append(createScope.ScopeTypeRef).Append('[').Append(createScope.ResultSlot).Append(']');
                if (createScope.Parent is not null) Write(createScope.Parent, text);
                return;
            case DestroyEntityInst destroy:
                text.Append(destroy.ScopeSubtree ? "scope " : "entity ");
                Write(destroy.Target, text);
                return;
            case AddToCollectionInst add:
                text.Append('@').Append(add.CollectionRef).Append(' ').Append(add.Position).Append(' ');
                Write(add.Item, text);
                if (add.PositionIndex is not null)
                {
                    text.Append(' ');
                    Write(add.PositionIndex, text);
                }
                return;
            case RemoveFromCollectionInst remove:
                text.Append('@').Append(remove.CollectionRef).Append(' ');
                Write(remove.Item, text);
                return;
            case MoveInCollectionInst move:
                text.Append('@').Append(move.CollectionRef).Append(' ').Append(move.Position).Append(' ');
                Write(move.Item, text);
                return;
            case ShuffleCollectionInst shuffle:
                text.Append('@').Append(shuffle.CollectionRef).Append(' ');
                Write(shuffle.Stream, text);
                return;
            case SetRelationInst setRelation:
                text.Append('@').Append(setRelation.RelationRef).Append(setRelation.Add ? " + " : " - ");
                Write(setRelation.Owner, text);
                text.Append(' ');
                Write(setRelation.Target, text);
                return;
            case ClearRelationInst clearRelation:
                text.Append('@').Append(clearRelation.RelationRef).Append(' ');
                Write(clearRelation.Owner, text);
                return;
            case SampleIntInst sampleInt:
                text.Append('[').Append(sampleInt.ResultSlot).Append("] ");
                Write(sampleInt.Stream, text);
                text.Append(' ');
                Write(sampleInt.Min, text);
                text.Append(' ');
                Write(sampleInt.Max, text);
                return;
            case SamplePickInst samplePick:
                text.Append('[').Append(samplePick.ResultSlot).Append("] ");
                Write(samplePick.Stream, text);
                text.Append(' ');
                Write(samplePick.Candidates, text);
                return;
            case EmitEventInst emit:
                text.Append('@').Append(emit.EventRef);
                WriteFieldInits(emit.Payload, text);
                return;
            case RequestDecisionInst decision:
                text.Append('[').Append(decision.ResultSlot).Append("] ").Append(decision.Kind).Append(' ');
                Write(decision.Participant, text);
                if (decision.Candidates is not null)
                {
                    text.Append(' ');
                    Write(decision.Candidates, text);
                }
                if (decision.Minimum is not null)
                {
                    text.Append(" min=");
                    Write(decision.Minimum, text);
                }
                if (decision.Maximum is not null)
                {
                    text.Append(" max=");
                    Write(decision.Maximum, text);
                }
                return;
            case ComputeInst compute:
                text.Append('@').Append(compute.ComputeRef).Append('(');
                foreach (var input in compute.Inputs)
                {
                    text.Append(input.Name).Append('=');
                    Write(input.Value, text);
                    text.Append(',');
                }
                text.Append(")->");
                foreach (var output in compute.Outputs)
                    text.Append(output.Name).Append('[').Append(output.Slot).Append("],");
                return;
            case CallInst call:
                text.Append('@').Append(call.ProcedureRef).Append("->[").Append(call.ResultSlot).Append("](");
                WriteList(call.Arguments, text);
                text.Append(')');
                return;
            case IfInst conditional:
                Write(conditional.Condition, text);
                text.Append('?');
                Write(conditional.Then, text);
                text.Append(':');
                if (conditional.Else is not null) Write(conditional.Else, text);
                return;
            case RepeatInst repeat:
                Write(repeat.Count, text);
                text.Append('x');
                Write(repeat.Body, text);
                return;
            case ForEachInst forEach:
                text.Append('[').Append(forEach.ItemSlot).Append("] ");
                Write(forEach.Query, text);
                text.Append(" in ");
                Write(forEach.Body, text);
                return;
            case ReturnInst returnInst:
                if (returnInst.Value is not null) Write(returnInst.Value, text);
                return;
            case SetTerminalInst terminal:
                text.Append('@').Append(terminal.TerminalRef);
                WriteFieldInits(terminal.Fields, text);
                return;
            case FailInst failure:
                text.Append("(\"").Append(failure.Message).Append("\")");
                return;
            default:
                // Silently dumping only the primitive name would let two different nodes hash identically, which
                // would break "a semantic change changes the identity". A new node must teach the dumper about it.
                throw Faults.Of(EngineFaultCode.Unsupported,
                    $"the IR dumper does not know node '{node.GetType().Name}' ({node.Primitive})");
        }
    }

    private static void WriteList(IReadOnlyList<IrNode> nodes, StringBuilder text)
    {
        for (var i = 0; i < nodes.Count; i++)
        {
            if (i > 0) text.Append(',');
            Write(nodes[i], text);
        }
    }

    private static void WriteFieldInits(IReadOnlyList<FieldInit> inits, StringBuilder text)
    {
        text.Append('(');
        for (var i = 0; i < inits.Count; i++)
        {
            if (i > 0) text.Append(',');
            text.Append('@').Append(inits[i].FieldRef).Append('=');
            Write(inits[i].Value, text);
        }
        text.Append(')');
    }
}

public static class ContentHasher
{
    public static string HashPackage(EnginePackageBuilder builder)
    {
        var text = new StringBuilder();
        text.Append("compiler=").Append(builder.CompilerIdentity).Append('\n');
        foreach (var declaration in builder.Declarations)
        {
            text.Append(declaration.GetType().Name).Append(':').Append(declaration.QualifiedId).Append(';');
            switch (declaration)
            {
                case EnumTypeSymbol enumType:
                    text.Append(string.Join("|", enumType.Members));
                    break;
                case EntityTypeSymbol entityType:
                    foreach (var field in entityType.Fields)
                    {
                        text.Append(field.Name).Append(':').Append(field.Type.Kind)
                            .Append(':').Append(field.Visibility).Append(':').Append(field.Lifetime).Append(',');
                    }
                    break;
                case ProcedureSymbol procedure:
                    text.Append(procedure.IsPure).Append(procedure.Body is null ? "?" : IrDumper.Dump(procedure.Body));
                    break;
                case EventSymbol eventType:
                    foreach (var field in eventType.Payload) text.Append(field.Name).Append(':').Append(field.Type.Kind).Append(',');
                    break;
                case ActionSymbol action:
                    text.Append(action.Guard is null ? "-" : IrDumper.Dump(action.Guard))
                        .Append(action.Body is null ? "?" : IrDumper.Dump(action.Body));
                    break;
                case ComputeAssetSymbol compute:
                    text.Append(compute.BackendId).Append('#').Append(compute.SourceHash).Append('@').Append(compute.Entry);
                    break;
            }
            text.Append('\n');
        }

        foreach (var subscription in builder.Subscriptions) text.Append("sub:").Append(subscription).Append('\n');
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())))
            .ToLowerInvariant()[..16];
    }
}
