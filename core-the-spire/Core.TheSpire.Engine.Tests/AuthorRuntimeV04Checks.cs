using CoreTheSpire.Engine;

namespace CoreTheSpire.Engine.Tests;

internal static class AuthorRuntimeV04Checks
{
    public static void Run()
    {
        Check.Run("V04 pending action queries preserve the suspended pure-guard decision and resume once", () =>
        {
            var package = EngineDslCompiler.Compile("""
                using namespace pending;
                dsl_version = "core/0.3" start = [Setup]
                procedure Available { pure = true result = Bool body = { return = { value = true } } }
                terminal Done { fields = { Accepted = Bool } }
                procedure Setup {
                    locals = { answer = Bool }
                    body = {
                        decide = { participant = participant(1) kind = Bool prompt = "continue" into = answer }
                        terminal = { result = Done fields = { Accepted = answer } }
                    }
                }
                action Run { requires = pureCall(Available) body = { fail = { message = "unreachable" } } }
                """, "pending.spire");
            var session = new Session(new RuleSetBuilder().Add(package).Build(), 7, ["player"]);
            session.Start();
            var pending = session.PendingDecision;
            var version = session.StateVersion;
            for (var i = 0; i < 3; i++) Check.Equal(0, session.LegalActions(session.Participant(0)).Count, "no submit-able actions");
            Check.Equal(SessionStatus.AwaitingDecision, session.Status, "not faulted");
            Check.True(ReferenceEquals(pending, session.PendingDecision), "same decision");
            Check.Equal(version, session.StateVersion, "read does not mutate state");
            session.Answer(new DecisionAnswer(pending!.RequestId, Flag: true));
            Check.True(session.Terminal!.Fields.Single().AsBool(), "continuation finished");
        });

        Check.Run("V04 text binds existing game symbols and named input order produces the expected value", () =>
        {
            var package = new EnginePackageBuilder("host");
            var thing = package.EntityType("Thing");
            thing.AddField("Score", FieldTypeSchema.Int(), Value.Of(0L));
            EngineDslCompiler.BindInto("""
                using namespace host; dsl_version = "core/0.3" start = [Setup]
                procedure Difference {
                    pure = true result = Int parameters = { left = Int right = Int }
                    body = { return = { value = subtract(left, right) } }
                }
                procedure SetScore {
                    parameters = { target = Ref(Thing) value = Int }
                    body = { set = { target = target field = Thing.Score value = value } }
                }
                procedure Setup {
                    locals = { item = Binding }
                    body = {
                        create = { type = Thing scope = frameScope() into = item }
                        call = { procedure = SetScore args = {
                            value = pureCall(Difference, args = { right = 3 left = 10 }) target = item
                        } }
                    }
                }
                projection View { role = player types = { Thing = {} } }
                """, package, "host-rules.spire");
            var session = new Session(new RuleSetBuilder().Add(package.Build()).Build(), 1, ["player"]);
            session.Start();
            Check.Equal(7L, session.Observe(session.Participant(0), "host:View").Instances.Single().Fields.Single().Value.AsInt(), "named binding");
            Check.True(package.CompilerIdentity.Contains("core-dsl/0.3"), "compiler identity included");
            Check.Throws<DslSourceException>(() => EngineDslCompiler.Compile("""
                using namespace bad; dsl_version = "core/0.3"
                procedure P { parameters = { value = Int } body = {} }
                procedure Q { body = { call = { procedure = P args = { typo = 1 } } } }
                """), "unknown named input rejected");
            Check.Throws<DslSourceException>(() => EngineDslCompiler.Compile("""
                using namespace bad; dsl_version = "core/0.3"
                procedure P { parameters = { value = Int } body = {} }
                procedure Q { body = { call = { procedure = P args = {} } } }
                """), "missing named input rejected");
        });

        Check.Run("V04 imported extension templates compile and execute through the common loader", () =>
        {
            var folder = Path.Combine(Path.GetTempPath(), "spire-v04-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                File.WriteAllText(Path.Combine(folder, "library.spire"), """
                    using namespace library; dsl_version = "core/0.3"
                    template Item { parameters = { value = { type = Int default = 9 } }
                        body = { token Token { value = arg(value) } } }
                    """);
                File.WriteAllText(Path.Combine(folder, "game.spire"), """
                    using namespace game; dsl_version = "core/0.3" imports = ["library.spire"]
                    use Instance { template = library:Item args = { value = 17 } }
                    start = [Setup]
                    procedure Setup { locals = { item = Binding } body = {
                        create = { type = Instance.Token scope = frameScope() into = item }
                    } }
                    projection View { role = player types = { Instance.Token = {} } }
                    """);
                var rules = EngineDslCompiler.LoadFiles([Path.Combine(folder, "game.spire")], [new TokenExtension()]);
                var session = new Session(rules, 1, ["player"]); session.Start();
                Check.Equal(17L, session.Observe(session.Participant(0), "game:View").Instances.Single().Fields.Single().Value.AsInt(), "instantiated extension executed");
            }
            finally
            {
                var cleanup = Path.GetFullPath(folder);
                var tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (!cleanup.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Temporary library path escaped the test directory.");
                Directory.Delete(cleanup, recursive: true);
            }
        });

        Check.Run("V04 source annotations preserve precise nested sources and reject old runtime identity", () =>
        {
            var child = new FailInst("inner").At(new("inner.spire", 7, 3));
            var outer = new Block([child]).At(new("outer.spire", 1, 1));
            Check.Equal("inner.spire", child.SourceLocation!.Value.Source, "nested source preserved");
            var old = new EnginePackageBuilder("old") { EngineCompatibility = "engine/ir@0.3" };
            Check.Fault(EngineFaultCode.SchemaInvalid, () => new RuleSetBuilder().Add(old.Build()).Build(), "0.3 packages require rebuilding");
        });
    }

    private sealed class TokenExtension : IEngineDslExtension
    {
        public string Identity => "token/0.3";
        public IReadOnlyList<string> DeclarationKinds => ["token"];
        private readonly Dictionary<string, EntityTypeSymbol> types = [];
        public void Declare(DslDeclarationNode node, EnginePackageBuilder package) => types[node.Name] = package.EntityType(node.Name);
        public void Bind(DslDeclarationNode node, EnginePackageBuilder package) => types[node.Name].AddField("Value",
            FieldTypeSchema.Int(), Value.Of(long.Parse(((DslNumberNode)node.Members.OfType<DslPropertyNode>().Single().Value).Literal)));
    }
}
