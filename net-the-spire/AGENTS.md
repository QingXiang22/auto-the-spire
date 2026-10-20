# net-the-spire 协作约定

本文件适用于 `net-the-spire/` 及其子目录，补充根 [AGENTS.md](../AGENTS.md)。

## 开发流程

遵循 [开发约定](../CONTRIBUTING.md)，近期任务见 [路线图](../docs/roadmap.md)。使用任务分支／独立 worktree，通过 PR 和受影响 CI 交付；使用方法或接口变化时更新正式文档，无需逐批交接、会话登记或本地证据链接。

## 模块边界

本线负责net、公开Host/契约/codec/schema/client及相应文档，遵守[net spec](../docs/design/net-the-spire/spec.md)、[协议](../docs/design/net-the-spire/protocol.md)和[ENV实现约束](../docs/design/environment-implementation-constraints.md)。消费base配置好但未Start的独立Session并启动一次，不另造默认随机Session或重算玩法；A/B跨线需求交文件主责修改。

模拟侧游戏语义来自规则包/内核公开输出，本体适配器对齐同一公开契约。按[公开语义透传的实施边界](../docs/design/net-the-spire/design.md#7-2026-10-07公开语义透传的源码核对与接续)迁移正式Profile，不逐项重登记mod字段/动作或重判可见性；保持通用编码、公开句柄、账本和版本核对，不用直接序列化State代替公开出口。

版本化变更同批更新C#/Python/schema及载荷证据；保留适用旧契约。公开观察/完整候选来自Engine稳定边界，隐藏状态/RNG/debug不进公开输入，不静默丢字段/截断候选，不让AI从历史补公开事实。局部net检查不能替代同构建公开全程与独立对拍验收。不得自行修改冻结core、参考玩法或启动训练。
