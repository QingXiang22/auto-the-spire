namespace NetTheSpire.Contracts.Dto;

/// <summary>身份 <c>{protocol_version, ruleset_id, catalog_id, backend_profile_id}</c>。</summary>
public sealed record IdentityDto(
    string ProtocolVersion,
    string RulesetId,
    string CatalogId,
    string BackendProfileId)
{
    /// <summary>0.1 默认分支（旧切片/作者场景）。0.2 走 <see cref="FromWire(WireValue,string)"/>。</summary>
    public const string ExpectedProtocolVersion = NetProtocol.V01;

    public static IdentityDto FromWire(WireValue value) => FromWire(value, NetProtocol.V01);

    public static IdentityDto FromWire(WireValue value, string protocolVersion)
    {
        NetProtocol.RequireKnown(protocolVersion, "identity.protocol_version（运行时声明）");
        var reader = new WireReader(value, "identity")
            .Known("protocol_version", "ruleset_id", "catalog_id", "backend_profile_id");
        var protocol = reader.Require("protocol_version");
        if (protocol is not WireString version || version.Value != protocolVersion)
            throw new WireFaultException(
                "I-66", $"identity.protocol_version: 本版要求完全相等 '{protocolVersion}'");
        return new IdentityDto(
            protocolVersion,
            reader.RequireNonEmptyString("ruleset_id"),
            reader.RequireNonEmptyString("catalog_id"),
            reader.RequireNonEmptyString("backend_profile_id"));
    }

    public WireObject ToWire() => WireValue.Object(
        ("protocol_version", WireValue.Str(ProtocolVersion)),
        ("ruleset_id", WireValue.Str(RulesetId)),
        ("catalog_id", WireValue.Str(CatalogId)),
        ("backend_profile_id", WireValue.Str(BackendProfileId)));
}

public sealed record PositionSchemaDto(string Unit, string Origin, long Min, long? Max)
{
    public static PositionSchemaDto FromWire(WireValue value)
    {
        var reader = new WireReader(value, "position_schema").Known("unit", "origin", "min", "max");
        var min = reader.RequireSafeInteger("min");
        long? max = null;
        var maxNode = reader.Require("max");
        if (maxNode is not WireNull) max = WireDecimal.SafeInteger(maxNode, "position_schema.max");
        if (max is not null && max < min)
            throw new WireFaultException("N-01-DTO", "position_schema: max 不得小于 min");
        return new PositionSchemaDto(
            reader.RequireNonEmptyString("unit"), reader.RequireNonEmptyString("origin"), min, max);
    }

    public WireObject ToWire() => WireValue.Object(
        ("unit", WireValue.Str(Unit)),
        ("origin", WireValue.Str(Origin)),
        ("min", WireValue.Integer(Min)),
        ("max", WireBuild.NullableInteger(Max)));
}

public sealed record AppliesToDto(
    IReadOnlyList<string> EntityKinds,
    IReadOnlyList<string> ActionTypes,
    IReadOnlyList<string> TerminalReasons)
{
    public static AppliesToDto FromWire(WireValue value)
    {
        var reader = new WireReader(value, "applies_to").Known("entity_kinds", "action_types", "terminal_reasons");
        return new AppliesToDto(
            reader.RequireIdList("entity_kinds"),
            reader.RequireIdList("action_types"),
            reader.RequireIdList("terminal_reasons"));
    }

    public WireObject ToWire() => WireValue.Object(
        ("entity_kinds", WireBuild.IdList(EntityKinds)),
        ("action_types", WireBuild.IdList(ActionTypes)),
        ("terminal_reasons", WireBuild.IdList(TerminalReasons)));
}

public sealed record EntityKindDeclarationDto(string Id, string SemanticVersion, string Description)
{
    public static EntityKindDeclarationDto FromWire(WireValue value)
    {
        var reader = new WireReader(value, "entity_kind").Known("id", "semantic_version", "description");
        return new EntityKindDeclarationDto(
            reader.RequireNonEmptyString("id"),
            reader.RequireNonEmptyString("semantic_version"),
            reader.RequireString("description"));
    }

