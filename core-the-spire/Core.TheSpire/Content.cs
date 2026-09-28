using System.Collections.ObjectModel;

namespace CoreTheSpire;

public readonly record struct ContentId(string Value)
{
    public override string ToString() => Value;

    public static ContentId Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var separator = value.IndexOf(':');
        if (separator <= 0 || separator == value.Length - 1)
            throw new ArgumentException("Content IDs must be namespaced (namespace:name).", nameof(value));
        return new ContentId(value);
    }

    internal void Validate()
    {
        var separator = Value.IndexOf(':');
        if (separator <= 0 || separator == Value.Length - 1)
            throw new ArgumentException($"Content ID '{Value}' must use namespace:name form.");
    }
}

public sealed record PackageInfo(string Id, string Version);
public sealed record PowerStackAdjustment(ContentId Power, int AmountPerStack);
public sealed record PowerVariableDefinition(ContentId Id, int InitialValue);
public sealed record BlockGainModifierSubscription(int Multiplier = 2,
    DamageProperties RequiredProperties = DamageProperties.Move,
    DamageProperties ExcludedProperties = DamageProperties.None,
    int PriorEntriesPerPowerStack = 1, bool PlayerOnly = false);
public sealed record CardPlayRule(CardType Type, int? Cost = null, CardPile? ResultPile = null,
    bool ConsumeOnPlay = false, int ExtraPlays = 0, bool ConsumeOnPlayCount = false)
{
    public void Validate()
    {
        if (!Enum.IsDefined(Type) || Cost is < 0 || ExtraPlays < 0
            || ResultPile is not (null or CardPile.Discard or CardPile.Exhaust))
            throw new ArgumentException("A card-play rule requires a valid type, nonnegative cost, and discard/exhaust result pile.");
        if (ConsumeOnPlayCount && ExtraPlays == 0)
            throw new ArgumentException("Consuming a stack during play-count resolution requires extra plays.");
        if (Cost is null && ResultPile is null && !ConsumeOnPlay && ExtraPlays == 0)
            throw new ArgumentException("A card-play rule must modify a cost, result pile, or consume a stack.");
    }
}
public sealed record PowerDefinition(ContentId Id, PowerTrigger Trigger = PowerTrigger.None, ContentId? TriggerEffect = null,
    bool SkipFirstTriggerWhenAppliedToEnemy = false, PowerRole Role = PowerRole.None, bool DecrementOnTrigger = false,
    bool SkipFirstTriggerWhenAppliedToPlayer = false, decimal? OutgoingDamageMultiplier = null,
    bool RemoveWhenApplierDies = false, EffectNode? TriggerRuleEffect = null,
    CardType? TriggerCardType = null, bool RemoveAfterPlayerTurnEnd = false,
    int MaxEnergyPerStack = 0, int VulnerableMultiplierPercentPerStack = 0,
    PowerStackAdjustment? OnStackChange = null, PowerStackAdjustment? OnExpire = null,
    bool RemoveAfterOwnerTurnEnd = false, bool RemoveAfterOpponentTurnEnd = false)
{
    public CardPlayRule? CardRule { get; init; }
    public bool PreventEnergyGain { get; init; }
    public bool PreventsFatal { get; init; }
    public bool IsDebuff { get; init; }
    public IReadOnlyList<PowerEventSubscription> Events { get; init; } = Array.Empty<PowerEventSubscription>();
    public IReadOnlyList<PowerLifecycleSubscription> Lifecycle { get; init; } = Array.Empty<PowerLifecycleSubscription>();
    public IReadOnlyList<DamageModifierSubscription> IncomingDamageModifiers { get; init; } = Array.Empty<DamageModifierSubscription>();
    public IReadOnlyList<BlockGainModifierSubscription> BlockGainModifiers { get; init; } = Array.Empty<BlockGainModifierSubscription>();
    public decimal? BlockGainMultiplier { get; init; }
    public IReadOnlyList<PowerVariableDefinition> Variables { get; init; } = Array.Empty<PowerVariableDefinition>();

    public void Validate()
    {
        Id.Validate();
        CardRule?.Validate();
        if (TriggerEffect is { } triggerEffect) triggerEffect.Validate();
        if (!Enum.IsDefined(Trigger) || !Enum.IsDefined(Role))
            throw new ArgumentException("Power trigger and role must be recognized values.");
        if (TriggerCardType is { } cardType && (!Enum.IsDefined(cardType)
            || Trigger is not (PowerTrigger.AfterCardPlayed or PowerTrigger.AfterCardExhausted)))
            throw new ArgumentException("A card-type filter requires a card event trigger.");
        if (OutgoingDamageMultiplier is < 0m)
            throw new ArgumentOutOfRangeException(nameof(OutgoingDamageMultiplier));
        if (BlockGainMultiplier is < 0m)
            throw new ArgumentOutOfRangeException(nameof(BlockGainMultiplier));
        foreach (var adjustment in new[] { OnStackChange, OnExpire })
            if (adjustment is not null)
            {
                adjustment.Power.Validate();
                if (adjustment.AmountPerStack == 0)
                    throw new ArgumentException("A lifecycle power adjustment requires a nonzero multiplier.");
            }
        ArgumentNullException.ThrowIfNull(Variables);
        if (Variables.Select(variable => variable.Id).Distinct().Count() != Variables.Count)
            throw new ArgumentException("A Power cannot declare duplicate variables.");
        foreach (var variable in Variables)
            variable.Id.Validate();
        foreach (var subscription in Events)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(subscription.EventType);
            if (subscription.Owner is not (GameEventActor.Source or GameEventActor.Target)
                || (subscription.DeferUntilCardPlayed && subscription.DeferUntilOwnerTurnEnd)
                || !Enum.IsDefined(subscription.Accumulation)
                || (subscription.RequiredProperties & subscription.ExcludedProperties) != 0
                || !ValidDamageProperties(subscription.RequiredProperties)
                || !ValidDamageProperties(subscription.ExcludedProperties))
                throw new ArgumentException("A Power event subscription requires a source/target owner and non-overlapping damage property filters.");
            subscription.AffectedPower?.Validate();
            subscription.CardTag?.Validate();
        }
        foreach (var subscription in Lifecycle)
        {
            if (!Enum.IsDefined(subscription.Trigger)
                || subscription.Trigger is PowerTrigger.None or PowerTrigger.AfterCardPlayed
                    or PowerTrigger.AfterCardExhausted)
                throw new ArgumentException("Power lifecycle subscriptions require a combat phase trigger.");
            if (subscription.Effect is null)
                throw new ArgumentException("Power lifecycle subscriptions require an effect.");
        }
        if (Lifecycle.Select(subscription => subscription.Trigger).Distinct().Count() != Lifecycle.Count)
            throw new ArgumentException("A Power can define only one lifecycle effect per trigger.");
        foreach (var modifier in IncomingDamageModifiers)
        {
            if (modifier.Multiplier < 0m
                || (modifier.RequiredProperties & modifier.ExcludedProperties) != 0
                || !ValidDamageProperties(modifier.RequiredProperties)
                || !ValidDamageProperties(modifier.ExcludedProperties))
                throw new ArgumentException("Incoming damage modifiers require a nonnegative multiplier and valid, non-overlapping property filters.");
            modifier.RequiredSourcePower?.Validate();
        }
        foreach (var modifier in BlockGainModifiers)
        {
            if (modifier.Multiplier < 0 || modifier.PriorEntriesPerPowerStack < 0
                || (modifier.RequiredProperties & modifier.ExcludedProperties) != 0
                || !ValidDamageProperties(modifier.RequiredProperties)
                || !ValidDamageProperties(modifier.ExcludedProperties))
                throw new ArgumentException("Block gain modifiers require a nonnegative multiplier, limit, and valid non-overlapping property filters.");
        }
    }

    private static bool ValidDamageProperties(DamageProperties properties) =>
        (properties & ~(DamageProperties.Move | DamageProperties.Unpowered
            | DamageProperties.Unblockable | DamageProperties.SkipHurtAnimation)) == 0;
}
public enum CardRarity { None, Basic, Common, Uncommon, Rare, Ancient, Event, Token, Status, Curse, Quest }
public enum PotionRarity { Common, Uncommon, Rare }
public sealed record PotionDefinition(ContentId Id, PotionRarity Rarity, EffectNode Effect,
    TargetSelector Target = TargetSelector.Self, bool InSharedPool = true,
    IReadOnlyList<ContentId>? CharacterPoolIds = null, bool CanBeGeneratedInCombat = true)
{
    public void Validate()
    {
        Id.Validate();
        if (!Enum.IsDefined(Rarity) || !Enum.IsDefined(Target))
            throw new ArgumentException("Potion rarity and target must be recognized values.");
        if (CharacterPoolIds is { Count: 0 })
            throw new ArgumentException("A character potion pool must name at least one character.");
        if (CharacterPoolIds is not null)
        {
            foreach (var characterId in CharacterPoolIds) characterId.Validate();
            if (CharacterPoolIds.Distinct().Count() != CharacterPoolIds.Count)
                throw new ArgumentException("A character potion pool cannot list a character more than once.");
            if (InSharedPool)
                throw new ArgumentException("A potion cannot be both a shared potion and a character-pool potion.");
        }
    }
}
public enum CardCostKind { Fixed, X }
public enum RunRelicRarity { None, Starter, Common, Uncommon, Rare, Shop, Event, Ancient, Boss }
public enum RunRelicPool { Shared, Player }

public sealed record RelicDefinition(ContentId Id, RunRelicRarity Rarity, bool InSharedPool = true,
    bool IsFallback = false, bool IsStackable = false, IReadOnlyList<ContentId>? CharacterPoolIds = null)
{
    public void Validate()
    {
        Id.Validate();
        if (!Enum.IsDefined(Rarity)) throw new ArgumentOutOfRangeException(nameof(Rarity));
        if (IsFallback && (InSharedPool || Rarity != RunRelicRarity.None))
            throw new ArgumentException("A fallback relic must be outside reward pools and use the None rarity.");
        if (CharacterPoolIds is { Count: 0 })
            throw new ArgumentException("A character relic pool must name at least one character.");
        if (CharacterPoolIds is not null)
        {
            foreach (var characterId in CharacterPoolIds) characterId.Validate();
            if (CharacterPoolIds.Distinct().Count() != CharacterPoolIds.Count)
                throw new ArgumentException("A character relic pool cannot list a character more than once.");
            if (InSharedPool)
                throw new ArgumentException("A relic cannot be both a shared relic and a character-pool relic.");
            if (IsFallback || Rarity is not (RunRelicRarity.Common or RunRelicRarity.Uncommon
                or RunRelicRarity.Rare or RunRelicRarity.Shop))
                throw new ArgumentException("Character-pool relics must use a reward rarity and cannot be fallback relics.");
        }
    }
}

public sealed record CardDefinition(ContentId Id, int Cost, CardType Type, EffectNode Effect,
    CardKeywords Keywords = CardKeywords.None, TargetSelector Target = TargetSelector.FirstEnemy,
    CardRarity Rarity = CardRarity.Common, EffectNode? UpgradedEffect = null, int? UpgradedCost = null,
    CardCostKind CostKind = CardCostKind.Fixed, bool CanBeGeneratedInCombat = true)
{
    public bool HasEnergyXCost => CostKind == CardCostKind.X;
    public IReadOnlyList<ContentId> Tags { get; init; } = Array.Empty<ContentId>();
    public IReadOnlyList<ContentId> PoolIds { get; init; } = Array.Empty<ContentId>();
    public IReadOnlyList<CardVariableDefinition> Variables { get; init; } = Array.Empty<CardVariableDefinition>();
    public ContentId? DamageValueVariable { get; init; }
    public decimal? DamageValue { get; init; }
    public decimal? UpgradedDamageValue { get; init; }
    public ScaledCombatAmount? CalculatedDamageValue { get; init; }
    public ScaledCombatAmount? UpgradedCalculatedDamageValue { get; init; }
    public EffectNode? OnExhaust { get; init; }
    public EffectNode? UpgradedOnExhaust { get; init; }
    public EffectNode? AfterAutoPostPlay { get; init; }
    public EffectNode? UpgradedAfterAutoPostPlay { get; init; }
    public CardKeywords UpgradedKeywords { get; init; }
    public EffectNode? OnEnterCombat { get; init; }
    public EffectNode? UpgradedOnEnterCombat { get; init; }
    public CardPlayListener? BeforeCardPlayed { get; init; }
    public CardPlayListener? UpgradedBeforeCardPlayed { get; init; }
    public CardPlayListener? AfterCardPlayed { get; init; }
    public CardPlayListener? UpgradedAfterCardPlayed { get; init; }
    public bool HasUpgrade => UpgradedEffect is not null || UpgradedCost is not null
        || UpgradedKeywords != CardKeywords.None || UpgradedOnExhaust is not null
        || UpgradedOnEnterCombat is not null || UpgradedBeforeCardPlayed is not null || UpgradedAfterCardPlayed is not null
        || UpgradedAfterAutoPostPlay is not null
        || Variables.Any(variable => variable.UpgradedValue is not null);

    public int ResolveVariable(CardInstance instance, ContentId variableId) =>
        decimal.ToInt32(decimal.Floor(ResolveDecimalVariable(instance, variableId)));

    public decimal ResolveDecimalVariable(CardInstance instance, ContentId variableId)
    {
        if (instance.CardId != Id) throw new ArgumentException("Card instance belongs to a different definition.", nameof(instance));
        var variable = Variables.FirstOrDefault(variable => variable.Id == variableId)
            ?? throw new InvalidOperationException($"Card '{Id}' does not declare variable '{variableId}'.");
        var initial = instance.UpgradeLevel > 0 ? variable.UpgradedValue ?? variable.InitialValue : variable.InitialValue;
        return initial + instance.VariableOffsets.FirstOrDefault(value => value.Variable == variableId).Offset;
    }

    public EffectNode? ResolveEnterEffect(CardInstance instance)
    {
        if (instance.CardId != Id) throw new ArgumentException("Card instance belongs to a different definition.", nameof(instance));
        return instance.UpgradeLevel > 0 ? UpgradedOnEnterCombat ?? OnEnterCombat : OnEnterCombat;
    }

    public CardPlayListener? ResolvePlayListener(CardInstance instance, bool afterPlay)
    {
        if (instance.CardId != Id) throw new ArgumentException("Card instance belongs to a different definition.", nameof(instance));
        var ordinary = afterPlay ? AfterCardPlayed : BeforeCardPlayed;
        var upgraded = afterPlay ? UpgradedAfterCardPlayed : UpgradedBeforeCardPlayed;
        return instance.UpgradeLevel > 0 ? upgraded ?? ordinary : ordinary;
    }

    public EffectNode? ResolveExhaustEffect(CardInstance instance)
    {
        if (instance.CardId != Id) throw new ArgumentException("Card instance belongs to a different definition.", nameof(instance));
        return instance.UpgradeLevel > 0 ? UpgradedOnExhaust ?? OnExhaust : OnExhaust;
    }

    public EffectNode? ResolveAfterAutoPostPlayEffect(CardInstance instance)
    {
        if (instance.CardId != Id) throw new ArgumentException("Card instance belongs to a different definition.", nameof(instance));
        return instance.UpgradeLevel > 0 ? UpgradedAfterAutoPostPlay ?? AfterAutoPostPlay : AfterAutoPostPlay;
    }

    public EffectNode ResolveEffect(CardInstance instance)
    {
        if (instance.CardId != Id) throw new ArgumentException("Card instance belongs to a different definition.", nameof(instance));
        return instance.UpgradeLevel > 0 ? UpgradedEffect ?? Effect : Effect;
    }

    public int ResolveCost(CardInstance instance)
    {
        if (instance.CardId != Id) throw new ArgumentException("Card instance belongs to a different definition.", nameof(instance));
        return instance.UpgradeLevel > 0 ? UpgradedCost ?? Cost : Cost;
    }

    public CardKeywords ResolveKeywords(CardInstance instance)
    {
        if (instance.CardId != Id) throw new ArgumentException("Card instance belongs to a different definition.", nameof(instance));
        return instance.UpgradeLevel > 0 ? Keywords | UpgradedKeywords : Keywords;
    }

}

