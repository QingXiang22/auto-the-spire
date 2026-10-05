using CoreTheSpire.Engine;

namespace CoreTheSpire.Engine.Tests;

/// <summary>
/// R-CORE deliverable A and B.
///
/// <para><b>A — required reference construction window.</b> A non-optional <c>Ref</c> gameplay field may hold
/// <c>none</c> only while the construction process that created the instance is still open; the process is one host
/// command (or one <see cref="SessionState.RunAtomic{T}"/> unit for direct store use), so it may span instructions.
/// At the first stable boundary — command end, decision request, terminal result, projection or state hash — an open
/// construction is the explicit fault <see cref="EngineFaultCode.RequiredRefUnassigned"/> and the state must not be
/// published.</para>
///
/// <para><b>B — ExecutionOnly gameplay fields.</b> Entity gameplay fields are Persistent only; an execution-only
/// entity field is rejected at link time. Temporary values belong in frame slots, which are execution-only by
/// construction and must keep linking.</para>
///
/// Every check below is a counterexample first: it proves the engine can <em>distinguish</em> the wrong shape, not
/// merely accept the right one.
/// </summary>
internal static class RequiredRefChecks
{
    private const string Pkg = "ref-lifecycle:";

    public static void Run()
    {
        ConstructionWindowChecks();
        StableBoundaryChecks();
        StorageChecks();
        LifetimeChecks();
    }

    // -- A: the construction window ------------------------------------------------------------------------------

    private static void ConstructionWindowChecks()
    {
        Check.Run("R-CORE a construction window spans instructions inside one command", () =>
        {
            var session = NewSession(out var fixture);
            session.Start();

            // "Seed" creates node two without its required reference and assigns it two instructions later.
            Check.Equal(SessionStatus.Running, session.Status, "the command ended stable");
            Check.Equal(0, session.State.UnderConstruction.Count, "no open construction at the command boundary");
            var nodes = Members(session, fixture.Nodes);
            Check.Equal(2, nodes.Count, "both seeded nodes exist");
            Check.Equal(nodes[0], NextOf(session, fixture, nodes[1]), "the later instruction assigned the reference");
            Check.Equal(nodes[1], NextOf(session, fixture, nodes[0]), "the reference may point at the peer");
            Check.True(session.State.StateHash().Length > 0, "a stable state hashes");
        });

        Check.Run("R-CORE a required reference may be closed before the command ends", () =>
        {
            var session = NewSession(out var fixture);
            session.Start();
            session.Submit(Action(session, Pkg + "Finish"), Fixtures.White, []);

            Check.Equal(SessionStatus.Running, session.Status, "finishing the construction is legal");
            Check.Equal(0, session.State.UnderConstruction.Count, "the construction window closed");
            var created = Members(session, fixture.Nodes).Last();
            Check.Equal(created, NextOf(session, fixture, created), "the required reference points at itself");
        });

        Check.Run("R-CORE an open construction faults at the command boundary", () =>
        {
            var session = NewSession(out var fixture);
            session.Start();
            var orphan = Action(session, Pkg + "Orphan");
            var fault = Check.Fault(EngineFaultCode.RequiredRefUnassigned,
                () => session.Submit(orphan, Fixtures.White, []), "command boundary");

            Check.True(fault.Message.Contains("Next", StringComparison.Ordinal),
                "the fault names the unassigned required field");
            Check.True(!fault.Message.Contains("Peer", StringComparison.Ordinal),
                "the optional field is not reported as missing");
            Check.Equal(SessionStatus.Faulted, session.Status, "the session stopped");
            Check.Equal(EngineFaultCode.RequiredRefUnassigned, session.Fault!.Code, "recorded fault code");
            Check.Equal(null, session.Terminal, "no terminal result was published");
            Check.Equal(null, session.PendingDecision, "no decision was published");

            // Nothing may publish the half-built instance any more.
            Check.Fault(EngineFaultCode.SessionStateInvalid,
                () => session.Submit(orphan, Fixtures.White, []), "no command after the fault");
            Check.Equal(0, session.LegalActions(Fixtures.White).Count, "no candidate is offered");
            Check.Fault(EngineFaultCode.RequiredRefUnassigned,
                () => session.Observe(Fixtures.White, Pkg + "View"), "an unstable projection is refused");
        });

        Check.Run("R-CORE an open construction faults at the decision boundary", () =>
        {
            var session = NewSession(out var fixture);
            session.Start();
            Check.Fault(EngineFaultCode.RequiredRefUnassigned,
                () => session.Submit(Action(session, Pkg + "Suspend"), Fixtures.White, []), "decision boundary");

            Check.Equal(null, session.PendingDecision, "the decision request was never published");
            Check.Equal(SessionStatus.Faulted, session.Status, "the session stopped");
            Check.Equal(1, session.State.UnderConstruction.Count, "the diagnostic still names the open instance");
        });

        Check.Run("R-CORE an open construction faults at the terminal boundary", () =>
        {
            var session = NewSession(out var fixture);
            session.Start();
            Check.Fault(EngineFaultCode.RequiredRefUnassigned,
                () => session.Submit(Action(session, Pkg + "Win"), Fixtures.White, []), "terminal boundary");

            Check.Equal(null, session.Terminal, "the terminal result was never published");
            Check.Equal(SessionStatus.Faulted, session.Status, "the session stopped");
        });
    }

