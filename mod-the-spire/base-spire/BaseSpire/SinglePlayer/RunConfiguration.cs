namespace BaseSpire.SinglePlayer;

/// <summary>解锁策略。当前只支持 `all`（正式环境按"全解锁"固定）。</summary>
public enum UnlockPolicy
{
    All = 0,
}

/// <summary>游戏模式。当前只支持单人。</summary>
public enum RunMode
{
    SinglePlayer = 0,
}

/// <summary>
/// 正式构建的**范围身份**（不可变构建配置的一部分，不属于某一局）。
///
/// 三个 id 描述**三种不同的完成度**，不是同一实现的三个标签：
/// <list type="bullet">
/// <item><see cref="Dev"/>：限范围开发。当前唯一**可实现**的范围。</item>
/// <item><see cref="Ironclad"/>：完整 Ironclad 单人可达域（四幕胜负、全部房间与永久状态）。</item>
/// <item><see cref="Full"/>：五标准角色与完整单人可达域。</item>
/// </list>
///
/// <see cref="Implemented"/> 是"当前构建真的能满足哪些范围"的**唯一事实来源**：它随实际能力/内容闭合
/// 才增加，不能靠改标签。见 [BASE-01-R1 复核](../../../../../docs/review/records/2026-10-05/base-01-owner-review.md)。
/// </summary>
public static class BaseSpireScope
{
    /// <summary>限范围开发：必须列 unsupported，**不能**关闭 Ironclad/全环境阶段。</summary>
    public const string Dev = "base-spire-singleplayer-dev-v1";

    /// <summary>完整 Ironclad 单人可达域：只接受 Ironclad，可申请阶段审查，不称五角色完成。</summary>
    public const string Ironclad = "base-spire-ironclad-singleplayer-v1";

    /// <summary>五标准角色与完整单人可达域：用于 D/E 最终候选。</summary>
    public const string Full = "base-spire-singleplayer-v1";

    /// <summary>
    /// T05 地图/房间开发范围（**新增开关**，默认路径不受影响）：与 <see cref="Dev"/> 同一内容子集，
    /// 额外把原版 `act_{n}_map` 命名流生成的地图物化进引擎（`MapNodes`/`MapChildren`、
    /// `Run.CurrentNode/MapPhase/MapFloor/MapNodeCount/MapDraws`），并把房间选择从"按遭遇池序号推进"
    /// 换成地图上的 `ChooseMapPoint`（`EnterNextRoom` 在该 scope 下不注册）。
    /// 未实现房型仍具名失败、不伪装空事件，因此它**不等于** Ironclad/full 闭合。
    /// </summary>
    public const string DevMap = "base-spire-singleplayer-dev-map-v1";

    /// <summary>已知范围 id 的完整集合（用于区分"未知 id"与"已知但尚未实现"）。</summary>
    public static readonly IReadOnlyList<string> Known = [Dev, DevMap, Ironclad, Full];

    /// <summary>
    /// **当前可实现**的范围：dev 与 T05 的 dev-map。正式内容仍是子集（地图只到"能生成并选择，
    /// 未实现房型具名失败"；**普通战斗的真实奖励**（金币/卡牌候选/跳过/药水掉落判定）已接入 dev-map，
    /// 但没有事件/幕转换/四幕终局，药水取得仍因内容未装载而具名受阻），因此不能发布 Ironclad/full 身份。
    /// 开启某个范围的条件是相应能力与内容真实闭合，而不是把这里的字符串改一下。
    /// </summary>
    public static readonly IReadOnlyList<string> Implemented = [Dev, DevMap];

    /// <summary>某个范围为什么还不能实现（用于拒绝时的可读信息）。</summary>
    public static string NotImplementedReason(string scopeId) => scopeId switch
    {
        // 已接入的东西不再列：起始遗物效果（Burning Blood 胜利回血）、敌人出生生命掷点（`Niche` 流）、
        // 起始牌序/起始资源、**敌人行动状态机**与**整幕遭遇队列**（GrabBag + 标签回避）都已进入正式构建，
        // 因此这里只列**真的还缺**的范围。
        Ironclad => "完整 Ironclad 单人域尚未闭合：地图与房间类型（营火/商店/宝箱/精英/BOSS/事件）、"
            + "真实战斗奖励、"
            + "事件与幕转换、四幕真实胜负都还缺；敌人行动状态机（含按原版 float32 语义的加权选取）、"
            + "整幕遭遇队列（普通/精英两个独立队列）与遭遇**编队**（均匀抽编队 + 唯一选择组 + 随机槽位）"
            + "已接入，但仍有具名/位次条件、敌人 `variables`/`conditions` 声明表与 `summonEnemy`（召唤机制族）"
            + "未实现（`addCards` 已实现并有检查），当前内容只是 dev 子集",
        Full => "五标准角色完整单人域尚未闭合：除 Ironclad 的缺口外，其余四个角色的正式卡池/资源/机制"
            + "尚未迁移",
        DevMap => "T05 地图开发范围：地图生成/物化、`ChooseMapPoint` 房间选择与**普通战斗的真实奖励**"
            + "（源奖励顺序的掉落判定/金币/三张候选卡/领取或跳过，奖励关闭前不能旅行）已接入，"
            + "营火/宝箱/精英/Vantom及独立幕完成已接入，商店（购买/删牌/离店）与**问号房判定的实际房型**"
            + "（源 `UnknownMapPointOdds` + 源事件队列消费 + 已实现事件的独立模块）已接入，"
            + "但幕转换、四幕终局、已实现事件之外的源事件身份（选中即具名失败）"
            + "以及药水奖励的**取得**路径（内容未装载药水，掉落判定与赔率状态已忠实实现）仍未实现"
            + "（选中未实现房型/事件时具名失败，节点保留、不伪装空事件），因此不等于 Ironclad/full 闭合",
        _ => "该范围尚未实现",
    };

