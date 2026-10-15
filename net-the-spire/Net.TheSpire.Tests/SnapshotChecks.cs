using BaseSpire.Slice;
using CoreTheSpire.Engine;
using NetTheSpire.Contracts;
using NetTheSpire.Contracts.Dto;
using NetTheSpire.Control;
using NetTheSpire.Projection;

namespace NetTheSpire.Tests;

/// <summary>
/// N-02 的检查：引擎 <see cref="ParticipantView"/> → 协议 <c>PublicSnapshot</c> 的映射、候选展开与暂停期语义。
///
/// 覆盖监督者在台账 §4 给 N-02 的九条预审里**可从 C# 侧独立断言**的部分：
/// ① 候选严格按公开语义键的 UTF-8 字节序排列（<c>contract-artifacts.md#3</c>）；② <c>candidate_id</c> 不进特征；
/// ③ <c>AwaitingDecision</c> → 协议 <c>active</c> + 快照 <c>status=decision</c>，候选恰为 <c>PendingDecision.Candidates</c>；
/// ④ <c>observation_id</c> 绑定稳定状态版本且纯读可重复；⑤ 切片不声明 sequences/groups（输出空数组、成员引用闭合）；
/// ⑥ 隐藏牌堆只暴露真实总数（V-02）；⑦ 终局/故障/空候选的边界组合被显式拒绝；⑧ §6 未裁定项以具名 TODO 记录。
/// </summary>
internal static class SnapshotChecks
{
    private const string PlayerViewId = "base-spire:PlayerView";
    private const string PlayCardId = "base-spire:PlayCard";
    private const string EndTurnId = "base-spire:EndTurn";
    private const string CardTypeId = "base-spire:Card";
    private const string MonsterTypeId = "base-spire:Monster";
    private const string HandCollectionId = "base-spire:Hand";
    private const string PlayCardFieldId = "net:field:base-spire:PlayCard.Card";
    private const string SessionId = "s-net-02";

    public static IEnumerable<(string Name, Action Body)> Build()
    {
        yield return ("N-02 引擎会话状态映射到协议 session_status，并给出是否发布快照", SessionStatusMapping);
        yield return ("N-02 活跃会话投影为 active + status=decision，且 observation_id 绑定稳定版本", ActiveProjectsDecisionSnapshot);
        yield return ("N-02 候选严格按公开语义键的 UTF-8 字节序排列", CandidatesSortedBySemanticKey);
        yield return ("N-02 candidate_id 只作 step 引用、不进入任何特征", CandidateIdNotInFeatures);
        yield return ("N-02 暂停期投影为 active + status=decision，候选恰为 PendingDecision.Candidates", PausedMapsToActiveDecision);
        yield return ("N-02 observation_id 在同一稳定版本上可重复（纯读，不改状态）", ObservationIdIsStableAcrossReads);
        yield return ("N-02 切片不声明 sequences/groups，输出空数组且成员引用闭合", NoSequencesOrGroupsAndClosedReferences);
        yield return ("N-02 隐藏牌堆只暴露真实总数、不暴露成员（V-02）", HiddenDrawPileLeaksNoMembers);
        yield return ("N-02 终局/故障/空候选的边界组合被显式拒绝", BoundaryRejections);
        yield return ("N-02 §6 未裁定冲突以具名 TODO 记录在切片 profile 内", UndecidedConflictIsRecorded);
        yield return ("H-01/H-02 已知位置经真实实例引用进入 wire，并满足 I-27/I-29", KnownPositionReachesTheWire);
        yield return ("H-02 目录声明 pile_types 且 pile_ref 唯一匹配牌堆种类", CatalogDeclaresPilesAndKinds);
        yield return ("H-02 entity_ref 目标种类与必需字段来自显式声明", TargetKindsAndRequirednessAreDeclared);
        yield return ("W-02 只对调不可见创建顺序的成对场景：公开快照逐字节相同", CreationOrderLeaksNothing);
        yield return ("W-02 重复同定义实例各有互不相同的公开引用，且重复投影保持同一绑定", DuplicateDefinitionsGetStableRefs);
        yield return ("W-02 已打牌授权与可见弃牌成员重叠时，只公开一次且引用闭合", PlayedAuthorizationOverlap);
        yield return ("W-02 跨状态持久绑定：同定义实例的公开字段变化后句柄不换绑", RefsSurvivePublishedFieldChanges);
        yield return ("W-02 生成/销毁：新实例取新句柄、销毁句柄不复用、候选引用同一批持久句柄", RefsSurviveCreationAndDestruction);
        yield return ("W-02 撤销再授权：同一实例重新揭示后仍是同一公开句柄", RefsSurviveRevocationAndRegrant);
        yield return ("W-01 作者内容包与程序化冻结切片是可选的两套包（内容确实不同）", AuthorContentIsSelectable);
        yield return ("W-01 作者变体发布药水 kind/枚举、药水栏（group+容量）与 UsePotion", AuthorVariantPublishesPotions);
        yield return ("W-01 作者变体的 describe 限额/结构声明与实际发布一致", AuthorDescribeMatchesPublishedFacts);
        yield return ("W-01 作者变体发布回合摘要与卡牌成长变量，且默认 Profile 不受影响", AuthorVariantPublishesDecisionFacts);
        yield return ("W-01 成对局面：当前 HP 相同、回合摘要不同 ⇒ 快照差异保留而不被抹掉", TurnSummaryDifferenceSurvivesTheWire);
    }

    // -- helpers -----------------------------------------------------------------------------------------------

    private static Session NewBattle(bool includeHeadbutt = false, bool swapEncounterOrder = false,
        bool sameDefinitionEnemyPair = false)
    {
        var builder = new RuleSetBuilder();
        builder.Add(CombatSlicePackage.Build(includeHeadbutt, swapEncounterOrder: swapEncounterOrder,
            sameDefinitionEnemyPair: sameDefinitionEnemyPair).Build());
        var session = new Session(builder.Build(), 20261001UL, [SliceManifest.PlayerRole]);
        session.Start();
        return session;
    }

    private static PublicSnapshotProjector Projector(Session session) =>
        new(session.RuleSet, SlicePublicProfile.Create(), SessionId);

    private static void PlayedAuthorizationOverlap()
    {
        var session = NewBattle();
        var target = session.State.AllInstances(session.RuleSet.TypeByQualifiedId(MonsterTypeId).Id).First();
        PlayHandCardAt(session, SliceManifest.StrikeId, target);
        var snapshot = Project(session, Projector(session)).Snapshot
            ?? throw new CheckFailedException("played card must publish a snapshot");
        foreach (var pile in snapshot.Piles)
            Check.Equal(pile.VisibleMembers.Count, pile.VisibleMembers.Distinct().Count(), "authorized and ordinary membership form a set union");
    }

