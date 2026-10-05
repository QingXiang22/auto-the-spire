using CoreTheSpire.Engine;

namespace CoreTheSpire.Engine.Tests;

/// <summary>
/// K-01/R-CORE counterexamples: every check here must be <b>rejected</b> at link time with a specific fault code, so
/// the check discriminates a real rule from an accidental pass. A positive fixture would not be evidence for these.
///
/// Covered: compute input/output coverage and the static input budget, projection owner field type, event and
/// terminal field ownership, decision result type versus response kind, and the operand types of
/// <c>entityScope</c> / <c>collectionIndex</c>.
/// </summary>
internal static class CounterexampleChecks
{
    public static void Run()
    {
        ComputeOutputCoverage();
        ComputeInputCoverage();
        ComputeInputBudget();
        ProjectionOwnerFieldType();
        EventAndTerminalFieldOwnership();
        DecisionResultType();
        ScopeAndIndexOperandTypes();
    }

    /// <summary>A compute call must bind every declared output.</summary>
    private static void ComputeOutputCoverage()
    {
        Check.Run("K-01 a compute call that leaves a declared output unbound is rejected", () =>
        {
            var package = new EnginePackageBuilder("counterexample");
            var asset = Asset(package, "Score", inputs: 1, outputs: 2);
            var action = package.Action("Score");
            var body = package.ActionBody(action);
            body.Role("player");
            body.Body(body.Compute(asset,
                new[] { (asset.Inputs[0], body.Const(1L)) },
                Array.Empty<(FieldSymbol Field, int Slot)>()));

            Check.Fault(EngineFaultCode.SchemaInvalid, () => Fixtures.Link(package), "unbound compute output");
        });
    }

    /// <summary>A compute input name must belong to the asset that is being called.</summary>
    private static void ComputeInputCoverage()
    {
        Check.Run("K-01 a compute input that the asset does not declare is rejected", () =>
        {
            var package = new EnginePackageBuilder("counterexample");
            var asset = Asset(package, "Score", inputs: 1, outputs: 1);
            var foreign = Asset(package, "Other", inputs: 1, outputs: 1);
            var slot = 0;
            var action = package.Action("Score");
            var body = package.ActionBody(action);
            var result = body.Local("result", FieldTypeSchema.Int(0, 999));
            body.Role("player");
            body.Body(body.Compute(asset,
                new[] { (foreign.Inputs[0], body.Const(1L)) },
                new[] { (asset.Outputs[0], result) }));
            _ = slot;

            Check.Fault(EngineFaultCode.ReferenceUnresolved, () => Fixtures.Link(package), "foreign compute input");
        });
    }

    /// <summary>The declared compute budgets are static limits, checked when the asset is declared.</summary>
    private static void ComputeInputBudget()
    {
        Check.Run("K-01 a compute asset that exceeds its declared input budget is rejected", () =>
        {
            var package = new EnginePackageBuilder("counterexample");
            var asset = Asset(package, "Score", inputs: 3, outputs: 1);
            asset.Limits = new ComputeLimits(MaxInputValues: 2);

            Check.Fault(EngineFaultCode.ComputeLimitExceeded, () => Fixtures.Link(package), "input budget");
        });
    }

    /// <summary>An owner field must be a participant field: it decides owner-private visibility.</summary>
    private static void ProjectionOwnerFieldType()
    {
        Check.Run("K-01 a projection whose owner field is not a participant is rejected", () =>
        {
            var package = new EnginePackageBuilder("counterexample");
            var thing = package.EntityType("Thing");
            thing.OwnerFieldName = "Owner";
            thing.AddField("Owner", FieldTypeSchema.Int(0, 7));
            var view = package.Projection("View");
            view.ParticipantRole = "player";
            view.Type(thing);

            Check.Fault(EngineFaultCode.TypeError, () => Fixtures.Link(package), "projection owner type");
        });
    }

