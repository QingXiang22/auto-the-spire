using CoreTheSpire.Engine;

namespace Differential;

// These are representation differences, not empty gameplay overrides. Each
// classification checks the compiled body or the actual effect-free invocation.
internal sealed class NormalDslEventExclusions(RuleSet rules)
{
    private static readonly HashSet<string> Registration = ["base-spire:RegisterPowerDefinitions", "base-spire:RegisterEnemyMoveGraphs",
        "base-spire:RegisterEnemyMoveNodes", "base-spire:RegisterEnemyMoveBranches", "base-spire:RegisterCardDefinitions"];
    private static readonly HashSet<string> Definitions = ["base-spire:PowerBlueprint", "base-spire:CardBlueprint", "base-spire:EnemyMoveGraph",
        "base-spire:EnemyMoveNode", "base-spire:EnemyMoveBranch"];
    private static readonly HashSet<string> CardFields = ["Cost", "BaseCost", "BaseDamage", "Kind", "Rarity", "SourceEntry", "StrikeTag", "IsX",
        "Targeted", "Exhausts", "LeavesPlay", "CanUpgrade"];
    private readonly Dictionary<int, bool> definitionBodies = [];
    private readonly Dictionary<int, bool> cardBodies = [];
    private readonly Dictionary<int, bool> guards = [];

    internal string? Reaction(ProcedureDefinition procedure, string[] eventTypes, bool noEffects)
    {
        if (eventTypes.Any(Registration.Contains))
            return DefinitionBody(procedure) ? "rule-materialization: native ModelDb initialization" : null;
        if (eventTypes.Contains("base-spire:CardCreated"))
            return CardBody(procedure) ? "card-materialization: only this card's intrinsic metadata fields" : null;
        if (procedure.IsPure && noEffects && eventTypes.All(e => e is "base-spire:ModifyCardKeyword" or "base-spire:CardHasTurnEndInHandEffect"))
            return "card-property-projection: native intrinsic keywords/hand-end eligibility; raw inputs and results retained";
        if (!procedure.IsPure && noEffects && DefinitionGuard(procedure))
            return "inactive-lifecycle-adapter: definition/instance filtering executed without state writes, events, RNG or decisions";
        if (!procedure.IsPure && noEffects && OwnedTeardown(procedure))
            return "inactive-owned-state-teardown: no state write or entity destruction occurred";
        if (noEffects && procedure.QualifiedId is "base-spire:CopyCardAttachments" or "base-spire:RemoveCardCosts" or "base-spire:RemoveCardAttachments")
            return "empty-metadata-bookkeeping: no copied/removed metadata, source construction/teardown has no notification";
        return null;
    }

    private static bool OwnedTeardown(ProcedureDefinition procedure)
    {
        var instructions = Nodes(procedure.Body).OfType<Inst>().ToArray();
        return instructions.Any(i => i is ForEachInst) && instructions.Any(i => i is DestroyEntityInst)
            && instructions.All(i => i is ForEachInst or DestroyEntityInst or IfInst or SetLocalInst or ReturnInst);
    }

