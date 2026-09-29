using System.Collections.ObjectModel;

namespace CoreTheSpire;

public enum MapPointType { Unassigned, Unknown, Shop, Treasure, RestSite, Monster, Elite, Boss, Ancient, Start }
public enum RunRoomType { Unassigned, Monster, Elite, Boss, Treasure, Shop, Event, RestSite, Map }
public enum RunPhase { ChooseMapPoint, ResolveRoom, ResolveEvent, ResolveEventCardSelection, ResolveRestSite, ResolveTreasure, ResolveRewards, ResolveFinalEvent, Terminal }
public enum RunOutcome { RoomCleared, PlayerDefeated, RunWon }
public enum RunStatus { InProgress, Won, Lost }

public sealed record MapPointDefinition(string Id, int Floor, int Column, MapPointType Type,
    IReadOnlyList<string> Children);

public sealed class ActMapDefinition
{
    private readonly IReadOnlyDictionary<string, MapPointDefinition> _points;

    public ActMapDefinition(string startPointId, string bossPointId, IEnumerable<MapPointDefinition> points)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(startPointId);
        ArgumentException.ThrowIfNullOrWhiteSpace(bossPointId);
        ArgumentNullException.ThrowIfNull(points);

        var pointMap = new Dictionary<string, MapPointDefinition>(StringComparer.Ordinal);
        foreach (var point in points)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(point.Id);
            if (point.Floor < 0) throw new ArgumentOutOfRangeException(nameof(points), "Map floors cannot be negative.");
            if (point.Children is null) throw new ArgumentException("Map point children cannot be null.", nameof(points));
            if (!pointMap.TryAdd(point.Id, point with { Children = Array.AsReadOnly(point.Children.ToArray()) }))
                throw new ArgumentException($"Duplicate map point ID '{point.Id}'.", nameof(points));
        }

        if (!pointMap.TryGetValue(startPointId, out var start) || start.Type is not (MapPointType.Start or MapPointType.Ancient))
            throw new ArgumentException("The start point must exist and have type Start or Ancient.", nameof(startPointId));
        if (!pointMap.TryGetValue(bossPointId, out var boss) || boss.Type != MapPointType.Boss)
            throw new ArgumentException("The boss point must exist and have type Boss.", nameof(bossPointId));

        foreach (var point in pointMap.Values)
        foreach (var childId in point.Children)
        {
            if (!pointMap.TryGetValue(childId, out var child))
                throw new ArgumentException($"Map point '{point.Id}' links to missing point '{childId}'.", nameof(points));
            if (child.Floor <= point.Floor)
                throw new ArgumentException($"Map links must move to a later floor ('{point.Id}' -> '{childId}').", nameof(points));
        }

        var reachable = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>();
        pending.Push(startPointId);
        while (pending.TryPop(out var pointId))
        {
            if (!reachable.Add(pointId)) continue;
            foreach (var childId in pointMap[pointId].Children) pending.Push(childId);
        }
        if (reachable.Count != pointMap.Count)
            throw new ArgumentException("Every map point must be reachable from the start point.", nameof(points));
        if (pointMap.Values.Any(point => point.Id != bossPointId && point.Children.Count == 0))
            throw new ArgumentException("Every non-boss map point must lead to a later point.", nameof(points));
        if (!reachable.Contains(bossPointId))
            throw new ArgumentException("The boss point must be reachable from the start point.", nameof(bossPointId));

        StartPointId = startPointId;
        BossPointId = bossPointId;
        _points = new System.Collections.ObjectModel.ReadOnlyDictionary<string, MapPointDefinition>(pointMap);
    }

    public string StartPointId { get; }
    public string BossPointId { get; }
    public IReadOnlyDictionary<string, MapPointDefinition> Points => _points;
}

public sealed record ActDefinition
{
    public ActDefinition(ContentId id, Func<SeededRandom, ActMapDefinition> generateMap,
        Func<SeededRandom, IReadOnlySet<RunRoomType>, RunRoomType>? resolveUnknownRoom = null,
        ActEncounterPoolDefinition? encounterPool = null, IEnumerable<ContentId>? eventIds = null,
        IEnumerable<ContentId>? ancientIds = null)
    {
        id.Validate();
        ArgumentNullException.ThrowIfNull(generateMap);
        Id = id;
        GenerateMap = generateMap;
        ResolveUnknownRoom = resolveUnknownRoom;
        EncounterPool = encounterPool;
        EventIds = CopyIds(eventIds, nameof(eventIds));
        AncientIds = CopyIds(ancientIds, nameof(ancientIds));
    }

    public ContentId Id { get; }
    public Func<SeededRandom, ActMapDefinition> GenerateMap { get; }
    public Func<SeededRandom, IReadOnlySet<RunRoomType>, RunRoomType>? ResolveUnknownRoom { get; }
    public ActEncounterPoolDefinition? EncounterPool { get; }
    public IReadOnlyList<ContentId> EventIds { get; }
    public IReadOnlyList<ContentId> AncientIds { get; }

    internal static IReadOnlyList<ContentId> CopyIds(IEnumerable<ContentId>? source, string parameterName)
    {
        var ids = source?.ToArray() ?? [];
        foreach (var id in ids) id.Validate();
        if (ids.Distinct().Count() != ids.Length)
            throw new ArgumentException("Run content IDs must be unique within a pool.", parameterName);
        return Array.AsReadOnly(ids);
    }
}

public sealed record UnknownRoomOddsEntry(RunRoomType RoomType, float Weight);

public sealed class UnknownRoomOddsDefinition
{
    public UnknownRoomOddsDefinition(IEnumerable<UnknownRoomOddsEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var odds = entries.ToArray();
        if (odds.Length == 0) throw new ArgumentException("Unknown-room odds need at least one non-event room type.", nameof(entries));
        if (odds.Any(entry => entry.RoomType is not (RunRoomType.Monster or RunRoomType.Elite or RunRoomType.Treasure or RunRoomType.Shop)))
            throw new ArgumentException("Unknown-room odds may define Monster, Elite, Treasure, and Shop only.", nameof(entries));
        if (odds.Any(entry => !float.IsFinite(entry.Weight)))
            throw new ArgumentException("Unknown-room weights must be finite.", nameof(entries));
        if (odds.Select(entry => entry.RoomType).Distinct().Count() != odds.Length)
            throw new ArgumentException("Unknown-room types must be unique.", nameof(entries));
        Entries = Array.AsReadOnly(odds);
    }

    public IReadOnlyList<UnknownRoomOddsEntry> Entries { get; }
}

internal sealed class UnknownRoomOddsState
{
    private readonly UnknownRoomOddsEntry[] _baseOdds;
    private readonly float[] _currentOdds;

    public UnknownRoomOddsState(UnknownRoomOddsDefinition definition)
    {
        _baseOdds = definition.Entries.ToArray();
        _currentOdds = _baseOdds.Select(entry => entry.Weight).ToArray();
    }

    public RunRoomType Roll(SeededRandom random, IReadOnlySet<RunRoomType> blacklist,
        IReadOnlyList<IRunHook> hooks, RunObservation observation)
    {
        var roomTypes = _baseOdds.Select(entry => entry.RoomType).Append(RunRoomType.Event)
            .Where(type => !blacklist.Contains(type)).ToHashSet();
        foreach (var hook in hooks)
        {
            var modified = hook.ModifyUnknownRoomTypes(observation, roomTypes);
            ArgumentNullException.ThrowIfNull(modified);
            if (modified.Any(type => type is not (RunRoomType.Event or RunRoomType.Monster or
                RunRoomType.Elite or RunRoomType.Treasure or RunRoomType.Shop)))
                throw new InvalidOperationException($"Run hook '{hook.Id}' returned an unsupported unknown-room type.");
            roomTypes = modified.ToHashSet();
        }
        if (roomTypes.Count == 0)
            throw new InvalidOperationException("Unknown-room hooks removed every possible room type.");

        var roll = random.NextFloat();
        var result = roomTypes.Contains(RunRoomType.Event) ? RunRoomType.Event : roomTypes.Order().First();
        var cumulative = 0f;
        for (var i = 0; i < _baseOdds.Length; i++)
        {
            var weight = _currentOdds[i];
            if (weight < 0f || !roomTypes.Contains(_baseOdds[i].RoomType)) continue;
            cumulative += weight;
            if (roll <= cumulative)
            {
                result = _baseOdds[i].RoomType;
                break;
            }
        }

        for (var i = 0; i < _baseOdds.Length; i++)
        {
            if (_baseOdds[i].RoomType == result) _currentOdds[i] = _baseOdds[i].Weight;
            else if (roomTypes.Contains(_baseOdds[i].RoomType))
            {
                var increase = hooks.Aggregate(_baseOdds[i].Weight,
                    (current, hook) => hook.ModifyUnknownOddsIncrease(observation, _baseOdds[i].RoomType, current));
                if (!float.IsFinite(increase))
                    throw new InvalidOperationException($"A run hook returned a non-finite unknown-room odds increase for '{_baseOdds[i].RoomType}'.");
                _currentOdds[i] += increase;
            }
        }
        return result;
    }

    public void ResetToBase()
    {
        for (var i = 0; i < _baseOdds.Length; i++) _currentOdds[i] = _baseOdds[i].Weight;
    }
}

