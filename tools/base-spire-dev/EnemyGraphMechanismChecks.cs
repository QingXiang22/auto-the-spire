using CoreTheSpire.Engine;

internal static partial class PackageChecks
{
    public static int RunEnemyGraphOnly(DslPackage package)
    {
        var count = 0;
        void Case(string name, Action body) { body(); count++; Console.WriteLine("ok   " + name); }
        Case("MOVE illegal graphs fail at assembly before random use", () =>
        {
            foreach (var (from, to) in new[] {
                ("initial = \"STICKY_SHOT_MOVE\"", "initial = \"MISSING\""),
                ("id = \"POKEY_POUNCE_MOVE\"", "id = \"STICKY_SHOT_MOVE\""),
                ("next = \"RAND\"", "next = \"MISSING\""),
                ("next = \"RAND\"", "next = \"\""),
                ("target = \"POKEY_POUNCE_MOVE\"", "target = \"MISSING\""),
                ("kind = \"random\"", "kind = \"unknown\""),
                ("maxRepeats = 2", "maxRepeats = 2 weight = -0.1") })
            {
                using var invalid = new Copy(package.Root);
                var path = invalid.Path("enemies/shared/twig-slime-m.spire"); File.WriteAllText(path, File.ReadAllText(path).Replace(from, to, StringComparison.Ordinal));
                var s = invalid.Load().CreateSession(1, ["player"]);
                Throws<EngineFaultException>(s.Start); Equal(SessionStatus.Faulted, s.Status, "invalid graph faults"); Equal(0L, s.Random.TotalDraws, "assembly never samples");
                True(s.Fault!.Message.Contains("Enemy move graph", StringComparison.Ordinal), "named graph failure");
            }
            using var noBranches = new Copy(package.Root);
            var noBranchesPath = noBranches.Path("enemies/shared/twig-slime-m.spire");
            File.WriteAllLines(noBranchesPath, File.ReadAllLines(noBranchesPath).Where(line => !line.Contains("template = EnemyMoveBranchDefinition", StringComparison.Ordinal)));
            var missing = noBranches.Load().CreateSession(1, ["player"]); Throws<EngineFaultException>(missing.Start);
            True(missing.Fault!.Message.Contains("no branches", StringComparison.Ordinal), "random nodes require registered successors");
            Equal(0L, missing.Random.TotalDraws, "missing branches fail at assembly");
        });
        Case("MOVE no legal branch faults without a compensating draw, and branch cycles respect execution budget", () =>
        {
            using var empty = new Copy(package.Root);
            var path = empty.Path("enemies/shared/twig-slime-m.spire"); File.WriteAllText(path, File.ReadAllText(path).Replace("maxRepeats = 2", "maxRepeats = 2 weight = 0.0", StringComparison.Ordinal));
            var s = Start(empty.Load()); Submit(s, "TwigSlimeM.Spawn"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Submit(s, "EndDevelopmentTurn");
            Throws<EngineFaultException>(() => Submit(s, "BeginDevelopmentTurn")); Equal(0L, s.Random.DrawCount(MonsterAi), "zero total never samples");
            True(s.Fault!.Message.Contains("no legal branch", StringComparison.Ordinal), "no silent fallback");
            using var loop = new Copy(package.Root);
            path = loop.Path("enemies/shared/twig-slime-m.spire"); File.WriteAllText(path, File.ReadAllText(path).Replace("target = \"POKEY_POUNCE_MOVE\" maxRepeats = 2", "target = \"RAND\" maxRepeats = 2", StringComparison.Ordinal));
            s = Start(loop.Load()); Submit(s, "TwigSlimeM.Spawn"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Submit(s, "EndDevelopmentTurn");
            Throws<EngineFaultException>(() => Submit(s, "BeginDevelopmentTurn")); Equal(SessionStatus.Faulted, s.Status, "transition loop stops at core budget");
            Equal(EngineFaultCode.BudgetExceeded, s.Fault!.Code, "transition recursion uses the core budget failure");
            True(s.Random.DrawCount(MonsterAi) > 0 && s.Random.DrawCount(MonsterAi) <= s.Budget.MaxCallDepth, "bounded transitions, no infinite loop");
        });
        Console.WriteLine($"All {count} enemy graph mechanism checks passed; production encounters are not executed here.");
        return 0;
    }
}
