namespace CoreTheSpire.Engine;

public static partial class EngineDslCompiler
{
    private sealed partial class CoreDslBinding
    {
        private readonly List<(string Table, string Handler, DslSourceLocation Location)> _dispatchBindings = [];

        private void ValidateDispatchSignatures()
        {
            foreach (var binding in _dispatchBindings)
            {
                var table = Symbol<ProcedureSymbol>(new DslNameNode(binding.Table, binding.Location));
                var handler = Symbol<ProcedureSymbol>(new DslNameNode(binding.Handler, binding.Location));
                if (!table.Parameters.Skip(1).Select(p => (p.Name, p.Type)).SequenceEqual(handler.Parameters.Select(p => (p.Name, p.Type))) ||
                    table.ResultType != handler.ResultType || table.IsPure && !handler.IsPure)
                    throw Error(binding.Location, $"Handler '{binding.Handler}' must match dispatch '{binding.Table}' parameters, result and purity.");
            }
        }
        // A closed, typed dispatch table lowers to ordinary IR. It owns no runtime state and performs
        // no reflection. Cases can live beside their content, including inside lexical template scopes.
        private DslPackageNode ExpandDispatches(DslPackageNode source)
        {
            var declarations = source.Members.OfType<DslDeclarationNode>().ToArray();
            var tables = declarations.Where(d => d.Kind == "dispatch").ToArray();
            var cases = declarations.Where(d => d.Kind == "dispatchCase").ToArray();
            if (tables.Length == 0 && cases.Length == 0) return source;
            _package.CompilerIdentity += "|dispatch/1";
            var groups = new Dictionary<string, List<DslDeclarationNode>>(StringComparer.Ordinal);
            foreach (var table in tables)
                if (!groups.TryAdd(Absolute(table, table.Name, ownDeclaration: true), []))
                    throw Error(table.Location, $"Duplicate dispatch '{table.Name}'.");
            foreach (var entry in cases)
            {
                var p = new Properties(entry.Members, entry.Location, "dispatch", "key", "procedure");
                var table = Absolute(entry, Text(p.Required("dispatch")));
                if (!groups.TryGetValue(table, out var entries)) throw Error(entry.Location, $"Unknown dispatch '{table}'.");
                var key = Text(p.Required("key"));
                if (key.Length is 0 or > 128) throw Error(entry.Location, "Dispatch keys must contain 1..128 characters.");
                if (entries.Any(e => Text(new Properties(e.Members, e.Location, "dispatch", "key", "procedure").Required("key")) == key))
                    throw Error(entry.Location, $"Duplicate dispatch key '{key}' for '{table}'.");
                entries.Add(entry);
                _dispatchBindings.Add((table, Absolute(entry, Text(p.Required("procedure"))), entry.Location));
            }
            var replacements = tables.ToDictionary(d => d, Lower);
            var members = new List<DslMemberNode>();
            foreach (var member in source.Members)
            {
                if (member is DslDeclarationNode { Kind: "dispatchCase" }) continue;
                if (member is not DslDeclarationNode d || !replacements.TryGetValue(d, out var lowered)) { members.Add(member); continue; }
                members.Add(lowered);
                var at = d.Location;
                var key = new DslNameNode("key", at);
                var comparisons = groups[Absolute(d, d.Name, ownDeclaration: true)].Select(e =>
                    (DslValueNode)new DslCallNode("equal", Array.AsReadOnly(new[] { new DslCallArgumentNode(null, key, at),
                        new DslCallArgumentNode(null, new Properties(e.Members, e.Location, "dispatch", "key", "procedure").Required("key"), at) }), at));
                DslValueNode value = new DslBooleanNode(false, at);
                foreach (var comparison in comparisons) value = new DslCallNode("or", Array.AsReadOnly(new[] {
                    new DslCallArgumentNode(null, value, at), new DslCallArgumentNode(null, comparison, at) }), at);
                var contains = DslSourceParser.Parse($"using namespace {source.Name}; procedure {d.Name}.Contains {{ pure = true parameters = {{ key = String(length = 128) }} result = Bool body = {{ return = {{ value = false }} }} }}").Members.OfType<DslDeclarationNode>().Single();
                var returned = new DslPropertyNode("return", new DslBlockNode(Array.AsReadOnly<DslMemberNode>([new DslPropertyNode("value", value, at)]), at), at);
                contains = contains with { Location = at, Members = Array.AsReadOnly(contains.Members.Select(m => m is DslPropertyNode { Name: "body" } p
                    ? (DslMemberNode)(p with { Value = new DslBlockNode(Array.AsReadOnly<DslMemberNode>([returned]), at) }) : m).ToArray()) };
                if (_templateScopes.TryGetValue(d, out var scope)) _templateScopes.Add(contains, scope);
                members.Add(contains);
            }
            return source with { Members = Array.AsReadOnly(members.ToArray()) };

            string Absolute(DslDeclarationNode node, string name, bool ownDeclaration = false)
            {
                if (name.Contains(':')) return name;
                if (_templateScopes.TryGetValue(node, out var scope))
                {
                    if (!ownDeclaration && scope.Owns(name)) return source.Name + ":" + scope.Prefix + "." + name;
                    return (ownDeclaration ? source.Name : scope.LibraryNamespace) + ":" + name;
                }
                return source.Name + ":" + name;
            }

            DslDeclarationNode Lower(DslDeclarationNode table)
            {
                var p = new Properties(table.Members, table.Location, "parameters", "pure", "result");
                var parameters = p.Get("parameters") is { } inputs ? Properties.Items(inputs).ToArray() : [];
                if (parameters.Any(x => x.Name == "key" || x.Name == "__dispatchResult")) throw Error(table.Location, "Dispatch parameter name is reserved.");
                var body = new List<DslMemberNode>();
                foreach (var entry in groups[Absolute(table, table.Name, ownDeclaration: true)].OrderBy(e => Text(new Properties(e.Members, e.Location, "dispatch", "key", "procedure").Required("key")), StringComparer.Ordinal))
                {
                    var c = new Properties(entry.Members, entry.Location, "dispatch", "key", "procedure");
                    var at = entry.Location;
                    DslNameNode Name(string name) => new(name, at);
                    DslPropertyNode Property(string name, DslValueNode value) => new(name, value, at);
                    DslBlockNode Block(params DslMemberNode[] members) => new(Array.AsReadOnly(members), at);
                    var call = new List<DslMemberNode> {
                        Property("procedure", Name(Absolute(entry, Text(c.Required("procedure"))))),
                        Property("args", Block(parameters.Select(x => (DslMemberNode)Property(x.Name, Name(x.Name))).ToArray()))
                    };
                    if (p.Get("result") is not null) call.Add(Property("into", Name("__dispatchResult")));
                    var condition = new DslCallNode("equal", Array.AsReadOnly(new[] {
                        new DslCallArgumentNode(null, Name("key"), at), new DslCallArgumentNode(null, c.Required("key"), at) }), at);
                    var returned = p.Get("result") is null ? Block() : Block(Property("value", Name("__dispatchResult")));
                    body.Add(Property("if", Block(Property("condition", condition), Property("then", Block(
                        Property("call", new DslBlockNode(Array.AsReadOnly(call.ToArray()), at)), Property("return", returned))))));
                }
                body.Add(new DslPropertyNode("fail", new DslBlockNode(Array.AsReadOnly<DslMemberNode>([
                    new DslPropertyNode("message", new DslStringNode($"Unbound dispatch key for '{table.Name}'", table.Location), table.Location)]), table.Location), table.Location));
                var result = new List<DslMemberNode> {
                    new DslPropertyNode("parameters", new DslBlockNode(Array.AsReadOnly<DslMemberNode>([
                        new DslPropertyNode("key", new DslCallNode("String", Array.AsReadOnly(new[] {
                            new DslCallArgumentNode("length", new DslNumberNode("128", table.Location), table.Location) }), table.Location), table.Location), ..parameters]), table.Location), table.Location),
                    new DslPropertyNode("body", new DslBlockNode(Array.AsReadOnly(body.ToArray()), table.Location), table.Location)
                };
                if (p.Get("pure") is { } pure) result.Add(new DslPropertyNode("pure", pure, table.Location));
                if (p.Get("result") is { } output)
                {
                    result.Add(new DslPropertyNode("result", output, table.Location));
                    result.Add(new DslPropertyNode("locals", new DslBlockNode(Array.AsReadOnly<DslMemberNode>([
                        new DslPropertyNode("__dispatchResult", output, table.Location)]), table.Location), table.Location));
                }
                var lowered = table with { Kind = "procedure", Members = Array.AsReadOnly(result.ToArray()) };
                if (_templateScopes.TryGetValue(table, out var scope)) _templateScopes.Add(lowered, scope);
                return lowered;
            }
        }
    }
}
