using CoreTheSpire.Engine;
using NetTheSpire.Contracts;
using NetTheSpire.Contracts.Dto;
using NetTheSpire.Projection;

namespace NetTheSpire.Control;

/// <summary>账本条目的状态（protocol.md §4 第 187/193 行：pending → settling → 完成结果）。</summary>
public enum LedgerState
{
    Pending,
    Settling,
    Completed,
}

/// <summary>一条请求账本记录：请求指纹 + 状态 + 完成响应（**成功与拒绝都缓存**）。</summary>
public sealed record LedgerEntry(string RequestId, Operation Operation, string Fingerprint)
{
    public LedgerState State { get; internal set; } = LedgerState.Pending;

    public ResponseDto? Response { get; internal set; }

    /// <summary>已完成结果对应的稳定观察版本；未完成时为 null。</summary>
    public long? ObservationId { get; internal set; }
}

/// <summary>
/// 一次会话：一个引擎 <see cref="Session"/> ＋ 它的请求账本 ＋ 当前候选绑定。
///
/// 语义依据（`protocol.md`）：`:187` 去重（start 进程级、step/close 会话级、成败都缓存、先核指纹再核版本）、
/// `:193` 锁内顺序 pending→settling→执行一次→先更新快照/绑定表再应答、并发返回 `session_busy`、
/// `:201` 关闭后旧句柄不执行、`:214` 不自动创建新会话继续旧动作。
/// </summary>
public sealed class ControlSession
{
    private readonly Session _session;
    private readonly PublicSnapshotProjector _projector;
    private readonly ParticipantId _participant;
    private readonly string _projectionId;
    private readonly Dictionary<string, LedgerEntry> _ledger = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CandidateBinding> _bindings = new(StringComparer.Ordinal);
    private readonly int _ledgerCapacity;
    private long _observationId;
    private bool _started;

