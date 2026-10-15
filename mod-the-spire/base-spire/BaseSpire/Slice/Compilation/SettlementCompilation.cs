using CoreTheSpire.Engine;
using BaseSpire.Slice.Effects;
using BaseSpire.Slice.Orchestration;
using static BaseSpire.Slice.Compilation.CompilationCallbacks;
using static BaseSpire.Slice.CombatSlicePackage;

namespace BaseSpire.Slice.Compilation;

internal static class SettlementCompilation
{
    internal static SettlementBindings Settlement(
        ContentBindings contentBindings,
        BattleBindings battleBindings,
        PlayerBindings playerBindings,
        RunStateBindings runStateBindings,
        RunCollectionsBindings runCollectionsBindings,
        RoomServicesBindings roomServicesBindings,
        RewardsBindings rewardsBindings)
    {

        // ENV-T02：**唯一**的"战斗 → 整局"永久状态写回点。
        //
        // 写回矩阵（权威方向是"整局 → 战斗副本 → 整局"，只有下面这两个字段会被写回）：
        //   * `Run.Hp` / `Run.MaxHp` ← 玩家战斗副本的同名字段。战斗里对这两个字段的**任何**改动
        //     （`DamagePlayer`、`LosePlayerHp`、`heal`、遗物 `AfterCombatVictory` 回血、
        //     父效果余效 `on_fatal_gain_max_hp`）都只改战斗副本，必须经由这里进整局。
        //   * `Run.Gold` 目前**没有**战斗内来源，因此不在写回集合里（将来加了来源必须回到这里补）。
        //   * 牌面/牌组**不**走这里：长期牌组只由奖励升级（`UpgradeLongTermCard`）写，
        //     战斗里永远是 `CopyCardFields` 造出的独立副本（临时牌只存在于战斗牌区）。
        //   * 换房时反向覆盖：`StartNextRoom` 把 `Run.Hp`/`Run.MaxHp` 灌回玩家战斗副本，
        //     所以漏写回会在下一间房被抹掉（ENV-T02 的复现点）。
        //
        // 幂等：写回本身无守卫，守卫在调用方——`SettleBattle` 的房间键（`SettledRoom` vs
        // `CurrentRoom`）与下面的 `BattleSettled`。
        ProcedureBuilder? commitPermanent = null;

        if (runStateBindings.HasRun)
        {
            commitPermanent = contentBindings.Package.Procedure("CommitPermanent");
            var commitRun = commitPermanent.First(commitPermanent.Collection(runCollectionsBindings.RunStates!));
            var commitPlayer = commitPermanent.First(commitPermanent.Type(playerBindings.Player, battleBindings.BattleScope));
            commitPermanent.Body(
                commitPermanent.SetField(commitRun, runStateBindings.RunHp!, commitPermanent.Field(commitPlayer, playerBindings.PlayerHp)),
                commitPermanent.SetField(commitRun, runStateBindings.RunMaxHp!, commitPermanent.Field(commitPlayer, playerBindings.PlayerMaxHp)));
        }

        // ENV-T02（复核修正）：这里曾经有一个 `CommitIfSettled`——"结算之后仍在跑的同一条效果的余效补写"。
        // 它随"效果链中途判胜利"一起被删掉：胜利判定挪到**动作体末尾**之后，`BattleEndTail` 是动作的最后
        // 一条指令，`VictoryTail → SettleTail → SettleBattle → CommitPermanent` 写的已经是跑完全部效果
        // （含 `after_card_played` 钩子与父效果余效）之后的完整战斗副本，不再存在"结算之后还有余效"的窗口。

        // B-07a / W-05：战斗 → 整局的**结算**。同一间房重复结算 = 空操作；进入下一间房后可以再结算一次。
        // 临时牌（战斗里生成的克隆实例）只存在于战斗牌区，因此不会进长期牌组——结构保证 + 检查断言。
        ProcedureBuilder? settleBattle = null;

        if (runStateBindings.HasRun)
        {
            settleBattle = contentBindings.Package.Procedure("SettleBattle");
            var settleRun = settleBattle.First(settleBattle.Collection(runCollectionsBindings.RunStates!));
            var settlePlayer = settleBattle.First(settleBattle.Type(playerBindings.Player, battleBindings.BattleScope));
            // 守卫�?*按房�?*的幂等键（`Run.SettledRoom` vs `Run.CurrentRoom`）：同一间房重复结算 �?空操作；
            // 进入下一间房（`CurrentRoom` 递增）后 �?可以再结算一次。调用点�?`SettleTail` 放在
            // 三个**战斗结束**分支里（敌人全灭 / 玩家阵亡），因此不会在战斗中途写回�?
            settleBattle.Body(
                settleBattle.If(
                    settleBattle.Not(settleBattle.Equal(settleBattle.Field(settleRun, runStateBindings.RunSettledRoom!),
                        settleBattle.Field(settleRun, runStateBindings.RunCurrentRoom!))),
                    settleBattle.Call(commitPermanent!.Symbol),
                    settleBattle.SetField(settleRun, runStateBindings.RunSettledRoom!,
                        settleBattle.Field(settleRun, runStateBindings.RunCurrentRoom!)),
                    // 冗余但真实的"本场已结�?标记（公开字段；幂等性由 Run 上的房间键保证）�?
                    settleBattle.SetField(settlePlayer, playerBindings.PlayerBattleSettled, settleBattle.Const(true))));

            // T05：结算边界（本场房战役已判定结束 + 长期写回之后）把 `MapPhase` 推到 2（`RoomSettled`）。
            // 重发一次 `Body` 而不是改上面那条参数列表：`actMap` 为 null 时既有 IR 逐条不变；
            // 地图 scope 下这一步是"下一次 `ChooseMapPoint` 的唯一合法窗口"，也是奖励子包的插入点。
            if (roomServicesBindings.ActMap is not null)
            {
                settleBattle.Body(
                [
                    settleBattle.If(
                        settleBattle.Not(settleBattle.Equal(settleBattle.Field(settleRun, runStateBindings.RunSettledRoom!),
                            settleBattle.Field(settleRun, runStateBindings.RunCurrentRoom!))),
                        settleBattle.Call(commitPermanent!.Symbol),
                        settleBattle.SetField(settleRun, runStateBindings.RunSettledRoom!,
                            settleBattle.Field(settleRun, runStateBindings.RunCurrentRoom!)),
                        settleBattle.SetField(settlePlayer, playerBindings.PlayerBattleSettled, settleBattle.Const(true)),
                        roomServicesBindings.ActMap.SettleStep(settleBattle, settleRun)),
                    // ENV-T05：结算的同一时刻**开奖**：抽掉落/金币/候选卡并把 `MapPhase` 推到 3
                    // （奖励开放）。奖励关闭（`RewardWiring.CloseRewards`）才写回 2，所以奖励关闭前
                    // `ChooseMapPoint` 的 {0, 2} 守卫不成立 ⇒ 不能旅行。
                    .. rewardsBindings.Rewards?.SettleSteps(settleBattle) ?? [],
                ]);
            }
        }

        return new SettlementBindings(commitPermanent, settleBattle);
    }

