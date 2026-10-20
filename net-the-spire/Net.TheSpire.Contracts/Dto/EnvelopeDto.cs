namespace NetTheSpire.Contracts.Dto;

/// <summary>控制操作（protocol.md §4 第 172–185 行）。</summary>
public enum Operation
{
    Describe,
    GetCatalog,
    Start,
    Attach,
    Observe,
    Step,
    RequestStatus,
    Close,
}

public static class Operations
{
    public static string Wire(this Operation operation) => operation switch
    {
        Operation.Describe => "describe",
        Operation.GetCatalog => "get_catalog",
        Operation.Start => "start",
        Operation.Attach => "attach",
        Operation.Observe => "observe",
        Operation.Step => "step",
        Operation.RequestStatus => "request_status",
        Operation.Close => "close",
        _ => throw new WireFaultException("I-66", $"未知操作 {operation}"),
    };

    public static Operation Parse(string text) => text switch
    {
        "describe" => Operation.Describe,
        "get_catalog" => Operation.GetCatalog,
        "start" => Operation.Start,
        "attach" => Operation.Attach,
        "observe" => Operation.Observe,
        "step" => Operation.Step,
        "request_status" => Operation.RequestStatus,
        "close" => Operation.Close,
        _ => throw new WireFaultException("I-66", $"未知操作 '{text}'"),
    };

    /// <summary>成功结果的形状（protocol.md §4 第 183 行）；不存在任意 result 兜底。</summary>
    public static ResultKind ExpectedResult(this Operation operation) => operation switch
    {
        Operation.Describe => ResultKind.Describe,
        Operation.GetCatalog => ResultKind.Catalog,
        Operation.Start => ResultKind.Start,
        Operation.Attach => ResultKind.Attach,
        Operation.Observe => ResultKind.Observe,
        Operation.Step => ResultKind.Snapshot,
        Operation.RequestStatus => ResultKind.RequestStatus,
        Operation.Close => ResultKind.Close,
        _ => ResultKind.Unknown,
    };
}

public enum ResultKind
{
    Unknown,
    Describe,
    Catalog,
    Start,
    Attach,
    Observe,
    Snapshot,
    RequestStatus,
    Close,
}

/// <summary>请求 <c>{request_id, operation, payload}</c>，并按操作校验 payload 形状。</summary>
public sealed record RequestDto(string RequestId, Operation Operation, WireObject Payload)
{
    /// <summary>当前协议入口；start.seed 为非空游戏字符串。</summary>
    public static RequestDto FromWire(WireValue value) => FromWire(value, NetProtocol.V02);

    /// <summary>
    /// 按当前协议校验请求载荷（protocol.md §7）：
    /// seed 是非空游戏字符串，由规则提供方决定校验／标准化，net 不解析成整数。
    /// </summary>
    public static RequestDto FromWire(WireValue value, string protocolVersion)
    {
        NetProtocol.RequireKnown(protocolVersion, "request（运行时声明的 protocol_version）");
        var reader = new WireReader(value, "request").Known("request_id", "operation", "payload");
        var operation = Operations.Parse(reader.RequireString("operation"));
        var payload = reader.Require("payload");
        if (payload is not WireObject payloadObject)
            throw new WireFaultException("I-66", "request.payload: 期望 object");
        ValidatePayload(operation, payloadObject, protocolVersion);
        return new RequestDto(reader.RequireNonEmptyString("request_id"), operation, payloadObject);
    }

