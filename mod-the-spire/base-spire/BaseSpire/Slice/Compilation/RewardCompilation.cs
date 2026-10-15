using CoreTheSpire.Engine;
using BaseSpire.Slice.Effects;
using BaseSpire.Slice.Orchestration;
using static BaseSpire.Slice.Compilation.CompilationCallbacks;
using static BaseSpire.Slice.CombatSlicePackage;

namespace BaseSpire.Slice.Compilation;

internal static class RewardCompilation
{
    internal static RewardsBindings Rewards(
        ContentBindings contentBindings,
        CardsBindings cardsBindings,
        PotionsBindings potionsBindings,
        RunStateBindings runStateBindings,
        RunCollectionsBindings runCollectionsBindings,
        RoomServicesBindings roomServicesBindings,
        RewardCardFieldsCallback RewardCardFields)
    {

        // ENV-T05：**真实战斗奖励**子包，同样只在 actMap 开启时声明（`BaseSpireScope.Dev`、受控切片与
        // 既有夹具的包形状逐字不变）。它写地图子包的 `MapPhase`（奖励开放 = 3），因此必须排在
        // `MapRoomWiring.Declare` 之后；候选卡实体复用同一卡类型与 `CardFields`（升级形态走变体字段）。
        // 赋值点刻意放在实例变量字段（`variableFields`）建好之后：`RewardCardFields` 是局部函数，
        // 转成委托时会捕获它，提前转会在编译期报"使用了未赋值的局部变量"。
        RewardWiring? rewards = null;

        // ENV-T05：奖励 wiring 必须在 SettleBattle 的 IR 生成前就绪；否则结算体里的
        // `rewards?.SettleSteps(...)` 会在构建期被折叠为空，清场后永远不会执行 OpenRewards。
        if (contentBindings.Author is not null)
        {
            foreach (var name in contentBindings.Author.Cards
                         .SelectMany(entry => entry.NormalVariables.Keys.Concat(entry.UpgradedVariables.Keys))
                         .Distinct(StringComparer.Ordinal)
                         .Order(StringComparer.Ordinal))
            {
                cardsBindings.VariableFields[name] = cardsBindings.Card.AddField($"Variable_{name}", FieldTypeSchema.Int(-999, 999));
            }
        }

        if (roomServicesBindings.ActMap is not null && contentBindings.Author is not null)
        {
            rewards = RewardWiring.Declare(contentBindings.Package, runStateBindings.RunEntity!, cardsBindings.Card, runCollectionsBindings.RunStates!, roomServicesBindings.DeckCommands!, roomServicesBindings.Resources!,
                runStateBindings.RunCurrentRoom!, runStateBindings.RunRewardTaken!, roomServicesBindings.ActMap.RunMapPhase, cardsBindings.CardDefinitions.Compiled, RewardCardFields.Invoke,
                afterClose: scope =>
                {
                    var run = scope.First(scope.Collection(runCollectionsBindings.RunStates!));
                    return [scope.If(
                        scope.Equal(scope.Field(scope.Field(run, roomServicesBindings.ActMap.RunCurrentNode), roomServicesBindings.ActMap.NodeType),
                            scope.EnumConst(roomServicesBindings.ActMap.PointType, "Boss")),
                        scope.SetField(run, roomServicesBindings.ActMap.RunActCompleted, scope.Const(true)),
                        scope.SetField(run, roomServicesBindings.ActMap.RunMapPhase, scope.Const((long)MapRoomWiring.ActCompletedPhase)))];
                },
                isBoss: scope => scope.Equal(scope.Field(
                    scope.Field(scope.First(scope.Collection(runCollectionsBindings.RunStates!)), roomServicesBindings.ActMap.RunCurrentNode), roomServicesBindings.ActMap.NodeType),
                    scope.EnumConst(roomServicesBindings.ActMap.PointType, "Boss")), potions: potionsBindings.Potions);
        }

        return new RewardsBindings(rewards);
    }
}
