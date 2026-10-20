# ai-the-spire 架构相关论文储备

| 项目 | 内容 |
| --- | --- |
| 状态与版本 | REFERENCE；修订 2，2026-10-01；历史研究资料，不能作为实现或设计批准验证记录 |
| 负责人及权威边界 | 项目维护者；当前要求以模块 spec/设计主稿为准，论文不证明项目效果 |

检索日期：2026-09-26。本文服务于 [项目规范](../spec.md) 的后续设计讨论，不把论文中的结果直接当作《杀戮尖塔 2》的性能或胜率保证。

2026-10-01 更新说明：以下问题保留原检索时的背景，资源数值与目标优先级不视为本轮确认要求。当前架构与首阶段范围以 [模块规范](spec.md)、[技术设计主稿](architecture.md)和[编码器设计](encoder-design.md)为准；可学习网络与训练算法尚未定案。

## 原检索时的背景（非当前实现要求）

- AI 控制战斗、选牌、路线、商店等整局决策；每个模型可以只服务一个角色及卡池。
- 部署于真实游戏，无 GPU，AI 内存预算约 8 GB，每次决策须在 5 秒内返回。
- 输入为 `Observation` 及当前合法动作，不读取未公开的内核状态。
- 训练后仍持续收集真实游戏经验；核心评价是通关率和平均可达层数。
- 希望保留各项结果，允许以后调整奖励设计。

## 优先阅读：直接影响接口与架构

