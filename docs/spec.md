# auto-the-spire 项目规范（草案）

> 源码依据：仓库中的 [`sts2/`](../sts2/README-for-browsing.md) 是从本机游戏数据恢复的浏览工程，并非开发者发布的原始源码。以下“源码观察”只描述当前这份恢复工程；`core-the-spire` 是独立实现，不直接复制其 Godot 运行时结构。

## 1. 目标与范围

本项目最终为 AI 提供可复现、可观测、可控且可扩展的《杀戮尖塔 2》训练环境。工作按依赖关系分三阶段推进：

1. **通用内核 `core-the-spire`**：先建立独立于原版内容的游戏运行态模型、玩家动作与内部命令、结算调度、内容注册及 mod hook。用独立的最小测试规则包验证这些契约。它是后续两个阶段的基础。
2. **原版规则包 `mod-the-spire/base-spire`**：通过内核的内容和扩展接口逐步描述原版内容，先完成可校验的战斗切片，再扩展整局流程，并与真实游戏行为对照。
3. **其他 mod 与实际测试**：在相同接口上组合其他 mod，选择实际 mod 验证表达能力、组合行为和模拟结果。明确记录必须适配的 mod 机制。

训练环境以模拟状态机为主要执行后端，同时保留连接真实游戏的后端。二者通过统一的外部操作与观察接口访问。支持范围以明确的规则契约和测试结果界定，不预设任意真实游戏 mod 都能未经适配直接运行。

当前完整性目标是恢复源码快照中的**单人玩法闭环**：从角色与种子配置开局，经过地图、各类房间和战斗，到胜负结算；包含快照中全部原版玩法内容，默认所有卡牌、遗物、药水等均已解锁。账号级解锁、成就、多人、UI、音频和平台服务不属于模拟目标。恢复源码是规则实现依据；本机游戏用于抽样差分验证。

## 2. 项目边界与已确定的技术约定

| 项目 | 职责 |
| --- | --- |
| `core-the-spire` | 用 **C#** 实现与 Godot 解耦的游戏运行态、玩家动作与内部命令、调度器、规则结算、随机数服务、内容注册和 mod hook。内核不直接写死原版卡牌、怪物或地图内容。 |
| `mod-the-spire/base-spire` | 作为 `mod-the-spire` 的子项目，承载原版卡牌、怪物、状态、地图配置及相关全局配置。它与其他 mod 使用同一内容注册和扩展接口。 |
| `mod-the-spire` 中的其他子项目 | 注册或改写内容与规则，按约定组合成最终规则集；各 mod 的动态状态使用独立命名空间。 |
| `net-the-spire` | 隔离外部操作和后端，向 AI 暴露统一的动作、观察和结果接口；后端可以是模拟内核或真实游戏适配器。 |
| `ai-the-spire` | 消费训练环境接口的 AI 相关代码；不直接修改内核状态。 |
| `reference` | 研究真实游戏接入、动作执行和状态提取的参考资料；其搜索与 SL 策略不作为内核规则。 |

最终规则集由 `core-the-spire`、`base-spire` 和所选其他 mod 组合而成。原版是通用内核加载 `base-spire` 后形成的一个具体规则集。内容定义可以使用 DSL 或结构化数据（如 JSON）；效果和行为使用共享的效果表示与受控 hook。内容 ID 要稳定且带命名空间，注册顺序、覆盖规则和规则集版本须可追踪。具体 DSL 语法须由可运行案例反推。

