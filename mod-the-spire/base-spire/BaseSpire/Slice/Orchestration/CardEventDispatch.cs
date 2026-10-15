using BaseSpire.Slice.Effects;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Orchestration;

public sealed class CardEventDispatch(CardSchema schema, IReadOnlyList<CardDefinition> cards,
    Func<IrScope, Expr, CardEffectProgram, Inst[]> emit)
{
    public Inst[] Dispatch(IrScope scope, Expr card, string hook) =>
        [.. cards.Where(c => c.Normal.Hooks.ContainsKey(hook) || c.Upgraded.Hooks.ContainsKey(hook)).Select(c =>
            scope.If(scope.Equal(scope.Field(card, schema.Definition), scope.Const(c.Id)),
                scope.IfElse(scope.Field(card, schema.Upgraded),
                    emit(scope, card, c.Upgraded.Hooks.GetValueOrDefault(hook, CardEffectProgram.Empty)),
                    emit(scope, card, c.Normal.Hooks.GetValueOrDefault(hook, CardEffectProgram.Empty)))))];
}