public sealed class CharacterDefinition
{
    public CharacterDefinition(ContentId id, int maxHp, int startingGold,
        IEnumerable<ContentId> startingDeck, IEnumerable<ContentId>? startingRelics = null,
        IEnumerable<ContentId>? startingPotions = null, IEnumerable<ContentId>? cardPools = null,
        int potionCapacity = 3)
    {
        id.Validate();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxHp);
        ArgumentOutOfRangeException.ThrowIfNegative(startingGold);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(potionCapacity);
        ArgumentNullException.ThrowIfNull(startingDeck);
        Id = id;
        MaxHp = maxHp;
        StartingGold = startingGold;
        StartingDeck = Array.AsReadOnly(startingDeck.ToArray());
        StartingRelics = Array.AsReadOnly(startingRelics?.ToArray() ?? []);
        StartingPotions = Array.AsReadOnly(startingPotions?.ToArray() ?? []);
        if (StartingPotions.Count > potionCapacity)
            throw new ArgumentException("Starting potions exceed the configured potion capacity.", nameof(startingPotions));
        PotionCapacity = potionCapacity;
        CardPools = ActDefinition.CopyIds(cardPools, nameof(cardPools));
    }

    public ContentId Id { get; }
    public int MaxHp { get; }
    public int StartingGold { get; }
    public IReadOnlyList<ContentId> StartingDeck { get; }
    public IReadOnlyList<ContentId> StartingRelics { get; }
    public IReadOnlyList<ContentId> StartingPotions { get; }
    public int PotionCapacity { get; }
    public IReadOnlyList<ContentId> CardPools { get; }
}

public sealed class RunDefinition
{
    public RunDefinition(ContentId id, IEnumerable<ActDefinition> acts, UnknownRoomOddsDefinition? unknownRoomOdds = null,
        IEnumerable<ContentId>? sharedEventIds = null, IEnumerable<ContentId>? sharedAncientIds = null,
        ContentId? finalEventId = null)
    {
        id.Validate();
        ArgumentNullException.ThrowIfNull(acts);
        Id = id;
        UnknownRoomOdds = unknownRoomOdds;
        finalEventId?.Validate();
        FinalEventId = finalEventId;
        SharedEventIds = ActDefinition.CopyIds(sharedEventIds, nameof(sharedEventIds));
        SharedAncientIds = ActDefinition.CopyIds(sharedAncientIds, nameof(sharedAncientIds));
        Acts = Array.AsReadOnly(acts.ToArray());
        if (Acts.Count == 0) throw new ArgumentException("A run needs at least one act.", nameof(acts));
        foreach (var act in Acts)
        {
            act.Id.Validate();
        }
        if (Acts.Select(act => act.Id).Distinct().Count() != Acts.Count)
            throw new ArgumentException("Act IDs must be unique within a run.", nameof(acts));
    }

    public ContentId Id { get; }
    public IReadOnlyList<ActDefinition> Acts { get; }
    public UnknownRoomOddsDefinition? UnknownRoomOdds { get; }
    public ContentId? FinalEventId { get; }
    public IReadOnlyList<ContentId> SharedEventIds { get; }
    public IReadOnlyList<ContentId> SharedAncientIds { get; }
}

public sealed record ChooseMapPointAction(string PointId) : PlayerAction;
public sealed record ChooseRunEventOptionAction(ContentId OptionId) : PlayerAction;
public sealed record ChooseRunCardAction(string CardInstanceId) : PlayerAction;
public sealed record SkipRunCardSelectionAction : PlayerAction;
public sealed record ClaimGoldRewardAction(string RewardId) : PlayerAction;
public sealed record ClaimPotionRewardAction(string RewardId) : PlayerAction;
public sealed record SkipRewardSetAction : PlayerAction;
public sealed record OpenTreasureChestAction : PlayerAction;
public sealed record ClaimRelicRewardAction(string RewardId) : PlayerAction;
public sealed record ProceedAfterRewardsAction : PlayerAction;
public sealed record RestSiteHealAction : PlayerAction;
public sealed record RestSiteUpgradeCardAction(string CardInstanceId) : PlayerAction;

public sealed record MapPointView(string Id, int Floor, int Column, MapPointType Type,
    IReadOnlyList<string> Children);

public sealed record RunMapVisitView(int ActIndex, string PointId, MapPointType MapPointType,
    IReadOnlyList<RunRoomType> RoomTypes);

public enum RunRewardKind { Gold, Card, Relic, Potion }
public sealed record RunRewardView(string Id, RunRewardKind Kind, int Amount,
    IReadOnlyList<CardInstance> CardOptions, ContentId? RelicId = null, ContentId? PotionId = null);
public sealed record ClaimCardRewardAction(string RewardId, string CardInstanceId) : PlayerAction;
public sealed record RunEventOptionView(ContentId Id, string Label, bool IsEnabled = true);
public sealed record PendingRunCardSelectionView(string Prompt, IReadOnlyList<CardInstance> Candidates, bool CanSkip);

public sealed record RunRoomFrameView(RunRoomType RoomType, string? MapPointId, bool IsFinalEvent,
    ContentId? EncounterId, ContentId? EventId, ContentId? EventPageId, string? EventPageDescription,
    IReadOnlyList<ContentId> EnemyIds,
    IReadOnlyList<RunRewardView> Rewards, IReadOnlyList<RunEventOptionView> EventOptions);

public sealed record RunObservation(ContentId Character, int Hp, int MaxHp, int Gold,
    IReadOnlyList<ContentId> Deck, IReadOnlyList<ContentId> Relics, IReadOnlyList<ContentId?> Potions,
    int ActIndex, string CurrentPointId, MapPointType? CurrentMapPointType,
    RunRoomType? CurrentRoom,
    RunPhase Phase, RunStatus Status, IReadOnlyList<MapPointView> Map, IReadOnlyList<RunMapVisitView> VisitedMapPoints,
    IReadOnlyList<RunRoomFrameView> RoomStack, IReadOnlyList<CardInstance> DeckInstances,
    PendingRunCardSelectionView? PendingRunCardSelection = null);

public sealed class RunRoomResult
{
    public RunRoomResult(RunOutcome outcome, int hp, int maxHp, int gold,
        IEnumerable<ContentId> deck, IEnumerable<ContentId> relics, IEnumerable<ContentId?>? potions = null,
        IEnumerable<CardInstance>? deckInstances = null)
    {
        ArgumentNullException.ThrowIfNull(deck);
        ArgumentNullException.ThrowIfNull(relics);
        var cardIds = deck.ToArray();
        var instances = deckInstances?.ToArray() ?? cardIds.Select((cardId, index) =>
            new CardInstance($"room-result-card-{index:D4}", cardId)).ToArray();
        if (!cardIds.SequenceEqual(instances.Select(card => card.CardId)))
            throw new ArgumentException("Deck card IDs must match the provided card instances.", nameof(deckInstances));
        if (instances.Select(card => card.InstanceId).Distinct(StringComparer.Ordinal).Count() != instances.Length)
            throw new ArgumentException("Deck card instance IDs must be unique.", nameof(deckInstances));
        Outcome = outcome;
        Hp = hp;
        MaxHp = maxHp;
        Gold = gold;
        Deck = Array.AsReadOnly(cardIds);
        DeckInstances = Array.AsReadOnly(instances);
        Relics = Array.AsReadOnly(relics.ToArray());
        Potions = Array.AsReadOnly(potions?.ToArray() ?? []);
    }

    public RunOutcome Outcome { get; }
    public int Hp { get; }
    public int MaxHp { get; }
    public int Gold { get; }
    public IReadOnlyList<ContentId> Deck { get; }
    public IReadOnlyList<CardInstance> DeckInstances { get; }
    public IReadOnlyList<ContentId> Relics { get; }
    public IReadOnlyList<ContentId?> Potions { get; }
}

public interface IRunHook
{
    ContentId Id { get; }
    void AfterCombatVictory(RunHookContext context);
    void AfterRelicObtained(RunHookContext context, ContentId relicId) { }
    void AfterCombatStarted(RunHookContext context) { }
    bool ShouldForcePotionReward(RunObservation observation, RunRoomType roomType) => false;
    IReadOnlySet<RunRoomType> ModifyUnknownRoomTypes(RunObservation observation,
        IReadOnlySet<RunRoomType> roomTypes) => roomTypes;
    float ModifyUnknownOddsIncrease(RunObservation observation, RunRoomType roomType, float increase) => increase;
    CardRewardOdds ModifyCardRewardOdds(RunObservation observation, RunRoomType roomType,
        CardRewardOdds odds) => odds;
    bool ShouldGenerateTreasure(RunObservation observation) => true;
    int ModifyRestSiteHealAmount(RunObservation observation, int amount) => amount;
    void AfterRestSiteHeal(RunHookContext context) { }
    void AfterRestSiteSmith(RunHookContext context) { }
}

public readonly record struct CardRewardOdds(float RareChance, float UncommonChance);

public sealed class RunHookContext
{
    private readonly RunState _state;
    private readonly string _modId;

    internal RunHookContext(RunState state, ContentId hookId)
    {
        _state = state;
        HookId = hookId;
        _modId = hookId.Value[..hookId.Value.IndexOf(':')];
    }

    public ContentId HookId { get; }
    public int Hp => _state.Hp;
    public int MaxHp => _state.MaxHp;
    public bool HasRelic(ContentId relic) => _state.Relics.Contains(relic);
    public int GetInt(string key) => _state.ReadModInt(_modId, key);
    public void SetInt(string key, int value) => _state.SetModInt(_modId, key, value);

    public void Heal(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        _state.Hp = Math.Min(_state.MaxHp, _state.Hp + amount);
    }

    public void GainBlock(int amount)
    {
        var combat = _state.ActiveCombat ?? throw new InvalidOperationException("There is no active combat for this run hook.");
        EffectExecutor.Execute(combat, combat.State.Player.Creature, new GainBlockEffect(amount));
    }

    public void ApplyPower(ContentId power, int amount)
    {
        var combat = _state.ActiveCombat ?? throw new InvalidOperationException("There is no active combat for this run hook.");
        EffectExecutor.Execute(combat, combat.State.Player.Creature,
            new ApplyPowerEffect(power, amount, TargetSelector.Self));
    }

