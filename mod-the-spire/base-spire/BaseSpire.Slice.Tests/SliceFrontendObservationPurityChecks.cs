using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// ENV-01：**观察不消耗随机数**——读公开状态（合法动作、待决决策、字段与牌区快照）不得推进任何随机流。
///
/// 这条不变量是"AI 侧观察可重复"的前提：若"看一眼"就消费随机数，那么同一局面在不同观察次数下会分叉，
/// 对拍与复现都失去意义。判别性设计：**必须有反例**——先验证"做一件真的会消耗随机数的事"会让计数器前进，
/// 否则"0 == 0"这种平凡通过说明不了任何事。
/// </summary>
internal static class SliceFrontendObservationPurityChecks
{
    public static void Run()
    {
        Check.Run("ENV-01 观察不消耗随机数：读动作/决策/字段前后随机流计数不变（带消耗反例）[真实]", () =>
        {
            var session = FormalSliceFixture.NewFormal();
            var player = FormalSliceFixture.Player(session);
            var enemy = FormalSliceFixture.Cultist(session);

            var before = session.Random.TotalDraws;
            var beforeDrawStream = session.Random.DrawCount(SliceManifest.DrawStream);

            // 纯观察：反复读合法动作、待决决策、能力与牌区状态。
            for (var round = 0; round < 3; round++)
            {
                _ = session.LegalActions(SliceManifest.Player);
                _ = session.PendingDecision;
                _ = FormalSliceFixture.PowerIds(session, player);
                _ = FormalSliceFixture.PowerIds(session, enemy);
                _ = FormalSliceFixture.Number(session, player, FormalSliceFixture.PlayerTypeId, "Hp");
                _ = session.State.CollectionMembers(FormalSliceFixture.Collection(session, FormalSliceFixture.HandPile));
                _ = session.State.CollectionMembers(
                    FormalSliceFixture.Collection(session, "base-spire:DrawPile"));
            }

            Check.Equal(before, session.Random.TotalDraws,
                $"三次「只看不动」之后总抽样数不变（{before} → {session.Random.TotalDraws}）");
            Check.Equal(beforeDrawStream, session.Random.DrawCount(SliceManifest.DrawStream),
                "抽牌流计数也不变（观察不会触发重洗）");

            // 反例（对照）：做一件真的会消耗随机数的事——随机目标攻击。
            _ = enemy;
            FormalSliceFixture.PlayAt(session,
                FormalSliceFixture.MoveToHand(session, "base-spire:sword-boomerang"), enemy);
            Check.True(session.Random.TotalDraws > before,
                $"对照：随机目标攻击会消耗随机数（{before} → {session.Random.TotalDraws}）——"
                + "说明上面的「不变」确实是在测量，而不是计数器本来就不动");
        });
    }
}
