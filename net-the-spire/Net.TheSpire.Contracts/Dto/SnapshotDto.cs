namespace NetTheSpire.Contracts.Dto;

/// <summary>实体字段 <c>{field_id, state, value?}</c>；known 必须有值，其余不带值。</summary>
public sealed record FieldDto(string FieldId, string State, WireValue? Value)
{
    private static readonly string[] States = ["known", "unknown", "unavailable"];

    public static FieldDto FromWire(WireValue value)
    {
        var reader = new WireReader(value, "field").Known("field_id", "state", "value");
        var state = reader.RequireString("state");
        if (!States.Contains(state, StringComparer.Ordinal))
            throw new WireFaultException("I-66", $"field.state: 不在枚举 {string.Join('/', States)}");
        var payload = reader.Optional("value");
        if (state == "known")
        {
            if (payload is null) throw new WireFaultException("I-66", "field: state=known 必须带 value");
            ValidateValue(payload);
        }
        else if (payload is not null)
        {
            throw new WireFaultException("I-66", $"field: state={state} 不得带 value");
        }

        return new FieldDto(reader.RequireNonEmptyString("field_id"), state, payload);
    }

    private static void ValidateValue(WireValue payload)
    {
        switch (payload)
        {
            case WireBool:
                return;
            // 字符串值同时承载 number/category/entity_ref 的规范字符串与 0.2 的 string 值类型。
            // 0.2 的 string 值类型按“原样文本”编码，空串是合法的公开文本（不代表缺失，缺失用 state 表达）。
            case WireString:
                return;
            default:
                throw new WireFaultException(
                    "I-66", $"field.value: 只接受规范十进制字符串/类别 ID/entity_ref/boolean/string，得到 {payload.TypeName}");
        }
    }

    public WireObject ToWire()
    {
        var members = new List<KeyValuePair<string, WireValue>>
        {
            new("field_id", WireValue.Str(FieldId)),
            new("state", WireValue.Str(State)),
        };
        if (Value is not null) members.Add(new KeyValuePair<string, WireValue>("value", Value));
        return new WireObject(members);
    }
}

/// <summary>公开实体 <c>{instance_id, kind_id, content_state, fields, content_id?}</c>。</summary>
public sealed record EntityDto(
    string InstanceId,
    string KindId,
    string ContentState,
    IReadOnlyList<FieldDto> Fields,
    string? ContentId)
{
    private static readonly string[] States = ["known", "unknown", "unavailable", "not_applicable"];

    public static EntityDto FromWire(WireValue value)
    {
        var reader = new WireReader(value, "entity")
            .Known("instance_id", "kind_id", "content_state", "fields", "content_id");
        var contentState = reader.RequireString("content_state");
        if (!States.Contains(contentState, StringComparer.Ordinal))
            throw new WireFaultException("I-66", $"entity.content_state: 不在枚举 {string.Join('/', States)}");
        var contentId = reader.OptionalNonEmptyString("content_id");
        if (contentState == "known" && contentId is null)
            throw new WireFaultException("I-66", "entity: content_state=known 必须带 content_id");
        if (contentState != "known" && contentId is not null)
            throw new WireFaultException("I-66", $"entity: content_state={contentState} 不得带 content_id");
        return new EntityDto(
            reader.RequireNonEmptyString("instance_id"),
            reader.RequireNonEmptyString("kind_id"),
            contentState,
            reader.RequireArray("fields").Items.Select(FieldDto.FromWire).ToList(),
            contentId);
    }

    public WireObject ToWire()
    {
        var members = new List<KeyValuePair<string, WireValue>>
        {
            new("instance_id", WireValue.Str(InstanceId)),
            new("kind_id", WireValue.Str(KindId)),
            new("content_state", WireValue.Str(ContentState)),
            new("fields", new WireArray(Fields.Select(field => (WireValue)field.ToWire()))),
        };
        if (ContentId is not null) members.Add(new KeyValuePair<string, WireValue>("content_id", WireValue.Str(ContentId)));
        return new WireObject(members);
    }
}

