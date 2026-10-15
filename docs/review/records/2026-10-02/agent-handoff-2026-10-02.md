# 长任务交接与审计文档（2026-10-02）

> 历史证据（2026-10-03整理）：正文保留原时点结果，历史“下一步”不再作为当前派单指令。本报告归属W-01、W-02、W-03、W-04、W-05、W-06、W-07、W-08，未结项转入[当前清单](../../tofix/README.md)；移入records不表示修复或验收通过。逐份归属见[记录索引](../README.md)。

| 项目 | 内容 |
| --- | --- |
| 文档目的 | 供**其他 agent 审计**本会话的长任务进展：逐条列出**已完成**、**部分完成（缺口）**、**未开始**、**已知缺陷**与**待裁定项**，每条都指到可复核的证据（文件 / 命令 / 数字） |
| 清单来源 | [`docs/design/implementation-kickoff.md`](../../../design/implementation-kickoff.md)（§12～§16 + §17 验收协议） |
| 进度索引 | [`long-task-progress-2026-10-02.md`](long-task-progress-2026-10-02.md)（本会话维护的状态表与证据索引） |
| 会话范围声明 | 本会话按 §17.1 的固定范围推进；**未**承诺其他角色/多人/OriMod/任意 mod 的零样本泛化，也**未**操作真实游戏或发布 |
| 工作区状态 | 未提交、未清理共享 Git；`git diff --check` 干净 |

---

## 0. 一句话状态

§12（H-00～H-03 / N-05 / J-01）、§13（D-01～D-04 / P-02）**已完成并有证据**；§14 的 **B-05a / B-05b 完成**，
**B-05c 覆盖大部分语言面（16+ 小批）**，**B-06a 迁入 22/94 张**，**K-09a 第一步与清点完成**，
**B-07 的整局冻结清单完成**、**B-07a 第一小批部分完成（胜利写回成立、失败写回未成立，缺陷已记录）**；
**B-06b / B-06c / K-09b / B-07b~d / N-06 / J-02 / J-03 / §16 L-* / K-08 未开始**。

## 1. 当前可复现的验证基线（本会话最后一次全量实跑）

| 命令 | 返回码 | 结果 |
| --- | --- | --- |
| `dotnet build AutoTheSpire.sln` | 0 | **0 错误 0 警告** |
| `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` | 0 | **64/64** |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | 0 | **190/190**（切片：25 既有 + 4 B-05b + 87 B-05c + 74 B-06a/B-07a） |
| `dotnet run --project core-the-spire/Core.TheSpire.Tests` | 0 | **190/190**（旧后端，未回归） |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | 0 | **383/383** |
| `python ai-the-spire/tests/run_checks.py` | 0 | **503/503** |
| `python ai-the-spire/tests/j01_episode.py` | 0 | **150/150**（M1） |
| `python net-the-spire/client/test_client.py` | 0 | **50/50** |
| `python tools/contract-check/check_contracts.py` | 0 | **210/210** |
| `python tools/coverage-check/check_slice_coverage.py` | 0 | 分母 **94** / 已迁 **22** / 剩余 **72**，三源不变量全过 |
| `python tools/dependency-check/check_dependencies.py` | 0 | **9/9**（含遮蔽检查；新路径引用 Legacy = 0） |
| `python tools/dependency-check/legacy_inventory.py` | 0 | 旧后端 26 文件 / 12138 行 / 363 类型，分类完整（未分类 0） |
| `python tools/doc-check/check_docs.py .` | 0 | 0 problems |
| `python tools/doc-check/check_trace.py .` | 0 | 0 mismatching requirement rows |

> 复核提示：这些数字**都可由上面命令当场复算**；若某条对不上，说明报告或代码有漂移，请按 §7 的核对清单追。

## 2. 已完成项（逐条：交付物 → 证据 → 复核方式）

