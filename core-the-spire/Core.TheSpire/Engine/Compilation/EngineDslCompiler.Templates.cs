using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CoreTheSpire.Engine;

// Front-end-only metadata. Parser collections and these exports are read-only; none become Session state.
internal sealed record EngineDslTemplate(string Namespace, string Name,
    IReadOnlyDictionary<string, EngineDslTemplateParameter> Parameters, DslBlockNode Body,
    DslSourceLocation Location);
internal sealed record EngineDslTemplateParameter(FieldTypeSchema Shape, DslValueNode? Default, DslSourceLocation Location);

public static partial class EngineDslCompiler
{
    private sealed partial class CoreDslBinding
    {
        private sealed record TemplateScope(string Prefix, string LibraryNamespace, IReadOnlySet<string> Names,
            DslSourceLocation UseLocation)
        {
            public bool Owns(string name) => Names.Contains(name.Split('.')[0]);
        }

        private void BindInScope(DslDeclarationNode node, Action action)
        {
            var previous = _bindingScope;
            _bindingScope = _templateScopes.GetValueOrDefault(node);
            try { Located(node.Location, action); }
            catch (DslSourceException error) when (_bindingScope is { } scope)
            {
                throw Error(error.Location, $"{error.Message}; instantiated by use '{scope.Prefix}' at {scope.UseLocation}.");
            }
            finally { _bindingScope = previous; }
        }

        private DslPackageNode ExpandTemplates(DslPackageNode source, IReadOnlyList<EnginePackage> dependencies)
        {
            var templates = new Dictionary<string, EngineDslTemplate>(StringComparer.Ordinal);
            var libraries = dependencies.ToDictionary(package => package.PackageId, StringComparer.Ordinal);
            var visible = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in _package.Dependencies) Include(id.Split('@')[0]);
            foreach (var dependency in dependencies)
                if (visible.Contains(dependency.PackageId))
                {
                    foreach (var symbol in dependency.Declarations)
                        _templateSymbols[(symbol.GetType(), symbol.QualifiedId)] = symbol;
                    foreach (var template in dependency.RuleTemplates)
                        templates.Add(template.Namespace + ":" + template.Name, template);
                }
            void Include(string id)
            {
                if (!visible.Add(id) || !libraries.TryGetValue(id, out var library)) return;
                foreach (var child in library.Dependencies) Include(child.Split('@')[0]);
            }

            var local = new List<EngineDslTemplate>();
            foreach (var node in source.Members.OfType<DslDeclarationNode>().Where(node => node.Kind == "template"))
            {
                if (node.Name.Contains(':') || node.Name.Contains('.')) throw Error(node.Location, "Template names must be local simple names.");
                var properties = Properties.Of(new DslBlockNode(node.Members, node.Location), "parameters", "body");
                var parameters = new Dictionary<string, EngineDslTemplateParameter>(StringComparer.Ordinal);
                foreach (var field in properties.Get("parameters") is { } fields ? Properties.Items(fields) : [])
                {
                    var p = Properties.Of(field.Value, "type", "default");
                    var shape = Shape(p.Required("type"));
                    if (!IsTemplateLiteralShape(shape)) throw Error(field.Location, "Template parameters support Int, Bool, String, Fixed and List(Int) literals.");
                    var value = p.Get("default");
                    if (value is not null) ValidateTemplateArgument(field.Name, shape, value);
                    parameters.Add(field.Name, new(shape, value, field.Location));
                }
                var bodyNode = properties.Required("body");
                if (bodyNode is not DslBlockNode body) throw Error(bodyNode.Location, "Template body must be a declaration block.");
                var template = new EngineDslTemplate(source.Name, node.Name,
                    new ReadOnlyDictionary<string, EngineDslTemplateParameter>(parameters), body, node.Location);
                if (!templates.TryAdd(source.Name + ":" + node.Name, template)) throw Error(node.Location, $"Duplicate template '{node.Name}'.");
                ValidateTemplateBody(template);
                local.Add(template);
            }
            _package.RuleTemplates = Array.AsReadOnly(local.ToArray());
            if (local.Count > 0)
            {
                var canonical = JsonSerializer.Serialize(source.Members.OfType<DslDeclarationNode>()
                    .Where(node => node.Kind == "template").Select(CanonicalMember));
                _package.CompilerIdentity += "|templates=" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
            }

