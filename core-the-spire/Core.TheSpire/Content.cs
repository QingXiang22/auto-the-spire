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
public sealed record PowerDefinition(ContentId Id, PowerTrigger Trigger = PowerTrigger.None, ContentId? TriggerEffect = null,
    bool SkipFirstTriggerWhenAppliedToEnemy = false, PowerRole Role = PowerRole.None, bool DecrementOnTrigger = false,
    bool SkipFirstTriggerWhenAppliedToPlayer = false, decimal? OutgoingDamageMultiplier = null,
    bool RemoveWhenApplierDies = false)
{
    public void Validate()
    {
        Id.Validate();
        if (TriggerEffect is { } triggerEffect) triggerEffect.Validate();
        if (!Enum.IsDefined(Trigger) || !Enum.IsDefined(Role))
            throw new ArgumentException("Power trigger and role must be recognized values.");
        if (OutgoingDamageMultiplier is < 0m)
            throw new ArgumentOutOfRangeException(nameof(OutgoingDamageMultiplier));
    }
}
public sealed record CardDefinition(ContentId Id, int Cost, CardType Type, EffectNode Effect,
    bool Exhaust = false, TargetSelector Target = TargetSelector.FirstEnemy);
public sealed record EnemyActionDefinition(ContentId Id, EffectNode Effect, IntentKind Intent,
    int IntentDamage = 0, ContentId? AppliedPower = null, int AppliedPowerAmount = 0,
    ContentId? NextActionId = null, TargetSelector AppliedPowerTarget = TargetSelector.Self,
    EnemyActionTransition? Transition = null);
public sealed record EnemyActionChoice(ContentId ActionId, int Weight = 1, int? MaxConsecutiveUses = null);
public enum EnemyActionRepeatPolicy { Allow, PreventImmediateRepeat }

public sealed record EnemyActionTransition
{
    public EnemyActionTransition(IEnumerable<EnemyActionChoice> choices,
        EnemyActionRepeatPolicy repeatPolicy = EnemyActionRepeatPolicy.Allow)
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
        }
        if (!Enum.IsDefined(repeatPolicy)) throw new ArgumentOutOfRangeException(nameof(repeatPolicy));
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
    }

    public IReadOnlyList<EnemyActionChoice> Choices { get; }
    public EnemyActionRepeatPolicy RepeatPolicy { get; }
}
public enum RunEventKind { Event, Ancient }

