---
task_id: ENV-T04
dispatch_id: ENV-T04-map-differential-20261008
session_id: session-542ab2c1-4079-47c1-9011-2b53fee16ca0
delivery_status: blocked
updated_at: 2026-10-08T23:08:00+08:00
dispatch_record: .audit/dsh-env-t04-map-differential-2026-10-08/task-record.json
---

# ENV-T04 B线真实工厂地图生成独立对拍交接 — 2026-10-08

本包为严格入口 `test --suite regression` 增加两个**真实地图生成**机制 case（`map-generated-alpha` /
`map-generated-beta`）：base 侧由唯一 `BaseSpireRuntimeFactory` 的 `DevMap` 正常 `Start` 后取引擎状态/稳定投影，
参考侧由 `reference/sts2-sim` 的独立地图实现与独立随机服务真正生成，逐节点/边/流比较。原 15 必测 ID、
`required` 与失败分类全部保留，新 case 不替代任何原 unsupported / `rng-initial-shuffle`，不关闭 M01。

- 归属：ENV-T04「严格regression接唯一工厂和独立参考」，B 线（`tools/differential/`）。
- 接手入口：主清单 [ENV-T04](../../tofix/README.md#env-t04)；[B 完整交付合同](../../../design/post-core-agent-assignments.md#4-bdiff-0102日常严格对拍)；[工具 README](../../../../tools/differential/README.md)。
- 交付范围：本会话是 B 线本包唯一生产执行者；只写 `tools/differential/`、必要 tools 文档、本交接与
  `.audit/dsh-env-t04-map-differential-2026-10-08/`。base / net / core / sts2 / reference / solution / 主清单只读。
- 派单记录：[.audit/dsh-env-t04-map-differential-2026-10-08/task-record.json](../../../../.audit/dsh-env-t04-map-differential-2026-10-08/task-record.json)。
- 前包：[严格 regression 交接](dsh-env-t04-handoff-2026-10-08.md)、
  [门槛覆盖 self_checked 交接](dsh-env-t04-gates-handoff-2026-10-08.md)、
  [本包直接前驱：源初始化 manifest 交接](dsh-env-t04-source-init-manifest-handoff-2026-10-08.md)。
- 同轮并行线：[C 地图 Host 交接](dsh-env-t03-map-host-handoff-2026-10-08.md)、
  [A 连续攻击修复交接](dsh-env-t05-consecutive-attack-handoff-2026-10-08.md)。

**当前状态（blocked）**：共享顺序为 C 地图 Host → A 连续攻击修复 → B 地图独立对拍。派单时刻实测 C/A 会话文件
仍在秒级写入，因此参与编译的 `tools/differential/*.cs` 与 `suites/*.json` 本包**一字未改**，也未做任何共享
`obj`/`bin` 构建。本阶段只做不参与编译的源审阅、audit 设计、fixture 数据与草稿准备。

**门状态实测（2026-10-08 22:17–23:03，每 30s 采样，monitor job `pwsh-807`）**：

- C `session-3baaf675-9539-441a-b208-7ee00280721c`：22:17–23:02 全程活跃写入；**23:02:54 停止写入，交接
  `delivery_status=ready_for_review`**（monitor 报 `STOPPED-WRITING C-map-host` 与 `ALL-PEERS-STOPPED`）。
- A `session-20bb6663-e012-4391-bffb-64516cc5a5df`：**22:38:09 起不再写入**（age 163.8s → 1484.4s 单调上升），
  交接 [`dsh-env-t05-consecutive-attack-handoff-2026-10-08.md`](dsh-env-t05-consecutive-attack-handoff-2026-10-08.md)
  已存在但仍 `delivery_status=in_progress`。
- 直读 A 会话状态文件 `C:\Users\26877\.dsh\storages\session_projcache\sessions\session-20bb6663-e012-4391-bffb-64516cc5a5df.json`
  （末次写入 22:38:09）：A 的最后一组事件是 `read`/`grep`（seq 507–509），补丁只落在**不参与编译**的
  `.audit/dsh-env-t05-consecutive-attack-2026-10-08/patch/ConsecutiveAttackChecks.cs`，**base 生产与测试文件均未改动、未构建**
  → A 的包未完成，处于"已准备、等 C 停止"的暂停态。

→ 结论：**C 已停止，但 A 的 base 修复尚未实际落盘**。按 C → A → B 顺序，以及派单里"A 修改 Power 纯查询可能改变
build 身份，所以参与编译 tools 修改/共享构建及最终 case 等 C/A 实际停止后进行"的要求，本包此刻不能改参与编译的
`tools/differential/*.cs`、也不能做共享构建：新 case 记录的 `build_id`/会话身份会随 A 的改动立刻失效，且会与 A
的构建争用同一 `obj`/`bin`。因此 `delivery_status=blocked`，阻塞条件 = **A 的 base 修复尚未实际落盘**（这不是
"前置未验收"，是共享构建的身份依赖）。

**已完成（不参与编译的阶段成果）**

- 源审阅（两份只读核对结论，行号见 audit 设计稿）：
  - base 侧地图物化与公开投影**已存在于工作区**（未提交）：`BaseSpireRuntimeFactory.ActMapEnabled => ScopeId == BaseSpireScope.DevMap`、
    建局时 `VanillaRandomLayout.ActMapSeed(runSeed, 1)` 补种 `base-spire:run:act_1_map`；
    `MapRoomWiring` 每轮抽一个 53 位 payload 交纯计算重放（`SourceActMapCompute.Span = 1<<53`、`WordBudget = 512`、`ActRooms = 15`），
    物化成公开集合 `base-spire:MapNodes` + 关系 `base-spire:MapChildren`。
  - 参考侧 `StandardActMap(Rng mapRng, int numberOfRooms, MapPointTypeCounts, bool hasSecondBoss = false, bool enablePruning = true)`、
    `Overgrowth.BaseNumberOfRooms => 15`、`GetMapPointTypes` 两次高斯（rest 先、unknown 后）、
    `MapPointTypeCounts.StandardRandomUnknownCount => NextGaussianInt(12, 1, 10, 14)`、`Rng.ToSerializable()` 可取 4 个状态字。
- 设计稿：[.audit/dsh-env-t04-map-differential-2026-10-08/map-case-design.md](../../../../.audit/dsh-env-t04-map-differential-2026-10-08/map-case-design.md)
  （判定语义、双向源定位表、fixture/字段/事实路径/流单位/参考身份方案、待落盘清单、未决风险）。
- 关键设计取舍（落盘前的决定，理由保留在设计稿）：
  - 新 `kind=map` + fixture 新字段 `base_scope`（默认 `dev`，本 case 用 `dev-map`），不混入既有 `run` 路径。
  - 事实只取地图机制：`map.count/rows/cols/node.[c,r].type/edge.[pc,pr]->[cc,cr]/start/boss/second_boss/words_consumed`，
    base 侧**只读引擎公开投影**（`MapNodes`/`MapChildren`/流诊断），不用 `SourceActMapCompute` 反推自证。
  - streams 只投影地图流一条；单位用**底层 64 位字数**（参考侧用工具自己的 `Xoshiro256StarStar` 渐进状态对齐求得，
    上界 512；对不上则记未对齐并如实报差异），指纹两侧同一公式。
  - 参考身份复用 `RngSourceCompat.ProfileId`，并登记负责人本轮冻结的 7 份源文件摘要
    （`.audit/dsh-supervisor/review-2026-10-08-2153/map-source-inputs.json`：`sts2/` 的 `StandardActMap.cs`、
    `MapPointTypeCounts.cs`、`Overgrowth.cs`、`Rng.cs`、`RunRngSet.cs` + `reference/sts2-sim/` 的 `StandardActMap.cs`、`Rng.cs`）。

## 接手后先做什么

1. 先读本节与「验证与证据」，确认本包**已交付部分**（避免重做）与仍需 A 的 base 修复落盘后才能做的编译步骤。
2. 在项目根目录运行 `powershell -NoProfile -ExecutionPolicy Bypass -File tools\differential\test-env.ps1 -Suite regression`
   （本机 `pwsh` 可用——本包监视脚本就是它跑的），读取实际 rc 与逐 case verdict 分布。
3. 若 `map-generated-alpha` / `map-generated-beta` 已注册但 base 侧或参考侧未接通，按「差异、阻塞与未验证」
   给出的具体剩余缺口继续；不要用补抽/换 seed/换敌人/删分母制造 passed，也不要关闭 T04 父项。

## 已交付与接口变化

**本阶段没有任何生产文件变更**：`tools/differential/` 下的 `.cs` 与 `suites/*.json` 一字未动（编译门未开），
所以严格入口的 case 数仍是原 15，接口没有变化。

只有**不参与编译**的准备件（可原样复制落盘，见
[.audit/dsh-env-t04-map-differential-2026-10-08/draft/](../../../../.audit/dsh-env-t04-map-differential-2026-10-08/draft)）：

- `draft/MapFacts.cs`——两侧共用的地图事实路径词表（只做格式化，不推导、不代算边）。
- `draft/MapSnapshot.cs`——base 侧只读引擎公开投影 `base-spire:MapNodes` / `base-spire:MapChildren`，
  流只保留 `base-spire:run:act_1_map`；**不**用 `SourceActMapCompute` 反推网格自证。
- `draft/ReferenceMapEntry.cs`——`#if REFERENCE_SIM` 参考侧独立生成入口（源派生种子 + 参考自己的
  `Rng`/`MegaRandom`/`StandardActMap`/剪枝/后处理）。
- `draft/patch-plan.md`——`RngSourceCompat` / `FixtureSuite` / `BaseBackend` / `ReferenceBackend` /
  `suites/regression.json` 的逐处改动、精确锚点、落盘后验证清单；以及设计稿与测试脚本中提到的其余文件定位。

## 验证与证据

本阶段唯一可验的是**门状态**（没有任何构建与运行，因此没有命令返回码）：

| 时间（+08:00） | 实测 | 结论 |
| --- | --- | --- |
| 22:17–23:02 | monitor 后台 job `pwsh-807`（`monitor-peers.ps1`，每 30s）；C 会话文件 age 在 0~100s 循环 | C 一直在活跃工作 |
| 22:38:09 | A 会话文件最后一次写入 | A 停止（age 单调升到 1484.4s） |
| 23:02:54 | monitor 报 `STOPPED-WRITING C-map-host age=3.4s handoff=ready_for_review` 与 `ALL-PEERS-STOPPED` | **C 已实际停止**，且已交包 |
| 23:03 前后 | 直读 A 会话状态文件：末组事件为 seq 507–509 的 `read`/`grep`；补丁仅在 `.audit/.../patch/` | A 未落盘、未构建，包未完成 |
| 23:0x | `Get-Process dotnet,MSBuild,VBCSCompiler` 有 7 个 `dotnet` 进程（启动于 22:40:43/22:40:45/22:55:25） | 疑似构建节点常驻；正是"不与 A 争用同一 `obj`/`bin`"的理由 |

命令与产物：`monitor-peers.ps1` 输出（job `pwsh-807`，退出码 0）为本节主要原始记录；未运行 `dotnet build`、
`test-env.ps1`、`dotnet run --project tools/differential`，故无 run_id / DLL 摘要 / seed 运行记录。

## 差异、阻塞与未验证

**阻塞（唯一条件）**：**A（`ENV-T05` 连续攻击修复）的 base 修复尚未实际落盘**。判据：A 会话自 22:38:09 停止
写入（此后约 27 分钟内无新事件，`age` 单调升到 1484.4s），其交接仍 `delivery_status=in_progress`，且直读 A 会话状态
文件后确认它的补丁只存在于**不参与编译**的 `.audit/dsh-env-t05-consecutive-attack-2026-10-08/patch/ConsecutiveAttackChecks.cs`，
base 生产/测试文件与共享 `obj`/`bin` 均未被它改动。C 已于 23:02:54 实际停止并交包（`ready_for_review`）。

→ B 需要的是**A 完成并停止**（不是"A 未验收"）：A 的改动落在 `mod-the-spire/base-spire/`，而 `Differential.csproj`
直接引用该 base 项目，A 一旦落盘就会改 `build_id`/会话身份并复用同一 `obj`/`bin`，所以 B 现在跑出的
case 记录会立刻失效，也可能与 A 的构建相互锁文件。派单已写明这一点，故此处**不抢跑**。

**接手第一步**：等 A 应用补丁并构建完成后（A 交接翻到 `ready_for_review`/`blocked`，或 A 会话再次静止），把
`draft/` 三个 `.cs` 原样复制进 `tools/differential/`，按 `draft/patch-plan.md` 应用 5 处改动，**串行**构建
`tools/differential/Differential.csproj`（先确认没有其它构建在跑），再跑严格 suite 并把原 15 条与新 2 条
（`map-generated-alpha` / `map-generated-beta`，seed `t05-map-alpha` / `t05-map-beta`）的逐条 verdict 落回本文件。

**未验证（全部编译与运行步骤）**：新 case 未注册、未构建、两侧未跑、**首差未知**。因此本包
**没有任何**"双侧通过"结论；原 15 条 ID/required/失败分类未动，`rng-initial-shuffle` 仍为 `diverged`，
`map-generated-*` 不替代任何原 unsupported case。T04 / 父项 / M01 **不关闭**。

**已知风险（未验证，可能在这里首次暴露为首差）**：

- 抽数单位：参考 `Rng.Counter` 是 API 调用计数，base 是 53 位 payload 计数。本包用工具自己的
  `Xoshiro256StarStar` 从种子渐进到参考 `mapRng.ToSerializable()` 的 4 个状态字，按**底层字数**对齐
  （上界 `SourceActMapCompute.WordBudget = 512`），指纹两侧同一公式；对不上则具名抛错而不是猜数。
- 若参考的 `NextFloat`/高斯语义、房型计数抽样或其他版本差与只读源不一致，会表现为地图网格或地图流首差：
  如实报 `diverged`/`blocked` 并给六字段候选，**不扩兼容 Profile、不改参考算法、不删分母**。
- base 侧边界**Actions 传空表**：参考侧没有 `ChooseMapPoint` 的对应物，放进来只会造出与本 case 主张
  无关的 `present` 差异；base 侧选择房的合法性与结算后相位机由 `SliceMapRoomChecks.cs` 自身证据覆盖，
  不在本 case 的比较范围内。
