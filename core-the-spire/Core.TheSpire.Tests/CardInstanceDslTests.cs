using BaseSpire;
using CoreTheSpire;

namespace Core.TheSpire.Tests;

public sealed class CardInstanceDslTests
{
    private static ContentRegistry Registry()
    {
        var registry = new ContentRegistry();
        registry.AddPackage(new BaseSpirePackage());
        return registry;
    }

    private static readonly ContentId Character = ContentId.Parse("instance-fixture:character");
    private static void RegisterPool(ContentRegistry registry, params ContentId[] cards)
    {
        var pool = ContentId.Parse("instance-fixture:pool");
        registry.Register(new CardPoolDefinition(pool, cards));
        registry.Register(new CharacterDefinition(Character, 65, 0, [], cardPools: [pool]));
    }

    public void InfernalBladeInstallsFreeCostBeforeInsertionAndClearsItAfterPlaying()
    {
        var registry = Registry();
        RegisterPool(registry, BaseSpireIds.StrikeIronclad, BaseSpireIds.Bludgeon,
            BaseSpireIds.Stoke, BaseSpireIds.NotYet);
        registry.Register(new GenerationPauseHook());
        var environment = new CombatEnvironment(registry.Build());
        foreach (var upgrade in new[] { 0, 1 })
        {
            var session = environment.Start(120, "player", 56, 65, [BaseSpireIds.InfernalBlade],
                BaseSpireIds.DampCultist.ToString(), enemyHp: 53, characterId: Character);
            session.State.Player.MutableHand[0] = new CardInstance("blade", BaseSpireIds.InfernalBlade, upgrade);
            session.State.Player.Energy = upgrade == 0 ? 1 : 0;
            Assert.True(environment.Step(session, new PlayCardAction(0, session.State.Player.Id)).NeedsChoice);
            var generated = session.State.Player.HandInstances.Single();
            Assert.Equal(BaseSpireIds.Bludgeon, generated.CardId);
            Assert.Equal(0, environment.Observe(session).HandPlayPreviews.Single().EnergyToSpend);
            environment.Step(session, new ChooseOptionAction(0));
            Assert.Equal(BaseSpireIds.InfernalBlade, session.State.Player.ExhaustPile.Single());
            Assert.Contains(environment.LegalActions(session), action => action is PlayCardAction);
            Assert.Equal(0, session.Random.Get("combat-card-generation").Counter); // A one-item distinct pool needs no shuffle draws.
            environment.Step(session, new PlayCardAction(0, session.State.Enemy.Id));
            Assert.Equal(21, session.State.Enemy.Hp);
            Assert.Equal(0, session.State.Player.Energy);
            var played = session.State.Player.DiscardPileInstances.Single();
            Assert.Equal(generated.InstanceId, played.InstanceId);
            Assert.Equal(0, played.CostModifiers.Count);
            session.State.MoveCard(CardPile.Discard, CardPile.Hand, played.InstanceId, CardPilePosition.Bottom);
            Assert.Equal(3, environment.Observe(session).HandPlayPreviews.Single().EnergyToSpend);
            Assert.False(environment.LegalActions(session).Any(action => action is PlayCardAction));
        }
    }

    private sealed class GenerationPauseHook : IRuleHook
    {
        public ContentId Id => ContentId.Parse("instance-fixture:generation-pause");
        public void OnEvent(RuleHookContext context, GameEvent gameEvent) { }
        public GameEventEffect? OnEventEffect(RuleHookContext context, GameEvent gameEvent) =>
            gameEvent.Type == "card_generated" ? new(new ChooseEffect("Inspect generated cost",
                [new ChoiceOption("Continue")]), GameEventActor.Player) : null;
    }

