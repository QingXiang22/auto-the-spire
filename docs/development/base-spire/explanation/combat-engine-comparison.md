# 战斗引擎与效果生命周期对照

文档类型：解释（Explanation）。面向需要审阅共享玩法、迁移遗物／Power，以及判断效果应放在哪个阶段的开发者。

先打开[交互图文](combat-engine-comparison.html)：六个主题分别展示架构、开始阶段、结束阶段、监听与攻击、Power和遗物。点击节点可定位锁定源码，支持突出差异、导出SVG和打印全部图文。下面的静态图与文字适合在仓库内阅读。

本文比较[锁定sts2-sim](../../../../reference/README.md)的提交`611cea2b498ee1eba3743d185c210c6b2a4624a6`（声明版本v0.111.0），以及base-spire提交`9be02893c13d915d3edcf4153337800749a1df09`（包版本`0.7.0-dsl.68`）。图中展示这两个版本的主要路径；差异节点表示当前实现边界，不表示已修复。具体接口仍查[操作参考](../reference/operations.md)和[Power参考](../reference/powers.md)。

## 两侧的职责与状态

![sts2-sim与base-spire的职责、调用与状态对照](assets/combat-engine/overview.svg)

sts2-sim把战斗领域直接写在C#模型与命令中：`RunDriver`通过`CombatRoom`准备本场状态；`CombatEngine`推进阶段；`Commands`执行完整操作并调用`Hook`；具体遗物、Power、怪物或卡牌覆盖模型方法响应。模型可以继续调用命令，形成嵌套效果。长期库存属于Run，战斗参与者和牌区属于Combat，内容私有计数属于模型实例。

base-spire把这些领域规则放在DSL：Host提交公开动作，通用`Session`执行已编译规则；Run与combat过程编排阶段；共享过程处理攻击、牌区、Power与库存；内容通过实例订阅参与通知或只读修正。可变状态、执行帧、随机流和决策续接由core管理。两侧都需要完整领域操作，承载它们的语言与模块位置不同。

迁移时应保留输入、状态变化、事件时点、实例寿命与随机消费。无需复制一个生产C#战斗引擎，也无需把每个C#模型机械地变成一个DSL模板。规则继续放在base，core不增加按游戏内容ID执行效果的分支。

