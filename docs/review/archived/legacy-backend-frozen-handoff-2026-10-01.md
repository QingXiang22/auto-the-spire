# 旧 DSL 后端冻结与 `Legacy/` 迁移交接（2026-10-01）

> 2026-10-01 归档说明：路径迁移与冻结边界已交付；旧内容后续迁移属K-09/B-05之后的独立任务，未提交工作本身不是报告问题。已知旧首条失败已有190/190新证据，执行接管范围见评审处置。 详见[评审处置与剩余任务](../records/2026-10-01/review-followup-2026-10-01.md)。

## 需求与交付状态

用户指示：把旧的规则后端移走，并在 `AGENTS.md` 里注明旧的已暂时不用。经确认采用**只换目录 + 标注冻结**的范围，即不排除编译、不弄破任何下游工程；多 agent 协调由用户承担。本次交付目录迁移、`AGENTS.md` 标注、文档链接修正与交接记录，**不改任何规则行为**。

| 状态层次 | 本次结果 |
| --- | --- |
| 设计目标 | 旧 STS 特化 DSL 后端冻结（不再扩展）；新的机制扩展默认落在通用引擎 `Engine/` |
| 解析器接受 | **未改**语法或词法：`.spire` 0.0.1 的接受范围与本次变更前完全一致 |
| 内容包能注册 | **未改**注册路径；`base-spire` 仍由旧后端加载，122 张卡照常注册 |
| 规则行为已验证 | **未改**行为。构建 0 错 0 警；`Engine` 32/32、切片 21/21 通过；旧后端规则检查有**既有失败**（见「验证及未验证事项」） |

## 交接对象与所需动作

| 对象 | 需要做什么 |
| --- | --- |
| **正在编辑旧后端源码的 agent** | 有 4 个带未提交改动的文件被一起移动，git 现显示为 ` D` + 未跟踪新路径。**改动内容没有丢**（已逐文件与 `HEAD` 比对）。请把后续编辑落到 `core-the-spire/Core.TheSpire/Legacy/<文件>.cs`，不要在原路径继续写 |
| **`base-spire` 负责人** | 本次未改 `base-spire` 任何文件。知晓即可：122 张卡仍走旧后端，冻结不等于停用 |
| **`Engine/` 负责人** | 无需动作。两套后端零互相引用（已核对），本次移动未触碰 `Engine/` |
| **任务负责人（提交时）** | 本次**未提交、未 `git mv`**，索引未改动。26 个文件当前是 ` D` + 未跟踪新目录，提交时 git 会自行识别为重命名 |
| **文档作者** | 引用旧后端源码时，路径改为 `core-the-spire/Core.TheSpire/Legacy/<文件>.cs` |

## 改动文件

**移动（26 个，`core-the-spire/Core.TheSpire/` 根 → `Legacy/`）**

`ActRoomContent`、`CardCostModifier`、`CardRewards`、`CombatCardEvents`、`CombatCardGeneration`、`CombatCardPlay`、`CombatCardRules`、`CombatCardVariables`、`Content`、`ContentDsl`、`DslSource`、`EffectCountScope`、`EffectDsl`、`EffectExecutionContext`、`Effects`、`EncounterPools`、`EncounterSlots`、`Environment`、`RandomStreams`、`RunEffectExecutionContext`、`Runs`、`ScopedIntState`、`SpireCombatBehaviorDslCompiler`、`SpireCombatDslCompiler`、`State`、`StateVariableDefinition`（均 `.cs`）。

`AssemblyInfo.cs`（工程级 `InternalsVisibleTo`）留在根目录。

**新增**

