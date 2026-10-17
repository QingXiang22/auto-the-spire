# 随机

查找或修改随机时从本目录开始。[全部领域](../README.md)。

| 位置 | 负责什么 |
| --- | --- |
| [VanillaRandomLayout.cs](VanillaRandomLayout.cs) | 原版随机流布局与派生 |
| [VanillaRandomProfile.cs](VanillaRandomProfile.cs) | 随机配置 |
| [SourceStableShuffle.cs](SourceStableShuffle.cs) | 源排序与稳定洗牌接缝 |
| [SourceWeightedPick.cs](SourceWeightedPick.cs) | 源权重抽取接缝 |

已有行为检查见 [BaseSpire.Slice.Tests/Randomness](../../BaseSpire.Slice.Tests/Randomness/)。

组合已有能力时先修改内容或所属领域模块；新增跨领域依赖或改变声明顺序时，再修改 [Composition](../Composition/README.md)。
