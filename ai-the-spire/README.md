# ai-the-spire（A-01 ~ A-05 交付物）

本模块通过 `net-the-spire` 公开协议（net v0.1）读取观察并选择合法候选，提供确定性编码、低成本模型/PPO，以及默认单战斗的命令行运行与诊断。使用既有工具从下节的 D1 入口开始；是否执行新实验以[唯一清单](../docs/review/tofix/README.md)为准。

* 编码/模型层不实现规则、不导入 `core-the-spire` / `mod-the-spire` / 活动 client；`delivery` 生产编排通过已有net客户端交互，只读公开快照。
* 协议结构与判定逻辑来自共享资产 `docs/contracts/net/0.1/`（schema/不变量）
  与 `tools/contract-check/`（参考实现）。`contracts/shared.py` 集中读取共享契约；运行编排负责解析生产 Host/客户端位置。
* 数值变换、误差容差与容量是**显式 `EncodingConfig`**，随 Manifest 一起持久化（A-02/A-04）。

## D1 可运行战斗学习版本（用户入口）

任务包见 [docs/design/battle-learning-delivery-v1.md](../docs/design/battle-learning-delivery-v1.md)；
阶段交接见 [D1/D1.1/D2交付历史](../docs/review/records/2026-10-04/battle-learning-delivery-2026-10-03.md)。
这些入口已验收；当前先按[ENV完整环境任务](../docs/design/environment-completion-and-differential-v1.md)完成core/base及源规则对拍，AI仅做必要契约兼容和回归。未启动D3/D4学习实验退出当前/暂缓，不能将设计命令当作可用功能；状态见[唯一清单](../docs/review/tofix/README.md)。
在**仓库根目录**执行（输出路径相对调用者工作目录解析；配置与 Host 相对仓库位置解析）：

```powershell
# 一次构建；之后只启动已构建的 Host，不每局调用 dotnet build/run
dotnet build AutoTheSpire.sln
python -m pip install -r ai-the-spire/requirements.txt   # 只有 numpy

# A：随机初始化小模型看一局，明确标注 untrained
python ai-the-spire/run.py demo --config ai-the-spire/configs/battle-demo-v1.json --output artifacts/d1/demo-initial

# B：有限真实训练（8 轮 x 4 局），保存初始模型、候选模型及逐轮检查点
python ai-the-spire/run.py train --config ai-the-spire/configs/battle-demo-v1.json --output artifacts/d1/train

# C：均匀基线 / 初始模型 / 训练候选的最终保留种子对照
python ai-the-spire/run.py evaluate --run artifacts/d1/train --split final --output artifacts/d1/evaluate

# D：加载训练候选再看一局（身份不一致会在启动游戏之前拒绝）
python ai-the-spire/run.py demo --config ai-the-spire/configs/battle-demo-v1.json --model artifacts/d1/train/candidate-model --output artifacts/d1/demo-candidate

# E：两轮 smoke 配置，第一轮完成后正常停止；再恢复到总目标两轮
python ai-the-spire/run.py train --config ai-the-spire/configs/battle-demo-smoke-v1.json --stop-after-round 1 --output artifacts/d1/smoke-partial
python ai-the-spire/run.py resume --run artifacts/d1/smoke-partial --output artifacts/d1/smoke-resumed

# D2 诊断：只读诊断已有 run（四策略对照 + 参考对局 + 同局面概率探针 + 学习信号 + 一个实验建议）
python ai-the-spire/run.py diagnose --run artifacts/d1/train --output artifacts/d2/diagnose

# 小预算自动化验收（对应任务包 §7 的八个场景；需要已构建宿主）
python ai-the-spire/tests/d1_delivery.py

# D1.1 收口验收：故障落盘 / 累计预算 / 指标中性 / 旧 run 拒绝恢复
python ai-the-spire/tests/d1_1_closure.py

# D2 诊断验收：用户命令 / 合法参考策略 / 同快照探针 / 不触发训练与预算故障
python ai-the-spire/tests/d2_diagnose.py
```

