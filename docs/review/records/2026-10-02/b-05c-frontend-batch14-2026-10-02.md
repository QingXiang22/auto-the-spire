# B-05c：完整前端覆盖（第十四批：状态来源的数值，2026-10-02）

> 历史证据（2026-10-03整理）：正文保留原时点结果，历史“下一步”不再作为当前派单指令。本报告归属W-06、W-01，未结项转入[当前清单](../../tofix/README.md)；移入records不表示修复或验收通过。逐份归属见[记录索引](../README.md)。

| 项目 | 内容 |
| --- | --- |
| 任务 ID | **B-05c**（第十四批）——**状态来源的数值**（切片里"实例变量"的最小形态：数值在打出那一刻求值，不是声明常量） |
| 权威 | [开工清单 §14](../../../design/implementation-kickoff.md#14-m1后阶段二sts作者前端与内容迁移)（B-05c 行含"实例变量"） |
| 负责范围 | `Slice/SliceAuthorFrontend.cs`（`amount-source` 解析与校验）、`Slice/CombatSlicePackage.cs`（打出时求值链）、`BaseSpire.Slice.Tests/SliceFrontendAmountSourceChecks.cs`（新增 3 项） |
| 状态 | **部分完成（十四批）**：状态来源已交付；B-05a 记的"卡牌变量"里的**可变实例量**（被效果写回并记住的值）仍未做（见 §4） |
| 前置 | [批1](b-05c-frontend-batch1-2026-10-02.md) … [批13](b-05c-frontend-batch13-2026-10-02.md) |

## 1. 作者语法与降级

```
card test-tally {
  name = TestTally  kind = Attack  cost = 0
  normal   { amount-source = hand-count   steps = [target, damage] }
  upgraded { amount-source = hand-count   steps = [target, damage] }
}
```

- `amount-source = literal`（默认，用声明的 `amount`）| `hand-count` | `discard-count` | `draw-count`。
- 与固定 `amount` **互斥**（同时写 → `unsupported_combination`）；未知来源 → `invalid_value`。都带路径。
- 降级：出牌 body 在**效果链之前**按 (定义, 升级) 分派，把当前状态写进这张牌的 `Amount` 字段
  （效果读的就是该字段）。用的都是既有通用原语（`Count(Collection(...))` / `SetField`）。
- 程序化路径与冻结作者文本不受影响（字面量来源不生成任何指令）。

## 2. 退出证据

| 判据 | 检查 |
| --- | --- |
| 语法 | 三种来源逐条读出；未声明时是 `literal`；`amount` 与 `amount-source` 同时写、未知来源分别以 `unsupported_combination`/`invalid_value` 拒绝且路径指向该卡 |
| **求值在打出时** | `hand-count 在打出那一刻读取手牌数（同一张牌两种结果）[真实]`：同一张牌在多 4 张手牌的情况下多造成**正好 4** 点（伤害 = 打出那一刻的手牌数），证明数值不是构筑时烤进去的常量 |
| 另一来源 | `discard-count 读弃牌堆`：先造 3 张弃牌 → 造成正好 3 点 |
| 默认路径 | 冻结作者文本全部是字面量来源 |

## 3. 本轮修正的缺陷（检查侧）

**`PutInHand` 不能用来加手牌**：它会先在手牌里找同定义的牌并直接复用，所以"加 4 张手牌"其实一张也没加
（伤害两次都是 5）。改为用手牌堆→手牌的**移动**（`Relocate`）来改变手牌数；并把期望值写成
"打出前手牌数 − 1"（这张牌在效果解析前已离开手牌）。

## 4. B-05c 剩余子项（仍未完成）

**可变实例量**（效果把值写回这张牌并让后续效果读到——本批只做了"读状态"，没有"写回并记住"）；
生成/升级/变形/附魔；药水/run（事件/房间/奖励/地图）；hook 里更广的步骤面；
伤害上限阶段（cap，切片仍无等价机制）。

## 5. 运行结果（本轮实跑）

| 命令 | 返回码 | 结果 |
| --- | --- | --- |
| `dotnet build AutoTheSpire.sln` | 0 | 0 错误 0 警告 |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | 0 | **All 87 slice checks passed**（25 既有 + 4 B-05b + 58 项 B-05c） |
| `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` | 0 | 64 passed |
| `dotnet run --project core-the-spire/Core.TheSpire.Tests` | 0 | 190 passed / 0 failed |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | 0 | 383 passed（不回归） |
| `python ai-the-spire/tests/run_checks.py` | 0 | 503 passed（不回归） |
| `python ai-the-spire/tests/j01_episode.py` | 0 | 150 passed（不回归） |
| `python tools/contract-check/check_contracts.py` | 0 | 210 passed（不回归） |
| `python net-the-spire/client/test_client.py` | 0 | 50 passed（不回归） |

## 6. 修改文件

- `BaseSpire/Slice/SliceAuthorFrontend.cs`（`AmountSource` 词表、`amount-source` 解析与互斥校验）
- `BaseSpire/Slice/CombatSlicePackage.cs`（`AmountSourceChain` / `AmountFrom`：打出时求值）
- `BaseSpire.Slice.Tests/SliceFrontendAmountSourceChecks.cs`（新增 3 项）、`SliceChecks.cs`（注册）
- `docs/review/tofix/long-task-progress-2026-10-02.md`（状态）

未修改：`Engine/`、Legacy、net、Python 侧。未提交、未清理共享工作区。

## 7. 下一批建议

**B-05c 第十五批：可变实例量**（效果把值写回这张牌、后续效果读到；即 B-05a 记的 `variables` +
`instance.VariableOffsets` 的最小等价物）。它需要在作者文本里声明变量并允许效果写回，
属于对 `Engine/` 实体字段能力的直接使用（不需要新 IR 节点）。
