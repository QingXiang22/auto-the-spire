# ai-the-spire 与接口整体规划交接

> 2026-10-01 归档说明：整体规划文档交接已完成；开放实现、训练偏好和部署预算转入现行计划及评审处置，不要求此规划报告等到所有未来阶段完成才归档。 详见[评审处置与剩余任务](../records/2026-10-01/review-followup-2026-10-01.md)。

日期：2026-10-01。状态：IN_REVIEW；仅文档交付，尚未批准为编码基线。负责人为项目维护者。用户要求完成本轮编码方案修订，补齐两个模块的 spec、有效架构和外部约束，后续编码交给小模型。

## 1. 阅读入口与文件变更

总入口为[文档导航](../../README.md)，实施入口为[分批计划](../../design/ai-net-implementation-plan.md)。当前 AI spec v0.2、主设计 v1.3、编码契约 v0.4；新增 net spec/design/protocol v0.1、模型/训练细化和共同验收/运行文档。

| 文件 | 本次改动 |
| --- | --- |
| 根 README.md、docs/README.md | 增加模块实现状态、统一导航和权威边界 |
| docs/design/spec.md | 仅增加模块规范、跨模块计划与验收链接；不改规则契约 |
| docs/design/ai-the-spire/spec.md | 需求扩展至训练/评估/部署，保留用户约束，增加 AI-01 至 AI-10 |
| docs/design/ai-the-spire/design-v0.md | 保留路径，扩展主架构、模块/运行视图、共享稀疏基线和 D-01 至 D-06 |
| docs/design/ai-the-spire/encoder-design.md | 分离目录/字典，对齐 net 快照，补具名版本/decimal/枚举/数组通道 |
| docs/design/ai-the-spire/policy-contract.md | 新增概率、可选收益、执行分布、模型包和错误契约 |
| docs/design/ai-the-spire/training-design.md | 新增短任务实验、奖励/偏好配置、轨迹/终止/故障、评估/发布 |
| docs/design/ai-the-spire/arc42.md、literature.md | 架构视图导航，研究说明及历史条件标签 |
| docs/design/net-the-spire/spec.md | 替换占位，增加 NET-01 至 NET-10、提供方责任和阶段范围 |
| docs/design/net-the-spire/design.md | 新增公开源、投影、句柄、会话、账本和 stdio 架构 |
| docs/design/net-the-spire/protocol.md | 新增声明/完整快照/控制/错误/生命周期/演进和配套 JSON 示例 |
| docs/design/ai-net-implementation-plan.md | 新增 E0 至 E4、A/N/L 任务、依赖及编码验收 |
| docs/design/ai-net-verification.md | 新增 V-01 至 V-14、需求追踪和各证据层级 |
| docs/design/ai-net-operations.md | 新增运行配置、资源指标、停止/恢复与产物管理 |
| docs/review/tofix/ai-encoder-architecture-2026-10-01.md、ai-encoder-review-disposition-2026-10-01.md | 原时点记录保留，增加当前版本与整体交接入口 |
| docs/review/tofix/ai-encoder-design-review-2026-10-01.md | 仅修正两处断链及增加整理注；不改其他模型评审判断 |
| 本报告 | 交付清单、决策/残余问题和本轮文档验证 |

没有修改 AI/net 代码、内核、DSL 或内容。已有未提交改动保留，不提交、不清理、不移动他人报告。

## 2. 关键决策与迁移约束

用户确认：NoSL；输入为当前可观测信息；有效公开历史体现为当前变量/关系/卡牌已知位置；策略输出全部合法候选概率；收益头可关闭；固定 Gym；允许只用内容身份和反馈学习；部署成本优先。

提供方 PublicCatalog 与 AI GymManifest 分离。net 的规则/目录/后端身份进入快照；模型字典和归一化仅在 AI 保存。早稿 schema_version/ruleset_version 以具名 protocol_version/ruleset_id/catalog_id/backend_profile_id/tensor_layout_version 取代，消费端不得混用双重别名。wire number 用精确十进制字符串，AI 再按配置变换与容差转浮点。

抽牌堆仍为 visible_members、total_count、members_complete、known_positions；当前位置顶部 0，可离散，不引入 known_top 或 AI 历史器。派生 net:pile_member/net:pile_total_count/net:pile_members_complete 在目录声明，wire 不重复表达这些事实。提供方所需公开状态缺口是 E1/真实接入依赖，不阻塞 E0 的独立 fixtures。

