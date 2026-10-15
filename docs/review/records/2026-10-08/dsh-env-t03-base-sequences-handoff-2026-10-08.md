---
task_id: ENV-T03
task_name: base 公开集合语义修复：交可保留真实公开顺序的元数据
dispatch_id: ENV-T03-base-public-sequences-20261008
session_id: session-fb517afd-8354-4a23-85e1-4f45550ebdda
dispatch_record: .audit/dsh-env-t03-base-sequences-2026-10-08/task-record.json
delivery_status: ready_for_review
updated_at: 2026-10-08T18:08:41+08:00
---

# ENV-T03 A 线：base 公开集合顺序语义元数据修复 — 2026-10-08

本包是 ENV-T03 的 A 线接续子包：修掉上一包（`ENV-T03-base-public-metadata-20261008`）里**与实际公开声明
冲突的集合用途**——`PlayedThisTurn` 被写成 `NotPublished`，而 `CombatSlicePackage` 的集合声明
（`OrderVisible=true`、成员种类在所选投影里）与 `Projection` 的真实发布行为说明成员/顺序/数量确实公开。
本包只改 base 侧的**不可变语义元数据**：新增 `BasePublicCollectionUse.Sequence` 与 owner 解析，
把「已公开成员身份 + 公开顺序」的集合逐条改成能保留真实顺序的载荷，并把具名不发布收紧到
「成员种类根本不在所选投影里」这一种结构原因。**不**接通 Host、**不**进入 T05、**不**关闭 T03。

归属：主清单 ENV-T03「公开Host接唯一工厂；从规则声明派生公开出口」（M01 / P1；关联 NET-01、ENV-05）。
交接入口：主清单 ENV-T03 条目 → 本文件；上游依赖包
[base 公开语义元数据交接](dsh-env-t03-base-metadata-handoff-2026-10-08.md)；
下游是 C 线[公开 sequence runtime 交接](dsh-env-t03-host-sequences-handoff-2026-10-08.md)。
交付范围：负责会话 `session-fb517afd-8354-4a23-85e1-4f45550ebdda`；只写 `mod-the-spire/base-spire/**`、
本文件与 `.audit/dsh-env-t03-base-sequences-2026-10-08/**`。`net-the-spire`、`tools`、`core-the-spire`、
`reference`、`ai-the-spire`、主清单只读。
本报告记录 2026-10-08 的交付；当前排期、状态与认领以主清单为准。

## 接手后先做什么

1. **不要重写**元数据类型与构造期闭合：`mod-the-spire/base-spire/BaseSpire/SinglePlayer/BasePublicSemantics.cs`
   已有内容目录/集合用途/已知位置/终局含义四类含义与 `BasePublicSemanticsBuilder.Build`；本包只在它上面
   加 `Sequence` 并改集合用途，8 条原有检查在 `BaseSpire.Slice.Tests/PublicSemanticsChecks.cs`。
2. 下一步（C 线）：按「C 接入步骤」把 `Use` 翻成 net 的 `PublicCollectionRule.Sequence(ownerKindEngineTypeId)`，
   并给 `PublicSnapshotProjector` 加通用 owner **种类**唯一解析（旧固定 `owner_ref` 路径保留、wire 不改）。
   现状：`net-the-spire/Net.TheSpire.Host/FormalDevMetadata.cs` 的 `switch` 对 `Sequence` 走 `_ =>` 抛
   `NotSupportedException` —— 这是 C 必须补的那个分支，base 侧不代写。
3. 完成条件见下节；本包只交付 base 侧元数据 + 检查 + 文档，公开轨迹与身份接线由 C 验证。

## 原完成条件（保留逐项，本包范围内）

