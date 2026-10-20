using CoreTheSpire.Engine;
using NetTheSpire.Contracts;

namespace NetTheSpire.Projection;

/// <summary>
/// 正式公开出口的**声明派生**（ENV-T03）：以所选规则集与所选参与者投影为唯一结构来源，
/// 生成 <see cref="PublicSnapshotProfile"/>。
///
/// 派生规则（逐条对应 design.md §7.1/§7.2 与负责人复核第 1/4 条）：
/// <list type="number">
/// <item><b>实体种类</b>：投影里 <c>Include</c> 的每个实体类型一个公开 kind；</item>
/// <item><b>字段</b>：由 <see cref="FieldDefinition"/> 直接派生值类型（int/fixed/bool/enum/ref/string），
/// 枚举给出来自 <see cref="EnumTypeDefinition"/> 的类别词表，引用给出来自目标类型的 kind；
/// 不可编码的形状（participant/list/binding、未投影的引用目标、投影不包含的隐藏字段）**具名跳过**，
/// 不静默丢弃、也不自造值类型；</item>
/// <item><b>动作</b>：<c>RuleSet.Actions</c> 逐个派生，参数槽按 <see cref="ActionDefinition.Parameters"/>
/// 的名字/形状派生；不编码参数个数与域（域是运行时绑定，不是声明）；</item>
/// <item><b>关系/终局/集合</b>：关系按引擎的 <c>Public</c> 声明派生（授权边除外），终局按声明派生、
/// 胜负含义由包元数据给出；集合的**用途**由包元数据给出（内核只声明容量/可见性）；</item>
/// <item><b>身份</b>：catalog_id 是派生目录的规范摘要，ruleset_id 综合提供方静态构建身份与目录摘要，
/// backend_profile_id 综合协议版本、投影与目录摘要；**每局 seed 不进入任何静态身份**。</item>
/// </list>
///
/// 本文件不含任何具体游戏/内容名：新增一个公开字段或一条合法动作只改规则包，net 不需要新登记行。
/// </summary>
public static class DerivedPublicProfile
{
    /// <summary>公开 ID 的词法前缀（与 0.1 Profile 保持同一风格，便于人工核对）。</summary>
    public const string KindPrefix = "net:kind:";

    public const string FieldPrefix = "net:field:";

    public const string CategoryPrefix = "net:cat:";

    public const string RelationPrefix = "net:relation:";

    public const string PileTypePrefix = "net:pile_type:";

    public const string GroupTypePrefix = "net:group_type:";

    public const string SequenceTypePrefix = "net:sequence_type:";

    public const string ActionPrefix = "net:action:";

    public const string ReasonPrefix = "net:reason:";

    /// <summary>暂停期应答动作的公开 ID：它是 net 自声明操作（协议没有独立"应答"操作），不由规则派生。</summary>
    public const string AnswerActionTypeId = "net:action:net.answer_decision";

    public const string AnswerInstanceFieldId = "net:field:net.answer_decision.Instance";
    public const string AnswerFlagFieldId = "net:field:net.answer_decision.Flag";

    /// <summary>
    /// 从规则集与所选投影派生正式公开 Profile。
    /// </summary>
    /// <param name="rules">只读规则集（内容/链接/编译产物）。</param>
    /// <param name="projectionId">所选公开投影（例如 base 工厂的 <c>base-spire:PlayerView</c>）。</param>
    /// <param name="participantRole">该投影声明的参与者角色（与投影自带角色必须一致）。</param>
    /// <param name="metadata">规则提供方随构建给出的不可变补充元数据（只补四类游戏含义）。</param>
    /// <param name="protocolVersion">协议版本（0.2 正式出口；0.1 保留给旧切片/作者场景）。</param>
    public static PublicSnapshotProfile Build(
        RuleSet rules,
        string projectionId,
        string participantRole,
        PublicBuildMetadata metadata,
        string protocolVersion = NetProtocol.V02)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(metadata);
        NetProtocol.RequireKnown(protocolVersion, "DerivedPublicProfile.protocolVersion");

