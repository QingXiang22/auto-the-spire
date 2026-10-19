# 内容模型迁移与实际开发验证

2026-10-19，CORE-AUTHOR-05／DSL-CONTENT-MODEL-01。用户授权按sts2-sim等实现重新组织内容，并实际开发验证成本。本包基于`a4ac0d5`，依赖PR #23；当前事项只在[主清单](../../tofix/README.md#当前交付)维护。

## 结果

base升级至`0.6.1-dsl.39`，Core.TheSpire升级至`0.6.1`。已使用的32个Power迁入所属模块，新增SelfFormingClay及其Power后共有33个Power定义。25个只供一张卡使用的效果模板移回该卡文件。普通单体／群体攻击不再重复声明damage与upgradedDamage，改读CardBaseDamage。旧规则身份与快照不能冒名复用；语言仍是core/0.3，执行语义仍为engine/ir@0.6。

实际新增完成，但首轮并非零共享修改：它暴露了既有AfterBlockCleared时序错误。修正后，在隔离的迁移基线上重新接入，只需2个玩法文件和5个登记文件；共享命令、schema、伤害、回合及其他内容均无需再改。修改奖励数值的实验只改1个玩法文件，实际结果随之变化。这里证明的是修改范围减少，没有净工时或token成本对照，不能声称开发速度提高了某个倍数。

## 迁移后的职责

| 位置 | 职责与本次改变 |
| --- | --- |
| powers/shared的所属文件 | 定义标志、修正／通知／到期订阅，以及该Power私有实体。Strength公式、Weak衰减、Rupture待结算、Juggling计数各在所属模块 |
| powers/rules/definitions.spire | 显式登记PowerBlueprint，重复登记拒绝；不根据游戏ID选择行为 |
| powers/rules/lifecycle.spire | 按定义查找、施加／合并、改量、移除与清理；调用方只传目标、definition和stacks，kind被拒绝。未知定义和非法持有者在创建前失败 |
| combat/schema.spire的Power | 保留定义引用、身份、持有者、层数和共有的应用寿命事实；删除Kind、Counter、SelfDamage、ExpiresBeforeEnemy专属槽 |
| RupturePower.Pending | 按实际Power与CardPlayRecord引用关联待结算收益；移除时清理。公共CardPlayRecord.RuptureGain与卡牌专用收尾已删除 |
| 卡牌所属文件 | 25个独有效果模板回到单张卡文件；共用操作／效果仍在cards/rules。普通攻击从Card.BaseDamage与升级逻辑读取同一值 |
| 通用DslPackage | catalog definition接受完整DSL声明模块，顶层属性仅允许dsl_version。简单use文件兼容；根路径、显式引用、未引用文件、静态JSON和编译检查继续执行 |

PowerLibrary是隐藏的定义集合，public元数据明确登记不发布；实际Power的身份／层数仍通过原投影公开。Host与Engine没有新增按遗物、卡牌或Power ID执行效果的分支。三个旧运行ID与源目录不一致的Power统一使用源目录既有ID：Barricade、DemonForm、NoEnergyGain；源分母和源hash未改。

## 实际新增SelfFormingClay

来源为`sts2/src/Core/Models/Relics/SelfFormingClay.cs`和`Powers/SelfFormingClayPower.cs`，原始字节SHA256已与两个catalog核对。只读参考为sts2-sim `2089659bd07920088b93226136c72b6b6ee590ad`，参考未修改。逐项摘要与文件差异见[开发实验数据](content-model-development-trial.json)。

实现方式：遗物监听战斗中的正生命损失，每次施加3层所属Power；Power监听AfterBlockCleared，以当前层数获得无加成格挡，再自行移除。累计、移除和时序都通过已有领域操作完成。该scope的PlayerHpLost统一正未阻挡伤害和不可阻挡失血，不具备完整DamageResult／来源载荷。

首轮保存文件摘要后开发，随后源码复核发现：原版CombatManager在Creature.AfterTurnStart后无条件发布AfterBlockCleared，包括首回合与保留格挡；base错误地把通知放在“实际清空格挡”的条件内。sts2-sim的CombatEngine与ToricToughnessTests也明确保留这点。本次移出条件块，补首回合／Barricade判别，并修正此前文档。没有在新Power内伪造另一回合监听来绕过缺口。

首轮完整差异是11个生产文件：7个新增／登记文件、combat/lifecycle.spire的时序修正，以及3个迁移收尾的注释／尾部空行调整。public/metadata.json还补齐迁移遗漏的PowerLibrary不发布用途。这些额外工作没有从首轮记录抹去。修正后的迁移基线在隔离目录中去掉新内容，再按以下7个文件重新接入；重建后的包摘要与最终生产包完全相同，7项CLAY检查再次通过。

| 类型 | 文件 |
| --- | --- |
| 2个玩法模块 | relics/shared/self-forming-clay.spire；powers/shared/self-forming-clay.spire |
| 2个装配入口 | relics/index.spire；powers/index.spire |
| 2个来源登记 | relics/catalog.json；powers/catalog.json，原有行从null改为定义路径，保留身份／池／hash |
| 1个公开登记 | public/metadata.json，为Relic与Power分别登记公开ID |

新增行为见证位于tools/base-spire-dev/SelfFormingClayChecks.cs，并接入已有定向／完整检查。它们是包外验证代码；不能把“生产只改7个文件”理解成无需写验证或登记文档。硬编码的55件当前支持遗物总名单已移除，完整检查在实际执行后比较catalog定义与真实见证；冻结297件来源分母继续保留。