    public void DistinctGenerationShufflesTheWholeFilteredPoolBeforeTaking()
    {
        var registry = Registry();
        RegisterPool(registry, BaseSpireIds.Anger, BaseSpireIds.Bludgeon, BaseSpireIds.PommelStrike,
            BaseSpireIds.Stoke, BaseSpireIds.StrikeIronclad);
        var environment = new CombatEnvironment(registry.Build());
        foreach (var count in new[] { 0, 1, 9 })
        {
            var session = environment.Start(121, "player", 56, 65, [], BaseSpireIds.DampCultist.ToString(),
                characterId: Character);
            var expected = new List<ContentId> { BaseSpireIds.Anger, BaseSpireIds.Bludgeon, BaseSpireIds.PommelStrike };
            new RandomStreams(121).Get("combat-card-generation").Shuffle(expected);
            EffectExecutor.Execute(session, session.State.Player.Creature,
                new GenerateCombatCardsEffect(count, Type: CardType.Attack, Distinct: true));
            Assert.Equal(expected.Take(count), session.State.Player.HandInstances.Select(card => card.CardId));
            Assert.Equal(2, session.Random.Get("combat-card-generation").Counter);
            Assert.Equal(Math.Min(count, 3), session.State.Player.HandInstances.Select(card => card.CardId).Distinct().Count());
        }
        var emptyRegistry = Registry();
        RegisterPool(emptyRegistry, BaseSpireIds.Stoke);
        var emptyEnvironment = new CombatEnvironment(emptyRegistry.Build());
        var empty = emptyEnvironment.Start(122, "player", 56, 65, [BaseSpireIds.InfernalBlade],
            BaseSpireIds.DampCultist.ToString(), characterId: Character);
        emptyEnvironment.Step(empty, new PlayCardAction(0, empty.State.Player.Id));
        Assert.Equal(0, empty.State.Player.Hand.Count);
        Assert.Equal(0, empty.Random.Get("combat-card-generation").Counter);
        Assert.Equal(BaseSpireIds.InfernalBlade, empty.State.Player.ExhaustPile.Single());
    }

    public void LocalCostsComposeAndSurviveMovesAndCostReducingUpgrade()
    {
        var environment = new CombatEnvironment(Registry().Build());
        var modifiers = new List<CardCostModifier> {
            new(2, CardCostOperation.Set, CardCostExpiration.CombatEnd),
            new(1, CardCostOperation.Add, CardCostExpiration.CombatEnd),
            new(0, CardCostOperation.Set, CardCostExpiration.TurnEnd),
            new(1, CardCostOperation.Add, CardCostExpiration.CombatEnd),
            new(2, CardCostOperation.Set, CardCostExpiration.CombatEnd, ReduceOnly: true)
        };
        var session = environment.Start(123, "player", 56, 65, [BaseSpireIds.Corruption], BaseSpireIds.DampCultist.ToString());
        var card = new CardInstance("local-cost", BaseSpireIds.Corruption, costModifiers: modifiers);
        session.State.Player.MutableHand[0] = card;
        modifiers.Clear(); // Instance owns an immutable copy.
        Assert.Equal(5, card.CostModifiers.Count);
        Assert.Equal(1, environment.Observe(session).HandPlayPreviews.Single().EnergyToSpend);
        session.State.MoveCard(CardPile.Hand, CardPile.Discard, card.InstanceId, CardPilePosition.Top);
        Assert.True(session.State.UpgradeCard(session, card.InstanceId));
        session.State.MoveCard(CardPile.Discard, CardPile.Hand, card.InstanceId, CardPilePosition.Bottom);
        Assert.Equal(1, session.State.Player.HandInstances.Single().UpgradeLevel);
        Assert.Equal(5, session.State.Player.HandInstances.Single().CostModifiers.Count);
        session.State.ExpireCardCosts(session, CardCostExpiration.TurnEnd);
        Assert.Equal(2, environment.Observe(session).HandPlayPreviews.Single().EnergyToSpend);

        var cap = new CardInstance("cap", BaseSpireIds.Corruption, costModifiers:
            [new(5, CardCostOperation.Set, CardCostExpiration.CombatEnd),
             new(1, CardCostOperation.Add, CardCostExpiration.CombatEnd)]);
        session.State.Player.MutableHand[0] = cap;
        Assert.Equal(6, environment.Observe(session).HandPlayPreviews.Single().EnergyToSpend);
        session.State.UpgradeCard(session, cap.InstanceId);
        Assert.Equal(3, environment.Observe(session).HandPlayPreviews.Single().EnergyToSpend);

        var x = new CardInstance("x", BaseSpireIds.Whirlwind, costModifiers:
            [new(0, CardCostOperation.Set, CardCostExpiration.TurnEnd)]);
        session.State.Player.MutableHand[0] = x;
        session.State.Player.Energy = 3;
        Assert.Equal(3, environment.Observe(session).HandPlayPreviews.Single().EnergyToSpend);
        var serialized = System.Text.Json.JsonSerializer.Serialize(cap);
        var roundTrip = System.Text.Json.JsonSerializer.Deserialize<CardInstance>(serialized)!;
        Assert.Equal(cap.InstanceId, roundTrip.InstanceId);
        Assert.True(cap.CostModifiers.SequenceEqual(roundTrip.CostModifiers));
    }

