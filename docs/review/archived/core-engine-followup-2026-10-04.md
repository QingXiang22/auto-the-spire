# core 后续任务交付与关闭记录

日期：2026-10-04。用户将 core 后续编码交由负责人直接完成。本报告关闭 CORE-SPLIT/FREEZE/RANDOM/DEBUG 及 CORE-DSL 首版核心编译链；不关闭完整 STS、base 扩展迁移、统一正式工厂、参考事件全量对拍或编辑器插件。接续范围仍在[唯一清单](../tofix/README.md)。

## 可直接使用的交付

普通作者编辑 [counter.spire](../../examples/core-choice-counter/counter.spire)；[Host](../../examples/core-choice-counter/Program.cs) 从文件加载规则，泛化驱动动作/回答和投影打印，没有 BuildRules 或按示例名字生成效果。默认选择 +2/+1 后 Score=3、Attempts=2、Applied=2、Terminal。修改 Delta=2 为4，使用 --no-build 的同一宿主首次回答直接终局 Score=4、Attempts=1、Applied=1。实际配置与输出保存在[产物目录](../../../artifacts/environment/core-engine-2026-10-04)。

[作者入门](../../dsl/core/README.md)、[core/0.1 参考](../../dsl/core/0.1/language-reference.md)、[开发者指南](../../design/core-the-spire/developer-guide.md)和[架构决定](../../design/core-the-spire/core-architecture-decisions.md)同步到实际接口，区分配置作者、扩展开发者与宿主。

## 实现和公共契约

- Session.Execution/Frames/Instructions/Expressions/Queries/Events/Resolution/Debug 按职责拆分；同一 Session、执行栈和 Drain，原有69项机制检查在机械拆分后通过。没有第二个调度器或开发者限权层。
- RuleAuthorObject/AuthoringList/RuleFreezer 将作者 Symbol 与子集合冻结；修改即拒绝。IR 的调用参数/初始化/载荷等集合与 Value 列表隔离输入数组并只读发布；包/链接表只读。包索引在链接上下文维护，作者 EnginePackage/DeclarationSymbol 上的可变 PackageIndex 已移除，调用者应读取链接 Definition 的 PackageIndex；扩展 Owner 字段不再写回共享 Symbol，多次链接互不影响。SessionState 仍可变。
- SamplePick 使用 query 原序列。base 三个旧切片取样点显式 Sort，保留旧切片结果；这不宣称它等于所有原版候选构造。IRandomProfile/IRandomGenerator 提供游戏拥有的版本化算法和映射，核心统一流计数、诊断和任意长度流状态快照；base 的 VanillaRandomProfile 通过既有冻结向量。既有内置 profile 保持兼容，不以改名替代算法实现。
- SessionDebugOptions 默认关闭，轨迹有记录数/文本长度上限、DroppedRecords 和独立 FirstFault。记录命令/过程/可选指令/事件监听/决策/随机消耗/终局/故障及来源；只读拉取，不在执行内调用接收器。开关不改变状态、随机、合法候选或语义身份；不进入 AI 的公开观察。tools/differential 通过这一实际入口保存12个case的非空轨迹，见 trace-audit.json。
- EngineDslCompiler 采用 ANTLR 4.13.1 grammar → 公共 AST → 名称/形状绑定 → 共同 IR/链接器，支持配置声明、表达式/查询、规则指令、依赖、投影、计算资产和终局。LoadFiles 为正常入口；扩展通过 Compile + RuleSetBuilder。IEngineDslExtension 按文件顺序声明/绑定游戏专属声明，拒绝覆盖核心声明；版本进入 CompilerIdentity。
- Grammar 与生成代码在 Engine/Compilation，runtime PackageReference 固定4.13.1，生成脚本在 tools/dsl-generate，工具 SHA256 固定。正常 .NET 构建不需要 Java；只有修改 grammar 后重新生成需要 Java。公共 DslSource AST/既有解析器继续服务 base 0.0.1，不作为 core/0.1 的第二套语义实现。
- `engine/ir@0.2` 标记新的随机候选顺序等契约；core/0.1 是语言版本，core-dsl/0.1 是编译身份，扩展身份也参与包/RuleSet 身份。旧 snapshot/训练资产必须按身份区分，不冒名兼容。源码路径与 debug 不参与语义身份。
- LegalAction.Bindings 排除探测帧局部量，只返回参数部分；未绑定自由参数仍需 Host 补全。修复跨包监听器 recheck 原来错误使用 reaction 包引用表的问题：参数布局来自 reaction，引用表来自订阅作者；实际 reaction 再回自己的包执行。同名本地/依赖程序按 Symbol 身份区分。

