---
task_id: ENV-T04
dispatch_id: ENV-T04-initialization-diagnosis-20261008
session_id: session-e1000802-c514-4680-8e63-6e4510518fbc
delivery_status: ready_for_review
updated_at: 2026-10-08T18:58:22+08:00
dispatch_record: .audit/dsh-env-t04-init-diagnosis-2026-10-08/task-record.json
---

# ENV-T04 初始化首差定位与窄校准证据纠正（B 线接续）：ready_for_review — 2026-10-08

本包接续已接收的窄校准包
[ENV-T04 窄源版本派生校准](dsh-env-t04-rng-calibration-handoff-2026-10-08.md)
（原记录 [.audit/dsh-env-t04-rng-calibration-2026-10-08/task-record.json](../../../../.audit/dsh-env-t04-rng-calibration-2026-10-08/task-record.json)）。
唯一目标：用真实 sts2 冻结源码、reference 与 base 当前开局逐段定位同 seed 初始化首差
（遭遇不同、UpFront 18 对 295）的**来源与具体调用/候选数量**，并修好 `RngCrossCheck` 里
「原生 counter0 指纹 vs 参考洗牌后 counter9 指纹」这一处窄证据错误。本包**不**把诊断写成规则通过，
**不**修改任何玩法/初始化实现；`rng-initial-shuffle` 仍以 `diverged` 结束（诚实保留）。