角色通过一个或多个 `CardPoolDefinition` 声明可生成的卡牌池；卡牌定义带有稀有度，注册构建时校验池中卡牌和角色池引用。当前 `base-spire` 的 Ironclad 池有 3 张 Basic 起始牌、13 张 Common、8 张 Uncommon 和 4 张 Rare；87 张原版池尚未齐备，不能用于完整奖励抽取。Barricade 与 Well-Laid Plans 已注册；Barricade 属于 Ironclad Rare 池，Well-Laid Plans 按原版 Uncommon Power 的费用、层数及升级效果注册为通用内容定义，但尚未加入 Silent 卡池（该卡池尚未建立）。Entrench 已按 Event 稀有度注册，暂不放入普通 Ironclad 奖励池；Impervious 则验证了 30/40 格挡、消耗关键字和 Feel No Pain 触发的组合。卡牌定义以 `CardKeywords` flags 表达 Exhaust、Ethereal、Unplayable 和 Retain；回合结束结算先逐张消耗手牌中的虚无牌并处理消耗触发，再保留标记牌并将其余牌移入弃牌堆，之后进入敌方行动。Dazed 与测试用 Retain 牌验证了牌区、合法动作和事件时序。IRuleHook 可按卡牌实例抑制 Ethereal 消耗、按玩家与已注册 Power 状态抑制整手清理，以及在实体回合开始时决定是否清空其格挡；Hook 调用按包注册顺序短路，已由测试验证。BeforeFlush / BeforeFlushLate 可返回效果 DSL 节点并暂停等待选牌；AfterFlush 收到实际弃置与保留实例快照。`ChooseCardsFromPileEffect` 建立可选数量区间，动作端逐张追加选择并显式确认，可合法选择零张；待选观察使用单一带判别字段的 `PendingCardSelectionView`：单选和多选共享一个 pending 状态，分别暴露候选，以及多选所需的上下限和已选实例；调度器不维护并行的单选/多选暂停标志。Well-Laid Plans Hook 在 `BeforeFlushLate` 按 Power 层数生成选牌效果，基础版最多留 1 张、升级版最多留 2 张，并排除已带 Retain 的牌。Barricade 通过 `ShouldClearBlock` 保留玩家格挡，基础费用为 3、升级减为 2；该 Hook 默认允许清空，多个规则包按全体同意才清空的方式组合。`GainBlockFromCurrentBlockEffect` 读取执行时的格挡值并按倍率增加格挡；Entrench 验证等量增加、零格挡与升级减费。`RetainSelectedCardEffect` 验证选择后留手与恢复结算。原版逐模型 Hook 收集、Bookmark 等遗物成本修改及回合末卡牌效果仍待迁移。Power 可声明玩家回合开始、玩家回合结束或敌方回合结束触发；玩家回合开始触发在能量重置后、抽牌前结算，Demon Form 以此持续获得力量。No Draw Power 通过 `NoDraw` 角色阻止本回合抽牌，并在玩家回合结束触发时消失；Battle Trance 验证先抽牌再禁止后续抽牌、升级抽牌数量和下回合恢复。Power 支持由效果 DSL 定义的触发效果和按层数缩放的格挡；Feel No Pain 在牌被消耗后按当前层数一次性获得格挡。生成牌的 DSL 可选择继承当前打出牌的升级级别，Anger 用此规则生成自身克隆。DSL 支持按具体卡牌实例暂停并等待玩家选择，再以该稳定实例 ID 执行牌堆移动或续接效果；过滤器可按是否可升级筛选候选，Armaments 验证单张升级、升级所有可升级手牌及空候选分支。`RandomlyMoveCardFromPileEffect` 使用命名 `card-selection` 随机流抽取候选；True Grit 验证基础版随机消耗手牌、升级版改为玩家选择以及空手分支。`RandomAttackEffect` 每次执行都从存活敌人中重新抽取目标；Sword Boomerang 验证多击目标抽取、升级增加击数和按种子重放目标序列。`ForEachCardInPileEffect` 支持基于卡牌类型过滤并给每个匹配牌实例续接一组效果；Second Wind 验证逐张消耗非攻击牌并按每张牌获得格挡。`AttackUsingBlockEffect` 在结算时读取攻击者当前格挡作为基础伤害；Body Slam 验证格挡动态伤害沿用普通攻击修正与升级费用变化。`LoseHpEffect` 表达无格挡、无力量修正的生命损失；Bloodletting 与 Offering 复用该原语结算生命、能量和抽牌。

动态牌组与战斗牌堆保存 `CardInstance`，用稳定的实例 ID 区分同一内容定义的多张牌，并记录升级级别。牌在战斗区域间移动时保留实例身份；卡牌定义可按实例升级级别解析费用与效果；`PendingCardPlay` 将正在结算的牌实例与 X 能量快照合并保存，X 费卡打出时一次性花费当前剩余能量，`RepeatForXCostEffect` 按该快照重复效果。Whirlwind 验证多敌多击、升级和 0 能量打出。此实例基础仍未覆盖完整的运行期修改数据，也未把战斗新增、移除或变形等牌组改动统一回写到局外牌组；这些生命周期规则需随奖励、营火及卡牌效果逐步补齐。