改动位置：core Engine 的编译/冻结/IR/Value/链接/随机/Session；Core.TheSpire.csproj；Engine.Tests 的 AuthoringChecks、DebugChecks、既有随机检查和配置 fixture 引用；配置例子工程/文件/Host；base 的三个显式排序点、VanillaRandomProfile/Layout 与对应检查；tools/differential 的记录契约/BaseBackend；上述作者/开发者/规范/导航/协作及任务文件。代码范围快照见 core-change-manifest.json；工作区本已有大量未提交/未跟踪改动，未重置、提交或整理其他任务文件。

## 实际验证

| 命令/检查 | 结果及范围 |
| --- | --- |
| `dotnet build AutoTheSpire.sln` | 返回0，0错误0警告；build.txt |
| `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` | 返回0，83/83；core-checks.txt |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | 返回0，264/264；base-checks.txt |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | 返回0，393/393；net-checks.txt |
| 配置例子默认运行 | 返回0；初始化/动作/选择续接/事件/观察/终局完整链 |
| 配置改动 + `--no-build` | 返回0；config-edit-run.txt、counter-modified.spire，终局改变 |
| ANTLR 重新生成 | 固定工具/哈希成功，生成后构建通过 |
| differential compare fixtures-v1 | 返回0，12case；7通过、4 unsupported_rng_profile、1 unsupported_decision_model；6自检符合期望，首差0；5项不计通过 |
| differential rng | 返回0；5种子位序列一致，8个布局差异均为已有登记的参考版本偏离，未登记差异0 |

新检查验证不同可观测行为：文本配置修改、依赖与跨包监听、扩展顺序/身份、冻结后的修改拒绝/数组别名、多次链接隔离、debug开关无语义变化/溢出/来源、第三方随机profile恢复精确消费。检查数量不等于原版覆盖率，也未用重复fixture来扩大完整环境结论。

本轮发现的验证失败及处理：隐式取样排序取消后 B-05c 依赖旧排序，base 改为显式 Sort 后恢复；跨包 recheck 使用错误引用表，按订阅上下文修复后新增实际执行检查通过。最终上述门槛均已重跑至绿色。

文档链接/格式检查扫描194份 Markdown，0问题；git diff --check 返回0，仅有既有LF/CRLF提示。模块依赖检查10/10通过，详见末尾与 dependencies.json。归档旧190项/兼容4项、本轮全量原版整局、AI训练和部署性能未执行。

## 仍开放的消费者和扩展

1. base 在 ENV 内容任务中绑定高级 DSL 声明、实现完整原版及正式工厂/profile 接线。新扩展接口并不自动迁移已有卡牌/敌人或完成整局。
2. tools 对参考侧规范化事件/随机/公开事实，全量对拍门槛按 ENV 完整范围继续。已有12个case的轨迹不是完整一致性证明。
3. IDE/LSP 插件后续复用 grammar/绑定元数据，高亮折叠先行；本轮没有交付插件或另一套语言服务编译器。
4. core/0.1 已支持目录之外的 DSL/Lua/IR 组合不算验收。多 start 入口当前为 LIFO，教程使用一个 Setup；自由参数、Enum 决策等边界在指南明确，不用教学宿主代替完整游戏驱动。

以上并入现有 ENV/W 清单，不新增一摞 tofix。首版核心编译链按本报告的实际可运行边界关闭，完整语言/内容/工具目标保持开放。

## 文档和依赖收口

新编译器内部类原名 Compiler 与归档后端内部类同名，依赖审计的名称隔离项因此拒绝。未放宽或跳过检查；内部类已改为 CoreDslBinding，重新构建0错误0警告，Engine83项仍通过。该命名调整不改变 DSL、IR、公开接口或规则身份。真实模块引用仍没有活动到archive或core到base的依赖。最终依赖检查10/10通过：15个工程、0依赖环、0隔离违规、0名称冲突，产物 dependencies.json。