- [`core-the-spire/Core.TheSpire/Legacy/README.md`](https://github.com/QingXiang22/auto-the-spire/blob/0aca7d1a26f3ad42d5b8657bc794cd0b12f85ba8/archive/legacy-sts/core-the-spire/Core.TheSpire.Legacy/Legacy/README.md)：该目录是什么、冻结理由、消费者清单、使用约定、待决事项。
- 本交接报告。

**修改**

- [根 `AGENTS.md`](../../../AGENTS.md)：`core-the-spire` 条目下新增两套后端的边界说明与指向；「规范与实现状态」新增 2026-10-01 检查现状。
- [`core-the-spire/AGENTS.md`](../../../core-the-spire/AGENTS.md)：新增「后端现状：两套并存，旧的已冻结」整节（对照表 + 5 条约定）。
- 10 份文档共 **68 处**链接：`Core.TheSpire/<文件>.cs` → `Core.TheSpire/Legacy/<文件>.cs`。

**未触碰**：`Engine/`、`base-spire` 的任何文件、`AutoTheSpire.sln`、`README.md`，以及其他 agent 在 `Legacy/` 之外的未提交改动。

## 关键决策与迁移影响

1. **只换目录，不排除编译。** 这 26 个文件仍属于 `Core.TheSpire.csproj`（SDK 风格工程按 `**/*.cs` 通配），命名空间仍为 `CoreTheSpire`。因此程序集输出、类型、行为均无变化，`base-spire` / `Core.TheSpire.Tests` / `test-hook-mod` / `tools/core-architecture-audit` 四个消费者照常工作。备选方案「从 csproj 排除」会让这四个工程全部编译失败，本次未采用。
2. **"冻结"定义为不再扩展，不是已停止运行。** 旧后端目前仍是 `base-spire` 原版内容的**唯一可运行实现**。把"暂时不用了"表述成"已弃用"会让 agent 误以为可以不维护它。
3. **不使用 `git mv`。** 为了让共享索引保持原样，改用文件系统移动。代价是 4 个带未提交改动的文件在 git 里呈现为重命名而非修改；已用内容比对确认改动未丢失。
4. **文档链接同步修正，只改路径不改语义。** 替换严格限定在这 26 个文件名内，未误伤 `Engine/` 路径、未重复加前缀。别人的报告（如 `core-base-architecture-dsl`、`ai-encoder-design-review`）也只动路径、不动表述。
5. **`base-spire` 何时迁移到 `Engine/` 尚未决定。** 这是本次冻结留下的最大开放项；在那个决定之前，旧后端仍是生产路径，`Engine/` 只在切片范围内验证过。
6. **给 OriMod 这类扩展型 mod 补能力，方向已明确不再走旧后端。** 旧后端的扩展面是封闭的：`PlayerAction` 是固定 record 层级、`state` 只有 combat/run 两个作用域且仅 `int`、`IRuleHook` 是固定 10 个方法、`TargetSelector` 是 7 个封闭枚举；`Engine/` 提供的是包可声明的类型字段、动作、事件订阅与可筛选排序的目标查询。

## 规范冲突及处理

根 `AGENTS.md` 原先只说 `core-the-spire` 是"独立的 C#/.NET 9 规则内核"，没有反映内核里已并存两套后端的事实——这会让人（和 agent）在错误的文件上做扩展。本次在根与模块 `AGENTS.md` 都补齐了这一边界，并明确"新机制扩展的默认落点"。

`AGENTS.md` 第 5 条要求"不移动他人的工作"。本次移动包含 4 个带有其他 agent 未提交改动的文件，属越界操作；**该动作由用户明确授权**（用户选择"直接做，我承担协调"），并在本报告「交接对象与所需动作」中列出受影响文件，供协调使用。

未改写任何协作约定条款，未删除旧实现证据，未归档既有未解决报告。

## 验证及未验证事项

**已执行**

| 命令 | 结果 |
| --- | --- |
| `dotnet build AutoTheSpire.sln` | ✅ 0 错误 0 警告（移动前后各一次） |
| `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` | ✅ All 32 engine checks passed. |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | ✅ All 21 slice checks passed. |
| `dotnet run --project core-the-spire/Core.TheSpire.Tests` | ❌ 首条失败 `CombatSliceTests.cs:498`：期望 `AttackEffect`、实得 `SequenceEffect` |
| `python tools/doc-check/check_docs.py .` | ✅ scanned 74 Markdown files, 0 problem(s) |
| 26 个文件移动前后的**内容比对**（4 个带他人改动的文件逐个与 `HEAD` 比对） | ✅ 均与 `HEAD` 不同，证明他人编辑随文件保留 |

**旧后端规则检查的失败与本次移动无关，已用 A/B 实证**：先把 26 个文件移回根目录，构建并运行，失败信息与移动后**逐字相同**；再移回 `Legacy/`，失败信息依旧。该失败与 [core-base-architecture-dsl-2026-10-01.md](../records/2026-10-01/core-base-architecture-dsl-2026-10-01.md) §7 记录的当日首个失败是同一行、同一对类型，且 `CombatSliceTests.cs` 当时带有其他 agent 的未提交改动。**未把"构建成功"当作规则行为通过。**

**未验证 / 未做**

- **未提交 Git、未 `git mv`、未归档任何报告。** 共享 Git 状态与提交时机由任务负责人决定。
- 未逐份通读被修改链接的 10 份文档正文；只做路径替换。若某份文档的表述依赖文件的物理位置（例如"根目录下的大文件"这类说法），需要另行核对。
- 未检查仓库之外是否有脚本、IDE 配置或本地工具按旧路径引用这 26 个文件。
- 未评估旧后端冻结对后续 `base-spire` 内容迁移工作量的影响，也未制定迁移计划。
- 未运行内容加载清点（`.spire` 实际注册数量），未做运行时差分。

本报告保留在 `tofix`，直到 `base-spire` 是否/何时迁移到 `Engine/` 有明确决定，且受影响的 4 个文件在其他 agent 侧不再有未提交改动为止。
