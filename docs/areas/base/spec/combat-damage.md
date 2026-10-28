# 伤害、格挡吸收与实际失血

文档类型：参考（Reference）。关联 CORE-AUTHOR-05／**DSL-COMBAT-DAMAGE-01**；任务与状态见[GitHub 任务清单](https://github.com/QingXiang22/auto-the-spire/issues/28)。本页定义生产base DSL的伤害行为，操作见[卡牌与战斗指南](../how-to/cards-and-combat.md)和[行为参考](../reference/card-behavior.md)。

## 来源与范围

规则依据为[锁定sts2-sim](../../../../reference/README.md)的 `Commands/CreatureCmd.cs::Damage`／`DamageSingle`、`Entities/Creatures/Creature.cs::DamageBlockInternal`／`LoseHpInternal`、`Hooks/Hook.cs::ModifyDamage`／`ModifyHpLost`。实现位于 `combat/card-damage.spire`、`damage-results.spire`、`damage.spire` 和 `attacks.spire`，由同一Engine执行。

本页覆盖当前已加载卡牌、敌人、药水和反应的有加成攻击、无加成攻击、直接伤害及不可格挡失血。已有消费者包括Strength／Vigor、Weak／Vulnerable（PaperPhrog、PaperKrane、Cruelty）、PenNib、Thorns、Rupture和Feed。承伤转移、防死与多人分支尚未接入；Fogmog的召唤与幻象复活见[敌人生命周期](enemy-turn.md)。

## 数值与结果要求

| ID | 行为要求 | 判别 |
| --- | --- | --- |
| DD01 | 目标来自当前实际角色／敌人实例，保存实际承受者与显式dealer、card、play、attack；缺少来源仍为空，不按EnemyPhase或CurrentCard猜测 | 无加成反应不能继承正在执行的卡；两个目标／会话不串引用 |
| DD02 | 先完整加性链，再按源顺序乘法，最后统一截到非负；不得在Strength与Vigor之间提前截零 | 基础6、Strength −10、Vigor +5得到1；负合计、Weak／Vulnerable及PenNib组合分别核对 |
| DD03 | 保留格挡与HP修正所需的分数时点；格挡消耗按截断的blocked扣除，未格挡量再经源适用的HP修正，最终HP扣除截断并限制在0..999999999 | 分数全挡／破挡、0伤、实际失血与超杀；不把请求值当作实际损失 |
| DD04 | `DamageResult`分别保存Requested、Final、Blocked、Unblocked、Overkill、BlockBroken、FullyBlocked及本次Killed；Unblocked是实际HP减少，Overkill使用源限制后的loss | 全挡、破挡、超杀、大值BodySlam和同批致死；Killed是本次事实，后续反应不得改写历史 |
| DD05 | 一击的全部目标先提交数值，再逐结果发布破挡、HP变化、给予和非致死接收通知，最后统一处理潜在死亡；同一Attack逐击推进 | 第一目标反应看到后续目标已提交；反击致死中止后续击；Tracker八击仍为一次攻击 |
| DD06 | 预览与执行复用同一有上下文公式，预览不消费Power、随机或实体；Vigor在一整个攻击期间保留有效快照，完成后只消费原快照 | 多击与nested、PenNib第十次、连续预览、未命中／非法提交分别核对 |
| DD07 | 生产公开Host及stdio消费同一包，显示手牌、格挡、HP和实际候选；显式开发目标与真实敌人、组件与正常Run分别报告 | 保留已有真实stdio路径、原四条正常路径、11份suite及严格M01要求 |
| DD08 | 仅在源码实际站点发布通知：AfterBlockBroken在格挡被实际消耗至0时；AfterCurrentHpChanged只在实际损失大于0时；给予／接收不以正伤害代替资格 | 零伤与全挡仍区分给予／接收，致死结果按源跳过普通接收并进入死亡处理 |
| DD09 | ModifyHpLost阶段与参与者的副作用分开；实际承受者、命令结束门禁和死亡边界由真实消费者确定 | 未迁移消费者显式说明，不以中性Hook声称转移／防死已实现 |

当前真实乘法消费者的比例及输入上限须逐项核对，不能仅因现有实现用四位整数缩放便推定精度不足，也不能由小值通过推定全部decimal语义成立。新增乘法族、比例或HP修正读取小数时重新证明表示、次序和溢出边界；发现通用数值能力缺口时先给最小复现及接口影响，不暗中改为浮点近似。

## 命令与通知顺序

`CreatureCmd.Damage`先固定目标列表，每个目标执行数值结算并保存结果；然后逐结果依次处理破挡、实际HP变化、伤害给予和允许的伤害接收；致死结果最后统一处理潜在死亡。DSL继续使用已有DamageResult／DamageBatch，不建立第二套伤害执行器。普通攻击、Unpowered|Move攻击和直接Unpowered伤害分别保留源属性与来源，结束中各命令仍按自身资格处理。

玩家受伤反应在本次实际结果上运行，Feed只据本次击杀事实处理末杀。反应触发的嵌套伤害不得提前结算外层战斗；结束、清理和奖励属于[战斗结束规格](combat-end.md)。回合开始清格挡、能量、AfterPlayerSideTurnStart与普通／Late通知顺序沿用[回合资源规格](combat-turn-resources.md)，不在伤害模块另建生命周期。

## 实施与验证

先以已有真实乘法链和源方法建立有区分力的双侧向量，再实现数值、结果及通知；补齐旧规格时保留现有多目标、Vigor多击、Tracker、玩家死亡Power清理和Feed末杀要求。人工构造的大值与正常游戏流程分别报告。只在相关改动后运行定向检查，再由PR CI选择公共检查；原必测ID、来源池和未支持边界不缩减。缺参考、缺用例、缺维度和实际差异保持非零。

## 已核对的表示边界与监听迁移

已核对的原乘法链包含玩家Weak的3/4、Vulnerable的整数百分比（含PaperPhrog／Cruelty）、PenNib的整数倍，以及敌方Weak的整数百分比、Vulnerable的3/2和Colossus的1/2；这些单实例链可用10000缩放精确表示。BodySlam合法大格挡值与原层数上限的中间乘积在Int64范围内。

首幕批次新增的Shrink使用7/10。基础伤害1叠加Weak、PaperPhrog修正的Vulnerable和Shrink时，系数为0.91875，当前10000缩放的整数计算保留为0.9187。因此原链的精度结论不覆盖该组合；其最终伤害与结果字段需按DD03核对。

四位缩放贯穿修正、格挡分数和HP输入；最终仅在源实际转换处截断。已确认并修复的差异是加性链中提前截零，以及未按LoseHpInternal限制loss而夸大的超杀。当前已核对消费者没有ModifyHpLost覆写，因此HP输入经过其源中性路径；TheBoot、TungstenRod、Intangible等未迁移族的四阶段修正、实际参与者回调、转移及防死仍未支持，不用中性Hook计作内容完成。

Inferno、Rupture、CentennialPuzzle与SelfFormingClay使用同一AfterDamageReceived，Thorns使用BeforeDamageReceived。旧PlayerHpLost／PlayerDamaged／BeforePlayerAttacked入口已移除；来源与属性在数值结算前绑定。AfterDamageHpLost只表示伤害造成的正实际损失，不冒充涵盖治疗的AfterCurrentHpChanged。DamageResolved继续是base内部结果摘要；完整原生事件比较由对应工单维护，不能仅凭本页的局部通知判别声明全部事件一致。
