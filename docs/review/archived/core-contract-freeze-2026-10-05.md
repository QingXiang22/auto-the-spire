# core 通用契约收口及冻结关闭

日期：2026-10-05。范围：用户授权完成 Start 顺序、枚举决策域及通用契约说明，随后冻结 core 并转向下游。状态：**CORE-CONTRACT 已关闭，core 基线 FROZEN**。冻结不以原版内容/Run/对拍或 AI 完成为前置，不关闭完整 ENV/W。

## 1. 修改与关键决策

- Scheduler.Start 使用同一执行栈中的启动游标，按链接包/登记顺序逐个进入过程。暂停保留游标，合法回答后先完成当前入口再执行下一入口；同级入口不占嵌套调用深度，调试 ProcedureEnter 对应实际顺序。没有新调度器或游戏阶段。
- IrTypeChecker 要求 Enum 决策使用具体非空 Enum 结果槽；运行时从参数/局部槽的链接类型推导 0..成员数-1，并验证域内 min/max。非法回答在续接前拒绝，原请求/状态保留；非法规则范围先 Faulted、不发布请求。
- RuleSetBuilder.EngineVersion 升为 engine/ir@0.3，旧显式 0.2 包拒绝链接；DSL core/0.1、core/0.2 及公开 C# 签名保留。行为变化需重建包与身份，旧快照/轨迹/训练资产保留原身份，不自动兼容或开始新训练。
- [冻结契约](../../contracts/core-engine-baseline.md)明确组合、集合归属、生命周期、执行、数值/随机、公开权限和维护边界。[源码摘要](../../contracts/core-engine-baseline.json)保存本批 UTF-8/LF 规范化指纹，只作交付证据，不替代语义规则身份。独立 Git 基线的范围与验证另见 [冻结交接](../../contracts/core-git-checkpoint.md)。
- 维护者约定、语言参考、开发者指南、core spec/README、架构决定及唯一任务清单同步；[下游 goal](../../design/agent-goals.md)改为默认消费基线，在 mod DSL 包实现游戏规则，通用缺口先给最小复现交负责人判断。

主要代码文件：Engine/Scheduler.cs、Session.Frames.cs、Session.Execution.cs、Session.Instructions.cs、Session.Events.cs、IrTypeChecker.cs、RuleSet.cs；Engine.Tests/ContractChecks.cs 和 EngineChecks.cs。其他共享改动未重置、提交或移走。

## 2. 实际验证

新增 5 项通用检查涵盖：跨包及包内顺序、启动暂停一次续接/低调用深度/实际调试顺序；默认枚举域及非法回答保留状态；SDK 参数槽与域内子集/非法声明范围；未定型和空枚举链接拒绝；旧运行包显式拒绝。首条顺序检查在修复前实际失败，修复后通过；检查不依赖 STS 规则。

| 命令 / 项目 | 本批结果 |
| --- | --- |
| dotnet build AutoTheSpire.sln | 0 错误/0 警告，rc=0 |
| core-the-spire/Core.TheSpire.Engine.Tests | **95/95**，rc=0 |
| mod-the-spire/base-spire/BaseSpire.Slice.Tests | **264/264**，rc=0 |
| net-the-spire/Net.TheSpire.Tests | **393/393**，rc=0 |
| tools/differential compare --suite fixtures-v1 | 12 个 case：7 passed、4 unsupported_rng_profile、1 unsupported_decision_model；0 新首差、6 自检符合预期；工具 rc=0 不代表 5 个 unsupported 通过 |
| 默认 core/0.2 game.spire | 实际加载 DSL 库，Score 0→2→3 Terminal，rc=0 |
| core/0.1 counter.spire | 暂停/回答及事件，Score=3、Attempts=2、Applied=2 Terminal，rc=0 |
| tools/dependency-check/check_dependencies.py | **10/10**，15 工程、0 环/违规，rc=0 |

详细产物目录：[core](../../../artifacts/environment/core-freeze-2026-10-05/engine-checks.txt)、[base](../../../artifacts/environment/core-freeze-2026-10-05/base-checks.txt)、[net](../../../artifacts/environment/core-freeze-2026-10-05/net-checks.txt)、[对拍](../../../artifacts/environment/core-freeze-2026-10-05/fixtures/comparison.json)、[依赖](../../../artifacts/environment/core-freeze-2026-10-05/dependencies.json)、[默认示例](../../../artifacts/environment/core-freeze-2026-10-05/default-example.txt)、[进阶示例](../../../artifacts/environment/core-freeze-2026-10-05/advanced-example.txt)。

`python tools/doc-check/check_docs.py` 扫描 199 份 Markdown，0 问题；`git diff --check` rc=0，仅既有跟踪文件的 CRLF 提示。本批未执行完整单人矩阵、真实客户端或 AI 学习，不算它们通过。归档旧后端不进入活动门槛。

## 3. 迁移与接续

重新构建 0.3 包/规则身份，多启动入口按登记顺序校核，Enum 决策使用具体类型而非 Binding。具体写法保持原 DSL；代码存在/构建通过不代替包行为或完整游戏验收。

当前范围的通用接口/语义按冻结契约维护；修复和兼容扩展可进行，改变可观察结果、确定性或权限的行为必须新语义版本，不沿用 0.3 身份。Scope 不自动实例化集合、模板不支持引用/行为注入、LoadFiles 不注册游戏扩展、Lua/完整 Session 恢复/编辑器未交付，均是明确范围而非已承诺功能。

下一步是 mod/base 正式规则库与完整单人环境、net 同构建公开出口、tools 严格日常对拍；完整 ENV 和 AI 暂缓沿原清单。死亡余效、回合、牌区和胜负时机都在游戏 DSL 包，不迁入 core。关闭只指核心收口，本报告按协作约定先进入 tofix，核对上述实际证据后移入 archived；未解决的环境报告保持开放。

## 4. Git 冻结提交的独立验证

2026-10-05 用户授权提交核心并暂时冻结。仅暂存通用内核、核心检查、DSL 示例/规范、必要的 Legacy 归档及其退出活动入口；工作区完整 solution 与 base/net/AI 等未提交实现继续保留。提交中的根 solution 只包含核心及核心检查，详见 [Git 交接](../../contracts/core-git-checkpoint.md)。

从 Git index 导出独立副本，未复制未跟踪的下游实现：54 项 LF 规范化源码指纹全部匹配；根 solution 构建 0 错误/0 警告；核心 95/95；默认和进阶示例实际加载与结果正确；归档旧规则 190/190。命令、返回码和输出见 [独立验证摘要](../../../artifacts/environment/core-freeze-2026-10-05/git-checkpoint/validation.json)及同目录日志。生成代码及生成脚本仅去除尾随空白，日志规范化换行；不改变运行语义或版本。暂存区 `git diff --cached --check` 通过。

原版环境下一步应在 base DSL/规则库中扩展，并将参考对拍纳入每批功能测试；参考端 unsupported 单列，最终以对应版本真实客户端/源码核对。其他 mod 在原版基线稳定后实施，并与真实游戏加载相同 mod 版本/配置/顺序的表现核对。冻结不关闭原版、整局、mod 或 AI 的工作。
