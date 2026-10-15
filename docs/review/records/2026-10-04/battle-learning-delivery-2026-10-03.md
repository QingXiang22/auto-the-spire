# D1 可运行战斗学习版本：阶段交接（2026-10-03）

| 项目 | 内容 |
| --- | --- |
| 任务包 | [D1：可运行战斗学习版本交付任务](../../../design/battle-learning-delivery-v1.md)（v1.0） |
| 范围 | D1-A → D1-B → D1-C，外加封板前的 R1（Bully）复核 |
| 执行者 | 单个 AI 集成负责人（本轮不创建 agent；R1 由同一执行者按"已有修复先复核、不重复改"处理） |
| RESUME HERE | 原执行交接的D1正常路径已复核；负责人发现故障记录/累计预算未满足契约，尚未封板。当前按[接续任务D1.1/D2](../../../design/battle-learning-next-step-v1.md)及[唯一清单](../../tofix/README.md)继续，最新结论见第10节；R1已独立复验关闭 |
| 状态声明 | D1 的三条产品检查点都已交付；**不宣称全项目全绿**（§9 列出 1 条既有失败），也不宣称已交付强策略或部署模型 |

本次**没有**新增卡牌/Power/敌人/药水，**没有**扩地图或整局，**没有**迁完整正式源，
**没有**切换默认后端或重选模型，**没有**加 GUI/网页/服务/分布式/训练框架，**没有**提交或清理 Git，
**没有**操作真实游戏。全部输入只来自当前公开快照与公开 catalog。

---

## 1. 用户可复制命令（仓库根目录）

```powershell
# 一次构建；入口之后只启动已构建 Host，不每局调用 dotnet build/run
dotnet build AutoTheSpire.sln

python -m pip install -r ai-the-spire/requirements.txt   # 最小依赖：numpy 1.26.3（Python 3.12.0）

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

# D1 小预算自动化验收（八个场景，需要已构建宿主）
python ai-the-spire/tests/d1_delivery.py
```

`python ai-the-spire/run.py --help`（以及四个子命令的 `--help`）**不需要 NumPy、也不需要已构建 Host**。
输出路径相对调用者工作目录解析并记录绝对路径；输出目录**已非空即拒绝**；`resume` 只读原 run、写新的空目录。

退出码：`0` 完成承诺（含正常 `--stop-after-round`、未改善、有效决策上限截断）、
`2` 用法/准备/配置/identity 或模型错配、`1` 运行故障或资源超限、`130` 用户中断；
最终摘要行固定为 `SUMMARY command=<cmd> exit=<code> category=<类别> reason=<原因>`。

---

## 2. 真实产物（本次实跑生成，未删除）

| 产物 | 路径 | 规模 |
| --- | --- | --- |
| A 可读对局 | [artifacts/d1/demo-initial/episode.md](../../../../artifacts/d1/demo-initial/episode.md) | 0.32 MiB（含公开轨迹） |
| A 公开轨迹 | `artifacts/d1/demo-initial/episodes/`（`index.jsonl` + `records/<episode_id>.json`） | 1 条 `EpisodeRecord` |
| B 训练 run | [artifacts/d1/train/](../../../../artifacts/d1/train) | 11.85 MiB |
| C 对照 | [artifacts/d1/evaluate/comparison.md](../../../../artifacts/d1/evaluate/comparison.md) / `comparison.json` | 6.6 KB / 32 KB |
| D 候选对局 | [artifacts/d1/demo-candidate/episode.md](../../../../artifacts/d1/demo-candidate/episode.md) | 0.32 MiB |
| E 停止/恢复 | `artifacts/d1/smoke-partial/`、`artifacts/d1/smoke-resumed/` | 1.00 / 1.88 MiB |

`artifacts/d1/train/` 内容：`resolved-config.json`、`manifest.json`、`run-state.json`、
`initial-model/`、`candidate-model/`、`checkpoints/round-0001…0008/`（各含
`metadata.json`、`weights.npz`、`optimizer.npz`、`action-samples.jsonl`）、
`episodes/`（32 条公开记录）、`metrics.jsonl`、`summary.json`、`summary.md`。
存储限额 512 MiB/run，实测最大 11.85 MiB。

**机器 / 软件 / 成本（实际测量，口径写在产物字段里）**：Python 3.12.0、NumPy 1.26.3、
dotnet SDK 9.0.306、16 逻辑处理器；模型参数 **5444** 个、权重 **43552** 字节（float64，按 dtype 实际占用，
初始包与候选包同尺寸）；`train` 的命令耗时 167.544 s = 环境交互 24.169 s + 策略更新 6.842 s +
validation 122.906 s + 样本构造等 13.627 s（读 `summary.json` 的 `timings`）。
内存只测**本 Python 进程的工作集**（train 末实测 74 485 760 字节）并注明"不含它启动的 Host 子进程"；
Host/NumPy **进程组 RSS 显式标记未测**（不引入新的监控平台）。

---

## 3. 默认配置与停止原因（全部来自 `describe`/`get_catalog`，不是 AI 猜的）

| 项 | 实际值 |
| --- | --- |
| Gym（由后端 `ruleset_id` 派生） | `net:gym:base-spire-combat-slice-v1` |
| ruleset / catalog | `net:ruleset:base-spire-combat-slice-v1` / `net:catalog:base-spire-combat-slice-v1` |
| 场景 profile（取 `describe` 声明） | `base-spire-combat-slice-v1` |
| 后端 profile / 协议 | `net:backend:generic-engine-runtime-v1` / `0.1` |
| 字典 / 张量布局 | `ai-dict-v1:sha256:68a340c0…` / `ai-tensor-layout-v1` |
| Manifest 摘要 | `sha256:16d9e728d2ef6d5d426e1ff78525bb5e1e110ad6ec3a6c07e44ebea03e1a14e4` |
| 编码配置摘要 | `sha256:9d5233e8d646f39579a91f2060e8e1cbf7787528b9361c0e4f19f642b874f657` |
| 执行器配置 | `net:executor:canonical-cdf-uniform-rng@1` |
| 奖励 Profile | `net:reward:d1-three-component-v1`（`sha256:b2c41ee2…`）= `-0.01*hp_lost + victory - defeat`，γ=0.99 |
| 模型结构摘要 | `sha256:c0107fae682e9d8b…`（`ModelConfig` 默认结构、float64，19 个字段全部落盘） |
| host 开关 | `extra_args = []`（默认战斗 Host，不加 `--author-content`/`--test-headbutt`） |
| Profile 配置摘要 | `sha256:27b5dcfef5fb3868e150bd2f1e3ac201f7f312e85464d44bc0fe3e49f27852a4` |
| 训练 Profile 身份 | `net:training-profile:battle-demo-v1@1` |

**停止原因**：`battle-demo-v1` 的训练在**用尽计划轮数（8/8）后正常结束**，
训练动作 765/4096、命令墙钟 167.544 s（限额 900 s）、存储 11.85 MiB（限额 512 MiB）；
没有触发任何资源限额，没有故障，没有截断轮次。
smoke 的 `--stop-after-round 1` 是**计划的正常停止**（退出码 0），不是故障。