    /// <summary>
    /// W-02 成对验收：两个场景**只有两个敌人的创建顺序不同**（可见事实完全一致），
    /// 因此公开快照必须逐字节相同——旧的 `"e" + 创建序号` 方案下这条必然失败。
    /// </summary>
    private static void CreationOrderLeaksNothing()
    {
        var straight = NewBattle(swapEncounterOrder: false);
        var swapped = NewBattle(swapEncounterOrder: true);
        var left = Project(straight, Projector(straight)).Snapshot
            ?? throw new CheckFailedException("原顺序场景必须给出快照");
        var right = Project(swapped, Projector(swapped)).Snapshot
            ?? throw new CheckFailedException("对调顺序场景必须给出快照");
        Check.Equal(WireCanonical.Text(left.ToWire()), WireCanonical.Text(right.ToWire()),
            "只对调创建顺序：公开快照（含实体/牌堆/关系/候选的规范顺序）逐字节相同");

        var leftRefs = Refs(straight);
        var rightRefs = Refs(swapped);
        Check.Equal(leftRefs.Count, rightRefs.Count, "两个场景的公开句柄数量相同");
        Check.True(leftRefs.Values.OrderBy(value => value, StringComparer.Ordinal)
                .SequenceEqual(rightRefs.Values.OrderBy(value => value, StringComparer.Ordinal)),
            "两个场景分配到的公开句柄集合相同（编号不含创建顺序）");
    }
    /// <summary>
    /// W-02 引用稳定性（本批次覆盖"重复同定义实例"与"重复投影"两点）：
    /// 同一内容的多个实例必须拿到**互不相同**的公开引用，且纯读重复投影得到**同一绑定**。
    /// </summary>
    private static void DuplicateDefinitionsGetStableRefs()
    {
        var session = NewBattle();
        var before = Refs(session);
        var view = session.Observe(SliceManifest.Player, PlayerViewId);
        var cards = view.Instances.Where(instance => instance.TypeId == "base-spire:Card").ToList();
        var duplicates = cards
            .GroupBy(instance => instance.Fields.First(field => field.Name == "DefinitionId").Value.ToString(),
                StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .ToList();
        Check.True(duplicates.Count > 0, "受控切片起手牌里有同定义的重复实例（打击 x5）");
        foreach (var group in duplicates)
        {
            var handles = group.Select(instance => before[instance.Id]).ToList();
            Check.Equal(group.Count(), handles.Distinct(StringComparer.Ordinal).Count(),
                $"同定义实例（{group.Key}）的公开引用互不相同");
        }

        var again = Refs(session);
        Check.True(before.OrderBy(pair => pair.Value, StringComparer.Ordinal)
                .SequenceEqual(again.OrderBy(pair => pair.Value, StringComparer.Ordinal)),
            "纯读重复投影得到同一公开引用绑定（会话内稳定）");
    }
    /// <summary>
    /// W-01：宿主新增的 `--author-content` 开关必须真的装载**另一套内容**（作者文本包），而不是同包的别名。
    /// 这里断言两套包在**声明层**可区分（Run 实体/长期牌组只由作者文本声明产生）；
    /// 经真实 stdio 的端到端验证由客户端脚本负责（见阶段交接的下一动作）。
    /// </summary>
    private static void AuthorContentIsSelectable()
    {
        var programmaticBuilder = new RuleSetBuilder();
        programmaticBuilder.Add(CombatSlicePackage.Build().Build());
        var authorBuilder = new RuleSetBuilder();
        authorBuilder.Add(CombatSlicePackage.Build(fromAuthorText: true).Build());
        var programmatic = programmaticBuilder.Build();
        var author = authorBuilder.Build();

        Check.False(programmatic.Collections.Any(entry => entry.QualifiedId == "base-spire:LongTermDeck"),
            "程序化冻结切片不建长期牌组");
        Check.True(author.Collections.Any(entry => entry.QualifiedId == "base-spire:LongTermDeck"),
            "作者内容包建长期牌组（作者文本声明了 run）");
        Check.True(author.Types.Count > 0 && programmatic.Types.Count > 0, "两套包都可构建");
    }
    /// <summary>
    /// W-01：作者 Profile 变体必须把**玩家可见**的药水事实发布出去：药水实体（内容身份 + Rarity/Target
    /// 的 category 词表 + 数值）、药水栏（group + owner + 容量）、以及 `UsePotion` 的候选。
    ///
    /// 这里断言的是**映射结果**（不是"源码里存在"）：每个断言都对应快照/目录里的一个真实字段或声明。
    /// 经真实 stdio 的同一批事实由 `net-the-spire/client/test_author_content.py` 覆盖。
    /// </summary>
    private static void AuthorVariantPublishesPotions()
    {
        var builder = new RuleSetBuilder();
        builder.Add(CombatSlicePackage.Build(fromAuthorText: true).Build());
        var rules = builder.Build();
        var profile = SlicePublicProfile.CreateAuthorContent(rules);
        Check.Equal(SlicePublicProfile.AuthorCatalogV3, profile.CatalogId,
            "作者变体身份与默认 Profile 不同");
        var session = new Session(rules, 20261001UL, [SliceManifest.PlayerRole]);
        session.Start();
        var projector = new PublicSnapshotProjector(rules, profile, SessionId);
        var projected = Project(session, projector);
        var snapshot = projected.Snapshot!;

        // ① 药水实体：内容身份 + Rarity/Target（category，值必须是目录声明的类别 ID）+ PowerStacks。
        var potions = snapshot.Entities.Where(entity => entity.KindId == SlicePublicProfile.PotionKindId).ToList();
        Check.Equal(2, potions.Count, "作者文本的药水栏声明两瓶药水");
        var beetle = potions.Single(potion => potion.ContentId == "base-spire:beetle-juice");
        var blessing = potions.Single(potion => potion.ContentId == "base-spire:blessing-of-the-forge");
        Check.Equal("net:cat:rare", PotionCategory(beetle, "Rarity"), "beetle-juice 稀有度 rare 按词表发布");
        Check.Equal("net:cat:enemy", PotionCategory(beetle, "Target"), "beetle-juice 目标是敌人");
        Check.Equal("4", PotionNumber(beetle, "PowerStacks"), "beetle-juice 的层数是公开数值");
        Check.Equal("net:cat:uncommon", PotionCategory(blessing, "Rarity"), "blessing 稀有度 uncommon");
        Check.Equal("net:cat:self", PotionCategory(blessing, "Target"), "blessing 目标是自己");
        foreach (var potion in potions)
        {
            Check.True(potion.Fields.All(field => field.FieldId != "net:field:base-spire:Potion.PowerId"),
                "未附加能力时 PowerId 是空串：具名跳过而不是发布一个空类别值");
        }

        // ② 药水栏：发布为 group（owner = 玩家、成员 = 药水），不借用卡牌 pile 语义。
        Check.False(snapshot.Piles.Any(pile => pile.VisibleMembers.Any(member =>
                snapshot.Entities.Any(entity => entity.InstanceId == member &&
                    entity.KindId == SlicePublicProfile.PotionKindId))),
            "药水不得出现在 piles 里（不得借用仅适用卡牌的 pile 语义）");
        var belt = snapshot.Groups.Single(group => group.TypeId == SlicePublicProfile.PotionBeltGroupTypeId);
        Check.Equal(SlicePublicProfile.PotionBeltGroupTypeId, belt.TypeId, "药水栏有公开 group_type");
        var owner = snapshot.Entities.Single(entity => entity.InstanceId == belt.OwnerRef);
        Check.Equal(SlicePublicProfile.Kind(SlicePublicProfile.PlayerType), owner.KindId,
            "药水栏 owner 是玩家实体（不是写死的引用）");
        Check.Sequence(potions.Select(potion => potion.InstanceId).OrderBy(id => id, StringComparer.Ordinal),
            belt.Members.Select(member => member.EntityRef).OrderBy(id => id, StringComparer.Ordinal),
            "药水栏成员恰为药水实体");
        Check.True(belt.Members.All(member => member.Multiplicity == 1), "引擎集合成员唯一 ⇒ multiplicity 恒为 1");

        // ③ 目录声明：容量来自**引擎声明的集合容量**（3 个槽），类别词表与 wire 值同源。
        var declarations = CatalogProjector.Project(profile).Declarations;
        var beltType = declarations.GroupTypes.Single(type => type.Id == SlicePublicProfile.PotionBeltGroupTypeId);
        Check.Equal(SlicePublicProfile.PotionBeltGroupTypeId, beltType.Id, "目录声明药水栏 group_type");
        Check.Sequence([SlicePublicProfile.Kind(SlicePublicProfile.PlayerType)], beltType.OwnerKindIds,
            "owner_kind_ids 来自 Profile 声明");
        Check.Sequence([SlicePublicProfile.PotionKindId], beltType.MemberKindIds, "member_kind_ids = 药水种类");
        Check.Equal(0L, beltType.MinCount, "min_count = 0（空药水栏是合法状态）");
        Check.Equal(3L, beltType.MaxCount!.Value, "max_count 来自引擎声明的药水栏容量（3 个槽位）");
        var rarity = declarations.Fields.Single(field => field.Id == "net:field:base-spire:Potion.Rarity");
        Check.Sequence(["net:cat:common", "net:cat:rare", "net:cat:uncommon"], rarity.CategoryIds ?? [],
            "目录登记 Rarity 的公开类别 ID（与 wire 值同源）");
        Check.Sequence(["net:cat:enemy", "net:cat:self"],
            declarations.Fields.Single(field => field.Id == "net:field:base-spire:Potion.Target").CategoryIds ?? [],
            "目录登记 Target 的公开类别 ID");

        // ④ UsePotion：唯一参数是药水引用，候选**恰为**药水栏成员，且绑定指向真实药水实例。
        var usePotion = snapshot.LegalActions
            .Where(action => action.ActionType == "net:action:base-spire:UsePotion")
            .ToList();
        Check.Equal(potions.Count, usePotion.Count, "每瓶药水各有一个 UsePotion 候选");
        Check.Sequence(potions.Select(potion => potion.InstanceId).OrderBy(id => id, StringComparer.Ordinal),
            usePotion.Select(action => Reference(action, "net:field:base-spire:UsePotion.Potion"))
                .OrderBy(id => id, StringComparer.Ordinal),
            "UsePotion 候选恰为药水栏成员");
        foreach (var binding in projected.Bindings.Where(binding =>
                     binding.ActionTypeId == "net:action:base-spire:UsePotion"))
        {
            Check.True(potions.Any(potion => potion.InstanceId == projected.InstanceRefs[binding.Arguments[0].AsRef()]),
                "候选绑定指向药水实例（可执行，不是只做展示）");
        }

        // ⑤ 药水**真的能被操作**：提交 beetle-juice → 药水的 target 步骤提问目标 → 药水离开药水栏、
        // 敌人获得 Shrink（作者内容独有的能力，只有作者变体登记了它）。
        var beetleAction = usePotion.Single(action =>
            Reference(action, "net:field:base-spire:UsePotion.Potion") == beetle.InstanceId);
        var beetleBinding = projected.Bindings.Single(binding => binding.CandidateId == beetleAction.CandidateId);
        session.Submit(beetleBinding.Action!, SliceManifest.Player, beetleBinding.Arguments);
        var pending = session.PendingDecision
            ?? throw new CheckFailedException("药水的 target 步骤必须在真实链路上暂停提问目标");
        var target = session.State.CollectionMembers(Collection(rules, "base-spire:Monsters").Id)[0];
        Check.True(pending.Candidates.Contains(target), "药水的目标候选是敌人实例");
        session.Answer(new DecisionAnswer(pending.RequestId, [target]));

        var afterUse = Project(session, projector);
        var beltAfter = afterUse.Snapshot!.Groups.Single(group =>
            group.TypeId == SlicePublicProfile.PotionBeltGroupTypeId);
        Check.Equal(1, beltAfter.Members.Count, "用过的药水离开药水栏（消耗）");
        Check.False(beltAfter.Members.Any(member => member.EntityRef == beetle.InstanceId),
            "用掉的那瓶药水不再是药水栏成员");
        var shrink = afterUse.Snapshot!.Entities.SingleOrDefault(entity =>
            entity.KindId == SlicePublicProfile.Kind("base-spire:Power") &&
            entity.ContentId == "base-spire:shrink");
        Check.True(shrink is not null, "beetle-juice 的效果在真实链路上生效（敌人获得 Shrink）");
        Check.Equal("4", ((WireString)shrink!.Fields.Single(field =>
            field.FieldId == "net:field:base-spire:Power.Stacks").Value!).Value, "Shrink 层数是药水声明的 4");

        // ⑥ 整局状态与长期牌组：Run 的公开字段（HP/最大 HP/金币）来自作者文本的 `run { … }`；
        //    长期牌组发布为 group（owner = Run），其成员是**长期那批实例**，与战斗里的实例互不相交。
        var run = snapshot.Entities.Single(entity => entity.KindId == SlicePublicProfile.RunKindId);
        Check.Equal("80", RunNumber(run, "Hp"), "整局 HP 是作者声明的 80");
        Check.Equal("80", RunNumber(run, "MaxHp"), "整局最大 HP 是作者声明的 80");
        Check.Equal("99", RunNumber(run, "Gold"), "整局金币是作者声明的 99");
        var deck = snapshot.Groups.Single(group => group.TypeId == SlicePublicProfile.LongTermDeckGroupTypeId);
        Check.Equal(run.InstanceId, deck.OwnerRef, "长期牌组的 owner 是 Run 实体（动态解析，不是写死的引用）");
        var battleCards = snapshot.Piles
            .Where(pile => "net:pile_type:base-spire:hand" == pile.PileRef ||
                           "net:pile_type:base-spire:draw" == pile.PileRef ||
                           "net:pile_type:base-spire:discard" == pile.PileRef)
            .SelectMany(pile => pile.VisibleMembers)
            .ToHashSet(StringComparer.Ordinal);
        Check.True(deck.Members.Count > 0, "长期牌组有成员（作者文本的 deck{} 声明）");
        Check.False(deck.Members.Any(member => battleCards.Contains(member.EntityRef)),
            "长期牌组与战斗牌区是**两批实例**（同一张牌的两批实例不得被合一）");
        Check.Equal(deck.Members.Count, deck.Members.Select(member => member.EntityRef).Distinct(StringComparer.Ordinal).Count(),
            "长期牌组成员互相不同");
        var deckType = declarations.GroupTypes.Single(type => type.Id == SlicePublicProfile.LongTermDeckGroupTypeId);
        Check.Sequence([SlicePublicProfile.RunKindId], deckType.OwnerKindIds, "长期牌组的 owner kind 是整局");
        Check.Sequence([SlicePublicProfile.CardKindId], deckType.MemberKindIds, "长期牌组的成员 kind 是卡牌");
        Check.True(deckType.MaxCount is null, "长期牌组未声明容量上限 ⇒ max_count 为 null（不编造上限）");
    }

    /// <summary>
    /// W-01：作者变体的 `describe` **能力/限额**必须与实际发布的事实一致——发布 group 的 Profile 不能再报
    /// `groups: 0`（消费者会按这些数字分配缓冲），而且真实快照不得超出声明的上限。
    /// </summary>
    private static void AuthorDescribeMatchesPublishedFacts()
    {
        var builder = new RuleSetBuilder();
        builder.Add(CombatSlicePackage.Build(fromAuthorText: true).Build());
        var rules = builder.Build();
        var profile = SlicePublicProfile.CreateAuthorContent(rules);
        var host = new ControlHost(rules, profile, SliceManifest.Player, SliceManifest.PlayerRole, PlayerViewId,
            20261001UL);
        var described = DescribeResultDto.FromWire(host.Dispatch(
            new RequestDto("d-author", Operation.Describe, new WireObject([]))).Result!);
        var capabilities = described.Capabilities;

        Check.Equal(SlicePublicProfile.AuthorCatalogV3, described.Identity.CatalogId,
            "describe 身份来自作者变体");
        var groups = capabilities.SupportedStructures.Single(structure => structure.Structure == "Group");
        Check.Sequence(profile.GroupTypes.Select(group => group.GroupTypeId)
                .OrderBy(id => id, StringComparer.Ordinal),
            groups.DeclarationIds.OrderBy(id => id, StringComparer.Ordinal),
            "Group 结构声明必须列出真实 group_type（药水栏 + 长期牌组）");
        Check.Equal((long)profile.GroupTypes.Count, capabilities.Limits.Groups,
            "groups 限额 = 声明的 group_type 数（不得报 0）");
        Check.True(capabilities.Limits.MembersPerGroup > 0,
            "发布 group 时 members_per_group 必须为正（成员都是实体 ⇒ 以实体限额为上界）");

        var session = new Session(rules, 20261001UL, [SliceManifest.PlayerRole]);
        session.Start();
        var snapshot = Project(session, new PublicSnapshotProjector(rules, profile, SessionId)).Snapshot!;
        Check.True(snapshot.Groups.Count <= capabilities.Limits.Groups,
            $"实际 group 数 {snapshot.Groups.Count} 不得超过声明限额 {capabilities.Limits.Groups}");
        Check.True(snapshot.Groups.All(group => group.Members.Count <= capabilities.Limits.MembersPerGroup),
            "实际 group 成员数不得超过声明限额");
        Check.True(snapshot.Entities.Count <= capabilities.Limits.Entities,
            $"实际实体数 {snapshot.Entities.Count} 不得超过声明限额 {capabilities.Limits.Entities}");
        Check.True(snapshot.Piles.Count <= capabilities.Limits.Piles, "实际 pile 数不得超过声明限额");
    }

    /// <summary>
    /// W-01（本轮）：作者 Gym 必须发布**影响决策的当前事实**，且默认受控切片的目录**不受影响**。
    ///
    /// 这三个字段都不是"同一份快照里其它字段的函数"：
    /// <list type="bullet">
    /// <item><c>LostHpThisTurn</c>/<c>ExhaustedCardThisTurn</c> 的时间范围是"本回合"——只出现在**回合内发生过**
    /// 这件事上。当前 Hp 无法区分"本回合掉过血"与"上一回合掉过、本回合没掉"；消耗区的现值同样无法区分
    /// "本回合消耗过"与"更早消耗过"。作者的 Spite/EvilEye 正是按这两个布尔分支的牌。</item>
    /// <item><c>Variable_extra-damage</c> 是卡牌实例自己的累积成长量（Rampage：初值 9、每次 +5/+9）。</item>
    /// </list>
    /// 因此作者变体**发布**它们；默认 Profile 仍具名跳过（默认切片没有读它们的动作/终局判定，
    /// 发布它只会改动共享目录与规范字节）。
    /// </summary>
    private static void AuthorVariantPublishesDecisionFacts()
    {
        var authorBuilder = new RuleSetBuilder();
        authorBuilder.Add(CombatSlicePackage.Build(fromAuthorText: true).Build());
        var authorRules = authorBuilder.Build();
        var authorProfile = SlicePublicProfile.CreateAuthorContent(authorRules);
        var authorSession = new Session(authorRules, 20261001UL, [SliceManifest.PlayerRole]);
        authorSession.Start();
        var playerType = authorRules.TypeByQualifiedId(SlicePublicProfile.PlayerType);
        var player = authorSession.State.AllInstances(playerType.Id).Single();
        authorSession.State.RunAtomic(() =>
        {
            authorSession.State.SetField(player, playerType.FieldIndex("LostHpThisTurn"), Value.Of(true));
            authorSession.State.SetField(player, playerType.FieldIndex("ExhaustedCardThisTurn"), Value.Of(true));
        });
        var projected = Project(authorSession,
            new PublicSnapshotProjector(authorRules, authorProfile, SessionId));
        var snapshot = projected.Snapshot!;

        // ① 两个回合摘要按 boolean 发布，且**真的**是当前状态值（不是恒真的占位）。
        var playerEntity = snapshot.Entities.Single(entity =>
            entity.KindId == SlicePublicProfile.Kind(SlicePublicProfile.PlayerType));
        Check.True(Boolean(playerEntity, SlicePublicProfile.LostHpThisTurnField),
            "作者变体发布 LostHpThisTurn 且值为真（本回合失去过生命）");
        Check.True(Boolean(playerEntity, SlicePublicProfile.ExhaustedCardThisTurnField),
            "作者变体发布 ExhaustedCardThisTurn 且值为真（本回合消耗过牌）");

        // 对照组：同一份作者目录里，把这两个字段改回假，wire 上的值必须跟着变（字段是可变的当前事实）。
        authorSession.State.RunAtomic(() => authorSession.State.SetField(player,
            playerType.FieldIndex("LostHpThisTurn"), Value.Of(false)));
        var flipped = Project(authorSession,
            new PublicSnapshotProjector(authorRules, authorProfile, SessionId)).Snapshot!.Entities.Single(entity =>
            entity.KindId == SlicePublicProfile.Kind(SlicePublicProfile.PlayerType));
        Check.False(Boolean(flipped, SlicePublicProfile.LostHpThisTurnField),
            "写回假之后 wire 上也是假（发布的是当前值，不是一次性快照）");

        // ③ 目录（发射物）声明这三个字段：只查 profile 内部清单会放过"清单有、wire 没有"的缺口。
        var authorCatalog = CatalogProjector.Project(authorProfile).Declarations;
        foreach (var fieldId in new[]
                 {
                     SlicePublicProfile.LostHpThisTurnField,
                     SlicePublicProfile.ExhaustedCardThisTurnField,
                     SlicePublicProfile.ExtraDamageVariableField,
                 })
        {
            Check.True(authorCatalog.Fields.Any(field => field.Id == fieldId),
                $"作者目录必须声明 {fieldId}");
        }

        // ④ 成长变量随**真实出牌**写回，发布值跟着变。
        //    受控场景：冻结作者 Gym 的牌组里没有带成长变量的牌，因此这里把**已经声明的** Rampage 加进卡组
        //    （只改测试模型，不改 `slice-content.author` 的内容，也不动 Gym 身份）。
        //    Rampage 的成长语义（9 → 14）另由 base-spire 的 B-06a 行为检查覆盖；这里只断言
        //    "公开变量 = 实例当前值"，即消费者看到的就是规则读的那个数。
        var frozen = SliceAuthorFrontend.Load();
        var withRampage = frozen with
        {
            Deck = [.. frozen.Deck, new AuthorDeckEntry("base-spire:rampage", 1)],
        };
        var growthBuilder = new RuleSetBuilder();
        growthBuilder.Add(CombatSlicePackage.Build(authorModel: withRampage).Build());
        var growthRules = growthBuilder.Build();
        var growthSession = new Session(growthRules, 20261001UL, [SliceManifest.PlayerRole]);
        growthSession.Start();
        var growthProjector = new PublicSnapshotProjector(growthRules,
            SlicePublicProfile.CreateAuthorContent(growthRules), SessionId);
        var growthCardType = growthRules.TypeByQualifiedId(SlicePublicProfile.CardType);
        // 长期牌组里也有一张 Rampage（它不会被出牌写回），因此这里明确取**战斗那批**的实例。
        var battleCollections = new[]
            {
                "base-spire:Hand", "base-spire:DrawPile", "base-spire:DiscardPile", "base-spire:ExhaustPile",
                "base-spire:InPlay",
            }
            .Select(collectionId => Collection(growthRules, collectionId).Id)
            .ToHashSet();
        var rampageId = growthSession.State.AllInstances(growthCardType.Id).Single(instance =>
            growthSession.State.GetField(instance, growthCardType.FieldIndex("DefinitionId")).AsString()
                == "base-spire:rampage"
            && battleCollections.Any(collection => growthSession.State.IsMember(collection, instance)));
        MoveToHand(growthSession, growthRules, rampageId);
        var growthBeforeProjection = Project(growthSession, growthProjector);
        var rampageHandle = growthBeforeProjection.InstanceRefs[rampageId];
        var growthBefore = growthBeforeProjection.Snapshot!.Entities.Single(entity =>
            entity.InstanceId == rampageHandle);
        Check.Equal("9", ((WireString)growthBefore.Fields.Single(field =>
                field.FieldId == SlicePublicProfile.ExtraDamageVariableField).Value!).Value,
            "成长变量的公开值 = 声明的初值 9");

        // 走真实链路打出去（打击实例 → 目标提问 → 伤害结算 → `grow` 写回）。
        var growthTarget = growthSession.State.CollectionMembers(
            Collection(growthRules, "base-spire:Monsters").Id)[0];
        var playCard = growthSession.LegalActions(SliceManifest.Player)
            .Single(candidate => candidate.Definition.QualifiedId == "base-spire:PlayCard");
        growthSession.Submit(playCard.Definition, SliceManifest.Player, [Value.OfRef(rampageId)]);
        while (growthSession.PendingDecision is { } pending)
        {
            growthSession.Answer(new DecisionAnswer(pending.RequestId, [growthTarget]));
        }

        var growthAfter = Project(growthSession, growthProjector).Snapshot!.Entities.Single(entity =>
            entity.InstanceId == rampageHandle);
        Check.Equal("14", ((WireString)growthAfter.Fields.Single(field =>
                field.FieldId == SlicePublicProfile.ExtraDamageVariableField).Value!).Value,
            "打出后成长变量写回 9 + 5 = 14，且公开值同步（消费者只看当前快照就能算出下次伤害）");

        // ⑤ 默认受控切片**不受影响**：同一份快照里没有这三个字段，目录也不声明它们。
        var defaultBuilder = new RuleSetBuilder();
        defaultBuilder.Add(CombatSlicePackage.Build().Build());
        var defaultRules = defaultBuilder.Build();
        var defaultProfile = SlicePublicProfile.Create();
        var defaultSession = new Session(defaultRules, 20261001UL, [SliceManifest.PlayerRole]);
        defaultSession.Start();
        var defaultSnapshot = Project(defaultSession,
            new PublicSnapshotProjector(defaultRules, defaultProfile, SessionId)).Snapshot!;
        var defaultPlayer = defaultSnapshot.Entities.Single(entity =>
            entity.KindId == SlicePublicProfile.Kind(SlicePublicProfile.PlayerType));
        Check.False(defaultPlayer.Fields.Any(field =>
                field.FieldId is SlicePublicProfile.LostHpThisTurnField
                    or SlicePublicProfile.ExhaustedCardThisTurnField),
            "默认 Profile 不发布回合摘要（作者独有事实，未改动共享目录）");
        var defaultCatalog = CatalogProjector.Project(defaultProfile).Declarations;
        Check.False(defaultCatalog.Fields.Any(field =>
                field.Id is SlicePublicProfile.LostHpThisTurnField
                    or SlicePublicProfile.ExhaustedCardThisTurnField
                    or SlicePublicProfile.ExtraDamageVariableField),
            "默认目录不声明作者独有的三个字段（两条 Profile 的目录互不污染）");
        Check.True(defaultProfile.CatalogId != authorProfile.CatalogId,
            "两条 Profile 的身份不同（消费者能区分目录内容）");
    }

    /// <summary>
    /// W-01 成对验收：两个局面的**当前 HP、能量、手牌与牌区完全一致**，只有"本回合是否失去过生命"这一条
    /// 回合摘要不同 ⇒ 公开快照必须保留这个差异（不得被聚合或按"可推导"抹掉）。
    /// 这正是 Spite 的 <c>if { condition = { playerLostHpThisTurn } }</c> 分支所读的事实。
    /// </summary>
    private static void TurnSummaryDifferenceSurvivesTheWire()
    {
        static (Session Session, PublicSnapshotProfile Profile) AuthorSession()
        {
            var builder = new RuleSetBuilder();
            builder.Add(CombatSlicePackage.Build(fromAuthorText: true).Build());
            var rules = builder.Build();
            var session = new Session(rules, 20261001UL, [SliceManifest.PlayerRole]);
            session.Start();
            return (session, SlicePublicProfile.CreateAuthorContent(rules));
        }

        var (lostHp, lostHpProfile) = AuthorSession();
        var (keptHp, keptHpProfile) = AuthorSession();
        foreach (var (session, value) in new[] { (lostHp, true), (keptHp, false) })
        {
            var type = session.RuleSet.TypeByQualifiedId(SlicePublicProfile.PlayerType);
            var player = session.State.AllInstances(type.Id).Single();
            session.State.RunAtomic(() =>
                session.State.SetField(player, type.FieldIndex("LostHpThisTurn"), Value.Of(value)));
        }

        var left = Project(lostHp, new PublicSnapshotProjector(lostHp.RuleSet, lostHpProfile, SessionId)).Snapshot!;
        var right = Project(keptHp, new PublicSnapshotProjector(keptHp.RuleSet, keptHpProfile, SessionId)).Snapshot!;

        static string Hp(EntityDto entity) => ((WireString)entity.Fields
            .Single(field => field.FieldId == "net:field:base-spire:Player.Hp").Value!).Value;

        var leftPlayer = left.Entities.Single(entity =>
            entity.KindId == SlicePublicProfile.Kind(SlicePublicProfile.PlayerType));
        var rightPlayer = right.Entities.Single(entity =>
            entity.KindId == SlicePublicProfile.Kind(SlicePublicProfile.PlayerType));
        Check.Equal(Hp(rightPlayer), Hp(leftPlayer), "成对局面的当前 HP 相同（差别只在回合摘要）");
        Check.True(Boolean(leftPlayer, SlicePublicProfile.LostHpThisTurnField)
                   && !Boolean(rightPlayer, SlicePublicProfile.LostHpThisTurnField),
            "只有回合摘要一个布尔不同：失去过生命 = 真，另一局为假");
        Check.Sequence(legalActionTypes(right), legalActionTypes(left),
            "这一对局面的合法动作集合相同（差异是给消费者解释分支用的事实，不是动作掩码）");
    }

    /// <summary>受控测试输入：把一张已有实例搬进手牌（与 `SliceFixtures.PutInHand` 同一做法）。</summary>
    private static void MoveToHand(Session session, RuleSet rules, InstanceId card)
    {
        var hand = Collection(rules, "base-spire:Hand").Id;
        session.State.RunAtomic(() =>
        {
            foreach (var collection in rules.Collections)
            {
                if (session.State.IsMember(collection.Id, card))
                {
                    session.State.RemoveFromCollection(collection.Id, card);
                }
            }

            session.State.AddToCollection(hand, card);
        });
    }

    private static bool Boolean(EntityDto entity, string fieldId) =>
        ((WireBool)entity.Fields.Single(field => field.FieldId == fieldId).Value!).Value;

    private static IEnumerable<string> legalActionTypes(PublicSnapshotDto snapshot) =>
        snapshot.LegalActions.Select(action => action.ActionType).Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);

