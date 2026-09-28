namespace CoreTheSpire;

[System.Text.Json.Serialization.JsonPolymorphic(TypeDiscriminatorPropertyName = "op")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(SequenceEffect), "sequence")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(AttackEffect), "attack")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(AttackCardVariableEffect), "attackCardVariable")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(ScaledAttackEffect), "scaledAttack")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(RandomAttackEffect), "randomAttack")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(AttackUsingBlockEffect), "attackUsingBlock")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(LoseHpEffect), "loseHp")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(HealEffect), "heal")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(KillEffect), "kill")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(GainBlockEffect), "gainBlock")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(GainBlockFromCurrentBlockEffect), "gainBlockFromCurrentBlock")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(ApplyPowerEffect), "applyPower")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(ApplyScaledPowerEffect), "applyScaledPower")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(DrawCardsEffect), "drawCards")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(DrawUntilCardTypeEffect), "drawUntilCardType")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(DrawUntilContinuationEffect), "drawUntilContinuation")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(MoveCardEffect), "moveCard")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(ModifyEnergyEffect), "modifyEnergy")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(GeneratePotionEffect), "generatePotion")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(GenerateCombatCardsEffect), "generateCombatCards")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(ChooseEffect), "choose")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(IfEffect), "if")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(RepeatEffect), "repeat")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(RepeatForCountEffect), "repeatForCount")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(WithCountEffect), "withCount")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(AutoPlayCardsEffect), "autoPlayCards")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(AutoPlayRandomCardsEffect), "autoPlayRandomCards")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(AutoPlaySelectedCardEffect), "autoPlaySelectedCard")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(RepeatForXCostEffect), "repeatForXCost")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(AddCardsToPileEffect), "addCardsToPile")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(ChooseCardFromPileEffect), "chooseCardFromPile")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(ChooseCardsFromPileEffect), "chooseCardsFromPile")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(RandomlyMoveCardFromPileEffect), "randomlyMoveCardFromPile")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(RandomlyMoveCardsEffect), "randomlyMoveCards")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(ForEachCardInPileEffect), "forEachCardInPile")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(MoveSelectedCardToPileEffect), "moveSelectedCardToPile")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(UpgradeSelectedCardEffect), "upgradeSelectedCard")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(ModifySelectedCardCostEffect), "modifySelectedCardCost")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(ModifyCardVariableEffect), "modifyCardVariable")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(AccumulateCardDamageEffect), "accumulateCardDamage")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(ModifyPowerVariableEffect), "modifyPowerVariable")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(ModifyEnemyVariableEffect), "modifyEnemyVariable")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(ModifyCombatStateEffect), "modifyCombatState")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(GainMaxHpEffect), "gainMaxHp")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(LoseMaxHpEffect), "loseMaxHp")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(DowngradeSelectedCardEffect), "downgradeSelectedCard")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(CloneCardEffect), "cloneCard")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(TransformSelectedCardEffect), "transformSelectedCard")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(UpgradeCardsInPileEffect), "upgradeCardsInPile")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(DecrementPowerEffect), "decrementPower")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(RemovePowerEffect), "removePower")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(SetEnemyActionEffect), "setEnemyAction")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(ExpirePowerEffect), "expirePower")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(RetainSelectedCardEffect), "retainSelectedCard")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(SummonEnemyEffect), "summonEnemy")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(StunEnemyEffect), "stunEnemy")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(SetEnemyHealthEffect), "setEnemyHealth")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(CustomEffect), "custom")]
public abstract record EffectNode;
public sealed record SequenceEffect : EffectNode
{
    [System.Text.Json.Serialization.JsonConstructor]
    public SequenceEffect(IReadOnlyList<EffectNode> effects) => Effects = Array.AsReadOnly(effects.ToArray());
    public IReadOnlyList<EffectNode> Effects { get; }
}
public sealed record AttackEffect(int Amount, TargetSelector Target = TargetSelector.FirstEnemy,
    DamageProperties Properties = DamageProperties.Move, EffectNode? OnFatal = null) : EffectNode;
public sealed record AttackCardVariableEffect(ContentId Variable, int HitCount = 1,
    TargetSelector Target = TargetSelector.SelectedTarget,
    DamageProperties Properties = DamageProperties.Move) : EffectNode;
public sealed record ScaledAttackEffect(int BaseAmount, int AmountPerCount, CombatCountSource Count,
    TargetSelector Target = TargetSelector.SelectedTarget,
    DamageProperties Properties = DamageProperties.Move, EffectNode? OnFatal = null) : EffectNode;
[System.Text.Json.Serialization.JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(PileCardCountSource), "pile")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(TargetPowerCountSource), "targetPower")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(TaggedCardCountSource), "taggedCards")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(PlayerHpLossCountSource), "playerHpLossCount")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(EnergyXCountSource), "energyX")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(SavedCountSource), "savedCount")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(CardPlaysThisTurnCountSource), "cardPlaysThisTurn")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(CardVariableCountSource), "cardVariable")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(PowerVariableCountSource), "powerVariable")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(TakePowerEventAmountCountSource), "takePowerEventAmount")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(TakePowerTurnEventAmountCountSource), "takePowerTurnEventAmount")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(ProductCombatCountSource), "product")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(BlockCountSource), "block")]
public abstract record CombatCountSource;
public sealed record PileCardCountSource(CardPile Pile, CardType? Type = null) : CombatCountSource;
public sealed record PlayerHpLossCountSource : CombatCountSource;
public sealed record EnergyXCountSource : CombatCountSource;
public sealed record SavedCountSource(string Name) : CombatCountSource;
public sealed record CardPlaysThisTurnCountSource(CardType? Type = null,
    CardPlayHistoryPhase Phase = CardPlayHistoryPhase.Finished) : CombatCountSource;
public sealed record CardVariableCountSource(ContentId Variable, CardContext Card = CardContext.Played) : CombatCountSource;
public sealed record PowerVariableCountSource(ContentId Power, ContentId Variable,
    TargetSelector Target = TargetSelector.Self) : CombatCountSource;
public sealed record TakePowerEventAmountCountSource(ContentId Power, CardContext Card = CardContext.Played) : CombatCountSource;
public sealed record TakePowerTurnEventAmountCountSource(ContentId Power) : CombatCountSource;
public sealed record ProductCombatCountSource(CombatCountSource Left, CombatCountSource Right) : CombatCountSource;
public sealed record BlockCountSource(TargetSelector Target = TargetSelector.Self) : CombatCountSource;
public sealed record ScaledCombatAmount(int BaseAmount, int PerCount, CombatCountSource Count);
public sealed record TargetPowerCountSource(ContentId Power, TargetSelector Target = TargetSelector.SelectedTarget) : CombatCountSource;
public sealed record TaggedCardCountSource(ContentId Tag) : CombatCountSource;
public sealed record RandomAttackEffect(int Amount, ScaledCombatAmount? CalculatedAmount = null,
    DamageProperties Properties = DamageProperties.Move) : EffectNode;
public sealed record AttackUsingBlockEffect(TargetSelector Target = TargetSelector.SelectedTarget) : EffectNode;
public sealed record LoseHpEffect(int Amount, TargetSelector Target = TargetSelector.Self,
    DamageProperties Properties = DamageProperties.None) : EffectNode;
public sealed record HealEffect(int Amount, TargetSelector Target = TargetSelector.Self) : EffectNode;
public sealed record KillEffect(TargetSelector Target = TargetSelector.Self) : EffectNode;
public sealed record GainBlockEffect(int Amount, ContentId? PerPowerStack = null,
    DamageProperties? Properties = null, TargetSelector Target = TargetSelector.Self,
    ContentId? OnlyEnemy = null) : EffectNode;
public sealed record GainBlockFromCurrentBlockEffect(int Multiplier = 1,
    TargetSelector Target = TargetSelector.Self) : EffectNode;
public sealed record ApplyPowerEffect(ContentId Power, int Amount, TargetSelector Target = TargetSelector.Self) : EffectNode;
public sealed record ApplyScaledPowerEffect(ContentId Power, int BaseAmount, int AmountPerCount,
    CombatCountSource Count, TargetSelector Target = TargetSelector.Self) : EffectNode;
public sealed record DrawCardsEffect(int Amount, ScaledCombatAmount? CalculatedAmount = null) : EffectNode;
public sealed record DrawUntilCardTypeEffect(CardType ContinueWhileType) : EffectNode;
internal sealed record DrawUntilContinuationEffect(CardType ContinueWhileType, CardType LastDrawnType) : EffectNode;
internal sealed record DrawCardsContinuationEffect(int Remaining) : EffectNode;
public sealed record MoveCardEffect(CardPile From, CardPile To, int Count = 1) : EffectNode;
public sealed record AddCardsToPileEffect(ContentId Card, CardPile Pile, int Count = 1,
    TargetSelector Target = TargetSelector.FirstEnemy, bool InheritUpgradeFromPlayedCard = false) : EffectNode;
public sealed record ChooseCardFromPileEffect(CardPile From, CardPile? To, string Prompt,
    CardPilePosition DestinationPosition = CardPilePosition.Top,
    CardSelectionFilter Filter = CardSelectionFilter.Any, EffectNode? OnSelected = null) : EffectNode;
public sealed record ChooseCardsFromPileEffect(CardPile From, string Prompt, int MinChoices, int MaxChoices,
    CardSelectionFilter Filter = CardSelectionFilter.Any, EffectNode? OnSelected = null) : EffectNode;
