# ai-the-spire 协作约定

本文件适用于 `ai-the-spire/` 及其子目录，遵循 [开发约定](../CONTRIBUTING.md)。

## 开发流程

遵循 [开发约定](../CONTRIBUTING.md)，近期任务见 [路线图](../docs/roadmap.md)。使用任务分支／独立 worktree，通过 PR 和受影响 CI 交付；使用方法或接口变化时更新正式文档，无需逐批交接、会话登记或本地证据链接。

## 模块边界

遵守[AI spec](../docs/design/ai-the-spire/spec.md)、[策略契约](../docs/design/ai-the-spire/policy-contract.md)和[训练设计](../docs/design/ai-the-spire/training-design.md)。AI消费net公开观察/合法候选，不读隐藏状态、内部RNG/debug，不用历史缓存补提供方应发布的当前事实；保持NoSL及完整合法候选概率契约。

不修改base/net/core生产规则以迎合训练。跨模块缺口交对应主责；身份/兼容、数据划分、奖励/评价与成本证据按实际范围报告。任务范围/优先级/效果晋升由负责人决定，日志放artifacts，历史报告按根规则分流。