* `python ai-the-spire/run.py --help`（以及每个子命令的 `--help`）**不需要 NumPy、也不需要已构建 Host**；
* 退出码：`0` = 完成承诺（含正常 `--stop-after-round`、未改善、有效决策上限截断）、
  `2` = 用法/准备/配置/identity 或模型错配、`1` = 运行故障或资源超限、`130` = 用户中断；
* 每个命令都把**机器/软件版本、实际采样与更新耗时、参数量与权重字节、内存口径**写进产物
  （`run-summary.json` / `summary.json` / `comparison.json` 的 `machine`、`timings` 字段）；
  内存只测**本 Python 进程的工作集**并注明"不含它启动的 Host 子进程"，进程组 RSS **显式标记未测**；
* 输出目录**已非空即拒绝**，不覆盖或删除任何已有产物；`resume` 只读原 run、写新的空目录；
* `resume` 需要来源 run 内有 **`budget-ledger.json`**（D1.1 起引入的累计账目）：
  更早的产物**可以查看/评估**，但 `resume` 会明确拒绝而不是猜剩余预算；
* `diagnose` 只读已有 run（**不调用 Trainer、不读 final 种子、不改原 run**），
  输出 `diagnosis.md`/`diagnosis.json` 与参考对局 `reference-episode.md`；
* 冻结 Profile 只有两个：`battle-demo-v1`（8 轮 x 4 局、最多 4096 个训练动作）与
  `battle-demo-smoke-v1`（2 轮 x 2 局、最多 512 个训练动作）。改种子/参数必须作为**新 profile**。

### D2 诊断的一句话结论（详见 `artifacts/d2/diagnose/diagnosis.md`）

规则基线（公开信息：先打攻击牌、Amount 大者优先、目标选公开 HP 最低、并列按语义键）在诊断种子
40000～40007 上 **8/8 获胜**（首局 32 步的逐决策可读对局见 `reference-episode.md`），
而均匀/初始/候选三个策略在这些种子上仍 **8/8 失败**、平均未折扣回报都是 −1.8。
**已排除**："任务不可完成"（受控场景确有可获胜路径）、"完全没有更新"（参数分组全部变化、
同局面概率 TV 均值 0.0097）、"采样/执行概率错配"（765 条样本 π=μ 且与冻结模型重算一致）。
**仍未判定/可定位的线索**：32 局全部失败且未折扣回报完全相同，实测**局长度与折扣回报
相关系数 0.986**；该相关性尚不能证明折扣是唯一瓶颈，"表示遗漏"本轮**未测**。
原诊断建议γ=1只属于待验证假设。负责人离线重算发现，在这32局失败轨迹中γ=1的G仍完全由当前玩家HP决定；
当前已冻结的下一实验是[D3敌方HP进度奖励对照](../docs/design/battle-progress-learning-experiment-v1.md)，不是自动执行原γ建议。

## 快速开始

需要 Python 3.12（`run_checks.py` 仅标准库；J-01 的端到端入口另外需要 numpy 与已构建的 net 宿主）。

```bash
# 仓库根目录执行
python ai-the-spire/tests/run_checks.py                    # 全部离线检查（工作目录不限）
python ai-the-spire/tests/run_checks.py --only acceptance  # 只跑一组
python ai-the-spire/tests/run_checks.py --list             # 列出组名与 fixtures 根

# J-01/M1 端到端（需要先 dotnet build AutoTheSpire.sln）
python ai-the-spire/tests/j01_episode.py                   # 真实 stdio 上的受控切片闭环
python ai-the-spire/tests/j01_episode.py --only model      # W-04：小模型作为策略在真实 Gym 上决策
python ai-the-spire/tests/j01_episode.py --only author     # W-01：作者内容包上的闭环 + 离线重编码
python ai-the-spire/tests/j01_episode.py --only trace      # 离线重读轨迹并重编码

# D-01 数据设施（record 组需要宿主；dedup/ending/index 同一进程内完成）
python ai-the-spire/tests/d01_records.py

# D-02 有界采样（需要宿主；interruption 组是**故障注入**）
python ai-the-spire/tests/d02_collector.py

# D-03 数据审计与冻结划分（需要宿主：现场采两个场景簇）
python ai-the-spire/tests/d03_audit.py

# P-02 分项与端到端测量（需要宿主；只报告测量，不给达标结论）
python ai-the-spire/tests/p02_measure.py

# D-04 长跑入口（跨模块 tools 入口；--resume 只恢复采样进度）
python tools/collect/collect.py --config tools/collect/example-config.json
python ai-the-spire/tests/d04_longrun.py

# D1 交付验收（八个场景；需要宿主）
python ai-the-spire/tests/d1_delivery.py
python ai-the-spire/tests/d1_delivery.py --only compare
python ai-the-spire/tests/d1_delivery.py --list

# D1.1 收口验收（故障落盘 / 累计预算 / 指标中性 / 旧 run 拒绝恢复；需要宿主）
python ai-the-spire/tests/d1_1_closure.py
python ai-the-spire/tests/d1_1_closure.py --only budget
python ai-the-spire/tests/d1_1_closure.py --list

# D2 诊断验收（命令 / 参考策略 / 同快照探针 / 不触发训练；需要宿主）
python ai-the-spire/tests/d2_diagnose.py
python ai-the-spire/tests/d2_diagnose.py --only isolation
python ai-the-spire/tests/d2_diagnose.py --list
```

