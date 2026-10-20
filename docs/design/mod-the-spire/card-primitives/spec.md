# 卡牌原语组合契约

归属[行为要求](../../environment-completion-and-differential-v1.md#env-t06)，本次以现有已支持卡牌为范围。普通卡牌的normal/upgraded与hooks组合可复用原语；原语编译到同一Engine IR，不增加按卡ID执行的旁路。诊断.author只在输入端翻译为相同类型化程序。

| 能力 | 契约 |
| --- | --- |
| 数值效果与控制流 | 伤害、格挡、能量、抽牌、生命和Power沿现有共享过程；顺序、活值与let快照保持，循环/条件组合不得复读已捕获数值 |
| 选择牌并执行子程序 | 选择只负责来源、资格、数量、提示和Engine暂停/恢复；选中实例绑定到onSelected子程序，可顺序升级/复制/移动；不把选牌与固定效果写成一组专用卡命令 |
| 随机选择 | 同一命名卡流，空域无抽取；普通随机手牌选择保留按实例排序，选中后的效果用相同当前牌绑定；特殊源预览流程不得改变候选顺序 |
| 当前牌操作 | 升级复用CardDefinitions，移动复用CardPileProcedures；手动弃牌、入手上限、置顶和消耗回调保留。当前牌只能在选择或forEachCard绑定内使用 |
| 致死后的增加上限 | 组合目标存活条件与普通GainMaxHp，不另设只属于Feed的执行原语；源先攻击后判该目标死亡，不能依据整场终局推断 |
| hooks与链接 | 子程序递归验证变量、ID、词法绑定和hook限制；禁止在hook递归开启玩家选择；未知字段/未绑定当前牌带源位置明确拒绝 |
| 特殊机制 | Thrash的所选攻击牌源伤害预览/成长、自动打出自身等保留明确的领域原语；仅在现有共享语义不足以正确表达时使用，不为每张牌添加执行分支 |

原先chooseCard的from/to写法、cloneCard、upgradeCards和randomMoveCard短写继续接受，直接展开为相同组合；完整onSelected可写多条效果。简单短写不需要在内容作者代码中重写复杂流程。破坏性变化限于base编译期类型与API，core语法/IR保持；实际产物身份变化由产物生成并迁移受影响基线。

战斗牌区选择沿源CardSelectCmd的结束守卫：最后一个敌人已离开存活集合时，不开启选择，也不执行其onSelected子程序；同一张牌的其他末杀余效仍继续。该守卫只接牌区选择，不接生成候选选择。`autoSelect=true`且候选不多于请求数量时，按原牌区顺序自动执行；Headbutt普通/升级均声明此行为，单张弃牌自动置顶，多张仍暂停选择。

验收使用已有Headbutt/Armaments/Anger/TrueGrit/Feed及选牌组合用例，新增只验证“选择后多个效果、嵌套作用域/链接、恢复后继续顺序”等旧用例不能区分的组合。完整包最终一次共同构建、Engine/Slice/net、现有严格17与正常公开前缀；受控检查不作为整局完整性证据。

## 生成候选并选择（ENV-T06-PR05）

`generateCards` 的 `choose=true` 表示生成候选后选择一张，`allowCancel=true` 允许空选择；须同时声明 `distinct=true` 和正整数字面量 `count`。按完整池的源顺序过滤单人资格、战斗可生成性、稀有度和类型，使用CombatCardGeneration流对整个合法池洗牌一次（N-1次抽取），再取至多count个不同定义创建候选。候选存放在独立公开集合，不进入手牌或触发入战钩子；隐藏的池索引和未选整池顺序不得公开。

Engine以EntityRefList决策暂停，数量0..1或1..1；续接不再抽数。选择后保留所选真实实例，移入手牌，满手则进入弃牌堆，再沿共享入战钩子结算；取消或未选实例全部销毁，候选集合清空。`choose=true,cost=0` 的临时费用持续到本回合结束，不在首次打出后清除。AttackPotion只组合此能力，不添加按药水ID执行的旁路。

验收区分完整池洗牌与仅抽三次、取消与自动选首项、满手落点、未选实例泄漏、首次出牌后费用及回合结束清理；参考独立通过公开API执行并核对候选与随机计数。

## 卡牌附魔首批：Sown

卡实例以公开附魔身份、整数层数、Disabled标记承载Sown，不借用Power或隐藏C#状态。新卡无附魔；复制沿源保留附魔及当前状态但拥有独立字段，永久牌复制到战斗时保留永久实例的Normal状态。升级不清除附魔，变换为新牌不继承旧附魔。不能以“每次复制都重置”掩盖状态寿命。

CanEnchant沿源EnchantmentModel：排除Status／Curse／Quest、Deck中的Unplayable及已附魔实例；Sown不可叠加。SapphireSeed.Plant使用真实永久候选单选，空域关闭、唯一候选自动执行、多候选暂停。Eat先回复9，再沿既有永久升级选择；两选项均不消费随机数。

Sown在每次牌本体执行完成且玩家存活后、CardPlayFinished／AfterCardPlayed之前触发：Normal时先GainEnergy(Amount)，再置Disabled；禁止能量获得时仍消耗本次状态。最后击杀仍执行附魔，玩家先死亡则不触发。同一战斗副本再次打出不再获能，永久牌不被副本状态回写，下场从永久牌生成的新副本再次Normal。源为Models/Enchantments/Sown.cs、CardModel.DeepCloneFields／Play及EnchantmentModel.CanEnchant。

参考的Plant自动首选，不能替代源的玩家单选；参考Sown通用MutableClone重置状态，但CreateClone会经CloneForCombat恢复原状态，后一路径可实际对照。两种复制路径须分别记录，不把通用复制偏离扩大到所有参考复制。当前支持Sown，不据此宣称Afflict、动态关键词或其他附魔已支持。

## 负层数无限持续的伤害倍率

能力可声明 `negativeStacksAreInfinite = true`：非零负层数代表无限持续，固定伤害倍率仍生效，回合末不衰减或移除；正层数继续按原声明衰减，零层数不生效。该属性进入共享作者模型及编译后的IR，不修改通用Engine或按怪物ID判断。Shrink使用此声明，对powered攻击固定乘0.7；Strength仍按有符号层数加减，Weak/Vulnerable未声明该属性时继续只接受正持续层数。来源为ShrinkPower.IsInfinite、ModifyDamageMultiplicative和AfterSideTurnEnd。

`rules = { hpLossCap = n }`在格挡吸收后封顶本次生命损失，有未格挡伤害才耗一层，减到零立即销毁实例；完全格挡不耗层。SlipperyPower的n=1，连击逐击重新读取是否仍持有能力，不能将首次封顶延续到后续命中。