C# 是 `core-the-spire` 的既定实现语言。恢复源码中的 [`Rng`](../sts2/src/Core/Random/Rng.cs) 包装的是自定义 [`MegaRandom`](../sts2/src/Core/Random/MegaRandom.cs)，后者实现 Xoshiro256**；**仅使用 C# 或 `System.Random` 不会自动复现原版随机数**。运行级与玩家级随机流分别见 [`RunRngSet`](../sts2/src/Core/Runs/RunRngSet.cs) 和 [`PlayerRngSet`](../sts2/src/Core/Random/PlayerRngSet.cs)。首阶段以固定种子从开局按需取数和重放为目标；随机流由运行会话持有，暂不要求把游标写入 `GameState`。原版存档确实保存各流计数器，任意中途恢复或分叉时必须增加等价能力，见第 3 节。

### 2.1 依赖方向与最小接口

- `core-the-spire` 不依赖 `base-spire`、其他 mod、`net-the-spire`、Godot 或恢复工程；它提供规则包注册、运行会话、状态推进和视图投影能力。
- `base-spire` 依赖内核提供的注册与扩展接口；其他 mod 依赖内核，也可显式依赖 `base-spire` 中的内容 ID。
- `net-the-spire` 提供统一协议，分别实现模拟后端（调用内核及已组合规则包）和真实游戏后端（连接外部游戏）。`ai-the-spire` 只使用协议接口；`sts2/` 是对照资料，不成为内核的编译依赖。

统一协议的首批操作是启动或重置会话、获取观察与合法动作、提交一个玩家决策、读取结果与后端能力。模拟后端可接受测试场景与种子；真实游戏后端只能声明自己实际支持的重置和控制能力。规则包组合发生在启动会话前，同一会话的规则集版本保持固定。Core 提供通用整局地图流转、地图访问历史和 Unknown 房间解析；`base-spire` 已按恢复源码迁移单人标准地图生成流程及赔率配置。Act 可声明 normal weak/regular、elite、boss encounter 池，以及 act-local/shared Event 和 Ancient ID 池；`ContentRegistry.Build()` 校验 ID 存在及事件类别。核心按原版顺序在 `up_front` 流上洗牌共享 Ancient、分配至后续 Acts、洗牌 Event 池、生成 encounter 队列与 Boss、抽取本幕 Ancient。进入事件房时，选中的 Event ID 出现在可观察房间帧；单局未访问过的事件会优先出队，耗尽后允许重复。encounter 同样在房间帧中暴露并可直接启动战斗。遭遇池实现等权 grab bag、相邻重复/tag 回避、源逻辑回退和队列轮换；Boss 暂时固定为本幕生成的一个 encounter。测试验证了这一配置驱动的调用计数顺序与 seed 重放。`RunEventDefinition` 已支持静态选项与 RunEffect DSL；原语覆盖金币增减、治疗、失血、最大生命增减、加入卡牌，以及可暂停并按实例 ID 续接的选牌升级/移除。mod 可注册 `ICustomRunEffectHandler`，通过 `customRun` JSON 节点调用受控的整局命令，不需为每个 mod 效果改动核心。事件选项及待选牌进入观察，合法动作会过滤不可支付或无候选的选项并完成房间转场。`base-spire` 尚未注册原版完整事件/遭遇/Ancient 池，原版事件条件、完整卡牌操作组合、遗物/药水等效果待补，因此真实内容 seed 仍未和原版对齐。

## 3. 规则定义、游戏运行态和外部视图

这三者必须分开建模：

- **规则定义 `GameDefinition`**：已组合的内容、效果、敌人行为、地图生成规则和全局配置，带有可追踪的规则集版本或标识。地图生成规则属于定义，生成后的地图属于运行态。原版 [`ModelDb`](../sts2/src/Core/Models/ModelDb.cs) 与 [`AbstractModel`](../sts2/src/Core/Models/AbstractModel.cs) 区分内容概念及可变实例；本项目也要区分定义和实例，但不必照搬其类层次。
- **游戏运行态 `GameState`**：当前一局游戏中会影响后续规则结算的数据，包括不向玩家公开的游戏信息。运行会话同时持有随机数服务；其内部进度首阶段不作为 `GameState` 字段。
- **外部视图 `Observation`**：按观察者及后端能力，从游戏运行态或真实游戏采集结果投影出的可见信息。合法动作从当前运行态和规则推导；不能把隐藏牌序等内部数据无意暴露给 AI。调试或校验用的内部视图应与训练观察分离。

