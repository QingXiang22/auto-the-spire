# Power定义与生命周期参考

文档类型：参考（Reference）。适用于Core.TheSpire 0.9.0与base 0.7.0-dsl.52的纯DSL内容模型。编写步骤见[新增内容模块](../how-to/add-content-module.md)，现有内容修改见[修改单个内容](../how-to/edit-content.md)。以下接口均来自正式DSL，不是完整原版PowerModel API。

## 定义与共有状态

[PowerDefinition](../../../../mod-the-spire/base-spire/powers/rules/definitions.spire)模板参数如下。

| 参数 | 类型／默认 | 行为 |
| --- | --- | --- |
| definition | String(64)，必填 | 精确查找标识；同次启动重复登记失败 |
| allowNegative | Bool，false | 是否保留负层数；零层数始终移除 |
| debuff | Bool，false | 玩家新实例设置SkipTick；TickPowerDuration首次调用只清标志。不是自动注册衰减，也不是完整PowerType枚举 |
| singleStack | Bool，false | 再施加时本次增量先归0，再经过ModifyPowerStacks；首次创建仍使用stacks。保留当前Corruption／Hellraiser开发行为，不等同于完整原版Single策略 |
| instanceMode | String(16)，shared | shared按定义与持有者合并；per-applier再按施加者引用匹配；independent每次施加独立实例；其他值在登记时失败 |

根启动过程SetupPowerDefinitions发布RegisterPowerDefinitions并建立隐藏PowerLibrary。自身监听、寿命与私有状态由所属模块定义。定义中没有通用hook回调参数；使用普通DSL过程和实例订阅。

[Power实体](../../../../mod-the-spire/base-spire/combat/schema.spire)保留DefinitionId、隐藏Definition引用、Owner、可选HolderPlayer／HolderEnemy、Stacks（-999..999）、SkipTick和ExpiresAtTurnEnd，并有隐藏ApplierPlayer／ApplierEnemy、SourceCard／SourcePlay。每个实例恰好一个持有者，至多一个施加者；Kind及专属Counter／SelfDamage槽已移除。共享合并不覆盖首次创建的来源引用。

ExpiresAtTurnEnd是一次施加的临时寿命事实，保留临时FlameBarrier与永久Thorns的区别。其他专属寿命由模块订阅通知处理。

## 领域操作

以下过程位于[powers/rules/lifecycle.spire](../../../../mod-the-spire/base-spire/powers/rules/lifecycle.spire)。

| 过程 | 参数／返回 | 行为 |
| --- | --- | --- |
| ApplyPlayerPower | owner: Ref(Player)、definition: String(64)、stacks: Int(-999..999)；无返回 | 委托ApplyPower，对玩家施加 |
| ApplyEnemyPower | target: Ref(Enemy)、definition、stacks；无返回 | 委托ApplyPower，对敌人施加 |
| ApplyPlayerPowerUntilTurnEnd | owner、definition、stacks；无返回 | 施加时设置ExpiresAtTurnEnd，在AfterPlayerTurnFinished移除；保留敌方阶段的效果 |
| ApplyPower | player: Binding、enemy: Binding、definition: String(64)、stacks: Int(-999..999)、untilTurnEnd: Bool；返回Binding | 先查定义并验证恰好一个持有者，再按定义与持有者匹配实例；返回仍有效的Power，否则none |
| ApplyPowerWithSource | 上述参数加applierPlayer／applierEnemy／card／play: Binding；返回Binding | 验证来源及持有者后，已确认死亡的敌人直接返回none；其余经过ShouldApplyPower、ModifyPowerGiven（存在施加者时）、ModifyPowerReceived，再按instanceMode匹配；阻止及新实例零结果返回none |
| ApplyPlayerPowerFromCard／ApplyEnemyPowerFromCard | owner、card、play、definition、stacks；敌方另加target | 卡牌效果显式传执行来源与玩家施加者；不通过CurrentPlay猜来源。临时版本ApplyPlayerPowerFromCardUntilTurnEnd同样保留来源 |
| ModifyPowerAmount | power: Ref(Power)、delta: Int；无返回 | 加到Stacks；结果0或不允许的负数通过RemovePower移除。字段范围仍为-999..999 |
| RemovePower | power: Ref(Power)；无返回 | 有效实例在Powers中时先出集合、发PowerRemoved、再销毁；不直接destroy绕过清理 |
| RemoveEnemyPowersOnDeath | target: Ref(Enemy)；无返回 | 普通敌人的死亡通知完成后，标记DeathResolved并按Powers顺序调用RemovePower；重复调用无额外效果，存活目标失败。仅覆盖当前Power的源默认移除策略，保留／否决消费者尚未接入，见[死亡约定](../../../design/mod-the-spire/combat-end/spec.md) |
| ConsumePower | power: Ref(Power)；无返回 | 调用ModifyPowerAmount(-1) |
| TickPowerDuration | power: Ref(Power)；无返回 | SkipTick为true时只清标志，否则ConsumePower；不会自行安排调用时点 |
| ClearCombatPowers | 无参数／返回 | 遍历Powers并调用RemovePower，复用同一私有清理路径 |

