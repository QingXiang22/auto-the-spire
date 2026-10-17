using BaseSpire.Slice;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// ENV-02：`MoltenFist` 的**嵌套条件段**——`if targetAlive then { if hasPower(Vulnerable) then
/// { applyScaledPower(Vulnerable, base=0, perCount=1, count=targetPowerAmount(Vulnerable)) } }`。
///
/// 原版依据（`sts2/src/Core/Models/Cards/MoltenFist.cs`）：先打 10 点（升级 14），
/// 然后 `num = Target.IsAlive ? Target.GetPowerAmount&lt;VulnerablePower&gt;() : 0`，
/// `if (num &gt; 0) Apply&lt;VulnerablePower&gt;(Target, num)` —— 也就是**把目标的易伤翻倍**。
///
/// 判别性设计：两组受控场景只改"目标有没有易伤"——
/// 只验证"打过之后易伤变了"会把"没有易伤时不该凭空加"漏掉；只验证外层 targetAlive 会把内层 hasPower 漏掉。
/// </summary>
internal static class SliceFrontendMoltenFistChecks
{
    private const string MoltenFistId = "base-spire:molten-fist";
    private const string VulnerableId = "base-spire:vulnerable";
    private const string MonsterTypeId = "base-spire:Monster";

    public static void Run()
    {
        Check.Run("ENV-02 MoltenFist：10 伤害；目标有易伤 ⇒ 易伤翻倍，没有易伤 ⇒ 不加 [真实]", () =>
        {
            // 场景 A：目标带 3 层易伤 ⇒ 打 10 点 + 施加 3 层（翻倍成 6）。
            var doubled = FormalSliceFixture.NewFormal();
            var doubledEnemy = FormalSliceFixture.Cultist(doubled);
            FormalSliceFixture.GivePower(doubled, doubledEnemy, VulnerableId, 3);
            var hpBefore = FormalSliceFixture.Number(doubled, doubledEnemy, MonsterTypeId, "Hp");
            FormalSliceFixture.PlayAt(doubled, FormalSliceFixture.MoveToHand(doubled, MoltenFistId), doubledEnemy);
            var hpAfter = FormalSliceFixture.Number(doubled, doubledEnemy, MonsterTypeId, "Hp");
            // 注意：目标带着 3 层易伤，因此这 10 点是**受易伤乘区影响**的——10 × 1.5 = 15。
            Check.Equal(15L, hpBefore - hpAfter,
                $"先造成 10 点基础伤害，经目标易伤 ×1.5 ⇒ 15（{hpBefore} → {hpAfter}）");
            Check.Equal(6L, FormalSliceFixture.PowerStacks(doubled, doubledEnemy, VulnerableId),
                "目标有 3 层易伤 ⇒ 内层 hasPower 成立 ⇒ 再施加 3 层（3 → 6，即翻倍）");

            // 场景 B（对照）：目标没有易伤 ⇒ 内层分支不成立，易伤仍是 0；伤害也不打折（10）。
            var plain = FormalSliceFixture.NewFormal();
            var plainEnemy = FormalSliceFixture.Cultist(plain);
            var plainHpBefore = FormalSliceFixture.Number(plain, plainEnemy, MonsterTypeId, "Hp");
            FormalSliceFixture.PlayAt(plain, FormalSliceFixture.MoveToHand(plain, MoltenFistId), plainEnemy);
            var plainHpAfter = FormalSliceFixture.Number(plain, plainEnemy, MonsterTypeId, "Hp");
            Check.Equal(10L, plainHpBefore - plainHpAfter,
                $"没有易伤时伤害是基础 10（{plainHpBefore} → {plainHpAfter}）：对照场景 A 的 15");
            Check.Equal(0L, FormalSliceFixture.PowerStacks(plain, plainEnemy, VulnerableId),
                "目标没有易伤 ⇒ 不施加（对照场景 A 的 6）：内层条件真的参与判定，而不是恒真");
        });
    }
}