| 清单项 | 交付物 | 证据（实跑数字） | 复核方式 |
| --- | --- | --- | --- |
| **H-00 开工清单与命令** | §10 的 11 条命令入口（含本会话新增 3 条：`ai-the-spire/tests/j01_episode.py`、`tools/dependency-check/*`、`tools/coverage-check/*`） | 全部命令返回 0 | 按 §10 逐条跑 |
| **H-01 当前已知成员投影** | 已知位置/成员投影修正 + 已知位置冲突报告 | Engine 64/64、切片 25/25（当时）、net 383/383 | 报告 [h-01-h-02](h-01-h-02-2026-10-02.md) |
| **H-02 目录与覆盖表** | `pile_types` 5 条且 kind 唯一；`target_kind_ids`/required 来自显式声明；覆盖表重写 | net 383/383；三个具名 TODO 已真正关闭 | [net-the-spire/coverage-n-02.md](../../../../net-the-spire/coverage-n-02.md) |
| **H-03 请求与故障证据** | 延迟/断开/重启的**故障注入**检查 | net 383/383（含 12 条 `faults` 组）、client 50/50 | [h-03](h-03-2026-10-02.md)；注意：脚本化后端只产生 `pending`/`outcome_unknown`，同步后端不伪造它们 |
| **N-05 接口验收** | 逐场景验收矩阵 + 四态标注（真实/夹具/故障注入/推迟） | net 381→383、切片 25、并发与异步在飞超时未覆盖（**已声明**） | [n-05](n-05-2026-10-02.md) + [acceptance-n-05.md](../../../../net-the-spire/acceptance-n-05.md) |
| **J-01 / M1** | 同一受控切片经**真实 stdio** 被 Python 客户端运行、编码、给概率、采样、执行并落盘可离线重编码 | **150 passed** | [j-01-m1](j-01-m1-2026-10-02.md) |
| **D-01 轨迹格式与索引** | `episode-record@1` + `EpisodeStore`（派生索引、独立重读、摘要对重编码稳定） | 100 | [d-01](d-01-2026-10-02.md) |
| **D-02 有界 Collector** | 显式限额（局数/步数/时长/磁盘/在飞）+ 种子派生确定 + worker 隔离 | 45 | [d-02](d-02-2026-10-02.md) |
| **D-03 数据审计与划分** | 按**场景簇**划分（同簇不跨 split）；奖励字段被拒 | 89（结论：当前冻结场景集只有 2 簇，**划分退化、已如实报告**） | [d-03](d-03-2026-10-02.md) |
| **P-02 模块与端到端测量** | 三段进程内 + AI 三段 + wire 每请求分开测；四档负载可重复 | net 383、`p02_measure.py` 32 | [p-02](p-02-2026-10-02.md) |
| **D-04 长跑检查与入口** | `tools/collect/collect.py` 一条命令完成采样+审计+汇报；中断局从头新开、不恢复游戏中途状态 | 50 | [d-04](d-04-2026-10-02.md) |
| **B-05a 内容与原语清单** | 冻结迁移 manifest（机器产物） | 注册 **354** / 源声明 **278** / Ironclad 卡池 **85**；不可达声明 0；无 `.spire` 来源的 76 条恰为 C# 声明四类之和 | [b-05a](b-05a-content-manifest-2026-10-02.md) + [manifest json](../../../../tools/content-manifest/manifest-2026-10-02.json) |
| **B-05b 切片前端** | 作者文本 → 声明模型 → 通用 IR → 链接 → 加载 → 基础/升级行为 | 30 类非法声明带路径拒绝；Headbutt 续接与已知位置不回归 | [b-05b](b-05b-slice-frontend-2026-10-02.md) |
| **B-05c 完整前端覆盖（大部分）** | 16+ 批：目标域/X 费/hooks/选择/批处理/实例变量/生成·升级·变形/Power·敌人·药水 | 逐批报告（见 §6 索引）；切片总检查 190 | 见 §6 的 `b-05c-*` 报告 |
| **K-09a 第一步 + 清点** | 依赖图 + 边界检查（含检查器自检与遮蔽检查）+ 旧后端领域面清点 | 56 项目 0 环；**39 个新路径文件引用 Legacy 类型 = 0**；旧后端 26/12138/363 分类完整 | [依赖图](k-09a-dependency-map-2026-10-02.md) / [清点](k-09a-legacy-inventory-2026-10-02.md) |
| **B-06a 基础内容批（16 小批）** | 22/94 张可运行 + 一批通用能力（见下） | `coverage-check` 复算：分母 94、已迁 22、剩余 72 | [覆盖核算](b-06a-coverage-2026-10-02.md)（含**四态证据表**与分母口径修正） |
| **B-07 整局前置换算** | 整局冻结清单（B-07 的**前置**，§15 明确要求） | 逐机制列出：新路径除药水 4/6、敌人 2/89 外**全部未实现**；**事件/古代人注册数 0（内容缺口）** | [run-flow-manifest](../../../design/mod-the-spire/run-flow-manifest-2026-10-02.md) |

