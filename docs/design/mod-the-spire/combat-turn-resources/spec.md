# 回合生命周期与生命／能量资源

关联 CORE-AUTHOR-05／**DSL-COMBAT-TURN-01**；实施与认领见[GitHub 任务清单](https://github.com/QingXiang22/auto-the-spire/issues/28)。本包基于前一批战斗开始格挡交付，按[纯 DSL 规格](../base-architecture.md)、[目录设计](../base-architecture.md)和[机制迁移指南](../../../areas/base/how-to/migrate-mechanism.md)编写；状态只在 GitHub Issues 维护。

## 已核实来源与范围

当前规则以锁定的 `reference/sts2-sim/src/Sts2Sim.Core/Combat/CombatEngine.cs`、`Entities/Creatures/Creature.cs`、`Commands/CreatureCmd.cs` 与 `Hooks/Hook.cs` 为依据。本页定义已有回合入口、生命／能量以及双方格挡命令；卡牌、伤害、终局和正常返回分别由所属领域约定，不以本页的组件判别替代正常整局验收。

Lantern在首次玩家侧开始加能量，BloodVial在首次玩家Late回复生命，HappyFlower按实例跨战计数。HornCleat读取AfterBlockCleared处尚未增加的回合号，值为1时在第二回合获得14无加成格挡；不能把它移动到增加编号之后。玩家首回合以入口旧TurnNumber=0识别并保留格挡，仍发布AfterBlockCleared。

## 行为要求与设计

| ID | 约定 | 判别 |
| --- | --- | --- |
| TC01 | Player 增加 Hp／MaxHp／Energy／MaxEnergy／TurnNumber／TurnOpen；开始战斗置 TurnNumber=0、Energy=0、Block=0、TurnOpen=false 后再发 BeforeCombatStart | 新周期从 0 回合 0 能量开始；监听器观察到的仍是已活跃状态；HP 不在开始战斗时重置 |
| TC02 | BeginDevelopmentTurn 只在活跃战斗允许，先保存本侧Power开始量，再发BeforePlayerSideTurnStart并准备敌人招式；旧回合号>0才查询清格挡，随后AfterBlockCleared与能量重置／AfterEnergyReset，再增加编号、起手抽牌和三个玩家开始阶段／侧开始 | 第一回合保留开始战斗获得的格挡；第二回合开始时格挡先被清空、监听器再补格挡；能量在监听器之前重置 |
| TC03 | BeginDevelopmentTurn 在开始阶段全部结束后置 TurnOpen=true 并把同回合的再次开始拒绝；EndDevelopmentTurn 只在 TurnOpen 时允许，先置TurnOpen=false，再执行当前手牌结束流程与完整敌方阶段；普通开发入口与下一次显式开始分开，正常动作的自动返回按正常Run接口验收 | 未开始回合不能结束；结束后不能重复结束；两次结束之间必须有一个开始；拒绝在守卫处发生且无状态变化 |
| TC04 | Heal(owner, amount) 增加 Hp 并按 MaxHp 截断；不处理复活、治疗修正或死亡 | 满血回复不变；超过上限只到 MaxHp |
| TC05 | GainEnergy(owner, amount) 增加 Energy，可超过 MaxEnergy；回合开始的重置仍按 MaxEnergy | Lantern 在 3 能量上限上得到 4 |
| TC06 | 四个模板按持有者、DefinitionId、IsMelted 过滤，只调用本批资源操作；HappyFlower 用实例 Counter，跨战斗保留 | 无遗物／已移除／熔化／别的持有者不触发；两件同时持有各自触发一次 |
| TC07 | 公开轨迹用同一 Host 从合法动作执行开始战斗与两个回合，观察 Hp／Energy／TurnNumber／Block 与合法候选 | 仅Lantern／HornCleat的真实stdio上，第一回合4能量0格挡、第二回合3能量14格挡、第三回合3能量0格挡 |
| TC08 | 内容文件仅 use 模板参数；源资格、ID、hash、池和 297 项来源条目总数不变；不新增 C# 效果分支 | catalog 只新增四个非空 definition；公开目录新增四个真实 ID |

字段与资源过程放 `combat`，回合过程与事件放 `combat/lifecycle.spire`，四个模板放 `relics/rules`，具体配置各放 `relics/shared` 单文件。开发分步动作显式命名`BeginDevelopmentTurn`／`EndDevelopmentTurn`，完整连续推进使用`EndTurn`；两类入口按各自资格公开。

## 正常结束回合与开发分步

完整`EndTurn(owner)`由DSL在同一调用栈中执行玩家收尾、敌方开始／行动／结束，以及下一普通玩家回合的开始。Host只提交一个公开动作，不补发开始动作。正常Run和真实事件子战斗公开这个完整动作；明确的开发配置环境另保留`BeginDevelopmentTurn`／`EndDevelopmentTurn`动作，以便受控组件分步观察。正常候选不暴露开发分步动作，强行提交须在状态与随机变化前拒绝。

玩家输入回合在开始阶段全部完成并确认仍可继续后开放；结束动作先关闭该输入资格再执行收尾。内部自动出牌与玩家手动候选分别判断，不能让开始／结束阶段的合法自动效果依赖手动输入已开放。完整结束入口只在有效且可结束的玩家回合接受；开发分步过程仍可供DSL内部编排，不能将动作可见性守卫误用于正常初始化。

玩家收尾或敌方过程中确认胜负时，完成所属效果与当前允许的通知后进入真实结算终点，不开始额外玩家回合。敌方结束后仅在当前战斗仍进行、尚未开放另一玩家回合且未进入ending时调用普通开始过程。起手／开始监听导致胜负，也不能重新开放手动候选。完整ending门禁与一次性结算继续遵循所属战斗结束约定，本页不把从结束动作到下一玩家回合的推进等同于全部终局行为完成。

选择暂停保留同一执行帧、阶段与游标；有效答案只续接一次，重复请求缓存或过期观察不得重做收尾、敌人行动、选招、能量重置或抽牌。暂停期间仅发布真实决策；不能以连续提交额外公开动作模拟续接。`NormalDslRun`的结束动作映射直接指向完整`EndTurn`，移除额外`Step(BeginDevelopmentTurn)`；客户端、固定正常路径与诊断的稳定边界同步迁移，不保留旧正常动作兼容入口。

先核对当前已加载内容的真实额外回合消费者；未迁移时保持明确边界，不添加空Hook或将多调用一次普通开始视作额外回合支持。验证保留原四路径、seed、决策及11份suite，并包含一次结束后的公开手牌／能量／回合号／招式／RNG、阶段末杀与死亡、开始／结束阶段选择续接，以及真实stdio请求重放／非法动作原子性。组件与正常路径分别执行；随机／事件维度仍缺时M01继续严格非零。

## 开始阶段、成员与资格

在BeforeSideTurnStart之前，将当前侧成员已有Power的Stacks复制到隐藏AmountAtTurnStart；该实例随后改量不改写快照，新Power在下一次该侧开始前保持初值0。移除与跨战通过真实Power寿命清理。首回合保留格挡已有源依据；不跳过其后的清挡通知。

换边时对当前敌侧成员清除SpawnedThisTurn。敌方开始按源顺序先发BeforeEnemySideTurnStart，逐个查询／清格挡或通知阻止者，全体完成后再逐个发AfterEnemyBlockCleared，最后AfterEnemySideTurnStart。每轮采用当前侧成员；在BeforeSideTurnStart之后新增的Power不补做已经结束的开始量快照。

敌方行动循环在AfterEnemySideTurnStart之后按槽位顺序查询一次，复用Engine已有forEach名单快照与续接游标；依次复核在场、存活和SpawnedThisTurn，后加入名单的敌人不追溯参与。执行中已确认死亡者跳过；活体从已保存名单直接detach、已死亡但不移除、召唤／复活等尚无正式消费者的路径仍不声明完成，不能推断原生会安静跳过失效引用。暂停保留原名单与游标，不另建重复执行器或名单实体。支持的玩家→敌方、敌方→玩家换边均更新出生资格标志；正常EndTurn自动进入下一回合独立验收。

## 清格挡查询和阻止者

ShouldClearBlock是只读资格查询，输入区分player／enemy且恰有一个目标。实际清挡通过隐藏BlockClearCheck包装纯资格判定，以Direct顺序记录首个否决的有效Power或遗物。后续判定不覆盖首个阻止者，查询阶段只可记录本次上下文，不得执行玩法、抽随机或请求选择。被阻止时仅向该实例发布AfterPreventingBlockClear，执行前重新核对有效性；不继续清零。之后无论清除、保留或首回合跳过，均发布对应AfterBlockCleared。Barricade按真实持有者同时支持玩家与敌人。

## 双方格挡命令

GainBlock（玩家卡牌／招式）、GainEnemyBlock（怪物招式）、GainUnpoweredBlock及GainEnemyUnpoweredBlock（无加成且无Move）调用同一GainCombatBlock；该通用命令也保留Unpowered|Move标志组合。输入保留目标、整数请求量、Unpowered／Move标志、真实card／play；目标与来源不从嵌套时可变化的全局字段猜测。

入口在ending或目标死亡时拒绝。被接受的命令依次BeforeBlockGained、加法、乘法、非负截断、AfterModifyingBlockAmount、提交格挡、AfterBlockGained；已经进入的命令不因Before回调造成ending而统一取消。零量、分数小于1以及已到上限的请求仍保留各通知。AfterModifying只分派实际参与且仍有效的修正者，一实例只通知一次。

当前已支持Dexterity整数加法、Frail的0.75与Unmovable的2倍用百万分之一整数表示中间量（AmountMicros）；全部修正后才对状态取整，格挡上限为源999999999。通知保留未截断中间量，而不是实得状态差。纯CalculateBlockAmount返回同一单位，可供只读预览／候选使用；不能增加缓存实体或消费随机。新精度需求超出该已支持数值集合时应先扩展约定与判别。

Dexterity按源card所有者或无card时的实际目标判断，Unpowered不参与；Frail仅对目标的powered Move生效；Unmovable排除怪物目标，但对Unpowered|Move仍生效。锁定Entrench用于工具组件判别该组合，不登记为正式卡牌，也不增加无当前消费者的专用包装操作。正的modified量且存在cardPlay和Move时记入该play的格挡历史，不能仅以实际增加了整数格挡或是否Unpowered判断。Juggernaut读取正的未截断通知量，因此0.75即使没有增加整数格挡也能触发；其正式持有者范围保持已有玩家消费者。

Plating的敌方消费者在第一轮玩家开始前获得无加成格挡；本侧Early末阶段再次获得当前量，本侧开始除首轮外按源减少。Frail／Dexterity不修正它的Unpowered请求。

## 验证

以固定源和实际独立执行分别验证首回合、普通清挡、首阻止者及回调顺序、两敌人先清完再通知／行动、Power开始量与迟加Power、行动名单／出生资格和暂停续接。双方真实怪物动作与直接命令分别覆盖Frail、正负Dexterity、无加成Plating、Entrench的Move历史、分数通知、零量和上限，以及纯预览的完整状态／版本／随机不变。

保留原回合组件、敌人组件、正常四路径、公开stdio、原11份suite和严格非零。工具专用监听与受控新成员只证明本页阶段／资格，不登记未迁移内容或宣称正常召唤、额外回合和整局完成。验证命令与实际结果在PR／CI维护。
