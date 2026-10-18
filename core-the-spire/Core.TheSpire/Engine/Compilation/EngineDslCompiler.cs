using System.Globalization;

namespace CoreTheSpire.Engine;

/// <summary>Compiles configuration files to the same package/IR used by the C# SDK.</summary>
public static partial class EngineDslCompiler
{
    public const string LanguageVersion = "core/0.3";

    public static EnginePackage Compile(string source, string sourceName = "<memory>",
        IEnumerable<EnginePackage>? dependencies = null, IEnumerable<IEngineDslExtension>? extensions = null) =>
        new CoreDslBinding(EngineDslParser.Parse(source, sourceName), dependencies ?? [], extensions ?? []).Compile();

    /// <summary>Loads entry files and recursive imports in dependency order; each canonical path is compiled once.</summary>
    public static RuleSet LoadFiles(params string[] paths)
    {
        return new SourceLibraryLoader().Load(paths);
    }

    public static RuleSet LoadFiles(IEnumerable<string> paths, IEnumerable<IEngineDslExtension> extensions,
        IEnumerable<EnginePackage>? dependencies = null) =>
        new SourceLibraryLoader(extensions.ToArray(), (dependencies ?? []).ToArray()).Load(paths.ToArray());

    /// <summary>Binds text rules into a game-owned authoring package before its single Build/freeze.</summary>
    public static void BindInto(string source, EnginePackageBuilder package, string sourceName = "<memory>",
        IEnumerable<EnginePackage>? dependencies = null, IEnumerable<IEngineDslExtension>? extensions = null) =>
        new CoreDslBinding(EngineDslParser.Parse(source, sourceName), dependencies ?? [], extensions ?? [],
            targetPackage: package).Bind();

    private sealed partial class CoreDslBinding
    {
        private readonly DslPackageNode _source;
        private readonly EnginePackageBuilder _package;
        private readonly Dictionary<(Type Kind, string Name), DeclarationSymbol> _symbols = [];
        private readonly Dictionary<DslDeclarationNode, DeclarationSymbol> _declarations = [];
        private readonly Dictionary<string, ProcedureBuilder> _procedures = new(StringComparer.Ordinal);
        private readonly Dictionary<string, ActionBuilder> _actions = new(StringComparer.Ordinal);
        private readonly Dictionary<string, IEngineDslExtension> _extensions = new(StringComparer.Ordinal);
        private readonly HashSet<DslDeclarationNode> _extensionNodes = [];
        private readonly HashSet<(string Kind, string Name)> _declaredNames = [];
        private readonly Dictionary<DslDeclarationNode, TemplateScope> _templateScopes = [];
        private readonly Dictionary<(Type Kind, string Name), DeclarationSymbol> _templateSymbols = [];
        private TemplateScope? _bindingScope;
        private readonly bool _modern;

