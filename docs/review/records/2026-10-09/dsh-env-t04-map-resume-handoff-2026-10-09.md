# ENV-T04 地图独立对拍机制包 — 交接（ready_for_review）

| 字段 | 值 |
| --- | --- |
| `task_id` | ENV-T04 |
| `dispatch_id` | ENV-T04-map-differential-resume-20261009 |
| `session_id` | session-8cf3be04-54db-46da-84c6-c97820fb95db |
| `request_id` | 3d069a75-c69b-4c88-aeab-b31d0b125ef8 |
| `delivery_status` | `ready_for_review` |
| `updated_at` | 2026-10-09 01:11:49 +08:00 |
| `dispatch_record` | `.audit/dsh-env-t04-map-resume-2026-10-09/task-record.json` |

关联：主清单 [ENV-T04](../../tofix/README.md#env-t04)（父项，**由负责人维护，本包未编辑**）；设计
[环境对拍范围](../../../design/environment-completion-and-differential-v1.md)、
[实现约束](../../../design/environment-implementation-constraints.md)、[B 对拍提示词](../../../design/agent-goals.md#4-b严格对拍与结算-agent)；
本包证据与脚本 [`../../../.audit/dsh-env-t04-map-resume-2026-10-09/`](../../../../.audit/dsh-env-t04-map-resume-2026-10-09)（含 `run-verification.ps1`、`build-map-negative-suites.py`、全部日志）；
复用来源（未重做）[`../../../.audit/dsh-env-t04-map-differential-2026-10-08/`](../../../../.audit/dsh-env-t04-map-differential-2026-10-08)（`map-case-design.md`、`draft/`）；
后续 A `.audit/dsh-env-t05-real-rewards-2026-10-09/task-record.json`、C `.audit/dsh-env-t03-reward-public-2026-10-09/task-record.json`。

**一句话交付**：幕 1 地图生成机制的两条 seed 对拍已落盘并完成**双侧独立实际执行**——严格 suite 17 个必测
case 全部执行（分母 15→17 只增不减），新增 `map-generated-alpha`/`map-generated-beta` 双侧 `passed`、
逐节点类型与逐条边及底层 64 位字消费量两侧完全一致；**六项既有红态逐字未变**（未降级、未换 seed、未补抽、
未过滤候选），严格入口仍诚实返回 rc 1。

## 接手后先做什么

1. **复测编译门（后继 A/C 必须先做）**：在 `F:/Scylier-Project/active/auto-the-spire` 执行
   `python C:\Users\26877\.codex\skills\dsh-remote-operator\scripts\dsh_client.py status --record .audit/dsh-env-t04-map-resume-2026-10-09/task-record.json`
   （**必须带 `--record`**）。要求 `sessions[].running == false` **且** `Test-Path docs/review/tofix/dsh-env-t04-map-resume-handoff-2026-10-09.md` 为真。
   本包交付时 `running` 仍为真（会话未停），所以**门只在派单会话真正停止后才开**；不得用 `status: accepted`、
   文件 mtime 静止或常驻 MSBuild 进程代替。门开后按共享顺序 **A（T05 奖励闭环）→ C（T03 公开奖励轨迹）** 串行开工。
2. **复核本包证据而不是重跑全套**：先读 `tools/differential/README.md` 末尾 `## 地图独立对拍（2026-10-09…）`
   一节（含 rc 表、判定分布、两侧绑定与未覆盖范围），再看
   `artifacts/environment/tests/regression/records/{base,reference}/map-generated-{alpha,beta}.json` 的逐条事实。
   若确需重跑：`dotnet run --project tools/differential -- test --suite regression --output artifacts/environment/tests/regression`
   **rc 1 是既有诚实红态**（6 个 unsupported/diverged 原 ID 仍在），不是本包引入的回归。
3. **父项不得据此关闭**：`ENV-T04` 与 M01 的关闭权在负责人；`accepted` / `running=false` / 绿色总数都不是关闭依据。

## 已交付与本轮实际改动

| 路径 | 内容 |
| --- | --- |
| `tools/differential/MapFacts.cs`（新，2841 B） | 地图事实的唯一落盘形状：`map.count`/`rows`/`cols`/`words_consumed`/`start`/`boss`/`second_boss` + 逐条 `map.node.[c,r].type` + 逐条 `map.edge.[pc,pr]->[cc,cr]`；按路径排序、不做形状推导 |
| `tools/differential/MapSnapshot.cs`（新，3069 B） | base 侧只读投影：经 `RuleSet/Collections/Relations` 公开接口读 `base-spire:MapNodes`/`base-spire:MapChildren`，流只取 `base-spire:run:act_1_map` |
| `tools/differential/ReferenceMapEntry.cs`（新，10203 B，整体 `#if REFERENCE_SIM`） | 参考侧独立地图入口：源派生种子 → `Rng(act_1_map)` → `Overgrowth.GetMapPointTypes` → `StandardActMap(15, …, hasSecondBoss:false, enablePruning:true)`；只读自己的公开输出，不建 RunState、不复制 base 结果 |
| `tools/differential/RngSourceCompat.cs` | 纯新增 `Xoshiro256StarStar.Matches(s0..s3)` 与 `WordsForState(seed, s0..s3, maxWords)`：把参考生成器状态对齐回源兼容模型，求出**底层 64 位字数**（唯一可比单位），512 字预算内对不上返回 `-1` |
| `tools/differential/FixtureSuite.cs` | `CaseKinds.Map="map"`；`BaseScopes`（`dev`/`dev-map`）；`FixtureCase` 末尾追加 `BaseScope`（默认 `dev`）；校验 `base_scope` 合法且 `kind=map` 必须 `base_source=formal-dev-factory` + 字符串 `game_seed` |
| `tools/differential/BaseBackend.cs` | `Run` 分派加地图路径；新增 `RunMap`：走唯一正式工厂正常开局（`base_scope=dev-map`），只取一个 `start` 边界，`Actions` 传空表；`OpenFormalSession(gameSeed, baseScope)` 选 `BaseSpireScope.Dev`/`DevMap` |
| `tools/differential/ReferenceBackend.cs` | `kind=map` 分派到 `ReferenceMapEntry.Run` |
| `tools/differential/suites/regression.json` | `expected_case_ids` 原 15 条一字未动，末尾追加 `map-generated-alpha`/`map-generated-beta`（17）；`cases[]` 追加两条同形 map case（`rng_profile=vanilla-compatible@1`、`base_scope=dev-map`、`required_dimensions=["facts","streams"]`、`expected_terminal=running`、`script=[]`、`semantic_actions=[]`） |
| `tools/differential/fixtures/negative/suite-map-missing-dimension.json`、`suite-map-wrong-terminal.json`（新） | 新维度的判别负例：完整注册 suite（8 条双侧 passed 前缀 + 地图 case），只改地图 case 一个字段（`required_dimensions` / `expected_terminal`） |
| `tools/differential/README.md` | 四处文档：`base_scope` 字段与 `kind=map` 额外要求、`kind=map` 条目（比较单位/字数对齐/不声明 `actions`/覆盖边界）、负例表两行与"四项完整 suite"口径、末尾 2026-10-09 实测节 |
| `.audit/dsh-env-t04-map-resume-2026-10-09/` | `task.txt`、`task-record.json`、`run-verification.ps1`、`build-map-negative-suites.py`、`verify-build.log`、`negative-map-missing-dimension.log`、`negative-map-wrong-terminal.log`、`strict-suite-run-1.log`、`strict-suite-run-2.log`、`run-verification-final.log`、`negative/` 产物 |
| `docs/review/tofix/dsh-env-t04-map-resume-handoff-2026-10-09.md` | 本交接（唯一维护入口） |

**未改**：`core-the-spire/**`（engine/ir@0.3 冻结）、`reference/**`、`sts2/src/**`（冻结源）、
`mod-the-spire/base-spire/**` 任何生产源（地图 port 与 DevMap 工厂是 2026-10-08 前序包已落盘的事实，本包只**调用**它）、
`tools/differential/{TestCommand,Comparator,Records,EngineSnapshot,Program}.cs`、`docs/review/tofix/README.md`（主清单归负责人）、
他线与 `solution`。本包因此**没有新增**生产玩法语义、没有参考答案、没有第二 RunState。

### 两处非"草稿原样复制"的改动（审阅发现，必须知道）

1. **源摘要假警报修正**：`ReferenceMapEntry.SourceFiles()` 原先拿 `Repo.DigestFiles` 的 `sha256:` 前缀摘要
   去比登记文件（`.audit/dsh-supervisor/review-2026-10-08-2153/map-source-inputs.json`）的裸十六进制，
   直比必然不等 ⇒ 一条**完全相符**的摘要也被写成"（登记=…，不一致）"。现各自剥掉前缀后按十六进制
   `OrdinalIgnoreCase` 比对。修正后 7 条源输入**全部相符**（修正前是噪声红态）。这是本包唯一非草稿原样落盘的参考侧代码改动。
2. **参考地图入口不建 RunState**：参考的运行层入口会消费本 case 不比较的流；用它比较地图会把
   "未声明随机消耗"混进证据。地图入口只构造被比较的那张图所需的最小链路。

## 源依据（只读核对，行号可复核）

- 派单：`.audit/dsh-env-t04-map-resume-2026-10-09/task.txt`；原任务与严格边界：
  `.audit/dsh-env-t04-map-differential-2026-10-08/task.txt`（其独立执行/严格边界本包继续适用）。
- 设计：`.audit/dsh-env-t04-map-differential-2026-10-08/map-case-design.md`（比较对象、两侧源定位、事实路径、字数单位、参考身份）。
- base 侧（只读）：`mod-the-spire/base-spire/BaseSpire/SinglePlayer/BaseSpireRuntimeFactory.cs:81-82`
  （`ActMapEnabled => ScopeId == BaseSpireScope.DevMap`）、`:150-166`（`SeedStream("base-spire:run:act_1_map", ActMapSeed(runSeed, 1))`，建局阶段消费随机即抛）；
  `Slice/VanillaRandomLayout.cs:219-236`（`ActMapSeed = runSeed + DeterministicHashCode("act_1_map")`）；
  `Slice/MapRoomWiring.cs`（`ActMapStreamName`/`MapNodes`/`MapChildren` 常量与 `:316-362` 增量抽样、
  超 `WordBudget` 即具名 Fail）；`Slice/SourceActMapCompute.cs:50-68`（`Span=1<<53`、`ActRooms=15`、`WordBudget=512`）；
  `SinglePlayer/RunConfiguration.cs:28,46,49,56`（`DevMap = "base-spire-singleplayer-dev-map-v1"`、`Implemented=[Dev,DevMap]`）。
- 参考侧（只读）：`reference/sts2-sim/src/Sts2Sim.Core/Content/Acts/Overgrowth.cs:20,68-73`（`BaseNumberOfRooms => 15`、
  `restCount=NextGaussianInt(7,1,6,7)`、`unknownCount=StandardRandomUnknownCount`）、
  `Map/StandardActMap.cs:1-150`、`Map/MapPathPruning.cs`、`Map/MapPostProcessing.cs`、`Map/MapPointTypeCounts.cs`、
  `Random/Rng.cs:23-53,507`（命名流 = `seed + DeterministicHashCode(name)`）、`Random/MegaRandom.cs:156-171`、
  `Random/RngDiagnostics.cs:8,17`。
- 冻结源：`sts2/src/Core/{Map/StandardActMap.cs,Map/MapPointTypeCounts.cs,Models/Acts/Overgrowth.cs,Random/Rng.cs,Runs/RunRngSet.cs}`
  （`reference_identity.source_files` 逐条比对的 7 条，见下）。

## 源码 / DLL 摘要、config、seed、run_id、命令返回码

**两侧绑定（同一次构建/同一 Profile/同一参考 HEAD/同一批源输入）**：

- base build：`build_id = 6b25fa16e5b567f4`（dev-map 规则集；与 A 交接记载的历史值一致）；
  工具构建闭包：`dotnet build tools/differential/Differential.csproj`（Core + BaseSpire + `reference/sts2-sim` + 工具）。
- 工具身份：`tool = {"version":"env-t04-test@1","assembly":"Differential","dotnet":"9.0.10","reference_adapter_available":true}`。
- Profile：`source-compat-sts2-frozen@1`，`profile_code_sha256 = sha256:952e57a026587844651cef822e01d4848cc7b2c3b2c1b27e74e3423b22046c91`
  （= `tools/differential/RngSourceCompat.cs`）。
- 参考 HEAD：`2cdaea3b6dbc20cfa340662fa73640f20bb6fb00`（`reference/sts2-sim/.git/refs/heads/main`）。
- 两侧记录 `rng_profile` 均为 `vanilla-compatible@1`（否则比较器不比流）。
- seed：`t05-map-alpha`（`source_run_seed=2333176601`）与 `t05-map-beta`（`source_run_seed=2252421439`）；
  alpha 的 `mapSeed=4127930008`（uint，`reference_identity.streams`）。
- 源输入 7 条逐条相符：`sts2/src/Core/Map/StandardActMap.cs`=`6f927e075056ac49cf4eb93943d332901d11208c8196a0b778c03d522e7ec9bb`、
  `sts2/src/Core/Map/MapPointTypeCounts.cs`=`2ca2f858c6b04ddddc5657951f0eae010994fcdc8eecb2c3d55d98781b309ab2`、
  `sts2/src/Core/Models/Acts/Overgrowth.cs`=`61ff9d71f9b17f2f6a2f7f51f64bbfaa6752ec8cf7e75b4d291f2b0f89dc232b`、
  `sts2/src/Core/Random/Rng.cs`=`99a404a807dae7b3c786821b3c287e6518b573f58265f98feaa63132748b7f40`、
  `sts2/src/Core/Runs/RunRngSet.cs`=`059f7ab16345cb1147d68c540ed1a24f66f4b14e370b6c1f019cde6fbac80784`、
  `reference/sts2-sim/src/Sts2Sim.Core/Map/StandardActMap.cs`=`25d729e85445bbcf1938a2282f3675ab95e3ddfd35f179c8d5f93a4af676e675`、
  `reference/sts2-sim/src/Sts2Sim.Core/Random/Rng.cs`=`a1f93f52040f7271042425788d6cb30db7ece688659ca23dfc3a388c1db62230`。

命令与返回码（工作目录一律 `F:/Scylier-Project/active/auto-the-spire`；本机无 `pwsh`，脚本用
Windows PowerShell 5.1 `powershell.exe -NoProfile -ExecutionPolicy Bypass -File` 跑；日志在
`.audit/dsh-env-t04-map-resume-2026-10-09/`）：

| 命令 | 返回码 | 证据与证明范围 |
| --- | ---: | --- |
| `dotnet build tools/differential/Differential.csproj` | **0** | `verify-build.log`：0 错误 0 警告。证明工具闭包（含参考）可构建 |
| `dotnet run --project tools/differential --no-build -- test --suite regression --output artifacts/environment/tests/regression` | **1** | `strict-suite-run-2.log`（同一脚本的整体 tee 在 `run-verification-final.log`；更早一次同结果 `strict-suite-run-1.log`，`run_id` 尾 `…170157Z`）。`expected 17 / selected 17 / executed 17`、`complete=true`；`passed 10`、`self_checked 1`、`failed 6`、`reference_ran 15`、`self_test_failures 0`；`run_id=regression@env-t04-regression@1+20261008T170824Z`、`suite_sha256=sha256:ac5026896180c516540f656624a9a1f3c33e8054eb2d4801080c20fc3e7028a7`、`exit_code=1` |
| `dotnet run --project tools/differential --no-build -- test --suite tools/differential/fixtures/negative/suite-map-missing-dimension.json --output .audit/dsh-env-t04-map-resume-2026-10-09/negative/map-missing-dimension` | **1** | `negative-map-missing-dimension.log`：`[FAIL] map-generated-alpha missing_dimension 声明的必需维度没有抽取：actions / actions(reference)`；其余 8 条 `PASS`；自检 6/6 |
| 同上，`--suite …/suite-map-wrong-terminal.json` | **1** | `negative-map-wrong-terminal.log`：`[FAIL] map-generated-alpha wrong_terminal 期望胜利终局，实际 session_status=Running，terminal=(无)`；其余 8 条 `PASS`；自检 6/6 |

交付前文档门（纯文档/工具改动）：`python tools/doc-check/check_docs.py` **rc=0**（扫描 304 个 Markdown，0 problem）；
`git diff --check` **rc=0**（它只覆盖已跟踪改动，`tools/differential/`、`docs/review/tofix/` 与 `.audit/` 在本工作区
整体仍为未跟踪，故对全部新增/修改文件另做尾随空白扫描，0 命中）。未提交、未 push、未 reset、未清理。

## 真实证据

**严格 suite（17 分母，全执行）**：verdicts = `passed` 10 / `unsupported_decision_model` 1 /
`unsupported_rng_profile` 4 / `diverged` 1 / `self_checked` 1。

| case | seed | 判定 | 实测比较量 |
| --- | --- | --- | --- |
| `map-generated-alpha` | `t05-map-alpha` | **passed**（`divergences=[]`、`missing_dimensions=[]`） | `map.count=51`、`rows/cols=17/7`、`start=[3,0]`、`boss=[3,16]`、`second_boss=none`、事实 123 条（51 节点类型 + 65 边 + 7 头部）、`act_1_map draws=292 fp=0x1d032dd0fc7e5192`（两侧相同） |
| `map-generated-beta` | `t05-map-beta` | **passed**（`divergences=[]`、`missing_dimensions=[]`） | `map.count=61`、rows/cols=17/7、事实 140 条（61 节点类型 + 72 边 + 7 头部）、`draws=265 fp=0xdaeca1274bcfdb41`（两侧相同） |

- 两侧各只有 1 个边界（`base_boundaries=reference_boundaries=1`）、两侧事实**逐条一致**；两个 seed 的网格规模与
  事实条数确实不同（123 vs 140），不是同一张图跑两遍。记录在
  `artifacts/environment/tests/regression/records/{base,reference}/map-generated-{alpha,beta}.json`。
- base 侧：`backend=base-spire:singleplayer-dev-v1`、`backend_version=env-t04-base@1`、`status=ok`、
  `session_status=Running`、`build_id=6b25fa16e5b567f4`。参考侧：`status=ok`、`session_status=MapGenerated`。
- 流维度按**底层 64 位字数**对齐（`WordsForState` 在 `WordBudget=512` 内命中）：alpha 292、beta 265，
  两侧 draws 与状态指纹相同 ⇒ 命名流种子派生与逐字消耗一致，不是只有形状像。

**判别负例（新维度不能被声明式地"比过"）**：两项完整注册 suite 各 9 条 case（`expected_case_ids` 9 条、
`complete` 为真），只有被改字段 FAIL，其余 8 条仍 `passed`、自检 6/6 ⇒ 非零只来自新维度的门槛，不是套件噪声。
派生脚本 `build-map-negative-suites.py` 每次改写后打印 `mutated: [(case_id, [字段])]` 漂移清单，实测分别只有
`map-generated-alpha` 的 `required_dimensions` / `expected_terminal` 一个字段变化（其余字段与正式 suite 逐字相同）。

**六项既有红态逐字未变（未降级、未替换、未过滤）**：`headbutt-mid-choice`=`unsupported_decision_model`；
`enemy-death-aftermath`/`power-turn-end-decay`/`vulnerable-decay-via-bash`/`random-reshuffle-unsupported`=
`unsupported_rng_profile`（首差均为 `boundary[*].pile.draw.sorted` 路径分叉，是证据不是规则结论）；
`rng-initial-shuffle`=`diverged`，首差仍是 `boundary[0].enemy.base-spire:fuzzy-wurm-crawler.alive: 缺失 ≠ 存在`。

## 首个差异

**本包新增的地图路径没有首差**：两条 case 的 `divergences` 为空数组，两侧节点类型、边集合、特殊点与
`map.words_consumed` 逐条相同。参考生成器状态在 512 字预算内对齐到源兼容模型（292/265 字），
所以不存在"单位不可比"或"预算不足"的降级。

既有 `rng-initial-shuffle` 的首差（`boundary[0].enemy.base-spire:fuzzy-wurm-crawler.alive: 缺失 ≠ 存在`）
与本包无关、由本包原样保留、未做任何遮蔽。若负责人判定地图覆盖需要更多 seed，**不得**换 seed/过滤候选/改分母
来制造通过；本包只按派单交付原两条。

## 未执行 / 未验证

- **不比 `ChooseMapPoint` 动作语义**：参考侧没有对应动作模型，地图 case 的 `actions` 传空表且
  `required_dimensions` 不含 `actions`。选点行为完全不在本包证据范围。
- **不证明多房间推进**：base 侧只停在 Start 之后的地图相位（`session_status=Running`、无终局）；
  base 选中尚未实现的房型仍具名 Fail。**不证明房间奖励与结算跑通**（那是 A/C 的开放范围）。
- **未跑共享根构建**：`dotnet build AutoTheSpire.sln`、`dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests`、
  `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` 本包**均未运行**（本包只改 `tools/differential/**`，
  按派单"按变更串行一次必要验证"只跑了工具闭包构建）；这些检查的既有状态不得表述为通过。
  历史上根构建曾因 `net-the-spire` 线的在制改动返回 1（2026-10-08 记录，非本包改动）。
- **未覆盖的误配**：地图 case 的负数判别只有上述两项，未覆盖 `base_scope=dev`（无地图投影）这类错配。
- **未提交 / 未清理**：工作区保持原状（`tools/differential/`、`.audit/dsh-env-t04-map-resume-2026-10-09/`、
  `artifacts/` 仍为未跟踪）；未 push/reset/训练。
- **未做**：未扩 Profile、未改参考算法、未删任何分母、未换 seed、未补抽随机、未注入清场制造通过。

## 下一步（具体动作）

1. **谁**：ENV-T05（A 奖励闭环）先、ENV-T03（C 公开奖励轨迹）后；**前置**：本会话
   `session-8cf3be04-…` 实际停止（`sessions[].running == false`）**且**本交接文件存在——两者都满足后 A 的
   `blocked` 即由本包解除（A 交接第 1 步的门就是这条）。
2. **在哪 / 做什么**：A 按 `.audit/dsh-env-t05-real-rewards-2026-10-09/reward-design-2026-10-09.md` §9 在
   `mod-the-spire/base-spire/BaseSpire/Slice/` 实施真实奖励闭环；C 随后接公开奖励轨迹。
3. **预期看到什么**：DevMap 下正常开局先选点、再进战斗、结算后开真实奖励；地图不因此被改语义。
4. **如何判断完成**：受控判别检查与固定 seed 正常闭环**分列**存在且为绿；串行一次根构建 + Engine/Slice 检查
   保留全部 strict 分母；交接收在 `ready_for_review`。
5. **T04 侧若还要加覆盖**：只能在负责人派新包后做（例如 `ChooseMapPoint` 语义、更多 seed、`base_scope=dev` 误配）；
   **不得**由后继 A/C 顺手改地图 case 或分母。

## 阻塞与依赖

- **本包无阻塞**，已按派单交付到 `ready_for_review`；`delivery_status` 不写 `accepted`，父项 `ENV-T04` 不关闭。
- **本包解除的依赖**：A/C 的前置门（本会话停止 + 本交接存在）。A 交付时记录的阻塞
  （`.audit/dsh-env-t04-map-resume-2026-10-09/task-record.json`、`asOfSeq` 506、交接不存在）现已满足后一半。
- **仍开放的关联项**（不由本包决定）：`rng-initial-shuffle` 源完整初始化对拍、5 个 unsupported 原 ID、
  `EncounterPool/Queue` 投影身份与 NotPublished、`net-the-spire` 在制改动导致的根构建红态。

## 维护位置

- 任务状态/认领/下一成果：主清单 `docs/review/tofix/README.md` 的 [ENV-T04](../../tofix/README.md#env-t04)（**由负责人维护**，本包未编辑）。
- 本包方法与设计依据：`.audit/dsh-env-t04-map-differential-2026-10-08/map-case-design.md`（复用，未重做）。
- 本包交付与证据：本文件 + `.audit/dsh-env-t04-map-resume-2026-10-09/`；工具侧长期口径在
  `tools/differential/README.md` 末尾实测节。
