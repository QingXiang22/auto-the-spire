# 共享 DSL 操作参考

文档类型：参考（Reference）。适用于 core/0.3 与纯 DSL base 的开发态战斗包。只列实际存在的声明；这里没有列出的游戏操作不能按名字猜测可调用。文件路径相对 `mod-the-spire/base-spire/`，精确实现通过各行链接定位。

## 状态与事件

| 声明 | 字段／载荷 | 约束与归属 |
| --- | --- | --- |
| [Player](../../../../mod-the-spire/base-spire/combat/schema.spire) | Owner: Participant；Hp／MaxHp: Int=80；Energy: Int=0；MaxEnergy: Int=3；TurnNumber: Int=0；TurnOpen: Bool=false；Block: Int(0..999)=0；DrawBlocked: Bool=false；CombatActive: Bool=false；CombatOutcome: String(16)=`none`；LostHpThisTurn／ExhaustedCardThisTurn: Bool=false | 开发玩家；CombatOutcome 是开发态结果（none／victory／defeat），不是协议终局；TurnOpen 是开发态回合门；两个 ThisTurn 标志是源战斗历史的开发替身，在回合开始时复位 |
| [Card](../../../../mod-the-spire/base-spire/cards/schema.spire) | Owner: Participant；Label: String(64)；DefinitionId: String(64)=`base-spire:development-card`；Cost: Int(0..9)=0；Upgraded: Bool=false；Targeted: Bool=false；Exhausts: Bool=false；LeavesPlay: Bool=false | Targeted 是原版 TargetType 的开发替身；Exhausts 是 CardKeyword.Exhaust 的开发替身；LeavesPlay 是 CardType.Power 的开发替身（结算后离开战斗）；卡面没有原版稀有度字段 |
| [Enemy](../../../../mod-the-spire/base-spire/combat/schema.spire) | Owner: Participant；DefinitionId: String(64)；Hp／MaxHp: Int=0；Block: Int(0..999)=0；MoveIndex: Int(0..9)=0；Intent: String(32)=`none` | 开发敌人：MoveIndex 是它自己轮转里的招位（含义由敌人模板拥有），Intent 是公开的下一个意图名（不含伤害数值）；没有移除、死亡钩子、遭遇编队或原版 AI 选择 |
| [Power](../../../../mod-the-spire/base-spire/combat/schema.spire) | Owner；DefinitionId: String(64)；Kind: String(32)=`none`；HolderPlayer／HolderEnemy: 可选 Ref；Stacks: Int(-999..999)=0 | 开发能力实例：两个持有者引用恰好设置一个；Kind 是规则查询用的修正角色，因此修正链不按能力 ID 分支；没有时长衰减、移除、能力类型或触发反应 |
| Powers | 有序 Power 集合 | 按持有者与 DefinitionId 合并层数；公开元数据把它标为 not_published，因为当前 group／sequence 集合元数据无法表达两种持有者类型 |
| [Potion](../../../../mod-the-spire/base-spire/potions/schema.spire) | DefinitionId: String(64) | 开发药水实例，只承载身份；原版的稀有度、使用窗口（战斗内／任意时刻）与来源未迁移 |
| Potions | 有序 Potion 集合，容量 3 | 三个共享槽位；`UsePotion`／`UsePotionOnTarget` 使用后在监听之后移除并销毁 |
| PotionUsed／PotionUsedOnTarget | owner: Ref(Player)、potion: Ref(Potion)（有目标时再加 target: Ref(Enemy)） | 药水仍在库存时发出，之后才消费；有目标版本解析第一个 `Hp > 0` 的开发目标 |
| Hand／DrawPile／DiscardPile／InPlay／ExhaustPile／RemovedFromCombat | 有序 Card 集合；Hand 容量 10 | 当前是全局开发牌堆，尚无按持有者分堆；DrawPile 隐藏身份及顺序；InPlay 只在出牌结算期间持有该卡；ExhaustPile 只接收带 Exhausts 的结算牌；RemovedFromCombat 只接收带 LeavesPlay 的结算牌（原版把它们交回主牌组，本 scope 没有主牌组） |
| [Relic](../../../../mod-the-spire/base-spire/relics/schema.spire) | Owner、Holder、DefinitionId、IsMelted=false、Counter=0 | Holder 是隐藏 Ref(Player)，Counter 隐藏；DefinitionId 长度上限 64；Counter 为 0..999999 |
| Relics | 有序 Relic 集合 | 监听器遍历现有库存并过滤 Holder、definition 和熔化状态 |
| RelicObtained／RelicRemoved | owner: Ref(Player)、relic: Ref(Relic) | 获得时已入库存；移除时已出库存，事件执行后才销毁实例 |
| [BeforeCombatStart](../../../../mod-the-spire/base-spire/combat/lifecycle.spire) | owner: Ref(Player) | 只在 Idle→Active 的开发态开始操作中发出；监听器观察到 CombatActive=true 且本场初始 Block 已清 0，之后获得的格挡保留 |
| AfterBlockCleared | owner: Ref(Player) | 回合号大于 1 的开始时，先清空格挡再发出；与源 `Creature.AfterTurnStart`→`Hook.AfterBlockCleared` 的顺序一致，第一回合不发 |
| [AfterSideTurnStarted](../../../../mod-the-spire/base-spire/combat/lifecycle.spire) | owner: Ref(Player) | 能量重置到 MaxEnergy 之后发出；原版把这段拆成 AfterEnergyReset 与 AfterSideTurnStart，本包只发一个开发时点 |
| AfterPlayerTurnStart | owner: Ref(Player) | 紧随 AfterSideTurnStarted 发出，对应原版 AfterPlayerTurnStart（原版按 Early、正常、Late 三轮遍历） |
| AfterPlayerTurnStartLate | owner: Ref(Player) | 紧随 AfterPlayerTurnStart 发出，对应原版 AfterPlayerTurnStartLate；三个玩家回合开始时点都在同一执行栈内按声明顺序完成 |
| AfterTurnEnded | owner: Ref(Player) | 回合末时点：先弃掉手牌，再发出，然后才关闭回合门；共享规则在这里把 debuff 角色能力减一层并在归零时移除（对应源 PowerCmd.TickDownDuration） |
| EnemyDied | target: Ref(Enemy) | 敌人生命从正数到 0 时发出一次；原版的移除生物与死亡钩子未迁移 |
| CombatEnded | owner: Ref(Player)、victory: Bool | 结束态已经写入 `CombatOutcome` 并关闭回合门与活跃标记之后发出；没有奖励与 Run 结算 |
| EnemyTurnStarted | owner: Ref(Player)、target: Ref(Enemy) | 玩家回合结束后，对每个 `Hp > 0` 的敌人各发一次；敌人内容模板按 DefinitionId 订阅并实现自己的招 |
| AfterEnemyTurnEnded | owner: Ref(Player) | 敌方阶段结束时发出；共享规则在这里做 debuff 减层（源在敌方回合末），取代此前挂在玩家回合末的做法 |
| AfterShuffle | owner: Ref(Player) | 仅在实际转移并完成 shuffle 后发出 |
| AfterCardDrawn | owner: Ref(Player)、card: Ref(Card) | 成功移到 Hand 后发出；未抽到牌不发 |
| [CardPlayed](../../../../mod-the-spire/base-spire/cards/schema.spire) | owner: Ref(Player)、card: Ref(Card) | 在卡仍位于 InPlay、效果结算之前发出；订阅者按 `Card.DefinitionId` 过滤，自目标与无目标效果用它 |
| CardPlayedOnTarget | owner、card、target: Ref(Enemy) | 单体攻击牌用它携带解析出的敌人；PlayAttackCard 先发 CardPlayed 再发它，与源“先格挡后伤害”的顺序一致；解析规则是“第一个 Hp > 0 的开发目标”，不是玩家选择 |
| CardUpgraded | card: Ref(Card) | 实例标记为升级后发出；卡片定义用它应用定义层升级（当前只有费用） |
| CardExhausted | owner: Ref(Player)、card: Ref(Card) | 带 Exhausts 的牌进入 ExhaustPile 后发出；订阅时可确认卡已在消耗堆 |