    /// <summary>该范围是否可被当前构建实现。</summary>
    public static bool IsImplemented(string scopeId) => Implemented.Contains(scopeId, StringComparer.Ordinal);
}

/// <summary>
/// **不可变的整局配置**：角色、进阶、解锁策略、模式、源基线、范围身份。
///
/// 它与"某一局"是两件事：`GameSeed` **不属于**配置——每局游戏单独传入（见
/// <see cref="BaseSpireRuntimeFactory.CreateSession"/>）。配置相同的两局必须得到同一份
/// 只读规则/构建描述，但各自拥有独立的状态与随机游标。
///
/// 拒绝策略：不认识的角色、不支持的进阶在**建局之前**失败，不退回 Ironclad / 进阶 0。
/// </summary>
public sealed record RunConfiguration
{
    /// <summary>规范角色 id（`CHARACTER.IRONCLAD`）或短名（`Ironclad`），两者都由 base 注册表解析。</summary>
    public required string Character { get; init; }

    /// <summary>进阶等级。当前只支持 0：正式源里 `variants(default=…, deadly=…)` 的 deadly/tough 分支尚未接入。</summary>
    public int Ascension { get; init; }

    public UnlockPolicy Unlocks { get; init; } = UnlockPolicy.All;

    public RunMode Mode { get; init; } = RunMode.SinglePlayer;

    /// <summary>源基线身份：必须是 <see cref="BaseSpireSourceBaselines.Supported"/> 里的一个（R3）。</summary>
    public string SourceBaselineId { get; init; } = BaseSpireSourceBaselines.Frozen;

    /// <summary>范围身份（<see cref="BaseSpireScope"/>）。默认 dev：未闭合范围不能声称完整。</summary>
    public string ScopeId { get; init; } = BaseSpireScope.Dev;

    /// <summary>Ironclad / 进阶 0 的正式配置（默认 dev 范围）。</summary>
    public static RunConfiguration Ironclad(int ascension = 0, string scopeId = BaseSpireScope.Dev) =>
        new() { Character = BaseSpireCharacters.IroncladId, Ascension = ascension, ScopeId = scopeId };

    /// <summary>解析并校验：返回角色定义；任何不支持项在这里失败（不是在建局中途）。</summary>
    public BaseSpireCharacter Resolve()
    {
        // R3：源基线先校验。未知基线下的"内容/数值从哪来"无从核对，因此不能构建。
        BaseSpireSourceBaselines.Require(SourceBaselineId);

        if (Mode != RunMode.SinglePlayer)
        {
            throw new NotSupportedException($"模式 '{Mode}' 未支持：当前只交付单人环境");
        }

        if (Unlocks != UnlockPolicy.All)
        {
            throw new NotSupportedException($"解锁策略 '{Unlocks}' 未支持：正式环境固定 all");
        }

        if (Ascension != 0)
        {
            // 正式源里的敌人数值/牌组都带 `variants(default=…, tough/deadly=…)`，降级目前取 default。
            // 用进阶 0 的数值跑进阶 5 会静默产生不同的原版行为，因此这里显式拒绝。
            throw new NotSupportedException(
                $"进阶 {Ascension} 未支持：正式源里的进阶分支（tough/deadly 变体）尚未接入，"
                + "当前只支持进阶 0；不会退回进阶 0 继续");
        }

        var character = BaseSpireCharacters.ByName(Character);
        if (!character.Playable)
        {
            throw new NotSupportedException($"角色 {character.Name} 未支持：{character.NotPlayableReason}");
        }

        if (ScopeId is not (BaseSpireScope.Dev or BaseSpireScope.DevMap
            or BaseSpireScope.Ironclad or BaseSpireScope.Full))
        {
            throw new ArgumentException(
                $"未知范围身份 '{ScopeId}'（可选：{string.Join(" / ", BaseSpireScope.Known)}）", nameof(ScopeId));
        }

        if (!BaseSpireScope.IsImplemented(ScopeId))
        {
            // R1：**先拒绝，再谈身份**。当前构建无法满足 Ironclad/full 的范围要求，因此不能生成 Session、
            // 也不能发布那个范围的描述——否则 B/C 按配置转发就会把 dev 子集发布成完整环境。
            throw new NotSupportedException(
                $"范围身份 '{ScopeId}' 未实现：{BaseSpireScope.NotImplementedReason(ScopeId)}。"
                + $"当前可实现的只有 '{BaseSpireScope.Dev}'；"
                + "范围由实际能力/内容闭合开启，不能靠改标签");
        }

        return character;
    }
}