源码入口：[参考CombatEngine](https://github.com/iRyougi/sts2-sim/blob/611cea2b498ee1eba3743d185c210c6b2a4624a6/src/Sts2Sim.Core/Combat/CombatEngine.cs)、[当前DSL生命周期](../../../../mod-the-spire/base-spire/combat/lifecycle.spire)、[Session事件执行器](../../../../core-the-spire/Core.TheSpire/Engine/Session.Events.cs)。

## 开场与玩家回合开始

![编队准备、开场、选招、清格挡与起手抽牌对照](assets/combat-engine/turn-start.svg)

参考的正常准备先按工厂顺序创建全部怪物，抽取HP并分配`CombatId`，随后按槽位排列行动列表。进入战斗后重置玩家与牌区，为全部怪物设置招式图，执行房间准备回调与`AfterAddedToRoom`，再发布`BeforeCombatStart`。出生历史、行动顺序和入场效果不是同一个排序问题。

参考普通玩家回合的主要顺序为：保存各Power的回合开始量，`BeforeSideTurnStart`，为怪物`RollMove`，按条件清格挡与`AfterBlockCleared`，重置能量与`AfterEnergyReset`，增加`TurnNumber`，起手抽牌，`AfterPlayerTurnStart`，`AfterSideTurnStart`，充能球和自动出牌阶段，胜负检查，再允许操作。`AfterPlayerTurnStart`的dispatcher内部依次调用Early、普通和Late；额外回合不按普通路径重新选招。

当前DSL的`BeginDevelopmentCombat`清理旧战斗状态与准备牌区，发布敌人入场和`BeforeCombatStart`；部分生成入口已提前重置招式图，开场订阅又会重置。后续选招位于上一敌方阶段末。玩家开始过程先增加回合号与重置字段，再清格挡、重置能量；`TurnOpen`在起手抽牌前开启，`AfterSideTurnStarted`也在起手抽牌前发布，随后才是抽牌与玩家开始／Late通知。

因此，即使Sai、HappyFlower等都订阅了看似对应的站点，其与起手抽牌的先后关系仍不同。选招还可能提前消费RNG或改变依赖玩家开始状态的条件。应先建立每个站点的准确映射，再修改共享模板；只对齐事件名称不够。

源码入口：[参考CombatRoom](https://github.com/iRyougi/sts2-sim/blob/611cea2b498ee1eba3743d185c210c6b2a4624a6/src/Sts2Sim.Core/Rooms/CombatRoom.cs)、[参考Hook](https://github.com/iRyougi/sts2-sim/blob/611cea2b498ee1eba3743d185c210c6b2a4624a6/src/Sts2Sim.Core/Hooks/Hook.cs)、[当前敌方流程](../../../../mod-the-spire/base-spire/combat/enemy-turn.spire)。

## 回合末与敌方阶段

![回合末早期阶段、虚无耗尽、弃牌及敌方行动对照](assets/combat-engine/turn-end.svg)

参考在自动出牌收尾后进入结束阶段，依次发布VeryEarly、Early与普通末阶段通知，处理充能球、虚无与手牌末效果，再执行`BeforeFlush`、保留／弃牌、`AfterFlush`、`AfterSideTurnEnd`。随后换边进入敌方完整开始生命周期，依次执行已选招式与胜负检查，再进入敌方末阶段。换回玩家后增加Round，下一次普通玩家开始阶段才准备后续招式。

当前DSL依次执行`BeforeTurnEndEffects`、`BeforePlayerTurnEnd`与`FinishTurnHand`，随后是`AfterTurnEnded`、`AfterPlayerSideEnd`和`RunEnemyTurn`。敌方逐实例直接清格挡并发布`EnemyTurnStarted`，行动后发布`AfterEnemyTurnEnded`，选择下一招，再执行玩家回合收尾、费用重置并关闭`TurnOpen`。这条路径尚不是双方共用完整的开始／结束阶段。

Orichalcum说明了为什么需要独立阶段：参考在Early记录“此刻是否零格挡”，允许同阶段的Plating增加格挡，再在普通阶段兑现6点。当前DSL先让Plating加格挡，较晚的Orichalcum才即时检查，因此组合结果改变。应为遗物实例保存短期资格，不能只把两个处理器改成相邻优先级。

耗尽还需要保留原因。参考`causedByEthereal`使DarkEmbrace先累计次数，等持有者`AfterSideTurnEnd`再抽牌；普通耗尽仍即时抽牌。当前`CardExhausted`只传owner／card，DarkEmbrace统一即时处理。应补齐完整耗尽操作的上下文，由具体消费者决定延迟，不应统一推迟所有耗尽效果。

源码入口：[参考DarkEmbrace](https://github.com/iRyougi/sts2-sim/blob/611cea2b498ee1eba3743d185c210c6b2a4624a6/src/Sts2Sim.Core/Models/Powers/DarkEmbracePower.cs)、[参考Orichalcum](https://github.com/iRyougi/sts2-sim/blob/611cea2b498ee1eba3743d185c210c6b2a4624a6/src/Sts2Sim.Core/Models/Relics/Orichalcum.cs)、[当前牌区操作](../../../../mod-the-spire/base-spire/cards/rules/operations.spire)。

## 监听顺序、攻击与战斗结束

![监听者枚举、纯修正、攻击边界与战斗结束状态对照](assets/combat-engine/effects.svg)

参考常用的`RunState.IterateHookListeners`先枚举玩家遗物与药水，再组合Combat监听者；Combat通常按盟友、敌人枚举Power、怪物及玩家专属牌区等。具体dispatcher可以选择不同路径，例如`ShouldClearBlock`使用Power先于库存的路径。因此不存在适用于所有通知的“遗物一定先于Power”规则。参考对不同模型的有效资格检查也有已声明的简化，不能把它理想化为全部模型都会严格重查库存。

当前`Session`在事件入口保存候选与排序键，按`priority`、`instance_order`和声明顺序执行，调用前重查实例存在及筛选资格。它提供通用机制；常用Power优先级0、遗物100只是base的选择，不自动等于源语义。修复应按事件决定参与者与顺序，保留获得顺序、槽位顺序及监听期间移除／新建的行为。

通知允许副作用和嵌套效果；`pure foldEvent`只计算值。参考部分修正链还记录实际修改数值的监听者，再单独通知这些参与者执行副作用。Artifact消费层数属于后一阶段。只返回最终数值，或者在纯修正中直接写状态，都不能完整表达这个流程。

攻击应以整次Attack为边界，再包含多个Hit。现有`ExecuteAttack`已支持`Hits`；旧Tracker模板循环创建八次Attack，会让Vigor在第一击后消费，改变后续伤害。攻击前后通知、每击伤害、反应与结果需要继续复用同一完整操作。

战斗结束也有不同含义。参考分别维护`IsEnding`和`IsInProgress`，各命令决定在结束中能否继续执行，例如最后一个敌人已死时后续抽牌会被拒绝。当前DSL用Attack／Play／Effect等深度计数推迟外层战斗关闭。这有助于完成嵌套操作，但不等于每条命令都已有相同的结束中检查。应逐命令对齐，保留允许发生的末杀后效果。

源码入口：[参考RunState](https://github.com/iRyougi/sts2-sim/blob/611cea2b498ee1eba3743d185c210c6b2a4624a6/src/Sts2Sim.Core/Runs/RunState.cs)、[参考AttackCommand](https://github.com/iRyougi/sts2-sim/blob/611cea2b498ee1eba3743d185c210c6b2a4624a6/src/Sts2Sim.Core/Commands/Builders/AttackCommand.cs)、[当前攻击过程](../../../../mod-the-spire/base-spire/combat/attacks.spire)、[通知与修正规格](../../../design/mod-the-spire/effect-listeners.md)。

## Power的完整变更

![Power施加、层数变更、纯修正及修正后回调对照](assets/combat-engine/power.svg)

参考施加Power时先判断战斗状态、目标附着与可命中资格，寻找已有实例。新建和层数变更都需要经过变更前通知、Given／Received修正，记录实际Received修正者，再提交实例与层数；新建还有`BeforeApplied`。计算完成后通知实际修正者，即使最终变化为0也可能消费其他Power；非零时再处理新建后的通知、层数变化及必要移除。实例模式、来源、回合到期状态也属于这条生命周期。

当前`ApplyPowerWithSource`已有施加者与接收者的折叠、普通／独立／按applier实例、创建与变化通知，以及模块私有状态入口。但已有实例的变更前通知和修正者事后通知尚不完整；`ModifyPowerAmountWithSource`直接写层数、通知变化并按零层移除，没有经过同一完整修正流程。

应先区分新建、叠加、直接增减的源调用关系，再提取共同步骤，避免Apply调用Modify后把修正执行两遍。Artifact尚未登记；其对负面变化返回0，并在修正后扣层，不能仅靠`PowerCreated`或`PowerApplied`订阅补齐。Strength等效果的负面类型还可能依本次变化符号而定。

共同生命周期不应接管所有内容私有计数：Vigor关心本次Attack及开始层数，DarkEmbrace关心虚无耗尽次数；这些状态继续归内容模块。公开标志、来源与通用到期机制才进入共享结构。

源码入口：[参考PowerCmd](https://github.com/iRyougi/sts2-sim/blob/611cea2b498ee1eba3743d185c210c6b2a4624a6/src/Sts2Sim.Core/Commands/PowerCmd.cs)、[参考ArtifactPower](https://github.com/iRyougi/sts2-sim/blob/611cea2b498ee1eba3743d185c210c6b2a4624a6/src/Sts2Sim.Core/Models/Powers/ArtifactPower.cs)、[当前Power生命周期](../../../../mod-the-spire/base-spire/powers/rules/lifecycle.spire)。

## 遗物的库存与持续效果

![遗物获得、来源袋、替换、熔化与实例状态对照](assets/combat-engine/relic.svg)

参考`Obtain`先处理已有可堆叠实例，否则克隆遗物、绑定Owner、加入库存、移出对应来源袋，再执行`AfterObtained`。`Replace`在旧位置插入新实例，并依次处理旧实例移除回调、来源袋和新实例获得回调。`Melt`有自身资格判断，保留库存实例并标记熔化，再调用移除效果；它与真正出库不是同一个操作。

当前`ObtainRelic`创建实例、加入`Relics`并发出`RelicObtained`；拾取模板和持续监听已有可复用基础。`RemoveRelic`出库、通知并销毁，但还没有完整Replace／Melt流程，获得操作的定义验证、堆叠及来源袋关系也需补齐。已有`IsMelted`筛选不能替代熔化生命周期。

遗物计数的寿命需要逐项确认：Nunchaku跨战保留计数，Kunai／Shuriken有回合计数，首次受伤类遗物有每战状态。模板可以复用“相同阶段＋相同正文＋配置参数”，不能只因都带Counter就统一重置。监听顺序还受库存位置影响，因此替换不能简单写成Remove后末尾Obtain。

源码入口：[参考RelicCmd](https://github.com/iRyougi/sts2-sim/blob/611cea2b498ee1eba3743d185c210c6b2a4624a6/src/Sts2Sim.Core/Commands/RelicCmd.cs)、[当前遗物生命周期](../../../../mod-the-spire/base-spire/relics/rules/lifecycle.spire)、[当前拾取模板](../../../../mod-the-spire/base-spire/relics/rules/content-lifecycle.spire)。

## 怎样选择可复用的逻辑

先确认源调用方的阶段、参与者、有效资格和结束中检查，再读Hook与内容正文，最后核对DSL操作、实例顺序与私有状态。共享操作负责完整状态变化；模板负责已证明同构的内容正文。

| 复用位置 | 适合的职责 | 必须保留的差异 |
| --- | --- | --- |
| 完整攻击操作 | 一次Attack、多次Hit、伤害反应与结果 | 攻击与命中边界、提前死亡、Vigor消费 |
| Power变更操作 | 前置检查、修正、提交、实际修正者通知、移除 | 实例模式、来源、变化符号与内容私有状态 |
| 生成并加入牌区 | 创建实例、持有者与牌区关系、通知 | 临时牌／永久牌、公开性、复制语义 |
| 遗物库存操作 | 获得、堆叠、替换、熔化及回调 | 来源袋、原位置、库存保留与监听资格 |
| 已有查询与值构造 | 进阶数值、结构化意图、有效成员筛选 | 完整来源池、槽位顺序、随机消费 |
| 参数化内容模板 | 同一站点、同一条件、同一正文 | 计数寿命、延迟兑现、特殊强制招式回调 |

怪物招式图与单次多击见[#126](https://github.com/QingXiang22/auto-the-spire/issues/126)，出生、槽位与选招顺序见[#127](https://github.com/QingXiang22/auto-the-spire/issues/127)。遗物／Power的后续工作由[#128](https://github.com/QingXiang22/auto-the-spire/issues/128)组织。这里解释当前机制与复用边界，任务状态以Issue为准。

结构扫描、定义登记、组件比较与正常Run可达性是不同判断。本地`no-energy-gain`没有按当前源类直接对应，需单独说明来源与用途；不能把名称对应或条目数量当成行为完成度。人工构造的组合可以发现真实缺陷，但不能替代正常Run比较，也不能据主路径图推导额外回合、复活、角色专属流程或全部遗物／Power都已支持。
