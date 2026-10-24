# 开发卡牌与共享战斗机制

文档类型：操作指南（How-to）。适用于卡牌、附着、战斗资源、格挡／伤害／死亡和回合规则。纯 DSL 开发包已有完整单人铁甲战士定义所需的出牌／牌区、显式选敌与手牌选择、X 费、自动出牌、生成、伤害与 Power 修正、永久牌组及战斗副本；准确签名见[铁甲战士共享操作](../../../design/mod-the-spire/ironclad-dsl/shared-operations.md)。原版随机机制与已迁移消费者已恢复；完整随机前置消费、获得修正、完整附着种类、正常初态和完整 Run 仍需恢复。以下按任务选择现有操作或恢复方法；不存在可直接调用的旧 `card` 声明或 C# Effect API。

先读[准确操作](../reference/operations.md)确认边界，再到 cards/catalog 定位具体源类。需求与判断意图参考[卡牌原语约定](../reference/card-behavior.md)及[行为要求](../../../design/environment-completion-and-differential-v1.md#env-t06)，这些旧约定中的接口名不自动成为新 DSL 接口。

有序正文、攻击／结果、独立费用、动态关键词与Sown已有实际入口，查[行为操作参考](../reference/card-behavior.md)。以下“恢复”步骤只用于尚缺的语义，先复用已存在的操作。

## 修改已有卡牌

先按[单个内容修改指南](edit-content.md)定位卡牌文件。普通AttackEffect／AttackAllEnemiesEffect的伤害只在CardDefinition中配置baseDamage／upgradedBaseDamage，效果读取CardBaseDamage；不要恢复damage／upgradedDamage双份配置。卡牌kind仍表示attack／skill／power，与已经移除的Power调用方kind不同。卡牌主要保留定义、参数、顺序与生命周期绑定；选择、移动、变换、生成和结果读取等通用行为放cards/rules供mod组合，即使当前只有一张原版牌使用。先查[公共组合操作](../reference/card-behavior.md#供内容与mod组合的公共操作)，不再把完整效果模板复制到卡牌里。只有依赖本内容语义的私有状态与绑定留在所属文件。以下恢复步骤用于缺共享机制的工作，不要求每改一张牌就重写出牌流程。

## 恢复费用、目标与出牌

1. 在 cards/schema 明确卡定义与卡实例、普通／升级状态、Owner、费用来源、目标域、各牌区和作用域。永久牌组卡与战斗副本使用可追踪映射，不能共用一份可变定义。
2. 在 cards/rules 描述合法动作：实际手牌成员、拥有者、可支付资源、所需目标及禁用状态。公开候选从相同守卫与候选声明产生；AI 或 Host 不另算可打条件。
3. 将支付、快照 X 值、离开手牌／进入打出区、实际效果、history、结果牌区及终局收尾按源顺序实现为完整流程。每张卡绑定唯一正文，在正文中按源顺序调用领域操作；外部通知不再收集主效果。
4. 对无目标、单目标、多目标／多张选择分别定义类型和取消／空选语义。选择暂停时保存显式局部上下文，续接不重复支付和发钩子；目标死亡后的处理按源裁定。

X 牌效果通过pure `ResolveEnergyXValue(owner,card)`读取修正值，不能直接用 `Card.PaidX` 作为段数。PaidX只保存捕获值，ChemicalX等参与者通过ModifyXValue实例监听贡献效果加值；手动支付、自动打出和重复出牌的捕获时机继续复用共享出牌流程。签名和边界见[操作参考](../reference/operations.md)。

验收至少能区分：资源不足拒绝无副作用、X 费在支付前捕获且与效果修正分开、合法候选与真实提交一致、最后一击的剩余效果和出牌收尾仍完成。不能仅让一个减 HP 过程代表完整出牌。

## 定义普通／升级和关键词

将共享字段、变体差异与展开规则写入卡牌约定。新模型需要明确字段继承／整体替换和空块含义，具体写法以[卡牌行为参考](../reference/card-behavior.md)和当前定义为准。升级对实例的费用、变量、附着与已有成长如何保留，分别定义。

状态、诅咒、无色和生成牌按来源组保留标识与资格，不因不进普通池而省略。retain／ethereal／innate／exhaust、自动打出和回合末手牌规则各依赖明确生命周期。普通牌配置优先调用共享模板，特殊牌也不新增 C# 按 ID 分派。

Infection保留源费用-1及Unplayable；局部set／add／免费修改不覆盖负费用。它没有手动出牌或升级候选，自动尝试则经过打出区进入结果牌区，不执行正文或记录真实出牌。新增在手回合末效果时，通过pure的CardHasTurnEndInHandEffect提供资格，再监听CardTurnEndInHandEffect执行效果；FinishTurnHand负责保存名单、先处理其他虚无牌，再按原手牌顺序结算效果牌。Infection调用DamagePlayerFromCard造成3点可格挡、无加成伤害。致死后牌区命令停止移动，致死牌留在打出区，后续效果牌留手；普通清理及敌方阶段停止。准确规则见[费用与回合末约定](../../../design/mod-the-spire/ironclad-dsl/spec.md#不可打出负费用与在手回合末效果)。

验收普通／升级实际差异及关键词时点，覆盖手动与自动路径的适用差别；同一机制不逐牌重跑整套战斗。自动出牌复用 AutoPlayCard，普通抽牌仍经 DrawOne；Hellraiser 只对实际抽到的 Strike 标签牌触发，不能把任意生成入手都当作抽牌。

## 处理牌区、复制和附着

已迁移定义的 `CardDefinition` 会生成 `<名称>.Obtain` 开发动作与同名过程，例如 `Strike.Obtain`；它调用 `ObtainPermanentCard`，把独立实例放入公开的 `RunDeck` 并发 `CardObtained`。当前只允许存活玩家在房间外、战斗外获得；不生成随机奖励，也不代表原版获得修正已恢复。干净初态也可用 `Ironclad.Initialize` 一次建立源起始牌组和遗物，见[角色初态约定](../../../design/mod-the-spire/character-combat/ironclad-start.md)。

一旦建立永久牌组，后续 `BeginDevelopmentCombat` 调用 `BuildCombatDeck`：清理旧非永久牌，按永久顺序复制到 DrawPile，保留普通／升级及费用等已有字段，隐藏的 `PermanentSource` 指向具体来源实例。不会把战斗升级反写到 RunDeck；临时生成、消耗和 Power 牌不会混入下一场。显式初始化 Ironclad 后，开战追加原版流初始洗牌，回合自动抽五张并处理首回合 Innate；其他机制探针仍用公开 `Draw` 显式抽牌。原版洗牌算法与流布局已恢复，整局前置消费仍需接续；见[随机约定](../../../design/mod-the-spire/run-domain/randomness.md)。营火消费者见[房间指南](run-and-rooms.md)，准确边界见[永久牌组与锻造约定](../../../design/mod-the-spire/permanent-deck-smith/spec.md)。

先分别定义移动、获得、生成、复制、变换、升级和移除的完整行为。移动必须维护原集合、目标容量、标识及事件；复制要规定永久 ID、成长、费用修改、附着、临时标记的保留／清除。不能把 `create Card` 当作任意复制操作。

附魔／负面附着与动态关键词放实例层，定义来源、层数、替换和寿命，消费统一选择／升级／变换接缝。验证两张同定义牌不串状态、离场和跨战寿命、复制／升级后的保留及失败不留下悬空引用。Sown已通过明确附魔阶段及复制通知实现，准确入口见[行为操作参考](../reference/card-behavior.md)。其他历史源行为在[行为要求](../../../design/environment-completion-and-differential-v1.md#env-t06)查其原判断点，不恢复旧 C# 装配。

## 恢复资源、伤害与死亡

在 combat/schema 定义权威 HP／最大 HP／Block／Energy 等状态，区分 Run 持久资源与战斗资源。共享获得／支付操作拥有上限、资格、来源和事件；内容不能直接加字段绕过它们。

伤害约定先说明来源、类型、目标、每击计算、加性／乘性修正、格挡吸收、实际失血、Fatal、死亡防止、死亡与胜负判断。多段伤害的每击顺序、批次监听和目标失效不能合并成末端总扣血。当前 GainUnpoweredBlock 只覆盖无加成格挡，不替代普通格挡修正。

主流程通过foldEvent征询伤害／格挡／费用等纯修正，遗物与Power按同一实例订阅方式贡献结果；Before／After通知负责副作用。新增消费者或迁移旧计算时，沿[监听编写指南](write-effect-listeners.md)复用同一链，预览与真实执行不得各写一份修正算法，也不在战斗主过程增加能力ID或Kind分支。

验证至少一个带实际 Power／遗物参与者的修正案例、多击中的死亡、最后目标死亡后余效、不可支付及零值边界。返回实际结果供后续效果消费，避免内容再从 HP 差猜测来源。

## 恢复回合与角色资源消费者

将战斗开始、玩家回合开始、抽牌、行动、回合末手牌处理、敌方行动、持续状态衰减和战斗结束写成显式流程。Power、遗物、敌人与卡牌钩子订阅同一事件，不在多个领域重复发出。

星星／Forge／球／宠物／Poison／Doom 等依所属领域建立共享状态和操作，再由卡牌消费。召唤／复活等仍按主清单暂停条件执行，不能因为新增卡牌而自行解除。用至少两个回合／两场战斗区分一次性初始化与逐次触发，长期资源只在约定边界写回一次。

交付时按[机制迁移](migrate-mechanism.md)更新约定、操作参考、消费者与实际执行结果，再按[验证指南](verify-and-debug.md)检查受影响范围。