## 过程与公开动作

| 声明 | 准确参数 | 行为与边界 |
| --- | --- | --- |
| [GainUnpoweredBlock](../../../../mod-the-spire/base-spire/combat/block.spire) | owner=Ref(Player)、amount=Int(0..999) | 累加并截到 999，不应用卡牌／Power 的加性或乘性修正；不是通用获得格挡流程 |
| GainBlock | owner=Ref(Player)、amount=Int(0..999) | 卡牌格挡链：先累加 `block-additive`（Dexterity），再在持有 `block-dealt-multiplier`（Frail）时把总量乘 0.75 向下取整，最后截到 999 且不低于 0；遗物／Power 的无加成格挡仍走 GainUnpoweredBlock |
| [ApplyPlayerPower](../../../../mod-the-spire/base-spire/combat/powers.spire) | owner=Ref(Player)、definition=String(64)、kind=String(32)、stacks=Int(-999..999) | 同持有者同 DefinitionId 时累加层数，否则创建实例；不处理衰减、移除或施加反应 |
| ApplyEnemyPower | target=Ref(Enemy)、definition、kind、stacks | 对敌人执行同一合并语义 |
| [Heal](../../../../mod-the-spire/base-spire/combat/resources.spire) | owner=Ref(Player)、amount=Int(0..999) | Hp 增加并按 MaxHp 截断；不处理复活、治疗修正或死亡，也不改变 MaxHp |
| GainEnergy | owner=Ref(Player)、amount=Int(0..999) | Energy 增加并可超过 MaxEnergy（上限 999）；回合开始的重置仍按 MaxEnergy |
| [DamageEnemy](../../../../mod-the-spire/base-spire/combat/damage.spire) | target=Ref(Enemy)、amount=Int(0..999) | 先扣 `min(Block, amount)` 格挡，剩余从 Hp 扣除且不低于 0；无加成伤害，修正参与者不参与（遗物与 Power 用它） |
| DamageEnemyPowered | target、amount | 卡牌攻击链：先加攻击者的 `damage-additive`（Strength），再在目标有 `damage-taken-multiplier`（Vulnerable）时乘 1.5 并向下取整，最后按同一格挡→生命顺序结算；攻击者固定是开发玩家，Weak（`damage-dealt-multiplier`）只记录层数，敌方可行动作未迁移前无可观察效果 |
| DamagePlayer | owner=Ref(Player)、amount=Int(0..999) | 对玩家执行同一格挡→生命结算；不改变 MaxHp，也不处理死亡 |
| [LosePlayerHp](../../../../mod-the-spire/base-spire/combat/damage.spire) | owner=Ref(Player)、amount=Int(0..999) | 绕过格挡的生命损失，对应源 `Unblockable | Unpowered` 伤害（Offering／Bloodletting）；不应用伤害修正、不触发受击反应，只保留生命下限 0 |
| [PlayCard](../../../../mod-the-spire/base-spire/cards/rules/play.spire) | owner=Ref(Player)、card=Ref(Card) | 校验战斗活跃、卡在手牌、能量足够；扣能量、Hand→InPlay、emit CardPlayed、结算后按 Exhausts 进入 ExhaustPile（并发 CardExhausted）或 DiscardPile |
| PlayAttackCard | owner、card | 同上，但先发 CardPlayed 再发 CardPlayedOnTarget，并把敌人解析为第一个 `Hp > 0` 的开发目标；无目标不是合法候选 |
| UpgradeCard | card=Ref(Card) | 置 `Upgraded=true` 并发 CardUpgraded；定义层变化（当前只有费用）由 CardDefinition 模板应用 |
| [DrawCards](../../../../mod-the-spire/base-spire/cards/rules/draw.spire) | owner=Ref(Player)、count=Int(0..9) | 逐张调用 DrawOne，保留重洗、手牌上限、禁止抽牌与 AfterCardDrawn 的既有顺序；不是一次抽多张的新算法 |
| PlayCardFromHand／PlayAttackFromHand／UpgradeCardInHand | 参数 card=Ref(Card)，候选来自 Hand | 公开 action：分别打出无目标牌、打出有目标牌与升级手牌；PlayCardFromHand 只提供 `Targeted=false` 的候选，PlayAttackFromHand 只提供 `Targeted=true` 的候选 |
| [PlayCard](../../../../mod-the-spire/base-spire/cards/rules/play.spire) 结算收尾 | — | 效果结算后按 `Exhausts`→ExhaustPile、`LeavesPlay`→RemovedFromCombat、否则 DiscardPile 分流；只有 Exhausts 分支发 CardExhausted |
| [EnterDevelopmentRestSite](../../../../mod-the-spire/base-spire/rooms/rest-site.spire) | owner=Ref(Player) | 公开 action：把房间置为 `rest-site` 并清掉已使用标记；不是地图节点入口 |
| RestAtDevelopmentCampfire | owner=Ref(Player) | 公开 action 与同名过程：只在营火且未使用时允许，回复 `floor(MaxHp × 0.3)` 后把选项标记为已用；过程在非营火或重复使用时失败 |
| LeaveDevelopmentRoom | owner=Ref(Player) | 公开 action：把房间与已使用标记复位为 `none`／false；奖励界面必须使用 CloseDevelopmentRewards |
| [CombatEnded 奖励订阅](../../../../mod-the-spire/base-spire/rewards/rules/combat-gold.spire) | owner=Ref(Player)、victory=Bool | 存活玩家胜利时用命名流 `base-spire:development-reward` 从 `[10, 21)` 抽一次，写入 GoldRewardAmount 并置 GoldRewardPending=true、Room=`rewards`；Gold 不变；失败不生成也不抽随机 |
| ClaimDevelopmentGoldReward | owner=Ref(Player) | 公开 action 与同名过程：只在 rewards 且条目 pending 时把保存金额加进 Gold（开发上限 99999），清空条目；领取不关闭界面，不重抽，不能重复领取 |
| CloseDevelopmentRewards | owner=Ref(Player) | 公开 action 与同名过程：只在 rewards 时清空未领条目、金额与房间状态；允许不领取直接关闭，不给金币也不抽随机；关闭前禁止开始新战斗、进入营火或 LeaveDevelopmentRoom |
| [CheckCombatVictory](../../../../mod-the-spire/base-spire/combat/lifecycle.spire) | owner=Ref(Player) | 只在敌人死亡后调用：若已无 `Hp > 0` 的敌人且战斗仍活跃，写 victory、关闭回合门与活跃标记并发 CombatEnded |
| CheckCombatDefeat | owner=Ref(Player) | 每次玩家掉血后调用：生命为 0 且战斗仍活跃时写 defeat 并关闭状态，再发 CombatEnded |
| [DamagePlayerPowered](../../../../mod-the-spire/base-spire/combat/enemy-turn.spire) | attacker=Ref(Enemy)、owner=Ref(Player)、amount | 敌方攻击链：防守方 `after-attacked-damage`（Thorns）先以无加成伤害反击攻击者，再按攻击者 Strength→攻击者 Weak ×0.75→防守方 Vulnerable ×1.5（整数向下取整）→格挡→生命，并做失败判定 |
| RunEnemyTurn | owner=Ref(Player) | 敌方阶段：对每个可命中敌人先清空其格挡（源 `Creature.AfterTurnStart` 的敌侧语义），再发 EnemyTurnStarted；最后发 AfterEnemyTurnEnded |
| [SingleAttackEnemy](../../../../mod-the-spire/base-spire/enemies/rules/single-attack-enemy.spire) 模板 | definition、hp、damage | 生成 `WhenEnemyTurn`（每回合对玩家造成固定伤害并保持 attack 意图）与 `Spawn`（用源最小 HP 创建） | TwigSlimeS（7 HP，4 伤害）、AssassinRubyRaider（18 HP，10 伤害） |
| AttackDefendEnemy | definition、hp、damage、block | 攻击后给自身格挡（`GainEnemyBlock`）并保持 attack 意图 | BowlbugEgg（21 HP，7 伤害＋7 格挡） |
| AttackBuffEnemy | definition、hp、damage、strength | 攻击与自我强化交替（MoveIndex 0／1） | BruteRubyRaider（30 HP，7 伤害／+3 强度） |
| AttackDebuffEnemy | definition、hp、damage、frail | 攻击后给玩家施加 `block-dealt-multiplier`（Frail） | Stabbot（18 HP，11 伤害＋1 Frail） |
| AttackDefendRotationEnemy | definition、hp、damage、block、bigDamage | 三段轮转：两次「攻击＋格挡」，再一记大攻击 | AxeRubyRaider（20 HP，5＋5 格挡 ×2，然后 12） |
| BlockThenAttackEnemy | definition、hp、block、damage | 先格挡再攻击，交替 | CrossbowRubyRaider（18 HP，3 格挡／14 伤害） |
| DebuffThenMultiAttackEnemy | definition、hp、frail、damage、hits | 先施加 Frail，之后每回合多段攻击 | TrackerRubyRaider（21 HP，Frail 2 后 1 伤害 ×8） |