public sealed record RunEventDefinition(ContentId Id, RunEventKind Kind = RunEventKind.Event)
{
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
        IReadOnlyDictionary<int, ContentId>? startingActionsByEnemyIndex = null)
        : this(id, roomType, ToFixedSlots(enemyIds), isWeak, tags, startingActionsByEnemyIndex)
    {
    }

    private EncounterDefinition(ContentId id, RunRoomType roomType, IEnumerable<EncounterEnemySlot> enemySlots,
        bool isWeak, IEnumerable<ContentId>? tags,
        IReadOnlyDictionary<int, ContentId>? startingActionsByEnemyIndex)
    {
        id.Validate();
        ArgumentNullException.ThrowIfNull(enemySlots);
        var slots = enemySlots.ToArray();
        if (roomType is not (RunRoomType.Monster or RunRoomType.Elite or RunRoomType.Boss))
            throw new ArgumentOutOfRangeException(nameof(roomType), "Encounters must be Monster, Elite, or Boss rooms.");
        if (slots.Length == 0) throw new ArgumentException("An encounter must contain at least one enemy.", nameof(enemySlots));
        if (slots.Any(slot => slot is null)) throw new ArgumentException("Encounter slots cannot be null.", nameof(enemySlots));
        ValidateChoiceGroups(slots);
        var encounterTags = tags?.ToArray() ?? [];
        foreach (var tag in encounterTags) tag.Validate();
        var startingActions = startingActionsByEnemyIndex is null
            ? new Dictionary<int, ContentId>()
            : new Dictionary<int, ContentId>(startingActionsByEnemyIndex);
        foreach (var (enemyIndex, actionId) in startingActions)
        {
            if (enemyIndex < 0 || enemyIndex >= slots.Length)
                throw new ArgumentOutOfRangeException(nameof(startingActionsByEnemyIndex), "Starting action index must refer to an enemy in this encounter.");
            actionId.Validate();
        }
        Id = id;
        RoomType = roomType;
        EnemySlots = Array.AsReadOnly(slots);
        IsWeak = isWeak;
        Tags = Array.AsReadOnly(encounterTags);
        StartingActionsByEnemyIndex = new ReadOnlyDictionary<int, ContentId>(startingActions);
    }

    public ContentId Id { get; }
    public RunRoomType RoomType { get; }
    public IReadOnlyList<EncounterEnemySlot> EnemySlots { get; }
    public bool IsWeak { get; }
    public IReadOnlyList<ContentId> Tags { get; }
    public IReadOnlyDictionary<int, ContentId> StartingActionsByEnemyIndex { get; }

    public static EncounterDefinition WithEnemySlots(ContentId id, RunRoomType roomType,
        IEnumerable<EncounterEnemySlot> enemySlots, bool isWeak = false, IEnumerable<ContentId>? tags = null,
        IReadOnlyDictionary<int, ContentId>? startingActionsByEnemyIndex = null) =>
        new(id, roomType, enemySlots, isWeak, tags, startingActionsByEnemyIndex);

    public IReadOnlyList<EncounterEnemySelection> SelectEnemies(SeededRandom random)
    {
        ArgumentNullException.ThrowIfNull(random);
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

    public EnemyDefinition(ContentId id, int minHp, int maxHp, IReadOnlyList<EnemyActionDefinition> actions,
        bool loopLastAction = true, ContentId? startingActionId = null,
        EnemyActionTransition? startingTransition = null)
    {
        id.Validate();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minHp);
        if (maxHp < minHp) throw new ArgumentOutOfRangeException(nameof(maxHp));
        ArgumentNullException.ThrowIfNull(actions);
        var copiedActions = actions.ToArray();
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
        _actionIndexes = new System.Collections.ObjectModel.ReadOnlyDictionary<ContentId, int>(indexes);
    }
    public ContentId Id { get; }
    public int MinHp { get; }
    public int MaxHp { get; }
    public IReadOnlyList<EnemyActionDefinition> Actions { get; }
    public bool LoopLastAction { get; }
    public int StartingActionIndex { get; }
    public EnemyActionTransition? StartingTransition { get; }
    internal int GetActionIndex(ContentId actionId) => _actionIndexes[actionId];

    internal int ResolveStartingActionIndex(SeededRandom random) => StartingTransition is { } transition
        ? ResolveTransitionIndex(transition, null, 0, random)
        : StartingActionIndex;

    internal int ResolveNextActionIndex(ContentId currentActionId, ContentId? lastActionId,
        int consecutiveActionCount, SeededRandom random)
    {
        var current = Actions[GetActionIndex(currentActionId)];
        if (current.NextActionId is { } next) return GetActionIndex(next);
        if (current.Transition is { } transition)
            return ResolveTransitionIndex(transition, lastActionId, consecutiveActionCount, random);
        return currentActionId == Actions[^1].Id && LoopLastAction
            ? Actions.Count - 1
            : Math.Min(GetActionIndex(currentActionId) + 1, Actions.Count - 1);
    }

    private int ResolveTransitionIndex(EnemyActionTransition transition, ContentId? lastActionId,
        int consecutiveActionCount, SeededRandom random)
    {
        var choices = transition.Choices.Where(choice =>
        {
            var maximumConsecutiveUses = transition.RepeatPolicy == EnemyActionRepeatPolicy.PreventImmediateRepeat
                ? 1
                : choice.MaxConsecutiveUses;
            if (maximumConsecutiveUses is null || lastActionId != choice.ActionId) return true;
            return consecutiveActionCount < maximumConsecutiveUses.Value;
        }).ToArray();
        if (choices.Length == 0)
            throw new InvalidOperationException("Enemy transition has no eligible action choices.");
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
public enum IntentKind { None, Attack, Buff, Debuff }
public enum PowerTrigger { None, AfterPlayerTurnEnd, AfterEnemyTurnEnd }
public enum PowerRole { None, Strength, Vulnerable }

public interface IRulePackage
{
    PackageInfo Info { get; }
    void Register(ContentRegistry registry);
}

public sealed class ContentRegistry
{
    private readonly Dictionary<ContentId, CardDefinition> _cards = [];
    private readonly Dictionary<ContentId, EnemyDefinition> _enemies = [];
    private readonly Dictionary<ContentId, EncounterDefinition> _encounters = [];
    private readonly Dictionary<ContentId, RunEventDefinition> _runEvents = [];
    private readonly Dictionary<ContentId, PowerDefinition> _powers = [];
    private readonly Dictionary<ContentId, CharacterDefinition> _characters = [];
    private readonly Dictionary<ContentId, RunDefinition> _runs = [];
    private readonly Dictionary<ContentId, ICustomEffectHandler> _customEffects = [];
    private readonly List<IRuleHook> _hooks = [];
    private readonly List<IRunHook> _runHooks = [];
    private readonly List<PackageInfo> _packages = [];

    public void AddPackage(IRulePackage package)
    {
        ArgumentNullException.ThrowIfNull(package);
        if (_packages.Any(p => p.Id == package.Info.Id))
            throw new InvalidOperationException($"Package '{package.Info.Id}' is already loaded.");
        var cardsBefore = new Dictionary<ContentId, CardDefinition>(_cards);
        var enemiesBefore = new Dictionary<ContentId, EnemyDefinition>(_enemies);
        var encountersBefore = new Dictionary<ContentId, EncounterDefinition>(_encounters);
        var runEventsBefore = new Dictionary<ContentId, RunEventDefinition>(_runEvents);
        var powersBefore = new Dictionary<ContentId, PowerDefinition>(_powers);
        var charactersBefore = new Dictionary<ContentId, CharacterDefinition>(_characters);
        var runsBefore = new Dictionary<ContentId, RunDefinition>(_runs);
        var customEffectsBefore = new Dictionary<ContentId, ICustomEffectHandler>(_customEffects);
        var hookCountBefore = _hooks.Count;
        var runHookCountBefore = _runHooks.Count;
        try
        {
            package.Register(this);
            _packages.Add(package.Info);
        }
        catch
        {
            Restore(_cards, cardsBefore);
            Restore(_enemies, enemiesBefore);
            Restore(_encounters, encountersBefore);
            Restore(_runEvents, runEventsBefore);
            Restore(_powers, powersBefore);
            Restore(_characters, charactersBefore);
            Restore(_runs, runsBefore);
            Restore(_customEffects, customEffectsBefore);
            _hooks.RemoveRange(hookCountBefore, _hooks.Count - hookCountBefore);
            _runHooks.RemoveRange(runHookCountBefore, _runHooks.Count - runHookCountBefore);
            throw;
        }
    }

    public void Register(CardDefinition definition) => AddUnique(_cards, definition.Id, definition, "card");
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

    public void Override(CardDefinition definition) => Replace(_cards, definition.Id, definition, "card");
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
        foreach (var encounter in _encounters.Values)
        {
            foreach (var enemyId in encounter.EnemySlots.SelectMany(slot => slot.EnemyChoices).Distinct())
                if (!_enemies.ContainsKey(enemyId))
                    throw new InvalidOperationException($"Encounter '{encounter.Id}' references unknown enemy '{enemyId}'.");
            foreach (var (enemyIndex, actionId) in encounter.StartingActionsByEnemyIndex)
            {
                foreach (var enemyId in encounter.EnemySlots[enemyIndex].EnemyChoices)
                    if (!_enemies[enemyId].Actions.Any(action => action.Id == actionId))
                        throw new InvalidOperationException($"Encounter '{encounter.Id}' assigns unknown starting action '{actionId}' to enemy '{enemyId}'.");
            }
        }
        foreach (var enemy in _enemies.Values)
        foreach (var action in enemy.Actions)
        {
            if (action.AppliedPower is { } powerId && !_powers.ContainsKey(powerId))
                throw new InvalidOperationException($"Enemy action '{action.Id}' references unknown power '{powerId}'.");
            ValidateEffectReferences(action.Effect, $"Enemy action '{action.Id}'");
        }
        foreach (var card in _cards.Values)
            ValidateEffectReferences(card.Effect, $"Card '{card.Id}'");
        foreach (var power in _powers.Values)
            if (power.TriggerEffect is { } effect && !_powers.ContainsKey(effect))
                throw new InvalidOperationException($"Power '{power.Id}' triggers unknown power '{effect}'.");
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
        }
        return new GameDefinition(
            _packages.ToArray(),
            new ReadOnlyDictionary<ContentId, CardDefinition>(new Dictionary<ContentId, CardDefinition>(_cards)),
            new ReadOnlyDictionary<ContentId, EnemyDefinition>(new Dictionary<ContentId, EnemyDefinition>(_enemies)),
            new ReadOnlyDictionary<ContentId, EncounterDefinition>(new Dictionary<ContentId, EncounterDefinition>(_encounters)),
            new ReadOnlyDictionary<ContentId, RunEventDefinition>(new Dictionary<ContentId, RunEventDefinition>(_runEvents)),
            new ReadOnlyDictionary<ContentId, PowerDefinition>(new Dictionary<ContentId, PowerDefinition>(_powers)),
            new ReadOnlyDictionary<ContentId, CharacterDefinition>(new Dictionary<ContentId, CharacterDefinition>(_characters)),
            new ReadOnlyDictionary<ContentId, RunDefinition>(new Dictionary<ContentId, RunDefinition>(_runs)),
            new ReadOnlyDictionary<ContentId, ICustomEffectHandler>(new Dictionary<ContentId, ICustomEffectHandler>(_customEffects)),
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

    private void ValidateEffectReferences(EffectNode effect, string owner)
    {
        switch (effect)
        {
            case SequenceEffect sequence:
                foreach (var child in sequence.Effects) ValidateEffectReferences(child, owner);
                break;
            case ApplyPowerEffect applyPower when !_powers.ContainsKey(applyPower.Power):
                throw new InvalidOperationException($"{owner} applies unknown power '{applyPower.Power}'.");
            case AddCardsToPileEffect addCards:
                addCards.Card.Validate();
                if (!_cards.ContainsKey(addCards.Card))
                    throw new InvalidOperationException($"{owner} adds unknown card '{addCards.Card}'.");
                if (!Enum.IsDefined(addCards.Pile) || !Enum.IsDefined(addCards.Target))
                    throw new InvalidOperationException($"{owner} has an invalid card destination or target.");
                ArgumentOutOfRangeException.ThrowIfNegative(addCards.Count);
                break;
            case ChooseEffect choice:
                foreach (var option in choice.Options)
                    if (option.Effect is { } optionEffect) ValidateEffectReferences(optionEffect, owner);
                break;
            case IfEffect conditional:
                ValidateEffectReferences(conditional.Then, owner);
                if (conditional.Else is { } elseEffect) ValidateEffectReferences(elseEffect, owner);
                break;
            case RepeatEffect repeat:
                ValidateEffectReferences(repeat.Effect, owner);
                break;
            case CustomEffect custom when !_customEffects.ContainsKey(custom.Handler):
                throw new InvalidOperationException($"{owner} references unknown custom effect '{custom.Handler}'.");
        }
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

    private static void Restore<T>(Dictionary<ContentId, T> target, Dictionary<ContentId, T> snapshot)
    {
        target.Clear();
        foreach (var (id, definition) in snapshot) target.Add(id, definition);
    }
}

public sealed class GameDefinition
{
    internal GameDefinition(PackageInfo[] packages, IReadOnlyDictionary<ContentId, CardDefinition> cards,
        IReadOnlyDictionary<ContentId, EnemyDefinition> enemies, IReadOnlyDictionary<ContentId, EncounterDefinition> encounters,
        IReadOnlyDictionary<ContentId, RunEventDefinition> runEvents,
        IReadOnlyDictionary<ContentId, PowerDefinition> powers,
        IReadOnlyDictionary<ContentId, CharacterDefinition> characters, IReadOnlyDictionary<ContentId, RunDefinition> runs,
        IReadOnlyDictionary<ContentId, ICustomEffectHandler> customEffects, IRuleHook[] hooks, IRunHook[] runHooks)
    {
        Packages = Array.AsReadOnly(packages);
        Cards = cards;
        Enemies = enemies;
        Encounters = encounters;
        RunEvents = runEvents;
        Powers = powers;
        Characters = characters;
        Runs = runs;
        CustomEffects = customEffects;
        Hooks = Array.AsReadOnly(hooks);
        RunHooks = Array.AsReadOnly(runHooks);
        Version = string.Join("+", packages.Select(p => $"{p.Id}@{p.Version}"));
    }

    public IReadOnlyList<PackageInfo> Packages { get; }
    public IReadOnlyDictionary<ContentId, CardDefinition> Cards { get; }
    public IReadOnlyDictionary<ContentId, EnemyDefinition> Enemies { get; }
    public IReadOnlyDictionary<ContentId, EncounterDefinition> Encounters { get; }
    public IReadOnlyDictionary<ContentId, RunEventDefinition> RunEvents { get; }
    public IReadOnlyDictionary<ContentId, PowerDefinition> Powers { get; }
    public IReadOnlyDictionary<ContentId, CharacterDefinition> Characters { get; }
    public IReadOnlyDictionary<ContentId, RunDefinition> Runs { get; }
    internal IReadOnlyDictionary<ContentId, ICustomEffectHandler> CustomEffects { get; }
    internal IReadOnlyList<IRuleHook> Hooks { get; }
    internal IReadOnlyList<IRunHook> RunHooks { get; }
    public string Version { get; }
}

public interface IRuleHook
{
    ContentId Id { get; }
    void OnEvent(RuleHookContext context, GameEvent gameEvent);
}
