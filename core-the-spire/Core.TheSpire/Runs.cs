namespace CoreTheSpire;

public enum MapPointType { Unassigned, Unknown, Shop, Treasure, RestSite, Monster, Elite, Boss, Ancient, Start }
public enum RunRoomType { Unassigned, Monster, Elite, Boss, Treasure, Shop, Event, RestSite, Map }
public enum RunPhase { ChooseMapPoint, ResolveRoom, ResolveFinalEvent, Terminal }
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

public sealed record ActDefinition(ContentId Id, Func<SeededRandom, ActMapDefinition> GenerateMap,
    Func<SeededRandom, IReadOnlySet<RunRoomType>, RunRoomType>? ResolveUnknownRoom = null,
    ActEncounterPoolDefinition? EncounterPool = null);

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
        IEnumerable<ContentId>? startingPotions = null)
    {
        id.Validate();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxHp);
        ArgumentOutOfRangeException.ThrowIfNegative(startingGold);
        ArgumentNullException.ThrowIfNull(startingDeck);
        Id = id;
        MaxHp = maxHp;
        StartingGold = startingGold;
        StartingDeck = Array.AsReadOnly(startingDeck.ToArray());
        StartingRelics = Array.AsReadOnly(startingRelics?.ToArray() ?? []);
        StartingPotions = Array.AsReadOnly(startingPotions?.ToArray() ?? []);
    }

    public ContentId Id { get; }
    public int MaxHp { get; }
    public int StartingGold { get; }
    public IReadOnlyList<ContentId> StartingDeck { get; }
    public IReadOnlyList<ContentId> StartingRelics { get; }
    public IReadOnlyList<ContentId> StartingPotions { get; }
}

public sealed class RunDefinition
{
    public RunDefinition(ContentId id, IEnumerable<ActDefinition> acts, UnknownRoomOddsDefinition? unknownRoomOdds = null)
    {
        id.Validate();
        ArgumentNullException.ThrowIfNull(acts);
        Id = id;
        UnknownRoomOdds = unknownRoomOdds;
        Acts = Array.AsReadOnly(acts.ToArray());
        if (Acts.Count == 0) throw new ArgumentException("A run needs at least one act.", nameof(acts));
        foreach (var act in Acts)
        {
            act.Id.Validate();
            ArgumentNullException.ThrowIfNull(act.GenerateMap);
        }
        if (Acts.Select(act => act.Id).Distinct().Count() != Acts.Count)
            throw new ArgumentException("Act IDs must be unique within a run.", nameof(acts));
    }

    public ContentId Id { get; }
    public IReadOnlyList<ActDefinition> Acts { get; }
    public UnknownRoomOddsDefinition? UnknownRoomOdds { get; }
}

public sealed record ChooseMapPointAction(string PointId) : PlayerAction;

public sealed record MapPointView(string Id, int Floor, int Column, MapPointType Type,
    IReadOnlyList<string> Children);

public sealed record RunMapVisitView(int ActIndex, string PointId, MapPointType MapPointType,
    IReadOnlyList<RunRoomType> RoomTypes);

public sealed record RunRoomFrameView(RunRoomType RoomType, string? MapPointId, bool IsFinalEvent,
    ContentId? EncounterId);

public sealed record RunObservation(ContentId Character, int Hp, int MaxHp, int Gold,
    IReadOnlyList<ContentId> Deck, IReadOnlyList<ContentId> Relics, IReadOnlyList<ContentId> Potions,
    int ActIndex, string CurrentPointId, MapPointType? CurrentMapPointType,
    RunRoomType? CurrentRoom,
    RunPhase Phase, RunStatus Status, IReadOnlyList<MapPointView> Map, IReadOnlyList<RunMapVisitView> VisitedMapPoints,
    IReadOnlyList<RunRoomFrameView> RoomStack);

public sealed class RunRoomResult
{
    public RunRoomResult(RunOutcome outcome, int hp, int maxHp, int gold,
        IEnumerable<ContentId> deck, IEnumerable<ContentId> relics, IEnumerable<ContentId>? potions = null)
    {
        ArgumentNullException.ThrowIfNull(deck);
        ArgumentNullException.ThrowIfNull(relics);
        Outcome = outcome;
        Hp = hp;
        MaxHp = maxHp;
        Gold = gold;
        Deck = Array.AsReadOnly(deck.ToArray());
        Relics = Array.AsReadOnly(relics.ToArray());
        Potions = Array.AsReadOnly(potions?.ToArray() ?? []);
    }

    public RunOutcome Outcome { get; }
    public int Hp { get; }
    public int MaxHp { get; }
    public int Gold { get; }
    public IReadOnlyList<ContentId> Deck { get; }
    public IReadOnlyList<ContentId> Relics { get; }
    public IReadOnlyList<ContentId> Potions { get; }
}

