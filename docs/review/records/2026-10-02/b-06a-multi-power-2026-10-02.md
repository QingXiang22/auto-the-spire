# B-06a：基础战斗内容批（第十一小批：多能力同牌 + FightMe，2026-10-02）

> 历史证据（2026-10-03整理）：正文保留原时点结果，历史“下一步”不再作为当前派单指令。本报告归属W-06、W-01，未结项转入[当前清单](../../tofix/README.md)；移入records不表示修复或验收通过。逐份归属见[记录索引](../README.md)。

| 项目 | 内容 |
| --- | --- |
| 任务 ID | **B-06a 基础战斗内容批**（第十一小批）——**多能力同牌**（同一张牌、同一能力、两个收受者、两种层数）+ `FightMe` |
| 权威 | [开工清单 §14](../../../design/implementation-kickoff.md)（B-06a 行）；来源：`Cards/Content/ironclad-cards.spire:3321` |
| 负责范围 | `Authoring/SliceAuthorFrontend.cs`（`apply_power:<n>`/`apply_power_self:<n>`/`apply_power_all:<n>` 字面量层数、`IsStep`、`stacks` 要求按需）、`Composition/CombatSlicePackage.cs`（三条新分支）、`Authoring/Examples/slice-content.author`（FightMe）、`net` 目录、切片检查（新增 4 项） |
| 状态 | **本小批完成**；B-06a 其余子项仍未完成（见 §5） |
| 前置 | 第十小批（出牌触发点 + 回合末移除） |

## 1. 具体反例决定了设计

`FightMe`（:3321）：

```
play = { repeat { count = 2  effect = { attack { damage = 5 } } }
         applyPower { power = Strength  stacks = 3  target = context.owner }    // 升级 4
         applyPower { power = Strength  stacks = 1  target = context.target } }
```

**同一张牌、同一个能力、两个收受者、两种层数**。原有作者模型每个变体只有**一组** `power`/`stacks`，
写不出这张牌——这就是缺口的**具体反例**（而不是"也许以后会需要"）。

## 2. 采用的表示（沿用既有写法，不新造一套语法）

| 写法 | 含义 |
| --- | --- |
| `apply_power_self:<n>` | 给自己施加 **n 层**（能力 id 取卡牌级 `power`） |
| `apply_power:<n>` | 给**所选目标**施加 n 层 |
| `apply_power_all:<n>` | 给全体各施加 n 层 |

- 参数是**层数**（0..999，越界不截断），能力 id 仍是**卡牌级** `power` —— 与 `generate:<id>`/`draw:<n>` 同一套"字面量参数步骤"机制；
- 卡牌级 `stacks` 现在**只在"存在没带字面量的施加步骤"时必需**（不再无条件要求），这样 `FightMe` 可以不写 `stacks`；
- **步骤次序按来源**：先两段伤害、再给自己、再给目标 ⇒ 这张牌自己的伤害**不**吃它给的 Strength（检查里专门断言了 10 点而不是 16）。

## 3. 退出证据

| 判据 | 检查 |
| --- | --- |
| 语法/编译 | 步骤序列 `[target, damage x2, apply_power_self:3, apply_power:1]`、伤害两段共 5/6、来源行 3321、能力 id 为卡牌级、升级形态 `self:4`/目标 `:1` |
| 校验 | 没有字面量且没有卡牌级 `stacks` → `missing_key`（不猜 0 层）；字面量 `1000` → `invalid_value` |
| **运行语义** | 两段 **10**（不吃自己给的 Strength）；**玩家 +3 力量**、**目标 +1 力量**（两个收受者互不串）；随后一张打击 6 + 3 = **9** |
| 升级形态 | 两段 **12**、玩家 **+4**、目标 **+1** |
| 冻结文本 | 来源行、两形态伤害、两形态逐步骤层数逐条一致 |

## 4. 运行结果（本轮实跑）

| 命令 | 返回码 | 结果 |
| --- | --- | --- |
| `dotnet build AutoTheSpire.sln` | 0 | 0 错误 0 警告 |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | 0 | **All 162 slice checks passed**（25 既有 + 4 B-05b + 87 B-05c + 46 项 B-06a） |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | 0 | 383 passed |
| `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` | 0 | 64 passed |
| `dotnet run --project core-the-spire/Core.TheSpire.Tests` | 0 | 190 passed / 0 failed |
| `python ai-the-spire/tests/run_checks.py` | 0 | 503 passed |
| 依赖检查 / 清点 / J-01 / client / 契约 | 0 | 9 / 分类完整 / 150 / 50 / 210 |

## 5. 覆盖分母与剩余

| 口径 | 值 |
| --- | --- |
| 迁移分母（94 张 Ironclad 卡 + 2 张状态牌） | **96** |
| 已迁入并可运行 | **18**（切片 4 + Dazed/Beckon + IronWave/TwinStrike/Breakthrough + PommelStrike/ShrugItOff + Thunderclap + Rampage + Entrench + Inflame + FeelNoPain + Rage + FightMe） |
| 明确未迁 | 其余 78 张 |

**下一个具体反例已经找到并记录**：`Dominate`（:3425）除了多能力之外，还需要
**跨实体的数值来源**——`applyScaledPower { power = Strength  base = 0  perCount = 1
count = { targetPowerAmount = { power = Vulnerable } } }`（"给自己加等于**目标身上**易伤层数的力量"）。
这需要新的数值来源形态（读**别的**实体身上的能力层数），属下一批的能力缺口，不是本批能顺手带的。

其余未迁：能量获取（`ExpectAFight`）、条件（`Clash`）、按名字计数（`PerfectedStrike`）、
交互式抽牌（`Pillage`/`BurningPact`）、`BodySlam`（来源缺失）。

## 6. 修改文件

- `BaseSpire/Authoring/SliceAuthorFrontend.cs`（三种带层数的施加形式、`IsStep`、`stacks` 按需）
- `BaseSpire/Composition/CombatSlicePackage.cs`（三条参数形式的分支）
- `BaseSpire/Authoring/Examples/slice-content.author`（FightMe + 表示说明注释）
- `net-the-spire/Net.TheSpire/Projection/SlicePublicProfile.cs`（目录内容 ID）
- 切片检查：`SliceFrontendMultiPowerChecks.cs`（新增 4 项）、`SliceFixtures.cs`、`SliceChecks.cs`
- `docs/review/tofix/long-task-progress-2026-10-02.md`（状态）

未提交、未清理共享工作区。

## 7. 下一批建议

**B-06a 第十二小批：跨实体数值来源（`target-power:<id>`）**——以 `Dominate` 为反例：
把自己要施加的层数建立在**目标身上某个能力的层数**上。
需要先决定表示（数值来源词表里加 `target-power:<id>`，还是 `apply_power_self_from_target:<id>`），
并核对它与"来源读取时点"（施加前/后）的关系，再实现并声明 `Dominate`。