在**同一规则集版本、同一种子、同一外部动作序列、同一随机数调用顺序**下，从开局重放应得到相同结果。仅有种子和中途的 `GameState` 并不足以从该时点直接恢复随机数进度。恢复源码的 [`SerializableRunRngSet`](../sts2/src/Core/Saves/Runs/SerializableRunRngSet.cs) 和 [`SerializablePlayerRngSet`](../sts2/src/Core/Saves/SerializablePlayerRngSet.cs) 都保存种子与各流计数器，并在加载时快进。因此，若未来实现任意快照和分叉，须重放此前动作或保存等价的流进度；首阶段不把该能力作为前提。

`net-the-spire` 对外提供一致的动作和观察语义，并声明各后端支持的能力。例如，指定种子、任意重置和状态复制可能只在模拟后端可用，不能由真实游戏后端虚假承诺。

## 4. 游戏运行态结构（`GameState`）

| 区块 | 必须容纳的子内容 | 边界 |
| --- | --- | --- |
| 运行上下文 | 规则集标识与版本、模式、难度、初始种子、当前幕、运行级计数器、共享的规则修饰器与概率状态 | 内容定义本身不复制进动态状态。 |
| 玩家长期状态 | 玩家集合；每名玩家的角色身份、当前与最大生命、金币、遗物、药水、牌组中的卡牌实例、永久升级或修改、跨房间变量 | 原版 [`RunState`](../sts2/src/Core/Runs/RunState.cs) 持有玩家集合；首个实现只要求单人战斗，但结构不能把玩家身份混成全局字段。 |
| 世界与路线状态 | 当前幕和层、已生成地图及节点连接、当前位置、已访问和可选节点、已生成或已揭示的房间信息、路线标记 | 未揭示的信息仍可存在于游戏运行态，但不能自动进入外部视图。 |
| 房间栈与当前场景 | 可嵌套房间的栈，以及栈顶房间的战斗、事件、商店、休息、奖励等局部状态 | 原版 `RunState` 允许事件中的选择再进入战斗，完成后返回事件；单一互斥场景字段不足以表达该流程。 |
| 执行中状态 | 待结算效果与事件、待完成的选择、继续执行所需的上下文 | 效果要求玩家选择目标或卡牌时，状态机必须能暂停并恢复。 |
| mod 私有状态 | 按 mod 命名空间存放的整局、场景、实体或卡牌实例级动态数据及其版本 | mod 不得通过无约束字段写入绕过内核结算规则。 |

运行态应只保存规则结算或恢复执行所需的游戏事实。可重新计算的合法动作、展示文案和预测数值属于派生信息；事件日志可用于观测与排错，但只有规则真正依赖的历史摘要才进入权威运行态。

### 4.1 战斗场景

战斗场景至少包含：

1. **时序**：战斗轮数、当前阵营、每名玩家的回合数与阶段、当前决策点。原版的阵营轮数与玩家回合数并不相同，见 [`CombatState`](../sts2/src/Core/Combat/CombatState.cs) 和 [`PlayerCombatState`](../sts2/src/Core/Entities/Players/PlayerCombatState.cs)。
2. **参与者**：玩家、敌人与其他可行动实体的身份、生命、格挡、状态及层数、战斗属性、存活状态、实体级扩展数据。
3. **卡牌区域**：每名玩家的抽牌堆、手牌、弃牌堆、消耗堆、出牌区；各区域持有有序的卡牌实例。局外牌组另存于玩家长期状态。
4. **资源**：每名玩家的当前能量、星星等由规则集定义的资源；其他机制所需的战斗实例状态可扩展。
5. **敌人行为**：已选定的下一行动、行动状态机当前位置与局部记忆、目标及行为选择所需数据。原版使用 [`MonsterMoveStateMachine`](../sts2/src/Core/MonsterMoves/MonsterMoveStateMachine/MonsterMoveStateMachine.cs)。内核敌人动作以有向图描述，边存于动作定义；条件分支、随机选择和运行时记忆由后续扩展，不把行为压成只支持循环尾动作的列表。
6. **结算上下文**：待结算效果、触发事件和未完成的玩家选择。

