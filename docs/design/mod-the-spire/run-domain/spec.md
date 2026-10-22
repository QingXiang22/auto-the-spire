# 整局操作行为要求

归属 ENV-T09 资源／遗物与 ENV-T06 永久牌组。下列操作名称用于说明源行为和待恢复范围，旧编译接口不作为当前可调用 API；当前签名见[操作参考](../../../development/base-spire/reference/operations.md)和[行为参考](../../../development/base-spire/reference/card-behavior.md)。未实现要求不代表现有开发 scope 已支持。所有可变状态与暂停仍由同一 Engine Session 管理。

| 操作 | 前置条件与顺序 | 范围及依据 |
| --- | --- | --- |
| GainGold | 输入快照一次，Modify→AfterModifying→正值才写金币→AfterGained | 沿用[资源约定](../event-domain/spec.md)，奖励/宝箱/遗物共用同一服务 |
| LoseGold | 非负输入快照一次，金币最低0；购买调用方先校验够钱 | PlayerCmd.LoseGold；当前不建地图历史，Spent/Lost/Stolen历史分类仍未支持，不能以负GainGold实现 |
| HealLivingPlayer | 非负输入快照一次，存活且回复最多MaxHp | 营火保留源回复量计算；不实现复活或未登记治疗参与者 |
| GainMaxHp | 非负输入快照一次，先增加MaxHp，再按实际增量回复且封顶 | CreatureCmd.GainMaxHp；当前整数范围和存活玩家，无上限修正参与者 |
| LoseMaxHp | 非负输入快照一次，先损失超过新上限的当前生命，再将MaxHp最低设为1 | CreatureCmd.LoseMaxHp非卡牌来源；复用脱战伤害。历史、全部修正及死亡阻止参与者仍未完整支持，TabletOfTruth的致死选择另按源显式击杀 |
| ObtainRelic | 解析受支持标识→按指定位置创建持有实例→移除玩家/共享袋同标识→按声明顺序AfterObtained | RelicCmd.Obtain；支持现有声明，未知效果明确失败；资格、叠加／去重策略、抽取、商品支付及访问收尾由来源拥有。生命周期见下节 |
| AcquireCard / CreateCard / RemoveCard | 已有实例保留标识、加入永久牌组；新实例用CardDefinitions配方；移除前校验永久成员、BeforeRemoved→移除状态；获得AfterChangedPiles在入组后 | CardPileCmd.Add/RemoveFromDeck；当前支持内容无永久获得替换参与者，替换、历史和全部hook参与者仍未支持，不用战斗移动代替 |
| AcquirePotion | `PotionInventoryRules.Acquire(scope, potion, source)`捕获实例，选择首个空槽；有容量、非已持有且ShouldAcquire允许时才写槽位/移出来源/按槽序入栏/AfterAcquired | PotionCmd.TryToProcure默认slotIndex=-1；满栏或拒绝不变来源/槽位、不发获得hook、不抽随机。source可省略，创建及失败实物清理由来源负责；奖励Acquire(scope)适配相同操作，商店不再借用RewardPotions暂存。指定槽位、全部参与者及历史仍未支持 |

遗物池抽取不属于Obtain：商店生成商品、宝箱进入已消耗相应袋项，不改变随机流或将生成挪到领取。已不存在的袋项移除为无操作。受支持标识名单与获得实现使用同一判据，避免商店说可买但获得时无效果。

永久牌组的BeforeRemoved/AfterChangedPiles编译接口作用于当前实例，默认空仅适用当前内容。奖励候选集合不是战斗牌堆；领取时移出候选后获得，未选候选由奖励模块清理。商店保留源购买/删牌确认和扣款顺序。

验证复用正式房间检查，另覆盖资源输入只读一次、共享金币hook穿透遗物、获得hook观察已完成袋移除、永久牌组实例标识与移除前hook。未知遗物领取显式失败，正常买不起/满栏由Requires拒绝，不改会话状态。

## Run 阶段与访问续接

以下 RUN-01～05 是完整 Run 的目标要求，不改变上文开发操作的实际支持范围。正常入口须包含 Neow、真实地图与前置随机消费；复用角色初态、战斗、奖励及永久实例，不另建 C# RunDriver 或 Host 玩法循环。