当前结果（2026-10-03，本地实跑）：

```text
[PASS] shared-layers:   9 · expectations: 144 · roundtrip: 9 · negative: 39
[PASS] manifest: 125 · l0-text: 19 · validate: 95
[PASS] tensorize: 36 · collate: 14 · acceptance: 13
[PASS] model-shape: 8 · model-mask: 5 · model-invariance: 7 · model-policy: 5 · model-bundle: 7
[PASS] learning-grad: 2 · learning-update: 6 · learning-restore: 4
[PASS] reward-profile: 8 · reward-derive: 6 · reward-missing: 6
[PASS] eval-plan: 6 · eval-stats: 6 · eval-failures: 7
[PASS] scenario-manifest: 4 · scenario-facts: 4 · scenario-splits: 6 · scenario-cost: 4
TOTAL: 604 passed, 0 failed, 604 executed

# j01_episode.py（需要活动宿主）
[PASS] episode: 133 · headbutt: 15 · invariants: 7 · trace: 7 · author: 14 · model: 10 · reward: 9 · evaluation: 9 · scenarios: 15
TOTAL: 219 passed, 0 failed, 219 executed

# d01_records.py（record 组需要活动宿主）
[PASS] record: 73 · dedup: 5 · ending: 11 · index: 11
TOTAL: 100 passed, 0 failed, 100 executed

# d02_collector.py（需要活动宿主；interruption 组为故障注入）
[PASS] seeds: 8 · limits: 17 · isolation: 6 · interruption: 10 · replay: 4
TOTAL: 45 passed, 0 failed, 45 executed

# d03_audit.py（需要活动宿主，现场采两个场景簇）
[PASS] split: 15 · rewards: 46 · integrity: 9 · counts: 13 · drift: 6
TOTAL: 89 passed, 0 failed, 89 executed

# p02_measure.py（需要活动宿主；只报告测量）
[PASS] stages: 5 · loads: 11 · machine: 6 · diagnostics: 10
TOTAL: 32 passed, 0 failed, 32 executed

# d04_longrun.py（需要活动宿主；restart 组为故障注入）
[PASS] entry: 13 · restart: 20 · multiworker: 6 · failure-classes: 7 · config: 4
TOTAL: 50 passed, 0 failed, 50 executed

# d1_delivery.py（D1 八个验收场景；需要活动宿主，小预算 smoke）
[PASS] bounded: 24 · ending: 23 · visible: 16 · update: 60 · source: 11 · load: 8 · compare: 23 · resume: 27
TOTAL: 192 passed, 0 failed, 192 executed

# d1_1_closure.py（D1.1 故障/预算/指标/旧 run 验收；受控故障与时钟注入）
[PASS] fault: 20 · budget: 26 · metric: 7 · legacy: 4
TOTAL: 57 passed, 0 failed, 57 executed

# d2_diagnose.py（D2 诊断验收；含行为级"不触发训练"）
[PASS] command: 30 · rule: 6 · probe: 10 · isolation: 23
TOTAL: 69 passed, 0 failed, 69 executed
```