| # | 原条件 | 本包状态 |
| --- | --- | --- |
| 1 | 正式工厂给出只读、不含每局 seed、同配置稳定的内容/集合/位置/终局含义 | 已在上包交付，本包保持（增量见下） |
| 2 | 公开集合的身份/顺序/数量事实不得被具名不发布或无序载荷丢弃 | 本包修（`PlayedThisTurn` 等） |
| 3 | `CanonicalText`/`Digest`/`PublicBuildId` 纳入集合用途与 owner | 本包修（新增 sequence 分支） |
| 4 | 逐条给出集合的 `ParticipantView` 可见事实与是否已被另一载荷完整表达 | 本包给出（下表 + 证据） |
| 5 | 同工厂正常 describe/catalog/start/observe/step 公开轨迹、跨 seed owner 绑定 | **未交付**：C 线范围 |
| 6 | 只改规则包即可新增公开字段/Action（net 不加逐项注册） | **未交付**：T03 后续子包 |
| 7 | T05 地图/奖励新选择 | **未交付**：T05 |

## 交付内容与接口变化

`mod-the-spire/base-spire/BaseSpire/SinglePlayer/BasePublicSemantics.cs`（唯一新增接口）：

```csharp
public enum BasePublicCollectionUse { NotPublished = 0, Pile = 1, Group = 2, /* 新增 */ Sequence = 3 }

// 与 Group 同形：owner 是**领域实体的种类**（引擎限定名），运行时按"该种类唯一可见实例"解析；
// 不写死每局公开句柄。顺序按集合真实顺序发布（protocol.md §2：sequence 的 elements 顺序有公开语义）。
public static BasePublicCollectionSemantics Sequence(string ownerKindEngineTypeId);
```

- `Describe(collectionId, semantics)` 新增 sequence 分支：`<id>|sequence|owner=<ownerKindEngineTypeId>`，
  因此 use 与 owner 都进入 `CanonicalText()` → `Digest` → `PublicBuildId`。
- `CollectionIntents` 的 owner 槽复用（`Group`/`Sequence` 都用它）；构造期新增三条判别（见「判别检查」）。

## 集合逐项裁定（以 `CombatSlicePackage` 实际声明为主要证据）

| 集合 | 元素 | 引擎声明 | 裁定 | owner | 依据 |
| --- | --- | --- | --- | --- | --- |
| `Hand` | Card | group=pile，identity/size/order=true（cap 10） | Pile `HandPile` | — | 牌区，牌堆块是成员唯一事实来源 |
| `DrawPile` | Card | group=pile，**identity=false order=false** size=true | Pile `DrawPileEntity` + 位置 `card/top/0` | — | 成员/顺序声明即隐藏，大小公开 |
| `DiscardPile` | Card | group=pile | Pile `DiscardPileEntity` + 位置 | — | 同上 |
| `ExhaustPile` / `InPlay` | Card | group=pile | Pile `ExhaustPileEntity` / `InPlayPileEntity` | — | 同上 |
| `PlayedThisTurn` | Card | **无 group**，identity/size/order=true | **Sequence** | `Player` | 本回合出牌流的顺序是公开游戏事实（上包写 NotPublished，与实际发布冲突） |
| `Monsters` | Monster | 无 group，order=true | **Sequence** | `Player` | 敌人顺序=行动顺序；战斗作用域没有独立实体，`Player` 是该场战斗唯一的战斗参与者实体（战斗副本） |
| `Known` | KnownCard | 无 group，order=true | **Sequence** | `Player` | `KnownCard.OwnerFieldName = "Viewer"`，该事实的领域 owner 是参与者；实体单独发布不等于集合顺序已表达 |
| `RunStates` | Run | 无 group，order=true | **Sequence** | `Run` | 成员就是 `Run` 单例自身；发布为 sequence 不再"用自指 owner 重复表达实体存在"，而是忠实表达声明的成员/顺序 |
| `LongTermDeck` | Card | 无 group，identity/size/order=true | **Sequence**（原 Group） | `Run` | Group 是无序载荷，会丢声明的公开顺序 |
| `Relics` | Relic | 无 group，identity/size/order=true | **Sequence**（原 Group） | `Run` | 同上 |
| `EncounterPool` | EncounterPoolEntry | 成员种类**不在投影** | NotPublished（**受阻**） | — | 见「差异、阻塞」 |
| `EncounterQueue` | EncounterQueueEntry | 成员种类**不在投影**、`Index` 隐藏 | NotPublished（**受阻**） | — | 见「差异、阻塞」 |