    // -- A: what the window does *not* allow ---------------------------------------------------------------------

    private static void StableBoundaryChecks()
    {
        Check.Run("R-CORE clearing a required reference after construction is a type error", () =>
        {
            var session = NewSession(out var fixture);
            session.Start();
            var before = session.State.StateHash();
            Check.Fault(EngineFaultCode.TypeError,
                () => session.Submit(Action(session, Pkg + "Clear"), Fixtures.White, []),
                "clearing a constructed required reference");

            Check.Equal(SessionStatus.Faulted, session.Status, "the session stopped");
            Check.Equal(before, session.State.StateHash(), "the rejected write was rolled back");
        });

        Check.Run("R-CORE an explicitly optional reference accepts none at any time", () =>
        {
            var session = NewSession(out var fixture);
            session.Start();
            session.Submit(Action(session, Pkg + "Detach"), Fixtures.White, []);

            Check.Equal(SessionStatus.Running, session.Status, "an optional reference may be cleared");
            var node = Members(session, fixture.Nodes).First();
            Check.Equal(ValueKind.None,
                session.State.GetField(node, FieldIndex(session, fixture, "Peer")).Kind, "the optional field is none");
            Check.Equal(0, session.State.UnderConstruction.Count, "optional fields are no obligation");
        });
    }

    // -- A: direct store semantics -------------------------------------------------------------------------------

