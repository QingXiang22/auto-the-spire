# 敌方阶段与 FuzzyWurmCrawler 交付记录

日期：2026-10-18。任务：**CORE-AUTHOR-05／DSL-ENEMY-TURN-01**；当前认领、状态与恢复顺序只维护在[主清单](../../tofix/README.md#当前交付)。本记录对应[本包规格](../../../design/mod-the-spire/enemy-turn/spec.md)的 EN01～EN05，是实施与自查记录，不是独立评审，也没有独立原版对拍。

## 交付与范围

新增**敌方阶段**：`EndDevelopmentTurn` 的顺序变为弃手牌 → `AfterTurnEnded` → `RunEnemyTurn`（对每个 `Hp > 0` 的敌人发 `EnemyTurnStarted`）→ `AfterEnemyTurnEnded` → 关闭回合门。debuff 减层订阅从 `AfterTurnEnded` 移到 `AfterEnemyTurnEnded`，回到源的“敌方回合末减层”时点。

新增**敌方攻击链** `DamagePlayerPowered`：攻击者 `damage-additive`（Strength）→ 攻击者 `damage-dealt-multiplier`（Weak ×0.75）→ 防守方 `damage-taken-multiplier`（Vulnerable ×1.5），整数向下取整，再格挡→生命并做失败判定。

新增敌人字段 `MoveIndex`（招位）与 `Intent`（公开意图名）与第一个原版敌人 **FuzzyWurmCrawler**（`enemies/rules` 模板 + `enemies/shared` 内容）：HP 取 Ascension 0 最小值 55，轮转为 goop(4) → inhale(+7 强度) → goop(4) → 回到 goop(4)，与源三个状态的后继关系一致。包版本升到 `0.5.0-dsl.13`。

不迁移：其余 120 个敌人、遭遇编队、意图伤害数值（只公开意图名）、原版 AI 随机分支、初始 HP 区间随机（用最小值）、死亡钩子与生物移除、奖励与 Run 结算。

## 来源依据

`sts2/src/Core/Models/Monsters/FuzzyWurmCrawler.cs`：`MinInitialHp` 55／`MaxInitialHp` 57（A0）、`AcidGoopDamage` 4、`Inhale` 施加 `StrengthPower` 7、`FIRST_ACID_GOOP → INHALE → ACID_GOOP → FIRST_ACID_GOOP`。源 hash 由已安装 skill 的定位助手从安装路径核对为 `source_hash_status: matches`，资格 `singleplayer`（敌人目录原本 121 条、`pools` 为空、资格 `unreviewed`，本批只改这一条的资格与 definition）。

## 实际验证

Windows／PowerShell，.NET SDK 9.0.306；Debug 配置。

| 命令 | 实际结果 |
| --- | --- |
| `dotnet build AutoTheSpire.sln -v minimal` | 通过，0 警告／0 错误 |
| `dotnet run --project tools/base-spire-dev -- validate` | 退出 0；包 `0.5.0-dsl.13`；enemy 目录 121 源／1 个 DSL definition；card 25、relic 8 |
| `dotnet run --project tools/base-spire-dev -- check --report .artifacts/base-dsl/behavior` | 86 项全部通过 |
| `python -X utf8 tools/ci/check_relic_coverage.py .artifacts/base-dsl/behavior/package-behavior.json` | 退出 0：8 件遗物行为见证；源 297；未实现 288；`independent_verified=false` |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | 362 项通过／0 失败，含新增 CP61 真实 stdio 轨迹 |

新增判别覆盖：开发生成取源最小 HP 55，首个意图为 attack；结束回合后 goop 打 4、招位与意图推进为 inhale／buff；第二回合 inhale 造成 0 伤害并给敌人 7 层强度；第三回合 goop 打 11；第四回合再打 11（生命 80→76→76→65→54）；10 点格挡完全吸收 4 点并保留 6 点；1 层 Weak 让 4 点攻击降到 3；Bash 施加的 2 层 Vulnerable 在玩家回合结束时仍是 2，敌方阶段结束后变 1。真实 stdio 轨迹生成敌人、开始战斗与回合、结束回合，观察公开玩家生命 80→76 与 `Enemy.Intent` 变为 buff。

## 未验证与未支持

- 未运行独立原版对拍、完整 Engine 套件（留给 Actions）、正常 Run、完整游戏矩阵与性能套件；`independent_verified` 保持 false。
- 只有 1/121 个敌人迁移；没有遭遇编队、意图伤害数值、开场条件、随机 AI、死亡钩子与移除；初始 HP 用最小值而非源随机区间。
- 敌方阶段按 `type(Enemy)` 全局遍历，没有按遭遇或战斗分组；多玩家场景未考虑。
- `Enemy.Intent` 只是招位对应的意图名，公开元数据没有意图伤害字段。

## 接续入口

下一包可继续补敌人（每只一个模板＋内容）或补遭遇编队与意图伤害，也可回到卡牌内容批次或原版目标选择。开始前先读主清单确认认领与依赖；不改 Engine 语义，不用 C# 效果绕过 DSL 表达缺口。
