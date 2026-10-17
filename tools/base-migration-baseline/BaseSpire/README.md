# base-spire 领域目录

本页导航当前过渡实现。目标为Engine主体、完整DSL规则库与Spire内容子集；现有C#规则模块待迁移，不能因已按领域分类视为目标架构完成。见[迁移设计](../../../docs/design/mod-the-spire/engine-dsl-migration.md)。

按要维护的游戏对象选目录。每个领域收拢自己的内容、解析、规则和编译代码；公共组包只负责组合。

| 领域 | 入口 |
| --- | --- |
| 卡牌 | [Cards](Cards/README.md) |
| Power／能力 | [Powers](Powers/README.md) |
| Relic／遗物 | [Relics](Relics/README.md) |
| 药水 | [Potions](Potions/README.md) |
| 敌人与遭遇 | [Enemies](Enemies/README.md) |
| 共享战斗 | [Combat](Combat/README.md) |
| 房间与事件 | [Rooms](Rooms/README.md) |
| 地图 | [Maps](Maps/README.md) |
| 整局 | [Run](Run/README.md) |
| 奖励 | [Rewards](Rewards/README.md) |
| 随机 | [Randomness](Randomness/README.md) |
| 规则包组装 | [Composition](Composition/README.md) |
| 作者模型与语言 | [Authoring](Authoring/README.md) |
| 正式单人入口 | [SinglePlayer](SinglePlayer/)：配置、内容加载、工厂和公开语义 |

## 放文件的规则

- 先按领域选目录，再按职责进入 `Content`、`Compilation`、`Runtime` 等子目录。同一领域的内容和机制应能从它的 README 找到。
- Power 放 `Powers`，遗物放 `Relics`；敌人专属且与行为共用声明的 Power 仍在 `Enemies/Content`，能力加载器会一并发现。
- 卡牌类型 `power` 的卡仍是卡牌，放 `Cards/Content`；它施加的能力放 `Powers/Content`。
- 房间的价格、资格与离场留在 `Rooms`；获得遗物、资源和永久牌组操作分别调用 `Relics`、`Run`、`Cards` 的公共能力。
- 检查放 `BaseSpire.Slice.Tests` 的对应领域目录，共用夹具放 `Shared`；不因移动目录新增重复行为检查。
- 新增内容文件后核对 `BaseSpire.csproj` 的领域资源规则。现有 `*-cards.spire`、`*-powers.spire`、`enemies*.spire` 的文件命名参与资源发现，同名文件不可重复。

## 与既有接口的关系

目录按领域组织；`BaseSpire.Slice.*` 命名空间和公开类型保持兼容，不能用命名空间推断物理路径。嵌入资源仍使用 `BaseSpire.ContentSource.*`、`BaseSpire.Content.*` 和 `BaseSpire.SliceAuthor.*` 逻辑名，保持内容摘要与发现顺序。实际诊断来源路径随文件迁移更新，规则集与公开构建身份因此需要重新核对。

正式运行从 [SinglePlayer/BaseSpireRuntimeFactory.cs](SinglePlayer/BaseSpireRuntimeFactory.cs)进入。开发步骤见 [base开发指南](../../../docs/design/mod-the-spire/base-development-guide.md)，运行检查见 [模块入口](../README.md)。
