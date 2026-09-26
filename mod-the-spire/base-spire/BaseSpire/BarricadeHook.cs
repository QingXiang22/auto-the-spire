using CoreTheSpire;

namespace BaseSpire;

internal sealed class BarricadeHook : IRuleHook
{
    public ContentId Id { get; } = ContentId.Parse("base-spire:barricade-hook");

    public void OnEvent(RuleHookContext context, GameEvent gameEvent) { }

    public bool ShouldClearBlock(RuleHookContext context, string creatureId) =>
        context.GetPowerAmount(creatureId, BaseSpireIds.BarricadePower) == 0;
}
