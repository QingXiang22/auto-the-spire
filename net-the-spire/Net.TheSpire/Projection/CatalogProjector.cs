using NetTheSpire.Contracts;
using NetTheSpire.Contracts.Dto;

namespace NetTheSpire.Projection;

/// <summary>
/// 运行时声明 → 协议 <see cref="PublicCatalogDto"/> 的适配器。
///
/// 单一来源是 <see cref="PublicSnapshotProfile"/>（它已承载公开 id 的映射约定），**不**另建一套 id 词表。
/// 已导出：entity_kinds、content_types、fields（实体/动作/终局三类 owner）、action_types、
/// terminal_reasons、relation_types、pile_types、**sequence_types**、**group_types**（W-01：药水栏等有 owner 的集合）。
/// `sequence_types` 只在规则声明了公开有序集合时才非空（此前硬编码为空的理由——"引擎没有 Sequence 声明"——
/// 只是当时的输入事实，不能当成映射层的规则）。
/// </summary>
public static class CatalogProjector
{
    public static PublicCatalogDto Project(PublicSnapshotProfile profile)
    {
        var allKinds = profile.Kinds.Values.Select(rule => rule.KindId).OrderBy(id => id, ById).ToList();
        // 目录里的 **kind id 是公开 id**（`net:kind:<引擎类型>`），与快照实体上的 kind_id 逐字一致；
        // 引擎的 qualified id 只作映射键，不能当公开声明 id（否则目录与 wire 引用不闭合，V-15 会抓到）。
        var entityKinds = profile.Kinds.Values
            .Select(rule => new EntityKindDeclarationDto(rule.KindId, "1", rule.KindId))
            .OrderBy(declaration => declaration.Id, ById)
            .ToList();

        // content_types 按**内容 ID** 唯一（I-86）。协议允许一个内容 ID 属于多个 kind（`kind_ids` 是列表）——
        // 作者内容里 `feel-no-pain` 同时是卡牌与能力的内容 ID，因此这里按 ID 合并 kind 列表，
        // 而不是每个 (kind, 内容 ID) 发一条（那会产生重复 id，被共享 L2 判为 I-86）。
        var contentTypes = profile.Kinds.Values
            .SelectMany(rule => rule.ContentIds.Select(contentId => (ContentId: contentId, KindId: rule.KindId)))
            .GroupBy(entry => entry.ContentId, StringComparer.Ordinal)
            .Select(group => new ContentTypeDeclarationDto(
                group.Key, "1", group.Key, Sorted(group.Select(entry => entry.KindId).Distinct().ToList())))
            .OrderBy(declaration => declaration.Id, ById)
            .ToList();

        var fields = new List<FieldDeclarationDto>();
        foreach (var rule in profile.Kinds.Values)
        {
            foreach (var (engineName, disposition) in rule.Fields)
            {
                if (disposition.IsPublished)
                {
                    fields.Add(ToField(disposition.Published!, kindIds: [rule.KindId], actionTypes: [],
                        terminalReasons: [], description: $"{rule.KindId}.{engineName}", allKinds));
                }
            }
        }

        var actionTypes = new List<ActionTypeDeclarationDto>();
        foreach (var (engineActionId, rule) in profile.Actions)
        {
            var requiredFields = new List<string>();
            var optionalFields = new List<string>();
            foreach (var (slot, parameter) in rule.Parameters)
            {
                fields.Add(ToField(new PublishedField(parameter.FieldId, parameter.Kind, parameter.Required,
                        parameter.CategoryValues, TargetKindId: null),
                    kindIds: [], actionTypes: [rule.ActionTypeId], terminalReasons: [],
                    description: $"{rule.ActionTypeId}.slot{slot}", allKinds,
                    targetKindIds: parameter.TargetKindIds));
                (parameter.Required ? requiredFields : optionalFields).Add(parameter.FieldId);
            }

            actionTypes.Add(new ActionTypeDeclarationDto(rule.ActionTypeId, "1", rule.ActionTypeId,
                RequiredFields: requiredFields, OptionalFields: optionalFields));
        }

        // net 自声明的动作（暂停期的应答走 `step` 的候选，不在引擎动作表里）也必须进目录：它的
        // `action_type` 出现在 wire 的 legal_actions 里，缺声明就破坏了引用闭合（I-05）。
        {
            var answer = profile.AnswerAction;
            actionTypes.Add(new ActionTypeDeclarationDto(answer.ActionTypeId, "1", answer.ActionTypeId,
                RequiredFields: answer.Parameters.Values.Where(parameter => parameter.Required)
                    .Select(parameter => parameter.FieldId).OrderBy(id => id, ById).ToList(),
                OptionalFields: answer.Parameters.Values.Where(parameter => !parameter.Required)
                    .Select(parameter => parameter.FieldId).OrderBy(id => id, ById).ToList()));
            foreach (var (slot, parameter) in answer.Parameters)
            {
                fields.Add(ToField(
                    new PublishedField(parameter.FieldId, parameter.Kind, parameter.Required,
                        parameter.CategoryValues, TargetKindId: null),
                    kindIds: [], actionTypes: [answer.ActionTypeId], terminalReasons: [],
                    description: $"{answer.ActionTypeId}.slot{slot}", allKinds,
                    targetKindIds: parameter.TargetKindIds));
            }
        }

        var terminalReasons = new List<TerminalReasonDeclarationDto>();
        foreach (var (engineTerminalId, rule) in profile.Terminals)
        {
            var requiredFields = new List<string>();
            var optionalFields = new List<string>();
            foreach (var (fieldName, disposition) in rule.Fields)
            {
                if (!disposition.IsPublished)
                {
                    continue;
                }

                fields.Add(ToField(disposition.Published!, kindIds: [], actionTypes: [],
                    terminalReasons: [rule.ReasonId], description: $"{rule.ReasonId}.{fieldName}", allKinds));
                (disposition.Published!.Required ? requiredFields : optionalFields)
                    .Add(disposition.Published!.FieldId);
            }

            terminalReasons.Add(new TerminalReasonDeclarationDto(rule.ReasonId, "1", rule.ReasonId,
                rule.Outcome, RequiredFields: requiredFields, OptionalFields: optionalFields));
        }

        var relationTypes = profile.Relations
            .Select(pair => pair.Value.Published)
            .Where(rule => rule is not null)
            .Select(rule => new RelationTypeDeclarationDto(rule!.TypeId, "1", rule.TypeId,
                Sorted(rule.SourceKindIds), Sorted(rule.TargetKindIds), PositionSchema: null))
            .OrderBy(declaration => declaration.Id, ById)
            .ToList();

        // pile_types：每种牌堆区一个声明，kind_id 唯一（H-02）。position_schema 为 null 表示该种牌堆没有公开位置语义。
        var pileTypes = profile.PileTypes
            .Select(pile => new PileTypeDeclarationDto(
                pile.PileTypeId,
                "1",
                pile.PileTypeId,
                pile.PileKindId,
                Sorted(pile.MemberKindIds),
                pile.PositionSchema is null
                    ? null
                    : new PositionSchemaDto(pile.PositionSchema.Unit, pile.PositionSchema.Origin,
                        pile.PositionSchema.Min, pile.PositionSchema.Max),
                ExclusiveGroupId: null,
                MembershipScopeFieldId: null))
            .OrderBy(declaration => declaration.Id, ById)
            .ToList();

        // 公开有序流程（protocol.md:96）。owner 只能按**公开种类**声明：目录的 owner_kind_ids 是 kind id 列表，
        // 写死的公开实例引用（旧兼容路径）在这里无法表达，因此显式拒绝而不是发一条假的空 owner 声明。
        var sequenceTypes = profile.SequenceTypes
            .Select(sequence =>
            {
                if (sequence.OwnerKindId is not { } ownerKindId)
                {
                    throw new PublicMappingException("N-02-SEQUENCE-OWNER",
                        $"sequence '{sequence.SequenceTypeId}' 的 owner 是一个写死的公开引用 " +
                        $"'{sequence.OwnerPublicRef}'：目录的 owner_kind_ids 只能声明公开种类，无法表达固定实例引用");
                }

                return new SequenceTypeDeclarationDto(
                    sequence.SequenceTypeId,
                    "1",
                    sequence.SequenceTypeId,
                    Sorted([ownerKindId]),
                    Sorted(sequence.ElementKindIds),
                    sequence.MinCount,
                    sequence.MaxCount);
            })
            .OrderBy(declaration => declaration.Id, ById)
            .ToList();

        // W-01：group_types——"有 owner 的集合"（例如作者包里的药水栏）。min/max_count 来自 **Profile 声明**，
        // 而 Profile 的容量又从引擎的集合容量推导（`ProjectedCollection.Capacity`），因此目录与运行时同一个数字。
        // 引擎集合的成员唯一 ⇒ 不声明重数上限（multiplicity 恒为 1）。
        var groupTypes = profile.GroupTypes
            .Select(group => new GroupTypeDeclarationDto(
                group.GroupTypeId,
                "1",
                group.GroupTypeId,
                Sorted([group.OwnerKindId]),
                Sorted(group.MemberKindIds),
                group.MinCount,
                group.MaxCount))
            .OrderBy(declaration => declaration.Id, ById)
            .ToList();

        // I-79：使用 piles 的目录必须声明保留的派生关系与两个派生字段（`protocol.md:94`）。
        // 它们在 wire 的 relations/entity.fields 里**不重复填入**，只作为目录声明存在（张量化派生）。
        // 这里必须真的发射到目录文档里：只在 profile 的内部清单里"声明"是不够的（那正是此前的缺口，
        // 由共享 L2 校验器在真实 catalog 上抓到）。
        if (profile.DeclaresPileTypes)
        {
            var pileKindIds = Sorted(profile.PileTypes.Select(pile => pile.PileKindId).Distinct().ToList());
            var memberKindIds = Sorted(profile.PileTypes.SelectMany(pile => pile.MemberKindIds)
                .Distinct().ToList());
            relationTypes.Add(new RelationTypeDeclarationDto(
                PublicSnapshotProfile.ReservedPileRelationId, "1",
                "派生：牌堆成员（张量化用；wire 的 relations 不重复填入）",
                pileKindIds, memberKindIds, PositionSchema: null));
            fields.Add(ToField(
                new PublishedField(PublicSnapshotProfile.ReservedPileTotalCountId, PublicValueKind.Integer,
                    Required: false),
                pileKindIds, [], [], "派生：牌堆真实总数（张量化用；wire 的 entity.fields 不重复填入）",
                allKinds));
            fields.Add(ToField(
                new PublishedField(PublicSnapshotProfile.ReservedPileMembersCompleteId, PublicValueKind.Boolean,
                    Required: false),
                pileKindIds, [], [], "派生：成员是否完整（张量化用；wire 的 entity.fields 不重复填入）",
                allKinds));
        }

        // contract-artifacts §3：声明表按 id 排序、ID 集合排序（有序类别/序列不擅自排序）。
        // 目录的身份摘要本来就会排序，但 **wire 数组本身**也必须规范有序，否则同一目录以不同注册顺序
        // 产出不同字节（V-01），跨端字节比较与下游缓存都会漂移。
        fields.Sort((left, right) => Id(left.Id, right.Id));
        actionTypes.Sort((left, right) => Id(left.Id, right.Id));
        terminalReasons.Sort((left, right) => Id(left.Id, right.Id));
        relationTypes.Sort((left, right) => Id(left.Id, right.Id));
        sequenceTypes.Sort((left, right) => Id(left.Id, right.Id));
        groupTypes.Sort((left, right) => Id(left.Id, right.Id));
        var declarations = new DeclarationsDto(
            EntityKinds: entityKinds,
            ContentTypes: contentTypes,
            Fields: fields,
            RelationTypes: relationTypes,
            PileTypes: pileTypes,
            SequenceTypes: sequenceTypes,
            GroupTypes: groupTypes,
            ActionTypes: actionTypes,
            TerminalReasons: terminalReasons);
        RequireReservedPileDeclarations(profile, declarations);

        return new PublicCatalogDto(
            // 目录的 protocol_version 必须与**所选 Profile 的版本**一致（protocol.md §7：
            // 0.2 所有带版本的目录/快照必须一致地写 0.2）。用 V01 常量会让 0.2 目录自称 0.1，
            // 于是 exact identity 判不出真正的版本错配。
            profile.ProtocolVersion,
            profile.RulesetId,
            profile.CatalogId,
            declarations);
    }

