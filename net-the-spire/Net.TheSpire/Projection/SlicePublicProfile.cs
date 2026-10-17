using CoreTheSpire.Engine;

namespace NetTheSpire.Projection;

/// <summary>
/// 受控切片 <c>base-spire:combat-slice-v1</c> 的公开映射声明（N-02 的首个训练配置，H-01/H-02 收口）。
///
/// 依据：<c>docs/design/net-the-spire/design.md</c> 第 45/53 行（“公开机制 → 当前字段/位置 → 提供方来源”的映射
/// 必须显式登记；wire 的 piles 是 STS 适配结构，其种类由目录声明）与第 63 行（当前内容先用显式映射注册）。
/// 这里登记的是**引擎声明 ID → 公开声明 ID**，不是从内部字段反射生成目录。
///
/// 已收口：每个牌区都有自己的实体种类（protocol.md:83 的唯一匹配），已知位置来自包声明的事实实体
/// （<c>KnownCard</c> 引用真实卡牌实例，见 H-01），不再有 TODO-N02-PILE-ENTITY / TODO-N02-KNOWN-POSITION。
/// </summary>
public static class SlicePublicProfile
{
    public const string EnginePackage = "base-spire";
    public const string EngineVersion = "combat-slice-v1";

    /// <summary>引擎侧类型/字段/集合名，逐字取自 <c>CombatSlicePackage</c> 的声明。</summary>
    public const string PlayerType = "base-spire:Player";
    public const string MonsterType = "base-spire:Monster";
    public const string CardType = "base-spire:Card";
    public const string PowerType = "base-spire:Power";
    public const string KnownCardType = "base-spire:KnownCard";
    public const string HandPileType = "base-spire:HandPile";
    public const string DrawPileEntityType = "base-spire:DrawPileEntity";
    public const string DiscardPileEntityType = "base-spire:DiscardPileEntity";
    public const string ExhaustPileEntityType = "base-spire:ExhaustPileEntity";
    public const string InPlayPileEntityType = "base-spire:InPlayPileEntity";

    /// <summary>W-01：作者内容独有的药水实体（只在 `--author-content` 的包里存在）。</summary>
    public const string PotionType = "base-spire:Potion";

    /// <summary>W-01：药水栏集合（引擎侧 `CombatSlicePackage` 的 `PotionBelt`）。</summary>
    public const string PotionBeltCollection = "base-spire:PotionBelt";

    /// <summary>W-01：药水栏集合的公开动作（引擎侧唯一参数是 `Potion: ref<Potion>`）。</summary>
    public const string UsePotionAction = "base-spire:UsePotion";

    /// <summary>W-05：进入下一间房的公开动作（无参数；引擎侧由房间守卫决定是否合法）。</summary>
    public const string EnterNextRoomAction = "base-spire:EnterNextRoom";

    /// <summary>W-05：房间奖励（升级一张长期牌）的公开动作。</summary>
    public const string UpgradeLongTermCardAction = "base-spire:UpgradeLongTermCard";

    /// <summary>W-01：整局状态实体（只在作者包里存在；Hp/MaxHp/Gold 是公开字段）。</summary>
    public const string RunType = "base-spire:Run";

    /// <summary>W-01：整局的长期牌组集合（引擎侧 `LongTermDeck`，成员是卡牌实例）。</summary>
    public const string LongTermDeckCollection = "base-spire:LongTermDeck";

    /// <summary>W-01：整局单例集合；它的事实由 <see cref="RunType"/> 实体的公开字段表达。</summary>
    public const string RunStatesCollection = "base-spire:RunStates";

    public const string DrawPileName = "draw";

    /// <summary>协议 pile_type 的公开 ID 与 kind ID（kind_id 同时用于在本快照里唯一匹配牌堆实体）。</summary>
    public const string HandPileTypeId = "net:pile_type:base-spire:hand";
    public const string DrawPileTypeId = "net:pile_type:base-spire:draw";
    public const string DiscardPileTypeId = "net:pile_type:base-spire:discard";
    public const string ExhaustPileTypeId = "net:pile_type:base-spire:exhaust";
    public const string InPlayPileTypeId = "net:pile_type:base-spire:in_play";

    /// <summary>角色 → 公开动作/能力声明时用到的固定 ID。</summary>
    public const string CardKindId = "net:kind:base-spire:Card";
    public const string MonsterKindId = "net:kind:base-spire:Monster";

    /// <summary>W-01：药水实体与药水栏 group_type 的公开 ID（只在作者变体里出现）。</summary>
    public const string PotionKindId = "net:kind:base-spire:Potion";
    public const string PotionBeltGroupTypeId = "net:group_type:base-spire:potion_belt";

    /// <summary>W-01：整局 kind 与长期牌组 group_type 的公开 ID（只在作者变体里出现）。</summary>
    public const string RunKindId = "net:kind:base-spire:Run";
    public const string LongTermDeckGroupTypeId = "net:group_type:base-spire:long_term_deck";

    public static PublicSnapshotProfile Create() => new(
        rulesetId: "net:ruleset:base-spire-combat-slice-v1",
        catalogId: "net:catalog:base-spire-combat-slice-v1",
        backendProfileId: "net:backend:generic-engine-runtime-v1",
        kinds: Kinds(),
        collections: Collections(),
        relations: Relations(),
        actions: Actions(),
        answerAction: AnswerAction(),
        terminals: Terminals(),
        knownPositions: KnownPositions());

