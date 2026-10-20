using CoreTheSpire.Engine;

namespace CoreTheSpire.Engine.Tests;

internal static class AuthoringChecks
{
    private static string CounterSource => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "counter.spire"));
    private static RuleSet Link(string source) => new RuleSetBuilder().Add(EngineDslCompiler.Compile(source, "game.spire")).Build();

    public static void Run()
    {
        Check.Run("DSL config drives initialization, action, suspended choice, event, projection and terminal", () =>
        {
            var session = new Session(Link(CounterSource), 42, ["player"]);
            var player = session.Participant(0);
            session.Start();
            Submit(session);
            Check.Equal(SessionStatus.AwaitingDecision, session.Status, "suspended");
            Check.Equal(1L, Counter(session, "Attempts"), "effect before decision committed");
            Check.Equal(0L, Counter(session, "Applied"), "listener has not run");
            Answer(session, 1);
            Check.Equal(2L, Counter(session, "Score"), "selected config increment");
            Check.Equal(1L, Counter(session, "Applied"), "event before action continuation");
            Submit(session);
            Answer(session, 0);
            Check.Equal(SessionStatus.Terminal, session.Status, "declared terminal");
            Check.Equal(2L, Counter(session, "Attempts"), "resume never repeats previous writes");
            Check.Equal(2L, Counter(session, "Applied"), "listener runs once per event");
            Check.Equal("tutorial:ReachedTarget", session.Terminal!.Definition.QualifiedId, "declared result");
            Check.Equal(0, session.LegalActions(player).Count, "terminal stops actions");
        });

        Check.Run("DSL editing the rule file changes behavior without game-specific host code", () =>
        {
            var source = CounterSource.Replace("fields = { Delta = 2 }", "fields = { Delta = 4 }");
            var session = new Session(Link(source), 42, ["player"]);
            session.Start(); Submit(session); Answer(session, 1);
            Check.Equal(4L, Counter(session, "Score"), "edited config has effect");
            Check.Equal(SessionStatus.Terminal, session.Status, "new increment changes time to terminal");
        });

        Check.Run("DSL syntax, unknown properties/names and type errors keep file/line/column", () =>
        {
            var syntax = Check.Throws<DslSourceException>(() => EngineDslCompiler.Compile(
                "using namespace broken;\ndsl_version = @", "syntax.spire"), "bad syntax");
            Check.Equal("syntax.spire", syntax.Location.Source, "syntax source");
            Check.Equal(2, syntax.Location.Line, "syntax line");
            foreach (var source in new[] {
                CounterSource.Replace("capacity = 2", "capcity = 2"),
                CounterSource.Replace("field = Counter.Applied", "field = Counter.Missing"),
                CounterSource.Replace("fields = { Delta = 2 }", "fields = { Delta = 2 Delta = 3 }") })
            {
                var failure = Check.Throws<DslSourceException>(() => Link(source), "binding failure");
                Check.Equal("game.spire", failure.Location.Source, "binding source");
                Check.True(failure.Location.Line > 1 && failure.Location.Column > 0, "binding locator");
            }
            var type = Check.Throws<EngineFaultException>(() => Link(CounterSource.Replace(
                "fields = { Delta = 2 }", "fields = { Delta = true }")), "type mismatch");
            Check.Equal(EngineFaultCode.TypeError, type.Code, "same linked type checker");
            Check.True(type.Fault.Context?.StartsWith("game.spire(") == true, "type source locator");
        });

        Check.Run("DSL finite loops, filters, sort, aggregate, pure calls and enum parameters use common IR", () =>
        {
            var rules = Link("""
                using namespace operations;
                dsl_version = "core/0.3"
                start = [Setup]
                enum Mode { members = [Ready, Done] }
                entity Item { fields = { Number = { type = Int default = 0 } } }
                collection Items { element = Item }
                procedure Setup {
                    locals = { item = Binding }
                    body = { repeat = { count = 3 body = {
                        create = { type = Item scope = frameScope() into = item collection = Items }
                    } } }
                }
                procedure Increment {
                    parameters = { value = Int }
                    pure = true result = Int
                    body = { return = { value = add(value, 1) } }
                }
                terminal Finished { fields = { Total = Int } }
                action Run {
                    parameters = { mode = Enum(Mode) }
                    choices = { mode = [enum(Mode, Ready)] }
                    requires = equal(mode, enum(Mode, Ready))
                    body = {
                        forEach = {
                            query = sort(where(collection(Items), i, equal(field(i, Item.Number), 0)), i, field(i, Item.Number))
                            item = item
                            body = { set = { target = item field = Item.Number value = pureCall(Increment, 4) } }
                        }
                        terminal = { result = Finished fields = { Total = aggregate(collection(Items), Sum, i, field(i, Item.Number)) } }
                    }
                }
                """);
            var session = new Session(rules, 1, ["player"]);
            session.Start();
            var action = session.LegalActions(session.Participant(0)).Single();
            session.Submit(action.Definition, session.Participant(0), action.Bindings.Single().Take(1).ToArray());
            Check.Equal(15L, session.Terminal!.Fields.Single().AsInt(), "actual loop and pure result");
        });

        Check.Run("DSL dependency binding calls a library procedure without a second runtime", () =>
        {
            var library = EngineDslCompiler.Compile("""
                using namespace library;
                dsl_version = "core/0.3"
                procedure Number { pure = true result = Int body = { return = { value = 7 } } }
                """, "library.spire");
            var game = EngineDslCompiler.Compile("""
                using namespace game;
                dsl_version = "core/0.3" dependencies = [library] start = [Setup]
                terminal Done { fields = { Value = Int } }
                procedure Setup { body = { terminal = { result = Done fields = { Value = pureCall(library:Number) } } } }
                """, "dependent.spire", [library]);
            var session = new Session(new RuleSetBuilder().Add(library).Add(game).Build(), 1, ["player"]);
            session.Start();
            Check.Equal(7L, session.Terminal!.Fields.Single().AsInt(), "library result");
        });

        Check.Run("DSL imported listener bindings do not resolve to a same-named local procedure", () =>
        {
            var library = EngineDslCompiler.Compile("""
                using namespace listeners;
                dsl_version = "core/0.3"
                entity Unit { fields = { Value = { type = Int default = 0 } } }
                event Changed { payload = { unit = Ref(Unit) } }
                procedure Reaction {
                    parameters = { unit = Ref(Unit) }
                    body = { set = { target = unit field = Unit.Value value = 8 } }
                }
                """);
            var game = EngineDslCompiler.Compile("""
                using namespace consumer;
                dsl_version = "core/0.3" dependencies = [listeners] start = [Setup]
                procedure Reaction { parameters = { unrelated = Int } body = {} }
                subscription Listener {
                    event = listeners:Changed reaction = listeners:Reaction
                    recheck = equal(field(unit, listeners:Unit.Value), 0)
                }
                terminal Done { fields = { Value = Int } }
                procedure Setup {
                    locals = { item = Binding }
                    body = {
                        create = { type = listeners:Unit scope = frameScope() into = item }
                        emit = { event = listeners:Changed payload = { unit = item } }
                        terminal = { result = Done fields = { Value = field(item, listeners:Unit.Value) } }
                    }
                }
                """, dependencies: [library]);
            var session = new Session(new RuleSetBuilder().Add(library).Add(game).Build(), 1, ["player"]);
            session.Start();
            Check.Equal(8L, session.Terminal!.Fields.Single().AsInt(), "imported listener and recheck use imported parameters");
        });

        Check.Run("DSL game-owned declarations expand to common IR in source order with versioned identity", () =>
        {
            const string source = """
                using namespace extensions;
                dsl_version = "core/0.3" start = [Setup]
                token Piece { value = 7 }
                token Other { value = 9 }
                terminal Done { fields = { Value = Int } }
                procedure Setup {
                    locals = { item = Binding }
                    body = {
                        create = { type = Piece scope = frameScope() into = item }
                        terminal = { result = Done fields = { Value = field(item, Piece.Value) } }
                    }
                }
                """;
            var extension = new TokenExtension("tokens/1");
            var package = EngineDslCompiler.Compile(source, "extended.spire", extensions: [extension]);
            Check.Equal("Piece,Other", string.Join(',', extension.Declared), "extension declaration order");
            var rules = new RuleSetBuilder().Add(package).Build();
            var session = new Session(rules, 1, ["player"]);
            session.Start();
            Check.Equal(7L, session.Terminal!.Fields.Single().AsInt(), "extension field used by core program");
            var revised = EngineDslCompiler.Compile(source, extensions: [new TokenExtension("tokens/2")]);
            Check.True(package.ContentHash != revised.ContentHash, "extension version changes package identity");
            Check.True(rules.Identity != new RuleSetBuilder().Add(revised).Build().Identity, "extension version changes linked identity");
            Check.Throws<DslSourceException>(() => EngineDslCompiler.Compile(source), "unknown extension rejected");
            Check.Throws<DslSourceException>(() => EngineDslCompiler.Compile(source,
                extensions: [extension, new TokenExtension("conflict/1")]), "duplicate ownership rejected");
            Check.Throws<DslSourceException>(() => EngineDslCompiler.Compile(source,
                extensions: [new TokenExtension("reserved/1", "entity")]), "core declaration cannot be replaced");
        });

        Check.Run("FREEZE built symbols and their collections reject mutation, while session state remains mutable", () =>
        {
            var builder = new EnginePackageBuilder("freeze");
            var type = builder.EntityType("Thing");
            type.AddField("Value", FieldTypeSchema.Int());
            var action = builder.Action("Act");
            var body = builder.ActionBody(action); body.Body();
            var procedure = builder.Procedure("Proc"); procedure.Body();
            var projection = builder.Projection("View"); projection.ParticipantRole = "player";
            var view = projection.Type(type);
            var package = builder.Build();
            Check.Fault(EngineFaultCode.PackageAlreadyBuilt, () => type.AddField("Late", FieldTypeSchema.Int()), "late field");
            Check.Fault(EngineFaultCode.PackageAlreadyBuilt, () => action.Guard = body.Const(false), "late body/guard");
            Check.Fault(EngineFaultCode.PackageAlreadyBuilt, () => procedure.Symbol.Parameters.Clear(), "parameter list");
            Check.Fault(EngineFaultCode.PackageAlreadyBuilt, () => view.IncludeHiddenFields = true, "projection child");
            var rules = new RuleSetBuilder().Add(package).Build();
            Check.Throws<NotSupportedException>(() => ((IList<EntityTypeDefinition>)rules.Types).Clear(), "linked table readonly");
            Check.Throws<NotSupportedException>(() => ((IDictionary<int, TypeProjection>)rules.Projections.Single().Types).Clear(), "linked projection readonly");
            var session = new Session(rules, 1, ["player"]);
            var instance = session.State.CreateInstance(rules.Types.Single().Id, session.State.RootScope);
            session.State.SetField(instance, 0, Value.Of(12L));
            Check.Equal(12L, session.State.GetField(instance, 0).AsInt(), "state stays mutable");
        });

        Check.Run("FREEZE IR arguments, block instructions and Value lists cannot alias writable input arrays", () =>
        {
            Expr[] arguments = [new ConstExpr(Value.Of(1L))];
            var call = new CallInst(0, arguments);
            arguments[0] = new ConstExpr(Value.Of(99L));
            Check.Equal(1L, ((ConstExpr)call.Arguments[0]).Value.AsInt(), "input argument copy");
            var block = new Block([call]);
            Check.Throws<NotSupportedException>(() => ((IList<Inst>)block.Instructions).Clear(), "block readonly");
            long[] values = [1]; var value = Value.OfInts(values); values[0] = 99;
            Check.Equal(1L, value.AsIntList()[0], "input value copy");
            Check.Throws<NotSupportedException>(() => ((IList<long>)value.AsIntList())[0] = 99, "value view readonly");
        });

        Check.Run("FREEZE sharing a package across links does not change existing rule content or identity", () =>
        {
            var package = EngineDslCompiler.Compile(CounterSource);
            var first = new RuleSetBuilder().Add(package).Build();
            var identity = first.Identity;
            var extra = new EnginePackageBuilder("extra").Build();
            var second = new RuleSetBuilder().Add(extra).Add(package).Build();
            Check.Equal(identity, first.Identity, "first identity stable");
            Check.Equal(0, first.Types.Single(type => type.QualifiedId == "tutorial:Counter").PackageIndex, "first index");
            Check.Equal(1, second.Types.Single(type => type.QualifiedId == "tutorial:Counter").PackageIndex, "independent second index");
            Check.Equal(first.Identity, new RuleSetBuilder().Add(package).Build().Identity, "relink identity stable");
        });
    }

    private sealed class TokenExtension(string identity, string kind = "token") : IEngineDslExtension
    {
        public string Identity => identity;
        public IReadOnlyList<string> DeclarationKinds { get; } = [kind];
        public List<string> Declared { get; } = [];
        private readonly Dictionary<string, EntityTypeSymbol> _types = [];
        public void Declare(DslDeclarationNode declaration, EnginePackageBuilder package)
        {
            Declared.Add(declaration.Name);
            _types.Add(declaration.Name, package.EntityType(declaration.Name));
        }
        public void Bind(DslDeclarationNode declaration, EnginePackageBuilder package)
        {
            var literal = (DslNumberNode)declaration.Members.OfType<DslPropertyNode>().Single().Value;
            _types[declaration.Name].AddField("Value", FieldTypeSchema.Int(), Value.Of(long.Parse(literal.Literal)));
        }
    }

    private static long Counter(Session session, string field) => session.Observe(session.Participant(0), "tutorial:PlayerView")
        .Instances.Single(instance => instance.TypeId == "tutorial:Counter").Fields.Single(value => value.Name == field).Value.AsInt();
    private static void Submit(Session session) => session.Submit(session.LegalActions(session.Participant(0)).Single().Definition,
        session.Participant(0), []);
    private static void Answer(Session session, int index) => session.Answer(new DecisionAnswer(session.PendingDecision!.RequestId,
        Instances: [session.PendingDecision.Candidates[index]]));
}
