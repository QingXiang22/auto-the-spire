namespace CoreTheSpire.Engine;

public static partial class EngineDslCompiler
{
    private sealed partial class CoreDslBinding
    {
        private sealed partial class ProgramContext
        {
            public Block Body(DslValueNode node)
            {
                var instructions = new List<Inst>();
                foreach (var member in Properties.Members(node))
                {
                    if (member is not DslPropertyNode command)
                        throw Error(member.Location, "A program body contains command properties.");
                    instructions.Add(Located(command.Location, () => Instruction(command)));
                }
                return Source(new Block(instructions), node.Location);
            }

            private Inst Instruction(DslPropertyNode command)
            {
                var p = Properties.Of(command.Value, CommandProperties(command.Name));
                Expr E(string name) => Expression(p.Required(name));
                Expr? Optional(string name) => p.Get(name) is { } value ? Expression(value) : null;
                int Into() => Slot(p.Required("into"));
                InsertPosition Position() => EnumValue(p.Get("position"), InsertPosition.End);
                Inst instruction;
                switch (command.Name)
                {
                    case "let": instruction = _scope.SetLocal(Into(), E("value")); break;
                    case "set": instruction = _scope.SetField(E("target"), _compiler.Field(p.Required("field")), E("value")); break;
                    case "create":
                    {
                        var type = _compiler.Symbol<EntityTypeSymbol>(p.Required("type"));
                        instruction = _scope.CreateEntity(type, E("scope"), Into(),
                            Initializers(p.Get("fields"), type.Field),
                            p.Get("collection") is { } collection ? _compiler.Symbol<CollectionSymbol>(collection) : null,
                            Position());
                        break;
                    }
                    case "createScope": instruction = _scope.CreateScope(
                        _compiler.Symbol<ScopeTypeSymbol>(p.Required("type")), Into(), Optional("parent")); break;
                    case "destroy": instruction = _scope.Destroy(E("target")); break;
                    case "destroyScope": instruction = _scope.DestroyScope(E("scope")); break;
                    case "addToCollection": instruction = _scope.AddToCollection(
                        _compiler.Symbol<CollectionSymbol>(p.Required("collection")), E("item"), Position(), Optional("index")); break;
                    case "removeFromCollection": instruction = _scope.RemoveFromCollection(
                        _compiler.Symbol<CollectionSymbol>(p.Required("collection")), E("item")); break;
                    case "moveInCollection": instruction = _scope.MoveInCollection(
                        _compiler.Symbol<CollectionSymbol>(p.Required("collection")), E("item"), Position(), Optional("index")); break;
                    case "shuffle": instruction = _scope.ShuffleCollection(
                        _compiler.Symbol<CollectionSymbol>(p.Required("collection")), E("stream")); break;
                    case "addRelation": instruction = _scope.AddRelation(
                        _compiler.Symbol<RelationSymbol>(p.Required("relation")), E("owner"), E("target")); break;
                    case "removeRelation": instruction = _scope.RemoveRelation(
                        _compiler.Symbol<RelationSymbol>(p.Required("relation")), E("owner"), E("target")); break;
                    case "clearRelation": instruction = _scope.ClearRelation(
                        _compiler.Symbol<RelationSymbol>(p.Required("relation")), E("owner")); break;
                    case "sampleInt": instruction = _scope.SampleInt(E("stream"), E("min"), E("max"), Into()); break;
                    case "samplePick": instruction = new SamplePickInst(E("stream"), Query(p.Required("candidates")), Into()); break;
                    case "emit":
                    {
                        var eventType = _compiler.Symbol<EventSymbol>(p.Required("event"));
                        instruction = _scope.Emit(eventType, Initializers(p.Get("payload"), eventType.PayloadField));
                        break;
                    }
                    case "decide": instruction = _scope.Decide(E("participant"),
                        EnumValue(p.Required("kind"), DecisionResponseKind.EntityRef), Text(p.Required("prompt")), Into(),
                        p.Get("candidates") is { } candidates ? Query(candidates) : null, Optional("min"), Optional("max")); break;
                    case "call":
                    {
                        var procedure = _compiler.Symbol<ProcedureSymbol>(p.Required("procedure"));
                        var args = CallArguments(procedure, p.Get("args"));
                        instruction = p.Get("into") is { } into ? _scope.Call(procedure, Slot(into), args) : _scope.Call(procedure, args);
                        break;
                    }
                    case "if": instruction = new IfInst(E("condition"), Body(p.Required("then")),
                        p.Get("else") is { } otherwise ? Body(otherwise) : null); break;
                    case "repeat": instruction = new RepeatInst(E("count"), Body(p.Required("body"))); break;
                    case "forEach":
                    {
                        var query = Query(p.Required("query"));
                        var item = p.Required("item");
                        var body = p.Required("body");
                        instruction = _scope.ForEach(query, expression => Binding(item, expression,
                            () => Body(body).Instructions));
                        break;
                    }
                    case "return": instruction = _scope.Return(Optional("value")); break;
                    case "terminal":
                    {
                        var terminal = _compiler.Symbol<TerminalResultSymbol>(p.Required("result"));
                        instruction = _scope.SetTerminal(terminal, Initializers(p.Get("fields"), name =>
                            terminal.Fields.FirstOrDefault(field => field.Name == name) ??
                            throw Faults.Unresolved($"Terminal '{terminal.Name}' has no field '{name}'.")));
                        break;
                    }
                    case "fail": instruction = _scope.Fail(Text(p.Required("message"))); break;
                    case "compute":
                    {
                        var asset = _compiler.Symbol<ComputeAssetSymbol>(p.Required("asset"));
                        var inputs = Initializers(p.Get("inputs"), name => asset.Inputs.FirstOrDefault(field => field.Name == name)
                            ?? throw Faults.Unresolved($"Compute '{asset.Name}' has no input '{name}'."));
                        var outputs = p.Get("outputs") is { } output ? Properties.Items(output).Select(field =>
                            (asset.Outputs.FirstOrDefault(candidate => candidate.Name == field.Name) ??
                                throw Error(field.Location, $"Compute '{asset.Name}' has no output '{field.Name}'."),
                                Slot(field.Value))).ToArray() : [];
                        instruction = _scope.Compute(asset, inputs, outputs);
                        break;
                    }
                    default: throw Error(command.Location, $"Unknown command '{command.Name}'.");
                }
                return Source(instruction, command.Location);
            }

