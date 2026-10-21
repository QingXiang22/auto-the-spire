# 共享 DSL 操作参考

文档类型：参考（Reference）。适用于 core/0.3 与纯 DSL base 的开发态战斗包。只列实际存在的声明；这里没有列出的游戏操作不能按名字猜测可调用。文件路径相对 `mod-the-spire/base-spire/`，精确实现通过各行链接定位。铁甲战士新增费用／选择／历史／生成／复制／自动出牌与 Power 反应的完整签名见[专项操作参考](../../../design/mod-the-spire/ironclad-dsl/shared-operations.md)。

Power定义、施加命令、通知顺序及私有清理的准确签名见[Power参考](powers.md)。卡牌普通攻击的基础值位于CardDefinition；下方效果模板表不再接受重复伤害值或Power行为kind。

有序正文、显式执行、攻击结果、费用、牌区、附魔及事件的新增接口见[行为操作参考](card-behavior.md)。

## 状态与事件

拾取时随机升级永久牌使用[`UpgradeRandomPermanentCards(owner,kind,amount)`](../../../../mod-the-spire/base-spire/cards/rules/random-permanent-upgrade.spire)：owner为Ref(Player)，kind只接受attack／skill／all，amount为Int(1..999)。操作拥有资格筛选、SourceCardSortKey、完整Niche洗牌、原实例UpgradeCard及临时状态清理；不改RunDeck顺序，不触发战斗洗牌事件。调用方法和边界见[遗物指南](../how-to/powers-relics-potions.md)。