owner 规则（本包新增、由构造期强制）：owner 必须是**所选投影里的真实实体种类**，且在该快照里
唯一解析（唯一性由运行时检查核对）。战斗作用域用 `Player`，整局作用域用 `Run`。

## 判别检查（构造期抛错 + 检查逐行核对）

1. **具名不发布必须有结构证据**（构造期抛）：`NotPublished` 只允许在「该集合的成员种类不在所选投影里」时使用；
   否则抛 `InvalidOperationException`（成员种类在投影里 ⇒ 投影确实发布成员事实，不得用具名理由丢弃）。
   上包的 `PlayedThisTurn` / `Monsters` / `Known` / `RunStates` 会各自触发这条。
2. **公开顺序不得落进无序载荷**（构造期抛）：`IdentityVisible && OrderVisible && 成员种类在投影里` 的集合不得是
   `Group`；必须 `Pile`（牌区）或 `Sequence`（流程）。上包的 `LongTermDeck` / `Relics` 会触发这条。
3. **有序载荷不得泄漏隐藏事实**（构造期抛）：标成 `Sequence` 的集合必须 `IdentityVisible && OrderVisible`，
   否则不得用有序载荷承载（DrawPile 这类声明隐藏成员/顺序的集合只能走 `Pile`）。
4. **owner 与摘要一致**（检查逐行核对）：owner 必须是投影里的真实种类，且 use+owner 出现在 `CanonicalText()` 里
   （即进入 `Digest` 与 `PublicBuildId`）。`BaseSpire.Slice.Tests/PublicSemanticsChecks.cs` 新增第 9 条检查
   断言 1–4 的整组（含 `sequence=6` / `notPublished=2`），并把原第 6 条的 owner 分支从 `Group` 扩到
   `Group || Sequence`、逐条核对 6 条 sequence 的元素种类。

## 验证与证据

- **前置状态**：B 线校准交接 `docs/review/tofix/dsh-env-t04-rng-calibration-handoff-2026-10-08.md`
  于 2026-10-08T18:04:07 变为 `ready_for_review`（最后日志 `doc-check.log` 18:03:28）⇒ 按 B→A→C
  顺序，确认前置停止后开建。C 线交接当时仍 `in_progress`。
