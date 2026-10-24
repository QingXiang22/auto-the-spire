# Power定义与生命周期参考

文档类型：参考（Reference）。适用于Core.TheSpire 0.9.4与base 0.7.0-dsl.72的纯DSL内容模型。编写步骤见[新增内容模块](../how-to/add-content-module.md)，现有内容修改见[修改单个内容](../how-to/edit-content.md)。以下接口均来自正式DSL，不是完整原版PowerModel API。

## 定义与共有状态

[PowerDefinition](../../../../mod-the-spire/base-spire/powers/rules/definitions.spire)模板参数如下。

| 参数 | 类型／默认 | 行为 |
| --- | --- | --- |
| definition | String(64)，必填 | 精确查找标识；同次启动重复登记失败 |
| allowNegative | Bool，false | 已有实例改量后，0总是移除，负数仅在false时移除。锁定参考的新建非零负值仍先附着，不在新建阶段应用此移除判定 |
| powerType | String(8)，buff | buff／debuff／none；玩家新建debuff设置SkipTick。决定Power本身类型，不代表当前增量一定为减益 |
| stackType | String(8)，counter | counter／single／none；用于按符号判断增量类型，不把single的再施加增量强制归0 |
| instanceMode | String(16)，shared | shared按定义与持有者合并；per-applier再按施加者引用匹配；independent每次施加独立实例；其他值在登记时失败 |

根启动过程SetupPowerDefinitions发布RegisterPowerDefinitions并建立隐藏PowerLibrary。自身监听、寿命与私有状态由所属模块定义。定义中没有通用hook回调参数；使用普通DSL过程和实例订阅。

[Power实体](../../../../mod-the-spire/base-spire/combat/schema.spire)保留DefinitionId、隐藏Definition引用、Owner、可选HolderPlayer／HolderEnemy、Stacks（-999..999）、SkipTick和ExpiresAtTurnEnd，并有隐藏ApplierPlayer／ApplierEnemy、SourceCard／SourcePlay。每个实例恰好一个持有者，至多一个施加者；Kind及专属Counter／SelfDamage槽已移除。共享合并不覆盖首次创建的来源引用。

ExpiresAtTurnEnd是一次施加的临时寿命事实，保留临时FlameBarrier与永久Thorns的区别。其他专属寿命由模块订阅通知处理。

## 领域操作

### 新建、叠加与直接改量的阶段

共享流程以锁定参考的PowerCmd.ApplyNew／ModifyAmount为依据。战斗胜负已定但父效果仍在收尾时，施加及改量在任何通知前返回；RemovePower仍允许执行。已死亡／逃离的敌人不能改量，普通开发场景中的空靶场不视为自然胜利。

每次计算创建独立且隐藏的PowerAmountChange，保存定义、目标、已有Power、原始与实际增量、是否新建及施加者／card／play。先发布BeforePowerAmountChanged，再依次折叠ModifyPowerGivenAdditive与ModifyPowerGivenMultiplicative（有施加者时）；PowerAmountReceiving中的实例包装过程调用pure判定／计算，只更新本次上下文并记录参与者。计算阶段不扣层、不发玩法副作用；Try判定成立而数值未变也记录。AfterModifyingPowerAmountReceived仅让本次记录且仍有效的实例执行副作用，嵌套操作使用不同上下文，结束后清理计算记录。

- 新建原始0量直接返回，不发布通知。其余新建在修正后调用BeforePowerCreated（对应源BeforeApplied，包括修正为0），非零才加入Powers并发布PowerCreated；随后通知修正者，非零才发布AfterPowerApplied和PowerAmountChanged。
- 叠加先按实例模式找到已有Power，再进入与直接Modify相同的一次流程，不预先执行一遍修正。已有Power的0量也经过Before、Given／Received和修正者通知；仅非零实际增量发布PowerAmountChanged，然后按AllowNegative与最终层数判定是否完整移除。
- PowerApplied表示Apply命令完成且实例仍在集合，保留created与实际增量；不是直接Modify通知。AfterPowerApplied只表示新建实例的源AfterApplied。单层显示类型不将再施加增量强制归0，沿锁定参考的ModifyAmount计算。
- 定义类型buff／debuff／none与层数显示counter／single／none独立于实例模式；counter且AllowNegative的负增量视为debuff，不允许负层的debuff在负增量时视为buff。Artifact按当前增量类型拦截，并在修正者通知中消费自身一次。