    public WireObject ToWire() => WireValue.Object(
        ("id", WireValue.Str(Id)),
        ("semantic_version", WireValue.Str(SemanticVersion)),
        ("description", WireValue.Str(Description)));
}

public sealed record ContentTypeDeclarationDto(
    string Id,
    string SemanticVersion,
    string Description,
    IReadOnlyList<string> KindIds)
{
    public static ContentTypeDeclarationDto FromWire(WireValue value)
    {
        var reader = new WireReader(value, "content_type")
            .Known("id", "semantic_version", "description", "kind_ids");
        return new ContentTypeDeclarationDto(
            reader.RequireNonEmptyString("id"),
            reader.RequireNonEmptyString("semantic_version"),
            reader.RequireString("description"),
            reader.RequireIdList("kind_ids"));
    }

    public WireObject ToWire() => WireValue.Object(
        ("id", WireValue.Str(Id)),
        ("semantic_version", WireValue.Str(SemanticVersion)),
        ("description", WireValue.Str(Description)),
        ("kind_ids", WireBuild.IdList(KindIds)));
}

    /// <summary>
    /// 字段声明。类型专用键按 value_type 精确配对（protocol.md §1 第 40 行：
    /// 未列的类型专用键不得混入其他值类型）。
    ///
    /// 0.2 在第 7 节新增 <c>string</c> 值类型（公开字符串值原样编码，不加类别词表）；
    /// 0.1 的机器 schema 仍只承认四个旧值类型，因此旧文档不会被放宽。
    /// </summary>
    public sealed record FieldDeclarationDto(
    string Id,
    string SemanticVersion,
    string Description,
    string ValueType,
    AppliesToDto AppliesTo,
    bool Required,
    string Visibility,
    string? NumberFormat,
    IReadOnlyList<string>? CategoryIds,
    IReadOnlyList<string>? TargetKindIds,
    string? ConditionDescription,
    IReadOnlyList<string>? VisibilityCheckIds)
{
    private static readonly string[] ValueTypes = ["number", "boolean", "category", "entity_ref", "string"];
    private static readonly string[] NumberFormats = ["integer", "decimal"];
    private static readonly string[] Visibilities = ["public", "conditional"];

    public static FieldDeclarationDto FromWire(WireValue value)
    {
        var reader = new WireReader(value, "field_declaration").Known(
            "id", "semantic_version", "description", "value_type", "applies_to", "required", "visibility",
            "number_format", "category_ids", "target_kind_ids", "condition_description", "visibility_check_ids");
        var valueType = reader.RequireString("value_type");
        if (!ValueTypes.Contains(valueType, StringComparer.Ordinal))
            throw new WireFaultException(
                "I-66", $"field_declaration.value_type: 不在枚举 {string.Join('/', ValueTypes)}");
        var visibility = reader.RequireString("visibility");
        if (!Visibilities.Contains(visibility, StringComparer.Ordinal))
            throw new WireFaultException(
                "I-66", $"field_declaration.visibility: 不在枚举 {string.Join('/', Visibilities)}");
        var numberFormat = reader.OptionalNonEmptyString("number_format");
        if (numberFormat is not null && !NumberFormats.Contains(numberFormat, StringComparer.Ordinal))
            throw new WireFaultException(
                "I-66", $"field_declaration.number_format: 不在枚举 {string.Join('/', NumberFormats)}");
        var categoryIds = reader.OptionalStringList("category_ids");
        var targetKindIds = reader.OptionalStringList("target_kind_ids");
        var conditionDescription = reader.Has("condition_description")
            ? reader.RequireString("condition_description")
            : null;
        var visibilityCheckIds = reader.OptionalStringList("visibility_check_ids");

        ExpectPairing(valueType == "number", numberFormat is not null, "number_format", valueType);
        ExpectPairing(valueType == "category", categoryIds is not null, "category_ids", valueType);
        ExpectPairing(valueType == "entity_ref", targetKindIds is not null, "target_kind_ids", valueType);
        ExpectPairing(
            visibility == "conditional",
            conditionDescription is not null && visibilityCheckIds is { Count: > 0 },
            "condition_description/visibility_check_ids",
            visibility);

        return new FieldDeclarationDto(
            reader.RequireNonEmptyString("id"),
            reader.RequireNonEmptyString("semantic_version"),
            reader.RequireString("description"),
            valueType,
            AppliesToDto.FromWire(reader.Require("applies_to")),
            reader.RequireBoolean("required"),
            visibility,
            numberFormat,
            categoryIds,
            targetKindIds,
            conditionDescription,
            visibilityCheckIds);
    }

    private static void ExpectPairing(bool applies, bool present, string keys, string when)
    {
        if (applies != present)
            throw new WireFaultException(
                "I-66", $"field_declaration: {keys} 必须{(applies ? "存在" : "不存在")}（当前 {when}）");
    }

    public WireObject ToWire()
    {
        var members = new List<KeyValuePair<string, WireValue>>
        {
            new("id", WireValue.Str(Id)),
            new("semantic_version", WireValue.Str(SemanticVersion)),
            new("description", WireValue.Str(Description)),
            new("value_type", WireValue.Str(ValueType)),
            new("applies_to", AppliesTo.ToWire()),
            new("required", WireValue.Bool(Required)),
            new("visibility", WireValue.Str(Visibility)),
        };
        if (NumberFormat is not null)
            members.Add(new KeyValuePair<string, WireValue>("number_format", WireValue.Str(NumberFormat)));
        if (CategoryIds is not null)
            members.Add(new KeyValuePair<string, WireValue>("category_ids", WireBuild.IdList(CategoryIds)));
        if (TargetKindIds is not null)
            members.Add(new KeyValuePair<string, WireValue>("target_kind_ids", WireBuild.IdList(TargetKindIds)));
        if (ConditionDescription is not null)
            members.Add(new KeyValuePair<string, WireValue>("condition_description", WireValue.Str(ConditionDescription)));
        if (VisibilityCheckIds is not null)
            members.Add(new KeyValuePair<string, WireValue>("visibility_check_ids", WireBuild.IdList(VisibilityCheckIds)));
        return new WireObject(members);
    }
}