        public CoreDslBinding(DslPackageNode source, IEnumerable<EnginePackage> dependencies, IEnumerable<IEngineDslExtension> extensions,
            IReadOnlyList<EnginePackage>? imports = null, EnginePackageBuilder? targetPackage = null)
        {
            _source = source;
            foreach (var member in source.Members)
                if (member is not DslPropertyNode and not DslDeclarationNode)
                    throw Error(member.Location, "Root members must be declarations or named properties.");
            var properties = new Properties(source.Members.OfType<DslPropertyNode>(), source.Location,
                "dsl_version", "version", "dependencies", "overrides", "extends", "start", "imports", "includes");
            var language = Text(properties.Required("dsl_version"));
            if (properties.Get("includes") is { } includeNode)
                throw Error(includeNode.Location, "Use LoadFiles to resolve same-package includes.");
            if (language is not "core/0.1" and not "core/0.2" and not LanguageVersion)
                throw Error(source.Location, $"dsl_version must be 'core/0.1', 'core/0.2' or '{LanguageVersion}'.");
            _modern = language == LanguageVersion;
            var authoring = source.Members.Any(member => member is DslDeclarationNode { Kind: "template" or "use" }) || properties.Get("imports") is not null;
            if (authoring && language == "core/0.1") throw Error(source.Location, "imports/template/use require core/0.2 or later.");
            if (properties.Get("imports") is { } importNode && imports is null)
                throw Error(importNode.Location, "Use LoadFiles to resolve imports; Compile accepts explicit package dependencies only.");
            if (targetPackage is not null && targetPackage.PackageId != source.Name)
                throw Error(source.Location, "BindInto namespace must match the target package.");
            _package = targetPackage ?? new EnginePackageBuilder(source.Name,
                properties.Get("version") is { } version ? Text(version) : "0.1.0");
            if (targetPackage is null) _package.CompilerIdentity = language.Replace("core/", "core-dsl/");
            else _package.CompilerIdentity += "|" + language.Replace("core/", "core-dsl/");
            foreach (var extension in extensions)
            {
                if (string.IsNullOrWhiteSpace(extension.Identity)) throw Error(source.Location, "Extension identity is required.");
                foreach (var kind in extension.DeclarationKinds)
                    if (IsCoreKind(kind) || !_extensions.TryAdd(kind, extension))
                        throw Error(source.Location, $"Extension declaration kind '{kind}' is already owned.");
                _package.CompilerIdentity += "|" + extension.Identity;
            }
            foreach (var (name, target) in new[] { ("dependencies", _package.Dependencies),
                         ("overrides", _package.Overrides), ("extends", _package.Extends) })
                if (properties.Get(name) is { } values) target.AddRange(List(values).Select(Text));
            foreach (var imported in imports ?? [])
                if (!_package.Dependencies.Any(id => id.Split('@')[0] == imported.PackageId))
                    _package.Dependencies.Add(imported.PackageId);
            var dependencyArray = dependencies.ToArray();
            foreach (var dependency in dependencyArray)
            {
                if (!_package.Dependencies.Any(id => id.Split('@')[0] == dependency.PackageId)) continue;
                foreach (var symbol in dependency.Declarations)
                    _symbols[(symbol.GetType(), symbol.QualifiedId)] = symbol;
            }
            if (language != "core/0.1") _source = ExpandTemplates(source, dependencyArray);
        }

        public EnginePackage Compile()
        {
            Bind();
            return Located(_source.Location, _package.Build);
        }

        public void Bind()
        {
            // Declare names first: fields and programs may reference later declarations.
            foreach (var node in _source.Members.OfType<DslDeclarationNode>())
                Located(node.Location, () => Declare(node));
            foreach (var node in _source.Members.OfType<DslDeclarationNode>().Where(_extensionNodes.Contains))
                Located(node.Location, () => _extensions[node.Kind].Declare(node, _package));
            ImportGeneratedSymbols();
            foreach (var node in _source.Members.OfType<DslDeclarationNode>())
                if (!_extensionNodes.Contains(node)) BindInScope(node, () => Define(node));
            foreach (var node in _source.Members.OfType<DslDeclarationNode>().Where(_extensionNodes.Contains))
                Located(node.Location, () => _extensions[node.Kind].Bind(node, _package));
            ImportGeneratedSymbols();
            foreach (var node in _source.Members.OfType<DslDeclarationNode>())
                if (!_extensionNodes.Contains(node)) BindInScope(node, () => BindProgram(node));
            foreach (var node in _source.Members.OfType<DslDeclarationNode>())
                if (!_extensionNodes.Contains(node)) BindInScope(node, () => Finish(node));
            var start = _source.Members.OfType<DslPropertyNode>().SingleOrDefault(p => p.Name == "start");
            if (start is not null)
                foreach (var name in List(start.Value)) _package.OnSessionStart(Symbol<ProcedureSymbol>(name));
        }