    private static void ValidatePayload(Operation operation, WireObject payload, string protocolVersion)
    {
        switch (operation)
        {
            case Operation.Describe:
                new WireReader(payload, "describe_payload").Known();
                break;
            case Operation.GetCatalog:
                new WireReader(payload, "get_catalog_payload").Known("catalog_id").RequireNonEmptyString("catalog_id");
                break;
            case Operation.Start:
            {
                var reader = new WireReader(payload, "start_payload")
                    .Known("expected_identity", "scenario_profile", "seed");
                IdentityDto.FromWire(reader.Require("expected_identity"), protocolVersion);
                reader.RequireNonEmptyString("scenario_profile");
                if (reader.Has("seed")) GameSeed(reader.Require("seed"), protocolVersion);
                break;
            }
            case Operation.Attach:
            {
                var reader = new WireReader(payload, "attach_payload")
                    .Known("expected_identity", "external_session_ref");
                IdentityDto.FromWire(reader.Require("expected_identity"));
                reader.RequireNonEmptyString("external_session_ref");
                break;
            }
            case Operation.Observe:
                new WireReader(payload, "observe_payload").Known("session_id").RequireNonEmptyString("session_id");
                break;
            case Operation.Step:
            {
                var reader = new WireReader(payload, "step_payload")
                    .Known("session_id", "expected_observation_id", "candidate_id");
                reader.RequireNonEmptyString("session_id");
                reader.RequireSafeInteger("expected_observation_id");
                reader.RequireNonEmptyString("candidate_id");
                break;
            }
            case Operation.RequestStatus:
            {
                var reader = new WireReader(payload, "request_status_payload")
                    .Known("session_id", "target_request_id");
                if (reader.Has("session_id")) reader.RequireNonEmptyString("session_id");
                reader.RequireNonEmptyString("target_request_id");
                break;
            }
            case Operation.Close:
                new WireReader(payload, "close_payload").Known("session_id").RequireNonEmptyString("session_id");
                break;
            default:
                throw new WireFaultException("I-66", $"未知操作 {operation}");
        }
    }

    public WireObject ToWire() => WireValue.Object(
        ("request_id", WireValue.Str(RequestId)),
        ("operation", WireValue.Str(Operation.Wire())),
        ("payload", Payload));

    /// <summary>
    /// <c>start.seed</c> 是非空游戏字符串
    /// （不 trim、不改大小写、不数值强转；具体接受值由 base 决定）。未知版本由调用方先拒绝。
    /// </summary>
    public static string GameSeed(WireValue value, string protocolVersion)
    {
        NetProtocol.RequireKnown(protocolVersion, "start.seed protocol_version");
        return WireDecimal.Seed(value, "start.seed");
    }

    public string PayloadFingerprint() => WireIdentity.RequestPayloadFingerprint(Operation.Wire(), Payload);
}

public sealed record ErrorDto(
    string Code,
    string Message,
    string? Path,
    string? SessionId,
    long? ObservationId,
    string ExecutionState,
    string RetryMode)
{
    private static readonly string[] Codes =
    [
        "version_mismatch", "unsupported_capability", "invalid_input", "unknown_type", "dangling_reference",
        "stale_observation", "illegal_candidate", "request_conflict", "session_busy", "capacity_exceeded",
        "session_not_found", "session_closed", "backend_fault", "outcome_unknown",
    ];

    private static readonly string[] ExecutionStates = ["not_started", "completed", "unknown"];
    private static readonly string[] RetryModes = ["correct_input", "query_request", "stop"];

    public static ErrorDto FromWire(WireValue value)
    {
        var reader = new WireReader(value, "error")
            .Known("code", "message", "path", "session_id", "observation_id", "execution_state", "retry_mode");
        var code = reader.RequireString("code");
        if (!Codes.Contains(code, StringComparer.Ordinal))
            throw new WireFaultException("I-66", $"error.code: 未知错误码 '{code}'");
        var executionState = reader.RequireString("execution_state");
        if (!ExecutionStates.Contains(executionState, StringComparer.Ordinal))
            throw new WireFaultException(
                "I-66", $"error.execution_state: 不在枚举 {string.Join('/', ExecutionStates)}");
        var retryMode = reader.RequireString("retry_mode");
        if (!RetryModes.Contains(retryMode, StringComparer.Ordinal))
            throw new WireFaultException("I-66", $"error.retry_mode: 不在枚举 {string.Join('/', RetryModes)}");
        return new ErrorDto(
            code,
            reader.RequireString("message"),
            reader.Has("path") ? reader.RequireString("path") : null,
            reader.OptionalNonEmptyString("session_id"),
            reader.Has("observation_id") ? reader.RequireSafeInteger("observation_id") : null,
            executionState,
            retryMode);
    }

    public WireObject ToWire()
    {
        var members = new List<KeyValuePair<string, WireValue>>
        {
            new("code", WireValue.Str(Code)),
            new("message", WireValue.Str(Message)),
        };
        if (Path is not null) members.Add(new KeyValuePair<string, WireValue>("path", WireValue.Str(Path)));
        if (SessionId is not null) members.Add(new KeyValuePair<string, WireValue>("session_id", WireValue.Str(SessionId)));
        if (ObservationId is not null)
            members.Add(new KeyValuePair<string, WireValue>("observation_id", WireValue.Integer(ObservationId.Value)));
        members.Add(new KeyValuePair<string, WireValue>("execution_state", WireValue.Str(ExecutionState)));
        members.Add(new KeyValuePair<string, WireValue>("retry_mode", WireValue.Str(RetryMode)));
        return new WireObject(members);
    }
}

