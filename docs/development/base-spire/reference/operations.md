# 共享 DSL 操作参考

文档类型：参考（Reference）。适用于 core/0.3 与纯 DSL base 的遗物抽牌开发包。只列实际存在的声明；这里没有列出的游戏操作不能按名字猜测可调用。文件路径相对 `mod-the-spire/base-spire/`，精确实现通过各行链接定位。

## 状态与事件

| 声明 | 字段／载荷 | 约束与归属 |
| --- | --- | --- |
| [Player](../../../../mod-the-spire/base-spire/combat/schema.spire) | Owner: Participant；Block: Int(0..999)=0；DrawBlocked: Bool=false | 开发玩家，没有 HP／Energy／回合状态 |
| [Card](../../../../mod-the-spire/base-spire/cards/schema.spire) | Owner: Participant；Label: String(64) | 开发抽牌对象，没有卡牌定义、费用、升级、目标和效果 |
| Hand／DrawPile／DiscardPile | 有序 Card 集合；Hand 容量 10 | 当前是全局开发牌堆，尚无按持有者分堆；DrawPile 隐藏身份及顺序 |
| [Relic](../../../../mod-the-spire/base-spire/relics/schema.spire) | Owner、Holder、DefinitionId、IsMelted=false、Counter=0 | Holder 是隐藏 Ref(Player)，Counter 隐藏；DefinitionId 长度上限 64；Counter 为 0..999999 |
| Relics | 有序 Relic 集合 | 监听器遍历现有库存并过滤 Holder、definition 和熔化状态 |
| RelicObtained／RelicRemoved | owner: Ref(Player)、relic: Ref(Relic) | 获得时已入库存；移除时已出库存，事件执行后才销毁实例 |
| AfterShuffle | owner: Ref(Player) | 仅在实际转移并完成 shuffle 后发出 |
| AfterCardDrawn | owner: Ref(Player)、card: Ref(Card) | 成功移到 Hand 后发出；未抽到牌不发 |

## 过程与公开动作

| 声明 | 准确参数 | 行为与边界 |
| --- | --- | --- |
| [GainUnpoweredBlock](../../../../mod-the-spire/base-spire/combat/block.spire) | owner=Ref(Player)、amount=Int(0..999) | 累加并截到 999，不应用卡牌／Power 的加性或乘性修正；不是通用获得格挡流程 |
| [ObtainRelic](../../../../mod-the-spire/base-spire/relics/rules/lifecycle.spire) | owner=Ref(Player)、definition=String(64) | 在 owner 作用域创建、写 Owner／Holder／DefinitionId、加入 Relics 后发获得事件；过程本身不去重 |
| RemoveRelic | instance=Ref(Relic) | 必须在 Relics；先移出，再发移除事件，再销毁；不持有时报错 |
| RemoveOwnedRelic | instance=Ref(Relic)，公开候选来自 Relics | action，调用 RemoveRelic；参数必须来自实际候选，不能伪造实例字符串 |
| [ReshuffleDiscard](../../../../mod-the-spire/base-spire/cards/rules/draw.spire) | owner=Ref(Player) | DrawPile 空且 DiscardPile 非空才逐项转移、shuffle、emit AfterShuffle；开发流名 base-spire:development-shuffle |
| DrawOne | owner=Ref(Player) | 未禁止抽牌且手牌不足 10 时先尝试重洗，再取首张，成功移动后 emit AfterCardDrawn |
| Draw／DiscardHand | 无参数，role=player | Draw 使用 first(type(Player))；DiscardHand 将全局 Hand 移到 DiscardPile；均是开发动作 |
| [SetupDevelopmentDraw](../../../../mod-the-spire/base-spire/run/setup-dev.spire) | 无参数；根 start 调用一次 | 创建一个 Player 和弃牌堆三张开发 Card；不提供正常 Run 初始化 |

调用现有共享操作时使用具名参数。例如：

```text
call = { procedure = GainUnpoweredBlock args = { owner = owner amount = 6 } }
```

这里的 owner 必须是明确传入／绑定的 Ref(Player)。`participant(1)` 是 Participant，不能代替实体引用。普通内容不应绕过生命周期直接更新库存或引用字段。

## 模板与内容配置

[AfterShuffleBlockRelic](../../../../mod-the-spire/base-spire/relics/rules/after-shuffle-block.spire) 的参数为 definition: String(64)、amount: Int(0..999)。它生成 `WhenShuffled`、`OnShuffle` 与 `Obtain`，按当前持有库存过滤并调用 GainUnpoweredBlock。`use TheAbacus` 生成 `TheAbacus.Obtain` 等名称；TheAbacus 正式参数为 6。

模板获得动作通过 definition 检查重复，并使用 first(type(Player))。多 Player、同定义多实例、按拥有者牌堆、替换、熔化操作与跨战重建尚未在该模板契约交付；不能将开发动作当作这些功能的实现。

[source-catalog 的 definition](../../../dsl/core/0.3/package-reference.md) 指向的内容文件只能包含头部与 `use`。共享过程／模板放规则文件。示例新增 AfterDrawBlockRelic 位于 docs/examples，仅用于[第二篇教程](../tutorials/shared-rule.md)，不是正式包现有公共操作。

## 公开投影

[PlayerView](../../../../mod-the-spire/base-spire/public/projection.spire) 由 DSL 声明 Player／Card／Relic；[公开元数据](../../../../mod-the-spire/base-spire/public/metadata.json) 解释 DefinitionId 内容列表与集合用途。Holder 和 Counter 不公开，开发牌堆没有正常卡牌或 known-position 契约。

公开 action_type 为 `net:action:base-spire:TheAbacus.Obtain`，格挡字段为 `net:field:base-spire:Player.Block`。协议用真实候选 ID 提交动作；元数据不改变状态、触发顺序或合法动作守卫。
