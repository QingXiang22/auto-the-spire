namespace NetTheSpire.Tests;

/// <summary>
/// net-the-spire 契约层检查入口（仿照 <c>Core.TheSpire.Tests</c> 的可执行检查约定）：
/// <c>dotnet run --project net-the-spire/Net.TheSpire.Tests</c>。
/// 逐条打印 <c>ok   &lt;名字&gt;</c>，失败打印 <c>FAIL &lt;名字&gt;: 详情</c>（stderr）并继续执行，
/// 末行给出 TOTAL 计数，任一条失败即返回非零退出码。
/// </summary>
internal static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length > 0 && (args.Length != 1 || args[0] != "--base-dsl-only"))
        {
            Console.Error.WriteLine("Usage: Net.TheSpire.Tests [--base-dsl-only]");
            return 2;
        }
        // Migration gate: generic wire and text-rule behavior. Retired base checks remain individually inventoried.
        var checks = (args.Length == 1 ? BaseDslPathChecks.Build() :
            CrossEndChecks.Build().Concat(DeclarationExtensionChecks.Build()).Concat(BaseDslPathChecks.Build())).ToList();
        var passed = 0;
        var failed = 0;
        foreach (var (name, body) in checks)
        {
            try
            {
                Check.Run(name, body);
                passed++;
            }
            catch (CheckFailedException failure)
            {
                failed++;
                Console.Error.WriteLine($"FAIL {failure.Message}");
            }
        }

        var executed = passed + failed;
        Console.WriteLine($"net-the-spire checks (generic wire + text rules + pure DSL path): {passed} passed / {failed} failed / {executed} executed.");
        Console.WriteLine($"TOTAL: {executed} executed, {passed} passed, {failed} failed.");        return passed > 0 && failed == 0 ? 0 : 1;
    }
}
