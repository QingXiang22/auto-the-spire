using BaseSpire;
using CoreTheSpire;
using TrainingMod;

namespace Core.TheSpire.Tests;

public sealed class CombatSliceTests
{
    public void SyntheticRitualFixtureUsesCorrectSideTurnTiming()
    {
        var definition = Build(new SyntheticCombatPackage());
        var environment = new CombatEnvironment(definition);
        var state = environment.Start(123, "ironclad", 56, 65,
            [ContentId.Parse("test-fixture:strike"), ContentId.Parse("test-fixture:defend"), ContentId.Parse("test-fixture:bash")],
            "test-fixture:cultist");

        environment.Step(state, new EndTurnAction());
        Assert.Equal(5, state.State.Enemy.PowerAmount(BaseSpireIds.Ritual));
        Assert.Equal(0, state.State.Enemy.Strength);
        Assert.Equal(3, environment.Observe(state).IntentDamage);

        environment.Step(state, new EndTurnAction());
        Assert.Equal(53, state.State.Player.Creature.Hp);
        Assert.Contains(state.State.Events, e => e.Type == "damage" && e.Amount == 3);
        Assert.Equal(5, state.State.Enemy.Strength);
        Assert.Equal(8, environment.Observe(state).IntentDamage);

        environment.Step(state, new EndTurnAction());
        Assert.Contains(state.State.Events, e => e.Type == "damage" && e.Amount == 8);
        Assert.Equal(45, state.State.Player.Creature.Hp);
    }

    public void BasePackageRegistersVanillaStartingDeckAndDeadlyCultistValues()
    {
        var definition = Build(new BaseSpirePackage(deadlyEnemies: true));
        Assert.Equal(10, BaseSpireContent.IroncladStarterDeck().Count);
        Assert.Equal(5, BaseSpireContent.IroncladStarterDeck().Count(id => id == BaseSpireIds.StrikeIronclad));
        Assert.Equal(4, BaseSpireContent.IroncladStarterDeck().Count(id => id == BaseSpireIds.DefendIronclad));
        var ironclad = definition.Characters[BaseSpireIds.Ironclad];
        Assert.Equal(BaseSpireIds.IroncladCardPool, ironclad.CardPools.Single());
        Assert.Equal(new[] { BaseSpireIds.Anger, BaseSpireIds.Armaments, BaseSpireIds.Bash, BaseSpireIds.BattleTrance,
                BaseSpireIds.Bloodletting,
                BaseSpireIds.Bludgeon, BaseSpireIds.BodySlam,
                BaseSpireIds.BurningPact, BaseSpireIds.DefendIronclad,
                BaseSpireIds.DemonForm, BaseSpireIds.FeelNoPain, BaseSpireIds.Headbutt, BaseSpireIds.Inflame,
                BaseSpireIds.IronWave, BaseSpireIds.Offering, BaseSpireIds.PommelStrike, BaseSpireIds.SecondWind,
                BaseSpireIds.ShrugItOff,
                BaseSpireIds.StrikeIronclad, BaseSpireIds.SwordBoomerang, BaseSpireIds.Thunderclap,
                BaseSpireIds.TrueGrit, BaseSpireIds.TwinStrike,
                BaseSpireIds.Uppercut }.AsEnumerable(),
            definition.CardPools[BaseSpireIds.IroncladCardPool].Cards);
        Assert.Equal(CardRarity.Basic, definition.Cards[BaseSpireIds.StrikeIronclad].Rarity);
        Assert.Equal(CardRarity.Common, definition.Cards[BaseSpireIds.Anger].Rarity);
        Assert.Equal(CardRarity.Common, definition.Cards[BaseSpireIds.Armaments].Rarity);
        Assert.Equal(CardRarity.Common, definition.Cards[BaseSpireIds.BodySlam].Rarity);
        Assert.Equal(CardRarity.Uncommon, definition.Cards[BaseSpireIds.BattleTrance].Rarity);
        Assert.Equal(CardRarity.Uncommon, definition.Cards[BaseSpireIds.FeelNoPain].Rarity);
        Assert.Equal(CardRarity.Common, definition.Cards[BaseSpireIds.Headbutt].Rarity);
        Assert.Equal(CardRarity.Common, definition.Cards[BaseSpireIds.TrueGrit].Rarity);
        Assert.Equal(CardRarity.Common, definition.Cards[BaseSpireIds.SwordBoomerang].Rarity);
        Assert.Equal(CardRarity.Uncommon, definition.Cards[BaseSpireIds.SecondWind].Rarity);
        Assert.Equal(CardRarity.Common, definition.Cards[BaseSpireIds.Bloodletting].Rarity);
        Assert.Equal(CardRarity.Common, definition.Cards[BaseSpireIds.IronWave].Rarity);
        Assert.Equal(CardRarity.Uncommon, definition.Cards[BaseSpireIds.Inflame].Rarity);
        Assert.Equal(CardRarity.Uncommon, definition.Cards[BaseSpireIds.Uppercut].Rarity);
        Assert.Equal(CardRarity.Uncommon, definition.Cards[BaseSpireIds.Bludgeon].Rarity);
        Assert.Equal(CardRarity.Uncommon, definition.Cards[BaseSpireIds.BurningPact].Rarity);
        Assert.Equal(CardRarity.Rare, definition.Cards[BaseSpireIds.DemonForm].Rarity);
        Assert.Equal(CardRarity.Rare, definition.Cards[BaseSpireIds.Offering].Rarity);
        Assert.Equal(new AttackEffect(42), definition.Cards[BaseSpireIds.Bludgeon]
            .ResolveEffect(new CardInstance("test:bludgeon+", BaseSpireIds.Bludgeon, 1)));
        Assert.Equal(new ApplyPowerEffect(BaseSpireIds.DemonFormPower, 3, TargetSelector.Self),
            definition.Cards[BaseSpireIds.DemonForm]
                .ResolveEffect(new CardInstance("test:demon-form+", BaseSpireIds.DemonForm, 1)));
        var upgradedHeadbutt = (SequenceEffect)definition.Cards[BaseSpireIds.Headbutt]
            .ResolveEffect(new CardInstance("test:headbutt+", BaseSpireIds.Headbutt, 1));
        Assert.Equal(new AttackEffect(12), upgradedHeadbutt.Effects[0]);
        Assert.Equal(new ChooseCardFromPileEffect(CardPile.Discard, CardPile.Draw,
            "Choose a card from discard", CardPilePosition.Top), upgradedHeadbutt.Effects[1]);
        var upgradedBurningPact = (SequenceEffect)definition.Cards[BaseSpireIds.BurningPact]
            .ResolveEffect(new CardInstance("test:burning-pact+", BaseSpireIds.BurningPact, 1));
        Assert.Equal(new DrawCardsEffect(3), upgradedBurningPact.Effects[1]);
        var upgradedBloodletting = (SequenceEffect)definition.Cards[BaseSpireIds.Bloodletting]
            .ResolveEffect(new CardInstance("test:bloodletting+", BaseSpireIds.Bloodletting, 1));
        Assert.Equal(new ModifyEnergyEffect(3), upgradedBloodletting.Effects[1]);
        var upgradedOffering = (SequenceEffect)definition.Cards[BaseSpireIds.Offering]
            .ResolveEffect(new CardInstance("test:offering+", BaseSpireIds.Offering, 1));
        Assert.Equal(new LoseHpEffect(6), upgradedOffering.Effects[0]);
        Assert.Equal(new ModifyEnergyEffect(2), upgradedOffering.Effects[1]);
        Assert.Equal(new DrawCardsEffect(5), upgradedOffering.Effects[2]);
        Assert.Equal(TargetSelector.AllOpponents, definition.Cards[BaseSpireIds.Thunderclap].Target);
        var thunderclap = (SequenceEffect)definition.Cards[BaseSpireIds.Thunderclap].Effect;
        Assert.Equal(new AttackEffect(4, TargetSelector.AllOpponents), thunderclap.Effects[0]);
        Assert.Equal(new ApplyPowerEffect(BaseSpireIds.Vulnerable, 1, TargetSelector.AllOpponents), thunderclap.Effects[1]);
        var upgradedIronWave = (SequenceEffect)definition.Cards[BaseSpireIds.IronWave]
            .ResolveEffect(new CardInstance("test:iron-wave+", BaseSpireIds.IronWave, 1));
        Assert.Equal(new GainBlockEffect(7), upgradedIronWave.Effects[0]);
        Assert.Equal(new AttackEffect(7), upgradedIronWave.Effects[1]);
        Assert.Equal(new ApplyPowerEffect(BaseSpireIds.Strength, 3, TargetSelector.Self),
            definition.Cards[BaseSpireIds.Inflame].ResolveEffect(new CardInstance("test:inflame+", BaseSpireIds.Inflame, 1)));
        var upgradedPommel = (SequenceEffect)definition.Cards[BaseSpireIds.PommelStrike]
            .ResolveEffect(new CardInstance("test:pommel+", BaseSpireIds.PommelStrike, 1));
        Assert.Equal(new AttackEffect(10), upgradedPommel.Effects[0]);
        Assert.Equal(new DrawCardsEffect(2), upgradedPommel.Effects[1]);
        var upgradedShrug = (SequenceEffect)definition.Cards[BaseSpireIds.ShrugItOff]
            .ResolveEffect(new CardInstance("test:shrug+", BaseSpireIds.ShrugItOff, 1));
        Assert.Equal(new GainBlockEffect(11), upgradedShrug.Effects[0]);
        Assert.Equal(new DrawCardsEffect(1), upgradedShrug.Effects[1]);
        Assert.Equal(new RepeatEffect(2, new AttackEffect(7)),
            definition.Cards[BaseSpireIds.TwinStrike].ResolveEffect(new CardInstance("test:twin+", BaseSpireIds.TwinStrike, 1)));
        var upgradedUppercut = (SequenceEffect)definition.Cards[BaseSpireIds.Uppercut]
            .ResolveEffect(new CardInstance("test:uppercut+", BaseSpireIds.Uppercut, 1));
        Assert.Equal(new ApplyPowerEffect(BaseSpireIds.Weak, 2, TargetSelector.FirstEnemy), upgradedUppercut.Effects[1]);
        Assert.Equal(new ApplyPowerEffect(BaseSpireIds.Vulnerable, 2, TargetSelector.FirstEnemy), upgradedUppercut.Effects[2]);
        Assert.Equal(new AttackEffect(9), definition.Cards[BaseSpireIds.StrikeIronclad]
            .ResolveEffect(new CardInstance("test:strike+", BaseSpireIds.StrikeIronclad, 1)));
        Assert.Equal(new GainBlockEffect(8), definition.Cards[BaseSpireIds.DefendIronclad]
            .ResolveEffect(new CardInstance("test:defend+", BaseSpireIds.DefendIronclad, 1)));
        var upgradedBash = (SequenceEffect)definition.Cards[BaseSpireIds.Bash]
            .ResolveEffect(new CardInstance("test:bash+", BaseSpireIds.Bash, 1));
        Assert.Equal(new AttackEffect(10), upgradedBash.Effects[0]);
        Assert.Equal(new ApplyPowerEffect(BaseSpireIds.Vulnerable, 3, TargetSelector.FirstEnemy), upgradedBash.Effects[1]);
        Assert.Throws<ArgumentException>(() => new CardPoolDefinition(ContentId.Parse("test:duplicate-pool"),
            [BaseSpireIds.Bash, BaseSpireIds.Bash]));
        var invalidPoolRegistry = new ContentRegistry();
        invalidPoolRegistry.AddPackage(new InvalidCardPoolPackage());
        Assert.Throws<InvalidOperationException>(() => invalidPoolRegistry.Build());
        var env = new CombatEnvironment(definition);
        var state = env.Start(1, "ironclad", 56, 65, [], BaseSpireIds.DampCultist.ToString());
        env.Step(state, new EndTurnAction());
        Assert.Equal(6, state.State.Enemy.PowerAmount(BaseSpireIds.Ritual));
        env.Step(state, new EndTurnAction());
        Assert.Contains(state.State.Events, e => e.Type == "damage" && e.Amount == 3);
        Assert.Equal(6, state.State.Enemy.Strength);
    }

    public void BaseStrikeBashAndVulnerableUseRegisteredRules()
    {
        var definition = Build(new BaseSpirePackage());
        var env = new CombatEnvironment(definition);
        var state = env.Start(2, "ironclad", 56, 65,
            [BaseSpireIds.Bash, BaseSpireIds.StrikeIronclad], BaseSpireIds.DampCultist.ToString(), enemyHp: 53);

        env.Step(state, new PlayCardAction(0, state.State.Enemy.Id));
        Assert.Equal(45, state.State.Enemy.Hp);
        Assert.Equal(2, state.State.Enemy.PowerAmount(BaseSpireIds.Vulnerable));
        env.Step(state, new PlayCardAction(0, state.State.Enemy.Id));
        Assert.Equal(36, state.State.Enemy.Hp);
        Assert.Contains(state.State.Events, e => e.Type == "damage" && e.Amount == 9);

        var addedCards = env.Start(21, "ironclad", 56, 65,
            [BaseSpireIds.IronWave, BaseSpireIds.Inflame], BaseSpireIds.DampCultist.ToString(), enemyHp: 53);
        env.Step(addedCards, new PlayCardAction(0, addedCards.State.Enemy.Id));
        Assert.Equal(5, addedCards.State.Player.Creature.Block);
        Assert.Equal(48, addedCards.State.Enemy.Hp);
        env.Step(addedCards, new PlayCardAction(0, addedCards.State.Player.Id));
        Assert.Equal(2, addedCards.State.Player.Creature.Strength);

        var pommel = env.Start(22, "ironclad", 56, 65, [BaseSpireIds.PommelStrike],
            BaseSpireIds.DampCultist.ToString(), enemyHp: 53, drawPile: [BaseSpireIds.StrikeIronclad]);
        env.Step(pommel, new PlayCardAction(0, pommel.State.Enemy.Id));
        Assert.Equal(44, pommel.State.Enemy.Hp);
        Assert.Equal(BaseSpireIds.StrikeIronclad, pommel.State.Player.Hand[0]);

        var shrug = env.Start(23, "ironclad", 56, 65, [BaseSpireIds.ShrugItOff],
            BaseSpireIds.DampCultist.ToString(), drawPile: [BaseSpireIds.StrikeIronclad]);
        env.Step(shrug, new PlayCardAction(0, "ironclad"));
        Assert.Equal(8, shrug.State.Player.Creature.Block);
        Assert.Equal(BaseSpireIds.StrikeIronclad, shrug.State.Player.Hand[0]);

        var twin = env.Start(24, "ironclad", 56, 65, [BaseSpireIds.TwinStrike],
            BaseSpireIds.DampCultist.ToString(), enemyHp: 53);
        env.Step(twin, new PlayCardAction(0, twin.State.Enemy.Id));
        Assert.Equal(43, twin.State.Enemy.Hp);
        Assert.Equal(2, twin.State.Events.Count(gameEvent => gameEvent.Type == "damage" && gameEvent.Amount == 5));

        var weakRules = new CombatEnvironment(Build(new BaseSpirePackage(deadlyEnemies: true)));
        var darkStrike = ContentId.Parse("base-spire:damp-cultist-dark-strike");
        var uppercut = weakRules.StartEncounter(25, "ironclad", 56, 65, [BaseSpireIds.Uppercut],
            [new CombatEnemySpawn(BaseSpireIds.DampCultist.ToString(), 52, StartingActionOverride: darkStrike)]);
        weakRules.Step(uppercut, new PlayCardAction(0, uppercut.State.Enemy.Id));
        Assert.Equal(39, uppercut.State.Enemy.Hp);
        Assert.Equal(1, uppercut.State.Enemy.PowerAmount(BaseSpireIds.Weak));
        Assert.Equal(1, uppercut.State.Enemy.PowerAmount(BaseSpireIds.Vulnerable));
        Assert.Equal(2, weakRules.Observe(uppercut).IntentDamage);
        weakRules.Step(uppercut, new EndTurnAction());
        Assert.Equal(54, uppercut.State.Player.Creature.Hp);
        Assert.Equal(0, uppercut.State.Enemy.PowerAmount(BaseSpireIds.Weak));
        Assert.Equal(0, uppercut.State.Enemy.PowerAmount(BaseSpireIds.Vulnerable));
    }

    public void BaseDampCultistNormalValuesMatchSourceRules()
    {
        var env = new CombatEnvironment(Build(new BaseSpirePackage()));
        var state = env.Start(3, "ironclad", 56, 65, [], BaseSpireIds.DampCultist.ToString(), enemyHp: 53);
        env.Step(state, new EndTurnAction());
        Assert.Equal(5, state.State.Enemy.PowerAmount(BaseSpireIds.Ritual));
        Assert.Equal(1, env.Observe(state).IntentDamage);
        env.Step(state, new EndTurnAction());
        Assert.Contains(state.State.Events, e => e.Type == "damage" && e.Amount == 1);
        Assert.Equal(5, state.State.Enemy.Strength);
    }

    public void BaseCultistsEncounterRunsBothSourceEnemySequences()
    {
        var definition = Build(new BaseSpirePackage());
        Assert.True(definition.Encounters.ContainsKey(BaseSpireIds.CultistsNormal));
        var environment = new CombatEnvironment(definition);
        var session = environment.StartEncounter(186, "ironclad", 56, 65, [], BaseSpireIds.CultistsNormal);

        var initial = environment.Observe(session);
        Assert.Equal(2, initial.Enemies.Count);
        Assert.True(initial.Enemies[0].Hp is >= 38 and <= 41);
        Assert.True(initial.Enemies[1].Hp is >= 52 and <= 54);
        environment.Step(session, new EndTurnAction());
        var afterIncantations = environment.Observe(session);
        Assert.Equal(2, session.State.Enemies[0].Creature.PowerAmount(BaseSpireIds.Ritual));
        Assert.Equal(5, session.State.Enemies[1].Creature.PowerAmount(BaseSpireIds.Ritual));
        Assert.Equal(0, session.State.Enemies[0].Creature.Strength);
        Assert.Equal(0, session.State.Enemies[1].Creature.Strength);
        Assert.Equal(9, afterIncantations.Enemies[0].IntentDamage);
        Assert.Equal(1, afterIncantations.Enemies[1].IntentDamage);

        var secondTurn = environment.Step(session, new EndTurnAction());
        Assert.Equal(46, secondTurn.Observation.PlayerHp);
        Assert.Equal(10, secondTurn.Events.Where(gameEvent => gameEvent.Type == "damage").Sum(gameEvent => gameEvent.Amount));
        Assert.Equal(2, session.State.Enemies[0].Creature.Strength);
        Assert.Equal(5, session.State.Enemies[1].Creature.Strength);

        var thirdTurn = environment.Step(session, new EndTurnAction());
        Assert.Equal(29, thirdTurn.Observation.PlayerHp);
        Assert.Equal(17, thirdTurn.Events.Where(gameEvent => gameEvent.Type == "damage").Sum(gameEvent => gameEvent.Amount));

        var deadlyEnvironment = new CombatEnvironment(Build(new BaseSpirePackage(deadlyEnemies: true, toughEnemies: true)));
        var deadly = deadlyEnvironment.StartEncounter(187, "ironclad", 56, 65, [], BaseSpireIds.CultistsNormal);
        Assert.True(deadlyEnvironment.Observe(deadly).Enemies[0].Hp is >= 39 and <= 42);
        Assert.True(deadlyEnvironment.Observe(deadly).Enemies[1].Hp is >= 51 and <= 53);
        deadlyEnvironment.Step(deadly, new EndTurnAction());
        var deadlyAttackIntents = deadlyEnvironment.Observe(deadly).Enemies.Select(enemy => enemy.IntentDamage).ToArray();
        Assert.True(deadlyAttackIntents.SequenceEqual([11, 3]));
    }

