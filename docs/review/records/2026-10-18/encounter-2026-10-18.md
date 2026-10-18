# 遭遇编队模板与首个源遭遇交付记录

日期：2026-10-18。任务：**CORE-AUTHOR-05／DSL-ENCOUNTER-01**；当前认领、状态与恢复顺序只维护在[主清单](../../tofix/README.md#当前交付)。本记录接在[敌方阶段规格](../../../design/mod-the-spire/enemy-turn/spec.md)之后，是实施与自查记录，不是独立评审，也没有独立原版对拍。

## 交付与范围

新增遭遇域：

- 共享过程 `SpawnDevelopmentEnemy`（按 DefinitionId 与 HP 创建一只敌人，`MoveIndex=0`、`Intent="attack"`）。
- 模板 `SingleEnemyEncounter`（definition、enemyDefinition、hp），生成 `Spawn` 动作。
- 首个源遭遇 `base-spire:fuzzy-wurm-crawler-weak`（源 `FuzzyWurmCrawlerWeak.cs`：Monster 房、`IsWeak`、只刷一只 FuzzyWurmCrawler）。

包版本升到 `0.5.0-dsl.17`，遭遇定义 1/88。

不迁移：遭遇池与队列、房间与战斗入口、弱/普通遭遇的资格与权重、多怪编队、随机槽位选择、开场动作（`startingActionsByEnemyIndex`），以及 Run／奖励。`Spawn` 是开发动作，不是房间入口。

## 来源依据

`sts2/src/Core/Models/Encounters/FuzzyWurmCrawlerWeak.cs`：`RoomType.Monster`、`IsWeak = true`、`AllPossibleMonsters` 与 `GenerateMonsters` 都只含 `FuzzyWurmCrawler`。源 hash 由已安装 skill 的定位助手从安装路径核对为 `source_hash_status: matches`，资格 `singleplayer`。遭遇自带的怪物仍是 FuzzyWurmCrawler 的模板行为（A0 HP 55 起、goop／inhale 轮转），本批没有改动它。

## 实际验证

Windows／PowerShell，.NET SDK 9.0.306；Debug 配置。

| 命令 | 实际结果 |
| --- | --- |
| `dotnet build AutoTheSpire.sln -v minimal` | 通过，0 警告／0 错误 |
| `dotnet run --project tools/base-spire-dev -- validate` | 退出 0；包 `0.5.0-dsl.17`；encounter 目录 88 源／1 个 DSL definition；enemy 6、card 29、relic 8 |
| `dotnet run --project tools/base-spire-dev -- check --report .artifacts/base-dsl/behavior` | 105 项全部通过 |
| `python -X utf8 tools/ci/check_relic_coverage.py .artifacts/base-dsl/behavior/package-behavior.json` | 退出 0：8 件遗物行为见证；源 297；未实现 288；`independent_verified=false` |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | 362 项通过／0 失败 |

新增判别覆盖：遭遇的 `Spawn` 只创建一只敌人、DefinitionId 与源一致、HP/MaxHp 取源最小 55、意图与招位从初始值开始；这只怪物的轮转照常（结束一个玩家回合后玩家掉 4 点），把它打到 0 生命后开发战斗判为 victory。

## 未验证与未支持

- 未运行独立原版对拍、完整 Engine 套件（留给 Actions）、正常 Run、完整游戏矩阵与性能套件；`independent_verified` 保持 false。
- 遭遇只覆盖 1/88；没有房间入口、遭遇池、弱/普通权重、多怪编队、随机槽位与开场动作；初始 HP 用区间最小值。
- `definition` 参数只承担身份命名，不参与行为；遭遇与敌人的关系靠内容文件里的 `enemyDefinition` 与目录定义。
- 开发动作 `Spawn` 不校验战斗状态、不与房间或奖励联动。

## 接续入口

下一包可继续补多怪编队（含随机槽位与开场动作）、把遭遇接进房间／地图，或继续补敌人与卡牌。开始前先读主清单确认认领与依赖；不改 Engine 语义，不用 C# 效果绕过 DSL 表达缺口。
