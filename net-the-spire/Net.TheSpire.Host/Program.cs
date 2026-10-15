using System.Text;
using BaseSpire.SinglePlayer;
using BaseSpire.Slice;
using CoreTheSpire.Engine;
using NetTheSpire.Contracts;
using NetTheSpire.Contracts.Dto;
using NetTheSpire.Control;
using NetTheSpire.Projection;

namespace NetTheSpire.Host;

/// <summary>
/// N-04 的本机 stdio 宿主：**UTF-8 NDJSON**，一行一个完整请求／响应；**stdout 只写协议**、诊断写 stderr；
/// **帧字节上限显式配置，超限失败且不解析部分数据**（`protocol.md:221`）。
///
/// 用法：
/// <code>dotnet run --project net-the-spire/Net.TheSpire.Host -- [--frame-limit N] [--ledger-capacity N] [--scenario NAME] [--seed N] [--game-seed S] [--test-headbutt] [--author-content]</code>
///
/// 两个场景（`--scenario`，默认 `legacy-slice`）：
///
/// * `legacy-slice`（协议 **0.1**）：程序化冻结切片 `CombatSlicePackage` + `SlicePublicProfile`，
///   seed 是**数值**（`--seed`，十进制 ulong）。既有 stdio 检查与 M1 行为不变。
/// * `formal-dev`（协议 **0.2**）：base 的**唯一正式工厂** `BaseSpireRuntimeFactory`（Ironclad / 进阶 0 /
///   dev 范围），公开目录从引擎声明派生，场景 id 是构建的范围身份，每局 seed 是**游戏字符串**
///   （`--game-seed`，`start` 未带 seed 时用它当显式默认值）。这条路径**不**自己 new 会话：
///   工厂交出「已配置、尚未 Start」的句柄，控制入口只启动一次。
/// * `text-rules`（协议 **0.2**）：**通用文本规则包**场景（`--rules <入口 .spire>` 可重复 +
///   `--public-metadata <描述文件>`，同一 `--game-seed` 语义）。规则包由 `EngineDslCompiler.LoadFiles`
///   通用加载，公开目录仍走 `DerivedPublicProfile`：**新增一个公开字段或一条合法 Action 只改规则包**，
///   net 这条路径里没有逐项注册行；描述文件只提供四类不可推导元数据（内容 id、集合用途、终局胜负解释、
///   已知位置角色）。这条路径同样**不**自己 new 会话：提供方交出「已配置、尚未 Start」的句柄。
///
/// 场景的 seed 开关**互斥**：`formal-dev`/`text-rules` 下给 `--seed`、或 `legacy-slice` 下给 `--game-seed`，
/// 都在启动前显式失败（不静默忽略一个"看起来生效了"的开关）；`--rules`/`--public-metadata` 只属于
/// `text-rules`，给别的场景同样是启动前失败。
///
/// `--test-headbutt` 打开切片的**测试场景开关**（`CombatSlicePackage.Build(includeTestHeadbutt: true)`）：
/// 它把一张头槌加进抽牌堆，使"出牌中途选择 → 置顶 → 已知位置"这条路径能在真实 stdio 链路上被驱动。
/// 它**不是**原版起始卡组（默认关闭），因此用它产生的轨迹/证据必须标注为测试场景；它只属于 `legacy-slice`
/// （正式工厂不接这个开关），在 `formal-dev` 下显式拒绝。
///
/// 不可解析或超限的帧：stderr 写诊断，stdout 回一条 `request_id="&lt;malformed&gt;"` 的错误响应（协议未规定
/// 这种帧的 request_id，此处采用**非空占位**以便响应本身可被解析；见 N-04 报告）。
/// </summary>
internal static class Program
{
    private const string MalformedRequestId = "<malformed>";

    private const string LegacyScenario = "legacy-slice";

    private const string FormalScenario = "formal-dev";

    /// <summary>正式场景下 <c>start</c> 未带 seed 时的 Host 显式默认游戏 seed（与 base 里程碑 seed 同名）。</summary>
    private const string DefaultGameSeed = "ENV-IRONCLAD-00";