### 正常开局首段

`StartNormalRun` 使用全解锁、A0、单人 Ironclad 配置。它只接受未开始开发操作的初态，复用 Session 中的 Player 与空 Run，建立 80 HP、99 金币、5 Strike／4 Defend／Bash 及 BurningBlood。入口复用角色初始化守卫，检查资源、回合、牌区与物品状态，并要求尚未关联 Run；获得再移除遗物后遗留的金币变化也会阻止开局。拒绝启动不改状态或随机流。Run.Profile 公开说明该输入配置；其他角色、进阶和解锁配置尚未接入。

`Player.CurrentRun` 一旦关联正常 Run，开发便利动作便由 DSL 前置拒绝：直接加牌／获得物品／生成敌人、移除遗物、开发靶人、免费抽牌／弃手牌／升级手牌及直接进入开发房间。未关联 Run 的开发会话保留原候选与执行方式；仅创建开发 Run 容器不构成关联。正常出牌、目标选择、回合推进、药水使用／丢弃、奖励领取／跳过与地图操作继续使用各自资格。该限制只作用于外部开发 action，共享 procedure 仍由正常初始化和来源规则调用；不改变来源池、随机顺序或内容支持范围。直接提交被限制的 action 也须在执行前拒绝，状态与随机不变。

开局先按 `RunManager.InitializeNewRun`／`GenerateRooms` 建立两侧遗物袋，分配共享 Ancient，再按 Overgrowth→Hive→Glory 预生成事件、普通／精英遭遇、Boss 和 Ancient 队列。源池成员及顺序完整保留。相邻遭遇使用源标签拒绝抽样，未支持内容仍参与源队列，不改成已实现子集。获得遗物时，先移除两侧袋中相同标识，再执行获得监听。

首幕地图在进入 Neow 前生成。`SourceGaussianInt` 从 `act_1_map` 流按源算法计算营火／问号数量；随后执行七条路径、源预排序与洗牌、房型分配、重复路径裁剪／修复及坐标整理。公开 MapNode.Type 与 MapChildren 保存最终真实地图，临时列表和路径段在发布前清理。路径段在裁剪阶段以每列三位编码，17 个节点占用最多 51 位；坐标在裁剪结束前不改变。

Neow 按独立内容种子生成两个正向遗物选项与一个诅咒选项，保留源排除关系及随机顺序。选择恰好一个 NeowOption 后，通过共享获得操作执行效果，等待离开确认后回到地图。GoldenPearl、NutritiousOyster、BoomingConch 的效果已接入；其余选项仍公开，选择时明确报告未支持，不以无效果获得代替。

`ChooseMapPoint` 只允许当前节点的未访问后继。首个 Monster 消费实际普通遭遇队列，创建稳定递增的 RunVisit，记录原地图类型、实际房型及遭遇标识。已接通 SlimesWeak 和 FuzzyWurmCrawlerWeak：按源编队顺序及 Niche 流抽取真实 HP，再生成敌人、重建和洗牌战斗副本，并自动启动首回合抽五张牌。SlimesWeak 的累计楼层与初始化消费见下文。其他遭遇或房型在修改访问和抽取 HP 之前报告未支持。非法、重复或非后继选点不改状态或 RNG。

正常普通战斗胜利沿用出牌、攻击和伤害深度保护；手动出牌、回合开始／结束及药水使用还以 `Player.EffectDepth` 等待整个外层操作结束，药水先消耗，回合末通知先完成。胜利还要求本场已有真实敌人死亡通知，不能仅因旧目标已死而在回合边界凭空生成奖励。战斗结束反应先执行，再按 `Player.AfterCombatEnd` 清理能力、格挡及战斗牌，随后执行早期胜利反应（MeatOnTheBone），最后执行普通胜利反应（BurningBlood／BlackBlood）。`CombatEnded`、`CombatVictoryEarly` 与 `CombatVictory` 分开通知；单人死亡不发胜利通知、不生成奖励。开发战斗仍保留其检查所需的战斗牌，正常访问清理不改变开发场景的生命周期。