- **构建门槛**（原始日志在 `.audit/dsh-env-t03-base-sequences-2026-10-08/artifacts/`）：

  | 命令 | 返回码 | 结果 | 日志 |
  | --- | --- | --- | --- |
  | `dotnet build mod-the-spire/base-spire/BaseSpire/BaseSpire.csproj -v q` | 0 | 0 错误 / 1 警告（`Slice/SpirePowerSource.cs(1241,39)` CS8604，预存在） | `01-base-build.txt` |
  | `dotnet build AutoTheSpire.sln -v q` | 1 | 唯一错误在 C 线在制文件 `net-the-spire/Net.TheSpire/Projection/DerivedPublicProfile.cs(375,29)` CS1739（见下节） | `02-root-build.txt` |
  | `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` | 0 | `All 95 engine checks passed.` | `03-engine-tests.txt` |
  | `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | 0 | `All 333 slice checks passed.`（原 332 + 本包新增 1 条判别） | `04-slice-tests.txt` |
- **实测摘要值**（`04-slice-tests.txt` 的 ENV-T03 行）：
  `semantics=9d92f078cdb3ff5f build=52b285cbc40711cc ruleset=04078d3be6c4b66c`；
  `ENV-T03 集合用途：sequence=6 notPublished=2 pillar/pile=5`。
- **`CanonicalText()` 实测行**：`collection=base-spire:PlayedThisTurn|sequence|owner=base-spire:Player`、
  `base-spire:Monsters|sequence|owner=base-spire:Player`、`base-spire:Known|sequence|owner=base-spire:Player`、
  `base-spire:RunStates|sequence|owner=base-spire:Run`、`base-spire:LongTermDeck|sequence|owner=base-spire:Run`、
  `base-spire:Relics|sequence|owner=base-spire:Run`；
  `collection=base-spire:EncounterPool|not-published|base-spire:N-02-BLOCKED-CONTENT-POOL`、
  `collection=base-spire:EncounterQueue|not-published|base-spire:N-02-BLOCKED-INTERNAL-QUEUE`；
  五条牌堆行与上包一致。目录实测：卡 68 / 能力 29 / 敌人 35 / 遗物 1。
- **纯文档门槛**（`05-doc-check.txt`、`06-git-diff-check.txt`）：`python tools/doc-check/check_docs.py`
  首轮返回码 7，其中 1 条属于本文件（上一包交接的相对路径少了一层，已修为
  `../records/2026-10-08/dsh-env-t03-base-metadata-handoff-2026-10-08.md`）；修后复跑返回码 6，
  剩下 6 条**全部**是 C 线在制文件 `docs/review/tofix/dsh-env-t03-host-sequences-handoff-2026-10-08.md`
  的相对路径（深度多算了一层，如 `../../../../.audit/…`），非本包写范围，未改。
  `git diff --check` 返回码 0（只有 LF/CRLF 提示，无空白错误）。
- **源码 / 产物摘要**（SHA-256 前 16 位）：`SinglePlayer/BasePublicSemantics.cs` =
  `3a09d1297a45dfb4`（18:03:19）、`BaseSpire.Slice.Tests/PublicSemanticsChecks.cs` = `aa6207a02b8a906c`（18:04:17）、
  产物 `BaseSpire/bin/Debug/net9.0/BaseSpire.dll` = `ce81f096d5935ea7`（18:05:33）、
  测试 `BaseSpire.Slice.Tests/bin/Debug/net9.0/BaseSpire.Slice.Tests.dll` = `8f7a2e601aad7303`（18:05:44）
  —— 两个 DLL 的写入时间均晚于其源码，证据不来自陈旧产物。
- **检查输入**：`RunConfiguration.Ironclad()` + `SinglePlayerContentSource.Shared` + `SpireRelics.All`，
  里程碑 seed `ENV-IRONCLAD-00`（`PublicSemanticsChecks.MilestoneSeed`）；本包不产生 Host 轨迹的 `run_id`
  （公开 describe/start/observe/step 轨迹属 C 线原条件 5），故不以 run_id 作证据。

## 差异、阻塞与未验证

- **根构建未通过，原因不在本包**：`dotnet build AutoTheSpire.sln` 返回码 1，唯一错误
  `net-the-spire/Net.TheSpire/Projection/DerivedPublicProfile.cs(375,29): error CS1739: "ForOwnerKind"的最佳重载没有名为"MinCount"的参数`
  —— C 线正在补 sequence 支持的中间状态。本包前序状态：B 线校准交接 2026-10-08T18:04:07 `ready_for_review`
  （最后日志 18:03:28）；C 线交接仍 `in_progress`。按派单「记录首错与范围、不修他人、不把失败写成通过」处理：
  base 项目自身构建、Engine 95 项、Slice 333 项全绿，根构建待 C 线自洽后复跑。
- **受阻项 1 `EncounterPool`**：成员种类 `EncounterPoolEntry` 不在 `base-spire:PlayerView` 投影里，
  没有任何已发布载荷能承载其成员 ⇒ 元数据只能具名不发布。最小可表示形状：把 `EncounterPoolEntry`
  加入投影并只公开 `EncounterId`/`Kind`（`BagUsed`/`Tag_*` 保持隐藏），再发布为 sequence/group。
  受影响范围：构建期内容池的成员集合；**不影响**当前房间遭遇（`Run.EncounterId`）。
  属 T05/规则包范围，交负责人裁定。
- **受阻项 2 `EncounterQueue`**：成员种类 `EncounterQueueEntry` 不在投影里、`Index` 字段隐藏；
  发布成员身份等于公开**后续房间**的遭遇顺序，而当前范围没有地图/房间顺序的公开语义（T05）。
  最小可表示形状：投影 `EncounterQueueEntry` 并公开 `EncounterId`（`Index` 保持隐藏），
  由 T05 的地图/房间顺序语义决定是否发 sequence。受影响范围：整幕队列顺序；**不影响** `Run.CurrentRoom`/
  `Run.RoomCount` 已公开的事实。
- **未验证**：同工厂公开轨迹、跨 seed owner 绑定、`StaticBuildIdentity` 随之变化 —— C 线范围。
  `PublicBuildId` 值随本包再次变化（`semantics=` 维度），旧记录值作废，本包实测
  `semantics=9d92f078cdb3ff5f build=52b285cbc40711cc`（见上节）。
- **跨线影响（C 必读）**：`Sequence` 是新增枚举成员；`net-the-spire/Net.TheSpire.Host/FormalDevMetadata.cs`
  的 `switch` 目前对未知成员走 `_ => throw new NotSupportedException`，**能编译但运行时抛错**，
  直到 C 补上 sequence 分支与通用 owner 解析。base 侧不引用 net，也不代写该分支。

## C 接入步骤

1. `FormalDevMetadata`：加 `BasePublicCollectionUse.Sequence => PublicCollectionRule.Sequence(
   declared.OwnerKindEngineTypeId ?? throw ...)`（沿用 Group 的 owner 解析形状）。
2. `DerivedPublicProfile` / `PublicCollectionRule` / `PublishedSequence` / `PublicSnapshotProjector`：
   把 `BuildGroup` 已有的「按 owner 种类取唯一可见实例」路径泛化给 sequence；保留旧固定 `owner_ref` 路径；
   owner 缺失或有歧义显式拒绝，不猜。
3. 核对（A 侧实测值见本文件「验证与证据」）：6 条 sequence 的 use/owner；`CanonicalText()` 里
   `collection=<id>|sequence|owner=<限定名>` 逐条存在；`PlayedThisTurn` 的两次合法出牌顺序与
   `ParticipantView` 一致（成员/顺序/数量都不丢）。

## 维护位置

- 状态与认领：主清单 ENV-T03 条目（负责人维护，本包未编辑）。
- 方法与 API：`mod-the-spire/base-spire/README.md` 的「公开语义元数据」节（本包同步更新 `Collections` 行）。
- 派单记录：`.audit/dsh-env-t03-base-sequences-2026-10-08/task-record.json`。
- 本次证据与原始输出：`.audit/dsh-env-t03-base-sequences-2026-10-08/artifacts/`。
- 后续证据：负责人并入主清单后转 `docs/review/records/2026-10-08/`。

## 18:39负责人核验与归并

实际会话停止，完整结果取齐至475、hasMore=false、turn/end completed；task/dispatch/session与派单记录一致。BasePublicSemantics.cs/PublicSemanticsChecks.cs当前完整SHA256分别3a09d1297a45dfb4ef796a449543a2da8041d9723122193746b258b01907df4d、aa6207a02b8a906c2bbdb5782ab7a36087370e581d5a151f9f14fb602f54c676，与日志摘要对应。执行者Engine95/Slice333返回0证据复用，负责人不在B/C运行中重建共享obj/bin。
核验范围＝六类公开有序集合的Sequence/领域owner元数据与摘要接线；保持base→core，无net依赖。真实公开轨迹与owner绑定仍交C，不据本包关闭T03。EncounterPool/EncounterQueue虽成员种类不在实例投影，core Projection.cs110–126仍发布其集合size（及IdentityVisible时成员ID），所以“成员类型未投影”不能证明整个集合无公开事实；此窄缺口继续开放，不以BLOCKED命名Skip代替显式不可编码处理，也不直接公开未来遭遇或更改规则标记来逃避。
A可以接T05第一个地图/房间完整子包；C/B继续已认领包，A新包待其停止才改变base输入和构建。详见[当日复核](dsh-supervisor-review-2026-10-08.md#1839复核与t05地图房间接续)。转入records仅归并已接收证据，父项与M01保持开放。
