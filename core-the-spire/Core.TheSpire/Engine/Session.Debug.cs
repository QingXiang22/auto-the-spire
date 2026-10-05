namespace CoreTheSpire.Engine;

public sealed record SessionDebugOptions(int MaxRecords = 10_000, bool IncludeInstructions = false, int MaxTextLength = 1024);

public enum SessionDebugKind
{
    CommandBegin, CommandEnd, ProcedureEnter, ProcedureExit, Instruction,
    EventEmit, ListenerEnter, DecisionRequested, DecisionAnswered, RandomCall, Terminal, Fault,
}

/// <summary>Copied metadata for developer diagnostics, never a participant observation.</summary>
public sealed record SessionDebugRecord(
    long Sequence, SessionDebugKind Kind, string? Subject, string? Detail,
    DslSourceLocation? Source, long StateVersion, SessionStatus Status, long Instructions);

/// <summary>Bounded pull-based diagnostics. No sink callback runs inside the scheduler.</summary>
public sealed class SessionDebugTrace
{
    private readonly SessionDebugOptions _options;
    private readonly List<SessionDebugRecord> _records = [];
    private long _sequence;

    internal SessionDebugTrace(SessionDebugOptions options)
    {
        if (options.MaxRecords < 1 || options.MaxTextLength < 1)
            throw Faults.Schema("debug MaxRecords and MaxTextLength must be positive");
        _options = options;
    }

    public long DroppedRecords { get; private set; }
    public SessionDebugRecord? FirstFault { get; private set; }
    public IReadOnlyList<SessionDebugRecord> Records => _records.AsReadOnly();
    public IReadOnlyList<SessionDebugRecord> Capture() => Array.AsReadOnly(_records.ToArray());

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

    private void TraceRandom(Inst instruction, long before)
    {
        if (Debug is null) return;
        foreach (var stream in Random.Diagnostics())
            if (stream.Draws != _debugRandomBefore.GetValueOrDefault(stream.Name))
                Trace(SessionDebugKind.RandomCall, stream.Name,
                    $"{_debugRandomBefore.GetValueOrDefault(stream.Name)}->{stream.Draws}; total={before}->{Random.TotalDraws}",
                    instruction.SourceLocation);
    }

    private readonly Dictionary<string, long> _debugRandomBefore = new(StringComparer.Ordinal);

    private void BeginRandomTrace()
    {
        if (Debug is null) return;
        _debugRandomBefore.Clear();
        foreach (var stream in Random.Diagnostics()) _debugRandomBefore[stream.Name] = stream.Draws;
    }
}