    private static FieldDto PotionField(EntityDto potion, string name) =>        potion.Fields.Single(field => field.FieldId == $"net:field:base-spire:Potion.{name}");

    private static string PotionCategory(EntityDto potion, string name) =>
        ((WireString)PotionField(potion, name).Value!).Value;

    private static string PotionNumber(EntityDto potion, string name) =>
        ((WireString)PotionField(potion, name).Value!).Value;

    private static string RunNumber(EntityDto run, string name) =>
        ((WireString)run.Fields.Single(field => field.FieldId == $"net:field:base-spire:Run.{name}").Value!).Value;

    private static string Reference(LegalActionDto action, string fieldId) =>
        ((WireString)action.Fields.Single(field => field.FieldId == fieldId).Value!).Value;

    /// <summary>W-02：公开句柄从投影的绑定表取，不由引擎序号推导。</summary>
    private static IReadOnlyDictionary<InstanceId, string> Refs(Session session) =>
        Project(session, Projector(session)).InstanceRefs;

    /// <summary>
    /// W-02 跨状态持久绑定：同定义的两个敌人在**公开字段（Hp）变化**后必须保持同一公开句柄。
    ///
    /// 这是对"每份快照按公开字段重排编号"的直接反例：Hp 10 变成 4 之后，"Hp=4" 会排到 "Hp=20" 之后，
    /// 于是两个实例的编号交叉、句柄互换。本检查用一个**跨两次投影复用**的投影器来固定绑定，
    /// 并对照目标敌人确实掉了血（公开字段真的变了）。
    /// </summary>
    private static void RefsSurvivePublishedFieldChanges()
    {
        var session = NewBattle(sameDefinitionEnemyPair: true);
        var projector = Projector(session);
        var first = Project(session, projector);
        var monsters = MonstersByHp(session);
        Check.Equal(2, monsters.Count, "同定义敌人对场景必须有两个敌人且出生生命互不相同");
        var healthy = monsters[20L];
        var wounded = monsters[10L];
        var healthyRef = first.InstanceRefs[healthy];
        var woundedRef = first.InstanceRefs[wounded];
        Check.True(!string.Equals(healthyRef, woundedRef, StringComparison.Ordinal),
            "同定义的两个实例各有互不相同的公开引用");

        PlayHandCardAt(session, SliceManifest.StrikeId, wounded);
        Check.Equal(4L, HpOf(session, wounded), "目标敌人的 Hp 已按规则变化（公开字段确实变了）");

        var second = Project(session, projector);
        Check.Equal(woundedRef, second.InstanceRefs[wounded], "受伤实例跨快照保留同一公开句柄（不换绑）");
        Check.Equal(healthyRef, second.InstanceRefs[healthy], "未受伤的同定义实例也保留同一公开句柄");
        var entities = second.Snapshot!.Entities.Select(entity => entity.InstanceId)
            .ToHashSet(StringComparer.Ordinal);
        Check.True(entities.Contains(woundedRef) && entities.Contains(healthyRef),
            "两个实例仍在同一份公开快照里");
    }

