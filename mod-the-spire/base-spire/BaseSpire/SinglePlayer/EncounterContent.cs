using System.Text.Json;
using BaseSpire.Slice;

namespace BaseSpire.SinglePlayer;

/// <summary>
/// `Content/base-spire.encounters.json` 里一条**遭遇声明**（原样读出，不含选择规则）。
///
/// 字段来源：`id` / `roomType` / `enemyIds` / `isWeak` / `tags` / `formations` / `enemySlots` / `weight`。
/// `enemyIds` 只对"直接列出敌人"的遭遇非空；带 `formations` 或 `enemySlots` 的遭遇按**编队程序**运行
/// （模型的定义在 <see cref="BaseSpire.Slice.EncounterFormation"/>），不再被当作未支持。
/// </summary>
public sealed record EncounterDeclaration(
    string Id,
    string RoomType,
    bool IsWeak,
    IReadOnlyList<string> EnemyIds,
    IReadOnlyList<string> Tags,
    IReadOnlyList<EncounterFormation> Formations,
    IReadOnlyList<EncounterSlot> Slots)
{
    /// <summary>编队条数（报告与校验用）。</summary>
    public int FormationCount => Formations.Count;

    /// <summary>顶层槽位数（报告与校验用；`formations` 里的槽位不计入）。</summary>
    public int SlotCount => Slots.Count;

    /// <summary>该遭遇可能出现的全部敌人（固定名单 + 编队/槽位候选）。</summary>
    public IReadOnlyList<string> AllPossibleEnemyIds =>
    [
        .. EnemyIds
            .Concat(Formations.SelectMany(formation => formation.AllEnemyIds))
            .Concat(Slots.SelectMany(slot => slot.EnemyChoices))
            .Distinct(StringComparer.Ordinal),
    ];
}

/// <summary>一幕的遭遇池声明（`base-spire.acts.json` 的 `actPools[]`）。</summary>
public sealed record ActPoolDeclaration(
    string ActId,
    int NormalRoomCount,
    int WeakNormalRoomCount,
    int EliteQueueCount,
    IReadOnlyList<string> WeakNormalEncounters,
    IReadOnlyList<string> RegularEncounters,
    IReadOnlyList<string> EliteEncounters,
    IReadOnlyList<string> BossEncounters);

/// <summary>
/// 正式**遭遇/幕池内容源**的读取器。
///
/// 为什么走 JSON：这两份内容没有 0.0.1 `.spire` 声明入口（它们是被旧实现读取的版本化注册数据）。
/// 按 ENV 约束"无现有声明入口的内容可由 base 版本化注册数据/受控算法定义并生成同一 IR 和状态，
/// 须有来源且纳入身份"，这里保留它们为**正式内容唯一来源**，并把文件摘要带进构建描述。
/// </summary>
public static class EncounterContent
{
    public const string EncountersResource = "BaseSpire.Content.base-spire.encounters.json";

    public const string ActsResource = "BaseSpire.Content.base-spire.acts.json";

    public const string EncountersSourcePath = "mod-the-spire/base-spire/BaseSpire/Content/base-spire.encounters.json";

    public const string ActsSourcePath = "mod-the-spire/base-spire/BaseSpire/Content/base-spire.acts.json";

    public static string ReadText(string resourceName)
    {
        using var stream = typeof(EncounterContent).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"缺少正式遭遇资源 '{resourceName}'");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static IReadOnlyList<EncounterDeclaration> LoadEncounters() => ParseEncounters(ReadText(EncountersResource));

    public static IReadOnlyList<ActPoolDeclaration> LoadActPools() => ParseActPools(ReadText(ActsResource));

    /// <summary>源文件摘要（进构建描述与证据；同一内容改动必须让摘要变化）。</summary>
    public static string Digest(string text) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(text)))[..16].ToLowerInvariant();

    public static IReadOnlyList<EncounterDeclaration> ParseEncounters(string json)
    {
        using var document = JsonDocument.Parse(json);
        var result = new List<EncounterDeclaration>();
        foreach (var node in document.RootElement.GetProperty("encounters").EnumerateArray())
        {
            var formations = new List<EncounterFormation>();
            if (node.TryGetProperty("formations", out var formationArray)
                && formationArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var formation in formationArray.EnumerateArray())
                {
                    formations.Add(new EncounterFormation(
                        // 缺省权重 1；权重参与编队抽取（当前内容全是 1 ⇒ 均匀抽一次）。
                        Math.Max(1, Int(formation, "weight")),
                        Strings(formation, "enemyIds"),
                        ReadSlots(formation)));
                }
            }

            result.Add(new EncounterDeclaration(
                node.GetProperty("id").GetString() ?? string.Empty,
                node.TryGetProperty("roomType", out var roomType) ? roomType.GetString() ?? string.Empty : string.Empty,
                node.TryGetProperty("isWeak", out var weak) && weak.ValueKind == JsonValueKind.True,
                Strings(node, "enemyIds"),
                Strings(node, "tags"),
                formations,
                ReadSlots(node)));
        }

        return result;
    }

    /// <summary>`enemySlots[]` → 槽位模型（缺失 `enemyChoices` 的槽位被跳过 = 声明错误由调用方核对）。</summary>
    private static IReadOnlyList<EncounterSlot> ReadSlots(JsonElement node)
    {
        var slots = new List<EncounterSlot>();
        if (!node.TryGetProperty("enemySlots", out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return slots;
        }

        foreach (var slot in array.EnumerateArray())
        {
            var choices = Strings(slot, "enemyChoices");
            if (choices.Count == 0)
            {
                continue;
            }

            slots.Add(new EncounterSlot(
                choices,
                slot.TryGetProperty("chooseRandomly", out var random) && random.ValueKind == JsonValueKind.True,
                slot.TryGetProperty("uniqueChoiceGroup", out var group) ? group.GetString() ?? string.Empty
                    : string.Empty));
        }

        return slots;
    }

    public static IReadOnlyList<ActPoolDeclaration> ParseActPools(string json)
    {
        using var document = JsonDocument.Parse(json);
        var result = new List<ActPoolDeclaration>();
        foreach (var node in document.RootElement.GetProperty("actPools").EnumerateArray())
        {
            result.Add(new ActPoolDeclaration(
                node.GetProperty("actId").GetString() ?? string.Empty,
                Int(node, "normalRoomCount"),
                Int(node, "weakNormalRoomCount"),
                Int(node, "eliteQueueCount"),
                Strings(node, "weakNormalEncounters"),
                Strings(node, "regularEncounters"),
                Strings(node, "eliteEncounters"),
                Strings(node, "bossEncounters")));
        }

        return result;
    }

    private static IReadOnlyList<string> Strings(JsonElement node, string name)
    {
        if (!node.TryGetProperty(name, out var array) || array.ValueKind != JsonValueKind.Array) return [];
        return [.. array.EnumerateArray().Select(item => item.GetString() ?? string.Empty)];
    }

    private static int Int(JsonElement node, string name) =>
        node.TryGetProperty(name, out var value) && value.TryGetInt32(out var parsed) ? parsed : 0;
}