public sealed record CardInstance
{
    public CardInstance(string instanceId, ContentId cardId, int upgradeLevel = 0,
        IReadOnlyList<CardCostModifier>? costModifiers = null, bool isClone = false,
        IReadOnlyList<CardVariableOffset>? variableOffsets = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        cardId.Validate();
        ArgumentOutOfRangeException.ThrowIfNegative(upgradeLevel);
        InstanceId = instanceId;
        CardId = cardId;
        UpgradeLevel = upgradeLevel;
        IsClone = isClone;
        var modifiers = costModifiers?.ToArray() ?? [];
        foreach (var modifier in modifiers) modifier.Validate();
        CostModifiers = modifiers.Length == 0 ? Array.Empty<CardCostModifier>() : Array.AsReadOnly(modifiers);
        var offsets = variableOffsets?.ToArray() ?? [];
        if (offsets.Length > 0)
        {
            foreach (var offset in offsets) offset.Variable.Validate();
            if (offsets.Select(value => value.Variable).Distinct().Count() != offsets.Length)
                throw new ArgumentException("A card instance cannot contain duplicate variable offsets.", nameof(variableOffsets));
        }
        VariableOffsets = offsets.Length == 0 ? Array.Empty<CardVariableOffset>() : Array.AsReadOnly(offsets);
    }

    public string InstanceId { get; }
    public ContentId CardId { get; }
    public int UpgradeLevel { get; }
    public IReadOnlyList<CardCostModifier> CostModifiers { get; }
    public bool IsClone { get; }
    public IReadOnlyList<CardVariableOffset> VariableOffsets { get; }

    internal CardInstance WithUpgradeLevel(int level) => new(InstanceId, CardId, level, CostModifiers, IsClone, VariableOffsets);
    internal CardInstance WithCostModifiers(IReadOnlyList<CardCostModifier> modifiers) =>
        new(InstanceId, CardId, UpgradeLevel, modifiers, IsClone, VariableOffsets);
    internal CardInstance WithVariableOffsets(IReadOnlyList<CardVariableOffset> offsets) =>
        new(InstanceId, CardId, UpgradeLevel, CostModifiers, IsClone, offsets);
}

public sealed class CardPoolDefinition
{
    public CardPoolDefinition(ContentId id, IEnumerable<ContentId> cards)
    {
        id.Validate();
        ArgumentNullException.ThrowIfNull(cards);
        var cardIds = cards.ToArray();
        foreach (var cardId in cardIds) cardId.Validate();
        if (cardIds.Distinct().Count() != cardIds.Length)
            throw new ArgumentException("Card pools cannot contain duplicate card IDs.", nameof(cards));
        Id = id;
        Cards = Array.AsReadOnly(cardIds);
    }

    public ContentId Id { get; }
    public IReadOnlyList<ContentId> Cards { get; }
}
public sealed record EnemyActionDefinition(ContentId Id, EffectNode Effect, IntentKind Intent,
    int IntentDamage = 0, ContentId? AppliedPower = null, int AppliedPowerAmount = 0,
    ContentId? NextActionId = null, TargetSelector AppliedPowerTarget = TargetSelector.Self,
    EnemyActionTransition? Transition = null);
public sealed record EnemyStartingPower(ContentId Power, int Amount);
public sealed record EnemyActionChoice(ContentId ActionId, int Weight = 1, int? MaxConsecutiveUses = null,
    EnemyActionCondition? Condition = null, int? MaxUsesPerCombat = null);
public enum EnemyActionRepeatPolicy { Allow, PreventImmediateRepeat }
public enum EnemyActionSelectionMode { WeightedRandom, FirstMatchingCondition }

public sealed record EnemyActionTransition
{
    public EnemyActionTransition(IEnumerable<EnemyActionChoice> choices,
        EnemyActionRepeatPolicy repeatPolicy = EnemyActionRepeatPolicy.Allow,
        EnemyActionSelectionMode selectionMode = EnemyActionSelectionMode.WeightedRandom)
    {
        ArgumentNullException.ThrowIfNull(choices);
        var copiedChoices = choices.ToArray();
        if (copiedChoices.Length == 0 || copiedChoices.Any(choice => choice is null))
            throw new ArgumentException("An enemy action transition needs at least one choice.", nameof(choices));
        foreach (var choice in copiedChoices)
        {
            choice.ActionId.Validate();
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(choice.Weight);
            if (choice.MaxConsecutiveUses is { } maxUses)
                ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxUses);
            if (choice.MaxUsesPerCombat is { } maxTotalUses)
                ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxTotalUses);
        }
        if (!Enum.IsDefined(repeatPolicy)) throw new ArgumentOutOfRangeException(nameof(repeatPolicy));
        if (!Enum.IsDefined(selectionMode)) throw new ArgumentOutOfRangeException(nameof(selectionMode));
        if (selectionMode == EnemyActionSelectionMode.FirstMatchingCondition
            && (copiedChoices.All(choice => choice.Condition is null)
                || copiedChoices.Any(choice => choice.Weight != 1 || choice.MaxConsecutiveUses is not null)
                || repeatPolicy != EnemyActionRepeatPolicy.Allow))
            throw new ArgumentException("First-match transitions require conditions and cannot use weights or repeat limits.", nameof(choices));
        if (repeatPolicy == EnemyActionRepeatPolicy.PreventImmediateRepeat
            && copiedChoices.Select(choice => choice.ActionId).Distinct().Count() < 2)
            throw new ArgumentException("PreventImmediateRepeat needs at least two distinct actions.", nameof(choices));
        long totalWeight = 0;
        try
        {
            foreach (var choice in copiedChoices) totalWeight = checked(totalWeight + choice.Weight);
        }
        catch (OverflowException)
        {
            throw new ArgumentOutOfRangeException(nameof(choices), "Transition weights exceed supported range.");
        }
        if (totalWeight > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(choices), "Transition weights exceed supported range.");
        Choices = Array.AsReadOnly(copiedChoices);
        RepeatPolicy = repeatPolicy;
        SelectionMode = selectionMode;
    }

    public IReadOnlyList<EnemyActionChoice> Choices { get; }
    public EnemyActionRepeatPolicy RepeatPolicy { get; }
    public EnemyActionSelectionMode SelectionMode { get; }
}
public enum RunEventKind { Event, Ancient, Final }

[System.Text.Json.Serialization.JsonPolymorphic(TypeDiscriminatorPropertyName = "op")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(GainRunGoldEffect), "gainRunGold")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(LoseRunGoldEffect), "loseRunGold")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(HealRunEffect), "healRun")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(GainRunMaxHpEffect), "gainRunMaxHp")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(LoseRunMaxHpEffect), "loseRunMaxHp")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(LoseRunHpEffect), "loseRunHp")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(AddCardToRunDeckEffect), "addCardToRunDeck")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(ChooseRunCardEffect), "chooseRunCard")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(RemoveSelectedRunCardEffect), "removeSelectedRunCard")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(UpgradeSelectedRunCardEffect), "upgradeSelectedRunCard")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(WinRunEffect), "winRun")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(GainRunRelicEffect), "gainRunRelic")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(EnterRunCombatEffect), "enterRunCombat")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(CustomRunEffect), "customRun")]
public abstract record RunEffectNode;
public sealed record GainRunGoldEffect(int Amount) : RunEffectNode;
public sealed record LoseRunGoldEffect(int Amount) : RunEffectNode;
public sealed record HealRunEffect(int Amount) : RunEffectNode;
public sealed record GainRunMaxHpEffect(int Amount) : RunEffectNode;
public sealed record LoseRunMaxHpEffect(int Amount) : RunEffectNode;
public sealed record LoseRunHpEffect(int Amount) : RunEffectNode;
public sealed record AddCardToRunDeckEffect(ContentId Card) : RunEffectNode;
public enum RunCardFilter { Any, Upgradeable }
public sealed record ChooseRunCardEffect(string Prompt, RunCardFilter Filter, bool CanSkip,
    RunEffectNode OnSelected) : RunEffectNode;
public sealed record RemoveSelectedRunCardEffect : RunEffectNode;
public sealed record UpgradeSelectedRunCardEffect : RunEffectNode;
public sealed record WinRunEffect : RunEffectNode;
public sealed record GainRunRelicEffect(ContentId RelicId) : RunEffectNode;
public sealed record EnterRunCombatEffect(ContentId EncounterId, bool ResumeEventAfterCombat = true) : RunEffectNode;
public sealed record CustomRunEffect(ContentId Handler, System.Text.Json.JsonElement Payload) : RunEffectNode;

[System.Text.Json.Serialization.JsonPolymorphic(TypeDiscriminatorPropertyName = "test")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(RunMetricCondition), "metric")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(HasRunRelicCondition), "hasRelic")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(HasRunPotionCondition), "hasPotion")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(AllRunCondition), "all")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(AnyRunCondition), "any")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(NotRunCondition), "not")]
public abstract record RunCondition;
public enum RunMetric { Gold, Hp, MaxHp, DeckSize, PotionCount, EmptyPotionSlots }
public enum RunComparison { Equal, AtLeast, AtMost, GreaterThan, LessThan }
internal static class ConditionEvaluation
{
    public static bool Compare(int value, RunComparison comparison, int threshold) => comparison switch
    {
        RunComparison.Equal => value == threshold,
        RunComparison.AtLeast => value >= threshold,
        RunComparison.AtMost => value <= threshold,
        RunComparison.GreaterThan => value > threshold,
        RunComparison.LessThan => value < threshold,
        _ => throw new ArgumentOutOfRangeException(nameof(comparison))
    };
}
public enum EnemyActionMetric { Turn, EnemyIndex, EnemyHp, EnemyMaxHp, EnemyHpPercent, EnemyBlock, PlayerHp, PlayerMaxHp, PlayerHpPercent, PlayerBlock, LivingEnemies }
public enum EnemyActionSide { Enemy, Player }

[System.Text.Json.Serialization.JsonPolymorphic(TypeDiscriminatorPropertyName = "test")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(EnemyMetricCondition), "metric")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(EnemyPowerCondition), "power")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(AllEnemyCondition), "all")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(AnyEnemyCondition), "any")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(NotEnemyCondition), "not")]
public abstract record EnemyActionCondition
{
    internal abstract bool Evaluate(EnemyActionEvaluationContext context);
}

public sealed record EnemyMetricCondition(EnemyActionMetric Metric, RunComparison Comparison, int Value)
    : EnemyActionCondition
{
    internal override bool Evaluate(EnemyActionEvaluationContext context) =>
        ConditionEvaluation.Compare(context.GetMetric(Metric), Comparison, Value);
}

public sealed record EnemyPowerCondition(EnemyActionSide Side, ContentId PowerId, RunComparison Comparison, int Value)
    : EnemyActionCondition
{
    internal override bool Evaluate(EnemyActionEvaluationContext context) =>
        ConditionEvaluation.Compare(context.GetPower(Side, PowerId), Comparison, Value);
}

public sealed record AllEnemyCondition : EnemyActionCondition
{
    public AllEnemyCondition(IReadOnlyList<EnemyActionCondition> conditions)
    {
        ArgumentNullException.ThrowIfNull(conditions);
        var copiedConditions = conditions.ToArray();
        if (copiedConditions.Length == 0 || copiedConditions.Any(condition => condition is null))
            throw new ArgumentException("An all condition needs at least one non-null condition.", nameof(conditions));
        Conditions = Array.AsReadOnly(copiedConditions);
    }

    public IReadOnlyList<EnemyActionCondition> Conditions { get; }
    internal override bool Evaluate(EnemyActionEvaluationContext context) => Conditions.All(condition => condition.Evaluate(context));
}

