using System.Text.Json;
using BaseSpire;
using CoreTheSpire;

namespace Core.TheSpire.Tests;

public sealed class CardVariableDslTests
{
    private static readonly ContentId ExtraDamage = ContentId.Parse("base-spire:extra-damage");
    private static readonly ContentId Counter = ContentId.Parse("variable-fixture:counter");

    private static ContentRegistry Registry(string? source = null)
    {
        var registry = new ContentRegistry();
        registry.AddPackage(new BaseSpirePackage());
        if (source is not null)
            foreach (var card in SpireCombatDslCompiler.CompileCards(DslSourceParser.Parse(source))) registry.Register(card);
        return registry;
    }

    private static int Value(GameSession session, string instanceId, ContentId variable)
    {
        var card = session.State.FindCombatCard(instanceId)!;
        return session.State.Definition.Cards[card.CardId].ResolveVariable(card, variable);
    }

    private static int LocalCost(GameState state, CardInstance card)
    {
        var cost = state.Definition.Cards[card.CardId].ResolveCost(card);
        foreach (var modifier in card.CostModifiers) cost = modifier.Apply(cost);
        return Math.Max(0, cost);
    }

    public void RampageGrowthSurvivesTurnsUpgradeAndDowngrade()
    {
        var environment = new CombatEnvironment(Registry().Build());
        var session = environment.Start(150, "player", 56, 65, [BaseSpireIds.Rampage],
            BaseSpireIds.DampCultist.ToString(), enemyHp: 53);
        var id = session.State.Player.HandInstances.Single().InstanceId;
        environment.Step(session, new PlayCardAction(0, session.State.Enemy.Id));
        Assert.Equal(44, session.State.Enemy.Hp);
        Assert.Equal(5, Value(session, id, ExtraDamage));
        var snapshot = environment.Observe(session).DiscardPileInstances.Single();
        environment.Step(session, new EndTurnAction());
        Assert.Equal(5, Value(session, id, ExtraDamage));
        environment.Step(session, new PlayCardAction(0, session.State.Enemy.Id));
        Assert.Equal(30, session.State.Enemy.Hp);
        Assert.Equal(10, Value(session, id, ExtraDamage));
        Assert.Equal(5, snapshot.VariableOffsets.Single().Offset); // Earlier observations stay immutable.
        session.State.MoveCard(CardPile.Discard, CardPile.Hand, id, CardPilePosition.Bottom);
        session.State.UpgradeCard(session, id);
        environment.Step(session, new PlayCardAction(0, session.State.Enemy.Id));
        Assert.Equal(11, session.State.Enemy.Hp);
        Assert.Equal(19, Value(session, id, ExtraDamage));
        EffectExecutor.Execute(session, session.State.Player.Creature, new DowngradeSelectedCardEffect(),
            selectedCard: session.State.FindCombatCard(id));
        Assert.Equal(0, session.State.FindCombatCard(id)!.UpgradeLevel);
        Assert.Equal(19, Value(session, id, ExtraDamage));
        Assert.Equal(0, session.State.Definition.Cards[BaseSpireIds.Rampage].ResolveVariable(
            new CardInstance("fresh-rampage", BaseSpireIds.Rampage), ExtraDamage));
    }

    public void RampageReplaysReadGrowthFromThePreviousExecution()
    {
        foreach (var upgrade in new[] { 0, 1 })
        {
            var environment = new CombatEnvironment(Registry().Build());
            var session = environment.Start(151, "player", 56, 65, [BaseSpireIds.OneTwoPunch, BaseSpireIds.Rampage],
                BaseSpireIds.DampCultist.ToString(), enemyHp: 53);
            var original = session.State.Player.HandInstances[1].WithUpgradeLevel(upgrade);
            session.State.Player.MutableHand[1] = original;
            environment.Step(session, new PlayCardAction(0, session.State.Player.Id));
            environment.Step(session, new PlayCardAction(0, session.State.Enemy.Id));
            Assert.Equal(upgrade == 0 ? 30 : 26, session.State.Enemy.Hp);
            Assert.Equal(upgrade == 0 ? 10 : 18, Value(session, original.InstanceId, ExtraDamage));
            Assert.Equal(1, session.State.Player.DiscardPileInstances.Count(card => card.CardId == BaseSpireIds.Rampage));
        }
    }