    public void ChangePotionCapacity(int amount) => _state.ChangePotionCapacity(amount);
}

internal sealed class RunState
{
    private readonly ScopedIntState _modInts;
    public RunState(RunDefinition definition, CharacterDefinition character, RandomStreams random,
        IReadOnlyDictionary<ContentId, EncounterDefinition>? encounters = null,
        IReadOnlyDictionary<ContentId, RunEventDefinition>? runEvents = null,
        IReadOnlyDictionary<ContentId, RelicDefinition>? relicDefinitions = null,
        IReadOnlyDictionary<ContentId, PotionDefinition>? potionDefinitions = null,
        IReadOnlyDictionary<StateVariableKey, StateVariableDefinition>? stateVariables = null)
    {
        Definition = definition;
        _modInts = new ScopedIntState(stateVariables, StateVariableScope.Run);
        Character = character;
        Random = random;
        RewardRandom = random.Get("rewards");
        TreasureRelicRandom = random.Get("treasure_room_relics");
        CardRarityOdds = new CardRarityOddsState();
        Encounters = encounters;
        RunEvents = runEvents;
        RelicDefinitions = relicDefinitions;
        PotionDefinitions = potionDefinitions;
        FallbackRelicId = relicDefinitions?.Values.SingleOrDefault(relic => relic.IsFallback)?.Id;
        Hp = character.MaxHp;
        MaxHp = character.MaxHp;
        Gold = character.StartingGold;
        Deck = character.StartingDeck.Select((cardId, index) =>
            new CardInstance($"run-card-{index:D4}", cardId)).ToList();
        NextEventCardInstanceId = Deck.Count;
        Relics = [.. character.StartingRelics];
        PotionCapacity = character.PotionCapacity;
        Potions = Enumerable.Repeat<ContentId?>(null, PotionCapacity).ToList();
        for (var i = 0; i < character.StartingPotions.Count; i++) Potions[i] = character.StartingPotions[i];
        SharedRelicBags = new Dictionary<RunRelicRarity, List<ContentId>>();
        PlayerRelicBags = new Dictionary<RunRelicRarity, List<ContentId>>();
        if (relicDefinitions is not null)
        {
            var relicPoolRandom = random.Get("up_front");
            var rewardRarities = new[]
                { RunRelicRarity.Common, RunRelicRarity.Uncommon, RunRelicRarity.Rare, RunRelicRarity.Shop };
            foreach (var rarity in rewardRarities)
            {
                var bag = relicDefinitions.Values.Where(relic => relic.InSharedPool && relic.Rarity == rarity)
                    .Select(relic => relic.Id).OrderBy(id => id.Value, StringComparer.Ordinal).ToList();
                relicPoolRandom.Shuffle(bag);
                SharedRelicBags.Add(rarity, bag);
            }
            foreach (var rarity in rewardRarities)
            {
                var bag = relicDefinitions.Values.Where(relic => relic.Rarity == rarity
                        && (relic.InSharedPool || relic.CharacterPoolIds?.Contains(character.Id) == true))
                    .Select(relic => relic.Id).OrderBy(id => id.Value, StringComparer.Ordinal).ToList();
                relicPoolRandom.Shuffle(bag);
                PlayerRelicBags.Add(rarity, bag);
            }
        }
        UnknownRoomOdds = definition.UnknownRoomOdds is null ? null : new UnknownRoomOddsState(definition.UnknownRoomOdds);
        RoomContentStates = ActRoomContentState.Generate(definition, Random.Get("up_front"), encounters, runEvents);
        Map = GenerateMap(0);
        MapView = BuildMapView(Map);
        EnterMapStart();
    }

    public void EnterMapStart()
    {
        RoomStack.Clear();
        CurrentPointId = Map.StartPointId;
        var entryPoint = Map.Points[CurrentPointId];
        var ancientEventId = RoomContentStates[ActIndex]?.AncientEventId;
        if (entryPoint.Type == MapPointType.Ancient && ancientEventId is { } selectedAncient)
        {
            CurrentMapPointType = MapPointType.Ancient;
            RoomStack.Add(new RunRoomFrame(RunRoomType.Event, entryPoint.Id, false, null,
                selectedAncient, GetInitialEventPageId(selectedAncient), [],
                GetEventOptions(selectedAncient)));
            Phase = RoomStack[^1].EventOptions.Count > 0 ? RunPhase.ResolveEvent : RunPhase.ResolveRoom;
            VisitHistory.Add(new RunMapVisitView(ActIndex, entryPoint.Id, entryPoint.Type,
                Array.AsReadOnly(new[] { RunRoomType.Event })));
        }
        else
        {
            // An Ancient start without an assigned event must expose its route rather than stall the run.
            CurrentMapPointType = null;
            Phase = RunPhase.ChooseMapPoint;
        }
    }

    public RunDefinition Definition { get; }
    public CharacterDefinition Character { get; }
    public RandomStreams Random { get; }
    public SeededRandom RewardRandom { get; }
    public SeededRandom TreasureRelicRandom { get; }
    public CardRarityOddsState CardRarityOdds { get; }
    public int NextRewardCardInstanceId { get; set; }
    public IReadOnlyDictionary<ContentId, EncounterDefinition>? Encounters { get; }
    public IReadOnlyDictionary<ContentId, RunEventDefinition>? RunEvents { get; }
    public IReadOnlyDictionary<ContentId, RelicDefinition>? RelicDefinitions { get; }
    public IReadOnlyDictionary<ContentId, PotionDefinition>? PotionDefinitions { get; }
    public ContentId? FallbackRelicId { get; }
    public Dictionary<RunRelicRarity, List<ContentId>> SharedRelicBags { get; }
    public Dictionary<RunRelicRarity, List<ContentId>> PlayerRelicBags { get; }
    public int NextEventCardInstanceId { get; set; }
    public PendingRunCardSelectionState? PendingRunCardSelection { get; set; }
    public UnknownRoomOddsState? UnknownRoomOdds { get; }
    public IReadOnlyList<ActRoomContentState?> RoomContentStates { get; }
    public HashSet<ContentId> VisitedEventIds { get; } = [];
    public int Hp { get; set; }
    public int MaxHp { get; set; }
    public int Gold { get; set; }
    public int NextCombatGoldRewardIndex { get; set; }
    public List<CardInstance> Deck { get; }
    public List<ContentId> Relics { get; }
    public List<ContentId?> Potions { get; }
    public int PotionCapacity { get; private set; }
    public float PotionRewardChance { get; set; } = 0.4f;
    public int ActIndex { get; set; }
    public ActMapDefinition Map { get; set; }
    public IReadOnlyList<MapPointView> MapView { get; set; }
    public List<RunMapVisitView> VisitHistory { get; } = [];
    public List<RunRoomFrame> RoomStack { get; } = [];
    public GameSession? ActiveCombat { get; set; }
    public RunRoomType? CurrentRoom => RoomStack.Count == 0 ? null : RoomStack[^1].RoomType;
    public ContentId? CurrentEncounterId => RoomStack.Count == 0 ? null : RoomStack[^1].EncounterId;

    internal int LoseGold(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        var lost = Math.Min(Gold, amount);
        Gold -= lost;
        return lost;
    }

    internal void AddCombatGoldReward(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        if (amount == 0) return;
        if (RoomStack.Count == 0) throw new InvalidOperationException("A combat reward requires an active room.");
        var rewardId = $"combat-extra-gold-{NextCombatGoldRewardIndex++}";
        RoomStack[^1].Rewards.Add(new RunRewardState(rewardId, RunRewardKind.Gold, amount));
    }
    internal int GetModInt(string modId, string key, int defaultValue = 0) => _modInts.Get(modId, key, defaultValue);
    internal int ReadModInt(string modId, string key) => _modInts.GetDeclared(modId, key);
    internal void SetModInt(string modId, string key, int value) => _modInts.Set(modId, key, value);
    public IReadOnlyList<EncounterEnemySelection> CurrentEncounterSelections =>
        RoomStack.Count == 0 ? Array.Empty<EncounterEnemySelection>() : RoomStack[^1].EnemySelections;

    public ContentId? TakeRelic(RunRelicRarity rarity, RunRelicPool pool)
    {
        if (!Enum.IsDefined(pool)) throw new ArgumentOutOfRangeException(nameof(pool));
        var bags = pool == RunRelicPool.Shared ? SharedRelicBags : PlayerRelicBags;
        if (!bags.TryGetValue(rarity, out var bag)) return FallbackRelicId;
        var index = bag.FindIndex(id => !Relics.Contains(id));
        if (index < 0) return FallbackRelicId;
        var relicId = bag[index];
        bag.RemoveAt(index);
        return relicId;
    }

    public void RemoveRelicFromBags(ContentId relicId)
    {
        foreach (var bag in SharedRelicBags.Values) bag.Remove(relicId);
        foreach (var bag in PlayerRelicBags.Values) bag.Remove(relicId);
    }

    public int FindEmptyPotionSlot() => Potions.FindIndex(potion => potion is null);

    public bool TryAddPotion(ContentId potionId)
    {
        var slot = FindEmptyPotionSlot();
        if (slot < 0) return false;
        Potions[slot] = potionId;
        return true;
    }

    public bool TryUsePotion(int slot, out ContentId potionId)
    {
        if ((uint)slot >= (uint)Potions.Count || Potions[slot] is not { } potion)
        {
            potionId = default;
            return false;
        }
        Potions[slot] = null;
        potionId = potion;
        return true;
    }