public sealed record RelationDto(string TypeId, string SourceRef, string TargetRef, long? Position)
{
    public static RelationDto FromWire(WireValue value)
    {
        var reader = new WireReader(value, "relation").Known("type_id", "source_ref", "target_ref", "position");
        return new RelationDto(
            reader.RequireNonEmptyString("type_id"),
            reader.RequireNonEmptyString("source_ref"),
            reader.RequireNonEmptyString("target_ref"),
            reader.Has("position") ? reader.RequireSafeInteger("position") : null);
    }

    public WireObject ToWire()
    {
        var members = new List<KeyValuePair<string, WireValue>>
        {
            new("type_id", WireValue.Str(TypeId)),
            new("source_ref", WireValue.Str(SourceRef)),
            new("target_ref", WireValue.Str(TargetRef)),
        };
        if (Position is not null)
            members.Add(new KeyValuePair<string, WireValue>("position", WireValue.Integer(Position.Value)));
        return new WireObject(members);
    }
}

public sealed record CardPositionDto(string CardRef, long Position)
{
    public static CardPositionDto FromWire(WireValue value)
    {
        var reader = new WireReader(value, "known_position").Known("card_ref", "position");
        return new CardPositionDto(reader.RequireNonEmptyString("card_ref"), reader.RequireSafeInteger("position"));
    }

    public WireObject ToWire() => WireValue.Object(
        ("card_ref", WireValue.Str(CardRef)),
        ("position", WireValue.Integer(Position)));
}

public sealed record PileDto(
    string PileRef,
    long TotalCount,
    IReadOnlyList<string> VisibleMembers,
    bool MembersComplete,
    IReadOnlyList<CardPositionDto> KnownPositions)
{
    public static PileDto FromWire(WireValue value)
    {
        var reader = new WireReader(value, "pile")
            .Known("pile_ref", "total_count", "visible_members", "members_complete", "known_positions");
        return new PileDto(
            reader.RequireNonEmptyString("pile_ref"),
            reader.RequireSafeInteger("total_count"),
            reader.RequireIdList("visible_members"),
            reader.RequireBoolean("members_complete"),
            reader.RequireArray("known_positions").Items.Select(CardPositionDto.FromWire).ToList());
    }

    public WireObject ToWire() => WireValue.Object(
        ("pile_ref", WireValue.Str(PileRef)),
        ("total_count", WireValue.Integer(TotalCount)),
        ("visible_members", WireBuild.IdList(VisibleMembers)),
        ("members_complete", WireValue.Bool(MembersComplete)),
        ("known_positions", new WireArray(KnownPositions.Select(item => (WireValue)item.ToWire()))));
}

public sealed record SequenceDto(string TypeId, string OwnerRef, IReadOnlyList<string> Elements)
{
    public static SequenceDto FromWire(WireValue value)
    {
        var reader = new WireReader(value, "sequence").Known("type_id", "owner_ref", "elements");
        return new SequenceDto(
            reader.RequireNonEmptyString("type_id"),
            reader.RequireNonEmptyString("owner_ref"),
            reader.RequireIdList("elements"));
    }

    public WireObject ToWire() => WireValue.Object(
        ("type_id", WireValue.Str(TypeId)),
        ("owner_ref", WireValue.Str(OwnerRef)),
        ("elements", WireBuild.IdList(Elements)));
}

public sealed record GroupMemberDto(string EntityRef, long Multiplicity)
{
    public static GroupMemberDto FromWire(WireValue value)
    {
        var reader = new WireReader(value, "group_member").Known("entity_ref", "multiplicity");
        var multiplicity = reader.RequireSafeInteger("multiplicity");
        if (multiplicity < 1)
            throw new WireFaultException("I-66", "group_member.multiplicity: 必须是正整数");
        return new GroupMemberDto(reader.RequireNonEmptyString("entity_ref"), multiplicity);
    }

    public WireObject ToWire() => WireValue.Object(
        ("entity_ref", WireValue.Str(EntityRef)),
        ("multiplicity", WireValue.Integer(Multiplicity)));
}

