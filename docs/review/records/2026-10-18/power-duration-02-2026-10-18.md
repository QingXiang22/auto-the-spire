# 回合末能力失效（Flame Barrier 生命周期）交付记录

日期：2026-10-18。任务：**CORE-AUTHOR-05／DSL-POWER-DURATION-02**；当前认领、状态与恢复顺序只维护在[主清单](../../tofix/README.md#当前交付)。本记录接在[反击记录](thorns-2026-10-18.md)之后，用于关闭该记录里登记的“Flame Barrier 反击不会在回合末清除”偏差。

## 交付与范围

- `Power` 增加 `ExpiresAtTurnEnd` 标记，`ApplyPlayerPowerUntilTurnEnd` 在创建与合层两条路径上都会置位。
- 新增 `ExpireTurnEndPowers`，在 `EndDevelopmentTurn` 的**敌方阶段之后**调用（顺序：弃手牌 → `AfterTurnEnded` → `RunEnemyTurn` → `ExpireTurnEndPowers` → 关闭回合门）。放在敌方阶段之后是关键：Flame Barrier 的反击必须在敌人的攻击期间仍然生效。
- 新增效果模板 `ApplySelfPowerUntilTurnEndEffect`，Flame Barrier 改用它；Bronze Scales 仍用永久版本。

包版本仍为 `0.5.0-dsl.23`（同批未发布，仅内容与规则修订）。

不迁移：其它带“本回合”寿命的能力（如临时力量、姿态）、敌方持有者的当回合能力、能力的显式移除与驱散。

## 来源依据

`FlameBarrierPower` 在源里是随回合结束移除的能力，而 `ThornsPower` 的伤害来自 `BeforeDamageReceived`，所以 Flame Barrier 在敌方攻击期间仍要反击。本批只依据这一点调整寿命顺序；数值仍来自上一批已核对的 `FlameBarrier.cs`（12／16 格挡、4／6 反击）。

## 实际验证

Windows／PowerShell，.NET SDK 9.0.306；Debug 配置。包摘要 `af86da1dccf315c7d92185553bd3dad0cac3e998637a07621e1663d7e922c217`，规则身份 `2d6dec14e0583d54`。

| 命令 | 实际结果 |
| --- | --- |
| `dotnet build AutoTheSpire.sln -v minimal` | 通过，0 警告／0 错误 |
| `dotnet run --project tools/base-spire-dev -- validate` | 退出 0；包 `0.5.0-dsl.23` |
| `dotnet run --project tools/base-spire-dev -- check --report .artifacts/base-dsl/behavior` | 136 项全部通过 |
| `python -X utf8 tools/ci/check_relic_coverage.py .artifacts/base-dsl/behavior/package-behavior.json` | 退出 0：17 件遗物行为见证；源 297；未实现 279 |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | 362 项通过／0 失败 |

新增判别覆盖：Flame Barrier 打出后，敌方第一次攻击仍吃 4 点反击（7→3），该回合结束后反击能力消失；下一回合敌方再攻击时不再受反击（仍为 3），玩家这次掉了 4 点（上一回合的 12 格挡吸收了第一次攻击）。Bronze Scales 的 3 层反击在两次攻击中都生效（7→4→1）且不会过期。

过程中修了一处自己的期望值错误：把“第一次被 12 格挡吸收、第二次才掉血”的情况写成了掉 8 点，实际是 76。

## 未验证与未支持

- 未运行独立原版对拍、完整 Engine、正常 Run 与性能套件；`independent_verified` 保持 false。
- 只覆盖“玩家回合末失效”这一种寿命；敌方侧的当回合能力与其它临时能力未迁移。
- 能力目录 260 条 definition 仍为空（角色与寿命都由应用内容传入）。

## 接续入口

下一包可继续补能力触发／寿命点（如临时力量、每回合能力），或转向房间与奖励入口。开始前先读主清单确认认领与依赖；不改 Engine 语义，不用 C# 效果绕过 DSL 表达缺口。