`RunVisit.Committed` 在余效及结束反应完成后只设置一次。Player 资源和 RunDeck 本身就是权威永久实例，不另复制生命或将战斗牌字段回写永久牌。胜利调用共享普通奖励生成，以实际访问 StableId、房型与幕号为输入，`RunVisit.Rewards` 关联同一 CombatRewardSet，Run 与访问阶段变为 rewards；失败访问标记 defeated，不开放后继。正常战斗不能通过 EndDevelopmentCombat 强行关闭。

正常访问复用现有 `ClaimDevelopmentGoldReward`、卡牌／药水领取与跳过、`CloseDevelopmentRewards` 动作。领取和跳过不抽样，关闭清理未选实例并标记奖励容器与访问已关闭，然后返回 map，仅将当前节点的未访问子节点设为 Selectable。关闭和再次领取由原守卫拒绝；后续未迁移房型／遭遇仍在修改访问或随机流前失败。已领取永久实例在下一场重建为独立副本。

包为地图生成声明每条命令四百万条指令上限；超出时保留显式 BudgetExceeded，不截断路径、裁剪步骤或随机消费。跨幕、事件子战斗和终局尚未接入该访问。源移植地图比较、开局操作向量、同一公开 Host 流程和完整模拟器逐步比较分别验证，不能据此关闭 M01。

| 要求 | 输入、状态与执行顺序 | 失败与可观察结果 |
| --- | --- | --- |
| RUN-01 阶段与所有者 | Run 保存当前幕、旅行／访问／幕转换／终局阶段；每次访问有稳定标识、原地图节点类型、实际房型、局部阶段与待完成事项。战斗、奖励各由该访问关联；具体阶段名由 DSL 声明 | 合法动作来自同一阶段及真实后继图；未答决策、未完成余效或未关闭奖励时，跨房动作不可用 |
| RUN-02 外层结算 | 最后一击后先完成父出牌、攻击批次、附着、通知及源要求的战斗结束反应，再按源码顺序提交永久变化、处理适用奖励和访问返回。复用现有深度保护与共享操作，明确每个提交点 | 最后击杀 Feed／治疗等变化不丢失；死亡不产生胜利奖励；最终事件完成前不发布全局胜利 |
| RUN-03 父子访问 | 事件发起子战斗时保存父访问引用、页面、待选及返回位置；子访问完成后按源要求继续同一父事件或结束父访问 | 不重建事件、不重新支付、不重抽已生成候选；BattlewornDummy 返回与 DenseVegetation 不返回分别验证 |
| RUN-04 提交与拒绝 | 每次提交绑定访问／奖励实例及完成标记；同一请求重发沿 net 去重，新的重复或过期动作由 DSL 守卫拒绝。合法取消按源返回上一阶段 | 非法输入不改状态或 RNG；关闭访问不重复加金币、加牌或提交成长。执行故障按 core 既定指令原子性处理，不承诺动作级回滚或自动重试 |
| RUN-05 跨幕与结果 | 源要求的 Boss 奖励解决后，清理幕内对象、保留长期实例并启用对应幕队列；已预生成未来不重洗。自然胜负与外部截断、超时、故障、中断分开表达 | 旧访问不能再次提交；上限截断不成为死亡／胜利；TheArchitect 等最终事件未支持时明确报告，不能提前终局 |

验收从同一公开 Host 的正常开局执行地图→真实战斗→领取／跳过→长期提交→合法地图后继，另检查事件子战斗返回、最后击杀余效及跨幕／最终事件。受控机制用例与正常流程分列，双方比较合法候选、访问关系、阶段、资产及随机调用；未恢复独立执行时不能关闭正常 Run 验收。

### 正常问号房型

Run分别保存原地图节点类型和实际进入房型。问号使用unknown_map_point流的一次源NextFloat，不重设流或重新归一化被禁类型。隐藏赔率按Monster、Elite、Treasure、Shop顺序保存；初始binary32值为0.1、-1、0.02、0.03。负赔率不加入轮盘，其他可选项按binary32顺序累加，roll小于等于累计值时选中，否则使用Event。选中项恢复base，其他可选项增加base；被禁项保持不变，负值仍按源继续增加负base。