遭遇内容用模板生成 `Spawn` 动作：`SingleEnemyEncounter`（definition 是遭遇身份，enemyDefinition／hp 描述唯一怪物）调用共享过程 `SpawnDevelopmentEnemy`；`RubyRaidersFormation` 先建 `EnemyPool` 的五个候选（Axe 20／Assassin 18／Brute 30／Crossbow 18／Tracker 21），再用 `samplePick(stream = base-spire:development-encounter)` 抽三次并逐个移除（不重复），最后清空池。当前遭遇：`base-spire:fuzzy-wurm-crawler-weak`（Monster 房、Weak、单怪）与 `base-spire:ruby-raiders-normal`（Monster 房、三个互不相同的 Ruby Raider，源随机流未迁移）。
| [ObtainRelic](../../../../mod-the-spire/base-spire/relics/rules/lifecycle.spire) | owner=Ref(Player)、definition=String(64) | 在 owner 作用域创建、写 Owner／Holder／DefinitionId、加入 Relics 后发获得事件；过程本身不去重 |
| RemoveRelic | instance=Ref(Relic) | 必须在 Relics；先移出，再发移除事件，再销毁；不持有时报错 |
| RemoveOwnedRelic | instance=Ref(Relic)，公开候选来自 Relics | action，调用 RemoveRelic；参数必须来自实际候选，不能伪造实例字符串 |
| [ReshuffleDiscard](../../../../mod-the-spire/base-spire/cards/rules/draw.spire) | owner=Ref(Player) | DrawPile 空且 DiscardPile 非空才逐项转移、shuffle、emit AfterShuffle；开发流名 base-spire:development-shuffle |
| DrawOne | owner=Ref(Player) | 未禁止抽牌且手牌不足 10 时先尝试重洗，再取首张，成功移动后 emit AfterCardDrawn |
| Draw／DiscardHand | 无参数，role=player | Draw 使用 first(type(Player))；DiscardHand 将全局 Hand 移到 DiscardPile；均是开发动作 |
| [BeginDevelopmentCombat](../../../../mod-the-spire/base-spire/combat/lifecycle.spire) | owner=Ref(Player) | action 与同名过程：inactive 时先置 Block=0、TurnNumber=0、Energy=0、TurnOpen=false、CombatActive=true，再 emit BeforeCombatStart；已活跃时报 "Development combat is already active"。不是正常战斗初始化 |
| EndDevelopmentCombat | owner=Ref(Player) | action 与同名过程：只在活跃时允许，置 CombatActive=false，不发开始事件也不发胜利；不活跃时报 "Development combat is not active" |
| BeginDevelopmentTurn | owner=Ref(Player) | action 与同名过程：活跃且回合门关闭时允许；先自增 TurnNumber，回合号大于 1 时清空格挡并发 AfterBlockCleared，再置 Energy=MaxEnergy、打开回合门，最后依次发 AfterTurnStarted 与 AfterPlayerTurnStartLate。不抽牌 |
| EndDevelopmentTurn | owner=Ref(Player) | action 与同名过程：活跃且回合门打开时允许；把 Hand 移到 DiscardPile 并关闭回合门；不结算敌方回合、不发胜利、不逐张派发弃牌钩子 |
| [AddDevelopmentTarget](../../../../mod-the-spire/base-spire/run/setup-dev.spire) | owner=Ref(Player) | 过程与公开 action：在 owner 作用域创建一个 Hp／MaxHp 为 40、DefinitionId 为 `base-spire:development-target` 的 Enemy；不是遭遇或原版怪物生成 |
| [SetupDevelopmentDraw](../../../../mod-the-spire/base-spire/run/setup-dev.spire) | 无参数；根 start 调用一次 | 创建一个 Player 和弃牌堆三张开发 Card；不提供正常 Run 初始化 |

