# K-00 实施基线（四模块开工清单）

> 历史证据（2026-10-03整理）：正文保留原时点结果，历史“下一步”不再作为当前派单指令。本报告归属W-01、W-02、W-03、W-04、W-05、W-06、W-07、W-08，未结项转入[当前清单](../../tofix/README.md)；移入records不表示修复或验收通过。逐份归属见[记录索引](../README.md)。

| 项目 | 内容 |
| --- | --- |
| 状态与日期 | 2026-10-01；K-00 全部条目已执行；结论为"实际基线"，不是功能完成声明 |
| 负责范围 | K-00（工作区与实际基线）；本任务随后交付 core-the-spire 通用运行时批次 K-01 至 K-07 |
| 执行环境 | Windows / pwsh；`dotnet 9.0.306`；工作目录 `F:\Scylier-Project\active\auto-the-spire` |
| 本任务修改文件 | `core-the-spire/Core.TheSpire/Engine/Value.cs`、`PackageBuilder.cs`、`RuleSet.cs`、`Schema.cs`、新增 `IrTypeChecker.cs` |
| 未触碰 | `docs/`（除本报告）、`mod-the-spire/`、`net-the-spire/`、`ai-the-spire/`、`sts2/`、`reference/`、任何他人未提交改动；未 git add/commit/clean |

## 1. 工作区既有改动（他人工作，未触碰）

`git status --short` 在开工时即存在下列改动，本任务只读取、不重置：

- **未跟踪**：`core-the-spire/Core.TheSpire/Engine/`（六份初稿）、`AGENTS.md`、`docs/AGENTS.md`、`docs/README.md`、`docs/design/`、`docs/dsl/`、`docs/review/`、`reference/sts2-sim/`、`tools/*`。
- **已修改**：`README.md`；`core-the-spire/Core.TheSpire.Tests/CombatSliceTests.cs`；`core-the-spire/Core.TheSpire/{Content,DslSource,Environment,SpireCombatDslCompiler}.cs`；`mod-the-spire/base-spire/BaseSpire/ContentSource/{ironclad-cards,status-cards}.spire`；`mod-the-spire/base-spire/README.md`。
- **已删除**：`docs/` 根下 9 份旧文档（已迁入 `docs/design/`、`docs/dsl/`）。
- **本次会话期间新出现**：`reference/orimod/`（`src/` 目录时间戳 21:39），非本任务产出，未读取、未修改。按根 AGENTS 的多 agent 约定记为并行工作区活动。

判定：`core-the-spire` 与 `mod-the-spire` 处于**未提交的 DSL/内容迁移中间态**，旧后端的断言与内容形状可能不一致。本任务不代其修改。

## 2. 构建基线

| 命令 | 修复前 | 修复后 |
| --- | --- | --- |
| `dotnet build AutoTheSpire.sln` | **失败**，10 个错误，全部在 `Engine/Value.cs(304-307)`，首个 `CS1519` | **成功**，`0 个警告 / 0 个错误`，返回码 0 |

修复前的 Engine 初稿**不可编译**，因此开工时"构建成功"不可作为任何既有能力的证据。

本轮为恢复构建所做的修复（全部在 `Engine/` 内，均属 K-01 范围）：

| 文件 | 问题 | 处理 |
| --- | --- | --- |
| `Engine/Value.cs` | `NumericProfile` 使用了 record 紧凑构造器写法（C# 不支持），10 个语法错误 | 改为显式构造函数 + `init` 属性，保留 `FixedScale` 0..12 与取整模式校验 |
| `Engine/PackageBuilder.cs` | 嵌套类 `Anchor` 与方法 `Anchor(string)` 同名（`CS0102`） | 嵌套类改名 `AnchorSymbol`，`RuleSet.cs` 解析分支同步 |
| `Engine/IrTypeChecker.cs` | 被 `RuleSet.cs:152` 引用但不存在（`CS0103`） | 新增：K-01 的定义/链接检查（见第 4 节） |
| `Engine/RuleSet.cs` | `CollectWinners` 未接受 `ActionSymbol`，任何 `builder.Action()` 的包在链接期必然抛"unsupported declaration" | 接受 `ActionSymbol`（动作经 action 表寻址，不占引用槽） |
| `Engine/RuleSet.cs` | 作用域父类型经 `TypeIds` 解析，而作用域只登记在 `ScopeIds`，任何带 `Parent` 的作用域类型必然解析失败 | 改为两遍解析：先分配全部作用域 id，再解析父链；`Schema.cs` 字段更名 `ParentScopeTypeId` |
| `Engine/PackageBuilder.cs` | `EnginePackage.Freeze` 不检查同包重名声明，两个同名声明会静默拿到同一 `QualifiedId` 并随后以"越权覆盖"报错 | `Freeze` 增加同包重名检查（`DuplicateDeclaration`） |
| `Engine/PackageBuilder.cs` | `IrScope.Temp()` 默认声明为 `Int`，而 `FieldTypeKind.Any` 的文档语义正是"引擎分配的绑定槽" | 默认改为 `FieldTypeSchema.Binding()`；新增该工厂方法 |

