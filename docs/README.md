# 项目文档

团队约束见 [AGENTS.md](../AGENTS.md)，开发流程、文档规则和临时产物管理见 [CONTRIBUTING](../CONTRIBUTING.md)，用词见[术语与表达规范](terminology.md)。近期任务见 [任务入口](roadmap.md)，产品目标见 [项目规格](design/spec.md)。版本与支持范围以包 manifest 和所审提交的 CI 为准。

## 开始开发

在仓库根目录使用 [global.json](../global.json) 指定的 .NET SDK、Python 3.12 和 PowerShell。正常构建与文档检查不需要本地原版源码；源码核对按 [参考依赖说明](../reference/README.md)准备。

| 要完成什么 | 入口 |
| --- | --- |
| 运行第一个规则与宿主 | [规则教程](dsl/core/first-rule.md)、[宿主教程](design/core-the-spire/first-host.md) |
| 修改或添加原版内容 | [base 开发手册](development/base-spire/README.md) |
| 构建及运行检查 | [CI 与本地检查](../tools/ci/README.md)、[base 开发工具](../tools/base-spire-dev/README.md) |
| 理解 DSL 组织与生命周期 | [base 架构](design/mod-the-spire/base-architecture.md)、[控制流程](design/mod-the-spire/control-flow-modules.md) |
| 运行公开 Host 与客户端 | [公开运行指南](design/net-the-spire/how-to-run-public-game.md)、[客户端参考](design/net-the-spire/public-client-reference.md) |
| 准备独立参考与差异测试 | [参考版本](../reference/README.md)、[差异测试工具](../tools/differential/README.md) |

## 查询规则与接口

| 主题 | 文档 |
| --- | --- |
| 通用 DSL | [core 作者入口](dsl/core/README.md)、[基础语言参考](dsl/core/0.1/language-reference.md)、[同包绑定与具名调用](dsl/core/0.3/authoring-reference.md)、[文本包格式](dsl/core/0.3/package-reference.md) |
| Engine API 与决定 | [Session API](design/core-the-spire/session-api-reference.md)、[活动约定](contracts/core-engine-baseline.md)、[架构决定](design/core-the-spire/core-architecture-decisions.md) |
| 游戏玩法与验收 | [base 规格](design/mod-the-spire/spec.md)、[环境验收](design/environment-completion-and-differential-v1.md)、[实现边界](design/environment-implementation-constraints.md) |
| 共享领域操作 | [操作参考](development/base-spire/reference/operations.md)、[卡牌行为参考](development/base-spire/reference/card-behavior.md) |
| 公开协议 | [net 规格](design/net-the-spire/spec.md)、[协议](design/net-the-spire/protocol.md) |
| AI 后续工程 | [AI 规格](design/ai-the-spire/spec.md)、[策略约定](design/ai-the-spire/policy-contract.md)；实际启动按总任务中的恢复条件 |

按读者需要维护教程、操作指南、参考和解释。历史改动查 Git／PR，运行结果查 CI；不再维护逐批审阅、交接、日志和任务表快照。
