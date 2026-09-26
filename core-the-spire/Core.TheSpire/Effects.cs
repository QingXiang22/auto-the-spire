namespace CoreTheSpire;

[System.Text.Json.Serialization.JsonPolymorphic(TypeDiscriminatorPropertyName = "op")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(SequenceEffect), "sequence")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(AttackEffect), "attack")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(LoseHpEffect), "loseHp")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(GainBlockEffect), "gainBlock")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(ApplyPowerEffect), "applyPower")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(DrawCardsEffect), "drawCards")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(MoveCardEffect), "moveCard")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(ModifyEnergyEffect), "modifyEnergy")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(ChooseEffect), "choose")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(IfEffect), "if")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(RepeatEffect), "repeat")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(AddCardsToPileEffect), "addCardsToPile")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(ChooseCardFromPileEffect), "chooseCardFromPile")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(UpgradeSelectedCardEffect), "upgradeSelectedCard")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(UpgradeCardsInPileEffect), "upgradeCardsInPile")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(CustomEffect), "custom")]
public abstract record EffectNode;
public sealed record SequenceEffect : EffectNode
{
    [System.Text.Json.Serialization.JsonConstructor]
    public SequenceEffect(IReadOnlyList<EffectNode> effects) => Effects = Array.AsReadOnly(effects.ToArray());
    public IReadOnlyList<EffectNode> Effects { get; }
}
public sealed record AttackEffect(int Amount, TargetSelector Target = TargetSelector.FirstEnemy) : EffectNode;
public sealed record LoseHpEffect(int Amount, TargetSelector Target = TargetSelector.Self) : EffectNode;
public sealed record GainBlockEffect(int Amount) : EffectNode;
public sealed record ApplyPowerEffect(ContentId Power, int Amount, TargetSelector Target = TargetSelector.Self) : EffectNode;
public sealed record DrawCardsEffect(int Amount) : EffectNode;
public sealed record MoveCardEffect(CardPile From, CardPile To, int Count = 1) : EffectNode;
public sealed record AddCardsToPileEffect(ContentId Card, CardPile Pile, int Count = 1,
    TargetSelector Target = TargetSelector.FirstEnemy, bool InheritUpgradeFromPlayedCard = false) : EffectNode;
public sealed record ChooseCardFromPileEffect(CardPile From, CardPile? To, string Prompt,
    CardPilePosition DestinationPosition = CardPilePosition.Top,
    CardSelectionFilter Filter = CardSelectionFilter.Any, EffectNode? OnSelected = null) : EffectNode;
public sealed record UpgradeSelectedCardEffect : EffectNode;
public sealed record UpgradeCardsInPileEffect(CardPile Pile) : EffectNode;
public sealed record ModifyEnergyEffect(int Amount) : EffectNode;
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
public sealed record CustomEffect(ContentId Handler, System.Text.Json.JsonElement Payload) : EffectNode;

public enum TargetSelector { Self, FirstEnemy, SelectedTarget, AllOpponents }
public enum CardPile { Draw, Hand, Discard, Exhaust, Play }
public enum CardPilePosition { Top, Bottom }
public enum CardSelectionFilter { Any, Upgradeable }
[System.Text.Json.Serialization.JsonPolymorphic(TypeDiscriminatorPropertyName = "test")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(TargetAlivePredicate), "targetAlive")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(HasPowerPredicate), "hasPower")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(EnergyAtLeastPredicate), "energyAtLeast")]
public abstract record EffectPredicate;
public sealed record TargetAlivePredicate : EffectPredicate;
public sealed record HasPowerPredicate(ContentId Power) : EffectPredicate;
public sealed record EnergyAtLeastPredicate(int Amount) : EffectPredicate;

internal static class EffectExecutor
{
    public static void Execute(GameSession session, EntityState owner, EffectNode effect, EntityState? target = null,
        CardInstance? selectedCard = null)
    {
        var state = session.State;
        state.PendingEffects.Add((owner, effect, target, selectedCard));
        Drain(session);
    }

    public static void Resume(GameSession session) => Drain(session);

