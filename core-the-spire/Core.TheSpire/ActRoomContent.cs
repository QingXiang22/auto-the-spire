namespace CoreTheSpire;

/// <summary>Per-run selections generated from an act's room pools in source call order.</summary>
internal sealed class ActRoomContentState
{
    private readonly IReadOnlyList<ContentId> _events;
    private readonly ActEncounterState? _encounters;
    private int _eventIndex;

    private ActRoomContentState(IReadOnlyList<ContentId> events, ActEncounterState? encounters,
        ContentId? ancientEventId)
    {
        _events = events;
        _encounters = encounters;
        AncientEventId = ancientEventId;
    }

    public ContentId? AncientEventId { get; }

    public ContentId? NextEvent(ISet<ContentId> visitedEvents)
    {
        if (_events.Count == 0) return null;
        var startingIndex = _eventIndex;
        for (var i = 0; i < _events.Count; i++)
        {
            var candidate = _events[_eventIndex % _events.Count];
            _eventIndex++;
            if (visitedEvents.Add(candidate)) return candidate;
        }

        _eventIndex = startingIndex + 1;
        return _events[startingIndex % _events.Count];
    }

    public ContentId? NextEncounter(RunRoomType roomType) => _encounters?.Next(roomType);

    public static IReadOnlyList<ActRoomContentState?> Generate(RunDefinition run, SeededRandom random,
        IReadOnlyDictionary<ContentId, EncounterDefinition>? encounters,
        IReadOnlyDictionary<ContentId, RunEventDefinition>? runEvents)
    {
        if ((run.SharedEventIds.Count > 0 || run.SharedAncientIds.Count > 0 ||
            run.Acts.Any(act => act.EventIds.Count > 0 || act.AncientIds.Count > 0)) && runEvents is null)
            throw new InvalidOperationException("Run event pools require a run environment created from a built GameDefinition.");

        var sharedAncients = run.SharedAncientIds.ToList();
        random.Shuffle(sharedAncients);
        var sharedByAct = Enumerable.Range(0, run.Acts.Count)
            .Select(_ => new List<ContentId>()).ToArray();
        for (var actIndex = 1; actIndex < run.Acts.Count; actIndex++)
        {
            var count = random.NextInt(sharedAncients.Count + 1);
            sharedByAct[actIndex].AddRange(sharedAncients.Take(count));
            sharedAncients.RemoveRange(0, count);
        }

        var states = new ActRoomContentState?[run.Acts.Count];
        for (var actIndex = 0; actIndex < run.Acts.Count; actIndex++)
        {
            var act = run.Acts[actIndex];
            var events = act.EventIds.Concat(run.SharedEventIds).ToList();
            random.Shuffle(events);

            ActEncounterState? encounterState = null;
            if (act.EncounterPool is { } pool)
            {
                encounterState = pool.Generate(random, encounters ?? throw new InvalidOperationException(
                    "Encounter pools require a run environment created from a built GameDefinition."));
            }

            var ancientPool = act.AncientIds.Concat(sharedByAct[actIndex]).ToArray();
            ContentId? ancientId = ancientPool.Length == 0
                ? null
                : ancientPool[random.NextInt(ancientPool.Length)];
            if (events.Count != 0 || encounterState is not null || ancientId is not null)
                states[actIndex] = new ActRoomContentState(Array.AsReadOnly(events.ToArray()), encounterState, ancientId);
        }

        return Array.AsReadOnly(states);
    }
}
