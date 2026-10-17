using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Orchestration;

/// <summary>Cost legality, X snapshot, payment and one-shot rule consumption; no card effects or piles.</summary>
public sealed class CardPaymentRules(CardSchema schema, EnumTypeSymbol kinds, FieldSymbol energy,
    SliceAuthorModel? source, Func<IrScope, Expr, string, Expr> holds,
    IReadOnlyList<BaseSpire.Slice.Effects.CardDefinition> definitions)
{
    public FieldSymbol? Stars { get; init; }
    private Expr StarCostAllowed(IrScope scope, Expr card, Expr owner)
    {
        Expr allowed = scope.Const(true);
        foreach (var definition in definitions.Where(item => item.Normal.StarCost > 0 || item.Upgraded.StarCost > 0))
            allowed = scope.And(allowed, scope.Or(scope.Not(scope.Equal(scope.Field(card, schema.Definition), scope.Const(definition.Id))),
                scope.Or(scope.And(scope.Field(card, schema.Upgraded), scope.GreaterOrEqual(scope.Field(owner, Stars!), scope.Const((long)definition.Upgraded.StarCost))),
                    scope.And(scope.Not(scope.Field(card, schema.Upgraded)), scope.GreaterOrEqual(scope.Field(owner, Stars!), scope.Const((long)definition.Normal.StarCost))))));
        return allowed;
    }
    private IReadOnlyList<AuthorPower> CostRules => source?.Powers.Where(p => p.CardRuleType.Length > 0 && p.CardRuleCost >= 0).ToArray() ?? [];
    private IReadOnlyList<AuthorPower> ConsumeRules => source?.Powers.Where(p => p.CardRuleType.Length > 0 && p.CardRuleConsumeOnPlay).ToArray() ?? [];

    private Expr IsX(IrScope scope, Expr card) => definitions
        .Aggregate((Expr)scope.Const(false), (test, c) => scope.Or(test,
            scope.And(scope.Equal(scope.Field(card, schema.Definition), scope.Const(c.Id)),
                scope.Or(scope.And(scope.Field(card, schema.Upgraded), scope.Const(c.Upgraded.IsXCost)),
                    scope.And(scope.Not(scope.Field(card, schema.Upgraded)), scope.Const(c.Normal.IsXCost))))));

    private Expr BaseCost(IrScope scope, Expr card) => scope.Maximum(scope.Const(0L),
        scope.Add(scope.Field(card, schema.Cost), scope.Field(card, schema.CostModifier)));

    private Expr Matches(IrScope scope, Expr card, Expr owner, AuthorPower power) => scope.And(holds(scope, owner, power.Id),
        scope.Equal(scope.Field(card, schema.Kind), scope.EnumConst(kinds, power.CardRuleType)));

    public Expr CanPay(IrScope scope, Expr card, Expr owner)
    {
        Expr allowed = scope.Or(IsX(scope, card), scope.GreaterOrEqual(scope.Field(owner, energy), BaseCost(scope, card)));
        foreach (var rule in CostRules)
            allowed = scope.Or(allowed, scope.And(Matches(scope, card, owner, rule),
                scope.GreaterOrEqual(scope.Field(owner, energy), scope.Const((long)rule.CardRuleCost))));
        return Stars is null ? allowed : scope.And(allowed, StarCostAllowed(scope, card, owner));
    }

    public Inst[] Pay(CardPlayContext c)
    {
        var s = c.Scope;
        var paid = s.Local("paidCost", FieldTypeSchema.Int(0, 99));
        return [s.SetLocal(c.PaidX, s.Const(0L)), s.SetLocal(c.PaidStarX, s.Const(0L)), s.IfElse(IsX(s, c.Card),
            [s.SetLocal(c.PaidX, s.Field(c.Owner, energy)),
                s.SetField(c.Card, schema.Amount, s.LocalRef(c.PaidX)),
                s.If(s.Not(c.Auto), s.SetField(c.Owner, energy, s.Const(0L)))],
            [s.If(s.Not(c.Auto), [s.SetLocal(paid, BaseCost(s, c.Card)),
                .. CostRules.Select(rule => s.If(Matches(s, c.Card, c.Owner, rule),
                    s.SetLocal(paid, s.Minimum(s.LocalRef(paid), s.Const((long)rule.CardRuleCost))))),
                s.SetField(c.Owner, energy, s.Subtract(s.Field(c.Owner, energy), s.LocalRef(paid)))])]),
            .. Stars is null ? Array.Empty<Inst>() : definitions.Where(item => item.Normal.IsStarXCost || item.Upgraded.IsStarXCost)
                .Select(item => s.If(s.And(s.Equal(s.Field(c.Card, schema.Definition), s.Const(item.Id)),
                    s.Or(s.And(s.Field(c.Card, schema.Upgraded), s.Const(item.Upgraded.IsStarXCost)),
                        s.And(s.Not(s.Field(c.Card, schema.Upgraded)), s.Const(item.Normal.IsStarXCost)))),
                    [s.SetLocal(c.PaidStarX, s.Field(c.Owner, Stars)), s.If(s.Not(c.Auto), s.SetField(c.Owner, Stars, s.Const(0L)))])).ToArray(),
            .. Stars is null ? Array.Empty<Inst>() : definitions.Where(item => item.Normal.StarCost > 0 || item.Upgraded.StarCost > 0)
                .Select(item => s.If(s.And(s.Not(c.Auto), s.Equal(s.Field(c.Card, schema.Definition), s.Const(item.Id))),
                    s.IfElse(s.Field(c.Card, schema.Upgraded),
                        [s.SetField(c.Owner, Stars, s.Maximum(s.Const(0L), s.Subtract(s.Field(c.Owner, Stars), s.Const((long)item.Upgraded.StarCost))))],
                        [s.SetField(c.Owner, Stars, s.Maximum(s.Const(0L), s.Subtract(s.Field(c.Owner, Stars), s.Const((long)item.Normal.StarCost))))]))).ToArray()];
    }

    public (Inst[] Capture, Inst[] Consume) OneShot(CardPlayContext c, Func<IrScope, Expr, string, Inst[]> remove)
    {
        var s = c.Scope;
        var pairs = ConsumeRules.Select(rule => (Rule: rule, Slot: s.Local("heldCardRule", FieldTypeSchema.Bool()))).ToArray();
        return ([.. pairs.Select(pair => s.SetLocal(pair.Slot, Matches(s, c.Card, c.Owner, pair.Rule)))],
            [.. pairs.Select(pair => s.If(s.LocalRef(pair.Slot), remove(s, c.Owner, pair.Rule.Id)))]);
    }
}