/// <summary>响应 <c>{request_id, ok, result?, error?}</c>；ok=true 仅有 result，false 仅有 error。</summary>
public sealed record ResponseDto(string RequestId, bool Ok, WireValue? Result, ErrorDto? Error)
{
    public static ResponseDto FromWire(WireValue value)
    {
        var reader = new WireReader(value, "response").Known("request_id", "ok", "result", "error");
        var ok = reader.RequireBoolean("ok");
        var resultNode = reader.Optional("result");
        var errorNode = reader.Optional("error");
        if (ok)
        {
            if (resultNode is null) throw new WireFaultException("N-01-DTO", "response: ok=true 必须带 result");
            if (errorNode is not null) throw new WireFaultException("N-01-DTO", "response: ok=true 不得带 error");
            return new ResponseDto(reader.RequireNonEmptyString("request_id"), true, resultNode, null);
        }

        if (errorNode is null) throw new WireFaultException("N-01-DTO", "response: ok=false 必须带 error");
        if (resultNode is not null) throw new WireFaultException("N-01-DTO", "response: ok=false 不得带 result");
        return new ResponseDto(reader.RequireNonEmptyString("request_id"), false, null, ErrorDto.FromWire(errorNode));
    }

    public WireObject ToWire()
    {
        var members = new List<KeyValuePair<string, WireValue>>
        {
            new("request_id", WireValue.Str(RequestId)),
            new("ok", WireValue.Bool(Ok)),
        };
        if (Result is not null) members.Add(new KeyValuePair<string, WireValue>("result", Result));
        if (Error is not null) members.Add(new KeyValuePair<string, WireValue>("error", Error.ToWire()));
        return new WireObject(members);
    }

    /// <summary>
    /// 成功结果形状判别。用于核对 operation→result 配对（protocol.md §4 第 183 行），
    /// 判别依据是结构键，不是任意 result 兜底。
    /// </summary>
    public static ResultKind ClassifyResult(WireValue result)
    {
        if (result is not WireObject obj) return ResultKind.Unknown;
        var keys = obj.Keys.ToHashSet(StringComparer.Ordinal);
        if (keys.SetEquals(["identity", "capabilities"])) return ResultKind.Describe;
        if (keys.Contains("declarations")) return ResultKind.Catalog;
        // 0.2：start 成功结果多一个实际 seed（protocol.md §7）。结构判别仍按精确键集合，不做任意兜底。
        if (keys.SetEquals(["session_id", "seed", "snapshot"])) return ResultKind.Start;
        if (keys.Contains("session_status") && obj.Get("session_status") is WireString status)
        {
            if (status.Value == "closed") return ResultKind.Close;
            return keys.Contains("session_id") ? ResultKind.Attach : ResultKind.Observe;
        }

        if (keys.Contains("protocol_version") && keys.Contains("entities")) return ResultKind.Snapshot;
        if (keys.Contains("status")) return ResultKind.RequestStatus;
        return ResultKind.Unknown;
    }

    /// <summary>结果状态组合：session_status 为 active/terminal 时必须给出稳定快照。</summary>
    public static void CheckStateCombination(WireValue result, string role)
    {
        if (result is not WireObject obj) return;
        if (obj.Get("session_status") is not WireString status) return;
        if (!SessionStatuses.All.Contains(status.Value, StringComparer.Ordinal))
            throw new WireFaultException(
                "I-66", $"{role}.session_status: 不在枚举 {string.Join('/', SessionStatuses.All)}");
        var hasSnapshot = obj.ContainsKey("snapshot");
        var mustHaveSnapshot = status.Value is "active" or "terminal";
        if (mustHaveSnapshot != hasSnapshot)
            throw new WireFaultException(
                "N-01-DTO", $"{role}: session_status={status.Value} 时 snapshot 必须{(mustHaveSnapshot ? "存在" : "省略")}");
    }
}

/// <summary>会话状态枚举（protocol.md §5 第 193 行）。</summary>
public static class SessionStatuses
{
    public static readonly string[] All =
        ["active", "settling", "terminal", "uncertain", "stopped", "faulted", "closed"];
}
