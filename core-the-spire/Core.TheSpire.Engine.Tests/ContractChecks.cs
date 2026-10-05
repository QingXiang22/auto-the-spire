using CoreTheSpire.Engine;

namespace CoreTheSpire.Engine.Tests;

internal static class ContractChecks
{
    private static RuleSet Link(string source) => new RuleSetBuilder()
        .Add(EngineDslCompiler.Compile(source, "contract.spire")).Build();

    public static void Run()
    {
        Check.Run("CONTRACT startup follows package/declaration order and resumes pending initializers once", () =>
        {
            var library = EngineDslCompiler.Compile("""
                using namespace initial;
                dsl_version = "core/0.1" start = [Create]
                entity Ledger { fields = { Sequence = { type = Int default = 0 } } }
                procedure Create {
                    locals = { item = Binding }
                    body = { create = { type = Ledger scope = frameScope() into = item } }
                }
                """, "initial.spire");
            var game = EngineDslCompiler.Compile("""
                using namespace ordered;
                dsl_version = "core/0.2" dependencies = [initial] start = [First, Second, Third]
                procedure First {
                    locals = { answer = Bool }
                    body = {
                        set = { target = first(type(initial:Ledger)) field = initial:Ledger.Sequence value = 1 }
                        decide = { participant = participant(1) kind = Bool prompt = "continue" into = answer }
                    }
                }
                procedure Second {
                    body = { set = { target = first(type(initial:Ledger)) field = initial:Ledger.Sequence
                        value = add(multiply(field(first(type(initial:Ledger)), initial:Ledger.Sequence), 10), 2) } }
                }
                procedure Third {
                    body = { set = { target = first(type(initial:Ledger)) field = initial:Ledger.Sequence
                        value = add(multiply(field(first(type(initial:Ledger)), initial:Ledger.Sequence), 10), 3) } }
                }
                """, "ordered.spire", [library]);
            var rules = new RuleSetBuilder().Add(library).Add(game).Build();
            var session = new Session(rules, 1, ["player"], new SessionBudget(MaxCallDepth: 1),
                debug: new SessionDebugOptions());
            session.Start();
            var instance = session.State.AllInstances().Single();
            var ledger = rules.Types.Single(type => type.QualifiedId == "initial:Ledger");
            var sequence = ledger.Fields.Single();
            Check.Equal(SessionStatus.AwaitingDecision, session.Status, "first initializer pauses");
            Check.Equal(1L, session.State.GetField(instance, sequence.Index).AsInt(), "later initializers have not run");
            session.Answer(new DecisionAnswer(session.PendingDecision!.RequestId, Flag: true));
            Check.Equal(SessionStatus.Running, session.Status, "startup resumes");
            Check.Equal(123L, session.State.GetField(instance, sequence.Index).AsInt(), "remaining initializers run in order once");
            Check.Equal(1, session.State.AllInstances().Count(), "library initializer is not replayed");
            Check.Sequence(new[] { "initial:Create", "ordered:First", "ordered:Second", "ordered:Third" },
                session.Debug!.Records.Where(record => record.Kind == SessionDebugKind.ProcedureEnter).Select(record => record.Subject),
                "trace follows actual entry order");
        });

        Check.Run("CONTRACT typed enum decisions expose bounds and reject invalid answers without consuming the request", () =>
        {
            var session = new Session(Link(EnumSource), 1, ["player"]);
            var action = session.LegalActions(session.Participant(0)).Single();
            session.Submit(action.Definition, session.Participant(0), action.Bindings.Single());
            var request = session.PendingDecision!;
            Check.Equal(0L, request.Minimum, "first member ordinal");
            Check.Equal(2L, request.Maximum, "last member ordinal");
            var hash = session.State.StateHash();
            foreach (var ordinal in new[] { -1, 3, int.MaxValue })
            {
                Check.Fault(EngineFaultCode.InvalidDecisionResponse,
                    () => session.Answer(new DecisionAnswer(request.RequestId, EnumOrdinal: ordinal)), "invalid member");
                Check.Equal(SessionStatus.AwaitingDecision, session.Status, "invalid answer does not resume");
                Check.Equal(request, session.PendingDecision, "same request remains pending");
                Check.Equal(hash, session.State.StateHash(), "no state mutation");
            }
            session.Answer(new DecisionAnswer(request.RequestId, EnumOrdinal: 2));
            Check.Equal(SessionStatus.Terminal, session.Status, "valid last member resumes");
            Check.Equal(2, session.Terminal!.Fields.Single().AsEnumOrdinal(), "typed value reaches continuation");
        });

        Check.Run("CONTRACT enum decisions restrict declared members and support a parameter result slot", () =>
        {
            var package = new EnginePackageBuilder("subset");
            var mode = package.Enum("Mode"); mode.Member("A"); mode.Member("B"); mode.Member("C");
            var action = package.ActionBody(package.Action("Choose"));
            var result = action.Param("selection", FieldTypeSchema.EnumOf(mode));
            action.Choose("selection", action.EnumConst(mode, "A"));
            var terminal = package.TerminalResult("Chosen");
            var field = terminal.AddField("Selection", FieldTypeSchema.EnumOf(mode));
            action.Body(action.Decide(action.Const(Value.OfParticipant(new ParticipantId(1))),
                DecisionResponseKind.Enum, "subset", result.DeclarationIndex,
                minimum: action.Const(1L), maximum: action.Const(1L)),
                action.SetTerminal(terminal, (field, action.Param(result.DeclarationIndex))));
            var session = new Session(new RuleSetBuilder().Add(package.Build()).Build(), 1, ["player"]);
            var legal = session.LegalActions(session.Participant(0)).Single();
            session.Submit(legal.Definition, session.Participant(0), legal.Bindings.Single());
            var request = session.PendingDecision!;
            Check.Equal(1L, request.Minimum, "subset minimum");
            Check.Equal(1L, request.Maximum, "subset maximum");
            foreach (var ordinal in new[] { 0, 2 })
                Check.Fault(EngineFaultCode.InvalidDecisionResponse,
                    () => session.Answer(new DecisionAnswer(request.RequestId, EnumOrdinal: ordinal)), "member outside subset");
            session.Answer(new DecisionAnswer(request.RequestId, EnumOrdinal: 1));
            Check.Equal(1, session.Terminal!.Fields.Single().AsEnumOrdinal(), "parameter slot resumes correctly");

            foreach (var bounds in new[] { "min = -1", "max = 3", "min = 2 max = 1" })
            {
                var invalid = new Session(Link(EnumSource.Replace("prompt = \"pick\"", "prompt = \"pick\" " + bounds)), 1, ["player"]);
                var pick = invalid.LegalActions(invalid.Participant(0)).Single();
                Check.Fault(EngineFaultCode.ValueOutOfRange,
                    () => invalid.Submit(pick.Definition, invalid.Participant(0), pick.Bindings.Single()), "invalid declared range");
                Check.Equal(SessionStatus.Faulted, invalid.Status, "invalid rule faults before publishing a request");
                Check.True(invalid.PendingDecision is null, "no invalid request published");
            }
        });

        Check.Run("CONTRACT enum decisions require a declared nonempty enum result type at link time", () =>
        {
            Check.Fault(EngineFaultCode.TypeError,
                () => Link(EnumSource.Replace("selection = Enum(Mode)", "selection = Binding")), "untyped result");
            Check.Fault(EngineFaultCode.SchemaInvalid,
                () => Link(EnumSource.Replace("[A, B, C]", "[]")), "empty result enum");
        });

        Check.Run("CONTRACT previous runtime packages cannot silently link under the frozen semantics", () =>
        {
            var previous = new EnginePackageBuilder("previous") { EngineCompatibility = "engine/ir@0.2" };
            Check.Fault(EngineFaultCode.SchemaInvalid,
                () => new RuleSetBuilder().Add(previous.Build()).Build(), "explicit migration is required");
        });
    }

    private const string EnumSource = """
        using namespace enumeration;
        dsl_version = "core/0.2"
        enum Mode { members = [A, B, C] }
        terminal Chosen { fields = { Selection = Enum(Mode) } }
        action Choose {
            parameters = { unused = Int }
            choices = { unused = [7] }
            locals = { selection = Enum(Mode) }
            body = {
                decide = { participant = participant(1) kind = Enum prompt = "pick" into = selection }
                terminal = { result = Chosen fields = { Selection = selection } }
            }
        }
        """;
}
