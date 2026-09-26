using BaseSpire;
using CoreTheSpire;
using System.Text.Json;

namespace TrainingMod;

public sealed class TrainingModPackage : IRulePackage
{
    public PackageInfo Info { get; } = new("training-mod", "0.1.0");
    public PlayCounterHook Hook { get; } = new();

    public void Register(ContentRegistry registry)
    {
        registry.Override(new CardDefinition(BaseSpireIds.StrikeIronclad, 1, CardType.Attack, new AttackEffect(7)));
        registry.Register(new CardDefinition(ContentId.Parse("training-mod:practice-card"), 0, CardType.Skill,
            EffectDsl.Parse("""{"op":"custom","handler":"training-mod:grant-block","payload":{"amount":2}}"""),
            Target: TargetSelector.Self));
        registry.Register(new GrantBlockHandler());
        registry.Register(Hook);
    }
}

public sealed class GrantBlockHandler : ICustomEffectHandler
{
    public ContentId Id { get; } = ContentId.Parse("training-mod:grant-block");
    public void Execute(EffectExecutionContext context, JsonElement payload)
    {
        context.SetInt("practice-card-uses", context.GetInt("practice-card-uses") + 1);
        context.GainBlock(payload.GetProperty("amount").GetInt32());
    }
}

public sealed class PlayCounterHook : IRuleHook
{
    public ContentId Id { get; } = ContentId.Parse("training-mod:play-counter");
    public void OnEvent(RuleHookContext context, GameEvent gameEvent)
    {
        if (gameEvent.Type == "play") context.SetInt("play-count", context.GetInt("play-count") + 1);
    }
}