    public void FreeCostPersistsThroughReplayAndExhaustChoiceThenExpires()
    {
        var registry = Registry();
        var source = DslSourceParser.Parse("""
            package instance-fixture {
              card ExhaustAttack { type = attack rarity = token cost = 2 target = enemy keywords = [exhaust]
                play = attack(1)
                onExhaust = choose(prompt="Exhaust", options=[option("Continue")]) }
              card Modifier { type = skill rarity = token cost = 0 target = self
                play = chooseCard(from=hand, prompt="Free", onSelected=
                  modifySelectedCardCost(costModifier(0, expires=[turnEnd, played]))) }
            }
            """);
        foreach (var card in SpireCombatDslCompiler.CompileCards(source)) registry.Register(card);
        var environment = new CombatEnvironment(registry.Build());
        var attackId = ContentId.Parse("instance-fixture:exhaust-attack");
        var session = environment.Start(124, "player", 56, 65,
            [ContentId.Parse("instance-fixture:modifier"), attackId], BaseSpireIds.DampCultist.ToString(), enemyHp: 53);
        var attack = session.State.Player.HandInstances[1];
        session.State.Player.Energy = 0;
        Assert.True(environment.Step(session, new PlayCardAction(0, session.State.Player.Id)).NeedsChoice);
        environment.Step(session, new ChooseCardAction(attack.InstanceId));
        session.State.ApplyPower(session, session.State.Player.Creature, BaseSpireIds.OneTwoPunchPower, 1, session.State.Player.Creature);
        var paused = environment.Step(session, new PlayCardAction(0, session.State.Enemy.Id));
        Assert.True(paused.NeedsChoice);
        Assert.Equal(51, session.State.Enemy.Hp);
        Assert.Equal(1, session.State.Player.ExhaustPileInstances.Single().CostModifiers.Count);
        Assert.Equal(0, session.State.Events.Count(e => e.Type == "card_cost_expired"));
        var resumed = environment.Step(session, new ChooseOptionAction(0));
        Assert.False(resumed.NeedsChoice);
        Assert.Equal(0, session.State.Player.ExhaustPileInstances.Single().CostModifiers.Count);
        Assert.Equal(1, session.State.Events.Count(e => e.Type == "card_cost_expired"));
        Assert.Equal(1, session.State.Events.Count(e => e.Type == "card_exhausted"));
    }

