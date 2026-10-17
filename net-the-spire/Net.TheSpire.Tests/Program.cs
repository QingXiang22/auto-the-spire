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
        // ENV-T03 诊断入口：只打印正式 dev 工厂的引擎声明，不参与检查计数（见 DeclarationDump）。
        if (args.Length > 0 && args[0] == "--dump-declarations") return DeclarationDump.Run();

        // 六组检查共用同一计数与退出码约定：跨端 wire 对照（N-01）+ 投影映射（N-02）+ 会话账本（N-03）
        // + 接口验收（N-05：V-01 至 V-10 与 V-15 的适用项）+ 正式 Host 路径（ENV-T03：正式工厂/0.2 seed/公开轨迹）
        // + 声明扩展实证（ENV-T03：同一通用文本规则提供方跑 baseline/extended 两个规则包）
        // + 声明闭合判别（I-71/I-84：describe 的 declaration_ids 与同一 get_catalog 闭合，正反例可区分）。
        var checks = CrossEndChecks.Build().Concat(SnapshotChecks.Build()).Concat(ControlChecks.Build())
            .Concat(AcceptanceChecks.Build()).Concat(MeasurementChecks.Build())
            .Concat(FormalPathChecks.Build()).Concat(DeclarationExtensionChecks.Build())
            .Concat(DeclarationClosure.Build()).ToList();
        if (args.Contains("--snapshot-only", StringComparer.Ordinal)) checks = SnapshotChecks.Build().ToList();
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
        Console.WriteLine($"net-the-spire checks (cross-end wire + snapshot mapping): {passed} passed / {failed} failed / {executed} executed.");
        Console.WriteLine($"TOTAL: {executed} executed, {passed} passed, {failed} failed.");        return passed > 0 && failed == 0 ? 0 : 1;
    }
}
