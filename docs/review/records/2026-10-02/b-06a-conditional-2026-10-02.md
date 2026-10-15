# B-06a：基础战斗内容批（第十四小批：条件效果 + Spite，2026-10-02）

> 历史证据（2026-10-03整理）：正文保留原时点结果，历史“下一步”不再作为当前派单指令。本报告归属W-06、W-01，未结项转入[当前清单](../../tofix/README.md)；移入records不表示修复或验收通过。逐份归属见[记录索引](../README.md)。

| 项目 | 内容 |
| --- | --- |
| 任务 ID | **B-06a 基础内容批**（第十四小批）——**条件效果**（`if { condition then else }`）+ `Spite` |
| 权威 | [开工清单 §14](../../../design/implementation-kickoff.md)（B-06a 行）；来源：`ContentSource/ironclad-cards.spire:3604` |
| 负责范围 | `Slice/SliceAuthorFrontend.cs`（`AuthorCondition`/`AuthorConditional`、`if` 嵌套块、与 `steps` 二选一、步骤表解析复用）、`Slice/CombatSlicePackage.cs`（玩家 `LostHpThisTurn` 字段 + 置位/清零 + 条件降级）、`Slice/Content/slice-content.author`、`net` 公开映射（新字段处置）、切片检查（新增 5 项） |
| 状态 | **本小批完成**；B-06a 其余子项仍未完成（见 §5） |
| 前置 | 第十三小批（能量） |

## 1. 来源与表示

```
play = { if = { condition = { playerLostHpThisTurn = { } }
                then = { repeat { count = 2  effect = { attack { damage = 5 } } } }
                else = { attack { damage = 5 } } } }
```

作者写法与该结构同形：

```
normal {
  amount = 5
  if {
    condition = player-lost-hp-this-turn
    then = [target, damage x2]
    else = [target, damage]
  }
}
```

**条件与 `steps` 二选一**（来源里 `if` 就是整个 play）：同时写两者 → `unsupported_combination`（不猜、不合并）。
谓词表只列切片**真的能判定**的条件（当前只有 `player-lost-hp-this-turn`），别的谓词解析期即拒绝。

## 2. 谓词的语义（置位/清零点都写清楚）

| 事件 | 处理 | 依据 |
| --- | --- | --- |
| 敌人攻击**真的扣到 HP**（扣掉格挡吸收之后 > 0） | 置位 | 来源把"受到伤害"算作 lost HP |
| `lose_hp`（HP 损失）且数值 > 0 | 置位 | 来源把 `loseHp` 也算作 lost HP |
| 玩家**回合开始** | 清零 | 谓词名是"this turn" |

## 3. 【net 契约】新玩家字段必须显式处置

新增 `Player.LostHpThisTurn` 后，net 投影立刻报 `[N-02-UNDECLARED-FIELD]`（不允许静默丢弃）。
处置：**不发布**，理由写进 profile —— 它完全可由公开的 `Hp` 变化推导，且不参与任何公开动作或终局判定
（重复发布会违反 `protocol.md:94`）。这是**有意**的处置决定，不是把检查放宽。

> 排查过程中的一个真问题：net **测试**通过后 J-01/client 仍失败——因为那两个套件跑的是
> **Host 可执行文件**，而我当时只重建了测试项目，Host 还是旧的。重建 solution 后恢复
> （J-01 150/150、client 50/50）。**教训：改了投影 profile 必须重建 Host 再验下游套件。**

## 4. 退出证据

| 判据 | 检查 |
| --- | --- |
| 语法/编译 | `if` 块与谓词被解析；两分支的步骤序列与批次数（then 两段 / else 一段）；条件效果时顶层 `steps` 为空；来源行 3604、普通 5 / 升级 6 |
| 拒绝面 | 未知谓词 `moon-is-full` → `invalid_value`；同时声明 `steps` 与 `if` → `unsupported_combination` |
| **运行（else）** | 本回合没掉血 → **5** 点 |
| **运行（then）** | 同回合内先自损 1 点 HP → **两段 5 = 10** 点 |
| **清零** | 下一回合开始后再打 → 回到 **5** 点（谓词真的按回合清零） |
| 冻结文本 | 来源行、升级形态谓词与两分支段数逐条一致 |

## 5. 覆盖分母与剩余

| 口径 | 值 |
| --- | --- |
| 迁移分母（94 张 Ironclad 卡 + 2 张状态牌） | **96** |
| 已迁入并可运行 | **21**（切片 4 + Dazed/Beckon + IronWave/TwinStrike/Breakthrough + PommelStrike/ShrugItOff + Thunderclap + Rampage + Entrench + Inflame + FeelNoPain + Rage + FightMe + Dominate + ExpectAFight + Spite） |
| 明确未迁 | 其余 75 张 |

仍未迁（各有具体缺口）：**按名字计数**、**交互式抽牌**（`Pillage`/`BurningPact`）、**费用修正/临时费用**
（`Stomp` 的 `modifySelectedCardCost` 一类）、**其他触发点**（受伤时、抽牌时…）、**其他条件谓词**
（`cardPlaysThisTurn` 等）、**`BodySlam`**（来源缺失，未声明）。

## 6. 运行结果（本轮实跑）

| 命令 | 返回码 | 结果 |
| --- | --- | --- |
| `dotnet build AutoTheSpire.sln` | 0 | 0 错误 0 警告 |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | 0 | **All 176 slice checks passed**（25 既有 + 4 B-05b + 87 B-05c + 60 项 B-06a） |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | 0 | 383 passed |
| `python ai-the-spire/tests/j01_episode.py` | 0 | 150 passed（**重建 Host 后**） |
| `python net-the-spire/client/test_client.py` | 0 | 50 passed |
| `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` | 0 | 64 passed |
| `dotnet run --project core-the-spire/Core.TheSpire.Tests` | 0 | 190 passed / 0 failed |
| AI 检查 / 契约 / 依赖 / 清点 | 0 | 503 / 210 / 9 / 分类完整 |

## 7. 修改文件

- `BaseSpire/Slice/SliceAuthorFrontend.cs`（`AuthorCondition`/`AuthorConditional`、`if` 嵌套块、二选一校验、`ParseStepList` 复用）
- `BaseSpire/Slice/CombatSlicePackage.cs`（`LostHpThisTurn` 字段、置位/清零、`ConditionExpr`、条件降级）
- `BaseSpire/Slice/Content/slice-content.author`（Spite）
- `net-the-spire/Net.TheSpire/Projection/SlicePublicProfile.cs`（新字段的显式处置）
- 切片检查：`SliceFrontendPlayConditionalChecks.cs`（新增 5 项）、`SliceChecks.cs`、`SliceFixtures.cs`、B-01 的字段计数
- `docs/review/tofix/long-task-progress-2026-10-02.md`（状态）

未提交、未清理共享工作区。

## 8. 下一批建议

**B-06a 第十五小批：按名字计数（`PerfectedStrike` 为反例）**——来源里它按"名字里含 Strike 的牌"计数，
需要在数值来源词表里加"按内容标签/名字匹配的牌区计数"，并确定匹配口径（名字子串 vs 声明标签 `tags = { Strike }`）；
来源里 `TwinStrike` 已带 `tags = { Strike }`，因此**优先按声明标签**实现并核对，取不到就记成明确未完成项。