            var members = new List<DslMemberNode>();
            var starts = new List<DslValueNode>();
            var stack = new HashSet<string>(StringComparer.Ordinal);
            var instances = new HashSet<string>(StringComparer.Ordinal);
            var uses = 0;
            foreach (var member in source.Members)
            {
                if (member is DslDeclarationNode { Kind: "template" }) continue;
                if (member is DslDeclarationNode { Kind: "use" } use) Instantiate(use, use.Name, source.Name);
                else if (member is DslPropertyNode { Name: "start" } start) starts.AddRange(List(start.Value));
                else members.Add(member);
            }
            if (starts.Count > 0) members.Add(new DslPropertyNode("start", new DslListNode(Array.AsReadOnly(starts.ToArray()), source.Location), source.Location));
            return source with { Members = Array.AsReadOnly(members.ToArray()) };

            void Instantiate(DslDeclarationNode use, string prefix, string lexicalNamespace)
            {
                if (use.Name.Contains(':') || use.Name.Contains('.')) throw Error(use.Location, "use names must be local simple names.");
                if (!instances.Add(prefix)) throw Error(use.Location, $"Duplicate template instance '{prefix}'.");
                if (++uses > 256 || stack.Count >= 32) throw Error(use.Location, "Template expansion limit exceeded (256 instances, depth 32).");
                var p = Properties.Of(new DslBlockNode(use.Members, use.Location), "template", "args");
                var reference = p.Required("template");
                var name = Text(reference);
                var id = name.Contains(':') ? name : lexicalNamespace + ":" + name;
                if (!templates.TryGetValue(id, out var template)) throw Error(reference.Location, $"Unknown template '{id}'.");
                if (!stack.Add(id)) throw Error(use.Location, $"Recursive template expansion of '{id}'.");
                try
                {
                    var arguments = new Dictionary<string, DslValueNode>(StringComparer.Ordinal);
                    foreach (var argument in p.Get("args") is { } supplied ? Properties.Items(supplied) : [])
                    {
                        if (!template.Parameters.TryGetValue(argument.Name, out var parameter)) throw Error(argument.Location, $"Unknown template argument '{argument.Name}' for '{id}'.");
                        ValidateTemplateArgument(argument.Name, parameter.Shape, argument.Value);
                        arguments.Add(argument.Name, argument.Value);
                    }
                    foreach (var (parameterName, parameter) in template.Parameters)
                        if (!arguments.ContainsKey(parameterName))
                            arguments.Add(parameterName, parameter.Default ?? throw Error(use.Location, $"Missing template argument '{parameterName}' for '{id}'."));
                    var names = template.Body.Members.OfType<DslDeclarationNode>().Select(node => node.Name).ToHashSet(StringComparer.Ordinal);
                    var scope = new TemplateScope(prefix, template.Namespace, names, use.Location);
                    foreach (var original in template.Body.Members)
                    {
                        var expanded = SubstituteMember(original, arguments);
                        if (expanded is DslDeclarationNode { Kind: "use" } nested)
                            Instantiate(nested, prefix + "." + nested.Name, template.Namespace);
                        else if (expanded is DslDeclarationNode declaration)
                        {
                            var renamed = declaration with { Name = prefix + "." + declaration.Name };
                            members.Add(renamed);
                            _templateScopes.Add(renamed, scope);
                            if (members.Count > 4096) throw Error(use.Location, "Expanded declaration limit exceeded (4096).");
                        }
                        else if (expanded is DslPropertyNode { Name: "start" } initialization)
                            foreach (var entry in List(initialization.Value))
                            {
                                var target = Text(entry);
                                starts.Add(new DslNameNode(target.Contains(':') ? target : scope.Owns(target) ? prefix + "." + target : template.Namespace + ":" + target, entry.Location));
                            }
                    }
                }
                finally { stack.Remove(id); }
            }
        }

        private static void ValidateTemplateBody(EngineDslTemplate template)
        {
            foreach (var member in template.Body.Members)
            {
                if (member is DslDeclarationNode declaration)
                {
                    if (!IsCoreKind(declaration.Kind) || declaration.Kind == "template") throw Error(declaration.Location, "Template bodies contain core declarations and nested use only.");
                    if (declaration.Name.Contains(':') || declaration.Name.Contains('.')) throw Error(declaration.Location, "Template-owned declarations must use simple names.");
                }
                else if (member is not DslPropertyNode { Name: "start" }) throw Error(member.Location, "Only start and declarations are allowed in a template body.");
            }
            // Validate every placeholder even in unused templates; quoted strings are never interpolated.
            ValidatePlaceholders(template.Body);
            void ValidatePlaceholders(DslValueNode value)
            {
                if (value is DslCallNode { Name: "arg" } call)
                {
                    var name = ArgumentName(call);
                    if (!template.Parameters.ContainsKey(name)) throw Error(call.Location, $"Unknown template parameter '{name}'.");
                }
                else if (value is DslCallNode other) foreach (var item in other.Arguments) ValidatePlaceholders(item.Value);
                else if (value is DslListNode list) foreach (var item in list.Items) ValidatePlaceholders(item);
                else if (value is DslBlockNode block) foreach (var item in block.Members)
                    if (item is DslPropertyNode property) ValidatePlaceholders(property.Value);
                    else if (item is DslDeclarationNode declaration) ValidatePlaceholders(new DslBlockNode(declaration.Members, declaration.Location));
            }
        }

