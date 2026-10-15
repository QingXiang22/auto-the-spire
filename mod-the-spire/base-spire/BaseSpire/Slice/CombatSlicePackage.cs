using CoreTheSpire.Engine;
using BaseSpire.Slice.Effects;
using BaseSpire.Slice.Orchestration;

namespace BaseSpire.Slice;

/// <summary>
/// The controlled STS combat slice <c>base-spire:combat-slice-v1</c>, authored against the generic runtime
/// (<see cref="EnginePackageBuilder"/>). Nothing in the engine knows what a card, an energy point, a cultist, block
/// or vulnerability is: every STS concept below is declared by this package.
///
/// Delivered so far:
/// <list type="bullet">
/// <item><b>B-01</b> the player/monster/card/power/area/resource/flow/action/event vocabulary, the controlled
/// initial state and the frozen starter deck;</item>
/// <item><b>B-02</b> cost and energy, damage with the recovered additive-then-multiplicative order and a single
/// truncation, block absorption, health and death, powers (apply / stack / turn-end tick / Ritual), the two-cultist
/// enemy turn with their recovered move cycles, discard reshuffling and victory or defeat.</item>
/// </list>
///
/// Card orchestration is composed from the Slice.Orchestration modules; formal card programs use
/// Slice.Effects, while the diagnostic author path retains an explicit step adapter. Enemy, power and Run
/// compilation is composed by Slice.Compilation.CombatPackageComposer. Implemented mechanics and complete-environment gaps are tracked in the main
/// task list; this controlled slice does not establish full original-game coverage.
/// </summary>
public static class CombatSlicePackage
{
    internal static EngineFaultException Unsupported(string operation, string reason) =>
        Faults.Schema($"Cannot compile '{operation}': {reason}");

    /// <param name="includeTestHeadbutt">
    /// Test-scenario switch. The frozen starter deck is five Strikes, four Defends and one Bash, so it contains no
    /// Headbutt; setting this adds one Headbutt to the draw pile so the choice path of B-03 can be exercised. It is
    /// explicitly <em>not</em> part of the frozen deck and is never enabled by the default build.
    /// </param>
    /// <param name="fromAuthorText">
    /// B-05b：从**作者文本**（<see cref="SliceAuthorFrontend" />）载入切片内容的卡牌/能力/敌人数据与效果步骤，
    /// 并用同一套通用 IR 生成程序；效果序列按声明的 step 列表展开（不再是以 kind 分派的通用分支）。
    /// 数值与出处与程序化路径逐条镜像，因此两条路径是同一份冻结数据。
    /// </param>
    public static EnginePackageBuilder Build(bool includeTestHeadbutt = false, bool fromAuthorText = false,
        SliceAuthorModel? authorModel = null,
        // W-02 测试场景旋钮：对调两个敌人的**创建顺序**（默认关；两个场景的可见事实完全相同，
        // 因此公开快照必须逐字节相同）。非原版配置，仅用于成对验收。
        bool swapEncounterOrder = false,
        // W-02 测试场景旋钮：两个敌人共用**同一个定义**、只有初始 Hp 不同（10 / 10）。
        // 这说明"同定义实例的公开身份只差一个已发布字段"，用来验证"公开字段变化（受伤）后公开句柄不换号"。
        // 若句柄仍按每份快照重新排序编号，Hp 从 10 掉到 4 会让两个实例的排序交叉、句柄互换。
        // 非原版配置，默认关；默认路径的字段值与 IR 逐条不变。
        bool sameDefinitionEnemyPair = false,
        // ENV/BASE-01 正式单人构建开关（默认全关 ⇒ 受控切片与既有夹具的包形状逐字不变）：
        // ① 包版本：正式构建必须有自己的构建身份，不能沿用 `combat-slice-v1`。
        string? packageVersion = null,
        // ② 真实遭遇池：每个战斗房间从池里按原版流取一个遭遇（而不是固定两只 Cultist）。
        SliceEncounterPool? encounterPool = null,
        // ③ 战斗牌堆开局洗牌：原版战斗开始时洗牌（`Shuffle` 流），受控切片按声明顺序发牌。
        bool shuffleBattleDeck = false,
        // ④ 是否以"打完配置的房间数"时设整局胜利。正式构建为 false——按房间计数设 Victory 不是原版规则。
        //    原版的胜利只在最后 BOSS 之后发生（BASE-02）。
        bool terminalOnConfiguredRoomCount = true,
        // ⑤ BASE-02：起始遗物。`relicLibrary` 是**声明表**（触发点/种类/数值），`startingRelics`
        //    是本局开局实际放进遗物栏的 id（来自角色注册表）。效果由声明统一展开。
        //    不为每件遗物写 C# 效果类。
        IReadOnlyList<SliceRelic>? relicLibrary = null,
        IReadOnlyList<string>? startingRelics = null,
        // ⑥ BASE-02：敌人**出生生命掷点**用的原版流（`Niche`）。null = 受控切片行为
        //    （取声明范围下限），非 null = 正式构建（每次出生恰好抽一次，双端包含，排除同侧已用值）。
        string? spawnHpStream = null,
        // ⑦ T05：地图/房间子包（默认关）。开启时新增 `MapNode`/`MapNodes`/`MapChildren` 与
        //    `Run.CurrentNode`/`MapPhase`/`MapFloor`/`MapNodeCount`/`MapDraws`、`ChooseMapPoint` 动作，
        //    开局从新流 `base-spire:run:act_1_map` 逐笔重放生成整幕地图；
        //    关闭时（受控切片与既有夹具）声明、IR、摘要、行为逐字不变。
        bool actMapEnabled = false, bool initializeNewRun = false)
    {
        return Compilation.CombatPackageComposer.Build(new Compilation.PackageBuildOptions(
            includeTestHeadbutt, fromAuthorText, authorModel, swapEncounterOrder, sameDefinitionEnemyPair,
            packageVersion, encounterPool, shuffleBattleDeck, terminalOnConfiguredRoomCount,
            relicLibrary, startingRelics, spawnHpStream, actMapEnabled, initializeNewRun));
    }

    /// <summary>把内�?id / 标签名变成合法的字段名末段（`base-spire:crawler` �?`base_spire_crawler`）�?/summary>
    internal static string Identifier(string value) =>
        new([.. value.Select(character => char.IsAsciiLetterOrDigit(character) ? character : '_')]);
}
