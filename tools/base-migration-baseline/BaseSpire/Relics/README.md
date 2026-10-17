# Relic／遗物

查找或修改Relic／遗物时从本目录开始。[全部领域](../README.md)。

| 位置 | 负责什么 |
| --- | --- |
| [Content/relics.json](Content/relics.json) | 已实现遗物的效果声明；按 Name 或 ContentId 查找 |
| [Content/relic-source-catalog.json](Content/relic-source-catalog.json) | 完整源身份、池成员、源摘要与单人资格；不是可执行效果表 |
| [SliceRelics.cs](SliceRelics.cs) | 声明模型、触发点、效果种类、加载与严格校验 |
| [RelicSourceCatalog.cs](RelicSourceCatalog.cs) | 完整源清单的加载与资格核对 |
| [RunRelics.cs](RunRelics.cs) | 遗物实例、获得、移除、替换、熔化与计数 |
| [Compilation/RelicCompilation.cs](Compilation/RelicCompilation.cs) | 按持有顺序生成遗物触发与修正程序 |
| [SharedRelicPoolManifest.cs](SharedRelicPoolManifest.cs) | 原版共享池顺序 |
| [TreasureRelicBag.cs](TreasureRelicBag.cs) | 宝箱遗物袋 |
| [TreasureRelicRules.cs](TreasureRelicRules.cs) | 宝箱遗物资格与抽取规则 |

已有行为检查见 [BaseSpire.Slice.Tests/Relics](../../BaseSpire.Slice.Tests/Relics/)。

组合已有能力时先修改内容或所属领域模块；新增跨领域依赖或改变声明顺序时，再修改 [Composition](../Composition/README.md)。