退出码：

| 码 | 含义 |
| --- | --- |
| 0 | 全部检查通过 |
| 1 | 有用例失败（逐条打印 `case_id` 与期望-实际差异） |
| 2 | 参数错误，或套件在跑完前抛异常（仍打印 `TOTAL` 行但计数不完整，stderr 有 traceback） |

最后一行固定为 `TOTAL: x passed, y failed, z executed`。**任何一条失败即非零退出**，且"零用例"不算通过。

## 目录结构

```text
ai-the-spire/
├─ ai_the_spire/
│  ├─ contracts/            # A-01：纯协议类型与视图
│  │  ├─ types.py           # wire <-> 不可变 dataclass（无判定逻辑）
│  │  ├─ views.py           # PublicCatalog / PublicSnapshot 视图与 to_wire 往返
│  │  ├─ layers.py          # 编排共享 L0–L3 检查、摘要、动作语义键（只转发）
│  │  ├─ shared.py          # 唯一触碰仓库其他目录处：加载并校验共享规范资产
│  │  └─ errors.py          # Finding / ContractViolation
│  └─ encoding/             # A-02/A-03/A-04
│     ├─ config.py          # EncodingConfig：变换、容差、容量上限（不静默截断）
│     ├─ errors.py          # 编码层错误码表（{code, path, related_ids, message}）
│     ├─ manifest.py        # build/save/load Manifest、固定名称排序、类别索引、exact identity
│     ├─ validate.py        # 字段/内容状态、适用域、引用、牌堆 V-06 四条不变量、候选与容量上限
│     └─ tensor.py          # §5.1 张量化：块/列 schema、固定枚举、值通道、引用映射、掩码、派生行
│  └─ runner/               # J-01：把公开 wire 与编码层接起来（只消费当前快照）
│     ├─ baseline.py        # 均匀候选概率 + 按规范语义键的执行器采样（无 padding 项）
│     ├─ episode.py         # describe→catalog→Manifest→start→observe→编码→概率→step + 轨迹身份
│     └─ trace.py           # 轨迹格式（j01-trace@1）与离线重读/重编码/执行器重放
│  └─ models/               # W-04：共享低成本模型（结构 + 推理 + **numpy 手写训练**）
│     ├─ config.py          # ModelConfig：显式维度/传播轮数/精度/种子（带结构摘要）
│     ├─ params.py          # 参数集合：确定性初始化、分项统计、内容摘要、检查点 state
│     ├─ arrays.py          # 张量化 → 模型输入（只有数值/掩码/字典规模，无实例句柄）
│     ├─ autodiff.py        # 微型反向模式自动微分（前向只写一遍；推理走 numpy 快路径）
│     ├─ encoder.py         # embedding → 实体/字段聚合 → L 轮稀疏关系传播 → 全局向量
│     ├─ scorer.py          # 候选向量（动作类型 + 具名字段 + 被引用实体行）→ logits / 掩码 softmax
│     ├─ policy.py          # predict(snapshot) -> 概率 + 内部 V（不返回 Q；终止无候选不伪造）
│     ├─ learning.py        # TrainingConfig / Adam / PPO 截断代理目标 / Trainer（μ=π、显式超参数）
│     ├─ bundle.py          # 模型包（含可选优化器检查点 + 奖励/训练 Profile 身份声明）
│     └─ cost.py            # 参数分项、权重字节、tensorize/前向 CPU 延迟与峰值内存（只报告）
│  └─ reward/               # W-04：版本化奖励/偏好派生层（只读公开事实）
│     ├─ profile.py         # RewardProfile：分量（单位/来源/缩放/可选）+ 显式权重 + 公开路径 facts + 摘要
│     └─ derive.py          # 公开快照序列 → 奖励向量（值/单位/逐分量贡献/标量）；缺事实显式处理
│  └─ evaluation/           # W-04：评估（固定执行配置 + 保留种子 + 配对比较 + 成本/失败）
│     ├─ plan.py            # EvaluationPlan：策略/基线/保留种子/指标（单位+方向）/自助配置/身份摘要
│     ├─ stats.py           # 均值、样本标准差、分位数、**百分位自助法**区间（可复现）
│     └─ runner.py          # 逐 (策略, 种子) 跑真实会话 → 聚合/配对/verdict/成本；失败局不入均值
│  └─ scenarios/            # W-03：场景簇 + 按簇隔离的划分 + 有界采样成本
│     ├─ manifest.py        # ScenarioSpec/Manifest：宿主开关 + 决策差异轴 + **可核对的区分事实**
│     └─ sampling.py        # 逐簇起新宿主采样 → 成本报告 + 分身份分组审计（split 非空/不跨簇）
│  └─ data/                 # D-01/D-03：公开产物
│     ├─ record.py          # episode-record@1：转移、结束分类、身份/来源、记录摘要
│     ├─ store.py           # 落盘 + 派生索引 + 独立复核（摘要/引用/重建索引）
│     └─ audit.py           # D-03：冻结划分、奖励隔离、完整性/可用性/异常/偏移审计
│  └─ collector/            # D-02：有界采样
│     └─ collector.py       # 显式限额、确定性种子派生、独立 worker、停止原因与进度索引
└─ tests/
   ├─ run_checks.py         # 入口：逐组计数 + TOTAL + 退出码 0/1/2
   ├─ fixture_suite.py      # A-01：输入/期望分离（期望手写，不由被测实现生成）
   ├─ encoding_suite.py     # A-02：Manifest/字典索引/变换/exact identity/旧文件与版本混用
   ├─ validate_suite.py     # A-03：校验层（错误码 + 路径）
   ├─ tensor_suite.py       # A-04：张量化与拼批（含跨样本误连反例）
   ├─ acceptance_suite.py   # A-05：encoder-design §9 的 13 条验收场景（样例侧）
   ├─ j01_episode.py        # J-01/M1：真实 stdio 闭环（episode/headbutt/invariants/trace）
   ├─ d01_records.py        # D-01：记录格式/索引/去重/结束分类（record 组需要宿主）
   ├─ d02_collector.py      # D-02：限额/种子派生/worker 隔离/中断/可重放（需要宿主）
   ├─ d03_audit.py          # D-03：冻结划分/奖励隔离/完整性/计数/偏移（需要宿主）
   ├─ p02_measure.py        # P-02：AI 分段与 wire 每请求延迟、四档负载、机器/峰值、诊断纯度（需要宿主）
   ├─ d04_longrun.py        # D-04：长跑入口/可重启监督/失败分类（需要宿主；restart 组为故障注入）
   ├─ d1_delivery.py        # D1：八个验收场景（bounded/ending/visible/update/source/load/compare/resume）
   ├─ d1_1_closure.py       # D1.1：故障落盘/累计预算/指标中性/旧 run 拒绝恢复（需要宿主；受控故障与时钟注入）
   ├─ d2_diagnose.py        # D2：命令/参考策略/同快照探针/不触发训练与预算故障（需要宿主）
   └─ fixtures/             # combat / card_choice / scenario / variables / negative / manifest / text / tensor / validate
```

