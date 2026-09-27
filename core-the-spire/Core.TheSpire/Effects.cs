namespace CoreTheSpire;

[System.Text.Json.Serialization.JsonPolymorphic(TypeDiscriminatorPropertyName = "op")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(SequenceEffect), "sequence")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(AttackEffect), "attack")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(ScaledAttackEffect), "scaledAttack")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(RandomAttackEffect), "randomAttack")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(AttackUsingBlockEffect), "attackUsingBlock")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(LoseHpEffect), "loseHp")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(HealEffect), "heal")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(GainBlockEffect), "gainBlock")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(GainBlockFromCurrentBlockEffect), "gainBlockFromCurrentBlock")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(ApplyPowerEffect), "applyPower")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(ApplyScaledPowerEffect), "applyScaledPower")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(DrawCardsEffect), "drawCards")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(MoveCardEffect), "moveCard")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(ModifyEnergyEffect), "modifyEnergy")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(GeneratePotionEffect), "generatePotion")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(ChooseEffect), "choose")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(IfEffect), "if")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(RepeatEffect), "repeat")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(RepeatForXCostEffect), "repeatForXCost")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(AddCardsToPileEffect), "addCardsToPile")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(ChooseCardFromPileEffect), "chooseCardFromPile")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(ChooseCardsFromPileEffect), "chooseCardsFromPile")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(RandomlyMoveCardFromPileEffect), "randomlyMoveCardFromPile")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(ForEachCardInPileEffect), "forEachCardInPile")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(MoveSelectedCardToPileEffect), "moveSelectedCardToPile")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(UpgradeSelectedCardEffect), "upgradeSelectedCard")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(UpgradeCardsInPileEffect), "upgradeCardsInPile")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(DecrementPowerEffect), "decrementPower")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(RemovePowerEffect), "removePower")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(RetainSelectedCardEffect), "retainSelectedCard")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(CustomEffect), "custom")]
public abstract record EffectNode;
public sealed record SequenceEffect : EffectNode
{
    [System.Text.Json.Serialization.JsonConstructor]
    public SequenceEffect(IReadOnlyList<EffectNode> effects) => Effects = Array.AsReadOnly(effects.ToArray());
    public IReadOnlyList<EffectNode> Effects { get; }
}
public sealed record AttackEffect(int Amount, TargetSelector Target = TargetSelector.FirstEnemy,
    DamageProperties Properties = DamageProperties.Move) : EffectNode;
public sealed record ScaledAttackEffect(int BaseAmount, int AmountPerCount, CombatCountSource Count,
    TargetSelector Target = TargetSelector.SelectedTarget) : EffectNode;
[System.Text.Json.Serialization.JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(PileCardCountSource), "pile")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(TargetPowerCountSource), "targetPower")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(TaggedCardCountSource), "taggedCards")]
public abstract record CombatCountSource;
public sealed record PileCardCountSource(CardPile Pile) : CombatCountSource;
public sealed record TargetPowerCountSource(ContentId Power) : CombatCountSource;
public sealed record TaggedCardCountSource(ContentId Tag) : CombatCountSource;
public sealed record RandomAttackEffect(int Amount) : EffectNode;
public sealed record AttackUsingBlockEffect(TargetSelector Target = TargetSelector.SelectedTarget) : EffectNode;
public sealed record LoseHpEffect(int Amount, TargetSelector Target = TargetSelector.Self) : EffectNode;
public sealed record HealEffect(int Amount, TargetSelector Target = TargetSelector.Self) : EffectNode;
public sealed record GainBlockEffect(int Amount, ContentId? PerPowerStack = null) : EffectNode;
public sealed record GainBlockFromCurrentBlockEffect(int Multiplier = 1) : EffectNode;
public sealed record ApplyPowerEffect(ContentId Power, int Amount, TargetSelector Target = TargetSelector.Self) : EffectNode;
public sealed record ApplyScaledPowerEffect(ContentId Power, int BaseAmount, int AmountPerCount,
    CombatCountSource Count, TargetSelector Target = TargetSelector.Self) : EffectNode;
