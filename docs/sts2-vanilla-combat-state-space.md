# STS2 原版战斗内状态空间

> 本文根据工作区 `reference/CombatSolver` 的当前实现整理，重点说明该项目为预测、分叉和搜索而捕获的 STS2 单人战斗状态。它描述的是“会影响战斗后续演化的状态”，不是 STS2 全部运行时对象的清单，也不是官方序列化格式。

## 1. 这里的“状态空间”是什么意思

战斗状态空间是：从某个战斗时点出发，按照玩家出牌、使用药水、敌人行动、回合开始/结束效果和随机事件推进后，所有可能到达的战斗局面。每个局面不仅由当前血量决定，还包含会影响未来合法动作、结算结果或随机分支的状态。

`CombatSolver` 在主线程捕获稳定的战斗根，再把可变化的字段放进影子战斗状态并分叉搜索。原版对象仍可作为模型身份、类型和只读基线；分支中的生命、格挡、资源、牌堆、球、RNG、能力和领域计数由模拟状态维护。相关入口见 [`CombatRootSnapshot.cs`](../reference/CombatSolver/src/Runtime/CombatRootSnapshot.cs)、[`CombatPredictionState.cs`](../reference/CombatSolver/src/Engine/InCombat/Simulation/CombatPredictionState.cs)、[`SimPlayerCombatState.cs`](../reference/CombatSolver/src/Engine/InCombat/Simulation/SimPlayerCombatState.cs) 与 [`SimulatedCombatState.cs`](../reference/CombatSolver/src/Search/SimulatedCombatState.cs)。

## 2. 状态空间包含的内容

### 2.1 战斗阶段、回合与玩家资源

- 当前回合、当前行动方，以及玩家战斗阶段（例如玩家回合的具体阶段）。
- 玩家当前生命、最大生命、格挡、能量和星能。
- 玩家金币；特定角色/宠物的当前生命、最大生命、是否可被命中等战斗相关状态。
- 玩家回合编号、跨洗牌次数等会影响本场战斗后续抽牌或效果的计数。

这些值决定可行动作、费用是否可付、受伤/死亡结果和后续回合效果。搜索键的基础字段可见 [`CombatBeamSolver.StateEvaluation.cs`](../reference/CombatSolver/src/Search/CombatBeamSolver.StateEvaluation.cs) 中的 `BuildStateKey`。

### 2.2 生物名册与每个生物的战斗状态

状态空间包括玩家、敌人及战斗中加入的召唤物等生物，并记录足以区分战斗个体的信息：

- 生物/怪物身份、战斗 ID、名册位置及是否仍属于战斗、是否存活或可被命中。
- 当前生命、最大生命和格挡。
- 死亡、移出战斗、复活、召唤、逃跑等改变名册或死亡结算的状态。
- 怪物当前意图/招式、行动状态机位置、行动历史、初始行动随机选择状态和怪物专属私有计数。

敌人的静态身份与战斗数值并不足以确定局面：相同生命值的怪物若下一招、AI 历史或专属计数不同，后续行为仍可能不同。实现分布在 [`SimulatedCombatState.MonsterAi.cs`](../reference/CombatSolver/src/Search/SimulatedCombatState.MonsterAi.cs)、[`SimulatedCombatState.MonsterState.cs`](../reference/CombatSolver/src/Search/SimulatedCombatState.MonsterState.cs) 和死亡生命周期相关分部文件中。

### 2.3 卡牌实例与牌堆

每张牌按实例状态参与计算，而非只按卡牌名称计数。状态可包含：

- 卡牌身份、升级等级、当前能量/星能费用及 X 费用语义。
- 本回合免费、保留、下次打出后耗尽、重放次数等临时标记。
- 附魔、Affliction、动态变量、关键词及会改变效果的卡牌私有累计值。
- 牌所在区域及区域内顺序。

玩家战斗牌堆模型有五个区域：手牌、抽牌堆、弃牌堆、消耗堆和打出堆。顺序会影响抽牌、洗牌、选择与后续动作，因此不是可忽略的显示顺序。影子区定义见 [`SimPlayerCombatState.cs`](../reference/CombatSolver/src/Engine/InCombat/Simulation/SimPlayerCombatState.cs)；卡牌状态文本的编码见 [`ContinuationStamp.cs`](../reference/CombatSolver/src/Runtime/ContinuationStamp.cs) 的 `AppendCard`。

需要区分“影子模型保存了什么”和“搜索去重键直接枚举了什么”：`BuildStateKey` 明确加入手牌、抽牌堆、弃牌堆和消耗堆的有序内容；打出堆及出牌产生的影响则由卡牌生命周期、历史计数和领域状态等路径表示。不可仅凭去重键里四个显式牌堆推断影子战斗没有打出堆。

### 2.4 Power、状态效果与生命周期

所有生物身上的 Power 都是战斗状态的一部分，包括：

- Power 身份、层数/数值、动态变量和与生物的归属关系。
- 会影响未来回合的持续时间、回合开始/结束衰减、延迟触发或跳过衰减状态。
- 部分 Power 的私有方向、选中对象、剩余能量等领域字段。
- 尚未结算完的获得、移除、死亡触发及其他生命周期进度。

这不仅限于 UI 上显示的层数；若某个内部计数会改变未来触发行为，它也需要纳入分支状态或状态指纹。Power overlay 和特定模型状态通过 `SimulatedCombatState`、`PredictionStateStore` 与 `Prediction` 支持代码协作维护。

### 2.5 遗物、药水与其他战斗修正