    private static void Drain(GameSession session)
    {
        var state = session.State;
        while (state.PendingChoice is null && state.PendingCardChoice is null && state.PendingEffects.Count > 0)
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
                        if (owner.IsEnemy) ApplyAttack(session, owner, state.Player.Creature, attack.Amount);
                        else
                            foreach (var enemy in state.Enemies)
                                if (enemy.Creature.Hp > 0) ApplyAttack(session, owner, enemy.Creature, attack.Amount);
                    }
                    else ApplyAttack(session, owner, ResolveSingleTarget(state, owner, target, attack.Target), attack.Amount);
                    break;
                case LoseHpEffect loseHp:
                    if (loseHp.Amount < 0) throw new InvalidOperationException("HP loss cannot be negative.");
                    var hpLossTarget = ResolveSingleTarget(state, owner, target, loseHp.Target);
                    var hpLost = Math.Min(hpLossTarget.Hp, loseHp.Amount);
                    hpLossTarget.Hp -= hpLost;
                    state.Emit(session, "hp_loss", owner.Id, hpLossTarget.Id, hpLost);
                    if (hpLossTarget.Hp == 0) state.RemovePowersAppliedBy(session, hpLossTarget.Id);
                    break;
                case GainBlockEffect block:
                    owner.Block += block.Amount;
                    state.Emit(session, "block", owner.Id, owner.Id, block.Amount);
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
                case DrawCardsEffect draw:
                    state.Draw(session, draw.Amount);
                    break;
                case MoveCardEffect move:
                    state.MoveCards(move.From, move.To, move.Count);
                    break;
                case AddCardsToPileEffect addCards:
                    if (addCards.Count < 0) throw new InvalidOperationException("Added card count cannot be negative.");
                    var cardOwner = ResolveSingleTarget(state, owner, target, addCards.Target);
                    if (!ReferenceEquals(cardOwner, state.Player.Creature))
                        throw new InvalidOperationException("Combat card piles are only modeled for the player.");
                    var destination = state.GetPile(addCards.Pile);
                    var upgradeLevel = addCards.InheritUpgradeFromPlayedCard
                        ? state.PendingPlayedCard?.UpgradeLevel ?? 0
                        : 0;
                    for (var i = 0; i < addCards.Count; i++)
                        destination.Add(state.Player.CreateGeneratedCard(addCards.Card, upgradeLevel));
                    state.Emit(session, "cards_added", owner.Id, cardOwner.Id, addCards.Count);
                    break;
                case ChooseCardFromPileEffect chooseCard:
                    var options = state.GetPile(chooseCard.From)
                        .Where(card => chooseCard.Filter switch
                        {
                            CardSelectionFilter.Any => true,
                            CardSelectionFilter.Upgradeable => state.IsUpgradeable(card),
                            _ => false
                        }).ToArray();
                    if (options.Length > 0)
                        state.PendingCardChoice = new PendingCardChoice(chooseCard, options, owner, target, selectedCard);
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
                case ModifyEnergyEffect energy:
                    state.Player.Energy = Math.Max(0, state.Player.Energy + energy.Amount);
                    state.Emit(session, "energy", state.Player.Id, state.Player.Id, energy.Amount);
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
                        HasPowerPredicate hasPower => owner.PowerAmount(hasPower.Power) > 0,
                        EnergyAtLeastPredicate energy => state.Player.Energy >= energy.Amount,
                        _ => false
                    };
                    if (condition) state.PendingEffects.Add((owner, conditional.Then, target, selectedCard));
                    else if (conditional.Else is not null) state.PendingEffects.Add((owner, conditional.Else, target, selectedCard));
                    break;
                case RepeatEffect repeat:
                    if (repeat.Count < 0) throw new InvalidOperationException("Repeat count cannot be negative.");
                    for (var i = 0; i < repeat.Count; i++) state.PendingEffects.Add((owner, repeat.Effect, target, selectedCard));
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

    private static void ApplyAttack(GameSession session, EntityState owner, EntityState victim, int amount)
    {
        if (victim.Hp <= 0) return;
        var state = session.State;
        var rawDamage = state.CalculateDamage(owner, victim, amount);
        var absorbed = Math.Min(victim.Block, rawDamage);
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
