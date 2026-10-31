using System.Globalization;
using System.Text;
using CoreTheSpire.Engine;
using NetTheSpire.Contracts;
using NetTheSpire.Contracts.Dto;

namespace NetTheSpire.Projection;

/// <summary>
/// 一次映射的全部输入。唯一观察来源是引擎的 <see cref="ParticipantView"/>（K-06 投影）与运行时给出的合法
/// <see cref="LegalAction"/> / <see cref="DecisionRequest"/> / <see cref="TerminalOutcome"/>；
/// <see cref="RuleSet"/> 只用于查声明（枚举成员名、字段序号），不读状态。
/// </summary>
public sealed record SnapshotInput(
    ParticipantView View,
    IReadOnlyList<LegalAction> LegalActions,
    DecisionRequest? PendingDecision,
    SessionStatus Status,
    TerminalOutcome? Terminal);

/// <summary>
/// 一个已发布候选到引擎内部动作/应答的绑定（design.md §3 的 CandidateBinder 数据）。
///
/// <see cref="CandidateId"/> 只用于 <c>step</c> 的引用，不进特征；它是**本会话**的提交句柄：
/// 由本会话命名空间 + <see cref="SemanticKeyDigest"/> 决定，因此两会话即使同 seed、同观察版本、
/// 同动作语义也不会发出同一个 candidate_id（T03 跨会话候选绑定）。<see cref="SemanticKeyDigest"/>
/// 仍是纯 <c>{action_type, fields}</c> 语义键摘要，跨会话逐字一致，供执行器对齐概率（contract-artifacts §3）。
/// </summary>
public sealed record CandidateBinding(
    string CandidateId,
    string ActionTypeId,
    string SemanticKeyDigest,
    ActionDefinition? Action,
    IReadOnlyList<Value> Arguments,
    long? DecisionRequestId = null,
    InstanceId? DecisionInstance = null,
    bool EmptyDecision = false,
    bool? DecisionFlag = null,
    InstanceId? DecisionSecondInstance = null);

/// <summary>一条**显式**不发布的公开事实：带规则号与理由，用来证明没有静默丢弃。</summary>
public sealed record SkippedPublicFact(string EngineQualifiedId, string RuleId, string Reason);

/// <summary>一次映射的结果：协议 <c>session_status</c>、<c>observation_id</c>、快照（无可执行快照时为 null）与候选绑定。</summary>
public sealed record ProjectedSnapshot(
    string SessionStatus,
    long ObservationId,
    PublicSnapshotDto? Snapshot,
    IReadOnlyList<CandidateBinding> Bindings,
    IReadOnlyList<SkippedPublicFact> Skipped)
{
    /// <summary>
    /// W-02：本次投影的**公开引用绑定表**（引擎实例 → 公开句柄）。会话/观看者内稳定，
    /// 是"公开实体引用"的唯一来源；消费方（宿主、检查、编码器）应从这里取句柄，
    /// **不要**再用引擎创建序号推导。
    /// </summary>
    public IReadOnlyDictionary<InstanceId, string> InstanceRefs { get; init; } =
        new Dictionary<InstanceId, string>();
    /// <summary>协议快照 <c>status</c>；没有快照时为 null。</summary>
    public string? SnapshotStatus => Snapshot?.Status;
}

/// <summary>
/// 从通用运行时的游戏包投影创建 <see cref="PublicSnapshotDto"/>。
///
/// 本层只做映射，不实现游戏规则、不读隐藏状态、不维护第二套历史缓存：
/// <list type="number">
/// <item>实体/字段/关系/牌区全部来自 <see cref="ParticipantView"/>（引擎已按观看者裁剪）；</item>
/// <item>候选来自运行时当前的 <see cref="LegalAction.Bindings"/>（真实可提交的参数元组），并按协议
/// <c>contract-artifacts.md §3</c> 的动作语义键的 UTF-8 字节序排序，<c>candidate_id</c> 不参与排序；</item>
/// <item>暂停期只映射 <see cref="DecisionRequest.Candidates"/> 的合法应答，普通动作不暴露为可执行；</item>
/// <item>每个公开数组都按 protocol.md §2 第 102 行 / contract-artifacts §3 的公开键排序，
/// 使打乱输入顺序不改变输出字节；</item>
/// <item><c>sequences</c>/<c>groups</c> 只在集合明确声明了公开顺序/归组语义时发布，顺序不可见的集合映射成
/// sequence 会泄漏内部顺序（V-02），因此直接拒绝。</item>
/// </list>
///
/// Host 提供当前控制边界的 <c>observation_id</c>；独立投影调用默认使用视图的存储版本。
/// 同一边界重复观察不推进版本，无存储写入的新决策仍由 Host 分配新版本。
/// </summary>
public sealed class PublicSnapshotProjector
{
    private readonly RuleSet _ruleSet;
    private readonly PublicSnapshotProfile _profile;
    private readonly string _sessionId;

    /// <summary>
    /// 本投影所属会话的**候选句柄命名空间令牌**（= 会话句柄的规范摘要，不另行随机）。
    ///
    /// 作用与边界（T03 跨会话候选绑定）：
    /// <list type="bullet">
    /// <item><c>candidate_id</c> 是**提交引用**：只在本会话（本投影）内有效。命名空间令牌来自唯一会话句柄，
    ///       因此同 seed/同观察版本/同动作语义的两局也不会发出同一个 candidate_id；</item>
    /// <item>它**不**进动作语义键摘要、也不进任何公开身份（ruleset/catalog/backend/build id）：训练侧仍按
    ///       <c>{action_type, fields}</c> 对齐概率，换会话不改语义；</item>
    /// <item>公开快照本来就已发布 <c>session_id</c>（协议 0.2），故令牌不泄漏新信息；它只是做完哈希，
    ///       不与内部实例序号/随机数/隐藏状态相关。</item>
    /// </list>
    /// </summary>
    private readonly string _candidateNamespace;

    /// <summary>
    /// W-02：**会话/观看者内持久**的公开引用绑定表（引擎实例 → 不透明句柄）。
    /// 句柄在实例**首次公开出现**时分配，此后跨快照保留：已有实例受伤、升级、被别的实体引用
    /// 或其它实例生成/销毁都不会换绑；绑定的实例销毁后句柄**不再分配给别的实例**。
    /// 表只增不减（引擎的 <see cref="InstanceId"/> 本身会话内不回收），因此它同时是"已用句柄"的记录。
    /// </summary>
    private readonly Dictionary<InstanceId, string> _publicRefs = new();

    /// <summary>下一个未分配的公开句柄序号；只增不减（销毁不复用句柄）。</summary>
    private int _nextPublicHandle = 1;