    public void TurnCostCleanupWaitsForFlushChoiceAndCoversAllCombatPiles()
    {
        var registry = Registry();
        registry.Register(new CostCleanupChoiceHook());
        var environment = new CombatEnvironment(registry.Build());
        var session = environment.Start(125, "player", 56, 65, [], BaseSpireIds.DampCultist.ToString());
        foreach (var pile in new[] { CardPile.Hand, CardPile.Draw, CardPile.Discard, CardPile.Exhaust })
        {
            var instance = new CardInstance($"cost-{pile}", BaseSpireIds.Bludgeon, costModifiers:
                [new(0, CardCostOperation.Set, CardCostExpiration.TurnEnd),
                 new(1, CardCostOperation.Set, CardCostExpiration.Played)]);
            session.State.GetPile(pile).Add(instance);
            if (pile == CardPile.Hand) session.State.TemporarilyRetainedCards.Add(instance.InstanceId);
        }
        Assert.True(environment.Step(session, new EndTurnAction()).NeedsChoice);
        Assert.Equal(0, session.State.Events.Count(e => e.Type == "card_cost_expired"));
        Assert.Equal(2, session.State.Player.HandInstances.Single().CostModifiers.Count);
        Assert.True(environment.Step(session, new ChooseOptionAction(0)).NeedsChoice);
        Assert.Equal(4, session.State.Events.Count(e => e.Type == "card_cost_expired"));
        for (var i = 0; i < 4; i++) environment.Step(session, new ChooseOptionAction(0));
        Assert.Equal(2, session.State.Turn);
        Assert.Equal(4, session.State.Events.Count(e => e.Type == "card_cost_expired"));
        foreach (var pile in Enum.GetValues<CardPile>())
            Assert.True(session.State.GetPile(pile).All(card => card.CostModifiers.Count == 1
                && card.CostModifiers[0].Expires == CardCostExpiration.Played));
    }

    private sealed class CostCleanupChoiceHook : IRuleHook
    {
        public ContentId Id => ContentId.Parse("instance-fixture:cleanup-choice");
        public void OnEvent(RuleHookContext context, GameEvent gameEvent) { }
        private static ChooseEffect Choice(string text) => new(text, [new ChoiceOption("Continue")]);
        public EffectNode? AfterFlush(RuleHookContext context, string playerId,
            IReadOnlyList<CardInstance> flushedCards, IReadOnlyList<CardInstance> retainedCards) => Choice("Flush");
        public GameEventEffect? OnEventEffect(RuleHookContext context, GameEvent gameEvent) =>
            gameEvent.Type == "card_cost_expired" ? new(Choice("Cost expired"), GameEventActor.Player) : null;
    }

    public void PrimalForceTransformsOnlyOriginalHandAttacksInPlaceWithFreshIdentity()
    {
        var registry = Registry();
        var originalDefinition = registry.Build().Cards[BaseSpireIds.PrimalForce];
        registry.Override(originalDefinition with {
            Effect = EffectDsl.Parse(EffectDsl.Serialize(originalDefinition.Effect)),
            UpgradedEffect = EffectDsl.Parse(EffectDsl.Serialize(originalDefinition.UpgradedEffect!))
        });
        var environment = new CombatEnvironment(registry.Build());
        foreach (var upgrade in new[] { 0, 1 })
        {
            var session = environment.Start(126, "player", 56, 65,
                [BaseSpireIds.PrimalForce, BaseSpireIds.Anger, BaseSpireIds.DefendIronclad, BaseSpireIds.Bludgeon],
                BaseSpireIds.DampCultist.ToString(), enemyHp: 53);
            session.State.Player.MutableHand[0] = new CardInstance("primal", BaseSpireIds.PrimalForce, upgrade);
            session.State.Player.MutableHand[1] = new CardInstance("old-attack", BaseSpireIds.Anger, 1,
                [new(0, CardCostOperation.Set, CardCostExpiration.TurnEnd)]);
            session.State.TemporarilyRetainedCards.Add("old-attack");
            var defend = session.State.Player.HandInstances[2];
            environment.Step(session, new PlayCardAction(0, session.State.Player.Id));
            var hand = session.State.Player.HandInstances;
            Assert.Equal(new[] { BaseSpireIds.GiantRock, BaseSpireIds.DefendIronclad, BaseSpireIds.GiantRock },
                hand.Select(card => card.CardId));
            Assert.Equal(defend, hand[1]);
            Assert.True(hand.Where(card => card.CardId == BaseSpireIds.GiantRock)
                .All(card => card.UpgradeLevel == upgrade && card.CostModifiers.Count == 0));
            Assert.False(session.State.TemporarilyRetainedCards.Contains("old-attack"));
            Assert.False(hand.Any(card => card.InstanceId == "old-attack"));
            Assert.Equal(hand.Count, hand.Select(card => card.InstanceId).Distinct().Count());
            Assert.Equal(2, session.State.Events.Count(e => e.Type == "card_transformed"));
            Assert.False(session.State.Events.Any(e => e.Type is "card_generated" or "card_exhausted"));
            environment.Step(session, new PlayCardAction(0, session.State.Enemy.Id));
            Assert.Equal(upgrade == 0 ? 37 : 33, session.State.Enemy.Hp);
            Assert.Equal(2, session.State.Player.Energy);
        }
    }