调用现有共享操作时使用具名参数。例如：

```text
call = { procedure = GainUnpoweredBlock args = { owner = owner amount = 6 } }
```

这里的 owner 必须是明确传入／绑定的 Ref(Player)。`participant(1)` 是 Participant，不能代替实体引用。普通内容不应绕过生命周期直接更新库存或引用字段。

## 模板与内容配置

[AfterShuffleBlockRelic](../../../../mod-the-spire/base-spire/relics/rules/after-shuffle-block.spire) 的参数为 definition: String(64)、amount: Int(0..999)。它生成 `WhenShuffled`、`OnShuffle` 与 `Obtain`，按当前持有库存过滤并调用 GainUnpoweredBlock。`use TheAbacus` 生成 `TheAbacus.Obtain` 等名称；TheAbacus 正式参数为 6。

[BeforeCombatStartBlockRelic](../../../../mod-the-spire/base-spire/relics/rules/before-combat-start-block.spire) 的参数同样是 definition: String(64)、amount: Int(0..999)，生成 `WhenCombatStarts`、`OnCombatStart`（订阅 BeforeCombatStart）与 `Obtain`。它按持有者、DefinitionId 和 IsMelted 过滤后调用同一 GainUnpoweredBlock；`use Anchor` 为 10，`use FakeAnchor` 为 4，两者同时持有在同一次开始中共得 14。