        var projection = rules.Projections.FirstOrDefault(item =>
                             string.Equals(item.QualifiedId, projectionId, StringComparison.Ordinal))
                         ?? throw new PublicMappingException("N-02-NO-PROJECTION",
                             $"规则集没有声明的公开投影 '{projectionId}'：正式出口必须在所选投影上派生");
        if (!string.Equals(projection.ParticipantRole, participantRole, StringComparison.Ordinal))
        {
            throw new PublicMappingException("N-02-PROJECTION-ROLE",
                $"投影 '{projectionId}' 声明给角色 '{projection.ParticipantRole}'，" +
                $"而控制入口绑定的是 '{participantRole}'");
        }

        var included = projection.Types.Values.Where(item => item.Include)
            .Select(item => item.Type.QualifiedId)
            .ToHashSet(StringComparer.Ordinal);
        if (included.Count == 0)
        {
            throw new PublicMappingException("N-02-NO-PROJECTION",
                $"投影 '{projectionId}' 没有包含任何实体类型：无法发布任何公开观察");
        }

        var kinds = BuildKinds(rules, metadata, protocolVersion, included, projection);
        var collections = BuildCollections(rules, metadata, included);
        var relations = BuildRelations(rules, projection, included);
        var actions = BuildActions(rules, included, protocolVersion);
        var terminals = BuildTerminals(rules, metadata);
        var answer = BuildAnswerAction(included, protocolVersion);
        var knownPositions = BuildKnownPositions(rules, metadata, collections);

        // 身份先按占位构造一次，用真实目录文档算目录摘要，再据此定稿（目录摘要本身不含 identity 字段）。
        var draft = new PublicSnapshotProfile(
            "net:catalog:pending", "net:catalog:pending", "net:backend:pending",
            kinds, collections, relations, actions, answer, terminals, knownPositions, protocolVersion);
        var catalogWire = CatalogProjector.Project(draft).ToWire();
        var catalogDigest = WireIdentity.CatalogDigest(catalogWire);
        var shortCatalog = Short(catalogDigest);
        var projectionAndCatalog = projectionId + "|" + catalogDigest + "|" + protocolVersion;