另有 `run.py`（用户入口：demo / train / evaluate / resume / diagnose）与 `ai_the_spire/delivery/`
（冻结 Profile 解析、可读渲染、逐动作奖励、预算账目、规则基线、训练/评估/恢复/诊断编排）；
它复用现有 runner / encoding / models / reward / evaluation / data，只补真正阻塞使用场景的衔接。

## 覆盖范围（28 组，共 **604** 项）

另有 J-01/M1 的端到端入口 `tests/j01_episode.py`（**219** 项，需要活动宿主；含 `author`、`model`、`reward`、`evaluation`、`scenarios` 五组，
见 [阶段交接批次 27–35](../docs/review/records/2026-10-03/stage-handoff-2026-10-03.md)）。

| 组 | 项数 | 内容 | 任务 |
| --- | --- | --- | --- |
| `shared-layers` | 9 | 4 份目录 + 1 份覆盖表 + 5 份快照对共享 L1/L2/L3 零违规 | A-01 |
| `expectations` | 144 | 手写 `expected.json` vs contracts 视图事实逐项比对 | A-01 |
| `roundtrip` | 9 | 视图 `to_wire()` 与 wire 逐键一致（不丢键、不虚构键） | A-01 |
| `negative` | 39 | 37 条 reject 负例命中共享规则 ID + 2 条登记型 canary | A-01 |
| `manifest` | 125 | 字典索引/变换/exact identity/旧文件加载/版本混用拒绝 | A-02 |
| `l0-text` | 19 | L0 严格文本解析：坏字节向量拒绝 + 合法文件接受 | A-02 |
| `validate` | 95 | 字段/内容状态、适用域、引用、**牌堆 V-06 四条不变量**、候选与容量上限（**错误码 + 路径**） | A-03 |
| `tensorize` | 36 | 块/列/dtype/固定枚举、值通道、引用映射、局部索引、派生行、隐藏顺序不入编码 | A-04 |
| `collate` | 14 | B 轴拼批、逐样本补齐、**跨样本不误连**、混批拒绝、掩码语义 | A-04 |
| `acceptance` | 13 | `encoder-design.md` §9 的 13 条验收场景（逐条一例） | A-05 |
| `model-shape` | 8 | 显式维度/配置校验与摘要/参数分项与总量/**字典 embedding 保留补齐行**/成本分阶段 | W-04 |
| `model-mask` | 5 | 概率只覆盖有效候选、补齐候选恒 0、全掩码拒绝、掩码实体不参与聚合、V 是标量 | W-04 |
| `model-invariance` | 7 | 模型输入不含实例 ID、候选行重排按语义键等价、句柄换名不影响前向、非规范输入被拒、行序与种子复现 | W-04 |
| `model-policy` | 5 | 终止无候选不伪造概率、与 runner 的概率覆盖一致、身份随结果记录、概率非均匀、跨 Manifest 拒绝 | W-04 |
| `model-bundle` | 7 | 模型包往返一致 + 缺失/多余键、权重损坏、换 Manifest、格式版本不一致时**显式失败** | W-04 |
| `learning-grad` | 2 | **有限差分**对照自动微分的解析梯度（逐算子 + 整模型选定参数）；梯度形状/广播也对账 | W-04 |
| `learning-update` | 6 | 有限合成任务：正确率 0.00→1.00、损失下降、参数确实更新、同种子复现、梯度裁剪、**截断把区间外梯度置零** | W-04 |
| `learning-restore` | 4 | 优化器状态往返；内存与**落盘**检查点恢复后继续训练与不中断训练逐字节一致；未训练包不谎称可恢复 | W-04 |
| `reward-profile` | 8 | 分量（单位/公开来源/缩放/可选）与显式权重覆盖、版本与摘要往返、换权重/换公开路径即换身份、Profile id 随模型包声明 | W-04 |
| `reward-derive` | 6 | 从公开快照序列派生向量与标量：值与**独立读数**一致、单位保留、**塞隐藏诱饵不改结果**、权重线性、0 权重分量仍记录、确定性摘要 | W-04 |
| `reward-missing` | 6 | 缺必需公开事实 ⇒ 显式失败（不用 0 冒充）；optional 分量记 unavailable 且不参与标量化；无派生规则的分量被拒绝；空序列被拒绝 | W-04 |
| `eval-plan` | 6 | 评估计划身份：基线必须存在且与候选不同、保留种子非空不重复、指标带单位与方向、自助配置合法、换种子/换奖励即新身份、摘要往返 | W-04 |
| `eval-stats` | 6 | 描述统计与自助区间的**已知答案**核对、可复现（同种子同区间）、随方差变宽、配对只用共享种子、方向被尊重 | W-04 |
| `eval-failures` | 7 | 失败局（faulted/interrupted）不进均值、无可用局不给指标、不可用指标不聚合（如未到终局的胜负）、报告记录失败与声明、摘要确定、奖励 Profile 与计划不符被拒、未绑定模型策略在跑任何一局前失败 | W-04 |
| `scenario-manifest` | 4 | 簇必须绑定真实宿主开关（未知开关/同环境双标签/资源轴无开关都被拒）、决策差异轴与事实种类校验、清单往返与摘要 | W-03 |
| `scenario-facts` | 4 | 三种区分事实谓词在公开快照上的判定（group 有/无、候选引用内容可**中途**成立）、未实现种类与空快照被拒 | W-03 |
| `scenario-splits` | 6 | 划分覆盖全部簇、train≥2 簇且三个 split 非空、空划分/未知 split 被拒、划分摘要即身份、簇键绑定 profile+variant、无跨 split 同簇 | W-03 |
| `scenario-cost` | 4 | 成本报告聚合（限额/计数/墙钟/字节/停止原因）、显式声明「轮转非吞吐」与「随机数据非新策略近期 PPO 数据」、簇事实验证失败在报告里可见 | W-03 |

