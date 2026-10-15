# B-05c：完整前端覆盖（第五批：随机目标与候选顺序来源，2026-10-02）

> 历史证据（2026-10-03整理）：正文保留原时点结果，历史“下一步”不再作为当前派单指令。本报告归属W-06、W-01，未结项转入[当前清单](../../tofix/README.md)；移入records不表示修复或验收通过。逐份归属见[记录索引](../README.md)。

| 项目 | 内容 |
| --- | --- |
| 任务 ID | **B-05c**（第五批）——目标域 `damage_random`，以及 §14 明文要求的"随机候选顺序和消费点有来源" |
| 权威 | [开工清单 §14](../../../design/implementation-kickoff.md#14-m1后阶段二sts作者前端与内容迁移) |
| 负责范围 | `Engine/Scheduler.cs` + `Engine/RandomService.cs`（**公共契约修正**）、`Slice/SliceAuthorFrontend.cs`、`Slice/CombatSlicePackage.cs`、`Slice/SliceManifest.cs`、`BaseSpire.Slice.Tests/SliceFrontendRandomChecks.cs`、`Core.TheSpire.Engine.Tests/EngineChecks.cs` |
| 状态 | **部分完成（五批）**：随机目标与候选顺序/消费点来源已交付；B-05c 其余子项仍未开始（见 §6） |
| 前置 | [批1](b-05c-frontend-batch1-2026-10-02.md) … [批4](b-05c-frontend-batch4-2026-10-02.md) |

## 1. 【公共契约变更】`inst.samplePick` 现在按公开实例标识排序候选

**旧行为**：`inst.samplePick` 直接把查询结果交给 `RandomService.DrawPick`，因此采样结果取决于**查询枚举顺序**——
对 `Collection(...)` 而言就是集合的**成员顺序**（受插入/移动历史影响）。

**新行为**（本批修正）：`Scheduler.cs` 在采样前把候选按**公开实例标识升序**排序（与公开快照列举成员用的是同一把尺：
`Projection.cs:111/116` 的 `OrderBy(member => member.Value)`）。于是采样只取决于候选**集合**，
不取决于成员顺序、插入顺序或查询枚举顺序。要做"顺序相关"的随机请用 `ShuffleCollection`（未改动）。

**为什么改**：`RandomService.DrawPick` 的文档字符串**已经声称**"the IR sorts candidates by public semantic key
before sampling"，但实现里没有这一步——文档与实现不一致。本批把实现补成文档所说的行为（`Engine/` 是
新机制的唯一落点，符合 `core-the-spire/AGENTS.md` 的约定）。

**影响面**：`inst.samplePick` 在此之前**没有任何调用者**（全仓库检索：只有声明、类型检查、转储与本次新增的切片用法），
因此没有既有内容的行为被改变；`RandomService.DrawPick` 的签名与语义（按调用者给的顺序抽）不变，只更新了文档。

**消费点与顺序来源（本切片）**：

| 事实 | 来源 |
| --- | --- |
| 流名 | `SliceManifest.RandomTargetStream = "base-spire:combat-random-target"`（与重洗流 `base-spire:combat-draw` 分开） |
| 每次消费次数 | 每次 `damage_random` **恰好一次**（`RandomService.DrawCount` 可核对） |
| 候选顺序 | 引擎按公开实例标识升序（上表的修正），**不是**集合成员顺序 |

## 2. 作者语法与降级

- 新步骤 `damage_random`：从**全部敌人**里抽一个作为目标并造成声明数值的伤害；**不需要** `target` 步骤。
- 目标确定方式必须唯一：`target` / `damage_all` / `damage_random` 三者中出现两个及以上 →
  `unsupported_combination`（带路径）。未知步骤仍 `unknown_step`。
- 降级复用共享的步骤降级：`SamplePick(流名, Collection(monsters), slot)` + `Call(damageMonster, ..., slot, Amount)`。

## 3. 退出证据

| 判据 | 检查 |
| --- | --- |
| 语法 | `damage_random` 被接受；`target + damage_random`、`damage_random + damage_all`、`damage_any` 分别以 `unsupported_combination`/`unknown_step` 拒绝且路径指向该卡 |
| **消费点有来源** | `消费点有来源（每次恰好一次命名流，且不动重洗流）[真实]`：开局该流还不存在（流在首次消费时创建）；一次打出后 `DrawCount == 1`、重洗流计数不变；第二次打出后为 2；`Diagnostics()` 的抽取次数与之一致且状态指纹非零 |
| **候选顺序有来源** | `候选顺序不是插入顺序（倒转集合成员后同种子仍选同一实例）[真实]`（切片）与 `K-05 inst.samplePick samples the candidate SET, not the collection member order`（引擎）：同一实例集合、同一流、同一种子，**倒转集合成员顺序**后仍采到**同一个实例**；且恰好一名敌人受伤（不是全体） |
| 随机真的被消费 | `不同种子会选出不同目标 [真实]`：24 个种子两个目标都抽到过；引擎检查里 12 个种子出现多于一种结果 |
| 默认路径不受影响 | `冻结作者文本没有随机目标牌`：冻结文本无 `damage_random`；程序化路径不创建随机目标流（其程序里没有 `SamplePick`） |

## 4. 本轮修正/发现的缺陷

1. **引擎文档与实现不一致（真实缺陷）**：`DrawPick` 的文档声称 IR 会先按公开语义键排序，实现没有排序，
   采样结果因此依赖集合成员顺序。已按文档修正实现，并在**引擎**里加了行为检查（倒转成员顺序 → 同一实例）。
2. **检查假设了流的创建时机**：我最初断言"重洗流已由抽牌创建"，但流是**首次消费时**才创建的 →
   断言改为"还没消费时该流尚未创建"。
3. **测试场景牌数不足**：`PutInHand` 第二次取同一张牌时牌已被打出（在弃牌堆），报
   `no 'base-spire:test-volley' card in the source area`；场景卡组改为 `test-volley x2`。

## 5. 运行结果（本轮实跑）

| 命令 | 返回码 | 结果 |
| --- | --- | --- |
| `dotnet build AutoTheSpire.sln` | 0 | 0 错误 0 警告 |
| `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` | 0 | **All 64 engine checks passed**（+1 项 samplePick 顺序检查） |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | 0 | **All 51 slice checks passed**（25 既有 + 4 B-05b + 22 项 B-05c） |
| `dotnet run --project core-the-spire/Core.TheSpire.Tests` | 0 | 190 passed / 0 failed |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | 0 | 383 passed（不回归） |
| `python ai-the-spire/tests/run_checks.py` | 0 | 503 passed（不回归） |
| `python ai-the-spire/tests/j01_episode.py` | 0 | 150 passed（不回归） |
| `python tools/contract-check/check_contracts.py` | 0 | 210 passed（不回归） |
| `python net-the-spire/client/test_client.py` | 0 | 50 passed（不回归） |

## 6. B-05c 剩余子项（仍未完成）

选择（多选/条件选择）与**批处理**（`count`、重复、按变量次数）、保留时点（retain）、实例变量、
生成/升级/变形/附魔、**Power/敌人的行动程序**（只作者化了数据）、药水/run（事件/房间/奖励/地图）、
hook 里更广的步骤面（`target`/`apply_power`/`top_from_discard`/`exhaust_self`）、0.0.1 兼容入口。
B-05a 的分母（354 条声明 / 85 张卡池卡）不变。

## 7. 修改文件

- `core-the-spire/Core.TheSpire/Engine/Scheduler.cs`（`SamplePick` 候选排序 + 注释）
- `core-the-spire/Core.TheSpire/Engine/RandomService.cs`（`DrawPick` 文档改为与实现一致）
- `core-the-spire/Core.TheSpire.Engine.Tests/EngineChecks.cs`（新增 K-05 顺序检查）
- `BaseSpire/Slice/SliceManifest.cs`（`RandomTargetStream` 常量与出处注释）
- `BaseSpire/Slice/SliceAuthorFrontend.cs`（`damage_random` 词表 + 目标确定方式唯一性）
- `BaseSpire/Slice/CombatSlicePackage.cs`（`damage_random` 降级：`SamplePick` + 伤害）
- `BaseSpire.Slice.Tests/SliceFrontendRandomChecks.cs`（新增 5 项）、`SliceChecks.cs`（注册）
- `docs/review/tofix/long-task-progress-2026-10-02.md`（状态）

未提交、未清理共享工作区。

## 8. 下一批建议

**B-05c 第六批：选择与批处理**（多选/条件选择、`count` 与按变量次数的重复）。
它需要先补通用能力（`Decision` 的多选与重复指令），按纪律先在 `Engine/` 补齐再开放语法；
随机相关部分已在批 5 完成，批处理里的随机顺序可直接复用同一条命名流规则。
