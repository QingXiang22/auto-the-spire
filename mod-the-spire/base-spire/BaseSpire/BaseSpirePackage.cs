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
    public static readonly ContentId FuzzyWurmCrawler = ContentId.Parse("base-spire:fuzzy-wurm-crawler");
    public static readonly ContentId FuzzyWurmCrawlerWeak = ContentId.Parse("base-spire:fuzzy-wurm-crawler-weak");
    public static readonly ContentId ShrinkerBeetle = ContentId.Parse("base-spire:shrinker-beetle");
    public static readonly ContentId ShrinkerBeetleWeak = ContentId.Parse("base-spire:shrinker-beetle-weak");
    public static readonly ContentId OvergrowthCrawlers = ContentId.Parse("base-spire:overgrowth-crawlers");
    public static readonly ContentId Nibbit = ContentId.Parse("base-spire:nibbit");
    public static readonly ContentId NibbitsWeak = ContentId.Parse("base-spire:nibbits-weak");
    public static readonly ContentId NibbitsNormal = ContentId.Parse("base-spire:nibbits-normal");
    public static readonly ContentId Ritual = ContentId.Parse("base-spire:ritual");
    public static readonly ContentId Strength = ContentId.Parse("base-spire:strength");
    public static readonly ContentId Vulnerable = ContentId.Parse("base-spire:vulnerable");
    public static readonly ContentId Shrink = ContentId.Parse("base-spire:shrink");
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
        registry.Register(new PowerDefinition(BaseSpireIds.Shrink,
            OutgoingDamageMultiplier: 0.7m, RemoveWhenApplierDies: true));

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

        var crawlerDamage = deadlyEnemies ? 6 : 4;
        var firstAcidGoop = ContentId.Parse("base-spire:fuzzy-wurm-first-acid-goop");
        var inhale = ContentId.Parse("base-spire:fuzzy-wurm-inhale");
        var acidGoop = ContentId.Parse("base-spire:fuzzy-wurm-acid-goop");
        registry.Register(new EnemyDefinition(BaseSpireIds.FuzzyWurmCrawler,
            toughEnemies ? 58 : 55, toughEnemies ? 59 : 57,
            [
                new EnemyActionDefinition(firstAcidGoop, new AttackEffect(crawlerDamage), IntentKind.Attack,
                    crawlerDamage, NextActionId: inhale),
                new EnemyActionDefinition(inhale, new SequenceEffect(Array.Empty<EffectNode>()), IntentKind.Buff,
                    AppliedPower: BaseSpireIds.Strength, AppliedPowerAmount: 7, NextActionId: acidGoop),
                new EnemyActionDefinition(acidGoop, new AttackEffect(crawlerDamage), IntentKind.Attack,
                    crawlerDamage, NextActionId: firstAcidGoop)
            ]));
        registry.Register(new EncounterDefinition(BaseSpireIds.FuzzyWurmCrawlerWeak, RunRoomType.Monster,
            [BaseSpireIds.FuzzyWurmCrawler], isWeak: true, tags: [ContentId.Parse("base-spire:crawler")]));

        var shrink = ContentId.Parse("base-spire:shrinker-beetle-shrink");
        var chomp = ContentId.Parse("base-spire:shrinker-beetle-chomp");
        var stomp = ContentId.Parse("base-spire:shrinker-beetle-stomp");
        var chompDamage = deadlyEnemies ? 8 : 7;
        var stompDamage = deadlyEnemies ? 14 : 13;
        registry.Register(new EnemyDefinition(BaseSpireIds.ShrinkerBeetle,
            toughEnemies ? 40 : 38, toughEnemies ? 42 : 40,
            [
                new EnemyActionDefinition(shrink, new SequenceEffect(Array.Empty<EffectNode>()), IntentKind.Buff,
                    AppliedPower: BaseSpireIds.Shrink, AppliedPowerAmount: -1,
                    NextActionId: chomp, AppliedPowerTarget: TargetSelector.FirstEnemy),
                new EnemyActionDefinition(chomp, new AttackEffect(chompDamage), IntentKind.Attack,
                    chompDamage, NextActionId: stomp),
                new EnemyActionDefinition(stomp, new AttackEffect(stompDamage), IntentKind.Attack,
                    stompDamage, NextActionId: chomp)
            ]));
        registry.Register(new EncounterDefinition(BaseSpireIds.ShrinkerBeetleWeak, RunRoomType.Monster,
            [BaseSpireIds.ShrinkerBeetle], isWeak: true, tags: [ContentId.Parse("base-spire:shrinker")]));
        registry.Register(new EncounterDefinition(BaseSpireIds.OvergrowthCrawlers, RunRoomType.Monster,
            [BaseSpireIds.ShrinkerBeetle, BaseSpireIds.FuzzyWurmCrawler],
            tags: [ContentId.Parse("base-spire:shrinker"), ContentId.Parse("base-spire:crawler")]));

        var nibbitButt = ContentId.Parse("base-spire:nibbit-butt");
        var nibbitSlice = ContentId.Parse("base-spire:nibbit-slice");
        var nibbitHiss = ContentId.Parse("base-spire:nibbit-hiss");
        var nibbitSliceDamage = deadlyEnemies ? 7 : 6;
        var nibbitSliceBlock = toughEnemies ? 6 : 5;
        var nibbitHissStrength = deadlyEnemies ? 3 : 2;
        registry.Register(new EnemyDefinition(BaseSpireIds.Nibbit,
            toughEnemies ? 44 : 42, toughEnemies ? 48 : 46,
            [
                new EnemyActionDefinition(nibbitButt, new AttackEffect(12), IntentKind.Attack,
                    12, NextActionId: nibbitSlice),
                new EnemyActionDefinition(nibbitSlice,
                    new SequenceEffect([new AttackEffect(nibbitSliceDamage), new GainBlockEffect(nibbitSliceBlock)]),
                    IntentKind.Attack, nibbitSliceDamage, NextActionId: nibbitHiss),
                new EnemyActionDefinition(nibbitHiss, new SequenceEffect(Array.Empty<EffectNode>()), IntentKind.Buff,
                    AppliedPower: BaseSpireIds.Strength, AppliedPowerAmount: nibbitHissStrength,
                    NextActionId: nibbitButt)
            ]));
        registry.Register(new EncounterDefinition(BaseSpireIds.NibbitsWeak, RunRoomType.Monster,
            [BaseSpireIds.Nibbit], isWeak: true, tags: [ContentId.Parse("base-spire:nibbit")]));
        registry.Register(new EncounterDefinition(BaseSpireIds.NibbitsNormal, RunRoomType.Monster,
            [BaseSpireIds.Nibbit, BaseSpireIds.Nibbit], tags: [ContentId.Parse("base-spire:nibbit")],
            startingActionsByEnemyIndex: new Dictionary<int, ContentId>
            {
                [0] = nibbitSlice,
                [1] = nibbitHiss
            }));

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