    public void BaseFuzzyWurmEncounterUsesItsLinkedSourceActionCycle()
    {
        var actionId = ContentId.Parse("action-graph-test:start");
        var missingActionId = ContentId.Parse("action-graph-test:missing");
        Assert.Throws<ArgumentException>(() => new EnemyDefinition(ContentId.Parse("action-graph-test:enemy"), 1, 1,
        [
            new EnemyActionDefinition(actionId, new AttackEffect(1), IntentKind.Attack,
                NextActionId: missingActionId)
        ]));

        var definition = Build(new BaseSpirePackage());
        var environment = new CombatEnvironment(definition);
        var session = environment.StartEncounter(207, "ironclad", 56, 65, [], BaseSpireIds.FuzzyWurmCrawlerWeak);
        Assert.True(environment.Observe(session).Enemies[0].Hp is >= 55 and <= 57);
        Assert.Equal(IntentKind.Attack, environment.Observe(session).Intent);
        Assert.Equal(4, environment.Observe(session).IntentDamage);

        environment.Step(session, new EndTurnAction());
        Assert.Equal(52, environment.Observe(session).PlayerHp);
        Assert.Equal(IntentKind.Buff, environment.Observe(session).Intent);
        environment.Step(session, new EndTurnAction());
        Assert.Equal(7, session.State.Enemies[0].Creature.Strength);
        Assert.Equal(IntentKind.Attack, environment.Observe(session).Intent);
        Assert.Equal(11, environment.Observe(session).IntentDamage);
        environment.Step(session, new EndTurnAction());
        Assert.Equal(41, environment.Observe(session).PlayerHp);
        Assert.Equal(11, environment.Observe(session).IntentDamage);
        environment.Step(session, new EndTurnAction());
        Assert.Equal(30, environment.Observe(session).PlayerHp);
        Assert.Equal(IntentKind.Buff, environment.Observe(session).Intent);

        var toughDeadlyEnvironment = new CombatEnvironment(Build(new BaseSpirePackage(deadlyEnemies: true, toughEnemies: true)));
        var toughDeadly = toughDeadlyEnvironment.StartEncounter(208, "ironclad", 56, 65, [], BaseSpireIds.FuzzyWurmCrawlerWeak);
        Assert.True(toughDeadlyEnvironment.Observe(toughDeadly).Enemies[0].Hp is >= 58 and <= 59);
        Assert.Equal(6, toughDeadlyEnvironment.Observe(toughDeadly).IntentDamage);
        toughDeadlyEnvironment.Step(toughDeadly, new EndTurnAction());
        toughDeadlyEnvironment.Step(toughDeadly, new EndTurnAction());
        Assert.Equal(7, toughDeadly.State.Enemies[0].Creature.Strength);
        Assert.Equal(13, toughDeadlyEnvironment.Observe(toughDeadly).IntentDamage);
    }

    public void BaseShrinkerBeetleUsesAPlayerDamageModifierRemovedWithItsApplier()
    {
        var definition = Build(new BaseSpirePackage());
        var environment = new CombatEnvironment(definition);
        var session = environment.StartEncounter(209, "ironclad", 56, 65,
            [BaseSpireIds.StrikeIronclad], BaseSpireIds.ShrinkerBeetleWeak);
        var initial = environment.Observe(session);
        Assert.True(initial.Enemies[0].Hp is >= 38 and <= 40);
        Assert.Equal(IntentKind.Buff, initial.Intent);
        environment.Step(session, new EndTurnAction());
        Assert.Equal(-1, session.State.Player.Creature.PowerAmount(BaseSpireIds.Shrink));
        Assert.Equal(7, environment.Observe(session).IntentDamage);
        var enemyInstanceId = environment.Observe(session).Enemies[0].Id;
        var enemyHp = session.State.Enemies[0].Creature.Hp;
        var attack = environment.Step(session, new PlayCardAction(0, enemyInstanceId));
        Assert.Equal(enemyHp - 4, session.State.Enemies[0].Creature.Hp);
        Assert.Contains(attack.Events, gameEvent => gameEvent.Type == "damage" && gameEvent.Amount == 4);

        environment.Step(session, new EndTurnAction());
        Assert.Equal(49, environment.Observe(session).PlayerHp);
        Assert.Equal(13, environment.Observe(session).IntentDamage);
        environment.Step(session, new EndTurnAction());
        Assert.Equal(36, environment.Observe(session).PlayerHp);
        Assert.Equal(7, environment.Observe(session).IntentDamage);

        var composite = environment.StartEncounter(210, "ironclad", 56, 65, [], BaseSpireIds.OvergrowthCrawlers);
        Assert.Equal(2, environment.Observe(composite).Enemies.Count);
        Assert.True(environment.Observe(composite).Enemies.Any(enemy => enemy.Id.StartsWith(BaseSpireIds.ShrinkerBeetle.Value, StringComparison.Ordinal)));
        Assert.True(environment.Observe(composite).Enemies.Any(enemy => enemy.Id.StartsWith(BaseSpireIds.FuzzyWurmCrawler.Value, StringComparison.Ordinal)));

        var lethal = environment.Start(211, "ironclad", 56, 65, [BaseSpireIds.StrikeIronclad],
            BaseSpireIds.ShrinkerBeetle.ToString(), enemyHp: 1);
        environment.Step(lethal, new EndTurnAction());
        Assert.Equal(-1, lethal.State.Player.Creature.PowerAmount(BaseSpireIds.Shrink));
        var lethalEnemyId = environment.Observe(lethal).Enemies[0].Id;
        environment.Step(lethal, new PlayCardAction(0, lethalEnemyId));
        Assert.Equal(0, lethal.State.Player.Creature.PowerAmount(BaseSpireIds.Shrink));
        Assert.Contains(lethal.State.Events, gameEvent => gameEvent.Type == "power_removed"
            && gameEvent.Target == "ironclad" && gameEvent.Amount == -1);
    }

    public void EnemyActionPowerReferencesAreValidatedWhenBuildingContent()
    {
        var registry = new ContentRegistry();
        registry.AddPackage(new MissingEnemyPowerPackage());
        Assert.Throws<InvalidOperationException>(() => registry.Build());
    }

    public void EnemyTransitionsUseWeightedRngAndPreventImmediateRepeats()
    {
        var environment = new CombatEnvironment(Build(new RandomEnemyTransitionPackage()));

        static int[] Trace(CombatEnvironment combat, uint seed)
        {
            var session = combat.Start(seed, "ironclad", 100, 100, [], "random-enemy:enemy");
            var damages = new List<int> { combat.Observe(session).IntentDamage };
            for (var i = 0; i < 8; i++)
            {
                combat.Step(session, new EndTurnAction());
                damages.Add(combat.Observe(session).IntentDamage);
            }
            Assert.Equal(9, session.Random.Get("enemy-ai").Counter);
            return damages.ToArray();
        }

        var first = Trace(environment, 304);
        var replay = Trace(environment, 304);
        Assert.Equal(first.AsEnumerable(), replay.AsEnumerable());
        Assert.True(first.Zip(first.Skip(1)).All(pair => pair.First != pair.Second));
        Assert.True(Enumerable.Range(0, 12).Select(seed => Trace(environment, (uint)seed)[1]).Distinct().Count() > 1);

        var invalidTransition = new EnemyActionTransition([new EnemyActionChoice(ContentId.Parse("random-enemy:missing"))]);
        Assert.Throws<ArgumentException>(() => new EnemyDefinition(ContentId.Parse("random-enemy:invalid"), 10, 10,
            [new EnemyActionDefinition(ContentId.Parse("random-enemy:action"), new AttackEffect(1), IntentKind.Attack,
                Transition: invalidTransition)]));
        Assert.Throws<ArgumentException>(() => new EnemyActionTransition(
            [new EnemyActionChoice(ContentId.Parse("random-enemy:only"))],
            EnemyActionRepeatPolicy.PreventImmediateRepeat));
    }

    public void BaseWeakSlimeEncounterUsesRandomSlotsAndAppliesSlimedCards()
    {
        var environment = new CombatEnvironment(Build(new BaseSpirePackage()));
        var encounter = environment.StartEncounter(401, "ironclad", 80, 80, [], BaseSpireIds.SlimesWeak);
        var enemyIds = environment.Observe(encounter).Enemies.Select(enemy => enemy.Id.Split('#')[0]).ToArray();
        Assert.Equal(3, enemyIds.Length);
        Assert.True(enemyIds[0] is "base-spire:leaf-slime-small" or "base-spire:twig-slime-small");
        Assert.True(enemyIds[2] is "base-spire:leaf-slime-small" or "base-spire:twig-slime-small");
        Assert.True(enemyIds[0] != enemyIds[2]);
        Assert.True(enemyIds[1] is "base-spire:leaf-slime-medium" or "base-spire:twig-slime-medium");
        Assert.Equal(3, encounter.Random.Get("encounter-enemies").Counter);
        var replay = environment.StartEncounter(401, "ironclad", 80, 80, [], BaseSpireIds.SlimesWeak);
        Assert.True(environment.Observe(encounter).Enemies.Select(enemy => (enemy.Id, enemy.Intent, enemy.IntentDamage))
            .SequenceEqual(environment.Observe(replay).Enemies.Select(enemy => (enemy.Id, enemy.Intent, enemy.IntentDamage))));

        GameSession? goopSession = null;
        for (uint seed = 0; seed < 32; seed++)
        {
            var candidate = environment.Start(seed, "ironclad", 80, 80, [], BaseSpireIds.LeafSlimeSmall.ToString());
            if (environment.Observe(candidate).Intent != IntentKind.Debuff) continue;
            goopSession = candidate;
            break;
        }
        Assert.True(goopSession is not null);
        environment.Step(goopSession!, new EndTurnAction());
        Assert.Contains(goopSession!.State.Player.Hand, card => card == BaseSpireIds.Slimed);
        var addedSlimed = goopSession.State.Player.HandInstances.Single(card => card.CardId == BaseSpireIds.Slimed);
        Assert.True(addedSlimed.InstanceId.Contains("generated-card-", StringComparison.Ordinal));
        Assert.Equal(goopSession.State.Player.DiscardPile.AsEnumerable(), environment.Observe(goopSession).DiscardPile.AsEnumerable());

        var playedSlimed = environment.Start(403, "ironclad", 80, 80, [BaseSpireIds.Slimed], BaseSpireIds.TwigSlimeSmall.ToString(),
            drawPile: [BaseSpireIds.StrikeIronclad]);
        var slimedInstanceId = playedSlimed.State.Player.HandInstances[0].InstanceId;
        environment.Step(playedSlimed, new PlayCardAction(0, "ironclad"));
        Assert.Equal(BaseSpireIds.StrikeIronclad, playedSlimed.State.Player.Hand[0]);
        Assert.Equal(BaseSpireIds.Slimed, playedSlimed.State.Player.ExhaustPile[0]);
        Assert.Equal(slimedInstanceId, playedSlimed.State.Player.ExhaustPileInstances[0].InstanceId);

        var leafMedium = environment.Start(404, "ironclad", 80, 80, [], BaseSpireIds.LeafSlimeMedium.ToString());
        Assert.Equal(IntentKind.Debuff, environment.Observe(leafMedium).Intent);
        environment.Step(leafMedium, new EndTurnAction());
        Assert.Equal(2, leafMedium.State.Player.Hand.Count(card => card == BaseSpireIds.Slimed));
        Assert.Equal(8, environment.Observe(leafMedium).IntentDamage);

        var twigMedium = environment.Start(405, "ironclad", 1000, 1000, [], BaseSpireIds.TwigSlimeMedium.ToString());
        Assert.Equal(IntentKind.Debuff, environment.Observe(twigMedium).Intent);
        var history = new List<ContentId>();
        for (var turn = 0; turn < 18; turn++)
        {
            environment.Step(twigMedium, new EndTurnAction());
            history.Add(twigMedium.State.Enemies[0].Definition.Actions[twigMedium.State.Enemies[0].ActionIndex].Id);
        }
        var clumpAction = ContentId.Parse("base-spire:twig-medium-clump-shot");
        Assert.True(history.Zip(history.Skip(1)).Any(pair => pair.First == clumpAction && pair.Second == clumpAction));
        Assert.False(history.Zip(history.Skip(1)).Zip(history.Skip(2), (pair, third) =>
            pair.First == clumpAction && pair.Second == clumpAction && third == clumpAction).Any(run => run));
    }

    public void BaseSoloNibbitFollowsItsAttackBlockAndStrengthCycle()
    {
        var definition = Build(new BaseSpirePackage());
        var environment = new CombatEnvironment(definition);
        var session = environment.StartEncounter(212, "ironclad", 80, 80, [], BaseSpireIds.NibbitsWeak);
        Assert.True(environment.Observe(session).Enemies[0].Hp is >= 42 and <= 46);
        Assert.Equal(12, environment.Observe(session).IntentDamage);

        environment.Step(session, new EndTurnAction());
        Assert.Equal(68, environment.Observe(session).PlayerHp);
        Assert.Equal(6, environment.Observe(session).IntentDamage);
        environment.Step(session, new EndTurnAction());
        Assert.Equal(62, environment.Observe(session).PlayerHp);
        Assert.Equal(5, environment.Observe(session).Enemies[0].Block);

        environment.Step(session, new EndTurnAction());
        Assert.Equal(2, session.State.Enemies[0].Creature.Strength);
        Assert.Equal(14, environment.Observe(session).IntentDamage);
        environment.Step(session, new EndTurnAction());
        Assert.Equal(48, environment.Observe(session).PlayerHp);

        var toughDeadly = new CombatEnvironment(Build(new BaseSpirePackage(deadlyEnemies: true, toughEnemies: true)));
        var altered = toughDeadly.StartEncounter(213, "ironclad", 80, 80, [], BaseSpireIds.NibbitsWeak);
        Assert.True(toughDeadly.Observe(altered).Enemies[0].Hp is >= 44 and <= 48);
        toughDeadly.Step(altered, new EndTurnAction());
        toughDeadly.Step(altered, new EndTurnAction());
        Assert.Equal(6, toughDeadly.Observe(altered).Enemies[0].Block);
        toughDeadly.Step(altered, new EndTurnAction());
        Assert.Equal(3, altered.State.Enemies[0].Creature.Strength);

        var pair = environment.StartEncounter(214, "ironclad", 80, 80, [], BaseSpireIds.NibbitsNormal);
        Assert.Equal(0, pair.Random.Get("enemy-ai").Counter);
        var pairOpening = environment.Observe(pair).Enemies;
        Assert.Equal(2, pairOpening.Count);
        Assert.Equal(6, pairOpening[0].IntentDamage);
        Assert.Equal(IntentKind.Buff, pairOpening[1].Intent);
        var openingTurn = environment.Step(pair, new EndTurnAction());
        Assert.Equal(74, openingTurn.Observation.PlayerHp);
        Assert.Equal(5, openingTurn.Observation.Enemies[0].Block);
        Assert.Equal(2, openingTurn.Observation.Enemies[1].Strength);
        Assert.Equal(IntentKind.Buff, openingTurn.Observation.Enemies[0].Intent);
        Assert.Equal(14, openingTurn.Observation.Enemies[1].IntentDamage);

        Assert.Throws<ArgumentException>(() => environment.StartEncounter(215, "ironclad", 80, 80, [],
            [new CombatEnemySpawn(BaseSpireIds.Nibbit.ToString(), StartingActionOverride: ContentId.Parse("base-spire:missing-action"))]));
    }

    public void MultipleMonstersPreferDistinctInitialHpWhenRangesAllow()
    {
        var environment = new CombatEnvironment(Build(new BaseSpirePackage()));
        var enemyId = BaseSpireIds.Nibbit.ToString();
        var spawns = Enumerable.Range(0, 6).Select(index =>
            new CombatEnemySpawn(enemyId, InstanceId: $"nibbit-{index}")).ToArray();
        var session = environment.StartEncounter(20260927, "ironclad", 80, 80, [], spawns);
        var hp = session.State.Enemies.Select(enemy => enemy.Creature.Hp).ToArray();

        Assert.Equal(6, hp.Length);
        Assert.Equal(5, hp.Take(5).Distinct().Count());
        Assert.True(hp.All(value => value is >= 42 and <= 46));
        Assert.Equal(6, session.Random.Get("encounter-hp").Counter);

        var replay = environment.StartEncounter(20260927, "ironclad", 80, 80, [], spawns);
        Assert.Equal(hp, replay.State.Enemies.Select(enemy => enemy.Creature.Hp));
    }

    public void PlayerDebuffSkipsItsApplicationSideTurnEndAndTicksAfterEnemySide()
    {
        var env = new CombatEnvironment(Build(new IncomingVulnerablePackage()));
        var state = env.Start(4, "ironclad", 56, 65, [], "test-fixture:vulnerable-cultist");
        env.Step(state, new EndTurnAction());
        Assert.Equal(2, state.State.Player.Creature.PowerAmount(BaseSpireIds.Vulnerable));
        Assert.Equal(4, env.Observe(state).IntentDamage);
        env.Step(state, new EndTurnAction());
        Assert.Equal(1, state.State.Player.Creature.PowerAmount(BaseSpireIds.Vulnerable));
        Assert.Equal(49, state.State.Player.Creature.Hp);
    }

    public void SameSeedAndActionsProduceSameTrace()
    {
        static GameSession Run()
        {
            var env = new CombatEnvironment(Build(new SyntheticCombatPackage()));
            var state = env.Start(456, "ironclad", 56, 65, [], "test-fixture:cultist",
                drawPile: [ContentId.Parse("test-fixture:strike"), ContentId.Parse("test-fixture:defend")]);
            env.Step(state, new EndTurnAction());
            env.Step(state, new EndTurnAction());
            return state;
        }

        var left = Run();
        var right = Run();
        Assert.Equal(left.State.Events.AsEnumerable(), right.State.Events.AsEnumerable());
        Assert.Equal(left.State.Player.DrawPile.AsEnumerable(), right.State.Player.DrawPile.AsEnumerable());
        Assert.Equal(left.State.Player.Creature.Hp, right.State.Player.Creature.Hp);
    }

    public void NamedRandomStreamsAreDeterministicAndCountCalls()
    {
        var first = new RandomStreams(42);
        var replay = new RandomStreams(42);
        var firstStream = first.Get("shuffle");
        var replayStream = replay.Get("shuffle");
        Assert.Equal(firstStream.NextInt(1000), replayStream.NextInt(1000));
        Assert.Equal(firstStream.NextInt(1000), replayStream.NextInt(1000));
        Assert.Equal(2, firstStream.Counter);
        Assert.Equal(0, first.Get("monster-ai").Counter);
    }

    public void RandomNumbersMatchRecoveredMegaRandomSequence()
    {
        var random = new SeededRandom(0);
        Assert.Equal(new[] { 601, 747, 103, 416 }.AsEnumerable(), Enumerable.Range(0, 4).Select(_ => random.NextInt(1000)));
        var gaussian = new SeededRandom(0);
        Assert.Equal(new[] { 13, 12, 12, 12, 13, 12, 12, 11 }.AsEnumerable(),
            Enumerable.Range(0, 8).Select(_ => gaussian.NextGaussianInt(12, 1, 10, 14)));
    }

