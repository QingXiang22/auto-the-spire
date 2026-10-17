# B-05c：完整前端覆盖（第十六批：生成/升级/变形，2026-10-02）

> 历史证据（2026-10-03整理）：正文保留原时点结果，历史“下一步”不再作为当前派单指令。本报告归属W-06、W-01，未结项转入[当前清单](../../tofix/README.md)；移入records不表示修复或验收通过。逐份归属见[记录索引](../README.md)。

| 项目 | 内容 |
| --- | --- |
| 任务 ID | **B-05c**（第十六批）——**生成 / 升级 / 变形**（战斗中改动实例身份或实例字段） |
| 权威 | [开工清单 §14](../../../design/implementation-kickoff.md#14-m1后阶段二sts作者前端与内容迁移)（B-05c 行含"生成/升级/变形"） |
| 负责范围 | `Authoring/SliceAuthorFrontend.cs`（带参数步骤、`generate:`/`becomes:` 命名空间限定）、`Composition/CombatSlicePackage.cs`（`CreateEntity` 生成、`upgrade_hand` 升级、`becomes:` 定义改写）、`BaseSpire.Slice.Tests/Cards/SliceFrontendGenerationChecks.cs`（新增 5 项） |
| 状态 | **部分完成（十六批）**：三种能力已交付；B-05c 其余子项仍未开始（见 §5） |
| 前置 | [批1](b-05c-frontend-batch1-2026-10-02.md) … [批15](b-05c-frontend-batch15-2026-10-02.md) |

## 1. 作者语法与降级

| 步骤 | 含义 | 降级（只用既有原语） |
| --- | --- | --- |
| `generate:<card-id>` | 手牌里多出一个该定义的新实例 | `CreateEntity(card, EntityScope(attacker), …, hand)`（**同一战斗作用域**、普通形态、含实例变量初值） |
| `upgrade_hand` | 手牌里每张牌置为升级形态并换成升级数值 | `ForEach(hand, …)` + 按定义分派 `SetField(Upgraded/Amount/PowerStacks)`（已升级的跳过） |
| `becomes:<card-id>` | 这张牌打出后变成另一个定义 | 同一个实例上改写 `DefinitionId/Kind/Cost/Amount/PowerId/PowerStacks/Upgraded` 与实例变量初值 |

- 带参数的步骤写作 `kind:参数`（单个词，与 `variable:charge` 一致）；缺参数以 `invalid_value`、未知步骤以
  `unknown_step` 拒绝（都带路径）。
- 参数同样按 `package { id }` **限定到包命名空间**（`generate:strike-ironclad` → `generate:base-spire:strike-ironclad`），
  与卡片/能力引用一套规则。
- 生成的新实例用**攻击者的作用域**（`EntityScope`）作为 owner——与切片里其它卡牌的 owner 一致（都是战斗作用域）。

## 2. 退出证据

| 判据 | 检查 |
| --- | --- |
| 语法 | 三种步骤逐条读出（含限定后的参数）；缺参数/未知步骤被拒绝且路径指向 `steps[…]` |
| **生成** | `手牌里多出一个新实例，它可以被正常打出 [真实]`：手牌里该定义多一张；多出来的是**新实例**；它按普通形态的声明造成 3 点 |
| **升级** | `手牌里的牌全部变成升级形态并换成升级数值 [真实]`：升级前手里都不是升级形态；`upgrade_hand` 后**全部**是；升级后的打击造成 **7**（而不是 3） |
| **变形** | `打出后这张牌变成目标定义、数值重置为普通形态 [真实]`：先按自己的声明造成 1 点；打完进弃牌堆（**同一个实例**）；`DefinitionId` 变成打击、`Amount` 重置为 3、`Upgraded` 回到 false |
| 默认路径 | 冻结作者文本没有这三种步骤 |

## 3. 本轮修正的缺陷

1. **局部函数的捕获顺序**（编译期）：`StepInstructions` 经由 `CardFields`/`VariableFields` 捕获了在它之后才声明的
   `setup` 与 `variableFields`，编译器要求被捕获变量在**调用点**已确定赋值。修法：让 `CardFields`/`VariableFields`
   接收 `scope` 参数（不再捕获 `setup`），并把 `variableFields` 字典上移到 `Build` 顶部（在任何使用之前）。
2. **步骤参数没有限定命名空间**：`generate:strike-ironclad` 直接查 `author.Cards` 查不到（模型里是限定 id），
   报 `Sequence contains no matching element`。现在与其它引用一起限定。
3. **`CardFields` 调用点遗漏**：改为带 scope 的参数后漏改一处调用点（编译期 `CS7036` 报出，已修）。

## 4. 运行结果（本轮实跑）

| 命令 | 返回码 | 结果 |
| --- | --- | --- |
| `dotnet build AutoTheSpire.sln` | 0 | 0 错误 0 警告 |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | 0 | **All 96 slice checks passed**（25 既有 + 4 B-05b + 67 项 B-05c） |
| `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` | 0 | 64 passed |
| `dotnet run --project core-the-spire/Core.TheSpire.Tests` | 0 | 190 passed / 0 failed |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | 0 | 383 passed（不回归） |
| `python ai-the-spire/tests/run_checks.py` | 0 | 503 passed（不回归） |
| `python ai-the-spire/tests/j01_episode.py` | 0 | 150 passed（不回归） |
| `python tools/contract-check/check_contracts.py` | 0 | 210 passed（不回归） |
| `python net-the-spire/client/test_client.py` | 0 | 50 passed（不回归） |

## 5. B-05c 剩余子项（仍未完成）

药水/run（事件/房间/奖励/地图，整块内容域）；hook 里更广的步骤面（`target`/`apply_power`/`top_from_discard`/`exhaust_self`）；
伤害上限阶段（cap）；生成时的**随机**选择（`generate-random`，需声明随机流与候选集）；
变量的跨战斗/跨实例复制语义。另外：本批的"变形"是**确定性**变成指定定义，不是 STS 的"随机同稀有度替换"。

## 6. 修改文件

- `BaseSpire/Authoring/SliceAuthorFrontend.cs`（`Parameterized` 步骤、`UpgradeHand`、参数限定）
- `BaseSpire/Composition/CombatSlicePackage.cs`（`createdSlot` 参数、`UpgradeOne`、生成/变形分支、`CardFields`/`VariableFields` 取 scope）
- `BaseSpire.Slice.Tests/Cards/SliceFrontendGenerationChecks.cs`（新增 5 项）、`SliceChecks.cs`（注册）
- `docs/review/tofix/long-task-progress-2026-10-02.md`（状态）

未修改：`Engine/`、Legacy、net、Python 侧。未提交、未清理共享工作区。

## 7. 下一批建议

**B-05c 第十七批：hook 里更广的步骤面**（把 `target`/`apply_power`/`top_from_discard`/`exhaust_self` 也允许写进 hook），
它是纯**放宽既有能力**的一项（不需要新引擎能力），风险低；随后考虑**药水/run** 整块内容域（需要先定范围）。