    public void InstanceDslRejectsInvalidCostsAndTransformationReferences()
    {
        foreach (var effect in new EffectNode[] {
            new ModifySelectedCardCostEffect(new(-1, CardCostOperation.Set, CardCostExpiration.Played)),
            new ModifySelectedCardCostEffect(new(0, CardCostOperation.Set, (CardCostExpiration)8)),
            new TransformSelectedCardEffect(ContentId.Parse("instance-fixture:missing"), CardPile.Hand),
            new TransformSelectedCardEffect(BaseSpireIds.GiantRock, CardPile.Play)
        })
        {
            var registry = Registry();
            registry.Register(new CardDefinition(ContentId.Parse("instance-fixture:invalid"), 0, CardType.Skill, effect));
            Assert.Throws<Exception>(() => registry.Build());
        }
        Assert.Throws<DslSourceException>(() => SpireCombatDslCompiler.CompileCards(DslSourceParser.Parse("""
            package instance-fixture { card Invalid { type = skill rarity = token cost = 0 target = self
              play = modifySelectedCardCost(costModifier(0, expires=[])) } }
            """)));
    }

    public void ControlledNativeEffectsPreserveSelectedCardContext()
    {
        var registry = Registry();
        var handler = new SelectedCostHandler();
        registry.Register(handler);
        var modifierCard = ContentId.Parse("instance-fixture:native-modifier");
        registry.Register(new CardDefinition(modifierCard, 0, CardType.Skill,
            new ChooseCardFromPileEffect(CardPile.Hand, null, "Modify selected cost",
                OnSelected: new CustomEffect(handler.Id, System.Text.Json.JsonSerializer.SerializeToElement(new { }))),
            Target: TargetSelector.Self));
        var environment = new CombatEnvironment(registry.Build());
        var session = environment.Start(127, "player", 56, 65,
            [modifierCard, BaseSpireIds.Bludgeon], BaseSpireIds.DampCultist.ToString());
        var selected = session.State.Player.HandInstances[1].InstanceId;
        Assert.True(environment.Step(session, new PlayCardAction(0, session.State.Player.Id)).NeedsChoice);
        environment.Step(session, new ChooseCardAction(selected));
        Assert.Equal(selected, session.State.Player.HandInstances.Single().InstanceId);
        Assert.Equal(0, environment.Observe(session).HandPlayPreviews.Single().EnergyToSpend);
    }

    private sealed class SelectedCostHandler : ICustomEffectHandler
    {
        public ContentId Id => ContentId.Parse("instance-fixture:selected-cost");
        public void Execute(EffectExecutionContext context, System.Text.Json.JsonElement payload) =>
            context.Execute(new ModifySelectedCardCostEffect(new(0, CardCostOperation.Set, CardCostExpiration.TurnEnd)));
    }
}