    /// <summary>Payload and terminal field references must belong to the declaration the instruction names.</summary>
    private static void EventAndTerminalFieldOwnership()
    {
        Check.Run("K-01 an emitted payload or terminal field from another declaration is rejected", () =>
        {
            var events = new EnginePackageBuilder("counterexample");
            var first = events.Event("First");
            first.AddPayload("Item", FieldTypeSchema.Int(0, 7));
            var second = events.Event("Second");
            var foreign = second.AddPayload("Item", FieldTypeSchema.Int(0, 7));
            var procedure = events.Procedure("Emit");
            procedure.Body(procedure.Emit(first, (foreign, procedure.Const(1L))));
            Check.Fault(EngineFaultCode.TypeError, () => Fixtures.Link(events), "foreign event payload field");

            var terminals = new EnginePackageBuilder("counterexample");
            var victory = terminals.TerminalResult("Victory");
            victory.AddField("Turn", FieldTypeSchema.Int(0, 7));
            var defeat = terminals.TerminalResult("Defeat");
            var foreignField = defeat.AddField("Turn", FieldTypeSchema.Int(0, 7));
            var setter = terminals.Procedure("End");
            setter.Body(setter.SetTerminal(victory, (foreignField, setter.Const(1L))));
            Check.Fault(EngineFaultCode.TypeError, () => Fixtures.Link(terminals), "foreign terminal field");
        });
    }

    /// <summary>A decision's response kind must produce a value the result slot can hold.</summary>
    private static void DecisionResultType()
    {
        Check.Run("K-01 a decision result that cannot fit its slot is rejected", () =>
        {
            var package = new EnginePackageBuilder("counterexample");
            var thing = package.EntityType("Thing");
            var size = thing.AddField("Size", FieldTypeSchema.Int(0, 7));
            var items = package.Collection("Items", thing);
            var decision = package.Procedure("Ask");
            var numericSlot = decision.Local("answer", FieldTypeSchema.Int(0, 7));
            decision.Body(decision.Decide(decision.Const(Value.OfParticipant(new ParticipantId(1))),
                DecisionResponseKind.EntityRef, "pick one", numericSlot, decision.Collection(items),
                decision.Const(1L), decision.Const(1L)));
            _ = size;

            Check.Fault(EngineFaultCode.TypeError, () => Fixtures.Link(package), "decision result into an int slot");
        });
    }

    /// <summary>Scope and position expressions take an entity reference and a collection member respectively.</summary>
    private static void ScopeAndIndexOperandTypes()
    {
        Check.Run("K-01 entityScope and collectionIndex reject operands of the wrong kind", () =>
        {
            var scopes = new EnginePackageBuilder("counterexample");
            var thing = scopes.EntityType("Thing");
            thing.AddField("Size", FieldTypeSchema.Int(0, 7));
            var scopeProcedure = scopes.Procedure("Scope");
            var scopeSlot = scopeProcedure.Local("scope", FieldTypeSchema.Binding());
            scopeProcedure.Body(scopeProcedure.SetLocal(scopeSlot, scopeProcedure.EntityScope(scopeProcedure.Const(1L))));
            Check.Fault(EngineFaultCode.TypeError, () => Fixtures.Link(scopes), "entityScope of an int");

            var indices = new EnginePackageBuilder("counterexample");
            var card = indices.EntityType("Card");
            card.AddField("Value", FieldTypeSchema.Int(0, 7));
            var pile = indices.Collection("Pile", card);
            var indexProcedure = indices.Procedure("Index");
            var indexSlot = indexProcedure.Local("position", FieldTypeSchema.Int(0, 7));
            indexProcedure.Body(indexProcedure.SetLocal(indexSlot,
                indexProcedure.CollectionIndex(pile, indexProcedure.Const("not a reference"))));
            Check.Fault(EngineFaultCode.TypeError, () => Fixtures.Link(indices), "collectionIndex of a string");
        });
    }

    /// <summary>A minimal, linkable compute asset with the requested number of inputs and outputs.</summary>
    private static ComputeAssetSymbol Asset(EnginePackageBuilder package, string name, int inputs, int outputs)
    {
        var asset = package.ComputeAsset(name);
        asset.BackendId = "test";
        asset.SourcePath = "fixtures/" + name + ".lua";
        asset.SourceHash = "0000000000000000";
        asset.Entry = "score";
        for (var index = 0; index < inputs; index++)
            asset.Input(name + "In" + index, FieldTypeSchema.Int(0, 99));
        for (var index = 0; index < outputs; index++)
            asset.Output(name + "Out" + index, FieldTypeSchema.Int(0, 999));
        return asset;
    }
}