§9 场景与检查的对应见 [A-05 报告](../docs/review/records/2026-10-01/a-05-2026-10-01.md)；张量化逐项规格见
[A-04 报告](../docs/review/records/2026-10-01/a-04-2026-10-01.md) 与 [监督台账](../docs/review/records/2026-10-01/supervision-ledger-2026-10-01.md) §2.3。

D1 交付另有 **192 项** 端到端验收（`tests/d1_delivery.py`，需要宿主），
覆盖任务包 §7 的 VISIBLE/UPDATE/SOURCE/ENDING/LOAD/COMPARE/RESUME/BOUNDED 八个场景；
D1.1 收口另有 **57 项**（`tests/d1_1_closure.py`），覆盖故障事实落盘、累计预算、
指标中性与旧 run 拒绝恢复；D2 诊断另有 **69 项**（`tests/d2_diagnose.py`），
覆盖用户命令、合法参考策略、同快照探针与**不触发训练**（进程内拦截 `Trainer`/`save_bundle`，
调用次数必须为 0）。

## 未验证项（不要据此宣称已覆盖）

* **D1 没有学到更好的策略（如实报告）**：`battle-demo-v1` 的 8 轮 x 4 局真实采样确实产生了有效更新
  （初始/候选 `model_id` 与权重摘要都变了、逐轮检查点可恢复），但 8 个 final 保留种子上三个策略的
  平均未折扣回报**都相同**（−1.8000 = 80 HP 全损 + 失败），配对差值判定为 `undecided`；
  candidate 的确认决策数略少（−1.125，区间跨 0）、墙钟更慢（模型推理开销，`worse`）。
  **不构成学习改善结论，也不代表正式游戏收益偏好**；训练命令从未读取 final 种子。