public sealed record AnyEnemyCondition : EnemyActionCondition
{
    public AnyEnemyCondition(IReadOnlyList<EnemyActionCondition> conditions)
    {
        ArgumentNullException.ThrowIfNull(conditions);
        var copiedConditions = conditions.ToArray();
        if (copiedConditions.Length == 0 || copiedConditions.Any(condition => condition is null))
            throw new ArgumentException("An any condition needs at least one non-null condition.", nameof(conditions));
        Conditions = Array.AsReadOnly(copiedConditions);
    }

    public IReadOnlyList<EnemyActionCondition> Conditions { get; }
    internal override bool Evaluate(EnemyActionEvaluationContext context) => Conditions.Any(condition => condition.Evaluate(context));
}

public sealed record NotEnemyCondition : EnemyActionCondition
{
    public NotEnemyCondition(EnemyActionCondition condition) =>
        Condition = condition ?? throw new ArgumentNullException(nameof(condition));

    public EnemyActionCondition Condition { get; }
    internal override bool Evaluate(EnemyActionEvaluationContext context) => !Condition.Evaluate(context);
}
public sealed record RunMetricCondition(RunMetric Metric, RunComparison Comparison, int Threshold) : RunCondition;
public sealed record HasRunRelicCondition(ContentId Relic) : RunCondition;
public sealed record HasRunPotionCondition(ContentId Potion) : RunCondition;
public sealed record AllRunCondition : RunCondition
{
    [System.Text.Json.Serialization.JsonConstructor]
    public AllRunCondition(IReadOnlyList<RunCondition> conditions) => Conditions = Array.AsReadOnly(conditions.ToArray());
    public IReadOnlyList<RunCondition> Conditions { get; }
}
public sealed record AnyRunCondition : RunCondition
{
    [System.Text.Json.Serialization.JsonConstructor]
    public AnyRunCondition(IReadOnlyList<RunCondition> conditions) => Conditions = Array.AsReadOnly(conditions.ToArray());
    public IReadOnlyList<RunCondition> Conditions { get; }
}
public sealed record NotRunCondition(RunCondition Condition) : RunCondition;

public sealed record RunEventOptionDefinition
{
    public RunEventOptionDefinition(ContentId id, string label, IEnumerable<RunEffectNode>? effects = null,
        RunCondition? requirement = null, ContentId? nextPageId = null)
    {
        id.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        Id = id;
        Label = label;
        var copiedEffects = effects?.ToArray() ?? [];
        if (copiedEffects.Any(effect => effect is null))
            throw new ArgumentException("Run event effects cannot contain null values.", nameof(effects));
        Effects = Array.AsReadOnly(copiedEffects);
        Requirement = requirement;
        if (nextPageId is { } pageId) pageId.Validate();
        NextPageId = nextPageId;
    }

    public ContentId Id { get; }
    public string Label { get; }
    public IReadOnlyList<RunEffectNode> Effects { get; }
    public RunCondition? Requirement { get; }
    public ContentId? NextPageId { get; }
}

public sealed record RunEventPageDefinition
{
    public RunEventPageDefinition(ContentId id, string description,
        IEnumerable<RunEventOptionDefinition> options)
    {
        id.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(options);
        var copiedOptions = options.ToArray();
        if (copiedOptions.Any(option => option is null))
            throw new ArgumentException("Run event page options cannot contain null values.", nameof(options));
        if (copiedOptions.Select(option => option.Id).Distinct().Count() != copiedOptions.Length)
            throw new ArgumentException("Run event option IDs must be unique within a page.", nameof(options));
        Id = id;
        Description = description;
        Options = Array.AsReadOnly(copiedOptions);
    }

    public ContentId Id { get; }
    public string Description { get; }
    public IReadOnlyList<RunEventOptionDefinition> Options { get; }
}

public sealed record RunEventDefinition
{
    public RunEventDefinition(ContentId id, RunEventKind kind = RunEventKind.Event,
        IEnumerable<RunEventOptionDefinition>? options = null)
        : this(id, kind, id, [new RunEventPageDefinition(id, id.Value, options ?? [])])
    {
    }

    public RunEventDefinition(ContentId id, RunEventKind kind, ContentId initialPageId,
        IEnumerable<RunEventPageDefinition> pages)
    {
        id.Validate();
        initialPageId.Validate();
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        Id = id;
        Kind = kind;
        ArgumentNullException.ThrowIfNull(pages);
        var copiedPages = pages.ToArray();
        if (copiedPages.Length == 0 || copiedPages.Any(page => page is null))
            throw new ArgumentException("A run event must define at least one page.", nameof(pages));
        if (copiedPages.Select(page => page.Id).Distinct().Count() != copiedPages.Length)
            throw new ArgumentException("Run event page IDs must be unique.", nameof(pages));
        if (copiedPages.All(page => page.Id != initialPageId))
            throw new ArgumentException("The initial run event page must be defined.", nameof(initialPageId));
        if (kind == RunEventKind.Final && copiedPages.Single(page => page.Id == initialPageId).Options.Count == 0)
            throw new ArgumentException("A final event needs at least one option.", nameof(pages));
        InitialPageId = initialPageId;
        Pages = new System.Collections.ObjectModel.ReadOnlyDictionary<ContentId, RunEventPageDefinition>(
            copiedPages.ToDictionary(page => page.Id));
        Options = Pages[initialPageId].Options;
    }

    public ContentId Id { get; }
    public RunEventKind Kind { get; }
    public ContentId InitialPageId { get; }
    public IReadOnlyDictionary<ContentId, RunEventPageDefinition> Pages { get; }
    public IReadOnlyList<RunEventOptionDefinition> Options { get; }

    public void Validate()
    {
        Id.Validate();
        if (!Enum.IsDefined(Kind)) throw new ArgumentOutOfRangeException(nameof(Kind));
    }
}

public sealed class EncounterDefinition
{
    public EncounterDefinition(ContentId id, RunRoomType roomType, IEnumerable<ContentId> enemyIds,
        bool isWeak = false, IEnumerable<ContentId>? tags = null,
        IReadOnlyDictionary<int, ContentId>? startingActionsByEnemyIndex = null,
        bool shouldGiveRewards = true, int? minGoldReward = null, int? maxGoldReward = null)
        : this(id, roomType, ToFixedSlots(enemyIds), isWeak, tags, startingActionsByEnemyIndex,
            shouldGiveRewards, minGoldReward, maxGoldReward)
    {
    }

    private EncounterDefinition(ContentId id, RunRoomType roomType, IEnumerable<EncounterEnemySlot> enemySlots,
        bool isWeak, IEnumerable<ContentId>? tags,
        IReadOnlyDictionary<int, ContentId>? startingActionsByEnemyIndex,
        bool shouldGiveRewards, int? minGoldReward, int? maxGoldReward,
        IEnumerable<EncounterFormationChoice>? formations = null)
    {
        id.Validate();
        ArgumentNullException.ThrowIfNull(enemySlots);
        var slots = enemySlots.ToArray();
        var copiedFormations = formations?.ToArray() ?? [];
        if (roomType is not (RunRoomType.Monster or RunRoomType.Elite or RunRoomType.Boss))
            throw new ArgumentOutOfRangeException(nameof(roomType), "Encounters must be Monster, Elite, or Boss rooms.");
        if (slots.Any(slot => slot is null)) throw new ArgumentException("Encounter slots cannot be null.", nameof(enemySlots));
        if (copiedFormations.Any(formation => formation is null))
            throw new ArgumentException("Encounter formations cannot be null.", nameof(formations));
        if (copiedFormations.Length > 0)
        {
            if (slots.Length != 0)
                throw new ArgumentException("An encounter must use fixed slots or complete formations, not both.", nameof(enemySlots));
            if (copiedFormations.Select(formation => formation.EnemyIds.Count).Distinct().Count() != 1)
                throw new ArgumentException("Encounter formation choices must contain the same number of enemies.", nameof(formations));
            long totalWeight = 0;
            try
            {
                foreach (var formation in copiedFormations) totalWeight = checked(totalWeight + formation.Weight);
            }
            catch (OverflowException)
            {
                throw new ArgumentOutOfRangeException(nameof(formations), "Formation weights exceed supported range.");
            }
            if (totalWeight > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(formations), "Formation weights exceed supported range.");
        }
        else
        {
            if (slots.Length == 0) throw new ArgumentException("An encounter must contain at least one enemy.", nameof(enemySlots));
            ValidateChoiceGroups(slots);
        }
        var encounterTags = tags?.ToArray() ?? [];
        foreach (var tag in encounterTags) tag.Validate();
        var startingActions = startingActionsByEnemyIndex is null
            ? new Dictionary<int, ContentId>()
            : new Dictionary<int, ContentId>(startingActionsByEnemyIndex);
        foreach (var (enemyIndex, actionId) in startingActions)
        {
            var slotCount = copiedFormations.Length > 0 ? copiedFormations[0].EnemyIds.Count : slots.Length;
            if (enemyIndex < 0 || enemyIndex >= slotCount)
                throw new ArgumentOutOfRangeException(nameof(startingActionsByEnemyIndex), "Starting action index must refer to an enemy in this encounter.");
            actionId.Validate();
        }
        Id = id;
        RoomType = roomType;
        EnemySlots = Array.AsReadOnly(slots);
        Formations = Array.AsReadOnly(copiedFormations);
        IsWeak = isWeak;
        ShouldGiveRewards = shouldGiveRewards;
        var defaultGold = roomType switch
        {
            RunRoomType.Monster => (Min: 10, Max: 20),
            RunRoomType.Elite => (Min: 35, Max: 45),
            RunRoomType.Boss => (Min: 100, Max: 100),
            _ => throw new ArgumentOutOfRangeException(nameof(roomType))
        };
        MinGoldReward = minGoldReward ?? defaultGold.Min;
        MaxGoldReward = maxGoldReward ?? defaultGold.Max;
        if (MinGoldReward < 0 || MaxGoldReward < MinGoldReward || MaxGoldReward == int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(minGoldReward), "Gold reward bounds must satisfy 0 <= min <= max.");
        Tags = Array.AsReadOnly(encounterTags);
        StartingActionsByEnemyIndex = new ReadOnlyDictionary<int, ContentId>(startingActions);
    }

    public ContentId Id { get; }
    public RunRoomType RoomType { get; }
    public IReadOnlyList<EncounterEnemySlot> EnemySlots { get; }
    public IReadOnlyList<EncounterFormationChoice> Formations { get; }
    public IReadOnlyList<ContentId> PossibleEnemyIds => Formations.Count > 0
        ? Array.AsReadOnly(Formations.SelectMany(formation => formation.EnemyIds).Distinct().ToArray())
        : Array.AsReadOnly(EnemySlots.SelectMany(slot => slot.EnemyChoices).Distinct().ToArray());
    public bool IsWeak { get; }
    public bool ShouldGiveRewards { get; }
    public int MinGoldReward { get; }
    public int MaxGoldReward { get; }
    public IReadOnlyList<ContentId> Tags { get; }
    public IReadOnlyDictionary<int, ContentId> StartingActionsByEnemyIndex { get; }

    public static EncounterDefinition WithEnemySlots(ContentId id, RunRoomType roomType,
        IEnumerable<EncounterEnemySlot> enemySlots, bool isWeak = false, IEnumerable<ContentId>? tags = null,
        IReadOnlyDictionary<int, ContentId>? startingActionsByEnemyIndex = null,
        bool shouldGiveRewards = true, int? minGoldReward = null, int? maxGoldReward = null) =>
        new(id, roomType, enemySlots, isWeak, tags, startingActionsByEnemyIndex,
            shouldGiveRewards, minGoldReward, maxGoldReward);

    public static EncounterDefinition WithFormations(ContentId id, RunRoomType roomType,
        IEnumerable<EncounterFormationChoice> formations, bool isWeak = false, IEnumerable<ContentId>? tags = null,
        IReadOnlyDictionary<int, ContentId>? startingActionsByEnemyIndex = null,
        bool shouldGiveRewards = true, int? minGoldReward = null, int? maxGoldReward = null) =>
        new(id, roomType, Array.Empty<EncounterEnemySlot>(), isWeak, tags, startingActionsByEnemyIndex,
            shouldGiveRewards, minGoldReward, maxGoldReward, formations);

    public IReadOnlyList<EncounterEnemySelection> SelectEnemies(SeededRandom random)
    {
        ArgumentNullException.ThrowIfNull(random);
        if (Formations.Count > 0)
        {
            var totalWeight = Formations.Sum(formation => formation.Weight);
            var roll = random.NextInt(totalWeight);
            EncounterFormationChoice? selectedFormation = null;
            foreach (var formation in Formations)
            {
                if (roll < formation.Weight)
                {
                    selectedFormation = formation;
                    break;
                }
                roll -= formation.Weight;
            }
            if (selectedFormation is null)
                throw new InvalidOperationException($"Encounter '{Id}' formation selection exceeded its total weight.");
            return Array.AsReadOnly(selectedFormation.EnemyIds.Select((enemyId, index) =>
                new EncounterEnemySelection(enemyId,
                    StartingActionsByEnemyIndex.TryGetValue(index, out var overrideId) ? overrideId : null)).ToArray());
        }
        var selectedByGroup = new Dictionary<ContentId, HashSet<ContentId>>();
        var selected = new EncounterEnemySelection[EnemySlots.Count];
        for (var i = 0; i < EnemySlots.Count; i++)
        {
            var slot = EnemySlots[i];
            var candidates = slot.EnemyChoices;
            HashSet<ContentId>? groupSelection = null;
            if (slot.UniqueChoiceGroup is { } group)
            {
                if (!selectedByGroup.TryGetValue(group, out groupSelection))
                    selectedByGroup.Add(group, groupSelection = []);
                candidates = candidates.Where(enemyId => !groupSelection.Contains(enemyId)).ToArray();
            }
            if (candidates.Count == 0)
                throw new InvalidOperationException($"Encounter '{Id}' has no enemy choices left for slot {i}.");
            var enemyId = slot.ChooseRandomly ? candidates[random.NextInt(candidates.Count)] : candidates[0];
            groupSelection?.Add(enemyId);
            ContentId? startingAction = StartingActionsByEnemyIndex.TryGetValue(i, out var overrideId) ? overrideId : null;
            selected[i] = new EncounterEnemySelection(enemyId,
                startingAction);
        }
        return Array.AsReadOnly(selected);
    }

