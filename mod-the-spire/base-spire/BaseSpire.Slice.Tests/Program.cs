using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// 规则检查入口。默认只跑检查；带 <c>--report &lt;目录&gt;</c> 时额外写出**机器可读的自报产物**，
/// 供 ENV-06 的结算工具读取（<c>behavior_verified</c> 列）。
///
/// 为什么自报而不是让工具解析测试源码：内容 id 清单是运行期的契约，解析源码会把注释、
/// 字符串常量、重构后的写法都当成事实；自报则顺带能做一致性检查（见
/// <see cref="SliceFormalSourceChecks.BehaviorReport"/>）。
/// </summary>
internal static class Program
{
    public static int Main(string[] args)
    {
        var suite = ReadOption(args, "--suite") ?? "all";
        if (suite is not ("all" or "runtime" or "source")
            || (args.Contains("--suite", StringComparer.Ordinal) && ReadOption(args, "--suite") is null))
        {
            Console.Error.WriteLine("--suite requires all, runtime, or source.");
            return 2;
        }
        if (suite == "source")
        {
            PotionFlowChecks.RunSourceChecks();
            SilentPoisonChecks.RunSourceChecks();
            Console.WriteLine($"All {Check.Passed} source checks passed (requires local sts2).");
            return 0;
        }
        if (args.Contains("--describe-build", StringComparer.Ordinal))
        {
            foreach (var scope in new[] { BaseSpire.SinglePlayer.BaseSpireScope.Dev, BaseSpire.SinglePlayer.BaseSpireScope.DevMap })
                Console.WriteLine(JsonSerializer.Serialize(new BaseSpire.SinglePlayer.BaseSpireRuntimeFactory(
                    BaseSpire.SinglePlayer.RunConfiguration.FrozenIronclad(0, scope)).Describe()));
            return 0;
        }
        if (args.Contains("--public-semantics-only", StringComparer.Ordinal))
        {
            PublicSemanticsChecks.Run();
            SliceMapRoomChecks.DefaultIdentity();
            Console.WriteLine($"All {Check.Passed} public semantics checks passed.");
            return 0;
        }
        if (args.Contains("--shrink-only", StringComparer.Ordinal))
        {
            SliceFrontendShrinkChecks.Run();
            Console.WriteLine($"All {Check.Passed} Shrink checks passed (finite/infinite source duration and damage).");
            return 0;
        }
        if (args.Contains("--run-lifecycle-only", StringComparer.Ordinal))
        {
            RunLifecycleChecks.Run();
            Console.WriteLine($"All {Check.Passed} Run lifecycle checks passed.");
            return 0;
        }
        if (args.Contains("--sown-only", StringComparer.Ordinal))
        {
            SownChecks.Run();
            Console.WriteLine($"All {Check.Passed} Sown/SapphireSeed checks passed (controlled visits and combats).");
            return 0;
        }
        if (args.Contains("--capacity-prerequisites-only", StringComparer.Ordinal))
        {
            CapacityPrerequisiteChecks.Run();
            Console.WriteLine($"All {Check.Passed} controlled capacity prerequisite checks passed.");
            return 0;
        }
        if (args.Contains("--potion-reuse-only", StringComparer.Ordinal))
        {
            PotionReuseChecks.Run();
            Console.WriteLine($"All {Check.Passed} potion reuse checks passed (controlled formal content; includes known-gap witnesses).");
            return 0;
        }
        if (args.Contains("--osty-only", StringComparer.Ordinal))
        {
            CombatOstyChecks.Run();
            Console.WriteLine($"All {Check.Passed} Osty checks passed (controlled foundation witnesses).");
            return 0;
        }
        if (args.Contains("--poison-only", StringComparer.Ordinal))
        {
            CombatPoisonChecks.Run();
            SilentPoisonChecks.Run();
            Console.WriteLine($"All {Check.Passed} Poison checks passed (controlled source contract witnesses).");
            return 0;
        }
        if (args.Contains("--doom-only", StringComparer.Ordinal))
        {
            CombatDoomChecks.Run();
            Console.WriteLine($"All {Check.Passed} Doom checks passed (controlled source contract witnesses).");
            return 0;
        }
        if (args.Contains("--orbs-only", StringComparer.Ordinal))
        {
            CombatOrbChecks.Run();
            Console.WriteLine($"All {Check.Passed} orb checks passed (controlled source contract witnesses).");
            return 0;
        }
        if (args.Contains("--stars-only", StringComparer.Ordinal))
        {
            CombatStarsChecks.Run();
            Console.WriteLine($"All {Check.Passed} stars checks passed (controlled source contract witnesses).");
            return 0;
        }
        if (args.Contains("--forge-only", StringComparer.Ordinal))
        {
            CombatForgeChecks.Run();
            Console.WriteLine($"All {Check.Passed} forge checks passed (controlled source contract witnesses).");
            return 0;
        }
        if (args.Contains("--potion-flow-only", StringComparer.Ordinal))
        {
            PotionFlowChecks.Run(includeLegacy: true, includeSource: suite == "all");
            Console.WriteLine($"All {Check.Passed} potion flow checks passed.");
            return 0;
        }
        if (args.Contains("--next-turn-draw-only", StringComparer.Ordinal))
        {
            NextTurnDrawChecks.Run();
            Console.WriteLine($"All {Check.Passed} next-turn draw checks passed.");
            return 0;
        }
        if (args.Contains("--reshuffle-only", StringComparer.Ordinal))
        {
            // ENV-T06-source-stable-shuffle-20261012：源 `StableShuffle` 预排序的判别面。
            SourceStableShuffleChecks.Run();
            Console.WriteLine($"All {Check.Passed} source stable-shuffle checks passed (controlled reshuffle; source-sort vectors).");
            return 0;
        }
        if (args.Contains("--next-turn-energy-only", StringComparer.Ordinal))
        {
            NextTurnEnergyChecks.Run();
            Console.WriteLine($"All {Check.Passed} next-turn energy checks passed.");
            return 0;
        }
        if (args.Contains("--card-selection-only", StringComparer.Ordinal))
        {
            CardSelectionChecks.Run();
            Console.WriteLine($"All {Check.Passed} card selection checks passed.");
            return 0;
        }
        if (args.Contains("--run-domain-only", StringComparer.Ordinal))
        {
            RunDomainChecks.Run();
            Console.WriteLine($"All {Check.Passed} run-domain checks passed (controlled contract witnesses).");
            return 0;
        }
        if (args.Contains("--x-card-family-only", StringComparer.Ordinal))
        {
            XCardFamilyChecks.Run(args.Contains("--benefits-only", StringComparer.Ordinal));
            Console.WriteLine($"All {Check.Passed} X-cost card family checks passed (controlled formal source).");
            return 0;
        }
        if (args.Contains("--unblocked-card-coverage", StringComparer.Ordinal))
        {
            UnblockedCardFamiliesChecks.PrintCoverage();
            return 0;
        }
        if (args.Contains("--unblocked-card-families-only", StringComparer.Ordinal))
        {
            UnblockedCardFamiliesChecks.Run(ReadOption(args, "--card"));
            Console.WriteLine($"All {Check.Passed} unblocked card benefit and timing checks passed (controlled formal source).");
            return 0;
        }
        if (args.Contains("--remaining-card-families-only", StringComparer.Ordinal))
        {
            RemainingCardFamiliesChecks.Run(ReadOption(args, "--card"));
            Console.WriteLine($"All {Check.Passed} remaining card family checks passed (controlled formal source; no independent reference comparison).");
            return 0;
        }
        if (args.Contains("--treasure-room-only", StringComparer.Ordinal))
        {
            TreasureRoomChecks.Run();
            Console.WriteLine($"All {Check.Passed} treasure room checks passed (controlled boundary checks).");
            return 0;
        }
        if (args.Contains("--rest-site-only", StringComparer.Ordinal))
        {
            SliceMapRoomChecks.RestSiteBoundary();
            Console.WriteLine($"All {Check.Passed} controlled rest site checks passed (not a normal run).");
            return 0;
        }
        if (args.Contains("--treasure-bag-only", StringComparer.Ordinal))
        {
            TreasureRelicBagChecks.Run();
            Console.WriteLine($"All {Check.Passed} treasure relic bag checks passed.");
            return 0;
        }
        if (args.Contains("--encounter-queue-only", StringComparer.Ordinal))
        {
            // ENV-T08：普通/精英**独立库存与独立消费者**的局部反馈面（不含地图/奖励动作）。
            SliceEncounterQueueChecks.Run();
            Console.WriteLine($"All {Check.Passed} encounter queue checks passed (normal/elite inventories separate).");
            return 0;
        }
        if (args.Contains("--byrdonis-only", StringComparer.Ordinal))
        {
            SliceByrdonisChecks.Run();
            Console.WriteLine($"All {Check.Passed} Byrdonis mechanism checks passed (controlled formal definitions; not a public Run).");
            return 0;
        }
        if (args.Contains("--elite-room-only", StringComparer.Ordinal))
        {
            // ENV-T08：精英房消费者（真实地图 + 独立精英队列）的局部反馈面。
            SliceMapRoomChecks.EliteRoomBoundary();
            Console.WriteLine($"All {Check.Passed} elite room consumer checks passed.");
            return 0;
        }
        if (args.Contains("--boss-reward-only", StringComparer.Ordinal))
        {
            SliceMapRoomChecks.BossRewardBoundary();
            Console.WriteLine($"All {Check.Passed} controlled Boss reward boundary checks passed (not a normal run).");
            return 0;
        }
        if (args.Contains("--shop-flow-only", StringComparer.Ordinal))
        {
            // ENV-T09.2：正式商店（地图 Shop 分支 → 库存 → 购买/删牌 → 离店）的局部反馈面。
            ShopFlowChecks.Run();
            Console.WriteLine($"All {Check.Passed} shop flow checks passed (formal dev-map entry).");
            return 0;
        }
        if (args.Contains("--unknown-room-only", StringComparer.Ordinal))
        {
            // ENV-T09-unknown-room-modules-20261012：真实问号房（赔率/黑名单/实际房型）+ 事件访问生命周期。
            UnknownRoomChecks.Run();
            Console.WriteLine($"All {Check.Passed} unknown-room and event checks passed (formal dev-map entry; seed ENV-T03-SEED-L golden).");
            return 0;
        }
        if (args.Contains("--event-domain-only", StringComparer.Ordinal))
        {
            EventDomainChecks.Run();
            Console.WriteLine($"All {Check.Passed} event-domain checks passed (controlled contract witnesses).");
            return 0;
        }
        if (args.Contains("--author-contracts-only", StringComparer.Ordinal))
        {
            AuthorContractChecks.Run();
            Console.WriteLine($"All {Check.Passed} author contract checks passed.");
            return 0;
        }
        if (args.Contains("--orchestration-only", StringComparer.Ordinal))
        {
            OrchestrationChecks.Run();
            SliceFrontendIroncladCompletionChecks.Run();
            Console.WriteLine($"All {Check.Passed} orchestration and card completion checks passed (local group; excludes identity/map gates).");
            return 0;
        }
        if (args.Contains("--card-completion-only", StringComparer.Ordinal))
        {
            // Local feedback for the in-flight card batch; default identity/map gates remain in RunAll.
            SliceFrontendIroncladCompletionChecks.Run();
            Console.WriteLine("All card completion checks passed (local group; excludes identity/map gates).");
            return 0;
        }
        if (args.Contains("--effects-only", StringComparer.Ordinal))
        {
            EffectProgramChecks.Run();
            // ENV-T05：克隆判别与效果程序同属"不受地图/身份门槛阻挡"的局部反馈面。
            CloneCardChecks.Run();
            Console.WriteLine("All effect program checks passed.");
            return 0;
        }
        var reportDirectory = ReadOption(args, "--report");
        PotionFlowChecks.Run(includeSource: suite == "all");
        PotionReuseChecks.Run();
        CapacityPrerequisiteChecks.Run();
        RunLifecycleChecks.Run();
        CombatStarsChecks.Run();
        CombatForgeChecks.Run();
        CombatOrbChecks.Run();
        CombatDoomChecks.Run();
        CombatPoisonChecks.Run();
        SilentPoisonChecks.Run();
        CombatOstyChecks.Run();
        NextTurnEnergyChecks.Run();
        NextTurnDrawChecks.Run();
        CardSelectionChecks.Run();
        var includeMap = !args.Contains("--combat-only", StringComparer.Ordinal);
        RemainingCardFamiliesChecks.Run();
        UnblockedCardFamiliesChecks.Run();
        XCardFamilyChecks.Run(benefitsOnly: true);
        XCardFamilyChecks.Run();
        ShopFlowChecks.Run();
        UnknownRoomChecks.Run();
        EventDomainChecks.Run();
        SownChecks.Run();
        RunDomainChecks.Run();
        var passed = SliceChecks.RunAll(includeMap);
        Console.WriteLine(includeMap ? $"All {passed} slice checks passed (suite={suite})."
            : $"All {passed} slice checks excluding map checks passed (--combat-only).");

        if (reportDirectory is null)
        {
            return 0;
        }

        var (ids, notLowered) = SliceFormalSourceChecks.BehaviorReport();
        var report = new JsonObject
        {
            ["report_version"] = "env-06-behavior-report@1",
            ["project"] = "BaseSpire.Slice.Tests",
            ["checks_passed"] = passed,
            ["suite"] = suite,
            ["source_checks_executed"] = suite == "all",
            ["behavior_verified_ids"] = new JsonArray(
                ids.Order(StringComparer.Ordinal).Select(id => (JsonNode)JsonValue.Create(id)).ToArray()),
            ["behavior_verified_count"] = ids.Count,
            ["not_lowered"] = new JsonArray(
                notLowered.Select(id => (JsonNode)JsonValue.Create(id)).ToArray()),
            ["scope"] = "只覆盖**受控切片**里已迁移并使用正式源的内容；不代表完整原版内容的覆盖。",
            ["command"] = "dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests -- --report "
                + "<目录>",
        };

        Directory.CreateDirectory(reportDirectory);
        var path = Path.Combine(reportDirectory, "slice-behavior-report.json");
        File.WriteAllText(path,
            report.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
        Console.WriteLine($"behavior report written: {path}（{ids.Count} 条行为已验证内容）");
        return 0;
    }

    private static string? ReadOption(string[] args, string name)
    {
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (args[index] == name)
            {
                return args[index + 1];
            }
        }

        return null;
    }
}
