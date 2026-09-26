using CoreTheSpire;

namespace BaseSpire;

public static class BaseSpireIds
{
    public static readonly ContentId Ironclad = ContentId.Parse("base-spire:ironclad");
    public static readonly ContentId StrikeIronclad = ContentId.Parse("base-spire:strike-ironclad");
    public static readonly ContentId DefendIronclad = ContentId.Parse("base-spire:defend-ironclad");
    public static readonly ContentId Bash = ContentId.Parse("base-spire:bash");
    public static readonly ContentId DampCultist = ContentId.Parse("base-spire:damp-cultist");
    public static readonly ContentId CalcifiedCultist = ContentId.Parse("base-spire:calcified-cultist");
    public static readonly ContentId CultistsNormal = ContentId.Parse("base-spire:cultists-normal");
    public static readonly ContentId Ritual = ContentId.Parse("base-spire:ritual");
    public static readonly ContentId Strength = ContentId.Parse("base-spire:strength");
    public static readonly ContentId Vulnerable = ContentId.Parse("base-spire:vulnerable");
    public static readonly ContentId BurningBlood = ContentId.Parse("base-spire:burning-blood");
    public static readonly ContentId OvergrowthAct = ContentId.Parse("base-spire:overgrowth-act");
    public static readonly ContentId HiveAct = ContentId.Parse("base-spire:hive-act");
    public static readonly ContentId GloryAct = ContentId.Parse("base-spire:glory-act");
    public static readonly ContentId UnderdocksAct = ContentId.Parse("base-spire:underdocks-act");
    public static readonly ContentId StandardRun = ContentId.Parse("base-spire:standard-run");
}

public static class BaseSpireContent
{
    public const int IroncladStartingMaxHp = 80;
    public const int IroncladStartingGold = 99;
    public static IReadOnlyList<ContentId> IroncladStarterDeck() =>
        [.. Enumerable.Repeat(BaseSpireIds.StrikeIronclad, 5), .. Enumerable.Repeat(BaseSpireIds.DefendIronclad, 4), BaseSpireIds.Bash];
    public static CharacterDefinition Ironclad() => new(BaseSpireIds.Ironclad, IroncladStartingMaxHp,
        IroncladStartingGold, IroncladStarterDeck(), [BaseSpireIds.BurningBlood]);
}

public sealed class BaseSpirePackage(bool deadlyEnemies = false, bool toughEnemies = false) : IRulePackage
{
    public PackageInfo Info { get; } = new("base-spire", "0.1.0");