    internal IReadOnlyList<ContentId> GetPossibleEnemyIdsAtSlot(int index) => Formations.Count > 0
        ? Array.AsReadOnly(Formations.Select(formation => formation.EnemyIds[index]).Distinct().ToArray())
        : EnemySlots[index].EnemyChoices;

    private static EncounterEnemySlot[] ToFixedSlots(IEnumerable<ContentId> enemyIds)
    {
        ArgumentNullException.ThrowIfNull(enemyIds);
        return enemyIds.Select(EncounterEnemySlot.Fixed).ToArray();
    }

    private static void ValidateChoiceGroups(IReadOnlyList<EncounterEnemySlot> slots)
    {
        foreach (var group in slots.Where(slot => slot.UniqueChoiceGroup is not null)
            .GroupBy(slot => slot.UniqueChoiceGroup!.Value))
        {
            var groupedSlots = group.ToArray();
            var choices = groupedSlots[0].EnemyChoices.ToHashSet();
            if (groupedSlots.Any(slot => !slot.ChooseRandomly || !slot.EnemyChoices.ToHashSet().SetEquals(choices)))
                throw new ArgumentException($"All slots in unique-choice group '{group.Key}' must use the same random enemy pool.", nameof(slots));
            if (choices.Count < groupedSlots.Length)
                throw new ArgumentException($"Unique-choice group '{group.Key}' has fewer enemy choices than slots.", nameof(slots));
        }
    }
}
public sealed record EnemyDefinition
{
    private readonly IReadOnlyDictionary<ContentId, int> _actionIndexes;
    private readonly IReadOnlySet<ContentId> _limitedActionIds;

    public EnemyDefinition(ContentId id, int minHp, int maxHp, IReadOnlyList<EnemyActionDefinition> actions,
        bool loopLastAction = true, ContentId? startingActionId = null,
        EnemyActionTransition? startingTransition = null,
        IEnumerable<EnemyStartingPower>? startingPowers = null)
    {
        id.Validate();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minHp);
        if (maxHp < minHp) throw new ArgumentOutOfRangeException(nameof(maxHp));
        ArgumentNullException.ThrowIfNull(actions);
        var copiedActions = actions.ToArray();
        var copiedStartingPowers = startingPowers?.ToArray() ?? [];
        foreach (var power in copiedStartingPowers)
        {
            power.Power.Validate();
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(power.Amount);
        }
        if (copiedStartingPowers.Select(power => power.Power).Distinct().Count() != copiedStartingPowers.Length)
            throw new ArgumentException("Enemy starting Powers cannot be duplicated.", nameof(startingPowers));
        if (copiedActions.Any(action => action is null))
            throw new ArgumentException("Enemy actions cannot be null.", nameof(actions));
        foreach (var action in copiedActions)
        {
            action.Id.Validate();
            action.AppliedPower?.Validate();
            if (!Enum.IsDefined(action.Intent) || !Enum.IsDefined(action.AppliedPowerTarget))
                throw new ArgumentException($"Enemy action '{action.Id}' has an unsupported intent or power target.", nameof(actions));
            ArgumentOutOfRangeException.ThrowIfNegative(action.IntentDamage);
            if (action.AppliedPower is null && action.AppliedPowerAmount != 0)
                throw new ArgumentException($"Enemy action '{action.Id}' has a power amount without a power ID.", nameof(actions));
        }
        var indexes = copiedActions.Select((action, index) => (action.Id, index))
            .ToDictionary(pair => pair.Id, pair => pair.index);
        foreach (var action in copiedActions)
        {
            if (action.NextActionId is { } next && !indexes.ContainsKey(next))
                throw new ArgumentException($"Enemy action '{action.Id}' references unknown successor '{next}'.", nameof(actions));
            if (action.NextActionId is not null && action.Transition is not null)
                throw new ArgumentException($"Enemy action '{action.Id}' cannot declare both a fixed successor and a transition.", nameof(actions));
            if (action.Transition is { } transition)
            foreach (var choice in transition.Choices)
                if (!indexes.ContainsKey(choice.ActionId))
                    throw new ArgumentException($"Enemy action '{action.Id}' transitions to unknown action '{choice.ActionId}'.", nameof(actions));
        }
        if (startingActionId is not null && startingTransition is not null)
            throw new ArgumentException("An enemy cannot declare both a fixed starting action and a starting transition.", nameof(startingTransition));
        if (startingTransition is { } startTransition)
        foreach (var choice in startTransition.Choices)
            if (!indexes.ContainsKey(choice.ActionId))
                throw new ArgumentException($"Enemy starting transition references unknown action '{choice.ActionId}'.", nameof(startingTransition));
        var limitedActionIds = copiedActions.SelectMany(action => action.Transition?.Choices ?? [])
            .Concat(startingTransition?.Choices ?? [])
            .Where(choice => choice.MaxUsesPerCombat is not null)
            .Select(choice => choice.ActionId)
            .ToHashSet();
        var startId = startingActionId ?? copiedActions.FirstOrDefault()?.Id;
        if (startId is { } starting && !indexes.ContainsKey(starting))
            throw new ArgumentException($"Starting action '{starting}' is not in the enemy action graph.", nameof(startingActionId));
        Id = id;
        MinHp = minHp;
        MaxHp = maxHp;
        Actions = Array.AsReadOnly(copiedActions);
        LoopLastAction = loopLastAction;
        StartingActionIndex = startId is { } value ? indexes[value] : 0;
        StartingTransition = startingTransition;
        StartingPowers = Array.AsReadOnly(copiedStartingPowers);
        _limitedActionIds = limitedActionIds;
        _actionIndexes = new System.Collections.ObjectModel.ReadOnlyDictionary<ContentId, int>(indexes);
    }
    public ContentId Id { get; }
    public int MinHp { get; }
    public int MaxHp { get; }
    public IReadOnlyList<EnemyActionDefinition> Actions { get; }
    public bool LoopLastAction { get; }
    public int StartingActionIndex { get; }
    public EnemyActionTransition? StartingTransition { get; }
    public IReadOnlyList<EnemyStartingPower> StartingPowers { get; }
    internal IReadOnlySet<ContentId> LimitedActionIds => _limitedActionIds;
    internal int GetActionIndex(ContentId actionId) => _actionIndexes[actionId];

    internal int ResolveStartingActionIndex(EnemyActionEvaluationContext context, SeededRandom random) => StartingTransition is { } transition
        ? ResolveTransitionIndex(transition, null, 0, context, random)
        : StartingActionIndex;

    internal int ResolveNextActionIndex(ContentId currentActionId, ContentId? lastActionId,
        int consecutiveActionCount, EnemyActionEvaluationContext context, SeededRandom random)
    {
        var current = Actions[GetActionIndex(currentActionId)];
        if (current.NextActionId is { } next) return GetActionIndex(next);
        if (current.Transition is { } transition)
            return ResolveTransitionIndex(transition, lastActionId, consecutiveActionCount, context, random);
        return currentActionId == Actions[^1].Id && LoopLastAction
            ? Actions.Count - 1
            : Math.Min(GetActionIndex(currentActionId) + 1, Actions.Count - 1);
    }

    private int ResolveTransitionIndex(EnemyActionTransition transition, ContentId? lastActionId,
        int consecutiveActionCount, EnemyActionEvaluationContext context, SeededRandom random)
    {
        if (transition.SelectionMode == EnemyActionSelectionMode.FirstMatchingCondition)
        {
            foreach (var choice in transition.Choices)
                if ((choice.Condition is null || choice.Condition.Evaluate(context))
                    && (choice.MaxUsesPerCombat is not { } maxTotalUses
                        || context.GetActionUseCount(choice.ActionId) < maxTotalUses))
                    return GetActionIndex(choice.ActionId);
            throw new InvalidOperationException("Enemy conditional transition has no matching action for the current combat state.");
        }

        var choices = transition.Choices.Where(choice =>
        {
            if (choice.Condition is { } condition && !condition.Evaluate(context)) return false;
            if (choice.MaxUsesPerCombat is { } maximumTotalUses
                && context.GetActionUseCount(choice.ActionId) >= maximumTotalUses) return false;
            var maximumConsecutiveUses = transition.RepeatPolicy == EnemyActionRepeatPolicy.PreventImmediateRepeat
                ? 1
                : choice.MaxConsecutiveUses;
            if (maximumConsecutiveUses is null || lastActionId != choice.ActionId) return true;
            return consecutiveActionCount < maximumConsecutiveUses.Value;
        }).ToArray();
        if (choices.Length == 0)
            throw new InvalidOperationException("Enemy transition has no eligible action choices for the current combat state.");
        var roll = random.NextInt(choices.Sum(choice => choice.Weight));
        foreach (var choice in choices)
        {
            if (roll < choice.Weight) return GetActionIndex(choice.ActionId);
            roll -= choice.Weight;
        }
        throw new InvalidOperationException("Enemy transition weight selection failed.");
    }
}

public enum CardType { Attack, Skill, Power, Status }
[Flags]
public enum CardKeywords { None = 0, Exhaust = 1, Ethereal = 2, Unplayable = 4, Retain = 8, Innate = 16 }
public enum IntentKind { None, Attack, Buff, Debuff }
public enum PowerTrigger { None, AfterPlayerTurnEnd, AfterEnemyTurnEnd, AfterPlayerTurnStart, AfterCardExhausted,
    AfterCardPlayed, AfterOwnerTurnEnd, AfterOwnerTurnStart, AfterAutoPostPlayPhaseEntered }
public sealed record PowerLifecycleSubscription(PowerTrigger Trigger, EffectNode Effect);
public sealed record DamageModifierSubscription(decimal Multiplier,
    DamageProperties RequiredProperties = DamageProperties.None,
    DamageProperties ExcludedProperties = DamageProperties.None,
    ContentId? RequiredSourcePower = null);
public enum PowerRole { None, Strength, Vulnerable, NoDraw, Artifact }

public interface IRulePackage
{
    PackageInfo Info { get; }
    void Register(ContentRegistry registry);
}

public sealed class ContentRegistry
{
    private readonly Dictionary<ContentId, CardDefinition> _cards = [];
    private readonly Dictionary<ContentId, PotionDefinition> _potions = [];
    private readonly Dictionary<ContentId, CardPoolDefinition> _cardPools = [];
    private readonly Dictionary<ContentId, RelicDefinition> _relics = [];
    private readonly Dictionary<ContentId, EnemyDefinition> _enemies = [];
    private readonly Dictionary<ContentId, EncounterDefinition> _encounters = [];
    private readonly Dictionary<ContentId, RunEventDefinition> _runEvents = [];
    private readonly Dictionary<ContentId, PowerDefinition> _powers = [];
    private readonly Dictionary<ContentId, CharacterDefinition> _characters = [];
    private readonly Dictionary<ContentId, RunDefinition> _runs = [];
    private readonly Dictionary<StateVariableKey, StateVariableDefinition> _stateVariables = [];
    private readonly Dictionary<ContentId, ICustomEffectHandler> _customEffects = [];
    private readonly Dictionary<ContentId, ICustomRunEffectHandler> _customRunEffects = [];
    private readonly List<IRuleHook> _hooks = [];
    private readonly List<IRunHook> _runHooks = [];
    private readonly List<PackageInfo> _packages = [];
    private string? _registeringPackageId;

    public void AddPackage(IRulePackage package)
    {
        ArgumentNullException.ThrowIfNull(package);
        if (_packages.Any(p => p.Id == package.Info.Id))
            throw new InvalidOperationException($"Package '{package.Info.Id}' is already loaded.");
        var cardsBefore = new Dictionary<ContentId, CardDefinition>(_cards);
        var potionsBefore = new Dictionary<ContentId, PotionDefinition>(_potions);
        var cardPoolsBefore = new Dictionary<ContentId, CardPoolDefinition>(_cardPools);
        var relicsBefore = new Dictionary<ContentId, RelicDefinition>(_relics);
        var enemiesBefore = new Dictionary<ContentId, EnemyDefinition>(_enemies);
        var encountersBefore = new Dictionary<ContentId, EncounterDefinition>(_encounters);
        var runEventsBefore = new Dictionary<ContentId, RunEventDefinition>(_runEvents);
        var powersBefore = new Dictionary<ContentId, PowerDefinition>(_powers);
        var charactersBefore = new Dictionary<ContentId, CharacterDefinition>(_characters);
        var runsBefore = new Dictionary<ContentId, RunDefinition>(_runs);
        var stateVariablesBefore = new Dictionary<StateVariableKey, StateVariableDefinition>(_stateVariables);
        var customEffectsBefore = new Dictionary<ContentId, ICustomEffectHandler>(_customEffects);
        var customRunEffectsBefore = new Dictionary<ContentId, ICustomRunEffectHandler>(_customRunEffects);
        var hookCountBefore = _hooks.Count;
        var runHookCountBefore = _runHooks.Count;
        try
        {
            _registeringPackageId = package.Info.Id;
            package.Register(this);
            _packages.Add(package.Info);
        }
        catch
        {
            Restore(_cards, cardsBefore);
            Restore(_potions, potionsBefore);
            Restore(_cardPools, cardPoolsBefore);
            Restore(_relics, relicsBefore);
            Restore(_enemies, enemiesBefore);
            Restore(_encounters, encountersBefore);
            Restore(_runEvents, runEventsBefore);
            Restore(_powers, powersBefore);
            Restore(_characters, charactersBefore);
            Restore(_runs, runsBefore);
            Restore(_stateVariables, stateVariablesBefore);
            Restore(_customEffects, customEffectsBefore);
            Restore(_customRunEffects, customRunEffectsBefore);
            _hooks.RemoveRange(hookCountBefore, _hooks.Count - hookCountBefore);
            _runHooks.RemoveRange(runHookCountBefore, _runHooks.Count - runHookCountBefore);
            throw;
        }
        finally { _registeringPackageId = null; }
    }