### 2.1 B-06a 已交付的能力（每项都有语法/编译/运行/检查四态证据）

`unplayable`/`ethereal`/`kind = Status`；`lose_hp:<n>`；`draw:<n>`；`apply_power_all`；
`grow-amount`；变体 `cost`；`block-count`；`apply_power_self`；声明式能力触发点
（`trigger` / `when-card-type` / `trigger-effect`，含 `after_card_exhausted`、`after_card_played`）；
`owner-turn-end = remove`；多能力同牌（逐步骤能力 id + 层数）；跨实体来源
（`apply_power_self_from_target:<id>`）；`gain_energy` + `prevent-energy-gain` + `hand-count:<Kind>`；
条件效果 `if { condition then else }`（谓词 `player-lost-hp-this-turn`）；缩放数值
（`amount-base` / `amount-per-count` + `exhaust-count`）；临时费用修正
（`modify_cost:<n>`）与 `played-count:<Kind>`。
**同时修掉两个潜伏缺口**：① 旧实现里"非 Ritual 的能力从不按回合衰减"；② **玩家侧能力的回合末行为此前从不执行**。

## 3. 部分完成项（缺口逐条）

| 清单项 | 已完成 | **未完成/缺口** |
| --- | --- | --- |
| **B-05c** | 语言面大部分（目标域、X 费、hooks、选择、批处理、实例变量、生成/升级/变形、Power/敌人/药水、0.0.1 兼容入口） | ① 自我施加能力只做"语义已实现"的那些（其余约 21 张需要新触发点）；② 交互式抽牌（`Pillage`/`BurningPact`）；③ **卡牌 hook 上的 `when-card-type` 过滤**与**克隆/复制语义**（`Stomp` 的两件前置，故 `Stomp` 未声明）；④ 其他触发点（受伤时、抽牌时）与其他条件谓词 |
| **B-06a** | 22/94 张 + 上表能力；覆盖核算可复算 | **剩余 72 张（分母内）**，多数依赖上面 ①②③④；另有 3 条**来源缺口**见 §5 |
| **K-09a** | 依赖图、边界检查、旧后端清点；结论"切片所需领域职责已迁完" | **实体文件/引用/测试与工具的迁移**未开始（清单要求"再迁文件/引用/测试与工具"） |
| **B-07a** | `run { hp/max-hp/gold }` 声明（未知键显式拒绝）；`Run` 实体（含 `BattleSettled` 幂等守卫）**在战斗开始前创建**；`SettleBattle` 过程；**胜利写回成立**（检查通过） | ① **失败（`lose_hp` 路径）写回未成立**（已知缺陷，见 §5）；② 长期牌组归属、战斗克隆/生成牌与长期牌组的边界、升级/移除/变形、金币/药水/遗物的**跨房间写回**；③ 三条验收未做：写回恰好一次/重复调用空操作、**临时牌不误写长期牌组**、**相同内容实例分别升级/移除** |

## 4. 未开始项

| 清单项 | 内容 | 前置 |
| --- | --- | --- |
| **B-06b** | 自动打牌、replay、X费、嵌套选择、生成/变形、耗尽回调、敌人召唤/复活、随机选择、持久实例变量；需"能区分重复扣费/重复伤害/错误父槽/实例复制/监听变化"的反例 | B-06a 的对应机制 |
| **B-06c** | 把 B-05a 冻结范围全部迁入新路径，输出四态覆盖与差异清单 | B-06b、剩余 72 张 |
| **K-09b** | base-spire 默认入口转新后端、旧兼容入口显式命名、旧 STS 实现隔离到游戏侧兼容项目 | B-06c + 所选 run 范围有证据 |
| **B-07b/c/d** | 路线与房间框架 / 经济与选择 / 遗物与跨阶段机制 | 依次为 B-07a / B-07b / B-07c+B-06b |
| **N-06** | 整局公开规范（房间/路线/待选牌/商店/事件/选项/遗物/公共资源） | 每批 B-07 有来源后；目前**只覆盖战斗** |
| **J-02 / J-03** | 有限整局闭环 / 冻结整局范围与差异报告 | B-07a~d + N-06 / J-02 + B-06c |
| **§16 L-00 / L-01a~L-05a** | 训练工程、诊断、学习流程、评估、模型包 | 工程可做；**真实实验**要等 §16.2 的目标/偏好/预算明确 |
| **K-08 Lua** | 独立批次 | 只阻塞实际需要 Compute 的规则 |