另一个隔离副本只把遗物的`stacks = 3`改为4，实际Power层数与下一回合格挡均为4，其他生产文件未改。Strike实验只把baseDamage从6改为12，普通实际伤害为12，升级后仍按唯一升级值为9，不再出现字段12而执行6。

## 验证

scope=`base-spire-dsl-relic-draw-dev-v1`，默认行为seed=`20261018`；公开路径保留其既有固定seed。迁移前基线为395项，迁移后、真实新增前为400项；最终结果如下。

| 实际命令／入口 | 结果 |
| --- | --- |
| check --ironclad-only | 139项通过，包括嵌套Rupture、重复效果及私有计数 |
| check --content-model-only | 5项通过：定义身份／负值／零层／持有者，未知／非法输入，私有清理，新模块自有行为，卡牌数值单一来源 |
| check --relic-content-only --filter CLAY | 7项通过，跳过87项；一次本地记录5.476秒，不含构建与入口预加载；隔离重新接入再次7项通过 |
| check --report | 407项通过；遗物56件真实见证，源297件、1件排除、240件待实现；卡牌87件见证，Ironclad单人85/85 |
| Core.TheSpire.Engine.Tests | 118项通过 |
| Net.TheSpire.Tests --base-dsl-only | 41项通过，包括真实stdio中的新Power身份／层数、延迟格挡及移除 |
| check_relic_coverage.py、check_card_coverage.py --require-ironclad-complete | 均返回0；不等于全遗物或独立原版比较通过 |

本地日志在`.artifacts/content-model-migration-20261019/`，不提交。开发期间公开组曾因PowerLibrary缺登记失败；另一次因检查运行期间修改包文本导致身份不匹配。前者已补元数据，后者在固定生产树后重跑，最终41/41。CLAY初版保留格挡断言反映了旧base行为，源码复核后已按源更正；不将初版通过当作源正确性证明。

## 后续开发方法与边界

开发者先从catalog定位所属文件和一个准确领域操作，再修改独有定义／监听；只在缺少真实共享语义时扩共享规则。调试只运行相应前缀或内容模型组，批次收尾产生一次完整证据并交CI；纯数值修改不再重复写项目级设计。指南入口为[Power与物品](../../../development/base-spire/how-to/powers-relics-potions.md)和[内容扩展](../../../development/base-spire/how-to/extend-content.md)。

仍有5处显式登记，单个新内容的检查接入也有成本；本次没有引入目录扫描或生成第二个注册事实。尚未迁移内容、新共享阶段和正常来源仍需领域工作，不能预期所有新效果都只改一个文件。

本次没有补齐完整Creature模型、所有Power实例／叠加策略、施加来源／伤害结果上下文、Vigor攻击命令边界、附着私有状态复制协议或CardBaseDamage现有DamageScale分派。SingleStack标志保留当前Corruption／Hellraiser开发行为，不等同于完整原版PowerStackType。Rupture移除了公共专属槽并保留已有开发语义，但仍沿用当前EnemyPhase／CurrentPlay上下文，不能宣称任意来源已校准。自然胜负时其他Power仍沿用现有开发清理边界，显式结束／下战开始会清理。

正常Run、完整来源、独立适配器与原17项regression均未恢复；independent_verified保持false。原ENV/W完整验收与父项保持开放。

## 合并与手册同步（同日）

用户后续明确授权合并。核实QingXiang22Bot认证、两个准确head的四组CI和无未解决审阅要求后，先以merge方式合并#23（`3b562cf`），再将#24改以main为目标。#23合并树与#24原依赖树一致，#24合并候选树与已验证head `3d03740`一致，随后合并为`9078674`；没有强推或绕过检查。

手册同步基于该main，新添[修改单个内容](../../../development/base-spire/how-to/edit-content.md)、[新增独有内容模块](../../../development/base-spire/how-to/add-content-module.md)和[Power定义／生命周期参考](../../../development/base-spire/reference/powers.md)。同步导航、卡牌／物品指南、共享操作与skill路由，修正旧kind／重复伤害参数、已迁移衰减及自然胜负清理边界；CardCountRelic遗留但未使用的powerKind参数明确标注，不引导作者继续传入。本轮未修改生产DSL、Engine、Host或检查实现。

新指南的validate、5项content-model及7项CLAY命令实际通过。两篇既有教程从新隔离副本逐阶段复跑：TheAbacus原6、修改8、1000越界返回2；新增共享模板后得到2／4／6／6，无遗物为0／0／0／0，组合为10／12／14。真实Host固定seed为BASE-DSL-LESSON-00，报告仍为independent_verified=false；正式包摘要保持`e313cdcb91801a3f6894ccdda0ed3301599b39f3aa245f770b7313cfbd033711`。

项目skill校验及两个精确来源查询通过，文档检查与diff检查通过。Diataxis自查将两篇新增页面保持为目标导向的操作指南，参数／事件表放参考页，职责解释放explanation；未声称独立读者验收。本地演练和日志在`.audit/base-spire-manual-sync-20261019/`与`.artifacts/content-model-migration-20261019/manual-*`，均不提交。纯文档同步未在本地重跑407项全包或完整Engine／net组，广覆盖由PR CI负责。