    internal static string? Event(string name) => Registration.Contains(name) ? "rule-materialization: native ModelDb initialization" : name switch
    {
        "base-spire:CardCreated" => "card-materialization: intrinsic constructor fields; each actual initializer body is audited",
        "base-spire:CardCombatCopied" => "card-materialization: native combat clone; attachment reactions are audited separately",
        "base-spire:CardRemoving" or "base-spire:EnemyRemoving" => "object-teardown: native synchronous combat disposal; actual removal reactions are audited separately",
        "base-spire:CardMoved" => "pile-bookkeeping: native synchronous pile transfer; resulting piles and actual reactions are checked separately",
        "base-spire:BeforeCardPlayed" => "notification-adapter: native BeforeCardPlayed is represented by BeforeCardExecution with its complete play record",
        "base-spire:AfterCardDrawnEarly" => "notification-adapter: native AfterCardDrawn early pass; actual reactions are audited separately from the draw observation",
        "base-spire:ExecuteCardEnchantment" or "base-spire:ExecuteCardAffliction" => "intrinsic-card-stage: native CardModel.Play executes attachments internally; any actual DSL effect remains an audited reaction",
        "base-spire:BeforePowerAmountChanged" => "default-notification: BeforePowerAmountChanged; actual reactions are audited separately",
        "base-spire:PowerAmountReceiving" => "default-notification: ModifyPowerAmountReceived; actual reactions are audited separately",
        "base-spire:AfterModifyingPowerAmountReceived" => "default-notification: AfterModifyingPowerAmountReceived; actual reactions are audited separately",
        "base-spire:PowerCreated" => "power-materialization: native PowerModel instance fields; BeforeApplied/AfterApplied/amount notifications are compared",
        "base-spire:PowerApplied" => "notification-adapter: native apply completion is represented by AfterPowerApplied and PowerAmountChanged",
        "base-spire:AfterCombatEnd" => "notification-adapter: native AfterCombatEnd is represented by CombatEnded; actual reactions are audited separately",
        "base-spire:CombatVictoryEarly" => "notification-adapter: native AfterCombatVictory early pass; actual reactions are audited separately",
        _ => null
    };
    private bool DefinitionBody(ProcedureDefinition procedure)
    {
        if (definitionBodies.TryGetValue(procedure.Id, out var found)) return found;
        // A recursive builder is not a static definition projection.
        definitionBodies[procedure.Id] = false;
        var valid = Nodes(procedure.Body).OfType<Inst>().All(i => i switch
        {
            IfInst or SetLocalInst or ForEachInst or ReturnInst or FailInst => true,
            CreateEntityInst c => Definitions.Contains(rules.ResolveType(procedure.RefBase, c.TypeRef).Type.QualifiedId),
            SetFieldInst s => DefinitionField(rules.ResolveField(procedure.RefBase, s.FieldRef)),
            CallInst c => DefinitionBody(rules.ResolveProcedure(procedure.RefBase, c.ProcedureRef).Procedure),
            _ => false
        });
        return definitionBodies[procedure.Id] = valid;
    }
    private bool DefinitionField(FieldResolution field) => field.OwnerKind == FieldOwnerKind.Entity
        && Definitions.Contains(rules.Types.Single(t => t.Id == field.OwnerId).QualifiedId);

    private bool CardBody(ProcedureDefinition procedure)
    {
        if (cardBodies.TryGetValue(procedure.Id, out var found)) return found;
        var slot = procedure.Parameters.Select(p => p.Name).ToList().IndexOf("card");
        var valid = slot >= 0 && Nodes(procedure.Body).OfType<Inst>().All(i => i switch
        {
            IfInst or ReturnInst => true,
            SetFieldInst s => (s.Target is LocalExpr local && local.Slot == slot || s.Target is ParamExpr param && param.Slot == slot)
                && CardField(rules.ResolveField(procedure.RefBase, s.FieldRef)),
            _ => false
        });
        return cardBodies[procedure.Id] = valid;
    }
    private bool CardField(FieldResolution field) => field.OwnerKind == FieldOwnerKind.Entity
        && field.OwnerId == rules.TypeByQualifiedId("base-spire:Card").Id && CardFields.Contains(field.Field.Name);

    private bool DefinitionGuard(ProcedureDefinition procedure)
    {
        if (guards.TryGetValue(procedure.Id, out var found)) return found;
        bool Definition(Expr field, Expr constant) => field is FieldExpr f
            && rules.ResolveField(procedure.RefBase, f.FieldRef).Field.Name == "DefinitionId"
            && constant is ConstExpr { Value.Kind: ValueKind.String } c && c.Value.AsString().StartsWith("base-spire:", StringComparison.Ordinal);
        return guards[procedure.Id] = Nodes(procedure.Body).OfType<CompareExpr>()
            .Any(c => c.Op is CompareOp.Equal or CompareOp.NotEqual && (Definition(c.Left, c.Right) || Definition(c.Right, c.Left)));
    }
    private static IEnumerable<IrNode> Nodes(IrNode root)
    {
        yield return root;
        foreach (var child in root.Children) foreach (var node in Nodes(child)) yield return node;
    }
}