能量 X 的实际支付查询为 `GetCardEnergyCost(owner: Ref(Player), card: Ref(Card)) → Int`，效果查询为 [`ResolveEnergyXValue(owner,card) → Int`](../../../../mod-the-spire/base-spire/cards/rules/operations.spire)，两者均为pure。`CardPlayRecord.RawX` 保存每次执行关联的序列捕获值，`Card.PaidX`保留兼容读取，效果通过 `ModifyXValue(value: Int, owner: Ref(Player), card: Ref(Card))` 征询实例监听，不修改能量或捕获值；非 X 效果查询返回0。自动出牌不扣费但捕获当前能量，重复出牌不重新捕获；准确顺序及 ChemicalX 消费者见[遗物生命周期](../../../design/mod-the-spire/relic-combat-start/content-lifecycle.md#效果-x-修正dsl-relic-content-03)。

角色初态入口 `Ironclad.Initialize(owner: Ref(Player))` 与同名公开 action 只接受干净开发初态，建立源资源、永久牌组与起始遗物；`CanInitializeCharacter(owner)` 是共用 pure 守卫。`Player.CharacterId` 为 String(64)，默认 `none`；`AutoDrawPerTurn` 为 Int(0..10)，默认 0，初始化后为 5。源顺序、初始化拒绝与未支持范围见[角色初态约定](../../../design/mod-the-spire/character-combat/ironclad-start.md)。

| 声明 | 字段／载荷 | 约束与归属 |
| --- | --- | --- |
| [Player](../../../../mod-the-spire/base-spire/combat/schema.spire) | Owner: Participant；Hp／MaxHp: Int=80；Energy: Int=0；MaxEnergy: Int=3；TurnNumber: Int=0；TurnOpen: Bool=false；Block: Int(0..999)=0；DrawBlocked: Bool=false；CombatActive: Bool=false；CombatOutcome: String(16)=`none`；LostHpThisTurn／ExhaustedCardThisTurn: Bool=false | 开发玩家；CombatOutcome 是开发态结果（none／victory／defeat），不是协议终局；TurnOpen 是开发态回合门；两个 ThisTurn 标志在回合开始时复位，逐次 CardPlays 和 HpLossCount 补充所需历史 |
| [Card](../../../../mod-the-spire/base-spire/cards/schema.spire) | Owner: Participant；Label: String(64)；DefinitionId: String(64)=`base-spire:development-card`；Cost: Int(0..9)=0；Upgraded: Bool=false；CanUpgrade: Bool=false；PermanentSource: 隐藏可选 Ref(Card)；Targeted: Bool=false；Exhausts: Bool=false；LeavesPlay: Bool=false | Targeted 是原版 TargetType 的开发替身；Exhausts 是 CardKeyword.Exhaust 的开发替身；LeavesPlay 是 CardType.Power 的开发替身（结算后离开战斗）；Rarity 字段保存源稀有度，奖励资格与战斗生成资格分别维护 |
| [Enemy](../../../../mod-the-spire/base-spire/combat/schema.spire) | Owner: Participant；DefinitionId: String(64)；Hp／MaxHp: Int=0；Block: Int(0..999)=0；MoveIndex: Int(0..9)=0；Intent: String(32)=`none` | 开发敌人：MoveIndex 是它自己轮转里的招位（含义由敌人模板拥有），Intent 是公开的下一个意图名（不含伤害数值）；没有移除、死亡钩子、遭遇编队或原版 AI 选择 |
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
| AfterEnergyReset | owner: Ref(Player) | ResetTurnEnergy重置／补足能量后发出，早于打开回合门和AfterSideTurnStarted；ArtOfWar在此奖励 |
| [AfterSideTurnStarted](../../../../mod-the-spire/base-spire/combat/lifecycle.spire) | owner: Ref(Player) | AfterEnergyReset完成并打开回合门后发出，早于起手抽牌；TurnStartEnergyRelic、EveryTurnsEnergyRelic与Sai在此奖励 |
| AfterPlayerTurnStart | owner: Ref(Player) | AfterSideTurnStarted和起手抽牌后发出，对应原版 AfterPlayerTurnStart（原版按 Early、正常、Late 三轮遍历） |
| AfterPlayerTurnStartLate | owner: Ref(Player) | 紧随 AfterPlayerTurnStart 发出，对应原版 AfterPlayerTurnStartLate；三个玩家回合开始时点都在同一执行栈内按声明顺序完成 |
| AfterTurnEnded | owner: Ref(Player) | 回合末自动打出后弃掉手牌，再发出；临时玩家侧 Power 随后清理，debuff 的减层在敌方阶段结束时发生 |
| EnemyDied | target: Ref(Enemy) | 敌人生命从正数到 0 时发出一次；原版的移除生物与死亡钩子未迁移 |
| CombatEnded | owner: Ref(Player)、victory: Bool | 结束态已经写入 `CombatOutcome` 并关闭回合门与活跃标记之后发出；胜利后可生成开发金币待领项；没有正常 Run 结算 |
| EnemyTurnStarted | owner: Ref(Player)、target: Ref(Enemy) | 玩家回合结束后，对每个 `Hp > 0` 的敌人各发一次；敌人内容模板按 DefinitionId 订阅并实现自己的招 |
| AfterEnemyTurnEnded | owner: Ref(Player) | 敌方阶段结束时发出；共享规则在这里做 debuff 减层（源在敌方回合末），取代此前挂在玩家回合末的做法 |
| AfterShuffle | owner: Ref(Player) | 仅在实际转移并完成 shuffle 后发出 |
| AfterCardDrawn | owner: Ref(Player)、card: Ref(Card) | 成功移到 Hand 后发出；未抽到牌不发 |
| [CardPlayed](../../../../mod-the-spire/base-spire/cards/schema.spire) | owner: Ref(Player)、card: Ref(Card) | 旧声明保留，当前正文管线不发布；新增主效果绑定ExecuteCardBody，外部反应使用Before／After通知 |
| CardPlayedOnTarget | owner、card、target: Ref(Enemy) | 旧声明保留，当前正文管线不发布；目标通过ExecuteCardBody的target传入；兼容入口选首个存活敌人，PlayTargetedCard使用原生决策 |
| CardUpgraded | card: Ref(Card) | 实例标记为升级后发出；卡片定义用它应用定义层费用、伤害参数和 Innate 升级 |
| CardExhausted | owner: Ref(Player)、card: Ref(Card) | 任何受支持的完整耗尽操作把牌移入 ExhaustPile 后发出；订阅时可确认卡已在消耗堆 |

## 过程与公开动作

遗物与Power统一使用[通知与修正监听](../../../design/mod-the-spire/effect-listeners.md)：通知通过emit，计算通过foldEvent；订阅声明instances与instance_order，过程接收载荷和末尾listener实例。主流程不读取遗物专属字段或直接调用Power效果过程。具体监听与私有状态位于powers/shared对应模块；通用定义与生命周期位于powers/rules。

遗物共享生命周期提供以下实际入口，参数名区分大小写；内容仅配置模板，状态在Engine遗物实例中。详细时序见[遗物生命周期约定](../../../design/mod-the-spire/relic-combat-start/content-lifecycle.md)。

| 遗物入口 | 参数 | 行为与边界 |
| --- | --- | --- |
| HasOwnedRelic／HasActiveRelic | owner: Ref(Player)、definition: String(64)，pure→Bool | 前者检查库存标识，包含熔化实例；后者还要求未熔化。防止重复拾取使用Owned，动态效果使用Active |
| CombatModifierRelic模板 | definition、strikeDamage: Int(0..999)=0、retainEnergy: Bool=false | 监听ModifyAttackAdditive和ShouldResetEnergy返回修正值，不配置公共Relic效果字段；供StrikeDummy／FakeStrikeDummy／IceCream复用 |
| NoAttackTurnEnergyRelic模板 | definition、energy: Int(0..999) | ArtOfWar.State按遗物实例保存私有标志，获得时创建、移除时清理；AfterCardPlayed记录本拥有者攻击，AfterTurnEnded转交；AfterEnergyReset在非首回合且上回合无攻击时GainEnergy，再清理标志；ArtOfWar配置1 |
| ObtainMaxHpRelic模板 | definition、amount、fullHeal=false | 通过RelicObtained触发IncreaseMaxHp，必要时HealToMaximum；不在动作尾部重复奖励。供Strawberry／Pear／Mango／LeesWaffle复用 |
| CardCountRelic模板 | definition、kind、every、perTurn，以及energy／block／damage／power／stacks | kind筛选Card.Kind；AfterCardPlayed按实际次数推进独立Relic.Counter。perTurn决定重置或跨战保留，重复／自动各计一次、多段只计一次。源码仍声明未使用的powerKind默认参数，新配置不传它，行为只按power定义查找 |
| TurnStartEnergyRelic模板 | definition: String(64)、amount: Int(0..999)、turn: Int(1..999)、throughTurn: Bool=false | AfterSideTurnStarted调用GainEnergy；默认TurnNumber==turn，throughTurn=true时<=turn；NoEnergyGain抑制奖励 |
| EveryTurnsEnergyRelic模板 | definition: String(64)、amount: Int(0..999)、turns: Int(1..999) | 每次本持有者回合开始将本实例Counter加1模turns；归零GainEnergy，计数跨战保留且不受NoEnergyGain抑制 |
| TurnStartBlockRelic模板 | definition: String(64)、amount: Int(0..999) | AfterSideTurnStarted每回合调用GainUnpoweredBlock，包含首回合；不使用敏捷／脆弱修正 |
| GainGold | owner: Ref(Player)、amount: Int(0..99999) | 共享已迁移金币获得，沿用99999上限；尚无完整获得金币修正参与者，金币奖励与OldCoin共用 |
| HealToMaximum | owner: Ref(Player) | 计算MaxHp-Hp并调用Heal；支持最大生命至999999，不替代复活或完整治疗修正 |
| BeforePlayerTurnStart／BeforePlayerTurnEnd | owner: Ref(Player)，事件 | 前者在回合号增加后、抽牌／自动出牌前；后者在回合末自动出牌后、普通弃牌前。StoneCalendar致胜后不再执行敌方行动 |

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
| [GainUnpoweredBlock](../../../../mod-the-spire/base-spire/combat/block.spire) | owner=Ref(Player)、amount=Int(0..999) | 累加并截到 999，不应用卡牌／Power 的加性或乘性修正；不是通用获得格挡流程 |
| GainBlock | owner=Ref(Player)、amount=Int(0..999) | 卡牌格挡链：先由Dexterity贡献加性修正，再在持有正层Frail时把总量乘 0.75 向下取整，再按 Unmovable 的本回合历史决定是否翻倍，最后截到 999 且不低于 0；遗物／Power 的无加成格挡仍走 GainUnpoweredBlock |
| [ApplyPlayerPower](../../../../mod-the-spire/base-spire/powers/rules/lifecycle.spire) | owner=Ref(Player)、definition=String(64)、stacks=Int(-999..999) | 先查定义，未知即失败；同持有者同定义合并，否则创建。定义决定负值／Single策略；零层移除，事件交给所属模块处理 |
| ApplyEnemyPower | target=Ref(Enemy)、definition、stacks | 对敌人执行同一合并语义 |
| [Heal](../../../../mod-the-spire/base-spire/combat/resources.spire) | owner=Ref(Player)、amount=Int(0..999) | Hp 增加并按 MaxHp 截断；不处理复活、治疗修正或死亡，也不改变 MaxHp |
| GainEnergy | owner=Ref(Player)、amount=Int(0..999) | Energy 增加并可超过 MaxEnergy（上限 999），尊重 NoEnergyGain；回合重置使用 GetMaxEnergy |
| [DamageEnemy](../../../../mod-the-spire/base-spire/combat/damage.spire) | target=Ref(Enemy)、amount=Int(0..999999) | 先扣 `min(Block, amount)` 格挡，剩余从 Hp 扣除且不低于 0；无加成伤害，修正参与者不参与（遗物与 Power 用它） |
| EnemyIsHittable | target: Ref(Enemy)，pure，返回 Bool | Hp<=0 直接返回 false；存活敌人以 true 为初值征询 ShouldAllowHittingEnemy |
| ShouldAllowHittingEnemy | value: Bool、target: Ref(Enemy)，pure修正事件 | 供命中资格监听返回当前目标是否允许命中；不写状态或消费随机 |
| DamageAllEnemiesFromPlayer | owner: Ref(Player)、amount: Int(0..999999) | 对当前 EnemyIsHittable 目标执行无加成群伤；整批提交生命／格挡后才逐项通知伤害和死亡。无目标直接返回；其他攻击、选敌和胜利条件仍沿各自现有资格 |
| DamageEnemyPowered | owner: Ref(Player)、card: Ref(Card)、target: Ref(Enemy)、amount: Int(0..999999) | 以显式攻击者和牌源计算Strength、Strike及升级牌遗物加成，再应用Weak、目标Vulnerable和攻击者Cruelty，最终一次向下取整，再按格挡→生命结算；BodySlam等计算伤害也走此链 |
| ModifyVulnerableMultiplier | value: Int、owner: Ref(Player)、card: Binding、target: Binding；pure修正事件 | 正层目标Vulnerable在玩家加成攻击链以150百分数点征询；PaperPhrog加25，之后叠加Cruelty，再乘伤害；无目标状态不征询 |
| ModifyEnemyWeakMultiplier | value: Int、owner: Ref(Player)、attacker: Ref(Enemy)；pure修正事件 | 正层攻击者Weak在敌方加成攻击链以75百分数点征询；owner为防守玩家，PaperKrane减15；无攻击者状态不征询 |
| UpgradedAttackDamageRelic模板 | definition: String(64)、amount: Int(0..999) | ModifyAttackAdditive对非空且已升级牌源贡献加值；Holder是攻击者或牌拥有者即可，二者不要求同时满足；MiniatureCannon配置3 |
| StatusMultiplierRelic模板 | definition: String(64)、vulnerableBonus: Int(0..100)=0、weakReduction: Int(0..75)=0 | 有效持有实例订阅两个状态倍率站点；PaperPhrog配置25、PaperKrane配置15，虚弱倍率最低0；不改变状态层数／时长 |
| ResetTurnEnergy／AfterEnergyReset | owner: Ref(Player)，过程／事件 | 首回合重置；后续有有效保留能量资格时将最大能量加到余能，上限999，否则重置。该操作绕过禁止获得能量，随后发事件；额外奖励仍调用GainEnergy |
| DamagePlayer | owner=Ref(Player)、amount=Int(0..999999) | 对玩家执行格挡→生命结算，正的未阻挡伤害发 PlayerHpLost 并检查开发失败；不改变 MaxHp |
| [LosePlayerHp](../../../../mod-the-spire/base-spire/combat/damage.spire) | owner=Ref(Player)、amount=Int(0..999) | 绕过格挡的生命损失，对应源 `Unblockable | Unpowered` 伤害（Offering／Bloodletting）；不应用伤害修正、不触发受击反应，只保留生命下限 0 |
| [PlayCard](../../../../mod-the-spire/base-spire/cards/rules/play.spire) | owner=Ref(Player)、card=Ref(Card) | 校验战斗／回合、手牌、共享资格与能量；付款、进入InPlay、执行唯一正文；全部重复完成后提交捕获的结果区 |
| PlayAttackCard | owner、card | 同上，但把敌人解析为第一个 `Hp > 0` 的开发目标后调用唯一正文；无目标不是合法候选 |
| UpgradeCard | card=Ref(Card) | 只允许 CanUpgrade=true 且未升级的实例；置 `Upgraded=true` 并发 CardUpgraded，定义层费用变化由 CardDefinition 应用；只改当前实例 |
| [DrawCards](../../../../mod-the-spire/base-spire/cards/rules/draw.spire) | owner=Ref(Player)、count=Int(0..9) | 逐张调用 DrawOne，保留重洗、手牌上限、禁止抽牌与 AfterCardDrawn 的既有顺序；不是一次抽多张的新算法 |
| PlayCardFromHand／PlayAttackFromHand／UpgradeCardInHand | 参数 card=Ref(Card)，候选来自 Hand | 公开 action：分别打出无目标牌、打出有目标牌与升级手牌；PlayCardFromHand 只提供 `Targeted=false` 的候选，PlayAttackFromHand 只提供 `Targeted=true` 的候选 |
| [PlayCard](../../../../mod-the-spire/base-spire/cards/rules/play.spire) 结算收尾 | — | 效果结算后按 `Exhausts`→ExhaustPile、`LeavesPlay`→RemovedFromCombat、否则 DiscardPile 分流；只有 Exhausts 分支发 CardExhausted |
| [EnterDevelopmentRestSite](../../../../mod-the-spire/base-spire/rooms/rest-site.spire) | owner=Ref(Player) | 公开 action：把房间置为 `rest-site` 并清掉已使用标记；不是地图节点入口 |
| RestAtDevelopmentCampfire | owner=Ref(Player) | 公开 action 与同名过程：只在营火且未使用时允许，回复 `floor(MaxHp × 0.3)` 后把选项标记为已用；过程在非营火或重复使用时失败 |
| ForgeAtDevelopmentCampfire | owner=Ref(Player) | 公开 action 与同名过程：未用过的营火、存活且非战斗，有本玩家可升级永久牌时发出 Engine 的 0..1 实例决策；选择升级该永久实例并用掉选项，取消不消耗；成功后发 AfterRestSiteSmith |
| LeaveDevelopmentRoom | owner=Ref(Player) | 公开 action 与同名过程：只有成功用过营火选项、没有选择待决且非战斗时可离开，房间复位为 `none`；奖励界面必须使用 CloseDevelopmentRewards |
| [CombatEnded 奖励订阅](../../../../mod-the-spire/base-spire/rewards/rules/combat-gold.spire) | owner=Ref(Player)、victory=Bool | 存活玩家胜利时，Ironclad调用完整普通奖励操作；未选角色的金币探针仅从命名流 `base-spire:player:rewards` 的 `[10, 21)` 抽金额；写入待领状态、Room=`rewards`；Gold 不变；失败不生成也不抽随机 |
| ClaimDevelopmentGoldReward | owner=Ref(Player) | 公开 action 与同名过程：只在 rewards 且条目 pending 时把保存金额加进 Gold（开发上限 99999），清空条目；领取不关闭界面，不重抽，不能重复领取 |
| CloseDevelopmentRewards | owner=Ref(Player) | 公开 action 与同名过程：只在 rewards 时清空未领条目、金额与房间状态；允许不领取直接关闭，不给金币也不抽随机；关闭前禁止开始新战斗、进入营火或 LeaveDevelopmentRoom |
| [CheckCombatVictory](../../../../mod-the-spire/base-spire/combat/lifecycle.spire) | owner=Ref(Player) | 只在敌人死亡后调用：若已无 `Hp > 0` 的敌人且战斗仍活跃，写 victory、关闭回合门与活跃标记并发 CombatEnded |
| CheckCombatDefeat | owner=Ref(Player) | 每次玩家掉血后调用：生命为 0 且战斗仍活跃时写 defeat 并关闭状态，再发 CombatEnded |
| [DamagePlayerPowered](../../../../mod-the-spire/base-spire/combat/enemy-turn.spire) | attacker=Ref(Enemy)、owner=Ref(Player)、amount | 敌方攻击链：防守方 `after-attacked-damage`（Thorns）先以无加成伤害反击攻击者，再按攻击者 Strength→攻击者 Weak ×0.75→防守方 Vulnerable ×1.5（整数向下取整）→格挡→生命，并做失败判定 |
| RunEnemyTurn | owner=Ref(Player) | 敌方阶段：对每个可命中敌人先清空其格挡（源 `Creature.AfterTurnStart` 的敌侧语义），再发 EnemyTurnStarted；最后发 AfterEnemyTurnEnded |
| [ResetEnemyMoves](../../../../mod-the-spire/base-spire/enemies/rules/move-graph.spire) | target=Ref(Enemy) | 删除该实例旧历史，按定义图保存初始招式；固定首招不抽样。生成和BeforeCombatStart复用，不重置随机流 |
| PerformEnemyMove | owner=Ref(Player)、target=Ref(Enemy) | 复核双方生命，按保存节点分派ExecuteEnemyMove；完成后标记需要选招，暂停续接不重放正文 |
| SelectNextEnemyMove | target=Ref(Enemy) | 对存活且未准备实例解析共享后继，保存MoveId／MoveIndex／Intent及实例历史；随机节点按源顺序与binary32权重消费monster_ai，已准备实例不再选招 |
| [SingleAttackEnemy](../../../../mod-the-spire/base-spire/enemies/rules/single-attack-enemy.spire) 模板 | definition、hp、damage | 生成 `WhenEnemyTurn`（每回合对玩家造成固定伤害并保持 attack 意图）与 `Spawn`（用源最小 HP 创建） | TwigSlimeS（7 HP，4 伤害）、AssassinRubyRaider（18 HP，10 伤害） |
| AttackDefendEnemy | definition、hp、damage、block | 攻击后给自身格挡（`GainEnemyBlock`）并保持 attack 意图 | BowlbugEgg（21 HP，7 伤害＋7 格挡） |
| AttackBuffEnemy | definition、hp、damage、strength | 攻击与自我强化交替（MoveIndex 0／1） | BruteRubyRaider（30 HP，7 伤害／+3 强度） |
| AttackDebuffEnemy | definition、hp、damage、frail | 攻击后给玩家施加 `block-dealt-multiplier`（Frail） | Stabbot（18 HP，11 伤害＋1 Frail） |
| AttackDefendRotationEnemy | definition、hp、damage、block、bigDamage | 三段轮转：两次「攻击＋格挡」，再一记大攻击 | AxeRubyRaider（20 HP，5＋5 格挡 ×2，然后 12） |
| BlockThenAttackEnemy | definition、hp、block、damage | 先格挡再攻击，交替 | CrossbowRubyRaider（18 HP，3 格挡／14 伤害） |
| DebuffThenMultiAttackEnemy | definition、hp、frail、damage、hits | 先施加 Frail，之后每回合多段攻击 | TrackerRubyRaider（21 HP，Frail 2 后 1 伤害 ×8） |

遭遇内容用模板生成 `Spawn` 动作：`SingleEnemyEncounter`（definition 是遭遇标识，enemyDefinition／hp 描述唯一怪物）调用共享过程 `SpawnDevelopmentEnemy`；`RubyRaidersFormation` 先建 `EnemyPool` 的五个候选（Axe 20／Assassin 18／Brute 30／Crossbow 18／Tracker 21），再用 `samplePick(stream = base-spire:development-encounter)` 抽三次并逐个移除（不重复），最后清空池。当前遭遇：`base-spire:fuzzy-wurm-crawler-weak`（Monster 房、Weak、单怪）与 `base-spire:ruby-raiders-normal`（Monster 房、三个互不相同的 Ruby Raider，源随机流未迁移）。
| [ObtainRelic](../../../../mod-the-spire/base-spire/relics/rules/lifecycle.spire) | owner=Ref(Player)、definition=String(64) | 在 owner 作用域创建、写 Owner／Holder／DefinitionId、加入 Relics 后发获得事件；过程本身不去重 |
| RemoveRelic | instance=Ref(Relic) | 必须在 Relics；先移出，再发移除事件，再销毁；不持有时报错 |
| RemoveOwnedRelic | instance=Ref(Relic)，公开候选来自 Relics | action，调用 RemoveRelic；参数必须来自实际候选，不能伪造实例字符串 |
| [ReshuffleDiscard](../../../../mod-the-spire/base-spire/cards/rules/draw.spire) | owner=Ref(Player) | DrawPile 空且 DiscardPile 非空才按源条目／升级排序后转移、shuffle、emit AfterShuffle；与初始洗牌共用原版流 base-spire:run:shuffle |
| DrawOne | owner=Ref(Player) | 未禁止抽牌且手牌不足 10 时先尝试重洗，再取首张，成功移动后 emit AfterCardDrawn |
| Draw／DiscardHand | 无参数，role=player | Draw 使用 first(type(Player))；DiscardHand 将全局 Hand 移到 DiscardPile；均是开发动作 |
| [BeginDevelopmentCombat](../../../../mod-the-spire/base-spire/combat/lifecycle.spire) | owner=Ref(Player) | action 与同名过程：inactive 时先置 Block=0、TurnNumber=0、Energy=0、TurnOpen=false、CombatActive=true，再 emit BeforeCombatStart；已活跃时报 "Development combat is already active"。不是正常战斗初始化 |
| EndDevelopmentCombat | owner=Ref(Player) | action 与同名过程：只在活跃时允许，置 CombatActive=false，不发开始事件也不发胜利；不活跃时报 "Development combat is not active" |
| BeginDevelopmentTurn | owner=Ref(Player) | action与同名过程：活跃且回合门关闭时允许；自增TurnNumber并发BeforePlayerTurnStart，非首回合按保留资格清格挡，随后每回合均发AfterBlockCleared，发BeforeTurnResources，ResetTurnEnergy并发AfterEnergyReset，再打开回合门和发AfterSideTurnStarted；AutoDrawPerTurn大于0时DrawDevelopmentTurnHand，最后发AfterPlayerTurnStart与Late |
| DrawDevelopmentTurnHand | owner=Ref(Player) | 以 AutoDrawPerTurn 为基础，首回合逐张把 Innate 移到顶部，抽取数至少为 Innate 数且至多十张；调用 DrawCards，继承满手／禁止抽牌和真实抽牌事件。不实现附魔底部排序或完整手牌修正 |
| EndDevelopmentTurn | owner=Ref(Player) | action与同名过程：活跃且回合门打开时允许；先回合末自动出牌，再BeforePlayerTurnEnd；仍活跃才弃牌、发AfterTurnEnded并处理能力及敌方阶段。末杀后停止后续阶段，不逐张派发弃牌钩子 |
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
| ObtainPermanentCard | owner: Ref(Player)、definition: String(64)、cost: Int(0..9)、targeted／exhausts／leavesPlay／strikeTag／isX: Bool、kind: String(16)、baseDamage: Int(0..999) | 只允许存活玩家在房间与战斗外获得；以给定已迁移定义创建永久实例，CanUpgrade=true，置 UsesRunDeck 后发 CardObtained；没有随机池或获得修正 |
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
| [TurnStartEnergyRelic](../../../../mod-the-spire/base-spire/relics/rules/turn-start-energy.spire) | amount: Int、turn: Int(1..999) | AfterSideTurnStarted，且 `TurnNumber == turn` 时 GainEnergy | Lantern（1 能量，第 1 回合） |
| [TurnStartHealRelic](../../../../mod-the-spire/base-spire/relics/rules/turn-start-heal.spire) | amount、turn | AfterPlayerTurnStartLate，且 `TurnNumber == turn` 时 Heal | BloodVial（2 生命，第 1 回合） |
| [BlockClearedBlockRelic](../../../../mod-the-spire/base-spire/relics/rules/block-cleared-block.spire) | amount、turn | AfterBlockCleared，且 `TurnNumber == turn` 时 GainUnpoweredBlock | HornCleat（14 格挡，第 2 回合） |
| [EveryTurnsEnergyRelic](../../../../mod-the-spire/base-spire/relics/rules/every-turns-energy.spire) | amount、turns: Int(1..999) | AfterSideTurnStarted：实例 Counter 加一并取模 turns，为 0 时 GainEnergy；Counter 跨战斗保留 | HappyFlower（每 3 回合 1 能量） |
| [TurnStartDamageAllEnemiesRelic](../../../../mod-the-spire/base-spire/relics/rules/turn-start-damage-all-enemies.spire) | amount: Int(0..999) | AfterPlayerTurnStart：对每个 `Hp > 0` 的 Enemy 调用 DamageEnemy；没有目标时无事发生也不消耗随机 | MercuryHourglass（3 点无加成伤害） |

卡牌内容绑定唯一ExecuteCardBody及ReadCardDamage；正文显式顺序调用效果模板的过程。卡牌内容由**一次** [CardDefinition](../../../../mod-the-spire/base-spire/cards/rules/card-definition.spire)（definition、cost、upgradedCost、targeted、exhausts）加若干[效果模板](../../../../mod-the-spire/base-spire/cards/rules/effects.spire)组成；效果过程保留定义过滤，主效果不再订阅广播。CardDefinition 生成 `AddToHand`（开发授予，直接放进手牌）与升级订阅：

| 效果模板 | 参数 | 正文显式调用后的行为 | 当前内容 |
| --- | --- | --- | --- |
| AttackEffect | definition: String(64)、hits: Int(1..9) | 读取CardBaseDamage并创建一个hits段的CardAttack | Strike（6／9）、TwinStrike（5／7×2）、PommelStrike（9／10）、IronWave（5／7）、Anger（6／8）、Bludgeon（32／42） |
| AttackAllEnemiesEffect | definition: String(64)、hits／upgradedHits: Int(1..9) | 创建一个all目标策略的CardAttack，按击次再按目标结算 | Thunderclap（4／7，1 段）、HowlFromBeyond（16／21，1 段）、Breakthrough（9／13，1 段）、Conflagration（2／2，4／5 段） |
| BlockEffect | block、upgradedBlock | 按 `Upgraded` 取值后 GainBlock | Defend（5／8）、ShrugItOff（8／11）、IronWave（5／7）、Impervious（30／40）、Taunt（7／8）、BloodWall（16／20） |
| DrawEffect | cards、upgradedCards | 按 `Upgraded` 取值后 DrawCards | PommelStrike（1／2）、ShrugItOff（1／1）、Offering（3／5） |
| EnergyEffect | energy、upgradedEnergy | 按 `Upgraded` 取值后 GainEnergy | Bloodletting（2／3）、Offering（2／2） |
| LoseHpEffect | hp、upgradedHp | 按 `Upgraded` 取值后 LosePlayerHpFromCard并显式传play | Bloodletting（3／3）、Offering（6／6）、BloodWall（2／2） |
| ApplyEnemyPowerEffect | power、stacks、upgradedStacks | 按 `Upgraded` 取值后 ApplyEnemyPowerFromCard到解析出的目标 | Bash（Vulnerable 2／3）、Uppercut（Weak 与 Vulnerable 各 1／2）、Taunt（Vulnerable 1／2）、Tremble（Vulnerable 3／4） |
| ApplyAllEnemiesPowerEffect | power、stacks、upgradedStacks | 对每个 `Hp > 0` 的开发目标调用ApplyEnemyPowerFromCard | Shockwave（Weak 与 Vulnerable 各 3／5）、Thunderclap（Vulnerable 1／1） |
| ApplySelfPowerEffect | power、stacks、upgradedStacks | 按 `Upgraded` 取值后ApplyPlayerPowerFromCard | Inflame（Strength 2／3） |

以下模板仅由一个内容拥有，已移到所属卡牌文件；它们不是effects.spire中的共享操作。需要类似能力时先确认语义，再决定是否抽成共用模板。

| 独有效果 | 所属模块 |
| --- | --- |
| DamageEqualBlockEffect | [BodySlam](../../../../mod-the-spire/base-spire/cards/ironclad/body-slam.spire) |
| AddCopyEffect | [Anger](../../../../mod-the-spire/base-spire/cards/ironclad/anger.spire) |
| ApplySelfPowerUntilTurnEndEffect | [FlameBarrier](../../../../mod-the-spire/base-spire/cards/ironclad/flame-barrier.spire) |
| FlaggedHitsAttackEffect | [Spite](../../../../mod-the-spire/base-spire/cards/ironclad/spite.spire) |
| FlaggedBlockMultiplierEffect | [EvilEye](../../../../mod-the-spire/base-spire/cards/ironclad/evil-eye.spire) |
| VulnerableHitsAttackEffect | [Dismantle](../../../../mod-the-spire/base-spire/cards/ironclad/dismantle.spire) |
| AmplifyVulnerableEffect | [MoltenFist](../../../../mod-the-spire/base-spire/cards/ironclad/molten-fist.spire) |

同一张牌内的效果顺序由所属ExecuteCardBody处理过程的call顺序决定，与模板use或事件订阅顺序无关。Bash、Uppercut的正文显式先伤害后施加。Weak、Vulnerable与Frail在所属Power模块监听AfterEnemyTurnEnded并调用TickPowerDuration，玩家新实例的首次跳过由debuff标志控制；详见[Power参考](powers.md)。

源条件为“`TurnNumber <= 1`”的两个遗物在这里写成 `turn = 1`：开发战斗从 0 回合开始，第一次开始时回合号一定是 1，两者等价。原版 `AfterSideTurnStart`、`AfterPlayerTurnStart` 与 `AfterPlayerTurnStartLate` 的区别保留成三个开发时点，所需能量／伤害修正、开发胜负与回合结束钩子已恢复；完整正常 Run 顺序和其余参与者不属于本范围。

战斗开始模板 `BeforeCombatStartPowerRelic` 的参数为definition、power、stacks，在 BeforeCombatStart 时对持有者调用 ApplyPlayerPower；`use BronzeScales` 授予 Thorns 3，`use Vajra` 授予 Strength 1，`use OddlySmoothStone` 授予 Dexterity 1。

`BeforeCombatStartEnemyPowerRelic` 参数相同但作用于每个 `Hp > 0` 的敌人（`use BagOfMarbles` 施加 Vulnerable 1）。`EnemyDiedEnergyDrawRelic` 的参数为 definition、energy、cards，订阅 EnemyDied 后对持有者给能量并抽牌（`use GremlinHorn` 为 1／1）。

回合末与拾取类模板：`TurnEndNoBlockRelic`（AfterTurnEnded 时若 Block 为 0 则给无加成格挡，`use Orichalcum` 为 6）、`EveryTurnsDrawRelic`（AfterSideTurnStarted 计数取模，归零时抽牌，`use Pendulum` 为 3 回合 1 张）、`ObtainMaxHpRelic`（获得时提高 MaxHp 并回复同量，`use Strawberry` 为 7）、`CombatEndHealRelic`（CombatEnded 且胜利、Hp 不超过 MaxHp 的阈值百分比时回复，`use MeatOnTheBone` 为 12／50）。显式EndDevelopmentCombat和下次BeginDevelopmentCombat通过ClearCombatPowers调用各实例的RemovePower；自然胜负仅发布CombatEnded，其他Power沿用现有开发清理边界。ExpireTurnPower订阅AfterPlayerTurnFinished，移除带ExpiresAtTurnEnd的能力（对应源 `FlameBarrierPower` 的当回合寿命），因此在敌方攻击期间仍会反击。

药水：`PotionDefinition` 生成 `Obtain`（创建实例后调用共享ObtainPotion放进三格库存），效果模板按 DefinitionId 订阅 `PotionUsed`／`PotionUsedOnTarget`：`PotionBlockEffect`（无加成格挡，BlockPotion 12）、`PotionEnergyEffect`（EnergyPotion 2）、`PotionDrawEffect`（SwiftPotion 3）、`PotionMaxHpEffect`（FruitJuice 5，提高上限并回复同量）、`PotionHealPercentEffect`（BloodPotion 20% 上限）与 `PotionDamageEffect`（FirePotion 20 点无加成伤害）。

模板开发获得动作通过definition检查重复，默认使用first(type(Player))；不能将它当作完整多人获得、替换或熔化操作。监听按实际持有者／实例过滤，跨战状态按所属模板约定处理。Obtain的库存去重不代表该遗物的全部原版来源资格（奖励、事件、商店或角色初态）已经实现。

[内容目录的 definition](../../../dsl/core/0.3/package-reference.md) 指向完整 DSL 内容模块；独有规则／状态留在该文件，共用过程／模板放领域规则库。示例新增 AfterDrawBlockRelic 位于 docs/examples，仅用于[第二篇教程](../tutorials/shared-rule.md)，不是正式包现有公共操作。

## 公开投影

[PlayerView](../../../../mod-the-spire/base-spire/public/projection.spire)声明公开实体类型；[公开元数据](../../../../mod-the-spire/base-spire/public/metadata.json)维护各类型的DefinitionId列表与集合用途。准确内容列表直接读取该文件，不在本页复制支持名单。Power.Definition、PowerBlueprint字段及所属模块私有字段保持Hidden；Power实例的标识和层数继续公开。

开发牌堆、InPlay、ExhaustPile、RemovedFromCombat、Powers和定义集合按metadata各自声明发布或具名不发布。集合不发布不等于可忽略实体上的公开事实；新增集合必须明确用途。当前声明不构成完整原版牌序、能力生命周期或正常Run约定。

公开 action_type 为 `net:action:base-spire:TheAbacus.Obtain`，格挡字段为 `net:field:base-spire:Player.Block`；生命、能量与回合见 `net:field:base-spire:Player.Hp`／`Player.Energy`／`Player.TurnNumber`／`Player.TurnOpen`／`Player.CombatActive`，目标生命见 `net:field:base-spire:Enemy.Hp`，能力层数见 `net:field:base-spire:Power.Stacks`，战斗结果见 `net:field:base-spire:Player.CombatOutcome`，回合内标志见 `net:field:base-spire:Player.LostHpThisTurn`／`Player.ExhaustedCardThisTurn`，公开动作见 `net:action:base-spire:BeginDevelopmentCombat`／`EndDevelopmentCombat`／`BeginDevelopmentTurn`／`EndDevelopmentTurn`／`AddDevelopmentTarget`／`PlayCardFromHand`／`PlayAttackFromHand`／`UpgradeCardInHand`。协议用真实候选 ID 提交动作；元数据不改变状态、触发顺序或合法动作守卫。