**一处文档内部歧义的显式解释（可复核）**：任务包 §4 表格同时写了
「demo 环境/执行器 seed = 20261001 / 20261002」与「局执行器 seed = 环境 seed + 1000000」。
本实现按**行**取用：`demo` 命令用该行专指的 `20261002`；训练/validation/final/evaluate 的每一局
用「环境 seed + 1000000」（三个评估策略同规则）。两条都写进了 `resolved-config.json` 的 `seeds`
（`demo_executor=20261002`、`executor_offset=1000000`），没有隐藏的第三条规则。

---

## 4. 三个产品检查点

### 检查点 A（D1-A：可看的一局）— 已交付，可试用

用户拿到什么：`run.py demo` 在真实默认 Gym 上跑一局，终端与 `episode.md` 逐决策显示
**步号/回合、玩家 HP/格挡/能量、手牌公开 ID 与实例属性、敌人 HP/意图/Power、公开牌区与已知位置、
全部合法候选及概率、实际选择（π=μ）、执行后的公开变化**；终止快照单独成段、不计假动作；
开头写明真实 Gym/scenario/种子/模型 ID/是否训练过，结尾写明胜负/截断/故障、确认动作数、耗时与产物路径。

本次实测：`demo-initial` → `terminal`（负），确认动作 **19**，耗时 0.513 s，
模型 `small-embedding-scorer@1:c0107fae682e:50eb6abea1a1`，状态 `untrained(random-init)`；
公开轨迹 1 条记录，`EpisodeStore.verify_all()` 无问题。

### 检查点 B（D1-B：有限真实训练）— 已交付

用户拿到什么：`run.py train` 按冻结 Profile 真实采样并更新，逐轮输出
轮号 / 冻结行为模型 ID / 完成·截断·故障局数 / 确认决策数 / 未折扣回报与 loss / 更新后的候选 ID /
耗时 / 检查点路径；run 目录含 `resolved-config.json`、`manifest.json`、`initial-model/`、
`candidate-model/`、`checkpoints/round-XXXX/`、公开 `episodes/`、`metrics.jsonl`、`summary.json`、
`summary.md`、`run-state.json`。

**权重与身份确实变了**（W-04 已复现的"权重变了但 model_id 不变"缺陷已修）：

| 轮 | 行为模型 → 候选模型 | 确认决策 | 未折扣回报 | loss | grad_norm | optimizer steps |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | `…50eb6abea1a1` → `…4a448e88e1f0` | 100 | −1.8 ×4 | 1.551807 | 1.000（裁剪） | 2 |
| 2 | `…4a448e88e1f0` → `…526251b09cea` | 99 | −1.8 ×4 | 0.499014 | 1.000（裁剪） | 4 |
| 3 | `…526251b09cea` → `…2fd15e32e60f` | 88 | −1.8 ×4 | −0.123017 | 1.000（裁剪） | 6 |
| 4 | `…2fd15e32e60f` → `…112aefd8ab84` | 96 | −1.8 ×4 | −0.279937 | 1.000（裁剪） | 8 |
| 5 | `…112aefd8ab84` → `…e0bb6d5b40be` | 97 | −1.8 ×4 | −0.179676 | 1.000（裁剪） | 10 |
| 6 | `…e0bb6d5b40be` → `…bba4d18d6e0a` | 85 | −1.8 ×4 | 0.026234 | 1.000（裁剪） | 12 |
| 7 | `…bba4d18d6e0a` → `…499ab79568cd` | 93 | −1.8 ×4 | 0.244011 | 1.000（裁剪） | 14 |
| 8 | `…499ab79568cd` → `…210728c580e9` | 107 | −1.8 ×4 | 0.155309 | 1.000（裁剪） | 16 |

逐轮的"行为模型 = 上一轮候选模型"链条成立；`initial-model` 的 `training` 为
`none(structural,random-init)`，`candidate-model` 为 `ppo(steps=16)`，两者独立保留。
32 局全部走到 `terminal`（负），0 截断 0 故障；**每一局的 `limit_reason` 为 null**（没有靠截断凑预算）。
validation（均匀/初始/当前 × 保留 validation 种子 20000..20007）在训练前与每轮成功后各跑一次，
**只报告曲线**：平均未折扣回报都是 −1.8000；确认决策 177（均匀）/ 166（初始）/ 161（当前，第 1 轮后稳定）。

### 检查点 C（D1-C：比较与恢复）— 已交付

三策略对照（8 个 final 保留种子 30000..30007，每 (策略, 种子) 各新开独立会话，
同 Gym / 同限额 / 同执行器规则 `执行器种子 = 环境种子 + 1000000`）：

| 策略 | 平均值（未折扣回报） | 样本标准差 | 95% 自助区间 | 平均确认决策 | 平均墙钟(ms) | 完成/截断/故障 | 总耗时 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `baseline:uniform-legal-candidates@1` | −1.8 | ≈0（1.2e−16 浮点噪声） | [−1.8, −1.8] | 25.000 | 291.8 | 8/0/0 | 2.33 s |
| `initial-model`（untrained） | −1.8 | ≈0（1.5e−16） | [−1.8, −1.8] | 23.875 | 801.4 | 8/0/0 | 6.41 s |
| `candidate-model`（trained） | −1.8 | ≈0（1.5e−16） | [−1.8, −1.8] | 23.875 | 680.9 | 8/0/0 | 5.45 s |

同种子配对（差值 = 策略 − 均匀基线；自助重抽 1000 次、seed=20261003）：

| 候选 | 指标 | 平均差值 | 区间 | 判定 |
| --- | --- | --- | --- | --- |
| initial | `reward_scalar` | 0.0000 | [0.0000, 8.3e−17] | **undecided** |
| initial | `steps` | −1.1250 | [−3.3750, 0.0000] | **undecided** |
| initial | `wall_ms` | +509.62 | [428.75, 595.35] | **worse**（模型推理开销） |
| initial | `terminal_win` | 0.0000 | [0.0000, 0.0000] | **undecided** |
| candidate | `reward_scalar` | 0.0000 | [0.0000, 8.3e−17] | **undecided** |
| candidate | `steps` | −1.1250 | [−3.3750, 0.0000] | **undecided** |
| candidate | `wall_ms` | +389.06 | [342.12, 440.50] | **worse** |
| candidate | `terminal_win` | 0.0000 | [0.0000, 0.0000] | **undecided** |

计划摘要 `sha256:72c758d3e797bc8b99f09d36243cf321fe3f8d650047fa2da91ce0a614cd3e44`；
报告摘要 `sha256:f966b22b2bb66659a9b059d705d909df8597ac02bde88eb875edd4c12dc1e8f9`；
命令总墙钟 14.382 s（限额 180 s）；无失败局；`evaluate` 使用 final 种子，**train 从未读取 final 种子**。

