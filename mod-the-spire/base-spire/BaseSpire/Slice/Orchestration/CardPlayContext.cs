using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Orchestration;

/// <summary>One emitted execution frame. Slots are Engine locals, never mutable C# gameplay state.</summary>
public sealed record CardPlayContext(IrScope Scope, Expr Card, Expr Owner, Expr Auto, Expr ForceExhaust,
    int Target, int PaidX, int DiscardChoice, int DiscardChoices, int Created)
{
    public int PaidStarX { get; init; }
    public static CardPlayContext Create(IrScope scope, Expr card, Expr owner, Expr auto, Expr forceExhaust,
        EntityTypeSymbol cardType) => new(scope, card, owner, auto, forceExhaust,
            scope.Local("target", FieldTypeSchema.Binding()), scope.Local("paidX", FieldTypeSchema.Int(0, 99)),
            scope.Local("discardChoice", FieldTypeSchema.Binding()),
            scope.Local("discardChoices", FieldTypeSchema.ListOf(FieldTypeSchema.RefTo(cardType))),
            scope.Local("created", FieldTypeSchema.Binding()))
        { PaidStarX = scope.Local("paidStarX", FieldTypeSchema.Int(0, 99999)) };

    public Inst SelectTarget(CollectionSymbol enemies) => Scope.IfElse(Auto,
        [Scope.SamplePick(SliceManifest.RandomTargetStream, Scope.Sort(Scope.Collection(enemies), item => item), Target)],
        [Scope.Decide(Scope.Const(Value.OfParticipant(SliceManifest.Player)), DecisionResponseKind.EntityRef,
            "Choose a target", Target, Scope.Collection(enemies), Scope.Const(1L), Scope.Const(1L))]);
}
