# 卡牌行为、攻击与实例操作参考

文档类型：参考（Reference）。适用于当前base的单玩家开发scope，版本见[包manifest](../../../../mod-the-spire/base-spire/package.json)。来源顺序、状态归属和限制见[迁移约定](../../../design/mod-the-spire/content-behavior-migration.md)；通用语法见[封闭键分派](../../../dsl/core/0.3/authoring-reference.md#封闭键分派)。这些操作不开放正常 Run scope。

## 定义正文与纯值

每张可执行牌在自己的模块保留 CardDefinition，并绑定两张分派表。处理签名必须完全匹配：

| 表／处理参数 | 用途 |
| --- | --- |
| `ExecuteCardBody(owner: Ref(Player), card: Ref(Card), target: Binding, play: Ref(CardPlayRecord))` | 普通过程；正文顺序调用领域命令，target 可以 none |
| `ReadCardDamage(owner: Ref(Player), card: Ref(Card), target: Binding) → Int` | pure；普通、升级与动态基础值由所属模块维护；`CardBaseDamage` 委托本表 |

绑定使用 `dispatchCase Name { dispatch = ExecuteCardBody key = "完整定义ID" procedure = Body }`。值提供者同样绑定 ReadCardDamage。缺绑定在启动登记时失败，重复键在编译时失败；`CardPlayed`／`CardPlayedOnTarget` 不再执行正文。

常见正文使用[共享卡牌模板](../../../../mod-the-spire/base-spire/cards/rules/card-bodies.spire)，不逐卡复制处理过程：

| 模板 | 参数与生成内容 |
| --- | --- |
| `AttackCardBody` | definition、hits（默认1）；调用已有AttackEffect，一次攻击命令保留多段语义，绑定正文与静态伤害读取 |
| `BlockCardBody` | definition、block、upgradedBlock；调用BlockEffect，绑定正文与静态伤害读取 |
| `SelfPowerCardBody` | definition、power、stacks、upgradedStacks、可选untilTurnEnd（默认false）；调用ApplySelfPowerEffect，绑定正文与静态伤害读取 |
| `StaticCardDamage` | definition；只绑定ReadCardDamage，读取Card.BaseDamage与Card.DamageGrowth之和，供显式组合正文复用 |

这些正文模板与原名称的CardDefinition并列使用，数值仍在定义或效果参数中各自维护。一个定义只选一种正文绑定和一种伤害绑定；前三个模板已包含StaticCardDamage，不再额外展开。动态公式可以直接绑定共享pure过程；剩余专属公式才在所属模块定义pure过程与case。多个效果继续显式排列call，不以模板登记顺序代替正文顺序。

效果内部的普通／升级常量选择共用pure过程`CardUpgradeValue(card: Ref(Card), baseValue: Int, upgradedValue: Int) → Int`。格挡、抽牌、能量、Power层数、失血、成长与段数模板调用同一过程；内容仍拥有两个输入值及额外的动态计算。目标卡是否需要选敌不影响自身Power效果，统一调用`ApplySelfPowerEffect.WhenPlayed(play,owner,card)`；临时效果通过untilTurnEnd显式选择原有到期操作。

每次执行的 BeforeCardExecution／AfterCardExecution 载荷均为 owner、card、play。正文之后依次经过 ExecuteCardEnchantment 和 ExecuteCardAffliction，存活时才继续完成通知。CardPlayRecord 保存付款、原始 X、自动标记、目标、结果区、ActionNumber、ParentPlay 和 PlayIndex／PlayCount；内部执行历史不公开。新效果收到 play 后继续显式传给子操作。

## 供内容与mod组合的公共操作

公共过程不检查调用卡的DefinitionId；正文直接传参数，也可以由共享模板包装。只有绑定、数值及执行顺序属于具体卡牌。现有单一原版消费者不妨碍语义通用的过程进入公共库。下表沿用本页的单玩家牌区范围：owner为Ref(Player)，card为Ref(Card)，play／target未特别说明时为Binding；有返回值的call必须用into接收。

[手牌操作](../../../../mod-the-spire/base-spire/cards/rules/hand-operations.spire)：

| 操作 | 其他参数／返回 | 行为与边界 |
| --- | --- | --- |
| `SelectAndExhaustHandCard` | owner、random: Bool | 随机或原生单选手牌后完整消耗；空域／取消不执行消耗 |
| `UpgradeHandCards` | owner、all: Bool | 升级所有可升级手牌，或原生单选一张；不创建替代实例 |
| `MoveSelectedDiscardCard` | owner、to: String(16)、reason: String(32) | 原生单选弃牌再移动；回顶传draw-top，仍经过MoveCombatCard的检查与通知 |
| `ExhaustHandCards` | owner → Int | 捕获数量和原手牌批次，逐张消耗后返回原数量；反应中新入手的牌不追加，供FiendFire／Stoke分别组合攻击／生成 |
| `ExhaustNonAttackCardsForBlock` | owner、block: Int | 捕获非攻击手牌，每张先消耗再GainBlock；不能改成末尾合计，否则反应顺序不同 |
| `TransformHandCards` | owner、kind: String(16)、replacementDefinition: String(64)、upgraded: Bool | 匹配kind的手牌逐张创建／升级新牌、移除原牌、放置新牌；不继承原附魔，不发消耗事件 |
| `GenerateCardsToHand` | owner、count: Int、attacksOnly／upgraded: Bool | 依原池逐张随机生成，可升级后入手；满手走弃牌，无候选跳过；不是候选展示／选择操作 |
| `GenerateCombatCardWithCost` | owner、attacksOnly: Bool、cost: Int、source: String(64)、lifetime: String(16) → Binding | 生成并附加set费用修正，返回尚未放置的实例或none；调用方再AddGeneratedToHand，保证改费早于移动通知；资格、费用及寿命沿用底层检查 |
| `DrawUntilCardKindChanges` | owner、kind: String(16)、limit: Int | 至多抽limit张，抽到不同kind或DrawOne未抽到牌即停止；保留抽牌限制和LastDrawn语义 |
| `AbsorbRandomHandAttack` | owner、card | 随机选一张手牌攻击，读取无目标修正伤害，加到card成长再消耗所选牌；空域不抽样、不成长 |

[战斗数值与结果](../../../../mod-the-spire/base-spire/cards/rules/combat-values.spire)：

| 操作 | 参数／返回 | 行为与边界 |
| --- | --- | --- |
| `PlayerCombatFlag` | owner、flag: String(32) → Bool，pure | flag接受lost-hp／exhausted-card，其他值明确失败 |
| `PlayerCombatFlagValue` | owner、flag、baseValue／flaggedValue: Int → Int，pure | 按同一标志读取数值，供条件格挡／段数组合；不派发资源命令 |
| `EnemyPowerHits` | target: Ref(Enemy)、power: String(64)、hits／poweredHits: Int → Int，pure | 指定Power正层时选poweredHits，否则hits |
| `ReadCardDamageFromBlock` | owner、card、target → Int，pure | 与ReadCardDamage相同签名，直接绑定即可复用当前玩家格挡公式 |
| `ReadUntargetedCardAttackDamage` | owner、card → Int，pure | 使用ModifyAttackAdditive及ModifyAttackMultiplier与四位整数计算，保留吸收行为的无目标语义；不消耗修正状态，完整伤害预览使用PreviewPoweredCardDamage |
| `GrowCardDamage` | card、amount: Int | 只增加该实例DamageGrowth；适用于Rampage及吸收行为 |
| `AttackKilledEnemy` | attack: Ref(Attack)、target: Ref(Enemy) → Bool，pure | 只读取本命令对本目标的致死DamageResult，不用当前HP或其他攻击的结果推断 |
| `AmplifyEnemyPowerFromCard` | owner、card、play、target: Ref(Enemy)、definition: String(64)、multiplier: Int | 正层时以当前层数×(multiplier−1)调用带来源Power施加操作；不直接写层数，不绕过阻止／修正 |

[费用库](../../../../mod-the-spire/base-spire/cards/rules/costs.spire)的`ModifyCombatCardCostsByDefinition(owner,definition: String(64),source: String(64),operation: String(16),amount: Int,lifetime: String(16))`逐个修改同拥有者的匹配战斗实例，排除RunDeck；仍由AddCardCostModifier验证参数并刷新费用。[自动出牌库](../../../../mod-the-spire/base-spire/cards/rules/autoplay.spire)的`AutoPlayExhaustedCards(owner,definition: String(64),forceExhaust: Bool)`捕获匹配的耗尽牌，逐张调用AutoPlayCard，供回合末绑定复用。

[ExhaustEnergyReaction模板](../../../../mod-the-spire/base-spire/cards/rules/card-reactions.spire)接收definition、energy／upgradedEnergy，监听CardExhausted后按升级值回能。卡牌保留use绑定；未来需要同反应的mod无需复制监听。HowlFromBeyond和Stomp的事件时点与定义绑定仍留在所属内容，实际自动出牌与整批改费调用上述公共过程。

例如Stoke的正文只需先调用ExhaustHandCards并into接收size，再调用GenerateCardsToHand，传入size与当前升级状态。mod可以将第二步替换成CardAttack或GrowCardDamage，无需拷贝手牌遍历与生命周期实现。

## 攻击与结果

下列声明位于 [combat/attacks.spire](../../../../mod-the-spire/base-spire/combat/attacks.spire) 和 [damage-results.spire](../../../../mod-the-spire/base-spire/combat/damage-results.spire)。

| 操作 | 参数／返回与边界 |
| --- | --- |
| `CreateCardAttack` | owner: Ref(Player)、card: Ref(Card)、play／target: Binding、amount／hits: Int、mode: String(16)、dynamic: Bool → Binding；mode 只接受 single／all／random |
| `ExecuteAttack` | attack: Ref(Attack)；一对 BeforeAttack／AfterAttack，ModifyAttackHits 修正段数；逐击刷新存活目标，本批全部提交后才通知结果及死亡 |
| `CardAttackWithResult` | 与CreateCardAttack同参，返回Binding；创建并执行后返回本次Attack，供致死奖励及结果读取 |
| `CardAttack` | 与 CreateCardAttack 同参，无返回；创建并执行一个命令。多段只调用一次，不能用多次调用代替一个多段攻击 |
| `PreviewPoweredCardDamage` | owner: Ref(Player)、card: Ref(Card)、target: Binding → Int，pure；和执行共用 CalculatePoweredCardDamage，无临时实体或计数消耗 |
| `DamageEnemyFromPlayer` | owner: Ref(Player)、target: Ref(Enemy)、amount: Int(0..999999)；非攻击直接伤害，明确玩家攻击者，card／play保持none |
| `DamageAllEnemiesFromPlayer` | owner、amount；创建独立DamageBatch，固定存活目标、整批提交、结果通知、死亡通知；不发Before／AfterAttack，嵌套回调不能提前结算胜利 |

DamageResult 保存请求／最终伤害、Blocked、实际 HP 损失 Unblocked、Overkill、BlockBroken、FullyBlocked、Killed，以及 Batch／Attack／hit／card／play／攻击者／受击者引用。直接群伤Batch与Attack分开；DamageDepth、AttackDepth、PlayDepth共同约束外层胜利提交。`DamageResolved(result)` 发布每个结果；`PlayerDamaged(owner,result)` 用于玩家存活后的受伤消费者。直接 `LosePlayerHp` 不自动获取当前卡来源；卡效果使用 `LosePlayerHpFromCard(owner,card,play,amount)`。

Vigor 按 Attack 保存开始层数并在对应 AfterAttack 消耗；PenNib 按源使用卡引用及第十次执行计数，不能改成只比较 play 相等。未支持死亡防止、召唤／复活和任意来源模型；当前百分比修正仍使用四位整数中间值，不能宣称完整 decimal 精度。

## 费用、牌区与关键词

| 操作 | 参数／返回与语义 |
| --- | --- |
| `AddCardCostModifier` | card: Ref(Card)、source: String(64)、operation: String(16)、amount: Int、lifetime: String(16)；operation 为 set／add，lifetime 为 turn／play／turn-or-play／combat，按插入顺序折叠 |
| `LocalCardCost` | card → Int，pure；BaseCost为-1时直接保留哨兵；非负费用折叠独立修正后夹至0..9。RefreshCardCost更新公开Cost缓存，实际费用仍经GetCardEnergyCost的Power修正 |
| `ExpireCardCostModifiers` | card、boundary: String(16)；共享出牌／回合流程负责到期。新战开始清除所有临时费用，克隆独立复制现有修正 |
| `MoveCombatCard` | owner、card、to: String(16)、reason: String(32)；to 为 hand／draw-top／draw／discard／play／removed。拒绝永久牌及外来拥有者；满手转弃牌，通知 CardMoved(owner,card,from,to,reason) |
| `DiscardCombatCard` | owner、card；只接受手牌，移动后发 CardDiscarded。回合末 flush 和正常打出结果区只发移动通知，不冒充主动弃牌 |
| `ExhaustCombatCard` | owner、card；移动后依次发 CardMoved、CardExhausted；拒绝永久牌、已耗尽和离场牌 |
| `CardHasKeyword` | card、keyword: String(16) → Bool，pure；innate／exhaust 的基础事实加 ModifyCardKeyword 修正，retain／ethereal／unplayable／eternal 可由内容提供 |
| `CardPlayAllowed` | owner、card、auto: Bool → Bool，pure；共享已定义／拥有者／Unplayable／ShouldPlayCard 门禁；手动另验证手牌、回合、能量和目标 |
| `FinishTurnHand` | owner；保存原手牌的效果牌与其他Ethereal牌名单，先耗尽后者，再依原手牌顺序将效果牌移入Play、通知CardTurnEndInHandEffect并移入结果区。死亡守卫拒绝后续移动；仍活跃才BeforeHandFlush、留手筛选、整批移动及AfterHandFlush。新抽牌不追加旧批次 |
| `CardHasTurnEndInHandEffect`／`CardTurnEndInHandEffect` | 前者payload为value: Bool、card: Ref(Card)，pure修正返回资格；后者通知owner、card，所属模块执行效果。Infection使用这两个入口 |
| `DamagePlayerFromCard` | owner: Ref(Player)、card: Ref(Card)、amount: Int(0..999999)；玩家为伤害来源与目标，保留牌源、空play，可格挡且无加成，经DamageResult通知及失败判定 |
| `AutoPlayFromDrawPile` | owner、play: Binding、count: Int、forceExhaust: Bool；先预取并移出整个批次，再逐张共用自动出牌流程；嵌套批次独立 |

上述牌区仍为单玩家全局集合。回合末手牌效果已由Infection接入；完整多选／取消策略和所有变换政策未由本表一并承诺。

## 复制、附魔与永久牌

`CloneCombatCardToPile(owner: Ref(Player), source: Ref(Card), to: String(16), reason: String(32)) → Binding`组合完整战斗克隆与移动。先通过CloneCombatCard复制实例值、费用修正及附属能力，依次完成CardCloned、CardEnteredCombat，再调用MoveCombatCard处理目的区、满手回退和CardMoved，返回实际新实例。该操作只移动新实例，自身不抽样，回调仍按原顺序执行；特殊流程需要在克隆与移动之间插入操作时，继续分别调用底层过程。

卡牌正文可使用`ClonePlayedCardEffect(definition,destination="discard")`，生成的WhenPlayed只接收play／owner／card，不依赖敌人目标；它以reason="clone"复制本次牌。Anger显式先攻击后调用该效果。Juggling直接复用CloneCombatCardToPile，目的区为hand、reason为generate；克隆与新生成同定义牌继续分别使用CloneCombatCard与CreateCombatCard，不混用初始化或复制通知。

`CardCloned(owner,source,card)` 复制战斗实例；`CardCombatCopied(owner,source,card)` 从永久实例建立战斗副本；`CardRemoving(card)` 在销毁前让所属能力清理。共享 Card 仍复制共有事实，私有状态由所属模块消费生命周期通知，不再扩专属 Bool 或两份复制字段清单。

`EnchantCard(owner,card,definition: String(64),amount: Int(1..999))` 先执行所属纯资格，再派到 ApplyCardEnchantment。基础 `CanEnchantCard` 拒绝状态／诅咒／任务牌、已有附魔及牌组中的 Unplayable。CardAttachment 拥有 Card、DefinitionId、Kind、Amount、Status；Sown 的执行阶段、禁用状态由自己的模块处理。升级不重建附魔，战斗克隆复制当前状态，永久副本不反写来源。依附投影只在关联卡可识别时发布，不另公开附魔集合计数。

`SelectPermanentCard(owner,purpose: String(16),enchantment: String(64),cancelable: Bool) → Binding` 复用原生选择；purpose 为 upgrade／remove／enchant。空域返回 none；不可取消且单候选时自动选择，其余按可取消性发布 EntityRefList。`RemovePermanentCard(owner,card)` 只在战斗外移除可移除永久牌，发通知并清理附属状态。

`HealWithSource(owner,amount,source)` 和 `GainGoldWithSource(owner,amount,source)` 提供来源及实际变化通知；GainGold 先经过 ModifyGoldGained／AfterModifyingGoldGained，再提交正收益。`LoseGold(owner,amount,reason,source)` 夹至实际余额，reason 为 spent／lost／stolen；需要余额足额的购买使用 `SpendGold(owner,amount,source)`。

SapphireSeed 与 DoorsOfLightAndDark 通过 `EnterDevelopmentSapphireSeed`／`EnterDevelopmentDoorsOfLightAndDark` 进入开发访问。叶模块只定义选项与效果，共享 EventVisit 保存页面、状态、返回位置与访问序号；`CloseDevelopmentEvent(visit)` 关闭。SapphireSeed与DenseVegetation另由正常地图的完整源事件选择器进入，关联同一RunVisit；完成非战斗分支后使用 `CloseNormalEvent(visit)` 返回地图，开发关闭入口不能推进正常Run。DenseVegetation的 `DenseVegetationFight(visit)` 仅在REST后合法，建立公开Parent／ChildVisit关联的四Wriggler子战斗；普通奖励关闭后结束原父事件，BattlewornDummy通过 `EnterDevelopmentBattlewornDummy` 提供开发升级档：三个BattlewornOption实例选择SETTING_2进入真实子战斗，EventCombatVisit.Parent与EventVisit.CombatVisit双向关联。击杀后随机升级最多两张永久牌，三回合超时只返回同一父事件；完成后用 `CloseDevelopmentEvent(visit)` 关闭。药水／遗物档选择后明确报未支持，第三幕正常可达尚未接通。

## 生成候选与选择的行为要求

以下规定恢复或扩展内容时应保持的源行为，具体支持范围以当前包和操作表为准，不将旧作者语法作为可调用接口。

生成候选使用完整源池，按源顺序过滤单人资格、战斗可生成性、稀有度和类型。需要洗牌后截取时，对整个合法池完成稳定洗牌，消费 n−1 次抽取，再取至多请求数量的不同定义；不能仅抽取请求数量次代替。候选存放于独立公开集合，不提前入手或触发入战通知，隐藏的整池顺序不公开。

原生选择允许的数量、取消与自动选择由具体命令规定。恢复不重抽；所选真实实例按满手规则入手或进弃牌，未选和取消实例清理。零费用持续到回合末的生成牌，不能首次打出后提前复原。战斗已结束时，牌区选择不再开启或执行选中子程序；生成候选选择单独按源规定处理。

## Sown 与实例生命周期要求

Sown 使用独立附着状态。新卡无附魔；复制保留源规定状态但实例独立，升级不清除，变换为新牌不继承旧附魔。资格排除 Status／Curse／Quest、永久牌组中的 Unplayable 及已附魔实例，Sown 不叠加。

SapphireSeed 的 Plant 按锁定来源自动附魔首张合资格永久牌，不暂停选牌；空域直接完成。Eat 先回复9，再调用永久升级选择；空域不产生决策。两项均不消费随机。

牌正文完成且玩家仍存活时，在完成通知前触发 Sown：正常状态先获得层数对应能量，再禁用；禁止获得能量时仍禁用。末杀仍执行，玩家先死亡则不触发。副本禁用状态不回写永久牌；下一战从永久实例创建的新副本按永久状态初始化。战斗克隆与永久到战斗复制分别遵守源策略，不能统一重置掩盖差异。

## Power 特殊持续与伤害上限要求

负层无限持续必须由能力显式声明：负非零层维持倍率且不衰减，正层按正常规则衰减，零层不生效。Shrink 对符合资格的伤害乘0.7；Strength 继续按有符号层数加减，未声明无限持续的 Weak／Vulnerable 不自动获得该语义。

生命损失上限在格挡后应用，只有实际未格挡伤害才消耗一层，归零立即移除；全阻挡不消耗。SlipperyPower 将单次失血限制为1，多段逐击重新读取状态，不能让首次上限延续到后续命中。
