# B-05c：完整前端覆盖（第八批：多选，2026-10-02）

> 历史证据（2026-10-03整理）：正文保留原时点结果，历史“下一步”不再作为当前派单指令。本报告归属W-06、W-01，未结项转入[当前清单](../../tofix/README.md)；移入records不表示修复或验收通过。逐份归属见[记录索引](../README.md)。

| 项目 | 内容 |
| --- | --- |
| 任务 ID | **B-05c**（第八批）——**多选**（`top_attacks_from_discard xN`） |
| 权威 | [开工清单 §14](../../../design/implementation-kickoff.md#14-m1后阶段二sts作者前端与内容迁移)（B-05c 行含"选择"）；引擎纪律见 [core-the-spire/AGENTS.md](../../../../core-the-spire/AGENTS.md) |
| 负责范围 | `Engine/Ir.cs`、`Engine/IrTypeChecker.cs`、`Engine/Scheduler.cs`、`Engine/RuleSet.cs`（**新 IR 节点 `query.refList`**，4 处：声明/类型检查/求值/**转储**）、`Slice/SliceAuthorFrontend.cs`、`Slice/CombatSlicePackage.cs`、`BaseSpire.Slice.Tests/SliceFrontendMultiSelectChecks.cs`（新增 5 项） |
| 状态 | **部分完成（八批）**：多选已交付；B-05c 其余子项仍未开始（见 §5） |
| 前置 | [批1](b-05c-frontend-batch1-2026-10-02.md) … [批7](b-05c-frontend-batch7-2026-10-02.md) |

## 1. 【Engine 能力新增】`query.refList`——遍历引用列表槽

**为什么必须动 `Engine/`**：引擎已有 `DecisionResponseKind.EntityRefList`（多选应答），但 IR 里**没有**任何
查询能读回这个引用列表，于是"多选之后逐一消费"在作者层无法表达，只能由宿主展开。
按纪律（新机制一律落在 `Engine/`，缺能力先补引擎）先补通用能力，再开放作者语法。

| 位置 | 改动 |
| --- | --- |
| `Ir.cs` | 新节点 `RefListQuery(int listSlot) : Query`，`Primitive = "query.refList"` |
| `IrTypeChecker.cs` | 槽位必须是 `ListOf(Ref)`，元素类型取槽位的 `ElementTargetId`；否则**链接期**报 `SchemaInvalid` |
| `Scheduler.cs` | 求值：读槽位为引用列表，按**存入顺序**返回（多选的消费顺序＝回答顺序，不做额外排序） |
| `RuleSet.cs` | 转储器新增分支（`list[槽位]`）——该文件明确要求"新节点必须让转储器知道"，否则身份文本会漏语义 |

## 2. 作者语法与降级

```
normal { amount = 1  steps = [top_attacks_from_discard x2] }
```

- 候选集：弃牌堆里的**攻击牌**（沿用批 7 的 `Where` 过滤）。
- 一次 `Decide(EntityRefList, min = 1, max = 批次)`：**最多选 N 张**；候选为空时不提问。
- 消费：`ForEach(new RefListQuery(slot), each => 移除 + 置顶 + 记录已知)`。
- **批次归属**：多选步骤自己消费批次（批次＝最多选几张），因此**不再**被外层 `Repeat` 包住
  （其余步骤的批次仍是 `Repeat`）。

## 3. 退出证据

| 判据 | 检查 |
| --- | --- |
| 语法 | `top_attacks_from_discard x2` 被接受（步骤名 + 批次＝最多选几张）；未知步骤仍 `unknown_step` |
| 一次提问 + 条件过滤 | `一次提问最多两张、候选只有攻击牌... [真实]`：应答种类是 `EntityRefList`；候选含两张攻击牌、**不含**技能牌 |
| 每个引用恰好消费一次 | 同上：答 [A, B] 后两张都离开弃牌堆、都出现在抽牌堆前两位、**没有第二次提问**、技能牌未动 |
| 数量不足 | `候选不足时按可用数选`：只有一张攻击牌时仍提问，只选一张也能完成并置顶 |
| 候选为空 | 没有攻击牌时**不提问**，且没人被误移 |
| **转储器认识新节点** | `IrDumper.Dump(new RefListQuery(7))` 含 `list[7]`（直接对节点调用公开转储器） |
| **新节点参与身份** | 多选包与"条件选择单选"包的 `RuleSet.Identity` **必须不同**（语义变了身份不变是 K-01 明确要防的） |
| 默认路径不受影响 | 冻结作者文本没有多选步骤 |

## 4. 运行结果（本轮实跑）

| 命令 | 返回码 | 结果 |
| --- | --- | --- |
| `dotnet build AutoTheSpire.sln` | 0 | 0 错误 0 警告 |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | 0 | **All 66 slice checks passed**（25 既有 + 4 B-05b + 37 项 B-05c） |
| `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` | 0 | 64 passed（新 IR 节点未破坏既有类型检查/身份检查） |
| `dotnet run --project core-the-spire/Core.TheSpire.Tests` | 0 | 190 passed / 0 failed |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | 0 | 383 passed（不回归） |
| `python ai-the-spire/tests/run_checks.py` | 0 | 503 passed（不回归） |
| `python ai-the-spire/tests/j01_episode.py` | 0 | 150 passed（不回归） |
| `python tools/contract-check/check_contracts.py` | 0 | 210 passed（不回归） |
| `python net-the-spire/client/test_client.py` | 0 | 50 passed（不回归） |

## 5. B-05c 剩余子项（仍未完成）

保留时点（retain）；实例变量；生成/升级/变形/附魔；**Power/敌人的行动程序**（只作者化了数据）；
药水/run（事件/房间/奖励/地图）；hook 里更广的步骤面（`target`/`apply_power`/`top_from_discard`/`exhaust_self`）；
0.0.1 兼容入口（旧 `.spire` 与旧后端保持原样）。

## 6. 修改文件

- `core-the-spire/Core.TheSpire/Engine/Ir.cs`、`IrTypeChecker.cs`、`Scheduler.cs`、`RuleSet.cs`（新节点 + 三处教学 + 转储）
- `BaseSpire/Slice/SliceAuthorFrontend.cs`（`top_attacks_from_discard` 词表）
- `BaseSpire/Slice/CombatSlicePackage.cs`（多选降级、`discardSlots` 槽位、批次归属规则）
- `BaseSpire.Slice.Tests/SliceFrontendMultiSelectChecks.cs`（新增 5 项）、`SliceChecks.cs`（注册）
- `docs/review/tofix/long-task-progress-2026-10-02.md`（状态）

未提交、未清理共享工作区。

## 7. 下一批建议

**B-05c 第九批：Power/敌人的行动程序作者化**（当前只作者化了它们的数据，行动循环仍是程序化 IR）。
它把 B-05a 记的"敌人行动"从数据层推进到程序层，是剩余子项里离"内容迁移"最近的一项；
保留时点与实例变量可与之并行评估。