FlexPotion、SetupStrike与Mangle的初次力量变化由临时Power的BeforePowerCreated处理，后续由PowerAmountChanged同步；卡牌只施加临时Power。这样Artifact阻止Mangle时不会先改Strength，修正链也不会执行两次。三个临时Power在持有者侧回合结束先移除自身，再撤回保存层数。

以下过程位于[powers/rules/lifecycle.spire](../../../../mod-the-spire/base-spire/powers/rules/lifecycle.spire)。

| 过程 | 参数／返回 | 行为 |
| --- | --- | --- |
| ApplyPlayerPower | owner: Ref(Player)、definition: String(64)、stacks: Int(-999..999)；无返回 | 委托ApplyPower，对玩家施加 |
| ApplyEnemyPower | target: Ref(Enemy)、definition、stacks；无返回 | 委托ApplyPower，对敌人施加 |
| ApplyPlayerPowerUntilTurnEnd | owner、definition、stacks；无返回 | 施加时设置ExpiresAtTurnEnd，在AfterEnemySideTurnEnd移除；保留敌方阶段的效果 |
| ApplyPower | player: Binding、enemy: Binding、definition: String(64)、stacks: Int(-999..999)、untilTurnEnd: Bool；返回Binding | 先查定义并验证恰好一个持有者，再按定义与持有者匹配实例；返回仍有效的Power，否则none |
| ApplyPowerWithSource | 上述参数加applierPlayer／applierEnemy／card／play: Binding；返回Binding | ending时在通知前返回none；验证来源与持有者，排除死亡／逃离敌人，经ShouldApplyPower并按instanceMode匹配后，进入一次共享改量流程。新建原始0量或修正后0量返回none |
| ApplyPlayerPowerFromCard／ApplyEnemyPowerFromCard | owner、card、play、definition、stacks；敌方另加target | 卡牌效果显式传执行来源与玩家施加者；不通过CurrentPlay猜来源。临时版本ApplyPlayerPowerFromCardUntilTurnEnd同样保留来源 |
| ModifyPowerAmount | power: Ref(Power)、delta: Int；无返回 | 委托ModifyPowerAmountWithSource，来源参数为none |
| ModifyPowerAmountWithSource | power、delta，加applierPlayer／applierEnemy／card／play: Binding；无返回 | 通过相同ending／持有者检查，再执行Before、Given／Received、提交、修正者After与PowerAmountChanged；最后将0或不允许的负数通过RemovePower移除。字段范围仍为-999..999 |
| RemovePower | power: Ref(Power)；无返回 | 有效实例在Powers中时先出集合、发PowerRemoved、再销毁；不直接destroy绕过清理 |
| RemoveEnemyPowersOnDeath | target: Ref(Enemy)；无返回 | 普通敌人的死亡通知完成后，标记DeathResolved并按Powers顺序调用RemovePower；重复调用无额外效果，存活目标失败。仅覆盖当前Power的源默认移除策略，保留／否决消费者尚未接入，见[死亡约定](../../../design/mod-the-spire/combat-end/spec.md) |
| RemovePowersOnDeath | player／enemy: Binding，恰好一个持有者；无返回 | 要求持有者生命为0；死亡通知完成后快照其Powers，逐个复核仍在集合中并调用RemovePower。玩家失败和敌人死亡共用此操作；只覆盖已加载Power的两项源默认移除资格，不移除其他持有者 |
| ConsumePower | power: Ref(Power)；无返回 | 调用ModifyPowerAmount(-1) |
| TickPowerDuration | power: Ref(Power)；无返回 | SkipTick为true时只清标志，否则ConsumePower；不会自行安排调用时点 |
| ClearCombatPowers | 无参数／返回 | 遍历Powers并调用RemovePower，复用同一私有清理路径 |

所有施加调用均不接受kind。通过ending检查后，未知definition在创建或发送应用事件前失败。新建原始0量直接返回；已有实例把stacks作为本次增量进入共享流程。先前的debuff／singleStack参数、ModifyPowerGiven／ModifyPowerReceived／ModifyPowerStacks及PlayerPowerApplied／EnemyPowerApplied已移除，当前消费者同步使用本页接口。

## 通知与顺序