* **执行器分辨率**：小模型与均匀分布的概率差异很小（约 0.02 概率质量），单个种子上两个策略常常
  走出**完全相同的动作串**（final 8 个种子里 candidate 有 2 个、initial 有 3 个与基线同串）。
  `evaluate` 会把这件事写进 `comparison.json` 的 `action_sequence_equality` 并注明；
  "回报相同"不等于"策略等价"。
* **D1 只覆盖一个固定 Gym 内的有限种子对照**：不声称多场景泛化；`resume` 的一致性验收是
  "同机同软件同配置同计算顺序"下的逐元素相等，耗时与运行标识（进程/时间戳）不要求一致。
* **每局墙钟限额在 Windows 上粒度约 15.6ms**（`time.monotonic()` 用系统时钟节拍）：
  60s 预算下无关紧要，但极短限额的检查不能断言"恰好 0 个动作"。
* **机器报告不含进程组 RSS**：只测**本 Python 进程的工作集**（Windows `K32GetProcessMemoryInfo`）并注明
  "不含它启动的 Host 子进程"；Host/NumPy 进程组 RSS 显式标记"未测"，不为 D1 引入新的监控平台。

* **真实模拟证据缺失**：全部检查都在夹具/单元层。V-01~V-10 与 V-15 的"真实模拟"档、以及需真实运行才能
  证明的**改变隐藏牌序/RNG 游标后公开特征仍然相同**（完整 V-02），属 **N-05**；本目录不声称已覆盖。
* **奖励/偏好层与评估已交付"工程"，未交付"效果"**：`reward/` 提供版本化分量表、单位、公开来源与**显式**标量化权重
  （**默认权重全 0**；真实权重/折扣/时间尺度由 **W-07** 冻结的 Profile 给出，本目录不自造收益权重）；
  `evaluation/` 用固定执行配置 + 保留种子做**同种子配对**比较，报告均值/样本标准差/自助区间/成本/失败与未训练声明，
  真实 stdio 实测（随机基线 vs 未训练小模型）= `reward worse / steps improved / terminal_win undecided / wall_ms worse`
  ——这正是"未训练模型不更好"的如实结果，**不**构成学习改善结论。
