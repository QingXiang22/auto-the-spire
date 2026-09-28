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

    public void EnemyLocalVariablesDriveDslTransitionsAndAppearInObservations()
    {
        var environment = new CombatEnvironment(Build(new BaseSpirePackage(), new EnemyVariableFixturePackage()));
        var session = environment.StartEncounter(918, "ironclad", 56, 65, [],
            ContentId.Parse("enemy-variable-fixture:encounter"));
        var enemies = environment.Observe(session).Enemies;
        var calls = ContentId.Parse("enemy-variable-fixture:calls");
        Assert.Equal(2, enemies.Count);
        Assert.Equal(0, enemies[0].Variables[calls]);
        Assert.Equal(0, enemies[1].Variables[calls]);
        session.State.Enemies[0].SetVariable(calls, 5);

        environment.Step(session, new EndTurnAction());
        enemies = environment.Observe(session).Enemies;
        Assert.Equal(6, enemies[0].Variables[calls]);
        Assert.Equal(1, enemies[1].Variables[calls]);
        Assert.Equal(2, enemies[0].IntentDamage);
        Assert.Equal(1, enemies[1].IntentDamage);
        Assert.Contains(session.State.Events, gameEvent => gameEvent.Type == "enemy_variable_modified"
            && gameEvent.Target == enemies[1].Id && gameEvent.Amount == 1
            && gameEvent.AffectedVariable == calls);

        var invalid = DslSourceParser.Parse("""
            package invalid-enemy-state {
              enemy Counter {
                hp = range(10, 10)
                moves = actions(hit = attack(1, target=player))
                behavior = graph(start=hit, edges=[edge(hit,
                  chooseByCondition(candidate(hit, when=variable(Missing, Equal, 0))))])
              }
            }
            """, "invalid-enemy-state.spire");
        Assert.Throws<DslSourceException>(() => SpireCombatBehaviorDslCompiler.Compile(invalid));
    }

    public void BaseToughEggHatchesByResettingItsHealthAndSwitchingItsLocalPhase()
    {
        var environment = new CombatEnvironment(Build(new BaseSpirePackage()));
        var session = environment.Start(919, "ironclad", 56, 65, [], BaseSpireIds.ToughEgg.ToString());
        var egg = environment.Observe(session).Enemies.Single();
        var hatchPower = ContentId.Parse("base-spire:hatch");
        var hatchedVariable = ContentId.Parse("base-spire:hatched");
        Assert.True(egg.Hp is >= 14 and <= 18);
        Assert.Equal(1, egg.Powers[hatchPower]);
        Assert.Equal(0, egg.Variables[hatchedVariable]);
        Assert.Equal(IntentKind.Summon, egg.Intent);

        var hatchTurn = environment.Step(session, new EndTurnAction());
        egg = hatchTurn.Observation.Enemies.Single();
        Assert.True(egg.Hp is >= 19 and <= 22);
        Assert.Equal(0, egg.Powers.GetValueOrDefault(hatchPower));
        Assert.Equal(1, egg.Variables[hatchedVariable]);
        Assert.Equal(IntentKind.Attack, egg.Intent);
        Assert.Equal(4, egg.IntentDamage);
        Assert.Contains(hatchTurn.Events, gameEvent => gameEvent.Type == "enemy_health_set"
            && gameEvent.Target == egg.Id && gameEvent.ExactAmount == egg.Hp);
    }

    public void BaseOvicopterSummonsEggsThatHatchOnTheirNextEnemyTurn()
    {
        var environment = new CombatEnvironment(Build(new BaseSpirePackage()));
        var session = environment.StartEncounter(920, "ironclad", 80, 80, [], BaseSpireIds.OvicopterNormal);
        Assert.Equal(IntentKind.Summon, environment.Observe(session).Enemies.Single().Intent);

        environment.Step(session, new EndTurnAction());
        var firstEnemyTurn = environment.Observe(session).Enemies;
        Assert.Equal(4, firstEnemyTurn.Count);
        var eggs = firstEnemyTurn.Where(enemy => enemy.Id.StartsWith(BaseSpireIds.ToughEgg.Value,
            StringComparison.Ordinal)).ToArray();
        Assert.Equal(3, eggs.Length);
        Assert.True(eggs.All(egg => egg.Powers.GetValueOrDefault(ContentId.Parse("base-spire:hatch")) == 2));
        Assert.True(eggs.All(egg => egg.Powers.GetValueOrDefault(ContentId.Parse("base-spire:minion")) == 1));
        Assert.True(eggs.All(egg => egg.Variables.GetValueOrDefault(ContentId.Parse("base-spire:hatched")) == 0));
        Assert.True(eggs.All(egg => egg.Hp is >= 14 and <= 18));

        environment.Step(session, new EndTurnAction());
        var secondEnemyTurn = environment.Observe(session).Enemies;
        eggs = secondEnemyTurn.Where(enemy => enemy.Id.StartsWith(BaseSpireIds.ToughEgg.Value,
            StringComparison.Ordinal)).ToArray();
        Assert.True(eggs.All(egg => egg.Powers.GetValueOrDefault(ContentId.Parse("base-spire:hatch")) == 0));
        Assert.True(eggs.All(egg => egg.Variables.GetValueOrDefault(ContentId.Parse("base-spire:hatched")) == 1));
        Assert.True(eggs.All(egg => egg.Hp is >= 19 and <= 22));

        environment.Step(session, new EndTurnAction());
        var thirdEnemyTurn = environment.Observe(session).Enemies;
        Assert.Equal(4, thirdEnemyTurn.Count);
        Assert.Equal(IntentKind.Summon, thirdEnemyTurn[0].Intent);

        environment.Step(session, new EndTurnAction());
        var fourthEnemyTurn = environment.Observe(session).Enemies;
        Assert.Equal(6, fourthEnemyTurn.Count);
        var newlyLaidEggs = fourthEnemyTurn.Where(enemy => enemy.Id.StartsWith(BaseSpireIds.ToughEgg.Value,
                StringComparison.Ordinal) && enemy.Powers.GetValueOrDefault(ContentId.Parse("base-spire:hatch")) == 2)
            .ToArray();
        Assert.Equal(2, newlyLaidEggs.Length);
    }

    public void BaseMinionsSuppressFatalBonusesWhileOrdinaryKillsTriggerThem()
    {
        var definition = Build(new BaseSpirePackage());
        var environment = new CombatEnvironment(definition);
        var encounter = environment.StartEncounter(921, "ironclad", 80, 80, [], BaseSpireIds.OvicopterNormal);
        environment.Step(encounter, new EndTurnAction());
        var egg = encounter.State.Enemies.First(enemy => enemy.Creature.Id.StartsWith(BaseSpireIds.ToughEgg.Value,
            StringComparison.Ordinal)).Creature;
        Assert.Equal(1, egg.PowerAmount(ContentId.Parse("base-spire:minion")));
        egg.Hp = 1;
        var feed = ContentId.Parse("base-spire:feed");
        encounter.State.Player.MutableHand.Add(new CardInstance("feed-minion-test", feed));

        environment.Step(encounter, new PlayCardAction(0, egg.Id));

        Assert.Equal(80, encounter.State.Player.Creature.MaxHp);
        Assert.Equal(0, egg.Hp);

        var ordinary = environment.Start(922, "ironclad", 80, 80, [feed],
            BaseSpireIds.ToughEgg.ToString(), enemyHp: 1);
        environment.Step(ordinary, new PlayCardAction(0, ordinary.State.Enemy.Id));
        Assert.Equal(83, ordinary.State.Player.Creature.MaxHp);
    }

    public void SlipperyPowerDecrementsOnlyAfterPositiveUnblockedDamage()
    {
        var definition = Build(new BaseSpirePackage());
        var environment = new CombatEnvironment(definition);
        var fullyBlocked = environment.Start(123, "ironclad", 80, 80,
            [BaseSpireIds.StrikeIronclad], "base-spire:inklet");
        fullyBlocked.State.Enemy.Block = 6;

        environment.Step(fullyBlocked, new PlayCardAction(0, fullyBlocked.State.Enemy.Id));

        Assert.Equal(1, fullyBlocked.State.Enemy.PowerAmount(ContentId.Parse("base-spire:slippery-power")));

        var partiallyBlocked = environment.Start(123, "ironclad", 80, 80,
            [BaseSpireIds.StrikeIronclad], "base-spire:inklet");
        partiallyBlocked.State.Enemy.Block = 5;

        environment.Step(partiallyBlocked, new PlayCardAction(0, partiallyBlocked.State.Enemy.Id));

        Assert.Equal(0, partiallyBlocked.State.Enemy.PowerAmount(ContentId.Parse("base-spire:slippery-power")));
    }

    public void BowlbugRockStunsWhenItsAttackIsFullyBlockedAndRecoversAfterSkippedAction()
    {
        var definition = Build(new BaseSpirePackage());
        var environment = new CombatEnvironment(definition);
        var session = environment.StartEncounter(41, "ironclad", 80, 80,
            [BaseSpireIds.Impervious], ContentId.Parse("base-spire:bowlbugs-weak"));
        var rockId = session.State.Enemies.Single(enemy =>
            enemy.Definition.Id == ContentId.Parse("base-spire:bowlbug-rock")).Creature.Id;

        environment.Step(session, new PlayCardAction(0, session.State.Player.Id));
        var attackTurn = environment.Step(session, new EndTurnAction());

        var rockAfterAttack = attackTurn.Observation.Enemies.Single(enemy => enemy.Id == rockId);
        Assert.Equal(IntentKind.Stun, rockAfterAttack.Intent);
        Assert.Contains(attackTurn.Events, gameEvent => gameEvent.Type == "fully_blocked"
            && gameEvent.Source == rockId && gameEvent.Amount > 0);

        var skippedTurn = environment.Step(session, new EndTurnAction());
        Assert.Contains(skippedTurn.Events, gameEvent => gameEvent.Type == "enemy_action_skipped"
            && gameEvent.Target == rockId);
        Assert.Equal(0, skippedTurn.Observation.Enemies.Single(enemy => enemy.Id == rockId)
            .Powers.GetValueOrDefault(ContentId.Parse("base-spire:off-balance")));
    }

    public void BasePackageRegistersVanillaStartingDeckAndDeadlyCultistValues()
    {
        var definition = Build(new BaseSpirePackage(deadlyEnemies: true));
        Assert.Equal(10, BaseSpireContent.IroncladStarterDeck().Count);
        Assert.Equal(5, BaseSpireContent.IroncladStarterDeck().Count(id => id == BaseSpireIds.StrikeIronclad));
        Assert.Equal(4, BaseSpireContent.IroncladStarterDeck().Count(id => id == BaseSpireIds.DefendIronclad));
        var ironclad = definition.Characters[BaseSpireIds.Ironclad];
        Assert.Equal(BaseSpireIds.IroncladCardPool, ironclad.CardPools.Single());
        Assert.Equal(RunRelicRarity.Rare, definition.Relics[BaseSpireIds.CharonsAshes].Rarity);
        Assert.True(definition.Relics[BaseSpireIds.CharonsAshes].CharacterPoolIds!
            .Contains(BaseSpireIds.Ironclad));
        Assert.Equal(RunRelicRarity.Uncommon, definition.Relics[BaseSpireIds.SelfFormingClay].Rarity);
        Assert.Equal(RunRelicRarity.Common, definition.Relics[BaseSpireIds.RedSkull].Rarity);
        Assert.True(definition.Relics[BaseSpireIds.RedSkull].CharacterPoolIds!
            .Contains(BaseSpireIds.Ironclad));
        Assert.Equal(RunRelicRarity.Rare, definition.Relics[BaseSpireIds.DemonTongue].Rarity);
        Assert.True(definition.Relics[BaseSpireIds.DemonTongue].CharacterPoolIds!
            .Contains(BaseSpireIds.Ironclad));
        var ironcladPool = definition.CardPools[BaseSpireIds.IroncladCardPool].Cards;
        Assert.Equal(ironcladPool.Count, ironcladPool.Distinct().Count());
        Assert.Equal(85, ironcladPool.Count);
        Assert.True(ironcladPool.All(id => definition.Cards.ContainsKey(id)));
        Assert.True(ironcladPool.Contains(BaseSpireIds.Break));
        Assert.True(ironcladPool.Contains(BaseSpireIds.Breakthrough));
        Assert.True(ironcladPool.Contains(BaseSpireIds.Hemokinesis));
        Assert.True(ironcladPool.Contains(BaseSpireIds.Tremble));
        Assert.False(ironcladPool.Contains(BaseSpireIds.Entrench));
        Assert.False(ironcladPool.Contains(BaseSpireIds.Slimed));
        Assert.False(definition.Cards.ContainsKey(ContentId.Parse("base-spire:clothesline")));
        Assert.Equal(CardRarity.Basic, definition.Cards[BaseSpireIds.StrikeIronclad].Rarity);
        Assert.Equal(CardRarity.Common, definition.Cards[BaseSpireIds.Anger].Rarity);
        Assert.Equal(CardRarity.Common, definition.Cards[BaseSpireIds.Armaments].Rarity);
        Assert.Equal(CardRarity.Common, definition.Cards[BaseSpireIds.BodySlam].Rarity);
        Assert.Equal(CardRarity.Uncommon, definition.Cards[BaseSpireIds.BattleTrance].Rarity);
        Assert.Equal(CardRarity.Uncommon, definition.Cards[BaseSpireIds.Whirlwind].Rarity);
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
        Assert.Equal(CardRarity.Rare, definition.Cards[BaseSpireIds.Barricade].Rarity);
        Assert.Equal(CardRarity.Rare, definition.Cards[BaseSpireIds.Impervious].Rarity);
        Assert.Contains(definition.CardPools[BaseSpireIds.IroncladCardPool].Cards,
            id => id == BaseSpireIds.Impervious);
        Assert.Equal(new AttackEffect(42, TargetSelector.SelectedTarget), definition.Cards[BaseSpireIds.Bludgeon]
            .ResolveEffect(new CardInstance("test:bludgeon+", BaseSpireIds.Bludgeon, 1)));
        Assert.Equal(new ApplyPowerEffect(BaseSpireIds.DemonFormPower, 3, TargetSelector.Self),
            definition.Cards[BaseSpireIds.DemonForm]
                .ResolveEffect(new CardInstance("test:demon-form+", BaseSpireIds.DemonForm, 1)));
        var upgradedHeadbutt = (SequenceEffect)definition.Cards[BaseSpireIds.Headbutt]
            .ResolveEffect(new CardInstance("test:headbutt+", BaseSpireIds.Headbutt, 1));
        Assert.Equal(new AttackEffect(12, TargetSelector.SelectedTarget), upgradedHeadbutt.Effects[0]);
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
        Assert.Equal(new AttackEffect(7, TargetSelector.SelectedTarget), upgradedIronWave.Effects[1]);
        Assert.Equal(new ApplyPowerEffect(BaseSpireIds.Strength, 3, TargetSelector.Self),
            definition.Cards[BaseSpireIds.Inflame].ResolveEffect(new CardInstance("test:inflame+", BaseSpireIds.Inflame, 1)));
        var upgradedPommel = (SequenceEffect)definition.Cards[BaseSpireIds.PommelStrike]
            .ResolveEffect(new CardInstance("test:pommel+", BaseSpireIds.PommelStrike, 1));
        Assert.Equal(new AttackEffect(10, TargetSelector.SelectedTarget), upgradedPommel.Effects[0]);
        Assert.Equal(new DrawCardsEffect(2), upgradedPommel.Effects[1]);
        var upgradedShrug = (SequenceEffect)definition.Cards[BaseSpireIds.ShrugItOff]
            .ResolveEffect(new CardInstance("test:shrug+", BaseSpireIds.ShrugItOff, 1));
        Assert.Equal(new GainBlockEffect(11), upgradedShrug.Effects[0]);
        Assert.Equal(new DrawCardsEffect(1), upgradedShrug.Effects[1]);
        Assert.Equal(new RepeatEffect(2, new AttackEffect(7, TargetSelector.SelectedTarget)),
            definition.Cards[BaseSpireIds.TwinStrike].ResolveEffect(new CardInstance("test:twin+", BaseSpireIds.TwinStrike, 1)));
        var upgradedUppercut = (SequenceEffect)definition.Cards[BaseSpireIds.Uppercut]
            .ResolveEffect(new CardInstance("test:uppercut+", BaseSpireIds.Uppercut, 1));
        Assert.Equal(new ApplyPowerEffect(BaseSpireIds.Weak, 2, TargetSelector.SelectedTarget), upgradedUppercut.Effects[1]);
        Assert.Equal(new ApplyPowerEffect(BaseSpireIds.Vulnerable, 2, TargetSelector.SelectedTarget), upgradedUppercut.Effects[2]);
        Assert.Equal(new AttackEffect(9, TargetSelector.SelectedTarget), definition.Cards[BaseSpireIds.StrikeIronclad]
            .ResolveEffect(new CardInstance("test:strike+", BaseSpireIds.StrikeIronclad, 1)));
        Assert.Equal(new GainBlockEffect(8), definition.Cards[BaseSpireIds.DefendIronclad]
            .ResolveEffect(new CardInstance("test:defend+", BaseSpireIds.DefendIronclad, 1)));
        var upgradedBash = (SequenceEffect)definition.Cards[BaseSpireIds.Bash]
            .ResolveEffect(new CardInstance("test:bash+", BaseSpireIds.Bash, 1));
        Assert.Equal(new AttackEffect(10, TargetSelector.SelectedTarget), upgradedBash.Effects[0]);
        Assert.Equal(new ApplyPowerEffect(BaseSpireIds.Vulnerable, 3, TargetSelector.SelectedTarget), upgradedBash.Effects[1]);
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

    public void StandardRunLoadsOvergrowthEncounterPoolsFromBaseContent()
    {
        var definition = Build(new BaseSpirePackage());
        var pool = definition.Runs[BaseSpireIds.StandardRun].Acts[0].EncounterPool;

        Assert.True(pool is not null);
        if (pool is null) throw new InvalidOperationException("Overgrowth encounter pool is missing.");
        Assert.Equal(15, pool.NormalRoomCount);
        Assert.Equal(3, pool.WeakNormalRoomCount);
        Assert.Equal(12, pool.RegularEncounters.Count);
        Assert.Equal(4, pool.WeakNormalEncounters.Count);
        Assert.Equal(3, pool.EliteEncounters.Count);
        Assert.Equal(1, pool.BossEncounters.Count);
        foreach (var encounterId in pool.WeakNormalEncounters)
            Assert.True(definition.Encounters[encounterId].IsWeak);
        foreach (var encounterId in pool.BossEncounters)
            Assert.Equal(RunRoomType.Boss, definition.Encounters[encounterId].RoomType);

        var runEnvironment = new RunEnvironment(definition, BaseSpireIds.StandardRun, BaseSpireIds.Ironclad);
        var run = runEnvironment.Start(1234);
        Assert.Equal(RunPhase.ChooseMapPoint, runEnvironment.Observe(run).Phase);
        var firstMapChoice = runEnvironment.LegalActions(run).OfType<ChooseMapPointAction>().First();
        var inMonsterRoom = runEnvironment.Step(run, firstMapChoice);
        Assert.True(inMonsterRoom.RoomStack[^1].EncounterId.HasValue);

        var combatEnvironment = new CombatEnvironment(definition);
        var combat = combatEnvironment.StartCurrentEncounterForRun(run);
        Assert.True(combatEnvironment.Observe(combat).Enemies.Count > 0);
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
            var expectedRandom = new RandomStreams(seed).Get("enemy-ai");
            var branch = new[] { (Damage: 1, Weight: 1), (Damage: 2, Weight: 2), (Damage: 3, Weight: 3) };
            var expectedDamages = new List<int>();
            int? previousDamage = null;
            for (var i = 0; i < 9; i++)
            {
                var eligible = branch.Where(choice => choice.Damage != previousDamage).ToArray();
                var roll = expectedRandom.NextFloat(eligible.Sum(choice => (float)choice.Weight));
                var expectedDamage = eligible[^1].Damage;
                foreach (var choice in eligible)
                {
                    roll -= choice.Weight;
                    if (roll <= 0f)
                    {
                        expectedDamage = choice.Damage;
                        break;
                    }
                }
                expectedDamages.Add(expectedDamage);
                previousDamage = expectedDamage;
            }

            var damages = new List<int> { combat.Observe(session).IntentDamage };
            for (var i = 0; i < 8; i++)
            {
                combat.Step(session, new EndTurnAction());
                damages.Add(combat.Observe(session).IntentDamage);
            }
            Assert.Equal(9, session.Random.Get("enemy-ai").Counter);
            Assert.Equal(expectedDamages.AsEnumerable(), damages.AsEnumerable());
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

    public void BaseTurretOperatorEncounterUsesAllyTargetingAndLivingAllyConditions()
    {
        var environment = new CombatEnvironment(Build(new BaseSpirePackage()));
        var session = environment.StartEncounter(1030, "ironclad", 1000, 1000, [],
            BaseSpireIds.TurretOperatorWeak);
        var livingShield = session.State.Enemies.Single(enemy => enemy.Definition.Id == BaseSpireIds.LivingShield);
        var turret = session.State.Enemies.Single(enemy => enemy.Definition.Id == BaseSpireIds.TurretOperator);
        Assert.Equal(25, livingShield.Creature.PowerAmount(BaseSpireIds.Rampart));
        Assert.Equal(6, environment.Observe(session).Enemies.Single(enemy => enemy.Id == livingShield.Creature.Id).IntentDamage);
        Assert.Equal(15, environment.Observe(session).Enemies.Single(enemy => enemy.Id == turret.Creature.Id).IntentDamage);

        environment.Step(session, new EndTurnAction());
        Assert.Equal(25, turret.Creature.Block);
        Assert.Equal(0, livingShield.Creature.Block);
        Assert.Contains(session.State.Events, gameEvent => gameEvent.Type == "block"
            && gameEvent.Source == livingShield.Creature.Id && gameEvent.Target == turret.Creature.Id
            && gameEvent.Amount == 25 && gameEvent.Properties == DamageProperties.Unpowered);

        environment.Step(session, new EndTurnAction());
        var turretObservation = environment.Observe(session).Enemies.Single(enemy => enemy.Id == turret.Creature.Id);
        Assert.Equal(IntentKind.Buff, turretObservation.Intent);
        environment.Step(session, new EndTurnAction());
        Assert.Equal(1, turret.Creature.Strength);
        Assert.Equal(20, environment.Observe(session).Enemies.Single(enemy => enemy.Id == turret.Creature.Id).IntentDamage);

        turret.Creature.Hp = 0;
        environment.Step(session, new EndTurnAction());
        var shieldObservation = environment.Observe(session).Enemies.Single(enemy => enemy.Id == livingShield.Creature.Id);
        Assert.Equal(16, shieldObservation.IntentDamage);
        environment.Step(session, new EndTurnAction());
        Assert.Equal(3, livingShield.Creature.Strength);
        shieldObservation = environment.Observe(session).Enemies.Single(enemy => enemy.Id == livingShield.Creature.Id);
        Assert.Equal(19, shieldObservation.IntentDamage);
    }

    public void EnemyTransitionsEvaluateOrderedConditionsAgainstCombatState()
    {
        var serializedCondition = System.Text.Json.JsonSerializer.Serialize<EnemyActionCondition>(
            new AllEnemyCondition([
                new EnemyMetricCondition(EnemyActionMetric.Turn, RunComparison.AtLeast, 1),
                new NotEnemyCondition(new EnemyPowerCondition(EnemyActionSide.Player,
                    ContentId.Parse("conditional-enemy:focus"), RunComparison.AtLeast, 1))
            ]));
        Assert.Equal(serializedCondition, System.Text.Json.JsonSerializer.Serialize(
            System.Text.Json.JsonSerializer.Deserialize<EnemyActionCondition>(serializedCondition)));

        var definition = Build(new ConditionalEnemyTransitionPackage());
        var environment = new CombatEnvironment(definition);
        var session = environment.StartEncounter(305, "ironclad", 100, 100, [], [
            new CombatEnemySpawn("conditional-enemy:enemy"),
            new CombatEnemySpawn("conditional-enemy:enemy")
        ]);

        Assert.Equal(3, environment.Observe(session).Enemies[0].IntentDamage);
        Assert.Equal(5, environment.Observe(session).Enemies[1].IntentDamage);
        Assert.Equal(0, session.Random.Get("enemy-ai").Counter);

        environment.Step(session, new EndTurnAction());
        Assert.Equal(92, environment.Observe(session).PlayerHp);
        Assert.Equal(ContentId.Parse("conditional-enemy:low-health-branch"),
            session.State.Enemies[0].Definition.Actions[session.State.Enemies[0].ActionIndex].Id);
        Assert.Equal(7, environment.Observe(session).Enemies[0].IntentDamage);
        Assert.Equal(0, session.Random.Get("enemy-ai").Counter);
    }

    public void EncounterContentDslRejectsUnsupportedShapesAndDuplicateIds()
    {
        Assert.Throws<System.Text.Json.JsonException>(() => ContentDsl.ParseEncounters(
            """{"schemaVersion":1,"encounters":[{"id":"dsl-fixture:encounter","roomType":"monster","enemyIds":[],"formations":[] }]}"""));
        Assert.Throws<System.Text.Json.JsonException>(() => ContentDsl.ParseEncounters(
            """{"schemaVersion":1,"encounters":[{"id":"dsl-fixture:encounter","roomType":"monster","enemySlots":[{"enemyChoices":["dsl-fixture:enemy"],"uniqueChoiceGroup":"dsl-fixture:group"}]}]}"""));
        Assert.Throws<System.Text.Json.JsonException>(() => ContentDsl.ParseEncounters(
            """{"schemaVersion":1,"encounters":[{"id":"dsl-fixture:encounter","roomType":"monster","enemyIds":["dsl-fixture:enemy"]},{"id":"dsl-fixture:encounter","roomType":"monster","enemyIds":["dsl-fixture:enemy"]}] }"""));
    }

    public void SpireCardDslCompilesTheCurrentIroncladCardSet()
    {
        using var stream = typeof(BaseSpirePackage).Assembly.GetManifestResourceStream(
            "BaseSpire.ContentSource.ironclad-cards.spire");
        Assert.True(stream is not null);
        using var reader = new StreamReader(stream!);
        var source = reader.ReadToEnd();
        var package = DslSourceParser.Parse(source, "base-spire/ironclad-cards.spire");

        Assert.Equal("base-spire", package.Name);
        var cards = SpireCombatDslCompiler.CompileCards(package);
        Assert.Equal(93, cards.Count);
        Assert.Equal(cards.Count, cards.Select(card => card.Id).Distinct().Count());
        var pool = Build(new BaseSpirePackage()).CardPools[BaseSpireIds.IroncladCardPool].Cards;
        Assert.Equal(cards.Where(card => card.PoolIds.Contains(BaseSpireIds.IroncladCardPool))
            .Select(card => card.Id), pool);
        Assert.Equal(87, pool.Count + cards.Count(card => card.MultiplayerOnly));
        Assert.True(cards.Any(card => card.Id == BaseSpireIds.Slimed));
        var demonicShield = cards.Single(card => card.Id == BaseSpireIds.DemonicShield);
        Assert.True(demonicShield.MultiplayerOnly);
        Assert.Equal(2, cards.Count(card => card.MultiplayerOnly));
        Assert.Equal(TargetSelector.SelectedTarget, demonicShield.Target);
        Assert.Equal(CardKeywords.Exhaust,
            demonicShield.ResolveKeywords(new CardInstance("demonic-shield", demonicShield.Id)));
        Assert.Equal(CardKeywords.None,
            demonicShield.ResolveKeywords(new CardInstance("demonic-shield+", demonicShield.Id, upgradeLevel: 1)));
        var demonicShieldEffect = demonicShield.Effect as SequenceEffect;
        Assert.True(demonicShieldEffect is not null);
        Assert.Equal(DamageProperties.Move | DamageProperties.Unpowered | DamageProperties.Unblockable,
            ((LoseHpEffect)demonicShieldEffect!.Effects[0]).Properties);
        Assert.Equal(new GainBlockFromCurrentBlockEffect(Target: TargetSelector.SelectedTarget),
            demonicShieldEffect.Effects[1]);
        Assert.False(pool.Contains(demonicShield.Id));
        Assert.True(cards.Single(card => card.Id == BaseSpireIds.Tank).MultiplayerOnly);
        Assert.False(pool.Contains(BaseSpireIds.Tank));
        Assert.Equal(BaseSpireIds.StrikeTag, cards.Single(card => card.Id == BaseSpireIds.StrikeIronclad)
            .Tags.Single());
        Assert.True(cards.Single(card => card.Id == BaseSpireIds.PerfectedStrike).Effect is
            ScaledAttackEffect { Count: TaggedCardCountSource });

        var strike = cards.Single(card => card.Id == BaseSpireIds.StrikeIronclad);
        Assert.Equal(TargetSelector.SelectedTarget, strike.Target);
        Assert.Equal(new AttackEffect(6, TargetSelector.SelectedTarget), strike.Effect);
        Assert.Equal(new AttackEffect(9, TargetSelector.SelectedTarget), strike.UpgradedEffect);
        var thunderclap = cards.Single(card => card.Id == BaseSpireIds.Thunderclap);
        Assert.Equal(TargetSelector.AllOpponents, thunderclap.Target);
        var whirlwind = cards.Single(card => card.Id == BaseSpireIds.Whirlwind);
        Assert.Equal(CardCostKind.X, whirlwind.CostKind);
        Assert.True(whirlwind.Effect is RepeatForXCostEffect
            { Effect: AttackEffect { Amount: 5, Target: TargetSelector.AllOpponents } });

        var compiledByPackage = Build(new BaseSpirePackage()).Cards;
        var packageDefinition = Build(new BaseSpirePackage());
        Assert.True(cards.All(card => compiledByPackage.ContainsKey(card.Id)));
        Assert.Equal(2m, packageDefinition.Powers[BaseSpireIds.TankPower].IncomingDamageModifiers.Single().Multiplier);
        Assert.Equal(strike.ResolveEffect(new CardInstance("dsl-strike", strike.Id)),
            compiledByPackage[strike.Id].ResolveEffect(new CardInstance("runtime-strike", strike.Id)));
        Assert.Equal(whirlwind.CostKind, compiledByPackage[whirlwind.Id].CostKind);

        var environment = new CombatEnvironment(Build(new BaseSpirePackage()));
        var unavailable = environment.StartEncounter(902, "ironclad", 56, 65,
            [BaseSpireIds.Tank, BaseSpireIds.DemonicShield], BaseSpireIds.TurretOperatorWeak);
        Assert.False(environment.LegalActions(unavailable).Any(action => action is PlayCardAction));

        var invalid = DslSourceParser.Parse(
            "package broken { card Bad { type = attack rarity = common cost = 1 target = enemy play = madeUp() } }",
            "broken.spire");
        Assert.Throws<DslSourceException>(() => SpireCombatDslCompiler.CompileCards(invalid));

        var parseError = Assert.Throws<DslSourceException>(() => DslSourceParser.Parse(
            "package broken {\n  enemy Missing {\n    hp =\n  }\n}", "broken.spire"));
        Assert.Equal(4, parseError.Location.Line);
    }

    public void CardPoolMembershipIsExplicitAndModsCanExtendExistingPools()
    {
        var definition = Build(new BaseSpirePackage(), new CardPoolExtensionPackage());
        var pool = definition.CardPools[BaseSpireIds.IroncladCardPool].Cards;
        Assert.Equal(86, pool.Count);
        Assert.Equal(CardPoolExtensionPackage.CardId, pool[^1]);
        Assert.False(pool.Contains(BaseSpireIds.Entrench));
        Assert.False(pool.Contains(BaseSpireIds.Slimed));

        var unknownPoolRegistry = new ContentRegistry();
        unknownPoolRegistry.AddPackage(new UnknownPoolCardPackage());
        Assert.Throws<InvalidOperationException>(() => unknownPoolRegistry.Build());
    }

    public void SpiteRepeatsAfterPlayerHpLossAndResetsOnTheNextTurn()
    {
        var definition = Build(new BaseSpirePackage());
        var spite = definition.Cards[BaseSpireIds.Spite];
        Assert.Equal(CardRarity.Uncommon, spite.Rarity);
        Assert.Equal(new IfEffect(new PlayerLostHpThisTurnPredicate(),
            new RepeatEffect(2, new AttackEffect(5, TargetSelector.SelectedTarget)),
            new AttackEffect(5, TargetSelector.SelectedTarget)), spite.Effect);

        var environment = new CombatEnvironment(definition);
        var session = environment.Start(76, "ironclad", 56, 65,
            [BaseSpireIds.Spite, BaseSpireIds.Bloodletting, BaseSpireIds.Spite, BaseSpireIds.Spite],
            BaseSpireIds.DampCultist.ToString(), enemyHp: 53, drawPile: [BaseSpireIds.Spite]);
        environment.Step(session, new PlayCardAction(0, session.State.Enemy.Id));
        Assert.Equal(48, session.State.Enemy.Hp);
        environment.Step(session, new PlayCardAction(0, session.State.Player.Id));
        Assert.Equal(53, session.State.Player.Creature.Hp);
        environment.Step(session, new PlayCardAction(0, session.State.Enemy.Id));
        Assert.Equal(38, session.State.Enemy.Hp);
        session.State.Player.MutableHand[0] = new CardInstance("spite+", BaseSpireIds.Spite, 1);
        environment.Step(session, new PlayCardAction(0, session.State.Enemy.Id));
        Assert.Equal(23, session.State.Enemy.Hp);

        environment.Step(session, new EndTurnAction());
        Assert.Equal(2, session.State.Turn);
        var nextSpiteIndex = session.State.Player.Hand.ToList().IndexOf(BaseSpireIds.Spite);
        Assert.True(nextSpiteIndex >= 0);
        environment.Step(session, new PlayCardAction(nextSpiteIndex, session.State.Enemy.Id));
        Assert.Equal(18, session.State.Enemy.Hp);

        var blocked = environment.Start(77, "ironclad", 56, 65, [],
            BaseSpireIds.DampCultist.ToString(), enemyHp: 53,
            drawPile: [BaseSpireIds.DefendIronclad, BaseSpireIds.DefendIronclad, BaseSpireIds.Spite]);
        environment.Step(blocked, new EndTurnAction());
        for (var i = 0; i < 2; i++)
        {
            var defendIndex = blocked.State.Player.Hand.ToList().IndexOf(BaseSpireIds.DefendIronclad);
            Assert.True(defendIndex >= 0);
            environment.Step(blocked, new PlayCardAction(defendIndex, blocked.State.Player.Id));
        }
        environment.Step(blocked, new EndTurnAction());
        Assert.Equal(56, blocked.State.Player.Creature.Hp);
        var blockedSpiteIndex = blocked.State.Player.Hand.ToList().IndexOf(BaseSpireIds.Spite);
        Assert.True(blockedSpiteIndex >= 0);
        environment.Step(blocked, new PlayCardAction(blockedSpiteIndex, blocked.State.Enemy.Id));
        Assert.Equal(48, blocked.State.Enemy.Hp);
    }

    public void PyreIncreasesTheNextTurnsMaximumEnergyThroughItsPower()
    {
        var definition = Build(new BaseSpirePackage());
        Assert.Equal(1, definition.Powers[BaseSpireIds.PyrePower].MaxEnergyPerStack);
        var environment = new CombatEnvironment(definition);
        var normal = environment.Start(78, "ironclad", 56, 65, [BaseSpireIds.Pyre],
            BaseSpireIds.DampCultist.ToString(), enemyHp: 53);
        Assert.Equal(3, environment.Observe(normal).MaxEnergy);
        environment.Step(normal, new PlayCardAction(0, normal.State.Player.Id));
        Assert.Equal(1, normal.State.Player.Energy);
        Assert.Equal(4, environment.Observe(normal).MaxEnergy);
        environment.Step(normal, new EndTurnAction());
        Assert.Equal(4, environment.Observe(normal).Energy);

        var upgraded = environment.Start(79, "ironclad", 56, 65, [BaseSpireIds.Pyre],
            BaseSpireIds.DampCultist.ToString(), enemyHp: 53);
        upgraded.State.Player.MutableHand[0] = new CardInstance("pyre+", BaseSpireIds.Pyre, 1);
        environment.Step(upgraded, new PlayCardAction(0, upgraded.State.Player.Id));
        Assert.Equal(2, upgraded.State.Player.Creature.PowerAmount(BaseSpireIds.PyrePower));
        Assert.Equal(5, environment.Observe(upgraded).MaxEnergy);
        environment.Step(upgraded, new EndTurnAction());
        Assert.Equal(5, environment.Observe(upgraded).Energy);
    }

    public void CrueltyAddsItsStacksToPoweredAttacksAgainstVulnerableTargets()
    {
        var definition = Build(new BaseSpirePackage());
        Assert.Equal(1, definition.Powers[BaseSpireIds.CrueltyPower].VulnerableMultiplierPercentPerStack);
        var environment = new CombatEnvironment(definition);
        var normal = environment.Start(80, "ironclad", 56, 65,
            [BaseSpireIds.Cruelty, BaseSpireIds.StrikeIronclad],
            BaseSpireIds.DampCultist.ToString(), enemyHp: 53);
        normal.State.Enemy.MutablePowers[BaseSpireIds.Vulnerable] = 1;
        environment.Step(normal, new PlayCardAction(0, normal.State.Player.Id));
        Assert.Equal(25, normal.State.Player.Creature.PowerAmount(BaseSpireIds.CrueltyPower));
        Assert.Equal(6, normal.State.CalculateDamage(normal, normal.State.Player.Creature,
            normal.State.Enemy, 6, DamageProperties.Unpowered));
        environment.Step(normal, new PlayCardAction(0, normal.State.Enemy.Id));
        Assert.Equal(43, normal.State.Enemy.Hp);

        var upgraded = environment.Start(81, "ironclad", 56, 65,
            [BaseSpireIds.Cruelty, BaseSpireIds.StrikeIronclad],
            BaseSpireIds.DampCultist.ToString(), enemyHp: 53);
        upgraded.State.Enemy.MutablePowers[BaseSpireIds.Vulnerable] = 1;
        upgraded.State.Player.MutableHand[0] = new CardInstance("cruelty+", BaseSpireIds.Cruelty, 1);
        environment.Step(upgraded, new PlayCardAction(0, upgraded.State.Player.Id));
        environment.Step(upgraded, new PlayCardAction(0, upgraded.State.Enemy.Id));
        Assert.Equal(41, upgraded.State.Enemy.Hp);

        var noVulnerable = environment.Start(82, "ironclad", 56, 65,
            [BaseSpireIds.Cruelty, BaseSpireIds.StrikeIronclad],
            BaseSpireIds.DampCultist.ToString(), enemyHp: 53);
        environment.Step(noVulnerable, new PlayCardAction(0, noVulnerable.State.Player.Id));
        environment.Step(noVulnerable, new PlayCardAction(0, noVulnerable.State.Enemy.Id));
        Assert.Equal(47, noVulnerable.State.Enemy.Hp);
    }

    public void HavocAutoPlaysFreeAndHonorsForcedExhaustionAndPowerPlacement()
    {
        var environment = new CombatEnvironment(Build(new BaseSpirePackage()));
        var attack = environment.Start(101, "ironclad", 56, 65, [BaseSpireIds.Havoc],
            BaseSpireIds.DampCultist.ToString(), enemyHp: 53, drawPile: [BaseSpireIds.Bludgeon]);
        environment.Step(attack, new PlayCardAction(0, attack.State.Player.Id));
        Assert.Equal(21, attack.State.Enemy.Hp);
        Assert.Equal(2, attack.State.Player.Energy);
        Assert.Equal(BaseSpireIds.Bludgeon, attack.State.Player.ExhaustPile.Single());
        Assert.Equal(BaseSpireIds.Havoc, attack.State.Player.DiscardPile.Single());
        Assert.Equal(1, attack.Random.Get("card-targets").Counter);
        Assert.Equal(0, attack.State.CardPlayFrames.Count);
        Assert.Equal(0, attack.State.Events.Count(gameEvent => gameEvent.Type == "draw"));

        var power = environment.Start(102, "ironclad", 56, 65, [BaseSpireIds.Havoc],
            BaseSpireIds.DampCultist.ToString(), drawPile: [BaseSpireIds.Inflame]);
        power.State.Player.MutableHand[0] = new CardInstance("havoc+", BaseSpireIds.Havoc, 1);
        environment.Step(power, new PlayCardAction(0, power.State.Player.Id));
        Assert.Equal(3, power.State.Player.Energy);
        Assert.Equal(2, power.State.Player.Creature.Strength);
        Assert.Equal(0, power.State.Player.ExhaustPile.Count);
        Assert.False(Enum.GetValues<CardPile>().Any(pile => power.State.GetPile(pile)
            .Any(card => card.CardId == BaseSpireIds.Inflame)));
        Assert.Equal(0, power.Random.Get("card-targets").Counter);

        var unplayable = environment.Start(103, "ironclad", 56, 65, [BaseSpireIds.Havoc],
            BaseSpireIds.DampCultist.ToString(), drawPile: [BaseSpireIds.Dazed]);
        environment.Step(unplayable, new PlayCardAction(0, unplayable.State.Player.Id));
        Assert.Equal(BaseSpireIds.Dazed, unplayable.State.Player.ExhaustPile.Single());
        Assert.False(unplayable.State.Events.Any(gameEvent => gameEvent.Type == "auto_play"));
    }

    public void CascadeReservesItsWholeBatchBeforeCardDrawsAndSupportsZeroXUpgrade()
    {
        var environment = new CombatEnvironment(Build(new BaseSpirePackage()));
        var session = environment.Start(104, "ironclad", 56, 65, [BaseSpireIds.Cascade],
            BaseSpireIds.DampCultist.ToString(), enemyHp: 53,
            drawPile: [BaseSpireIds.PommelStrike, BaseSpireIds.StrikeIronclad, BaseSpireIds.DefendIronclad, BaseSpireIds.Anger]);
        environment.Step(session, new PlayCardAction(0, session.State.Player.Id));
        Assert.Equal(38, session.State.Enemy.Hp);
        Assert.Equal(5, session.State.Player.Creature.Block);
        Assert.Equal(0, session.State.Player.Energy);
        Assert.Equal(BaseSpireIds.Anger, session.State.Player.Hand.Single());
        Assert.Equal(3, session.State.Events.Count(gameEvent => gameEvent.Type == "auto_play"));
        Assert.Equal(1, session.State.Events.Count(gameEvent => gameEvent.Type == "draw"));
        Assert.Equal(4, session.State.Player.DiscardPile.Count);
        Assert.Equal(0, session.State.Player.PlayPile.Count);
        Assert.Equal(2, session.Random.Get("card-targets").Counter);
        foreach (var upgraded in new[] { false, true })
        {
            var zero = environment.Start(105, "ironclad", 56, 65, [BaseSpireIds.Cascade],
                BaseSpireIds.DampCultist.ToString(), enemyHp: 53, drawPile: [BaseSpireIds.StrikeIronclad]);
            zero.State.Player.Energy = 0;
            if (upgraded) zero.State.Player.MutableHand[0] = new CardInstance("cascade+", BaseSpireIds.Cascade, 1);
            environment.Step(zero, new PlayCardAction(0, zero.State.Player.Id));
            Assert.Equal(upgraded ? 47 : 53, zero.State.Enemy.Hp);
            Assert.Equal(upgraded ? 0 : 1, zero.State.Player.DrawPile.Count);
        }
    }

    public void AutomaticPlayShufflesDuringReservationAndCapturesXWithoutSpendingEnergy()
    {
        var environment = new CombatEnvironment(Build(new BaseSpirePackage()));
        var batch = environment.Start(112, "ironclad", 56, 65, [BaseSpireIds.Cascade],
            BaseSpireIds.DampCultist.ToString(), enemyHp: 53);
        batch.State.Player.MutableDiscardPile.AddRange([
            new CardInstance("auto-shuffle-strike", BaseSpireIds.StrikeIronclad),
            new CardInstance("auto-shuffle-defend", BaseSpireIds.DefendIronclad)]);
        environment.Step(batch, new PlayCardAction(0, batch.State.Player.Id));
        Assert.Equal(2, batch.State.Events.Count(gameEvent => gameEvent.Type == "auto_play"));
        Assert.Equal(3, batch.State.Player.DiscardPile.Count);
        Assert.Equal(47, batch.State.Enemy.Hp);
        Assert.Equal(5, batch.State.Player.Creature.Block);
        Assert.Equal(1, batch.Random.Get("shuffle").Counter);

        var x = environment.Start(113, "ironclad", 56, 65, [BaseSpireIds.Havoc],
            BaseSpireIds.DampCultist.ToString(), enemyHp: 53, drawPile: [BaseSpireIds.Whirlwind]);
        x.State.ApplyPower(x, x.State.Player.Creature, BaseSpireIds.OneTwoPunchPower, 1, x.State.Player.Creature);
        environment.Step(x, new PlayCardAction(0, x.State.Player.Id));
        Assert.Equal(33, x.State.Enemy.Hp);
        Assert.Equal(2, x.State.Player.Energy);
        Assert.Equal(2, x.State.Events.Count(gameEvent => gameEvent.Type == "auto_play"));
        Assert.Equal(1, x.State.Events.Count(gameEvent => gameEvent.Type == "card_exhausted"));
        Assert.Equal(0, x.Random.Get("card-targets").Counter);
        Assert.Equal(0, x.State.CardPlayFrames.Count);

        var bottom = environment.Start(114, "ironclad", 56, 65, [], BaseSpireIds.DampCultist.ToString(),
            drawPile: [BaseSpireIds.StrikeIronclad, BaseSpireIds.DefendIronclad]);
        var effect = EffectDsl.Parse("""{"op":"autoPlayCards","from":"draw","count":1,"position":"bottom"}""");
        EffectExecutor.Execute(bottom, bottom.State.Player.Creature, effect);
        Assert.Equal(5, bottom.State.Player.Creature.Block);
        Assert.Equal(BaseSpireIds.StrikeIronclad, bottom.State.Player.DrawPile.Single());
        Assert.Equal(3, bottom.State.Player.Energy);
    }

    public void NestedAutoPlayChoiceRestoresParentXAndCompletesEachCardOnce()
    {
        var environment = new CombatEnvironment(Build(new BaseSpirePackage(), new AutoPlayContextFixturePackage()));
        var session = environment.Start(106, "ironclad", 56, 65, [AutoPlayContextFixturePackage.CardId],
            BaseSpireIds.DampCultist.ToString(), enemyHp: 53,
            drawPile: [BaseSpireIds.Havoc, BaseSpireIds.Whirlwind, BaseSpireIds.Headbutt, BaseSpireIds.StrikeIronclad]);
        var discarded = new CardInstance("nested-discard-defend", BaseSpireIds.DefendIronclad);
        session.State.Player.MutableDiscardPile.Add(discarded);
        var paused = environment.Step(session, new PlayCardAction(0, session.State.Player.Id));
        Assert.True(paused.NeedsChoice);
        Assert.Equal(3, session.State.CardPlayFrames.Count);
        Assert.Equal(4, session.State.Player.PlayPile.Count);
        Assert.Equal(0, session.State.CurrentCardPlay!.XEnergy);
        Assert.Equal(44, session.State.Enemy.Hp);
        var resumed = environment.Step(session, new ChooseCardAction(discarded.InstanceId));
        Assert.False(resumed.NeedsChoice);
        Assert.Equal(44, session.State.Enemy.Hp);
        Assert.Equal(3, session.State.Player.Creature.Block);
        Assert.Equal(0, session.State.Player.Energy);
        Assert.Equal(0, session.State.CardPlayFrames.Count);
        Assert.Equal(0, session.State.Player.PlayPile.Count);
        Assert.Equal(BaseSpireIds.Headbutt, session.State.Player.ExhaustPile.Single());
        Assert.Equal(3, session.State.Player.DiscardPile.Count);
        Assert.Equal(discarded.InstanceId, session.State.Player.DrawPileInstances[0].InstanceId);
        Assert.Equal(3, session.State.Events.Count(gameEvent => gameEvent.Type == "auto_play"));
        Assert.Equal(1, session.State.Events.Count(gameEvent => gameEvent.Type == "card_exhausted"));
        Assert.Equal(1, session.Random.Get("card-targets").Counter);
    }

    public void OneTwoPunchReplaysEffectsAndTriggersWhilePayingAndPlacingOnce()
    {
        var environment = new CombatEnvironment(Build(new BaseSpirePackage()));
        var session = environment.Start(107, "ironclad", 56, 65,
            [BaseSpireIds.Rage, BaseSpireIds.OneTwoPunch, BaseSpireIds.Anger, BaseSpireIds.StrikeIronclad],
            BaseSpireIds.DampCultist.ToString(), enemyHp: 53);
        session.State.Player.MutableHand[1] = new CardInstance("one-two+", BaseSpireIds.OneTwoPunch, 1);
        var anger = new CardInstance("anger-replayed+", BaseSpireIds.Anger, 1);
        session.State.Player.MutableHand[2] = anger;
        environment.Step(session, new PlayCardAction(0, session.State.Player.Id));
        environment.Step(session, new PlayCardAction(0, session.State.Player.Id));
        session.State.ApplyPower(session, session.State.Player.Creature, BaseSpireIds.FreeAttackPower, 2,
            session.State.Player.Creature);
        environment.Step(session, new PlayCardAction(0, session.State.Enemy.Id));
        Assert.Equal(37, session.State.Enemy.Hp);
        Assert.Equal(6, session.State.Player.Creature.Block);
        Assert.Equal(2, session.State.Player.Energy);
        Assert.Equal(1, session.State.Player.Creature.PowerAmount(BaseSpireIds.OneTwoPunchPower));
        Assert.Equal(0, session.State.Player.Creature.PowerAmount(BaseSpireIds.FreeAttackPower));
        var angers = session.State.Player.DiscardPileInstances.Where(card => card.CardId == BaseSpireIds.Anger).ToArray();
        Assert.Equal(3, angers.Length);
        Assert.True(angers.All(card => card.UpgradeLevel == 1));
        Assert.Equal(3, angers.Select(card => card.InstanceId).Distinct().Count());
        environment.Step(session, new PlayCardAction(0, session.State.Enemy.Id));
        Assert.Equal(25, session.State.Enemy.Hp);
        Assert.Equal(12, session.State.Player.Creature.Block);
        Assert.Equal(1, session.State.Player.Energy);
        Assert.Equal(0, session.State.Player.Creature.PowerAmount(BaseSpireIds.OneTwoPunchPower));
        Assert.Equal(1, session.State.Player.DiscardPileInstances.Count(card => card.CardId == BaseSpireIds.StrikeIronclad));
        Assert.Equal(0, session.State.CardPlayFrames.Count);

        var expiry = environment.Start(108, "ironclad", 56, 65, [BaseSpireIds.OneTwoPunch],
            BaseSpireIds.DampCultist.ToString());
        environment.Step(expiry, new PlayCardAction(0, expiry.State.Player.Id));
        environment.Step(expiry, new EndTurnAction());
        Assert.Equal(0, expiry.State.Player.Creature.PowerAmount(BaseSpireIds.OneTwoPunchPower));
    }

    public void ReplayedCardCanSuspendForEachExecutionWithoutRepeatingConsumption()
    {
        var environment = new CombatEnvironment(Build(new BaseSpirePackage()));
        var session = environment.Start(109, "ironclad", 56, 65, [BaseSpireIds.Headbutt],
            BaseSpireIds.DampCultist.ToString(), enemyHp: 53);
        session.State.ApplyPower(session, session.State.Player.Creature, BaseSpireIds.OneTwoPunchPower, 1,
            session.State.Player.Creature);
        session.State.ApplyPower(session, session.State.Player.Creature, BaseSpireIds.RagePower, 3,
            session.State.Player.Creature);
        var first = new CardInstance("replay-choice-first", BaseSpireIds.DefendIronclad);
        var second = new CardInstance("replay-choice-second", BaseSpireIds.DefendIronclad);
        session.State.Player.MutableDiscardPile.AddRange([first, second]);
        Assert.True(environment.Step(session, new PlayCardAction(0, session.State.Enemy.Id)).NeedsChoice);
        Assert.Equal(44, session.State.Enemy.Hp);
        Assert.Equal(0, session.State.Player.Creature.Block);
        Assert.True(environment.Step(session, new ChooseCardAction(first.InstanceId)).NeedsChoice);
        Assert.Equal(35, session.State.Enemy.Hp);
        Assert.Equal(3, session.State.Player.Creature.Block);
        Assert.False(environment.Step(session, new ChooseCardAction(second.InstanceId)).NeedsChoice);
        Assert.Equal(6, session.State.Player.Creature.Block);
        Assert.Equal(2, session.State.Player.Energy);
        Assert.Equal(1, session.State.Events.Count(gameEvent => gameEvent.Type == "power" && gameEvent.Amount == -1));
        Assert.Equal(BaseSpireIds.Headbutt, session.State.Player.DiscardPile.Single());
    }

    public void ReplayResolvesAnUpgradeAppliedDuringItsFirstExecution()
    {
        var environment = new CombatEnvironment(Build(new BaseSpirePackage(), new AutoPlayContextFixturePackage()));
        var session = environment.Start(115, "ironclad", 56, 65,
            [AutoPlayContextFixturePackage.UpgradingCardId], BaseSpireIds.DampCultist.ToString(), enemyHp: 53);
        session.State.ApplyPower(session, session.State.Player.Creature, BaseSpireIds.OneTwoPunchPower, 1,
            session.State.Player.Creature);
        var card = session.State.Player.HandInstances.Single();
        Assert.True(environment.Step(session, new PlayCardAction(0, session.State.Enemy.Id)).NeedsChoice);
        Assert.Equal(52, session.State.Enemy.Hp);
        var resumed = environment.Step(session, new ChooseCardAction(card.InstanceId));
        Assert.False(resumed.NeedsChoice);
        Assert.Equal(49, session.State.Enemy.Hp);
        Assert.Equal(1, session.State.Player.DiscardPileInstances.Single().UpgradeLevel);
        Assert.Equal(0, session.State.CardPlayFrames.Count);
    }

    public void DrumOfBattleExhaustCallbackUsesItsUpgradeAndWorksDuringAutomaticPlay()
    {
        var environment = new CombatEnvironment(Build(new BaseSpirePackage()));
        foreach (var upgraded in new[] { false, true })
        {
            var session = environment.Start(110, "ironclad", 56, 65, [BaseSpireIds.Havoc],
                BaseSpireIds.DampCultist.ToString(),
                drawPile: [BaseSpireIds.DrumOfBattle, BaseSpireIds.DefendIronclad, BaseSpireIds.StrikeIronclad]);
            if (upgraded) session.State.Player.MutableDrawPile[0] = new CardInstance("drum+", BaseSpireIds.DrumOfBattle, 1);
            environment.Step(session, new PlayCardAction(0, session.State.Player.Id));
            Assert.Equal(upgraded ? 5 : 4, session.State.Player.Energy);
            Assert.Equal(2, session.State.Player.Hand.Count);
            Assert.Equal(BaseSpireIds.DrumOfBattle, session.State.Player.ExhaustPile.Single());
            Assert.Equal(1, session.State.Events.Count(gameEvent => gameEvent.Type == "energy"));
            Assert.Equal(upgraded ? 3 : 2, session.State.Events.Single(gameEvent => gameEvent.Type == "energy").Amount);
        }
        var bulk = environment.Start(111, "ironclad", 56, 65, [BaseSpireIds.DrumOfBattle],
            BaseSpireIds.DampCultist.ToString());
        Assert.True(bulk.State.IsUpgradeable(bulk.State.Player.HandInstances[0]));
        EffectExecutor.Execute(bulk, bulk.State.Player.Creature, new MoveCardEffect(CardPile.Hand, CardPile.Exhaust));
        Assert.Equal(5, bulk.State.Player.Energy);
        Assert.Equal(1, bulk.State.Events.Count(gameEvent => gameEvent.Type == "energy"));
    }

    public void FiendFireCapturesOriginalHandAndCompletesExhaustCallbacksBeforeHits()
    {
        var registry = new ContentRegistry();
        registry.AddPackage(new BaseSpirePackage());
        var callbackCard = ContentId.Parse("count-fixture:draw-and-strength");
        registry.Register(new CardDefinition(callbackCard, 0, CardType.Skill, new SequenceEffect([]),
            Target: TargetSelector.Self)
        {
            OnExhaust = new SequenceEffect([new DrawCardsEffect(1),
                new ApplyPowerEffect(BaseSpireIds.Strength, 1)])
        });
        var environment = new CombatEnvironment(registry.Build());
        foreach (var upgrade in new[] { 0, 1 })
        {
            var session = environment.Start(112, "ironclad", 56, 65,
                [BaseSpireIds.FiendFire, callbackCard, callbackCard],
                BaseSpireIds.DampCultist.ToString(), enemyHp: 53,
                drawPile: [BaseSpireIds.StrikeIronclad, BaseSpireIds.DefendIronclad]);
            if (upgrade > 0) session.State.Player.MutableHand[0] = new CardInstance("fiend+", BaseSpireIds.FiendFire, 1);
            environment.Step(session, new PlayCardAction(0, session.State.Enemy.Id));
            Assert.Equal(upgrade > 0 ? 29 : 35, session.State.Enemy.Hp);
            Assert.Equal(1, session.State.Player.Energy);
            Assert.Equal(2, session.State.Player.Hand.Count);
            Assert.Equal(3, session.State.Player.ExhaustPile.Count);
            Assert.Equal(2, session.State.Events.Count(e => e.Type == "damage"));
            var originalExhausts = session.State.Events.Where(e => e.Type == "card_exhausted").Take(2).ToArray();
            var callbackDraws = session.State.Events.Where(e => e.Type == "draw").ToArray();
            Assert.True(originalExhausts[0].Sequence < callbackDraws[0].Sequence);
            Assert.True(callbackDraws[0].Sequence < originalExhausts[1].Sequence);
            Assert.True(originalExhausts[1].Sequence < callbackDraws[1].Sequence);
            var firstHit = session.State.Events.First(e => e.Type == "damage").Sequence;
            Assert.True(session.State.Events.Where(e => e.Type is "draw" or "power")
                .All(e => e.Sequence < firstHit));
            Assert.Equal(0, session.State.CardPlayFrames.Count);
        }
        var empty = environment.Start(113, "ironclad", 56, 65, [BaseSpireIds.FiendFire],
            BaseSpireIds.DampCultist.ToString(), enemyHp: 53);
        environment.Step(empty, new PlayCardAction(0, empty.State.Enemy.Id));
        Assert.Equal(53, empty.State.Enemy.Hp);
        Assert.Equal(1, empty.State.Player.ExhaustPile.Count);
        Assert.False(empty.State.Events.Any(e => e.Type == "damage"));
    }

    public void CountBindingsSurviveChoicesAndRestoreOuterLexicalScope()
    {
        var source = DslSourceParser.Parse("""
            package count-fixture {
              card Scoped { type = skill rarity = token cost = 1 target = self
                play = withCount("n", count=pileCount(hand), effect=sequence(
                  forEachCard(from=hand, effect=moveSelectedCard(from=hand, to=exhaust)),
                  choose(prompt="Continue", options=[option("Continue", effect=
                    modifyEnergy(scaled(0, 1, count=savedCount("n"))))]),
                  withCount("n", count=pileCount(hand), effect=
                    modifyEnergy(scaled(0, 1, count=savedCount("n")))),
                  chooseCard(from=exhaust, prompt="Recover", onSelected=sequence(
                    modifyEnergy(scaled(0, 1, count=savedCount("n"))),
                    moveSelectedCard(from=exhaust, to=hand))),
                  chooseCards(from=hand, prompt="Select", minChoices=1, maxChoices=1,
                    onSelected=modifyEnergy(scaled(0, 1, count=savedCount("n")))),
                  repeatForCount(0, 1, count=savedCount("n"), effect=gainBlock(1))
                )) }
            }
            """);
        var registry = new ContentRegistry();
        registry.AddPackage(new BaseSpirePackage());
        foreach (var card in SpireCombatDslCompiler.CompileCards(source))
            registry.Register(card with { Effect = EffectDsl.Parse(EffectDsl.Serialize(card.Effect)) });
        var environment = new CombatEnvironment(registry.Build());
        var session = environment.Start(114, "ironclad", 56, 65,
            [ContentId.Parse("count-fixture:scoped"), BaseSpireIds.StrikeIronclad, BaseSpireIds.DefendIronclad],
            BaseSpireIds.DampCultist.ToString());
        Assert.True(environment.Step(session, new PlayCardAction(0, session.State.Player.Id)).NeedsChoice);
        Assert.Equal(2, session.State.Player.Energy);
        Assert.True(environment.Step(session, new ChooseOptionAction(0)).NeedsChoice);
        Assert.Equal(4, session.State.Player.Energy); // Inner hand count is zero; outer count stays two.
        var recovered = session.State.Player.ExhaustPileInstances[0].InstanceId;
        Assert.True(environment.Step(session, new ChooseCardAction(recovered)).NeedsChoice);
        Assert.Equal(6, session.State.Player.Energy);
        environment.Step(session, new ChooseCardAction(recovered));
        var finished = environment.Step(session, new ConfirmCardSelectionAction());
        Assert.False(finished.NeedsChoice);
        Assert.Equal(8, session.State.Player.Energy);
        Assert.Equal(2, session.State.Player.Creature.Block);
        Assert.Equal(0, session.State.PendingEffects.Count);
        Assert.True(session.State.PendingChoiceCounts is null);
    }

    public void CountBindingsStayWithParentAcrossNestedAutomaticCardChoices()
    {
        var registry = new ContentRegistry();
        registry.AddPackage(new BaseSpirePackage());
        var source = DslSourceParser.Parse("""
            package count-fixture {
              card Parent { type = skill rarity = token cost = 1 target = self
                play = withCount("n", count=pileCount(hand), effect=sequence(
                  autoPlay(from=draw, count=scaled(0, 1, count=savedCount("n"))),
                  modifyEnergy(scaled(0, 1, count=savedCount("n"))),
                  repeatForCount(0, 1, count=savedCount("n"), effect=gainBlock(1)))) }
              card Child { type = skill rarity = token cost = 0 target = self
                play = withCount("n", count=pileCount(exhaust), effect=
                  choose(prompt="Child", options=[option("Continue", effect=
                    modifyEnergy(scaled(0, 1, count=savedCount("n"))))])) }
            }
            """);
        foreach (var card in SpireCombatDslCompiler.CompileCards(source)) registry.Register(card);
        var environment = new CombatEnvironment(registry.Build());
        var session = environment.Start(119, "player", 56, 65,
            [ContentId.Parse("count-fixture:parent"), BaseSpireIds.StrikeIronclad, BaseSpireIds.DefendIronclad],
            BaseSpireIds.DampCultist.ToString(), enemyHp: 53,
            drawPile: [ContentId.Parse("count-fixture:child"), BaseSpireIds.StrikeIronclad]);
        Assert.True(environment.Step(session, new PlayCardAction(0, session.State.Player.Id)).NeedsChoice);
        Assert.Equal(2, session.State.CardPlayFrames.Count);
        Assert.Equal(2, session.State.Player.Energy);
        var completed = environment.Step(session, new ChooseOptionAction(0));
        Assert.False(completed.NeedsChoice);
        Assert.Equal(4, session.State.Player.Energy);
        Assert.Equal(2, session.State.Player.Creature.Block);
        Assert.Equal(47, session.State.Enemy.Hp);
        Assert.Equal(0, session.State.CardPlayFrames.Count);
    }

    public void CountBindingsRejectUnboundAndOutOfScopeReferences()
    {
        foreach (var effect in new EffectNode[] {
            new RepeatForCountEffect(0, 1, new SavedCountSource("missing"), new GainBlockEffect(1)),
            new SequenceEffect([
                new WithCountEffect("n", new PileCardCountSource(CardPile.Hand), new GainBlockEffect(1)),
                new ModifyEnergyEffect(0, new ScaledCombatAmount(0, 1, new SavedCountSource("n")))
            ]),
            new WithCountEffect("n", new SavedCountSource("n"), new GainBlockEffect(1)),
            new WithCountEffect("n", new PileCardCountSource(CardPile.Hand),
                new ChooseCardFromPileEffect(CardPile.Hand, null, "Choose",
                    OnSelected: new ModifyEnergyEffect(0, new ScaledCombatAmount(0, 1, new SavedCountSource("other")))))
        })
        {
            var registry = new ContentRegistry();
            registry.AddPackage(new BaseSpirePackage());
            registry.Register(new CardDefinition(ContentId.Parse("count-fixture:invalid"), 0, CardType.Skill, effect));
            Assert.Throws<InvalidOperationException>(() => registry.Build());
        }
    }

    public void StokeUsesOwnerPoolAndOriginalCountDespiteExhaustDraws()
    {
        var registry = new ContentRegistry();
        registry.AddPackage(new BaseSpirePackage());
        var drawCard = ContentId.Parse("generation-fixture:exhaust-draw");
        registry.Register(new CardDefinition(drawCard, 0, CardType.Skill, new SequenceEffect([]),
            Target: TargetSelector.Self) { OnExhaust = new DrawCardsEffect(1) });
        var poolId = ContentId.Parse("generation-fixture:pool");
        var characterId = ContentId.Parse("generation-fixture:character");
        registry.Register(new CardPoolDefinition(poolId,
            [BaseSpireIds.StrikeIronclad, BaseSpireIds.Corruption, BaseSpireIds.Entrench,
                BaseSpireIds.NotYet, BaseSpireIds.Anger]));
        registry.Register(new CharacterDefinition(characterId, 65, 0, [], cardPools: [poolId]));
        var definition = registry.Build();
        var stoke = definition.Cards[BaseSpireIds.Stoke];
        registry.Override(stoke with { Effect = EffectDsl.Parse(EffectDsl.Serialize(stoke.Effect)),
            UpgradedEffect = EffectDsl.Parse(EffectDsl.Serialize(stoke.UpgradedEffect!)) });
        var environment = new CombatEnvironment(registry.Build());
        foreach (var upgrade in new[] { 0, 1 })
        {
            var session = environment.Start(115, "arbitrary-player-id", 56, 65,
                [BaseSpireIds.Stoke, drawCard, drawCard], BaseSpireIds.DampCultist.ToString(),
                drawPile: [BaseSpireIds.StrikeIronclad, BaseSpireIds.DefendIronclad], characterId: characterId);
            if (upgrade > 0) session.State.Player.MutableHand[0] = new CardInstance("stoke+", BaseSpireIds.Stoke, 1);
            var originalIds = session.State.Player.HandInstances.Skip(1).Select(c => c.InstanceId).ToArray();
            environment.Step(session, new PlayCardAction(0, session.State.Player.Id));
            Assert.Equal(2, session.State.Player.Energy);
            Assert.Equal(characterId, environment.Observe(session).CharacterId!.Value);
            Assert.Equal(4, session.State.Player.Hand.Count);
            Assert.Equal(originalIds, session.State.Player.ExhaustPileInstances.Select(c => c.InstanceId));
            var generated = session.State.Player.HandInstances.Where(c => c.CardId == BaseSpireIds.Anger).ToArray();
            Assert.Equal(2, generated.Length); // Sampling with replacement permits duplicates.
            Assert.True(generated.All(c => c.UpgradeLevel == upgrade));
            Assert.Equal(2, generated.Select(c => c.InstanceId).Distinct().Count());
            Assert.Equal(2, session.Random.Get("combat-card-generation").Counter);
            var firstGenerated = session.State.Events.First(e => e.Type == "card_generated").Sequence;
            Assert.True(session.State.Events.Where(e => e.Type is "card_exhausted" or "draw")
                .All(e => e.Sequence < firstGenerated));
            Assert.Equal(BaseSpireIds.Stoke, session.State.Player.DiscardPile.Single());
        }
        var empty = environment.Start(116, "arbitrary-player-id", 56, 65, [BaseSpireIds.Stoke],
            BaseSpireIds.DampCultist.ToString());
        environment.Step(empty, new PlayCardAction(0, empty.State.Player.Id));
        Assert.Equal(0, empty.Random.Get("combat-card-generation").Counter);
        Assert.Equal(0, empty.State.Player.Hand.Count);
    }

    public void GeneratedCardsReserveRandomBatchAndSuspendBetweenInsertionEvents()
    {
        var registry = new ContentRegistry();
        registry.AddPackage(new BaseSpirePackage());
        registry.Register(new GenerationChoiceHook());
        var firstPool = ContentId.Parse("generation-fixture:first-pool");
        var secondPool = ContentId.Parse("generation-fixture:second-pool");
        var character = ContentId.Parse("generation-fixture:character");
        registry.Register(new CardPoolDefinition(firstPool, [BaseSpireIds.Anger]));
        registry.Register(new CardPoolDefinition(secondPool, [BaseSpireIds.Anger, BaseSpireIds.PommelStrike]));
        registry.Register(new CharacterDefinition(character, 65, 0, [], cardPools: [firstPool, secondPool]));
        var environment = new CombatEnvironment(registry.Build());
        var session = environment.Start(117, "player", 56, 65, [], BaseSpireIds.DampCultist.ToString(),
            characterId: character);
        var expectedRandom = new RandomStreams(117).Get("combat-card-generation");
        var expected = Enumerable.Range(0, 3).Select(_ => new[] { BaseSpireIds.Anger, BaseSpireIds.PommelStrike }
            [expectedRandom.NextInt(2)]).ToArray();
        EffectExecutor.Execute(session, session.State.Player.Creature, new GenerateCombatCardsEffect(3));
        Assert.Equal(3, session.Random.Get("combat-card-generation").Counter);
        for (var inserted = 1; inserted <= 3; inserted++)
        {
            Assert.True(session.State.PendingChoice is not null);
            Assert.Equal(inserted, session.State.Player.Hand.Count);
            Assert.Equal(inserted - 1, session.State.Player.Creature.Block);
            environment.Step(session, new ChooseOptionAction(0));
        }
        Assert.Equal(expected, session.State.Player.HandInstances.Select(c => c.CardId));
        Assert.Equal(3, session.State.Player.Creature.Block);
        Assert.True(session.State.PendingChoice is null);
        Assert.Equal(0, session.State.PendingEffects.Count);
        Assert.Equal(0, session.Random.Get("shuffle").Counter);
        Assert.Equal(0, session.Random.Get("card-targets").Counter);
    }

    private sealed class GenerationChoiceHook : IRuleHook
    {
        public ContentId Id => ContentId.Parse("generation-fixture:choice-hook");
        public void OnEvent(RuleHookContext context, GameEvent gameEvent) { }
        public GameEventEffect? OnEventEffect(RuleHookContext context, GameEvent gameEvent) =>
            gameEvent.Type == "card_generated" ? new GameEventEffect(new ChooseEffect("Generated card",
                [new ChoiceOption("Continue", new GainBlockEffect(1))]), GameEventActor.Player) : null;
    }

    public void CombatGenerationRequiresExplicitCharacterAndEligiblePool()
    {
        var registry = new ContentRegistry();
        registry.AddPackage(new BaseSpirePackage());
        var characterId = ContentId.Parse("generation-fixture:empty-character");
        registry.Register(new CharacterDefinition(characterId, 65, 0, []));
        var environment = new CombatEnvironment(registry.Build());
        foreach (var character in new ContentId?[] { null, characterId })
        {
            var session = environment.Start(118, "ironclad", 56, 65, [], BaseSpireIds.DampCultist.ToString(),
                characterId: character);
            Assert.Throws<InvalidOperationException>(() => EffectExecutor.Execute(session,
                session.State.Player.Creature, new GenerateCombatCardsEffect(1)));
            Assert.Equal(0, session.Random.Get("combat-card-generation").Counter);
            Assert.Equal(0, session.State.Player.Hand.Count);
        }
        Assert.Throws<KeyNotFoundException>(() => environment.Start(118, "player", 56, 65, [],
            BaseSpireIds.DampCultist.ToString(), characterId: ContentId.Parse("generation-fixture:unknown")));
        var invalid = new ContentRegistry();
        invalid.AddPackage(new BaseSpirePackage());
        invalid.Register(new CardDefinition(ContentId.Parse("generation-fixture:invalid"), 0, CardType.Skill,
            new GenerateCombatCardsEffect(1, CardPile.Play)));
        Assert.Throws<InvalidOperationException>(() => invalid.Build());
    }

    private sealed class AutoPlayContextFixturePackage : IRulePackage
    {
        public static readonly ContentId CardId = ContentId.Parse("autoplay-context:parent-x");
        public static readonly ContentId UpgradingCardId = ContentId.Parse("autoplay-context:upgrade-during-play");
        public PackageInfo Info { get; } = new("autoplay-context", "1.0.0");
        public void Register(ContentRegistry registry)
        {
            var source = DslSourceParser.Parse("""
                package autoplay-context {
                  card ParentX { type = skill rarity = token cost = 0 costKind = x target = self
                    play = sequence(autoPlay(from=draw, count=2), repeatForX(gainBlock(1))) }
                  card UpgradeDuringPlay { type = attack rarity = token cost = 1 target = enemy
                    play = sequence(attack(1), chooseCard(from=play, prompt="Upgrade this card",
                      filter=upgradeable, onSelected=upgradeSelectedCard()))
                    upgradedPlay = attack(3) }
                }
                """);
            foreach (var card in SpireCombatDslCompiler.CompileCards(source)) registry.Register(card);
        }
    }

    public void CorruptionSharesCostRulesAndSnapshotsExhaustionAcrossCardChoices()
    {
        var environment = new CombatEnvironment(Build(new BaseSpirePackage()));
        var session = environment.Start(91, "ironclad", 56, 65,
            [BaseSpireIds.Corruption, BaseSpireIds.BurningPact, BaseSpireIds.DefendIronclad,
                BaseSpireIds.Impervious, BaseSpireIds.StrikeIronclad, BaseSpireIds.Inflame],
            BaseSpireIds.DampCultist.ToString());
        var corruption = session.State.Player.HandInstances[0];
        environment.Step(session, new PlayCardAction(0, session.State.Player.Id));
        Assert.Equal(0, session.State.Player.Energy);
        Assert.False(Enum.GetValues<CardPile>().Any(pile => session.State.GetPile(pile).Contains(corruption)));
        var previews = environment.Observe(session).HandPlayPreviews;
        Assert.Equal(0, previews[0].EnergyToSpend);
        Assert.Equal((CardPile?)CardPile.Exhaust, previews[0].ResultPile);
        Assert.Equal(0, previews[2].EnergyToSpend);
        Assert.Equal(1, previews[3].EnergyToSpend);
        Assert.False(environment.LegalActions(session).OfType<PlayCardAction>().Any(action => action.HandIndex == 3));
        session.State.ApplyPower(session, session.State.Player.Creature, BaseSpireIds.FeelNoPain, 3,
            session.State.Player.Creature);
        var pact = session.State.Player.HandInstances[0];
        var defend = session.State.Player.HandInstances[1];
        var paused = environment.Step(session, new PlayCardAction(0, session.State.Player.Id));
        Assert.True(paused.NeedsChoice);
        Assert.True(session.State.Player.PlayPileInstances.Contains(pact));
        session.State.RemovePower(session.State.Player.Creature, BaseSpireIds.CorruptionPower);
        environment.Step(session, new ChooseCardAction(defend.InstanceId));
        Assert.Equal(0, session.State.Player.Energy);
        Assert.True(session.State.Player.ExhaustPileInstances.Contains(pact));
        Assert.Equal(1, session.State.Events.Count(gameEvent => gameEvent.Type == "card_exhausted"
            && gameEvent.Target == pact.InstanceId));
        Assert.Equal(6, session.State.Player.Creature.Block);
        Assert.Equal(0, session.State.Player.PlayPileInstances.Count);
        Assert.Equal(2, session.State.Definition.Cards[BaseSpireIds.Corruption]
            .ResolveCost(new CardInstance("corruption+", BaseSpireIds.Corruption, 1)));
    }

    public void FreeAttackConsumesOnlyMatchingPlaysAndPreservesXCostSpending()
    {
        var environment = new CombatEnvironment(Build(new BaseSpirePackage()));
        var session = environment.Start(92, "ironclad", 56, 65,
            [BaseSpireIds.Unrelenting, BaseSpireIds.DefendIronclad, BaseSpireIds.Bash, BaseSpireIds.StrikeIronclad],
            BaseSpireIds.DampCultist.ToString(), enemyHp: 53);
        environment.Step(session, new PlayCardAction(0, session.State.Enemy.Id));
        Assert.Equal(39, session.State.Enemy.Hp);
        Assert.Equal(1, session.State.Player.Energy);
        Assert.Equal(0, environment.Observe(session).HandPlayPreviews[1].EnergyToSpend);
        environment.Step(session, new PlayCardAction(0, session.State.Player.Id));
        Assert.Equal(1, session.State.Player.Creature.PowerAmount(BaseSpireIds.FreeAttackPower));
        environment.Step(session, new PlayCardAction(0, session.State.Enemy.Id));
        Assert.Equal(31, session.State.Enemy.Hp);
        Assert.Equal(0, session.State.Player.Energy);
        Assert.Equal(0, session.State.Player.Creature.PowerAmount(BaseSpireIds.FreeAttackPower));
        Assert.Equal(1, environment.Observe(session).HandPlayPreviews.Single().EnergyToSpend);
        Assert.False(environment.LegalActions(session).OfType<PlayCardAction>().Any());
        var x = environment.Start(93, "ironclad", 56, 65,
            [BaseSpireIds.Unrelenting, BaseSpireIds.Whirlwind], BaseSpireIds.DampCultist.ToString(), enemyHp: 53);
        x.State.Player.MutableHand[0] = new CardInstance("unrelenting+", BaseSpireIds.Unrelenting, 1);
        environment.Step(x, new PlayCardAction(0, x.State.Enemy.Id));
        Assert.Equal(33, x.State.Enemy.Hp);
        var preview = environment.Observe(x).HandPlayPreviews.Single();
        Assert.Equal(1, preview.EnergyToSpend);
        Assert.Equal(1, preview.XEnergy);
        environment.Step(x, new PlayCardAction(0));
        Assert.Equal(28, x.State.Enemy.Hp);
        Assert.Equal(0, x.State.Player.Energy);
        Assert.Equal(0, x.State.Player.Creature.PowerAmount(BaseSpireIds.FreeAttackPower));

        var choice = environment.Start(97, "ironclad", 56, 65,
            [BaseSpireIds.Unrelenting, BaseSpireIds.Headbutt], BaseSpireIds.DampCultist.ToString());
        var unrelenting = choice.State.Player.HandInstances[0];
        environment.Step(choice, new PlayCardAction(0, choice.State.Enemy.Id));
        Assert.True(environment.Step(choice, new PlayCardAction(0, choice.State.Enemy.Id)).NeedsChoice);
        Assert.Equal(0, choice.State.Player.Creature.PowerAmount(BaseSpireIds.FreeAttackPower));
        environment.Step(choice, new ChooseCardAction(unrelenting.InstanceId));
        Assert.Equal(1, choice.State.Events.Count(gameEvent => gameEvent.Type == "power" && gameEvent.Amount == -1));
        Assert.Equal(1, choice.State.Player.Energy);

        var skillEnvironment = new CombatEnvironment(Build(new BaseSpirePackage(), new XSkillRuleFixturePackage()));
        var skill = skillEnvironment.Start(98, "ironclad", 56, 65,
            [XSkillRuleFixturePackage.CardId], BaseSpireIds.DampCultist.ToString());
        skill.State.ApplyPower(skill, skill.State.Player.Creature, BaseSpireIds.CorruptionPower, 1,
            skill.State.Player.Creature);
        Assert.Equal(3, skillEnvironment.Observe(skill).HandPlayPreviews.Single().EnergyToSpend);
        skillEnvironment.Step(skill, new PlayCardAction(0, skill.State.Player.Id));
        Assert.Equal(0, skill.State.Player.Energy);
        Assert.Equal(3, skill.State.Player.Creature.Block);
        Assert.Contains(skill.State.Player.ExhaustPileInstances, card => card.CardId == XSkillRuleFixturePackage.CardId);
    }

    private sealed class XSkillRuleFixturePackage : IRulePackage
    {
        public static readonly ContentId CardId = ContentId.Parse("card-rule-fixture:x-guard");
        public PackageInfo Info { get; } = new("card-rule-fixture", "1.0.0");
        public void Register(ContentRegistry registry)
        {
            var source = DslSourceParser.Parse("""
                package card-rule-fixture {
                  card XGuard { type = skill rarity = token cost = 0 costKind = x target = self
                    play = repeatForX(gainBlock(1)) }
                }
                """);
            foreach (var card in SpireCombatDslCompiler.CompileCards(source)) registry.Register(card);
        }
    }

    public void ExpectAFightCountsHandAttacksAndLocksPositiveEnergyUntilTurnEnd()
    {
        var environment = new CombatEnvironment(Build(new BaseSpirePackage()));
        foreach (var upgraded in new[] { false, true })
        {
            var session = environment.Start(94, "ironclad", 56, 65,
                [BaseSpireIds.ExpectAFight, BaseSpireIds.StrikeIronclad, BaseSpireIds.Bash, BaseSpireIds.DefendIronclad],
                BaseSpireIds.DampCultist.ToString());
            if (upgraded) session.State.Player.MutableHand[0] = new CardInstance("expect+", BaseSpireIds.ExpectAFight, 1);
            environment.Step(session, new PlayCardAction(0, session.State.Player.Id));
            Assert.Equal(upgraded ? 4 : 3, session.State.Player.Energy);
            Assert.Equal(1, session.State.Events.Count(gameEvent => gameEvent.Type == "energy"));
            Assert.Equal(2, session.State.Events.Single(gameEvent => gameEvent.Type == "energy").Amount);
            EffectExecutor.Execute(session, session.State.Player.Creature, new ModifyEnergyEffect(2));
            Assert.Equal(upgraded ? 4 : 3, session.State.Player.Energy);
            Assert.Equal(0, session.State.Events.Last(gameEvent => gameEvent.Type == "energy").Amount);
            EffectExecutor.Execute(session, session.State.Player.Creature, new ModifyEnergyEffect(-1));
            Assert.Equal(upgraded ? 3 : 2, session.State.Player.Energy);
            environment.Step(session, new EndTurnAction());
            Assert.Equal(0, session.State.Player.Creature.PowerAmount(BaseSpireIds.NoEnergyGainPower));
            Assert.Equal(3, session.State.Player.Energy);
            EffectExecutor.Execute(session, session.State.Player.Creature, new ModifyEnergyEffect(2));
            Assert.Equal(5, session.State.Player.Energy);
        }
    }

    public void TearAsunderCountsPositiveHpLossAcrossTurnsAndRepeatCountsAreCapturedOnce()
    {
        var definition = Build(new BaseSpirePackage());
        var environment = new CombatEnvironment(definition);
        var session = environment.Start(95, "ironclad", 56, 65,
            [BaseSpireIds.TearAsunder, BaseSpireIds.TearAsunder], BaseSpireIds.DampCultist.ToString(), enemyHp: 53);
        session.State.Player.Creature.Block = 5;
        EffectExecutor.Execute(session, session.State.Enemy, new AttackEffect(5));
        EffectExecutor.Execute(session, session.State.Player.Creature, new LoseHpEffect(2));
        EffectExecutor.Execute(session, session.State.Enemy, new AttackEffect(1));
        Assert.Equal(2, session.State.PlayerHpLossCount);
        var second = new CardInstance(session.State.Player.HandInstances[1].InstanceId, BaseSpireIds.TearAsunder, 1);
        session.State.Player.MutableHand[1] = second;
        environment.Step(session, new PlayCardAction(0, session.State.Enemy.Id));
        Assert.Equal(38, session.State.Enemy.Hp);
        environment.Step(session, new EndTurnAction());
        Assert.Equal(2, session.State.PlayerHpLossCount);
        var index = session.State.Player.HandInstances.ToList().FindIndex(card => card.InstanceId == second.InstanceId);
        environment.Step(session, new PlayCardAction(index, session.State.Enemy.Id));
        Assert.Equal(17, session.State.Enemy.Hp);

        var snapshot = environment.Start(96, "ironclad", 56, 65,
            [BaseSpireIds.StrikeIronclad, BaseSpireIds.Bash], BaseSpireIds.DampCultist.ToString(),
            drawPile: [BaseSpireIds.DefendIronclad, BaseSpireIds.DefendIronclad, BaseSpireIds.StrikeIronclad]);
        EffectExecutor.Execute(snapshot, snapshot.State.Player.Creature,
            new RepeatForCountEffect(0, 1, new PileCardCountSource(CardPile.Hand), new DrawCardsEffect(1)));
        Assert.Equal(4, snapshot.State.Player.Hand.Count);
        Assert.Equal(1, snapshot.State.Player.DrawPile.Count);
        Assert.True(EffectDsl.Parse("""{"op":"repeatForCount","baseCount":1,"perCount":1,"count":{"kind":"playerHpLossCount"},"effect":{"op":"attack","amount":5}}""") is RepeatForCountEffect);
        var invalid = DslSourceParser.Parse("""
            package invalid { power Rule { cardRule = cardRule(type=skill, cost=-1) } }
            """);
        Assert.Throws<DslSourceException>(() => SpireCombatBehaviorDslCompiler.Compile(invalid));
    }

    public void SetupStrikeStacksTemporaryStrengthAndPreservesPermanentStrengthAtExpiry()
    {
        var environment = new CombatEnvironment(Build(new BaseSpirePackage()));
        var session = environment.Start(83, "ironclad", 56, 65,
            [BaseSpireIds.SetupStrike, BaseSpireIds.SetupStrike, BaseSpireIds.StrikeIronclad],
            BaseSpireIds.DampCultist.ToString(), enemyHp: 53);
        session.State.ApplyPower(session, session.State.Player.Creature, BaseSpireIds.Strength, 2,
            session.State.Player.Creature);
        session.State.Player.MutableHand[1] = new CardInstance("setup-strike+", BaseSpireIds.SetupStrike, 1);
        environment.Step(session, new PlayCardAction(0, session.State.Enemy.Id));
        Assert.Equal(44, session.State.Enemy.Hp);
        Assert.Equal(4, session.State.Player.Creature.Strength);
        environment.Step(session, new PlayCardAction(0, session.State.Enemy.Id));
        Assert.Equal(31, session.State.Enemy.Hp);
        Assert.Equal(5, session.State.Player.Creature.PowerAmount(BaseSpireIds.SetupStrikePower));
        Assert.Equal(7, environment.Observe(session).PlayerPowers[BaseSpireIds.Strength]);
        environment.Step(session, new PlayCardAction(0, session.State.Enemy.Id));
        Assert.Equal(18, session.State.Enemy.Hp);
        environment.Step(session, new EndTurnAction());
        Assert.Equal(2, session.State.Player.Creature.Strength);
        Assert.Equal(2, environment.Observe(session).PlayerPowers[BaseSpireIds.Strength]);
        Assert.False(session.State.Player.Creature.Powers.ContainsKey(BaseSpireIds.SetupStrikePower));
        Assert.True(session.State.Definition.Cards[BaseSpireIds.SetupStrike].Tags.Contains(BaseSpireIds.StrikeTag));
    }

    public void MangleSuppressesEnemyStrengthThroughItsActionThenRestoresIt()
    {
        var environment = new CombatEnvironment(Build(new BaseSpirePackage()));
        var darkStrike = ContentId.Parse("base-spire:damp-cultist-dark-strike");
        var session = environment.StartEncounter(84, "ironclad", 56, 65, [BaseSpireIds.Mangle],
            [new CombatEnemySpawn(BaseSpireIds.DampCultist.ToString(), 53, StartingActionOverride: darkStrike)]);
        session.State.ApplyPower(session, session.State.Enemy, BaseSpireIds.Strength, 4,
            session.State.Enemy);
        environment.Step(session, new PlayCardAction(0, session.State.Enemy.Id));
        Assert.Equal(38, session.State.Enemy.Hp);
        Assert.Equal(-6, session.State.Enemy.Strength);
        Assert.Equal(0, environment.Observe(session).IntentDamage);
        environment.Step(session, new EndTurnAction());
        Assert.Equal(56, session.State.Player.Creature.Hp);
        Assert.Equal(4, session.State.Enemy.Strength);
        Assert.Equal(4, session.State.Enemy.PowerAmount(BaseSpireIds.Strength));
        Assert.False(session.State.Enemy.Powers.ContainsKey(BaseSpireIds.ManglePower));
        Assert.Equal(5, environment.Observe(session).IntentDamage);
        environment.Step(session, new EndTurnAction());
        Assert.Equal(51, session.State.Player.Creature.Hp);

        var upgraded = environment.StartEncounter(85, "ironclad", 56, 65, [BaseSpireIds.Mangle],
            [new CombatEnemySpawn(BaseSpireIds.DampCultist.ToString(), 53, StartingActionOverride: darkStrike)]);
        upgraded.State.Player.MutableHand[0] = new CardInstance("mangle+", BaseSpireIds.Mangle, 1);
        environment.Step(upgraded, new PlayCardAction(0, upgraded.State.Enemy.Id));
        Assert.Equal(33, upgraded.State.Enemy.Hp);
        Assert.Equal(-15, upgraded.State.Enemy.Strength);
        environment.Step(upgraded, new EndTurnAction());
        Assert.Equal(0, upgraded.State.Enemy.Strength);
        Assert.Equal(56, upgraded.State.Player.Creature.Hp);

        var lethal = environment.Start(86, "ironclad", 56, 65, [BaseSpireIds.Mangle],
            BaseSpireIds.DampCultist.ToString(), enemyHp: 15);
        environment.Step(lethal, new PlayCardAction(0, lethal.State.Enemy.Id));
        Assert.True(lethal.State.IsTerminal);
        Assert.False(lethal.State.Events.Any(gameEvent => gameEvent.Type == "power"));
    }

    public void PowerLifecycleUsesSignedDeltasAndRejectsRecursiveLinks()
    {
        var environment = new CombatEnvironment(Build(new BaseSpirePackage()));
        var session = environment.Start(87, "ironclad", 56, 65, [], BaseSpireIds.DampCultist.ToString());
        var player = session.State.Player.Creature;
        session.State.ApplyPower(session, player, BaseSpireIds.Strength, 2, player);
        session.State.ApplyPower(session, player, BaseSpireIds.SetupStrikePower, 4, player);
        session.State.DecrementPower(session, player, BaseSpireIds.SetupStrikePower);
        Assert.Equal(3, player.PowerAmount(BaseSpireIds.SetupStrikePower));
        Assert.Equal(5, player.Strength);
        session.State.ExpirePower(session, player, BaseSpireIds.SetupStrikePower);
        Assert.Equal(2, player.Strength);
        session.State.ExpirePower(session, player, BaseSpireIds.SetupStrikePower);
        Assert.Equal(2, player.Strength);
        session.State.ApplyPower(session, player, BaseSpireIds.SetupStrikePower, 3, player);
        session.State.ApplyPower(session, player, BaseSpireIds.SetupStrikePower, -3, player);
        Assert.Equal(2, player.Strength);
        Assert.False(player.Powers.ContainsKey(BaseSpireIds.SetupStrikePower));
        session.State.ApplyPower(session, player, BaseSpireIds.SetupStrikePower, 4, player);
        session.State.RemovePower(player, BaseSpireIds.SetupStrikePower);
        Assert.Equal(6, player.Strength);

        var modifiedEnvironment = new CombatEnvironment(Build(new BaseSpirePackage(),
            new TestHookPackage(new TemporaryStackDeltaHook())));
        var modified = modifiedEnvironment.Start(89, "ironclad", 56, 65, [], BaseSpireIds.DampCultist.ToString());
        var modifiedPlayer = modified.State.Player.Creature;
        modified.State.ApplyPower(modified, modifiedPlayer, BaseSpireIds.SetupStrikePower, 4, modifiedPlayer);
        EffectExecutor.Execute(modified, modifiedPlayer, new DecrementPowerEffect(BaseSpireIds.SetupStrikePower));
        Assert.Equal(2, modifiedPlayer.PowerAmount(BaseSpireIds.SetupStrikePower));
        Assert.Equal(2, modifiedPlayer.Strength);
        Assert.True(modified.State.Events.Any(gameEvent => gameEvent.Type == "power" && gameEvent.Amount == -2));
        modified.State.ExpirePower(modified, modifiedPlayer, BaseSpireIds.SetupStrikePower);
        Assert.Equal(0, modifiedPlayer.Strength);

        static ContentRegistry CompileRegistry(string source)
        {
            var registry = new ContentRegistry();
            registry.AddPackage(new PowerLifecycleFixturePackage(source));
            return registry;
        }
        var qualified = CompileRegistry("""
            package lifecycle {
              power Strength { role = strength }
              power Temporary {
                onStackChange = grantPower("lifecycle:strength", stackDelta)
                onExpire = grantPower(Strength, negativeSourceStacks)
                removeAfterOwnerTurnEnd = true
              }
            }
            """).Build();
        Assert.Equal(new PowerStackAdjustment(ContentId.Parse("lifecycle:strength"), 1),
            qualified.Powers[ContentId.Parse("lifecycle:temporary")].OnStackChange);
        Assert.Throws<InvalidOperationException>(() => CompileRegistry("""
            package lifecycle {
              power A { onStackChange = grantPower(B, stackDelta) }
              power B { onStackChange = grantPower(A, negativeStackDelta) }
            }
            """).Build());
        Assert.Throws<InvalidOperationException>(() => CompileRegistry(
            "package lifecycle { power A { onExpire = grantPower(Missing, sourceStacks) } }").Build());
        Assert.Throws<DslSourceException>(() => CompileRegistry(
            "package lifecycle { power A { onStackChange = grantPower(A, sourceStacks) } }"));
    }

    public void PowerExpiryWaitsForItsTurnEndChoiceAndUsesRemainingStacks()
    {
        var environment = new CombatEnvironment(Build(new BaseSpirePackage(), new PowerExpiryChoicePackage()));
        var session = environment.Start(88, "ironclad", 56, 65, [], BaseSpireIds.DampCultist.ToString());
        var player = session.State.Player.Creature;
        session.State.ApplyPower(session, player, PowerExpiryChoicePackage.PowerId, 3, player);
        var paused = environment.Step(session, new EndTurnAction());
        Assert.True(paused.NeedsChoice);
        Assert.Equal(1, session.State.Turn);
        Assert.Equal(3, player.Strength);
        Assert.Equal(3, player.PowerAmount(PowerExpiryChoicePackage.PowerId));
        environment.Step(session, new ChooseOptionAction(0));
        Assert.Equal(2, session.State.Turn);
        Assert.Equal(0, player.Strength);
        Assert.False(player.Powers.ContainsKey(PowerExpiryChoicePackage.PowerId));
        Assert.Equal(1, session.State.Events.Count(gameEvent => gameEvent.Type == "power_removed"
            && gameEvent.Source == player.Id && gameEvent.Amount == 2));
    }

    public void SourceBackedIroncladCardsUseSharedEffectsInCombat()
    {
        var definition = Build(new BaseSpirePackage());
        var environment = new CombatEnvironment(definition);
        var breakthrough = environment.StartEncounter(51, "ironclad", 56, 65,
            [BaseSpireIds.Breakthrough],
            [new CombatEnemySpawn(BaseSpireIds.DampCultist.ToString()),
                new CombatEnemySpawn(BaseSpireIds.CalcifiedCultist.ToString())]);
        var enemyHp = breakthrough.State.Enemies.Select(enemy => enemy.Creature.Hp).ToArray();
        environment.Step(breakthrough, new PlayCardAction(0));
        Assert.Equal(55, breakthrough.State.Player.Creature.Hp);
        for (var i = 0; i < enemyHp.Length; i++)
            Assert.Equal(enemyHp[i] - 9, breakthrough.State.Enemies[i].Creature.Hp);
        var upgradedBreakthrough = definition.Cards[BaseSpireIds.Breakthrough]
            .ResolveEffect(new CardInstance("breakthrough+", BaseSpireIds.Breakthrough, 1));
        Assert.True(upgradedBreakthrough is SequenceEffect { Effects.Count: 2 });
        Assert.Equal(new AttackEffect(13, TargetSelector.AllOpponents),
            ((SequenceEffect)upgradedBreakthrough).Effects[1]);

        var hemokinesis = environment.Start(52, "ironclad", 56, 65, [BaseSpireIds.Hemokinesis],
            BaseSpireIds.DampCultist.ToString(), enemyHp: 53);
        environment.Step(hemokinesis, new PlayCardAction(0, hemokinesis.State.Enemy.Id));
        Assert.Equal(54, hemokinesis.State.Player.Creature.Hp);
        Assert.Equal(38, hemokinesis.State.Enemy.Hp);
        var upgradedHemokinesis = definition.Cards[BaseSpireIds.Hemokinesis]
            .ResolveEffect(new CardInstance("hemokinesis+", BaseSpireIds.Hemokinesis, 1));
        Assert.Equal(new AttackEffect(20, TargetSelector.SelectedTarget),
            ((SequenceEffect)upgradedHemokinesis).Effects[1]);

        var breakCombat = environment.Start(53, "ironclad", 56, 65, [BaseSpireIds.Break],
            BaseSpireIds.DampCultist.ToString(), enemyHp: 53);
        environment.Step(breakCombat, new PlayCardAction(0, breakCombat.State.Enemy.Id));
        Assert.Equal(33, breakCombat.State.Enemy.Hp);
        Assert.Equal(5, breakCombat.State.Enemy.PowerAmount(BaseSpireIds.Vulnerable));
        var upgradedBreak = (SequenceEffect)definition.Cards[BaseSpireIds.Break]
            .ResolveEffect(new CardInstance("break+", BaseSpireIds.Break, 1));
        Assert.Equal(new AttackEffect(30, TargetSelector.SelectedTarget), upgradedBreak.Effects[0]);
        Assert.Equal(new ApplyPowerEffect(BaseSpireIds.Vulnerable, 7, TargetSelector.SelectedTarget),
            upgradedBreak.Effects[1]);

        var tremble = environment.Start(54, "ironclad", 56, 65, [BaseSpireIds.Tremble],
            BaseSpireIds.DampCultist.ToString(), enemyHp: 53);
        environment.Step(tremble, new PlayCardAction(0, tremble.State.Enemy.Id));
        Assert.Equal(3, tremble.State.Enemy.PowerAmount(BaseSpireIds.Vulnerable));
        Assert.Equal(BaseSpireIds.Tremble, tremble.State.Player.ExhaustPile.Single());
        Assert.Equal(new ApplyPowerEffect(BaseSpireIds.Vulnerable, 4, TargetSelector.SelectedTarget),
            definition.Cards[BaseSpireIds.Tremble]
                .ResolveEffect(new CardInstance("tremble+", BaseSpireIds.Tremble, 1)));
    }

    public void SourceBackedIroncladCardsComposeLossChoiceAndPower()
    {
        var definition = Build(new BaseSpirePackage());
        var env = new CombatEnvironment(definition);

        var bloodWall = env.Start(55, "ironclad", 56, 65, [BaseSpireIds.BloodWall],
            BaseSpireIds.DampCultist.ToString(), enemyHp: 53);
        env.Step(bloodWall, new PlayCardAction(0, bloodWall.State.Player.Id));
        Assert.Equal(54, bloodWall.State.Player.Creature.Hp);
        Assert.Equal(16, bloodWall.State.Player.Creature.Block);
        Assert.Equal(new GainBlockEffect(20), ((SequenceEffect)definition.Cards[BaseSpireIds.BloodWall]
            .ResolveEffect(new CardInstance("blood-wall+", BaseSpireIds.BloodWall, 1))).Effects[1]);

        var brand = env.Start(56, "ironclad", 56, 65,
            [BaseSpireIds.Brand, BaseSpireIds.StrikeIronclad], BaseSpireIds.DampCultist.ToString(), enemyHp: 53);
        var strike = brand.State.Player.HandInstances[1];
        env.Step(brand, new PlayCardAction(0, brand.State.Player.Id));
        Assert.Equal(55, brand.State.Player.Creature.Hp);
        Assert.True(brand.State.PendingCardSelection is PendingCardChoice);
        Assert.Equal(0, brand.State.Player.Creature.Strength);
        env.Step(brand, new ChooseCardAction(strike.InstanceId));
        Assert.Equal(strike.InstanceId, brand.State.Player.ExhaustPileInstances.Single().InstanceId);
        Assert.Equal(1, brand.State.Player.Creature.Strength);
        Assert.Equal(new ApplyPowerEffect(BaseSpireIds.Strength, 2, TargetSelector.Self),
            ((SequenceEffect)definition.Cards[BaseSpireIds.Brand]
                .ResolveEffect(new CardInstance("brand+", BaseSpireIds.Brand, 1))).Effects[2]);

        var cinder = env.Start(57, "ironclad", 56, 65,
            [BaseSpireIds.Cinder, BaseSpireIds.DefendIronclad], BaseSpireIds.DampCultist.ToString(), enemyHp: 53);
        env.Step(cinder, new PlayCardAction(0, cinder.State.Enemy.Id));
        Assert.Equal(35, cinder.State.Enemy.Hp);
        Assert.Equal(BaseSpireIds.DefendIronclad, cinder.State.Player.ExhaustPile.Single());
        Assert.Equal(new AttackEffect(24, TargetSelector.SelectedTarget),
            ((SequenceEffect)definition.Cards[BaseSpireIds.Cinder]
                .ResolveEffect(new CardInstance("cinder+", BaseSpireIds.Cinder, 1))).Effects[0]);

        var taunt = env.Start(58, "ironclad", 56, 65, [BaseSpireIds.Taunt],
            BaseSpireIds.DampCultist.ToString(), enemyHp: 53);
        env.Step(taunt, new PlayCardAction(0, taunt.State.Enemy.Id));
        Assert.Equal(7, taunt.State.Player.Creature.Block);
        Assert.Equal(1, taunt.State.Enemy.PowerAmount(BaseSpireIds.Vulnerable));
        var upgradedTaunt = (SequenceEffect)definition.Cards[BaseSpireIds.Taunt]
            .ResolveEffect(new CardInstance("taunt+", BaseSpireIds.Taunt, 1));
        Assert.Equal(new GainBlockEffect(8), upgradedTaunt.Effects[0]);
        Assert.Equal(new ApplyPowerEffect(BaseSpireIds.Vulnerable, 2, TargetSelector.SelectedTarget),
            upgradedTaunt.Effects[1]);
    }

    public void SourceBackedConditionalCardsUseTurnHistoryAndPileCounts()
    {
        var definition = Build(new BaseSpirePackage());
        var env = new CombatEnvironment(definition);
        var evilEye = env.Start(59, "ironclad", 56, 65,
            [BaseSpireIds.EvilEye, BaseSpireIds.Tremble, BaseSpireIds.EvilEye],
            BaseSpireIds.DampCultist.ToString(), enemyHp: 53,
            drawPile: [BaseSpireIds.EvilEye]);
        env.Step(evilEye, new PlayCardAction(0, evilEye.State.Player.Id));
        Assert.Equal(8, evilEye.State.Player.Creature.Block);
        env.Step(evilEye, new PlayCardAction(0, evilEye.State.Enemy.Id));
        Assert.Equal(1, evilEye.State.Player.ExhaustPile.Count);
        env.Step(evilEye, new PlayCardAction(0, evilEye.State.Player.Id));
        Assert.Equal(24, evilEye.State.Player.Creature.Block);
        env.Step(evilEye, new EndTurnAction());
        Assert.Equal(2, evilEye.State.Turn);
        var nextEvilEye = evilEye.State.Player.HandInstances
            .Select((card, index) => (card, index))
            .First(item => item.card.CardId == BaseSpireIds.EvilEye).index;
        env.Step(evilEye, new PlayCardAction(nextEvilEye, evilEye.State.Player.Id));
        Assert.Equal(8, evilEye.State.Player.Creature.Block);

        var ritualWithoutExhaust = env.Start(60, "ironclad", 56, 65,
            [BaseSpireIds.ForgottenRitual], BaseSpireIds.DampCultist.ToString(), enemyHp: 53);
        env.Step(ritualWithoutExhaust, new PlayCardAction(0, ritualWithoutExhaust.State.Player.Id));
        Assert.Equal(2, ritualWithoutExhaust.State.Player.Energy);
        Assert.Equal(BaseSpireIds.ForgottenRitual, ritualWithoutExhaust.State.Player.ExhaustPile.Single());
        var ritualWithExhaust = env.Start(61, "ironclad", 56, 65,
            [BaseSpireIds.Tremble, BaseSpireIds.ForgottenRitual],
            BaseSpireIds.DampCultist.ToString(), enemyHp: 53);
        env.Step(ritualWithExhaust, new PlayCardAction(0, ritualWithExhaust.State.Enemy.Id));
        env.Step(ritualWithExhaust, new PlayCardAction(0, ritualWithExhaust.State.Player.Id));
        Assert.Equal(4, ritualWithExhaust.State.Player.Energy);
        Assert.True(definition.Cards[BaseSpireIds.ForgottenRitual]
            .ResolveEffect(new CardInstance("ritual+", BaseSpireIds.ForgottenRitual, 1))
            is IfEffect { Then: ModifyEnergyEffect { Amount: 4 } });

        var pactsEnd = env.Start(62, "ironclad", 56, 65,
            [BaseSpireIds.PactsEnd, BaseSpireIds.PactsEnd],
            BaseSpireIds.DampCultist.ToString(), enemyHp: 53);
        pactsEnd.State.Player.MutableExhaustPile.Add(new CardInstance("exhaust-1", BaseSpireIds.Dazed));
        pactsEnd.State.Player.MutableExhaustPile.Add(new CardInstance("exhaust-2", BaseSpireIds.Dazed));
        env.Step(pactsEnd, new PlayCardAction(0));
        Assert.Equal(53, pactsEnd.State.Enemy.Hp);
        pactsEnd.State.Player.MutableExhaustPile.Add(new CardInstance("exhaust-3", BaseSpireIds.Dazed));
        env.Step(pactsEnd, new PlayCardAction(0));
        Assert.Equal(36, pactsEnd.State.Enemy.Hp);
        Assert.True(definition.Cards[BaseSpireIds.PactsEnd]
            .ResolveEffect(new CardInstance("pacts-end+", BaseSpireIds.PactsEnd, 1))
            is IfEffect { Then: AttackEffect { Amount: 23, Target: TargetSelector.AllOpponents } });
        var conditional = new IfEffect(new PileCountAtLeastPredicate(CardPile.Exhaust, 3),
            new AttackEffect(17, TargetSelector.AllOpponents));
        Assert.Equal(conditional, EffectDsl.Parse(EffectDsl.Serialize(conditional)));
    }

    public void ScaledAttacksAndTargetPredicatesUseLiveCombatState()
    {
        var definition = Build(new BaseSpirePackage());
        var env = new CombatEnvironment(definition);

        var ashen = env.Start(63, "ironclad", 56, 65, [BaseSpireIds.AshenStrike],
            BaseSpireIds.DampCultist.ToString(), enemyHp: 53);
        ashen.State.Player.MutableExhaustPile.Add(new CardInstance("ashen-exhaust-1", BaseSpireIds.Dazed));
        ashen.State.Player.MutableExhaustPile.Add(new CardInstance("ashen-exhaust-2", BaseSpireIds.Dazed));
        env.Step(ashen, new PlayCardAction(0, ashen.State.Enemy.Id));
        Assert.Equal(41, ashen.State.Enemy.Hp);

        var upgradedAshen = env.Start(64, "ironclad", 56, 65, [BaseSpireIds.AshenStrike],
            BaseSpireIds.DampCultist.ToString(), enemyHp: 53);
        upgradedAshen.State.Player.MutableHand[0] = new CardInstance("ashen+", BaseSpireIds.AshenStrike, 1);
        upgradedAshen.State.Player.MutableExhaustPile.Add(new CardInstance("ashen+-exhaust", BaseSpireIds.Dazed));
        env.Step(upgradedAshen, new PlayCardAction(0, upgradedAshen.State.Enemy.Id));
        Assert.Equal(43, upgradedAshen.State.Enemy.Hp);

        var bully = env.Start(65, "ironclad", 56, 65, [BaseSpireIds.Bully],
            BaseSpireIds.DampCultist.ToString(), enemyHp: 53);
        bully.State.Enemy.MutablePowers[BaseSpireIds.Vulnerable] = 2;
        env.Step(bully, new PlayCardAction(0, bully.State.Enemy.Id));
        Assert.Equal(41, bully.State.Enemy.Hp);

        var dismantle = env.Start(66, "ironclad", 56, 65, [BaseSpireIds.Dismantle],
            BaseSpireIds.DampCultist.ToString(), enemyHp: 53);
        dismantle.State.Enemy.MutablePowers[BaseSpireIds.Vulnerable] = 2;
        env.Step(dismantle, new PlayCardAction(0, dismantle.State.Enemy.Id));
        Assert.Equal(29, dismantle.State.Enemy.Hp);
        Assert.Equal(2, dismantle.State.Events.Count(gameEvent => gameEvent.Type == "damage"));

        var noVulnerable = env.Start(67, "ironclad", 56, 65, [BaseSpireIds.Dismantle],
            BaseSpireIds.DampCultist.ToString(), enemyHp: 53);
        env.Step(noVulnerable, new PlayCardAction(0, noVulnerable.State.Enemy.Id));
        Assert.Equal(45, noVulnerable.State.Enemy.Hp);
        Assert.Equal(1, noVulnerable.State.Events.Count(gameEvent => gameEvent.Type == "damage"));

        var scaled = definition.Cards[BaseSpireIds.Bully].Effect;
        Assert.Equal(scaled, EffectDsl.Parse(EffectDsl.Serialize(scaled)));
        Assert.Equal(new HasPowerPredicate(BaseSpireIds.Vulnerable, TargetSelector.SelectedTarget),
            ((IfEffect)definition.Cards[BaseSpireIds.Dismantle].Effect).Predicate);
    }

    public void NotYetHealsAndKeepsItsCombatGenerationRestriction()
    {
        var definition = Build(new BaseSpirePackage());
        var card = definition.Cards[BaseSpireIds.NotYet];
        Assert.False(card.CanBeGeneratedInCombat);
        Assert.Equal(new HealEffect(13, TargetSelector.Self),
            card.ResolveEffect(new CardInstance("not-yet+", BaseSpireIds.NotYet, 1)));

        var env = new CombatEnvironment(definition);
        var state = env.Start(68, "ironclad", 40, 65, [BaseSpireIds.NotYet],
            BaseSpireIds.DampCultist.ToString(), enemyHp: 53);
        env.Step(state, new PlayCardAction(0, state.State.Player.Id));
        Assert.Equal(50, state.State.Player.Creature.Hp);
        Assert.Equal(BaseSpireIds.NotYet, state.State.Player.ExhaustPile.Single());
    }

    public void PerfectedStrikeCountsTaggedCardsAcrossAllFiveCombatPiles()
    {
        var definition = Build(new BaseSpirePackage());
        var env = new CombatEnvironment(definition);
        var state = env.Start(69, "ironclad", 56, 65,
            [BaseSpireIds.PerfectedStrike, BaseSpireIds.StrikeIronclad],
            BaseSpireIds.DampCultist.ToString(), enemyHp: 53,
            drawPile: [BaseSpireIds.PommelStrike]);
        state.State.Player.MutableDiscardPile.Add(new CardInstance("tagged-discard", BaseSpireIds.TwinStrike));
        state.State.Player.MutableExhaustPile.Add(new CardInstance("tagged-exhaust", BaseSpireIds.AshenStrike));
        env.Step(state, new PlayCardAction(0, state.State.Enemy.Id));
        Assert.Equal(37, state.State.Enemy.Hp);
        Assert.Equal(BaseSpireIds.StrikeTag, definition.Cards[BaseSpireIds.PerfectedStrike].Tags.Single());

        var upgraded = env.Start(70, "ironclad", 56, 65, [BaseSpireIds.PerfectedStrike],
            BaseSpireIds.DampCultist.ToString(), enemyHp: 53);
        upgraded.State.Player.MutableHand[0] = new CardInstance("perfected+", BaseSpireIds.PerfectedStrike, 1);
        env.Step(upgraded, new PlayCardAction(0, upgraded.State.Enemy.Id));
        Assert.Equal(44, upgraded.State.Enemy.Hp);
        Assert.Equal(definition.Cards[BaseSpireIds.PerfectedStrike].Effect,
            EffectDsl.Parse(EffectDsl.Serialize(definition.Cards[BaseSpireIds.PerfectedStrike].Effect)));

        var crossPackageTag = DslSourceParser.Parse(
            "package fixture { card Tagged { type = attack rarity = common cost = 1 target = enemy " +
            "tags = [\"base-spire:strike\"] play = scaledAttack(1, 1, " +
            "count=cardsWithTag(\"base-spire:strike\")) } }", "tagged.spire");
        var tagged = SpireCombatDslCompiler.CompileCards(crossPackageTag).Single();
        Assert.Equal(BaseSpireIds.StrikeTag, tagged.Tags.Single());
        Assert.Equal(new TaggedCardCountSource(BaseSpireIds.StrikeTag),
            ((ScaledAttackEffect)tagged.Effect).Count);
    }

    public void FightMeGrantsStrengthAfterItsTwoHits()
    {
        var env = new CombatEnvironment(Build(new BaseSpirePackage()));
        var state = env.Start(71, "ironclad", 56, 65, [BaseSpireIds.FightMe],
            BaseSpireIds.DampCultist.ToString(), enemyHp: 53);
        env.Step(state, new PlayCardAction(0, state.State.Enemy.Id));
        Assert.Equal(43, state.State.Enemy.Hp);
        Assert.Equal(2, state.State.Events.Count(gameEvent => gameEvent.Type == "damage"));
        Assert.Equal(3, state.State.Player.Creature.Strength);
        Assert.Equal(1, state.State.Enemy.Strength);

        var upgraded = env.Start(72, "ironclad", 56, 65, [BaseSpireIds.FightMe],
            BaseSpireIds.DampCultist.ToString(), enemyHp: 53);
        upgraded.State.Player.MutableHand[0] = new CardInstance("fight-me+", BaseSpireIds.FightMe, 1);
        env.Step(upgraded, new PlayCardAction(0, upgraded.State.Enemy.Id));
        Assert.Equal(41, upgraded.State.Enemy.Hp);
        Assert.Equal(4, upgraded.State.Player.Creature.Strength);
        Assert.Equal(1, upgraded.State.Enemy.Strength);
    }

    public void CountScaledPowerEffectsUseTheSelectedTargetsCurrentStacks()
    {
        var definition = Build(new BaseSpirePackage());
        var env = new CombatEnvironment(definition);
        var dominate = env.Start(73, "ironclad", 56, 65, [BaseSpireIds.Dominate],
            BaseSpireIds.DampCultist.ToString(), enemyHp: 53);
        dominate.State.Enemy.MutablePowers[BaseSpireIds.Vulnerable] = 2;
        env.Step(dominate, new PlayCardAction(0, dominate.State.Enemy.Id));
        Assert.Equal(3, dominate.State.Enemy.PowerAmount(BaseSpireIds.Vulnerable));
        Assert.Equal(3, dominate.State.Player.Creature.Strength);
        Assert.Equal(BaseSpireIds.Dominate, dominate.State.Player.ExhaustPile.Single());

        var molten = env.Start(74, "ironclad", 56, 65, [BaseSpireIds.MoltenFist],
            BaseSpireIds.DampCultist.ToString(), enemyHp: 53);
        molten.State.Enemy.MutablePowers[BaseSpireIds.Vulnerable] = 2;
        env.Step(molten, new PlayCardAction(0, molten.State.Enemy.Id));
        Assert.Equal(38, molten.State.Enemy.Hp);
        Assert.Equal(4, molten.State.Enemy.PowerAmount(BaseSpireIds.Vulnerable));
        Assert.Equal(BaseSpireIds.MoltenFist, molten.State.Player.ExhaustPile.Single());

        var killed = env.Start(75, "ironclad", 56, 65, [BaseSpireIds.MoltenFist],
            BaseSpireIds.DampCultist.ToString(), enemyHp: 10);
        killed.State.Enemy.MutablePowers[BaseSpireIds.Vulnerable] = 2;
        env.Step(killed, new PlayCardAction(0, killed.State.Enemy.Id));
        Assert.Equal(0, killed.State.Enemy.Hp);
        Assert.False(killed.State.Events.Any(gameEvent => gameEvent.Type == "power"
            && gameEvent.Target == killed.State.Enemy.Id));

        var scaled = ((SequenceEffect)definition.Cards[BaseSpireIds.Dominate].Effect).Effects[1];
        Assert.Equal(scaled, EffectDsl.Parse(EffectDsl.Serialize(scaled)));
    }

    public void ConflagrationRepeatsAreaDamageAcrossLivingEnemies()
    {
        var env = new CombatEnvironment(Build(new BaseSpirePackage()));
        var enemies = new[]
        {
            new CombatEnemySpawn(BaseSpireIds.DampCultist.ToString(), Hp: 53),
            new CombatEnemySpawn(BaseSpireIds.CalcifiedCultist.ToString(), Hp: 40)
        };
        var state = env.StartEncounter(76, "ironclad", 56, 65, [BaseSpireIds.Conflagration], enemies);
        env.Step(state, new PlayCardAction(0));
        Assert.Equal(new[] { 45, 32 }, state.State.Enemies.Select(enemy => enemy.Creature.Hp));
        Assert.Equal(8, state.State.Events.Count(gameEvent => gameEvent.Type == "damage"));

        var upgraded = env.StartEncounter(77, "ironclad", 56, 65, [BaseSpireIds.Conflagration], enemies);
        upgraded.State.Player.MutableHand[0] = new CardInstance("conflagration+", BaseSpireIds.Conflagration, 1);
        env.Step(upgraded, new PlayCardAction(0));
        Assert.Equal(new[] { 43, 30 }, upgraded.State.Enemies.Select(enemy => enemy.Creature.Hp));
        Assert.Equal(10, upgraded.State.Events.Count(gameEvent => gameEvent.Type == "damage"));
    }

    public void RageTriggersOnlyAfterAttackCardsAndExpiresAtPlayerTurnEnd()
    {
        var definition = Build(new BaseSpirePackage());
        var ragePower = definition.Powers[BaseSpireIds.RagePower];
        Assert.Equal(PowerTrigger.AfterCardPlayed, ragePower.Trigger);
        Assert.Equal(CardType.Attack, ragePower.TriggerCardType);
        Assert.True(ragePower.RemoveAfterPlayerTurnEnd);
        Assert.Equal(new GainBlockEffect(1, BaseSpireIds.RagePower), ragePower.TriggerRuleEffect);
        Assert.Equal(new GainBlockEffect(1, BaseSpireIds.FeelNoPain),
            definition.Powers[BaseSpireIds.FeelNoPain].TriggerRuleEffect);

        var env = new CombatEnvironment(definition);
        var state = env.Start(78, "ironclad", 56, 65,
            [BaseSpireIds.Rage, BaseSpireIds.DefendIronclad, BaseSpireIds.StrikeIronclad],
            BaseSpireIds.DampCultist.ToString(), enemyHp: 53);
        env.Step(state, new PlayCardAction(0, state.State.Player.Id));
        Assert.Equal(3, state.State.Player.Creature.PowerAmount(BaseSpireIds.RagePower));
        Assert.Equal(0, state.State.Player.Creature.Block);
        env.Step(state, new PlayCardAction(0, state.State.Player.Id));
        Assert.Equal(5, state.State.Player.Creature.Block);
        env.Step(state, new PlayCardAction(0, state.State.Enemy.Id));
        Assert.Equal(8, state.State.Player.Creature.Block);
        env.Step(state, new EndTurnAction());
        Assert.Equal(0, state.State.Player.Creature.PowerAmount(BaseSpireIds.RagePower));

        var upgraded = env.Start(79, "ironclad", 56, 65,
            [BaseSpireIds.Rage, BaseSpireIds.StrikeIronclad],
            BaseSpireIds.DampCultist.ToString(), enemyHp: 53);
        upgraded.State.Player.MutableHand[0] = new CardInstance("rage+", BaseSpireIds.Rage, 1);
        env.Step(upgraded, new PlayCardAction(0, upgraded.State.Player.Id));
        env.Step(upgraded, new PlayCardAction(0, upgraded.State.Enemy.Id));
        Assert.Equal(5, upgraded.State.Player.Creature.Block);

        var exhaustingAttack = env.Start(81, "ironclad", 56, 65,
            [BaseSpireIds.Rage, BaseSpireIds.MoltenFist],
            BaseSpireIds.DampCultist.ToString(), enemyHp: 53);
        env.Step(exhaustingAttack, new PlayCardAction(0, exhaustingAttack.State.Player.Id));
        env.Step(exhaustingAttack, new PlayCardAction(0, exhaustingAttack.State.Enemy.Id));
        var events = exhaustingAttack.State.Events.ToArray();
        var rageBlockIndex = Array.FindIndex(events, gameEvent => gameEvent.Type == "block"
            && gameEvent.Amount == 3);
        var exhaustIndex = Array.FindIndex(events, gameEvent => gameEvent.Type == "card_exhausted");
        Assert.True(rageBlockIndex >= 0 && exhaustIndex > rageBlockIndex);
    }

    public void AfterCardPlayedPowerResumesOnceAfterCardChoice()
    {
        var env = new CombatEnvironment(Build(new BaseSpirePackage()));
        var state = env.Start(80, "ironclad", 56, 65,
            [BaseSpireIds.Rage, BaseSpireIds.Headbutt],
            BaseSpireIds.DampCultist.ToString(), enemyHp: 53);
        var discarded = new CardInstance("rage-headbutt-choice", BaseSpireIds.DefendIronclad);
        state.State.Player.MutableDiscardPile.Add(discarded);
        env.Step(state, new PlayCardAction(0, state.State.Player.Id));
        var paused = env.Step(state, new PlayCardAction(0, state.State.Enemy.Id));
        Assert.True(paused.NeedsChoice);
        Assert.Equal(0, state.State.Player.Creature.Block);
        env.Step(state, new ChooseCardAction(discarded.InstanceId));
        Assert.Equal(3, state.State.Player.Creature.Block);
        Assert.Equal(1, state.State.Events.Count(gameEvent => gameEvent.Type == "block"
            && gameEvent.Amount == 3));
        Assert.True(state.State.CurrentCardPlay is null);
    }

    public void SpireEnemyDslCompilesTheCompleteCurrentMonsterRoster()
    {
        using var stream = typeof(BaseSpirePackage).Assembly.GetManifestResourceStream(
            "BaseSpire.ContentSource.enemies.spire");
        Assert.True(stream is not null);
        using var reader = new StreamReader(stream!);
        var package = DslSourceParser.Parse(reader.ReadToEnd(), "base-spire/enemies.spire");
        var content = SpireCombatBehaviorDslCompiler.Compile(package);

        Assert.Equal(27, content.Powers.Count);
        Assert.Equal(59, content.Enemies.Count);
        Assert.Equal(59, content.Enemies.Select(enemy => enemy.Id).Distinct().Count());
        var actionIds = content.Enemies.SelectMany(enemy => enemy.Actions).Select(action => action.Id).ToArray();
        Assert.Equal(actionIds.Length, actionIds.Distinct().Count());

        var ritual = content.Powers.Single(power => power.Id == BaseSpireIds.Ritual);
        Assert.Equal(PowerTrigger.AfterEnemyTurnEnd, ritual.Trigger);
        Assert.Equal(BaseSpireIds.Strength, ritual.TriggerEffect);
        Assert.True(ritual.SkipFirstTriggerWhenAppliedToEnemy);

        var cultist = content.Enemies.Single(enemy => enemy.Id == BaseSpireIds.DampCultist);
        Assert.Equal(52, cultist.MinHp);
        Assert.Equal(54, cultist.MaxHp);
        Assert.Equal(ContentId.Parse("base-spire:damp-cultist-incantation"), cultist.Actions[0].Id);
        Assert.Equal(ContentId.Parse("base-spire:damp-cultist-dark-strike"), cultist.Actions[0].NextActionId);
        Assert.Equal(cultist.Actions[1].Id, cultist.Actions[1].NextActionId);
        Assert.Equal(new ApplyPowerEffect(BaseSpireIds.Ritual, 5), cultist.Actions[0].Effect);
        Assert.Equal(new AttackEffect(1, TargetSelector.FirstEnemy), cultist.Actions[1].Effect);

        var deadlyTough = SpireCombatBehaviorDslCompiler.Compile(package,
            new ContentVariantProfile(DeadlyEnemies: true, ToughEnemies: true));
        var toughCultist = deadlyTough.Enemies.Single(enemy => enemy.Id == BaseSpireIds.DampCultist);
        Assert.Equal(51, toughCultist.MinHp);
        Assert.Equal(53, toughCultist.MaxHp);
        Assert.Equal(new ApplyPowerEffect(BaseSpireIds.Ritual, 6), toughCultist.Actions[0].Effect);
        Assert.Equal(new AttackEffect(3, TargetSelector.FirstEnemy), toughCultist.Actions[1].Effect);

        var leafSmall = content.Enemies.Single(enemy => enemy.Id == BaseSpireIds.LeafSlimeSmall);
        Assert.Equal(EnemyActionRepeatPolicy.PreventImmediateRepeat,
            leafSmall.StartingTransition!.RepeatPolicy);
        Assert.True(leafSmall.Actions.All(action => action.Transition == leafSmall.StartingTransition));
        var twigMedium = content.Enemies.Single(enemy => enemy.Id == BaseSpireIds.TwigSlimeMedium);
        Assert.True(twigMedium.Actions.Any(action => action.Id == ContentId.Parse("base-spire:twig-medium-sticky-shot")));
        Assert.True(twigMedium.Actions.SelectMany(action => action.Transition!.Choices)
            .Any(choice => choice.MaxConsecutiveUses == 2));

        var invalid = DslSourceParser.Parse(
            "package broken { enemy Bad { hp = range(1, 2) moves = actions(badHit = attack(1, target=player)) behavior = loop(missing) } }",
            "broken.spire");
        Assert.Throws<DslSourceException>(() => SpireCombatBehaviorDslCompiler.Compile(invalid));
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

        var normalCompositions = new HashSet<string>(StringComparer.Ordinal);
        for (uint seed = 0; seed < 8; seed++)
        {
            var normal = environment.StartEncounter(seed, "ironclad", 80, 80, [], BaseSpireIds.SlimesNormal);
            var normalIds = environment.Observe(normal).Enemies.Select(enemy => enemy.Id.Split('#')[0]).ToArray();
            Assert.Equal(4, normalIds.Length);
            Assert.Equal(BaseSpireIds.TwigSlimeMedium.Value, normalIds[0]);
            Assert.Equal(BaseSpireIds.LeafSlimeMedium.Value, normalIds[1]);
            Assert.True(normalIds.Skip(2).ToHashSet(StringComparer.Ordinal).SetEquals([
                BaseSpireIds.LeafSlimeSmall.Value, BaseSpireIds.TwigSlimeSmall.Value]));
            Assert.Equal(1, normal.Random.Get("encounter-enemies").Counter);
            normalCompositions.Add(string.Join(',', normalIds.Skip(2)));
        }
        Assert.Equal(2, normalCompositions.Count);

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

        var clumpAction = ContentId.Parse("base-spire:twig-medium-clump-shot");
        var stickyAction = ContentId.Parse("base-spire:twig-medium-sticky-shot");
        var sawTwoConsecutiveClumps = false;
        for (uint seed = 405; seed < 421; seed++)
        {
            var twigMedium = environment.Start(seed, "ironclad", 1000, 1000, [], BaseSpireIds.TwigSlimeMedium.ToString());
            Assert.Equal(IntentKind.Debuff, environment.Observe(twigMedium).Intent);
            var history = new List<ContentId>();
            for (var turn = 0; turn < 24; turn++)
            {
                environment.Step(twigMedium, new EndTurnAction());
                history.Add(twigMedium.State.Enemies[0].Definition.Actions[twigMedium.State.Enemies[0].ActionIndex].Id);
            }
            Assert.False(history.Zip(history.Skip(1)).Any(pair =>
                pair.First == stickyAction && pair.Second == stickyAction));
            var clumpRuns = history.Zip(history.Skip(1)).Zip(history.Skip(2), (pair, third) =>
                pair.First == clumpAction && pair.Second == clumpAction && third == clumpAction);
            Assert.False(clumpRuns.Any(run => run));
            sawTwoConsecutiveClumps |= history.Zip(history.Skip(1)).Any(pair =>
                pair.First == clumpAction && pair.Second == clumpAction);
        }
        Assert.True(sawTwoConsecutiveClumps);
    }

    public void BaseFlyconidHonorsMoveCooldownsAcrossRecentActionHistory()
    {
        var environment = new CombatEnvironment(Build(new BaseSpirePackage()));
        var vulnerableSpores = ContentId.Parse("base-spire:flyconid-vulnerable-spores");
        var frailSpores = ContentId.Parse("base-spire:flyconid-frail-spores");
        var smash = ContentId.Parse("base-spire:flyconid-smash");
        var zeroWeightFallbackCount = 0;

        for (uint seed = 1000; seed < 1012; seed++)
        {
            var session = environment.Start(seed, "ironclad", 100_000, 100_000, [],
                "base-spire:flyconid");
            var enemy = session.State.Enemies.Single(enemy => enemy.Definition.Id == ContentId.Parse("base-spire:flyconid"));
            var history = new List<ContentId>();
            for (var turn = 0; turn < 40; turn++)
            {
                var selected = enemy.Definition.Actions[enemy.ActionIndex].Id;
                ContentId?[] eligible = history.Count == 0
                    ? [frailSpores, smash]
                    : new[]
                {
                    history.TakeLast(3).Contains(vulnerableSpores) ? (ContentId?)null : vulnerableSpores,
                    history.TakeLast(2).Contains(frailSpores) ? (ContentId?)null : frailSpores,
                    history.TakeLast(1).Contains(smash) ? (ContentId?)null : smash
                };
                if (selected != vulnerableSpores && selected != frailSpores && selected != smash)
                    throw new InvalidOperationException($"Unexpected Flyconid move '{selected}'.");
                var possible = eligible.Where(action => action is not null).Select(action => action!.Value).ToArray();
                if (possible.Length == 0)
                {
                    Assert.Equal(vulnerableSpores, selected);
                    zeroWeightFallbackCount++;
                }
                else
                    Assert.True(possible.Contains(selected));
                history.Add(selected);
                var randomCallsBefore = session.Random.Get("enemy-ai").Counter;
                environment.Step(session, new EndTurnAction());
                Assert.Equal(randomCallsBefore + 1, session.Random.Get("enemy-ai").Counter);
            }
        }
        Assert.True(zeroWeightFallbackCount > 0);
    }

    public void BaseSoloNibbitFollowsItsAttackBlockAndStrengthCycle()
    {
        var definition = Build(new BaseSpirePackage());
        var environment = new CombatEnvironment(definition);
        var session = environment.StartEncounter(212, "ironclad", 80, 80, [], BaseSpireIds.NibbitsWeak);
        Assert.Equal(0, session.Random.Get("enemy-ai").Counter);
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
        var deadlyAlone = new CombatEnvironment(Build(new BaseSpirePackage(deadlyEnemies: true)));
        var deadlyOpening = deadlyAlone.StartEncounter(216, "ironclad", 80, 80, [], BaseSpireIds.NibbitsWeak);
        Assert.Equal(13, deadlyAlone.Observe(deadlyOpening).IntentDamage);

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
        Assert.Equal(6, session.Random.Get("niche").Counter);

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
        Assert.False(definition.StateVariables.ContainsKey(new StateVariableKey(
            ContentId.Parse("partial-mod:orphan-state"), StateVariableScope.Run)));
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

        var runEnvironment = new RunEnvironment(definition, BaseSpireIds.StandardRun, BaseSpireIds.Ironclad);
        var run = runEnvironment.Start(9);
        Assert.Equal(TrainingModPackage.TrainingAncientId, runEnvironment.Observe(run).RoomStack[^1].EventId);
        var afterRunEffect = runEnvironment.Step(run,
            new ChooseRunEventOptionAction(TrainingModPackage.TrainingAncientOptionId));
        Assert.Equal(101, afterRunEffect.Gold);
        Assert.Equal(RunPhase.ChooseMapPoint, afterRunEffect.Phase);
    }

    public void ModVariablesKeepRunAndCombatLifetimesSeparate()
    {
        var definition = Build(new BaseSpirePackage(), new TrainingModPackage());
        var runEnvironment = new RunEnvironment(definition, BaseSpireIds.StandardRun, BaseSpireIds.Ironclad);
        var run = runEnvironment.Start(9);
        run.State.Deck.Clear();
        for (var i = 0; i < 5; i++)
            run.State.Deck.Add(new CardInstance($"practice-{i}", ContentId.Parse("training-mod:practice-card")));

        var afterEvent = runEnvironment.Step(run,
            new ChooseRunEventOptionAction(TrainingModPackage.TrainingAncientFightOptionId));
        Assert.Equal(RunPhase.ResolveRoom, afterEvent.Phase);
        Assert.Equal(1, run.State.GetModInt("training-mod", "ancient-visits"));
        Assert.Equal(0, run.State.GetModInt("other-mod", "ancient-visits"));

        var combatEnvironment = new CombatEnvironment(definition);
        var combat = combatEnvironment.StartCurrentEncounterForRun(run);
        Assert.Equal(1, run.State.GetModInt("training-mod", "combats-started"));
        Assert.Equal(-1, combat.State.GetModInt("base-spire", "demon-tongue-last-triggered-turn", 999));
        var ownedContext = new EffectExecutionContext(combat, combat.State.Player.Creature, null,
            ContentId.Parse("training-mod:grant-block"));
        Assert.Throws<InvalidOperationException>(() => ownedContext.GetInt("ancient-visits"));
        Assert.Throws<InvalidOperationException>(() => ownedContext.GetRunInt("observed-ancient-visits"));
        Assert.Throws<InvalidOperationException>(() => ownedContext.SetInt("undeclared", 1));
        var runHpBeforeCombatDamage = run.State.Hp;
        combat.State.Player.Creature.Hp = 42;
        Assert.Equal(42, runEnvironment.Observe(run).Hp);
        Assert.Equal(runHpBeforeCombatDamage, run.State.Hp);
        Assert.Equal(0, combat.State.GetModInt("training-mod", "ancient-visits", 0));
        Assert.Equal(0, run.State.GetModInt("training-mod", "practice-card-uses"));

        combatEnvironment.Step(combat, new PlayCardAction(0, combat.State.Player.Id));
        Assert.Equal(1, combat.State.GetModInt("training-mod", "practice-card-uses", 0));
        Assert.Equal(1, combat.State.GetModInt("training-mod", "observed-ancient-visits", 0));
        Assert.Equal(1, run.State.GetModInt("training-mod", "practice-card-uses"));
        Assert.Equal(1, run.State.GetModInt("training-mod", "play-count"));
        Assert.Equal(0, run.State.GetModInt("other-mod", "play-count"));

        var standalone = combatEnvironment.Start(9, "ironclad", 56, 65,
            [ContentId.Parse("training-mod:practice-card")], BaseSpireIds.DampCultist.ToString());
        var context = new EffectExecutionContext(standalone, standalone.State.Player.Creature, null,
            ContentId.Parse("training-mod:grant-block"));
        Assert.False(context.IsRunOwned);
        Assert.Throws<InvalidOperationException>(() => context.GetRunInt("ancient-visits"));
        combatEnvironment.Step(standalone, new PlayCardAction(0, standalone.State.Player.Id));
        Assert.Equal(1, standalone.State.GetModInt("training-mod", "practice-card-uses", 0));
        Assert.Equal(1, run.State.GetModInt("training-mod", "practice-card-uses"));

        var freshRun = runEnvironment.Start(9);
        Assert.Equal(0, freshRun.State.GetModInt("training-mod", "ancient-visits"));
    }

    public void StateDslDefinesScopedDefaultsAndRejectsInvalidDeclarations()
    {
        var source = DslSourceParser.Parse("""
            package example-mod {
              state Counter { scope = run type = int default = 7 }
              state Counter { scope = combat type = int default = -1 }
            }
            """, "state-test.spire");
        var definitions = SpireStateDslCompiler.Compile(source);
        Assert.Equal(2, definitions.Count);
        Assert.Equal(definitions[0].Id, definitions[1].Id);
        Assert.Equal(StateVariableScope.Run, definitions[0].Scope);
        Assert.Equal(StateVariableScope.Combat, definitions[1].Scope);
        Assert.Equal(7, definitions[0].DefaultInt);
        Assert.Equal(-1, definitions[1].DefaultInt);

        Assert.Throws<DslSourceException>(() => SpireStateDslCompiler.Compile(DslSourceParser.Parse(
            "package x { state Value { scope = room type = int } }", "bad-scope.spire")));
        Assert.Throws<DslSourceException>(() => SpireStateDslCompiler.Compile(DslSourceParser.Parse(
            "package x { state Value { scope = run type = string } }", "bad-type.spire")));
        Assert.Throws<DslSourceException>(() => SpireStateDslCompiler.Compile(DslSourceParser.Parse(
            "package x { state Value { scope = run type = int default = true } }", "bad-default.spire")));
        Assert.Throws<DslSourceException>(() => SpireStateDslCompiler.Compile(DslSourceParser.Parse(
            "package x { state Value { scope = run type = int } state Value { scope = run type = int } }",
            "duplicate.spire")));
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
        Assert.Equal(ContentId.Parse("base-spire:fire-potion"), afterCombatObservation.Potions.OfType<ContentId>().Single());
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
        Assert.Equal(ContentId.Parse("base-spire:fire-potion"), won.Potions.OfType<ContentId>().Single());
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
        Assert.Equal(2, basic.State.Player.DiscardPileInstances.Count);
        var basicCopy = basic.State.Player.DiscardPileInstances.Single(card => card.InstanceId != originalBasicId);
        Assert.Equal(BaseSpireIds.Anger, basicCopy.CardId);
        Assert.Equal(0, basicCopy.UpgradeLevel);
        Assert.True(originalBasicId != basicCopy.InstanceId);

        var upgraded = env.Start(34, "ironclad", 56, 65, [BaseSpireIds.Anger],
            BaseSpireIds.DampCultist.ToString(), enemyHp: 53);
        upgraded.State.Player.MutableHand[0] = new CardInstance("fixture-upgraded-anger",
            BaseSpireIds.Anger, upgradeLevel: 1);
        env.Step(upgraded, new PlayCardAction(0, upgraded.State.Enemy.Id));
        Assert.Equal(45, upgraded.State.Enemy.Hp);
        var upgradedCopy = upgraded.State.Player.DiscardPileInstances.Single(card => card.InstanceId != "fixture-upgraded-anger");
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
        Assert.Equal("Choose a card from discard", SingleCardChoice(paused.Observation)!.Prompt);
        Assert.Equal(new[] { strike.InstanceId, defend.InstanceId },
            SingleCardChoice(paused.Observation)!.Options.Select(card => card.InstanceId));
        var cardChoices = env.LegalActions(state).OfType<ChooseCardAction>().ToArray();
        Assert.Equal(2, cardChoices.Length);
        Assert.True(cardChoices.Any(choice => choice.CardInstanceId == defend.InstanceId));

        var resolved = env.Step(state, new ChooseCardAction(defend.InstanceId));
        Assert.True(resolved.Observation.PendingCardSelection is null);
        Assert.Equal(defend.InstanceId, state.State.Player.DrawPileInstances[0].InstanceId);
        Assert.Equal(2, state.State.Player.DiscardPileInstances.Count);
        Assert.True(state.State.Player.DiscardPileInstances.Contains(strike));
        Assert.Contains(state.State.Player.DiscardPileInstances, card => card.CardId == BaseSpireIds.Headbutt);
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
        Assert.True(noChoice.Observation.PendingCardSelection is null);
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
        Assert.Equal(2, SingleCardChoice(paused.Observation)!.Options.Count);
        Assert.True(SingleCardChoice(paused.Observation)!.Options.Any(card => card.InstanceId == chosenCard.InstanceId));
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
            SingleCardChoice(paused.Observation)!.Options.Select(card => card.InstanceId));
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
        Assert.Equal(3, SingleCardChoice(paused.Observation)!.Options.Count);
        var chosen = SingleCardChoice(paused.Observation)!.Options.Single(card => card.CardId == BaseSpireIds.Slimed);
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
        var chosen = SingleCardChoice(paused.Observation)!.Options.Single(card => card.CardId == BaseSpireIds.Anger);
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

    public void BeforeFlushHookCanPauseToRetainASelectedCardAndAfterFlushSeesTheResult()
    {
        var registry = new ContentRegistry();
        registry.AddPackage(new BaseSpirePackage());
        var hook = new RetainOneCardHook();
        registry.AddPackage(new TestHookPackage(hook));
        var env = new CombatEnvironment(registry.Build());
        var state = env.Start(68, "ironclad", 56, 65,
            [BaseSpireIds.StrikeIronclad, BaseSpireIds.DefendIronclad], BaseSpireIds.DampCultist.ToString(),
            drawPile: Enumerable.Repeat(BaseSpireIds.StrikeIronclad, 5));
        var retainedCard = state.State.Player.HandInstances[1];
        var flushedCard = state.State.Player.HandInstances[0];

        var paused = env.Step(state, new EndTurnAction());

        Assert.True(paused.NeedsChoice);
        Assert.Equal(2, SingleCardChoice(paused.Observation)!.Options.Count);
        env.Step(state, new ChooseCardAction(retainedCard.InstanceId));

        Assert.Contains(state.State.Player.HandInstances, card => card.InstanceId == retainedCard.InstanceId);
        Assert.Equal(new[] { flushedCard.InstanceId }, hook.FlushedCards.Select(card => card.InstanceId));
        Assert.Equal(new[] { retainedCard.InstanceId }, hook.RetainedCards.Select(card => card.InstanceId));
    }

    public void WellLaidPlansAllowsSelectingUpToItsPowerAmountOrConfirmingNone()
    {
        Assert.Equal(new ChooseCardsFromPileEffect(CardPile.Hand, "Choose cards", 0, 2,
                CardSelectionFilter.NotRetained, new RetainSelectedCardEffect()),
            EffectDsl.Parse("""{"op":"chooseCardsFromPile","from":"hand","prompt":"Choose cards","minChoices":0,"maxChoices":2,"filter":"notRetained","onSelected":{"op":"retainSelectedCard"}}"""));
        PendingCardSelectionView multiView = new PendingCardSetChoiceView("Choose cards", [], [], 0, 2);
        var multiJson = System.Text.Json.JsonSerializer.Serialize<PendingCardSelectionView>(multiView);
        Assert.True(multiJson.Contains("\"kind\":\"multiple\"", StringComparison.Ordinal));
        Assert.True(System.Text.Json.JsonSerializer.Deserialize<PendingCardSelectionView>(multiJson)
            is PendingCardSetChoiceView);
        var env = new CombatEnvironment(Build(new BaseSpirePackage()));
        var state = env.Start(69, "ironclad", 56, 65,
            [BaseSpireIds.WellLaidPlans, BaseSpireIds.StrikeIronclad, BaseSpireIds.DefendIronclad],
            BaseSpireIds.DampCultist.ToString());
        env.Step(state, new PlayCardAction(0, state.State.Player.Id));
        var hand = state.State.Player.HandInstances.ToArray();

        var paused = env.Step(state, new EndTurnAction());
        var choice = MultiCardChoice(paused.Observation)!;
        Assert.Equal(0, choice.MinChoices);
        Assert.Equal(1, choice.MaxChoices);
        Assert.Equal(2, choice.Options.Count);
        Assert.Contains(env.LegalActions(state), action => action is ConfirmCardSelectionAction);

        env.Step(state, new ChooseCardAction(hand[0].InstanceId));
        Assert.Equal(1, ((PendingCardSetChoice)state.State.PendingCardSelection!).Selected.Count);
        Assert.Contains(env.LegalActions(state), action => action is ConfirmCardSelectionAction);
        env.Step(state, new ConfirmCardSelectionAction());

        Assert.Contains(state.State.Player.HandInstances, card => card.InstanceId == hand[0].InstanceId);
        Assert.Contains(state.State.Events, gameEvent => gameEvent.Type == "card_retained"
            && gameEvent.Target == hand[0].InstanceId);
        Assert.Contains(state.State.Events, gameEvent => gameEvent.Type == "card_moved"
            && gameEvent.Target == hand[1].InstanceId);

        var skip = env.Start(70, "ironclad", 56, 65,
            [BaseSpireIds.WellLaidPlans, BaseSpireIds.StrikeIronclad, BaseSpireIds.DefendIronclad],
            BaseSpireIds.DampCultist.ToString());
        env.Step(skip, new PlayCardAction(0, skip.State.Player.Id));
        var skippedCards = skip.State.Player.HandInstances.ToArray();
        env.Step(skip, new EndTurnAction());
        env.Step(skip, new ConfirmCardSelectionAction());
        Assert.Equal(2, skip.State.Events.Count(gameEvent => gameEvent.Type == "card_moved"
            && skippedCards.Any(skipped => skipped.InstanceId == gameEvent.Target)));

        var upgraded = env.Start(71, "ironclad", 56, 65,
            [BaseSpireIds.WellLaidPlans, BaseSpireIds.StrikeIronclad, BaseSpireIds.DefendIronclad],
            BaseSpireIds.DampCultist.ToString());
        var plans = upgraded.State.Player.MutableHand[0];
        upgraded.State.Player.MutableHand[0] = new CardInstance(plans.InstanceId, plans.CardId, upgradeLevel: 1);
        env.Step(upgraded, new PlayCardAction(0, upgraded.State.Player.Id));
        var upgradeCards = upgraded.State.Player.HandInstances.ToArray();
        var upgradedChoice = MultiCardChoice(env.Step(upgraded, new EndTurnAction()).Observation)!;
        Assert.Equal(2, upgradedChoice.MaxChoices);
        env.Step(upgraded, new ChooseCardAction(upgradeCards[0].InstanceId));
        env.Step(upgraded, new ChooseCardAction(upgradeCards[1].InstanceId));
        Assert.Equal(1, env.LegalActions(upgraded).Count(action => action is ConfirmCardSelectionAction));
        Assert.False(env.LegalActions(upgraded).Any(action => action is ChooseCardAction));
        env.Step(upgraded, new ConfirmCardSelectionAction());
        Assert.Equal(2, upgraded.State.Events.Count(gameEvent => gameEvent.Type == "card_retained"
            && upgradeCards.Any(card => card.InstanceId == gameEvent.Target)));
    }

    public void BarricadePreventsPlayerBlockFromClearingAtTurnStart()
    {
        var env = new CombatEnvironment(Build(new BaseSpirePackage()));
        var state = env.Start(72, "ironclad", 56, 65, [BaseSpireIds.Barricade],
            BaseSpireIds.DampCultist.ToString());
        var barricade = state.State.Definition.Cards[BaseSpireIds.Barricade];
        Assert.Equal(3, barricade.ResolveCost(new CardInstance("barricade-basic", BaseSpireIds.Barricade)));
        Assert.Equal(2, barricade.ResolveCost(new CardInstance("barricade-upgraded", BaseSpireIds.Barricade, 1)));
        env.Step(state, new PlayCardAction(0, state.State.Player.Id));
        state.State.Player.Creature.Block = 12;

        env.Step(state, new EndTurnAction());
        Assert.Equal(12, state.State.Player.Creature.Block);
        Assert.Equal(1, state.State.Player.Creature.PowerAmount(BaseSpireIds.BarricadePower));

        var controlEnv = new CombatEnvironment(Build(new BaseSpirePackage()));
        var control = controlEnv.Start(73, "ironclad", 56, 65, [BaseSpireIds.StrikeIronclad],
            BaseSpireIds.DampCultist.ToString());
        control.State.Player.Creature.Block = 12;
        controlEnv.Step(control, new EndTurnAction());
        Assert.Equal(0, control.State.Player.Creature.Block);
    }

    public void EntrenchAddsBlockEqualToCurrentBlockAndIsAnEventCard()
    {
        var env = new CombatEnvironment(Build(new BaseSpirePackage()));
        var state = env.Start(74, "ironclad", 56, 65, [BaseSpireIds.Entrench],
            BaseSpireIds.DampCultist.ToString());
        var card = state.State.Definition.Cards[BaseSpireIds.Entrench];
        Assert.Equal(CardRarity.Event, card.Rarity);
        Assert.Equal(2, card.ResolveCost(new CardInstance("entrench-basic", BaseSpireIds.Entrench)));
        Assert.Equal(1, card.ResolveCost(new CardInstance("entrench-upgraded", BaseSpireIds.Entrench, 1)));
        Assert.False(state.State.Definition.CardPools[BaseSpireIds.IroncladCardPool].Cards
            .Contains(BaseSpireIds.Entrench));
        Assert.Equal(new GainBlockFromCurrentBlockEffect(), EffectDsl.Parse(
            """{"op":"gainBlockFromCurrentBlock"}"""));

        state.State.Player.Creature.Block = 9;
        var result = env.Step(state, new PlayCardAction(0, state.State.Player.Id));
        Assert.Equal(18, state.State.Player.Creature.Block);
        Assert.Contains(result.Events, gameEvent => gameEvent.Type == "block" && gameEvent.Amount == 9);
    }

    public void ImperviousGrantsBlockAndExhaustsItself()
    {
        var env = new CombatEnvironment(Build(new BaseSpirePackage()));
        var state = env.Start(75, "ironclad", 56, 65, [BaseSpireIds.Impervious],
            BaseSpireIds.DampCultist.ToString());
        var card = state.State.Definition.Cards[BaseSpireIds.Impervious];
        Assert.Equal(new GainBlockEffect(40), card.ResolveEffect(
            new CardInstance("impervious-upgraded", BaseSpireIds.Impervious, 1)));

        env.Step(state, new PlayCardAction(0, state.State.Player.Id));

        Assert.Equal(30, state.State.Player.Creature.Block);
        Assert.Equal(1, state.State.Player.ExhaustPileInstances.Count(instance =>
            instance.CardId == BaseSpireIds.Impervious));
        Assert.Equal(0, state.State.Player.PlayPileInstances.Count);

        var synergy = env.Start(76, "ironclad", 56, 65,
            [BaseSpireIds.FeelNoPain, BaseSpireIds.Impervious], BaseSpireIds.DampCultist.ToString());
        env.Step(synergy, new PlayCardAction(0, synergy.State.Player.Id));
        env.Step(synergy, new PlayCardAction(0, synergy.State.Player.Id));
        Assert.Equal(33, synergy.State.Player.Creature.Block);
        Assert.Contains(synergy.State.Events, gameEvent => gameEvent.Type == "card_exhausted");
    }

    public void WhirlwindSpendsXEnergyAndRepeatsAnAoEAttack()
    {
        Assert.Equal(new RepeatForXCostEffect(new AttackEffect(5, TargetSelector.AllOpponents)),
            EffectDsl.Parse("""{"op":"repeatForXCost","effect":{"op":"attack","amount":5,"target":"allOpponents"}}"""));
        var env = new CombatEnvironment(Build(new BaseSpirePackage()));
        var state = env.StartEncounter(77, "ironclad", 56, 65, [BaseSpireIds.Whirlwind],
            [new CombatEnemySpawn(BaseSpireIds.DampCultist.ToString()),
                new CombatEnemySpawn(BaseSpireIds.CalcifiedCultist.ToString())]);
        var initialHp = state.State.Enemies.Select(enemy => enemy.Creature.Hp).ToArray();
        Assert.Equal(CardCostKind.X, state.State.Definition.Cards[BaseSpireIds.Whirlwind].CostKind);

        env.Step(state, new PlayCardAction(0));

        Assert.Equal(0, state.State.Player.Energy);
        Assert.Equal(new[] { initialHp[0] - 15, initialHp[1] - 15 },
            state.State.Enemies.Select(enemy => enemy.Creature.Hp));
        Assert.Equal(6, state.State.Events.Count(gameEvent => gameEvent.Type == "damage" && gameEvent.Amount == 5));

        var upgraded = env.StartEncounter(78, "ironclad", 56, 65, [BaseSpireIds.Whirlwind],
            [new CombatEnemySpawn(BaseSpireIds.DampCultist.ToString()),
                new CombatEnemySpawn(BaseSpireIds.CalcifiedCultist.ToString())]);
        var whirlwind = upgraded.State.Player.MutableHand[0];
        upgraded.State.Player.MutableHand[0] = new CardInstance(whirlwind.InstanceId, whirlwind.CardId, 1);
        upgraded.State.Player.Energy = 3;
        var upgradedHp = upgraded.State.Enemies.Select(enemy => enemy.Creature.Hp).ToArray();
        env.Step(upgraded, new PlayCardAction(0));
        Assert.Equal(new[] { upgradedHp[0] - 24, upgradedHp[1] - 24 },
            upgraded.State.Enemies.Select(enemy => enemy.Creature.Hp));

        var zeroEnergy = env.Start(79, "ironclad", 56, 65, [BaseSpireIds.Whirlwind],
            BaseSpireIds.DampCultist.ToString());
        zeroEnergy.State.Player.Energy = 0;
        Assert.Contains(env.LegalActions(zeroEnergy), action => action is PlayCardAction { HandIndex: 0 });
        var enemyHp = zeroEnergy.State.Enemy.Hp;
        env.Step(zeroEnergy, new PlayCardAction(0));
        Assert.Equal(enemyHp, zeroEnergy.State.Enemy.Hp);
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
        var goldReward = completed.RoomStack[^1].Rewards.Single(reward => reward.Kind == RunRewardKind.Gold);
        Assert.Equal(RunRewardKind.Gold, goldReward.Kind);
        Assert.True(goldReward.Amount is >= 10 and <= 20);
        Assert.Equal(4, runSession.State.Random.Get("rewards").Counter);
        Assert.Contains(runEnvironment.LegalActions(runSession), action => action is ClaimGoldRewardAction);
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
        if (claimed.RoomStack[^1].Rewards.Count > 0)
            claimed = runEnvironment.Step(runSession, new SkipRewardSetAction());
        Assert.False(claimed.RoomStack[^1].Rewards.Any());
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
        Assert.Equal(goldReward.Amount, replayReward.RoomStack[^1].Rewards
            .Single(reward => reward.Kind == RunRewardKind.Gold).Amount);
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

    public void CombatCardRewardsGenerateDistinctOptionsAndWriteTheChosenInstanceToTheDeck()
    {
        var registry = new ContentRegistry();
        registry.AddPackage(new CardRewardFixturePackage());
        var definition = registry.Build();
        var oddsHook = new CardRewardOddsHook();
        var runEnvironment = new RunEnvironment(definition.Runs[CardRewardFixturePackage.RunId],
            definition.Characters[CardRewardFixturePackage.CharacterId], [oddsHook],
            definition.Cards.Values, definition.CardPools.Values, definition.Encounters.Values);
        var combatEnvironment = new CombatEnvironment(definition);
        var run = runEnvironment.Start(810);
        runEnvironment.PushSubRoom(run, RunRoomType.Monster);
        run.State.RoomStack[^1] = run.State.RoomStack[^1] with
        {
            EncounterId = CardRewardFixturePackage.EncounterId
        };
        var combat = combatEnvironment.StartForRun(run,
            CardRewardFixturePackage.EnemyId.ToString(), enemyHp: 1);
        combatEnvironment.Step(combat, new PlayCardAction(0, combat.State.Enemy.Id));
        var rewardObservation = runEnvironment.CompleteCombat(run, combat);
        var rewards = rewardObservation.RoomStack[^1].Rewards;
        var cardReward = rewards.Single(reward => reward.Kind == RunRewardKind.Card);
        Assert.Equal(3, cardReward.CardOptions.Count);
        Assert.Equal(3, cardReward.CardOptions.Select(card => card.CardId).Distinct().Count());
        Assert.True(cardReward.CardOptions.All(card => CardRewardFixturePackage.RareRewardCardIds.Contains(card.CardId)));
        Assert.Equal(10, run.State.RewardRandom.Counter);
        Assert.Equal(3, oddsHook.CallCount);
        Assert.True(oddsHook.RoomTypes.All(roomType => roomType == RunRoomType.Monster));

        var selected = cardReward.CardOptions[1];
        Assert.Contains(runEnvironment.LegalActions(run), action => action ==
            new ClaimCardRewardAction(cardReward.Id, selected.InstanceId));
        Assert.Throws<InvalidOperationException>(() => runEnvironment.Step(run,
            new ClaimCardRewardAction(cardReward.Id, "unknown-option")));
        var claimed = runEnvironment.Step(run,
            new ClaimCardRewardAction(cardReward.Id, selected.InstanceId));
        Assert.Contains(claimed.DeckInstances, card => card == selected);
        Assert.False(claimed.RoomStack[^1].Rewards.Any(reward => reward.Kind == RunRewardKind.Card));
        Assert.True(claimed.RoomStack[^1].Rewards.Any(reward => reward.Kind == RunRewardKind.Gold));

        var replayRunEnvironment = new RunEnvironment(definition.Runs[CardRewardFixturePackage.RunId],
            definition.Characters[CardRewardFixturePackage.CharacterId], [new CardRewardOddsHook()],
            definition.Cards.Values, definition.CardPools.Values, definition.Encounters.Values);
        var replayCombatEnvironment = new CombatEnvironment(definition);
        var replayRun = replayRunEnvironment.Start(810);
        replayRunEnvironment.PushSubRoom(replayRun, RunRoomType.Monster);
        replayRun.State.RoomStack[^1] = replayRun.State.RoomStack[^1] with
        {
            EncounterId = CardRewardFixturePackage.EncounterId
        };
        var replayCombat = replayCombatEnvironment.StartForRun(replayRun,
            CardRewardFixturePackage.EnemyId.ToString(), enemyHp: 1);
        replayCombatEnvironment.Step(replayCombat, new PlayCardAction(0, replayCombat.State.Enemy.Id));
        var replayCards = replayRunEnvironment.CompleteCombat(replayRun, replayCombat)
            .RoomStack[^1].Rewards.Single(reward => reward.Kind == RunRewardKind.Card).CardOptions;
        Assert.True(cardReward.CardOptions.SequenceEqual(replayCards));
    }

    public void RunEventOptionsExposeLegalChoicesAndApplyDeclarativeRunEffects()
    {
        var conditionDsl = new AllRunCondition([
            new RunMetricCondition(RunMetric.Gold, RunComparison.AtLeast, 100),
            new NotRunCondition(new RunMetricCondition(RunMetric.Hp, RunComparison.Equal, 0))
        ]);
        var serializedCondition = System.Text.Json.JsonSerializer.Serialize<RunCondition>(conditionDsl);
        Assert.Equal(serializedCondition, System.Text.Json.JsonSerializer.Serialize(
            System.Text.Json.JsonSerializer.Deserialize<RunCondition>(serializedCondition)));

        var cardEffect = EffectDsl.ParseRun(
            """{"op":"addCardToRunDeck","card":"run-event-fixture:card"}""");
        Assert.Equal(new AddCardToRunDeckEffect(RunEventFixturePackage.CardId), cardEffect);
        Assert.Equal(cardEffect, EffectDsl.ParseRun(EffectDsl.Serialize(cardEffect)));
        var gainMaxHp = new GainRunMaxHpEffect(5);
        Assert.Equal(gainMaxHp, EffectDsl.ParseRun(EffectDsl.Serialize(gainMaxHp)));
        var loseMaxHp = new LoseRunMaxHpEffect(5);
        Assert.Equal(loseMaxHp, EffectDsl.ParseRun(EffectDsl.Serialize(loseMaxHp)));
        var customRunEffect = EffectDsl.ParseRun(
            """{"op":"customRun","handler":"run-event-fixture:gain-gold","payload":{"amount":4}}""");
        var customRunEffectJson = EffectDsl.Serialize(customRunEffect);
        Assert.Equal(customRunEffectJson, EffectDsl.Serialize(EffectDsl.ParseRun(customRunEffectJson)));
        var chooseCard = new ChooseRunCardEffect("Choose a card to upgrade", RunCardFilter.Upgradeable,
            false, new UpgradeSelectedRunCardEffect());
        Assert.Equal(chooseCard, EffectDsl.ParseRun(EffectDsl.Serialize(chooseCard)));
        Assert.Equal(new WinRunEffect(), EffectDsl.ParseRun("""{"op":"winRun"}"""));
        var enterCombat = new EnterRunCombatEffect(RunEventFixturePackage.EventEncounterId, false);
        Assert.Equal(enterCombat, EffectDsl.ParseRun(EffectDsl.Serialize(enterCombat)));
        var gainRelic = new GainRunRelicEffect(RunEventFixturePackage.CommonRelicId);
        Assert.Equal(gainRelic, EffectDsl.ParseRun(EffectDsl.Serialize(gainRelic)));

        var registry = new ContentRegistry();
        registry.AddPackage(new RunEventFixturePackage());
        var definition = registry.Build();
        var environment = new RunEnvironment(definition, RunEventFixturePackage.RunId,
            RunEventFixturePackage.CharacterId);
        var run = environment.Start(421);
        run.State.Hp = 20;
        var observation = environment.Observe(run);
        Assert.Equal(RunPhase.ResolveEvent, observation.Phase);
        Assert.Equal(RunEventFixturePackage.EventId,
            observation.RoomStack[^1].EventId!.Value);
        Assert.Equal(13, observation.RoomStack[^1].EventOptions.Count);
        Assert.False(observation.RoomStack[^1].EventOptions.Single(option =>
            option.Id == RunEventFixturePackage.PredicateOptionId).IsEnabled);
        Assert.False(observation.RoomStack[^1].EventOptions.Single(option =>
            option.Id == RunEventFixturePackage.UnaffordableOptionId).IsEnabled);

        var legalActions = environment.LegalActions(run);
        Assert.Contains(legalActions, action => action ==
            new ChooseRunEventOptionAction(RunEventFixturePackage.TradeOptionId));
        Assert.False(legalActions.Any(action => action ==
            new ChooseRunEventOptionAction(RunEventFixturePackage.UnaffordableOptionId)));
        Assert.False(legalActions.Any(action => action ==
            new ChooseRunEventOptionAction(RunEventFixturePackage.PredicateOptionId)));
        Assert.Throws<InvalidOperationException>(() => environment.Step(run,
            new ChooseRunEventOptionAction(ContentId.Parse("run-event-fixture:unknown-option"))));

        var result = environment.Step(run,
            new ChooseRunEventOptionAction(RunEventFixturePackage.TradeOptionId));
        Assert.Equal(2, result.Gold);
        Assert.Equal(20, result.Hp);
        Assert.Equal(3, result.DeckInstances.Count);
        Assert.Equal(RunEventFixturePackage.CardId, result.DeckInstances[^1].CardId);
        Assert.Equal("run-event-card-0002", result.DeckInstances[^1].InstanceId);

        var relicRun = environment.Start(421);
        var relicResult = environment.Step(relicRun,
            new ChooseRunEventOptionAction(RunEventFixturePackage.RelicOptionId));
        Assert.True(relicResult.Relics.Contains(RunEventFixturePackage.CommonRelicId));

        var predicateRun = environment.Start(421);
        predicateRun.State.Hp = 20;
        predicateRun.State.Gold = 100;
        var predicateObservation = environment.Observe(predicateRun);
        Assert.True(predicateObservation.RoomStack[^1].EventOptions.Single(option =>
            option.Id == RunEventFixturePackage.PredicateOptionId).IsEnabled);
        Assert.Contains(environment.LegalActions(predicateRun), action => action ==
            new ChooseRunEventOptionAction(RunEventFixturePackage.PredicateOptionId));
        var transitioned = environment.Step(predicateRun,
            new ChooseRunEventOptionAction(RunEventFixturePackage.PredicateOptionId));
        Assert.Equal(RunPhase.ResolveEvent, transitioned.Phase);
        Assert.Equal(RunEventFixturePackage.ContinuationPageId, transitioned.RoomStack[^1].EventPageId);
        Assert.Equal("Continuation page", transitioned.RoomStack[^1].EventPageDescription);
        Assert.Equal(3, transitioned.RoomStack[^1].EventOptions.Count);
        Assert.Equal(101, transitioned.Gold);
        var completedEvent = environment.Step(predicateRun,
            new ChooseRunEventOptionAction(RunEventFixturePackage.ContinueOptionId));
        Assert.Equal(RunPhase.ChooseMapPoint, completedEvent.Phase);
        Assert.Equal(103, completedEvent.Gold);

        static RunObservation ResolveEventCombat(ContentRegistry registry, bool resumeEvent)
        {
            var runEnvironment = new RunEnvironment(registry.Build(), RunEventFixturePackage.RunId,
                RunEventFixturePackage.CharacterId);
            var runSession = runEnvironment.Start(421);
            runSession.State.Gold = 100;
            runEnvironment.Step(runSession,
                new ChooseRunEventOptionAction(RunEventFixturePackage.PredicateOptionId));
            var combatOption = resumeEvent
                ? RunEventFixturePackage.ResumeCombatOptionId
                : RunEventFixturePackage.EndingCombatOptionId;
            var enteringCombat = runEnvironment.Step(runSession,
                new ChooseRunEventOptionAction(combatOption));
            Assert.Equal(RunPhase.ResolveRoom, enteringCombat.Phase);
            Assert.Equal(2, enteringCombat.RoomStack.Count);

            var combatEnvironment = new CombatEnvironment(registry.Build());
            var combat = combatEnvironment.StartCurrentEncounterForRun(runSession);
            Assert.Equal(1, combat.State.Enemies.Count);
            combatEnvironment.Step(combat, new PlayCardAction(0, combat.State.Enemies[0].Creature.Id));
            var afterCombat = runEnvironment.CompleteCombat(runSession, combat);
            Assert.Equal(resumeEvent ? RunPhase.ResolveEvent : RunPhase.ChooseMapPoint, afterCombat.Phase);
            Assert.Equal(resumeEvent ? 1 : 0, afterCombat.RoomStack.Count);
            return afterCombat;
        }

        var resumedEvent = ResolveEventCombat(registry, resumeEvent: true);
        Assert.Equal(RunEventFixturePackage.ContinuationPageId, resumedEvent.RoomStack[^1].EventPageId);
        Assert.Contains(resumedEvent.RoomStack[^1].EventOptions,
            option => option.Id == RunEventFixturePackage.ContinueOptionId);
        ResolveEventCombat(registry, resumeEvent: false);

        var pageAfterSelectionRun = environment.Start(421);
        var selectionPending = environment.Step(pageAfterSelectionRun,
            new ChooseRunEventOptionAction(RunEventFixturePackage.SelectionTransitionOptionId));
        Assert.Equal(RunPhase.ResolveEventCardSelection, selectionPending.Phase);
        var selectedInstanceId = selectionPending.PendingRunCardSelection!.Candidates[0].InstanceId;
        var selectionTransitioned = environment.Step(pageAfterSelectionRun,
            new ChooseRunCardAction(selectedInstanceId));
        Assert.Equal(RunPhase.ResolveEvent, selectionTransitioned.Phase);
        Assert.Equal(RunEventFixturePackage.ContinuationPageId, selectionTransitioned.RoomStack[^1].EventPageId);
        Assert.Equal(1, selectionTransitioned.DeckInstances.Count);

        var upgradeRun = environment.Start(421);
        var upgradePending = environment.Step(upgradeRun,
            new ChooseRunEventOptionAction(RunEventFixturePackage.UpgradeOptionId));
        Assert.Equal(RunPhase.ResolveEventCardSelection, upgradePending.Phase);
        Assert.Equal("Choose a card to upgrade", upgradePending.PendingRunCardSelection!.Prompt);
        Assert.Equal(2, upgradePending.PendingRunCardSelection.Candidates.Count);
        var upgradeTarget = upgradePending.PendingRunCardSelection.Candidates[1];
        Assert.Contains(environment.LegalActions(upgradeRun), action => action ==
            new ChooseRunCardAction(upgradeTarget.InstanceId));
        var upgradedResult = environment.Step(upgradeRun, new ChooseRunCardAction(upgradeTarget.InstanceId));
        Assert.Equal(RunPhase.ChooseMapPoint, upgradedResult.Phase);
        Assert.Equal(12, upgradedResult.Gold);
        Assert.Equal(1, upgradedResult.DeckInstances.Single(card =>
            card.InstanceId == upgradeTarget.InstanceId).UpgradeLevel);

        var removeRun = environment.Start(421);
        var removePending = environment.Step(removeRun,
            new ChooseRunEventOptionAction(RunEventFixturePackage.RemoveOptionId));
        var removedInstanceId = removePending.PendingRunCardSelection!.Candidates[0].InstanceId;
        var removed = environment.Step(removeRun, new ChooseRunCardAction(removedInstanceId));
        Assert.Equal(1, removed.DeckInstances.Count);
        Assert.False(removed.DeckInstances.Any(card => card.InstanceId == removedInstanceId));

        var skipRun = environment.Start(421);
        var skipPending = environment.Step(skipRun,
            new ChooseRunEventOptionAction(RunEventFixturePackage.OptionalRemoveOptionId));
        Assert.True(skipPending.PendingRunCardSelection!.CanSkip);
        var skippedSelection = environment.Step(skipRun, new SkipRunCardSelectionAction());
        Assert.Equal(2, skippedSelection.DeckInstances.Count);
        Assert.Equal(11, skippedSelection.Gold);
        Assert.Equal(RunPhase.ChooseMapPoint, result.Phase);

        environment.Step(run, new ChooseMapPointAction("boss"));
        var finalEvent = environment.CompleteRoom(run,
            RunResult(RunOutcome.RoomCleared, environment.Observe(run)));
        Assert.Equal(RunPhase.ResolveFinalEvent, finalEvent.Phase);
        Assert.Equal(RunEventFixturePackage.FinalEventId, finalEvent.RoomStack[^1].EventId!.Value);
        Assert.Contains(environment.LegalActions(run), action => action ==
            new ChooseRunEventOptionAction(RunEventFixturePackage.FinalOptionId));
        Assert.Throws<InvalidOperationException>(() => environment.CompleteRoom(run,
            RunResult(RunOutcome.RunWon, environment.Observe(run))));
        var won = environment.Step(run,
            new ChooseRunEventOptionAction(RunEventFixturePackage.FinalOptionId));
        Assert.Equal(RunStatus.Won, won.Status);
        Assert.Equal(RunPhase.Terminal, won.Phase);

        var healRun = environment.Start(421);
        healRun.State.Hp = 20;
        var healed = environment.Step(healRun,
            new ChooseRunEventOptionAction(RunEventFixturePackage.GoldOptionId));
        Assert.Equal(15, healed.Gold);
        Assert.Equal(23, healed.Hp);
        Assert.Equal(RunPhase.ChooseMapPoint, healed.Phase);

        var fatalRun = environment.Start(422);
        fatalRun.State.Hp = 10;
        var defeated = environment.Step(fatalRun,
            new ChooseRunEventOptionAction(RunEventFixturePackage.FatalOptionId));
        Assert.Equal(0, defeated.Hp);
        Assert.Equal(RunStatus.Lost, defeated.Status);
        Assert.Equal(RunPhase.Terminal, defeated.Phase);

        var maxHpRun = environment.Start(423);
        maxHpRun.State.Hp = 20;
        var strengthened = environment.Step(maxHpRun,
            new ChooseRunEventOptionAction(RunEventFixturePackage.MaxHpOptionId));
        Assert.Equal(35, strengthened.MaxHp);
        Assert.Equal(25, strengthened.Hp);

        var maxHpLossRun = environment.Start(424);
        maxHpLossRun.State.Hp = 28;
        var weakened = environment.Step(maxHpLossRun,
            new ChooseRunEventOptionAction(RunEventFixturePackage.MaxHpLossOptionId));
        Assert.Equal(20, weakened.MaxHp);
        Assert.Equal(20, weakened.Hp);

        var customRunEffectRun = environment.Start(425);
        var customRunEffectResult = environment.Step(customRunEffectRun,
            new ChooseRunEventOptionAction(RunEventFixturePackage.CustomRunEffectOptionId));
        Assert.Equal(14, customRunEffectResult.Gold);

        var treasureRun = environment.Start(426);
        Assert.False(treasureRun.State.SharedRelicBags[RunRelicRarity.Common]
            .Contains(RunEventFixturePackage.CharacterPoolRelicId));
        Assert.True(treasureRun.State.PlayerRelicBags[RunRelicRarity.Common]
            .Contains(RunEventFixturePackage.CharacterPoolRelicId));
        environment.Step(treasureRun, new ChooseRunEventOptionAction(RunEventFixturePackage.GoldOptionId));
        var enteredTreasure = environment.Step(treasureRun, new ChooseMapPointAction("treasure"));
        Assert.Equal(RunPhase.ResolveTreasure, enteredTreasure.Phase);
        Assert.Contains(environment.LegalActions(treasureRun), action => action is OpenTreasureChestAction);
        var goldBeforeChest = enteredTreasure.Gold;
        var openedChest = environment.Step(treasureRun, new OpenTreasureChestAction());
        Assert.True(openedChest.Gold - goldBeforeChest is >= 42 and <= 52);
        Assert.Equal(RunPhase.ResolveRewards, openedChest.Phase);
        var relicReward = openedChest.RoomStack[^1].Rewards.Single(reward => reward.Kind == RunRewardKind.Relic);
        Assert.True(relicReward.RelicId.HasValue);
        var claimedRelic = environment.Step(treasureRun, new ClaimRelicRewardAction(relicReward.Id));
        Assert.True(claimedRelic.Relics.Contains(relicReward.RelicId!.Value));
        Assert.Equal(0, claimedRelic.RoomStack[^1].Rewards.Count);
        Assert.False(treasureRun.State.SharedRelicBags[definition.Relics[relicReward.RelicId.Value].Rarity]
            .Contains(relicReward.RelicId.Value));
        Assert.False(treasureRun.State.PlayerRelicBags[definition.Relics[relicReward.RelicId.Value].Rarity]
            .Contains(relicReward.RelicId.Value));
        environment.Step(treasureRun, new ProceedAfterRewardsAction());

        var skippedTreasureRun = environment.Start(427);
        environment.Step(skippedTreasureRun, new ChooseRunEventOptionAction(RunEventFixturePackage.GoldOptionId));
        environment.Step(skippedTreasureRun, new ChooseMapPointAction("treasure"));
        var skippedChest = environment.Step(skippedTreasureRun, new OpenTreasureChestAction());
        var skippedRelic = skippedChest.RoomStack[^1].Rewards.Single(reward => reward.Kind == RunRewardKind.Relic);
        environment.Step(skippedTreasureRun, new SkipRewardSetAction());
        var afterSkippingRelic = environment.Step(skippedTreasureRun, new ProceedAfterRewardsAction());
        Assert.False(afterSkippingRelic.Relics.Contains(skippedRelic.RelicId!.Value));
        var skippedRelicRarity = definition.Relics[skippedRelic.RelicId.Value].Rarity;
        Assert.False(skippedTreasureRun.State.SharedRelicBags[skippedRelicRarity]
            .Contains(skippedRelic.RelicId.Value));
        Assert.True(skippedTreasureRun.State.PlayerRelicBags[skippedRelicRarity]
            .Contains(skippedRelic.RelicId.Value));

        var eliteRun = environment.Start(428);
        environment.Step(eliteRun, new ChooseRunEventOptionAction(RunEventFixturePackage.GoldOptionId));
        environment.Step(eliteRun, new ChooseMapPointAction("boss"));
        environment.PushSubRoom(eliteRun, RunRoomType.Elite);
        eliteRun.State.BindEncounterToCurrentRoom(definition.Encounters[RunEventFixturePackage.EliteEncounterId]);
        var eliteCombatEnvironment = new CombatEnvironment(definition);
        var eliteCombat = eliteCombatEnvironment.StartCurrentEncounterForRun(eliteRun);
        eliteCombatEnvironment.Step(eliteCombat, new PlayCardAction(0, eliteCombat.State.Enemy.Id));
        var eliteRewards = environment.CompleteCombat(eliteRun, eliteCombat);
        var eliteRelic = eliteRewards.RoomStack[^1].Rewards.Single(reward => reward.Kind == RunRewardKind.Relic);
        Assert.True(eliteRelic.RelicId.HasValue);
        environment.Step(eliteRun, new ClaimRelicRewardAction(eliteRelic.Id));
        Assert.True(eliteRun.State.Relics.Contains(eliteRelic.RelicId!.Value));
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
        var card = Build(new BaseSpirePackage()).Cards[BaseSpireIds.StrikeIronclad] with { PoolIds = [] };
        var cardId = BaseSpireIds.StrikeIronclad;
        var firstEnemyId = ContentId.Parse("test-fixture:first-foe");
        var secondEnemyId = ContentId.Parse("test-fixture:second-foe");
        var package = new MultiEnemyPackage(
            card,
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
        Assert.Equal(4, afterTargetedAttack.Enemies[1].Hp);
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

    private static PendingCardChoiceView? SingleCardChoice(CombatObservation observation) =>
        observation.PendingCardSelection as PendingCardChoiceView;

    private static PendingCardSetChoiceView? MultiCardChoice(CombatObservation observation) =>
        observation.PendingCardSelection as PendingCardSetChoiceView;

    private static GameDefinition Build(params IRulePackage[] packages)
    {
        var registry = new ContentRegistry();
        foreach (var package in packages) registry.AddPackage(package);
        return registry.Build();
    }

    private sealed class DuplicateCardPackage : IRulePackage
    {
        public PackageInfo Info { get; } = new("duplicate", "1.0.0");
        public void Register(ContentRegistry registry) => registry.Register(new CardDefinition(
            ContentId.Parse("test-fixture:strike"), 0, CardType.Attack, new AttackEffect(99)));
    }

    private sealed class EnemyVariableFixturePackage : IRulePackage
    {
        public PackageInfo Info { get; } = new("enemy-variable-fixture", "1.0.0");

        public void Register(ContentRegistry registry)
        {
            var source = DslSourceParser.Parse("""
                package enemy-variable-fixture {
                  enemy Counter {
                    hp = range(10, 10)
                    variables = [variable(Calls, 0)]
                    moves = actions(
                      count = action(intent=Buff, effect=modifyEnemyVariable(Calls, 1)),
                      hit = attack(1, target=player),
                      finish = attack(2, target=player)
                    )
                    behavior = graph(start=count, edges=[
                      edge(count, chooseByCondition(
                        candidate(hit, when=variable(Calls, LessThan, 2)),
                        candidate(finish, when=variable(Calls, AtLeast, 2))
                      )),
                      edge(hit, count),
                      edge(finish, finish)
                    ])
                  }
                }
                """, "enemy-variable-fixture.spire");
            var content = SpireCombatBehaviorDslCompiler.Compile(source);
            foreach (var enemy in content.Enemies) registry.Register(enemy);
            registry.Register(new EncounterDefinition(ContentId.Parse("enemy-variable-fixture:encounter"),
                RunRoomType.Monster, [ContentId.Parse("enemy-variable-fixture:counter"),
                    ContentId.Parse("enemy-variable-fixture:counter")]));
        }
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

    private sealed class CardRewardFixturePackage : IRulePackage
    {
        public static readonly ContentId RunId = ContentId.Parse("card-reward-fixture:run");
        public static readonly ContentId CharacterId = ContentId.Parse("card-reward-fixture:character");
        public static readonly ContentId CardPoolId = ContentId.Parse("card-reward-fixture:pool");
        public static readonly ContentId StarterCardId = ContentId.Parse("card-reward-fixture:starter");
        public static readonly ContentId EnemyId = ContentId.Parse("card-reward-fixture:enemy");
        public static readonly ContentId EncounterId = ContentId.Parse("card-reward-fixture:encounter");
        public static readonly ContentId[] RewardCardIds =
        [
            ContentId.Parse("card-reward-fixture:reward-one"),
            ContentId.Parse("card-reward-fixture:reward-two"),
            ContentId.Parse("card-reward-fixture:reward-three")
        ];
        public static readonly ContentId[] RareRewardCardIds =
        [
            ContentId.Parse("card-reward-fixture:rare-reward-one"),
            ContentId.Parse("card-reward-fixture:rare-reward-two"),
            ContentId.Parse("card-reward-fixture:rare-reward-three")
        ];

        public PackageInfo Info { get; } = new("card-reward-fixture", "1.0.0");

        public void Register(ContentRegistry registry)
        {
            var map = new ActMapDefinition("entry", "boss",
            [
                new MapPointDefinition("entry", 0, 3, MapPointType.Ancient, ["boss"]),
                new MapPointDefinition("boss", 1, 3, MapPointType.Boss, [])
            ]);
            registry.Register(new RunDefinition(RunId,
                [new ActDefinition(ContentId.Parse("card-reward-fixture:act"), _ => map)]));
            registry.Register(new CharacterDefinition(CharacterId, 30, 25, [StarterCardId],
                cardPools: [CardPoolId]));
            registry.Register(new CardDefinition(StarterCardId, 0, CardType.Attack,
                new AttackEffect(5), Rarity: CardRarity.Basic));
            foreach (var cardId in RewardCardIds)
                registry.Register(new CardDefinition(cardId, 1, CardType.Attack,
                    new AttackEffect(6), Rarity: CardRarity.Common,
                    UpgradedEffect: new AttackEffect(9)));
            foreach (var cardId in RareRewardCardIds)
                registry.Register(new CardDefinition(cardId, 1, CardType.Attack,
                    new AttackEffect(12), Rarity: CardRarity.Rare,
                    UpgradedEffect: new AttackEffect(15)));
            registry.Register(new CardPoolDefinition(CardPoolId, RewardCardIds.Concat(RareRewardCardIds)));
            var enemyActionId = ContentId.Parse("card-reward-fixture:idle");
            registry.Register(new EnemyDefinition(EnemyId, 5, 5,
                [new EnemyActionDefinition(enemyActionId, new GainBlockEffect(0), IntentKind.None)]));
            registry.Register(new EncounterDefinition(EncounterId, RunRoomType.Monster, [EnemyId],
                minGoldReward: 10, maxGoldReward: 20));
        }
    }

    private sealed class RunEventFixturePackage : IRulePackage
    {
        public static readonly ContentId RunId = ContentId.Parse("run-event-fixture:run");
        public static readonly ContentId CharacterId = ContentId.Parse("run-event-fixture:character");
        public static readonly ContentId ActId = ContentId.Parse("run-event-fixture:act");
        public static readonly ContentId EventId = ContentId.Parse("run-event-fixture:ancient");
        public static readonly ContentId FinalEventId = ContentId.Parse("run-event-fixture:final-event");
        public static readonly ContentId CardId = ContentId.Parse("run-event-fixture:card");
        public static readonly ContentId EliteEnemyId = ContentId.Parse("run-event-fixture:elite-enemy");
        public static readonly ContentId EliteEncounterId = ContentId.Parse("run-event-fixture:elite-encounter");
        public static readonly ContentId CommonRelicId = ContentId.Parse("run-event-fixture:common-relic");
        public static readonly ContentId CharacterPoolRelicId = ContentId.Parse("run-event-fixture:character-pool-relic");
        public static readonly ContentId UncommonRelicId = ContentId.Parse("run-event-fixture:uncommon-relic");
        public static readonly ContentId RareRelicId = ContentId.Parse("run-event-fixture:rare-relic");
        public static readonly ContentId FallbackRelicId = ContentId.Parse("run-event-fixture:circlet");
        public static readonly ContentId TradeOptionId = ContentId.Parse("run-event-fixture:trade");
        public static readonly ContentId RelicOptionId = ContentId.Parse("run-event-fixture:gain-relic");
        public static readonly ContentId GoldOptionId = ContentId.Parse("run-event-fixture:gold");
        public static readonly ContentId MaxHpOptionId = ContentId.Parse("run-event-fixture:max-hp");
        public static readonly ContentId MaxHpLossOptionId = ContentId.Parse("run-event-fixture:max-hp-loss");
        public static readonly ContentId CustomRunEffectHandlerId = ContentId.Parse("run-event-fixture:gain-gold");
        public static readonly ContentId CustomRunEffectOptionId = ContentId.Parse("run-event-fixture:custom-effect");
        public static readonly ContentId UnaffordableOptionId = ContentId.Parse("run-event-fixture:unaffordable");
        public static readonly ContentId FatalOptionId = ContentId.Parse("run-event-fixture:fatal");
        public static readonly ContentId FinalOptionId = ContentId.Parse("run-event-fixture:final-option");
        public static readonly ContentId UpgradeOptionId = ContentId.Parse("run-event-fixture:upgrade-option");
        public static readonly ContentId RemoveOptionId = ContentId.Parse("run-event-fixture:remove-option");
        public static readonly ContentId OptionalRemoveOptionId = ContentId.Parse("run-event-fixture:optional-remove-option");
        public static readonly ContentId PredicateOptionId = ContentId.Parse("run-event-fixture:predicate-option");
        public static readonly ContentId ContinuationPageId = ContentId.Parse("run-event-fixture:continuation-page");
        public static readonly ContentId ContinueOptionId = ContentId.Parse("run-event-fixture:continue-option");
        public static readonly ContentId ResumeCombatOptionId = ContentId.Parse("run-event-fixture:resume-combat");
        public static readonly ContentId EndingCombatOptionId = ContentId.Parse("run-event-fixture:ending-combat");
        public static readonly ContentId EventEnemyId = ContentId.Parse("run-event-fixture:event-enemy");
        public static readonly ContentId EventEncounterId = ContentId.Parse("run-event-fixture:event-encounter");
        public static readonly ContentId SelectionTransitionOptionId = ContentId.Parse("run-event-fixture:selection-transition-option");

        public PackageInfo Info { get; } = new("run-event-fixture", "1.0.0");

        public void Register(ContentRegistry registry)
        {
            registry.Register(new GainGoldRunEffectHandler());
            var map = new ActMapDefinition("entry", "boss",
            [
                new MapPointDefinition("entry", 0, 3, MapPointType.Ancient, ["treasure", "boss"]),
                new MapPointDefinition("treasure", 1, 2, MapPointType.Treasure, ["boss"]),
                new MapPointDefinition("boss", 2, 3, MapPointType.Boss, [])
            ]);
            registry.Register(new RunDefinition(RunId,
                [new ActDefinition(ActId, _ => map, ancientIds: [EventId])],
                finalEventId: FinalEventId));
            registry.Register(new CharacterDefinition(CharacterId, 30, 10, [CardId, CardId]));
            registry.Register(new EnemyDefinition(EliteEnemyId, 1, 1,
                [new EnemyActionDefinition(ContentId.Parse("run-event-fixture:elite-wait"),
                    new SequenceEffect(Array.Empty<EffectNode>()), IntentKind.Buff)]));
            registry.Register(new EncounterDefinition(EliteEncounterId, RunRoomType.Elite, [EliteEnemyId]));
            registry.Register(new EnemyDefinition(EventEnemyId, 1, 1,
                [new EnemyActionDefinition(ContentId.Parse("run-event-fixture:event-wait"),
                    new SequenceEffect(Array.Empty<EffectNode>()), IntentKind.None)]));
            registry.Register(new EncounterDefinition(EventEncounterId, RunRoomType.Monster, [EventEnemyId],
                shouldGiveRewards: false));
            registry.Register(new RelicDefinition(CommonRelicId, RunRelicRarity.Common));
            registry.Register(new RelicDefinition(CharacterPoolRelicId, RunRelicRarity.Common,
                InSharedPool: false, CharacterPoolIds: [CharacterId]));
            registry.Register(new RelicDefinition(UncommonRelicId, RunRelicRarity.Uncommon));
            registry.Register(new RelicDefinition(RareRelicId, RunRelicRarity.Rare));
            registry.Register(new RelicDefinition(FallbackRelicId, RunRelicRarity.None,
                InSharedPool: false, IsFallback: true, IsStackable: true));
            registry.Register(new CardDefinition(CardId, 1, CardType.Attack, new AttackEffect(5),
                Rarity: CardRarity.Basic, UpgradedEffect: new AttackEffect(8)));
            registry.Register(new RunEventDefinition(EventId, RunEventKind.Ancient, EventId,
            [
                new RunEventPageDefinition(EventId, "Initial page",
                [
                new RunEventOptionDefinition(RelicOptionId, "Find a relic",
                    [new GainRunRelicEffect(CommonRelicId)]),
                new RunEventOptionDefinition(TradeOptionId, "Trade gold for a card",
                    [new LoseRunGoldEffect(8), new AddCardToRunDeckEffect(CardId)]),
                new RunEventOptionDefinition(GoldOptionId, "Find gold",
                    [new GainRunGoldEffect(5), new HealRunEffect(3)]),
                new RunEventOptionDefinition(MaxHpOptionId, "Gain maximum health",
                    [new GainRunMaxHpEffect(5)]),
                new RunEventOptionDefinition(MaxHpLossOptionId, "Lose maximum health",
                    [new LoseRunMaxHpEffect(10)]),
                new RunEventOptionDefinition(CustomRunEffectOptionId, "Gain gold through a custom run effect",
                    [EffectDsl.ParseRun(
                        """{"op":"customRun","handler":"run-event-fixture:gain-gold","payload":{"amount":4}}""")]),
                new RunEventOptionDefinition(UnaffordableOptionId, "Pay too much",
                    [new LoseRunGoldEffect(11)]),
                new RunEventOptionDefinition(FatalOptionId, "Take lethal damage",
                    [new LoseRunHpEffect(10)]),
                new RunEventOptionDefinition(UpgradeOptionId, "Upgrade a card",
                    [new ChooseRunCardEffect("Choose a card to upgrade", RunCardFilter.Upgradeable,
                        false, new UpgradeSelectedRunCardEffect()), new GainRunGoldEffect(2)]),
                new RunEventOptionDefinition(RemoveOptionId, "Remove a card",
                    [new ChooseRunCardEffect("Choose a card to remove", RunCardFilter.Any,
                        false, new RemoveSelectedRunCardEffect())]),
                new RunEventOptionDefinition(OptionalRemoveOptionId, "Optionally remove a card",
                    [new ChooseRunCardEffect("Choose a card to remove", RunCardFilter.Any,
                        true, new RemoveSelectedRunCardEffect()), new GainRunGoldEffect(1)]),
                new RunEventOptionDefinition(PredicateOptionId, "A predicate-gated option",
                    [new GainRunGoldEffect(1)], new AllRunCondition([
                        new RunMetricCondition(RunMetric.Gold, RunComparison.AtLeast, 100),
                        new AnyRunCondition([
                            new RunMetricCondition(RunMetric.Hp, RunComparison.AtLeast, 20),
                            new RunMetricCondition(RunMetric.Hp, RunComparison.LessThan, 5)
                        ])
                    ]), nextPageId: ContinuationPageId),
                new RunEventOptionDefinition(SelectionTransitionOptionId, "Select then continue",
                    [new ChooseRunCardEffect("Choose a card to remove", RunCardFilter.Any,
                        false, new RemoveSelectedRunCardEffect())], nextPageId: ContinuationPageId)
                ]),
                new RunEventPageDefinition(ContinuationPageId, "Continuation page",
                [
                    new RunEventOptionDefinition(ContinueOptionId, "Finish event",
                        [new GainRunGoldEffect(2)]),
                    new RunEventOptionDefinition(ResumeCombatOptionId, "Fight and return",
                        [new EnterRunCombatEffect(EventEncounterId)]),
                    new RunEventOptionDefinition(EndingCombatOptionId, "Fight and leave event",
                        [new EnterRunCombatEffect(EventEncounterId, ResumeEventAfterCombat: false)])
                ])
            ]));
            registry.Register(new RunEventDefinition(FinalEventId, RunEventKind.Final,
            [
                new RunEventOptionDefinition(FinalOptionId, "Complete the run", [new WinRunEffect()])
            ]));
        }

        private sealed class GainGoldRunEffectHandler : ICustomRunEffectHandler
        {
            public ContentId Id => CustomRunEffectHandlerId;

            public void Execute(RunEffectExecutionContext context, System.Text.Json.JsonElement payload) =>
                context.GainGold(payload.GetProperty("amount").GetInt32());
        }
    }

    public void BaseAnchorAndVajraApplyTheirCombatStartEffects()
    {
        var definition = Build(new BaseSpirePackage());
        var runEnvironment = new RunEnvironment(definition, BaseSpireIds.StandardRun, BaseSpireIds.Ironclad);
        var run = runEnvironment.Start(817);
        run.State.Relics.Add(BaseSpireIds.Anchor);
        run.State.Relics.Add(BaseSpireIds.Vajra);
        run.State.Relics.Add(BaseSpireIds.CharonsAshes);
        run.State.Relics.Add(BaseSpireIds.SelfFormingClay);
        run.State.Deck.Clear();
        run.State.Deck.Add(new CardInstance("run-offering", BaseSpireIds.Offering));
        runEnvironment.PushSubRoom(run, RunRoomType.Monster);

        var combatEnvironment = new CombatEnvironment(definition);
        var combat = combatEnvironment.StartForRun(run, BaseSpireIds.DampCultist.ToString());

        Assert.Equal(10, combat.State.Player.Creature.Block);
        Assert.Equal(1, combat.State.Player.Creature.PowerAmount(BaseSpireIds.Strength));
        var enemyHp = combat.State.Enemy.Hp;
        var result = combatEnvironment.Step(combat, new PlayCardAction(0, combat.State.Player.Id));
        Assert.Equal(enemyHp - 3, combat.State.Enemy.Hp);
        Assert.Contains(result.Events, gameEvent => gameEvent.Type == "damage" && gameEvent.Amount == 3);
        combatEnvironment.Step(combat, new EndTurnAction());
        Assert.Equal(0, combat.State.Player.Creature.PowerAmount(BaseSpireIds.SelfFormingClayPower));
        combatEnvironment.Step(combat, new EndTurnAction());
        Assert.Equal(9, combat.State.Player.Creature.Block);
        Assert.Equal(0, combat.State.Player.Creature.PowerAmount(BaseSpireIds.SelfFormingClayPower));
        combat.State.Player.Creature.Hp = 1;
        combat.State.Player.Creature.Block = 0;
        combat.State.Enemy.Strength = 10;
        combat.State.EnemyActionIndex = 1;
        combatEnvironment.Step(combat, new EndTurnAction());
        Assert.Equal(0, combat.State.Player.Creature.Hp);
        Assert.Equal(0, combat.State.Player.Creature.PowerAmount(BaseSpireIds.SelfFormingClayPower));
    }

    public void BaseRedSkullTracksCombatHealingAndHpLossAcrossItsThreshold()
    {
        var definition = Build(new BaseSpirePackage(), new RedSkullHealFixturePackage());
        var runEnvironment = new RunEnvironment(definition, BaseSpireIds.StandardRun, BaseSpireIds.Ironclad);
        var run = runEnvironment.Start(818);
        run.State.Hp = 40;
        run.State.Relics.Add(BaseSpireIds.RedSkull);
        run.State.Deck.Clear();
        run.State.Deck.Add(new CardInstance("red-skull-heal", RedSkullHealFixturePackage.HealCard));
        run.State.Deck.Add(new CardInstance("red-skull-hp-loss", RedSkullHealFixturePackage.HpLossCard));
        runEnvironment.PushSubRoom(run, RunRoomType.Monster);

        var combatEnvironment = new CombatEnvironment(definition);
        var combat = combatEnvironment.StartForRun(run, BaseSpireIds.DampCultist.ToString());
        Assert.Equal(3, combat.State.Player.Creature.PowerAmount(BaseSpireIds.Strength));

        combatEnvironment.Step(combat, new PlayCardAction(0, combat.State.Player.Id));
        Assert.Equal(41, combat.State.Player.Creature.Hp);
        Assert.Equal(0, combat.State.Player.Creature.PowerAmount(BaseSpireIds.Strength));

        combatEnvironment.Step(combat, new PlayCardAction(0, combat.State.Player.Id));
        Assert.Equal(39, combat.State.Player.Creature.Hp);
        Assert.Equal(3, combat.State.Player.Creature.PowerAmount(BaseSpireIds.Strength));
    }

    public void BaseDemonTongueHealsOnlyForFirstPlayerSideHitEachTurn()
    {
        var definition = Build(new BaseSpirePackage(), new DemonTongueFixturePackage());
        var runEnvironment = new RunEnvironment(definition, BaseSpireIds.StandardRun, BaseSpireIds.Ironclad);
        var run = runEnvironment.Start(819);
        run.State.Hp = 40;
        run.State.Relics.Add(BaseSpireIds.DemonTongue);
        run.State.Deck.Clear();
        run.State.Deck.Add(new CardInstance("demon-tongue-first-hit", DemonTongueFixturePackage.SelfDamageCard));
        run.State.Deck.Add(new CardInstance("demon-tongue-second-hit", DemonTongueFixturePackage.SelfDamageCard));
        runEnvironment.PushSubRoom(run, RunRoomType.Monster);

        var combatEnvironment = new CombatEnvironment(definition);
        var combat = combatEnvironment.StartForRun(run, BaseSpireIds.DampCultist.ToString());
        combatEnvironment.Step(combat, new PlayCardAction(0, combat.State.Player.Id));
        Assert.Equal(40, combat.State.Player.Creature.Hp);
        Assert.Contains(combat.State.Events, gameEvent => gameEvent.Type == "heal" && gameEvent.Amount == 4);

        combatEnvironment.Step(combat, new PlayCardAction(0, combat.State.Player.Id));
        Assert.Equal(36, combat.State.Player.Creature.Hp);
        Assert.Equal(1, combat.State.Events.Count(gameEvent => gameEvent.Type == "heal"));

        combatEnvironment.Step(combat, new EndTurnAction());
        combat.State.Player.MutableHand.Add(new CardInstance("demon-tongue-next-turn",
            DemonTongueFixturePackage.SelfDamageCard));
        combatEnvironment.Step(combat, new PlayCardAction(0, combat.State.Player.Id));
        Assert.Equal(36, combat.State.Player.Creature.Hp);
        Assert.Equal(2, combat.State.Events.Count(gameEvent => gameEvent.Type == "heal"));
    }

    public void BasePaperPhrogAddsTwentyFivePercentToPoweredDamageAgainstVulnerableEnemies()
    {
        var definition = Build(new BaseSpirePackage());
        var runEnvironment = new RunEnvironment(definition, BaseSpireIds.StandardRun, BaseSpireIds.Ironclad);
        var run = runEnvironment.Start(820);
        run.State.Relics.Add(BaseSpireIds.PaperPhrog);
        run.State.Deck.Clear();
        run.State.Deck.Add(new CardInstance("paper-phrog-bash", BaseSpireIds.Bash));
        run.State.Deck.Add(new CardInstance("paper-phrog-strike", BaseSpireIds.StrikeIronclad));
        runEnvironment.PushSubRoom(run, RunRoomType.Monster);

        var combatEnvironment = new CombatEnvironment(definition);
        var combat = combatEnvironment.StartForRun(run, BaseSpireIds.DampCultist.ToString());
        combat.State.Player.MutableHand.Clear();
        combat.State.Player.MutableHand.Add(new CardInstance("paper-phrog-bash", BaseSpireIds.Bash));
        combat.State.Player.MutableHand.Add(new CardInstance("paper-phrog-strike", BaseSpireIds.StrikeIronclad));
        var enemyId = combat.State.Enemy.Id;
        var startingEnemyHp = combat.State.Enemy.Hp;

        combatEnvironment.Step(combat, new PlayCardAction(0, enemyId));
        Assert.Equal(2, combat.State.Enemy.PowerAmount(BaseSpireIds.Vulnerable));
        combatEnvironment.Step(combat, new PlayCardAction(0, enemyId));

        Assert.Equal(startingEnemyHp - 18, combat.State.Enemy.Hp);
        Assert.Contains(combat.State.Events, gameEvent => gameEvent.Type == "damage"
            && gameEvent.Source == combat.State.Player.Id && gameEvent.Target == enemyId && gameEvent.Amount == 10);
    }

    public void BaseRuinedHelmetDoublesTheFirstStrengthApplicationEachCombat()
    {
        var definition = Build(new BaseSpirePackage(), new RuinedHelmetStrengthFixturePackage());
        var runEnvironment = new RunEnvironment(definition, BaseSpireIds.StandardRun, BaseSpireIds.Ironclad);
        var run = runEnvironment.Start(821);
        run.State.Relics.Add(BaseSpireIds.RuinedHelmet);
        run.State.Deck.Clear();
        run.State.Deck.Add(new CardInstance("ruined-helmet-first-strength", RuinedHelmetStrengthFixturePackage.StrengthCard));
        run.State.Deck.Add(new CardInstance("ruined-helmet-second-strength", RuinedHelmetStrengthFixturePackage.StrengthCard));
        runEnvironment.PushSubRoom(run, RunRoomType.Monster);

        var combatEnvironment = new CombatEnvironment(definition);
        var combat = combatEnvironment.StartForRun(run, BaseSpireIds.DampCultist.ToString());
        combat.State.Player.MutableHand.Clear();
        combat.State.Player.MutableHand.Add(new CardInstance("ruined-helmet-first-strength", RuinedHelmetStrengthFixturePackage.StrengthCard));
        combat.State.Player.MutableHand.Add(new CardInstance("ruined-helmet-second-strength", RuinedHelmetStrengthFixturePackage.StrengthCard));

        combatEnvironment.Step(combat, new PlayCardAction(0, combat.State.Player.Id));
        combatEnvironment.Step(combat, new PlayCardAction(0, combat.State.Player.Id));

        Assert.Equal(6, combat.State.Player.Creature.PowerAmount(BaseSpireIds.Strength));
        Assert.Equal(new[] { 4, 2 }, combat.State.Events.Where(gameEvent => gameEvent.Type == "power"
            && gameEvent.Target == combat.State.Player.Id).Select(gameEvent => gameEvent.Amount));
    }

    public void BaseFirePotionUsesUnpoweredDamageAndConsumesItsRunInventorySlot()
    {
        var definition = Build(new BaseSpirePackage());
        Assert.Equal(new AttackEffect(20, TargetSelector.SelectedTarget, DamageProperties.Unpowered),
            definition.Potions[BaseSpireIds.FirePotion].Effect);
        var runEnvironment = new RunEnvironment(definition, BaseSpireIds.StandardRun, BaseSpireIds.Ironclad);
        var run = runEnvironment.Start(822);
        run.State.Relics.Add(BaseSpireIds.Vajra);
        Assert.True(run.State.TryAddPotion(BaseSpireIds.BlockPotion));
        Assert.True(run.State.TryAddPotion(BaseSpireIds.StrengthPotion));
        Assert.True(run.State.TryAddPotion(BaseSpireIds.FirePotion));
        runEnvironment.PushSubRoom(run, RunRoomType.Monster);

        var combatEnvironment = new CombatEnvironment(definition);
        var combat = combatEnvironment.StartForRun(run, BaseSpireIds.DampCultist.ToString());
        var enemyId = combat.State.Enemy.Id;
        var startingEnemyHp = combat.State.Enemy.Hp;
        combat.State.ApplyPower(combat, combat.State.Player.Creature, BaseSpireIds.Weak, 2,
            combat.State.Player.Creature);
        combat.State.ApplyPower(combat, combat.State.Enemy, BaseSpireIds.Vulnerable, 2,
            combat.State.Player.Creature);
        var usePotion = new UsePotionAction(2, enemyId);

        Assert.True(combatEnvironment.LegalActions(combat).Contains(usePotion));
        Assert.Equal((ContentId?)BaseSpireIds.FirePotion, combatEnvironment.Observe(combat).Potions[2]);
        combatEnvironment.Step(combat, usePotion);

        Assert.Equal(startingEnemyHp - 20, combat.State.Enemy.Hp);
        Assert.Equal((ContentId?)BaseSpireIds.BlockPotion, combatEnvironment.Observe(combat).Potions[0]);
        Assert.Equal((ContentId?)BaseSpireIds.StrengthPotion, combatEnvironment.Observe(combat).Potions[1]);
        Assert.Equal(null, combatEnvironment.Observe(combat).Potions[2]);
        Assert.Contains(combat.State.Events, gameEvent => gameEvent.Type == "damage" && gameEvent.Amount == 20);
        Assert.Contains(combat.State.Events, gameEvent => gameEvent.Type == "potion_used");
    }

    public void FlexPotionReusesTemporaryStrengthAcrossPotionsAndCards()
    {
        var definition = Build(new BaseSpirePackage());
        var runEnvironment = new RunEnvironment(definition, BaseSpireIds.StandardRun, BaseSpireIds.Ironclad);
        var run = runEnvironment.Start(827);
        run.State.Deck.Clear();
        var setup = new CardInstance("flex-setup", BaseSpireIds.SetupStrike);
        var strike = new CardInstance("flex-strike", BaseSpireIds.StrikeIronclad);
        run.State.Deck.Add(setup);
        run.State.Deck.Add(strike);
        Assert.True(run.State.TryAddPotion(BaseSpireIds.FlexPotion));
        Assert.True(run.State.TryAddPotion(BaseSpireIds.FlexPotion));
        Assert.True(run.State.TryAddPotion(BaseSpireIds.StrengthPotion));
        runEnvironment.PushSubRoom(run, RunRoomType.Monster);
        var environment = new CombatEnvironment(definition);
        var combat = environment.StartForRun(run, BaseSpireIds.DampCultist.ToString());
        combat.State.Player.MutableHand.Clear();
        combat.State.Player.MutableHand.Add(setup);
        combat.State.Player.MutableHand.Add(strike);
        var startingHp = combat.State.Enemy.Hp;

        environment.Step(combat, new UsePotionAction(2, combat.State.Player.Id));
        environment.Step(combat, new UsePotionAction(0, combat.State.Player.Id));
        environment.Step(combat, new UsePotionAction(1, combat.State.Player.Id));
        Assert.Equal(3, combat.State.Player.Energy);
        Assert.True(run.State.Potions.All(potion => potion is null));
        Assert.Equal(10, environment.Observe(combat).PlayerPowers[BaseSpireIds.FlexPotionPower]);
        Assert.Equal(12, combat.State.Player.Creature.Strength);
        environment.Step(combat, new PlayCardAction(0, combat.State.Enemy.Id));
        Assert.Equal(startingHp - 19, combat.State.Enemy.Hp);
        Assert.Equal(14, combat.State.Player.Creature.Strength);
        environment.Step(combat, new PlayCardAction(0, combat.State.Enemy.Id));
        Assert.Equal(startingHp - 39, combat.State.Enemy.Hp);

        environment.Step(combat, new EndTurnAction());
        var powers = environment.Observe(combat).PlayerPowers;
        Assert.Equal(2, powers[BaseSpireIds.Strength]);
        Assert.False(powers.ContainsKey(BaseSpireIds.FlexPotionPower));
        Assert.False(powers.ContainsKey(BaseSpireIds.SetupStrikePower));
    }

    public void MigratedPotionDslUpgradesHandAndAppliesEffectsToTheirDeclaredTargets()
    {
        var definition = Build(new BaseSpirePackage());
        Assert.Equal(6, BaseSpireContent.LoadPotions().Count);
        var runEnvironment = new RunEnvironment(definition, BaseSpireIds.StandardRun, BaseSpireIds.Ironclad);
        var run = runEnvironment.Start(828);
        Assert.True(run.State.TryAddPotion(BaseSpireIds.BlockPotion));
        Assert.True(run.State.TryAddPotion(BaseSpireIds.BlessingOfTheForge));
        Assert.True(run.State.TryAddPotion(BaseSpireIds.BeetleJuice));
        runEnvironment.PushSubRoom(run, RunRoomType.Monster);
        var environment = new CombatEnvironment(definition);
        var combat = environment.StartForRun(run, BaseSpireIds.DampCultist.ToString());
        environment.Step(combat, new UsePotionAction(0, combat.State.Player.Id));
        Assert.Equal(12, combat.State.Player.Creature.Block);
        Assert.Equal(0, combat.State.Enemy.Block);
        environment.Step(combat, new UsePotionAction(1, combat.State.Player.Id));
        Assert.True(combat.State.Player.HandInstances.All(card => card.UpgradeLevel == 1));
        environment.Step(combat, new UsePotionAction(2, combat.State.Enemy.Id));
        Assert.Equal(4, combat.State.Enemy.PowerAmount(BaseSpireIds.Shrink));
        Assert.Equal(0, combat.State.Player.Creature.PowerAmount(BaseSpireIds.Shrink));
        Assert.Equal(3, combat.State.Player.Energy);
    }

    public void PotionDslValidatesUsageFlagsAndCrossPackageReferences()
    {
        const string source = """
            package potion-mod {
              potion Training {
                rarity = uncommon usage = combatOnly target = self
                inSharedPool = false characterPools = ["base-spire:ironclad"]
                canGenerateInCombat = false
                use = applyPower("base-spire:strength", 3, target=self)
              }
            }
            """;
        static IReadOnlyList<PotionDefinition> Compile(string text) =>
            SpireCombatDslCompiler.CompilePotions(DslSourceParser.Parse(text, "potions.spire"));
        var potion = Compile(source).Single();
        Assert.Equal(new ApplyPowerEffect(BaseSpireIds.Strength, 3, TargetSelector.Self), potion.Effect);
        Assert.Equal(BaseSpireIds.Ironclad, potion.CharacterPoolIds!.Single());
        Assert.False(potion.InSharedPool);
        Assert.False(potion.CanBeGeneratedInCombat);
        var definition = Build(new BaseSpirePackage(), new PotionDslFixturePackage(source));
        Assert.Contains(definition.PotionPoolFor(BaseSpireIds.Ironclad), item => item.Id == potion.Id);
        Assert.Throws<InvalidOperationException>(() => Build(new BaseSpirePackage(),
            new PotionDslFixturePackage(source.Replace("base-spire:ironclad", "missing:character"))));
        Assert.Throws<InvalidOperationException>(() => Build(new BaseSpirePackage(),
            new PotionDslFixturePackage(source.Replace("base-spire:strength", "missing:power"))));
        Assert.Throws<DslSourceException>(() => Compile(source.Replace("combatOnly", "anyTime")));
        Assert.Throws<DslSourceException>(() => Compile(source.Replace("usage = combatOnly", "")));
        Assert.Throws<DslSourceException>(() => Compile(source.Replace("inSharedPool = false", "inSharedPool = true")));
        Assert.Throws<DslSourceException>(() => Compile(source.Replace("use =", "unknown =")));
        const string attackSource = """
            package potion-mod {
              potion Attack { rarity = common usage = combatOnly target = enemy
                use = attack(4, properties=[unpowered, unblockable]) }
            }
            """;
        Assert.Equal(new AttackEffect(4, TargetSelector.SelectedTarget,
            DamageProperties.Unpowered | DamageProperties.Unblockable), Compile(attackSource).Single().Effect);
        Assert.Throws<DslSourceException>(() => Compile(attackSource.Replace("unblockable", "unpowered")));
        Assert.Throws<DslSourceException>(() => Compile(attackSource.Replace("unblockable", "unknown")));
        Assert.Throws<DslSourceException>(() => Compile(attackSource.Replace("[unpowered, unblockable]", "unpowered")));
    }

    private sealed class PotionDslFixturePackage(string source) : IRulePackage
    {
        public PackageInfo Info { get; } = new("potion-mod", "1.0.0");
        public void Register(ContentRegistry registry)
        {
            foreach (var potion in SpireCombatDslCompiler.CompilePotions(DslSourceParser.Parse(source)))
                registry.Register(potion);
        }
    }

    public void PotionRewardUsesPityOddsAndRespectsFixedInventoryCapacity()
    {
        var definition = Build(new BaseSpirePackage(), new PotionRewardFixturePackage());
        var ironcladPotions = definition.PotionPoolFor(BaseSpireIds.Ironclad);
        Assert.Contains(ironcladPotions, potion => potion.Id == BaseSpireIds.BeetleJuice);
        Assert.False(ironcladPotions.Any(potion =>
            potion.Id == PotionRewardFixturePackage.ForeignCharacterPotionId));
        var runEnvironment = new RunEnvironment(definition, BaseSpireIds.StandardRun, BaseSpireIds.Ironclad);
        var combatEnvironment = new CombatEnvironment(definition);

        var run = runEnvironment.Start(823);
        Assert.True(run.State.TryAddPotion(BaseSpireIds.FirePotion));
        Assert.True(run.State.TryAddPotion(BaseSpireIds.BlockPotion));
        var observation = CompletePotionRewardCombat(runEnvironment, combatEnvironment, run);
        Assert.Equal(0.3f, run.State.PotionRewardChance);
        var potionReward = observation.RoomStack[^1].Rewards
            .Single(reward => reward.Kind == RunRewardKind.Potion);
        Assert.True(potionReward.PotionId.HasValue);
        var claimed = runEnvironment.Step(run, new ClaimPotionRewardAction(potionReward.Id));
        Assert.Equal(potionReward.PotionId, claimed.Potions[2]);
        Assert.True(claimed.Potions.All(potion => potion.HasValue));

        var fullRun = runEnvironment.Start(824);
        Assert.True(fullRun.State.TryAddPotion(BaseSpireIds.FirePotion));
        Assert.True(fullRun.State.TryAddPotion(BaseSpireIds.BlockPotion));
        Assert.True(fullRun.State.TryAddPotion(BaseSpireIds.StrengthPotion));
        var fullInventoryRewards = CompletePotionRewardCombat(runEnvironment, combatEnvironment, fullRun);
        Assert.False(runEnvironment.LegalActions(fullRun).Any(action => action is ClaimPotionRewardAction));
        Assert.Contains(runEnvironment.LegalActions(fullRun), action => action is SkipRewardSetAction);
        Assert.True(fullInventoryRewards.RoomStack[^1].Rewards.Any(reward => reward.Kind == RunRewardKind.Potion));
    }

    public void PotionCapacityChangesPreserveSlotsAndPotionBeltAddsTwoSlots()
    {
        var definition = Build(new BaseSpirePackage());
        var runEnvironment = new RunEnvironment(definition, BaseSpireIds.StandardRun, BaseSpireIds.Ironclad);
        var run = runEnvironment.Start(825);
        Assert.True(run.State.TryAddPotion(BaseSpireIds.FirePotion));
        Assert.True(run.State.TryAddPotion(BaseSpireIds.StrengthPotion));
        Assert.True(run.State.TryAddPotion(BaseSpireIds.BeetleJuice));
        Assert.True(run.State.TryUsePotion(1, out _));
        run.State.ChangePotionCapacity(2);
        Assert.Equal(5, run.State.PotionCapacity);
        Assert.Equal(5, run.State.Potions.Count);
        Assert.Equal(null, run.State.Potions[1]);
        Assert.True(run.State.TryAddPotion(BaseSpireIds.BlockPotion));
        Assert.True(run.State.TryAddPotion(BaseSpireIds.FirePotion));
        Assert.True(run.State.TryUsePotion(1, out _));

        run.State.ChangePotionCapacity(-2);
        Assert.Equal(3, run.State.PotionCapacity);
        Assert.Equal(3, run.State.Potions.Count);
        Assert.Equal(BaseSpireIds.FirePotion, run.State.Potions[1]);

        var hook = definition.RunHooks.Single(candidate => candidate.Id ==
            ContentId.Parse("base-spire:potion-belt-hook"));
        hook.AfterRelicObtained(new RunHookContext(run.State, hook.Id), BaseSpireIds.PotionBelt);
        Assert.Equal(5, run.State.PotionCapacity);
    }

    public void PotionGenerationEffectUsesItsOwnRngAndFillsOpenSlotsWithoutDuplicates()
    {
        Assert.True(EffectDsl.Parse("""{"op":"generatePotion","count":2}""") is GeneratePotionEffect { Count: 2 });
        var definition = Build(new BaseSpirePackage(), new PotionGenerationFixturePackage());
        var runEnvironment = new RunEnvironment(definition, BaseSpireIds.StandardRun, BaseSpireIds.Ironclad);
        var run = runEnvironment.Start(826);
        Assert.True(run.State.TryAddPotion(PotionGenerationFixturePackage.GeneratorPotionId));
        Assert.True(run.State.TryAddPotion(BaseSpireIds.BlockPotion));
        runEnvironment.PushSubRoom(run, RunRoomType.Monster);
        var combatEnvironment = new CombatEnvironment(definition);
        var combat = combatEnvironment.StartForRun(run, BaseSpireIds.DampCultist.ToString());
        var rewardsCounter = run.State.Random.Get("rewards").Counter;

        combatEnvironment.Step(combat,
            new UsePotionAction(0, combat.State.Player.Id));

        var generated = run.State.Potions.OfType<ContentId>().ToArray();
        Assert.Equal(3, generated.Length);
        Assert.Equal(generated.Length, generated.Distinct().Count());
        Assert.Equal(4, run.State.Random.Get("combat-potion-generation").Counter);
        Assert.Equal(rewardsCounter, run.State.Random.Get("rewards").Counter);
        Assert.Contains(combat.State.Events, gameEvent => gameEvent.Type == "potion_generated");
    }

    private static RunObservation CompletePotionRewardCombat(RunEnvironment runEnvironment,
        CombatEnvironment combatEnvironment, RunSession run)
    {
        runEnvironment.PushSubRoom(run, RunRoomType.Monster);
        var frame = run.State.RoomStack[^1];
        run.State.RoomStack[^1] = frame with { EncounterId = PotionRewardFixturePackage.EncounterId };
        var combat = combatEnvironment.StartForRun(run, PotionRewardFixturePackage.EnemyId.ToString());
        combat.State.Enemy.Hp = 1;
        combat.State.Player.MutableHand.Clear();
        combat.State.Player.MutableHand.Add(new CardInstance($"potion-reward-strike-{run.State.Random.Get("rewards").Counter}",
            BaseSpireIds.StrikeIronclad));
        combatEnvironment.Step(combat, new PlayCardAction(0, combat.State.Enemy.Id));
        return runEnvironment.CompleteCombat(run, combat);
    }

    private sealed class PotionRewardFixturePackage : IRulePackage
    {
        public static readonly ContentId EnemyId = ContentId.Parse("potion-reward-fixture:enemy");
        public static readonly ContentId EncounterId = ContentId.Parse("potion-reward-fixture:encounter");
        public static readonly ContentId ForeignCharacterId = ContentId.Parse("potion-reward-fixture:foreign-character");
        public static readonly ContentId ForeignCharacterPotionId = ContentId.Parse("potion-reward-fixture:foreign-potion");
        public PackageInfo Info { get; } = new("potion-reward-fixture", "1.0.0");

        public void Register(ContentRegistry registry)
        {
            registry.Register(new CharacterDefinition(ForeignCharacterId, 60, 99,
                [BaseSpireIds.StrikeIronclad]));
            registry.Register(new PotionDefinition(ForeignCharacterPotionId, PotionRarity.Rare,
                new GainBlockEffect(1), InSharedPool: false, CharacterPoolIds: [ForeignCharacterId]));
            registry.Register(new EnemyDefinition(EnemyId, 1, 1,
                [new EnemyActionDefinition(ContentId.Parse("potion-reward-fixture:idle"),
                    new GainBlockEffect(0), IntentKind.None)]));
            registry.Register(new EncounterDefinition(EncounterId, RunRoomType.Monster, [EnemyId],
                minGoldReward: 1, maxGoldReward: 1));
            registry.Register(new ForcePotionRewardHook());
        }
    }

    private sealed class PotionGenerationFixturePackage : IRulePackage
    {
        public static readonly ContentId GeneratorPotionId = ContentId.Parse("potion-generation-fixture:generator");
        public PackageInfo Info { get; } = new("potion-generation-fixture", "1.0.0");

        public void Register(ContentRegistry registry) => registry.Register(new PotionDefinition(
            GeneratorPotionId, PotionRarity.Common, new GeneratePotionEffect(2)));
    }

    private sealed class ForcePotionRewardHook : IRunHook
    {
        public ContentId Id { get; } = ContentId.Parse("potion-reward-fixture:force-hook");
        public void AfterCombatVictory(RunHookContext context) { }
        public bool ShouldForcePotionReward(RunObservation observation, RunRoomType roomType) => true;
    }

    private sealed class RuinedHelmetStrengthFixturePackage : IRulePackage
    {
        public static readonly ContentId StrengthCard = ContentId.Parse("ruined-helmet-fixture:strength-card");
        public PackageInfo Info { get; } = new("ruined-helmet-fixture", "1.0.0");

        public void Register(ContentRegistry registry) => registry.Register(new CardDefinition(StrengthCard, 0,
            CardType.Skill, new ApplyPowerEffect(BaseSpireIds.Strength, 2), Target: TargetSelector.Self,
            Rarity: CardRarity.Basic));
    }

    private sealed class DemonTongueFixturePackage : IRulePackage
    {
        public static readonly ContentId SelfDamageCard = ContentId.Parse("demon-tongue-fixture:self-damage");
        public PackageInfo Info { get; } = new("demon-tongue-fixture", "1.0.0");

        public void Register(ContentRegistry registry) => registry.Register(new CardDefinition(SelfDamageCard, 0,
            CardType.Attack, new AttackEffect(4, TargetSelector.Self), Target: TargetSelector.Self,
            Rarity: CardRarity.Basic));
    }

    private sealed class RedSkullHealFixturePackage : IRulePackage
    {
        public static readonly ContentId HealCard = ContentId.Parse("red-skull-fixture:heal");
        public static readonly ContentId HpLossCard = ContentId.Parse("red-skull-fixture:hp-loss");
        public PackageInfo Info { get; } = new("red-skull-fixture", "1.0.0");

        public void Register(ContentRegistry registry)
        {
            registry.Register(new CardDefinition(HealCard, 0, CardType.Skill,
                new HealEffect(1), Target: TargetSelector.Self, Rarity: CardRarity.Basic));
            registry.Register(new CardDefinition(HpLossCard, 0, CardType.Skill,
                new LoseHpEffect(2), Target: TargetSelector.Self, Rarity: CardRarity.Basic));
        }
    }

    private sealed class CardRewardOddsHook : IRunHook
    {
        public ContentId Id { get; } = ContentId.Parse("test-fixture:card-reward-odds-hook");
        public int CallCount { get; private set; }
        public List<RunRoomType> RoomTypes { get; } = [];

        public void AfterCombatVictory(RunHookContext context) { }

        public CardRewardOdds ModifyCardRewardOdds(RunObservation observation, RunRoomType roomType,
            CardRewardOdds odds)
        {
            CallCount++;
            RoomTypes.Add(roomType);
            Assert.Equal(RunRoomType.Monster, observation.CurrentRoom);
            return new CardRewardOdds(1f, 0f);
        }
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

    private sealed class RetainOneCardHook : IRuleHook
    {
        public ContentId Id { get; } = ContentId.Parse("retain-hook:plans");
        public IReadOnlyList<CardInstance> FlushedCards { get; private set; } = [];
        public IReadOnlyList<CardInstance> RetainedCards { get; private set; } = [];

        public void OnEvent(RuleHookContext context, GameEvent gameEvent) { }

        public EffectNode? BeforeFlush(RuleHookContext context, string playerId) =>
            EffectDsl.Parse("""{"op":"chooseCardFromPile","from":"hand","to":null,"prompt":"Choose a card to retain","filter":"notRetained","onSelected":{"op":"retainSelectedCard"}}""");

        public EffectNode? AfterFlush(RuleHookContext context, string playerId,
            IReadOnlyList<CardInstance> flushedCards, IReadOnlyList<CardInstance> retainedCards)
        {
            FlushedCards = flushedCards;
            RetainedCards = retainedCards;
            return null;
        }
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

    private sealed class ConditionalEnemyTransitionPackage : IRulePackage
    {
        public PackageInfo Info { get; } = new("conditional-enemy", "1.0.0");

        public void Register(ContentRegistry registry)
        {
            var focusPower = ContentId.Parse("conditional-enemy:focus");
            var opener = ContentId.Parse("conditional-enemy:opener");
            var fallback = ContentId.Parse("conditional-enemy:fallback");
            var lowHealth = ContentId.Parse("conditional-enemy:low-health-branch");
            var highHealth = ContentId.Parse("conditional-enemy:high-health-branch");
            registry.Register(new PowerDefinition(focusPower));
            var startingTransition = new EnemyActionTransition([
                new EnemyActionChoice(opener, Condition: new EnemyMetricCondition(
                    EnemyActionMetric.EnemyIndex, RunComparison.Equal, 0)),
                new EnemyActionChoice(fallback)
            ], selectionMode: EnemyActionSelectionMode.FirstMatchingCondition);
            var healthTransition = new EnemyActionTransition([
                new EnemyActionChoice(lowHealth, Condition: new AllEnemyCondition([
                    new EnemyMetricCondition(EnemyActionMetric.PlayerHp, RunComparison.AtMost, 97),
                    new NotEnemyCondition(new EnemyPowerCondition(EnemyActionSide.Player, focusPower,
                        RunComparison.AtLeast, 1))
                ])),
                new EnemyActionChoice(highHealth)
            ], selectionMode: EnemyActionSelectionMode.FirstMatchingCondition);
            registry.Register(new EnemyDefinition(ContentId.Parse("conditional-enemy:enemy"), 20, 20,
            [
                new EnemyActionDefinition(opener, new AttackEffect(3), IntentKind.Attack, 3,
                    Transition: healthTransition),
                new EnemyActionDefinition(fallback, new AttackEffect(5), IntentKind.Attack, 5),
                new EnemyActionDefinition(lowHealth, new AttackEffect(7), IntentKind.Attack, 7),
                new EnemyActionDefinition(highHealth, new AttackEffect(9), IntentKind.Attack, 9)
            ], startingTransition: startingTransition));
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
            registry.Register(new StateVariableDefinition(ContentId.Parse("partial-mod:orphan-state"),
                StateVariableScope.Run, StateVariableType.Int, 5));
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

    private sealed class TemporaryStackDeltaHook : IRuleHook
    {
        public ContentId Id { get; } = ContentId.Parse("lifecycle-test:delta-hook");
        public void OnEvent(RuleHookContext context, GameEvent gameEvent) { }
        public int ModifyPowerAmountReceived(RuleHookContext context, ContentId powerId, PowerRole powerRole,
            string targetId, string applierId, int amount) =>
            powerId == BaseSpireIds.SetupStrikePower && amount < 0 ? amount * 2 : amount;
    }

    private sealed class PowerLifecycleFixturePackage(string source) : IRulePackage
    {
        public PackageInfo Info { get; } = new("lifecycle", "1.0.0");

        public void Register(ContentRegistry registry)
        {
            var content = SpireCombatBehaviorDslCompiler.Compile(DslSourceParser.Parse(source, "lifecycle.spire"));
            foreach (var power in content.Powers) registry.Register(power);
        }
    }

    private sealed class PowerExpiryChoicePackage : IRulePackage
    {
        public static readonly ContentId PowerId = ContentId.Parse("expiry-choice:temporary");
        public PackageInfo Info { get; } = new("expiry-choice", "1.0.0");

        public void Register(ContentRegistry registry) => registry.Register(new PowerDefinition(PowerId,
            PowerTrigger.AfterPlayerTurnEnd,
            TriggerRuleEffect: new ChooseEffect("Reduce temporary strength", [
                new ChoiceOption("Reduce", new ApplyPowerEffect(PowerId, -1))]),
            OnStackChange: new PowerStackAdjustment(BaseSpireIds.Strength, 1),
            OnExpire: new PowerStackAdjustment(BaseSpireIds.Strength, -1), RemoveAfterOwnerTurnEnd: true));
    }

    private sealed class CardPoolExtensionPackage : IRulePackage
    {
        public static readonly ContentId CardId = ContentId.Parse("pool-extension:training-strike");
        public PackageInfo Info { get; } = new("pool-extension", "1.0.0");

        public void Register(ContentRegistry registry)
        {
            var source = DslSourceParser.Parse("""
                package pool-extension {
                  card TrainingStrike {
                    pools = ["base-spire:ironclad-card-pool"]
                    type = attack
                    rarity = common
                    cost = 1
                    target = enemy
                    play = attack(6)
                  }
                }
                """, "pool-extension.spire");
            registry.Register(SpireCombatDslCompiler.CompileCards(source).Single());
        }
    }

    private sealed class UnknownPoolCardPackage : IRulePackage
    {
        public PackageInfo Info { get; } = new("unknown-pool-card", "1.0.0");

        public void Register(ContentRegistry registry) => registry.Register(new CardDefinition(
            ContentId.Parse("unknown-pool-card:card"), 0, CardType.Skill, new GainBlockEffect(1))
            { PoolIds = [ContentId.Parse("unknown-pool-card:missing")] });
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
        tests.EnemyLocalVariablesDriveDslTransitionsAndAppearInObservations();
        tests.BaseToughEggHatchesByResettingItsHealthAndSwitchingItsLocalPhase();
        tests.BaseOvicopterSummonsEggsThatHatchOnTheirNextEnemyTurn();
        tests.BaseMinionsSuppressFatalBonusesWhileOrdinaryKillsTriggerThem();
        tests.BasePackageRegistersVanillaStartingDeckAndDeadlyCultistValues();
        tests.StandardRunLoadsOvergrowthEncounterPoolsFromBaseContent();
        tests.BaseAnchorAndVajraApplyTheirCombatStartEffects();
        tests.BaseRedSkullTracksCombatHealingAndHpLossAcrossItsThreshold();
        tests.BaseDemonTongueHealsOnlyForFirstPlayerSideHitEachTurn();
        tests.BasePaperPhrogAddsTwentyFivePercentToPoweredDamageAgainstVulnerableEnemies();
        tests.BaseRuinedHelmetDoublesTheFirstStrengthApplicationEachCombat();
        tests.BaseFirePotionUsesUnpoweredDamageAndConsumesItsRunInventorySlot();
        tests.FlexPotionReusesTemporaryStrengthAcrossPotionsAndCards();
        tests.MigratedPotionDslUpgradesHandAndAppliesEffectsToTheirDeclaredTargets();
        tests.PotionDslValidatesUsageFlagsAndCrossPackageReferences();
        tests.PotionRewardUsesPityOddsAndRespectsFixedInventoryCapacity();
        tests.PotionCapacityChangesPreserveSlotsAndPotionBeltAddsTwoSlots();
        tests.PotionGenerationEffectUsesItsOwnRngAndFillsOpenSlotsWithoutDuplicates();
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
        tests.BeforeFlushHookCanPauseToRetainASelectedCardAndAfterFlushSeesTheResult();
        tests.WellLaidPlansAllowsSelectingUpToItsPowerAmountOrConfirmingNone();
        tests.BarricadePreventsPlayerBlockFromClearingAtTurnStart();
        tests.EntrenchAddsBlockEqualToCurrentBlockAndIsAnEventCard();
        tests.ImperviousGrantsBlockAndExhaustsItself();
        tests.WhirlwindSpendsXEnergyAndRepeatsAnAoEAttack();
        tests.BloodlettingAndOfferingLoseHpWithoutConsumingBlockOrPoweringDamage();
        tests.BaseStrikeBashAndVulnerableUseRegisteredRules();
        tests.BaseDampCultistNormalValuesMatchSourceRules();
        tests.BaseCultistsEncounterRunsBothSourceEnemySequences();
        tests.BaseFuzzyWurmEncounterUsesItsLinkedSourceActionCycle();
        tests.BaseShrinkerBeetleUsesAPlayerDamageModifierRemovedWithItsApplier();
        tests.EnemyActionPowerReferencesAreValidatedWhenBuildingContent();
        tests.EnemyTransitionsUseWeightedRngAndPreventImmediateRepeats();
        tests.BaseTurretOperatorEncounterUsesAllyTargetingAndLivingAllyConditions();
        tests.EnemyTransitionsEvaluateOrderedConditionsAgainstCombatState();
        tests.EncounterContentDslRejectsUnsupportedShapesAndDuplicateIds();
        tests.SpireCardDslCompilesTheCurrentIroncladCardSet();
        tests.CardPoolMembershipIsExplicitAndModsCanExtendExistingPools();
        tests.SpiteRepeatsAfterPlayerHpLossAndResetsOnTheNextTurn();
        tests.PyreIncreasesTheNextTurnsMaximumEnergyThroughItsPower();
        tests.CrueltyAddsItsStacksToPoweredAttacksAgainstVulnerableTargets();
        tests.SetupStrikeStacksTemporaryStrengthAndPreservesPermanentStrengthAtExpiry();
        tests.CorruptionSharesCostRulesAndSnapshotsExhaustionAcrossCardChoices();
        tests.HavocAutoPlaysFreeAndHonorsForcedExhaustionAndPowerPlacement();
        tests.CascadeReservesItsWholeBatchBeforeCardDrawsAndSupportsZeroXUpgrade();
        tests.AutomaticPlayShufflesDuringReservationAndCapturesXWithoutSpendingEnergy();
        tests.NestedAutoPlayChoiceRestoresParentXAndCompletesEachCardOnce();
        tests.OneTwoPunchReplaysEffectsAndTriggersWhilePayingAndPlacingOnce();
        tests.ReplayedCardCanSuspendForEachExecutionWithoutRepeatingConsumption();
        tests.ReplayResolvesAnUpgradeAppliedDuringItsFirstExecution();
        tests.DrumOfBattleExhaustCallbackUsesItsUpgradeAndWorksDuringAutomaticPlay();
        tests.FiendFireCapturesOriginalHandAndCompletesExhaustCallbacksBeforeHits();
        tests.CountBindingsSurviveChoicesAndRestoreOuterLexicalScope();
        tests.CountBindingsStayWithParentAcrossNestedAutomaticCardChoices();
        tests.CountBindingsRejectUnboundAndOutOfScopeReferences();
        tests.StokeUsesOwnerPoolAndOriginalCountDespiteExhaustDraws();
        tests.GeneratedCardsReserveRandomBatchAndSuspendBetweenInsertionEvents();
        tests.CombatGenerationRequiresExplicitCharacterAndEligiblePool();
        tests.FreeAttackConsumesOnlyMatchingPlaysAndPreservesXCostSpending();
        tests.ExpectAFightCountsHandAttacksAndLocksPositiveEnergyUntilTurnEnd();
        tests.TearAsunderCountsPositiveHpLossAcrossTurnsAndRepeatCountsAreCapturedOnce();
        tests.MangleSuppressesEnemyStrengthThroughItsActionThenRestoresIt();
        tests.PowerLifecycleUsesSignedDeltasAndRejectsRecursiveLinks();
        tests.PowerExpiryWaitsForItsTurnEndChoiceAndUsesRemainingStacks();
        tests.SourceBackedIroncladCardsUseSharedEffectsInCombat();
        tests.SourceBackedIroncladCardsComposeLossChoiceAndPower();
        tests.SourceBackedConditionalCardsUseTurnHistoryAndPileCounts();
        tests.ScaledAttacksAndTargetPredicatesUseLiveCombatState();
        tests.NotYetHealsAndKeepsItsCombatGenerationRestriction();
        tests.PerfectedStrikeCountsTaggedCardsAcrossAllFiveCombatPiles();
        tests.FightMeGrantsStrengthAfterItsTwoHits();
        tests.CountScaledPowerEffectsUseTheSelectedTargetsCurrentStacks();
        tests.ConflagrationRepeatsAreaDamageAcrossLivingEnemies();
        tests.RageTriggersOnlyAfterAttackCardsAndExpiresAtPlayerTurnEnd();
        tests.AfterCardPlayedPowerResumesOnceAfterCardChoice();
        tests.SpireEnemyDslCompilesTheCompleteCurrentMonsterRoster();
        tests.BaseWeakSlimeEncounterUsesRandomSlotsAndAppliesSlimedCards();
        tests.BaseFlyconidHonorsMoveCooldownsAcrossRecentActionHistory();
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
        tests.ModVariablesKeepRunAndCombatLifetimesSeparate();
        tests.StateDslDefinesScopedDefaultsAndRejectsInvalidDeclarations();
        tests.LegalActionsRejectUnaffordableCardAndInvalidHandIndex();
        tests.ChoicePausesAndResumesTheRemainingEffectSequence();
        tests.ChoiceDuringEnemyTurnResumesTheTurnScheduler();
        tests.SinglePlayerRunTraversesActsAndStopsAtVictory();
        tests.SinglePlayerRunDefeatIsTerminal();
        tests.RestSiteOffersHealingOrUpgradingOneCard();
        tests.NestedCombatReturnsToItsParentRoom();
        tests.UnknownRoomOddsExposeOrderedModHooks();
        tests.RunCombatUsesTheRunDeckAndWritesItsOutcomeBack();
        tests.CombatCardRewardsGenerateDistinctOptionsAndWriteTheChosenInstanceToTheDeck();
        tests.RunEventOptionsExposeLegalChoicesAndApplyDeclarativeRunEffects();
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
        var instanceTests = new CardInstanceDslTests();
        instanceTests.InfernalBladeInstallsFreeCostBeforeInsertionAndClearsItAfterPlaying();
        instanceTests.DistinctGenerationShufflesTheWholeFilteredPoolBeforeTaking();
        instanceTests.LocalCostsComposeAndSurviveMovesAndCostReducingUpgrade();
        instanceTests.FreeCostPersistsThroughReplayAndExhaustChoiceThenExpires();
        instanceTests.TurnCostCleanupWaitsForFlushChoiceAndCoversAllCombatPiles();
        instanceTests.PrimalForceTransformsOnlyOriginalHandAttacksInPlaceWithFreshIdentity();
        instanceTests.InstanceDslRejectsInvalidCostsAndTransformationReferences();
        instanceTests.ControlledNativeEffectsPreserveSelectedCardContext();
        var cardEvents = new CardEventDslTests();
        cardEvents.StompPaysItsPreviewedCostAndListensAcrossCombatPiles();
        cardEvents.ReplaysCountEachAttackAndStompCostsResetBeforeNextTurn();
        cardEvents.EntryUsesFinishedHistoryAndCompletesBeforeGenerationOrTransformationEvents();
        cardEvents.EntryChoicesSuspendBeforeCombatStartedAndCalculatedCostsRoundTrip();
        cardEvents.ListenerChoicesAndNestedAutomaticCardsRespectBothHistoryBoundaries();
        cardEvents.ListenerSnapshotsResolveUpgradesAndSkipCardsThatLeftCombat();
        cardEvents.FatalSelfDamageDoesNotRecordCompletionOrRunRemainingReplays();
        cardEvents.CallbackDefinitionsRejectUnboundValuesAndInvalidHistoryFilters();
        cardEvents.HistoryRemainsPlayerScopedDuringFlushAndClearsOnEnemySide();
        cardEvents.InitialEntryExtensionsHaveTheirRunOwnerAndSelectedInstance();
        var cardVariables = new CardVariableDslTests();
        cardVariables.RampageGrowthSurvivesTurnsUpgradeAndDowngrade();
        cardVariables.RampageReplaysReadGrowthFromThePreviousExecution();
        cardVariables.PlayedSelectedAndCapturedValuesSurviveChoicesAndNestedFrames();
        cardVariables.DefinitionUpgradeValuesAndSetOffsetsShareOneInstance();
        cardVariables.ClonesCopyCurrentValuesAndCostsAndThenChangeIndependently();
        cardVariables.StompCloneRetainsDiscountWithoutReinitializingAndAngerClonesCosts();
        cardVariables.CardVariableAndCloneDefinitionsRejectInvalidInputsAndRoundTrip();
        Console.WriteLine("All gameplay checks passed.");
    }
}

