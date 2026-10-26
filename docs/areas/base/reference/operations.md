# 共享 DSL 操作参考

文档类型：参考（Reference）。适用于 core/0.3 与纯 DSL base 的开发态战斗包。只列实际存在的声明；这里没有列出的游戏操作不能按名字猜测可调用。文件路径相对 `mod-the-spire/base-spire/`，精确实现通过各行链接定位。铁甲战士新增费用／选择／历史／生成／复制／自动出牌与 Power 反应的完整签名见[专项操作参考](ironclad-shared-operations.md)。

Power定义、施加命令、通知顺序及私有清理的准确签名见[Power参考](powers.md)。卡牌普通攻击的基础值位于CardDefinition；下方效果模板表不再接受重复伤害值或Power行为kind。

有序正文、显式执行、攻击结果、费用、牌区、附魔及事件的新增接口见[行为操作参考](card-behavior.md)。

正常事件子战斗使用[共享访问操作](../../../../mod-the-spire/base-spire/events/rules/combat-visits.spire)：`BeginNormalEventCombat(owner: Ref(Player), visit: Ref(EventVisit), encounter: String(64)) → Ref(RunVisit)` 只接受已完成的当前事件，创建同节点子访问；`CloseNormalEventCombat(owner, visit: Ref(RunVisit))` 在普通奖励关闭后结束已完成父事件；`DefeatNormalEvent(owner, visit: Ref(EventVisit))` 标记父事件与Run失败。`CanCloseNormalEventCombatRewards(owner, visit: Ref(RunVisit), rewards: Ref(CombatRewardSet)) → Bool` 是pure关联守卫，奖励清理前验证当前子访问、父事件和存活状态。真实消费者是DenseVegetation；BattlewornDummy的父事件续接不使用此结束语义。

开发事件的[子战斗续接操作](../../../../mod-the-spire/base-spire/events/rules/development-combat.spire)由 `BeginDevelopmentEventCombat(owner,visit)` 暂停当前自有事件并创建EventCombatVisit；`SettleDevelopmentEventCombat(owner,victory)` 由真实战斗结束调用，胜利后通过ResumeEventCombat恢复同一父事件，死亡只标记父子defeated。`CanPrepareDevelopmentEventCombat(owner) → Bool` 拒绝旧敌人和未清奖励，`HasDevelopmentEventCombat(owner) → Bool` 禁止开发便利动作绕过真实结果。真实消费者为BattlewornDummy的开发升级档；普通Monster奖励不参与此流程。

## 状态与事件

拾取时随机升级永久牌使用[`UpgradeRandomPermanentCards(owner,kind,amount)`](../../../../mod-the-spire/base-spire/cards/rules/random-permanent-upgrade.spire)：owner为Ref(Player)，kind只接受attack／skill／all，amount为Int(1..999)。操作拥有资格筛选、SourceCardSortKey、完整Niche洗牌、原实例UpgradeCard及临时状态清理；不改RunDeck顺序，不触发战斗洗牌事件。调用方法和边界见[遗物指南](../how-to/powers-relics-potions.md)。

