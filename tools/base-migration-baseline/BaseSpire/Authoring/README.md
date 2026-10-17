# 作者模型与语言

查找或修改作者模型与语言时从本目录开始。[全部领域](../README.md)。

| 位置 | 负责什么 |
| --- | --- |
| [SliceAuthorFrontend.cs](SliceAuthorFrontend.cs) | 共享作者模型及旧 .author 前端 |
| [Content/](Content/) | 共享状态与生命周期规则库 |
| [Examples/](Examples/) | 兼容 .author 示例；正式单人内容从各领域加载 |

已有行为检查见 [BaseSpire.Slice.Tests/Authoring](../../BaseSpire.Slice.Tests/Authoring/)。

组合已有能力时先修改内容或所属领域模块；新增跨领域依赖或改变声明顺序时，再修改 [Composition](../Composition/README.md)。
