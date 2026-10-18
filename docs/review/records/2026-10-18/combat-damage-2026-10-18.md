# 伤害与格挡吸收交付记录

日期：2026-10-18。任务：**CORE-AUTHOR-05／DSL-COMBAT-DAMAGE-01**；当前认领、状态与恢复顺序只维护在[主清单](../../tofix/README.md#当前交付)。本记录对应[本包规格](../../../design/mod-the-spire/combat-damage/spec.md)的 DD01～DD07，是实施与自查记录，不是独立评审，也没有独立原版对拍。

## 交付与范围

在纯 DSL base 包内新增无加成、可格挡的伤害结算：`DamageEnemy` 与 `DamagePlayer` 都先扣 `min(Block, amount)` 格挡，剩余从 Hp 扣除且不低于 0（源 `Creature.DamageBlockInternal`＋`LoseHpInternal`）。新增开发伤害目标实体 `Enemy`（Owner／DefinitionId／Hp／MaxHp／Block）与显式创建动作 `AddDevelopmentTarget`；目标是开发探针，不是任何原版怪物的行为、意图、AI、死亡或遭遇实现。

同时补上与源同名的玩家回合开始中段时点 `AfterPlayerTurnStart`，并把原来的 `AfterTurnStarted` 改名为 `AfterSideTurnStarted`（对应源 `AfterSideTurnStart`）。三个时点的顺序为：清格挡与 `AfterBlockCleared` → 能量重置 → `AfterSideTurnStarted` → `AfterPlayerTurnStart` → `AfterPlayerTurnStartLate`。

消费者为 MercuryHourglass：`AfterPlayerTurnStart` 对每个 `Hp > 0` 的 Enemy 造成 3 点无加成伤害（源 `DamageVar(3m, ValueProp.Unpowered)`）。包版本升到 `0.5.0-dsl.4`，公开目录新增该真实 ID，DSL 定义由 7 增至 8 件。

## 来源依据

| 内容 | 源 | 行为 |
| --- | --- | --- |
| `base-spire:mercury-hourglass` | [MercuryHourglass.cs](../../../../sts2/src/Core/Models/Relics/MercuryHourglass.cs)，`916280bc3a6627c90ed9ff2868adafcdb1f59f7941427156fc61a957d2147a50`（本批经定位助手核对为 matches） | `AfterPlayerTurnStart`，`DamageVar(3m, ValueProp.Unpowered)`，对全部可命中敌人结算 |
| 伤害结算 | [CreatureCmd.cs](../../../../sts2/src/Core/Commands/CreatureCmd.cs)、[Creature.cs](../../../../sts2/src/Core/Entities/Creatures/Creature.cs) | 格挡吸收 `min(Block, amount)`，生命扣除且下限 0；`UnblockedDamage` 为实际掉血 |
| 回合开始时点 | [CombatManager.cs](../../../../sts2/src/Core/Combat/CombatManager.cs)、[Hook.cs](../../../../sts2/src/Core/Hooks/Hook.cs) | 能量重置与抽牌后先 `AfterSideTurnStart`，再 `AfterPlayerTurnStart`（Early→正常→Late 三轮），最后 `AfterPlayerTurnStartLate` |

本批用已安装的 skill 定位助手从安装路径核对 MercuryHourglass 的源身份：`locate.py relics --id base-spire:mercury-hourglass --verify-source` 返回 0、`source_hash_status: matches`、definition 指向 `relics/shared/mercury-hourglass.spire`；定位只证明静态源身份一致，不是玩法执行或独立验证。开发目标的 Hp 40 是开发常量，不来自任何源怪物。

## 实际验证

Windows／PowerShell，.NET SDK 9.0.306；Debug 配置。包摘要 `114485cbcc64a0f30a68d08536cd6d02fcc052fca4147e078aca68c692029014`，规则身份 `2aa61b89cc93fbbc`；行为报告内的 `package_digest` 与当前包一致。

| 命令 | 实际结果 |
| --- | --- |
| `dotnet build AutoTheSpire.sln -v minimal` | 通过，0 警告／0 错误 |
| `dotnet run --project tools/base-spire-dev -- validate` | 退出 0；包 `0.5.0-dsl.4`；relic 目录 297 源／8 个 DSL definition |
| `dotnet run --project tools/base-spire-dev -- check --report .artifacts/base-dsl/behavior` | 34 项全部通过 |
| `python -X utf8 tools/ci/check_relic_coverage.py .artifacts/base-dsl/behavior/package-behavior.json` | 退出 0：8 件行为见证；源 297；未实现 288；`independent_verified=false` |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | 353 项通过／0 失败，含新增 DD07 真实 stdio 轨迹 |

新增判别覆盖：未调用创建动作时没有任何目标；每次调用创建一个带声明身份的目标；格挡充足时生命不变、格挡不足时只掉差额、格挡被精确消耗、生命到 0 后继续受击仍为 0；玩家侧同样先扣格挡且不改 MaxHp；`AfterPlayerTurnStartLate` 监听器能看到 `AfterPlayerTurnStart` 已经造成的伤害（顺序判别）；无目标、已移除、已熔化和别的持有者都不触发，目标所属战斗外的持有者在其自己回合开始时才结算；MercuryHourglass 对多个目标各扣 3，并在下一回合与新战斗中重复。真实 stdio 轨迹用同一 Host 走 `AddDevelopmentTarget`→获得遗物→开始战斗→两个回合，核对公开 `Enemy.Hp` 40→37→34 与玩家能量／回合号。

## 回归与兼容

上一批的 RC 与 TC 系列检查在同一份报告里继续执行并通过；`AfterTurnStarted` 改名为 `AfterSideTurnStarted` 时同步更新了两个订阅它的模板，包加载验证确认没有遗留旧符号。`DB-A05/A10` 的迁移身份断言同步更新为 8 件，297 源分母与卡池资格不变。

## 未验证与未支持

- 未运行独立原版对拍、完整 Engine 套件（留给 Actions）、正常 Run、完整游戏矩阵与性能套件；`independent_verified` 保持 false。
- 未实现：`Unblockable` 与其他 ValueProp、力量／易伤等伤害修正、`ModifyHpLost`、受击反应事件、死亡与移除、战斗胜利、怪物意图与行动、卡牌与出牌、Power。
- 开发目标 Hp 40、`base-spire:development-target` 与只由动作创建都不是原版遭遇；`AddDevelopmentTarget` 不是正式怪物生成。
- `AfterSideTurnStarted` 仍把原版 `AfterEnergyReset` 与 `AfterSideTurnStart` 合并，Energy 修正链未迁移。

## 接续入口

下一包按[迁移任务](../../../design/mod-the-spire/engine-dsl-migration/tasks.md#后续恢复范围)进入出牌与牌区：卡牌定义字段、费用与能量消耗、Hand→InPlay→DiscardPile 移动、`CardPlayed` 事件与目标选择，再接 Ironclad 起始的 Strike／Defend 内容。开始前先读主清单确认认领与依赖；不改 Engine 语义，不用 C# 效果绕过 DSL 表达缺口。
