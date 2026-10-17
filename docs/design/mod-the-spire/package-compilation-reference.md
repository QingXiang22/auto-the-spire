# 公共组包编译模块

文档类型：参考（Reference）。适用于修改base的共享规则接入；内容作者使用已有原语时仍从[base开发指南](base-development-guide.md)进入。本页列出实现入口和依赖传递方法，不维护任务状态。

## 公开入口与编译期参数

[CombatSlicePackage.Build](../../../mod-the-spire/base-spire/BaseSpire/Slice/CombatSlicePackage.cs)保留原有命名参数及默认值，将它们装入内部`PackageBuildOptions`，调用[CombatPackageComposer.Build](../../../mod-the-spire/base-spire/BaseSpire/Slice/Compilation/CombatPackageComposer.cs)。正式游戏仍由`BaseSpireRuntimeFactory`配置并调用这条路径。

`PackageBuildOptions`包含内容模型、场景开关、源遭遇池、遗物配置及Run/地图选项。它仅用于构建规则，不是可变游戏状态。[CompilationBindings](../../../mod-the-spire/base-spire/BaseSpire/Slice/Compilation/CompilationBindings.cs)定义每个声明阶段返回的符号分组；可选Run/地图/敌人机器符号保留可空类型，不在受控场景中虚构声明。

## 按职责定位

以下模块均在`BaseSpire/Slice/Compilation/`。每个方法的参数表列出它需要的符号分组和回调；它们不查询全局服务。