public sealed record RandomlyMoveCardFromPileEffect(CardPile From, CardPile To,
    CardSelectionFilter Filter = CardSelectionFilter.Any, EffectNode? AfterMove = null) : EffectNode;
public sealed record RandomlyMoveCardsEffect(CardPile From, CardPile To, int Count = 1,
    ScaledCombatAmount? CalculatedCount = null, CardSelectionFilter Filter = CardSelectionFilter.Any,
    EffectNode? AfterMove = null) : EffectNode;
internal sealed record RandomMoveCardsBatchEffect(CardPile From, CardPile To,
    IReadOnlyList<CardInstance> Cards, int Index, CardSelectionFilter Filter, EffectNode? AfterMove,
    EntityState? Target, CardInstance? ParentSelectedCard, EffectCountScope? Counts) : EffectNode;
internal sealed record RandomMoveCardsAfterEffect(CardPile From, CardPile To,
    IReadOnlyList<CardInstance> Cards, int Index, CardSelectionFilter Filter, EffectNode? AfterMove,
    EntityState? Target, CardInstance? ParentSelectedCard, EffectCountScope? Counts,
    CardInstance MovedCard) : EffectNode;
internal sealed record RandomMoveSingleAfterEffect(CardPile From, CardPile To, EffectNode? AfterMove,
    EntityState? Target, CardInstance? Card, EffectCountScope? Counts) : EffectNode;
public sealed record ForEachCardInPileEffect(CardPile From, CardSelectionFilter Filter, EffectNode Effect) : EffectNode;
public sealed record MoveSelectedCardToPileEffect(CardPile From, CardPile To,
    CardPilePosition DestinationPosition = CardPilePosition.Bottom) : EffectNode;
public sealed record UpgradeSelectedCardEffect : EffectNode;
public sealed record ModifySelectedCardCostEffect(CardCostModifier Modifier,
    ScaledCombatAmount? CalculatedAmount = null) : EffectNode;
public sealed record ModifyCardVariableEffect(ContentId Variable, decimal Amount,
    CardContext Card = CardContext.Played, CardVariableOperation Operation = CardVariableOperation.Add,
    ScaledCombatAmount? CalculatedAmount = null) : EffectNode;
public sealed record AccumulateCardDamageEffect(ContentId Variable,
    CardContext TargetCard = CardContext.Played, CardContext SourceCard = CardContext.Selected) : EffectNode;
public enum PowerVariableOperation { Add, Set }
public sealed record ModifyPowerVariableEffect(ContentId Power, ContentId Variable, int Amount,
    TargetSelector Target = TargetSelector.Self, PowerVariableOperation Operation = PowerVariableOperation.Add) : EffectNode;
public sealed record ModifyEnemyVariableEffect(ContentId Variable, int Amount,
    PowerVariableOperation Operation = PowerVariableOperation.Add) : EffectNode;
public sealed record ModifyCombatStateEffect(ContentId Variable, int Amount,
    PowerVariableOperation Operation = PowerVariableOperation.Add) : EffectNode;
public sealed record GainMaxHpEffect(int Amount) : EffectNode;
public sealed record LoseMaxHpEffect(int Amount, TargetSelector Target = TargetSelector.Self) : EffectNode;
public sealed record DowngradeSelectedCardEffect : EffectNode;
public sealed record CloneCardEffect(CardContext Card = CardContext.Played, CardPile Pile = CardPile.Hand,
    int Count = 1, ScaledCombatAmount? CalculatedCount = null) : EffectNode;
public sealed record TransformSelectedCardEffect(ContentId Card, CardPile From, bool Upgraded = false) : EffectNode;
public sealed record UpgradeCardsInPileEffect(CardPile Pile) : EffectNode;
public sealed record DecrementPowerEffect(ContentId Power) : EffectNode;
public sealed record RemovePowerEffect(ContentId Power) : EffectNode;
public sealed record SetEnemyActionEffect(ContentId Action) : EffectNode;
public sealed record ExpirePowerEffect(ContentId Power) : EffectNode;
public sealed record RetainSelectedCardEffect : EffectNode;
public sealed record ModifyEnergyEffect(int Amount, ScaledCombatAmount? CalculatedAmount = null) : EffectNode;
public sealed record GeneratePotionEffect(int Count = 1) : EffectNode;
public sealed record GenerateCombatCardsEffect(int Count, CardPile Pile = CardPile.Hand, bool Upgraded = false,
    ScaledCombatAmount? CalculatedCount = null, CardType? Type = null, bool Distinct = false,
    CardCostModifier? Cost = null) : EffectNode;
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
public sealed record RepeatForCountEffect(int BaseCount, int PerCount, CombatCountSource Count,
    EffectNode Effect) : EffectNode;
public sealed record WithCountEffect(string Name, CombatCountSource Count, EffectNode Effect) : EffectNode;
public sealed record AutoPlayCardsEffect(CardPile From, int Count, CardPilePosition Position = CardPilePosition.Top,
    bool ForceExhaust = false, ScaledCombatAmount? CalculatedCount = null) : EffectNode;
public sealed record AutoPlayRandomCardsEffect(CardPile From, int Count, CardType? Type = null,
    bool ForceExhaust = false, ScaledCombatAmount? CalculatedCount = null) : EffectNode;
public sealed record AutoPlaySelectedCardEffect(CardPile From = CardPile.Exhaust, bool ForceExhaust = false) : EffectNode;
internal sealed record AutoPlayRandomCardsContinuationEffect(AutoPlayRandomCardsEffect Effect, int Remaining) : EffectNode;
internal sealed record CardAfterAutoPostPlayEffect(string InstanceId) : EffectNode;
public sealed record RepeatForXCostEffect(EffectNode Effect) : EffectNode;
public sealed record CustomEffect(ContentId Handler, System.Text.Json.JsonElement Payload) : EffectNode;
public enum SummonSlotPlacement { NextAvailable, FirstAvailable, LastAvailable }
public sealed record SummonEnemyEffect(ContentId Enemy, int Count = 1, ContentId? StartingAction = null,
    IReadOnlyList<EnemyStartingPower>? StartingPowers = null, int? MaximumLivingEnemyCount = null,
    int? MaximumEnemyCount = null, SummonSlotPlacement SlotPlacement = SummonSlotPlacement.NextAvailable) : EffectNode;
public sealed record StunEnemyEffect(TargetSelector Target = TargetSelector.Self) : EffectNode;
public sealed record SetEnemyHealthEffect(int Minimum, int Maximum) : EffectNode;
internal sealed record InitializeSummonedEnemiesEffect(IReadOnlyList<string> InstanceIds) : EffectNode;

public enum TargetSelector { Self, FirstEnemy, SelectedTarget, AllOpponents, RandomEnemy, OtherAllies }
[Flags]
public enum DamageProperties { None = 0, Move = 1, Unpowered = 2, Unblockable = 4, SkipHurtAnimation = 8 }
public enum GameEventActor { Source, Target, Player }
public sealed record GameEventEffect(EffectNode Effect, GameEventActor Actor = GameEventActor.Source);
public enum PowerEventAccumulationMode { EventCount, PowerStacks }
public sealed record PowerEventSubscription(string EventType, GameEventActor Owner, EffectNode Effect,
    DamageProperties RequiredProperties = DamageProperties.None,
    DamageProperties ExcludedProperties = DamageProperties.None, bool RequiresPositiveAmount = true,
    bool OwnerTurnOnly = false, bool DeferUntilCardPlayed = false,
    bool DeferUntilOwnerTurnEnd = false, PowerEventAccumulationMode Accumulation = PowerEventAccumulationMode.PowerStacks,
    bool? CausedByEtherealExhaustion = null, ContentId? AffectedPower = null, ContentId? CardTag = null);
internal sealed record PowerEventEffect(ContentId Power, EffectNode Effect, string? EventCardInstanceId = null) : EffectNode;
public enum CardPile { Draw, Hand, Discard, Exhaust, Play }
public enum CardPilePosition { Top, Bottom }
public enum CardSelectionFilter { Any, Upgradeable, NonAttack, NotRetained, Attack }
[System.Text.Json.Serialization.JsonPolymorphic(TypeDiscriminatorPropertyName = "test")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(TargetAlivePredicate), "targetAlive")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(HasPowerPredicate), "hasPower")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(EnergyAtLeastPredicate), "energyAtLeast")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(CardExhaustedThisTurnPredicate), "cardExhaustedThisTurn")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(PlayerLostHpThisTurnPredicate), "playerLostHpThisTurn")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(SelectedCardIsClonePredicate), "selectedCardIsClone")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(PileCountAtLeastPredicate), "pileCountAtLeast")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(CardPlayCountEqualsPredicate), "cardPlayCountEquals")]
public abstract record EffectPredicate;
public sealed record TargetAlivePredicate : EffectPredicate;
public sealed record HasPowerPredicate(ContentId Power, TargetSelector Target = TargetSelector.Self) : EffectPredicate;
public sealed record EnergyAtLeastPredicate(int Amount) : EffectPredicate;
public sealed record CardExhaustedThisTurnPredicate : EffectPredicate;
public sealed record PlayerLostHpThisTurnPredicate : EffectPredicate;
public sealed record SelectedCardIsClonePredicate : EffectPredicate;
public sealed record PileCountAtLeastPredicate(CardPile Pile, int Count) : EffectPredicate;
public sealed record CardPlayCountEqualsPredicate(CardType? Type, CardPlayHistoryPhase Phase, int Count) : EffectPredicate;

