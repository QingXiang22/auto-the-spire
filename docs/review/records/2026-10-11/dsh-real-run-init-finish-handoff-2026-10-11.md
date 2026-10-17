# 真实新 Run 初始化收口（普通/精英库存分离）交接 — 2026-10-11

本包把 base 整局初始化里的**普通遭遇队列与精英遭遇队列拆成两个独立库存、各自消费**，消除了完整 Slice 的首差
（普通队列预期 15、实际 30）；公开 `dev-map` 正常开局因此能真实走进地图、打完前 3 间普通战斗房并结算奖励，
在第 4 间房遇到未支持敌人时**具名停下**。仍未完成的关键范围：精英战利品差异、精英战斗本身（敌人能力原语未实现）、
商店/事件/幕转换，以及 net 侧 F-05（本包未改 net）。

归属：`ENV-T08-real-run-init-finish-20261011`（主清单 [ENV-T08 / T08.1 / P1](../../tofix/README.md#env-t08)，父项 T08 不因本包关闭）。
接手入口：主清单该任务行 → 本文件 → [源依据](#源依据与真实配置) → [验证与证据](#验证与证据)。
交付范围：base 初始化/遭遇池/组包器/地图接缝、必要公开语义与构建描述、受影响判别检查与 `Program` 选组入口、本文件与 `.audit` 证据。
本报告记录 2026-10-11 的交付；当前排期、状态与认领以主清单为准。

## 接手后先做什么

1. 先核对主清单 ENV-T08 的认领与工作区：本包已交付的部分列在[已交付与接口变化](#已交付与接口变化)，**不要重做队列分离**；
   本包未提交、未移动任何他人文件（`git status --short` 中 base 的 `Slice/`、`SinglePlayer/`、`BaseSpire.Slice.Tests/` 仍是未跟踪状态）。
2. 复核接口迁移（这是唯一被改的公开形状）：`SliceEncounterPool` 新增 `EliteRoomCount`/`EliteEncounters`，
   组包器新增 `EliteEncounterQueue` 集合与 `Run.NormalEncountersVisited`/`EliteEncountersVisited` 两个**隐藏**字段。
   若认为形状应改成别的名字/位置，先由负责人裁定再改动消费端。
3. 一个可执行的下一步（负责人/下一会话）：跑
   `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests -- --encounter-queue-only`
   与 `-- --elite-room-only`，应看到 `All 7 …` / `All 1 …`；再按[验证与证据](#验证与证据)复核同构建三 DLL。
   预期结果：普通队列 15 槽、精英队列 15 槽、`ACT1-BASIC-10` 与参考侧逐槽一致。判完成依据见主清单 ENV-T08 完成条件。
4. 需要负责人决定的两处：① dev 规则身份/公开构建身份已按新声明**迁移**（旧值 `75a9d9345def3426` / `2f0fd07325835101`
   → 新值见下），需确认接受；② 精英房是否要等敌人能力原语就绪再视为"可达"。

## 已交付与接口变化

**① 普通/精英队列分离（首差本体）**

- 旧行为：`CombatSlicePackage.Build` 在普通 15 槽循环之后，把 15 个精英槽以 `Index = RoomCount + slot`（15..29）
  追加进**同一个** `EncounterQueue`，普通队列变成 30 项，精英也没有自己的消费者。
  这与源 `ActModel.GenerateRooms`（`_rooms.normalEncounters` 与 `_rooms.eliteEncounters` 两个列表、两个 `GrabBag`）不符。
- 新行为：精英槽写进**独立集合** `EliteEncounterQueue`（元素类型 `EliteEncounterQueueEntry`，`Index` 0..14、隐藏），
  普通队列仍为 15 项（`Index` 0..14）。两段共用 `up_front` 上的抓取程序实现，但成员集合（按 `Kind` 过滤）、袋子、
  入队集合都独立；相位顺序保持源的"弱 3 → 普通 12 → 精英 15 → BOSS → 幕古代人"。
- 消费端：`Run.NormalEncountersVisited` / `Run.EliteEncountersVisited`（Hidden，初值 0）对应源
  `RoomSet.normalEncountersVisited` / `eliteEncountersVisited`；`SpawnEncounter` 按当前房型
  （`Run.CurrentNode.Type`；默认 dev 无地图时按普通战斗房）从对应队列按 `Index == 该房型访问计数` 取遭遇，
  取完再推进对应计数一格。营火/宝箱/BOSS 都不推进这两个计数（源 `MarkVisited` 分支）。
  精英房拿不到队列时显式 `Fail`，不退回普通队列。
- 地图：`MapRoomWiring` 的 `UnimplementedTypes` 移除 `Elite`，新增 Elite 分支与普通战斗房走同一条 `StartNextRoom`
  （`MapRoomWiring.cs:491`）。精英战利品差异未建模，按普通战斗同形结算——这是**已知边界**。

**② 接口迁移（一个包一个主责，消费端已同步）**

| 位置 | 变化 |
| --- | --- |
| `Enemies/SliceEncounterPool.cs` | 新增可选参数 `int EliteRoomCount = 0`、`IReadOnlyList<SliceEncounter>? EliteEncounters = null`；新增 `EliteKind = "elite"`、`Elites`、`AllEncounters` 含精英、`Tags`/`EnemyIds` 含精英。既有位置参数调用点无需改动（新参数在 `BossEncounters` 之后）。 |
| `Run/RunSourcePoolManifest.cs` | `Elites` 改为取自幕声明 `base-spire.acts.json` 的 `eliteEncounters`（顺序 = 源 `AllEliteEncounters`：BygoneEffigyElite / ByrdonisElite / PhrogParasiteElite）；`Complete` 同时填 `EliteEncounters` 与 `EliteRoomCount = act.EliteQueueCount`（15）。 |
| `Composition/CombatSlicePackage.cs` | 新声明 `EliteEncounterQueueEntry`/`EliteEncounterQueue`（仅 `initializeNewRun`）+ 两个访问计数；`EnqueueProcedure` 改为接收"目标队列/条目类型/槽位字段/遭遇字段"（`CombatSlicePackage.cs:3328`），弱/普通/精英各建一个程序。 |
| `SinglePlayer/BasePublicSemantics.cs` | 新增 `["EliteEncounterQueue"] = (NotPublished, …, "base-spire:N-02-BLOCKED-ELITE-QUEUE", …)`——新的内部库存**不得**经公开出口泄漏。 |
| `SinglePlayer/BaseSpireRuntimeFactory.cs`、`SinglePlayer/RunConfiguration.cs` | 构建描述与范围拒绝文案更新（普通/精英是两个独立队列；精英房已接入、战利品差异未建模；`16 槽`→`15 槽`、重抽上限 `2048`→`256` 的旧文案订正）。 |

**③ 身份迁移（需负责人复核）**

包形状新增 1 个条目类型、1 个集合、2 个隐藏字段，因此 dev 与 dev-map 身份必须变化：

| 范围 | RulesetId 旧 → 新 | PublicBuildId 旧 → 新 |
| --- | --- | --- |
| dev | `75a9d9345def3426` → `3224ed9fcee52136` | `2f0fd07325835101` → `35a160593789e880` |
| dev-map | （未记录旧值）→ `2e8fb3c93863d568` | （未记录旧值）→ `7fda0b085ec671b8` |

`SliceMapRoomChecks.cs` 的 `DevGolden*` 常量已随之迁移并在注释里留档旧值（未提交、未声明的历史值按原样保留在 `.audit/dsh-startup-alignment-2026-10-11`）。

**④ 检查与选组入口**

- `BaseSpire.Slice.Tests/Enemies/SliceEncounterQueueChecks.cs`：普通队列仍=独立复算；**新增**"精英队列是独立库存"
  （15 槽、只含源精英池成员、与普通队列零交集、每身份恰好 5 次、逐槽等于独立复算）、"up_front 逐段对账
  （遗物袋 230 + 事件 30 + 普通 15 + 精英 15 + BOSS/古代人 2 = 292）"、"Start 只初始化一次"、
  "`ACT1-BASIC-10` 普通队列逐槽 = 参考侧已核对向量"、"标签回避真的生效（回避/不回避队列必须不同 + 抽取次数逐次相同）"。
- `BaseSpire.Slice.Tests/Run/SliceMapRoomChecks.cs`：`UnimplementedRooms` 只剩商店；新增
  "精英房消费者：精英节点按访问计数消费精英队列（内容未支持时具名失败）"，并新增局部入口
  `EliteRoomBoundary()`。
- `BaseSpire.Slice.Tests/Run/PublicSemanticsChecks.cs`：受阻列表加入 `EliteEncounterQueue`；
  `notPublished` 计数 `4 → 17`（**这条在本包之前就已过期**：T05/T08 期间遗物袋/事件/共享古代人已陆续具名不发布，
  本包只加了 1 项。见[首差与未验证](#首差未验证与边界)）。
- `BaseSpire.Slice.Tests/Program.cs`：新增 `--encounter-queue-only`、`--elite-room-only`（只覆盖本机制）。

## 源依据与真实配置

- 源（同版本只读）：`sts2/src/Core/Models/ActModel.cs::GenerateRooms`（331-386，三个独立 `GrabBag`）、
  `::PullNextEncounter`（445-454）、`Core/Rooms/RoomSet.cs`（`NextNormalEncounter`/`NextEliteEncounter` 按
  per-type `visited % Count` 取模、`MarkVisited` 分支）、`Core/Models/Acts/Overgrowth.cs`
  （`NumberOfWeakEncounters=3`、`BaseNumberOfRooms=15`、`GenerateAllEncounters` 顺序 ⇒ `AllEliteEncounters` 3 项）、
  `Core/Random/Rng.cs` 与引擎 `RandomService.Shuffle`（降序 Fisher-Yates ⇒ n 项 n-1 抽）。
- 内容来源：`mod-the-spire/base-spire/BaseSpire/Run/Content/base-spire.acts.json`（`eliteEncounters`、`eliteQueueCount=15`）
  与 `base-spire.encounters.json`。
- 构建身份：`base-spire-singleplayer-dev-v1` / `base-spire-singleplayer-dev-map-v1`；
  config `RunConfiguration.Ironclad(0, …)`（角色 Ironclad、进阶 0、解锁 all、单人）、源基线 `sts2-frozen-2026-10-05`、
  随机布局 `mega-random/xoshiro256ss-splitmix64@1` + `vanilla-uniform@1`。
- seed：里程碑 `ENV-IRONCLAD-00`（队列检查）、`ACT1-BASIC-10`（参考向量与公开路径）、地图检查沿用 `t05-map-alpha`/`t05-map-beta`。
- 构建 DLL 身份（`.artifacts/parallel/dsh-real-run-init-finish/bin`，SHA256）：
  `BaseSpire.dll 5A8C30709005A4ABCF7694EC784BC1404E30601EACC582B271DA8C49CDF93E14`、
  `Core.TheSpire.dll BC536182CE0D41317CD88FB12E7857A6DC5A432CEAD45F6880ABDA75BE821C1E`、
  `Net.TheSpire.Host.dll FB8044FE7403C0CD5B09867C33F2890CA90D088F28BC9C091D15367FC4DC5D8E`、
  `Differential.dll 0396434C18D8F285DD0A97BDA2D3312CAF39F6E5939D48600EE8A299F1C0A6E`。

## 验证与证据

工作目录均为仓库根 `F:\Scylier-Project\active\auto-the-spire`；证据目录 `.audit/dsh-real-run-init-finish-2026-10-11/`。

| 检查 | 实际命令 | 返回码/结果 | 证据与证明范围 |
| --- | --- | --- | --- |
| 同根构建 | `dotnet build AutoTheSpire.sln --artifacts-path .artifacts/parallel/dsh-real-run-init-finish` | rc=0，0 错误 | `final-build.log`。只证明编译与链接。 |
| 引擎 | `dotnet .artifacts/…/bin/Core.TheSpire.Engine.Tests/debug/Core.TheSpire.Engine.Tests.dll` | rc=0，`All 99 engine checks passed` | `final-Core.TheSpire.Engine.Tests.log` |
| base 完整 Slice | `dotnet .artifacts/…/bin/BaseSpire.Slice.Tests/debug/BaseSpire.Slice.Tests.dll` | rc=0，`All 759 slice checks passed`（本包前同一入口在首差处中止；本包新增 6 项检查：队列文件 5 + 地图 1） | `final-BaseSpire.Slice.Tests.log`。含普通/精英独立库存与消费者、`ACT1-BASIC-10` 参考向量、Start 只一次。 |
| net | `dotnet .artifacts/…/bin/Net.TheSpire.Tests/debug/Net.TheSpire.Tests.dll` | rc=1，`416 executed, 415 passed, 1 failed` | `final-Net.TheSpire.Tests.log`。失败项与本包无关，见下。 |
| 严格对拍 regression | `dotnet .artifacts/…/bin/Differential/debug/Differential.dll test --suite regression --output .audit/…/regression` | rc=1；`expected/selected/executed 17/17/17`，`passed 15`、`self_checked 1`（`factory-wiring-dev-scope`）、`不计通过 1`（`random-reshuffle-unsupported` = `unsupported_rng_profile`，保留原 ID 与原比较范围）、自检失败 0 | `final-differential-regression.log` + `regression/regression.json`。T04 交接当时记录的口径是"14 双侧 passed / 1 单侧 / 2 未过"；本包未改工具，差异来自 T04 之后的工作区状态，请负责人以本次运行记录为准。 |
| 初始化首差诊断 | `… Differential.dll init-upfront --seed ACT1-BASIC-10 --output .audit/dsh-real-run-init-finish-2026-10-11` | rc=0；参考侧阶段重放 293 抽且 `matches_observed=true`；`startup trace: reference=303, base=303, replay_matches_factory=True` | `final-init-upfront.log` + `init-upfront-diagnosis.json`。**诊断 rc=0 不是双侧通过**；它是逐阶段账目证据（共享袋 112 + 角色袋 118 + 事件 30 + 普通段 16 + 精英段 15 + BOSS 1 + 古代人 1）。 |
| 正常公开路径 | `python -X utf8 -u net-the-spire/client/run_game.py --host-dll .artifacts/…/Net.TheSpire.Host/debug/Net.TheSpire.Host.dll --scope dev-map --seed ACT1-BASIC-10 --output .audit/…/public --max-actions 600 --use-potions` | rc=4，`STOP fault_host_rejected steps=58` | `final-public-run.log`、`public/summary.json`、`public/steps.ndjson`。轨迹见下。 |
| 本机制选组 | `--encounter-queue-only` / `--elite-room-only` | rc=0 / rc=0（`All 7` / `All 1`） | 同构建 DLL。 |
| 既有选组 | `--rest-site-only` / `--treasure-room-only` / `--treasure-bag-only` | rc=0 / rc=0 / rc=0 | 同构建 DLL，未被本包破坏。 |
| 文档 | `python tools/doc-check/check_docs.py`；`git diff --check` | doc-check rc=4（4 处**他人**旧交接锚点错误，见下）；diff-check rc=0 | 本文件未新增锚点错误。 |

**公开 dev-map 真实轨迹（seed `ACT1-BASIC-10`，实时公开候选，不是固定回放）**

- `start`：`Run.Hp=80/80`、`Gold=99`、`MapPhase=0`、`RoomCount=16`（15 普通槽 + Boss）、`MapNodeCount=66`。
- 房间 1 `base-spire:shrinker-beetle-weak`（普通队列 0 号槽）→ 打完 → 奖励（金币 12、三选一卡、药水掉落判定）。
- 房间 2 `base-spire:nibbits-weak`（1 号槽）→ 打完 → 奖励（金币 10、卡）。
- 房间 3 `base-spire:slimes-weak`（2 号槽）→ 打完 → 奖励（金币 16、卡、药水**领取**）。
- 步 59（`ChooseMapPoint` 进入第 4 间房）时，本间遭遇 = 普通队列 3 号槽 `base-spire:fogmog-normal`，
  敌人 `base-spire:fogmog` 未加载（其行动用 `summonEnemy`）⇒ 引擎显式
  `Source encounter … cannot execute: missing enemy behavior base-spire:fogmog`，宿主拒绝，客户端 `stop`。
- 该序列与参考侧重放的普通槽位序列逐槽一致（`SHRINKER_BEETLE_WEAK, NIBBITS_WEAK, SLIMES_WEAK, FOGMOG_NORMAL, …`），
  即"普通队列消费者在公开路径上按计数依次取槽"成立；**这是首个明确边界，不是通关**。

## 首差、未验证与边界

**已消除的首差**：完整 Slice 的 `普通遭遇队列预期 15、实际 30` 已由独立精英库存修复；
`SliceEncounterQueueChecks` 现在断言普通队列 15 槽、精英队列 15 槽、两者零交集且 `ACT1-BASIC-10` 与参考侧逐槽一致。

**本包未改、需负责人/其他线处理**

1. **net F-05（本包未改 net）**：`416 executed / 415 passed / rc=1`，失败断言是 `FormalPathChecks.cs:920` 的
   `Check.True(response.Error is not null, "期望错误响应")`，由 `:309` 首次调用。**首个失败分支已定位**：
   `f05-cross` 这一步——对会话 `f05-b` 提交 `session_id=f05-b` + **f05-a 的** `expected_observation_id` 与
   `candidate_id`——**返回了成功而不是错误**（期望 `stale_observation` 或 `illegal_candidate`）。
   最小复现：`ControlHost` 起两局（`f05-a`/`MilestoneSeed`、`f05-b`/`SecondSeed`）→ 各自 `observe` →
   用**第一局**的 `observation_id` 与 `candidate_id` 对**第二局**发 `step`。假设（未证实）：两局的
   `observation_id` 都是起始值、且 `candidate_id` 不是会话作用域（引擎实例 id 逐会话顺序分配，容易撞上），
   因此两个门都放行。定位与修复属 net/C 线，本包只交最小复现。
2. **精英战本身未验证（内容边界，非本包可闭合）**：3 个 Act-1 精英敌人的 `startingPowers` 都引用尚未实现的能力原语
   —— `BygoneEffigy` 的 `Slow`（`variable` + `incomingDamageModifiers`）、`Byrdonis` 的 `TerritorialPower`
   （`applyScaledPower` + `targetPowerAmount`）、`PhrogParasite` 的 `Infested`（`summonEnemy`）——敌人行为因此未加载，
   `author.Enemies` 不含它们。`--elite-room-only` 证明的是**消费者契约**：精英节点会消费精英队列 0 号槽的遭遇
   （该 id 不在普通队列里）并**具名失败**；精英战斗的清场/奖励出口保持**未验证**，也不在"8 个精英节点都能打"的意义上声称可达。
   普通池同样受内容边界限制：`fogmog-normal` 之类身份保留在源全池里、执行时具名失败（见公开轨迹）。
3. **pre-existing 检查过期（本包随手订正 1 处，另列不顺手改的）**：
   - 已订正：`PublicSemanticsChecks.cs` 的 `notPublished` 期望值 `4` 在本包之前就与实现不符（实际 16，
     T05/T08 期间遗物袋/事件/共享古代人已陆续具名不发布）；本包 +1（精英队列）后为 17。逐条判别仍由同一检查的
     循环与 ENV-T03 元数据闭合检查承担。
   - 未改动（已知、属他人交接）：`python tools/doc-check/check_docs.py` 报告 4 处 `#L` 锚点缺失，分列：
     `docs/review/tofix/dsh-power-fixture-handoff-2026-10-11.md:21`（`../../../tools/differential/README.md#L7`）、
     同文件 `:25`（`../../design/environment-implementation-constraints.md#L181`）、
     `docs/review/tofix/dsh-startup-alignment-handoff-2026-10-11-f68b943d.md:77` 两处指向上面那份交接的 `#L21`/`#L25`。
4. **仍与源有已知差别（沿用既有口径，本包未扩大）**：重抽循环按 256 次上限实现（源码无上限）；
   首次运行（`NumberOfRuns == 0`）的固定房间顺序未建模；精英战利品差异未建模。
5. **诊断工具的 `records` 段是旧构建记录**：`init-upfront` 输出的 `记录里的 up_front 抽取：base 18 / reference 295`
   读的是 `artifacts/environment/tests/regression/records/`（早于本包的产物）。**本包证据请用同次的
   `startup trace: reference=303, base=303, replay_matches_factory=True`** 与 `replay.matches_observed=true`。
6. **未运行**：商店/事件/幕转换相关门槛（不在本包范围）；`workpacks/shop-flow/` 未读未改；跨幕四幕胜负未验证。

## 维护位置

- 任务状态/认领/完成条件：主清单 `docs/review/tofix/README.md` 的 ENV-T08（**由负责人回写**，本包不关闭父 T08/M01）。
- 本包证据与日志：`.audit/dsh-real-run-init-finish-2026-10-11/`（`final-*.log`、`regression/`、`init-upfront-diagnosis.json`、`public/`、
  `dll-identity.txt`、`build-descriptions.json`）。本包内较早的 `*.log`（无 `final-` 前缀）是同一次迭代的中间运行，保留但不作为最终证据。
- 本文件：`docs/review/tofix/dsh-real-run-init-finish-handoff-2026-10-11.md`（新阶段报告先落 tofix，由负责人并入开放项后再移 records）。
- 方法/契约：`docs/design/mod-the-spire/room-compile-reference.md`、`how-to-add-a-room.md`（本包未改；接口未变，
  `RoomCompileContext` 与营火/宝箱接缝保持原样）。
