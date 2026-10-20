using CoreTheSpire.Engine;
using NetTheSpire.Contracts;

namespace NetTheSpire.Projection;

/// <summary>
/// 映射层用到的一份公开声明表。它不是目录本身：<c>PublicCatalog</c>（<c>semantic_version</c>、
/// <c>applies_to</c>、<c>visibility</c>…）由 <c>get_catalog</c>/<c>describe</c> 物化（N-03/N-04）。
/// 这里固定的是“引擎声明 → 公开声明 ID”的显式登记，使快照里的每个 ID 都能回溯到一个登记项，
/// 而不是由映射层现编。
/// </summary>
public sealed class PublicSnapshotProfile
{
    public PublicSnapshotProfile(
        string rulesetId,
        string catalogId,
        string backendProfileId,
        IReadOnlyDictionary<string, PublicKindRule> kinds,
        IReadOnlyDictionary<string, CollectionDisposition> collections,
        IReadOnlyDictionary<string, Disposition<PublicRelationRule>> relations,
        IReadOnlyDictionary<string, PublicActionRule> actions,
        PublicActionRule answerAction,
        IReadOnlyDictionary<string, PublicTerminalRule> terminals,
        IReadOnlyDictionary<string, PublicKnownPositionRule>? knownPositions = null,
        string protocolVersion = NetProtocol.V02)
    {
        RulesetId = rulesetId ?? throw new ArgumentNullException(nameof(rulesetId));
        CatalogId = catalogId ?? throw new ArgumentNullException(nameof(catalogId));
        BackendProfileId = backendProfileId ?? throw new ArgumentNullException(nameof(backendProfileId));
        Kinds = kinds ?? throw new ArgumentNullException(nameof(kinds));
        Collections = collections ?? throw new ArgumentNullException(nameof(collections));
        Relations = relations ?? throw new ArgumentNullException(nameof(relations));
        Actions = actions ?? throw new ArgumentNullException(nameof(actions));
        AnswerAction = answerAction ?? throw new ArgumentNullException(nameof(answerAction));
        Terminals = terminals ?? throw new ArgumentNullException(nameof(terminals));
        KnownPositions = knownPositions ?? new Dictionary<string, PublicKnownPositionRule>(StringComparer.Ordinal);
        NetProtocol.RequireKnown(protocolVersion, "PublicSnapshotProfile.protocolVersion");
        ProtocolVersion = protocolVersion;
        DeclaredIds = CollectDeclaredIds();
    }

    /// <summary>当前公开协议版本，要求精确相等。</summary>
    public string ProtocolVersion { get; }

    public string RulesetId { get; }

    public string CatalogId { get; }

    public string BackendProfileId { get; }

    /// <summary>引擎实体类型 qualified id → 公开 kind 声明。</summary>
    public IReadOnlyDictionary<string, PublicKindRule> Kinds { get; }

    /// <summary>引擎集合 qualified id → pile/sequence/group/不发布 的处置。</summary>
    public IReadOnlyDictionary<string, CollectionDisposition> Collections { get; }

    /// <summary>引擎关系 qualified id → 公开关系声明。</summary>
    public IReadOnlyDictionary<string, Disposition<PublicRelationRule>> Relations { get; }

    /// <summary>引擎动作 qualified id → 公开动作声明。</summary>
    public IReadOnlyDictionary<string, PublicActionRule> Actions { get; }

    /// <summary>暂停期应答所用的 net 自声明动作类型（协议没有“应答”操作，应答走 <c>step</c> 的候选）。</summary>
    public PublicActionRule AnswerAction { get; }

    /// <summary>引擎终局结果 qualified id → 公开终局原因声明。</summary>
    public IReadOnlyDictionary<string, PublicTerminalRule> Terminals { get; }

    /// <summary>引擎“已知位置”事实类型 qualified id → 公开位置映射（H-01/H-02）。</summary>
    public IReadOnlyDictionary<string, PublicKnownPositionRule> KnownPositions { get; }

    /// <summary>该事实类型是不是一个已知位置来源；不是则返回 null（不猜、不推断）。</summary>
    public PublicKnownPositionRule? KnownPositionFor(string engineKindQualifiedId) =>
        KnownPositions.TryGetValue(engineKindQualifiedId, out var rule) ? rule : null;

    /// <summary>目录声明的全部 pile_type（按 PileTypeId 唯一；重复声明的 kind_id 直接拒绝）。</summary>
    public IReadOnlyList<PublishedPile> PileTypes =>
        Collections.Values.Select(item => item.Pile).Where(pile => pile is not null)
            .Select(pile => pile!).ToList();

