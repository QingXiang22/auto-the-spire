using BaseSpire.Slice.Effects;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Orchestration;

/// <summary>Result pile is committed once, after all effects/hooks, through the shared exhaust command.</summary>
public sealed class CardResultRules(CardSchema schema, EnumTypeSymbol kind, SliceAuthorModel? source,
    IReadOnlyList<CardDefinition> definitions, CardPileProcedures piles, Func<IrScope, Expr, string, Expr> holds)
{
    public Inst[] Emit(CardPlayContext c)
    {
        var s = c.Scope;
        Expr exhaust = c.ForceExhaust;
        foreach (var card in definitions)
        {
            Expr own = s.And(s.Equal(s.Field(c.Card, schema.Definition), s.Const(card.Id)),
                s.Or(s.And(s.Field(c.Card, schema.Upgraded), s.Const(card.Upgraded.Result == CardResultPolicy.Exhaust)),
                    s.And(s.Not(s.Field(c.Card, schema.Upgraded)), s.Const(card.Normal.Result == CardResultPolicy.Exhaust))));
            foreach (var power in source!.Powers.Where(p => p.CardRuleResultPile == "exhaust" && p.CardRuleType == card.Kind))
                own = s.Or(own, s.And(s.Equal(s.Field(c.Card, schema.Definition), s.Const(card.Id)), holds(s, c.Owner, power.Id)));
            exhaust = s.Or(exhaust, own);
        }
        // Native CardModel.GetResultPileTypeForCardPlay checks Power before ExhaustOnNextPlay.
        // Keep the entity for play history/long-term references; remove only its combat pile membership.
        return [s.IfElse(s.Equal(s.Field(c.Card, schema.Kind), s.EnumConst(kind, "Power")),
            piles.RemoveFromCombat(s, CardEffectPile.InPlay, c.Card),
            [s.IfElse(exhaust, piles.Move(s, CardEffectPile.InPlay, CardEffectPile.Exhaust, c.Card),
                piles.Move(s, CardEffectPile.InPlay, CardEffectPile.Discard, c.Card))])];
    }
}