## 3. 规则检查基线

| 命令 | 返回码 | 执行数 | 结果 |
| --- | --- | --- | --- |
| `dotnet run --project core-the-spire/Core.TheSpire.Tests` | `-532462766`（`0xE0434352`，未捕获 .NET 异常） | `Program.Main` 注册 189 次调用；第 13 次抛异常 | **12 通过 / 1 失败 / 176 未执行** |

- 首个失败：`CombatSliceTests.cs:498`，`BasePackageRegistersVanillaStartingDeckAndDeadlyCultistValues`。
  `Expected 'AttackEffect { Amount = 42, Target = SelectedTarget, Properties = Move, OnFatal = }', got 'SequenceEffect { ... }'`（Bludgeon 升级后的效果形状）。
- **归属：base-spire / 旧 DSL 工作流**，不是 core Engine 批次。依据：`CombatSliceTests.cs`、`ironclad-cards.spire`、`SpireCombatDslCompiler.cs` 均为工作区未提交改动；本批只改 `Engine/` 内文件，且 `CoreTheSpire.Engine`、`EnginePackageBuilder`、`RuleSetBuilder` 在 `Engine/` 之外**没有任何引用**（全仓 `Select-String` 为空），Engine 不可能改变旧后端的 DSL 编译结果。
- **诚实限定**：修复前整个 solution 不可编译，因此无法用"改动前运行"直接对照该失败；上表是"归属判断 + 引用隔离证据"，不能读作"已证明该失败早于本批"。
- 影响：该入口在**首个断言**即中止，所以它**不能**用来度量旧后端覆盖；本批 K-02 至 K-07 的验证必须走独立的 `Core.TheSpire.Engine.Tests`（当前不存在）。
- 同一命令的其余 176 项未执行，不得当作通过。

文档与空白检查（同一基线，均为他人文档改动）：

| 命令 | 返回码 | 输出 |
| --- | --- | --- |
| `python tools/doc-check/check_docs.py .` | 0 | 扫描 49 份 Markdown，0 problem |
| `python tools/doc-check/check_trace.py .` | 0 | 20 条需求行，0 mismatching |
| `git diff --check` | 0 | 仅 `LF will be replaced by CRLF` 警告（他人文件） |

## 4. `core-the-spire/Core.TheSpire/Engine/` 清点

开工时六份初稿（未跟踪、未编译过），加本轮新增一份：

| 文件 | 行数 | 已有能力（读源码得到） | 真实可用范围 |
| --- | --- | --- | --- |
| `Value.cs` | 399 | `InstanceId`/`ParticipantId`/`ScopeId`；`Value` 11 种 kind；`NumericProfile` 定点/取整/checked 整数；`EngineFaultCode`/`Faults` | 编译通过；无执行证据 |
| `Schema.cs` | 597 | 12 种声明符号；`FieldTypeSchema`；冻结定义（Type/Collection/Relation/Scope/Event/Terminal/Compute/Procedure/Action/Subscription/Projection）；`PrimitiveSignature`/`EngineEffect` | 同上 |
| `Ir.cs` | 609 | 全量 `Expr`/`Query`/`Inst` 节点 + `PrimitiveSignatures` 目录 + `IrDumper` 依据 | 同上 |
| `PackageBuilder.cs` | 489 | `EnginePackageBuilder`、`IrScope`/`ProcedureBuilder`/`ActionBuilder`、`EnginePackage.Freeze`、`ContentHasher` | 同上 |
| `RuleSet.cs` | 1014 | `RuleSetBuilder` + `Linker`：依赖校验、winner 收集、显式覆盖/字段扩展、密集 id、resolution 表、identity、`IrDumper`、`ContentHasher` | 同上 |
| `RuleSetData.cs` | 135 | `LinkedTables`、`RuleSet`（只读门面 + `Resolve*`） | 同上 |
| `IrTypeChecker.cs` | 1035 | **本轮新增**：引用解析校验、表达式类型推断、帧槽与绑定域、字段初始化归属、查询绑定作用域、纯过程副作用拒绝、compute 静态预算、投影角色、作用域父链环检测 | 编译通过；尚无正反例测试 |

