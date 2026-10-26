using CoreTheSpire.Engine;

namespace NetTheSpire.Projection;

/// <summary>
/// 公开映射层的声明词汇。唯一规范出处：<c>docs/areas/net/reference/protocol.md</c>（结构/键）
/// 与 <c>docs/areas/net/spec/contract-artifacts.md</c>（排序、身份、覆盖表）。
///
/// 这里只声明“引擎的哪个声明映射成哪个公开声明、哪个引擎字段不可发布”，不实现任何游戏规则，
/// 也不复制目录：目录的 <c>semantic_version</c>/<c>applies_to</c> 等键由 <c>get_catalog</c>（N-03/N-04）物化。
/// </summary>
/// <remarks>
/// 值类型只有 <see cref="PublicValueKind"/> 四种（protocol.md §2 第 70–75 行）。协议没有 string、
/// participant 或 scope 值类型；引擎的 <see cref="ValueKind.Participant"/>、<see cref="ValueKind.Scope"/>
/// 字段只能声明为“不发布”，不能自造第二个值类型来塞进去。
/// </remarks>
public enum PublicValueKind
{
    /// <summary>规范十进制整数字符串（<c>number_format=integer</c>）。</summary>
    Integer = 0,

    /// <summary>规范十进制字符串（<c>number_format=decimal</c>）。</summary>
    Decimal = 1,

    /// <summary>JSON true/false。</summary>
    Boolean = 2,

    /// <summary>目录声明的类别 ID 字符串。</summary>
    Category = 3,

    /// <summary>当前快照的 <c>instance_id</c> 字符串。</summary>
    EntityRef = 4,

    /// <summary>
    /// 0.2 新增（protocol.md §7）：公开字符串值，原样编码，**不是**类别词表。
    /// 0.1 的协议没有这个值类型，因此 0.1 Profile 不得声明它（由 0.1 机器 schema 与目录校验拒绝）。
    /// </summary>
    String = 5,
}

/// <summary>
/// 一个已发布字段的声明：公开 <c>field_id</c>、值类型、是否 required。
/// <see cref="CategoryValues"/> 是“引擎原值 → 目录声明的类别 ID”的**显式**全表；未列出的原值一律拒绝，
/// 不默默改写（protocol.md §2 第 74 行）。
/// </summary>
public sealed record PublishedField(
    string FieldId,
    PublicValueKind Kind,
    bool Required = true,
    IReadOnlyDictionary<string, string>? CategoryValues = null,
    string? TargetKindId = null);

/// <summary>引擎声明 → 公开声明的处置：要么发布，要么**显式**不发布并写明规则号与理由。没有静默丢弃。</summary>
public sealed class Disposition<T> where T : class
{
    private Disposition(T? published, string? skipRuleId, string? skipReason)
    {
        Published = published;
        SkipRuleId = skipRuleId;
        SkipReason = skipReason;
    }

    public T? Published { get; }

    public string? SkipRuleId { get; }

    public string? SkipReason { get; }

    public bool IsPublished => Published is not null;

    public static Disposition<T> Publish(T published) =>
        new(published ?? throw new ArgumentNullException(nameof(published)), null, null);

    public static Disposition<T> Skip(string ruleId, string reason)
    {
        if (string.IsNullOrWhiteSpace(ruleId)) throw new ArgumentException("不发布必须带规则号", nameof(ruleId));
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("不发布必须带理由", nameof(reason));
        return new Disposition<T>(null, ruleId, reason);
    }
}

/// <summary>一个引擎实体类型的公开映射：公开 kind_id、内容标识来源、逐字段处置。</summary>
public sealed record PublicKindRule(
    string KindId,
    string? ContentFieldName,
    IReadOnlyList<string> ContentIds,
    IReadOnlyDictionary<string, Disposition<PublishedField>> Fields);

/// <summary>发布为协议 <c>pile</c>：protocol.md:83 要求 pile_ref 的 kind_id **唯一匹配**一个已声明 pile_type，
/// 因此每种牌堆区都是自己的实体种类，<see cref="PileKindId"/> 同时用于目录的 <c>kind_id</c> 与在本快照里定位那唯一
/// 一个实例；找不到或找到多个都拒绝，不猜、不自造 id。</summary>
public sealed record PublishedPile(
    string PileTypeId,
    string PileKindId,
    IReadOnlyList<string> MemberKindIds,
    PublicPositionSchema? PositionSchema);

/// <summary>pile_type 的 position_schema（protocol.md:81）：unit/origin 由该声明解释，min/max 是真实牌堆从顶部 0
/// 起的绝对位置范围。null 表示该种牌堆没有公开位置语义。</summary>
public sealed record PublicPositionSchema(string Unit, string Origin, long Min, long? Max);

