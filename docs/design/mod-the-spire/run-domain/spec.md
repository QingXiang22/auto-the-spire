# 整局公共操作契约

归属ENV-T09资源/遗物和ENV-T06永久牌组，排期只查[GitHub 任务清单](https://github.com/QingXiang22/auto-the-spire/issues/28)。这些编译期领域操作生成Engine IR，所有可变状态仍在Engine；规则包构建一次提供相同资源能力给各来源，不在内容叶建立默认空hook的副本。

| 操作 | 前置条件与顺序 | 范围及依据 |
| --- | --- | --- |
| GainGold | 输入快照一次，Modify→AfterModifying→正值才写金币→AfterGained | 沿用[资源契约](../event-domain/spec.md)，奖励/宝箱/遗物共用同一服务 |
| LoseGold | 非负输入快照一次，金币最低0；购买调用方先校验够钱 | PlayerCmd.LoseGold；当前不建地图历史，Spent/Lost/Stolen历史分类仍未支持，不能以负GainGold实现 |
| HealLivingPlayer | 非负输入快照一次，存活且回复最多MaxHp | 营火保留源回复量计算；不实现复活或未登记治疗参与者 |
| GainMaxHp | 非负输入快照一次，先增加MaxHp，再按实际增量回复且封顶 | CreatureCmd.GainMaxHp；当前整数范围和存活玩家，无上限修正参与者 |
| LoseMaxHp | 非负输入快照一次，先损失超过新上限的当前生命，再将MaxHp最低设为1 | CreatureCmd.LoseMaxHp非卡牌来源；复用脱战伤害。历史、全部修正及死亡阻止参与者仍未完整支持，TabletOfTruth的致死选择另按源显式击杀 |
| ObtainRelic | 解析受支持身份→按指定位置创建持有实例→移除玩家/共享袋同身份→按声明顺序AfterObtained | RelicCmd.Obtain；支持现有声明，未知效果明确失败；资格、叠加／去重策略、抽取、商品支付及访问收尾由来源拥有。生命周期见下节 |
| AcquireCard / CreateCard / RemoveCard | 已有实例保留身份、加入永久牌组；新实例用CardDefinitions配方；移除前校验永久成员、BeforeRemoved→移除状态；获得AfterChangedPiles在入组后 | CardPileCmd.Add/RemoveFromDeck；当前支持内容无永久获得替换参与者，替换、历史和全部hook参与者仍未支持，不用战斗移动代替 |
| AcquirePotion | `PotionInventoryRules.Acquire(scope, potion, source)`捕获实例，选择首个空槽；有容量、非已持有且ShouldAcquire允许时才写槽位/移出来源/按槽序入栏/AfterAcquired | PotionCmd.TryToProcure默认slotIndex=-1；满栏或拒绝不变来源/槽位、不发获得hook、不抽随机。source可省略，创建及失败实物清理由来源负责；奖励Acquire(scope)适配相同操作，商店不再借用RewardPotions暂存。指定槽位、全部参与者及历史仍未支持 |

遗物池抽取不属于Obtain：商店生成商品、宝箱进入已消耗相应袋项，不改变随机流或将生成挪到领取。已不存在的袋项移除为无操作。受支持身份名单与获得实现使用同一判据，避免商店说可买但获得时无效果。

永久牌组的BeforeRemoved/AfterChangedPiles编译接口作用于当前实例，默认空仅适用当前内容。奖励候选集合不是战斗牌堆；领取时移出候选后获得，未选候选由奖励模块清理。商店保留源购买/删牌确认和扣款顺序。

验证复用正式房间检查，另覆盖资源输入只读一次、共享金币hook穿透遗物、获得hook观察已完成袋移除、永久牌组实例身份与移除前hook。未知遗物领取显式失败，正常买不起/满栏由Requires拒绝，不改会话状态。

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

AfterObtained／AfterRemoved是构建期注入的`(scope, run, relic) → Inst[]`，与资源／牌组命令一致；它们不是另一套运行时调度器，也不表示完整模型hook排序已支持。现有胜利回血按持有顺序逐有效实例执行，仍沿共享战斗结算幂等边界。启动实例使用同一字段配方；现有起始遗物没有获得效果，未来有获得效果的开局消费者必须显式接通，不能以创建字段代替Obtain。

本批判别：OldCoin获得hook看到已入组及已移除两侧袋；替换中间实例后邻居不变、移除先于获得；熔毁BurningBlood仍可观察但不再回血；普通／重复熔毁失败；Pear移除不扣回已增加上限；计数读写和多次组合调用捕获正确实例。检查为受控正式内容，不代替正常可达、独立双侧和完整公开Run。SwordOfStone、ChosenCheese、PotionBelt等真实内容消费者留后续批次。

## 遗物正式内容与覆盖门槛

过渡实现的 `after-shuffle` 在弃牌堆重洗、源排序与实际随机洗牌完成后派发，早于下一张牌的抽取或自动打出。普通抽牌与自动出牌复用同一个Reshuffle；抽牌堆非空或弃牌堆为空时不派发，初始洗牌不走此监听。当前TheAbacus每次获得6点无加成格挡，移除后停止监听。主动合并非空抽牌堆的完整Shuffle命令不在此接缝范围。这是后续DSL迁移的行为基线，不表示C#规则路径已符合目标架构。

遗物效果的唯一正式来源为`Relics/Content/relics.json`，读取器拒绝未知字段、重复ID、缺失来源、空效果且无明确源无玩法依据、未实现触发点／效果组合。`relic-source-catalog.json`保存完整297件源身份、300条池成员、源摘要与资格裁定；它不是可执行效果库，未实现成员不进入已支持目录，两者摘要都进入实际ContentDigest。原C#四件声明迁入JSON，不保留第二份效果表。

获得型效果复用RunResources的GainMaxHp／GainGold／HealLivingPlayer／HealToFull／HealMaxHpPercent；百分比治疗按原版最终整数生命截断。获得药水容量调用同一PotionInventoryRules.GainSlots：MaxPotionCount为玩家Engine持久字段、初值3，支持域0..999；SlotIndex为稳定槽号，领取扫描首个空槽，满栏不入组，容量获得不移动已有瓶。Belt的静态容量只是Engine上界，不能替代动态资格。

战斗遗物按持有顺序编译到同一Engine生命周期，已熔毁／已移除监听不运行；定义ID在执行前捕获，替换不在同一批监听中重新执行新实例。源CombatManager.EndCombatInternal顺序是AfterCombatEnd→AfterCombatVictory→永久提交；单人死亡不伪造成功战斗结束。房间进入／BeforeCombatStart先于首回合；起始遗物必须在首战之前创建。Creature.AfterTurnStart明确首回合不清格挡，Anchor等因此保留开战格挡；后续回合按既有防清空参与者处理。PlatingPower的源首回合不递减使用通用`minimumStartTriggerTurn = 2`声明，仅约束玩家回合开始触发，不阻断首回合末格挡。

效果可声明固定回合、实际Monster／Elite／Boss房型、目标及计数阈值，也可声明手牌数量条件（`HandAtLeast`／`HandAtMost`，来源`PileType.Hand`的`Count != 0`与`IsEmpty`）。SwordOfStone只在精英胜利加计数，第五次通过共享Replace保位换成SwordOfJade，新实例下一次战斗进入时施加力量。Counter隐藏，ShowCounter／DisplayAmount公开所需计数，StackCount独立于游戏效果计数。完整多变量／历史／模型优先级和其他未接语义不得据此声明支持。

回合修正按当前有效持有实例查询，不缓存移除前的结果。抽牌／最大能量修正支持首回合或回合区间；保留能量仅在声明区间内跳过清零并增加本回合最大能量。留牌只阻止普通flush，虚无处置仍先执行。保留格挡上限在没有其他Power阻止清空时截断当前格挡。计数取模用于跨战斗的回合周期；战斗结束不隐式重置遗物Counter。胜利早期治疗先于普通胜利治疗，生命比例条件按当前最大生命向下取整后比较，不能被普通胜利回血提前改变资格。

房间进入监听区分永久Run资源与战斗Player资源：非战斗房的治疗写Run.Hp，战斗进入在重建Player之后、首回合之前执行同一声明的战斗资源版本。营火、商店、宝箱及事件的真实进入边界调用监听，问号判为战斗时仍保留原地图Unknown身份。治疗按永久牌组数量缩放时先整数除法；营火后下场战斗的能量标记保存在遗物Counter，AfterEnergyReset发放后立即清除。营火治疗增量由共享休息命令在治疗前查询；源依据为MealTicket、EternalFeather、Planisphere、VenerableTeaSet及RegalPillow，检查须区分进入／选休息、原地图类型／实际房型、重复进入／跨战斗及移除后的行为。

遗物的生成球／增加球槽复用CombatOrbs.Emit，与卡牌使用同一队列、槽位上限、满槽激发、随机目标与战斗重置。FocusPower声明为既有Focus角色，不另写遗物伤害；Lightning专属值修正在Focus处理后按遗物持有顺序执行（源CombatState.IterateHookListeners先能力后遗物）。数据磁盘、破损核心、注能核心、共生病毒与符文电容器的检查须实际观察球被动／激发伤害或存值、首回合限制及下一战重建，不能只检查Power或球实例存在。

CI的runtime入口实际运行RelicContentChecks，只有具体行为断言通过后登记相应遗物与场景；RelicCoverageChecks要求每件可执行定义至少有实际通过的行为证据，输出297行`relic-coverage.json`。CI校验器独立对照两份正式JSON，拒绝缺报告、遗漏／重复源身份、资格漂移、可执行但无用例、空场景或伪造完成汇总。尚未实现逐行保留unimplemented；`--require-complete`额外要求无剩余项。行为检查不自动等于独立对拍或完整公开路径，两列保持未知，直到取得真实证据。

## 永久牌组选择与变换

`RunDeck`提供单张永久牌组升级/变换选择：候选保持长期牌组顺序，空集合无选择、唯一候选自动执行，多候选由Engine暂停并选一张；恢复后才升级或抽取变换结果。升级复用CardDefinitions并保留实例。AromaOfChaos两个源选项均保留，选择结束后由共享事件访问层关闭。

多张选取复用同一领域操作：候选数不超过请求数量时直接按原候选顺序处理全部，否则用一次Engine EntityRefList决策返回恰好所需数量。自动处理的候选在执行前快照；变换追加的新实例不能再进入同一次自动选取。手动答案顺序保留，选择域、数量与重复实例由Engine校验。MorphicGrove是同时选择两张的真实消费者。

变换使用原牌所属完整源池、排除原定义、过滤单人资格及源稀有度；当前支持Ironclad普通池来源，其他未支持池明确失败，不用已实现子集替代。AromaOfChaos使用自己的事件Rng，一次有界抽取，不使用奖励流、不额外滚稀有度或升级。新牌为普通形态的新实例，旧牌退出永久牌组，新牌按源Deck规则追加末尾，牌数保持；不继承旧升级/临时费用。后续获得替换、附着和变换参与者须显式接入，不能将此窄范围写成全量Transform已完成。

源码依据为CardSelectCmd.FromDeckForUpgrade/FromDeckForTransformation、CardFactory.GetDefaultTransformationOptions与CardCmd.Transform；永久Deck变换追加末尾，其他战斗牌区才保留旧下标。普通移除的BeforeRemoved不等于变换的AfterTransformedFrom/To，不能混用生命周期。

## 战斗副本来源与结算写回

正常对拍在各侧分别保留永久实例的首次出现编号，删除后不复用。每个战斗牌区逐实例记录永久来源：base读取LongTermSource，独立参考读取DeckVersion；生成的临时牌标为temporary，诊断来源不发布到net或策略输入。同定义不同来源必须保持可区分，不按当前卡名重匹配。

事件后第三场的严格用例要求：当前永久牌每实例恰有一个战斗副本；实际打出事件升级或变换得到的实例；战斗结束后事件前已存在的永久实例和形态保持，奖励新增牌另计。策略只在事件后优先公开Power牌，其余沿原可见候选规则，不能用隐藏来源选择动作。此证据不替代Feed上限跨房、最后击杀余效和全部永久写回矩阵验收。


## 有序永久牌组的随机选择

RunDeck.SelectRandom接收有序候选Query、源流和选中实例回调。非空候选恰好一次有界抽取，保持原牌组顺序定位同一实例，回调在选择完成后执行；空域明确失败。CanRemove按当前已支持内容的Eternal资格过滤，HasRarity按实际普通/升级形态判别。动态关键字及附着资格仍依其机制契约，不能借静态元数据宣称完成全部修正参与者。
