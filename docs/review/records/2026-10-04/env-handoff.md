# ENV 整改线交接（2026-10-04，执行线：environment-completion-and-differential-v1）

本文件是 `docs/review/tofix/README.md` 的**交接证据**，不是任务定义的替代品：
清单与开放项仍以 `tofix/README.md` 为准，本文件只回答"现在在哪、怎么验、下一步做什么、别再踩哪些坑"。

## 1. 当前实测状态（可复现）

| 指标 | 值 | 来源 |
| --- | --- | --- |
| 环境分母 | **1567**（16 类，每项带源文件:行） | `verify` 的 `denominator_has_sources` **通过** |
| 已降级 | **88** | `verify` 汇总行 |
| 仅旧后端 | 约 257 | 同上 |
| 未落地 | **1222** | 同上 |
| 无法结算 | **0** | `coverage_settleable` **通过** |
| 门 | **8 通过 / 6 开放**（共 14） | `verify` rc=1（"结算完整、环境未完成"，非工具失败） |
| 正式源 | 卡 **68** / 能力 **25**（全部实例化，缺口 0） | `engine_loaded_mechanism` **通过** |
| 行为已验证 | **53** 条（自报产物） | `behavior_verified`（开放） |
| 对拍 | 9 条内容 / 7 passed / 0 可计入首差 | `compare` rc=0 |
| 测试 | Engine **69**、Legacy **190**、Slice **268**、net **393** | 全绿 |
| `run` 命令 | **rc=1（部分实现）**：一场真实战斗 + 写回 + `EnterNextRoom` 推进，`status=flow_verified` | `artifacts/environment/run/run.json` |
| 其他 | build 0/0、依赖审计 9/9、`check_docs.py` 180 份 0 问题 | 全绿 |

**验证命令（按顺序，缺一不可）**

```powershell
dotnet build AutoTheSpire.sln
dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests
dotnet run --project core-the-spire/Core.TheSpire.Tests
dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests
dotnet run --project net-the-spire/Net.TheSpire.Tests
dotnet run --project tools/differential -- inventory --output artifacts/environment/inventory   # 必须先跑
dotnet run --project tools/differential -- verify    --output artifacts/environment/verify
dotnet run --project tools/differential -- compare --suite fixtures-v1 --output artifacts/environment/fixtures
dotnet run --project tools/differential -- rng
python tools/doc-check/check_docs.py .
python tools/dependency-check/check_dependencies.py
git diff --check
```

> `verify` 的 `baseline_frozen` 比对的是**冻结在 `inventory` 产物里的摘要**：任何玩法改动后**先跑 `inventory` 再跑 `verify`**，否则看到的是"快照过期"而非真漂移。

## 2. 怎么挑下一个目标（不要靠猜）

`tools/differential` 的 `inventory` 产物里有三份**推导出来**的清单：

| 字段 | 用途 |
| --- | --- |
| `base_spire.formal_frontend.unsupported_detail` | **逐条**未支持内容 + 具体缺哪个字段/原语（挑目标的第一入口） |
| `next_batch_mechanisms` | 机制依赖图（机制 × 挡住的内容数 × 内容名），按挡住多少排序 |
| `single_family_content` | 只命中一个**族**的候选清单（**会少报**：见下） |

**已测得的结论**：单机制目标**已经没有了**——所有只挡 1 条的单元要么是"能力名"（该能力自身被拒的下游症状），要么是更大族里的参数。后续必须是**多机制批**。

## 3. ENV-02 已落地的机制（含原版依据）

| 机制 | 内容 | 原版依据 |
| --- | --- | --- |
| `NoDraw`（抽不了牌） | BattleTrance | `Powers/NoDrawPower.cs` 的 `ShouldDraw` |
| 受击修正的**来源条件**（`sourcePower`） | Colossus | `ColossusPower.ModifyDamageMultiplicative` 的 `dealer.HasPower<Vulnerable>()` |
| 每层**最大能量** | Pyre | `PyrePower.ModifyMaxEnergy` |
| **易伤倍率**按攻击者层数 | Cruelty | `CrueltyPower.ModifyVulnerableMultiplier`（`amount + Amount/100`） |
| `on = [trigger(...)]` 两触发点 + `decrementPower` | Plating（+ `StoneArmor`） | `PlatingPower` 声明 |
| **临时力量**（`isTemporary`/`onStackChange`/`onExpire`） | Mangle、SetupStrike、FlexPotion | `TemporaryStrengthPower`（`BeforeApplied` + `AfterSideTurnEnd`） |
| **尾部条件段** + `targetAlive` | Mangle | `Cards/Mangle.cs` |
| **嵌套条件段** + 目标方向的缩放施加（`apply_power_target_from_target`） | MoltenFist | `Cards/MoltenFist.cs`（易伤翻倍） |
| **格挡乘区**（`blockGainModifiers` + 每回合计数） | Unmovable | `UnmovablePower.ModifyBlockMultiplicative` |
| **卡牌实例变量**（`variables`/`cardVariable`/`modifyCardVariable` → 既有 `variable:` 与 grow 机制） | Rampage | 批次 51 的既有机制 + `.spire` 前端映射 |
| **`power` 事件类型** + `draw-per-stack` | Vicious | `ViciousPower.events`（`type=power`） |
| 观察不消耗随机数（不变量） | —（ENV-01） | — |