| 论文 | 论文研究了什么 | 对本项目的启发与边界 |
| --- | --- | --- |
| [Playing Various Strategies in Dominion with Deep Reinforcement Learning](https://ojs.aaai.org/index.php/AIIDE/article/view/27518)（Gerigk & Engels，2023） | 用多重集合表示牌组构筑游戏，并将 Soft Actor-Critic 适配到可变大小的动作集合。 | **最接近的游戏类比。** 值得研究它怎样编码卡牌与逐步选择动作；Dominion 的规则、目标和可观测性不同，不能外推胜率或训练成本。 |
| [A Closer Look at Invalid Action Masking in Policy Gradient Algorithms](https://arxiv.org/abs/2006.14171)（Huang & Ontañón，2020） | 从理论与实验角度研究策略梯度中的非法动作屏蔽。 | `net-the-spire` 应明确提供合法动作集合，使模型只在当前决策点的有效动作中选择。屏蔽动作本身并不教会模型游戏规则。 |
| [Deep Sets](https://papers.neurips.cc/paper_files/paper/2017/hash/f22e4747da1aa27e363d86d40ff442fe-Abstract.html)（Zaheer et al.，2017） | 建模顺序不影响结果的可变大小集合。 | 手牌以外的某些卡牌、遗物、敌人集合可用集合编码；有语义的顺序、实例标识和位置仍须单独表示。是否采用，要和更简单的固定特征基线实测比较。 |
| [Deep Recurrent Q-Learning for Partially Observable MDPs](https://arxiv.org/abs/1507.06527)（Hausknecht & Stone，2015） | 研究在部分可观测任务中用循环网络整合过去信息。 | 当前设计要求公开历史的有效影响由当前观察变量和已知序列反映，AI 编码器不额外维护历史。该论文保留为其他部分可观测场景的研究依据，不是当前编码器实现循环记忆的要求。 |
| [Policy Invariance Under Reward Transformations](https://ai.stanford.edu/~ang/papers/shaping-icml99.pdf)（Ng, Harada & Russell，1999） | 给出在其假设下保持最优策略不变的势函数奖励塑形条件。 | 加密集反馈前先检查它是否改变了通关目标；“伤害加分”“出牌加分”等任意奖励可能诱发错误偏好。理论条件须按我们的整局任务、终局和折扣方式核对。 |
| [A Generalized Algorithm for Multi-Objective Reinforcement Learning and Policy Adaptation](https://proceedings.neurips.cc/paper_files/paper/2019/file/4a46fbfca3f1465a27b210f4bdfe6ab3-Paper.pdf)（Yang et al.，2019） | 用向量价值函数及偏好条件化学习多目标策略。 | 说明“记录奖励向量”和“训练可随权重切换的策略”是两个不同层级。首版可以先完整记录各结果，不必立刻实现多目标算法；若未来频繁改权重，再研究偏好条件化。 |

## 训练与部署期持续学习

| 论文 | 论文研究了什么 | 对本项目的启发与边界 |
| --- | --- | --- |
| [Proximal Policy Optimization Algorithms](https://arxiv.org/abs/1707.06347)（Schulman et al.，2017） | 基于当前策略采样进行策略优化。 | 可作为模拟器批量训练的清晰基线；部署后若要反复利用很久以前的实机经验，不能仅凭 PPO 名称假定旧数据可直接复用。 |
| [Safe and Efficient Off-Policy Reinforcement Learning](https://arxiv.org/abs/1606.02647)（Munos et al.，2016） | 提出 Retrace，研究利用其他行为策略产生的轨迹进行学习。 | 旧版本策略产生的实机记录是异策略数据；选择经验重放算法时需要处理这个差异。论文的理论结果不自动保证具体深度网络实现稳定。 |
| [Experience Replay for Continual Learning](https://arxiv.org/abs/1811.11682)（Rolnick et al.，2019） | 研究重放旧经验以缓解持续学习中的遗忘。 | 为保留有上限的历史轨迹、混合新旧经验提供依据；其顺序任务实验不等同于本项目的单游戏在线改进。 |
| [Deep Q-learning from Demonstrations](https://arxiv.org/abs/1704.03732)（Hester et al.，2018） | 研究专家示范与后续强化学习的结合。 | 如果能获得人工或参考求解器的高质量、合法轨迹，可研究预训练；不能把求解器输出无验证地当作最优标签。 |
| [RUDDER: Return Decomposition for Delayed Rewards](https://papers.neurips.cc/paper_files/paper/2019/hash/16105fb9cc614fc29e1bda00dab60d41-Abstract.html)（Arjona-Medina et al.，2019） | 研究长延迟奖励的归因和回报重新分配。 | 整局胜负与早期选牌相隔很远，归因值得研究；这是后续候选技术，不应在没有简单基线时直接增加复杂度。 |

## 评估与泛化

| 论文 | 论文研究了什么 | 对本项目的启发与边界 |
| --- | --- | --- |
| [Empirical Design in Reinforcement Learning](https://www.jmlr.org/papers/v25/23-0183.html)（Patterson et al.，2024） | 系统讨论强化学习实验的性能统计、算法比较和实验偏差。 | 模型晋升需要事先规定测试种子、主要指标、置信区间及候选筛选流程；局数应由观察到的波动和希望识别的改善幅度决定。 |
| [Deep Reinforcement Learning at the Edge of the Statistical Precipice](https://arxiv.org/abs/2108.13264)（Agarwal et al.，2021） | 展示只看少量运行的均值会导致不稳定结论，提出区间估计等评估工具。 | 对通关率和层数都报告不确定度，不根据单个 200 局点估计自动发布新模型。 |
| [Leveraging Procedural Generation to Benchmark Reinforcement Learning](https://arxiv.org/abs/1912.01588)（Cobbe et al.，2019） | 用程序生成环境研究训练样本效率和泛化。 | 训练、模型选择、最终验证的种子应分开；模拟器中的高分还需要真实游戏对照，尤其要排查 AI 利用模拟规则误差的情况。这是本项目的设计推论，不是该论文对《杀戮尖塔 2》的直接结论。 |

## 论文尚不能替我们决定的事

1. **奖励优先级。** 通关率和平均可达层数如何取舍，必须由项目目标指定。保存原始事件和奖励向量只能支持重新计算已观察轨迹的奖励，不能提供未采取动作的反事实结果。
2. **训练算法。** PPO、异策略价值学习、示范预训练及多目标算法都应在同一模拟器、相同训练预算和保留种子上比较，不能仅凭其他游戏的结果定案。
3. **观察充分性。** 必须列出哪些玩家已知事实会影响决策，并核对当前观察是否通过变量、关系或已知序列表达。当前编码器只消费当前观察，不以历史缓存补缺；训练输入不得泄露隐藏牌序等内部状态。
4. **实机资源。** 论文不能给出本项目在无 GPU、约 8 GB、5 秒约束下的延迟与峰值内存；需要真实 `Observation → 合法动作 → 决策 → 提交` 端到端基准，至少报告尾部延迟和峰值内存。
5. **环境正确性。** 整局训练必须覆盖战斗之外的路线、选牌、商店等状态转换，并对照真实游戏验证。模拟器的训练吞吐、规则偏差和实机对局成本需要分别测量。

## 建议的阅读顺序

2026-10-01 整体规划补充：本轮核对了 [PPO](https://arxiv.org/abs/1707.06347)、[Deep Sets](https://arxiv.org/abs/1703.06114)及[动作屏蔽](https://arxiv.org/abs/2006.14171)的论文入口。短任务 PPO 与共享集合/稀疏表示是[当前实验提案](training-design.md)，不宣称论文本身证明了本游戏的学习效果或资源成本。

先读 Dominion、动作屏蔽、奖励塑形、多目标策略四篇，确定环境和奖励接口；再读 PPO、Retrace、经验重放，选择初始训练及部署期学习基线；最后读实验设计和统计评估，确定模型晋升验收条件。Deep Sets、循环记忆、示范学习和 RUDDER 可以在对应基线暴露问题后深入。
