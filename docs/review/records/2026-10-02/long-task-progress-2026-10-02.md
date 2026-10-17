# 长任务进度与证据索引（2026-10-02）

> 历史证据（2026-10-03整理）：正文保留原时点结果，历史“下一步”不再作为当前派单指令。本报告归属W-01、W-02、W-03、W-04、W-05、W-06、W-07、W-08，未结项转入[当前清单](../../tofix/README.md)；移入records不表示修复或验收通过。逐份归属见[记录索引](../README.md)。

2026-10-03负责人评审：本表保留执行时点与实跑记录，其中阶段汇总和“下一项”存在过期项，不能直接派单。当前完成度/顺序以[清单v0.3第19节](../../../design/implementation-kickoff.md#19-2026-10-03整体完成度与当前任务清单)及[整体评审](../2026-10-03/project-readiness-review-2026-10-03.md)为准；新执行者按W-01至W-08维护后续证据，不照抄第一批的未完成列表。

| 项目 | 内容 |
| --- | --- |
| 主入口 | [开工清单 v0.2 第12至17节](../../../design/implementation-kickoff.md#12-h-00长任务起点先关闭-m1-缺口)；[第18节可复制指令](../../../design/implementation-kickoff.md#18-可直接给小模型的长任务指令) |
| 本轮范围 | 第12节 H-00（复核）；随后 H-01/H-02/H-03 → N-05 → J-01/M1 |
| 工作区 | 分支同工作区；存在他人未提交改动（Legacy 路径迁移删除、`AutoTheSpire.sln`、`README.md`、`CombatSliceTests.cs`、内容源）。本轮只读它们，不 reset/清理/提交 |
| 冻结 Profile | `base-spire:combat-slice-v1`（`mod-the-spire/base-spire/BaseSpire/Slice/`）；net 侧 `SlicePublicProfile` |
| 证据边界 | 下文数字均为本轮实跑（执行者本人），不采信旧报告自述 |

## 1. H-00 复核：本轮实跑结果（2026-10-02）

从仓库根目录执行，逐项保存返回码与实际执行数：

| 命令 | 返回码 | 实际执行数 / 结果 |
| --- | --- | --- |
| `dotnet build AutoTheSpire.sln` | 0 | 0 错误 0 警告；10 个项目全部生成 |
| `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` | 0 | `All 50 engine checks passed.` |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | 0 | `All 25 slice checks passed.` |
| `dotnet run --project core-the-spire/Core.TheSpire.Tests` | 0 | `190 passed / 0 failed / 190 executed`（旧后端冻结对照） |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | 0 | `TOTAL: 362 executed, 362 passed, 0 failed` |
| `python tools/contract-check/check_contracts.py` | 0 | `TOTAL: 210 passed, 0 failed, 210 executed` |
| `python ai-the-spire/tests/run_checks.py` | 0 | `TOTAL: 503 passed, 0 failed, 503 executed`（10 组） |
| `python net-the-spire/client/test_client.py` | 0 | wire 26 / transport 7 / recovery 3 = `36 passed`（真实 stdio 宿主进程） |
| `python tools/doc-check/check_docs.py .` | 0 | 95 份 Markdown，0 problem |
| `python tools/doc-check/check_trace.py .` | 0 | 20 条需求行，0 不一致 |
| `git diff --check` | 0 | 仅 LF/CRLF 提示，无空白错误 |

**首个失败**：无。以上全部返回码 0。

## 2. 任务状态表（H-00 产出）

| 任务 | 状态 | 本轮证据 | 备注 |
| --- | --- | --- | --- |
| K-00～K-07 | 实现已验证（认领） | H-00 时 Engine 50/50；H-01 后 63/63（本报告 §1） | 不重写 |
| B-01～B-04 | 实现已验证（认领） | 本轮切片 25/25 | 不重写 |
| P-01 | 实现已验证（认领） | 本轮契约 210/210 | 报告 §7 的 15 条"无强制手段"不变量仍开放（归规划者裁定） |
| A-01～A-05 | 实现已验证（认领） | 本轮 AI 503/503 | 不重写 |
| N-01～N-04 | 实现已验证（认领） | H-00 时 Net 362/362 + client 36/36；H-03 后 369/369 + 50/50 | 不重写 |
| **H-00** | **已完成** | 本文件 §1 的 11 条命令 | — |
| **H-01 当前已知成员投影** | **已完成（实现已验证）** | Engine 63/63（+13）、切片 25/25、net 365/365（+3）；报告 [h-01-h-02](h-01-h-02-2026-10-02.md) | 已知位置冲突报告**保留 tofix**：尚缺 J-01 的实际 stdio 断言 |
| **H-02 目录与覆盖表** | **已完成（实现已验证）** | `pile_types` 5 条且 kind 唯一、`target_kind_ids`/required 来自显式声明；覆盖表 [coverage-n-02.md](../../../../net-the-spire/coverage-n-02.md) 已按当前实现重写 | 旧 TODO（PILE-ENTITY/KNOWN-POSITION/CATALOG-PILES）已真正关闭 |
| **H-03 请求与故障证据** | **已完成（实现已验证）** | Net 369/369（+4）、client 50/50（+12 `faults` 组）；报告 [h-03](h-03-2026-10-02.md) | 延迟/断开/重启为**故障注入**，脚本化后端只为 `pending`/`outcome_unknown`；同步后端不伪造它们 |
| N-05 接口验收 | **已完成（实现已验证）** | Net 381/381（+12 条 V 检查）、切片 25/25；报告 [n-05](n-05-2026-10-02.md) + 矩阵 [acceptance-n-05.md](../../../../net-the-spire/acceptance-n-05.md) | 逐场景标注**真实/夹具/故障注入/推迟**；并发与异步在飞超时未覆盖（已声明） |
| **J-01 / M1** | **已完成（实现已验证）** | `python ai-the-spire/tests/j01_episode.py` → **150 passed**（episode 121 / headbutt 15 / invariants 7 / trace 7）；报告 [j-01-m1](j-01-m1-2026-10-02.md) | **M1 达成**：同一受控切片经真实 stdio 被 Python 客户端运行、编码、给概率、采样、执行并落盘可离线重编码的公开轨迹 |
| **D-01 轨迹格式及数据索引** | **已完成（实现已验证）** | `python ai-the-spire/tests/d01_records.py` → **98 passed**；报告 [d-01](d-01-2026-10-02.md) | `episode-record@1` + `EpisodeStore`（派生索引、独立重读/验摘要/重编码）；重复 request 与未补造未知结果均有拒绝用例 |
| **D-02 有界 Collector** | **已完成（实现已验证）** | `python ai-the-spire/tests/d02_collector.py` → **45 passed**；报告 [d-02](d-02-2026-10-02.md) | 显式限额（局数/步数/时长/磁盘/在飞）各自有原因；种子派生确定；worker 隔离；中断局不混入终止；重复重放得同一批记录 |
| **D-03 数据审计与划分** | **已完成（实现已验证）** | `python ai-the-spire/tests/d03_audit.py` → **89 passed**；报告 [d-03](d-03-2026-10-02.md) | 划分按**场景簇**（同簇不跨 split）；奖励字段被拒绝；完整性/身份混用/计数/偏移分别报告；**当前划分退化为已如实报告** |
| **P-02 模块与端到端测量** | **已完成（实现已验证）** | Net **383/383**（+2 项进程内测量）、`python ai-the-spire/tests/p02_measure.py` → **32 passed**；报告 [p-02](p-02-2026-10-02.md) | 三段进程内 + AI 三段 + wire 每请求分开测；四档负载可重复；诊断纯度有反例；**只报告不写达标** |
| **D-04 长跑检查与入口** | **已完成（实现已验证）** | `tools/collect/collect.py`（跨模块入口）+ `python ai-the-spire/tests/d04_longrun.py` → **50 passed**；报告 [d-04](d-04-2026-10-02.md) | 一条命令完成采样+审计+汇报；`resume_from_index` 与 `next_episode_index` 区分；中断局从头新开、不恢复游戏中途状态；失败分类互不混淆 |
| **§13 阶段一（D/P 设施）** | **全部完成** | D-01 100 · D-02 45 · D-03 89 · P-02 32 · D-04 50 | 后续按第14～17节顺序推进 |
| **B-05a / B-05b** | **已完成** | 清单 354/278/85；切片 29/29 | B-05c 起逐批扩面 |
| **B-05a 内容与原语清单** | **已完成（机器产物已冻结）** | `dotnet run --project tools/content-manifest -- tools/content-manifest/manifest-2026-10-02.json`；报告 [b-05a](b-05a-content-manifest-2026-10-02.md) | 注册 **354** / 源文件声明 **278** / Ironclad 池 **85** 三条分母分别列出；不可达声明 0；无 `.spire` 来源的 76 条恰为 C# 声明类别之和 |
| **B-05b 切片前端** | **已完成（实现已验证）** | 切片检查 **29/29**（+4 项 B-05b）；报告 [b-05b](b-05b-slice-frontend-2026-10-02.md) | 作者文本 → 声明模型 → 通用 IR → 链接 → 加载 → 基础/升级行为；10 类非法声明带路径拒绝；Headbutt 续接与已知位置不回归 |
| B-05c 完整前端覆盖 | **部分完成（第一批）** | 切片 **33/33**（+4 项 B-05c）；报告 [b-05c 批1](b-05c-frontend-batch1-2026-10-02.md) | 已交付：效果完全由步骤驱动 + 目标域 `damage_all`（语法/编译/运行/检查四态齐备）。**未完成**：X 费、hooks、选择/批处理、实例变量、生成/升级/变形、Power/敌人**行动程序**、药水/run、0.0.1 兼容入口、随机候选顺序来源 |
| **K-09a 依赖抽离** | **第一步 + 清点完成** | `check_dependencies.py` **9/9**、`legacy_inventory.py` 分类完整；报告 [依赖图](k-09a-dependency-map-2026-10-02.md) / [清点](k-09a-legacy-inventory-2026-10-02.md) | 56 项目 0 环；**39 个新路径文件（Engine+切片+net+ai）类型位置引用 Legacy = 0**（检查器自检含 4 类误报回归）；旧后端 26 文件/12138 行/363 类型分类完整（未分类 0）。**结论：切片所需领域职责已迁完**。未完成：实体文件迁移与 K-09b |
| **B-06a 基础内容批** | **十六小批完成** | 切片 **186/186**、net **383/383**、J-01 **150/150**、client **50/50**、依赖检查 **9/9**；报告见 [状态牌](b-06a-status-cards-2026-10-02.md)…[缩放数值](b-06a-scaled-amount-2026-10-02.md) / [费用修正](b-06a-cost-modifier-2026-10-02.md) | 已迁并可运行 **22/94**（**口径已修正**：分母 = 注册 96 − 2 张多人专属；见 [覆盖核算](b-06a-coverage-2026-10-02.md)）；能力新增：`modify_cost:<n>`（临时费用修正，有效费用 `max(0, Cost+修正)`，四牌区回合末过期）、`played-count:<Kind>`（**已完成**的已打出计数）；net 公开映射新增 `CostModifier` 字段处置。**未声明 `Stomp`**：还缺 hook 上的 `when-card-type` 过滤与克隆语义（不夸大覆盖）。**未完成**：卡牌标签、交互式抽牌、其他触发点/谓词、BodySlam（来源缺失）、其余 72 张（分母内）、B-06b/c |
et:cat:Status。**未完成**：其余基础牌、loseHp+Beckon、典型多敌、费用修正、胜负显式场景、B-06b/c |
| **B-07 整局前置换算** | **清单已冻结（B-07 前置）** | [run-flow-manifest-2026-10-02.md](../../../design/mod-the-spire/run-flow-manifest-2026-10-02.md) | 逐机制列出整局分母与来源：**新路径除药水 4/6、敌人 2/89 外全部未实现**；**事件/古代人注册数 0（内容缺口）**；给出 B-07a~d 与本表的对应与缺口清单 |
| 第15节 B-07 / N-06 / J-02 / J-03 | 未开始 | — | 单人整局流程与公开规范 |
| **B-07a 整局状态与写回** | **第一小批（部分完成）** | 切片 **186/186**、net **383/383**；报告 [b-07a](b-07a-run-state-2026-10-02.md) | 已交付：`run { hp/max-hp/gold }` 声明（未知键显式拒绝）、`Run` 实体（含 `BattleSettled` 幂等守卫）、`SettleBattle` 过程；程序化路径不建 Run 实体（公开投影不回归）。**未接通**：写回。第二小批用**三个实验**把症状收窄到"创建/可见性"：① 胜利路径接线复现；② **在 setup 末尾紧接着创建后调用也报同样错**（⇒ 与调用上下文无关）；③ 换成集合查法仍取不到。报告 §3b 记了实验表、一个**被放弃**的实验（重复调 `Body` 导致 `DuplicateDeclaration`）与下一轮的第一个实验设计。 |
| 第16节 L-00 / L-01a~L-05a | 未开始 | — | 低成本模型与学习工程（16.2 目标/预算未定前不做真实实验） |
| K-08 Lua | 未开始 | — | 独立批次；只阻塞需要 Compute 的机制 |

### 2.1 未挂入入口的检查 / 临时关闭的检查组 / 仅 TODO 的功能

- **无临时关闭的检查组**。`ai-the-spire/tests/run_checks.py` 的 10 组全部执行；A-04 曾被我停挂的 `tensorize`/`collate` 两组已挂回并在本轮 503 项中执行。新增入口 `ai-the-spire/tests/j01_episode.py` 已记入开工清单 §10 与 AI README。
- **本轮关闭的具名缺口**：`TODO-N02-PILE-ENTITY`、`TODO-N02-KNOWN-POSITION`、`TODO-N03-CATALOG-PILES`（检查由"TODO 存在"改为"TODO 不存在且行为成立"）。
- **H-03 关闭的缺口**：① 会话句柄跨进程复用；② 客户端不按 `request_id` 归属响应；③ 帧拒绝响应被静默丢弃导致客户端永久挂起；④ `dotnet run` 包装进程导致的孤儿宿主。
- **N-05 关闭的缺口**：① 撤销的已知事实仍是活实例（I-29 失败）；② 目录 `entity_kinds[].id`/`applies_to` 用了引擎内部 ID；③ 目录 wire 数组按注册顺序输出。
- **J-01 关闭的缺口**：① 目录缺少 I-79 的三个保留声明（C# 自检只查内部清单，共享 L2 在真实目录上抓到）；② `category_ids` 用引擎原值而非公开类别 ID；③ net 自声明的应答动作不在目录里（引用闭合破裂）；④ 应答参数目标种类声明过窄（决策域含敌人）；⑤ 宿主参数解析漏掉最后一个参数（末尾开关被静默忽略）。
- **D-01 关闭的缺口**：记录摘要把按进程随机的 `session_id` 计入了身份 → 同一配置两次重放摘要不同；修复为**摘要输入归一化控制元数据**（存储仍保留完整快照）。
- **D-02 关闭的缺口**：① `step` 之后的**可选对账查询**失败会逃出 `run()`，被误归成 `error` 而不是 `interrupted`；② 原先无法表达"中断"（只会抛异常），Collector 拿不到已确认的转移 → 新增 `raise_on_failure=False` 与 `unknown` 转移，补上了 D-01 报告里"`unknown`/`interrupted` 没有真实来源"的缺口。
- **D-03 关闭的缺口**：① 审计只信索引 → "磁盘上有记录但没进索引"审计不到（改为审计记录目录、索引只用于报告漂移）；② 奖励扫描把协议结构键 `field.value` 当成奖励标签（每条正常记录都被误报）；③ 不同场景簇共用种子流（同种子不同配置）；④ 缺 `scenario_variant` 时静默合并成同一簇（划分看似无泄漏、簇内混配置）。
- **D-04 关闭的缺口**：① 中断后复用已死的 worker 连接（下一局在死管道上失败并被归成 `error`）→ 中断后替换连接；② 准备阶段（describe/get_catalog）的传输失败被兜底成 `implementation_error` → 先按 `interruption_errors` 判为 `host_lost`；③ 配置文件带 BOM 时被判"配置非法"（Windows 编辑器默认行为）→ 按 `utf-8-sig` 读并加回归检查。
- **B-05a 关闭的缺口（清单工具自身的记账 bug）**：状态变量类别在生成清单时没有进入 `registered` 集合，导致 4 条状态变量被误报成"声明但未注册"、注册总数少算 4（350→**354**）。修复后不可达声明为 **0**，且 76 条"无 `.spire` 来源"的注册项恰为 C# 声明的四类之和（63+11+1+1）。
- **B-05b 关闭的缺口**：① 作者前端的裸名字（`strike-ironclad`）没有限定到包命名空间，导致 `SliceManifest.Card` 查不到（改为按 `package { id }` 限定，带命名空间的 id 原样保留）；② 我此前 N-05 代码留下的 3 条 CS8604 可空警告（`TargetKindIds`/`CategoryIds` 可空）——顺手修掉，构建回到 0 警告。
- **D-03 结论**：当前冻结场景集只有 2 个簇（`baseline` 与测试场景），`final_eval` 为空 → **划分退化，已如实报告**；在补足场景簇之前这批数据不得当作训练/评估划分使用。记录里**没有任何奖励字段**（审计会拒绝）。
- **仍以显式跳过登记的集合**（能力边界，**不作为 V-15 成立**）：`PlayedThisTurn`（无公开有序流程语义）、`Monsters`（非牌堆、无 group owner 实体）、`Known`（事实已逐个作为实体发布）、`KnownReveal`（授权边，公开表达为 `known_positions`）。
- Engine 侧：无 `Sequence`/`Group` 声明入口，`Group` 无 `multiplicity` → 属能力边界。
- **待裁定项**：公开实例 ID 仍直接使用内部创建计数器（`design.md:61`）。切片下影响有限，一般情形存在风险；需"只改隐藏创建顺序"的成对场景与一个新的公开映射，未擅自改动。
- **同步后端的明确边界**：`request_status` 只回答 `completed`/`not_found`；`pending`/`outcome_unknown` 在同步实现下**不可达**；并发 `session_busy` 同理，后端声明单写者。
- **测试场景开关**：宿主新增 `--test-headbutt`（默认关闭，非原版起始卡组），仅用于在真实链路上驱动头槌的中途选择与置位；用它产生的证据已标注为测试场景。
- **P-01 审计遗留**：15 条规范不变量当前无强制手段（`docs/contracts/net/0.1/invariants-disposition.json` 的 `missing` 条）；其中 6 条属 N-03 规格且 N-03 已实现检查。

## 3. 下一项与未定字段

- B-06a（覆盖核算）：[b-06a-coverage-2026-10-02.md](b-06a-coverage-2026-10-02.md)（分母/已迁/剩余的机械核算 + 四态证据汇总；**修正分母口径**）。
- **下一项**：**B-07a 第二小批：接通战斗结束的写回**（先查清"跨过程按类型查作用域成员"取不到实例的原因，再做最小复现；接通后补三件检查：写回一次/重复调用空操作、临时牌不进长期牌组、相同内容实例分别升级）；B-06a 剩余能力批次与 §15 可并行推进。原计划项：卡牌 hook 上的 `when-card-type` 过滤 + 克隆/复制语义（两件都是声明 `Stomp` 的前置；先做过滤，再做克隆，取不到克隆语义就把 `Stomp` 记成明确未完成项）。**待裁定**：`cardsWithTag` 牌区范围、BodySlam powered、药水栏快照、`FlexPotion` 触发点。B-05a 的 **354 条声明 / 278 条源声明 / 85 张卡池卡**仍是分母：B-05b 带进 4 张卡，B-05c 批 1 只加了一个目标域原语，其余子项逐条列在批 1 报告 §4。
- **§13 阶段一（D-01/D-02/D-03/D-04/P-02）与 B-05a/B-05b 已完成**。随机基线只证明管道，**不证明学习效果**；测量只报告数字，**不写达标**；清单只证明"已注册"，**不证明行为已验证**；B-05b 只覆盖 4 张卡，其余 92 张仍在 B-05c 范围内。
- **未定字段（不阻塞当前工程）**：L-00b 的真实学习实验目标/奖励向量与单位/偏好/时间尺度/预算/种子拆分（第16.2节）。未填写前只做 16.1 工程与配置模板。
- **新增未裁定（需规划者/用户）**：④ **药水栏是否进公开快照**（`net` 公开词典/目录是**已承诺的契约**，加一个 `PotionBelt` 牌堆会改变默认目录形状；建议做成"仅当内容声明了药水时才投影"的可选形状，但这是契约决定）；⑤ **`FlexPotion` 需要的引擎触发点**（临时能力的 `on-stack-change`/`on-expire`）——属"缺能力先补 Engine"，但它的**语义**（过期时是否撤回已给出的力量）需按来源确认后再定。- **未裁定（需规划者/用户）**：① P-01 的 15 条 `missing` 不变量如何处理（补检查 or 明确只作规范条文）；② U-11 的 `error.path` 语法无文档规定（当前实现采用与共享 L3 一致的约定并已声明）；③ **公开实例 ID 的创建顺序映射**（`design.md:61`；见 §2.1）。

## 4. 单项证据索引

- H-00：本文件 §1–§2。
- H-01 / H-02：[h-01-h-02-2026-10-02.md](h-01-h-02-2026-10-02.md)；覆盖表 [coverage-n-02.md](../../../../net-the-spire/coverage-n-02.md)。
- H-03：[h-03-2026-10-02.md](h-03-2026-10-02.md)。
- N-05：[n-05-2026-10-02.md](n-05-2026-10-02.md)；验收矩阵 [acceptance-n-05.md](../../../../net-the-spire/acceptance-n-05.md)。
- J-01 / M1：[j-01-m1-2026-10-02.md](j-01-m1-2026-10-02.md)；入口 `python ai-the-spire/tests/j01_episode.py`。
- D-01：[d-01-2026-10-02.md](d-01-2026-10-02.md)；入口 `python ai-the-spire/tests/d01_records.py`。
- D-02：[d-02-2026-10-02.md](d-02-2026-10-02.md)；入口 `python ai-the-spire/tests/d02_collector.py`。
- D-03：[d-03-2026-10-02.md](d-03-2026-10-02.md)；入口 `python ai-the-spire/tests/d03_audit.py`。
- P-02：[p-02-2026-10-02.md](p-02-2026-10-02.md)；入口 `python ai-the-spire/tests/p02_measure.py` + `Net.TheSpire.Tests` 的两项进程内测量。
- D-04：[d-04-2026-10-02.md](d-04-2026-10-02.md)；入口 `python tools/collect/collect.py`、`python ai-the-spire/tests/d04_longrun.py`。
- B-05a：[b-05a-content-manifest-2026-10-02.md](b-05a-content-manifest-2026-10-02.md)；产物 	ools/content-manifest/manifest-2026-10-02.json。
- B-05b：[b-05b-slice-frontend-2026-10-02.md](b-05b-slice-frontend-2026-10-02.md)；作者文本 mod-the-spire/base-spire/BaseSpire/Authoring/Examples/slice-content.author。
- 历史（本轮只读、不重复实现）：[监督台账](../2026-10-01/supervision-ledger-2026-10-01.md) §3 逐任务表；各任务报告 `r-*`/`a-*`/`n-*`/`p01-*` 同目录。