能量 X 的实际支付查询为 `GetCardEnergyCost(owner: Ref(Player), card: Ref(Card)) → Int`，效果查询为 [`ResolveEnergyXValue(owner,card) → Int`](../../../../mod-the-spire/base-spire/cards/rules/operations.spire)，两者均为pure。`CardPlayRecord.RawX` 保存每次执行关联的序列捕获值，`Card.PaidX`保留兼容读取，效果通过 `ModifyXValue(value: Int, owner: Ref(Player), card: Ref(Card))` 征询实例监听，不修改能量或捕获值；非 X 效果查询返回0。自动出牌不扣费但捕获当前能量，重复出牌不重新捕获；准确顺序及 ChemicalX 消费者见[遗物生命周期](../../../design/mod-the-spire/relic-combat-start/content-lifecycle.md#效果-x-修正dsl-relic-content-03)。

角色初态入口 `Ironclad.Initialize(owner: Ref(Player))` 与同名公开 action 只接受干净开发初态，建立源资源、永久牌组与起始遗物；`CanInitializeCharacter(owner)` 是共用 pure 守卫。`Player.CharacterId` 为 String(64)，默认 `none`；`AutoDrawPerTurn` 为 Int(0..10)，默认 0，初始化后为 5。源顺序、初始化拒绝与未支持范围见[角色初态约定](../../../design/mod-the-spire/character-combat/ironclad-start.md)。

| 声明 | 字段／载荷 | 约束与归属 |
| --- | --- | --- |
| [Player](../../../../mod-the-spire/base-spire/combat/schema.spire) | Owner: Participant；Hp／MaxHp: Int=80；Energy: Int=0；MaxEnergy: Int=3；TurnNumber: Int=0；TurnOpen: Bool=false；Block: Int(0..999)=0；DrawBlocked: Bool=false（效果抽牌查询被否决的发布值）；CombatActive: Bool=false；CombatOutcome: String(16)=`none`；CombatTimedOut: Bool=false；LostHpThisTurn／ExhaustedCardThisTurn: Bool=false | 开发玩家；CombatOutcome 是开发态结果（none／victory／defeat），不是协议终局；TurnOpen 是开发态回合门；两个 ThisTurn 标志在回合开始时复位，逐次 CardPlays 和 HpLossCount 补充所需历史 |
| [Card](../../../../mod-the-spire/base-spire/cards/schema.spire) | Owner: Participant；Label: String(64)；DefinitionId: String(64)=`base-spire:development-card`；Cost: Int(0..9)=0；Upgraded: Bool=false；CanUpgrade: Bool=false；PermanentSource: 隐藏可选 Ref(Card)；Targeted: Bool=false；Exhausts: Bool=false；LeavesPlay: Bool=false | Targeted 是原版 TargetType 的开发替身；Exhausts 是 CardKeyword.Exhaust 的开发替身；LeavesPlay 是 CardType.Power 的开发替身（结算后离开战斗）；Rarity 字段保存源稀有度，奖励资格与战斗生成资格分别维护 |
| [Enemy](../../../../mod-the-spire/base-spire/combat/schema.spire) | Owner: Participant；DefinitionId: String(64)；Hp／MaxHp: Int=0；Block: Int(0..999)=0；MoveIndex: Int(0..9)=0；Intent: String(32)=`none`；Escaped: Bool=false；隐藏SpawnOrder: Int | 开发敌人：MoveIndex 是它自己轮转里的招位（含义由敌人模板拥有），Intent 是公开的下一个意图名（不含伤害数值）；死亡和逃离各有独立状态；招式图、编队及支持范围见敌人指南 |
| [Power](../../../../mod-the-spire/base-spire/combat/schema.spire) | Owner；DefinitionId: String(64)；隐藏Definition: Ref(PowerBlueprint)；HolderPlayer／HolderEnemy: 可选Ref；Stacks: Int(-999..999)=0；SkipTick／ExpiresAtTurnEnd: Bool=false | 两个持有者引用恰好设置一个；所属模块拥有行为与私有状态，已删除Kind及专属计数槽。准确语义见[Power参考](powers.md) |
| Powers | 有序 Power 集合 | 按定义的shared／per-applier／independent策略匹配实例；公开元数据把它标为 not_published，因为当前 group／sequence 集合元数据无法表达两种持有者类型 |
| [Potion](../../../../mod-the-spire/base-spire/potions/schema.spire) | DefinitionId: String(64)、EffectSupported: Bool、Usage／Target: String(16)、Slot: Int(-1..2) | 真实药水实例，Slot=-1表示未入库存；来源池元数据与效果支持分别维护 |
| Potions | 有序 Potion 集合，容量 3 | 三个共享槽位；ObtainPotion使用首个空槽，DiscardPotion可腾空；使用要求已有支持的效果、正确窗口和目标，在监听之后移除并销毁 |
| PotionUsed／PotionUsedOnTarget | owner: Ref(Player)、potion: Ref(Potion)（有目标时再加 target: Ref(Enemy)） | 药水仍在库存时发出，之后才消费；有目标版本解析第一个 `Hp > 0` 的开发目标 |
| Hand／DrawPile／DiscardPile／InPlay／ExhaustPile／RemovedFromCombat | 有序 Card 集合；Hand 容量 10 | 当前是全局开发牌堆，尚无按持有者分堆；DrawPile 隐藏标识及顺序；InPlay 只在出牌结算期间持有该卡；ExhaustPile 接收完整耗尽操作和强制耗尽结果；RemovedFromCombat 接收离场 Power 或被战斗变换移除的旧牌；这些都是临时实例，永久牌独立保存在 RunDeck |
| [Relic](../../../../mod-the-spire/base-spire/relics/schema.spire) | Owner、Holder、DefinitionId、IsMelted=false、Counter=0 | Holder 是隐藏 Ref(Player)，Counter 隐藏；DefinitionId 长度上限 64；Counter 为 0..999999 |
| Relics | 有序 Relic 集合 | 监听器遍历现有库存并过滤 Holder、definition 和熔化状态 |
| RelicObtained／RelicRemoved | owner: Ref(Player)、relic: Ref(Relic) | 获得时已入库存；移除时已出库存，事件执行后才销毁实例 |
| [BeforeCombatStart](../../../../mod-the-spire/base-spire/combat/lifecycle.spire) | owner: Ref(Player) | 只在 Idle→Active 的开发态开始操作中发出；监听器观察到 CombatActive=true 且本场初始 Block 已清 0，之后获得的格挡保留 |
| AfterBlockCleared | owner: Ref(Player) | 每次玩家回合开始，先尝试按首回合／保留资格处理格挡，再无条件发出；源CombatManager在Creature.AfterTurnStart后派发，包括首回合及保留格挡时 |
| AfterEnergyReset | owner: Ref(Player) | ResetTurnEnergy重置／补足能量后、TurnNumber递增前发出；ArtOfWar依上一编号在此奖励 |
| [AfterPlayerSideTurnStart](../../../../mod-the-spire/base-spire/combat/lifecycle.spire) | owner: Ref(Player) | 起手抽牌及AfterPlayerTurnStart的Early／普通／Late三轮完成后发出；TurnStartEnergyRelic、EveryTurnsEnergyRelic与Sai在此奖励 |
| AfterPlayerTurnStart | owner: Ref(Player) | 起手抽牌后依次发AfterPlayerTurnStartEarly、普通、Late；全部完成后才发AfterPlayerSideTurnStart |
| AfterPlayerTurnStartLate | owner: Ref(Player) | 紧随AfterPlayerTurnStart发出，对应原版AfterPlayerTurnStartLate；三轮在同一执行栈内依阶段顺序完成，每轮按源实例顺序遍历 |
| AfterPlayerSideTurnEnd | owner: Ref(Player) | 自动打出→BeforePlayerSideTurnEndVeryEarly／Early／普通→手牌回合末效果与Flush之后发出；Rage等在本阶段按获得顺序到期 |
| EnemyAddedToCombat | owner: Ref(Player)、target: Ref(Enemy) | 开战时对活动敌人在 BeforeCombatStart 前逐一发出；BattleFriend 在此获得3层计时 Power |
| EnemyEscaped | owner: Ref(Player)、target: Ref(Enemy) | 全部Power移除、Escaped及CombatTimedOut写入后发出；不发EnemyDied或Fatal |
| EnemyDied | target: Ref(Enemy) | 普通敌人确认死亡时发出一次，完成通知后标记DeathResolved并通过RemovePower清理其Power；完整死亡钩子、防死与保留策略未迁移，见[死亡约定](../../../design/mod-the-spire/combat-end/spec.md) |
| CombatEnded | owner: Ref(Player)、victory: Bool | 结束态已经写入 `CombatOutcome` 并关闭回合门与活跃标记之后发出；开发胜利可生成奖励；正常访问在结束通知、清理及胜利通知后提交并生成奖励 |
| CombatVictoryEarly／CombatVictory | owner: Ref(Player)、victory: Bool | 胜利时依次通知早期／普通胜利效果；正常访问先清理能力、格挡与战斗牌，再发送这两项通知，之后提交访问及生成奖励 |
| EnemyTurnStarted | owner: Ref(Player)、target: Ref(Enemy) | 玩家回合结束后，对每个 `Hp > 0` 的敌人各发一次；敌人内容模板按 DefinitionId 订阅并实现自己的招 |
| AfterEnemySideTurnEnd | owner: Ref(Player) | 敌方阶段结束时发出；遗物库存先执行，再按当前敌方顺序，每个生物先执行其Power（获得顺序）再执行怪物监听；出生编号保持不变，SpawnOrder保存槽位整理后的行动顺序；这里做计时及 debuff 减层（源在敌方回合末），取代此前挂在玩家回合末的做法 |
| AfterShuffle | owner: Ref(Player) | 仅在实际转移并完成 shuffle 后发出 |
| AfterCardDrawn | owner: Ref(Player)、card: Ref(Card) | 成功移到 Hand 后发出；未抽到牌不发 |
| [CardPlayed](../../../../mod-the-spire/base-spire/cards/schema.spire) | owner: Ref(Player)、card: Ref(Card) | 旧声明保留，当前正文管线不发布；新增主效果绑定ExecuteCardBody，外部反应使用Before／After通知 |
| CardPlayedOnTarget | owner、card、target: Ref(Enemy) | 旧声明保留，当前正文管线不发布；目标通过ExecuteCardBody的target传入；兼容入口选首个存活敌人，PlayTargetedCard使用原生决策 |
| CardUpgraded | card: Ref(Card) | 实例标记为升级后发出；卡片定义用它应用定义层费用、伤害参数和 Innate 升级 |
| CardExhausted | owner: Ref(Player)、card: Ref(Card)、causedByEthereal: Bool | ExhaustCombatCard入口按源拒绝ending；成功移动到ExhaustPile并完成CardMoved后，在未ending时发布本通知；已进入的监听快照保持，订阅时可确认实际耗尽牌已在耗尽堆 |

## 过程与公开动作

开发便利 action 只在 `Player.CurrentRun` 未关联正常 Run 时提供；`CanUseDevelopmentActions(owner)` 返回这一资格。直接加牌／获得／生成、移除遗物、开发靶人、免费抽牌／弃手牌／升级和直接进入开发房间均受该守卫约束。`StartNormalRun` 后仍复用正常出牌、回合、药水、奖励和地图动作；正常规则调用下表共享 procedure 不受开发 action 守卫限制。

遗物与Power统一使用[通知与修正监听](../../../design/mod-the-spire/effect-listeners.md)：通知通过emit，计算通过foldEvent；订阅声明instances与instance_order，过程接收载荷和末尾listener实例。主流程不读取遗物专属字段或直接调用Power效果过程。具体监听与私有状态位于powers/shared对应模块；通用定义与生命周期位于powers/rules。

遗物共享生命周期提供以下实际入口，参数名区分大小写；内容仅配置模板，状态在Engine遗物实例中。详细时序见[遗物生命周期约定](../../../design/mod-the-spire/relic-combat-start/content-lifecycle.md)。

| 遗物入口 | 参数 | 行为与边界 |
| --- | --- | --- |
| HasOwnedRelic／HasActiveRelic | owner: Ref(Player)、definition: String(64)，pure→Bool | 前者检查库存标识，包含熔化实例；后者还要求未熔化。防止重复拾取使用Owned，动态效果使用Active |
| CombatModifierRelic模板 | definition、strikeDamage: Int(0..999)=0、retainEnergy: Bool=false | 监听ModifyAttackAdditive和ShouldResetEnergy返回修正值，不配置公共Relic效果字段；供StrikeDummy／FakeStrikeDummy／IceCream复用 |
| NoAttackTurnEnergyRelic模板 | definition、energy: Int(0..999) | ArtOfWar.State按遗物实例保存私有标志，获得时创建、移除时清理；AfterCardPlayed记录本拥有者攻击，AfterPlayerSideTurnEnd转交；AfterEnergyReset在非首回合且上回合无攻击时GainEnergy，再清理标志；ArtOfWar配置1 |
| ObtainMaxHpRelic模板 | definition、amount、fullHeal=false | 通过RelicObtained触发IncreaseMaxHp，必要时HealToMaximum；不在动作尾部重复奖励。供Strawberry／Pear／Mango／LeesWaffle复用 |
| CardCountRelic模板 | definition、kind、every、perTurn，以及energy／block／damage／power／stacks | kind筛选Card.Kind；AfterCardPlayed按实际次数推进独立Relic.Counter。perTurn决定重置或跨战保留，重复／自动各计一次、多段只计一次。源码仍声明未使用的powerKind默认参数，新配置不传它，行为只按power定义查找 |
| TurnStartEnergyRelic模板 | definition: String(64)、amount: Int(0..999)、turn: Int(1..999)、throughTurn: Bool=false | AfterPlayerSideTurnStart调用GainEnergy；默认TurnNumber==turn，throughTurn=true时<=turn；NoEnergyGain抑制奖励 |
| EveryTurnsEnergyRelic模板 | definition: String(64)、amount: Int(0..999)、turns: Int(1..999) | 每次本持有者回合开始将本实例Counter加1模turns；归零GainEnergy，计数跨战保留且不受NoEnergyGain抑制 |
| TurnStartBlockRelic模板 | definition: String(64)、amount: Int(0..999) | AfterPlayerSideTurnStart每回合调用GainUnpoweredBlock，包含首回合；不使用敏捷／脆弱修正 |
| GainGold | owner: Ref(Player)、amount: Int(0..99999) | 共享已迁移金币获得，沿用99999上限；尚无完整获得金币修正参与者，金币奖励与OldCoin共用 |
| HealToMaximum | owner: Ref(Player) | 计算MaxHp-Hp并调用Heal；支持最大生命至999999，不替代复活或完整治疗修正 |
| BeforePlayerSideTurnStart／BeforePlayerSideTurnEnd | owner: Ref(Player)，事件 | 前者在清格挡／能量与回合号增加前；后者在回合末自动打出及VeryEarly／Early阶段后、手牌回合末效果前。StoneCalendar致胜后不再执行敌方行动 |

卡牌奖励保存在公开 CardRewardOptions；Player.CardRewardPending 表示该条目尚可选择，CardRarityOffset 是隐藏的跨奖励偏移。Card.Rarity 与 CardBlueprint.Rarity 为 String(16)，由源内容配置。下列入口限首幕 A0、全部解锁 Ironclad 开发配置；算法及未支持范围见[奖励约定](../../../design/mod-the-spire/combat-rewards/spec.md)。

| 奖励入口 | 参数 | 行为 |
| --- | --- | --- |
| OfferCombatCards | owner: Ref(Player) | 由战斗胜利编排调用，生成三张不重复奖励；依次稀有度抽样及偏移更新、缺稀有度回退、候选抽取、升级抽样。不是公开发牌动作 |
| ClaimDevelopmentCardReward | owner: Ref(Player)、card: Ref(Card) | 公开 action 固定本玩家的 CardRewardOptions 候选；过程调用 ObtainCardReward，将同一实例入永久牌组并销毁未选项。保留金币与 rewards 房间 |
| SkipDevelopmentCardReward | owner: Ref(Player) | 要求 rewards 且有待领卡；销毁所有候选，不抽随机、不增加永久牌，金币保持 |
| CloseDevelopmentRewards | owner: Ref(Player) | 同时清理所有未领卡牌、药水及金币，关闭访问容器后离开奖励房；关闭前不得跨房 |
| OfferNormalCombatRewards | owner、visitId、source、actIndex、ascension | 仅全部解锁无修正Ironclad首幕A0 Monster；访问唯一，创建CombatRewardSet，按药水判定→金币→适用药水→卡牌生成 |
| ClaimDevelopmentPotionReward／SkipDevelopmentPotionReward | owner: Ref(Player) | 领取复用ObtainPotion；满栏保留同一待领实例；跳过仅销毁待领药水，不抽随机 |
| ObtainPotion／DiscardPotion | owner: Ref(Player)、potion: Ref(Potion) | 首个空槽获得后发布PotionObtained；丢弃先移出库存再发布PotionDiscarded，最后销毁 |

| 声明 | 准确参数 | 行为与边界 |
| --- | --- | --- |
| [GainUnpoweredBlock](../../../../mod-the-spire/base-spire/combat/block.spire) | owner=Ref(Player)、amount=Int(0..999999999) | 以Unpowered且无Move调用共享格挡命令，绕过当前敏捷／脆弱／不可动摇修正，保留零量与分数量通知；最终格挡上限999999999 |
| GainBlock | owner=Ref(Player)、amount=Int(0..999999999) | 捕获当前card／play并以powered Move调用共享命令；先加法再乘法，最终取整。源旗标、持有者和独立实例决定修正，不在Frail后提前取整 |
| GainEnemyBlock／GainEnemyUnpoweredBlock | target=Ref(Enemy)、amount=Int(0..999999999) | 怪物招式与无加成格挡使用同一GainCombatBlock；Frail／Dexterity可作用于怪物招式，Plating走无加成入口 |
| GainCombatBlock | player、enemy=Binding（恰一非空）、amount=Int、unpowered、move=Bool、card、play=Binding → Int | 返回修正后的百万分之一格挡单位；BeforeBlockGained→加法→乘法→非负截断→实际修正者回调→提交→AfterBlockGained。接受后不因Before导致ending撤销提交；通知仍遵守阶段门禁 |
| CalculateBlockAmount | 同GainCombatBlock参数，pure → Int | 返回相同中间单位；不创建上下文、不写状态、不消费随机。支持的精度与[格挡约定](../../../design/mod-the-spire/combat-turn-resources/spec.md#双方格挡命令)一致 |
| ClearCombatantBlock | player、enemy=Binding（恰一非空） | Direct顺序记录首个阻止者，清零或仅向该仍有效实例发AfterPreventingBlockClear；通知后的AfterBlockCleared由侧开始流程发布 |
| [ApplyPlayerPower](../../../../mod-the-spire/base-spire/powers/rules/lifecycle.spire) | owner=Ref(Player)、definition=String(64)、stacks=Int(-999..999) | 先查定义，未知即失败；同持有者同定义合并，否则创建。定义决定负值／Single策略；零层移除，事件交给所属模块处理 |
| ApplyEnemyPower | target=Ref(Enemy)、definition、stacks | 对敌人执行同一合并语义；已确认死亡或逃离的持有者不再接受Power |
| [Heal](../../../../mod-the-spire/base-spire/combat/resources.spire) | owner=Ref(Player)、amount=Int(0..999) | Hp 增加并按 MaxHp 截断；不处理复活、治疗修正或死亡，也不改变 MaxHp |
| GainEnergy | owner=Ref(Player)、amount=Int(0..999) | Energy 增加并可超过 MaxEnergy（上限 999），尊重 NoEnergyGain；回合重置使用 GetMaxEnergy |
| [DamageEnemy](../../../../mod-the-spire/base-spire/combat/damage.spire) | target=Ref(Enemy)、amount=Int(0..999999) | 先扣 `min(Block, amount)` 格挡，剩余从 Hp 扣除且不低于 0；无加成伤害，修正参与者不参与（遗物与 Power 用它） |
| EnemyIsActive | target: Ref(Enemy)，pure，返回 Bool | 正 HP 且未逃离；选敌、出牌、伤害、敌方行动及终局判断共享此资格 |
| EnemyIsHittable | target: Ref(Enemy)，pure，返回 Bool | 非活动目标返回 false；其余以 true 为初值征询 ShouldAllowHittingEnemy |
| EscapeEnemy | owner: Ref(Player)、target: Ref(Enemy) | 活战中的活动敌人先逐个RemovePower，再写Escaped／CombatTimedOut、发EnemyEscaped并在外层效果结束后检查胜利；保留正HP，重复调用无效果 |
| ShouldAllowHittingEnemy | value: Bool、target: Ref(Enemy)，pure修正事件 | 供命中资格监听返回当前目标是否允许命中；不写状态或消费随机 |
| DamageAllEnemiesFromPlayer | owner: Ref(Player)、amount: Int(0..999999) | 对当前 EnemyIsHittable 目标执行无加成群伤；整批提交生命／格挡后才逐项通知伤害和死亡。无目标直接返回；其他攻击、选敌和胜利条件仍沿各自现有资格 |
| DamageEnemyPowered | owner: Ref(Player)、card: Ref(Card)、target: Ref(Enemy)、amount: Int(0..999999) | 以显式攻击者和牌源计算Strength、Strike及升级牌遗物加成，再应用Weak、目标Vulnerable和攻击者Cruelty，最终一次向下取整，再按格挡→生命结算；BodySlam等计算伤害也走此链 |
| ModifyVulnerableMultiplier | value: Int、owner: Ref(Player)、card: Binding、target: Binding；pure修正事件 | 正层目标Vulnerable在玩家加成攻击链以150百分数点征询；PaperPhrog加25，之后叠加Cruelty，再乘伤害；无目标状态不征询 |
| ModifyEnemyWeakMultiplier | value: Int、owner: Ref(Player)、attacker: Ref(Enemy)；pure修正事件 | 正层攻击者Weak在敌方加成攻击链以75百分数点征询；owner为防守玩家，PaperKrane减15；无攻击者状态不征询 |
| UpgradedAttackDamageRelic模板 | definition: String(64)、amount: Int(0..999) | ModifyAttackAdditive对非空且已升级牌源贡献加值；Holder是攻击者或牌拥有者即可，二者不要求同时满足；MiniatureCannon配置3 |
| StatusMultiplierRelic模板 | definition: String(64)、vulnerableBonus: Int(0..100)=0、weakReduction: Int(0..75)=0 | 有效持有实例订阅两个状态倍率站点；PaperPhrog配置25、PaperKrane配置15，虚弱倍率最低0；不改变状态层数／时长 |
| ResetTurnEnergy／AfterEnergyReset | owner: Ref(Player)，过程／事件 | 首回合重置；后续有有效保留能量资格时将最大能量加到余能，上限999，否则重置。该操作绕过禁止获得能量，随后发事件；额外奖励仍调用GainEnergy |
| DamagePlayer | owner=Ref(Player)、amount=Int(0..999999) | 对玩家执行格挡→生命结算，正的未阻挡伤害发 PlayerHpLost 并检查开发失败；不改变 MaxHp |
| DamagePlayerFromCard | owner=Ref(Player)、card=Ref(Card)、amount=Int(0..999999) | 玩家自伤，保留牌源与空play，可格挡、无加成；经DamageResult、生命损失通知和失败判定，Infection回合末消费 |
| [LosePlayerHp](../../../../mod-the-spire/base-spire/combat/damage.spire) | owner=Ref(Player)、amount=Int(0..999) | 绕过格挡的生命损失，对应源 `Unblockable | Unpowered` 伤害（Offering／Bloodletting）；不应用伤害修正、不触发受击反应，只保留生命下限 0 |
| [PlayCard](../../../../mod-the-spire/base-spire/cards/rules/play.spire) | owner=Ref(Player)、card=Ref(Card) | 校验战斗／回合、手牌、共享资格与能量；付款、进入InPlay、执行唯一正文；全部重复完成后提交捕获的结果区 |
| PlayAttackCard | owner、card | 同上，但把敌人解析为第一个 `Hp > 0` 的开发目标后调用唯一正文；无目标不是合法候选 |
| UpgradeCard | card=Ref(Card) | 只允许 CanUpgrade=true 且未升级的实例；置 `Upgraded=true` 并发 CardUpgraded，定义层费用变化由 CardDefinition 应用；只改当前实例 |
| [DrawCards](../../../../mod-the-spire/base-spire/cards/rules/draw.spire) | owner=Ref(Player)、count=Int(min=0) | 效果抽牌，fromHandDraw=false，委托DrawCardsWithSource；次数允许层数乘虚无次数超过999，仍受实际手牌上限约束 |
| PlayCardFromHand／PlayAttackFromHand／UpgradeCardInHand | 参数 card=Ref(Card)，候选来自 Hand | 公开 action：分别打出无目标牌、打出有目标牌与升级手牌；PlayCardFromHand 只提供 `Targeted=false` 的候选，PlayAttackFromHand 只提供 `Targeted=true` 的候选 |
| [PlayCard](../../../../mod-the-spire/base-spire/cards/rules/play.spire) 结算收尾 | — | 效果后按Exhausts调用完整耗尽、LeavesPlay移出，否则弃牌；耗尽命令依源拒绝ending时牌仍在InPlay，不能为触发回调强制入堆 |
| [EnterDevelopmentRestSite](../../../../mod-the-spire/base-spire/rooms/rest-site.spire) | owner=Ref(Player) | 公开 action：把房间置为 `rest-site` 并清掉已使用标记；不是地图节点入口 |
| RestAtDevelopmentCampfire | owner=Ref(Player) | 公开 action 与同名过程：只在营火且未使用时允许，回复 `floor(MaxHp × 0.3)` 后把选项标记为已用；过程在非营火或重复使用时失败 |
| ForgeAtDevelopmentCampfire | owner=Ref(Player) | 公开 action 与同名过程：未用过的营火、存活且非战斗，有本玩家可升级永久牌时发出 Engine 的 0..1 实例决策；选择升级该永久实例并用掉选项，取消不消耗；成功后发 AfterRestSiteSmith |
| LeaveDevelopmentRoom | owner=Ref(Player) | 公开 action 与同名过程：只有成功用过营火选项、没有选择待决且非战斗时可离开，房间复位为 `none`；奖励界面必须使用 CloseDevelopmentRewards |
| [CombatEnded 奖励订阅](../../../../mod-the-spire/base-spire/rewards/rules/combat-gold.spire) | owner=Ref(Player)、victory=Bool | 仅用于开发战斗；存活玩家胜利时，Ironclad调用完整普通奖励操作；未选角色的金币探针仅从命名流 `base-spire:player:rewards` 的 `[10, 21)` 抽金额；写入待领状态、Room=`rewards`；Gold 不变；失败不生成也不抽随机 |
| ClaimDevelopmentGoldReward | owner=Ref(Player) | 公开 action 与同名过程：只在 rewards 且条目 pending 时把保存金额加进 Gold（开发上限 99999），清空条目；领取不关闭界面，不重抽，不能重复领取 |
| CloseDevelopmentRewards | owner=Ref(Player) | 公开 action 与同名过程：只在 rewards 时清空未领条目、金额与房间状态；允许不领取直接关闭，不给金币也不抽随机；关闭前禁止开始新战斗、进入营火或 LeaveDevelopmentRoom |
| [CheckCombatVictory](../../../../mod-the-spire/base-spire/combat/lifecycle.spire) | owner=Ref(Player) | 在伤害、攻击、出牌、回合或药水操作的外层边界检查；本场已有真实敌人死亡通知、深度均归零且已无存活敌人时，写 victory、关闭回合与活跃标记，按结束→正常清理→早期胜利→普通胜利顺序通知，再提交正常访问及生成奖励 |
| CheckCombatDefeat | owner=Ref(Player) | 每次玩家掉血后调用：生命为0且战斗仍活跃时写defeat并关闭状态，发PlayerDied，完整清理该玩家Power及私有状态，再发CombatEnded并提交失败访问；重复调用不重复死亡或结案 |
| [DamagePlayerPowered](../../../../mod-the-spire/base-spire/combat/enemy-turn.spire) | attacker=Ref(Enemy)、owner=Ref(Player)、amount | 敌方攻击链：防守方 `after-attacked-damage`（Thorns）先以无加成伤害反击攻击者，再按攻击者 Strength→攻击者 Weak ×0.75→防守方 Vulnerable ×1.5（整数向下取整）→格挡→生命，并做失败判定 |
| EnemyAttack | attacker=Ref(Enemy)、defender=Ref(Player)、amount=Int(0..999)、hits=Int | 一次Attack包含指定命中数；负命中数拒绝，BeforeAttack／AfterAttack各一次，逐击走已有伤害／反应／死亡流程；单击DamagePlayerPowered复用此过程 |
| RunEnemyTurn | owner=Ref(Player) | 敌方阶段：BeforeEnemySideTurnStart→先清全体参与者格挡→逐个AfterEnemyBlockCleared→AfterEnemySideTurnStart→逐个EnemyTurnStarted→BeforeEnemySideTurnEndVeryEarly／Early／普通→AfterEnemySideTurnEnd；选招位置仍保留已有边界 |
| CreateSourceEncounterEnemy／RollEncounterEnemyHp | definition=String(64)、min／max=Int(1..9999) → Binding；后者仅min／max → Int | 按当前敌侧成员的不同MaxHp排除候选，niche候选偏移或原区间回退；固定HP也消费一次，非法范围抽样前失败 |
| CreateEncounterEnemy | definition=String(64)、hp=Int(1..9999) → Binding | 给定HP的开发／下层创建，保存单调BirthOrder、CombatId与源个体MonsterSeed，不抽HP或选招 |
| PrepareEnemyBirthContext／SortEncounterEnemies | 无参数 | 前者仅允许非活动战斗且敌侧为空时重置出生计数；后者按SlotIndex与BirthOrder整理SpawnOrder，不修改实例编号及种子 |
| [ResetEnemyMoves](../../../../mod-the-spire/base-spire/enemies/rules/move-graph.spire) | target=Ref(Enemy) | 删除该实例旧历史，按定义初始化图；固定初始move预记历史，但不发布NextMove。生成只建实例与内容私有状态；战斗开始在房间／敌人入场效果前统一建图／重置，不重置随机流 |
| ClearEnemyMoves／RemoveCombatEnemy | target=Ref(Enemy) | 前者只清招式及历史；后者依次移除Power、清招式、发EnemyRemoving供内容清私有状态，再销毁敌人。正常访问关闭复用后者 |
| Wriggler.Create | slot=String(16)、startStunned=Bool → Binding | 验证四个真实槽名后抽HP，登记模块私有出生状态，不初始化图或选招；条件节点不消费AI随机，未知槽位拒绝 |
| PerformEnemyMove | owner=Ref(Player)、target=Ref(Enemy) | 复核双方生命，按保存节点分派ExecuteEnemyMove；完成后标记需要选招，暂停续接不重放正文 |
| SelectNextEnemyMove | target=Ref(Enemy) | 普通玩家BeforePlayerSideTurnStart之后对存活且未准备实例解析初始节点或共享后继，保存MoveId／MoveIndex／Intent及实例历史；随机节点按源顺序与binary32权重消费monster_ai，已准备实例不再选招 |
| EnemyMoveGraphDefinition／EnemyMoveNodeDefinition／EnemyMoveBranchDefinition | 具体参数见[招式图约定](../../../design/mod-the-spire/enemy-turn/spec.md#共享图的当前操作与失败方式) | 固定、条件和随机招式复用同一状态与执行分派；具体敌人模块保存源节点、后继、意图和正文 |

Assassin、Axe、BowlbugEgg、Brute、Crossbow、Stabbot与Tracker均复用共享图。Axe／BowlbugEgg的复合意图为attack,defend，Stabbot为attack,debuff；Tracker的HOUNDS_MOVE调用一次EnemyAttack(amount=1,hits=8)。怪物施加Power通过ApplyPowerWithSource保留applierEnemy，card／play为空。各模块的Spawn动作名和最小HP保持既有定义，旧轮转模板已移除。

遭遇内容用模板生成 `Spawn` 动作：`SingleEnemyEncounter`（definition 是遭遇标识，enemyDefinition／hp 描述唯一怪物）调用共享过程 `SpawnDevelopmentEnemy`；`RubyRaidersFormation` 先建 `EnemyPool` 的五个候选（Axe 20／Assassin 18／Brute 30／Crossbow 18／Tracker 21），再用 `samplePick(stream = base-spire:development-encounter)` 抽三次并逐个移除（不重复），最后清空池。当前遭遇：`base-spire:fuzzy-wurm-crawler-weak`（Monster 房、Weak、单怪）与 `base-spire:ruby-raiders-normal`（Monster 房、三个互不相同的 Ruby Raider，源随机流未迁移）。
| [ObtainRelic](../../../../mod-the-spire/base-spire/relics/rules/lifecycle.spire) | owner=Ref(Player)、definition=String(64) | 在 owner 作用域创建、写 Owner／Holder／DefinitionId、加入 Relics 后发获得事件；过程本身不去重 |
| RemoveRelic | instance=Ref(Relic) | 必须在 Relics；先移出，再发移除事件，再销毁；不持有时报错 |
| RemoveOwnedRelic | instance=Ref(Relic)，公开候选来自 Relics | action，调用 RemoveRelic；参数必须来自实际候选，不能伪造实例字符串 |
| [ReshuffleDiscard](../../../../mod-the-spire/base-spire/cards/rules/draw.spire) | owner=Ref(Player) | DrawPile 空且 DiscardPile 非空才按源条目／升级排序后转移、shuffle、emit AfterShuffle；与初始洗牌共用原版流 base-spire:run:shuffle |
| CanDrawCards | owner=Ref(Player)、fromHandDraw=Bool → Bool | 纯 ShouldDraw 查询；非活跃战斗跳过战斗监听，不含容量或牌堆条件，不改状态或随机 |
| RefreshDrawPermissions | 无 | 战斗活跃状态或 Power 生命周期改变资格后同步所有 Player.DrawBlocked；新增其他资格状态的消费者须在状态改变后调用 |
| DrawCardsWithSource | owner=Ref(Player)、count=Int(min=0)、fromHandDraw=Bool | 命令入口检查ending并征询一次ShouldDraw；逐张复查容量与ending、必要时重洗，空牌堆或满手即返回。回合手牌为true，效果抽牌为false |
| DrawOne | owner=Ref(Player)、fromHandDraw=Bool | 委托DrawCardsWithSource(count=1)。内部DrawNextCard重洗后复查命令资格／手牌上限，再取首张；成功移动后分AfterCardDrawnEarly和普通两轮，普通入口再次检查ending。DrawBlocked仍只表示ShouldDraw资格 |
| Draw／DiscardHand | 无参数，role=player | Draw 使用 first(type(Player))；DiscardHand 将全局 Hand 移到 DiscardPile；均是开发动作 |
| [BeginDevelopmentCombat](../../../../mod-the-spire/base-spire/combat/lifecycle.spire) | owner=Ref(Player) | action 与同名过程：inactive 时先置 Block=0、TurnNumber=0、Energy=0、TurnOpen=false、CombatActive=true，再 emit BeforeCombatStart；已活跃时报 "Development combat is already active"。不是正常战斗初始化 |
| EndDevelopmentCombat | owner=Ref(Player) | action 与同名过程：只在活跃时允许，置 CombatActive=false，不发开始事件也不发胜利；不活跃时报 "Development combat is not active" |
| BeginDevelopmentTurn | owner=Ref(Player) | 内部过程与开发分步action：action只在CanUseDevelopmentActions成立时公开；活跃且回合门关闭时允许；BeforePlayerSideTurnStart→按资格清格挡／AfterBlockCleared→ResetTurnEnergy／AfterEnergyReset→递增TurnNumber／BeforeHandDraw→起手抽牌→AfterPlayerTurnStartEarly／普通／Late→AfterPlayerSideTurnStart；首回合跳过清格挡但仍通知AfterBlockCleared，全部开始阶段结束且战斗仍可继续才打开TurnOpen |
| DrawDevelopmentTurnHand | owner=Ref(Player) | 以 AutoDrawPerTurn 为基础，首回合逐张把 Innate 移到顶部，抽取数至少为 Innate 数且至多十张；调用 DrawCardsWithSource(fromHandDraw=true)，继承满手／来源资格和真实抽牌事件，NoDraw 不阻止回合手牌。不实现附魔底部排序或完整手牌修正 |
| EndDevelopmentTurn | owner=Ref(Player) | 内部过程与开发分步action：action只在CanUseDevelopmentActions成立时公开；活跃且回合门打开时允许，先关闭TurnOpen；自动打出→BeforePlayerSideTurnEndVeryEarly／Early／普通→手牌回合末效果与Flush→ResetCardTurnCosts→AfterPlayerSideTurnEnd→敌方阶段；各阶段入口检查ending，同一快照内部保留命令各自的源门禁 |
| EndTurn | owner=Ref(Player) | 正常完整action与同名过程：先执行玩家收尾和敌方阶段；战斗仍活跃、未ending且未打开新回合时，在同一DSL调用中完成下一普通玩家开始。胜负或选择返回真实边界，不由Host补发开始动作；正常Run及真实事件子战斗只公开完整入口 |
| [AddDevelopmentTarget](../../../../mod-the-spire/base-spire/run/setup-dev.spire) | owner=Ref(Player) | 过程与公开 action：在 owner 作用域创建一个 Hp／MaxHp 为 40、DefinitionId 为 `base-spire:development-target` 的 Enemy；不是遭遇或原版怪物生成 |
| [SetupDevelopmentDraw](../../../../mod-the-spire/base-spire/run/setup-dev.spire) | 无参数；根 start 调用一次 | 创建一个 Player 和弃牌堆三张开发 Card；不提供正常 Run 初始化 |

调用现有共享操作时使用具名参数。例如：

```text
call = { procedure = GainUnpoweredBlock args = { owner = owner amount = 6 } }
```

这里的 owner 必须是明确传入／绑定的 Ref(Player)。`participant(1)` 是 Participant，不能代替实体引用。普通内容不应绕过生命周期直接更新库存或引用字段。

## 永久牌组与战斗副本

[永久牌组规则](../../../../mod-the-spire/base-spire/cards/rules/permanent-deck.spire)仅支持单玩家开发范围，不是正常角色初态或完整获得链。

| 声明／入口 | 参数 | 已实现语义 |
| --- | --- | --- |
| RunDeck | 有序 Card 集合 | 永久实例，公开为 group；与全部战斗牌区分离 |
| Player.UsesRunDeck | Bool=false | 第一次显式获得永久牌后为 true，后续不再使用默认开发占位牌 |
| ObtainPermanentCard | owner: Ref(Player)、definition: String(64)、cost: Int(-1..9)、targeted／exhausts／leavesPlay／strikeTag／isX: Bool、kind: String(16)、baseDamage: Int(0..999) | 只允许存活玩家在房间与战斗外获得；以给定已迁移定义创建永久实例，CanUpgrade=true，置 UsesRunDeck 后发 CardObtained；没有随机池或获得修正 |
| AcquirePermanentCard | owner: Ref(Player)、card: Ref(Card) | 存活且非战斗时，将已创建的同持有者实例加入RunDeck，再发布CardObtained；拒绝已有永久成员与六类战斗牌区成员。CursedPearl用它在获得金币前接入Greed |
| CardDefinition 生成的 Obtain | owner: Ref(Player)；公开 action 无参数 | 以本定义配置调用共享获得过程，例如 Strike.Obtain；每次调用是独立实例 |
| BuildCombatDeck | owner: Ref(Player) | UsesRunDeck=true 时删除本玩家所有旧非永久牌，从 RunDeck 复制已支持字段到 DrawPile；PermanentSource 隐藏引用具体原实例；顺序不变，不自动洗牌／抽牌，也不反写永久牌 |
| HasUpgradablePermanentCards | owner: Ref(Player)，纯过程→Bool | 检查本玩家 RunDeck 中 CanUpgrade=true 且 Upgraded=false 的实例 |
| CanUseUnusedRestSite | owner: Ref(Player)，纯过程→Bool | 当前是营火、存活、非战斗、尚未用过且无 PendingForge；动作与过程复用同一资格判断 |

`BeginDevelopmentCombat` 与 `EnterDevelopmentRestSite` 仅在 Room=none、玩家存活且非战斗时开放。锻造的取消／选牌统一使用 `net:action:net.answer_decision`；不再暴露 ForgeCardInHand。准确寿命和未支持边界见[约定](../../../design/mod-the-spire/permanent-deck-smith/spec.md)。

## 模板与内容配置

[CardDiscardBlockRelic](../../../../mod-the-spire/base-spire/relics/rules/card-discard-block.spire) 接受 definition: String(64)、amount: Int(0..999)，生成 `WhenCardDiscarded`、`OnCardDiscarded` 与 `Obtain`。有效持有实例在 CardDiscarded 后、CombatActive 且非 EnemyPhase 时调用 GainUnpoweredBlock；ToughBandages 配置为 3。DiscardCombatCard 先移动本玩家手牌、发布 CardMoved，再发布 CardDiscarded；普通移牌和回合末 FinishTurnHand 清手不发布弃牌通知。

[AfterShuffleBlockRelic](../../../../mod-the-spire/base-spire/relics/rules/after-shuffle-block.spire) 的参数为 definition: String(64)、amount: Int(0..999)。它生成 `WhenShuffled`、`OnShuffle` 与 `Obtain`，按当前持有库存过滤并调用 GainUnpoweredBlock。`use TheAbacus` 生成 `TheAbacus.Obtain` 等名称；TheAbacus 正式参数为 6。

[BeforeCombatStartBlockRelic](../../../../mod-the-spire/base-spire/relics/rules/before-combat-start-block.spire) 的参数同样是 definition: String(64)、amount: Int(0..999)，生成 `WhenCombatStarts`、`OnCombatStart`（订阅 BeforeCombatStart）与 `Obtain`。它按持有者、DefinitionId 和 IsMelted 过滤后调用同一 GainUnpoweredBlock；`use Anchor` 为 10，`use FakeAnchor` 为 4，两者同时持有在同一次开始中共得 14。

[CardExhaustDamageRelic](../../../../mod-the-spire/base-spire/relics/rules/card-exhaust-damage.spire) 接受 definition: String(64)、amount: Int(0..999)，生成 `WhenCardExhausted`、`OnCardExhausted` 与 `Obtain`。它在 CardExhausted 时按持有者、DefinitionId、IsMelted 和库存顺序绑定有效实例，调用 DamageAllEnemiesFromPlayer；CharonsAshes 配置为 3。

回合资源模板共享 `{ definition: String(64) }`，各自再取一个触发回合、计数周期或伤害数值；它们都生成各自的监听过程与 `Obtain`：

| 模板 | 参数 | 订阅与行为 | 当前内容 |
| --- | --- | --- | --- |
| [TurnStartEnergyRelic](../../../../mod-the-spire/base-spire/relics/rules/turn-start-energy.spire) | amount: Int、turn: Int(1..999) | AfterPlayerSideTurnStart，且 `TurnNumber == turn` 时 GainEnergy | Lantern（1 能量，第 1 回合） |
| [TurnStartHealRelic](../../../../mod-the-spire/base-spire/relics/rules/turn-start-heal.spire) | amount、turn | AfterPlayerTurnStartLate，且 `TurnNumber == turn` 时 Heal | BloodVial（2 生命，第 1 回合） |
| [BlockClearedBlockRelic](../../../../mod-the-spire/base-spire/relics/rules/block-cleared-block.spire) | amount、turn | AfterBlockCleared，且前一编号 `TurnNumber == turn - 1` 时 GainUnpoweredBlock | HornCleat（14 格挡，第 2 回合） |
| [EveryTurnsEnergyRelic](../../../../mod-the-spire/base-spire/relics/rules/every-turns-energy.spire) | amount、turns: Int(1..999) | AfterPlayerSideTurnStart：实例 Counter 加一并取模 turns，为 0 时 GainEnergy；Counter 跨战斗保留 | HappyFlower（每 3 回合 1 能量） |
| [TurnStartDamageAllEnemiesRelic](../../../../mod-the-spire/base-spire/relics/rules/turn-start-damage-all-enemies.spire) | amount: Int(0..999) | AfterPlayerTurnStart：对每个 `Hp > 0` 的 Enemy 调用 DamageEnemy；没有目标时无事发生也不消耗随机 | MercuryHourglass（3 点无加成伤害） |

卡牌内容由**一次** [CardDefinition](../../../../mod-the-spire/base-spire/cards/rules/card-definition.spire)与[正文模板](card-behavior.md#定义正文与纯值)或显式组合正文构成，绑定唯一ExecuteCardBody及ReadCardDamage。普通攻击、格挡和自身Power优先使用正文模板；组合牌按call顺序调用[效果模板](../../../../mod-the-spire/base-spire/cards/rules/effects.spire)，并用StaticCardDamage绑定静态公式，动态公式保留自己的pure绑定。效果过程保留定义过滤，主效果不再订阅广播。CardDefinition 生成 `AddToHand`（开发授予，直接放进手牌）与升级订阅。

实例克隆后放置复用[CloneCombatCardToPile及ClonePlayedCardEffect](card-behavior.md#复制附魔与永久牌)，保留复制通知、费用与附属状态、满手回退和移动原因。普通／升级数值用CardUpgradeValue计算，目标卡的自身增益也使用ApplySelfPowerEffect；无需在具体牌里重复声明同一效果模板。

| 效果模板 | 参数 | 正文显式调用后的行为 | 当前内容 |
| --- | --- | --- | --- |
| AttackEffect | definition: String(64)、hits: Int(1..9) | 读取CardBaseDamage并创建一个hits段的CardAttack | Strike（6／9）、TwinStrike（5／7×2）、PommelStrike（9／10）、IronWave（5／7）、Anger（6／8）、Bludgeon（32／42） |
| ClonePlayedCardEffect | definition、destination（默认discard） | 克隆当前牌并通过CloneCombatCardToPile放置，以clone记录移动原因 | Anger；Juggling从监听中调用同一放置过程 |
| AttackAllEnemiesEffect | definition: String(64)、hits／upgradedHits: Int(1..9) | 创建一个all目标策略的CardAttack，按击次再按目标结算 | Thunderclap（4／7，1 段）、HowlFromBeyond（16／21，1 段）、Breakthrough（9／13，1 段）、Conflagration（2／2，4／5 段） |
| BlockEffect | block、upgradedBlock | 按 `Upgraded` 取值后 GainBlock | Defend（5／8）、ShrugItOff（8／11）、IronWave（5／7）、Impervious（30／40）、Taunt（7／8）、BloodWall（16／20） |
| DrawEffect | cards、upgradedCards | 按 `Upgraded` 取值后 DrawCards | PommelStrike（1／2）、ShrugItOff（1／1）、Offering（3／5） |
| EnergyEffect | energy、upgradedEnergy | 按 `Upgraded` 取值后 GainEnergy | Bloodletting（2／3）、Offering（2／2） |
| LoseHpEffect | hp、upgradedHp | 按 `Upgraded` 取值后 LosePlayerHpFromCard并显式传play | Bloodletting（3／3）、Offering（6／6）、BloodWall（2／2） |
| ApplyEnemyPowerEffect | power、stacks、upgradedStacks | 按 `Upgraded` 取值后 ApplyEnemyPowerFromCard到解析出的目标 | Bash（Vulnerable 2／3）、Uppercut（Weak 与 Vulnerable 各 1／2）、Taunt（Vulnerable 1／2）、Tremble（Vulnerable 3／4） |
| ApplyAllEnemiesPowerEffect | power、stacks、upgradedStacks | 对每个 `Hp > 0` 的开发目标调用ApplyEnemyPowerFromCard | Shockwave（Weak 与 Vulnerable 各 3／5）、Thunderclap（Vulnerable 1／1） |
| ApplySelfPowerEffect | power、stacks、upgradedStacks、untilTurnEnd（默认false） | 通过CardUpgradeValue取值，按到期标志调用对应的带来源Power操作；不要求目标敌人 | Inflame（Strength 2／3）、FightMe、Unrelenting、FlameBarrier（untilTurnEnd=true） |

卡牌不再在本地声明选择消耗、手牌变换、生成改费、吸收伤害或条件攻击等效果模板。直接调用[供内容与mod组合的公共操作](card-behavior.md#供内容与mod组合的公共操作)：手牌操作管理选择、批次及实例生命周期，战斗数值库提供条件读取、成长和致死结果查询。BodySlam直接绑定共享格挡公式；Spite／EvilEye／Dismantle把条件值传入已有攻击／格挡命令，MoltenFist传入Power与倍率。通用操作不要求当前已有两个消费者。

同一张牌内的效果顺序由所属ExecuteCardBody处理过程的call顺序决定，与模板use或事件订阅顺序无关。Bash、Uppercut的正文显式先伤害后施加。Weak、Vulnerable与Frail在所属Power模块监听AfterEnemySideTurnEnd并调用TickPowerDuration，玩家新实例的首次跳过由PowerBlueprint.PowerType与施加时点控制；详见[Power参考](powers.md)。

源条件为“`TurnNumber <= 1`”的两个遗物在这里写成 `turn = 1`：开发战斗从 0 回合开始，第一次开始时回合号一定是 1，两者等价。原版 `AfterSideTurnStart`、`AfterPlayerTurnStart` 与 `AfterPlayerTurnStartLate` 的区别保留成三个开发时点，所需能量／伤害修正、开发胜负与回合结束钩子已恢复；完整正常 Run 顺序和其余参与者不属于本范围。

战斗开始模板 `BeforeCombatStartPowerRelic` 的参数为definition、power、stacks，在 BeforeCombatStart 时对持有者调用 ApplyPlayerPower；`use BronzeScales` 授予 Thorns 3，`use Vajra` 授予 Strength 1，`use OddlySmoothStone` 授予 Dexterity 1。

`FirstSideStartEnemyPowerRelic` 在BeforePlayerSideTurnStart且上一编号为0时，对每个活动敌人调用ApplyPowerWithSource，保留持有玩家作为施加者（`use BagOfMarbles` 施加Vulnerable1）。`EnemyDiedEnergyDrawRelic` 的参数为definition、energy、cards，订阅EnemyDied后对持有者给能量并抽牌（`use GremlinHorn` 为1／1）；末杀抽牌依Draw命令拒绝ending。

回合末与拾取类模板：`TurnEndNoBlockRelic`（BeforePlayerSideTurnEndEarly保存每实例Block为0的资格，普通阶段先清标志再给无加成格挡，`use Orichalcum` 为6；移除清理状态、新回合重置）、`EveryTurnsDrawRelic`（AfterPlayerSideTurnStart 计数取模，归零时抽牌，`use Pendulum` 为 3 回合 1 张）、`ObtainMaxHpRelic`（获得时提高 MaxHp 并回复同量，`use Strawberry` 为 7）、`CombatEndHealRelic`（胜利且 Hp 不超过 MaxHp 的阈值百分比时回复；early=true 订阅 CombatVictoryEarly，否则订阅 CombatVictory；`use MeatOnTheBone` 为 12／50 且 early=true）。显式EndDevelopmentCombat和下次BeginDevelopmentCombat通过ClearCombatPowers调用各实例的RemovePower；开发场景的自然胜负保留原能力清理边界；正常胜利在结束通知后清理能力、格挡和战斗牌，再发胜利通知并生成奖励。ExpireTurnPower订阅AfterEnemySideTurnEnd，移除带ExpiresAtTurnEnd的能力（对应源 `FlameBarrierPower` 的当回合寿命），因此在敌方攻击期间仍会反击。

药水：`PotionDefinition` 生成 `Obtain`（创建实例后调用共享ObtainPotion放进三格库存），效果模板按 DefinitionId 订阅 `PotionUsed`／`PotionUsedOnTarget`：`PotionBlockEffect`（无加成格挡，BlockPotion 12）、`PotionEnergyEffect`（EnergyPotion 2）、`PotionDrawEffect`（SwiftPotion 3）、`PotionMaxHpEffect`（FruitJuice 5，提高上限并回复同量）、`PotionHealPercentEffect`（BloodPotion 20% 上限）与 `PotionDamageEffect`（FirePotion 20 点无加成伤害）。FlexPotion独有监听调用ApplyPowerWithSource施加5层FlexPotionPower；实际力量及到期顺序见[Power参考](powers.md)。

模板开发获得动作通过definition检查重复，默认使用first(type(Player))；不能将它当作完整多人获得、替换或熔化操作。监听按实际持有者／实例过滤，跨战状态按所属模板约定处理。Obtain的库存去重不代表该遗物的全部原版来源资格（奖励、事件、商店或角色初态）已经实现。

[内容目录的 definition](../../../dsl/core/0.3/package-reference.md) 指向完整 DSL 内容模块；独有规则／状态留在该文件，共用过程／模板放领域规则库。示例新增 AfterDrawBlockRelic 位于 docs/examples，仅用于[第二篇教程](../../../start/shared-rule.md)，不是正式包现有公共操作。

## 公开投影

[PlayerView](../../../../mod-the-spire/base-spire/public/projection.spire)声明公开实体类型；[公开元数据](../../../../mod-the-spire/base-spire/public/metadata.json)维护各类型的DefinitionId列表与集合用途。准确内容列表直接读取该文件，不在本页复制支持名单。Power.Definition、PowerBlueprint字段及所属模块私有字段保持Hidden；Power实例的标识和层数继续公开。

开发牌堆、InPlay、ExhaustPile、RemovedFromCombat、Powers和定义集合按metadata各自声明发布或具名不发布。集合不发布不等于可忽略实体上的公开事实；新增集合必须明确用途。当前声明不构成完整原版牌序、能力生命周期或正常Run约定。

公开 action_type 为 `net:action:base-spire:TheAbacus.Obtain`，格挡字段为 `net:field:base-spire:Player.Block`；生命、能量与回合见 `net:field:base-spire:Player.Hp`／`Player.Energy`／`Player.TurnNumber`／`Player.TurnOpen`／`Player.CombatActive`，目标生命见 `net:field:base-spire:Enemy.Hp`，能力层数见 `net:field:base-spire:Power.Stacks`，战斗结果见 `net:field:base-spire:Player.CombatOutcome`，回合内标志见 `net:field:base-spire:Player.LostHpThisTurn`／`Player.ExhaustedCardThisTurn`，公开动作见 `net:action:base-spire:BeginDevelopmentCombat`／`EndDevelopmentCombat`／`BeginDevelopmentTurn`／`EndDevelopmentTurn`／`EndTurn`／`AddDevelopmentTarget`／`PlayCardFromHand`／`PlayAttackFromHand`／`UpgradeCardInHand`。协议用真实候选 ID 提交动作；元数据不改变状态、触发顺序或合法动作守卫。