回合资源模板共享 `{ definition: String(64) }`，各自再取一个触发回合、计数周期或伤害数值；它们都生成各自的监听过程与 `Obtain`：

| 模板 | 参数 | 订阅与行为 | 当前内容 |
| --- | --- | --- | --- |
| [TurnStartEnergyRelic](../../../../mod-the-spire/base-spire/relics/rules/turn-start-energy.spire) | amount: Int、turn: Int(1..999) | AfterSideTurnStarted，且 `TurnNumber == turn` 时 GainEnergy | Lantern（1 能量，第 1 回合） |
| [TurnStartHealRelic](../../../../mod-the-spire/base-spire/relics/rules/turn-start-heal.spire) | amount、turn | AfterPlayerTurnStartLate，且 `TurnNumber == turn` 时 Heal | BloodVial（2 生命，第 1 回合） |
| [BlockClearedBlockRelic](../../../../mod-the-spire/base-spire/relics/rules/block-cleared-block.spire) | amount、turn | AfterBlockCleared，且 `TurnNumber == turn` 时 GainUnpoweredBlock | HornCleat（14 格挡，第 2 回合） |
| [EveryTurnsEnergyRelic](../../../../mod-the-spire/base-spire/relics/rules/every-turns-energy.spire) | amount、turns: Int(1..999) | AfterSideTurnStarted：实例 Counter 加一并取模 turns，为 0 时 GainEnergy；Counter 跨战斗保留 | HappyFlower（每 3 回合 1 能量） |
| [TurnStartDamageAllEnemiesRelic](../../../../mod-the-spire/base-spire/relics/rules/turn-start-damage-all-enemies.spire) | amount: Int(0..999) | AfterPlayerTurnStart：对每个 `Hp > 0` 的 Enemy 调用 DamageEnemy；没有目标时无事发生也不消耗随机 | MercuryHourglass（3 点无加成伤害） |

