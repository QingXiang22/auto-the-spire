# 项目文档

从要完成的任务进入。初次贡献先看[新人路径](start/README.md)；团队约束见 [AGENTS.md](../AGENTS.md)，开发流程见 [CONTRIBUTING](../CONTRIBUTING.md)。规则要求与当前支持范围分别查规格、包 manifest 和对应提交的 CI。

| 要完成什么 | 入口 |
| --- | --- |
| 准备环境、运行首次检查、提交第一个改动 | [开始贡献](start/README.md)、[环境与首次检查](start/environment.md) |
| 学习 DSL 或编写 C# 宿主 | [第一份规则](start/first-rule.md)、[第一个宿主](start/first-host.md) |
| 修改卡牌、Power、遗物或其他原版规则 | [base 开发手册](development/base-spire/README.md) |
| 修改编译、通用执行或 Session 接入 | [core 开发入口](areas/core/README.md) |
| 运行公开 Host、客户端或手动试玩 | [net 开发入口](areas/net/README.md) |
| 维护公开观察编码、策略与训练接口 | [AI 开发入口](areas/ai/README.md) |
| 理解模块职责与规则执行关系 | [架构总览](understand/architecture.md) |
| 查询 DSL、操作签名或协议字段 | [参考索引](reference/README.md) |
| 查询产品、核心接口与完整环境验收要求 | [跨模块规格](specs/README.md) |
| 构建、运行检查或准备独立参考比较 | [CI 与本地检查](../tools/ci/README.md)、[参考依赖](../reference/README.md)、[差异测试工具](../tools/differential/README.md) |
| 查当前任务、里程碑与用词 | [任务入口](roadmap.md)、[术语规范](terminology.md) |

教程用于受控练习，操作指南帮助完成具体任务，参考供准确查询，解释说明关系与原因。规范标明要求的权威位置，不能据此推断功能已经实现。模块 README 保留局部索引；历史变更查 Git／PR，运行结果查 CI，任务状态只在 GitHub Issues 维护。
