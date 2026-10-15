# core/DSL 与跨模块设计评审采纳交接（2026-10-01）

> 历史证据（2026-10-03整理）：正文保留原时点结果，历史“下一步”不再作为当前派单指令。本报告归属W-02、W-04、W-06、W-08，未结项转入[当前清单](../../tofix/README.md)；移入records不表示修复或验收通过。逐份归属见[记录索引](../README.md)。

## 1. 范围与状态

用户要求阅读当前相关 review、判断值得采纳的意见并修改设计。本轮按已确认目标判断：core 是通用桌游运行时，STS 模型/规则归游戏包，Lua 可处理复杂算法；AI 消费当前可知观察、固定 Gym、输出候选概率，实际游玩 NoSL。

盘点原有9份报告，重点核对 core/DSL、新 AI/net 评审与已有 encoder 处置；参考模拟器报告只抽取版本/随机保真和验证原则。本轮是**设计修订**，不实施规则、协议或 Lua 后端；PROPOSED/IN_REVIEW 不代表解析器接受、包能注册或行为验证。C-01 机器资产仍未交付，协议尚未冻结。

同名 `design-review-disposition-2026-10-01.md` 在本轮期间被其他工作更新，已保留其内容；本轮 core/DSL 与实际修改清单单独记录在本文。其 [AI/net 意见处置](design-review-disposition-2026-10-01.md)可作为补充审阅，不能把任何一份交接的检查结果推定为运行时已实现。

## 2. 各报告的判断

| 报告 | 判断 |
| --- | --- |
| [core/base 架构与 DSL](core-base-architecture-dsl-2026-10-01.md) | 调度、查询、IR 和分配问题有效；允许 core 保留 STS 机制的建议不符合最新目标 |
| [通用引擎交接](../../archived/generic-engine-design-handoff-2026-10-01.md) | 不是独立缺陷评审；保留三层边界、纯 Compute、临时槽与玩法状态分离、非 STS 原型验收 |
| [AGENTS 合规审计](../../archived/agents-md-conformance-2026-09-30.md) | 采纳四态、公共节点形状/X费/RNG 迁移说明、检查入口登记；旧失败数不是今天的清点 |
| [encoder 原评审](ai-encoder-design-review-2026-10-01.md)及[已有处置](ai-encoder-review-disposition-2026-10-01.md) | 保持 B 类契约细化与已作修正；来源风险归提供方，不恢复 known_top 或 AI 历史器，不以 fixtures 当真实来源证据 |
| [encoder 架构交接](../../archived/ai-encoder-architecture-2026-10-01.md)及[AI/net 规划交接](../../archived/ai-net-planning-2026-10-01.md) | 保留原时点记录；新增统一规范资产前置，明确来源/覆盖交付与阶段时序，不扩大 E0 到学习/真实控制 |
| [AI/net 新评审](ai-net-design-review-2026-10-01.md) | 大多数契约缺口有效；不采纳配对必须跨策略随机对齐、自动放宽固定 Gym 身份或让 core 内置牌区的推论 |
| [sts2-sim 保真差异](sts2-sim-补全与保真差异-2026-10-01.md) | 采纳版本化来源、无版本归档风险、待排查与确诊分开；参考模拟器不成为原版权威，不自动移植其修复 |

## 3. core、DSL 与 Lua 的采纳

目标主稿为 [通用引擎修订2](../../../design/core-the-spire/generic-tabletop-engine-design.md)，同步 [core spec](../../../design/core-the-spire/spec.md) 与 [mod spec](../../../design/mod-the-spire/spec.md)。

