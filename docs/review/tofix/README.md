# 当前任务清单

这页按游戏领域整理要实现的东西，再说明依赖和推进顺序。整局通关是各领域集成后的验收目标；ENV编号用于追溯原合同和证据，不再作为日常浏览与派单的主分类。

2026-10-17按用户要求重新规划，并依据当前workflow-b审阅修订；用户随后指令继续推进，Codex `/root`按当前交付接续Sown＋SapphireSeed，暂停项保持。**部分交付、待验证、整体已验收分别记录**；计划修订不代表新增玩法验收。

[领域任务](#当前任务) · [当前交付](#当前交付) · [遗物分组](#遗物分组任务) · [归属与依赖](#归属与依赖) · [推进顺序](#推进顺序) · [每批集成](#每批集成与重排条件) · [原任务合同](#任务详情) · [完整环境范围](#完整环境的验收范围)

## 当前目标

目标是同版本原版的五角色完整单人环境。2026-10-18用户已将当前顺序改为先完成 **CORE-AUTHOR-05：按规格驱动完成纯DSL base目录与切换**，允许未迁移玩法暂时报未支持。随后继续 **M01：从公开接口正常开局，完成地图→战斗→奖励→返回地图，并与独立参考逐步比较**；原完整范围与下表阶段验收不删减。已有旧路径的第一幕与局部证据，不能据此认定新DSL环境已具备对应能力。

**实施主线**：2026-10-18用户最新要求优先补全base-spire内容，当前接续全遗物，按共享生命周期成组实现并逐件保留行为见证；只补内容所需公共能力。正常Run集成继续保留为后续验收目标，不据内容定义数量关闭Ironclad首幕、跨幕或完整胜负。Feed、药水、随机行为和永久写回的M01原条件保留。

| 阶段 | 要交付什么 | 什么时候可以结束 |
| --- | --- | --- |
| M01：当前阶段 | 正常公开流程与独立比较 | ENV-T01～05 按同一构建、配置和 seed 验收；实际执行正常奖励与永久状态比较 |
| 开发开局首幕：中间检查 | 复用现有dev配置，正常打败首幕Boss并关闭奖励 | 明确不包含Neow；不得据此关闭正常开局或完整首幕合同 |
| 正常开局首幕 | 包含Neow选项与效果，再完成首幕Boss奖励关闭 | 无初态注入／跳过，Neow结果进入后续战斗，独立比较及公开全程有证据 |
| M02 | 完整 Ironclad 单人四幕胜负 | 战斗、房间、长期状态、随机顺序、固定种子与公开全程均有证据 |
| M03 | 五角色、全部支持进阶与完整单人内容 | 每个角色正常多幕胜负，内容、变体、来源与各层验证完整 |
| M04 | 可供后续 AI 使用的环境候选 | 最终 120 对矩阵、覆盖、差异裁决、公开轨迹和 Release 成本通过审查 |

**切换前历史可运行边界（当前纯 DSL 未恢复）**：原seed已连续完成七场战斗、Aroma、宝箱、营火和SlipperyBridge，经过138个稳定边界到达地图节点(0,12)的PhrogParasite精英入口；此前路径与独立参考一致，公开Host重放到同一边界。继续该路径依赖仍暂缓的敌人召唤机制。另一固定策略已走到真实死亡并完成双侧与公开闭环；首幕完成目标仍未通过，不能据此前缀关闭M01/M02。见[连续Run集成交付](../records/2026-10-17/ironclad-run-integration-2026-10-17.md)。

**切换前跨幕证据（当前纯 DSL 未恢复）**：显式`dev-run`已接通默认三幕预生成、地图和切幕生命周期，三组seed的九张地图/队列/RNG与原生参考一致，受控公开切幕通过。正常原seed现已越过TabletOfTruth五页至真实死亡：39边界双侧一致，同计划55次公开提交通过。选择回复分支则推进137次至BygoneEffigyElite缺敌人行为；另两seed仍分别缺WoodCarvings效果、Cheese公开二选应答（规则层Gorge已对照通过）。单幕身份因事件内容增加已迁移；后续古代人、TheArchitect与完整四幕仍未交付。见[跨幕交付](../records/2026-10-17/default-act-transition-2026-10-17.md)与[第一幕事件接续](../records/2026-10-17/act1-event-continuation-2026-10-17.md)。

**领域交付补充**：事件层已有独立访问入口及JungleMazeAdventure、MorphicGrove完整选项，相关共享多张选择可复用；Jungle独行保留具名参考RNG差异。见[事件层记录](../records/2026-10-17/event-layer-batch-2026-10-17.md)。后续按[推进顺序](#推进顺序)集中交付，不按固定seed首错来回切换领域；PhrogParasite的召唤暂停继续有效。

## 当前任务

下面是工作分类的唯一总览。每个具体事项只选一个主要归属，其他类别记录依赖；同一类可以分批交付，不要求一次补齐全类。部分交付只说明已有可复用能力，完整范围仍待验证。A负责base玩法，B负责独立比较，C负责公开出口；角色分工不构成自动派出agent的授权。

### 游戏实现分类

| 类别 | 负责什么 | 已有基础与主要剩余工作 | 原合同索引 |
| --- | --- | --- | --- |
| **卡牌本身** | 定义、费用、目标、普通／升级效果、牌池和单人资格；状态／诅咒／衍生牌也归此类 | 纯 DSL 单人 Ironclad 内容与共享机制按用户 2026-10-18 指令接续，实际进度见本页当前交付；其他角色批量补卡仍暂缓 | [卡牌与角色](#env-t12)、[完整覆盖](#env-t13) |
| **卡牌附魔与附着** | Enchant／Afflict身份、资格、叠加／替换／移除、动态关键词、升级／复制／战斗副本保留及失效 | 首批提案限定Sown＋SapphireSeed，范围与验收只维护在[当前交付](#当前交付)。Slither、SelfHelpBook及其他附着后续安排，不先做全量统一框架 | [机制PR04](#env-t06)、[永久牌组PR01](#env-t09) |
| **怪物本身** | 普通／精英／Boss定义、血量、开场、意图、行动状态机、编队和专属行为 | 已有部分怪物和遭遇队列；补BygoneEffigy等行为、其余精英／Boss。召唤／复活依赖仍暂缓 | [随机行为](#env-t01)、[遭遇内容](#env-t08)、[行动控制PR16](#env-t06) |
| **共享战斗与资源机制** | 跨内容复用的Power、伤害／格挡／Fatal／死亡、出牌／费用／选择、生成／牌区／历史及长期资源操作 | 基础已交付；补来源、修正参与者、寿命和写回。只拥有PR01／02／03／05／06／07／17；附魔、怪物控制与角色专属操作归各自领域 | [共享机制](#env-t06)、[选牌PR01／资源PR04](#env-t09)、[写回](#env-t02)、[文本规则库](#env-t07) |
| **问号房里的事件** | 事件资格、进入初始化、页面／选项、效果顺序、结束和事件战斗挂起／返回 | 已有7个注册模块和独立访问入口，部分模块有未完成分支；补其余事件及资格，调用附魔／奖励／物品能力。Jungle独行参考RNG差异仍开放 | [事件PR05](#env-t09)、[事件层证据](../records/2026-10-17/event-layer-batch-2026-10-17.md) |
| **地图与整局推进** | 开局、地图生成、选点、问号房型判定、遭遇／事件队列生成消费、房间进入退出、跨幕、胜负和长期状态延续 | 默认三幕结构和受控切幕已交付；补正常首幕／跨幕／终局闭环。保持概率、队列和内容效果解耦 | [地图往返](#env-t05)、[初始化](#env-t08)、[跨幕与终局](#env-t10)、[写回](#env-t02) |
| **商店** | 货架、价格／折扣、购买、删牌、库存／支付与离店 | 基础已交付；核对完整商品资格、真实修正参与者、购买结果和公开完整流程。商品持有效果调用物品域 | [房间T09.2](#env-t09) |
| **火堆／营火** | 休息、锻造、遗物增加或限制的选项、资格和离场 | 休息／升级已有交付；补实际遗物参与者与扩展选项，不重做基础休息 | [房间T09.1](#env-t09) |
| **先古之民** | Neow和后续Ancient的选项、专属奖励、持续效果及进入新幕参与者 | 库存身份和后续幕起点已接线，具体效果未完整实现。Neow必须在正常开局首幕验收前接通；后续Ancient在对应幕连续运行前接通 | [房间T09.4](#env-t09)、[跨幕](#env-t10) |
| **遗物与药水** | 持有实例、获得／移除／替换、计数、触发与修正；药水生成／容量／使用／自动触发 | 部分遗物、药水使用与满栏流程已交付；补SwordOfStone、ChosenCheese等生命周期、动态容量和适用参与者 | [遗物PR02／药水PR03](#env-t09) |
| **奖励与宝箱** | 战斗／事件奖励容器、候选生成、领取／跳过、满容量保留、未选清理、开箱与结算 | 基础战斗／精英奖励、宝箱、Cheese规则层二选已有交付；补通用自定义奖励和完整生命周期。物品效果不在此重复实现 | [奖励往返](#env-t05)、[房间T09.3／PR05](#env-t09)、[生成PR05](#env-t06) |
| **角色与进阶** | 各角色开局、专属资源／机制接入、解锁、进阶差异和整局验证 | 有星星／Forge／球／Poison／Doom／Osty等基础；其余角色正式开局与完整内容仍缺，批量实现保持暂缓 | [角色](#env-t12)、[进阶覆盖](#env-t13)、[专属机制PR09～14](#env-t06) |

### 配套工程分类

| 类别 | 当前范围与状态 | 原合同索引 |
| --- | --- | --- |
| **公开接口与客户端** | 既有公开流程部分交付；补真实多选应答、所有房型／内容的公开事实和合法候选。随各领域交付验证，不能无限后置 | [公开出口](#env-t03)、[暂停期API](#core-legal-pending) |
| **独立对拍与环境验收** | 复用严格套件及独立事件入口；补参考差异裁决、完整内容映射、Ironclad／五角色整局、120对矩阵和性能证据 | [初始化对拍](#env-t04)、[Ironclad验收](#env-t11)、[覆盖](#env-t13)、[候选](#env-t14)、[真实客户端](#env-t15) |
| **引擎、作者工具与发布维护** | 0.4集成已交付、发布验收开放；CI结果待审阅，scope诊断／暂停期API按原边界，损坏注释随相关改动处理 | [发布](#core-author-04)、[CI](#ci-01)、[诊断](#core-dsl-warn)、[维护](#maint-01) |
| **后续AI与指定mod** | AI／共享Transformer和训练暂缓；指定mod需用户明确范围，其他工作沿原触发条件 | [指定mod](#env-t16)、[W完整范围](#项目长期范围)、[暂停项](#暂缓和另行安排的工作) |

## 当前交付

2026-10-19，Codex `/root` 按用户要求审阅纯 DSL 完成度并重构执行工作流；归属 CORE-AUTHOR-05／CI-01 的负责人维护，不认领新的玩法生产包。基线 `978a0b6`，修改仅限检查工具与工作流／审阅文档。本地已交付检查筛选／计时、去重CI触发及单一当前入口，待本次PR／CI审阅；完成证据和未验证项见[本次审阅](../records/2026-10-19/base-completion-workflow-review-2026-10-19.md)。

同日已完成内容解耦及sts2-sim对照审阅：Codex `/root`核对锁定参考与原版Power职责，并执行五项耦合探针及内容自有模块可行性原型。已确认Power调用方kind、重复卡牌数值、公共私有状态和重复名单问题；现有DSL可承载模块自有状态／监听，但catalog的use-only限制拒绝完整模块。候选调整为“简单内容配模板、复杂内容用完整DSL模块＋共享领域命令”，具体设计与验收见[修订提案](../../design/mod-the-spire/engine-dsl-migration/plan.md#2026-10-19内容模型修订提案)，来源／命令／未验证见上述同一记录。提案未实施、正式作者约束未改；下一生产切片需据此重排，不能认定内容隔离已验收。

| 当前事项 | 已有成果与下一动作 |
| --- | --- |
| 最近生产批：DSL-RELIC-CONTENT-07／RELIC-G05首子包，P1／待验证 | base `0.6.0-dsl.38`；MiniatureCannon／PaperPhrog／PaperKrane已实现，共55件遗物有本地见证。准确语义、来源及本地检查见[批次证据](../records/2026-10-18/relic-stateless-attack-modifiers-2026-10-18.md)。PR #23基线`978a0b6`的四组CI已核实成功；本次工具／流程修改仍需新提交的CI结果 |
| 下一玩法包：RELIC-G05后续，P1／待认领 | 先核对PenNib计数／当前牌身份，明确多段、重复与嵌套；Akabeko／Vigor及MysticLighter／附魔按依赖另分。本次不进入玩法编码 |
| 全遗物与正常环境，未验收 | 来源297件、1件Deprecated排除；241件尚未实现。每件适用性／完整效果／正常来源及独立证据继续收口，不注册空效果。正常Run、G05整体及父项均保持开放 |

Ironclad已有85张单人牌本地见证、两项多人源排除；旧C#的90件遗物及正常路线属于切换前成果，不能加到当前55件中。其他已交批次、接口和来源从[记录索引](../records/README.md)按需读取；不在本节逐批追加版本、测试总数与下一步。全遗物优先、其他角色／召唤复活／训练的暂停保持。

<details>
<summary>切换前 Sown＋SapphireSeed 的未关闭验收合同（历史证据，不是当前派单）</summary>

| 项目 | 本批约定 |
| --- | --- |
| 目标 | 玩家在SapphireSeed选择Eat或Plant后，永久牌组变化正确；Plant附着的Sown在后续战斗按源顺序生效，战斗局部状态不污染永久实例 |
| 状态／主责 | 待验证；Codex `/root`已完成Sown与SapphireSeed实现、本地／受控公开及可比参考检查，本批编码窗口结束。正常路线未到达该事件、非首张Plant参考比较仍缺，不能关闭完整PR04。复用下列证据，不重新实现已交付能力 |
| 主要归属 | 卡牌附魔与附着：ENV-T06-PR04首批；SapphireSeed页面归事件域ENV-T09-PR05，调用共享选牌PR01，作为同批消费者，不另开重复工作包 |
| 纳入 | 仅Sown的身份、层数、Normal／Disabled状态、源CanEnchant资格与拒绝重复附着；SapphireSeed两个选项；永久牌到战斗副本、升级及既有复制路径的附着保留／隔离；必要的公开字段和单选交互 |
| 不纳入 | Slither、Sharp／Nimble／Swift、Afflict、临时Sly、全量附着替换／移除框架、WoodCarvings／SelfHelpBook完整事件。它们保留在原类别及PR04完整合同，未取消 |
| 修改窗口 | base的卡实例配方／复制、附着领域操作、现有出牌流程和SapphireSeed模块；tools复用独立事件比较；net只按实际需要接通用公开语义及检查。先定位现有字段／hook接缝，不新建独立结算器，不改core或参考玩法 |
| 起点与身份 | 复用正式工厂、RunDeck选牌、CardDefinitions的New／Copy／Upgrade、事件目录和独立访问入口；开发验证限定Ironclad／A0／all-unlock／singleplayer，继承当前v0.109配置。验收记录实际构建／规则／内容／参考身份，不硬用旧build常量 |
| 完成证据 | 下列行为场景通过、实际命令和结果齐备；相关严格回归与同构建公开轨迹无新增回归。证据不足保持待验证，不能关闭PR04完整范围或正常首幕里程碑 |
| 实际结果／接续 | 8项定向检查、Slice运行867＋源码1、net422、Engine99、Python12通过；4个实际出牌边界双侧一致，SapphireSeed三seed共9项含目录进入检查通过，3项非首张Plant参考差异使命令rc1。正常探索14条路线均未取得Sown，原首幕完成例继续rc1。见[本批记录](../records/2026-10-17/sown-sapphire-2026-10-17.md)；下一成果为补齐本批正常可达／使用及参考边界证据，不自动转去补怪物或公开多选 |

### 本批行为与验收

1. **Eat**：受伤玩家先回复9、封顶MaxHp，再在真实可升级永久候选中选一张；仅选中实例升级，空域直接完成，单候选自动处理，多候选暂停后继续，结束后回到旅行。不得用升级域代替附魔域。
2. **Plant**：用源EnchantmentModel.CanEnchant构造候选，选择一张后附着Sown 1；同定义其他实例不变，不额外升级、变换或抽随机。无候选沿源完成，有候选时不得强制选首张。回答重放／旧候选拒绝沿公开账本处理，不能重复附着。
3. **进入战斗与出牌**：选中永久牌生成带Sown的战斗副本；先执行牌本体，玩家仍存活才沿源顺序执行附魔，Normal时获得1能量并变Disabled。同一副本再次打出不再次获能；永久牌的附着及状态不被战斗副本反向覆盖。最后击杀和玩家死亡边界复用原出牌／结算契约。
4. **实例寿命**：同定义两张牌仅选中者有附着；永久升级保留附着。既有复制／变换路径按源区分复制状态与新实例，不将战斗副本统一重置来掩盖复制差异；跨两场战斗重新从永久牌建立副本，验证Sown在新战斗可再次生效。临时副本不得写回永久牌。
5. **公开与保护**：公开可见的附魔身份／层数／状态和当前单选候选与Engine一致；选择后返回地图并实际参与后续战斗。保护已有Aroma、Tablet、Cheese和Morphic选择语义及随机位置。受控进入只证明本批行为，正常开局回放的到达范围和停止原因另列。

### 开工前的不确定性与处理

先用一个Sown实例走查“永久牌→战斗副本→出牌→下场副本”，核对CardModel复制、CardCmd.Enchant、Sown.OnPlay及本体现有New／Copy／出牌接缝。产物是一段源时序／状态归属说明和最小可执行判别；能证明现有Engine状态／hook承载后再接事件。不展开全量附魔扫描；若需要修改core或改变公开接口承诺，停下受影响部分，登记具体缺口和方案，按既有权限裁定。

参考的SapphireSeed.Plant自动选首张，不能当作玩家任选实例的正确性依据；已实测其Sown通用MutableClone重置状态，但CreateClone经CloneForCombat恢复状态，后者可实际比较。可比部分使用独立原生执行，非首张选择按只读sts2源场景核验并具名保留差异，不改参考、不自动代选。对应比较保持blocked_compare，不计整批完整通过。

验证已扩展`event-modules --event SapphireSeed`，包含Eat、首张Plant比较及非首张差异；Slice的`--sown-only`验证实例与两战寿命，`sown-combat`核对实际出牌。检查范围和运行命令见本批记录；受控访问不替代正常路线，正常探索失败不替换原固定验收case。

</details>

## 遗物分组任务

本表维护全遗物主线的机制分组及排期；名字为候选示例，完整来源仍以catalog为准。每组按真实源方法进一步拆完整子包，只有参数不同才直接共享模板；已交付的其他内容不重做。设计、风险与G01实施步骤见[开发计划](../../design/mod-the-spire/relic-combat-start/development-plan.md)。共用主责Codex `/root`，未认领组不占生产窗口，其他角色及召唤／复活暂停保持。

| 组／机制 | 候选与合批边界 | 优先级／状态 | 依赖及下一可验成果 |
| --- | --- | --- | --- |
| RELIC-G01 拾取随机永久升级 | Whetstone、WarPaint、SandCastle；类型／张数参数化 | P1／待验证 | CONTENT-04已交付；固定源向量、两战及公开证据见当前交付，待PR／CI审阅；正常来源未验收 |
| RELIC-G02 永久选牌修改 | Pomander、YummyCookie、EmptyCage、PreciseScissors；升级与移除分别子包 | P2／待认领 | 原生永久多选、取消和完整移除；暂停续接不重放拾取 |
| RELIC-G03 卡牌创建修正 | MoltenEgg、ToxicEgg、FrozenEgg；附魔参与者另接依赖 | P2／待认领 | 奖励展示／商店创建／入组前修正与NoHookUpgrades三处完整消费者 |
| RELIC-G04 回合资源与开场效果 | Candelabra、Chandelier、VeryHotCocoa、FakeHappyFlower、Sai；按事件和资源子包 | P1／待验证 | CONTENT-06已交付五件；77项定向／385项包行为／38项公开路径及52件见证，待PR／CI审阅；正常来源未验收 |
| RELIC-G05 攻击与格挡修正 | Akabeko、PenNib、MiniatureCannon、MysticLighter、PaperPhrog、PaperKrane | P1／待认领（后续子包） | CONTENT-07三件已交付，待验证；下一PenNib计数／当前牌，Akabeko／Vigor与MysticLighter／附魔按依赖分包；整组未关闭 |
| RELIC-G06 出牌／牌区反应 | GamePiece、JossPaper、CharonsAshes、ToughBandages、Tingsha、UnceasingTop | P1／待认领 | 缺失弃牌／手空通知先补契约；计数、嵌套抽牌及末杀余效 |
| RELIC-G07 抽牌、留牌与行动限制 | Pocketwatch、Fiddle、RunicPyramid、RingingTriangle、VelvetChoker、SneckoEye | P2／待认领 | 手牌清理／可出牌守卫／随机费用；纯查询与跨回合历史一致 |
| RELIC-G08 受损、阈值与死亡 | RedSkull、SelfFormingClay、DemonTongue、TungstenRod、TheBoot、LizardTail、FragrantMushroom | P2／待认领 | 最终失血与防止死亡顺序；伤害修正和死亡防止分别子包 |
| RELIC-G09 药水库存与触发 | PotionBelt、AlchemicalCoffer、PhialHolster、Sozu、ReptileTrinket | P2／待认领 | 动态容量、获得／拒绝／满栏／使用，掉落消费由G10提供 |
| RELIC-G10 奖励生成与资格 | WhiteBeastStatue、BlackStar、PrayerWheel、WhiteStar、Orrery、AmethystAubergine | P2／待认领 | 真实奖励前置消费、容器及完整池；卡牌／药水／遗物奖励分子包 |
| RELIC-G11 房间与长期资源 | VenerableTeaSet、FakeVenerableTeaSet、EternalFeather、MealTicket、Pantograph、MawBank | P2／待认领 | 房间类型／历史、离场与下战重置；不拿开发战斗当真实房间 |
| RELIC-G12 商店参与者 | MembershipCard、TheCourier、WongoCustomerAppreciationBadge、FakeMerchantsRug | P2／待认领 | 价格／补货与真实商店库存；静态资格也须源依据 |
| RELIC-G13 营火参与者 | Girya、Shovel、MeatCleaver、MiniatureTent、StoneHumidifier、DreamCatcher | P2／待认领 | 选项修改、费用、一次到访、取消及产生奖励；不重做已有休息 |
| RELIC-G14 永久生成／复制／变换 | Astrolabe、PandorasBox、NewLeaf、DollysMirror及生成牌拾取者 | P2／待认领 | 完整获得／移除／复制／变换、源池与随机流；先交共享操作再接内容 |
| RELIC-G15 附魔与关键词 | GnarledHammer、Kifuda、PunchDagger、RoyalStamp、FresnelLens、WingCharm | P3／待认领 | 源附着身份／替换／升级／复制及资格；不把注册名当行为 |
| RELIC-G16 地图、古代人与跨域长期效果 | GoldenCompass、JuzuBracelet、WingedBoots、FurCoat、LordsParasol | P3／待认领 | 正常地图／房间／Ancient上下文；按跨域真实消费者收口 |
| RELIC-G17 遗物替换／熔化 | SwordOfStone、TouchOfOrobas、ToyBox及升级遗物 | P2／待认领 | 完整Replace／Melt、旧状态清理、新拾取和实例身份；不能只改字段 |
| RELIC-G18 其他角色与特殊规则 | CrackedCore、GoldPlatedCables、BigHat、MiniRegent、Byrdpip、PaelsLegion等 | P3／暂缓 | 各自角色资源／召唤依原授权恢复，不将通用物品分支一并暂停 |
| RELIC-G19 拾取基础资源 | FakeMango、LoomingFruit、NutritiousOyster、SignetRing、GoldenPearl | P1／待验证 | CONTENT-05已交付；逐件数值、拾取失效／持有者隔离及公开证据见当前交付，待PR／CI审阅；正常来源未验收 |
| RELIC-G20 源特例复核与归组 | 尚未确定主要机制、只有属性覆写或有多个跨域阶段的剩余条目 | P2／待认领 | 从完整catalog核对遗漏；记录源方法／属性与组依赖，无法归类不表示排除或自动暂停 |

## 归属与依赖

分类回答“工作放在哪里”，依赖回答“需要谁的能力”，里程碑回答“最终是否可用”。分类顺序不等于全部串行依赖，也不为一个事件的每个文件建立项目级任务。

- **附魔与事件**：附魔域拥有附着数据和规则；事件只描述选项、选牌和调用。WoodCarvings只在事件域登记一次，Slither的实现和寿命归附魔域。
- **机制与内容**：伤害、Power、选择和费用等共享规则只在机制域实现；卡牌／怪物／遗物定义组合它们。怪物缺一种机制就声明依赖，不把每个怪物重做成机制项目。
- **事件与地图**：问号房型概率归地图，事件资格／页面／效果归事件，源队列生成与消费归推进；事件可独立进入验证。
- **房间与物品**：商店／宝箱／事件拥有选择、价格、奖励与离场；遗物／药水拥有获得后的持有和使用效果。ChosenCheese效果归遗物，Cheese的Search选项归事件。
- **先古之民与终局**：Ancient选项／奖励归先古之民，切幕归推进；TheArchitect最终事件内容归事件，触发与整局Victory归推进，原四幕胜负合同保留。
- **公开与验收**：真实多选的Engine行为归选择机制，wire应答归公开接口，参考比较归对拍。规则层通过不等于公开可用。

分类不要求新建对应目录、重写架构或另建运行时。文件主责继续遵守A／B／C边界；共享能力只实现一份，调用方保持可追溯依赖。

原PR保留在同页，下面指定其唯一主要归属，不新增任务ID或复制状态。父合同T06／T09仍跨域追溯，不是第二个生产负责人。

| 主要归属 | 原子任务／边界 |
| --- | --- |
| 卡牌本身 | T06-PR08：永久卡实例成长／降级；其他牌的内容定义仍归本类 |
| 卡牌附魔与附着 | T06-PR04：附魔／负面附着／动态关键词；PR01仅提供所需生命周期接缝 |
| 怪物本身 | T06-PR15：敌人动态召唤／复活（仍暂缓）；PR16：行动控制／逃离 |
| 共享战斗与资源机制 | T06-PR01／02／03／05／06／07／17；T09-PR01永久牌组选择／变换，T09-PR04长期资源命令 |
| 角色与进阶 | T06-PR09～14：星星／Forge／球／宠物／Poison／Doom；通用伤害／死亡沿PR01／02调用 |
| 遗物与药水 | T09-PR02／03：持有及获得／使用生命周期，房间只调用 |
| 问号房里的事件 | T09-PR05的资格／页面／事件战斗挂起返回；事件选择调用PR01，内容不另复制选择机制 |
| 奖励与宝箱 | T09-PR05的通用奖励容器；事件页面调用该容器，不再各写领取／跳过算法 |

T09-PR05原为组合合同，拆分的是上述两个职责，保留同一原ID；事件／奖励两边不得各自认领完整PR05。T13-PR-AUDIT归独立对拍与环境验收；商店、营火、先古之民、地图推进沿自身房间合同，无需强塞进机制PR。

## 推进顺序

1. **按用户2026-10-18顺序继续全遗物**：从[遗物分组](#遗物分组任务)选就绪机制，复用最新统一监听；先核对同机制的完整源行为，再成组配置和执行见证。G05下一候选PenNib，不能按旧Sown接手建议重开附魔或其他角色。
2. **按依赖补完整消费者**：只有参数差异的内容合批；新增共享机制时将契约、真实消费者与必要边界一起交付。房间／奖励／地图依赖具体归组，不以开发获得证明正常来源；受阻条目退出本批，其他就绪内容继续。
3. **恢复独立比较与正常Run集成**：原严格适配器当前零执行，独立恢复包应接同一DSL会话与只读参考，先恢复一个原case，再扩组。地图／房间／奖励往返、Neow、首幕Boss与后续Ancient／跨幕逐步恢复；每批收尾核对该层是否有新增证据，若连续批次只增长定义就由负责人重排。该规则不自动解除全遗物优先或暂停项。
4. **其余角色与完整环境候选**：按原恢复条件安排五角色、进阶、全覆盖和120对矩阵；开发开局首幕仍只是中间检查，Neow先于正常首幕验收。

分类不要求整类全部完成后才暴露集成缺口；下一包以实际就绪依赖为准。历史任务合同保留验收范围，历史“接手先做”不驱动当前派单。

## 每批集成与重排条件

- **批次边界**：按同一触发／状态寿命／消费者合批，一个批次可以包含多个参数配置；不为每3～5件机械结束编码窗口。同一交付分支持续提交，完成一个可审阅机制批后再形成PR；CI等待期间可推进独立就绪工作，合并前仍核对必需结果。
- **配置与机制分流**：仅复用既有模板时，补内容／注册／元数据及逐件数值见证；不重复撰写共享机制规格、接口参考或公开协议测试。新增状态、通知、随机顺序、生命周期或公开字段时，才补对应契约／操作文档和判别。一个批次一份简短证据记录；工具README只在使用方法改变时更新。
- **反馈必须有边界**：调试遗物组使用`check --relic-content-only --filter "AM "`这类已确认的名称筛选，记录实际执行数／跳过数和耗时；零匹配必须失败。局部检查不得生成完整覆盖报告。最终包的逐件报告和覆盖门槛由CI统一执行；CI已有同一包摘要结果时直接引用，不能重复在本地跑完整check取同一证据。
- **默认节奏**：整组实现完成后，统一跑受影响模块的单元／局部行为测试，以及少量与本组直接相关、预先固定种子的对拍。组内按需要做受影响项目的编译／解析／加载检查，不默认启动行为套件；仅遇到阻挡继续实现的具体缺陷时运行最小复现。
- **检查选择**：先列明本组要跑的测试组、固定case／seed和预期结果，复用已有入口；不会只因测试工程叫Slice就运行其全部867项，也不默认捆绑全部Engine、net或十组历史suite。没有触及net、core或跨幕时，不附带对应全量检查。必要构建只覆盖受影响项目，避免同时构建和执行同一输出目录。
- **重跑限制**：通过后不重复跑同一状态。失败修复后只复跑失败用例和修复直接影响的检查；若确有跨模块疑点，说明具体风险后扩大受影响范围。补文档、追加一条局部测试、汇总证据或迁移经核对的身份常量，不据此重跑整套行为测试。
- **CI与阶段验收**：当前 Actions 执行core／base／net／migration；原严格 regression 尚未迁移，不能把migration通过写成独立对拍通过。完整回归在 push／PR 后执行，本地不默认复跑；入口及实际覆盖见[CI说明](../../../tools/ci/README.md)。长程公开回放、全 seed／120对矩阵、性能套件仍留对应里程碑／发布验收，尚未自动接入 CI。组内若存在当前轻量检查无法判断的共享层故障，仅补足定位它的检查；不能用局部通过宣称最终环境已验收。用户2026-10-17的这一分工替代根约定中“完整包本地串行跑全套”的旧执行安排，原验收范围保留。
- **种子与公开证据**：固定种子只运行一次预定计划，保留真实失败和参考缺口；不为了碰到某事件或找一条通路而循环搜索新seed。公开／正常路线证据可在相应集成里程碑补齐，未取得的范围单列待验证，不占用本组时间反复刷路线，也不驱动跨领域跳转。
- **本批回归**：代码和本地轻量检查完成后可交付待验证的内容组，继续下一组；合并前核对 CI。CI 发现新回归后，按失败日志定向复现、修复并重新提交，只复跑相关本地检查，不重启全套；无法修复则保留待验证及具体首差，不跳过失败或标成已验收。
- **外部缺口**：运行到已有怪物缺失、暂缓召唤或无关未实现内容，记到唯一归属和证据；本批自己的验收已足够时可继续安排下一批，不自动转去补该领域。若它使本批承诺的公开／行为证据无法取得，保留对应层待验证，重新缩小或调整下一交付，不能伪造通过。
- **回到规划**：发现必须扩展core、改变接口承诺、参考缺失关键语义、依赖循环或暂停项才能满足目标，停止受影响步骤，给出原因和可继续范围；修改当前交付并记录决定。用户尚在review时不启动编码。


## 任务详情

以下保留稳定ID、文件主责、原依赖、完成条件及证据，是领域总览的追溯合同。详情中的旧接手建议与新领域排期冲突时，以前面的分类和推进顺序为准；原P值保留为合同的历史排期信息，不再独立驱动派单。完整旧记录保存在[整理前快照](../records/2026-10-15/task-list-before-reader-refactor.md)，重排前21项任务表保存在[本轮规划记录](../records/2026-10-17/domain-task-plan-2026-10-17.md)。

### ENV-T10

<details>
<summary>实现幕转换，完成 Ironclad 四幕流程：接手步骤与完成条件</summary>

**负责人**：Codex `/root` 本地接续；旧DSH派发记录保留历史身份。

**执行登记**：2026-10-17按用户继续推进整局主线的指令由Codex本地接续T10，写base SinglePlayer/Run/Map/Encounter编排、必要Host配置接线、独立工具检查及文档。旧`session-fa7f87ac-e285-452a-801a-ad4604e6c431`上次已确认停止、无可验收交付；本轮只读登录仍未建立会话，不恢复旧会话、不修改其派发记录。既有未提交内容保留，实际修改前核对源文件；不恢复召唤族、不改core或参考。

**本轮接续交付**：Codex完成显式多幕开发配置`dev-run`，保留原单幕身份。默认Overgrowth→Hive→Glory开局预生成、跨幕地图/访问/清理、长期实例保留及实际幕索引奖励已接线；三组seed逐幕库存、九张地图/RNG指纹与原生参考一致，受控公开切幕/幂等/旧候选拒绝通过。正常新配置仍被具体事件内容阻塞，后续古代人及TheArchitect未实现，T10整体不关闭。见[默认幕计划与跨幕交付](../records/2026-10-17/default-act-transition-2026-10-17.md)及[契约](../../design/mod-the-spire/act-transitions.md)。

**接手先做**：复用dev-run与act-transition独立核对，不重做预生成或切幕；按正常路线实现已定位的事件/古代人内容及真实正常跨幕取证。原单幕失败例继续保留，不能通过切换配置或删除case掩盖旧差异。

**完整完成条件**：分幕交付：第一幕闭环→后续幕/幕转换→最终事件；正常四幕胜利/真实死亡，最后击杀余效/提交完整，跨角色可获得内容和变体仍计入域。

**验收与证据**：由 Codex 按原完成条件审查；方法和完整范围见[ENV 合同](../../design/environment-completion-and-differential-v1.md)、[实现约束](../../design/environment-implementation-constraints.md)。本任务的逐批交付和原始证据见[整理前记录](../records/2026-10-15/task-list-before-reader-refactor.md#env-t10)，后续新增证据只补与本次验收相关的链接。

</details>

### ENV-T04

<details>
<summary>解决正常开局的参考初始化差异：接手步骤与完成条件</summary>

**负责人**：B：tools/differential；Codex 裁决接口。

**本地接续**：Codex `/root` 已完成AttackPotion窄包集成与第一幕源资格接入，原seed正常持药第二场、使用/取消双侧及公开重放通过；事件身份独立核对均为AromaOfChaos。见[2026-10-16交付](../records/2026-10-16/potion-offer-and-event-eligibility-2026-10-16.md)。10月15日遗漏UsePotion的临时通过仍撤回，不与本轮真实通过混用。AromaOfChaos两分支已通过[正常双侧与公开重放](../records/2026-10-16/aroma-of-chaos-2026-10-16.md)。正常满容量及真实精英奖励已[双侧/公开通过](../records/2026-10-16/normal-potion-capacity-2026-10-16.md)；继续其他适用使用及后续房间，参考只读，不覆盖幕转换主责文件。

**2026-10-15 纠正**：本轮参考及工具代码修改已全部撤销，参考仓库恢复干净。此前“初始化已修复”的结论撤回，不作为验收证据；sts2-sim 保持只读。详见[撤销说明](env-t04-rng-bootstrap-2026-10-15.md)。

**接手先做**：复用v0.109随机布局、显式内容元数据及共同动作计划。两例首战、两场连续正常战斗与实际药水领取已双侧通过；MonsterAi保持run级共享流，独立怪物流旧判断已撤回。复用原seed容量两例的已通过证据，继续后续房间；完整UsePotion候选与AttackPotion独立执行已通过，继续保留比较维度。随机迁移历史见[原交付](env-t04-v109-random-2026-10-15.md)，旧远端会话状态不由本地结果推断。

**实现与验证入口**：[严格对拍工具](../../../tools/base-migration-baseline/differential/README.md)、[正常初始化审计交接](dsh-normal-initialization-audit-handoff-2026-10-13-e524322c.md)。

**执行登记**：`session-e524322c-614d-4142-b393-2ab4a0c80829`；[派发记录](../../../.audit/dsh-normal-initialization-audit-20261013/task-record.json)、[已交审计与提案](dsh-normal-initialization-audit-handoff-2026-10-13-e524322c.md)。该子包原登记“进行中”，交接已有待复核材料；本次不推断远端会话已停止，也不把提案当作参考修改授权。

**完整完成条件**：既有7个case实际双侧执行，5个unsupported原ID保留；缺case/零执行/缺参考/unsupported/首个差异/缺维度非零；源/reference派生与随机trace可核对。

**验收与证据**：由 Codex 按原完成条件审查；方法和完整范围见[ENV 合同](../../design/environment-completion-and-differential-v1.md)、[实现约束](../../design/environment-implementation-constraints.md)。本任务的逐批交付和原始证据见[整理前记录](../records/2026-10-15/task-list-before-reader-refactor.md#env-t04)，后续新增证据只补与本次验收相关的链接。

</details>

### CI-01

<details>
<summary>审阅 CI 与仓库整理结果：接手步骤与完成条件</summary>

**负责人**：Codex；用户审阅。

**接手先做**：core/base/严格对拍三个 CI 组和目录整理已有交付。本轮 Codex `/root` 按用户要求调整测试分工，修改边界为 `tools/ci/check.py`、工作流和相关约定：增加 net 检查组，下一可验成果为四组命令接线、失败传播和文档检查；不在本地重跑全量游戏测试。审阅对应提交的 Actions，核对干净检出构建、固定参考版本和日志。contracts-python 的其余检查仍未接入。

**审阅入口**：`codex/core-base-ci` 与 `codex/repository-hygiene-review-20261015`；[CI 实现与验证](../records/2026-10-15/core-base-ci-2026-10-15.md)、[仓库整理证据](../records/2026-10-15/repository-hygiene-2026-10-15.md)。原登记为等待用户 PR 审阅、尚未合入；本次未查询 GitHub。

**完整完成条件**：固定最新已核对参考身份；独立干净检出构建和命令验证；Actions实际运行结果、日志/结算及未验证明确；本地完整检查分组不丢失。网络或上游兼容失败保留最小复现及受影响case，不将旧参考回退包装为新版成功。

**验收与证据**：由 Codex 按原完成条件审查；方法和完整范围见[ENV 合同](../../design/environment-completion-and-differential-v1.md)、[实现约束](../../design/environment-implementation-constraints.md)。本任务的逐批交付和原始证据见[整理前记录](../records/2026-10-15/task-list-before-reader-refactor.md#ci-01)，后续新增证据只补与本次验收相关的链接。

</details>

### CORE-AUTHOR-04

<details>
<summary>完成引擎 0.4 发布验收：接手步骤与完成条件</summary>

**负责人**：Codex。

**接手先做**：作者模型、同包文本绑定、base/0.0.2 语法和 0.4 身份迁移已有交付，原实现会话已停止。复用这些成果，按下方条件完成最终集成与发布；不再开同类重构。参考项目和 AI 不在本任务范围。

**实现与验证入口**：[core/0.3 绑定参考](../../dsl/core/0.3/authoring-reference.md)、[base/0.0.2 作者参考](../../dsl/0.0.2/authoring-reference.md)、[负责人原复核](../records/2026-10-11/core-author-04-owner-review-2026-10-11.md)。

**完整完成条件**：正式执行不依赖旧步骤摘要/空Program回退；第二个同类机制内容只改DSL和案例；普通/升级与清空/替换语义、实时数值/快照、缺上下文拒绝、源位置、暂停续接均有针对性检查。串行完成solution、Engine、Slice、受影响net/tools与作者例子、严格回归/公开第一幕重放；发布版本与迁移说明，按实际证据结算未验证范围，父项不自动关闭。

**验收与证据**：由 Codex 按原完成条件审查；方法和完整范围见[ENV 合同](../../design/environment-completion-and-differential-v1.md)、[实现约束](../../design/environment-implementation-constraints.md)。本任务的逐批交付和原始证据见[整理前记录](../records/2026-10-15/task-list-before-reader-refactor.md#core-author-04)，后续新增证据只补与本次验收相关的链接。

</details>

### ENV-T03

<details>
<summary>补齐公开客户端的完整流程验证：接手步骤与完成条件</summary>

**负责人**：C：net 与客户端。

**接手先做**：复用当前连续Run的公开轨迹与正常死亡闭环；旧记录中的客户端Unknown停止由后续事件/房间交付替代。下一步随 ENV-T09/10 补完整流程，复核药水旧交接及完整公开契约。Host、客户端和 schema 要使用同一构建；MapDraws 的公开/诊断边界仍需核对。

**正常事件公开依赖**：TabletOfTruth五页＋后续死亡已同计划55次公开提交／39边界核对，并验证页面请求重放与旧观察拒绝。Cheese的Engine真实二选已与参考通过，正常公开流程在`TODO-N02-MULTI-SELECT`明确停止；现有0.2仅支持单选，需在真实数量约束下补应答形状及配套契约，不能拆成伪单选、自动代选或靠组合枚举冒充支持。见[事件接续证据](../records/2026-10-17/act1-event-continuation-2026-10-17.md)。

**实现与验证入口**：[公开运行指南](../../design/net-the-spire/how-to-run-public-game.md)、[客户端参考](../../design/net-the-spire/public-client-reference.md)。

**完整完成条件**：C#/Python/schema一致；每局字符串seed、未Start会话只启动一次、身份前置拒绝/重发幂等/同Host多seed；正常开局describe/catalog/start/step/observe公开可玩；新增公开字段/Action仅改规则包即可加载、发布、执行，net无新增逐项注册；不可编码结构明确处理，地图/奖励新增选择在T05验收。

**验收与证据**：由 Codex 按原完成条件审查；方法和完整范围见[ENV 合同](../../design/environment-completion-and-differential-v1.md)、[实现约束](../../design/environment-implementation-constraints.md)。本任务的逐批交付和原始证据见[整理前记录](../records/2026-10-15/task-list-before-reader-refactor.md#env-t03)，后续新增证据只补与本次验收相关的链接。

</details>

### ENV-T05

<details>
<summary>验收地图、战斗、奖励的完整往返：接手步骤与完成条件</summary>

**负责人**：A 实现，B/C 随能力验证。

**当前实施认领**：Codex `/root` 接续“首幕正常整局闭环”，合并消费T02/03/04/05/08/09的现成能力。范围为tools连续Run/独立适配、同Host公开运行、实际阻塞的base生命周期/房间内容与必要通用net映射。已越过旧六战停止点，修复敌方格挡清理、玩家死亡能力清理和SlipperyBridge；当前首幕完成目标因PhrogParasite的PR15暂缓依赖未完成，新整局suite保留非零。证据和限制见[连续Run集成](../records/2026-10-17/ironclad-run-integration-2026-10-17.md)。未覆盖幕转换主责文件，未改core或参考玩法。

**本包交付**：2026-10-16 Codex `/root` 完成原seed五场Monster＋一场Byrdonis Elite的正常满栏分支：真实取得三瓶后再遇掉落，验证跳过及丢弃槽位1后领取同一奖励实例，槽位0/2稳定、无额外随机；真实精英遗物显式跳过后整组奖励关闭。双侧107/108边界及同Host公开149/150次提交通过，见[容量与路线修复报告](../records/2026-10-16/normal-potion-capacity-2026-10-16.md)。共同构建及Engine99/Slice855/net419、八组严格suite通过。空setup、参考只读；必要修复含精英奖励、事件后的遭遇楼层、Inklet范围/中位开场、Slippery耗尽与Headbutt选择边界。

**接续范围**：容量小包完成，作为整局回归复用。按完整完成条件继续其他适用药水/末杀及后续房间；全部遗物资格/空袋回退、其他精英与四幕未在本包验收，不关闭父项，不恢复召唤族或训练。

**接手先做**：复用RewardWiring与共同动作计划。两场跳过、持药续战、AttackPotion使用/取消、领取/丢弃及公开幂等已通过，见[本轮交付](../records/2026-10-16/potion-offer-and-event-eligibility-2026-10-16.md)。容量已在上列报告验收，继续其余适用使用与后续房间；原seed的AromaOfChaos升级/变换及永久结果已[双侧和公开验证](../records/2026-10-16/aroma-of-chaos-2026-10-16.md)，正式客户端已可越过事件继续。

**实现与验证入口**：[正常公开流程脚本](../../../tools/base-spire-dev/act1_smoke.py)、[药水契约](../../design/mod-the-spire/potion-inventory/spec.md)。

**完整完成条件**：分步交地图/房间阶段→战斗→真实奖励/选牌/跳过/容量→长期提交→地图合法后继；占位升级退出该路径，奖励幂等、同Host公开可操作与新增双侧case实际执行；正常运行不注入清场/改血。

**验收与证据**：由 Codex 按原完成条件审查；方法和完整范围见[ENV 合同](../../design/environment-completion-and-differential-v1.md)、[实现约束](../../design/environment-implementation-constraints.md)。本任务的逐批交付和原始证据见[整理前记录](../records/2026-10-15/task-list-before-reader-refactor.md#env-t05)，后续新增证据只补与本次验收相关的链接。

</details>

### ENV-T02

<details>
<summary>验证战斗结果跨房间保存：接手步骤与完成条件</summary>

**负责人**：A 提供规则与输入，B 比较。

**接续交付**：2026-10-16 Codex `/root` 完成事件结果进入第三场正常战斗、实际打出及结算的两条路线；45/53个边界双侧、66/76次公开提交通过，永久来源和同名实例可区分。范围为tools策略/来源/比较器/suite、实际首差的base能力声明/伤害/衰减与检查及文档；原seed和完整维度保留，参考只读，不改幕转换。见[第三场与Shrink修复交付](../records/2026-10-16/post-event-carryover-2026-10-16.md)。父项仍待Feed等原完成条件。

**已关闭的本包缺口**：Shrink=-1现在按声明应用0.7伤害倍率并无限持续，正/零层数回归通过；DarkEmbracePower工具身份映射及故障前首差定位已修复。原失败证据保留在上述交付，不恢复暂缓机制。

**接手先做**：复用已修复的末次击杀收尾和长期来源检查，沿 last-kill-before-terminal 比较长期实例变化。动作钩子、InPlay 移除、消耗与结果牌区处理必须先于终局；同定义的不同实例不能互相覆盖。依赖 ENV-T04/05 的正常流程证据，不重做已交付的末次击杀修复。

**实现与验证入口**：[结算与写回工作包](../../design/mod-the-spire/base-spire-implementation-plan.md#env-t02)。

**完整完成条件**：Feed MaxHp跨房、最后击杀父效果余效、同定义不同实例永久变化成立；长期牌→战斗副本来源/写回矩阵明确，临时变化不污染长期状态。

**验收与证据**：由 Codex 按原完成条件审查；方法和完整范围见[ENV 合同](../../design/environment-completion-and-differential-v1.md)、[实现约束](../../design/environment-implementation-constraints.md)。本任务的逐批交付和原始证据见[整理前记录](../records/2026-10-15/task-list-before-reader-refactor.md#env-t02)，后续新增证据只补与本次验收相关的链接。

</details>

### ENV-T01

<details>
<summary>验证敌人加权随机行为：接手步骤与完成条件</summary>

**负责人**：A 实现，B 比较，C 接入。

**接手先做**：复用已交付的 float32 权重算法和内容检查，核对构建描述与实际支持范围。TwoTailedRat/Fogmog 仍受 ENV-T06-PR15 等机制限制；这部分没有加载或执行证据，不能因地图比较通过而关闭。用户暂缓的敌人机制不在本轮恢复。

**实现与验证入口**：[加权随机工作包](../../design/mod-the-spire/base-spire-implementation-plan.md#env-t01)。

**完整完成条件**：正式敌人状态机用既有SampleInt+纯Compute，真实分数权重/顺序/动态门槛/多候选/消费计数有针对性检查与B双侧case；构建身份含算法，文案反映实际能力。

**验收与证据**：由 Codex 按原完成条件审查；方法和完整范围见[ENV 合同](../../design/environment-completion-and-differential-v1.md)、[实现约束](../../design/environment-implementation-constraints.md)。本任务的逐批交付和原始证据见[整理前记录](../records/2026-10-15/task-list-before-reader-refactor.md#env-t01)，后续新增证据只补与本次验收相关的链接。

</details>

### ENV-T08

<details>
<summary>完成真实开局与整幕内容：接手步骤与完成条件</summary>

**负责人**：A；B 比较同一构建，C 使用正式工厂。

**接手先做**：复用真实初始化、普通/精英独立队列、Byrdonis/Territorial及完整精英奖励证据；Byrdonis奖励已完成正常双侧和公开验证，不再列为待实现。当前原seed的连续Run在(0,12)遇到PhrogParasite，依赖仍暂缓的ENV-T06-PR15；该阻塞不停止其他房间或T10。其他精英、全部资格/池回退和整幕全域仍须按原完成条件验收。

**实现与验证入口**：[最近初始化与精英复核](../records/2026-10-13/project-continuation-review-2026-10-13.md)、[源初始化交接](../records/2026-10-11/dsh-real-run-init-finish-handoff-2026-10-11.md)。

**完整完成条件**：原始池/顺序/回避/补袋/派生/出生与流位置符合源；不以过滤池局部复算宣称整局一致。

**修改与比较边界**：A主责base正式初始化/内容池与必要针对性检查；B只在tools比较同构建/config/seed实际开局，C消费正式工厂。保持原敌人、牌区、动作及流位置比较；实现所需的真实生命周期与池顺序后再判首个差异，不把诊断返回0当双侧通过。具体会话、文件边界及本包源范围见本轮认领和派单记录。

**验收与证据**：由 Codex 按原完成条件审查；方法和完整范围见[ENV 合同](../../design/environment-completion-and-differential-v1.md)、[实现约束](../../design/environment-implementation-constraints.md)。本任务的逐批交付和原始证据见[整理前记录](../records/2026-10-15/task-list-before-reader-refactor.md#env-t08)，后续新增证据只补与本次验收相关的链接。

</details>

### ENV-T09

<details>
<summary>补齐房间、事件与持有物品：接手步骤与完成条件</summary>

**负责人**：A；C 接公开接口，B 补独立比较。

**历史执行登记（2026-10-17，非当前认领）**：Codex `/root` 已完成TabletOfTruth多页、WoodCarvings资格、Cheese规则层奖励及相应比较，结果见[事件接续记录](../records/2026-10-17/act1-event-continuation-2026-10-17.md)；未完成范围仍按原合同保留。现行范围与主责只查[当前交付](#当前交付)，本历史记录不表示继续编码。

**历史接续交付（2026-10-17，非当前认领）**：JungleMazeAdventure、MorphicGrove和独立事件入口已交付，证据见[事件层记录](../records/2026-10-17/event-layer-batch-2026-10-17.md)；Jungle独行参考RNG差异、Morphic公开多选及整局验收仍开放。该批的原实现窗口已结束，不覆盖当前待review的Sown＋SapphireSeed计划。

**既有交付及未完成范围**：TabletOfTruth完整五页／回复／放弃／致死已接通，正常39边界双侧及同计划55次公开提交通过；RunResources补LoseMaxHp，Cheese的Gorge八选二规则层与WoodCarvings资格完成独立核对。Cheese的Search及公开多选、WoodCarvings效果、BygoneEffigyElite行为仍缺，按领域归属保留；它们不自动成为本批新增范围。证据见[第一幕事件接续](../records/2026-10-17/act1-event-continuation-2026-10-17.md)。

**接手先做**：问号判定、普通事件、商店、营火、宝箱和基础资源操作已有交付。旧记录中的 Unknown/Byrdonis 停止不能作为当前待做；较新边界见 ENV-T08。仍需 SwordOfStone 等遗物、未实现事件及持有内容，Fogmog/Cubex 等原范围也保留。幕定义与跨幕由 ENV-T10 负责，不在这里另开重复任务。

**实现与验证入口**：[房间开发指南](../../design/mod-the-spire/how-to-add-a-room.md)、[公共操作入口](../../design/mod-the-spire/room-compile-reference.md#按操作查找已有公共能力)。

**完整完成条件**：子包T09.1营火、T09.2商店、T09.3宝箱/药水/遗物获得使用容量、T09.4古代人、T09.5事件/事件战斗返回；每族从正式入口可达，资源写回/选择/生命周期及双侧case成立。

#### 整局公共原语补全子任务

这些任务与 ENV-T06 共用 base 的文件修改窗口。每项交付一个完整行为，复用已有资源操作，不逐事件复制算法。幕转换仍由 ENV-T10 负责。

<details>
<summary>永久牌组选取／变换（ENV-T09-PR01 · AromaOfChaos窄包已验收；其余P2待验证）</summary>

**机制包交付**：2026-10-16 Codex `/root` 完成AromaOfChaos升级/变换及RunDeck共享选取命令。原seed正常两战→事件选择→永久结果→回到地图，两分支各33边界双侧及49提交公开重放通过；完整Slice850/net419通过。见[交付记录](../records/2026-10-16/aroma-of-chaos-2026-10-16.md)。实际修改为base共享命令/事件/能力装配/检查、tools适配与suite和文档；现有公开客户端可直接消费，无需改net。未改core、参考或幕转换，本窄包不关闭PR01完整范围。

**已有基础与剩余工作**：已有获得／移除、永久单选升级及Ironclad普通池随机变换；继续其他池、定向Transform、获得替换／阻止、全部源hook和附魔选择。

**依赖与完成条件**：选择复用Engine，附魔依T06-PR04；AromaOfChaos/Astrolabe/DollysMirror等验证实例身份、替换卡池及随机流、升级／附着迁移、BeforeRemoved/AfterChangedPiles，重复提交不再改牌

</details>

<details>
<summary>遗物实例与生命周期（ENV-T09-PR02 · P2 · 待认领）</summary>

**已有基础与剩余工作**：已有四件正式效果及Obtain／Remove／Replace／Melt、隐藏实例Counter与有效性查询，本批状态和证据只维护[当前交付](#当前交付)；继续完整局内变量／StackCount／历史、真实遗物消费者、开局／战斗／回合／房间与修正参与者及源资格

**依赖与完成条件**：依T06-PR01/02的适用hook；SwordOfStone按精英胜利计数并原位替换，获得／移除效果各一次；源遗物袋不按可执行子集缩小，未知效果继续失败

</details>

<details>
<summary>药水容量与使用生命周期（ENV-T09-PR03 · P2 · 待认领）</summary>

**已有基础与剩余工作**：已有生成／任意来源领取／三槽／丢弃／部分战斗使用；补动态容量、指定槽位、非奖励来源生成、多实例、Automatic/非战斗使用及全部适用获得／使用参与者

**依赖与完成条件**：容量先用PotionBelt验证空槽和跨房保留；FairyInABottle依T06-PR02死亡防止，不能注册成手动药水；Alchemize与满栏拒绝保持源随机及实例清理。剩余药水按所缺机制消费其他PR，不另造37个原语任务

</details>

<details>
<summary>跨战斗／房间资源命令（ENV-T09-PR04 · P2 · 待认领）</summary>

**已有基础与剩余工作**：已有基础金币／HP及Tablet所需非卡牌LoseMaxHp；继续SetMaxHp、完整LoseMaxHp参与者／历史、金币来源与Spent/Lost/Stolen历史，以及战斗内容调用Run资源的共享入口

**依赖与完成条件**：依T06-PR02与T09-PR02适用参与者；HandOfGreed致死获金、失去上限与跨房提交、购买与被偷的历史区分；每个值唯一权威，不能把Run字段与战斗副本相互覆盖或重复获利

</details>

<details>
<summary>事件子流程／奖励容器与资格（ENV-T09-PR05 · P2；第一幕资格子包已交付，完整范围待验证）</summary>

**资格子包交付**：2026-10-16 Codex `/root` 完成独立第一幕源资格表与生命周期接入、局部判别和同构建验收。完整池与顺序保留，原seed在正常两战后双方均选AromaOfChaos；未支持资格明确失败。AromaOfChaos效果及其选择/永久提交已[交付](../records/2026-10-16/aroma-of-chaos-2026-10-16.md)；继续剩余资格与事件子流程。范围与证据见[交付记录](../records/2026-10-16/potion-offer-and-event-eligibility-2026-10-16.md)；未改参考、core、幕转换或公共房间装配文件。

**已有基础与剩余工作**：已有普通Continue/Finish、Tablet五页及Cheese的规则层自定义二选奖励；补真实公开多选、其余事件效果／资格、事件战斗挂起／返回及适用奖励参与者。WoodCarvings资格已独立于效果核对，不能当效果已实现

**依赖与完成条件**：复用T02结算、T04正常奖励证据；BattlewornDummy返回原事件、DenseVegetation不返回、奖励领取／跳过／失败不重复关闭或写回；未实现事件仍需正确资格，不能恒true掩盖原版池选择差异。原seed两场后Unknown房的源资格差异已修复，AromaOfChaos两分支已完成并返回旅行，正式客户端继续到第8层自然死亡；历史错误选择见[实测与源码依据](../records/2026-10-15/env-t04-discard-idempotence-2026-10-15.md)。

</details>

**验收与证据**：由 Codex 按原完成条件审查；方法和完整范围见[ENV 合同](../../design/environment-completion-and-differential-v1.md)、[实现约束](../../design/environment-implementation-constraints.md)。本任务的逐批交付和原始证据见[整理前记录](../records/2026-10-15/task-list-before-reader-refactor.md#env-t09)，后续新增证据只补与本次验收相关的链接。

</details>

### ENV-T06

<details>
<summary>补齐战斗公共机制：接手步骤与完成条件</summary>

**负责人**：A；Codex 协调公共接口。

**接手先做**：下一项机制先从下方子任务选择，核对当前 base 文件负责人。已有选牌、出牌编排、文本生命周期、星星/Forge/球/Poison/Doom/Osty 基础能力可以复用。默认空 hook 和受控检查不能替代真实参与者、正式角色流程或独立比较。敌人召唤/复活按 PR15 暂缓；TwoTailedRat 的变量/条件等缺口也仍保留。

**实现与验证入口**：[卡牌原语契约](../../design/mod-the-spire/card-primitives/spec.md)、[角色战斗契约](../../design/mod-the-spire/character-combat/spec.md)、[原语审阅](../records/2026-10-13/base-primitives-review-2026-10-13.md)。

**完整完成条件**：Thorns before站点/零伤害与格挡门控正确，移除含AfterRemoved；出牌计数按执行次数；源decimal/伤害/死亡/召唤/资源归属有对应case。

#### 原语补全子任务

每次只继续一个完整机制，先核对公共文件修改窗口。PR09～14 的基础能力已交付，以下列的是剩余语义；独立比较和正常角色流程没有证据时，不能算整体通过。依赖只限制所用部分，不要求先做全部机制。

<details>
<summary>Power状态与生命周期（ENV-T06-PR01 · P2 · 待认领）</summary>

**已有基础与剩余工作**：已有施加／叠层／部分回合末；补真实施加者、来源卡、实际变化量、类型化触发／修正、阻止施加、修改／移除与独立实例状态；不再用字符串前缀或目标身份猜来源

**依赖与完成条件**：当前A包释放公共文件后接；用Vicious＋Artifact验证来源区分、零变化不误触发、抵消后消耗，Automation类实例计数不串；Expose／Misery所需移除与快照传播后续在同族按完整子包交付

</details>

<details>
<summary>生命／格挡／伤害结果与死亡边界（ENV-T06-PR02 · P2 · 待认领）</summary>

**已有基础与剩余工作**：已有基础伤害、格挡和末尾结算；补LoseBlock、Lose/SetMaxHp、实际伤害／致死结果、Fatal资格、伤害属性／来源、治疗和死亡防止参与者，区分Kill与Damage

**依赖与完成条件**：来源接口依PR01；Expose、Feed/HandOfGreed、BrightestFlame及FairyInABottle为源判别，区分格挡／HP损失、死亡被阻止、致死余效；玩家死亡防止可先交，敌人重附／复活仍受PR15暂缓约束

</details>

<details>
<summary>可暂停的生命周期选牌（ENV-T06-PR03 · P3 · 待认领）</summary>

**已有基础与剩余工作**：已有普通选牌和静态Retain；补源允许时点的hook决策、动态数量／实例资格、临时保留与清理

**依赖与完成条件**：依PR01生命周期；WellLaidPlans在BeforeFlushLate按当前层数选0～N张，恢复后继续原顺序且下一回合过期；不全局放宽所有hook选择限制，普通chooseCard不重做

</details>

<details>
<summary>实例附魔／负面附着与动态关键词（ENV-T06-PR04 · P2 · 首批已交付，完整范围待验证）</summary>

**首批交付边界**：主要归属为卡牌附魔与附着；Sown＋SapphireSeed实现、复制／升级／变换／两战状态及受控公开检查已有证据，实际状态只维护[当前交付](#当前交付)。正常可达及非首张参考比较仍缺。下列PR04完整目标继续保留，未扩展到Slither、Afflict或动态关键词。

**已有基础与剩余工作**：已有定义关键字和变量；补Enchant/Afflict的身份、叠加／替换／清除，Apply/RemoveKeyword、单回合Sly及效果寿命

**依赖与完成条件**：依PR01，选择消费PR03的适用部分；Ringing挂载／新牌进入／移除清理，HandTrick只赋予选中Skill临时Sly，升级／复制／换房不串实例；附魔不得伪装Power字段

</details>

<details>
<summary>生成候选→选择→获得（ENV-T06-PR05 · AttackPotion窄包已验收；其余P2待验证）</summary>

**交付与边界**：Codex `/root` 完成AttackPotion所需完整池生成候选、可取消选择、入手/清理及临时免费，修改边界为base效果与生成编译器/内容/检查、net通用可选单选、tools适配与suite及契约。2026-10-16窄包集成、独立比较及公开验证通过，见[交付证据](../records/2026-10-16/potion-offer-and-event-eligibility-2026-10-16.md)。不改core或参考，不覆盖幕转换文件；该结论不关闭整个PR05。

**后续范围**：Discovery及其他角色/池仍按原条件验收，不以AttackPotion代替完整族；当前M01已不受AttackPotion缺失阻塞。

**已有基础与剩余工作**：显式池及Ironclad完整攻击池distinct多候选、可取消offer、未选实例清理和临时费用已有证据；继续其他池适配、Discovery及其他角色适用范围。

**依赖与完成条件**：复用现有Engine决策；Discovery正常生成3个不重复候选并选择／跳过，满手落点正确，候选生成只抽一次且续接不补抽；其他角色完整池在T12内容就绪后接，不以已实现子集替代源池。原seed的AttackPotion候选、使用/取消及第二场已通过；完整攻击池洗牌取三张、临时免费和清理的历史缺口见[首差记录](../records/2026-10-15/env-t04-discard-idempotence-2026-10-15.md)，关闭证据见本项交付链接。

</details>

<details>
<summary>费用修正与重复出牌上下文（ENV-T06-PR06 · P2 · 待认领）</summary>

**已有基础与剩余工作**：已有固定费／X／StarX／自动出牌；补费用修正寿命、X修正、重复出牌捕获值继承、资源花费hook及目标／出牌限制

**依赖与完成条件**：依PR01；ChemicalX对能量X与星星X生效，EchoForm类重复不二次扣费或错误重读X，零费／自动／临时改费按源结束；复用统一出牌链，不另造重放执行器

</details>

<details>
<summary>领域查询与作用域历史（ENV-T06-PR07 · P3 · 待认领）</summary>

**已有基础与剩余工作**：已有牌区数量、数值运算／let、回合出牌历史；补按定义／实例／类型过滤的牌区查询、HP／资源条件、抽弃牌／伤害／Power来源历史及上一回合／整战斗寿命

**依赖与完成条件**：来源历史依PR01/02；SoulStorm读消耗堆Soul实例数（不是历史），DeathMarch排除回合初抽牌，Bolas辨认上一回合该实例；暂停不重复记录，隐藏历史不进入公开观察

</details>

<details>
<summary>永久卡实例成长与降级（ENV-T06-PR08 · P3 · 待认领）</summary>

**已有基础与剩余工作**：已有战斗变量、LongTermSource和升级；补有来源依据的战斗实例→永久实例写入、Downgrade与修正保留策略

**依赖与完成条件**：依永久牌组身份；GeneticAlgorithm/TheScythe跨两战保留成长，临时克隆不污染原牌或其他同定义实例；直接升级／降级与既有成长分别核对，不把所有战斗变量一律提交

</details>

<details>
<summary>星星事件参与者（ENV-T06-PR09 · P3 · 待认领）</summary>

**已有基础与剩余工作**：保留Gain/Lose/Set、固定费／X快照；补真实ShouldGain/AfterGained/AfterSpent参与者与触发来源

**依赖与完成条件**：依PR01/06；BlackHole等按实际花费触发，拒绝获得与零值不误发，重复／自动出牌正确；正式Regent开局归T12

</details>

<details>
<summary>Forge完整实例语义（ENV-T06-PR10 · P3 · 待认领）</summary>

**已有基础与剩余工作**：保留生成与成长；补CreatedThroughForge、IsDupe过滤、次数修正、AfterForge及SovereignBlade目标／效果参与者

**依赖与完成条件**：依PR01/04适用部分；源ForgeCmd＋SovereignBlade验证消耗区、临时dupe、多个真实剑实例、满手与升级；不能以CloneOf替代IsDupe

</details>

<details>
<summary>充能球定向操作与hook（ENV-T06-PR11 · P3 · 待认领）</summary>

**已有基础与剩余工作**：保留五种球、槽位、Focus和基础被动／激发；补末球／指定球、指定被动目标、次数修正与Channel/Evoke参与者

**依赖与完成条件**：依PR01/02；ConsumingShadow末球、TeslaCoil指定目标、Loop/EmotionChip时点可判别；保持队列、满槽激发及随机抽取顺序，Defect正式开局归T12

</details>

<details>
<summary>宠物参与者与目标语义（ENV-T06-PR12 · P3 · 待认领）</summary>

**已有基础与剩余工作**：保留Osty身份／成长／死亡保留／同实例复活／基础代伤；补宠物Power、真实攻击来源与修正、ModifySummonAmount及Summon/Revived参与者

**依赖与完成条件**：依PR01/02及明确生命周期；Osty自身力量与玩家力量隔离、宠物致死与玩家反应隔离、复活先于召唤hook、两战清理；其他友方宠物按实际内容另核对，不冒充敌人召唤或自动恢复暂缓族

</details>

<details>
<summary>Poison完整参与者（ENV-T06-PR13 · P3 · 待认领）</summary>

**已有基础与剩余工作**：保留已有伤害／递减／Accelerant；补统一伤害属性／hook、合法宠物目标和死亡防止路径

**依赖与完成条件**：依PR01/02，宠物范围依PR12；继续验证5/4/3及次数捕获，同时验证伤害被修改／死亡阻止后是否递减；接口存在不算参与者实现

</details>

<details>
<summary>Doom完整死亡与触发（ENV-T06-PR14 · P3 · 待认领）</summary>

**已有基础与剩余工作**：保留阈值、批次和基础阵营结算；补AfterDiedToDoom参与者、UndyingSigil时点及适用宠物／防死交互

**依赖与完成条件**：依PR01/02，宠物依PR12；同批多个目标、死亡被阻止和时点改变均按源，敌人Reattach部分依PR15解除后再交；不重做基础Doom

</details>

<details>
<summary>动态敌人召唤／复活（ENV-T06-PR15 · P2 · 暂缓）</summary>

**已有基础与剩余工作**：已有状态机／队列／编队／出生；补动态召唤、死亡保留／重附／复活及相应目标资格

**依赖与完成条件**：延续用户对敌人召唤／复活族的暂缓；源为CreatureCmd、Reattach/Fabricator等。解除前只登记缺口；届时按实际机制拆单，验证不误判胜利、行动队列与随机流、不可击中资格；不阻塞PR16独立范围

</details>

<details>
<summary>行动控制与逃离（ENV-T06-PR16 · P2 · 待认领）</summary>

**已有基础与剩余工作**：已有固定／加权状态机；补Stun、强制下一行动、逃离与目标资格，区分逃离和击杀

**依赖与完成条件**：依PR02结算边界；Whistle及CreatureCmd.Escape代表例，验证跳过／恢复行动、队列位置、逃跑不触发Fatal或伪造死亡收益；不涉及PR15暂缓的召唤／复活实现

</details>

<details>
<summary>牌区重排与显式洗牌（ENV-T06-PR17 · P3 · 待认领）</summary>

**已有基础与剩余工作**：已有移动／置顶／抽空重洗；补作者可组合的主动Shuffle与ShuffleIfNecessary、批量迁移的源顺序及相应已知位置失效

**依赖与完成条件**：复用CardDraw/牌区命令；Reboot先手牌入抽牌堆再主动洗牌／抽牌，与“抽空才重洗”区分；ForegoneConclusion的选择依PR03，空域／非空域随机消费及隐藏牌序不泄漏

</details>

**验收与证据**：由 Codex 按原完成条件审查；方法和完整范围见[ENV 合同](../../design/environment-completion-and-differential-v1.md)、[实现约束](../../design/environment-implementation-constraints.md)。本任务的逐批交付和原始证据见[整理前记录](../records/2026-10-15/task-list-before-reader-refactor.md#env-t06)，后续新增证据只补与本次验收相关的链接。

</details>

### ENV-T07

<details>
<summary>核验文本规则库在真实机制中的复用：接手步骤与完成条件</summary>

**负责人**：A；core 缺口交 Codex 裁决。

**接手先做**：随 ENV-T06 或房间机制核对，不先重写全部语言。CORE-AUTHOR-04 已有文本生命周期交付；接手前先确认可复用部分，不能直接重新实现。无法表达的行为需给最小复现。

**实现与验证入口**：[同包文本绑定](../../dsl/core/0.3/authoring-reference.md)、[规则库复用指南](../../design/mod-the-spire/maintainable-rule-sets.md)。

**完整完成条件**：core文本库与领域lowering在同一RuleSet链接执行，普通/升级/适用分支可区分；不能表达时给最小复现由负责人裁定。

**验收与证据**：由 Codex 按原完成条件审查；方法和完整范围见[ENV 合同](../../design/environment-completion-and-differential-v1.md)、[实现约束](../../design/environment-implementation-constraints.md)。本任务的逐批交付和原始证据见[整理前记录](../records/2026-10-15/task-list-before-reader-refactor.md#env-t07)，后续新增证据只补与本次验收相关的链接。

</details>

### ENV-T11

<details>
<summary>验收完整 Ironclad 环境：接手步骤与完成条件</summary>

**负责人**：B，C 提供公开轨迹。

**接手先做**：随 ENV-T10 准备输入；最终验收依赖全部 M02 能力。核对正式构建、配置、seed 与完整轨迹，交负责人决定是否开放完整 Ironclad 身份。

**完整完成条件**：ENV-IRONCLAD-00～07严格执行，另有完整四幕胜利/死亡脚本与必要机制定向case；同正式构建公开完整轨迹；负责人批准后才开放Ironclad身份。

**验收与证据**：由 Codex 按原完成条件审查；方法和完整范围见[ENV 合同](../../design/environment-completion-and-differential-v1.md)、[实现约束](../../design/environment-implementation-constraints.md)。本任务的逐批交付和原始证据见[整理前记录](../records/2026-10-15/task-list-before-reader-refactor.md#env-t11)，后续新增证据只补与本次验收相关的链接。

</details>

### CORE-DSL-WARN

<details>
<summary>为暂未支持的 scope 语法添加警告：接手步骤与完成条件</summary>

**负责人**：core 编译诊断维护；需明确派单。

**接手先做**：先核对 call/scope 语法审阅决定，只修改编译诊断、必要检查与参考文档。空块与非空块都需提示；内部未知名称和未来语法不绑定、不执行，外层仍正常绑定。不改变当前 A/B/C 顺序。

**实现与验证入口**：[call/scope 语法决定](../../design/core-the-spire/core-architecture-decisions.md#12-2026-10-07call与scope求值块占位)。

**完整完成条件**：空/非空scope求值块均提示内部已忽略、使用调用位置this，诊断包含源位置且仅在编译期输出；未知名称/内部未来语法不被绑定或执行，外层block仍正常绑定；其他warning按实际作者需求接续。

**验收与证据**：由 Codex 按原完成条件审查；方法和完整范围见[ENV 合同](../../design/environment-completion-and-differential-v1.md)、[实现约束](../../design/environment-implementation-constraints.md)。本任务的逐批交付和原始证据见[整理前记录](../records/2026-10-15/task-list-before-reader-refactor.md#core-dsl-warn)，后续新增证据只补与本次验收相关的链接。

</details>

### MAINT-01

<details>
<summary>恢复损坏的中文注释：接手步骤与完成条件</summary>

**负责人**：A。

**接手先做**：只修本次触及的机制段，保留事故备份，严格使用 UTF-8；不得改变字面量或行为，不阻断真实整局流程。

**完整完成条件**：严格UTF-8局部补丁，保留事故备份，字面量/行为不受损；不阻断真实Run主线。

**验收与证据**：由 Codex 按原完成条件审查；方法和完整范围见[ENV 合同](../../design/environment-completion-and-differential-v1.md)、[实现约束](../../design/environment-implementation-constraints.md)。本任务的逐批交付和原始证据见[整理前记录](../records/2026-10-15/task-list-before-reader-refactor.md#maint-01)，后续新增证据只补与本次验收相关的链接。

</details>

### CORE-LEGAL-PENDING

<details>
<summary>核对暂停时的动作查询与提交：接手步骤与完成条件</summary>

**负责人**：core 维护；需明确派单。

**接手先做**：原 Host 反例与 base 等价内联修复保留。接手前核对 CORE-AUTHOR-04 已交的非 Running 动作查询行为，不能重做已修部分；真正仍缺的通用语义按冻结契约裁决，不阻塞当前 A/B/C 任务。

**实现与验证入口**：[原隔离复现](../../../.audit/core-model-review-2026-10-07/results.txt)、[冻结契约](../../contracts/core-engine-baseline.md)。

**完整完成条件**：暂停时对外选择与可接受命令一致；保留有效决策及续接，Running/Terminal/Faulted行为明确；按冻结契约裁定语义版本与迁移，完成针对性检查/API文档。

**验收与证据**：由 Codex 按原完成条件审查；方法和完整范围见[ENV 合同](../../design/environment-completion-and-differential-v1.md)、[实现约束](../../design/environment-implementation-constraints.md)。本任务的逐批交付和原始证据见[整理前记录](../records/2026-10-15/task-list-before-reader-refactor.md#core-legal-pending)，后续新增证据只补与本次验收相关的链接。

</details>

### ENV-T12

<details>
<summary>完成其余四角色与全部卡牌：接手步骤与完成条件</summary>

**负责人**：A；角色公共机制只在 ENV-T06 维护。

**接手先做**：遵守用户先完成真实整局流程的安排，不恢复旧卡牌会话。Ironclad 正常多幕交付后，由负责人按实际缺口重新安排；当前流程只补阻塞正式入口的最小内容依赖。保留完整 577 项范围及单人资格，基础原语存在不表示角色已能正式开局。

**实现与验证入口**：[五角色卡牌与证据快照](../records/2026-10-12/five-character-card-catalog/README.md)、[公共机制阶段记录](../records/2026-10-12/five-character-primitives-2026-10-12.md)。

**完整完成条件**：Silent/Regent/Necrobinder/Defect各为一个角色包，逐包交真实开局、多幕胜负及五种覆盖证据；未知来源/unsupported不静默省略。

**验收与证据**：由 Codex 按原完成条件审查；方法和完整范围见[ENV 合同](../../design/environment-completion-and-differential-v1.md)、[实现约束](../../design/environment-implementation-constraints.md)。本任务的逐批交付和原始证据见[整理前记录](../records/2026-10-15/task-list-before-reader-refactor.md#env-t12)，后续新增证据只补与本次验收相关的链接。

</details>

### ENV-T13

<details>
<summary>完成全部进阶与内容覆盖核查：接手步骤与完成条件</summary>

**负责人**：A 提供实现，B 独立验证，Codex 统筹。

**接手先做**：随 ENV-T12 进行；Ironclad 阶段必需的进阶能力可提前。下方 ENV-T13-PR-AUDIT 负责把源行为、共享操作、DSL、实际执行和独立证据逐项对应，不把静态调用导航当成完整性证明。

**完整完成条件**：全部支持进阶实际可配置；源分母/排除理由/待核可达性逐项处理，不继承参考缺内容的缩水范围。

**原语覆盖收口任务 ENV-T13-PR-AUDIT / P3 / 待认领**：由负责人统筹A提供实现清单、B做独立验证；在上述机制包交付后，按完整577项源卡及敌人／遗物／药水／事件／古代人／附魔的单人可达范围逐族建立“源行为→领域操作／hook→DSL／编译接入→实际行为→双侧证据”映射。当前[调用导航清单](../../../.audit/base-primitives-review-20261013/inventory.json)只覆盖静态命令调用，不是完整性证明；需补直接属性改写、hook覆写、任务牌／奖励／地图修正和源资格等遗漏维度。每个缺口归并现有机制包或登记新完整包，已支持组合只补内容／证据，不新增同义原语；多人专用、表现层及明确不支持域按原ENV合同记录，不能删分母。验收为所有纳入范围的行为有可追溯去向，未知／未验证显式保留，不用调用计数或测试数量声明完整。

**验收与证据**：由 Codex 按原完成条件审查；方法和完整范围见[ENV 合同](../../design/environment-completion-and-differential-v1.md)、[实现约束](../../design/environment-implementation-constraints.md)。本任务的逐批交付和原始证据见[整理前记录](../records/2026-10-15/task-list-before-reader-refactor.md#env-t13)，后续新增证据只补与本次验收相关的链接。

</details>

### ENV-T14

<details>
<summary>验收五角色环境候选：接手步骤与完成条件</summary>

**负责人**：B，负责人审查。

**接手先做**：随 M03 准备；关闭依赖五角色/进阶完整能力与 C 的完整公开出口。所有证据须对应同一构建和 run_id，未验证客户端或真实性范围要明确标注。

**完整完成条件**：五角色×进阶0/5/10×八seed共120对，每角色完整胜负/必要房间证据；manifest/首个差异/覆盖/公开轨迹/Release成本完整，交负责人审查。

**验收与证据**：由 Codex 按原完成条件审查；方法和完整范围见[ENV 合同](../../design/environment-completion-and-differential-v1.md)、[实现约束](../../design/environment-implementation-constraints.md)。本任务的逐批交付和原始证据见[整理前记录](../records/2026-10-15/task-list-before-reader-refactor.md#env-t14)，后续新增证据只补与本次验收相关的链接。

</details>

### ENV-T15

<details>
<summary>与真实游戏客户端比较：接手步骤与完成条件</summary>

**负责人**：D；需用户/负责人另行派单。

**接手先做**：候选稳定后再确定版本、构建、解锁、seed 与记录。可以导入真实录制，缺客户端或记录时保持未验证，不能以源码阅读代替实测。

**完整完成条件**：可复现真实记录采集/导入/逐步比较；缺客户端/记录保持未验证，不用源码审计抵实测。

**验收与证据**：由 Codex 按原完成条件审查；方法和完整范围见[ENV 合同](../../design/environment-completion-and-differential-v1.md)、[实现约束](../../design/environment-implementation-constraints.md)。本任务的逐批交付和原始证据见[整理前记录](../records/2026-10-15/task-list-before-reader-refactor.md#env-t15)，后续新增证据只补与本次验收相关的链接。

</details>

### ENV-T16

<details>
<summary>验证一个指定 mod：接手步骤与完成条件</summary>

**负责人**：E；需用户指定范围。

**接手先做**：候选稳定且用户指定 mod/版本/范围后安排。复用共同接口和 DSL，保留原版回归；不自动扩展为任意 mod 迁移。

**完整完成条件**：同接口加载到终局，与真实游戏加载同mod比较；原版回归保持。

**验收与证据**：由 Codex 按原完成条件审查；方法和完整范围见[ENV 合同](../../design/environment-completion-and-differential-v1.md)、[实现约束](../../design/environment-implementation-constraints.md)。本任务的逐批交付和原始证据见[整理前记录](../records/2026-10-15/task-list-before-reader-refactor.md#env-t16)，后续新增证据只补与本次验收相关的链接。

</details>

## 完整环境的验收范围

小任务是下面范围的具体实现步骤，不能代替或缩减原验收条件。局部机制通过不自动关闭这些整体任务。

<details>
<summary>展开 ENV、BASE 与 core 集成的完整范围</summary>

| 父项 | 当前状态 | 完整范围与关联小任务 |
| --- | --- | --- |
| ENV-00 / DIFF-01/02 | 部分工具交付，未验收 | 源基线/完整分母/严格suite/独立比较与候选结算；T04/11/13/14 |
| ENV-01 | 部分随机Profile交付，未验收 | 游戏seed、独立流、调用顺序、源数值/trace/身份保真；T01/04/06/08 |
| ENV-02 / CORE-DSL-BASE | 正式战斗子集，未验收 | 高级声明→同一Engine IR、真实文本规则库、完整战斗机制；T06/07/10 |
| ENV-03 / BASE-02 | dev连续战斗，未验收 | 真实地图/所有房间/长期实例/余效/四幕Ironclad胜负；T02/05/08/09/10/11 |
| ENV-04 / BASE-03 | 后续，未验收 | 五角色/全部进阶/全量单人可达域与来源；T12/13 |
| ENV-05 / NET-01/02 | 正式工厂及地图公开子包已接，完整出口未验收 | 完整当前公开事实/候选/已知位置及公开全程；T03/05/09/11/12/14 |
| ENV-06 | 旧verify为incomplete，未验收 | 全量矩阵/有效覆盖/源裁决/成本/候选及真实性边界；T14/15 |
| BASE-01 | 工厂部分交付，未验收 | 真实开局/同工厂消费者/正式奖励地图/身份与源证据，满足完整合同后申请；T01/03/04/05 |
| CORE-ENV-INTEGRATION | 开放，由base/tools/net接续 | 工厂、源随机与事件/观察对拍；T01/03/04/08 |
| CORE-EDITOR | P4后续，未交付 | 共享grammar高亮/折叠/诊断/补全/跳转；不阻断文本包运行，需独立派单 |

</details>

## 项目长期范围

W-01～08 保留全项目范围。它们说明最终要交付什么；当前执行顺序仍看前面的任务表。AI、数据和成本要求没有因为先做环境而取消。

<details>
<summary>展开 W-01～08：已认可成果与完整完成条件</summary>

### W-01

**已认可范围与当前状态**：受控作者Gym、药水/Run/长期牌组、公开摘要v2及真实stdio部分链路已验收；正式内容/AI专项编码开放

**完整目标、下一交付与关闭条件**：正式源同Host describe→catalog→start→出牌/用药→终局→离线重编码；药水定义/稀有度/目标词表、栏位容量/可用状态及长期组正确。当前LostHpThisTurn/ExhaustedCardThisTurn/成长变量摘要由提供方发布，AI保留差异，不从历史补齐；逐适用Profile核对Manifest，区分原型与正式内容

### W-02

**已认可范围与当前状态**：持久句柄修复、受伤/生成销毁/撤销授权和故障注入已验收；扩展投影可信度开放

**完整目标、下一交付与关闭条件**：同定义实例在关系/位置可区分时正确表达；隐藏增删/重排不泄露，公开事实完整。跨会话一致换名后核对结构/引用/编码/概率/默认选择，扩展场景执行V-15；相应未验收场景不进入真实学习

### W-03

**已认可范围与当前状态**：四簇采样设施与D1固定种子划分交付；丰富场景分布/全环境成本开放

**完整目标、下一交付与关闭条件**：固定Gym内资源/目标/牌区知识等真实差异簇，逐模型身份保证适用train/validation/final隔离与非空，不能用跨身份总计代替；绑定scenario源、故障/截断/资源上限/采样成本。ENV当前先完整分母、suite与Release成本；学习分布/预算由W-07另冻结

### W-04

**已认可范围与当前状态**：默认Gym管线、D1.1可靠性及D2诊断已验收；正式实验暂缓，学习效果开放；用户2026-10-11要求先不动AI，模型工程也暂缓，PyTorch技术选择保留

**完整目标、下一交付与关闭条件**：保留模型/自动微分/Adam/PPO、版本化奖励、真实轨迹学习、同seed配对评价/区间、模型包exact-identity及参数/内存/延迟/吞吐、保存加载/resume证据。Trainer通用调用仍需显式refresh_identity；环境门槛后处理新增路径身份失效、真实效果和晋升。用户2026-10-10已授权优先PyTorch、共享Transformer工程，取代仅NumPy手写的框架限制；社区STS2接口研究已交付；用户2026-10-11要求先不动AI，共享Transformer工程暂缓，后续明确恢复时再登记具体修改边界及验收包。既有NumPy交付保留，不启动真实训练或恢复LLM实验；工程替换不等于学习效果已验收。依据见[社区接口研究与PyTorch决策](../records/2026-10-10/sts2-community-interfaces-and-pytorch-2026-10-10.md)。

### W-05

**已认可范围与当前状态**：受控两房间升级和长期/临时边界有交付；真实完整Run开放

**完整目标、下一交付与关闭条件**：战斗终止与整局终止分离，结算幂等及最终余效、HP/MaxHp/金币/药水/遗物等永久写回；同定义不同实例来源，临时牌隔离。正式地图/遭遇/奖励/商店/营火/宝箱/古代人/事件战斗返回/幕转换与终局全部实现；历史两房间Victory不抵完整环境

### W-06

**已认可范围与当前状态**：前端/共享模型→同Engine IR及正式战斗子集已交付，完整规则/内容/公开/对拍开放；Bully窄缺陷已关闭

**完整目标、下一交付与关闭条件**：ContentSource正式单一来源，高级声明/完整普通升级/条件/X费/目标/hooks语义和文本库组合可核对；运行时支持白名单、来源/解析/加载/行为/对拍/公开可用分列。五角色与所有原版单人可达内容，缺源不删分母，不回退.author/Legacy，不按检查数算覆盖

### W-07

**已认可范围与当前状态**：ENV范围、基线/保护与源裁决流程已规划；完整环境及AI效果未验收

**完整目标、下一交付与关闭条件**：先ENV-00～06完整环境候选，明示参考偏离/客户端抽样边界；门槛后重新冻结AI任务分布、目标/奖励、预算/晋升及部署收益偏好，旧D3/D4方案/seed不自动沿用

### W-08

**已认可范围与当前状态**：窄缺陷有关闭记录，发布/全量证据持续开放

**完整目标、下一交付与关闭条件**：N/H/J、P-01与U-11按适用范围对账，绑定同一源/规则/内容/Profile/模型与run_id，复现首个差异/故障、覆盖/确定性及部署成本；历史已交付模型/句柄/UsePotion/两房间不重做，部分父项不能随窄缺陷关闭

</details>

## 执行约束

- 游戏规则和可变状态分别由 base 与 Engine 的既有接口承接；base 不另建调度器或隐藏 RunState。core/0.3 的历史冻结锚点和 CORE-AUTHOR-04 的活动升级范围要区分。
- 公开句柄跨状态稳定，销毁后不复用；提供方发布当前有效摘要，不能让 AI 用历史补齐未提供的当前事实。药水栏和长期牌组使用通用实体、字段、关系表达。
- 身份跟随实际内容和随机配置变化。合法变化需要迁移预期结果基线，不能为了保住旧常量撤销修复，也不能重录期望来掩盖行为错误。
- 每条执行线一次只做一个完整机制包，公共文件和构建输出须协调。core 缺口附最小复现交负责人，只暂停实际受影响的比较。

## 最近验证与原始记录

以下按日期列出证据及证明范围；2026-10-16接续已实测，旧记录不覆盖较新结果。

| 要查什么 | 记录与限制 |
| --- | --- |
| 事件结果进入第三场及Shrink | [2026-10-16跨战斗交付](../records/2026-10-16/post-event-carryover-2026-10-16.md)：事件改过的牌实际打出，45/53边界双侧及66/76公开提交通过；Engine99/Slice851/net419通过，父项继续开放 |
| AromaOfChaos与永久牌组选择 | [2026-10-16事件交付](../records/2026-10-16/aroma-of-chaos-2026-10-16.md)：两分支各33边界双侧、49提交公开重放通过；完整850/419通过，正式客户端174提交到第8层自然死亡 |
| AttackPotion使用与第一幕事件资格 | [2026-10-16接续交付](../records/2026-10-16/potion-offer-and-event-eligibility-2026-10-16.md)：药水持有/使用/取消双侧及公开重放通过，完整Slice847/net419通过；原seed下一事件身份一致；效果的后续通过见AromaOfChaos交付 |
| v0.109正常战斗与药水领取 | [2026-10-15 连续战斗与药水领取](../records/2026-10-15/env-t04-potion-continuation-2026-10-15.md)：首战两例、两场连续战斗、真实药水领取双侧通过，公开首战24提交跨会话重放通过；容量与完整后续流程开放 |
| CI、干净检出和仓库整理 | [2026-10-15 CI](../records/2026-10-15/core-base-ci-2026-10-15.md)、[目录整理](../records/2026-10-15/repository-hygiene-2026-10-15.md)；等待分支审阅，不代表环境父项完成 |
| 最近共同构建、正常奖励失败与公开流程边界 | [2026-10-13 共同复核](../records/2026-10-13/project-continuation-review-2026-10-13.md)：原严格 17 为 16 两侧通过加 1 接口自检；正常奖励 2 例初始地图不同；公开 105 次成功、第 106 次尝试在 PhrogParasite 停止 |
| 正常初始化缺口和接口提案 | [B 审计交接](dsh-normal-initialization-audit-handoff-2026-10-13-e524322c.md)；已有材料待负责人复核，未取得正常比较通过结论 |
| 公共机制、卡牌与各日期的局部验证 | [记录索引](../records/README.md)、[整理前完整快照](../records/2026-10-15/task-list-before-reader-refactor.md)；不同日期和构建的结果不能合并成一次完整验收 |

旧的事件策略、Byrdonis、排序和参考流缺口已有后续交付，不能从早期日志重新派单。新角色原语、完整四幕、五角色、最终 120 对、真实客户端与 Release 性能仍须各自的验收证据。

## AI 实验的启动条件

- [ ] 完整源内容清单及 source/reference 身份可核对，独立比较实际可执行，所有排除项有依据。
- [ ] ENV-01～05 的规则、全部单人可达内容、整局与公开出口成立，没有静默回退。
- [ ] ENV-06 的覆盖、差异裁决、正常胜负、成本与真实性边界完成，由负责人审查环境候选。
- [ ] 环境验收后，另定 AI 分布、目标/奖励、学习与部署预算、晋升条件；不自动沿用旧 D3/D4 方案和 seed。

完整五角色环境是总目标，Ironclad 只是中间阶段。每用例保护仍为 120 秒、20000 次外部决策、100 层；最终 120 对总计 14400 活动秒。超限单列，不能算自然死亡。参考缺功能时保持 blocked_compare，不能删内容、换 seed 或注入状态来制造正常胜负。

## 暂缓和另行安排的工作

- **敌人召唤/复活**：按 ENV-T06-PR15 保持暂缓；不影响行动控制等独立机制。若实际流程受阻，先报告依赖，不能自行解除。
- **批量补卡**：用户 2026-10-18 已恢复 Ironclad，其他角色按 ENV-T12 的恢复条件安排。五角色、事件和古代人仍是总范围，暂缓批量补卡不等于取消这些内容。
- **AI 与共享 Transformer 工程**：PyTorch 技术选择保留，工程和正式实验按用户要求暂缓；既有 NumPy 交付保留，不恢复真实训练或 LLM 实验。
- **需另行触发**：真实客户端、指定 mod、Lua Compute 后端、IDE/LSP、多人/私密同时选择、任意 mod 自动迁移、参考项目全量修复、内部恢复/fork/search、分布式与真实部署。

## 维护这份清单

1. “当前交付”只保留最近生产批与正在进行的负责人维护，替换已结束批次，不向其累加流水账。领域总览只维护该类已有基础和长期缺口；最近一批的状态、主责、范围、验收和失败处理唯一维护在“当前交付”，跨域只标依赖。旧ID详情保留完整合同及历史证据，认领结束时标明记录，不与当前批重复登记。优先顺序写“推进顺序”，每批执行“每批集成与重排条件”。
2. 开始实现前核对 git 状态、[角色入口](../../design/agent-goals.md)、认领、依赖和已有交付。新任务登记实际会话、目录、当前机制和下一项可验证结果，不恢复旧会话或重做已交付能力。
3. 长日志和逐批过程放 records 或本地证据目录，本页只保留当前行动和必要证据链接。历史记录中的“下一步”不覆盖本表。
4. 按“每批集成与重排条件”分配验证：本地只做受影响的轻量检查，广覆盖回归交给 CI；正常整局、最终矩阵和性能按阶段执行，不能以局部检查替代。纯文档只运行文档与差异检查。
5. 待验证不等于已验收；阻塞要写依赖、最小复现、受影响范围和仍可继续的工作。只有负责人能关闭整体任务、开放完整环境身份或改变范围。共享工作区不自动提交、重置、清理、派新会话或启动训练。

完整协作和验收责任见[管理约定](../../design/post-core-agent-assignments.md)。

## 本次文档整理

2026-10-19，Codex `/root` 已完成本地完成度审阅及工作流维护，待PR／CI审阅；实际结果见[本次记录](../records/2026-10-19/base-completion-workflow-review-2026-10-19.md)。旧入口整理、开发手册及统一监听指南的结果从[历史索引](../records/README.md)查阅，不在当前入口重复维护状态。原ENV／W与机制验收合同均保留。
