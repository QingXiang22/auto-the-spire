namespace BaseSpire.Slice;

/// <summary>
/// 一个遭遇**槽位**（`enemySlots[]` 的一项）：候选敌人 + 是否随机 + 唯一选择组。
///
/// 对应的原版行为是各遭遇自己的 `GenerateMonsters()`（例如 `SlimesWeak.cs:48-59`、
/// `RubyRaidersNormal.cs:38-50`）：`chooseRandomly = true` ⇒ 从候选里随机取一个（**恰好一次抽取**）；
/// `UniqueChoiceGroup` ⇒ 同名组的槽位之间不得选中同一个敌人（源里用"每个敌人可用次数"表达，
/// 本包用"组内不重复"表达）。组内槽位在**第一个成员的位置**连续抽取（`SlimesWeak.cs:51-57`：
/// 先挑完两个小史莱姆、再挑中等史莱姆，而阵容顺序仍是 小、中、小）。
/// </summary>
public sealed record EncounterSlot(
    IReadOnlyList<string> EnemyChoices,
    bool ChooseRandomly,
    string UniqueChoiceGroup);

/// <summary>
/// 一条**编队**（`formations[]` 的一项）。`EnemyIds` 是直接列出的固定阵容（**不抽数**），
/// `Slots` 是逐槽随机（每个 `chooseRandomly` 槽恰好一次抽取）。
/// </summary>
public sealed record EncounterFormation(
    int Weight,
    IReadOnlyList<string> EnemyIds,
    IReadOnlyList<EncounterSlot> Slots)
{
    public IReadOnlyDictionary<int, string> StartingActionsByEnemyIndex { get; init; } = new Dictionary<int, string>();
    /// <summary>这条编队可能出现的全部敌人（直接列出的 + 槽位候选；顺序稳定、去重）。</summary>
    public IReadOnlyList<string> AllEnemyIds =>
    [
        .. EnemyIds.Concat(Slots.SelectMany(slot => slot.EnemyChoices)).Distinct(StringComparer.Ordinal),
    ];
}

/// <summary>
/// 正式构建里**一个战斗房间可能出现的遭遇**（来源 `Enemies/Content/base-spire.encounters.json` 的
/// `encounters[]`：`id` / `roomType` / `enemyIds` / `isWeak` / `tags` / `formations` / `enemySlots`）。
///
/// `Formations`/`Slots` 为空 ⇒ 固定阵容（`EnemyIds` 直接列出，出生时不抽数）；
/// 非空 ⇒ **编队程序**：先按权重抽编队（当前内容权重全 1 ⇒ 均匀一次抽取），再按槽位抽取敌人
/// （每个 `chooseRandomly` 槽恰好一次抽取，同名 `uniqueChoiceGroup` 组内不重复）。
/// </summary>
public sealed record SliceEncounter(
    string EncounterId,
    string Kind,
    IReadOnlyList<string> EnemyIds,
    IReadOnlyList<string> Tags,
    /// <summary>编队（空 = 固定阵容）。</summary>
    IReadOnlyList<EncounterFormation> Formations,
    /// <summary>顶层槽位（`formations` 为空时使用的单编队形状）。</summary>
    IReadOnlyList<EncounterSlot> Slots)
{
    public IReadOnlyDictionary<int, string> StartingActionsByEnemyIndex { get; init; } = new Dictionary<int, string>();
    /// <summary>这条遭遇**全部可能出现**的敌人（固定名单 + 编队/槽位候选）。</summary>
    public IReadOnlyList<string> AllEnemyIds =>
    [
        .. EnemyIds
            .Concat(Formations.SelectMany(formation => formation.AllEnemyIds))
            .Concat(Slots.SelectMany(slot => slot.EnemyChoices))
            .Distinct(StringComparer.Ordinal),
    ];

    /// <summary>是否需要在出生时执行编队程序（抽编队/抽槽位）。</summary>
    public bool HasFormationProgram => Formations.Count > 0 || Slots.Count > 0;

    /// <summary>统一的编队列表：没有 `formations` 时把顶层槽位当作**唯一一条**编队（权重 1）。</summary>
    public IReadOnlyList<EncounterFormation> EffectiveFormations => Formations.Count > 0
        ? Formations
        : Slots.Count > 0
            ? [new EncounterFormation(1, [], Slots)]
            : [];
}