public sealed record RelationTypeDeclarationDto(
    string Id,
    string SemanticVersion,
    string Description,
    IReadOnlyList<string> SourceKindIds,
    IReadOnlyList<string> TargetKindIds,
    PositionSchemaDto? PositionSchema)
{
    public static RelationTypeDeclarationDto FromWire(WireValue value)
    {
        var reader = new WireReader(value, "relation_type").Known(
            "id", "semantic_version", "description", "source_kind_ids", "target_kind_ids", "position_schema");
        var schemaNode = reader.Require("position_schema");
        return new RelationTypeDeclarationDto(
            reader.RequireNonEmptyString("id"),
            reader.RequireNonEmptyString("semantic_version"),
            reader.RequireString("description"),
            reader.RequireIdList("source_kind_ids"),
            reader.RequireIdList("target_kind_ids"),
            schemaNode is WireNull ? null : PositionSchemaDto.FromWire(schemaNode));
    }

    public WireObject ToWire() => WireValue.Object(
        ("id", WireValue.Str(Id)),
        ("semantic_version", WireValue.Str(SemanticVersion)),
        ("description", WireValue.Str(Description)),
        ("source_kind_ids", WireBuild.IdList(SourceKindIds)),
        ("target_kind_ids", WireBuild.IdList(TargetKindIds)),
        ("position_schema", PositionSchema is null ? WireValue.Null : PositionSchema.ToWire()));
}