    public void PlayedSelectedAndCapturedValuesSurviveChoicesAndNestedFrames()
    {
        var registry = Registry("""
            package variable-fixture {
              card Parent { type=skill rarity=token cost=0 target=self
                variables=[variable(Counter, 4)]
                play=sequence(
                  chooseCard(from=hand, prompt="Select", onSelected=sequence(
                    modifyCardVariable(Counter, scaled(0, 1, count=cardVariable(Counter, card=selected))),
                    modifyCardVariable(Counter, 3, card=selected),
                    autoPlay(from=draw, count=1),
                    withCount("earlier", count=cardVariable(Counter, card=selected), effect=
                      choose(prompt="Wait", options=[option("Continue", sequence(
                        modifyEnergy(scaled(0, 1, count=savedCount("earlier"))),
                        modifyEnergy(scaled(0, 1, count=cardVariable(Counter, card=selected)))
                      ))]))
                  )),
                  modifyEnergy(scaled(0, 1, count=cardVariable(Counter)))
                ) }
              card Selected { type=skill rarity=token cost=0 target=self
                variables=[variable(Counter, 10)] play=sequence() }
              card Child { type=skill rarity=token cost=0 target=self
                variables=[variable(Counter, 100)] play=modifyCardVariable(Counter, 2) }
            }
            """);
        var environment = new CombatEnvironment(registry.Build());
        var session = environment.Start(152, "player", 56, 65,
            [ContentId.Parse("variable-fixture:parent"), ContentId.Parse("variable-fixture:selected")],
            BaseSpireIds.DampCultist.ToString(), drawPile: [ContentId.Parse("variable-fixture:child")]);
        var parent = session.State.Player.HandInstances[0];
        var selected = session.State.Player.HandInstances[1];
        var child = session.State.Player.DrawPileInstances.Single();
        Assert.True(environment.Step(session, new PlayCardAction(0, session.State.Player.Id)).NeedsChoice);
        Assert.True(environment.Step(session, new ChooseCardAction(selected.InstanceId)).NeedsChoice);
        Assert.Equal(14, Value(session, parent.InstanceId, Counter));
        Assert.Equal(13, Value(session, selected.InstanceId, Counter));
        Assert.Equal(102, Value(session, child.InstanceId, Counter));
        EffectExecutor.Execute(session, session.State.Player.Creature,
            new ModifyCardVariableEffect(Counter, 10, CardContext.Selected), selectedCard: selected);
        // Pending choice prevents execution until its answer; this command then runs before the continuation.
        environment.Step(session, new ChooseOptionAction(0));
        Assert.Equal(43, session.State.Player.Energy); // 3 + captured 13 + pre-update 13 + parent 14.
        Assert.Equal(23, Value(session, selected.InstanceId, Counter));
        Assert.Equal(0, session.State.CardPlayFrames.Count);
    }

    public void DefinitionUpgradeValuesAndSetOffsetsShareOneInstance()
    {
        var registry = Registry("""
            package variable-fixture {
              card Numeric { type=attack rarity=token cost=1 target=enemy
                variables=[variable(Counter, 5, upgraded=8)]
                play=scaledAttack(0, 1, count=cardVariable(Counter)) }
            }
            """);
        var environment = new CombatEnvironment(registry.Build());
        var session = environment.Start(153, "player", 56, 65, [ContentId.Parse("variable-fixture:numeric")],
            BaseSpireIds.DampCultist.ToString(), enemyHp: 53);
        var original = session.State.Player.HandInstances.Single();
        EffectExecutor.Execute(session, session.State.Player.Creature,
            new ModifyCardVariableEffect(Counter, 9, CardContext.Selected, CardVariableOperation.Set), selectedCard: original);
        Assert.Equal(9, Value(session, original.InstanceId, Counter));
        Assert.True(session.State.IsUpgradeable(session.State.FindCombatCard(original.InstanceId)!));
        session.State.UpgradeCard(session, original.InstanceId);
        Assert.Equal(12, Value(session, original.InstanceId, Counter));
        environment.Step(session, new PlayCardAction(0, session.State.Enemy.Id));
        Assert.Equal(41, session.State.Enemy.Hp);
        EffectExecutor.Execute(session, session.State.Player.Creature, new DowngradeSelectedCardEffect(), selectedCard: original);
        Assert.Equal(5, Value(session, original.InstanceId, Counter));
        Assert.Equal(0, session.State.FindCombatCard(original.InstanceId)!.VariableOffsets.Count);
    }

