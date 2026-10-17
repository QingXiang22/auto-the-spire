using BaseSpire.Slice;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// ENV-02：`Mangle` 的两个机制一起验——① **尾部条件段** + `targetAlive`（打完这一下目标还活着才施加能力）；
/// ② **临时力量（负）**：`ManglePower` 持有期间给拥有者 −1/层力量，拥有者所在侧回合末移除并回补。
///
/// 原版依据：`ManglePower : TemporaryStrengthPower`（`IsPositive = false` ⇒ Sign = −1），
/// `TemporaryStrengthPower.AfterSideTurnEnd` 在**拥有者所在侧**回合末移除并施加 `−Sign × Amount`。
/// 卡侧 `Mangle`：3 费攻击 15 伤害，随后 `if { targetAlive } then { applyPower(ManglePower, 10) }`。
///
/// 判别性设计：三组受控场景分别打中"条件为真 / 条件为假 / 回补发生"。
/// </summary>
internal static class SliceFrontendManglePowerChecks
{
    private const string MangleId = "base-spire:mangle";
    private const string ManglePowerId = "base-spire:mangle-power";
    private const string StrengthId = "base-spire:strength";
    private const string MonsterTypeId = "base-spire:Monster";

    public static void Run()
    {
        Check.Run("ENV-02 Mangle：先造成 15 伤害，目标存活时才施加 ManglePower（压低血量即不施加）[真实]", () =>
        {
            var survivor = FormalSliceFixture.NewFormal();
            var survivorEnemy = FormalSliceFixture.Cultist(survivor);
            var survivorHpBefore = FormalSliceFixture.Number(survivor, survivorEnemy, MonsterTypeId, "Hp");
            var decisions = FormalSliceFixture.PlayAtCountingDecisions(survivor,
                FormalSliceFixture.MoveToHand(survivor, MangleId), survivorEnemy);
            Check.Equal(1, decisions,
                $"打这张牌只问**一次**目标：目标已在固定步骤里绑定，尾部条件段不得再问（实测 {decisions} 次）");
            var survivorHp = FormalSliceFixture.Number(survivor, survivorEnemy, MonsterTypeId, "Hp");
            Check.Equal(15L, survivorHpBefore - survivorHp,
                $"Mangle 打出后先造成 15 伤害（{survivorHpBefore} → {survivorHp}）：固定步骤与尾部条件段必须都在");
            Check.True(survivorHp > 0, $"目标应当还活着才能验证条件为真这一支（实测剩余 {survivorHp}）");
            Check.Equal(10L, FormalSliceFixture.PowerStacks(survivor, survivorEnemy, ManglePowerId),
                "目标存活 ⇒ 施加 ManglePower 10");

            var victim = FormalSliceFixture.NewFormal();
            var victimEnemy = FormalSliceFixture.Cultist(victim);
            FormalSliceFixture.SetNumber(victim, victimEnemy, MonsterTypeId, "Hp", 10);
            FormalSliceFixture.PlayAt(victim, FormalSliceFixture.MoveToHand(victim, MangleId), victimEnemy);
            Check.Equal(0L, FormalSliceFixture.PowerStacks(victim, victimEnemy, ManglePowerId),
                "目标被打死 ⇒ targetAlive 为假 ⇒ 不施加 ManglePower（对照场景 A 的 10）");
        });

        Check.Run("ENV-02 Mangle：ManglePower 给拥有者 −10 力量，并在**敌方**回合末移除且回补 [真实]", () =>
        {
            var session = FormalSliceFixture.NewFormal();
            var enemy = FormalSliceFixture.Cultist(session);
            Check.Equal(0L, FormalSliceFixture.PowerStacks(session, enemy, StrengthId), "对照：敌人开局力量为 0");

            FormalSliceFixture.PlayAt(session, FormalSliceFixture.MoveToHand(session, MangleId), enemy);
            Check.Equal(-10L, FormalSliceFixture.PowerStacks(session, enemy, StrengthId),
                "施加 ManglePower 10 ⇒ 敌人获得 −10 力量（Sign = −1，来源 BeforeApplied）");

            FormalSliceFixture.EndTurn(session);
            Check.Equal(0L, FormalSliceFixture.PowerStacks(session, enemy, ManglePowerId),
                "敌人回合末移除 ManglePower（`removeAfterOwnerTurnEnd` + ResolveOwnerTurnEnd 对敌人按侧调用）");
            Check.Equal(0L, FormalSliceFixture.PowerStacks(session, enemy, StrengthId),
                "移除的同一刻回补 +10 力量（AfterSideTurnEnd 的 −Sign × Amount）");
        });
    }
}
