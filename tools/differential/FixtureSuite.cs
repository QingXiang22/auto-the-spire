using System.Text.Json;
using System.Text.Json.Serialization;

namespace Differential;

/// <summary>夹具里的一张牌：定义 id、是否升级、以及需要显式指定的数值/费用（升级形态必须显式给，不猜）。</summary>
internal sealed record FixtureCard(
    string Card,
    bool Upgraded = false,
    int? Amount = null,
    int? PowerStacks = null,
    int? Cost = null);

/// <summary>脚本前已经持有的能力；按各侧声明/源Power保留持续行为，初始不跳过拥有者回合末，不模拟新施加。</summary>
internal sealed record FixturePower(string Owner, string Power, long Stacks);

/// <summary>受控初始状态（手工设定）：只用于机制夹具，**不能**当正常开局胜负证据（ENV §5/§6.1）。</summary>
internal sealed record FixtureSetup(
    bool IncludeHeadbutt = false,
    int Seed = 20261001,
    IReadOnlyList<FixtureCard>? Hand = null,
    IReadOnlyList<FixtureCard>? Discard = null,
    IReadOnlyList<int>? EnemyHp = null,
    int? PlayerHp = null,
    int? Energy = null,
    IReadOnlyList<FixturePower>? Powers = null,
    InitialPiles? InitialPiles = null);

/// <summary>
/// 一个语义动作（两后端接受**同一个**决策序列；不含任何后端原生句柄）。
/// <paramref name="Action"/> 取值：<c>play_card</c> / <c>end_turn</c>。
/// <paramref name="Target"/> 是第一个决策点的选择，<paramref name="Choose"/> 是后续决策点的选择
/// （都用语义槽位名；缺省取候选首项）。
/// <paramref name="ObserveDrawTop"/> 只在该步骤结算边界读取实际抽牌堆顶，不比较受控开局的随机牌序。
/// </summary>
internal sealed record FixtureStep(
    string Action,
    string? Card = null,
    string? Target = null,
    string? Choose = null,
    bool ObserveDrawTop = false);

internal sealed record FixtureProvenance(string Kind, string Source, string Note);

/// <summary>base 侧会话的来源：它决定"这一侧的 Session 是怎么造出来的"（ENV-T04 接线证据）。</summary>
internal static class BaseSources
{
    /// <summary>
    /// 受控机制会话：用<strong>与工厂相同的公开配置资产</strong>
    /// （<c>VanillaRandomLayout</c> 的算法/映射/Profile + <c>SeedAllStreams</c> + <c>SourceWeightedPickBackend</c>）
    /// 配置受控切片包，然后 Start 一次。参考侧是单场战斗适配器，只有这一条来源能与它逐边界对应。
    /// </summary>
    public const string ControlledSlice = "controlled-slice-v1";

    /// <summary>
    /// 正式开局会话：<c>BaseSpireRuntimeFactory</c> + <c>RunConfiguration</c>（dev 范围）→
    /// <c>CreateSession(gameSeed)</c> → Start 一次。不做任何受控初始化，用于正常开局证据与工厂接线。
    /// </summary>
    public const string FormalFactory = "formal-dev-factory";

    public static readonly IReadOnlyList<string> All = [ControlledSlice, FormalFactory];
}

/// <summary>base 侧正式工厂的 range（决定建局时启用哪个 scope 的规则包）。</summary>
internal static class BaseScopes
{
    /// <summary>`BaseSpireScope.Dev`（默认，既有全部 case 不变）。</summary>
    public const string Dev = "dev";

    /// <summary>`BaseSpireScope.DevMap`：T05 地图生成/物化与 ChooseMapPoint 已接入的范围。</summary>
    public const string DevMap = "dev-map";

    public static readonly IReadOnlyList<string> All = [Dev, DevMap];
}

/// <summary>case 种类。受控机制与正常开局是不同 kind，不能混算（ENV 约束 §9）。</summary>
internal static class CaseKinds
{
    /// <summary>受控初始化 + 真实规则执行：只证明机制，不当正常开局胜负证据。</summary>
    public const string Mechanism = "mechanism";

