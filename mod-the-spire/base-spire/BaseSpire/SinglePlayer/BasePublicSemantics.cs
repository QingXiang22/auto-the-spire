using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.SinglePlayer;

/// <summary>
/// 一个实体类型的**内容 ID 目录**：哪个字段承载内容 ID，以及本构建**实际加载**的内容 ID 全集。
///
/// 这是"不能从通用声明推导"的含义之一：Engine 的 `FieldDefinition` 只说"这是一个 string 字段"，
/// 静态枚举不出它的取值，而公开协议的 `content_types` 必须是一个**闭合**目录。它不是逐字段登记表：
/// 普通字段、枚举、引用、动作与参数一律由 `RuleSet` 声明与所选投影派生。
/// </summary>
public sealed record BasePublicContentCatalog(string FieldName, IReadOnlyList<string> ContentIds);

/// <summary>引擎集合在公开协议里的用途（protocol.md §2 只承认牌堆/归组/有序流程三种块 + 具名不发布）。</summary>
public enum BasePublicCollectionUse
{
    /// <summary>显式不发布：必须带规则号与理由，不是静默丢弃。</summary>
    NotPublished = 0,

    /// <summary>协议 `piles`：成员是牌/卡实例，牌堆种类由本集合专属的实体种类唯一确定。</summary>
    Pile = 1,

    /// <summary>协议 `groups`：有唯一 owner 实体的无序归组。</summary>
    Group = 2,

    /// <summary>
    /// 协议 `sequences`：有唯一 owner 实体的**公开有序流程**。元素的顺序本身就是公开游戏语义，
    /// 因此承载不了顺序的载荷（无序归组）不得用来表达它；反过来也不得把引擎声明隐藏的成员/顺序
    /// 塞进有序载荷。
    /// </summary>
    Sequence = 3,
}

/// <summary>公开牌堆的**绝对位置**坐标语义（只对"由事实公开的绝对位置"有意义，不是集合内部顺序）。</summary>
public sealed record BasePublicPositionSemantics(string Scope, string Origin, int First, int? Max)
{
    /// <summary>牌的公开位置：从顶部 0 起算、不设上界（抽牌堆/弃牌堆用它，手牌/消耗/出牌区没有公开位置语义）。</summary>
    public static readonly BasePublicPositionSemantics TopOfPile = new("card", "top", 0, null);
}

/// <summary>
/// 一个引擎集合的公开用途。**集合自身的容量与身份/顺序可见性不在这里重抄**（那是引擎声明，公开侧直接读）。
/// </summary>
public sealed record BasePublicCollectionSemantics
{
    private BasePublicCollectionSemantics(
        BasePublicCollectionUse use,
        string? pileKindEngineTypeId,
        BasePublicPositionSemantics? position,
        string? ownerKindEngineTypeId,
        string? skipRuleId,
        string? skipReason)
    {
        Use = use;
        PileKindEngineTypeId = pileKindEngineTypeId;
        Position = position;
        OwnerKindEngineTypeId = ownerKindEngineTypeId;
        SkipRuleId = skipRuleId;
        SkipReason = skipReason;
    }

    public BasePublicCollectionUse Use { get; }

    /// <summary>发布为 pile 时，用哪个人工实体种类当 `pile_ref`（该种类必须唯一解析）。</summary>
    public string? PileKindEngineTypeId { get; }

    /// <summary>牌堆的公开位置语义；null 表示该牌堆没有公开位置。</summary>
    public BasePublicPositionSemantics? Position { get; }

    /// <summary>发布为 group/sequence 时的 owner **实体种类**（同样必须唯一解析）。</summary>
    public string? OwnerKindEngineTypeId { get; }

    public string? SkipRuleId { get; }

    public string? SkipReason { get; }

    public static BasePublicCollectionSemantics Pile(string pileKindEngineTypeId,
        BasePublicPositionSemantics? position = null) =>
        new(BasePublicCollectionUse.Pile,
            pileKindEngineTypeId ?? throw new ArgumentNullException(nameof(pileKindEngineTypeId)),
            position, null, null, null);

    public static BasePublicCollectionSemantics Group(string ownerKindEngineTypeId) =>
        new(BasePublicCollectionUse.Group, null, null,
            ownerKindEngineTypeId ?? throw new ArgumentNullException(nameof(ownerKindEngineTypeId)), null, null);

    /// <summary>
    /// 发布为 sequence 时的 owner **实体种类**（引擎限定名，运行时按"该种类唯一可见实例"解析；
    /// 不写死每局的公开句柄）。元素顺序按集合的真实顺序发布。
    /// </summary>
    public static BasePublicCollectionSemantics Sequence(string ownerKindEngineTypeId) =>
        new(BasePublicCollectionUse.Sequence, null, null,
            ownerKindEngineTypeId ?? throw new ArgumentNullException(nameof(ownerKindEngineTypeId)), null, null);