    public void Register(CardDefinition definition) => AddUnique(_cards, definition.Id, definition, "card");
    public void Register(PotionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        definition.Validate();
        AddUnique(_potions, definition.Id, definition, "potion");
    }
    public void Register(CardPoolDefinition definition) => AddUnique(_cardPools, definition.Id, definition, "card pool");
    public void Register(RelicDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        definition.Validate();
        AddUnique(_relics, definition.Id, definition, "relic");
    }
    public void Register(EnemyDefinition definition) => AddUnique(_enemies, definition.Id, definition, "enemy");
    public void Register(EncounterDefinition definition) => AddUnique(_encounters, definition.Id, definition, "encounter");
    public void Register(RunEventDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        definition.Validate();
        AddUnique(_runEvents, definition.Id, definition, "run event");
    }
    public void Register(PowerDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        definition.Validate();
        AddUnique(_powers, definition.Id, definition, "power");
    }
    public void Register(CharacterDefinition definition) => AddUnique(_characters, definition.Id, definition, "character");
    public void Register(RunDefinition definition) => AddUnique(_runs, definition.Id, definition, "run");
    public void Register(StateVariableDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        definition.Validate();
        if (_registeringPackageId is { } packageId
            && !definition.Id.Value.StartsWith(packageId + ":", StringComparison.Ordinal))
            throw new InvalidOperationException($"Package '{packageId}' cannot declare another package's state variable '{definition.Id}'.");
        if (!_stateVariables.TryAdd(definition.Key, definition))
            throw new InvalidOperationException($"Duplicate state variable '{definition.Id}' in scope '{definition.Scope}'.");
    }
    public void Register(IRuleHook hook)
    {
        ArgumentNullException.ThrowIfNull(hook);
        hook.Id.Validate();
        if (_hooks.Any(existing => existing.Id == hook.Id))
            throw new InvalidOperationException($"Duplicate hook ID '{hook.Id}'.");
        _hooks.Add(hook);
    }
    public void Register(IRunHook hook)
    {
        ArgumentNullException.ThrowIfNull(hook);
        hook.Id.Validate();
        if (_runHooks.Any(existing => existing.Id == hook.Id))
            throw new InvalidOperationException($"Duplicate run hook ID '{hook.Id}'.");
        _runHooks.Add(hook);
    }
    public void Register(ICustomEffectHandler handler) => AddUnique(_customEffects, handler.Id, handler, "custom effect handler");
    public void Register(ICustomRunEffectHandler handler) => AddUnique(_customRunEffects, handler.Id, handler, "custom run effect handler");

    public void Override(CardDefinition definition) => Replace(_cards, definition.Id, definition, "card");
    public void Override(CardPoolDefinition definition) => Replace(_cardPools, definition.Id, definition, "card pool");
    public void Override(EnemyDefinition definition) => Replace(_enemies, definition.Id, definition, "enemy");
    public void Override(EncounterDefinition definition) => Replace(_encounters, definition.Id, definition, "encounter");
    public void Override(PowerDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        definition.Validate();
        Replace(_powers, definition.Id, definition, "power");
    }
    public void Override(CharacterDefinition definition) => Replace(_characters, definition.Id, definition, "character");
    public void Override(RunDefinition definition) => Replace(_runs, definition.Id, definition, "run");

    public GameDefinition Build()
    {
        if (_packages.Count == 0)
            throw new InvalidOperationException("At least one rules package must be loaded.");
        if (_relics.Values.Count(relic => relic.IsFallback) > 1)
            throw new InvalidOperationException("Only one fallback relic may be registered.");
        foreach (var encounter in _encounters.Values)
        {
            foreach (var enemyId in encounter.PossibleEnemyIds)
                if (!_enemies.ContainsKey(enemyId))
                    throw new InvalidOperationException($"Encounter '{encounter.Id}' references unknown enemy '{enemyId}'.");
            foreach (var (enemyIndex, actionId) in encounter.StartingActionsByEnemyIndex)
            {
                foreach (var enemyId in encounter.GetPossibleEnemyIdsAtSlot(enemyIndex))
                    if (!_enemies[enemyId].Actions.Any(action => action.Id == actionId))
                        throw new InvalidOperationException($"Encounter '{encounter.Id}' assigns unknown starting action '{actionId}' to enemy '{enemyId}'.");
            }
        }
        foreach (var enemy in _enemies.Values)
        foreach (var startingPower in enemy.StartingPowers)
            if (!_powers.ContainsKey(startingPower.Power))
                throw new InvalidOperationException($"Enemy '{enemy.Id}' starts with unknown power '{startingPower.Power}'.");
        foreach (var enemy in _enemies.Values)
        foreach (var action in enemy.Actions)
        {
            if (action.AppliedPower is { } powerId && !_powers.ContainsKey(powerId))
                throw new InvalidOperationException($"Enemy action '{action.Id}' references unknown power '{powerId}'.");
            if (action.Transition is { } transition)
                foreach (var choice in transition.Choices)
                    if (choice.Condition is { } condition)
                        ValidateEnemyActionCondition(condition, $"Enemy action '{action.Id}' transition");
            ValidateEffectReferences(action.Effect, $"Enemy action '{action.Id}'");
        }
        foreach (var enemy in _enemies.Values)
        if (enemy.StartingTransition is { } transition)
            foreach (var choice in transition.Choices)
                if (choice.Condition is { } condition)
                    ValidateEnemyActionCondition(condition, $"Enemy '{enemy.Id}' starting transition");
        foreach (var card in _cards.Values)
        {
            card.Id.Validate();
            if (card.Variables is null || card.Variables.Select(variable => variable.Id).Distinct().Count() != card.Variables.Count)
                throw new InvalidOperationException($"Card '{card.Id}' has null or duplicate variable declarations.");
            foreach (var variable in card.Variables) variable.Id.Validate();
            if (card.Tags is null || card.Tags.Distinct().Count() != card.Tags.Count)
                throw new InvalidOperationException($"Card '{card.Id}' has null or duplicate tags.");
            foreach (var tag in card.Tags) tag.Validate();
            if (card.PoolIds is null || card.PoolIds.Distinct().Count() != card.PoolIds.Count)
                throw new InvalidOperationException($"Card '{card.Id}' has null or duplicate pool memberships.");
            foreach (var poolId in card.PoolIds)
                if (!_cardPools.ContainsKey(poolId))
                    throw new InvalidOperationException($"Card '{card.Id}' references unknown card pool '{poolId}'.");
            if (!Enum.IsDefined(card.Type) || !Enum.IsDefined(card.Target) || !Enum.IsDefined(card.Rarity)
                || !Enum.IsDefined(card.CostKind))
                throw new InvalidOperationException($"Card '{card.Id}' contains an unsupported type, target, rarity, or cost kind.");
            ArgumentOutOfRangeException.ThrowIfNegative(card.Cost);
            if (card.UpgradedCost is { } upgradedCost) ArgumentOutOfRangeException.ThrowIfNegative(upgradedCost);
            if (card.HasEnergyXCost && card.Cost != 0)
                throw new InvalidOperationException($"X-cost card '{card.Id}' must declare a base cost of zero.");
            if ((card.Keywords & ~(CardKeywords.Exhaust | CardKeywords.Ethereal | CardKeywords.Unplayable
                | CardKeywords.Retain | CardKeywords.Innate)) != 0
                || (card.UpgradedKeywords & ~(CardKeywords.Exhaust | CardKeywords.Ethereal | CardKeywords.Unplayable
                    | CardKeywords.Retain | CardKeywords.Innate)) != 0)
                throw new InvalidOperationException($"Card '{card.Id}' contains unsupported keywords.");
            if (card.DamageValueVariable is { } damageVariable
                && !card.Variables.Any(variable => variable.Id == damageVariable))
                throw new InvalidOperationException($"Card '{card.Id}' damage value references undeclared variable '{damageVariable}'.");
            if (card.CalculatedDamageValue is { } calculatedDamage)
                ValidateCombatCountSource(calculatedDamage.Count, $"Card '{card.Id}' damage value");
            if (card.UpgradedCalculatedDamageValue is { } upgradedCalculatedDamage)
                ValidateCombatCountSource(upgradedCalculatedDamage.Count, $"Upgraded card '{card.Id}' damage value");
            ValidateEffectReferences(card.Effect, $"Card '{card.Id}'");
            if (card.UpgradedEffect is { } upgradedEffect)
                ValidateEffectReferences(upgradedEffect, $"Upgraded card '{card.Id}'");
            if (card.OnExhaust is { } exhaustEffect) ValidateEffectReferences(exhaustEffect, $"Card '{card.Id}' exhaust effect");
            if (card.UpgradedOnExhaust is { } upgradedExhaust)
                ValidateEffectReferences(upgradedExhaust, $"Upgraded card '{card.Id}' exhaust effect");
            if (card.OnEnterCombat is { } entryEffect) ValidateEffectReferences(entryEffect, $"Card '{card.Id}' entry effect");
            if (card.UpgradedOnEnterCombat is { } upgradedEntry)
                ValidateEffectReferences(upgradedEntry, $"Upgraded card '{card.Id}' entry effect");
            if (card.AfterAutoPostPlay is { } postPlay)
                ValidateEffectReferences(postPlay, $"Card '{card.Id}' after auto post-play");
            if (card.UpgradedAfterAutoPostPlay is { } upgradedPostPlay)
                ValidateEffectReferences(upgradedPostPlay, $"Upgraded card '{card.Id}' after auto post-play");
            foreach (var listener in new[] { card.BeforeCardPlayed, card.UpgradedBeforeCardPlayed,
                card.AfterCardPlayed, card.UpgradedAfterCardPlayed })
            {
                if (listener is null) continue;
                if (listener.WhenCardType is { } type && !Enum.IsDefined(type))
                    throw new InvalidOperationException($"Card '{card.Id}' listens to an invalid card type.");
                ValidateEffectReferences(listener.Effect, $"Card '{card.Id}' play listener");
            }
        }
        foreach (var potion in _potions.Values)
            ValidateEffectReferences(potion.Effect, $"Potion '{potion.Id}'");
        foreach (var pool in _cardPools.Values)
            foreach (var cardId in pool.Cards)
                if (!_cards.ContainsKey(cardId))
                    throw new InvalidOperationException($"Card pool '{pool.Id}' references unknown card '{cardId}'.");
        var resolvedCardPools = _cardPools.ToDictionary(pair => pair.Key, pair =>
            new CardPoolDefinition(pair.Key, pair.Value.Cards.Concat(_cards.Values
                .Where(card => card.PoolIds.Contains(pair.Key) && !pair.Value.Cards.Contains(card.Id))
                .Select(card => card.Id))));
        foreach (var character in _characters.Values)
        {
            foreach (var cardId in character.StartingDeck)
                if (!_cards.ContainsKey(cardId))
                    throw new InvalidOperationException($"Character '{character.Id}' starts with unknown card '{cardId}'.");
            foreach (var poolId in character.CardPools)
                if (!_cardPools.ContainsKey(poolId))
                    throw new InvalidOperationException($"Character '{character.Id}' references unknown card pool '{poolId}'.");
            foreach (var relicId in character.StartingRelics)
                if (!_relics.ContainsKey(relicId))
                    throw new InvalidOperationException($"Character '{character.Id}' starts with unknown relic '{relicId}'.");
            foreach (var potionId in character.StartingPotions)
                if (!_potions.ContainsKey(potionId))
                    throw new InvalidOperationException($"Character '{character.Id}' starts with unknown potion '{potionId}'.");
        }
        foreach (var relic in _relics.Values)
            foreach (var characterId in relic.CharacterPoolIds ?? Array.Empty<ContentId>())
                if (!_characters.ContainsKey(characterId))
                    throw new InvalidOperationException($"Relic '{relic.Id}' references unknown character pool '{characterId}'.");
        foreach (var potion in _potions.Values)
            foreach (var characterId in potion.CharacterPoolIds ?? Array.Empty<ContentId>())
                if (!_characters.ContainsKey(characterId))
                    throw new InvalidOperationException($"Potion '{potion.Id}' references unknown character pool '{characterId}'.");
        foreach (var power in _powers.Values)
        {
            if (power.Variables is null || power.Variables.Select(variable => variable.Id).Distinct().Count() != power.Variables.Count)
                throw new InvalidOperationException($"Power '{power.Id}' has null or duplicate variable declarations.");
            foreach (var variable in power.Variables) variable.Id.Validate();
            if (power.TriggerEffect is { } effect && !_powers.ContainsKey(effect))
                throw new InvalidOperationException($"Power '{power.Id}' triggers unknown power '{effect}'.");
            if (power.TriggerRuleEffect is { } triggerEffect)
                ValidateEffectReferences(triggerEffect, $"Power '{power.Id}' trigger");
            foreach (var subscription in power.Events)
            {
                if (subscription.AffectedPower is { } affectedPower && !_powers.ContainsKey(affectedPower))
                    throw new InvalidOperationException($"Power '{power.Id}' filters an unknown affected Power '{affectedPower}'.");
                if (subscription.CardTag is { } cardTag
                    && !_cards.Values.Any(card => card.Tags.Contains(cardTag)))
                    throw new InvalidOperationException($"Power '{power.Id}' filters unknown card tag '{cardTag}'.");
                ValidateEffectReferences(subscription.Effect, $"Power '{power.Id}' event '{subscription.EventType}'");
            }
            foreach (var subscription in power.Lifecycle)
                ValidateEffectReferences(subscription.Effect, $"Power '{power.Id}' lifecycle '{subscription.Trigger}'");
            foreach (var modifier in power.IncomingDamageModifiers)
                if (modifier.RequiredSourcePower is { } requiredPower && !_powers.ContainsKey(requiredPower))
                    throw new InvalidOperationException($"Power '{power.Id}' requires unknown source Power '{requiredPower}' for its damage modifier.");
            foreach (var adjustment in new[] { power.OnStackChange, power.OnExpire })
                if (adjustment is not null && !_powers.ContainsKey(adjustment.Power))
                    throw new InvalidOperationException($"Power '{power.Id}' references unknown lifecycle power '{adjustment.Power}'.");
        }
        foreach (var power in _powers.Values)
        {
            var visited = new HashSet<ContentId>();
            var current = power;
            while (current.OnStackChange is { } adjustment)
            {
                if (!visited.Add(current.Id))
                    throw new InvalidOperationException($"Power '{power.Id}' has a recursive stack-change link.");
                current = _powers[adjustment.Power];
            }
        }
        foreach (var runEvent in _runEvents.Values)
        foreach (var page in runEvent.Pages.Values)
        foreach (var option in page.Options)
        {
            if (option.NextPageId is { } nextPageId && !runEvent.Pages.ContainsKey(nextPageId))
                throw new InvalidOperationException($"Run event '{runEvent.Id}' option '{option.Id}' references unknown page '{nextPageId}'.");
            if (option.Effects.Count(effect => effect is ChooseRunCardEffect) > 1)
                throw new InvalidOperationException($"Run event '{runEvent.Id}' option '{option.Id}' can select one card at a time.");
            var winEffectCount = option.Effects.Count(effect => effect is WinRunEffect);
            var endsWithWin = option.Effects.LastOrDefault() is WinRunEffect;
            if (runEvent.Kind == RunEventKind.Final && option.NextPageId is null
                && (winEffectCount != 1 || !endsWithWin))
                throw new InvalidOperationException($"Final event '{runEvent.Id}' option '{option.Id}' must end with WinRunEffect.");
            if ((runEvent.Kind != RunEventKind.Final || option.NextPageId is not null)
                && option.Effects.Any(effect => effect is WinRunEffect))
                throw new InvalidOperationException($"Only a final event option can win the run ('{runEvent.Id}/{option.Id}').");
            if (option.Requirement is { } requirement)
                ValidateRunConditionReferences(requirement, $"Run event '{runEvent.Id}' option '{option.Id}'");
            foreach (var effect in option.Effects)
                ValidateRunEffectReferences(effect, $"Run event '{runEvent.Id}' option '{option.Id}'");
        }
        foreach (var run in _runs.Values)
        foreach (var act in run.Acts)
        {
            act.EncounterPool?.Validate(_encounters);
            ValidateRunEventPool(act.EventIds, RunEventKind.Event, _runEvents);
            ValidateRunEventPool(act.AncientIds, RunEventKind.Ancient, _runEvents);
        }
        foreach (var run in _runs.Values)
        {
            ValidateRunEventPool(run.SharedEventIds, RunEventKind.Event, _runEvents);
            ValidateRunEventPool(run.SharedAncientIds, RunEventKind.Ancient, _runEvents);
            if (run.FinalEventId is { } finalEventId
                && (!_runEvents.TryGetValue(finalEventId, out var finalEvent)
                    || finalEvent.Kind != RunEventKind.Final))
                throw new InvalidOperationException($"Run '{run.Id}' references missing or non-final event '{finalEventId}'.");
        }
        return new GameDefinition(
            _packages.ToArray(),
            new ReadOnlyDictionary<ContentId, CardDefinition>(new Dictionary<ContentId, CardDefinition>(_cards)),
            new ReadOnlyDictionary<ContentId, PotionDefinition>(new Dictionary<ContentId, PotionDefinition>(_potions)),
            new ReadOnlyDictionary<ContentId, CardPoolDefinition>(resolvedCardPools),
            new ReadOnlyDictionary<ContentId, RelicDefinition>(new Dictionary<ContentId, RelicDefinition>(_relics)),
            new ReadOnlyDictionary<ContentId, EnemyDefinition>(new Dictionary<ContentId, EnemyDefinition>(_enemies)),
            new ReadOnlyDictionary<ContentId, EncounterDefinition>(new Dictionary<ContentId, EncounterDefinition>(_encounters)),
            new ReadOnlyDictionary<ContentId, RunEventDefinition>(new Dictionary<ContentId, RunEventDefinition>(_runEvents)),
            new ReadOnlyDictionary<ContentId, PowerDefinition>(new Dictionary<ContentId, PowerDefinition>(_powers)),
            new ReadOnlyDictionary<ContentId, CharacterDefinition>(new Dictionary<ContentId, CharacterDefinition>(_characters)),
            new ReadOnlyDictionary<ContentId, RunDefinition>(new Dictionary<ContentId, RunDefinition>(_runs)),
            new ReadOnlyDictionary<StateVariableKey, StateVariableDefinition>(
                new Dictionary<StateVariableKey, StateVariableDefinition>(_stateVariables)),
            new ReadOnlyDictionary<ContentId, ICustomEffectHandler>(new Dictionary<ContentId, ICustomEffectHandler>(_customEffects)),
            new ReadOnlyDictionary<ContentId, ICustomRunEffectHandler>(new Dictionary<ContentId, ICustomRunEffectHandler>(_customRunEffects)),
            _hooks.ToArray(), _runHooks.ToArray());
    }

