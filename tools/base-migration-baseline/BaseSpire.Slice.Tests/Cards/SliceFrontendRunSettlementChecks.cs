using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// ENV-03 前置：**战斗结束真的把结果写回整局**（`SettleBattle` 的三个写入）。
///
/// 过程定义与接线早批次就有（`SettleTail` 放在"敌人全灭 / 玩家阵亡"两个尾分支里），
/// 但此前只有"过程存在"的证据、没有**端到端行为证据**。本检查把它跑通：
/// 把两个敌人血量压到 1、把玩家血量压到 30（**判别性**：整局初始 Hp 是满血，若没写回就不会等于 30），
/// 用两张不同的攻击牌依次击杀 ⇒ 胜利尾触发结算 ⇒ 断言 `Run.Hp`、`Run.SettledRoom`（幂等键）与
/// `Player.BattleSettled` 三项都被写。
/// </summary>
internal static class SliceFrontendRunSettlementChecks
{
    private const string RunTypeId = "base-spire:Run";
    private const string RunStatesCollection = "base-spire:RunStates";
    private const string MonsterTypeId = "base-spire:Monster";
    private const string StrikeId = "base-spire:strike-ironclad";
    private const string PommelStrikeId = "base-spire:pommel-strike";

    public static void Run()
    {
        Check.Run("ENV-03 前置：敌人全灭触发结算，整局 Hp / 幂等键 / BattleSettled 都被写回 [真实]", () =>
        {
            var session = FormalSliceFixture.NewFormal();
            var player = FormalSliceFixture.Player(session);
            var run = session.State.CollectionMembers(
                FormalSliceFixture.Collection(session, RunStatesCollection)).Single();

            var runHpBefore = FormalSliceFixture.Number(session, run, RunTypeId, "Hp");
            var runSettledBefore = FormalSliceFixture.Number(session, run, RunTypeId, "SettledRoom");
            var runRoom = FormalSliceFixture.Number(session, run, RunTypeId, "CurrentRoom");
            Check.Equal(0L, runSettledBefore,
                $"对照：开局尚未结算（SettledRoom={runSettledBefore}）");
            Check.True(runHpBefore != 30L,
                $"对照：整局初始 Hp 不是 30（实际 {runHpBefore}）——否则本检查没有判别力");

            // 判别性构造：玩家血量压到 30；两个敌人各压到 1，便于用两张不同的攻击牌依次击杀。
            FormalSliceFixture.SetNumber(session, player, FormalSliceFixture.PlayerTypeId, "Hp", 30);
            var calcified = FormalSliceFixture.Cultist(session, "base-spire:calcified-cultist");
            var damp = FormalSliceFixture.Cultist(session, "base-spire:damp-cultist");
            FormalSliceFixture.SetNumber(session, calcified, MonsterTypeId, "Hp", 1);
            FormalSliceFixture.SetNumber(session, damp, MonsterTypeId, "Hp", 1);

            FormalSliceFixture.PlayAt(session, FormalSliceFixture.MoveToHand(session, StrikeId), calcified);
            FormalSliceFixture.PlayAt(session, FormalSliceFixture.MoveToHand(session, PommelStrikeId), damp);

            Check.Equal(0L, FormalSliceFixture.Number(session, player, FormalSliceFixture.PlayerTypeId, "Hp") == 0
                    ? 1L
                    : 0L,
                "玩家没有在胜利时被误判为阵亡");
            Check.Equal(30L, FormalSliceFixture.Number(session, run, RunTypeId, "Hp"),
                $"整局 Hp 被写回为玩家的 30（写回前是 {runHpBefore}）");
            Check.Equal(runRoom, FormalSliceFixture.Number(session, run, RunTypeId, "SettledRoom"),
                $"整局 SettledRoom 被钉在本场房间号 {runRoom}（幂等键：同房重复结算将成空操作）");
            Check.True(session.State.GetField(player,
                    session.RuleSet.TypeByQualifiedId(FormalSliceFixture.PlayerTypeId)
                        .FieldIndex("BattleSettled")).AsBool(),
                "玩家身上的 BattleSettled 被置为真（本场已结算的公开标记）");
        });
    }
}