public sealed record DrawCardsEffect(int Amount) : EffectNode;
public sealed record MoveCardEffect(CardPile From, CardPile To, int Count = 1) : EffectNode;
public sealed record AddCardsToPileEffect(ContentId Card, CardPile Pile, int Count = 1,
    TargetSelector Target = TargetSelector.FirstEnemy, bool InheritUpgradeFromPlayedCard = false) : EffectNode;
public sealed record ChooseCardFromPileEffect(CardPile From, CardPile? To, string Prompt,
    CardPilePosition DestinationPosition = CardPilePosition.Top,
    CardSelectionFilter Filter = CardSelectionFilter.Any, EffectNode? OnSelected = null) : EffectNode;
public sealed record ChooseCardsFromPileEffect(CardPile From, string Prompt, int MinChoices, int MaxChoices,
    CardSelectionFilter Filter = CardSelectionFilter.Any, EffectNode? OnSelected = null) : EffectNode;
public sealed record RandomlyMoveCardFromPileEffect(CardPile From, CardPile To,
    CardSelectionFilter Filter = CardSelectionFilter.Any) : EffectNode;
public sealed record ForEachCardInPileEffect(CardPile From, CardSelectionFilter Filter, EffectNode Effect) : EffectNode;
public sealed record MoveSelectedCardToPileEffect(CardPile From, CardPile To,
    CardPilePosition DestinationPosition = CardPilePosition.Bottom) : EffectNode;
public sealed record UpgradeSelectedCardEffect : EffectNode;
public sealed record UpgradeCardsInPileEffect(CardPile Pile) : EffectNode;
public sealed record DecrementPowerEffect(ContentId Power) : EffectNode;
public sealed record RemovePowerEffect(ContentId Power) : EffectNode;
public sealed record RetainSelectedCardEffect : EffectNode;
public sealed record ModifyEnergyEffect(int Amount) : EffectNode;
public sealed record GeneratePotionEffect(int Count = 1) : EffectNode;
public sealed record ChooseEffect : EffectNode
{
    [System.Text.Json.Serialization.JsonConstructor]
    public ChooseEffect(string prompt, IReadOnlyList<ChoiceOption> options)
    {
        Prompt = prompt;
        Options = Array.AsReadOnly(options.ToArray());
    }
    public string Prompt { get; }
    public IReadOnlyList<ChoiceOption> Options { get; }
}
public sealed record ChoiceOption(string Label, EffectNode? Effect = null);
public sealed record IfEffect(EffectPredicate Predicate, EffectNode Then, EffectNode? Else = null) : EffectNode;
public sealed record RepeatEffect(int Count, EffectNode Effect) : EffectNode;
public sealed record RepeatForXCostEffect(EffectNode Effect) : EffectNode;
public sealed record CustomEffect(ContentId Handler, System.Text.Json.JsonElement Payload) : EffectNode;

public enum TargetSelector { Self, FirstEnemy, SelectedTarget, AllOpponents, RandomEnemy }
[Flags]
public enum DamageProperties { None = 0, Move = 1, Unpowered = 2, Unblockable = 4, SkipHurtAnimation = 8 }
public enum GameEventActor { Source, Target, Player }
public sealed record GameEventEffect(EffectNode Effect, GameEventActor Actor = GameEventActor.Source);
public enum CardPile { Draw, Hand, Discard, Exhaust, Play }
public enum CardPilePosition { Top, Bottom }
public enum CardSelectionFilter { Any, Upgradeable, NonAttack, NotRetained }
[System.Text.Json.Serialization.JsonPolymorphic(TypeDiscriminatorPropertyName = "test")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(TargetAlivePredicate), "targetAlive")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(HasPowerPredicate), "hasPower")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(EnergyAtLeastPredicate), "energyAtLeast")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(CardExhaustedThisTurnPredicate), "cardExhaustedThisTurn")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(PileCountAtLeastPredicate), "pileCountAtLeast")]
public abstract record EffectPredicate;
public sealed record TargetAlivePredicate : EffectPredicate;
public sealed record HasPowerPredicate(ContentId Power, TargetSelector Target = TargetSelector.Self) : EffectPredicate;
public sealed record EnergyAtLeastPredicate(int Amount) : EffectPredicate;
public sealed record CardExhaustedThisTurnPredicate : EffectPredicate;
public sealed record PileCountAtLeastPredicate(CardPile Pile, int Count) : EffectPredicate;