    public static BasePublicCollectionSemantics NotPublished(string ruleId, string reason)
    {
        if (string.IsNullOrWhiteSpace(ruleId)) throw new ArgumentException("不发布必须带规则号", nameof(ruleId));
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("不发布必须带理由", nameof(reason));
        return new BasePublicCollectionSemantics(BasePublicCollectionUse.NotPublished, null, null, null, ruleId, reason);
    }
}

/// <summary>
/// 终局的**胜负解释**。终局名字不同不等于 win/loss：`base-spire:Victory` 只在原版终局事件里发生，
/// 而"打完配置的房间数"不是原版胜负规则。名字与含义必须由规则提供方给出一次。
/// </summary>
public enum BasePublicTerminalOutcome
{
    Win = 0,
    Loss = 1,
    Other = 2,
}

public static class BasePublicTerminalOutcomeNames
{
    /// <summary>公开 wire 上的胜负标记（协议只承认这三个值，不由名字猜）。</summary>
    public static string WireName(this BasePublicTerminalOutcome outcome) => outcome switch
    {
        BasePublicTerminalOutcome.Win => "win",
        BasePublicTerminalOutcome.Loss => "loss",
        _ => "other",
    };
}

/// <summary>
/// 一个"已知位置"角色（H-01/H-02）：哪类事实实体通过哪三个字段引用真实卡牌实例、牌堆标记与绝对位置，
/// 以及**标记值 → 引擎集合限定名**的词表。
///
/// 已知位置的值与失效仍由包程序维护（当前只有抽牌堆顶的 `draw` 标记），metadata 只指定角色；
/// 消费者读到的是公开事实，不得从隐藏实时索引重算。
/// </summary>
public sealed record BasePublicKnownPositionRole(
    string FactTypeId,
    string CardFieldName,
    string PileFieldName,
    string PositionFieldName,
    IReadOnlyDictionary<string, string> PileCollectionByToken);

/// <summary>
/// **实际加载内容的内容 ID 全集**（来自组装结果，不是源文本清单）：未支持/未降级的内容不在里面。
/// </summary>
public sealed record BasePublicContentSource(
    IReadOnlyList<string> CardIds,
    IReadOnlyList<string> PowerIds,
    IReadOnlyList<string> EnemyIds,
    IReadOnlyList<string> RelicIds)
{
    public IReadOnlyList<string> PotionIds { get; init; } = [];
}

/// <summary>
/// 正式 base 工厂随构建给出的**不可变游戏语义元数据**。
///
/// 只补通用 `RuleSet` 声明缺的、**不能仅凭名字推导**的四类含义（见 net design §7.1/§7.2）：
/// <list type="number">
/// <item>内容 ID 目录（<see cref="Content"/>）；</item>
/// <item>集合用途与位置/归属（<see cref="Collections"/>）；</item>
/// <item>已知位置角色（<see cref="KnownPositions"/>）；</item>
/// <item>终局胜负解释（<see cref="TerminalOutcomes"/>）。</item>
/// </list>
///
/// 它**不**列普通字段、动作、参数、枚举或引用（那些由所选投影派生），**不**承载可变游戏状态，
/// **不含每局 seed**。它由 <see cref="BasePublicSemanticsBuilder.Build"/> 逐条对照真实规则集与
/// 实际加载内容构造成只读字典；<see cref="Digest"/> 进入公开构建身份。本类型与 net 无关：
/// base 不引用 net 的 DTO/程序集，跨包翻译由公开出口一侧完成。
/// </summary>
public sealed record BasePublicSemantics(
    string ProjectionId,
    string ParticipantRole,
    IReadOnlyDictionary<string, BasePublicContentCatalog> Content,
    IReadOnlyDictionary<string, BasePublicCollectionSemantics> Collections,
    IReadOnlyDictionary<string, BasePublicTerminalOutcome> TerminalOutcomes,
    IReadOnlyDictionary<string, BasePublicKnownPositionRole> KnownPositions)
{
    private string? _digest;

    /// <summary>
    /// 元数据自身的稳定摘要：同配置逐字节相同，内容/语义变化即变化，不含每局 seed。
    /// 工厂把它折进 <c>BaseSpireBuildDescription.PublicBuildId</c>。
    /// </summary>
    public string Digest => _digest ??= BasePublicSemanticsBuilder.Digest(CanonicalText());

    /// <summary>规范化文本（排序、单行一项）：既是摘要输入，也是证据里可逐行核对的形态。</summary>
    public string CanonicalText()
    {
        var lines = new List<string>
        {
            $"projection={ProjectionId}",
            $"role={ParticipantRole}",
        };
        lines.AddRange(Content.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"content={pair.Key}|{pair.Value.FieldName}|{string.Join(",", pair.Value.ContentIds)}"));
        lines.AddRange(Collections.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"collection={Describe(pair.Key, pair.Value)}"));
        lines.AddRange(TerminalOutcomes.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"terminal={pair.Key}|{pair.Value.WireName()}"));
        lines.AddRange(KnownPositions.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"known-position={pair.Key}|{pair.Value.CardFieldName}|{pair.Value.PileFieldName}"
                + $"|{pair.Value.PositionFieldName}|{string.Join(",", pair.Value.PileCollectionByToken
                    .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                    .Select(entry => $"{entry.Key}={entry.Value}"))}"));
        return string.Join("\n", lines);
    }

    private static string Describe(string collectionId, BasePublicCollectionSemantics semantics) =>
        semantics.Use switch
        {
            BasePublicCollectionUse.Pile => $"{collectionId}|pile|{semantics.PileKindEngineTypeId}|"
                + (semantics.Position is { } position
                    ? $"position={position.Scope}/{position.Origin}/{position.First}/"
                        + (position.Max is { } max ? max.ToString() : "-")
                    : "position=-"),
            BasePublicCollectionUse.Group => $"{collectionId}|group|owner={semantics.OwnerKindEngineTypeId}",
            BasePublicCollectionUse.Sequence =>
                $"{collectionId}|sequence|owner={semantics.OwnerKindEngineTypeId}",
            _ => $"{collectionId}|not-published|{semantics.SkipRuleId}",
        };
}