    /// <summary>
    /// 正式路径的会话：接管规则提供方返回的**已配置、尚未 Start** 的会话，只 Start 一次，
    /// 并在 <c>start</c> 成功结果里回报**实际接受的游戏 seed 字符串**（protocol.md §7）。
    /// </summary>
    public ControlSession(
        PublicSnapshotProfile profile,
        string sessionId,
        ProvidedSession provided,
        int ledgerCapacity = 4096)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(provided);
        _session = provided.Session;
        _projector = new PublicSnapshotProjector(provided.Session.RuleSet, profile, sessionId);
        _participant = provided.Participant;
        _projectionId = provided.ProjectionId;
        _ledgerCapacity = ledgerCapacity;
        AcceptedSeed = provided.AcceptedSeed;
        RequestedSeed = provided.RequestedSeed;
        SessionId = sessionId;
    }

    public string SessionId { get; }

    /// <summary>本局实际接受的游戏 seed 字符串。</summary>
    public string AcceptedSeed { get; }

    /// <summary>请求方给出的原始 seed（证据用，未标准化）。</summary>
    public string? RequestedSeed { get; }

    public bool Closed { get; private set; }

    /// <summary>是否有请求正在结算（结算中收到另一个控制请求必须报 <c>session_busy</c>）。</summary>
    public bool Settling => _ledger.Values.Any(entry => entry.State == LedgerState.Settling);

    public SessionStatus EngineStatus => _session.Status;

    /// <summary>
    /// 测试/提供方判定工具专用：暴露引擎会话，供检查读取**内部**状态（状态版本、RNG 游标、隐藏牌序）。
    /// 这些内部值**不得**进入任何公开快照、模型特征或训练输入；它们只用于 V-02/V-09 这类"公开面不变"
    /// 的对照判定。
    /// </summary>
    public Session EngineForTests => _session;

    /// <summary>
    /// 测试/提供方判定工具专用：本会话的观看者与投影 id，供检查**自己**回归引擎
    /// <c>ParticipantView</c>（公开对照必须以原始投影成员顺序为准，而不是回头读已投影的公开快照）。
    /// 生产路径不读它：控制入口一律走 <see cref="Snapshot"/>。
    /// </summary>
    public ParticipantId ParticipantForTests => _participant;

    /// <inheritdoc cref="ParticipantForTests"/>
    public string ProjectionIdForTests => _projectionId;

    public LedgerEntry? Lookup(string requestId) =>
        _ledger.TryGetValue(requestId, out var entry) ? entry : null;

    public int LedgerCount => _ledger.Count;

    // -- 生命周期 ---------------------------------------------------------------------------------------------

    public ResponseDto Start(RequestDto request)
    {
        // "只 Start 一次"是明文契约：工厂不代启动，控制入口也不得启动第二次。
        // 这个断言把违反契约的调用变成**显式错误**，而不是悄悄再跑一遍开局程序。
        if (_started)
        {
            throw new InvalidOperationException(
                $"会话 {SessionId} 已经 Start 过：同一会话不得启动两次（ENV 实现约束第 3 节）");
        }

        _started = true;
        _session.Start();
        var projected = Snapshot();
        var result = WireValue.Object(
            ("session_id", WireValue.Str(SessionId)),
            ("seed", WireValue.Str(AcceptedSeed)),
            ("snapshot", projected.Snapshot is null ? WireValue.Null : projected.Snapshot.ToWire()));
        // 注意：start 的去重记录是**进程级**（protocol.md:187），因此不写入本会话账本；
        // 由 ControlHost 的 start 账本负责"原样重发不启动第二局"。
        return Ok(request.RequestId, result);
    }

    public ResponseDto Observe(RequestDto request)
    {
        if (Closed)
        {
            return Fail(request, "session_closed", "会话已关闭（不自动创建新会话继续旧动作）", null, "stop", "completed");
        }

        var projected = Snapshot();
        var result = WireValue.Object(
            ("session_status", WireValue.Str(projected.SessionStatus)),
            ("snapshot", projected.Snapshot is null ? WireValue.Null : projected.Snapshot.ToWire()));
        return Complete(request, Ok(request.RequestId, result));
    }

    public ResponseDto Close(RequestDto request)
    {
        Closed = true;
        var result = WireValue.Object(
            ("session_id", WireValue.Str(SessionId)),
            ("session_status", WireValue.Str("closed")));
        return Complete(request, Ok(request.RequestId, result));
    }

    /// <summary>
    /// 查询一条请求的状态。**同步后端只会回答 completed 或 not_found**：一次 <see cref="Step"/> 在会话锁内
    /// 走完 pending → settling → 记录结果 → 应答（protocol.md:193），因此不存在可被观察到的 pending 条目，
    /// 也不存在"开始了但无法确认结果"的本地情形。协议预留的 pending/outcome_unknown 面向异步提供方或
    /// 进程丢失后的重新识别（protocol.md:197/199），本实现**不伪造**这两种状态：客户端对它们的处理由
    /// 脚本化测试后端单独验证（H-03，标为故障注入）。
    /// </summary>
    public ResponseDto RequestStatus(RequestDto request, string targetRequestId)
    {
        var entry = Lookup(targetRequestId);
        string status;
        WireValue? response = null;
        if (entry is null)
        {
            // 没有记录：这**不是**"未执行"的证明（protocol.md:197/199 明确禁止把它当作未执行），
            // 只是"本会话无法回答这个 request_id"。
            status = "not_found";
        }
        else if (entry.Response is not null)
        {
            status = "completed";
            response = entry.Response.ToWire();
        }
        else
        {
            // 同步路径下不可达；保留为显式 pending 而不是静默改成 completed（异步后端接入时才有意义）。
            status = "pending";
        }

        var body = WireValue.Object(
            ("status", WireValue.Str(status)),
            ("response", response ?? WireValue.Null));
        return Ok(request.RequestId, body);
    }

    // -- step：版本/候选绑定 → 执行一次 → 记录结果 ---------------------------------------------------------

    /// <summary>
    /// 开始执行一个候选。返回的 <c>NeedsFinish</c> 为 true 时，条目处于 <see cref="LedgerState.Settling"/>，
    /// 需要调用方随后调用 <see cref="FinishStep"/>（正常路径下 <c>Step</c> 会自动收尾；分离是为了让
    /// "结算中收到其它控制请求 → session_busy" 可被测试）。
    /// </summary>
    public ResponseDto Step(RequestDto request, string candidateId, long expectedObservationId)
    {
        if (Closed)
        {
            return Fail(request, "session_closed", "会话已关闭", null, "stop", "completed");
        }

        if (Settling)
        {
            return Fail(request, "session_busy", "另有控制请求正在结算：查询原请求或稍后重试", null, "query_request", "unknown");
        }

        if (expectedObservationId != _observationId)
        {
            return Fail(request, "stale_observation",
                $"expected_observation_id={expectedObservationId} 已过期（当前 {_observationId}）：必须重新 observe",
                null, "query_request", "not_started");
        }

        if (!_bindings.TryGetValue(candidateId, out var binding))
        {
            return Fail(request, "illegal_candidate",
                $"候选不属于当前稳定边界（不得把旧候选映射到新索引）：{candidateId}",
                null, "correct_input", "not_started");
        }

        if (_observationId == WireCanonical.SafeIntegerMax)
        {
            return Fail(request, "capacity_exceeded", "观察版本已达到安全整数上限：拒绝新动作", null, "stop", "not_started");
        }

        if (!TryBegin(request, out var entry))
        {
            return FailCapacity(request);
        }

        entry!.State = LedgerState.Settling;
        try
        {
            if (binding.DecisionRequestId is { } flagRequest && binding.DecisionFlag is { } flag)
            {
                _session.Answer(new DecisionAnswer(flagRequest, Flag: flag));
            }
            else if (binding.DecisionRequestId is { } emptyRequest && binding.EmptyDecision)
            {
                _session.Answer(new DecisionAnswer(emptyRequest, Instances: []));
            }
            else if (binding.DecisionRequestId is { } decisionRequestId && binding.DecisionInstance is { } instance)
            {
                // 决策候选：投影器已把 RequestId 与所选实例写进绑定，直接还原为一条应答。
                _session.Answer(new DecisionAnswer(decisionRequestId,
                    Instances: binding.DecisionSecondInstance is { } second ? [instance, second] : [instance]));
            }
            else if (binding.Action is { } action)
            {
                _session.Submit(action, _participant, binding.Arguments);
            }
            else
            {
                entry.Response = Fail(request, "illegal_candidate",
                    "候选绑定既不是决策应答也不是可提交动作（拒绝猜测）", null, "correct_input", "not_started");
                entry.State = LedgerState.Completed;
                return entry.Response;
            }
        }
        catch (EngineFaultException fault)
        {
            entry.Response = Fail(request, "backend_fault", $"引擎拒绝该动作：{fault.Message}", null, "stop", "completed");
            entry.State = LedgerState.Completed;
            return entry.Response;
        }

        return Settle(entry, request);
    }

    /// <summary>把处于结算中的条目推进到完成（正常路径由 <see cref="Step"/> 内部调用）。</summary>
    public ResponseDto FinishStep(string requestId)
    {
        var entry = Lookup(requestId);
        if (entry is null || entry.State != LedgerState.Settling)
        {
            return Fail(new RequestDto(requestId, Operation.Step, new WireObject([])),
                "invalid_input", "该 request_id 不在结算中", null, "correct_input", "not_started");
        }

        return Settle(entry, new RequestDto(requestId, Operation.Step, new WireObject([])));
    }

    // -- 去重与容量 --------------------------------------------------------------------------------------------

    /// <summary>同 request_id + 同载荷 → 直接返回**原响应**（不第二次执行）；换载荷 → <c>request_conflict</c>。</summary>
    public ResponseDto? ReplayIfKnown(RequestDto request)
    {
        var entry = Lookup(request.RequestId);
        if (entry is null)
        {
            return null;
        }

        if (!string.Equals(entry.Fingerprint, Fingerprint(request), StringComparison.Ordinal))
        {
            return Fail(request, "request_conflict", "同一 request_id 被用于不同载荷：修正输入，不得换载荷重用",
                null, "correct_input", "not_started");
        }

        return entry.Response ?? Ok(request.RequestId, WireValue.Object(
            ("status", WireValue.Str("pending")),
            ("response", WireValue.Null)));
    }

    /// <summary>容量上限：拒绝**新的控制请求**，不丢弃旧记录、不截断数据。</summary>
    public ResponseDto FailCapacity(RequestDto request) =>
        Fail(request, "capacity_exceeded",
            $"账本容量 {_ledgerCapacity} 已满：拒绝新控制请求（不截断、不丢弃既有记录）", null, "stop", "not_started");

    // -- 投影与内部 --------------------------------------------------------------------------------------------

    public ProjectedSnapshot Snapshot()
    {
        var view = _session.Observe(_participant, _projectionId);
        var projected = _projector.Project(new SnapshotInput(
            view,
            _session.LegalActions(_participant),
            _session.PendingDecision,
            _session.Status,
            _session.Terminal), _observationId);
        _observationId = projected.ObservationId;
        _bindings.Clear();
        foreach (var binding in projected.Bindings)
        {
            _bindings[binding.CandidateId] = binding;
        }

        return projected;
    }

    private ResponseDto Settle(LedgerEntry entry, RequestDto request)
    {
        // A new decision can leave entity storage unchanged. Versions belong to settled
        // control boundaries, not to StateStore writes or the number of observe calls.
        _observationId++;
        var projected = Snapshot();
        var result = WireValue.Object(
            ("session_status", WireValue.Str(projected.SessionStatus)),
            ("snapshot", projected.Snapshot is null ? WireValue.Null : projected.Snapshot.ToWire()));
        entry.Response = _session.Status == SessionStatus.Faulted
            ? Fail(request, "backend_fault", "引擎在结算中进入 faulted", null, "stop", "completed")
            : Ok(request.RequestId, result);
        entry.State = LedgerState.Completed;
        entry.ObservationId = projected.ObservationId;
        return entry.Response;
    }

    private bool TryBegin(RequestDto request, out LedgerEntry? entry)
    {
        if (_ledger.Count >= _ledgerCapacity)
        {
            entry = null;
            return false;
        }

        entry = new LedgerEntry(request.RequestId, request.Operation, Fingerprint(request));
        _ledger[request.RequestId] = entry;
        return true;
    }

    private ResponseDto Complete(RequestDto request, ResponseDto response)
    {
        if (!TryBegin(request, out var entry))
        {
            return FailCapacity(request);
        }

        entry!.Response = response;
        entry.State = LedgerState.Completed;
        entry.ObservationId = _observationId;
        if (ReferenceEquals(entry.Response, response))
        {
            return response;
        }

        return response;
    }

    /// <summary>把一次已完成的操作记入会话账本（成功与拒绝都缓存，protocol.md:187）。</summary>
    public void Record(RequestDto request, ResponseDto response)
    {
        if (!TryBegin(request, out var entry))
        {
            return;
        }

        entry!.Response = response;
        entry.State = LedgerState.Completed;
        entry.ObservationId = _observationId;
    }

    private static ResponseDto Ok(string requestId, WireValue result) =>
        new(requestId, true, result, null);

    public ResponseDto Fail(
        RequestDto request,
        string code,
        string message,
        string? path,
        string retry,
        string execution) =>
        new(request.RequestId, false, null, new ErrorDto(
            code,
            message,
            path,
            SessionId,
            _observationId,
            execution,
            retry));

    public static string Fingerprint(RequestDto request) => WireCanonical.Digest(request.Payload);
}
