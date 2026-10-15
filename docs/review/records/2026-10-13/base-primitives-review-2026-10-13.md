# base-spire原语完整性审阅（2026-10-13）

本次按用户要求核查“现有原语能否表达后续原版内容”，并把确认的缺口登记为任务。结论：**基础组合能力已经具备，完整原版单人环境所需的原语与参与者尚不完整。** 缺口包括真正缺失的领域操作、已有操作缺少来源／时序／持久化语义，以及只有hook接缝而没有真实参与者的能力。

当前任务仅在[主清单](../../tofix/README.md)维护：战斗及角色公共机制归ENV-T06-PR01～17，整局机制归ENV-T09-PR01～05，共22个机制子任务；ENV-T13-PR-AUDIT承担覆盖收口。这里保存日期证据，不复制认领和排期。没有新派单或生产改动；敌人召唤／复活继续遵守既有暂缓，独立的行动控制不因此一并暂停。

## 方法与覆盖边界

读取现有卡牌、角色战斗、整局资源、事件、药水契约，对照base前端、类型化效果、编译接线和领域操作，再核对原版代表消费者。

[inventory.py](../../../../.audit/base-primitives-review-20261013/inventory.py)扫描本地`sts2/src/Core/Models`的Cards、Powers、Relics、Potions、Events、Monsters、Ancients、Enchantments、Afflictions九个目录的顶层C#文件。保存1419个源文件、123个base生产C#文件的SHA-256及91个命令调用名称的逐行导航，见[inventory.json](../../../../.audit/base-primitives-review-20261013/inventory.json)。这是检索索引：包含表现调用和未必属于单人可达范围的内容，不证明91种语义已经逐一验收，也不能直接作为原语数量。

人工核对覆盖下表的实际源方法及base入口。直接属性赋值、继承／hook覆写、动态资格、嵌套源码目录和全部角色内容的完整可达性没有穷尽；相应收口已登记ENV-T13-PR-AUDIT。保留577项源卡完整分母，不把22个已确认机制包称为全部原版的最终封闭清单。

本次复用上一轮[架构审阅](base-architecture-review-2026-10-13.md)中的来源／组装问题；新增任务针对行为能力，不把移动文件或改名当成原语交付。

## 已具备的基础不重复立项

- 类型化卡牌效果、顺序／条件／循环、数值组合与let快照；基础伤害／格挡／能量／抽牌／生命／施加Power。
- 普通选牌、随机选牌、遍历、升级、复制、移动／置顶、战斗内定向变换、自动出牌、基本实例变量。特定输入与hook上下文仍有限制。
- 基础固定费、能量X、星星固定费／X、静态Retain、部分出牌／消耗／弃牌hook及回合历史。
- 星星资源、Forge生成／成长、五种球与槽位／Focus、Poison、Doom和Osty基础语义。它们的完整参与者与正式角色流程仍需补全。
- Run金币／HP基础操作、获得／移除永久牌、四种遗物效果、药水任意来源领取、普通事件Continue／Finish和共享守卫。

这些是源码及既有交付范围的复核，不是本轮新执行的加载或规则测试结论。仅缺内容声明、升级数值或真实对拍证据的内容，应消费已有操作；不为每张牌新增同义原语。

## 战斗与卡牌缺口的来源定位