敌人的**已选定行动**与**显示给玩家的攻击意图**是不同概念。外部视图中展示的伤害应按真实《杀戮尖塔 2》的实际行为生成；展示逻辑由视图／适配层隔离，不将显示数字当作攻击效果的原始定义。具体预测和展示规则需要与真实游戏对照。

### 4.2 其他场景

事件、商店、休息、奖励和路线选择场景保留独立决策接口，并逐步补全各自的库存、选项、局部变量及结算规则。当前营火流程已进入 run 状态机：单人提供最大生命 30% 治疗或升级一张可升级牌，规则 hook 可调整治疗并响应选择。Run 事件池提供 Event/Ancient 内容定义、按源顺序生成和可观察 ID 选择；通用事件选项与 RunEffect DSL 支持金币、治疗、失血、最大生命增减、加入卡牌，以及暂停续接的单牌升级/移除选择。已提供受控的 C# 自定义 run 效果 handler 供 mod 扩展；原版事件条件和完整效果仍待实现。房间可压栈与出栈；原版 [`StandardActMap`](../sts2/src/Core/Map/StandardActMap.cs) 通过路径生成、节点类型分配、限制检查和后处理构图，且 [`RunManager.GenerateMap`](../sts2/src/Core/Runs/RunManager.cs) 提供地图修改 hook。因此 `base-spire` 的地图内容应包含参数和生成器实现或受控扩展，不能预设一份静态 JSON 足以表达原版地图。

## 5. 决策边界与效果结算

外部动作表示玩家在当前决策点的选择，例如打出某张卡、使用药水、选择目标或结束回合。内部命令表示造成伤害、施加状态、移动卡牌、获得资源等规则效果；外部客户端不能直接提交内部命令来绕过规则。这对应恢复源码中 [`GameAction`](../sts2/src/Core/GameActions/GameAction.cs) 与 [`Commands`](../sts2/src/Core/Commands/Cmd.cs) 的明确区分。

一次环境 `step` 接受一个合法外部动作，执行内部命令及 hook，直到到达下一个需要外部选择的决策点或终局。若效果中途要求选择，执行上下文必须能暂停并恢复。原版 [`GameAction`](../sts2/src/Core/GameActions/GameAction.cs) 和 [`PlayerChoiceContext`](../sts2/src/Core/GameActions/Multiplayer/PlayerChoiceContext.cs) 已有暂停、恢复及嵌套选择的机制；本项目可以换实现方式，但要保留这些语义。

### 5.1 效果语言的边界

效果 DSL 第一版先表达**组合结构**（顺序、条件、目标、次数）与**有规则语义的命令**。最小命令候选是攻击、施加／修改状态、抽牌／移动卡牌、获得格挡、修改资源和请求选择。目标解析应同时支持自身、首个／指定对手及所有对手，使 AoE 由共享规则节点组合，而非卡牌专属代码。攻击不能实现成直接减少生命；原版的 [`DamageCmd`](../sts2/src/Core/Commands/DamageCmd.cs)、[`PowerCmd`](../sts2/src/Core/Commands/PowerCmd.cs) 及 [`Hook.ModifyDamage`](../sts2/src/Core/Hooks/Hook.cs) 表明伤害和状态应用均经过多阶段结算。

每种命令必须规定输入、合法性、修正 hook、原子提交点、后续事件及失败结果。状态触发需要指明精确阶段（例如 `AfterSideTurnEnd`），不能只使用含糊的“回合开始／结束”。执行与显示效果分离；音效、动画和等待不进入训练状态机的规则语义。

可用结构化数据定义普通内容和效果组合；无法合理表达的新机制通过受控 C# 扩展接口实现。Power 定义可声明统一的出手伤害倍率和“施加者死亡时移除”，Power 实例记录施加者；这些共享语义用于表示 Shrink 等规则，不为某只怪物写专用伤害分支。原版的 [`DampCultist`](../sts2/src/Core/Models/Monsters/DampCultist.cs) 将行动状态机、效果命令及表现代码写在一个类中，正说明本项目需要把这三者拆开。`base-spire` 应通过与其他 mod 相同的 DSL／扩展接口注册原版内容，以实际内容检验接口是否够用。

