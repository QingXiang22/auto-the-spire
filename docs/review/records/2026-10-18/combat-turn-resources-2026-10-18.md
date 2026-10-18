# 开发态回合生命周期与生命／能量资源交付记录

日期：2026-10-18。任务：**CORE-AUTHOR-05／DSL-COMBAT-TURN-01**；当前认领、状态与恢复顺序只维护在[主清单](../../tofix/README.md#当前交付)。本记录对应[本包规格](../../../design/mod-the-spire/combat-turn-resources/spec.md)的 TC01～TC08，是实施与自查记录，不是独立评审，也没有独立原版对拍。

## 交付与范围

在纯 DSL base 包内新增玩家生命、能量与回合计数：`Hp`／`MaxHp`／`Energy`／`MaxEnergy`／`TurnNumber`／`TurnOpen`，共享过程 `Heal`、`GainEnergy`，以及开发态回合生命周期 `BeginDevelopmentTurn`／`EndDevelopmentTurn` 和事件 `AfterBlockCleared`、`AfterTurnStarted`、`AfterPlayerTurnStartLate`。开始战斗现在同时把回合号、能量和回合门复位。

以四个只依赖这些资源的新模板迁移四件原版遗物：Lantern（第 1 回合 +1 能量）、BloodVial（第 1 回合回复 2 生命）、HornCleat（第 2 回合开始时 +14 无加成格挡）、HappyFlower（每 3 个回合开始 +1 能量，计数器跨战斗保留）。包版本升到 `0.5.0-dsl.3`，公开目录新增四个真实 ID，DLS 定义数由 3 增至 7。

## 回合顺序的来源依据

按 `sts2/src/Core/Combat/CombatManager.cs`、`Creature.AfterTurnStart`、`PlayerCombatState.ResetEnergy` 与 `PlayerCmd.GainEnergy` 核对后固定为：自增回合号 → 回合号大于 1 时清空格挡并发 `AfterBlockCleared`（源在玩家第 1 回合提前返回，因此第一回合保留战斗开始时获得的格挡）→ `Energy = MaxEnergy` → 依次发 `AfterTurnStarted` 与 `AfterPlayerTurnStartLate`。结束回合把 `Hand` 移到 `DiscardPile` 并关闭回合门。

开发时点与原版钩子不是一一对应：原版把能量重置与侧回合开始拆在 `AfterEnergyReset`／`AfterSideTurnStart` 两处，本包合并为一个 `AfterTurnStarted`；`TurnOpen` 代替未迁移的 `PlayerTurnPhase`。能量修正链、格挡修正参与者（Barricade／Blur／SturdyClamp）、死亡与复活、逐张弃牌钩子和敌方回合都没有迁移。

| 内容 | 源 | 行为 |
| --- | --- | --- |
| `base-spire:lantern` | `Lantern.cs`（`5f94cfba…7a57fbb9`） | `AfterSideTurnStart`，`TurnNumber <= 1`，+1 能量 |
| `base-spire:blood-vial` | `BloodVial.cs`（`abf8d390…8574cad56`） | `AfterPlayerTurnStartLate`，`TurnNumber <= 1`，回复 2 |
| `base-spire:horn-cleat` | `HornCleat.cs`（`31339778…367b75405`） | `AfterBlockCleared`，`TurnNumber == 2`，+14 无加成格挡 |
| `base-spire:happy-flower` | `HappyFlower.cs`（`b9ec4d3d…80258b89d`） | `AfterSideTurnStart` 计数取模 3 归零时 +1 能量，计数跨战斗保留 |

四个 hash 由已安装的 skill 定位助手从安装路径实际核对：`locate.py relics --id <id> --verify-source` 对四件都返回 0、`source_hash_status: matches`、definition 指向本批新文件。定位只证明静态源身份一致，不是玩法执行或独立验证。源条件 `TurnNumber <= 1` 写成模板参数 `turn = 1`：开发战斗从 0 回合开始，第一次开始时回合号必为 1，两者等价。

## 实际验证

Windows／PowerShell，.NET SDK 9.0.306；Debug 配置。包摘要 `c07e0ba65a43f8ae88f02d8a43f1202790e7892688e1adf1b34a27c8fc334bff`，规则身份 `3e05ef88aad26120`；行为报告内的 `package_digest` 与当前包一致。

| 命令 | 实际结果 |
| --- | --- |
| `dotnet build AutoTheSpire.sln -v minimal` | 通过，0 警告／0 错误 |
| `dotnet run --project tools/base-spire-dev -- validate` | 退出 0；包 `0.5.0-dsl.3`；relic 目录 297 源／7 个 DSL definition |
| `dotnet run --project tools/base-spire-dev -- check --report .artifacts/base-dsl/behavior` | 28 项全部通过 |
| `python -X utf8 tools/ci/check_relic_coverage.py .artifacts/base-dsl/behavior/package-behavior.json` | 退出 0：7 件行为见证；源 297；未实现 289；`independent_verified=false` |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | 352 项通过／0 失败，含新增 TC07 真实 stdio 轨迹 |
| `python -X utf8 docs/examples/base-dsl/run_relic_lesson.py --manifest mod-the-spire/base-spire/package.json --obtain TheAbacus.Obtain --expect-block 6` | 退出 0，`passed: true`；新增字段与动作没有破坏第一篇教程的公开路径 |

新增判别覆盖：新周期只重置回合／能量／格挡而不重置生命；第一回合保留战斗开始格挡、第二回合先清空再由监听补足；回合门在公开候选与过程守卫两处拒绝未开始、重复开始和重复结束，且拒绝不产生状态变化；`Heal` 在满血、部分血量与超上限三种边界；`GainEnergy` 可以超过 `MaxEnergy` 并在下一回合重置；四个模板分别对无遗物、已移除、已熔化和别的持有者都不触发；HappyFlower 计数跨回合与跨战斗持续推进。四个内容见证各自生成，没有复用其它遗物的场景。

## 回归与兼容

上一批的 RC 系列检查在同一份报告里继续执行并通过；`DB-A05/A10` 的迁移身份断言同步更新为 7 件，297 源分母、卡池资格和 `exclude` 语义不变。旧 scope 仍在创建会话前拒绝，`--require-complete` 仍不成立。

## 未验证与未支持

- 未运行独立原版对拍、完整 Engine 套件（留给 Actions）、正常 Run、完整游戏矩阵与性能套件；`independent_verified` 保持 false。
- 未实现：伤害与敌人、卡牌定义／出牌／费用／目标、Power、正常战斗初始化与胜负、房间／事件、地图与奖励、跨房写回、原版随机布局与钩子划分。
- 未迁移：能量修正链、格挡清理参与者、死亡与复活、逐张弃牌钩子、敌方回合；`TurnOpen` 不是原版 `PlayerTurnPhase`。
- 四个新模板只覆盖“单个遗物实例每场触发一次”的语义；未交付替换、熔化和按角色的来源资格。

## 接续入口

下一包按[迁移任务](../../../design/mod-the-spire/engine-dsl-migration/tasks.md#后续恢复范围)继续共享战斗与资源机制的伤害／格挡吸收部分，再接有源依据的敌人与卡牌消费者。开始前先读主清单确认认领与依赖；不改 Engine 语义，不用 C# 效果绕过 DSL 表达缺口。
