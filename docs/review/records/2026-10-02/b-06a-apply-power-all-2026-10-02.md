# B-06a：基础战斗内容批（第五小批：对全体施加能力，2026-10-02）

> 历史证据（2026-10-03整理）：正文保留原时点结果，历史“下一步”不再作为当前派单指令。本报告归属W-06、W-01，未结项转入[当前清单](../../tofix/README.md)；移入records不表示修复或验收通过。逐份归属见[记录索引](../README.md)。

| 项目 | 内容 |
| --- | --- |
| 任务 ID | **B-06a 基础战斗内容批**（第五小批）——**`apply_power_all`（对全体施加能力）** + `Thunderclap` |
| 权威 | [开工清单 §14](../../../design/implementation-kickoff.md)（B-06a 行："典型多敌…逐机制更新net profile/coverage"）；来源：`Cards/Content/ironclad-cards.spire:2535` |
| 负责范围 | `Authoring/SliceAuthorFrontend.cs`（新步骤 + 目标域互斥 + `power`/`stacks` 要求 + hook 允许面）、`Composition/CombatSlicePackage.cs`（降级）、`Authoring/Examples/slice-content.author`（Thunderclap）、`net` 目录、切片检查（新增 4 项） |
| 状态 | **本小批完成**；B-06a 其余子项仍未完成（见 §5） |
| 前置 | 第一〜四小批（状态牌 / HP 损失 / 基础牌 / 抽牌） |

## 1. 来源语义

```
card Thunderclap {
    normal = { type = attack  cost = 1  target = { selectAll = {} }
               play = { attack = { damage = 4  target = allEnemies }
                        applyPower = { power = Vulnerable  stacks = 1  target = allEnemies } } }
    upgraded = { … damage = 7 … applyPower = { power = Vulnerable  stacks = 1 … } } }
```

两个要点：**伤害数与层数是独立事实**（4/7 与 1/1），以及**给全体施加不提问**（没有可挂起的选择）。

## 2. 纪律与既有校验对齐（不是新发明一套）

| 规则 | 理由 |
| --- | --- |
| `apply_power_all` 需要 `power` 与 `stacks` | 与 `apply_power` 一致：能力与层数必须显式声明，不能猜 |
| `apply_power` 与 `apply_power_all` **互斥** | 同一能力落在"单个目标"还是"全体"身上必须唯一（与 `damage`/`damage_all` 同一条纪律） |
| `apply_power_all` **不可加批次** | 它本身就是给全员各一次，"×2"只表示层数翻倍（应直接写层数），与 `damage_all` 同理 |
| `apply_power_all` **不要求** `target` 步骤 | 它不设置伤害目标槽；与 `damage_all` 一样不必先 `target` |
| 允许进 hook 允许面 | 它在端末/入场等时点不挂起决策，是可安全触发的批量效果 |

## 3. 退出证据

| 判据 | 检查 |
| --- | --- |
| 语法/编译 | 步骤次序 `[damage_all, apply_power_all]`、来源行 2535、普通 4 / 升级 7、层数 1、能力 id；四条负例分别以 `unsupported_combination`（互斥）、`missing_key`（缺 `power`）、`missing_key`（缺 `stacks`）、`unsupported_combination`（批次）拒绝 |
| **典型多敌（运行）** | `Thunderclap 给每个敌人各上 1 层易伤，且不提问 [真实]`：提交后**没有**目标决策；前排 −4、**后排也 −4**；两个敌人**各获得 1 层易伤**；随后一张打击 6 → **9**（易伤真的生效，证明层数落在正确的敌人身上） |
| 升级形态 | 打 7，后排仍然各上 1 层 |
| 冻结文本 | 来源行、升级形态末步骤为 `apply_power_all`、升级伤害 7 |

## 4. 运行结果（本轮实跑）

| 命令 | 返回码 | 结果 |
| --- | --- | --- |
| `dotnet build AutoTheSpire.sln` | 0 | 0 错误 0 警告 |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | 0 | **All 136 slice checks passed**（25 既有 + 4 B-05b + 87 B-05c + 20 项 B-06a） |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | 0 | 383 passed |
| `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` | 0 | 64 passed |
| `dotnet run --project core-the-spire/Core.TheSpire.Tests` | 0 | 190 passed / 0 failed |
| `python ai-the-spire/tests/run_checks.py` | 0 | 503 passed |
| 依赖检查 / 清点 / J-01 / client / 契约 | 0 | 8 / 分类完整 / 150 / 50 / 210 |

## 5. 覆盖分母

| 口径 | 值 |
| --- | --- |
| 迁移分母（94 张 Ironclad 卡 + 2 张状态牌） | **96** |
| 已迁入并可运行 | **12**（切片 4 + Dazed/Beckon + IronWave/TwinStrike/Breakthrough + PommelStrike/ShrugItOff + Thunderclap） |
| 明确未迁 | 其余 84 张 |

## 6. B-06a 剩余（未完成）

- **以格挡为伤害**（`Body Slam`）、**按名字计数**（`Perfected Strike`）、**条件**（`Clash`）；
- **能量获取**（`Offering`/`Seeing Red`）、**费用修正/临时费用**；
- 需要选择/交互的：`Pillage`（`drawUntil`）、`BattleTrance`（本回合不能再抽）、`BurningPact`（弃牌选择）；
- 3+ 敌/召唤/复活等更复杂的多敌场景；其余 84 张卡；随后是 B-06b/c。

## 7. 修改文件

- `BaseSpire/Authoring/SliceAuthorFrontend.cs`（`ApplyPowerAll`、互斥校验、`power`/`stacks` 要求、hook 允许面、不可批次）
- `BaseSpire/Composition/CombatSlicePackage.cs`（`apply_power_all` → `ForEach(monsters, applyPower)`）
- `BaseSpire/Authoring/Examples/slice-content.author`（Thunderclap）
- `net-the-spire/Net.TheSpire/Projection/SlicePublicProfile.cs`（目录内容 ID）
- 切片检查：`SliceFrontendApplyPowerAllChecks.cs`（新增 4 项）、`SliceFrontendHookStepChecks.cs`（允许面）、
  `SliceFixtures.cs`（已迁内容清单）、`SliceChecks.cs`（注册）
- `docs/review/tofix/long-task-progress-2026-10-02.md`（状态）

未提交、未清理共享工作区。

## 8. 下一批建议

**B-06a 第六小批：以格挡为伤害（`damage_from_block`）**——解锁 `Body Slam`，
它要求把"格挡值"作为一个**数值来源**（作者侧的 `amount-source` 已有 `literal/hand-count/discard-count/…
variable:<name>`；把"自身格挡"加入来源词表即可，且必须核对与乘性修正、上限阶段的相互关系）。
