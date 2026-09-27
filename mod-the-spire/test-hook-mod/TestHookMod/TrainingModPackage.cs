using BaseSpire;
using CoreTheSpire;
using System.Text.Json;

namespace TrainingMod;

public sealed class TrainingModPackage : IRulePackage
{
    public static readonly ContentId TrainingAncientId = ContentId.Parse("training-mod:training-ancient");
    public static readonly ContentId TrainingAncientOptionId = ContentId.Parse("training-mod:gain-run-gold");
    public static readonly ContentId TrainingAncientFightOptionId = ContentId.Parse("training-mod:gain-run-gold-and-fight");
    public PackageInfo Info { get; } = new("training-mod", "0.1.0");
    public PlayCounterHook Hook { get; } = new();

    public void Register(ContentRegistry registry)
    {
        registry.Override(new CardDefinition(BaseSpireIds.StrikeIronclad, 1, CardType.Attack, new AttackEffect(7)));
        registry.Register(new CardDefinition(ContentId.Parse("training-mod:practice-card"), 0, CardType.Skill,
            EffectDsl.Parse("""{"op":"custom","handler":"training-mod:grant-block","payload":{"amount":2}}"""),
            Target: TargetSelector.Self));
        registry.Register(new GrantBlockHandler());
        registry.Register(new TrainingRunEffectHandler());
        registry.Register(new RunEventDefinition(TrainingAncientId, RunEventKind.Ancient,
        [
            new RunEventOptionDefinition(TrainingAncientOptionId, "Gain training gold",
            [
                EffectDsl.ParseRun("""{"op":"customRun","handler":"training-mod:gain-run-gold","payload":{"amount":2}}""")
            ]),
            new RunEventOptionDefinition(TrainingAncientFightOptionId, "Gain training gold and fight",
            [
                EffectDsl.ParseRun("""{"op":"customRun","handler":"training-mod:gain-run-gold","payload":{"amount":2}}"""),
                new EnterRunCombatEffect(ContentId.Parse("base-spire:fuzzy-wurm-crawler-weak"))
            ])
        ]));
        var standardRun = BaseSpireMapGenerator.CreateStandardRun();
        var acts = standardRun.Acts.Select((act, index) => index == 0
            ? new ActDefinition(act.Id, act.GenerateMap, act.ResolveUnknownRoom, act.EncounterPool,
                act.EventIds, [TrainingAncientId])
            : act).ToArray();
        registry.Override(new RunDefinition(standardRun.Id, acts, standardRun.UnknownRoomOdds));
        registry.Register(Hook);
        registry.Register(new TrainingRunHook());
    }
}

public sealed class TrainingRunEffectHandler : ICustomRunEffectHandler
{
    public ContentId Id { get; } = ContentId.Parse("training-mod:gain-run-gold");

    public void Execute(RunEffectExecutionContext context, JsonElement payload)
    {
        context.SetInt("ancient-visits", context.GetInt("ancient-visits") + 1);
        context.GainGold(payload.GetProperty("amount").GetInt32());
    }
}

public sealed class GrantBlockHandler : ICustomEffectHandler
{
    public ContentId Id { get; } = ContentId.Parse("training-mod:grant-block");
    public void Execute(EffectExecutionContext context, JsonElement payload)
    {
        context.SetInt("practice-card-uses", context.GetInt("practice-card-uses") + 1);
        if (context.IsRunOwned)
        {
            context.SetInt("observed-ancient-visits", context.GetRunInt("ancient-visits"));
            context.SetRunInt("practice-card-uses", context.GetRunInt("practice-card-uses") + 1);
        }
        context.GainBlock(payload.GetProperty("amount").GetInt32());
    }
}

public sealed class PlayCounterHook : IRuleHook
{
    public ContentId Id { get; } = ContentId.Parse("training-mod:play-counter");
    public void OnEvent(RuleHookContext context, GameEvent gameEvent)
    {
        if (gameEvent.Type != "play") return;
        context.SetInt("play-count", context.GetInt("play-count") + 1);
        if (context.IsRunOwned)
            context.SetRunInt("play-count", context.GetRunInt("play-count") + 1);
    }
}

public sealed class TrainingRunHook : IRunHook
{
    public ContentId Id { get; } = ContentId.Parse("training-mod:run-counter");
    public void AfterCombatVictory(RunHookContext context) { }
    public void AfterCombatStarted(RunHookContext context) =>
        context.SetInt("combats-started", context.GetInt("combats-started") + 1);
}
