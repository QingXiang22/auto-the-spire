# ai-the-spire 模型与概率输出约定

本文供模型与执行器实现者查询概率输出、候选对齐和模型包约束。设计表记录 2026-10-01 时点；实际模型交付与验收以[GitHub 任务清单](https://github.com/QingXiang22/auto-the-spire/issues/28)为准。

| 项目 | 内容 |
| --- | --- |
| 状态与版本 | IN_REVIEW；v0.1，2026-10-01；尚无模型实现 |
| 负责人、读者 | 项目维护者；编码器、评分器、执行器及模型包实现者 |
| 范围与变化 | 新增内部模型接口、公开概率返回、可选收益头及部署包约束 |
| 依据与决策 | [AI 规范](spec.md)；不生成单一动作；具体包格式和容差待评审 |
| 下一步 | 先实现约定 fixtures，再在学习阶段接入可学习模块 |

## 1. 调用边界

外部调用为 `predict(snapshot, model_bundle) -> PolicyEvaluation`，只接收[net 当前快照](../net-the-spire/protocol.md)。内部依次调用 validate/tensorize、`encoder.forward(T) -> EncodedObservation`、`scorer.forward(EncodedObservation, CandidateTable) -> logits`、有效候选归一化；不执行环境动作。

EncodedObservation 包含 global `[B,dg]`、entities `[B,E,de]`、必要的关系/序列表示和 masks；引用映射仅为侧信息。不同模块可使用不同维度，但模型包必须声明 dg/de、输入布局版本和 scorer 所需通道。不允许只保留 global 后丢失候选引用实体；也不允许把实例行号作为学到的位置。端到端训练可共同更新 encoder/scorer，接口解耦不意味着任意权重可拼接。

Scorer 对每个完整候选产生一个 logit；候选编码的字段/引用角色都必须影响评分路径，不能靠 candidate_id 或列表下标评分。真正的公开位置可作为特征，无序实体和候选必须分别满足置换等变。终止空候选返回 terminal 结果，不进行 softmax。

## 2. 返回结构与不变量

`PolicyEvaluation = {policy_contract_version, model_id, gym_id, dictionary_version, session_id, observation_id, status, choices, return_schema?}`。status 为 decision/terminal。choices 为 `{candidate_id, probability, return_estimate?}` 列表；顺序建议与输入一致，语义按句柄对齐。

decision 时每个当前候选恰有一项、没有额外项，probability 为有限、非负浮点数，所有有效项总和在配置的 normalization_tolerance 内等于 1。tensor 内 padding=0；外部 choices 不带 padding。容差必须为有限正值且足够小，随推理配置保存，不能自动放宽以掩盖错误；批准的数值阈值由精度实验确定。

softmax 在减去有效项最大 logit 后计算，masked 项不参与最大值及归一化求和。有效 logit 非有限、标识不符、概率失败或超限则整次预测失败，不返回“可用的前半部分”。单候选概率为 1；terminal 的 choices=[]。不要求所有合法项有严格正概率，但训练中若采样策略对其置零，应如实记录该行为支持集。

执行器检查版本和句柄后才决定采样或 argmax。采样温度、混入均匀探索或其他改动形成新的实际行为分布 μ；日志必须记录 μ(a|o)，不能把原始 π(a|o) 当实际行为概率。argmax 的实际行为是确定分布；对齐项相等的处理方式在 ExecutorProfile 中固定，不按不透明 ID 字符串择优。为避免列表顺序成为偏好，建议等概率并列采样并记录执行 RNG；评估时使用固定执行配置及 RNG 种子。

执行器按[公开动作语义键](../net-the-spire/contract-artifacts.md#3-公开数组与动作语义键)规范对齐概率后采样。相同候选语义/概率和执行 RNG 下，输入排列或 candidate_id 换名不改变选中的动作语义；规范排序仅控制可复现性，行号和键字节不进入评分特征。V-11 同时检查这个性质与外部概率集合，不能只检查按句柄对齐的概率。

概率不是动作收益、偏好置信度或通关概率。只有当前选择概率和可选收益属于该输出，真正执行由独立的 `select(evaluation, executor_profile)` 与 net.step 完成。

## 3. 可选收益向量

return_schema 必须声明 reward_profile_id、component_ids/单位/尺度、折扣与时间单位、horizon（例如当前战斗或整局）、continuation_policy_id 及估计方法版本。每个候选的 return_estimate 按该顺序给出有限向量。语义为“现在执行该动作，随后按声明策略决策”的预期收益；若后续执行器改变分布，不能还标原策略版本。

某个维度无训练依据时关闭整项可选头，不能填零假装预测。初版训练可只使用内部状态价值 V(o) 做优化，不返回 Q(o,a)。内部 V 不能复制给所有候选后称为动作收益。即使某分量包含最终胜负，未完成独立校准评估前也不能称为可信通关概率。

## 4. 模型包

一个 ModelBundle 由 metadata.json、Manifest 与变换配置、模型结构配置、权重和评估摘要组成，内容校验摘要绑定全部文件。它必须声明 net exact identity、训练 scenario_profile、所需字段能力、gym_id/dictionary_version、张量布局、encoder/scorer 接口、网络精度、reward/training/executor profile 标识、可选收益语义和训练来源范围。

加载时先校验文件摘要和兼容性，再创建模型；不自动补字典、不猜维度、不把 unavailable 当训练时已知值。仅从可信本地构建产物加载；默认权重格式不得执行任意对象反序列化代码，具体训练/导出格式在学习阶段选定。部署仅携带推理所需权重，优化器与经验数据保留在训练 checkpoint 中；量化/蒸馏后视为新模型并重新评估。

一局固定一个 model_id、Manifest 和 ExecutorProfile；候选晋升后仅在新局切换。终止和用户停止不调用模型继续操作。模型错误默认停止控制；另行启用随机基线时必须显式切换并记录，不静默以随机动作掩盖错误。

## 5. 错误与验收

沿用编码器的结构错误，追加 incompatible_model、invalid_logits、invalid_probability、stale_evaluation、unsupported_return_schema。错误包含阶段、输入路径与当前标识，不暴露隐藏数据。输出约定以[共同验收 V-11](../ai-net-verification.md#3-验收场景目录)为主；实例重命名、候选重排、不同 padding 长度、单候选和终止空集合均须覆盖。网络概率比较使用声明的数值容差，引用和候选集合匹配是精确检查。