    public void ReplacePotions(IEnumerable<ContentId?> potions)
    {
        var slots = potions.ToArray();
        if (slots.Length > Potions.Count)
            throw new ArgumentException("Potion inventory exceeds the player's potion capacity.", nameof(potions));
        Potions.Clear();
        Potions.AddRange(slots);
        while (Potions.Count < PotionCapacity) Potions.Add(null);
    }

    public void ChangePotionCapacity(int amount)
    {
        var newCapacity = checked(PotionCapacity + amount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(newCapacity);
        if (newCapacity == PotionCapacity) return;
        if (newCapacity > PotionCapacity)
        {
            Potions.AddRange(Enumerable.Repeat<ContentId?>(null, newCapacity - PotionCapacity));
            PotionCapacity = newCapacity;
            return;
        }

        for (var slot = Potions.Count - 1; slot >= newCapacity; slot--)
        {
            if (Potions[slot] is { } potion)
            {
                var openSlot = Potions.FindIndex(0, newCapacity, item => item is null);
                if (openSlot >= 0) Potions[openSlot] = potion;
            }
            Potions.RemoveAt(slot);
        }
        PotionCapacity = newCapacity;
    }

    public ContentId? CurrentEventId => RoomStack.Count == 0 ? null : RoomStack[^1].EventId;
    public ContentId? SelectEncounter(RunRoomType roomType) => roomType is RunRoomType.Monster or RunRoomType.Elite or RunRoomType.Boss
        ? ActIndex < RoomContentStates.Count ? RoomContentStates[ActIndex]?.NextEncounter(roomType) : null
        : null;
    public ContentId? SelectEvent(RunRoomType roomType) => roomType == RunRoomType.Event && ActIndex < RoomContentStates.Count
        ? RoomContentStates[ActIndex]?.NextEvent(VisitedEventIds)
        : null;

    public IReadOnlyList<RunEventOptionDefinition> GetEventOptions(ContentId? eventId) =>
        eventId is { } id && RunEvents is not null && RunEvents.TryGetValue(id, out var runEvent)
            ? runEvent.Options
            : Array.Empty<RunEventOptionDefinition>();

    public ContentId? GetInitialEventPageId(ContentId? eventId) =>
        eventId is { } id && RunEvents is not null && RunEvents.TryGetValue(id, out var runEvent)
            ? runEvent.InitialPageId
            : null;

    public RunEventPageDefinition GetEventPage(ContentId eventId, ContentId pageId) =>
        RunEvents is not null && RunEvents.TryGetValue(eventId, out var runEvent)
            && runEvent.Pages.TryGetValue(pageId, out var page)
                ? page
                : throw new KeyNotFoundException($"Unknown event page '{eventId}/{pageId}'.");

    public RunRoomFrame CreateRoomFrame(RunRoomType roomType, string? mapPointId, ContentId? eventId)
    {
        var encounterId = SelectEncounter(roomType);
        var enemySelections = encounterId is { } id && Encounters is not null && Encounters.TryGetValue(id, out var encounter)
            ? encounter.SelectEnemies(Random.Get("encounter-enemies"))
            : Array.Empty<EncounterEnemySelection>();
        return new RunRoomFrame(roomType, mapPointId, false, encounterId, eventId,
            GetInitialEventPageId(eventId), enemySelections,
            GetEventOptions(eventId));
    }

    public void BindEncounterToCurrentRoom(EncounterDefinition encounter)
    {
        ArgumentNullException.ThrowIfNull(encounter);
        if (RoomStack.Count == 0) throw new InvalidOperationException("There is no active room for the encounter.");
        var frame = RoomStack[^1];
        if (frame.RoomType != encounter.RoomType)
            throw new InvalidOperationException($"Encounter '{encounter.Id}' is incompatible with active room '{frame.RoomType}'.");
        if (frame.EncounterId is { } selected && selected != encounter.Id)
            throw new InvalidOperationException($"Encounter '{encounter.Id}' does not match the encounter selected for this room.");
        if (frame.EncounterId is not null) return;

        var selections = encounter.SelectEnemies(Random.Get("encounter-enemies"));
        RoomStack[^1] = frame with { EncounterId = encounter.Id, EnemySelections = selections };
    }
    public string CurrentPointId { get; set; } = string.Empty;
    public MapPointType? CurrentMapPointType { get; set; }
    public RunPhase Phase { get; set; } = RunPhase.ChooseMapPoint;
    public RunStatus Status { get; set; } = RunStatus.InProgress;

    public ActMapDefinition GenerateMap(int actIndex) => Definition.Acts[actIndex]
        .GenerateMap(Random.Get($"act_{actIndex + 1}_map"));

    public static IReadOnlyList<MapPointView> BuildMapView(ActMapDefinition map) =>
        Array.AsReadOnly(map.Points.Values.OrderBy(point => point.Floor).ThenBy(point => point.Column)
            .Select(point => new MapPointView(point.Id, point.Floor, point.Column, point.Type, point.Children)).ToArray());
}

public sealed class RunSession
{
    internal RunSession(RunState state) => State = state;
    internal RunState State { get; }
}

internal sealed record RunRoomFrame(RunRoomType RoomType, string? MapPointId, bool IsFinalEvent,
    ContentId? EncounterId, ContentId? EventId, ContentId? EventPageId,
    IReadOnlyList<EncounterEnemySelection> EnemySelections,
    IReadOnlyList<RunEventOptionDefinition> EventOptions, bool ResumeParentAfter = true)
{
    public List<RunRewardState> Rewards { get; } = [];
}

internal sealed record PendingRunCardSelectionState(ChooseRunCardEffect Selection,
    IReadOnlyList<CardInstance> Candidates, IReadOnlyList<RunEffectNode> RemainingEffects,
    bool IsFinalEvent, ContentId? NextPageId);

internal sealed record RunRewardState(string Id, RunRewardKind Kind, int Amount,
    IReadOnlyList<CardInstance>? CardOptions = null, ContentId? RelicId = null, ContentId? PotionId = null);

/// <summary>Single-player run lifecycle and map-choice layer. Room rules are resolved by room-specific environments.</summary>
public sealed class RunEnvironment
{
    private readonly RunDefinition _definition;
    private readonly CharacterDefinition _character;
    private readonly IReadOnlyList<IRunHook> _runHooks;
    private readonly IReadOnlyDictionary<ContentId, EncounterDefinition>? _encounters;
    private readonly IReadOnlyDictionary<ContentId, RunEventDefinition>? _runEvents;
    private readonly IReadOnlyDictionary<ContentId, CardDefinition>? _cards;
    private readonly IReadOnlyDictionary<ContentId, CardPoolDefinition>? _cardPools;
    private readonly IReadOnlyDictionary<ContentId, ICustomRunEffectHandler> _customRunEffects;
    private readonly IReadOnlyDictionary<ContentId, RelicDefinition>? _relicDefinitions;
    private readonly IReadOnlyDictionary<ContentId, PotionDefinition>? _potionDefinitions;
    private readonly IReadOnlyDictionary<StateVariableKey, StateVariableDefinition>? _stateVariables;

    public RunEnvironment(RunDefinition definition, CharacterDefinition character, IEnumerable<IRunHook>? runHooks = null,
        IEnumerable<CardDefinition>? cards = null, IEnumerable<CardPoolDefinition>? cardPools = null,
        IEnumerable<EncounterDefinition>? encounters = null,
        IEnumerable<RunEventDefinition>? runEvents = null,
        IEnumerable<ICustomRunEffectHandler>? customRunEffects = null,
        IEnumerable<RelicDefinition>? relicDefinitions = null,
        IEnumerable<PotionDefinition>? potionDefinitions = null)
    {
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _character = character ?? throw new ArgumentNullException(nameof(character));
        _runHooks = Array.AsReadOnly(runHooks?.ToArray() ?? []);
        _cards = cards?.ToDictionary(card => card.Id);
        _cardPools = cardPools?.ToDictionary(pool => pool.Id);
        _encounters = encounters?.ToDictionary(encounter => encounter.Id);
        _runEvents = runEvents?.ToDictionary(runEvent => runEvent.Id);
        _customRunEffects = new ReadOnlyDictionary<ContentId, ICustomRunEffectHandler>(
            customRunEffects?.ToDictionary(handler => handler.Id) ?? []);
        _relicDefinitions = relicDefinitions?.ToDictionary(relic => relic.Id);
        _potionDefinitions = potionDefinitions?.ToDictionary(potion => potion.Id);
    }

    public RunEnvironment(GameDefinition gameDefinition, ContentId runId, ContentId characterId)
        : this(GetRun(gameDefinition, runId), GetCharacter(gameDefinition, characterId), gameDefinition.RunHooks)
    {
        _encounters = gameDefinition.Encounters;
        _runEvents = gameDefinition.RunEvents;
        _cards = gameDefinition.Cards;
        _cardPools = gameDefinition.CardPools;
        _customRunEffects = gameDefinition.CustomRunEffects;
        _relicDefinitions = gameDefinition.Relics;
        _potionDefinitions = gameDefinition.Potions;
        _stateVariables = gameDefinition.StateVariables;
    }

    public RunSession Start(uint seed)
    {
        return new RunSession(new RunState(_definition, _character, new RandomStreams(seed),
            _encounters, _runEvents, _relicDefinitions, _potionDefinitions, _stateVariables));
    }

    public RunSession Start(string seed)
    {
        ArgumentNullException.ThrowIfNull(seed);
        return new RunSession(new RunState(_definition, _character, RandomStreams.FromStringSeed(seed),
            _encounters, _runEvents, _relicDefinitions, _potionDefinitions, _stateVariables));
    }

    public RunObservation Observe(RunSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return ObserveState(session.State);
    }