/// <summary>
/// 包声明的“已知位置”事实类（H-01/H-02）：一个事实实体引用真实卡牌实例，并给出牌堆种类与该牌的绝对位置。
/// 位置由包维护；映射层只读当前快照，绝不从隐藏实时索引重算或补造。
/// </summary>
public sealed record PublicKnownPositionRule(
    string FactKindId,
    string CardFieldName,
    string PileFieldName,
    string PositionFieldName,
    IReadOnlyDictionary<string, string> PileTypeByToken);

/// <summary>发布为协议 <c>sequence</c>：elements 是**公开有序**流程（protocol.md:96），从 0 起连续。
/// 只有引擎明确声明顺序可见（<c>OrderVisible</c>）的集合才允许走这条路：把 <c>OrderVisible=false</c> 的集合
/// 映射成 sequence 就是把内部顺序泄漏成公开信息（V-02）。
///
/// owner 有**两种**声明形状，且必须恰好给出一种：
/// <list type="bullet">
/// <item><see cref="OwnerPublicRef"/>：写入声明的**固定**公开引用（旧 0.1 作者/切片路径）。该引用必须解析成
/// 快照内的实体。</item>
/// <item><see cref="OwnerKindId"/>：一个**公开种类**（W-02）。正式派生路径只能按种类解析，因为公开句柄由
/// 会话内绑定决定，不能写死每局句柄；该种类在本快照里必须**恰好有一个**可见实例（与 <c>pile_ref</c> 及
/// group owner 的唯一匹配同理），否则拒绝发布而不是猜一个。</item>
/// </list></summary>
public sealed record PublishedSequence
{
    private PublishedSequence(
        string sequenceTypeId,
        string? ownerPublicRef,
        string? ownerKindId,
        IReadOnlyList<string> elementKindIds,
        int minCount,
        int? maxCount)
    {
        if ((ownerPublicRef is null) == (ownerKindId is null))
        {
            throw new ArgumentException("sequence 的 owner 必须恰好给出一种：固定公开引用或公开种类");
        }

        SequenceTypeId = sequenceTypeId ?? throw new ArgumentNullException(nameof(sequenceTypeId));
        OwnerPublicRef = ownerPublicRef;
        OwnerKindId = ownerKindId;
        ElementKindIds = elementKindIds ?? throw new ArgumentNullException(nameof(elementKindIds));
        MinCount = minCount;
        MaxCount = maxCount;
    }

    public string SequenceTypeId { get; }

    /// <summary>写死的公开 owner 引用（旧兼容路径）；按种类解析时为 null。</summary>
    public string? OwnerPublicRef { get; }

    /// <summary>owner 的公开种类 ID（新通用路径）；按固定引用发布时为 null。</summary>
    public string? OwnerKindId { get; }

    public IReadOnlyList<string> ElementKindIds { get; }

    /// <summary>基数下限（目录 <c>sequence_types.min_count</c>），来自引擎声明的集合容量。</summary>
    public int MinCount { get; }

    /// <summary>基数上限（目录 <c>sequence_types.max_count</c>），<c>int.MaxValue</c> 表示未声明上限 ⇒ null。</summary>
    public int? MaxCount { get; }

    /// <summary>旧路径：owner 是一个写入声明的固定公开引用。</summary>
    public static PublishedSequence ForPublicRef(
        string sequenceTypeId,
        string ownerPublicRef,
        IReadOnlyList<string> elementKindIds,
        int minCount,
        int? maxCount) =>
        new(sequenceTypeId, ownerPublicRef ?? throw new ArgumentNullException(nameof(ownerPublicRef)), null,
            elementKindIds, minCount, maxCount);

    /// <summary>正式路径：owner 按公开种类在本快照里唯一解析（不写死每局句柄）。</summary>
    public static PublishedSequence ForOwnerKind(
        string sequenceTypeId,
        string ownerKindId,
        IReadOnlyList<string> elementKindIds,
        int minCount,
        int? maxCount) =>
        new(sequenceTypeId, null, ownerKindId ?? throw new ArgumentNullException(nameof(ownerKindId)),
            elementKindIds, minCount, maxCount);
}

