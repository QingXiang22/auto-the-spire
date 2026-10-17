# 整局

查找或修改整局时从本目录开始。[全部领域](../README.md)。

| 位置 | 负责什么 |
| --- | --- |
| [Content/](Content/) | 幕与整局路线源清单 |
| [Compilation/](Compilation/) | 整局状态声明和新局编译 |
| [RunInitializationWiring.cs](RunInitializationWiring.cs) | 真实开局库存与初始化 |
| [RunActWiring.cs](RunActWiring.cs) | 切幕与幕生命周期 |
| [RunSourcePoolManifest.cs](RunSourcePoolManifest.cs) | 整局源池、事件与队列顺序 |
| [RunResources.cs](RunResources.cs) | 金币、生命、最大生命等整局公共操作 |

已有行为检查见 [BaseSpire.Slice.Tests/Run](../../BaseSpire.Slice.Tests/Run/)。

组合已有能力时先修改内容或所属领域模块；新增跨领域依赖或改变声明顺序时，再修改 [Composition](../Composition/README.md)。