        return new PublicSnapshotProfile(
            rulesetId: "net:ruleset:" + Short(WireCanonical.Digest(WireValue.Object(
                ("canonical_version", WireValue.Str(WireCanonical.CanonicalVersion)),
                ("static_build", WireValue.Str(metadata.StaticBuildIdentity)),
                ("projection", WireValue.Str(projectionId)),
                ("catalog", WireValue.Str(catalogDigest)),
                ("protocol", WireValue.Str(protocolVersion))))),
            catalogId: "net:catalog:" + shortCatalog,
            backendProfileId: "net:backend:simulation-" + protocolVersion + "-" +
                              Short(WireCanonical.Digest(WireValue.Object(
                                  ("canonical_version", WireValue.Str(WireCanonical.CanonicalVersion)),
                                  ("kind", WireValue.Str("derived-engine-simulation")),
                                  ("projection_and_catalog", WireValue.Str(projectionAndCatalog))))),
            kinds: kinds,
            collections: collections,
            relations: relations,
            actions: actions,
            answerAction: answer,
            terminals: terminals,
            knownPositions: knownPositions,
            protocolVersion: protocolVersion);
    }

    private static string Short(string digest) =>
        digest.StartsWith("sha256:", StringComparison.Ordinal) ? digest["sha256:".Length..][..16] : digest[..16];

    // -- kinds / fields ----------------------------------------------------------------------------------------

    private static IReadOnlyDictionary<string, PublicKindRule> BuildKinds(
        RuleSet rules, PublicBuildMetadata metadata, string protocolVersion, HashSet<string> included,
        ProjectionDefinition projection)
    {
        var knownPositionFacts = new Dictionary<string, PublicKnownPositionRole>(StringComparer.Ordinal);
        foreach (var (factKindId, role) in metadata.KnownPositions)
        {
            if (!included.Contains(factKindId))
            {
                throw new PublicMappingException("N-02-KNOWN-POSITION-ROLE",
                    $"已知位置事实类型 '{factKindId}' 不在所选公开投影里：不得声明一个不发布的角色");
            }

            knownPositionFacts[factKindId] = role;
        }

        var kinds = new Dictionary<string, PublicKindRule>(StringComparer.Ordinal);
        foreach (var (_, typeProjection) in projection.Types.OrderBy(pair => pair.Key))
        {
            if (!typeProjection.Include) continue;
            var type = typeProjection.Type;
            PublicContentRule? content = null;
            if (metadata.Content.TryGetValue(type.QualifiedId, out var declared))
            {
                if (!type.TryFieldIndex(declared.FieldName, out _))
                {
                    throw new PublicMappingException("N-02-UNDECLARED-CONTENT",
                        $"内容目录声明 '{type.QualifiedId}.{declared.FieldName}' 不是该类型的字段：" +
                        "不得凭名字猜内容字段");
                }

                content = declared;
            }

            knownPositionFacts.TryGetValue(type.QualifiedId, out var role);
            var fields = new Dictionary<string, Disposition<PublishedField>>(StringComparer.Ordinal);
            foreach (var field in type.Fields)
            {
                fields[field.Name] = MapField(rules, type, field, typeProjection, content, role, protocolVersion,
                    included);
            }

            kinds[type.QualifiedId] = new PublicKindRule(
                KindPrefix + type.QualifiedId,
                ContentFieldName: content?.FieldName,
                ContentIds: content?.ContentIds.ToList() ?? [],
                Fields: fields);
        }

        return kinds;
    }

    private static Disposition<PublishedField> MapField(
        RuleSet rules,
        EntityTypeDefinition owner,
        FieldDefinition field,
        TypeProjection typeProjection,
        PublicContentRule? content,
        PublicKnownPositionRole? knownPositionRole,
        string protocolVersion,
        HashSet<string> included)
    {
        var where = owner.QualifiedId + "." + field.Name;
        // 内容标识走 entity.content_state/content_id（protocol.md §2 第 62/66 行），不在 fields 里重复。
        if (content is not null && string.Equals(content.FieldName, field.Name, StringComparison.Ordinal))
        {
            return Disposition<PublishedField>.Skip("N-02-CONTENT",
                "内容标识由 content_state/content_id 表达，按 protocol.md §2 第 94 行不在 fields 里重复发布");
        }

        // 已知位置事实的三个角色字段与观看者绑定由 known_positions/投影表达，不重复进公共字段。
        if (knownPositionRole is { } role)
        {
            if (string.Equals(role.CardFieldName, field.Name, StringComparison.Ordinal) ||
                string.Equals(role.PileFieldName, field.Name, StringComparison.Ordinal) ||
                string.Equals(role.PositionFieldName, field.Name, StringComparison.Ordinal))
            {
                return Disposition<PublishedField>.Skip("H-02-KNOWN-POSITION",
                    "真实卡牌实例引用与绝对位置由 known_positions 表达（protocol.md §2 第 85/89 行），" +
                    "在实体字段里重复发布会被 §2 第 94 行禁止");
            }
        }

        if (field.Visibility == FieldVisibility.Hidden && !typeProjection.IncludeHiddenFields)
        {
            return Disposition<PublishedField>.Skip("N-02-HIDDEN-FIELD",
                "引擎声明为 Hidden 且所选投影不包含隐藏字段：隐藏事实不得进入公开观察");
        }

        if (field.Kind == FieldTypeKind.Participant)
        {
            return Disposition<PublishedField>.Skip("N-02-VALUE-KIND",
                "协议值类型只有 number/boolean/category/entity_ref" +
                (NetProtocol.UsesGameSeedString(protocolVersion) ? "/string" : "") +
                "（protocol.md §2 第 70–75 行、§7）：参与者绑定不是公开游戏事实，也不自造值类型");
        }

        if (field.Kind is FieldTypeKind.List or FieldTypeKind.Any)
        {
            return Disposition<PublishedField>.Skip("N-02-VALUE-KIND",
                $"协议没有 {field.Kind} 值类型：不丢字段也不自造结构，需按 §7 版本化扩展后另行发布");
        }

        if (field.Kind == FieldTypeKind.String && !NetProtocol.UsesGameSeedString(protocolVersion))
        {
            return Disposition<PublishedField>.Skip("N-02-VALUE-KIND",
                $"协议 {protocolVersion} 没有 string 值类型（protocol.md §7 在 0.2 新增）：" +
                "公开 String 不冒充有限类别，旧版本不发布该字段");
        }

        // 引用字段按 optional 发布：投影会在目标实例不可识别时**省略**该引用（ProjectionObserver），
        // 把它声明成 required 会与投影契约冲突（I-22 会误报"必需字段缺失"）。
        var required = !field.Optional && field.Visibility == FieldVisibility.Public &&
                       field.Kind != FieldTypeKind.Ref;
        var fieldId = FieldPrefix + where;
        switch (field.Kind)
        {
            case FieldTypeKind.Int:
                return Disposition<PublishedField>.Publish(
                    new PublishedField(fieldId, PublicValueKind.Integer, required));
            case FieldTypeKind.Fixed:
                return Disposition<PublishedField>.Publish(
                    new PublishedField(fieldId, PublicValueKind.Decimal, required));
            case FieldTypeKind.Bool:
                return Disposition<PublishedField>.Publish(
                    new PublishedField(fieldId, PublicValueKind.Boolean, required));
            case FieldTypeKind.String:
                return Disposition<PublishedField>.Publish(
                    new PublishedField(fieldId, PublicValueKind.String, required));
            case FieldTypeKind.Enum:
            {
                var enumType = rules.Enums[field.EnumTypeId];
                var values = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var member in enumType.Members)
                {
                    values[member] = CategoryPrefix + enumType.QualifiedId + ":" + member;
                }

                return Disposition<PublishedField>.Publish(
                    new PublishedField(fieldId, PublicValueKind.Category, required, values));
            }

            case FieldTypeKind.Ref:
            {
                var targetKind = field.TargetTypeId < 0
                    ? null
                    : rules.Type(field.TargetTypeId).QualifiedId;
                if (targetKind is not null && !included.Contains(targetKind))
                {
                    return Disposition<PublishedField>.Skip("N-02-REF-TARGET",
                        $"引用目标种类 '{targetKind}' 不在所选公开投影里：引用无法在本快照闭合，" +
                        "不发布该字段也不自造替身");
                }

                return Disposition<PublishedField>.Publish(
                    new PublishedField(fieldId, PublicValueKind.EntityRef, Required: false,
                        TargetKindId: targetKind is null ? null : KindPrefix + targetKind));
            }

            default:
                throw new PublicMappingException("N-02-VALUE-KIND",
                    $"{where}: 未处理的引擎字段形状 {field.Kind}（派生层不静默跳过未知形状）");
        }
    }

    // -- collections -------------------------------------------------------------------------------------------

    private static IReadOnlyDictionary<string, CollectionDisposition> BuildCollections(
        RuleSet rules, PublicBuildMetadata metadata, HashSet<string> included)
    {
        var result = new Dictionary<string, CollectionDisposition>(StringComparer.Ordinal);
        foreach (var collection in rules.Collections.OrderBy(item => item.QualifiedId, StringComparer.Ordinal))
        {
            if (!metadata.Collections.TryGetValue(collection.QualifiedId, out var rule))
            {
                throw new PublicMappingException("N-02-UNDECLARED-COLLECTION",
                    $"引擎集合 '{collection.QualifiedId}' 没有公开用途：公开投影包含全部集合，" +
                    "每条都必须发布或具名不发布（不允许静默丢弃公开事实）");
            }

            var elementKind = KindPrefix + rules.Type(collection.ElementTypeId).QualifiedId;
            switch (rule.Use)
            {
                case PublicCollectionUse.Pile:
                {
                    var pileKindEngineId = RequireIncluded(rule.PileKindEngineTypeId!, included,
                        collection.QualifiedId, "pile_ref 实体种类");
                    if (!included.Contains(rules.Type(collection.ElementTypeId).QualifiedId))
                    {
                        throw new PublicMappingException("N-02-UNDECLARED-COLLECTION",
                            $"集合 '{collection.QualifiedId}' 的成员种类不在所选公开投影里：成员无法闭合，不得发布为 pile");
                    }

                    result[collection.QualifiedId] = CollectionDisposition.AsPile(new PublishedPile(
                        PileTypeId: PileTypePrefix + collection.QualifiedId,
                        PileKindId: KindPrefix + pileKindEngineId,
                        MemberKindIds: [elementKind],
                        PositionSchema: rule.PositionSchema));
                    break;
                }

                case PublicCollectionUse.Group:
                {
                    var ownerKind = RequireIncluded(rule.OwnerKindEngineTypeId!, included,
                        collection.QualifiedId, "group owner 种类");
                    if (!included.Contains(rules.Type(collection.ElementTypeId).QualifiedId))
                    {
                        throw new PublicMappingException("N-02-UNDECLARED-COLLECTION",
                            $"集合 '{collection.QualifiedId}' 的成员种类不在所选公开投影里：group 的 members 必须闭合");
                    }

                    result[collection.QualifiedId] = CollectionDisposition.AsGroup(new PublishedGroup(
                        GroupTypeId: GroupTypePrefix + collection.QualifiedId,
                        OwnerKindId: KindPrefix + ownerKind,
                        MemberKindIds: [elementKind],
                        MinCount: 0,
                        // 槽位/容量是引擎声明的规则，不在这里另抄数字（protocol.md §2 第 96 行）。
                        MaxCount: collection.Capacity == int.MaxValue ? null : collection.Capacity));
                    break;
                }

                case PublicCollectionUse.Sequence:
                {
                    // owner 只按公开种类声明：公开句柄由会话内绑定决定，派生层不写死每局句柄（W-02）。
                    // 该种类在每个快照里必须恰好有一个可见实例，由投影层解析；这里只核对它与成员都在投影里。
                    var sequenceOwnerKind = RequireIncluded(rule.OwnerKindEngineTypeId!, included,
                        collection.QualifiedId, "sequence owner 种类");
                    if (!included.Contains(rules.Type(collection.ElementTypeId).QualifiedId))
                    {
                        throw new PublicMappingException("N-02-UNDECLARED-COLLECTION",
                            $"集合 '{collection.QualifiedId}' 的成员种类不在所选公开投影里：sequence 的 elements 必须闭合");
                    }

                    result[collection.QualifiedId] = CollectionDisposition.AsSequence(
                        PublishedSequence.ForOwnerKind(
                            SequenceTypePrefix + collection.QualifiedId,
                            KindPrefix + sequenceOwnerKind,
                            [elementKind],
                            0,
                            // 与 group 同源：容量来自引擎声明的集合容量，不另抄数字（protocol.md §2 第 96 行）。
                            collection.Capacity == int.MaxValue ? null : collection.Capacity));
                    break;
                }

                default:
                    result[collection.QualifiedId] = CollectionDisposition.Skip(rule.SkipRuleId!, rule.SkipReason!);
                    break;
            }
        }

        return result;
    }

    private static string RequireIncluded(string engineTypeId, HashSet<string> included, string collection,
        string what)
    {
        if (!included.Contains(engineTypeId))
        {
            throw new PublicMappingException("N-02-UNDECLARED-COLLECTION",
                $"集合 '{collection}' 的{what} '{engineTypeId}' 不在所选公开投影里：无法唯一解析，不得猜一个");
        }

        return engineTypeId;
    }

    // -- relations ---------------------------------------------------------------------------------------------

    private static IReadOnlyDictionary<string, Disposition<PublicRelationRule>> BuildRelations(
        RuleSet rules, ProjectionDefinition projection, HashSet<string> included)
    {
        // 授权边（RevealProjection 指向的关系）是"谁被授权识别什么"，不是公开游戏事实：
        // 它的公开表达是 known_positions（H-01/H-02），发布它会暴露授权结构。
        var revealRelations = projection.Reveals.Select(reveal => reveal.RelationId).ToHashSet();
        var result = new Dictionary<string, Disposition<PublicRelationRule>>(StringComparer.Ordinal);
        var allKinds = included.Select(id => KindPrefix + id).OrderBy(id => id, StringComparer.Ordinal).ToList();
        foreach (var relation in rules.Relations.OrderBy(item => item.QualifiedId, StringComparer.Ordinal))
        {
            if (revealRelations.Contains(relation.Id))
            {
                result[relation.QualifiedId] = Disposition<PublicRelationRule>.Skip("H-01-REVEAL-EDGE",
                    "观看者授权边由 known_positions 表达（protocol.md §2 第 83–90 行）：它记录谁被授权，" +
                    "不是游戏事实，发布它会暴露授权结构并从它反推隐藏牌序");
                continue;
            }

            if (!relation.Public)
            {
                result[relation.QualifiedId] = Disposition<PublicRelationRule>.Skip("N-02-NOT-PUBLIC",
                    "引擎声明本身不是公开关系（RelationDefinition.Public=false）：不发布");
                continue;
            }

            var target = rules.Type(relation.TargetTypeId).QualifiedId;
            if (!included.Contains(target))
            {
                result[relation.QualifiedId] = Disposition<PublicRelationRule>.Skip("N-02-RELATION-TARGET",
                    $"关系目标种类 '{target}' 不在所选公开投影里：边无法闭合，不发布");
                continue;
            }

            result[relation.QualifiedId] = Disposition<PublicRelationRule>.Publish(new PublicRelationRule(
                RelationPrefix + relation.QualifiedId,
                // 关系的持有者可以是任意可识别实例；公开目录只能给出种类上界（过近似，不猜具体种类）。
                SourceKindIds: allKinds,
                TargetKindIds: [KindPrefix + target]));
        }

        return result;
    }

    // -- actions -----------------------------------------------------------------------------------------------

    private static IReadOnlyDictionary<string, PublicActionRule> BuildActions(
        RuleSet rules, HashSet<string> included, string protocolVersion)
    {
        var actions = new Dictionary<string, PublicActionRule>(StringComparer.Ordinal);
        foreach (var action in rules.Actions.OrderBy(item => item.QualifiedId, StringComparer.Ordinal))
        {
            var parameters = new Dictionary<int, PublicActionField>();
            for (var slot = 0; slot < action.Parameters.Count; slot++)
            {
                var parameter = action.Parameters[slot];
                var where = $"{action.QualifiedId}.{parameter.Name}";
                var fieldId = FieldPrefix + action.QualifiedId + "." + parameter.Name;
                switch (parameter.Kind)
                {
                    case FieldTypeKind.Int:
                        parameters[slot] = new PublicActionField(fieldId, PublicValueKind.Integer, Required: true);
                        break;
                    case FieldTypeKind.Fixed:
                        parameters[slot] = new PublicActionField(fieldId, PublicValueKind.Decimal, Required: true);
                        break;
                    case FieldTypeKind.Bool:
                        parameters[slot] = new PublicActionField(fieldId, PublicValueKind.Boolean, Required: true);
                        break;
                    case FieldTypeKind.String when NetProtocol.UsesGameSeedString(protocolVersion):
                        parameters[slot] = new PublicActionField(fieldId, PublicValueKind.String, Required: true);
                        break;
                    case FieldTypeKind.Enum:
                    {
                        var enumType = rules.Enums[parameter.EnumTypeId];
                        var values = new Dictionary<string, string>(StringComparer.Ordinal);
                        foreach (var member in enumType.Members)
                        {
                            values[member] = CategoryPrefix + enumType.QualifiedId + ":" + member;
                        }

                        parameters[slot] = new PublicActionField(fieldId, PublicValueKind.Category, values,
                            Required: true);
                        break;
                    }

                    case FieldTypeKind.Ref:
                    {
                        var target = parameter.TargetTypeId < 0 ? null : rules.Type(parameter.TargetTypeId).QualifiedId;
                        if (target is not null && !included.Contains(target))
                        {
                            throw new PublicMappingException("N-02-ACTION-PARAM",
                                $"动作 '{where}' 的引用目标种类 '{target}' 不在所选公开投影里：" +
                                "候选值无法编码，不得静默省略该动作");
                        }

                        parameters[slot] = new PublicActionField(fieldId, PublicValueKind.EntityRef,
                            Required: true,
                            TargetKindIds: target is null ? null : [KindPrefix + target]);
                        break;
                    }

                    default:
                        // 动作参数是"执行所需字段"（protocol.md §2 第 98 行要求 known）：不可编码就**显式失败**，
                        // 不能悄悄少发一条动作，也不能在快照期才报 UNDECLARED-ACTION。
                        throw new PublicMappingException("N-02-ACTION-PARAM",
                            $"动作 '{where}' 的参数形状 {parameter.Kind} 在当前协议版本 {protocolVersion} 下不可编码：" +
                            "需要按 protocol.md §7 版本化扩展，映射层不省略该动作");
                }
            }

            actions[action.QualifiedId] = new PublicActionRule(ActionPrefix + action.QualifiedId, parameters);
        }

        return actions;
    }

    private static PublicActionRule BuildAnswerAction(HashSet<string> included, string protocolVersion)
    {
        var parameters = new Dictionary<int, PublicActionField>
            {
                // 决策域不是单一类型：候选可以是卡牌、敌人或其它实体，因此按**公开种类全集**声明上界
                // （protocol.md §2 第 100 行允许，但不得为此截断真实候选）。
                [0] = new PublicActionField(AnswerInstanceFieldId, PublicValueKind.EntityRef, Required: protocolVersion == NetProtocol.V01,
                    TargetKindIds: included.Select(id => KindPrefix + id).OrderBy(id => id, StringComparer.Ordinal)
                        .ToList()),
            };
        if (protocolVersion == NetProtocol.V02)
            parameters[1] = new PublicActionField(AnswerFlagFieldId, PublicValueKind.Boolean, Required: false);
        return new PublicActionRule(AnswerActionTypeId, parameters, NetDeclared: true);
    }

    // -- terminals / known positions ---------------------------------------------------------------------------

    private static IReadOnlyDictionary<string, PublicTerminalRule> BuildTerminals(
        RuleSet rules, PublicBuildMetadata metadata)
    {
        var terminals = new Dictionary<string, PublicTerminalRule>(StringComparer.Ordinal);
        foreach (var terminal in rules.TerminalResults.OrderBy(item => item.QualifiedId, StringComparer.Ordinal))
        {
            if (!metadata.TerminalOutcomes.TryGetValue(terminal.QualifiedId, out var outcome))
            {
                throw new PublicMappingException("N-02-TERMINAL-OUTCOME",
                    $"终局结果 '{terminal.QualifiedId}' 没有胜负解释：不能根据终局名字猜 win/loss");
            }

            if (outcome is not ("win" or "loss" or "other"))
            {
                throw new PublicMappingException("N-02-TERMINAL-OUTCOME",
                    $"终局结果 '{terminal.QualifiedId}' 的胜负解释 '{outcome}' 不在 win/loss/other 内");
            }

            var fields = new Dictionary<string, Disposition<PublishedField>>(StringComparer.Ordinal);
            foreach (var field in terminal.Fields)
            {
                fields[field.Name] = MapTerminalField(rules, terminal.QualifiedId, field);
            }

            terminals[terminal.QualifiedId] = new PublicTerminalRule(
                ReasonPrefix + terminal.QualifiedId, outcome, fields);
        }

        return terminals;
    }

    private static Disposition<PublishedField> MapTerminalField(
        RuleSet rules, string terminalQualifiedId, FieldDefinition field)
    {
        var fieldId = FieldPrefix + terminalQualifiedId + "." + field.Name;
        switch (field.Kind)
        {
            case FieldTypeKind.Int:
                return Disposition<PublishedField>.Publish(
                    new PublishedField(fieldId, PublicValueKind.Integer, Required: !field.Optional));
            case FieldTypeKind.Fixed:
                return Disposition<PublishedField>.Publish(
                    new PublishedField(fieldId, PublicValueKind.Decimal, Required: !field.Optional));
            case FieldTypeKind.Bool:
                return Disposition<PublishedField>.Publish(
                    new PublishedField(fieldId, PublicValueKind.Boolean, Required: !field.Optional));
            case FieldTypeKind.String:
                return Disposition<PublishedField>.Publish(
                    new PublishedField(fieldId, PublicValueKind.String, Required: !field.Optional));
            case FieldTypeKind.Enum:
            {
                var enumType = rules.Enums[field.EnumTypeId];
                var values = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var member in enumType.Members)
                {
                    values[member] = CategoryPrefix + enumType.QualifiedId + ":" + member;
                }

                return Disposition<PublishedField>.Publish(
                    new PublishedField(fieldId, PublicValueKind.Category, !field.Optional, values));
            }

            default:
                throw new PublicMappingException("N-02-TERMINAL-FIELD",
                    $"终局结果 '{terminalQualifiedId}.{field.Name}' 的字段形状 {field.Kind} 不可编码：" +
                    "终局事实是声明的一部分，不能静默省略该字段");
        }
    }

    private static IReadOnlyDictionary<string, PublicKnownPositionRule> BuildKnownPositions(
        RuleSet rules, PublicBuildMetadata metadata, IReadOnlyDictionary<string, CollectionDisposition> collections)
    {
        var result = new Dictionary<string, PublicKnownPositionRule>(StringComparer.Ordinal);
        foreach (var (factKindId, role) in metadata.KnownPositions)
        {
            var tokens = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (token, collectionId) in role.PileTypeByCollection)
            {
                if (!collections.TryGetValue(collectionId, out var disposition))
                {
                    throw new PublicMappingException("H-01-KNOWN-FACT",
                        $"已知位置角色 '{factKindId}' 把标记 '{token}' 指向引擎集合 '{collectionId}'，" +
                        "但该集合没有公开用途声明");
                }

                if (disposition.Pile is not { } pile)
                {
                    throw new PublicMappingException("H-01-KNOWN-FACT",
                        $"已知位置角色 '{factKindId}' 的标记 '{token}' 指向集合 '{collectionId}'，" +
                        "而它没有发布为 pile：位置只能落在公开牌堆上");
                }

                tokens[token] = pile.PileTypeId;
            }

            result[factKindId] = new PublicKnownPositionRule(
                FactKindId: factKindId,
                CardFieldName: role.CardFieldName,
                PileFieldName: role.PileFieldName,
                PositionFieldName: role.PositionFieldName,
                PileTypeByToken: tokens);
        }

        return result;
    }

    /// <summary>规则集里的一条内容 ID 是否由某个类型的公开内容目录声明（诊断用）。</summary>
    public static bool DeclaresContent(PublicSnapshotProfile profile, string contentId) =>
        profile.Kinds.Values.Any(kind => kind.ContentIds.Contains(contentId, StringComparer.Ordinal));
}
