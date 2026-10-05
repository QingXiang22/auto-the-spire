using BaseSpire;
using CoreTheSpire;

namespace Core.TheSpire.Tests;

public sealed class CardEventDslTests
{
    private static ContentRegistry Registry(string? source = null)
    {
        var registry = new ContentRegistry();
        registry.AddPackage(new BaseSpirePackage());
        if (source is not null)
            foreach (var card in SpireCombatDslCompiler.CompileCards(DslSourceParser.Parse(source))) registry.Register(card);
        return registry;
    }

    public void StompPaysItsPreviewedCostAndListensAcrossCombatPiles()
    {
        var environment = new CombatEnvironment(Registry().Build());
        var session = environment.Start(130, "player", 56, 65,
            [BaseSpireIds.Stomp, BaseSpireIds.Anger, BaseSpireIds.DefendIronclad],
            BaseSpireIds.DampCultist.ToString(), enemyHp: 53, drawPile: [BaseSpireIds.Stomp]);
        session.State.Player.MutableDiscardPile.Add(new CardInstance("discard-stomp", BaseSpireIds.Stomp));
        session.State.Player.MutableExhaustPile.Add(new CardInstance("exhaust-stomp", BaseSpireIds.Stomp));
        environment.Step(session, new PlayCardAction(1, session.State.Enemy.Id));
        foreach (var pile in Enum.GetValues<CardPile>())
            Assert.True(session.State.GetPile(pile).Where(card => card.CardId == BaseSpireIds.Stomp)
                .All(card => card.CostModifiers.Single().Amount == -1));
        Assert.Equal(2, environment.Observe(session).HandPlayPreviews[0].EnergyToSpend);
        environment.Step(session, new PlayCardAction(1, session.State.Player.Id));
        Assert.Equal(2, environment.Observe(session).HandPlayPreviews[0].EnergyToSpend);
        environment.Step(session, new PlayCardAction(0));
        Assert.Equal(0, session.State.Player.Energy); // Self-listening occurs after the two-energy payment.
        Assert.Equal(35, session.State.Enemy.Hp);
        Assert.Equal(2, session.State.CardPlayHistory.Count(CardType.Attack, CardPlayHistoryPhase.Started));
        Assert.Equal(2, session.State.CardPlayHistory.Count(CardType.Attack, CardPlayHistoryPhase.Finished));
        Assert.Equal(1, session.State.CardPlayHistory.Count(CardType.Skill, CardPlayHistoryPhase.Finished));
        Assert.Equal(2, session.State.Player.DiscardPileInstances.First(card => card.CardId == BaseSpireIds.Stomp).CostModifiers.Count);

        var area = environment.StartEncounter(131, "player", 56, 65, [BaseSpireIds.Stomp],
            [new(BaseSpireIds.DampCultist.ToString(), 53), new(BaseSpireIds.DampCultist.ToString(), 53)]);
        area.State.UpgradeCard(area, area.State.Player.HandInstances.Single().InstanceId);
        environment.Step(area, new PlayCardAction(0));
        Assert.True(area.State.Enemies.All(enemy => enemy.Creature.Hp == 38));
        Assert.Equal(0, area.State.Player.Energy);
    }

    public void ReplaysCountEachAttackAndStompCostsResetBeforeNextTurn()
    {
        var environment = new CombatEnvironment(Registry().Build());
        var session = environment.Start(132, "player", 56, 65, [BaseSpireIds.Stomp, BaseSpireIds.Anger],
            BaseSpireIds.DampCultist.ToString(), enemyHp: 53);
        var stompId = session.State.Player.HandInstances[0].InstanceId;
        session.State.ApplyPower(session, session.State.Player.Creature, BaseSpireIds.OneTwoPunchPower, 1,
            session.State.Player.Creature);
        environment.Step(session, new PlayCardAction(1, session.State.Enemy.Id));
        Assert.Equal(1, environment.Observe(session).HandPlayPreviews.Single().EnergyToSpend);
        Assert.Equal(2, session.State.CardPlayHistory.Count(CardType.Attack, CardPlayHistoryPhase.Finished));
        environment.Step(session, new EndTurnAction());
        Assert.Equal(2, session.State.Turn);
        Assert.Equal(0, session.State.CardPlayHistory.Count(null, CardPlayHistoryPhase.Started));
        Assert.Equal(0, session.State.CardPlayHistory.Count(null, CardPlayHistoryPhase.Finished));
        Assert.Equal(0, session.State.Player.HandInstances.Single(card => card.InstanceId == stompId).CostModifiers.Count);
        Assert.Equal(3, environment.Observe(session).HandPlayPreviews.Single(preview => preview.InstanceId == stompId).EnergyToSpend);
    }