前一地图点实际包含Shop，或目标节点的非空后继全为Shop时，先禁用Shop。房型资格和未选增长使用pure修正接缝；当前未迁移的遗物／事件参与者不因此宣称支持。幕初ResetUnknownRoomOdds将当前值恢复已配置base，不重新抽样。观察不显示赔率或抽样，不推进流。

ChooseMapPoint先验证当前Run、节点所有权、未访问及真实后继，再解析问号。问号判为Monster时消费真实普通遭遇队列，RunVisit.MapType仍为Unknown，RoomType为monster；成功进入后清理前一地图点的商店标记。尚未实现的实际房型在分类后具名失败，此时分类抽样及赔率更新已经发生；不承诺动作级回滚。其他直接未支持的地图类型仍在抽样前失败。问号机制不开放正常事件池、商店、宝箱、精英或跨幕；这些消费者各按完整条件验收。

## 遗物实例与生命周期

ENV-T09-PR02首批公共接口复用`RunRelics`，不声明全部遗物效果已实现。依据`RelicCmd.Obtain/Remove/Replace/Melt`、`Player.AddRelicInternal/RemoveRelicInternal/MeltRelicInternal`。定义与编译符号不可变，持有列表、Counter、IsWax与IsMelted均在Engine实例中。Counter是隐藏的首批单计数器接缝（0..999999），不代替原版DisplayAmount、未来多变量、StackCount、FloorAddedToDeck或完整历史。IsWax／IsMelted是公开实例事实；蜡质内容来源、完整状态显示和蜡质特殊效果尚未实现。

| 接口 | 前置与结果 |
| --- | --- |
| IsOwned / IsActive / HasActive | 持有以Owned集合成员为准；有效还要求未熔毁。熔毁实例仍属于持有列表，但不参与实际效果 |
| Obtain | 捕获定义与插入下标；创建普通／蜡质实例并加入指定位置（默认末尾），移除两侧袋项，执行已有声明获得效果，再执行AfterObtained接缝。不将多次调用伪装成幂等；来源负责动作去重／资格 |
| Remove | 捕获持有实例，先移出列表，再执行AfterRemoved，最后销毁实例；回调仍可读原定义与计数。不自动退还或撤销永久获得收益 |
| Replace | 捕获旧实例、目标定义与原下标；校验目标受支持后Remove→Obtain，新实例在原位置，Counter重置。不是就地换DefinitionId，不继承旧计数或蜡质状态 |
| Melt | 仅持有、蜡质且未熔毁的实例；标记熔毁，执行AfterRemoved一次，保留实例、位置及Counter。再次熔毁或熔毁普通遗物具名失败 |
| SetCounter / AddCounter | 只操作有效持有实例，数值由Engine整数域检查；同定义不同实例不串值。计数本身不定义何时触发 |

AfterObtained／AfterRemoved 表示获得与移除的通知阶段，准确 DSL 参数见当前生命周期参考。现有胜利回血按持有顺序逐有效实例执行，仍沿共享战斗结算幂等边界。启动实例使用同一字段配方；现有起始遗物没有获得效果，未来有获得效果的开局消费者必须显式接通，不能以创建字段代替Obtain。

本批判别：OldCoin获得hook看到已入组及已移除两侧袋；替换中间实例后邻居不变、移除先于获得；熔毁BurningBlood仍可观察但不再回血；普通／重复熔毁失败；Pear移除不扣回已增加上限；计数读写和多次组合调用捕获正确实例。检查为受控正式内容，不代替正常可达、独立双侧和完整公开Run。SwordOfStone、ChosenCheese、PotionBelt等真实内容消费者留后续批次。

## 遗物正式内容与覆盖验收条件

过渡实现的 `after-shuffle` 在弃牌堆重洗、源排序与实际随机洗牌完成后派发，早于下一张牌的抽取或自动打出。普通抽牌与自动出牌复用同一个Reshuffle；抽牌堆非空或弃牌堆为空时不派发，初始洗牌不走此监听。当前TheAbacus每次获得6点无加成格挡，移除后停止监听。主动合并非空抽牌堆的完整Shuffle命令不在此接缝范围。具体监听由 DSL 表达，不能恢复旧 C# 规则路径。

