# 战斗结束与敌人死亡交付记录

日期：2026-10-18。任务：**CORE-AUTHOR-05／DSL-COMBAT-END-01**；当前认领、状态与恢复顺序只维护在[主清单](../../tofix/README.md#当前交付)。本记录对应[本包规格](../../../design/mod-the-spire/combat-end/spec.md)的 CB01～CB06，是实施与自查记录，不是独立评审，也没有独立原版对拍。

## 交付与范围

在既有伤害与失血过程的末尾接入胜负判定：

- `DamageEnemy`／`DamageEnemyPowered` 在“受击前生命 > 0 且受击后为 0”时发出 `EnemyDied`，然后调用 `CheckCombatVictory`；已经 0 生命的敌人再受击不会重复发事件。
- `DamagePlayer`／`LosePlayerHp` 调用 `CheckCombatDefeat`。
- `CheckCombatVictory` 只在敌人死亡之后评估（没有目标的开发战斗不会被误判为胜利）；`CheckCombatDefeat` 在生命为 0 时失败。
- 结束态写在 `Player.CombatOutcome`（`none`／`victory`／`defeat`），同时关闭回合门与战斗活跃标记，再发 `CombatEnded`。开始新战斗把它复位为 `none`。

不迁移：死亡钩子与移除生物、战斗奖励与 Run 结算、真实引擎 terminal、敌方回合、复活与死亡防止、玩家死亡后的流程。包版本升到 `0.5.0-dsl.11`。

## 来源依据

| 行为 | 源 |
| --- | --- |
| 生物生命到 0 即死亡并触发死亡钩子，场上无可命中敌人则胜利、玩家死亡则失败 | `sts2/src/Core/Commands/CreatureCmd.cs`（`Kill`／`KillWithoutCheckingWinCondition`）与 `CombatManager` 的胜负路径 |
| 可命中敌人 = 未死亡 | `CombatState.HittableEnemies`（本 scope 以 `Hp > 0` 表示） |

本批没有新增内容定义，因此没有新的源 hash 需要核对；行为依据是上一批已核对的伤害与失血链。

## 实际验证

Windows／PowerShell，.NET SDK 9.0.306；Debug 配置。包摘要 `07897fea5ca9aa69cb7339ebec3f619696d347af8d4cf2a6136bfb14452c83f7`，规则身份 `86e9fff8757a6411`；行为报告内的 `package_digest` 与当前包一致。

| 命令 | 实际结果 |
| --- | --- |
| `dotnet build AutoTheSpire.sln -v minimal` | 通过，0 警告／0 错误 |
| `dotnet run --project tools/base-spire-dev -- validate` | 退出 0；包 `0.5.0-dsl.11`；card 目录 578 源／20 个 DSL definition |
| `dotnet run --project tools/base-spire-dev -- check --report .artifacts/base-dsl/behavior` | 75 项全部通过（既有 72 项无回归） |
| `python -X utf8 tools/ci/check_relic_coverage.py .artifacts/base-dsl/behavior/package-behavior.json` | 退出 0：8 件遗物行为见证；源 297；未实现 288；`independent_verified=false` |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | 360 项通过／0 失败，含新增 CP48 真实 stdio 轨迹 |

新增判别覆盖：只打死一个目标而另一个存活时战斗继续（`CombatOutcome` 仍为 `none`）；杀死最后一个目标后结果变 `victory`，出牌与开始回合都不再有合法候选，而开始新战斗仍合法；生命降到 0 时即使没有敌人也判 `defeat` 并关闭战斗；开新战斗把结果复位为 `none` 且回合号回到 1。真实 stdio 轨迹用升级 Bludgeon 一击 42 点杀死 40 HP 目标，观察公开目标 40→0、能量 3→0 与 `Player.CombatOutcome` 为 `victory`。

过程中出现过一次环境性失败：版本号只改了 `package.json`／公开元数据而没同步 `base.spire`，net 组因此报 `Manifest identity does not match the compiled source`（13 项失败）。同步版本后重新运行，360 项全部通过；这不是规则行为回归。

## 未验证与未支持

- 未运行独立原版对拍、完整 Engine 套件（留给 Actions）、正常 Run、完整游戏矩阵与性能套件；`independent_verified` 保持 false。
- 未实现：敌方回合与敌方攻击、死亡钩子与生物移除、战斗奖励与 Run 结算、引擎 terminal、复活与死亡防止。
- `CombatOutcome` 是开发态字段，不进入公开协议的终局声明；公开元数据的 `terminal_outcomes` 仍为空。
- 胜负判定只在伤害／失血路径触发；若将来出现其它改变生命的路径，需要同样接入。

## 接续入口

下一包可补敌方行动（并接回 Weak／易伤减层的真实时点）与战斗奖励入口，或继续批量接牌、补原版目标选择与公开决策应答。开始前先读主清单确认认领与依赖；不改 Engine 语义，不用 C# 效果绕过 DSL 表达缺口。
