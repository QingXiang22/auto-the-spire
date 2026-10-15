using CoreTheSpire.Engine;
using NetTheSpire.Contracts;
using NetTheSpire.Contracts.Dto;
using NetTheSpire.Projection;

namespace NetTheSpire.Control;

/// <summary>
/// 控制宿主：按 <c>protocol.md</c> §4 的操作表分发请求，并持有进程级与会话级的请求账本。
///
/// * `start` 的去重记录是**进程级**的（`:187`）：同一 `request_id` 原样重发**不得启动两局**。
/// * `step`/`close` 为**会话级**；成功与拒绝响应都缓存（`:187`）。
/// * `describe`/`get_catalog` 不推进游戏（`:174-175`）。
/// * `attach` 在本模拟后端**不支持**：明确返回 `unsupported_capability`（`:177` 面向真实后端）。
/// </summary>
public sealed class ControlHost
{
    private readonly RuleSet _rules;
    private readonly PublicSnapshotProfile _profile;
    private readonly ParticipantId _participant;
    private readonly string _participantRole;
    private readonly string _projectionId;
    private readonly ulong _seed;
    private readonly int _ledgerCapacity;
    private readonly IComputeBackend? _computeBackend;
    private readonly ISessionSource? _source;
    private readonly Dictionary<string, ControlSession> _sessions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (string Fingerprint, ResponseDto Response)> _startLedger =
        new(StringComparer.Ordinal);
    private readonly string _runToken;
    private int _nextSession = 1;

    /// <summary>
    /// 旧受控切片/作者场景的控制宿主（自己建默认随机会话，协议 0.1）。
    ///
    /// <paramref name="computeBackend"/> 是**通用注入接缝**：规则包里若声明了计算资产
    /// （<c>compute …</c>），宿主必须把该资产要求的后端交给会话。控制层不认识任何游戏 ID 或玩法，
    /// 后端实例由组合层（Host/检查）提供；不传时保持旧行为（引擎在**执行**该资产时报
    /// <c>Unsupported</c>，链接期只检查声明）。
    /// </summary>
    public ControlHost(
        RuleSet rules,
        PublicSnapshotProfile profile,
        ParticipantId participant,
        string participantRole,
        string projectionId,
        ulong seed,
        int ledgerCapacity = 4096,
        string? runToken = null,
        IComputeBackend? computeBackend = null)
    {
        _rules = rules;
        _profile = profile;
        _participant = participant;
        _participantRole = participantRole;
        _projectionId = projectionId;
        _seed = seed;
        _ledgerCapacity = ledgerCapacity;
        _computeBackend = computeBackend;
        // 会话句柄**跨进程不复用**（protocol.md:193「ID 不复用」）：宿主重启后，"session-1" 这类字符串不得
        // 让旧进程发出的句柄指向另一局游戏。会话 ID 属于控制元数据，不进入模型特征或重放语义比较
        // （通用引擎设计 §8.1），因此用一个每次进程启动都不同的运行令牌把它们隔开。
        _runToken = string.IsNullOrWhiteSpace(runToken) ? NewRunToken() : runToken!;
    }