    private static void ValidateRunEventPool(IEnumerable<ContentId> ids, RunEventKind expectedKind,
        IReadOnlyDictionary<ContentId, RunEventDefinition> definitions)
    {
        foreach (var id in ids)
        {
            if (!definitions.TryGetValue(id, out var definition))
                throw new InvalidOperationException($"Run event pool references unknown content '{id}'.");
            if (definition.Kind != expectedKind)
                throw new InvalidOperationException($"Run event '{id}' has kind '{definition.Kind}', expected '{expectedKind}'.");
        }
    }

    private void ValidateRunEffectReferences(RunEffectNode effect, string owner,
        bool hasSelectedCardContext = false)
    {
        switch (effect)
        {
            case GainRunGoldEffect gainGold:
                ArgumentOutOfRangeException.ThrowIfNegative(gainGold.Amount);
                break;
            case LoseRunGoldEffect loseGold:
                ArgumentOutOfRangeException.ThrowIfNegative(loseGold.Amount);
                break;
            case HealRunEffect heal:
                ArgumentOutOfRangeException.ThrowIfNegative(heal.Amount);
                break;
            case GainRunMaxHpEffect gainMaxHp:
                ArgumentOutOfRangeException.ThrowIfNegative(gainMaxHp.Amount);
                break;
            case LoseRunMaxHpEffect loseMaxHp:
                ArgumentOutOfRangeException.ThrowIfNegative(loseMaxHp.Amount);
                break;
            case LoseRunHpEffect loseHp:
                ArgumentOutOfRangeException.ThrowIfNegative(loseHp.Amount);
                break;
            case AddCardToRunDeckEffect addCard when !_cards.ContainsKey(addCard.Card):
                throw new InvalidOperationException($"{owner} adds unknown card '{addCard.Card}'.");
            case AddCardToRunDeckEffect addCard:
                addCard.Card.Validate();
                break;
            case ChooseRunCardEffect chooseCard:
                ArgumentException.ThrowIfNullOrWhiteSpace(chooseCard.Prompt);
                if (!Enum.IsDefined(chooseCard.Filter))
                    throw new InvalidOperationException($"{owner} has an invalid run card filter.");
                if (chooseCard.OnSelected is not (RemoveSelectedRunCardEffect or UpgradeSelectedRunCardEffect))
                    throw new InvalidOperationException($"{owner} must remove or upgrade the selected card.");
                if (chooseCard.OnSelected is UpgradeSelectedRunCardEffect
                    && chooseCard.Filter != RunCardFilter.Upgradeable)
                    throw new InvalidOperationException($"{owner} must filter for upgradeable cards before upgrading.");
                ValidateRunEffectReferences(chooseCard.OnSelected, owner, hasSelectedCardContext: true);
                break;
            case RemoveSelectedRunCardEffect or UpgradeSelectedRunCardEffect when !hasSelectedCardContext:
                throw new InvalidOperationException($"{owner} uses a selected-card effect without selecting a card.");
            case RemoveSelectedRunCardEffect or UpgradeSelectedRunCardEffect:
                break;
            case WinRunEffect:
                break;
            case GainRunRelicEffect gainRelic:
                gainRelic.RelicId.Validate();
                if (!_relics.ContainsKey(gainRelic.RelicId))
                    throw new InvalidOperationException($"{owner} grants unknown relic '{gainRelic.RelicId}'.");
                break;
            case EnterRunCombatEffect enterCombat:
                enterCombat.EncounterId.Validate();
                if (!_encounters.TryGetValue(enterCombat.EncounterId, out var encounter))
                    throw new InvalidOperationException($"{owner} starts unknown encounter '{enterCombat.EncounterId}'.");
                if (encounter.RoomType is not (RunRoomType.Monster or RunRoomType.Elite or RunRoomType.Boss))
                    throw new InvalidOperationException($"{owner} cannot start non-combat encounter '{encounter.Id}'.");
                break;
            case CustomRunEffect custom when !_customRunEffects.ContainsKey(custom.Handler):
                throw new InvalidOperationException($"{owner} references unknown custom run effect '{custom.Handler}'.");
            case CustomRunEffect custom:
                custom.Handler.Validate();
                break;
            default:
                throw new InvalidOperationException($"{owner} uses unsupported run effect '{effect.GetType().Name}'.");
        }
    }

    private void ValidateRunConditionReferences(RunCondition condition, string owner)
    {
        switch (condition)
        {
            case RunMetricCondition metric:
                if (!Enum.IsDefined(metric.Metric) || !Enum.IsDefined(metric.Comparison))
                    throw new InvalidOperationException($"{owner} has an invalid run metric condition.");
                ArgumentOutOfRangeException.ThrowIfNegative(metric.Threshold);
                break;
            case HasRunRelicCondition hasRelic when !_relics.ContainsKey(hasRelic.Relic):
                throw new InvalidOperationException($"{owner} checks unknown relic '{hasRelic.Relic}'.");
            case HasRunRelicCondition hasRelic:
                hasRelic.Relic.Validate();
                break;
            case HasRunPotionCondition hasPotion when !_potions.ContainsKey(hasPotion.Potion):
                throw new InvalidOperationException($"{owner} checks unknown potion '{hasPotion.Potion}'.");
            case HasRunPotionCondition hasPotion:
                hasPotion.Potion.Validate();
                break;
            case AllRunCondition all:
                foreach (var nested in all.Conditions) ValidateRunConditionReferences(nested, owner);
                break;
            case AnyRunCondition any:
                foreach (var nested in any.Conditions) ValidateRunConditionReferences(nested, owner);
                break;
            case NotRunCondition not:
                ValidateRunConditionReferences(not.Condition, owner);
                break;
            default:
                throw new InvalidOperationException($"{owner} uses an unsupported run condition '{condition.GetType().Name}'.");
        }
    }

    private void ValidateEnemyActionCondition(EnemyActionCondition condition, string owner)
    {
        switch (condition)
        {
            case EnemyMetricCondition metric:
                if (!Enum.IsDefined(metric.Metric) || !Enum.IsDefined(metric.Comparison))
                    throw new InvalidOperationException($"{owner} has an invalid enemy metric condition.");
                break;
            case EnemyPowerCondition power when !_powers.ContainsKey(power.PowerId):
                throw new InvalidOperationException($"{owner} checks unknown power '{power.PowerId}'.");
            case EnemyPowerCondition power:
                power.PowerId.Validate();
                if (!Enum.IsDefined(power.Side) || !Enum.IsDefined(power.Comparison))
                    throw new InvalidOperationException($"{owner} has an invalid enemy power condition.");
                break;
            case AllEnemyCondition all when all.Conditions.Count == 0:
                throw new InvalidOperationException($"{owner} has an empty all condition.");
            case AllEnemyCondition all:
                foreach (var nested in all.Conditions) ValidateEnemyActionCondition(nested, owner);
                break;
            case AnyEnemyCondition any when any.Conditions.Count == 0:
                throw new InvalidOperationException($"{owner} has an empty any condition.");
            case AnyEnemyCondition any:
                foreach (var nested in any.Conditions) ValidateEnemyActionCondition(nested, owner);
                break;
            case NotEnemyCondition not:
                ValidateEnemyActionCondition(not.Condition, owner);
                break;
            default:
                throw new InvalidOperationException($"{owner} uses unsupported enemy condition '{condition.GetType().Name}'.");
        }
    }

    private static bool ValidDamageProperties(DamageProperties properties) =>
        (properties & ~(DamageProperties.Move | DamageProperties.Unpowered
            | DamageProperties.Unblockable | DamageProperties.SkipHurtAnimation)) == 0;