internal static class EffectExecutor
{
    private static readonly CardPile[] AllCardPiles = Enum.GetValues<CardPile>();

    public static void Execute(GameSession session, EntityState owner, EffectNode effect, EntityState? target = null,
        CardInstance? selectedCard = null)
    {
        var state = session.State;
        state.PendingEffects.Add((owner, effect, target, selectedCard));
        Drain(session);
    }

    public static void Resume(GameSession session) => Drain(session);

    internal static void QueueCardExhaustion(GameSession session, EntityState owner, CardInstance card)
    {
        var state = session.State;
        state.Emit(session, "card_exhausted", owner.Id, card.InstanceId, 1);
        QueuePowerTriggerEffects(session, owner, PowerTrigger.AfterCardExhausted);
    }

    internal static void QueuePowerTriggerEffects(GameSession session, EntityState owner, PowerTrigger trigger,
        EntityState? target = null, CardInstance? selectedCard = null)
    {
        var state = session.State;
        var effects = BuildPowerTriggerEffects(state, owner, trigger);
        if (effects.Count > 0)
            state.PendingEffects.Add((owner, new SequenceEffect(effects), target, selectedCard));
    }

    private static List<EffectNode> BuildPowerTriggerEffects(GameState state, EntityState owner, PowerTrigger trigger)
    {
        var effects = new List<EffectNode>();
        foreach (var powerId in owner.Powers.Keys.ToArray())
        {
            var definition = state.Definition.Powers[powerId];
            if (definition.Trigger != trigger) continue;
            if (owner.SkipNextPowerTriggers.Remove(powerId)) continue;
            if (definition.TriggerEffect is { } triggeredPower)
                effects.Add(new ApplyPowerEffect(triggeredPower, owner.PowerAmount(powerId), TargetSelector.Self));
            if (definition.TriggerRuleEffect is { } triggerEffect) effects.Add(triggerEffect);
            if (definition.DecrementOnTrigger) effects.Add(new DecrementPowerEffect(powerId));
        }
        return effects;
    }

