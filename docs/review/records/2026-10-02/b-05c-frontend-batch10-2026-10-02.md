# B-05c：完整前端覆盖（第十批：保留时点，2026-10-02）

> 历史证据（2026-10-03整理）：正文保留原时点结果，历史“下一步”不再作为当前派单指令。本报告归属W-06、W-01，未结项转入[当前清单](../../tofix/README.md)；移入records不表示修复或验收通过。逐份归属见[记录索引](../README.md)。

| 项目 | 内容 |
| --- | --- |
| 任务 ID | **B-05c**（第十批）——**保留时点（retain）** |
| 权威 | [开工清单 §14](../../../design/implementation-kickoff.md#14-m1后阶段二sts作者前端与内容迁移)（B-05c 行含"保留时点有独立检查"） |
| 负责范围 | `Slice/SliceAuthorFrontend.cs`（`retain` 解析与逐变体模型）、`Slice/CombatSlicePackage.cs`（端末取舍程序）、`BaseSpire.Slice.Tests/SliceFrontendRetainChecks.cs`（新增 4 项） |
| 状态 | **部分完成（十批）**：保留时点已交付；B-05c 其余子项仍未开始（见 §5） |
| 前置 | [批1](b-05c-frontend-batch1-2026-10-02.md) … [批9](b-05c-frontend-batch9-2026-10-02.md) |

## 1. 作者语法与降级

```
card test-guard {
  name = TestGuard  kind = Skill  cost = 0
  normal   { amount = 3  retain = true   steps = [block] }
  upgraded { amount = 3  retain = false  steps = [block] }
}
```

- `retain = true|false` 是**逐变体**的牌面属性（不是效果步骤），非法值以 `invalid_value` 带路径拒绝。
- 降级：端末的手牌循环改为"先跑 `turn_end_in_hand` hook，再按 `retain` 决定留下还是进弃牌堆"；
  取舍程序按 **(定义, 升级)** 分派，因此两个形态可以不同。
- **程序化路径逐字不变**：那条路径仍是"移除手牌 + 进弃牌堆"，没有多出任何分支。
- 时点关系：`turn_end_in_hand` 的触发条件是"回合结束时仍在手牌"，因此**保留的牌照样触发**（先触发、后决定去留）。

## 2. 退出证据

| 判据 | 检查 |
| --- | --- |
| 语法 | `retain` 逐变体解析（普通 true / 升级 false / 未声明为 false）；`retain = yes` 以 `invalid_value` 拒绝且路径含 `retain` |
| 运行（保留） | `普通形态留在手牌、升级形态照常弃掉 [真实]`：普通形态端末后仍在手牌且**不在弃牌堆**；升级形态离开手牌且**在弃牌堆**（同一定义、两种形态，判定确实逐变体） |
| 保留仍有端末时点 | `保留的牌仍然触发 turn_end_in_hand（时点没变）[真实]`：保留牌在端末造成声明的伤害、**没有被移走**，且**连续两个回合各触发一次** |
| 默认路径 | `冻结作者文本没有 retain`：冻结文本无 retain；程序化路径端末前的整手牌全部离开手牌并进弃牌堆（行为逐字不变） |

## 3. 本轮修正的缺陷（都是检查本身的问题）

1. **"手牌为空"是错断言**：端末之后新回合会**重抽一手牌**，所以端末后手牌非空是正常的。改为跟踪**端末前那几个实例**
   是否全部离开手牌并进弃牌堆。
2. **重洗会把弃牌搬回手牌**：第一个断言用"在不在手牌"判别非保留牌，但卡组太小时重洗会把刚弃掉的牌再抽回手里，
   造成假失败。把场景卡组加大到 10 张打击（一次端末不触发重洗）后，"在手牌/在弃牌堆"才是可判别的。

## 4. 运行结果（本轮实跑）

| 命令 | 返回码 | 结果 |
| --- | --- | --- |
| `dotnet build AutoTheSpire.sln` | 0 | 0 错误 0 警告 |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | 0 | **All 73 slice checks passed**（25 既有 + 4 B-05b + 44 项 B-05c） |
| `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` | 0 | 64 passed |
| `dotnet run --project core-the-spire/Core.TheSpire.Tests` | 0 | 190 passed / 0 failed |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | 0 | 383 passed（不回归） |
| `python ai-the-spire/tests/run_checks.py` | 0 | 503 passed（不回归） |
| `python ai-the-spire/tests/j01_episode.py` | 0 | 150 passed（不回归） |
| `python tools/contract-check/check_contracts.py` | 0 | 210 passed（不回归） |
| `python net-the-spire/client/test_client.py` | 0 | 50 passed（不回归） |

## 5. B-05c 剩余子项（仍未完成）

**Power 的行为程序**（events/lifecycle 仍由程序化程序处理；作者文本只声明其数据）；
实例变量（卡牌成长值）；生成/升级/变形/附魔；药水/run（事件/房间/奖励/地图）；
hook 里更广的步骤面（`target`/`apply_power`/`top_from_discard`/`exhaust_self`）；0.0.1 兼容入口。

## 6. 修改文件

- `BaseSpire/Slice/SliceAuthorFrontend.cs`（`retain` 键、`NormalRetain`/`UpgradedRetain`、`ParseRetain`）
- `BaseSpire/Slice/CombatSlicePackage.cs`（`EndTurnHandSteps` 与 `KeepOrDiscard`；程序化路径保持原样）
- `BaseSpire.Slice.Tests/SliceFrontendRetainChecks.cs`（新增 4 项）、`SliceChecks.cs`（注册）
- `docs/review/tofix/long-task-progress-2026-10-02.md`（状态）

未修改：`Engine/`、Legacy、net、Python 侧。未提交、未清理共享工作区。

## 7. 下一批建议

**B-05c 第十一批：Power 的行为程序作者化**（把 Power 的 events/lifecycle 从程序化程序迁到作者文本，
与第九批的敌人行动程序对称）。它需要先确认运行时对 Power 触发点的表达能力，缺则先在 `Engine/` 补；
实例变量与"生成/升级/变形"可随后评估。