    /// <summary>
    /// W-02 生成/销毁：一个实例被**销毁**后，其句柄不得复用；随后**新生成**的可见实例必须取全新句柄，
    /// 期间所有存活实例保持绑定；wire 上的候选引用与绑定表同源（候选对齐）。
    ///
    /// 场景用切片里真实存在的销毁路径：Headbutt 的 `top_from_discard` 依赖 `RecordKnownTop`，
    /// 而它先 `ClearKnownCards`（**销毁**旧的 KnownCard 事实）再创建新事实。切片里被击杀的敌人只是
    /// 离开 Monsters 集合、仍留在投影里，因此不能用来证明"销毁句柄不复用"。
    /// </summary>
    private static void RefsSurviveCreationAndDestruction()
    {
        var session = NewBattle(includeHeadbutt: true);
        var rules = session.RuleSet;
        var draw = Collection(rules, "base-spire:DrawPile").Id;
        var hand = Collection(rules, "base-spire:Hand").Id;
        var discard = Collection(rules, "base-spire:DiscardPile").Id;
        Relocate(session, SliceManifest.HeadbuttId, draw, hand);
        Relocate(session, SliceManifest.StrikeId, hand, discard);

        var projector = Projector(session);
        var before = Project(session, projector);
        var used = before.InstanceRefs.Values.ToHashSet(StringComparer.Ordinal);
        var front = session.State.CollectionMembers(Collection(rules, "base-spire:Monsters").Id)[0];

        // 第一次 Headbutt：生成 KnownCard 事实（新的可见实例）。
        PlayHandCardAt(session, SliceManifest.HeadbuttId, front);
        var first = Project(session, projector);
        var firstKnown = KnownFactRef(first);
        Check.False(used.Contains(firstKnown), $"新生成的实例必须取全新句柄：{firstKnown}");
        CheckHandlesStable(before.InstanceRefs, first.InstanceRefs, "第一次生成之后");

        // 结束回合：敌人回合生成 Ritual（新的可见实例）；新回合那张已知牌被抽到（或在重洗时）
        // ⇒ 已知事实被**销毁**。这一步同时覆盖"生成"与"销毁"两种变化。
        var endTurn = session.LegalActions(SliceManifest.Player)
            .Single(action => action.Definition.QualifiedId == EndTurnId);
        session.Submit(endTurn.Definition, SliceManifest.Player, []);
        var second = Project(session, projector);
        Check.Equal(0, second.Snapshot!.Entities.Count(entity => entity.KindId == "net:kind:base-spire:KnownCard"),
            "已知事实被销毁后不再出现在公开快照里");
        var spawned = second.InstanceRefs.Values.Where(handle => !used.Contains(handle)).ToList();
        Check.True(spawned.Count > 0, "回合推进后应出现新生成的可见实例（敌人的 Ritual 能力）");
        Check.False(spawned.Contains(firstKnown, StringComparer.Ordinal),
            "新生成的实例不得复用已被销毁实例的句柄");
        CheckHandlesStable(first.InstanceRefs, second.InstanceRefs, "生成与销毁之后");

        // 第二次 Headbutt：重新生成同一类事实，句柄必须是**全新**的（不复用被销毁的句柄）。
        Relocate(session, SliceManifest.HeadbuttId, discard, hand);
        used.UnionWith(second.InstanceRefs.Values);
        PlayHandCardAt(session, SliceManifest.HeadbuttId, front);
        var third = Project(session, projector);
        var secondKnown = KnownFactRef(third);
        Check.True(!string.Equals(firstKnown, secondKnown, StringComparison.Ordinal),
            $"销毁后重新生成的实例不得复用旧句柄（旧 {firstKnown}，新 {secondKnown}）");
        Check.False(used.Contains(secondKnown), $"新实例的句柄必须是全新分配的：{secondKnown}");
        CheckHandlesStable(second.InstanceRefs, third.InstanceRefs, "销毁并重新生成之后");

        // 候选对齐：wire 上的出牌候选引用必须与绑定表里同一批持久句柄一致。
        var expected = third.Bindings
            .Where(binding => binding.ActionTypeId == "net:action:base-spire:PlayCard")
            .Select(binding => third.InstanceRefs[binding.Arguments[0].AsRef()])
            .OrderBy(reference => reference, StringComparer.Ordinal)
            .ToList();
        var published = third.Snapshot!.LegalActions
            .Where(action => action.ActionType == "net:action:base-spire:PlayCard")
            .SelectMany(action => action.Fields)
            .Where(field => field.FieldId == PlayCardFieldId)
            .Select(field => ((WireString)field.Value!).Value)
            .OrderBy(reference => reference, StringComparer.Ordinal)
            .ToList();
        Check.True(expected.Count > 0, "当前边界仍有可提交的出牌候选");
        Check.Sequence(expected, published, "候选引用的公开句柄与绑定表同源（候选对齐）");
    }