public interface IRunHook
{
    ContentId Id { get; }
    void AfterCombatVictory(RunHookContext context);
    IReadOnlySet<RunRoomType> ModifyUnknownRoomTypes(RunObservation observation,
        IReadOnlySet<RunRoomType> roomTypes) => roomTypes;
    float ModifyUnknownOddsIncrease(RunObservation observation, RunRoomType roomType, float increase) => increase;
}

public sealed class RunHookContext
{
    private readonly RunState _state;

    internal RunHookContext(RunState state, ContentId hookId)
    {
        _state = state;
        HookId = hookId;
    }

    public ContentId HookId { get; }
    public int Hp => _state.Hp;
    public int MaxHp => _state.MaxHp;
    public bool HasRelic(ContentId relic) => _state.Relics.Contains(relic);

    public void Heal(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        _state.Hp = Math.Min(_state.MaxHp, _state.Hp + amount);
    }
}

internal sealed class RunState
{
    public RunState(RunDefinition definition, CharacterDefinition character, RandomStreams random,
        IReadOnlyDictionary<ContentId, EncounterDefinition>? encounters = null)
    {
        Definition = definition;
        Character = character;
        Random = random;
        Hp = character.MaxHp;
        MaxHp = character.MaxHp;
        Gold = character.StartingGold;
        Deck = [.. character.StartingDeck];
        Relics = [.. character.StartingRelics];
        Potions = [.. character.StartingPotions];
        UnknownRoomOdds = definition.UnknownRoomOdds is null ? null : new UnknownRoomOddsState(definition.UnknownRoomOdds);
        EncounterStates = definition.Acts.Select(act => act.EncounterPool is { } pool
            ? pool.Generate(Random.Get("up_front"), encounters ?? throw new InvalidOperationException(
                "Encounter pools require a run environment created from a built GameDefinition."))
            : null).ToArray();
        Map = GenerateMap(0);
        MapView = BuildMapView(Map);
        EnterMapStart();
    }

    public void EnterMapStart()
    {
        RoomStack.Clear();
        CurrentPointId = Map.StartPointId;
        var entryPoint = Map.Points[CurrentPointId];
        if (entryPoint.Type == MapPointType.Ancient)
        {
            CurrentMapPointType = MapPointType.Ancient;
            RoomStack.Add(new RunRoomFrame(RunRoomType.Event, entryPoint.Id, false, null));
            Phase = RunPhase.ResolveRoom;
            VisitHistory.Add(new RunMapVisitView(ActIndex, entryPoint.Id, entryPoint.Type,
                Array.AsReadOnly(new[] { RunRoomType.Event })));
        }
        else
        {
            CurrentMapPointType = null;
            Phase = RunPhase.ChooseMapPoint;
        }
    }

    public RunDefinition Definition { get; }
    public CharacterDefinition Character { get; }
    public RandomStreams Random { get; }
    public UnknownRoomOddsState? UnknownRoomOdds { get; }
    public IReadOnlyList<ActEncounterState?> EncounterStates { get; }
    public int Hp { get; set; }
    public int MaxHp { get; set; }
    public int Gold { get; set; }
    public List<ContentId> Deck { get; }
    public List<ContentId> Relics { get; }
    public List<ContentId> Potions { get; }
    public int ActIndex { get; set; }
    public ActMapDefinition Map { get; set; }
    public IReadOnlyList<MapPointView> MapView { get; set; }
    public List<RunMapVisitView> VisitHistory { get; } = [];
    public List<RunRoomFrame> RoomStack { get; } = [];
    public GameSession? ActiveCombat { get; set; }
    public RunRoomType? CurrentRoom => RoomStack.Count == 0 ? null : RoomStack[^1].RoomType;
    public ContentId? CurrentEncounterId => RoomStack.Count == 0 ? null : RoomStack[^1].EncounterId;
    public ContentId? SelectEncounter(RunRoomType roomType) => roomType is RunRoomType.Monster or RunRoomType.Elite or RunRoomType.Boss
        ? ActIndex < EncounterStates.Count ? EncounterStates[ActIndex]?.Next(roomType) : null
        : null;
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
    ContentId? EncounterId);

/// <summary>Single-player run lifecycle and map-choice layer. Room rules are resolved by room-specific environments.</summary>
public sealed class RunEnvironment
{
    private readonly RunDefinition _definition;
    private readonly CharacterDefinition _character;
    private readonly IReadOnlyList<IRunHook> _runHooks;
    private readonly IReadOnlyDictionary<ContentId, EncounterDefinition>? _encounters;

    public RunEnvironment(RunDefinition definition, CharacterDefinition character, IEnumerable<IRunHook>? runHooks = null)
    {
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _character = character ?? throw new ArgumentNullException(nameof(character));
        _runHooks = Array.AsReadOnly(runHooks?.ToArray() ?? []);
    }

    public RunEnvironment(GameDefinition gameDefinition, ContentId runId, ContentId characterId)
        : this(GetRun(gameDefinition, runId), GetCharacter(gameDefinition, characterId), gameDefinition.RunHooks)
    {
        _encounters = gameDefinition.Encounters;
    }

