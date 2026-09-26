namespace CoreTheSpire;

public enum MapPointType { Start, Unknown, Shop, Treasure, RestSite, Monster, Elite, Boss, Ancient }
public enum RunRoomType { Monster, Elite, Boss, Treasure, Shop, Event, RestSite, Map }
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

        if (!pointMap.TryGetValue(startPointId, out var start) || start.Type != MapPointType.Start)
            throw new ArgumentException("The start point must exist and have type Start.", nameof(startPointId));
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
    Func<SeededRandom, RunRoomType>? ResolveUnknownRoom = null);

public sealed class CharacterDefinition
{
    public CharacterDefinition(ContentId id, int maxHp, int startingGold,
        IEnumerable<ContentId> startingDeck, IEnumerable<ContentId>? startingRelics = null)
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
    }

    public ContentId Id { get; }
    public int MaxHp { get; }
    public int StartingGold { get; }
    public IReadOnlyList<ContentId> StartingDeck { get; }
    public IReadOnlyList<ContentId> StartingRelics { get; }
}

public sealed class RunDefinition
{
    public RunDefinition(ContentId id, IEnumerable<ActDefinition> acts)
    {
        id.Validate();
        ArgumentNullException.ThrowIfNull(acts);
        Id = id;
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
}

public sealed record ChooseMapPointAction(string PointId) : PlayerAction;

public sealed record MapPointView(string Id, int Floor, int Column, MapPointType Type,
    IReadOnlyList<string> Children);

public sealed record RunObservation(ContentId Character, int Hp, int MaxHp, int Gold,
    IReadOnlyList<ContentId> Deck, IReadOnlyList<ContentId> Relics, int ActIndex, string CurrentPointId, MapPointType? CurrentMapPointType,
    RunRoomType? CurrentRoom,
    RunPhase Phase, RunStatus Status, IReadOnlyList<MapPointView> Map);

internal sealed class RunState
{
    public RunState(RunDefinition definition, CharacterDefinition character, RandomStreams random)
    {
        Definition = definition;
        Character = character;
        Random = random;
        Hp = character.MaxHp;
        Gold = character.StartingGold;
        Deck = [.. character.StartingDeck];
        Relics = [.. character.StartingRelics];
        Map = GenerateMap(0);
        MapView = BuildMapView(Map);
        CurrentPointId = Map.StartPointId;
    }

    public RunDefinition Definition { get; }
    public CharacterDefinition Character { get; }
    public RandomStreams Random { get; }
    public int Hp { get; set; }
    public int Gold { get; set; }
    public List<ContentId> Deck { get; }
    public List<ContentId> Relics { get; }
    public int ActIndex { get; set; }
    public ActMapDefinition Map { get; set; }
    public IReadOnlyList<MapPointView> MapView { get; set; }
    public string CurrentPointId { get; set; }
    public MapPointType? CurrentMapPointType { get; set; }
    public RunRoomType? CurrentRoom { get; set; }
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

/// <summary>Single-player run lifecycle and map-choice layer. Room rules are resolved by room-specific environments.</summary>
public sealed class RunEnvironment(RunDefinition definition, CharacterDefinition character)
{
    public RunEnvironment(GameDefinition gameDefinition, ContentId runId, ContentId characterId)
        : this(GetRun(gameDefinition, runId), GetCharacter(gameDefinition, characterId))
    {
    }

    public RunSession Start(uint seed)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(character);
        ArgumentOutOfRangeException.ThrowIfNegative(character.StartingGold);
        if (character.MaxHp <= 0) throw new ArgumentOutOfRangeException(nameof(character));
        return new RunSession(new RunState(definition, character, new RandomStreams(seed)));
    }

    public RunObservation Observe(RunSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var state = session.State;
        return new RunObservation(state.Character.Id, state.Hp, state.Character.MaxHp, state.Gold,
            Array.AsReadOnly(state.Deck.ToArray()), Array.AsReadOnly(state.Relics.ToArray()), state.ActIndex,
            state.CurrentPointId, state.CurrentMapPointType, state.CurrentRoom,
            state.Phase, state.Status, state.MapView);
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
        state.CurrentRoom = ResolveRoomType(state, point.Type);
        state.Phase = RunPhase.ResolveRoom;
        return Observe(session);
    }

    /// <summary>Called by the active room environment after its rules reach an outcome.</summary>
    public RunObservation CompleteRoom(RunSession session, RunOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(session);
        var state = session.State;
        if (state.Phase == RunPhase.ResolveFinalEvent)
        {
            if (outcome != RunOutcome.RunWon)
                throw new InvalidOperationException("The final event must report the run victory outcome.");
            state.Status = RunStatus.Won;
            state.Phase = RunPhase.Terminal;
            state.CurrentRoom = null;
            return Observe(session);
        }

        if (state.Phase != RunPhase.ResolveRoom || state.CurrentRoom is null)
            throw new InvalidOperationException("There is no active room to complete.");

        if (outcome == RunOutcome.PlayerDefeated)
        {
            state.Hp = 0;
            state.Status = RunStatus.Lost;
            state.Phase = RunPhase.Terminal;
            return Observe(session);
        }
        if (outcome != RunOutcome.RoomCleared)
            throw new InvalidOperationException("RunWon is valid only after the final event.");

        if (state.CurrentPointId == state.Map.BossPointId)
        {
            state.ActIndex++;
            if (state.ActIndex == state.Definition.Acts.Count)
            {
                state.CurrentRoom = RunRoomType.Event;
                state.Phase = RunPhase.ResolveFinalEvent;
                return Observe(session);
            }

            state.Map = state.GenerateMap(state.ActIndex);
            state.MapView = RunState.BuildMapView(state.Map);
            state.CurrentPointId = state.Map.StartPointId;
            state.CurrentMapPointType = null;
            state.CurrentRoom = null;
            state.CurrentMapPointType = null;
            state.Phase = RunPhase.ChooseMapPoint;
        }
        else
        {
            state.CurrentRoom = null;
            state.CurrentMapPointType = null;
            state.Phase = RunPhase.ChooseMapPoint;
        }
        return Observe(session);
    }

    private static RunRoomType ResolveRoomType(RunState state, MapPointType pointType) => pointType switch
    {
        MapPointType.Unknown => state.Definition.Acts[state.ActIndex].ResolveUnknownRoom?.Invoke(
            state.Random.Get($"act_{state.ActIndex + 1}_unknown-room"))
            ?? throw new InvalidOperationException("Unknown map points require an act room resolver."),
        MapPointType.Shop => RunRoomType.Shop,
        MapPointType.Treasure => RunRoomType.Treasure,
        MapPointType.RestSite => RunRoomType.RestSite,
        MapPointType.Monster => RunRoomType.Monster,
        MapPointType.Elite => RunRoomType.Elite,
        MapPointType.Boss => RunRoomType.Boss,
        MapPointType.Ancient => RunRoomType.Event,
        MapPointType.Start => throw new InvalidOperationException("The map entry point is not a room."),
        _ => throw new ArgumentOutOfRangeException(nameof(pointType), pointType, null)
    };

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