        private static string ArgumentName(DslCallNode call)
        {
            var args = new Arguments(call);
            var name = Text(args.Required("name", 0));
            args.Finish();
            return name;
        }

        private static DslMemberNode SubstituteMember(DslMemberNode member, IReadOnlyDictionary<string, DslValueNode> arguments) => member switch
        {
            DslPropertyNode property => property with { Value = SubstituteValue(property.Value, arguments) },
            DslDeclarationNode declaration => declaration with { Members = Array.AsReadOnly(declaration.Members.Select(item => SubstituteMember(item, arguments)).ToArray()) },
            _ => throw Error(member.Location, "Unsupported template member."),
        };

        private static DslValueNode SubstituteValue(DslValueNode value, IReadOnlyDictionary<string, DslValueNode> arguments) => value switch
        {
            DslCallNode { Name: "arg" } call => arguments[ArgumentName(call)],
            DslCallNode call => call with { Arguments = Array.AsReadOnly(call.Arguments.Select(item => item with { Value = SubstituteValue(item.Value, arguments) }).ToArray()) },
            DslBlockNode block => block with { Members = Array.AsReadOnly(block.Members.Select(item => SubstituteMember(item, arguments)).ToArray()) },
            DslListNode list => list with { Items = Array.AsReadOnly(list.Items.Select(item => SubstituteValue(item, arguments)).ToArray()) },
            _ => value,
        };

        private static bool IsTemplateLiteralShape(FieldTypeSchema shape) => shape.Kind is FieldTypeKind.Int or FieldTypeKind.Bool or FieldTypeKind.String or FieldTypeKind.Fixed ||
            shape is { Kind: FieldTypeKind.List, Element.Kind: FieldTypeKind.Int };

        private void ValidateTemplateArgument(string name, FieldTypeSchema shape, DslValueNode node)
        {
            if (shape.Kind == FieldTypeKind.List)
            {
                var list = List(node);
                if (list.Count > shape.MaxLength) throw Error(node.Location, $"Template argument '{name}' exceeds length {shape.MaxLength}.");
                foreach (var item in list) ValidateTemplateArgument(name, shape.Element!, item);
                return;
            }
            var value = Constant(node);
            var valid = shape.Kind switch
            {
                FieldTypeKind.Int => value.Kind == ValueKind.Int && value.AsInt() >= shape.Min && value.AsInt() <= shape.Max,
                FieldTypeKind.Bool => value.Kind == ValueKind.Bool,
                FieldTypeKind.String => value.Kind == ValueKind.String && value.AsString().Length <= shape.MaxLength,
                FieldTypeKind.Fixed => value.Kind == ValueKind.Fixed && value.AsDecimal() >= shape.FixedMin && value.AsDecimal() <= shape.FixedMax,
                _ => false,
            };
            if (!valid) throw Error(node.Location, $"Template argument '{name}' does not satisfy {shape.Kind} and its bounds.");
        }

        // Canonical syntax includes template semantics, excluding file paths/comments/locations.
        private static object CanonicalMember(DslMemberNode member) => member switch
        {
            DslPropertyNode property => new object[] { "property", property.Name, CanonicalValue(property.Value) },
            DslDeclarationNode declaration => new object[] { "declaration", declaration.Kind, declaration.Name, declaration.Members.Select(CanonicalMember).ToArray() },
            _ => throw Error(member.Location, "Unsupported member."),
        };
        private static object CanonicalValue(DslValueNode node) => node switch
        {
            DslNameNode name => new object[] { "name", name.Name },
            DslStringNode text => new object[] { "string", text.Value },
            DslNumberNode number => new object[] { "number", number.Literal },
            DslBooleanNode flag => new object[] { "bool", flag.Value },
            DslBlockNode block => new object[] { "block", block.Members.Select(CanonicalMember).ToArray() },
            DslListNode list => new object[] { "list", list.Items.Select(CanonicalValue).ToArray() },
            DslCallNode call => new object[] { "call", call.Name, call.Arguments.Select(arg => new object?[] { arg.Name, CanonicalValue(arg.Value) }).ToArray() },
            _ => throw Error(node.Location, "Unsupported value."),
        };
    }
}