    /// <summary>正常开局（无受控初始化），用于开局/整局路径。</summary>
    public const string Run = "run";

    /// <summary>工具/工厂接线检查：不是产品内容覆盖，也不是规则对拍。</summary>
    public const string Wiring = "wiring";

    /// <summary>真实地图生成机制：base 建局后的地图公开投影 vs 参考独立生成的地图（ENV-T04 地图包）。</summary>
    public const string Map = "map";

    public const string NormalReward = "normal-reward";
    public static readonly IReadOnlyList<string> All = [Mechanism, Run, Wiring, Map, NormalReward];
}

/// <summary>参考侧范围。<c>compared</c> 以外的值都**不计通过**，且必须写明理由。</summary>
internal static class ReferenceScopes
{
    /// <summary>两侧都跑并逐边界比较。</summary>
    public const string Compared = "compared";

    /// <summary>两个后端的决策模型暂时映射不上：只跑 base 侧，判定不计通过。</summary>
    public const string UnsupportedDecisionModel = "unsupported_decision_model";

    /// <summary>参考侧缺这类能力/入口（例如没有整局与房间驱动）：只跑 base 侧，记 blocked_compare。</summary>
    public const string BlockedCompare = "blocked_compare";

    /// <summary>本 case 没有参考侧对应物（工具/工厂接线检查）。</summary>
    public const string NotApplicable = "not_applicable";

    public static readonly IReadOnlyList<string> All =
        [Compared, UnsupportedDecisionModel, BlockedCompare, NotApplicable];
}

/// <summary>必需比较维度：声明了就必须真的抽取出来，否则严格入口失败（ENV 约束 §9）。</summary>
internal static class Dimensions
{
    public const string Actions = "actions";
    public const string Decisions = "decisions";
    public const string Facts = "facts";
    public const string PileOrder = "pile-order";
    public const string Power = "power";
    public const string Streams = "streams";
    public const string Trace = "trace";
    public const string Terminal = "terminal";

    public const string Rewards = "rewards";
    public const string Permanent = "permanent-inventory";
    public const string RewardClosure = "reward-closure";
    public static readonly IReadOnlyList<string> All =
        [Actions, Decisions, Facts, PileOrder, Power, Streams, Trace, Terminal, Rewards, Permanent, RewardClosure];
}

/// <summary>预期终点：严格入口按它核对 base 侧实际状态（错误终点直接非零）。</summary>
internal static class ExpectedTerminals
{
    /// <summary>会话仍在运行（停在某个可继续的稳定边界）。</summary>
    public const string Running = "running";

    /// <summary>会话已到声明的胜利终局。</summary>
    public const string Victory = "victory";

    /// <summary>会话已到声明的失败终局。</summary>
    public const string Defeat = "defeat";
}

/// <summary>
/// 一个 case。前八个字段是**可执行的夹具定义**，其后是**严格 suite 的预登记元数据**
/// （ENV 约束 §9：case 固定 id、kind、内容与变体/机制、角色/进阶/seed、初始化来源、
/// 动作脚本、必需维度及预期终点，并给出独立参考来源）。
/// </summary>
internal sealed record FixtureCase(
    string Id,
    string Title,
    string RngProfile,
    FixtureProvenance Provenance,
    FixtureSetup Setup,
    IReadOnlyList<FixtureStep> Script,
    string ReferenceScope = ReferenceScopes.Compared,
    string? ReferenceScopeReason = null,
    // -- 严格 suite 的注册字段（宽松夹具可留默认；严格 suite 必须逐条给出） ----------------------------
    string Group = "existing-regression",
    string Kind = CaseKinds.Mechanism,
    string BaseSource = BaseSources.ControlledSlice,
    string BaseScope = BaseScopes.Dev,
    string? GameSeed = null,
    string? Initialization = null,
    IReadOnlyList<string>? Contents = null,
    IReadOnlyList<string>? Variants = null,
    IReadOnlyList<string>? RequiredDimensions = null,
    IReadOnlyList<string>? SemanticActions = null,
    string? ExpectedTerminal = null,
    string? ReferenceSource = null);

