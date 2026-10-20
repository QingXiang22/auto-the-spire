# 开发 Power、遗物与药水

文档类型：操作指南（How-to）。先从对应catalog查源身份，再选择[修改已有内容](edit-content.md)、[新增独有效果模块](add-content-module.md)或补共享生命周期。纯 DSL 已有遗物持有实例、拾取资源、战斗／回合触发、出牌计数、首次受损抽牌、首回合抽牌及营火治疗修正，也可复用已迁移的伤害、Power与药水操作。准确接口见[操作参考](../reference/operations.md)，实际内容与验证范围见[GitHub 任务清单](https://github.com/QingXiang22/auto-the-spire/issues/28)；不能将源目录或旧C#覆盖数量视为当前DSL支持。

## 实现 Power 生命周期

遗物与Power使用相同的通知／修正订阅模型。新增或迁移监听先沿[编写通知与修正监听](write-effect-listeners.md)选择入口、绑定实例并安排顺序；本页继续说明物品和能力各自的生命周期。定义标志、施加／合并／移除签名与通知顺序统一查[Power参考](../reference/powers.md)。

1. 在powers/shared新增内容模块，用PowerDefinition声明definition、allowNegative、debuff、singleStack。通用Power实例只保留持有者、定义引用、层数与共有寿命事实；特有状态放模块内部实体，以实际Power引用关联。
2. 在同一文件注册通知／修正／到期监听；PowerCreated时建立私有状态，PowerRemoved时清理。施加调用ApplyPlayerPower／ApplyEnemyPower(owner或target、definition、stacks)，移除调用RemovePower，降层调用ModifyPowerAmount。未知定义在产生实例前失败；两持有者不合并。debuff标志只服务已迁移Weak／Vulnerable／Frail的首次跳过衰减，其他寿命由模块监听决定。
3. 用 combat 的统一修正阶段处理力量、敏捷、易伤等数值参与者，顺序和精度取自源契约；不要把 Power 数值缓存成另一个 C# 事实。
4. 验证施加与初始持有差异、叠加／降层／到零、无限持续、两持有者隔离、移除后停止订阅，以及暂停续接不重复施加。

需要源入口时按 `powers/catalog.json` 的 source_path 定位；旧源 `Powers` 目录仅辅助查迁移意图。不要将已记录的 260 个源身份当作 260 种已实现行为。

## 扩展遗物实例与触发

复用当前 [ObtainRelic／RemoveRelic](../reference/operations.md) 时，遵守入库后发获得事件、出库后发移除事件、随后销毁的顺序。获得过程本身不去重，模板 action 的守卫才限制当前开发范围的重复 definition。需要正常库存资格、唯一性或奖励获得时先补对应契约。

新触发先列 owner／source／target、调用时点、是否需要有效实例、每个实例的计数与清理、允许取消／暂停的位置。监听过滤 Holder 和 IsMelted；独有行为留在内容文件，复用模板才放共享规则；学习路径见[共享触发教程](../tutorials/shared-rule.md)。

拾取资源放在RelicObtained订阅中，使显式开发获得、角色初始化和未来奖励消费者都经过同一生命周期；不要仅写在Obtain动作尾部。ObtainMaxHpRelic供Strawberry／Pear／Mango／LeesWaffle及FakeMango／LoomingFruit／NutritiousOyster复用；PickupResourceRelic供OldCoin／SignetRing／GoldenPearl的金币和百分比治疗复用。两模板通过instances绑定本次有效relic，按库存顺序执行并复核资格，早期移除／熔化会取消尚未执行的拾取效果。移除不撤回已经获得的最大生命或金币；同定义新实例只获得自己的奖励，其他持有者不阻止本玩家开发获得。

出牌计数模板CardCountRelic按真实AfterCardPlayed逐次计数，自动／重复打出分别计算，多段攻击只计一次。perTurn决定回合开始早期重置或跨战保留，实例计数不能从全局出牌数反推。首回合抽牌和营火治疗修正通过ModifyTurnDraw／ModifyRestHeal纯监听贡献；移除／熔化后停止参与。共享参数、完整消费者和边界见[遗物生命周期契约](../../../design/mod-the-spire/relic-combat-start/content-lifecycle.md)。

回合开始资源监听AfterSideTurnStarted：TurnStartEnergyRelic默认只在turn相等时GainEnergy，VeryHotCocoa用throughTurn=true保留<=turn条件；EveryTurnsEnergyRelic按实例Counter模turns，HappyFlower／FakeHappyFlower分别每3／5次奖励且跨战保留。NoEnergyGain抑制奖励但不停止计数。Sai配置TurnStartBlockRelic，每回合GainUnpoweredBlock，不受敏捷／脆弱影响。Sai的源时点在能量重置后、起手抽牌前，因此监听AfterSideTurnStarted；AfterBlockCleared位于能量重置前，首回合和保留格挡时也会发布。准确参数见[操作参考](../reference/operations.md)，顺序与边界见[回合资源契约](../../../design/mod-the-spire/relic-combat-start/content-lifecycle.md#回合开始资源dsl-relic-content-06relic-g04)。

替换与熔化尚无正式 DSL 操作。实现时明确：旧获得效果如何撤回，新获得效果何时生效，是否保留同一实例／计数，熔化时保留哪些库存事实与触发，跨战如何重建。不能仅写 DefinitionId 或 IsMelted 就宣称完整生命周期。

伤害与能量参与者使用CombatModifierRelic订阅ModifyAttackAdditive和ShouldResetEnergy，主流程通过foldEvent征询监听者。DamageEnemyPowered显式传owner和card；Strike加成在虚弱／易伤前参与。ResetTurnEnergy依ShouldResetEnergy结果重置或补最大能量，再发AfterEnergyReset。NoAttackTurnEnergyRelic监听出牌、回合末和能量重置，状态归ArtOfWar.State，获得时创建、移除时清理。完整顺序及签名见[统一监听契约](../../../design/mod-the-spire/effect-listeners.md)。

升级牌加伤使用UpgradedAttackDamageRelic，MiniatureCannon配置amount=3；判断实际Card.Upgraded，并保留源“攻击者或牌拥有者满足其一”的资格，不限Strike。PaperPhrog／PaperKrane使用StatusMultiplierRelic，在对应Power确认正层状态后，分别经ModifyVulnerableMultiplier／ModifyEnemyWeakMultiplier调整百分数点，再计算伤害。不要把1.75／0.60作为总伤害固定倍率，也不要在Power中按遗物ID查询。移除／熔化由实例订阅即时失效；计算仍在最终扣格挡前取整一次。参数见[操作参考](../reference/operations.md)，完整边界见[无状态攻击修正契约](../../../design/mod-the-spire/relic-combat-start/content-lifecycle.md#无状态攻击修正dsl-relic-content-07relic-g05)。

效果 X 参与者复用 `XValueRelic` 模板，ChemicalX配置amount=2；通过ModifyXValue pure监听返回新值，不改Card.PaidX或Player.Energy。卡牌效果调用ResolveEnergyXValue，支付仍用GetCardEnergyCost。移除／熔化立即失效，重复效果重新查询监听；星星 X 尚未接入。完整边界见[效果X契约](../../../design/mod-the-spire/relic-combat-start/content-lifecycle.md#效果-x-修正dsl-relic-content-03)。

主动／回合／战斗／受伤／死亡／抽牌等遗物分别消费相应共享机制。涉及长期最大生命、金币或永久牌组时，调用 Run 的权威操作，避免战斗副本与永久状态重复获利。验证一次获得／一次移除、实际触发、不持有／熔化负例、计数边界、两实例／持有者隔离和跨战重建。

同类遗物先沿[分组计划](../../../design/mod-the-spire/relic-combat-start/development-plan.md)核对触发、状态寿命、随机与完整消费者，再认领主清单中一个完整子包。Whetstone／WarPaint／SandCastle复用PickupRandomUpgradeRelic的definition、kind、amount配置，通过RelicObtained实例订阅调用UpgradeRandomPermanentCards。不要把候选临时移出RunDeck，也不要为只取两张而仅消费两次随机；完整源顺序及隔离要求见[随机永久升级契约](../../../design/mod-the-spire/relic-combat-start/content-lifecycle.md#拾取随机永久升级dsl-relic-content-04relic-g01)。选牌升级和Egg的创建修正分别沿其他机制包接续。

源池保持完整资格和顺序。当前覆盖检查只承认真实 DSL 行为报告，不接受旧 C# 覆盖数量或将一个模板测试标成所有遗物均通过。[行为要求](../../../design/environment-completion-and-differential-v1.md#env-t09)继续有效。

## 恢复药水容量、获得和使用

1. 在 potions/schema 定义库存实例、槽位／容量、Owner、用途及消费状态；区分静态容量和由遗物／长期状态决定的动态容量。
2. 生成候选时保留完整源池、资格、权重和随机顺序。获得时明确满栏保留／拒绝、替换选择、奖励容器状态与未选实例清理，不能先删源奖励再让获得失败。
3. 使用共享动作验证目标、资格、支付／消费与效果，主动使用和自动触发分别声明。FairyInABottle 一类死亡防止不应伪装成普通手动药水。
4. 将死亡防止、资源变化、生成牌、选牌和战斗终局交给相应共享操作。使用结束或取消后按源处理实例，不得重复消费。

用动态容量参与者、满栏后领取、无合法目标、自动触发和跨房保留做区分检查；与[奖励／Run 流程](run-and-rooms.md)联合验证。旧 PotionBelt 固定容量断言冲突保存在迁移记录，不能放宽期望来掩盖新模型的差异。

每批先完成一种可复用生命周期，再接实际内容并更新[操作参考](../reference/operations.md)，无需一次重写整个物品框架。
