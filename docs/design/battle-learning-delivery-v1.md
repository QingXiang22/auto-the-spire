# D1：可运行战斗学习版本交付任务

| 项目 | 内容 |
| --- | --- |
| 版本 / 日期 | v1.0；2026-10-03，按用户要求从全项目长任务收紧为可见成果 |
| 本次用户成果 | 从仓库根目录看一局AI对局、执行有限训练、比较训练前后、从检查点继续；命令和产物可直接使用 |
| 当前派单 | 本文保留D1原验收契约；正常主路径已交付，D1.1/D2也已验收，当前接续以[ENV完整环境任务](environment-completion-and-differential-v1.md)为准，正式AI实验暂缓，不重新派D1/R1 |
| 范围与状态入口 | [当前清单](../review/tofix/README.md)；[完整W范围](implementation-kickoff.md#19-2026-10-03整体完成度与当前任务清单)保留作后续，不删除 |
| 依据 | [整体复核](../review/records/2026-10-03/project-completion-review-2026-10-03.md)、现有runner/models/reward/evaluation及net客户端 |
| 负责人决定 | 固定已有默认单战斗Gym；下述小预算工程演示Profile允许执行有限模拟采样/更新，不等待另一次W-07产品奖励决策 |

## 1. 本次范围与完成含义

本次交付一个命令行版本，复用默认`Net.TheSpire.Host`，**不加`--author-content`、`--test-headbutt`或新场景开关**。实际Gym、catalog和scenario从`describe/get_catalog`取得并冻结；初次运行落盘identity/Manifest，后续严格校验，不在AI中猜身份。规则集内容保持默认受控范围。

默认Gym的公开信息只按其实际机制验收；作者扩展摘要、药水/两房间、正式源Host及完整原版覆盖不成为D1前置。发现默认路径确有决策信息缺失时提供成对反例，最小修复提供方并更新身份；不让AI补历史。变更identity使已有模型/检查点失效，应在训练前完成，不静默迁移。

本次不新增卡牌/Power/敌人/药水，不扩地图/整局，不迁完整正式源，不切换默认后端，不重选模型、不加GUI/网页/服务/分布式或训练框架。后续工作仍保留在W清单。只有阻塞下述使用场景的缺陷进入当前实现。

**完成含义是使用路径真实、更新有效、结果可核对。** 训练后可能变差或没有显著变化，只要诚实报告且工程验收成立，D1仍可完成；不能因此无限调参，也不能称已交付强策略、完整STS环境或获准晋升的部署模型。

## 2. 用户入口：执行者必须实现这些命令

以下入口已在2026-10-04负责人复核中确认可运行；原故障/累计预算四项已在D1.1关闭，最新状态见当前清单。工作目录为仓库根；无需设置PYTHONPATH、导入tests模块、修改测试文件或手工编辑内部对象。

```powershell
# 一次构建；入口之后只启动已构建Host，不每局调用dotnet build/run
dotnet build AutoTheSpire.sln

# A：随机初始化小模型看一局，明确标注untrained
python ai-the-spire/run.py demo --config ai-the-spire/configs/battle-demo-v1.json --output artifacts/d1/demo-initial

# B：有限训练，保存初始模型、候选模型及逐轮检查点
python ai-the-spire/run.py train --config ai-the-spire/configs/battle-demo-v1.json --output artifacts/d1/train

# C：均匀策略/初始模型/训练候选三者的最终保留种子对照
python ai-the-spire/run.py evaluate --run artifacts/d1/train --split final --output artifacts/d1/evaluate

# D：加载训练候选看一局
python ai-the-spire/run.py demo --config ai-the-spire/configs/battle-demo-v1.json --model artifacts/d1/train/candidate-model --output artifacts/d1/demo-candidate

# E：两轮smoke配置，在第一轮完成后正常停止；恢复到总目标两轮
python ai-the-spire/run.py train --config ai-the-spire/configs/battle-demo-smoke-v1.json --stop-after-round 1 --output artifacts/d1/smoke-partial
python ai-the-spire/run.py resume --run artifacts/d1/smoke-partial --output artifacts/d1/smoke-resumed
```

入口必须有`--help`，help无需已构建Host或NumPy即可查看。缺Python依赖或Host程序集时，在启动游戏前显示准确准备步骤并非零退出；不静默安装或后台构建。NumPy为当前已有学习依赖，记录实际版本，提供最小依赖声明及README准备步骤；不新增训练框架。

所有输出路径相对调用者工作目录解析并记录绝对路径；入口配置/Host定位相对仓库位置解析。输出目录已非空则拒绝，不覆盖旧结果、不删除已有产物。`resume`读取原run、写新的空目录。CPU、一个worker、顺序执行，资源计数与报告均来自实际运行。

退出码：0＝该命令完成其承诺（包括正常的stop-after-round、未改善、有效决策上限截断）；2＝用法/准备/配置/identity或模型错配；1＝运行故障、未知动作结果、非法概率/数据、数值失败或资源超限；130＝用户中断。最终摘要同时写退出类别与原因。技术故障必须停止当前命令，保留已确认记录，不自动随机出牌或开启新局掩盖失败。

## 3. 用户看得到的内容与产物

### D1-A：先交可看的一局

每个确认决策以一段可读文本显示：步号/回合、玩家HP/格挡/能量、手牌名称或公开ID及实例属性、敌人HP/意图/Power、该快照公开的牌区及已知位置、全部合法候选及概率、实际选择、执行后的公开变化。无动作的终止快照单独显示，不计一个假动作。可以复用公开catalog显示名；没有名称就显示ID，不解析DSL推断效果、不展示内部规则或隐藏信息。

开始显示真实Gym/scenario、seed、模型ID、是否受过训练；结束显示胜/负/截断/故障、确认动作数、耗时和产物路径。保存`episode.md`（可读对局）与复用正式EpisodeRecord的公开轨迹。输出显示无需回查测试PASS列表即可判断发生了什么。

**A验收后即可交给用户试用，再继续B。** 不等训练器所有工作完成才首次交付入口。

### D1-B：交有限真实训练

终端每轮仅输出：轮号、冻结行为模型ID、尝试/完成/截断/故障局数、确认决策数、回报及loss、更新后的候选ID、耗时、检查点路径。逐动作细节进轨迹；不刷屏打印每个内部检查。

训练run最少包含：`resolved-config.json`（全部实际参数及摘要）、`manifest.json`、`initial-model/`、`candidate-model/`、`checkpoints/round-0001/`等、公开`episodes/`、`metrics.jsonl`、`summary.json`、`summary.md`。复用现有模型包、记录和摘要，不再设计平行格式替代它们；仅补运行状态/样本来源必要字段。完整配置字段可由现有模型/编码配置导出，不能只记“使用默认值”。

### D1-C：交比较与恢复

`evaluate`对同一保留seed分别新开独立会话，运行均匀基线、initial-model、candidate-model；相同Gym、限额及执行器规则，报告平均回报、胜负/截断/故障数、确认决策数、耗时，以及候选相对两种对照的配对差值和可用区间。小样本/截断/无改善明确注明；故障不能从表中消失，技术故障使命令非零。写`comparison.json`与`comparison.md`。

`resume`只恢复已提交训练轮次，不恢复/回退游戏会话。载入权重、优化器、训练/执行器RNG或确定派生位置、已完成轮数、预算计数与身份；第一轮停止后恢复只跑剩余轮，不额外增加目标轮数。同一机器、软件、配置和计算顺序下，最终参数数组、Adam状态/步数、已完成轮数及下一批种子与连续运行逐元素/逐字段一致，实际权重摘要/模型ID一致；耗时、时间戳、进程/会话/request标识不要求一致。新目录包含查看/评估所需的配置、Manifest、初始及候选包和来源索引，不要求用户编辑旧路径。运行中途未提交轮次不能伪装已完成，任何不确定动作不得作为已确认样本重用。

## 4. 固定工程演示Profile：执行者无需猜参数

本表由本次规划冻结，标识为`battle-demo-v1`，用途仅是受控模拟工程交付，不代表正式游戏收益偏好或学习效果晋升标准。实现完整落盘配置和摘要；参数变化就是新profile/run，不能修改最终种子后保留旧实验身份。

| 配置 | battle-demo-v1 | battle-demo-smoke-v1 |
| --- | --- | --- |
| 宿主 | 当前默认战斗Host，extra_args为空 | 相同 |
| demo环境/执行器seed | 20261001 / 20261002 | 相同 |
| train环境seed | 每轮4局；第k轮第j局为10000+4k+j，k=0..7、j=0..3 | 每轮2局；10000+2k+j，k=0..1、j=0..1 |
| validation / final环境seed | 20000..20007 / 30000..30007 | 20000..20003 / 30000..30003 |
| 策略与执行器RNG | 模型初始化seed=20261003；局执行器seed=环境seed+1000000，三种评估策略相同规则 | 相同 |
| 模型/编码 | 复用当前ModelConfig/EncodingConfig默认结构与float64，所有解析后字段落盘 | 相同 |
| PPO | lr=0.002，clip_ratio=0.2，epochs=2，value_coef=0.5，entropy_coef=0，max_grad_norm=1；Adam beta1=0.9/beta2=0.999/epsilon=1e-8 | 相同 |
| 折扣与估计 | 每个确认外部动作一个时间步；gamma=0.99，折扣回报、A=G−V_old，无GAE/优势归一化 | 相同 |
| 采样/更新 | 8轮×4局，整轮冻结μ=π，采完后更新一次；最多4096个训练动作 | 2轮×2局；最多512个训练动作 |
| 每局/请求限额 | 每局最多128个确认动作、60s；每请求10s截止，恢复查询仍受该局60s约束 | 相同 |
| 命令总限额 | train含validation及checkpoint最多900s；evaluate最多180s；demo最多60s | train/resume最多180s |
| 运行存储限额 | 单个输出run累计512MiB，包含模型/轨迹/检查点；超限停止且不删旧产物 | 相同 |
| 评估执行 | 从真实候选概率采样，canonical语义键顺序；均匀基线同执行器；bootstrap重抽1000次、seed=20261003 | 相同 |

种子划分是**一个固定场景内的有限种子对照**，不声称已有多场景泛化。此次明确允许这一小范围交付，不以W-03未来“丰富场景簇”条件阻塞D1；训练、validation、final仍不得混用。

每个命令报告机器/软件版本、实际采样/更新耗时、参数/权重字节、可测内存及其测量口径。不能把Python分配跟踪量称为整个Host/NumPy进程组RSS；未测到的指标标记未测，不为D1引入新的监控平台或宣称部署达标。资源限额到达时保存已有完整检查点/错误摘要，停止采样更新，非零退出；不能靠截断必要观察/候选满足预算。resume累计计入原run已用预算，正常用户查看demo/evaluate另计，不增加训练预算。

## 5. 奖励、样本和身份的固定语义

三个奖励分量保留向量及单位：`hp_lost=max(0,player_hp_before−player_hp_after)`（HP）、`victory`（布尔）、`defeat`（布尔）。每个**已确认动作及取得的真实后快照**计算`r=-0.01*hp_lost+victory-defeat`；胜负分量只在该动作首次进入真实终止状态时计一次。无终止、可确认继续运行时胜负分量为0；未知/缺失状态不得按0奖励补样本。default无需药水/卡牌成长收益分量。训练与评估使用同一语义；报告episode未折扣分量/总回报，训练另用gamma折扣回报。

正常终止：末状态bootstrap为0。确认达到决策上限且仍在运行：取得最后动作后的新公开快照，用本轮冻结模型`V_old(s_T)`bootstrap，标记truncated；步数上限恰好终止必须识别为terminal。时间/存储限额、协议错误、未知执行或用户中断：保存已确认事实，未完成该轮不提交更新，不把故障转成defeat，不启动替代局填满批次。

动作身份按该快照canonical语义键匹配张量候选行，记录π、μ、选中概率及old_log_prob，V_old、G、A可从公开轨迹和冻结模型复核；不跨快照复用candidate_id或下标，不把终止标记计为训练样本。首版全批次更新，不额外实现replay buffer/异步learner。

先修已复现的权重摘要/模型ID过期问题：冻结行为模型生命周期内参数和身份不变，更新成功后按实际权重内容生成候选身份；实际权重未变则不伪造变化。Gym/Manifest/结构/权重/Profile随样本与包绑定，更新前拒绝混用行为版本、identity、奖励或执行器来源。保存/加载逐项校验一致，数值非有限或概率非法时停止，不随机兜底。

validation在训练前及每个成功更新后使用均匀/初始/当前三种策略的保留validation种子；只报告曲线，不调参、不自动选最优轮。candidate固定为最后成功轮模型；训练命令不读取final种子。final仅供用户evaluate，结果不驱动追加训练。无改善允许阶段完成，技术错误不允许。

## 6. 实现责任与顺序：减少重复实现和接口返工

**一个AI集成负责人**主责D1-A/B/C的入口与整个可运行成果；用户可以安排支持agent，但不能把A/B/C拆成互不验收的三个孤立组件。本次规划不自动创建agent。只有一个执行者时允许其按对应AGENTS处理R1必要支持；已有多人认领时遵守归属与共享文件/构建协调。

| 任务 | 文件/边界 | 交付与验收 |
| --- | --- | --- |
| D1-A | AI新增`run.py`及薄的应用编排/文本输出；复用runner、data、模型/Manifest及net客户端 | help、准备错误、真实单局及可读产物；用户可试用后继续B |
| D1-B | AI学习样本衔接、模型身份、编排/配置、现有bundle必要字段；不改游戏规则 | 固定预算真实采样/更新，权重与身份对应，逐轮checkpoint，确认/终止/截断/故障正确 |
| D1-C | AI现有evaluation/恢复与README、使用场景检查 | 三策略对照、加载候选demo、smoke连续与恢复一致、有限资源/错误路径、运行交付报告 |
| R1 | base当前Bully数值来源/伤害规则及切片检查；确有通用缺陷才协调Engine | 复现此前零易伤预期4实0，最小修复；核对零/正层及普通/升级，缩放基础值与易伤最终修正分开；适用检查及Legacy回归通过；不借此新增其他内容 |
| net支持 | 仅D1实际协议/当前快照/限额问题 | 复用既有client/Host；变更向AI说明字段/identity和失败类别，附最小真实反例；不做作者Profile扩面 |

纯编码/模型层继续与活动client解耦；生产入口/运行编排允许调用现有`net-the-spire/client/net_client.py`，集中适配导入位置，不复制客户端或把测试helper引入生产。AI禁止导入core/base运行时或读取内部State/DSL；Console从公开快照渲染。

先完成A，再完成B，再完成C。身份及采样事实字段在B开始前由AI集成负责人一次确定；涉及net公共契约才协调net。R1可由用户安排独立支持线，编辑与构建协调，不让AI负责人接手内容迁移。D1-A可先交试用；阶段最终封板须R1关闭及当前受影响检查通过，否则明确可试用/封板未完成，不能宣称全项目全绿。

现有contracts/encoding/runner/models/learning/bundle/evaluation/data均优先复用。只为真实衔接缺口增代码，不重写网络/自动微分/协议，不先做全面目录重构。发现新问题先判断是否阻塞上述命令；不阻塞的并入相应W后续项，不扩大当前goal。

## 7. 验收场景与有限退出条件

| 场景 | 必须观察到的结果 |
| --- | --- |
| VISIBLE | 从根目录按命令启动，显示当前局面/全候选概率/选择/后状态；episode.md与真实轨迹相符 |
| UPDATE | train通过真实默认Gym采样，至少一次非空有效更新；实际权重变化对应新摘要/ID，初始与候选包独立保留 |
| SOURCE | 注入另一行为版本或Gym/奖励Profile样本，更新前明确拒绝；相同输入同冻结模型概率一致 |
| ENDING | 最后允许动作进入terminal仍识别终止；仍运行的max_decisions=1标截断且bootstrap；断连/未知执行非零且不伪造后状态/胜负 |
| LOAD | 新进程加载candidate-model运行demo；身份或权重损坏则启动游戏前拒绝，不能默默随机初始化 |
| COMPARE | 三策略、同保留seed/执行器/限额的表与JSON一致；未改善照实报告，训练未消费final种子 |
| RESUME | 同机同软件同smoke配置连续两轮与停下恢复所得参数数组/Adam状态/计数逐元素或逐字段相等，摘要/ID一致（耗时与运行标识除外）；已完成轮不重复，旧run未覆盖 |
| BOUNDED | help/缺依赖/非空输出目录/限额错误明确；正常、失败、中断退出均回收自己启动的Host；停止后不无限调参扩面 |

执行者选择现有测试入口接入这些能区分行为的检查，写一份小预算自动化验收入口并在README提供命令，不再为每个内部函数镜像写测试。开发中只跑受影响检查；D1封板顺序构建并运行现有AI、J、客户端/契约及R1受影响Engine/Slice/Legacy检查，记录命令/返回码/实际范围。无新变化不每个细小修改重跑整套；测试数为0不算通过。

交付仅维护一份`docs/review/tofix/battle-learning-delivery-2026-10-03.md`阶段交接，包含：根目录可复制命令、产物路径、默认配置及停止原因、三策略结果、恢复结果、实际检查及未完成项。A/B/C追加三个产品检查点，不每个原语写新报告。任务状态只更新review清单，规划者负责分诊归档；未完成不自行关闭父W项。

达到上述使用场景与封板要求后，原goal完成并停止。2026-10-04负责人已发布[D1.1/D2接续任务](battle-learning-next-step-v1.md)：D1.1收口实际发现的故障/预算问题，D2优先解释学习未改善；原先预想的作者/正式源内容扩面与多场景随后另排，完整整局继续后续。产品奖励、晋升、真实部署仍由用户/规划者决定。