    /// <summary>目录声明的全部 group_type（W-01：药水栏等"有 owner 的集合"用 group 表达，不借用卡牌 pile 语义）。</summary>
    public IReadOnlyList<PublishedGroup> GroupTypes =>
        Collections.Values.Select(item => item.Group).Where(group => group is not null)
            .Select(group => group!).ToList();

    /// <summary>
    /// 目录声明的全部 sequence_type（公开有序流程）。owner 可能是**公开种类**（新通用路径，
    /// 快照内唯一解析）或写死的**公开引用**（旧 0.1 兼容路径）——两种形状都在这里，由投影层分别解析。
    /// </summary>
    public IReadOnlyList<PublishedSequence> SequenceTypes =>
        Collections.Values.Select(item => item.Sequence).Where(sequence => sequence is not null)
            .Select(sequence => sequence!).ToList();

    /// <summary>需要按"种类在本快照里唯一匹配"解析 owner 的公开种类 ID（group owner 与 sequence owner）。</summary>
    public IReadOnlyList<string> OwnerKindIds =>
        GroupTypes.Select(group => group.OwnerKindId)
            .Concat(SequenceTypes.Select(sequence => sequence.OwnerKindId).Where(kind => kind is not null)
                .Select(kind => kind!))
            .ToList();

    /// <summary>本表登记的全部公开声明 ID（kind/field/category/relation/pile/action/reason + 内容 ID）。</summary>
    public IReadOnlyList<string> DeclaredIds { get; }

    /// <summary>I-33/I-79：目录一旦包含 piles，必须同时声明这三个保留名。</summary>
    public static IReadOnlyList<string> ReservedPileDeclarationIds { get; } =
        [ReservedPileRelationId, ReservedPileTotalCountId, ReservedPileMembersCompleteId];

    /// <summary>保留的派生关系：牌堆 → 成员（张量化用，wire 的 relations 不重复填入）。</summary>
    public const string ReservedPileRelationId = "net:pile_member";

    /// <summary>保留的派生字段：牌堆真实总数（wire 的 entity.fields 不重复填入）。</summary>
    public const string ReservedPileTotalCountId = "net:pile_total_count";

    /// <summary>保留的派生字段：成员是否完整（wire 的 entity.fields 不重复填入）。</summary>
    public const string ReservedPileMembersCompleteId = "net:pile_members_complete";

    public PublicKindRule RequireKind(string engineTypeQualifiedId) =>
        Kinds.TryGetValue(engineTypeQualifiedId, out var rule)
            ? rule
            : throw new PublicMappingException("N-02-UNDECLARED-KIND",
                $"引擎实体类型 '{engineTypeQualifiedId}' 没有公开 kind 声明（protocol.md §1 第 30 行要求引用闭合）");

    public CollectionDisposition RequireCollection(string engineCollectionQualifiedId) =>
        Collections.TryGetValue(engineCollectionQualifiedId, out var rule)
            ? rule
            : throw new PublicMappingException("N-02-UNDECLARED-COLLECTION",
                $"引擎集合 '{engineCollectionQualifiedId}' 没有处置（不允许静默丢弃公开事实）");

    public Disposition<PublicRelationRule> RequireRelation(string engineRelationQualifiedId) =>
        Relations.TryGetValue(engineRelationQualifiedId, out var rule)
            ? rule
            : throw new PublicMappingException("N-02-UNDECLARED-RELATION",
                $"引擎关系 '{engineRelationQualifiedId}' 没有公开关系声明");

    public PublicActionRule RequireAction(string engineActionQualifiedId) =>
        Actions.TryGetValue(engineActionQualifiedId, out var rule)
            ? rule
            : throw new PublicMappingException("N-02-UNDECLARED-ACTION",
                $"引擎动作 '{engineActionQualifiedId}' 没有公开动作声明");

    public PublicTerminalRule RequireTerminal(string engineTerminalQualifiedId) =>
        Terminals.TryGetValue(engineTerminalQualifiedId, out var rule)
            ? rule
            : throw new PublicMappingException("N-02-UNDECLARED-TERMINAL",
                $"引擎终局结果 '{engineTerminalQualifiedId}' 没有公开终局原因声明");

    /// <summary>该公开表是否声明了任何 pile_type（决定 I-33 的保留声明是否必须出现）。</summary>
    public bool DeclaresPileTypes => Collections.Values.Any(item => item.Pile is not null);

