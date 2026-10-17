# 共享战斗

查找或修改共享战斗时从本目录开始。[全部领域](../README.md)。

| 位置 | 负责什么 |
| --- | --- |
| [Compilation/](Compilation/) | 伤害、格挡、死亡、回合、动作与战斗结算 |
| [CombatLifecycle.cs](CombatLifecycle.cs) | 战斗进入与重置 |
| [Mechanics/](Mechanics/) | Poison、Doom、Osty、Forge、Stars与球等共享领域操作 |

已有行为检查见 [BaseSpire.Slice.Tests/Combat](../../BaseSpire.Slice.Tests/Combat/)。

组合已有能力时先修改内容或所属领域模块；新增跨领域依赖或改变声明顺序时，再修改 [Composition](../Composition/README.md)。
