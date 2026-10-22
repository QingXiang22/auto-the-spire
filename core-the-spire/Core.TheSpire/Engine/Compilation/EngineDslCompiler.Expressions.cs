namespace CoreTheSpire.Engine;

public static partial class EngineDslCompiler
{
    private sealed partial class CoreDslBinding
    {
        private sealed partial class ProgramContext
        {
            private readonly CoreDslBinding _compiler;
            private readonly IrScope _scope;
            private readonly Dictionary<string, Expr> _bindings = new(StringComparer.Ordinal);
            private readonly Dictionary<string, int> _locals = new(StringComparer.Ordinal);

            public ProgramContext(CoreDslBinding compiler, IrScope scope)
            {
                _compiler = compiler;
                _scope = scope;
                var parameters = scope is ProcedureBuilder procedure ? procedure.Symbol.Parameters :
                    ((ActionBuilder)scope).Action.Parameters;
                for (var i = 0; i < parameters.Count; i++) Add(parameters[i].Name, scope.Param(i));
                for (var i = 0; i < scope.LocalFields.Count; i++)
                {
                    var name = scope.LocalFields[i].Name;
                    var slot = scope.ParameterCount + i;
                    Add(name, scope.LocalRef(slot));
                    _locals.Add(name, slot);
                }
            }

            private void Add(string name, Expr expression)
            {
                if (!_bindings.TryAdd(name, expression))
                    throw Error(_compiler._source.Location, $"Duplicate parameter/local '{name}'.");
            }

            private int Slot(DslValueNode name) => _locals.TryGetValue(Text(name), out var slot) ? slot :
                throw Error(name.Location, $"Unknown local slot '{Text(name)}'.");

            private T Binding<T>(DslValueNode name, Expr expression, Func<T> body)
            {
                var key = Text(name);
                if (_bindings.ContainsKey(key)) throw Error(name.Location, $"Query binding '{key}' shadows another binding.");
                _bindings.Add(key, expression);
                try { return body(); }
                finally { _bindings.Remove(key); }
            }

            private static T Source<T>(T node, DslSourceLocation location) where T : IrNode
            {
                node.SourceLocation = location;
                return node;
            }

            public Expr Expression(DslValueNode node) => Located(node.Location, () =>
            {
                if (node is DslNameNode name && _bindings.TryGetValue(name.Name, out var binding)) return binding;
                if (node is not DslCallNode call) return Source(_scope.Const(_compiler.Constant(node)), node.Location);
                var args = new Arguments(call);
                Expr E(string name, int index) => Expression(args.Required(name, index));
                Query Q(string name, int index) => Query(args.Required(name, index));
                Expr result = call.Name switch
                {
                    "participant" => _scope.Const(_compiler.Constant(call)),
                    "enum" => _scope.EnumConst(_compiler.Symbol<EnumTypeSymbol>(args.Required("type", 0)),
                        Text(args.Required("member", 1))),
                    "field" => _scope.Field(E("target", 0), _compiler.Field(args.Required("field", 1))),
                    "frameScope" => new FrameScopeExpr(),
                    "gameSeed" => new RandomValueExpr(RandomValueOp.GameSeed),
                    "hash32" => new RandomValueExpr(RandomValueOp.Hash32, E("text", 0)),
                    "hash64" => new Word64Expr(Word64Op.HashUtf8, E("text", 0)),
                    "uint64Add" => new Word64Expr(Word64Op.Add, E("left", 0), E("right", 1)),
                    "startsWith" => new TextExpr(TextOp.StartsWith, E("text", 0), E("prefix", 1)),
                    "substring" => new TextExpr(TextOp.Substring, E("text", 0), E("start", 1)),
                    "float32" => new RandomValueExpr(RandomValueOp.Float32, E("value", 0)),
                    "float32Add" => new RandomValueExpr(RandomValueOp.Float32Add, E("left", 0), E("right", 1)),
                    "float32Subtract" => new RandomValueExpr(RandomValueOp.Float32Subtract, E("left", 0), E("right", 1)),
                    "float32Minimum" => new RandomValueExpr(RandomValueOp.Float32Minimum, E("left", 0), E("right", 1)),
                    "float32Less" => new RandomValueExpr(RandomValueOp.Float32Less, E("left", 0), E("right", 1)),
                    "float32LessOrEqual" => new RandomValueExpr(RandomValueOp.Float32LessOrEqual, E("left", 0), E("right", 1)),
                    "float64" => new Binary64Expr(Binary64Op.FromNumber, E("value", 0)),
                    "float64Sqrt" => new Binary64Expr(Binary64Op.Sqrt, E("value", 0)),
                    "float64Log" => new Binary64Expr(Binary64Op.Log, E("value", 0)),
                    "float64Sin" => new Binary64Expr(Binary64Op.Sin, E("value", 0)),
                    "float64RoundToInt" => new Binary64Expr(Binary64Op.RoundToInt, E("value", 0)),
                    "float64Add" => new Binary64Expr(Binary64Op.Add, E("left", 0), E("right", 1)),
                    "float64Subtract" => new Binary64Expr(Binary64Op.Subtract, E("left", 0), E("right", 1)),
                    "float64Multiply" => new Binary64Expr(Binary64Op.Multiply, E("left", 0), E("right", 1)),
                    "float64Divide" => new Binary64Expr(Binary64Op.Divide, E("left", 0), E("right", 1)),
                    "float64Less" => new Binary64Expr(Binary64Op.Less, E("left", 0), E("right", 1)),
                    "float64LessOrEqual" => new Binary64Expr(Binary64Op.LessOrEqual, E("left", 0), E("right", 1)),
                    "entityScope" => _scope.EntityScope(E("entity", 0)),
                    "add" => _scope.Add(E("left", 0), E("right", 1)),
                    "subtract" => _scope.Subtract(E("left", 0), E("right", 1)),
                    "multiply" => _scope.Multiply(E("left", 0), E("right", 1)),
                    "divide" => _scope.Divide(E("left", 0), E("right", 1)),
                    "modulo" => _scope.Modulo(E("left", 0), E("right", 1)),
                    "minimum" => _scope.Minimum(E("left", 0), E("right", 1)),
                    "maximum" => _scope.Maximum(E("left", 0), E("right", 1)),
                    "equal" => _scope.Equal(E("left", 0), E("right", 1)),
                    "notEqual" => _scope.NotEqual(E("left", 0), E("right", 1)),
                    "less" => _scope.Less(E("left", 0), E("right", 1)),
                    "lessOrEqual" => _scope.LessOrEqual(E("left", 0), E("right", 1)),
                    "greater" => _scope.Greater(E("left", 0), E("right", 1)),
                    "greaterOrEqual" => _scope.GreaterOrEqual(E("left", 0), E("right", 1)),
                    "and" => _scope.And(E("left", 0), E("right", 1)),
                    "or" => _scope.Or(E("left", 0), E("right", 1)),
                    "not" => _scope.Not(E("value", 0)),
                    "intToFixed" => _scope.IntToFixed(E("value", 0)),
                    "fixedToInt" => _scope.FixedToInt(E("value", 0),
                        EnumValue(args.Optional("rounding", 1), FixedRounding.Truncate)),
                    "count" => _scope.Count(Q("query", 0)),
                    "exists" => _scope.Exists(Q("query", 0)),
                    "first" => _scope.First(Q("query", 0)),
                    "isNone" => _scope.IsNone(E("value", 0)),
                    "collectionIndex" => _scope.CollectionIndex(
                        _compiler.Symbol<CollectionSymbol>(args.Required("collection", 0)), E("item", 1)),
                    "concat" => _scope.Concat(args.Rest(0).Select(Expression).ToArray()),
                    "pureCall" => PureCall(call, args),
                    "foldEvent" => new FoldEventExpr(_compiler._package.Ref(_compiler.Symbol<EventSymbol>(args.Required("event", 0))), args.Rest(1).Select(Expression).ToArray()),
                    "aggregate" => Aggregate(args),
                    _ => throw Error(call.Location, $"Unknown expression '{call.Name}'."),
                };
                // Constants have their own strict argument validation.
                if (call.Name != "participant") args.Finish();
                return Source(result, node.Location);
            });

