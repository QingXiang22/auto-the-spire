# ENV-T09 真实问号房与独立事件模块交接 — 2026-10-12

- task_id: `ENV-T09-unknown-room-modules-20261012`
- session_id: `session-b4f2d691-688b-494a-83d2-ed06ad4b5ad9`
- request_id: `e749b313-a4a9-4889-bae6-da6baac1e40d`
- url: `https://124.221.182.144`（[派发记录](../../../.audit/dsh-unknown-room-modules-20261012/task-record.json)、[短工作包](../../../.audit/dsh-unknown-room-modules-20261012/task.txt)）
- delivery_status: `ready_for_review`（本包交付的行为与证据已在正常公开流程上实地跑通；**不等于验收**：本包不关闭父项 ENV-T09、不启动下一包、不写主清单）

> 主清单由 Codex 独自修改：本包**没有**修改 [主清单](README.md)。对应条目
> `ENV-T09 / ENV-T09-unknown-room-modules-20261012` 需由负责人按本交接更新认领/证据行。

## 1. 本包交付的用途与接手第一步

目标：让固定 seed `ENV-T03-SEED-L` 上"打到一半停住"的**真实问号房**通过——按源判定实际房型、
复用已有房间入口、按源事件队列选事件、进入独立事件模块作真实选择/写回/关闭，然后继续地图旅行。

接手先做（不需要读历史报告）：

1. 看第 3 节的"实际命令 / rc / 执行数"，用第 3 节的命令复跑一次固定 seed 轨迹；`rc 3 + step_limit`
   表示"跑到步数上限"，第 46/47 步就是本包交付的行为。