    public void JsonEffectDslBuildsTypedRuntimeTree()
    {
        const string source = """{"op":"sequence","effects":[{"op":"attack","amount":8},{"op":"applyPower","power":"base-spire:vulnerable","amount":2,"target":"firstEnemy"}]}""";
        var parsed = EffectDsl.Parse(source);
        var roundTrip = EffectDsl.Parse(EffectDsl.Serialize(parsed));
        Assert.True(parsed is SequenceEffect);
        Assert.True(roundTrip is SequenceEffect { Effects.Count: 2 });
        Assert.True(((SequenceEffect)roundTrip).Effects[1] is ApplyPowerEffect { Power.Value: "base-spire:vulnerable", Amount: 2 });
    }

    public void DuplicateContentIdRequiresExplicitOverride()
    {
        var registry = new ContentRegistry();
        registry.AddPackage(new SyntheticCombatPackage());
        Assert.Throws<InvalidOperationException>(() => registry.AddPackage(new DuplicateCardPackage()));
    }

    public void FailedPackageRegistrationRollsBackPartialChanges()
    {
        var registry = new ContentRegistry();
        registry.AddPackage(new SyntheticCombatPackage());
        Assert.Throws<InvalidOperationException>(() => registry.AddPackage(new PartialFailurePackage()));
        var definition = registry.Build();
        Assert.False(definition.Cards.ContainsKey(ContentId.Parse("partial-mod:orphan")));
        Assert.False(definition.Characters.ContainsKey(ContentId.Parse("partial-mod:character")));
        Assert.False(definition.Runs.ContainsKey(ContentId.Parse("partial-mod:run")));
        Assert.False(definition.RunEvents.ContainsKey(ContentId.Parse("partial-mod:event")));
        Assert.False(definition.RunHooks.Any(hook => hook.Id == ContentId.Parse("test-fixture:counting-run-hook")));
    }

    public void IndependentModCanOverrideContentAndObserveHooks()
    {
        var registry = new ContentRegistry();
        registry.AddPackage(new BaseSpirePackage());
        var mod = new TrainingModPackage();
        registry.AddPackage(mod);
        var definition = registry.Build();
        var env = new CombatEnvironment(definition);
        var state = env.Start(9, "ironclad", 56, 65, [BaseSpireIds.StrikeIronclad], BaseSpireIds.DampCultist.ToString(), enemyHp: 53);

        env.Step(state, new PlayCardAction(0, state.State.Enemy.Id));

        Assert.Equal(46, state.State.Enemy.Hp);
        Assert.Equal(1, state.State.GetModInt("training-mod", "play-count", 0));
        Assert.True(definition.Version.Contains("training-mod@0.1.0", StringComparison.Ordinal));

        var practice = env.Start(9, "ironclad", 56, 65,
            [ContentId.Parse("training-mod:practice-card"), ContentId.Parse("training-mod:practice-card")],
            BaseSpireIds.DampCultist.ToString(), enemyHp: 53);
        env.Step(practice, new PlayCardAction(0, practice.State.Player.Id));
        Assert.Equal(2, practice.State.Player.Creature.Block);
        env.Step(practice, new PlayCardAction(0, practice.State.Player.Id));
        Assert.Equal(2, practice.State.GetModInt("training-mod", "practice-card-uses", 0));
        Assert.Equal(2, practice.State.GetModInt("training-mod", "play-count", 0));
        var freshPractice = env.Start(9, "ironclad", 56, 65, [ContentId.Parse("training-mod:practice-card")], BaseSpireIds.DampCultist.ToString(), enemyHp: 53);
        env.Step(freshPractice, new PlayCardAction(0, freshPractice.State.Player.Id));
        Assert.Equal(1, freshPractice.State.GetModInt("training-mod", "practice-card-uses", 0));
        Assert.Equal(1, freshPractice.State.GetModInt("training-mod", "play-count", 0));
    }

    public void LegalActionsRejectUnaffordableCardAndInvalidHandIndex()
    {
        var env = new CombatEnvironment(Build(new SyntheticCombatPackage()));
        var state = env.Start(7, "ironclad", 56, 65,
            [ContentId.Parse("test-fixture:bash"), ContentId.Parse("test-fixture:bash")], "test-fixture:cultist");
        Assert.Equal(3, env.LegalActions(state).Count);
        env.Step(state, new PlayCardAction(0, state.State.Enemy.Id));
        Assert.Equal(1, env.LegalActions(state).Count);
        Assert.Throws<InvalidOperationException>(() => env.Step(state, new PlayCardAction(0, state.State.Enemy.Id)));

        var emptyHand = env.Start(7, "ironclad", 56, 65, [], "test-fixture:cultist");
        Assert.Throws<InvalidOperationException>(() => env.Step(emptyHand, new PlayCardAction(0, emptyHand.State.Enemy.Id)));
    }

    public void ChoicePausesAndResumesTheRemainingEffectSequence()
    {
        var registry = new ContentRegistry();
        registry.AddPackage(new SyntheticCombatPackage());
        registry.AddPackage(new ChoiceCardPackage());
        var env = new CombatEnvironment(registry.Build());
        var state = env.Start(8, "ironclad", 56, 65, [ContentId.Parse("test-fixture:choose-block")], "test-fixture:cultist");

        var paused = env.Step(state, new PlayCardAction(0, state.State.Enemy.Id));
        Assert.True(paused.NeedsChoice);
        Assert.Equal(0, state.State.Player.Creature.Block);
        Assert.Equal("Choose", paused.Observation.PendingChoice!.Prompt);
        Assert.Equal(2, paused.Observation.PendingChoice.Options.Count);
        Assert.Equal(2, env.LegalActions(state).Count);

        var resumed = env.Step(state, new ChooseOptionAction(1));
        Assert.False(resumed.NeedsChoice);
        Assert.Equal(7, state.State.Player.Creature.Block);
    }

    public void ChoiceDuringEnemyTurnResumesTheTurnScheduler()
    {
        var env = new CombatEnvironment(Build(new EnemyChoicePackage()));
        var state = env.Start(10, "ironclad", 56, 65, [], "test-fixture:choosing-cultist");

        var paused = env.Step(state, new EndTurnAction());
        Assert.True(paused.NeedsChoice);
        Assert.Equal(1, paused.Observation.Turn);
        Assert.Equal(1, env.LegalActions(state).Count);

        env.Step(state, new ChooseOptionAction(0));
        Assert.Equal(2, env.Observe(state).Turn);
        Assert.Equal(5, state.State.Enemy.Block);
        Assert.Equal(53, state.State.Player.Creature.Hp);
        Assert.True(env.LegalActions(state).Count > 0);
    }