        private void Declare(DslDeclarationNode node)
        {
            if (!_declaredNames.Add((node.Kind, node.Name))) throw Error(node.Location, $"Duplicate {node.Kind} '{node.Name}'.");
            if (_extensions.ContainsKey(node.Kind)) { _extensionNodes.Add(node); return; }
            DeclarationSymbol? symbol = node.Kind switch
            {
                "entity" => _package.EntityType(node.Name),
                "enum" => _package.Enum(node.Name),
                "scope" => _package.ScopeType(node.Name),
                "event" => _package.Event(node.Name),
                "terminal" => _package.TerminalResult(node.Name),
                "compute" => _package.ComputeAsset(node.Name),
                "projection" => _package.Projection(node.Name),
                "procedure" => DeclareProcedure(node.Name),
                "action" => DeclareAction(node.Name),
                "collection" or "relation" or "subscription" => null,
                _ => throw Error(node.Location, $"Unknown declaration '{node.Kind}'."),
            };
            if (symbol is not null) Register(node, symbol);
        }

        private static bool IsCoreKind(string kind) => kind is "entity" or "enum" or "scope" or "event" or
            "terminal" or "compute" or "projection" or "procedure" or "action" or "collection" or "relation" or "subscription" or "template" or "use";

        private void ImportGeneratedSymbols()
        {
            foreach (var symbol in _package.Declarations)
            {
                var key = (symbol.GetType(), symbol.Name);
                if (_symbols.TryGetValue(key, out var existing) && !ReferenceEquals(existing, symbol) &&
                    _package.Declarations.Contains(existing))
                    throw Error(_source.Location, $"Extension generated duplicate declaration '{symbol.Name}'.");
                _symbols[key] = symbol;
                _symbols[(symbol.GetType(), symbol.Name.Contains(':') ? symbol.Name : _source.Name + ":" + symbol.Name)] = symbol;
            }
        }

        private ProcedureSymbol DeclareProcedure(string name)
        {
            var builder = _package.Procedure(name);
            _procedures.Add(name, builder);
            return builder.Symbol;
        }

        private ActionSymbol DeclareAction(string name)
        {
            var symbol = _package.Action(name);
            _actions.Add(name, _package.ActionBody(symbol));
            return symbol;
        }

        private void Register(DslDeclarationNode node, DeclarationSymbol symbol)
        {
            var key = (symbol.GetType(), node.Name);
            if (_declarations.Values.Any(existing => existing.GetType() == symbol.GetType() && existing.Name == node.Name))
                throw Error(node.Location, $"Duplicate {node.Kind} '{node.Name}'.");
            // A dependency may already export this absolute name; the linker checks explicit override permission.
            _symbols[key] = symbol;
            var qualified = node.Name.Contains(':') ? node.Name : _source.Name + ":" + node.Name;
            _symbols[(symbol.GetType(), qualified)] = symbol;
            _declarations.Add(node, symbol);
        }