    private static void Drain(GameSession session)
    {
        var state = session.State;
        while (state.PendingChoice is null && state.PendingCardSelection is null && state.PendingEffects.Count > 0)
        {
            var index = state.PendingEffects.Count - 1;
            var (owner, effect, target, selectedCard) = state.PendingEffects[index];
            state.PendingEffects.RemoveAt(index);
            switch (effect)
            {
                case SequenceEffect sequence:
                    for (var i = sequence.Effects.Count - 1; i >= 0; i--)
                        state.PendingEffects.Add((owner, sequence.Effects[i], target, selectedCard));
                    break;
                case AttackEffect attack:
                    if (attack.Target == TargetSelector.AllOpponents)
                    {
                        if (owner.IsEnemy) ApplyAttack(session, owner, state.Player.Creature, attack.Amount, attack.Properties);
                        else
                            foreach (var enemy in state.Enemies)
                                if (enemy.Creature.Hp > 0)
                                    ApplyAttack(session, owner, enemy.Creature, attack.Amount, attack.Properties);
                    }
                    else ApplyAttack(session, owner, ResolveSingleTarget(state, owner, target, attack.Target),
                        attack.Amount, attack.Properties);
                    break;
                case ScaledAttackEffect scaledAttack:
                    var scaledVictim = ResolveSingleTarget(state, owner, target, scaledAttack.Target);
                    ApplyAttack(session, owner, scaledVictim,
                        ScaledAmount(state, owner, scaledVictim, scaledAttack.BaseAmount,
                            scaledAttack.AmountPerCount, scaledAttack.Count));
                    break;
                case RandomAttackEffect randomAttack:
                    if (randomAttack.Amount < 0) throw new InvalidOperationException("Attack damage cannot be negative.");
                    var opponents = owner.IsEnemy
                        ? state.Player.Creature.Hp > 0 ? new[] { state.Player.Creature } : []
                        : state.Enemies.Select(enemy => enemy.Creature).Where(enemy => enemy.Hp > 0).ToArray();
                    if (opponents.Length > 0)
                    {
                        var victim = opponents[session.Random.Get("card-targets").NextInt(opponents.Length)];
                        state.Emit(session, "random_attack_target", owner.Id, victim.Id, randomAttack.Amount);
                        ApplyAttack(session, owner, victim, randomAttack.Amount);
                    }
                    break;
                case AttackUsingBlockEffect attackUsingBlock:
                    var blockBasedDamage = owner.Block;
                    if (attackUsingBlock.Target == TargetSelector.AllOpponents)
                    {
                        if (owner.IsEnemy) ApplyAttack(session, owner, state.Player.Creature, blockBasedDamage);
                        else
                            foreach (var enemy in state.Enemies)
                                if (enemy.Creature.Hp > 0)
                                    ApplyAttack(session, owner, enemy.Creature, blockBasedDamage);
                    }
                    else
                        ApplyAttack(session, owner,
                            ResolveSingleTarget(state, owner, target, attackUsingBlock.Target), blockBasedDamage);
                    break;
                case LoseHpEffect loseHp:
                    if (loseHp.Amount < 0) throw new InvalidOperationException("HP loss cannot be negative.");
                    var hpLossTarget = ResolveSingleTarget(state, owner, target, loseHp.Target);
                    var hpLost = Math.Min(hpLossTarget.Hp, loseHp.Amount);
                    hpLossTarget.Hp -= hpLost;
                    state.Emit(session, "hp_loss", owner.Id, hpLossTarget.Id, hpLost);
                    if (hpLossTarget.Hp == 0) state.RemovePowersAppliedBy(session, hpLossTarget.Id);
                    break;
                case HealEffect heal:
                    if (heal.Amount < 0) throw new InvalidOperationException("Healing cannot be negative.");
                    state.Heal(session, owner, ResolveSingleTarget(state, owner, target, heal.Target), heal.Amount);
                    break;
                case GainBlockEffect block:
                    var blockAmount = block.PerPowerStack is { } perPowerStack
                        ? block.Amount * owner.PowerAmount(perPowerStack)
                        : block.Amount;
                    owner.Block += blockAmount;
                    state.Emit(session, "block", owner.Id, owner.Id, blockAmount);
                    break;
                case GainBlockFromCurrentBlockEffect blockFromBlock:
                    ArgumentOutOfRangeException.ThrowIfNegative(blockFromBlock.Multiplier);
                    var gainedBlock = owner.Block * blockFromBlock.Multiplier;
                    owner.Block += gainedBlock;
                    state.Emit(session, "block", owner.Id, owner.Id, gainedBlock);
                    break;
                case ApplyPowerEffect power:
                    if (power.Target == TargetSelector.AllOpponents)
                    {
                        if (owner.IsEnemy) state.ApplyPower(session, state.Player.Creature, power.Power, power.Amount, owner);
                        else
                            foreach (var enemy in state.Enemies)
                                if (enemy.Creature.Hp > 0)
                                    state.ApplyPower(session, enemy.Creature, power.Power, power.Amount, owner);
                    }
                    else
                        state.ApplyPower(session, ResolveSingleTarget(state, owner, target, power.Target), power.Power, power.Amount, owner);
                    break;
                case ApplyScaledPowerEffect scaledPower:
                    var powerAmount = ScaledAmount(state, owner, target, scaledPower.BaseAmount,
                        scaledPower.AmountPerCount, scaledPower.Count);
                    state.ApplyPower(session, ResolveSingleTarget(state, owner, target, scaledPower.Target),
                        scaledPower.Power, powerAmount, owner);
                    break;
                case DrawCardsEffect draw:
                    state.Draw(session, draw.Amount);
                    break;
                case MoveCardEffect move:
                    var movedCards = state.MoveCards(move.From, move.To, move.Count);
                    var moveTriggerEffects = new List<EffectNode>();
                    foreach (var movedCard in movedCards)
                    {
                        state.Emit(session, "card_moved", owner.Id, movedCard.InstanceId, 1);
                        if (move.To == CardPile.Exhaust && move.From != CardPile.Exhaust)
                        {
                            state.Emit(session, "card_exhausted", state.Player.Id, movedCard.InstanceId, 1);
                            moveTriggerEffects.AddRange(BuildPowerTriggerEffects(state, state.Player.Creature,
                                PowerTrigger.AfterCardExhausted));
                        }
                    }
                    if (moveTriggerEffects.Count > 0)
                        state.PendingEffects.Add((state.Player.Creature, new SequenceEffect(moveTriggerEffects), null, null));
                    break;
                case AddCardsToPileEffect addCards:
                    if (addCards.Count < 0) throw new InvalidOperationException("Added card count cannot be negative.");
                    var cardOwner = ResolveSingleTarget(state, owner, target, addCards.Target);
                    if (!ReferenceEquals(cardOwner, state.Player.Creature))
                        throw new InvalidOperationException("Combat card piles are only modeled for the player.");
                    var destination = state.GetPile(addCards.Pile);
                    var upgradeLevel = addCards.InheritUpgradeFromPlayedCard
                        ? state.PendingCardPlay?.Card.UpgradeLevel ?? 0
                        : 0;
                    for (var i = 0; i < addCards.Count; i++)
                        destination.Add(state.Player.CreateGeneratedCard(addCards.Card, upgradeLevel));
                    state.Emit(session, "cards_added", owner.Id, cardOwner.Id, addCards.Count);
                    break;
                case ChooseCardFromPileEffect chooseCard:
                    var options = state.GetPile(chooseCard.From)
                        .Where(card => state.MatchesCardSelectionFilter(card, chooseCard.Filter)).ToArray();
                    if (options.Length > 0)
                        state.PendingCardSelection = new PendingCardChoice(chooseCard, options, owner, target, selectedCard);
                    break;
                case ChooseCardsFromPileEffect chooseCards:
                    if (chooseCards.MinChoices < 0 || chooseCards.MaxChoices < chooseCards.MinChoices)
                        throw new InvalidOperationException("Card selection bounds are invalid.");
                    var multiOptions = state.GetPile(chooseCards.From)
                        .Where(card => state.MatchesCardSelectionFilter(card, chooseCards.Filter)).ToArray();
                    if (multiOptions.Length > 0 && chooseCards.MaxChoices > 0)
                    {
                        var maximum = Math.Min(chooseCards.MaxChoices, multiOptions.Length);
                        if (chooseCards.MinChoices > maximum)
                            throw new InvalidOperationException("Not enough cards satisfy the required selection count.");
                        state.PendingCardSelection = new PendingCardSetChoice(chooseCards, multiOptions,
                            owner, target, selectedCard, maximum);
                    }
                    break;
                case RandomlyMoveCardFromPileEffect randomCard:
                    var randomOptions = state.GetPile(randomCard.From)
                        .Where(card => state.MatchesCardSelectionFilter(card, randomCard.Filter)).ToArray();
                    if (randomOptions.Length > 0)
                    {
                        var randomOption = randomOptions[session.Random.Get("card-selection").NextInt(randomOptions.Length)];
                        if (!state.MoveCard(randomCard.From, randomCard.To, randomOption.InstanceId, CardPilePosition.Bottom))
                            throw new InvalidOperationException("The randomly selected card is no longer in its source pile.");
                        state.Emit(session, "random_card_choice", owner.Id, randomOption.InstanceId, 1);
                        if (randomCard.To == CardPile.Exhaust && randomCard.From != CardPile.Exhaust)
                            QueueCardExhaustion(session, state.Player.Creature, randomOption);
                    }
                    break;
                case ForEachCardInPileEffect eachCard:
                    var matchingCards = state.GetPile(eachCard.From)
                        .Where(card => state.MatchesCardSelectionFilter(card, eachCard.Filter)).ToArray();
                    for (var i = matchingCards.Length - 1; i >= 0; i--)
                        state.PendingEffects.Add((owner, eachCard.Effect, target, matchingCards[i]));
                    break;
                case MoveSelectedCardToPileEffect moveSelectedCard:
                    if (selectedCard is null)
                        throw new InvalidOperationException("Moving a selected card requires a card selection context.");
                    if (!state.MoveCard(moveSelectedCard.From, moveSelectedCard.To,
                            selectedCard.InstanceId, moveSelectedCard.DestinationPosition))
                        throw new InvalidOperationException("The selected card is no longer in its source pile.");
                    state.Emit(session, "card_moved", owner.Id, selectedCard.InstanceId, 1);
                    if (moveSelectedCard.To == CardPile.Exhaust && moveSelectedCard.From != CardPile.Exhaust)
                        QueueCardExhaustion(session, state.Player.Creature, selectedCard);
                    break;
                case UpgradeSelectedCardEffect:
                    if (selectedCard is null)
                        throw new InvalidOperationException("Upgrading a selected card requires a card selection context.");
                    state.UpgradeCard(session, selectedCard.InstanceId);
                    break;
                case UpgradeCardsInPileEffect upgradeCards:
                    var upgradedCount = 0;
                    foreach (var card in state.GetPile(upgradeCards.Pile).ToArray())
                        if (state.IsUpgradeable(card) && state.UpgradeCard(session, card.InstanceId)) upgradedCount++;
                    state.Emit(session, "cards_upgraded", owner.Id, state.Player.Id, upgradedCount);
                    break;
                case RetainSelectedCardEffect:
                    if (selectedCard is null || state.Player.MutableHand.All(card => card.InstanceId != selectedCard.InstanceId))
                        throw new InvalidOperationException("Retaining a card requires a selected card still in hand.");
                    state.TemporarilyRetainedCards.Add(selectedCard.InstanceId);
                    state.Emit(session, "card_retained", owner.Id, selectedCard.InstanceId, 1);
                    break;
                case DecrementPowerEffect decrementPower:
                    var previousAmount = owner.PowerAmount(decrementPower.Power);
                    state.DecrementPower(owner, decrementPower.Power);
                    if (previousAmount > 0)
                        state.Emit(session, "power", owner.Id, owner.Id, -1);
                    break;
                case RemovePowerEffect removePower:
                    var removedAmount = state.RemovePower(owner, removePower.Power);
                    if (removedAmount != 0)
                        state.Emit(session, "power_removed", owner.Id, owner.Id, removedAmount);
                    break;
                case ModifyEnergyEffect energy:
                    state.Player.Energy = Math.Max(0, state.Player.Energy + energy.Amount);
                    state.Emit(session, "energy", state.Player.Id, state.Player.Id, energy.Amount);
                    break;
                case GeneratePotionEffect generatePotion:
                    GeneratePotions(session, owner, generatePotion.Count);
                    break;
                case ChooseEffect choice:
                    if (choice.Options.Count == 0) throw new InvalidOperationException("A choice must have at least one option.");
                    state.PendingChoice = choice;
                    state.PendingChoiceOwner = owner;
                    state.PendingChoiceTarget = target;
                    state.PendingChoiceSelectedCard = selectedCard;
                    break;
                case IfEffect conditional:
                    var condition = conditional.Predicate switch
                    {
                        TargetAlivePredicate => (target ?? OpponentOf(state, owner)).Hp > 0,
                        HasPowerPredicate hasPower => ResolveSingleTarget(state, owner, target, hasPower.Target)
                            .PowerAmount(hasPower.Power) > 0,
                        EnergyAtLeastPredicate energy => state.Player.Energy >= energy.Amount,
                        CardExhaustedThisTurnPredicate => state.LastPlayerCardExhaustTurn == state.Turn,
                        PileCountAtLeastPredicate pile => state.GetPile(pile.Pile).Count >= pile.Count,
                        _ => throw new InvalidOperationException($"Unsupported effect predicate '{conditional.Predicate.GetType().Name}'.")
                    };
                    if (condition) state.PendingEffects.Add((owner, conditional.Then, target, selectedCard));
                    else if (conditional.Else is not null) state.PendingEffects.Add((owner, conditional.Else, target, selectedCard));
                    break;
                case RepeatEffect repeat:
                    if (repeat.Count < 0) throw new InvalidOperationException("Repeat count cannot be negative.");
                    for (var i = 0; i < repeat.Count; i++) state.PendingEffects.Add((owner, repeat.Effect, target, selectedCard));
                    break;
                case RepeatForXCostEffect repeatForX:
                    for (var i = 0; i < (state.PendingCardPlay?.XEnergy ?? 0); i++)
                        state.PendingEffects.Add((owner, repeatForX.Effect, target, selectedCard));
                    break;
                case CustomEffect custom:
                    if (!state.Definition.CustomEffects.TryGetValue(custom.Handler, out var handler))
                        throw new InvalidOperationException($"Unknown custom effect handler '{custom.Handler}'.");
                    handler.Execute(new EffectExecutionContext(session, owner, target, handler.Id), custom.Payload);
                    break;
                default:
                    throw new NotSupportedException($"Unsupported effect node: {effect.GetType().Name}");
            }
        }
    }

