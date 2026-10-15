using CoreTheSpire.Engine;

namespace NetTheSpire.Projection;

/// <summary>
/// 一个实体类型的内容标识：哪个字段承载内容 ID，以及**已声明内容 ID 目录**。
///
/// 这是规则提供方（base/mod）补充的**不可变包元数据**之一：内容 ID 是作者声明的值，
/// 内核的 <see cref="FieldDefinition"/> 只说"这是一个 string 字段"，无法静态枚举它的取值，
/// 而目录的 <c>content_types</c> 必须闭合（protocol.md §1 第 30/50 行）。
/// 它不是"逐字段登记表"：普通字段、动作、枚举与引用都由声明派生，只有内容 ID 目录走这里。
/// </summary>
public sealed record PublicContentRule(string FieldName, IReadOnlyList<string> ContentIds);

/// <summary>引擎集合在公开协议里的用途（protocol.md §2 第 83–98 行只承认这三种块 + 不发布）。</summary>
public enum PublicCollectionUse
{
    /// <summary>显式不发布（必须带规则号与理由，不是静默丢弃）。</summary>
    NotPublished = 0,

    /// <summary>协议 <c>piles</c>：成员是牌/卡实例，牌堆种类由本集合专属的实体种类决定。</summary>
    Pile = 1,

    /// <summary>协议 <c>groups</c>：有 owner 的无序归组（药水栏、长期牌组、遗物栏）。</summary>
    Group = 2,

    /// <summary>协议 <c>sequences</c>：公开有序流程。owner 按**公开种类**在本快照里唯一解析
    /// （公开句柄由会话内绑定决定，派生层不写死每局句柄；见 <see cref="PublishedSequence"/>）。</summary>
    Sequence = 3,
}

/// <summary>
/// 一个引擎集合的公开用途。集合本身（容量、身份/顺序可见性）由引擎声明读取，不在这里重抄。
/// </summary>
public sealed record PublicCollectionRule
{
    private PublicCollectionRule(
        PublicCollectionUse use,
        string? pileKindEngineTypeId,
        PublicPositionSchema? positionSchema,
        string? ownerKindEngineTypeId,
        string? skipRuleId,
        string? skipReason)
    {
        Use = use;
        PileKindEngineTypeId = pileKindEngineTypeId;
        PositionSchema = positionSchema;
        OwnerKindEngineTypeId = ownerKindEngineTypeId;
        SkipRuleId = skipRuleId;
        SkipReason = skipReason;
    }

    public PublicCollectionUse Use { get; }

    /// <summary>发布为 pile 时，用哪个人工实体种类当 <c>pile_ref</c>（该种类在本快照里必须恰好有一个实例）。</summary>
    public string? PileKindEngineTypeId { get; }

    /// <summary>牌堆的公开位置语义；null 表示该牌堆没有公开位置（protocol.md §2 第 83 行）。</summary>
    public PublicPositionSchema? PositionSchema { get; }

    /// <summary>发布为 group 或 sequence 时 owner 的实体种类（在该集合的每个快照里都必须唯一解析，
    /// 不猜一个；协议块由 <see cref="Use"/> 决定）。</summary>
    public string? OwnerKindEngineTypeId { get; }

    public string? SkipRuleId { get; }

    public string? SkipReason { get; }

    public static PublicCollectionRule Pile(string pileKindEngineTypeId, PublicPositionSchema? positionSchema = null) =>
        new(PublicCollectionUse.Pile,
            pileKindEngineTypeId ?? throw new ArgumentNullException(nameof(pileKindEngineTypeId)),
            positionSchema, null, null, null);

    public static PublicCollectionRule Group(string ownerKindEngineTypeId) =>
        new(PublicCollectionUse.Group, null, null,
            ownerKindEngineTypeId ?? throw new ArgumentNullException(nameof(ownerKindEngineTypeId)), null, null);