    private RunObservation ObserveState(RunState state)
    {
        var activePlayer = state.ActiveCombat?.State.Player.Creature;
        return new RunObservation(state.Character.Id, activePlayer?.Hp ?? state.Hp,
            activePlayer?.MaxHp ?? state.MaxHp, state.Gold,
            Array.AsReadOnly(state.Deck.Select(card => card.CardId).ToArray()), Array.AsReadOnly(state.Relics.ToArray()),
            Array.AsReadOnly(state.Potions.ToArray()), state.ActIndex,
            state.CurrentPointId, state.CurrentMapPointType, state.CurrentRoom,
            state.Phase, state.Status, state.MapView, Array.AsReadOnly(state.VisitHistory.ToArray()),
            Array.AsReadOnly(state.RoomStack.Select(frame => ObserveRoomFrame(frame, state)).ToArray()),
            Array.AsReadOnly(state.Deck.ToArray()), state.PendingRunCardSelection is { } pending
                ? new PendingRunCardSelectionView(pending.Selection.Prompt, pending.Candidates, pending.Selection.CanSkip)
                : null);
    }

    private RunRoomFrameView ObserveRoomFrame(RunRoomFrame frame, RunState state) => new(
        frame.RoomType, frame.MapPointId, frame.IsFinalEvent, frame.EncounterId, frame.EventId, frame.EventPageId,
        frame.EventId is { } eventId && frame.EventPageId is { } pageId
            ? state.GetEventPage(eventId, pageId).Description
            : null,
        Array.AsReadOnly(frame.EnemySelections.Select(enemy => enemy.EnemyId).ToArray()),
        Array.AsReadOnly(frame.Rewards.Select(reward => new RunRewardView(reward.Id, reward.Kind,
            reward.Amount, reward.CardOptions ?? Array.Empty<CardInstance>(), reward.RelicId, reward.PotionId)).ToArray()),
        Array.AsReadOnly(frame.EventOptions.Select(option => new RunEventOptionView(option.Id, option.Label,
            IsEventOptionLegal(option, state))).ToArray()));

    public IReadOnlyList<PlayerAction> LegalActions(RunSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var state = session.State;
        if (state.Status != RunStatus.InProgress) return [];
        if (state.Phase == RunPhase.ChooseMapPoint)
            return state.Map.Points[state.CurrentPointId].Children
                .Select(id => (PlayerAction)new ChooseMapPointAction(id)).ToArray();
        if (state.Phase == RunPhase.ResolveEventCardSelection && state.PendingRunCardSelection is { } pendingSelection)
        {
            var actions = pendingSelection.Candidates
                .Select(card => (PlayerAction)new ChooseRunCardAction(card.InstanceId)).ToList();
            if (pendingSelection.Selection.CanSkip) actions.Add(new SkipRunCardSelectionAction());
            return Array.AsReadOnly(actions.ToArray());
        }
        if (state.Phase == RunPhase.ResolveEvent && state.RoomStack.Count > 0)
            return state.RoomStack[^1].EventOptions
                .Where(option => IsEventOptionLegal(option, state))
                .Select(option => (PlayerAction)new ChooseRunEventOptionAction(option.Id)).ToArray();
        if (state.Phase == RunPhase.ResolveFinalEvent && state.RoomStack.Count > 0
            && state.RoomStack[^1].EventOptions.Count > 0)
            return state.RoomStack[^1].EventOptions
                .Where(option => IsEventOptionLegal(option, state))
                .Select(option => (PlayerAction)new ChooseRunEventOptionAction(option.Id)).ToArray();
        if (state.Phase == RunPhase.ResolveRewards && state.RoomStack.Count > 0)
        {
            var rewards = state.RoomStack[^1].Rewards;
            return rewards.Count == 0
                ? [new ProceedAfterRewardsAction()]
                : rewards.SelectMany(reward => reward.Kind switch
                    {
                        RunRewardKind.Gold => [(PlayerAction)new ClaimGoldRewardAction(reward.Id)],
                        RunRewardKind.Card => reward.CardOptions!.Select(card =>
                            (PlayerAction)new ClaimCardRewardAction(reward.Id, card.InstanceId)).ToArray(),
                        RunRewardKind.Relic => [(PlayerAction)new ClaimRelicRewardAction(reward.Id)],
                        RunRewardKind.Potion when state.Potions.Any(potion => potion is null) =>
                            [(PlayerAction)new ClaimPotionRewardAction(reward.Id)],
                        _ => Array.Empty<PlayerAction>()
                    })
                    .Append(new SkipRewardSetAction()).ToArray();
        }
        if (state.Phase == RunPhase.ResolveTreasure && state.RoomStack.Count > 0
            && state.RoomStack[^1].RoomType == RunRoomType.Treasure)
            return [new OpenTreasureChestAction()];
        if (state.Phase == RunPhase.ResolveRestSite && state.RoomStack.Count > 0
            && state.RoomStack[^1].RoomType == RunRoomType.RestSite)
        {
            var choices = new List<PlayerAction> { new RestSiteHealAction() };
            if (_cards is not null)
                choices.AddRange(state.Deck.Where(instance => instance.UpgradeLevel == 0
                        && _cards.TryGetValue(instance.CardId, out var card)
                        && card.HasUpgrade)
                    .Select(instance => (PlayerAction)new RestSiteUpgradeCardAction(instance.InstanceId)));
            return Array.AsReadOnly(choices.ToArray());
        }
        return [];
    }

    public RunObservation Step(RunSession session, PlayerAction action)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(action);
        var state = session.State;
        if (state.Phase == RunPhase.ResolveRewards)
            return ResolveRewardAction(session, action);
        if (state.Phase == RunPhase.ResolveTreasure)
            return OpenTreasureChest(session, action);
        if (state.Phase == RunPhase.ResolveEvent)
            return ResolveRunEventAction(session, action);
        if (state.Phase == RunPhase.ResolveEventCardSelection)
            return ResolveRunCardSelectionAction(session, action);
        if (state.Phase == RunPhase.ResolveFinalEvent && state.RoomStack.Count > 0
            && state.RoomStack[^1].EventOptions.Count > 0)
            return ResolveRunEventAction(session, action);
        if (state.Phase == RunPhase.ResolveRestSite)
            return ResolveRestSiteAction(session, action);
        if (action is not ChooseMapPointAction choose || !LegalActions(session).Contains(choose))
            throw new InvalidOperationException("A reachable map point must be selected.");

        var point = state.Map.Points[choose.PointId];
        state.CurrentPointId = point.Id;
        state.CurrentMapPointType = point.Type;
        var blacklist = BuildRoomTypeBlacklist(state, point);
        var roomType = ResolveRoomType(state, point, blacklist);
        var frame = state.CreateRoomFrame(roomType, point.Id, state.SelectEvent(roomType));
        state.RoomStack.Add(frame);
        state.VisitHistory.Add(new RunMapVisitView(state.ActIndex, point.Id, point.Type,
            Array.AsReadOnly(new[] { roomType })));
        state.Phase = RunPhase.ResolveRoom;
        if (frame.EventOptions.Count > 0) state.Phase = RunPhase.ResolveEvent;
        if (roomType == RunRoomType.RestSite) state.Phase = RunPhase.ResolveRestSite;
        if (roomType == RunRoomType.Treasure) state.Phase = RunPhase.ResolveTreasure;
        return Observe(session);
    }

    private RunObservation OpenTreasureChest(RunSession session, PlayerAction action)
    {
        var state = session.State;
        if (state.RoomStack.Count == 0 || state.RoomStack[^1].RoomType != RunRoomType.Treasure
            || action is not OpenTreasureChestAction || !LegalActions(session).Contains(action))
            throw new InvalidOperationException("There is no unopened treasure chest.");

        var observation = Observe(session);
        if (_runHooks.All(hook => hook.ShouldGenerateTreasure(observation)))
        {
            var gold = state.RewardRandom.NextInt(42, 53);
            state.Gold = checked(state.Gold + gold);
            var rarityRoll = state.TreasureRelicRandom.NextFloat();
            var rarity = rarityRoll < 0.5f ? RunRelicRarity.Common
                : rarityRoll < 0.83f ? RunRelicRarity.Uncommon : RunRelicRarity.Rare;
            if (state.TakeRelic(rarity, RunRelicPool.Shared) is { } relicId)
                state.RoomStack[^1].Rewards.Add(new RunRewardState("treasure:relic",
                    RunRewardKind.Relic, 0, RelicId: relicId));
        }

        if (state.RoomStack[^1].Rewards.Count == 0)
            return FinishCompletedRoom(session, state.RoomStack[^1]);
        state.Phase = RunPhase.ResolveRewards;
        return Observe(session);
    }

    private RunObservation ResolveRestSiteAction(RunSession session, PlayerAction action)
    {
        var state = session.State;
        if (state.RoomStack.Count == 0 || state.RoomStack[^1].RoomType != RunRoomType.RestSite)
            throw new InvalidOperationException("There is no active rest site.");
        if (!LegalActions(session).Contains(action))
            throw new InvalidOperationException("The selected rest site action is not currently legal.");

        if (action is RestSiteHealAction)
        {
            var amount = (int)(state.MaxHp * 0.3m);
            var observation = Observe(session);
            foreach (var hook in _runHooks)
                amount = hook.ModifyRestSiteHealAmount(observation, amount);
            ArgumentOutOfRangeException.ThrowIfNegative(amount);
            state.Hp = Math.Min(state.MaxHp, checked(state.Hp + amount));
            foreach (var hook in _runHooks) hook.AfterRestSiteHeal(new RunHookContext(state, hook.Id));
        }
        else if (action is RestSiteUpgradeCardAction upgrade)
        {
            var index = state.Deck.FindIndex(card => card.InstanceId == upgrade.CardInstanceId);
            if (index < 0) throw new InvalidOperationException("The selected card is no longer in the deck.");
            var card = state.Deck[index];
            state.Deck[index] = card.WithUpgradeLevel(card.UpgradeLevel + 1);
            foreach (var hook in _runHooks) hook.AfterRestSiteSmith(new RunHookContext(state, hook.Id));
        }

        return FinishCompletedRoom(session, state.RoomStack[^1]);
    }

