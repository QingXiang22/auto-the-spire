using BaseSpire.Slice;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// ENV-02：`CrueltyPower` 对**易伤倍率**的抬高。
///
/// 原版语义（`sts2/src/Core/Models/Powers/CrueltyPower.cs` 的 `ModifyVulnerableMultiplier`）：
/// 这个钩子由 `VulnerablePower` 在**出手方**身上调用，受击者不是出手方自己时返回
/// `amount + Amount / 100m`——即**攻击者**每层把易伤倍率提高 1 个百分点。
/// 我们源里声明为 `vulnerableMultiplierPercentPerStack = 1`。
///
/// 判别性设计：四个受控场景只改"谁有 Cruelty / 有没有易伤"，伤害必须不同——
/// 只验证"有 Cruelty 伤害更高"会把"倍率挂在谁身上""没有易伤时会不会凭空生效"一起漏掉。
/// </summary>
internal static class SliceFrontendCrueltyChecks
{
    private const string CrueltyId = "base-spire:cruelty-power";
    private const string VulnerableId = "base-spire:vulnerable";
    private const string StrikeId = "base-spire:strike-ironclad";
    private const long CrueltyStacks = 20;

    public static void Run()
    {
        Check.Run("ENV-02 CrueltyPower：**攻击者**每层把易伤倍率抬 1 个百分点（四组对照）[真实]", () =>
        {
            // 对照组：只有易伤 ⇒ 6 × 1.5 = 9。
            var vulnerableOnly = StrikeDamage(crueltyOnAttacker: 0, crueltyOnVictim: 0, vulnerable: true);
            Check.Equal(9L, vulnerableOnly, $"只有易伤时 6 × 1.5 = 9（实测 {vulnerableOnly}）");

            // 没有易伤时 Cruelty 不该凭空生效（倍率只在易伤阶段被读）。
            var crueltyWithoutVulnerable = StrikeDamage(CrueltyStacks, 0, vulnerable: false);
            Check.Equal(6L, crueltyWithoutVulnerable,
                $"没有易伤时 Cruelty 不生效：伤害就是基础 6（实测 {crueltyWithoutVulnerable}）");

            // 攻击者 20 层 ⇒ 倍率 1.5 + 0.20 = 1.70 ⇒ 6 × 1.70 = 10.2 → 截断一次 = 10。
            var crueltyOnAttacker = StrikeDamage(CrueltyStacks, 0, vulnerable: true);
            Check.Equal(10L, crueltyOnAttacker,
                $"攻击者 20 层 ⇒ 6 × 1.70 = 10.2 → 10（实测 {crueltyOnAttacker}）；对照组是 9");

            // 把 Cruelty 挂在**受击者**身上不该抬高倍率（钩子在出手方）。
            var crueltyOnVictim = StrikeDamage(0, CrueltyStacks, vulnerable: true);
            Check.Equal(vulnerableOnly, crueltyOnVictim,
                $"Cruelty 挂在受击者身上不生效（{crueltyOnVictim} == 对照组 {vulnerableOnly}）");
        });
    }

    /// <summary>打一张 Strike，返回敌人损失的生命（受控：手牌只留这一张，其余牌区不动）。</summary>
    private static long StrikeDamage(long crueltyOnAttacker, long crueltyOnVictim, bool vulnerable)
    {
        var session = FormalSliceFixture.NewFormal();
        var player = FormalSliceFixture.Player(session);
        var enemy = FormalSliceFixture.Cultist(session);

        if (crueltyOnAttacker > 0)
        {
            FormalSliceFixture.GivePower(session, player, CrueltyId, crueltyOnAttacker);
        }

        if (crueltyOnVictim > 0)
        {
            FormalSliceFixture.GivePower(session, enemy, CrueltyId, crueltyOnVictim);
        }

        if (vulnerable)
        {
            FormalSliceFixture.GivePower(session, enemy, VulnerableId, 2);
        }

        var before = FormalSliceFixture.Number(session, enemy, FormalSliceFixture.MonsterTypeId, "Hp");
        FormalSliceFixture.PlayAt(session, FormalSliceFixture.MoveToHand(session, StrikeId), enemy);
        var after = FormalSliceFixture.Number(session, enemy, FormalSliceFixture.MonsterTypeId, "Hp");
        return before - after;
    }
}