    private static void StorageChecks()
    {
        Check.Run("R-CORE direct store refuses to hash or publish an open construction", () =>
        {
            var rules = Rules();
            var state = new SessionState(rules);
            var type = rules.TypeByQualifiedId(Pkg + "Node");
            var next = type.FieldIndex("Next");
            var label = type.FieldIndex("Label");

            var open = state.CreateInstance(type.Id, state.RootScope);
            Check.Sequence([open], state.UnderConstruction, "the new instance is under construction");
            Check.Fault(EngineFaultCode.RequiredRefUnassigned,
                () => state.RequireStableState("a check"), "stable-state guard");
            Check.Fault(EngineFaultCode.RequiredRefUnassigned, () => state.StateHash(), "state hash");
            Check.Equal(ValueKind.None, state.GetField(open, next).Kind, "none is storable while constructing");

            state.SetField(open, next, Value.OfRef(open));
            Check.Equal(0, state.UnderConstruction.Count, "assigning the last required reference closes the window");
            state.RequireStableState("a check");
            Check.True(state.StateHash().Length > 0, "the closed construction hashes");

            Check.Fault(EngineFaultCode.TypeError, () => state.SetField(open, next, Value.None),
                "clearing closed a construction");
            Check.Fault(EngineFaultCode.TypeError,
                () => state.SetField(open, next, Value.OfRef(InstanceId.None)),
                "the explicit none reference is the same obligation");

            // A second instance stays open while the first one is closed: the window is per instance.
            var second = state.CreateInstance(type.Id, state.RootScope);
            state.SetField(second, label, Value.Of("second"));
            Check.Sequence([second], state.UnderConstruction, "the window follows each instance");
            state.SetField(second, next, Value.OfRef(open));
            Check.Equal(0, state.UnderConstruction.Count, "both instances are constructed");
        });

        Check.Run("R-CORE rollback and destroy leave no open construction behind", () =>
        {
            var rules = Rules();
            var state = new SessionState(rules);
            var type = rules.TypeByQualifiedId(Pkg + "Node");
            var label = type.FieldIndex("Label");

            var before = state.InstanceCount;
            Check.Fault(EngineFaultCode.ValueOutOfRange, () => state.RunAtomic(() =>
            {
                var created = state.CreateInstance(type.Id, state.RootScope);
                state.SetField(created, label, Value.Of(new string('x', 40)));
            }), "atomic rollback of an open construction");
            Check.Equal(before, state.InstanceCount, "the instance was rolled back");
            Check.Equal(0, state.UnderConstruction.Count, "the derived window was rolled back too");
            state.RequireStableState("a check");

            var doomed = state.CreateInstance(type.Id, state.RootScope);
            Check.Equal(1, state.UnderConstruction.Count, "the instance is open");
            state.DestroyInstance(doomed);
            Check.Equal(0, state.UnderConstruction.Count, "destroying the instance ends its obligation");
            state.RequireStableState("a check");
        });
    }

    // -- B: execution-only gameplay fields -----------------------------------------------------------------------

    private static void LifetimeChecks()
    {
        Check.Run("R-CORE an execution-only entity gameplay field is rejected", () =>
        {
            var plain = new EnginePackageBuilder("legacy-field");
            plain.EntityType("Thing").AddField("Temp", FieldTypeSchema.Int(0, 9),
                lifetime: FieldLifetime.ExecutionOnly);
            Check.Fault(EngineFaultCode.Unsupported, () => Fixtures.Link(plain), "execution-only gameplay field");

            // The merged-field path of an explicit extension must be covered as well, not only the first declaration.
            var origin = new EnginePackageBuilder("legacy-base");
            var extensible = origin.EntityType("Shared");
            extensible.AddField("Value", FieldTypeSchema.Int(0, 9));
            extensible.ExtensibleFields = true;
            var extender = new EnginePackageBuilder("legacy-extender");
            extender.Extends.Add("legacy-base:Shared");
            extender.EntityType("legacy-base:Shared").AddField("Scratch", FieldTypeSchema.Int(0, 9),
                lifetime: FieldLifetime.ExecutionOnly);
            Check.Fault(EngineFaultCode.Unsupported, () => Fixtures.Link(origin, extender),
                "execution-only field in an extension");

            // Positive control: the same field declared Persistent links, so the check discriminates the lifetime.
            var fine = new EnginePackageBuilder("persistent-field");
            fine.EntityType("Thing").AddField("Value", FieldTypeSchema.Int(0, 9));
            Check.True(Fixtures.Link(fine).Types.Count == 1, "a persistent gameplay field still links");
        });

        Check.Run("R-CORE frame slots and payloads stay execution-only and keep linking", () =>
        {
            // The ref-lifecycle package itself is the counterexample: its procedures use execution-only parameters,
            // locals and bindings, and they must not be confused with entity gameplay fields.
            var rules = Rules();
            var seed = rules.Procedures.Single(procedure => procedure.QualifiedId == Pkg + "Seed");
            Check.True(seed.Locals.Count > 0, "the seed procedure declares locals");
            Check.True(seed.Locals.All(local => local.Lifetime == FieldLifetime.ExecutionOnly),
                "locals stay execution-only");

            var orphan = rules.Actions.Single(action => action.QualifiedId == Pkg + "Orphan");
            Check.True(orphan.Program.Locals.All(local => local.Lifetime == FieldLifetime.ExecutionOnly),
                "action program locals stay execution-only");

            var both = Fixtures.BuildBoth();
            Check.True(both.Types.SelectMany(type => type.Fields).All(field =>
                    field.Lifetime == FieldLifetime.Persistent),
                "every entity gameplay field of the shipped fixtures is Persistent");
            var moved = both.Events.Single(eventType => eventType.QualifiedId == "grid-wargame:Moved");
            Check.True(moved.Payload.All(field => field.Lifetime == FieldLifetime.ExecutionOnly),
                "event payloads stay execution-only");
            var victory = both.TerminalResults.Single(terminal => terminal.QualifiedId == "grid-wargame:Victory");
            Check.True(victory.Fields.All(field => field.Lifetime == FieldLifetime.ExecutionOnly),
                "terminal result fields stay execution-only");
            var score = both.ComputeAssets.Single(asset => asset.QualifiedId == "micro-board:Score");
            Check.True(score.Inputs.Concat(score.Outputs).All(field => field.Lifetime == FieldLifetime.ExecutionOnly),
                "compute inputs and outputs stay execution-only");
        });
    }

