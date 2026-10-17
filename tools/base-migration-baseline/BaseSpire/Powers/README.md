# Power／能力

查找或修改Power／能力时从本目录开始。[全部领域](../README.md)。

| 位置 | 负责什么 |
| --- | --- |
| [Content/](Content/) | 共享、角色、药水与下回合能力的声明；按文件内 power 名称查找 |
| [SpirePowerSource.cs](SpirePowerSource.cs) | 正式能力解析、身份与降级 |
| [PowerRules.cs](PowerRules.cs) | 共享能力规则类型及作者语法 |
| [Runtime/PowerRuleRuntime.cs](Runtime/PowerRuleRuntime.cs) | 能力触发程序与规则库绑定 |
| [Compilation/PowerCompilation.cs](Compilation/PowerCompilation.cs) | 叠加、查询、衰减、移除与触发的编译接入 |

已有行为检查见 [BaseSpire.Slice.Tests/Powers](../../BaseSpire.Slice.Tests/Powers/)。

组合已有能力时先修改内容或所属领域模块；新增跨领域依赖或改变声明顺序时，再修改 [Composition](../Composition/README.md)。
