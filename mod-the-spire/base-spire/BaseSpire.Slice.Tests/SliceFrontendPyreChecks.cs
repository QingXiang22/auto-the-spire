using BaseSpire.Slice;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// ENV-02：`PyrePower` 的**最大能量加成**（我们源里声明为 `maxEnergyPerStack = 1`）。
///
/// 原版语义（`sts2/src/Core/Models/Powers/PyrePower.cs`）：`ModifyMaxEnergy(player, amount)` 在
/// 玩家是拥有者时返回 `amount + Amount`——即**每层 +1 最大能量**（`StackType = Counter`，不随时间递减）。
/// 切片的等价落点是**回合开始的能量重置**：`Energy = MaxEnergy + Σ(层数 × maxEnergyPerStack)`。
///
/// 判别性设计：带对照——没有该能力时回合开始仍是基础能量；有 2 层时多 2 点；
/// 再过一个回合仍是 2 层（证明它不是一次性效果）。
/// </summary>
internal static class SliceFrontendPyreChecks
{
    private const string PyrePowerId = "base-spire:pyre-power";

    public static void Run()
    {
        Check.Run("ENV-02 PyrePower：每层 +1 最大能量，在回合开始的能量重置处生效并跨回合保留 [真实]", () =>
        {
            var plain = EnergyAtTurnStart(withPyre: 0);
            Check.Equal(SliceManifest.EnergyPerTurn, (int)plain, $"不带能力时回合开始能量 = 基础值（{plain}）");

            var boosted = EnergyAtTurnStart(withPyre: 2);
            Check.Equal(SliceManifest.EnergyPerTurn + 2, (int)boosted,
                $"带 2 层时回合开始能量 = 基础 + 2（{SliceManifest.EnergyPerTurn} → {boosted}）");

            var later = EnergyAtTurnStart(withPyre: 2, extraTurns: 1);
            Check.Equal(boosted, (int)later, $"再过一回合仍是 2 层（{boosted} → {later}）：不是一次性效果");
        });
    }

    /// <summary>挂上指定层数的 PyrePower，结束 <paramref name="extraTurns"/> + 1 个回合，返回玩家当时的能量。</summary>
    private static long EnergyAtTurnStart(long withPyre, int extraTurns = 0)
    {
        var session = FormalSliceFixture.NewFormal();
        var player = FormalSliceFixture.Player(session);
        if (withPyre > 0)
        {
            FormalSliceFixture.GivePower(session, player, PyrePowerId, withPyre);
        }

        for (var turn = 0; turn <= extraTurns; turn++)
        {
            FormalSliceFixture.EndTurn(session);
        }

        return FormalSliceFixture.Number(session, player, FormalSliceFixture.PlayerTypeId, "Energy");
    }
}
