# sts2-sim 与 base 的流程和扩展接口对照 — 2026-10-12

> 2026-10-19 整理：过时／撤销材料：本文不作为当前设计、接手或验收入口。原时点内容留作历史追溯。 当前任务见[主清单](../tofix/README.md)，后续架构见[内容与行为重构目标](../../design/mod-the-spire/content-behavior-refactor-goals.md)。


本地 sim 的主要优势是内容作者面前有完整的游戏领域接口：普通事件写选项并调用共享命令，通用流程承担进入、选择、奖励、退出和跨幕。base 的卡牌/怪物发现与出牌共享管线已有相似分工，但房间/事件仍让作者处理较多IR、守卫与状态寿命。上轮的模块注册解决了横向放置问题，还没有完全解决写一个内容需要掌握多少公共规则的问题。

这是源码组织审阅，没有测量两边开发耗时，不据此声称加速倍数，也没有验证 sim 的整局保真。本次不修改生产。当前任务、认领与接续只看[主清单](../tofix/README.md#env-t09)。

## 输入与证据范围

本地`reference/sts2-sim`的HEAD是`2cdaea3b6dbc20cfa340662fa73640f20bb6fb00`，工作树存在较多未提交修改；本文比较实际工作树，不能归因于该提交的干净版本或上游最新版本。[HEAD、状态及21份关键源码SHA/行数](../../../.audit/sim-architecture-comparison-20261012)保留本次读取依据。

base的已验收起点为[横向扩展接口交付](../records/2026-10-12/horizontal-content-extension-2026-10-12.md)。A正在实现的`UnknownRoomWiring`、`EventVisitLifecycle`、`SunkenStatueEvent`仅作为开发中结构样本，本次没有运行或验收它们。读取前后源码可能继续变化，具体位置以证据SHA和方法名定位。

## 1. sim 如何组织整局

| 层 | 入口 | 拥有的责任 |
| --- | --- | --- |
| 内容发现 | [ContentRegistry](../../../reference/sts2-sim/src/Sts2Sim.Core/Content/ContentRegistry.cs) | 扫描本程序集中的具体内容模型子类，按FullName排序；并非逐卡/事件手写注册 |
| 模板注册 | [ModelDb](../../../reference/sts2-sim/src/Sts2Sim.Core/Models/ModelDb.cs)、[RunReplayDemo.EnsureModelsRegistered](../../../reference/sts2-sim/src/Sts2Sim.Core/Demo/RunReplayDemo.cs) | 以ModelId登记canonical模板，运行时再MutableClone；实际入口传`ContentRegistry.AllTypes`，ModelDb旧注释的“显式列表”不能覆盖这条调用事实 |
| 池与资格 | [ActDefinition](../../../reference/sts2-sim/src/Sts2Sim.Core/Content/ActDefinition.cs)、[IroncladCardPool](../../../reference/sts2-sim/src/Sts2Sim.Core/Models/CardPools/IroncladCardPool.cs) | 源池顺序、普通/精英/Boss/事件范围和资格独立维护；自动注册不会自动改变池成员 |
| 整局状态与转换 | [RunState](../../../reference/sts2-sim/src/Sts2Sim.Core/Runs/RunState.cs) | 幕、地图、预生成队列、访问计数、事件历史和房间栈；`AdvanceToNextAct`统一重置本幕状态并切换地图 |
| 房型判定与创建 | [RoomFactory](../../../reference/sts2-sim/src/Sts2Sim.Core/Rooms/RoomFactory.cs) | 从地图类型判定实际房型；Unknown赔率/黑名单独立于具体事件；房间类提供统一结构 |
| 房间生命周期 | [AbstractRoom](../../../reference/sts2-sim/src/Sts2Sim.Core/Rooms/AbstractRoom.cs)、[EventRoom](../../../reference/sts2-sim/src/Sts2Sim.Core/Rooms/EventRoom.cs) | Before/After进入hook、EnterInternal、Exit；EventRoom绑定owner并开始事件，退出负责清理 |
| 选择驱动 | [RunDriver](../../../reference/sts2-sim/src/Sts2Sim.Core/Runs/RunDriver.cs)、[IRunDecisionSource](../../../reference/sts2-sim/src/Sts2Sim.Core/Runs/IRunDecisionSource.cs) | 请求地图/战斗/奖励/商店/营火/事件选择，验证返回值在当前候选中，推进对应通用流程 |
| 具体事件 | [EventModel](../../../reference/sts2-sim/src/Sts2Sim.Core/Models/EventModel.cs)、[SunkenStatue](../../../reference/sts2-sim/src/Sts2Sim.Core/Models/Events/SunkenStatue.cs) | CalculateVars、初始选项和选项效果；普通事件不自行编排地图旅行和下一幕 |
| 领域命令 | [PlayerCmd](../../../reference/sts2-sim/src/Sts2Sim.Core/Commands/PlayerCmd.cs)、[RelicCmd](../../../reference/sts2-sim/src/Sts2Sim.Core/Commands/RelicCmd.cs)、[CardPileCmd](../../../reference/sts2-sim/src/Sts2Sim.Core/Commands/CardPileCmd.cs) | 包含资源修改、适用hook、获得/移除和实例处理等完整操作，不只是字段赋值别名 |

RunDriver的普通路径可直接追踪为：选择可旅行节点 → ResolveRoomType → CreateRoom → PushRoom/Enter → 通用房间选择循环 → Exit/Pop → 检查死亡或Boss → AdvanceToNextAct/最终事件。具体普通事件并不修改这条链。

事件内部另有统一循环：读取CurrentOptions → 请求选项 → ChooseOption → 清理待领取奖励；遇到强制战斗则驱动战斗后ResumeAfterForcedCombat，直至IsFinished。这让增加一个使用已有选项/命令的事件主要落在自身文件和池资格上。

## 2. 同一个事件暴露的差别

sim的`SunkenStatue`提供两个选项：获得SwordOfStone，或获得随机金币并受到7点伤害。内容方法调用`RelicCmd.Obtain`、`PlayerCmd.GainGold`、`CreatureCmd.Damage`和`Finish`。例如`PlayerCmd.GainGold`本身包含ModifyGoldGained和AfterGoldGained；`RelicCmd.Obtain`包含克隆、owner、持有实例、从袋中移除和AfterObtained。因此叶内容无需再接这些环节。

我们已验收的[GoldEvent接口例子](../../examples/base-events/GoldEvent.cs)仍需声明ActionBody、Role、Requires、字段读写和Close；开发中的[SunkenStatueEvent](../../../tools/base-migration-baseline/BaseSpire/Rooms/Events/SunkenStatueEvent.cs)也直接生成金币/生命写入IR，其遗物选项和脱战死亡明确仍有边界。这里不能用两边行数判断谁更快：注释和实现范围不同，A的文件也尚未验收。真正的结构差别是“调用完整领域操作”与“事件作者拼接状态读写、守卫和收尾”。

这会影响扩展成本：相同的金币hook、遗物获得、永久牌组变化或脱战死亡，如果没有一个完整共享入口，作者每次都要重新确认该接什么；评审者也要重新检查是否遗漏。注册目录不会自动解决这些问题。

## 3. 对照我们已有的改善与剩余负担

| 扩展情形 | sim | base | 判断 |
| --- | --- | --- | --- |
| 使用已有能力的新卡 | CardModel.OnPlay调用命令，基类承担出牌流程 | 独立卡DSL，SpireCardSource自动加载，CardPlayPipeline/效果编译器承担共享流程 | base已有明确内容入口；简单数值/组合卡甚至可少写C#，不应笼统归咎于DSL |
| 使用已有能力的新怪物 | MonsterModel定义行动与状态机，自动扫描注册 | 独立enemies文件，前端降级为状态机并统一编译 | 注册已横向化；新行为仍需共享能力支持，两边都有这一层成本 |
| 普通新事件 | EventModel选项+Commands+Finish，驱动器处理访问 | IEventRoomModule可分文件，但动作守卫、字段读写和部分资源收尾仍由作者生成 | 这是当前应优先减负的地方；分文件已经成立，作者接口还偏底层 |
| 真正的新房间类别 | 新房间类，并修改RoomFactory与RunDriver/RunEngine的类别分派；相关观察/策略可能接续 | 新入口处理器/房间服务及公开声明/消费者适配 | 两边都需系统接入，不能把“新增事件”与“新增房间类别”混为一谈 |
| 特殊事件交互 | CustomEventDecisionPolicy等按具体事件分派 | 通用公开候选可表达动作，策略仍需识别新语义 | sim也不是全部内容零接线；我们的候选绑定/公开边界有保留价值 |
| 幕转换/嵌套事件战斗 | RunState/RunDriver有统一方法与房间栈 | 幕转换还未实现，访问phase和奖励/战斗续接分布在多个模块 | 需要整局生命周期契约，而非继续增加散落phase写入 |

base从[RuntimeFactory](../../../tools/base-migration-baseline/BaseSpire/SinglePlayer/BaseSpireRuntimeFactory.cs)构建规则、由[Composer](../../../tools/base-migration-baseline/BaseSpire/Composition/CombatPackageComposer.cs)安排编译顺序，再由Engine Session执行。编译路径比直接执行C#方法多一层是事实，但这不要求内容作者理解所有Bindings、局部槽位和声明顺序；这些应由编译模块主责处理。

[RewardCompilation](../../../tools/base-migration-baseline/BaseSpire/Rewards/Compilation/RewardCompilation.cs)仍因原声明顺序补入卡变量并创建奖励、挂Boss关闭回调；[RoomCompilation](../../../tools/base-migration-baseline/BaseSpire/Rooms/Compilation/RoomCompilation.cs)同时组合地图、初始化、库存与事件服务。拆分降低了搜索范围，但这些接缝尚未全部形成内容作者无需关心的完整能力契约。

[公开运行器](../../../net-the-spire/client/run_game.py)按MapPhase、动作类别和房型支持集合选择策略。这不是客户端重算游戏规则，但新增决策形态仍需要维护该识别层；Engine暂停/恢复保持唯一权威，不再另造客户端Run调度器。

## 4. 不应照搬的部分

1. sim以可变C#对象和async方法直接执行玩法；base以编译期模型生成IR，运行状态属于Engine。借鉴领域接口和生命周期职责，不能把sim RunState、房间对象状态或Task调度再塞进base形成第二套运行态。
2. ContentRegistry只自动扫描自身程序集；池仍显式维护。ModelDb.Inject支持注入类型，但不等于完整的mod依赖、命名空间与加载顺序框架。base既有包身份和内容命名空间不应为模仿扫描机制而放弃。
3. RunDriver约1070行，RunEngine约899行，均包含整局、房间和事件推进分支；大型共享文件与重复编排客观存在。sim更容易扩展普通内容，原因是这些复杂度主要集中在公共基础层，不是项目完全没有大文件。
4. 普通EventOption是通用的，但CrystalSphere/FakeMerchant等特殊交互在[CustomEventDecisionPolicy](../../../reference/sts2-sim/src/Sts2Sim.Core/Runs/CustomEventDecisionPolicy.cs)仍有具体类型switch。其源码结构也不能替代原版一致性验证。

## 5. 建议如何改善开发效率

下面是本次建议，尚未实施，不改变已经派发的A/B/C工作包。

1. **先完成正在生产的真实问号访问。** 用当前源队列和具体事件把进入、选项、资源提交、关闭走通；记录作者真正缺的共享操作。不要在流程未通前设计全事件DSL或全新插件框架。
2. **从该事件提取房间/事件可用的领域命令。** 优先金币获得/扣除、脱战伤害/治疗与死亡、长期牌获得/删除/升级、奖励提出与关闭、遗物获得。命令生成Engine IR，包含相应hook、实例/池处理和时序；接口传递有类型能力，不把全包Bindings塞给事件作者。一次实现后让事件、商店和奖励实际复用。
3. **统一事件选项编译。** 普通内容提供身份、初始化、选项资格和效果序列；共享编译器生成Role、当前事件守卫、候选/选择绑定、重复选择拒绝与结束指令。新增普通事件应主要编辑事件文件和明确的池/组合登记，不触及MapRoomWiring、Composer、net逐事件表。
4. **在T10建立整局转换的唯一归属。** 进入访问、等待选择、关闭奖励、离开访问、恢复事件战斗、幕完成与下一幕分别有具名过程；房间叶请求转换，不直接设置其他模块的phase。状态仍在Engine实体/字段/集合中，实现顺序按原版hook/随机生命周期验证。
5. **收敛所有入口的运行依赖装配。** 规则集、所需compute后端、随机布局和公开元数据由同一内容运行定义提供。当前正式工厂已有多数能力，旧切片入口绕过装配导致三项缺后端，是需要消除的接缝；C正在收口，不另建一套Factory。

验证减负应看真实修改路径：新增第二个普通事件是否只写本文件/池登记；是否不用读取整个Composer；是否不需要重新拼phase/金币hook/死亡/牌组提交；同构建能否从正式入口正常选择并继续游戏。源码行数和文件数量仅作定位，不能代替这些可操作判据，也不新增token统计。

本轮未构建或运行规则/参考套件，因为生产只读；文档检查和diff-check的实际结果留[本次audit](../../../.audit/sim-architecture-comparison-20261012)。
