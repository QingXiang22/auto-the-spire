using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// ENV-02：`Rampage` 的**卡牌实例变量**——`variables = { variable = { name = ExtraDamage default = 0 } }`
/// 配合 `scaledAttack(base=9, perCount=1, count={ cardVariable = { variable = ExtraDamage } })`
/// 与 `modifyCardVariable { variable = ExtraDamage amount = 5 }`。
///
/// 原版依据（`sts2/src/Core/Models/Cards/Rampage.cs`）：打出时造成 `Damage` 点伤害，然后给这张牌的
/// 伤害变量 +5（升级 +9）。运行时的存储机制早于本批次存在（`Variable_&lt;name&gt;` 字段 + 运行时 `Grow`），
/// 本批次补的是 `.spire` 前端到它的映射。
///
/// 判别性设计：**同一个卡实例在连续两回合打出**——第一次读到的变量是 0（伤害 9），第二次读到 5（伤害 14）。
/// 只验证"有伤害"会把变量成长漏掉；只验证"变量变了"会把"伤害真的读它"漏掉。
/// （同一回合内重复打出同一实例会撞上"已在本回合打出"的成员冲突，因此跨回合。）
/// </summary>
internal static class SliceFrontendRampageVariableChecks
{
    private const string RampageId = "base-spire:rampage";
    private const string MonsterTypeId = "base-spire:Monster";
    private const string CardTypeId = "base-spire:Card";
    private const string ExtraDamageField = "Variable_extra-damage";

    public static void Run()
    {
        Check.Run("ENV-02 Rampage：伤害读卡牌实例变量，且每次打出把变量 +5（跨两回合）[真实]", () =>
        {
            var session = FormalSliceFixture.NewFormal();
            var enemy = FormalSliceFixture.Cultist(session);
            var card = FormalSliceFixture.MoveToHand(session, RampageId);

            var hpBeforeFirst = FormalSliceFixture.Number(session, enemy, MonsterTypeId, "Hp");
            FormalSliceFixture.PlayAt(session, card, enemy);
            var hpAfterFirst = FormalSliceFixture.Number(session, enemy, MonsterTypeId, "Hp");
            Check.Equal(9L, hpBeforeFirst - hpAfterFirst,
                $"第一次打出：base 9 + 变量 0 ⇒ 9 点伤害（{hpBeforeFirst} → {hpAfterFirst}）");
            Check.Equal(5L, FormalSliceFixture.Number(session, card, CardTypeId, ExtraDamageField),
                "打出后变量 +5（来源 `modifyCardVariable { amount = 5 }`）");

            FormalSliceFixture.EndTurn(session);
            var hpBeforeSecond = FormalSliceFixture.Number(session, enemy, MonsterTypeId, "Hp");
            FormalSliceFixture.PlayAt(session, FormalSliceFixture.MoveToHand(session, RampageId), enemy);
            var hpAfterSecond = FormalSliceFixture.Number(session, enemy, MonsterTypeId, "Hp");
            Check.Equal(14L, hpBeforeSecond - hpAfterSecond,
                $"第二次打出：base 9 + 变量 5 ⇒ 14 点伤害（{hpBeforeSecond} → {hpAfterSecond}）；"
                + "说明伤害步骤真的读了实例变量，而不是固定 9");
            Check.Equal(10L, FormalSliceFixture.Number(session, card, CardTypeId, ExtraDamageField),
                "第二次打出后再 +5 ⇒ 变量累计 10（跨回合保留，不是每回合重置）");
        });
    }
}
