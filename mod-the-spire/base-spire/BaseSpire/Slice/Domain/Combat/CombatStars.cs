using BaseSpire.Slice.Effects;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Domain.Combat;

public sealed class CombatStars(FieldSymbol stars, Func<Expr, Expr> active)
{
    public Func<IrScope, Expr, Expr, Expr> ShouldGain { get; init; } = (scope, _, _) => scope.Const(true);
    public Func<IrScope, Expr, Expr, Inst[]> AfterGained { get; init; } = (_, _, _) => [];

    public Inst[] Change(IrScope scope, Expr player, CardEffectOperation operation, Expr amount)
    {
        var captured = scope.Local("starsAmount", FieldTypeSchema.Int(-99999, 99999));
        var delta = scope.Local("starsDelta", FieldTypeSchema.Int(-99999, 99999));
        var value = scope.LocalRef(captured);
        var current = scope.Field(player, stars);
        Inst[] Gain(Expr gained) => [scope.If(ShouldGain(scope, player, gained),
            [scope.SetField(player, stars, scope.Add(scope.Field(player, stars), gained)), .. AfterGained(scope, player, gained)])];
        Inst[] change = operation switch
        {
            CardEffectOperation.GainStars => Gain(value),
            CardEffectOperation.LoseStars => [scope.SetField(player, stars, scope.Maximum(scope.Const(0L), scope.Subtract(current, value)))],
            CardEffectOperation.SetStars => [scope.SetLocal(delta, scope.Subtract(value, current)),
                scope.IfElse(scope.Greater(scope.LocalRef(delta), scope.Const(0L)), Gain(scope.LocalRef(delta)),
                    [scope.SetField(player, stars, value)])],
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };
        return [scope.If(active(player), [scope.SetLocal(captured, amount),
            scope.If(scope.Less(value, scope.Const(0L)), scope.Fail("Stars amount must not be negative")), .. change])];
    }
}