    public void Register(ContentRegistry registry)
    {
        registry.Register(BaseSpireContent.Ironclad());
        registry.Register(new PowerDefinition(BaseSpireIds.Strength, Role: PowerRole.Strength));
        registry.Register(new PowerDefinition(BaseSpireIds.Ritual, PowerTrigger.AfterEnemyTurnEnd,
            BaseSpireIds.Strength, SkipFirstTriggerWhenAppliedToEnemy: true));
        registry.Register(new PowerDefinition(BaseSpireIds.Vulnerable, PowerTrigger.AfterEnemyTurnEnd,
            Role: PowerRole.Vulnerable, DecrementOnTrigger: true, SkipFirstTriggerWhenAppliedToPlayer: true));

        registry.Register(new CardDefinition(BaseSpireIds.StrikeIronclad, 1, CardType.Attack,
            EffectDsl.Parse("""{"op":"attack","amount":6}""")));
        registry.Register(new CardDefinition(BaseSpireIds.DefendIronclad, 1, CardType.Skill,
            EffectDsl.Parse("""{"op":"gainBlock","amount":5}"""), Target: TargetSelector.Self));
        registry.Register(new CardDefinition(BaseSpireIds.Bash, 2, CardType.Attack,
            EffectDsl.Parse("""{"op":"sequence","effects":[{"op":"attack","amount":8},{"op":"applyPower","power":"base-spire:vulnerable","amount":2,"target":"firstEnemy"}]}""")));

        var ritual = deadlyEnemies ? 6 : 5;
        var attack = deadlyEnemies ? 3 : 1;
        var incantation = new EnemyActionDefinition(ContentId.Parse("base-spire:damp-cultist-incantation"),
            new SequenceEffect(Array.Empty<EffectNode>()), IntentKind.Buff, AppliedPower: BaseSpireIds.Ritual, AppliedPowerAmount: ritual);
        var darkStrike = new EnemyActionDefinition(ContentId.Parse("base-spire:damp-cultist-dark-strike"),
            new AttackEffect(attack, TargetSelector.FirstEnemy), IntentKind.Attack, IntentDamage: attack);
        registry.Register(new EnemyDefinition(BaseSpireIds.DampCultist,
            toughEnemies ? 51 : 52, toughEnemies ? 53 : 54, [incantation, darkStrike]));

        var calcifiedAttack = deadlyEnemies ? 11 : 9;
        var calcifiedIncantation = new EnemyActionDefinition(ContentId.Parse("base-spire:calcified-cultist-incantation"),
            new SequenceEffect(Array.Empty<EffectNode>()), IntentKind.Buff,
            AppliedPower: BaseSpireIds.Ritual, AppliedPowerAmount: 2);
        var calcifiedDarkStrike = new EnemyActionDefinition(ContentId.Parse("base-spire:calcified-cultist-dark-strike"),
            new AttackEffect(calcifiedAttack), IntentKind.Attack, IntentDamage: calcifiedAttack);
        registry.Register(new EnemyDefinition(BaseSpireIds.CalcifiedCultist,
            toughEnemies ? 39 : 38, toughEnemies ? 42 : 41, [calcifiedIncantation, calcifiedDarkStrike]));
        registry.Register(new EncounterDefinition(BaseSpireIds.CultistsNormal, RunRoomType.Monster,
            [BaseSpireIds.CalcifiedCultist, BaseSpireIds.DampCultist]));

        registry.Register(new BurningBloodRunHook());
        registry.Register(BaseSpireMapGenerator.CreateStandardRun());
    }

}

internal sealed class BurningBloodRunHook : IRunHook
{
    public ContentId Id { get; } = ContentId.Parse("base-spire:burning-blood-post-combat");

    public void AfterCombatVictory(RunHookContext context)
    {
        if (context.Hp > 0 && context.HasRelic(BaseSpireIds.BurningBlood))
            context.Heal(6);
    }
}

public sealed class SyntheticCombatPackage : IRulePackage
{
    public PackageInfo Info { get; } = new("test-fixture", "1.0.0");

    public void Register(ContentRegistry registry)
    {
        registry.Register(new PowerDefinition(BaseSpireIds.Strength, Role: PowerRole.Strength));
        registry.Register(new PowerDefinition(BaseSpireIds.Ritual, PowerTrigger.AfterEnemyTurnEnd,
            BaseSpireIds.Strength, SkipFirstTriggerWhenAppliedToEnemy: true));
        registry.Register(new EnemyDefinition(ContentId.Parse("test-fixture:cultist"), 53, 53,
        [
            new EnemyActionDefinition(ContentId.Parse("test-fixture:ritual"), new SequenceEffect(Array.Empty<EffectNode>()), IntentKind.Buff,
                AppliedPower: BaseSpireIds.Ritual, AppliedPowerAmount: 5),
            new EnemyActionDefinition(ContentId.Parse("test-fixture:attack"), new AttackEffect(3), IntentKind.Attack, 3)
        ]));
        registry.Register(new CardDefinition(ContentId.Parse("test-fixture:strike"), 1, CardType.Attack, new AttackEffect(6)));
        registry.Register(new CardDefinition(ContentId.Parse("test-fixture:defend"), 1, CardType.Skill, new GainBlockEffect(5)));
        registry.Register(new CardDefinition(ContentId.Parse("test-fixture:bash"), 2, CardType.Attack, new AttackEffect(8)));
    }
}