    /// <summary>I-33/I-79：目录一旦包含 piles，就必须同时声明这三个保留名。缺一个即拒绝发布。</summary>
    public void RequireReservedPileDeclarations()
    {
        if (!DeclaresPileTypes) return;
        // 注意：这里检查的是**本 profile 的声明清单**；真正的发布物（目录文档）必须在
        // CatalogProjector 里再次核对，否则"自己检查自己"会放过"清单有、wire 没有"的缺口。
        var missing = ReservedPileDeclarationIds
            .Where(id => !DeclaredIds.Contains(id, StringComparer.Ordinal))
            .ToList();
        if (missing.Count > 0)
        {
            throw new PublicMappingException("I-33",
                "目录使用 piles，必须声明保留名 " + string.Join("/", ReservedPileDeclarationIds) +
                "，缺少: " + string.Join(",", missing));
        }

        // protocol.md:83 要求 pile_ref 的 kind_id 唯一匹配一个 pile_type：同一个 kind_id 不得声明两次。
        var duplicated = PileTypes.GroupBy(pile => pile.PileKindId, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();
        if (duplicated.Count > 0)
        {
            throw new PublicMappingException("I-07",
                "同一个 kind_id 被多个 pile_type 声明，pile_ref 无法唯一匹配: " + string.Join(",", duplicated));
        }
    }

    private IReadOnlyList<string> CollectDeclaredIds()
    {
        var ids = new List<string>();
        foreach (var (_, kind) in Kinds)
        {
            ids.Add(kind.KindId);
            ids.AddRange(kind.ContentIds);
            foreach (var (_, disposition) in kind.Fields)
            {
                if (disposition.Published is not null) ids.Add(disposition.Published.FieldId);
            }
        }

        foreach (var (_, disposition) in Collections)
        {
            if (disposition.Pile is { } pile)
            {
                ids.Add(pile.PileTypeId);
                ids.Add(pile.PileKindId);
                ids.AddRange(pile.MemberKindIds);
            }
            else if (disposition.Sequence is { } sequence)
            {
                ids.Add(sequence.SequenceTypeId);
                if (sequence.OwnerKindId is { } sequenceOwnerKind) ids.Add(sequenceOwnerKind);
                ids.AddRange(sequence.ElementKindIds);
            }
            else if (disposition.Group is { } group)
            {
                ids.Add(group.GroupTypeId);
                ids.Add(group.OwnerKindId);
                ids.AddRange(group.MemberKindIds);
            }
        }

        foreach (var (_, disposition) in Relations)
        {
            if (disposition.Published is { } relation) ids.Add(relation.TypeId);
        }

        foreach (var (_, action) in Actions)
        {
            ids.Add(action.ActionTypeId);
            ids.AddRange(action.Parameters.Values.Select(field => field.FieldId));
        }

        ids.Add(AnswerAction.ActionTypeId);
        ids.AddRange(AnswerAction.Parameters.Values.Select(field => field.FieldId));

        foreach (var (_, terminal) in Terminals)
        {
            ids.Add(terminal.ReasonId);
            foreach (var (_, disposition) in terminal.Fields)
            {
                if (disposition.Published is not null) ids.Add(disposition.Published.FieldId);
            }
        }

        // category 表里的类别 ID（目录的 fields[].category_ids）也要登记。
        foreach (var category in Kinds.Values.SelectMany(kind => kind.Fields.Values)
                     .Select(item => item.Published)
                     .Where(field => field?.CategoryValues is not null)
                     .SelectMany(field => field!.CategoryValues!.Values))
        {
            ids.Add(category);
        }

        foreach (var category in Actions.Values.SelectMany(action => action.Parameters.Values)
                     .Concat(AnswerAction.Parameters.Values)
                     .Where(field => field.CategoryValues is not null)
                     .SelectMany(field => field.CategoryValues!.Values))
        {
            ids.Add(category);
        }

        if (DeclaresPileTypes) ids.AddRange(ReservedPileDeclarationIds);

        return ids;
    }
}

/// <summary>
/// 映射层的显式拒绝：声明缺失、值域未登记、协议不变式在当前引擎形状下无法满足。
/// <see cref="RuleId"/> 用协议/不变量 ID，或本层自己命名的 <c>TODO-N02-*</c>（必须带名字与指针，不许静默降级）。
///
/// <see cref="Path"/> 是给协调者组 error 用的路径，采用**与共享 L3 一致的约定**（按公开 ID 索引，
/// 如 <c>snapshot.entities[&lt;instance_id&gt;].fields[&lt;field_id&gt;]</c>），不是数组下标。
/// 协议本身没有规定 <c>error.path</c> 的语法（<c>protocol.md:205</c>；<c>invariants.md</c> 的 U-11 仍开放），
/// 因此这里只声明“沿用 L3 约定”，不声称符合某个已规定的语法；无法用该约定表达时为 null。
/// </summary>
public sealed class PublicMappingException(string ruleId, string message, string? path = null)
    : Exception(path is null ? $"[{ruleId}] {message}" : $"[{ruleId}] {path}: {message}")
{
    public string RuleId { get; } = ruleId;

    /// <summary>L3 风格路径（公开 ID 索引）；协议未规定该语法，见类型注释。</summary>
    public string? Path { get; } = path;
}
