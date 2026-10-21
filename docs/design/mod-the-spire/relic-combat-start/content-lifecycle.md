# 遗物内容接续与生命周期

关联 CORE-AUTHOR-05／DSL-RELIC-CONTENT-01／02／03；当前状态见[GitHub 任务清单](https://github.com/QingXiang22/auto-the-spire/issues/28)。本约定覆盖纯DSL当前单人开发范围，遗物来源目录保留297项；分别报告已有定义、行为执行结果、独立比较与正常 Run 的验证结果。

## 内容与源依据

源为sts2的Models/Relics中相应类，原始SHA256与tools/base-spire-dev/fixtures/source-catalogs.json的relics域逐项核对。Pear最大生命+10、Mango+14；LeesWaffle最大生命+7后回满，FakeLeesWaffle治疗最大生命10%向下取整；OldCoin获得300金币。BlackBlood胜利治疗12；CaptainsWheel第三回合清格挡后得18无加成格挡。BagOfPreparation首回合基础手牌抽取+2，与Innate数量取max而非多发两次抽牌；RegalPillow营火基础治疗+15后封顶。

Nunchaku每10次攻击打完+1能量，实例计数跨回合／跨战保留。Kunai／Shuriken每回合每3次攻击打完分别+1敏捷／力量；OrnamentalFan同样计数得4无加成格挡；LetterOpener每回合每3次技能打完对所有存活敌人造成5无加成伤害。重复／自动出牌各计实际次数，多段攻击只计一次，拥有者不同不串计数。每回合计数在开场及玩家回合开始的早期站点重置，不能在AfterPlayerTurnStart才重置而吞掉自动打出。StoneCalendar第7个玩家回合结束前对存活敌人造成52无加成伤害。CentennialPuzzle本场第一次正的未挡伤害后抽3，先标已用再抽，下一战恢复；零伤害／全挡不触发。

## 状态、接口与顺序

简单拾取资源组（DSL-RELIC-CONTENT-05／RELIC-G19）复用ObtainMaxHpRelic与PickupResourceRelic：FakeMango最大生命+3、LoomingFruit+31、NutritiousOyster+11，均同时治疗相同数值；SignetRing获得999金币、GoldenPearl获得150金币。完整AfterObtained已核对；FakeMango的MerchantCost=50保留为来源事实，当前不据此宣称商店定价；LoomingFruit的存档UniqueId仅决定图标，不消费玩法随机或改变数值。

两模板以RelicObtained的本次relic绑定有效持有实例，按库存顺序响应并在执行前复核资格；早期订阅移除或熔化本次遗物后，不再发放拾取资源。当前实例获得一次，旧同定义实例不会因新获得而重发；开发动作按本持有者HasOwnedRelic阻止重复，包括熔化但仍持有的实例，其他持有者不阻止本玩家开发获得。移除不撤回永久生命／治疗／金币；移除后再次真实获得新实例按源再发一次。开战／回合／结束不重放拾取，也不消费随机。参数差异用逐件表驱动执行结果，机制验证拾取失效、两持有者和同定义新实例；正常来源资格及资源修正者仍按原范围恢复。

现行统一接入见[通知与修正监听](../effect-listeners.md)。通知和修正均声明instances，按实际库存顺序响应；主流程不再调用遗物专属查询。

所有局内状态在Engine实体。Relic.Counter保持独立实例计数，OncePerCombat使用标志仍归Relic，ArtOfWar私有标志归ArtOfWar.State。提供pure HasActiveRelic(owner,definition)与共用有效实例筛选；修正参与者由模板声明纯监听，不在Host按ID分派。

拾取效果在入Relics后发RelicObtained时执行，不能只放开发Obtain动作；模板拾取订阅按本次relic过滤，旧Strawberry迁移到相同接缝，不重复奖励。永久最大生命不会在移除时撤回，治疗和金币不能因移除反向扣除。OldCoin源商店／幕资格保留在来源说明，当前显式开发获得不等于正常池资格已恢复。BlackBlood当前只交付持有效果，不宣称先古之民升级替换BurningBlood已接入。

新增BeforePlayerTurnStart在回合编号增加后、抽牌／自动出牌前发出，供当回合计数重置。新增BeforePlayerTurnEnd在回合末自动出牌后、弃牌前发出，供StoneCalendar；完成当前群体伤害后沿现有胜负判定，终局后不得继续敌方行动。EndDevelopmentCombat手动终止不发胜利治疗。

CentennialPuzzle消费已支持的PlayerHpLost站点，要求存活且战斗活跃，先写本实例已用再DrawCards；来源为当前未挡伤害／牌源掉血，未知伤害修正与死亡防止未额外承诺。移除／熔化使后续触发和动态手牌／营火修正失效；每次获得新实例从默认计数开始。

## 伤害与能量修正（DSL-RELIC-CONTENT-02）

StrikeDummy／FakeStrikeDummy对有Strike标签的牌源加成攻击分别加3／1，先与力量相加，再应用虚弱和易伤，最后取整和扣格挡；非Strike及无加成伤害不参与。DamageEnemyPowered改为显式传owner、card、target和amount，所有真实调用方迁移，不以全局CurrentCard猜来源；当前单人范围owner同时是攻击者和牌拥有者，召唤物／跨拥有者攻击另行恢复。通过ModifyAttackAdditive纯监听贡献加值，不改变牌本身的基础伤害。重复与多段每段均参与，嵌套自动出牌以各自card为准。

IceCream监听ShouldResetEnergy决定保留资格，首回合仍重置为GetMaxEnergy，后续回合将当前余能加GetMaxEnergy，按现有资源域上限999封顶。能量重置与补足不走GainEnergy，不能被no-energy-gain阻止；重置完成发AfterEnergyReset，再打开回合门并发AfterSideTurnStarted。移除／熔化后下一重置即时失效。

ArtOfWar.State按遗物实例保存ThisTurn和LastTurn，获得时创建、移除时销毁，AfterCardPlayed只记录本拥有者实际打完的Attack，自动／重复同样记录；在玩家侧末AfterTurnEnded转交上一回合标志，AfterEnergyReset在TurnNumber>1且上回合未攻击时GainEnergy(1)，随后清理两标志。首回合不奖励；重置站点早于起手抽牌／回合开始自动攻击。战斗开始与CombatEnded清理，手动终止后下一战也重新清理。移除销毁状态，熔化不参与触发与奖励。判别须覆盖连续空攻击回合、攻击后下一回合失奖而再下一回合恢复、技能嵌套自动攻击与回合末自动攻击、奖励受no-energy-gain抑制、与IceCream叠加及不同持有者隔离。

## 效果 X 修正（DSL-RELIC-CONTENT-03）

源依据为 ChemicalX.ModifyXValue、CardModel.ResolveEnergyXValue／SpendEnergy、CardEnergyCost.GetAmountToSpend 与 CardCmd.AutoPlay。ChemicalX 对本持有者的效果 X 加2，不修改实际支付、捕获值或能量。当前仅恢复已实现的能量 X 牌，星星 X 仍待对应角色机制。

`Card.PaidX` 保留支付前捕获的能量 X：手动打出为本次实际应付的全部剩余能量；自动打出为开始自动出牌时的当前能量且不扣费。普通免费标志、Corruption／FreeAttack 不令手动 X 牌免费。新增 pure `ResolveEnergyXValue(owner,card)`，对 X 牌以 PaidX 为初值调用 `foldEvent(ModifyXValue, value, owner, card)`。它不写回 PaidX、不消费随机、不缓存修正结果。已有 X 效果在各次真实效果开始时读取一次结果，再运行该次效果；重复出牌复用原捕获 X，重新征询当前有效监听，不把上次加值累积到下一次。该入口前置为能量 X 牌；非 X 查询返回0、不发布修正，这一点与源内部错误调用抛异常不同，当前 pure DSL 不允许终止会话的 fail；生产非 X 效果不调用此入口。

`XValueRelic` 模板以有效持有实例监听 ModifyXValue，按库存顺序返回 value+amount；具体 ChemicalX 只配置 amount=2。过滤 Holder／IsMelted 并校验牌拥有者；移除／熔化即时失效，不在出牌主流程判断遗物 ID。Whirlwind 每次效果取修正 X 作为全体攻击段数；Cascade 取修正 X 后，升级额外+1，再依次自动打出牌堆顶部。嵌套自动打出使用子牌自己的捕获值和拥有者，不借父牌上下文。

验收需区分0／正能量、普通／升级、与无遗物基线及非 X 牌的差别；每次重复只支付一次且不重复叠加修正；Havoc／Cascade 的嵌套自动 X 捕获正确；移除／熔化／不同持有者及同定义两实例隔离；X 查询多次调用不改变状态、版本或随机游标；在首次效果后的移除使后续重复使用新监听集合。覆盖报告必须包含实际 ChemicalX 获得和触发，公开真实stdio执行3能量Whirlwind产生5段攻击，保留PaidX=3且能量降为0。

## 拾取随机永久升级（DSL-RELIC-CONTENT-04／RELIC-G01）

源Whetstone与WarPaint在AfterObtained分别筛选可升级Attack／Skill，StableShuffle(Niche)后Take(2)；SandCastle筛选所有可升级牌后Take(6)。已升级和CanUpgrade=false不入候选，仅处理该持有者RunDeck中的永久实例。按SourceCardSortKey使用源非稳定排序，再完整降序Fisher-Yates；即使只取前两张也消费全部候选n−1次Niche，空／单张不消费。不得改用CombatCardSelection、Rewards或逐次samplePick，不发战斗AfterShuffle，不移动RunDeck成员或改变顺序。

cards拥有`UpgradeRandomPermanentCards(owner: Ref(Player), kind: String(16), amount: Int(1..999))`完整操作，kind仅接受attack／skill／all，非法参数在创建选择状态或消费随机之前拒绝。每次调用有独立临时选择实体和按序号排列的候选引用；排序与洗牌只改选择状态。选择完成后按选中顺序调用UpgradeCard，在原永久实例上应用定义层升级并发CardUpgraded；后续战斗副本保留升级。临时状态逐项销毁、最后销毁操作实体，允许嵌套升级事件开启另一选择而不污染父调用。临时类型不进入PlayerView。

遗物用统一实例订阅监听RelicObtained，instances只选本次relic且仍有效、未熔化；模板参数仅有definition、kind、amount。直接调用ObtainRelic与开发获得动作共用此链，每次真实获得只触发一次；移除不撤回已发生的永久升级，开战／回合不重新执行。当前只交付显式获得，常见／Ancient来源资格保持原源池，不宣称正常地图来源已经恢复。

判别包括每件正例、类型筛选、0／1／不足／超过N张、已升级和不支持升级的排除、重复定义实例标识及RunDeck顺序不变；固定源向量比较选中实例／升级事件顺序／Niche游标，并验证其他流不变、不触发TheAbacus。嵌套获得用不同调用状态隔离，返回后无选择实体残留；两场战斗复制与移除后的永久结果保留。公开stdio从实际永久牌组获得遗物并消费升级副本，不人工注入升级结果。

## 回合开始资源（DSL-RELIC-CONTENT-06／RELIC-G04）

Candelabra在本持有者第2回合GainEnergy(2)，Chandelier在第3回合GainEnergy(3)，VeryHotCocoa在TurnNumber<=1时GainEnergy(4)。三者复用TurnStartEnergyRelic；新增throughTurn布尔参数，默认false保持精确相等，true使用小于等于turn，保留Cocoa源条件。当前回合入口先递增编号再发通知，首个可达回合为1。拾取和开战不回能，每战重新按本场回合号判断。

FakeHappyFlower复用EveryTurnsEnergyRelic(amount=1,turns=5)，每次本持有者AfterSideTurnStarted先将本实例Counter加1模5，归零时才GainEnergy。计数跨战保留，获得新实例从0开始；NoEnergyGain只抑制奖励，不阻止计数递增或归零。移除／熔化后不再推进计数。视觉状态、等待动画和源商店价格不属于本次玩法恢复。

Sai使用TurnStartBlockRelic，每次本持有者AfterSideTurnStarted调用GainUnpoweredBlock(7)，不受敏捷／脆弱影响。此事件位于清除格挡、AfterBlockCleared、能量重置之后、起手抽牌之前；不能复用HornCleat的AfterBlockCleared，因为该事件在能量重置前；首回合和保留格挡时也会发布。首回合叠加Anchor，次回合先清理再叠加HornCleat与Sai。资源沿现有上限999，能量走GainEnergy并受NoEnergyGain限制，重置MaxEnergy不受该限制。

三模板均按实际库存顺序绑定有效、未熔化且Holder匹配的实例，执行前由统一调度重查资格。开发Obtain仅检查本持有者已有定义，不让其他持有者阻止获得；直接ObtainRelic同样触发后续回合效果。同定义实例分别计数并各自贡献。非法重复BeginDevelopmentTurn在发布通知前拒绝，不能推进计数或重复奖励。判别复用回合测试，补每件数值执行结果、跨战、新实例、失效与持有者隔离、NoEnergyGain和资源封顶；公开stdio从实际获得到多回合和跨战结果。正常来源／Run和独立原版双侧仍未验证。

## 无状态攻击修正（DSL-RELIC-CONTENT-07／RELIC-G05）

MiniatureCannon的完整ModifyDamageAdditive对有牌源、牌已升级的加成攻击加3；源资格为攻击者是本持有者或牌属于本持有者，二者满足其一即可。UpgradedAttackDamageRelic监听ModifyAttackAdditive，牌为空先跳过，再按Card.Upgraded及显式owner／Card.Owner与本实例Holder判断；不要求Strike标签，不直接改基础伤害。当前DamageEnemyPowered的owner表示玩家攻击者，不能借CurrentPlay猜牌源；召唤物攻击仍未恢复。加值与力量／Strike加值同在乘法前，每段攻击分别应用，自动和重复读取各自实际牌源。

PaperPhrog由原VulnerablePower查询攻击者持有遗物后，将目标易伤倍率从1.5加0.25；无易伤不生效，不增强持有者自身受到的易伤。PaperKrane由原WeakPower查询目标持有遗物后，将攻击者虚弱倍率从0.75减0.15；无虚弱不减伤，也不增强持有者自己攻击时的虚弱。恢复的消费者分别是玩家攻击敌人和敌人攻击玩家，双侧标识与源谓词一致；不能把两件作为整段伤害的固定乘数。

新增pure修正通知`ModifyVulnerableMultiplier(value: Int, owner: Ref(Player), card: Binding, target: Binding)`，value单位为百分数点，Vulnerable实例确认目标且层数>0后才以150征询，之后加已有Cruelty的百分数点，再乘当前伤害。新增`ModifyEnemyWeakMultiplier(value: Int, owner: Ref(Player), attacker: Ref(Enemy))`，owner为防守玩家；EnemyWeak确认攻击者且层数>0后以75征询再乘伤害。参数化StatusMultiplierRelic的vulnerableBonus=25或weakReduction=15，默认各为0，只对有效且Holder匹配的实例贡献。两个站点不在无状态时调用，不修改Power层数，也不改变时长与衰减。

乘法仍沿现有10000定点链保留小数，最终只在扣格挡前向下取整；例如升级Strike9+力量1+Cannon3，经玩家虚弱0.75及目标易伤(1.5+0.25+Cruelty0.20)，最终floor(13*0.75*1.95)=19，再扣格挡。敌方(基础13+力量1)*0.60*玩家易伤1.5=12.6，最终12再扣格挡。未加成伤害／直接失血不经过这些站点。监听只读，不写状态、不消费随机或缓存；移除／熔化即时失效，开发获得仅按持有者去重。现有取整精度及百分数域保持，未来更细小数倍率另补约定。

直接ObtainRelic允许开发测试场景创建同定义多实例，沿统一监听约定分别贡献；weakReduction返回的倍率最低为0，不能产生负伤害导致加血或增加格挡。正常库存唯一性仍待来源域恢复，该开发多实例折叠不作为原版GetRelic只取一件的独立比较验证记录。

判别包含三件实际获得及数值、Cannon未升级／非Strike／多段与重复／自动、两个倍率的有无状态／错误侧／其他敌人、Cruelty叠加与最终取整、实例失效／持有者隔离、重复pure查询和Thrash预览消费者。公开stdio从真实获得、升级牌／施加状态到伤害与敌方阶段，不注入结果。正常来源、独立原版双侧及正常Run仍未验证；Akabeko、PenNib和MysticLighter需要各自完整机制，不归本开发场景。

## 判别与边界

逐件真实获得／触发的数值执行结果；拾取通过直接ObtainRelic也执行一次；非首次／非法重复／去除后的负例；同定义不同实例／不同持有者隔离；每3次／第10次触发、单回合6次、跨回合重置与跨战保留差别；自动与重复计数、多段只计1；最后击杀后收尾、StoneCalendar群体击杀不提前漏敌人；Bag与Innate／NoDraw／满手，Puzzle在首次伤害后立即抽牌且无递归重触发；RegalPillow封顶与失效。公开真实stdio验证拾取、计数、Strike伤害和跨回合能量消费者，覆盖验收条件要求所有新增definition有实际场景。

玩法全部由base DSL执行，不恢复参考玩法。内容组实际加载触及原256个模板实例上限，必要通用维护将总量增至8192；32层深度、递归与4096个展开成员限制保持，内核回归验证300个真实实例。独立原版两侧未执行保持未验证。完整获得修正、随机遗物来源／资格、升级替换、死亡防止、其他遗物与正常Run仍待恢复，不注册空效果补数量。