    private RunObservation ResolveRunEventAction(RunSession session, PlayerAction action)
    {
        var state = session.State;
        if (state.RoomStack.Count == 0 || state.RoomStack[^1].EventId is null
            || state.Phase is not (RunPhase.ResolveEvent or RunPhase.ResolveFinalEvent))
            throw new InvalidOperationException("There is no active run event.");
        if (action is not ChooseRunEventOptionAction choose || !LegalActions(session).Contains(action))
            throw new InvalidOperationException("The selected event option is not currently legal.");

        var frame = state.RoomStack[^1];
        var option = frame.EventOptions.Single(candidate => candidate.Id == choose.OptionId);
        return ExecuteRunEventEffects(session, frame, option.Effects, frame.IsFinalEvent, option.NextPageId);
    }

    private RunObservation ExecuteRunEventEffects(RunSession session, RunRoomFrame frame,
        IReadOnlyList<RunEffectNode> effects, bool isFinalEvent, ContentId? nextPageId = null)
    {
        var state = session.State;
        var roomDepth = state.RoomStack.Count;
        for (var index = 0; index < effects.Count; index++)
        {
            var effect = effects[index];
            if (effect is ChooseRunCardEffect chooseCard)
            {
                var candidates = GetRunCardCandidates(state, chooseCard);
                if (candidates.Count == 0)
                {
                    if (chooseCard.CanSkip) continue;
                    throw new InvalidOperationException("The event card selection has no available cards.");
                }
                state.PendingRunCardSelection = new PendingRunCardSelectionState(chooseCard,
                    candidates, Array.AsReadOnly(effects.Skip(index + 1).ToArray()), isFinalEvent, nextPageId);
                state.Phase = RunPhase.ResolveEventCardSelection;
                return Observe(session);
            }

            ApplyRunEventEffect(state, frame, effect, selectedCard: null);
            if (state.RoomStack.Count > roomDepth) return Observe(session);
            if (state.Hp == 0)
            {
                state.Status = RunStatus.Lost;
                state.Phase = RunPhase.Terminal;
                return Observe(session);
            }
            if (state.Status == RunStatus.Won) return Observe(session);
        }

        if (nextPageId is { } pageId)
        {
            if (frame.EventId is not { } eventId)
                throw new InvalidOperationException("Only an active run event can transition to another page.");
            var page = state.GetEventPage(eventId, pageId);
            if (page.Options.Count == 0)
            {
                if (isFinalEvent)
                    throw new InvalidOperationException("A final event cannot transition to an empty page.");
                return FinishCompletedRoom(session, frame);
            }
            var updatedFrame = frame with { EventPageId = page.Id, EventOptions = page.Options };
            state.RoomStack[^1] = updatedFrame;
            state.Phase = isFinalEvent ? RunPhase.ResolveFinalEvent : RunPhase.ResolveEvent;
            return Observe(session);
        }

        if (isFinalEvent)
            throw new InvalidOperationException("A final event option did not complete the run.");
        return FinishCompletedRoom(session, frame);
    }

    private RunObservation ResolveRunCardSelectionAction(RunSession session, PlayerAction action)
    {
        var state = session.State;
        var pending = state.PendingRunCardSelection
            ?? throw new InvalidOperationException("There is no pending run card selection.");
        var frame = state.RoomStack[^1];
        if (action is SkipRunCardSelectionAction && pending.Selection.CanSkip)
        {
            state.PendingRunCardSelection = null;
            state.Phase = pending.IsFinalEvent ? RunPhase.ResolveFinalEvent : RunPhase.ResolveEvent;
            return ExecuteRunEventEffects(session, frame, pending.RemainingEffects, pending.IsFinalEvent,
                pending.NextPageId);
        }
        if (action is not ChooseRunCardAction choose
            || !pending.Candidates.Any(card => card.InstanceId == choose.CardInstanceId))
            throw new InvalidOperationException("The selected run card is not currently available.");

        var cardIndex = state.Deck.FindIndex(card => card.InstanceId == choose.CardInstanceId);
        if (cardIndex < 0) throw new InvalidOperationException("The selected run card is no longer in the deck.");
        var selectedCard = state.Deck[cardIndex];
        state.PendingRunCardSelection = null;
        state.Phase = pending.IsFinalEvent ? RunPhase.ResolveFinalEvent : RunPhase.ResolveEvent;
        ApplyRunEventEffect(state, frame, pending.Selection.OnSelected, selectedCard);
        if (state.Hp == 0)
        {
            state.Status = RunStatus.Lost;
            state.Phase = RunPhase.Terminal;
            return Observe(session);
        }
        if (state.Status == RunStatus.Won) return Observe(session);
        return ExecuteRunEventEffects(session, frame, pending.RemainingEffects, pending.IsFinalEvent,
            pending.NextPageId);
    }

    private void ApplyRunEventEffect(RunState state, RunRoomFrame frame,
        RunEffectNode effect, CardInstance? selectedCard)
    {
            switch (effect)
            {
                case GainRunGoldEffect gainGold:
                    RunEffectCommands.GainGold(state, gainGold.Amount);
                    break;
                case LoseRunGoldEffect loseGold:
                    state.Gold -= loseGold.Amount;
                    break;
                case HealRunEffect heal:
                    RunEffectCommands.Heal(state, heal.Amount);
                    break;
                case GainRunMaxHpEffect gainMaxHp:
                    RunEffectCommands.GainMaxHp(state, gainMaxHp.Amount);
                    break;
                case LoseRunMaxHpEffect loseMaxHp:
                    RunEffectCommands.LoseMaxHp(state, loseMaxHp.Amount);
                    break;
                case LoseRunHpEffect loseHp:
                    RunEffectCommands.LoseHp(state, loseHp.Amount);
                    break;
                case AddCardToRunDeckEffect addCard:
                    RunEffectCommands.AddCardToDeck(state, addCard.Card);
                    break;
                case GainRunRelicEffect gainRelic:
                    if (state.Relics.Contains(gainRelic.RelicId)
                        && state.RelicDefinitions?.TryGetValue(gainRelic.RelicId, out var heldRelic) == true
                        && !heldRelic.IsStackable)
                        throw new InvalidOperationException($"Unique relic '{gainRelic.RelicId}' is already owned.");
                    state.Relics.Add(gainRelic.RelicId);
                    break;
                case EnterRunCombatEffect enterCombat:
                    if (frame.EventId is null)
                        throw new InvalidOperationException("An event combat can only be entered from a run event.");
                    var encounter = state.Encounters is not null
                        && state.Encounters.TryGetValue(enterCombat.EncounterId, out var selectedEncounter)
                            ? selectedEncounter
                            : throw new InvalidOperationException($"Unknown encounter '{enterCombat.EncounterId}'.");
                    var enemySelections = encounter.SelectEnemies(state.Random.Get("encounter-enemies"));
                    state.RoomStack.Add(new RunRoomFrame(encounter.RoomType, null, false, encounter.Id,
                        null, null, enemySelections, [], enterCombat.ResumeEventAfterCombat));
                    state.Phase = RunPhase.ResolveRoom;
                    break;
                case CustomRunEffect custom:
                    if (!_customRunEffects.TryGetValue(custom.Handler, out var handler))
                        throw new InvalidOperationException($"Unknown custom run effect '{custom.Handler}'.");
                    handler.Execute(new RunEffectExecutionContext(state, custom.Handler, _cards), custom.Payload);
                    break;
                case RemoveSelectedRunCardEffect when selectedCard is { } selectedToRemove:
                    state.Deck.RemoveAll(card => card.InstanceId == selectedToRemove.InstanceId);
                    break;
                case UpgradeSelectedRunCardEffect when selectedCard is { } selectedToUpgrade:
                    var selectedIndex = state.Deck.FindIndex(card => card.InstanceId == selectedToUpgrade.InstanceId);
                    if (selectedIndex < 0) throw new InvalidOperationException("The selected run card is no longer in the deck.");
                    state.Deck[selectedIndex] = selectedToUpgrade.WithUpgradeLevel(
                        selectedToUpgrade.UpgradeLevel + 1);
                    break;
                case WinRunEffect when frame.IsFinalEvent:
                    state.Status = RunStatus.Won;
                    state.Phase = RunPhase.Terminal;
                    break;
                case WinRunEffect:
                    throw new InvalidOperationException("Only the final event can win the run.");
                default:
                    throw new InvalidOperationException($"Unsupported run event effect '{effect.GetType().Name}'.");
            }
    }

    private bool IsEventOptionLegal(RunEventOptionDefinition option, RunState state)
    {
        if (option.Requirement is { } requirement && !IsRunConditionMet(requirement, state)) return false;
        long gold = state.Gold;
        foreach (var effect in option.Effects)
        {
            if (effect is GainRunGoldEffect gain) gold += gain.Amount;
            if (effect is LoseRunGoldEffect loss) gold -= loss.Amount;
            if (gold is < 0 or > int.MaxValue) return false;
            if (effect is ChooseRunCardEffect chooseCard
                && GetRunCardCandidates(state, chooseCard).Count == 0 && !chooseCard.CanSkip)
                return false;
        }
        return true;
    }