/// <summary>
/// 把 base 的**游戏语义意图**（这里唯一手写的部分：哪个集合是什么、哪个字段是内容 ID、
/// 哪个终局是胜是负、哪类事实表达已知位置）与**真实规则集 + 实际加载内容**闭合起来。
///
/// 闭合是构造期强制的，不是注释约定：
/// <list type="bullet">
/// <item>规则集里的**每一个**集合都必须有一条用途声明，缺一条就显式失败（不允许静默丢弃公开事实）；</item>
/// <item>具名不发布只能用于"成员种类不在所选投影里"的集合：成员种类在投影里就说明投影确实发布成员事实；</item>
/// <item>引擎声明公开成员身份与公开顺序的集合不得发布为无序 group（会丢掉真实顺序），必须 sequence；</item>
/// <item>sequence 不得用于引擎声明隐藏成员/顺序的集合（不得借有序载荷泄漏隐藏成员）；</item>
/// <item>每个终局结果都必须有胜负解释（不允许按名字猜 win/loss）；</item>
/// <item>内容目录的字段必须真的存在于该类型且是 string；内容 ID 不重复、不为空；</item>
/// <item>pile 的牌堆种类、group/sequence 的 owner 种类、已知位置事实种类都必须**在所选投影里**（否则消费者无法解析）；</item>
/// <item>已知位置的角色字段必须真的存在且形状相符（卡牌引用/字符串标记/整数位置）；</item>
/// <item>已知位置的标记只能指向一个**发布为 pile** 的集合。</item>
/// </list>
/// </summary>
public static class BasePublicSemanticsBuilder
{
    /// <summary>内容 ID 目录的意图：实体类型 → 承载内容 ID 的字段 → 该目录的实际内容来源。</summary>
    private static readonly (string TypeName, string FieldName,
        Func<BasePublicContentSource, IReadOnlyList<string>> Ids)[] ContentIntents =
    [
        ("Card", "DefinitionId", source => source.CardIds),
        ("Power", "DefinitionId", source => source.PowerIds),
        ("Monster", "DefinitionId", source => source.EnemyIds),
        ("Relic", "DefinitionId", source => source.RelicIds),
        ("Potion", "DefinitionId", source => source.PotionIds),
    ];

