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

    public void UnknownMapPointKeepsMapTypeSeparateFromResolvedRoomType()
    {
        var map = new ActMapDefinition("start", "boss",
        [
            new MapPointDefinition("start", 0, 3, MapPointType.Start, ["unknown"]),
            new MapPointDefinition("unknown", 1, 3, MapPointType.Unknown, ["boss"]),
            new MapPointDefinition("boss", 2, 3, MapPointType.Boss, [])
        ]);
        var act = new ActDefinition(ContentId.Parse("test-fixture:act"), _ => map, _ => RunRoomType.Event);
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
        tests.UnknownMapPointKeepsMapTypeSeparateFromResolvedRoomType();
        tests.RunMapGenerationReplaysFromTheSessionSeed();
        tests.BaseSpireActMapsAreDeterministicAndHonorTheSourceLayout();
        tests.AncientEntryIsAnInitialEventBeforeTheFirstRouteChoice();
        tests.BaseSpireStandardRunUsesItsActMapGenerators();
        tests.UnknownMapPointOddsAdaptAfterEachRoll();
        tests.UnknownMapOddsResetWhenTheActChanges();
        Console.WriteLine("Passed 24 gameplay checks.");
    }
}

