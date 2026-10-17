using BaseSpire.Slice;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// ENV-02：`Plating`（StoneArmor 施加的能力）的**两个触发点**。
///
/// 原版依据（我们源里的声明 `ironclad-powers.spire`）：
/// <code>
/// on = [trigger(event=afterOwnerTurnStart, effect=decrementPower(PlatingPower)),
///       trigger(event=afterOwnerTurnEnd,   effect=gainBlock(1, perPowerStack=PlatingPower))]
/// </code>
/// 卡侧 `StoneArmor`：1 费能力牌、自目标、施加 Plating 4（升级 6）。
///
/// 判别性设计：**打出卡**再走两个回合，用敌人第二次攻击的掉血量判定——若回合末没给格挡，
/// 掉血会是满额；若回合开始没递减，格挡会是 4 而不是 3。两半各自都能在数字上区分开。
/// </summary>
internal static class SliceFrontendPlatingChecks
{
    private const string PlatingId = "base-spire:plating-power";
    private const string StoneArmorId = "base-spire:stone-armor";

    public static void Run()
    {
        Check.Run("ENV-02 Plating：回合开始减 1 层、回合末按层数给格挡（打出 StoneArmor，带对照）[真实]", () =>
        {
            var control = DamageTaken(withStoneArmor: false);
            var plated = DamageTaken(withStoneArmor: true);
            // 基准是**实测**而不是假设：邪教徒的 Ritual 每回合加力量，第二次攻击的数值不等于卡面基础值。
            // 因此判定用**差值**：格挡恰好吸收"回合末的层数"。
            Check.True(control > 0, $"对照：不打出 StoneArmor 时挨满额伤害（实测 {control}）");
            Check.Equal(control - 3, plated,
                $"打出 StoneArmor（4 层，开局回合开始先减到 3）⇒ 回合末给 3 格挡 ⇒ 伤害 {control} → {plated}；"
                + "若递减没实现会少吸收 1 点（4 格挡），若给格挡没实现则与对照相同");
        });

        Check.Run("ENV-02 Plating：回合开始确实递减（4 → 3），且层数不跨回合自行清零 [真实]", () =>
        {
            var session = FormalSliceFixture.NewFormal();
            var player = FormalSliceFixture.Player(session);
            FormalSliceFixture.PlayAt(session, FormalSliceFixture.MoveToHand(session, StoneArmorId), player);
            Check.Equal(4L, FormalSliceFixture.PowerStacks(session, player, PlatingId),
                "打出 StoneArmor 后 Plating 4 层");

            FormalSliceFixture.EndTurn(session);
            Check.Equal(3L, FormalSliceFixture.PowerStacks(session, player, PlatingId),
                "下一个回合开始减 1 层（4 → 3）");

            FormalSliceFixture.EndTurn(session);
            Check.Equal(2L, FormalSliceFixture.PowerStacks(session, player, PlatingId),
                "再一个回合开始再减 1 层（3 → 2）：不是一次性效果，也不是直接清零");
        });
    }

    /// <summary>打出/不打出 StoneArmor，走两个回合，返回玩家损失的生命。</summary>
    private static long DamageTaken(bool withStoneArmor)
    {
        var session = FormalSliceFixture.NewFormal();
        var player = FormalSliceFixture.Player(session);
        if (withStoneArmor)
        {
            FormalSliceFixture.PlayAt(session, FormalSliceFixture.MoveToHand(session, StoneArmorId), player);
        }

        var before = FormalSliceFixture.Number(session, player, FormalSliceFixture.PlayerTypeId, "Hp");
        // 第一次敌方回合是念咒（无伤害），第二次才是攻击（9 点）。
        FormalSliceFixture.EndTurn(session);
        FormalSliceFixture.EndTurn(session);
        var after = FormalSliceFixture.Number(session, player, FormalSliceFixture.PlayerTypeId, "Hp");
        return before - after;
    }
}