| 原意见/问题 | 设计处理与关闭条件 |
| --- | --- |
| A1 native 重入乱序 | 唯一调度器/显式子帧；G1 验证 `[1,2,20]`。当前运行时问题未修复 |
| A2 查询可写、读取消费计数 | 纯程序调用图能力校验，消费另用命令，明确实时读取/局部捕获；Observe/LegalActions 多读不改变状态/RNG。纯查询超限也不能写 Faulted |
| A3 core/base 边界 | 全部 STS 模型/生命周期/奖励策略归游戏包，不只提取默认参数；拒绝 core 中的伤害、牌区、力量/敏捷特例 |
| A4 Power/事件特例与日志耦合 | 包定义纯修正/生命周期/反应；监听候选快照、确定顺序与执行时重查；本次新增监听不参与当前事件，日志过滤不关闭规则事件 |
| A5 身份与恢复 | 完整规则指纹与内部快照目标；公开 load/fork 和训练分叉不提前开放，不以旧搜索建议改变 NoSL |
| 新语法绕旧位置调用、多份原语清单 | 统一签名直接生成通用 IR，签名包含能力/求值/错误/计费；IR 使用独立版本，实际前端迁移待原型 |
| 数值表达/目标域不足 | 纯表达式组合，目标查询与响应保留类型/域；不得降成丢域的 SelectedTarget，G5 验证 allies/opponents |
| 完整升级却运行时继承 | 分别完整编译，G5 检查 upgraded 空 hooks 不回退；模板仅显式编译期展开 |
| 重复赋值/X费/节点包装 | 保留后写覆盖和执行保序，双位置诊断；明确编译形状、捕获/保留时点和 RNG 影响，结构与行为断言分开 |
| 禁 while 仍可能失控 | 有限循环用迭代帧，不按次数展开全部帧；事件/实例/候选/Compute 均计费；advance 超限显式故障，不静默截断 |
| 引用与生命周期 | 销毁策略检查集合/关系/帧引用，失效决策需换版本；不由悬挂引用的宿主异常决定规则 |
| 高频训练效率 | advance/projection/单动作校验/trace 分离；覆盖反应、选择、大候选与 Compute 后 profile。单敌空过回合短测不能证明整体高效，不先引入 ECS/JIT/COW |
| Lua 复杂算法 | 保留用户需求与纯输入输出方案；隔离、配额、数值、marshal 总成本待 G4；不把 DSL 临时槽变持久字段，不宣称 Lua 自动更快 |
| 参考后端与 golden | `sts2/` 版本化来源为准，旧 core/reference 辅助差分；记录 PRNG/派生/候选池/消费点。无版本归档冲突不只靠重录期望解决 |

安装 release_info 与恢复日志是来源线索，不能只凭相同版本字符串推定恢复文件逐字一致；后续记录源码/程序集摘要。源码未找到不证明内容不存在，116项钩子差异不是116个确诊缺陷。本轮不复核参考模拟器内容修复。

DSL 0.0.1 作者文档新增“设计目标/解析器接受/内容包能注册/规则行为已验证”说明；旧 content/combat 文档明确 STS core 所有权只是兼容实现。通用 schema/procedure/Compute 示例不是该版本新增可执行语法。

## 4. 跨模块修订及不同意的推论

新增 [契约资产设计](../../../design/net-the-spire/contract-artifacts.md)，同步 [协议](../../../design/net-the-spire/protocol.md)、[实施计划](../../../design/ai-net-implementation-plan.md)与[验收](../../../design/ai-net-verification.md)：

- F-01/F-14：统一 schema/不变量/规范化/decimal 资产，纯 contracts 与活动 client 分开；C-01 前置是本稿工程提案，双端最小原型并非独立 Python 编码的逻辑必需。现行计划选择先验证两端，资产还未实现；没有仅靠散文就声称机器契约已闭合。
- F-02/F-03/F-04/F-17：无序 wire 按公开键排序，五张牌 fixture 统一 C@0/D@3；position 是 `[0,total_count)` 的绝对位置。pile_types 通过唯一 kind_id 保留种类，以互斥组/作用域引用检查成员约束；不成为 core 牌区模型，不冗余填 relations/groups/sequences。
- F-05/F-18/F-19/F-20：验收矩阵是映射维护源，20条需求与场景一致、全部16个任务有追踪；增加 V-16 模型变换重评；目标/预算/晋升要求统一 E2 前；外部 choices 无 padding，内部补齐概率为0。
- F-06/F-07/F-08/F-15：声明终局原因、结构化场景/能力/limits 与提供方检查引用；机制→事实→载体→来源→验证覆盖表是 N-02 产物。V-15 检查公开决策事实不被错误合并，不能证明所有游戏 Markov，也不能要求揭示隐藏状态。
- F-11/F-12/F-13/F-16/F-21：session_id 全 worker/重启不复用；候选按公开动作语义键排序并对齐采样；轨迹整份保存或内容摘要存储，不依赖临时账本；字段省略仅 absent；net/AI 的协议规模和张量规模分别计数。
- F-22/F-23：修复历史报告24个 Markdown #L 锚点（标签保留审计行号）与阶段评审一处尾空白，明确既有检查入口；arc42/研究资料补状态表。CI 纳入 C-01 后续实施，没有宣称 CI 已存在。