    private static bool IsRunConditionMet(RunCondition condition, RunState state) => condition switch
    {
        RunMetricCondition metric => ConditionEvaluation.Compare(ReadRunMetric(metric.Metric, state),
            metric.Comparison, metric.Threshold),
        HasRunRelicCondition hasRelic => state.Relics.Contains(hasRelic.Relic),
        HasRunPotionCondition hasPotion => state.Potions.Contains(hasPotion.Potion),
        AllRunCondition all => all.Conditions.All(nested => IsRunConditionMet(nested, state)),
        AnyRunCondition any => any.Conditions.Any(nested => IsRunConditionMet(nested, state)),
        NotRunCondition not => !IsRunConditionMet(not.Condition, state),
        _ => throw new InvalidOperationException($"Unsupported run condition '{condition.GetType().Name}'.")
    };

    private static int ReadRunMetric(RunMetric metric, RunState state) => metric switch
    {
        RunMetric.Gold => state.Gold,
        RunMetric.Hp => state.Hp,
        RunMetric.MaxHp => state.MaxHp,
        RunMetric.DeckSize => state.Deck.Count,
        RunMetric.PotionCount => state.Potions.Count(potion => potion.HasValue),
        RunMetric.EmptyPotionSlots => state.Potions.Count(potion => potion is null),
        _ => throw new ArgumentOutOfRangeException(nameof(metric))
    };

    private IReadOnlyList<CardInstance> GetRunCardCandidates(RunState state, ChooseRunCardEffect selection) =>
        Array.AsReadOnly(state.Deck.Where(card => selection.Filter switch
        {
            RunCardFilter.Any => true,
            RunCardFilter.Upgradeable => card.UpgradeLevel == 0 && _cards is not null
                && _cards.TryGetValue(card.CardId, out var definition)
                && definition.HasUpgrade,
            _ => false
        }).ToArray());

    /// <summary>Push a nested room, such as a combat started by an event, while preserving its parent room.</summary>
    public RunObservation PushSubRoom(RunSession session, RunRoomType roomType)
    {
        ArgumentNullException.ThrowIfNull(session);
        var state = session.State;
        if (state.Status != RunStatus.InProgress || state.Phase is not (RunPhase.ResolveRoom or RunPhase.ResolveEvent or RunPhase.ResolveFinalEvent)
            || state.RoomStack.Count == 0 || state.ActiveCombat is not null)
            throw new InvalidOperationException("A sub-room can only be entered from an active room.");
        if (roomType == RunRoomType.Unassigned)
            throw new ArgumentOutOfRangeException(nameof(roomType));

        var frame = state.CreateRoomFrame(roomType, null, state.SelectEvent(roomType));
        state.RoomStack.Add(frame);
        state.Phase = frame.EventOptions.Count > 0 ? RunPhase.ResolveEvent : RunPhase.ResolveRoom;
        if (state.RoomStack.Any(frame => frame.MapPointId is not null) && state.VisitHistory.Count > 0)
        {
            var currentVisit = state.VisitHistory[^1];
            state.VisitHistory[^1] = currentVisit with
            {
                RoomTypes = Array.AsReadOnly(currentVisit.RoomTypes.Append(roomType).ToArray())
            };
        }
        return Observe(session);
    }

    /// <summary>Copies a finished run-owned combat result back into the active room and run state.</summary>
    public RunObservation CompleteCombat(RunSession session, GameSession combatSession)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(combatSession);
        var state = session.State;
        if (!ReferenceEquals(state.ActiveCombat, combatSession)
            || !ReferenceEquals(combatSession.RunOwner, session))
            throw new InvalidOperationException("The combat session does not belong to this active run.");
        if (!combatSession.State.IsTerminal)
            throw new InvalidOperationException("Combat must reach a terminal outcome before it can update the run.");

