namespace BaseSpire.SinglePlayer;

/// <summary>
/// 一个**可验证的源基线**：这份构建的规则数值/内容是从哪一份冻结源码得到的。
///
/// 它不是一个自由标签：id 必须出现在 <see cref="BaseSpireSourceBaselines.Supported"/> 里，且
/// 构建描述会同时发布**实际加载内容的摘要**与**证据路径**——"声称的来源"和"真正加载的东西"
/// 因此可以互相对照（R3）。
/// </summary>
public sealed record BaseSpireSourceBaseline(
    string Id,
    /// <summary>这份基线的版本说明（不猜客户端版本：只有源码树能证明的部分才写）。</summary>
    string VersionNote,
    /// <summary>本构建取值直接依据的源码文件（file:line 形式，进构建描述与证据）。</summary>
    IReadOnlyList<string> EvidencePaths,
    string Note);

/// <summary>
/// 支持范围内的源基线注册表。**唯一事实来源**：未知 id 一律拒绝，不会"带个名字继续跑"。
/// </summary>
public static class BaseSpireSourceBaselines
{
    /// <summary>当前冻结的 `sts2/` 源码树（本项目的原版权威依据）。</summary>
    public const string Frozen = "sts2-frozen-2026-10-05";

    public static IReadOnlyList<string> Supported { get; } = [Frozen];

    public static BaseSpireSourceBaseline Current { get; } = new(
        Frozen,
        "恢复源码树 `sts2/`（冻结于 2026-10-05，core 锚点提交 92ccd34）。"
        + "注意：这棵树仍带旧的 31 位字符串哈希（`StringHelper.cs:120-134`），"
        + "而 `reference/sts2-sim` 固定在 v0.111 并改用 XxHash64——两者不能互相冒充，"
        + "也不能据此断言客户端版本。",
        [
            "sts2/src/Core/Models/Characters/Ironclad.cs:33,35,43-57（起始生命/金币/牌组/遗物）",
            "sts2/src/Core/Entities/Players/Player.cs:301-346（起始背包组装）",
            "sts2/src/Core/Runs/RunRngSet.cs:104-119（字符串 seed → run seed 与 12 条 run 流派生）",
            "sts2/src/Core/Random/PlayerRngSet.cs:31-47（玩家级 3 条流派生）",
            "sts2/src/Core/Random/Rng.cs:47-55（具名流派生 = seed + hash32(snake_case(名))）",
            "sts2/src/Core/Helpers/StringHelper.cs:69-134（SnakeCase / GetDeterministicHashCode）",
            "sts2/src/Core/Helpers/SeedHelper.cs:32-39（大厅 seed 标准化；命令行路径不做）",
            "sts2/src/Core/Models/Acts/Overgrowth.cs:45,71-98（act-1 弱遭遇数/遭遇池名单）",
            "mod-the-spire/base-spire/BaseSpire/Enemies/Content/base-spire.encounters.json（63 条遭遇声明）",
            "mod-the-spire/base-spire/BaseSpire/Run/Content/base-spire.acts.json（幕池与房间数）",
            "mod-the-spire/base-spire/BaseSpire/{Cards,Powers,Enemies,Potions}/Content/*.spire（卡/能力/敌人声明）",
        ],
        "同一 id 下不允许出现行为不同的运行配置：内容/顺序/数值的任何变化都会改摘要与公开构建身份。");

    public static bool IsSupported(string baselineId) => Supported.Contains(baselineId, StringComparer.Ordinal);

    /// <summary>解析基线；未知 id 直接失败（不静默回退到当前基线）。</summary>
    public static BaseSpireSourceBaseline Require(string baselineId)
    {
        ArgumentNullException.ThrowIfNull(baselineId);
        if (!IsSupported(baselineId))
        {
            throw new ArgumentException(
                $"未知源基线 '{baselineId}'：只接受实际支持且可验证的基线 "
                + $"（{string.Join(" / ", Supported)}）。不会按名字继续跑，也不会退回当前基线",
                nameof(baselineId));
        }

        return Current;
    }
}