        private void Define(DslDeclarationNode node)
        {
            var p = new Properties(node.Members, node.Location, Allowed(node.Kind));
            switch (node.Kind)
            {
                case "enum":
                    foreach (var member in List(p.Required("members")))
                        ((EnumTypeSymbol)_declarations[node]).Member(Text(member));
                    break;
                case "entity":
                {
                    var entity = (EntityTypeSymbol)_declarations[node];
                    entity.ExtensibleFields = Boolean(p.Get("extensible"), false);
                    entity.OwnerFieldName = p.Get("owner") is { } owner ? Text(owner) : null;
                    foreach (var field in Fields(p.Get("fields")))
                        entity.AddField(field.Name, Shape(field.Properties.Required("type")),
                            field.Properties.Get("default") is { } value ? Constant(value) : null,
                            EnumValue(field.Properties.Get("visibility"), FieldVisibility.Public),
                            optional: Boolean(field.Properties.Get("optional"), false));
                    break;
                }
                case "scope":
                    ((ScopeTypeSymbol)_declarations[node]).Parent =
                        p.Get("parent") is { } parent ? Symbol<ScopeTypeSymbol>(parent) : null;
                    break;
                case "event":
                    foreach (var field in Shapes(p.Get("payload")))
                        ((EventSymbol)_declarations[node]).AddPayload(field.Name, field.Shape);
                    break;
                case "terminal":
                    foreach (var field in Shapes(p.Get("fields")))
                        ((TerminalResultSymbol)_declarations[node]).AddField(field.Name, field.Shape);
                    break;
                case "collection":
                {
                    var collection = _package.Collection(node.Name, Symbol<EntityTypeSymbol>(p.Required("element")));
                    collection.Capacity = Integer32(p.Get("capacity"), int.MaxValue);
                    collection.Ordered = Boolean(p.Get("ordered"), true);
                    collection.MembershipGroup = p.Get("group") is { } group ? Text(group) : null;
                    collection.IdentityVisible = Boolean(p.Get("identity_visible"), true);
                    collection.OrderVisible = Boolean(p.Get("order_visible"), true);
                    collection.SizeVisible = Boolean(p.Get("size_visible"), true);
                    Register(node, collection);
                    break;
                }
                case "relation":
                {
                    var relation = _package.Relation(node.Name, Symbol<EntityTypeSymbol>(p.Required("target")));
                    relation.Many = Boolean(p.Get("many"), true);
                    relation.Public = Boolean(p.Get("public"), true);
                    Register(node, relation);
                    break;
                }
                case "procedure":
                {
                    var procedure = _procedures[node.Name];
                    foreach (var field in Shapes(p.Get("parameters"))) procedure.Param(field.Name, field.Shape);
                    foreach (var field in Shapes(p.Get("locals"))) procedure.Local(field.Name, field.Shape);
                    procedure.Symbol.IsPure = Boolean(p.Get("pure"), false);
                    if (p.Get("result") is { } result) procedure.Result(Shape(result));
                    break;
                }
                case "action":
                {
                    var action = _actions[node.Name];
                    foreach (var field in Shapes(p.Get("parameters"))) action.Param(field.Name, field.Shape);
                    foreach (var field in Shapes(p.Get("locals"))) action.Local(field.Name, field.Shape);
                    if (p.Get("role") is { } role) action.Role(Text(role));
                    break;
                }
                case "compute": DefineCompute((ComputeAssetSymbol)_declarations[node], p); break;
            }
        }

        private void BindProgram(DslDeclarationNode node)
        {
            var p = new Properties(node.Members, node.Location, Allowed(node.Kind));
            if (node.Kind == "procedure")
            {
                var context = new ProgramContext(this, _procedures[node.Name]);
                var body = context.Body(p.Required("body"));
                _procedures[node.Name].Body(body.Instructions.ToArray());
                _procedures[node.Name].Symbol.Body!.SourceLocation = body.SourceLocation;
            }
            else if (node.Kind == "action")
            {
                var action = _actions[node.Name];
                var context = new ProgramContext(this, action);
                if (p.Get("requires") is { } guard) action.Requires(context.Expression(guard));
                if (p.Get("candidates") is { } candidates)
                {
                    var candidate = Properties.Of(candidates, "parameter", "query");
                    action.Candidates(Text(candidate.Required("parameter")), context.Query(candidate.Required("query")));
                }
                if (p.Get("choices") is { } choices)
                    foreach (var choice in Properties.Items(choices))
                    {
                        var values = List(choice.Value);
                        if (values.Count == 0) throw Error(choice.Value.Location, $"Action choices for '{choice.Name}' must not be empty.");
                        foreach (var value in values) action.Choose(choice.Name, context.Expression(value));
                    }
                var body = context.Body(p.Required("body"));
                action.Body(body.Instructions.ToArray());
                action.Action.Body!.SourceLocation = body.SourceLocation;
            }
        }