public sealed record PileTypeDeclarationDto(
    string Id,
    string SemanticVersion,
    string Description,
    string KindId,
    IReadOnlyList<string> MemberKindIds,
    PositionSchemaDto? PositionSchema,
    string? ExclusiveGroupId,
    string? MembershipScopeFieldId)
{
    public static PileTypeDeclarationDto FromWire(WireValue value)
    {
        var reader = new WireReader(value, "pile_type").Known(
            "id", "semantic_version", "description", "kind_id", "member_kind_ids", "position_schema",
            "exclusive_group_id", "membership_scope_field_id");
        var schemaNode = reader.Require("position_schema");
        var exclusive = reader.Require("exclusive_group_id");
        var scope = reader.Require("membership_scope_field_id");
        return new PileTypeDeclarationDto(
            reader.RequireNonEmptyString("id"),
            reader.RequireNonEmptyString("semantic_version"),
            reader.RequireString("description"),
            reader.RequireNonEmptyString("kind_id"),
            reader.RequireIdList("member_kind_ids"),
            schemaNode is WireNull ? null : PositionSchemaDto.FromWire(schemaNode),
            exclusive is WireNull ? null : reader.RequireNonEmptyString("exclusive_group_id"),
            scope is WireNull ? null : reader.RequireNonEmptyString("membership_scope_field_id"));
    }

    public WireObject ToWire() => WireValue.Object(
        ("id", WireValue.Str(Id)),
        ("semantic_version", WireValue.Str(SemanticVersion)),
        ("description", WireValue.Str(Description)),
        ("kind_id", WireValue.Str(KindId)),
        ("member_kind_ids", WireBuild.IdList(MemberKindIds)),
        ("position_schema", PositionSchema is null ? WireValue.Null : PositionSchema.ToWire()),
        ("exclusive_group_id", WireBuild.NullableString(ExclusiveGroupId)),
        ("membership_scope_field_id", WireBuild.NullableString(MembershipScopeFieldId)));
}

public sealed record SequenceTypeDeclarationDto(
    string Id,
    string SemanticVersion,
    string Description,
    IReadOnlyList<string> OwnerKindIds,
    IReadOnlyList<string> ElementKindIds,
    long MinCount,
    long? MaxCount)
{
    public static SequenceTypeDeclarationDto FromWire(WireValue value)
    {
        var reader = new WireReader(value, "sequence_type").Known(
            "id", "semantic_version", "description", "owner_kind_ids", "element_kind_ids", "min_count", "max_count");
        return new SequenceTypeDeclarationDto(
            reader.RequireNonEmptyString("id"),
            reader.RequireNonEmptyString("semantic_version"),
            reader.RequireString("description"),
            reader.RequireIdList("owner_kind_ids"),
            reader.RequireIdList("element_kind_ids"),
            reader.RequireSafeInteger("min_count"),
            reader.Require("max_count") is WireNull ? null : reader.RequireSafeInteger("max_count"));
    }

    public WireObject ToWire() => WireValue.Object(
        ("id", WireValue.Str(Id)),
        ("semantic_version", WireValue.Str(SemanticVersion)),
        ("description", WireValue.Str(Description)),
        ("owner_kind_ids", WireBuild.IdList(OwnerKindIds)),
        ("element_kind_ids", WireBuild.IdList(ElementKindIds)),
        ("min_count", WireValue.Integer(MinCount)),
        ("max_count", WireBuild.NullableInteger(MaxCount)));
}

public sealed record GroupTypeDeclarationDto(
    string Id,
    string SemanticVersion,
    string Description,
    IReadOnlyList<string> OwnerKindIds,
    IReadOnlyList<string> MemberKindIds,
    long MinCount,
    long? MaxCount)
{
    public static GroupTypeDeclarationDto FromWire(WireValue value)
    {
        var reader = new WireReader(value, "group_type").Known(
            "id", "semantic_version", "description", "owner_kind_ids", "member_kind_ids", "min_count", "max_count");
        return new GroupTypeDeclarationDto(
            reader.RequireNonEmptyString("id"),
            reader.RequireNonEmptyString("semantic_version"),
            reader.RequireString("description"),
            reader.RequireIdList("owner_kind_ids"),
            reader.RequireIdList("member_kind_ids"),
            reader.RequireSafeInteger("min_count"),
            reader.Require("max_count") is WireNull ? null : reader.RequireSafeInteger("max_count"));
    }

    public WireObject ToWire() => WireValue.Object(
        ("id", WireValue.Str(Id)),
        ("semantic_version", WireValue.Str(SemanticVersion)),
        ("description", WireValue.Str(Description)),
        ("owner_kind_ids", WireBuild.IdList(OwnerKindIds)),
        ("member_kind_ids", WireBuild.IdList(MemberKindIds)),
        ("min_count", WireValue.Integer(MinCount)),
        ("max_count", WireBuild.NullableInteger(MaxCount)));
}

