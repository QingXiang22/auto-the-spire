# 铁甲战士使用的共享 DSL 操作

本页供内容作者查询调用入口，适用于 `base-spire-dsl-relic-draw-dev-v1` 的单人、全部解锁开发配置。范围与验证条件见[约定](spec.md)，当前进度见[GitHub 任务清单](https://github.com/QingXiang22/auto-the-spire/issues/28)。这些过程由 Engine 执行；Host 只投影状态和提交合法候选。

新增正文分派、费用、攻击、附魔和永久选择的准确签名见[行为操作参考](../../../development/base-spire/reference/card-behavior.md)。

## 定义、实例与牌区

实现位于 `mod-the-spire/base-spire/cards/rules/`，所有参数名区分大小写。下面的 `owner` 是 `Ref(Player)`，`card` 是 `Ref(Card)`；返回 `Binding` 的过程可能返回 `none`，调用方必须判断。

| 入口 | 参数与返回 | 语义与边界 |
| --- | --- | --- |
| `CardDefinition` 模板 | `definition`、普通／升级费用、类型、`rarity`、目标、关键词、伤害元数据；可选 `inIroncladPool`／`generatable` | 自动注册可执行定义。源池资格、奖励稀有度和战斗生成资格分别声明；内容叶组合模板并绑定唯一有序正文及纯伤害提供者 |
| `CreateCardInstance` | `owner, definition` → Binding | 验证可执行定义、创建独立实例并发 CardCreated，尚未入牌区或战斗，不发 CardEnteredCombat；奖励复用该入口 |
| `ObtainCardReward` | `owner, card` | 只在存活、非战斗且存在本玩家待领卡牌奖励时，将所选同一实例从 CardRewardOptions 移至 RunDeck，再发 CardObtained；保留升级，未选清理由奖励域完成 |
| `CreateCombatCard` | `owner, definition` → Binding | 从已注册定义创建全新战斗实例，初始化字段后发 `CardEnteredCombat(clone=false)`；未知定义在创建前明确失败。尚不入牌区 |
| `CloneCombatCard` | `owner, source` → Binding | 保留实际升级、类型、Strike 标签、成长、费用修改与回合末自动打出能力；不携带 PermanentSource、当前 PaidX 或强制耗尽结果。依次发 CardCloned 与 `CardEnteredCombat(clone=true)`，私有费用／附魔监听自行复制，避免 Stomp 重复初始化减费 |
| `AddGeneratedToHand` | `card` | 未入牌区的新建／复制实例进入手牌；满十张则进入弃牌堆。不是普通抽牌，不发抽牌事件 |
| `ExhaustCombatCard` | `owner, card` | 仅处理该玩家 Hand／DrawPile／DiscardPile／InPlay 中未耗尽的战斗牌；先移动，再发 CardExhausted。永久牌、已耗尽牌、离场牌拒绝 |
| `SelectHandCard` | `owner, upgradeOnly` → Binding | 空域返回 none，单张自动选，多张发布必须单选的原生决策。升级模式仅选 CanUpgrade 且未升级牌；当前 InPlay 不属于手牌 |
| `SelectDiscardCard` | `owner` → Binding | 同样按 0／1／多张处理，仅选该玩家弃牌。Headbutt 消费结果并移到抽牌堆顶部 |
| `UpgradeCard` | `card` | 验证实际可升级性，先标记升级，再发 CardUpgraded；不会把战斗副本反写到永久源 |
| `UpgradeRandomPermanentCards` | `owner: Ref(Player), kind: String(16), amount: Int(1..999)` | kind为attack／skill／all；仅筛选本拥有者RunDeck中可升级且未升级的实例，按源排序后完整Fisher-Yates(Niche)，再按洗后顺序升级前amount张。永久顺序不变、不发AfterShuffle；每调用独立临时状态并在返回前清理。拾取模板与边界见[约定](../relic-combat-start/content-lifecycle.md#拾取随机永久升级dsl-relic-content-04relic-g01) |
| `GenerateCombatCard` | `owner, attacksOnly` → Binding | 从完整已解锁单人源池按 CardFactory 的生成资格抽样。85 张源标识全部保留；78 张可随机生成。Feed／NotYet、Basic／Ancient 按源排除；GiantRock／Shockwave 不混入铁甲池 |

`RemoveFromCombatPiles` 是上述完整操作的内部移动接缝，不代表获得、复制或耗尽。永久获得与战斗重建继续使用[永久牌组约定](../permanent-deck-smith/spec.md)；直接 `create Card` 不替代这些操作。

## 费用、出牌与历史

| 入口 | 参数与返回 | 语义与边界 |
| --- | --- | --- |
| `GetCardEnergyCost` | `owner, card` → Int，pure | 返回实际应付能量。X 首先取当前 Energy，不受普通免费效果影响；普通费用再处理 Corruption／FreeAttack 与实例费用。Card.Cost 是实例局部费用，不等于含 Power 修正的应付费用 |
| `ResolveEnergyXValue` | `owner: Ref(Player), card: Ref(Card)` → Int，pure | 效果 X 入口：优先读取匹配当前 CardPlayRecord.RawX，兼容入口回退 Card.PaidX，再经 ModifyXValue 统一监听修正，不写回捕获值或扣费。ChemicalX 每个有效持有实例+2；Whirlwind／Cascade 在每次效果开始取一次值，重复仍用原捕获值，Cascade 升级随后额外+1。前置为能量 X 牌；非 X 查询返回0、不派发修正。星星 X 未接入 |
| `GetMaxEnergy` | `owner` → Int，pure | Player.MaxEnergy 基值加 Pyre 修正；重置能量与调用方共用此查询，获得 Pyre 不立即加能量 |
| `PlayCard` | `owner, card` | 手牌中的非选敌牌；验证战斗／回合、持有与能量后付款一次 |
| `PlayTargetedCard` | `owner, card` | 先验证可支付性，再发布存活敌人必须单选决策；答复后付款。对应公开 action 同名 |
| `PlayAttackCard` | `owner, card` | 兼容旧开发脚本，默认首个存活敌人；新客户端使用显式目标入口。也服务有选敌目标的 Skill |
| `AutoPlayCard` | `owner, card, forceExhaust` | 从该玩家手牌、抽牌、弃牌或耗尽堆免费打出；不重新抽牌、不支付能量，X 捕获当前能量。保留父出牌上下文；没有存活敌人或玩家已死亡时不开始 |
| `AttacksPlayedThisTurn` | `owner` → Int，pure | 每次开始的攻击分别计数，包括同实例重复和嵌套出牌；Stomp 初始化另用 finished 历史 |
| `CardBaseDamage` | `owner, card, target` → Int，pure | 计算普通／升级基础伤害、战斗成长、耗尽数量、Strike 标签数、目标易伤层数或当前格挡。target 可为 none；攻击者／目标修正由伤害链再处理 |

共享结算过程 `ResolveCardEffects` 为每次真实重复创建 CardPlayRecord，保存并恢复 CurrentPlay／CurrentCard。顺序为 BeforeCardPlayed／BeforeCardExecution → 唯一正文 → 附魔／负面附着 → finished 历史 → AfterCardExecution／AfterCardPlayed；全部重复后才移到结果牌区，最后检查胜利。这个顺序依据源 `CardModel.OnPlayWrapper`，不把 AfterCardPlayed 放在自身耗尽之后。

CardPlays／CardLibrary 和内部引用不作为完整公开历史或牌区发布。公开快照提供卡类型、标签、基础／临时费用、PaidX、成长、Power 层数与实际合法候选；目标／选牌继续通过 `net:action:net.answer_decision` 应答，无须新 wire 业务命令。

## 资源、能力与时间点

`combat/` 拥有 HP、格挡、能量、Power 与战斗状态。`IncreaseMaxHp(owner, amount)` 同时增加上限并按同量治疗，Feed、Strawberry 和 FruitJuice 复用；`GainEnergy` 尊重 NoEnergyGain，`DrawOne` 尊重 NoDraw／容量，`RecordPlayerHpLoss` 对每次正的未阻挡损伤分别记录并通知。

`GainBlock` 处理 Dexterity／Frail／Unmovable，`GainUnpoweredBlock` 绕过这些修正；正的格挡事件供 Juggernaut 消费。攻击通过独立Attack和DamageResult归集结果；现有伤害修正使用四位整数中间量至最终取整，尚不能宣称与原版decimal完全等价。兼容`DamageEnemyPowered`／`DamagePlayerPowered`共用此链，前者接 Strength／Weak／Vulnerable／Cruelty，后者接敌方修正和 Colossus。HpLossCount 是事件次数，不是失血总量。

`ApplyPlayerPowerFromCard`／`ApplyEnemyPowerFromCard`显式传来源与play，普通非卡来源仍可调用不带来源的入口；定义决定实例匹配策略。`powers/shared`所属模块按定义标识消费通知／修正，准确生命周期见[Power参考](../../../development/base-spire/reference/powers.md)；不再由调用方kind选择行为。已有消费者覆盖耗尽抽牌／格挡、受损 Strength／群伤、每回合 Strength／自伤／格挡、第三次攻击复制、下一攻击重复、抽到 Strike 自动打出、回合末自动打出、临时 Strength 恢复和费用／格挡／伤害修正。Rupture 的牌源自伤 Strength 延后到该次出牌结束；敌方阶段不触发 Rupture／Inferno。Power 通用目录仍保留 260 项来源，不能把这些角色处理器视为全 Power 目录已迁移。

随机选择使用 `vanilla-uniform@1` 与源布局：`base-spire:run:combat_card_selection`、`base-spire:run:combat_targets`、`base-spire:run:combat_card_generation`；初始洗牌、弃牌重洗与 Stampede 共用 `base-spire:run:shuffle`。空资格不抽样，连续命中重建存活候选。初始洗牌直接 Fisher-Yates，重洗先按源条目与升级排序；精确布局和整局前置消费边界见[随机约定](../run-domain/randomness.md)。

显式 `Ironclad.Initialize` 已接入源基础初态、开发流初始洗牌、自动回合抽牌和首回合 Innate 排序，准确入口见[角色初态约定](../character-combat/ironclad-start.md)。本页不承诺含 Neow 的正常角色开局、解锁进度、多人、无限生命目标、完整附着种类／其他未迁移 Power、正常 Run 或原版逐步一致；继承开发范围的资源字段上限。需要这些行为时继续扩展对应约定，不在内容里绕过共享接口。
