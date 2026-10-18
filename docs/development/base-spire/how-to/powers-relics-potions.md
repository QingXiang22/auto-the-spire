# 开发 Power、遗物与药水

文档类型：操作指南（How-to）。先从对应 catalog 查源身份，再区分“已有模板配置”和“缺共享生命周期”。纯 DSL 已有遗物持有实例、拾取资源、战斗／回合触发、出牌计数、首次受损抽牌、首回合抽牌及营火治疗修正，也可复用已迁移的伤害、Power与药水操作。准确接口见[操作参考](../reference/operations.md)，实际内容与验证范围见[主清单](../../../review/tofix/README.md#当前交付)；不能将源目录或旧C#覆盖数量视为当前DSL支持。

## 实现 Power 生命周期

1. 在combat/schema.spire的Power实体中明确持有者、施加来源、层数、持续时间、累计／替换规则、零层和负值含义，以及战斗／回合寿命。名称相同不代表同一实例，必须约定同类型多来源的聚合方式。
2. 在powers/reactions.spire与powers/modifiers.spire实现施加、修改、衰减、移除和事件订阅；内容配置声明参数及参与的触发／修正模板。首次施加是否跳过当回合衰减按源判断，不能统一猜成所有负面状态都一样。
3. 用 combat 的统一修正阶段处理力量、敏捷、易伤等数值参与者，顺序和精度取自源契约；不要把 Power 数值缓存成另一个 C# 事实。
4. 验证施加与初始持有差异、叠加／降层／到零、无限持续、两持有者隔离、移除后停止订阅，以及暂停续接不重复施加。

需要源入口时按 `powers/catalog.json` 的 source_path 定位；旧源 `Powers` 目录仅辅助查迁移意图。不要将已记录的 260 个源身份当作 260 种已实现行为。

## 扩展遗物实例与触发

复用当前 [ObtainRelic／RemoveRelic](../reference/operations.md) 时，遵守入库后发获得事件、出库后发移除事件、随后销毁的顺序。获得过程本身不去重，模板 action 的守卫才限制当前开发范围的重复 definition。需要正常库存资格、唯一性或奖励获得时先补对应契约。

新触发先列 owner／source／target、调用时点、是否需要有效实例、每个实例的计数与清理、允许取消／暂停的位置。共享规则过滤 Holder 和 IsMelted，内容文件只配置模板；学习路径见[共享触发教程](../tutorials/shared-rule.md)。

拾取资源放在RelicObtained订阅中，使显式开发获得、角色初始化和未来奖励消费者都经过同一生命周期；不要仅写在Obtain动作尾部。ObtainMaxHpRelic供Strawberry／Pear／Mango及LeesWaffle复用；PickupResourceRelic供金币和百分比治疗复用。移除不撤回已经获得的最大生命或金币。

出牌计数模板CardCountRelic按真实AfterCardPlayed逐次计数，自动／重复打出分别计算，多段攻击只计一次。perTurn决定回合开始早期重置或跨战保留，实例计数不能从全局出牌数反推。首回合抽牌和营火治疗修正通过ModifyTurnDraw／ModifyRestHeal纯监听贡献；移除／熔化后停止参与。共享参数、完整消费者和边界见[遗物生命周期契约](../../../design/mod-the-spire/relic-combat-start/content-lifecycle.md)。

替换与熔化尚无正式 DSL 操作。实现时明确：旧获得效果如何撤回，新获得效果何时生效，是否保留同一实例／计数，熔化时保留哪些库存事实与触发，跨战如何重建。不能仅写 DefinitionId 或 IsMelted 就宣称完整生命周期。

伤害与能量参与者使用CombatModifierRelic订阅ModifyAttackAdditive和ShouldResetEnergy，主流程通过foldEvent征询监听者。DamageEnemyPowered显式传owner和card；Strike加成在虚弱／易伤前参与。ResetTurnEnergy依ShouldResetEnergy结果重置或补最大能量，再发AfterEnergyReset。NoAttackTurnEnergyRelic监听出牌、回合末和能量重置，状态归ArtOfWar.State，获得时创建、移除时清理。完整顺序及签名见[统一监听契约](../../../design/mod-the-spire/effect-listeners.md)。

主动／回合／战斗／受伤／死亡／抽牌等遗物分别消费相应共享机制。涉及长期最大生命、金币或永久牌组时，调用 Run 的权威操作，避免战斗副本与永久状态重复获利。验证一次获得／一次移除、实际触发、不持有／熔化负例、计数边界、两实例／持有者隔离和跨战重建。

源池保持完整资格和顺序。当前覆盖检查只承认真实 DSL 行为报告，不接受旧 C# 覆盖数量或将一个模板测试标成所有遗物均通过。[遗物原合同](../../../review/tofix/README.md#env-t09)继续有效。

## 恢复药水容量、获得和使用

1. 在 potions/schema 定义库存实例、槽位／容量、Owner、用途及消费状态；区分静态容量和由遗物／长期状态决定的动态容量。
2. 生成候选时保留完整源池、资格、权重和随机顺序。获得时明确满栏保留／拒绝、替换选择、奖励容器状态与未选实例清理，不能先删源奖励再让获得失败。
3. 使用共享动作验证目标、资格、支付／消费与效果，主动使用和自动触发分别声明。FairyInABottle 一类死亡防止不应伪装成普通手动药水。
4. 将死亡防止、资源变化、生成牌、选牌和战斗终局交给相应共享操作。使用结束或取消后按源处理实例，不得重复消费。

用动态容量参与者、满栏后领取、无合法目标、自动触发和跨房保留做区分检查；与[奖励／Run 流程](run-and-rooms.md)联合验证。旧 PotionBelt 固定容量断言冲突保存在迁移记录，不能放宽期望来掩盖新模型的差异。

每批先完成一种可复用生命周期，再接实际内容并更新[操作参考](../reference/operations.md)，无需一次重写整个物品框架。
