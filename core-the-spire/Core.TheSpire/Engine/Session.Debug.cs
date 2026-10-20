namespace CoreTheSpire.Engine;

public sealed record SessionDebugOptions(int MaxRecords = 10_000, bool IncludeInstructions = false,
    int MaxTextLength = 1024, int MaxCandidates = 256);

public enum SessionDebugKind
{
    CommandBegin, CommandEnd, ProcedureEnter, ProcedureExit, Instruction,
    EventEmit, ListenerEnter, DecisionRequested, DecisionAnswered, RandomCall, Terminal, Fault,
}

/// <summary>Copied metadata for developer diagnostics, never a participant observation.</summary>
public sealed record SessionDebugRecord(
    long Sequence, SessionDebugKind Kind, string? Subject, string? Detail,
    DslSourceLocation? Source, long StateVersion, SessionStatus Status, long Instructions)
{
    public RandomCallDetails? Random { get; init; }
}

/// <summary>One successful API draw, with raw-word cursors and a bounded copy of ordered instance candidates.</summary>
public sealed record RandomCallDetails(string Operation, string Minimum, string Maximum, string Result,
    long Before, long After, long TotalBefore, long TotalAfter,
    IReadOnlyList<InstanceId>? Candidates, int CandidateCount, bool CandidatesTruncated);

/// <summary>Bounded pull-based diagnostics. No sink callback runs inside the scheduler.</summary>
public sealed class SessionDebugTrace
{
    private readonly SessionDebugOptions _options;
    private readonly List<SessionDebugRecord> _records = [];
    private long _sequence;
    private SessionDebugRecord? _randomContext;

    internal SessionDebugTrace(SessionDebugOptions options)
    {
        if (options.MaxRecords < 1 || options.MaxTextLength < 1 || options.MaxCandidates < 1)
            throw Faults.Schema("debug MaxRecords, MaxTextLength and MaxCandidates must be positive");
        _options = options;
    }

    public long DroppedRecords { get; private set; }
    public SessionDebugRecord? FirstFault { get; private set; }
    public IReadOnlyList<SessionDebugRecord> Records => _records.AsReadOnly();
    public IReadOnlyList<SessionDebugRecord> Capture() => Array.AsReadOnly(_records.ToArray());

    internal void BeginRandom(Session session, DslSourceLocation? source) =>
        _randomContext = new(0, SessionDebugKind.RandomCall, null, null, source,
            session.StateVersion, session.Status, session.DebugInstructions);

    internal void EndRandom() => _randomContext = null;

    internal void RecordRandom(RandomService service, string stream, string operation,
        string minimum, string maximum, string result, long before, long totalBefore,
        IReadOnlyList<InstanceId>? candidates = null)
    {
        // All records share one capacity. Do not allocate candidate copies after it has filled.
        var sequence = ++_sequence;
        if (_records.Count >= _options.MaxRecords) { DroppedRecords++; return; }
        var context = _randomContext ?? new(0, SessionDebugKind.RandomCall, null, null, null, 0, SessionStatus.Running, 0);
        var count = candidates?.Count ?? 0;
        _records.Add(context with
        {
            Sequence = sequence, Subject = Limit(stream), Detail = operation,
            Random = new(operation, minimum, maximum, result, before, service.DrawCount(stream),
                totalBefore, service.TotalDraws,
                candidates is null ? null : Array.AsReadOnly(candidates.Take(_options.MaxCandidates).ToArray()),
                count, count > _options.MaxCandidates),
        });
    }

    internal void Record(Session session, SessionDebugKind kind, string? subject, string? detail,
        DslSourceLocation? source)
    {
        if (kind == SessionDebugKind.Instruction && !_options.IncludeInstructions) return;
        var record = new SessionDebugRecord(++_sequence, kind, Limit(subject), Limit(detail), source,
            session.StateVersion, session.Status, session.DebugInstructions);
        if (kind == SessionDebugKind.Fault) FirstFault ??= record;
        if (_records.Count < _options.MaxRecords) _records.Add(record);
        else DroppedRecords++;
    }

    private string? Limit(string? text) => text is not null && text.Length > _options.MaxTextLength
        ? text[.._options.MaxTextLength] : text;
}

public sealed partial class Session
{
    private string? _debugCommand;
    private DslSourceLocation? _executingSource;
    internal long DebugInstructions => _instructions;
    public SessionDebugTrace? Debug { get; }

    private void Trace(SessionDebugKind kind, string? subject = null, string? detail = null,
        DslSourceLocation? source = null) => Debug?.Record(this, kind, subject, detail, source);

}