卡牌内容由**一次** [CardDefinition](../../../../mod-the-spire/base-spire/cards/rules/card-definition.spire)（definition、cost、upgradedCost、targeted、exhausts）加若干[效果模板](../../../../mod-the-spire/base-spire/cards/rules/effects.spire)组成；效果只按 `Card.DefinitionId` 过滤，因此一张牌可以组合多个效果。CardDefinition 生成 `AddToHand`（开发授予，直接放进手牌）与升级订阅：

| 效果模板 | 参数 | 订阅与行为 | 当前内容 |
| --- | --- | --- | --- |
| AttackEffect | damage、upgradedDamage、hits | CardPlayedOnTarget，按 `Upgraded` 取值并重复 hits 次调用 DamageEnemyPowered | Strike（6／9）、TwinStrike（5／7×2）、PommelStrike（9／10）、IronWave（5／7）、Anger（6／8）、Bludgeon（32／42） |
| AttackAllEnemiesEffect | damage、upgradedDamage、hits、upgradedHits | CardPlayed，对每个 `Hp > 0` 的开发目标按 hits（升级取 upgradedHits）逐段调用 DamageEnemyPowered（逐目标扣格挡） | Thunderclap（4／7，1 段）、HowlFromBeyond（16／21，1 段）、Breakthrough（9／13，1 段）、Conflagration（2／2，4／5 段） |
| BlockEffect | block、upgradedBlock | CardPlayed，按 `Upgraded` 取值后 GainBlock | Defend（5／8）、ShrugItOff（8／11）、IronWave（5／7）、Impervious（30／40）、Taunt（7／8）、BloodWall（16／20） |
| DrawEffect | cards、upgradedCards | CardPlayed，按 `Upgraded` 取值后 DrawCards | PommelStrike（1／2）、ShrugItOff（1／1）、Offering（3／5） |
| EnergyEffect | energy、upgradedEnergy | CardPlayed，按 `Upgraded` 取值后 GainEnergy | Bloodletting（2／3）、Offering（2／2） |
| LoseHpEffect | hp、upgradedHp | CardPlayed，按 `Upgraded` 取值后 LosePlayerHp | Bloodletting（3／3）、Offering（6／6）、BloodWall（2／2） |
| DamageEqualBlockEffect | 无 | CardPlayedOnTarget，伤害等于玩家当前 Block | BodySlam |
| AddCopyEffect | 无 | CardPlayedOnTarget，把打出牌的克隆（含升级与 Exhausts 状态）放进弃牌堆 | Anger |
| ApplyEnemyPowerEffect | power、kind、stacks、upgradedStacks | CardPlayedOnTarget，按 `Upgraded` 取值后 ApplyEnemyPower 到解析出的目标 | Bash（Vulnerable 2／3）、Uppercut（Weak 与 Vulnerable 各 1／2）、Taunt（Vulnerable 1／2）、Tremble（Vulnerable 3／4） |
| ApplyAllEnemiesPowerEffect | power、kind、stacks、upgradedStacks | CardPlayed，对每个 `Hp > 0` 的开发目标调用 ApplyEnemyPower | Shockwave（Weak 与 Vulnerable 各 3／5）、Thunderclap（Vulnerable 1／1） |
| ApplySelfPowerEffect | power、kind、stacks、upgradedStacks | CardPlayed，按 `Upgraded` 取值后 ApplyPlayerPower | Inflame（Strength 2／3） |
| ApplySelfPowerUntilTurnEndEffect | power、kind、stacks、upgradedStacks | 同上但调用 ApplyPlayerPowerUntilTurnEnd：实例在敌方阶段结束后被移除 | FlameBarrier（Thorns 4／6） |
| FlaggedHitsAttackEffect | damage、upgradedDamage、hits、flaggedHits、upgradedFlaggedHits、flag | CardPlayedOnTarget，按 `Upgraded` 取伤害，再按 flag（`lost-hp`／`exhausted-card`）决定打 hits 还是 flaggedHits 段 | Spite（5，1／2／3 段） |
| FlaggedBlockMultiplierEffect | block、upgradedBlock、flag | CardPlayed，按 `Upgraded` 取值，flag 为真时 GainBlock 两次 | EvilEye（8／11，翻倍为 16／22） |
| VulnerableHitsAttackEffect | damage、upgradedDamage、hits、vulnerableHits | CardPlayedOnTarget，目标已有 `damage-taken-multiplier` 时打 vulnerableHits 段 | Dismantle（8／10，1／2 段） |
| AmplifyVulnerableEffect | 无 | CardPlayedOnTarget，把目标当前易伤层数再加一次（翻倍） | MoltenFist |

