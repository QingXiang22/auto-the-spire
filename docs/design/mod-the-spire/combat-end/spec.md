# 战斗结束与敌人死亡

关联 CORE-AUTHOR-05／**DSL-COMBAT-END-01**；实施与认领见[主清单](../../../review/tofix/README.md#当前交付）。本包接在伤害与修正之后，按[纯 DSL 规格](../engine-dsl-migration/spec.md)和[机制迁移指南](../../../development/base-spire/how-to/migrate-mechanism.md)编写；状态只在 GitHub Issues 维护。

## 已核实来源与范围

从 `sts2/src/Core/Commands/CreatureCmd.cs`（`Kill`／`KillWithoutCheckingWinCondition`）与 `CombatManager` 的胜负路径核对：生物生命到 0 即死亡并触发死亡钩子；场内没有可命中敌人时战斗胜利；玩家死亡时战斗失败。本包把它落成开发态的两个判定：

- **敌人死亡**：`DamageEnemy` 与 `DamageEnemyPowered` 在“受击前生命 > 0 且受击后为 0”时发出 `EnemyDied`，然后计算战斗是否结束；已经 0 生命的敌人再次受击不重复发事件。
- **战斗结束**：`Player.CombatOutcome` 从 `none` 变为 `victory` 或 `defeat`，同时关闭回合门与战斗活跃标记，并发 `CombatEnded`。胜利只在**敌人死亡之后**评估（因此没有目标的开发战斗不会被误判为胜利）；失败在每次玩家掉血后评估（受到格挡伤害或失去生命都算）。

不迁移：死亡钩子与移除生物、战斗奖励与 Run 结算、真实引擎 terminal、敌方回合、复活与死亡防止、玩家死亡后的流程。`CombatOutcome` 是开发态字符串字段，不是公开协议里的终局声明。

## 行为要求与设计

| ID | 契约 | 判别 |
| --- | --- | --- |
| CB01 | 敌人生命从正数到 0 时发一次 EnemyDied；0 生命敌人再受击不重复发 | 连续两次击杀事件只出现一次，重复伤害不改变结果 |
| CB02 | 胜利只在敌人死亡后、且场上没有 `Hp > 0` 的敌人时判定 | 只打死一个而另一个存活时战斗继续 |
| CB03 | 玩家生命为 0 时判定失败，即使场上还有敌人 | 有存活敌人也能因失血失败 |
| CB04 | 结束状态先写 `CombatOutcome`、关闭回合门与活跃标记，再发 `CombatEnded` | 监听器观察到的已是结束态；结束后出牌与开始回合都没有合法候选 |
| CB05 | 新一次开始战斗把 `CombatOutcome` 复位为 `none`，回合与能量重新开始 | 失败后可以开新战斗，回合号为 1 |
| CB06 | 没有目标的开发战斗在掉血时不判胜利 | 只放失血牌、没有敌人的战斗保持 `none` 直到真正失败或结束 |

## 实施与验证顺序

1. 先加死亡事件与两个结束判定，并把它们接在既有伤害／失血过程的末尾；确认既有 72 项检查不回归。
2. 再补结束态字段、重置与公开观察。
3. 在包外补胜利、失败、重置与“无目标不判胜”的判别，并通过真实 stdio 验证公开 `CombatOutcome`。
4. 最后运行 solution 构建、包行为／覆盖、受影响 net 与文档检查；不改 Engine 语义。