| 对应子任务 | 原版行为与base差距 |
| --- | --- |
| ENV-T06-PR01 | [ViciousPower](../../../../sts2/src/Core/Models/Powers/ViciousPower.cs)检查applier及实际amount；[ArtifactPower](../../../../sts2/src/Core/Models/Powers/ArtifactPower.cs)修改收到的减益并消耗自身；[AutomationPower](../../../../sts2/src/Core/Models/Powers/AutomationPower.cs)有独立实例计数。[PowerCompilation](../../../../mod-the-spire/base-spire/BaseSpire/Slice/Compilation/PowerCompilation.cs)的ApplyPower没有来源参数，按Owner＋ID找单实例；[PowerRuleRuntime](../../../../mod-the-spire/base-spire/BaseSpire/Slice/Orchestration/PowerRuleRuntime.cs)只实现特定规则／变量，无法据此称通用Power生命周期完整。 |
| ENV-T06-PR02 | [Expose](../../../../sts2/src/Core/Models/Cards/Expose.cs)先LoseBlock再移除Artifact；[Feed](../../../../sts2/src/Core/Models/Cards/Feed.cs)读取真实DamageResult及Fatal资格；[BrightestFlame](../../../../sts2/src/Core/Models/Cards/BrightestFlame.cs)减少最大HP；[FairyInABottle](../../../../sts2/src/Core/Models/Potions/FairyInABottle.cs)在ShouldDie/AfterPreventingDeath自动治疗。[CardEffectProgram](../../../../mod-the-spire/base-spire/BaseSpire/Slice/Effects/CardEffectProgram.cs)与[CardEffectCompiler](../../../../mod-the-spire/base-spire/BaseSpire/Slice/Effects/CardEffectCompiler.cs)只有现有基础操作／目标，当前致死组合以TargetAlive分支表达，不能替代完整伤害结果与死亡参与者。 |
| ENV-T06-PR03 | [WellLaidPlansPower](../../../../sts2/src/Core/Models/Powers/WellLaidPlansPower.cs)在BeforeFlushLate选择0～当前层数张未保留牌并给予单回合Retain。[CardSelectionProcedures](../../../../mod-the-spire/base-spire/BaseSpire/Slice/Orchestration/CardSelectionProcedures.cs)已有普通决策，但Count为固定整数、过滤种类有限；当前卡牌hook限制选择、回合末保留依据静态定义。需要专门交付源允许的生命周期决策与临时状态，不能把普通选牌判成未实现。 |
| ENV-T06-PR04 | [HandTrick](../../../../sts2/src/Core/Models/Cards/HandTrick.cs)对选中Skill赋予单回合Sly；[RingingPower](../../../../sts2/src/Core/Models/Powers/RingingPower.cs)施加Affliction、对新进入牌继续施加，移除时清理。[CardSchema](../../../../mod-the-spire/base-spire/BaseSpire/Slice/Orchestration/CardSchema.cs)及类型化效果未形成完整附魔／Affliction身份和寿命模型；定义关键字与卡变量不能自动提供这些语义。 |
| ENV-T06-PR05 | [Discovery](../../../../sts2/src/Core/Models/Cards/Discovery.cs)从合格池抽3个不重复候选，允许跳过并只获得选中项。[SourceEffectPrograms](../../../../mod-the-spire/base-spire/BaseSpire/Slice/Effects/SourceEffectPrograms.cs)限制distinct为字面量1；[CardGenerationRules](../../../../mod-the-spire/base-spire/BaseSpire/Slice/Orchestration/CardGenerationRules.cs)逐项生成直接入手／弃牌，角色池只接受Ironclad。缺候选容器和选择语义，也存在其他角色内容池依赖。 |
| ENV-T06-PR06 | [ChemicalX](../../../../sts2/src/Core/Models/Relics/ChemicalX.cs)实现ModifyXValue，原版CardModel/CardCmd另有费用、StarX和重放捕获规则。[CardPaymentRules](../../../../mod-the-spire/base-spire/BaseSpire/Slice/Orchestration/CardPaymentRules.cs)支持基础支付和特定Power减费，[CardPlayPipeline](../../../../mod-the-spire/base-spire/BaseSpire/Slice/Orchestration/CardPlayPipeline.cs)复用出牌链，但不等于全部源修正／重放／限制参与者。补全时复用现有PaidX/PaidStarX和自动出牌，不重做支付。 |
| ENV-T06-PR07 | [SoulStorm](../../../../sts2/src/Core/Models/Cards/SoulStorm.cs)查询当前消耗堆里的Soul数量，**不是灵魂历史次数**；[DeathMarch](../../../../sts2/src/Core/Models/Cards/DeathMarch.cs)过滤本回合非起手抽牌，[Bolas](../../../../sts2/src/Core/Models/Cards/Bolas.cs)查询上一回合该实例的完成出牌。[CardEffectExpressions](../../../../mod-the-spire/base-spire/BaseSpire/Slice/Effects/CardEffectExpressions.cs)与[CardPlayHistory](../../../../mod-the-spire/base-spire/BaseSpire/Slice/Orchestration/CardPlayHistory.cs)提供有限状态／类型计数、回合清理，缺上述查询维度与寿命。 |
| ENV-T06-PR08 | [GeneticAlgorithm](../../../../sts2/src/Core/Models/Cards/GeneticAlgorithm.cs)显式同时更新战斗实例和DeckVersion。[CardDefinitions](../../../../mod-the-spire/base-spire/BaseSpire/Slice/Orchestration/CardDefinitions.cs)已保存LongTermSource，但[CardCompilation](../../../../mod-the-spire/base-spire/BaseSpire/Slice/Compilation/CardCompilation.cs)的ModifyCardVariable只更新cardRef；通用永久成长／降级／附着迁移尚未闭合。链接存在不等于写回已实现。 |
| ENV-T06-PR09～14 | [角色战斗契约](../../../design/mod-the-spire/character-combat/spec.md)与[Domain/Combat实现](../../../../mod-the-spire/base-spire/BaseSpire/Slice/Domain/Combat)逐项对照：星星默认hook接缝；Forge缺CreatedThroughForge／IsDupe及参与者；Orb缺末球／指定被动及完整hook；Osty缺宠物Power、完整伤害与召唤参与者；Poison缺完整伤害／防死；Doom缺完整死亡／时点参与者。额外读取[TeslaCoil](../../../../sts2/src/Core/Models/Cards/TeslaCoil.cs)确认指定闪电球与目标，不将基础五球重新列为缺失。 |
| ENV-T06-PR15／16 | [CreatureCmd](../../../../sts2/src/Core/Commands/CreatureCmd.cs)分别提供Add、Kill、Escape、Stun；[ReattachPower](../../../../sts2/src/Core/Models/Powers/ReattachPower.cs)涉及保留死亡实体、行动变化及不可击中资格。现有出生和固定／加权状态机不等于动态召唤／复活／行动控制。[SpireEnemySource](../../../../mod-the-spire/base-spire/BaseSpire/Slice/SpireEnemySource.cs)和[EnemyCompilation](../../../../mod-the-spire/base-spire/BaseSpire/Slice/Compilation/EnemyCompilation.cs)仍需对应领域能力。召唤／复活暂缓与独立Stun／逃离分开登记。 |
| ENV-T06-PR17 | [Reboot](../../../../sts2/src/Core/Models/Cards/Reboot.cs)先把手牌移入抽牌堆，再主动Shuffle并抽牌；[ForegoneConclusionPower](../../../../sts2/src/Core/Models/Powers/ForegoneConclusionPower.cs)先ShuffleIfNecessary再选择入手。[CardPileProcedures](../../../../mod-the-spire/base-spire/BaseSpire/Slice/Orchestration/CardPileProcedures.cs)已有移动／置顶等操作，抽牌模块有自动重洗；类型化效果没有主动Shuffle节点。应暴露已有机制的准确组合接口，不能用补抽或强制抽空伪造。 |