        var combat = combatSession.State;
        var outcome = combat.Player.Creature.Hp <= 0 ? RunOutcome.PlayerDefeated : RunOutcome.RoomCleared;
        var result = new RunRoomResult(outcome, combat.Player.Creature.Hp, combat.Player.Creature.MaxHp,
            state.Gold, state.Deck.Select(card => card.CardId), state.Relics, state.Potions, state.Deck);
        var observation = CompleteRoomCore(session, result, generateCombatRewards: outcome == RunOutcome.RoomCleared);
        state.ActiveCombat = null;
        combatSession.RunOwner = null;
        return observation;
    }

    /// <summary>Called by the active room environment after its rules reach an outcome.</summary>
    public RunObservation CompleteRoom(RunSession session, RunRoomResult result)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(result);
        var state = session.State;
        if (state.ActiveCombat is not null)
            throw new InvalidOperationException("A run-owned combat must be completed through CompleteCombat.");
        return CompleteRoomCore(session, result, generateCombatRewards: false);
    }

    private RunObservation CompleteRoomCore(RunSession session, RunRoomResult result, bool generateCombatRewards)
    {
        var state = session.State;
        if (state.RoomStack.Count == 0)
            throw new InvalidOperationException("There is no active room to complete.");

        var completedFrame = state.RoomStack[^1];
        if (completedFrame.IsFinalEvent)
        {
            if (state.Phase != RunPhase.ResolveFinalEvent || state.Status != RunStatus.InProgress)
                throw new InvalidOperationException("The final event is not the active room.");
            if (completedFrame.EventOptions.Count > 0)
                throw new InvalidOperationException("A configured final event must be completed through a legal option.");
            if (result.Outcome != RunOutcome.RunWon)
                throw new InvalidOperationException("The final event must report the run victory outcome.");
            ApplyRoomResult(state, result);
            state.Status = RunStatus.Won;
            state.Phase = RunPhase.Terminal;
            return Observe(session);
        }

        if (state.Phase != RunPhase.ResolveRoom)
            throw new InvalidOperationException("There is no active room to complete.");

        if (result.Outcome == RunOutcome.RunWon)
            throw new InvalidOperationException("RunWon is valid only after the final event.");
        var completedRoomType = completedFrame.RoomType;
        ApplyRoomResult(state, result);
        if (result.Outcome == RunOutcome.PlayerDefeated)
        {
            state.Status = RunStatus.Lost;
            state.Phase = RunPhase.Terminal;
            return Observe(session);
        }
        if (completedRoomType is RunRoomType.Monster or RunRoomType.Elite or RunRoomType.Boss)
        {
            foreach (var hook in _runHooks)
                hook.AfterCombatVictory(new RunHookContext(state, hook.Id));
        }

        if (generateCombatRewards && TryGenerateCombatRewards(state, completedFrame, Observe(session)))
        {
            state.Phase = RunPhase.ResolveRewards;
            return Observe(session);
        }

        return FinishCompletedRoom(session, completedFrame);
    }

    private bool TryGenerateCombatRewards(RunState state, RunRoomFrame frame, RunObservation observation)
    {
        if (frame.RoomType is not (RunRoomType.Monster or RunRoomType.Elite or RunRoomType.Boss)
            || frame.EncounterId is not { } encounterId
            || state.Encounters is null || !state.Encounters.TryGetValue(encounterId, out var encounter)
            || !encounter.ShouldGiveRewards)
            return false;

        if (frame.RoomType == RunRoomType.Boss && state.ActIndex >= state.Definition.Acts.Count - 1)
            return true;

        var shouldGivePotion = GetPotionOptions(state).Any()
            && RollForPotionReward(state, frame.RoomType, observation);
        var baseAmount = state.RewardRandom.NextInt(encounter.MinGoldReward, encounter.MaxGoldReward + 1);
        var combat = state.ActiveCombat?.State;
        var goldProportion = combat?.GoldRewardPercent is { } configuredPercent
            ? configuredPercent / 100m
            : combat is null || combat.Enemies.Count == 0 ? 1m
                : (combat.Enemies.Count - combat.Enemies.Count(enemy => enemy.IsEscaped))
                    / (decimal)combat.Enemies.Count;
        var amount = (int)Math.Round(baseAmount * goldProportion);
        if (goldProportion > 0m)
            frame.Rewards.Add(new RunRewardState($"{encounterId}:gold", RunRewardKind.Gold, amount));
        if (shouldGivePotion && RollPotion(state) is { } potionId)
            frame.Rewards.Add(new RunRewardState($"{encounterId}:potion", RunRewardKind.Potion,
                0, PotionId: potionId));
        if (_cards is not null && _cardPools is not null && state.Character.CardPools.Count > 0)
        {
            var options = CardRewardGenerator.Generate(state, _cards, _cardPools, _runHooks, observation);
            if (options.Count > 0)
                frame.Rewards.Add(new RunRewardState($"{encounterId}:cards", RunRewardKind.Card, 0, options));
        }
        if (frame.RoomType == RunRoomType.Elite
            && state.TakeRelic(RollRelicRarity(state.RewardRandom), RunRelicPool.Player) is { } relicId)
            frame.Rewards.Add(new RunRewardState($"{encounterId}:relic", RunRewardKind.Relic, 0,
                RelicId: relicId));
        return true;
    }

    private bool RollForPotionReward(RunState state, RunRoomType roomType, RunObservation observation)
    {
        var forced = _runHooks.Any(hook => hook.ShouldForcePotionReward(observation, roomType));
        var bonus = roomType == RunRoomType.Elite ? 0.125f : 0f;
        var roll = state.RewardRandom.NextFloat();
        if (forced || roll < state.PotionRewardChance + bonus)
        {
            state.PotionRewardChance -= 0.1f;
            return true;
        }
        state.PotionRewardChance += 0.1f;
        return false;
    }

    private ContentId? RollPotion(RunState state)
    {
        var random = state.RewardRandom;
        var rarityRoll = random.NextFloat();
        var rarity = rarityRoll <= 0.1f ? PotionRarity.Rare
            : rarityRoll <= 0.35f ? PotionRarity.Uncommon : PotionRarity.Common;
        var pool = GetPotionOptions(state).ToArray();
        var potions = pool.Where(potion => potion.Rarity == rarity)
            .OrderBy(potion => potion.Id.Value, StringComparer.Ordinal).ToArray();
        if (potions.Length == 0)
            potions = pool.OrderBy(potion => potion.Id.Value, StringComparer.Ordinal).ToArray();
        if (potions.Length == 0) return null;
        return potions[random.NextInt(potions.Length)].Id;
    }

    private IEnumerable<PotionDefinition> GetPotionOptions(RunState state) =>
        (_potionDefinitions?.Values ?? Array.Empty<PotionDefinition>())
        .Where(potion => potion.InSharedPool || potion.CharacterPoolIds?.Contains(state.Character.Id) == true);

    private static RunRelicRarity RollRelicRarity(SeededRandom random)
    {
        var roll = random.NextFloat();
        return roll < 0.5f ? RunRelicRarity.Common
            : roll < 0.83f ? RunRelicRarity.Uncommon : RunRelicRarity.Rare;
    }

    private RunObservation ResolveRewardAction(RunSession session, PlayerAction action)
    {
        var state = session.State;
        if (state.RoomStack.Count == 0) throw new InvalidOperationException("There is no active reward room.");
        var frame = state.RoomStack[^1];
        if (action is ClaimGoldRewardAction claim)
        {
            var reward = frame.Rewards.SingleOrDefault(item => item.Id == claim.RewardId && item.Kind == RunRewardKind.Gold)
                ?? throw new InvalidOperationException($"Gold reward '{claim.RewardId}' is not available.");
            state.Gold = checked(state.Gold + reward.Amount);
            frame.Rewards.Remove(reward);
            return Observe(session);
        }
        if (action is ClaimCardRewardAction claimCard)
        {
            var reward = frame.Rewards.SingleOrDefault(item =>
                item.Id == claimCard.RewardId && item.Kind == RunRewardKind.Card)
                ?? throw new InvalidOperationException($"Card reward '{claimCard.RewardId}' is not available.");
            var card = reward.CardOptions?.SingleOrDefault(option =>
                option.InstanceId == claimCard.CardInstanceId)
                ?? throw new InvalidOperationException($"Card option '{claimCard.CardInstanceId}' is not available.");
            state.Deck.Add(card);
            frame.Rewards.Remove(reward);
            return Observe(session);
        }
        if (action is ClaimPotionRewardAction claimPotion)
        {
            var reward = frame.Rewards.SingleOrDefault(item =>
                item.Id == claimPotion.RewardId && item.Kind == RunRewardKind.Potion)
                ?? throw new InvalidOperationException($"Potion reward '{claimPotion.RewardId}' is not available.");
            var potionId = reward.PotionId
                ?? throw new InvalidOperationException($"Potion reward '{claimPotion.RewardId}' has no potion.");
            if (!state.TryAddPotion(potionId))
                throw new InvalidOperationException("The player's potion inventory is full.");
            frame.Rewards.Remove(reward);
            return Observe(session);
        }
        if (action is ClaimRelicRewardAction claimRelic)
        {
            var reward = frame.Rewards.SingleOrDefault(item =>
                item.Id == claimRelic.RewardId && item.Kind == RunRewardKind.Relic)
                ?? throw new InvalidOperationException($"Relic reward '{claimRelic.RewardId}' is not available.");
            var relicId = reward.RelicId
                ?? throw new InvalidOperationException($"Relic reward '{claimRelic.RewardId}' has no relic.");
            var isStackable = _relicDefinitions?.TryGetValue(relicId, out var relicDefinition) == true
                && relicDefinition.IsStackable;
            if (state.Relics.Contains(relicId) && !isStackable)
                throw new InvalidOperationException($"Relic '{relicId}' is already owned.");
            state.Relics.Add(relicId);
            if (!isStackable) state.RemoveRelicFromBags(relicId);
            frame.Rewards.Remove(reward);
            foreach (var hook in _runHooks)
                hook.AfterRelicObtained(new RunHookContext(state, hook.Id), relicId);
            return Observe(session);
        }
        if (action is SkipRewardSetAction && frame.Rewards.Count > 0)
        {
            frame.Rewards.Clear();
            return Observe(session);
        }
        if (action is ProceedAfterRewardsAction && frame.Rewards.Count == 0)
            return FinishCompletedRoom(session, frame);
        throw new InvalidOperationException("The selected reward action is not currently legal.");
    }

    private RunObservation FinishCompletedRoom(RunSession session, RunRoomFrame completedFrame)
    {
        var state = session.State;
        state.RoomStack.RemoveAt(state.RoomStack.Count - 1);
        if (state.RoomStack.Count > 0)
        {
            var parentFrame = state.RoomStack[^1];
            if (!completedFrame.ResumeParentAfter && parentFrame.EventId is not null && !parentFrame.IsFinalEvent)
                return FinishCompletedRoom(session, parentFrame);
            state.Phase = parentFrame.IsFinalEvent ? RunPhase.ResolveFinalEvent
                : parentFrame.EventId is not null ? RunPhase.ResolveEvent : RunPhase.ResolveRoom;
            return Observe(session);
        }

        if (completedFrame.MapPointId == state.Map.BossPointId)
        {
            state.ActIndex++;
            if (state.ActIndex == state.Definition.Acts.Count)
            {
                var finalEventId = state.Definition.FinalEventId;
                var finalEventOptions = state.GetEventOptions(finalEventId);
                state.RoomStack.Add(new RunRoomFrame(RunRoomType.Event, null, true, null,
                    finalEventId, state.GetInitialEventPageId(finalEventId), [], finalEventOptions));
                state.Phase = RunPhase.ResolveFinalEvent;
                return Observe(session);
            }

            state.Map = state.GenerateMap(state.ActIndex);
            state.MapView = RunState.BuildMapView(state.Map);
            state.UnknownRoomOdds?.ResetToBase();
            state.EnterMapStart();
        }
        else
        {
            state.CurrentMapPointType = null;
            state.Phase = RunPhase.ChooseMapPoint;
        }
        return Observe(session);
    }

    private static void ApplyRoomResult(RunState state, RunRoomResult result)
    {
        if (result.MaxHp <= 0 || result.Hp < 0 || result.Hp > result.MaxHp)
            throw new ArgumentOutOfRangeException(nameof(result), "Room result health must be within the positive maximum HP.");
        if (result.Gold < 0) throw new ArgumentOutOfRangeException(nameof(result), "Room result gold cannot be negative.");
        if (result.Outcome == RunOutcome.PlayerDefeated && result.Hp != 0)
            throw new ArgumentException("A defeated player must have zero HP.", nameof(result));
        if (result.Outcome != RunOutcome.PlayerDefeated && result.Hp == 0)
            throw new ArgumentException("A surviving room result must have positive HP.", nameof(result));

        state.Hp = result.Hp;
        state.MaxHp = result.MaxHp;
        state.Gold = result.Gold;
        state.Deck.Clear();
        state.Deck.AddRange(result.DeckInstances);
        state.Relics.Clear();
        state.Relics.AddRange(result.Relics);
        state.ReplacePotions(result.Potions);
    }

    private static HashSet<RunRoomType> BuildRoomTypeBlacklist(RunState state, MapPointDefinition point)
    {
        var blacklist = new HashSet<RunRoomType>();
        if ((state.VisitHistory.Count > 0 && state.VisitHistory[^1].RoomTypes.Contains(RunRoomType.Shop))
            || (point.Children.Count > 0 && point.Children.All(childId => state.Map.Points[childId].Type == MapPointType.Shop)))
            blacklist.Add(RunRoomType.Shop);
        return blacklist;
    }

    private RunRoomType ResolveRoomType(RunState state, MapPointDefinition point, IReadOnlySet<RunRoomType> blacklist) => point.Type switch
    {
        MapPointType.Unknown => ResolveUnknownRoom(state, blacklist),
        MapPointType.Shop => RunRoomType.Shop,
        MapPointType.Treasure => RunRoomType.Treasure,
        MapPointType.RestSite => RunRoomType.RestSite,
        MapPointType.Monster => RunRoomType.Monster,
        MapPointType.Elite => RunRoomType.Elite,
        MapPointType.Boss => RunRoomType.Boss,
        MapPointType.Ancient => RunRoomType.Event,
        MapPointType.Start => throw new InvalidOperationException("The map entry point is not a room."),
        _ => throw new ArgumentOutOfRangeException(nameof(point), point.Type, null)
    };

    private RunRoomType ResolveUnknownRoom(RunState state, IReadOnlySet<RunRoomType> blacklist)
    {
        var act = state.Definition.Acts[state.ActIndex];
        if (act.ResolveUnknownRoom is not null)
            return act.ResolveUnknownRoom(state.Random.Get($"act_{state.ActIndex + 1}_unknown-room"), blacklist);
        if (state.UnknownRoomOdds is not null)
            return state.UnknownRoomOdds.Roll(state.Random.Get("unknown_map_point"), blacklist,
                _runHooks, ObserveState(state));
        throw new InvalidOperationException("Unknown map points need configured run odds or an act room resolver.");
    }

    private static RunDefinition GetRun(GameDefinition gameDefinition, ContentId id)
    {
        ArgumentNullException.ThrowIfNull(gameDefinition);
        return gameDefinition.Runs.TryGetValue(id, out var run) ? run : throw new KeyNotFoundException($"Unknown run '{id}'.");
    }

    private static CharacterDefinition GetCharacter(GameDefinition gameDefinition, ContentId id)
    {
        ArgumentNullException.ThrowIfNull(gameDefinition);
        return gameDefinition.Characters.TryGetValue(id, out var character) ? character : throw new KeyNotFoundException($"Unknown character '{id}'.");
    }

}