- 遗物的身份、所属玩家、槽位/状态，以及本场或本回合的计数器和资源。
- 需要跨分支变化的遗物预测状态，例如抽牌贡献轮转、已触发次数、可用次数或已消耗标志。
- 药水槽位、药水身份、可否使用及使用后的分支结果。
- 战斗 Modifier、已捕获的模型状态适配器、适用的 Hook 订阅者及受支持的适配配置。

遗物状态由 [`SimulatedCombatState.Relics.cs`](../reference/CombatSolver/src/Search/SimulatedCombatState.Relics.cs)、[`SimulatedCombatState.RelicResources.cs`](../reference/CombatSolver/src/Search/SimulatedCombatState.RelicResources.cs) 及 [`RelicPredictionStateSupport.cs`](../reference/CombatSolver/src/Prediction/RelicPredictionStateSupport.cs) 等维护；模型适配状态由 [`ModelPredictionStateMirrors.cs`](../reference/CombatSolver/src/Prediction/ModelPredictionStateMirrors.cs) 编入指纹/续接文本。

### 2.6 球队列

对于使用球队列的角色，状态包括球槽容量、球的顺序与身份，以及每个球当前的被动值和激发值。队列顺序会决定被动触发、被挤出和激发结果。影子数据结构见 [`SimOrbQueue.cs`](../reference/CombatSolver/src/Engine/InCombat/Simulation/SimOrbQueue.cs)。

### 2.7 随机数状态

战斗分支维护九条独立 RNG 流的完整可恢复状态，而不只是某个总随机种子或已抽次数：

1. 洗牌（Shuffle）
2. 战斗卡牌生成
3. 战斗药水生成
4. 战斗卡牌选择
5. 能量费用
6. 目标选择
7. 球生成
8. 怪物 AI
9. Niche（特殊/零散战斗随机用途）

每条流的状态参与分支 Fork 和等价判断；一条流被错误地推进或遗漏，可能让后续抽牌、生成、目标或怪物行动不同。流定义见 [`CombatPredictionRngSet.cs`](../reference/CombatSolver/src/Engine/InCombat/Simulation/CombatPredictionRngSet.cs)。

### 2.8 选择点、结算续接和逐回合历史

战斗可能暂停在需要玩家选择的节点，例如从若干牌中选择、从牌堆移动/弃置牌或回合开始时的遗物选择。尚未解决的选择请求、选择来源、可选项和嵌套结算续接位置都会影响下一步可执行动作。

此外，有些牌、Power、遗物或怪物效果依赖本回合已经发生的事实。状态因此会追踪其需要的历史计数，例如：本回合打出的攻击/技能/格挡牌数、打出次数与连打序列、弃牌/消耗牌数、抽到的状态牌、非手牌抽牌数、花费的能量/星能、造成的未格挡伤害、攻击次数和特定攻击命中计数等。只有被当前战斗语义依赖的历史会进入相应指纹/历史键；它们不能全部简化为当前生命与手牌。

对应实现可从 [`SimulatedCombatState.ActionChoices.cs`](../reference/CombatSolver/src/Search/SimulatedCombatState.ActionChoices.cs)、[`SimulatedCombatState.CardContinuation.cs`](../reference/CombatSolver/src/Search/SimulatedCombatState.CardContinuation.cs) 和 [`CombatHistoryCounterKey.cs`](../reference/CombatSolver/src/Search/CombatHistoryCounterKey.cs) 追踪。

## 3. 状态空间与求解器数据的边界

并不是求解器里的每个字段都是原版战斗状态。Beam 宽度、节点/时间预算、路线评分、动作父链、候选排序和搜索阶段等属于求解控制数据；它们决定搜索如何探索或保留路线，不等于游戏战斗局面。

但一些看似“历史”的值会进入战斗等价判断，因为游戏规则会读取它们。反过来，续接协议或路线质量策略也可能比搜索去重键保存更多元数据。判断某个值是否属于状态空间，关键标准是：它是否会改变从该局面继续推进时的合法动作、随机结果、效果结算或战斗终局。

## 4. 项目如何判定两个战斗局面等价

项目有两种用途不同的状态表示：

- **搜索状态键**：[`CombatBeamSolver.StateEvaluation.cs`](../reference/CombatSolver/src/Search/CombatBeamSolver.StateEvaluation.cs) 的 `BuildStateKey` 将回合、玩家与宠物基础值、敌人身份/生命/格挡、主要牌堆、球、九条 RNG、已处理死亡、依赖的战斗历史计数及 `SimulatedCombatState.AppendFingerprint` 中的领域状态混合成双 64 位指纹。它适合高频去重和循环检测，是概率型紧凑键，不是字段逐项比较的数学证明。
- **跨回合续接戳**：[`ContinuationStamp.cs`](../reference/CombatSolver/src/Runtime/ContinuationStamp.cs) 把 live 战斗和预测战斗投影成有序 canonical 状态文本，并比较完整文本是否相等，发现差异时可报告字段位置。除玩家/敌人基础值外，它还写入 AI 与怪物私有状态、有序牌堆、牌的动态/私有字段、球、药水、状态型遗物、Power、适配器状态和九条 RNG。

因此，文中列举的是由现行代码显式表示并用于预测、指纹或续接核验的主要状态维度。具体卡牌/怪物/遗物的适配覆盖由相应镜像与测试覆盖目录决定；对于未镜像或未知语义，不能从这份概览推断项目已经完整模拟。

## 5. 一句话概括

STS2 原版单人战斗的一局面可以理解为：**行动阶段与回合 + 双方生物名册及生命/格挡/Power/AI 状态 + 玩家资源与五个牌区中的牌实例和顺序 + 球/遗物/药水 + RNG 进度 + 结算选择与影响未来的战斗历史计数**。任何会改变未来战斗推进的字段，都可能是状态空间的一部分。