            private Expr PureCall(DslCallNode call, Arguments args)
            {
                var procedure = _compiler.Symbol<ProcedureSymbol>(args.Required("procedure", 0));
                var named = call.Arguments.Any(a => a.Name == "args") ||
                    call.Arguments.Count == 2 && call.Arguments[1].Value is DslBlockNode;
                return _scope.CallPure(procedure, named
                    ? CallArguments(procedure, args.Required("args", 1))
                    : args.Rest(1).Select(Expression).ToArray());
            }

            private Expr Aggregate(Arguments args)
            {
                var query = Query(args.Required("query", 0));
                var op = EnumValue(args.Required("operation", 1), AggregateOp.Count);
                var item = args.Optional("item", 2);
                var value = args.Optional("value", 3);
                if (value is not null && item is null) throw Error(value.Location, "Aggregate value needs an item binding.");
                return _scope.Aggregate(query, op, value is null ? null :
                    expression => Binding(item!, expression, () => Expression(value)));
            }

            public Query Query(DslValueNode node) => Located(node.Location, () =>
            {
                if (node is not DslCallNode call) throw Error(node.Location, "Expected a query call.");
                var args = new Arguments(call);
                Query result;
                switch (call.Name)
                {
                    case "type":
                        result = _scope.Type(_compiler.Symbol<EntityTypeSymbol>(args.Required("type", 0)),
                            args.Optional("scope", 1) is { } typeScope ? _compiler.Symbol<ScopeTypeSymbol>(typeScope) : null);
                        break;
                    case "collection":
                        result = _scope.Collection(_compiler.Symbol<CollectionSymbol>(args.Required("collection", 0)),
                            args.Optional("scope", 1) is { } collectionScope ? _compiler.Symbol<ScopeTypeSymbol>(collectionScope) : null);
                        break;
                    case "relation":
                        result = _scope.Relation(_compiler.Symbol<RelationSymbol>(args.Required("relation", 0)),
                            Expression(args.Required("owner", 1)));
                        break;
                    case "refList": result = new RefListQuery(Slot(args.Required("local", 0))); break;
                    case "where":
                    {
                        var source = Query(args.Required("query", 0));
                        var item = args.Required("item", 1);
                        var predicate = args.Required("predicate", 2);
                        result = _scope.Where(source, expression => Binding(item, expression, () => Expression(predicate)));
                        break;
                    }
                    case "sort":
                    {
                        var source = Query(args.Required("query", 0));
                        var item = args.Required("item", 1);
                        var key = args.Required("key", 2);
                        var descending = Boolean(args.Optional("descending", 3), false);
                        var query = (SortQuery)_scope.Sort(source, expression => Binding(item, expression, () => Expression(key)), descending);
                        result = Boolean(args.Optional("stable", 4), true) ? query : new SortQuery(query.Source, query.Key, query.Descending, query.ItemSlot, false);
                        break;
                    }
                    case "take": result = _scope.Take(Query(args.Required("query", 0)), Expression(args.Required("count", 1))); break;
                    case "skip": result = _scope.Skip(Query(args.Required("query", 0)), Expression(args.Required("count", 1))); break;
                    default: throw Error(node.Location, $"Unknown query '{call.Name}'.");
                }
                args.Finish();
                return Source(result, node.Location);
            });
        }
    }
}
