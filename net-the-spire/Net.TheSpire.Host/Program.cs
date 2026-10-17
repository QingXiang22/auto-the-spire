using System.Text;
using CoreTheSpire.Engine;
using NetTheSpire.Contracts;
using NetTheSpire.Contracts.Dto;
using NetTheSpire.Control;
using NetTheSpire.Projection;

namespace NetTheSpire.Host;

/// <summary>UTF-8 NDJSON host for declarative rule packages. No game-rule builder or fallback.</summary>
internal static class Program
{
    private const string MalformedRequestId = "<malformed>";

    public static int Main(string[] args)
    {
        try { return Run(args); }
        catch (NotSupportedException error) { Console.Error.WriteLine(error.Message); return 3; }
        catch (Exception error) when (error is FormatException or ArgumentException or IOException or KeyNotFoundException or InvalidOperationException)
        { Console.Error.WriteLine(error.Message); return 2; }
    }

    private static int Run(string[] args)
    {
        var frameLimit = 1 << 20;
        var ledgerCapacity = 4096;
        var scenario = TextRuleScenario.Scenario;
        string? gameSeed = null, scope = null, metadataPath = null, packagePath = null;
        var rulePaths = new List<string>();
        for (var index = 0; index < args.Length; index++)
        {
            string Input() => index + 1 < args.Length ? args[++index] : throw new ArgumentException($"{args[index]} requires a value.");
            switch (args[index])
            {
                case "--scenario": scenario = Input(); break;
                case "--package": packagePath = Input(); break;
                case "--scope": scope = Input(); break;
                case "--rules": rulePaths.Add(Input()); break;
                case "--public-metadata": metadataPath = Input(); break;
                case "--game-seed": gameSeed = Input(); break;
                case "--frame-limit": frameLimit = int.Parse(Input()); break;
                case "--ledger-capacity": ledgerCapacity = int.Parse(Input()); break;
                case "--seed" or "--test-headbutt" or "--author-content":
                    throw new NotSupportedException("unsupported_capability: legacy slice flags are not supported by the DSL host.");
                default: throw new ArgumentException($"Unknown option: {args[index]}");
            }
        }
        if (scenario != TextRuleScenario.Scenario)
            throw new NotSupportedException($"unsupported_capability: scenario '{scenario}' is not migrated; only text-rules is supported. No legacy fallback.");
        if (frameLimit < 1 || ledgerCapacity < 1) throw new ArgumentException("Frame and ledger limits must be positive.");
        TextRuleScenarioSetup setup;
        if (rulePaths.Count > 0)
        {
            if (metadataPath is null || packagePath is not null || scope is not null)
                throw new ArgumentException("--rules requires --public-metadata and cannot be combined with --package/--scope.");
            var spec = TextRuleProviderSpec.Load(metadataPath);
            setup = TextRuleScenario.Create(rulePaths, spec, ledgerCapacity, gameSeed);
        }
        else
        {
            if (metadataPath is not null) throw new ArgumentException("--public-metadata requires --rules; packages declare their own metadata.");
            setup = TextRuleScenario.CreatePackage(packagePath ?? "mod-the-spire/base-spire/package.json", scope, ledgerCapacity, gameSeed);
        }
        var control = setup.Host;
        Console.Error.WriteLine($"net-the-spire host ready: scenario=text-rules protocol={control.ProtocolVersion} scope={control.SupportedScenarioProfile} build={setup.PublicBuildIdentity} frame_limit={frameLimit}");
        using var input = Console.OpenStandardInput();
        using var output = Console.OpenStandardOutput();
        foreach (var frame in ReadFrames(input, frameLimit))
        {
            var bytes = new UTF8Encoding(false).GetBytes(Handle(control, frame, frameLimit) + "\n");
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
        catch (NotSupportedException exception)
        {
            return ErrorLine(request.RequestId, "unsupported_capability", exception.Message, "correct_input", "not_started");
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