## 整局缺口的来源定位

| 对应子任务 | 原版行为与base差距 |
| --- | --- |
| ENV-T09-PR01 | [CardCmd](../../../../sts2/src/Core/Commands/CardCmd.cs)的Transform、[CardSelectCmd](../../../../sts2/src/Core/Commands/CardSelectCmd.cs)的永久牌组选择／附魔路径具有独立生命周期；[RunDeck](../../../../mod-the-spire/base-spire/BaseSpire/Slice/Domain/Run/RunDeck.cs)只有Create／Acquire／Remove和两个默认hook。战斗牌区变换不能直接当永久变换。 |
| ENV-T09-PR02 | [SwordOfStone](../../../../sts2/src/Core/Models/Relics/SwordOfStone.cs)按精英胜利累计，达到阈值调用RelicCmd.Replace；[RelicCmd](../../../../sts2/src/Core/Commands/RelicCmd.cs)另有Remove／Melt。[SliceRelics](../../../../mod-the-spire/base-spire/BaseSpire/Slice/SliceRelics.cs)仅两种触发、三种效果和四件声明，[RunRelics](../../../../mod-the-spire/base-spire/BaseSpire/Slice/Domain/Run/RunRelics.cs)只实现Obtain；SunkenStatue的GrabSword已有明确失败，是实际流程消费者。 |
| ENV-T09-PR03 | [PotionBelt](../../../../sts2/src/Core/Models/Relics/PotionBelt.cs)增加容量，FairyInABottle自动使用；[PotionCmd](../../../../sts2/src/Core/Commands/PotionCmd.cs)支持指定槽位。[PotionInventoryRules](../../../../mod-the-spire/base-spire/BaseSpire/Slice/PotionInventoryRules.cs)容量／SlotIndex边界及空槽循环固定为PotionSlotCount，生成绑定单个RewardPotions容器。任意来源Acquire已经存在，不能因此判定生成、动态容量、自动／脱战使用都已支持。 |
| ENV-T09-PR04 | [HandOfGreed](../../../../sts2/src/Core/Models/Cards/HandOfGreed.cs)战斗致死时获得金币；[PlayerCmd](../../../../sts2/src/Core/Commands/PlayerCmd.cs)区分金币损失类型。[RunResources](../../../../mod-the-spire/base-spire/BaseSpire/Slice/Domain/Run/RunResources.cs)有基础命令与默认hook，但缺完整来源历史／修正；卡效果未接Run获金，[SettlementCompilation](../../../../mod-the-spire/base-spire/BaseSpire/Slice/Compilation/SettlementCompilation.cs)仅提交HP／MaxHP。需要裁定唯一权威写入入口，而非机械增加双向拷贝。 |
| ENV-T09-PR05 | [BattlewornDummy](../../../../sts2/src/Core/Models/Events/BattlewornDummy.cs)通过EnterCombatWithoutExitingEvent与Resume返回事件并提供奖励；[EventVisitLifecycle](../../../../mod-the-spire/base-spire/BaseSpire/Slice/Rooms/Events/EventVisitLifecycle.cs)说明返回尚未接线，Eligibility对未实现模块默认true。完整源身份池虽保留，原版有IsAllowed覆写的未实现事件仍不能用true表示已对齐；资格应独立核对，不能用删池绕过。 |

地图生成和第一幕队列并非从零缺失；跨幕生命周期已归ENV-T10。正式五角色开局、池资格和完整内容仍归ENV-T12／13，不能靠完成某个基础原语就开放完整环境身份。

## 交付与验证要求

每个机制包先明确源代表例、Engine状态归属、输入／前置条件、随机与hook顺序、公开结果和未支持范围。前端接受、领域链接、内容注册、真实执行、独立双侧、正常公开流程分别结算。已有组合能表达时只补内容与验证，不照原版每个方法机械新增一个DSL节点。

本次未运行构建、Engine／Slice、运行时内容加载、严格对拍或公开游戏；没有把静态检索当作缺陷复现。调用清单与逐项源码定位用于支持任务范围，后续实现仍须给出真实行为证据。纯文档检查结果由本轮汇报提供。
