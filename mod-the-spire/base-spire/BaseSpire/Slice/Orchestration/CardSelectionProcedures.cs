using BaseSpire.Slice.Effects;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Orchestration;

/// <summary>Selection only binds instances and resumes a caller-supplied effect program.</summary>
public sealed class CardSelectionProcedures(CardSchema schema, CardPileSymbols piles,
    Func<IrScope, Expr, Expr> isAttack, Func<IrScope, Expr, Expr> canUpgrade)
{
    public Func<IrScope, Expr, Expr> IsNonSlySkill { get; init; } = (_, _) => throw new InvalidOperationException("Sly skill filter binding missing");
    public Inst[] Emit(IrScope scope, SelectCardsEffect effect, Func<Expr, Inst[]> apply)
    {
        var candidates = scope.Collection(piles[effect.From]);
        Query eligible = effect.Filter switch
        {
            CardEffectCardFilter.Any => candidates,
            CardEffectCardFilter.Attack => scope.Where(candidates, card => isAttack(scope, card)),
            CardEffectCardFilter.NonAttack => scope.Where(candidates, card => scope.Not(isAttack(scope, card))),
            CardEffectCardFilter.Upgradeable => scope.Where(candidates, card => canUpgrade(scope, card)),
            CardEffectCardFilter.NonSlySkill => scope.Where(candidates, card => IsNonSlySkill(scope, card)),
            _ => throw new ArgumentOutOfRangeException(nameof(effect)),
        };
        var count = scope.Local("selectionCount", FieldTypeSchema.Int(0, 9999));
        var selected = scope.Local("selectedCards", effect.Count == 1
            ? FieldTypeSchema.RefTo(schema.Type) : FieldTypeSchema.ListOf(FieldTypeSchema.RefTo(schema.Type)));
        var answer = scope.Decide(scope.Const(Value.OfParticipant(SliceManifest.Player)),
            effect.Count == 1 ? DecisionResponseKind.EntityRef : DecisionResponseKind.EntityRefList,
            effect.Prompt, selected, eligible,
            effect.MinimumCount is { } minimum ? scope.Minimum(scope.Const((long)minimum), scope.LocalRef(count)) : scope.LocalRef(count), scope.LocalRef(count));
        Inst[] confirmed = effect.Count == 1
            ? apply(scope.LocalRef(selected))
            : [scope.ForEach(new RefListQuery(selected), apply)];
        var requested = scope.Const((long)effect.Count);
        return [scope.SetLocal(count, scope.Minimum(requested, scope.Count(eligible))),
            scope.If(scope.Greater(scope.LocalRef(count), scope.Const(0L)),
                scope.IfElse(scope.And(scope.Const(effect.AutoSelect),
                    scope.Not(scope.Greater(scope.Count(eligible), requested))),
                    [scope.ForEach(eligible, apply)],
                    [answer, .. confirmed]))];
    }
}