    public void ClonesCopyCurrentValuesAndCostsAndThenChangeIndependently()
    {
        var environment = new CombatEnvironment(Registry().Build());
        var session = environment.Start(154, "player", 56, 65, [BaseSpireIds.Rampage],
            BaseSpireIds.DampCultist.ToString(), enemyHp: 53);
        var original = session.State.Player.HandInstances.Single();
        session.State.UpgradeCard(session, original.InstanceId);
        EffectExecutor.Execute(session, session.State.Player.Creature,
            new ModifyCardVariableEffect(ExtraDamage, 12, CardContext.Selected), selectedCard: original);
        session.State.ModifyCardCost(session, original.InstanceId,
            new(0, CardCostOperation.Set, CardCostExpiration.TurnEnd));
        EffectExecutor.Execute(session, session.State.Player.Creature, new CloneCardEffect(CardContext.Selected, Count: 2),
            selectedCard: original);
        var copies = session.State.Player.HandInstances.Where(card => card.IsClone).ToArray();
        Assert.Equal(2, copies.Length);
        Assert.Equal(3, session.State.Player.HandInstances.Select(card => card.InstanceId).Distinct().Count());
        foreach (var copy in copies)
        {
            Assert.Equal(1, copy.UpgradeLevel);
            Assert.Equal(12, Value(session, copy.InstanceId, ExtraDamage));
            Assert.Equal(0, LocalCost(session.State, copy));
        }
        environment.Step(session, new PlayCardAction(1, session.State.Enemy.Id));
        Assert.Equal(32, session.State.Enemy.Hp);
        Assert.Equal(21, Value(session, copies[0].InstanceId, ExtraDamage));
        Assert.Equal(12, Value(session, copies[1].InstanceId, ExtraDamage));
        Assert.Equal(12, Value(session, original.InstanceId, ExtraDamage));
        Assert.Equal(0, session.Random.Get("combat-card-generation").Counter);
        Assert.Equal(0, session.Random.Get("card-selection").Counter);
        Assert.True(session.State.Definition.Cards[BaseSpireIds.Rampage].ResolveVariable(original, ExtraDamage) == 0);
        var serialized = JsonSerializer.Serialize(session.State.FindCombatCard(copies[0].InstanceId));
        var restored = JsonSerializer.Deserialize<CardInstance>(serialized)!;
        Assert.True(restored.IsClone);
        Assert.Equal(21, session.State.Definition.Cards[restored.CardId].ResolveVariable(restored, ExtraDamage));
        Assert.True(restored.CostModifiers.SequenceEqual(copies[0].CostModifiers));
    }

    public void StompCloneRetainsDiscountWithoutReinitializingAndAngerClonesCosts()
    {
        var environment = new CombatEnvironment(Registry().Build());
        var session = environment.Start(155, "player", 56, 65, [BaseSpireIds.Anger, BaseSpireIds.Stomp],
            BaseSpireIds.DampCultist.ToString(), enemyHp: 53);
        var anger = session.State.Player.HandInstances[0].WithUpgradeLevel(1);
        session.State.Player.MutableHand[0] = anger;
        session.State.ModifyCardCost(session, anger.InstanceId,
            new(1, CardCostOperation.Add, CardCostExpiration.CombatEnd));
        environment.Step(session, new PlayCardAction(0, session.State.Enemy.Id));
        var angerCopy = session.State.Player.DiscardPileInstances.Single(card => card.IsClone);
        Assert.Equal(1, angerCopy.UpgradeLevel);
        Assert.Equal(1, angerCopy.CostModifiers.Single().Amount);
        var stomp = session.State.Player.HandInstances.Single();
        EffectExecutor.Execute(session, session.State.Player.Creature, new CloneCardEffect(CardContext.Selected), selectedCard: stomp);
        Assert.True(session.State.Player.HandInstances.All(card =>
            LocalCost(session.State, card) == 1));
        Assert.Equal(1, session.State.Player.HandInstances.Single(card => card.IsClone).CostModifiers.Count);
    }

    public void CardVariableAndCloneDefinitionsRejectInvalidInputsAndRoundTrip()
    {
        foreach (var effect in new EffectNode[] {
            new ModifyCardVariableEffect(ContentId.Parse("invalid:missing"), 1),
            new ModifyCardVariableEffect(ExtraDamage, 1, (CardContext)99),
            new ModifyCardVariableEffect(ExtraDamage, 1, Operation: (CardVariableOperation)99),
            new CloneCardEffect((CardContext)99), new CloneCardEffect(Pile: CardPile.Play), new CloneCardEffect(Count: -1),
            new ModifyEnergyEffect(0, new(0, 1, new CardVariableCountSource(ContentId.Parse("invalid:missing"))))
        })
        {
            var registry = Registry();
            registry.Register(new CardDefinition(ContentId.Parse("invalid:effect"), 0, CardType.Skill, effect));
            Assert.Throws<InvalidOperationException>(() => registry.Build());
        }
        var duplicate = Registry();
        duplicate.Register(new CardDefinition(ContentId.Parse("invalid:duplicate"), 0, CardType.Skill, new GainBlockEffect(1)) {
            Variables = [new(ExtraDamage, 0), new(ExtraDamage, 1)] });
        Assert.Throws<InvalidOperationException>(() => duplicate.Build());
        var expression = new ModifyCardVariableEffect(ExtraDamage, 0, CardContext.Selected, CardVariableOperation.Set,
            new(3, 2, new CardVariableCountSource(ExtraDamage)));
        var serialized = JsonSerializer.Serialize<EffectNode>(expression);
        Assert.Equal(expression, JsonSerializer.Deserialize<EffectNode>(serialized));
        Assert.Equal(new CloneCardEffect(CardContext.Selected, CardPile.Exhaust, 2),
            JsonSerializer.Deserialize<EffectNode>(JsonSerializer.Serialize<EffectNode>(new CloneCardEffect(CardContext.Selected, CardPile.Exhaust, 2))));
    }
}
