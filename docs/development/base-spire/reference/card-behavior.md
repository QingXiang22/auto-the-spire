# 卡牌行为、攻击与实例操作参考

文档类型：参考（Reference）。适用于 base `0.7.0-dsl.40` 的单玩家开发 scope。来源顺序、状态归属和限制见[迁移约定](../../../design/mod-the-spire/content-behavior-migration.md)；通用语法见[封闭键分派](../../../dsl/core/0.3/authoring-reference.md#封闭键分派)。这些操作不开放正常 Run scope。

## 定义正文与纯值

每张可执行牌在自己的模块保留 CardDefinition，并绑定两张分派表。处理签名必须完全匹配：

| 表／处理参数 | 用途 |
| --- | --- |
| `ExecuteCardBody(owner: Ref(Player), card: Ref(Card), target: Binding, play: Ref(CardPlayRecord))` | 普通过程；正文顺序调用领域命令，target 可以 none |
| `ReadCardDamage(owner: Ref(Player), card: Ref(Card), target: Binding) → Int` | pure；普通、升级与动态基础值由所属模块维护；`CardBaseDamage` 委托本表 |

绑定使用 `dispatchCase Name { dispatch = ExecuteCardBody key = "完整定义ID" procedure = Body }`。值提供者同样绑定 ReadCardDamage。缺绑定在启动登记时失败，重复键在编译时失败；`CardPlayed`／`CardPlayedOnTarget` 不再执行正文。

每次执行的 BeforeCardExecution／AfterCardExecution 载荷均为 owner、card、play。正文之后依次经过 ExecuteCardEnchantment 和 ExecuteCardAffliction，存活时才继续完成通知。CardPlayRecord 保存付款、原始 X、自动标记、目标、结果区、ActionNumber、ParentPlay 和 PlayIndex／PlayCount；内部执行历史不公开。新效果收到 play 后继续显式传给子操作。

## 攻击与结果

下列声明位于 [combat/attacks.spire](../../../../mod-the-spire/base-spire/combat/attacks.spire) 和 [damage-results.spire](../../../../mod-the-spire/base-spire/combat/damage-results.spire)。

| 操作 | 参数／返回与边界 |
| --- | --- |
| `CreateCardAttack` | owner: Ref(Player)、card: Ref(Card)、play／target: Binding、amount／hits: Int、mode: String(16)、dynamic: Bool → Binding；mode 只接受 single／all／random |
| `ExecuteAttack` | attack: Ref(Attack)；一对 BeforeAttack／AfterAttack，ModifyAttackHits 修正段数；逐击刷新存活目标，本批全部提交后才通知结果及死亡 |
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
| `LocalCardCost` | card → Int，pure；从 BaseCost 折叠独立修正，夹至0..9；RefreshCardCost 更新公开 Cost 缓存。实际费用仍经 GetCardEnergyCost 的 Power 修正 |
| `ExpireCardCostModifiers` | card、boundary: String(16)；共享出牌／回合流程负责到期。新战开始清除所有临时费用，克隆独立复制现有修正 |
| `MoveCombatCard` | owner、card、to: String(16)、reason: String(32)；to 为 hand／draw-top／draw／discard／play／removed。拒绝永久牌及外来拥有者；满手转弃牌，通知 CardMoved(owner,card,from,to,reason) |
| `DiscardCombatCard` | owner、card；只接受手牌，移动后发 CardDiscarded。回合末 flush 和正常打出结果区只发移动通知，不冒充主动弃牌 |
| `ExhaustCombatCard` | owner、card；移动后依次发 CardMoved、CardExhausted；拒绝永久牌、已耗尽和离场牌 |
| `CardHasKeyword` | card、keyword: String(16) → Bool，pure；innate／exhaust 的基础事实加 ModifyCardKeyword 修正，retain／ethereal／unplayable／eternal 可由内容提供 |
| `CardPlayAllowed` | owner、card、auto: Bool → Bool，pure；共享已定义／拥有者／Unplayable／ShouldPlayCard 门禁；手动另验证手牌、回合、能量和目标 |
| `FinishTurnHand` | owner；固定 Ethereal 批次，处理消耗，再 BeforeHandFlush、留手筛选、整批移动及 AfterHandFlush。新抽入牌不追加到旧 Ethereal 批次 |
| `AutoPlayFromDrawPile` | owner、play: Binding、count: Int、forceExhaust: Bool；先预取并移出整个批次，再逐张共用自动出牌流程；嵌套批次独立 |

上述牌区仍为单玩家全局集合。带独有回合末手牌效果、完整多选／取消策略和所有变换政策未由本表一并承诺。

## 复制、附魔与永久牌

`CardCloned(owner,source,card)` 复制战斗实例；`CardCombatCopied(owner,source,card)` 从永久实例建立战斗副本；`CardRemoving(card)` 在销毁前让所属能力清理。共享 Card 仍复制共有事实，私有状态由所属模块消费生命周期通知，不再扩专属 Bool 或两份复制字段清单。

`EnchantCard(owner,card,definition: String(64),amount: Int(1..999))` 先执行所属纯资格，再派到 ApplyCardEnchantment。基础 `CanEnchantCard` 拒绝状态／诅咒／任务牌、已有附魔及牌组中的 Unplayable。CardAttachment 拥有 Card、DefinitionId、Kind、Amount、Status；Sown 的执行阶段、禁用状态由自己的模块处理。升级不重建附魔，战斗克隆复制当前状态，永久副本不反写来源。依附投影只在关联卡可识别时发布，不另公开附魔集合计数。

`SelectPermanentCard(owner,purpose: String(16),enchantment: String(64),cancelable: Bool) → Binding` 复用原生选择；purpose 为 upgrade／remove／enchant。空域返回 none；不可取消且单候选时自动选择，其余按可取消性发布 EntityRefList。`RemovePermanentCard(owner,card)` 只在战斗外移除可移除永久牌，发通知并清理附属状态。

`HealWithSource(owner,amount,source)` 和 `GainGoldWithSource(owner,amount,source)` 提供来源及实际变化通知；GainGold 先经过 ModifyGoldGained／AfterModifyingGoldGained，再提交正收益。`LoseGold(owner,amount,reason,source)` 夹至实际余额，reason 为 spent／lost／stolen；需要余额足额的购买使用 `SpendGold(owner,amount,source)`。

SapphireSeed 与 DoorsOfLightAndDark 通过 `EnterDevelopmentSapphireSeed`／`EnterDevelopmentDoorsOfLightAndDark` 进入开发访问。叶模块只定义选项与效果，共享 EventVisit 保存页面、状态、返回位置与访问序号；`CloseDevelopmentEvent(visit)` 关闭。它们不是正常事件池、子战斗恢复或地图 Run 入口。
