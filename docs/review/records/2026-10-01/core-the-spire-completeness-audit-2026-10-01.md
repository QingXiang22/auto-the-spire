# core-the-spire 清单完成度审计（K-01 至 K-07）

> 历史证据（2026-10-03整理）：正文保留原时点结果，历史“下一步”不再作为当前派单指令。本报告归属W-05、W-06、W-02，未结项转入[当前清单](../../tofix/README.md)；移入records不表示修复或验收通过。逐份归属见[记录索引](../README.md)。

> 2026-10-01 规划复核：§3A应限定为“只有一个动态候选查询”，现有 Domains/Bindings 可含多个声明 choices 参数；不阻塞首批 N-01。§3B及§6“请求结果查询/停止与新局重放必须依赖整体快照”的推论不采纳。专项检查、Ref默认值/ExecutionOnly与数值来源仍开放，任务与理由见[评审处置](review-followup-2026-10-01.md)。下文保留原审计时点。

| 项目 | 内容 |
| --- | --- |
| 状态与日期 | 2026-10-01；按开工清单 §5 的**验收判据**逐条核对，不是按"测试是否通过"核对 |
| 审计对象 | `core-the-spire/Core.TheSpire/Engine/`（7 个源文件）与 `Core.TheSpire.Engine.Tests`（32 项检查） |
| 结论 | 交付项齐全、验收判据现均有可执行证据；**但审计发现 4 处"判据不成立"的实现缺陷（已全部修复并加回归）**，另有 2 处表达力缺口需维护者决策、若干"实现存在但无专项检查"项 |
| 验证 | `dotnet build` 0 错误；Engine 检查 32/32；切片检查 21/21；doc-check 0 problem |

## 1. 逐条核对

| 任务 | 清单要求（交付） | 清单要求（验收判据） | 状态与证据 |
| --- | --- | --- | --- |
| K-01 定义与链接 | 复用 Schema/IR/RuleSet；补类型、作用域、引用域、依赖拓扑、显式覆盖、冻结与身份校验 | 缺引用/环依赖/非法覆盖/纯过程副作用拒绝；注册遍历稳定；**修改相关语义改变身份** | 🟡→✅ **本轮修复 3 处后成立**。前两条有 9 项检查；第三条此前**只有 2 项弱证据**（同输入同哈希 + 改版本号变哈希），本轮改为 26 维语义探针（见第 2 节），并修掉 3 个身份缺陷 |
| K-02 实例与存储 | 类型化字段、集合/关系、作用域、稳定内部实例 ID；局部槽与玩法字段分开 | 两会话不共享；集合互斥/容量/引用校验；非法单命令无半写入；销毁策略可判定 | ✅ 5 项检查（含回滚后哈希/版本/实例数不变、销毁清空入站可选引用、身份不复用）。局部槽=调度器帧、玩法字段=存储，两者分离 ✅ |
| K-03 唯一调度器 | 显式调用/返回/分支/迭代帧、局部变量、事件监听快照、预算与 Faulted | 嵌套次序正确；循环帧数随深度而非迭代数增长；事件级联超限明确故障；扩展不能重入 Step/Drain | ✅ 8 项检查（含"MaxCallDepth=4 跑 50 次迭代"证明循环不占帧、事件级联预算、计算后端重入被拒）。事件监听在 emit 时快照并按 priority/声明序派发 ✅ |
| K-04 动作与选择 | 通用动作权限/guard/候选查询、单参与者决策、版本核对、暂停续接 | 非法/过期选择不改状态或 RNG；暂停点续接不重放；同一候选查询参与合法性与执行验证 | ✅ 5 项检查 + 切片 3 项。**注意**：动作只能声明**一个**候选查询（见第 3 节缺口 A），多域动作靠过程内 `RequestDecision` 表达 |
| K-05 RNG 与数值 | 具名随机服务、算法/种子派生配置、计数/状态诊断；数值精度/取整/溢出契约 | 固定配置/seed 可重放；查询不消费 RNG；查除零/溢出/取整；不冒充原版算法等价 | ✅ 2 项检查（覆盖 13 个断言组）。身份含算法 id、档位 id、种子指纹 ✅。**注意**：切片走整数运算，未使用 `NumericProfile`（见第 3 节缺口 C） |
| K-06 参与者投影 | 包声明的公开/私有/已知投影、纯读查询、稳定边界接口 | 多次观察/合法查询不改状态或游标；内部身份与隐藏顺序不自动公开；终局与故障分开 | ✅ 2 项检查 + 切片 3 项。本轮新增"只经身份不可见区域可达的实例不进投影"（此前会泄露隐藏抽牌堆的成员/顺序） |
| K-07 通用性验收 | 一个小型网格走子包 + 一个私有手牌/公共区域微包，置于测试夹具 | 同一运行时不加游戏专用分支加载两包；走子/资源/胜负由包定义；记录实际执行检查数与结果 | ✅ 32 项检查；项目只引用 `Core.TheSpire`；检查用反射断言 core **不引用** BaseSpire ✅ |

## 2. 本轮审计发现并修复的 4 个缺陷