    /// <summary>
    /// 发布为协议 <c>sequence</c>（公开有序流程）。owner 只声明**公开种类**：正式派生路径下公开句柄由会话内
    /// 绑定决定（W-02），所以不能在声明里写死每局句柄；该种类在本快照里必须恰好有一个可见实例，否则拒绝发布。
    /// 写死 owner 公开引用的旧 0.1 路径保留在 <see cref="PublishedSequence.ForPublicRef"/>，不经过本工厂。
    /// </summary>
    public static PublicCollectionRule Sequence(string ownerKindEngineTypeId) =>
        new(PublicCollectionUse.Sequence, null, null,
            ownerKindEngineTypeId ?? throw new ArgumentNullException(nameof(ownerKindEngineTypeId)), null, null);

    public static PublicCollectionRule NotPublished(string ruleId, string reason)
    {
        if (string.IsNullOrWhiteSpace(ruleId)) throw new ArgumentException("不发布必须带规则号", nameof(ruleId));
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("不发布必须带理由", nameof(reason));
        return new PublicCollectionRule(PublicCollectionUse.NotPublished, null, null, null, ruleId, reason);
    }
}

/// <summary>
/// 一个"已知位置"事实类（H-01/H-02）：事实实体引用真实卡牌实例，并给出牌堆标记与绝对位置。
///
/// 位置由规则包维护；映射层只读当前快照，绝不从隐藏实时索引重算。<see cref="PileTypeByToken"/> 的键是
/// 事实字段里的**标记值**（包自己的词表），值是**引擎集合限定名**——派生时再翻译成公开 pile_type ID，
/// 因此提供方不需要知道 net 派生出来的公开 ID。
/// </summary>
public sealed record PublicKnownPositionRole(
    string FactKindId,
    string CardFieldName,
    string PileFieldName,
    string PositionFieldName,
    IReadOnlyDictionary<string, string> PileTypeByCollection);

/// <summary>
/// 规则提供方随构建给出的**不可变补充元数据**：只补通用声明缺的游戏含义。
///
/// 允许的内容**仅限**四类（见 design.md §7.1/§7.2 与负责人复核第 4 条）：
/// <list type="number">
/// <item>内容 ID 目录（<see cref="Content"/>）；</item>
/// <item>集合用途（<see cref="Collections"/>）；</item>
/// <item>终局胜负解释（<see cref="TerminalOutcomes"/>：终局名字不同不等于 win/loss）；</item>
/// <item>已知位置角色（<see cref="KnownPositions"/>）。</item>
/// </list>
///
/// 它**不**列出普通字段、动作、参数、枚举或引用——那些一律由 <c>RuleSet</c> 声明与所选投影派生；
/// 也不承载可变游戏状态、不重复内容清单以外的实体表。校验在
/// <see cref="DerivedPublicProfile"/> 里逐条对照真实声明完成：未知名字、缺条目、指向未投影种类的条目都显式失败。
/// </summary>
public sealed record PublicBuildMetadata(
    /// <summary>提供方的**静态**构建身份（不含每局 seed）；进入公开 ruleset_id，规则/内容/配置变化必须换它。</summary>
    string StaticBuildIdentity,
    IReadOnlyDictionary<string, PublicContentRule> Content,
    IReadOnlyDictionary<string, PublicCollectionRule> Collections,
    IReadOnlyDictionary<string, string> TerminalOutcomes,
    IReadOnlyDictionary<string, PublicKnownPositionRole> KnownPositions)
{
    public static PublicBuildMetadata Create(
        string staticBuildIdentity,
        IReadOnlyDictionary<string, PublicContentRule>? content = null,
        IReadOnlyDictionary<string, PublicCollectionRule>? collections = null,
        IReadOnlyDictionary<string, string>? terminalOutcomes = null,
        IReadOnlyDictionary<string, PublicKnownPositionRole>? knownPositions = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(staticBuildIdentity);
        return new PublicBuildMetadata(
            staticBuildIdentity,
            content ?? new Dictionary<string, PublicContentRule>(StringComparer.Ordinal),
            collections ?? new Dictionary<string, PublicCollectionRule>(StringComparer.Ordinal),
            terminalOutcomes ?? new Dictionary<string, string>(StringComparer.Ordinal),
            knownPositions ?? new Dictionary<string, PublicKnownPositionRole>(StringComparer.Ordinal));
    }
}