    /// <summary>
    /// **正式路径**的控制宿主：规则集、公开 Profile、场景与实际开局全部来自规则提供方
    /// （<see cref="ISessionSource"/>）。控制宿主不 new 第二个默认随机会话、不重算玩法。
    /// </summary>
    public ControlHost(ISessionSource source, int ledgerCapacity = 4096, string? runToken = null)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _rules = source.Rules;
        _profile = source.Profile;
        _participant = default;
        _participantRole = string.Empty;
        _projectionId = string.Empty;
        _seed = 0;
        _ledgerCapacity = ledgerCapacity;
        SupportedScenarioProfile = source.ScenarioProfile;
        _runToken = string.IsNullOrWhiteSpace(runToken) ? NewRunToken() : runToken!;
    }

    /// <summary>协议版本：由所选公开 Profile 决定（0.1 旧场景 / 0.2 正式出口）。</summary>
    public string ProtocolVersion => _profile.ProtocolVersion;

    /// <summary>
    /// 本运行时**实际选定**的公开 Profile：`describe`/`get_catalog` 发布的身份、目录与它逐字一致。
    /// 消费者（检查/客户端）要核对身份时必须读这里，不能自己再拼一份可能与实际选择不同的 Profile。
    /// </summary>
    public PublicSnapshotProfile Profile => _profile;

    /// <summary>
    /// 本后端支持的场景 profile id（`describe` 与 `start` 必须用同一个）。
    ///
    /// 它是**实例**属性：旧切片/作者场景是 <c>base-spire-combat-slice-v1</c>，正式路径是规则提供方
    /// （<see cref="ISessionSource.ScenarioProfile"/>）实际实现的范围 id。没有"全局默认 profile"这种退路
    /// ——那会让一个忽略实际 source 的常量冒充正式身份。
    /// </summary>
    public string SupportedScenarioProfile { get; } = "base-spire-combat-slice-v1";

    /// <summary>本次运行（≈本次进程）的会话命名空间令牌；测试可显式传入以复现"同一进程"的语义。</summary>
    public string RunToken => _runToken;

    private static string NewRunToken() =>
        Guid.NewGuid().ToString("N", System.Globalization.CultureInfo.InvariantCulture)[..12];

    public int SessionCount => _sessions.Count;

    /// <summary>
    /// 检查用：正式路径下所选 <see cref="ISessionSource"/>（旧场景路径为 <c>null</c>）。
    /// 只用于核对"提供方建的会话确实是已配置未 Start 的那一个"，不得作为生产访问路径。
    /// </summary>
    public ISessionSource? SourceForTests => _source;

    /// <summary>`describe` 声明的实体限额；也是 group 成员数的派生上界（成员都是实体）。</summary>
    private const long MaxEntities = 256;

    /// <summary>测试用：按 id 取会话（检查需要观察 <c>Settling</c> 等内部状态）。</summary>
    public ControlSession GetSessionForTests(string sessionId) =>
        _sessions.TryGetValue(sessionId, out var session)
            ? session
            : throw new InvalidOperationException($"会话不存在：{sessionId}");

    public ResponseDto Dispatch(RequestDto request) => request.Operation switch
    {
        Operation.Describe => Describe(request),
        Operation.GetCatalog => GetCatalog(request),
        Operation.Start => Start(request),
        Operation.Attach => Unsupported(request, "attach",
            "本后端是模拟后端：不提供对现有会话的 attach（protocol.md:177 面向真实后端）"),
        Operation.Observe => ForSession(request, (session, id) => session.Observe(request)),
        Operation.Step => Step(request),
        Operation.RequestStatus => RequestStatus(request),
        Operation.Close => ForSession(request, (session, id) => session.Close(request)),
        _ => Fail(request, "invalid_input", $"不支持的 operation：{request.Operation}"),
    };

    // -- describe / get_catalog --------------------------------------------------------------------------------

    private ResponseDto Describe(RequestDto request)
    {
        var identity = new IdentityDto(
            ProtocolVersion,
            _profile.RulesetId,
            _profile.CatalogId,
            _profile.BackendProfileId);
        // 限额必须与**实际发布**一致：消费者会按这些数字分配缓冲，报一个比真实值小的上限就是假声明。
        // `groups` 的上界 = 声明的 group_type 数（每个集合每份快照最多一个 group）。
        // `members_per_group`：有界声明的 group 用它自己的 max_count；未声明上限的（例如长期牌组）其成员都是
        // **实体**，因此以 `entities` 限额为上界（派生上界，不是另编一个数字）。
        var entityLimit = _source?.EntityLimit ?? MaxEntities;
        var actionLimit = _source?.LegalActionLimit ?? 256;
        var groupTypes = _profile.GroupTypes;
        var membersPerGroup = DerivedMemberLimit(groupTypes.Select(group => group.MaxCount).ToList(), entityLimit);
        // sequences 同理：`sequences` 的上界 = 声明的 sequence_type 数；`elements_per_sequence` 与 group 同源，
        // 有界声明用它自己的 max_count，未声明上限的元素也都是实体 ⇒ 以 entities 为上界。
        var sequenceTypes = _profile.SequenceTypes;
        var elementsPerSequence =
            DerivedMemberLimit(sequenceTypes.Select(sequence => sequence.MaxCount).ToList(), entityLimit);

        var capabilities = new CapabilitiesDto(
            "simulation",
            // 只声明**实际支持**的操作：不含 attach。
            ["describe", "get_catalog", "start", "observe", "step", "request_status", "close"],
            [new ScenarioProfileDto(
                SupportedScenarioProfile,
                "1",
                _profile.DeclaredIds.Take(8).ToList(),
                _profile.Kinds.Keys.Concat(_profile.Actions.Keys).ToList(),
                // 只登记**当前仍未收口**的约束。已收口的具名缺口（已知位置映射、牌堆实体）不得继续留在这里
                // ——那会让消费者以为该能力不可用（H-01/H-02 的检查已断言这两个 TODO 不再存在）。
                ["完整 V-02 证据待 N-05（隐藏顺序/RNG 游标的真实模拟对照）"],
                "net-the-spire/coverage-n-02.md")],
            SeedControl: true,
            FieldAvailability: new Dictionary<string, string>(StringComparer.Ordinal),
            SupportedStructures:
            [
                new SupportedStructureDto("Entity",
                    PublishedIds(_profile.Kinds.Values.Select(rule => rule.KindId)), []),
                // 关系只登记**已发布**的公开关系：`NotPublished` 的引擎关系在目录里没有 relation_type，
                // 把它写进 declaration_ids 会让 `describe` 引用一个 `get_catalog` 不存在的 ID（I-71/I-84）。
                new SupportedStructureDto("Relation",
                    PublishedIds(_profile.Relations.Values
                        .Select(disposition => disposition.Published)
                        .Where(rule => rule is not null)
                        .Select(rule => rule!.TypeId)), []),
                new SupportedStructureDto("Pile",
                    _profile.PileTypes.Select(pile => pile.PileTypeId).ToList(), []),
                new SupportedStructureDto("Sequence",
                    sequenceTypes.Select(sequence => sequence.SequenceTypeId).ToList(),
                    // 有声明就如实列出；没有声明时说明这是**当前输入**的事实，不是能力上限（W-01 后作者 Profile 会发布）。
                    sequenceTypes.Count == 0 ? ["本规则包未声明公开有序集合（sequence）"] : []),
                new SupportedStructureDto("Group",
                    groupTypes.Select(group => group.GroupTypeId).ToList(),
                    // multiplicity 恒为 1 是**能力边界**（引擎集合成员唯一）；是否声明了 group 由上面的
                    // declaration_ids 表态，不再用一句"引擎无 Group 声明"掩盖（W-01 后作者 Profile 会发布 group）。
                    ["引擎集合成员唯一 ⇒ multiplicity 恒为 1"]),
                // 合法动作 = 引擎动作的公开动作类型 + net 自声明的决策应答类型。`answer_decision` 出现在
                // wire 的 legal_actions 里（暂停期的应答），目录也发布了它，因此必须与目录一致地登记；
                // 缺一条就破坏了「describe 的声明 ID 与同一 get_catalog 闭合」。
                new SupportedStructureDto("LegalAction",
                    PublishedIds(_profile.Actions.Values.Select(rule => rule.ActionTypeId)
                        .Append(_profile.AnswerAction.ActionTypeId)), []),
                new SupportedStructureDto("TerminalResult",
                    PublishedIds(_profile.Terminals.Values.Select(rule => rule.ReasonId)), []),
            ],
            new LimitsDto(
                Entities: entityLimit, EntityFields: 4096, Relations: 4096, Piles: 16,
                VisibleMembersPerPile: 256, KnownPositionsPerPile: 256,
                Sequences: sequenceTypes.Count, ElementsPerSequence: elementsPerSequence,
                Groups: groupTypes.Count, MembersPerGroup: membersPerGroup,
                LegalActions: actionLimit, ActionFields: 1024, FrameBytes: 262144,
                DecimalChars: 40, LedgerEntries: _ledgerCapacity, Sessions: 64),
            new ExecutionGuaranteesDto(
                StableSnapshot: true,
                RequestStatus: true,
                ActiveDeduplication: true,
                DecisionPause: true,
                InternalSnapshotRestore: false,
                SeedActionReplay: "public_trace",
                PublicStateRestore: false,
                PublicFork: false,
                RngProfileId: _source?.RngProfileId ?? RandomService.DefaultDrawProfileId));
        return Ok(request.RequestId, new DescribeResultDto(identity, capabilities).ToWire());
    }

    /// <summary>
    /// `supported_structures.declaration_ids` 的取值：**本目录已发布的公开声明 ID**。
    ///
    /// 映射表的键是**引擎 qualified id**（例如 `base-spire:Player`），它只是映射键；目录里发布的
    /// 公开 ID 是 `net:kind:base-spire:Player` 这类形态（`CatalogProjector` 用 `rule.KindId` 发射）。
    /// 把引擎键当公开 ID 发出去，`describe` 就引用了一批 `get_catalog` 里不存在的 ID（I-84）。
    ///
    /// 去重并按公开 ID 的 UTF-8 字节序排序（与目录声明同一约定），使同一目录无论注册/遍历顺序如何
    /// 都产出同一份声明列表。
    /// </summary>
    private static IReadOnlyList<string> PublishedIds(IEnumerable<string> publicIds) =>
        publicIds.Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, Comparer<string>.Create(WireCanonical.CompareByUtf8Bytes))
            .ToList();

    /// <summary>
    /// 归组类结构的成员/元素上界（group、sequence 共用）：一个也没声明时是 0；有界声明取各自 max_count 的
    /// 最大值；未声明上限的成员都是**实体** ⇒ 以 <paramref name="entityLimit"/> 为上界（派生上界，不另编数字）。
    /// </summary>
    private static long DerivedMemberLimit(IReadOnlyList<int?> declaredMaxCounts, long entityLimit)
    {
        if (declaredMaxCounts.Count == 0) return 0;
        var bounded = declaredMaxCounts.Where(max => max is not null).Select(max => (long)max!.Value).ToList();
        return Math.Max(bounded.Count == 0 ? 0L : bounded.Max(), entityLimit);
    }

    /// <summary>
    /// exact 目录：由运行时声明派生（<see cref="CatalogProjector"/>），身份与本会话一致。
    /// 按 `protocol.md:175`，`catalog_id` 不存在则失败（本后端只服务自己的目录）。
    /// 导出 entity_kinds / content_types / fields / action_types / terminal_reasons / relation_types /
    /// pile_types / sequence_types / group_types（声明了哪个结构就发哪个，空表表示本规则包没声明它）。
    /// </summary>
    private ResponseDto GetCatalog(RequestDto request)
    {
        var catalogId = RequireString(request, "catalog_id", out var error);
        if (catalogId is null)
        {
            return error!;
        }

        if (!string.Equals(catalogId, _profile.CatalogId, StringComparison.Ordinal))
        {
            return Fail(request, "version_mismatch",
                $"catalog_id '{catalogId}' 不存在（本后端目录是 {_profile.CatalogId}）：拒绝，不自动降级",
                "$.catalog_id", retry: "stop", execution: "not_started");
        }

        return Ok(request.RequestId, CatalogProjector.Project(_profile).ToWire());
    }

    // -- start（进程级去重）------------------------------------------------------------------------------------

    private ResponseDto Start(RequestDto request)
    {
        if (_startLedger.TryGetValue(request.RequestId, out var known))
        {
            if (!string.Equals(known.Fingerprint, ControlSession.Fingerprint(request), StringComparison.Ordinal))
            {
                return Fail(request, "request_conflict", "同一 start request_id 被用于不同载荷：修正输入",
                    retry: "correct_input", execution: "not_started");
            }

            // 原样重发：返回原结果，**不启动第二局**。
            return known.Response;
        }

        // protocol.md:176 —— start 必须带 expected_identity 与 scenario_profile；不支持配置则在**启动前**拒绝。
        if (!TryExpectedIdentity(request, out var error))
        {
            return error!;
        }

        var scenarioProfile = RequireString(request, "scenario_profile", out var scenarioError);
        if (scenarioProfile is null)
        {
            return scenarioError!;
        }

        if (!string.Equals(scenarioProfile, SupportedScenarioProfile, StringComparison.Ordinal))
        {
            return Fail(request, "unsupported_capability",
                $"不支持的 scenario_profile '{scenarioProfile}'（本后端只支持 {SupportedScenarioProfile}）",
                "$.scenario_profile", retry: "stop", execution: "not_started");
        }

        if (_source is not null)
        {
            // 正式路径：seed 的形状校验已按协议版本在 RequestDto.FromWire 完成（0.2 是非空游戏字符串，
            // 0.1 仍是规范十进制字符串）；这里只取值，不做 trim/大小写/数值强转 —— 标准化由 base 负责。
            string? requestedSeed = null;
            if (TryMember(request.Payload, "seed", out var seedNode))
            {
                if (seedNode is not WireString seedText)
                {
                    return Fail(request, "invalid_input",
                        NetProtocol.UsesGameSeedString(ProtocolVersion)
                            ? "payload.seed 必须是游戏 seed 字符串"
                            : "payload.seed 必须是十进制整数字符串", "$.seed");
                }

                requestedSeed = seedText.Value;
            }

            ProvidedSession provided;
            try
            {
                // 会话由规则提供方建：已配置、尚未 Start、随机流已种好但一个随机数都没消费。
                // 任何拒绝（未知 seed 形状、不支持的配置）都发生在**创建/启动之前**，不消耗游戏 RNG。
                provided = _source.CreateSession(requestedSeed);
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException
                                                 or InvalidOperationException)
            {
                return Fail(request, "invalid_input",
                    $"规则提供方拒绝该 start（未创建会话、未消耗 RNG）：{exception.Message}",
                    "$.seed", retry: "correct_input", execution: "not_started");
            }

            var formalSessionId = "session-" + _runToken + "-" +
                                  _nextSession.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var formal = new ControlSession(_profile, formalSessionId, provided, _ledgerCapacity);
            _sessions[formalSessionId] = formal;
            _nextSession++;
            var formalResponse = formal.Start(request);
            // 同 request_id 原样重发必须返回**同一局**（同一 session/seed/snapshot），不重新生成 seed。
            _startLedger[request.RequestId] = (ControlSession.Fingerprint(request), formalResponse);
            return formalResponse;
        }

        var seed = _seed;
        if (TryMember(request.Payload, "seed", out var seedNode2))
        {
            // 协议要求 seed 是十进制整数字符串（不是 JSON 整数）；形状错误已由 RequestDto.FromWire 拦下，
            // 这里只做取值；后端声明 seed_control=true，就必须真的使用它。
            if (seedNode2 is WireString seedText && ulong.TryParse(seedText.Value, out var parsedSeed))
            {
                seed = parsedSeed;
            }
            else
            {
                return Fail(request, "invalid_input", "payload.seed 必须是十进制整数字符串", "$.seed");
            }
        }

        var sessionId = "session-" + _runToken + "-" + _nextSession.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var session = new ControlSession(_rules, _profile, sessionId, _participant, _participantRole,
            _projectionId, seed, _ledgerCapacity, _computeBackend);
        _sessions[sessionId] = session;
        _nextSession++;
        var response = session.Start(request);
        _startLedger[request.RequestId] = (ControlSession.Fingerprint(request), response);
        return response;
    }

    /// <summary>`expected_identity` 必须与本运行时身份**完全相等**（exact identity，protocol.md:219）。
    /// 身份里的 protocol_version 也必须匹配：两版本都合法也必须拒绝错版本，不按内容猜版本。</summary>
    private bool TryExpectedIdentity(RequestDto request, out ResponseDto? error)
    {
        error = null;
        if (!TryMember(request.Payload, "expected_identity", out var node))
        {
            error = Fail(request, "invalid_input", "payload.expected_identity 缺失", "$.expected_identity");
            return false;
        }

        IdentityDto identity;
        try
        {
            identity = IdentityDto.FromWire(node, ProtocolVersion);
        }
        catch (WireFaultException fault)
        {
            error = Fail(request, "invalid_input", fault.Message, "$.expected_identity");
            return false;
        }

        var expected = new IdentityDto(ProtocolVersion, _profile.RulesetId, _profile.CatalogId,
            _profile.BackendProfileId);
        if (identity.ProtocolVersion != expected.ProtocolVersion || identity.RulesetId != expected.RulesetId ||
            identity.CatalogId != expected.CatalogId || identity.BackendProfileId != expected.BackendProfileId)
        {
            error = Fail(request, "version_mismatch",
                "expected_identity 与本运行时身份不符：拒绝，不自动降级、不热扩",
                "$.expected_identity", retry: "stop", execution: "not_started");
            return false;
        }

        return true;
    }

    // -- step / request_status ---------------------------------------------------------------------------------

    private ResponseDto Step(RequestDto request)
    {
        if (!TryPayload(request, out var sessionId, out var error))
        {
            return error!;
        }

        if (!_sessions.TryGetValue(sessionId!, out var session))
        {
            return Fail(request, "session_not_found", $"会话不存在：{sessionId}（不自动创建新会话继续旧动作）",
                retry: "stop", execution: "not_started");
        }

        if (session.ReplayIfKnown(request) is { } replayed)
        {
            return replayed;   // 完成的 step 原样重发 → 原结果；换载荷 → request_conflict
        }

        var candidateId = RequireString(request, "candidate_id", out var candidateError);
        if (candidateId is null)
        {
            return candidateError!;
        }

        var expected = RequireInteger(request, "expected_observation_id", out var expectedError);
        if (expected is null)
        {
            return expectedError!;
        }

        return session.Step(request, candidateId, expected.Value);
    }

    private ResponseDto RequestStatus(RequestDto request)
    {
        // 查询 start 时可省略 session_id（`:187`）：先查进程级的 start 账本。
        var target = RequireString(request, "target_request_id", out var targetError);
        if (target is null)
        {
            return targetError!;
        }

        if (TryOptionalString(request, "session_id") is { } sessionId && _sessions.TryGetValue(sessionId, out var session))
        {
            return session.RequestStatus(request, target);
        }

        if (_startLedger.TryGetValue(target, out var start))
        {
            var body = WireValue.Object(
                ("status", WireValue.Str("completed")),
                ("response", start.Response.ToWire()));
            return Ok(request.RequestId, body);
        }

        return Ok(request.RequestId, WireValue.Object(
            ("status", WireValue.Str("not_found")),
            ("response", WireValue.Null)));
    }

    // -- 辅助 --------------------------------------------------------------------------------------------------

    private ResponseDto ForSession(RequestDto request, Func<ControlSession, string, ResponseDto> body)
    {
        if (!TryPayload(request, out var sessionId, out var error))
        {
            return error!;
        }

        if (!_sessions.TryGetValue(sessionId!, out var session))
        {
            return Fail(request, "session_not_found", $"会话不存在：{sessionId}（不自动创建新会话继续旧动作）",
                retry: "stop", execution: "not_started");
        }

        if (session.ReplayIfKnown(request) is { } replayed && request.Operation != Operation.Step)
        {
            return replayed;
        }

        var response = body(session, sessionId!);
        session.Record(request, response);
        return response;
    }

    private static WireValue StringArray(IEnumerable<string> values) =>
        WireValue.Array(values.Select(value => (WireValue)WireValue.Str(value)));

    private ResponseDto Unsupported(RequestDto request, string what, string because) =>
        Fail(request, "unsupported_capability", $"{what} 不受支持：{because}", retry: "stop", execution: "not_started");

    private static ResponseDto Ok(string requestId, WireValue result) => new(requestId, true, result, null);

    private ResponseDto Fail(RequestDto request, string code, string message, string? path = null,
        string retry = "correct_input", string execution = "not_started") =>
        new(request.RequestId, false, null, new ErrorDto(code, message, path, null, null, execution, retry));

    private bool TryPayload(RequestDto request, out string? sessionId, out ResponseDto? error)
    {
        sessionId = RequireString(request, "session_id", out error);
        return sessionId is not null;
    }

    private string? RequireString(RequestDto request, string key, out ResponseDto? error)
    {
        error = null;
        if (TryMember(request.Payload, key, out var value) && value is WireString text && text.Value.Length > 0)
        {
            return text.Value;
        }

        error = Fail(request, "invalid_input", $"payload.{key} 缺失或不是非空字符串", $"$.{key}");
        return null;
    }

    private string? TryOptionalString(RequestDto request, string key) =>
        TryMember(request.Payload, key, out var value) && value is WireString text && text.Value.Length > 0
            ? text.Value
            : null;

    private long? RequireInteger(RequestDto request, string key, out ResponseDto? error)
    {
        error = null;
        if (TryMember(request.Payload, key, out var value) && value is WireInteger integer &&
            long.TryParse(integer.Digits, out var parsed))
        {
            return parsed;
        }

        error = Fail(request, "invalid_input", $"payload.{key} 缺失或不是 JSON 整数", $"$.{key}");
        return null;
    }

    /// <summary><c>WireObject.Members</c> 是键值对列表（不是字典），这里做一次显式查找。</summary>
    private static bool TryMember(WireObject payload, string key, out WireValue value)
    {
        foreach (var member in payload.Members)
        {
            if (string.Equals(member.Key, key, StringComparison.Ordinal))
            {
                value = member.Value;
                return true;
            }
        }

        value = WireValue.Null;
        return false;
    }
}
