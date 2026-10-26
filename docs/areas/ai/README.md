# AI 开发入口

AI 消费 net 的公开观察与合法候选，维护编码、概率输出、采样和评价。当前先完成环境与保真验收；恢复新训练实验的条件以[任务入口](../../roadmap.md)关联的 Issue 为准。

| 任务 | 入口 |
| --- | --- |
| 查询既有离线组件及其检查命令 | [AI 模块说明](../../../ai-the-spire/README.md) |
| 核对公开输入、可观察行为与范围 | [AI 规格](../../design/ai-the-spire/spec.md) |
| 维护编码、字典和张量布局 | [编码器要求](../../design/ai-the-spire/encoder-design.md) |
| 维护候选概率、模型包及错误边界 | [策略约定](../../design/ai-the-spire/policy-contract.md) |
| 理解数据、训练、恢复和评估要求 | [训练设计](../../design/ai-the-spire/training-design.md)、[运行约束](../../design/ai-net-operations.md) |
| 理解模型与接口设计的理由 | [AI 架构](../../design/ai-the-spire/architecture.md)、[论文说明](../../design/ai-the-spire/literature.md) |
| 核对需求与验收场景 | [共同验收](../../design/ai-net-verification.md) |

遵循 [AI 模块规则](../../../ai-the-spire/AGENTS.md)。既有组件和历史检查不代表完整游戏训练已经可用；本页不提供新的实验授权。
