# D3：敌方HP进度奖励的有限学习对照

> **历史未执行方案，退出当前派单（2026-10-04）。** 用户重新讨论未知mod与自监督学习后，D4也因环境优先暂缓，当前按[ENV任务包](environment-completion-and-differential-v1.md)执行。本文件参数/命令仅保留原决定；未启动不等于已解决，也不计任何学习成果。

| 项目 | 内容 |
| --- | --- |
| 版本 / 日期 | v1.0；2026-10-04，原冻结方案，现由D4替代 |
| 目的 | 检验失败轨迹中增加公开战斗进度信号，是否改变模型行为并改善战斗结果；交付两个可加载模型及一份真实对照 |
| 现状 | D1/D1.1/D2已验收；工程能运行，规则参考8/8胜，现有模型0/8胜；不再派原修复/诊断 |
| 当前状态 | 只在[路线图](../roadmap.md)维护；负责人依据见 |
| 执行范围 | 一个AI集成负责人顺序实现并运行有限模拟实验；只改AI，不新增core/base/net机制或内容 |

## 1. 选择这个实验的理由

旧训练32局全败，未折扣回报均为−1.8，敌方剩余HP却有差异。D2发现局长与折扣回报相关0.9856，但相关性不能证明折扣是唯一瓶颈。负责人离线重算765条样本：γ=1时G精确等于`−1−0.01×当前玩家HP`，误差小于4.5e−16；没有凭空产生攻击进度信号。下一步不先花一个训练批次重复确认这个恒等式。

本次只改变一个训练偏好系数：敌方HP净减少的权重0→0.01。它是明确的工程奖励实验，会改变训练目标，不宣称保持原目标的最优策略。原目标作为两模型共同的评价口径；奖励增多不能自己证明模型变好。规则基线只作对照，不注入训练标签或规则动作；继续固定小模型/NumPy PPO和默认单战斗Gym。

## 2. 冻结两组训练条件

新增两个显式Profile，由原`battle-demo-v1.json`派生；不修改旧文件或放宽旧Profile校验。主实验配置只引用下面两个已批准Profile及固定评估参数，不建设通用搜索/实验平台。

| 字段 | control | treatment |
| --- | --- | --- |
| 配置文件 | `configs/battle-progress-control-v1.json` | `configs/battle-progress-treatment-v1.json` |
| profile_id / semantic_version | `battle-progress-control-v1` / `1` | `battle-progress-treatment-v1` / `1` |
| 新字段reward.enemy_hp_delta_weight | **0.0** | **0.01** |
| 训练标量 | `−0.01×hp_lost+victory−defeat+0×enemy_hp_delta` | `−0.01×hp_lost+victory−defeat+0.01×enemy_hp_delta` |
| 其他实际字段 | 与D1默认配置完全相同 | 与control完全相同 |

唯一算法自变量是上述权重；Profile名称/描述/摘要不同属于必要身份元数据。两组都保留四分量事实，不能一组用不同的派生/采样实现。四分量奖励Profile族为`net:reward:d3-battle-progress-v1`，两组权重/摘要不同；训练Profile绑定各自配置和模型包。禁止跨组恢复、混用样本或修改旧checkpoint。control的前三分量和训练标量必须与D1一致。

共同条件：默认Host、无额外开关；5444参数的现有ModelConfig与EncodingConfig、float64；model_init=20261003，PPO配置及γ=0.99保持D1全部原值；每组8轮×4局，训练环境seed10000～10031、validation20000～20007，执行器seed=环境seed+1000000。两组**从相同随机初始化权重独立开始**，禁止从旧候选继续或从control训练后初始化treatment。初始参数/摘要必须相等，训练Profile不同不应使初始化随机序列不同。

每组训练限额4096个实际确认动作/900秒（含validation）/512MiB，单局128动作/60秒、请求10秒；合计最多64局训练、8192个训练动作、1800秒。逐轮validation继续D1既有协议，不缓存/降低频率，不同时改梯度裁剪、学习率、网络或折扣。旧D1产物只作历史参考，不代替当前代码上的control。技术故障停整个实验，不重启补局；无改善不追加轮数。

## 3. 新分量只从公开事实派生

对每个已确认动作的真实前后快照，读取`net:kind:base-spire:Monster`所有已发布实体的`net:field:base-spire:Monster.Hp`，求和为H；定义`enemy_hp_delta=H_before−H_after`，单位HP。这是**净减少**，不取绝对值、不按每个敌人分别截正：敌方回血给负值，格挡吸收没有HP变化则为0，过量伤害只算实际HP减少。多个敌人求和一次，终止动作也只计一次。

复用已有`net:reward:enemy_hp_delta`和RewardVector派生设施；补足此Profile必需的完整性校验，不复制奖励引擎。任一已发布敌人缺失/unknown/非法HP时明确失败，不能只把有字段的敌人相加。当前默认Gym无战斗中生成/替换敌人；只有真实终局outcome=win且确认不存在敌人实体时，才允许H_after=0。普通运行快照没有敌人、未知执行或后快照缺失均不能按0补样本；死敌若仍发布，其HP=0自然计入。若Gym身份或机制不符合本范围，启动前拒绝，不让AI猜死亡/隐藏敌人。