F-09 不自动放宽 fixed Gym 的全目录 exact identity：仅声明子集一致不足以证明规则/分布兼容；E3 按冻结批次记录重评/重训成本，场景闭包子目录以后另议。

F-10 不采纳“不同策略随机流不对齐就不能配对”：可按相同初始场景/种子/重复编号比较差值，但不保证方差降低。训练第5节明确配对单位、场景簇、环境/执行 RNG 和实测差值方差；固定动作重放与跨策略公共随机数分开，不改变 STS 原版随机流。配对差值口径核查了 [NIST 原始说明](https://www.itl.nist.gov/div898/handbook/prc/section3/prc311.htm)，种子匹配是本项目实验设计判断。

## 5. 文件与迁移清单

- core/mod：generic-tabletop-engine-design、core spec、mod spec。
- DSL 文档：dsl-usage/content-dsl/combat-dsl-design，补状态与目标边界，没有改原语/解析器。
- net：新增 contract-artifacts，修改 protocol/design/spec。
- AI：spec/design-v0/encoder-design/policy-contract/training-design/arc42/literature。
- 跨模块：ai-net-implementation-plan/ai-net-verification/ai-net-operations、docs/README。
- 证据记录：agents-md-conformance 的24个 Markdown 链接和定位说明、project-progress-review 一处尾空白，历史数值/结论不重写；原 core 与 AI/net 评审补处置入口；本文。

公共契约迁移：目录新增 pile_types/terminal_reasons，fields.applies_to 新增 terminal_reasons，conditional 新增 visibility_check_ids；两端 contracts/目录/Manifest/fixtures 共同迁移，最小目录样例已同步。Pile wire 结构保留，种类经其实体 kind_id 查表；互斥作用域是声明字段。候选从未约束顺序改为规范公开语义顺序，candidate_id 仍仅在当前快照有效。

没有修改生产代码、solution、依赖或内容，没有提交/移动他人文件。新出现的 Engine 与 LuaBackendProbe 路径属于其他工作，不将其存在视作本轮实现/验证证据；同名 AI/net 处置报告的外部更新也保留。

## 6. 验证与未完成项

初次检查的25条历史 Markdown 链接/空白问题已修正；临时缺失的报告链接在交接建立后闭合。最终文档检查结果另在本节记录，不能替代双端 codec、投影机制或规则验证。

- `python tools/doc-check/check_trace.py .`：20/20需求场景一致，原7条不一致为0，exit 0。
- 临时只读 Python 核对：协议两份完整 JSON 的身份/声明/字段适用域一致；16/16 C/A/N/L任务有验收追踪，16个场景定义且无 spec 悬空场景引用。不是未来机器 schema 的校验。
- `git diff --check`：exit 0，仅既有 LF/CRLF 提示。未跟踪文档的尾空白由全量文档检查覆盖。

未执行构建、规则检查、运行时内容加载、Lua 示例、通用游戏原型、训练或性能实验：本轮未改对应代码/注册路径。旧报告的构建/规则失败/小场景性能保留原时点，不转为新设计证据。

仍需 G1 执行/纯查询/预算/引用、G2/G3 非 STS 通用性、G4 Lua 隔离/配额/数值/成本、G5 STS 目标/升级/X费/时序/RNG 差分；C-01机器资产、E1来源/可见性/幂等、E2覆盖/学习/资源与真实后端能力分别验证。预算/奖励/绑定默认值仍待相应阶段决定。

所有混合问题报告保持 tofix，不能仅因本轮已采纳设计建议整体归档。