        private void Finish(DslDeclarationNode node)
        {
            var p = new Properties(node.Members, node.Location, Allowed(node.Kind));
            if (node.Kind == "subscription")
            {
                var reaction = Symbol<ProcedureSymbol>(p.Required("reaction"));
                var evaluation = new ProcedureBuilder(_package, new ProcedureSymbol("$subscription"));
                foreach (var parameter in reaction.Parameters) evaluation.Param(parameter.Name, parameter.Type);
                var context = new ProgramContext(this, evaluation);
                _package.Subscribe(Symbol<EventSymbol>(p.Required("event")), reaction,
                    Integer32(p.Get("priority"), 0),
                    p.Get("recheck") is { } recheck ? context.Expression(recheck) : null,
                    p.Get("scope_payload") is { } scope ? Text(scope) : null,
                    p.Get("instances") is { } instances ? context.Query(instances) : null,
                    p.Get("instance_order") is { } order ? context.Expression(order) : null,
                    evaluation.LocalFields.Count);
            }
            else if (node.Kind == "projection")
            {
                var projection = (ProjectionSymbol)_declarations[node];
                projection.ParticipantRole = Text(p.Required("role"));
                projection.IncludeCollections = Boolean(p.Get("collections"), true);
                projection.IncludeRelations = Boolean(p.Get("relations"), true);
                foreach (var type in Properties.Items(p.Required("types")))
                {
                    var settings = Properties.Of(type.Value, "include", "owner", "hidden");
                    var view = projection.Type(Symbol<EntityTypeSymbol>(new DslNameNode(type.Name, type.Location)),
                        Boolean(settings.Get("include"), true));
                    view.OwnerField = settings.Get("owner") is { } owner ? Text(owner) : null;
                    view.IncludeHiddenFields = Boolean(settings.Get("hidden"), false);
                }
                if (p.Get("reveals") is { } reveals)
                    foreach (var reveal in List(reveals))
                    {
                        var binding = Properties.Of(reveal, "authority", "relation");
                        projection.Reveal(Symbol<EntityTypeSymbol>(binding.Required("authority")),
                            Symbol<RelationSymbol>(binding.Required("relation")));
                    }
            }
        }

        private static string[] Allowed(string kind) => kind switch
        {
            "entity" => ["fields", "owner", "extensible"],
            "enum" => ["members"], "scope" => ["parent"],
            "event" => ["payload"], "terminal" => ["fields"],
            "collection" => ["element", "capacity", "ordered", "group", "identity_visible", "order_visible", "size_visible"],
            "relation" => ["target", "many", "public"],
            "procedure" => ["parameters", "locals", "pure", "result", "body"],
            "action" => ["parameters", "locals", "role", "requires", "candidates", "choices", "body"],
            "subscription" => ["event", "reaction", "priority", "recheck", "scope_payload", "instances", "instance_order"],
            "projection" => ["role", "types", "collections", "relations", "reveals"],
            "compute" => ["backend", "source_path", "source_hash", "entry", "inputs", "outputs", "limits"],
            _ => [],
        };

        private T Symbol<T>(DslValueNode name) where T : DeclarationSymbol
        {
            var text = Text(name);
            if (_bindingScope is { } scope)
            {
                if (text.Contains(':'))
                {
                    if (_symbols.TryGetValue((typeof(T), text), out var absolute) || _templateSymbols.TryGetValue((typeof(T), text), out absolute)) return (T)absolute;
                }
                else
                {
                    if (scope.Owns(text) && _symbols.TryGetValue((typeof(T), scope.Prefix + "." + text), out var owned)) return (T)owned;
                    var qualified = scope.LibraryNamespace + ":" + text;
                    if (_symbols.TryGetValue((typeof(T), qualified), out var library) || _templateSymbols.TryGetValue((typeof(T), qualified), out library)) return (T)library;
                }
                // Template names are lexical: do not accidentally capture a same-named declaration in the game.
                throw Error(name.Location, $"Unknown {typeof(T).Name} '{text}' in template '{scope.Prefix}'.");
            }
            return _symbols.TryGetValue((typeof(T), text), out var symbol) && symbol is T typed
                ? typed : throw Error(name.Location, $"Unknown {typeof(T).Name} '{text}'.");
        }