同一张牌内多个效果的先后顺序是“先 CardPlayed 的无目标效果，再 CardPlayedOnTarget 的有目标效果”，同一事件内按内容文件的 `use` 声明顺序执行；因此声明了攻击效果再加 debuff 的卡（Bash、Uppercut）与源 OnPlay 的“先伤害后施加”一致。Weak、Vulnerable 的回合末衰减没有迁移。

源条件为“`TurnNumber <= 1`”的两个遗物在这里写成 `turn = 1`：开发战斗从 0 回合开始，第一次开始时回合号一定是 1，两者等价。原版 `AfterSideTurnStart`、`AfterPlayerTurnStart` 与 `AfterPlayerTurnStartLate` 的区别保留成三个开发时点，但能量修正链、伤害修正、死亡与回合结束钩子仍未迁移。

战斗开始模板 `BeforeCombatStartPowerRelic` 的参数为 definition、power、kind、stacks，在 BeforeCombatStart 时对持有者调用 ApplyPlayerPower；`use BronzeScales` 授予 Thorns 3（kind `after-attacked-damage`），`use Vajra` 授予 Strength 1，`use OddlySmoothStone` 授予 Dexterity 1。

`BeforeCombatStartEnemyPowerRelic` 参数相同但作用于每个 `Hp > 0` 的敌人（`use BagOfMarbles` 施加 Vulnerable 1）。`EnemyDiedEnergyDrawRelic` 的参数为 definition、energy、cards，订阅 EnemyDied 后对持有者给能量并抽牌（`use GremlinHorn` 为 1／1）。

回合末与拾取类模板：`TurnEndNoBlockRelic`（AfterTurnEnded 时若 Block 为 0 则给无加成格挡，`use Orichalcum` 为 6）、`EveryTurnsDrawRelic`（AfterSideTurnStarted 计数取模，归零时抽牌，`use Pendulum` 为 3 回合 1 张）、`ObtainMaxHpRelic`（获得时提高 MaxHp 并回复同量，`use Strawberry` 为 7）、`CombatEndHealRelic`（CombatEnded 且胜利、Hp 不超过 MaxHp 的阈值百分比时回复，`use MeatOnTheBone` 为 12／50）。战斗结束时 `ClearCombatPowers` 清空整个能力集合（对应源 AfterCombatEnd 的 RemoveAllPowers）；`ExpireTurnEndPowers` 在敌方阶段结束后移除带 `ExpiresAtTurnEnd` 的能力（对应源 `FlameBarrierPower` 的当回合寿命），因此在敌方攻击期间仍会反击。