    private void ValidateEffectReferences(EffectNode effect, string owner, IReadOnlySet<string>? counts = null)
    {
        switch (effect)
        {
            case WithCountEffect binding:
                ArgumentException.ThrowIfNullOrWhiteSpace(binding.Name);
                ValidateCombatCountSource(binding.Count, owner, counts);
                var innerCounts = new HashSet<string>(counts ?? new HashSet<string>(), StringComparer.Ordinal)
                    { binding.Name };
                ValidateEffectReferences(binding.Effect, owner, innerCounts);
                break;
            case SequenceEffect sequence:
                foreach (var child in sequence.Effects) ValidateEffectReferences(child, owner, counts);
                break;
            case AttackEffect attack:
                ArgumentOutOfRangeException.ThrowIfNegative(attack.Amount);
                if (!Enum.IsDefined(attack.Target)
                    || (attack.Properties & ~(DamageProperties.Move | DamageProperties.Unpowered
                        | DamageProperties.Unblockable | DamageProperties.SkipHurtAnimation)) != 0)
                    throw new InvalidOperationException($"{owner} has invalid attack damage properties or target.");
                if (attack.OnFatal is { } attackFatal) ValidateEffectReferences(attackFatal, owner, counts);
                break;
            case AttackCardVariableEffect dynamicAttack:
                if (dynamicAttack.HitCount < 0 || dynamicAttack.Target is not
                    (TargetSelector.Self or TargetSelector.SelectedTarget or TargetSelector.FirstEnemy or TargetSelector.AllOpponents)
                    || !ValidDamageProperties(dynamicAttack.Properties))
                    throw new InvalidOperationException($"{owner} has an invalid dynamic card attack.");
                ValidateCardVariableReference(dynamicAttack.Variable, CardContext.Played, owner);
                break;
            case ScaledAttackEffect scaled:
                ArgumentOutOfRangeException.ThrowIfNegative(scaled.BaseAmount);
                ArgumentOutOfRangeException.ThrowIfNegative(scaled.AmountPerCount);
                if (scaled.Target is not (TargetSelector.Self or TargetSelector.SelectedTarget
                    or TargetSelector.FirstEnemy or TargetSelector.AllOpponents))
                    throw new InvalidOperationException($"{owner} has an invalid scaled attack target.");
                if (!ValidDamageProperties(scaled.Properties))
                    throw new InvalidOperationException($"{owner} has invalid scaled attack properties.");
                ValidateCombatCountSource(scaled.Count, owner, counts);
                if (scaled.OnFatal is { } scaledFatal) ValidateEffectReferences(scaledFatal, owner, counts);
                break;
            case ApplyScaledPowerEffect scaledPower:
                if (!_powers.ContainsKey(scaledPower.Power))
                    throw new InvalidOperationException($"{owner} applies unknown power '{scaledPower.Power}'.");
                ArgumentOutOfRangeException.ThrowIfNegative(scaledPower.BaseAmount);
                ArgumentOutOfRangeException.ThrowIfNegative(scaledPower.AmountPerCount);
                if (scaledPower.Target is not (TargetSelector.Self or TargetSelector.SelectedTarget
                    or TargetSelector.FirstEnemy))
                    throw new InvalidOperationException($"{owner} has an invalid scaled power target.");
                ValidateCombatCountSource(scaledPower.Count, owner, counts);
                break;
            case ApplyPowerEffect applyPower when !_powers.ContainsKey(applyPower.Power):
                throw new InvalidOperationException($"{owner} applies unknown power '{applyPower.Power}'.");
            case LoseHpEffect loseHp:
                if (!Enum.IsDefined(loseHp.Target))
                    throw new InvalidOperationException($"{owner} has an invalid HP loss target.");
                ArgumentOutOfRangeException.ThrowIfNegative(loseHp.Amount);
                break;
            case HealEffect heal:
                if (!Enum.IsDefined(heal.Target))
                    throw new InvalidOperationException($"{owner} has an invalid heal target.");
                ArgumentOutOfRangeException.ThrowIfNegative(heal.Amount);
                break;
            case RandomAttackEffect randomAttack:
                ArgumentOutOfRangeException.ThrowIfNegative(randomAttack.Amount);
                if (!ValidDamageProperties(randomAttack.Properties))
                    throw new InvalidOperationException($"{owner} has invalid random attack properties.");
                if (randomAttack.CalculatedAmount is { } calculatedRandomAttack)
                {
                    ArgumentOutOfRangeException.ThrowIfNegative(calculatedRandomAttack.BaseAmount);
                    ArgumentOutOfRangeException.ThrowIfNegative(calculatedRandomAttack.PerCount);
                    ValidateCombatCountSource(calculatedRandomAttack.Count, owner, counts);
                }
                break;
            case DrawCardsEffect draw:
                ArgumentOutOfRangeException.ThrowIfNegative(draw.Amount);
                if (draw.CalculatedAmount is { } calculatedDraw)
                {
                    ArgumentOutOfRangeException.ThrowIfNegative(calculatedDraw.BaseAmount);
                    ArgumentOutOfRangeException.ThrowIfNegative(calculatedDraw.PerCount);
                    ValidateCombatCountSource(calculatedDraw.Count, owner, counts);
                }
                break;
            case DrawUntilCardTypeEffect drawUntil:
                if (!Enum.IsDefined(drawUntil.ContinueWhileType))
                    throw new InvalidOperationException($"{owner} draws until an invalid card type.");
                break;
            case GainBlockEffect gainBlock:
                ArgumentOutOfRangeException.ThrowIfNegative(gainBlock.Amount);
                if (gainBlock.PerPowerStack is { } blockPower && !_powers.ContainsKey(blockPower))
                    throw new InvalidOperationException($"{owner} scales block by unknown power '{blockPower}'.");
                break;
            case GainBlockFromCurrentBlockEffect gainBlockFromBlock:
                ArgumentOutOfRangeException.ThrowIfNegative(gainBlockFromBlock.Multiplier);
                break;
            case AttackUsingBlockEffect attackUsingBlock when !Enum.IsDefined(attackUsingBlock.Target)
                || attackUsingBlock.Target == TargetSelector.RandomEnemy:
                throw new InvalidOperationException($"{owner} has an invalid block-based attack target.");
            case AddCardsToPileEffect addCards:
                addCards.Card.Validate();
                if (!_cards.ContainsKey(addCards.Card))
                    throw new InvalidOperationException($"{owner} adds unknown card '{addCards.Card}'.");
                if (!Enum.IsDefined(addCards.Pile) || !Enum.IsDefined(addCards.Target))
                    throw new InvalidOperationException($"{owner} has an invalid card destination or target.");
                ArgumentOutOfRangeException.ThrowIfNegative(addCards.Count);
                break;
            case SummonEnemyEffect summon:
                if (!_enemies.ContainsKey(summon.Enemy))
                    throw new InvalidOperationException($"{owner} summons unknown enemy '{summon.Enemy}'.");
                ArgumentOutOfRangeException.ThrowIfNegativeOrZero(summon.Count);
                break;
            case ChooseCardFromPileEffect chooseCard:
                if (!Enum.IsDefined(chooseCard.From)
                    || chooseCard.To is { } destination && !Enum.IsDefined(destination)
                    || !Enum.IsDefined(chooseCard.DestinationPosition)
                    || !Enum.IsDefined(chooseCard.Filter))
                    throw new InvalidOperationException($"{owner} has an invalid card selection pile or destination.");
                ArgumentException.ThrowIfNullOrWhiteSpace(chooseCard.Prompt);
                if (chooseCard.OnSelected is { } onSelected) ValidateEffectReferences(onSelected, owner, counts);
                break;
            case ChooseCardsFromPileEffect chooseCards:
                if (!Enum.IsDefined(chooseCards.From) || !Enum.IsDefined(chooseCards.Filter))
                    throw new InvalidOperationException($"{owner} has an invalid multi-card selection pile or filter.");
                ArgumentException.ThrowIfNullOrWhiteSpace(chooseCards.Prompt);
                ArgumentOutOfRangeException.ThrowIfNegative(chooseCards.MinChoices);
                if (chooseCards.MaxChoices < chooseCards.MinChoices)
                    throw new InvalidOperationException($"{owner} has invalid multi-card selection bounds.");
                if (chooseCards.OnSelected is { } eachSelected) ValidateEffectReferences(eachSelected, owner, counts);
                break;
            case RandomlyMoveCardFromPileEffect randomCard:
                if (!Enum.IsDefined(randomCard.From) || !Enum.IsDefined(randomCard.To)
                    || !Enum.IsDefined(randomCard.Filter))
                    throw new InvalidOperationException($"{owner} has an invalid random card selection pile or filter.");
                if (randomCard.AfterMove is { } afterRandomMove)
                    ValidateEffectReferences(afterRandomMove, owner, counts);
                break;
            case RandomlyMoveCardsEffect randomCards:
                if (!Enum.IsDefined(randomCards.From) || !Enum.IsDefined(randomCards.To)
                    || !Enum.IsDefined(randomCards.Filter))
                    throw new InvalidOperationException($"{owner} has invalid random multi-card selection settings.");
                ArgumentOutOfRangeException.ThrowIfNegative(randomCards.Count);
                if (randomCards.CalculatedCount is { } calculatedMoveCount)
                {
                    ArgumentOutOfRangeException.ThrowIfNegative(calculatedMoveCount.BaseAmount);
                    ArgumentOutOfRangeException.ThrowIfNegative(calculatedMoveCount.PerCount);
                    ValidateCombatCountSource(calculatedMoveCount.Count, owner, counts);
                }
                if (randomCards.AfterMove is { } afterMove) ValidateEffectReferences(afterMove, owner, counts);
                break;
            case ForEachCardInPileEffect eachCard:
                if (!Enum.IsDefined(eachCard.From) || !Enum.IsDefined(eachCard.Filter))
                    throw new InvalidOperationException($"{owner} has an invalid card iteration pile or filter.");
                ValidateEffectReferences(eachCard.Effect, owner, counts);
                break;
            case MoveSelectedCardToPileEffect moveSelectedCard:
                if (!Enum.IsDefined(moveSelectedCard.From) || !Enum.IsDefined(moveSelectedCard.To)
                    || !Enum.IsDefined(moveSelectedCard.DestinationPosition))
                    throw new InvalidOperationException($"{owner} moves a selected card to an invalid pile.");
                break;
            case UpgradeCardsInPileEffect upgradeCards when !Enum.IsDefined(upgradeCards.Pile):
                throw new InvalidOperationException($"{owner} upgrades an invalid card pile.");
            case UpgradeCardsInPileEffect:
            case UpgradeSelectedCardEffect:
            case DowngradeSelectedCardEffect:
            case RetainSelectedCardEffect:
                break;
            case CloneCardEffect clone:
                if (!Enum.IsDefined(clone.Card) || !Enum.IsDefined(clone.Pile) || clone.Pile == CardPile.Play || clone.Count < 0)
                    throw new InvalidOperationException($"{owner} has an invalid combat clone context, destination or count.");
                if (clone.CalculatedCount is { } calculatedClone)
                    ValidateCombatCountSource(calculatedClone.Count, owner, counts);
                break;
            case ModifyCardVariableEffect variable:
                ValidateCardVariableReference(variable.Variable, variable.Card, owner);
                if (!Enum.IsDefined(variable.Operation))
                    throw new InvalidOperationException($"{owner} has an unsupported card variable operation.");
                if (variable.CalculatedAmount is { } variableAmount)
                    ValidateCombatCountSource(variableAmount.Count, owner, counts);
                break;
            case AccumulateCardDamageEffect accumulateDamage:
                ValidateCardVariableReference(accumulateDamage.Variable, accumulateDamage.TargetCard, owner);
                if (!Enum.IsDefined(accumulateDamage.SourceCard))
                    throw new InvalidOperationException($"{owner} reads card damage from an invalid context.");
                break;
            case ModifyPowerVariableEffect variable:
                ValidatePowerVariableReference(variable.Power, variable.Variable, owner);
                if (variable.Target is not (TargetSelector.Self or TargetSelector.SelectedTarget or TargetSelector.FirstEnemy))
                    throw new InvalidOperationException($"{owner} modifies a Power variable on an invalid target.");
                break;
            case GainMaxHpEffect gainMaxHp:
                ArgumentOutOfRangeException.ThrowIfNegative(gainMaxHp.Amount);
                break;
            case ModifySelectedCardCostEffect cost:
                cost.Modifier.Validate();
                if (cost.CalculatedAmount is { } calculatedCost)
                    ValidateCombatCountSource(calculatedCost.Count, owner, counts);
                break;
            case TransformSelectedCardEffect transform:
                transform.Card.Validate();
                if (!_cards.ContainsKey(transform.Card))
                    throw new InvalidOperationException($"{owner} transforms into unknown card '{transform.Card}'.");
                if (!Enum.IsDefined(transform.From) || transform.From == CardPile.Play)
                    throw new InvalidOperationException($"{owner} transforms from an unsupported source pile.");
                break;
            case DecrementPowerEffect decrementPower when !_powers.ContainsKey(decrementPower.Power):
                throw new InvalidOperationException($"{owner} decrements unknown power '{decrementPower.Power}'.");
            case RemovePowerEffect removePower when !_powers.ContainsKey(removePower.Power):
                throw new InvalidOperationException($"{owner} removes unknown power '{removePower.Power}'.");
            case ExpirePowerEffect expirePower when !_powers.ContainsKey(expirePower.Power):
                throw new InvalidOperationException($"{owner} expires unknown power '{expirePower.Power}'.");
            case ChooseEffect choice:
                foreach (var option in choice.Options)
                    if (option.Effect is { } optionEffect) ValidateEffectReferences(optionEffect, owner, counts);
                break;
            case IfEffect conditional:
                ValidateEffectPredicate(conditional.Predicate, owner);
                ValidateEffectReferences(conditional.Then, owner, counts);
                if (conditional.Else is { } elseEffect) ValidateEffectReferences(elseEffect, owner, counts);
                break;
            case RepeatEffect repeat:
                ArgumentOutOfRangeException.ThrowIfNegative(repeat.Count);
                ValidateEffectReferences(repeat.Effect, owner, counts);
                break;
            case GeneratePotionEffect generatePotion:
                ArgumentOutOfRangeException.ThrowIfNegativeOrZero(generatePotion.Count);
                break;
            case GenerateCombatCardsEffect generateCards:
                ArgumentOutOfRangeException.ThrowIfNegative(generateCards.Count);
                if (!Enum.IsDefined(generateCards.Pile) || generateCards.Pile == CardPile.Play)
                    throw new InvalidOperationException($"{owner} generates cards into an invalid pile.");
                if (generateCards.CalculatedCount is { } generatedCount)
                    ValidateCombatCountSource(generatedCount.Count, owner, counts);
                if (generateCards.Type is { } generatedType && !Enum.IsDefined(generatedType))
                    throw new InvalidOperationException($"{owner} generates an invalid card type.");
                generateCards.Cost?.Validate();
                break;
            case ModifyEnergyEffect { CalculatedAmount: { } amount }:
                ValidateCombatCountSource(amount.Count, owner, counts);
                break;
            case RepeatForCountEffect repeat:
                ArgumentOutOfRangeException.ThrowIfNegative(repeat.BaseCount);
                ArgumentOutOfRangeException.ThrowIfNegative(repeat.PerCount);
                ValidateCombatCountSource(repeat.Count, owner, counts);
                ValidateEffectReferences(repeat.Effect, owner, counts);
                break;
            case AutoPlayCardsEffect autoPlay:
                ArgumentOutOfRangeException.ThrowIfNegative(autoPlay.Count);
                if (autoPlay.From != CardPile.Draw || !Enum.IsDefined(autoPlay.Position))
                    throw new InvalidOperationException($"{owner} has an unsupported automatic-play source or position.");
                if (autoPlay.CalculatedCount is { } count) ValidateCombatCountSource(count.Count, owner, counts);
                break;
            case AutoPlayRandomCardsEffect randomAutoPlay:
                ArgumentOutOfRangeException.ThrowIfNegative(randomAutoPlay.Count);
                if (!Enum.IsDefined(randomAutoPlay.From) || randomAutoPlay.From == CardPile.Play
                    || randomAutoPlay.Type is { } randomType && !Enum.IsDefined(randomType))
                    throw new InvalidOperationException($"{owner} has an invalid random automatic-play source or type.");
                if (randomAutoPlay.CalculatedCount is { } randomCount)
                    ValidateCombatCountSource(randomCount.Count, owner, counts);
                break;
            case AutoPlaySelectedCardEffect selectedAutoPlay:
                if (!Enum.IsDefined(selectedAutoPlay.From) || selectedAutoPlay.From == CardPile.Play)
                    throw new InvalidOperationException($"{owner} has an invalid selected automatic-play source.");
                break;
            case RepeatForXCostEffect repeatForX:
                ValidateEffectReferences(repeatForX.Effect, owner, counts);
                break;
            case CustomEffect custom when !_customEffects.ContainsKey(custom.Handler):
                throw new InvalidOperationException($"{owner} references unknown custom effect '{custom.Handler}'.");
        }
    }

