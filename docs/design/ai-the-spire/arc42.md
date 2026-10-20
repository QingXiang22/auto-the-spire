# AI 与 net 架构视图导航

| 项目 | 内容 |
| --- | --- |
| 状态与版本 | IN_REVIEW；v0.1 修订 2，2026-10-01；架构导航，无实现证据 |
| 负责人及下一步 | 项目维护者；按四模块开工清单，主契约范围确认与 P-01 后进入 E0，双端检查由 N-01 完成 |

IN_REVIEW；v0.1，2026-10-01；负责人为项目维护者。本文是架构类导航，帮助评审者按问题查阅，主设计分别为 [AI 架构](design-v0.md)和 [net 架构](../net-the-spire/design.md)，不复制规范。此次建立视图映射；没有实现/性能证据，下一步是用户评审及 E0。

## 1. 引言与目标

AI 以当前观察选择合法候选，NoSL 实际游玩；优先可扩展且可验证的编码，控制部署成本。[AI 需求](spec.md)、[net 需求](../net-the-spire/spec.md)与[阶段目标](../ai-net-implementation-plan.md)规定结果。

## 2. 架构约束

AI 不访问 core 私有状态、不维护公开历史缓存；提供方给当前有效已知位置；固定 Gym；完整候选概率输出；数值与版本显式。[编码约束](encoder-design.md)和[公开协议](../net-the-spire/protocol.md)是实现契约。

## 3. 系统范围与上下文

core/mod 拥有规则，net 拥有公开投影及控制，AI 拥有表示/学习/选择，用户拥有目标与晋升。[项目边界](../spec.md)及 [net 组件图](../net-the-spire/design.md#1-组件与依赖)说明依赖。

## 4. 解决方案策略

先公开 fixtures 与确定性层，再模拟闭环，最后训练/真实接入。共享稀疏/集合表示为低成本实验提案，Transformer 保留比较空间。[AI 主设计](design-v0.md#9-低部署成本学习基线提案)给取舍。

## 5. 构建块视图

[AI 模块布局](design-v0.md#8-可实施的模块布局)与 [net 组件目录](../net-the-spire/design.md#1-组件与依赖)列出职责、拥有数据、依赖和禁止跨越的边界。

## 6. 运行时视图

[正常/失败决策路径](design-v0.md#11-运行与失效视图)、[请求生命周期](../net-the-spire/protocol.md#5-生命周期与恢复)说明选择、执行、结算、重发和 unknown 时的响应。

## 7. 部署视图

[运行形态](../ai-net-operations.md#1-部署形态与配置)为本机 Python/.NET 子进程、离线训练/评估和未来真实适配，不要求先建远程服务。

## 8. 横切概念

身份/目录、知识状态、十进制值、句柄、mask、完整快照、事实/奖励分离分别归[协议](../net-the-spire/protocol.md)、[编码](encoder-design.md)、[训练数据](training-design.md)。记录不成为历史模型输入。

## 9. 架构决策

[D-01 到 D-06](design-v0.md#12-设计决策记录)记录驱动、替代方案、后果和验收；明确用户约束与待批准提案。

## 10. 质量需求

[质量目标和门槛](../ai-net-verification.md#2-质量目标与门槛)把可见性、执行、扩展、概率、学习及 CPU 成本分别度量，不猜预算。

## 11. 风险与技术债

[提供方依赖](../net-the-spire/design.md#2-当前已知信息的来源)、与[待决项](../ai-net-implementation-plan.md#6-待维护者决定的事项)列出当前缺口及阶段阻塞范围。

## 12. 词汇表

Observation 是玩家当前可观测事实；PublicCatalog 是 net 的结构/语义声明；GymManifest 是 AI 的固定索引与变换；Gym 是固定规则/观察/动作/任务配置；Candidate 是当前可执行选择；Policy 是候选概率；Q 是特定后续策略的动作收益估计；Executor 是按行为配置选择并提交的组件。以上均为拟议类型，不等同现有 core record。