    public RunSession Start(uint seed)
    {
        return new RunSession(new RunState(_definition, _character, new RandomStreams(seed), _encounters));
    }

    public RunSession Start(string seed)
    {
        ArgumentNullException.ThrowIfNull(seed);
        return new RunSession(new RunState(_definition, _character, RandomStreams.FromStringSeed(seed), _encounters));
    }

    public RunObservation Observe(RunSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return ObserveState(session.State);
    }

    private static RunObservation ObserveState(RunState state)
    {
        return new RunObservation(state.Character.Id, state.Hp, state.MaxHp, state.Gold,
            Array.AsReadOnly(state.Deck.ToArray()), Array.AsReadOnly(state.Relics.ToArray()),
            Array.AsReadOnly(state.Potions.ToArray()), state.ActIndex,
            state.CurrentPointId, state.CurrentMapPointType, state.CurrentRoom,
            state.Phase, state.Status, state.MapView, Array.AsReadOnly(state.VisitHistory.ToArray()),
            Array.AsReadOnly(state.RoomStack.Select(frame => new RunRoomFrameView(
                frame.RoomType, frame.MapPointId, frame.IsFinalEvent, frame.EncounterId)).ToArray()));
    }

    public IReadOnlyList<PlayerAction> LegalActions(RunSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var state = session.State;
        if (state.Phase != RunPhase.ChooseMapPoint || state.Status != RunStatus.InProgress) return [];
        return state.Map.Points[state.CurrentPointId].Children
            .Select(id => (PlayerAction)new ChooseMapPointAction(id)).ToArray();
    }

    public RunObservation Step(RunSession session, PlayerAction action)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(action);
        var state = session.State;
        if (action is not ChooseMapPointAction choose || !LegalActions(session).Contains(choose))
            throw new InvalidOperationException("A reachable map point must be selected.");

        var point = state.Map.Points[choose.PointId];
        state.CurrentPointId = point.Id;
        state.CurrentMapPointType = point.Type;
        var blacklist = BuildRoomTypeBlacklist(state, point);
        var roomType = ResolveRoomType(state, point, blacklist);
        state.RoomStack.Add(new RunRoomFrame(roomType, point.Id, false, state.SelectEncounter(roomType)));
        state.VisitHistory.Add(new RunMapVisitView(state.ActIndex, point.Id, point.Type,
            Array.AsReadOnly(new[] { roomType })));
        state.Phase = RunPhase.ResolveRoom;
        return Observe(session);
    }

    /// <summary>Push a nested room, such as a combat started by an event, while preserving its parent room.</summary>
    public RunObservation PushSubRoom(RunSession session, RunRoomType roomType)
    {
        ArgumentNullException.ThrowIfNull(session);
        var state = session.State;
        if (state.Status != RunStatus.InProgress || state.Phase is not (RunPhase.ResolveRoom or RunPhase.ResolveFinalEvent)
            || state.RoomStack.Count == 0 || state.ActiveCombat is not null)
            throw new InvalidOperationException("A sub-room can only be entered from an active room.");
        if (roomType == RunRoomType.Unassigned)
            throw new ArgumentOutOfRangeException(nameof(roomType));

        state.RoomStack.Add(new RunRoomFrame(roomType, null, false, state.SelectEncounter(roomType)));
        state.Phase = RunPhase.ResolveRoom;
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
            state.Gold, state.Deck, state.Relics, state.Potions);
        var observation = CompleteRoomCore(session, result);
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
        return CompleteRoomCore(session, result);
    }

    private RunObservation CompleteRoomCore(RunSession session, RunRoomResult result)
    {
        var state = session.State;
        if (state.RoomStack.Count == 0)
            throw new InvalidOperationException("There is no active room to complete.");

        var completedFrame = state.RoomStack[^1];
        if (completedFrame.IsFinalEvent)
        {
            if (state.Phase != RunPhase.ResolveFinalEvent || state.Status != RunStatus.InProgress)
                throw new InvalidOperationException("The final event is not the active room.");
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

        state.RoomStack.RemoveAt(state.RoomStack.Count - 1);
        if (state.RoomStack.Count > 0)
        {
            state.Phase = state.RoomStack[^1].IsFinalEvent ? RunPhase.ResolveFinalEvent : RunPhase.ResolveRoom;
            return Observe(session);
        }

        if (completedFrame.MapPointId == state.Map.BossPointId)
        {
            state.ActIndex++;
            if (state.ActIndex == state.Definition.Acts.Count)
            {
                state.RoomStack.Add(new RunRoomFrame(RunRoomType.Event, null, true, null));
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
        state.Deck.AddRange(result.Deck);
        state.Relics.Clear();
        state.Relics.AddRange(result.Relics);
        state.Potions.Clear();
        state.Potions.AddRange(result.Potions);
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