### 5.2 Hook 与 mod 兼容范围

Hook 要覆盖内容生命周期、伤害与状态结算、回合和阵营阶段、地图生成及房间事件；监听对象与遍历顺序必须确定。未知地图点至少开放两个独立扩展点：修改可抽取房间类型集合，以及修改未抽中房间的赔率增量。恢复源码的 [`UnknownMapPointOdds`](../sts2/src/Core/Odds/UnknownMapPointOdds.cs) 在基础黑名单后调用 `ModifyUnknownMapPointRoomTypes`，再逐项通过 `ModifyOddsIncreaseForUnrolledRoomType` 调整赔率；因此候选集合 hook 必须先于赔率增量 hook，并按稳定的 mod 注册顺序执行。恢复源码的 [`CombatState.IterateHookListeners`](../sts2/src/Core/Combat/CombatState.cs) 会收集状态、怪物、遗物、药水、战斗牌区中的卡牌及其附加效果、修饰器等；[`Hook`](../sts2/src/Core/Hooks/Hook.cs) 还区分普通与较晚的回合结束回调。设计 hook 时应按阶段逐项对照，不能把所有触发合并成一个通用事件。

真实游戏的 [`ModManager`](../sts2/src/Core/Modding/ModManager.cs) 可加载程序集，并允许 Harmony 补丁。它能改写任意方法，因此“支持实际 mod”不能等同于自动运行所有原版 mod。项目支持的范围是能映射到内容注册、DSL 命令和受控 hook 的 mod；超出范围的补丁需要适配或新增内核能力，并记录在兼容性清单中。

## 6. 首个战斗校验案例

现有“铁甲战士 `56/65` 生命、`5` 打击／`4` 防御／`1` 痛击、咔咔先施加 `仪式(5)` 再以基础伤害 `3` 攻击”的叙述保留为**内核测试夹具**，不标注为未经修改的原版遭遇。手牌、抽牌顺序、敌人生命、能量及地图位置均由夹具显式指定。

源码对照给出两个必须修正的事实：

1. 接近此行为的原版敌人 [`DampCultist`](../sts2/src/Core/Models/Monsters/DampCultist.cs) 使用“念咒 → 循环黑暗打击”的行动状态机。通常条件下念咒施加 `仪式(5)`、攻击基础伤害为 `1`；启用 `DeadlyEnemies` 条件时相应数值是 `6` 和 `3`。因此“仪式 5、攻击 3”是混合参数的测试夹具。
2. 原版 [`RitualPower`](../sts2/src/Core/Models/Powers/RitualPower.cs) 在持有者**阵营回合结束后**施加力量；敌人刚施加仪式的那次结束会跳过。以夹具的 `仪式(5)`、基础攻击 `3`，且没有其他修正为例：首次行动施加仪式；第二次行动攻击造成 `3`，该回合结束后获得 5 力量；第三次行动攻击造成 `8`。此前“第二次行动在回合开始获得力量并打出 8”的描述不符合这份源码。

第一批校验分为两条：内核夹具验证命令、阶段和重放；`base-spire` 对照用真实 `DampCultist` 的难度参数和时序验证原版一致性。攻击意图伤害由视图根据当前状态计算，原版 [`AttackIntent`](../sts2/src/Core/MonsterMoves/Intents/AttackIntent.cs) 也调用伤害修正逻辑来产生展示值；实际显示细节仍需运行真实游戏比对。

## 7. 源码观察与设计影响