## 5. 已知缺陷与"来源缺口"（**不是**完成项，审计时优先看）

### 5.1 B-07a：失败写回不成立 + "带条件的写回"三种形态都没跑通

| 实验 | 做法 | 观察 | 结论 |
| --- | --- | --- | --- |
| ① | 把结算接在**胜利分支内**（集合表达式 + spread） | 会话进 Terminal 但 `Run.Hp` 未变、`BattleSettled=false` | **分支内调用不执行** |
| ② | 把结算接在 `LosePlayerHp` 的 **body 末尾** | 失败终局成立（Defeat）但 `Run.Hp=73`（未变）、`settled=false` | 该路径的末尾调用**也没执行** |
| ③ | 结束条件放进**过程内部**（读敌人集合算"全灭"） | 胜利路径也**不再写回** | **跨过程读敌人集合不可靠** |
| ④ | 结束条件由**调用方作用域算好、参数传入** | 仍不写回 | 带条件的写回整体不生效 |

当前保留的是**唯一被验证会真的写回**的形态：`SettleTail` 接在 `DamageMonster` / `DamagePlayer` 的
**body 末尾**、过程内只保留 `Not(BattleSettled)` 守卫。**因此写回是幂等的，但不是"只在战斗结束时"发生**
——中途受伤也会写一次（表现为"提前结算"）。这段结论已写进源码注释，`SliceFrontendRunStateChecks.cs`
的文件头也明确写了"本文件**没有**对应的通过检查"，且**没有**用"断言错误行为"冒充验收。

**审计建议**：这是最需要复核的一块。可先在一个最小场景里验证四个实验的观察是否可复现（命令见 §7），
再从 IR/调度器层面找原因（怀疑点：跨过程按类型/集合取实例的可见性、以及 `If` 体的执行条件）。

### 5.2 三条**来源缺口**（不猜规则、不编数值，故未声明对应内容）

| 内容 | 缺口 | 需要什么 |
| --- | --- | --- |
| `PerfectedStrike` | `cardsWithTag` **没有牌区限定**（源码里只此一处），`sts2/` 也无实现 ⇒ "数哪些牌区"不确定 | 口径裁定或来源实现位置 |
| `BodySlam` | `attackUsingBlock` 在恢复源码里**不存在** ⇒ 这次攻击是否 powered（是否吃 Strength/易伤）不确定 | 来源或裁定 |
| `Stomp` | 差 hook 上的 `when-card-type` 过滤 + `onEnterCombat` 的 `selectedIsClone`（克隆）分支 | 下一批能力（不需要外部裁定） |

### 5.3 已显式登记的其它边界

- **D-03 划分退化**：当前冻结场景集只有 2 个簇，`final_eval` 为空 ⇒ **这批数据不得当作训练/评估划分使用**（报告里已如实标注）。
- **能力边界（显式跳过而非静默丢弃）**：`PlayedThisTurn`、`Monsters`、`Known`、`KnownReveal` 等集合在公开投影里以具名理由跳过；Engine 侧无 `Sequence`/`Group` 声明入口、`Group` 无 `multiplicity`。
- **异步/并发在飞超时**：N-05 未覆盖（已声明）。

## 6. 本会话报告索引（按批次，便于审计逐篇核对）