    public void SinglePlayerRunTraversesActsAndStopsAtVictory()
    {
        var definition = BuildRunDefinition();
        var registry = new ContentRegistry();
        registry.AddPackage(new BaseSpirePackage());
        registry.AddPackage(new TestRunPackage(definition));
        var environment = new RunEnvironment(registry.Build(), definition.Id, BaseSpireIds.Ironclad);
        var session = environment.Start(17);

        var started = environment.Observe(session);
        Assert.Equal(RunStatus.InProgress, started.Status);
        Assert.Equal(80, started.Hp);
        Assert.Equal(99, started.Gold);
        Assert.Equal(10, started.Deck.Count);
        Assert.Equal(10, started.DeckInstances.Select(card => card.InstanceId).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(BaseSpireIds.BurningBlood, started.Relics.Single());
        Assert.Equal(2, environment.LegalActions(session).Count);
        Assert.Throws<InvalidOperationException>(() => environment.Step(session, new ChooseMapPointAction("boss")));

        environment.Step(session, new ChooseMapPointAction("monster"));
        Assert.Equal(RunPhase.ResolveRoom, environment.Observe(session).Phase);
        Assert.Equal(0, environment.LegalActions(session).Count);
        var afterCombat = new RunRoomResult(RunOutcome.RoomCleared, 73, 80, 120,
            started.Deck, started.Relics, [ContentId.Parse("base-spire:fire-potion")], started.DeckInstances);
        environment.CompleteRoom(session, afterCombat);
        var afterCombatObservation = environment.Observe(session);
        Assert.Equal(79, afterCombatObservation.Hp);
        Assert.Equal(120, afterCombatObservation.Gold);
        Assert.Equal(ContentId.Parse("base-spire:fire-potion"), afterCombatObservation.Potions.Single());
        Assert.Equal(RunPhase.ChooseMapPoint, environment.Observe(session).Phase);

        environment.Step(session, new ChooseMapPointAction("boss"));
        environment.CompleteRoom(session, RunResult(RunOutcome.RoomCleared, environment.Observe(session)));
        Assert.Equal(80, environment.Observe(session).Hp);
        Assert.Equal(1, environment.Observe(session).ActIndex);
        Assert.Equal(RunStatus.InProgress, environment.Observe(session).Status);

        environment.Step(session, new ChooseMapPointAction("monster"));
        environment.CompleteRoom(session, RunResult(RunOutcome.RoomCleared, environment.Observe(session)));
        environment.Step(session, new ChooseMapPointAction("boss"));
        var finalEvent = environment.CompleteRoom(session, RunResult(RunOutcome.RoomCleared, environment.Observe(session)));
        Assert.Equal(RunPhase.ResolveFinalEvent, finalEvent.Phase);
        Assert.Equal(RunStatus.InProgress, finalEvent.Status);
        environment.PushSubRoom(session, RunRoomType.Monster);
        Assert.Throws<InvalidOperationException>(() => environment.CompleteRoom(session,
            RunResult(RunOutcome.RunWon, environment.Observe(session))));
        var returnedToArchitect = environment.CompleteRoom(session,
            RunResult(RunOutcome.RoomCleared, environment.Observe(session)));
        Assert.Equal(RunPhase.ResolveFinalEvent, returnedToArchitect.Phase);
        Assert.Equal(RunRoomType.Event, returnedToArchitect.CurrentRoom!.Value);
        var won = environment.CompleteRoom(session, RunResult(RunOutcome.RunWon, environment.Observe(session)));
        Assert.Equal(RunStatus.Won, won.Status);
        Assert.Equal(RunPhase.Terminal, won.Phase);
        Assert.Equal(10, won.Deck.Count);
        Assert.Equal(BaseSpireIds.BurningBlood, won.Relics.Single());
        Assert.Equal(ContentId.Parse("base-spire:fire-potion"), won.Potions.Single());
    }

    public void SinglePlayerRunDefeatIsTerminal()
    {
        var hook = new CountingRunHook();
        var environment = new RunEnvironment(BuildRunDefinition(),
            new CharacterDefinition(ContentId.Parse("base-spire:ironclad"), 80, 99, BaseSpireContent.IroncladStarterDeck()), [hook]);
        var session = environment.Start(18);
        environment.Step(session, new ChooseMapPointAction("monster"));
        var beforeDeath = environment.Observe(session);
        var lost = environment.CompleteRoom(session, new RunRoomResult(RunOutcome.PlayerDefeated, 0,
            beforeDeath.MaxHp, beforeDeath.Gold, beforeDeath.Deck, beforeDeath.Relics, beforeDeath.Potions));
        Assert.Equal(RunStatus.Lost, lost.Status);
        Assert.Equal(0, lost.Hp);
        Assert.Equal(0, hook.InvocationCount);
        Assert.Equal(0, environment.LegalActions(session).Count);
        Assert.Throws<InvalidOperationException>(() => environment.CompleteRoom(session, RunResult(RunOutcome.RoomCleared, lost)));
    }

    public void BludgeonAndDemonFormUseRegisteredCardAndPlayerTurnStartRules()
    {
        var env = new CombatEnvironment(Build(new BaseSpirePackage()));
        var bludgeon = env.Start(31, "ironclad", 56, 65, [BaseSpireIds.Bludgeon],
            BaseSpireIds.DampCultist.ToString(), enemyHp: 53);
        env.Step(bludgeon, new PlayCardAction(0, bludgeon.State.Enemy.Id));
        Assert.Equal(21, bludgeon.State.Enemy.Hp);

        var demonForm = env.Start(32, "ironclad", 56, 65, [BaseSpireIds.DemonForm],
            BaseSpireIds.DampCultist.ToString(),
            drawPile: [BaseSpireIds.StrikeIronclad, BaseSpireIds.StrikeIronclad,
                BaseSpireIds.StrikeIronclad, BaseSpireIds.StrikeIronclad, BaseSpireIds.StrikeIronclad]);
        env.Step(demonForm, new PlayCardAction(0, demonForm.State.Player.Id));
        Assert.Equal(2, demonForm.State.Player.Creature.PowerAmount(BaseSpireIds.DemonFormPower));
        Assert.Equal(0, demonForm.State.Player.Creature.Strength);
        env.Step(demonForm, new EndTurnAction());
        Assert.Equal(2, demonForm.State.Player.Creature.Strength);
        Assert.Equal(5, demonForm.State.Player.Hand.Count);
        env.Step(demonForm, new EndTurnAction());
        Assert.Equal(4, demonForm.State.Player.Creature.Strength);
    }

    public void AngerCreatesAnIndependentCopyAndPreservesPlayedUpgradeLevel()
    {
        var env = new CombatEnvironment(Build(new BaseSpirePackage()));
        var basic = env.Start(33, "ironclad", 56, 65, [BaseSpireIds.Anger],
            BaseSpireIds.DampCultist.ToString(), enemyHp: 53);
        var originalBasicId = basic.State.Player.HandInstances[0].InstanceId;
        env.Step(basic, new PlayCardAction(0, basic.State.Enemy.Id));
        Assert.Equal(47, basic.State.Enemy.Hp);
        var basicCopy = basic.State.Player.DiscardPileInstances.Single();
        Assert.Equal(BaseSpireIds.Anger, basicCopy.CardId);
        Assert.Equal(0, basicCopy.UpgradeLevel);
        Assert.True(originalBasicId != basicCopy.InstanceId);

        var upgraded = env.Start(34, "ironclad", 56, 65, [BaseSpireIds.Anger],
            BaseSpireIds.DampCultist.ToString(), enemyHp: 53);
        upgraded.State.Player.MutableHand[0] = new CardInstance("fixture-upgraded-anger",
            BaseSpireIds.Anger, upgradeLevel: 1);
        env.Step(upgraded, new PlayCardAction(0, upgraded.State.Enemy.Id));
        Assert.Equal(45, upgraded.State.Enemy.Hp);
        var upgradedCopy = upgraded.State.Player.DiscardPileInstances.Single();
        Assert.Equal(1, upgradedCopy.UpgradeLevel);
        Assert.True("fixture-upgraded-anger" != upgradedCopy.InstanceId);
    }

    public void HeadbuttPausesForAnExactCardInstanceAndMovesItToDrawPileTop()
    {
        var env = new CombatEnvironment(Build(new BaseSpirePackage()));
        var state = env.Start(35, "ironclad", 56, 65, [BaseSpireIds.Headbutt],
            BaseSpireIds.DampCultist.ToString(), enemyHp: 53);
        var strike = new CardInstance("fixture-discard-strike", BaseSpireIds.StrikeIronclad);
        var defend = new CardInstance("fixture-discard-defend", BaseSpireIds.DefendIronclad);
        state.State.Player.MutableDiscardPile.AddRange([strike, defend]);

        var paused = env.Step(state, new PlayCardAction(0, state.State.Enemy.Id));
        Assert.Equal(44, state.State.Enemy.Hp);
        Assert.Equal("Choose a card from discard", paused.Observation.PendingCardChoice!.Prompt);
        Assert.Equal(new[] { strike.InstanceId, defend.InstanceId },
            paused.Observation.PendingCardChoice.Options.Select(card => card.InstanceId));
        var cardChoices = env.LegalActions(state).OfType<ChooseCardAction>().ToArray();
        Assert.Equal(2, cardChoices.Length);
        Assert.True(cardChoices.Any(choice => choice.CardInstanceId == defend.InstanceId));

        var resolved = env.Step(state, new ChooseCardAction(defend.InstanceId));
        Assert.True(resolved.Observation.PendingCardChoice is null);
        Assert.Equal(defend.InstanceId, state.State.Player.DrawPileInstances[0].InstanceId);
        Assert.Equal(strike.InstanceId, state.State.Player.DiscardPileInstances.Single().InstanceId);
        Assert.True(resolved.Events.Any(gameEvent => gameEvent.Type == "card_choice"
            && gameEvent.Target == defend.InstanceId));

        var continuationCard = ContentId.Parse("test:card-choice-continuation");
        var registry = new ContentRegistry();
        registry.AddPackage(new BaseSpirePackage());
        registry.Register(new CardDefinition(continuationCard, 1, CardType.Skill,
            new SequenceEffect([
                new ChooseCardFromPileEffect(CardPile.Discard, CardPile.Hand, "Choose a card"),
                new GainBlockEffect(4)
            ]), Target: TargetSelector.Self));
        var continuationEnvironment = new CombatEnvironment(registry.Build());
        var continuation = continuationEnvironment.Start(38, "ironclad", 56, 65, [continuationCard],
            BaseSpireIds.DampCultist.ToString());
        var continuationCardInstance = new CardInstance("fixture-followup-choice", BaseSpireIds.StrikeIronclad);
        continuation.State.Player.MutableDiscardPile.Add(continuationCardInstance);
        var continuationPaused = continuationEnvironment.Step(continuation,
            new PlayCardAction(0, continuation.State.Player.Id));
        Assert.True(continuationPaused.NeedsChoice);
        Assert.Equal(0, continuation.State.Player.Creature.Block);
        continuationEnvironment.Step(continuation, new ChooseCardAction(continuationCardInstance.InstanceId));
        Assert.Equal(4, continuation.State.Player.Creature.Block);

        var lethal = env.Start(37, "ironclad", 56, 65, [BaseSpireIds.Headbutt],
            BaseSpireIds.DampCultist.ToString(), enemyHp: 9);
        var lethalChoice = new CardInstance("fixture-lethal-choice", BaseSpireIds.StrikeIronclad);
        lethal.State.Player.MutableDiscardPile.Add(lethalChoice);
        var lethalPaused = env.Step(lethal, new PlayCardAction(0, lethal.State.Enemy.Id));
        Assert.False(lethalPaused.Observation.IsTerminal);
        Assert.Equal(1, env.LegalActions(lethal).Count);
        Assert.True(env.LegalActions(lethal).Single() is ChooseCardAction);
        var lethalResolved = env.Step(lethal, new ChooseCardAction(lethalChoice.InstanceId));
        Assert.True(lethal.State.Player.DrawPileInstances.Single().InstanceId == lethalChoice.InstanceId);
        Assert.True(lethalResolved.Observation.IsTerminal);

        var emptyDiscard = env.Start(36, "ironclad", 56, 65, [BaseSpireIds.Headbutt],
            BaseSpireIds.DampCultist.ToString(), enemyHp: 53);
        var noChoice = env.Step(emptyDiscard, new PlayCardAction(0, emptyDiscard.State.Enemy.Id));
        Assert.True(noChoice.Observation.PendingCardChoice is null);
        Assert.Equal(44, emptyDiscard.State.Enemy.Hp);
    }

    public void BurningPactExhaustsAChosenHandInstanceThenDraws()
    {
        var env = new CombatEnvironment(Build(new BaseSpirePackage()));
        var state = env.Start(39, "ironclad", 56, 65,
            [BaseSpireIds.BurningPact, BaseSpireIds.Anger, BaseSpireIds.DefendIronclad],
            BaseSpireIds.DampCultist.ToString(),
            drawPile: [BaseSpireIds.StrikeIronclad, BaseSpireIds.Bash, BaseSpireIds.IronWave]);
        var chosenCard = state.State.Player.HandInstances[2];
        var pactCard = state.State.Player.HandInstances[0];
        var paused = env.Step(state, new PlayCardAction(0, state.State.Player.Id));
        Assert.True(paused.NeedsChoice);
        Assert.Equal(2, paused.Observation.PendingCardChoice!.Options.Count);
        Assert.True(paused.Observation.PendingCardChoice.Options.Any(card => card.InstanceId == chosenCard.InstanceId));
        env.Step(state, new ChooseCardAction(chosenCard.InstanceId));

        Assert.True(state.State.Player.ExhaustPileInstances.Any(card => card.InstanceId == chosenCard.InstanceId));
        Assert.True(state.State.Player.ExhaustPileInstances.Any(card => card.InstanceId == pactCard.InstanceId));
        Assert.True(state.State.Player.Hand.SequenceEqual(
            [BaseSpireIds.Anger, BaseSpireIds.StrikeIronclad, BaseSpireIds.Bash]));

        var emptyHand = env.Start(40, "ironclad", 56, 65, [BaseSpireIds.BurningPact],
            BaseSpireIds.DampCultist.ToString(),
            drawPile: [BaseSpireIds.StrikeIronclad, BaseSpireIds.Bash, BaseSpireIds.IronWave]);
        var noSelection = env.Step(emptyHand, new PlayCardAction(0, emptyHand.State.Player.Id));
        Assert.False(noSelection.NeedsChoice);
        Assert.True(emptyHand.State.Player.Hand.SequenceEqual(
            [BaseSpireIds.StrikeIronclad, BaseSpireIds.Bash]));
        Assert.Equal(BaseSpireIds.BurningPact, emptyHand.State.Player.ExhaustPile.Single());
    }

    public void ArmamentsFiltersUpgradeableCardsAndUpgradesOneOrAll()
    {
        var env = new CombatEnvironment(Build(new BaseSpirePackage()));
        var basic = env.Start(48, "ironclad", 56, 65,
            [BaseSpireIds.Armaments, BaseSpireIds.StrikeIronclad, BaseSpireIds.DefendIronclad, BaseSpireIds.Slimed],
            BaseSpireIds.DampCultist.ToString());
        var strike = basic.State.Player.HandInstances[1];
        var defend = basic.State.Player.HandInstances[2];
        var slimed = basic.State.Player.HandInstances[3];
        var paused = env.Step(basic, new PlayCardAction(0, basic.State.Player.Id));

        Assert.Equal(5, basic.State.Player.Creature.Block);
        Assert.Equal(new[] { strike.InstanceId, defend.InstanceId },
            paused.Observation.PendingCardChoice!.Options.Select(card => card.InstanceId));
        env.Step(basic, new ChooseCardAction(defend.InstanceId));
        Assert.Equal(0, strike.UpgradeLevel);
        Assert.Equal(1, basic.State.Player.HandInstances.Single(card => card.InstanceId == defend.InstanceId).UpgradeLevel);
        Assert.Equal(0, slimed.UpgradeLevel);

        var upgraded = env.Start(49, "ironclad", 56, 65,
            [BaseSpireIds.Armaments, BaseSpireIds.StrikeIronclad, BaseSpireIds.DefendIronclad, BaseSpireIds.Slimed],
            BaseSpireIds.DampCultist.ToString());
        upgraded.State.Player.MutableHand[0] = new CardInstance("fixture-armaments-upgraded",
            BaseSpireIds.Armaments, upgradeLevel: 1);
        var handCards = upgraded.State.Player.HandInstances.Skip(1).ToArray();
        var upgradedResolution = env.Step(upgraded, new PlayCardAction(0, upgraded.State.Player.Id));

        Assert.False(upgradedResolution.NeedsChoice);
        Assert.Equal(5, upgraded.State.Player.Creature.Block);
        Assert.Equal(new[] { 1, 1, 0 }, upgraded.State.Player.HandInstances.Select(card => card.UpgradeLevel));
        Assert.Contains(upgraded.State.Events, gameEvent => gameEvent.Type == "cards_upgraded" && gameEvent.Amount == 2);
        Assert.Equal(new[] { BaseSpireIds.StrikeIronclad, BaseSpireIds.DefendIronclad, BaseSpireIds.Slimed },
            handCards.Select(card => card.CardId));

        var noCandidates = env.Start(50, "ironclad", 56, 65,
            [BaseSpireIds.Armaments, BaseSpireIds.Slimed], BaseSpireIds.DampCultist.ToString());
        var noCandidateResult = env.Step(noCandidates, new PlayCardAction(0, noCandidates.State.Player.Id));
        Assert.False(noCandidateResult.NeedsChoice);
        Assert.Equal(5, noCandidates.State.Player.Creature.Block);
    }

    public void TrueGritUsesSeededRandomExhaustAndUpgradedPlayerChoice()
    {
        var env = new CombatEnvironment(Build(new BaseSpirePackage()));
        var hand = new[] { BaseSpireIds.TrueGrit, BaseSpireIds.StrikeIronclad,
            BaseSpireIds.DefendIronclad, BaseSpireIds.Slimed };
        var state = env.Start(51, "ironclad", 56, 65, hand, BaseSpireIds.DampCultist.ToString());
        var candidateIds = state.State.Player.HandInstances.Skip(1).Select(card => card.InstanceId).ToArray();
        env.Step(state, new PlayCardAction(0, state.State.Player.Id));

        Assert.Equal(7, state.State.Player.Creature.Block);
        Assert.Equal(1, state.State.Player.ExhaustPileInstances.Count);
        var randomExhausted = state.State.Player.ExhaustPileInstances.Single();
        Assert.True(candidateIds.Contains(randomExhausted.InstanceId));
        Assert.Contains(state.State.Events, gameEvent => gameEvent.Type == "random_card_choice"
            && gameEvent.Target == randomExhausted.InstanceId);
        Assert.Equal(1, state.Random.Get("card-selection").Counter);

        var replay = env.Start(51, "ironclad", 56, 65, hand, BaseSpireIds.DampCultist.ToString());
        env.Step(replay, new PlayCardAction(0, replay.State.Player.Id));
        Assert.Equal(randomExhausted.InstanceId, replay.State.Player.ExhaustPileInstances.Single().InstanceId);

        var upgraded = env.Start(52, "ironclad", 56, 65, hand, BaseSpireIds.DampCultist.ToString());
        upgraded.State.Player.MutableHand[0] = new CardInstance("fixture-true-grit-upgraded",
            BaseSpireIds.TrueGrit, upgradeLevel: 1);
        var paused = env.Step(upgraded, new PlayCardAction(0, upgraded.State.Player.Id));
        Assert.Equal(9, upgraded.State.Player.Creature.Block);
        Assert.Equal(3, paused.Observation.PendingCardChoice!.Options.Count);
        var chosen = paused.Observation.PendingCardChoice.Options.Single(card => card.CardId == BaseSpireIds.Slimed);
        env.Step(upgraded, new ChooseCardAction(chosen.InstanceId));
        Assert.Contains(upgraded.State.Player.ExhaustPileInstances, card => card.InstanceId == chosen.InstanceId);
        Assert.Equal(2, upgraded.State.Player.HandInstances.Count);

        var emptyHand = env.Start(53, "ironclad", 56, 65, [BaseSpireIds.TrueGrit], BaseSpireIds.DampCultist.ToString());
        env.Step(emptyHand, new PlayCardAction(0, emptyHand.State.Player.Id));
        Assert.Equal(7, emptyHand.State.Player.Creature.Block);
        Assert.Equal(0, emptyHand.State.Player.ExhaustPileInstances.Count);
        Assert.Equal(0, emptyHand.Random.Get("card-selection").Counter);
    }

    public void SwordBoomerangRandomizesEveryHitAndReplaysTheTargetSequence()
    {
        var env = new CombatEnvironment(Build(new BaseSpirePackage()));
        var enemies = new[]
        {
            new CombatEnemySpawn(BaseSpireIds.DampCultist.ToString(), Hp: 53, InstanceId: "first-cultist"),
            new CombatEnemySpawn(BaseSpireIds.DampCultist.ToString(), Hp: 53, InstanceId: "second-cultist")
        };
        var state = env.StartEncounter(54, "ironclad", 56, 65, [BaseSpireIds.SwordBoomerang], enemies);
        Assert.Equal(1, env.LegalActions(state).OfType<PlayCardAction>().Count());
        env.Step(state, new PlayCardAction(0));

        var targetSequence = state.State.Events.Where(gameEvent => gameEvent.Type == "random_attack_target")
            .Select(gameEvent => gameEvent.Target).ToArray();
        Assert.Equal(3, targetSequence.Length);
        Assert.True(targetSequence.All(target => enemies.Any(enemy => enemy.InstanceId == target)));
        Assert.Equal(3, state.Random.Get("card-targets").Counter);
        Assert.Equal(9, state.State.Events.Where(gameEvent => gameEvent.Type == "damage").Sum(gameEvent => gameEvent.Amount));

        var replay = env.StartEncounter(54, "ironclad", 56, 65, [BaseSpireIds.SwordBoomerang], enemies);
        env.Step(replay, new PlayCardAction(0));
        Assert.Equal(targetSequence, replay.State.Events.Where(gameEvent => gameEvent.Type == "random_attack_target")
            .Select(gameEvent => gameEvent.Target));

        var upgraded = env.StartEncounter(55, "ironclad", 56, 65, [BaseSpireIds.SwordBoomerang], enemies);
        upgraded.State.Player.MutableHand[0] = new CardInstance("fixture-sword-boomerang-upgraded",
            BaseSpireIds.SwordBoomerang, upgradeLevel: 1);
        env.Step(upgraded, new PlayCardAction(0));
        Assert.Equal(4, upgraded.State.Events.Count(gameEvent => gameEvent.Type == "random_attack_target"));
        Assert.Equal(4, upgraded.Random.Get("card-targets").Counter);
    }

    public void SecondWindProcessesEveryNonAttackHandCardInOrder()
    {
        var env = new CombatEnvironment(Build(new BaseSpirePackage()));
        var hand = new[] { BaseSpireIds.SecondWind, BaseSpireIds.StrikeIronclad,
            BaseSpireIds.DefendIronclad, BaseSpireIds.Slimed, BaseSpireIds.DemonForm };
        var state = env.Start(56, "ironclad", 56, 65, hand, BaseSpireIds.DampCultist.ToString());
        var matchingCards = state.State.Player.HandInstances.Skip(2).ToArray();
        env.Step(state, new PlayCardAction(0, state.State.Player.Id));

        Assert.Equal(15, state.State.Player.Creature.Block);
        Assert.Equal(new[] { BaseSpireIds.DefendIronclad, BaseSpireIds.Slimed, BaseSpireIds.DemonForm },
            state.State.Player.ExhaustPileInstances.Select(card => card.CardId));
        Assert.True(state.State.Player.Hand.SequenceEqual([BaseSpireIds.StrikeIronclad]));
        Assert.Equal(3, state.State.Events.Count(gameEvent => gameEvent.Type == "card_moved"));
        Assert.Equal(new[] { "card_moved", "block", "card_moved", "block", "card_moved", "block" },
            state.State.Events.Where(gameEvent => gameEvent.Type is "card_moved" or "block")
                .Select(gameEvent => gameEvent.Type));
        Assert.Equal(new[] { BaseSpireIds.DefendIronclad, BaseSpireIds.Slimed, BaseSpireIds.DemonForm },
            matchingCards.Select(card => card.CardId));

        var upgraded = env.Start(57, "ironclad", 56, 65, hand, BaseSpireIds.DampCultist.ToString());
        upgraded.State.Player.MutableHand[0] = new CardInstance("fixture-second-wind-upgraded",
            BaseSpireIds.SecondWind, upgradeLevel: 1);
        env.Step(upgraded, new PlayCardAction(0, upgraded.State.Player.Id));
        Assert.Equal(21, upgraded.State.Player.Creature.Block);
        Assert.Equal(3, upgraded.State.Player.ExhaustPileInstances.Count);

        var attacksOnly = env.Start(58, "ironclad", 56, 65,
            [BaseSpireIds.SecondWind, BaseSpireIds.StrikeIronclad], BaseSpireIds.DampCultist.ToString());
        env.Step(attacksOnly, new PlayCardAction(0, attacksOnly.State.Player.Id));
        Assert.Equal(0, attacksOnly.State.Player.Creature.Block);
        Assert.Equal(0, attacksOnly.State.Player.ExhaustPileInstances.Count);
    }

    public void BodySlamUsesCurrentBlockAsAttackDamageAndUpgradesItsCost()
    {
        var env = new CombatEnvironment(Build(new BaseSpirePackage()));
        var state = env.Start(59, "ironclad", 56, 65, [BaseSpireIds.BodySlam],
            BaseSpireIds.DampCultist.ToString(), enemyHp: 53);
        state.State.Player.Creature.Block = 8;
        state.State.Player.Creature.Strength = 2;
        env.Step(state, new PlayCardAction(0, state.State.Enemy.Id));

        Assert.Equal(43, state.State.Enemy.Hp);
        Assert.Equal(8, state.State.Player.Creature.Block);
        Assert.Equal(2, state.State.Player.Energy);

        var upgraded = env.Start(60, "ironclad", 56, 65, [BaseSpireIds.BodySlam],
            BaseSpireIds.DampCultist.ToString(), enemyHp: 53);
        upgraded.State.Player.MutableHand[0] = new CardInstance("fixture-body-slam-upgraded",
            BaseSpireIds.BodySlam, upgradeLevel: 1);
        upgraded.State.Player.Creature.Block = 6;
        env.Step(upgraded, new PlayCardAction(0, upgraded.State.Enemy.Id));

        Assert.Equal(47, upgraded.State.Enemy.Hp);
        Assert.Equal(6, upgraded.State.Player.Creature.Block);
        Assert.Equal(3, upgraded.State.Player.Energy);
        Assert.Equal(0, Build(new BaseSpirePackage()).Cards[BaseSpireIds.BodySlam]
            .ResolveCost(new CardInstance("fixture-body-slam-cost", BaseSpireIds.BodySlam, upgradeLevel: 1)));
    }

    public void BattleTranceDrawsThenPreventsFurtherDrawsUntilNextTurn()
    {
        var env = new CombatEnvironment(Build(new BaseSpirePackage()));
        var drawPile = Enumerable.Repeat(BaseSpireIds.StrikeIronclad, 10).ToArray();
        var state = env.Start(61, "ironclad", 56, 65, [BaseSpireIds.BattleTrance],
            BaseSpireIds.DampCultist.ToString(), drawPile: drawPile);
        env.Step(state, new PlayCardAction(0, state.State.Player.Id));

        Assert.Equal(3, state.State.Player.HandInstances.Count);
        Assert.Equal(1, state.State.Player.Creature.PowerAmount(BaseSpireIds.NoDrawPower));
        Assert.Equal(1, env.Observe(state).PlayerPowers[BaseSpireIds.NoDrawPower]);
        state.State.Draw(state, 2);
        Assert.Equal(3, state.State.Player.HandInstances.Count);
        Assert.Equal(7, state.State.Player.DrawPileInstances.Count);

        env.Step(state, new EndTurnAction());
        Assert.Equal(0, state.State.Player.Creature.PowerAmount(BaseSpireIds.NoDrawPower));
        Assert.Equal(5, state.State.Player.HandInstances.Count);
        Assert.Equal(2, state.State.Player.DrawPileInstances.Count);

        var upgraded = env.Start(62, "ironclad", 56, 65, [BaseSpireIds.BattleTrance],
            BaseSpireIds.DampCultist.ToString(), drawPile: drawPile);
        upgraded.State.Player.MutableHand[0] = new CardInstance("fixture-battle-trance-upgraded",
            BaseSpireIds.BattleTrance, upgradeLevel: 1);
        env.Step(upgraded, new PlayCardAction(0, upgraded.State.Player.Id));
        Assert.Equal(4, upgraded.State.Player.HandInstances.Count);
        Assert.Equal(1, upgraded.State.Player.Creature.PowerAmount(BaseSpireIds.NoDrawPower));
    }

    public void FeelNoPainTriggersAfterChosenAndPlayedCardsExhaust()
    {
        var env = new CombatEnvironment(Build(new BaseSpirePackage()));
        var state = env.Start(63, "ironclad", 56, 65,
            [BaseSpireIds.FeelNoPain, BaseSpireIds.FeelNoPain, BaseSpireIds.BurningPact,
                BaseSpireIds.Anger, BaseSpireIds.DefendIronclad],
            BaseSpireIds.DampCultist.ToString(), drawPile: [BaseSpireIds.StrikeIronclad, BaseSpireIds.Bash]);
        env.Step(state, new PlayCardAction(0, state.State.Player.Id));
        env.Step(state, new PlayCardAction(0, state.State.Player.Id));
        Assert.Equal(6, state.State.Player.Creature.PowerAmount(BaseSpireIds.FeelNoPain));

        var paused = env.Step(state, new PlayCardAction(0, state.State.Player.Id));
        var chosen = paused.Observation.PendingCardChoice!.Options.Single(card => card.CardId == BaseSpireIds.Anger);
        env.Step(state, new ChooseCardAction(chosen.InstanceId));

        Assert.Equal(12, state.State.Player.Creature.Block);
        Assert.True(state.State.Player.ExhaustPileInstances.Any(card => card.InstanceId == chosen.InstanceId));
        Assert.True(state.State.Player.ExhaustPileInstances.Any(card => card.CardId == BaseSpireIds.BurningPact));
        Assert.Equal(new[] { 6, 6 }, state.State.Events.Where(gameEvent => gameEvent.Type == "block")
            .Select(gameEvent => gameEvent.Amount));
        Assert.Equal(2, state.State.Events.Count(gameEvent => gameEvent.Type == "card_exhausted"));
        Assert.True(state.State.Player.Hand.SequenceEqual([
            BaseSpireIds.DefendIronclad, BaseSpireIds.StrikeIronclad, BaseSpireIds.Bash]));
    }

    public void EtherealCardsExhaustBeforeEnemyTurnAndTriggerPowers()
    {
        var env = new CombatEnvironment(Build(new BaseSpirePackage()));
        var state = env.Start(64, "ironclad", 56, 65,
            [BaseSpireIds.FeelNoPain, BaseSpireIds.Dazed, BaseSpireIds.Dazed, BaseSpireIds.StrikeIronclad],
            BaseSpireIds.DampCultist.ToString());

        env.Step(state, new PlayCardAction(0, state.State.Player.Id));
        Assert.True(!env.LegalActions(state).Any(action => action is PlayCardAction play && play.HandIndex == 0));
        env.Step(state, new EndTurnAction());

        Assert.Equal(2, state.State.Player.ExhaustPileInstances.Count(card => card.CardId == BaseSpireIds.Dazed));
        Assert.Contains(state.State.Player.Hand, card => card == BaseSpireIds.StrikeIronclad);
        var exhaustEvents = state.State.Events.Where(gameEvent => gameEvent.Type == "card_exhausted").ToArray();
        var blockEvents = state.State.Events.Where(gameEvent => gameEvent.Type == "block" && gameEvent.Amount == 3).ToArray();
        Assert.Equal(2, exhaustEvents.Length);
        Assert.Equal(2, blockEvents.Length);
        var enemyActionEvent = state.State.Events.Last(gameEvent => gameEvent.Type == "enemy_action");
        Assert.True(exhaustEvents[0].Sequence < blockEvents[0].Sequence
            && blockEvents[0].Sequence < exhaustEvents[1].Sequence
            && exhaustEvents[1].Sequence < blockEvents[1].Sequence
            && blockEvents[1].Sequence < enemyActionEvent.Sequence);
    }

    public void RetainedCardInstancesStayInHandAcrossEnemyTurn()
    {
        var retainedId = ContentId.Parse("retain-fixture:retained");
        var plainId = ContentId.Parse("retain-fixture:plain");
        var env = new CombatEnvironment(Build(new RetainFixturePackage(retainedId, plainId)));
        var state = env.Start(65, "player", 30, 30, [retainedId, plainId], "retain-fixture:enemy");
        var retainedInstanceId = state.State.Player.HandInstances[0].InstanceId;

        env.Step(state, new EndTurnAction());

        Assert.Contains(state.State.Player.HandInstances, card => card.InstanceId == retainedInstanceId);
        Assert.True(state.State.Player.DiscardPileInstances.All(card => card.InstanceId != retainedInstanceId));
        Assert.True(state.State.Player.ExhaustPileInstances.All(card => card.InstanceId != retainedInstanceId));
    }

    public void HandCleanupHooksCanSuppressEtherealAndFlush()
    {
        var registry = new ContentRegistry();
        registry.AddPackage(new BaseSpirePackage());
        registry.AddPackage(new TestHookPackage(new HandCleanupHook(BaseSpireIds.Dazed, suppressFlush: false)));
        var env = new CombatEnvironment(registry.Build());
        var state = env.Start(66, "ironclad", 56, 65, [BaseSpireIds.Dazed, BaseSpireIds.StrikeIronclad],
            BaseSpireIds.DampCultist.ToString(), drawPile: Enumerable.Repeat(BaseSpireIds.DefendIronclad, 5));

        env.Step(state, new EndTurnAction());

        Assert.Equal(0, state.State.Player.ExhaustPileInstances.Count);
        Assert.Contains(state.State.Player.DiscardPileInstances, card => card.CardId == BaseSpireIds.Dazed);

        registry = new ContentRegistry();
        registry.AddPackage(new BaseSpirePackage());
        registry.AddPackage(new TestHookPackage(new HandCleanupHook(null, suppressFlush: false,
            suppressFlushPower: BaseSpireIds.FeelNoPain)));
        env = new CombatEnvironment(registry.Build());
        state = env.Start(67, "ironclad", 56, 65,
            [BaseSpireIds.FeelNoPain, BaseSpireIds.StrikeIronclad, BaseSpireIds.DefendIronclad],
            BaseSpireIds.DampCultist.ToString());
        env.Step(state, new PlayCardAction(0, state.State.Player.Id));
        var handBefore = state.State.Player.HandInstances.Select(card => card.InstanceId).ToArray();

        env.Step(state, new EndTurnAction());

        Assert.True(handBefore.All(instanceId => state.State.Player.HandInstances.Any(card => card.InstanceId == instanceId)));
    }

    public void BloodlettingAndOfferingLoseHpWithoutConsumingBlockOrPoweringDamage()
    {
        var env = new CombatEnvironment(Build(new BaseSpirePackage()));
        var bloodletting = env.Start(41, "ironclad", 56, 65, [BaseSpireIds.Bloodletting],
            BaseSpireIds.DampCultist.ToString());
        bloodletting.State.Player.Creature.Block = 9;
        bloodletting.State.Player.Creature.Strength = 4;
        env.Step(bloodletting, new PlayCardAction(0, bloodletting.State.Player.Id));
        Assert.Equal(53, bloodletting.State.Player.Creature.Hp);
        Assert.Equal(9, bloodletting.State.Player.Creature.Block);
        Assert.Equal(5, bloodletting.State.Player.Energy);
        Assert.True(bloodletting.State.Events.Any(gameEvent => gameEvent.Type == "hp_loss"
            && gameEvent.Amount == 3));

        var offering = env.Start(42, "ironclad", 56, 65,
            [BaseSpireIds.Offering, BaseSpireIds.Anger], BaseSpireIds.DampCultist.ToString(),
            drawPile: [BaseSpireIds.StrikeIronclad, BaseSpireIds.Bash, BaseSpireIds.IronWave]);
        offering.State.Player.Creature.Block = 7;
        offering.State.Player.Creature.Strength = 5;
        var offeringInstance = offering.State.Player.HandInstances[0];
        env.Step(offering, new PlayCardAction(0, offering.State.Player.Id));
        Assert.Equal(50, offering.State.Player.Creature.Hp);
        Assert.Equal(7, offering.State.Player.Creature.Block);
        Assert.Equal(5, offering.State.Player.Energy);
        Assert.True(offering.State.Player.ExhaustPileInstances.Any(card => card.InstanceId == offeringInstance.InstanceId));
        Assert.True(offering.State.Player.Hand.SequenceEqual([BaseSpireIds.Anger,
            BaseSpireIds.StrikeIronclad, BaseSpireIds.Bash, BaseSpireIds.IronWave]));
    }

    public void RestSiteOffersHealingOrUpgradingOneCard()
    {
        var map = new ActMapDefinition("start", "boss",
        [
            new MapPointDefinition("start", 0, 3, MapPointType.Start, ["rest"]),
            new MapPointDefinition("rest", 1, 3, MapPointType.RestSite, ["boss"]),
            new MapPointDefinition("boss", 2, 3, MapPointType.Boss, [])
        ]);
        var act = new ActDefinition(ContentId.Parse("test-fixture:rest-site-act"), _ => map);
        var run = new RunDefinition(ContentId.Parse("test-fixture:rest-site-run"), [act]);
        var character = new CharacterDefinition(ContentId.Parse("test-fixture:rest-site-character"), 80, 0,
            [BaseSpireIds.StrikeIronclad, BaseSpireIds.DefendIronclad]);
        var cards = new[]
        {
            new CardDefinition(BaseSpireIds.StrikeIronclad, 1, CardType.Attack, new AttackEffect(6),
                UpgradedEffect: new AttackEffect(9)),
            new CardDefinition(BaseSpireIds.DefendIronclad, 1, CardType.Skill, new GainBlockEffect(5),
                UpgradedEffect: new GainBlockEffect(8))
        };
        var restSiteHook = new RestSiteTestHook();
        var environment = new RunEnvironment(run, character, [restSiteHook], cards);

        var healSession = environment.Start(501);
        healSession.State.Hp = 40;
        environment.Step(healSession, new ChooseMapPointAction("rest"));
        Assert.Equal(RunPhase.ResolveRestSite, environment.Observe(healSession).Phase);
        Assert.Equal(3, environment.LegalActions(healSession).Count);
        var healed = environment.Step(healSession, new RestSiteHealAction());
        Assert.Equal(66, healed.Hp);
        Assert.Equal(RunPhase.ChooseMapPoint, healed.Phase);
        Assert.Equal(1, restSiteHook.HealCount);

        var smithSession = environment.Start(502);
        var alreadyUpgraded = smithSession.State.Deck[0];
        smithSession.State.Deck[0] = new CardInstance(alreadyUpgraded.InstanceId, alreadyUpgraded.CardId, 1);
        var cardToUpgrade = smithSession.State.Deck[1];
        environment.Step(smithSession, new ChooseMapPointAction("rest"));
        Assert.Equal(2, environment.LegalActions(smithSession).Count);
        Assert.False(environment.LegalActions(smithSession).OfType<RestSiteUpgradeCardAction>()
            .Any(action => action.CardInstanceId == alreadyUpgraded.InstanceId));
        var upgraded = environment.Step(smithSession, new RestSiteUpgradeCardAction(cardToUpgrade.InstanceId));
        var upgradedInstance = upgraded.DeckInstances.Single(card => card.InstanceId == cardToUpgrade.InstanceId);
        Assert.Equal(1, upgradedInstance.UpgradeLevel);
        Assert.Equal(RunPhase.ChooseMapPoint, upgraded.Phase);
        Assert.Equal(1, restSiteHook.SmithCount);
    }

    public void NestedCombatReturnsToItsParentRoom()
    {
        var map = new ActMapDefinition("start", "boss",
        [
            new MapPointDefinition("start", 0, 3, MapPointType.Start, ["event"]),
            new MapPointDefinition("event", 1, 3, MapPointType.Unknown, ["boss"]),
            new MapPointDefinition("boss", 2, 3, MapPointType.Boss, [])
        ]);
        var act = new ActDefinition(ContentId.Parse("test-fixture:nested-act"), _ => map,
            (_, _) => RunRoomType.Event);
        var hook = new CountingRunHook();
        var environment = new RunEnvironment(new RunDefinition(ContentId.Parse("test-fixture:nested-run"), [act]),
            BaseSpireContent.Ironclad(), [hook]);
        var session = environment.Start(181);
        var parent = environment.Step(session, new ChooseMapPointAction("event"));
        Assert.Equal(RunRoomType.Event, parent.CurrentRoom!.Value);

        var child = environment.PushSubRoom(session, RunRoomType.Monster);
        Assert.Equal(RunRoomType.Monster, child.CurrentRoom!.Value);
        Assert.Equal(2, child.RoomStack.Count);
        Assert.True(child.VisitedMapPoints[^1].RoomTypes.SequenceEqual(
            [RunRoomType.Event, RunRoomType.Monster]));
        var returned = environment.CompleteRoom(session, RunResult(RunOutcome.RoomCleared, child));
        Assert.Equal(RunPhase.ResolveRoom, returned.Phase);
        Assert.Equal(RunRoomType.Event, returned.CurrentRoom!.Value);
        Assert.Equal(1, returned.RoomStack.Count);
        Assert.Equal(1, hook.InvocationCount);

        var route = environment.CompleteRoom(session, RunResult(RunOutcome.RoomCleared, returned));
        Assert.Equal(RunPhase.ChooseMapPoint, route.Phase);
        Assert.Equal(1, hook.InvocationCount);
    }

    public void UnknownRoomOddsExposeOrderedModHooks()
    {
        var map = new ActMapDefinition("start", "boss",
        [
            new MapPointDefinition("start", 0, 3, MapPointType.Start, ["unknown"]),
            new MapPointDefinition("unknown", 1, 3, MapPointType.Unknown, ["boss"]),
            new MapPointDefinition("boss", 2, 3, MapPointType.Boss, [])
        ]);
        var act = new ActDefinition(ContentId.Parse("test-fixture:odds-hook-act"), _ => map);
        var odds = new UnknownRoomOddsDefinition(
        [
            new UnknownRoomOddsEntry(RunRoomType.Monster, 0.1f),
            new UnknownRoomOddsEntry(RunRoomType.Elite, -1f),
            new UnknownRoomOddsEntry(RunRoomType.Treasure, 0.02f),
            new UnknownRoomOddsEntry(RunRoomType.Shop, 0.03f)
        ]);
        var hook = new UnknownRoomOddsHook();
        var environment = new RunEnvironment(new RunDefinition(ContentId.Parse("test-fixture:odds-hook-run"), [act], odds),
            BaseSpireContent.Ironclad(), [hook]);
        var session = environment.Start(182);
        var observation = environment.Step(session, new ChooseMapPointAction("unknown"));

        Assert.Equal(MapPointType.Unknown, hook.ObservedMapType!.Value);
        Assert.True(hook.AvailableTypes!.SetEquals(
            [RunRoomType.Event, RunRoomType.Monster, RunRoomType.Elite, RunRoomType.Treasure,
                RunRoomType.Shop]));
        Assert.True(observation.CurrentRoom != RunRoomType.Shop);
        Assert.True(hook.IncreaseCalls >= 2);
        Assert.True(hook.TreasureIncreaseWasModified);
    }

    public void RunCombatUsesTheRunDeckAndWritesItsOutcomeBack()
    {
        var runId = ContentId.Parse("test-fixture:combat-run");
        var characterId = ContentId.Parse("test-fixture:combat-character");
        var enemyId = ContentId.Parse("test-fixture:one-hit-enemy");
        var encounterId = ContentId.Parse("test-fixture:one-hit-encounter");
        var finalBossId = ContentId.Parse("test-fixture:final-boss-encounter");
        var finalBossEnemyId = ContentId.Parse("test-fixture:final-boss-enemy");
        var noRewardId = ContentId.Parse("test-fixture:no-reward-encounter");
        var noRewardEnemyId = ContentId.Parse("test-fixture:no-reward-enemy");
        var deck = Enumerable.Range(0, 10)
            .Select(index => ContentId.Parse($"test-fixture:run-strike-{index}"))
            .ToArray();
        var map = new ActMapDefinition("start", "boss",
        [
            new MapPointDefinition("start", 0, 3, MapPointType.Start, ["event"]),
            new MapPointDefinition("event", 1, 3, MapPointType.Unknown, ["boss"]),
            new MapPointDefinition("boss", 2, 3, MapPointType.Boss, [])
        ]);
        var runDefinition = new RunDefinition(runId,
        [
            new ActDefinition(ContentId.Parse("test-fixture:combat-act"), _ => map,
                (_, _) => RunRoomType.Event)
        ]);
        var package = new RunCombatPackage(runDefinition,
            new CharacterDefinition(characterId, 60, 50, deck),
            new EnemyDefinition(enemyId, 2, 2,
            [
                new EnemyActionDefinition(ContentId.Parse("test-fixture:idle"),
                    new AttackEffect(100), IntentKind.Attack, 100)
            ]), deck.Select(id => new CardDefinition(id, 0, CardType.Attack, new AttackEffect(1),
                UpgradedEffect: new AttackEffect(2), UpgradedCost: 1)),
            new EncounterDefinition(encounterId, RunRoomType.Monster, [enemyId]),
            [new EnemyDefinition(finalBossEnemyId, 1, 1,
                [new EnemyActionDefinition(ContentId.Parse("test-fixture:boss-idle"),
                    new AttackEffect(0), IntentKind.None)]),
             new EnemyDefinition(noRewardEnemyId, 1, 1,
                [new EnemyActionDefinition(ContentId.Parse("test-fixture:no-reward-idle"),
                    new AttackEffect(0), IntentKind.None)])],
            [new EncounterDefinition(finalBossId, RunRoomType.Boss, [finalBossEnemyId],
                minGoldReward: 5, maxGoldReward: 5),
             new EncounterDefinition(noRewardId, RunRoomType.Monster, [noRewardEnemyId], shouldGiveRewards: false)]);
        var registry = new ContentRegistry();
        registry.AddPackage(new BaseSpirePackage());
        registry.AddPackage(package);
        var definition = registry.Build();
        var runEnvironment = new RunEnvironment(definition, runId, characterId);
        var combatEnvironment = new CombatEnvironment(definition);
        uint runSeed = 183;
        while (true)
        {
            var shuffledIndexes = Enumerable.Range(0, deck.Length).ToList();
            new RandomStreams(runSeed).Get("shuffle").Shuffle(shuffledIndexes);
            if (shuffledIndexes.Take(5).Contains(0)) break;
            runSeed++;
        }
        var runSession = runEnvironment.Start(runSeed);
        var originalCard = runSession.State.Deck[0];
        var upgradedCard = new CardInstance(originalCard.InstanceId, originalCard.CardId, 1);
        runSession.State.Deck[0] = upgradedCard;
        Assert.Equal(2, ((AttackEffect)definition.Cards[upgradedCard.CardId].ResolveEffect(upgradedCard)).Amount);
        runEnvironment.Step(runSession, new ChooseMapPointAction("event"));
        runEnvironment.PushSubRoom(runSession, RunRoomType.Monster);

        var combatSession = combatEnvironment.StartEncounterForRun(runSession, encounterId);
        Assert.Equal(5, combatEnvironment.Observe(combatSession).Hand.Count);
        Assert.True(combatEnvironment.Observe(combatSession).HandInstances
            .Concat(combatEnvironment.Observe(combatSession).DrawPileInstances)
            .Any(card => card == upgradedCard));
        Assert.Equal(1, definition.Cards[upgradedCard.CardId].ResolveCost(upgradedCard));
        var upgradedHandIndex = combatEnvironment.Observe(combatSession).HandInstances
            .ToList().FindIndex(card => card.InstanceId == upgradedCard.InstanceId);
        Assert.True(upgradedHandIndex >= 0);
        Assert.Equal(5, combatEnvironment.Observe(combatSession).Hand.Distinct().Count());
        Assert.True(combatEnvironment.Observe(combatSession).Hand.All(deck.Contains));
        Assert.Throws<InvalidOperationException>(() => combatEnvironment.StartEncounterForRun(runSession, encounterId));
        Assert.Throws<InvalidOperationException>(() => runEnvironment.CompleteRoom(runSession,
            RunResult(RunOutcome.RoomCleared, runEnvironment.Observe(runSession))));
        Assert.Throws<InvalidOperationException>(() => runEnvironment.CompleteCombat(runSession, combatSession));

        var replayRun = runEnvironment.Start(runSeed);
        replayRun.State.Deck[0] = upgradedCard;
        runEnvironment.Step(replayRun, new ChooseMapPointAction("event"));
        runEnvironment.PushSubRoom(replayRun, RunRoomType.Monster);
        var replayCombat = combatEnvironment.StartEncounterForRun(replayRun, encounterId);
        Assert.True(combatEnvironment.Observe(combatSession).Hand.SequenceEqual(
            combatEnvironment.Observe(replayCombat).Hand));

        var upgradedPlay = combatEnvironment.Step(combatSession,
            new PlayCardAction(upgradedHandIndex, enemyId.ToString()));
        Assert.Contains(upgradedPlay.Events, gameEvent => gameEvent.Type == "damage" && gameEvent.Amount == 2);
        Assert.Equal(2, combatSession.State.Player.Energy);
        Assert.True(combatEnvironment.Observe(combatSession).IsTerminal);
        var completed = runEnvironment.CompleteCombat(runSession, combatSession);
        Assert.Equal(RunStatus.InProgress, completed.Status);
        Assert.Equal(RunPhase.ResolveRewards, completed.Phase);
        Assert.Equal(RunRoomType.Monster, completed.CurrentRoom!.Value);
        Assert.Equal(encounterId, completed.RoomStack[^1].EncounterId!.Value);
        var goldReward = completed.RoomStack[^1].Rewards.Single();
        Assert.Equal(RunRewardKind.Gold, goldReward.Kind);
        Assert.True(goldReward.Amount is >= 10 and <= 20);
        Assert.Equal(1, runSession.State.Random.Get("rewards").Counter);
        Assert.Equal(2, runEnvironment.LegalActions(runSession).Count);
        Assert.True(completed.VisitedMapPoints[^1].RoomTypes.SequenceEqual(
            [RunRoomType.Event, RunRoomType.Monster]));
        Assert.Equal(60, completed.Hp);
        Assert.True(deck.SequenceEqual(completed.Deck));
        Assert.Equal(upgradedCard, completed.DeckInstances.Single(card => card.InstanceId == upgradedCard.InstanceId));
        Assert.Throws<InvalidOperationException>(() => runEnvironment.CompleteCombat(runSession, combatSession));

        var claimAction = runEnvironment.LegalActions(runSession).OfType<ClaimGoldRewardAction>().Single();
        Assert.Throws<InvalidOperationException>(() => runEnvironment.Step(runSession,
            new ClaimGoldRewardAction("test-fixture:unknown-gold")));
        var claimed = runEnvironment.Step(runSession, claimAction);
        Assert.Equal(50 + goldReward.Amount, claimed.Gold);
        Assert.False(claimed.RoomStack[^1].Rewards.Any());
        Assert.Throws<InvalidOperationException>(() => runEnvironment.Step(runSession, new SkipRewardSetAction()));
        var returnedToEvent = runEnvironment.Step(runSession, new ProceedAfterRewardsAction());
        Assert.Equal(RunPhase.ResolveRoom, returnedToEvent.Phase);
        Assert.Equal(RunRoomType.Event, returnedToEvent.CurrentRoom!.Value);
        var returnedToRoute = runEnvironment.CompleteRoom(runSession,
            RunResult(RunOutcome.RoomCleared, returnedToEvent));
        Assert.Equal(RunPhase.ChooseMapPoint, returnedToRoute.Phase);

        runEnvironment.Step(runSession, new ChooseMapPointAction("boss"));
        var finalBossCombat = combatEnvironment.StartEncounterForRun(runSession, finalBossId);
        combatEnvironment.Step(finalBossCombat, new PlayCardAction(0, finalBossEnemyId.ToString()));
        var finalBossRewards = runEnvironment.CompleteCombat(runSession, finalBossCombat);
        Assert.Equal(RunPhase.ResolveRewards, finalBossRewards.Phase);
        Assert.False(finalBossRewards.RoomStack[^1].Rewards.Any());
        Assert.True(runEnvironment.LegalActions(runSession).Single() is ProceedAfterRewardsAction);
        Assert.Equal(RunPhase.ResolveFinalEvent,
            runEnvironment.Step(runSession, new ProceedAfterRewardsAction()).Phase);

        var noRewardRun = runEnvironment.Start(185);
        runEnvironment.Step(noRewardRun, new ChooseMapPointAction("event"));
        runEnvironment.PushSubRoom(noRewardRun, RunRoomType.Monster);
        var noRewardCombat = combatEnvironment.StartEncounterForRun(noRewardRun, noRewardId);
        combatEnvironment.Step(noRewardCombat, new PlayCardAction(0, noRewardEnemyId.ToString()));
        var noRewardCompletion = runEnvironment.CompleteCombat(noRewardRun, noRewardCombat);
        Assert.Equal(RunPhase.ResolveRoom, noRewardCompletion.Phase);
        Assert.Equal(RunRoomType.Event, noRewardCompletion.CurrentRoom!.Value);

        combatEnvironment.Step(replayCombat, new PlayCardAction(upgradedHandIndex, enemyId.ToString()));
        var replayReward = runEnvironment.CompleteCombat(replayRun, replayCombat);
        Assert.Equal(goldReward.Amount, replayReward.RoomStack[^1].Rewards.Single().Amount);
        Assert.Throws<InvalidOperationException>(() => runEnvironment.CompleteRoom(replayRun,
            RunResult(RunOutcome.RoomCleared, replayReward)));
        var skipped = runEnvironment.Step(replayRun, new SkipRewardSetAction());
        Assert.False(skipped.RoomStack[^1].Rewards.Any());
        Assert.Equal(1, runEnvironment.LegalActions(replayRun).Count);
        Assert.Equal(RunPhase.ResolveRoom,
            runEnvironment.Step(replayRun, new ProceedAfterRewardsAction()).Phase);

        var defeatedRun = runEnvironment.Start(184);
        runEnvironment.Step(defeatedRun, new ChooseMapPointAction("event"));
        runEnvironment.PushSubRoom(defeatedRun, RunRoomType.Monster);
        var doomedCombat = combatEnvironment.StartEncounterForRun(defeatedRun, encounterId);
        combatEnvironment.Step(doomedCombat, new EndTurnAction());
        Assert.Equal(0, combatEnvironment.Observe(doomedCombat).PlayerHp);
        var lost = runEnvironment.CompleteCombat(defeatedRun, doomedCombat);
        Assert.Equal(RunStatus.Lost, lost.Status);
        Assert.Equal(RunPhase.Terminal, lost.Phase);
        Assert.Equal(0, lost.Hp);
    }

    public void ActEncounterPoolsAreDeterministicAndSelectCombatRoomEncounters()
    {
        var points = new List<MapPointDefinition>
        {
            new("start", 0, 0, MapPointType.Start, ["monster-1"])
        };
        for (var floor = 1; floor <= 8; floor++)
            points.Add(new MapPointDefinition($"monster-{floor}", floor, 0, MapPointType.Monster,
                [floor == 8 ? "boss" : $"monster-{floor + 1}"]));
        points.Add(new MapPointDefinition("boss", 9, 0, MapPointType.Boss, []));
        var map = new ActMapDefinition("start", "boss", points);
        var weak = ContentId.Parse("pool-test:weak");
        var regular = ContentId.Parse("pool-test:regular");
        var tagged = ContentId.Parse("pool-test:tagged");
        var elite = ContentId.Parse("pool-test:elite");
        var boss = ContentId.Parse("pool-test:boss");
        var enemyId = ContentId.Parse("pool-test:enemy");
        var alternateEnemyId = ContentId.Parse("pool-test:alternate-enemy");
        var runId = ContentId.Parse("pool-test:run");
        var characterId = ContentId.Parse("pool-test:character");
        var pool = new ActEncounterPoolDefinition([weak], [regular, tagged], [elite], [boss],
            normalRoomCount: 7, weakNormalRoomCount: 2, eliteQueueCount: 3);
        var run = new RunDefinition(runId,
            [new ActDefinition(ContentId.Parse("pool-test:act"), _ => map, encounterPool: pool)]);
        var package = new EncounterPoolPackage(run, new CharacterDefinition(characterId, 60, 50, []), enemyId,
        [
            EncounterDefinition.WithEnemySlots(weak, RunRoomType.Monster,
                [EncounterEnemySlot.ChooseOne([enemyId, alternateEnemyId])], isWeak: true),
            new EncounterDefinition(regular, RunRoomType.Monster, [enemyId]),
            new EncounterDefinition(tagged, RunRoomType.Monster, [enemyId], tags: [ContentId.Parse("pool-test:group")]),
            new EncounterDefinition(elite, RunRoomType.Elite, [enemyId]),
            new EncounterDefinition(boss, RunRoomType.Boss, [enemyId])
        ], alternateEnemyId);
        var registry = new ContentRegistry();
        registry.AddPackage(package);
        var definition = registry.Build();
        var environment = new RunEnvironment(definition, runId, characterId);

        var combatRun = environment.Start(912);
        environment.Step(combatRun, new ChooseMapPointAction("monster-1"));
        var plannedEnemyId = environment.Observe(combatRun).RoomStack[^1].EnemyIds.Single();
        Assert.Equal(1, combatRun.State.Random.Get("encounter-enemies").Counter);
        var combatEnvironment = new CombatEnvironment(definition);
        Assert.Throws<InvalidOperationException>(() => combatEnvironment.StartEncounterForRun(combatRun, regular));
        var combat = combatEnvironment.StartCurrentEncounterForRun(combatRun);
        Assert.Equal(1, combatEnvironment.Observe(combat).Enemies.Count);
        Assert.Equal(plannedEnemyId.ToString(), combatEnvironment.Observe(combat).Enemies[0].Id.Split('#')[0]);
        Assert.Equal(1, combatRun.State.Random.Get("encounter-enemies").Counter);

        var eliteRun = environment.Start(912);
        environment.Step(eliteRun, new ChooseMapPointAction("monster-1"));
        var eliteChoices = new List<ContentId?>();
        for (var visit = 0; visit < 4; visit++)
        {
            environment.PushSubRoom(eliteRun, RunRoomType.Elite);
            eliteChoices.Add(environment.Observe(eliteRun).RoomStack[^1].EncounterId);
            environment.CompleteRoom(eliteRun, RunResult(RunOutcome.RoomCleared, environment.Observe(eliteRun)));
        }
        Assert.True(eliteChoices.All(id => id == elite));
        Assert.Equal(eliteChoices[0], eliteChoices[3]);

        var first = environment.Start(912);
        var replay = environment.Start(912);
        var selected = new List<ContentId?>();
        var replaySelected = new List<ContentId?>();
        for (var floor = 1; floor <= 8; floor++)
        {
            environment.Step(first, new ChooseMapPointAction($"monster-{floor}"));
            environment.Step(replay, new ChooseMapPointAction($"monster-{floor}"));
            selected.Add(environment.Observe(first).RoomStack[^1].EncounterId);
            replaySelected.Add(environment.Observe(replay).RoomStack[^1].EncounterId);
            var completed = RunResult(RunOutcome.RoomCleared, environment.Observe(first));
            environment.CompleteRoom(first, completed);
            environment.CompleteRoom(replay, RunResult(RunOutcome.RoomCleared, environment.Observe(replay)));
        }
        Assert.True(selected.SequenceEqual(replaySelected));
        Assert.True(selected.Take(2).All(id => id == weak));
        Assert.True(selected.Skip(2).Take(5).All(id => id is { } value && (value == regular || value == tagged)));
        Assert.True(selected.Skip(2).Take(4).Zip(selected.Skip(3).Take(4),
            (left, right) => left != right).All(equal => equal));
        Assert.Equal(selected[0], selected[7]);

        var noContentEnvironment = new RunEnvironment(run, BaseSpireContent.Ironclad());
        Assert.Throws<InvalidOperationException>(() => noContentEnvironment.Start(912));
    }

    public void EncounterPoolsRejectMissingOrMisclassifiedContentAtBuild()
    {
        var enemyId = ContentId.Parse("pool-validation:enemy");
        var encounterId = ContentId.Parse("pool-validation:wrong-category");
        var runId = ContentId.Parse("pool-validation:run");
        var characterId = ContentId.Parse("pool-validation:character");
        var map = new ActMapDefinition("start", "boss",
        [
            new MapPointDefinition("start", 0, 0, MapPointType.Start, ["boss"]),
            new MapPointDefinition("boss", 1, 0, MapPointType.Boss, [])
        ]);
        var pool = new ActEncounterPoolDefinition([], [encounterId], [encounterId], [encounterId],
            normalRoomCount: 1, weakNormalRoomCount: 0);
        var package = new EncounterPoolPackage(new RunDefinition(runId,
            [new ActDefinition(ContentId.Parse("pool-validation:act"), _ => map, encounterPool: pool)]),
            new CharacterDefinition(characterId, 60, 50, []), enemyId,
            [new EncounterDefinition(encounterId, RunRoomType.Monster, [enemyId])]);
        var registry = new ContentRegistry();
        registry.AddPackage(package);
        Assert.Throws<InvalidOperationException>(() => registry.Build());
    }

    public void ActRoomContentShufflesEventsBeforeSelectingTheAncientAndReplaysBySeed()
    {
        var start = new ActMapDefinition("start", "boss",
        [
            new MapPointDefinition("start", 0, 0, MapPointType.Ancient, ["event"]),
            new MapPointDefinition("event", 1, 0, MapPointType.Unknown, ["boss"]),
            new MapPointDefinition("boss", 2, 0, MapPointType.Boss, [])
        ]);
        var runId = ContentId.Parse("room-content-test:run");
        var characterId = ContentId.Parse("room-content-test:character");
        var localAncient = ContentId.Parse("room-content-test:local-ancient");
        var sharedAncientA = ContentId.Parse("room-content-test:shared-ancient-a");
        var sharedAncientB = ContentId.Parse("room-content-test:shared-ancient-b");
        var eventA = ContentId.Parse("room-content-test:event-a");
        var eventB = ContentId.Parse("room-content-test:event-b");
        var sharedEvent = ContentId.Parse("room-content-test:shared-event");
        var enemyId = ContentId.Parse("room-content-test:enemy");
        var eliteEncounter = ContentId.Parse("room-content-test:elite-encounter");
        var bossEncounter = ContentId.Parse("room-content-test:boss-encounter");
        var encounterPool = new ActEncounterPoolDefinition([], [], [eliteEncounter], [bossEncounter],
            normalRoomCount: 0, weakNormalRoomCount: 0);
        var run = new RunDefinition(runId,
            [new ActDefinition(ContentId.Parse("room-content-test:act"), _ => start,
                resolveUnknownRoom: (_, _) => RunRoomType.Event,
                encounterPool: encounterPool, eventIds: [eventA, eventB], ancientIds: [localAncient])],
            sharedEventIds: [sharedEvent], sharedAncientIds: [sharedAncientA, sharedAncientB]);
        var events = new[]
        {
            new RunEventDefinition(localAncient, RunEventKind.Ancient),
            new RunEventDefinition(sharedAncientA, RunEventKind.Ancient),
            new RunEventDefinition(sharedAncientB, RunEventKind.Ancient),
            new RunEventDefinition(eventA),
            new RunEventDefinition(eventB),
            new RunEventDefinition(sharedEvent)
        };
        var registry = new ContentRegistry();
        registry.AddPackage(new RoomContentPackage(run, new CharacterDefinition(characterId, 60, 50, []), events,
            enemyId,
            [
                new EncounterDefinition(eliteEncounter, RunRoomType.Elite, [enemyId]),
                new EncounterDefinition(bossEncounter, RunRoomType.Boss, [enemyId])
            ]));
        var definition = registry.Build();
        var environment = new RunEnvironment(definition, runId, characterId);
        var first = environment.Start(531);
        var replay = environment.Start(531);
        Assert.Equal(20, first.State.Random.Get("up_front").Counter);
        var ancientId = environment.Observe(first).RoomStack[^1].EventId;
        Assert.Equal(ancientId, environment.Observe(replay).RoomStack[^1].EventId);
        Assert.Equal(localAncient, ancientId!.Value);

        foreach (var session in new[] { first, replay })
            environment.CompleteRoom(session, RunResult(RunOutcome.RoomCleared, environment.Observe(session)));
        environment.Step(first, new ChooseMapPointAction("event"));
        environment.Step(replay, new ChooseMapPointAction("event"));
        var selected = new List<ContentId?> { environment.Observe(first).RoomStack[^1].EventId };
        var replaySelected = new List<ContentId?> { environment.Observe(replay).RoomStack[^1].EventId };
        for (var i = 0; i < 3; i++)
        {
            environment.PushSubRoom(first, RunRoomType.Event);
            environment.PushSubRoom(replay, RunRoomType.Event);
            selected.Add(environment.Observe(first).RoomStack[^1].EventId);
            replaySelected.Add(environment.Observe(replay).RoomStack[^1].EventId);
            if (i < 2)
            {
                environment.CompleteRoom(first, RunResult(RunOutcome.RoomCleared, environment.Observe(first)));
                environment.CompleteRoom(replay, RunResult(RunOutcome.RoomCleared, environment.Observe(replay)));
            }
        }
        Assert.True(selected.SequenceEqual(replaySelected));
        Assert.Equal(3, selected.Where(id => id is not null).Distinct().Count());
        Assert.Equal(selected[0], selected[3]);
        Assert.True(selected.All(id => id is { } value && (value == eventA || value == eventB || value == sharedEvent)));
    }

    public void ActRoomPoolsRejectEventIdsWithTheWrongKind()
    {
        var id = ContentId.Parse("room-content-validation:event");
        var map = new ActMapDefinition("start", "boss",
        [
            new MapPointDefinition("start", 0, 0, MapPointType.Start, ["boss"]),
            new MapPointDefinition("boss", 1, 0, MapPointType.Boss, [])
        ]);
        var run = new RunDefinition(ContentId.Parse("room-content-validation:run"),
        [
            new ActDefinition(ContentId.Parse("room-content-validation:act"), _ => map, ancientIds: [id])
        ]);
        var package = new RoomContentPackage(run,
            new CharacterDefinition(ContentId.Parse("room-content-validation:character"), 60, 50, []),
            [new RunEventDefinition(id, RunEventKind.Event)]);
        var registry = new ContentRegistry();
        registry.AddPackage(package);
        Assert.Throws<InvalidOperationException>(() => registry.Build());
    }

    public void SharedAncientDistributionConsumesOneDrawForEveryActAfterTheFirst()
    {
        var map = new ActMapDefinition("start", "boss",
        [
            new MapPointDefinition("start", 0, 0, MapPointType.Start, ["boss"]),
            new MapPointDefinition("boss", 1, 0, MapPointType.Boss, [])
        ]);
        var run = new RunDefinition(ContentId.Parse("shared-ancient-test:run"),
        [
            new ActDefinition(ContentId.Parse("shared-ancient-test:act-1"), _ => map),
            new ActDefinition(ContentId.Parse("shared-ancient-test:act-2"), _ => map),
            new ActDefinition(ContentId.Parse("shared-ancient-test:act-3"), _ => map)
        ]);
        var environment = new RunEnvironment(run, BaseSpireContent.Ironclad());
        var session = environment.Start(331);

        Assert.Equal(2, session.State.Random.Get("up_front").Counter);
    }

    public void CombatEncounterSupportsMultipleEnemiesAndSelectedTargets()
    {
        var cardId = ContentId.Parse("test-fixture:focused-strike");
        var firstEnemyId = ContentId.Parse("test-fixture:first-foe");
        var secondEnemyId = ContentId.Parse("test-fixture:second-foe");
        var package = new MultiEnemyPackage(
            new CardDefinition(cardId, 1, CardType.Attack,
                new AttackEffect(9, TargetSelector.SelectedTarget), Target: TargetSelector.SelectedTarget),
            new EnemyDefinition(firstEnemyId, 10, 10,
            [
                new EnemyActionDefinition(ContentId.Parse("test-fixture:first-hit"),
                    new AttackEffect(3), IntentKind.Attack, 3)
            ]),
            new EnemyDefinition(secondEnemyId, 10, 10,
            [
                new EnemyActionDefinition(ContentId.Parse("test-fixture:second-hit"),
                    new AttackEffect(4), IntentKind.Attack, 4)
            ]));
        var definition = Build(package);
        var environment = new CombatEnvironment(definition);
        var session = environment.StartEncounter(185, "ironclad", 20, 20, [cardId],
        [
            new CombatEnemySpawn(firstEnemyId.ToString()),
            new CombatEnemySpawn(secondEnemyId.ToString())
        ]);
        var initial = environment.Observe(session);
        Assert.Equal(2, initial.Enemies.Count);
        Assert.Equal($"{firstEnemyId}#1", initial.Enemies[0].Id);
        Assert.Equal($"{secondEnemyId}#2", initial.Enemies[1].Id);
        var targetChoices = environment.LegalActions(session).OfType<PlayCardAction>().ToArray();
        Assert.True(targetChoices.Select(action => action.TargetId).ToHashSet(StringComparer.Ordinal)
            .SetEquals([$"{firstEnemyId}#1", $"{secondEnemyId}#2"]));

        environment.Step(session, new PlayCardAction(0, $"{secondEnemyId}#2"));
        var afterTargetedAttack = environment.Observe(session);
        Assert.Equal(10, afterTargetedAttack.Enemies[0].Hp);
        Assert.Equal(1, afterTargetedAttack.Enemies[1].Hp);
        var enemyTurn = environment.Step(session, new EndTurnAction());
        Assert.Equal(13, enemyTurn.Observation.PlayerHp);
        Assert.Equal(2, enemyTurn.Events.Count(gameEvent => gameEvent.Type == "enemy_action"));
        Assert.Equal(2, enemyTurn.Observation.Turn);

        var lethalSession = environment.StartEncounter(188, "ironclad", 3, 20, [],
        [
            new CombatEnemySpawn(firstEnemyId.ToString()),
            new CombatEnemySpawn(secondEnemyId.ToString())
        ]);
        var lethalTurn = environment.Step(lethalSession, new EndTurnAction());
        Assert.Equal(0, lethalTurn.Observation.PlayerHp);
        Assert.Equal(1, lethalTurn.Events.Count(gameEvent => gameEvent.Type == "enemy_action"));

        var areaCardId = ContentId.Parse("test-fixture:all-opponents-strike");
        var vulnerableId = ContentId.Parse("test-fixture:area-vulnerable");
        var areaPackage = new AllOpponentsPackage(
            new CardDefinition(areaCardId, 1, CardType.Attack,
                new SequenceEffect([new AttackEffect(4, TargetSelector.AllOpponents),
                    new ApplyPowerEffect(vulnerableId, 1, TargetSelector.AllOpponents)]),
                Target: TargetSelector.AllOpponents),
            new PowerDefinition(vulnerableId, Role: PowerRole.Vulnerable),
            new EnemyDefinition(firstEnemyId, 10, 10,
                [new EnemyActionDefinition(ContentId.Parse("test-fixture:first-hit-area"),
                    new AttackEffect(3), IntentKind.Attack, 3)]),
            new EnemyDefinition(secondEnemyId, 10, 10,
                [new EnemyActionDefinition(ContentId.Parse("test-fixture:second-hit-area"),
                    new AttackEffect(4), IntentKind.Attack, 4)]));
        var areaEnvironment = new CombatEnvironment(Build(areaPackage));
        var areaSession = areaEnvironment.StartEncounter(189, "ironclad", 20, 20, [areaCardId],
        [
            new CombatEnemySpawn(firstEnemyId.ToString()),
            new CombatEnemySpawn(secondEnemyId.ToString())
        ]);
        var areaAction = areaEnvironment.LegalActions(areaSession).OfType<PlayCardAction>().Single();
        Assert.True(areaAction.TargetId is null);
        areaEnvironment.Step(areaSession, areaAction);
        Assert.Equal(6, areaSession.State.Enemies[0].Creature.Hp);
        Assert.Equal(6, areaSession.State.Enemies[1].Creature.Hp);
        Assert.Equal(1, areaSession.State.Enemies[0].Creature.PowerAmount(vulnerableId));
        Assert.Equal(1, areaSession.State.Enemies[1].Creature.PowerAmount(vulnerableId));
    }

    public void UnknownMapPointKeepsMapTypeSeparateFromResolvedRoomType()
    {
        var map = new ActMapDefinition("start", "boss",
        [
            new MapPointDefinition("start", 0, 3, MapPointType.Start, ["unknown"]),
            new MapPointDefinition("unknown", 1, 3, MapPointType.Unknown, ["boss"]),
            new MapPointDefinition("boss", 2, 3, MapPointType.Boss, [])
        ]);
        var act = new ActDefinition(ContentId.Parse("test-fixture:act"), _ => map, (_, _) => RunRoomType.Event);
        var environment = new RunEnvironment(new RunDefinition(ContentId.Parse("test-fixture:run"), [act]),
            BaseSpireContent.Ironclad());

        var session = environment.Start(19);
        var observation = environment.Step(session, new ChooseMapPointAction("unknown"));
        Assert.Equal(MapPointType.Unknown, observation.CurrentMapPointType!.Value);
        Assert.Equal(RunRoomType.Event, observation.CurrentRoom!.Value);
    }

    public void RunMapGenerationReplaysFromTheSessionSeed()
    {
        var act = new ActDefinition(ContentId.Parse("test-fixture:random-act"), rng =>
        {
            var selectedType = rng.NextInt(2) == 0 ? MapPointType.Monster : MapPointType.Elite;
            return new ActMapDefinition("start", "boss",
            [
                new MapPointDefinition("start", 0, 3, MapPointType.Start, ["route"]),
                new MapPointDefinition("route", 1, 3, selectedType, ["boss"]),
                new MapPointDefinition("boss", 2, 3, MapPointType.Boss, [])
            ]);
        });
        var run = new RunDefinition(ContentId.Parse("test-fixture:seeded-run"), [act]);
        var environment = new RunEnvironment(run, BaseSpireContent.Ironclad());

        var first = environment.Observe(environment.Start(2026));
        var replay = environment.Observe(environment.Start(2026));
        Assert.Equal(first.Map.Select(point => (point.Id, point.Type)), replay.Map.Select(point => (point.Id, point.Type)));
    }

    public void BaseSpireActMapsAreDeterministicAndHonorTheSourceLayout()
    {
        Func<SeededRandom, ActMapDefinition>[] generators =
        [
            BaseSpireMapGenerator.GenerateOvergrowth,
            BaseSpireMapGenerator.GenerateHive,
            BaseSpireMapGenerator.GenerateGlory,
            BaseSpireMapGenerator.GenerateUnderdocks
        ];
        foreach (var generate in generators)
        foreach (var seed in new uint[] { 0, 1, 2, 7, 42, 99, 2026 })
        {
            var first = generate(new SeededRandom(seed));
            var replay = generate(new SeededRandom(seed));
            Assert.Equal(first.Points.OrderBy(point => point.Key).Select(point =>
                    $"{point.Key}:{point.Value.Floor}:{point.Value.Column}:{point.Value.Type}:{string.Join(',', point.Value.Children)}"),
                replay.Points.OrderBy(point => point.Key).Select(point =>
                    $"{point.Key}:{point.Value.Floor}:{point.Value.Column}:{point.Value.Type}:{string.Join(',', point.Value.Children)}"));
            Assert.Equal(MapPointType.Ancient, first.Points[first.StartPointId].Type);
            Assert.Equal(MapPointType.Boss, first.Points[first.BossPointId].Type);
            Assert.Equal(0, first.Points[first.StartPointId].Floor);
            Assert.True(first.Points[first.StartPointId].Children.Count > 0);
            Assert.True(first.Points.Values.Where(point => point.Floor == 1).All(point => point.Type == MapPointType.Monster));
            Assert.True(first.Points.Values.Where(point => point.Floor == first.Points[first.BossPointId].Floor - 1)
                .All(point => point.Type == MapPointType.RestSite));
            Assert.True(first.Points.Values.All(point => point.Column is >= 0 and < 7));
            Assert.True(first.Points.Values.Count(point => point.Type == MapPointType.Elite) is > 0 and <= 5);
            Assert.True(first.Points.Values.Count(point => point.Type == MapPointType.Shop) is > 0 and <= 3);
            Assert.True(first.Points.Values.Count(point => point.Type == MapPointType.Treasure) > 0);
        }
    }

    public void AncientEntryIsAnInitialEventBeforeTheFirstRouteChoice()
    {
        var map = new ActMapDefinition("ancient", "boss",
        [
            new MapPointDefinition("ancient", 0, 3, MapPointType.Ancient, ["monster"]),
            new MapPointDefinition("monster", 1, 3, MapPointType.Monster, ["boss"]),
            new MapPointDefinition("boss", 2, 3, MapPointType.Boss, [])
        ]);
        var act = new ActDefinition(ContentId.Parse("test-fixture:ancient-act"), _ => map);
        var environment = new RunEnvironment(new RunDefinition(ContentId.Parse("test-fixture:ancient-run"), [act]),
            BaseSpireContent.Ironclad());
        var session = environment.Start(8);

        var opening = environment.Observe(session);
        Assert.Equal(RunPhase.ResolveRoom, opening.Phase);
        Assert.Equal(RunRoomType.Event, opening.CurrentRoom!.Value);
        Assert.Equal(MapPointType.Ancient, opening.CurrentMapPointType!.Value);
        Assert.Equal(0, environment.LegalActions(session).Count);
        var route = environment.CompleteRoom(session, RunResult(RunOutcome.RoomCleared, opening));
        Assert.Equal(RunPhase.ChooseMapPoint, route.Phase);
        Assert.Equal(1, environment.LegalActions(session).Count);

        var secondAct = new ActDefinition(ContentId.Parse("test-fixture:ancient-act-two"), _ => map);
        var twoActRun = new RunDefinition(ContentId.Parse("test-fixture:two-ancient-run"), [act, secondAct]);
        var twoActEnvironment = new RunEnvironment(twoActRun, BaseSpireContent.Ironclad());
        var twoActSession = twoActEnvironment.Start(10);
        var firstEntry = twoActEnvironment.Observe(twoActSession);
        twoActEnvironment.CompleteRoom(twoActSession, RunResult(RunOutcome.RoomCleared, firstEntry));
        twoActEnvironment.Step(twoActSession, new ChooseMapPointAction("monster"));
        twoActEnvironment.CompleteRoom(twoActSession,
            RunResult(RunOutcome.RoomCleared, twoActEnvironment.Observe(twoActSession)));
        twoActEnvironment.Step(twoActSession, new ChooseMapPointAction("boss"));
        var nextActEntry = twoActEnvironment.CompleteRoom(twoActSession,
            RunResult(RunOutcome.RoomCleared, twoActEnvironment.Observe(twoActSession)));
        Assert.Equal(1, nextActEntry.ActIndex);
        Assert.Equal(RunPhase.ResolveRoom, nextActEntry.Phase);
        Assert.Equal(RunRoomType.Event, nextActEntry.CurrentRoom!.Value);
    }

    public void BaseSpireStandardRunUsesItsActMapGenerators()
    {
        var registry = new ContentRegistry();
        registry.AddPackage(new BaseSpirePackage());
        var definition = registry.Build();
        var run = definition.Runs[BaseSpireIds.StandardRun];
        Assert.True(run.UnknownRoomOdds is not null);
        Assert.Equal(new[] { BaseSpireIds.OvergrowthAct, BaseSpireIds.HiveAct, BaseSpireIds.GloryAct }.AsEnumerable(),
            run.Acts.Select(act => act.Id));
        var environment = new RunEnvironment(definition, BaseSpireIds.StandardRun, BaseSpireIds.Ironclad);
        var session = environment.Start("20260927");
        var replay = environment.Observe(environment.Start("20260927"));
        var start = environment.Observe(session);
        Assert.Equal(start.Map.Select(point => (point.Id, point.Type)), replay.Map.Select(point => (point.Id, point.Type)));
        Assert.Equal(MapPointType.Ancient, start.Map.Single(point => point.Id == start.CurrentPointId).Type);
        Assert.Equal(RunPhase.ResolveRoom, start.Phase);
        Assert.Equal(RunRoomType.Event, start.CurrentRoom!.Value);
        var route = environment.CompleteRoom(session, RunResult(RunOutcome.RoomCleared, start));
        Assert.True(environment.LegalActions(session).Count > 0);
        Assert.Equal(RunPhase.ChooseMapPoint, route.Phase);
    }

    public void UnknownMapPointOddsAdaptAfterEachRoll()
    {
        var map = new ActMapDefinition("ancient", "boss",
        [
            new MapPointDefinition("ancient", 0, 3, MapPointType.Ancient, ["unknown-1"]),
            new MapPointDefinition("unknown-1", 1, 3, MapPointType.Unknown, ["unknown-2"]),
            new MapPointDefinition("unknown-2", 2, 3, MapPointType.Unknown, ["boss"]),
            new MapPointDefinition("boss", 3, 3, MapPointType.Boss, [])
        ]);
        var odds = new UnknownRoomOddsDefinition(
        [
            new UnknownRoomOddsEntry(RunRoomType.Monster, 0.1f),
            new UnknownRoomOddsEntry(RunRoomType.Elite, -1f),
            new UnknownRoomOddsEntry(RunRoomType.Treasure, 0.02f),
            new UnknownRoomOddsEntry(RunRoomType.Shop, 0.03f)
        ]);
        var act = new ActDefinition(ContentId.Parse("test-fixture:odds-act"), _ => map);
        var run = new RunDefinition(ContentId.Parse("test-fixture:odds-run"), [act], odds);
        var environment = new RunEnvironment(run, BaseSpireContent.Ironclad());
        var seed = FindSeedForUnknownRolls(first => first > 0.15f, second => second <= 0.2f);
        var session = environment.Start(seed);
        var opening = environment.Observe(session);
        environment.CompleteRoom(session, RunResult(RunOutcome.RoomCleared, opening));

        var firstUnknown = environment.Step(session, new ChooseMapPointAction("unknown-1"));
        Assert.Equal(RunRoomType.Event, firstUnknown.CurrentRoom!.Value);
        environment.CompleteRoom(session, RunResult(RunOutcome.RoomCleared, firstUnknown));
        var secondUnknown = environment.Step(session, new ChooseMapPointAction("unknown-2"));
        Assert.Equal(RunRoomType.Monster, secondUnknown.CurrentRoom!.Value);
    }

    public void UnknownMapOddsResetWhenTheActChanges()
    {
        var firstMap = new ActMapDefinition("ancient", "boss",
        [
            new MapPointDefinition("ancient", 0, 3, MapPointType.Ancient, ["unknown-1"]),
            new MapPointDefinition("unknown-1", 1, 3, MapPointType.Unknown, ["unknown-2"]),
            new MapPointDefinition("unknown-2", 2, 3, MapPointType.Unknown, ["boss"]),
            new MapPointDefinition("boss", 3, 3, MapPointType.Boss, [])
        ]);
        var secondMap = new ActMapDefinition("ancient-2", "boss-2",
        [
            new MapPointDefinition("ancient-2", 0, 3, MapPointType.Ancient, ["unknown-3"]),
            new MapPointDefinition("unknown-3", 1, 3, MapPointType.Unknown, ["boss-2"]),
            new MapPointDefinition("boss-2", 2, 3, MapPointType.Boss, [])
        ]);
        var odds = new UnknownRoomOddsDefinition(
        [
            new UnknownRoomOddsEntry(RunRoomType.Monster, 0.1f),
            new UnknownRoomOddsEntry(RunRoomType.Elite, -1f),
            new UnknownRoomOddsEntry(RunRoomType.Treasure, 0.02f),
            new UnknownRoomOddsEntry(RunRoomType.Shop, 0.03f)
        ]);
        var run = new RunDefinition(ContentId.Parse("test-fixture:odds-reset-run"),
        [
            new ActDefinition(ContentId.Parse("test-fixture:odds-reset-act-1"), _ => firstMap),
            new ActDefinition(ContentId.Parse("test-fixture:odds-reset-act-2"), _ => secondMap)
        ], odds);
        var environment = new RunEnvironment(run, BaseSpireContent.Ironclad());
        var seed = FindSeedForUnknownRolls(first => first > 0.15f, second => second <= 0.2f, third => third > 0.15f);
        var session = environment.Start(seed);
        environment.CompleteRoom(session, RunResult(RunOutcome.RoomCleared, environment.Observe(session)));
        var firstUnknown = environment.Step(session, new ChooseMapPointAction("unknown-1"));
        Assert.Equal(RunRoomType.Event, firstUnknown.CurrentRoom!.Value);
        environment.CompleteRoom(session, RunResult(RunOutcome.RoomCleared, firstUnknown));
        var secondUnknown = environment.Step(session, new ChooseMapPointAction("unknown-2"));
        Assert.Equal(RunRoomType.Monster, secondUnknown.CurrentRoom!.Value);
        var secondUnknownResult = environment.CompleteRoom(session, RunResult(RunOutcome.RoomCleared, secondUnknown));
        Assert.Equal(RunPhase.ChooseMapPoint, secondUnknownResult.Phase);
        environment.Step(session, new ChooseMapPointAction("boss"));
        var nextActEntry = environment.CompleteRoom(session, RunResult(RunOutcome.RoomCleared, environment.Observe(session)));
        Assert.Equal(1, nextActEntry.ActIndex);
        environment.CompleteRoom(session, RunResult(RunOutcome.RoomCleared, nextActEntry));
        var thirdUnknown = environment.Step(session, new ChooseMapPointAction("unknown-3"));
        Assert.Equal(RunRoomType.Event, thirdUnknown.CurrentRoom!.Value);
    }

    public void ShopHistoryBlacklistsShopFromTheNextUnknownRoom()
    {
        var map = new ActMapDefinition("ancient", "boss",
        [
            new MapPointDefinition("ancient", 0, 3, MapPointType.Ancient, ["shop"]),
            new MapPointDefinition("shop", 1, 3, MapPointType.Shop, ["unknown"]),
            new MapPointDefinition("unknown", 2, 3, MapPointType.Unknown, ["boss"]),
            new MapPointDefinition("boss", 3, 3, MapPointType.Boss, [])
        ]);
        var odds = new UnknownRoomOddsDefinition(
        [
            new UnknownRoomOddsEntry(RunRoomType.Monster, 0.1f),
            new UnknownRoomOddsEntry(RunRoomType.Elite, -1f),
            new UnknownRoomOddsEntry(RunRoomType.Treasure, 0.02f),
            new UnknownRoomOddsEntry(RunRoomType.Shop, 0.03f)
        ]);
        var run = new RunDefinition(ContentId.Parse("test-fixture:shop-blacklist-run"),
            [new ActDefinition(ContentId.Parse("test-fixture:shop-blacklist-act"), _ => map)], odds);
        var environment = new RunEnvironment(run, BaseSpireContent.Ironclad());
        var seed = FindSeedForUnknownRolls(first => first > 0.12f && first <= 0.15f, _ => true);
        var session = environment.Start(seed);
        var opening = environment.Observe(session);
        environment.CompleteRoom(session, RunResult(RunOutcome.RoomCleared, opening));
        var shop = environment.Step(session, new ChooseMapPointAction("shop"));
        Assert.Equal(RunRoomType.Shop, shop.CurrentRoom!.Value);
        environment.CompleteRoom(session, RunResult(RunOutcome.RoomCleared, shop));
        var unknown = environment.Step(session, new ChooseMapPointAction("unknown"));
        Assert.Equal(RunRoomType.Event, unknown.CurrentRoom!.Value);
        Assert.Equal(new[] { "ancient", "shop", "unknown" }.AsEnumerable(),
            unknown.VisitedMapPoints.Select(visit => visit.PointId));

        var shopChildrenMap = new ActMapDefinition("entry", "end",
        [
            new MapPointDefinition("entry", 0, 3, MapPointType.Ancient, ["unknown"]),
            new MapPointDefinition("unknown", 1, 3, MapPointType.Unknown, ["shop-left", "shop-right"]),
            new MapPointDefinition("shop-left", 2, 2, MapPointType.Shop, ["end"]),
            new MapPointDefinition("shop-right", 2, 4, MapPointType.Shop, ["end"]),
            new MapPointDefinition("end", 3, 3, MapPointType.Boss, [])
        ]);
        var childrenRun = new RunDefinition(ContentId.Parse("test-fixture:shop-children-blacklist-run"),
            [new ActDefinition(ContentId.Parse("test-fixture:shop-children-blacklist-act"), _ => shopChildrenMap)], odds);
        var childrenEnvironment = new RunEnvironment(childrenRun, BaseSpireContent.Ironclad());
        var childrenSession = childrenEnvironment.Start(seed);
        childrenEnvironment.CompleteRoom(childrenSession,
            RunResult(RunOutcome.RoomCleared, childrenEnvironment.Observe(childrenSession)));
        var childrenUnknown = childrenEnvironment.Step(childrenSession, new ChooseMapPointAction("unknown"));
        Assert.Equal(RunRoomType.Event, childrenUnknown.CurrentRoom!.Value);
    }

    private static string FindSeedForUnknownRolls(Func<float, bool> firstCondition, Func<float, bool> secondCondition,
        Func<float, bool>? thirdCondition = null)
    {
        for (var candidate = 0; candidate < 100_000; candidate++)
        {
            var rng = RandomStreams.FromStringSeed(candidate.ToString()).Get("unknown_map_point");
            var first = rng.NextFloat();
            var second = rng.NextFloat();
            var third = rng.NextFloat();
            if (firstCondition(first) && secondCondition(second) && (thirdCondition is null || thirdCondition(third)))
                return candidate.ToString();
        }
        throw new InvalidOperationException("Could not find a deterministic run seed for the unknown-room odds fixture.");
    }

    private static RunDefinition BuildRunDefinition()
    {
        static ActDefinition Act(string id) => new(ContentId.Parse(id), _ => new ActMapDefinition("start", "boss",
        [
            new MapPointDefinition("start", 0, 3, MapPointType.Start, ["monster", "elite"]),
            new MapPointDefinition("monster", 1, 2, MapPointType.Monster, ["boss"]),
            new MapPointDefinition("elite", 1, 4, MapPointType.Elite, ["boss"]),
            new MapPointDefinition("boss", 2, 3, MapPointType.Boss, [])
        ]));

        return new RunDefinition(ContentId.Parse("test-fixture:two-act-run"),
            [Act("test-fixture:act-one"), Act("test-fixture:act-two")]);
    }

    private static RunRoomResult RunResult(RunOutcome outcome, RunObservation state) =>
        new(outcome, outcome == RunOutcome.PlayerDefeated ? 0 : state.Hp, state.MaxHp, state.Gold,
            state.Deck, state.Relics, state.Potions, state.DeckInstances);

    private static GameDefinition Build(IRulePackage package)
    {
        var registry = new ContentRegistry();
        registry.AddPackage(package);
        return registry.Build();
    }

    private sealed class DuplicateCardPackage : IRulePackage
    {
        public PackageInfo Info { get; } = new("duplicate", "1.0.0");
        public void Register(ContentRegistry registry) => registry.Register(new CardDefinition(
            ContentId.Parse("test-fixture:strike"), 0, CardType.Attack, new AttackEffect(99)));
    }

    private sealed class TestRunPackage(RunDefinition run) : IRulePackage
    {
        public PackageInfo Info { get; } = new("run-fixture", "1.0.0");
        public void Register(ContentRegistry registry) => registry.Register(run);
    }

    private sealed class CountingRunHook : IRunHook
    {
        public ContentId Id { get; } = ContentId.Parse("test-fixture:counting-run-hook");
        public int InvocationCount { get; private set; }
        public void AfterCombatVictory(RunHookContext context) => InvocationCount++;
    }

    private sealed class RestSiteTestHook : IRunHook
    {
        public ContentId Id { get; } = ContentId.Parse("test-fixture:rest-site-hook");
        public int HealCount { get; private set; }
        public int SmithCount { get; private set; }
        public void AfterCombatVictory(RunHookContext context) { }
        public int ModifyRestSiteHealAmount(RunObservation observation, int amount) => amount + 2;
        public void AfterRestSiteHeal(RunHookContext context) => HealCount++;
        public void AfterRestSiteSmith(RunHookContext context) => SmithCount++;
    }

    private sealed class UnknownRoomOddsHook : IRunHook
    {
        public ContentId Id { get; } = ContentId.Parse("test-fixture:unknown-room-odds-hook");
        public MapPointType? ObservedMapType { get; private set; }
        public IReadOnlySet<RunRoomType>? AvailableTypes { get; private set; }
        public int IncreaseCalls { get; private set; }
        public bool TreasureIncreaseWasModified { get; private set; }

        public void AfterCombatVictory(RunHookContext context) { }

        public IReadOnlySet<RunRoomType> ModifyUnknownRoomTypes(RunObservation observation,
            IReadOnlySet<RunRoomType> roomTypes)
        {
            ObservedMapType = observation.CurrentMapPointType;
            AvailableTypes = roomTypes;
            return roomTypes.Where(type => type != RunRoomType.Shop).ToHashSet();
        }

        public float ModifyUnknownOddsIncrease(RunObservation observation, RunRoomType roomType, float increase)
        {
            IncreaseCalls++;
            if (roomType != RunRoomType.Treasure) return increase;
            TreasureIncreaseWasModified = true;
            return increase * 2;
        }
    }

    private sealed class RunCombatPackage(RunDefinition run, CharacterDefinition character,
        EnemyDefinition enemy, IEnumerable<CardDefinition> cards, EncounterDefinition encounter,
        IEnumerable<EnemyDefinition>? extraEnemies = null,
        IEnumerable<EncounterDefinition>? extraEncounters = null) : IRulePackage
    {
        public PackageInfo Info { get; } = new("run-combat-fixture", "1.0.0");

        public void Register(ContentRegistry registry)
        {
            registry.Register(run);
            registry.Register(character);
            registry.Register(enemy);
            foreach (var extraEnemy in extraEnemies ?? []) registry.Register(extraEnemy);
            foreach (var card in cards) registry.Register(card);
            registry.Register(encounter);
            foreach (var extraEncounter in extraEncounters ?? []) registry.Register(extraEncounter);
        }
    }

    private sealed class EncounterPoolPackage(RunDefinition run, CharacterDefinition character,
        ContentId enemyId, IEnumerable<EncounterDefinition> encounters, ContentId? alternateEnemyId = null) : IRulePackage
    {
        public PackageInfo Info { get; } = new("encounter-pool-fixture", "1.0.0");

        public void Register(ContentRegistry registry)
        {
            registry.Register(run);
            registry.Register(character);
            registry.Register(new EnemyDefinition(enemyId, 20, 20,
                [new EnemyActionDefinition(ContentId.Parse("pool-test:idle"), new GainBlockEffect(0), IntentKind.None)]));
            if (alternateEnemyId is { } alternateId)
                registry.Register(new EnemyDefinition(alternateId, 20, 20,
                    [new EnemyActionDefinition(ContentId.Parse("pool-test:alternate-idle"), new GainBlockEffect(0), IntentKind.None)]));
            foreach (var encounter in encounters) registry.Register(encounter);
        }
    }

    private sealed class RoomContentPackage(RunDefinition run, CharacterDefinition character,
        IEnumerable<RunEventDefinition> events, ContentId? enemyId = null,
        IEnumerable<EncounterDefinition>? encounters = null) : IRulePackage
    {
        public PackageInfo Info { get; } = new("room-content-fixture", "1.0.0");

        public void Register(ContentRegistry registry)
        {
            registry.Register(run);
            registry.Register(character);
            foreach (var runEvent in events) registry.Register(runEvent);
            if (enemyId is { } id)
                registry.Register(new EnemyDefinition(id, 20, 20,
                    [new EnemyActionDefinition(ContentId.Parse("room-content-test:idle"),
                        new GainBlockEffect(0), IntentKind.None)]));
            foreach (var encounter in encounters ?? []) registry.Register(encounter);
        }
    }

    private sealed class RetainFixturePackage(ContentId retainedId, ContentId plainId) : IRulePackage
    {
        public PackageInfo Info { get; } = new("retain-fixture", "1.0.0");

        public void Register(ContentRegistry registry)
        {
            registry.Register(new CardDefinition(retainedId, 0, CardType.Skill, new GainBlockEffect(1),
                Keywords: CardKeywords.Retain, Target: TargetSelector.Self));
            registry.Register(new CardDefinition(plainId, 0, CardType.Skill, new GainBlockEffect(1),
                Target: TargetSelector.Self));
            var actionId = ContentId.Parse("retain-fixture:enemy-action");
            registry.Register(new EnemyDefinition(ContentId.Parse("retain-fixture:enemy"), 10, 10,
                [new EnemyActionDefinition(actionId, new SequenceEffect(Array.Empty<EffectNode>()), IntentKind.Buff)]));
        }
    }

    private sealed class HandCleanupHook(ContentId? suppressedEtherealCard, bool suppressFlush,
        ContentId? suppressFlushPower = null) : IRuleHook
    {
        public ContentId Id { get; } = ContentId.Parse("hand-cleanup-hook:rules");

        public void OnEvent(RuleHookContext context, GameEvent gameEvent) { }

        public bool ShouldEtherealTrigger(RuleHookContext context, CardInstance card) =>
            card.CardId != suppressedEtherealCard;

        public bool ShouldFlush(RuleHookContext context, string playerId) => !suppressFlush
            && (suppressFlushPower is not { } power || context.GetPowerAmount(playerId, power) == 0);
    }

    private sealed class TestHookPackage(IRuleHook hook) : IRulePackage
    {
        public PackageInfo Info { get; } = new("hand-cleanup-hook", "1.0.0");

        public void Register(ContentRegistry registry) => registry.Register(hook);
    }

    private sealed class MultiEnemyPackage(CardDefinition card, params EnemyDefinition[] enemies) : IRulePackage
    {
        public PackageInfo Info { get; } = new("multi-enemy-fixture", "1.0.0");

        public void Register(ContentRegistry registry)
        {
            registry.Register(card);
            foreach (var enemy in enemies) registry.Register(enemy);
        }
    }

    private sealed class AllOpponentsPackage(CardDefinition card, PowerDefinition power,
        params EnemyDefinition[] enemies) : IRulePackage
    {
        public PackageInfo Info { get; } = new("all-opponents-fixture", "1.0.0");

        public void Register(ContentRegistry registry)
        {
            registry.Register(card);
            registry.Register(power);
            foreach (var enemy in enemies) registry.Register(enemy);
        }
    }

    private sealed class MissingEnemyPowerPackage : IRulePackage
    {
        public PackageInfo Info { get; } = new("missing-enemy-power-fixture", "1.0.0");

        public void Register(ContentRegistry registry) => registry.Register(new EnemyDefinition(
            ContentId.Parse("missing-enemy-power:enemy"), 10, 10,
            [new EnemyActionDefinition(ContentId.Parse("missing-enemy-power:action"),
                new SequenceEffect(Array.Empty<EffectNode>()), IntentKind.Buff,
                AppliedPower: ContentId.Parse("missing-enemy-power:unknown-power"), AppliedPowerAmount: 1)]));
    }

    private sealed class RandomEnemyTransitionPackage : IRulePackage
    {
        public PackageInfo Info { get; } = new("random-enemy-fixture", "1.0.0");

        public void Register(ContentRegistry registry)
        {
            var actions = new[]
            {
                (Id: ContentId.Parse("random-enemy:one"), Damage: 1),
                (Id: ContentId.Parse("random-enemy:two"), Damage: 2),
                (Id: ContentId.Parse("random-enemy:three"), Damage: 3)
            };
            var transition = new EnemyActionTransition(actions.Select((action, index) =>
                new EnemyActionChoice(action.Id, index + 1)), EnemyActionRepeatPolicy.PreventImmediateRepeat);
            registry.Register(new EnemyDefinition(ContentId.Parse("random-enemy:enemy"), 30, 30,
                actions.Select(action => new EnemyActionDefinition(action.Id, new AttackEffect(action.Damage),
                    IntentKind.Attack, action.Damage, Transition: transition)).ToArray(), startingTransition: transition));
        }
    }

    private sealed class ChoiceCardPackage : IRulePackage
    {
        public PackageInfo Info { get; } = new("choice-test", "1.0.0");
        public void Register(ContentRegistry registry) => registry.Register(new CardDefinition(
            ContentId.Parse("test-fixture:choose-block"), 1, CardType.Skill,
            new SequenceEffect(new EffectNode[] { new ChooseEffect("Choose", [
                new ChoiceOption("A"),
                new ChoiceOption("B", new GainBlockEffect(5))
            ]), new GainBlockEffect(2) })));
    }

    private sealed class PartialFailurePackage : IRulePackage
    {
        public PackageInfo Info { get; } = new("partial-mod", "1.0.0");
        public void Register(ContentRegistry registry)
        {
            registry.Register(new CountingRunHook());
            registry.Register(new RunEventDefinition(ContentId.Parse("partial-mod:event")));
            registry.Register(new CardDefinition(ContentId.Parse("partial-mod:orphan"), 0, CardType.Skill, new GainBlockEffect(1)));
            registry.Register(new CharacterDefinition(ContentId.Parse("partial-mod:character"), 60, 50, []));
            registry.Register(new RunDefinition(ContentId.Parse("partial-mod:run"),
                [new ActDefinition(ContentId.Parse("partial-mod:act"), _ => throw new NotSupportedException())]));
            registry.Register(new CardDefinition(ContentId.Parse("test-fixture:strike"), 0, CardType.Attack, new AttackEffect(99)));
        }
    }

    private sealed class InvalidCardPoolPackage : IRulePackage
    {
        public PackageInfo Info { get; } = new("invalid-card-pool", "1.0.0");

        public void Register(ContentRegistry registry)
        {
            var poolId = ContentId.Parse("invalid-card-pool:pool");
            registry.Register(new CardPoolDefinition(poolId, [ContentId.Parse("invalid-card-pool:missing-card")]));
            registry.Register(new CharacterDefinition(ContentId.Parse("invalid-card-pool:character"), 60, 50, [],
                cardPools: [poolId]));
        }
    }

    private sealed class IncomingVulnerablePackage : IRulePackage
    {
        public PackageInfo Info { get; } = new("incoming-vulnerable-test", "1.0.0");
        public void Register(ContentRegistry registry)
        {
            registry.Register(new PowerDefinition(BaseSpireIds.Vulnerable, PowerTrigger.AfterEnemyTurnEnd,
                Role: PowerRole.Vulnerable, DecrementOnTrigger: true, SkipFirstTriggerWhenAppliedToPlayer: true));
            registry.Register(new EnemyDefinition(ContentId.Parse("test-fixture:vulnerable-cultist"), 30, 30,
            [
                new EnemyActionDefinition(ContentId.Parse("test-fixture:apply-vulnerable"),
                    new SequenceEffect(new EffectNode[]
                    {
                        new AttackEffect(3),
                        new ApplyPowerEffect(BaseSpireIds.Vulnerable, 2, TargetSelector.FirstEnemy)
                    }), IntentKind.Attack, 3),
                new EnemyActionDefinition(ContentId.Parse("test-fixture:vulnerable-attack"),
                    new AttackEffect(3), IntentKind.Attack, 3)
            ]));
        }
    }

    private sealed class EnemyChoicePackage : IRulePackage
    {
        public PackageInfo Info { get; } = new("enemy-choice-test", "1.0.0");
        public void Register(ContentRegistry registry) => registry.Register(new EnemyDefinition(
            ContentId.Parse("test-fixture:choosing-cultist"), 30, 30,
            [new EnemyActionDefinition(ContentId.Parse("test-fixture:choose-and-attack"),
                new SequenceEffect(new EffectNode[]
                {
                    new ChooseEffect("Choose enemy action", [new ChoiceOption("Guard", new GainBlockEffect(5))]),
                    new AttackEffect(3)
                }), IntentKind.Attack, 3)]));
    }
}

internal static class Assert
{
    public static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
    }

