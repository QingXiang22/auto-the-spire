using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// ENV-02：`UnmovablePower` 的 `blockGainModifiers`——"本回合**由牌获得格挡**的前 N 次翻倍"。
///
/// 原版依据（`sts2/src/Core/Models/Powers/UnmovablePower.cs` 的 `ModifyBlockMultiplicative`）：
/// 只对非怪物目标、`IsCardOrMonsterMove()`、且卡牌属于拥有者时生效；统计本回合同目标的
/// `BlockGainedEntry`（**不含本次出牌**）数量，`num &gt;= Amount` 时倍率 1，否则倍率 2。
/// 我们源里声明为 `blockGainModifiers = [modifier(multiplier=2, require=[move],
/// priorEntriesPerStack=1, playerOnly=true)]`。
///
/// 判别性设计：三组受控场景分别打中"第一次翻倍 / 第二次不翻倍 / 下一回合重新翻倍"。
/// 用**两张不同的牌**各给 5 点格挡（`Defend` + `IronWave`）：正式源卡组每张只有 1 份，
/// 同一回合重复使用同一实例会撞上"已在本回合打出"的成员冲突（夹具构造问题，不是规则问题）。
/// </summary>
internal static class SliceFrontendUnmovableChecks
{
    private const string UnmovablePowerId = "base-spire:unmovable-power";
    private const string DefendId = "base-spire:defend-ironclad";
    private const string IronWaveId = "base-spire:iron-wave";

    public static void Run()
    {
        Check.Run("ENV-02 Unmovable：同回合第一次获得格挡翻倍、第二次不翻倍（带对照）[真实]", () =>
        {
            var plain = FormalSliceFixture.NewFormal();
            var plainPlayer = FormalSliceFixture.Player(plain);
            FormalSliceFixture.PlayAt(plain, FormalSliceFixture.MoveToHand(plain, DefendId), plainPlayer);
            var plainFirst = Block(plain, plainPlayer);
            FormalSliceFixture.PlayAt(plain, FormalSliceFixture.MoveToHand(plain, IronWaveId),
                FormalSliceFixture.Cultist(plain));
            var plainSecond = Block(plain, plainPlayer);
            Check.Equal(5L, plainFirst, $"对照：没有 Unmovable 时防御给 5（实测 {plainFirst}）");
            Check.Equal(10L, plainSecond, $"对照：铁斩波再给 5（累计 {plainSecond}）");

            var session = FormalSliceFixture.NewFormal();
            var player = FormalSliceFixture.Player(session);
            FormalSliceFixture.GivePower(session, player, UnmovablePowerId, 1);
            FormalSliceFixture.PlayAt(session, FormalSliceFixture.MoveToHand(session, DefendId), player);
            var first = Block(session, player);
            FormalSliceFixture.PlayAt(session, FormalSliceFixture.MoveToHand(session, IronWaveId),
                FormalSliceFixture.Cultist(session));
            var second = Block(session, player);
            Check.Equal(10L, first, $"第一次获得格挡翻倍：5 → 10（实测 {first}）");
            Check.Equal(15L, second,
                $"第二次**不**翻倍：10 + 5 = 15（实测 {second}）；若豁免没实现会是 20");
        });

        Check.Run("ENV-02 Unmovable：计数按回合清零（下一回合第一次又翻倍）[真实]", () =>
        {
            var session = FormalSliceFixture.NewFormal();
            var player = FormalSliceFixture.Player(session);
            FormalSliceFixture.GivePower(session, player, UnmovablePowerId, 1);
            FormalSliceFixture.PlayAt(session, FormalSliceFixture.MoveToHand(session, DefendId), player);
            Check.Equal(10L, Block(session, player), "本回合第一次翻倍");

            // 回合开始会把格挡清零、也会把"本回合获得格挡次数"清零。
            FormalSliceFixture.EndTurn(session);
            Check.Equal(0L, Block(session, player), "新回合开始格挡清零");
            FormalSliceFixture.PlayAt(session, FormalSliceFixture.MoveToHand(session, DefendId), player);
            Check.Equal(10L, Block(session, player),
                "新回合的第一次又翻倍（10）——说明计数确实按回合清零，而不是一次性用完");
        });
    }

    private static long Block(Session session, InstanceId player) =>
        FormalSliceFixture.Number(session, player, FormalSliceFixture.PlayerTypeId, "Block");
}