    /// <summary>
    /// W-02 撤销再授权：同一实例的授权被**撤销**、之后**重新授权揭示**时，公开引用必须保持一致——
    /// 既不按授权状态重编号，也不因为新事实生成而在重新揭示时换一个新句柄。
    /// </summary>
    private static void RefsSurviveRevocationAndRegrant()
    {
        var session = NewBattle(includeHeadbutt: true);
        var rules = session.RuleSet;
        var draw = Collection(rules, "base-spire:DrawPile").Id;
        var hand = Collection(rules, "base-spire:Hand").Id;
        var discard = Collection(rules, "base-spire:DiscardPile").Id;
        var projector = Projector(session);

        Relocate(session, SliceManifest.HeadbuttId, draw, hand);
        var revealed = CardIn(session, hand, SliceManifest.StrikeId);
        MoveInstance(session, revealed, hand, discard);
        var first = Project(session, projector);
        var revealedRef = first.InstanceRefs[revealed];

        // 首次授权揭示：known_positions 指向该实例的持久公开句柄。
        PlayHandCardAt(session, SliceManifest.HeadbuttId, revealed);
        var published = Project(session, projector);
        Check.Equal(revealedRef, KnownPositionCardRef(published), "首次揭示使用该实例的持久公开句柄");

        // 撤销走真实链路：这张牌被抽到 ⇒ 授权事实销毁（而不是直接改字段）。
        var endTurn = session.LegalActions(SliceManifest.Player)
            .Single(action => action.Definition.QualifiedId == EndTurnId);
        session.Submit(endTurn.Definition, SliceManifest.Player, []);
        var revoked = Project(session, projector);
        Check.Equal(0, KnownPositionRefs(revoked).Count, "抽到已知牌后授权事实被销毁（撤销）");
        Check.True(session.State.IsMember(hand, revealed), "撤销后该实例已进入手牌（真实链路，非直接改字段）");
        Check.Equal(revealedRef, revoked.InstanceRefs[revealed], "撤销后该实例仍以同一公开句柄出现");

        // 再授权：把它放回弃牌堆，再经 Headbutt 揭示**同一个实例**。
        Relocate(session, SliceManifest.HeadbuttId, discard, hand);
        MoveInstance(session, revealed, hand, discard);
        PlayHandCardAt(session, SliceManifest.HeadbuttId, revealed);
        var regranted = Project(session, projector);
        Check.Equal(revealedRef, KnownPositionCardRef(regranted),
            "撤销后重新授权揭示的仍是同一公开句柄（跨授权周期稳定）");
        Check.Equal(revealedRef, regranted.InstanceRefs[revealed], "再授权后该实例仍是同一公开句柄");

        var expected = regranted.Bindings
            .Where(binding => binding.ActionTypeId == "net:action:base-spire:PlayCard")
            .Select(binding => regranted.InstanceRefs[binding.Arguments[0].AsRef()])
            .OrderBy(reference => reference, StringComparer.Ordinal)
            .ToList();
        var wire = regranted.Snapshot!.LegalActions
            .Where(action => action.ActionType == "net:action:base-spire:PlayCard")
            .SelectMany(action => action.Fields)
            .Where(field => field.FieldId == PlayCardFieldId)
            .Select(field => ((WireString)field.Value!).Value)
            .OrderBy(reference => reference, StringComparer.Ordinal)
            .ToList();
        Check.Sequence(expected, wire, "再授权后候选引用仍与绑定表同源");
    }