/// <summary>
/// 发布为协议 <c>group</c>：members 带 <c>multiplicity</c>（protocol.md:96）。引擎集合的成员唯一，
/// 因此**无法表达**“同一实例出现多次”的分组（能力边界，见报告）；需要该形状时必须报告缺口，
/// 不得靠重复 append 掩盖。
///
/// <see cref="OwnerKindId"/> 而不是一个写死的公开引用：公开句柄由会话内绑定决定（W-02），
/// 所以 owner 只能按**种类**解析——该种类在本快照里必须**恰好有一个**可见实例（与 <c>pile_ref</c> 的唯一匹配同理），
/// 否则拒绝发布而不是猜一个。
///
/// <see cref="MinCount"/>/<see cref="MaxCount"/> 来自**引擎声明**（集合的容量是规则，不是隐藏状态；
/// <c>int.MaxValue</c> 表示未声明上限 ⇒ <see cref="MaxCount"/> 为 null），并覆盖"容量/槽位"这类公开事实。
/// </summary>
public sealed record PublishedGroup(
    string GroupTypeId,
    string OwnerKindId,
    IReadOnlyList<string> MemberKindIds,
    int MinCount,
    int? MaxCount);

/// <summary>
/// 一个引擎集合的处置：pile / sequence / group 三种公开块之一，或**显式**不发布。
/// 同一集合只能选一种（protocol.md:94：牌堆成员以 piles 为唯一事实来源，不在 relations/groups/sequences 重复表达）。
/// 引擎的 <c>MembershipGroup</c> 是内部互斥成员组，**不是**协议的 <c>Group</c>，不得冒充。
/// </summary>
public sealed class CollectionDisposition
{
    private CollectionDisposition(PublishedPile? pile, PublishedSequence? sequence, PublishedGroup? group,
        string? skipRuleId, string? skipReason)
    {
        Pile = pile;
        Sequence = sequence;
        Group = group;
        SkipRuleId = skipRuleId;
        SkipReason = skipReason;
    }

    public PublishedPile? Pile { get; }

    public PublishedSequence? Sequence { get; }

    public PublishedGroup? Group { get; }

    public string? SkipRuleId { get; }

    public string? SkipReason { get; }

    public bool IsPublished => Pile is not null || Sequence is not null || Group is not null;

    /// <summary>该集合落在哪个协议块（<c>piles</c>/<c>sequences</c>/<c>groups</c>/未发布）。</summary>
    public string BlockName => Pile is not null ? "piles"
        : Sequence is not null ? "sequences"
        : Group is not null ? "groups"
        : "not-published";

    public static CollectionDisposition AsPile(PublishedPile pile) =>
        new(pile ?? throw new ArgumentNullException(nameof(pile)), null, null, null, null);

    public static CollectionDisposition AsSequence(PublishedSequence sequence) =>
        new(null, sequence ?? throw new ArgumentNullException(nameof(sequence)), null, null, null);

    public static CollectionDisposition AsGroup(PublishedGroup group) =>
        new(null, null, group ?? throw new ArgumentNullException(nameof(group)), null, null);

    public static CollectionDisposition Skip(string ruleId, string reason)
    {
        if (string.IsNullOrWhiteSpace(ruleId)) throw new ArgumentException("不发布必须带规则号", nameof(ruleId));
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("不发布必须带理由", nameof(reason));
        return new CollectionDisposition(null, null, null, ruleId, reason);
    }
}

/// <summary>一个公开关系类型的声明（protocol.md §1 第 41 行）。position_schema 为 null：wire 省略 position。</summary>
public sealed record PublicRelationRule(
    string TypeId,
    IReadOnlyList<string> SourceKindIds,
    IReadOnlyList<string> TargetKindIds);

/// <summary>一个公开动作类型的声明；<see cref="Parameters"/> 按引擎参数槽索引映射到公开 field_id。
/// <see cref="PublicActionField.Required"/> 决定它在目录里进 required_fields 还是 optional_fields（protocol.md:98：
/// 执行所需字段必须 known）。</summary>
public sealed record PublicActionRule(
    string ActionTypeId,
    IReadOnlyDictionary<int, PublicActionField> Parameters,
    bool NetDeclared = false);

/// <summary>动作参数 → 公开字段。<see cref="CategoryValues"/> 与 <see cref="PublishedField"/> 同义。
/// <see cref="TargetKindIds"/> 是该参数**可能引用到的全部公开种类**的显式声明（决策域可以有多种：
/// 只声明一种会让真实候选被判成类型错，声明全集则是过近似——两者都不能靠猜）。</summary>
public sealed record PublicActionField(
    string FieldId,
    PublicValueKind Kind,
    IReadOnlyDictionary<string, string>? CategoryValues = null,
    bool Required = false,
    IReadOnlyList<string>? TargetKindIds = null);

/// <summary>一个公开终局原因的声明：terminal_reasons 的 id/outcome 与 facts 字段表。</summary>
public sealed record PublicTerminalRule(
    string ReasonId,
    string Outcome,
    IReadOnlyDictionary<string, Disposition<PublishedField>> Fields);