药水：`PotionDefinition` 生成 `Obtain`（直接放进三格库存），效果模板按 DefinitionId 订阅 `PotionUsed`／`PotionUsedOnTarget`：`PotionBlockEffect`（无加成格挡，BlockPotion 12）、`PotionEnergyEffect`（EnergyPotion 2）、`PotionDrawEffect`（SwiftPotion 3）、`PotionMaxHpEffect`（FruitJuice 5，提高上限并回复同量）、`PotionHealPercentEffect`（BloodPotion 20% 上限）与 `PotionDamageEffect`（FirePotion 20 点无加成伤害）。

模板获得动作通过 definition 检查重复，并使用 first(type(Player))。多 Player、同定义多实例、按拥有者牌堆、替换、熔化操作与跨战重建尚未在该模板契约交付；不能将开发动作当作这些功能的实现。模板的 `Obtain` 只在遗物库存层去重，不表示原版来源资格（战斗奖励、事件、商店或角色初始遗物）已实现。

[source-catalog 的 definition](../../../dsl/core/0.3/package-reference.md) 指向的内容文件只能包含头部与 `use`。共享过程／模板放规则文件。示例新增 AfterDrawBlockRelic 位于 docs/examples，仅用于[第二篇教程](../tutorials/shared-rule.md)，不是正式包现有公共操作。

## 公开投影

[PlayerView](../../../../mod-the-spire/base-spire/public/projection.spire) 由 DSL 声明 Player／Card／Relic／Enemy／Power；[公开元数据](../../../../mod-the-spire/base-spire/public/metadata.json) 解释 DefinitionId 内容列表与集合用途。Holder／Counter 与 Power 的持有者引用不公开；开发牌堆、InPlay、ExhaustPile、RemovedFromCombat 与 Powers 分别是 `DB-DEVELOPMENT-PILES`／`DB-DEVELOPMENT-POWERS` 的 not_published，没有原版牌序、出牌历史或能力生命周期契约；卡牌内容 ID 目前是 `base-spire:development-card` 加二十九张正式牌（strike-ironclad、defend-ironclad、twin-strike、pommel-strike、shrug-it-off、iron-wave、anger、body-slam、impervious、offering、bloodletting、bludgeon、bash、uppercut、inflame、taunt、tremble、thunderclap、blood-wall、spite、evil-eye、break、howl-from-beyond、hemokinesis、breakthrough、conflagration、dismantle、molten-fist、shockwave；其中 shockwave 属于无色卡组），能力内容 ID 是 strength／vulnerable／weak／dexterity／frail-power，开发敌人内容 ID 有 `base-spire:development-target`、`base-spire:fuzzy-wurm-crawler`（专属轮转模板）、`base-spire:twig-slime-s` 与 `base-spire:assassin-ruby-raider`（共用 `enemies/rules/single-attack-enemy.spire` 的单招循环模板）、`base-spire:bowlbug-egg`、`base-spire:brute-ruby-raider` 与 `base-spire:stabbot`。

公开 action_type 为 `net:action:base-spire:TheAbacus.Obtain`，格挡字段为 `net:field:base-spire:Player.Block`；生命、能量与回合见 `net:field:base-spire:Player.Hp`／`Player.Energy`／`Player.TurnNumber`／`Player.TurnOpen`／`Player.CombatActive`，目标生命见 `net:field:base-spire:Enemy.Hp`，能力层数见 `net:field:base-spire:Power.Stacks`，战斗结果见 `net:field:base-spire:Player.CombatOutcome`，回合内标志见 `net:field:base-spire:Player.LostHpThisTurn`／`Player.ExhaustedCardThisTurn`，公开动作见 `net:action:base-spire:BeginDevelopmentCombat`／`EndDevelopmentCombat`／`BeginDevelopmentTurn`／`EndDevelopmentTurn`／`AddDevelopmentTarget`／`PlayCardFromHand`／`PlayAttackFromHand`／`UpgradeCardInHand`。协议用真实候选 ID 提交动作；元数据不改变状态、触发顺序或合法动作守卫。
