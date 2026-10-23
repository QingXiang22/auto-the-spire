# 开发角色、房间与完整 Run

规则目标为[锁定 sts2-sim](../../../../reference/README.md) 的原生顺序路径。随机派生、开局来源和首幕地图已迁移到该目标；以下入口描述当前 DSL 可执行行为，完整后续双侧比较仍未完成。

首幕正常事件选择由 `PullNextNormalEvent` 使用完整31项源队列，资格读取当前资源、永久牌组与进入目标前的累计楼层。Neow进入推进同一游标一次；第一轮优先未访问且合资格的自然事件，耗尽后允许已访问或临时不合资格者，永久禁止自然出现项仍排除。选择不抽随机数，pure `ModifyNextNormalEvent` 修正后才记录最终标识。后幕及未审计profile明确失败；SapphireSeed与DenseVegetation已接入正常事件效果，其余选中事件仍具名失败且不创建空访问。

文档类型：操作指南（How-to）。适用于角色／进阶、正常开局、地图、战斗／商店／营火／宝箱／事件、奖励、永久状态、Ancient、跨幕与终局。SetupDevelopmentDraw 创建开发玩家和三张占位牌；可用下述显式入口建立铁甲战士基础初态。完整 Run 仍需按本页方法恢复，不提供旧 SinglePlayer 工厂或房间 C# 接口。

先查[领域参考](../reference/domains.md)和原[完整环境合同](../../../design/environment-completion-and-differential-v1.md)。按共享状态和生命周期的依赖恢复一个完整纵向流程；不从固定 seed 的首错临时复制一个房型实现。

SapphireSeed／DoorsOfLightAndDark开发访问使用共享EventVisit及永久选牌／资源命令，准确入口见[行为操作参考](../reference/card-behavior.md)。SapphireSeed与DenseVegetation可由正常事件池自然进入；BattlewornDummy的开发升级档可战后返回同一父事件；第三幕正常可达、其他奖励档和完整正常Run仍未接通。

正常seed `sapphire-normal-79` 选择BoomingConch，沿(0,1)首战自然胜利、跳过奖励后进入(0,2)问号，实际选择SapphireSeed。EAT回复9后选择升级一张永久牌，PLANT自动附魔首张合资格永久牌；空候选直接完成。效果完成后提交 `CloseNormalEvent(visit)`，同一RunVisit与EventVisit只关闭一次，返回真实后继(0,3)。公开的 `RunVisit.Event` 与 `EventVisit.RunVisit` 指向同一访问，不复制永久牌或重放效果。

正常seed `dense-normal-30` 选择BoomingConch，沿(1,1)首战自然胜利、跳过奖励后进入(0,2)问号，实际选择DenseVegetation。回答false执行TRUDGE_ON，先扣8生命再获得独立事件流预先抽取的61～99金币，完成后提交 `CloseNormalEvent(visit)`。回答true执行REST，按营火规则治疗最大生命的30%，随后只能提交 `DenseVegetationFight(visit)`，进入四只Wriggler的真实子战斗。

子访问的公开 `RunVisit.Parent` 指向原访问，原 `EventVisit.ChildVisit` 指向子访问；两者使用同一地图节点，子战斗不增加楼层或普通遭遇游标。胜利后按普通战斗领取或跳过奖励，提交 `CloseDevelopmentRewards` 一次关闭子访问和已完成的父事件，返回真实后继(0,3)。父事件不会再次展示选项。踩踏致死或子战斗死亡均结束父访问，不提供地图后继或奖励；其他正常事件的战后续接另行实现。

开发初态先提交 `Ironclad.Initialize`，再提交 `EnterDevelopmentBattlewornDummy`，从三个真实 `BattlewornOption` 候选选择SETTING_2。入口要求无旧敌人和未清奖励。战斗沿真实150HP BattleFriendV2及三回合时限；普通击杀恢复同一父事件并随机升级最多两张永久牌，超时返回但不升级，死亡不返回。此战不产生普通Monster奖励，父事件完成后用 `CloseDevelopmentEvent(visit)` 返回开发位置。SETTING_1药水与SETTING_3遗物保留候选，选择后明确报未支持，不能继续该会话。该入口不代表第三幕正常可达。

在同一公开 Host 的新会话中，可提交 `StartNormalRun` 运行全解锁、A0、单人 Ironclad 的正常开局首段：

1. 回答 Neow 的真实三选一。入口已建立源初态、完整前置队列和真实首幕地图。
2. 选择已支持的 GoldenPearl、NutritiousOyster、BoomingConch、WingedBoots 或 CursedPearl。其余源选项保留，选择后明确报未支持。
3. 提交离开确认，再选择 `ChooseMapPoint` 的当前合法候选。源队列选到 SlimesWeak 或 FuzzyWurmCrawlerWeak 时，会按真实编队及 HP 抽样进入首战并自动开始首回合；其他遭遇明确报未支持。