所有施加调用均不接受kind。未知definition在创建或发送应用事件前失败。没有既有实例时，0量或不允许的负量返回none；已有实例通常将stacks作为增量，经ModifyPowerStacks折叠后改量。singleStack改变的是再施加增量，不是统一把首次层数截成1。

## 通知与顺序

| 事件 | 载荷 | 发布时点 |
| --- | --- | --- |
| RegisterPowerDefinitions | 无 | SetupPowerDefinitions时；定义模板登记各自Blueprint |
| PowerCreated | power: Ref(Power) | 新实例已加入Powers，基础字段与玩家SkipTick已设置；用于建立私有状态 |
| PowerApplied | power: Ref(Power)、amount: Int、created: Bool | 创建／合并后实例仍在Powers时；amount是Given／Received修正后的请求量，不保证等于最终层数增量 |
| PlayerPowerApplied | owner: Ref(Player)、power: Ref(Power)、amount: Int | PowerApplied之后的玩家侧通知 |
| EnemyPowerApplied | target: Ref(Enemy)、power: Ref(Power)、amount: Int | PowerApplied之后的敌方通知 |
| PowerRemoved | power: Ref(Power) | 已离开Powers，销毁之前；可按载荷清理所属私有entity |

层数变化导致移除时，不再发布应用通知。创建时不调用ModifyPowerStacks，再施加时才调用。重新施加已有玩家实例不会重新设置SkipTick。

Weak／Vulnerable／Frail在自己的模块监听AfterEnemyTurnEnded并调用TickPowerDuration。Rage等短期Power自行订阅AfterPlayerSideEnd。AfterBlockCleared在每次玩家回合开始的格挡处理阶段发布，包括首回合以及格挡被保留时；它不是“Block值发生变化”的通知。

显式EndDevelopmentCombat和下次BeginDevelopmentCombat调用ClearCombatPowers。自然胜负发布CombatEnded；开发场景沿用现有能力清理边界。正常胜利在该通知后清理所有能力、格挡及战斗牌，再通知早期／普通胜利效果并生成奖励。精确阶段见[战斗操作参考](operations.md)。

## 私有状态与未支持范围

私有entity声明在所属模板内，字段设为Hidden，通过实际Power引用关联。例如JugglingPower.State保存计数，RupturePower.Pending关联实际CardPlayRecord；模块负责创建、重置与移除清理。Engine保存这些实体；它们不是C#隐藏状态，也不应公开为玩法观察。

本接口未提供完整Creature实例类型、任意Power的合并策略、施加者／牌来源参数、完整DamageResult或私有状态复制协议。Rupture仍使用当前开发范围的EnemyPhase／CurrentPlay上下文；SingleStack与debuff标志只描述上述已实现策略。不能从一个已登记定义推导全量来源资格或独立原版等价性。
