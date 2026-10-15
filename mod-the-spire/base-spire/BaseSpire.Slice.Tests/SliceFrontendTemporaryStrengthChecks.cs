using BaseSpire.Slice;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// ENV-02：**临时力量**能力（`TemporaryStrengthPower`）。
///
/// 原版语义（`sts2/src/Core/Models/Powers/TemporaryStrengthPower.cs`）：
/// `BeforeApplied` 时给拥有者 `Sign × amount` 力量；`AfterSideTurnEnd`（拥有者所在侧回合末）移除自身
/// 并回补 `−Sign × Amount`。`ManglePower` 的 `IsPositive = false` ⇒ Sign = −1；`SetupStrikePower` 为正。
///
/// 本检查走**正号**的端到端路径：打出 `SetupStrike`（1 费攻击，7 伤害 + 给自己 SetupStrikePower 2）。
/// **负号（Mangle）只做到结构上同一路径（同一个 `TemporaryStrengthPerStack` × (−1)），尚未有行为证据**：
/// 唯一施加它的卡 `Mangle` 还缺 `targetAlive` 谓词，因此这里不把它当成已验证。
/// </summary>
internal static class SliceFrontendTemporaryStrengthChecks
{
    private const string SetupStrikeId = "base-spire:setup-strike";
    private const string SetupStrikePowerId = "base-spire:setup-strike-power";
    private const string StrengthId = "base-spire:strength";

    public static void Run()
    {
        Check.Run("ENV-02 临时力量（正）：施加时 +2 力量，拥有者回合末移除并回补 0（带对照）[真实]", () =>
        {
            var control = FormalSliceFixture.NewFormal();
            var controlPlayer = FormalSliceFixture.Player(control);
            Check.Equal(0L, FormalSliceFixture.PowerStacks(control, controlPlayer, StrengthId),
                "对照：开局力量为 0");

            var session = FormalSliceFixture.NewFormal();
            var player = FormalSliceFixture.Player(session);
            var enemy = FormalSliceFixture.Cultist(session);
            FormalSliceFixture.PlayAt(session, FormalSliceFixture.MoveToHand(session, SetupStrikeId), enemy);

            Check.Equal(2L, FormalSliceFixture.PowerStacks(session, player, SetupStrikePowerId),
                "打出 SetupStrike 后自身有 2 层 SetupStrikePower");
            Check.Equal(2L, FormalSliceFixture.PowerStacks(session, player, StrengthId),
                "施加时同时获得 +2 力量（来源 BeforeApplied 的 Sign × amount）；玩家身上的能力："
                + string.Join(", ", FormalSliceFixture.PowerIds(session, player)));

            FormalSliceFixture.EndTurn(session);
            Check.Equal(0L, FormalSliceFixture.PowerStacks(session, player, SetupStrikePowerId),
                "拥有者回合末移除该能力（removeAfterOwnerTurnEnd）");
            Check.Equal(0L, FormalSliceFixture.PowerStacks(session, player, StrengthId),
                "移除的同一刻回补 −2 力量（来源 AfterSideTurnEnd 的 Remove + Apply(−Sign × Amount)）");
        });
    }
}