    public static int Main(string[] args)
    {
        var frameLimit = 1 << 20;         // 1 MiB，显式配置
        var ledgerCapacity = 4096;
        var seed = 20261001UL;
        var seedGiven = false;
        var gameSeed = DefaultGameSeed;
        var gameSeedGiven = false;
        var scenario = LegacyScenario;
        // 正式场景的范围选择（`--scope`）：默认 dev，与既有正式入口逐字相同；显式给名字才换范围。
        var scopeName = FormalDevScenario.DefaultScopeName;
        var scopeGiven = false;
        var includeTestHeadbutt = false;
        // W-01：显式选择**作者内容包**（22 张已迁卡 + 药水走作者文本）而不是程序化冻结切片。
        // 默认关闭 ⇒ 既有 stdio 检查与 M1 行为不变。
        var fromAuthorText = false;
        // text-rules：规则包入口（可重复，交给 EngineDslCompiler.LoadFiles）与公开元数据描述文件。
        var rulePaths = new List<string>();
        string? publicMetadataPath = null;
        // 注意边界：循环必须覆盖**最后一个**参数——否则把开关放在末尾（例如 `... -- --test-headbutt`）
        // 会被静默忽略，而"开关没生效"在下游表现为完全不同的行为。
        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--frame-limit" when index + 1 < args.Length:
                    frameLimit = int.Parse(args[++index]);
                    break;
                case "--ledger-capacity" when index + 1 < args.Length:
                    ledgerCapacity = int.Parse(args[++index]);
                    break;
                case "--scenario" when index + 1 < args.Length:
                    scenario = args[++index];
                    break;
                case "--seed" when index + 1 < args.Length:
                    seed = ulong.Parse(args[++index]);
                    seedGiven = true;
                    break;
                case "--game-seed" when index + 1 < args.Length:
                    gameSeed = args[++index];
                    gameSeedGiven = true;
                    break;
                case "--scope" when index + 1 < args.Length:
                    scopeName = args[++index];
                    scopeGiven = true;
                    break;
                case "--test-headbutt":
                    includeTestHeadbutt = true;
                    break;
                case "--author-content":
                    fromAuthorText = true;
                    break;
                case "--rules" when index + 1 < args.Length:
                    rulePaths.Add(args[++index]);
                    break;
                case "--public-metadata" when index + 1 < args.Length:
                    publicMetadataPath = args[++index];
                    break;
            }
        }

        if (scenario is not (LegacyScenario or FormalScenario or TextRuleScenario.Scenario))
        {
            Console.Error.WriteLine(
                $"unknown scenario '{scenario}'（可选：{LegacyScenario} / {FormalScenario} / {TextRuleScenario.Scenario}）");
            return 2;
        }

        // seed 开关按场景互斥：给错的那个不是"没生效"，而是**在启动前显式拒绝**。
        if (scenario != LegacyScenario && seedGiven)
        {
            Console.Error.WriteLine(
                $"--scenario {scenario} 不接受数值 --seed：0.2 路径的每局 seed 是游戏字符串，用 --game-seed");
            return 2;
        }

        if (scenario == LegacyScenario && gameSeedGiven)
        {
            Console.Error.WriteLine(
                $"--scenario {LegacyScenario} 不接受 --game-seed：旧切片场景的 seed 是数值，用 --seed");
            return 2;
        }

        if (scenario == FormalScenario && (includeTestHeadbutt || fromAuthorText))
        {
            Console.Error.WriteLine(
                $"--scenario {FormalScenario} 不支持 --test-headbutt/--author-content："
                + "正式工厂按正式构建组包，不接切片的测试/作者开关");
            return 2;
        }

        if (scenario == TextRuleScenario.Scenario && (includeTestHeadbutt || fromAuthorText))
        {
            Console.Error.WriteLine(
                $"--scenario {TextRuleScenario.Scenario} 不支持 --test-headbutt/--author-content："
                + "文本规则包按自己的规则入口通用加载，不接切片的测试/作者开关");
            return 2;
        }

        if (scenario != TextRuleScenario.Scenario && (rulePaths.Count > 0 || publicMetadataPath is not null))
        {
            Console.Error.WriteLine(
                $"--rules/--public-metadata 只属于 --scenario {TextRuleScenario.Scenario}："
                + $"别的场景不静默忽略这两个开关（当前 {scenario}）");
            return 2;
        }

        if (scenario == TextRuleScenario.Scenario && (rulePaths.Count == 0 || publicMetadataPath is null))
        {
            Console.Error.WriteLine(
                $"--scenario {TextRuleScenario.Scenario} 必须同时给出至少一个 --rules 与 --public-metadata");
            return 2;
        }

        // 范围开关同样按场景互斥，且名字解析失败**在组建工厂之前**返回：不静默回退到默认范围。
        if (scopeGiven && scenario != FormalScenario)
        {
            Console.Error.WriteLine(
                $"--scope 只属于 --scenario {FormalScenario}：别的场景不静默忽略这个开关（当前 {scenario}）");
            return 2;
        }

        string? scopeId = null;
        if (scenario == FormalScenario)
        {
            scopeId = FormalDevScenario.ScopeIdForName(scopeName);
            if (scopeId is null)
            {
                Console.Error.WriteLine(
                    $"unknown scope '{scopeName}'（可选："
                    + $"{string.Join(" / ", FormalDevScenario.SelectableScopes.Select(scope => scope.Name))}）");
                return 2;
            }
        }

        ControlHost control;
        string ready;
        if (scenario == FormalScenario)
        {
            var factory = FormalDevScenario.CreateFactory(scopeId!);
            var build = factory.Describe();
            // base 侧不可变公开语义元数据 → net 公开元数据的**唯一**转换点（Host 是唯一的组合点）。
            var metadata = FormalDevMetadata.FromBase(factory);
            control = FormalDevScenario.Create(factory, metadata, gameSeed, ledgerCapacity);
            ready = $"net-the-spire host ready: scenario={FormalScenario} protocol={control.ProtocolVersion} "
                + $"scope={control.SupportedScenarioProfile} scope_name={scopeName} build={build.PublicBuildId} "
                + $"ruleset={control.Profile.RulesetId} projection={BaseSpireRuntimeFactory.ProjectionId} "
                + $"rng_profile={build.RandomDrawProfileId} "
                + $"default_game_seed={gameSeed} frame_limit={frameLimit}";
        }
        else if (scenario == TextRuleScenario.Scenario)
        {
            // 通用文本规则包路径：规则包通用加载，公开目录从引擎声明 + 规则包自己的投影派生。
            // `--game-seed` 显式给出时覆盖描述文件里的默认游戏 seed（不静默忽略一个看起来生效的开关）。
            var spec = TextRuleProviderSpec.Load(publicMetadataPath!);
            var defaultGameSeed = gameSeedGiven ? gameSeed : spec.DefaultGameSeed;
            var setup = TextRuleScenario.Create(rulePaths, spec, ledgerCapacity, defaultGameSeed);
            control = setup.Host;
            ready = $"net-the-spire host ready: scenario={TextRuleScenario.Scenario} protocol={control.ProtocolVersion} "
                + $"scope={control.SupportedScenarioProfile} build={setup.PublicBuildIdentity} "
                + $"ruleset={control.Profile.RulesetId} projection={spec.ProjectionId} "
                + $"rng_profile={control.SourceForTests!.RngProfileId} "
                + $"default_game_seed={defaultGameSeed} rules={string.Join(",", rulePaths)} frame_limit={frameLimit}";
        }
        else
        {
            var builder = new RuleSetBuilder();
            builder.Add(CombatSlicePackage.Build(includeTestHeadbutt, fromAuthorText: fromAuthorText).Build());
            var rules = builder.Build();
            // W-01：包/Profile 显式选择——`--author-content` 同时选择**作者 Profile 变体**（身份随之变化，可核对）。
            // 作者变体需要规则集：药水栏的容量等公开事实从**引擎声明**读，不在 Profile 里另抄数字。
            var profile = fromAuthorText ? SlicePublicProfile.CreateAuthorContent(rules) : SlicePublicProfile.Create();
            control = new ControlHost(rules, profile, SliceManifest.Player, SliceManifest.PlayerRole,
                "base-spire:PlayerView", seed, ledgerCapacity,
                // 旧切片场景同样要装规则包声明的计算资产后端（源预排序/加权选取）：这一行是 Host 作为
                // **唯一组合点**的职责，控制层只接收通用后端实例。
                computeBackend: BaseSpireComputeBackend.Instance);
            ready = $"net-the-spire host ready: scenario={LegacyScenario} protocol={control.ProtocolVersion} "
                + $"ruleset={profile.RulesetId} frame_limit={frameLimit} "
                + $"test_headbutt={includeTestHeadbutt} author_content={fromAuthorText}";
        }

        Console.Error.WriteLine(ready);
        using var input = Console.OpenStandardInput();
        using var output = Console.OpenStandardOutput();
        foreach (var frame in ReadFrames(input, frameLimit))
        {
            var line = Handle(control, frame, frameLimit);
            var bytes = new UTF8Encoding(false).GetBytes(line + "\n");
            output.Write(bytes, 0, bytes.Length);
            output.Flush();
        }

        return 0;
    }

    /// <summary>逐帧读取：每个 `\n` 分隔的原始字节就是一个帧；超过上限即在该帧上失败，不解析部分数据。</summary>
    private static IEnumerable<Frame> ReadFrames(Stream input, int frameLimit)
    {
        var buffer = new List<byte>();
        var pending = new MemoryStream();
        var chunk = new byte[8192];
        int read;
        while ((read = input.Read(chunk, 0, chunk.Length)) > 0)
        {
            for (var index = 0; index < read; index++)
            {
                if (chunk[index] == (byte)'\n')
                {
                    yield return new Frame(pending.ToArray());
                    pending.SetLength(0);
                    continue;
                }

                if (pending.Length >= frameLimit)
                {
                    // 已超限：丢弃该帧剩余字节直到换行，且**不解析**已读到的部分。
                    while (index < read && chunk[index] != (byte)'\n')
                    {
                        index++;
                    }

                    yield return new Frame(null);
                    pending.SetLength(0);
                    continue;
                }

                pending.WriteByte(chunk[index]);
            }
        }

        if (pending.Length > 0)
        {
            yield return new Frame(pending.ToArray());
        }
    }

    private static string Handle(ControlHost control, Frame frame, int frameLimit)
    {
        if (frame.Bytes is null)
        {
            Console.Error.WriteLine($"frame rejected: exceeds frame_limit={frameLimit}");
            return ErrorLine(MalformedRequestId, "capacity_exceeded",
                $"帧超过配置上限 {frameLimit} 字节：不解析部分数据", "stop", "not_started");
        }

        if (frame.Bytes.Length == 0)
        {
            return ErrorLine(MalformedRequestId, "invalid_input", "空帧", "correct_input", "not_started");
        }

        RequestDto request;
        try
        {
            // 载荷校验必须按**本运行时实际选择的协议版本**做：0.1 的 `start.seed` 是规范十进制字符串、
            // 0.2 是非空游戏字符串（protocol.md §7）。用默认分支（0.1）会让正式场景的游戏 seed 在入口
            // 就被当成非法输入挡下——那不是"严格"，而是拿错版本校验。
            request = RequestDto.FromWire(
                WireParser.ParseUtf8Strict(frame.Bytes, "request"), control.ProtocolVersion);
        }
        catch (Exception exception)
        {
            // 帧本身可能是合法 JSON、但载荷不符合该 operation 的 schema：这时**真实 request_id 仍可提取**，
            // 必须保留它，否则消费端无法把错误关联到自己的请求（只有连 request_id 都读不到时才用占位符）。
            var requestId = ExtractRequestId(frame.Bytes);
            Console.Error.WriteLine($"frame rejected ({requestId}): {exception.Message}");
            return ErrorLine(requestId, "invalid_input", exception.Message, "correct_input", "not_started");
        }

        try
        {
            return WireCanonical.Text(control.Dispatch(request).ToWire());
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"dispatch failed: {exception}");
            return ErrorLine(request.RequestId, "backend_fault", exception.Message, "stop", "unknown");
        }
    }

    /// <summary>从原始帧里尽量取出 request_id（宽松解析），取不到才用占位符。</summary>
    private static string ExtractRequestId(byte[] bytes)
    {
        try
        {
            var value = WireParser.ParseLenient(Encoding.UTF8.GetString(bytes), "request");
            if (value is WireObject obj)
            {
                foreach (var member in obj.Members)
                {
                    if (member.Key == "request_id" && member.Value is WireString text && text.Value.Length > 0)
                    {
                        return text.Value;
                    }
                }
            }
        }
        catch
        {
            // 落回占位符
        }

        return MalformedRequestId;
    }

    private static string ErrorLine(string requestId, string code, string message, string retry, string execution)
    {
        var response = new ResponseDto(requestId, false, null,
            new ErrorDto(code, message, null, null, null, execution, retry));
        return WireCanonical.Text(response.ToWire());
    }

    private readonly record struct Frame(byte[]? Bytes);
}