    public void EntryUsesFinishedHistoryAndCompletesBeforeGenerationOrTransformationEvents()
    {
        var registry = Registry("""
            package card-event-fixture {
              card AttackMaker { type = attack rarity = token cost = 0 target = enemy
                play = sequence(attack(1), addCards("base-spire:stomp", pile=hand, target=self)) }
            }
            """);
        var attackMaker = ContentId.Parse("card-event-fixture:attack-maker");
        registry.Register(new InsertionPauseHook());
        var pool = ContentId.Parse("card-event-fixture:pool");
        var character = ContentId.Parse("card-event-fixture:character");
        registry.Register(new CardPoolDefinition(pool, [BaseSpireIds.Stomp]));
        registry.Register(new CharacterDefinition(character, 65, 0, [], cardPools: [pool]));
        var environment = new CombatEnvironment(registry.Build());
        var session = environment.Start(133, "player", 56, 65, [attackMaker, BaseSpireIds.Anger],
            BaseSpireIds.DampCultist.ToString(), enemyHp: 53, characterId: character);
        Assert.True(environment.Step(session, new PlayCardAction(0, session.State.Enemy.Id)).NeedsChoice);
        var first = session.State.Player.HandInstances.Single(card => card.CardId == BaseSpireIds.Stomp);
        Assert.Equal(0, first.CostModifiers.Count); // The creating Attack has started but has not finished.
        Assert.Equal(1, session.State.CardPlayHistory.Count(CardType.Attack, CardPlayHistoryPhase.Started));
        Assert.Equal(0, session.State.CardPlayHistory.Count(CardType.Attack, CardPlayHistoryPhase.Finished));
        environment.Step(session, new ChooseOptionAction(0));
        EffectExecutor.Execute(session, session.State.Player.Creature, new GenerateCombatCardsEffect(1));
        Assert.True(session.State.PendingChoice is not null);
        var second = session.State.Player.HandInstances.Last();
        Assert.Equal(-1, second.CostModifiers.Single().Amount);
        environment.Step(session, new ChooseOptionAction(0));
        var anger = session.State.Player.HandInstances.First(card => card.CardId == BaseSpireIds.Anger);
        EffectExecutor.Execute(session, session.State.Player.Creature,
            new TransformSelectedCardEffect(BaseSpireIds.Stomp, CardPile.Hand), selectedCard: anger);
        Assert.True(session.State.PendingChoice is not null);
        var transformed = session.State.Player.HandInstances[0];
        Assert.Equal(BaseSpireIds.Stomp, transformed.CardId);
        Assert.Equal(-1, transformed.CostModifiers.Single().Amount);
        Assert.True(transformed.InstanceId != anger.InstanceId);
        environment.Step(session, new ChooseOptionAction(0));
        Assert.Equal(2, session.State.Events.Count(e => e.Type == "card_generated"));
        Assert.Equal(1, session.State.Events.Count(e => e.Type == "card_transformed"));

        var clone = new CardInstance("clone-stomp", BaseSpireIds.Stomp, costModifiers:
            [new(-1, CardCostOperation.Add, CardCostExpiration.TurnEnd)], isClone: true);
        session.State.Player.MutableHand.Add(clone);
        CombatCardEvents.QueueEntry(session.State, clone);
        EffectExecutor.Resume(session);
        Assert.Equal(1, session.State.Player.HandInstances.Last().CostModifiers.Count);
        session.State.UpgradeCard(session, clone.InstanceId);
        Assert.True(session.State.Player.HandInstances.Last().IsClone);
        session.State.ExpireCardCosts(session, CardCostExpiration.TurnEnd, clone.InstanceId);
        Assert.True(session.State.Player.HandInstances.Last().IsClone);
    }