    /// <summary>当前快照里全部已知位置事实引用的卡牌句柄。</summary>
    private static List<string> KnownPositionRefs(ProjectedSnapshot projected) =>
        projected.Snapshot!.Piles.SelectMany(pile => pile.KnownPositions)
            .Select(position => position.CardRef)
            .ToList();

    private static string KnownPositionCardRef(ProjectedSnapshot projected)
    {
        var refs = KnownPositionRefs(projected);
        Check.Equal(1, refs.Count, "当前快照必须恰有一条已知位置");
        return refs[0];
    }

    /// <summary>受控场景：把**指定实例**从一个牌区搬到另一个牌区（不按定义挑第一张）。</summary>
    private static void MoveInstance(Session session, InstanceId card, int from, int to)
    {
        if (!session.State.IsMember(from, card))
            throw new CheckFailedException($"受控场景：实例 {card} 不在源牌区里");
        session.State.RunAtomic(() =>
        {
            session.State.RemoveFromCollection(from, card);
            session.State.AddToCollection(to, card);
        });
    }

    /// <summary>受控场景：某个牌区里第一张指定定义的牌。</summary>
    private static InstanceId CardIn(Session session, int collection, string definitionId)
    {
        var definition = session.RuleSet.TypeByQualifiedId(CardTypeId).FieldIndex("DefinitionId");
        var card = session.State.CollectionMembers(collection).FirstOrDefault(candidate =>
            session.State.GetField(candidate, definition).AsString() == definitionId);
        if (card == default)
            throw new CheckFailedException($"牌区里没有 '{definitionId}'");
        return card;
    }


    private static string KnownFactRef(ProjectedSnapshot projected)
    {
        var facts = projected.Snapshot!.Entities
            .Where(entity => entity.KindId == "net:kind:base-spire:KnownCard")
            .Select(entity => entity.InstanceId)
            .ToList();
        Check.Equal(1, facts.Count, "当前快照必须恰有一个 KnownCard 事实实体");
        return facts[0];
    }

    /// <summary>两次投影之间都可见的实例必须保持同一公开句柄（不换绑）。</summary>
    private static void CheckHandlesStable(IReadOnlyDictionary<InstanceId, string> before,
        IReadOnlyDictionary<InstanceId, string> after, string what)
    {
        foreach (var (instance, handle) in before)
        {
            if (after.TryGetValue(instance, out var current))
            {
                Check.Equal(handle, current, $"{what}：仍可见的实例 {instance} 的公开句柄不换绑");
            }
        }
    }

    /// <summary>受控场景：按出生生命定位敌人（同定义敌人对里唯一的区分字段）。</summary>
    private static IReadOnlyDictionary<long, InstanceId> MonstersByHp(Session session)
    {
        var monsters = session.Observe(SliceManifest.Player, PlayerViewId).Instances
            .Where(instance => instance.TypeId == MonsterTypeId)
            .ToList();
        var result = new Dictionary<long, InstanceId>();
        foreach (var monster in monsters)
        {
            var hp = monster.Fields.Single(field => field.Name == "Hp").Value.AsInt();
            if (!result.TryAdd(hp, monster.Id))
                throw new CheckFailedException($"两个敌人的出生生命相同（{hp}），无法用 Hp 区分");
        }

        return result;
    }

    private static long HpOf(Session session, InstanceId monster) =>
        session.State.GetField(monster, session.RuleSet.TypeByQualifiedId(MonsterTypeId).FieldIndex("Hp"))
            .AsInt();

    /// <summary>受控场景：打出指定定义的手牌，并把随后的决策都答给 <paramref name="target"/>。</summary>
    private static void PlayHandCardAt(Session session, string definitionId, InstanceId target)
    {
        var hand = session.RuleSet.Collections.Single(collection => collection.QualifiedId == HandCollectionId).Id;
        var card = CardIn(session, hand, definitionId);

        var play = session.LegalActions(SliceManifest.Player)
            .Single(action => action.Definition.QualifiedId == PlayCardId);
        session.Submit(play.Definition, SliceManifest.Player, [Value.OfRef(card)]);
        while (session.Status == SessionStatus.AwaitingDecision)
        {
            var pending = session.PendingDecision!;
            var chosen = pending.Candidates.Contains(target) ? target : pending.Candidates[0];
            session.Answer(new DecisionAnswer(pending.RequestId, [chosen]));
        }
    }