例如 seed `777` 提供 BoomingConch，首个真实遭遇为 SlimesWeak，出生顺序为 LeafSlimeS／TwigSlimeM／TwigSlimeS，HP 为 14／28／11。在首战用起始牌组正常出牌和结束／开始回合；胜利后复用 `ClaimDevelopmentGoldReward`、`ClaimDevelopmentCardReward`／`SkipDevelopmentCardReward` 与药水领取／跳过动作，最后提交 `CloseDevelopmentRewards` 返回地图。这些共享动作的现有名称保留 Development。全流程不使用开发生成目标、手工改血或手工抽牌。永久牌与战斗副本保持各自实例，公开候选来自同一访问状态。完整边界和源顺序见[Run 约定](../../../design/mod-the-spire/run-domain/spec.md#正常开局首段)。首个已支持普通战斗胜利后可领取／跳过奖励并返回合法地图后继；完整正常 Run scope 仍未开放。

## 定义角色、进阶与正常开局

WingedBoots使下一网格行的全部节点成为合法候选；离开网格后仍沿地图真实连接。实际进入非连接房间才消耗一次，最多三次；沿普通连接、重复通知和事件子战斗不消耗。候选查询与公开Selectable复用纯`CanTravelToNormalMapPoint`，不推进随机流。移除或IsMelted资格失效后不再提供自由旅行；完整熔化入口尚未接入。

CursedPearl通过遗物获得通知先将真实Greed加入永久牌组，再获得333金币。Greed的战斗副本保留Eternal、Unplayable和不可升级资格，也不进入战斗生成池。seed `777` 的三项Neow均有生产效果，可分别选择WingedBoots或CursedPearl并正常进入首战；这不证明完整Run已通过双侧比较。

FlexPotion可从完整正常药水奖励池领取，或在开发场景通过`FlexPotion.Obtain`获得。战斗中用`UsePotion`施加5点临时Strength；多瓶叠加，本方回合结束撤回全部临时量并保留原有正负Strength。药水只消费一次，战斗外没有使用候选。seed `777` 的现有首战领取路径自然提供该药水。

在开发 scope 的干净初态提交 `Ironclad.Initialize`，可建立 80 HP、99 金币、5 Strike／4 Defend／1 Bash 的永久牌组及 BurningBlood。它拒绝覆盖已获得的牌、物品、房间或战斗进度。接着用真实遭遇的 `Spawn` 或开发目标入口建敌人，再提交 `BeginDevelopmentCombat` 和 `BeginDevelopmentTurn`：新副本先用原版 Shuffle 流洗牌，回合自动抽五张；后续回合也自动抽牌，首回合按 Innate 优先并扩展抽取数量至十张上限。不要再手动补五次 Draw。旧机制探针没有初始化角色时仍手动抽牌。

BurningBlood 在实际胜利时治疗 6 并封顶；跨战保留永久牌组、生命与遗物。此入口已使用原版随机算法及流布局，但不包含 Neow、进阶、地图和完整前置消费，不能当作正常 Run。准确守卫、顺序与边界见[角色初态约定](../../../design/mod-the-spire/character-combat/ironclad-start.md)。

在 characters 的内容配置保存角色标识、源池归属及参数，在 run 的 DSL 初始化流程创建权威角色／永久牌组／遗物／药水／资源与源随机状态。区分基础角色配置、进阶修正和 Neow 等正常开局选项。

获得初始物品是否触发生命周期、随机队列的生成顺序、初始资源和跨幕持久字段均以源约定固定。五角色专属资源分别依赖共享机制；一种角色能开局不代表其他角色可玩。没有 Neow 和真实初态的开发开局不能冒用正常 scope。

## 建地图与房间访问生命周期

maps 保存图结构、节点／边、原始节点类型与访问历史；run 保存当前幕、旅行／访问／待选择等权威状态。问号节点判定的实际房型与原节点类型分开存储，不覆盖地图标识。

先定义选点守卫、进入房间、活跃访问、关闭访问和返回旅行的共享操作。房间自身只拥有自己的阶段、选项、资格与独有内容。公开候选必须来自同一状态机，不能由客户端发任意节点／房间字符串跳转。

源地图生成、问号赔率、事件／遭遇队列和 Ancient 分配分别消费明确随机流。静态 act-plan／act-pools 只保存来源，不能靠 JSON 中的效果字段执行整局。

正常`ChooseMapPoint`已接入问号房型抽取。每次合法Unknown选择只从`unknown_map_point`流抽一次；四类隐藏赔率按Monster／Elite／Treasure／Shop顺序更新。前一实际访问包含商店，或目标的非空后继全为Shop时排除Shop；资格和增长修正使用pure接缝。`ResetUnknownRoomOdds`恢复已配置base，不重置随机流；跨幕入口恢复时须调用它。

实际抽到Monster才消费正常遭遇队列、建立新访问并开战，地图和访问的原始MapType保持Unknown。Event使用上文的完整选择器；选到未实现效果或抽中Elite／Treasure／Shop后具名报未支持。分类已消费的随机与赔率更新会保留，不承诺动作级回滚。非法、过期或其他Run的节点在抽样前拒绝。正常seed `unknown-normal-354`可选择GoldenPearl，从(3,1)正常胜利并关闭奖励，再走(3,2)问号进入下一场FuzzyWurmCrawlerWeak；这条可运行前缀不代表完整Run已开放。

## 实现各类房间

开发包的营火已可消费永久牌组：先在房间外通过 `Strike.Obtain` 等已迁移定义入口建立 `RunDeck`，再 `EnterDevelopmentRestSite`。`ForgeAtDevelopmentCampfire` 会发布可取消的单张选择；公开 `net.answer_decision` 中带实例字段的是选牌，空字段候选是取消。取消后仍可休息或重新锻造；成功后用 `LeaveDevelopmentRoom` 离场，升级进入后续战斗副本。没有永久可升级牌时不提供锻造，不能用手牌代替；同次访问不能靠重复进入重置选项。旧原稿 `ForgeCardInHand` 已移除。营火遗物修正、多个升级目标和正常地图入口仍未恢复；见[约定](../../../design/mod-the-spire/permanent-deck-smith/spec.md)。

| 房型／内容 | 先定义的局部行为 | 必须区分的结果 |
| --- | --- | --- |
| 战斗房 | 从永久牌组构建战斗副本，实例映射、源随机与战斗上下文 | 真实胜负后收尾、长期状态提交一次、奖励进入，最后一击余效不丢 |
| 商店 | 源资格、货架生成、价格／折扣、支付／购买、删牌与离店 | 不可支付／满容量不丢货、消费一次、遗物参与者及永久牌组结果 |
| 营火 | 源选项资格、休息／升级及遗物增减选项 | 没有候选的处理、一次选择、合法离场和永久写回 |
| 宝箱 | 资格／开箱、遗物生成与获得、领取或跳过 | 完整源池不缩、重复领取拒绝、获得钩子与访问关闭 |
| 普通事件 | 访问初始化、页面、选项资格、支付／效果和关闭 | 每个有效分支、无效支付、状态携带、选择暂停与取消 |
| 事件子战斗 | 保存父访问、建立子战斗、完成后的返回规则 | 返回原页／继续事件或直接结束，父状态与未完选项不丢 |
| 先古之民 | 幕进入与选项、专属奖励、持续效果及来源 | Neow 与后续 Ancient 分列，选择影响后续真实战斗 |

按上述完整访问链建立规则和约定，再用事件／房型内容配置组合。不存在一种可将所有房间任意字段修改并统一 Close 的万能操作；关闭访问不等于完成支付、获得、子战斗结算或终局。

事件资格是执行内容选择的规则，不是静态 catalog 字符串。保留 act-local／shared／Ancient 分类、未访问优先、耗尽回退和源顺序；不能恒 true 或删未迁移事件以推进某个 seed。

## 生成并结算奖励

开发包已恢复 Monster 金币的最小领取链：胜利后 `Player.Room="rewards"`、`GoldRewardPending=true`，`GoldRewardAmount` 保存 10..20 的金额，金币暂不增加。公开候选 `ClaimDevelopmentGoldReward` 领取保存金额；`CloseDevelopmentRewards` 关闭界面，未领取时即跳过。领取后仍需关闭，才能开始下一开发战斗或进入营火。金额仅在生成时抽一次，失败不给奖励；准确参数和边界见[操作参考](../reference/operations.md)与[奖励约定](../../../design/mod-the-spire/combat-rewards/spec.md)。此金币路径已使用原版流布局，但尚非正常 Run；显式角色的卡牌与药水奖励扩展见下文，遗物奖励未恢复。

显式初始化 Ironclad 后，首幕开发战斗胜利还会提供三张不同的卡牌奖励。公开 CardRewardOptions 和 ClaimDevelopmentCardReward 候选指向真实待领实例；提交所选候选后，该同一实例进入 RunDeck，其他候选销毁，金币奖励仍可独立领取。SkipDevelopmentCardReward 仅跳过卡牌；CloseDevelopmentRewards 同时丢弃未领取的卡牌和金币，之后才能进入下一战。候选生成一次，观察、领取和跳过不重抽；所选牌在下一战重建为独立副本。

奖励使用完整单人 Ironclad 源池中的 80 张 Common／Uncommon／Rare 牌，包含 Feed／NotYet，区别于战斗生成池。基础稀有度、逐项偏移与去重已恢复；仍限首幕 A0、全部解锁、无奖励修正参与者。金币、稀有度、选牌和升级已共用原版玩家 Rewards 流及精确 binary32 抽样；普通来源已按药水掉落判定→金币→适用药水→卡牌的源顺序生成，48项药水候选保留完整源顺序。Elite／Boss、后幕升级率、替代奖励与正常 Run 未恢复。准确参数见[奖励约定](../../../design/mod-the-spire/combat-rewards/spec.md)。

显式 Ironclad 胜利会建立带访问号的 CombatRewardSet，并公开固定 PotionRewardOptions。ClaimDevelopmentPotionReward 领取同一实例；满三栏时保留原候选，可用 DiscardPotion 腾出槽位后再次领取。SkipDevelopmentPotionReward 只跳过药水；关闭奖励清理全部未选实例。48项均可获得，EffectSupported=false 的药水尚不可使用；已有六种效果按窗口和目标提供使用动作。正常访问调用 OfferNormalCombatRewards 并传唯一 visitId，开发自动访问号不能充当正常地图路径。

rewards 拥有奖励容器和待领取状态；卡牌／遗物／药水的完整获得操作仍归对应领域。继续扩展时明确生成候选、随机权重／升级／稀有度、显示、领取／跳过、满容量处理、未选实例清理和最终关闭顺序。

卡牌选择、升级／变换及获得通知可以暂停，续接不能重新抽样、重复支付或提前关闭访问。普通药水满栏领取按源返回未成功，保留同一候选；不额外创建替换或取消选择。战斗／事件／商店调用同一获得操作，不各自创建永久对象。

## 保存永久状态并推进到终局

在 Run 模型中唯一维护永久 HP／最大 HP、金币、永久牌组、物品实例和跨房状态。战斗映射保存哪些变化可回写、何时回写，Fatal、失败、取消与重试分别约定。不要同时更新战斗镜像与长期字段两次获利。

幕转换明确关闭 Boss 奖励、清理幕内寿命、保留长期实例、生成／切换队列、进入 Ancient 和下一幕地图。正常最终胜负必须来自 DSL 终局规则，在待结算余效和选择处理之后发生。受控强制清场或直接注入下一幕不能替代真实跨幕验收。

## 验证一个可观察的流程

先让一个完整访问链可执行：正常初态→公开选点→实际房间／战斗→领取或跳过→长期状态写回→回到地图。记录每个稳定边界、seed／scope／构建标识、随机消费和公开结果，再做独立参考逐步比较。

随后验证失败／死亡路径、其他房型、Boss 奖励关闭和跨幕。原 ENV 里完整 Ironclad、多角色／进阶、四幕与最终矩阵保持原标准，不能由一个正常返回地图的案例关闭。按[机制迁移](migrate-mechanism.md)维护共享操作与检查去向。

## 恢复跨幕时保留源生命周期

默认地图幕计划为 Overgrowth→Hive→Glory，之后是 TheArchitect 最终事件；地图幕完成不等于全局胜利。原四幕终局验收范围保持不变。正常第一幕必须包含 Neow 选择、效果和后续延续，开发初态不能代替。

开局的遗物袋只生成一次；先分配后续幕共享古代人子集，再按幕顺序生成事件、普通、精英、Boss 与古代人队列，保留空子集抽取等源消费。预生成结果隐藏，进入后续幕只启用已生成库存，不重洗或补抽。遭遇派生楼层使用跨幕累计已访问节点数，包含非战斗节点。

Boss 奖励全部解决后才允许切幕。保留生命、金币、永久牌、物品实例和跨房计数；重置幕内访问计数与问号赔率，保留全局已访问事件。清理旧战斗与地图对象、临时牌和监听，重建玩家战斗副本，旧公开句柄失效；房间提交标识跨幕不重复。

每幕使用独立地图流。普通房间数为15／14／13，Rest 计数依源使用 Gaussian(7)／Gaussian(6)／NextInt(5,7)，后两幕 Unknown 标准值减1。后续幕先进入实际古代人；效果或资格未支持时明确失败，不跳过为空事件。奖励升级率读取实际幕索引，TheArchitect 未实现时保持非全局胜利边界。