**K-02 至 K-06 尚未存在的交付**：实例/字段存储、集合与关系、作用域树、唯一调度器（帧/调用/事件快照/预算）、动作与选择（guard/候选/版本核对/暂停续接）、具名随机服务、参与者投影与公开快照、会话与状态哈希。**目前没有任何包被实际构建、链接或执行过**，所以不能宣称 K-01 之后任一能力已实现，也不能宣称 K-01 已"验证"（只做到"有实现 + 编译通过"）。

> 本段是 **K-00 时点**的记录。上述缺口的后续交付与证据见 [core-engine-k01-k07-2026-10-01.md](core-engine-k01-k07-2026-10-01.md)：K-01 至 K-07 已实现并由 `Core.TheSpire.Engine.Tests` 的 31 项可执行检查覆盖；本文件其余结论（旧后端首个失败、base-spire 浅层清点）仍然有效。

## 5. base-spire 内容清点（浅层）

- `mod-the-spire/` 下 18 个 `.spire`，约 5952 行；其中 `ironclad-cards.spire` 4020 行、`enemies.spire` 1111 行。
- **加载证据**：旧检查第 13 项在 498 行之前通过（卡池成员、卡牌稀有度等断言），说明 base-spire 内容在旧后端**可以加载并注册**；它只是对 Bludgeon 的效果形状期望与当前实现不一致。
- 旧语法仍在使用：`enemies-*.spire`、`potions.spire`、`*-powers.spire`、`state.spire` 的新语法计数为 0。
- **未执行**：切片 7 项内容（StrikeIronclad、DefendIronclad、Bash、Headbutt、Strength、Vulnerable、DampCultist）的逐项数值与 `sts2/` 来源对照。该清点转入 **B-01/B-02**，本报告不冒充已有结论。

## 6. 失败、残余风险与未验证项

1. **旧后端首个断言失败**（第 3 节）：归属 base-spire/DSL 工作流，需由该工作流负责人决定是修内容还是修断言；本任务不擅自放宽断言。
2. **旧入口不可用于度量**：它一次只跑到首个失败，本批的验证必须另建检查入口。
3. `Engine/` 无任何运行时执行证据；`IrTypeChecker` 需要正反例测试（缺引用、环依赖、非法覆盖、纯过程副作用、类型错配、绑定越界）才能算验收。
4. 本次**未执行**：`dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests`（项目不存在）、`net-the-spire`/`ai-the-spire` 的任何命令（目录无实现）。
5. 并行工作区：`reference/orimod/` 与 DSL 迁移中间态可能继续变动；重跑本报告的命令前应先看 `git status`。

## 7. 下一批依赖

- **K-01 收尾**（本任务）：`Core.TheSpire.Engine.Tests` 建立后补定义检查正反例，使"K-01 验收判据"有可执行证据。
- **K-02 → K-07**（本任务）：实例与存储、唯一调度器、动作与选择、RNG 与数值、参与者投影、通用性验收。
- **B-01 至 B-04**：依赖 K-07 完成；在此期间不要按本文第 5 节的浅层计数宣称切片已就绪。
- **P-01 → A-01 至 A-05**：与本批无依赖，可独立开工（`docs/contracts/net/0.1/` 当前不存在）。
- **N-01**：依赖 P-01，且不得与本批并行执行 `dotnet build`（同一 `obj/bin`）。