| 事件 | 载荷 | 发布时点 |
| --- | --- | --- |
| RegisterPowerDefinitions | 无 | SetupPowerDefinitions时；定义模板登记各自Blueprint |
| BeforePowerAmountChanged | change: Ref(PowerAmountChange) | 所有实际进入计算的新建／叠加／直接Modify，在Given／Received之前；Requested为原始增量 |
| ModifyPowerGivenAdditive／ModifyPowerGivenMultiplicative | value: Int、player／enemy: Binding、definition: String(64)、applierPlayer／applierEnemy／card／play: Binding | 有施加者时的两次pure折叠，先全部加法，再全部乘法；监听返回当前新值 |
| PowerAmountReceiving | change: Ref(PowerAmountChange) | Given之后；实例包装调用pure并经RecordPowerAmountModifier(change,power,amount)提交本次计算值与参与者 |
| BeforePowerCreated | player／enemy: Binding、definition: String(64)、amount: Int、applierPlayer／applierEnemy／card／play: Binding | 新建修正后、首次实例加入Powers前，对应源BeforeApplied；amount为0也发布 |
| PowerCreated | power: Ref(Power) | 新实例已加入Powers，基础字段与玩家SkipTick已设置；用于建立私有状态 |
| AfterModifyingPowerAmountReceived | change: Ref(PowerAmountChange) | 提交新建／改量后；只让记录且仍有效的接收修正者产生副作用，修正为0也执行 |
| AfterPowerApplied | power: Ref(Power)、amount: Int、created: Bool | 仅新建成功，在修正者通知之后、PowerAmountChanged之前，对应源AfterApplied；created固定为true |
| PowerAmountChanged | power: Ref(Power)、delta: Int、applierPlayer／applierEnemy／card／play: Binding | 新建／已有实例的非零实际增量，在修正者通知之后、已有实例零／负移除判定之前；临时Power按此同步，Vicious核对真实施加者 |
| PowerApplied | power: Ref(Power)、amount: Int、created: Bool | Apply命令完成、实例仍有效时；amount保存本次实际增量，不从嵌套反应后的最终层数反推。直接Modify不发布 |
| PowerRemoved | power: Ref(Power) | 已离开Powers，销毁之前；可按载荷清理所属私有entity |

层数变化导致移除时，不再发布PowerApplied。重新施加已有玩家实例不会重新设置SkipTick。PowerAmountChange及PowerAmountModifier不进入公开投影；完成后销毁，暂停期间由Session保存。移除参与者时可选引用清空，后续回调重新检查有效性。

Weak／Vulnerable／Frail在自己的模块监听AfterEnemySideTurnEnd并调用TickPowerDuration。Rage等短期Power自行订阅AfterPlayerSideTurnEnd。抽牌、格挡和耗尽按各自命令拒绝ending；已进入的耗尽快照仍继续，不统一取消其他余效。AfterBlockCleared在每次玩家回合开始的格挡处理阶段发布，包括首回合以及格挡被保留时；它不是“Block值发生变化”的通知。

三个临时力量Power共享TemporaryStrengthPowerContent，参数definition／sign／powerType。FlexPotion与SetupStrike的sign为1，Mangle为-1。首次BeforePowerCreated已处理初始量，因此源AfterPowerAmountChanged在delta等于当前Stacks时不重复处理；其余改量按实际delta同步。它们不使用ExpiresAtTurnEnd：直接移除与跨战清理不会再次撤回Strength，零层变化已在移除前同步。

显式EndDevelopmentCombat和下次BeginDevelopmentCombat调用ClearCombatPowers。玩家失败先提交defeat状态，发布PlayerDied(owner)，再通过RemovePowersOnDeath清理该玩家的Power及模块私有状态，随后发布CombatEnded并提交失败访问；存活敌人及其他玩家不受清理影响。正常胜利在CombatEnded后清理所有能力、格挡及战斗牌，再通知早期／普通胜利效果并生成奖励。精确阶段见[战斗操作参考](operations.md)。

## 私有状态与未支持范围

私有entity声明在所属模板内，字段设为Hidden，通过实际Power引用关联。例如JugglingPower.State保存计数，RupturePower.Pending关联实际CardPlayRecord；模块负责创建、重置与移除清理。Engine保存这些实体；它们不是C#隐藏状态，也不应公开为玩法观察。

本接口未提供完整Creature模型、任意Power的私有状态复制或全部源hook。接收修正者当前以Power实例记录，遗物接收修正消费者仍需明确其记录与资格；Given仍使用当前Int范围，未迁移的比例型内容不因此获得支持。Rupture沿已有开发上下文。不能从一个已登记定义推导全量来源资格或独立原版等价性。