public sealed record ActionTypeDeclarationDto(
    string Id,
    string SemanticVersion,
    string Description,
    IReadOnlyList<string> RequiredFields,
    IReadOnlyList<string> OptionalFields)
{
    public static ActionTypeDeclarationDto FromWire(WireValue value)
    {
        var reader = new WireReader(value, "action_type")
            .Known("id", "semantic_version", "description", "required_fields", "optional_fields");
        var required = reader.RequireIdList("required_fields");
        var optional = reader.RequireIdList("optional_fields");
        var overlap = required.Intersect(optional, StringComparer.Ordinal).ToList();
        if (overlap.Count > 0)
            throw new WireFaultException(
                "N-01-DTO", $"action_type: 同一字段不得同时为 required/optional: {string.Join(",", overlap)}");
        return new ActionTypeDeclarationDto(
            reader.RequireNonEmptyString("id"),
            reader.RequireNonEmptyString("semantic_version"),
            reader.RequireString("description"),
            required,
            optional);
    }

    public WireObject ToWire() => WireValue.Object(
        ("id", WireValue.Str(Id)),
        ("semantic_version", WireValue.Str(SemanticVersion)),
        ("description", WireValue.Str(Description)),
        ("required_fields", WireBuild.IdList(RequiredFields)),
        ("optional_fields", WireBuild.IdList(OptionalFields)));
}

public sealed record TerminalReasonDeclarationDto(
    string Id,
    string SemanticVersion,
    string Description,
    string Outcome,
    IReadOnlyList<string> RequiredFields,
    IReadOnlyList<string> OptionalFields)
{
    private static readonly string[] Outcomes = ["win", "loss", "other"];

    public static TerminalReasonDeclarationDto FromWire(WireValue value)
    {
        var reader = new WireReader(value, "terminal_reason").Known(
            "id", "semantic_version", "description", "outcome", "required_fields", "optional_fields");
        var outcome = reader.RequireString("outcome");
        if (!Outcomes.Contains(outcome, StringComparer.Ordinal))
            throw new WireFaultException("I-66", $"terminal_reason.outcome: 不在枚举 {string.Join('/', Outcomes)}");
        return new TerminalReasonDeclarationDto(
            reader.RequireNonEmptyString("id"),
            reader.RequireNonEmptyString("semantic_version"),
            reader.RequireString("description"),
            outcome,
            reader.RequireIdList("required_fields"),
            reader.RequireIdList("optional_fields"));
    }

    public WireObject ToWire() => WireValue.Object(
        ("id", WireValue.Str(Id)),
        ("semantic_version", WireValue.Str(SemanticVersion)),
        ("description", WireValue.Str(Description)),
        ("outcome", WireValue.Str(Outcome)),
        ("required_fields", WireBuild.IdList(RequiredFields)),
        ("optional_fields", WireBuild.IdList(OptionalFields)));
}