    private sealed class InsertionPauseHook : IRuleHook
    {
        public ContentId Id => ContentId.Parse("card-event-fixture:insertion-pause");
        public void OnEvent(RuleHookContext context, GameEvent gameEvent) { }
        public GameEventEffect? OnEventEffect(RuleHookContext context, GameEvent gameEvent) =>
            gameEvent.Type is "card_generated" or "card_transformed"
                ? new(new ChooseEffect("Inserted", [new ChoiceOption("Continue")]), GameEventActor.Player) : null;
    }

    public void EntryChoicesSuspendBeforeCombatStartedAndCalculatedCostsRoundTrip()
    {
        var registry = Registry("""
            package card-event-fixture {
              card Entry { type = skill rarity = token cost = 5 target = self play = gainBlock(1)
                onEnterCombat = withCount("hand", count=pileCount(hand), effect=choose(prompt="Entry",
                  options=[option("Continue", effect=modifySelectedCardCost(costModifier(
                    scaled(0, 1, count=savedCount("hand")), expires=[combatEnd])))])) }
            }
            """);
        var entryId = ContentId.Parse("card-event-fixture:entry");
        var entry = registry.Build().Cards[entryId];
        registry.Override(entry with { OnEnterCombat = EffectDsl.Parse(EffectDsl.Serialize(entry.OnEnterCombat!)) });
        var environment = new CombatEnvironment(registry.Build());
        var session = environment.Start(134, "player", 56, 65, [entryId, BaseSpireIds.DefendIronclad],
            BaseSpireIds.DampCultist.ToString());
        Assert.True(session.State.PendingChoice is not null);
        Assert.False(session.State.Events.Any(e => e.Type == "combat_started"));
        environment.Step(session, new ChooseOptionAction(0));
        Assert.Equal(2, environment.Observe(session).HandPlayPreviews[0].EnergyToSpend);
        Assert.Equal(1, session.State.Events.Count(e => e.Type == "combat_started"));
        Assert.True(session.State.Events.Single(e => e.Type == "card_cost_modified").Sequence
            < session.State.Events.Single(e => e.Type == "combat_started").Sequence);
    }