public sealed record GroupDto(string TypeId, string OwnerRef, IReadOnlyList<GroupMemberDto> Members)
{
    public static GroupDto FromWire(WireValue value)
    {
        var reader = new WireReader(value, "group").Known("type_id", "owner_ref", "members");
        var members = reader.RequireArray("members").Items.Select(GroupMemberDto.FromWire).ToList();
        var duplicates = members.GroupBy(member => member.EntityRef, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();
        if (duplicates.Count > 0)
            throw new WireFaultException("N-01-DTO", $"group: 成员引用必须唯一，重复: {string.Join(",", duplicates)}");
        return new GroupDto(reader.RequireNonEmptyString("type_id"), reader.RequireNonEmptyString("owner_ref"), members);
    }

    public WireObject ToWire() => WireValue.Object(
        ("type_id", WireValue.Str(TypeId)),
        ("owner_ref", WireValue.Str(OwnerRef)),
        ("members", new WireArray(Members.Select(member => (WireValue)member.ToWire()))));
}

public sealed record LegalActionDto(string CandidateId, string ActionType, IReadOnlyList<FieldDto> Fields)
{
    public static LegalActionDto FromWire(WireValue value)
    {
        var reader = new WireReader(value, "legal_action").Known("candidate_id", "action_type", "fields");
        var fields = reader.RequireArray("fields").Items.Select(FieldDto.FromWire).ToList();
        foreach (var field in fields)
        {
            if (field.State != "known")
                throw new WireFaultException(
                    "N-01-DTO", $"legal_action.fields: 所有执行所需字段必须 known（{field.FieldId} 为 {field.State}）");
        }

        return new LegalActionDto(
            reader.RequireNonEmptyString("candidate_id"),
            reader.RequireNonEmptyString("action_type"),
            fields);
    }

    public WireObject ToWire() => WireValue.Object(
        ("candidate_id", WireValue.Str(CandidateId)),
        ("action_type", WireValue.Str(ActionType)),
        ("fields", new WireArray(Fields.Select(field => (WireValue)field.ToWire()))));
}

public sealed record TerminalResultDto(string Outcome, string ReasonId, IReadOnlyList<FieldDto> Facts)
{
    private static readonly string[] Outcomes = ["win", "loss", "other"];