2. 需要改事件内容时读[问号事件接入指南](../../design/mod-the-spire/how-to-add-a-room.md#横向扩展问号事件)，
   接口速查见[编译接口参考](../../design/mod-the-spire/room-compile-reference.md#横向注册地图入口与事件)。
3. 需要复核局部检查时跑 `--unknown-room-only`（第 3 节 4 号命令）。

## 2. 已交付能力（按机制，不是按文件）

### 2.1 问号概率与源池选择（共享模块一）

- `Slice/Rooms/Unknown/UnknownMapPointOdds.cs`：源 `Odds/UnknownMapPointOdds.cs` 的 `Roll` 纯函数 +
  后端分派。初值 0.1f/-1f/0.02f/0.03f、**逐项 float32 累加**（不是 LINQ `Sum` 的 double 累加器）、
  阈值 `roll <= sum`、"命中复位到 base / 未命中按 base 累加"、负赔率不进累加但照样变负。
  赔率状态以 **float32 位型**存在 `Run.Unknown{Monster,Elite,Treasure,Shop}Odds`（隐藏字段，默认值即源初值）。
- `Slice/Rooms/Unknown/UnknownRoomWiring.cs`：声明上述字段与计算资产 `base-spire:UnknownMapPointOddsRoll`
  （后端 `base-spire-source-float32@1`，按 `asset.Entry` 分派，见 `SourceActMapCompute.cs`）；
  提供源黑名单 `BuildRoomTypeBlacklist`（上一间房是 Shop，或当前节点子节点全是 Shop）。
- `Slice/Rooms/Unknown/UnknownRoomEntry.cs`：`MapPointType.Unknown` 的进入处理器。顺序按源：
  **先黑名单 → 再判定 → 再进入**；判定结果写 `Run.ActualRoomType`（源 `MapPointHistoryEntry.RoomType`），
  **地图节点 `Type` 保持 `Unknown`**；判成战斗/精英/宝箱/商店时直接调用
  `MapRoomEntry.Standard(...)` 的**同一批** `Enter`（不复制房型逻辑），判成事件时走生命周期 + 目录分派；
  判出未接入房型显式 `Fail`。

### 2.2 事件访问生命周期（共享模块二）

`Slice/Rooms/Events/EventVisitLifecycle.cs`：`Declare` 拥有事件状态（`Run.CurrentEventId` 公开、
`Run.EventsVisited`/`Run.EventPageGold` 隐藏、`RunVisitedEvents` 集合、事件级随机资产
`base-spire:SourceEventNextInt`）；`Pull` 逐行照抄 `RoomSet.EnsureNextEventIsValid` +
`ActModel.PullNextEvent`（`eventsVisited % Count` → 资格 → 已访问跳过 → `AddVisitedEvent`；
走满一轮按源"允许重复"回到本轮起点）；`MarkVisited` 对应 `RoomSet.MarkVisited(RoomType.Event)`。
队列与已访问集合复用 `RunInitializationWiring` 的 `RunRoomIdentity`/`SourceId`（源里是同一个 `ModelId` 概念），
队列仍是 Start 时用 `up_front` 流洗好的**源池全量身份**，本模块只消费、不重洗、不按可执行模块筛选。

### 2.3 源身份 ↔ 注册 ID 的转换

`Slice/Rooms/Events/EventSourceIdentity.cs`：模块 ID **逐字等于**源事件类名（`Models/Events/<Name>.cs`）。
映射刻意是恒等——`EventRoomCatalog.Enter` 的运行时分派只有 IR 字符串相等这一种原语，任何重命名都会让
分派永远落到"未注册"分支；这里的价值是**建包期校验**：不在 `RunSourcePoolManifest.Events` 里的名字直接抛错。
未实现的源身份不登记、不替换，选中时由目录显式失败。

### 2.4 第一个真实事件：`SunkenStatue`

`Slice/Rooms/Events/SunkenStatueEvent.cs`（源 `Models/Events/SunkenStatue.cs`）：

- 进入时按源 `CalculateVars` 掷一笔**事件级** `Rng.NextInt(-10, 11)`（流 `base-spire:event:SUNKEN_STATUE`，
  种子按源公式 `runSeed + slot + hash(Slugify("SunkenStatue"))`，建局时按源池全量种好），
  金币量 = `GoldVar(111) + 掷点`，写进 `Run.EventPageGold`。
- `SunkenStatueDiveIntoWater`（源 `DIVE_INTO_WATER`）：`Gold += 本页金币量`、`Hp = max(0, Hp-7)`
  （源 `CreatureCmd.Damage(7, Unblockable|Unpowered)`），然后 `context.Close` 恢复旅行。
- `SunkenStatueGrabSword`（源 `GRAB_SWORD`）需要遗物 `SwordOfStone` 的获得与效果（未实现）：
  动作保留源选项身份，选择它**显式失败**并说明原因（不省略、不替代、不伪造离场）。
- 源没有覆盖 `IsAllowed` ⇒ 本模块不实现 `IEventEligibility`（源默认 true）。

### 2.5 共享房间守卫认"实际房型"

- `MapRoomWiring.AtRoomType` + `RoomCompileContext.At`：房型判据 = `Run.ActualRoomType` **或**节点类型。
  营火/宝箱/商店的既有守卫因此自动认问号房判出的房型（这三个文件的实现未改）。
- `BattleRoomCompilation.IsMonsterRoom/IsEliteRoom` 同样改为按实际房型判定——否则问号房判出的战斗
  会去取普通队列却不推进 `NormalEncountersVisited`。**这是本包唯一超出短工作包"可写"清单的改动**
  （两行守卫，理由与营火/商店/宝箱同一条；未改核心分派）。
- `MapRoomEntry.Standard`/`EventRoomCatalog`/`IEventRoomModule` 三份契约**未改动**；
  `docs/examples/base-events` 逐字未改且仍通过。

### 2.6 公开状态与描述

新增公开字段 `Run.ActualRoomType`（源 `RoomType`）、`Run.CurrentEventId`；隐藏字段
`Run.Unknown*Odds`/`EventsVisited`/`EventPageGold`；新增隐藏集合 `RunVisitedEvents`（公开语义里具名不发布，
理由写在 `BasePublicSemantics.CollectionIntents`）。`MapPhase` 上界抬到 `EventPhase = 8`；
`dev-map` 的构建描述/范围说明已同步（`dev` 范围身份逐字节不变）。

## 3. 实际命令、rc 与执行数

独立输出目录：`.artifacts/parallel/dsh-unknown-room-modules-20261012/`（下文简称 `<A>`）。
全部构建都用 `-p:ArtifactsPath=<A>/build` 隔离输出，未写入 `net`/`core` 自己的 `obj`/`bin`。

| # | 命令 | rc | 执行数/结果 | 证据 |
| --- | --- | --- | --- | --- |
| 1 | `dotnet build mod-the-spire/base-spire/BaseSpire/BaseSpire.csproj -p:ArtifactsPath=<A>/build` | 0 | 0 error / 4 warning（既有） | 本节 |
| 2 | `dotnet build net-the-spire/Net.TheSpire.Host/Net.TheSpire.Host.csproj -p:ArtifactsPath=<A>/build` | 0 | 0 error（**net 源码未改**，只按本包 Base 编译固定组合层） | Host SHA256 见 §5 |
| 3 | `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests -p:ArtifactsPath=<A>/build -- --report <A>/report` | 0 | **785 passed / 0 failed**（本包前基线 775；+10 为本包新增），行为报告 53 条 | [slice-full.log](../../../.artifacts/parallel/dsh-unknown-room-modules-20261012/slice-full.log)、`<A>/report/slice-behavior-report.json` |
| 4 | `dotnet run --project …/BaseSpire.Slice.Tests -- --unknown-room-only` | 0 | **10 项局部检查全过** | `<A>/unknown-room-checks.log` |
| 5 | `dotnet run --project docs/examples/base-events -p:ArtifactsPath=<A>/build` | 0 | 两个独立模块例子原样通过（选项隔离/永久写回/关闭/重复 ID 拒绝/未注册显式失败） | `<A>/base-events-example.log` |
| 6 | `python -X utf8 -u <A>/verify_source_vectors.py` | 0 | 独立来源复算（Python 重写 hash/xoshiro/float32 赔率） | `<A>/source-vectors-seed-L.txt` |
| 7 | `python -X utf8 -u <A>/drive_unknown.py --host-dll <A>/build/bin/Net.TheSpire.Host/debug/Net.TheSpire.Host.dll --scope dev-map --seed ENV-T03-SEED-L --output <A>/runs/seed-L-unknown --max-actions 48 --use-potions --shop-room auto --shop-policy buy --shop-buy-kind card --shop-remove` | **3**（`step_limit`，不是故障） | **48 步**；第 46/47 步即本包目标 | `<A>/seed-L-drive.log`、`steps.ndjson`、`<A>/seed-L-unknown-evidence.json` |
| 8 | `dotnet run --project …/BaseSpire.Slice.Tests -- --describe-build` | 0 | dev `2dab0c5b3c3a4034`/`4a08d5bbaf4270be`（未变）；dev-map `e19e4e5b63d0dd01`/`110a5b7867698e0c` | `<A>/describe-build.txt` |

第 7 条命令是"正常公开流程"：它通过**只读包装** `drive_unknown.py` 复用 `net-the-spire/client/run_game.py`
的固定策略（战斗/奖励/宝箱/营火/商店/地图连通性逐字沿用），只在**本包输出目录**里把 `Unknown` 加进
"可走房型"常量、并补一条事件选项分支（`net-the-spire/client/run_game.py` 本身未改）。

### 3.1 固定 seed 的实际轨迹（第 7 条命令）

| 步 | 动作 | 公开事实 |
| ---: | --- | --- |
| 45 | `TakeRewardPotion` | `MapPhase=2`、`ActualRoomType=Monster`、`Hp=67`、`Gold=73`、`CurrentNode=e17` |
| 46 | `ChooseMapPoint`（问号节点 `e21`） | **`ActualRoomType=net:cat:base-spire:RoomType:Event`**、**`CurrentEventId=SunkenStatue`**、`MapPhase=8`、`MapFloor=5`；合法动作多出 `SunkenStatueGrabSword` / `SunkenStatueDiveIntoWater`（源代码选项都在） |
| 47 | `SunkenStatueDiveIntoWater` | `Hp=60`（-7）、`Gold=179`（+106）、`MapPhase=2`（关闭并恢复旅行）、`CurrentEventId=""` |
| 48 | `ChooseMapPoint` | 继续旅行到 floor 6 的商店节点（`ActualRoomType=Shop`），整局未结束（步数上限停止） |

独立复算（第 6 条命令）给出同样的期望值：`unknown_map_point` 第一笔 payload `7790481153947240`
→ `roll=0.8649171590805054` > 0.15 ⇒ Event；事件流第一笔 → `NextInt(-10,11) = -5` ⇒ 金币 `106`。

## 4. 首次边界 / 未验证 / 已知差异（明确登记，不静默）

1. **源 `NumberOfRuns == 0` 的首局固定顺序分支未建模**：源 `UnknownMapPointOdds.Roll` 开头在
   `UnlockState.NumberOfRuns == 0` 时按已访问 Unknown 数直接给 Event/Monster（**不消费随机数**）。
   本工厂固定"全解锁"策略、没有 profile 运行次数，因此一律走加权分支。对 seed L 的第一次判定，
   两条路径的结果都是 Event（且事件身份只取决于队列），但**随机数位置**不同；后续若引入 profile
   运行次数必须从这里接回来。
2. **`Hook.ModifyUnknownMapPointRoomTypes` / `ModifyOddsIncreaseForUnrolledRoomType` 的接管者未接入**：
   DeadlyEvents 修饰符、`GoldenCompass`/`JuzuBracelet`/`LanternKey` 遗物等当前既不在 dev-map 的
   可见遗物集合里、也没有效果实现；本包调用源的**默认实现**（恒等）。这些内容落地时必须从这里接。
3. **未实现源事件的 `IsAllowed` 未建模**：未登记模块的源身份按源默认 `true` 参与资格判定，
   随后在目录分派处报错并指出不支持的对象。也就是说，若某个未实现事件在源里**本应被跳过**，本构建会在它上面失败
   而不是跳过它（失败是显式的，不静默替代）。本包只实现了 `SunkenStatue`（源无覆盖）。
4. **事件级随机流的"再次开始"语义**：源每次 `BeginEvent` 新建 `Rng`（计数器 0），同一事件在同一局
   再次开始时得到同一串随机数；引擎流有计数器，第二次访问会前进。当前幕内不可能重复访问源身份
   （31 个事件在一个幕里走不完一轮），因此本条不可达。
5. **`SunkenStatueDiveIntoWater` 的脱战死亡路径未接入**：生命截断到 0，源在 0 之后走死亡/终局；
   本流程生命充裕（67 → 60）未触发，因此整局终局仍未验证。
6. **`SunkenStatueGrabSword` 未实现**：需要 `SwordOfStone` 的获得与效果（遗物效果族）。选择它会
   Fault 整个会话（显式）。这是**内容依赖受阻**，不是本机制缺口；最小后续形状是"遗物获得 + 该效果"
   两项之一落地后再放开该选项。
7. **精英内容依赖**：seed L 的精英队列首槽 `base-spire:byrdonis-elite` 的敌人 `base-spire:byrdonis`
   尚未加载 ⇒ 走问号房判成精英时报错并指出不支持的对象（与直接走精英节点同样的既有内容边界）。本包用失败消息
   点名精英队列遭遇来判别"没有退回普通队列"。
8. **地图驱动器的 `Elite` 边界**：`net-the-spire/client/run_game.py` 的 `IMPLEMENTED_MAP_TYPES`
   不含 `Elite`，因此第 7 条命令在 48 步后继续时会停在精英节点（`unsupported_map_room`）。这与本包
   无关（C 线文件），包装脚本只加了 `Unknown`。
9. **未验证项**：`dotnet build AutoTheSpire.sln`、`core-the-spire/Core.TheSpire.Engine.Tests`、
   `Net.TheSpire.Tests`、`tools/differential` 严格对拍、B/C 固定闭包、整局/多幕、遗物/Eternal 内容。
   本包只跑上面 §3 列出的命令。
10. **身份迁移**：`dev` 的 ruleset/build 未变；`dev-map` 因新增房型/事件字段、集合、两条计算资产、
   两个事件动作与相位上界而**必须**变化（新值见 §3 第 8 条，`SliceMapRoomChecks` 的常量已按同一
   迁移惯例更新并留下旧值）。这不是回归，但所有绑定旧 `dev-map` 身份的固定证据（例如
   `docs/review/tofix/dsh-shop-public-contract-handoff-2026-10-12.md` 里的 `F8FACE60…` 闭包）
   需要负责人按新构建重新结算。
11. **判别归属（避免误读覆盖）**："两个独立事件模块的选项隔离/永久写回/关闭"由**已交付的受控例子**
    （`docs/examples/base-events` 的 `GoldEvent`/`HealEvent`）承担，本包已原样复跑通过（§3 第 5 条），
   没有为它另写一份重复检查；本包只实现**一个**真实事件（`SunkenStatue`），seed L 队列里的下一条
   真实事件 `UnrestSite` 仍属内容依赖，留给下一位按 §7 的规则新增文件。

## 5. 构建与复放坐标

- 本包 Base 程序集（Host 运行目录里的同一份）：`<A>/build/bin/Net.TheSpire.Host/debug/BaseSpire.dll`
  SHA256 `54B13FF8DD2C2152270D9A80EC79D1F712CF000C148269AA55CC2A2CD20793C7`
- 本包 Host 程序集：`<A>/build/bin/Net.TheSpire.Host/debug/Net.TheSpire.Host.dll`
  SHA256 `5260362612C30DA104427F4056B5FC3872FD4BE5E191EF4F2584481BFD931161`
  （§3 第 7 条轨迹就是用这两份文件跑出来的；dev-map 规则身份 `e19e4e5b63d0dd01` / 构建身份
  `110a5b7867698e0c` 在交付前的最后一次整理里**逐字未变**，同一轨迹复跑两次结果一致）
- 该 Host 是**用本包 Base 编译固定 net 组合层**的结果（net 源码零改动、输出全部在 `<A>/build`），
  与 B/C 冻结输入（`.artifacts/parallel/owner-project-review-20261012/bin/...`，Host SHA256 `F8FACE60…`）
  **不是同一份闭包**：比较两者时必须先说明身份差异，不能把本包的公开轨迹当成冻结闭包的证据。
- 轨迹期望（可直接复放）：seed `ENV-T03-SEED-L`、scope `dev-map`、profile
  `base-spire-singleplayer-dev-map-v1`；第 46 步 `MapPhase 2→8` 且 `CurrentEventId=SunkenStatue`，
  第 47 步 `Hp -7`、`Gold +106`、`MapPhase →2`。
- 公开身份（第 7 条命令实测）：`net:ruleset:f272a05b5a2bb01b`、`net:catalog:fa6f27e2ab03d2ed`、
  `net:backend:simulation-0.2-3727c85de0833514`、protocol `0.2`。

## 6. 改动文件清单

新增（本包生产代码）：

- `mod-the-spire/base-spire/BaseSpire/Slice/Rooms/Unknown/UnknownMapPointOdds.cs`
- `mod-the-spire/base-spire/BaseSpire/Slice/Rooms/Unknown/UnknownRoomWiring.cs`
- `mod-the-spire/base-spire/BaseSpire/Slice/Rooms/Unknown/UnknownRoomEntry.cs`
- `mod-the-spire/base-spire/BaseSpire/Slice/Rooms/Events/EventVisitLifecycle.cs`
- `mod-the-spire/base-spire/BaseSpire/Slice/Rooms/Events/EventRandom.cs`
- `mod-the-spire/base-spire/BaseSpire/Slice/Rooms/Events/EventSourceIdentity.cs`
- `mod-the-spire/base-spire/BaseSpire/Slice/Rooms/Events/EventModules.cs`
- `mod-the-spire/base-spire/BaseSpire/Slice/Rooms/Events/SunkenStatueEvent.cs`
- `mod-the-spire/base-spire/BaseSpire.Slice.Tests/UnknownRoomChecks.cs`

修改（仅本包接口）：

- `MapRoomWiring.cs`（`RoomType` 枚举、`Run.ActualRoomType`、`EventPhase` 上界、`AtRoomType`、
  进入前写实际房型；核心分派未改）
- `RoomCompileContext.cs`（`At` 改按实际房型）
- `BattleRoomCompilation.cs`（`IsMonsterRoom`/`IsEliteRoom` 两行守卫改按实际房型）
- `Compilation/RoomCompilation.cs`、`Compilation/CompilationBindings.cs`（组合问号房/事件三件套）
- `RunInitializationWiring.cs`（暴露 `RoomIdentity`/`RoomId` 两个符号）
- `VanillaRandomLayout.cs`（`Slugify`、事件级流名/种子）
- `SinglePlayer/BaseSpireRuntimeFactory.cs`（建局种事件级流；dev-map 描述与相位表）
- `SinglePlayer/BasePublicSemantics.cs`（`RunVisitedEvents` 具名不发布）
- `SinglePlayer/RunConfiguration.cs`（`DevMap` 范围说明）
- `Slice/SourceActMapCompute.cs`（计算后端多分派两条纯计算条目）
- `BaseSpire.Slice.Tests/SliceMapRoomChecks.cs`（dev-map 身份迁移常量、种子流 12+3+1+31）
- `BaseSpire.Slice.Tests/Program.cs`（`--unknown-room-only` 入口）
- `docs/design/mod-the-spire/how-to-add-a-room.md`、`docs/design/mod-the-spire/room-compile-reference.md`

未改：`core-the-spire/**`、`net-the-spire/**`、`tools/**`、`reference/**`、`sts2/**`、
`MapRoomEntry.cs`、`EventRoomCatalog.cs`、`RestSiteWiring.cs`、`TreasureRoomWiring.cs`、
`ShopRoomWiring.cs`、`docs/examples/base-events/**`。

## 7. 下一步（都是负责人排期，不是本包自派单）

1. 负责人复核本交接与 §3 证据，按新 `dev-map` 身份结算受影响的固定闭包证据，更新主清单
   `ENV-T09 / ENV-T09-unknown-room-modules-20261012` 行。
2. 继续真实流程时，下一个 Unknown 会取队列下一条（seed L：`UnrestSite`，未实现）⇒ 需要新的事件模块；
   按 §2.3 的规则只加文件 + `EventModules.Create` 一项。
3. 内容依赖（`SwordOfStone`/遗物效果族、`byrdonis` 精英、事件战斗返回、幕转换 T10）按主清单既有顺序接续。