    public void ListenerChoicesAndNestedAutomaticCardsRespectBothHistoryBoundaries()
    {
        var registry = Registry("""
            package card-event-fixture {
              card Monitor { type = skill rarity = token cost = 2 target = self play = gainBlock(1)
                beforeCardPlayed = cardEvent(whenCardType=attack, effect=sequence(
                  modifySelectedCardCost(costModifier(0, expires=[turnEnd])),
                  choose(prompt="Before", options=[option("Continue")]),
                  autoPlay(from=draw, count=1)))
                afterCardPlayed = cardEvent(whenCardType=attack, effect=withCount("finished",
                  count=cardPlaysThisTurn(type=attack), effect=choose(prompt="After", options=[option("Continue",
                    effect=modifyEnergy(scaled(0, 1, count=savedCount("finished"))))]))) }
            }
            """);
        var environment = new CombatEnvironment(registry.Build());
        var monitor = ContentId.Parse("card-event-fixture:monitor");
        var session = environment.Start(135, "player", 56, 65, [monitor, BaseSpireIds.Headbutt],
            BaseSpireIds.DampCultist.ToString(), enemyHp: 53, drawPile: [BaseSpireIds.DefendIronclad]);
        var recovered = new CardInstance("recover", BaseSpireIds.StrikeIronclad);
        session.State.Player.MutableDiscardPile.Add(recovered);
        Assert.True(environment.Step(session, new PlayCardAction(1, session.State.Enemy.Id)).NeedsChoice);
        Assert.Equal(0, session.State.CardPlayHistory.Count(null, CardPlayHistoryPhase.Started));
        Assert.Equal(0, environment.Observe(session).HandPlayPreviews.Single().EnergyToSpend);
        Assert.Equal(2, session.State.Player.Energy);
        Assert.True(environment.Step(session, new ChooseOptionAction(0)).NeedsChoice);
        Assert.Equal(1, session.State.CardPlayHistory.Count(CardType.Skill, CardPlayHistoryPhase.Finished));
        Assert.Equal(1, session.State.CardPlayHistory.Count(CardType.Attack, CardPlayHistoryPhase.Started));
        Assert.Equal(0, session.State.CardPlayHistory.Count(CardType.Attack, CardPlayHistoryPhase.Finished));
        Assert.Equal(44, session.State.Enemy.Hp);
        Assert.True(environment.Step(session, new ChooseCardAction(recovered.InstanceId)).NeedsChoice);
        Assert.Equal(1, session.State.CardPlayHistory.Count(CardType.Attack, CardPlayHistoryPhase.Finished));
        Assert.Equal(BaseSpireIds.Headbutt, session.State.Player.PlayPile.Single());
        environment.Step(session, new ChooseOptionAction(0));
        Assert.Equal(3, session.State.Player.Energy);
        Assert.Equal(0, session.State.CardPlayFrames.Count);
        Assert.Equal(0, session.State.PendingEffects.Count);
    }

    public void ListenerSnapshotsResolveUpgradesAndSkipCardsThatLeftCombat()
    {
        var registry = Registry("""
            package card-event-fixture {
              card First { type = skill rarity = token cost = 2 target = self play = gainBlock(1)
                beforeCardPlayed = cardEvent(whenCardType=attack, effect=chooseCard(from=hand,
                  prompt="Upgrade", filter=upgradeable, onSelected=upgradeSelectedCard())) }
              card Second { type = skill rarity = token cost = 2 target = self play = gainBlock(1)
                upgradedBeforeCardPlayed = cardEvent(whenCardType=attack, effect=modifyEnergy(1))
                upgradedAfterCardPlayed = cardEvent(whenCardType=attack, effect=modifyEnergy(2)) }
              card Automatic { type = skill rarity = token cost = 2 target = self play = gainBlock(1)
                beforeCardPlayed = cardEvent(whenCardType=attack, effect=autoPlay(from=draw, count=1)) }
              card Removed { type = power rarity = token cost = 2 target = self play = gainBlock(2)
                beforeCardPlayed = cardEvent(whenCardType=attack, effect=modifyEnergy(99)) }
            }
            """);
        var environment = new CombatEnvironment(registry.Build());
        var second = ContentId.Parse("card-event-fixture:second");
        var session = environment.Start(136, "player", 56, 65,
            [ContentId.Parse("card-event-fixture:first"), second, BaseSpireIds.Anger], BaseSpireIds.DampCultist.ToString());
        Assert.True(session.State.IsUpgradeable(session.State.Player.HandInstances[1]));
        var secondInstance = session.State.Player.HandInstances[1].InstanceId;
        Assert.True(environment.Step(session, new PlayCardAction(2, session.State.Enemy.Id)).NeedsChoice);
        environment.Step(session, new ChooseCardAction(secondInstance));
        Assert.Equal(6, session.State.Player.Energy);
        Assert.Equal(1, session.State.Player.HandInstances.Single(card => card.CardId == second).UpgradeLevel);
        var removed = environment.Start(137, "player", 56, 65,
            [ContentId.Parse("card-event-fixture:automatic"), BaseSpireIds.Anger], BaseSpireIds.DampCultist.ToString(),
            drawPile: [ContentId.Parse("card-event-fixture:removed")]);
        environment.Step(removed, new PlayCardAction(1, removed.State.Enemy.Id));
        Assert.Equal(3, removed.State.Player.Energy);
        Assert.Equal(2, removed.State.Player.Creature.Block);
        Assert.Equal(1, removed.State.CardPlayHistory.Count(CardType.Power, CardPlayHistoryPhase.Finished));
    }