/// <summary>suite 级别的构建身份：同一 suite 版本必须绑定同一构建/配置/参考身份。</summary>
internal sealed record SuiteBuildIdentity(
    string Scope,
    string Character,
    int Ascension,
    string SourceBaseline,
    string Reference,
    string Note);

internal sealed record FixtureSuite(
    string Suite,
    string FixturesVersion,
    string Note,
    IReadOnlyList<FixtureCase> Cases)
{
    /// <summary>目标范围（严格 suite 必填）。</summary>
    public string? TargetScope { get; init; }

    /// <summary>本 suite 的**必测 case ID 分母**；严格入口要求它与 case 集合一致（ENV §6.4）。</summary>
    public IReadOnlyList<string>? ExpectedCaseIds { get; init; }

    /// <summary>构建/配置/参考身份。</summary>
    public SuiteBuildIdentity? BuildIdentity { get; init; }

    /// <summary>严格 suite：声明了必测分母，因此缺 case/零执行/未注册 case 都要失败。</summary>
    [JsonIgnore]
    public bool IsStrict => ExpectedCaseIds is { Count: > 0 };
}

internal static class FixtureSuites
{
    public static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>
    /// `--suite regression` → 先找 <c>tools/differential/suites/regression.json</c>，
    /// 再退回 <c>tools/differential/fixtures/regression.json</c>；也接受直接给的路径或带 <c>.json</c> 的名字。
    /// </summary>
    public static string ResolvePath(string repositoryRoot, string suite)
    {
        if (suite.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            || suite.Contains('/') || suite.Contains('\\'))
        {
            return Path.IsPathRooted(suite)
                ? suite
                : Path.Combine(repositoryRoot, suite.Replace('/', Path.DirectorySeparatorChar));
        }

        var strict = Path.Combine(repositoryRoot, "tools", "differential", "suites", suite + ".json");
        return File.Exists(strict)
            ? strict
            : Path.Combine(repositoryRoot, "tools", "differential", "fixtures", suite + ".json");
    }

