# net-the-spire 协作约定

本文件适用于 `net-the-spire/` 及其子目录，补充根 [AGENTS.md](../AGENTS.md)。

## 任务与交接

必须执行根[任务与文档管理强制规则](../AGENTS.md#任务与文档管理全仓库强制)。先读[总入口](../docs/README.md)、[主清单](../docs/review/tofix/README.md)本线已就绪任务/P优先级和[C提示词](../docs/design/agent-goals.md#5-cnet-the-spire-编码-agent)，登记任务ID/会话/目录/下一可验成果；每线同时一个完整包。优先级、父项关闭和范围身份交负责人，不把旧报告的“下一步”当派单。

只维护本线任务行与综合交接；实际命令/构建/配置/seed、协议变化、公开轨迹与未验证范围必须可核对。模块README写使用方法，当前进度只在主清单，长日志放artifacts；报告分流遵守根规则。

## 模块边界

本线负责net、公开Host/契约/codec/schema/client及相应文档，遵守[net spec](../docs/design/net-the-spire/spec.md)、[协议](../docs/design/net-the-spire/protocol.md)和[ENV实现约束](../docs/design/environment-implementation-constraints.md)。消费base配置好但未Start的独立Session并启动一次，不另造默认随机Session或重算玩法；A/B跨线需求交文件主责修改。

模拟侧游戏语义来自规则包/内核公开输出，本体适配器对齐同一公开契约。按[公开语义透传的实施边界](../docs/design/net-the-spire/design.md#7-2026-10-07公开语义透传的源码核对与接续)迁移正式Profile，不逐项重登记mod字段/动作或重判可见性；保持通用编码、公开句柄、账本和版本核对，不用直接序列化State代替公开出口。

版本化变更同批更新C#/Python/schema及载荷证据；保留适用旧契约。公开观察/完整候选来自Engine稳定边界，隐藏状态/RNG/debug不进公开输入，不静默丢字段/截断候选，不让AI从历史补公开事实。局部net检查不能替代同构建公开全程与独立对拍验收。不得自行修改冻结core、参考玩法或启动训练。