* **W-03 的场景是"受控 Gym 上真实存在的四个簇"**：默认切片 / 加头槌 / 作者内容 / 作者内容+头槌。
  每个簇绑定真实宿主开关并声明一条**可核对的公开事实**（药水栏存在与否、候选里出现头槌），
  采完后在该簇真实录到的快照上核对；作者内容簇与默认簇的 **ruleset/catalog 身份不同**，
  因此按身份分组存放并逐组审计（混在一个目录会被 `data.audit` 正确判为混用身份）。
  采样是**轮转不是吞吐**（`workers>1` 只是多个独立宿主连接逐簇串行 + 簇内轮转），
  随机策略数据只用于数据设施验证与基线，**不**作为新策略的近期 PPO 数据。
* **W-03 的划分只有 4 个簇**（train 2 / validation 1 / final_eval 1）：非空且按簇隔离，
  但**不支持簇内泛化**的可信结论——真实场景簇扩张属 W-06 内容与 W-07 任务分布的后续范围。
* **W-05 的两房间 + 房间奖励已接通**：作者 Gym 由**连续两间房**组成（`Run.CurrentRoom`/`SettledRoom`/`RewardTaken`
  + `EnterNextRoom` + `UpgradeLongTermCard`），`--only author` 组实测两间房 + 一次房间奖励走到整局 Victory（62 步，
  轨迹里 `CurrentRoom` 1→2、用过两个动作、第二间房里有升级牌）。**加牌型奖励**（把新卡加入长期牌组）仍未公开化。
* **真实 STS 训练仍未启动**：任务分布、奖励权重、预算与晋升门槛要等 **W-07** 冻结；
  在它之前禁止把工程权重当训练目标、禁止无限训练。
* **未运行任何 `dotnet`**：本目录证据独立于 .NET 泳道；跨语言 wire 一致性由 `net-the-spire` 侧对照（N-01）。
  （例外：`j01_episode.py` 与 `test_author_content.py` 需要**已构建的宿主**，属端到端入口。）
* **`tensorize`/`collate` 未做变异测试**：50 条检查的判别力只有断言形态与组名作间接证据。
* **无性能/规模验证**：最大输入 16 实体 / 3 牌堆 / 3 候选，不能外推吞吐或内存。
* **两条登记型 canary 仍是"缺口登记"而非行为验证**：
  `gap-conditional-field-omitted`、`gap-position-on-no-position-schema-pile`（共享侧补规则后会转失败）。
* **入口中途抛异常时计数不完整**（退化为 `0 passed, 1 failed, 1 executed`，退出码 2）——
  "宁可不报数字也不报错数字"的取舍。

## 与共享资产的耦合

`contracts/shared.py` 是**唯一**读取仓库其他目录的模块，它加载并校验：`wire.schema.json`、
`coverage.schema.json`（结构唯一来源）与 `tools/contract-check/` 的 `wire_json`/`wire_canonical`/
`wire_layers`/`jsonschema_min`/`check_contracts`（行为唯一来源），并校验这些模块确实来自该目录
（拒绝同名的第二套实现）。任一资产缺失或被移动会抛 `SpecAssetError`（显式失败，不静默降级）。
除此以外，包内导入只有标准库与相对导入。

## 维护提示

* 期望是**手写**的：共享 schema/规则变化会把失败推到本套件，这是预期信号；修期望前先判断
  "期望写错"还是"契约真的变了"，后者要给反例，**不要放宽检查**。
* 新增 bundle 时同步更新 `fixture_suite.BUNDLES`（`run_checks.py` 自动跟随组名）。
* 新增组：在对应模块里给 `run_all(only=...)`，并在 `run_checks.py` 的 `SUITE_ORDER` 与
  `SUITE_MODULES` 各加一处。
* 运行会在 `tests/` 与 `ai_the_spire/` 下生成 `__pycache__`（可随时删除）。
