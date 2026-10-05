# core-the-spire 协作约定

本文件适用于 `core-the-spire/`，在根 [AGENTS.md](../AGENTS.md) 之上补充本模块约定。

## 当前唯一后端

2026-10-04 用户决定退出 Legacy 兼容依赖。旧内核、旧注册器、旧测试及工具已移到[代码归档](../archive/legacy-sts/README.md)，不进入活动 solution。活动 core/base/net/tools 不得引用或链接编译归档实现，不得运行时回退。归档工程可单独运行，反向复用活动 core 的通用语法解析器；不表示新原版环境已完成。

新的规则能力落 `Core.TheSpire/Engine/`。游戏领域类型、玩法、原版随机布局与内容 DSL 语义由 mod/base 提供。当前普通作者入口为 core/0.2 `.spire` 规则库配置（imports/template/use），保留 core/0.1 基础声明；两者使用 EngineDslCompiler；C# EnginePackageBuilder 用于扩展与宿主集成，见[作者入门](../docs/dsl/core/README.md)。

## 最新架构决定

必须先读[core 架构决定](../docs/design/core-the-spire/core-architecture-decisions.md)和[ENV 实现约束](../docs/design/environment-implementation-constraints.md)。最新用户决定：保留开发者 Session API；不新增限权 facade；不另建通用日志系统；按职责尽早拆分但保持唯一调度器；为对拍增加可关闭调试模式；随机取样保留包提供的候选顺序，原版特定策略由 mod 拥有。冻结规则产物与可变游戏状态分开。

2026-10-05 用户澄清：core 独立于原版内容。死亡余效、回合、牌堆与胜负时机由游戏 DSL 包定义；core 只提供类型化状态、组合与执行原语及其明确契约。核心冻结按通用能力独立验收，不要求完整原版/Run/对拍先完成；游戏案例发现的缺口先还原成通用能力问题。未承诺的未来框架功能不自动成为冻结门槛。

正式环境共享 base 的组包/配置入口；Host 不另造默认随机 Session。不要将 AST 解析成功当成完整 DSL 编译或行为实现。通用 DSL 与 C# 构建器生成同一 IR，再由同一链接检查器和 Session 执行。

## 冻结基线（2026-10-05）

按 [core 冻结契约](../docs/contracts/core-engine-baseline.md)维护当前语法/API与行为。启动按包/登记顺序，Enum 决策域来自具体结果类型；旧 0.2 产物须重建并保留旧身份。新开发默认使用 core，在 mod DSL 包中定义玩法；不得自行重开核心架构迭代。通用缺口先给出最小复现交负责人，兼容维护可继续，破坏性语义变化必须版本迁移。完整原版/Run/对拍不作为 core 冻结条件。

## 验证

从仓库根构建 `dotnet build AutoTheSpire.sln`；运行 `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests`。base 内容变化还运行 `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests`。归档旧检查只作历史兼容验证，不算新后端覆盖率。

## 已交付接口（2026-10-04）

ANTLR tool/runtime 固定 4.13.1，grammar 与生成代码在 Engine/Compilation；改 grammar 后运行 tools/dsl-generate/generate-core.ps1。正常构建不依赖 Java。基础语言参考在 docs/dsl/core/0.1/language-reference.md，规则库指南在 docs/dsl/core/0.2/authoring-guide.md；设计提案不自动等于已支持语法。IEngineDslExtension 是游戏侧声明扩展入口，不得替换核心声明或另建运行时。

规则产物 Build 后拒绝作者对象/子集合写入；Session 的 partial 文件共用唯一执行栈/Drain。SamplePick 保留 Query 顺序，游戏需要排序时显式 Sort。IRandomProfile 由游戏/宿主提供版本化算法及映射，核心统一计数和快照；SessionDebugOptions 默认关闭，只读有界轨迹不进公开观察。当前 engine/ir@0.3，编译器/扩展身份进入规则身份；旧版本训练资产不能冒名复用。
