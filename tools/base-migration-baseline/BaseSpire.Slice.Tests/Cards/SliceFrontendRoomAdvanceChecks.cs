using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// ENV-03：**房间推进这半边**——胜利结算后可以执行 `EnterNextRoom`，房间号 +1、本场结算标记复位、
/// 幂等键仍停在**上一间房**（因此下一场还会写回整局）。
///
/// 这是整局循环的核心不变量：`Run.SettledRoom`（已结算的房间）与 `Run.CurrentRoom`（当前房间）之差
/// 就是"本场是否需要结算"的**幂等键**。只验证"赢了一场"会把房间推进整段漏掉；
/// 只验证"房间号 +1"会把幂等键复位漏掉。战斗侧的写回已在 `SliceFrontendRunSettlementChecks` 里验证。
/// </summary>
internal static class SliceFrontendRoomAdvanceChecks
{
    private const string RunTypeId = "base-spire:Run";
    private const string RunStatesCollection = "base-spire:RunStates";
    private const string MonsterTypeId = "base-spire:Monster";
    private const string MonstersCollection = "base-spire:Monsters";
    private const string EnterNextRoom = "base-spire:EnterNextRoom";
    private const string StrikeId = "base-spire:strike-ironclad";
    private const string PommelStrikeId = "base-spire:pommel-strike";

    /// <summary>当前存活敌人的定义 id 集合（有序），用于特征化"每间房用哪一套遭遇"。</summary>
    private static string[] Encounter(Session session) => session.State
        .CollectionMembers(FormalSliceFixture.Collection(session, MonstersCollection))
        .Select(monster => FormalSliceFixture.Definition(session, monster, MonsterTypeId))
        .Order(StringComparer.Ordinal)
        .ToArray();

    public static void Run()
    {
        Check.Run("ENV-03 房间推进：胜利结算后 EnterNextRoom 使房间号 +1、结算标记复位、幂等键留在上一间房 [真实]", () =>
        {
            var session = FormalSliceFixture.NewFormal();
            var player = FormalSliceFixture.Player(session);
            var run = session.State.CollectionMembers(
                FormalSliceFixture.Collection(session, RunStatesCollection)).Single();
            FormalSliceFixture.SetNumber(session, player, FormalSliceFixture.PlayerTypeId, "Hp", 30);

            // 清场：两个敌人各压到 1，用两张不同攻击牌依次击杀 ⇒ 胜利尾触发结算。
            var calcified = FormalSliceFixture.Cultist(session, "base-spire:calcified-cultist");
            var damp = FormalSliceFixture.Cultist(session, "base-spire:damp-cultist");
            FormalSliceFixture.SetNumber(session, calcified, MonsterTypeId, "Hp", 1);
            FormalSliceFixture.SetNumber(session, damp, MonsterTypeId, "Hp", 1);
            // 特征化：第一间房的**敌人集合**要在清场**之前**取（清场后它们已从存活集合里移除）。
            var firstEncounter = Encounter(session);
            FormalSliceFixture.PlayAt(session, FormalSliceFixture.MoveToHand(session, StrikeId), calcified);
            FormalSliceFixture.PlayAt(session, FormalSliceFixture.MoveToHand(session, PommelStrikeId), damp);

            var roomBefore = FormalSliceFixture.Number(session, run, RunTypeId, "CurrentRoom");
            var settledBefore = FormalSliceFixture.Number(session, run, RunTypeId, "SettledRoom");
            Check.Equal(roomBefore, settledBefore, "清场后：幂等键已钉在本场房间");

            // 房间推进必须由**公开动作**驱动（否则玩家/AI 无法离开这间房）。
            var enter = session.LegalActions(SliceManifest.Player)
                .FirstOrDefault(candidate => candidate.Definition.QualifiedId == EnterNextRoom);
            Check.True(enter is not null,
                $"清场后应当可以执行 {EnterNextRoom}（合法动作里有就说明这半边是通的）");

            session.Submit(enter!.Definition, SliceManifest.Player, []);
            while (session.Status == SessionStatus.AwaitingDecision)
            {
                var pending = session.PendingDecision!;
                session.Answer(new DecisionAnswer(pending.RequestId, [pending.Candidates[0]]));
            }

            Check.Equal(roomBefore + 1, FormalSliceFixture.Number(session, run, RunTypeId, "CurrentRoom"),
                $"房间号 +1（{roomBefore} → {FormalSliceFixture.Number(session, run, RunTypeId, "CurrentRoom")}）");
            Check.Equal(settledBefore, FormalSliceFixture.Number(session, run, RunTypeId, "SettledRoom"),
                $"幂等键仍停在上一间房（{settledBefore}）：下一场因此**还会**写回整局");
            Check.False(session.State.GetField(player,
                    session.RuleSet.TypeByQualifiedId(FormalSliceFixture.PlayerTypeId)
                        .FieldIndex("BattleSettled")).AsBool(),
                "本场已结算的标记被复位（新房间尚未结算）");

            // 特征化断言：把"新房间用哪一套敌人"钉住（当前实测：与第一间相同）。
            // 这条**不是**在断言原版语义，而是把现状写成可测事实：将来接入"地图/遭遇选择"时，
            // 这条会先失败，提醒改动者去更新它——而不是让"每间房都是同一遭遇"悄悄留在代码里。
            var secondEncounter = Encounter(session);
            Check.Equal(string.Join(",", firstEncounter), string.Join(",", secondEncounter),
                $"特征化：当前每间房使用**同一套**敌人（第一间 [{string.Join(",", firstEncounter)}] / "
                + $"第二间 [{string.Join(",", secondEncounter)}]）；接入地图与遭遇选择后此断言应被更新");

            // 第二间房：再清一次场 ⇒ 幂等键必须**重新武装**（SettledRoom 从 1 走到 2），
            // 且整局 Hp 被**再次**写回（这一次用 25，与第一间的 30 区分开）。
            var room2Calcified = FormalSliceFixture.Cultist(session, "base-spire:calcified-cultist");
            var room2Damp = FormalSliceFixture.Cultist(session, "base-spire:damp-cultist");
            FormalSliceFixture.SetNumber(session, room2Calcified, MonsterTypeId, "Hp", 1);
            FormalSliceFixture.SetNumber(session, room2Damp, MonsterTypeId, "Hp", 1);
            FormalSliceFixture.SetNumber(session, player, FormalSliceFixture.PlayerTypeId, "Hp", 25);
            FormalSliceFixture.PlayAt(session, FormalSliceFixture.MoveToHand(session, StrikeId), room2Calcified);
            FormalSliceFixture.PlayAt(session, FormalSliceFixture.MoveToHand(session, PommelStrikeId), room2Damp);

            Check.Equal(roomBefore + 1, FormalSliceFixture.Number(session, run, RunTypeId, "SettledRoom"),
                "第二间房清场后幂等键前进到 2 ⇒ 结算**再次**发生（只验证「能进下一间房」会把这条漏掉）");
            Check.Equal(25L, FormalSliceFixture.Number(session, run, RunTypeId, "Hp"),
                "整局 Hp 被第二次写回（30 → 25）：跨房间的写回链路是通的，不是只有第一间房能写");
        });
    }
}
