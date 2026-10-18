# 战斗金币奖励交付记录

日期：2026-10-18。任务：**CORE-AUTHOR-05／DSL-REWARD-GOLD-01**；当前认领、状态与恢复顺序只维护在[主清单](../../tofix/README.md#当前交付)。本记录接在[营火记录](room-rest-2026-10-18.md)之后，是实施与自查记录，不是独立评审，也没有独立原版对拍。

## 交付与范围

- `Player.Gold`（0..99999，默认 99，取 `Ironclad.StartingGold`）。
- `rewards/rules/combat-gold.spire`：订阅 `CombatEnded`，胜利时用命名流 `base-spire:development-reward` 调 `sampleInt(10, 21)`（源 Monster 房区间 10..20 是闭区间，`sampleInt` 半开），把结果加进金币；失败不给。

包版本升到 `0.5.0-dsl.24`。

**已知偏差**：金数来自开发命名流而不是源 run RNG，因此不复现源序列；只实现 Monster 房区间（Elite 35..45、Boss 100 与逃脱比例未迁移）；没有奖励面板、跳过、金币加成遗物与商店花费。

不迁移：Elite／Boss 区间、`CalculateGoldProportion`（逃脱比例）、GoldReward 的奖励面板与领取流程、金币修正遗物、商店与跨 Run 写回。

## 来源依据

`sts2/src/Core/Models/.../EncounterModel.cs`：`MinGoldReward`／`MaxGoldReward` 在 `RoomType.Monster` 下为 10／20（Elite 35／45、Boss 100／100），并按 Poverty 进阶缩放（本批不处理进阶）。`HealRestSiteOption` 同文件族里也确认 `Gold` 是玩家资源；`Ironclad.cs` 的 `StartingGold = 99` 作为开发初始值。

## 实际验证

Windows／PowerShell，.NET SDK 9.0.306；Debug 配置。包摘要 `54bff837cb2b0298acbc65d0f4bc4d0942044dfa555c8461d57626dca366b82d`，规则身份 `25375bb360a0ed22`。

| 命令 | 实际结果 |
| --- | --- |
| `dotnet build AutoTheSpire.sln -v minimal` | 通过，0 警告／0 错误 |
| `dotnet run --project tools/base-spire-dev -- validate` | 退出 0；包 `0.5.0-dsl.24` |
| `dotnet run --project tools/base-spire-dev -- check --report .artifacts/base-dsl/behavior` | 141 项全部通过 |
| `python -X utf8 tools/ci/check_relic_coverage.py .artifacts/base-dsl/behavior/package-behavior.json` | 退出 0：17 件遗物行为见证；源 297；未实现 279 |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | 362 项通过／0 失败 |

新增判别覆盖：开发玩家初始金币 99；一场胜利后金币落在 109..119；同 seed 的两个会话得到相同金币；失败不增加金币；四个不同 seed 的金币都在区间内。

## 未验证与未支持

- 未运行独立原版对拍、完整 Engine、正常 Run 与性能套件；`independent_verified` 保持 false。
- 只有 Monster 区间与“胜利即结算金币”，没有奖励面板、卡片／药水／遗物奖励、Elite／Boss 区间、逃脱比例与进阶缩放。
- 金币没有消费者（商店、事件花费、删除卡牌），也没有跨 Run 写回。

## 接续入口

下一包可补 Elite／Boss 区间与奖励面板（卡片三选一需要卡池权重），或补金币消费者（商店）。开始前先读主清单确认认领与依赖；不改 Engine 语义，不用 C# 效果绕过 DSL 表达缺口。