    internal static void SettlementBinding(
        BattleBindings battleBindings,
        PlayerBindings playerBindings,
        RunStateBindings runStateBindings,
        RunCollectionsBindings runCollectionsBindings,
        RoomServicesBindings roomServicesBindings,
        RewardsBindings rewardsBindings,
        SettlementBindings settlementBindings)
    {

        // -- damage -----------------------------------------------------------------------------------------
        // The recovered damage order (docs/review/tofix/r-damage-source-evidence-2026-10-01.md §3.1): the additive
        // stage and both multiplicative stages run in the engine's fixed-point domain, then a single lower bound of
        // zero, then exactly one decimal-to-int conversion, truncating. Authority: Hook.cs:2515-2555 for the stages,
        // Hook.cs:1565 for the lower bound and Creature.cs:449 (`(int)Math.Min(amount, 999999999m)`) for the single
        // truncation point.
        //
        // No stage rounds to an integer in between, which is what makes the planner's counterexample expressible at
        // all: base 5 with Weak x0.75 and Vulnerable x1.5 keeps its exact 5.625 and truncates to 5, while the legacy
        // backend's intermediate floors give 4.
        // B-05c：实例变量是**每张牌自己的字段**（同名变量在所有卡上共用一个字�?id，值随实例走）�?

        // Rewards are declared after card/power schemas; now bind settlement to the actual procedure.
        if (rewardsBindings.Rewards is not null)
        {
            var settledRun = settlementBindings.SettleBattle!.First(settlementBindings.SettleBattle.Collection(runCollectionsBindings.RunStates!));
            var settledPlayer = settlementBindings.SettleBattle.First(settlementBindings.SettleBattle.Type(playerBindings.Player, battleBindings.BattleScope));
            settlementBindings.SettleBattle.Body(settlementBindings.SettleBattle.If(settlementBindings.SettleBattle.NotEqual(settlementBindings.SettleBattle.Field(settledRun, runStateBindings.RunSettledRoom!), settlementBindings.SettleBattle.Field(settledRun, runStateBindings.RunCurrentRoom!)),
                [settlementBindings.SettleBattle.Call(settlementBindings.CommitPermanent!.Symbol),
                settlementBindings.SettleBattle.SetField(settledRun,runStateBindings.RunSettledRoom!,settlementBindings.SettleBattle.Field(settledRun,runStateBindings.RunCurrentRoom!)),
                settlementBindings.SettleBattle.SetField(settledPlayer,playerBindings.PlayerBattleSettled,settlementBindings.SettleBattle.Const(true)),
                roomServicesBindings.ActMap!.SettleStep(settlementBindings.SettleBattle,settledRun),
                settlementBindings.SettleBattle.If(settlementBindings.SettleBattle.Greater(settlementBindings.SettleBattle.Field(settledPlayer,playerBindings.PlayerHp),settlementBindings.SettleBattle.Const(0L)),[..rewardsBindings.Rewards.SettleSteps(settlementBindings.SettleBattle)])]));
        }

    }

