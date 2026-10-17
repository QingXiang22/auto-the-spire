# B-05c：完整前端覆盖（第九批：敌人行动程序，2026-10-02）

> 历史证据（2026-10-03整理）：正文保留原时点结果，历史“下一步”不再作为当前派单指令。本报告归属W-06、W-01，未结项转入[当前清单](../../tofix/README.md)；移入records不表示修复或验收通过。逐份归属见[记录索引](../README.md)。

| 项目 | 内容 |
| --- | --- |
| 任务 ID | **B-05c**（第九批）——**敌人的行动程序**作者化（此前只作者化了它们的数据） |
| 权威 | [开工清单 §14](../../../design/implementation-kickoff.md#14-m1后阶段二sts作者前端与内容迁移)（B-05c 行含"Power/敌人/药水及 run"） |
| 负责范围 | `Authoring/Examples/slice-content.author`（两个敌人的行动声明）、`Authoring/SliceAuthorFrontend.cs`（move/cycle 解析与校验）、`Composition/CombatSlicePackage.cs`（按定义 + MoveIndex 分派的通用程序）、`BaseSpire.Slice.Tests/Enemies/SliceFrontendEnemyProgramChecks.cs`（新增 3 项） |
| 状态 | **部分完成（九批）**：敌人行动程序已作者化；B-05c 其余子项仍未开始（见 §5） |
| 前置 | [批1](b-05c-frontend-batch1-2026-10-02.md) … [批8](b-05c-frontend-batch8-2026-10-02.md) |

## 1. 作者语法与降级

```
enemy calcified-cultist {
  ...（数据不变）...
  move incantation { ritual = first-ritual }     // 给自己上 Ritual（层数取该敌人的字段）
  move dark-strike { damage = strike }           // 打玩家（伤害取该敌人的字段）
  cycle = [incantation, dark-strike xforever]    // 先各一次，最后一个永远重复
}
```

- **行动动作词表**：`ritual = <int|first-ritual>`、`damage = <int|strike>`。
  值可以是字面量，也可以是**该敌人的字段引用**（`first-ritual` → `FirstMoveRitual`，`strike` → `StrikeDamage`），
  因此每个敌人的数值仍然只写在数据里，不会两处维护。
- **降级**：一个程序服务所有敌人 → 先按 `DefinitionId` 分派，再按 `MoveIndex` 走声明的循环
  （`IfElse(index == k)` 链，最后一项用 `GreaterOrEqual` 表示"永远重复"）。
  **程序化路径保持原来的两分支程序**（开局念咒、此后暗击），冻结作者文本与它对同一份数据给出同样行为。

## 2. 编译期规则（都带路径）

| 情形 | 错误码 |
| --- | --- |
| 循环里少了某个已声明的行动（**不可达行动**） | `unreachable_move` |
| 循环引用未声明的行动 | `dangling_reference` |
| 非末尾行动标 `xforever` | `unsupported_combination` |
| 未知动作名（如 `heal = strike`） | `unknown_key` |
| 完全没有行动程序（只有数据） | `missing_key` |

第 5 条是**刻意的收紧**：行动程序是必需事实，缺了它运行时就无法让这个敌人行动；
以前"只声明数据"能通过，等于把必需事实留成隐式默认——现在显式拒绝。

## 3. 退出证据

| 判据 | 检查 |
| --- | --- |
| 解析 | `解析 move/cycle`：循环为 `[incantation, dark-strike]`、行动数为 2、动作文本 `ritual = first-ritual` |
| 编译期拒绝 | 上表 5 类情形全部被拒绝且路径含 `enemy` |
| 运行语义 | `开场念咒、此后每回合暗击，数值来自声明的字段引用 [真实]`：第 1 个敌人回合玩家**不掉血**、两名敌人分别获得声明的 3/5 层 Ritual；第 2 个回合受 **7+2=9**（前排字段引用、后排字面量）；第 3 个回合仍在暗击（`xforever`），伤害 9 + 8（Ritual 已转成 Strength） |
| **与程序化路径一致** | `冻结作者文本已声明行动程序，且作者路径与程序化路径行为一致 [真实]`：两个敌人在两个回合后玩家血量完全相同 |
| 必需事实不留默认 | 冻结作者文本里每个敌人都声明了行动程序与循环，且重复行动是 `dark-strike` |

## 4. 运行结果（本轮实跑）

| 命令 | 返回码 | 结果 |
| --- | --- | --- |
| `dotnet build AutoTheSpire.sln` | 0 | 0 错误 0 警告 |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | 0 | **All 69 slice checks passed**（25 既有 + 4 B-05b + 40 项 B-05c） |
| `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` | 0 | 64 passed |
| `dotnet run --project core-the-spire/Core.TheSpire.Tests` | 0 | 190 passed / 0 failed |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | 0 | 383 passed（不回归） |
| `python ai-the-spire/tests/run_checks.py` | 0 | 503 passed（不回归） |
| `python ai-the-spire/tests/j01_episode.py` | 0 | 150 passed（不回归） |
| `python tools/contract-check/check_contracts.py` | 0 | 210 passed（不回归） |
| `python net-the-spire/client/test_client.py` | 0 | 50 passed（不回归） |

## 5. 本轮修正的缺陷

1. **检查的期望算错（不是实现错）**：第 3 个敌人回合我按"每回合 9 点"断言，实际是 17 —— 因为 Ritual 在
   上一个回合结束时转成了 Strength（3+5=8）。改为断言 9 + 17 并在注释里写明差额来源；实现未动。
2. **fixture 文本的缩进导致 Replace 静默失配**：C# 原始字符串会剥掉公共缩进，我按"源文件里的缩进"写替换模式，
   于是"完全没有行动程序"那一例根本没被改写（解析成功 → 检查报"必须被拒绝"）。改为按行过滤（`TrimStart`），
   不再依赖缩进。
3. 顺带把 8 个检查文件的场景文本补上行动程序（此前只声明数据），与新规则一致。

## 6. B-05c 剩余子项（仍未完成）

Power 的**行为程序**（Power 的 events/lifecycle 仍由程序化程序处理，作者文本只声明其数据）；
保留时点（retain）；实例变量；生成/升级/变形/附魔；药水/run（事件/房间/奖励/地图）；
hook 里更广的步骤面（`target`/`apply_power`/`top_from_discard`/`exhaust_self`）；0.0.1 兼容入口。

## 7. 修改文件

- `BaseSpire/Authoring/Examples/slice-content.author`（两个敌人的 `move`/`cycle`）
- `BaseSpire/Authoring/SliceAuthorFrontend.cs`（`AuthorMove`/`AuthorMoveStep`、`ParseMoves`/`ParseCycle`、5 条校验）
- `BaseSpire/Composition/CombatSlicePackage.cs`（按定义 + MoveIndex 分派的通用敌人程序；字段引用求值）
- `BaseSpire.Slice.Tests/Enemies/SliceFrontendEnemyProgramChecks.cs`（新增 3 项）、8 个既有场景文本补行动程序、`SliceChecks.cs`（注册）
- `docs/review/tofix/long-task-progress-2026-10-02.md`（状态）

未修改：`Engine/`、Legacy、net、Python 侧。未提交、未清理共享工作区。

## 8. 下一批建议

**B-05c 第十批：Power 的行为程序作者化**（与敌人行动程序对称：把 Power 的 events/lifecycle 从程序化程序
迁到作者文本），以及**保留时点（retain）**。两者都需要先确认运行时是否有对应能力，缺则先在 `Engine/` 补。
