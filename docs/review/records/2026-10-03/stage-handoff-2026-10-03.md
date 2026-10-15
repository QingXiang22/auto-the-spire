> 记录整理（2026-10-06）：原交付、复核及逐批证据保留；开放项已并入[主清单](../../tofix/README.md)，执行合同见[对应提示词](../../../design/agent-goals.md)。本文按原日期阅读，不作为当前待办入口；移入records不表示问题解决或父项验收。

# 阶段综合交接：W-05 复核与当前基线（2026-10-03）

**负责人最新派单（2026-10-04）：** 用户决定完整环境优先，当前按[ENV完整单人环境与源规则对拍](../../../design/environment-completion-and-differential-v1.md)推进core/Engine、base正式DSL/完整内容与Run及必要net出口；D3/D4未执行方案退出当前/暂缓，不新增学习。D1/D1.1/D2及R1保留，状态只看[唯一清单](../../tofix/README.md)。旧批次RESUME HERE/下一原语属历史，不能绕过ENV的新完整分母、RNG及保真验收；最新依据见[阶段复核](../2026-10-04/battle-learning-stage-review-2026-10-04.md)。

> 负责人更新：完整任务清单已恢复，当前派单只看[README](../../tofix/README.md)。本报告批次2～24是执行者原时点证据；“W-02核心完成、只剩再揭示”已由[批次25](#批次-25负责人恢复清单与重新派单)修正。下述RESUME HERE保留本执行线的历史定位，不能替代全项目主清单。

| 项目 | 内容 |
| --- | --- |
| 认领 | **W-05**（持久Run与两房间闭环）：执行者＝本会话；负责目录＝`mod-the-spire/base-spire/**/Slice/**` ＋（必要时）`core-the-spire/Core.TheSpire/Engine/**` 的通用机制；状态＝**进行中** |
| 本文角色 | 本阶段（2026-10-03）的**唯一阶段交接**；同阶段的小批次追加到本文，不另建报告（遵循 [tofix/README](../../tofix/README.md) §4.2） |
| 前置复核 | 按 [§19.4](../../../design/implementation-kickoff.md) 要求**不照抄旧报告结论**：先复核当前工作区（目录已被重排：`Legacy/`、`Engine/` 等就位，`git status` 有 35 项 `D` + 29 项 `??`，属他人未提交的重排，**未触碰**） |

## 0. RESUME HERE（新会话从这里开工，不必读完本文）

**当前基线**（最近一次实跑，返回码全 0）：build 0 错 0 警 · Engine 64 · 切片 **194** · 旧后端 190 · net **386** ·
J-01 **162** · AI 503 · client 50 · 契约 210 · 覆盖 1（四态：分母 94 / 已声明=已加载=行为=22 / **host 可用=0** / 剩余 72）·
依赖 9 · doc-check 0 · trace 0 · `git diff --check` 干净。工作区有**他人未提交的目录重排**（`Legacy/`、`Engine/` 就位），**不要**重置/清理。

**下一步（首选，W-01，配方见 §批次21 + 顺序修正见 §批次23）**：

```powershell
# 1) 先看当前阻塞项（应为 [N-02-UNDECLARED-ACTION] base-spire:UsePotion，rc=3）
python net-the-spire/client/test_author_content.py
```

按**修正后的顺序**在 `SlicePublicProfile.CreateAuthorContent()` 的变体字典里逐项加声明，每加一项重跑上面的探针：

1. `AuthorKinds()`：药水 kind（槽位/容量/可用状态用**字段**表达；§19.2 禁止借用卡牌 pile 语义）
2. `AuthorCollections()`：`base-spire:PotionBelt` 由"具名跳过"改为**发布**（默认 Profile 不动）
3. `AuthorActions()`：`base-spire:UsePotion`（`TargetKindIds` 指向第 1 步的 kind；引擎侧只有一个 `Potion: ref<药水>` 参数）
4. `AuthorKinds()` 续：`base-spire:Run`（HP/金币字段）、`LongTermDeck` 的公开表达（W-05 语义：长期 vs 战斗两批实例）
5. 探针 **rc=0** 后：把断言扩到"使用药水 → 终局 → 离线重编码"，并去掉 Runner/Collector 的硬编码身份（`ai-the-spire/ai_the_spire/runner/episode.py:27,30`）

**次选（W-05）**：第二房间 + 中间奖励/升级需要**整局级入口**（与 W-01 宿主相邻的协调项）；D5① 在第二房间场景里断言"重复结算不重复奖励"。

**边界（勿踩）**：

- 给**共享** Profile 增加"作者独有"的字段/集合/动作/kind 会改动程序化冻结切片的目录与规范字节——实测发布**一个字段**即打掉 net 40 项 + J-01 4 项 ⇒ 必须走**变体**（`CreateAuthorContent()` 已存在）。
- 终局之后同块指令**不执行**（`Scheduler.cs:803` 起）⇒ 终局后要做的写回必须放在设终局**之前**（W-05 已按此修好）。
- 公开引用一律从 `ProjectedSnapshot.InstanceRefs` 取；句柄由**可见身份**决定，与创建顺序无关（W-02 已完成并有故障注入证据）。
## 1. 当前工作区基线（本阶段实跑，非引用旧数字）

| 命令 | 返回码 | 结果 |
| --- | --- | --- |
| `dotnet build AutoTheSpire.sln` | 0 | 0 错误 0 警告 |
| `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` | 0 | 64/64 |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | 0 | **190/190** |
| `dotnet run --project core-the-spire/Core.TheSpire.Tests` | 0 | 190/190（旧后端） |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | 0 | 383/383 |
| `python ai-the-spire/tests/j01_episode.py` | 0 | 150/150（M1 回归） |
| `python ai-the-spire/tests/run_checks.py` | 0 | 503/503 |
| `python net-the-spire/client/test_client.py` | 0 | 50/50 |
| `python tools/contract-check/check_contracts.py` | 0 | 210/210 |
| `python tools/coverage-check/check_slice_coverage.py` | 0 | 分母 94 / 已迁 22 / 剩余 72；三源不变量全过 |
| `python tools/dependency-check/check_dependencies.py` | 0 | 9/9 |
| `python tools/doc-check/check_docs.py .` | 0 | 0 problems |

## 2. W-05 现状（复核结论，含**必须改**的设计冲突）

### 2.1 已核实成立

- `run { hp / max-hp / gold }` 声明可解析，未知键显式拒绝；`Run` 单例**在战斗开始前**创建（避免 setup 内 `on_enter_combat` 结束战斗时取不到实例）。
- **胜利写回成立**：切片检查 `B-07a 写回：胜利后整局 HP = 玩家收尾 HP，且 BattleSettled 置位 [真实]` 通过（含在 190/190 内）。
- `SettleBattle` 的写回是**幂等**的（`Not(BattleSettled)` 守卫）。

### 2.2 未成立 / 必须修

| 编号 | 问题 | 证据 |
| --- | --- | --- |
| **D1** | **失败（`lose_hp` 路径）不写回** | 上一阶段实验②：终局确实为 `Defeat`，但 `Run.Hp` 与 `BattleSettled` 都没变；对应检查**未留在套件里**（避免用"断言错误行为"冒充验收），缺陷写在 `SliceFrontendRunStateChecks.cs` 文件头 |
| **D2** | **"提前结算"**：写回接在各伤害过程 **body 末尾**且守卫只有 `Not(BattleSettled)` ⇒ **战斗中途受伤也会写一次** | 源码注释已标注为已知缺陷（唯一被验证"真的会写回"的形态就是 body 末尾无条件调用） |
| **D3** | **全局 `BattleSettled` 与 §19.2 冲突** | §19.2 明确：*"写回幂等性按战斗/房间实例区分，一个不重置的全局 BattleSettled 布尔不能代表所有房间"* ⇒ 必须改成**按战斗实例**的幂等键，否则第二个房间无法再结算 |
| **D4** | 未做：长期牌组与临时战斗实例边界、两个连续战斗房间、中间卡牌奖励/升级选择 | W-05 完成判据要求的其余部分 |
| **D5** | 未做：三条验收检查（重复调用不重复奖励、同定义实例分别升级、临时生成牌不误入长期组） | 同上 |

### 2.3 四个调试实验（保留为起点，但不盲从方向）

分支内调用不执行／setup 末尾紧随创建后调用也取不到实例／过程内读敌人集合不可靠／把结束条件作为参数传入仍不写回——记录在
[records/2026-10-02/b-07a-run-state](../2026-10-02/b-07a-run-state-2026-10-02.md) §3/§3b。
**下一动作不再沿用这些方向**，改从"按实例幂等键 + 结束时刻"这两条设计入手（见 §3）。

## 3. 下一份可运行交付（W-05 第一步）

1. **把幂等键改成按战斗实例**：在 Run 上放"已结算的战斗序号/实例引用"，而不是一个全局布尔；
   重复结算**同一场**是空操作，进入下一场后可以再结算一次（直接回应 D3 与 §19.2）。
2. **只在战斗结束时刻结算**（修 D2）：先解决"结算调用挂在哪里才真的执行"这一点——
   用**最小可观察**的方式验证：在同一场景里分别构造"战斗中受伤"与"战斗结束"两种情形，
   断言前者**不**改变 `Run`（现在是会改的）。
3. 修 D1（失败不写回）并补回对应的通过检查；再补 D5 的三条检查。
4. 之后才做长期牌组/两房间/中间奖励（D4）。

## 4. 未结项与依赖

- W-05 的 D1/D2/D3 属本阶段自有；D4/D5 依赖前述修好后的 Run 骨架。
- 与其它 W 项的关系：**W-05 独立推进**（§19.2），不依赖 W-01/W-02 的宿主接入；
  W-01/W-02（真实 stdio 宿主接入、公开 ID 去序号透传）与 W-04（低成本模型工程）本阶段**未认领**。
- 事件/古代人内容缺失仍归 W-05/W-06（来源缺失逐项登记）。

## 5. 本阶段改动

- 新增本文件（阶段交接），并按 §4.1 在 [tofix/README](../../tofix/README.md) 登记 W-05 的执行者/范围/状态/下一份交付。
- **未改代码/测试**（本阶段只做复核、基线与设计冲突登记）；未提交、未清理共享 Git。
---

## 批次 2（2026-10-03）：W-05 第一步完成——按战斗实例幂等 + 只在战斗结束结算

### 根因（修 D1/D2 的关键）

`Scheduler.cs:803-819`：`SetTerminalInst` 设置 `Terminal`/`Status` 后返回 `Outcome.Completed`，
**调度器随即停止执行同一块里的后续指令**。因此上一阶段把结算调用放在终局*之后*（无论分支内还是 body 末尾）
都不会执行——这正是"失败不写回"与"提前结算"两类现象的共同来源。
**修法：把结算放在 `SetTerminal` 之前**（同一分支内），即可稳定执行。

### 改动

| 文件 | 改动 |
| --- | --- |
| `BaseSpire/Slice/CombatSlicePackage.cs` | 幂等键从 `Run.BattleSettled`（全局）移到**战斗作用域的玩家** `Player.BattleSettled`（新战斗＝新实例＝false ⇒ 按战斗实例幂等，符合 §19.2"不能用一个不重置的全局布尔代表所有房间"）；`SettleBattle` 改为"先写回 HP、再置这场战斗的守卫"；三处终局分支（胜利 / 普通伤害失败 / HP 损失失败）都在 `SetTerminal` **之前**调用结算 |
| `net-the-spire/Net.TheSpire/Projection/SlicePublicProfile.cs` | 为 `Player.BattleSettled` 写显式处置：**Skip**（`N-02-INTERNAL-BOOKKEEPING`，理由：结算幂等键是引擎内部簿记，终局事实已由终局结果公开表达） |
| `BaseSpire.Slice.Tests/SliceFrontendRunStateChecks.cs` | 更新两处断言到玩家作用域；**补回**失败写回检查；**新增**"只死一个敌人时不结算"检查 |

### 验证（本批次实跑）

| 检查 | 结果 |
| --- | --- |
| `B-07a 写回：胜利后整局 HP = 玩家收尾 HP，且守卫置位 [真实]` | ✓ |
| `B-07a 写回：只死一个敌人时**不**结算（战斗未结束）[真实]` | ✓（新；以前会被提前结算） |
| `B-07a 写回：失败（HP 损失路径）也写回一次 [真实]` | ✓（新；D1 修复证据） |
| `B-07a 整局：Run 单例在开局被创建…` / `程序化路径不建 Run 实体` | ✓ |

全量：build **0 错误 0 警告**；切片 **192/192**；Engine 64/64；旧后端 190/190；net 383/383；
J-01 150/150；AI 503/503；client 50/50；契约 210/210；覆盖 1/1；依赖 9/9；doc-check 0 problems。

### W-05 未结项（下一步）

- **D4**：长期牌组与**临时战斗实例**的边界；**两个连续战斗房间**；中间**卡牌奖励/升级选择**（战斗结束不再终止整局）。
- **D5**：三条验收的显式断言——① 重复结算同一场不重复奖励（当前由"这场战斗的守卫"隐含成立，需在**第二房间**场景里显式断言）；② **同定义实例分别升级**；③ **临时生成牌不误入长期组**。
- 第二房间起：`Run` 的 Hp/MaxHp/Gold 已可跨战斗持续，但"新战斗 = 新玩家实例"意味着 ① 幂等键天然重置 ✓、
  ② 跨房间的长期牌组尚未建模（当前牌组仍是每场战斗按声明创建）。
---

## 批次 3（2026-10-03）：W-05 长期牌组与临时/长期边界（D4/D5 第一步）

### 改动

| 文件 | 改动 |
| --- | --- |
| `BaseSpire/Slice/CombatSlicePackage.cs` | `Run` 侧新增**长期牌组**集合 `LongTermDeck`（只在作者文本声明了 `run` 时创建）；setup 里为**每个声明条目**另建一份实例，**只进长期牌组、不进任何战斗牌区** ⇒ 战斗实例与长期实例是两批独立实例 |
| `BaseSpire.Slice.Tests/SliceFrontendRunStateChecks.cs` | 新增检查：长期牌组张数＝声明卡组张数；战斗牌区（抽牌堆/手牌/弃牌堆）实例与长期实例**不重叠**；把一张**战斗**牌置为升级态后，**同定义的长期实例仍未升级** |

### 验证

| 检查 | 结果 |
| --- | --- |
| `W-05 长期牌组：按声明逐张建立，且与战斗实例是两批独立实例 [真实]` | ✓（含 D5②："同定义实例分别升级/移除"成立） |
| 既有 B-07a 写回四条 + 声明/创建两条 | ✓ |

实跑：build **0 错误 0 警告**；切片 **193/193**；Engine 64/64；旧后端 190/190；net 383/383；
J-01 150/150；AI 503/503；client 50/50；覆盖 1/1；依赖 9/9。

### W-05 未结项（下一步）

- **第二房间 / 中间奖励·升级**：需要**整局级的入口**（"战斗完成先提交一次结果并退出战斗，随后仍可进入下一房间"，§19.2）。
  当前会话在终局即结束，`LongTermDeck` 已就位但还没有"下一场战斗从长期牌组重新发牌"的流程；
  这一步与 **W-01 的宿主/Profile 入口**相邻，属共享文件的协调项（先按 §19.2 由 base-spire 侧提出入口形状，再与 net 宿主对齐）。
- **D5①**：在第二房间场景里显式断言"重复结算同一场不重复奖励"（当前由"这场战斗的守卫"隐含成立）。
- **D5③**：临时**生成**牌不进长期组的显式断言（当前由"两批独立实例"结构保证，尚未用生成路径单独断言）。
---

## 批次 4（2026-10-03）：W-05 D5③ 关闭——战斗内**生成**的牌不进长期组

### 改动

| 文件 | 改动 |
| --- | --- |
| `BaseSpire.Slice.Tests/SliceFrontendRunStateChecks.cs` | 场景加一张 `test-generate`（`steps = [generate:strike-ironclad]`）夹具牌；新增检查：打出后手牌里多一张打击、该**生成实例不在**长期牌组里、长期牌组张数与成员**不变** |

### 验证

| 检查 | 结果 |
| --- | --- |
| `W-05 长期牌组：战斗里生成的牌不进长期组（D5③）[真实]` | ✓ |
| `W-05 长期牌组：按声明逐张建立，且与战斗实例是两批独立实例 [真实]` | ✓ |

实跑：build **0 错误 0 警告**；切片 **194/194**；Engine 64/64；旧后端 190/190；net 383/383；
J-01 150/150；AI 503/503；client 50/50；覆盖 1/1；依赖 9/9。

### W-05 剩余未结项

- **第二房间 / 中间奖励·升级**：需要整局级入口（§19.2），与 W-01 宿主/Profile 入口相邻 ⇒ **协调项**，先定入口形状再与 net 对齐。
- **D5①**：第二房间场景里的"重复结算同一场不重复奖励"显式断言（当前由这场战斗的守卫隐含）。
---

## 批次 5（2026-10-03）：W-02 定位——公开ID与公开排序的序号透传点（4 处，含行号）

本批次只做**定位与方案**（未改代码）：W-02 的验收要求"成对场景只增删/重排**不可见**内部实例时，
公开结构及对齐后的编码/动作概率等价；不从内部计数器排序公开事实"。当前实现有 4 处直接依赖引擎创建序号：

| # | 位置 | 现状 | 问题 |
| --- | --- | --- | --- |
| L1 | `net-the-spire/Net.TheSpire/Projection/PublicSnapshotProjector.cs:95-96` | `PublicInstanceId(instance) => "e" + instance.Value` | **句柄本身就是创建序号** ⇒ 透传 |
| L2 | 同文件 `:278` | `entities.Sort(CompareByUtf8Bytes(l.InstanceId, r.InstanceId))`（按公开句柄的 UTF-8 字节） | 句柄含序号 ⇒ **公开实体顺序＝创建顺序** |
| L3 | 同文件 `:419`、`:531` | 牌堆/成员列表 `members.Sort(CompareByUtf8Bytes)` | 同上：**牌堆内的公开顺序**也按序号 |
| L4 | 同文件 `:319`、`:325-329` | 关系排序用 `SourceRef`/`TargetRef`（即 L1 的句柄） | 同上：**关系顺序**也按序号 |

（`relations`/`piles` 等其它排序键 `TypeId`/`PileRef` 同样间接含序号。）

### 结论与建议方案（供负责人/执行者确认后实施）

1. **句柄**：改为"会话/观看者内的公开引用绑定"——`InstanceId → 公开句柄` 的表在会话内**稳定**
   （跨请求不变、`observation_id` 语义保持），句柄本身是**不透明**编号（可整批换名而不影响结构等价）。
2. **编号分配**：按**可见事实**推出的规范顺序分配（例如 类型 id → 定义/名称 → 所属者公开句柄 → 牌区/位置），
   **不**使用引擎创建序号；完全不可区分的同定义重复实例允许占位顺序任意，但**必须与内部序号解耦**
   （否则 L2/L3/L4 的等价性无法成立）。
3. **排序**：L2/L3/L4 全部改为按上述**可见键**排序（或按规范化后的句柄，但句柄必须已与序号解耦）。
4. **验收骨架（先建）**：成对场景（只增删/重排不可见内部实例）→ 公开快照经"一致换名"后**结构相等** →
   编码特征相等 → 动作概率相等；再覆盖"重复同定义实例、再揭示、授权撤销、引用闭合"。

### 与其它 W 项的关系

- W-02 归 net（会话/观看者绑定），但句柄分配用到"公开可见事实"的规范顺序 ⇒ 需要 core `Engine` 的公开视图配合
  （若有通用机制缺口，按根约定补 `Engine/`）。
- **W-01 的宿主接入**（`ControlHost.SupportedScenarioProfile`、宿主 `CombatSlicePackage.Build` 写死、AI 侧
  `GYM_ID`/`SCENARIO_PROFILE` 常量）与本批次相邻；两项都要动 net 宿主与 AI Runner 的**共享文件**，
  建议由负责人协调同一改动窗口。
- 本批次**未**改代码，构建/检查沿用批次 4 的结果（切片 194/194、net 383/383、全量零回归）。
---

## 批次 6（2026-10-03）：W-02 实现**已试跑**（可行）+ 迁移范围量化（7 处，2 个文件）

### 做了什么

把公开句柄从"内部创建序号"改为"**按可见身份的规范顺序分配的不透明编号**"（改 `BuildInstanceTable`：
按 `TypeId` → profile 声明的 **内容身份字段** → 其余**已发布字段**（按名序）排序后依次编号）。
因为 L2/L3/L4 三处公开排序都是**按句柄**排序，句柄一改，四处泄漏**一并消除**（一次改动覆盖 4 个泄漏点）。

### 结果（实跑）

- 构建：**0 错误 0 警告**；句柄确实按可见身份重排（例如同一张牌在不同创建顺序下得到同一编号）。
- net 套件：**4 项失败**，都属"**测试里预测旧句柄**"的迁移项，不是投影逻辑错误：

| 失败检查 | 现象 |
| --- | --- |
| `N-02 暂停期投影…候选恰为 PendingDecision.Candidates` | expected `[e2,e3]`, got `[]`（期望值由引擎实例号推出） |
| `H-01/H-02 已知位置经真实实例引用进入 wire` | expected `e9`, got `e2` |
| `V-03 已知位置在真实链路上保留/失效` | `Sequence contains no matching element`（用旧句柄反查实例） |
| `N-05 公开动作到通用决策响应的适配` | 同上 |

### 迁移范围（已量化，**精确 7 处 / 2 个文件**）

`net-the-spire/Net.TheSpire.Tests/` 里只有这两处文件用 `PublicSnapshotProjector.PublicInstanceId(...)`
去**预测**句柄：`AcceptanceChecks.cs`（4 处：:138、:548、:636、:666）、`SnapshotChecks.cs`（3 处：:151、:212、:284）。
（全仓库检索：**0 处**写死 `e<数字>` 句柄，故不存在"黄金文件"批量重生成的问题。）

**建议的迁移形状**（把"预测句柄"换成"读取已发布的句柄/结构断言"，也正是 W-02 验收要的"允许一致换名"）：

1. 先取一次投影，**从投影里读出**目标实体的句柄（例如已知位置的 `card_ref`、候选绑定的引用），再用于后续断言；
2. "同一引用跨请求稳定"这类断言改成**用前一帧读到的句柄**在后一帧查找（而不是用引擎实例号反推）；
3. 需要"某张牌"时按**公开语义**定位（动作/候选的绑定引用、内容身份），不按实例号。

### 本轮处置

为避免把套件留在红色，**已把投影改动回退**（net 恢复 **383/383**、build 0/0），
把"实现已试跑可行 + 迁移清单 + 迁移形状"记在本批次，**下一次把投影改动与这 7 处测试迁移作为一个协调批次一起落**（同一批共享文件：`Projection/PublicSnapshotProjector.cs` + `Net.TheSpire.Tests/{AcceptanceChecks,SnapshotChecks}.cs`）。
另外仍需新建**成对场景验收骨架**（只增删/重排不可见实例 → 一致换名后结构/编码/动作概率等价）。
---

## 批次 7（2026-10-03）：W-02 机制先落——公开引用**绑定表** + 部分测试迁移（保持绿）

按"先机制、后翻转"的顺序推进（避免把套件留红）：

### 已落地（可运行、已实跑）

| 改动 | 说明 |
| --- | --- |
| `Projection/PublicSnapshotProjector.cs` | `ProjectedSnapshot` 新增 **`InstanceRefs`：引擎实例 → 公开句柄的绑定表**（会话/观看者内稳定），并在投影构造点填充。这是 W-02 要求的"会话/观看者内的公开引用绑定"**机制**；消费方（宿主/检查/编码器）今后应从这里取句柄，而不是用引擎序号推导 |
| `Net.TheSpire.Tests/SnapshotChecks.cs` | 3 处"预测句柄"改为**从投影读句柄**（新增 `Refs(session)` 助手）；该文件对 `PublicInstanceId(...)` 的依赖 3 → **0** |

实跑：build **0 错误 0 警告**；net **383/383**（句柄取值未变，因此本批次是纯加法 + 迁移）。

### 下一步（同一协调批次收尾）

1. `Net.TheSpire.Tests/AcceptanceChecks.cs` 剩余 **4 处**（:138、:548、:636、:666）同样改为从投影读句柄——该文件**没有** `Project(session, projector)` 助手，需先按其现有观察助手（`KnownPositions`/`Entities`/`LegalActions` 那一套）补一个等价的 `Refs(...)`。
2. 然后**翻转编号规则**：`BuildInstanceTable` 改为"按可见身份（类型 → profile 内容身份字段 → 其余已发布字段名序）规范排序后编号"，**不再** `"e" + instance.Value`；此时四处泄漏（句柄本身 + 实体/牌堆/关系排序）一并消除。
3. 新建**成对场景验收骨架**：只增删/重排不可见内部实例的两个场景 → 一致换名后**公开结构/编码/动作概率等价**；再补"重复同定义实例、再揭示、授权撤销、引用闭合"。
4. 完成后按 W-02 关闭条件核对（含"对应真实训练须先通过"）。
---

## 批次 8（2026-10-03）：AcceptanceChecks 迁移尝试 → 精确阻塞点（已回退，保持绿）

### 尝试

把 `AcceptanceChecks.cs` 的 4 处"预测句柄"统一改为 `PublicRef(session, …)`（从 `ControlSession.Snapshot().InstanceRefs` 读）。

### 结果（实跑）

- **2 处成功**：位于带 `ControlSession` 参数的方法内（`DiscardedToInstance`、以及反查循环）。
- **2 处阻塞**（编译错误 `CS0103: 不存在名称 session`）：
  | 位置 | 上下文 | 可用来源 |
  | --- | --- | --- |
  | `AcceptanceChecks.cs:138`（V-03 已知位置检查体内） | 只有 `first`（`ControlHost`）与 `sessionId`（string） | ✅ 宿主已提供 **`ControlHost.GetSessionForTests(sessionId)`**（`ControlHost.cs:66`） |
  | `AcceptanceChecks.cs:548` 附近 | 同属 host/sessionId 形式的检查体 | 同上 |

### 处置与下一步（明确、无需再探）

- 已**回退**这 4 处迁移以保持套件为绿（build **0 错误 0 警告**、net **383/383**）。
- 下一动作（可直接做）：把这两个检查体里取会话的方式统一为
  `var session = host.GetSessionForTests(sessionId);`（或在检查体里改用 `StartedHost()` 的三元组），
  然后再执行"翻转编号规则"（`BuildInstanceTable` 按可见身份规范序编号）——届时 `SnapshotChecks` 的 3 处已不再依赖旧句柄，
  仅剩这 4 处，翻转后可立即看出是否还有隐藏依赖。
- 之后仍需新建**成对场景验收骨架**（只增删/重排不可见实例 → 一致换名后结构/编码/动作概率等价）。

### 本轮改动

- `Net.TheSpire.Tests/AcceptanceChecks.cs`：迁移尝试后**已完整回退**（净改动 0）。
- 保留上一批次的成果：`ProjectedSnapshot.InstanceRefs` 绑定表 + `SnapshotChecks` 3 处迁移。
---

## 批次 9（2026-10-03）：**W-02 核心已落地并验证**——公开句柄不再透传内部创建序号

### 改动（3 个文件）

| 文件 | 改动 |
| --- | --- |
| `Net.TheSpire/Projection/PublicSnapshotProjector.cs` | ① `ProjectedSnapshot` 新增 **`InstanceRefs`**（引擎实例 → 公开句柄的绑定表，会话/观看者内稳定）；② `BuildInstanceTable` **翻转**：句柄改按**可见身份**的规范顺序编号——`TypeId` → profile 声明的**内容身份字段** → 其余**已发布字段**（按名序），不再 `"e" + instance.Value` |
| `Net.TheSpire.Tests/SnapshotChecks.cs` | 3 处"预测句柄"改为从投影读（`Refs(session)` 助手）；对 `PublicInstanceId(...)` 的依赖 3 → 0 |
| `Net.TheSpire.Tests/AcceptanceChecks.cs` | 4 处同样迁移（2 处直接用方法参数里的 `ControlSession`，2 处经宿主 `ControlHost.GetSessionForTests(sessionId)` 取会话）；依赖 4 → 0 |

**为何一次改动覆盖 4 个泄漏点**：L2/L3/L4 三处公开排序（实体、牌堆成员、关系）**都是按句柄排序**，
句柄一经与序号解耦，这三处顺序随之与创建顺序无关。

### 验证（全量实跑，返回码全 0）

| 命令 | 结果 |
| --- | --- |
| `dotnet build AutoTheSpire.sln` | **0 错误 0 警告** |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | **383/383** |
| `python ai-the-spire/tests/j01_episode.py` | **162/162** |
| `python net-the-spire/client/test_client.py` | **50/50** |
| `python ai-the-spire/tests/run_checks.py` | 503/503 |
| `python tools/contract-check/check_contracts.py` | 210/210 |
| `python tools/coverage-check/check_slice_coverage.py` | 1/1 |
| `python tools/dependency-check/check_dependencies.py` | 9/9 |
| Engine / 切片 / 旧后端 | 64/64 / **194/194** / 190/190 |
| `tools/doc-check`（check_docs / check_trace）/ `git diff --check` | 0 problems / 0 / 干净 |

> 关键点：先做**行为中性迁移**（句柄取值不变，383/383）再**翻转编号**（仍 383/383），
> 说明迁移是完整的、没有隐藏依赖被掩盖。

### W-02 仍未结（关闭条件剩余部分）

1. **成对场景验收骨架**：只增删/重排**不可见**内部实例的两个场景 → 一致换名后公开结构、编码特征、动作概率**等价**。
   （本批次的翻转是该性质的**实现保证**，但还缺"用检查证明"的部分。）
2. **引用稳定性覆盖**：公开可识别生命周期、撤销/再揭示、**重复同定义实例**、引用闭合。
3. 关闭前按 §19.3 核对"对应真实训练须先通过"。

### 未结风险（明示）

- 完全不可区分的重复实例之间的编号顺序仍取排序稳定性（任意置换给出同一公开结构，故不构成可推断泄漏）；
  若将来引入"同定义但公开字段完全相同的实例仍有隐藏差异"的内容，需要重新评估这一点。
---

## 批次 10（2026-10-03）：W-02 成对骨架的**确切设计**（含"不可见实例"边界的核实结论）

### 核实结论（决定骨架该怎么造）

- `BuildInstanceTable` 目前对 **`view.Instances` 全体**编号；而 `BuildEntities` 对每个实例都调用
  `_profile.RequireKind(instance.TypeId)`——**未声明的 kind 会抛错，而不是被跳过**。
  ⇒ 凡是进入投影的实例**都是已发布的**；不存在"已发布句柄里混入不可见实例"的缺口。
- 因此 W-02 意义上"只看不可见差异"的**真实边界**是**引擎 K-06 观看者投影**：
  `ParticipantView` 已经只含该观看者可见的实例；协议侧不存在第二层"隐藏实例"。

### 骨架的确切构造（可直接实现）

**成对场景 = 同一批可见实例、不同的内部创建顺序**：

1. 造两个包/会话，**只**把创建顺序对调（例如两个敌人的创建次序互换：A 先/B 后 ↔ B 先/A 后），
   其余声明完全相同；
2. 对两者各取一次公开投影，做**一致换名**比较（本实现下句柄已由可见身份决定，期望**逐字节相同**，
   即"换名"退化为恒等映射）；
3. 断言：公开快照 `WireCanonical` 字节相同、候选/合法动作集合相同、编码特征相同、动作概率相同
   （后两项沿用既有编码/概率设施）。

**为什么这条能真的抓错**：旧的 `"e" + instance.Value` 方案下，创建顺序对调会**同时**改变句柄与
L2/L3/L4 三处排序 ⇒ 该成对比较必然失败；本批次落地后应当通过。这也正是 W-02 关闭条件里
"只改变不可见创建/排列的成对场景不泄漏编号、排序或默认选择"的可执行形式。

### 实现路径（下一动作，二选一）

- **A（推荐，改动小）**：给 `CombatSlicePackage.Build` 已有的测试旋钮再加一个
  `swapEncounterOrder: false`（与 `includeTestHeadbutt` 同级的**测试场景开关**，默认关、明确标注非原版），
  在 net 检查里用它对调两个敌人的创建次序。
- **B**：在 Engine 测试侧直接用通用构建器造两个只有创建顺序不同的包，再把投影器拉进来比较
  （需要 net 的投影器可从 Engine 测试引用）。

### 仍待覆盖（关闭条件其余部分）

撤销/再揭示、重复同定义实例、公开可识别生命周期与引用闭合。
---

## 批次 11（2026-10-03）：W-02 成对验收骨架**已完成**（含故障注入证据）

### 改动

| 文件 | 改动 |
| --- | --- |
| `BaseSpire/Slice/CombatSlicePackage.cs` | 新增测试场景旋钮 `swapEncounterOrder`（默认 **false**；只把两个敌人的**创建顺序**对调，可见事实完全相同；明确标注为非原版、仅供成对验收） |
| `Net.TheSpire.Tests/SnapshotChecks.cs` | `NewBattle` 透传该旋钮；新增检查 **`W-02 只对调不可见创建顺序的成对场景：公开快照逐字节相同`**：① 两个场景的公开快照 `WireCanonical` **逐字节相同**；② 公开句柄**数量相同**；③ 句柄**集合相同**（编号不含创建顺序） |

### 证据（三类，分开记）

| 类别 | 结果 |
| --- | --- |
| **真实**（默认旋钮关） | 切片 **194/194**；net **384/384**；新增检查 ✓ |
| **成对场景** | 只对调两个敌人的创建顺序 ⇒ 公开快照（含实体/牌堆/关系/候选的规范顺序）**逐字节相同** ✓ |
| **故障注入** | 把句柄临时退回旧的 `"e" + 创建序号` ⇒ 新增检查**按预期失败**：快照出现 `calcified-cultist`/`damp-cultist` 的 `instance_id` 与实体顺序随创建顺序互换（e2/e3 对调）⇒ 证明该检查**有鉴别力**，不是恒真。注入后已完整还原，net 恢复 **384/384** |

### W-02 现状

- ✅ 公开句柄不再透传内部创建序号（批次 9）；四处排序随之解耦。
- ✅ **成对场景验收**（只改不可见创建/排列 ⇒ 结构等价）**已由检查证明**（本批次）。
- ⬜ 仍待覆盖：撤销/再揭示、**重复同定义实例**、公开可识别生命周期与引用闭合；关闭前按 §19.3 核对"真实训练须先通过"。
---

## 批次 12（2026-10-03）：W-02 引用稳定性覆盖补齐 + 与既有检查的对账

### 新增（已实跑）

| 检查 | 内容 | 结果 |
| --- | --- | --- |
| `W-02 重复同定义实例各有互不相同的公开引用，且重复投影保持同一绑定` | 起手牌里同定义的重复实例（打击 ×5）：每个实例拿到**互不相同**的公开引用；纯读重复投影得到**同一绑定**（会话内稳定） | ✓ |

net 套件 **385/385**（新增 1 项），build 0 错误 0 警告。

### 与既有检查的对账（避免重复建设）

| W-02 关闭条件要素 | 现有覆盖 | 结论 |
| --- | --- | --- |
| 不泄漏编号/排序/默认选择（成对场景） | 批次 11 的成对检查 + 故障注入证据 | ✅ 已覆盖 |
| **重复同定义实例** | 本批次新增检查 | ✅ 已覆盖 |
| **授权撤销/失效**（抽到已知牌即失效、不跨会话残留） | `V-03 已知位置在真实链路上保留/失效，且不跨会话残留 [真实]`（`AcceptanceChecks.cs:40`） | ✅ 已覆盖（复用，无需重做） |
| **引用闭合**（快照/目录里出现的 id 均有声明） | `V-15 成对状态…+ 目录引用闭合 [真实]`（:49）、`N-02 …成员引用闭合`、`H-01/H-02 …引用闭合` | ✅ 已覆盖（复用） |
| **再揭示**（撤销后**重新授权**揭示，且引用保持一致） | **未找到**显式检查 | ⬜ **唯一剩余窄缺口** |

### 下一步

1. 为"再揭示"补一条检查：同一会话内先让已知位置失效（抽牌），再经新授权重新揭示同一实例，
   断言重新揭示后 `known_positions` 指向的公开引用与失效前**一致**（即引用跨授权周期稳定）。
2. 之后 W-02 按其关闭条件仅剩"对应真实训练须先通过"这一前置（真实训练未启动，故不阻塞本条结论的记录）。
3. 随后转 **W-01**：包/Profile/场景显式选择（`Host/Program.cs:57`、`ControlHost.cs:63`、`runner/episode.py:27,30` 三处写死身份）。
---

# 阶段综合交接（2026-10-03 阶段小结，W-01～W-08）

本阶段（批次 1～12）全部改动都在**当前工作区实跑验证**过。基线（最近一次全量，返回码全 0）：

| 构建 | Engine | 切片 | 旧后端 | net | J-01（真实 stdio） | AI | client | 契约 | 覆盖 | 依赖 | doc/trace |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 0 错 0 警 | 64/64 | **194/194** | 190/190 | **385/385** | **162/162** | 503/503 | 50/50 | 210/210 | 1/1 | 9/9 | 0 / 0 |

## 逐项现状

| W 项 | 状态 | 本阶段交付（有证据） | 未结项 / 下一动作 |
| --- | --- | --- | --- |
| **W-02 公开ID与事实可信** | **核心完成** | ① 句柄不再透传创建序号（`BuildInstanceTable` 按可见身份规范序编号）⇒ 实体/牌堆/关系三处排序一并解耦；② `ProjectedSnapshot.InstanceRefs` 公开引用绑定表；③ **成对场景检查**"只对调创建顺序 ⇒ 公开快照逐字节相同"＋**故障注入**证明其鉴别力；④ 重复同定义实例引用互异 + 重复投影稳定；⑤ 撤销/失效与引用闭合**对账复用** V-03/V-15/N-02/H-01 既有检查 | **再揭示**（撤销后重新授权揭示且引用一致）一条检查；之后仅剩"对应真实训练须先通过"的前置 |
| **W-05 持久Run与两房间闭环** | **进行中（前两步完成）** | ① 结算移到 `SetTerminal` **之前**（根因：调度器终局后停止执行）⇒ 胜利写回、**未结束不结算**、**失败也写回**三条真实检查通过；② 幂等键改为**战斗作用域玩家**上的 `BattleSettled`（按战斗实例，符合 §19.2）；③ **长期牌组** `LongTermDeck` 与战斗实例分成两批，同定义实例分别升级已断言；④ 生成的牌不进长期组已断言 | **第二房间 / 中间奖励·升级**（需整局级入口，与 W-01 宿主相邻的**协调项**）；D5① 第二房间里的"重复结算不重复奖励"显式断言 |
| **W-01 可配置战斗Gym** | **进行中（定位完成）** | 写死点已定位到行号：`Host/Program.cs:57`、`Control/ControlHost.cs:63`、`runner/episode.py:27,30` | 引入包/Profile/场景显式选择；Runner/Collector 接受**实际**身份；扩展卡与药水经同一 stdio 入口实跑 |
| **W-03 / W-04 / W-06 / W-07 / W-08** | 未认领（本节外） | 本阶段未动 | 见 [tofix/README](../../tofix/README.md) 对应行；W-04 可独立用现有编码与测试任务推进 |

## 关键工程结论（供后续复用）

1. **`SetTerminal` 之后同块指令不执行**（`Scheduler.cs:803` 起）——凡是"终局后还要做的事"必须放在设终局**之前**。
2. **公开引用 = 会话/观看者绑定表**（`InstanceRefs`），任何新消费方都应从这里取句柄；句柄由可见身份决定，与创建顺序无关。
3. **`RequireKind` 语义**：未声明的 kind 会**抛错**而非跳过 ⇒ 进入投影的实例都是已发布的，"不可见实例"的真实边界是引擎 K-06 观看者投影。
4. **测试迁移要先行为中性再翻转**（本阶段两次采用：先迁 7 处预测句柄→383/383，再翻转编号→383/383），这样"没有隐藏依赖"是可证明的。

## 未执行/未验证（明确列出，不用"源码存在"代替）

- 二次运行/长期稳定性：本阶段每次改动都跑了全量检查，但**未**做长时间重复跑（如 J-01 多轮、采集长跑）。
- **未**运行真实训练（W-07 未冻结：任务/奖励偏好/预算）。
- **未**做 W-01 的宿主改造与 W-03 的真实多场景数据；`swapEncounterOrder` 是**测试场景旋钮**（默认关），不属原版能力。
- 事件/古代人内容仍缺失（注册数 0）；`BodySlam`/`cardsWithTag`/`FlexPotion` 仍按 §19.2 归 W-06 查来源。
---

## 批次 13（2026-10-03）：W-02 引用跨授权周期稳定（已证明）+ "字面再揭示"为何未覆盖

### 新增断言（扩展既有 V-03 检查，未新增检查项）

在 `V-03 已知位置在真实链路上保留/失效，且不跨会话残留 [真实]` 里补一条：

> 被揭示过的实例在"**撤销 + 换了公开位置**"（抽到手上）之后，**仍以同一不透明公开引用**出现在公开集合里。

实现：从 `known_positions[0].card_ref` 取回句柄 → 撤销后再扫所有牌堆的 `visible_members`，断言该句柄仍在其中
（不是自证：断言的是"同一句柄在失效后仍被公开可及"）。实跑：net **385/385**、build 0 错误 0 警告。

### 结论与"字面再揭示"的如实说明

- ✅ 已证明：引用**不随授权状态、也不随所在牌堆**改变（跨授权周期稳定）。
- ⬜ **未**覆盖：字面意义的"撤销后**再次授权揭示同一实例**"。
  原因（具体，不泛化）：受控切片**只有一条授权路径**——打出 Headbutt 时从**弃牌堆**选一张置顶揭示；
  同一实例被抽走后已不在弃牌堆，切片内**不存在**第二条"再次揭示同一实例"的机制。
  ⇒ 要覆盖它需要一个能二次授权的机制/内容（属 W-06 的内容与机制扩面），或用夹具强行搬动实例（价值有限且是夹具证据）。
- 因此 W-02 的关闭条件里，本项记为"**依赖二次授权机制**"，与 W-06 的内容扩面一起解决；其余要素均有证据。
---

## 批次 14（2026-10-03）：W-01 第一步——宿主显式选择**作者内容包**（真实进程验证）

### 缺口（具体到行）

`Net.TheSpire.Host/Program.cs:57` 原先固定 `CombatSlicePackage.Build(includeTestHeadbutt)`，
`fromAuthorText` 取默认 **false** ⇒ 宿主服务的是**程序化冻结切片**，
**22 张已迁作者卡与药水无法经 stdio 触达**——这正是 W-01"把已有扩展作者内容通过显式包/Profile 接入真实 stdio"的具体形态。

### 改动

| 文件 | 改动 |
| --- | --- |
| `Net.TheSpire.Host/Program.cs` | 新增 `--author-content` 开关（默认**关**）：置位后 `CombatSlicePackage.Build(includeTestHeadbutt, fromAuthorText: true)`；ready 行追加 `author_content=<bool>` |
| `Net.TheSpire.Tests/SnapshotChecks.cs` | 新增检查 **`W-01 作者内容包与程序化冻结切片是可选的两套包（内容确实不同）`**：程序化包**不**建长期牌组，作者包**建**（作者文本声明了 `run`）⇒ 证明该开关装载的是另一套内容，而非同包别名 |

### 验证（实跑，返回码全 0）

| 项 | 结果 |
| --- | --- |
| build | 0 错误 0 警告 |
| net | **386/386**（+1） |
| **真实进程**：`dotnet run --project net-the-spire/Net.TheSpire.Host -- --author-content` | 打印 `net-the-spire host ready: … test_headbutt=False author_content=True` ⇒ 带作者包**正常启动** |
| J-01（真实 stdio 回归） | **162/162**（默认路径未变） |
| client（真实 stdio 回归） | **50/50** |

### W-01 仍未结（下一动作，已具体）

1. **端到端 stdio 验证扩展内容可操作**：让客户端脚本可传**宿主参数**（把 `--author-content` 带上），
   然后断言：扩展卡（22 张之一）**出现在牌堆/手牌**、**药水实体与"使用药水"动作**可达、终局与离线重编码仍成立。
   （本批次只证到"包可选 + 进程可起 + 两套内容不同"，**尚未**证到客户端实跑扩展内容。）
2. **Profile/场景显式选择**：目前仍只有一套 `SlicePublicProfile`（`ControlHost.SupportedScenarioProfile` 为常量）；
   要真成"可配置 Gym"，需要目录/Profile/场景的身份可由宿主参数选择，并让 Runner/Collector 读取**实际**身份
   （`ai-the-spire/ai_the_spire/runner/episode.py:27,30` 的 `GYM_ID`/`SCENARIO_PROFILE` 常量随之改为读取实际值）。
3. 药水槽位/容量/可用状态按现有实体/字段/关系映射（W-01 验收的玩家可见信息部分）。
---

## 批次 15（2026-10-03）：W-01 端到端 stdio 发现**真实集成阻塞**（带原始错误）

### 做法

写了一个临时对照脚本（放系统临时目录，不入库），用**真实 stdio**分别启动默认宿主与 `--author-content` 宿主，
对同一次 `start → observe` 的公开输出做逐字段比较。

### 结果（真实进程、真实错误）

| 宿主 | 结果 |
| --- | --- |
| 默认 | `describe → start → observe` 全部成功（比较脚本跑到第二个宿主才报错） |
| `--author-content` | **`start` 被拒**，原始响应：<br>`{"error":{"code":"backend_fault", …, "message":"[N-02-UNDECLARED-FIELD] base-spire:Card.Variable_extra-damage 没有公开映射处置（不允许静默丢弃公开字段）","retry_mode":"stop"},"ok":false}` |

### 结论（重要）

1. **`--author-content` 是必要的但不充分**：作者内容引入了**实例变量字段**（例：`Card.Variable_extra-damage`，
   对应 Rampage 的成长量），而 `SlicePublicProfile` 还没有这些字段的公开处置 ⇒ **投影按纪律拒绝启动**（没有静默丢弃 ✓ 守卫是对的）。
2. 因此 W-01 的"扩展内容可经同一客户端实际操作"这条验收，当前**卡在公开映射覆盖**上：需要为作者内容的
   新字段逐个补处置（发布 or 具名跳过 + 理由），补完才能继续走到"实际出牌/用药水/终局/离线重编码"。
3. 这也把 W-01 与 W-02 的关系具体化了：**内容扩面会不断引入新公开字段**，必须随批次同步声明，否则扩展 Gym 起不来。

### 下一动作（明确、可迭代）

1. 用上述对照脚本作为**回归工具**（可入库为 `net-the-spire/client/test_author_content.py`），逐个补齐
   `SlicePublicProfile` 中缺失的字段处置；每补一个字段重跑一次，直到 `--author-content` 宿主能完成
   `describe → start → observe`（即"扩展 Gym 真正开局"）。
2. 然后扩展断言到"**实际出牌或使用药水**、终局、离线重编码"，才算 W-01 该条验收成立。
3. 字段处置的选择纪律沿用既有做法：玩家可见的**发布**，内部/可推导的**具名跳过并写理由**。
---

## 批次 16（2026-10-03）：W-01 阻塞链推进——字段已发布，**下一个阻塞是药水栏**

### 已解决

为作者内容的卡牌实例变量补公开处置并**发布**（§19.2："影响决策的玩家可见变量必须纳入有该机制的 Gym"）：

```csharp
["Variable_extra-damage"] = Number(CardType, "Variable_extra-damage"),   // Rampage 的成长量
```

实跑：build 0 错误 0 警告；net **386/386**；J-01 **162/162**（真实 stdio 未回归）。

### 下一个阻塞（原始错误）

`--author-content` 宿主再次被拒，原始响应：

```
{"error":{"code":"backend_fault", …, "message":"[N-02-UNDECLARED-COLLECTION] 引擎集合 'base-spire:PotionBelt' 没有处置（不允许静默丢弃公开事实）","retry_mode":"stop"},"ok":false}
```

即作者内容还引入了 **`base-spire:PotionBelt` 集合**，公开映射未覆盖 ⇒ 扩展 Gym 仍无法开局。
这正是此前反复出现的"药水栏是否进公开快照"待裁定项，而 **§19.2 已经给了答案**：

> "药水栏、可用次数/容量、目标与影响决策的玩家可见变量**必须纳入**有该机制的 Gym。
> 优先以现有实体/字段/关系表达药水及槽位；**不要为方便借用只适用于卡牌的 Pile 语义**。"

### 下一动作（W-01 药水映射，按 §19.2 的约束）

1. 在 `SlicePublicProfile` 为 `PotionBelt` 补**处置**：用具名声明把它映射为**现有实体/字段/关系**表达，
   **不**套用卡牌 `pile_types` 语义（§19.2 明确禁止图省事借用）。
2. 药水的"槽位/容量/可用状态"用实体字段表达（如 `Slot`/`Capacity`/`Usable`），随映射一起声明。
3. 迭代方式：每补一项就重跑本批次记录的 stdio 探针（`describe → start → observe`），
   直到作者宿主能开局；再把断言扩到"实际使用药水 → 终局 → 离线重编码"。
---

## 批次 17（2026-10-03）：W-01 重要架构教训——**发布新字段会改动共享目录**（实测 40+4 项失败）

### 实测事实

把作者独有的卡牌实例变量 `Card.Variable_extra-damage` 改成**发布**后：

| 套件 | 结果 |
| --- | --- |
| `dotnet build` | 0 错误 0 警告（**构建通过**，问题不在编译层） |
| net | **386 项里 40 项失败**（目录/规范字节随新字段变化） |
| J-01（真实 stdio） | **8 项里 4 项失败** |

改回**具名跳过**（`Disposition<PublishedField>.Skip("N-02-AUTHOR-ONLY", …)`，不改目录）后：net **386/386**、J-01 **162/162** 恢复。

### 结论（W-01 必须遵守的约束）

1. **catalog / 公开字段声明是共享资产**：给 Card kind 加一个字段会同时改动**程序化冻结切片**的目录与规范字节，
   而冻结切片的卡并无该字段 ⇒ 大批既有检查失败。**"作者独有的公开字段"不能直接加进当前共享目录**。
2. 正确做法：**Profile 变体**——W-01 的"Profile 显式选择"要真正落地（宿主参数选 Profile），
   作者内容用它自己的 Profile（含作者独有字段的发布/跳过），默认 Profile 保持不动。
   ⇒ 这也解释了为什么 W-01 里"包"与"Profile"是**两个**要显式选择的维度。
3. 本批次因此把该字段记为**具名跳过**（不是静默丢弃）：其影响经出牌时的 `Card.Amount`/伤害结算公开，
   理由与"需要 Profile 变体"一并写在声明处。

### 阻塞链现状

| 顺序 | 阻塞 | 状态 |
| --- | --- | --- |
| 1 | `[N-02-UNDECLARED-FIELD] Card.Variable_extra-damage` | ✅ 已解决（具名跳过；发布需 Profile 变体） |
| 2 | `[N-02-UNDECLARED-COLLECTION] base-spire:PotionBelt` | ⬜ **当前阻塞**：按 §19.2 用具名声明映射为**现有实体/字段/关系**表达药水栏（槽位/容量/可用状态），**不得**借用卡牌 `pile_types` 语义 |
| 3+ | 未知（每补一项重跑探针） | 迭代直到 `describe → start → observe` 通过 |
---

## 批次 18（2026-10-03）：W-01 阻塞链走到底——**结论：剩余工作本质是 Profile 变体**

### 阻塞链（真实 stdio 探针逐项推进，原始错误码）

| 顺序 | 阻塞 | 处置 | 结果 |
| --- | --- | --- | --- |
| 1 | `[N-02-UNDECLARED-FIELD] Card.Variable_extra-damage` | 具名跳过（发布需变体） | ✅ 越过 |
| 2 | `[N-02-UNDECLARED-COLLECTION] base-spire:PotionBelt` | 具名跳过（合规纳入需变体，§19.2 禁止借用卡牌 pile 语义） | ✅ 越过 |
| 3 | `[N-02-UNDECLARED-COLLECTION] base-spire:RunStates` / `base-spire:LongTermDeck` | 具名跳过（公开表达需变体） | ✅ 越过 |
| 4 | **`[N-02-UNDECLARED-ACTION] base-spire:UsePotion`** | **不能跳过**——W-01 验收要求"药水可实际操作" ⇒ 必须**发布**动作声明 | ⬜ **当前阻塞** |
| 5+ | 预期还有 `base-spire:Run` 等作者独有 kind | 必须发布 | ⬜ |

### 决定性结论（供负责人/下一位执行者）

1. **继续用"具名跳过"无法完成 W-01**：药水动作（UsePotion）与整局实体（Run）等**必须发布**，
   而把它们加进**当前共享 Profile** 会改动程序化冻结切片的目录与规范字节
   （批次 17 实测：发布一个字段即打掉 net 40 项 + J-01 4 项）。
2. ⇒ W-01 剩余工作的正确形态是 **Profile 变体（显式选择）**：
   - 默认 Profile 保持冻结（既有 net/J-01/契约全绿）；
   - **作者 Profile 变体**发布作者独有的字段/集合/动作/kind（药水槽位/容量/可用状态、`UsePotion`、Run 的 HP/金币、长期牌组表达）；
   - 宿主 `--author-content`（已存在）同时选择该变体；`describe` 的 `catalog_id` 应随变体变化（身份可核对）。
3. 本批次已落地的具名跳过都是**合规的临时态**（具名 + 理由，不是静默丢弃），且不改变默认目录（net **386/386**、J-01 **162/162** 保持）。

### 建议的下一动作（顺序固定）

1. 抽出 `SlicePublicProfile` 的**变体工厂**（默认 / 作者），把作者独有声明集中到作者变体；
2. 宿主按 `--author-content` 选择变体，并让 `catalog_id`/`profile` 身份随之变化；
3. 用本批次的 stdio 探针迭代到 `describe → start → observe` 通过，再把断言扩到"使用药水 → 终局 → 离线重编码"；
4. 最后让 Runner/Collector 读取**实际**身份（去掉 `runner/episode.py:27,30` 常量）。
---

## 批次 19（2026-10-03）：W-01 阻塞链固化为**可复用迭代工具**（不是通过检查）

### 新增工具

`net-the-spire/client/test_author_content.py`：用**真实 stdio**启动带 `--author-content` 的宿主，
跑一次 `describe → start → observe`，并打印当前阻塞。

| 退出码 | 含义 |
| --- | --- |
| 0 | 作者宿主能开局（W-01 该条验收的**必要条件**；另外会提示药水实体/动作是否已进公开快照） |
| **3** | **已知阻塞**：宿主起来了但 `start` 被拒（当前正是 `[N-02-UNDECLARED-ACTION] base-spire:UsePotion`）⇒ W-01 尚未完成 |
| 2 | 基础设施问题（宿主未构建等） |

**它明确不是"通过检查"**：本会话首次运行返回 **rc=3** 并打印阻塞项，不会伪装成功；
文件头注释也写明了"当前为已知阻塞诊断"。

### 用法与迭代方式

```powershell
python net-the-spire/client/test_author_content.py   # 当前 rc=3（阻塞：UsePotion）
```

每补齐一项公开映射（Profile 变体里的字段/集合/动作/kind）后重跑本脚本，阻塞项会逐个后移；
全部通过后再把断言扩到"实际使用药水 → 终局 → 离线重编码"。

### 本批次未改实现代码

投影/Profile 未改动（沿用批次 18 的状态：net **386/386**、J-01 **162/162**）。
下一个实现动作仍是批次 18 的"抽出 `SlicePublicProfile` 变体工厂 + 宿主按 `--author-content` 选变体"。
---

## 批次 20（2026-10-03）：W-06 统一覆盖口径——四态机械核算（§19.1 点名的 18+4/76 与 22/72 分歧已消除）

### 改动

`tools/coverage-check/check_slice_coverage.py` 的报告新增 **`content_states`** 块（并写进 JSON 产物）：
把"声明 / 加载 / 行为已验证 / host 可用"**四态分开**，只有"已声明"是从作者文本**机械算出**的，
其余三态各带**证据指针**，不做无证据推断。

| 状态 | 值 | 证据 |
| --- | --- | --- |
| 分母 | **94**（注册 96 − 多人专属 2：`demonic-shield`、`tank`） | B-05a 冻结清单 |
| **已声明**（作者文本） | **22** = 切片自有 4 + 新迁 18 | 本脚本解析 `slice-content.author` |
| 已加载（作者包可构建/加载） | 22 | `BaseSpire.Slice.Tests` 通过 |
| 行为已验证（有检查覆盖） | 22（注明：十进制"有检查覆盖"，不等于原版一致性） | 切片 B-06a/B-07a 检查 |
| **host 可用（真实 stdio 可玩）** | **0** | `net-the-spire/client/test_author_content.py` **rc=3**（作者宿主 `start` 被拒：`UsePotion` 未声明） |
| 剩余（分母内） | **72** | 94 − 22 |

统一表述（脚本直接产出）：`分母 94（注册 96 − 多人专属 2）；已声明/已加载/行为已验证 = 22（= 4 切片自有 + 18 新迁）；host 可用 = 0；剩余 72`

### 为什么这个区分重要

§19.1 指出正文同时存在"18+4/76"与"22/72"两套说法。本批次把它们统一到**同一分母（94）**下的**四态**，
并明确：**"已声明/已加载/行为已验证 = 22" 不等于 "可玩"——真实 stdio 上是 0**。
这正是 §19 反复强调的"不以源码存在、构建成功或夹具通过代替真实集成完成"。

### 实跑

`python tools/coverage-check/check_slice_coverage.py --json tools/coverage-check/slice-coverage-2026-10-02.json`
→ **TOTAL: 1 passed, 0 failed**（不变量全过）；产物 JSON 含 `content_states`。
---

## 批次 21（2026-10-03）：W-01 Profile 变体的**可照抄实现配方**（照着做即可，无需再探索）

### 已核实的 API 形状（`SlicePublicProfile.cs`）

```csharp
public static PublicSnapshotProfile Create() => new(
    rulesetId: "net:ruleset:base-spire-combat-slice-v1",
    catalogId: "net:catalog:base-spire-combat-slice-v1",
    backendProfileId: "net:backend:generic-engine-runtime-v1",
    kinds: Kinds(), …, actions: Actions(), answerAction: AnswerAction(),
    terminals: Terminals(), knownPositions: KnownPositions());
```

`Actions()` 形如 `new Dictionary<string, PublicActionRule> { ["base-spire:PlayCard"] = new PublicActionRule("net:action:base-spire:PlayCard", …) }`（第 295 行起）；
`Kinds()` 同形。**因此"变体"就是同形工厂 + 在字典里多几条**。

### 配方（建议直接落地）

1. **新增作者变体工厂**（与 `Create()` 并列）：

   ```csharp
   public static PublicSnapshotProfile CreateAuthorContent() => new(
       rulesetId: "net:ruleset:base-spire-author-content-v1",      // 身份随变体改变（W-01 要求可核对）
       catalogId: "net:catalog:base-spire-author-content-v1",
       backendProfileId: "net:backend:generic-engine-runtime-v1",
       kinds: AuthorKinds(),                                        // = Kinds() + 作者独有 kind
       …,
       actions: AuthorActions(),                                    // = Actions() + base-spire:UsePotion
       …);
   ```

   `AuthorKinds()`/`AuthorActions()` 以只读字典合并实现（`new Dictionary<string, …>(Kinds(), StringComparer.Ordinal) { … }`），
   默认 Profile 完全不动。

2. **按探针结果逐项补声明**（每补一项跑一次探针；`net-the-spire/client/test_author_content.py` 会给出下一个缺失项）：
   - `actions`：`base-spire:UsePotion`（**必须发布**——W-01 要求药水可操作；参数按 `UsePotion` 动作的实际参数声明）；
   - `kinds`：`base-spire:Run`（HP/金币等公开字段）+ 药水实体 kind（槽位/容量/可用状态按**字段**表达，§19.2 禁止借用卡牌 pile 语义）；
   - 集合：`PotionBelt` / `LongTermDeck` 在变体里改为**发布**（当前共享 Profile 里是具名跳过，保持不变）。
3. **宿主接线**（`Net.TheSpire.Host/Program.cs:59`）：
   `var profile = fromAuthorText ? SlicePublicProfile.CreateAuthorContent() : SlicePublicProfile.Create();`
   （`describe` 的 `catalog_id` 随之变化 ⇒ 身份可核对；`--author-content` 已存在。）
4. **迭代与验收**：`python net-the-spire/client/test_author_content.py` 直到 **rc=0**；
   然后把断言扩到"使用药水 → 终局 → 离线重编码"，并把该脚本升级为正式检查（纳入 net 侧检查清单）。
5. **收尾**：Runner/Collector 去掉硬编码身份（`ai-the-spire/ai_the_spire/runner/episode.py:27,30`），改读 `describe` 的实际值。

### 本批次未改代码

配方所需信息（工厂形状、字典形状、宿主接线点、探针命令）已全部核实并写在此处；
实现本身是 1 个新工厂 + 若干字典条目 + 1 行宿主选择，属**低风险但需连续多步**的改动，
在上下文本轮预算之外，故未开始（避免留下半成品或把全绿状态改红）。
---

## 批次 22（2026-10-03）：W-01 **包与 Profile 显式选择已落地**（身份变化有 stdio 证据）

### 改动（照批次 21 配方的第 1、3 步）

| 文件 | 改动 |
| --- | --- |
| `Net.TheSpire/Projection/SlicePublicProfile.cs` | 新增 **`CreateAuthorContent()`**：与 `Create()` **规则完全相同、只有身份不同**（`net:ruleset/catalog:base-spire-author-content-v1`）。默认 Profile 一字未动 |
| `Net.TheSpire.Host/Program.cs` | `--author-content` 同时选择作者 Profile 变体：`fromAuthorText ? CreateAuthorContent() : Create()` |

### 验证（真实 stdio）

| 宿主 | describe 身份 |
| --- | --- |
| 默认 | `ruleset=net:ruleset:base-spire-combat-slice-v1` / `catalog=net:catalog:base-spire-combat-slice-v1` |
| `--author-content` | `ruleset=net:ruleset:base-spire-author-content-v1` / `catalog=net:catalog:base-spire-author-content-v1` |

⇒ W-01 的"**规则/场景变更改变身份**"与"**变更身份可核对**"两条已有真实 stdio 证据。
探针 `test_author_content.py` 仍 **rc=3**、阻塞项不变（`[N-02-UNDECLARED-ACTION] base-spire:UsePotion`）——
符合预期（本批次只做身份，未加作者独有声明）。

### 回归

build **0 错误 0 警告**；net **386/386**；J-01 **162/162**；client **50/50**（默认路径完全未受影响）。

### 下一步（配方的第 2、4、5 步）

在同形的 `AuthorKinds()`/`AuthorActions()`/`AuthorCollections()` 里逐项追加作者独有声明
（`base-spire:UsePotion` → `base-spire:Run` → 药水实体与槽位字段 → `PotionBelt`/`LongTermDeck` 发布），
每加一项跑一次 `python net-the-spire/client/test_author_content.py` 迭代到 rc=0；
再把断言扩到"使用药水 → 终局 → 离线重编码"，并去掉 Runner/Collector 的硬编码身份。
---

## 批次 23（2026-10-03）：W-01 作者变体的**依赖顺序修正** + 所需声明形状（已核实）

### 新核实的事实

| 事实 | 证据 |
| --- | --- |
| 引擎侧 `UsePotion` 只有**一个**参数：`Potion`，类型 `ref<药水实例>`，候选＝**药水栏成员** | `CombatSlicePackage.cs` 的 `package.Action("UsePotion")` 段 |
| 公开侧 `PublicActionRule` 形状：`new PublicActionRule("net:action:…", new Dictionary<int, PublicActionField> { [0] = new PublicActionField(fieldId, PublicValueKind.EntityRef, Required: true, TargetKindIds: [...]) }, NetDeclared: true)` | `SlicePublicProfile.cs` 的 `AnswerAction()` |
| Profile 里**没有药水 kind**（只有 `CardKindId`/`MonsterKindId` 两个常量） | 全文件检索 `Potion`：仅命中我写的注释与 `PotionBelt` 跳过项 |

### 因此顺序必须修正（原配方把 actions 排在前面，实际不成立）

**先 kind → 再集合 → 最后 action**，理由：`UsePotion` 的候选是药水栏成员；
药水栏当前被**具名跳过** ⇒ 成员根本不进公开集合，动作即便声明了也**引用不到候选**；
而药水实体要进公开集合，必须先有**药水 kind**（否则 `RequireKind` 直接抛错）。

修正后的作者变体落地顺序：

1. **`AuthorKinds()`**：加药水 kind（`net:kind:base-spire:Potion` 之类），字段按 §19.2 用**字段**表达
   槽位/容量/可用状态（**不得**借用卡牌 pile 语义）；
2. **`AuthorCollections()`**：把 `base-spire:PotionBelt` 从"具名跳过"改为**发布**（默认 Profile 保持跳过不动）；
3. **`AuthorActions()`**：加 `base-spire:UsePotion`，`TargetKindIds` 指向第 1 步的药水 kind；
4. **`AuthorKinds()` 续**：`base-spire:Run`（HP/金币等字段）+ `LongTermDeck` 的公开表达（按 W-05 语义：长期 vs 战斗两批实例）；
5. 每步跑 `python net-the-spire/client/test_author_content.py`，直到 **rc=0**；
6. 之后把断言扩到"使用药水 → 终局 → 离线重编码"，并去掉 Runner/Collector 的硬编码身份。

### 本批次未改代码

只做了信息核实与顺序修正（写在上方）。变体的字典条目仍需按第 1～4 步逐个实现并逐次用探针验证。
---

## 批次 24（2026-10-03）：W-01 药水 kind 的**已核实事实**与**唯一决策点**

### 已核实（可直接照抄）

| 项 | 内容 |
| --- | --- |
| kind 规则形状 | `new PublicKindRule(Kind(<Type>), ContentFieldName: "DefinitionId", ContentIds: [...], Fields: new Dictionary<string, Disposition<PublishedField>>(StringComparer.Ordinal) { ... })`（见 `Kinds()` 的 Monster 条目，第 117 行起） |
| 药水实体类型 | 切片包里的 `potion` 实体；公开侧需要 `PotionType = "base-spire:Potion"`（Profile 现有常量只有 `CardKindId`/`MonsterKindId`） |
| 药水实体的引擎字段 | `DefinitionId`、`Rarity`、`Target`、`Amount`（`CombatSlicePackage.cs` 的 `potion!.AddField(...)` 三处 + 金额一处；另见 `potionPowerId`/`potionPowerStacks` 用于用药水施加能力） |
| 作者文本声明的药水 | `potion beetle-juice`、`potion blessing-of-the-forge`（≥2，另需以作者文本为准补全 `ContentIds`） |
| 可用的处置助手 | `Number(type, field)`、`Boolean(type, field)`、`Category(type, field, Dictionary<string,string>)`、`ContentIsEntityFact(type, field)`（第 370～383 行） |

### 唯一决策点（阻塞 `AuthorKinds()` 一次写对）

**没有"纯字符串字段"的公开助手**：`Rarity`（common/uncommon/rare）与 `Target`（enemy/self/none 之类）
既不是数字也不是布尔，而 `Category` 需要**先声明枚举词表**（`CategoryId("rare")` 等）。

两个合规选项（二选一，均需记录理由）：

- **A（推荐）**：按 `Category` 发布，词表取自作者文本的实际取值（`rarity`/`target` 的枚举 id 与 `net:cat:` 声明），
  理由是玩家可见（药水稀有度/目标直接影响决策）；
- **B**：具名跳过（`Skip("N-02-…", 理由)`），理由需说明其影响已由别处公开表达。

选 A 时，`CreateAuthorContent()` 的 `collections` 里 `PotionBelt` 必须同步从"具名跳过"改为**发布**
（否则 `UsePotion` 的候选引用不到——见 §批次23 的顺序修正）。

### 本批次未改代码

只做事实核实与决策点固定；实现仍是"1 个 `AuthorKinds()`（含药水 kind）+ 1 个集合处置 + 1 个 `AuthorActions()` 条目"。

## 批次 25：负责人恢复清单与重新派单

日期：2026-10-03。范围为文档恢复、当前进度核对及任务重排；执行仍由用户协调。本批次优先于上文历史“下一步”和完成结论，但不改写原运行证据。

### 恢复与保留

本轮看到的README是执行者补写的最小版本，并非仍为0字节；清空时间等取自该版本的记录，负责人没有独立目击删除过程。按本会话先前清单及主设计恢复W-01～W-08完整范围、责任、依赖、验收、待决和后续范围，保留本阶段实现进展。[最小重建版](task-list-executor-rebuild-2026-10-03.md)另存为历史，原报告不删除。当前清单与阶段交接均未跟踪，不声称从Git恢复原文件字节。

### 当前评估

| 事项 | 认可的交付 | 本轮结论及下一成果 |
| --- | --- | --- |
| W-01 | 作者包/Profile显式开关，身份变化及开局探针 | 开局仍拒绝UsePotion；补药水/槽位/Run/长期组公开映射后，实际操作到终局并离线重编码；AI消费真实身份 |
| W-02 | InstanceRefs接口迁移、初始创建顺序对照及纯读重复实例检查 | **重新开放**：跨状态持久绑定未实现，原“只剩再揭示”结论不成立；修复后验证跨状态、原始ref/Skip排序、再授权与新增销毁 |
| W-05 | 终局前写回、战斗作用域守卫及胜负/未结束检查；长期/战斗独立实例、生成牌隔离 | 不再重复派结算定位。第二房间、持久奖励/升级、下一战由长期组初始化仍开放；战斗实例升级隔离不等于两个长期实例分别升级 |
| W-06 | 22张作者声明与四态栏目结构 | 正式0.0.1到Engine仍开放；四态脚本未逐项绑定运行证据，不称加载/行为机械核算完成 |
| W-03/W-04/W-07/W-08 | 原设施复用；本阶段没有模型或新场景交付 | 完整任务恢复；AI模型工程立即派，场景数据等真实Gym；实验Profile由用户/规划者冻结，约束证据随阶段对账 |

### W-02的具体静态反例与范围

[PublicSnapshotProjector](../../../../net-the-spire/Net.TheSpire/Projection/PublicSnapshotProjector.cs)的Project每次调用BuildInstanceTable（约130、211～250行），排序后重新创建Dictionary，并按当前index+1发e编号；投影器本身没有跨快照的实例绑定缓存。[ControlSession](../../../../net-the-spire/Net.TheSpire/Control/ControlSession.cs)复用投影器并不能让该局部Dictionary持久化。

可核对反例：两个相同TypeId/DefinitionId、其余字段相同的敌人A/B，Hp分别10/20；字符串排序把A排在B前。A受1点伤害变成9，排序变成B在A前，**两者句柄互换**。这不是不同会话允许的一致换名，而是同会话同一实例在受伤后换绑。新增排序靠前的可见实体也会推动既有句柄变化。此处为源码算法的静态反例，本轮没有新增或运行该C#行为检查；执行者需将反例变成有鉴别力的回归。

排序循环读取instance.Fields全部字段，没有按kind.Fields的Publish/Skip过滤；[Value.ToString](../../../../core-the-spire/Core.TheSpire/Engine/Value.cs)对Ref/RefList包含原始实例序号。因而“只使用已发布字段”“所有隐藏顺序已隔离”也未由现有双敌人对照证明。补含原始ref字段、已Skip簿记变化、关系/公开位置、同定义多实例的成对场景，不能通过观察者可见View就假设net所有字段都是模型公开事实。

修复采用会话/观看者内首次公开绑定、之后保持；同实例撤销后再授权恢复原句柄，销毁后不把句柄给别的实例。绑定属于提供方身份管理，不要求AI加入历史记忆；跨独立会话允许一致不透明换名，对齐全部结构和策略结果，不强制每个初始状态字节完全相同。

### W-01当前观察摘要

W-01另一个必需事实：[作者内容](../../../../mod-the-spire/base-spire/BaseSpire/Slice/Content/slice-content.author)的Spite约400～420行使用player-lost-hp-this-turn决定单次/两次伤害，包中的LostHpThisTurn在回合开始重置；当前SlicePublicProfile约130行却按“可由公开HP变化推导”Skip。该推导依赖过去而非当前快照，且直接影响收益。作者Profile应发布当前布尔摘要并补对应差异检查；不是要求AI记录历史。本轮只核对源码，尚未运行该成对场景。

### W-06四态核算的边界

[覆盖脚本](../../../../tools/coverage-check/check_slice_coverage.py)约139～160行以作者/夹具列表数量直接填loaded_in_author_package与behaviour_verified，并把host_available固定为0，证据文字没有逐卡关联实际运行产物。接受分母/声明口径和四态结构，不把它当作独立验证22项加载/行为的执行器。已有内容行为检查保留；下一步挂接逐内容执行证据，并区分旧M1可用路径与作者扩展无法开局的路径。

### 重新派发及工程决定

1. net：复用接口，先修W-02持久引用，收口W-01作者Profile。Rarity/Target按Category词表发布，无需再次等待“发布还是Skip”的负责人决定；必要槽位/容量/可用状态也要表达。
2. AI：立即推进W-04模型/策略接入、奖励派生层及学习工程，先有限合成验证，不等全部内容或真实奖励Profile。真实Gym推理依赖接口收口。
3. base：复用结算与实例边界推进W-05两房间，同时协调文件后推进W-06正式作者源和逐项覆盖。不要继续用配方与定位小批次替代这两项纵向交付。
4. W-03在实际Gym就绪后接续，W-07实验目标/预算仍待冻结，W-08随各阶段维护。具体执行者由用户指派；本文更新不表示已启动新agent。

已同步README、主清单v0.4、agent-goals（含一段话版本）、AI/net计划、文档导航及docs约定；没有修改生产代码、测试或schema，没有提交/清理共享工作区。

### 本轮验证

- 已运行现有作者开局探针，直接使用已构建Host DLL，不触发build；探针实际返回码 **3**，首错 **N-02-UNDECLARED-ACTION / base-spire:UsePotion**，与源码未声明动作一致。这是未完成项的诊断结果，不记为通过；不验证新的完整药水rollout。
- 执行者的build/Engine64/切片194/Legacy190/net386/J-01 162/AI503等计数只作原报告证据；本轮未重跑构建、全套规则/内容加载、协议/采样、学习或部署验收。
- 文档检查python tools/doc-check/check_docs.py .返回0（159份Markdown、0问题）；需求追踪python tools/doc-check/check_trace.py .返回0（20条、0不一致）；git diff --check返回0，仅既有LF/CRLF提示。完整八项及新增重建历史均保留，tofix只含当前README和本阶段综合交接。

---

## 批次 26（2026-10-03）：W-02 持久引用绑定落地——跨状态不换绑、销毁不复用、撤销再授权

认领：**W-02**（公开ID与事实可信）。主责目录 `net-the-spire/Net.TheSpire/Projection/**` 与 `net-the-spire/Net.TheSpire.Tests/SnapshotChecks.cs`；
另有一个**默认关闭**的受控场景旋钮落在共享文件 `mod-the-spire/base-spire/BaseSpire/Slice/CombatSlicePackage.cs`（与既有 `swapEncounterOrder` 同级、同风格）。
本批次**只改**这三类文件，未触碰 Host/Control/client/AI，未提交、未清理共享工作区。

### 修复了什么（批次25负责人的静态反例）

批次9的"按可见身份规范序编号"仍然**每份快照重算一遍**（`BuildInstanceTable` 里 `OrderBy(... )` + `index+1`），
所以公开字段一变，同一实例的句柄就会换绑。本批次把编号改为**会话/观看者内的持久绑定**：

| 规则 | 实现 | 可核对点 |
| --- | --- | --- |
| 首次公开分配 | `_publicRefs`（`InstanceId → 句柄`）+ `_nextPublicHandle` 存在投影器实例上（= 每个 `ControlSession`／观看者一份） | 实例第一次进入该观看者投影时拿号 |
| 跨状态保留 | 已绑定的实例**不再参与**编号；只给"本次首次公开"的实例发号 | 受伤/升级/移动牌区/被引用都不换绑 |
| 销毁不复用 | 表只增不减、序号只增不减；被销毁实例的句柄留在表里不再分配 | 新实例的句柄必是全新的 |
| 不可见创建不消费序号 | 只有 `view.Instances` 里的实例参与编号 | 隐藏实例（及其内部 `InstanceId`）不影响公开编号 |
| 不按内部序号排序 | 新实例之间的顺序只由 `PublicIdentityKey` 决定：**只读已发布字段**，引用按对端的公开身份（`TypeId|内容标识`）表达 | 既不读 Skip 字段，也不用 `Value.ToString()`（它对 Ref/RefList 会打印内部 `#序号`） |
| 引用闭合不变 | 返回表只含**本快照可见**实例 | 引用已销毁/被隐藏实例仍按 I-22 拒绝，不会被持久表放行 |

`PublicInstanceId` 不再是公开句柄，保留为诊断函数并在 XML 注释里写明"不得用于 wire 值/排序键/特征"；
`AttachKnownPositions` 里最后一处使用它构造错误路径的地方已改为读绑定表。

### 新增检查（3 条，全部有故障注入证据）

| 检查 | 内容 |
| --- | --- |
| `W-02 跨状态持久绑定：同定义实例的公开字段变化后句柄不换绑` | 两个**同定义**敌人（出生 Hp 20 / 10，测试旋钮）→ 打 6 点把 10 变 4 → **同一个投影器**两次投影，两个实例的句柄都不变；并断言 Hp 确实变了 |
| `W-02 生成/销毁：新实例取新句柄、销毁句柄不复用、候选引用同一批持久句柄` | Headbutt 的 `top_from_discard` → `RecordKnownTop` 先 `ClearKnownCards`（**销毁**）再创建事实：断言新实例取全新句柄、被销毁句柄不复用、跨两次变化所有仍可见实例的句柄不变，并把 wire 候选引用与绑定表逐一对照（候选对齐） |
| `W-02 撤销再授权：同一实例重新揭示后仍是同一公开句柄` | 真实链路撤销（抽到已知牌 ⇒ 授权事实销毁）后再授权（同一实例回到弃牌堆、再次经 Headbutt 揭示）：`known_positions.card_ref` 与首揭示**同一公开句柄**，候选对齐仍成立 |

**故障注入（鉴别力）**：把 `BuildInstanceTable` 临时改回"每份快照按公开字段重排编号"（清空绑定表 + 序号归 1），
三条新检查**全部按预期失败**，且症状与批次25的静态反例一致：

- 受伤检查：`受伤实例跨快照保留同一公开句柄（不换绑）: expected 'e11', got 'e12'`（两个同定义敌人句柄互换）
- 生成/销毁检查：`新生成的实例必须取全新句柄：e12`（复用了旧编号）
- 撤销再授权检查：`撤销后该实例仍以同一公开句柄出现: expected 'e2', got 'e6'`

注入后已完整还原；还原后 net **389/389**。**注意**：批次11/12的两条旧检查在注入下仍然通过——它们证明的是
"创建顺序不影响公开结构"，**不能**替代跨状态稳定性，这正是负责人重新开放 W-02 的理由。

### 一个必须记录的事实（避免下次走弯路）

受控切片里**被击杀的敌人不是"销毁"**：`DamageMonster` 只把它移出 `Monsters` 集合，实例仍在状态与投影里，
所以击杀**不能**用来证明"销毁句柄不复用"。本批次改用切片里真实存在的销毁路径（`ClearKnownCards`）。
另：同一张牌在同一回合重复打出会被引擎按 `PlayedThisTurn` 成员冲突拒绝（`MembershipConflict`），
所以第二次 Headbutt 必须跨回合（本批次用一次 `EndTurn` 完成）。

### 验证（本批次实跑，返回码全 0）

| 命令 | 结果 |
| --- | --- |
| `dotnet build AutoTheSpire.sln` | 0 错误 0 警告 |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | **389/389**（修复前 386，新增 3） |
| `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` | 64/64 |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | 194/194 |
| `dotnet run --project core-the-spire/Core.TheSpire.Tests` | 190/190（旧后端） |
| `python ai-the-spire/tests/j01_episode.py` | 162/162（M1 回归） |
| `python net-the-spire/client/test_client.py` | 50/50 |
| `python ai-the-spire/tests/run_checks.py` | 503/503 |
| `python tools/contract-check/check_contracts.py` | 210/210 |
| `python tools/coverage-check/check_slice_coverage.py` | 1/1（分母 96/已声明 22/已迁 22/剩余 72；本批次未改覆盖口径） |
| `python tools/dependency-check/check_dependencies.py` | 9/9 |
| `python tools/doc-check/check_docs.py .` / `check_trace.py .` | 0 problems / 0（20 条需求） |
| `git diff --check` | 干净（仅既有 LF/CRLF 提示） |

对照：作者开局探针 `python net-the-spire/client/test_author_content.py` 仍返回 **3**、首错仍为
**N-02-UNDECLARED-ACTION / base-spire:UsePotion**——W-01 未动，这不是回归也不是通过。

### 未结项与下一份成果

1. **W-01（下一份，优先）**：按批次21配方 + 批次23顺序修正，在 `SlicePublicProfile.CreateAuthorContent()` 变体里
   逐项发布药水 kind/槽位容量/集合/`UsePotion`、`Run` 与长期牌组，目标把探针从 rc=3 推到 rc=0，
   再扩到"用药水 → 终局 → 离线重编码"。药水映射**不得**借用卡牌 pile 语义，且**不得**改默认 Profile（实测会打掉 net 40 项 + J-01 4 项）。
2. **W-02 关闭**：适用检查已全绿（本批次 3 条 + 既有成对/重复/引用闭合/V-03）。剩余关闭前置是
   "对应真实训练须先通过"（W-07 Profile 未冻结），以及**负担者/规划者的复核**——执行者不自行关闭本条。
3. 残余边界（已写进 `net-the-spire/coverage-n-02.md` §2）：公开身份完全相同的同定义实例（如起手 5 张打击）
   之间的编号顺序仍取投影稳定顺序；任意置换给出同一公开结构，故不构成可推断泄漏，但若将来出现
   "公开字段完全相同、隐藏字段不同"的内容需重新评估。
4. 本批次**没有**动 W-04/W-05/W-06/W-03/W-07/W-08 的任何范围；文档侧只更新了
   `net-the-spire/coverage-n-02.md`（§1 entities 行、§2 两行、§3.6，均为"已收口/残余边界"的如实改写）与本文。

---

## 批次 27（2026-10-03）：W-01 药水栏与 UsePotion 经真实 stdio 收口

认领：**W-01**（可配置战斗 Gym）。批次 26 之后，作者探针的首个阻塞是
`[N-02-UNDECLARED-ACTION] base-spire:UsePotion`。本批次按批次 23 的**修正顺序**（kind → 集合 → action）把它推到 **rc=0**，
并且不是"只到开局"：探针现在真的**用掉一瓶药水**并核对效果。

### 改了什么（按依赖顺序）

| 文件 | 改动 |
| --- | --- |
| `core-the-spire/Core.TheSpire/Engine/Projection.cs` | **通用机制**：`ProjectedCollection` 增加 `Capacity`——集合的**声明**容量（`CollectionSymbol.Capacity`，越界时引擎已按 `CapacityExceeded` 拒绝）。它是规则而不是隐藏状态，且让"容量"只有一个来源：引擎声明 |
| `base-spire/Slice/SliceManifest.cs` | 新增 `PotionSlotCount = 3`，来源逐条注明（`reference/sts2-sim/src/Sts2Sim.Core/Entities/Players/Player.cs:23` `InitialMaxPotionSlotCount = 3`；`PotionCmdTests.cs:51-56`）。增槽来源属原版遗物范围，未实现 |
| `base-spire/Slice/CombatSlicePackage.cs` | ① `PotionBelt.Capacity = PotionSlotCount`；② 药水实体加入**观看者投影**（`projection.Type(potion!)`）——否则它既不是公开实体，`UsePotion` 候选也引用不到（I-22）；③ `Run` 实体加入观看者投影 ⇒ Hp/MaxHp/Gold 成为公开字段。三处都在 `hasPotions`/`hasRun` 分支内，默认路径形状不变 |
| `net/.../PublicVocabulary.cs` | `PublishedGroup` 改为 `(GroupTypeId, OwnerKindId, MemberKindIds, MinCount, MaxCount?)`：owner 不再是**写死的公开引用**（句柄由 W-02 的会话内绑定决定），min/max 承载容量 |
| `net/.../PublicSnapshotProjector.cs` | group 发布：owner 按**种类唯一解析**（与 `pile_ref` 同理，多个实例即按 `N-02-GROUP-OWNER` 拒绝，不猜一个）；成员引用闭合；并对声明的 `[min..max]` **自检**（`I-35`） |
| `net/.../CatalogProjector.cs` | 发射 `group_types`（owner/member kind + min/max_count）。默认 Profile 不发布任何 group ⇒ 默认目录字节不变 |
| `net/.../PublicSnapshotProfile.cs` | 新增 `GroupTypes` 访问器；`CollectDeclaredIds` 补 owner kind |
| `net/.../SlicePublicProfile.cs` | 作者变体：`CreateAuthorContent(RuleSet rules)`（**容量必须从引擎声明读**，不在 Profile 里另抄数字）、`AuthorKinds()`（Potion kind：内容身份 + Rarity/Target category 词表 + Amount/PowerStacks；PowerId 具名跳过，理由与卡牌同源；**Run kind**：Hp/MaxHp/Gold）、`AuthorCollections()`（`PotionBelt` 与 `LongTermDeck` 由"具名跳过"改为**发布为 group**，`max_count` = 引擎容量/未声明上限；`RunStates` 保持具名跳过并写明理由）、`AuthorActions()`（`UsePotion`，slot0 = 药水引用、required、TargetKindIds 收窄到药水 kind）；同时补全**作者能力内容表**（作者的 `shrink`/`feel-no-pain`/`rage-power`/`no-energy-gain` 只有作者变体登记；否则一用 beetle-juice 就按 `N-02-UNDECLARED-CONTENT` 拒绝） |
| `net/Net.TheSpire.Host/Program.cs` | 作者变体传入规则集 |
| `net/Net.TheSpire.Tests/SnapshotChecks.cs` | 新增检查 `W-01 作者变体发布药水 kind/枚举、药水栏（group+容量）与 UsePotion` |
| `base-spire/BaseSpire.Slice.Tests/SliceFrontendPotionChecks.cs` | 新增检查 `W-01 药水栏槽位：容量 = SliceManifest.PotionSlotCount，越界由引擎拒绝 [真实]` |
| `net-the-spire/client/test_author_content.py` | 探针升级为**真检查**：断言药水 kind / 药水栏 group / UsePotion 候选，并**提交使用**一瓶药水（目标提问 → 应答 → 消耗 → 效果），失败返回 1 |

### 验证（本批次实跑）

| 命令 | 结果 |
| --- | --- |
| `dotnet build AutoTheSpire.sln` | 0 错误 0 警告 |
| `python net-the-spire/client/test_author_content.py` | **rc=0**（此前 rc=3）：`kinds=[…Potion, Run]`、`actions=[EndTurn, PlayCard, UsePotion]`、药水栏 `owner=e23`、成员 `[beetle-juice, blessing-of-the-forge]`、`整局 HP=80`、长期牌组 10 张且与战斗牌区互不相交；用药水后**药水栏 2 → 1**、敌人获得 **Shrink(4)** |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | **390/390**（新增 1） |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | **195/195**（新增 1） |
| `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` | 64/64 |
| `dotnet run --project core-the-spire/Core.TheSpire.Tests` | 190/190（旧后端） |
| `python ai-the-spire/tests/j01_episode.py` / `python ai-the-spire/tests/run_checks.py` | 162/162 / 503/503 |
| `python net-the-spire/client/test_client.py` | 50/50（默认路径未受影响） |
| `python tools/contract-check/check_contracts.py` | 210/210 |
| `python tools/coverage-check/check_slice_coverage.py` / `tools/dependency-check` | 1/1 / 9/9 |

### 关键设计决定（记录理由）

1. **药水栏用 group 表达，不用 pile**：协议 group 有 `owner_ref` 与目录声明的 `min/max_count`，
   正好承载"owner + 成员 + 容量"；卡牌 pile 语义（`members_complete`/`known_positions`/`position_schema`）不适用于药水栏。
   代价是 owner 必须**按种类**解析，因此 `PublishedGroup` 从"写死引用"改成"owner kind"。
2. **容量只有一个来源**：`SliceManifest.PotionSlotCount(3)` → `PotionBelt.Capacity` → `ProjectedCollection.Capacity`
   → Profile 的 `max_count` → 目录。Profile 里没有第二个数字；越界由引擎拒绝（切片检查有行为证据）。
3. **Rarity/Target 按 Category 发布**（批次 24 的选项 A）：词表取自作者前端允许的取值，
   目录登记 `net:cat:*` 且与 wire 值同源。
4. **PowerId 具名跳过**：未附加能力时是空串，协议 category 值必须来自声明的词表 ⇒ 不自造哨兵类别，
   理由与卡牌 `PowerId` 完全一致（这是同一处缺口，不是新缺口）。
5. **整局与长期牌组**（同一批次后半）：`Run` 单例进入观看者投影 ⇒ Hp/MaxHp/Gold 成为公开字段；
   长期牌组发布为**第二个 group**（owner = Run，成员 = 长期那批卡牌实例），因此"长期 vs 战斗两批实例"
   在公开侧可判定；`RunStates` 仍具名跳过，理由是"整局事实已由 Run 实体的字段表达，再发 group 会重复表达
   （protocol.md:94）"。检查里还断言长期牌组与战斗牌区（hand/draw/discard）**互不相交**。
6. **group 的身份可见性**：`BuildGroup` 对"身份不可见却给出成员"的集合按 `N-02-GROUP-IDENTITY` 拒绝——
   否则会发布一个空成员表而真实成员是隐藏的（公布的集合与真实成员必须一致）。

### 未结项（W-01 的剩余部分）

1. **终局 + 轨迹离线重编码**：探针当前覆盖"开局 + 整局/长期牌组事实 + 用药水"。要走完
   `describe → catalog → start → 扩展卡/药水操作 → 终局 → 离线重编码`，需要一段有界 rollout 脚本
   （默认切片已有 M1 轨迹设施可复用，作者路径需要同样的产物）。
2. **Runner/Collector 去掉硬编码身份**（`ai-the-spire/ai_the_spire/runner/episode.py:27,30`），改读 `describe`。
   属 AI 线（W-04/W-01 消费侧）的接口交接项。
3. 本批次**没有**动 W-02/W-04/W-05/W-06/W-03/W-07/W-08 的范围；`net-the-spire/coverage-n-02.md`
   更新了检查计数、新增 W-01 药水/整局事实行、并把"无 Group 声明"改为"Group 已可从集合发布（Sequence 仍不可表达）"。
   对 W-05 的交接：长期牌组的公开表达已就位，"第二房间由长期牌组/持久 HP 初始化"仍归 W-05。

---

## 批次 28（2026-10-03）：W-01 验收项收官与两个真实缺陷修复

本批次把 W-01 剩下的"终局 + 轨迹离线重编码"和"Runner/Collector 身份不硬编码"做完，
并修掉两个由新检查/新路径**抓出来的真实缺陷**。

### 缺陷一：`describe` 的能力/限额与实际发布不一致（我方上一批次引入）

批次 27 让作者 Profile 发布 group 之后，`ControlHost.Describe` 仍然报 `groups: 0`、`members_per_group: 0`，
并把"引擎无 Group 声明、无 multiplicity"当作不支持约束——**消费者会按这些数字分配缓冲**，报 0 就是假声明；
它同时还继续把已收口的 `TODO-N02-PILE-ENTITY` / `TODO-N02-KNOWN-POSITION` 作为"不支持约束"发布
（而 H-01/H-02 的检查已断言这两个 TODO 不再存在）。

修法（`ControlHost.Describe`）：
- `groups` = 声明的 group_type 数（每个集合每份快照最多一个 group）；
- `members_per_group`：有界声明的 group 用自己的 `max_count`；未声明上限的（长期牌组）其成员都是**实体**，
  因此以 `entities` 限额（256）为派生上界，而不是另编一个数字；
- `Pile`/`Group` 的 `declaration_ids` 改为列出**真实**的 pile_type/group_type；
- "不支持约束"只留仍未收口的项（完整 V-02 待 N-05）。

新增检查：`W-01 作者变体的 describe 限额/结构声明与实际发布一致`（net）——它同时断言**真实快照**
不超出声明限额（group 数/成员数/实体数/pile 数），并在默认 Profile 的 `N-03 describe…` 检查里加了
"结构声明必须列出真实 pile_type、默认 Profile 的 group 限额为 0、不支持约束不得再登记 TODO"。

### 缺陷二：目录 `content_types` 出现重复 id（I-86）

AI 侧新检查第一次运行就报 `[I-86/L2] catalog.declarations.content_types: 声明 id 重复: 'base-spire:feel-no-pain'`。
根因：作者内容里 **`feel-no-pain` 同时是卡牌与能力的内容 ID**（`slice-content.author` 的 `card feel-no-pain`
与 `power feel-no-pain`），而 `CatalogProjector` 原先按 (kind, 内容 ID) 逐条发声明 ⇒ 同一个 id 出现两次。
协议里 `content_types[].kind_ids` 本来就是**列表**，因此正确形状是"按内容 ID 合并、kind_ids 列出全部种类"。

修法（`CatalogProjector`）：`content_types` 按内容 ID 分组后合并 `kind_ids`（排序、去重）。
默认 Profile 的目录字节不变（默认内容没有跨 kind 同名 ID）；新增检查断言 content_type 的 id 唯一、
`kind_ids` 非空且已排序。**这是共享 L2 校验器第一次真正拦住我方目录**，值得记录。

### W-01 验收项收官

| 验收项 | 落点 |
| --- | --- |
| 药水 kind / Rarity·Target 枚举 / 槽位·容量 / 集合 / `UsePotion` | 批次 27（`SlicePublicProfile` 作者变体 + group 机制 + `PotionBelt.Capacity`） |
| Run 与长期牌组的公开表达 | 批次 27（Run 实体字段 + `LongTermDeck` group，与战斗牌区两批实例不相交） |
| Runner/Collector 接受实际 Gym/scenario（不硬编码） | `ai_the_spire/runner/episode.py`：Gym 身份由后端 `ruleset_id` **派生**（`gym_id_from`），场景 profile 取 `describe` 声明的那个（恰好一个，否则报错不猜）；`j01_episode.py` 的 `SCENARIO` 常量与写死 `gym_id` 已删除。`data/audit.py` 本来就从轨迹身份读 `scenario_profile`，无需改 |
| 真实 stdio：describe→catalog→start→扩展卡/药水→终局→离线重编码 | **新增 AI 侧 `author` 组**（`python ai-the-spire/tests/j01_episode.py --only author`，11 项）：作者内容包上跑脚本策略（一热概率，离线可复核）到**终局**——本次 `reason=net:reason:base-spire:Victory`、31 步；断言轨迹里有用药、药水栏 2→1，落盘后用 `load_trace` + `verify_trace` **离线重编码**（0 问题） |
| 旧 M1 仍可回归 | `episode` 133 + `headbutt` 15 + `invariants` 7 + `trace` 7 全部通过（J-01 总 173/173） |

**策略注入**（顺带交付 W-04 的一项前置）：`EpisodeRunner` 新增可选 `policy`（收到 `(公开快照, 候选)`，
必须给出"恰好覆盖真实候选、和为 1"的概率，否则报错）与 `model_id`；注入策略却沿用随机基线身份会**直接报错**
（不得把脚本策略的数据记成基线）。默认不传 = 均匀基线，既有行为逐字不变。

### 验证（本批次实跑，返回码全 0）

| 命令 | 结果 |
| --- | --- |
| `dotnet build AutoTheSpire.sln` | 0 错误 0 警告 |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | **391/391** |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | 195/195 |
| `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` / `Core.TheSpire.Tests` | 64/64 / 190/190 |
| `python ai-the-spire/tests/j01_episode.py` | **173/173**（`author` 11 项：`ending=terminal steps=31`、离线重编码 0 问题） |
| `python net-the-spire/client/test_client.py` / `test_author_content.py` | 50/50 / **rc=0** |
| `python ai-the-spire/tests/run_checks.py` / `tools/contract-check/check_contracts.py` | 503/503 / 210/210 |

### 未结项

1. **W-01 的关闭结论待负责人复核**（执行者不自行关闭）；验收项本身已全部落地。
2. **药水栏/长期牌组的策略可用性**（W-03/W-04）：AI 编码器已支持 group 张量块，但本轮**没有**核对
   "药水栏成员是否真的进入了模型特征"；这属于 W-04 的编码覆盖与 W-03 的场景设计。
3. **终局结果本身**：本次 author 跑出 `Victory`（31 步，脚本策略）。它只证明"作者 Gym 能走到终局并留下可离线复核的轨迹"，
   **不**代表策略强度或学习效果（W-04/W-07 范围）。
4. 未动 W-02/W-04/W-05/W-06/W-03/W-07/W-08 的范围；W-02 仍待负责人复核关闭。

---

## 批次 29（2026-10-03）：W-04 共享低成本模型——结构与推理交付

认领：**W-04**（低成本模型与学习工程）。本批次交付"模型结构与前向 + 概率契约 + 模型包 + 成本报告"，
并让它在**真实 Gym** 上实际决策；梯度/更新/PPO 留下一步（框架路线本轮由用户裁定）。

### 新增（`ai-the-spire/ai_the_spire/models/`）

| 模块 | 内容 |
| --- | --- |
| `config.py` | `ModelConfig`：显式维度（每张字典表一个 embedding 维度 + 中间层 + 传播轮数 + 精度 + 种子 + 两个数值通道缩放），字段校验、规范 wire（浮点写成规范十进制字符串）、结构摘要。传播轮数有显式上界 0..8，避免无界堆叠 |
| `params.py` | 参数集合：**确定性初始化**（按参数名排序抽样，矩阵 `1/sqrt(fan_in)`）、按组统计、权重内容摘要（dtype+shape+bytes）、检查点 `state()/load_state()`（形状/名字/dtype 逐项一致才接受） |
| `arrays.py` | 张量化 → 模型输入：数组 + 掩码 + 字典规模，**只有数值/布尔**（实例句柄只以样本内行号出现）；`pad_model_input` 供掩码验收 |
| `encoder.py` | 共享 embedding → 实体基础向量 → **字段聚合**（数值/布尔/类别/**被引用实体行**）→ **L 轮稀疏关系传播** → 全局向量（掩码均值/最大值 + 四个**有效行数**计数） |
| `scorer.py` | 候选向量（动作类型 + 该候选具名字段的聚合，含**被引用实体行**）→ 与全局向量交互 → logits；**只在有效候选上** softmax；价值头输出单个内部 V（**无 Q**） |
| `policy.py` | `predict(snapshot, model) -> PolicyEvaluation`（概率 + V + 模型/字典/布局/Manifest 身份）；语义键来自**同一份快照的 legal_actions**；终止/无候选**显式拒绝**；`policy()` 适配 `EpisodeRunner` |
| `bundle.py` | 模型包 `metadata.json` + `weights.npz`：声明 exact identity、接口、精度与 `training="none(structural,random-init)"`；载入时逐项核对（缺键/多键/格式版本/结构摘要/字典与布局与 Manifest/权重摘要）并**显式失败** |
| `cost.py` | 参数分项 + 权重字节 + `tensorize` 与前向**分开**的 CPU 延迟（p50/p90/max）与峰值内存；只报告，不下"够轻"的结论 |

### 检查（新增 42 项）

| 入口 | 新增 |
| --- | --- |
| `run_checks.py`（离线，AI 侧） | **535/535**（503 → +32）：`model-shape` 8、`model-mask` 5、`model-invariance` 7、`model-policy` 5、`model-bundle` 7 |
| `j01_episode.py`（真实 stdio） | **183/183**（173 → +10）：新增 `model` 组 |

关键断言（都是"可判别"的，不是恒真）：
* **掩码**：补齐候选概率恒 0 且真实候选概率不变；全掩码时报错；屏蔽一个实体后全局向量必须改变；
* **不变性**：模型输入**不含任何字符串**（实例句柄不可能成为特征）；候选行重排（连同语义键映射）后按语义键概率不变；
  把语义键换成任意别名后概率与 V 逐字不变；非规范输入（实体未按 `instance_id` 排序）被校验层按 I-74 拒绝；
  关系行重排不改全局向量；同种子同结构复现同一权重与身份；
* **策略**：终止快照（无候选）**不伪造**概率；`policy()` 与 runner 的真实候选**不重不漏**；概率**不是均匀分布**（证明来自模型）；
  跨 Manifest 的张量被拒绝；
* **模型包**：往返后概率一致；缺 `metadata.json`/多键/权重被改写/换 Manifest/格式版本不一致分别命中各自的错误码。

### 查出的真实缺陷（已修 + 回归检查）

**字典索引 0 保留给补齐**（`Dictionary.index_of` 返回"位置 + 1"，真实条目从 1 开始）。我最初按 `size` 分配 embedding 行数，
于是"最后一条内容/字段"一出现就越界（`IndexError: index 7 is out of bounds for axis 0 with size 7`）。
修法：每张字典 embedding 分配 `size + 1` 行，并加检查 `shape/dictionary-embeddings-reserve-pad-row` 固定这一点。
同时把全局向量的四个计数特征从"块行数"改成"**有效行数**"（掩码和），否则补齐行会改变全局状态。

### 真实 Gym 证据（`python ai-the-spire/tests/j01_episode.py --only model`）

```text
model: …/episode.json ending=terminal steps=20 params=5444 forward_p50=0.381ms
```

即：随机初始化的 5444 参数小模型作为行为策略 π，在真实 stdio 上走到**终局**（20 步），
轨迹记录 `model_id` 与逐步概率，`verify_trace` 离线重编码 **0 问题**。
这证明"小模型输出满足契约并实际参与决策"；**不**代表学习效果（权重未训练）。

### 验证（本批次实跑，返回码全 0）

| 命令 | 结果 |
| --- | --- |
| `python ai-the-spire/tests/run_checks.py` | **535/535** |
| `python ai-the-spire/tests/j01_episode.py` | **183/183**（episode 133 / headbutt 15 / invariants 7 / trace 7 / author 11 / model 10） |
| `python net-the-spire/client/test_client.py` / `test_author_content.py` | 50/50 / rc=0 |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | 391/391 |
| 切片 / Engine / Legacy / 契约 | 195/195 / 64/64 / 190/190 / 210/210 |

### 用户裁定（2026-10-03，写进交接以免下一步再问）

1. **训练阶段只用 numpy 手写反向/Adam/PPO**，不引入训练框架依赖（不加 torch），也不引入 LLM 策略线；
   因此下一步的梯度与数值正确性由本项目自己实现与检查（不借助 autograd）。
2. "小模型"始终指本项目 `ai_the_spire/models/` 的策略模型（`model_id` 随轨迹记录），**不是**把对话中的大模型当策略。

### 未结项（W-04 剩余，下一步）

1. **反向传播与 Adam**：掩码 softmax 策略头 + 价值头的解析梯度、按参数名对齐的更新、梯度数值检查（对少量参数做有限差分对照）。
2. **近期 PPO（μ=π）**：只用本轮策略数据、clip 比率、旧/新 log-prob 同源计算、熵/价值损失配置化。
3. **合成任务验证**：有限且可判定的小任务上验证"损失下降、参数确实更新、保存→载入→输出一致、同种子可复现"。
4. **评估与模型包晋升**：评估配置、保留场景/种子、资源成本与失败记录；模型包升级为带训练身份的版本。
5. **奖励/偏好派生层**：版本化向量奖励与显式标量化（不猜权重）；真实 STS 训练仍等 **W-07** 冻结 Profile。
6. 本轮**没有**动 W-01/W-02/W-05/W-06/W-03/W-07/W-08 的范围。

---

## 批次 30（2026-10-03）：W-04 学习工程交付（自动微分 + Adam + PPO + 检查点）

认领：**W-04**。本批次按用户裁定（只用 numpy 手写，不引入训练框架、不引入 LLM 策略线）
交付 W-04 第 3–4 项的学习工程，并用有限差分与小任务把"梯度确实对、更新确实发生、恢复确实一致"证成可判别的事实。

### 新增与改动

| 文件 | 内容 |
| --- | --- |
| `models/autodiff.py`（新） | **微型反向模式自动微分**：`Var` + 逆拓扑 `backward` + 模型真正用到的算子（matmul/dot/add/sub/mul/divide/maximum/minimum/exp/clip/tanh/layernorm/gather/scatter_add/where/total/concat/reshape/掩码 softmax/交叉熵/熵）。**前向只写一遍**：推理时算子直接落到 numpy（不建图），训练时同一份代码记录反向闭包——避免"手写梯度"与"第二套前向"这两个典型错误源 |
| `models/encoder.py`、`models/scorer.py`（改） | 前向改为走算子（含 `vars=` 参数视图）；补上实体行为空、掩码行不参与、每行计数用**有效行数**等显式约束 |
| `models/learning.py`（新） | `TrainingConfig`（显式学习率/截断/轮数/价值与熵系数/梯度裁剪/Adam 超参 + 摘要与校验）、手写 `Adam`（含状态导出/恢复/摘要）、`PolicySample`（校验动作行有效）、`ppo_loss`（截断代理 + 价值回归 + 熵正则）、`Trainer`（参数视图与优化器**绑定在一起**，杜绝"梯度落在别的 Var 上"这类误用） |
| `models/bundle.py`（改） | 模型包可选携带**优化器检查点**（`optimizer.npz` + `training_state`），并把"未训练"与"可恢复检查点"区分开：要求恢复却没有训练状态 ⇒ `checkpoint_missing` |
| `tests/model_learning_suite.py`（新） | 3 组 12 项：`learning-grad`（有限差分对照）、`learning-update`（小任务学习曲线/更新/复现/截断）、`learning-restore`（内存与落盘检查点恢复） |
| `run_checks.py`、`ai-the-spire/README.md` | 接线与覆盖表更新（18 组 547 项） |

### 两个真实缺陷（都是新检查抓出来的）

1. **PPO 比率被写成倒数**：`ratio = exp(negative_log_prob + old_log_prob)`——`negative_log_prob` 返回的是 **−log π**，
   于是比率成了 π_old/π。比率一偏离 1 就被截断项吃掉梯度（被截断区域是平的），表现是"训练不升反降"
   （首次运行 `p(correct) 0.475 → 0.005`、loss 0.80 → 1.00）。修法是改用 log 概率相减。
   回归检查：构造"比率恰为 1.5"的样本，`clip_ratio=0.05` 时策略梯度必须**恰好为 0**、`clip_ratio=1.0` 时必须非零。
2. **Adam 的 β 允许等于 1**：偏差修正 `1 − β^t` 会变成 0 而除零。校验里改为**严格小于 1**。

另外把自动微分的一个真实形状缺陷修掉：`total(axis=0)` 的反向必须把梯度**广播**回输入形状（原先只 `expand_dims`，
二维输入上会报"梯度形状 (1,16) 与 (2,16) 不符"）。整模型梯度检查正是靠这一点才通过。

### 验证（本批次实跑，返回码全 0）

| 命令 | 结果 |
| --- | --- |
| `python ai-the-spire/tests/run_checks.py` | **547/547**（535 → +12：learning-grad 2、learning-update 6、learning-restore 4） |
| `python ai-the-spire/tests/j01_episode.py` | **183/183**（真实 stdio；`model` 组仍为 20 步到终局、离线重编码 0 问题） |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` / 切片 / Engine / Legacy | 391/391 / 195/195 / 64/64 / 190/190 |
| `python net-the-spire/client/test_client.py` / `test_author_content.py` | 50/50 / rc=0 |
| `python tools/contract-check/check_contracts.py` | 210/210 |

关键证据（逐条可核对）：

* **梯度**：逐算子（matmul/tanh、layernorm、掩码 softmax+熵、scatter_add、gather、where、maximum、clip）
  与**整模型选定参数**（`candidate/W_in`、`candidate/v`、`field/W_field`、`emb/action_type`、`value/w_out`）
  的解析梯度与中心差分一致（相对误差 < 1e-4）。
* **学习**：有限合成任务（学会挑数值更大的候选）正确率 **0.00 → 1.00**、正确候选平均概率 0.475 → 0.998、
  损失 0.8047 → −0.9998；参数内容摘要改变（确实更新了）；同种子两次训练得到**逐字节相同**的权重摘要。
* **截断**：区间外样本 `grad_norm = 0` 且 `clip_fraction = 1.0`，区间内同条件 `grad_norm = 0.526`。
* **梯度裁剪**：`max_grad_norm=0.25` 时观测到 `grad_norm=0.4884`、`scale=0.5119`。
* **恢复**：训练 5 步 → 落盘（权重 + 优化器矩 + 训练状态）→ 重新载入 → 再训 3 步，
  最终权重摘要与"不中断训 8 步"**逐字节一致**；未训练的包在要求恢复时按 `checkpoint_missing` 拒绝。

### 未结项（W-04 剩余）

1. **版本化奖励/偏好派生层**：向量奖励、单位、按显式配置标量化；本批次样本的优势/回报由调用方给出
   （合成任务里是 ±1 与 0/1），**没有**奖励归一化、优势估计（GAE）或偏好权重——因此合成任务上的损失波动较大，如实记录。
2. **评估**：固定执行配置与保留场景/种子的对照评估、方差/置信区间与资源成本记录（`training-design.md` §Evaluator）。
3. **真实 Gym 上的有界训练**：接口已就位（`Trainer` + `EpisodeRunner(policy=...)`），
   但真实 STS 训练仍等 **W-07** 冻结任务分布、奖励向量、预算与晋升门槛；不猜收益权重、不做无限训练。
4. **模型包晋升**：训练后包仍标 `training_profile_id=None`/`reward_profile_id=None`（因为这两个 Profile 还没冻结）；
   未冻结前不得把它当成"已训练的正式模型包"。
5. 本轮**没有**动 W-01/W-02/W-05/W-06/W-03/W-07/W-08 的范围。

---

## 批次 31（2026-10-03）：W-04 版本化奖励/偏好派生层

认领：**W-04**。本批次交付第 3 项里"在原始公开事实之外建立版本化奖励/偏好派生层，保留向量及单位，
按显式配置标量化"，并把奖励 Profile 身份接进模型包。

### 新增

| 文件 | 内容 |
| --- | --- |
| `ai_the_spire/reward/profile.py`（新） | `RewardComponent`（`component_id` + **单位** + **公开来源说明** + `scale` + `optional`）、`RewardFactPaths`（派生层要读的**公开路径**：player/monster 的 kind 与 hp 字段、turn 字段、药水栏 group type；为 None 表示该 Gym 没有这条公开事实）、`RewardProfile`（分量 + **显式权重必须恰好覆盖分量** + `discount` + facts + 语义版本 + 摘要）；`standard_short_task_profile()` 提供工程用分量表（**默认权重全 0**，真实权重由 W-07 给） |
| `ai_the_spire/reward/derive.py`（新） | `derive_episode_reward(快照序列, profile, steps)` → `RewardVector`（值 + 单位 + **逐分量贡献** + 标量 + `unavailable`）。规则与每个分量的 `source` 说明一一对应：敌人/玩家 HP 差、回合差、决策数、药水消耗、终局胜负。**缺必需公开事实 ⇒ `RewardError`**；optional 分量 ⇒ 记 `unavailable` 且**不参与**标量化（不用 0 冒充） |
| `tests/reward_suite.py`（新） | 3 组 20 项（见下） |
| `j01_episode.py`（改） | 新增 `reward` 组（9 项）：在**真实 stdio 轨迹**上派生，并与"独立从公开快照读字段"的结果逐项对照 |
| `models/bundle.py`（改） | 模型包新增 `reward_profile_id`/`training_profile_id`/`executor_profile_id` 三个可选身份字段（未冻结时为 null，**不自造 id**） |

### 检查（新增 29 项：离线 20 + 真实链路 9）

* `reward-profile`（8）：分量表显式（单位/来源齐全）、权重必须覆盖分量、未知权重/重复分量/非法单位/空来源被拒绝、
  版本不一致被拒绝、权重或 **facts** 变化即身份变化、非法标量（`discount ∉ (0,1]`、`scale=0`）被拒绝、Profile id 随模型包声明；
* `reward-derive`（6）：派生值与独立读数一致、单位保留、**塞隐藏诱饵（`hidden_state`/`internal_id`）不改结果**、
  权重线性、0 权重分量仍留在向量与贡献表、确定性摘要；
* `reward-missing`（6）：必需分量缺事实 ⇒ 显式失败（信息里点名该分量）、optional 记 `unavailable` 且不参与标量化、
  没有药水栏时药水分量记 unavailable（不是 0）、无派生规则的分量被拒绝、空序列被拒绝、**路径与快照不匹配时显式失败**；
* `j01 reward`（9）：真实轨迹上 `enemy_hp_delta`/`player_hp_delta`/`decision_count` 与独立读数一致、
  终局分量与 `terminal_result` 一致（未到终局则记 unavailable）、标量等于加权和、两次派生摘要相同、单位齐全。

### 验证（本批次实跑，返回码全 0）

| 命令 | 结果 |
| --- | --- |
| `python ai-the-spire/tests/run_checks.py` | **567/567**（547 → +20） |
| `python ai-the-spire/tests/j01_episode.py` | **192/192**（183 → +9；`reward` 组实测：`enemy_hp_delta=36`、`player_hp_delta=80`、`turn_count=5`、`decision_count=22`、`outcome_loss=1`、标量 **−49.6**，与独立读数逐项一致） |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` / 切片 / Engine / Legacy | 391/391 / 195/195 / 64/64 / 190/190 |
| `python net-the-spire/client/test_client.py` / `test_author_content.py` | 50/50 / rc=0 |
| `python tools/contract-check/check_contracts.py` | 210/210 |

### 过程事故（如实记录，并已固化为规则）

用 PowerShell 的 `Get-Content`/`Set-Content` 往返编辑 `ai-the-spire/tests/reward_suite.py` 时，
UTF-8 源文件被按 cp936 二次编码写成乱码，且**有损不可逆**（无法靠反向转码恢复），只能整文件重写。
检查后确认没有其它文件受影响的痕迹（乱码特征扫描为空、无残留临时文件），全套检查重跑为绿。
**规则补充**：源码/文档文件只用编辑工具修改，不用 shell 文本往返（`Get-Content | Set-Content`）。

### 未结项（W-04 剩余）

1. **评估**：固定执行配置 + 保留场景/种子的对照评估（随机基线 vs 候选模型）、方差/置信区间、资源成本与失败记录；
   在它交付前**不能**声称任何"学习改善"。
2. **真实 Gym 有界训练**：奖励层与 `Trainer` 已就位，但真实 STS 训练仍等 **W-07** 冻结任务分布、奖励向量、
   预算与晋升门槛；工程权重（例如本批次探针的 ±HP/回合/终局权重）**不得**当作训练目标。
3. **优势估计/奖励归一化**：当前样本的优势与回报由调用方给出（合成任务 ±1；真实轨迹可用本层的向量/标量派生），
   GAE 与归一化属下一步。
4. 本轮**没有**动 W-01/W-02/W-05/W-06/W-03/W-07/W-08 的范围。

---

## 批次 32（2026-10-03）：W-04 评估——同种子配对与失败分类

认领：**W-04**。本批次交付第 3 项最后一件"评估"（`training-design.md` §Evaluator），
W-04 的验收项至此全部落地（模型、学习器、奖励层、评估、真实 Gym 有界推理）。

### 新增

| 文件 | 内容 |
| --- | --- |
| `ai_the_spire/evaluation/plan.py`（新） | `EvaluationPlan`：策略表（`PolicySpec`：种类 + **训练状态**）、**基线必须存在且与候选不同**、非空且不重复的**保留种子**、指标表（`MetricSpec`：公开 id + **单位** + **方向**）、奖励 Profile 身份与摘要、执行器 Profile、scenario/gym、步数上限、自助配置；带版本与摘要（换种子/换奖励即新实验） |
| `ai_the_spire/evaluation/stats.py`（新） | 均值、**样本**标准差、线性插值分位数、**百分位自助法**置信区间（给定重抽次数与种子可复现） |
| `ai_the_spire/evaluation/runner.py`（新） | `EvaluationRunner`：每个 (策略, 种子) 跑一局真实会话，记录 `terminal/truncated/interrupted/faulted`、逐指标可用性、步数、墙钟与奖励向量；聚合**只用可用局**并逐指标报样本数与区间；同种子配对（差值 = 候选 − 基线）按方向给 `improved/worse/undecided/insufficient_samples`；报告含成本、失败清单与**未训练声明**。未绑定的模型策略在跑**任何一局之前**失败 |
| `tests/evaluation_suite.py`（新） | 3 组 19 项：计划身份、统计口径（已知答案/可复现/随方差变宽）、失败分类与报告 |
| `j01_episode.py`（改） | 新增 `evaluation` 组（9 项）：真实 stdio 上按固定计划跑配对评估并落盘报告 |

### 真实 stdio 实测（`python ai-the-spire/tests/j01_episode.py --only evaluation`，3 个保留种子）

```text
reward_mean = {baseline: -47.9, model(untrained): -53.8}
verdicts     = reward_scalar: worse | steps: improved | terminal_win: undecided | wall_ms: worse
```

即：随机初始化的小模型在奖励上**更差**、步数更少、胜负**区间跨 0（undecided）**、墙钟更慢（多了前向计算）。
这份报告**如实记录**"未训练模型不更好"，且报告里带 untrained 声明——这正是评估层该做的事，
**不**构成学习改善结论（学习改善要等 W-07 冻结任务/奖励/预算后的对照实验）。

### 验证（本批次实跑，返回码全 0）

| 命令 | 结果 |
| --- | --- |
| `python ai-the-spire/tests/run_checks.py` | **586/586**（567 → +19） |
| `python ai-the-spire/tests/j01_episode.py` | **201/201**（192 → +9） |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` / 切片 / Engine / Legacy | 391/391 / 195/195 / 64/64 / 190/190 |
| `python net-the-spire/client/test_client.py` / `test_author_content.py` | 50/50 / rc=0 |
| `python tools/contract-check/check_contracts.py` | 210/210 |

关键断言（可判别，不是恒真）：

* **失败局不入均值**：给候选塞入 reward=999 的 `faulted`/`interrupted` 局后，其奖励均值仍为 3.0、样本数 1；
* **无可用局不给指标**：全部局失败时所有策略的 `metrics` 为空、比较为 `insufficient_samples`；
* **不可用指标不聚合**：未到终局（truncated）时 `terminal_win` 不进入聚合，但 `reward_scalar` 仍可用；
* **区间正确性**：自助区间包含均值、同种子可复现、异种子不同、方差大时更宽、n 小时更宽；
* **方向**：步数（越低越好）上候选步数更多 ⇒ 判 `worse`；
* **报告**：失败清单逐条记录、未训练策略带声明、同输入摘要一致、奖励 Profile 与计划不符被拒。

### 未结项（W-04 剩余，都是"等 W-07"或可选增强）

1. **真实 STS 训练**：任务分布、奖励向量/权重、折扣与时间尺度、预算与晋升门槛要等 **W-07** 冻结；
   在那之前工程权重**不得**当训练目标，也不做无限训练。
2. **优势估计与奖励归一化**（GAE/标准化）：当前样本的优势/回报由调用方给出（合成任务 ±1；真实轨迹可用本层奖励向量派生）。
3. **模型包晋升**：训练后包仍标 `training_profile_id=None`/`reward_profile_id=None`（未冻结）；
   报告集**不用于反复选参**（评估计划里已绑定种子与权重，改动即新实验）。
4. 本轮**没有**动 W-01/W-02/W-05/W-06/W-03/W-07/W-08 的范围。

---

## 批次 33（2026-10-03）：W-03 真实场景簇、按簇隔离的划分与有界采样成本

认领：**W-03**（可用场景、数据与成本）。前置（W-01/W-02 落地、Gym 可运行且公开输入可信）在批次 26–32 满足，
本批次在其上交付"簇不是标签"的场景层、三个非空划分与有界采样成本证据。

### 新增

| 文件 | 内容 |
| --- | --- |
| `ai_the_spire/scenarios/manifest.py`（新） | `ScenarioSpec`：**真实宿主开关**（`--author-content` / `--test-headbutt`，未知开关被拒）+ **决策差异轴**（资源/目标/可观测知识，未知轴被拒）+ **可核对的区分事实**（`group_present` / `group_absent` / `candidate_content_present`）+ `identity_family`；`ScenarioManifest` 拒绝"两个簇共用同一套宿主配置"（= 同环境双标签）与"声明资源/目标轴却没有任何宿主开关"；清单有版本与摘要 |
| 其中的事实核对 | `check_distinguishing_fact(快照序列, spec)`：在**该簇真实录到的公开快照**上核对（允许"中途成立"，例如头槌后来才被抽到）；未实现的种类与空快照**显式失败**，不默认通过 |
| `ai_the_spire/scenarios/sampling.py`（新） | `run_scenario_sampling`：逐簇用**自己的宿主开关起新宿主**采样（复用 D-02 `Collector` 的限额/种子派生/进度），采完复算区分事实，汇总成本（限额/计数/墙钟/字节/停止原因）并写入报告（含"轮转非吞吐""随机数据非新策略近期 PPO 数据"声明）；`audit_sampling`：**按身份分组**做划分审计并聚合出全局三个 split 的非空性 |
| `tests/scenario_suite.py`（新） | 4 组 18 项（清单/事实/划分/成本，见下） |
| `j01_episode.py`（改） | 新增 `scenarios` 组（15 项）：**真实 stdio** 上采四个簇、逐簇核对事实、整份划分审计 |

### 四个真实簇（受控 Gym 上今天确实存在的差异）

| 簇 | 宿主开关 | 决策差异轴 | 可核对的区分事实 |
| --- | --- | --- | --- |
| `slice-default-v1` | 无 | 隐藏抽牌堆 | 公开快照里**没有**药水栏 group |
| `slice-headbutt-v1` | `--test-headbutt` | 弃牌选择 | 某个候选引用内容 `base-spire:headbutt`（中途成立） |
| `author-v1` | `--author-content` | 药水/槽位/整局 HP·金币/长期牌组 | 公开快照里有药水栏 group |
| `author-headbutt-v1` | 两者 | 上述两轴 | 药水栏 group 存在（组合场景） |

### 真实 stdio 实测（`python ai-the-spire/tests/j01_episode.py --only scenarios`）

```text
totals    = {attempts: 4, written: 4, interrupted: 0, faulted: 0, clusters: 4,
             wall_seconds: 1.92, written_bytes: 1414781}
per_split = {train: 2, validation: 1, final_eval: 1}
```

即：四个簇各采 1 局全部写入、无中断无故障；**每簇的区分事实都在真实录到的公开快照上成立**；
三个 split 非空且按簇隔离（无跨 split 同簇、无未标注簇）。

### 查出的真实约束（已修）

**作者内容簇与默认簇的 ruleset/catalog 身份不同**：把四个簇的产物写进同一个目录时，
`data.audit` **正确地**报出"同一产物目录里出现 2 种身份……不得合并成一份数据"。
修法不是放宽审计，而是**按身份分组存放**（`identity_family` 由 `--author-content` 决定，AI 侧不复制 net 的身份字符串），
逐组审计后再在聚合层判定"全局三个 split 非空"——单组里某个 split 为空是正常的（`划分退化` 是全局性质）。

### 验证（本批次实跑，返回码全 0）

| 命令 | 结果 |
| --- | --- |
| `python ai-the-spire/tests/run_checks.py` | **604/604**（586 → +18） |
| `python ai-the-spire/tests/j01_episode.py` | **216/216**（201 → +15） |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` / 切片 / Engine / Legacy | 391/391 / 195/195 / 64/64 / 190/190 |
| `python net-the-spire/client/test_client.py` / `test_author_content.py` | 50/50 / rc=0 |
| `python tools/contract-check/check_contracts.py` | 210/210 |

关键断言（可判别）：

* **簇不是标签**：两个簇用同一套宿主配置 ⇒ 清单拒绝；声明资源/目标轴却无开关 ⇒ 拒绝；未知开关 ⇒ 拒绝；
* **事实核对**：默认簇"药水栏不存在"、作者簇"药水栏存在"、头槌簇"候选引用头槌"三种谓词都在公开快照上判定，
  反例（事实不成立）被如实报否；未实现的事实种类与空快照显式失败；
* **划分**：覆盖全部簇、train ≥2 簇、三个 split 非空、空划分/未知 split 被拒、划分摘要即身份、无跨 split 同簇；
* **成本**：报告聚合限额/计数/墙钟/字节/停止原因，且**显式声明**"轮转不是并行吞吐""随机策略数据不是新策略的近期 PPO 数据"。

### 未结项（W-03 剩余）

1. **场景簇只有 4 个**：非空且按簇隔离，但**不支持簇内泛化**的可信结论；更丰富的资源/目标/知识簇
   依赖 **W-06** 的内容扩张（更多卡牌/药水/事件）与 **W-07** 冻结的任务分布。
2. **真实预算未冻结**：本轮限额（每簇 1 局、每局 20 步、120s、32MB）是工程探针值，
   真实采样规模/时间/磁盘预算等 **W-07**。
3. **数据尚未用于训练**：随机基线数据只作数据设施验证与基线；近期 PPO 数据必须来自**被优化的策略**（μ=π），
   这一条已写进报告声明，等 W-07 之后执行。
4. 本轮**没有**动 W-01/W-02/W-04/W-05/W-06/W-07/W-08 的范围。

---

## 批次 34（2026-10-03）：W-05 两房间纵向闭环（战斗终止 ≠ 整局终止）

认领：**W-05**（持久 Run 与两个连续房间）。批次 25 之后 W-05 一直停在"单场战斗 + 结算"，
本批次把**整局由连续战斗房间组成**这件事做成领域流程，并在真实 stdio 上跑通两个房间。

### 改了什么（base-spire 领域 + net 公开面）

| 位置 | 改动 |
| --- | --- |
| `CombatSlicePackage.cs`：`Run` 字段 | 新增 `CurrentRoom` / `SettledRoom`：**按房间**的结算幂等键（§19.2 明确"一个不重置的全局布尔代表不了所有房间"） |
| `SettleBattle` | 守卫改为 `SettledRoom != CurrentRoom` ⇒ 同一间房重复结算为空操作；进入下一间房后可以**再**结算一次 |
| **新增 `StartNextRoom` 过程** | ① 房间号 +1；② 清掉上一间房的一切（已知事实、敌人及其能力、玩家能力、五个战斗牌区里**全部**牌，含临时生成的）；③ 玩家从**整局**状态重置（HP/最大 HP 来自 `Run`，格挡/能量/回合/守卫复位）；④ 重建敌人（同一份遭遇表）；⑤ 战斗牌 = **长期牌组逐张字段级复制**（升级、数值、实例变量随长期实例走；临时费用修正清零）；⑥ 开这一间房的回合 |
| `SetupBattle` | 作者路径改为：建 Run/长期牌组/玩家/牌区实体/药水 → 调 `StartNextRoom`（**第一间房与后续房间走同一条流程**）。程序化路径（无作者文本）逐字不变 |
| **新增 `EnterNextRoom` 动作** | 仅作者路径；`Requires` = 本房已结算 **∧** 还有下一间（动作层把关，不靠调用方自觉），Body = `StartNextRoom` |
| 胜负分支 | 清房 ⇒ 结算但**不**终止（会话继续 Running）；只有**最后一间房**才设整局 `Victory` 终局 ⇒ "战斗终止与整局终止可区分" |
| `SliceManifest.RunRoomCount = 2` | 受控切片的房间数（显式标注为受控取值，真实地图/房间表仍归整局 manifest） |
| `SlicePublicProfile`（net） | 发布 `Run.CurrentRoom`/`SettledRoom`（number）与 `EnterNextRoom` 动作 |

### 新增检查（切片 +4，作者组 +1）与证据

切片（`SliceFrontendRunStateChecks`）：

* **两房间：HP 从整局延续，第二间房重建敌人与牌区** —— 第一间房把玩家打到 41 再清场 ⇒ 整局 HP=41；
  进入第二间房后玩家 HP=41（**不是**声明初值 73）、Block=0、Turn=1、`BattleSettled=false`、抽 5 张、敌人血量回到定义值；
* **长期牌组里的升级会进入第二间房（同定义实例分别升级）** —— 提供方侧把一个长期打击升到 9 点、另一个升到 12 点，
  第二间房的战斗牌里**同时**出现 9 点与 12 点，且未升级的仍是 6 点（互不牵连）；
* **临时生成的牌不污染长期组，也不进入第二间房** —— 打 `test-generate` 后长期牌组成员**完全不变**，
  且生成的实例不出现在第二间房任何牌区；
* **结算按房间幂等** —— 未结算时 `EnterNextRoom` **不合法**；结算后合法；进入第二间房后 `SettledRoom=1 < CurrentRoom=2`
  ⇒ 第二间房结算后整局 HP 更新为 55（按房间键**再结算一次**，而不是被全局布尔挡住）；最后一间房清完 ⇒ 整局 Victory 终局；
* 既有的 `B-06a 胜利终局` 检查改为**最后一间房才终局**（第二间房先打一回合，终局回合 = 2 ⇒ 仍证明它读当前回合）。

真实 stdio（`python ai-the-spire/tests/j01_episode.py --only author`，12 项）：

```text
author: …/episode.json ending=terminal reason=net:reason:base-spire:Victory steps=63
        rooms: CurrentRoom 1 → 2，轨迹里使用过 EnterNextRoom；药水栏 2→1；离线重编码 0 问题
```

即：**作者 Gym 通过公开接口走完两间房并取得整局胜利**——玩家用牌/药水打完第一间房 → 引擎提供 `EnterNextRoom`
→ 进入第二间房（HP 与长期牌组延续）→ 再打完 → 整局 Victory；轨迹自足且可离线重编码。

### 查出的真实约束（已修）

**`Requires` 是"设置"单个守卫，不是累积条件**（`PackageBuilder.cs:641`：`Action.Guard = guard`）。
我最初写了两次 `Requires`，于是后一个（"还有下一间房"）覆盖了前一个（"本房已结算"），
导致**未结算时 `EnterNextRoom` 也合法**——被新检查当场抓住。修法是显式合取（`And(...)`），
并保留"未结算时该动作不出现"的检查。

### 验证（本批次实跑，返回码全 0）

| 命令 | 结果 |
| --- | --- |
| `dotnet build AutoTheSpire.sln` | 0 错误 0 警告 |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | **199/199**（195 → +4） |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | **391/391** |
| `python ai-the-spire/tests/j01_episode.py` | **217/217**（含作者组 12 项：两间房 Victory 63 步） |
| `python ai-the-spire/tests/run_checks.py` | 604/604 |
| Engine / Legacy / client / 契约 / 覆盖 / 依赖 / doc-check | 64/64 / 190/190 / 50/50 / 210/210 / 1/1 / 9/9 / 0 |
| `python net-the-spire/client/test_author_content.py` | rc=0（新增的 Run 字段与动作没有破坏开局探针） |

### 未结项（W-05 剩余）

1. **卡牌奖励/升级的公开动作未交付**：本批次的"长期实例升级"由**提供方侧**编辑完成（检查里明确标注），
   公开流程（战斗后奖励选择 → 升级某张长期牌）还没有动作与公开投影；这是下一份交付。
2. **房间数是受控值 2**：真实整局的地图/房间表由整局 manifest 决定（W-06/整局范围）。
3. **net 侧的中间奖励选择**：两房间的公开快照已被真实轨迹覆盖（可编码、离线重编码通过），
   但"奖励选择"这一公开动作尚未暴露。
4. 本轮**没有**动 W-01/W-02/W-03/W-04/W-06/W-07/W-08 的范围。

---

## 批次 35（2026-10-03）：W-05 房间奖励（升级）公开化

认领：**W-05** 剩余项"长期实例奖励/升级"的**公开**流程。批次 34 把两间房接通，但升级只能由提供方侧编辑；
本批次把它变成玩家可提交的公开动作，并保留"每间房至多一次"的幂等语义。

### 改了什么

| 位置 | 改动 |
| --- | --- |
| `CombatSlicePackage.cs`：`Run` 字段 | 新增 `RewardTaken`：**奖励**的房间键（与 `SettledRoom` 分开 ⇒ 可以清房但**跳过**奖励） |
| **新增 `UpgradeLongTermCard` 动作** | 作者路径独有；候选＝**长期牌组**成员；`Requires` = 本房已结算 **∧** 本房未拿奖励（`And` 显式合取）；Body = 按 `DefinitionId` **动态分派**套用升级变体：`Upgraded=true`、费用、数值、能力层数、不可打出、虚无、实例变量初值；已经是升级形态时是**空操作**且**不**记 `RewardTaken`（玩家仍可再选一张） |
| `SlicePublicProfile`（net） | 发布 `Run.RewardTaken`（number）与 `UpgradeLongTermCard` 动作（参数 `Card: ref<Card>`，`TargetKindIds=[Card]`，required） |
| `SliceFrontendRunStateChecks.cs` | +2 检查（见下）；受控场景里把 `test-selfharm` 的**升级**数值从 1 改成 3，让"升级确实换了数值"可判别 |
| `j01_episode.py` | 作者策略优先取奖励，再进入下一间房；+2 条轨迹检查 |

### 新增检查

* **房间奖励：升级长期实例（每间房一次，且升级形态进入下一间房）** —— 未清房时动作**不合法**；清房后合法；
  提交后长期实例 `Upgraded=true`、`Amount` 写入**升级变体**的 3（不是普通形态的 1）、`RewardTaken=1`；
  同一间房**不能**再拿第二次；进入第二间房后 `RewardTaken(1) != CurrentRoom(2)`（奖励键按房间），
  且第二间房的**战斗拷贝**里恰好有一张升级过的 `test-selfharm`、数值为 3；最后一间房清完 ⇒ 整局 Victory、不再有奖励动作；
* **跳过奖励不影响进入下一间房** —— 清房后不拿奖励也能执行 `EnterNextRoom`（奖励与结算分开），
  进入第二间房后两个动作都因"尚未结算"而不合法。

### 真实 stdio 证据（`python ai-the-spire/tests/j01_episode.py --only author`，14 项）

```text
author: …/episode.json ending=terminal reason=net:reason:base-spire:Victory steps=62
        轨迹里：CurrentRoom 1→2、使用过 EnterNextRoom 与 UpgradeLongTermCard、
        第二间房的公开快照里出现 Upgraded=true 的卡（房间奖励确实进入了下一战）
```

### 验证（本批次实跑，返回码全 0）

| 命令 | 结果 |
| --- | --- |
| `dotnet build AutoTheSpire.sln` | 0 错误 0 警告 |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | **201/201**（199 → +2） |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | **391/391** |
| `python ai-the-spire/tests/j01_episode.py` | **219/219**（作者组 14 项） |
| `python ai-the-spire/tests/run_checks.py` | 604/604 |
| Engine / Legacy / client / 契约 / 覆盖 / 依赖 | 64/64 / 190/190 / 50/50 / 210/210 / 1/1 / 9/9 |
| `python net-the-spire/client/test_author_content.py` | rc=0 |

### 未结项（W-05 剩余）

1. **加牌型奖励**（把一张新卡加入长期牌组，而不是升级已有牌）尚未公开化：需要作者声明"奖励卡池"，
   否则无法区分"可以进卡池的牌"与"状态牌/初始牌"。
2. **房间数仍是受控值 2**：真实地图/房间表归整局 manifest（W-06/整局范围）。
3. 奖励目前是"每间房一次升级"，没有**多选一**（原版的三选一）与随机性；受控切片先保持确定性。
4. 本轮**没有**动 W-01/W-02/W-03/W-04/W-06/W-07/W-08 的范围。

---

## 批次 36（2026-10-03）：W-06 正式 0.0.1 → Engine 首段纵向路径与解析器路径级移动

认领：**W-06** 的第一份交付——"先让实际 0.0.1 作者内容沿解析/AST→通用 IR→链接→加载→执行走通，
选基础/升级、目标…等能区分语义的代表内容作纵向证据"。

### 结构决定：一个解析器、两个后端

新路径**不能**引用 `Core.TheSpire/Legacy/` 的类型（架构检查 `engine-and-slice/no-legacy-types`）。
第一次尝试直接复用 `Legacy/DslSource.cs` 的解析器时，依赖检查**当场失败**
（`违规: 1 … SpireCardSource.cs`）——这是本轮第一个真实发现。

两条路：(a) 在新路径再写一个解析器（两份语法必然漂移）；(b) 把**泛型语法**解析器从 `Legacy/` 移出。
选了 (b)：`core-the-spire/Core.TheSpire/Legacy/DslSource.cs` → `Engine/DslSource.cs`，
**路径级移动**——命名空间仍是 `CoreTheSpire`、类型名不变，因此四个 Legacy 消费者**零改动**；
依赖检查随之通过（Legacy 类型 363 → 344，违规 0），新路径复用的是同一个解析器，
`Legacy/README.md` 的文件表已更新说明。

### 新增

| 文件 | 内容 |
| --- | --- |
| `BaseSpire/Slice/SpireCardSource.cs`（新） | 正式 `*-cards.spire`（嵌入资源）→ `DslSourceParser` AST → 共享作者模型 `AuthorCard`（与 `.author` 同一模型）→ `CombatSlicePackage` 的 Engine IR → 会话执行。**四态分开报**：声明（AST 里的 `card` 数）/ 解析 / 降级 / 未支持（逐条原因） |
| `BaseSpire.Slice.Tests/SliceFormalSourceChecks.cs`（新） | 9 项检查：资源枚举、四态分离、未支持名单（多原语/非空 hooks/X 费）、AST 声明集一致、基础/升级伤害与格挡、`exhaust`、`unplayable`、`draw`、目标语义 |

显式范围（本批次）：单效果原语（`attack { damage }` / `gainBlock { amount }` / `draw { amount }` / 空 `play`）
+ 关键词 `exhaust`/`ethereal`/`unplayable`/`retain` + 目标域 `selectSelf` /
`selectOne { from = context.opponents where = alive }`；其它构造**记为未支持并附原因**。

### 四态实测（真实嵌入资源，不是夹具）

```text
base-spire/ironclad-cards.spire: 声明 94 / 解析 94 / 降级 7 / 未支持 87
base-spire/status-cards.spire:   声明  2 / 解析  2 / 降级 1 / 未支持  1
```

降级的 8 张：`strike-ironclad`、`defend-ironclad`、`bludgeon`、`giant-rock`、`impervious`、
`slimed`、`wound`、`dazed`。未支持一律带原因，例如 `Beckon`：`target 选择器 'selectNone' 未支持`
+ `hooks 非空（本批次未支持）`；`TwoPrimitives`：`声明了 2 个效果原语（本批次只支持单一原语…）`。

### 行为证据（真的跑起来）

| 检查 | 内容 |
| --- | --- |
| 基础/升级伤害与格挡 | 正式源 `StrikeIronclad` 6 → 升级 9；`DefendIronclad` 5 → 升级 8（数值逐字来自源） |
| `keywords = { exhaust }` | `Impervious` 给 30 点格挡，牌进**消耗区**且不在弃牌堆 |
| `keywords = { unplayable }` | `Wound` 不在 `PlayCard` 的**合法提交组合**里；强制提交被 guard 拒绝且会话状态不变 |
| `draw` 原语 | `Slimed` 打出后手牌数不变（打出 1 张、抽 1 张）且进消耗区 |
| 目标语义 | `DefendIronclad` 声明 `selectSelf`（不需要目标）、`StrikeIronclad` 声明 `one-opponent-alive` |

### 验证（本批次实跑，返回码全 0）

| 命令 | 结果 |
| --- | --- |
| `dotnet build AutoTheSpire.sln` | 0 错误 0 警告 |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | **210/210**（201 → +9） |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | 391/391 |
| `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` / `Core.TheSpire.Tests` | 64/64 / 190/190 |
| `python tools/dependency-check/check_dependencies.py` | **9/9**（违规 0；Legacy 类型 363→344） |
| `python tools/coverage-check/check_slice_coverage.py` | 1/1（剩余未迁 72，分母口径不变） |
| `python tools/contract-check/check_contracts.py` | 210/210 |
| `python ai-the-spire/tests/run_checks.py` / `j01_episode.py` | 604/604 / 219/219 |
| `python net-the-spire/client/test_client.py` / `test_author_content.py` | 50/50 / rc=0 |

### 未结项（W-06 剩余，明确不把代表批次当整项完成）

1. **内容覆盖只到单原语代表批次**：多原语（`IronWave` 类）、X 费、hook、`selectNone`/`selectAll`
   等目标域仍**显式未支持**；87/94 张铁甲战士卡尚未迁移。
2. **power / enemy / potion 的正式源仍走旧路径**：本批次只做 `card`；`power`/`enemy`/`potion`
   的 AST 降级与四态登记是下一段。
3. **四态里的"host/行为"数只登记本批次的 8–9 张**（逐张有行为检查），**没有**用声明数填充加载或行为数；
   其余卡片在四态表里应保持"未迁移/未知"，不得写 0 或声明数。
4. **默认后端不切换**：范围闭合前保留 Legacy 兼容，不删旧后端（与关闭条件一致）。
5. 本轮**没有**动 W-01/W-02/W-03/W-04/W-05/W-07/W-08 的范围。

---

## 批次 37（2026-10-03）：W-06 多原语与能力/自损/全体覆盖扩张（降级 43/94）

认领：**W-06** 的第二段交付——按批次 36 的路径继续扩大"正式源 → Engine"的实际覆盖，
并同样只登记**跑过**的行为证据。

### 新增覆盖（`SpireCardSource`）

| 能力 | 说明 |
| --- | --- |
| **多原语** | `play` 块里的多个原语按**源内顺序**降级；需要选目标时把 `target` 步骤放在最前（与冻结 `.author` 编码 `[target, block, damage]` 一致） |
| `applyPower` | `target = context.target` → `apply_power`；`context.owner` → `apply_power_self`；`allEnemies` → `apply_power_all`；单能力时把 `power`/`stacks` 写进**变体级**字段（与冻结 `.author` 逐字一致），多能力时用带参步骤 |
| `loseHp` | `lose_hp:<n>`（HP 损失，不经格挡/修正，与来源 `properties = { move unpowered unblockable }` 同义） |
| `count` | 批次（`damage x2` 等）→ 步骤的 `Count` |
| `selectAll` | 目标域 → `all`（配合 `attack { target = allEnemies }` 降级为 `damage_all`） |

变体级 `amount` 取**攻击**数值、无攻击时取格挡数值（与冻结 `.author` 的 IronWave/Bash/Breakthrough 口径一致）。

### 四态实测

```text
base-spire/ironclad-cards.spire: 声明 94 / 解析 94 / 降级 43 / 未支持 51
base-spire/status-cards.spire:   声明  2 / 解析  2 / 降级  1 / 未支持  1
```

即：**降级从 7 张升到 43 张**；其余 51 张仍在未支持清单里**逐条带原因**
（`chooseCard` / `repeat` / `if` / `forEachCard` / `withCount` / `autoPlay` / `scaledAttack` /
X 费 / 未知关键词等）。四态里的"加载/行为"数**只登记真的跑过的代表卡**，不用声明数填充。

### 行为证据（新增 4 项，全部可判别）

| 检查 | 判据 |
| --- | --- |
| 多原语按源内顺序 | IronWave（源里 `gainBlock` 在 `attack` 之前）：先 5 点格挡、再 5 点伤害 |
| `applyPower` 目标 + **顺序** | Bash 打 8 点（**不是**被自己施加的易伤放大后的 12）⇒ 证明"先伤害后能力"；随后该目标有 2 层易伤（另一个敌人 0 层）⇒ 证明能力落在**所选目标**上；后续打击 6 → **9** ⇒ 证明易伤真的生效 |
| `applyPower`（自己）+ `attack`+`draw` | PommelStrike 9 点 + 抽 1 张（手牌数不变）；Inflame `target = context.owner` 给自己 2 层力量、敌人 0 层 |
| `loseHp` + `allEnemies` | Breakthrough 自损 1 点**不经格挡**（先给 5 点格挡，格挡保持 5）+ 前后排各 -9 |

### 验证（本批次实跑，返回码全 0）

| 命令 | 结果 |
| --- | --- |
| `dotnet build AutoTheSpire.sln` | 0 错误 0 警告 |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | **214/214**（210 → +4） |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | 391/391 |
| `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` / `Core.TheSpire.Tests` | 64/64 / 190/190 |
| `python tools/dependency-check/check_dependencies.py` | 9/9（违规 0） |
| `python tools/coverage-check/check_slice_coverage.py` | 1/1 |
| `python tools/contract-check/check_contracts.py` | 210/210 |
| `python ai-the-spire/tests/run_checks.py` / `j01_episode.py` | 604/604 / 219/219 |
| `python net-the-spire/client/test_client.py` | 50/50 |

### 未结项（W-06 剩余）

1. **51 张仍显式未支持**：控制流类原语（`if`/`repeat`/`forEachCard`/`withCount`/`autoPlay`）、
   选择类（`chooseCard`）、数值来源类（`scaledAttack`/`damageValue`）、X 费与未知关键词；
   它们要么需要 Engine 侧先补机制（数值来源/批次/控制流），要么需要新的语义决定。
2. **power / enemy / potion 的正式源仍走旧路径**（本批次只做 `card`）。
3. **默认后端不切换**；范围闭合（内容与机制覆盖）后才谈切换与隔离兼容程序集。
4. 本轮**没有**动 W-01/W-02/W-03/W-04/W-05/W-07/W-08 的范围。

---

## 批次 38（2026-10-03）：W-06 数值来源与能力链接（口径修正：降级 28/94）

认领：**W-06** 的第三段交付——补"不依赖控制流"的两类机制：**数值来源（scaled）** 与 **能力引用链接**。

### 新增能力

| 机制 | 说明 |
| --- | --- |
| `damageValue` / `upgradedDamageValue` | 卡级数值：标量（字面量）或 `{ scaled = { base, perCount, count = {…} } }`，逐变体 |
| `scaledAttack` | 原语级 scaled 攻击（数值即 `base + perCount × count`） |
| `attackUsingBlock` | 伤害 = 当前格挡（无卡级 `damageValue` 时按 base 0 / perCount 1） |
| `gainBlockFromCurrentBlock` | 格挡 = 当前格挡（冻结 `.author` 的 `amount-source = block-count`） |
| `modifyEnergy` | 获得能量；`amount` 也可以是 scaled（ExpectAFight：能量 = 手牌攻击数） |
| **数值来源词表** | `block-count`、`exhaust-count`（`pileCount { pile = exhaust }`）、`hand-count:Attack`（`pileCount { pile = hand, type = attack }`）——与冻结 `.author` 的 `amount-source` 词表对齐；其它来源（`cardsWithTag`/`targetPowerAmount`/`energyX`/`variable`）**记为未支持** |
| **能力链接** | 正式源里 `power = <能力名>` 通过"能力名 → 已声明 id"表解析；**链接不上记为未支持**（不猜 id），步骤形式的 `apply_power:<名字>:<层数>` 同样链接 |

### 口径修正（重要的诚实性修正）

批次 37 的"降级 43"以及我随后报的"50"里，**能力名被 kebab-case 当成 id**
（例如 `NoEnergyGainPower` → `base-spire:no-energy-gain-power`），而冻结内容里的 id 是
`base-spire:no-energy-gain` —— 也就是说那批"已降级"的卡其实**引用了不存在的 id**，
一旦真的构建会话就会失败（本轮在 ExpectAFight 上就是这样撞出来的）。

加上链接之后：

```text
base-spire/ironclad-cards.spire: 声明 94 / 解析 94 / 降级 28 / 未支持 66
  其中 27 张：引用了**本模型未声明的能力**（Tank / Juggling / FlameBarrier / Juggernaut …）
              ⇒ 正确记为未支持（等 power 正式源迁移）
base-spire/status-cards.spire:   声明  2 / 解析  2 / 降级  1 / 未支持  1
```

因此"降级 43/50"是**虚高**的；本轮把它更正为 **28/94**（口径：只有真正能加载、且引用都能解析的卡才计降级）。

### 行为证据（新增 4 项）

| 检查 | 判据 |
| --- | --- |
| `block-count`（BodySlam / Entrench） | 先用 Defend 攒 5 格挡 ⇒ BodySlam 打 5 点（**等于当时格挡**，不是字面量）；Entrench 把 5 格挡变 10 |
| `exhaust-count`（AshenStrike） | 消耗堆为空 ⇒ 打 base 6；消耗 2 张后再打 ⇒ 6 + 3×2 = **12**（数值随公开状态变化） |
| `hand-count:Attack`（ExpectAFight） | 能量先扣 2 费归零 ⇒ 打出后能量 = 手牌攻击数（5）；同时自加 1 层 `no-energy-gain` |
| **链接** | ExpectAFight 的能力 id 来自**链接表**（`base-spire:no-energy-gain`，不是 kebab 名）；Juggernaut 因引用未声明能力而**记未支持**且不在降级集里 |

### 验证（本批次实跑，返回码全 0）

| 命令 | 结果 |
| --- | --- |
| `dotnet build AutoTheSpire.sln` | 0 错误 0 警告 |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | **218/218**（214 → +4） |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | 391/391 |
| `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` / `Core.TheSpire.Tests` | 64/64 / 190/190 |
| `python tools/dependency-check/check_dependencies.py` | 9/9（违规 0） |
| `python tools/coverage-check/check_slice_coverage.py` | 1/1 |
| `python tools/contract-check/check_contracts.py` | 210/210 |
| `python ai-the-spire/tests/run_checks.py` / `j01_episode.py` | 604/604 / 219/219 |

### 未结项（W-06 剩余）

1. **66 张未支持**：27 张等 **power 正式源迁移**（本轮的链接表来自作者文本，能力声明仍走旧路径）；
   其余为控制流/选择/其它数值来源与 X 费（`if`/`repeat`/`forEachCard`/`withCount`/`autoPlay`/
   `chooseCard`/`cardsWithTag`/`targetPowerAmount`/`energyX`/`Whirlwind` 的 `repeatForX` 等）。
2. **power / enemy / potion 正式源**仍是旧路径（下一段）。
3. **默认后端不切换**；范围闭合后才谈切换与隔离兼容程序集。
4. 本轮**没有**动 W-01/W-02/W-03/W-04/W-05/W-07/W-08 的范围。

---

## 批次 39（2026-10-03）：W-06 能力（power）正式源迁移与运行时实现白名单

认领：**W-06** 的第四段交付——把**能力声明**从作者文本迁到正式能力源，解开依赖能力链接的卡。

### 新增

| 内容 | 说明 |
| --- | --- |
| `BaseSpire/Slice/SpirePowerSource.cs`（新） | 正式 `*-powers.spire` → `DslSourceParser` AST → `AuthorPower`；四态（声明/解析/降级/未支持原因） |
| 字段降级 | `role`、`outgoingDamageMultiplier`→`damage = multiply-outgoing:M`、`preventEnergyGain`、`removeAfter{Owner,Player}TurnEnd`→`owner-turn-end = remove`、`decrementOnTrigger`/`afterEnemyTurnEnd`/`afterPlayerTurnEnd`→`owner-turn-end = tick`、`trigger = afterCardExhausted/afterCardPlayed`→`trigger`、`whenCardType`→`when-card-type`、`effect = gainBlock(N, perPowerStack=自己)`→`trigger-effect = gain-block-per-stack:N` |
| 卡牌模型 | 能力声明**整体改用正式能力源**；卡牌的"能力名 → id"链接表也随之来自正式源 |

### 两个关键判断（都由真实失败逼出来）

1. **能力 id 不能从名字 kebab 推导**：`NoEnergyGainPower` → 运行时 id 是 `no-energy-gain`（不是
   `no-energy-gain-power`）、`RagePower` → `rage-power`。因此 id 由**显式规范 id 表**给出
   （本轮取自冻结内容的 id 表；表里没有时才回退 kebab 并保留该事实）。
2. **运行时实现集合白名单**：有些能力"字段能映射、但运行时没有该行为"（NoDraw 的"不能抽牌"、
   DemonForm 的回合开始、Barricade 的 `preventsBlockClear`、各种 `cardRule`/`events`）。
   这些**记为未支持**——降级出一个不生效的空能力就是过度声称。白名单里明确写了每个能力的实现语义。

### 四态实测

```text
能力：base-spire/ironclad-powers.spire: 声明 27 / 解析 27 / 降级  3 / 未支持 24
      base-spire/shared-powers.spire:   声明  7 / 解析  7 / 降级  4 / 未支持  3
      base-spire/potion-powers.spire:   声明  1 / 解析  1 / 降级  0 / 未支持  1
      ⇒ 降级 7：strength、vulnerable、weak、shrink、feel-no-pain、rage-power、no-energy-gain
卡牌：base-spire/ironclad-cards.spire:  声明 94 / 解析 94 / 降级 28 / 未支持 66
      其中 27 张未支持的原因是"引用了运行时未实现的能力"（Tank/Juggling/FlameBarrier/Juggernaut…）
```

### 行为证据（新增 2 项；能力声明来自正式源）

| 检查 | 判据 |
| --- | --- |
| FeelNoPain（`afterCardExhausted` + `gainBlock(1, perPowerStack=自己)`） | 打出 FeelNoPain 得 3 层；再打出 Impervious（消耗）⇒ 格挡 = 30 + 3×1 = **33** |
| RagePower（`afterCardPlayed` + `whenCardType=attack` + 回合末移除） | 打出 Rage 得 3 层；打**技能**牌不触发（只多 Defend 的 5）；打**攻击**牌触发 +3；回合末层数归 **0** |
| 迁移回归 | Bash 施加的 Vulnerable 换成**正式源**处理后，后续打击仍是 6 → **9**（×1.5 生效） |

### 验证（本批次实跑，返回码全 0）

| 命令 | 结果 |
| --- | --- |
| `dotnet build AutoTheSpire.sln` | 0 错误 0 警告 |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | **220/220**（218 → +2） |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | 391/391 |
| `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` / `Core.TheSpire.Tests` | 64/64 / 190/190 |
| `python tools/dependency-check/check_dependencies.py` | 9/9（违规 0） |
| `python tools/coverage-check/check_slice_coverage.py` | 1/1 |
| `python tools/contract-check/check_contracts.py` | 210/210 |
| `python ai-the-spire/tests/run_checks.py` / `j01_episode.py` | 604/604 / 219/219 |
| `python net-the-spire/client/test_client.py` | 50/50 |

### 未结项（W-06 剩余）

1. **28 个能力未支持**：`cardRule`（改费用/额外打出/结果区）、`events`（事件订阅）、
   `incomingDamageModifiers`、回合开始触发、`isTemporary`/`onExpire` 等——**运行时没有对应机制**；
   要迁移它们必须先给 Engine/切片补机制（不是"再写个映射"）。
2. **66 张卡未支持**：27 张等上面的能力机制；其余为控制流/选择/其它数值来源/X 费
   （`if`/`repeat`/`forEachCard`/`withCount`/`autoPlay`/`chooseCard`/`cardsWithTag`/
   `targetPowerAmount`/`repeatForX` 等）。
3. **enemy / potion 正式源**仍走旧路径（下一段）。
4. **默认后端不切换**；范围闭合后才谈切换与隔离兼容程序集。
5. 本轮**没有**动 W-01/W-02/W-03/W-04/W-05/W-07/W-08 的范围。

---

## 批次 40（2026-10-03）：W-06 先补机制再迁内容——`preventsBlockClear` 与 Barricade

认领：**W-06** 的第五段交付。批次 39 的结论是"剩下 28 个能力缺的是**运行时机制**，不是映射"，
本批次按这个结论走一遍完整流程：**先在通用运行时补一条机制，再把对应能力与卡迁过来**。

### 补的机制（声明驱动，不写死内容）

| 位置 | 改动 |
| --- | --- |
| `CombatSlicePackage.cs` | 新增 `BlockClearPrevented(scope, owner)`：只要拥有者身上有**声明了 `preventsBlockClear`** 的能力（层数 > 0）就为真；`StartPlayerTurn` 里格挡清零改为 `If(Not(BlockClearPrevented(...)), SetField(Block, 0))`。写法与既有的 `EnergyGainBlocked` 同款（**声明驱动**：没声明的能力不会进判定链） |
| `SliceAuthorFrontend.cs` | `AuthorPower`/`SlicePower` 各加 `PreventsBlockClear`；作者文本键 `prevents-block-clear` |
| `SpirePowerSource.cs` | 正式字段 `preventsBlockClear = true` → 该标志；`BarricadePower` → `base-spire:barricade` 进**规范 id 表**；运行时实现白名单加 `barricade` |
| `SlicePublicProfile.cs`（net） | 抽出 `CardRule(ids)` 复用字段处置；把新卡 `base-spire:barricade` 与能力 `base-spire:barricade` **只加进作者变体**（默认 Profile 不变 ⇒ 受控 M1 与作者扩展路径的 host 能力分开统计） |

### 四态与行为证据

```text
卡牌：ironclad-cards.spire 声明 94 / 解析 94 / 降级 29（+Barricade）/ 未支持 65
能力：ironclad-powers.spire 声明 27 / 解析 27 / 降级  4（+barricade）/ 未支持 23
      shared-powers 7/7/4；potion-powers 1/1/0 ⇒ 能力合计降级 8
```

行为检查（**带对照**，切片 **221/221**）：

| 组 | 判据 |
| --- | --- |
| 对照组（无 Barricade） | 先打 Defend 攒 5 格挡 ⇒ 结束回合 ⇒ 格挡 **0**（机制是它带来的差异，而不是"格挡本来就不清"） |
| 实验组（有 Barricade） | 打出 Barricade 得 1 层 ⇒ 再攒 5 格挡 ⇒ 结束回合 ⇒ 格挡仍是 **5** |

### 验证（本批次实跑，返回码全 0）

| 命令 | 结果 |
| --- | --- |
| `dotnet build AutoTheSpire.sln` | 0 错误 0 警告 |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | **221/221**（220 → +1） |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | 391/391（作者变体新增内容未破坏既有投影检查） |
| `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` / `Core.TheSpire.Tests` | 64/64 / 190/190 |
| `python tools/dependency-check/check_dependencies.py` | 9/9 |
| `python tools/coverage-check/check_slice_coverage.py` / `check_contracts.py` | 1/1 / 210/210 |
| `python ai-the-spire/tests/run_checks.py` / `j01_episode.py` | 604/604 / 219/219 |
| `python net-the-spire/client/test_client.py` / `test_author_content.py` | 50/50 / rc=0 |

### 未结项（W-06 剩余；下一步按"先补机制再迁内容"逐类推进）

1. **能力机制缺口（27 个）**：`cardRule`（改费用/额外打出/结果区）、`events`（格挡后/受伤后触发）、
   `incomingDamageModifiers`、回合开始触发（`afterPlayerTurnStart`）、`isTemporary`/`onExpire`、
   `preventsBlockClear` 之外的 `prevents*` 系列。每一类都要先在 Engine/切片补机制，再迁能力与卡。
2. **卡牌 65 张未支持**：多数依赖上面的机制；其余是控制流/选择/其它数值来源/X 费。
3. **enemy / potion 正式源**仍走旧路径。
4. **默认后端不切换**；范围闭合后才谈切换与隔离兼容程序集。
5. 本轮**没有**动 W-01/W-02/W-03/W-04/W-05/W-07/W-08 的范围。

---

## 批次 41（2026-10-03）：W-06 `incomingDamageModifiers` 机制与 Tank

认领：**W-06** 第六段交付，继续"先补机制再迁内容"。这一例的机制**运行时已有**
（`multiply-incoming`，Vulnerable 在用），因此工作量集中在**忠实映射 + 行为证据**，
以及过程中撞出来的一个**真缺陷**。

### 改动

| 位置 | 改动 |
| --- | --- |
| `SpirePowerSource.cs` | `incomingDamageModifiers = [modifier(multiplier=M, require=[move], exclude=[unpowered])]` → `multiply-incoming:M`（只接受单个 `modifier(...)` 且该 require/exclude 组合；其它形态记为未支持）。`TankPower`→`base-spire:tank-power` 进规范 id 表与运行时白名单 |
| `SlicePublicProfile.cs`（net） | 作者变体登记 `base-spire:tank`（卡）与 `base-spire:tank-power`（能力）；默认 Profile 不变 |
| **修的真缺陷** | 正式源里 `applyPower` **省略 `target`**（Tank 就没写）时，此前按"所选目标"处理 ⇒ 生成"打给 None"的能力施加，引擎报 `relation owner must be an instance reference but is None`。现在按**卡牌自己的目标域**解释：`selectSelf` ⇒ owner、否则 ⇒ 所选目标（与 Inflame 显式写 `context.owner` 的语义一致） |

### 四态与行为证据

```text
卡牌：ironclad-cards.spire 声明 94 / 解析 94 / 降级 30（+Tank）/ 未支持 64
能力：ironclad-powers.spire 声明 27 / 解析 27 / 降级  5（+tank-power）/ 未支持 22；合计降级 9
```

行为检查（切片 **222/222**；**用实测对照，不写死数字**）：

| 组 | 判据 |
| --- | --- |
| 对照组（无 Tank） | 连续两个回合结束（第一动 Incantation 不造成伤害、第二动 DarkStrike）⇒ 实测受击 **10** 点 |
| 实验组（有 Tank） | 打出 Tank 得 1 层 TankPower ⇒ 同样两回合 ⇒ 实测受击 **20** 点（正好是对照的 **2 倍**）；且 TankPower 无移除声明 ⇒ 跨回合仍在 |

### 验证（本批次实跑，返回码全 0）

| 命令 | 结果 |
| --- | --- |
| `dotnet build AutoTheSpire.sln` | 0 错误 0 警告 |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | **222/222**（221 → +1） |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | 391/391 |
| `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` / `Core.TheSpire.Tests` | 64/64 / 190/190 |
| `python tools/dependency-check/check_dependencies.py` | 9/9 |
| `python tools/coverage-check/check_slice_coverage.py` / `check_contracts.py` | 1/1 / 210/210 |
| `python ai-the-spire/tests/run_checks.py` / `j01_episode.py` | 604/604 / 219/219 |
| `python net-the-spire/client/test_client.py` / `test_author_content.py` | 50/50 / rc=0 |

### 未结项（W-06 剩余）

1. **需要真正补机制的还剩**：`events`（格挡后/受伤后触发，如 FlameBarrier 的反击、Juggernaut 的
   格挡转攻击）、`cardRule`（改费用/额外打出/结果区，如 Corruption/FreeAttack）、
   回合开始触发（DemonForm/SelfFormingClay）、`isTemporary`/`onExpire`。每类都要先补运行时机制。
2. **卡牌 64 张、能力 26 个未支持**（多数依赖上面这些机制）。
3. **enemy / potion 正式源**仍走旧路径。
4. **默认后端不切换**；范围闭合后才谈切换与隔离兼容程序集。
5. 本轮**没有**动 W-01/W-02/W-03/W-04/W-05/W-07/W-08 的范围。

---

## 批次 42（2026-10-03）：W-06 `events` 机制（受伤后反击）与 FlameBarrier

认领：**W-06** 第七段交付，"先补机制再迁内容"的第三例，也是**事件订阅**这一类机制的第一个代表。

### 补的机制

| 位置 | 改动 |
| --- | --- |
| `CombatSlicePackage.cs` | ① 新增过程 `TriggerAfterDamageTaken(Attacker)`：`RetaliateChain` 把**声明了 `trigger = after_damage_taken`** 的能力展开成"读玩家层数 → >0 就对伤害来源造成 `N×层数` 点**无修正**伤害"；② `DamagePlayer` 里在**powered 且正数**伤害之后调用它（对应来源事件的 `require=[move]`/`exclude=[unpowered]`/`positiveAmount=false`）；③ 新增 `ExpireAtOpponentTurnEnd`：`EndEnemyTurn` 里扫描**玩家持有**的 `remove-at-opponent-turn-end` 能力并移除；④ `ResolveOwnerTurnEnd` 对该形态保持空操作（不能在拥有者自己的回合末提前移除） |
| `SliceAuthorFrontend.cs` | 新增触发点 `after_damage_taken`、触发效果前缀 `retaliate-per-stack:`、回合末形态 `remove-at-opponent-turn-end`（作者文本词表同步） |
| `SpirePowerSource.cs` | `events = [event(...)]` → 上述触发链（事件类型/owner/require/exclude/positiveAmount/数值来源/目标**逐一匹配**，不符即记为未支持）；`removeAfterOpponentTurnEnd = true` → `remove-at-opponent-turn-end`；`FlameBarrierPower`→`base-spire:flame-barrier` 进 id 表与白名单 |

**边界（写在代码注释与白名单说明里）**：这条链当前只展开**玩家持有**的能力（来源里该事件的 `owner = target`，
玩家是受害方）；敌人持有的同类能力、以及 `events` 的其它形态（`type=block` 的 Juggernaut、
`randomAttack` 等）仍记为未支持。

### 四态与行为证据

```text
卡牌：ironclad-cards.spire 声明 94 / 解析 94 / 降级 31（+FlameBarrier）/ 未支持 63
能力：ironclad-powers.spire 声明 27 / 解析 27 / 降级  6（+flame-barrier）/ 未支持 21；合计降级 10
```

行为检查（切片 **223/223**）：

| 判据 | 结果 |
| --- | --- |
| FlameBarrier 卡本身 | 12 点格挡 + 4 层（多原语 `gainBlock` + `applyPower`） |
| 受伤后反击 | 两个敌人（各造成 9 / 1 点）**各被反击 4 点** ⇒ 反击落在**每一次命中的来源**上，而不是"反击一次" |
| 触发条件 | 玩家确实掉血（powered 命中）；对照组（没有该能力）敌人毫发无损 |
| 对手回合末移除 | 覆盖完这个敌人回合后层数归 **0**（`removeAfterOpponentTurnEnd`），因此只覆盖敌人的一个回合 |

**受控输入与理由（写进检查注释）**：本受控场景里敌人的第一动是念咒（不造成伤害），而该能力只覆盖
**一个**敌人回合，因此检查在敌人的**攻击**回合前把 4 层直接挂到玩家身上——否则观察不到"受伤 → 反击 →
对手回合末移除"这条完整链路。这是**测试输入**，不是机制取巧。

### 验证（本批次实跑，返回码全 0）

| 命令 | 结果 |
| --- | --- |
| `dotnet build AutoTheSpire.sln` | 0 错误 0 警告 |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | **223/223**（222 → +1） |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | 391/391 |
| `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` / `Core.TheSpire.Tests` | 64/64 / 190/190 |
| `python tools/dependency-check/check_dependencies.py` | 9/9 |
| `python tools/coverage-check/check_slice_coverage.py` / `check_contracts.py` | 1/1 / 210/210 |
| `python ai-the-spire/tests/run_checks.py` / `j01_episode.py` | 604/604 / 219/219 |
| `python net-the-spire/client/test_client.py` / `test_author_content.py` | 50/50 / rc=0 |

### 未结项（W-06 剩余）

1. **`events` 的其它形态**：`type=block`（Juggernaut：获得格挡时随机攻击）、`randomAttack`、
   `events` 挂在**敌人**身上的情形——都需要各自的机制与命名随机流证据。
2. **`cardRule`**（改费用/额外打出/结果区：Corruption/FreeAttack/OneTwoPunch）、
   回合开始触发（DemonForm/SelfFormingClay）、`isTemporary`/`onExpire` 仍缺机制。
3. **卡牌 63 张、能力 25 个未支持**。
4. **enemy / potion 正式源**仍走旧路径；**默认后端不切换**。
5. 本轮**没有**动 W-01/W-02/W-03/W-04/W-05/W-07/W-08 的范围。

---

## 批次 43（2026-10-03）：W-06 回合开始触发机制与 DemonForm

认领：**W-06** 第八段交付，"先补机制再迁内容"的第四例：**回合开始触发**。

### 补的机制

| 位置 | 改动 |
| --- | --- |
| `CombatSlicePackage.cs` | 新增过程 `TriggerAfterPlayerTurnStart`：`GrantPowerChain` 把**声明了 `trigger = after_player_turn_start`** 的能力展开成"读玩家自身层数 → >0 就给自己施加 `n × 层数` 层 `<linked-power>`"；在 `StartPlayerTurn` 里**抽牌之前**调用（回合计数/能量/格挡重置之后） |
| `SliceAuthorFrontend.cs` | 新增触发点 `after_player_turn_start`、触发效果前缀 `gain-power-per-stack:`（作者文本词表同步） |
| `SpirePowerSource.cs` | `trigger = afterPlayerTurnStart` → 该触发点；`effect = grantPower(<能力>, sourceStacks)` → `gain-power-per-stack:1:<能力 id>`（**降级时**就把名字解析成 id，运行时不再做名字解析；其它 `grantPower` 形态与 `sequence(...)` 仍记为未支持）；`DemonFormPower`→`base-spire:demon-form` 进 id 表与白名单 |

### 四态与行为证据

```text
卡牌：ironclad-cards.spire 声明 94 / 解析 94 / 降级 32（+DemonForm）/ 未支持 62
能力：shared-powers 7/7/降级 5（+demon-form）；合计降级 11
```

行为检查（切片 **224/224**）：

| 组 | 判据 |
| --- | --- |
| 对照组（无 DemonForm） | 回合推进不会凭空获得力量（0 层） |
| 实验组 | 打出 DemonForm（2 层）当回合力量仍为 0（触发点是**回合开始**）；第 2 回合开始 **+2 力量** ⇒ 打击 6 → **8**；第 3 回合开始再 +2 ⇒ 累计 **4**（每回合都触发） |

### 验证（本批次实跑，返回码全 0）

| 命令 | 结果 |
| --- | --- |
| `dotnet build AutoTheSpire.sln` | 0 错误 0 警告 |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | **224/224**（223 → +1） |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | 391/391 |
| `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` / `Core.TheSpire.Tests` | 64/64 / 190/190 |
| `python tools/dependency-check/check_dependencies.py` | 9/9 |
| `python tools/coverage-check/check_slice_coverage.py` / `check_contracts.py` | 1/1 / 210/210 |
| `python ai-the-spire/tests/run_checks.py` / `j01_episode.py` | 604/604 / 219/219 |
| `python net-the-spire/client/test_client.py` / `test_author_content.py` | 50/50 / rc=0 |

### 未结项（W-06 剩余）

1. **`events` 的其它形态**：`type=block` + `randomAttack`（Juggernaut）、`events` 挂在敌人身上的情形。
2. **`cardRule`**（改费用/额外打出/结果区：Corruption/FreeAttack/OneTwoPunch）、
   `sequence(...)`/`removePower(...)`（SelfFormingClay）、`isTemporary`/`onExpire`（FlexPotionPower）仍缺机制。
3. **卡牌 62 张、能力 24 个未支持**。
4. **enemy / potion 正式源**仍走旧路径；**默认后端不切换**。
5. 本轮**没有**动 W-01/W-02/W-03/W-04/W-05/W-07/W-08 的范围。

---

## 批次 44（2026-10-03）：W-06 牌规则 `cardRule` 与 Corruption/Unrelenting

认领：**W-06** 第九段交付，"先补机制再迁内容"的第五例，也是剩余机制里**最大的一类**（按牌类型改写）。

### 补的机制（三条路径，全部**声明驱动**）

| 路径 | 实现 |
| --- | --- |
| **费用** | `CardRuleCostAllows`：合法性 guard 写成"按**基础**费用够 **或** 按**规则**费用够"（guard 是表达式，不能先算局部变量）；`PayCostSteps` 在主体阶段把费用写进局部变量并按**牌规则**改写，付款取 `min(基础, 规则)`（避免加价规则多扣）。X 费卡的"捕获剩余能量"路径**原样保留**（首版把它并进新分支，X 费检查当场失败，已修） |
| **结果区** | `resultPile=exhaust`：按**牌类型**匹配（构建期判定，类型是声明事实），只有"是否持有该能力"需要运行时表达式；进消耗区时照常走 `on_exhaust` 触发 |
| **一次性** | `consumeOnPlay`：打出这类牌后消耗该能力。判据是"**打出这张牌之前**是否持有"——在卡片体开头捕获到局部变量、结算后读取。**首版按结算后判定**，结果 Unrelenting 把自己刚施加的 FreeAttackPower 立刻吃掉，被新检查抓出 |

正式能力源把 `cardRule = cardRule(type=…, cost=N, resultPile=exhaust, consumeOnPlay=true)` 映射成这些字段；
`extraPlays`/`consumeOnPlayCount`（OneTwoPunch 的「额外打出」）**仍记为未支持**（需要额外打出机制）。
`CorruptionPower`/`FreeAttackPower` 进 id 表与白名单。

### 四态与行为证据

```text
卡牌：ironclad-cards.spire 声明 94 / 解析 94 / 降级 34（+Corruption、+Unrelenting）/ 未支持 60
能力：ironclad-powers.spire 声明 27 / 解析 27 / 降级 8（+corruption-power、+free-attack-power）/ 未支持 19；合计降级 13
```

行为检查（切片 **226/226**）：

| 组 | 判据 |
| --- | --- |
| Corruption（对照） | 没有该能力时 Defend 花 **1** 点能量并进**弃牌堆** |
| Corruption（实验） | 花 3 费打出 Corruption（1 层）后：Defend **0 费**（能量保持 0）、5 点格挡照常、牌进**消耗区**且不在弃牌堆 |
| Unrelenting（实验） | 2 费打出（1 层 FreeAttackPower）⇒ 攻击牌 **0 费**（能量保持 1）；`consumeOnPlay` ⇒ 打出一次攻击后该能力**层数归 0**；第二张攻击恢复 **1 费**（一次性判据） |

### 验证（本批次实跑，返回码全 0）

| 命令 | 结果 |
| --- | --- |
| `dotnet build AutoTheSpire.sln` | 0 错误 0 警告 |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | **226/226**（224 → +2） |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | 391/391 |
| `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` / `Core.TheSpire.Tests` | 64/64 / 190/190 |
| `python tools/dependency-check/check_dependencies.py` | 9/9 |
| `python tools/coverage-check/check_slice_coverage.py` / `check_contracts.py` | 1/1 / 210/210 |
| `python ai-the-spire/tests/run_checks.py` / `j01_episode.py` | 604/604 / 219/219 |
| `python net-the-spire/client/test_client.py` / `test_author_content.py` | 50/50 / rc=0 |

### 未结项（W-06 剩余）

1. **「额外打出」机制**（OneTwoPunch 的 `extraPlays=1`/`consumeOnPlayCount`）：需要"同一张牌再结算一次"的机制。
2. **`events` 的其它形态**：`type=block` + `randomAttack`（Juggernaut）、敌人侧触发链。
3. **`sequence(...)`/`removePower(...)`**（SelfFormingClay）、**`isTemporary`/`onExpire`**（FlexPotionPower）。
4. **提高费用**的牌规则与**敌人持有**的牌规则是显式边界（语料里没有，语义未定义到运行时就记为未支持）。
5. **卡牌 60 张、能力 22 个未支持**；enemy/potion 正式源仍走旧路径；默认后端不切换。
6. 本轮**没有**动 W-01/W-02/W-03/W-04/W-05/W-07/W-08 的范围。

---

## 批次 45（2026-10-03）：W-06 获得格挡后触发、随机攻击与 Juggernaut

认领：**W-06** 第十段交付，"先补机制再迁内容"的第六例（事件机制的第二个形态）。

### 补的机制

| 位置 | 改动 |
| --- | --- |
| `CombatSlicePackage.cs` | 新增过程 `TriggerAfterBlockGained`：`RandomAttackChain` 把**声明了 `trigger = after_block_gained`** 的能力展开成"读玩家层数 → >0 就按**命名随机流**抽一个存活敌人，造成 `n×层数` 点**无修正**伤害"；在 `GainPlayerBlock` 写完格挡之后调用 |
| 随机目标 | **复用既有原语** `SamplePick(SliceManifest.RandomTargetStream, Collection(monsters), slot)`——与 `damage_random` 步骤同一套：采样前由引擎按公开语义键排序，因此**可复现且可见**（没有新造随机源） |
| `SliceAuthorFrontend.cs` | 新增触发点 `after_block_gained`、触发效果前缀 `random-attack-per-stack:` |
| `SpirePowerSource.cs` | block 事件 `randomAttack(scaled(0, n, count=targetPowerAmount(自己, target=self)), properties=[unpowered])` → 该形态；`scaledAttack`（damage 事件）与 `randomAttack`（block 事件）**共用一个形状读取器** `ReadScaledAttackShape`；`JuggernautPower`→`base-spire:juggernaut-power` 进 id 表与白名单 |

边界（写在注释里）：这条链只对**玩家获得格挡**展开（切片里只有玩家会获得格挡）。

### 四态与行为证据

```text
卡牌：ironclad-cards.spire 声明 94 / 解析 94 / 降级 35（+Juggernaut）/ 未支持 59
能力：ironclad-powers.spire 声明 27 / 解析 27 / 降级 9（+juggernaut-power）/ 未支持 18；合计降级 14
```

行为检查（切片 **227/227**）：

| 组 | 判据 |
| --- | --- |
| 对照组（无 Juggernaut） | 获得格挡不造成任何伤害 |
| 实验组 | 打出 Juggernaut（6 层）后每次获得格挡：两个敌人的血量合计 **-6**（随机目标 ⇒ 用合计断言，不写死是哪一只） |
| **无修正**判据 | 受控给玩家 2 层力量后，这次随机攻击仍是 **6** 点（力量没有加进去，对应来源的 `properties=[unpowered]`） |
| 格挡本身 | 触发不消耗格挡（玩家仍有 5 点） |

顺带修：旧的"链接失败"检查断言的是 Juggernaut（现已支持），改用仍不支持的 JugglingPower（`cardPlaysThisTurn` + `cloneCard`），避免断言过时。

### 验证（本批次实跑，返回码全 0）

| 命令 | 结果 |
| --- | --- |
| `dotnet build AutoTheSpire.sln` | 0 错误 0 警告 |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | **227/227**（226 → +1） |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | 391/391 |
| `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` / `Core.TheSpire.Tests` | 64/64 / 190/190 |
| `python tools/dependency-check/check_dependencies.py` | 9/9 |
| `python tools/coverage-check/check_slice_coverage.py` / `check_contracts.py` | 1/1 / 210/210 |
| `python ai-the-spire/tests/run_checks.py` / `j01_episode.py` | 604/604 / 219/219 |
| `python net-the-spire/client/test_client.py` / `test_author_content.py` | 50/50 / rc=0 |

### 未结项（W-06 剩余）

1. **「额外打出」机制**（OneTwoPunch 的 `extraPlays=1`/`consumeOnPlayCount`）。
2. **`sequence(...)`/`removePower(...)`**（SelfFormingClay）、**`isTemporary`/`onExpire`**（FlexPotionPower）。
3. **`cloneCard`/`cardPlaysThisTurn`**（Juggling）、**`pileCount` 之外的数值来源**（`cardsWithTag`/`targetPowerAmount`/`energyX` 等）。
4. **敌人侧触发链**与**提高费用的牌规则**是显式边界。
5. **卡牌 59 张、能力 21 个未支持**；enemy/potion 正式源仍走旧路径；默认后端不切换。
6. 本轮**没有**动 W-01/W-02/W-03/W-04/W-05/W-07/W-08 的范围。

---

## 批次 46（2026-10-03）：W-06 `sequence` + `removePower` 与 SelfFormingClay

认领：**W-06** 第十一段交付，"先补机制再迁内容"的第七例：把"一次触发"从**单效果**推广成**多效果**。

### 补的机制

| 位置 | 改动 |
| --- | --- |
| `SliceAuthorFrontend.cs` | 触发效果新增两种：`gain-block:&lt;n&gt;`（**固定数值**格挡，与既有的 `gain-block-per-stack:` 按层数形态区分）与 `remove-self`（在同一次触发里移除自己）；一次触发的多个效果用 `|` 连接（`PowerTriggerEffect.SequenceSeparator`），**执行顺序＝声明顺序** |
| `CombatSlicePackage.cs` | 回合开始链按 `|` 拆分效果并逐个展开（`gain-power-per-stack` / `gain-block` / `remove-self`）；`TriggerAfterPlayerTurnStart` 的过程声明**移到 `GainPlayerBlock` 之后**（`gain-block` 效果需要它的符号——这是本轮撞到的编译期顺序约束） |
| `SpirePowerSource.cs` | `effect = sequence(A, B)` → `A\|B`：序列成员支持 `gainBlock(n, target=self)`（**不**支持按层数形态）、`removePower(自己)`、`grantPower(<能力>, sourceStacks)`；`SelfFormingClayPower`→`base-spire:self-forming-clay` 进 id 表与白名单 |

### 四态与行为证据

```text
能力：shared-powers.spire 声明 7 / 解析 7 / 降级 6（+self-forming-clay）/ 未支持 1；合计降级 15
卡牌：ironclad-cards.spire 声明 94 / 解析 94 / 降级 35 / 未支持 59（该能力由遗物施加，语料里没有对应卡）
```

行为检查（切片 **228/228**）：

| 组 | 判据 |
| --- | --- |
| 对照组 | 没有该能力时，回合开始不会凭空得到格挡（0） |
| 实验组 | **受控输入**挂上 1 层（遗物类能力，语料里没有卡）：玩家第 2 回合开始得到 **3 点格挡** |
| 一次性判据 | 同一次触发里的第二个效果 `removePower(自己)` ⇒ 该能力**被移除**；再推进一个回合也不再给格挡 |

### 验证（本批次实跑，返回码全 0）

| 命令 | 结果 |
| --- | --- |
| `dotnet build AutoTheSpire.sln` | 0 错误 0 警告 |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | **228/228**（227 → +1） |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | 391/391 |
| `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` / `Core.TheSpire.Tests` | 64/64 / 190/190 |
| `python tools/dependency-check/check_dependencies.py` | 9/9 |
| `python tools/coverage-check/check_slice_coverage.py` / `check_contracts.py` | 1/1 / 210/210 |
| `python ai-the-spire/tests/run_checks.py` / `j01_episode.py` | 604/604 / 219/219 |
| `python net-the-spire/client/test_client.py` / `test_author_content.py` | 50/50 / rc=0 |

### 未结项（W-06 剩余）

1. **「额外打出」机制**（OneTwoPunch 的 `extraPlays=1`/`consumeOnPlayCount`）。
2. **`isTemporary`/`onExpire`**（FlexPotionPower，药水侧）。
3. **`cloneCard`/`cardPlaysThisTurn`**（Juggling）、**`pileCount` 之外的数值来源**（`cardsWithTag`/`targetPowerAmount`/`energyX` 等）。
4. **控制流/选择类**卡牌原语：`chooseCard`/`repeat`/`forEachCard`/`withCount`/`autoPlay`/`drawUntil`/`generateCards`。
5. **敌人侧触发链**与**提高费用**的牌规则是显式边界。
6. **卡牌 59 张、能力 20 个未支持**；enemy/potion 正式源仍走旧路径；默认后端不切换。
7. 本轮**没有**动 W-01/W-02/W-03/W-04/W-05/W-07/W-08 的范围。

---

## 批次 47（2026-10-03）：W-06 卡级 hooks 与能力名链接大小写修正

认领：**W-06** 第十二段交付。本批次先做了一次**精确分诊**（把 59 张未支持卡按原因分组，
用环境变量 `W06_DUMP_UNSUPPORTED=1` 打开诊断输出），再挑其中**两处口径问题**下手——
一处是真缺陷（链接大小写），一处是纯机械缺口（卡级钩子）。

### 分诊结论（当时 59 张的成因）

| 成因 | 张数（约） |
| --- | --- |
| 卡级 `hooks` 非空（当时未支持） | 6 |
| 能力引用无法链接（该能力的行为未实现） | 16 |
| 控制流/选择类原语（`if`/`repeat`/`chooseCard`/`withCount`/`forEachCard`/`autoPlay`/`repeatForCount`/`repeatForX`/`drawUntil`/`generateCards`/`cloneCard`/`upgradeCards`/`randomMoveCard`/`heal`/`onFatal` 等） | 30+ |
| 数值来源（`cardsWithTag`/`targetPowerAmount`/`cardVariable`） | 3 |

### 本批次改的两处

| 位置 | 改动 |
| --- | --- |
| `SpireCardSource.cs`（hooks） | 新增 `ReadHooks`：`hooks = { onExhaust \| turnEndInHand \| onEnterCombat \| beforeCardPlayed \| afterCardPlayed }` 的每个块按**与 `play` 同一套规则**降级（抽出 `StepsFrom` 复用）；未知钩子名、需要选目标的钩子、**声明了 `on_exhaust` 却没有 `exhaust` 关键词**（死钩子，切片里没有别的路径消耗这张牌）都记为未支持；`Variant.Hooks` → `AuthorCard.NormalHooks/UpgradedHooks`（此前恒为 `None`） |
| `SpireCardSource.cs`（链接） | 能力名解析改为**大小写不敏感**：正式源里同一个能力并存 `Vulnerable`（Bash）与 `vulnerable`（Uppercut）两种写法，此前只认大写 ⇒ Uppercut 被误判成"链接失败"（**真缺陷**） |
| 顺带修正 | `loseHp` **不消费目标**：上一轮我加的"非 owner 目标即未支持"校验在"省略 target"的牌上误伤（Breakthrough 的 `loseHp = { amount = 1 }` 卡级目标是 `selectAll` ⇒ 被投影成 `target` 而被拒），现已把 `draw`/`loseHp`/`modifyEnergy` 排除出目标默认化 |

### 四态与行为证据

```text
卡牌：ironclad-cards.spire 声明 94 / 解析 94 / 降级 37（+Infection、+Uppercut）/ 未支持 57
      DrumOfBattle 因"onExhaust 死钩子"被**正确排除**（它的 hooks 声明了 onExhaust 却没有 exhaust 关键词）
能力：合计降级 15（本批次无新增能力）
```

行为检查（切片 **230/230**）：

| 组 | 判据 |
| --- | --- |
| Infection（`turnEndInHand = { loseHp 3 }`） | 留在手里回合末 **-3 HP**；对照（手里没有它）不掉 HP |
| Uppercut（`weak` / `vulnerable` 小写引用） | 打 **13** 点，并给所选目标 **1 层 Weak + 1 层 Vulnerable**（两个能力都链接成功） |

### 验证（本批次实跑，返回码全 0）

| 命令 | 结果 |
| --- | --- |
| `dotnet build AutoTheSpire.sln` | 0 错误 0 警告 |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | **230/230**（228 → +2） |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | 391/391 |
| `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` / `Core.TheSpire.Tests` | 64/64 / 190/190 |
| `python tools/dependency-check/check_dependencies.py` | 9/9 |
| `python tools/coverage-check/check_slice_coverage.py` / `check_contracts.py` | 1/1 / 210/210 |
| `python ai-the-spire/tests/run_checks.py` / `j01_episode.py` | 604/604 / 219/219 |
| `python net-the-spire/client/test_client.py` / `test_author_content.py` | 50/50 / rc=0 |

（诊断开关：`W06_DUMP_UNSUPPORTED=1` 会把每一张未支持卡及其原因逐行打印，便于下一轮分诊。）

### 未结项（W-06 剩余）

1. **控制流/选择类原语（最大一支，30+ 张）**：`if`、`repeat`、`withCount`、`repeatForCount`、
   `repeatForX`、`chooseCard`、`forEachCard`、`autoPlay`、`drawUntil`、`generateCards`、
   `cloneCard`、`upgradeCards`、`randomMoveCard`、`heal`、`onFatal` 等——多数需要新的运行时机制
   （选择、迭代、额外打出、随机移动牌）。
2. **15 个能力的行为未实现**（Rupture/DarkEmbrace/Vicious/Colossus/Aggression/OneTwoPunch/Pyre/
   Cruelty/SetupStrike/Hellraiser/Unmovable/Stampede/WellLaidPlans/CrimsonMantle/Inferno/Plating/NoDraw…）。
3. **数值来源**：`cardsWithTag`（PerfectedStrike）、`targetPowerAmount`（Bully）、`cardVariable`（Rampage）。
4. **敌人侧触发链**与**提高费用**的牌规则是显式边界。
5. **卡牌 57 张、能力 20 个未支持**；enemy/potion 正式源仍走旧路径；默认后端不切换。
6. 本轮**没有**动 W-01/W-02/W-03/W-04/W-05/W-07/W-08 的范围。

---

## 批次 48（2026-10-03）：W-06 条件效果 `if`、批次 `repeat` 与八个新卡

认领：**W-06** 第十三段交付，本阶段**单轮覆盖最大**的一次（卡牌 37 → 45）。

### 补的机制

| 机制 | 说明 |
| --- | --- |
| **条件效果 `if`** | `play` 里只有 `if` 时降级成作者模型的 `AuthorConditional`（与"固定步骤"**二选一**，沿用既有约束）。条件映射为运行时谓词：`playerLostHpThisTurn`（既有）、`cardExhaustedThisTurn`（**新增** Player 字段 `ExhaustedCardThisTurn`：进消耗区时置位、回合开始清零）、`hasPower { power, target = context.target }` → `target-has-power:<id>`（读所选目标身上的能力层数）、`pileCountAtLeast { pile, count }` → `pile-count-at-least:<pile>:<n>`（牌区张数比较，用既有 `Count` 原语）。判不出来的条件（`targetAlive`/`selectedCardIsClone`）、`if` 与其它原语**混用**、**嵌套 `if`** 仍记为未支持 |
| **批次 `repeat`** | `repeat = { count = N effect = { … } }` → 内层原语的 `Count *= N`，直接复用运行时**既有**的批次机制（无需新指令） |
| **统一准备** | 抽出 `PrepareSteps`：`play` 与条件分支共用"省略 `target` 按卡牌目标域解释 + 需要提问时在最前插 `target` 步骤"的规则 |

### 顺带修的两个真缺陷（都由新检查抓出）

1. **条件分支缺 `target` 步骤**：分支里的 `attack` 没有目标绑定点 ⇒ 引擎报
   `relation owner must be an instance reference but is None`；修法是把分支也走 `PrepareSteps` 并在最前插 `target`。
2. **条件变体的变体级 `Amount` 没从分支原语取**：`play` 里没有固定步骤，`amount` 默认 0 ⇒ Spite 打出 **0** 点；
   修法是从 then/else 的原语里取主数值与数值来源。

### 集成发现：新的公开字段必须显式映射（检查系统拦住）

新增 Player 字段后第一次跑 net：**45 项失败 + 作者探针 rc=3**，
原因 `N-02-UNDECLARED-FIELD: base-spire:Player.ExhaustedCardThisTurn 没有公开映射处置`。
按既有 `LostHpThisTurn` 的口径以 `N-02-DERIVABLE` **具名跳过**（它完全由公开的牌区变化推导），
而不是静默丢弃——这正是"不允许静默丢弃公开字段"这条规则在起作用。

### 四态与行为证据

```text
卡牌：ironclad-cards.spire 声明 94 / 解析 94 / 降级 45（+EvilEye、ForgottenRitual、Dismantle、PactsEnd、
      Spite、TwinStrike、FightMe、Conflagration）/ 未支持 49
能力：合计降级 15（本批次无新增能力）
```

行为检查（切片 **233/233**）：

| 卡 / 机制 | 判据 |
| --- | --- |
| Spite（`if playerLostHpThisTurn then repeat 2 × attack 5 else attack 5`） | 没掉过 HP ⇒ **5**；先用 Breakthrough 自损 1 点 ⇒ **10** |
| EvilEye（`if cardExhaustedThisTurn then repeat 2 × gainBlock 8 else gainBlock 8`） | 本回合没消耗过牌 ⇒ **8** 格挡；先打出 Impervious（消耗）⇒ **16** 格挡 |
| TwinStrike（`repeat 2 × attack 5`） | 同一目标 **2×5 = 10** 点 |

### 验证（本批次实跑，返回码全 0）

| 命令 | 结果 |
| --- | --- |
| `dotnet build AutoTheSpire.sln` | 0 错误 0 警告 |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | **233/233**（230 → +3） |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | 391/391 |
| `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` / `Core.TheSpire.Tests` | 64/64 / 190/190 |
| `python tools/dependency-check/check_dependencies.py` | 9/9 |
| `python tools/coverage-check/check_slice_coverage.py` / `check_contracts.py` | 1/1 / 210/210 |
| `python ai-the-spire/tests/run_checks.py` / `j01_episode.py` | 604/604 / 219/219 |
| `python net-the-spire/client/test_client.py` / `test_author_content.py` | 50/50 / rc=0 |

### 未结项（W-06 剩余）

1. **控制流/选择类原语（仍最大一支）**：`chooseCard`、`forEachCard`、`autoPlay`、`withCount`、
   `repeatForCount`、`repeatForX`、`drawUntil`、`generateCards`、`cloneCard`、`upgradeCards`、
   `randomMoveCard`、`heal`、`onFatal`、`applyScaledPower`、`modifyPowerVariable`。
2. **条件效果的边界**：`targetAlive`/`selectedCardIsClone` 谓词、`if` 与其它原语混用（Mangle/MoltenFist）、嵌套 `if`。
3. **20 个能力的行为未实现**；**3 类数值来源**（`cardsWithTag`/`targetPowerAmount`/`cardVariable`）。
4. **敌人侧触发链**与**提高费用**的牌规则是显式边界。
5. **卡牌 49 张、能力 20 个未支持**；enemy/potion 正式源仍走旧路径；默认后端不切换。
6. 本轮**没有**动 W-01/W-02/W-03/W-04/W-05/W-07/W-08 的范围。

---

## 批次 49（2026-10-03）：W-06 选择类 `chooseCard` 与三张新卡

认领：**W-06** 第十四段交付，攻"选择类"原语里运行时**最接近可用**的一支。

### 补的机制

| 位置 | 改动 |
| --- | --- |
| `SliceAuthorFrontend.cs` | 新作者步骤 `exhaust_from_hand`（词表 + 文档） |
| `CombatSlicePackage.cs` | 新增该步骤的降级：手牌非空时向玩家提问（候选＝当前手牌、恰好选 1 张）→ 从手牌移除 → 进消耗区 → 置"本回合消耗过牌"→ **跑消耗触发链**（FeelNoPain 等）；手牌为空时**不提问**（不产生空决策） |
| `SpireCardSource.cs` | `chooseCard` 两种形态映射：`{ from = hand, to = exhaust }` → `exhaust_from_hand`；`{ from = discard, to = draw, destinationPosition = top }` → **既有**的 `top_from_discard`（Headbutt 由此也能走正式源）。`to = none`（升级手牌）/`filter`/`onSelected`/其它组合记为未支持；`Primitive` 增加 `StepKind` 便于"直接对应某个步骤"的原语 |
| net | 作者变体登记 `base-spire:burning-pact`、`base-spire:brand`（`headbutt` 早已登记） |

### 四态与行为证据

```text
卡牌：ironclad-cards.spire 声明 94 / 解析 94 / 降级 48（+Headbutt、BurningPact、Brand）/ 未支持 46
能力：合计降级 15（本批次无新增能力）
```

行为检查（切片 **236/236**）：

| 卡 | 判据 |
| --- | --- |
| Headbutt | 先打 Defend 进弃牌堆 ⇒ 打 Headbutt：**9 点伤害** + 选中的 Defend **离开弃牌堆并回到抽牌堆** |
| BurningPact | 先拿 FeelNoPain（3 层）⇒ 打出后**选中的那张手牌进消耗区**、BurningPact 自己（`exhaust`）也进消耗区，且**触发 FeelNoPain（至少 +3 格挡）**——证明触发链作用在"被选中的那张牌"上 |
| Brand | 自损 **1** 点 HP + 选中的手牌进消耗区 + 给自己 **1 层力量** |

（断言细节：Headbutt 那条不能只比"弃牌堆张数"——打出的 Headbutt 自己也会进弃牌堆，
首版断言因此失败并被修正为"看**那张被选的牌**是否离开弃牌堆"。）

### 验证（本批次实跑，返回码全 0）

| 命令 | 结果 |
| --- | --- |
| `dotnet build AutoTheSpire.sln` | 0 错误 0 警告 |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | **236/236**（233 → +3） |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | 391/391 |
| `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` / `Core.TheSpire.Tests` | 64/64 / 190/190 |
| `python tools/dependency-check/check_dependencies.py` | 9/9 |
| `python tools/coverage-check/check_slice_coverage.py` / `check_contracts.py` | 1/1 / 210/210 |
| `python ai-the-spire/tests/run_checks.py` / `j01_episode.py` | 604/604 / 219/219 |
| `python net-the-spire/client/test_client.py` / `test_author_content.py` | 50/50 / rc=0 |

### 未结项（W-06 剩余）

1. **选择/控制流其它形态**：`to = none` + `onSelected = upgradeSelectedCard`（Armaments）、
   `randomMoveCard`（TrueGrit/Cinder/Thrash）、`forEachCard`、`autoPlay`、`withCount`、
   `repeatForCount`/`repeatForX`、`drawUntil`、`generateCards`、`cloneCard`、`upgradeCards`。
2. **目标域**：`selectRandom`（SwordBoomerang 等）。
3. **其它卡牌原语**：`heal`、`onFatal`、`applyScaledPower`、`modifyPowerVariable`、`attackVariable`。
4. **20 个能力的行为未实现**；**3 类数值来源**（`cardsWithTag`/`targetPowerAmount`/`cardVariable`）。
5. **敌人侧触发链**与**提高费用**的牌规则是显式边界。
6. **卡牌 46 张、能力 20 个未支持**；enemy/potion 正式源仍走旧路径；默认后端不切换。
7. 本轮**没有**动 W-01/W-02/W-03/W-04/W-05/W-07/W-08 的范围。

---

## 批次 50（2026-10-03）：W-06 随机目标攻击 `randomAttack` 与 SwordBoomerang

认领：**W-06** 第十五段交付，继续"复用既有机制"的便宜增量。

### 补的机制

| 位置 | 改动 |
| --- | --- |
| `SpireCardSource.cs` | `play` 支持原语 `randomAttack`（数值写在 `damage` 上）→ 降级成**既有**的 `damage_random` 步骤；目标域 `target = { selectRandom = {} }` 按"打出时不需要目标步骤"处理（与 `selectAll` 同类，因为目标由效果里的随机抽取决定）；变体级 `amount` 把 `randomAttack` 也算作主攻击数值（否则随机攻击会打出 0 点） |
| 随机语义 | **没有新造随机源**：`damage_random` 用的是 `SamplePick(RandomTargetStream, …)`，采样前由引擎按公开语义键排序 ⇒ 可复现、可见；批次机制让 `repeat 3 × randomAttack` 的三次命中各自独立抽样、各自计力量 |

### 四态与行为证据

```text
卡牌：ironclad-cards.spire 声明 94 / 解析 94 / 降级 49（+SwordBoomerang）/ 未支持 45
能力：合计降级 15（本批次无新增能力）
```

行为检查（切片 **237/237**）：

| 组 | 判据 |
| --- | --- |
| 对照（无力量） | `repeat 3 × randomAttack 3` ⇒ 两个敌人血量**合计 −9**（随机分布 ⇒ 断言合计，不写死是哪一只） |
| 受控给玩家 2 层力量 | 合计 **−15**（3 × (3+2)）⇒ 证明批次里的**每一次命中都是独立的 powered 攻击**（不是只算一次力量） |

### 验证（本批次实跑，返回码全 0）

| 命令 | 结果 |
| --- | --- |
| `dotnet build AutoTheSpire.sln` | 0 错误 0 警告 |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | **237/237**（236 → +1） |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | 391/391 |
| `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` / `Core.TheSpire.Tests` | 64/64 / 190/190 |
| `python tools/dependency-check/check_dependencies.py` | 9/9 |
| `python tools/coverage-check/check_slice_coverage.py` / `check_contracts.py` | 1/1 / 210/210 |
| `python ai-the-spire/tests/run_checks.py` / `j01_episode.py` | 604/604 / 219/219 |
| `python net-the-spire/client/test_client.py` / `test_author_content.py` | 50/50 / rc=0 |

### 未结项（W-06 剩余）

1. **升级手牌类**：`upgradeCards`、`chooseCard { to = none, filter = upgradeable, onSelected = upgradeSelectedCard }`（Armaments）。
2. **随机移动牌**：`randomMoveCard`（TrueGrit / Cinder / Thrash）。
3. **迭代/自动打出**：`forEachCard`、`autoPlay`、`withCount`、`repeatForCount`、`repeatForX`、`drawUntil`、`generateCards`、`cloneCard`。
4. **其它原语**：`heal`、`onFatal`、`applyScaledPower`、`modifyPowerVariable`、`attackVariable`。
5. **20 个能力的行为未实现**；**3 类数值来源**（`cardsWithTag`/`targetPowerAmount`/`cardVariable`）。
6. **敌人侧触发链**与**提高费用**的牌规则是显式边界。
7. **卡牌 45 张、能力 20 个未支持**；enemy/potion 正式源仍走旧路径；默认后端不切换。
8. 本轮**没有**动 W-01/W-02/W-03/W-04/W-05/W-07/W-08 的范围。

---

## 批次 51（2026-10-03）：W-06 随机消耗手牌 `randomMoveCard` 与两张新卡

认领：**W-06** 第十六段交付（"随机"这一族机制的第二支）。

### 补的机制

| 位置 | 改动 |
| --- | --- |
| `SliceAuthorFrontend.cs` | 新作者步骤 `exhaust_random_from_hand` |
| `SliceManifest.cs` | 新命名流 `base-spire:combat-random-card`——**刻意与随机目标流分开**：两条流的消费次数互不影响，重放/重编码不会互相挪动随机序列 |
| `CombatSlicePackage.cs` | 该步骤的降级：手牌非空时按命名流抽一张（`SamplePick`，候选在采样前由引擎按公开语义键排序）→ 移出手牌 → 进消耗区 → 置"本回合消耗过牌"→ 跑消耗触发链；手牌为空时什么也不做 |
| `SpireCardSource.cs` | `randomMoveCard { from = hand, to = exhaust }` → 该步骤；其它牌区组合记为未支持 |

### 四态与行为证据

```text
卡牌：ironclad-cards.spire 声明 94 / 解析 94 / 降级 51（+TrueGrit、+Cinder）/ 未支持 43
能力：合计降级 15（本批次无新增能力）
```

行为检查（切片 **239/239**）：

| 卡 | 判据 |
| --- | --- |
| TrueGrit（升级形态用 `chooseCard`，也已支持 ⇒ 两个变体都通） | 格挡 **7 + FeelNoPain 的 3 = 10** —— 证明消耗触发链作用在**被随机选中的那张牌**上（不是打出的 TrueGrit 自己）；并且恰好**一张**原手牌进了消耗区 |
| Cinder | 打 **18** 点，并随机消耗恰好一张原手牌 |

### 验证（本批次实跑，返回码全 0）

| 命令 | 结果 |
| --- | --- |
| `dotnet build AutoTheSpire.sln` | 0 错误 0 警告 |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | **239/239**（237 → +2） |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | 391/391 |
| `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` / `Core.TheSpire.Tests` | 64/64 / 190/190 |
| `python tools/dependency-check/check_dependencies.py` | 9/9 |
| `python tools/coverage-check/check_slice_coverage.py` / `check_contracts.py` | 1/1 / 210/210 |
| `python ai-the-spire/tests/run_checks.py` / `j01_episode.py` | 604/604 / 219/219 |
| `python net-the-spire/client/test_client.py` / `test_author_content.py` | 50/50 / rc=0 |

### 未结项（W-06 剩余）

1. **升级手牌类**：`upgradeCards`、`chooseCard { to = none, filter = upgradeable, onSelected = upgradeSelectedCard }`（Armaments）。
2. **迭代/自动打出**：`forEachCard`、`autoPlay`、`withCount`、`repeatForCount`、`repeatForX`、`drawUntil`、`generateCards`、`cloneCard`。
3. **变量类**：`variables`/`damageVariable`/`cardVariable`/`modifyPowerVariable`/`attackVariable`（Rampage/Thrash/CrimsonMantle/Inferno）。
4. **其它原语**：`heal`、`onFatal`、`applyScaledPower`（NotYet/Feed/Dominate）。
5. **20 个能力的行为未实现**；**数值来源**：`cardsWithTag`（PerfectedStrike）、`targetPowerAmount`（Bully）。
6. **敌人侧触发链**与**提高费用**的牌规则是显式边界。
7. **卡牌 43 张、能力 20 个未支持**；enemy/potion 正式源仍走旧路径；默认后端不切换。
8. 本轮**没有**动 W-01/W-02/W-03/W-04/W-05/W-07/W-08 的范围。

---

## 批次 52（2026-10-03）：W-06 手牌升级与 Armaments

认领：**W-06** 第十七段交付，打通"选择类"里最后一条还在等机制的支线：**选择一张手牌升级**。

### 补的机制

| 位置 | 改动 |
| --- | --- |
| `SliceAuthorFrontend.cs` | 新作者步骤 `upgrade_choose_from_hand` |
| `CombatSlicePackage.cs` | 该步骤的降级：候选＝`Where(hand, 未升级)`（`UpgradeableInHand`），非空时提问（恰好 1 张）→ 对**选中的那一个实例**调用既有的 `UpgradeOne`（写 `Upgraded`、费用、数值、层数）；没有候选时不提问 |
| `SpireCardSource.cs` | 正式源两形态映射：`chooseCard { from = hand, to = none, filter = upgradeable, onSelected = { upgradeSelectedCard } }` → 新步骤；`upgradeCards { pile = hand }` → **既有** `upgrade_hand`（其余牌区仍记为未支持） |
| `SliceManifest.cs` | 提问文案 `UpgradeChoicePrompt` |

### 四态与行为证据

```text
卡牌：ironclad-cards.spire 声明 94 / 解析 94 / 降级 52（+Armaments）/ 未支持 42
能力：合计降级 15（本批次无新增能力）
```

行为检查（切片 **240/240**）——断言用**手牌实例的 `Upgraded` 字段**，不假设具体选中哪一张：

| 形态 | 判据 |
| --- | --- |
| 普通形态（`upgradeSelectedCard`） | 打出后**恰好一张**手牌被升级（不是整手），且该实例的 Amount **6 → 9**（标准牌组起手是打击）；其余手牌保持普通形态 |
| 升级形态（`upgradeCards`） | 原来的手牌**全部**被升级（0 张保持普通形态） |

（过程记录：首版断言写死"选中的是某张 Defend"，被实际行为证伪——起手是 5 张打击，
候选里的第一张是打击而不是 Defend。这属于**检查自身**的假设错误，改为按字段断言后通过。
另外正式源模型的升级实例不能走 `PutInHand(upgraded: true)`（那条路查冻结清单、不认正式源新增卡），
改为显式写实例的 `Upgraded` 字段——运行时按**实例字段**分派普通/升级步骤，因此行为等价。）

### 验证（本批次实跑，返回码全 0）

| 命令 | 结果 |
| --- | --- |
| `dotnet build AutoTheSpire.sln` | 0 错误 0 警告 |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | **240/240**（239 → +1） |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | 391/391 |
| `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` / `Core.TheSpire.Tests` | 64/64 / 190/190 |
| `python tools/dependency-check/check_dependencies.py` | 9/9 |
| `python tools/coverage-check/check_slice_coverage.py` / `check_contracts.py` | 1/1 / 210/210 |
| `python ai-the-spire/tests/run_checks.py` / `j01_episode.py` | 604/604 / 219/219 |
| `python net-the-spire/client/test_author_content.py` | rc=0 |

### 未结项（W-06 剩余）

1. **迭代/自动打出**：`forEachCard`、`autoPlay`、`withCount`、`repeatForCount`、`repeatForX`、`drawUntil`、`generateCards`、`cloneCard`。
2. **变量类**：`variables`/`damageVariable`/`cardVariable`/`modifyPowerVariable`/`attackVariable`。
3. **其它原语**：`heal`、`onFatal`、`applyScaledPower`。
4. **20 个能力的行为未实现**；**数值来源**：`cardsWithTag`、`targetPowerAmount`。
5. **敌人侧触发链**与**提高费用**的牌规则是显式边界。
6. **卡牌 42 张、能力 20 个未支持**；enemy/potion 正式源仍走旧路径；默认后端不切换。
7. 本轮**没有**动 W-01/W-02/W-03/W-04/W-05/W-07/W-08 的范围。

---

## 批次 53（2026-10-03）：W-06 治疗与击杀奖励

认领：**W-06** 第十八段交付，两个"小原语"一次做完（NotYet / Feed）。

### 补的机制

| 位置 | 改动 |
| --- | --- |
| `SliceAuthorFrontend.cs` | 新步骤 `heal`（参数形态 `heal:<n>`）与 `on_fatal_gain_max_hp`（参数形态 `on_fatal_gain_max_hp:<n>`） |
| `CombatSlicePackage.cs` | `heal:<n>` → `Hp = Minimum(MaxHp, Hp + n)`；`on_fatal_gain_max_hp:<n>` → 紧跟攻击伤害之后判断**目标**生命是否 ≤ 0，成立则 `MaxHp += n` 且 `Hp += n` |
| `SpireCardSource.cs` | `heal { amount = n }` → `heal:n`；`attack { onFatal = { gainMaxHp = { amount = n } } }` → 攻击步骤后再补一条击杀奖励步骤（只支持这一形态，其它 `onFatal` 记为未支持）；卡级 `canGenerateInCombat` 具名接受为**生成约束**（注明"不改变打出行为"，不做静默忽略） |

### 四态与行为证据

```text
卡牌：ironclad-cards.spire 声明 94 / 解析 94 / 降级 54（+NotYet、+Feed）/ 未支持 40
能力：合计降级 15（本批次无新增能力）
```

行为检查（切片 **242/242**）：

| 卡 | 判据 |
| --- | --- |
| NotYet（`heal 10`） | 20 → **30**；另有受控用例把生命设为 `MaxHp - 4` ⇒ 只回到 **MaxHp**（被最大生命**截断**，不是无脑 +10） |
| Feed（`attack 10, onFatal = gainMaxHp 3`） | 先把目标压到 5 点生命 ⇒ 击杀且最大生命 **+3**；对照组（目标血量充足）最大生命**不变** ⇒ 证明奖励只在击杀时给 |

### 验证（本批次实跑，返回码全 0）

| 命令 | 结果 |
| --- | --- |
| `dotnet build AutoTheSpire.sln` | 0 错误 0 警告 |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | **242/242**（240 → +2） |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | 391/391 |
| `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` / `Core.TheSpire.Tests` | 64/64 / 190/190 |
| `python tools/dependency-check/check_dependencies.py` | 9/9 |
| `python tools/coverage-check/check_slice_coverage.py` / `check_contracts.py` | 1/1 / 210/210 |
| `python ai-the-spire/tests/run_checks.py` / `j01_episode.py` | 604/604 / 219/219 |
| `python net-the-spire/client/test_client.py` / `test_author_content.py` | 50/50 / rc=0 |

### 未结项（W-06 剩余）

1. **迭代/自动打出**：`forEachCard`、`autoPlay`、`withCount`、`repeatForCount`、`repeatForX`、`drawUntil`、`generateCards`、`cloneCard`。
2. **变量/缩放类**：`applyScaledPower`（Dominate）、`modifyPowerVariable`（CrimsonMantle/Inferno）、`variables`/`cardVariable`/`attackVariable`（Rampage/Thrash）。
3. **数值来源**：`cardsWithTag`（PerfectedStrike）、`targetPowerAmount`（Bully）。
4. **20 个能力的行为未实现**。
5. **敌人侧触发链**与**提高费用**的牌规则是显式边界。
6. **卡牌 40 张、能力 20 个未支持**；enemy/potion 正式源仍走旧路径；默认后端不切换。
7. 本轮**没有**动 W-01/W-02/W-03/W-04/W-05/W-07/W-08 的范围。









## 负责人规划调整：先交D1可运行版本（2026-10-03）

本次按用户“可交付/可见成果优先、减少歧义返工”的要求更新任务描述，没有修改生产实现或运行训练。新任务包为docs/design/battle-learning-delivery-v1.md；开工清单升v0.6，当前goal与review清单均以D1-A/B/C及R1为准。

关键决定：固定已有默认单战斗Gym，不加author/headbutt开关；先交可看单局，再交固定小预算真实采样更新，最后交三策略对照及smoke恢复。奖励/时间尺度、PPO参数、种子、8轮×4局及动作/时间/存储上限明确落在任务包，仅作工程演示，不代表正式产品偏好/晋升。完整W范围保留后续，不要求执行者完成全部正式源/丰富场景/整局才首次交付。

一个集成负责人拥有用户命令与成果，支持由用户安排；R1仅修已有Bully回归，不借此扩内容。新增问题按是否阻塞D1使用场景分诊。现有报告未整份归档，未将计划命令或产物宣称已实现。

文档检查：check_docs.py扫描161份Markdown，0问题；check_trace.py核对20条需求，0不一致；git diff --check返回0（已有文件只有换行转换提示）。本轮未重跑build/规则/真实内容加载/学习；实现状态沿用此前复核，D1具体命令仍待执行者交付。
