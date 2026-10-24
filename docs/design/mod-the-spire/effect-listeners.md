# 遗物与Power的通知和修正监听

本约定对应DSL-EFFECT-LISTENERS-01，统一当前纯DSL包中已有遗物与Power行为。当前任务见[GitHub 任务清单](https://github.com/QingXiang22/auto-the-spire/issues/28)。不扩大声明支持的内容范围，不宣称正常Run或独立原版比较通过。

## 职责与执行

主流程只定义并发布领域通知或计算阶段，不能读取遗物专属能力字段，也不直接调用Power家族的效果过程。遗物与Power使用同一监听模型：实例保存标识／持有者／寿命，规则监听通知或修正，根据自身状态响应。玩法仍全部在base DSL，core不识别任何游戏内容或阶段。

通知沿现有事件栈执行，允许嵌套、原生决策暂停和续接。base订阅用instances声明有效实例，混合监听采用相同priority，再用instance_order表达源顺序；core在入口固定候选与排序键，执行前重新检查资格。新增实例不追溯参与已开始的同一通知，移除后不得执行。已经进入的快照不会仅因中途胜负已定就统一取消，具体命令按自身源规则决定是否执行。

## 源时点与监听顺序

依据锁定参考的RunState.IterateHookListeners、CombatState.IterateHookListeners与Hook各站点，不能把Power0／遗物100作为全部事件的顺序。combat/hook-order中的PowerHookOrder、RelicHookOrder、CardHookOrder及EnemyHookOrder统一计算键，direct参数选择下表两种路径。同一持有者保留实际实例／牌堆顺序，敌方分组使用当前Enemy.SpawnOrder；出生与槽位分离、条件选招时点仍按敌人领域的支持边界处理。

| 源路径 | 顺序 | 当前对应事件 |
| --- | --- | --- |
| Run包装，direct=false | 遗物库存，再玩家Power与有序战斗牌区，再敌人的Power及模型 | 回合阶段、抽牌Early／普通、耗尽、伤害接收、HP变化、Power修正及通常的计算监听 |
| Direct生物路径，direct=true | 玩家Power、该玩家库存与牌区，再敌人的Power及模型 | AfterCardPlayed、ModifyCardKeyword、ShouldClearBlock、EnemyAddedToCombat；源中尚未接入的格挡击破／伤害给予等站点仍须逐项恢复 |

玩家回合的编号在AfterEnergyReset后增加。AfterBlockCleared看到的是上一编号：第一回合为0；HornCleat／CaptainsWheel等依源值判断，不能将AfterSideTurnStart提前到起手抽牌前。

| 顺序 | DSL站点与源调用 |
| --- | --- |
| 玩家开始 | BeforePlayerSideTurnStart（Hook.BeforeSideTurnStart）→清格挡／AfterBlockCleared→ResetTurnEnergy／AfterEnergyReset→编号增加／BeforeHandDraw→起手抽牌→AfterPlayerTurnStartEarly／普通／Late→AfterPlayerSideTurnStart |
| 玩家结束 | AfterAutoPostPlayPhaseEntered→BeforePlayerSideTurnEndVeryEarly／Early／普通→回合末手牌效果及Flush→AfterPlayerSideTurnEnd |
| 敌方开始／结束 | BeforeEnemySideTurnStart→清本侧格挡／AfterEnemyBlockCleared→AfterEnemySideTurnStart→逐个敌人行动→BeforeEnemySideTurnEndVeryEarly／Early／普通→AfterEnemySideTurnEnd |

每个外层阶段按源检查ending，已经选中的通知或父效果内部不使用统一中断。Draw与GainBlock各自拒绝ending操作，Feed等其他余效仍保留各自规则；DrawBlocked仍来自ShouldDraw资格，不混入Draw命令的ending限制。普通开发空靶场和脱战调试操作仍按其明确范围运行。

Orichalcum在BeforePlayerSideTurnStart清理实例资格，在BeforePlayerSideTurnEndEarly记录Block为0，在普通阶段先清标志再得6格挡。Plating的5格挡位于Early；同一源迭代中库存先于Power，因此两者初始Block0时共得11。移除、熔化与同定义实例分别校验，跨战的新回合重新清理资格。StoneCalendar同样按实例在AfterPlayerSideTurnStart保存第七回合资格，普通侧回合末先清标志再兑现。

修正监听必须pure且返回与累积值相同的类型，不能写实体、消费随机、发布事件或请求决策。foldEvent以事件载荷首项为初值，依priority／instance_order／声明顺序调用纯监听者，每次把返回值作为下一监听者首参；没有监听者返回初值。签名、返回类型及纯性在链接时验证，执行时缺返回值也明确失败。通知和修正共用实例快照与有效性检查，各实例收到上下文与当前累计值。计算阶段明确加法／乘法／最终取整，不能把临时实体当预览缓存；观察、候选和费用查询不得改变实体／随机快照。

通知与修正分开：Before／After通知产生后续行为，Should／Modify修正决定当前计算。伤害拥有者、牌源、攻击者、目标显式传递，嵌套上下文不借全局字段猜测。保留现有开发范围及资源上限，所有旧内容实际行为执行结果继续执行。

Power接收修正需要记录实际参与者，以便计算后执行其副作用，使用[Power改量约定](../../development/base-spire/reference/powers.md#新建叠加与直接改量的阶段)。PowerAmountReceiving是计算包装通知：实例过程调用pure判定／计算，只写本次隐藏上下文与参与者记录，不在此阶段消费Power、发玩法通知或请求决策。Artifact的判断是pure过程；扣层在AfterModifyingPowerAmountReceived通知执行，资格同时要求本次记录与当前实例仍有效。该流程仍使用相同Session事件栈，不把pure的foldEvent改为可写操作；测试中的暂停专用于验证通用续接，不作为生产修正者的编写方式。

## 迁移和判别

移除Relic的StrikeDamageBonus、RetainEnergy、FirstTurnDrawBonus、RestHealBonus等效果配置字段，由具体监听规则贡献。ArtOfWar等私有状态移入对应附属实体，移除时清理、下战重置，不能污染其他遗物。Power的修正和反应从战斗主过程搬到Power监听模板；层数叠加、临时寿命及独立计数仍在实例生命周期中维护。

最小core检查区分有序折叠／空监听、纯性拒绝／类型不符、嵌套调用和预算；base检查反转获得顺序后按实例执行、执行中移除后续监听者、嵌套自动出牌及决策续接、重复查询不写状态／不消费随机，以及现有遗物、伤害／格挡、费用／能量、抽牌和Power寿命回归。公开stdio仍经相同工厂和规则，不加入Host补偿。