| 源码观察 | 本项目的设计影响 |
| --- | --- |
| [`RunState`](../sts2/src/Core/Runs/RunState.cs) 持有玩家集合、地图、当前幕、房间栈、运行修饰器及运行级 RNG。 | 将运行态拆成玩家、世界、房间栈和会话随机数服务；首期可以只支持单人，但保留玩家身份。 |
| [`Player`](../sts2/src/Core/Entities/Players/Player.cs) 持有牌组、遗物、药水、金币及玩家级 RNG；[`PlayerCombatState`](../sts2/src/Core/Entities/Players/PlayerCombatState.cs) 持有五个战斗牌区、能量、星星、回合阶段和额外战斗机制。 | 明确长期牌组与战斗牌区、长期资源与战斗资源的生命周期。 |
| [`StandardActMap`](../sts2/src/Core/Map/StandardActMap.cs) 过程生成路线、节点类型并进行后处理。 | 把地图拓扑与生成算法分离；原版生成器由 `base-spire` 提供。 |
| [`ModelDb`](../sts2/src/Core/Models/ModelDb.cs) 按 ID 查找内容；[`AbstractModel`](../sts2/src/Core/Models/AbstractModel.cs) 区分规范与可变实例。 | 规则包注册稳定 ID 的定义，运行态只持有实例及其变化。 |
| [`GameAction`](../sts2/src/Core/GameActions/GameAction.cs) 是玩家输入，内部小效果由 Commands 执行。 | 对外 Action 与内部 EffectCommand 使用不同类型和权限。 |
| [`Rng`](../sts2/src/Core/Random/Rng.cs) 包装自定义 PRNG；[`RunRngSet`](../sts2/src/Core/Runs/RunRngSet.cs) 与 [`PlayerRngSet`](../sts2/src/Core/Random/PlayerRngSet.cs) 有命名随机流并保存计数器。 | 以可替换、可观测的随机数服务实现原版兼容；仅凭种子不能从中途任意恢复。 |

## 8. 下一步交付与验收

首个可运行里程碑及后续整局地图迭代已落地到根目录 `AutoTheSpire.sln`：