遗物效果由所属 DSL 内容模块定义；来源目录保留完整297件及源池顺序，可执行定义与来源总数分开。未知字段、重复定义、缺失来源和未实现组合明确拒绝，不保留第二份 C#／JSON 效果解释器。

获得型效果复用RunResources的GainMaxHp／GainGold／HealLivingPlayer／HealToFull／HealMaxHpPercent；百分比治疗按原版最终整数生命截断。获得药水容量调用同一PotionInventoryRules.GainSlots：MaxPotionCount为玩家Engine持久字段、初值3，支持域0..999；SlotIndex为稳定槽号，领取扫描首个空槽，满栏不入组，容量获得不移动已有瓶。Belt的静态容量只是Engine上界，不能替代动态资格。

战斗遗物按持有顺序编译到同一Engine生命周期，已熔毁／已移除监听不运行；定义ID在执行前捕获，替换不在同一批监听中重新执行新实例。源CombatManager.EndCombatInternal顺序是AfterCombatEnd→AfterCombatVictory→永久提交；单人死亡不伪造成功战斗结束。房间进入／BeforeCombatStart先于首回合；起始遗物必须在首战之前创建。Creature.AfterTurnStart明确首回合不清格挡，Anchor等因此保留开战格挡；后续回合按既有防清空参与者处理。PlatingPower的源首回合不递减使用通用`minimumStartTriggerTurn = 2`声明，仅约束玩家回合开始触发，不阻断首回合末格挡。

效果可声明固定回合、实际Monster／Elite／Boss房型、目标及计数阈值，也可声明手牌数量条件（`HandAtLeast`／`HandAtMost`，来源`PileType.Hand`的`Count != 0`与`IsEmpty`）。SwordOfStone只在精英胜利加计数，第五次通过共享Replace保位换成SwordOfJade，新实例下一次战斗进入时施加力量。Counter隐藏，ShowCounter／DisplayAmount公开所需计数，StackCount独立于游戏效果计数。完整多变量／历史／模型优先级和其他未接语义不得据此声明支持。

回合修正按当前有效持有实例查询，不缓存移除前的结果。抽牌／最大能量修正支持首回合或回合区间；保留能量仅在声明区间内跳过清零并增加本回合最大能量。留牌只阻止普通flush，虚无处置仍先执行。保留格挡上限在没有其他Power阻止清空时截断当前格挡。计数取模用于跨战斗的回合周期；战斗结束不隐式重置遗物Counter。胜利早期治疗先于普通胜利治疗，生命比例条件按当前最大生命向下取整后比较，不能被普通胜利回血提前改变资格。

房间进入监听区分永久Run资源与战斗Player资源：非战斗房的治疗写Run.Hp，战斗进入在重建Player之后、首回合之前执行同一声明的战斗资源版本。营火、商店、宝箱及事件的真实进入边界调用监听，问号判为战斗时仍保留原地图Unknown标识。治疗按永久牌组数量缩放时先整数除法；营火后下场战斗的能量标记保存在遗物Counter，AfterEnergyReset发放后立即清除。营火治疗增量由共享休息命令在治疗前查询；源依据为MealTicket、EternalFeather、Planisphere、VenerableTeaSet及RegalPillow，检查须区分进入／选休息、原地图类型／实际房型、重复进入／跨战斗及移除后的行为。

遗物的生成球／增加球槽复用CombatOrbs.Emit，与卡牌使用同一队列、槽位上限、满槽激发、随机目标与战斗重置。FocusPower声明为既有Focus角色，不另写遗物伤害；Lightning专属值修正在Focus处理后按遗物持有顺序执行（源CombatState.IterateHookListeners先能力后遗物）。数据磁盘、破损核心、注能核心、共生病毒与符文电容器的检查须实际观察球被动／激发伤害或存值、首回合限制及下一战重建，不能只检查Power或球实例存在。

