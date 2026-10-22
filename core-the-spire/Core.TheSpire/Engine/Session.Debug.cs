namespace CoreTheSpire.Engine;

public sealed record SessionDebugOptions(int MaxRecords = 10_000, bool IncludeInstructions = false,
    int MaxTextLength = 1024, int MaxCandidates = 256, bool CaptureValues = false,
    int MaxSnapshotEntities = 256, int MaxSnapshotValues = 4096, bool CaptureCandidateReferences = false,
    bool CaptureCandidateRelations = false);

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
    public SessionDebugSnapshot? Snapshot { get; init; }
    public bool TextTruncated { get; init; }
}

/// <summary>One successful API draw, with raw-word cursors and a bounded copy of ordered instance candidates.</summary>
public sealed record RandomCallDetails(string Operation, string Minimum, string Maximum, string Result,
    long Before, long After, long TotalBefore, long TotalAfter,
    IReadOnlyList<InstanceId>? Candidates, int CandidateCount, bool CandidatesTruncated)
{
    public Float32SampleDetails? Float32 { get; init; }
    /// <summary>Actual generator output only when this API consumed exactly one raw word; otherwise null.</summary>
    public ulong? SingleRawWord { get; init; }
}

/// <summary>Evaluated binary32 bounds and final result, encoded as unsigned 32-bit patterns in Int values.</summary>
public sealed record Float32SampleDetails(long MinimumBits, long MaximumBits, long ResultBits);

/// <summary>Bounded pull-based diagnostics. No sink callback runs inside the scheduler.</summary>
public sealed partial class SessionDebugTrace
{
    private readonly SessionDebugOptions _options;
    private readonly Session _session;
    private readonly List<SessionDebugRecord> _records = [];
    private long _sequence;
    private SessionDebugRecord? _randomContext;

    internal SessionDebugTrace(Session session, SessionDebugOptions options)
    {
        if (options.MaxRecords < 1 || options.MaxTextLength < 1 || options.MaxCandidates < 1
            || options.MaxSnapshotEntities < 1 || options.MaxSnapshotValues < 1)
            throw Faults.Schema("debug record, text, candidate and snapshot limits must be positive");
        _session = session;
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

    internal void RecordFloat32(long minimumBits, long maximumBits, long resultBits)
    {
        // A dropped draw must never attach metadata to a previously retained record.
        if (_records.Count == 0 || _records[^1] is not { Sequence: var sequence, Random.Operation: "double" } record
            || sequence != _sequence) return;
        _records[^1] = record with
        {
            Random = record.Random! with { Float32 = new(minimumBits, maximumBits, resultBits) }
        };
    }

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
            TextTruncated = stream.Length > _options.MaxTextLength,
            Random = new(operation, minimum, maximum, result, before, service.DrawCount(stream),
                totalBefore, service.TotalDraws,
                candidates is null ? null : Array.AsReadOnly(candidates.Take(_options.MaxCandidates).ToArray()),
                count, count > _options.MaxCandidates) { SingleRawWord = service.SingleRawWord(stream, before, totalBefore) },
            Snapshot = _options.CaptureValues ? CaptureSnapshot([], candidates?.Take(_options.MaxCandidates), count > _options.MaxCandidates,
                _options.CaptureCandidateReferences && candidates is not null,
                _options.CaptureCandidateRelations && candidates is not null) : null,
        });
    }

    internal void Record(Session session, SessionDebugKind kind, string? subject, string? detail,
        DslSourceLocation? source, IReadOnlyList<Value>? payload = null)
    {
        if (kind == SessionDebugKind.Instruction && !_options.IncludeInstructions) return;
        // Retain the first fault separately, but never allocate value snapshots after capacity.
        if (_records.Count >= _options.MaxRecords && kind != SessionDebugKind.Fault)
        { _sequence++; DroppedRecords++; return; }
        var record = new SessionDebugRecord(++_sequence, kind, Limit(subject), Limit(detail), source,
            session.StateVersion, session.Status, session.DebugInstructions)
        {
            TextTruncated = subject?.Length > _options.MaxTextLength || detail?.Length > _options.MaxTextLength,
            Snapshot = payload is null || _records.Count >= _options.MaxRecords ? null : CaptureSnapshot(payload)
        };
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
        DslSourceLocation? source = null, IReadOnlyList<Value>? payload = null) => Debug?.Record(this, kind, subject, detail, source, payload);

}