先用旧公开轨迹完成离线核对，输出`reward-audit.json/md`：同一动作在两组的四分量相同、标量差恰为0.01×enemy_hp_delta；列出已有32局失败的进度分布和新标量是否产生差异。用能区分语义的有限检查核对多敌人、格挡、击杀/过量伤害、HP增加、终局空敌人、缺字段及未知后状态。没有产生区分或派生失败时停在审计阶段，非零退出并报告首错，不盲目开训。

## 4. 用户入口和可见检查点

新增主实验配置`ai-the-spire/configs/battle-progress-experiment-v1.json`，显式记实验ID`battle-progress-experiment-v1`、语义版本1、两训练配置路径及本任务固定的预算/评估字段。复用run.py的标准库help与现有薄应用编排；配置缺失/身份错配启动Host前退出2。

```powershell
python ai-the-spire/run.py experiment --config ai-the-spire/configs/battle-progress-experiment-v1.json --output artifacts/d3/progress
python ai-the-spire/run.py demo --config ai-the-spire/configs/battle-progress-treatment-v1.json --model artifacts/d3/progress/treatment/candidate-model --output artifacts/d3/treatment-demo
```

experiment顺序：**离线审计→control有限训练→treatment有限训练→两模型冻结后的共同评估**。复用现有train、记录、账目、模型包、评估及参考策略，不另写优化器、采样器或协议。首个可见成果是reward-audit，不等最终报告才汇报；之后两组训练进度和路径可查看。两组失败/中断保持已有产物及资源账目，最终共同评估完成前不写工程成功，不静默运行另一组掩盖错误。

输出根目录包含：实验配置/实际条件与差异审计、reward-audit.json/md、`control/`和`treatment/`（全部D1训练产物及预算账目）、`comparison.json/md`、`summary.json/md`及`episodes/`共同评估公开记录。至少保存两模型同评估seed50000的可读对局，成功与失败都保存。初始/候选模型可用现有demo加载；每组可用现有resume继承账目，但主实验不自动恢复/重试。

## 5. 共同评价与停止条件

两模型只取最后成功轮，禁止看结果选最佳轮。两组训练都完成且冻结后，才访问**一次性最终seed50000～50007**；这是负责人本次启用的此前保留组，训练和离线审计不得访问。旧30000～30007已用过，不作新的未见证据；60000～60007继续保留，本任务不得访问。

比较control候选、treatment候选、共享初始模型、均匀基线、D2规则基线五种策略，使用相同8个环境seed/执行器/限额。共同评估最多40局、5120确认动作/180秒/512MiB；单局及请求限额同D1。整个experiment活动墙钟最多2100秒、输出最多1536MiB，训练和评估分别计账，不把评估动作算训练。顺序执行，任一技术故障停止，不换seed或补局。

共同评价Profile：前三分量与D1相同、γ=0.99，标量为`−0.01×hp_lost+victory−defeat`，记录独立评价身份`net:reward:d3-original-objective-v1`及摘要，同时绑定各模型自己的训练Profile。对不同训练奖励模型使用同一个明确的评价Profile属于本实验批准的交叉评价；仍拒绝Gym/目录/Manifest/模型权重错配，不能删来源校验来让两组混在一起。

主结果是treatment对control的同seed胜负与原目标回报差值、95%配对自助区间（1000次，seed20261003）。敌方HP净减少/剩余HP、剩余能量结束回合、EndTurn概率、同局面TV及成本用于解释行为；训练奖励只画各自曲线，**不直接比较不同训练标量说谁更好**。有更大伤害但仍全败，写“战斗进度改变，尚无获胜改善”；只一局胜或区间跨0写证据有限，不称已学会/泛化。相关性或无改善不能单独排除表示/探索/优化问题。

最多完成这一对照；不扫描权重、改γ、加训练种子或用final结果自动开新训练。未改善允许实验工程完成；非法数据、超限或数值失败保持未完成并非零退出。技术退出码沿用D1，摘要注明停在哪个检查点。

## 6. 责任与检查

一个AI集成负责人负责`ai-the-spire`内新Profile、奖励最小扩展、实验编排/评价、用户命令及README；负责人负责docs规划，执行者通过 PR 更新实现与检查结果。core/base/net没有新任务；若发现默认观察反例，记录具体公开快照和阻断范围，暂停相应数据路径，不扩成内容迁移。禁止读隐藏状态、导入tests helper进生产、引入训练框架/LLM、改架构或自行创建agent。

同时修正D2自动诊断文案中的“唯一可学方向”“γ=1后优势只剩−V_old”等过强/不准确断言，改为有范围的观察和未验证假设；旧已保存报告保留历史。D2本轮继续只接受原D1三分量run，遇D3四分量run明确拒绝并提示使用本实验comparison，不能静默遗漏新奖励分量；不扩建通用诊断平台。

开发检查优先验证奖励区分、两组唯一算法差异/同初始化、跨组身份拒绝、共同评价口径和故障预算；可用有界smoke/受控注入，不新增一整套无差别真实长测试。合并后运行AI现有604、J219及与改动相关的D1/D1.1/D2检查，明确哪些复用既有证据；不因未改规则反复构建/跑全部内容。正式64局实验只跑一次，验收测试不再重复跑完整实验。

实验启动须另有明确安排。执行结果在对应 PR／实验讨论中说明配置、成本、结论与限制；只更新实际变化的规格，不生成阶段交接文件。
