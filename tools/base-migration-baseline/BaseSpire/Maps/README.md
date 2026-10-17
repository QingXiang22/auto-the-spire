# 地图

查找或修改地图时从本目录开始。[全部领域](../README.md)。

| 位置 | 负责什么 |
| --- | --- |
| [Generation/](Generation/) | 源地图数据类型、生成、剪枝与后处理 |
| [SourceActMapCompute.cs](SourceActMapCompute.cs) | 地图生成的纯计算接缝 |

行为检查随对应领域的实际消费者维护。

组合已有能力时先修改内容或所属领域模块；新增跨领域依赖或改变声明顺序时，再修改 [Composition](../Composition/README.md)。