    public static FixtureSuite Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"找不到夹具套件：{path}", path);
        }

        return JsonSerializer.Deserialize<FixtureSuite>(File.ReadAllText(path), Json)
            ?? throw new InvalidOperationException($"夹具套件为空：{path}");
    }

    /// <summary>套件自检：id 唯一、每个 case 都带来源与 RNG 声明、脚本里的引用非空；严格 suite 另查注册字段。</summary>
    public static IReadOnlyList<string> Validate(FixtureSuite suite)
    {
        var problems = new List<string>();
        if (suite.Cases.Count == 0)
        {
            problems.Add("套件里没有任何 case");
        }

        foreach (var group in suite.Cases.GroupBy(item => item.Id, StringComparer.Ordinal))
        {
            if (group.Count() > 1)
            {
                problems.Add($"case id 重复：{group.Key}");
            }
        }

        foreach (var fixture in suite.Cases)
        {
            if (fixture.Setup.InitialPiles is not null || fixture.RngProfile == RngProfiles.ControlledStateNoRandom)
            {
                if (!ControlledInit.AllowedCases.Contains(fixture.Id)) problems.Add($"{fixture.Id}: 未授权完整受控初始化");
                if (string.IsNullOrWhiteSpace(fixture.GameSeed)) problems.Add($"{fixture.Id}: 缺少原 game_seed");
                try { ControlledInit.Validate(fixture.Setup); }
                catch (CheckFailed failure) { problems.Add($"{fixture.Id}: {failure.Message}"); }
            }

            if (string.IsNullOrWhiteSpace(fixture.RngProfile))
            {
                problems.Add($"{fixture.Id}: 缺少 rng_profile 声明");
            }

            if (string.IsNullOrWhiteSpace(fixture.Provenance?.Source))
            {
                problems.Add($"{fixture.Id}: 缺少来源（provenance.source）");
            }

            foreach (var (step, index) in fixture.Script.Select((step, index) => (step, index)))
            {
                switch (step.Action)
                {
                    case "play_card" when string.IsNullOrWhiteSpace(step.Card):
                        problems.Add($"{fixture.Id}: 第 {index} 步 play_card 缺少 card");
                        break;
                    case "normal_reward_take" or "normal_reward_skip" when fixture.Kind == CaseKinds.NormalReward:
                    case "play_card" or "end_turn":
                        break;
                    default:
                        problems.Add($"{fixture.Id}: 第 {index} 步未知动作 '{step.Action}'");
                        break;
                }
            }

            if (!ReferenceScopes.All.Contains(fixture.ReferenceScope, StringComparer.Ordinal))
            {
                problems.Add($"{fixture.Id}: 未知 reference_scope '{fixture.ReferenceScope}'");
            }

            // `end_turn` 会把控制权交给**对端自己的**随机序列（选招、洗牌/抽牌顺序），
            // 在 ENV-01 提供原版兼容随机之前这样的 case 不可能逐步可比。
            // 我们这一侧的"没有消费随机"实测不足以证明可比（例如切片从固定牌序抽牌，
            // 抽牌不消费随机，而参考侧抽的是它自己洗过的牌序）。因此把它变成**被检查的声明规则**，
            // 而不是靠"记得声明成 unsupported"。
            if (fixture.RngProfile == RngProfiles.ControlledNoRandom
                && fixture.Script.Any(step => step.Action == "end_turn"))
            {
                problems.Add($"{fixture.Id}: 脚本含 end_turn 就不能声明为 {RngProfiles.ControlledNoRandom}"
                    + $"（回合切换会走对端私有随机序列）：改用 {RngProfiles.UnsupportedRngProfile}，"
                    + "或在 ENV-01 的兼容 Profile 下再做可计入对拍");
            }

            if (fixture.ReferenceScope != ReferenceScopes.Compared
                && string.IsNullOrWhiteSpace(fixture.ReferenceScopeReason))
            {
                problems.Add($"{fixture.Id}: reference_scope={fixture.ReferenceScope} 必须写明理由");
            }

            if (suite.IsStrict)
            {
                problems.AddRange(ValidateRegistration(fixture));
            }
        }

        if (suite.IsStrict)
        {
            problems.AddRange(ValidateStrictSuite(suite));
        }

        return problems;
    }

    /// <summary>严格 suite 的 case 级注册检查：预登记元数据必须齐全且可判别。</summary>
    private static IEnumerable<string> ValidateRegistration(FixtureCase fixture)
    {
        if (!CaseKinds.All.Contains(fixture.Kind, StringComparer.Ordinal))
        {
            yield return $"{fixture.Id}: 未知 kind '{fixture.Kind}'（可选：{string.Join(" / ", CaseKinds.All)}）";
        }

        if (!BaseSources.All.Contains(fixture.BaseSource, StringComparer.Ordinal))
        {
            yield return $"{fixture.Id}: 未知 base_source '{fixture.BaseSource}'"
                + $"（可选：{string.Join(" / ", BaseSources.All)}）";
        }

        if (!BaseScopes.All.Contains(fixture.BaseScope, StringComparer.Ordinal))
        {
            yield return $"{fixture.Id}: 未知 base_scope '{fixture.BaseScope}'"
                + $"（可选：{string.Join(" / ", BaseScopes.All)}）";
        }

        if (fixture.Kind == CaseKinds.Map
            && (fixture.BaseSource != BaseSources.FormalFactory || string.IsNullOrWhiteSpace(fixture.GameSeed)))
        {
            yield return $"{fixture.Id}: kind={CaseKinds.Map} 必须用 base_source={BaseSources.FormalFactory}"
                + " 并给出每局字符串 game_seed（地图来自真实工厂的正常开局）";
        }

        if (fixture.RequiredDimensions is not { Count: > 0 })
        {
            yield return $"{fixture.Id}: 严格 suite 必须声明 required_dimensions（声明了就必须真的抽取）";
        }
        else
        {
            foreach (var dimension in fixture.RequiredDimensions)
            {
                if (!Dimensions.All.Contains(dimension, StringComparer.Ordinal))
                {
                    yield return $"{fixture.Id}: 未知 required_dimension '{dimension}'"
                        + $"（可选：{string.Join(" / ", Dimensions.All)}）";
                }
            }
        }

        if (string.IsNullOrWhiteSpace(fixture.ExpectedTerminal))
        {
            yield return $"{fixture.Id}: 严格 suite 必须声明 expected_terminal";
        }
        else if (fixture.ExpectedTerminal is not (ExpectedTerminals.Running
                 or ExpectedTerminals.Victory or ExpectedTerminals.Defeat))
        {
            yield return $"{fixture.Id}: 未知 expected_terminal '{fixture.ExpectedTerminal}'";
        }

        if (string.IsNullOrWhiteSpace(fixture.Initialization))
        {
            yield return $"{fixture.Id}: 必须声明初始化来源（initialization）";
        }

        if (fixture.BaseSource == BaseSources.FormalFactory && string.IsNullOrWhiteSpace(fixture.GameSeed))
        {
            yield return $"{fixture.Id}: base_source={BaseSources.FormalFactory} 必须给出每局字符串 game_seed";
        }

        if (fixture.Kind == CaseKinds.Wiring && fixture.ReferenceScope != ReferenceScopes.NotApplicable)
        {
            yield return $"{fixture.Id}: kind=wiring 的 reference_scope 必须是"
                + $" {ReferenceScopes.NotApplicable}（工具/工厂接线检查不是规则对拍）";
        }

        if (fixture.Kind != CaseKinds.Wiring
            && fixture.ReferenceScope == ReferenceScopes.NotApplicable)
        {
            yield return $"{fixture.Id}: 只有 kind=wiring 可以声明 reference_scope={ReferenceScopes.NotApplicable}";
        }

        if (fixture.Contents is not { Count: > 0 })
        {
            yield return $"{fixture.Id}: 必须登记本 case 作用的内容/机制（contents）";
        }

        if (string.IsNullOrWhiteSpace(fixture.ReferenceSource))
        {
            yield return $"{fixture.Id}: 必须登记独立参考来源（reference_source）";
        }
    }

    /// <summary>严格 suite 的分母检查：必测 ID 与实际 case 集合必须一致（缺 case 直接失败）。</summary>
    private static IEnumerable<string> ValidateStrictSuite(FixtureSuite suite)
    {
        var expected = suite.ExpectedCaseIds!;
        foreach (var group in expected.GroupBy(item => item, StringComparer.Ordinal))
        {
            if (group.Count() > 1)
            {
                yield return $"expected_case_ids 重复：{group.Key}";
            }
        }

        var present = suite.Cases.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var id in expected.Where(id => !present.Contains(id)))
        {
            yield return $"必测 case 缺失：'{id}' 在 expected_case_ids 里，但 suite 里没有可执行夹具";
        }

        foreach (var id in present.Where(id => !expected.Contains(id, StringComparer.Ordinal)))
        {
            yield return $"未注册 case：'{id}' 在 suite 里，但没有进 expected_case_ids"
                + "（新 case 必须显式登记，不能靠『出现在文件里』扩大分母）";
        }

        if (suite.BuildIdentity is null)
        {
            yield return "严格 suite 必须声明 build_identity（范围/角色/进阶/源基线/参考身份）";
        }
        else
        {
            if (string.IsNullOrWhiteSpace(suite.TargetScope))
            {
                yield return "严格 suite 必须声明 target_scope";
            }

            if (suite.BuildIdentity.Scope != suite.TargetScope)
            {
                yield return $"build_identity.scope '{suite.BuildIdentity.Scope}'"
                    + $" 与 target_scope '{suite.TargetScope}' 不一致";
            }
        }
    }
}