    /// <summary>
    /// 发布前自检：使用 piles 时，**目录文档本身**必须带三个保留声明（`protocol.md:94`/I-79）。
    /// 与 <see cref="PublicSnapshotProfile.RequireReservedPileDeclarations"/> 的区别是这里检查发射物，
    /// 而不是只检查 profile 的内部清单。
    /// </summary>
    private static void RequireReservedPileDeclarations(PublicSnapshotProfile profile, DeclarationsDto declarations)
    {
        if (!profile.DeclaresPileTypes) return;
        var missing = new List<string>();
        if (declarations.RelationTypes.All(item => item.Id != PublicSnapshotProfile.ReservedPileRelationId))
        {
            missing.Add(PublicSnapshotProfile.ReservedPileRelationId);
        }

        foreach (var fieldId in new[]
                 {
                     PublicSnapshotProfile.ReservedPileTotalCountId,
                     PublicSnapshotProfile.ReservedPileMembersCompleteId,
                 })
        {
            if (declarations.Fields.All(item => item.Id != fieldId)) missing.Add(fieldId);
        }

        if (missing.Count > 0)
        {
            throw new PublicMappingException("I-79",
                "使用 piles 的目录文档缺少保留声明: " + string.Join(",", missing));
        }
    }

    /// <summary>规范顺序：按公开 id 的 UTF-8 字节序（contract-artifacts §3）；空表保持空。</summary>
    private static int Id(string left, string right) => WireCanonical.CompareByUtf8Bytes(left, right);

