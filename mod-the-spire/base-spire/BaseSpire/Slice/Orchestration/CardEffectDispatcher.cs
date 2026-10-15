using BaseSpire.Slice.Effects;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Orchestration;

public delegate Inst[] EmitCardProgram(CardPlayContext context, CardEffectProgram program, bool unpowered);

/// <summary>Only normalized complete variants can enter the shared play pipeline.</summary>
public sealed class CardEffectDispatcher(CardSchema schema, IReadOnlyList<CardDefinition> cards,
    CollectionSymbol enemies, EmitCardProgram emit)
{
    public Inst[] PrepareTarget(CardPlayContext c) => [.. cards.Select(card => c.Scope.If(
        c.Scope.Equal(c.Scope.Field(c.Card, schema.Definition), c.Scope.Const(card.Id)),
        c.Scope.IfElse(c.Scope.Field(c.Card, schema.Upgraded), Select(c, card.Upgraded), Select(c, card.Normal))))];
    private Inst[] Select(CardPlayContext c, CardVariant variant) => variant.Program.NeedsSelectedTarget ? [c.SelectTarget(enemies)] : [];
    public Inst[] Body(CardPlayContext c) => [.. cards.Select(card => c.Scope.If(
        c.Scope.Equal(c.Scope.Field(c.Card, schema.Definition), c.Scope.Const(card.Id)),
        c.Scope.IfElse(c.Scope.Field(c.Card, schema.Upgraded), emit(c, card.Upgraded.Program, card.Upgraded.Unpowered),
            emit(c, card.Normal.Program, card.Normal.Unpowered))))];
}