    // -- fixture -------------------------------------------------------------------------------------------------

    private const string MicroView = "micro-board:PlayerView";

    private sealed record RefFixture(
        EntityTypeSymbol Node,
        FieldSymbol Label,
        FieldSymbol Next,
        FieldSymbol Peer,
        CollectionSymbol Nodes,
        TerminalResultSymbol Done);

    /// <summary>
    /// The construction-window fixture. <c>Seed</c> completes one node inside the create instruction and one node
    /// with a later instruction of the same command; the actions each open a construction and then either close it,
    /// or reach a stable boundary with it still open.
    /// </summary>
    private static EnginePackageBuilder Package(out RefFixture fixture)
    {
        var package = new EnginePackageBuilder("ref-lifecycle", "1.0.0");

        var zone = package.ScopeType("Zone");
        var node = package.EntityType("Node");
        var label = node.AddField("Label", FieldTypeSchema.String(16));
        var next = node.AddField("Next", FieldTypeSchema.RefTo(node));
        var peer = node.AddField("Peer", FieldTypeSchema.RefTo(node), optional: true);
        var nodes = package.Collection("Nodes", node);
        var done = package.TerminalResult("Done");
        // The construction-window checks observe through this projection, so it must exist and match the viewer role.
        var view = package.Projection("View");
        view.ParticipantRole = "white";
        view.Type(node);
        fixture = new RefFixture(node, label, next, peer, nodes, done);

        var seed = package.Procedure("Seed");
        var zoneSlot = seed.Local("zone", FieldTypeSchema.Binding());
        var first = seed.Local("first", FieldTypeSchema.Binding());
        var second = seed.Local("second", FieldTypeSchema.Binding());
        seed.Body(
            seed.CreateScope(zone, zoneSlot),
            seed.CreateEntity(node, seed.LocalRef(zoneSlot), first, [F(label, seed.Const("first"))], nodes),
            seed.CreateEntity(node, seed.LocalRef(zoneSlot), second, [F(label, seed.Const("second"))], nodes),
            seed.SetField(seed.LocalRef(second), next, seed.LocalRef(first)),
            seed.SetField(seed.LocalRef(first), next, seed.LocalRef(second)));
        package.OnSessionStart(seed.Symbol);

        var orphan = package.Action("Orphan");
        var orphanBody = package.ActionBody(orphan);
        var orphanSlot = orphanBody.Local("node", FieldTypeSchema.Binding());
        orphanBody.Role("white");
        orphanBody.Body(orphanBody.CreateEntity(node, new FrameScopeExpr(), orphanSlot,
            [F(label, orphanBody.Const("orphan"))], nodes));

        var finish = package.Action("Finish");
        var finishBody = package.ActionBody(finish);
        var finishSlot = finishBody.Local("node", FieldTypeSchema.Binding());
        finishBody.Role("white");
        finishBody.Body(
            finishBody.CreateEntity(node, new FrameScopeExpr(), finishSlot,
                [F(label, finishBody.Const("finish"))], nodes),
            finishBody.SetField(finishBody.LocalRef(finishSlot), next, finishBody.LocalRef(finishSlot)));

        var suspend = package.Action("Suspend");
        var suspendBody = package.ActionBody(suspend);
        var suspendSlot = suspendBody.Local("node", FieldTypeSchema.Binding());
        var chosen = suspendBody.Local("chosen", FieldTypeSchema.Binding());
        suspendBody.Role("white");
        suspendBody.Body(
            suspendBody.CreateEntity(node, new FrameScopeExpr(), suspendSlot,
                [F(label, suspendBody.Const("suspend"))], nodes),
            suspendBody.Decide(suspendBody.Const(Value.OfParticipant(Fixtures.White)),
                DecisionResponseKind.EntityRef, "pick a node", chosen, suspendBody.Collection(nodes),
                suspendBody.Const(1L), suspendBody.Const(1L)));

        var win = package.Action("Win");
        var winBody = package.ActionBody(win);
        var winSlot = winBody.Local("node", FieldTypeSchema.Binding());
        winBody.Role("white");
        winBody.Body(
            winBody.CreateEntity(node, new FrameScopeExpr(), winSlot, [F(label, winBody.Const("win"))], nodes),
            winBody.SetTerminal(done));

        var clear = package.Action("Clear");
        var clearBody = package.ActionBody(clear);
        var clearSlot = clearBody.Local("node", FieldTypeSchema.Binding());
        clearBody.Role("white");
        clearBody.Body(
            clearBody.SetLocal(clearSlot, clearBody.First(clearBody.Collection(nodes))),
            clearBody.SetField(clearBody.LocalRef(clearSlot), next, clearBody.Const(Value.None)));

        var detach = package.Action("Detach");
        var detachBody = package.ActionBody(detach);
        var detachSlot = detachBody.Local("node", FieldTypeSchema.Binding());
        detachBody.Role("white");
        detachBody.Body(
            detachBody.SetLocal(detachSlot, detachBody.First(detachBody.Collection(nodes))),
            detachBody.SetField(detachBody.LocalRef(detachSlot), peer, detachBody.Const(Value.None)));

        return package;
    }