    public static TerminalResultDto FromWire(WireValue value)
    {
        var reader = new WireReader(value, "terminal_result").Known("outcome", "reason_id", "facts");
        var outcome = reader.RequireString("outcome");
        if (!Outcomes.Contains(outcome, StringComparer.Ordinal))
            throw new WireFaultException("I-66", $"terminal_result.outcome: 不在枚举 {string.Join('/', Outcomes)}");
        var facts = reader.RequireArray("facts").Items.Select(FieldDto.FromWire).ToList();
        var duplicates = facts.GroupBy(fact => fact.FieldId, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();
        if (duplicates.Count > 0)
            throw new WireFaultException(
                "N-01-DTO", $"terminal_result.facts: field_id 不得重复: {string.Join(",", duplicates)}");
        return new TerminalResultDto(outcome, reader.RequireNonEmptyString("reason_id"), facts);
    }

    public WireObject ToWire() => WireValue.Object(
        ("outcome", WireValue.Str(Outcome)),
        ("reason_id", WireValue.Str(ReasonId)),
        ("facts", new WireArray(Facts.Select(fact => (WireValue)fact.ToWire()))));
}

/// <summary>公开快照（protocol.md §2 第 60–104 行）。</summary>
public sealed record PublicSnapshotDto(
    string ProtocolVersion,
    string RulesetId,
    string CatalogId,
    string BackendProfileId,
    string SessionId,
    long ObservationId,
    string Status,
    TerminalResultDto? TerminalResult,
    IReadOnlyList<EntityDto> Entities,
    IReadOnlyList<RelationDto> Relations,
    IReadOnlyList<PileDto> Piles,
    IReadOnlyList<SequenceDto> Sequences,
    IReadOnlyList<GroupDto> Groups,
    IReadOnlyList<LegalActionDto> LegalActions)
{
    public static PublicSnapshotDto FromWire(WireValue value) => FromWire(value, NetProtocol.V02);

    public static PublicSnapshotDto FromWire(WireValue value, string protocolVersion)
    {
        NetProtocol.RequireKnown(protocolVersion, "public_snapshot.protocol_version（运行时声明）");
        var reader = new WireReader(value, "public_snapshot").Known(
            "protocol_version", "ruleset_id", "catalog_id", "backend_profile_id", "session_id", "observation_id",
            "status", "terminal_result", "entities", "relations", "piles", "sequences", "groups", "legal_actions");
        var protocol = reader.Require("protocol_version");
        if (protocol is not WireString version || version.Value != protocolVersion)
            throw new WireFaultException("I-66",
                $"public_snapshot.protocol_version: 本版要求完全相等 '{protocolVersion}'");
        var status = reader.RequireString("status");
        if (status is not ("decision" or "terminal"))
            throw new WireFaultException("I-66", "public_snapshot.status: 只能是 decision/terminal");
        var terminalNode = reader.Require("terminal_result");
        var terminal = terminalNode is WireNull ? null : TerminalResultDto.FromWire(terminalNode);
        var legalActions = reader.RequireArray("legal_actions").Items.Select(LegalActionDto.FromWire).ToList();

        if (status == "decision")
        {
            if (terminal is not null)
                throw new WireFaultException("N-01-DTO", "decision 快照的 terminal_result 必须为 null");
            if (legalActions.Count == 0)
                throw new WireFaultException("N-01-DTO", "decision 快照的候选不得为空");
        }
        else
        {
            if (terminal is null) throw new WireFaultException("N-01-DTO", "terminal 快照必须有 terminal_result");
            if (legalActions.Count != 0) throw new WireFaultException("N-01-DTO", "terminal 快照的候选必须为空");
        }

        return new PublicSnapshotDto(
            protocolVersion,
            reader.RequireNonEmptyString("ruleset_id"),
            reader.RequireNonEmptyString("catalog_id"),
            reader.RequireNonEmptyString("backend_profile_id"),
            reader.RequireNonEmptyString("session_id"),
            reader.RequireSafeInteger("observation_id"),
            status,
            terminal,
            reader.RequireArray("entities").Items.Select(EntityDto.FromWire).ToList(),
            reader.RequireArray("relations").Items.Select(RelationDto.FromWire).ToList(),
            reader.RequireArray("piles").Items.Select(PileDto.FromWire).ToList(),
            reader.RequireArray("sequences").Items.Select(SequenceDto.FromWire).ToList(),
            reader.RequireArray("groups").Items.Select(GroupDto.FromWire).ToList(),
            legalActions);
    }

    public WireObject ToWire() => WireValue.Object(
        ("protocol_version", WireValue.Str(ProtocolVersion)),
        ("ruleset_id", WireValue.Str(RulesetId)),
        ("catalog_id", WireValue.Str(CatalogId)),
        ("backend_profile_id", WireValue.Str(BackendProfileId)),
        ("session_id", WireValue.Str(SessionId)),
        ("observation_id", WireValue.Integer(ObservationId)),
        ("status", WireValue.Str(Status)),
        ("terminal_result", TerminalResult is null ? WireValue.Null : TerminalResult.ToWire()),
        ("entities", new WireArray(Entities.Select(item => (WireValue)item.ToWire()))),
        ("relations", new WireArray(Relations.Select(item => (WireValue)item.ToWire()))),
        ("piles", new WireArray(Piles.Select(item => (WireValue)item.ToWire()))),
        ("sequences", new WireArray(Sequences.Select(item => (WireValue)item.ToWire()))),
        ("groups", new WireArray(Groups.Select(item => (WireValue)item.ToWire()))),
        ("legal_actions", new WireArray(LegalActions.Select(item => (WireValue)item.ToWire()))));

    /// <summary>快照存储摘要（contract-artifacts §2 第 31 行）。</summary>
    public string StorageDigest() => WireIdentity.SnapshotStorageDigest(ToWire());
}
