# 卡牌

查找或修改卡牌时从本目录开始。[全部领域](../README.md)。

| 位置 | 负责什么 |
| --- | --- |
| [Content/](Content/) | 各角色、无色、状态与机制族的卡牌声明 |
| [SpireCardSource.cs](SpireCardSource.cs) | 正式卡牌解析与降级 |
| [Effects/](Effects/) | 类型化效果、数值、校验与IR生成 |
| [Runtime/](Runtime/) | 出牌、费用、牌区、抽牌、选择、附魔与历史 |
| [Compilation/CardCompilation.cs](Compilation/CardCompilation.cs) | 把卡牌命令接入规则包 |
| [Pools/](Pools/) | 奖励与商店牌池资格 |
| [RunDeck.cs](RunDeck.cs) | 永久牌组的获得与移除 |

已有行为检查见 [BaseSpire.Slice.Tests/Cards](../../BaseSpire.Slice.Tests/Cards/)。

组合已有能力时先修改内容或所属领域模块；新增跨领域依赖或改变声明顺序时，再修改 [Composition](../Composition/README.md)。