    private static EntityState ResolveSingleTarget(GameState state, EntityState owner,
        EntityState? selected, TargetSelector selector) => selector switch
    {
        TargetSelector.Self => owner,
        TargetSelector.FirstEnemy => OpponentOf(state, owner),
        TargetSelector.SelectedTarget => selected ?? OpponentOf(state, owner),
        _ => throw new ArgumentOutOfRangeException(nameof(selector), "This effect requires one target.")
    };

    private static int ScaledAmount(GameState state, EntityState owner, EntityState? selectedTarget, int baseAmount,
        int amountPerCount, CombatCountSource source)
    {
        var count = source switch
        {
            PileCardCountSource pile => state.GetPile(pile.Pile).Count,
            TargetPowerCountSource power => (selectedTarget ?? OpponentOf(state, owner)).PowerAmount(power.Power),
            TaggedCardCountSource tag => AllCardPiles.Sum(pile => state.GetPile(pile)
                .Count(card => state.Definition.Cards[card.CardId].Tags.Contains(tag.Tag))),
            _ => throw new InvalidOperationException("Unsupported combat count source.")
        };
        return checked(baseAmount + amountPerCount * count);
    }

    private static void GeneratePotions(GameSession session, EntityState owner, int count)
    {
        var state = session.State;
        if (owner != state.Player.Creature)
            throw new InvalidOperationException("Potions can only be generated for the player.");
        var run = session.RunOwner?.State
            ?? throw new InvalidOperationException("Potion generation requires an active run inventory.");
        var available = state.Definition.PotionPoolFor(run.Character.Id)
            .Where(potion => potion.CanBeGeneratedInCombat)
            .ToList();
        var random = run.Random.Get("combat-potion-generation");
        for (var i = 0; i < count && run.FindEmptyPotionSlot() >= 0; i++)
        {
            var rarityRoll = random.NextFloat();
            var rarity = rarityRoll <= 0.1f ? PotionRarity.Rare
                : rarityRoll <= 0.35f ? PotionRarity.Uncommon : PotionRarity.Common;
            var rarityPool = available.Where(potion => potion.Rarity == rarity).ToArray();
            if (rarityPool.Length == 0)
                throw new InvalidOperationException($"No combat-generated {rarity} potion is registered for '{run.Character.Id}'.");
            var potion = rarityPool[random.NextInt(rarityPool.Length)];
            available.Remove(potion);
            if (!run.TryAddPotion(potion.Id)) break;
            state.Emit(session, "potion_generated", state.Player.Id, potion.Id.Value, 1);
        }
    }

    private static void ApplyAttack(GameSession session, EntityState owner, EntityState victim, int amount,
        DamageProperties properties = DamageProperties.Move)
    {
        if (victim.Hp <= 0) return;
        var state = session.State;
        var rawDamage = state.CalculateDamage(session, owner, victim, amount, properties);
        var absorbed = (properties & DamageProperties.Unblockable) != 0
            ? 0
            : Math.Min(victim.Block, rawDamage);
        victim.Block -= absorbed;
        var damage = Math.Min(victim.Hp, rawDamage - absorbed);
        victim.Hp -= damage;
        state.Emit(session, "damage", owner.Id, victim.Id, damage);
        if (victim.Hp == 0) state.RemovePowersAppliedBy(session, victim.Id);
    }

    private static EntityState OpponentOf(GameState state, EntityState owner) => owner.IsEnemy
        ? state.Player.Creature
        : state.FirstLivingEnemy ?? throw new InvalidOperationException("There is no living enemy to target.");
}
