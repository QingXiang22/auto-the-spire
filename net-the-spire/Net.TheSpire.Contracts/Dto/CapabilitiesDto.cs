namespace NetTheSpire.Contracts.Dto;

public sealed record ScenarioProfileDto(
    string Id,
    string SemanticVersion,
    IReadOnlyList<string> RequiredDeclarations,
    IReadOnlyList<string> SupportedMechanismIds,
    IReadOnlyList<string> UnsupportedConstraints,
    string CoverageRef)
{
    public static ScenarioProfileDto FromWire(WireValue value)
    {
        var reader = new WireReader(value, "scenario_profile").Known(
            "id", "semantic_version", "required_declarations", "supported_mechanism_ids",
            "unsupported_constraints", "coverage_ref");
        return new ScenarioProfileDto(
            reader.RequireNonEmptyString("id"),
            reader.RequireNonEmptyString("semantic_version"),
            reader.RequireStringList("required_declarations"),
            reader.RequireStringList("supported_mechanism_ids"),
            reader.RequireStringList("unsupported_constraints"),
            reader.RequireNonEmptyString("coverage_ref"));
    }

    public WireObject ToWire() => WireValue.Object(
        ("id", WireValue.Str(Id)),
        ("semantic_version", WireValue.Str(SemanticVersion)),
        ("required_declarations", WireBuild.StringList(RequiredDeclarations)),
        ("supported_mechanism_ids", WireBuild.StringList(SupportedMechanismIds)),
        ("unsupported_constraints", WireBuild.StringList(UnsupportedConstraints)),
        ("coverage_ref", WireValue.Str(CoverageRef)));
}

public sealed record SupportedStructureDto(
    string Structure,
    IReadOnlyList<string> DeclarationIds,
    IReadOnlyList<string> UnsupportedConstraints)
{
    private static readonly string[] Structures =
        ["Entity", "Relation", "Pile", "Sequence", "Group", "LegalAction", "TerminalResult"];

    public static SupportedStructureDto FromWire(WireValue value)
    {
        var reader = new WireReader(value, "supported_structure")
            .Known("structure", "declaration_ids", "unsupported_constraints");
        var structure = reader.RequireNonEmptyString("structure");
        if (!Structures.Contains(structure, StringComparer.Ordinal))
            throw new WireFaultException(
                "I-66", $"supported_structure.structure: 不在枚举 {string.Join('/', Structures)}");
        return new SupportedStructureDto(
            structure,
            reader.RequireStringList("declaration_ids"),
            reader.RequireStringList("unsupported_constraints"));
    }

    public WireObject ToWire() => WireValue.Object(
        ("structure", WireValue.Str(Structure)),
        ("declaration_ids", WireBuild.StringList(DeclarationIds)),
        ("unsupported_constraints", WireBuild.StringList(UnsupportedConstraints)));
}

/// <summary>
/// 执行保证：重放/暂停/恢复键只在这里，不在 capabilities 顶层重复
/// （protocol.md §1 第 52–54 行；contract-artifacts §4 第 49 行）。
/// </summary>
public sealed record ExecutionGuaranteesDto(
    bool StableSnapshot,
    bool RequestStatus,
    bool ActiveDeduplication,
    bool DecisionPause,
    bool InternalSnapshotRestore,
    string SeedActionReplay,
    bool PublicStateRestore,
    bool PublicFork,
    string? RngProfileId)
{
    private static readonly string[] ReplayModes = ["none", "public_trace"];

    public static ExecutionGuaranteesDto FromWire(WireValue value)
    {
        var reader = new WireReader(value, "execution_guarantees").Known(
            "stable_snapshot", "request_status", "active_deduplication", "decision_pause",
            "internal_snapshot_restore", "seed_action_replay", "public_state_restore", "public_fork", "rng_profile_id");
        var replay = reader.RequireString("seed_action_replay");
        if (!ReplayModes.Contains(replay, StringComparer.Ordinal))
            throw new WireFaultException(
                "I-66", $"execution_guarantees.seed_action_replay: 不在枚举 {string.Join('/', ReplayModes)}");
        var publicRestore = reader.RequireBoolean("public_state_restore");
        var publicFork = reader.RequireBoolean("public_fork");
        if (publicRestore || publicFork)
            throw new WireFaultException(
                "N-01-DTO", "首版 public_state_restore/public_fork 必须为 false（protocol.md §4 第 189 行）");
        var rng = reader.Require("rng_profile_id");
        return new ExecutionGuaranteesDto(
            reader.RequireBoolean("stable_snapshot"),
            reader.RequireBoolean("request_status"),
            reader.RequireBoolean("active_deduplication"),
            reader.RequireBoolean("decision_pause"),
            reader.RequireBoolean("internal_snapshot_restore"),
            replay,
            publicRestore,
            publicFork,
            rng is WireNull ? null : reader.RequireNonEmptyString("rng_profile_id"));
    }

    public WireObject ToWire() => WireValue.Object(
        ("stable_snapshot", WireValue.Bool(StableSnapshot)),
        ("request_status", WireValue.Bool(RequestStatus)),
        ("active_deduplication", WireValue.Bool(ActiveDeduplication)),
        ("decision_pause", WireValue.Bool(DecisionPause)),
        ("internal_snapshot_restore", WireValue.Bool(InternalSnapshotRestore)),
        ("seed_action_replay", WireValue.Str(SeedActionReplay)),
        ("public_state_restore", WireValue.Bool(PublicStateRestore)),
        ("public_fork", WireValue.Bool(PublicFork)),
        ("rng_profile_id", WireBuild.NullableString(RngProfileId)));
}