    private static ProjectedSnapshot Project(Session session, PublicSnapshotProjector projector) =>
        projector.Project(new SnapshotInput(
            session.Observe(SliceManifest.Player, PlayerViewId),
            session.LegalActions(SliceManifest.Player),
            session.PendingDecision,
            session.Status,
            session.Terminal));

    /// <summary>候选的公开语义键＝规范序列化的 <c>{action_type, fields}</c>（contract-artifacts §3）。</summary>
    private static string SemanticKey(LegalActionDto action) =>
        WireCanonical.Text(WireValue.Object(
            ("action_type", WireValue.Str(action.ActionType)),
            ("fields", WireValue.Array(action.Fields.Select(field => field.ToWire())))));

    // -- check bodies ------------------------------------------------------------------------------------------

    private static void SessionStatusMapping()
    {
        Check.Equal(("active", true), PublicSnapshotProjector.MapSessionStatus(SessionStatus.Running),
            "Running → active 且发布快照");
        Check.Equal(("active", true), PublicSnapshotProjector.MapSessionStatus(SessionStatus.AwaitingDecision),
            "AwaitingDecision → active（协议没有 awaiting_decision，见 protocol.md:193）");
        Check.Equal(("terminal", true), PublicSnapshotProjector.MapSessionStatus(SessionStatus.Terminal),
            "Terminal → terminal 且发布终止快照");
        Check.Equal(("faulted", false), PublicSnapshotProjector.MapSessionStatus(SessionStatus.Faulted),
            "Faulted → faulted 且不发布快照");
    }

    private static void ActiveProjectsDecisionSnapshot()
    {
        var session = NewBattle();
        var projected = Project(session, Projector(session));
        Check.Equal("active", projected.SessionStatus, "协议 session_status");
        Check.Equal(session.StateVersion, projected.ObservationId, "observation_id 必须等于稳定状态版本");
        var snapshot = projected.Snapshot;
        Check.True(snapshot is not null, "活跃会话必须发布快照");
        if (snapshot is null) return;
        Check.Equal("decision", snapshot.Status, "非终局快照 status=decision");
        Check.Equal(SessionId, snapshot.SessionId, "session_id 回填");
        Check.Equal(SlicePublicProfile.Create().RulesetId, snapshot.RulesetId, "ruleset_id 来自 profile");
        Check.Equal(SlicePublicProfile.Create().CatalogId, snapshot.CatalogId, "catalog_id 来自 profile");
        Check.True(snapshot.Entities.Count > 1, "至少包含玩家与敌人");
        Check.True(snapshot.LegalActions.Count > 0, "decision 快照必须有候选");
        Check.True(snapshot.TerminalResult is null, "decision 快照不得带终局结果");
    }

    private static void CandidatesSortedBySemanticKey()
    {
        var session = NewBattle();
        var snapshot = Project(session, Projector(session)).Snapshot!;
        var keys = snapshot.LegalActions.Select(SemanticKey).ToList();
        Check.True(keys.Count > 0, "需要至少一个候选才能验证排序");
        for (var index = 1; index < keys.Count; index++)
        {
            Check.True(WireCanonical.CompareByUtf8Bytes(keys[index - 1], keys[index]) < 0,
                $"候选必须严格递增（相同键应被拒绝）：{keys[index - 1]} !< {keys[index]}");
        }
    }

    private static void CandidateIdNotInFeatures()
    {
        var session = NewBattle();
        var projected = Project(session, Projector(session));
        var snapshot = projected.Snapshot!;
        Check.Equal(snapshot.LegalActions.Count, projected.Bindings.Count, "候选与绑定一一对应");
        var handles = snapshot.LegalActions.Select(action => action.CandidateId).ToHashSet(StringComparer.Ordinal);
        foreach (var action in snapshot.LegalActions)
        {
            Check.True(projected.Bindings.Any(binding => binding.CandidateId == action.CandidateId),
                $"绑定必须包含候选 {action.CandidateId}");
            foreach (var field in action.Fields)
            {
                Check.True(field.FieldId != "candidate_id", "候选特征里不得出现 candidate_id 字段");
                if (field.Value is WireString text)
                {
                    Check.False(handles.Contains(text.Value),
                        $"字段值不得是候选句柄（{field.FieldId}={text.Value}）");
                }
            }
        }
    }