internal static class EffectExecutor
{
    private static readonly CardPile[] AllCardPiles = Enum.GetValues<CardPile>();

    public static void Execute(GameSession session, EntityState owner, EffectNode effect, EntityState? target = null,
        CardInstance? selectedCard = null, EffectCountScope? counts = null)
    {
        var state = session.State;
        state.PendingEffects.Add(new(owner, effect, target, selectedCard, counts));
        Drain(session);
    }

    public static void Resume(GameSession session) => Drain(session);

    internal static void QueueCardExhaustion(GameSession session, EntityState owner, CardInstance card,
        bool causedByEtherealExhaustion = false)
    {
        var state = session.State;
        state.Emit(session, "card_exhausted", owner.Id, card.InstanceId, 1,
            cardSource: card, causedByEtherealExhaustion: causedByEtherealExhaustion);
        var effects = BuildExhaustTriggerEffects(state, owner, card);
        if (effects.Count > 0) state.PendingEffects.Add(new(owner, new SequenceEffect(effects), null, card));
    }

    private static List<EffectNode> BuildExhaustTriggerEffects(GameState state, EntityState owner, CardInstance card)
    {
        var effects = new List<EffectNode>();
        if (state.Definition.Cards[card.CardId].ResolveExhaustEffect(card) is { } ownEffect) effects.Add(ownEffect);
        effects.AddRange(BuildPowerTriggerEffects(state, owner, PowerTrigger.AfterCardExhausted, card));
        return effects;
    }

    internal static void QueuePowerTriggerEffects(GameSession session, EntityState owner, PowerTrigger trigger,
        EntityState? target = null, CardInstance? selectedCard = null)
    {
        var state = session.State;
        var effects = BuildPowerTriggerEffects(state, owner, trigger, selectedCard);
        if (effects.Count > 0)
            state.PendingEffects.Add(new(owner, new SequenceEffect(effects), target, selectedCard));
    }

    internal static void QueueCombatPowerTriggerEffects(GameSession session, PowerTrigger trigger,
        EntityState? target = null, CardInstance? selectedCard = null)
    {
        var state = session.State;
        foreach (var owner in state.Enemies.Select(enemy => enemy.Creature).Append(state.Player.Creature))
            QueuePowerTriggerEffects(session, owner, trigger, target, selectedCard);
    }

    private static List<EffectNode> BuildPowerTriggerEffects(GameState state, EntityState owner, PowerTrigger trigger,
        CardInstance? eventCard = null)
    {
        var effects = new List<EffectNode>();
        foreach (var powerId in owner.Powers.Keys.ToArray())
        {
            var definition = state.Definition.Powers[powerId];
            if (definition.Trigger != trigger) continue;
            if (definition.TriggerCardType is { } cardType && (eventCard is null
                || state.Definition.Cards[eventCard.CardId].Type != cardType)) continue;
            if (owner.SkipNextPowerTriggers.Remove(powerId)) continue;
            if (definition.TriggerEffect is { } triggeredPower)
                effects.Add(new ApplyPowerEffect(triggeredPower, owner.PowerAmount(powerId), TargetSelector.Self));
            if (definition.TriggerRuleEffect is { } triggerEffect) effects.Add(triggerEffect);
            if (definition.DecrementOnTrigger) effects.Add(new DecrementPowerEffect(powerId));
            foreach (var lifecycle in definition.Lifecycle.Where(subscription => subscription.Trigger == trigger))
                effects.Add(lifecycle.Effect);
        }
        return effects;
    }

