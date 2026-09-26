using CoreTheSpire;

namespace BaseSpire;

internal sealed class WellLaidPlansHook : IRuleHook
{
    public ContentId Id { get; } = ContentId.Parse("base-spire:well-laid-plans-hook");

    public void OnEvent(RuleHookContext context, GameEvent gameEvent) { }

    public EffectNode? BeforeFlushLate(RuleHookContext context, string playerId, bool shouldFlush)
    {
        if (!shouldFlush) return null;
        var amount = context.GetPowerAmount(playerId, BaseSpireIds.WellLaidPlansPower);
        if (amount <= 0) return null;
        return new ChooseCardsFromPileEffect(CardPile.Hand, "Choose cards to retain", 0, amount,
            CardSelectionFilter.NotRetained, new RetainSelectedCardEffect());
    }
}
