namespace CoreTheSpire.Engine;

public enum SessionStatus
{
    /// <summary>Idle or running: the host may submit an action.</summary>
    Running = 0,

    /// <summary>Suspended inside a decision. Only <see cref="Session.Answer"/> is legal.</summary>
    AwaitingDecision = 1,

    /// <summary>A declared terminal result was set. No further command is legal.</summary>
    Terminal = 2,

    /// <summary>An engine fault stopped the session. No further command is legal.</summary>
    Faulted = 3,
}

/// <summary>
/// Execution budget. A budget overrun is an explicit fault, never a silent truncation.
/// <paramref name="MaxLegalBindings"/> caps how many legal argument combinations one action may enumerate.
/// </summary>
public sealed record SessionBudget(
    long MaxInstructions = 100_000,
    int MaxCallDepth = 64,
    int MaxEventDepth = 32,
    int MaxLegalBindings = 256)
{
    public static readonly SessionBudget Default = new();
}

/// <summary>One public decision request. Candidates are ordered by the query that produced them.</summary>
public sealed record DecisionRequest(
    long RequestId,
    long StateVersion,
    ParticipantId Participant,
    DecisionResponseKind Kind,
    string Prompt,
    IReadOnlyList<InstanceId> Candidates,
    long Minimum,
    long Maximum);

/// <summary>Host answer to one decision request. Only the fields for the request's kind are read.</summary>
public sealed record DecisionAnswer(
    long RequestId,
    IReadOnlyList<InstanceId>? Instances = null,
    long Number = 0,
    bool Flag = false,
    int EnumOrdinal = 0,
    ParticipantId Participant = default);

/// <summary>
/// An action the host may submit for one participant.
///
/// Parameters whose values the engine can enumerate are exposed as <see cref="Domains"/>: a candidate query for the
/// candidate parameter and the declared choices for choice parameters. <see cref="Bindings"/> lists the argument
/// tuples whose guard actually holds right now, which is what the host offers as "legal submissions".
/// </summary>
public sealed record LegalAction(
    ActionDefinition Definition,
    IReadOnlyDictionary<int, IReadOnlyList<Value>> Domains,
    IReadOnlyList<IReadOnlyList<Value>> Bindings)
{
    /// <summary>Current candidate instances of the action's candidate parameter, if it declares one.</summary>
    public IReadOnlyList<InstanceId> Candidates =>
        Definition.CandidateParameterIndex >= 0 &&
        Domains.TryGetValue(Definition.CandidateParameterIndex, out var values)
            ? values.Select(value => value.AsRef()).ToArray()
            : [];
}

/// <summary>Declared terminal outcome of a session.</summary>
public sealed record TerminalOutcome(TerminalResultDefinition Definition, IReadOnlyList<Value> Fields);

/// <summary>
/// Compute backend seam (K-08). The scheduler never implements compute itself; without an installed backend a
/// <c>compute</c> instruction is an explicit unsupported fault.
/// </summary>
public interface IComputeBackend
{
    string Id { get; }

    void Execute(ComputeAssetDefinition asset, IReadOnlyList<Value> inputs, Value[] outputs);
}

/// <summary>
/// The single scheduler (K-03) and decision protocol (K-04).
///
/// Execution model: one explicit activation stack. A call activation owns a frame (parameters and locals in one
/// value array, a scope, and a stack of block cursors); a dispatch activation owns one event emission and its
/// listener snapshot. Loops reuse their cursor, so the stack grows with nesting depth, never with iteration count.
/// A decision suspension leaves the stack intact and advances the program counter past the decision instruction, so
/// resuming continues exactly after it and never re-runs already resolved effects.
///
/// Every command is guarded by an execution budget and by an explicit fault state: an overrun or a program failure
/// leaves the session <see cref="SessionStatus.Faulted"/> and can never be resumed by accident.
/// </summary>
public sealed partial class Session
{
    private readonly List<Activation> _stack = [];
    private readonly Dictionary<ParticipantId, string> _roles = [];
    private readonly List<string> _roleNames = [];
    private long _nextRequestId = 1;
    private long _instructions;
    private int _drainDepth;
    private Pending? _pending;