/// <summary>协议字段命名的运行限额（contract-artifacts §4 第 51 行）。</summary>
public sealed record LimitsDto(
    long Entities,
    long EntityFields,
    long Relations,
    long Piles,
    long VisibleMembersPerPile,
    long KnownPositionsPerPile,
    long Sequences,
    long ElementsPerSequence,
    long Groups,
    long MembersPerGroup,
    long LegalActions,
    long ActionFields,
    long FrameBytes,
    long DecimalChars,
    long LedgerEntries,
    long Sessions)
{
    public static readonly string[] KeyNames =
    [
        "entities", "entity_fields", "relations", "piles", "visible_members_per_pile", "known_positions_per_pile",
        "sequences", "elements_per_sequence", "groups", "members_per_group", "legal_actions", "action_fields",
        "frame_bytes", "decimal_chars", "ledger_entries", "sessions",
    ];

    public static LimitsDto FromWire(WireValue value)
    {
        var reader = new WireReader(value, "limits").Known(KeyNames);
        return new LimitsDto(
            reader.RequireSafeInteger("entities"),
            reader.RequireSafeInteger("entity_fields"),
            reader.RequireSafeInteger("relations"),
            reader.RequireSafeInteger("piles"),
            reader.RequireSafeInteger("visible_members_per_pile"),
            reader.RequireSafeInteger("known_positions_per_pile"),
            reader.RequireSafeInteger("sequences"),
            reader.RequireSafeInteger("elements_per_sequence"),
            reader.RequireSafeInteger("groups"),
            reader.RequireSafeInteger("members_per_group"),
            reader.RequireSafeInteger("legal_actions"),
            reader.RequireSafeInteger("action_fields"),
            reader.RequireSafeInteger("frame_bytes"),
            reader.RequireSafeInteger("decimal_chars"),
            reader.RequireSafeInteger("ledger_entries"),
            reader.RequireSafeInteger("sessions"));
    }

    public WireObject ToWire() => WireValue.Object(
        ("entities", WireValue.Integer(Entities)),
        ("entity_fields", WireValue.Integer(EntityFields)),
        ("relations", WireValue.Integer(Relations)),
        ("piles", WireValue.Integer(Piles)),
        ("visible_members_per_pile", WireValue.Integer(VisibleMembersPerPile)),
        ("known_positions_per_pile", WireValue.Integer(KnownPositionsPerPile)),
        ("sequences", WireValue.Integer(Sequences)),
        ("elements_per_sequence", WireValue.Integer(ElementsPerSequence)),
        ("groups", WireValue.Integer(Groups)),
        ("members_per_group", WireValue.Integer(MembersPerGroup)),
        ("legal_actions", WireValue.Integer(LegalActions)),
        ("action_fields", WireValue.Integer(ActionFields)),
        ("frame_bytes", WireValue.Integer(FrameBytes)),
        ("decimal_chars", WireValue.Integer(DecimalChars)),
        ("ledger_entries", WireValue.Integer(LedgerEntries)),
        ("sessions", WireValue.Integer(Sessions)));
}

