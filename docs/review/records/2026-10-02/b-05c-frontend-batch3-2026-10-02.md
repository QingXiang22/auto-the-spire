# B-05c：完整前端覆盖（第三批：出牌期 hook，2026-10-02）

> 历史证据（2026-10-03整理）：正文保留原时点结果，历史“下一步”不再作为当前派单指令。本报告归属W-06、W-01，未结项转入[当前清单](../../tofix/README.md)；移入records不表示修复或验收通过。逐份归属见[记录索引](../README.md)。

| 项目 | 内容 |
| --- | --- |
| 任务 ID | **B-05c**（第三批）——`on_enter_combat` / `before_card_played` / `after_card_played` |
| 权威 | [开工清单 §14](../../../design/implementation-kickoff.md#14-m1后阶段二sts作者前端与内容迁移) |
| 负责范围 | `Slice/SliceAuthorFrontend.cs`（hook 词表 5 个）、`Slice/CombatSlicePackage.cs`（三个新触发点）、`BaseSpire.Slice.Tests/SliceFrontendPlayHookChecks.cs`（新增 4 项检查） |
| 状态 | **部分完成（三批）**：本批把 hook 面补全到 5 个；B-05c 其余子项仍未开始（见 §4） |
| 前置 | [批1](b-05c-frontend-batch1-2026-10-02.md)、[批2](b-05c-frontend-batch2-2026-10-02.md) |

## 1. 本批交付

hook 词表现在共 **5** 个，全部"运行时会真的触发"：

| hook | 触发点（本切片） | 触发次数 |
| --- | --- | --- |
| `on_enter_combat` | 开局创建这张牌之后 | 每张牌一次 |
| `before_card_played` | 这张牌被打出、进入 in-play **之后**、它自己的效果**之前** | 每次打出 |
| `after_card_played` | 这张牌自己的效果**之后**、进结果区**之前** | 每次打出 |
| `on_exhaust` | 因 `exhaust_self` 进入消耗区时 | 每次消耗 |
| `turn_end_in_hand` | 回合结束时仍在手牌（丢弃之前） | 每回合 |

三个新触发点**复用批次 2 的共享步骤降级**（`StepInstructions`），因此 hook 里的步骤语义与出牌时完全一致；
hook 步骤面仍是 `damage_all` / `block`（不需要目标选择与弃牌槽位），超出能力在编译期以
`unsupported_capability` 拒绝。

`before/after_card_played` 的实现细节：链只对**声明了该 hook 的卡**生成分支（其余卡直接跳到下一个候选），
升级变体与普通变体各自取自己的 hook 列表 —— 继续遵守"**升级空 hooks 不隐式继承**"。

## 2. 退出证据

| 判据 | 检查 |
| --- | --- |
| 语法 / 逐变体独立 | `B-05c 三个出牌期 hook：语法与逐变体独立性`：`on_enter_combat` 只有普通变体声明→升级变体 hooks 为空；`before_card_played` 两个变体各自声明；`after_card_played` 正确落位；未知 hook 以 `unknown_hook`、超能力步骤以 `unsupported_capability` 拒绝（均带路径） |
| 运行（开局） | `on_enter_combat：开局按声明数值触发一次 [真实]`：**任何出牌之前**两名敌人各少 3；对照组（冻结作者文本，无该 hook）开局血量不变 |
| 运行（出牌前后） | `before_card_played 与 after_card_played 在该牌效果前后各触发一次 [真实]`：`before` 卡打出后所选目标受"自身 2 + hook 2"、后排只受 hook 的 2（**触发一次**）；`after` 卡升级形态所选目标受"自身 7 + hook 7"、后排只受 7 |
| 冻结数据不受影响 | `三个新 hook 在冻结作者文本里都不存在`：4 张冻结卡 hooks 全为空；程序化路径与作者路径的受控开局血量一致 |

## 3. 运行结果（本轮实跑）

| 命令 | 返回码 | 结果 |
| --- | --- | --- |
| `dotnet build AutoTheSpire.sln` | 0 | 0 错误 0 警告 |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | 0 | **All 42 slice checks passed**（25 既有 + 4 B-05b + 4 批1 + 5 批2 + 4 批3） |
| `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` | 0 | 63 passed |
| `dotnet run --project core-the-spire/Core.TheSpire.Tests` | 0 | 190 passed / 0 failed |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | 0 | 383 passed（不回归） |
| `python ai-the-spire/tests/run_checks.py` | 0 | 503 passed（不回归） |
| `python net-the-spire/client/test_client.py` | 0 | 50 passed（不回归） |
| `python tools/doc-check/check_docs.py .` | 0 | 0 problem(s) |

## 4. 本轮修正的缺陷

**基线取错**：`on_enter_combat` 检查用**冻结清单**的血量（38）当基线，而测试场景文本声明的是 44，
于是断言 `expected 35, got 41` 失败——实际上 hook 正常触发了（44−3=41）。改为从**本场景模型**取基线
（`model.Enemies[...].NormalMinHp`）。这条值得记：测试场景文本与冻结清单是两份数据，跨用会制造假失败
（也是"检查必须区分预期与实现"的一个具体例子）。

## 5. B-05c 剩余子项（仍未完成）

X 费（捕获时点与 `cost = X` 语法）、选择/批处理与**随机候选顺序来源**、实例变量、生成/升级/变形/附魔、
**Power/敌人的行动程序**（当前只作者化了数据）、药水/run（事件/房间/奖励/地图）、
hook 里更广的步骤面（`target`/`apply_power`/`top_from_discard`/`exhaust_self`）、0.0.1 兼容入口。
B-05a 的分母（354 条声明 / 85 张卡池卡）不变：作者路径覆盖的仍是 4 张冻结卡 + 检查内的测试场景卡。

## 6. 修改文件

- `BaseSpire/Slice/SliceAuthorFrontend.cs`（hook 词表 5 个；`AuthorHooks` 增 3 个字段）
- `BaseSpire/Slice/CombatSlicePackage.cs`（`EnterCombatChain`、`PlayedHookChain`；出牌 body 与 setup 的接线）
- `BaseSpire.Slice.Tests/SliceFrontendPlayHookChecks.cs`（新增 4 项）、`SliceChecks.cs`（注册）
- `docs/review/tofix/long-task-progress-2026-10-02.md`（状态）

未修改：`Engine/` 语义、Legacy、net、Python 侧。未提交、未清理共享工作区。

## 7. 下一批建议

**B-05c 第四批：X 费（`cost = X` 与捕获时点）**。它需要先补通用能力：当前引擎有
`CostKind=X`/`HasEnergyXCost` 的概念，但切片没有"打出时捕获剩余能量"的程序；按纪律先在通用运行时补齐，
再开放作者语法，并同时补"X 费捕获"的独立检查（这是 §14 B-05c 行的明文要求）。