    /// <summary>
    /// W-01：**作者内容包的 Profile 变体**。
    ///
    /// 目前与 <see cref="Create"/> 的规则完全相同，**只有身份不同**——这是"包/Profile 显式选择"的第一步：
    /// 宿主用 `--author-content` 选择本变体时，`describe` 报出的 ruleset/catalog 身份随即变化（可核对），
    /// 而默认 Profile 与既有检查完全不受影响。
    ///
    /// 后续（见阶段交接 §批次21 配方）在同形的 `AuthorKinds()`/`AuthorActions()`/`AuthorCollections()`
    /// 里逐项追加作者独有声明（`base-spire:UsePotion`、`base-spire:Run`、药水实体与槽位字段、
    /// `PotionBelt`/`LongTermDeck` 的发布），每加一项用 `net-the-spire/client/test_author_content.py` 迭代，
    /// 直到作者宿主能 `describe → start → observe`。
    /// </summary>
    /// <summary>
    /// W-01：**作者内容包的 Profile 变体**。
    ///
    /// 身份与 <see cref="Create"/> 不同（`…author-content-v1`），并在其上追加**只由作者文本声明**的公开事实：
    /// <list type="bullet">
    /// <item>药水实体 kind（内容身份 + Rarity/Target 的 category 词表 + 数量字段）；</item>
    /// <item>药水栏：发布为 **group**（owner = 玩家、成员 = 药水），容量取**引擎声明的集合容量**
    /// （<c>CollectionDefinition.Capacity</c>），因此不借用仅适用卡牌的 pile 语义；</item>
    /// <item><c>UsePotion</c> 动作（引擎侧唯一参数 <c>Potion: ref&lt;Potion&gt;</c>，候选＝药水栏成员）。</item>
    /// </list>
    ///
    /// 必须传入 <paramref name="rules"/>：容量这类公开事实要从**引擎声明**读，不能在 Profile 里另抄一个数字。
    /// 默认 Profile（<see cref="Create"/>）一字未动。
    /// </summary>
    public static PublicSnapshotProfile CreateAuthorContent(RuleSet rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return new PublicSnapshotProfile(
            // W-01（本轮）：作者变体的**声明集**变了（新增玩家回合摘要与卡牌实例变量的发布），
            // 药水槽位/使用支持/丢弃进一步增加声明，因此当前身份升到 v3：`describe` 报出的 ruleset/catalog id 能区分"发布前 / 发布后"两个目录。
            // 目录内容的可核对摘要另由消费者侧记录（J-01 记录 `source_catalog_digest`）。
            rulesetId: AuthorRulesetV3,
            catalogId: AuthorCatalogV3,
            backendProfileId: "net:backend:generic-engine-runtime-v1",
            kinds: AuthorKinds(),
            collections: AuthorCollections(rules),
            relations: Relations(),
            actions: AuthorActions(),
            answerAction: AnswerAction(),
            terminals: Terminals(),
            knownPositions: KnownPositions());
    }

    /// <summary>W-01：作者变体 v2 的身份（发布玩家回合摘要与卡牌实例变量后的目录）。</summary>
    public const string AuthorRulesetV2 = "net:ruleset:base-spire-author-content-v2";
    public const string AuthorCatalogV2 = "net:catalog:base-spire-author-content-v2";
    // v3 adds stable potion slots, usage/support metadata and explicit discard. Formal profiles
    // continue deriving their identity from declarations; this is only the legacy author adapter.
    public const string AuthorRulesetV3 = "net:ruleset:base-spire-author-content-v3";
    public const string AuthorCatalogV3 = "net:catalog:base-spire-author-content-v3";

    /// <summary>W-01：作者变体发布的两个"回合内摘要"布尔（引擎侧见 <c>CombatSlicePackage</c> 的 Player 字段）。</summary>
    public const string LostHpThisTurnField = "net:field:base-spire:Player.LostHpThisTurn";
    public const string ExhaustedCardThisTurnField = "net:field:base-spire:Player.ExhaustedCardThisTurn";

    /// <summary>
    /// ENV-02：本回合"由牌获得格挡"的次数（UnmovablePower 的豁免计数）。
    /// 与上面两个回合摘要同类：**不是**现有公开字段的函数，且不发布就等于要求 AI 记住历史来预测
    /// "下一次获得格挡会不会翻倍"，因此由 Profile 决定是否发布（默认 Profile 记具名跳过）。
    /// </summary>
    public const string BlockGainsThisTurnField = "net:field:base-spire:Player.BlockGainsThisTurn";

    /// <summary>W-01：作者变体发布的卡牌实例变量字段（Rampage 的成长量）。</summary>
    public const string ExtraDamageVariableField = "net:field:base-spire:Card.Variable_extra-damage";

