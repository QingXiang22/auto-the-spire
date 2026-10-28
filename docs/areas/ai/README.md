# AI 开发入口

AI 消费 net 的公开观察与合法候选，维护编码、概率输出、采样和评价。当前先完成环境与保真验收；恢复新训练实验的条件以[任务入口](../../meta/roadmap.md)关联的 Issue 为准。

| 任务 | 入口 |
| --- | --- |
| 查询既有离线组件及其检查命令 | [AI 模块说明](../../../ai-the-spire/README.md) |
| 核对公开输入、可观察行为与范围 | [AI 规格](spec/overview.md) |
| 维护编码、字典和张量布局 | [编码器要求](spec/encoder.md) |
| 维护候选概率、模型包及错误边界 | [策略约定](spec/policy-contract.md) |
| 理解数据、训练、恢复和评估要求 | [训练设计](spec/training.md)、[运行约束](../../specs/ai-net/operations.md) |
| 理解模型与接口设计的理由 | [AI 架构](../../understand/ai-architecture.md)、[论文说明](../../understand/ai-literature.md) |
| 核对需求与验收场景 | [共同验收](../../specs/ai-net/verification.md) |

模块开发要求见 [AI 模块规则](../../../ai-the-spire/AGENTS.md)。