    private void ValidateEffectPredicate(EffectPredicate predicate, string owner)
    {
        switch (predicate)
        {
            case TargetAlivePredicate or CardExhaustedThisTurnPredicate or PlayerLostHpThisTurnPredicate or SelectedCardIsClonePredicate:
                break;
            case HasPowerPredicate hasPower when !_powers.ContainsKey(hasPower.Power):
                throw new InvalidOperationException($"{owner} checks unknown power '{hasPower.Power}'.");
            case HasPowerPredicate hasPower when hasPower.Target is not
                (TargetSelector.Self or TargetSelector.SelectedTarget or TargetSelector.FirstEnemy):
                throw new InvalidOperationException($"{owner} checks power on an invalid target.");
            case HasPowerPredicate:
                break;
            case EnergyAtLeastPredicate energy:
                ArgumentOutOfRangeException.ThrowIfNegative(energy.Amount);
                break;
            case PileCountAtLeastPredicate pile:
                if (!Enum.IsDefined(pile.Pile) || pile.Count < 0)
                    throw new InvalidOperationException($"{owner} has an invalid pile-count condition.");
                break;
            case CardPlayCountEqualsPredicate plays:
                if (!Enum.IsDefined(plays.Phase) || plays.Count < 0
                    || plays.Type is { } playType && !Enum.IsDefined(playType))
                    throw new InvalidOperationException($"{owner} has an invalid card-play history condition.");
                break;
            default:
                throw new InvalidOperationException($"{owner} has an unsupported effect predicate '{predicate.GetType().Name}'.");
        }
    }

    private void ValidateCombatCountSource(CombatCountSource source, string owner, IReadOnlySet<string>? counts = null)
    {
        switch (source)
        {
            case PileCardCountSource pile when !Enum.IsDefined(pile.Pile):
                throw new InvalidOperationException($"{owner} counts an invalid card pile.");
            case PileCardCountSource pile when pile.Type is { } type && !Enum.IsDefined(type):
                throw new InvalidOperationException($"{owner} counts an invalid card type.");
            case TargetPowerCountSource power when !_powers.ContainsKey(power.Power):
                throw new InvalidOperationException($"{owner} counts unknown power '{power.Power}'.");
            case TargetPowerCountSource power when power.Target is not
                (TargetSelector.Self or TargetSelector.SelectedTarget or TargetSelector.FirstEnemy):
                throw new InvalidOperationException($"{owner} counts power on an invalid target.");
            case BlockCountSource block when block.Target is not
                (TargetSelector.Self or TargetSelector.SelectedTarget or TargetSelector.FirstEnemy):
                throw new InvalidOperationException($"{owner} reads block from an invalid target.");
            case SavedCountSource saved:
                ArgumentException.ThrowIfNullOrWhiteSpace(saved.Name);
                if (counts?.Contains(saved.Name) != true)
                    throw new InvalidOperationException($"{owner} reads unbound count '{saved.Name}'.");
                break;
            case CardPlaysThisTurnCountSource plays:
                if (!Enum.IsDefined(plays.Phase) || plays.Type is { } historyType && !Enum.IsDefined(historyType))
                    throw new InvalidOperationException($"{owner} counts an invalid card-play history phase or type.");
                break;
            case CardVariableCountSource variable:
                ValidateCardVariableReference(variable.Variable, variable.Card, owner);
                break;
            case PowerVariableCountSource variable:
                ValidatePowerVariableReference(variable.Power, variable.Variable, owner);
                if (variable.Target is not (TargetSelector.Self or TargetSelector.SelectedTarget or TargetSelector.FirstEnemy))
                    throw new InvalidOperationException($"{owner} reads a Power variable on an invalid target.");
                break;
            case TakePowerEventAmountCountSource deferred when !_powers.ContainsKey(deferred.Power):
                throw new InvalidOperationException($"{owner} reads deferred event amount for unknown power '{deferred.Power}'.");
            case TakePowerEventAmountCountSource deferred when !Enum.IsDefined(deferred.Card):
                throw new InvalidOperationException($"{owner} reads deferred event amount with an invalid card context.");
            case TakePowerTurnEventAmountCountSource deferred when !_powers.ContainsKey(deferred.Power):
                throw new InvalidOperationException($"{owner} reads deferred turn event amount for unknown power '{deferred.Power}'.");
            case TakePowerEventAmountCountSource or TakePowerTurnEventAmountCountSource:
                break;
            case ProductCombatCountSource product:
                ValidateCombatCountSource(product.Left, owner, counts);
                ValidateCombatCountSource(product.Right, owner, counts);
                break;
            case TaggedCardCountSource tag:
                tag.Tag.Validate();
                break;
            case PileCardCountSource or TargetPowerCountSource or BlockCountSource
                or PlayerHpLossCountSource or EnergyXCountSource:
                break;
            default:
                throw new InvalidOperationException($"{owner} has an unsupported combat count source.");
        }
    }

    private void ValidateCardVariableReference(ContentId variable, CardContext card, string owner)
    {
        variable.Validate();
        if (!Enum.IsDefined(card) || !_cards.Values.Any(definition => definition.Variables.Any(value => value.Id == variable)))
            throw new InvalidOperationException($"{owner} references an invalid card context or undeclared variable '{variable}'.");
    }

    private void ValidatePowerVariableReference(ContentId power, ContentId variable, string owner)
    {
        variable.Validate();
        if (!_powers.TryGetValue(power, out var definition)
            || !definition.Variables.Any(value => value.Id == variable))
            throw new InvalidOperationException($"{owner} references undeclared Power variable '{power}.{variable}'.");
    }

    private static void AddUnique<T>(Dictionary<ContentId, T> items, ContentId id, T definition, string kind)
    {
        id.Validate();
        if (!items.TryAdd(id, definition))
            throw new InvalidOperationException($"Duplicate {kind} ID '{id}'. Use Override explicitly to replace content.");
    }

    private static void Replace<T>(Dictionary<ContentId, T> items, ContentId id, T definition, string kind)
    {
        id.Validate();
        if (!items.ContainsKey(id))
            throw new InvalidOperationException($"Cannot override unknown {kind} ID '{id}'.");
        items[id] = definition;
    }

    private static void Restore<TKey, TValue>(Dictionary<TKey, TValue> target,
        Dictionary<TKey, TValue> snapshot) where TKey : notnull
    {
        target.Clear();
        foreach (var (id, definition) in snapshot) target.Add(id, definition);
    }
}

public sealed class GameDefinition
{
    internal GameDefinition(PackageInfo[] packages, IReadOnlyDictionary<ContentId, CardDefinition> cards,
        IReadOnlyDictionary<ContentId, PotionDefinition> potions,
        IReadOnlyDictionary<ContentId, CardPoolDefinition> cardPools,
        IReadOnlyDictionary<ContentId, RelicDefinition> relics,
        IReadOnlyDictionary<ContentId, EnemyDefinition> enemies, IReadOnlyDictionary<ContentId, EncounterDefinition> encounters,
        IReadOnlyDictionary<ContentId, RunEventDefinition> runEvents,
        IReadOnlyDictionary<ContentId, PowerDefinition> powers,
        IReadOnlyDictionary<ContentId, CharacterDefinition> characters, IReadOnlyDictionary<ContentId, RunDefinition> runs,
        IReadOnlyDictionary<StateVariableKey, StateVariableDefinition> stateVariables,
        IReadOnlyDictionary<ContentId, ICustomEffectHandler> customEffects,
        IReadOnlyDictionary<ContentId, ICustomRunEffectHandler> customRunEffects,
        IRuleHook[] hooks, IRunHook[] runHooks)
    {
        Packages = Array.AsReadOnly(packages);
        Cards = cards;
        HasBeforeCardPlayListeners = cards.Values.Any(card => card.BeforeCardPlayed is not null || card.UpgradedBeforeCardPlayed is not null);
        HasAfterCardPlayListeners = cards.Values.Any(card => card.AfterCardPlayed is not null || card.UpgradedAfterCardPlayed is not null);
        Potions = potions;
        CardPools = cardPools;
        Relics = relics;
        Enemies = enemies;
        Encounters = encounters;
        RunEvents = runEvents;
        Powers = powers;
        Characters = characters;
        Runs = runs;
        StateVariables = stateVariables;
        CustomEffects = customEffects;
        CustomRunEffects = customRunEffects;
        Hooks = Array.AsReadOnly(hooks);
        RunHooks = Array.AsReadOnly(runHooks);
        Version = string.Join("+", packages.Select(p => $"{p.Id}@{p.Version}"));
    }

    public IReadOnlyList<PackageInfo> Packages { get; }
    public IReadOnlyDictionary<ContentId, CardDefinition> Cards { get; }
    internal bool HasBeforeCardPlayListeners { get; }
    internal bool HasAfterCardPlayListeners { get; }
    public IReadOnlyDictionary<ContentId, PotionDefinition> Potions { get; }
    public IReadOnlyDictionary<ContentId, CardPoolDefinition> CardPools { get; }
    public IReadOnlyDictionary<ContentId, RelicDefinition> Relics { get; }
    public IReadOnlyDictionary<ContentId, EnemyDefinition> Enemies { get; }
    public IReadOnlyDictionary<ContentId, EncounterDefinition> Encounters { get; }
    public IReadOnlyDictionary<ContentId, RunEventDefinition> RunEvents { get; }
    public IReadOnlyDictionary<ContentId, PowerDefinition> Powers { get; }
    public IReadOnlyDictionary<ContentId, CharacterDefinition> Characters { get; }
    public IReadOnlyDictionary<ContentId, RunDefinition> Runs { get; }
    public IReadOnlyDictionary<StateVariableKey, StateVariableDefinition> StateVariables { get; }
    internal IReadOnlyDictionary<ContentId, ICustomEffectHandler> CustomEffects { get; }
    internal IReadOnlyDictionary<ContentId, ICustomRunEffectHandler> CustomRunEffects { get; }
    internal IReadOnlyList<IRuleHook> Hooks { get; }
    internal IReadOnlyList<IRunHook> RunHooks { get; }
    public string Version { get; }

    public IReadOnlyList<PotionDefinition> PotionPoolFor(ContentId characterId)
    {
        if (!Characters.ContainsKey(characterId))
            throw new KeyNotFoundException($"Unknown character '{characterId}'.");
        return Array.AsReadOnly(Potions.Values
            .Where(potion => potion.InSharedPool || potion.CharacterPoolIds?.Contains(characterId) == true)
            .OrderBy(potion => potion.Id.Value, StringComparer.Ordinal).ToArray());
    }
}

public interface IRuleHook
{
    ContentId Id { get; }
    void OnEvent(RuleHookContext context, GameEvent gameEvent);
    /// <summary>Returns an effect to queue after this event with an actor selected from the event.</summary>
    GameEventEffect? OnEventEffect(RuleHookContext context, GameEvent gameEvent) => null;
    /// <summary>Transforms the multiplier used when a powered move hits a Vulnerable creature.</summary>
    decimal ModifyVulnerableMultiplier(RuleHookContext context, string dealerId, string targetId,
        DamageProperties properties, decimal currentMultiplier) => currentMultiplier;
    int ModifyPowerAmountReceived(RuleHookContext context, ContentId powerId, PowerRole powerRole,
        string targetId, string applierId, int amount) => amount;
    bool ShouldEtherealTrigger(RuleHookContext context, CardInstance card) => true;
    bool ShouldFlush(RuleHookContext context, string playerId) => true;
    bool ShouldClearBlock(RuleHookContext context, string creatureId) => true;
    EffectNode? BeforeFlush(RuleHookContext context, string playerId) => null;
    EffectNode? BeforeFlushLate(RuleHookContext context, string playerId, bool shouldFlush) => null;
    EffectNode? AfterFlush(RuleHookContext context, string playerId,
        IReadOnlyList<CardInstance> flushedCards, IReadOnlyList<CardInstance> retainedCards) => null;
}