每件可执行遗物须有实际行为检查，覆盖报告从执行结果生成。遗漏／重复源标识、资格漂移、空场景及无用例的可执行定义均失败；来源、行为、独立比较和正常公开路径分别报告，规则见[遗物生命周期](../relic-combat-start/content-lifecycle.md)。

## 永久牌组选择与变换

`RunDeck`提供单张永久牌组升级/变换选择：候选保持长期牌组顺序，空集合无选择、唯一候选自动执行，多候选由Engine暂停并选一张；恢复后才升级或抽取变换结果。升级复用CardDefinitions并保留实例。AromaOfChaos两个源选项均保留，选择结束后由共享事件访问层关闭。

多张选取复用同一领域操作：候选数不超过请求数量时直接按原候选顺序处理全部，否则用一次Engine EntityRefList决策返回恰好所需数量。自动处理的候选在执行前快照；变换追加的新实例不能再进入同一次自动选取。手动答案顺序保留，选择域、数量与重复实例由Engine校验。MorphicGrove是同时选择两张的真实消费者。

变换使用原牌所属完整源池、排除原定义、过滤单人资格及源稀有度；当前支持Ironclad普通池来源，其他未支持池明确失败，不用已实现子集替代。AromaOfChaos使用自己的事件Rng，一次有界抽取，不使用奖励流、不额外滚稀有度或升级。新牌为普通形态的新实例，旧牌退出永久牌组，新牌按源Deck规则追加末尾，牌数保持；不继承旧升级/临时费用。后续获得替换、附着和变换参与者须显式接入，不能将此窄范围写成全量Transform已完成。

源码依据为CardSelectCmd.FromDeckForUpgrade/FromDeckForTransformation、CardFactory.GetDefaultTransformationOptions与CardCmd.Transform；永久Deck变换追加末尾，其他战斗牌区才保留旧下标。普通移除的BeforeRemoved不等于变换的AfterTransformedFrom/To，不能混用生命周期。

## 战斗副本来源与结算写回

正常遭遇使用跨幕累计的 `Run.TotalFloor`：Neow访问为1，每次合法地图访问加1，不能用当前幕行号代替。SlimesWeak以根seed加累计楼层、再加 `SLIMES_WEAK` 的UTF-8 XxHash64派生独立编队流；按源顺序抽小型A、剩余单候选B、中型，三次抽样均消费，出生顺序为A、中型、B。各怪HP按出生顺序消费Niche，开战统一初始化招式，不能在创建和开战各滚一次。未迁移遭遇在访问、HP及编队抽样之前失败。

`SpawnDevelopmentEnemy` 供受控单敌入口立即发布意图；正常批量出生调用只创建实例的 `CreateEncounterEnemy`，由 `BeforeCombatStart` 对全体按序初始化。LeafSlimeS初始RAND且两个等权分支不能连用；LeafSlimeM固定状态与攻击交替；TwigSlimeS固定TACKLE_MOVE。三者复用共享图及状态牌生成操作。

正常差异测试在各侧分别保留永久实例的首次出现编号，删除后不复用。每个战斗牌区逐实例记录永久来源：base读取LongTermSource，独立参考读取DeckVersion；生成的临时牌标为temporary，诊断来源不发布到net或策略输入。同定义不同来源必须保持可区分，不按当前卡名重匹配。

事件后第三场的严格用例要求：当前永久牌每实例恰有一个战斗副本；实际打出事件升级或变换得到的实例；战斗结束后事件前已存在的永久实例和形态保持，奖励新增牌另计。策略只在事件后优先公开Power牌，其余沿原可见候选规则，不能用隐藏来源选择动作。此验证记录不替代Feed上限跨房、最后击杀余效和全部永久写回矩阵验收。

## 有序永久牌组的随机选择

RunDeck.SelectRandom接收有序候选Query、源流和选中实例回调。非空候选恰好一次有界抽取，保持原牌组顺序定位同一实例，回调在选择完成后执行；空域明确失败。CanRemove按当前已支持内容的Eternal资格过滤，HasRarity按实际普通/升级形态判别。动态关键字及附着资格仍依其机制约定，不能借静态元数据宣称完成全部修正参与者。
