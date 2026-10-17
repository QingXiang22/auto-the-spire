# 药水

查找或修改药水时从本目录开始。[全部领域](../README.md)。

| 位置 | 负责什么 |
| --- | --- |
| [Content/potions.spire](Content/potions.spire) | 药水元数据、源池顺序与使用效果 |
| [SpirePotionSource.cs](SpirePotionSource.cs) | 药水解析与降级 |
| [PotionEffectSource.cs](PotionEffectSource.cs) | 药水效果编译 |
| [PotionInventoryRules.cs](PotionInventoryRules.cs) | 领取、槽位、容量与使用生命周期 |

已有行为检查见 [BaseSpire.Slice.Tests/Potions](../../BaseSpire.Slice.Tests/Potions/)。

组合已有能力时先修改内容或所属领域模块；新增跨领域依赖或改变声明顺序时，再修改 [Composition](../Composition/README.md)。
