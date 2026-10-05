# DSL 规则库与作者入口复核及关闭

日期：2026-10-04。问题：规则作者教程暴露大量实体/选择/事件样板，实际缺少 DSL 自身的规则库复用；不能用 C# 构建器代替作者配置。依据用户“规则库应该是 DSL 里专门配置的事”的决定，本批已实现并运行。归档关闭范围是 CORE-RULE-LIB 作者层；完整原版环境、所有游戏机制与编辑器不在此关闭范围。

## 1. 最终方案与实际结果

core/0.2 增加 imports、template、use；现有 procedure 继续封装运行时过程。规则库作者使用 DSL 描述类型/初始化/动作/观察等声明组，内容作者导入库并填具名参数。模板展开到原有类型化 IR，沿用链接器、冻结及唯一 Session；没有新增运行时或引擎版本。

默认入口 [game.spire](../../examples/core-choice-counter/game.spire) 是 15 行含中文注释的配置。[规则库](../../examples/core-choice-counter/rules/score-race.spire) 也是带注释的 DSL 文件。配置统一 target 与 increments；直接把加分值列为合法动作参数，不再需要 Option 实体、计数事件或额外选择边界。规则库仍须明确底层规则，不能声称所有游戏都只需两个参数。

| 实际执行 | 结果 | 返回码 |
| --- | --- | --- |
| 默认模板配置，target=3、increments=[1,2] | Score 0 → 2 → 3，Terminal | 0 |
| 仅改配置，target=5、increments=[2,4]，宿主 --no-build | Score 0 → 4 → 6，Terminal | 0 |
| 原 core/0.1 counter.spire 进阶例子 | 暂停/回答及事件仍运行；Score=3、Attempts=2、Applied=2 | 0 |

实际日志及修改后的配置在 [本批产物](../../../artifacts/environment/dsl-rule-library-2026-10-04/default-run.txt)、[配置修改日志](../../../artifacts/environment/dsl-rule-library-2026-10-04/config-edit-run.txt)、[进阶日志](../../../artifacts/environment/dsl-rule-library-2026-10-04/advanced-run.txt)。

## 2. 修改范围与验证依据

| 范围 | 变更 |
| --- | --- |
| core Compilation | 新增文件依赖加载器及模板前端；复用 ANTLR AST 与现有绑定器；实例词法作用域、参数和来源定位 |
| core PackageBuilder | 内部只读模板导出元数据，包冻结时快照；不进入 Session 状态 |
| Engine.Tests | 7 项判别性检查涵盖真实入口、配置修改、多实例隔离、同名函数隔离、嵌套传递依赖、身份、参数错误和导入异常 |
| 示例宿主及项目 | 默认改读 game.spire，复制规则库；选择合法参数绑定；保留进阶文件显式运行 |
| 文档及任务 | 作者入门、core/0.2 指南、开发者指南、版本/导航/架构决定同步；更新唯一清单，完整 ENV/W 任务保留 |

执行命令与结果：

```powershell
dotnet build AutoTheSpire.sln
dotnet run --no-build --project core-the-spire/Core.TheSpire.Engine.Tests
dotnet run --no-build --project mod-the-spire/base-spire/BaseSpire.Slice.Tests
dotnet run --no-build --project net-the-spire/Net.TheSpire.Tests
dotnet run --project docs/examples/core-choice-counter/CoreChoiceCounter.csproj
dotnet run --no-build --project docs/examples/core-choice-counter/CoreChoiceCounter.csproj -- artifacts/environment/dsl-rule-library-2026-10-04/game.spire "1,0"
dotnet run --no-build --project docs/examples/core-choice-counter/CoreChoiceCounter.csproj -- docs/examples/core-choice-counter/counter.spire "1,0"
python tools/dependency-check/check_dependencies.py --json artifacts/environment/dsl-rule-library-2026-10-04/dependencies.json
```

完整 solution 构建 0 错误/0 警告；Engine **90/90**、base **264/264**、net **393/393**，返回码均为 0。实际运行时加载了 DSL 库和展开内容，不仅是解析/构建；基础/变更/进阶案例均到正确终局。依赖检查 **10/10**，15 工程、0 环/违规。检查日志：[core](../../../artifacts/environment/dsl-rule-library-2026-10-04/engine-checks.txt)、[base](../../../artifacts/environment/dsl-rule-library-2026-10-04/base-checks.txt)、[net](../../../artifacts/environment/dsl-rule-library-2026-10-04/net-checks.txt)、[依赖](../../../artifacts/environment/dsl-rule-library-2026-10-04/dependencies.json)。

文档校验：`python tools/doc-check/check_docs.py` 扫描 196 份 Markdown，0 问题；`git diff --check` 返回 0，仅既有跟踪文件的 CRLF 提示。本轮未重跑全量对拍/RNG/AI，未修改其运行规则或开启训练；保留先前证据，不计本批通过项。base/net 回归不等于完整原版覆盖。

## 3. 契约与限制

- 前端版本 core/0.2、身份 core-dsl/0.2，运行时仍 engine/ir@0.2。core/0.1 文件保持原身份；模板及默认值（包括未使用导出）参与内容哈希。注释、路径和来源不改身份。
- imports 相对声明文件解析、去重并拒绝循环/缺文件/同包多文件；Compile 无磁盘加载，扩展宿主显式提供依赖并链接。当前 LoadFiles 不接受自定义声明扩展参数。
- 参数限 Int/Bool/String/Fixed/List(Int) 字面量，支持默认与范围；body 限核心声明和嵌套 use。引用/行为参数、扩展声明模板化不在已交付范围。
- 实例名称自动加前缀，内部引用按所属实例和库词法绑定；配置错误定位作者，规则绑定错误附库位置及 use 来源。递归拒绝，深度/数量受限。
- 未实例化模板仅验证结构、参数、默认与占位符，不代表全部 body 已链接或规则已行为验证。
- 现有多个启动程序仍按 LIFO 执行；有顺序要求放单个 Setup 中。示例宿主只支持单玩家、单投影、有限参数，不能用多实例检查宣称多玩家宿主可用。

## 4. 接续责任

作者层规范见 [core/0.2 指南](../../dsl/core/0.2/authoring-guide.md)；状态仅维护于 [唯一清单](../tofix/README.md)。下一批由 mod/base 选一个真实共享机制，将现有原语可表达的规则放 DSL procedure/template，内容填参数/组合，同批通过正式加载与原规则对拍。确需新原语时附语法、IR 和行为案例，再扩核心或游戏前端；不要每张卡写 C# 效果类，也不要先全面改写旧内容。

本报告先进入 tofix，负责人核对实际执行及回归后移入 archived。仅关闭作者入口/规则库复用缺口；CORE-DSL-BASE、CORE-ENV-INTEGRATION、CORE-EDITOR 和完整 ENV/W 范围继续开放。