- §12/§13：[h-01-h-02](h-01-h-02-2026-10-02.md)、[h-03](h-03-2026-10-02.md)、[n-05](n-05-2026-10-02.md)、[j-01-m1](j-01-m1-2026-10-02.md)、[d-01](d-01-2026-10-02.md)、[d-02](d-02-2026-10-02.md)、[d-03](d-03-2026-10-02.md)、[d-04](d-04-2026-10-02.md)、[p-02](p-02-2026-10-02.md)
- §14 作者前端：[b-05a](b-05a-content-manifest-2026-10-02.md)、[b-05b](b-05b-slice-frontend-2026-10-02.md)、[b-05c 批1](b-05c-frontend-batch1-2026-10-02.md) … [批23](b-05c-frontend-batch23-2026-10-02.md)
- §14 基础内容（B-06a）：[状态牌](b-06a-status-cards-2026-10-02.md)、[HP损失+Beckon](b-06a-losehp-beckon-2026-10-02.md)、[基础牌+胜利](b-06a-basic-cards-2026-10-02.md)、[抽牌](b-06a-draw-2026-10-02.md)、[对全体施加能力](b-06a-apply-power-all-2026-10-02.md)、[按量累加变量](b-06a-rampage-2026-10-02.md)、[每变体费用+格挡来源](b-06a-entrench-2026-10-02.md)、[自我施加能力](b-06a-inflame-2026-10-02.md)、[能力声明式触发点](b-06a-power-triggers-2026-10-02.md)、[出牌触发点+移除](b-06a-rage-2026-10-02.md)、[多能力同牌](b-06a-multi-power-2026-10-02.md)、[跨实体来源](b-06a-dominate-2026-10-02.md)、[能量](b-06a-energy-2026-10-02.md)、[条件效果](b-06a-conditional-2026-10-02.md)、[缩放数值](b-06a-scaled-amount-2026-10-02.md)、[费用修正](b-06a-cost-modifier-2026-10-02.md)、**[覆盖核算](b-06a-coverage-2026-10-02.md)**
- §14 依赖抽离：[K-09a 依赖图](k-09a-dependency-map-2026-10-02.md)、[K-09a 清点](k-09a-legacy-inventory-2026-10-02.md)
- §15 整局：[整局冻结清单](../../../design/mod-the-spire/run-flow-manifest-2026-10-02.md)、[B-07a 第一小批 + 实验记录](b-07a-run-state-2026-10-02.md)

## 7. 审计者快速核对清单（建议按此顺序做）

1. **数字是否对得上**：跑 §1 的命令表；再抽 2～3 篇报告核对其中的数字与检查名。
2. **覆盖率分母是否被"悄悄变小"**：`python tools/coverage-check/check_slice_coverage.py`
   （三源不变量：清单→分母、作者文本→声明、测试侧清单→已迁；任一不成立即退出码 1）。
3. **负例是不是真的被拒绝**：抽查几处"必须被拒绝"的检查，**把它们的变异字符串手工改一下**
   ——本会话**两次**踩到"变异因 CRLF/缩进失配而静默失效"（批 9、批 14），已改成不依赖换行/缩进的变异，
   但值得独立复核。
4. **新路径是否偷偷回退旧后端**：`python tools/dependency-check/check_dependencies.py`（9/9，含自检）。
5. **B-07a 缺陷是否可复现**：按 §5.1 的四个实验；重点看"分支内调用不执行"与"跨过程读集合不可靠"。
6. **公开契约是否有静默省略**：net 投影对**每个**引擎字段都要求显式处置（本会话新增
   `Card.Unplayable/Ethereal/CostModifier`、`Player.LostHpThisTurn`、`CardKind.Status`、`net:cat:Status`
   都逐条写了发布/跳过与理由）——可反向检查是否还有未处置字段。

## 8. 待裁定项汇总（需要规划者/用户给结论）

| # | 事项 | 影响 | 我需要的信息 |
| --- | --- | --- | --- |
| ① | `cardsWithTag` 统计的**牌区范围** | `PerfectedStrike` 能否声明 | 口径，或 `sts2/` 里的实现位置 |
| ② | `BodySlam` 的 **powered 语义**（`attackUsingBlock`） | 该卡能否声明、伤害是否吃修正 | 来源或裁定 |
| ③ | **药水栏是否进公开快照** | net 公开词典/目录是已承诺契约 | 是否发布、以何种形态 |
| ④ | **`FlexPotion` 的临时能力触发点** | `onStackChange`/`onExpire` 语义 | 语义确认（尤其"过期是否撤回已给出的力量"） |
| ⑤ | **事件/古代人内容范围** | 当前注册数为 **0**：是内容缺口还是不在本轮范围 | 范围裁定（以及来源是否可得） |
| ⑥ | 公开实例 ID 的**创建顺序映射** | `design.md:61` 相关风险 | 是否本轮处理 |
| ⑦ | P-01 的 **15 条 `missing` 不变量** | 其中 6 条属 N-03 规范 | 处置意见 |
| ⑧ | U-11 `error.path` 语法 | 错误路径的公开形态 | 语法裁定 |

## 9. 声明与边界（避免误读）

- 本会话**没有**宣称：B-06a/B-05c/B-07a 已完成；原版一致性已验证；其余角色/多人/任意 mod 已支持。
- "注册数"一律**不**当作"规则覆盖"：B-05a 的三条分母与 `coverage-check` 的复算就是为此存在的。
- 故障注入类证据（H-03、N-05）都**标注**为故障注入，不与"真实运行"证据混用。
- 未提交、未清理共享 Git；未操作真实游戏；未创建执行 agent。