**如实报告：没有改善。** 三个策略在这 8 个种子上都输（HP 80 全损 + 失败 ⇒ 回报恒为 −1.8）。
`evaluate` 另外记录了**执行器分辨率**事实（`comparison.json` 的 `action_sequence_equality`）：
candidate 在 2/8、initial 在 3/8 个种子上与均匀基线走出**完全相同的动作串**——
"回报相同"是这件事的结果，**不是策略等价的证据**，也不能当成改善证据。

---

## 5. 恢复（resume）结果

| 项 | smoke-partial（stop-after-round 1） | smoke-resumed（resume 到 2 轮） | 连续两轮（对照，由 `d1_delivery.py` 在临时目录现场生成） |
| --- | --- | --- | --- |
| 完成轮数 | 1 / 2 | 2 / 2 | 2 / 2 |
| 累计训练动作 | 46 | 100 | 100 |
| optimizer steps | 2 | 4 | 4 |
| round-0002 `model_id` | — | `…b8152867abf8` | `…b8152867abf8` |
| round-0002 权重摘要 | — | `sha256:b8152867abf8…` | 同 |
| 第 2 轮 loss | — | 0.454481 | 0.454481 |
| 命令耗时 | 15.500 s（限额 180 s） | 8.911 s | — |

逐元素核对（resume 的 `checkpoints/round-0002` vs 连续运行的同一检查点，由验收 `resume` 组实跑）：

* 28 个参数名与形状一致；**全部参数逐元素相等**（`np.array_equal` 全真，最大绝对差 `0.0`）；
* 优化器（Adam 一/二阶矩）全部键逐元素相等；
* `training_state` 的 `steps=4`、`rounds_completed=2`、`training_actions_used=100` 一致；
* `model_id` 与 `weights_digest` 一致（耗时与运行标识不要求一致，二者确实不同）。

其它恢复契约：`resume` 前对 `smoke-partial` 做整目录摘要，`resume` 后逐字节一致（**旧 run 未被覆盖**）；
新目录含 `resolved-config.json`、`manifest.json`（带 `resumed_from`）、`initial-model/`、
`candidate-model/`、`checkpoints/`、`episodes/`、`metrics.jsonl`、`source-index.json`，**不需要编辑旧路径**；
对已完成的 run 再 `resume` → 退出码 2（"没有剩余轮次可恢复，不会额外增加目标轮数"）；
删掉 `checkpoints/round-0001` 后 `resume` → 退出码 2（未提交轮次不能伪装已完成）。

---

## 6. 实际执行的检查与返回码

### D1 验收入口（本次新增）

| 命令 | 返回码 | 结果 |
| --- | --- | --- |
| `python ai-the-spire/tests/d1_delivery.py` | 0 | `TOTAL: 192 passed, 0 failed, 192 executed`（bounded 24 · ending 23 · visible 16 · update 60 · source 11 · load 8 · compare 23 · resume 27） |

任务包 §7 的八行验收场景与检查组的对应（每行都有**可判别的失败方式**，不是"跑通即通过"）：

| 场景 | 检查组 | 关键判别 |
| --- | --- | --- |
| VISIBLE | `visible` | `episode.md` 的动作段数 == 记录的已确认转移数；轨迹里的行为概率必须出现在对局文本里；`EpisodeStore.verify_all()` 为空 |
| UPDATE | `update` | 初始/候选 `model_id` 与 `weights_digest` 必须不同且候选人身份跟权重走；权重数组确实变化；行为模型=上一轮候选；每轮检查点带优化器与逐动作样本；训练动作不超预算 |
| SOURCE | `source` | 篡改 Gym 身份 / 奖励 Profile 摘要 / 轨迹行为概率 / 模型包 Manifest 摘要都必须在**启动游戏前或建样本前**被拒；同冻结模型+同快照⇒同概率；初始==候选身份被拒 |
| ENDING | `ending` | `max_steps=1` ⇒ `truncated/decisions` 且留下 bootstrap 快照；每局墙钟 ⇒ `truncated/wall_time` 且训练路径拒绝提交；最后动作进终局 ⇒ `terminal` 且终止快照无候选；断连 ⇒ `interrupted`、记录分类为 interrupted、无伪造 terminal |
| LOAD | `load` | `--model candidate` 的 `model_id` 与包内一致、状态 `loaded-bundle`；权重损坏/包缺失 ⇒ 退出码 2 且不产出 `episode.md` |
| COMPARE | `compare` | 3 策略 × 4 指标 × 2 候选的配对比对；种子必须是 validation 而非 final；重复运行的计划摘要与回报/步数指标一致 |
| RESUME | `resume` | 参数与 Adam 逐元素相等、身份/计数一致；旧 run 整目录摘要不变；无剩余轮次与缺检查点都返回 2 |
| BOUNDED | `bounded` | help 在无 NumPy 下可用、跑命令时缺依赖 ⇒ 2 且给准备步骤；冻结值被篡改 ⇒ 2；非空输出目录 ⇒ 2 且不删旧文件；缺 Host ⇒ 2 并给构建步骤；**失败命令也不留下孤儿 Host 进程** |

### 现有回归（本次实跑；AI 侧在改动 `runner/episode.py`、`models/policy.py`、`reward/derive.py` 之后）

| 命令 | 返回码 | 实际范围 |
| --- | --- | --- |
| `dotnet build AutoTheSpire.sln` | 0 | 0 警告 0 错误 |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | 0 | `All 244 slice checks passed`（含 R1 的 Bully 检查） |
| `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` | 0 | `All 64 engine checks passed` |
| `dotnet run --project core-the-spire/Core.TheSpire.Tests` | 0 | `190 passed / 0 failed / 190 executed`（Legacy） |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | 0 | `393 passed / 0 failed / 393 executed` |
| `python net-the-spire/client/test_client.py` | 0 | `50 passed, 0 failed, 50 executed` |
| `python net-the-spire/client/test_author_content.py` | 0 | 作者开局 + 长期牌组 + 用药水（药水栏 2 → 1，Shrink(4)） |
| `python tools/contract-check/check_contracts.py` | 0 | `210 passed, 0 failed, 210 executed` |
| `python tools/coverage-check/check_slice_coverage.py` | 0 | 分母 94 / 已迁 22 / 剩余 72（原型口径不变） |
| `python tools/dependency-check/check_dependencies.py` | 0 | `9 passed, 0 failed`；56 项目 0 环、违规 0 |
| `python ai-the-spire/tests/run_checks.py` | 0 | `604 passed, 0 failed, 604 executed` |
| `python ai-the-spire/tests/j01_episode.py` | **1** | `218 passed, 1 failed, 219 executed`（见 §9：过期期望，先于本次改动） || `python ai-the-spire/tests/d01_records.py` | 0 | `106 passed, 0 failed, 106 executed` |
| `python ai-the-spire/tests/d02_collector.py` | 0 | `45 passed, 0 failed, 45 executed` |
| `python ai-the-spire/tests/d03_audit.py` | 0 | `89 passed, 0 failed, 89 executed` |
| `python ai-the-spire/tests/d04_longrun.py` | 0 | `50 passed, 0 failed, 50 executed` |
| `python ai-the-spire/tests/p02_measure.py` | 0 | `32 passed, 0 failed, 32 executed`（只报告测量） |
| `git diff --check` | 0 | 干净（只有既有文件的 LF/CRLF 提示） |