    public Session(RuleSet ruleSet, ulong seed, IReadOnlyList<string> participantRoles,
        SessionBudget? budget = null, IComputeBackend? computeBackend = null,
        string randomAlgorithmId = RandomService.DefaultAlgorithmId,
        string randomDrawProfileId = RandomService.DefaultDrawProfileId, SessionDebugOptions? debug = null,
        IRandomProfile? randomProfile = null, string? gameSeed = null)
    {
        RuleSet = ruleSet ?? throw new ArgumentNullException(nameof(ruleSet));
        ArgumentNullException.ThrowIfNull(participantRoles);
        if (participantRoles.Count == 0) throw Faults.Schema("a session needs at least one participant role");
        Budget = budget ?? SessionBudget.Default;
        ComputeBackend = computeBackend;
        State = new SessionState(ruleSet);
        Random = new RandomService(seed, randomAlgorithmId, randomDrawProfileId, randomProfile);
        Seed = seed;
        GameSeed = gameSeed ?? seed.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Debug = debug is null ? null : new SessionDebugTrace(debug);
        Random.Debug = Debug;

        for (var i = 0; i < participantRoles.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(participantRoles[i]))
                throw Faults.Schema("participant role names must be non-empty");
            var participant = new ParticipantId(i + 1);
            _roles[participant] = participantRoles[i];
            _roleNames.Add(participantRoles[i]);
        }

