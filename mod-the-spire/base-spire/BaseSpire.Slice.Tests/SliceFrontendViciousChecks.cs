using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// ENV-02：`ViciousPower` 的 **`power` 事件**——"每当你施加易伤，抽 1 张牌（按 Vicious 层数）"。
///
/// 原版依据（我们源里的声明）：`events = [event(type=power, owner=source, power=Vulnerable,
/// ownerTurnOnly=false, effect=draw(scaled(0, 1, count=targetPowerAmount(ViciousPower, target=self))))]`；
/// 卡 `Vicious` 是 1 费能力牌、自目标、给自己 1 层。
///
/// 判别性设计：两组对照只改"玩家身上有没有 ViciousPower"——
/// 打出 `Bash`（施加易伤 2）后：有 Vicious ⇒ 手牌 **−1 + 2 = +1**；没有 ⇒ **−1**。
/// 只验证"抽了牌"会把"触发条件"漏掉。
///
/// 已知范围限制（未经行为测试的部分）：触发按"施加对象**不是玩家**"收窄（= 玩家施加给敌人），
/// 因为切片的施加点不传"谁施放的"。敌人的同类施加（`enemies*.spire` 里 6 处 `target=player`）
/// 因此在构造上不会触发；切片当前遭遇里没有会对玩家施加易伤的敌人，故这条界限**只有构造证据**。
/// </summary>
internal static class SliceFrontendViciousChecks
{
    private const string ViciousPowerId = "base-spire:vicious-power";
    private const string BashId = "base-spire:bash";

    public static void Run()
    {
        Check.Run("ENV-02 Vicious：施加易伤后按层数抽牌（带对照）[真实]", () =>
        {
            var plain = FormalSliceFixture.NewFormal();
            var plainHand = FormalSliceFixture.Collection(plain, FormalSliceFixture.HandPile);
            var plainBash = FormalSliceFixture.MoveToHand(plain, BashId);
            var plainBefore = plain.State.CollectionMembers(plainHand).Count;
            FormalSliceFixture.PlayAt(plain, plainBash, FormalSliceFixture.Cultist(plain));
            var plainAfter = plain.State.CollectionMembers(plainHand).Count;
            Check.Equal(plainBefore - 1, plainAfter,
                $"对照：没有 ViciousPower 时打出 Bash 只让手牌 −1（{plainBefore} → {plainAfter}）");

            var session = FormalSliceFixture.NewFormal();
            var player = FormalSliceFixture.Player(session);
            FormalSliceFixture.GivePower(session, player, ViciousPowerId, 2);
            var hand = FormalSliceFixture.Collection(session, FormalSliceFixture.HandPile);
            var bash = FormalSliceFixture.MoveToHand(session, BashId);
            var before = session.State.CollectionMembers(hand).Count;
            FormalSliceFixture.PlayAt(session, bash, FormalSliceFixture.Cultist(session));
            var after = session.State.CollectionMembers(hand).Count;
            Check.Equal(before + 1, after,
                $"2 层 Vicious：Bash 打出（−1）+ 施加易伤触发抽 2 张（+2）⇒ 手牌 {before} → {after}；"
                + "若事件没触发会是 −1，若按层数算错会是别值");
        });
    }
}
