using BaseSpire.Slice;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// ENV-02：`ColossusPower` 的**来源条件**（`incomingDamageModifiers` 的 `sourcePower=Vulnerable`）。
///
/// 原版语义（`sts2/src/Core/Models/Powers/ColossusPower.cs`）：`ModifyDamageMultiplicative` 只在
/// ①受击者是拥有者、②是 powered 攻击、③**攻击者身上有易伤** 三条同时成立时返回 0.5，否则返回 1。
/// 第三条是本轮新加的机制——现有 `incomingDamageModifiers` 只按受击者自身的层数判定。
///
/// 判别性设计：三组受控场景只改"易伤挂在谁身上"，伤害必须不同——
/// 只验证"有 Colossus 就减半"会漏掉条件本身。
/// </summary>
internal static class SliceFrontendColossusChecks
{
    private const string ColossusPowerId = "base-spire:colossus-power";
    private const string VulnerableId = "base-spire:vulnerable";

    public static void Run()
    {
        Check.Run("ENV-02 ColossusPower：只有**攻击者**身上有易伤时才减半（三组对照）[真实]", () =>
        {
            var plain = DamageTaken(vulnerableOnAttacker: 0, vulnerableOnVictim: 0, withColossus: false);
            var colossusOnly = DamageTaken(vulnerableOnAttacker: 0, vulnerableOnVictim: 0, withColossus: true);
            Check.Equal(plain, colossusOnly,
                $"没有易伤时 ColossusPower 不该生效（对照 {plain} → {colossusOnly}）");

            var attackerVulnerable = DamageTaken(vulnerableOnAttacker: 2, vulnerableOnVictim: 0, withColossus: true);
            Check.True(attackerVulnerable < plain,
                $"攻击者身上有易伤时减半：{plain} → {attackerVulnerable}（×0.5 后按既有规则截断一次）");

            var victimVulnerable = DamageTaken(vulnerableOnAttacker: 0, vulnerableOnVictim: 2, withColossus: true);
            Check.True(victimVulnerable > attackerVulnerable,
                $"易伤挂在受击者身上**不触发**来源条件（{victimVulnerable} > {attackerVulnerable}）："
                + "条件判的是 dealer，不是 target");
        });
    }

    /// <summary>
    /// 受控场景：玩家挂 ColossusPower（可选），按参数把易伤挂在攻击者或受击者身上，然后让敌人真的打一次，
    /// 返回玩家损失的生命。
    /// </summary>
    private static long DamageTaken(long vulnerableOnAttacker, long vulnerableOnVictim, bool withColossus)
    {
        var session = FormalSliceFixture.NewFormal();
        var player = FormalSliceFixture.Player(session);
        var enemy = FormalSliceFixture.Cultist(session);

        if (withColossus)
        {
            FormalSliceFixture.GivePower(session, player, ColossusPowerId, 1);
        }

        if (vulnerableOnAttacker > 0)
        {
            // 攻击者是敌人：把易伤挂在它身上。
            FormalSliceFixture.GivePower(session, enemy, VulnerableId, vulnerableOnAttacker);
        }

        if (vulnerableOnVictim > 0)
        {
            FormalSliceFixture.GivePower(session, player, VulnerableId, vulnerableOnVictim);
        }

        var before = FormalSliceFixture.Number(session, player, FormalSliceFixture.PlayerTypeId, "Hp");
        // 第一次敌方回合是念咒（不加伤害），第二次才是攻击：结束两次回合让攻击真的发生。
        FormalSliceFixture.EndTurn(session);
        FormalSliceFixture.EndTurn(session);
        var after = FormalSliceFixture.Number(session, player, FormalSliceFixture.PlayerTypeId, "Hp");
        return before - after;
    }
}