    private static readonly IComparer<string> ById = Comparer<string>.Create(Id);

    private static IReadOnlyList<string> Sorted(IReadOnlyList<string> ids) =>
        ids.OrderBy(id => id, ById).ToList();

    private static FieldDeclarationDto ToField(
        PublishedField field,
        IReadOnlyList<string> kindIds,
        IReadOnlyList<string> actionTypes,
        IReadOnlyList<string> terminalReasons,
        string description,
        IReadOnlyList<string> allKinds,
        IReadOnlyList<string>? targetKindIds = null)
    {
        var (valueType, numberFormat) = field.Kind switch
        {
            PublicValueKind.Integer => ("number", "integer"),
            PublicValueKind.Decimal => ("number", "decimal"),
            PublicValueKind.Boolean => ("boolean", null),
            PublicValueKind.Category => ("category", null),
            PublicValueKind.String => ("string", null),
            _ => ("entity_ref", null),
        };
        return new FieldDeclarationDto(
            field.FieldId,
            "1",
            description,
            valueType,
            new AppliesToDto(Sorted(kindIds), Sorted(actionTypes), Sorted(terminalReasons)),
            field.Required,
            "public",
            numberFormat,
            // `category_ids` 必须是 **wire 上出现的公开类别 ID**（即映射的值），不是引擎原值（映射的键）：
            // `protocol.md:74` 要求 category 的 wire 值来自目录声明，声明原始名会让消费者无法解析真实值。
            field.CategoryValues is null
                ? null
                : Sorted(field.CategoryValues.Values.Distinct().ToList()),
            targetKindIds is not null ? Sorted(targetKindIds.Distinct().ToList()) : TargetKinds(field, allKinds),
            null,
            null);
    }

    /// <summary>
    /// `entity_ref` 字段的目标种类：来自 profile 的**显式声明**（H-02）。未声明时取公开实体种类全集作为上界，
    /// 而不是虚构某一个种类；该过近似登记为 `TODO-N03-CATALOG-TARGET-KINDS`，由 profile 逐字段收紧。
    /// </summary>
    private static IReadOnlyList<string>? TargetKinds(PublishedField field, IReadOnlyList<string> allKinds)
    {
        if (field.Kind != PublicValueKind.EntityRef)
        {
            return null;
        }

        return field.TargetKindId is { } declared ? [declared] : allKinds;
    }}
