---
task_id: ENV-T05
dispatch_id: ENV-T05-map-room-20261008
session_id: session-3a7026c3-9a61-4e44-9393-b7077d8a0fc1
dispatch_record: .audit/dsh-env-t05-map-room-2026-10-08/task-record.json
delivery_status: ready_for_review
updated_at: 2026-10-08T21:27:36+08:00
---

# ENV-T05 第一子包（真实地图节点 / 房间阶段 / 合法进入战斗）交接 — 2026-10-08

> **ready_for_review**（2026-10-08）：本包实现、验证与独立评审均已完成，等待验收/负责人裁定。结构按 [交接模板](https://github.com/softaworks/agent-toolkit)（`$auto-the-spire-handoff`）组织。

一句话：本包要让 `BaseSpireRuntimeFactory` 用同一个正式工厂创建、配置好但未 Start 的 Session，走**真实源地图**：公开可观察地图节点/合法后继/房间阶段，正常 Start → 选择真实合法节点 → 进入现有正常战斗 → 稳定战斗完成出口；暂未实现的房间明确 unsupported，不做假路线、不注入状态。真实奖励/选牌/长期提交留给 T05 下一子包。

归属：ENV-T05「第一条真实地图战斗奖励闭环」第一个完整机制子包。
接手入口：[主清单 ENV-T05 行](../../tofix/README.md)、[base 工作包 ENV-T05](../../../design/mod-the-spire/base-spire-implementation-plan.md#env-t05)、第 4 节源算法细节。
交付范围：会话执行者只写 `mod-the-spire/base-spire/`、必要 base 文档、本包 `.audit/dsh-env-t05-map-room-2026-10-08/` 与本文；`net/`、`tools/`、`reference/`、`sts2/`、solution、AI、主清单只读。
本报告记录的是 2026-10-08 的交付；当前排期、状态与认领以主清单为准。

## 接手后先做什么

1. 只在[主清单 ENV-T05 行](../../tofix/README.md) 确认当前状态与认领（本文件不写状态）；本包已交付的是"地图/房间阶段"这一段，**不要重做**地图算法（逐字移植 + 独立对拍见"验证与证据"）。
2. 复现最小命令（见"验证与证据"表）：先 `dotnet build AutoTheSpire.sln`，再 `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` 与 `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests`（base 内容/规则改动后两者都要跑）。
3. 下一子包（已就绪的方向）：**真实战斗奖励/选牌/跳过/容量 → 长期提交 → 返回地图**。接缝已留：结算后地图阶段停在"已结算、可再次选点"（见"已交付与接口变化"的 `MapPhase`），奖励阶段插在它之前；不要另建第二套 RunState 或 C# 调度器。
4. `PowerStacksOf` 首差先按"交接条目"核实，再决定是否报负责人裁定；不要用吞异常/删候选的方式变绿。

## 已交付与接口变化

已交付 = **源真实地图生成（逐字移植）+ 地图物化 + 房间阶段/父子上下文 + 合法后继选择 → 进入既有正常战斗 → 稳定战斗完成出口**。默认（`Dev`）路径**逐字节不变**（见"验证与证据"第 3/5/7 行）。

**范围身份（配置与公开身份都含入口差异）**
- `mod-the-spire/base-spire/BaseSpire/SinglePlayer/RunConfiguration.cs:46` `public const string DevMap = "base-spire-singleplayer-dev-map-v1"`；`:49` `Known = [Dev, DevMap, Ironclad, Full]`；`:56` `Implemented = [Dev, DevMap]`；`:72` `NotImplementedReason(DevMap)`；`:144` `Resolve()` 白名单加 `DevMap`。**默认值仍是 `Dev`** ⇒ 不传 `ScopeId` 的 `new RunConfiguration(...)`／`RunConfiguration.Ironclad(...)` 身份与行为不变（B 的 `rng-initial-shuffle` 证据仍有效）。
- 旧兼容入口用途明示：`EnterNextRoom` 仍是 **Dev（切片房间推进）** 的动作，**不是正常地图路径**；地图 scope 不注册它（`Slice/CombatSlicePackage.cs` 中守卫改为 `if (hasRun && actMap is null)`）。

**地图状态（全部是引擎类型化存储，无隐藏 C# RunState）**
- 实体 `MapNode`（`Slice/MapRoomWiring.cs:24`）字段 `Col`(0..6)、`Row`(0..16)、`Type`(enum `MapPointType`，`:23`，与源同序 `Unassigned…Ancient`)、`Index`、`Visited`（`:234-238`）；集合 `MapNodes`（`:25`，Sequence，身份/顺序可见）；关系 `MapChildren`（`:26`，`MapNode → MapNode` 多值自关联 = 源 `MapPoint.Children`）。
- `Run` 新字段（`:243-247`）：`CurrentNode`(`RefTo MapNode`, optional)、`MapPhase`(0..2)、`MapFloor`、`MapNodeCount`、`MapDraws`（审计用 payload 词表）。
- 公开登记：`SinglePlayer/BasePublicSemantics.cs:279` `["MapNodes"] = (Sequence, null, "Run", null, null, null)`。**T03 按声明派生**：新实体/字段/关系/集合都在包声明里，base 只补了无法推导的领域含义（intent 行）。

**公开动作与阶段机**
- `ChooseMapPoint`（`:27`，参数 `Node` = `RefTo MapNode`，`:28`）：候选 = `body.Relation(Children, body.Field(run, CurrentNode))`（`:462`，**与源 `MapTravel.GetTravelablePointsFrom` 同规则**，不引入 ±1 偏移）；守卫 = `MapPhase ∈ {0,2}` 且 `MapFloor < Run.RoomCount`。
- Body 先按房型分派：`Monster` → 写 `CurrentNode`、`Visited=true`、`MapFloor = Row+1`、`MapPhase=1`，再 `Call(StartNextRoom)` 启动**既有**正常战斗；其余房型 → 具名 `Fail("base-spire: 房间类型 X 尚未实现（地图节点保留、不伪装空事件）")`，**零字段写入、节点保留**。
- 阶段：`0` 开局可选点 → 选点进战斗 `1` → 结算过程末尾 `SettleStep` 置 `2`（`MapRoomWiring.cs:442`，追加在既有长期写回之后）→ `2` 可再次选点。**奖励子包接缝 = 在 `MapPhase=2` 之前插 pending-reward 阶段**（本包不实现奖励）。
- 非法/非相邻/阶段错提交由引擎在**任何状态/随机变更之前**拒（候选域 `core-the-spire/Core.TheSpire/Engine/Scheduler.cs:322-330`、guard `:332-333`，`Faults.Illegal` ⇒ `EngineFaultCode.IllegalAction`）⇒ 状态不变、会话仍 Running。

**接入点（`Slice/CombatSlicePackage.cs`，默认全关）**
- `Build(...)` 新增第 ⑦ 参 `bool actMapEnabled = false`（`:68`）；`actMap = MapRoomWiring.Declare(package, projection, runEntity!)`（`:418`，仅 `actMapEnabled && hasRun`）；结算追加 `actMap.SettleStep(settleBattle, settleRun)`（`:487`）；`setupSteps.InsertRange(2, actMap.SetupSteps(setup, runStates!))`（`:3771`）；`actMap.RegisterChooseMapPoint(package, runStates!, startNextRoom!, runRoomCount)`。

**随机与纯计算通道**
- 新流：`Slice/VanillaRandomLayout.cs:219-236` `ActMapStreamName(act)` = `base-spire:run:act_1_map`、`ActMapStreamType(1) = "act_1_map"`、`ActMapSeed(runSeed, 1) = StreamSeed(runSeed, "act_1_map")`（等价源 `StandardActMap.CreateFor :111-114` 的 `new Rng(seed, $"act_1_map")`）。只在 `DevMap` 种它（`BaseSpireRuntimeFactory.cs:153-157`）⇒ Dev 15 条流、DevMap 16 条。
- 抽数：`SetupSteps`（`MapRoomWiring.cs:306` 起）在 `Repeat` 上限内 `SampleInt("base-spire:run:act_1_map", 0, 2^53)`（`:340`），每次恰取一个 53 位 payload 存入词表 blob；纯计算资产按源顺序重放；**每个随机分支恰好一次消费、词表用尽即 `Fail`、不补齐**。
- 三个纯计算资产 + 后端（`Slice/SourceActMapCompute.cs`，`:62 Span = 1L << 53`、`:65 ActRooms = 15`、`:68 WordBudget = 512`）：`SourceActMapGenerate`/`Cell`/`Edge`；`BaseSpireComputeBackend` 对非地图资产逐字转发 `SourceWeightedPickBackend`。Compute 是**纯函数**（拿不到 Session/State、不抽随机），状态转换全部由 IR 指令完成。
- 移植件：`BaseSpire/Slice/SourceMap/`（12 个 `.cs` + `README-port.md`，命名空间 `BaseSpire.Slice.SourceMap`）；`BaseSpire/BaseSpire.csproj` **未改**（默认 `**/*.cs` 通配已包含）。

**给 C 的迁移要求**：`net-the-spire/Net.TheSpire.Host/FormalDevScenario.cs:26` 硬编码 `new(RunConfiguration.Ironclad())`（scope = Dev），`net/` 对本包只读 ⇒ 正常地图入口无法从 Host 命令行选择，需要 ruleset/scenario → `RunConfiguration(ScopeId = …)` 的映射；`net-the-spire/Net.TheSpire.Tests/FormalPathChecks.cs:62` 断言场景身份是 `BaseSpireScope.Dev` ⇒ **默认入口必须保持 Dev**。新增公开事实（`MapNode`/`MapNodes`/`MapChildren`/`ChooseMapPoint`）需要在 C 侧按公开语义透传实施，**不逐项重登记**。

**明确未实现（显式 unsupported，不伪装空事件、不删节点）**：商店/营火/宝箱/精英/未知/事件/BOSS 房型、幕 2-4 与幕转换、`hasSecondBoss`、`TryGenerate` 失败重试、真实奖励/选牌/跳过/容量、事件系统、地图外的 run 流程（金币/遗物/商店库存/营火选项/宝箱/精英遗物/BOSS 结算）。

## 源依据

冻结源（只读，逐条为实读行号；本包按这些行实现，不按参考实现改写）：

- **地图生成**：`sts2/src/Core/Map/StandardActMap.cs`（ctor `:87-108` 顺序 `GetNumberOfRooms+1 → Grid[7,16] → GetMapPointTypes → GenerateMap → AssignPointTypes → PruneAndRepair → CenterGrid → SpreadAdjacentMapPoints → StraightenPaths`；`GenerateMap :207-234` 7 条路径 + 起始点连第 1 行 + 第 15 行连 BOSS；`GenerateNextCoord :145-182` 三向洗牌与 `HasInvalidCrossover :184-205` 重抽；`AssignPointTypes :248-307` 固定行覆盖 + 队列配额；`AssignRemainingTypesToRandomPoints :364-385`/`GetNextValidPointType :387-403`；五条合法性规则 `:405-485`）。`MapPathPruning.cs`（`PruneAndRepair :22-32` 最多 3 轮；`RepairPrunedPointTypes :43-50` 固定序 Shop→Elite→RestSite→Unknown；`RepairPointType :52-79`；`PruneDuplicateSegments :84-97` 上限 50；`FindAllPaths :110-141`；`AddSegmentsToDictionary :143-175`；`PrunePaths :254-269`）。`MapPostProcessing.cs`（`CenterGrid :21-81`、`StraightenPaths :86-127`、`SpreadAdjacentMapPoints :197-247`，三者零随机）。`MapPoint.cs`、`ActMap.cs`、`MapPointTypeCounts.cs`（`NumOfElites :14`、`NumOfShops :16`、`StandardRandomUnknownCount :30-33`）。
- **本幕输入**：`sts2/src/Core/Models/Acts/Overgrowth.cs:47` `BaseNumberOfRooms => 15`（单机 `ActModel.cs:261-269`）；`:134-139` 先 rest `NextGaussianInt(7,1,6,7)` 再 unknown `NextGaussianInt(12,1,10,14)`；`Rng.cs:234-247` 高斯每轮恰好 2 个 `NextDouble`、`Math.Round` 银行家舍入、越界重试。asc0 精英 5（SwarmingElites 则 8）、商店 3。
- **流种子**：`StandardActMap.cs:111-114 CreateFor` = `new Rng(runState.Rng.Seed, $"act_{CurrentActIndex+1}_map")`；`Rng.cs:52-55`（种子 = runSeed + hash32(名)，**名不做 SnakeCase 但本名恒等**）；`RunRngSet.cs:104-119`。⇒ 本包 = `VanillaRandomLayout.SeedStream(service, "base-spire:run:act_1_map", seed, "act_1_map")`，**每次生成地图都是全新 counter-0 流**，绝不复用 `UpFront`/`UnknownMapPoint` 游标。
- **合法后继**：`sts2/src/Core/Map/MapTravel.cs:14-21` = `currentPoint.Children`（仅 `Hook.ShouldAllowFreeTravel` 时改为下一行整行；全仓只有 `Models/Modifiers/Flight.cs:5` 与 `Models/Relics/WingedBoots.cs:52` 覆盖，基类 `AbstractModel.cs:2424` 返回 false）。⇒ 本包"合法下一节点"就是 `MapChildren` 关系，不引入 ±1 偏移假设。
- **房间进入与阶段**：`sts2/src/Core/Runs/RunManager.cs:758-769 EnterMapCoord`（**只拒绝已访问**）、`:788-796`（actFloor = row+1）、`:801-865 EnterMapPointInternal`（历史 → `ExitCurrentRooms` → `RollRoomTypeFor` → `CreateRoom` → 事件房父子）；`:867-892 CreateRoom`（Monster/Elite/Boss = 从本幕队列 `PullNextEncounter` 拿遭遇）；`:902-921 RollRoomTypeFor`（`Unknown` 走赔率、`Ancient → Event`）。`RunState.cs:460-469 AddVisitedMapCoord`（**没有相邻/阶段校验**）、`:476-498 PushRoom/PopCurrentRoom`；`Map/MapPointState.cs`；`Nodes/Screens/Map/NMapScreen.cs:377-424 RecalculateTravelability`（可行驶权威）与 `:571 await RunManager.Instance.EnterMapCoord(coord)`。⇒ "合法性/阶段拒绝"**在源里由 UI/宿主把关**，本包按同一规则在动作 `Requires` 上把关（不新增源规则、不绕过引擎候选）。
- **随机映射事实（本包抽数通道成立的前提）**：引擎 vanilla profile `DrawDouble = (raw >> 11) * 2^-53`（`core-the-spire/Core.TheSpire/Engine/RandomProfile.cs:61-78`）⇒ `SampleInt(stream, 0, 2^53)` 恰为一个 53 位 payload；`RandomService.Shuffle`（`RandomService.cs:196-204`）与源 `ListExtensions.UnstableShuffle`（`ListExtensions.cs:45-60`）同序；引擎**没有**高斯原语 ⇒ base 在纯计算里用 `DrawDouble` 复刻 `NextGaussianInt`。
- **独立对照**：`reference/sts2-sim/src/Sts2Sim.Core/Map/*` 与 `reference/sts2-sim/tests/Sts2Sim.Core.Tests/Map/*`（另一份逐字移植，仅作差分对照；`reference/` 只读，对照在 `.audit/.../port-verify/` 的副本里跑）。

## 验证与证据

工作目录一律 `F:/Scylier-Project/active/auto-the-spire`。日志在 `.audit/dsh-env-t05-map-room-2026-10-08/logs/`，本机**没有 `pwsh`**（只有 `C:\WINDOWS\System32\WindowsPowerShell\v1.0\powershell.exe`）⇒ 官方 `pwsh -File …` 一律用 `powershell -NoProfile -ExecutionPolicy Bypass -File …` 跑。

| 检查 | 工作目录与实际命令 | 返回码/结果 | 证据与证明范围 |
| --- | --- | --- | --- |
| 根构建 | `dotnet build AutoTheSpire.sln -v m` | **exit 0**（10.27s，0 错误 3 警告） | `logs/prod-rootbuild.log`。3 警告 = 移植件 `Slice/SourceMap/ActMap.cs(42,37)`/`(60,33)` CS8600 + 既有 `Slice/SpirePowerSource.cs(1241,39)` CS8604；证明生产源码进解决方案可编译 |
| 引擎检查 | `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` | **exit 0**，`All 95 engine checks passed.` | `logs/prod-engine.log`；core 冻结未改 |
| base 切片检查 | `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | **exit 0**，`All 339 slice checks passed.`（既有 333 + 新 6） | `logs/prod-slice.log`；证明本包 6 条 T05 检查在生产树通过（身份 / 两 seed 轨迹 / 合法性 / 未实现房型 / 结算出口 / 抽数与流） |
| 移植件独立对拍 | `.audit/dsh-env-t05-map-room-2026-10-08/port-verify`（`probe`/`ref` 两个 net9.0 工程，同一流字节） | exit 0；80 配置、5004 比较点、`mismatchedConfigs=0`；跨进程 `SAME=20 DIFF=0`；16/16 运行时扰动无差异 | `port-verify/REPORT.md`（含 `variants/sorted` 反例 26/80 分叉）。证明"逐字移植 == 独立参考移植"、枚举序差异是确定性函数、**排序"修法"实测有害故不采用** |
| 严格 regression | `powershell -NoProfile -ExecutionPolicy Bypass -File tools/differential/test-env.ps1 -Suite regression -Output .audit/dsh-env-t05-map-room-2026-10-08/differential/regression` | **exit 1**（严格 suite 有非通过项即非零）；`run_id=regression@env-t04-regression@1+20261008T131038Z`；`counts={"expected":15,"selected":15,"executed":15,"passed":8,"self_checked":1,"failed":6,"self_test_failures":0}` | `.audit/.../differential/regression/regression.json`。**与改动前 18:02 的官方产物 `artifacts/environment/tests/regression/regression.json` 逐 case 判决全部相同**：8 passed；`factory-wiring-dev-scope` self_checked；`headbutt-mid-choice` `unsupported_decision_model`；`enemy-death-aftermath`/`power-turn-end-decay`/`vulnerable-decay-via-bash`/`random-reshuffle-unsupported` `unsupported_rng_profile`；`rng-initial-shuffle` `diverged`（首差 `boundary[0].enemy.base-spire:fuzzy-wurm-crawler.alive: 缺失 ≠ 存在`）。⇒ 6 条非通过是 **T04 时期既有边界，非本包回归** |
| Host 正常加载 + Start | `dotnet run --project net-the-spire/Net.TheSpire.Host -- --scenario formal-dev --game-seed ENV-IRCLAD-00` + 两帧（`describe`、`start`） | **exit 0**；stderr `net-the-spire host ready: scenario=formal-dev protocol=0.2 scope=base-spire-singleplayer-dev-v1 build=52b2…`；两帧 `ok=true`；`seed=ENV-IRCLAD-00`、`session-318d819e0a9c-1` | `logs/prod-host-start.err`；证明同一正式工厂产出的 Session 能被正式 Host 正常加载并 Start；**未驱动到终局**（属 C 范围）；地图 scope 在 Host 不可选（`net/` 只读） |
| 两 seed 实跑数字 | 同第 3 行（检查内打印） | `t05-map-alpha` nodes=51 rest=7 unknown=11 words=292 draws=292 后继=3 首个合法选择 行1列2 Monster `base-spire:nibbits-weak`；`t05-map-beta` nodes=61 words=265 draws=265 后继=4 行1列0 `base-spire:fuzzy-wurm-crawler-weak` | `logs/prod-slice.log`；直方图 alpha `Monster:18` / beta `Monster:28`，两 seed 几何与遭遇都不同 |
| 身份未漂移 | 第 3 行检查的摘要行 | Dev `build=52b285cbc40711cc ruleset=04078d3be6c4b66c`（= 改动前 golden）；DevMap `build=29abce0b690a3d22 ruleset=3a57e1e3cd84a183` | 生产值 == 沙箱值逐字相同；默认 Dev 的规则/构建身份与 B 依赖的初始边界未变 |
| 抽数账目 | 第 3 行检查的抽数行 | 地图流 `base-spire:run:act_1_map` 292 字 = 重放字数；加载总抽数 338 = 292 + 其余 46（`up_front=19, combat-draw=9, encounter:SLIMES_WEAK:1=3, niche=3, monster_ai=2` 等） | 证明每分支一次消费、无补抽、选择不重复生成 |
| 未执行 | — | — | 幕 2-4/幕转换、`hasSecondBoss`、`TryGenerate` 重试、未实现房型真实内容、奖励/选牌/跳过/容量、事件系统、BOSS 结算；完整自动打赢**不是本机制所需**，故未循环刷种子 |

引擎拒绝语义的证明范围：`Check.Fault(EngineFaultCode.IllegalAction, …)` 断言了 `is not a current candidate`（候选域拒）与 `guard of action`（守卫拒，传入**真候选**以隔离原因）两种消息，并逐次断言 `session.State.StateHash()` 不变、拒绝后仍能做合法选择 ⇒ "非法/非相邻/阶段错拒绝且状态不变、候选不绕过引擎"成立。

**交付前最后一处代码修正与复跑**：对抗性评审指出 dev-map 的 Describe 把守卫写成 `MapFloor < 17`，与真实守卫不符 ⇒ 已改为 `MapFloor < Run.RoomCount`（含可达行范围说明，见"差异"节）。修正后重跑 `logs/prod2-rootbuild.log` / `prod2-engine.log` / `prod2-slice.log`：**exit 0 / exit 0 / exit 0，`All 95 engine checks passed.`、`All 339 slice checks passed.`**，dev 摘要 `52b285cbc40711cc` 与 dev-map 摘要 `29abce0b690a3d22` 均不变（限制文案不进构建身份）。第 5、6 行的 differential 与 Host 跑在同一源码上，唯一差异是 dev-map 的 Describe 文案，**默认 Dev 路径与 Host 可达范围完全未变**。

## 差异、阻塞与未验证

（首个差异与最终阻塞项在本包验证跑完后补全；下面是截至交付的条目）

- **首个差异 / 未验证（本包边界，接手先看这条）**：①**地图 scope 无法从正式 Host 选择**（`net/` 只读 + 默认必须保持 `Dev`）⇒ 地图能力目前只有 Slice 级轨迹证据 + 工厂/规范身份证据，**没有 Host 级地图轨迹**；C 加 ruleset/scenario → `ScopeId` 映射后可补。②严格 regression 仍 **exit 1**，第一条非通过项是 `rng-initial-shuffle`（`diverged`，与改动前同值）⇒ 本包没有把严格 suite 变绿，只证明**判决逐条未变**。③`ChooseMapPoint` 的 `Requires` 用 `MapFloor < Run.RoomCount`（`MapFloor = Row + 1`），不是源里的常量：源的相邻/阶段把关在 UI/宿主（见"源依据"），本包按同一规则在动作上把关。

- **前置门已解除（记录在案）**：派单要求"C host-sequences 与 B init-diagnosis 会话实际停止后才可编辑 base 生产文件/共享 obj/bin"。`docs/review/records/2026-10-08/dsh-supervisor-review-2026-10-08.md`（19:23:49）记录 19:09 三份记录各查询一次：C 序列包停止 seq1370、B 初始化诊断停止 seq745，均为 `turn/end completed`；19:21:04 两份交接归档到 `docs/review/records/2026-10-08/`。⇒ 本包据此编辑 base 生产文件。新派生的 C `.audit/dsh-env-t03-declaration-extension-2026-10-08/`、B `.audit/dsh-env-t04-source-init-manifest-2026-10-08/` 按 [主清单](../../tofix/README.md) 等 A 停止才动编译项目——本包不阻塞它们，也不改它们的输入。
- **未实现房型的表达方式（须知）**：选择商店/营火/宝箱/精英等未实现节点时，`ChooseMapPoint` 走具名 `Fail("… 房间类型 X 尚未实现（地图节点保留、不伪装空事件）")`。引擎语义下 `Fail` 会把会话置 `SessionStatus.Faulted`（`core-the-spire/Core.TheSpire/Engine/Scheduler.cs:488`），此后 `LegalActions` 返回空（`:215`）——**地图状态本身没有被改动**（节点保留、`Visited` 仍 false、`MapPhase`/`MapFloor` 不变）。这是引擎里唯一"显式 unsupported、不静默、不删节点"的表达；也正因如此，每个房型的检查都在**新会话**里做。若负责人要求"未实现房型不置 Faulted、只拒绝本次选择"，那需要 core 侧新增一个非 fault 的拒绝结果（本包不改 core）。
- **独立对抗性评审（已处理，无阻断项）**：报告 `.audit/dsh-env-t05-map-room-2026-10-08/review-implementation.md`（评审子代理 `22a31246-5787-4112-89a2-1fabf320f8f8`，只读评审）。结论 **A–H 未发现硬约束 1–7 的违反项**：A 合法性走引擎候选（不违反"不得绕过候选"）；B 8 条 `Fail` 全在 Monster 分支之后且是 throw（`Engine/Session.Instructions.cs:290-291`），无"先写状态再 Fail"的路径；C 每轮恰一次抽数、`done` 后零抽数、预算耗尽具名 Fail；D 计算后端无可变状态、`Type` 用 `Value.OfEnum`；E 摘要身份有改动前 golden 锁（`logs/check-baseline.log:383/:413`）；F 无 static 可变状态、无 C# RunState；G 6 条检查都能失败，但 `SettleThenChoose` 的边对照与被测物化共用 `SourceActMapCompute.BuildEdges`（同源自证）——评审另行用移植件独立推出等价（`SourceMap/SourceActMapGenerator.cs:211-221` 由 `point.Children` OR 出 mask、`README-port.md:238` 标顺序无关、网格列唯一）。
- **评审中经我复核的 2 条误报**：①"`EnterNextRoom` 在 dev-map 仍注册"——实际注册被 `if (hasRun && actMap is null)` 守卫（`Slice/CombatSlicePackage.cs:4658`，`:4735` 注释同述），dev-map **不注册**；②"Describe 仍写'不使用地图'"——dev-map 下 `Replace("地图：", …)`（`SinglePlayer/BaseSpireRuntimeFactory.cs:257-280`）整条替换，只有默认 `Dev` 保留原文。
- **评审建议中已采纳并修的 1 项（本包唯一代码改动）**：dev-map Describe 把守卫写成 `MapFloor < 17`，与真实守卫 `MapFloor < Run.RoomCount`（`Slice/MapRoomWiring.cs:463-468`；正式构建 `Run.RoomCount = encounterPool.RoomCount = 15`，`CombatSlicePackage.cs:341-342`/`:3762`）不符 ⇒ 已改为 `MapFloor < Run.RoomCount`（= 15 ⇒ 可走到第 1–14 行；拿不到该字段时退回常量 17）并写明"营火行与 BOSS 行不在本包范围内"。生产与 `.audit/.../scratch/` 两份同步改，改后重跑全绿（见"验证与证据"末段）。
- **评审提及、本包明确不改的 5 项**：①可达行止于第 14 行（范围边界；第 15 行营火与 BOSS 行要幕转换/奖励，属下一子包）；②起点 `Ancient` 的 `Fail` 分支不可达（候选只来自 `MapChildren`，起点不是自己的子节点），保留以保持源房型映射完整；③未实现房型用 `Fail` ⇒ 会话置 Faulted（引擎里唯一"显式 unsupported"，不回滚、靠书写时序），要"拒绝但不 fault"需 core 加法；④`Scheduler.Fatal`（`Engine/Scheduler.cs:474-502`）未用 `StateStore.RunAtomic`（`:562-586`）——core 冻结，不由本包改；⑤跨进程确定性与重放次数：以 `.audit/.../port-verify/REPORT.md` 为准（20 种子 × 2 进程 `SAME=20 DIFF=0`、8 进程哈希一致、16/16 运行时扰动无差异），评审未见该报告故标"无法判定"。
- **窄缺口（保持窄 blocked，等负责人裁定，不阻塞地图）**：`EncounterPool`/`EncounterQueue` 声明为 `NotPublished`，但核心投影仍发布这两条集合的**成员 InstanceId 列表/size/capacity**（`Schema.cs:151-178` 默认 `IdentityVisible/SizeVisible/OrderVisible = true`；`Projection.cs:109-128` 逐集合产出）——"没有公开事实"的结构性理由不成立。事实、暴露范围与三个最小建议见 `.audit/dsh-env-t05-map-room-2026-10-08/narrow-gap-encounter-publication.md`；本包不改这两行声明，也不为了让编码通过而降低规则标记。
- **Host 侧入口差异（迁移要求，交给 C）**：`net-the-spire/Net.TheSpire.Host/FormalDevScenario.cs:26` 硬编码 `new(RunConfiguration.Ironclad())`（scope=Dev），`net/` 对本包只读 ⇒ 正常地图入口无法从 Host 命令行选择，本包的地图证据只能到"同一个正式工厂 + 同一公开规则/投影"这一层；C 需要加 ruleset/scenario → `RunConfiguration(ScopeId = ...)` 的映射。另 `net-the-spire/Net.TheSpire.Tests/FormalPathChecks.cs:62` 断言场景身份是 `BaseSpireScope.Dev`，因此**默认入口必须保持 Dev 不变**。
- **`PowerStacksOf` pure-procedure 首差（主清单 :130，与地图无关）**：正式 Host seed `ENV-IRCLAD-00` 上连续 Strike→目标决策→再 Strike/Bash 会在合法动作查询触发 `SessionStateInvalid: pure procedure base-spire:PowerStacksOf did not complete; pure procedures cannot suspend`（源码落点 `Slice/CombatSlicePackage.cs:281-295`）。它在**默认 Dev 路径**上，本包不并开第二处生产修复、不吞异常、不删合法候选；若属冻结 core 缺口需最小复现 + 负责人裁定。
- **本包不做**：真实奖励/选牌/跳过/容量与长期提交（T05 下一子包，接缝见"已交付与接口变化"）；全房间族（商店/营火/精英/宝藏/未知/事件/BOSS 只有显式 unsupported 证据）；幕转换与第二幕地图；召唤族。

## 维护位置

任务状态/认领只更新[主清单 ENV-T05 行](../../tofix/README.md)；本包长日志与本包 `.audit` 产物留在 `.audit/dsh-env-t05-map-room-2026-10-08/`。

## 21:53负责人接收说明

实际状态停止，完整结果取齐至1529，最终turn/end completed。地图/房间首包局部能力接收；完整公开地图与真实奖励/正常攻击仍未验收。源地图移植与80配置证据只覆盖其声明范围，未实现房型Fail会使会话Faulted，不能描述为可继续选择。原文PowerStacksOf复现seed存在IRCLAD/IRONCLAD拼写差，接续包分别核实。负责人将文件头误写status字段更正为约定delivery_status；这是管理格式修正，保留执行者交付声明，不表示父项验收。详见[当日复核](dsh-supervisor-review-2026-10-08.md#2153验收与地图公开接续)。移入records只整理日期证据。

负责人补充证据口径：本包SettleThenChoose在SliceMapRoomChecks.cs:297–322使用玩家Hp=30/敌人Hp=1及MoveToHand；未实现房型检查通过Arrange置父节点。均是受控机制检查，不能标为正常无注入胜利或完整闭环。源初始地图与合法战斗进入可局部接收，正常战斗出口/奖励仍待后续公开轨迹。
