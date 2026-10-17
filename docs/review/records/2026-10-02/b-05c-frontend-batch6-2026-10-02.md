# B-05c：完整前端覆盖（第六批：批次与逐变体步骤，2026-10-02）

> 历史证据（2026-10-03整理）：正文保留原时点结果，历史“下一步”不再作为当前派单指令。本报告归属W-06、W-01，未结项转入[当前清单](../../tofix/README.md)；移入records不表示修复或验收通过。逐份归属见[记录索引](../README.md)。

| 项目 | 内容 |
| --- | --- |
| 任务 ID | **B-05c**（第六批）——效果批次（`damage x3` / `block x2`）与**逐变体步骤序列** |
| 权威 | [开工清单 §14](../../../design/implementation-kickoff.md#14-m1后阶段二sts作者前端与内容迁移)（B-05c 行含"批处理"） |
| 负责范围 | `Authoring/SliceAuthorFrontend.cs`（`AuthorStep`/批次解析/逐变体模型）、`Composition/CombatSlicePackage.cs`（`Repeat` 降级 + 按 (定义, 升级) 分派）、`BaseSpire.Slice.Tests/Cards/SliceFrontendBatchChecks.cs`（新增 5 项） |
| 状态 | **部分完成（六批）**：批次与逐变体已交付；**多选（`EntityRefList`）未做**，需要先补引擎能力（见 §5） |
| 前置 | [批1](b-05c-frontend-batch1-2026-10-02.md) … [批5](b-05c-frontend-batch5-2026-10-02.md) |

## 1. 本批交付

1. **批次语法**：`steps = [target, damage x3]`、`steps = [block x4]`（与卡组 `x5` 同一写法）。
   模型里是 `AuthorStep(Kind, Count)`，`ToString()` 回显 `damage x3`。
2. **通用 IR 降级**：一个步骤若降级成多条指令，批次用 `Repeat(Const(N), [.. single])` 包住**整组**
   （`damage_random` 这种"先抽样再打"的步骤也能整组重复）。
3. **批次规则**（都带路径）：
   - 只允许出现在可重复的效果步骤上：`target`（选 N 次目标语义不明）、`damage_all`（本身就是全员各一次）、
     `damage_random`（抽 N 次目标语义不明）、`exhaust_self`（一张牌只消耗一次）→ `unsupported_combination`；
   - `x0` / `x` / `x2 y3` → `invalid_value`。
4. **逐变体步骤序列（本批顺带解掉一条人为限制）**：此前"normal 与 upgraded 的步骤序列必须相同"是运行时按
   定义分派的副产品。现在运行时会按 **(定义, 升级)** 分派，因此两个形态可以有不同的步骤与批次，
   **消耗判定与 `on_exhaust` 也逐变体独立**（升级形态可以消耗、普通形态不消耗，反之亦然）。
   这不是放宽校验换绿灯：分派程序真的按 `Upgraded` 字段分支，检查分别断言两个形态。

## 2. 退出证据

| 判据 | 检查 |
| --- | --- |
| 语法 | `xN` 被解析为批次（`StepKinds` 与 `Count` 分别核对）；5 类非法写法分别以 `unsupported_combination`/`invalid_value` 拒绝且路径指向 `steps[...]` |
| 运行（伤害批次） | `damage x3` 在同一目标上打三次：普通形态 `3 × 2 = 6`、升级形态 `3 × 3 = 9`；**对照**：去掉批次（等差值）只打一次（差额确实来自批次） |
| 运行（格挡批次 + 不动随机） | `block x4` 加 4 点；`Random.TotalDraws` 不变；随机目标流未被创建（`Repeat` 不是随机点） |
| 运行（hook 里的批次） | `hook on_exhaust { steps = [block x3] }` 按 amount 2 加三次 = 6 点 |
| 逐变体分派 | 同一张卡的普通/升级形态使用不同批次（`block x4` vs `block x3`）并通过检查；hook 的 `Count` 逐变体读出 |
| 冻结文本不受影响 | 冻结作者文本里每个步骤批次都是 1、无 hook |

## 3. 本轮修正的缺陷（都在我自己的实现里）

1. **多指令步骤被截断**：批次重构时我把 `lowered.Add(single[0])` 写成只取第一条指令，于是 `damage_random`
   （抽样 + 伤害两条）丢掉伤害。既有检查立刻以"恰好一名敌人受到 5 点"失败抓到——**重构后立即跑既有检查的价值**。
2. **词法器把 `damage x3` 拆成两项**：`[...]` 列表按**逗号**分隔项，项内多词要用空格连接；
   改为按逗号 flush 项，`damage x3` 才是一个项。
3. **金丝雀断言写错（自造限制）**：批次场景最初给普通/升级写了不同批次，被"两变体必须一致"拒绝。
   正确做法不是把场景改弱，而是把这条**人为限制解掉**（见 §1.4）——检查因此更强而不是更弱。

## 4. 运行结果（本轮实跑）

| 命令 | 返回码 | 结果 |
| --- | --- | --- |
| `dotnet build AutoTheSpire.sln` | 0 | 0 错误 0 警告 |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | 0 | **All 56 slice checks passed**（25 既有 + 4 B-05b + 27 项 B-05c） |
| `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` | 0 | 64 passed |
| `dotnet run --project core-the-spire/Core.TheSpire.Tests` | 0 | 190 passed / 0 failed |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | 0 | 383 passed（不回归） |
| `python ai-the-spire/tests/run_checks.py` | 0 | 503 passed（不回归） |
| `python net-the-spire/client/test_client.py` | 0 | 50 passed（不回归） |
| `python tools/contract-check/check_contracts.py` | 0 | 210 passed（不回归） |

## 5. B-05c 剩余子项（仍未完成）

**多选选择**（引擎有 `DecisionResponseKind.EntityRefList`，但没有"对引用列表结果做查询/遍历"的原语，
因此"从弃牌堆选 2 张"这类内容要么拆成两次单选，要么先给 `Engine/` 补一个 ref-list 查询）；
条件选择（按条件筛选候选）；保留时点（retain）；实例变量；生成/升级/变形/附魔；
**Power/敌人的行动程序**（只作者化了数据）；药水/run（事件/房间/奖励/地图）；
hook 里更广的步骤面（`target`/`apply_power`/`top_from_discard`/`exhaust_self`）；0.0.1 兼容入口。

## 6. 修改文件

- `BaseSpire/Authoring/SliceAuthorFrontend.cs`（`AuthorStep`、批次解析与规则、逐变体步骤、`NormalExhaustsSelf`/`UpgradedExhaustsSelf`）
- `BaseSpire/Composition/CombatSlicePackage.cs`（`Repeat` 批次降级、按 (定义, 升级) 分派效果与结果区）
- `BaseSpire.Slice.Tests/Cards/SliceFrontendBatchChecks.cs`（新增 5 项）、既有 5 个前端检查文件改为按步骤名/逐变体断言
- `docs/review/tofix/long-task-progress-2026-10-02.md`（状态）

未修改：`Engine/`（本批只用既有 `Repeat`/`IfElse` 原语）、Legacy、net、Python 侧。未提交、未清理共享工作区。

## 7. 下一批建议

**B-05c 第七批：多选与条件选择**。多选需要先在 `Engine/` 补"引用列表结果可被查询/遍历"的通用能力
（`DecisionResponseKind.EntityRefList` 已经存在，缺的是消费端），再开放作者语法 `choose_from_discard xN`；
条件选择可直接复用既有的 `Where`/`Sort`/`Take` 查询原语，成本更低，可先做。