    public static void Equal<T>(IEnumerable<T> expected, IEnumerable<T> actual)
    {
        if (!expected.SequenceEqual(actual))
            throw new InvalidOperationException("Sequences differ.");
    }

    public static void True(bool value)
    {
        if (!value) throw new InvalidOperationException("Expected true.");
    }

    public static void False(bool value)
    {
        if (value) throw new InvalidOperationException("Expected false.");
    }

    public static void Contains<T>(IEnumerable<T> values, Predicate<T> predicate)
    {
        if (!values.Any(value => predicate(value))) throw new InvalidOperationException("Expected matching item.");
    }

    public static T Throws<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T exception) { return exception; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }
}

internal static class Program
{
    public static void Main()
    {
        var tests = new CombatSliceTests();
        tests.SyntheticRitualFixtureUsesCorrectSideTurnTiming();
        tests.BasePackageRegistersVanillaStartingDeckAndDeadlyCultistValues();
        tests.BludgeonAndDemonFormUseRegisteredCardAndPlayerTurnStartRules();
        tests.AngerCreatesAnIndependentCopyAndPreservesPlayedUpgradeLevel();
        tests.HeadbuttPausesForAnExactCardInstanceAndMovesItToDrawPileTop();
        tests.BurningPactExhaustsAChosenHandInstanceThenDraws();
        tests.ArmamentsFiltersUpgradeableCardsAndUpgradesOneOrAll();
        tests.TrueGritUsesSeededRandomExhaustAndUpgradedPlayerChoice();
        tests.SwordBoomerangRandomizesEveryHitAndReplaysTheTargetSequence();
        tests.SecondWindProcessesEveryNonAttackHandCardInOrder();
        tests.BodySlamUsesCurrentBlockAsAttackDamageAndUpgradesItsCost();
        tests.BattleTranceDrawsThenPreventsFurtherDrawsUntilNextTurn();
        tests.FeelNoPainTriggersAfterChosenAndPlayedCardsExhaust();
        tests.EtherealCardsExhaustBeforeEnemyTurnAndTriggerPowers();
        tests.RetainedCardInstancesStayInHandAcrossEnemyTurn();
        tests.HandCleanupHooksCanSuppressEtherealAndFlush();
        tests.BloodlettingAndOfferingLoseHpWithoutConsumingBlockOrPoweringDamage();
        tests.BaseStrikeBashAndVulnerableUseRegisteredRules();
        tests.BaseDampCultistNormalValuesMatchSourceRules();
        tests.BaseCultistsEncounterRunsBothSourceEnemySequences();
        tests.BaseFuzzyWurmEncounterUsesItsLinkedSourceActionCycle();
        tests.BaseShrinkerBeetleUsesAPlayerDamageModifierRemovedWithItsApplier();
        tests.EnemyActionPowerReferencesAreValidatedWhenBuildingContent();
        tests.EnemyTransitionsUseWeightedRngAndPreventImmediateRepeats();
        tests.BaseWeakSlimeEncounterUsesRandomSlotsAndAppliesSlimedCards();
        tests.BaseSoloNibbitFollowsItsAttackBlockAndStrengthCycle();
        tests.MultipleMonstersPreferDistinctInitialHpWhenRangesAllow();
        tests.PlayerDebuffSkipsItsApplicationSideTurnEndAndTicksAfterEnemySide();
        tests.SameSeedAndActionsProduceSameTrace();
        tests.NamedRandomStreamsAreDeterministicAndCountCalls();
        tests.RandomNumbersMatchRecoveredMegaRandomSequence();
        tests.JsonEffectDslBuildsTypedRuntimeTree();
        tests.DuplicateContentIdRequiresExplicitOverride();
        tests.FailedPackageRegistrationRollsBackPartialChanges();
        tests.IndependentModCanOverrideContentAndObserveHooks();
        tests.LegalActionsRejectUnaffordableCardAndInvalidHandIndex();
        tests.ChoicePausesAndResumesTheRemainingEffectSequence();
        tests.ChoiceDuringEnemyTurnResumesTheTurnScheduler();
        tests.SinglePlayerRunTraversesActsAndStopsAtVictory();
        tests.SinglePlayerRunDefeatIsTerminal();
        tests.RestSiteOffersHealingOrUpgradingOneCard();
        tests.NestedCombatReturnsToItsParentRoom();
        tests.UnknownRoomOddsExposeOrderedModHooks();
        tests.RunCombatUsesTheRunDeckAndWritesItsOutcomeBack();
        tests.ActEncounterPoolsAreDeterministicAndSelectCombatRoomEncounters();
        tests.EncounterPoolsRejectMissingOrMisclassifiedContentAtBuild();
        tests.ActRoomContentShufflesEventsBeforeSelectingTheAncientAndReplaysBySeed();
        tests.ActRoomPoolsRejectEventIdsWithTheWrongKind();
        tests.SharedAncientDistributionConsumesOneDrawForEveryActAfterTheFirst();
        tests.CombatEncounterSupportsMultipleEnemiesAndSelectedTargets();
        tests.UnknownMapPointKeepsMapTypeSeparateFromResolvedRoomType();
        tests.RunMapGenerationReplaysFromTheSessionSeed();
        tests.BaseSpireActMapsAreDeterministicAndHonorTheSourceLayout();
        tests.AncientEntryIsAnInitialEventBeforeTheFirstRouteChoice();
        tests.BaseSpireStandardRunUsesItsActMapGenerators();
        tests.UnknownMapPointOddsAdaptAfterEachRoll();
        tests.UnknownMapOddsResetWhenTheActChanges();
        tests.ShopHistoryBlacklistsShopFromTheNextUnknownRoom();
        Console.WriteLine("Passed 58 gameplay checks.");
    }
}

