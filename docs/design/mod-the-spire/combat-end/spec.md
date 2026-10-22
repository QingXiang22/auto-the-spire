# 战斗结束与敌人死亡

关联 CORE-AUTHOR-05／**DSL-COMBAT-END-01**；实施与认领见[GitHub 任务清单](https://github.com/QingXiang22/auto-the-spire/issues/28)。本包接在伤害与修正之后，按[纯 DSL 规格](../base-architecture.md)和[机制迁移指南](../../../development/base-spire/how-to/migrate-mechanism.md)编写；状态只在 GitHub Issues 维护。

## 已核实来源与范围

当前规则以[锁定参考](../../../../reference/README.md)的 `CreatureCmd.ResolveConfirmedDeath`、`PowerCmd.Apply`／`Remove` 和战斗胜负路径为依据。普通敌人确认死亡后发布通知并清理应失效的 Power；死亡或逃离后场内没有活动敌人时战斗胜利，玩家死亡时战斗失败。

- **敌人死亡**：伤害结果在“受击前生命 > 0 且受击后为 0”时标记一次死亡；`PublishDamageDeath` 先发出 `EnemyDied`，等待通知完成，再将敌人标记为 `DeathResolved` 并逐个调用完整 `RemovePower`。死亡通知能看到死亡前持有的 Power；移除通知发生在退出 `Powers` 后、实例销毁前。已确认死亡的敌人不能再次接受 Power；重复伤害不重复发死亡或移除通知。
- **战斗结束**：`Player.CombatOutcome` 从 `none` 变为 `victory` 或 `defeat`，同时关闭回合门与战斗活跃标记，并发 `CombatEnded`。胜利在**敌人死亡或逃离之后**评估（因此没有目标的开发战斗不会被误判为胜利）；失败在每次玩家掉血后评估（受到格挡伤害或失去生命都算）。

当前加载的 Power 均采用来源的两项默认死亡移除规则。`DieForYou`、`Adaptable`、`SteamEruption`、`Minion`、`PainfulStabs` 的自身保留，以及 `Illusion`／`Reattach` 的移除否决尚未接入，不注册为空效果；接入这些内容前必须补齐对应查询与持有者脱离后的监听资格。当前不宣称支持复活、防死或完整死亡钩子。敌人实例作为伤害／出牌结果的引用仍保留至访问清理，`DeathResolved` 只表达普通敌人的已确认死亡，不作为逃离状态。

## 非死亡逃离与靶人计时

锁定来源的 `CreatureCmd.Escape` 先移除当时持有的 Power，再将生物从活动战斗成员中移出，保留其正数 HP。共享 `EscapeEnemy(owner,target)` 对已逃离、死亡或非活动战斗不重复执行；成功时记录 `Enemy.Escaped`，发出 `EnemyEscaped`，不发 `EnemyDied`，不产生伤害或 Fatal。Power 移除通知发生时目标尚未脱离；脱离后不能再接受 Power、被选为目标、执行或准备招式。保留的实例供访问结果读取，逃离不等同于死亡。

`Player.CombatTimedOut` 记录本次战斗有靶人计时逃离，并在新战斗开始时复位。没有活动敌人时，既有死亡或本次逃离都可触发外层结束判定；空开发战斗仍不会自动获胜。与原生引擎一致，全部敌人逃离时战斗胜负仍为 victory，另以 TimedOut 区分父事件是否授予档位奖励。该标志必须在 CombatEnded 前可见；真正击杀不置位，计时过期但尚未逃离也不提前置位。开发靶人超时不生成普通怪物奖励；其他普通房间逃离的金币比例仍未迁移。

BattleFriendV1／V2／V3 的固定 HP 为75／150／300，每次真实创建仍消耗一次 niche HP 抽样；IDLE 为固定自循环且不消费 AI 随机。战斗加入阶段在 BeforeCombatStart 前赋予3层 BattlewornDummyTimeLimitPower。该 Power 只在存活持有者的敌方回合结束时推进：3→2→1，第三次保留1层并标记过期，随后由该靶人消费者执行 EscapeEnemy。先完成计时再执行逃离；多个实例独立计时，移除 Power 同时清理其私有状态。父事件访问与奖励返回另行实现，本组件不提供完整 BattlewornDummy 事件入口。

正常战斗的外层余效、奖励和永久提交见 [Run 约定](../run-domain/spec.md)。`CombatOutcome` 是玩法字符串字段，不是公开协议里的终局声明。

## 行为要求与设计

| ID | 约定 | 判别 |
| --- | --- | --- |
| CB01 | 敌人生命从正数到 0 时发一次 EnemyDied；0 生命敌人再受击不重复发 | 连续两次击杀事件只出现一次，重复伤害不改变结果 |
| CB02 | 胜利只在敌人死亡或逃离后、且场上没有正 HP 且未逃离的敌人时判定 | 只打死一个而另一个存活时战斗继续 |
| CB03 | 玩家生命为 0 时判定失败，即使场上还有敌人 | 有存活敌人也能因失血失败 |
| CB04 | 结束状态先写 `CombatOutcome`、关闭回合门与活跃标记，再发 `CombatEnded` | 监听器观察到的已是结束态；结束后出牌与开始回合都没有合法候选 |
| CB05 | 新一次开始战斗把 `CombatOutcome` 复位为 `none`，回合与能量重新开始 | 失败后可以开新战斗，回合号为 1 |
| CB06 | 没有目标的开发战斗在掉血时不判胜利 | 只放失血牌、没有敌人的战斗保持 `none` 直到真正失败或结束 |
| CB07 | 普通敌人先完成死亡通知，再标记确认死亡并移除其 Power；其他敌人和玩家的 Power 不受影响 | 真实首战 Bash→Strike 击杀 LeafSlimeS 后 Vulnerable 消失，战斗仍继续；通知可见旧 Power，移除时已出集合，重复通知无额外效果 |
| CB08 | 已确认死亡的目标不接受后续 Power，外层卡牌余效与胜利保护保持原顺序 | 致死 Bash 不在尸体上重新施加 Vulnerable；末杀 Feed 等余效仍在正常结算前完成 |

## 实施与验证顺序

1. 将死亡事件、Power 清理与两个结束判定接在既有伤害／失血过程的准确时点，验证既有伤害与结束判别。
2. 再补结束态字段、重置与公开观察。
3. 在包外补胜利、失败、重置与“无目标不判胜”的判别，并通过真实 stdio 验证公开 `CombatOutcome`。
4. 最后运行 solution 构建、包行为／覆盖、受影响 net 与文档检查；不改 Engine 语义。