/// <summary>capabilities 八个键全部必填并拒绝其他键（contract-artifacts §4 第 47 行）。</summary>
public sealed record CapabilitiesDto(
    string BackendKind,
    IReadOnlyList<string> Operations,
    IReadOnlyList<ScenarioProfileDto> ScenarioProfiles,
    bool SeedControl,
    IReadOnlyDictionary<string, string> FieldAvailability,
    IReadOnlyList<SupportedStructureDto> SupportedStructures,
    LimitsDto Limits,
    ExecutionGuaranteesDto ExecutionGuarantees)
{
    private static readonly string[] KnownOperations =
        ["describe", "get_catalog", "start", "attach", "observe", "step", "request_status", "close"];

    private static readonly string[] Availability = ["available", "unavailable"];

    public static CapabilitiesDto FromWire(WireValue value)
    {
        var reader = new WireReader(value, "capabilities").Known(
            "backend_kind", "operations", "scenario_profiles", "seed_control", "field_availability",
            "supported_structures", "limits", "execution_guarantees");
        var backendKind = reader.RequireString("backend_kind");
        if (backendKind is not ("simulation" or "real_game"))
            throw new WireFaultException("I-66", "capabilities.backend_kind: 只能是 simulation/real_game");
        var operations = reader.RequireStringList("operations");
        foreach (var operation in operations)
        {
            if (!KnownOperations.Contains(operation, StringComparer.Ordinal))
                throw new WireFaultException("I-66", $"capabilities.operations: 未知操作 '{operation}'");
        }

        var duplicates = operations.GroupBy(item => item, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();
        if (duplicates.Count > 0)
            throw new WireFaultException(
                "N-01-DTO", $"capabilities.operations: 不得重复: {string.Join(",", duplicates)}");

        var availabilityReader = new WireReader(reader.Require("field_availability"), "field_availability");
        var availability = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var member in availabilityReader.Raw.Members)
        {
            if (member.Key.Length == 0)
                throw new WireFaultException("I-66", "field_availability: 键必须是非空 field_id");
            if (member.Value is not WireString state || !Availability.Contains(state.Value, StringComparer.Ordinal))
                throw new WireFaultException(
                    "I-66", $"field_availability.{member.Key}: 只能是 available/unavailable");
            availability[member.Key] = state.Value;
        }

        return new CapabilitiesDto(
            backendKind,
            operations,
            reader.RequireArray("scenario_profiles").Items.Select(ScenarioProfileDto.FromWire).ToList(),
            reader.RequireBoolean("seed_control"),
            availability,
            reader.RequireArray("supported_structures").Items.Select(SupportedStructureDto.FromWire).ToList(),
            LimitsDto.FromWire(reader.Require("limits")),
            ExecutionGuaranteesDto.FromWire(reader.Require("execution_guarantees")));
    }

    public WireObject ToWire() => WireValue.Object(
        ("backend_kind", WireValue.Str(BackendKind)),
        ("operations", WireBuild.StringList(Operations)),
        ("scenario_profiles", new WireArray(ScenarioProfiles.Select(item => (WireValue)item.ToWire()))),
        ("seed_control", WireValue.Bool(SeedControl)),
        ("field_availability", new WireObject(
            FieldAvailability.Select(pair => new KeyValuePair<string, WireValue>(pair.Key, WireValue.Str(pair.Value))))),
        ("supported_structures", new WireArray(SupportedStructures.Select(item => (WireValue)item.ToWire()))),
        ("limits", Limits.ToWire()),
        ("execution_guarantees", ExecutionGuarantees.ToWire()));
}

public sealed record DescribeResultDto(IdentityDto Identity, CapabilitiesDto Capabilities)
{
    public static DescribeResultDto FromWire(WireValue value)
    {
        var reader = new WireReader(value, "describe_result").Known("identity", "capabilities");
        return new DescribeResultDto(
            IdentityDto.FromWire(reader.Require("identity")),
            CapabilitiesDto.FromWire(reader.Require("capabilities")));
    }

    public WireObject ToWire() => WireValue.Object(
        ("identity", Identity.ToWire()),
        ("capabilities", Capabilities.ToWire()));
}
