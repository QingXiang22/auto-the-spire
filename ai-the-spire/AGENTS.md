# ai-the-spire 协作约定

本文件适用于 `ai-the-spire/` 及其子目录，补充根 [AGENTS.md](../AGENTS.md)。

## 任务与交接

必须执行根[任务与文档管理强制规则](../AGENTS.md#任务与文档管理全仓库强制)。先从[总入口](../docs/README.md)查看[主清单](../docs/review/tofix/README.md)的任务ID/P优先级/依赖/完成条件，按明确派单登记会话/目录/下一成果；每线同时一个完整包。AI新实验当前暂缓，不因进入本目录、读取历史D3/D4方案或旧RESUME HERE自动训练/采样/恢复实验。

已验收工程按主清单接续，不重做管线。负责人验收环境门槛并另定分布/目标/预算后才安排新实验；当前仅执行明确授权的工程回归和公开契约兼容。交接注明适用Gym/Profile/模型身份、实际命令/产物和效果边界，更新主清单本线及证据，不复制第二套进度或以组件全绿宣称学习有效。

## 模块边界

遵守[AI spec](../docs/design/ai-the-spire/spec.md)、[策略契约](../docs/design/ai-the-spire/policy-contract.md)和[训练设计](../docs/design/ai-the-spire/training-design.md)。AI消费net公开观察/合法候选，不读隐藏状态、内部RNG/debug，不用历史缓存补提供方应发布的当前事实；保持NoSL及完整合法候选概率契约。

不修改base/net/core生产规则以迎合训练。跨模块缺口交对应主责；身份/兼容、数据划分、奖励/评价与成本证据按实际范围报告。任务范围/优先级/效果晋升由负责人决定，日志放artifacts，历史报告按根规则分流。
