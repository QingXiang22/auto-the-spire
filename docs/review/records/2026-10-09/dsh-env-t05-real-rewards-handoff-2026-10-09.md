# ENV-T05 真实奖励闭环机制包 — 交接（blocked）

| 字段 | 值 |
| --- | --- |
| `task_id` | ENV-T05 |
| `dispatch_id` | ENV-T05-real-rewards-20261009 |
| `session_id` | session-f6bce9bc-4f1f-4d1e-9375-af382f545d5d |
| `request_id` | 315f5fa3-286c-4921-af65-66e639f089b9 |
| `delivery_status` | `blocked` |
| `updated_at` | 2026-10-09 01:08:25 +08:00 |
| `dispatch_record` | `.audit/dsh-env-t05-real-rewards-2026-10-09/task-record.json` |

关联：主清单 [ENV-T03/T04/T05](../../tofix/README.md#env-t03)；设计 [环境实现约束](../../../design/environment-implementation-constraints.md)、
[base-spire 实现计划](../../../design/mod-the-spire/base-spire-implementation-plan.md)；
本包记录与设计 [`.audit/dsh-env-t05-real-rewards-2026-10-09/`](../../../../.audit/dsh-env-t05-real-rewards-2026-10-09)（含 `reward-design-2026-10-09.md`）；
前置 B `.audit/dsh-env-t04-map-resume-2026-10-09/task-record.json`，后继 C `.audit/dsh-env-t03-reward-public-2026-10-09/task-record.json`。

**一句话交付**：本轮完成了普通战斗房真实奖励的**冻结源依据与实现方案**（奖励调用链、11 次随机消费时序、
稀有度/牌池判定、阶段机与幂等设计），但**未交付可运行机制包**——共享编译门（B 地图对拍）实测仍未关闭，
按派单「只有指定前置会话实际停止、前置交接已结束时才改参与编译的文件或构建」不得改 base、不得构建。

## 接手后先做什么

1. 复测前置门：`python C:\Users\26877\.codex\skills\dsh-remote-operator\scripts\dsh_client.py status --record .audit/dsh-env-t04-map-resume-2026-10-09/task-record.json`
   （**必须带 `--record`**）。要求 `sessions[].running == false` **且** `docs/review/tofix/dsh-env-t04-map-resume-handoff-2026-10-09.md`
   已存在（`Test-Path` 为真）。两者缺一即继续 blocked；**不得**用 `status: accepted`、文件 mtime 静止或常驻 MSBuild 进程代替。
2. 门开后按 `.audit/.../reward-design-2026-10-09.md` §9 的 8 步串行实现（先补 `NextFloat()` 等价纯计算资产，再阶段机，再奖励实体）。
3. 复用本包已确认的源行号，不要重读全链；先读 `reward-design-2026-10-09.md` §1–§3 即可上手。
4. 父项**不得**据此关闭；`accepted` / `running=false` / 绿色总数都不是关闭依据。

## 已交付与本轮实际改动

| 路径 | 内容 |
| --- | --- |
| `.audit/dsh-env-t05-real-rewards-2026-10-09/reward-design-2026-10-09.md` | 源调用链（§1）、`rewards` 流 11 次抽取时序（§2）、稀有度/牌池依据（§3）、base 现状与改动点（§4）、阶段机设计（§5）、幂等与长期写回（§6）、药水容量（§7）、开放项（§8）、门开后 8 步（§9） |
| `docs/review/tofix/dsh-env-t05-real-rewards-handoff-2026-10-09.md` | 本交接（唯一维护入口） |

**未改**：`mod-the-spire/base-spire/**` 任何参与编译的源、`core-the-spire/**`、`reference/**`、`net-the-spire/**`、
`tools/**`、`docs/review/tofix/README.md`（主清单归负责人）、他线生产与 `solution`。
本轮无新增/修改的生产代码，因此没有源码/DLL 摘要变化。

### 本包确认的关键事实（供接手直接复用）

- 仓库**不存在** `UpgradeReward`；派单所指占位实现叫 **`UpgradeLongTermCard`**，位于
  `mod-the-spire/base-spire/BaseSpire/Composition/CombatSlicePackage.cs:4684-4737`，且**只在 `hasRun && actMap is null` 时注册**
  （`CombatSlicePackage.cs:4663-4738`）⇒ DevMap 下**完全没有奖励动作**，`MapPhase = 2` 直接开放 `ChooseMapPoint`。
- 开局根因：`CombatSlicePackage.cs:3884 setupSteps.Add(setup.Call(startNextRoom!.Symbol))` **不区分 `actMap`**，
  开局即铺第一间房战斗，与 `MapPhase = 0` 并存 ⇒ 派单所述「DevMap 开局同时暴露 PlayCard/EndTurn/ChooseMapPoint」。
- 奖励插入点：`CombatSlicePackage.cs:455-489 SettleBattle`，`actMap != null` 时重发 `Body` 追加
  `actMap.SettleStep(...)`（`MapRoomWiring.cs` 写 `Run.MapPhase = 2`）。
- 战斗结束顺序（源权威）：`sts2/src/Core/Combat/CombatManager.cs:970 EndCombatInternal()` 中
  `:988 AfterCombatEnd` / `:999 AfterCombatVictory` / `:995-998 AfterCombatEnd()` 全部在 `:1011 MarkPreFinished()` 之前
  ⇒ 「末次击杀余效先完再开礼」由源自身的 `MarkPreFinished` 边界保证；失败路径 `:963 ProcessPendingLoss()` **不给奖励**。
- 写回点：`CombatSlicePackage.cs:437-446 CommitPermanent` 只写 `Run.Hp`/`Run.MaxHp`，注释明确 `Run.Gold`
  「目前没有战斗内来源，因此不在写回集合」⇒ 金币奖励落地时必须同步该注释与写回集合。

## 源依据

派单 `.audit/dsh-env-t05-real-rewards-2026-10-09/task.txt`；冻结源 `sts2/src/Core/**` 只读核对，主要定位：

- `sts2/src/Core/Commands/RewardsCmd.cs:20,55`、`sts2/src/Core/Rewards/RewardsSet.cs:85,125-147,224-231,248-256`；
- `sts2/src/Core/Rewards/{Reward,GoldReward,PotionReward,CardReward}.cs`；
- `sts2/src/Core/Factories/CardFactory.cs:89-109,214-241,236,244-261,263-281,288-305`、`:23 UpgradedCardOddScaling`；
- `sts2/src/Core/Odds/{CardRarityOdds,PotionRewardOdds}.cs`、`sts2/src/Core/Runs/CardRarityOddsType.cs`、`sts2/src/Core/Runs/CardCreationOptions.cs:102-130`；
- `sts2/src/Core/Models/EncounterModel.cs:64-100`、`sts2/src/Core/Rooms/CombatRoom.cs:51,235,267`、`sts2/src/Core/Rooms/TreasureRoom.cs:82`；
- `sts2/src/Core/Commands/{PlayerCmd.cs:141,PotionCmd.cs:28}`、`sts2/src/Core/Entities/Players/Player.cs:34,42,166,581-613,624`；
- `sts2/src/Core/Random/Rng.cs:89-109,145-164,255`；
- base 侧：`mod-the-spire/base-spire/BaseSpire/Rooms/MapRoomWiring.cs`、`:CombatSlicePackage.cs`（上表行号）、
  `SinglePlayer/BaseSpireRuntimeFactory.cs`、`SinglePlayer/RunConfiguration.cs`、`Cards/SpireCardSource.cs`、`Randomness/SourceWeightedPick.cs`；
- 引擎侧：`core-the-spire/Core.TheSpire/Engine/{RandomProfile,RandomService,Session.Instructions,Schema,PackageBuilder}.cs`。

## 源码 / DLL 摘要、config、seed、run_id、命令返回码

**本轮未构建、未运行 Run，故无可采集的 source/DLL 摘要或 run_id。** 已执行命令只有两类：

| 检查 | 工作目录与实际命令 | 返回码/结果 | 证据与证明范围 |
| --- | --- | --- | --- |
| 前置门 B 会话 | `F:/Scylier-Project/active/auto-the-spire`：`python C:\Users\26877\.codex\skills\dsh-remote-operator\scripts\dsh_client.py status --record .audit/dsh-env-t04-map-resume-2026-10-09/task-record.json` | rc=0；`running: true`，`asOfSeq` 105→133→255→382→**475→506**（持续推进） | 证明编译门关闭；`Test-Path docs/review/tofix/dsh-env-t04-map-resume-handoff-2026-10-09.md` = `False` |
| 工作区状态 | `git status --short` | rc=0，130 行改动/未跟踪 | 仅用于确认未做提交/清理；本包未新增工作区条目以外的生产改动 |
| 前置文档检查 | `python tools/doc-check/check_docs.py`、`git diff --check` | **未运行**（本包新增文件位于 `.audit/` 与 `docs/review/tofix/`，接手或负责人可按需补跑） | — |

历史值（**不是**最终值，A 落盘后须重测）：准备期主清单记 dev `ruleset 6ac4cb3cb2b696c6` / build `f291cc66cf6a9050`，
dev-map `ruleset a1d8aa9faa64b143` / build `6b25fa16e5b567f4`。
计划 seed：沿用 `t05-map-alpha` / `t05-map-beta` 原尝试，另加 C 线登记的 `t05-reward-alpha` / `t05-reward-beta` 回归。

## 真实证据

**无（闸门未开，无构建、无 Run、无轨迹）**。全部结论为**只读源核对**（源码行号可逐条复核），
不是行为实测；不得表述为已实现或已通过。

## 首个差异

**尚未取得**（未运行）。已知并须在本包留意的一处**未验证保真风险**（不是首差，是首差候选）：
源 `GetUnlockedCards` 的**声明顺序**与 base `SpireCardSource.CardResources()` 的
`Order(StringComparer.Ordinal)` 顺序是否一致——它决定 `rng.NextItem(items)` / 引擎 `SamplePick` 抽到哪张牌。
若不一致，即为奖励轨迹的第一处差异，须交 B 对拍侧确认，不自行假设一致。

## 未执行 / 未验证

- 未改任何参与编译的 base 源，未新增奖励动作、未改阶段机、未实现金币/卡牌/药水奖励、未做幂等与长期写回。
- 未构建：根 `dotnet build AutoTheSpire.sln`、`dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests`、
  `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` 均**未运行**（旧 red 态不得说成通过）。
- 未运行正常闭环（`t05-map-alpha`/`beta`），因此「正常初始地图选点不能跳过战斗」**未验证**。
- 未做选牌/跳过/容量/幂等的受控判别检查（尚未实现）。
- 未实现药水**替换/丢弃**路径；未实现精英/ Boss /宝箱/商店奖励；`rng-initial-shuffle`（源完整初始化）仍独立开放。
- `EncounterPool/Queue` 投影 size/opaque 身份与 NotPublished 仍**保持开放**（派单要求）；本包未隐藏/未公开未来遭遇，语义裁定另需负责人。
- 「新增双实例检查现仅 `>=1`」的证据边界**未**被本包润色，保持原状。

## 下一步（具体动作）

1. **谁**：接手 ENV-T05 的会话；**前置**：B 会话 `running == false` 且
   `docs/review/tofix/dsh-env-t04-map-resume-handoff-2026-10-09.md` 存在。
2. **在哪**：`mod-the-spire/base-spire/BaseSpire/Slice/`（新增纯计算资产 + `CombatSlicePackage.cs` + `MapRoomWiring.cs`）。
3. **做什么**：按 `reward-design-2026-10-09.md` §9 顺序实施；先落 `NextFloat()` 等价计算资产（后端 id 进 `PublicBuildId`，
   属公开身份变化，须在交接口径里声明），再阶段机（`MapPhase Int(0,3)`，`ChooseMapPoint.Requires ∈ {0,3}`，
   `CombatSlicePackage.cs:3884` 在 `actMap != null` 时不再预开房间）。
4. **预期看到什么**：DevMap 开局只有 `ChooseMapPoint` 可选；选点后进入战斗（`MapPhase = 1`，无旅行）；
   击杀余效走完 → `MapPhase = 2` 开出真实奖励（金币/卡牌 3 候选/药水按容量）；领取或跳过后 `MapPhase = 3` 且出现合法后继。
5. **如何判断完成**：受控判别检查（选牌 / 跳过 / 容量 / 幂等）与固定 seed 正常闭环成功**分列**存在且为绿；
   串行一次根构建 + Engine/Slice 检查保留全部 strict 分母；交接收在 `ready_for_review`。

## 阻塞与依赖（具体 ID 与缺口）

- **阻塞 ID**：`.audit/dsh-env-t04-map-resume-2026-10-09/task-record.json`（B 会话 `session-8cf3be04-54db-46da-84c6-c97820fb95db`，
  task `ENV-T04-map-differential-resume-20261009`）。实测 `running: true`，`asOfSeq` 506（2026-10-09 01:08 观测）。
- **缺口**：前置交接 `docs/review/tofix/dsh-env-t04-map-resume-handoff-2026-10-09.md` 不存在。
- **受阻范围**：所有「参与编译的 base 修改」与共享构建；即本包的全部实现与验证动作。
- **不受阻、可继续**：本包 audit 内的只读源核对与设计（已完成，见 `reward-design-2026-10-09.md`）；
  门开后第 1 步可直接开工，无需重新侦察。
- **不阻塞**：C 线 `.audit/dsh-env-t03-reward-public-2026-10-09/task-record.json` 的公开奖励轨迹按顺序排在本包之后，
  其准备期只读工作不受本包影响。

## 维护位置

- 任务状态/认领/下一成果：主清单 `docs/review/tofix/README.md` 对应 ENV-T05 项（**由负责人维护**，本包不编辑）。
- 本包方法与源依据：`.audit/dsh-env-t05-real-rewards-2026-10-09/reward-design-2026-10-09.md`。
- 本包交付与证据：本文件；后续门的复测结论追加到同一处或另开带日期的 records 记录。