> **后续修订**：上面 J-01 的 1 条失败已在 D1.1-T 修复——断言改为从后端实际声明派生并对齐 v2，
> 修复后 `j01_episode.py` **219 passed, 0 failed（返回码 0）**；详见 §11.1 与 §11.4。
> 本节其余行是 D1 会话当时的实跑记录，保留不改。

---

## 7. R1（Bully）复核：已有修复已生效，**未做任何改动**

R1 的验收点是"复现此前零易伤预期 4 实 0，最小修复；核对零/正层及普通/升级；
缩放基础值与易伤最终修正分开；适用检查及 Legacy 回归通过；不借此新增其他内容"。

复核结论：**当前工作区的实现已经满足这些点，不需要重复修改**（按 R1 的"已有修复先复核，不重复改"）。
证据是切片套件里那条可判别检查的真实通过（`SliceFormalSourceChecks.cs:1296`，
`targetPowerAmount` 数值来源）：

| 情形 | 断言 | 说明 |
| --- | --- | --- |
| 目标 0 层易伤 | **4** | `4 + 2 × 0`，乘性阶段没有易伤可乘 |
| 目标 3 层易伤 | **15** | 来源给 `4 + 2×3 = 10`，随后受击方易伤 **×1.5** ⇒ 15（缩放基础值与易伤最终修正分开） |
| 升级变体（perCount 2→3）、3 层易伤 | **19** | `(4 + 3×3) × 1.5 = 19.5`，**单次截断** ⇒ 19（不是 19.5 也不是 20） |
| 目标 3 层**虚弱**（不是易伤） | **4** | 判别性对照：来源读的确实是易伤层数 |

实跑：`dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` → **返回码 0，
`All 244 slice checks passed`**；Legacy（`Core.TheSpire.Tests`）**190/190 返回码 0**。
未改任何 `.spire` 内容、未新增卡牌/能力、未放宽检查。

---

## 8. 本次改动文件

| 文件 | 改动 |
| --- | --- |
| `ai-the-spire/run.py` | **新增**：D1 用户入口（demo/train/evaluate/resume），顶层只用标准库；缺依赖映射到退出码 2 |
| `ai-the-spire/configs/battle-demo-v1.json` | **新增**：冻结工程演示 Profile（8 轮 × 4 局 / 4096 动作） |
| `ai-the-spire/configs/battle-demo-smoke-v1.json` | **新增**：冻结 smoke Profile（2 轮 × 2 局 / 512 动作） |
| `ai-the-spire/requirements.txt` | **新增**：最小依赖声明（numpy 1.26.3） |
| `ai-the-spire/ai_the_spire/delivery/` | **新增**：`errors`（退出码契约）、`config`（冻结 Profile 解析/自检）、`runtime`（Host/输出目录/有界客户端/资源计数/机器报告）、`console`（公开快照 → 可读文本与差异）、`reward_v1`（D1 三分量逐动作奖励与折扣回报）、`environment`（身份观察与严格校验）、`model_access`、`episodes`（跑局与结束分类）、`demo`、`train`、`evaluate`、`commands` |
| `ai-the-spire/tests/d1_delivery.py` | **新增**：八个场景的小预算自动化验收入口（192 项；含退出后回收自己启动的 Host） |
| `ai-the-spire/ai_the_spire/models/policy.py` | `SmallPolicyModel.refresh_identity()`：按**实际权重内容**重算 `weights_digest`/`model_id`（修"权重变了身份不变"） |
| `ai-the-spire/ai_the_spire/reward/derive.py` | 增加 `net:reward:hp_lost = max(0, hp_before − hp_after)` 派生规则（D1 固定语义；不改既有分量） |
| `ai-the-spire/ai_the_spire/runner/episode.py` | `EpisodeResult.limit_reason` / `final_snapshot`；`EpisodeRunner` 增加 `time_limit_s` 与 `capture_final_snapshot`（默认关闭，现有调用方行为不变） |
| `ai-the-spire/README.md` | D1 用户入口、命令、产物、验收与**如实报告**的未验证项 |
| `docs/review/tofix/README.md` | 本轮 D1/R1 认领、进度与证据（只追加，不清空 W 项） |
| 本文件 | 阶段交接 |

**未改动**：`net-the-spire/**`、`core-the-spire/**`、`mod-the-spire/**`（R1 只读复核）、
`docs/contracts/**`、`tools/contract-check/**`、`sts2/**`、`reference/**`、真实游戏。

---

## 9. 未完成项与残余边界（不要据此宣称更多）