        Identity = string.Join("|",
            ruleSet.Identity,
            "seed=" + Random.SeedFingerprint.ToString("x16"),
            "random=" + Random.AlgorithmId,
            "draw=" + Random.DrawProfileId,
            "participants=" + string.Join(",", _roleNames));
        if (gameSeed is not null) Identity += "|game-seed=" + Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(gameSeed)));
    }

    public RuleSet RuleSet { get; }

    public SessionState State { get; }

    public RandomService Random { get; }

    public SessionBudget Budget { get; }

    public IComputeBackend? ComputeBackend { get; }

    public ulong Seed { get; }
    public string GameSeed { get; }

    /// <summary>Session identity: rule set identity, seed fingerprint, random configuration and participant roles.</summary>
    public string Identity { get; }

    public SessionStatus Status { get; private set; } = SessionStatus.Running;

    public EngineFault? Fault { get; private set; }

    public DecisionRequest? PendingDecision => _pending?.Request;

    public TerminalOutcome? Terminal { get; private set; }

    public long StateVersion => State.Version;

    public IReadOnlyList<string> ParticipantRoles => _roleNames;

    public ParticipantId Participant(int index)
    {
        if (index < 0 || index >= _roleNames.Count) throw Faults.Range($"participant index {index} is out of range");
        return new ParticipantId(index + 1);
    }

    public string RoleOf(ParticipantId participant) =>
        _roles.TryGetValue(participant, out var role)
            ? role
            : throw Faults.Of(EngineFaultCode.UnknownEntity, $"{participant} is not a session participant");

    /// <summary>Executed instruction count of the most recent top-level command (diagnostics only).</summary>
    public long LastCommandInstructions { get; private set; }

    // -- commands -----------------------------------------------------------------------------------------------

    /// <summary>
    /// Runs the package's session-start procedures in package order. The command is one construction process: an
    /// instance created here may still be missing a required reference while later instructions of the same command
    /// assign it, but the command boundary itself must be stable.
    /// </summary>
    public void Start()
    {
        BeginCommand("start");
        try
        {
            Fatal(() =>
            {
                _stack.Add(new StartupActivation(RuleSet.SessionStart));
                Drain();
                State.RequireStableState("session start");
            });
        }
        finally
        {
            EndCommand();
        }
    }

    /// <summary>
    /// Actions the participant may submit now, with the argument tuples whose guard holds. A guard may only read
    /// parameters that declare a domain (a candidate query or declared choices), which the link-time check enforces,
    /// so availability is always decidable without host input.
    /// </summary>
    public IReadOnlyList<LegalAction> LegalActions(ParticipantId participant)
    {
        // A stopped session offers nothing. This is a read-only query, so it answers with an empty list instead of
        // being refused like a command; Submit/Step/Answer still reject anything after a fault or terminal result.
        if (Status != SessionStatus.Running) return [];
        BeginCommand("legal-actions");
        try
        {
            var role = RoleOf(participant);
            return Fatal(() =>
            {
                var result = new List<LegalAction>();
                foreach (var action in RuleSet.Actions)
                {
                    if (action.ParticipantRole is not null &&
                        !string.Equals(action.ParticipantRole, role, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    // Read-only: a throwaway activation and expression evaluation never touch state or randomness.
                    var probe = new CallActivation(action.Program, State.RootScope, -1, null, []);
                    var domains = Domains(action, probe);
                    if (domains.Values.Any(values => values.Count == 0)) continue;
                    var slots = domains.Keys.OrderBy(slot => slot).ToArray();
                    long combinations = 1;
                    foreach (var slot in slots) combinations *= domains[slot].Count;
                    if (combinations > Budget.MaxLegalBindings)
                    {
                        throw Faults.Budget(
                            $"action '{action.QualifiedId}' enumerates {combinations} argument combinations, " +
                            $"above the configured maximum {Budget.MaxLegalBindings}");
                    }

                    var bindings = new List<IReadOnlyList<Value>>();
                    var counters = new int[slots.Length];
                    while (true)
                    {
                        for (var index = 0; index < slots.Length; index++)
                            probe.Slots[slots[index]] = domains[slots[index]][counters[index]];
                        if (action.Guard is null || Eval(probe, action.Guard).AsBool())
                            bindings.Add(probe.Slots.Take(action.Program.Parameters.Count).ToArray());
                        var position = slots.Length - 1;
                        while (position >= 0 && ++counters[position] >= domains[slots[position]].Count)
                        {
                            counters[position] = 0;
                            position--;
                        }

                        if (position < 0) break;
                    }

                    if (bindings.Count > 0) result.Add(new LegalAction(action, domains, bindings));
                }

                return (IReadOnlyList<LegalAction>)result;
            });
        }
        finally
        {
            EndCommand();
        }
    }

    /// <summary>Enumerates the submit-able values of every parameter that declares a domain.</summary>
    private Dictionary<int, IReadOnlyList<Value>> Domains(ActionDefinition action, CallActivation activation)
    {
        var domains = new Dictionary<int, IReadOnlyList<Value>>();
        if (action.CandidateQuery is not null)
        {
            domains[action.CandidateParameterIndex] = EvaluateQuery(activation, action.CandidateQuery)
                .Select(Value.OfRef)
                .ToArray();
        }

        foreach (var (index, values) in action.Choices)
            domains[index] = values.Select(value => Eval(activation, value)).ToArray();
        return domains;
    }

    /// <summary>Submits one action for one participant. Validation happens before any state or randomness changes.</summary>
    public void Submit(ActionDefinition action, ParticipantId participant, IReadOnlyList<Value> arguments)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(arguments);
        BeginCommand("submit");
        try
        {
            if (Status == SessionStatus.AwaitingDecision)
            {
                throw Faults.Illegal(
                    $"the session is awaiting decision {_pending!.Request.RequestId}; only Answer is legal");
            }

            var role = RoleOf(participant);
            if (action.ParticipantRole is not null &&
                !string.Equals(action.ParticipantRole, role, StringComparison.Ordinal))
            {
                throw Faults.Of(EngineFaultCode.UnauthorizedAction,
                    $"action '{action.QualifiedId}' is reserved for role '{action.ParticipantRole}' but " +
                    $"{participant} is '{role}'");
            }

            if (arguments.Count != action.Program.Parameters.Count)
            {
                throw Faults.Illegal(
                    $"action '{action.QualifiedId}' takes {action.Program.Parameters.Count} arguments but " +
                    $"{arguments.Count} were supplied");
            }

            var activation = new CallActivation(action.Program, State.RootScope, -1, null, arguments);
            foreach (var (slot, allowed) in Domains(action, activation))
            {
                var supplied = arguments[slot];
                if (!allowed.Any(value => value.Equals(supplied)))
                {
                    throw Faults.Illegal(
                        $"'{supplied}' is not a current candidate for parameter {slot} of '{action.QualifiedId}'");
                }
            }

            if (action.Guard is not null && !Eval(activation, action.Guard).AsBool())
                throw Faults.Illegal($"guard of action '{action.QualifiedId}' does not hold");

            Fatal(() =>
            {
                _stack.Add(activation);
                Drain();
                State.RequireStableState($"the submitted action '{action.QualifiedId}'");
            });
        }
        finally
        {
            EndCommand();
        }
    }

    /// <summary>Answers the current decision request. A stale or repeated request id changes nothing.</summary>
    public void Answer(DecisionAnswer answer)
    {
        ArgumentNullException.ThrowIfNull(answer);
        BeginCommand("answer");
        try
        {
            var pending = _pending
                ?? throw Faults.Illegal("the session has no pending decision");

            if (answer.RequestId != pending.Request.RequestId)
            {
                throw Faults.Of(EngineFaultCode.StaleDecision,
                    $"decision request {answer.RequestId} is not current ({pending.Request.RequestId})");
            }

            if (pending.Request.StateVersion != State.Version)
            {
                throw Faults.Of(EngineFaultCode.StaleDecision,
                    $"decision request {answer.RequestId} was bound to state version " +
                    $"{pending.Request.StateVersion} but the state is at {State.Version}");
            }

            if (pending.ResultSlot < 0 || pending.ResultSlot >= pending.Caller.Slots.Length)
                throw Faults.Schema($"decision result slot {pending.ResultSlot} is not part of the suspended frame");

            var value = BuildAnswerValue(pending.Request, answer);
            Fatal(() =>
            {
                Trace(SessionDebugKind.DecisionAnswered, pending.Request.RequestId.ToString(), value.ToString());
                pending.Caller.Slots[pending.ResultSlot] = value;
                _pending = null;
                Status = SessionStatus.Running;
                Drain();
                State.RequireStableState($"the answer to decision {answer.RequestId}");
            });
        }
        finally
        {
            EndCommand();
        }
    }

    /// <summary>
    /// Continues execution of the current activation stack. Present so a host can drive the scheduler explicitly;
    /// the commands above already drain to the next suspension, terminal result or fault.
    /// </summary>
    public void Step()
    {
        BeginCommand("step");
        try
        {
            if (Status == SessionStatus.AwaitingDecision)
                throw Faults.Illegal($"the session is awaiting decision {_pending!.Request.RequestId}");
            Fatal(() =>
            {
                Drain();
                State.RequireStableState("the stepped command");
            });
        }
        finally
        {
            EndCommand();
        }
    }

    /// <summary>
    /// Pure read of one participant's projection. Observing never changes state, version or randomness. A participant
    /// may only observe a projection declared for their own role.
    ///
    /// A projection is a published snapshot, so observing an instance whose construction is still open is refused
    /// with <see cref="EngineFaultCode.RequiredRefUnassigned"/>.
    /// </summary>
    public ParticipantView Observe(ParticipantId viewer, ProjectionDefinition projection)
    {
        ArgumentNullException.ThrowIfNull(projection);
        // A projection is a stable boundary: an instance that still has a required reference unset must not be
        // published, so a half-built instance can never leak into an observation (or from there into a snapshot).
        if (State.UnderConstruction.Count > 0)
        {
            throw Faults.Of(EngineFaultCode.RequiredRefUnassigned,
                $"cannot publish a projection while {State.UnderConstruction.Count} instance(s) are still under " +
                "construction");
        }

        RequireProjectionRole(viewer, projection);
        State.RequireStableState("a participant view");
        return ProjectionObserver.Observe(State, viewer, projection, Terminal);
    }

    /// <summary>Pure read of one participant's projection by declared projection id.</summary>
    public ParticipantView Observe(ParticipantId viewer, string projectionId)
    {
        foreach (var projection in RuleSet.Projections)
        {
            if (string.Equals(projection.QualifiedId, projectionId, StringComparison.Ordinal))
                return Observe(viewer, projection);
        }

        throw Faults.Of(EngineFaultCode.NoProjection, $"projection '{projectionId}' is not part of the rule set");
    }

    private void RequireProjectionRole(ParticipantId viewer, ProjectionDefinition projection)
    {
        var role = RoleOf(viewer);
        if (!string.Equals(projection.ParticipantRole, role, StringComparison.Ordinal))
        {
            throw Faults.Of(EngineFaultCode.NoProjection,
                $"projection '{projection.QualifiedId}' is declared for role '{projection.ParticipantRole}' but " +
                $"{viewer} is '{role}'");
        }
    }

    /// <summary>Participant projections declared by the rule set.</summary>
    public IEnumerable<ProjectionDefinition> ProjectionsFor(ParticipantId participant)
    {
        var role = RoleOf(participant);
        return RuleSet.Projections.Where(projection =>
            string.Equals(projection.ParticipantRole, role, StringComparison.Ordinal));
    }

    /// <summary>
    /// Wraps the executing part of a command. Only faults raised <em>while executing</em> rule IR reach here, so only
    /// they stop the session: host-input rejections (guard, candidate, role, stale answer) are thrown before this
    /// point and leave the session usable.
    /// </summary>
    private void Fatal(Action body) => Fatal<object?>(() =>
    {
        body();
        return null;
    });

    private T Fatal<T>(Func<T> body)
    {
        try
        {
            return body();
        }
        catch (EngineFaultException fault)
        {
            Status = SessionStatus.Faulted;
            Fault = fault.Fault;
            Trace(SessionDebugKind.Fault, Fault.Code.ToString(), Fault.Message, _executingSource);
            throw;
        }
        catch (Exception exception)
        {
            var fault = Faults.Of(EngineFaultCode.SessionStateInvalid,
                $"unhandled {exception.GetType().Name} in the scheduler: {exception.Message}");
            Status = SessionStatus.Faulted;
            Fault = fault.Fault;
            Trace(SessionDebugKind.Fault, Fault.Code.ToString(), Fault.Message, _executingSource);
            throw fault;
        }
    }

    private void BeginCommand(string command)
    {
        RequireCommand(command);
        _instructions = 0;
        _debugCommand = command;
        _executingSource = null;
        Trace(SessionDebugKind.CommandBegin, command);
    }

    private void EndCommand()
    {
        LastCommandInstructions = _instructions;
        Trace(SessionDebugKind.CommandEnd, _debugCommand);
        _debugCommand = null;
    }

    private void RequireCommand(string command)
    {
        if (_drainDepth > 0)
        {
            throw Faults.Of(EngineFaultCode.SessionStateInvalid,
                $"'{command}' re-entered the scheduler while it was draining; extensions must not call back into it");
        }

        switch (Status)
        {
            case SessionStatus.Faulted:
                throw Faults.Of(EngineFaultCode.SessionStateInvalid,
                    $"the session is faulted ({Fault?.Code}) and cannot accept '{command}'");
            case SessionStatus.Terminal:
                throw Faults.Of(EngineFaultCode.TerminalReached,
                    $"the session already reached terminal result '{Terminal?.Definition.QualifiedId}'");
        }
    }

}