        private FieldSymbol Field(DslValueNode name)
        {
            var text = Text(name);
            var dot = text.LastIndexOf('.');
            if (dot <= 0) throw Error(name.Location, "Field must be qualified as Entity.Field.");
            var owner = Symbol<EntityTypeSymbol>(new DslNameNode(text[..dot], name.Location));
            return Located(name.Location, () => owner.Field(text[(dot + 1)..]));
        }

        private FieldTypeSchema Shape(DslValueNode node) => Located(node.Location, () =>
        {
            if (node is DslNameNode name)
                return name.Name switch
                {
                    "Int" => FieldTypeSchema.Int(), "Bool" => FieldTypeSchema.Bool(),
                    "Fixed" => FieldTypeSchema.Fixed(), "String" => FieldTypeSchema.String(),
                    "Participant" => FieldTypeSchema.Participant(), "Binding" => FieldTypeSchema.Binding(),
                    "Ref" => FieldTypeSchema.AnyRef(),
                    _ => throw Error(node.Location, $"Unknown field type '{name.Name}'."),
                };
            if (node is not DslCallNode call) throw Error(node.Location, "Expected a field type.");
            var args = new Arguments(call);
            var shape = call.Name switch
            {
                "Int" => FieldTypeSchema.Int(Integer(args.Optional("min"), long.MinValue), Integer(args.Optional("max"), long.MaxValue)),
                "Fixed" => FieldTypeSchema.Fixed(Integer32(args.Optional("scale"), 2),
                    Decimal(args.Optional("min"), decimal.MinValue), Decimal(args.Optional("max"), decimal.MaxValue)),
                "String" => FieldTypeSchema.String(Integer32(args.Optional("length"), 128)),
                "Ref" => FieldTypeSchema.RefTo(Symbol<EntityTypeSymbol>(args.Required("type", 0))),
                "Enum" => FieldTypeSchema.EnumOf(Symbol<EnumTypeSymbol>(args.Required("type", 0))),
                "List" => FieldTypeSchema.ListOf(Shape(args.Required("element", 0)), Integer32(args.Optional("length"), 256)),
                _ => throw Error(node.Location, $"Unknown field type '{call.Name}'."),
            };
            args.Finish();
            return shape;
        });

        private Value Constant(DslValueNode node) => node switch
        {
            DslBooleanNode boolean => Value.Of(boolean.Value),
            DslStringNode text => Value.Of(text.Value),
            DslNumberNode number when !number.Literal.Contains('.') => Value.Of(Integer(number, 0)),
            DslNumberNode number => Value.Of(Decimal(number, 0)),
            DslNameNode { Name: "none" } => Value.None,
            DslCallNode { Name: "participant" } call => ParticipantConstant(call),
            DslCallNode { Name: "enum" } call => EnumConstant(call),
            _ => throw Error(node.Location, "Expected a literal constant, participant(id), or enum(Type, Member)."),
        };

        private Value ParticipantConstant(DslCallNode call)
        {
            var args = new Arguments(call);
            var value = Value.OfParticipant(new ParticipantId(Integer32(args.Required("id", 0), 0)));
            args.Finish();
            return value;
        }

        private Value EnumConstant(DslCallNode call)
        {
            var args = new Arguments(call);
            var type = Symbol<EnumTypeSymbol>(args.Required("type", 0));
            var value = Value.OfEnum(Located(call.Location, () => type.OrdinalOf(Text(args.Required("member", 1)))));
            args.Finish();
            return value;
        }