| # | 缺陷 | 为什么判据不成立 | 修复 | 回归 |
| --- | --- | --- | --- | --- |
| 1 | **身份不覆盖 schema 细节**：字段只哈希 `Name/Kind/Visibility/Lifetime`，集合/关系/作用域/事件/终结结果/投影/订阅/过程参数/动作角色与候选查询**完全没进身份** | 改字段边界、集合容量、互斥组、可见性、订阅优先级、投影内容等**都不改身份** | `ComputeIdentity` 改为逐字段 dump 冻结表（字段含 Shape 全部成员、Enum/Target id、Optional、默认值；集合含 Capacity/Group/Order/Identity/Size；关系 Many/Public；作用域父链；事件与终结结果字段；compute 含 Limits/Profile/输入输出；过程含参数/局部/返回/纯性/IR；动作含角色/参数/候选索引与查询/guard/choices/program；投影逐类型含 Include/OwnerField/IncludeHidden；订阅含 priority/order/scopePayload/recheck；sessionStart） | `K-01 the rule set identity follows every semantic dimension`（26 维探针，每维断言哈希必须变化） |
| 2 | **身份不覆盖 resolution 表** | 两个只在"某引用槽解析到哪个声明"上不同的规则集可同哈希（探针 `ActionCandidateQuery` 抓出：`Items`/`Spare` 定义相同、只靠槽解析区分） | 把整张 `Resolution[]` 纳入身份（`DumpResolution` 按类型 dump 目标 id） | 同上（该维现通过） |
| 3 | **实体类型只能向后引用** | 字段引用同一包中**后声明**的类型会以 `ReferenceUnresolved: entity type 'x' is not part of the rule set` 失败 → **互递归类型无法声明** | `AssignIds` 改为两遍：先给所有类型分配密集 id，再解析字段形状（与作用域父链同样处理） | 探针 `FieldTarget`（前向引用）必须链接成功 |
| 4 | **`IrDumper` 的 `default` 静默返回** | 未知节点只 dump 原语名 → 两个不同节点可能同文本 → 身份碰撞风险（`FailInst` 的消息此前完全没进 dump） | `default` 改为抛明确 `Unsupported` 故障；补 `FailInst`（含消息）、`FrameScopeExpr`、`EntityScopeExpr`、`CollectionIndexExpr` 的 case | 脚本核对：**51 个 IR 节点类型 / 65 个 dumper case，无遗漏**；任何未来新节点在首次使用时**明确报错**而不是静默碰撞 |

缺陷 1/2/4 都直接违反 K-01 的"修改相关语义改变身份"；缺陷 3 违反"引用域"的完整表达（清单要求"补类型、作用域、引用域"）。它们此前**没有测试覆盖**，是本轮专门为"审计 core 完成度"而设计的探针抓出来的。

## 3. 两处表达力缺口（需维护者决策，未擅自扩 IR）

**A. 动作只能有一个候选域。** `ActionSymbol.CandidateParameter`/`CandidateQuery` 是单数，`ActionDefinition` 也只带一个 `CandidateParameterIndex`。因此"先选牌、再选目标"这类**两参数域**的动作无法直接声明，只能把第二个域放进过程体用 `RequestDecision` 表达（切片即如此：`PlayCard` 只有卡牌候选，目标由决策请求）。
影响：N-01 的动作目录/线协议若要求"一个动作带多个候选集"，需要先扩 IR（K-01/K-04 契约变更）；B-05 的作者前端也会受此限制。

**B. 没有会话内整体快照/恢复。** 存储层有按指令的原子回滚、`StateHash()`，随机服务有 `Capture/Restore`，但 `SessionState` 本身没有快照与恢复。K-02 的交付项不要求它，但 §9.3/N-03 的"不确定结果查询或停止"与 L 批的 fork/重放需要；建议作为 N-03 的前置项单独立项。

## 4. 实现存在、但缺专项检查（诚实清单）

| 范围 | 缺什么 |
| --- | --- |
| K-01 | 检查器已实现但**无反例测试**：compute 输入/输出覆盖与静态预算、投影拥有者字段非参与者、事件 payload/终结结果字段归属错配、`FrameScope` 误用、决策种类与结果槽类型不匹配、`EntityScope`/`CollectionIndex` 的操作数类型 |
| K-02/K-03 | 没有公开的**帧深度/迭代计数诊断**（"循环不占帧"靠 `MaxCallDepth=4` + 50 次迭代间接证明）；`Session.Step()` 无专项检查 |
| K-05 | **切片走整数运算，没有使用 `NumericProfile`**；decimal 取整契约只有单元检查。旧后端"双重 `Floor`"与 sts2"末端单次截断"的差异仍待 B 批选定（见来源核对报告差异 D） |
| K-06 | `ParticipantView` 的记录相等是**引用相等**（列表字段），N-02 映射到 `PublicSnapshot` 时需要结构化比较或稳定序列化 |

## 5. 不在 core 范围（清单原文如此）

游戏规则/内容/流程由游戏包定义（`base-spire` 切片已证明）；`net`/`ai` 的交付在各自模块；Lua 计算后端属 K-08（`IComputeBackend` 缝与假后端测试已就位，重入拒绝有实测）；投影的"已知"语义**由包声明**，引擎不推断（切片用 `KnownCard` 表达）。

## 6. 对 mod-the-spire 的影响（优先排查这条的原因）

core 已能承载 base-spire 的受控切片（21 项检查），且 core 不引用 base-spire。三个未覆盖点会直接压到后续包工作：
1. **多域动作（缺口 A）**——B-05 作者前端要把"目标选择"写进 Guard/候选时最先撞上；
2. **会话快照（缺口 B）**——B-07 整局流程与 N-03 恢复语义的前置；
3. **帧深度/预算诊断（第 4 节）**——长局与性能预算需要可观测的调度器指标，目前只有单命令指令数。

## 7. 验证命令

```
dotnet build AutoTheSpire.sln                                     # 0 错误
dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests    # All 32 engine checks passed.
dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests  # All 21 slice checks passed.
python tools/doc-check/check_docs.py .                            # 0 problem
```