    private static RuleSet Rules() => Fixtures.Link(Package(out _));

    private static Session NewSession(out RefFixture fixture)
    {
        var rules = Fixtures.Link(Package(out fixture));
        var session = new Session(rules, 7UL, Fixtures.Roles);
        return session;
    }

    private static (FieldSymbol, Expr) F(FieldSymbol field, Expr value) => (field, value);

    private static ActionDefinition Action(Session session, string qualifiedId) =>
        session.RuleSet.Actions.Single(action => action.QualifiedId == qualifiedId);

    private static IReadOnlyList<InstanceId> Members(Session session, CollectionSymbol collection) =>
        session.State.CollectionMembers(
            session.RuleSet.Collections.Single(definition => definition.QualifiedId == collection.QualifiedId).Id);

    private static InstanceId NextOf(Session session, RefFixture fixture, InstanceId node) =>
        session.State.GetField(node, FieldIndex(session, fixture, "Next")).AsRef();

    private static int FieldIndex(Session session, RefFixture fixture, string field) =>
        session.RuleSet.TypeByQualifiedId(fixture.Node.QualifiedId).FieldIndex(field);
}

/// <summary>Small helper: reads a field name out of a fault message so a check can assert the diagnosis, not the text.</summary>
internal static class FaultMessage
{
    public static IEnumerable<string> MissingNames(this string message) =>
        message.Split(' ', ',', ';', ':', '\'', '.', '(', ')')
            .Where(token => token.Length > 0)
            .ToArray();

    public static bool Mentions(this string message, string token) =>
        message.Contains(token, StringComparison.Ordinal);
}
