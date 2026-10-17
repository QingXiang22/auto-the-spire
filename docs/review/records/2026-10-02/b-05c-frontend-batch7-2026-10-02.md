# B-05c：完整前端覆盖（第七批：条件选择，2026-10-02）

> 历史证据（2026-10-03整理）：正文保留原时点结果，历史“下一步”不再作为当前派单指令。本报告归属W-06、W-01，未结项转入[当前清单](../../tofix/README.md)；移入records不表示修复或验收通过。逐份归属见[记录索引](../README.md)。

| 项目 | 内容 |
| --- | --- |
| 任务 ID | **B-05c**（第七批）——**条件选择**（用既有查询原语表达） |
| 权威 | [开工清单 §14](../../../design/implementation-kickoff.md#14-m1后阶段二sts作者前端与内容迁移)（B-05c 行含"选择"） |
| 负责范围 | `Authoring/SliceAuthorFrontend.cs`（两个新步骤）、`Composition/CombatSlicePackage.cs`（`Sort`/`Where` 降级）、`BaseSpire.Slice.Tests/Cards/SliceFrontendConditionalChecks.cs`（新增 5 项） |
| 状态 | **部分完成（七批）**：条件选择已交付；**多选（`EntityRefList` 消费端）仍未做**（需要先补 `Engine/`，见 §5） |
| 前置 | [批1](b-05c-frontend-batch1-2026-10-02.md) … [批6](b-05c-frontend-batch6-2026-10-02.md) |

## 1. 本批交付（只用既有原语，没有改 `Engine/`）

| 步骤 | 含义 | 降级用到的原语 |
| --- | --- | --- |
| `damage_lowest` | **自动**挑当前生命最低的敌人并造成伤害（不提问） | `Sort(Collection(monsters), key = Hp)` + `First` + `SetLocal` + `Call(damageMonster)` |
| `top_attack_from_discard` | 只把弃牌堆里的**攻击牌**作为候选，选一张置顶 | `Where(Collection(discardPile), Kind == Attack)` + `Count` + `Decide` + 集合移动 |

- `damage_lowest` 是一种**目标确定方式**，与 `target`/`damage_all`/`damage_random` 互斥（同时出现 →
  `unsupported_combination`，带路径）。
- 两个步骤都不引入新的 IR 节点；候选顺序与并列判定沿用引擎既有的稳定规则（按公开实例标识排序）。

## 2. 退出证据

| 判据 | 检查 |
| --- | --- |
| 语法 | `damage_lowest` 被接受；`target + damage_lowest`、`damage_lowest + damage_all` 以 `unsupported_combination` 拒绝且路径指向该卡 |
| 条件选择的语义 | `damage_lowest 命中的是生命最低者，不是第一个成员 [真实]`：场景刻意让**先声明**的敌人生命更高（90 vs 40），结果后声明者受伤、前者未受伤；随后把先声明者的生命改成 10 再打一次，**改打它**（40 血的那位这次未受伤）——证明选择跟随**状态**而不是固定在某个实例上 |
| 不做无谓提问 | 同上：`damage_lowest` 打出后会话直接回到 `Running`，没有决策挂起 |
| 候选被条件过滤 | `top_attack_from_discard 的候选只有攻击牌 [真实]`：弃牌堆里放一张攻击牌 + 一张技能牌，决策候选**包含攻击牌、不含技能牌**；答"攻击牌"后它被置顶到抽牌堆第 0 位，技能牌仍在弃牌堆 |
| 空候选不提问 | `没有攻击牌可弃时不提问 [真实]`：弃牌堆只有技能牌时直接 `Running`，且没有人被误移 |
| 默认路径不受影响 | 冻结作者文本没有这两个步骤；程序化路径受控开局仍成立 |

## 3. 运行结果（本轮实跑）

| 命令 | 返回码 | 结果 |
| --- | --- | --- |
| `dotnet build AutoTheSpire.sln` | 0 | 0 错误 0 警告 |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | 0 | **All 61 slice checks passed**（25 既有 + 4 B-05b + 32 项 B-05c） |
| `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` | 0 | 64 passed |
| `dotnet run --project core-the-spire/Core.TheSpire.Tests` | 0 | 190 passed / 0 failed |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | 0 | 383 passed（不回归） |
| `python ai-the-spire/tests/run_checks.py` | 0 | 503 passed（不回归） |
| `python net-the-spire/client/test_client.py` | 0 | 50 passed（不回归） |
| `python tools/contract-check/check_contracts.py` | 0 | 210 passed（不回归） |

## 4. 本轮修正的缺陷

**局部函数捕获了后声明的局部变量**：过滤查询最初写成 `AttackCardsInDiscard()` 并直接捕获 `playBody`，
但它在 `playBody` 声明之前就定义了（编译期 `CS0841`）。改为把 `scope` 作为参数传入——不捕获、不依赖声明顺序。

## 5. B-05c 剩余子项（仍未完成）

**多选**（`DecisionResponseKind.EntityRefList` 已存在，缺"对引用列表结果做查询/遍历"的消费端原语：
要在 `Engine/` 加一个新的 `Query` 节点，并同时教类型检查、调度器与**转储器**认识它——
`RuleSet.cs` 的注释明确要求"新节点必须让转储器知道"，否则身份文本会漏语义）；
保留时点（retain）；实例变量；生成/升级/变形/附魔；**Power/敌人的行动程序**；药水/run；
hook 里更广的步骤面（`target`/`apply_power`/`top_from_discard`/`exhaust_self`）；0.0.1 兼容入口。

## 6. 修改文件

- `BaseSpire/Authoring/SliceAuthorFrontend.cs`（`damage_lowest`/`top_attack_from_discard` 词表与目标域互斥）
- `BaseSpire/Composition/CombatSlicePackage.cs`（两个步骤的降级；`AttackCardsInDiscard(scope)` 过滤查询）
- `BaseSpire.Slice.Tests/Cards/SliceFrontendConditionalChecks.cs`（新增 5 项）、`SliceChecks.cs`（注册）
- `docs/review/tofix/long-task-progress-2026-10-02.md`（状态）

未修改：`Engine/`、Legacy、net、Python 侧。未提交、未清理共享工作区。

## 7. 下一批建议

**B-05c 第八批：多选**。它必须在 `Engine/` 里补一个"引用列表结果可查询"的原语（`Query` 节点 + 类型检查 +
调度器求值 + 转储），然后开放作者语法（如 `top_attack_from_discard x2` 或 `choose from discard xN`），
并补"多选结果被逐一消费、数量不足时按最少可用数提问/不提问"的检查。
