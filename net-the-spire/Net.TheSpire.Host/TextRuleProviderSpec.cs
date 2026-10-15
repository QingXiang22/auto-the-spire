using NetTheSpire.Contracts;
using NetTheSpire.Projection;

namespace NetTheSpire.Host;

/// <summary>
/// 通用文本规则提供方的「不可推导元数据」描述文件（design.md §7.1 / §7.3 第 4 项）。
/// </summary>
/// <remarks>
/// 只允许四类不可推导元数据：内容 ID 目录、集合用途、终局胜负解释、已知位置角色。其余公开出口
/// （实体种类、公开字段与其类型/参数、合法动作、终局字段）一律由 RuleSet 与所选投影派生，本文件
/// 不列普通字段/动作/参数/枚举/引用。文件里出现本实现未支持的结构时显式抛错，不静默丢弃公开事实。
/// </remarks>
internal sealed record TextRuleProviderSpec(
    string FixtureFormat,
    string ProjectionId,
    string ParticipantRole,
    string ScenarioProfile,
    string DefaultGameSeed,
    PublicBuildMetadata Metadata)
{
    public const string SupportedFormat = "net-the-spire/declaration-extension-metadata/1";

    private static readonly string[] KnownKeys =
    [
        "fixture_format",
        "static_build_id",
        "scenario_profile",
        "projection_id",
        "participant_role",
        "default_game_seed",
        "content",
        "collections",
        "terminal_outcomes",
        "known_positions",
    ];

    /// <summary>读取并用仓库自带的严格 JSON 解析器解析描述文件；不使用反射、不引入新依赖。</summary>
    public static TextRuleProviderSpec Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var resolved = Path.GetFullPath(path);
        if (!File.Exists(resolved))
        {
            throw new ArgumentException($"找不到提供方描述文件：{resolved}", nameof(path));
        }

        var root = WireParser.ParseUtf8Strict(File.ReadAllBytes(resolved), "public-metadata");
        var document = RequireObject(root, "public-metadata");
        var what = $"public-metadata {resolved}";

        foreach (var member in document.Members)
        {
            if (!KnownKeys.Contains(member.Key, StringComparer.Ordinal))
            {
                throw new NotSupportedException(
                    $"{what}: 不支持的顶层键 '{member.Key}'（只允许 {string.Join('/', KnownKeys)}）："
                    + "提供方描述文件不承载普通字段/动作/参数，也不接受未知结构");
            }
        }

        var format = RequireString(document, "fixture_format", what);
        if (!string.Equals(format, SupportedFormat, StringComparison.Ordinal))
        {
            throw new NotSupportedException($"{what}: 不支持的 fixture_format '{format}'（本提供方只认 {SupportedFormat}）");
        }

        var staticBuildId = RequireString(document, "static_build_id", what);
        var projectionId = RequireString(document, "projection_id", what);
        var participantRole = RequireString(document, "participant_role", what);
        var scenarioProfile = RequireString(document, "scenario_profile", what);
        var defaultGameSeed = RequireString(document, "default_game_seed", what);

        var content = ReadContent(Optional(document, "content"), what);
        var collections = ReadCollections(Optional(document, "collections"), what);
        var terminalOutcomes = ReadTerminalOutcomes(Optional(document, "terminal_outcomes"), what);
        var knownPositions = ReadKnownPositions(Optional(document, "known_positions"), what);

        return new TextRuleProviderSpec(
            format,
            projectionId,
            participantRole,
            scenarioProfile,
            defaultGameSeed,
            PublicBuildMetadata.Create(staticBuildId, content, collections, terminalOutcomes, knownPositions));
    }

    private static IReadOnlyDictionary<string, PublicContentRule> ReadContent(WireValue? node, string what)
    {
        var result = new Dictionary<string, PublicContentRule>(StringComparer.Ordinal);
        if (node is null)
        {
            return result;
        }

        foreach (var member in RequireObject(node, $"{what} content").Members)
        {
            var entry = RequireObject(member.Value, $"{what} content.{member.Key}");
            var fieldName = RequireString(entry, "field_name", $"{what} content.{member.Key}");
            var ids = RequireStringList(entry, "content_ids", $"{what} content.{member.Key}");
            result[member.Key] = new PublicContentRule(fieldName, ids);
        }

        return result;
    }

    private static IReadOnlyDictionary<string, PublicCollectionRule> ReadCollections(WireValue? node, string what)
    {
        var result = new Dictionary<string, PublicCollectionRule>(StringComparer.Ordinal);
        if (node is null)
        {
            return result;
        }

        foreach (var member in RequireObject(node, $"{what} collections").Members)
        {
            var entry = RequireObject(member.Value, $"{what} collections.{member.Key}");
            var use = RequireString(entry, "use", $"{what} collections.{member.Key}");
            result[member.Key] = use switch
            {
                "pile" => PublicCollectionRule.Pile(
                    RequireString(entry, "pile_kind_type_id", $"{what} collections.{member.Key}"),
                    ReadPosition(Optional(entry, "position"), what)),
                "group" => PublicCollectionRule.Group(
                    RequireString(entry, "owner_kind_type_id", $"{what} collections.{member.Key}")),
                "sequence" => PublicCollectionRule.Sequence(
                    RequireString(entry, "owner_kind_type_id", $"{what} collections.{member.Key}")),
                "not_published" => PublicCollectionRule.NotPublished(
                    RequireString(entry, "rule_id", $"{what} collections.{member.Key}"),
                    RequireString(entry, "reason", $"{what} collections.{member.Key}")),
                _ => throw new NotSupportedException(
                    $"{what} collections.{member.Key}: 不支持的集合用途 '{use}'"
                    + "（只允许 not_published/pile/group/sequence）：不静默丢弃公开事实"),
            };
        }

        return result;
    }

    private static PublicPositionSchema? ReadPosition(WireValue? node, string what)
    {
        if (node is null)
        {
            return null;
        }

        var entry = RequireObject(node, $"{what} position");
        return new PublicPositionSchema(
            RequireString(entry, "unit", $"{what} position"),
            RequireString(entry, "origin", $"{what} position"),
            RequireInteger(entry, "min", $"{what} position"),
            Optional(entry, "max") is { } max ? RequireInteger(entry, "max", $"{what} position") : null);
    }

    private static IReadOnlyDictionary<string, string> ReadTerminalOutcomes(WireValue? node, string what)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (node is null)
        {
            return result;
        }

        foreach (var member in RequireObject(node, $"{what} terminal_outcomes").Members)
        {
            if (member.Value is not WireString outcome || string.IsNullOrWhiteSpace(outcome.Value))
            {
                throw new NotSupportedException(
                    $"{what} terminal_outcomes.{member.Key}: 必须是 \"win\"/\"loss\"/\"other\" 字符串");
            }

            result[member.Key] = outcome.Value;
        }

        return result;
    }

    private static IReadOnlyDictionary<string, PublicKnownPositionRole> ReadKnownPositions(WireValue? node, string what)
    {
        var result = new Dictionary<string, PublicKnownPositionRole>(StringComparer.Ordinal);
        if (node is null)
        {
            return result;
        }

        foreach (var member in RequireObject(node, $"{what} known_positions").Members)
        {
            var where = $"{what} known_positions.{member.Key}";
            var entry = RequireObject(member.Value, where);
            var pileTypes = new Dictionary<string, string>(StringComparer.Ordinal);
            if (Optional(entry, "pile_type_by_collection") is { } pileNode)
            {
                foreach (var pair in RequireObject(pileNode, where).Members)
                {
                    pileTypes[pair.Key] = pair.Value is WireString text && !string.IsNullOrWhiteSpace(text.Value)
                        ? text.Value
                        : throw new NotSupportedException($"{where}.pile_type_by_collection.{pair.Key}: 必须是非空字符串");
                }
            }

            result[member.Key] = new PublicKnownPositionRole(
                RequireString(entry, "fact_kind_id", where),
                RequireString(entry, "card_field_name", where),
                RequireString(entry, "pile_field_name", where),
                RequireString(entry, "position_field_name", where),
                pileTypes);
        }

        return result;
    }

    private static WireObject RequireObject(WireValue node, string what) =>
        node as WireObject ?? throw new NotSupportedException($"{what}: 期望 JSON 对象，实际是 {node.TypeName}");

    private static WireValue? Optional(WireObject node, string key) => node.Get(key);

    private static string RequireString(WireObject node, string key, string what) =>
        node.Get(key) is WireString text && !string.IsNullOrWhiteSpace(text.Value)
            ? text.Value
            : throw new NotSupportedException($"{what}: 缺少非空字符串键 '{key}'");

    private static long RequireInteger(WireObject node, string key, string what) =>
        node.Get(key) is WireInteger number && number.TryToLong(out var value)
            ? value
            : throw new NotSupportedException($"{what}: 缺少整数键 '{key}'");

    private static IReadOnlyList<string> RequireStringList(WireObject node, string key, string what)
    {
        if (node.Get(key) is not WireArray array)
        {
            throw new NotSupportedException($"{what}: 缺少数组键 '{key}'");
        }

        var result = new List<string>(array.Items.Count);
        foreach (var item in array.Items)
        {
            if (item is not WireString text || string.IsNullOrWhiteSpace(text.Value))
            {
                throw new NotSupportedException($"{what}: '{key}' 的元素必须是非空字符串");
            }

            result.Add(text.Value);
        }

        return result;
    }
}
