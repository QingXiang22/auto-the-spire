using BaseSpire.SinglePlayer;
using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// ENV-T03 的 A 线依赖子包检查：正式工厂给出的**不可变公开语义元数据**是否与真实规则集、所选投影和
/// **实际加载内容**闭合，是否只读稳定、不含每局 seed，且内容变化会反映到构建身份。
///
/// 与已有检查的关系：这里复用正式工厂、真实开局与内容加载（不另造一套"开局"），但**不**复制
/// `BasePublicSemanticsBuilder` 的构造逻辑来当期望值——闭合部分是拿 `RuleSet` 的声明重新比对的。
/// </summary>
internal static class PublicSemanticsChecks
{
    private const string ProjectionId = BaseSpireRuntimeFactory.ProjectionId;
    private const string MilestoneSeed = "ENV-IRONCLAD-00";
    private const string CardTypeId = "base-spire:Card";
    private const string PowerTypeId = "base-spire:Power";
    private const string MonsterTypeId = "base-spire:Monster";
    private const string RelicTypeId = "base-spire:Relic";
    private const string KnownCardTypeId = "base-spire:KnownCard";

    private static BaseSpireRuntimeFactory Factory() => new(RunConfiguration.FrozenIronclad());

    public static void Run()
    {
        Check.Run("ENV-T03 公开语义元数据与实际规则集闭合：每条集合有用途、每个终局有解释 [真实]", () =>
        {
            var factory = Factory();
            var rules = factory.Rules;
            var semantics = factory.PublicSemantics;

            Check.Equal(ProjectionId, semantics.ProjectionId, "元数据绑定的是正式工厂的公开投影");
            Check.Equal(SliceManifest.PlayerRole, semantics.ParticipantRole, "参与者角色与投影声明一致");

            // 集合闭合：规则集里的**每一条**集合都必须有用途声明（多一条、少一条都失败）。
            Check.Sequence(
                rules.Collections.Select(item => item.QualifiedId).Order(StringComparer.Ordinal),
                semantics.Collections.Keys.Order(StringComparer.Ordinal),
                "集合用途必须覆盖规则集里的每一条集合（不得静默丢弃公开事实）");

            // 终局闭合：每个终局结果都必须有胜负解释（不允许按名字猜）。
            Check.Sequence(
                rules.TerminalResults.Select(item => item.QualifiedId).Order(StringComparer.Ordinal),
                semantics.TerminalOutcomes.Keys.Order(StringComparer.Ordinal),
                "终局胜负解释必须覆盖规则集里的每一个终局结果");

            // 每条"不发布"都必须带规则号与理由。
            foreach (var (collectionId, entry) in semantics.Collections)
            {
                if (entry.Use != BasePublicCollectionUse.NotPublished) continue;
                Check.True(!string.IsNullOrWhiteSpace(entry.SkipRuleId),
                    $"集合 '{collectionId}' 的具名不发布必须带规则号");
                Check.True(!string.IsNullOrWhiteSpace(entry.SkipReason),
                    $"集合 '{collectionId}' 的具名不发布必须带理由");
            }

            // 内容目录：字段必须真的存在于该类型且是 string，且类型在所选投影里。
            var included = rules.Projections.Single(item => item.QualifiedId == ProjectionId).Types.Values
                .Where(item => item.Include)
                .Select(item => item.Type.QualifiedId)
                .ToHashSet(StringComparer.Ordinal);
            Check.True(semantics.Content.Count > 0, "构建必须给出内容 ID 目录");
            foreach (var (typeId, catalog) in semantics.Content)
            {
                var type = rules.TypeByQualifiedId(typeId);
                Check.True(type.TryFieldIndex(catalog.FieldName, out var index)
                    && type.Fields[index].Kind == FieldTypeKind.String,
                    $"内容目录 '{typeId}.{catalog.FieldName}' 必须是真实的 string 字段");
                Check.True(included.Contains(typeId),
                    $"内容目录指向的类型 '{typeId}' 必须在所选公开投影里");
                Check.True(catalog.ContentIds.Count > 0, $"内容目录 '{typeId}' 不能是空目录");
            }

            // 投影/角色不匹配或指到不存在的投影必须显式失败（闭合是构造期强制的，不是注释约定）。
            var source = SourceOf();
            Check.True(Record.Exception(() =>
                    BasePublicSemanticsBuilder.Build(rules, "base-spire:NotAProjection", SliceManifest.PlayerRole,
                        source)) is InvalidOperationException,
                "指到不存在的投影必须显式失败");
            Check.True(Record.Exception(() =>
                    BasePublicSemanticsBuilder.Build(rules, ProjectionId, "not-the-player", source))
                is InvalidOperationException,
                "参与者角色与投影声明不一致必须显式失败");

            Console.WriteLine($"      ENV-T03 元数据：semantics={semantics.Digest} "
                + $"build={factory.Describe().PublicBuildId} ruleset={factory.Describe().RulesetId}");
            Console.WriteLine($"      ENV-T03 元数据：{semantics.CanonicalText().Replace("\n", " | ")}");
        });

        Check.Run("ENV-T03 内容 ID 目录 = 实际加载内容（未支持的源声明不进目录）[真实]", () =>
        {
            var factory = Factory();
            var semantics = factory.PublicSemantics;
            var build = factory.Describe();
            var content = SinglePlayerContentSource.Shared;

            Check.Sequence(content.CardIds, semantics.Content[CardTypeId].ContentIds,
                "卡牌目录 = 实际加载的卡（按 id 稳定排序）");
            Check.Sequence(content.PowerIds, semantics.Content[PowerTypeId].ContentIds,
                "能力目录 = 实际加载的能力");
            Check.Sequence(content.EnemyIds, semantics.Content[MonsterTypeId].ContentIds,
                "敌人目录 = 实际加载的敌人");
            Check.Sequence(SpireRelics.All.Select(relic => relic.ContentId).Order(StringComparer.Ordinal),
                semantics.Content[RelicTypeId].ContentIds,
                "遗物目录 = 包里声明的遗物库（DefinitionId 的闭合取值域）");
            Check.Equal(build.CardCount, semantics.Content[CardTypeId].ContentIds.Count,
                "目录条数与构建描述里的实际加载卡数一致");
            Check.Equal(build.PowerCount, semantics.Content[PowerTypeId].ContentIds.Count,
                "目录条数与构建描述里的实际加载能力数一致");
            Check.Equal(build.EnemyCount, semantics.Content[MonsterTypeId].ContentIds.Count,
                "目录条数与构建描述里的实际加载敌人数一致");

            // 判别力：被逐条记为**未支持**的源声明不得出现在目录里——目录是"实际加载的内容"，
            // 不是"源文本声明清单"（源里声明过但没降级下来的卡必须留在未支持列表里）。
            var declaredButUnsupported = build.UnsupportedContent
                .Where(entry => entry.Item.StartsWith("card:", StringComparison.Ordinal))
                .Select(entry => $"{SliceManifest.PackageId}:{SpireCardSource.ToId(entry.Item["card:".Length..])}")
                .ToList();
            Check.True(declaredButUnsupported.Count > 0,
                "本构建必须存在被记为未支持的卡，否则这条判别没有对照");
            Check.True(declaredButUnsupported.Any(id => !semantics.Content[CardTypeId].ContentIds.Contains(id,
                    StringComparer.Ordinal)),
                $"源里声明过但未支持的卡不得进入内容目录（对照：{string.Join(", ", declaredButUnsupported)}）");
            Check.DoesNotContain(semantics.Content[CardTypeId].ContentIds,
                id => !content.Model.Cards.Any(card => card.Id == id),
                "目录里的每个 id 都必须对应一个实际组装进包的内容定义");
            Console.WriteLine($"      ENV-T03 目录：卡={semantics.Content[CardTypeId].ContentIds.Count} "
                + $"能力={semantics.Content[PowerTypeId].ContentIds.Count} "
                + $"敌人={semantics.Content[MonsterTypeId].ContentIds.Count} "
                + $"遗物={semantics.Content[RelicTypeId].ContentIds.Count} "
                + $"（源里声明但未支持、不进目录的卡：{string.Join(", ", declaredButUnsupported)}）");
        });

        Check.Run("ENV-T03 集合用途按实际可见性解释：牌堆/有序流程/受阻不发布各有依据 [真实]", () =>
        {
            var factory = Factory();
            var rules = factory.Rules;
            var semantics = factory.PublicSemantics;

            // 五个牌区发布为 pile；牌堆种类必须是包里真实声明的实体种类（每个牌区一个）。
            var expectedPiles = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["base-spire:Hand"] = "base-spire:HandPile",
                ["base-spire:DrawPile"] = "base-spire:DrawPileEntity",
                ["base-spire:DiscardPile"] = "base-spire:DiscardPileEntity",
                ["base-spire:ExhaustPile"] = "base-spire:ExhaustPileEntity",
                ["base-spire:InPlay"] = "base-spire:InPlayPileEntity",
            };
            foreach (var (collectionId, pileKind) in expectedPiles)
            {
                var entry = semantics.Collections[collectionId];
                Check.Equal(BasePublicCollectionUse.Pile, entry.Use, $"'{collectionId}' 必须发布为牌堆");
                Check.Equal(pileKind, entry.PileKindEngineTypeId, $"'{collectionId}' 的 pile_ref 种类");
                Check.True(rules.Types.Any(type => type.QualifiedId == pileKind),
                    $"'{pileKind}' 必须是包里真实声明的实体种类");
            }

            // 位置只对抽牌堆/弃牌堆声明（由事实公开的绝对位置从堆顶 0 起算）。
            Check.True(semantics.Collections["base-spire:DrawPile"].Position is not null, "抽牌堆有公开位置坐标");
            Check.True(semantics.Collections["base-spire:DiscardPile"].Position is not null, "弃牌堆有公开位置坐标");
            foreach (var notPositional in new[] { "base-spire:Hand", "base-spire:ExhaustPile", "base-spire:InPlay" })
            {
                Check.True(semantics.Collections[notPositional].Position is null,
                    $"'{notPositional}' 没有公开位置语义，不得自造范围");
            }

            // 抽牌堆的成员与顺序在**引擎声明**里就是隐藏的，但"大小公开"这个事实不能因此被省略：
            // 它仍然发布为牌堆（大小由引擎声明读取），绝对位置只通过 KnownCard 事实公开。
            var drawDeclaration = rules.Collections.Single(item => item.QualifiedId == "base-spire:DrawPile");
            Check.True(drawDeclaration.SizeVisible, "抽牌堆的大小是公开事实");
            Check.False(drawDeclaration.IdentityVisible, "抽牌堆的成员身份在引擎声明里就是隐藏的");
            Check.False(drawDeclaration.OrderVisible, "抽牌堆的顺序在引擎声明里就是隐藏的");
            Check.Equal(BasePublicCollectionUse.Pile, semantics.Collections["base-spire:DrawPile"].Use,
                "成员隐藏不构成\"省略整个牌堆事实\"的理由：它仍发布为牌堆（大小公开）");

            // 有序流程：成员种类在投影里、引擎声明 identity/order 公开 ⇒ 必须发布为 sequence
            // （不得具名不发布，也不得落成无序 group）。owner 是领域实体的**种类**，不是写死的每局句柄。
            var expectedSequences = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["base-spire:PlayedThisTurn"] = "base-spire:Player",
                ["base-spire:GeneratedCardOffers"] = "base-spire:Player",
                ["base-spire:Monsters"] = "base-spire:Player",
                ["base-spire:Known"] = "base-spire:Player",
                ["base-spire:RunStates"] = "base-spire:Run",
                ["base-spire:LongTermDeck"] = "base-spire:Run",
                ["base-spire:Relics"] = "base-spire:Run",
            };
            foreach (var (collectionId, ownerKind) in expectedSequences)
            {
                var entry = semantics.Collections[collectionId];
                Check.Equal(BasePublicCollectionUse.Sequence, entry.Use,
                    $"'{collectionId}' 必须发布为有序流程");
                Check.Equal(ownerKind, entry.OwnerKindEngineTypeId, $"'{collectionId}' 的 owner 种类");
            }

            // 受阻项：成员种类不在投影里，没有任何已发布载荷能承载成员 ⇒ 只能具名不发布，
            // 但规则号必须标明"受阻"，不得写成已完成。
            foreach (var blocked in new[]
                     {
                         "base-spire:EncounterPool", "base-spire:EncounterQueue",
                         "base-spire:EliteEncounterQueue",
                     })
            {
                var entry = semantics.Collections[blocked];
                Check.Equal(BasePublicCollectionUse.NotPublished, entry.Use, $"'{blocked}' 的用途");
                Check.True(entry.SkipReason!.Length >= 20, $"'{blocked}' 的不发布理由必须说明实际用途");
                Check.True(entry.SkipRuleId!.Contains("BLOCKED", StringComparison.Ordinal),
                    $"'{blocked}' 是受阻项（成员种类不在投影里），规则号必须标明受阻");
            }
        });

        Check.Run("ENV-T03 判别：公开顺序集合不落 NotPublished/丢序 Group，owner 进入摘要 [真实]", () =>
        {
            var factory = Factory();
            var rules = factory.Rules;
            var semantics = factory.PublicSemantics;
            var included = rules.Projections.Single(item => item.QualifiedId == ProjectionId).Types.Values
                .Where(item => item.Include)
                .Select(item => item.Type.QualifiedId)
                .ToHashSet(StringComparer.Ordinal);
            var canonical = semantics.CanonicalText();

            // 期望值来自**引擎声明**（不是元数据自己的意图表），所以这是独立比对而不是同义反复。
            foreach (var collection in rules.Collections)
            {
                var id = collection.QualifiedId;
                var entry = semantics.Collections[id];
                var elementType = rules.Type(collection.ElementTypeId).QualifiedId;
                var memberKindPublished = included.Contains(elementType);

                if (entry.Use == BasePublicCollectionUse.NotPublished)
                {
                    Check.False(memberKindPublished,
                        $"'{id}' 的成员种类 '{elementType}' 在投影里，投影确实发布成员事实：不得以具名理由不发布");
                    continue;
                }

                Check.True(memberKindPublished, $"'{id}' 发布为 {entry.Use} 时成员种类必须在投影里");

                if (collection.IdentityVisible && collection.OrderVisible)
                {
                    Check.True(entry.Use == BasePublicCollectionUse.Pile
                            || entry.Use == BasePublicCollectionUse.Sequence,
                        $"'{id}' 的成员身份与顺序在引擎声明里都是公开的：不得落成无序 group（会丢真实顺序）");
                }

                if (entry.Use == BasePublicCollectionUse.Group || entry.Use == BasePublicCollectionUse.Sequence)
                {
                    Check.True(entry.OwnerKindEngineTypeId is not null, $"'{id}' 必须有 owner 种类");
                    Check.True(included.Contains(entry.OwnerKindEngineTypeId!),
                        $"'{id}' 的 owner 种类必须在所选投影里");
                    var token = entry.Use == BasePublicCollectionUse.Sequence ? "sequence" : "group";
                    Check.True(canonical.Contains(
                            $"collection={id}|{token}|owner={entry.OwnerKindEngineTypeId}",
                            StringComparison.Ordinal),
                        $"'{id}' 的用途与 owner 必须进入规范化文本（从而进入摘要与公开身份）");
                }

                if (entry.Use == BasePublicCollectionUse.Sequence)
                {
                    Check.True(collection.IdentityVisible && collection.OrderVisible,
                        $"'{id}' 声明为 sequence，但引擎声明隐藏了成员/顺序：不得借有序载荷泄漏");
                }
            }

            var sequences = semantics.Collections.Count(pair => pair.Value.Use == BasePublicCollectionUse.Sequence);
            var notPublished = semantics.Collections.Count(pair => pair.Value.Use == BasePublicCollectionUse.NotPublished);
            var offers = rules.Collections.Single(item => item.QualifiedId == "base-spire:GeneratedCardOffers");
            Check.True(offers.IdentityVisible && offers.OrderVisible && offers.SizeVisible,
                "生成候选的成员、顺序与数量公开，玩家据此作出选择");
            Check.Equal(BasePublicCollectionUse.Sequence, semantics.Collections["base-spire:OrbQueue"].Use, "orb order published");
            Check.Equal("base-spire:Player", semantics.Collections["base-spire:OrbQueue"].OwnerKindEngineTypeId, "orb queue belongs to combat player");
            foreach (var id in new[] { "base-spire:PotionBelt", "base-spire:RewardPotions" })
            {
                Check.Equal(BasePublicCollectionUse.Sequence, semantics.Collections[id].Use,
                    $"'{id}' 保持成员实例与顺序");
                Check.Equal("base-spire:Run", semantics.Collections[id].OwnerKindEngineTypeId,
                    $"'{id}' 属于跨房间的Run");
            }
            var generationPool = rules.Collections.Single(item => item.QualifiedId == "base-spire:GenerationPool");
            Check.False(generationPool.IdentityVisible || generationPool.OrderVisible || generationPool.SizeVisible,
                "完整生成池的临时洗牌状态不得公开");
            Check.Equal(BasePublicCollectionUse.NotPublished, semantics.Collections[generationPool.QualifiedId].Use,
                "完整生成池不进入公开集合");
            Check.Equal("BASE-INTERNAL-GENERATION-POOL", semantics.Collections[generationPool.QualifiedId].SkipRuleId,
                "完整生成池使用明确的不发布规则");
            Check.Equal("BASE-INTERNAL-PLAY-HISTORY", semantics.Collections["base-spire:CardPlayHistory"].SkipRuleId,
                "执行历史必须具名不发布，公开 PlayedThisTurn 仍保持 sequence");
            Console.WriteLine($"      ENV-T03 集合用途：sequence={sequences} notPublished={notPublished} "
                + $"pillar/pile={semantics.Collections.Count(pair => pair.Value.Use == BasePublicCollectionUse.Pile)}");
        });

        Check.Run("ENV-T03 已知位置角色绑定真实字段与公开牌堆 [真实]", () =>
        {
            var factory = Factory();
            var rules = factory.Rules;
            var semantics = factory.PublicSemantics;

            Check.True(semantics.KnownPositions.ContainsKey(KnownCardTypeId),
                "已知位置角色必须声明抽牌堆顶事实（H-01/H-02）");
            var role = semantics.KnownPositions[KnownCardTypeId];
            var factType = rules.TypeByQualifiedId(KnownCardTypeId);
            Check.Equal(KnownCardTypeId, role.FactTypeId, "角色绑定的事实类型");

            // 三个角色字段必须真的存在且形状相符（卡牌引用 / 字符串标记 / 整数位置）。
            CheckField(rules, factType, role.CardFieldName, FieldTypeKind.Ref, CardTypeId);
            CheckField(rules, factType, role.PileFieldName, FieldTypeKind.String, null);
            CheckField(rules, factType, role.PositionFieldName, FieldTypeKind.Int, null);

            // 标记词表：包程序当前只在"抽牌堆顶"写下事实，词表必须正好是它会写的那个标记。
            Check.Sequence([SliceManifest.DrawPileName], role.PileCollectionByToken.Keys,
                "标记词表 = 包程序实际写下的标记（当前只有抽牌堆）");
            var mapped = role.PileCollectionByToken[SliceManifest.DrawPileName];
            Check.Equal("base-spire:DrawPile", mapped, "标记映射到抽牌堆集合");
            Check.Equal(BasePublicCollectionUse.Pile, semantics.Collections[mapped].Use,
                "标记只能指向一个**发布为牌堆**的集合");
            Check.True(semantics.Collections[mapped].Position is not null,
                "标记携带绝对位置，它指向的牌堆必须给出位置坐标（否则位置无法被解释）");

            // 事实类型必须在所选投影里，否则消费者根本读不到角色。
            var included = rules.Projections.Single(item => item.QualifiedId == ProjectionId).Types.Values
                .Where(item => item.Include)
                .Select(item => item.Type.QualifiedId)
                .ToHashSet(StringComparer.Ordinal);
            Check.True(included.Contains(KnownCardTypeId), "已知位置事实类型必须在公开投影里");
        });

        Check.Run("ENV-T03 终局解释是声明的 win/loss，不由名字猜 [真实]", () =>
        {
            var semantics = Factory().PublicSemantics;
            Check.Equal(BasePublicTerminalOutcome.Win, semantics.TerminalOutcomes["base-spire:Victory"],
                "Victory 的胜负解释");
            Check.Equal(BasePublicTerminalOutcome.Loss, semantics.TerminalOutcomes["base-spire:Defeat"],
                "Defeat 的胜负解释");
            // 消费者只承认这三个 wire 标记：边界值必须落在协议词表里。
            Check.Equal("win", semantics.TerminalOutcomes["base-spire:Victory"].WireName(), "Victory 的公开标记");
            Check.Equal("loss", semantics.TerminalOutcomes["base-spire:Defeat"].WireName(), "Defeat 的公开标记");
            Check.Equal("other", ((BasePublicTerminalOutcome)99).WireName(),
                "未定义的终局含义落到 other，不得静默当成 win/loss");
        });

        Check.Run("ENV-T03 真实会话里声明的 pile_ref/owner 种类唯一解析 [真实]", () =>
        {
            var factory = Factory();
            var semantics = factory.PublicSemantics;
            var session = factory.CreateSession(MilestoneSeed);
            session.Start();
            var rules = session.Session.RuleSet;
            var state = session.Session.State;

            // net 的公开契约要求 pile_ref / owner 指向的种类在本快照里**唯一**解析；
            // 这里在真实开局会话上核对，而不是只信声明。
            foreach (var (collectionId, entry) in semantics.Collections)
            {
                if (entry.Use == BasePublicCollectionUse.Pile)
                {
                    var kind = rules.TypeByQualifiedId(entry.PileKindEngineTypeId!);
                    Check.Equal(1, state.AllInstances(kind.Id).Count,
                        $"牌堆 '{collectionId}' 的 pile_ref 种类 '{kind.QualifiedId}' 必须恰好一个实例");
                }
                else if (entry.Use == BasePublicCollectionUse.Group
                         || entry.Use == BasePublicCollectionUse.Sequence)
                {
                    var owner = rules.TypeByQualifiedId(entry.OwnerKindEngineTypeId!);
                    Check.Equal(1, state.AllInstances(owner.Id).Count,
                        $"{entry.Use} '{collectionId}' 的 owner 种类 '{owner.QualifiedId}' 必须恰好一个实例");
                }
            }

            // 有序流程的成员种类必须与引擎声明一致（成员种类决定消费者读到的实例集合）。
            var sequenceElements = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["base-spire:PlayedThisTurn"] = CardTypeId,
                ["base-spire:Monsters"] = MonsterTypeId,
                ["base-spire:Known"] = KnownCardTypeId,
                ["base-spire:RunStates"] = "base-spire:Run",
                ["base-spire:LongTermDeck"] = CardTypeId,
                ["base-spire:Relics"] = RelicTypeId,
            };
            foreach (var (collectionId, elementTypeId) in sequenceElements)
            {
                Check.Equal(BasePublicCollectionUse.Sequence, semantics.Collections[collectionId].Use,
                    $"'{collectionId}' 必须发布为有序流程");
                var declaration = rules.Collections.Single(item => item.QualifiedId == collectionId);
                Check.Equal(elementTypeId, rules.Type(declaration.ElementTypeId).QualifiedId,
                    $"'{collectionId}' 的成员种类");
            }
        });

        Check.Run("ENV-T03 同配置稳定、不含每局 seed，内容变更影响身份 [真实]", () =>
        {
            var first = Factory();
            var second = Factory();
            Check.Equal(first.PublicSemantics.Digest, second.PublicSemantics.Digest,
                "同一配置两次构造的元数据摘要必须相同");
            Check.Equal(first.PublicSemantics.CanonicalText(), second.PublicSemantics.CanonicalText(),
                "同一配置的规范化文本逐字节相同");
            Check.Equal(first.Describe().PublicBuildId, second.Describe().PublicBuildId,
                "同一配置的公开构建身份相同");
            Check.Equal(first.Describe().SemanticsDigest, first.PublicSemantics.Digest,
                "构建描述暴露的语义摘要就是元数据摘要（身份接线）");
            Check.True(first.Describe().Summary.Contains($"semantics={first.PublicSemantics.Digest}",
                    StringComparison.Ordinal),
                "人读摘要必须带上语义摘要，证据才能逐条核对");

            // 每局 seed 不进元数据：建两个不同 seed 的会话后，摘要与规范化文本都不变。
            var before = first.PublicSemantics.Digest;
            _ = first.CreateSession(MilestoneSeed);
            _ = first.CreateSession("ENV-IRONCLAD-01");
            Check.Equal(before, first.PublicSemantics.Digest, "建局不改变公开语义元数据");
            Check.False(first.PublicSemantics.CanonicalText().Contains("seed", StringComparison.Ordinal),
                "元数据里不得出现 seed 维度");
            Check.False(first.PublicSemantics.CanonicalText().Contains(MilestoneSeed, StringComparison.Ordinal),
                "元数据里不得出现每局 seed 值");

            // 判别力：把内容目录换一个 id（同一规则集），摘要必须变——"同配置稳定"不等于"对内容不敏感"。
            var rules = first.Rules;
            var source = SourceOf();
            var variant = new BasePublicContentSource(
                [.. source.CardIds, "base-spire:metadata-probe-card"],
                source.PowerIds, source.EnemyIds, source.RelicIds);
            var variantSemantics = BasePublicSemanticsBuilder.Build(rules, ProjectionId, SliceManifest.PlayerRole,
                variant);
            Check.True(variantSemantics.Digest != first.PublicSemantics.Digest,
                "内容目录变化必须改变元数据摘要（否则身份会在内容变化时静默不变）");
            Check.True(variantSemantics.CanonicalText().Contains("base-spire:metadata-probe-card",
                    StringComparison.Ordinal),
                "变化后的规范化文本必须能让接手者看出差在哪一条内容");
        });

        Check.Run("ENV-T03 元数据只读：字典与目录都不能被外部篡改 [真实]", () =>
        {
            var semantics = Factory().PublicSemantics;
            var catalog = semantics.Content[CardTypeId];
            var role = semantics.KnownPositions[KnownCardTypeId];

            Check.True(Record.Exception(() =>
                    ((IDictionary<string, BasePublicContentCatalog>)semantics.Content)
                    .Add("base-spire:Injected", catalog)) is NotSupportedException,
                "内容目录字典不能新增条目");
            Check.True(Record.Exception(() =>
                    ((IDictionary<string, BasePublicCollectionSemantics>)semantics.Collections)
                    ["base-spire:Hand"] =
                        BasePublicCollectionSemantics.NotPublished("x", "y")) is NotSupportedException,
                "集合用途字典不能替换条目");
            Check.True(Record.Exception(() =>
                    ((IDictionary<string, BasePublicTerminalOutcome>)semantics.TerminalOutcomes)
                    ["base-spire:Victory"] = BasePublicTerminalOutcome.Loss) is NotSupportedException,
                "终局解释字典不能被改写");
            Check.True(Record.Exception(() =>
                    ((IDictionary<string, BasePublicKnownPositionRole>)semantics.KnownPositions)
                    .Remove(KnownCardTypeId)) is NotSupportedException,
                "已知位置角色字典不能删除条目");
            Check.True(Record.Exception(() => ((IList<string>)catalog.ContentIds).Add("base-spire:injected"))
                is NotSupportedException,
                "内容 ID 目录列表不能追加");
            Check.True(Record.Exception(() =>
                    ((IDictionary<string, string>)role.PileCollectionByToken)["hand"] = "base-spire:Hand")
                is NotSupportedException,
                "标记词表不能被改写");

            // 元数据是构造期快照：两个工厂实例各自持有自己的只读投影，互不共享可变状态。
            var other = Factory().PublicSemantics;
            Check.Equal(semantics.Digest, other.Digest, "两个工厂的元数据等价");
            Check.False(ReferenceEquals(semantics, other), "每次构造产出独立实例（不是共享可变单例）");
        });
    }

    /// <summary>把正式构建的实际加载内容取出来（= 工厂构元数据时的同一份内容）。</summary>
    private static BasePublicContentSource SourceOf()
    {
        var content = SinglePlayerContentSource.Shared;
        return new BasePublicContentSource(
            content.CardIds,
            content.PowerIds,
            content.EnemyIds,
            [.. SpireRelics.All.Select(relic => relic.ContentId)]);
    }

    private static void CheckField(RuleSet rules, EntityTypeDefinition type, string fieldName,
        FieldTypeKind expected, string? refTargetTypeId)
    {
        Check.True(type.TryFieldIndex(fieldName, out var index),
            $"已知位置角色字段 '{type.QualifiedId}.{fieldName}' 必须真实存在");
        var field = type.Fields[index];
        Check.Equal(expected, field.Kind, $"'{type.QualifiedId}.{fieldName}' 的字段形状");
        if (refTargetTypeId is null) return;
        Check.Equal(refTargetTypeId, rules.Type(field.TargetTypeId).QualifiedId,
            $"'{type.QualifiedId}.{fieldName}' 的引用目标必须是真实卡牌实例");
    }
}