    /// <summary>集合用途的意图（按集合的包内局部名索引；类型名同为包内局部名，构造时再限定）。</summary>
    private static readonly IReadOnlyDictionary<string, (BasePublicCollectionUse Use, string? PileKind, string? Owner,
        BasePublicPositionSemantics? Position, string? SkipRuleId, string? SkipReason)> CollectionIntents =
        new Dictionary<string, (BasePublicCollectionUse, string?, string?, BasePublicPositionSemantics?, string?, string?)>(
            StringComparer.Ordinal)
        {
            // 每个牌区一个 pile_type；kind 唯一匹配一个牌区实体（protocol.md §2 第 83 行）。
            // 位置只对抽牌堆/弃牌堆声明：由事实公开的绝对位置从堆顶 0 起算；手牌/消耗/出牌区没有公开位置语义。
            ["Hand"] = (BasePublicCollectionUse.Pile, "HandPile", null, null, null, null),
            ["DrawPile"] = (BasePublicCollectionUse.Pile, "DrawPileEntity", null,
                BasePublicPositionSemantics.TopOfPile, null, null),
            ["DiscardPile"] = (BasePublicCollectionUse.Pile, "DiscardPileEntity", null,
                BasePublicPositionSemantics.TopOfPile, null, null),
            ["ExhaustPile"] = (BasePublicCollectionUse.Pile, "ExhaustPileEntity", null, null, null, null),
            ["InPlay"] = (BasePublicCollectionUse.Pile, "InPlayPileEntity", null, null, null, null),

            // 有序流程（协议 sequence）：成员种类在投影里、引擎声明 identity/size/order 公开，且顺序本身就是
            // 公开游戏事实（出牌顺序、敌人行动顺序、事实记载顺序）。凡是这类集合都必须用能保留真实顺序的
            // 载荷：写成具名不发布与"投影确实发布成员/顺序"冲突，写成 Group 会丢掉声明的顺序。
            // owner 是**领域实体的种类**（运行时按该种类唯一可见实例解析），不写死每局公开句柄：
            // 战斗作用域的流程锚在唯一的战斗参与者实体（Player，每场战斗的战斗副本），整局作用域的流程锚在 Run 单例。
            ["PlayedThisTurn"] = (BasePublicCollectionUse.Sequence, null, "Player", null, null, null),
            ["SharedRelicBagCommon"] = (BasePublicCollectionUse.NotPublished, null, null, null,
                "BASE-INTERNAL-RELIC-BAG", "内部随机遗物袋不发布未来奖励。"),
            ["SharedRelicBagUncommon"] = (BasePublicCollectionUse.NotPublished, null, null, null,
                "BASE-INTERNAL-RELIC-BAG", "内部随机遗物袋不发布未来奖励。"),
            ["SharedRelicBagRare"] = (BasePublicCollectionUse.NotPublished, null, null, null,
                "BASE-INTERNAL-RELIC-BAG", "内部随机遗物袋不发布未来奖励。"),
            ["SharedRelicBagShop"] = (BasePublicCollectionUse.NotPublished, null, null, null,
                "BASE-INTERNAL-RELIC-BAG", "内部随机遗物袋不发布未来奖励。"),
            ["SharedRelicBagEvent"] = (BasePublicCollectionUse.NotPublished, null, null, null,
                "BASE-INTERNAL-RELIC-BAG", "内部随机遗物袋不发布未来奖励。"),
            ["SharedRelicBagAncient"] = (BasePublicCollectionUse.NotPublished, null, null, null,
                "BASE-INTERNAL-RELIC-BAG", "内部随机遗物袋不发布未来奖励。"),
            ["RunEventQueue"] = (BasePublicCollectionUse.NotPublished, null, null, null,
                "BASE-INTERNAL-RUN-QUEUE", "源事件顺序不发布未来房间。"),
            // ENV-T09-unknown-room-modules-20261012：已访问事件身份（源 `RunState.VisitedEventIds`）。
            // 成员种类 `RunRoomIdentity` 不在所选投影里，没有任何已发布载荷能承载它，因此具名不发布：
            // 发布它等于公开"后续不会遇到哪些事件"，与事件队列同一条理由。
            ["RunVisitedEvents"] = (BasePublicCollectionUse.NotPublished, null, null, null,
                "BASE-INTERNAL-RUN-QUEUE",
                "已访问事件身份集合（源 RunState.VisitedEventIds）：成员种类不在公开投影里，"
                + "发布它等于公开后续不会再遇到哪些事件。当前事件的公开事实是 Run.CurrentEventId。"),
            ["RunSharedAncients"] = (BasePublicCollectionUse.NotPublished, null, null, null,
                "BASE-INTERNAL-RUN-QUEUE", "共享古代人库存不发布。"),
            ["PlayerRelicBagCommon"] = (BasePublicCollectionUse.NotPublished, null, null, null,
                "BASE-INTERNAL-RELIC-BAG", "玩家随机遗物库存不发布未来奖励。"),
            ["PlayerRelicBagUncommon"] = (BasePublicCollectionUse.NotPublished, null, null, null,
                "BASE-INTERNAL-RELIC-BAG", "玩家随机遗物库存不发布未来奖励。"),
            ["PlayerRelicBagRare"] = (BasePublicCollectionUse.NotPublished, null, null, null,
                "BASE-INTERNAL-RELIC-BAG", "玩家随机遗物库存不发布未来奖励。"),
            ["PlayerRelicBagShop"] = (BasePublicCollectionUse.NotPublished, null, null, null,
                "BASE-INTERNAL-RELIC-BAG", "玩家随机遗物库存不发布未来奖励。"),
            ["PowerCardSelection"] = (BasePublicCollectionUse.NotPublished, null, null, null,
                "BASE-INTERNAL-POWER-SELECTION", "内部Power候选快照，不发布中间牌区。"),
            ["CardPlayHistory"] = (BasePublicCollectionUse.NotPublished, null, null, null,
                "BASE-INTERNAL-PLAY-HISTORY", "内部执行事件记录；事件种类不在公开投影，公开牌序列继续由 PlayedThisTurn 提供。"),
            ["Monsters"] = (BasePublicCollectionUse.Sequence, null, "Player", null, null, null),
            ["GeneratedCardOffers"] = (BasePublicCollectionUse.Sequence, null, "Player", null, null, null),
            ["RunActMetadata"] = (BasePublicCollectionUse.NotPublished, null, null, null, "base-spire:INTERNAL-ACT-PLAN", "预生成的后续幕首领与古代人身份，不发布未来内容。"),
            ["RunActNormalQueue"] = (BasePublicCollectionUse.NotPublished, null, null, null, "base-spire:INTERNAL-ACT-PLAN", "各幕预生成普通遭遇队列。"),
            ["RunActEliteQueue"] = (BasePublicCollectionUse.NotPublished, null, null, null, "base-spire:INTERNAL-ACT-PLAN", "各幕预生成精英遭遇队列。"),
            ["RunActEventQueue"] = (BasePublicCollectionUse.NotPublished, null, null, null, "base-spire:INTERNAL-ACT-PLAN", "各幕预生成事件队列。"),
            ["RunActSharedAncients"] = (BasePublicCollectionUse.NotPublished, null, null, null, "base-spire:INTERNAL-ACT-PLAN", "开局分配的后续幕共享古代人子集。"),
            ["SlipperyBridgeSkippedCards"] = (BasePublicCollectionUse.NotPublished, null, null, null,
                "base-spire:N-02-INTERNAL-EVENT-HISTORY", "事件内部的已预览实例集合；当前候选由Run.SlipperyBridgeCard公开，不发布内部重抽过滤集合。"),
            ["GenerationPool"] = (BasePublicCollectionUse.NotPublished, null, null, null,
                "BASE-INTERNAL-GENERATION-POOL", "完整生成池的临时洗牌顺序不公开。"),
            ["OrbQueue"] = (BasePublicCollectionUse.Sequence, null, "Player", null, null, null),
            ["OstyPets"] = (BasePublicCollectionUse.Sequence, null, "Player", null, null, null),
            ["Known"] = (BasePublicCollectionUse.Sequence, null, "Player", null, null, null),
            ["RunStates"] = (BasePublicCollectionUse.Sequence, null, "Run", null, null, null),

            // T05 地图（只在 `BaseSpireScope.DevMap` 的包里存在；默认 scope 没有这个集合，多余的 intent 无副作用）：
            // 引擎声明 identity+order 公开（有序集合），顺序本身就是公开游戏事实（列优先物化顺序），
            // 成员种类 `MapNode` 已进观看者投影 ⇒ 必须用 Sequence，不能只具名不发布。
            ["MapNodes"] = (BasePublicCollectionUse.Sequence, null, "Run", null, null, null),

            // 长期牌组/遗物栏：声明的成员身份与顺序都是公开的（identity/size/order=true）。成员是真实实例
            // （卡牌/遗物），owner 是整局；公开侧靠它区分"长期实例"与"战斗副本"并保留获得顺序。
            ["PotionBelt"] = (BasePublicCollectionUse.Sequence, null, "Run", null, null, null),
            ["RewardPotions"] = (BasePublicCollectionUse.Sequence, null, "Run", null, null, null),
            ["RewardCards"] = (BasePublicCollectionUse.Sequence, null, "Run", null, null, null),
            ["EventCardOffers"] = (BasePublicCollectionUse.Sequence, null, "Run", null, null, null),
            ["LongTermDeck"] = (BasePublicCollectionUse.Sequence, null, "Run", null, null, null),
            ["Relics"] = (BasePublicCollectionUse.Sequence, null, "Run", null, null, null),

            // ENV-T09.2 商店货架：商品是**当前公开事实**（玩家必须看见能买什么、价格与是否已售才能决策），
            // 成员种类 `ShopOffer` 进观看者投影，owner 是整局 ⇒ 用 Sequence（身份与顺序都公开）。
            // `SourceName` 是源清单身份（无色源成员/回退遗物），与 `DefinitionId` 一起构成完整身份。
            ["ShopOffers"] = (BasePublicCollectionUse.Sequence, null, "Run", null, null, null),

            // 遭遇池/队列：成员种类**不在所选公开投影里**，没有任何已发布载荷能承载这些成员，因此只能具名
            // 不发布。这不是"已经由别的载荷完整表达"，而是**受阻项**（最小可表示形状与受影响范围见本包交接：
            // 把 EncounterPoolEntry/EncounterQueueEntry 放进投影并只公开 EncounterId，属 T05/规则包范围）。
            ["EncounterPool"] = (BasePublicCollectionUse.NotPublished, null, null, null,
                "base-spire:N-02-BLOCKED-CONTENT-POOL",
                "受阻：遭遇池的成员种类（EncounterPoolEntry）不在所选公开投影里，没有任何已发布载荷能承载其"
                + "成员，成员身份无法唯一解析 ⇒ 只能具名不发布。当前公开事实是当前房间的遭遇（Run.EncounterId）；"
                + "要发布池成员需先把成员种类放进投影（最小形状：只公开 EncounterId/Kind）。"),
            ["EncounterQueue"] = (BasePublicCollectionUse.NotPublished, null, null, null,
                "base-spire:N-02-BLOCKED-INTERNAL-QUEUE",
                "受阻：整幕队列的成员种类（EncounterQueueEntry）不在所选公开投影里、条目 `Index` 字段隐藏；"
                + "发布成员身份等于公开**后续房间**的遭遇顺序，而当前范围没有地图/房间顺序的公开语义（T05）"
                + "⇒ 只能具名不发布。最小形状：投影 EncounterQueueEntry 并公开 EncounterId（Index 保持隐藏），"
                + "由 T05 决定是否发布为 sequence。"),
            ["EliteEncounterQueue"] = (BasePublicCollectionUse.NotPublished, null, null, null,
                "base-spire:N-02-BLOCKED-ELITE-QUEUE",
                "受阻：精英遭遇队列的成员种类（EliteEncounterQueueEntry）不在所选公开投影里、条目 `Index` 字段隐藏；"
                + "它是普通队列的**独立兄弟**（源 `RoomSet.eliteEncounters`），发布成员身份等于公开**后续精英房**的"
                + "遭遇顺序 ⇒ 只能具名不发布。当前公开事实仍是本间房的遭遇（Run.EncounterId）。"),
        };