            private Expr[] CallArguments(ProcedureSymbol procedure, DslValueNode? values)
            {
                if (values is not DslBlockNode) return values is null ? [] : List(values).Select(Expression).ToArray();
                if (!_compiler._modern) throw Error(values.Location, "Named procedure arguments require core/0.3.");
                var named = Properties.Items(values).ToDictionary(p => p.Name, StringComparer.Ordinal);
                foreach (var name in named.Keys)
                    if (!procedure.Parameters.Any(p => p.Name == name))
                        throw Error(named[name].Location, $"Procedure '{procedure.Name}' has no input '{name}'.");
                return procedure.Parameters.Select(parameter => named.TryGetValue(parameter.Name, out var value)
                    ? Expression(value.Value)
                    : throw Error(values.Location, $"Missing input '{parameter.Name}' for '{procedure.Name}'.")).ToArray();
            }

            private (FieldSymbol, Expr)[] Initializers(DslValueNode? fields, Func<string, FieldSymbol> resolve) =>
                fields is null ? [] : Properties.Items(fields).Select(field => Located(field.Location,
                    () => (resolve(field.Name), Expression(field.Value)))).ToArray();

            private static string[] CommandProperties(string command) => command switch
            {
                "let" => ["into", "value"], "set" => ["target", "field", "value"],
                "create" => ["type", "scope", "into", "fields", "collection", "position"],
                "createScope" => ["type", "into", "parent"],
                "destroy" => ["target"], "destroyScope" => ["scope"],
                "addToCollection" or "moveInCollection" => ["collection", "item", "position", "index"],
                "removeFromCollection" => ["collection", "item"], "shuffle" => ["collection", "stream"],
                "addRelation" or "removeRelation" => ["relation", "owner", "target"],
                "clearRelation" => ["relation", "owner"],
                "sampleInt" => ["stream", "min", "max", "into"], "samplePick" => ["stream", "candidates", "into"],
                "emit" => ["event", "payload"],
                "decide" => ["participant", "kind", "prompt", "into", "candidates", "min", "max"],
                "call" => ["procedure", "args", "into"], "if" => ["condition", "then", "else"],
                "repeat" => ["count", "body"], "forEach" => ["query", "item", "body"],
                "return" => ["value"], "terminal" => ["result", "fields"], "fail" => ["message"],
                "compute" => ["asset", "inputs", "outputs"],
                _ => throw new ArgumentException($"Unknown command '{command}'."),
            };
        }
    }
}
