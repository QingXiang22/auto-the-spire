# 跨模块规格与验收要求

本页用于查询必须满足的行为和验收条件。规范的权威职责与文档类型分开：这些页面主要供规则查询，不代表其中全部目标已经实现。版本与支持范围另查包 manifest、实现和对应提交的 CI。

| 范围 | 要求与验收入口 |
| --- | --- |
| 产品目标、模块职责与全项目要求 | [项目规格](project.md) |
| 核心结构、执行、选择、状态、随机与兼容 | [核心接口要求](core-engine-baseline.md)、[游戏建模规格](core/game-model.md) |
| 完整单人环境、严格比较与阶段完成条件 | [环境验收](environment.md)、[实现约束](environment-constraints.md) |
| 公开协议跨结构不变量与机器资产 | [不变量](net/0.2/invariants.md)、[wire schema](net/0.2/wire.schema.json)、[覆盖 schema](net/0.2/coverage.schema.json) |
| AI 与接口的运行、资源和恢复约束 | [运行要求](ai-net/operations.md) |
| AI／net 需求到验证场景的追踪 | [共同验收](ai-net/verification.md) |

模块内的行为要求从 [base](../areas/base/README.md)、[core](../areas/core/README.md)、[net](../areas/net/README.md)、[AI](../areas/ai/README.md) 的局部索引进入。任务状态、负责人、排期与关闭结论只维护在[任务入口](../meta/roadmap.md)关联的 GitHub Issues。