| 要修改的规则 | 模块与入口 | 主要结果或消费关系 |
| --- | --- | --- |
| 源内容选择 | [ContentCompilation](../../../mod-the-spire/base-spire/BaseSpire/Slice/Compilation/ContentCompilation.cs)：`Content`、`CardOf`、`EnemyOf` | 创建包及作者模型；保留受控与正式内容路径 |
| 玩家、敌人、卡牌、Power、牌区、事件、投影字段 | [CombatSchema](../../../mod-the-spire/base-spire/BaseSpire/Slice/Compilation/CombatSchema.cs)：`Player`、`Enemies`、`Cards`、`Power`、`Piles`等 | 返回各自的Bindings；DSL卡牌变量字段由`RewardCompilation.Rewards`在地图服务之后、奖励声明之前补入 |
| Run字段、遗物、长期集合 | [RunSchema](../../../mod-the-spire/base-spire/BaseSpire/Slice/Compilation/RunSchema.cs)：`RunState`、`Relics`、`RunCollections` | Run资源、遗物库存和长期牌组分开返回 |
| 伤害阶段、受伤、失去HP、格挡和相关触发 | [DamageCompilation](../../../mod-the-spire/base-spire/BaseSpire/Slice/Compilation/DamageCompilation.cs)：`MonsterDamage`/`MonsterDamageBody`、`PlayerDamage`/`PlayerDamageBody`、`DamageChain`、`BlockGainSteps` | 声明程序后绑定Body；保留加性、乘性、截断及触发顺序 |
| Power查询、施加、事件和机制绑定 | [PowerCompilation](../../../mod-the-spire/base-spire/BaseSpire/Slice/Compilation/PowerCompilation.cs)：`PowerQuery`、`PowerCommands`、`PowerRules`、`TriggerChain` | 复用`Orchestration/PowerRuleRuntime`，实际计数在Engine中 |
| 敌人行动程序与状态机选取 | [EnemyCompilation](../../../mod-the-spire/base-spire/BaseSpire/Slice/Compilation/EnemyCompilation.cs)：`EnemySelection`/`EnemySelectionBody`、`SelectMachine`、`MoveInstructions` | 加权计算输入先声明，随后生成选择IR；不改变MonsterAi抽取顺序 |
| 玩家/敌人回合及手牌回合末 | [TurnCompilation](../../../mod-the-spire/base-spire/BaseSpire/Slice/Compilation/TurnCompilation.cs)：`EnemyTurn`/`EnemyTurnBody`、`PlayerTurn`、`PlayerTurnEnd`/`PlayerTurnEndBody` | 消费共同伤害、抽牌和Power程序 |
| 卡牌命令与效果能力绑定 | [CardCompilation](../../../mod-the-spire/base-spire/BaseSpire/Slice/Compilation/CardCompilation.cs)：`Draw`、`EffectState`、`CompileEffects`、`CardPiles`、`CardPlay` | 继续调用Effects和Orchestration；不增加另一份出牌链 |
| 战斗实体初始化 | [CombatantCompilation](../../../mod-the-spire/base-spire/BaseSpire/Slice/Compilation/CombatantCompilation.cs)：`MonsterFields`、`MachineFields`、`SpawnStartingPowers` | 生成出生字段及起始Power指令 |
| 遭遇池与普通/精英队列 | [EncounterCompilation](../../../mod-the-spire/base-spire/BaseSpire/Slice/Compilation/EncounterCompilation.cs)：`EncounterPool`、`RunEncounters`、`EnqueueProcedure`、`EncounterQueues` | 池、队列和消费计数分别绑定；保留真实源池与袋子重抽语义 |
| 换房和战斗重建 | [BattleRoomCompilation](../../../mod-the-spire/base-spire/BaseSpire/Slice/Compilation/BattleRoomCompilation.cs)：`RoomTransition` | 生成StartNextRoom：清旧战斗、消费遭遇、掷出生HP、复制长期牌组及开回合 |
| 真正新Run开局 | [NewRunCompilation](../../../mod-the-spire/base-spire/BaseSpire/Slice/Compilation/NewRunCompilation.cs)：`Setup`、`NewRun` | 将初始化库存、队列和首房接入OnSessionStart |
| 地图与房间叶接入 | [RoomCompilation](../../../mod-the-spire/base-spire/BaseSpire/Slice/Compilation/RoomCompilation.cs)：`RoomServices`、`Rooms` | 创建RoomCompileContext及房间服务，再登记进入和公开操作 |
| 奖励声明与Boss奖励关闭 | [RewardCompilation](../../../mod-the-spire/base-spire/BaseSpire/Slice/Compilation/RewardCompilation.cs)：`Rewards` | 在SettleBattle生成之前提供RewardWiring；Boss关闭回调只标幕完成 |
| 战斗胜负和长期提交 | [SettlementCompilation](../../../mod-the-spire/base-spire/BaseSpire/Slice/Compilation/SettlementCompilation.cs)：`Settlement`、`SettlementBinding`、`BattleEndTail` | 嵌套出牌返回后，外层动作才结算和提交 |
| 药水、结束回合、兼容换房/升级公开动作 | [ActionCompilation](../../../mod-the-spire/base-spire/BaseSpire/Slice/Compilation/ActionCompilation.cs)：`PotionAction`、`EndTurnAction`、`RunActions` | 消费已声明的规则命令，保留Role、候选与守卫 |

## 拥有者回合末按层数施加 Power

正式声明 `on = [trigger(event=afterOwnerTurnEnd, effect=grantPower(Strength, sourceStacks))]` 复用 [SpirePowerSource](../../../mod-the-spire/base-spire/BaseSpire/Slice/SpirePowerSource.cs) 的 grantPower 读取器，降级为 `AuthorPower.OwnerTurnEnd = gain-power-per-stack:1:base-spire:strength`，`TurnEndSide` 留空。目标必须可在规范 Power ID 表解析；常量数量、其他目标和未支持效果明确拒绝，不降级为空行为。