主清单入口：[ENV-T04](../../tofix/README.md#env-t04)；
工作包：[B 完整交付合同](../../../design/post-core-agent-assignments.md#4-bdiff-0102日常严格对拍)；
实现约束：[§10 参考差异与失败处理](../../../design/environment-implementation-constraints.md#10-参考差异与失败处理) 与
[ENV-T04 初始洗牌的派生对齐边界](../../../design/environment-implementation-constraints.md#env-t04初始洗牌的派生对齐边界2026-10-08)；
工具入口：[tools/differential/README.md](../../../../tools/differential/README.md)。

## 结论摘要（可直接引用的读数）

- **277 次额外消费已完整归因，无剩余项**：`295 − 18 = 277` 逐阶段完全对上 —— 参照阶段分解
  `230`（建玩家时两个遗物袋 population：共享池 `112` + 角色袋 `118`）+ `30`（本幕事件列表洗牌）
  + `17`（精英 15 槽，含 2 次重抽）+ `1`（BOSS）+ `1`（古代人）+ `0`（共享先古池只有 `Darv` 1 个候选，
  洗牌 0 次）+ `(−2)`（普通槽阶段两侧抽取次数：参考 `16` 对 base `18`）= `277`。
- **首差机制（定位到调用与计数，不是「池不同」）**：两侧的整幕普通遭遇队列算法逐字一致，
  但**参考的第一个普通槽抽取发生在 `up_front` 计数器 260 处**（230 次遗物袋 population + 30 次事件洗牌之后），
  **base 的同一个槽发生在计数器 0 处**（base 根本没有这些前置阶段）⇒ 同一个弱袋加权抽取选中不同候选：
  base 槽 0 = `shrinker-beetle-weak`（hp 39、`NextMove=shrinkerBeetleShrink`），
  参考槽 0 = `fuzzy-wurm-crawler-weak`（hp 56、tags `["Crawler"]`）。
  池差异（base 已支持 12 个候选 vs 参考 16 个候选）只解释两侧抽取次数 `18` 对 `16` 与可用候选集，**不解释首差**。
- **修正后的初始状态判别已实测通过**（同一 DLL 运行的 `rng`）：两侧都在 `counter=0` 取样 ——
  原生默认状态指纹 `0x9a0e07e8c860a3a8`、装上 `source-compat-sts2-frozen@1` 后 `0xaf4310fd7c212fde`、
  `counter0_states_equal=false`、counter0 首抽 `67` 对 `289`；洗牌后（`counter=9`）路径指纹 `0x6905ad2e367e85e2`
  单列为「路径结果」不再与初始状态混比。源版本 Profile 向量 **检查 48、不通过 0**（原为 46/0，+2 即本包新增的两条判别）。

## 本包新能力

1. **只读诊断入口 `init-upfront`**（新增 `tools/differential/InitUpFrontDiagnosis.cs`，`Program.cs` 注册）：
   ```
   dotnet tools/differential/bin/Debug/net9.0/Differential.dll init-upfront --seed ENV-T04-RNG-SHUFFLE-001
   ```
   做三件事，全部纯观察：① 用 `ReferenceRunEntry.Build` 等价的真实开局逐步（`run_constructed` → `profile_applied`
   → `player_created` → `player_added` → `room_created` → `room_pushed` → `room_entered`）读 `run.Rng.UpFront.Counter`；
   ② 在 `profile_applied` 后取 `run.Rng.UpFront.CloneExact()` 建**独立副本**（`Random/Rng.cs:93-99` 深拷贝 `_random.Clone()`，
   副本抽取不回写真实流），按 `reference/sts2-sim/src/Sts2Sim.Core/Runs/RunState.cs:271-353` 原文重放
   `shared_relic_bag → character_relic_bag → shared_ancients_shuffle → act0_events_shuffle → act0_normal_slots
   → act0_elite_slots → act0_boss → act0_ancient`，逐阶段打印增量并断言总和 == 实测计数（不等则 rc=1 并明说分解未覆盖全部消费点）；
   ③ 打印池全量（各池规模、遗物袋按稀有度桶的 `Σ(桶大小−1)` 期望抽取数）与 15 个普通槽的遭遇 IdEntry 序列。
   输出 `artifacts/environment/init-diagnosis/init-upfront-diagnosis.json`（7036B，本次运行）。
   观察纪律（写在文件头）：不改双方玩法、不注入状态、不强设敌人、不补抽、不删池。
2. **`RngCrossCheck` counter0 判别纠正**（负责人 18:09 指出的窄证据错误）：
   两侧初始状态都在 `counter=0` 取样并各自断言 `counter==0`，新增两条判别「取样时点必须是 counter=0」
   与「两侧 counter0 状态必须不同（否则 Profile 是空操作、检查不判别）」；报告字段改为
   `native_counter0_fingerprint` / `profile_counter0_fingerprint` / `counter0_states_equal` /
   `shuffle_fingerprint_scope`，删除易误读的 `native_shuffle_fingerprint`；`note` 写明
   「初始状态判别取两侧 counter=0 的指纹与首抽；`shuffle_fingerprint` 是洗牌 9 次之后（counter=9）的路径结果，两者不可互比」。
   未扩大成通用框架，只改这一处判别与字段/文案。

## 修改边界（逐项，供负责人核对）

- **修改**：`tools/differential/RngCrossCheck.cs`（counter0 判别与字段/文案）、`tools/differential/Program.cs`
  （新增 `init-upfront` case 与 `Usage()` 两行）、**新增** `tools/differential/InitUpFrontDiagnosis.cs`。
  三处 mtime：`RngCrossCheck.cs` 18:19:31、`Program.cs` 18:25:00、`InitUpFrontDiagnosis.cs` 18:26:54（+08:00）。
- **重新生成（覆盖上一包生成物，需负责人知悉）**：`artifacts/environment/rng-crosscheck.json`
  —— 运行 `rng` 时其 `--output` 参数被该命令忽略（args 里传了 `--output artifacts/environment/init-diagnosis`，
  工具仍写默认路径），因此该文件被本包重新生成：`source_compat.checks 46 → 48`、`failures 0`、
  `areas[0]` 含上述新字段且已无 `native_shuffle_fingerprint`。mtime 2026-10-08T18:59:47+08:00。
  这是同一检查的修正版产物，不是新增口径。
- **新增产物**：`artifacts/environment/init-diagnosis/init-upfront-diagnosis.json`；
  本包 `.audit/dsh-env-t04-init-diagnosis-2026-10-08/init-upfront.log`（18:59 复跑，rc 0）
  与 `.audit/dsh-env-t04-init-diagnosis-2026-10-08/rng-crosscheck.log`（18:59 复跑，rc 0）。
- **未修改**：`mod-the-spire/base-spire`、`net-the-spire`、`core-the-spire`、`sts2`、`reference`、`solution` 与主清单；
  未修改 A/C 两线交接；**未提交 / 未 push / 未 reset / 未清理 / 未训练**；core 未触碰（core 冻结），召唤族未触碰。

## 实测命令与返回码（2026-10-08 18:54–18:59 +08:00，cwd = 仓库根）

| 命令 | 返回码 | 关键读数 | 产物 |
| --- | --- | --- | --- |
| `dotnet tools/differential/bin/Debug/net9.0/Differential.dll init-upfront --seed ENV-T04-RNG-SHUFFLE-001` | 0 | 观察总计数 295 = 230（player_created）+ 65（room_entered）；副本重放 112+118+0+30+16+17+1+1 = 295 与实测一致 | `artifacts/environment/init-diagnosis/init-upfront-diagnosis.json`；日志 `.audit/dsh-env-t04-init-diagnosis-2026-10-08/init-upfront.log` |
| `dotnet tools/differential/bin/Debug/net9.0/Differential.dll rng --output artifacts/environment/init-diagnosis` | 0 | `读数向量：种子 5，一致 5，不一致 0`；`布局向量：不一致 8（其中已登记为参考偏离 8，未登记 0）`；`源版本Profile向量：检查 48，不通过 0` | `artifacts/environment/rng-crosscheck.json`（见上，`--output` 被忽略）；日志 `.audit/dsh-env-t04-init-diagnosis-2026-10-08/rng-crosscheck.log` |
| 根构建（`dotnet build AutoTheSpire.sln -v m`） | **未执行** | `build_after_records` 两线（A `.audit/dsh-env-t03-base-sequences-2026-10-08/task-record.json`、C `.audit/dsh-env-t03-host-sequences-2026-10-08/task-record.json`）在 18:58:22 复核时 `status` 仍为 `accepted`；本包遵守约束，不写共享 core/base obj/bin | — |
| 严格 suite（`test --suite regression`） | **未重跑** | 本包仅有「新增工具命令」与「一处工具内核对逻辑修正」，不改 base/core/reference 行为 ⇒ 既有 2026-10-08T10:02:45Z 结果仍是本 case 的有效严格证据；不无限重跑 | 原 `suite-regression.log` |

**所运行 DLL 的来源（诚实标注）**：本包**未构建**，运行的是
`tools/differential/bin/Debug/net9.0/Differential.dll`（mtime 2026-10-08T18:41:46+08:00，晚于本包三个源码 mtime）；
用 UTF-16 字符串扫描该 DLL 确认含 `init-upfront`、`init-upfront-diagnosis`、`act0_normal_slots`、
`counter0_states_equal`、`shuffle_fingerprint_scope`、`discriminator: 两侧 counter=0` ⇒ 它编译自本包当前源码
（该构建由本机既有进程完成，非本包触发）。参考侧 DLL `reference/sts2-sim/src/Sts2Sim.Core/bin/Debug/net9.0/Sts2Sim.Core.dll`
mtime 2026-10-05T16:53:49（源码未变，与 46 向量校准同一次参考构建）⇒ 本次读数与校准包同一参考版本。

## 证据：逐阶段计数（`init-upfront` 实测，seed `ENV-T04-RNG-SHUFFLE-001`）

真实开局观察（`run.Rng.UpFront.Counter`）：

| 阶段 | 计数 | 增量 |
| --- | --- | --- |
| `run_constructed`（`new RunState(gameSeed, new Overgrowth())`） | 0 | 0 |
| `profile_applied`（`RngSourceCompat.Apply`） | 0 | 0 |
| `player_created`（`Player.CreateForNewRun(ModelDb.Character<Ironclad>(), run)`） | 230 | **+230** |
| `player_added` / `room_created` / `room_pushed` | 230 | 0（`RoomFactory.CreateRoom` 的遭遇闭包惰性，不消费） |
| `room_entered`（`room.Enter(run)` → `EnsureRoomsGenerated`） | 295 | **+65** |

副本重放分解（与实测总和相等）：`shared_relic_bag` 112 → `character_relic_bag` 118 → `shared_ancients_shuffle` 0
→ `act0_events_shuffle` 30 → `act0_normal_slots` 16 → `act0_elite_slots` 17 → `act0_boss` 1 → `act0_ancient` 1。

池数据：共享遗物池 118（桶 `Ancient 2→1 抽`、`Common 25→24`、`Event 1→0`、`Rare 35→34`、`Shop 25→24`、`Uncommon 30→29`，Σ=112）；
角色袋 = 118 + Ironclad 8 = 126、8 个桶 ⇒ 118；`shared_ancient_pool_size 1`；`act0_event_pool_size 31`（Act1 13 + 共享 18）；
`act0_monster_candidates 16`；`act0_elite_candidates 3`；`act0_boss_candidates 3`；`act0_base_number_of_rooms 15`；
`act0_number_of_weak_encounters 3`；`act0_ancient_candidates 1`；`elite_encounter_pool_size 15`。

参考 15 个普通槽：`0:FUZZY_WURM_CRAWLER_WEAK`、`1:NIBBITS_WEAK`、`2:SLIMES_WEAK`、`3:MAWLER_NORMAL`、`4:INKLETS_NORMAL`、
`5:SLITHERING_STRANGLER_NORMAL`、`6:OVERGROWTH_CRAWLERS`、`7:CUBEX_CONSTRUCT_NORMAL`、`8:FLYCONID_NORMAL`、`9:NIBBITS_NORMAL`、
`10:VINE_SHAMBLER_NORMAL`、`11:SLIMES_NORMAL`、`12:FOGMOG_NORMAL`、`13:SNAPPING_JAXFRUIT_NORMAL`、`14:RUBY_RAIDERS_NORMAL`。

两侧记录（`artifacts/environment/tests/regression/records/{base,reference}/rng-initial-shuffle.json` boundary[0]）：
- base：`up_front=18`、`combat-draw=9`、`niche=1`；首敌 `enemy.base-spire:shrinker-beetle`（`hp/max_hp=39`）；
  `Run.EncounterId=base-spire:shrinker-beetle-weak`、`RoomCount=15`、`LastEncounterId=overgrowth-crawlers`、`Relics` 只有 `burning-blood`（无遗物袋 population）；
  base 队列 15 槽 `0 shrinker-beetle-weak`、`1 fuzzy-wurm-crawler-weak`、`2 nibbits-weak`、`3 snapping-jaxfruit-normal`、
  `4 slimes-normal`、`5 ruby-raiders-normal`、`6 slithering-strangler-normal`、`7 flyconid-normal`、`8 nibbits-normal`、
  `9 overgrowth-crawlers`、`10 mawler-normal`、`11 slimes-normal`、`12 slithering-strangler-normal`、`13 ruby-raiders-normal`、`14 overgrowth-crawlers`（袋空重装导致重复）；
  base 已支持遭遇池 12 条 = 弱 4 + 常规 8 ⇒ 相对参考 16 候选缺 4 个常规（`cubex-construct`/`inklets`/`vine-shambler`/`fogmog`）。
- 参考：`up_front=295`、`combat-draw=9`、`niche=1`；首敌 `enemy.base-spire:fuzzy-wurm-crawler`（`hp/max_hp=56`）。
⇒ 18 对 295 已独立复核（同 seed，两侧仅 `up_front` 不同；`combat-draw`/`niche` 两侧一致）。
上一包登记的 `rng-initial-shuffle` 12 条首差清单（含三组敌人事实互缺与两条指纹）**未变**，本包只补齐它的来源；
完整清单见[上一包交接](dsh-env-t04-rng-calibration-handoff-2026-10-08.md)与记录文件。

## 下一生产修改归 A 的文件与原始源语义（本包不动实现）

按源逐段对齐，base 的 `up_front` 消费序列需要补上（源行号；`reference` 侧为同一序列的镜像实现）：

1. `sts2/src/Core/Runs/RunManager.cs:447` 共享遗物袋 population（`State.SharedRelicGrabBag.Populate(..., State.Rng.UpFront)`）与
   `:450` 每玩家 `player.PopulateRelicGrabBagIfNecessary(State.Rng.UpFront)` —— 参考对应
   `reference/sts2-sim/src/Sts2Sim.Core/Runs/RunState.cs:242-258`（shared 袋 + 角色袋，各按稀有度桶 `Shuffle`）。**这是 230 次消费的全部来源**，也是首差的主因。
2. `RunManager.cs:668-692 GenerateRooms()`：`:670` 共享先古 `UnstableShuffle(UpFront)`（本机只有 `Darv` ⇒ 0 次）、
   `:671-677` 后续幕共享先古子集分配（单幕 0 次）、`:686-690` 仅 A10 双 BOSS 第二抽。
3. `sts2/src/Core/Models/ActModel.cs:334-347` 本幕事件池（本幕 + `ModelDb.AllSharedEvents`，按 epoch 过滤）`UnstableShuffle(rng)` —— 参考对应 `RunState.cs:295-298`，**30 次消费的来源**；
   `:348-359` 前 `NumberOfWeakEncounters` 槽弱袋 `AddWithoutRepeatingTags`；`:360-371` 补齐到 `GetNumberOfRooms` 的常规袋同法；
   `:372-383` **精英固定 15 槽**（同一个袋 + 标签回避）；`:384` `rng.NextItem(AllBossEncounters)`；`:385` `rng.NextItem(解锁古代 ∪ 本幕共享子集)`。
4. base 侧接入点：`mod-the-spire/base-spire/BaseSpire/Slice/SliceEncounterPool.cs:95` 与 `Slice/CombatSlicePackage.cs:3815`
   （当前只用 `up_front` 预生成整幕普通队列）、`SinglePlayer/SinglePlayerContent.cs:38-42`（池只绑 `UpFront`/`Shuffle` 两条引擎流）、
   `SinglePlayer/BaseSpireRuntimeFactory.cs:167-183`（自述这些前置阶段与精英/BOSS/古代人未接入）。
   **注意：base 现有普通队列算法本身逐字一致，A 线要补的是「前置阶段 + 精英/BOSS/古代人段」，不是重写队列；**
   补法必须是按源顺序真实消费，不得用「把 base 的流位置硬挪到 260」之类补偿。

## 仍需负责人窄裁定的参考差异（不能由 base 抄参考抹平）

1. **精英槽：本次已核实参考与源同形，无需为该段单独裁决**（原「参考无标签回避 predicate」的说法**作废**——实测参考精英段 17 次抽取（15 槽 + 2 次重抽）曾让人怀疑该处，逐字读码后确认：
   参考 `Content/ActDefinition.cs:152-156 PickEliteEncounter` → `:164-189 PickWithoutRepeating(_eliteEncounterState, EliteEncounters, rng)`，谓词为
   `!ReferenceEquals(candidate, previous) && !SharesTagWith(candidate, previous)`，`previous = state.LastPicked`（上一次精英抽中）；
   源 `sts2/src/Core/Models/ActModel.cs:372-383` 用 `grabBag3` + `:419-430 AddWithoutRepeatingTags`，谓词为
   `!e.SharesTagsWith(encounters.LastOrDefault()) && e != encounters.LastOrDefault()`，即同样「不与上一次抽中重复且不共享 tag」，
   空袋重装条件两边都在抽前检查 ⇒ 语义一致；**残留未验证**：两侧 `EncounterDefinition` 的 tag 集合未逐条比对，若要完全对齐需比 tag 表。
2. 参考 `EffectiveEventPool` 按**全解锁**取集合（`Content/ActDefinition.cs:53` = 本幕 13 + 共享 18 = 31），
   源按 epoch 过滤（`sts2/src/Core/Models/ActModel.cs:334-346`：`unlockState.IsEpochRevealed<Event1Epoch/Event2Epoch/Event3Epoch>` 逐段 `RemoveAll`）
   ⇒ **事件池大小不同会改变其后普通槽的起始位置**（本包实测参考事件洗牌 30 次）。此项在普通槽之前消费，**必须单独裁决**：
   需要负责人给定该 case 的 `unlockState`（或裁定统一按全解锁），不能靠 base 抄参考抹平。
3. 参考省略 `ApplyDiscoveryOrderModifications`（已登记偏离 #179）——已核实该源路径**不消费随机数**（`sts2/src/Core/Models/ActModel.cs:404-417`），只可能影响 BOSS/首次运行遭遇选择。
4. hash 宽度与命名流回绕（源 uint 回绕 vs 参考 XxHash64/ulong）已由 `source-compat-sts2-frozen@1` Profile 覆盖，本包不重开。
   按 §10，上述第 2 项若要判「参考错、源对」，需要负责人给出固定 case ID、source 文件与摘要/定位、两侧版本、允许偏离路径、base 应有结果、以及「参考仍持有该偏离」的断言。
5. 本 case 之外（A10 多幕）另有未验证差异：源的第二个 BOSS 用 `NextItem(AllBossEncounters.Where(e => e.Id != _rooms.SecondBoss?.Id))`（`ActModel.cs:393-396` 在 `ValidateRoomsAfterLoad` 里同名口径），
   参考 `PickEncounterExcluding` 以「传进来的第一个 BOSS」为排除项（`Content/ActDefinition.cs:104-119`）——单幕本局未走到，未验证。

## 未验证与缺口（诚实清单）

- **本包未执行构建**（受 `build_after_records` 约束，A/C 两线 18:58 仍 `accepted`）。「本包源码可编译」的证据是间接的：
  既有构建产物（18:41:46 的 `Differential.dll`）含本包全部源码标记且两条命令均可运行、返回码符合预期。
  接手者/A·T05 在 A/C 停止后应串行执行 `dotnet build AutoTheSpire.sln -v m`（0 错误；已知唯一警告
  `net-the-spire/Net.TheSpire/Projection/PublicSnapshotProjector.cs(667,52)` CS8604）与所需根 Engine/Slice。
- 工具层两处小瑕疵（不影响本次结论，接手可顺手修）：① 诊断 JSON 的
  `records.base_enemy_facts` / `reference_enemy_facts` 为 `{}`（容错递归字段名匹配未命中记录结构），
  两侧敌人事实以两个记录 JSON 直接读取为准（本文已逐字列出）；② stdout 那行
  `参考首个普通战斗遭遇 = FUZZY_WURM_CRAWLER_WEAK（槽位 ）` 的槽位号为空（JSON 里的
  `normal_slot_sequence` 完整无缺）。
- 本包**未**重跑严格 suite，故**不声称** T04 双侧比较通过：`rng-initial-shuffle` 仍为 `diverged`，
  首差清单/指纹不变，只是现在有了精确来源解释；其余 8 passed + 1 self_checked + 5 unsupported 的既有结论未受影响。
- 单 seed 实测：只跑了 `ENV-T04-RNG-SHUFFLE-001`；工具支持任意 `--seed`，其他 seed 的阶段分解未跑。
- 事件池 epoch 过滤项（上面第 2 条）尚无裁决 ⇒ 参考的 `act0_events_shuffle = 30` 与普通槽起始 260 是**参考当前实现**的读数；
  若负责人裁定源应按 epoch 过滤，则起始偏移会变（本包不做该判定，也不改 base 预期值）。
- 两侧 `EncounterDefinition` 的 tag 集合未逐条比对（上面第 1 条的残留）；本局未走到 A10 双 BOSS，第 5 条未验证。

## 接手后的第一步

1. 读[主清单 ENV-T04](../../tofix/README.md#env-t04) 确认本包已交付项与剩余目标（父项保持开放）。
2. 复跑本包两条命令（见上表）确认读数一致；若 `checks` 不是 48/0 或重放总和不等于 295，本包证据即失效。
3. A/C 停止后串行构建（根 → 所需 Engine/Slice）→ 由 A/T05 按上文「下一生产修改归 A 的文件与原始源语义」
   补 base 的 `up_front` 前置阶段与精英/BOSS/古代人段；对「仍需窄裁定」的两项先取负责人裁决，再决定 base 的期望结果。

## 验证与证据（命令 → 期望）

- `init-upfront --seed ENV-T04-RNG-SHUFFLE-001` → rc 0；观察 295；重放 112/118/0/30/16/17/1/1 且总和 295。
- `rng` → rc 0；读数向量 5/5；布局不一致 8（已登记偏离 8、未登记 0）；源版本 Profile 向量检查 48、不通过 0。
- `artifacts/environment/rng-crosscheck.json` → `source_compat.checks=48`、`failures=0`、`areas[0].counter0_states_equal=false`，
  且不再有 `native_shuffle_fingerprint`。

## 19:09负责人接收说明

实际会话停止，结果取齐至745，turn/end completed。完整745结果hasMore=false；负责人当前DLL独立init-upfront rc0，真实295与副本112/118/0/30/16/17/1/1一致，rng校准48/0 rc0，同counter0状态判别成立。源码/执行输入保存本轮audit。核验的是参考阶段分解和工具修正，不能将参考池数量当源版本完整期望。项目RunConfiguration及有效约束已固定UnlockPolicy.All，无需再询问解锁政策；源全解锁池/事件/tag/顺序仍需具体证据。本报告用记录accepted解释是否运行不成立，真实状态应读sessions.running；未重新构建不等于当前源码永久被阻塞。详见[当日复核](dsh-supervisor-review-2026-10-08.md#1909复核与声明扩展源输入接续)。移入records只接收日期证据，不关闭父项。
