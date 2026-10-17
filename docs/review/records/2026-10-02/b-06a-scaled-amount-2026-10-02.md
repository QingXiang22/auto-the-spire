# B-06a：基础战斗内容批（第十五小批：缩放数值 + AshenStrike，2026-10-02）

> 历史证据（2026-10-03整理）：正文保留原时点结果，历史“下一步”不再作为当前派单指令。本报告归属W-06、W-01，未结项转入[当前清单](../../tofix/README.md)；移入records不表示修复或验收通过。逐份归属见[记录索引](../README.md)。

| 项目 | 内容 |
| --- | --- |
| 任务 ID | **B-06a 基础内容批**（第十五小批）——**缩放数值来源**（`base + perCount × 计数`）+ `AshenStrike` |
| 权威 | [开工清单 §14](../../../design/implementation-kickoff.md)（B-06a 行）；来源：`Cards/Content/ironclad-cards.spire:3057` |
| 负责范围 | `Authoring/SliceAuthorFrontend.cs`（`exhaust-count`、`amount-base`/`amount-per-count`）、`Composition/CombatSlicePackage.cs`（`ScaledAmount` 降级）、`Authoring/Examples/slice-content.author`、`net` 目录、切片检查（新增 5 项） |
| 状态 | **本小批完成**；B-06a 其余子项仍未完成（见 §5） |
| 前置 | 第十四小批（条件效果） |

## 1. 先找**来源可判定**的反例（这一步值得记）

原计划做 `PerfectedStrike`（按名字/标签计数）。读来源后发现它用：

```
count = { cardsWithTag = { tag = Strike } }
```

而 `cardsWithTag` 在**整个可用来源里只出现在这张牌上**，且**没有牌区限定**（既没有 `pile = …`，
`sts2/src` 里也没有它的实现）。也就是说"数哪些牌区（手牌？抽牌堆？弃牌堆？全牌组？）"**无法确定**。
按纪律**不猜、不声明**——这条作为明确未完成项记在报告里，而不是用近似口径凑一张卡。

于是改选**来源完全可判定**的 `AshenStrike`（:3057）：

```
damageValue         = scaled { base = 6  perCount = 3  count = { pileCount { pile = exhaust } } }
upgradedDamageValue = scaled { base = 6  perCount = 4  count = { pileCount { pile = exhaust } } }
```

牌区（`exhaust`）、base、perCount **都写在来源里** ⇒ 可以逐条核对。

## 2. 本批能力

| 能力 | 说明 |
| --- | --- |
| 数值来源 `exhaust-count` | 消耗区牌数（来源 `pileCount { pile = exhaust }`） |
| 变体键 `amount-base` / `amount-per-count` | 数值 = **base + perCount × 来源**（来源 `scaled { base perCount count }`） |

**默认值 0 / 1** ⇒ 既有内容的数值语义**一字不变**（检查里专门断言"切片那 4 张卡的缩放参数是默认值"）。

## 3. 退出证据

| 判据 | 检查 |
| --- | --- |
| 语法/编译 | `exhaust-count` 被接受；`amount-base`/`amount-per-count` 被解析（普通 6/3、升级 6/4）；来源行 3057；带来源时不再有字面量 `amount` |
| 拒绝面 | `amount-base = -1` → `invalid_value`；`amount-per-count = 1000` → `invalid_value`；`exhaust-count:Attack`（不支持的类型过滤）→ `invalid_value` |
| **运行（0 张）** | 消耗区为空 → 伤害 **6**（= base） |
| **运行（2 张）** | 先消耗 2 张 → 伤害 **12**（= 6 + 3×2） |
| **升级** | 同样 2 张 → 伤害 **14**（= 6 + 4×2，升级只改 perCount） |
| 行为不变 | 切片那 4 张卡的 base/perCount 是默认值 |

## 4. 覆盖分母与剩余

| 口径 | 值 |
| --- | --- |
| 迁移分母（94 张 Ironclad 卡 + 2 张状态牌） | **96** |
| 已迁入并可运行 | **22**（切片 4 + Dazed/Beckon + IronWave/TwinStrike/Breakthrough + PommelStrike/ShrugItOff + Thunderclap + Rampage + Entrench + Inflame + FeelNoPain + Rage + FightMe + Dominate + ExpectAFight + Spite + AshenStrike） |
| 明确未迁 | 其余 74 张 |

**新增明确未完成项（来源不可判定）**：

- **`PerfectedStrike`（:3263）**：`count = { cardsWithTag { tag = Strike } }` **没有牌区限定**，
  且 `sts2/` 无实现 ⇒ "数哪些牌区"无法确定。要声明它，需要先取得该口径（或由规划者裁定）。
  顺带记：来源里已有 `tags = { Strike }` 这类**卡牌标签**，作者模型目前**没有**标签概念——
  它是实现 `cardsWithTag` 的前置能力，等口径确定后一起补。

其余未迁：交互式抽牌（`Pillage`/`BurningPact`）、费用修正/临时费用（`Stomp` 的 `modifySelectedCardCost`）、
其他触发点（受伤时、抽牌时…）、其他条件谓词、`BodySlam`（来源缺失）。

## 5. 运行结果（本轮实跑）

| 命令 | 返回码 | 结果 |
| --- | --- | --- |
| `dotnet build AutoTheSpire.sln` | 0 | 0 错误 0 警告 |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | 0 | **All 181 slice checks passed**（25 既有 + 4 B-05b + 87 B-05c + 65 项 B-06a） |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | 0 | 383 passed |
| `python ai-the-spire/tests/j01_episode.py` | 0 | 150 passed |
| `python net-the-spire/client/test_client.py` | 0 | 50 passed |
| `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` | 0 | 64 passed |
| `dotnet run --project core-the-spire/Core.TheSpire.Tests` | 0 | 190 passed / 0 failed |
| AI 检查 / 契约 / 依赖 | 0 | 503 / 210 / 9 |

## 6. 修改文件

- `BaseSpire/Authoring/SliceAuthorFrontend.cs`（`ExhaustCount`、`amount-base`/`amount-per-count`、`ScaledPart`）
- `BaseSpire/Composition/CombatSlicePackage.cs`（`exhaust-count` 与 `ScaledAmount` 降级）
- `BaseSpire/Authoring/Examples/slice-content.author`（AshenStrike）
- `net-the-spire/Net.TheSpire/Projection/SlicePublicProfile.cs`（目录内容 ID）
- 切片检查：`SliceFrontendScaledAmountChecks.cs`（新增 5 项）、`SliceFixtures.cs`、`SliceChecks.cs`
- `docs/review/tofix/long-task-progress-2026-10-02.md`（状态）

未提交、未清理共享工作区。

## 7. 下一批建议

**B-06a 第十六小批：卡牌标签（`tags`）+ `cardsWithTag`**——但**先要口径**：
需要规划者/来源确认 `cardsWithTag` 统计的牌区范围（或提供 `sts2/` 的实现位置）。
若口径无法确定，就改做**费用修正/临时费用**（`Stomp` 的 `modifySelectedCardCost` 在来源里字段完整：
`amount = scaled { base = 0  perCount = -1  count = { cardPlaysThisTurn { type = attack  phase = finished } } }`
+ `operation = add` + `expires = { turnEnd }`），按同样"先补能力再声明内容"的次序推进。