    /// <summary>终局胜负意图（按终局的包内局部名索引）。</summary>
    private static readonly IReadOnlyDictionary<string, BasePublicTerminalOutcome> TerminalIntents =
        new Dictionary<string, BasePublicTerminalOutcome>(StringComparer.Ordinal)
        {
            // 原版胜利只在最终 BOSS 之后的终局事件发生；"打完配置的房间数"不是原版胜负规则。
            ["Victory"] = BasePublicTerminalOutcome.Win,
            ["Defeat"] = BasePublicTerminalOutcome.Loss,
        };

    /// <summary>
    /// 已知位置角色意图：事实类型 → (卡牌字段, 牌堆字段, 位置字段) + **标记词表**（标记值 → 集合局部名）。
    /// 当前包只在"抽牌堆顶"写下事实（`SliceManifest.DrawPileName`），因此词表里只有这一个标记。
    /// </summary>
    private static readonly IReadOnlyDictionary<string, (string CardField, string PileField, string PositionField,
        IReadOnlyDictionary<string, string> PileByToken)> KnownPositionIntents =
        new Dictionary<string, (string, string, string, IReadOnlyDictionary<string, string>)>(StringComparer.Ordinal)
        {
            ["KnownCard"] = ("Card", "Pile", "Position",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [SliceManifest.DrawPileName] = "DrawPile",
                }),
        };

    public static BasePublicSemantics Build(
        RuleSet rules,
        string projectionId,
        string participantRole,
        BasePublicContentSource source)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(participantRole);
        ArgumentNullException.ThrowIfNull(source);

        var projection = rules.Projections.FirstOrDefault(item =>
                             string.Equals(item.QualifiedId, projectionId, StringComparison.Ordinal))
                         ?? throw new InvalidOperationException(
                             $"规则集没有投影 '{projectionId}'：公开语义元数据必须绑定一个真实投影");
        if (!string.Equals(projection.ParticipantRole, participantRole, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"投影 '{projectionId}' 声明给角色 '{projection.ParticipantRole}'，"
                + $"而元数据绑定的是 '{participantRole}'");
        }

        var included = projection.Types.Values.Where(item => item.Include)
            .Select(item => item.Type.QualifiedId)
            .ToHashSet(StringComparer.Ordinal);
        if (included.Count == 0)
        {
            throw new InvalidOperationException($"投影 '{projectionId}' 没有包含任何实体类型：无法发布任何公开观察");
        }

        var content = BuildContent(rules, included, projectionId, source);
        var collections = BuildCollections(rules, included);
        var terminals = BuildTerminals(rules);
        var knownPositions = BuildKnownPositions(rules, included, collections);

        return new BasePublicSemantics(
            projectionId,
            participantRole,
            new ReadOnlyDictionary<string, BasePublicContentCatalog>(content),
            new ReadOnlyDictionary<string, BasePublicCollectionSemantics>(collections),
            new ReadOnlyDictionary<string, BasePublicTerminalOutcome>(terminals),
            new ReadOnlyDictionary<string, BasePublicKnownPositionRole>(knownPositions));
    }

    // -- content ---------------------------------------------------------------------------------------------

    private static Dictionary<string, BasePublicContentCatalog> BuildContent(RuleSet rules, HashSet<string> included,
        string projectionId, BasePublicContentSource source)
    {
        var result = new Dictionary<string, BasePublicContentCatalog>(StringComparer.Ordinal);
        foreach (var (typeName, fieldName, ids) in ContentIntents)
        {
            var typeId = Qualify(SliceManifest.PackageId, typeName);
            var type = rules.TypeByQualifiedId(typeId);
            if (!type.TryFieldIndex(fieldName, out var fieldIndex) ||
                type.Fields[fieldIndex].Kind != FieldTypeKind.String)
            {
                throw new InvalidOperationException(
                    $"内容目录声明的 '{typeId}.{fieldName}' 不是该类型的 string 字段：不得凭名字指定内容字段");
            }

            if (!included.Contains(typeId))
            {
                throw new InvalidOperationException(
                    $"内容目录指向的实体类型 '{typeId}' 不在投影 '{projectionId}' 的公开范围里："
                    + "目录必须描述消费者真能看到的类型");
            }

            result[typeId] = new BasePublicContentCatalog(fieldName, CanonicalIds(typeId, fieldName, ids(source)));
        }

        return result;
    }

    private static IReadOnlyList<string> CanonicalIds(string typeId, string fieldName, IReadOnlyList<string> ids)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in ids)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new InvalidOperationException($"内容目录 '{typeId}.{fieldName}' 里有空内容 ID");
            }

            if (!seen.Add(id))
            {
                throw new InvalidOperationException($"内容目录 '{typeId}.{fieldName}' 里内容 ID '{id}' 重复");
            }
        }

        return Array.AsReadOnly(ids.Order(StringComparer.Ordinal).ToArray());
    }

    // -- collections -----------------------------------------------------------------------------------------

    private static Dictionary<string, BasePublicCollectionSemantics> BuildCollections(RuleSet rules,
        HashSet<string> included)
    {
        var result = new Dictionary<string, BasePublicCollectionSemantics>(StringComparer.Ordinal);
        foreach (var collection in rules.Collections.OrderBy(item => item.QualifiedId, StringComparer.Ordinal))
        {
            var local = LocalName(collection.QualifiedId);
            if (!CollectionIntents.TryGetValue(local, out var intent))
            {
                throw new InvalidOperationException(
                    $"引擎集合 '{collection.QualifiedId}' 没有公开用途声明：每条集合都必须发布或具名不发布"
                    + "（不允许静默丢弃公开事实）");
            }

            var elementType = rules.Type(collection.ElementTypeId).QualifiedId;
            var memberKindPublished = included.Contains(elementType);

            // 判别 1：具名不发布只能用在"成员种类根本不在所选投影里"这一种结构原因上。成员种类在投影里
            // ⇒ 投影确实发布成员身份（引擎声明的 identity/size/order 是公开可见性），用具名理由把它说成
            // "没有公开语义"就与实际声明冲突（这正是上一包被驳回的那条）。
            if (intent.Use == BasePublicCollectionUse.NotPublished && memberKindPublished)
            {
                throw new InvalidOperationException(
                    $"集合 '{collection.QualifiedId}' 声明为具名不发布，但它的成员种类 '{elementType}' 在所选"
                    + "公开投影里：投影确实发布成员事实，必须用 pile/sequence 表达，不得以具名理由丢弃");
            }

            // 判别 2：引擎声明为公开成员身份 + 公开顺序的集合不得落进无序载荷（group 会丢掉真实顺序）。
            if (intent.Use == BasePublicCollectionUse.Group
                && collection.IdentityVisible && collection.OrderVisible && memberKindPublished)
            {
                throw new InvalidOperationException(
                    $"集合 '{collection.QualifiedId}' 的成员身份与顺序在引擎声明里都是公开的（identity/order=true "
                    + "且成员种类在投影里），group 是无序载荷会丢掉真实顺序：必须用 sequence 保留公开顺序");
            }

            // 判别 3：sequence 是"成员与顺序都公开"的载荷，不得用在引擎声明隐藏成员/顺序的集合上，
            // 否则元数据会要求消费者发布引擎声明里隐藏的成员（大小/顺序隐藏的保证不得泄漏）。
            if (intent.Use == BasePublicCollectionUse.Sequence
                && !(collection.IdentityVisible && collection.OrderVisible))
            {
                throw new InvalidOperationException(
                    $"集合 '{collection.QualifiedId}' 声明为 sequence，但引擎声明里成员身份/顺序是隐藏的"
                    + $"（identity={collection.IdentityVisible} order={collection.OrderVisible}）："
                    + "有序公开载荷不得泄漏隐藏成员与内部顺序");
            }

            switch (intent.Use)
            {
                case BasePublicCollectionUse.Pile:
                {
                    var pileKind = Qualify(PackageOf(collection.QualifiedId), intent.PileKind!);
                    RequireIncluded(included, pileKind, collection.QualifiedId, "pile_ref 实体种类");
                    RequireIncluded(included, elementType, collection.QualifiedId, "成员种类");
                    result[collection.QualifiedId] = BasePublicCollectionSemantics.Pile(pileKind, intent.Position);
                    break;
                }

                case BasePublicCollectionUse.Group:
                {
                    var owner = Qualify(PackageOf(collection.QualifiedId), intent.Owner!);
                    RequireIncluded(included, owner, collection.QualifiedId, "group owner 种类");
                    RequireIncluded(included, elementType, collection.QualifiedId, "group 成员种类");
                    result[collection.QualifiedId] = BasePublicCollectionSemantics.Group(owner);
                    break;
                }

                case BasePublicCollectionUse.Sequence:
                {
                    var owner = Qualify(PackageOf(collection.QualifiedId), intent.Owner!);
                    RequireIncluded(included, owner, collection.QualifiedId, "sequence owner 种类");
                    RequireIncluded(included, elementType, collection.QualifiedId, "sequence 成员种类");
                    result[collection.QualifiedId] = BasePublicCollectionSemantics.Sequence(owner);
                    break;
                }

                default:
                    result[collection.QualifiedId] =
                        BasePublicCollectionSemantics.NotPublished(intent.SkipRuleId!, intent.SkipReason!);
                    break;
            }
        }

        return result;
    }

    private static void RequireIncluded(HashSet<string> included, string typeId, string collection, string what)
    {
        if (!included.Contains(typeId))
        {
            throw new InvalidOperationException(
                $"集合 '{collection}' 的{what} '{typeId}' 不在所选公开投影里：无法唯一解析，不得猜一个");
        }
    }

    // -- terminals / known positions ---------------------------------------------------------------------------

    private static Dictionary<string, BasePublicTerminalOutcome> BuildTerminals(RuleSet rules)
    {
        var result = new Dictionary<string, BasePublicTerminalOutcome>(StringComparer.Ordinal);
        foreach (var terminal in rules.TerminalResults.OrderBy(item => item.QualifiedId, StringComparer.Ordinal))
        {
            var local = LocalName(terminal.QualifiedId);
            if (!TerminalIntents.TryGetValue(local, out var outcome))
            {
                throw new InvalidOperationException(
                    $"终局结果 '{terminal.QualifiedId}' 没有胜负解释：不能根据终局名字猜 win/loss");
            }

            result[terminal.QualifiedId] = outcome;
        }

        return result;
    }

    private static Dictionary<string, BasePublicKnownPositionRole> BuildKnownPositions(RuleSet rules,
        HashSet<string> included, IReadOnlyDictionary<string, BasePublicCollectionSemantics> collections)
    {
        var result = new Dictionary<string, BasePublicKnownPositionRole>(StringComparer.Ordinal);
        foreach (var (local, intent) in KnownPositionIntents)
        {
            var factTypeId = Qualify(SliceManifest.PackageId, local);
            var factType = rules.TypeByQualifiedId(factTypeId);
            if (!included.Contains(factTypeId))
            {
                throw new InvalidOperationException(
                    $"已知位置事实类型 '{factTypeId}' 不在所选公开投影里：不得声明一个不发布的角色");
            }

            RequireFieldShape(rules, factType, intent.CardField, FieldTypeKind.Ref, factTypeId, "Card");
            RequireFieldShape(rules, factType, intent.PileField, FieldTypeKind.String, factTypeId, null);
            RequireFieldShape(rules, factType, intent.PositionField, FieldTypeKind.Int, factTypeId, null);

            var tokens = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (token, collectionLocal) in intent.PileByToken)
            {
                var collectionId = Qualify(SliceManifest.PackageId, collectionLocal);
                if (!collections.TryGetValue(collectionId, out var semantics))
                {
                    throw new InvalidOperationException(
                        $"已知位置角色 '{factTypeId}' 把标记 '{token}' 指向引擎集合 '{collectionId}'，"
                        + "但该集合没有公开用途声明");
                }

                if (semantics.Use != BasePublicCollectionUse.Pile)
                {
                    throw new InvalidOperationException(
                        $"已知位置角色 '{factTypeId}' 的标记 '{token}' 指向集合 '{collectionId}'，"
                        + "而它没有发布为 pile：位置只能落在公开牌堆上");
                }

                tokens[token] = collectionId;
            }

            result[factTypeId] = new BasePublicKnownPositionRole(
                factTypeId, intent.CardField, intent.PileField, intent.PositionField,
                new ReadOnlyDictionary<string, string>(tokens));
        }

        return result;
    }

    private static void RequireFieldShape(RuleSet rules, EntityTypeDefinition type, string fieldName,
        FieldTypeKind expected, string factTypeId, string? refTargetLocalName)
    {
        if (!type.TryFieldIndex(fieldName, out var index) || type.Fields[index].Kind != expected)
        {
            throw new InvalidOperationException(
                $"已知位置角色 '{factTypeId}' 的字段 '{fieldName}' 不是该类型上的 {expected} 字段："
                + "角色必须绑定真实声明，不得凭名字指定");
        }

        if (refTargetLocalName is null) return;

        var target = type.Fields[index].TargetTypeId;
        var targetId = target < 0 ? string.Empty : rules.Type(target).QualifiedId;
        if (!string.Equals(targetId, Qualify(SliceManifest.PackageId, refTargetLocalName), StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"已知位置角色 '{factTypeId}' 的字段 '{fieldName}' 指向 '{targetId}'，"
                + $"而不是真实卡牌实例类型 '{Qualify(SliceManifest.PackageId, refTargetLocalName)}'");
        }
    }

    // -- helpers ---------------------------------------------------------------------------------------------

    /// <summary>规则集里的（包限定）合法构造：只在这里拼接限定名，避免各处手写前缀。</summary>
    private static string Qualify(string packageId, string localName) => packageId + ":" + localName;

    private static string PackageOf(string qualifiedId)
    {
        var separator = qualifiedId.IndexOf(':');
        return separator < 0 ? qualifiedId : qualifiedId[..separator];
    }

    private static string LocalName(string qualifiedId)
    {
        var separator = qualifiedId.IndexOf(':');
        return separator < 0 ? qualifiedId : qualifiedId[(separator + 1)..];
    }

    /// <summary>与工厂同一种短摘要（SHA-256 前 16 个十六进制字符）：元数据摘要进入构建身份。</summary>
    internal static string Digest(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16].ToLowerInvariant();
}