step 绑定 session/observation/candidate；活跃请求账本去重，unknown 结果暂停查询，不自动重试真实操作。低成本集合/稀疏网络和短任务 PPO 为待评审基线，未定奖励权重/预算不能由编码实现者代填。Q 与可信通关概率不是首版依赖。

## 3. 对其他模型评审的判断

详细处置见[逐项评估](../records/2026-10-01/ai-encoder-review-disposition-2026-10-01.md)。采纳字典持久化、内容/补齐掩码、批处理、错误、类型/引用和明确位置等建议；隐藏排列泄漏及候选映射是有效提供方风险。不同意把提供方完成度变为独立 AI 编码器 fixtures 的前置阻塞，不强制删除核心有序数据，也不要求 AI 维护历史或重写规则。保留原评审的未解决依赖，不整体归档。

## 4. 残余问题与关闭条件

| 问题 | 阶段与负责人 | 关闭证据 |
| --- | --- | --- |
| 编码语言/布局、版本与精度容差 | E0 前，维护者 | 接受的文档版本与 EncodingConfig |
| 当前已知事实来源、切片覆盖、公开身份 | E1 前，net/core 负责人 | 机制到字段映射、V-02/V-03 的真实模拟检查 |
| 低成本网络容量与目标机器预算 | E2 前，维护者/AI 实现者 | 批准预算和典型/极端 CPU 基准 |
| 奖励向量、偏好、时间范围和训练分布 | E2 训练前，维护者 | 完整 Training/Reward/PreferenceProfile |
| 晋升规则、整局与真实覆盖 | E2 发布/E3/E4，维护者和规则/适配实现者 | 独立评估、规则范围和真实差分 |

本次提供可审查方案，不把文档完成当作上述问题已解决。交接后按[第一批任务](../../design/ai-net-implementation-plan.md#2-第一批给编码实现者的任务)做独立编码，不新增历史记录器、神经网络或 core 直连。

## 5. 验证记录

- 全仓库 README 与 docs 的 Markdown 检查：35 份文件、452 个本地引用、4 个 JSON 样例，文件目标及普通标题锚点无错误，围栏闭合。32 个既有历史 `#L...` 定位保留，按审计时源码定位处理，不宣称它们是当前有效标题锚点。
- 协议示例交叉检查：最小 PublicCatalog 与 PublicSnapshot 的身份、实体种类、动作类型、字段注册及整数十进制字符串匹配；通过。这只验证文档样例，不等于 C#/Python codec 已实现。
- 需求追踪：20 个 AI/NET 需求均出现在验收矩阵，引用的 V-01 至 V-14 存在；通过。场景仍是待执行计划。
- 空白检查：`git diff --check` 返回 0；对本轮 20 份新增/改动文档执行逐文件 no-index 检查并逐行检查行末空白，无诊断。no-index 检查配置 `core.autocrlf=false`、`core.whitespace=blank-at-eol,blank-at-eof,space-before-tab,cr-at-eol`，保留既有 Windows 行尾，不将 CRLF 的 CR 误判为空白。首次检查器的默认 GBK 解码和行尾处理有问题，改为显式 UTF-8 与上述配置后重跑成功；未改他人源码行尾。
- 手工一致性核对：统一 net 公开语义与 AI 数值字典的职责、具名版本和派生牌堆事实；首个学习方案改为 PPO 提案；短任务发布不要求先完成真实整局，真实差分仅在对应部署阶段要求。
- 构建 `dotnet build AutoTheSpire.sln`：未执行，文档改动不注册/修改项目代码。
- 规则检查 `dotnet run --project core-the-spire/Core.TheSpire.Tests`、运行时内容加载：未执行，未改变规则、DSL 或内容注册。
- AI 编码器/协议的可执行契约、学习实验、真实适配和部署成本基准：未执行；当前 AI/net 尚无实现，不将计划或零测试视作通过。

文档按仓库层级组织为轻量设计包，保持 IN_REVIEW，尚未进入 construction。没有宣称通过 workflow-b 固定文件名模板的设计就绪校验或已获人工批准；各阶段退出证据按共同验收执行。
