# 敌人与遭遇

查找或修改敌人与遭遇时从本目录开始。[全部领域](../README.md)。

| 位置 | 负责什么 |
| --- | --- |
| [Content/](Content/) | enemies*.spire 行为与 base-spire.encounters.json 遭遇源池 |
| [SpireEnemySource.cs](SpireEnemySource.cs) | 敌人行为解析与降级 |
| [SliceEnemyBehavior.cs](SliceEnemyBehavior.cs) | 类型化行动状态机 |
| [SliceEncounterPool.cs](SliceEncounterPool.cs) | 遭遇定义与查找 |
| [Compilation/](Compilation/) | 敌人、战斗实体、意图、行动和遭遇编译 |

已有行为检查见 [BaseSpire.Slice.Tests/Enemies](../../BaseSpire.Slice.Tests/Enemies/)。

组合已有能力时先修改内容或所属领域模块；新增跨领域依赖或改变声明顺序时，再修改 [Composition](../Composition/README.md)。