    public PublicSnapshotProjector(RuleSet ruleSet, PublicSnapshotProfile profile, string sessionId)
    {
        _ruleSet = ruleSet ?? throw new ArgumentNullException(nameof(ruleSet));
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));
        if (string.IsNullOrWhiteSpace(sessionId)) throw new ArgumentException("session_id 不得为空", nameof(sessionId));
        _profile.RequireReservedPileDeclarations();
        _sessionId = sessionId;
        // 唯一会话句柄 ⇒ 唯一命名空间；同一实例内稳定，跨实例（跨局/跨进程）不同。
        _candidateNamespace = TextDigest("candidate-namespace-v1\u001f" + sessionId);
    }

    public PublicSnapshotProfile Profile => _profile;

    /// <summary>引擎会话状态 → 协议 <c>session_status</c>。协议七个状态中只有这四个由引擎产生。</summary>
    public static (string SessionStatus, bool PublishesSnapshot) MapSessionStatus(SessionStatus status) => status switch
    {
        SessionStatus.Running => ("active", true),
        SessionStatus.AwaitingDecision => ("active", true),
        SessionStatus.Terminal => ("terminal", true),
        SessionStatus.Faulted => ("faulted", false),
        _ => throw new PublicMappingException("N-02-SESSION-STATUS",
            $"引擎会话状态 {status} 没有协议 session_status 映射（protocol.md §5 第 193 行）"),
    };

    /// <summary>
    /// 引擎实例序号的确定性函数。**W-02 之后它不再是公开句柄**：公开句柄只由
    /// <see cref="ProjectedSnapshot.InstanceRefs"/> 的会话/观看者绑定表给出（见 <see cref="BuildInstanceTable"/>）。
    /// 本方法保留仅用于本层的诊断文本；任何 wire 值、排序键或特征都不得使用它——它直接透传内部创建序号。
    /// </summary>
    public static string PublicInstanceId(InstanceId instance) =>
        "e" + instance.Value.ToString(CultureInfo.InvariantCulture);

    public ProjectedSnapshot Project(SnapshotInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.View);
        return Project(input, input.View.StateVersion);
    }

    internal ProjectedSnapshot Project(SnapshotInput input, long observationId)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.View);
        ArgumentNullException.ThrowIfNull(input.LegalActions);

        var view = input.View;
        var (sessionStatus, _) = MapSessionStatus(input.Status);
        if (observationId < 0 || observationId > WireCanonical.SafeIntegerMax)
            throw new PublicMappingException("N-03-OBSERVATION-VERSION", "观察版本必须是非负安全整数");

        if (input.Status == SessionStatus.Faulted)
        {
            if (input.Terminal is not null)
                throw new PublicMappingException("N-02-SESSION-STATUS", "faulted 会话不得同时带终局结果");
            return new ProjectedSnapshot(sessionStatus, observationId, null, [], []);
        }

        if ((input.Terminal is not null) != (input.Status == SessionStatus.Terminal))
            throw new PublicMappingException("N-02-SESSION-STATUS",
                "终局结果与会话状态不一致：protocol.md:60/190 要求 terminal 会话带终止快照");

        if (input.Status == SessionStatus.AwaitingDecision && input.PendingDecision is null)
            throw new PublicMappingException("N-02-DECISION-STATE", "AwaitingDecision 必须带当前 DecisionRequest");

        var ids = BuildInstanceTable(view);
        var skipped = new List<SkippedPublicFact>();
        var entities = BuildEntities(view, ids, skipped);
        var entitiesByPublicId = entities.ToDictionary(entity => entity.InstanceId, entity => entity.KindId,
            StringComparer.Ordinal);
        // protocol.md:83 的唯一匹配：每个声明的 pile kind 在本快照里必须恰好有一个实例。
        // W-01：group 与 sequence 的 owner 也只能按**种类**唯一解析（公开句柄由会话内绑定决定，
        // 不能写死在 Profile 里）；写死公开引用的旧 0.1 sequence 路径不参与本表。
        var pileKinds = _profile.PileTypes.Select(pile => pile.PileKindId).ToHashSet(StringComparer.Ordinal);
        var groupOwnerKinds = _profile.GroupTypes.Select(group => group.OwnerKindId)
            .ToHashSet(StringComparer.Ordinal);
        var sequenceOwnerKinds = _profile.SequenceTypes
            .Select(sequence => sequence.OwnerKindId)
            .Where(kind => kind is not null)
            .Select(kind => kind!)
            .ToHashSet(StringComparer.Ordinal);
        var ownerKinds = new HashSet<string>(groupOwnerKinds, StringComparer.Ordinal);
        ownerKinds.UnionWith(sequenceOwnerKinds);
        var uniqueKinds = new HashSet<string>(pileKinds, StringComparer.Ordinal);
        uniqueKinds.UnionWith(ownerKinds);
        var entitiesByKind = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entity in entities.Where(entity => uniqueKinds.Contains(entity.KindId)))
        {
            if (entitiesByKind.TryAdd(entity.KindId, entity.InstanceId)) continue;
            if (pileKinds.Contains(entity.KindId))
            {
                throw new PublicMappingException("I-07",
                    $"pile kind '{entity.KindId}' 在本快照里有多个实例，pile_ref 无法唯一匹配 pile_type",
                    $"snapshot.entities[{entity.InstanceId}]");
            }

            var block = sequenceOwnerKinds.Contains(entity.KindId) && !groupOwnerKinds.Contains(entity.KindId)
                ? "sequence"
                : "group";
            throw new PublicMappingException(block == "sequence" ? "N-02-SEQUENCE-OWNER" : "N-02-GROUP-OWNER",
                $"{block} owner kind '{entity.KindId}' 在本快照里有多个实例，owner_ref 无法唯一确定（不猜一个）",
                $"snapshot.entities[{entity.InstanceId}]");
        }

        var relations = BuildRelations(view, ids, skipped);
        var pileRules = new Dictionary<string, PublishedPile>(StringComparer.Ordinal);
        var (piles, sequences, groups) = BuildCollections(view, ids, entitiesByPublicId, entitiesByKind, skipped,
            pileRules);
        piles = AttachKnownPositions(piles, view, ids, pileRules);
        var terminal = input.Terminal is null ? null : BuildTerminal(input.Terminal);

        List<LegalActionDto> actions;
        List<CandidateBinding> bindings;
        if (input.Status == SessionStatus.Terminal)
        {
            actions = [];
            bindings = [];
        }
        else if (input.PendingDecision is { } pending)
        {
            (actions, bindings) = BuildDecisionAnswers(pending, ids);
        }
        else
        {
            (actions, bindings) = BuildLegalActions(input.LegalActions, ids);
        }

        var status = input.Status == SessionStatus.Terminal ? "terminal" : "decision";
        if (status == "decision")
        {
            if (terminal is not null)
                throw new PublicMappingException("N-01-DTO", "decision 快照的 terminal_result 必须为 null",
                    "snapshot.terminal_result");
            if (actions.Count == 0)
                throw new PublicMappingException("TODO-N02-EMPTY-CANDIDATES",
                    "status=decision 要求候选非空（protocol.md:60）。当前稳定边界没有任何可执行候选：" +
                    "不得发布一个空候选的 decision 快照，由协调者按 faulted/uncertain 处理。", "snapshot.legal_actions");
        }
        else if (actions.Count != 0)
        {
            throw new PublicMappingException("N-01-DTO", "terminal 快照的候选必须为空", "snapshot.legal_actions");
        }

        var snapshot = new PublicSnapshotDto(
            _profile.ProtocolVersion,
            _profile.RulesetId,
            _profile.CatalogId,
            _profile.BackendProfileId,
            _sessionId,
            observationId,
            status,
            terminal,
            entities,
            relations,
            piles,
            sequences,
            groups,
            actions);

        return new ProjectedSnapshot(sessionStatus, observationId, snapshot, bindings, skipped)
        {
            InstanceRefs = ids,
        };
    }

    // -- 实体 ----------------------------------------------------------------------------------------------------

    /// <summary>
    /// W-02：**会话/观看者内持久**的公开句柄绑定。
    ///
    /// 规则（对应验收条件，逐条可核对）：
    /// <list type="number">
    /// <item><b>首次公开分配</b>：实例第一次进入本观看者的投影时分配一个不透明句柄；</item>
    /// <item><b>跨状态保留</b>：句柄此后永不改变——公开字段变化（受伤/升级/被引用/位置变化）不换绑；</item>
    /// <item><b>销毁不复用</b>：句柄序号只增不减，已销毁实例的句柄不会分配给任何后来的实例；</item>
    /// <item><b>不可见创建不消费公开序号</b>：只有出现在本观看者投影里的实例才会被编号，
    /// 隐藏实例（以及它在内部创建时占用的 <see cref="InstanceId"/>）不影响公开编号；</item>
    /// <item><b>不按内部序号排序</b>：同一批新实例之间的编号顺序只由 <see cref="PublicIdentityKey"/> 决定——
    /// 它只读**已发布**字段，且引用按对端的**公开身份**（种类 + 内容标识）表达，既不读 Skip 字段，
    /// 也不使用 <see cref="Value.ToString"/>（后者对 Ref/RefList 会打印内部实例序号）。</item>
    /// </list>
    ///
    /// 返回的表只含**本快照可见**的实例：句柄本身跨状态保留，但引用闭合仍按本快照判定
    /// （引用一个已销毁或被隐藏的实例仍按 I-22 拒绝，而不是被这张表悄悄放行）。
    ///
    /// 残余边界：公开身份完全相同的同定义实例之间，编号顺序只能由投影顺序（稳定排序）决定。
    /// 这类实例的任何置换都给出同一公开结构，故不构成可推断的泄漏；一旦某类内容出现
    /// “公开字段完全相同、隐藏字段不同”的实例，需要重新评估（已登记为 W-02 残余风险）。
    /// </summary>
    private Dictionary<InstanceId, string> BuildInstanceTable(ParticipantView view)
    {
        var visible = new Dictionary<InstanceId, ProjectedInstance>();
        foreach (var instance in view.Instances)
        {
            if (instance.Id.IsNone)
                throw new PublicMappingException("N-02-INSTANCE-ID", "投影里出现 <none> 实例引用");
            if (!visible.TryAdd(instance.Id, instance))
                throw new PublicMappingException("N-02-INSTANCE-ID", $"投影里重复的实例 {instance.Id}");
        }

        // 只给**本次首次公开**的实例分配编号；其余实例沿用既有绑定（不换绑）。
        var fresh = view.Instances.Where(instance => !_publicRefs.ContainsKey(instance.Id)).ToList();
        if (fresh.Count > 0)
        {
            var ordered = fresh
                .OrderBy(instance => instance.TypeId, StringComparer.Ordinal)
                .ThenBy(instance => PublicIdentityKey(instance, visible), StringComparer.Ordinal)
                .ToList();
            foreach (var instance in ordered)
            {
                _publicRefs[instance.Id] = "e" + _nextPublicHandle.ToString(CultureInfo.InvariantCulture);
                _nextPublicHandle++;
            }
        }

        var ids = new Dictionary<InstanceId, string>();
        foreach (var instance in view.Instances)
        {
            ids[instance.Id] = _publicRefs[instance.Id];
        }

        return ids;
    }

    /// <summary>
    /// W-02：一个新实例的**公开身份键**，只用于决定同批新实例之间的编号顺序。
    /// 只读 kind 声明为"已发布"的字段（Skip 字段——内部簿记、观看者绑定、重复表达的字段——不参与），
    /// 引用值按对端的公开身份表达，因此既不泄漏内部创建序号，也不会因公开字段的重排而换绑。
    /// </summary>
    private string PublicIdentityKey(ProjectedInstance instance,
        IReadOnlyDictionary<InstanceId, ProjectedInstance> visible)
    {
        var kind = _profile.RequireKind(instance.TypeId);
        var key = new StringBuilder();
        if (kind.ContentFieldName is not null)
        {
            var content = instance.Fields.FirstOrDefault(field => field.Name == kind.ContentFieldName);
            // 内容标识由 entity.content_state/content_id 发布，是公开事实。
            key.Append("content=").Append(content is null ? "<none>" : PlainToken(content.Value)).Append('\u001f');
        }

        foreach (var field in instance.Fields.OrderBy(field => field.Name, StringComparer.Ordinal))
        {
            // 未声明的字段在这里跳过：BuildEntities 会用 N-02-UNDECLARED-FIELD 显式拒绝，不靠排序兜底。
            if (!kind.Fields.TryGetValue(field.Name, out var disposition)) continue;
            if (disposition.Published is null) continue;
            key.Append(field.Name).Append('=').Append(IdentityToken(field.Value, visible)).Append('\u001f');
        }

        return key.ToString();
    }

    /// <summary>
    /// 公开身份里的一个值。引用**不**打印内部序号（<see cref="Value.ToString"/> 会打印），
    /// 而是按对端的公开身份（种类 + 内容标识）表达。
    /// </summary>
    private string IdentityToken(Value value, IReadOnlyDictionary<InstanceId, ProjectedInstance> visible) =>
        value.Kind switch
        {
            ValueKind.Ref => ReferenceToken(value.AsRef(), visible),
            ValueKind.RefList => "[" + string.Join(",", value.AsRefList()
                .Select(reference => ReferenceToken(reference, visible))
                .OrderBy(token => token, StringComparer.Ordinal)) + "]",
            ValueKind.Int => "i:" + value.AsInt().ToString(CultureInfo.InvariantCulture),
            ValueKind.Fixed => "f:" + value.AsDecimal().ToString(CultureInfo.InvariantCulture),
            ValueKind.Bool => value.AsBool() ? "b:true" : "b:false",
            ValueKind.String => "s:" + value.AsString(),
            ValueKind.Enum => "e:" + value.AsEnumOrdinal().ToString(CultureInfo.InvariantCulture),
            ValueKind.Participant => "p:" + value.AsParticipant().Value.ToString(CultureInfo.InvariantCulture),
            ValueKind.Scope => "c:" + value.AsScope().Value.ToString(CultureInfo.InvariantCulture),
            ValueKind.IntList => "[" + string.Join(",", value.AsIntList()) + "]",
            _ => "<none>",
        };

    /// <summary>引用在公开身份里的表达：对端的种类 + 内容标识（不递归，故无环、与创建序号无关）。</summary>
    private string ReferenceToken(InstanceId reference, IReadOnlyDictionary<InstanceId, ProjectedInstance> visible)
    {
        if (reference.IsNone) return "<none>";
        // 投影只发布观看者可识别的引用目标（ProjectionObserver.IsIdentifiable），因此这里总能在本快照找到它。
        if (!visible.TryGetValue(reference, out var target)) return "<unresolved>";
        var contentName = _profile.RequireKind(target.TypeId).ContentFieldName;
        if (contentName is null) return target.TypeId;
        var content = target.Fields.FirstOrDefault(field => field.Name == contentName);
        return target.TypeId + "|" + (content is null ? "<none>" : PlainToken(content.Value));
    }

    /// <summary>标量值的字面量（不递归、不解引用）：只用于内容标识这一类公开字符串。</summary>
    private static string PlainToken(Value value) => value.Kind switch
    {
        ValueKind.String => value.AsString(),
        ValueKind.Int => value.AsInt().ToString(CultureInfo.InvariantCulture),
        ValueKind.Fixed => value.AsDecimal().ToString(CultureInfo.InvariantCulture),
        ValueKind.Bool => value.AsBool() ? "true" : "false",
        ValueKind.Enum => "enum#" + value.AsEnumOrdinal().ToString(CultureInfo.InvariantCulture),
        _ => "<" + value.Kind + ">",
    };

    private List<EntityDto> BuildEntities(ParticipantView view, IReadOnlyDictionary<InstanceId, string> ids,
        List<SkippedPublicFact> skipped)
    {
        var entities = new List<EntityDto>();
        foreach (var instance in view.Instances)
        {
            var kind = _profile.RequireKind(instance.TypeId);
            var type = _ruleSet.TypeByQualifiedId(instance.TypeId);
            var publicId = ids[instance.Id];
            var projected = instance.Fields.ToDictionary(field => field.Name, StringComparer.Ordinal);

            string contentState;
            string? contentId = null;
            if (kind.ContentFieldName is null)
            {
                contentState = "not_applicable";
            }
            else
            {
                if (!projected.TryGetValue(kind.ContentFieldName, out var contentField))
                    throw new PublicMappingException("N-02-CONTENT",
                        $"{instance.TypeId}.{kind.ContentFieldName} 未出现在投影里，无法确定 content_state");
                var raw = RequireString(contentField.Value, $"{instance.TypeId}.{kind.ContentFieldName}");
                if (!kind.ContentIds.Contains(raw, StringComparer.Ordinal))
                    throw new PublicMappingException("N-02-UNDECLARED-CONTENT",
                        $"{instance.TypeId}.{kind.ContentFieldName} 的值 '{raw}' 未在公开表登记");
                contentState = "known";
                contentId = raw;
                skipped.Add(new SkippedPublicFact($"{instance.TypeId}.{kind.ContentFieldName}", "N-02-CONTENT",
                    "内容标识由 content_state/content_id 表达（protocol.md:62/66）"));
            }

            var fields = new List<FieldDto>();
            foreach (var field in instance.Fields)
            {
                if (!kind.Fields.TryGetValue(field.Name, out var disposition))
                    throw new PublicMappingException("N-02-UNDECLARED-FIELD",
                        $"{instance.TypeId}.{field.Name} 没有公开映射处置（不允许静默丢弃公开字段）");
                if (disposition.Published is null)
                {
                    skipped.Add(new SkippedPublicFact($"{instance.TypeId}.{field.Name}", disposition.SkipRuleId!,
                        disposition.SkipReason!));
                    continue;
                }

                var declared = disposition.Published;
                var coreField = type.TryFieldIndex(field.Name, out var index) ? type.Fields[index] : null;
                if (coreField is { Optional: true } && field.Value.Kind == ValueKind.None && !declared.Required)
                {
                    skipped.Add(new SkippedPublicFact($"{instance.TypeId}.{field.Name}", "N-02-OPTIONAL-ABSENT",
                        "可选Engine字段当前为None，按字段可选契约省略；不编码成零、空引用或字段not_applicable。"));
                    continue;
                }
                var enumTypeId = coreField?.EnumTypeId ?? -1;
                fields.Add(EncodeField(declared, field.Value, enumTypeId, ids,
                    $"{instance.TypeId}.{field.Name}", $"snapshot.entities[{publicId}].fields[{declared.FieldId}]"));
            }

            // required 且适用的已发布字段不能不出现（protocol.md:65）。
            foreach (var (name, disposition) in kind.Fields)
            {
                if (disposition.Published is not { Required: true } declared) continue;
                if (projected.ContainsKey(name)) continue;
                throw new PublicMappingException("I-22",
                    $"{instance.TypeId}.{name} 是 required 的已发布字段，但投影里没有它（不得静默省略）",
                    $"snapshot.entities[{publicId}].fields[{declared.FieldId}]");
            }

            entities.Add(new EntityDto(publicId, kind.KindId, contentState, SortFields(fields), contentId));
        }

        entities.Sort((left, right) => WireCanonical.CompareByUtf8Bytes(left.InstanceId, right.InstanceId));
        return entities;
    }

    private static List<FieldDto> SortFields(IEnumerable<FieldDto> fields)
    {
        var sorted = fields.ToList();
        sorted.Sort((left, right) => WireCanonical.CompareByUtf8Bytes(left.FieldId, right.FieldId));
        return sorted;
    }

    // -- 关系 ----------------------------------------------------------------------------------------------------

    private List<RelationDto> BuildRelations(ParticipantView view, IReadOnlyDictionary<InstanceId, string> ids,
        List<SkippedPublicFact> skipped)
    {
        var relations = new List<RelationDto>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var relation in view.Relations)
        {
            var disposition = _profile.RequireRelation(relation.QualifiedId);
            if (disposition.Published is null)
            {
                skipped.Add(new SkippedPublicFact(relation.QualifiedId, disposition.SkipRuleId!,
                    disposition.SkipReason!));
                continue;
            }

            var source = RequirePublicId(ids, relation.Owner, "I-22");
            foreach (var target in relation.Targets)
            {
                var item = new RelationDto(disposition.Published.TypeId, source,
                    RequirePublicId(ids, target, "I-22"), null);
                if (!seen.Add(item.TypeId + "|" + item.SourceRef + "|" + item.TargetRef))
                    throw new PublicMappingException("I-38",
                        $"重复关系 {item.TypeId} {item.SourceRef}->{item.TargetRef}（protocol.md:79 要求拒绝）",
                        $"snapshot.relations[{item.TypeId}/{item.SourceRef}/{item.TargetRef}]");
                relations.Add(item);
            }
        }

        relations.Sort(CompareRelations);
        return relations;
    }

    private static int CompareRelations(RelationDto left, RelationDto right)
    {
        var result = WireCanonical.CompareByUtf8Bytes(left.TypeId, right.TypeId);
        if (result != 0) return result;
        result = WireCanonical.CompareByUtf8Bytes(left.SourceRef, right.SourceRef);
        if (result != 0) return result;
        return WireCanonical.CompareByUtf8Bytes(left.TargetRef, right.TargetRef);
    }

    // -- 牌区：piles / sequences / groups ------------------------------------------------------------------------

    private (List<PileDto> Piles, List<SequenceDto> Sequences, List<GroupDto> Groups) BuildCollections(
        ParticipantView view, IReadOnlyDictionary<InstanceId, string> ids,
        IReadOnlyDictionary<string, string> entitiesByPublicId,
        IReadOnlyDictionary<string, string> entitiesByKind, List<SkippedPublicFact> skipped,
        Dictionary<string, PublishedPile> pileRules)
    {
        var piles = new List<PileDto>();
        var sequences = new List<SequenceDto>();
        var groups = new List<GroupDto>();
        foreach (var collection in view.Collections)
        {
            var disposition = _profile.RequireCollection(collection.QualifiedId);
            if (!disposition.IsPublished)
            {
                skipped.Add(new SkippedPublicFact(collection.QualifiedId, disposition.SkipRuleId!,
                    disposition.SkipReason!));
                continue;
            }

            // 隐藏成员永远不得进入 visible_members（V-02）：Members 只在身份可见时才非空；观看者被授权识别的成员
            // 走引擎的 RevealedMembers，两者都在发布前逐条校验引用闭合与成员种类。
            if (!collection.IdentityVisible && collection.Members.Count > 0)
                throw new PublicMappingException("N-02-HIDDEN-MEMBER-LEAK",
                    $"集合 '{collection.QualifiedId}' 声明身份不可见却给出了 {collection.Members.Count} 个成员；" +
                    "隐藏成员不得进入公开数组", $"snapshot.{disposition.BlockName}[{collection.QualifiedId}]");

            if (disposition.Pile is { } pile)
            {
                var dto = BuildPile(collection, pile, ids, entitiesByPublicId, entitiesByKind);
                pileRules[dto.PileRef] = pile;
                piles.Add(dto);
            }
            else if (disposition.Sequence is { } sequence)
            {
                sequences.Add(BuildSequence(collection, sequence, ids, entitiesByPublicId, entitiesByKind));
            }
            else if (disposition.Group is { } group)
            {
                groups.Add(BuildGroup(collection, group, ids, entitiesByPublicId, entitiesByKind));
            }
        }

        piles.Sort((left, right) => WireCanonical.CompareByUtf8Bytes(left.PileRef, right.PileRef));
        sequences.Sort(CompareTypeOwner);
        groups.Sort(CompareTypeOwner);
        return (piles, sequences, groups);
    }

    private PileDto BuildPile(ProjectedCollection collection, PublishedPile rule,
        IReadOnlyDictionary<InstanceId, string> ids, IReadOnlyDictionary<string, string> entitiesByPublicId,
        IReadOnlyDictionary<string, string> entitiesByKind)
    {
        // pile_ref 由声明的 kind_id 唯一确定（protocol.md:83）；找不到即拒绝，不自造 id。
        if (!entitiesByKind.TryGetValue(rule.PileKindId, out var pileRef))
        {
            throw new PublicMappingException("I-26",
                $"集合 '{collection.QualifiedId}' 声明的牌堆实体 kind_id '{rule.PileKindId}' 不在本快照的实体里；" +
                "不得自造 pile_ref", $"snapshot.piles[{rule.PileKindId}]");
        }

        RequireBoundEntity(collection.QualifiedId, pileRef, rule.PileKindId, entitiesByPublicId,
            "I-26", $"snapshot.piles[{pileRef}]");
        if (collection.Size is not { } total)
            throw new PublicMappingException("I-26",
                $"集合 '{collection.QualifiedId}' 的 size 不可见；protocol.md:83 的 total_count 是必需键，" +
                "不得用成员数顶替真实总数", $"snapshot.piles[{pileRef}].total_count");

        var path = $"snapshot.piles[{pileRef}]";
        var members = ListMembers(collection, rule.MemberKindIds, ids, entitiesByPublicId, "I-28", path);
        // 观看者被授权识别的成员（H-01）也是真实成员：它们必须出现在 visible_members 里，
        // 否则 known_positions 的 card_ref ⊆ visible_members（I-29）无法成立。
        // An explicitly remembered card can also belong to a fully visible pile.
        // Validate each source independently, then publish their set union. This
        // preserves duplicate rejection within a source without double-counting
        // a legitimate overlap between ordinary and authorized visibility.
        var revealed = ListMembers(collection, rule.MemberKindIds, ids, entitiesByPublicId, "I-28", path,
            collection.RevealedMembers);
        members = members.Union(revealed, StringComparer.Ordinal).ToList();
        if (members.Count > total)
            throw new PublicMappingException("I-28",
                $"'{collection.QualifiedId}' 的可见成员 {members.Count} 超过 total_count {total}",
                $"{path}.visible_members");
        var complete = collection.IdentityVisible;
        if (complete && members.Count != total)
            throw new PublicMappingException("I-28",
                $"'{collection.QualifiedId}' 声称成员完整（IdentityVisible）但成员数 {members.Count} " +
                $"与 total_count {total} 不等", $"{path}.members_complete");
        members.Sort(WireCanonical.CompareByUtf8Bytes);

        return new PileDto(pileRef, total, members, complete, []);
    }

    /// <summary>
    /// 把包声明的已知位置事实（H-01/H-02）填入对应牌堆。位置与牌堆都由包维护；映射层只读当前快照，
    /// 不重算、不补造，并逐条检查 I-27（位置边界）与 I-29（card_ref ∈ visible_members、位置与卡牌一一对应）。
    /// </summary>
    private List<PileDto> AttachKnownPositions(List<PileDto> piles, ParticipantView view,
        IReadOnlyDictionary<InstanceId, string> ids,
        IReadOnlyDictionary<string, PublishedPile> pileRules)
    {
        if (_profile.KnownPositions.Count == 0) return piles;

        // 位置事实 → (pile_type, card_ref, position)。同一事实类的所有实例一起校验。
        var raw = new Dictionary<string, List<(string CardRef, long Position)>>(StringComparer.Ordinal);
        foreach (var rule in _profile.KnownPositions.Values)
        {
            foreach (var instance in view.Instances.Where(i => i.TypeId == rule.FactKindId))
            {
                var fields = instance.Fields.ToDictionary(field => field.Name, StringComparer.Ordinal);
                var where = $"snapshot.entities[{ids[instance.Id]}]";
                if (!fields.TryGetValue(rule.CardFieldName, out var card) ||
                    !fields.TryGetValue(rule.PileFieldName, out var token) ||
                    !fields.TryGetValue(rule.PositionFieldName, out var position))
                {
                    throw new PublicMappingException("H-01-KNOWN-FACT",
                        $"已知位置事实 '{instance.Id}' 缺少 {rule.CardFieldName}/{rule.PileFieldName}/" +
                        $"{rule.PositionFieldName} 字段；不得跳过或补默认值", where);
                }

                if (token.Value.Kind != ValueKind.String)
                    throw new PublicMappingException("H-01-KNOWN-FACT",
                        $"已知位置事实 '{instance.Id}' 的牌堆标记不是字符串", where);
                if (position.Value.Kind != ValueKind.Int)
                    throw new PublicMappingException("H-01-KNOWN-FACT",
                        $"已知位置事实 '{instance.Id}' 的位置不是整数", where);
                var cardRef = RequirePublicId(ids, card.Value, "I-29",
                    $"已知位置事实 {instance.Id} 的 {rule.CardFieldName}", where);
                var tokenText = token.Value.AsString();
                if (!rule.PileTypeByToken.TryGetValue(tokenText, out var pileTypeId))
                {
                    throw new PublicMappingException("H-01-KNOWN-FACT",
                        $"牌堆标记 '{tokenText}' 没有声明对应的 pile_type；不得猜测或丢弃", where);
                }

                if (!raw.TryGetValue(pileTypeId, out var list)) raw[pileTypeId] = list = [];
                list.Add((cardRef, position.Value.AsInt()));
            }
        }

        if (raw.Count == 0) return piles;

        var result = new List<PileDto>(piles.Count);
        foreach (var pile in piles)
        {
            if (!pileRules.TryGetValue(pile.PileRef, out var rule) || !raw.TryGetValue(rule.PileTypeId, out var entries))
            {
                result.Add(pile);
                continue;
            }

            var path = $"snapshot.piles[{pile.PileRef}].known_positions";
            var positions = new List<CardPositionDto>();
            var cards = new HashSet<string>(StringComparer.Ordinal);
            var slots = new HashSet<long>();
            foreach (var (cardRef, position) in entries.OrderBy(entry => entry.CardRef, StringComparer.Ordinal))
            {
                if (position < 0 || position >= pile.TotalCount)
                    throw new PublicMappingException("I-27",
                        $"已知位置 {position} 越界（total_count {pile.TotalCount}）", $"{path}[{cardRef}].position");
                if (!pile.VisibleMembers.Contains(cardRef, StringComparer.Ordinal))
                    throw new PublicMappingException("I-29",
                        $"已知位置的 card_ref '{cardRef}' 不是 visible_members 的子集", $"{path}[{cardRef}]");
                if (!cards.Add(cardRef))
                    throw new PublicMappingException("I-29", $"同一张牌出现多个位置记录: {cardRef}", path);
                if (!slots.Add(position))
                    throw new PublicMappingException("I-29", $"同一个位置出现多张牌: {position}", path);
                positions.Add(new CardPositionDto(cardRef, position));
            }

            result.Add(pile with { KnownPositions = positions });
        }

        return result;
    }

    private SequenceDto BuildSequence(ProjectedCollection collection, PublishedSequence rule,
        IReadOnlyDictionary<InstanceId, string> ids, IReadOnlyDictionary<string, string> entitiesByPublicId,
        IReadOnlyDictionary<string, string> entitiesByKind)
    {
        // 顺序不可见的集合映射成 sequence 就是把内部顺序泄漏成公开信息（V-02）。
        if (!collection.OrderVisible || !collection.IdentityVisible)
            throw new PublicMappingException("N-02-ORDER-LEAK",
                $"集合 '{collection.QualifiedId}' 的顺序不可见（OrderVisible={collection.OrderVisible}, " +
                $"IdentityVisible={collection.IdentityVisible}），不能发布为 protocol sequence：elements 的 ordinal " +
                "是公开游戏语义（protocol.md:96/102）", $"snapshot.sequences[{rule.SequenceTypeId}]");

        // owner 有两种声明形状：正式路径按**公开种类**在本快照里唯一解析（句柄由会话内绑定决定，
        // 不写死每局句柄；找不到唯一实例就拒绝，绝不猜一个），旧 0.1 路径仍是写死的公开引用。
        string ownerRef;
        if (rule.OwnerKindId is { } ownerKind)
        {
            if (!entitiesByKind.TryGetValue(ownerKind, out var resolved))
            {
                throw new PublicMappingException("N-02-SEQUENCE-OWNER",
                    $"集合 '{collection.QualifiedId}' 声明的 owner kind '{ownerKind}' 在本快照里没有唯一的可见实例",
                    $"snapshot.sequences[{rule.SequenceTypeId}].owner_ref");
            }

            ownerRef = resolved;
            RequireBoundEntity(collection.QualifiedId, ownerRef, ownerKind, entitiesByPublicId,
                "N-02-SEQUENCE-OWNER", $"snapshot.sequences[{rule.SequenceTypeId}].owner_ref");
        }
        else
        {
            ownerRef = rule.OwnerPublicRef!;
            RequireBoundEntity(collection.QualifiedId, ownerRef, null, entitiesByPublicId,
                "N-02-SEQUENCE-OWNER", $"snapshot.sequences[{rule.SequenceTypeId}].owner_ref");
        }

        var elements = ListMembers(collection, rule.ElementKindIds, ids, entitiesByPublicId, "N-02-SEQUENCE-CLOSURE",
            $"snapshot.sequences[{rule.SequenceTypeId}]");
        // Sequence.elements 保留真实公开顺序（protocol.md:102），不排序。
        return new SequenceDto(rule.SequenceTypeId, ownerRef, elements);
    }

    private GroupDto BuildGroup(ProjectedCollection collection, PublishedGroup rule,
        IReadOnlyDictionary<InstanceId, string> ids, IReadOnlyDictionary<string, string> entitiesByPublicId,
        IReadOnlyDictionary<string, string> entitiesByKind)
    {
        // W-01：身份不可见的集合不能发布为 group——那样只会发布一个空成员表，而真实成员是隐藏的
        // （"公布的集合"与"真实成员"必须一致；需要隐藏成员时不得用 group 表达）。
        if (!collection.IdentityVisible && collection.Members.Count > 0)
        {
            throw new PublicMappingException("N-02-GROUP-IDENTITY",
                $"集合 '{collection.QualifiedId}' 声明身份不可见却给出了 {collection.Members.Count} 个成员；" +
                "group 的 members 是真实成员，不能只发布一个空表",
                $"snapshot.groups[{rule.GroupTypeId}]");
        }

        // W-01：owner 不是写死的公开引用（句柄由会话内绑定决定），而是"该种类的唯一可见实例"。
        // 找不到唯一实例就拒绝发布，绝不猜一个 owner。
        if (!entitiesByKind.TryGetValue(rule.OwnerKindId, out var ownerRef))
        {
            throw new PublicMappingException("N-02-GROUP-OWNER",
                $"集合 '{collection.QualifiedId}' 声明的 owner kind '{rule.OwnerKindId}' 在本快照里没有唯一的可见实例",
                $"snapshot.groups[{rule.GroupTypeId}].owner_ref");
        }

        RequireBoundEntity(collection.QualifiedId, ownerRef, rule.OwnerKindId, entitiesByPublicId,
            "N-02-GROUP-OWNER", $"snapshot.groups[{rule.GroupTypeId}].owner_ref");
        var members = ListMembers(collection, rule.MemberKindIds, ids, entitiesByPublicId, "N-02-GROUP-CLOSURE",
            $"snapshot.groups[{rule.GroupTypeId}]");
        members.Sort(WireCanonical.CompareByUtf8Bytes);
        // 容量/槽位是**公开事实**（引擎已在越界时拒绝），因此这里对声明的范围自检，
        // 不把"本该表达的范围"留给下游去猜（L3 会再核对一次 wire）。
        if (members.Count < rule.MinCount || (rule.MaxCount is { } maximum && members.Count > maximum))
        {
            throw new PublicMappingException("I-35",
                $"集合 '{collection.QualifiedId}' 的可见成员数 {members.Count} 不在声明范围 " +
                $"[{rule.MinCount}..{(rule.MaxCount?.ToString(CultureInfo.InvariantCulture) ?? "unbounded")}] 内",
                $"snapshot.groups[{rule.GroupTypeId}].members");
        }

        // 引擎集合的成员唯一，只能表达 multiplicity=1；带重数的分组在引擎里无法表达（能力边界）。
        return new GroupDto(rule.GroupTypeId, ownerRef,
            members.Select(member => new GroupMemberDto(member, 1)).ToList());
    }

    private static List<string> ListMembers(ProjectedCollection collection, IReadOnlyList<string> memberKindIds,
        IReadOnlyDictionary<InstanceId, string> ids, IReadOnlyDictionary<string, string> entitiesByPublicId,
        string ruleId, string path, IReadOnlyList<InstanceId>? membersOverride = null)
    {
        var members = new List<string>();
        foreach (var member in membersOverride ?? collection.Members)
        {
            var reference = RequirePublicId(ids, member, ruleId, path);
            if (members.Contains(reference, StringComparer.Ordinal))
                throw new PublicMappingException("I-28",
                    $"成员引用 '{reference}' 在本块重复（protocol.md:88/96 要求唯一）", path);
            if (!entitiesByPublicId.TryGetValue(reference, out var kind))
                throw new PublicMappingException("I-28",
                    $"成员 '{reference}' 不是本快照的实体（protocol.md:104 要求引用闭合）", path);
            if (memberKindIds.Count > 0 && !memberKindIds.Contains(kind, StringComparer.Ordinal))
                throw new PublicMappingException("I-28",
                    $"成员 '{reference}' 的 kind_id '{kind}' 不属于声明的成员种类 " +
                    string.Join("/", memberKindIds), path);
            members.Add(reference);
        }

        return members;
    }

    private static void RequireBoundEntity(string collection, string publicRef, string? expectedKindId,
        IReadOnlyDictionary<string, string> entitiesByPublicId, string ruleId, string path)
    {
        if (!entitiesByPublicId.TryGetValue(publicRef, out var kind))
            throw new PublicMappingException(ruleId,
                $"集合 '{collection}' 引用的实体 '{publicRef}' 不是本快照的实体（protocol.md:83/96 要求引用闭合，" +
                "I-26/I-07 要求 pile_ref 的 kind_id 唯一匹配 pile_type）", path);
        if (expectedKindId is not null && !string.Equals(kind, expectedKindId, StringComparison.Ordinal))
            throw new PublicMappingException("I-07",
                $"引用 '{publicRef}' 的 kind_id '{kind}' 与声明的 '{expectedKindId}' 不一致", path);
    }

    private static int CompareTypeOwner(SequenceDto left, SequenceDto right) =>
        CompareTypeOwner(left.TypeId, left.OwnerRef, right.TypeId, right.OwnerRef);

    private static int CompareTypeOwner(GroupDto left, GroupDto right) =>
        CompareTypeOwner(left.TypeId, left.OwnerRef, right.TypeId, right.OwnerRef);

    private static int CompareTypeOwner(string leftType, string leftOwner, string rightType, string rightOwner)
    {
        var result = WireCanonical.CompareByUtf8Bytes(leftType, rightType);
        return result != 0 ? result : WireCanonical.CompareByUtf8Bytes(leftOwner, rightOwner);
    }

    // -- 候选 ----------------------------------------------------------------------------------------------------

    private (List<LegalActionDto>, List<CandidateBinding>) BuildLegalActions(IReadOnlyList<LegalAction> legalActions,
        IReadOnlyDictionary<InstanceId, string> ids)
    {
        var entries = new List<(LegalActionDto Action, CandidateBinding Binding)>();
        foreach (var legal in legalActions)
        {
            var rule = _profile.RequireAction(legal.Definition.QualifiedId);
            var parameters = legal.Definition.Program.Parameters.Count;
            foreach (var binding in legal.Bindings)
            {
                var fields = new List<FieldDto>();
                var arguments = new List<Value>();
                for (var slot = 0; slot < parameters; slot++)
                {
                    if (!rule.Parameters.TryGetValue(slot, out var declared))
                        throw new PublicMappingException("N-02-UNDECLARED-ACTION-FIELD",
                            $"动作 '{legal.Definition.QualifiedId}' 的参数槽 {slot} 没有公开字段声明");
                    if (!legal.Domains.ContainsKey(slot))
                        throw new PublicMappingException("TODO-N02-UNBOUND-PARAMETER",
                            $"动作 '{legal.Definition.QualifiedId}' 的参数槽 {slot} 没有域（候选查询或声明 choices），" +
                            "宿主无法提供执行所需值；protocol.md:98 要求所有执行参数 known");
                    var value = binding[slot];
                    arguments.Add(value);
                    var enumTypeId = legal.Definition.Parameters[slot].EnumTypeId;
                    fields.Add(EncodeField(declared, value, enumTypeId, ids,
                        $"{legal.Definition.QualifiedId}[{slot}]",
                        $"snapshot.legal_actions?fields[{declared.FieldId}]"));
                }

                entries.Add(BuildEntry(rule.ActionTypeId, SortFields(fields), legal.Definition, arguments,
                    _candidateNamespace));
            }
        }

        return Finish(entries);
    }

    private (List<LegalActionDto>, List<CandidateBinding>) BuildDecisionAnswers(DecisionRequest pending,
        IReadOnlyDictionary<InstanceId, string> ids)
    {
        if (pending.Kind is DecisionResponseKind.Bool or DecisionResponseKind.Confirm)
        {
            var boolRule = _profile.AnswerAction;
            var flagField = boolRule.Parameters.Values.SingleOrDefault(p => p.FieldId == DerivedPublicProfile.AnswerFlagFieldId && p.Kind == PublicValueKind.Boolean);
            if (flagField is null || flagField.Required)
                throw new PublicMappingException("N-02-BOOL-ANSWER-PROFILE", "Bool decisions require the optional Flag field in the 0.2 derived profile.");
            var boolEntries = new List<(LegalActionDto Action, CandidateBinding Binding)>();
            foreach (var flag in pending.Kind == DecisionResponseKind.Confirm ? new[] { true } : new[] { false, true })
            {
                var field = EncodeField(flagField, Value.Of(flag), -1, ids, "answer.flag", $"snapshot.legal_actions?fields[{flagField.FieldId}]");
                var entry = BuildEntry(boolRule.ActionTypeId, [field], null, [], _candidateNamespace);
                boolEntries.Add((entry.Action, entry.Binding with { DecisionRequestId = pending.RequestId, DecisionFlag = flag }));
            }
            return Finish(boolEntries);
        }
        if (pending.Kind is not (DecisionResponseKind.EntityRef or DecisionResponseKind.EntityRefList))
            throw new PublicMappingException("TODO-N02-DECISION-KIND",
                $"暂停的应答种类是 {pending.Kind}，当前Profile只支持Bool、Confirm及已声明的实体引用应答；" +
                "需要协议/协调者裁定该形状，映射层不发明第二个应答结构");
        var pair = pending.Kind == DecisionResponseKind.EntityRefList && pending.Minimum == 2 && pending.Maximum == 2
            && _profile.AnswerAction.Parameters.Values.Any(p => p.FieldId == DerivedPublicProfile.AnswerSecondInstanceFieldId);
        if (!pair && (pending.Maximum != 1 || pending.Minimum is < 0 or > 1
            || pending.Kind == DecisionResponseKind.EntityRef && pending.Minimum != 1))
            throw new PublicMappingException("TODO-N02-MULTI-SELECT",
                $"暂停要求选择 {pending.Minimum}..{pending.Maximum} 个实例；当前Profile只支持单选与已声明的恰选两项，" +
                "不能缩减真实决策数量", "snapshot.legal_actions");
        if (pending.Candidates.Count == 0)
            throw new PublicMappingException("TODO-N02-EMPTY-CANDIDATES",
                "暂停没有候选实例，无法发布非空的 decision 候选（protocol.md:60）", "snapshot.legal_actions");

        var rule = _profile.AnswerAction;
        var declared = rule.Parameters[0];
        var entries = new List<(LegalActionDto Action, CandidateBinding Binding)>();
        if (pair)
        {
            var secondField = rule.Parameters.Values.Single(p => p.FieldId == DerivedPublicProfile.AnswerSecondInstanceFieldId);
            // One candidate is one complete ordered answer. Do not split a 2..2
            // engine decision into two actions or discard its order-sensitive pairs.
            foreach (var first in pending.Candidates)
            foreach (var second in pending.Candidates.Where(c => c != first))
            {
                var fields = SortFields([
                    EncodeField(declared, Value.OfRef(first), -1, ids, "answer.instance", "snapshot.legal_actions"),
                    EncodeField(secondField, Value.OfRef(second), -1, ids, "answer.second_instance", "snapshot.legal_actions")]);
                var entry = BuildEntry(rule.ActionTypeId, fields, null, [], _candidateNamespace);
                entries.Add((entry.Action, entry.Binding with
                { DecisionRequestId = pending.RequestId, DecisionInstance = first, DecisionSecondInstance = second }));
            }
            return Finish(entries);
        }
        if (pending.Minimum == 0)
        {
            if (declared.Required) throw new PublicMappingException(
                "TODO-N02-OPTIONAL-ANSWER", "Profile must declare the answer instance optional before publishing cancellation.");
            var empty = BuildEntry(rule.ActionTypeId, [], null, [], _candidateNamespace);
            entries.Add((empty.Action, empty.Binding with { DecisionRequestId = pending.RequestId, EmptyDecision = true }));
        }
        foreach (var candidate in pending.Candidates)
        {
            var publicId = RequirePublicId(ids, candidate, "I-22", "snapshot.legal_actions");
            var fields = new List<FieldDto>
            {
                EncodeField(declared, Value.OfRef(candidate), -1, ids, "answer.instance",
                    $"snapshot.legal_actions?fields[{declared.FieldId}]"),
            };
            var entry = BuildEntry(rule.ActionTypeId, fields, null, [], _candidateNamespace);
            entries.Add((entry.Action, entry.Binding with
            {
                DecisionRequestId = pending.RequestId,
                DecisionInstance = candidate,
            }));
        }

        return Finish(entries);
    }

    private static (LegalActionDto Action, CandidateBinding Binding) BuildEntry(string actionTypeId,
        List<FieldDto> fields, ActionDefinition? action, IReadOnlyList<Value> arguments, string namespaceToken)
    {
        var actionWire = WireValue.Object(
            ("action_type", WireValue.Str(actionTypeId)),
            ("fields", new WireArray(fields.Select(field => (WireValue)field.ToWire()))));
        // 语义键 = 规范序列化的 {action_type, fields}，fields 按 field_id（contract-artifacts §3 第 39 行）。
        var digest = WireCanonical.DigestBytes(WireSort.ActionSemanticKey(actionWire));
        var candidateId = CandidateId(namespaceToken, digest);
        return (new LegalActionDto(candidateId, actionTypeId, fields),
            new CandidateBinding(candidateId, actionTypeId, digest, action, arguments));
    }

    /// <summary>
    /// 由 <paramref name="namespaceToken"/> 与 <paramref name="semanticKeyDigest"/> 派生的**候选句柄**。
    /// </summary>
    private static string CandidateId(string namespaceToken, string semanticKeyDigest) =>
        "c-" + CandidateHandleDigest(namespaceToken, semanticKeyDigest)[..16];

    /// <summary>
    /// 文本的规范 SHA-256（`sha256:` 前缀 + 小写十六进制）；用作命名空间令牌与句柄摘要。
    /// </summary>
    private static string TextDigest(string text) =>
        WireCanonical.DigestBytes(new UTF8Encoding(false).GetBytes(text));

    /// <summary>
    /// 候选句柄摘要 = SHA-256("candidate-v1" ‖ 本会话命名空间 ‖ 动作语义键摘要)。
    /// </summary>
    private static string CandidateHandleDigest(string namespaceToken, string semanticKeyDigest) =>
        TextDigest("candidate-v1\u001f" + namespaceToken + "\u001f" + semanticKeyDigest)["sha256:".Length..];

    private static (List<LegalActionDto>, List<CandidateBinding>) Finish(
        List<(LegalActionDto Action, CandidateBinding Binding)> entries)
    {
        // legal_actions 按动作语义键的 UTF-8 字节序排列；相同键拒绝重复（protocol.md:100/102、I-37/I-38）。
        entries.Sort((left, right) => CompareBytes(
            WireSort.ActionSemanticKey(left.Action.ToWire()),
            WireSort.ActionSemanticKey(right.Action.ToWire())));

        var actions = new List<LegalActionDto>();
        var bindings = new List<CandidateBinding>();
        string? previous = null;
        foreach (var (action, binding) in entries)
        {
            if (previous is not null && string.Equals(previous, binding.SemanticKeyDigest, StringComparison.Ordinal))
                throw new PublicMappingException("I-37",
                    $"同一动作语义以不同句柄重复发布：{action.ActionType}（protocol.md:100 要求拒绝）",
                    $"snapshot.legal_actions[{action.CandidateId}]");
            previous = binding.SemanticKeyDigest;
            actions.Add(action);
            bindings.Add(binding);
        }

        return (actions, bindings);
    }

    private static int CompareBytes(byte[] left, byte[] right)
    {
        var length = Math.Min(left.Length, right.Length);
        for (var index = 0; index < length; index++)
        {
            var difference = left[index] - right[index];
            if (difference != 0) return difference;
        }

        return left.Length - right.Length;
    }

    // -- 终局 ----------------------------------------------------------------------------------------------------

    private TerminalResultDto BuildTerminal(TerminalOutcome terminal)
    {
        var rule = _profile.RequireTerminal(terminal.Definition.QualifiedId);
        if (terminal.Fields.Count != terminal.Definition.Fields.Count)
            throw new PublicMappingException("N-02-TERMINAL-FIELDS",
                $"终局结果 '{terminal.Definition.QualifiedId}' 的字段数与声明不一致",
                "snapshot.terminal_result.facts");
        var facts = new List<FieldDto>();
        for (var index = 0; index < terminal.Fields.Count; index++)
        {
            var name = terminal.Definition.Fields[index].Name;
            if (!rule.Fields.TryGetValue(name, out var disposition))
                throw new PublicMappingException("N-02-UNDECLARED-FIELD",
                    $"终局结果 '{terminal.Definition.QualifiedId}' 的字段 '{name}' 没有公开映射处置");
            if (disposition.Published is null) continue;
            facts.Add(EncodeField(disposition.Published, terminal.Fields[index],
                terminal.Definition.Fields[index].EnumTypeId, new Dictionary<InstanceId, string>(),
                $"{terminal.Definition.QualifiedId}.{name}",
                $"snapshot.terminal_result.facts[{disposition.Published.FieldId}]"));
        }

        return new TerminalResultDto(rule.Outcome, rule.ReasonId, SortFields(facts));
    }

    // -- 值 ------------------------------------------------------------------------------------------------------

    private FieldDto EncodeField(PublishedField declared, Value value, int enumTypeId,
        IReadOnlyDictionary<InstanceId, string> ids, string where, string? path = null) =>
        EncodeField(declared.FieldId, declared.Kind, declared.CategoryValues, value, enumTypeId, ids, where, path);

    private FieldDto EncodeField(PublicActionField declared, Value value, int enumTypeId,
        IReadOnlyDictionary<InstanceId, string> ids, string where, string? path = null) =>
        EncodeField(declared.FieldId, declared.Kind, declared.CategoryValues, value, enumTypeId, ids, where, path);

    private FieldDto EncodeField(string fieldId, PublicValueKind kind,
        IReadOnlyDictionary<string, string>? categoryValues, Value value, int enumTypeId,
        IReadOnlyDictionary<InstanceId, string> ids, string where, string? path)
    {
        WireValue wire = kind switch
        {
            PublicValueKind.Integer => value.Kind == ValueKind.Int
                ? WireValue.Str(WireDecimal.Normalize(WireValue.Integer(value.AsInt())))
                : throw Mismatch(kind, value, where, path),
            PublicValueKind.Decimal => value.Kind == ValueKind.Fixed
                ? WireValue.Str(WireDecimal.Normalize(
                    WireValue.Str(value.AsDecimal().ToString(CultureInfo.InvariantCulture))))
                : throw Mismatch(kind, value, where, path),
            PublicValueKind.Boolean => value.Kind == ValueKind.Bool
                ? WireValue.Bool(value.AsBool())
                : throw Mismatch(kind, value, where, path),
            PublicValueKind.Category => WireValue.Str(RequireCategory(categoryValues, value, enumTypeId, where, path)),
            PublicValueKind.EntityRef => WireValue.Str(RequirePublicId(ids, value, "I-22", where, path)),
            // 0.2 的 string 值类型：原样公开文本，不冒充有限类别、也不改写大小写/空白（protocol.md §7）。
            PublicValueKind.String => value.Kind == ValueKind.String
                ? WireValue.Str(value.AsString())
                : throw Mismatch(kind, value, where, path),
            _ => throw new PublicMappingException("N-02-VALUE-KIND", $"未知公开值类型 {kind}（{where}）", path),
        };

        return new FieldDto(fieldId, "known", wire);
    }

    private static PublicMappingException Mismatch(PublicValueKind kind, Value value, string where, string? path) =>
        new("I-22", $"{where}: 公开值类型 {kind} 与引擎值 {value.Kind} 不一致", path);

    private string RequireCategory(IReadOnlyDictionary<string, string>? categoryValues, Value value, int enumTypeId,
        string where, string? path)
    {
        if (categoryValues is null || categoryValues.Count == 0)
            throw new PublicMappingException("N-02-UNDECLARED-CATEGORY",
                $"{where}: 声明为 category 但没有登记任何类别 ID（protocol.md:74 要求值来自目录声明）", path);
        var raw = value.Kind switch
        {
            ValueKind.String => RequireString(value, where),
            ValueKind.Enum => _ruleSet.Enums[enumTypeId].NameOf(value.AsEnumOrdinal()),
            _ => throw Mismatch(PublicValueKind.Category, value, where, path),
        };
        return categoryValues.TryGetValue(raw, out var category)
            ? category
            : throw new PublicMappingException("N-02-UNDECLARED-CATEGORY",
                $"{where}: 值 '{raw}' 不在公开类别表内；不得改写或自造类别 ID 来让它通过", path);
    }

    private static string RequireString(Value value, string where) => value.Kind == ValueKind.String
        ? value.AsString()
        : throw new PublicMappingException("N-02-CONTENT", $"{where}: 期望字符串值，得到 {value.Kind}");

    private static string RequirePublicId(IReadOnlyDictionary<InstanceId, string> ids, InstanceId instance,
        string ruleId, string where = "引用", string? path = null) =>
        ids.TryGetValue(instance, out var reference)
            ? reference
            : throw new PublicMappingException(ruleId,
                $"{where}: 引擎引用 {instance} 不在本快照的实体表内（protocol.md:104 要求引用闭合）", path);

    private static string RequirePublicId(IReadOnlyDictionary<InstanceId, string> ids, Value value, string ruleId,
        string where, string? path)
    {
        if (value.Kind != ValueKind.Ref)
            throw new PublicMappingException("I-22", $"{where}: 声明为 entity_ref 但引擎值是 {value.Kind}", path);
        return RequirePublicId(ids, value.AsRef(), ruleId, where, path);
    }
}
