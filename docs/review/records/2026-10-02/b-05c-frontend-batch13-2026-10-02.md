# B-05c：完整前端覆盖（第十三批：0.0.1 兼容入口，2026-10-02）

> 历史证据（2026-10-03整理）：正文保留原时点结果，历史“下一步”不再作为当前派单指令。本报告归属W-06、W-01，未结项转入[当前清单](../../tofix/README.md)；移入records不表示修复或验收通过。逐份归属见[记录索引](../README.md)。

| 项目 | 内容 |
| --- | --- |
| 任务 ID | **B-05c**（第十三批）——**保留 0.0.1 兼容入口**（开工清单 §14 B-05c 行的明文要求）的显式验证 |
| 权威 | [开工清单 §14](../../../design/implementation-kickoff.md#14-m1后阶段二sts作者前端与内容迁移)、[AGENTS.md](../../../../AGENTS.md)（新旧语法并存约定） |
| 负责范围 | `BaseSpire.Slice.Tests/SliceFrontendCompatibilityChecks.cs`（新增 4 项） |
| 状态 | **部分完成（十三批）**：兼容入口已验证；B-05c 其余子项仍未开始（见 §4） |
| 前置 | [批1](b-05c-frontend-batch1-2026-10-02.md) … [批12](b-05c-frontend-batch12-2026-10-02.md) |

## 1. 两套作者前端的分工（本批把它固化成检查）

| | 旧 0.0.1（冻结） | 切片作者文本（本轮新增） |
| --- | --- | --- |
| 落点 | `Core.TheSpire/Legacy` + `ContentSource/*.spire` | `Slice/Content/*.author` → 通用 `Engine/` IR |
| 解析器 | `DslSourceParser` / `SpireCombatDslCompiler` | `SliceAuthorFrontend` |
| 规模 | 17 个内容源、96 张卡、89 个敌人（可注册） | 4 张卡、4 个能力、2 个敌人 |
| 扩展名/资源名 | `BaseSpire.ContentSource.*.spire` | `BaseSpire.SliceAuthor.*.author` |

## 2. 退出证据

| 判据 | 检查 |
| --- | --- |
| 旧内容仍能加载注册 | `旧 0.0.1 内容仍能加载并注册出原版内容`：`BaseSpireContent.LoadCards()` = **96**、卡池 1、`BaseSpirePackage` 注册后卡 96 / 敌人 89；代表性核验（打击 费用 1、类型 Attack、有升级形态） |
| 新前端仍能加载 | `切片作者文本仍能加载（两套前端同进程共存）`：**先跑旧后端**再加载作者文本（4 卡/2 敌人/版本），并用它建包、开局成功 |
| **互不吞掉对方语法** | `新前端不吞旧语法（带路径拒绝），旧解析器读得懂旧语法`：**逐字取自冻结内容源**的 `Dazed` 片段被旧解析器编译出 1 张卡；同一片段被新前端以带错误码+路径的错误**拒绝**；反向也成立（旧解析器拒绝切片作者文本） |
| 落点不重叠 | `两套前端读的是不同文件`：资源名零交集；旧的全是 `.spire`，新的是 `.author` |

> 兼容性结论基于**真实存在的旧语法**（`using namespace`、`card X { normal = { target = { selectSelf = {} } keywords = { … } hooks = {} } }`），
> 而不是自造片段——第一次我自造的写法连旧解析器都不接受（`Expected a declaration or property name`），
> 这本身说明"凭印象写旧语法"不可靠。

## 3. 运行结果（本轮实跑）

| 命令 | 返回码 | 结果 |
| --- | --- | --- |
| `dotnet build AutoTheSpire.sln` | 0 | 0 错误 0 警告 |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | 0 | **All 84 slice checks passed**（25 既有 + 4 B-05b + 55 项 B-05c） |
| `dotnet run --project core-the-spire/Core.TheSpire.Tests` | 0 | 190 passed / 0 failed（旧后端不回归） |
| `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` | 0 | 64 passed |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | 0 | 383 passed（不回归） |
| `python ai-the-spire/tests/run_checks.py` | 0 | 503 passed（不回归） |
| `python ai-the-spire/tests/j01_episode.py` | 0 | 150 passed（不回归） |
| `python tools/contract-check/check_contracts.py` | 0 | 210 passed（不回归） |
| `python net-the-spire/client/test_client.py` | 0 | 50 passed（不回归） |

## 4. B-05c 剩余子项（仍未完成）

实例变量（卡牌成长值）；生成/升级/变形/附魔；药水/run（事件/房间/奖励/地图）；
hook 里更广的步骤面（`target`/`apply_power`/`top_from_discard`/`exhaust_self`）；
伤害上限阶段（cap，切片仍无等价机制）。

## 5. 修改文件

- `BaseSpire.Slice.Tests/SliceFrontendCompatibilityChecks.cs`（新增 4 项）、`SliceChecks.cs`（注册）
- `docs/review/tofix/long-task-progress-2026-10-02.md`（状态）

未修改：任何生产代码（本批只加验证）。未提交、未清理共享工作区。

## 6. 下一批建议

**B-05c 第十四批：实例变量（卡牌成长值）**。它把 B-05a 记录的"卡牌变量"面（`variables`/`upgraded`）带入作者文本，
需要先确认运行时是否已有"实例变量"的载体（引擎的实体字段或计算资产），缺则先在 `Engine/` 补。