[TurnCompilation](../../../mod-the-spire/base-spire/BaseSpire/Slice/Compilation/TurnCompilation.cs) 在 `ResolveOwnerTurnEnd` 的 OwnSide 门控内读取当前 Power.Stacks，调用已有 `ApplyPower(Owner, PowerId, Stacks, false, false)`。玩家侧回合末只处理玩家拥有者，敌方侧回合末处理敌方拥有者；玩家在敌方末尾的非 OwnSide 调用不得重复触发。怪物的施加发生在本侧行动结束后，因此新力量影响后续行动，而非刚完成的攻击。源 Power 不跳过首次触发、不递减、不移除；这不同于 Ritual 的 convert-to-strength 首次跳过语义。共享敌人选择程序把所选行动同时写回 Engine 的 MachineState 和公开 NextMove，以便下一次从实际当前状态求后继；只更新意图会导致固定循环重复初始状态的后继。

HP、Power 实例与层数、动作状态和随机游标全部属于 Engine 状态；本能力不新增可变 C# 状态、不抽随机。固定 Byrdonis SWOOP→PECK→SWOOP 转移不抽 MonsterAi；正式出生仍使用原有 HP 流在普通81..84范围抽取。tough90及deadly攻击声明不表示 Run 已支持 Ascension>0。源依据：[TerritorialPower](../../../sts2/src/Core/Models/Powers/TerritorialPower.cs) 的 participants.Contains(Owner) 及当前 Amount；[Byrdonis](../../../sts2/src/Core/Models/Monsters/Byrdonis.cs) 的出生与动作表。

可区分的错误行为由 [Byrdonis机制检查](../../../mod-the-spire/base-spire/BaseSpire.Slice.Tests/SliceByrdonisChecks.cs) 验证：应用即加力量、首次回合末跳过、递减层数、固定使用出生层数、敌方末尾重复触发玩家，以及 PECK 力量只加到总伤害一次。预设状态的测试场景不是正常公开 Run 或独立参考对拍证据。

## 声明与接线顺序

`CombatPackageComposer`集中维护顺序：内容与Schema → Run/地图/奖励声明 → 结算与共享规则 → 回合/敌人选择 → 换房与新Run → 公开动作和房间注册。代码按原来的声明和生成顺序展开，并未批量前移所有程序声明。

`XxxBindings`返回程序句柄后，`XxxBody`才能使用该句柄和其他已声明依赖。根方法末尾的短适配函数把具体分组传给领域方法；[CompilationCallbacks](../../../mod-the-spire/base-spire/BaseSpire/Slice/Compilation/CompilationCallbacks.cs)保留所需参数名、可选参数和返回类型。回调可以保存供后续IR编译使用，不能在相应声明阶段之前提前调用。

`CompilationSlots`只在编译过程中分配IR局部名称。格挡命名在玩家与敌人编译之间共享；编队命名由换房编译独立拥有，敌人状态机命名由选择编译拥有。它不持随机游标、玩家资源、Session或运行中的遭遇队列。

## 符号分组与状态寿命

`PlayerBindings`由`CombatSchema.Player`返回，`RunStateBindings`由`RunSchema.RunState`返回。记录成员是Engine字段/集合/程序的编译期符号，不保存对应状态值；分组中新增成员不改变已有方法的参数类型。新依赖组则体现在消费方法的参数及根组装调用中。

`CombatLifecycle.BeginTurn`负责已有回合事实的重置，`CombatLifecycle.EndBattle`由换房流程调用，清理已有战斗计数和临时牌。新增字段的声明与Bindings成员本身不会自动增加重置指令。玩家初值在`NewRunCompilation.NewRun`，实际换房重建在`BattleRoomCompilation.RoomTransition`；永久状态提交由`SettlementCompilation`拥有。

实际修改步骤见[新增共享状态字段](base-development-guide.md#新增共享状态字段)，新房型接入见[房间操作指南](how-to-add-a-room.md)。

构建顺序、IR局部量名称及随机调用顺序变化可能影响规则身份和轨迹。此类修改按根验证门槛检查真实内容加载及行为；接口没变的纯拆分，应保留同配置的规则身份和公开轨迹。