public sealed record DeclarationsDto(
    IReadOnlyList<EntityKindDeclarationDto> EntityKinds,
    IReadOnlyList<ContentTypeDeclarationDto> ContentTypes,
    IReadOnlyList<FieldDeclarationDto> Fields,
    IReadOnlyList<RelationTypeDeclarationDto> RelationTypes,
    IReadOnlyList<PileTypeDeclarationDto> PileTypes,
    IReadOnlyList<SequenceTypeDeclarationDto> SequenceTypes,
    IReadOnlyList<GroupTypeDeclarationDto> GroupTypes,
    IReadOnlyList<ActionTypeDeclarationDto> ActionTypes,
    IReadOnlyList<TerminalReasonDeclarationDto> TerminalReasons)
{
    public static DeclarationsDto FromWire(WireValue value)
    {
        var reader = new WireReader(value, "declarations").Known(
            "entity_kinds", "content_types", "fields", "relation_types", "pile_types",
            "sequence_types", "group_types", "action_types", "terminal_reasons");
        return new DeclarationsDto(
            reader.RequireArray("entity_kinds").Items.Select(EntityKindDeclarationDto.FromWire).ToList(),
            reader.RequireArray("content_types").Items.Select(ContentTypeDeclarationDto.FromWire).ToList(),
            reader.RequireArray("fields").Items.Select(FieldDeclarationDto.FromWire).ToList(),
            reader.RequireArray("relation_types").Items.Select(RelationTypeDeclarationDto.FromWire).ToList(),
            reader.RequireArray("pile_types").Items.Select(PileTypeDeclarationDto.FromWire).ToList(),
            reader.RequireArray("sequence_types").Items.Select(SequenceTypeDeclarationDto.FromWire).ToList(),
            reader.RequireArray("group_types").Items.Select(GroupTypeDeclarationDto.FromWire).ToList(),
            reader.RequireArray("action_types").Items.Select(ActionTypeDeclarationDto.FromWire).ToList(),
            reader.RequireArray("terminal_reasons").Items.Select(TerminalReasonDeclarationDto.FromWire).ToList());
    }

    public WireObject ToWire() => WireValue.Object(
        ("entity_kinds", new WireArray(EntityKinds.Select(item => (WireValue)item.ToWire()))),
        ("content_types", new WireArray(ContentTypes.Select(item => (WireValue)item.ToWire()))),
        ("fields", new WireArray(Fields.Select(item => (WireValue)item.ToWire()))),
        ("relation_types", new WireArray(RelationTypes.Select(item => (WireValue)item.ToWire()))),
        ("pile_types", new WireArray(PileTypes.Select(item => (WireValue)item.ToWire()))),
        ("sequence_types", new WireArray(SequenceTypes.Select(item => (WireValue)item.ToWire()))),
        ("group_types", new WireArray(GroupTypes.Select(item => (WireValue)item.ToWire()))),
        ("action_types", new WireArray(ActionTypes.Select(item => (WireValue)item.ToWire()))),
        ("terminal_reasons", new WireArray(TerminalReasons.Select(item => (WireValue)item.ToWire()))));
}

/// <summary>公开目录：声明结构，不含效果代码或隐藏资料（protocol.md §1 第 28–32 行）。</summary>
public sealed record PublicCatalogDto(
    string ProtocolVersion,
    string RulesetId,
    string CatalogId,
    DeclarationsDto Declarations)
{
    public static PublicCatalogDto FromWire(WireValue value) => FromWire(value, NetProtocol.V01);

    public static PublicCatalogDto FromWire(WireValue value, string protocolVersion)
    {
        NetProtocol.RequireKnown(protocolVersion, "public_catalog.protocol_version（运行时声明）");
        var reader = new WireReader(value, "public_catalog")
            .Known("protocol_version", "ruleset_id", "catalog_id", "declarations");
        var protocol = reader.Require("protocol_version");
        if (protocol is not WireString version || version.Value != protocolVersion)
            throw new WireFaultException("I-66",
                $"public_catalog.protocol_version: 本版要求完全相等 '{protocolVersion}'");
        return new PublicCatalogDto(
            protocolVersion,
            reader.RequireNonEmptyString("ruleset_id"),
            reader.RequireNonEmptyString("catalog_id"),
            DeclarationsDto.FromWire(reader.Require("declarations")));
    }

    public WireObject ToWire() => WireValue.Object(
        ("protocol_version", WireValue.Str(ProtocolVersion)),
        ("ruleset_id", WireValue.Str(RulesetId)),
        ("catalog_id", WireValue.Str(CatalogId)),
        ("declarations", Declarations.ToWire()));

    /// <summary>目录指纹：直接复用规范身份白名单（不另写一套摘要输入）。</summary>
    public string Digest() => WireIdentity.CatalogDigest(ToWire());
}