    /// <summary>`Create()` 的 kind 表 + 作者独有的药水 kind（默认 Profile 不受影响）。</summary>
    private static IReadOnlyDictionary<string, PublicKindRule> AuthorKinds()
    {
        // 药水内容 ID 逐字取自作者文本 `Slice/Content/slice-content.author` 的 `potion` 声明（第 135/145 行）。
        // 作者文本新增药水而这里没登记 ⇒ 投影按 N-02-UNDECLARED-CONTENT 显式拒绝（不静默放行）。
        var potionIds = new[]
        {
            "base-spire:beetle-juice",           // slice-content.author:135
            "base-spire:blessing-of-the-forge",  // slice-content.author:145
        };
        // 作者文本声明的**全部**能力（`power` 声明 + 卡牌/药水真正施加的那些）。默认 Profile 只登记切片会施加的四个；
        // 作者内容里的 shrink / feel-no-pain / rage-power / no-energy-gain 只有这里登记了，投影才能发布它们
        // （否则作者 Gym 一用药水/一打出这些牌就会按 N-02-UNDECLARED-CONTENT 拒绝）。
        var powerIds = new[]
        {
            "base-spire:strength", "base-spire:vulnerable", "base-spire:weak", "base-spire:ritual",
            "base-spire:shrink",          // slice-content.author:62（beetle-juice 施加）
            "base-spire:feel-no-pain",    // slice-content.author:307
            "base-spire:rage-power",      // slice-content.author:330
            "base-spire:no-energy-gain",  // slice-content.author:379
            // W-06：正式能力源新迁移进来的能力（`preventsBlockClear`）。
            "base-spire:barricade",       // ironclad-powers.spire 的 BarricadePower
            "base-spire:tank-power",      // ironclad-powers.spire 的 TankPower
            "base-spire:flame-barrier",   // ironclad-powers.spire 的 FlameBarrierPower（events 反击）
            "base-spire:demon-form",      // shared-powers.spire 的 DemonFormPower（回合开始触发）
            "base-spire:corruption-power", // ironclad-powers.spire 的 CorruptionPower（cardRule）
            "base-spire:free-attack-power", // ironclad-powers.spire 的 FreeAttackPower（cardRule）
            "base-spire:juggernaut-power", // ironclad-powers.spire 的 JuggernautPower（格挡事件 + 随机攻击）
            "base-spire:self-forming-clay", // shared-powers.spire 的 SelfFormingClayPower（sequence + removePower）
        };

        return new Dictionary<string, PublicKindRule>(Kinds(), StringComparer.Ordinal)
        {
            [PowerType] = PowerRule(powerIds),
            // W-01（本轮）：作者 Gym 必须发布**影响决策的当前事实**，它们不能从同一份快照的其它字段推出：
            //   * `LostHpThisTurn` / `ExhaustedCardThisTurn`：Spray/Spite/EvilEye 按它们分支，
            //     而"本回合"这一时间范围不在快照里（只有当前 Hp 与消耗区现值）；
            //   * `Variable_extra-damage`：Rampage 的成长量是实例自己的累积事实。
            // 默认 Profile（`Kinds()` 里的 `PlayerRule(false)` / `CardRule(..., false)`）一字未动。
            [PlayerType] = PlayerRule(publishTurnSummaries: true),
            // W-06：作者扩展路径新增的卡（正式 0.0.1 → Engine 迁移进来的 Barricade）。
            // 只加在**作者变体**里：受控 M1 的默认 Profile 不受影响（两条路径的 host 能力分开统计）。
            [CardType] = CardRule(
            [
                "base-spire:barricade",   // ironclad-cards.spire 的 Barricade（`preventsBlockClear` 机制）
                "base-spire:strike-ironclad", "base-spire:defend-ironclad", "base-spire:bash",
                "base-spire:headbutt",
                "base-spire:dazed", "base-spire:beckon",
                "base-spire:iron-wave", "base-spire:twin-strike", "base-spire:breakthrough",
                "base-spire:pommel-strike", "base-spire:shrug-it-off",
                "base-spire:thunderclap", "base-spire:rampage",
                "base-spire:entrench", "base-spire:inflame",
                "base-spire:feel-no-pain", "base-spire:rage",
                "base-spire:fight-me", "base-spire:dominate",
                "base-spire:expect-a-fight", "base-spire:spite",
                "base-spire:ashen-strike",
            ], publishInstanceVariables: true),
            // 整局状态：没有内容标识（单例），只有三个公开数值字段（HP/最大 HP/金币）。
            // 这些是玩家可见的整局事实，也是 W-05 的"长期状态延续"在公开侧的落点。
            [RunType] = new PublicKindRule(
                Kind(RunType),
                ContentFieldName: null,
                ContentIds: [],
                Fields: new Dictionary<string, Disposition<PublishedField>>(StringComparer.Ordinal)
                {
                    ["Hp"] = Number(RunType, "Hp"),
                    ["MaxHp"] = Number(RunType, "MaxHp"),
                    ["Gold"] = Number(RunType, "Gold"),
                    // W-05：整局由**连续战斗房间**组成。房间号与"哪一间已结算"是公开事实
                    // （它决定 `EnterNextRoom` 是否合法，也决定"战斗结束 ≠ 整局结束"）。
                    ["CurrentRoom"] = Number(RunType, "CurrentRoom"),
                    ["SettledRoom"] = Number(RunType, "SettledRoom"),
                    // 每间房至多一次的**奖励**标记（与结算分开：可以清房但不拿奖励）。
                    ["RewardTaken"] = Number(RunType, "RewardTaken"),
                }),
            [PotionType] = new PublicKindRule(
                Kind(PotionType),
                ContentFieldName: "DefinitionId",
                ContentIds: potionIds,
                Fields: new Dictionary<string, Disposition<PublishedField>>(StringComparer.Ordinal)
                {
                    ["DefinitionId"] = ContentIsEntityFact(PotionType, "DefinitionId"),
                    // 稀有度与目标直接影响决策（能否/对谁使用），且是玩家可见的公开事实 ⇒ 按已声明词表发布。
                    // 词表取自作者前端允许的取值（`PotionRarities.All` / `PotionTargets.All`）。
                    ["Rarity"] = Category(PotionType, "Rarity", new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["common"] = CategoryId("common"),
                        ["uncommon"] = CategoryId("uncommon"),
                        ["rare"] = CategoryId("rare"),
                    }),
                    ["Target"] = Category(PotionType, "Target", new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["enemy"] = CategoryId("enemy"),
                        ["self"] = CategoryId("self"),
                    }),
                    // 数量/层数是效果数值：发布（消费者要能解释合法动作的数值）。
                    ["Amount"] = Number(PotionType, "Amount"),
                    ["PowerStacks"] = Number(PotionType, "PowerStacks"),
                    ["SlotIndex"] = Number(PotionType, "SlotIndex"),
                    ["UseSupported"] = Boolean(PotionType, "UseSupported"),
                    ["Usage"] = Category(PotionType, "Usage", new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["combat-only"] = CategoryId("combat-only"),
                    }),
                    ["UnsupportedUseReason"] = Disposition<PublishedField>.Skip("BASE-LEGACY-POTION-SUPPORT",
                        "旧作者场景只注册可执行use，没有未支持药水；正式环境按声明派生并发布支持原因。"),
                    // 与卡牌同源的缺口：没有附加能力时该字段是空串，而协议 category 值必须来自目录声明的词表
                    // （protocol.md:74）。空串不放行、也不自造哨兵类别顶替 ⇒ 具名跳过并登记理由。
                    ["PowerId"] = Disposition<PublishedField>.Skip("N-02-EMPTY-CATEGORY",
                        "未附加能力时是空串；协议 category 值必须来自目录声明的类别 ID，空串不放行、" +
                        "也不自造哨兵类别顶替 → 需要 base-spire 提供可声明的“无内容”表达"),
                }),
        };
    }

    /// <summary>`Collections()` + 把作者独有的药水栏与长期牌组从"具名跳过"改为**发布为 group**（默认 Profile 保持跳过）。</summary>
    private static IReadOnlyDictionary<string, CollectionDisposition> AuthorCollections(RuleSet rules)
    {
        var belt = RequireCollection(rules, PotionBeltCollection);
        var deck = RequireCollection(rules, LongTermDeckCollection);
        if (rules.Collections.Any(collection =>
                string.Equals(collection.QualifiedId, RunStatesCollection, StringComparison.Ordinal)) is false)
        {
            throw new PublicMappingException("N-02-GROUP-OWNER",
                $"作者 Profile 需要引擎声明 '{RunStatesCollection}'（整局单例），当前规则集里没有");
        }

        return new Dictionary<string, CollectionDisposition>(Collections(), StringComparer.Ordinal)
        {
            [PotionBeltCollection] = CollectionDisposition.AsGroup(new PublishedGroup(
                GroupTypeId: PotionBeltGroupTypeId,
                OwnerKindId: Kind(PlayerType),
                MemberKindIds: [Kind(PotionType)],
                MinCount: 0,
                // 槽位/容量是引擎声明的规则（越界时引擎按 CapacityExceeded 拒绝），不在这里另抄数字。
                MaxCount: belt.Capacity == int.MaxValue ? null : belt.Capacity)),
            ["base-spire:RewardPotions"] = CollectionDisposition.Skip("BASE-LEGACY-NO-REWARD-POTIONS",
                "旧作者切片不接地图战斗奖励，候选集合恒为空；正式环境通过base语义发布奖励Sequence。"),
            // 长期牌组：owner 是整局单例，成员是**长期**那批卡牌实例（与战斗里的实例是两批）。
            // 它是"实体/字段/关系"路线里的 group，不借用仅适用卡牌的 pile 语义。
            [LongTermDeckCollection] = CollectionDisposition.AsGroup(new PublishedGroup(
                GroupTypeId: LongTermDeckGroupTypeId,
                OwnerKindId: Kind(RunType),
                MemberKindIds: [Kind(CardType)],
                MinCount: 0,
                MaxCount: deck.Capacity == int.MaxValue ? null : deck.Capacity)),
            // 玩家可见的整局事实（HP/金币）由 Run **实体的公开字段**表达，因此这个单例容器不重复发布
            // （protocol.md:94 禁止用 group 重复表达同一批事实）；只有整局的**成员**事实才走 group。
            [RunStatesCollection] = CollectionDisposition.Skip("N-02-KNOWN-FACT",
                "整局单例容器：其 HP/最大 HP/金币已由 Run 实体的公开字段表达，" +
                "再发成 group 会把同一事实重复表达（protocol.md:94）"),
        };
    }

    private static CollectionDefinition RequireCollection(RuleSet rules, string qualifiedId)
    {
        var collection = rules.Collections.SingleOrDefault(item =>
            string.Equals(item.QualifiedId, qualifiedId, StringComparison.Ordinal));
        return collection ?? throw new PublicMappingException("N-02-GROUP-OWNER",
            $"作者 Profile 需要引擎声明 '{qualifiedId}'，当前规则集里没有；" +
            "不得凭空声明一个没有引擎来源的集合");
    }

    /// <summary>`Actions()` + 作者独有的 `UsePotion`（引擎侧唯一参数槽是 `Potion`）与 `EnterNextRoom`。</summary>
    private static IReadOnlyDictionary<string, PublicActionRule> AuthorActions() =>
        new Dictionary<string, PublicActionRule>(Actions(), StringComparer.Ordinal)
        {
            ["base-spire:DiscardPotion"] = new PublicActionRule("net:action:base-spire:DiscardPotion",
                new Dictionary<int, PublicActionField>
                {
                    [0] = new PublicActionField("net:field:base-spire:DiscardPotion.Potion",
                        PublicValueKind.EntityRef, Required: true, TargetKindIds: [Kind(PotionType)]),
                }),
            [UsePotionAction] = new PublicActionRule(
                "net:action:base-spire:UsePotion",
                new Dictionary<int, PublicActionField>
                {
                    // protocol.md:98：执行所需字段必须 known ⇒ required；目标种类收窄到药水 kind（不是全集过近似）。
                    [0] = new PublicActionField("net:field:base-spire:UsePotion.Potion",
                        PublicValueKind.EntityRef, Required: true, TargetKindIds: [Kind(PotionType)]),
                }),
            // W-05：进入下一间房（无参数）。引擎侧它只在"本间房已结算且还有下一间"时合法，
            // 因此消费者只会在这两个条件都满足时看到这个候选。
            [EnterNextRoomAction] = new PublicActionRule(
                "net:action:base-spire:EnterNextRoom",
                new Dictionary<int, PublicActionField>()),
            // W-05：房间奖励——把一张**长期**牌升级（每间房至多一次；候选＝长期牌组）。
            [UpgradeLongTermCardAction] = new PublicActionRule(
                "net:action:base-spire:UpgradeLongTermCard",
                new Dictionary<int, PublicActionField>
                {
                    [0] = new PublicActionField("net:field:base-spire:UpgradeLongTermCard.Card",
                        PublicValueKind.EntityRef, Required: true, TargetKindIds: [Kind(CardType)]),
                }),
        };

    private static IReadOnlyDictionary<string, PublicKindRule> Kinds()
    {
        // B-06a：作者路径已经迁入的内容也要在目录里声明（"逐机制更新 net profile"）。
        // 注意这**只是目录声明**，不代表这些内容都已被规则覆盖（B-05a 的支持范围另见 manifest）。
        var cardIds = new[]
        {
            "base-spire:strike-ironclad", "base-spire:defend-ironclad", "base-spire:bash", "base-spire:headbutt",
            "base-spire:dazed", "base-spire:beckon",                       // status-cards.spire:3 / :25
            "base-spire:iron-wave", "base-spire:twin-strike", "base-spire:breakthrough",  // ironclad-cards.spire
            "base-spire:pommel-strike", "base-spire:shrug-it-off",          // ironclad-cards.spire
            "base-spire:thunderclap", "base-spire:rampage",                 // ironclad-cards.spire:2535 / :3852
            "base-spire:entrench", "base-spire:inflame",                 // ironclad-cards.spire:1752 / :2191
            "base-spire:feel-no-pain", "base-spire:rage",              // ironclad-cards.spire:1976 / :3565
            "base-spire:fight-me", "base-spire:dominate",             // ironclad-cards.spire:3321 / :3425
            "base-spire:expect-a-fight", "base-spire:spite",           // ironclad-cards.spire:1192 / :3604
            "base-spire:ashen-strike",                                // ironclad-cards.spire:3057
        };        var monsterIds = new[] { "base-spire:calcified-cultist", "base-spire:damp-cultist" };
        var powerIds = new[]
        {
            "base-spire:strength", "base-spire:vulnerable", "base-spire:weak",
            // Ritual 由敌人在开局第一动挂上（SliceManifest.RitualId）；公开词表必须登记它，
            // 否则回合推进后的投影会以 N-02-UNDECLARED-CONTENT 拒绝（N-03 的中途态检查暴露了该缺口）。
            "base-spire:ritual",
        };

        return new Dictionary<string, PublicKindRule>(StringComparer.Ordinal)
        {
            [PlayerType] = PlayerRule(publishTurnSummaries: false),

            [MonsterType] = new PublicKindRule(
                Kind(MonsterType),
                ContentFieldName: "DefinitionId",
                ContentIds: monsterIds,
                Fields: new Dictionary<string, Disposition<PublishedField>>(StringComparer.Ordinal)
                {
                    ["DefinitionId"] = ContentIsEntityFact(MonsterType, "DefinitionId"),
                    ["Hp"] = Number(MonsterType, "Hp"),
                    ["MaxHp"] = Number(MonsterType, "MaxHp"),
                    ["Block"] = Number(MonsterType, "Block"),
                    ["MoveIndex"] = Number(MonsterType, "MoveIndex"),
                    ["FirstMoveRitual"] = Number(MonsterType, "FirstMoveRitual"),
                    ["StrikeDamage"] = Number(MonsterType, "StrikeDamage"),
                    ["NextMove"] = Category(MonsterType, "NextMove",
                        new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["Incantation"] = CategoryId("Incantation"),
                            ["DarkStrike"] = CategoryId("DarkStrike"),
                        }),
                }),

            [CardType] = CardRule(cardIds),

            [PowerType] = PowerRule(powerIds),

            [KnownCardType] = new PublicKindRule(
                Kind(KnownCardType),
                ContentFieldName: null,
                ContentIds: [],
                Fields: new Dictionary<string, Disposition<PublishedField>>(StringComparer.Ordinal)
                {
                    // 事实内容不进普通字段：卡牌实例引用与绝对位置由 known_positions 表达（protocol.md:83-90），
                    // 观看者绑定不是公开游戏事实（协议没有 participant 值类型）。
                    ["Viewer"] = Disposition<PublishedField>.Skip("H-02-VIEWER-BINDING",
                        "授权/所有权用的 participant 绑定；协议值类型只有 number/boolean/category/entity_ref" +
                        "（protocol.md:70-75），不发布也不自造值类型"),
                    ["Card"] = Disposition<PublishedField>.Skip("H-02-KNOWN-POSITION",
                        "真实卡牌实例引用由 known_positions.card_ref 表达（protocol.md:85/89），" +
                        "在实体字段里重复发布同一事实会被 protocol.md:94 禁止"),
                    ["Pile"] = Disposition<PublishedField>.Skip("H-02-KNOWN-POSITION",
                        "牌堆由 known_positions 所属的 pile 块表达（protocol.md:83），不在实体字段重复"),
                    ["Position"] = Disposition<PublishedField>.Skip("H-02-KNOWN-POSITION",
                        "绝对位置由 known_positions.position 表达（protocol.md:85），不在实体字段重复"),
                }),

            // 牌区实体：它们本身就是 pile_ref，种类即牌堆种类（protocol.md:83）。
            [HandPileType] = AreaEntity(HandPileType),
            [DrawPileEntityType] = AreaEntity(DrawPileEntityType),
            [DiscardPileEntityType] = AreaEntity(DiscardPileEntityType),
            [ExhaustPileEntityType] = AreaEntity(ExhaustPileEntityType),
            [InPlayPileEntityType] = AreaEntity(InPlayPileEntityType),
        };
    }

    /// <summary>牌区实体没有任何玩法字段：牌堆种类由 kind_id 表达（protocol.md:83），成员由 piles 块表达。</summary>
    private static PublicKindRule AreaEntity(string engineTypeQualifiedId) =>
        new(Kind(engineTypeQualifiedId), ContentFieldName: null, ContentIds: [],
            Fields: new Dictionary<string, Disposition<PublishedField>>(StringComparer.Ordinal));

    /// <summary>
    /// Player kind 的单一来源（字段处置只有这里一份）。
    ///
    /// <paramref name="publishTurnSummaries"/> 决定"本回合摘要"是否发布：
    /// <list type="bullet">
    /// <item><c>false</c>（默认受控切片）：<c>LostHpThisTurn</c>/<c>ExhaustedCardThisTurn</c> 记为**具名跳过**。
    /// 不变的理由见下（默认 Profile 一字未动，冻结切片的目录与规范字节不受影响）。</item>
    /// <item><c>true</c>（作者 Gym）：**发布**为 boolean。</item>
    /// </list>
    /// 为什么作者 Gym 必须发布（W-01 的本轮交付；此前"可由 Hp/牌区变化推导"的判断是错的）：
    /// 消费者只拿到**当前**这一份公开快照，而"本回合是否失去过生命/消耗过牌"是**回合内发生过**的事实——
    /// 当前 Hp 与消耗区的现值都无法区分"本回合发生过"与"之前回合发生过"。作者的 Spite/EvilEye 正是按这两个
    /// 布尔分支的牌（<c>ironclad-cards.spire</c> 的 <c>if { condition = { playerLostHpThisTurn } }</c> /
    /// <c>cardExhaustedThisTurn</c>），不发布就等于要求 AI 记住历史来补洞（§19.2 明确禁止）。
    /// </summary>
    private static PublicKindRule PlayerRule(bool publishTurnSummaries) =>
        new(Kind(PlayerType),
            ContentFieldName: null,
            ContentIds: [],
            Fields: new Dictionary<string, Disposition<PublishedField>>(StringComparer.Ordinal)
            {
                ["Owner"] = Disposition<PublishedField>.Skip("N-02-NOT-PUBLIC",
                    "引擎的 participant 绑定；协议值类型只有 number/boolean/category/entity_ref（protocol.md:70-75），" +
                    "且它是观看者绑定而不是公开游戏事实，不发布而不是自造值类型"),
                ["Hp"] = Number(PlayerType, "Hp"),
                ["MaxHp"] = Number(PlayerType, "MaxHp"),
                ["Block"] = Number(PlayerType, "Block"),
                ["Energy"] = Number(PlayerType, "Energy"),
                ["Stars"] = Disposition<PublishedField>.Skip("BASE-LEGACY-RESOURCE-SCOPE",
                    "旧受控切片不注册星星消费者；正式环境通过base声明派生Profile发布该字段"),
                ["OrbSlots"] = Disposition<PublishedField>.Skip("BASE-LEGACY-RESOURCE-SCOPE",
                    "旧受控切片不注册充能球消费者；正式环境通过base声明派生Profile发布该字段"),
                ["MaxEnergy"] = Number(PlayerType, "MaxEnergy"),
                ["Turn"] = Number(PlayerType, "Turn"),
                // W-05：战斗作用域的"这场战斗是否已结算"。**不发布**：它是我们为幂等性维护的内部簿记，
                // 不是玩家可见的游戏事实（终局本身已由 terminal/终局结果公开表达）。
                ["BattleSettled"] = Disposition<PublishedField>.Skip("N-02-INTERNAL-BOOKKEEPING",
                    "结算幂等键是引擎内部簿记；终局事实已由终局结果公开表达，重复发布违反 protocol.md:94"),
                // 见本方法文档：这两个布尔是**回合内摘要**，不是"现有字段的函数"，因此按 Profile 决定是否发布。
                ["LostHpThisTurn"] = publishTurnSummaries
                    ? Boolean(PlayerType, "LostHpThisTurn")
                    : Disposition<PublishedField>.Skip("N-02-DERIVABLE",
                        "默认受控切片的公开动作/终局判定都不读它；作者 Gym 需要它（见 PlayerRule 文档），"
                        + "由 Profile 变体发布而不是改共享目录"),
                ["ExhaustedCardThisTurn"] = publishTurnSummaries
                    ? Boolean(PlayerType, "ExhaustedCardThisTurn")
                    : Disposition<PublishedField>.Skip("N-02-DERIVABLE",
                        "默认受控切片的公开动作/终局判定都不读它；作者 Gym 需要它（见 PlayerRule 文档），"
                        + "由 Profile 变体发布而不是改共享目录"),
                // ENV-02：UnmovablePower 的豁免计数（"本回合由牌获得格挡的次数"）。与上面两个回合摘要同类：
                // 它决定"下一次获得格挡会不会翻倍"，不发布就是要求 AI 记住历史来预测结果。
                ["BlockGainsThisTurn"] = publishTurnSummaries
                    ? Number(PlayerType, "BlockGainsThisTurn")
                    : Disposition<PublishedField>.Skip("N-02-DERIVABLE",
                        "默认受控切片目前没有引用 UnmovablePower 的内容；作者 Gym 需要它"
                        + "（Unmovable 的豁免次数影响下一次格挡是否翻倍），由 Profile 变体发布"),
                ["HpLossCount"] = Disposition<PublishedField>.Skip("N-02-FORMAL-ONLY",
                    "旧切片/作者夹具没有引用战斗掉血计数的牌；正式环境由 DerivedPublicProfile 发布真实字段，不依赖本适配表"),
            });

    /// <summary>
    /// Power kind 的单一来源：字段处置只有这里一份，内容 ID 由调用方给出
    /// （默认 Profile 只登记切片会施加的四个；作者变体登记作者文本声明的全部能力）。
    /// </summary>
    private static PublicKindRule PowerRule(IReadOnlyList<string> contentIds) =>
        new(Kind(PowerType),
            ContentFieldName: "DefinitionId",
            ContentIds: contentIds,
            Fields: new Dictionary<string, Disposition<PublishedField>>(StringComparer.Ordinal)
            {
                ["DefinitionId"] = ContentIsEntityFact(PowerType, "DefinitionId"),
                ["Stacks"] = Number(PowerType, "Stacks"),
                ["Ticks"] = Boolean(PowerType, "Ticks"),
                ["SkipNextTick"] = Boolean(PowerType, "SkipNextTick"),
                ["ApplicationCount"] = Disposition<PublishedField>.Skip("BASE-LEGACY-RESOURCE-SCOPE",
                    "旧受控切片没有施毒计数消费者；正式环境从base声明派生并公开该计数"),
            });

    /// <summary>
    /// Card kind 的单一来源：字段处置只有这里一份，内容 ID 由调用方给出
    /// （默认 Profile 用迁移中的冻结清单；作者变体在其上追加作者独有内容）。
    ///
    /// <paramref name="publishInstanceVariables"/>：是否发布卡牌**实例变量**（`Variable_&lt;name&gt;`）。
    /// 默认受控切片**不发布**（具名跳过：它的卡没有变量字段，发布它只会同步改动共享目录与规范字节，
    /// 实测会打掉 net 40 项 + J-01 4 项）；作者 Gym **发布**，因为作者的成长量正是按实例累积的决策事实
    /// （例：Rampage 的 `extra-damage`，见 `Slice/Content/slice-content.author` 与
    /// <c>ironclad-cards.spire:3855-3900</c> 的 `variables`/`modifyCardVariable`）。
    /// 只发布 `Amount` 不足以让消费者解释"这张牌下次打出会打多少"：成长量是**独立于当前 Amount** 的实例事实。
    /// </summary>
    private static PublicKindRule CardRule(IReadOnlyList<string> contentIds,
        bool publishInstanceVariables = false) =>
        new(Kind(CardType),
            ContentFieldName: "DefinitionId",
            ContentIds: contentIds,
            Fields: new Dictionary<string, Disposition<PublishedField>>(StringComparer.Ordinal)
            {
                ["DefinitionId"] = ContentIsEntityFact(CardType, "DefinitionId"),
                ["Kind"] = Category(CardType, "Kind", new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["Attack"] = CategoryId("Attack"),
                    ["Skill"] = CategoryId("Skill"),
                    ["Power"] = CategoryId("Power"),
                    ["Status"] = CategoryId("Status"),
                }),
                ["Upgraded"] = Boolean(CardType, "Upgraded"),
                // B-06a 状态牌关键词：这是**可见牌的公开属性**（能否打出、端末是否消耗）。
                // 不省略的理由：消费者要能解释"为什么这张牌不在可提交组合里"（除了从合法动作推断），
                // 而 wire 已有 boolean 值类型（protocol.md:70-75），无需自造类型。
                ["Unplayable"] = Boolean(CardType, "Unplayable"),
                ["Ethereal"] = Boolean(CardType, "Ethereal"),
                ["Cost"] = Number(CardType, "Cost"),
                // B-06a：临时费用修正（来源 modifySelectedCardCost）。**发布**：有效费用是
                // max(0, Cost + CostModifier)，只发布 Cost 会让消费方看到的费用与合法动作不一致。
                ["CostModifier"] = Number(CardType, "CostModifier"),
                ["EnchantmentId"] = Disposition<PublishedField>.Skip("N-02-LEGACY-ATTACHMENT-SCOPE",
                    "旧0.1受控切片不提供附魔消费者；正式0.2由规则声明派生并发布附魔字段"),
                ["EnchantmentAmount"] = Disposition<PublishedField>.Skip("N-02-LEGACY-ATTACHMENT-SCOPE",
                    "旧0.1受控切片不提供附魔消费者；正式0.2由规则声明派生发布"),
                ["EnchantmentDisabled"] = Disposition<PublishedField>.Skip("N-02-LEGACY-ATTACHMENT-SCOPE",
                    "旧0.1受控切片不提供附魔消费者；正式0.2由规则声明派生发布"),
                // W-01：作者内容的卡牌实例变量（`Variable_<name>`，例：Rampage 的成长量 extra-damage）。
                ["Variable_extra-damage"] = publishInstanceVariables
                    ? Number(CardType, "Variable_extra-damage")
                    : Disposition<PublishedField>.Skip("N-02-AUTHOR-ONLY",
                        "作者内容独有的实例变量；默认受控切片的卡没有该字段。作者 Gym 由 Profile 变体发布"),
                ["Amount"] = Number(CardType, "Amount"),
                ["PowerStacks"] = Number(CardType, "PowerStacks"),
                ["PowerId"] = Disposition<PublishedField>.Skip("N-02-EMPTY-CATEGORY",
                    "该字符串在没有附加 Power 时是空串；协议 category 值必须是目录声明的类别 ID（protocol.md:74），" +
                    "空串不放行、也不自造哨兵类别顶替 → 需要 base-spire 提供可声明的“无内容”表达"),
            });

    private static IReadOnlyDictionary<string, PublicKnownPositionRule> KnownPositions() =>
        new Dictionary<string, PublicKnownPositionRule>(StringComparer.Ordinal)
        {
            [KnownCardType] = new PublicKnownPositionRule(
                FactKindId: KnownCardType,
                CardFieldName: "Card",
                PileFieldName: "Pile",
                PositionFieldName: "Position",
                PileTypeByToken: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [DrawPileName] = DrawPileTypeId,
                }),
        };

    private static IReadOnlyDictionary<string, CollectionDisposition> Collections()
    {
        // 每个牌区一个 pile_type；kind_id 唯一匹配一个牌区实体（protocol.md:83）。
        // position_schema 只对抽牌/弃牌堆声明：它们的绝对位置从顶部 0 起算（protocol.md:85），
        // 手牌/消耗/出牌区没有公开位置语义，故为 null 而不是自造范围。
        var memberKinds = new[] { CardKindId };
        var positional = new PublicPositionSchema("card", "top", 0, null);

        var hand = CollectionDisposition.AsPile(new PublishedPile(HandPileTypeId, Kind(HandPileType),
            memberKinds, PositionSchema: null));
        var draw = CollectionDisposition.AsPile(new PublishedPile(DrawPileTypeId, Kind(DrawPileEntityType),
            memberKinds, positional));
        var discard = CollectionDisposition.AsPile(new PublishedPile(DiscardPileTypeId, Kind(DiscardPileEntityType),
            memberKinds, positional));
        var exhaust = CollectionDisposition.AsPile(new PublishedPile(ExhaustPileTypeId, Kind(ExhaustPileEntityType),
            memberKinds, PositionSchema: null));
        var inPlay = CollectionDisposition.AsPile(new PublishedPile(InPlayPileTypeId, Kind(InPlayPileEntityType),
            memberKinds, PositionSchema: null));

        return new Dictionary<string, CollectionDisposition>(StringComparer.Ordinal)
        {
            ["base-spire:Hand"] = hand,
            ["base-spire:DrawPile"] = draw,
            ["base-spire:DiscardPile"] = discard,
            ["base-spire:ExhaustPile"] = exhaust,
            ["base-spire:InPlay"] = inPlay,
            ["base-spire:PlayedThisTurn"] = CollectionDisposition.Skip("N-02-NO-SEQUENCE-ROLE",
                "该牌区有内部顺序，但切片没有任何公开有序流程语义（protocol.md:96 要求 elements 顺序必须有公开游戏语义）；" +
                "把 OrderVisible=true 的内部顺序当公开 ordinal 发布属于泄漏，故不发布并登记为缺口。"),
            ["base-spire:CardPlayHistory"] = CollectionDisposition.Skip("BASE-INTERNAL-PLAY-HISTORY",
                "内部执行事件种类未加入公开投影；出牌记录的公开兼容集合是 PlayedThisTurn，正式环境按 base 元数据派生"),
            ["base-spire:OrbQueue"] = CollectionDisposition.Skip("BASE-LEGACY-RESOURCE-SCOPE",
                "旧受控切片不注册充能球消费者；正式环境通过base声明派生Profile发布队列及成员"),
            ["base-spire:PowerCardSelection"] = CollectionDisposition.Skip("BASE-INTERNAL-POWER-SELECTION",
                "能力处理中的内部卡牌快照；成员种类未加入投影，不向玩家公开，也不改变旧切片目录"),
            ["base-spire:SlyDiscards"] = CollectionDisposition.Skip("BASE-INTERNAL-SLY-DISCARD",
                "手动弃牌批次的内部奇巧条目；成员种类未加入投影，实际卡牌位置由正式牌区发布"),
            ["base-spire:Monsters"] = CollectionDisposition.Skip("TODO-N02-GROUP-OWNER",
                "生物集合不是牌堆（protocol.md:83 的 pile 成员是卡牌实例）；发布为 group 需要一个 owner 实体，" +
                "而战斗作用域不是实体，且引擎集合成员唯一、无法表达 group 的 multiplicity（protocol.md:96）→ 登记为能力边界"),
            // W-01：作者内容独有的**药水栏**。当前记为**具名跳过**（不是静默丢弃）以便作者宿主能开局，
            // 阻塞链得以继续；§19.2 要求"有该机制的 Gym 必须纳入药水栏，且用现有实体/字段/关系表达、
            // 不得借用卡牌 pile 语义"。把它**发布**进当前共享目录会像"作者独有字段"一样改动程序化冻结切片的
            // 目录与规范字节（实测会打掉 net 40 项 + J-01 4 项）⇒ 合规纳入必须走 **Profile 变体**
            // （W-01 的"Profile 显式选择"），在作者 Profile 里用具名实体/字段/关系声明药水槽位、容量与可用状态。
            // W-01：整局侧的两个**作者内容独有**集合（Run 单例与长期牌组）。与药水栏同理：
            // 这里先用**具名跳过**打通阻塞链；合规纳入（整局 HP/金币/长期牌组对 AI 可见）走 Profile 变体。
            ["base-spire:RunStates"] = CollectionDisposition.Skip("N-02-AUTHOR-ONLY",
                "整局单例集合；其 HP/金币等公开事实应由作者 Profile 变体用具名实体字段发布，当前共享目录不改动"),
            ["base-spire:LongTermDeck"] = CollectionDisposition.Skip("N-02-AUTHOR-ONLY",
                "长期牌组；W-05 已把长期实例与战斗实例分成两批，公开表达需作者 Profile 变体（不得借用卡牌 pile 语义）"),
            ["base-spire:PotionBelt"] = CollectionDisposition.Skip("N-02-AUTHOR-ONLY",
                "作者内容独有的药水栏；合规纳入需 Profile 变体（用具名实体/字段/关系表达槽位/容量/可用状态，" +
                "不得借用卡牌 pile 语义），而当前共享目录不能改动（否则冻结切片目录/字节全变）"),
            ["base-spire:Known"] = CollectionDisposition.Skip("N-02-KNOWN-FACT",
                "KnownCard 事实已逐个作为实体发布，其卡牌引用与位置经 known_positions 表达；" +
                "protocol.md:94 禁止用 groups/sequences 重复表达成员事实"),
        };
    }

    private static IReadOnlyDictionary<string, Disposition<PublicRelationRule>> Relations() =>
        new Dictionary<string, Disposition<PublicRelationRule>>(StringComparer.Ordinal)
        {
            ["base-spire:Powers"] = Disposition<PublicRelationRule>.Publish(new PublicRelationRule(
                "net:relation:base-spire:Powers",
                [Kind(PlayerType), Kind(MonsterType)],
                [Kind(PowerType)])),
            // 授权边不是公开游戏事实：它的公开表达就是 known_positions（H-01/H-02）。
            // 发布成 relation 会把同一成员事实重复表达（protocol.md:94），也会把授权结构暴露给消费者。
            ["base-spire:KnownReveal"] = Disposition<PublicRelationRule>.Skip("H-01-REVEAL-EDGE",
                "观看者授权边由 known_positions.card_ref 表达（protocol.md:83-90）；" +
                "它记录的是谁被授权，不是游戏事实，不作公开关系发布，也不得从它反推隐藏牌序"),
            ["base-spire:PlayedReveal"] = Disposition<PublicRelationRule>.Skip("H-01-PLAYED-REVEAL-EDGE",
                "已打牌的身份授权由实际牌实例及历史表达；授权边不作为游戏关系发布，不透露抽牌位置"),
        };

    private static IReadOnlyDictionary<string, PublicActionRule> Actions() =>
        new Dictionary<string, PublicActionRule>(StringComparer.Ordinal)
        {
            ["base-spire:PlayCard"] = new PublicActionRule(
                "net:action:base-spire:PlayCard",
                new Dictionary<int, PublicActionField>
                {
                    // 执行所需字段：protocol.md:98 要求所有执行所需字段必须 known，故显式 required。
                    // 出牌参数的候选恒为手牌，因此目标种类可以收窄到卡牌。
                    [0] = new PublicActionField("net:field:base-spire:PlayCard.Card", PublicValueKind.EntityRef,
                        Required: true, TargetKindIds: [CardKindId]),
                }),
            ["base-spire:EndTurn"] = new PublicActionRule("net:action:base-spire:EndTurn",
                new Dictionary<int, PublicActionField>()),
        };

    private static PublicActionRule AnswerAction() => new(
        "net:action:net.answer_decision",
        new Dictionary<int, PublicActionField>
        {
            // 决策域不是单一类型：本切片的候选既有弃牌堆卡牌（头槌选择）也有敌人（目标选择）。
            // 只声明卡牌会把真实候选判成类型错；这里显式声明本切片实际的候选种类集合。
            [0] = new PublicActionField("net:field:net.answer_decision.Instance", PublicValueKind.EntityRef,
                Required: true, TargetKindIds: [CardKindId, MonsterKindId]),
        },
        NetDeclared: true);

    private static IReadOnlyDictionary<string, PublicTerminalRule> Terminals() =>
        new Dictionary<string, PublicTerminalRule>(StringComparer.Ordinal)
        {
            ["base-spire:Victory"] = new PublicTerminalRule(
                "net:reason:base-spire:Victory", "win",
                new Dictionary<string, Disposition<PublishedField>>(StringComparer.Ordinal)
                {
                    ["Turn"] = Number("base-spire:Victory", "Turn"),
                }),
            ["base-spire:Defeat"] = new PublicTerminalRule(
                "net:reason:base-spire:Defeat", "loss",
                new Dictionary<string, Disposition<PublishedField>>(StringComparer.Ordinal)
                {
                    ["Turn"] = Number("base-spire:Defeat", "Turn"),
                }),
        };

    public static string Kind(string engineTypeQualifiedId) => "net:kind:" + engineTypeQualifiedId;

    public static string FieldId(string engineTypeQualifiedId, string fieldName) =>
        "net:field:" + engineTypeQualifiedId + "." + fieldName;

    public static string CategoryId(string raw) => "net:cat:" + raw;

    private static Disposition<PublishedField> Number(string engineTypeQualifiedId, string fieldName) =>
        Disposition<PublishedField>.Publish(new PublishedField(FieldId(engineTypeQualifiedId, fieldName),
            PublicValueKind.Integer));

    private static Disposition<PublishedField> Boolean(string engineTypeQualifiedId, string fieldName) =>
        Disposition<PublishedField>.Publish(new PublishedField(FieldId(engineTypeQualifiedId, fieldName),
            PublicValueKind.Boolean));

    private static Disposition<PublishedField> Category(string engineTypeQualifiedId, string fieldName,
        IReadOnlyDictionary<string, string> values) =>
        Disposition<PublishedField>.Publish(new PublishedField(FieldId(engineTypeQualifiedId, fieldName),
            PublicValueKind.Category, CategoryValues: values));

    private static Disposition<PublishedField> ContentIsEntityFact(string engineTypeQualifiedId, string fieldName) =>
        Disposition<PublishedField>.Skip("N-02-CONTENT",
            "由 entity.content_state/content_id 表达（protocol.md:62/66），不再重复进 fields");
}
