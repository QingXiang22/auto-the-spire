using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Orchestration;

/// <summary>First supported card attachment. State belongs to the actual card, independently of its copies.</summary>
public sealed class CardEnchantments(CardDefinitions definitions)
{
    public const string SownId = "base-spire:sown";
    private CardSchema Schema => definitions.Schema;

    /// <summary>Source EnchantmentModel.CanEnchant for a permanent deck selection; Sown is not stackable.</summary>
    public Expr CanEnchantPermanent(IrScope s, Expr card)
    {
        Expr supportedKind = s.Const(false);
        foreach (var definition in definitions.Compiled.Where(c => c.Kind is "Attack" or "Skill" or "Power"))
            supportedKind = s.Or(supportedKind, s.Equal(s.Field(card, Schema.Definition), s.Const(definition.Id)));
        return s.And(supportedKind, s.And(s.Not(s.Field(card, Schema.Unplayable)),
            s.Equal(s.Field(card, Schema.Enchantment), s.Const(""))));
    }

    public Inst[] ApplySown(IrScope s, Expr card) =>
    [s.If(s.Not(CanEnchantPermanent(s, card)), s.Fail("Sown requires an enchantable permanent card without an existing attachment")),
     s.SetField(card, Schema.Enchantment, s.Const(SownId)),
     s.SetField(card, Schema.EnchantmentAmount, s.Const(1L)),
     s.SetField(card, Schema.EnchantmentDisabled, s.Const(false))];

    /// <summary>After card body, before play-finished listeners. Consume even when energy gain is blocked.</summary>
    public Inst[] AfterBody(CardPlayContext c, Expr living, Func<Expr, Inst[]> gainEnergy)
    {
        var s = c.Scope;
        return [s.If(s.And(living, s.And(s.Equal(s.Field(c.Card, Schema.Enchantment), s.Const(SownId)),
            s.Not(s.Field(c.Card, Schema.EnchantmentDisabled)))),
            [.. gainEnergy(s.Field(c.Card, Schema.EnchantmentAmount)),
             s.SetField(c.Card, Schema.EnchantmentDisabled, s.Const(true))])];
    }
}
