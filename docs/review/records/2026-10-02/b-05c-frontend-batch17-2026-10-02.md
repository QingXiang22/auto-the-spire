# B-05c：完整前端覆盖（第十七批：hook 步骤面，2026-10-02）

> 历史证据（2026-10-03整理）：正文保留原时点结果，历史“下一步”不再作为当前派单指令。本报告归属W-06、W-01，未结项转入[当前清单](../../tofix/README.md)；移入records不表示修复或验收通过。逐份归属见[记录索引](../README.md)。

| 项目 | 内容 |
| --- | --- |
| 任务 ID | **B-05c**（第十七批）——hook 里更广的步骤面（`apply_power` 可用，其余按**理由**拒绝） |
| 权威 | [开工清单 §14](../../../design/implementation-kickoff.md#14-m1后阶段二sts作者前端与内容迁移)（B-05c 行含 hook/选择） |
| 负责范围 | `Slice/SliceAuthorFrontend.cs`（`HookSteps` 扩容 + `HookStepReason`）、`Slice/CombatSlicePackage.cs`（hook 里的目标是 self）、`BaseSpire.Slice.Tests/SliceFrontendHookStepChecks.cs`（新增 2 项） |
| 状态 | **部分完成（十七批）**：hook 步骤面已扩到 3 个并有逐条理由；B-05c 其余子项仍未开始（见 §4） |
| 前置 | [批1](b-05c-frontend-batch1-2026-10-02.md) … [批16](b-05c-frontend-batch16-2026-10-02.md) |

## 1. 本批交付

- **hook 允许面**：`damage_all` / `block` / **`apply_power`**（新增）。
- **hook 里 `apply_power` 的目标是"玩家自己"**（hook 的时点没有"当前目标"这回事）；出牌时的 `apply_power`
  仍作用于所选目标。这与 STS 的卡牌 hook（给自己上能力）一致。
- **其余步骤按具体理由拒绝**（`unsupported_capability` + 路径 + 理由文本），而不是笼统的"未支持"：

| 步骤 | 理由（写进错误消息） |
| --- | --- |
| `target` | hook 的时点不允许挂起决策；需要选目标请写进这张牌的效果步骤 |
| `damage` | hook 里没有可选的敌人目标；要对全体敌人造成伤害请用 `damage_all` |
| `top_from_discard` / `top_attack_from_discard` / `top_attacks_from_discard` | 不允许挂起决策；置顶/多选请写进牌的效果步骤 |
| `exhaust_self` | hook 是在这张牌**已经**被消耗之后才触发的，再声明消耗自己没有意义 |
| `upgrade_hand` | hook 里不做手牌升级 |
| `generate:<id>` | hook 里不生成新实例（生成会改变实例身份） |
| `becomes:<id>` | hook 里不改变实例身份 |

这些是**设计边界**（时点不允许挂起决策、hook 不改变实例身份），不是"尚未实现的缺口"——报告与错误消息都这样表述。

## 2. 退出证据

| 判据 | 检查 |
| --- | --- |
| 语法 | `apply_power` 在 hook 里被接受；允许面为 `damage_all / block / apply_power`；**7 类**被拒步骤各自带出对应理由片段（逐条断言理由文本与路径） |
| 运行 | `hook 里的 apply_power 把能力加在**玩家自己**身上 [真实]`：消耗触发 `on_exhaust` → **玩家**获得 2 层力量、**敌人没有**；紧接着的打击造成 3 + 2 = **5**（力量立刻参与伤害） |

## 3. 运行结果（本轮实跑）

| 命令 | 返回码 | 结果 |
| --- | --- | --- |
| `dotnet build AutoTheSpire.sln` | 0 | 0 错误 0 警告 |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | 0 | **All 98 slice checks passed**（25 既有 + 4 B-05b + 69 项 B-05c） |
| `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` | 0 | 64 passed |
| `dotnet run --project core-the-spire/Core.TheSpire.Tests` | 0 | 190 passed / 0 failed |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | 0 | 383 passed（不回归） |
| `python ai-the-spire/tests/run_checks.py` | 0 | 503 passed（不回归） |
| `python ai-the-spire/tests/j01_episode.py` | 0 | 150 passed（不回归） |
| `python tools/contract-check/check_contracts.py` | 0 | 210 passed（不回归） |
| `python net-the-spire/client/test_client.py` | 0 | 50 passed（不回归） |

## 4. B-05c 剩余子项（仍未完成）

**药水/run**（事件/房间/奖励/地图，整块内容域）；**伤害上限阶段**（cap，切片仍无等价机制）；
**生成时的随机选择**（`generate-random`，需声明候选集与随机流）；**变量的跨战斗/跨实例复制**语义；
以及 B-05c 行里属于"更大内容域"的部分（药水、run 事件）。

## 5. 修改文件

- `BaseSpire/Slice/SliceAuthorFrontend.cs`（`HookSteps` 扩到 3 个、`HookStepReason` 逐条理由）
- `BaseSpire/Slice/CombatSlicePackage.cs`（`HookInstructions` 把 `owner` 同时作为 attacker 与 target）
- `BaseSpire.Slice.Tests/SliceFrontendHookStepChecks.cs`（新增 2 项）、`SliceChecks.cs`（注册）
- `docs/review/tofix/long-task-progress-2026-10-02.md`（状态）

未修改：`Engine/`、Legacy、net、Python 侧。未提交、未清理共享工作区。

## 6. 下一批建议

**B-05c 第十八批：伤害上限阶段（cap）** 或 **药水/run 的范围界定**。
前者是 `SliceManifest` 已明确标注的缺口（伤害链的 cap 阶段没有等价机制），属于**补齐既有语义**；
后者是整块内容域，开工前需要先确定范围（哪些药水/哪些 run 流程算本轮目标），建议先出范围说明再动手。
