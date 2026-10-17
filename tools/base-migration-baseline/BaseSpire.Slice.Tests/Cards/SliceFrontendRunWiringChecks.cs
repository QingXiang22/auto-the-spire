using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// ENV-03 前置：**整局实体是否真的被建立、是否可被战斗侧读到**。
///
/// 背景（B-07a）：切片的正式构建里声明了 `Run` 实体、`RunStates` 集合与 `SettleBattle` 过程，
/// 但当时的报告记载"自动接线未接通——在 setup 末尾紧接着创建后调用也**取不到实例**"，
/// 因此只保留了过程定义。本检查先测**事实**：集合里到底有没有实例、字段初值是多少。
/// 这是"战斗结果写回整局"这条接线的前提，也是 ENV-03（真实整局）的第一块地基。
/// </summary>
internal static class SliceFrontendRunWiringChecks
{
    private const string RunTypeId = "base-spire:Run";
    private const string RunStatesCollection = "base-spire:RunStates";

    public static void Run()
    {
        Check.Run("ENV-03 前置：正式构建里有唯一的整局实例，且字段初值来自声明的 run 段 [真实]", () =>
        {
            var session = FormalSliceFixture.NewFormal();
            var runStates = FormalSliceFixture.Collection(session, RunStatesCollection);
            var members = session.State.CollectionMembers(runStates).ToList();
            Check.Equal(1, members.Count,
                $"RunStates 集合里应有恰好 1 个整局实例（实际 {members.Count}）——"
                + "这是把战斗结果写回整局的前提");

            var run = members[0];
            var hp = FormalSliceFixture.Number(session, run, RunTypeId, "Hp");
            var maxHp = FormalSliceFixture.Number(session, run, RunTypeId, "MaxHp");
            var gold = FormalSliceFixture.Number(session, run, RunTypeId, "Gold");
            Check.True(maxHp > 0, $"整局 MaxHp 应为正（实际 {maxHp}）");
            Check.True(hp > 0 && hp <= maxHp, $"整局 Hp 应在 (0, MaxHp] 内（实际 Hp={hp} / MaxHp={maxHp}）");
            Check.True(gold >= 0, $"整局 Gold 不应为负（实际 {gold}）");
        });
    }
}