    private static void PausedMapsToActiveDecision()
    {
        var session = NewBattle();
        var playCard = session.LegalActions(SliceManifest.Player)
            .Single(action => action.Definition.QualifiedId == PlayCardId);
        session.Submit(playCard.Definition, SliceManifest.Player, [Value.OfRef(playCard.Candidates[0])]);
        Check.Equal(SessionStatus.AwaitingDecision, session.Status, "出牌后暂停等待目标选择");

        var projected = Project(session, Projector(session));
        Check.Equal("active", projected.SessionStatus, "AwaitingDecision 必须映射为协议 active");
        var snapshot = projected.Snapshot!;
        Check.Equal("decision", snapshot.Status, "暂停期快照 status=decision");

        var expected = session.PendingDecision!.Candidates
            .Select(instance => Refs(session)[instance])
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();
        var referenced = snapshot.LegalActions
            .SelectMany(action => action.Fields)
            .Where(field => field.Value is WireString)
            .Select(field => ((WireString)field.Value!).Value)
            .Where(value => expected.Contains(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();
        Check.Sequence(expected, referenced, "暂停期候选恰为 PendingDecision.Candidates（按公开实例 ID）");
    }

    private static void ObservationIdIsStableAcrossReads()
    {
        var session = NewBattle();
        var projector = Projector(session);
        var first = Project(session, projector);
        var version = session.StateVersion;
        var second = Project(session, projector);
        Check.Equal(first.ObservationId, second.ObservationId, "连续观察必须给出同一 observation_id");
        Check.Equal(version, session.StateVersion, "观察不得改变状态版本（observe 纯读）");
    }

    private static void NoSequencesOrGroupsAndClosedReferences()
    {
        var session = NewBattle();
        var snapshot = Project(session, Projector(session)).Snapshot!;
        Check.Equal(0, snapshot.Sequences.Count, "切片未声明有序流程 → sequences 必须是空数组");
        Check.Equal(0, snapshot.Groups.Count, "切片未声明分组 → groups 必须是空数组");

        var entityIds = snapshot.Entities.Select(entity => entity.InstanceId).ToHashSet(StringComparer.Ordinal);
        foreach (var pile in snapshot.Piles)
        {
            foreach (var member in pile.VisibleMembers)
            {
                Check.True(entityIds.Contains(member),
                    $"visible_members 必须引用快照内的实体：{pile.PileRef} → {member}");
            }
            Check.True(pile.VisibleMembers.Distinct(StringComparer.Ordinal).Count() == pile.VisibleMembers.Count,
                $"visible_members 不得重复：{pile.PileRef}");
        }
    }

    private static void HiddenDrawPileLeaksNoMembers()
    {
        var session = NewBattle();
        var view = session.Observe(SliceManifest.Player, PlayerViewId);
        var projected = Project(session, Projector(session));
        var snapshot = projected.Snapshot!;

        var hidden = view.Collections.Where(collection => !collection.IdentityVisible).ToList();
        Check.True(hidden.Count > 0, "切片应至少有一个身份不可见的集合（抽牌堆）");

        // V-02：隐藏集合的成员不得出现在任何公开位置。
        var visibleMembers = snapshot.Piles.SelectMany(pile => pile.VisibleMembers).ToHashSet(StringComparer.Ordinal);
        foreach (var collection in hidden)
        {
            foreach (var member in collection.Members)
            {
                var publicId = Refs(session)[member];
                Check.False(visibleMembers.Contains(publicId),
                    $"隐藏集合 {collection.QualifiedId} 的成员 {publicId} 泄漏到 visible_members");
            }

            // 未发布的集合必须逐条登记（规则号 + 理由），不得静默丢弃；
            // （引擎无牌堆实体，故本切片的牌堆走"显式跳过"这条路径 —— 见 TODO-N02-PILE-ENTITY。）
            var skipped = projected.Skipped.Any(fact => fact.EngineQualifiedId == collection.QualifiedId);
            var publishedWithoutMembers = snapshot.Piles.Any(pile =>
                pile.TotalCount > 0 && pile.VisibleMembers.Count == 0 && !pile.MembersComplete);
            Check.True(skipped || publishedWithoutMembers,
                $"隐藏集合 {collection.QualifiedId} 必须被显式跳过（带规则号与理由）或发布为成员不可见的牌堆");
        }

        foreach (var fact in projected.Skipped)
        {
            Check.True(!string.IsNullOrWhiteSpace(fact.RuleId), $"跳过项必须带规则号：{fact.EngineQualifiedId}");
            Check.True(!string.IsNullOrWhiteSpace(fact.Reason), $"跳过项必须带理由：{fact.EngineQualifiedId}");
        }
    }

    private static void BoundaryRejections()
    {
        var session = NewBattle();
        var projector = Projector(session);
        var view = session.Observe(SliceManifest.Player, PlayerViewId);
        var actions = session.LegalActions(SliceManifest.Player);

        Check.MappingFault(
            () => projector.Project(new SnapshotInput(view, actions, null, SessionStatus.Terminal, null)),
            "terminal 会话必须带终局结果",
            "N-02-SESSION-STATUS");

        Check.MappingFault(
            () => projector.Project(new SnapshotInput(view, actions, null, SessionStatus.AwaitingDecision, null)),
            "AwaitingDecision 必须带当前 DecisionRequest",
            "N-02-DECISION-STATE");

        Check.MappingFault(
            () => projector.Project(new SnapshotInput(view, [], null, SessionStatus.Running, null)),
            "decision 快照不得发布空候选",
            "TODO-N02-EMPTY-CANDIDATES");
    }

    private static void UndecidedConflictIsRecorded()
    {
        // H-01/H-02：两处具名缺口已收口，旧 TODO 不得再以"已记录"充当通过。
        Check.False(typeof(SlicePublicProfile).GetField("PileEntityTodo") is not null,
            "TODO-N02-PILE-ENTITY 必须已被真正收口，而不是继续登记");
        Check.False(typeof(SlicePublicProfile).GetField("KnownPositionTodo") is not null,
            "TODO-N02-KNOWN-POSITION 必须已被真正收口，而不是继续登记");
    }

    /// <summary>H-01/H-02：隐藏牌堆的成员按授权揭示，known_positions 满足 I-27/I-29 与引用闭合。</summary>
    private static void KnownPositionReachesTheWire()
    {
        var session = NewBattle(includeHeadbutt: true);
        var rules = session.RuleSet;
        var draw = Collection(rules, "base-spire:DrawPile").Id;
        var hand = Collection(rules, "base-spire:Hand").Id;
        var discard = Collection(rules, "base-spire:DiscardPile").Id;
        var headbutt = Relocate(session, SliceManifest.HeadbuttId, draw, hand);
        var chosen = Relocate(session, SliceManifest.StrikeId, hand, discard);
        var front = session.State.CollectionMembers(Collection(rules, "base-spire:Monsters").Id)[0];

        session.Submit(session.LegalActions(SliceManifest.Player)
            .Single(action => action.Definition.QualifiedId == PlayCardId).Definition, SliceManifest.Player,
            [Value.OfRef(headbutt)]);
        session.Answer(new DecisionAnswer(session.PendingDecision!.RequestId, [front]));
        session.Answer(new DecisionAnswer(session.PendingDecision!.RequestId, [chosen]));

        var snapshot = Project(session, Projector(session)).Snapshot!;
        var chosenRef = Refs(session)[chosen];
        var pile = snapshot.Piles.Single(p => p.KnownPositions.Count > 0);
        var position = pile.KnownPositions.Single();

        Check.Equal(chosenRef, position.CardRef, "known_positions 指向真实卡牌实例");
        Check.Equal(0L, position.Position, "置顶后的绝对位置为 0");
        Check.True(pile.VisibleMembers.Contains(chosenRef, StringComparer.Ordinal),
            "I-29：card_ref 必须是 visible_members 的子集");
        Check.False(pile.MembersComplete, "隐藏牌堆的成员不完整");
        Check.True(position.Position >= 0 && position.Position < pile.TotalCount, "I-27：位置在真实总数内");
        Check.True(snapshot.Entities.Any(e => e.InstanceId == chosenRef), "被引用的卡牌是快照内的实体");
        Check.True(pile.VisibleMembers.Count < pile.TotalCount, "授权揭示不得等同公开整个牌堆");
    }

    /// <summary>H-02：目录声明 pile_types，kind_id 唯一，且每个 pile_ref 都能解析到同种类的实体。</summary>
    private static void CatalogDeclaresPilesAndKinds()
    {
        var session = NewBattle();
        var profile = SlicePublicProfile.Create();
        var catalog = CatalogProjector.Project(profile);
        var declarations = catalog.Declarations;

        Check.Equal(5, declarations.PileTypes.Count, "五个牌区各有一个 pile_type");
        Check.Equal(declarations.PileTypes.Count,
            declarations.PileTypes.Select(pile => pile.KindId).Distinct(StringComparer.Ordinal).Count(),
            "pile_type 的 kind_id 必须唯一（protocol.md:83）");
        Check.Equal(declarations.PileTypes.Count,
            declarations.PileTypes.Select(pile => pile.Id).Distinct(StringComparer.Ordinal).Count(),
            "pile_type 的 id 必须唯一");
        foreach (var pileType in declarations.PileTypes)
        {
            Check.True(pileType.MemberKindIds.Contains(SlicePublicProfile.CardKindId, StringComparer.Ordinal),
                $"pile_type {pileType.Id} 的成员种类必须是公开的卡牌种类");
        }

        var snapshot = Project(session, Projector(session)).Snapshot!;
        var kindsByRef = snapshot.Entities.ToDictionary(entity => entity.InstanceId, entity => entity.KindId,
            StringComparer.Ordinal);
        var declaredKinds = declarations.PileTypes.Select(pile => pile.KindId).ToHashSet(StringComparer.Ordinal);
        Check.Equal(5, snapshot.Piles.Count, "五个牌区都被发布为 pile");
        foreach (var pile in snapshot.Piles)
        {
            Check.True(kindsByRef.TryGetValue(pile.PileRef, out var kind), $"pile_ref {pile.PileRef} 必须引用实体");
            Check.True(declaredKinds.Contains(kind!), $"pile_ref {pile.PileRef} 的 kind_id 必须唯一匹配已声明 pile_type");
        }
    }

    /// <summary>H-02：entity_ref 目标种类与动作/终局字段必需性来自显式声明，不是映射层的猜测。</summary>
    private static void TargetKindsAndRequirednessAreDeclared()
    {
        var catalog = CatalogProjector.Project(SlicePublicProfile.Create());
        var declarations = catalog.Declarations;

        var playCard = declarations.ActionTypes.Single(action => action.Id == "net:action:base-spire:PlayCard");
        var cardField = declarations.Fields.Single(field =>
            field.Id == "net:field:base-spire:PlayCard.Card");
        Check.Equal("entity_ref", cardField.ValueType, "出牌参数是实体引用");
        Check.Sequence([SlicePublicProfile.CardKindId], cardField.TargetKindIds ?? [], "目标种类来自显式声明");
        Check.Sequence(["net:field:base-spire:PlayCard.Card"], playCard.RequiredFields,
            "执行所需字段必须进 required_fields（protocol.md:98）");

        // 决策域不是单一类型：应答参数必须声明本切片实际的候选种类集合（卡牌 + 敌人），
        // 只声明一种会让真实候选被判成类型错。
        var answer = declarations.ActionTypes.Single(action => action.Id == "net:action:net.answer_decision");
        var answerField = declarations.Fields.Single(field => field.Id == "net:field:net.answer_decision.Instance");
        Check.Sequence([SlicePublicProfile.CardKindId, SlicePublicProfile.MonsterKindId],
            answerField.TargetKindIds ?? [], "应答参数的目标种类必须覆盖真实决策域");
        Check.Sequence(["net:field:net.answer_decision.Instance"], answer.RequiredFields,
            "应答参数是执行所需字段");

        // category_ids 必须是 wire 上出现的**公开类别 ID**；声明引擎原值会让消费者无法解析真实值。
        var kindField = declarations.Fields.Single(field => field.Id == "net:field:base-spire:Card.Kind");
        Check.Equal("category", kindField.ValueType, "卡牌种类是 category");
        Check.Sequence(["net:cat:Attack", "net:cat:Power", "net:cat:Skill", "net:cat:Status"], kindField.CategoryIds ?? [],
            "category_ids 必须与 wire 值同源（公开类别 ID）");

        foreach (var terminal in declarations.TerminalReasons)
        {
            Check.True(terminal.RequiredFields.Count > 0,
                $"终局原因 {terminal.Id} 的事实字段必需性必须来自声明而不是一律可选");
        }
    }

    /// <summary>受控场景：把某个定义的第一张牌从一个牌区搬到另一个牌区（测试场景，不改规则）。</summary>
    private static InstanceId Relocate(Session session, string cardId, int from, int to)
    {
        var card = session.State.CollectionMembers(from).FirstOrDefault(candidate =>
            session.State.GetField(candidate,
                session.RuleSet.TypeByQualifiedId("base-spire:Card").FieldIndex("DefinitionId")).AsString() == cardId);
        if (card == default)
            throw new CheckFailedException($"no '{cardId}' card in the source area");
        session.State.RunAtomic(() =>
        {
            session.State.RemoveFromCollection(from, card);
            session.State.AddToCollection(to, card);
        });
        return card;
    }

    private static CollectionDefinition Collection(RuleSet rules, string qualifiedId) =>
        rules.Collections.Single(collection => collection.QualifiedId == qualifiedId);
}
