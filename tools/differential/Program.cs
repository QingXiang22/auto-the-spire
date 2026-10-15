namespace Differential;

/// <summary>
/// ENV：完整单人环境与源规则对拍的工具入口（`tools/differential`，独立 C#/.NET 9 程序）。
///
/// 它**不是生产后端**：只做两后端的适配、标准动作/诊断快照、逐步比较与首差、有限生成与最小复现。
/// 参考实现的 ProjectReference 只允许出现在这里与工具测试里，不进入 core/base/net 的生产依赖。
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            Usage();
            return 2;
        }

        var command = args[0];
        var options = ParseOptions(args[1..]);

        switch (command)
        {
            case "inventory":
                return InventoryCommand.Run(Option(options, "output") ?? "artifacts/environment/inventory");

            case "compare":
                return CompareCommand.Run(
                    Repo.Root(),
                    Option(options, "suite") ?? "fixtures-v1",
                    Option(options, "output") ?? "artifacts/environment/fixtures",
                    Option(options, "records"));

            case "test":
            {
                var timeout = TestCommand.DefaultCaseTimeoutMs;
                if (Option(options, "case-timeout-ms") is { } raw
                    && (!int.TryParse(raw, out timeout) || timeout <= 0))
                {
                    Console.Error.WriteLine($"--case-timeout-ms 需要一个正整数毫秒值，收到 '{raw}'。");
                    return 2;
                }

                return TestCommand.Run(
                    Repo.Root(),
                    Option(options, "suite") ?? "regression",
                    Option(options, "output") ?? "artifacts/environment/tests/regression",
                    Option(options, "only"),
                    Option(options, "records"),
                    timeout);
            }

            case "rng":
                return ReferenceRngCrossCheck.Run(
                    Option(options, "vectors")
                        ?? Path.Combine(Repo.Root(), "artifacts", "environment", "vanilla-rng-vectors.json"),
                    Option(options, "output") ?? "artifacts/environment/rng",
                    Option(options, "layout")
                        ?? Path.Combine(Repo.Root(), "artifacts", "environment", "vanilla-rng-layout.json"),
                    Option(options, "source-compat")
                        ?? Path.Combine(Repo.Root(), "artifacts", "environment", "rng-source-compat.json"));

            case "init-upfront":
                return ReferenceInitDiagnosis.Run(
                    Option(options, "seed"),
                    Option(options, "output") ?? "artifacts/environment/init-diagnosis",
                    Option(options, "records"));

            case "verify":
                return VerifyCommand.Run(
                    Repo.Root(),
                    Option(options, "inventory")
                        ?? Path.Combine(Repo.Root(), "artifacts", "environment", "inventory", "inventory.json"),
                    Option(options, "fixtures")
                        ?? Path.Combine(Repo.Root(), "artifacts", "environment", "fixtures", "comparison.json"),
                    Option(options, "output") ?? "artifacts/environment/verify",
                    Option(options, "behavior"));

            case "run":
                return RunCommand.Run(
                    Option(options, "output") ?? Path.Combine(Repo.Root(), "artifacts", "environment", "run"),
                    Option(options, "coverage")
                        ?? Path.Combine(Repo.Root(), "artifacts", "environment", "verify", "coverage.json"));

            case "-h":
            case "--help":
            case "help":
                Usage();
                return 0;

            default:
                Console.Error.WriteLine($"未知命令 '{command}'。");
                Usage();
                return 2;
        }
    }

    private static void Usage()
    {
        Console.Error.WriteLine("用法：");
        Console.Error.WriteLine("  dotnet run --project tools/differential -- inventory [--output <目录>]");
        Console.Error.WriteLine("  dotnet run --project tools/differential -- compare --suite <夹具套件> [--records <参考记录目录>] [--output <目录>]");
        Console.Error.WriteLine("  dotnet run --project tools/differential -- test --suite regression [--only <id,id>] [--records <外部参考记录目录>] [--output <目录>] [--case-timeout-ms <毫秒>]");
        Console.Error.WriteLine("      严格入口：只接受声明 expected_case_ids 的 suite；缺 case/零执行/缺参考/unsupported/异常/超时/首差/错误终点/缺维度都非零。");
        Console.Error.WriteLine("  dotnet run --project tools/differential -- rng [--vectors <神谕向量 JSON>] [--layout <布局向量 JSON>] [--source-compat <源版本 Profile 向量 JSON>] [--output <目录>]");
        Console.Error.WriteLine("  dotnet run --project tools/differential -- init-upfront --seed <原始字符串 game seed> [--records <记录目录>] [--output <目录>]");
        Console.Error.WriteLine("      初始化首差诊断（只读）：参考开局各阶段 UpFront 计数、阶段重放分解、池全量、两侧记录里的 up_front 次数与首个战斗事实。");
        Console.Error.WriteLine("  dotnet run --project tools/differential -- verify [--inventory <inventory.json>] [--fixtures <comparison.json>] [--output <目录>]");
        Console.Error.WriteLine("  dotnet run --project tools/differential -- run --character <角色> --ascension <n> --seed <种子> [--output <目录>]   （未实现）");
        Console.Error.WriteLine("  dotnet run --project tools/differential -- verify --manifest <环境清单> [--output <目录>]   （未实现）");
    }

    private static Dictionary<string, string> ParseOptions(string[] args)
    {
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            if (!argument.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            var key = argument[2..];
            var value = index + 1 < args.Length && !args[index + 1].StartsWith("--", StringComparison.Ordinal)
                ? args[++index]
                : "true";
            options[key] = value;
        }

        return options;
    }

    private static string? Option(IReadOnlyDictionary<string, string> options, string key) =>
        options.TryGetValue(key, out var value) ? value : null;
}
