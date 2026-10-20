# ai-the-spire 协作约定

本文件适用于 `ai-the-spire/` 及其子目录，遵循根 [AGENTS.md](../AGENTS.md)、[开发约定](../CONTRIBUTING.md)和[术语规范](../docs/terminology.md)。

## 开发流程

遵循 [开发约定](../CONTRIBUTING.md)，近期任务见 [路线图](../docs/roadmap.md)。使用任务分支／独立 worktree，通过 PR 和受影响 CI 交付；使用方法或接口变化时更新正式文档，无需逐批交接、会话登记或本地验证记录链接。

## 模块边界

遵守[AI spec](../docs/design/ai-the-spire/spec.md)、[策略约定](../docs/design/ai-the-spire/policy-contract.md)和[训练设计](../docs/design/ai-the-spire/training-design.md)。AI消费net公开观察/合法候选，不读隐藏状态、内部RNG/debug，不用历史缓存补提供方应发布的当前事实；保持NoSL及完整合法候选概率约定。

不修改base/net/core生产规则以迎合训练。跨模块缺口交对应主责；包版本及摘要、兼容性、数据划分、奖励／评价与运行成本按实际范围报告。任务范围、优先级及模型采用条件在工单维护；日志使用 CI 或本地临时目录。