1. **J-01 `author` 组有 1 条失败，先于本次改动，未修改他人测试。**
   实测：`python ai-the-spire/tests/j01_episode.py` → `218 passed, 1 failed`，失败用例
   `author/gym-identity-follows-backend`，期望 Gym 身份以 `author-content-v1` 结尾
   （[j01_episode.py:538](../../../../ai-the-spire/tests/j01_episode.py#L538)），而后端现在声明
   `net:ruleset:base-spire-author-content-v2`
   （[SlicePublicProfile.cs:135](../../../../net-the-spire/Net.TheSpire/Projection/SlicePublicProfile.cs#L135) 的 `AuthorRulesetV2`；
   net 侧 393/393 与作者探针 rc=0 都按 v2 通过）。
   即 **C# 侧一致地换到了 v2、AI 侧这条期望没跟着更新**；与本次改动无关（`gym_id_from`/`describe` 路径未改）。
   处置建议：由 W-01 所属泳道把该期望对齐到 v2（一行）；本执行者按"不扩大当前 goal、保留他人工作"未自行修改。
2. **没有学到更好的策略**：8 个 final 种子上三策略平均回报都是 −1.8（全败），配对判定 undecided；
   只有 `steps` 略降（−1.125，区间跨 0）与 `wall_ms` 变差。工程闭环成立 ≠ 学习效果成立。
3. **评估/训练只覆盖一个固定 Gym 内的有限种子对照**，不声称多场景泛化或完整 STS 环境；
   `validation` 与 `final` 严格分开，训练未消费 final 种子。
4. **执行器分辨率**：当前小模型与均匀分布的概率差异很小（约 0.02 概率质量），
   部分种子上两策略执行同一串动作；`evaluate` 已把这件事写进产物。要区分策略需要更大的概率差异或更多种子。
5. **每局墙钟限额在 Windows 上粒度约 15.6 ms**（`time.monotonic()` 用系统时钟节拍）；
   60 s 预算下无影响，但极短限额下不能断言"恰好 0 个动作"。
6. **机器报告不含进程组 RSS**：只报 Python `tracemalloc` 分配峰值并注明口径，进程组 RSS 显式"未测"；
   不为 D1 引入新的监控平台。
7. **默认 Host 内容仍是受控切片**（原型作者变体 22/94），正式源宿主接入、作者内容扩面、
   完整整局、事件/商店/营火/遗物等仍归 W-01/W-05/W-06 后续，不在本次范围。
8. **未运行**：`tools/collect/collect.py`、`tools/doc-check`、`tools/orimod-decompile` 等与 D1 无关的入口；
   真实游戏与多人场景未操作。


## 10. 负责人复核（2026-10-04）

本节为最新负责人结论，第1～9节保留执行者2026-10-03交接。**D1正常主路径已交付，可以试用；故障事实保存、实际采样预算与恢复累计预算未满足原契约，暂未封板。**没有学习改善不构成D1工程失败，但真实可靠性缺口不能被192项通过替代。当前下一步以[接续任务](../../../design/battle-learning-next-step-v1.md)及[唯一清单](../../tofix/README.md)为准。

### 10.1 已做成的用户成果与独立复验

复核实际入口、产物和代码，并重跑检查；验证跨2026-10-03～04。未修改生产代码或测试，未提交/清理共享工作区。

| 本轮独立命令 | 结果 / 范围 |
| --- | --- |
| dotnet build AutoTheSpire.sln --no-restore | 返回码0，0警告0错误 |
| python -X utf8 ai-the-spire/tests/d1_delivery.py | 返回码0，192/192；包括有界真实smoke采样/更新和正常停止恢复 |
| python -X utf8 ai-the-spire/tests/run_checks.py | 返回码0，604/604 |
| python -X utf8 ai-the-spire/tests/j01_episode.py | 返回码1，218通过/1失败；author/gym-identity-follows-backend的author-content-v1硬编码与后端v2不符 |
| dotnet run --no-build --project mod-the-spire/base-spire/BaseSpire.Slice.Tests | 返回码0，244项通过，Bully零/正层与普通/升级判别成立 |
| dotnet run --no-build --project core-the-spire/Core.TheSpire.Tests | 返回码0，190 passed / 0 failed / 190 executed；Legacy范围 |
| dotnet run --no-build --project net-the-spire/Net.TheSpire.Tests | 返回码0，393 passed / 0 failed / 393 executed |

Engine64、client50、契约210、依赖9及作者探针是执行交接的报告证据，本轮负责人未重跑，不写成独立复验。未操作真实游戏、完整Run、部署或扩大训练。文档/追踪检查在本节结尾单独登记。

已存在的可交付产物：demo-initial有19个确认动作的可读对局；train有32局、765确认动作、8轮checkpoint与不同权重身份的initial/candidate-model；最终三策略各8局全部失败、平均未折扣回报都−1.8。正常smoke停下恢复与连续两轮权重/Adam/训练位置一致的检查本轮通过。这些成果不再重复派单。

训练summary记录167.544秒，其中validation122.906秒（约73%）、采样24.169秒、更新6.842秒；模型5444参数、float64权重43552字节。当前成本主要是反复评估，不是参数规模已有证据过大。本阶段不先优化运行编排或重选架构；以后减少固定基线重复评估须版本化实验配置，保留配对协议。

net作者Profile v2已发布LostHpThisTurn、ExhaustedCardThisTurn和Card.Variable_extra-damage，目录/快照/同HP不同回合摘要的成对检查包含在上述393项中。原W-01“先发布摘要”不再派单，剩余AI专项编码差异验收及正式源Host接入仍开放。Bully/R1关闭依据已单独放入[archived](../../archived/d1-r1-closure-2026-10-04.md)，不重修、不连带关闭W-06。

### 10.2 主线发现：任务可完成，模型仍未改善

负责人做了有限只读诊断：在默认真实Host、仅用当前公开字段与合法候选的规则策略，环境seed40000～40007，执行器seed=环境seed+1000000；优先可打攻击牌，再其他可打牌，最后EndTurn，攻击牌优先公开Amount，敌人选公开HP最低者。8局都Victory，确认动作分别32/34/31/30/36/31/35/33。使用现有NetClient/EpisodeRunner，每局128动作上限；策略返回合法候选one-hot，未读取隐藏状态、未更新模型、未写入训练集、未用D1 final种子。这是本轮临时诊断输出，尚未交成可重复使用的生产命令/轨迹包；D2负责交正式诊断产物。

这排除了“这些受控种子没有可获胜路径”的解释，不证明规则最优、完整原版可用或32局足以学会。训练全败且HP从80耗尽，在当前工程奖励下未折扣回报自然都−1.8，丢失不少失败策略之间的区分；折扣回报和时序仍可能不同，不能单凭同回报认定梯度无效。

离线探针：按训练episodes/records文件名排序取前8个公开EpisodeRecord，seed依次10024/10009/10004/10022/10005/10002/10020/10008；重建原Manifest、加载initial/candidate-model，对同一批202个非终止有合法候选快照推理。TV均值0.009739875、最大0.017424326；TV定义为0.5×候选概率绝对差之和。约1%的概率质量变化，参数各分组确有变化；不是“根本没有更新”的证据。D2应进一步检查学习信号、执行概率与更新幅度，不先靠扩大预算或模型解决。

### 10.3 实际封板缺口及有限处置

| 项目 | 可定位证据 | 处置 |
| --- | --- | --- |
| 故障当前局事实未落盘 | delivery/train.py:281～288先run_episode(require_usable=True)，返回后才build_record/write；episodes.py:83开启runner抛异常，98～107也在返回EpisodeRun之前抛FaultError；runner._interrupt在raise模式丢失结果对象。负责人内存注入faulted结果后确认抛出异常仅含reason，无结果/已确认转移；train外层catch只保存摘要，未保存故障局 | D1.1-F，在当前局保留真实事实与结束类别后停止；不得把故障当defeat或样本补齐。源码/包装注入已确认，真实train落盘注入验收由执行者补 |
| 实际与恢复预算未累计 | train.py:310～334采完整轮才检查动作上限、update后才累计；830新建完整900s/180s CommandBudget；868仅从已提交checkpoint恢复training_actions_used。runtime.CommandBudget没有来源累计耗时 | D1.1-B，分离实际消耗与已提交训练状态；执行前限制剩余额度，故障/未提交轮消耗也保留，resume来源链不得返还额度；先修而不称预算正确 |
| J作者版本失败 | tests/j01_episode.py:538要求author-content-v1，真实ruleset/Gym同为v2；目录/投影当前已升级 | D1.1-T，保留派生和身份断言，仅对齐适用版本依据，不删检查 |
| 动作数方向不成立 | delivery/evaluate.py:230将原始确认决策数设higher_is_better；更多动作可能只是更慢、更分散或更久存活 | D1.1-M，作为描述性行为/成本信息，不独立判策略改善；旧对照表保留历史，修后新产物写新口径 |

D1更新路径显式调用refresh_identity，当前产物身份成立；通用Trainer本身不会刷新、save_bundle仍依赖调用方。API防误用保留W-04后续，本次不据此重写模型库。

下一步严格为一个D1.1小包→一个D2可见diagnose交付。后者只评估四策略/同局面探针及现有学习记录，最多32局/180秒；不新增训练、不消费final、不做GUI/新内容/完整Run。提出一个最小可区分实验建议，交负责人冻结后再训练。故障/预算未结，本混合报告保持tofix，不整份放archived。

### 10.4 文档整理验证

更新唯一清单、开工清单v0.7、接续任务/goal与文档导航，修复D1交接相对链接及当前任务锚点，保留完整W-01～W-08。`python -X utf8 tools/doc-check/check_docs.py`扫描170份Markdown，0问题；`check_trace.py`核对20项需求，0不一致；`git diff --check`返回码0（仅现有Git换行提示）。另单独核对本次修改的10份文档尾部空白，避免未跟踪文件不在Git diff检查范围的遗漏。检查成立不代表实现缺口已关闭。

---

## 11. D1.1 收口与 D2 诊断（2026-10-04，执行者）

本节为[接续任务](../../../design/battle-learning-next-step-v1.md)的两个产品检查点。第 1～9 节是 D1 执行者交接，
第 10 节是负责人复核；**不重新实现 D1 正常路径、不重修已关闭的 R1**。

### 11.1 D1.1 产品检查点（四项收口，已交付）

负责人 §10.3 给出的四个可定位缺口，逐项处置与证据：

| 缺口 | 处置 | 判别证据 |
| --- | --- | --- |
| **F 故障当前局事实未落盘** | `runner/episode.py` 的 `_interrupt` 在**两种模式**下都先把 `result.ending/ending_detail` 写成自描述的 `interrupted`；raise 模式改抛 `EpisodeInterrupted`，它**携带** `result`。`delivery/episodes.py` 新增 `EpisodeFault(FaultError)` 携带部分 `EpisodeRun`；`train.py` 的 `_collect_episode` 在抛出前把该局写进 `episodes/`（复用既有 `EpisodeRecord` 格式）、写 `metrics.jsonl` 失败行并记账 | `tests/d1_1_closure.py --only fault` **20/20**：真实 `train.run` 调用链上注入断连（第 2 个训练 step 后杀宿主）后，磁盘上出现 `ending_class=interrupted`、无 terminal、保留 ≥1 条已确认转移与全部公开快照、`unknown` 转移不补后状态/不声称候选、`EpisodeStore.verify_all()` 无问题、`metrics.jsonl` 标 `usable_for_training=false` 带 `failure_reason`、`checkpoints/round-0001` **不存在**、`run-state.rounds_completed=0`、`summary.exit_category=fault`、账目记到实际消耗 |
| **B 实际与恢复预算未累计** | 新增 `delivery/budget.py`（`budget-ledger.json`）：**实际消耗**只增不减，**已提交状态**单独前移；每局开始前按剩余额度限流（本局上限 = min(每局限额, 剩余额度)）；命令墙钟按"来源链已用 + 本次活动"累计（暂停不计）；耗尽时不启动 Host/不开新局；resume 继承整本账；缺账目的旧 run 明确拒绝 | `--only budget` **26/26**：未提交轮消耗跨 resume 保留（46 → 48 → 不再返还）、`committed` 不随之前移、动作上限压到 50 后**恰好用满 50 不超采**（第 2 轮首局被削到 4 个动作）且不再开新局、累计时间为 0 时**宿主调用数 = 0**、来源链超时的 resume 也不起 Host |
| **T J-01 author 版本断言** | `tests/j01_episode.py:536` 的硬编码 `author-content-v1` 改为**从实际声明派生并对齐 v2**（仍断言 `gym_id == gym_id_from(ruleset_id)`，并交叉核对 catalog 也是 v2），不删检查、不改默认 Gym 身份 | `python ai-the-spire/tests/j01_episode.py` → **`219 passed, 0 failed, 219 executed`（返回码 0）**（修复前 218/219） |
| **M 动作数方向不成立** | `evaluation/plan.py` 新增**中性方向** `descriptive`；`evaluation/runner.py` 对中性指标只报差值、判定固定为 `descriptive`（永不给 improved/worse）；`delivery/evaluate.py` 的"确认决策数"改用该方向并加口径说明 | `--only metric` **7/7**：`steps.direction == "descriptive"`、其判定不再是 improved/worse，而 `reward`/`terminal_win` 仍保留带方向的配对比较；`comparison.md` 明写"描述性行为/成本" |

**旧产物处理（按接续任务 §2 的预算口径）**：D1.1 之前的 `artifacts/d1/**` **未被覆盖**。
它们没有 `budget-ledger.json`，因此：`demo`/`evaluate`/`diagnose` **照常可用**（本节 D2 就是直接读它们），
而 `resume` 会**明确拒绝**（返回码 2，提示"没有累计账目，拒绝猜剩余预算"）。
需要 resume 的用户请用当前代码重新跑一次 `train`（本节 E 组证据就在临时目录里现场生成）。
`ai-the-spire/README.md` 已写明这一点。

**D1.1 的检查入口**：`python ai-the-spire/tests/d1_1_closure.py` → **`TOTAL: 57 passed, 0 failed, 57 executed`（返回码 0）**
（fault 20 · budget 26 · metric 7 · legacy 4）。它**不替代** D1 的 192 项：D1 验收仍单独跑并通过。

### 11.2 D2 产品检查点（可见的学习诊断，已交付）

```powershell
python ai-the-spire/run.py diagnose --run artifacts/d1/train --output artifacts/d2/diagnose
```

只读诊断：**不调用 `Trainer`**、不更新模型、不写训练集、不改原 run；只访问诊断种子 **40000～40007**
（执行器种子 = 环境种子 + 1000000），**未访问** final 30000～30007 与未来保留 50000～50007。
命令总限额 180 s / 4096 个确认动作 / 512 MiB；实际使用 **857 个确认动作、约 27 s、0.50 MiB**。

产物（`artifacts/d2/diagnose/`）：`diagnosis.md`（18 KB）、`diagnosis.json`（433 KB）、
`reference-episode.md`（73 KB，参考策略首局的逐决策可读对局）与 `reference-episodes/`
（公开 `EpisodeRecord`，`store.verify_all()` 无问题）。

**四策略同条件对照**（8 个诊断种子，同 Gym/限额/执行器）：

| 策略 | 平均未折扣回报 | 平均确认决策 | 胜局 | 平均墙钟 | 完成/失败 |
| --- | --- | --- | --- | --- | --- |
| 均匀基线 | −1.8000 | 24.625 | 0/8 | 376 ms | 8/0 |
| 原初始模型 | −1.8000 | 25.000 | 0/8 | 752 ms | 8/0 |
| 原训练候选 | −1.8000 | 24.750 | 0/8 | 876 ms | 8/0 |
| **公开信息规则基线** `d2:rule:public-info-greedy@1` | **+0.6225** | 32.750 | **8/8** | 596 ms | 8/0 |

* 规则 vs 均匀：回报 `improved` 差值 **+2.4225**（区间 [2.3750, 2.4700]）、`terminal_win` `improved` **+1.0**；
* 初始/候选 vs 均匀：回报 `undecided`（差值 0.0，区间 [−8.3e−17, 0.0]）、`wall_ms` `worse`；
* 规则相对（对照 = 规则基线）：均匀/初始/候选三个策略的回报都是 `worse` **−2.4225**、`terminal_win` `worse` **−1.0**；
* 确认决策数的所有配对都是 `descriptive`（D1.1-M 的新口径）。

**规则基线的动作数与负责人独立探查逐局一致**：[32, 34, 31, 30, 36, 31, 35, 33]（负责人 §10.2 记录同为
32/34/31/30/36/31/35/33）。这是一次独立复现，不是同一份数据的转抄。
参考对局（seed 40000）**胜利**、32 个确认动作，`episode_id=ep-635b1276d718288f5e8fa220`，
`record_digest=sha256:11d543c60f14b4b0cf243cb2eb3e56c0b25fbe3614c2701cc78d65802a48b3b6`；
`reference-episode.md` 末尾逐动作给出规则的优先级理由（含 `answer_decision` 选公开 HP 最低者）。

**同局面概率探针**（按 `episodes/index.jsonl` 次序取最早 8 局，seeds 10000～10007；
只比较**同一份公开快照**上初始/候选模型的概率，不比较两条不同轨迹）：

* 有合法候选的公开快照 **199** 个；TV = 0.5×Σ|p_候选 − p_初始|；
  **均值 0.009704、最大 0.017424、p50 0.010866、p90 0.012786**；
* 存在结束回合候选的局面 **141** 个：p(EndTurn) 均值 **0.3680 → 0.3775**（候选略微更倾向结束回合）；
* **带剩余能量仍选择结束回合 22 次**；
* 逐局面（含每个候选在两个模型下的概率）全部保留在 `diagnosis.json`。

**学习信号与更新检查**（读公开记录 / `action-samples` / `metrics`，不重跑训练）：

* 32 局全部 `terminal_loss`，玩家 HP 80 → 0；未折扣回报只有 `−1.8`（另一个是 −1.7999999999999998 的浮点噪声）；
* 765 个训练样本：`V_old` 均值 −1.2624、`G` 均值 −1.4400、`A` 均值 −0.1775（正优势占 41.18%）、
  单步奖励均值 −0.0753；**π = μ 在 765 条样本上逐条成立**；
* **折扣方向（本节最可定位的发现）**：局长度与折扣回报的相关系数 **0.9856（γ=0.99）**，
  方向是**局越长折扣回报越高**——因为每局都失败且未折扣回报相同，折扣把 −1 的失败惩罚推得越远，
  回报就"越好"。于是当前唯一可学的方向是"**把失败推远**"，而不是获胜；
* 参数分组全部变化（相对 L2 变化 2.75%～6.28%：emb 2.75%、relation 4.96%、value 5.01%、
  entity 5.76%、field 6.17%、global 6.21%、candidate 6.28%）；
* 梯度：`max_grad_norm=1`，**3/8 轮触发裁剪**，`grad_norm` 均值 1.289；`lr=0.002`、`epochs=2`；
* 成本占比（按分项求，`command_s` 不重复计入分母）：**validation 73.4%**、环境交互 14.4%、
  样本构造 8.1%、策略更新 4.1%——与负责人 §10.1 的观察一致；
* **来源 run 缺字段如实列出**：`artifacts/d1/train` 是 D1.1 之前产出的，`metrics.jsonl` 没有
  `confirmed_transitions`/`unknown_actions`/`usable_for_training`/`reward_problem`/`failure_reason`/`budget`；
  诊断改用公开记录的 `summary.confirmed_transitions` 并把这些字段标为"来源 run 无此字段（未测）"，不用默认值补。

**接续实验建议**（`diagnosis.json` 的 `follow_up_experiment`，已写全唯一自变量/固定条件/最大采样量/评价/停止条件）：

* **已排除**：任务不可完成（规则基线 8/8 胜）；完全没有更新（参数分组全变、同局面 TV 均值 0.0097）；
  采样/执行概率错配（π=μ 逐条成立且与冻结模型重算一致）。
* **仍未判定**：表示遗漏（本轮未测，需成对可区分局面的编码检查）；更新幅度/裁剪影响（部分未测）；
  优势符号 vs 局内位置的相关（未测）。
* **建议的唯一自变量**：`reward.discount` **0.99 → 1.0**；其余（Gym/种子/PPO 超参/模型/预算）全部不变，
  预算仍为 8 轮 × 4 局 / 最多 4096 个训练动作 / 900 s。理由：它只改"同一批真实轨迹如何变成回报"，
  把 γ 设为 1.0 会消除"推远失败惩罚"这条伪方向，从而把"样本不足"与"信号方向问题"分开。
* **两种结果的判读**都已写明；本命令**只建议**，是否开训由负责人冻结。

**D2 的验收入口**：`python ai-the-spire/tests/d2_diagnose.py` → **`TOTAL: 69 passed, 0 failed, 69 executed`（返回码 0）**
（command 30 · rule 6 · probe 10 · isolation 23），按任务包 §4"只验证用户命令、合法参考策略、相同快照探针、
预算/故障与不触发训练"分四组，关键点都是**可判别**的：

* **用户命令**：根目录 `run.py diagnose` 返回 0、三类产物齐全、四段内容都在、参考对局是**胜利**且带逐步规则理由；
  缺产物与输出目录非空都返回 **2**；`--help` 在**屏蔽 NumPy** 时仍返回 0；
* **合法参考策略**：对**真实记录快照**逐局面断言"概率恰好覆盖当前合法候选、和为 1、one-hot、
  且选中项等于记录的优先级冠军"；候选顺序反转不改变选择（只看语义键与公开字段，不看位置/句柄）；
  同局面有两张攻击牌时选公开 `Amount` 最高者；
* **相同快照探针**：`episode_ids` 必须等于 `episodes/index.jsonl` 的最早 8 条；TV 落在 [0,1]；
  **用保留的逐候选概率独立重算 TV 必须等于记录的 TV**（不采信实现自报）；逐局面定位字段齐全；
  两个模型身份不同且都记录在案；
* **不触发训练（行为级，不只看文档）**：进程内跑一次缩短版诊断，**拦截 `Trainer.__init__` 与
  `save_bundle`**，两者调用次数必须都是 **0**；源码级正则断言诊断模块没有 `Trainer(`/`trainer`/
  `train_epoch`/`ppo_loss`/`models.learning`/`Adam(`；原 run **整目录逐字节摘要不变**、
  检查点仍是 8 个、候选权重摘要与 `ppo(steps=16)` 标签不变、输出目录里没有检查点或模型包；
* **预算与保留种子**：4 策略 × 8 局 = 32 局、确认动作与命令耗时都在限额内、单局不超每局上限；
  把命令时钟压到 0 ⇒ 退出码 1、**没有跑任何一局**、`exit_category=fault`；
  扫描 JSON 里所有以 seed 命名的字段，**没有任何 final 30000～30007 或未来保留 50000～50007 的取值**
  （它们只出现在 `source_run_scope` 的"记录在案但未访问"字段里）。

`diagnosis.json` 另含 `source_run_scope`：来源 run 的 Profile/配置摘要/Gym/场景、
train 10000～10031、validation 20000～20007、final 30000～30007 与未来保留 50000～50007
（后两者标注**未访问**）、执行器偏移与种子规则——满足任务包 §3"其他输入 run 须记录同样身份与范围"。

### 11.3 本次改动文件

| 文件 | 改动 |
| --- | --- |
| `ai-the-spire/ai_the_spire/runner/episode.py` | `EpisodeInterrupted`（携带部分结果）；`_interrupt` 两种模式都写出自描述的 `interrupted` |
| `ai-the-spire/ai_the_spire/runner/__init__.py` | 导出 `EpisodeInterrupted` |
| `ai-the-spire/ai_the_spire/delivery/episodes.py` | `EpisodeFault`（携带部分 `EpisodeRun`）；`run_episode` 支持 `max_steps` 并按剩余额度限流；`unknown_actions` 如实另记 |
| `ai-the-spire/ai_the_spire/delivery/budget.py` | **新增**：run 级资源账目（实际消耗 / 已提交状态分离、逐局限流、跨 resume 继承、旧 run 拒绝） |
| `ai-the-spire/ai_the_spire/delivery/train.py` | 接入账目；`_collect_episode` 在故障前落盘事实；提交点前移 `training_actions_used`；summary 增账目段 |
| `ai-the-spire/ai_the_spire/delivery/environment.py` | `make_runner` 支持只**收紧**每局上限 |
| `ai-the-spire/ai_the_spire/delivery/rule_policy.py` | **新增**：版本化公开信息规则基线（含完整优先级与理由渲染） |
| `ai-the-spire/ai_the_spire/delivery/diagnose.py` | **新增**：D2 诊断命令实现 |
| `ai-the-spire/ai_the_spire/delivery/console.py` | 抽出 `render_episode_body` / `confirmed_action_count` / `action_after_snapshot` / `turn_of`（demo 与 diagnose 共用） |
| `ai-the-spire/ai_the_spire/delivery/demo.py` | 改用共用的逐决策渲染（D1 可读对局的显示内容不变） |
| `ai-the-spire/ai_the_spire/evaluation/plan.py` | 中性方向 `descriptive`；策略种类增加 `rule_baseline`（基线状态一致） |
| `ai-the-spire/ai_the_spire/evaluation/runner.py` | 中性指标的配对只报差值、判定固定 `descriptive` |
| `ai-the-spire/ai_the_spire/delivery/evaluate.py` | 确认决策数改用 `descriptive` 并写明口径 |
| `ai-the-spire/run.py` / `delivery/commands.py` | 新增 `diagnose` 子命令（`--help` 仍不需要 NumPy/Host） |
| `ai-the-spire/tests/j01_episode.py` | author 版本断言对齐后端 v2（保留派生/身份断言） |
| `ai-the-spire/tests/d1_1_closure.py` | **新增**：D1.1 的 57 项可判别检查（受控故障与时钟注入） |
| `ai-the-spire/tests/d2_diagnose.py` | **新增**：D2 的 69 项验收（用户命令 / 合法参考策略 / 同快照探针 / 不触发训练与预算故障） |
| `ai-the-spire/README.md` | D1.1/D2 命令、账目与旧 run 的说明、D2 结论摘要、新的套件计数 |
| `docs/review/tofix/README.md` | D1.1/D2 的认领、进度与证据（只追加，不清空 W 项） |
| 本文件 §11 | D1.1 / D2 两个产品检查点 |

**未改动**：`core-the-spire/**`、`mod-the-spire/**`、`net-the-spire/**`（R1 与 net 均只读复核）、
`docs/contracts/**`、`tools/**`、`sts2/**`、`reference/**`、真实游戏。原 D1 产物 `artifacts/d1/**` 未被覆盖。

### 11.4 实际执行的检查与返回码（本次实跑，单线程顺序执行）

| 命令 | 返回码 | 实际范围 |
| --- | --- | --- |
| `dotnet build AutoTheSpire.sln --no-restore` | 0 | 0 警告 0 错误 |
| `python ai-the-spire/tests/d1_1_closure.py` | 0 | `TOTAL: 57 passed, 0 failed, 57 executed`（fault 20 · budget 26 · metric 7 · legacy 4） |
| `python ai-the-spire/tests/d1_delivery.py` | 0 | `TOTAL: 192 passed, 0 failed, 192 executed`（D1 原验收，未回归） |
| `python ai-the-spire/tests/j01_episode.py` | 0 | `TOTAL: 219 passed, 0 failed, 219 executed`（D1.1-T 修复后） |
| `python ai-the-spire/tests/d2_diagnose.py` | 0 | `TOTAL: 69 passed, 0 failed, 69 executed`（command 30 · rule 6 · probe 10 · isolation 23，含行为级"不触发训练"） |
| `python ai-the-spire/tests/run_checks.py` | 0 | `TOTAL: 604 passed, 0 failed, 604 executed` |
| `python ai-the-spire/run.py diagnose --run artifacts/d1/train --output artifacts/d2/diagnose` | 0 | 四策略对照 + 参考对局 + 199 个同局面探针 + 学习信号 + 一个实验建议 |
| `git diff --check` | 0 | 干净（只有既有文件的 LF/CRLF 提示） |

**未修改 core/base/net**，因此按接续任务 §4 复用第 10 节负责人本轮已验收的
Slice 244/244、Legacy 190/190、net 393/393、Engine 64、client 50、契约 210、依赖 9/9 证据，不重复跑整套内容回归。
R1 已关闭（[归档依据](../../archived/d1-r1-closure-2026-10-04.md)），本次未触碰。

### 11.5 未完成项与残余边界

1. **没有学会获胜（如实报告）**：D2 诊断确认 32 局全败、三策略平均未折扣回报都是 −1.8；
   规则基线在这些种子上 8/8 获胜。**这不构成学习改善，也不构成 PPO 实现有错的结论**。
2. **"表示遗漏"类假设本轮未测**：没有做"同一策略在成对可区分局面上的编码差异"检查，
   也没有做"关掉裁剪/改 lr 是否改变信号"的对照；这两项在 `diagnosis.json` 里显式标为未测。
3. **诊断种子不是未见数据**：40000～40007 已被负责人用于探查，本报告**不称最终保留集**；
   final 30000～30007 与未来保留 50000～50007 未被访问。
4. **旧 D1 产物不能再 resume**：`artifacts/d1/**` 没有累计账目，`resume` 按设计返回 2；
   查看/评估/诊断不受影响。这是有意的行为变更，不是缺陷。
5. **规则基线是朴素规则**：不评估效果、不做跨回合规划；它获胜只说明受控场景存在可获胜路径，
   **不**证明规则最优、**不**证明模型能在 32 局内学会。规则概率是 one-hot，**不是** Q 值或通关概率。
6. **D2 只覆盖默认单战斗 Gym**：仍是切片范围，不声称完整 STS 环境、完整整局或多场景泛化。
7. **未运行**：`tools/collect/collect.py`、真实游戏与多人场景；未提交/清理 Git，未创建 agent。
   `tools/doc-check/check_docs.py` **已跑**（172 份 Markdown、0 问题），用于核对本文件新增的相对链接。
