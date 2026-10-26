# 为遗物与Power编写通知和修正监听

文档类型：操作指南（How-to）。适用于新增或迁移遗物、Power的触发和计算修正。以当前纯DSL包为前提，使用core/0.3文本和支持实例订阅的引擎；先核对[包manifest](../../../../mod-the-spire/base-spire/package.json)。本页给出编写方法，顺序与寿命以[领域约定](../../../design/mod-the-spire/effect-listeners.md)为准，参数和错误语义查[通用作者参考](../../../reference/dsl/0.3/authoring-reference.md#实例订阅与只读修正)。

## 1. 选择通知或修正入口

先核对源方法要做的是响应一个事实，还是改变当前计算。查看[领域接口](../../../../mod-the-spire/base-spire/combat/modifier-hooks.spire)、[战斗时机](../../../../mod-the-spire/base-spire/combat/lifecycle.spire)和[操作参考](../reference/operations.md)，复用已有入口。

| 需求 | 主流程 | 监听者 | 实际消费者 |
| --- | --- | --- | --- |
| 洗牌后得格挡、出牌后推进计数、受损后抽牌、卡牌耗尽后伤害 | 在准确阶段emit事件，然后等待监听完成再继续 | 调用完整资源操作或更新本实例状态，可以产生副作用 | TheAbacus、Shuriken、CentennialPuzzle、CharonsAshes |
| 修改伤害、费用、抽牌数或能量策略 | 在计算阶段调用foldEvent，传初值与显式上下文 | pure过程返回当前修正值，不改游戏状态 | Strength、StrikeDummy、Corruption、IceCream |

例如AfterEnergyReset通知只能处理重置之后的效果；决定是否重置应监听ShouldResetEnergy。不要先让主流程完成错误操作，再用事后通知补偿。缺少时机时先按[机制迁移](migrate-mechanism.md)约定输入、顺序、失败与验收，再在拥有该流程的领域发布入口；主流程不判断具体遗物ID，也不读取遗物专属配置字段。

## 2. 将订阅绑定到有效实例

订阅的instances查询负责筛选持有者、能力标识和有效状态。reaction接收完整事件载荷，末尾增加listener实例参数；过程只处理当前实例，不再自行扫描整个库存。

以下是[抽牌教学模板](../../../examples/base-dsl/after-draw-block.spire)中的摘录，位于AfterDrawBlockRelic的body内；definition和amount是该模板声明的参数，不是独立文件中的全局变量：

```text
procedure WhenDrawn {
  parameters = { owner = Ref(Player) card = Ref(Card) listener = Ref(Relic) }
  body = {
    call = { procedure = GainUnpoweredBlock args = { owner = owner amount = arg(amount) } }
  }
}
subscription OnDraw {
  event = AfterCardDrawn
  reaction = WhenDrawn
  instances = where(collection(Relics), r, and(equal(field(r, Relic.Holder), owner), and(not(field(r, Relic.IsMelted)), equal(field(r, Relic.DefinitionId), arg(definition)))))
  instance_order = pureCall(RelicHookOrder, listener, false)
  priority = 0
}
```

instances只能读取事件载荷和查询自身的绑定，不能用尚未绑定的listener筛选自己。instance_order在实例绑定后求值，可以读取listener。引擎在通知开始时固定候选和排序键，按priority、instance_order、声明顺序执行；执行前再次检查资格，因此移除或熔化能取消后续响应，新获得实例不会补入已经开始的通知。优先级与排序表达游戏约定，不用文件includes顺序代替实际持有顺序。

完整的复制、登记、获得与公开运行步骤沿[共享触发教程](../../../start/shared-rule.md)操作。拾取事件已经携带本次relic，直接过滤该载荷即可；移除事件在出库存后发出，清理订阅不能要求它仍在Relics中。

主动弃牌使用 `CardDiscarded`：`DiscardCombatCard` 先移动牌、完成 `CardMoved`，再发布弃牌通知。ToughBandages 按有效持有实例响应，并复核 CombatActive／EnemyPhase；普通移牌和回合末清手只移动牌，不触发该遗物。

`CardExhausted(owner,card,causedByEthereal)` 在 `ExhaustCombatCard(owner,card,causedByEthereal)` 把卡牌放入 `ExhaustPile` 后发布。CharonsAshes 订阅时过滤 `Relic.Holder == owner`、未熔化和定义 ID，反应中调用 `DamageAllEnemiesFromPlayer`；因此嵌套耗尽会逐事件结算，且每个有效遗物实例只响应自己的持有者。

## 3. 用pure监听返回修正值

修正事件的第一个载荷是累计值。监听者接收它，返回修正后的同类型值；不适用时返回原值。没有有效监听者时，foldEvent返回调用方的初值。以下是[Strength监听](../../../../mod-the-spire/base-spire/powers/shared/strength-power.spire)的等价写法，用于阅读或替换原过程和订阅，不能在同一包重复登记同名声明：

```text
procedure Strength {
  pure = true
  parameters = { value = Int owner = Ref(Player) card = Binding target = Binding listener = Ref(Power) }
  result = Int
  body = { return = { value = add(value, field(listener, Power.Stacks)) } }
}
subscription Strength {
  event = ModifyAttackAdditive
  reaction = Strength
  instances = where(collection(Powers), p, and(equal(field(p, Power.HolderPlayer), owner), equal(field(p, Power.DefinitionId), "base-spire:strength-power")))
  instance_order = pureCall(PowerHookOrder, listener, false)
  priority = 0
}
```

伤害主流程调用foldEvent(ModifyAttackAdditive, amount, owner, card, target)。Strength贡献自己的Stacks；StrikeDummy订阅同一阶段，筛选Relic实例，再按牌的Strike标签贡献加值。两者使用相同订阅结构，差别是实例类型、资格与效果。牌源或目标可能为空的Binding要先检查，不能把全局CurrentCard当作传入上下文的替代。

修正过程不能写实体、消费随机、发布事件或请求决策，也不能创建临时实体作为预览缓存。加法、乘法和取整分属明确阶段；不能把每个乘法监听的中间值提前取整。预览、合法动作费用和真实执行复用同一修正链，例如Thrash吸收伤害也必须经过攻击修正。Power行为由所属定义模块注册；调用方只传definition和stacks，不再传Kind。

## 4. 把状态和生命周期归到能力实例

遗物与Power共用监听方式，不要求它们共用全部字段。优先复用现有实例层数／计数；能力特有的可变状态放对应附属实体，明确获得或施加时创建、叠加策略、跨回合／跨战重置及移除时清理。ArtOfWar.State是[实际示例](../../../../mod-the-spire/base-spire/relics/rules/combat-modifiers.spire)。不要为单件遗物在公共Relic不断增加配置字段，再让主流程汇总。

Power的持有者和层数在combat/schema.spire，定义与施加／合并／移除在powers/rules；具体修正、通知、到期和私有状态放powers/shared所属模块。遗物独有行为也可留在所属文件，真正共用的模板放relics/rules。新增文件须显式加入索引，源目录与公开元数据按[内容扩展](extend-content.md)分别维护。

## 5. 迁移旧行为并验证

将主流程中的专用遗物查询改为对应foldEvent，将直接调用Power效果过程改为领域通知，再把效果移到实例监听。删除被替代的过程、字段和重复库存扫描；保留真正的领域状态读取，例如卡牌自身读取特定Power层数，并说明它与通用修正的区别。

按实际风险选能区分错误的案例，复用已有用例：

- 反转获得顺序后，用非交换的效果观察实例顺序，而非只检查最终总和。
- 派发中移除／熔化后续实例，验证跳过；新增实例不追溯参与。
- 嵌套自动出牌或抽牌、决策暂停续接时，不重复支付、计数或已完成效果。
- 多次Observe和LegalActions前后，状态hash、版本和随机消费保持一致；修正值由实际有效监听者贡献。
- 移除后附属状态清理，下一战按源重建；已有内容数值、寿命和胜负余效保留。

命令选择见[验证指南](verify-and-debug.md)。当前遗物定向入口是check --relic-content-only，完整行为报告用check --report；新内容或新机制需实际新增判别，旧命令通过不自动证明任意新效果。教学与受控探针不算独立原版比较或正常Run验证记录。

耗尽调用者必须明确传causedByEthereal。回合末虚无及带回合末效果的虚无牌收尾为true；普通出牌、选牌、强制耗尽和正文效果为false，不能由牌是否带虚无关键词反推原因。全部订阅接收同一完整载荷，即使不使用原因。直接生成到ExhaustPile仅是放置，不发布CardExhausted。