    internal static Inst[] SettleTail(
        RunStateBindings runStateBindings,
        SettlementBindings settlementBindings,
        IrScope scope) => runStateBindings.HasRun ? [scope.Call(settlementBindings.SettleBattle!.Symbol)] : [];

    internal static Inst[] RelicVictorySteps(
        PackageBuildOptions options,
        BattleBindings battleBindings,
        PlayerBindings playerBindings,
        RelicsBindings relicsBindings,
        IrScope scope)
    {
        if (!relicsBindings.HasRelics || options.RelicLibrary is null)
        {
            return [];
        }

        var relicPlayer = scope.First(scope.Type(playerBindings.Player, battleBindings.BattleScope));
        var steps = new List<Inst>();
        foreach (var declared in options.RelicLibrary)
        {
            var reason = SpireRelics.UnsupportedReason(declared);
            if (reason.Length > 0)
            {
                throw Unsupported($"relic '{declared.ContentId}'", reason);
            }

            foreach (var effect in declared.Effects.Where(candidate =>
                         string.Equals(candidate.Trigger, RelicTriggers.AfterCombatVictory, StringComparison.Ordinal)))
            {
                // 拥有�?*没死**才回血（`BurningBlood.cs:18`），回血被最大生命截�?
                // （`CreatureCmd.Heal:698`）�?
                steps.Add(scope.If(
                    scope.And(
                        scope.Exists(scope.Where(scope.Collection(relicsBindings.Relics!),
                            entry => (Expr)scope.Equal(scope.Field(entry, relicsBindings.RelicDefinition!),
                                scope.Const(declared.ContentId)))),
                        scope.Greater(scope.Field(relicPlayer, playerBindings.PlayerHp), scope.Const(0L))),
                    scope.SetField(relicPlayer, playerBindings.PlayerHp,
                        scope.Minimum(scope.Field(relicPlayer, playerBindings.PlayerMaxHp),
                            scope.Add(scope.Field(relicPlayer, playerBindings.PlayerHp),
                                scope.Const((long)effect.Amount))))));
            }
        }

        return steps.ToArray();
    }

    internal static Inst[] VictoryTail(
        PackageBuildOptions options,
        EventsBindings eventsBindings,
        RunStateBindings runStateBindings,
        RunCollectionsBindings runCollectionsBindings,
        SettleTailCallback SettleTail,
        RelicVictoryStepsCallback RelicVictorySteps,
        IrScope scope,
        Expr turn)
    {
        if (!runStateBindings.HasRun)
        {
            return
            [
                .. SettleTail(scope),
                    scope.SetTerminal(eventsBindings.Victory, (eventsBindings.VictoryTurn, turn)),
                ];
        }

        var victoryRun = scope.First(scope.Collection(runCollectionsBindings.RunStates!));
        var victorySteps = new List<Inst>();
        victorySteps.AddRange(RelicVictorySteps(scope));
        victorySteps.AddRange(SettleTail(scope));
        if (options.TerminalOnConfiguredRoomCount)
        {
            victorySteps.Add(scope.If(scope.GreaterOrEqual(scope.Field(victoryRun, runStateBindings.RunCurrentRoom!),
                scope.Const((long)SliceManifest.RunRoomCount)),
                scope.SetTerminal(eventsBindings.Victory, (eventsBindings.VictoryTurn, turn))));
        }

        // 正式构建�?*不按房间计数�?Victory**。原版的胜利只来自最�?BOSS 之后的终局事件
        // （`RunManager.EnterNextAct` + `IsVictoryRoom`），房间数用尽只�?当前范围到此为止"�?
        // 会话保持 Running、`EnterNextRoom` 不再合法，由构建描述说明未覆盖的范围�?
        return victorySteps.ToArray();
    }

    internal static Inst BattleEndTail(
        BattleBindings battleBindings,
        PlayerBindings playerBindings,
        PilesBindings pilesBindings,
        VictoryTailCallback VictoryTail,
        IrScope scope)
    {
        var endPlayer = scope.First(scope.Type(playerBindings.Player, battleBindings.BattleScope));
        return scope.If(
            scope.And(
                scope.Not(scope.Field(endPlayer, playerBindings.PlayerBattleSettled)),
                scope.Equal(scope.Count(scope.Collection(pilesBindings.Monsters)), scope.Const(0L))),
            VictoryTail(scope, scope.Field(endPlayer, playerBindings.PlayerTurn)));
    }

}