    private static void Drain(GameSession session)
    {
        var state = session.State;
        while (state.PendingChoice is null && state.PendingCardSelection is null && state.PendingEffects.Count > 0)
        {
            var index = state.PendingEffects.Count - 1;
            var (owner, effect, target, selectedCard, counts) = state.PendingEffects[index];
            state.PendingEffects.RemoveAt(index);
            switch (effect)
            {
                case SummonEnemyEffect summon:
                    QueueSummonedEnemies(session, owner, summon);
                    break;
                case StunEnemyEffect stun:
                    var stunnedCreature = ResolveSingleTarget(state, owner, target, stun.Target);
                    var stunnedEnemy = state.Enemies.FirstOrDefault(enemy => enemy.Creature.Id == stunnedCreature.Id)
                        ?? throw new InvalidOperationException("Stun requires an enemy target.");
                    if (stunnedEnemy.Creature.Hp > 0)
                    {
                        stunnedEnemy.SkipNextAction = true;
                        state.Emit(session, "enemy_stunned", owner.Id, stunnedEnemy.Creature.Id, 1);
                    }
                    break;
                case KillEffect kill:
                    var killedCreature = ResolveSingleTarget(state, owner, target, kill.Target);
                    if (killedCreature.Hp > 0)
                    {
                        killedCreature.Hp = 0;
                        CompleteDeath(session, killedCreature, owner, selectedCard, DamageProperties.None);
                    }
                    break;
                case SetEnemyHealthEffect setHealth:
                    if (owner.IsEnemy is false)
                        throw new InvalidOperationException("Setting enemy health requires an enemy action owner.");
                    var healthRange = checked(setHealth.Maximum - setHealth.Minimum + 1);
                    var health = setHealth.Minimum + (healthRange == 1 ? 0
                        : session.Random.Get("niche").NextInt(healthRange));
                    var healthOwner = state.Enemies.FirstOrDefault(enemy => enemy.Creature.Id == owner.Id)
                        ?? throw new InvalidOperationException("Setting enemy health requires an active enemy owner.");
                    var previousHp = healthOwner.Creature.Hp;
                    healthOwner.Creature.MaxHp = health;
                    healthOwner.Creature.Hp = health;
                    state.Emit(session, "enemy_health_set", owner.Id, owner.Id, health - previousHp,
                        exactAmount: health);
                    break;
                case InitializeSummonedEnemiesEffect initialize:
                    foreach (var instanceId in initialize.InstanceIds)
                    {
                        var enemyIndex = state.Enemies.FindIndex(enemy =>
                            StringComparer.Ordinal.Equals(enemy.Creature.Id, instanceId));
                        if (enemyIndex < 0) continue;
                        var summoned = state.Enemies[enemyIndex];
                        summoned.ActionIndex = summoned.StartingActionOverride is { } startingAction
                            ? summoned.Definition.GetActionIndex(startingAction)
                            : summoned.Definition.ResolveStartingActionIndex(
                                new EnemyActionEvaluationContext(state, summoned, enemyIndex),
                                session.Random.Get("enemy-ai"));
                        summoned.SelectAction(summoned.Definition.Actions[summoned.ActionIndex].Id);
                    }
                    break;
                case EmitGameEventEffect emit:
                    state.Emit(session, emit.Type, emit.Source, emit.Target, emit.Amount);
                    break;
                case CardEnteredCombatEffect entry:
                    CombatCardEvents.Enter(session, entry.InstanceId);
                    break;
                case CardAfterAutoPostPlayEffect postPlay:
                    if (state.FindCombatCard(postPlay.InstanceId) is { } postPlayCard
                        && state.Definition.Cards[postPlayCard.CardId].ResolveAfterAutoPostPlayEffect(postPlayCard) is { } postPlayEffect)
                        state.PendingEffects.Add(new(state.Player.Creature, postPlayEffect, null, postPlayCard));
                    break;
                case CardListenerEffect listener:
                    CombatCardEvents.ExecuteListener(session, listener, target);
                    break;
                case CardPlayPhaseEffect phase:
                    CombatCardPlay.Continue(session, phase);
                    break;
                case BeginAutoCardEffect autoCard:
                    CombatCardPlay.QueueAutomatic(session, autoCard.Card, autoCard.ForceExhaust);
                    break;
                case PowerEventEffect powerEvent:
                    if (owner.PowerAmount(powerEvent.Power) > 0)
                    {
                        var eventCard = powerEvent.EventCardInstanceId is { } eventCardId
                            ? state.FindCombatCard(eventCardId) : selectedCard;
                        state.PendingEffects.Add(new(owner, powerEvent.Effect, target, eventCard, counts));
                    }
                    break;
                case PlaceGeneratedCardEffect generatedCard:
                    CombatCardGeneration.Place(session, generatedCard);
                    break;
                case ExpireCardCostEffect costExpiry:
                    state.ExpireCardCosts(session, costExpiry.Boundary, costExpiry.InstanceId);
                    break;
                case GenerateCombatCardsEffect generation:
                    var generatedCount = generation.CalculatedCount is { } calculatedGeneration
                        ? ScaledAmount(state, owner, target, calculatedGeneration.BaseAmount,
                            calculatedGeneration.PerCount, calculatedGeneration.Count, counts, selectedCard) : generation.Count;
                    CombatCardGeneration.Queue(session, owner, generation, generatedCount);
                    break;
                case AutoPlayCardsEffect autoPlay:
                    ReserveAutoPlayBatch(session, owner, target, autoPlay, counts, selectedCard);
                    break;
                case AutoPlayRandomCardsEffect randomAutoPlay:
                    var randomAutoPlayCount = randomAutoPlay.CalculatedCount is { } calculatedAutoPlay
                        ? ScaledAmount(state, owner, target, calculatedAutoPlay.BaseAmount,
                            calculatedAutoPlay.PerCount, calculatedAutoPlay.Count, counts, selectedCard)
                        : randomAutoPlay.Count;
                    if (randomAutoPlayCount < 0)
                        throw new InvalidOperationException("Automatic play count cannot be negative.");
                    QueueRandomAutoPlay(session, owner, target, randomAutoPlay, randomAutoPlayCount,
                        selectedCard, counts);
                    break;
                case AutoPlayRandomCardsContinuationEffect randomAutoPlay:
                    QueueRandomAutoPlay(session, owner, target, randomAutoPlay.Effect, randomAutoPlay.Remaining,
                        selectedCard, counts);
                    break;
                case AutoPlaySelectedCardEffect selectedAutoPlay:
                    QueueSelectedAutoPlay(session, owner, selectedCard, selectedAutoPlay);
                    break;
                case SequenceEffect sequence:
                    for (var i = sequence.Effects.Count - 1; i >= 0; i--)
                        state.PendingEffects.Add(new(owner, sequence.Effects[i], target, selectedCard, counts));
                    break;
                case AttackEffect attack:
                    if (attack.Target == TargetSelector.AllOpponents)
                    {
                        if (owner.IsEnemy) ApplyAttack(session, owner, state.Player.Creature, attack.Amount,
                            attack.Properties, selectedCard, attack.OnFatal);
                        else
                            foreach (var enemy in state.Enemies)
                                if (enemy.Creature.Hp > 0)
                                    ApplyAttack(session, owner, enemy.Creature, attack.Amount,
                                        attack.Properties, selectedCard, attack.OnFatal);
                    }
                    else ApplyAttack(session, owner, ResolveSingleTarget(state, owner, target, attack.Target),
                        attack.Amount, attack.Properties, selectedCard, attack.OnFatal);
                    break;
                case AttackCardVariableEffect dynamicAttack:
                    if (dynamicAttack.HitCount < 0) throw new InvalidOperationException("Dynamic attack hit count cannot be negative.");
                    var damageCard = CombatCardVariables.ResolveCard(state, CardContext.Played, selectedCard);
                    var dynamicAmount = CombatCardVariables.Read(state, dynamicAttack.Variable,
                        CardContext.Played, selectedCard);
                    for (var hit = 0; hit < dynamicAttack.HitCount; hit++)
                    {
                        var victims = dynamicAttack.Target == TargetSelector.AllOpponents
                            ? owner.IsEnemy ? new[] { state.Player.Creature }
                                : state.Enemies.Select(enemy => enemy.Creature).Where(enemy => enemy.Hp > 0).ToArray()
                            : new[] { ResolveSingleTarget(state, owner, target, dynamicAttack.Target) };
                        foreach (var victim in victims)
                            ApplyAttack(session, owner, victim, dynamicAmount, dynamicAttack.Properties, damageCard);
                    }
                    break;
                case ScaledAttackEffect scaledAttack:
                    var scaledVictims = scaledAttack.Target == TargetSelector.AllOpponents
                        ? owner.IsEnemy
                            ? new[] { state.Player.Creature }
                            : state.Enemies.Select(enemy => enemy.Creature).Where(enemy => enemy.Hp > 0).ToArray()
                        : new[] { ResolveSingleTarget(state, owner, target, scaledAttack.Target) };
                    foreach (var scaledVictim in scaledVictims)
                        ApplyAttack(session, owner, scaledVictim,
                            ScaledAmount(state, owner, scaledVictim, scaledAttack.BaseAmount,
                                scaledAttack.AmountPerCount, scaledAttack.Count, counts, selectedCard),
                            scaledAttack.Properties, selectedCard, scaledAttack.OnFatal);
                    break;
                case RandomAttackEffect randomAttack:
                    var randomAmount = randomAttack.CalculatedAmount is { } calculatedRandomAttack
                        ? ScaledAmount(state, owner, target, calculatedRandomAttack.BaseAmount,
                            calculatedRandomAttack.PerCount, calculatedRandomAttack.Count, counts, selectedCard)
                        : randomAttack.Amount;
                    if (randomAmount < 0) throw new InvalidOperationException("Attack damage cannot be negative.");
                    var opponents = owner.IsEnemy
                        ? state.Player.Creature.Hp > 0 ? new[] { state.Player.Creature } : []
                        : state.Enemies.Select(enemy => enemy.Creature).Where(enemy => enemy.Hp > 0).ToArray();
                    if (opponents.Length > 0)
                    {
                        var victim = opponents[session.Random.Get("card-targets").NextInt(opponents.Length)];
                        state.Emit(session, "random_attack_target", owner.Id, victim.Id, randomAmount);
                        ApplyAttack(session, owner, victim, randomAmount, randomAttack.Properties, selectedCard);
                    }
                    break;
                case AttackUsingBlockEffect attackUsingBlock:
                    var blockBasedDamage = owner.Block;
                    if (attackUsingBlock.Target == TargetSelector.AllOpponents)
                    {
                        if (owner.IsEnemy) ApplyAttack(session, owner, state.Player.Creature, blockBasedDamage,
                            DamageProperties.Move, selectedCard);
                        else
                            foreach (var enemy in state.Enemies)
                                if (enemy.Creature.Hp > 0)
                                    ApplyAttack(session, owner, enemy.Creature, blockBasedDamage,
                                        DamageProperties.Move, selectedCard);
                    }
                    else
                        ApplyAttack(session, owner,
                            ResolveSingleTarget(state, owner, target, attackUsingBlock.Target), blockBasedDamage,
                            DamageProperties.Move, selectedCard);
                    break;
                case LoseHpEffect loseHp:
                    if (loseHp.Amount < 0) throw new InvalidOperationException("HP loss cannot be negative.");
                    var hpLossTarget = ResolveSingleTarget(state, owner, target, loseHp.Target);
                    var wasAliveBeforeHpLoss = hpLossTarget.Hp > 0;
                    var hpLost = Math.Min(hpLossTarget.Hp, loseHp.Amount);
                    hpLossTarget.Hp -= hpLost;
                    state.Emit(session, "hp_loss", owner.Id, hpLossTarget.Id, hpLost,
                        loseHp.Properties, selectedCard);
                    if (wasAliveBeforeHpLoss && hpLossTarget.Hp == 0)
                        CompleteDeath(session, hpLossTarget, owner, selectedCard, loseHp.Properties);
                    break;
                case HealEffect heal:
                    if (heal.Amount < 0) throw new InvalidOperationException("Healing cannot be negative.");
                    state.Heal(session, owner, ResolveSingleTarget(state, owner, target, heal.Target), heal.Amount);
                    break;
                case GainBlockEffect block:
                    var blockAmount = block.PerPowerStack is { } perPowerStack
                        ? block.Amount * owner.PowerAmount(perPowerStack)
                        : block.Amount;
                    var blockProperties = block.Properties ?? (selectedCard is not null || owner.IsEnemy
                        ? DamageProperties.Move : DamageProperties.None);
                    foreach (var recipient in ResolveMultipleTargets(state, owner, target, block.Target))
                    {
                        if (block.OnlyEnemy is { } onlyEnemy && !state.Enemies.Any(enemy =>
                            enemy.Creature == recipient && enemy.Definition.Id == onlyEnemy)) continue;
                        if (recipient.Hp <= 0) continue;
                        var recipientBlock = state.ModifyBlockGain(recipient, blockAmount, blockProperties);
                        recipient.Block += recipientBlock;
                        state.Emit(session, "block", owner.Id, recipient.Id, recipientBlock,
                            properties: blockProperties,
                            cardSource: selectedCard);
                    }
                    break;
                case GainBlockFromCurrentBlockEffect blockFromBlock:
                    ArgumentOutOfRangeException.ThrowIfNegative(blockFromBlock.Multiplier);
                    var gainedBlock = owner.Block * blockFromBlock.Multiplier;
                    var currentBlockProperties = selectedCard is not null || owner.IsEnemy
                        ? DamageProperties.Move : DamageProperties.None;
                    foreach (var recipient in ResolveMultipleTargets(state, owner, target, blockFromBlock.Target))
                    {
                        if (recipient.Hp <= 0) continue;
                        var recipientBlock = state.ModifyBlockGain(recipient, gainedBlock, currentBlockProperties);
                        recipient.Block += recipientBlock;
                        state.Emit(session, "block", owner.Id, recipient.Id, recipientBlock,
                            properties: currentBlockProperties,
                            cardSource: selectedCard);
                    }
                    break;
                case ApplyPowerEffect power:
                    foreach (var recipient in ResolveMultipleTargets(state, owner, target, power.Target))
                        state.ApplyPower(session, recipient, power.Power, power.Amount, owner);
                    break;
                case ApplyScaledPowerEffect scaledPower:
                    var powerAmount = ScaledAmount(state, owner, target, scaledPower.BaseAmount,
                        scaledPower.AmountPerCount, scaledPower.Count, counts, selectedCard);
                    if (powerAmount != 0)
                        state.ApplyPower(session, ResolveSingleTarget(state, owner, target, scaledPower.Target),
                            scaledPower.Power, powerAmount, owner);
                    break;
                case DrawCardsEffect draw:
                    var drawAmount = draw.CalculatedAmount is { } calculatedDraw
                        ? ScaledAmount(state, owner, target, calculatedDraw.BaseAmount,
                            calculatedDraw.PerCount, calculatedDraw.Count, counts, selectedCard)
                        : draw.Amount;
                    if (drawAmount < 0) throw new InvalidOperationException("Draw count cannot be negative.");
                    if (drawAmount > 0)
                        QueueNextDraw(session, owner, drawAmount);
                    break;
                case DrawCardsContinuationEffect draw:
                    if (draw.Remaining > 0)
                        QueueNextDraw(session, owner, draw.Remaining);
                    break;
                case DrawUntilCardTypeEffect drawUntil:
                    QueueNextDrawUntil(session, owner, drawUntil.ContinueWhileType, target, selectedCard, counts);
                    break;
                case DrawUntilContinuationEffect continueDraw:
                    if (continueDraw.LastDrawnType == continueDraw.ContinueWhileType
                        && state.Player.HandInstances.Count < CombatLimits.MaxHandSize)
                        QueueNextDrawUntil(session, owner, continueDraw.ContinueWhileType,
                            target, selectedCard, counts);
                    break;
                case MoveCardEffect move:
                    var movedCards = state.MoveCards(move.From, move.To, move.Count);
                    var moveTriggerEffects = new List<(CardInstance Card, EffectNode Effect)>();
                    foreach (var movedCard in movedCards)
                    {
                        state.Emit(session, "card_moved", owner.Id, movedCard.InstanceId, 1);
                        if (move.To == CardPile.Exhaust && move.From != CardPile.Exhaust)
                        {
                            state.Emit(session, "card_exhausted", state.Player.Id, movedCard.InstanceId, 1);
                            foreach (var triggered in BuildExhaustTriggerEffects(state, state.Player.Creature, movedCard))
                                moveTriggerEffects.Add((movedCard, triggered));
                        }
                    }
                    for (var i = moveTriggerEffects.Count - 1; i >= 0; i--)
                        state.PendingEffects.Add(new(state.Player.Creature, moveTriggerEffects[i].Effect,
                            null, moveTriggerEffects[i].Card));
                    break;
                case AddCardsToPileEffect addCards:
                    if (addCards.Count < 0) throw new InvalidOperationException("Added card count cannot be negative.");
                    var cardOwner = ResolveSingleTarget(state, owner, target, addCards.Target);
                    if (!ReferenceEquals(cardOwner, state.Player.Creature))
                        throw new InvalidOperationException("Combat card piles are only modeled for the player.");
                    var playedFrame = state.CurrentCardPlay;
                    var upgradeLevel = addCards.InheritUpgradeFromPlayedCard && playedFrame is not null
                        ? state.CardInPlay(playedFrame).UpgradeLevel : 0;
                    var addedInstances = Enumerable.Range(0, addCards.Count)
                        .Select(_ => state.Player.CreateGeneratedCard(addCards.Card, upgradeLevel)).ToArray();
                    state.PendingEffects.Add(new(owner,
                        new EmitGameEventEffect("cards_added", owner.Id, cardOwner.Id, addCards.Count), target, selectedCard, counts));
                    CombatCardGeneration.QueuePlacements(state, addedInstances, addCards.Pile);
                    break;
                case ChooseCardFromPileEffect chooseCard:
                    var options = state.GetPile(chooseCard.From)
                        .Where(card => state.MatchesCardSelectionFilter(card, chooseCard.Filter)).ToArray();
                    if (options.Length > 0)
                        state.PendingCardSelection = new PendingCardChoice(chooseCard, options, owner, target, selectedCard) { Counts = counts };
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
                            owner, target, selectedCard, maximum) { Counts = counts };
                    }
                    break;
                case RandomlyMoveCardFromPileEffect randomCard:
                    var randomOptions = state.GetPile(randomCard.From)
                        .Where(card => state.MatchesCardSelectionFilter(card, randomCard.Filter)).ToArray();
                    if (randomOptions.Length > 0)
                    {
                        var randomOption = randomOptions[session.Random.Get("card-selection").NextInt(randomOptions.Length)];
                        var afterRandomMoveIndex = state.PendingEffects.Count;
                        if (!state.MoveCard(randomCard.From, randomCard.To, randomOption.InstanceId, CardPilePosition.Bottom))
                            throw new InvalidOperationException("The randomly selected card is no longer in its source pile.");
                        state.Emit(session, "random_card_choice", owner.Id, randomOption.InstanceId, 1);
                        state.PendingEffects.Insert(afterRandomMoveIndex, new(owner,
                            new RandomMoveSingleAfterEffect(randomCard.From, randomCard.To,
                                randomCard.AfterMove, target, randomOption, counts), target, randomOption, counts));
                    }
                    break;
                case RandomMoveSingleAfterEffect singleMove:
                    if (singleMove.To == CardPile.Exhaust && singleMove.From != CardPile.Exhaust
                        && singleMove.Card is { } exhaustedCard)
                        QueueCardExhaustion(session, state.Player.Creature, exhaustedCard);
                    if (singleMove.AfterMove is { } afterSingleMove && singleMove.Card is { } selectedMoveCard)
                        state.PendingEffects.Add(new(owner, afterSingleMove, singleMove.Target,
                            selectedMoveCard, singleMove.Counts));
                    break;
                case RandomlyMoveCardsEffect randomCards:
                    var randomMoveCount = randomCards.CalculatedCount is { } calculatedMoveCount
                        ? ScaledAmount(state, owner, target, calculatedMoveCount.BaseAmount,
                            calculatedMoveCount.PerCount, calculatedMoveCount.Count, counts, selectedCard)
                        : randomCards.Count;
                    if (randomMoveCount < 0) throw new InvalidOperationException("Random move count cannot be negative.");
                    var moveOptions = state.GetPile(randomCards.From)
                        .Where(card => state.MatchesCardSelectionFilter(card, randomCards.Filter)).ToList();
                    session.Random.Get("card-selection").Shuffle(moveOptions);
                    var selectedForMove = moveOptions.Take(randomMoveCount).ToArray();
                    if (selectedForMove.Length > 0)
                        state.PendingEffects.Add(new(owner,
                            new RandomMoveCardsBatchEffect(randomCards.From, randomCards.To, selectedForMove,
                                0, randomCards.Filter, randomCards.AfterMove, target, selectedCard, counts),
                            target, selectedCard, counts));
                    break;
                case RandomMoveCardsBatchEffect batch:
                    var cardToMove = batch.Cards[batch.Index];
                    var afterMoveIndex = state.PendingEffects.Count;
                    if (!state.MoveCard(batch.From, batch.To, cardToMove.InstanceId, CardPilePosition.Bottom))
                        throw new InvalidOperationException("A randomly selected card is no longer in its source pile.");
                    state.Emit(session, "random_card_choice", owner.Id, cardToMove.InstanceId, 1);
                    state.PendingEffects.Insert(afterMoveIndex, new(owner,
                        new RandomMoveCardsAfterEffect(batch.From, batch.To, batch.Cards, batch.Index,
                            batch.Filter, batch.AfterMove, batch.Target, batch.ParentSelectedCard,
                            batch.Counts, cardToMove), batch.Target, batch.ParentSelectedCard, batch.Counts));
                    break;
                case RandomMoveCardsAfterEffect moved:
                    if (moved.Index + 1 < moved.Cards.Count)
                        state.PendingEffects.Add(new(owner,
                            new RandomMoveCardsBatchEffect(moved.From, moved.To, moved.Cards,
                                moved.Index + 1, moved.Filter, moved.AfterMove, moved.Target,
                                moved.ParentSelectedCard, moved.Counts), moved.Target,
                            moved.ParentSelectedCard, moved.Counts));
                    if (moved.To == CardPile.Exhaust && moved.From != CardPile.Exhaust)
                        QueueCardExhaustion(session, state.Player.Creature, moved.MovedCard);
                    if (moved.AfterMove is { } afterMove)
                        state.PendingEffects.Add(new(owner, afterMove, moved.Target, moved.MovedCard, moved.Counts));
                    break;
                case ForEachCardInPileEffect eachCard:
                    var matchingCards = state.GetPile(eachCard.From)
                        .Where(card => state.MatchesCardSelectionFilter(card, eachCard.Filter)).ToArray();
                    for (var i = matchingCards.Length - 1; i >= 0; i--)
                        state.PendingEffects.Add(new(owner, eachCard.Effect, target, matchingCards[i], counts));
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
                case ModifySelectedCardCostEffect cost:
                    if (selectedCard is null)
                        throw new InvalidOperationException("A local cost modifier requires a selected card.");
                    var modifier = cost.CalculatedAmount is { } calculatedCost
                        ? cost.Modifier with { Amount = ScaledAmount(state, owner, target, calculatedCost.BaseAmount,
                            calculatedCost.PerCount, calculatedCost.Count, counts, selectedCard) } : cost.Modifier;
                    state.ModifyCardCost(session, selectedCard.InstanceId, modifier);
                    break;
                case ModifyCardVariableEffect variable:
                    var variableAmount = variable.CalculatedAmount is { } calculatedVariable
                        ? ScaledAmount(state, owner, target, calculatedVariable.BaseAmount,
                            calculatedVariable.PerCount, calculatedVariable.Count, counts, selectedCard) : variable.Amount;
                    CombatCardVariables.Modify(session, CombatCardVariables.ResolveCard(state, variable.Card, selectedCard),
                        variable.Variable, variable.Operation, variableAmount);
                    break;
                case AccumulateCardDamageEffect accumulateDamage:
                    var damageSource = CombatCardVariables.ResolveCard(state, accumulateDamage.SourceCard, selectedCard);
                    var damageTarget = CombatCardVariables.ResolveCard(state, accumulateDamage.TargetCard, selectedCard);
                    var calculatedCardDamage = ResolveCardDamageValue(session, owner, damageSource);
                    CombatCardVariables.Modify(session, damageTarget, accumulateDamage.Variable,
                        CardVariableOperation.Add, calculatedCardDamage);
                    break;
                case ModifyPowerVariableEffect variable:
                    var variableOwner = ResolveSingleTarget(state, owner, target, variable.Target);
                    variableOwner.ModifyPowerVariable(variable.Power, variable.Variable, variable.Amount, variable.Operation);
                    state.Emit(session, "power_variable_modified", owner.Id, variableOwner.Id, variable.Amount,
                        affectedPower: variable.Power);
                    break;
                case ModifyEnemyVariableEffect variable:
                    var actingEnemyForVariable = state.Enemies.FirstOrDefault(enemy => enemy.Creature.Id == owner.Id)
                        ?? throw new InvalidOperationException("Modifying an enemy variable requires an enemy action owner.");
                    var oldEnemyVariable = actingEnemyForVariable.GetVariable(variable.Variable);
                    actingEnemyForVariable.SetVariable(variable.Variable, variable.Operation switch
                    {
                        PowerVariableOperation.Add => checked(oldEnemyVariable + variable.Amount),
                        PowerVariableOperation.Set => variable.Amount,
                        _ => throw new ArgumentOutOfRangeException(nameof(variable.Operation))
                    });
                    state.Emit(session, "enemy_variable_modified", owner.Id, owner.Id,
                        variable.Operation == PowerVariableOperation.Add ? variable.Amount
                            : actingEnemyForVariable.GetVariable(variable.Variable) - oldEnemyVariable,
                        exactAmount: actingEnemyForVariable.GetVariable(variable.Variable),
                        affectedVariable: variable.Variable);
                    break;
                case ModifyCombatStateEffect variable:
                    var oldStateValue = state.ReadCombatState(variable.Variable);
                    var newStateValue = variable.Operation switch
                    {
                        PowerVariableOperation.Add => checked(oldStateValue + variable.Amount),
                        PowerVariableOperation.Set => variable.Amount,
                        _ => throw new InvalidOperationException($"Invalid combat state operation '{variable.Operation}'.")
                    };
                    state.SetCombatState(variable.Variable, newStateValue);
                    state.Emit(session, "combat_state_modified", owner.Id, owner.Id,
                        variable.Operation == PowerVariableOperation.Add ? variable.Amount : newStateValue - oldStateValue,
                        exactAmount: newStateValue, affectedVariable: variable.Variable);
                    break;
                case GainMaxHpEffect gainMaxHp:
                    state.GainMaxHp(session, gainMaxHp.Amount);
                    break;
                case LoseMaxHpEffect loseMaxHp:
                    if (loseMaxHp.Amount < 0) throw new InvalidOperationException("Maximum HP loss cannot be negative.");
                    LoseMaxHp(session, owner, ResolveSingleTarget(state, owner, target, loseMaxHp.Target), loseMaxHp.Amount);
                    break;
                case DowngradeSelectedCardEffect:
                    CombatCardVariables.Downgrade(session, CombatCardVariables.ResolveCard(state, CardContext.Selected, selectedCard));
                    break;
                case CloneCardEffect clone:
                    var cloneSource = CombatCardVariables.ResolveCard(state, clone.Card, selectedCard);
                    var cloneCount = clone.CalculatedCount is { } calculatedClone
                        ? ScaledAmount(state, owner, target, calculatedClone.BaseAmount, calculatedClone.PerCount,
                            calculatedClone.Count, counts, selectedCard) : clone.Count;
                    if (cloneCount < 0) throw new InvalidOperationException("Combat clone count cannot be negative.");
                    var clones = new CardInstance[cloneCount];
                    for (var i = 0; i < clones.Length; i++)
                    {
                        var fresh = state.Player.CreateGeneratedCard(cloneSource.CardId);
                        clones[i] = new(fresh.InstanceId, cloneSource.CardId, cloneSource.UpgradeLevel,
                            cloneSource.CostModifiers, isClone: true, cloneSource.VariableOffsets);
                    }
                    CombatCardGeneration.QueuePlacements(state, clones, clone.Pile);
                    break;
                case TransformSelectedCardEffect transformation:
                    if (selectedCard is null)
                        throw new InvalidOperationException("Transformation requires a selected card.");
                    state.TransformCard(session, selectedCard.InstanceId, transformation);
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
                    var stackDelta = state.DecrementPower(session, owner, decrementPower.Power);
                    if (stackDelta != 0)
                        state.Emit(session, "power", owner.Id, owner.Id, stackDelta,
                            affectedPower: decrementPower.Power);
                    break;
                case RemovePowerEffect removePower:
                    var removedAmount = state.RemovePower(owner, removePower.Power);
                    if (removedAmount != 0)
                        state.Emit(session, "power_removed", owner.Id, owner.Id, removedAmount,
                            affectedPower: removePower.Power);
                    break;
                case SetEnemyActionEffect setEnemyAction:
                    var actingEnemy = state.Enemies.FirstOrDefault(enemy => enemy.Creature.Id == owner.Id)
                        ?? throw new InvalidOperationException("Setting an enemy action requires an enemy Power owner.");
                    var actionIndex = actingEnemy.Definition.GetActionIndex(setEnemyAction.Action);
                    actingEnemy.ActionIndex = actionIndex;
                    actingEnemy.SelectAction(setEnemyAction.Action);
                    break;
                case ExpirePowerEffect expirePower:
                    state.ExpirePower(session, owner, expirePower.Power);
                    break;
                case ModifyEnergyEffect energy:
                    var requestedEnergy = energy.CalculatedAmount is { } calculatedEnergy
                        ? ScaledAmount(state, owner, target, calculatedEnergy.BaseAmount,
                            calculatedEnergy.PerCount, calculatedEnergy.Count, counts, selectedCard) : energy.Amount;
                    var energyDelta = requestedEnergy > 0 && state.Player.Creature.Powers.Any(power =>
                        power.Value > 0 && state.Definition.Powers[power.Key].PreventEnergyGain) ? 0 : requestedEnergy;
                    var previousEnergy = state.Player.Energy;
                    state.Player.Energy = Math.Max(0, checked(previousEnergy + energyDelta));
                    state.Emit(session, "energy", state.Player.Id, state.Player.Id, state.Player.Energy - previousEnergy);
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
                    state.PendingChoiceCounts = counts;
                    break;
                case IfEffect conditional:
                    var condition = conditional.Predicate switch
                    {
                        TargetAlivePredicate => (target ?? OpponentOf(state, owner)).Hp > 0,
                        HasPowerPredicate hasPower => ResolveSingleTarget(state, owner, target, hasPower.Target)
                            .PowerAmount(hasPower.Power) > 0,
                        EnergyAtLeastPredicate energy => state.Player.Energy >= energy.Amount,
                        CardExhaustedThisTurnPredicate => state.LastPlayerCardExhaustTurn == state.Turn,
                        PlayerLostHpThisTurnPredicate => state.LastPlayerHpLossTurn == state.Turn,
                        SelectedCardIsClonePredicate => selectedCard?.IsClone
                            ?? throw new InvalidOperationException("Clone predicates require a selected card context."),
                        PileCountAtLeastPredicate pile => state.GetPile(pile.Pile).Count >= pile.Count,
                        CardPlayCountEqualsPredicate plays => state.CardPlayHistory.Count(plays.Type, plays.Phase) == plays.Count,
                        _ => throw new InvalidOperationException($"Unsupported effect predicate '{conditional.Predicate.GetType().Name}'.")
                    };
                    if (condition) state.PendingEffects.Add(new(owner, conditional.Then, target, selectedCard, counts));
                    else if (conditional.Else is not null) state.PendingEffects.Add(new(owner, conditional.Else, target, selectedCard, counts));
                    break;
                case RepeatEffect repeat:
                    if (repeat.Count < 0) throw new InvalidOperationException("Repeat count cannot be negative.");
                    for (var i = 0; i < repeat.Count; i++) state.PendingEffects.Add(new(owner, repeat.Effect, target, selectedCard, counts));
                    break;
                case WithCountEffect binding:
                    var capturedCount = ScaledAmount(state, owner, target, 0, 1, binding.Count, counts, selectedCard);
                    state.PendingEffects.Add(new(owner, binding.Effect, target, selectedCard,
                        new EffectCountScope(binding.Name, capturedCount, counts)));
                    break;
                case RepeatForCountEffect repeat:
                    var repeatCount = ScaledAmount(state, owner, target, repeat.BaseCount, repeat.PerCount, repeat.Count, counts, selectedCard);
                    if (repeatCount < 0) throw new InvalidOperationException("Repeat count cannot be negative.");
                    for (var i = 0; i < repeatCount; i++) state.PendingEffects.Add(new(owner, repeat.Effect, target, selectedCard, counts));
                    break;
                case RepeatForXCostEffect repeatForX:
                    for (var i = 0; i < (state.CurrentCardPlay?.XEnergy ?? 0); i++)
                        state.PendingEffects.Add(new(owner, repeatForX.Effect, target, selectedCard, counts));
                    break;
                case CustomEffect custom:
                    if (!state.Definition.CustomEffects.TryGetValue(custom.Handler, out var handler))
                        throw new InvalidOperationException($"Unknown custom effect handler '{custom.Handler}'.");
                    handler.Execute(new EffectExecutionContext(session, owner, target, handler.Id, counts, selectedCard), custom.Payload);
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

    private static IEnumerable<EntityState> ResolveMultipleTargets(GameState state, EntityState owner,
        EntityState? selected, TargetSelector selector) => selector switch
    {
        TargetSelector.Self => [owner],
        TargetSelector.FirstEnemy => [OpponentOf(state, owner)],
        TargetSelector.SelectedTarget => [selected ?? OpponentOf(state, owner)],
        TargetSelector.AllOpponents => owner.IsEnemy
            ? [state.Player.Creature]
            : state.Enemies.Where(enemy => enemy.Creature.Hp > 0).Select(enemy => enemy.Creature),
        TargetSelector.OtherAllies => owner.IsEnemy
            ? state.Enemies.Where(enemy => enemy.Creature.Hp > 0 && enemy.Creature != owner)
                .Select(enemy => enemy.Creature)
            : Array.Empty<EntityState>(),
        _ => throw new ArgumentOutOfRangeException(nameof(selector), "This effect has an invalid target selector.")
    };

    private static int ScaledAmount(GameState state, EntityState owner, EntityState? selectedTarget, int baseAmount,
        int amountPerCount, CombatCountSource source, EffectCountScope? counts = null, CardInstance? selectedCard = null,
        CardInstance? playedCardOverride = null)
    {
        var count = ResolveCombatCount(state, owner, selectedTarget, source, counts, selectedCard, playedCardOverride);
        return checked(baseAmount + amountPerCount * count);
    }

    private static decimal ResolveCardDamageValue(GameSession session, EntityState owner, CardInstance card)
    {
        var state = session.State;
        var definition = state.Definition.Cards[card.CardId];
        decimal baseDamage;
        if (definition.DamageValueVariable is { } variable)
            baseDamage = CombatCardVariables.Read(state, variable, CardContext.Played, card, card);
        else if (card.UpgradeLevel > 0 && definition.UpgradedDamageValue is { } upgradedDamage)
            baseDamage = upgradedDamage;
        else if (card.UpgradeLevel == 0 && definition.DamageValue is { } normalDamage)
            baseDamage = normalDamage;
        else if (card.UpgradeLevel > 0 && definition.UpgradedCalculatedDamageValue is { } upgradedCalculated)
            baseDamage = ScaledAmount(state, owner, null, upgradedCalculated.BaseAmount,
                upgradedCalculated.PerCount, upgradedCalculated.Count, null, card, card);
        else if (definition.CalculatedDamageValue is { } calculated)
            baseDamage = ScaledAmount(state, owner, null, calculated.BaseAmount,
                calculated.PerCount, calculated.Count, null, card, card);
        else
            baseDamage = FindCardDamageValue(state, owner, definition.ResolveEffect(card), card, null) ?? 0m;
        return state.ModifyCardDamageValue(session, owner, baseDamage, DamageProperties.Move);
    }

    private static decimal? FindCardDamageValue(GameState state, EntityState owner, EffectNode effect,
        CardInstance card, EffectCountScope? counts)
    {
        switch (effect)
        {
            case AttackCardVariableEffect dynamicAttack:
                return CombatCardVariables.Read(state, dynamicAttack.Variable, CardContext.Played, card, card);
            case AttackEffect attack:
                return attack.Amount;
            case ScaledAttackEffect scaled:
                return ScaledAmount(state, owner, null, scaled.BaseAmount, scaled.AmountPerCount,
                    scaled.Count, counts, card, card);
            case RandomAttackEffect random when random.CalculatedAmount is { } calculated:
                return ScaledAmount(state, owner, null, calculated.BaseAmount, calculated.PerCount,
                    calculated.Count, counts, card, card);
            case RandomAttackEffect random:
                return random.Amount;
            case AttackUsingBlockEffect:
                return owner.Block;
            case SequenceEffect sequence:
                foreach (var child in sequence.Effects)
                    if (FindCardDamageValue(state, owner, child, card, counts) is { } damage) return damage;
                return null;
            case RepeatEffect repeat:
                return FindCardDamageValue(state, owner, repeat.Effect, card, counts);
            case RepeatForCountEffect repeat:
                return FindCardDamageValue(state, owner, repeat.Effect, card, counts);
            case RepeatForXCostEffect repeat:
                return FindCardDamageValue(state, owner, repeat.Effect, card, counts);
            case WithCountEffect binding:
                var captured = ResolveCombatCount(state, owner, null, binding.Count, counts, card, card);
                return FindCardDamageValue(state, owner, binding.Effect, card,
                    new EffectCountScope(binding.Name, captured, counts));
            case IfEffect conditional:
                return FindCardDamageValue(state, owner, conditional.Then, card, counts)
                    ?? (conditional.Else is { } elseEffect
                        ? FindCardDamageValue(state, owner, elseEffect, card, counts) : null);
            default:
                return null;
        }
    }

    private static void QueueNextDrawUntil(GameSession session, EntityState owner, CardType continueWhileType,
        EntityState? target, CardInstance? selectedCard, EffectCountScope? counts)
    {
        var state = session.State;
        var continuationIndex = state.PendingEffects.Count;
        var drawnCard = state.DrawOne(session);
        if (drawnCard is null) return;
        var drawnType = state.Definition.Cards[drawnCard.CardId].Type;
        state.PendingEffects.Insert(continuationIndex, new(owner,
            new DrawUntilContinuationEffect(continueWhileType, drawnType), target, selectedCard, counts));
    }

    private static void QueueNextDraw(GameSession session, EntityState owner, int remaining)
    {
        var state = session.State;
        var continuationIndex = state.PendingEffects.Count;
        var drawnCard = state.DrawOne(session);
        if (drawnCard is null) return;
        state.PendingEffects.Insert(continuationIndex,
            new(owner, new DrawCardsContinuationEffect(remaining - 1), null, null));
    }

    private static int ResolveCombatCount(GameState state, EntityState owner, EntityState? selectedTarget,
        CombatCountSource source, EffectCountScope? counts, CardInstance? selectedCard,
        CardInstance? playedCardOverride = null)
    {
        return source switch
        {
            PileCardCountSource pile => pile.Type is { } type
                ? state.GetPile(pile.Pile).Count(card => state.Definition.Cards[card.CardId].Type == type)
                : state.GetPile(pile.Pile).Count,
            PlayerHpLossCountSource => state.PlayerHpLossCount,
            EnergyXCountSource => state.CurrentCardPlay?.XEnergy ?? 0,
            CardPlaysThisTurnCountSource plays => state.CardPlayHistory.Count(plays.Type, plays.Phase),
            CardVariableCountSource variable => decimal.ToInt32(decimal.Floor(
                CombatCardVariables.Read(state, variable.Variable, variable.Card, selectedCard, playedCardOverride))),
            PowerVariableCountSource variable => ResolveSingleTarget(state, owner, selectedTarget, variable.Target)
                .PowerVariable(variable.Power, variable.Variable),
            TakePowerEventAmountCountSource deferred => owner.TakePowerEventAmount(deferred.Power,
                CombatCardVariables.ResolveCard(state, deferred.Card, selectedCard).InstanceId),
            TakePowerTurnEventAmountCountSource deferred => owner.TakePowerEventAmount(deferred.Power, null),
            BlockCountSource block => ResolveSingleTarget(state, owner, selectedTarget, block.Target).Block,
            SavedCountSource saved => counts?.Resolve(saved.Name)
                ?? throw new InvalidOperationException($"Count '{saved.Name}' is not bound in this effect scope."),
            TargetPowerCountSource power => ResolveSingleTarget(state, owner, selectedTarget, power.Target).PowerAmount(power.Power),
            TaggedCardCountSource tag => AllCardPiles.Sum(pile => state.GetPile(pile)
                .Count(card => state.Definition.Cards[card.CardId].Tags.Contains(tag.Tag))),
            ProductCombatCountSource product => checked(
                ResolveCombatCount(state, owner, selectedTarget, product.Left, counts, selectedCard, playedCardOverride)
                * ResolveCombatCount(state, owner, selectedTarget, product.Right, counts, selectedCard, playedCardOverride)),
            _ => throw new InvalidOperationException("Unsupported combat count source.")
        };
    }

    private static void ReserveAutoPlayBatch(GameSession session, EntityState owner, EntityState? target,
        AutoPlayCardsEffect effect, EffectCountScope? counts, CardInstance? selectedCard)
    {
        var state = session.State;
        if (owner != state.Player.Creature || effect.From != CardPile.Draw || !Enum.IsDefined(effect.Position))
            throw new InvalidOperationException("Automatic play requires the player's draw pile and a supported position.");
        if (owner.Hp <= 0 || state.FirstLivingEnemy is null) return;
        var count = effect.CalculatedCount is { } calculated
            ? ScaledAmount(state, owner, target, calculated.BaseAmount, calculated.PerCount, calculated.Count, counts, selectedCard)
            : effect.Count;
        if (count < 0) throw new InvalidOperationException("Automatic play count cannot be negative.");
        var reserved = new List<CardInstance>();
        for (var i = 0; i < count; i++)
        {
            state.EnsureDrawPile(session);
            var draw = state.Player.MutableDrawPile;
            if (draw.Count == 0) break;
            var index = effect.Position == CardPilePosition.Top ? 0 : draw.Count - 1;
            var card = draw[index];
            draw.RemoveAt(index);
            state.Player.MutablePlayPile.Add(card);
            reserved.Add(card);
            state.Emit(session, "card_moved", owner.Id, card.InstanceId, 1);
        }
        for (var i = reserved.Count - 1; i >= 0; i--)
            state.PendingEffects.Add(new(owner, new BeginAutoCardEffect(reserved[i], effect.ForceExhaust), null, reserved[i]));
    }

    private static void QueueRandomAutoPlay(GameSession session, EntityState owner, EntityState? target,
        AutoPlayRandomCardsEffect effect, int remaining, CardInstance? selectedCard, EffectCountScope? counts)
    {
        var state = session.State;
        if (remaining <= 0 || owner != state.Player.Creature || owner.Hp <= 0 || state.FirstLivingEnemy is null)
            return;
        if (!Enum.IsDefined(effect.From) || effect.From == CardPile.Play)
            throw new InvalidOperationException("Random automatic play requires a selectable card pile.");
        var candidates = state.GetPile(effect.From).Where(card =>
        {
            var definition = state.Definition.Cards[card.CardId];
            return (effect.Type is null || definition.Type == effect.Type)
                && (definition.ResolveKeywords(card) & CardKeywords.Unplayable) == 0;
        }).ToArray();
        if (candidates.Length == 0) return;
        var card = candidates[session.Random.Get("card-selection").NextInt(candidates.Length)];
        var source = state.GetPile(effect.From);
        var index = source.FindIndex(candidate => candidate.InstanceId == card.InstanceId);
        if (index < 0) throw new InvalidOperationException("The randomly selected card left its source pile.");
        source.RemoveAt(index);
        state.Player.MutablePlayPile.Add(card);
        state.Emit(session, "random_card_choice", owner.Id, card.InstanceId, 1);
        state.Emit(session, "card_moved", owner.Id, card.InstanceId, 1);
        if (remaining > 1)
            state.PendingEffects.Add(new(owner,
                new AutoPlayRandomCardsContinuationEffect(effect, remaining - 1), target, selectedCard, counts));
        state.PendingEffects.Add(new(owner, new BeginAutoCardEffect(card, effect.ForceExhaust), target, card));
    }

    private static void QueueSelectedAutoPlay(GameSession session, EntityState owner, CardInstance? card,
        AutoPlaySelectedCardEffect effect)
    {
        if (card is null) throw new InvalidOperationException("Automatic play requires a selected card context.");
        var state = session.State;
        if (owner != state.Player.Creature || owner.Hp <= 0 || state.FirstLivingEnemy is null) return;
        if (!Enum.IsDefined(effect.From) || effect.From == CardPile.Play)
            throw new InvalidOperationException("Automatic play requires a selectable card pile.");
        var source = state.GetPile(effect.From);
        var index = source.FindIndex(candidate => candidate.InstanceId == card.InstanceId);
        if (index < 0) return;
        source.RemoveAt(index);
        state.Player.MutablePlayPile.Add(card);
        state.Emit(session, "card_moved", owner.Id, card.InstanceId, 1);
        state.PendingEffects.Add(new(owner, new BeginAutoCardEffect(card, effect.ForceExhaust), null, card));
    }

    private static void QueueSummonedEnemies(GameSession session, EntityState owner, SummonEnemyEffect summon)
    {
        var state = session.State;
        if (!state.Definition.Enemies.TryGetValue(summon.Enemy, out var definition))
            throw new InvalidOperationException($"Unknown summoned enemy '{summon.Enemy}'.");
        var allowedCount = summon.Count;
        if (summon.MaximumLivingEnemyCount is { } maximumLiving)
            allowedCount = Math.Min(allowedCount,
                Math.Max(0, maximumLiving - state.Enemies.Count(enemy => enemy.Creature.Hp > 0)));
        if (summon.MaximumEnemyCount is { } maximumTotal)
            allowedCount = Math.Min(allowedCount, Math.Max(0, maximumTotal - state.Enemies.Count));
        var availableSlots = state.AvailableEnemySlots.ToList();
        if (state.TotalEnemySlots is not null)
            allowedCount = Math.Min(allowedCount, availableSlots.Count);
        var startingPowers = summon.StartingPowers ?? definition.StartingPowers;
        var spawned = new List<(string InstanceId, EnemyCombatState State)>(allowedCount);
        for (var i = 0; i < allowedCount; i++)
        {
            var hpRange = checked(definition.MaxHp - definition.MinHp + 1);
            var hp = definition.MinHp + (hpRange == 1 ? 0 : session.Random.Get("niche").NextInt(hpRange));
            var instanceId = state.NextSummonedEnemyInstanceId(definition.Id);
            var startingAction = summon.StartingAction ?? definition.Actions[definition.StartingActionIndex].Id;
            var slotIndex = state.TotalEnemySlots is null
                ? state.Enemies.Max(enemy => enemy.SlotIndex) + 1
                : summon.SlotPlacement switch
                {
                    SummonSlotPlacement.NextAvailable or SummonSlotPlacement.FirstAvailable => availableSlots[0],
                    SummonSlotPlacement.LastAvailable => availableSlots[^1],
                    _ => throw new InvalidOperationException($"Unknown summon slot placement '{summon.SlotPlacement}'.")
                };
            if (state.TotalEnemySlots is not null)
                availableSlots.Remove(slotIndex);
            var enemy = new EnemyCombatState(new EntityState(instanceId, true, hp, hp), definition)
            {
                ActionIndex = definition.GetActionIndex(startingAction),
                StartingActionOverride = summon.StartingAction,
                SlotIndex = slotIndex
            };
            enemy.Creature.Block = definition.StartingBlock;
            state.AddEnemy(enemy);
            spawned.Add((instanceId, enemy));
        }

        state.PendingEffects.Add(new(owner,
            new InitializeSummonedEnemiesEffect(spawned.Select(enemy => enemy.InstanceId).ToArray()), null, null));
        foreach (var (_, enemy) in spawned)
        {
            state.Emit(session, "enemy_summoned", owner.Id, enemy.Creature.Id, 1);
            foreach (var startingPower in startingPowers)
                state.ApplyPower(session, enemy.Creature, startingPower.Power, startingPower.Amount, enemy.Creature);
        }
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

    private static void ApplyAttack(GameSession session, EntityState owner, EntityState victim, decimal amount,
        DamageProperties properties = DamageProperties.Move, CardInstance? cardSource = null,
        EffectNode? onFatal = null)
    {
        if (victim.Hp <= 0) return;
        var state = session.State;
        var rawDamage = state.CalculateDamage(session, owner, victim, amount, properties);
        state.Emit(session, "before_damage", owner.Id, victim.Id, (int)rawDamage, properties, cardSource);
        var absorbed = (properties & DamageProperties.Unblockable) != 0
            ? 0
            : Math.Min(victim.Block, rawDamage);
        var blockBeforeDamage = victim.Block;
        victim.Block -= absorbed;
        if (blockBeforeDamage > 0 && victim.Block == 0)
            state.Emit(session, "block_broken", owner.Id, victim.Id, blockBeforeDamage, properties, cardSource);
        var damage = Math.Min(victim.Hp, rawDamage - absorbed);
        var fatal = damage > 0 && damage == victim.Hp
            && !victim.Powers.Any(power => power.Value > 0 && state.Definition.Powers[power.Key].PreventsFatal);
        victim.Hp -= damage;
        if (fatal && onFatal is not null)
            state.PendingEffects.Add(new(owner, onFatal, victim, cardSource));
        state.Emit(session, "damage", owner.Id, victim.Id, damage, properties, cardSource);
        if (rawDamage > 0 && absorbed == rawDamage)
            state.Emit(session, "fully_blocked", owner.Id, victim.Id, (int)rawDamage, properties, cardSource);
        if (victim.Hp == 0)
            CompleteDeath(session, victim, owner, cardSource, properties);
    }

    private static void CompleteDeath(GameSession session, EntityState victim, EntityState source,
        CardInstance? cardSource, DamageProperties properties)
    {
        var state = session.State;
        state.RemovePowersAppliedBy(session, victim.Id);
        QueuePowerTriggerEffects(session, victim, PowerTrigger.AfterOwnerDeath, source, cardSource);
        if (victim.IsEnemy)
            foreach (var ally in state.Enemies.Select(enemy => enemy.Creature)
                .Where(ally => ally.Id != victim.Id && ally.Hp > 0))
                state.Emit(session, "ally_death", victim.Id, ally.Id, 1, properties, cardSource);
    }

    private static void LoseMaxHp(GameSession session, EntityState source, EntityState target, int amount)
    {
        if (amount == 0) return;
        var previousMaxHp = target.MaxHp;
        var rawMaxHp = (long)previousMaxHp - amount;
        if (rawMaxHp < target.Hp)
            ApplyAttack(session, source, target, target.Hp - rawMaxHp,
                DamageProperties.Unblockable | DamageProperties.Unpowered);
        target.MaxHp = (int)Math.Max(1L, rawMaxHp);
        if (ReferenceEquals(target, session.State.Player.Creature) && session.RunOwner is { } runOwner)
        {
            runOwner.State.MaxHp = target.MaxHp;
            runOwner.State.Hp = target.Hp;
        }
        session.State.Emit(session, "max_hp", source.Id, target.Id, target.MaxHp - previousMaxHp,
            DamageProperties.Unpowered);
    }

    private static EntityState OpponentOf(GameState state, EntityState owner) => owner.IsEnemy
        ? state.Player.Creature
        : state.FirstLivingEnemy ?? throw new InvalidOperationException("There is no living enemy to target.");
}