**证据都在**：`mod-the-spire/base-spire/BaseSpire.Slice.Tests/SliceFrontend*Checks.cs`，每条都是"带对照"的判别性检查。

## 4. 开放项（按建议优先级）

1. **ENV-03 内容层**（**建议主线**）。**重要更正（2026-10-04 实测）**：整局**循环机制基本已完整**——
   ① 整局实体存在且初值合法；② 敌人全灭触发结算并写回 `Run.Hp` / `SettledRoom`（幂等键）/ `Player.BattleSettled`；
   ③ `EnterNextRoom` 是公开合法动作，执行后 `CurrentRoom` +1、`BattleSettled` 复位、幂等键留在上一间房，
   **且新房间里有 2 个敌人（新遭遇）**。三条都有带对照的判别性检查（`SliceFrontendRunWiringChecks` /
   `SliceFrontendRunSettlementChecks` / `SliceFrontendRoomAdvanceChecks`），`run` 命令端到端复现同一流程。
   **因此剩下的不是循环机制，而是内容/元层**：地图结构 `ACT 0/5`、遭遇作为数据 `ENCOUNTER 0 降级`（63 仅旧后端）、
   奖励 `RELIC 1/297` + `POTION 0/58`、事件 `EVENT 0/68`，以及"哪间房放哪个遭遇"的选择（当前由
   `StartNextRoom` 固定/轮转）。**建议下一批**：先把 act 的地图结构与遭遇数据接进正式路径
   （"3 个房间 + 每房一个真实遭遇"可声明），再做奖励结算。
2. **ENV-01 余项**：base 侧随机流**接线**（把切片抽牌流映到原版 `Shuffle` 的真实调用顺序）、decimal 数值保真。
   （"观察不消耗随机数"已在 `SliceFrontendObservationPurityChecks` 落定，带消耗反例。）
3. **卡牌长尾**（并行背景）：`Thrash`（还需 `attackVariable` + `accumulateCardDamage` + `damageVariable`）、`Stomp`（4 个机制：钩子里的条件段、`selectedCardIsClone`、缩放费用修正、`whenCardType`）、能力侧变量 `variables`+`powerVariable`（CrimsonMantle/Inferno，4 件事）、`HowlFromBeyond`（新时序点 `afterAutoPostPlay` + 自动打出机制）。**实测：单机制目标已经没有了**，后续都是多机制批。
4. **已登记待裁定**：`feel-no-pain` 的 CARD+POWER 命名空间歧义（`registries/id-exceptions.json`，改名属公共契约变更）；`AuthorRulesetV2/AuthorCatalogV2` 是否应按公开字段集升版；切片里"敌人施加易伤"这一类**只有构造证据**的界限（`after_power_applied` 按"施加对象不是玩家"收窄）。

## 5. 这一长会话踩过的坑（别再踩）

| 坑 | 后果 | 规则 |
| --- | --- | --- |
| 用 **PowerShell 改文本文件** | 中文被按 CP1252 误解 → 文件被写空 | **只用编辑工具/脚本按字节写**；这些目录在 git 里**未跟踪**，没有版本历史兜底 |
| 崩溃恢复 | `SliceChecks.cs` 曾被写空 | harness 在 `%TEMP%\dsh-workspace-changes-*\captures\` 留有**文件快照**；按内容找最新快照可**逐字节恢复**（本次已成功一次）；重建调度表会**静默缩水**（48 个类只还原出 232/257 条，因为原文件还有内联检查） |
| 位置参数记录里插字段 | 两个 `int` 字段**静默对调**（Cruelty 的百分比变成临时力量） | 新字段**追加在记录末尾**，或构造调用用命名参数 |
| `ProcedureBuilder.If(...)` 单独调用 | 指令被**静默丢弃**，钩子"看着写了"却什么都没做 | 指令必须放进 `Body(...)` 的参数数组或作为返回值 |
| 字符串里嵌 ASCII 双引号 | C# 编译错误 / JSON 解析失败 | 中文引号用「」 |
| 同回合重复用**同一张牌实例** | `MembershipConflict`（正式源卡组每张 1 份） | 换用**两张不同的牌**，或跨回合 |
| `.spire` 变量名与运行时字段名不一致 | 两条来源路径落到不同字段名，net 投影 `N-02-UNDECLARED-FIELD` | 变量名统一按 `ToId` 规范化为 **kebab** |
| 新增玩家/卡字段 | net 层守卫要求**显式公开映射处置**（"不允许静默丢弃公开字段"） | 按既有先例：作者变体发布、默认 Profile 记 `N-02-DERIVABLE` 具名跳过 |
| 只跑 `verify` 不重跑 `inventory` | `baseline_frozen` 假漂移 | 见第 1 节 |
| 为凑数仓促改模型 | 可能污染已验证的检查与对拍 | 预算不足时**整体回退到绿色**并写下精确计划（本轮线已这样做过两次） |