/// <summary>
/// 正式构建的**一幕遭遇池**与**整幕队列**的生成参数。
///
/// 与冻结源码对齐（`ActModel.GenerateRooms`，`ActModel.cs:331-386`）：
/// <list type="bullet">
/// <item>队列长度 = <see cref="RoomCount"/> = `GetNumberOfRooms(false)` = `BaseNumberOfRooms`
///       （Overgrowth = 15，`Overgrowth.cs:47`）；</item>
/// <item>**前 <see cref="WeakRoomCount"/> 个**（= `NumberOfWeakEncounters` = 3，`Overgrowth.cs:45`）
///       从弱遭遇池抓，其余从普通池抓——因此总长 15 里含 3 条弱遭遇，**不是** 3+15；</item>
/// <item>每个池各自一个 `GrabBag`（权重全 1.0），抓取时按"不与上一个入队遭遇共享标签、且不是它自己"
///       重抽（`ActModel.cs:419-430`）；</item>
/// <item>袋子空了就按池的枚举顺序以权重 1.0 重新装袋（不洗牌、不抽数）。</item>
/// <item><b>精英队列是独立库存</b>：源在普通两段之后单起 `grabBag3`，把 `AllEliteEncounters` 预生成到
///       `_rooms.eliteEncounters`（固定 15 槽，`eliteQueueCount`），并由 `RoomSet.NextEliteEncounter`
///       按 `eliteEncountersVisited` 计数取模消费——与普通队列不共享列表、不共享计数、不共享"上一个"。</item>
/// </list>
///
/// 精英池的成员与顺序来自幕声明（`base-spire.acts.json` 的 `eliteEncounters`），不是"可执行子集"。
///
/// 池的**内容**是规则（可共享、进规则身份），队列是**局内状态**（每局一份）——两者必须分开。
/// 输入池是**已支持子集**：未支持的遭遇由 `ExcludedEncounters` 逐条记账，池成员与顺序与源一致。
/// </summary>
public sealed record SliceEncounterPool(
    IReadOnlyList<SliceEncounter> WeakEncounters,
    IReadOnlyList<SliceEncounter> RegularEncounters,
    /// <summary>幕开头从弱池取的房间数（来源 `NumberOfWeakEncounters`）。</summary>
    int WeakRoomCount,
    /// <summary>普通遭遇队列总长（来源 `GetNumberOfRooms(false)` = `BaseNumberOfRooms`）。</summary>
    int RoomCount,
    /// <summary>预生成整幕队列用的原版流（`up_front`）。</summary>
    string PoolStream,
    /// <summary>战斗开始时洗战斗牌堆用的原版流（`shuffle`）。</summary>
    string ShuffleStream,
    /// <summary>幕末 Boss 遭遇；不进入普通房间队列，由地图 Boss 节点显式选择。</summary>
    IReadOnlyList<SliceEncounter>? BossEncounters = null,
    /// <summary>精英遭遇队列长度（来源 `ActModel.GenerateRooms` 的 `for (num3 = 0; num3 &lt; 15; num3++)`，
    /// 由幕声明 `eliteQueueCount` 给出）。0 = 本池不预生成精英队列（受控夹具的既有形状不变）。</summary>
    int EliteRoomCount = 0,
    /// <summary>精英遭遇池（源 `AllEliteEncounters`，声明顺序）。它与弱/普通池是**两个独立库存**：
    /// 源里 `_rooms.eliteEncounters` 与 `_rooms.normalEncounters` 是两个列表、两个 `GrabBag`、
    /// 两个 per-type 访问计数（`RoomSet.NextEliteEncounter` / `NextNormalEncounter`）。</summary>
    IReadOnlyList<SliceEncounter>? EliteEncounters = null)
{
    public string RandomLayoutId { get; init; } = BaseSpire.SinglePlayer.RunRandomLayout.Frozen;
    /// <summary>弱遭遇池的种类标签（来源 `isWeak = true`）。</summary>
    public const string WeakKind = "weak";

    /// <summary>普通遭遇池的种类标签。</summary>
    public const string NormalKind = "normal";

    /// <summary>精英遭遇池的种类标签（源 `RoomType.Elite`）。</summary>
    public const string EliteKind = "elite";

    /// <summary>池里可能出现的全部遭遇：**弱池在前、普通池在后**（= 源的池枚举顺序）。</summary>
    public IReadOnlyList<SliceEncounter> Encounters => [.. WeakEncounters, .. RegularEncounters];

    /// <summary>精英遭遇池（源 `AllEliteEncounters`）。空 = 本构建没有精英库存。</summary>
    public IReadOnlyList<SliceEncounter> Elites => EliteEncounters ?? [];

    /// <summary>池里出现的全部敌人内容 id（去重、稳定排序）：构建描述与包声明都按它取敌人定义。</summary>
    public IReadOnlyList<string> EnemyIds =>
    [
        .. Encounters.Concat(Elites).SelectMany(encounter => encounter.AllEnemyIds)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal),
    ];

    /// <summary>需要**遭遇派生流**的遭遇（带编队程序的那些）与其源 entry 名。</summary>
    public IReadOnlyList<SliceEncounter> FormationEncounters =>
        [.. Encounters.Where(encounter => encounter.HasFormationProgram)];

    /// <summary>幕末 Boss 遭遇（源顺序）。</summary>
    public IReadOnlyList<SliceEncounter> Bosses => BossEncounters ?? [];

    /// <summary>池里出现的全部遭遇：弱池、普通池、**独立精英池**与独立 Boss 声明。
    /// 战斗房间的分派链按它取遭遇（精英 id 只能在这条链里被解析）。</summary>
    public IReadOnlyList<SliceEncounter> AllEncounters => [.. Encounters, .. Elites, .. Bosses];

    public IReadOnlyList<string> Tags =>
    [
        .. Encounters.Concat(Elites).SelectMany(encounter => encounter.Tags).Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal),
    ];
}