    public void FatalSelfDamageDoesNotRecordCompletionOrRunRemainingReplays()
    {
        var registry = Registry("""
            package card-event-fixture {
              card Fatal { type = attack rarity = token cost = 0 target = self play = loseHp(65) }
              card Monitor { type = skill rarity = token cost = 0 target = self play = gainBlock(1)
                afterCardPlayed = cardEvent(whenCardType=attack, effect=modifyEnergy(99)) }
            }
            """);
        var environment = new CombatEnvironment(registry.Build());
        var session = environment.Start(138, "player", 56, 65,
            [ContentId.Parse("card-event-fixture:fatal"), ContentId.Parse("card-event-fixture:monitor")],
            BaseSpireIds.DampCultist.ToString());
        session.State.ApplyPower(session, session.State.Player.Creature, BaseSpireIds.OneTwoPunchPower, 1,
            session.State.Player.Creature);
        var result = environment.Step(session, new PlayCardAction(0, session.State.Player.Id));
        Assert.True(result.Observation.IsTerminal);
        Assert.Equal(1, session.State.CardPlayHistory.Count(CardType.Attack, CardPlayHistoryPhase.Started));
        Assert.Equal(0, session.State.CardPlayHistory.Count(CardType.Attack, CardPlayHistoryPhase.Finished));
        Assert.Equal(3, session.State.Player.Energy);
        Assert.Equal(1, session.State.Events.Count(e => e.Type == "play"));
    }

    public void CallbackDefinitionsRejectUnboundValuesAndInvalidHistoryFilters()
    {
        foreach (var invalid in new CardDefinition[] {
            new(ContentId.Parse("card-event-fixture:invalid"), 0, CardType.Skill, new GainBlockEffect(1)) {
                BeforeCardPlayed = new(new ModifyEnergyEffect(1), (CardType)99) },
            new(ContentId.Parse("card-event-fixture:invalid"), 0, CardType.Skill, new GainBlockEffect(1)) {
                OnEnterCombat = new ModifySelectedCardCostEffect(new(0, CardCostOperation.Add, CardCostExpiration.TurnEnd),
                    new ScaledCombatAmount(0, -1, new SavedCountSource("missing"))) },
            new(ContentId.Parse("card-event-fixture:invalid"), 0, CardType.Skill, new GainBlockEffect(1)) {
                AfterCardPlayed = new(new RepeatForCountEffect(0, 1,
                    new CardPlaysThisTurnCountSource(Phase: (CardPlayHistoryPhase)99), new GainBlockEffect(1))) }
        })
        {
            var registry = Registry();
            registry.Register(invalid);
            Assert.Throws<InvalidOperationException>(() => registry.Build());
        }
    }

    public void HistoryRemainsPlayerScopedDuringFlushAndClearsOnEnemySide()
    {
        var registry = Registry();
        registry.Register(new HistoryBoundaryHook());
        var environment = new CombatEnvironment(registry.Build());
        var session = environment.Start(139, "player", 56, 65, [BaseSpireIds.Anger, BaseSpireIds.Anger],
            BaseSpireIds.DampCultist.ToString(), enemyHp: 53);
        environment.Step(session, new PlayCardAction(0, session.State.Enemy.Id));
        environment.Step(session, new PlayCardAction(0, session.State.Enemy.Id));
        Assert.True(environment.Step(session, new EndTurnAction()).NeedsChoice);
        Assert.Equal(2, session.State.CardPlayHistory.Count(CardType.Attack, CardPlayHistoryPhase.Finished));
        Assert.True(environment.Step(session, new ChooseOptionAction(0)).NeedsChoice);
        Assert.Equal(5, session.State.Player.Energy); // Flush continuation sees two; enemy-side effect sees zero.
        Assert.Equal(0, session.State.CardPlayHistory.Count(null, CardPlayHistoryPhase.Finished));
        environment.Step(session, new ChooseOptionAction(0));
        Assert.Equal(2, session.State.Turn);
        Assert.Equal(3, session.State.Player.Energy);
    }

