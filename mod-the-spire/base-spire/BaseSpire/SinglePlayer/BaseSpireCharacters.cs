namespace BaseSpire.SinglePlayer;

/// <summary>
/// 一个角色的**原版起始配置**（起始牌组 / 起始遗物 / 初始资源）。
///
/// 这是"真实角色开局"的唯一来源：正式构建**不再**从冻结的 `.author` 原型文本取 run/deck，
/// 也不使用"每张降级卡各一张"的诊断牌组。
///
/// 来源（冻结只读 `sts2/`）：
/// <list type="bullet">
/// <item>起始生命 / 起始金币 / 起始牌组：`sts2/src/Core/Models/Characters/&lt;Character&gt;.cs`
///       （Ironclad.cs:33 `StartingHp =&gt; 80`、:35 `StartingGold =&gt; 99`、:43-55 起始牌组）；</item>
/// <item>起始遗物：同文件 `StartingRelics`（Ironclad.cs:57 `BurningBlood`）；</item>
/// <item>起始药水：基类默认为空（`CharacterModel.cs:102`），五个标准角色都不覆盖；</item>
/// <item>组装点：`Player.CreateForNewRun`（`Player.cs:301-306`）→ `PopulateStartingInventory`
///       （`Player.cs:330-346`）。</item>
/// </list>
///
/// 内容 id 用本项目自己的规范 id（`base-spire:&lt;kebab&gt;`，与 `ContentSource/*.spire` 的正式源一致）；
/// 角色 id 用 `CHARACTER.&lt;ENTRY&gt;`（`ModelDb.GetId`，`ModelDb.cs:466-494`）。
/// </summary>
public sealed record BaseSpireCharacter(
    /// <summary>规范角色 id（`CHARACTER.IRONCLAD`）。</summary>
    string CharacterId,
    /// <summary>本项目的短名/CLI 名（`Ironclad`）。</summary>
    string Name,
    /// <summary>原版显示名（`The Ironclad`，来自 `localization/eng/characters.json`）。</summary>
    string DisplayName,
    int MaxHp,
    int Gold,
    /// <summary>起始牌组（内容 id，含重复；顺序即长期牌组的初始顺序）。</summary>
    IReadOnlyList<string> Deck,
    /// <summary>起始遗物（内容 id）；效果按 <c>SliceRelics</c> 的声明表实现，未登记的遗物在开局前拒绝。</summary>
    IReadOnlyList<string> Relics,
    /// <summary>这个角色在本构建里是否**可实际开局**。</summary>
    bool Playable,
    /// <summary>不可开局时的具体原因（不静默退回 Ironclad）。</summary>
    string NotPlayableReason,
    /// <summary>来源定位（文件:行），进构建描述与证据。</summary>
    string Source);

/// <summary>
/// 五个标准角色的起始配置注册表（`ModelDb.AllCharacters`，`ModelDb.cs:123-130` 的顺序）。
///
/// 当前只有 Ironclad 可实际开局：其余四个角色的正式内容（卡池、资源、机制）尚未实现，
/// 因此在这里**声明但拒绝开局**——"不支持"必须是一个显式错误，而不是悄悄换角色。
/// </summary>
public static class BaseSpireCharacters
{
    public const string IroncladId = "CHARACTER.IRONCLAD";

    public const string SilentId = "CHARACTER.SILENT";

    public const string RegentId = "CHARACTER.REGENT";

    public const string NecrobinderId = "CHARACTER.NECROBINDER";

    public const string DefectId = "CHARACTER.DEFECT";

    /// <summary>Ironclad 起始牌组：五张打击、四张防御、一张重击（`Ironclad.cs:43-55`）。</summary>
    public static IReadOnlyList<string> IroncladDeck { get; } =
    [
        "base-spire:strike-ironclad",
        "base-spire:strike-ironclad",
        "base-spire:strike-ironclad",
        "base-spire:strike-ironclad",
        "base-spire:strike-ironclad",
        "base-spire:defend-ironclad",
        "base-spire:defend-ironclad",
        "base-spire:defend-ironclad",
        "base-spire:defend-ironclad",
        "base-spire:bash",
    ];

    public static IReadOnlyList<BaseSpireCharacter> All { get; } =
    [
        new(BaseSpireCharacters.IroncladId, "Ironclad", "The Ironclad", 80, 99, IroncladDeck,
            ["base-spire:burning-blood"], Playable: true, string.Empty,
            "sts2/src/Core/Models/Characters/Ironclad.cs:33,35,43-57"),
        new(SilentId, "Silent", "The Silent", 70, 99, [], [], Playable: false,
            "Silent 的正式卡池/起始牌组/遗物与中毒机制尚未迁移：不能拿 Ironclad 的牌代打",
            "sts2/src/Core/Models/Characters/Silent.cs"),
        new(RegentId, "Regent", "The Regent", 75, 99, [], [], Playable: false,
            "Regent 的正式卡池/起始配置与星能机制尚未迁移",
            "sts2/src/Core/Models/Characters/Regent.cs"),
        new(NecrobinderId, "Necrobinder", "The Necrobinder", 75, 99, [], [], Playable: false,
            "Necrobinder 的正式卡池/起始配置与召唤机制尚未迁移",
            "sts2/src/Core/Models/Characters/Necrobinder.cs"),
        new(DefectId, "Defect", "The Defect", 75, 99, [], [], Playable: false,
            "Defect 的正式卡池/起始配置与充能球机制尚未迁移",
            "sts2/src/Core/Models/Characters/Defect.cs"),
    ];

    public static BaseSpireCharacter ById(string characterId) =>
        All.FirstOrDefault(character => string.Equals(character.CharacterId, characterId, StringComparison.Ordinal))
        ?? throw new ArgumentException(
            $"未知角色 id '{characterId}'（可选：{string.Join(", ", All.Select(character => character.CharacterId))}）",
            nameof(characterId));

    /// <summary>短名（`Ironclad`）/ 规范 id（`CHARACTER.IRONCLAD`）都能解析；不认识的输入直接失败。</summary>
    public static BaseSpireCharacter ByName(string name) =>
        All.FirstOrDefault(character =>
            string.Equals(character.Name, name, StringComparison.OrdinalIgnoreCase)
            || string.Equals(character.CharacterId, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException(
            $"未知角色 '{name}'（可选：{string.Join(", ", All.Select(character => character.Name))}）",
            nameof(name));
}