1. **内核契约已建立**：`core-the-spire/Core.TheSpire` 实现 `GameDefinition`、不可从外部取得的 `GameState`、独立持有 RNG 的 `GameSession`、外部动作、观察投影、效果树、规则包注册和受控 Hook 事件。
2. **最小战斗切片已建立**：测试规则包覆盖攻击、格挡、仪式、三段行动时序、选择暂停/恢复、非法动作拒绝和种子重放。
3. **原版内容切片已建立**：`mod-the-spire/base-spire` 注册铁甲战士起始角色（生命、金币、起始牌组、Burning Blood 起始遗物）、Strike/Defend/Bash 的基础及升级效果、力量、易伤、仪式和普通/DeadlyEnemies 下的咔咔行动；Burning Blood 通过 `IRunHook` 在胜利战斗结算后回复 6 点生命，且受最大生命限制。
4. **mod 扩展已验证**：`mod-the-spire/test-hook-mod` 作为独立子项目覆盖一张卡、新增一张卡，通过受控 C# 战斗与整局效果 handler 执行扩展效果；额外注册 Ancient 事件验证 `customRun` 从 DSL 调用 mod handler，并用每局隔离的命名空间状态观察出牌事件。`IRuleHook.OnEventEffect` 允许按战斗事件返回 Effect DSL 节点，由核心效果队列统一结算；规则上下文可查询当前 run 遗物，已用于 Charon's Ashes。
5. **原版标准地图切片已建立**：`base-spire` 实现 Overgrowth、Hive、Glory、Underdocks 的单人地图参数，以及路径生成、节点类型分配、限制、路径剪枝/修复和网格后处理；地图入口 Ancient 会成为每幕开头待处理事件。
6. **Unknown 房间赔率已建立**：`core-the-spire` 提供规则定义配置及跨节点累计/重置运行态；`base-spire` 配置原版 Monster/Elite/Treasure/Shop 初始赔率并由 Event 承接剩余概率。运行支持按原版字符串种子哈希初始化随机流。
7. **源码审计已建索引**：`docs/sts2-core-source-audit.md` 记录当前核查来源、实现映射、待深读规则域和明确的等价性限制。
8. **遭遇、整局战斗与奖励接缝已建立**：encounter 可包含有序的多个敌人，敌人各自持有行动状态机并依次行动，支持选敌目标；整局可按 encounter 定义启动战斗并在胜负时回写。遭遇槽支持固定敌人、随机候选池和组内不重复；敌人阵容在进入 run 房间时抽取并保存在房间视图，启动战斗时沿用该阵容。战斗胜利进入统一奖励阶段，支持金币、三选一卡与遗物的领取/跳过；精英可生成遗物。宝箱房提供开箱动作，发放 42–52 金币并抽取一件遗物；共享宝箱袋与玩家奖励袋分别维护，玩家袋由共享遗物及匹配当前角色的 `CharacterPoolIds` 遗物构成。遗物领取时从两袋移除，跳过宝箱遗物只消耗共享袋中的选项。`base-spire` 已注册 Burning Blood、Anchor、Vajra、Ironclad 专属的 Charon's Ashes 与兜底 Circlet；Anchor、Vajra 在战斗开始时生效，Charon's Ashes 通过事件 Effect Hook 在消耗牌后造成 3 点无力量加成的全体伤害。伤害 DSL 现在区分 Move、Unpowered、Unblockable 属性。稀有度抽取与预洗牌袋已分离，但原版遗物池成员、可用性 Hook 和完整 RNG 调用次序仍需逐调用对照。普通战斗三选一卡按角色引用池及赔率生成；最后一幕 Boss 不生成默认奖励。当前 Ironclad 已迁入 28 张奖励池卡牌并注册 Event 卡 Entrench，尚未达到 87 张完整卡池；Potion 奖励、完整 Boss 规则、全部原版遗物效果和遗物池仍待迁移。`

运行 `dotnet run --project core-the-spire/Core.TheSpire.Tests` 执行无外部测试包依赖的规则检查；运行 `dotnet build AutoTheSpire.sln` 构建当前项目。当前检查覆盖地图图校验、可达节点选择、房间结果资源回写、跨幕推进、多敌战斗和终局，但不等同于原版整局支持。

后续补齐 Ironclad 与其他角色的完整 card pool、卡牌实例永久修改和玩家奖励 odds，完成卡牌奖励源规则差分，再实现 Potion 奖励、完整遗物池成员和遗物 Hook；Treasure/Elite 的 Relic 奖励流程已有通用内容定义与选择阶段，角色专属池结构已建并迁入 Charon's Ashes，但多数原版成员、可用性 Hook 和全量效果尚未实现。战斗事件 Hook 已可将事件映射到 Effect DSL，伤害节点区分 Move、Unpowered、Unblockable，但完整原版事件顺序、修正 Hook 与伤害阶段仍需对照。RunEvent 与 RunEffect DSL 已支持基础资源变化、最大生命增减和单牌升级/移除选择；最大生命损失当前尚未接入统一 HP loss / Damage Hook。RunDefinition 可声明 Final event，终局只能通过带 WinRunEffect 的合法选项完成。继续迁移原版 Architect 与事件内容、条件和更完整的选项效果；并扩充真实 Act encounter/Event/Ancient 内容池及可用性筛选、双 boss 选择、战斗中的牌组状态变化、真实敌人 HP/意图初始化、商店和其他非战斗房间。营火已提供治疗/升级选择，但仍缺全部原版选项修改 hook。通用房间栈已支持把子房间压入活动房间并在完成后返回父房间；整局战斗接缝可根据房间选择共享 run 随机流、从整局牌组洗牌发起多敌战斗、运行奖励决策并回写终局生命。地图生成已按源码实现并进行多 Act、多种子确定性与布局检查；仍需接入可运行原版构建或实际游戏，对固定 seed 地图做逐点差分校验。每个规则域都增加源码映射和行为校验；完成标准是全单人玩法闭环与内容覆盖，不以当前整局骨架宣称完整原版兼容。

## 9. 待验证事项

- 恢复源码与当前实际游戏版本的差异，以及原版各阶段的完整 Hook 监听集合、顺序和伤害取整规则。
- 当前 RNG 已对照 `MegaRandom` 的种子 0 序列，但命名流分类、运行/玩家流种子派生、玩家槽规则和存档计数器尚未完整映射。
- 当前战斗支持单玩家、多敌基础行动队列和攻击/施加状态的 AoE 目标；剩余 Hook/状态、遗物/药水/修饰器、完整卡牌生命周期、原版敌人行动选择和房间间状态联动尚待实现。
- 当前整局层支持原版地图生成与房间队列骨架、房间结果回调、战斗奖励决策、营火治疗/升级、通用静态事件选项、宝箱与精英遗物奖励和跨幕/终局流转；仍缺大量原版事件、商店、药水奖励、遗物内容/效果及内容 Hook。
- 各场景完整字段、训练观察可见性与真实游戏适配器的能力边界仍待逐域设计和校验。
- 代表性战斗和 mod 场景下的环境 `step` 吞吐、运行态表示和观察生成成本尚未测量；若引入状态复制，再测量其成本。