        private IEnumerable<(string Name, Properties Properties)> Fields(DslValueNode? fields) =>
            fields is null ? [] : Properties.Items(fields).Select(field => (field.Name,
                Properties.Of(field.Value, "type", "default", "visibility", "optional")));

        private IEnumerable<(string Name, FieldTypeSchema Shape)> Shapes(DslValueNode? fields) =>
            fields is null ? [] : Properties.Items(fields).Select(field => (field.Name, Shape(field.Value)));

        private void DefineCompute(ComputeAssetSymbol asset, Properties p)
        {
            asset.BackendId = Text(p.Required("backend"));
            asset.SourcePath = Text(p.Required("source_path"));
            asset.SourceHash = Text(p.Required("source_hash"));
            if (p.Get("entry") is { } entry) asset.Entry = Text(entry);
            foreach (var field in Shapes(p.Get("inputs"))) asset.Input(field.Name, field.Shape);
            foreach (var field in Shapes(p.Get("outputs"))) asset.Output(field.Name, field.Shape);
            if (p.Get("limits") is { } limits)
            {
                var l = Properties.Of(limits, "instructions", "memory", "input_values", "output_values", "call_depth");
                asset.Limits = new ComputeLimits(
                    MaxInputValues: Integer32(l.Get("input_values"), ComputeLimits.Default.MaxInputValues),
                    MaxOutputValues: Integer32(l.Get("output_values"), ComputeLimits.Default.MaxOutputValues),
                    MaxInstructions: Integer(l.Get("instructions"), ComputeLimits.Default.MaxInstructions),
                    MaxHeapBytes: Integer(l.Get("memory"), ComputeLimits.Default.MaxHeapBytes),
                    MaxCallDepth: Integer32(l.Get("call_depth"), ComputeLimits.Default.MaxCallDepth));
            }
        }

        private static T EnumValue<T>(DslValueNode? node, T fallback) where T : struct, Enum => node is null ? fallback :
            Enum.TryParse<T>(Text(node), true, out var value) && Enum.IsDefined(value) ? value :
            throw Error(node.Location, $"Unknown {typeof(T).Name} '{Text(node)}'.");
        private static string Text(DslValueNode node) => node switch
        {
            DslNameNode name => name.Name, DslStringNode text => text.Value,
            _ => throw Error(node.Location, "Expected a name or string."),
        };
        private static long Integer(DslValueNode? node, long fallback) => node is null ? fallback :
            node is DslNumberNode number && long.TryParse(number.Literal, NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out var value) ? value : throw Error(node.Location, "Expected an Int64 literal.");
        private static int Integer32(DslValueNode? node, int fallback) => node is null ? fallback :
            Located(node.Location, () => checked((int)Integer(node, fallback)));
        private static decimal Decimal(DslValueNode? node, decimal fallback) => node is null ? fallback :
            node is DslNumberNode number && decimal.TryParse(number.Literal,
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value)
                ? value : throw Error(node.Location, "Expected a decimal literal.");
        private static bool Boolean(DslValueNode? node, bool fallback) => node is null ? fallback :
            node is DslBooleanNode boolean ? boolean.Value : throw Error(node.Location, "Expected true or false.");
        private static IReadOnlyList<DslValueNode> List(DslValueNode node) => node is DslListNode list ? list.Items :
            throw Error(node.Location, "Expected a list [...].");
        private static DslSourceException Error(DslSourceLocation location, string message) => new(location, message);
        private static T Located<T>(DslSourceLocation location, Func<T> action)
        {
            try { return action(); }
            catch (EngineFaultException error) { throw Error(location, error.Fault.Message); }
            catch (OverflowException) { throw Error(location, "Numeric literal is outside the supported range."); }
            catch (ArgumentException error) { throw Error(location, error.Message); }
        }
        private static void Located(DslSourceLocation location, Action action) => Located(location, () => { action(); return true; });
    }
}