    private sealed class HistoryBoundaryHook : IRuleHook
    {
        public ContentId Id => ContentId.Parse("card-event-fixture:history-boundary");
        public void OnEvent(RuleHookContext context, GameEvent gameEvent) { }
        private static ModifyEnergyEffect CountEnergy() => new(0,
            new ScaledCombatAmount(0, 1, new CardPlaysThisTurnCountSource(CardType.Attack)));
        public EffectNode? AfterFlush(RuleHookContext context, string playerId,
            IReadOnlyList<CardInstance> flushedCards, IReadOnlyList<CardInstance> retainedCards) =>
            new ChooseEffect("Flush", [new ChoiceOption("Continue", CountEnergy())]);
        public GameEventEffect? OnEventEffect(RuleHookContext context, GameEvent gameEvent) =>
            gameEvent.Type == "enemy_action" ? new(new SequenceEffect([CountEnergy(),
                new ChooseEffect("Enemy", [new ChoiceOption("Continue")])]), GameEventActor.Player) : null;
    }

    public void InitialEntryExtensionsHaveTheirRunOwnerAndSelectedInstance()
    {
        var registry = Registry();
        var handler = new RunEntryHandler();
        registry.Register(handler);
        var cardId = ContentId.Parse("card-event-fixture:run-entry");
        registry.Register(new CardDefinition(cardId, 1, CardType.Skill, new GainBlockEffect(1), Target: TargetSelector.Self)
        {
            OnEnterCombat = new CustomEffect(handler.Id, System.Text.Json.JsonSerializer.SerializeToElement(new { }))
        });
        var map = new ActMapDefinition("start", "boss", [
            new MapPointDefinition("start", 0, 3, MapPointType.Start, ["monster"]),
            new MapPointDefinition("monster", 1, 3, MapPointType.Monster, ["boss"]),
            new MapPointDefinition("boss", 2, 3, MapPointType.Boss, [])
        ]);
        var character = ContentId.Parse("card-event-fixture:character");
        var runId = ContentId.Parse("card-event-fixture:run");
        registry.Register(new CharacterDefinition(character, 65, 0, Enumerable.Repeat(cardId, 5)));
        registry.Register(new RunDefinition(runId, [new ActDefinition(ContentId.Parse("card-event-fixture:act"), _ => map)]));
        var definition = registry.Build();
        var runEnvironment = new RunEnvironment(definition, runId, character);
        var run = runEnvironment.Start(140);
        runEnvironment.Step(run, new ChooseMapPointAction("monster"));
        var environment = new CombatEnvironment(definition);
        var combat = environment.StartForRun(run, BaseSpireIds.DampCultist.ToString());
        Assert.Equal(5, handler.CallsWithRunOwner);
        Assert.True(environment.Observe(combat).HandPlayPreviews.All(preview => preview.EnergyToSpend == 0));
        Assert.True(run.State.Deck.All(card => card.CostModifiers.Count == 0));
        Assert.True(ReferenceEquals(run.State.ActiveCombat, combat));
    }

    private sealed class RunEntryHandler : ICustomEffectHandler
    {
        public ContentId Id => ContentId.Parse("card-event-fixture:run-entry-handler");
        public int CallsWithRunOwner { get; private set; }
        public void Execute(EffectExecutionContext context, System.Text.Json.JsonElement payload)
        {
            if (!context.IsRunOwned) throw new InvalidOperationException("Initial card entries require their owning run.");
            CallsWithRunOwner++;
            context.Execute(new ModifySelectedCardCostEffect(new(0, CardCostOperation.Set, CardCostExpiration.CombatEnd)));
        }
    }
}
