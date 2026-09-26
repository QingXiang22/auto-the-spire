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
        Assert.Equal(BaseSpireIds.BurningBlood, started.Relics.Single());
        Assert.Equal(2, environment.LegalActions(session).Count);
        Assert.Throws<InvalidOperationException>(() => environment.Step(session, new ChooseMapPointAction("boss")));

        environment.Step(session, new ChooseMapPointAction("monster"));
        Assert.Equal(RunPhase.ResolveRoom, environment.Observe(session).Phase);
        Assert.Equal(0, environment.LegalActions(session).Count);
        var afterCombat = new RunRoomResult(RunOutcome.RoomCleared, 73, 80, 120,
            started.Deck, started.Relics, [ContentId.Parse("base-spire:fire-potion")]);
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
            new EnemyDefinition(enemyId, 1, 1,
            [
                new EnemyActionDefinition(ContentId.Parse("test-fixture:idle"),
                    new AttackEffect(100), IntentKind.Attack, 100)
            ]), deck.Select(id => new CardDefinition(id, 0, CardType.Attack, new AttackEffect(1))),
            new EncounterDefinition(encounterId, RunRoomType.Monster, [enemyId]));
        var registry = new ContentRegistry();
        registry.AddPackage(new BaseSpirePackage());
        registry.AddPackage(package);
        var definition = registry.Build();
        var runEnvironment = new RunEnvironment(definition, runId, characterId);
        var combatEnvironment = new CombatEnvironment(definition);
        var runSession = runEnvironment.Start(183);
        runEnvironment.Step(runSession, new ChooseMapPointAction("event"));
        runEnvironment.PushSubRoom(runSession, RunRoomType.Monster);

        var combatSession = combatEnvironment.StartEncounterForRun(runSession, encounterId);
        Assert.Equal(5, combatEnvironment.Observe(combatSession).Hand.Count);
        Assert.Equal(5, combatEnvironment.Observe(combatSession).Hand.Distinct().Count());
        Assert.True(combatEnvironment.Observe(combatSession).Hand.All(deck.Contains));
        Assert.Throws<InvalidOperationException>(() => combatEnvironment.StartEncounterForRun(runSession, encounterId));
        Assert.Throws<InvalidOperationException>(() => runEnvironment.CompleteRoom(runSession,
            RunResult(RunOutcome.RoomCleared, runEnvironment.Observe(runSession))));
        Assert.Throws<InvalidOperationException>(() => runEnvironment.CompleteCombat(runSession, combatSession));

        var replayRun = runEnvironment.Start(183);
        runEnvironment.Step(replayRun, new ChooseMapPointAction("event"));
        runEnvironment.PushSubRoom(replayRun, RunRoomType.Monster);
        var replayCombat = combatEnvironment.StartEncounterForRun(replayRun, encounterId);
        Assert.True(combatEnvironment.Observe(combatSession).Hand.SequenceEqual(
            combatEnvironment.Observe(replayCombat).Hand));

        combatEnvironment.Step(combatSession, new PlayCardAction(0, enemyId.ToString()));
        Assert.True(combatEnvironment.Observe(combatSession).IsTerminal);
        var completed = runEnvironment.CompleteCombat(runSession, combatSession);
        Assert.Equal(RunStatus.InProgress, completed.Status);
        Assert.Equal(RunPhase.ResolveRoom, completed.Phase);
        Assert.Equal(RunRoomType.Event, completed.CurrentRoom!.Value);
        Assert.True(completed.VisitedMapPoints[^1].RoomTypes.SequenceEqual(
            [RunRoomType.Event, RunRoomType.Monster]));
        Assert.Equal(60, completed.Hp);
        Assert.True(deck.SequenceEqual(completed.Deck));
        Assert.Throws<InvalidOperationException>(() => runEnvironment.CompleteCombat(runSession, combatSession));
        var returnedToRoute = runEnvironment.CompleteRoom(runSession,
            RunResult(RunOutcome.RoomCleared, completed));
        Assert.Equal(RunPhase.ChooseMapPoint, returnedToRoute.Phase);

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
        var runId = ContentId.Parse("pool-test:run");
        var characterId = ContentId.Parse("pool-test:character");
        var pool = new ActEncounterPoolDefinition([weak], [regular, tagged], [elite], [boss],
            normalRoomCount: 7, weakNormalRoomCount: 2, eliteQueueCount: 3);
        var run = new RunDefinition(runId,
            [new ActDefinition(ContentId.Parse("pool-test:act"), _ => map, encounterPool: pool)]);
        var package = new EncounterPoolPackage(run, new CharacterDefinition(characterId, 60, 50, []), enemyId,
        [
            new EncounterDefinition(weak, RunRoomType.Monster, [enemyId], isWeak: true),
            new EncounterDefinition(regular, RunRoomType.Monster, [enemyId]),
            new EncounterDefinition(tagged, RunRoomType.Monster, [enemyId], tags: [ContentId.Parse("pool-test:group")]),
            new EncounterDefinition(elite, RunRoomType.Elite, [enemyId]),
            new EncounterDefinition(boss, RunRoomType.Boss, [enemyId])
        ]);
        var registry = new ContentRegistry();
        registry.AddPackage(package);
        var definition = registry.Build();
        var environment = new RunEnvironment(definition, runId, characterId);

        var combatRun = environment.Start(912);
        environment.Step(combatRun, new ChooseMapPointAction("monster-1"));
        var combatEnvironment = new CombatEnvironment(definition);
        Assert.Throws<InvalidOperationException>(() => combatEnvironment.StartEncounterForRun(combatRun, regular));
        var combat = combatEnvironment.StartCurrentEncounterForRun(combatRun);
        Assert.Equal(1, combatEnvironment.Observe(combat).Enemies.Count);

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
            state.Deck, state.Relics, state.Potions);

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
        EnemyDefinition enemy, IEnumerable<CardDefinition> cards, EncounterDefinition encounter) : IRulePackage
    {
        public PackageInfo Info { get; } = new("run-combat-fixture", "1.0.0");

        public void Register(ContentRegistry registry)
        {
            registry.Register(run);
            registry.Register(character);
            registry.Register(enemy);
            foreach (var card in cards) registry.Register(card);
            registry.Register(encounter);
        }
    }

    private sealed class EncounterPoolPackage(RunDefinition run, CharacterDefinition character,
        ContentId enemyId, IEnumerable<EncounterDefinition> encounters) : IRulePackage
    {
        public PackageInfo Info { get; } = new("encounter-pool-fixture", "1.0.0");

        public void Register(ContentRegistry registry)
        {
            registry.Register(run);
            registry.Register(character);
            registry.Register(new EnemyDefinition(enemyId, 20, 20,
                [new EnemyActionDefinition(ContentId.Parse("pool-test:idle"), new GainBlockEffect(0), IntentKind.None)]));
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

    private sealed class MultiEnemyPackage(CardDefinition card, params EnemyDefinition[] enemies) : IRulePackage
    {
        public PackageInfo Info { get; } = new("multi-enemy-fixture", "1.0.0");

        public void Register(ContentRegistry registry)
        {
            registry.Register(card);
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
        tests.BaseStrikeBashAndVulnerableUseRegisteredRules();
        tests.BaseDampCultistNormalValuesMatchSourceRules();
        tests.BaseCultistsEncounterRunsBothSourceEnemySequences();
        tests.BaseFuzzyWurmEncounterUsesItsLinkedSourceActionCycle();
        tests.BaseShrinkerBeetleUsesAPlayerDamageModifierRemovedWithItsApplier();
        tests.EnemyActionPowerReferencesAreValidatedWhenBuildingContent();
        tests.BaseSoloNibbitFollowsItsAttackBlockAndStrengthCycle();
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
        Console.WriteLine("Passed 39 gameplay checks.");
    }
}

