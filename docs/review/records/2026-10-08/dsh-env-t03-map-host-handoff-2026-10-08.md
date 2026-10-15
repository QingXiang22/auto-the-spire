---
task_id: ENV-T03
dispatch_id: ENV-T03-map-host-20261008
session_id: session-3baaf675-9539-441a-b208-7ee00280721c
dispatch_record: .audit/dsh-env-t03-map-host-2026-10-08/task-record.json
delivery_status: ready_for_review
updated_at: 2026-10-08T23:01:22+08:00
---

# ENV-T03 C 线（地图公开 Host 完整接线）交接 — 2026-10-08

> **ready_for_review**：本包实现与验证已完成，等待验收/负责人裁定。**不等于 T03 验收**（T03 仍开放，EncounterPool/Queue 与连续攻击故障归 A）。结构按 `$auto-the-spire-handoff` 组织。

一句话：让正式宿主用**唯一共同工厂**、按**显式范围**创建一个未 Start 的独立 Session，走 `base` 的真实地图公开路径——公开目录/快照里能直接读到地图实体/字段/有序节点/父子关系/`ChooseMapPoint` 候选，合法选 Monster 节点进入既有正常战斗；这些事实**全部由引擎声明派生**，net 侧没有新增逐项登记；`dev` 缺省身份逐字不变。

归属：ENV-T03「公开 Host 接唯一工厂；从规则声明派生公开出口」的一个完整机制子包（C 线）。
接手入口：[主清单 ENV-T03 行](../../tofix/README.md#env-t03)、[设计 §7](../../../design/net-the-spire/design.md#7-2026-10-07公开语义透传的源码核对与接续)、[范围使用参考](../../../../net-the-spire/formal-dev-scopes.md)、[A 地图交接](dsh-env-t05-map-room-handoff-2026-10-08.md)。
交付范围：只写 `net-the-spire/`、本包 `.audit/dsh-env-t03-map-host-2026-10-08/` 与本文；`base/core/tools/reference/solution/主清单` 只读（本包未改它们，未改依赖输入）。
证据目录：[.audit/dsh-env-t03-map-host-2026-10-08/](../../../../.audit/dsh-env-t03-map-host-2026-10-08)。

## 接手后先做什么

1. 先只认[主清单 ENV-T03 行](../../tofix/README.md#env-t03)的当前状态与认领（本文件不写状态）。**不要重做**本包已交付的接线与两组检查。
2. 复现三条最小命令（见"已验证"表第 1～3 行）：根构建 → C# 侧真实检查（应 `415 executed / 415 passed`）→ Python 侧真实子进程对照（应 `182 passed`）。两条 Python 旧套件（103 / 157）用于确认没有回归。
3. 读[范围使用参考](../../../../net-the-spire/formal-dev-scopes.md)确认 `--scope dev|dev-map` 的接口与两个范围的公开身份；**要新范围就改 base 的 `RunConfiguration`，不要在 net 侧加映射表**。
4. 下一子包方向（**不要由 net 侧补**）：非 Monster 房型现在被 base 具名 `Fail`（会话 `Faulted`，位置见下"未验证"），要等 A 的房型实现；`EncounterPool`/`EncounterQueue` 公开摘要是有主责的开放问题，不要用"少发布"或 net 侧补造摘要代替。
5. 攻击首差（`base-spire:PowerStacksOf`）按"首差与最小复现"一节核实后交 A；**不要**在 net 侧吞异常、删候选或换 seed 变绿。

## 已交付（实际文件、接口与语义）

**范围选择入口（net 侧只此一处）**
- `net-the-spire/Net.TheSpire.Host/FormalDevScenario.cs`：`SelectableScopes`（`("dev", BaseSpireScope.Dev)`、`("dev-map", BaseSpireScope.DevMap)`）、`DefaultScopeName = "dev"`、`ScopeIdForName(name)`（未知名字返回 `null`）、工厂按 `new RunConfiguration.Ironclad(0, scopeId)` 建配置 ⇒ 仍由唯一 `BaseSpireRuntimeFactory` 配置未 Start 的 Session。
- `net-the-spire/Net.TheSpire.Host/Program.cs`：`--scope <name>` 只在 `--scenario formal-dev` 下解析；未知名字 rc=2 并在 stderr 列出可选值；省略即 `dev`。`--game-seed` 仍是字符串 seed，折算后**不进身份**。
- 公开身份键只有 `[backend_profile_id, catalog_id, protocol_version, ruleset_id]`（结构上无 seed 位）；范围连 backend profile 摘要一起进身份。

**地图公开事实（零逐项登记）**
- `MapNode`（实体）、`MapNodes`（`Run` 拥有的有序集合）、`MapChildren`（`MapNode → MapNode` 多值关系）、`ChooseMapPoint`（动作，唯一必需参数 `Node` = 到 `MapNode` 的实体引用）在公开目录里出现，**形状全部由 `DerivedPublicProfile` 从引擎声明 + base 公开语义派生**：字段形状来自声明（`number`/`category`/`boolean`/实体引用）；枚举成员按 `net:cat:<枚举限定名>:<成员名>`；关系因引擎 `RelationSymbol.Public` 默认真而自动发布（[DerivedPublicProfile.cs:404-445](../../../../net-the-spire/Net.TheSpire/Projection/DerivedPublicProfile.cs)）；集合 owner/元素来自 base 集合意图。
- 候选只来自引擎稳定边界的公开 `legal_actions`；不含隐藏状态、RNG 或 debug 轨迹。net 侧无地图登记表可改（新增地图声明只改规则包）。

**检查（两组互补：进程内 vs 真实子进程）**
- `net-the-spire/Net.TheSpire.Tests/FormalPathChecks.cs`：新增 G-01 范围选择入口 / G-02 目录派生声明 / G-03 开局公开事实与候选恰为公开后继 / G-04 两 seed 合法选点进战斗 + 幂等与非法/过期拒绝 / G-05 同批载荷过 0.2 wire schema（出牌要么正常结算、要么显式 base 故障）。
- `net-the-spire/client/test_map_host.py`（新，182 检查，分组 `host/catalog/trajectory/rejections/schema`）：驱动**真实宿主进程** NDJSON stdio，用真实客户端逐帧过 `docs/contracts/net/0.2/wire.schema.json`，并把每帧原始请求/响应写 NDJSON 证据。
- `net-the-spire/formal-dev-scopes.md`（新）：本路径的使用参考（命令行、身份表、派生说明、已知边界）。
- 契约/schema **未改**：本路径复用 0.2 既有形状（实体种类/字段/关系/有序集合/动作），`docs/contracts/net/0.2/wire.schema.json` 与本包交付前的字节一致；无版本化变更，故旧 0.1/旧 0.2 载荷保持有效。

## 已验证（命令 / 工作目录 / 返回码 / 证据范围）

| # | 命令 | 工作目录 | 返回码 | 证据范围 |
| --- | --- | --- | --- | --- |
| 1 | `dotnet build AutoTheSpire.sln --nologo` | 仓库根 | 0（0 警告 0 错误） | `.audit/dsh-env-t03-map-host-2026-10-08/root-build-2026-10-08.txt` |
| 2 | `dotnet run --no-build --project net-the-spire/Net.TheSpire.Tests` | 仓库根 | 0（**415 executed / 415 passed / 0 failed**，原 410 + G 5） | `.audit/.../net-tests-g-2026-10-08.txt`（G-01…G-05 全 ok） |
| 3 | `python -X utf8 -u net-the-spire/client/test_map_host.py --log .audit/.../python-map-host.ndjson` | 仓库根 | 0（**182 passed / 0 failed**；host 18 / catalog 49 / trajectory 62 / rejections 34 / schema 19） | `.audit/.../python-map-host.txt` + `.ndjson`；提炼跑 `summarize_evidence.py` |
| 4 | `python -X utf8 net-the-spire/client/test_client.py` | 仓库根 | 0（**103 passed**，旧 0.1 与 formal 兼容未回归） | 控制台（业务输出） |
| 5 | `python -X utf8 -u net-the-spire/client/test_declaration_extension.py` | 仓库根 | 0（**157 passed**，上一子包未回归） | 控制台（业务输出） |
| 6 | `python tools/doc-check/check_docs.py` | 仓库根 | **2**（仅 2 条，均在 B 的 `docs/review/tofix/dsh-env-t04-map-differential-handoff-2026-10-08.md:22` 的失效相对链接；本包文件 0 问题，边界外未改） | 控制台（业务输出） |
| 7 | `git diff --check` | 仓库根 | 0 | 控制台 |
| 8 | `python -X utf8 .audit/.../probe_map.py <seed> --json recon-<seed>.jsonl` | 仓库根 | 0（两 seed） | `.audit/.../recon-alpha.txt|.jsonl`、`recon-beta.txt|.jsonl` |

**同构建输入证据复用**：本包未改 base/core/tools 与任何依赖输入，故 Engine 95/95 与 Slice 339/339 沿用负责人对相同 base 输入的既有执行证据，未重复跑；`docs/contracts/net/0.2/` 未改，故协议载荷证据沿用上一子包（见 [C 声明扩展交接](dsh-env-t03-declaration-extension-handoff-2026-10-08.md) 与 [C 序列交接](dsh-env-t03-host-sequences-handoff-2026-10-08.md)）。

**关键实测事实（可核对）**
- 身份：`dev` = `ruleset:4206675da0ca12f6` / `catalog:3440d8467b85ae03` / `backend:simulation-0.2-ca6767486c903ad4` / `build:52b285cbc40711cc`（**与上一子包逐字相同 ⇒ 缺省范围身份不变**）；`dev-map` = `ruleset:508a1c95ddb54d1d` / `catalog:6f998eb6e876bea0` / `backend:simulation-0.2-99f5c5a6d155077c` / `build:29abce0b690a3d22`。两范围 `protocol_version` 都是 `0.2`。
- 目录（dev-map）：`entity_kinds 13 / content_types 129 / fields 56 / relation_types 3 / sequence_types 7 / action_types 4 / pile_types 5 / group_types 0 / terminal_reasons 2`；`MapNodes` owner `[Run]`、元素 `[MapNode]`；`MapChildren` 目标 `[MapNode]`、来源为全部 13 个公开种类；`ChooseMapPoint` 必需参数恰为 `net:field:base-spire:ChooseMapPoint.Node`（`entity_ref`、目标 `MapNode`、`required=true`、`visibility=public`）；`MapNode.Type` 的类别含 9 个 `MapPointType` 成员。
- 轨迹：`t05-map-alpha` 开局 obs 1147、`MapNodes` 51、`MapPhase 0`、`MapFloor 0`、`CurrentNode e34`、初始遭遇 `base-spire:slimes-weak`，合法动作同时含 `ChooseMapPoint`/`PlayCard`/`EndTurn`；3 个候选全是 Public 后继（row 1、Monster）。选 `e30` → obs 1351、`MapPhase 1`、`MapFloor 2`、`Visited=true`、遭遇 `base-spire:nibbits-weak`，合法动作只剩 `PlayCard`/`EndTurn`（**无 ChooseMapPoint**）；再出一张牌 ok → obs 1359。
- 轨迹：`t05-map-beta` 开局 obs 1174、61 节点、`CurrentNode e50`、遭遇 `base-spire:nibbits-weak`，4 个候选全是 Public 后继（Monster）；选 `e21` → obs 1376、`MapPhase 1`、`MapFloor 2`、遭遇 `base-spire:fuzzy-wurm-crawler-weak`；出牌 ok → obs 1380。
- 拒绝（执行前、状态与抽数不变）：错 identity（改 `catalog_id`）→ `version_mismatch`，`execution_state=not_started`、`retry_mode=stop`，且**没有创建 session**；实体引用当候选 → `illegal_candidate`（`not_started`/`correct_input`）；已消费候选重发 → `illegal_candidate`；合法候选配过期 observation → `stale_observation`（`not_started`/`query_request`）。拒绝后 `MapPhase`/`CurrentNode`/`MapDraws`/`MapNodeCount` 与观察号逐项不变。
- 幂等：同一 `request_id` 重发同一候选 → `ok`，同一观察号且响应**规范字节相同**。
- 战斗入口：合法选择 Monster 后进入的是既有正常战斗（`legal_actions` 是原本的 `PlayCard`/`EndTurn`，遭遇 id 来自地图节点），不是新造的战斗路径。

## 首差与最小复现（交 A，本包不修 base）

- 现象：连续攻击时公开动作查询触发引擎故障；响应 `{"code":"backend_fault","execution_state":"unknown","retry_mode":"stop"}`，消息 `SessionStateInvalid: pure procedure 'base-spire:PowerStacksOf' did not complete; pure procedures cannot suspend`。
- 最小复现（真实子进程，rc0）：`python -X utf8 .audit/dsh-env-t03-map-host-2026-10-08/probe_map.py t05-map-alpha --json recon-alpha.jsonl > recon-alpha.txt 2>&1`。
- 动作前缀（seed `t05-map-alpha`）：`start`(obs 1147) → `ChooseMapPoint` 选 Monster `e30`(obs 1351) → `PlayCard` defend `e78`(obs 1359) → `PlayCard` defend `e79`(obs 1367) → `PlayCard` **strike** `e80`（候选 `c-72a3fe42011dac87`）→ 故障。
- **最后一致边界** obs **1367**（`status=decision`、`MapPhase=1`）；**首差**就是那一步 strike。`beta` 同形（`strike` 于 obs 1550 后失败，`e90`）。
- 进程内栈（上一子包已给）：`Session.InvokePure` → `Scheduler.LegalActions` → `ControlSession.Snapshot()` → `ControlHost.Step`；base 声明位置 `mod-the-spire/base-spire/BaseSpire/Slice/CombatSlicePackage.cs:281-295`（`Procedure("PowerStacksOf")` 的纯过程体）。
- 本轮 C#/Python 自动检查里，地图进战斗后的第一张可出牌是 `defend`，所以**正常结算、没触发故障**（两组检查都打印/断言了"要么正常结算、要么显式 base 故障"这条口径），故障只在攻击牌上稳定复现——不要因为检查全绿就认为攻击路径已验证。

## 未验证 / 开放（如实列出，不要当成通过）

- **地图完整可玩**（奖励/选牌/长期写回/下一幕）未验证：本包只到"进入既有战斗并正常结算"；剩余由 T05 后续子包与验收负责。
- **非 Monster 房型**：base 目前具名 `Fail`（"房间类型 X 尚未实现（地图节点保留、不伪装空事件）"）⇒ 会话 `SessionStatus.Faulted`，此后 `legal_actions` 为空。本包在公开路径上**如实呈现**该状态，未伪装 unsupported 可继续、未改 base。
- **连续攻击故障**（`PowerStacksOf`）未修：归 A。C 结束时本包已提供可复现的最小前缀与最后一致边界，A 可用本包正式 Host 路径重现（见上节）。
- **`EncounterPool`/`EncounterQueue`**：公开目录仍是 `NotPublished`（跳过原因 `base-spire:N-02-BLOCKED-CONTENT-POOL` / `...-INTERNAL-QUEUE`），而引擎投影对集合成员仍有默认 `InstanceId/size/capacity` 可见性——这一窄缺口**不由本包裁定**，也未降低 base 可见性或公开未来遭遇/补造摘要；具体最小复现见 A 包证据 `.audit/dsh-env-t05-map-room-2026-10-08/narrow-gap-encounter-publication.md`。
- **源 float 数值、地图全版本**：沿用 A 包保留的未验证范围，本包未扩大验证面。
- `dev-map` 的公开构建/规则集/目录/后端摘要只在本包输入上核对过；换 base 内容会变，判身份请看整组而不要只看规则集。

## 现场记录（DLL / 配置 / seed / run_id）

- 最终产物：`net-the-spire/Net.TheSpire.Host/bin/Debug/net9.0/Net.TheSpire.Host.dll`（SHA256 `9E65E0C4345B696CAE8E842BDF2810B3DAE5F6A93B53F7CD6E0FA6E166D7834C`）；检查产物 `net-the-spire/Net.TheSpire.Tests/bin/Debug/net9.0/Net.TheSpire.Tests.dll`（SHA256 `4AC2514EED45E33AEA6CBD51781D5C139ECC04D561817F1E32F10CA4B725E63D`，415 检查即此产物）。
- 配置面：`scenario=formal-dev`、`protocol=0.2`、`projection=base-spire:PlayerView`、`rng_profile=vanilla-uniform@1`、`frame_limit=1048576`、`ledger_capacity=4096`、`default_game_seed=ENV-IRONCLAD-00`；会话 seed 用字符串 `t05-map-alpha` / `t05-map-beta`；`scope` ∈ {`dev`, `dev-map`}。
- `run_id`：`env-t03-map-host-20261008T230122+0800`（本文档证据集合的标签；现场 session id 每次启动新分配，证据 NDJSON 里逐帧可查）。
- 本包改动文件：`net-the-spire/Net.TheSpire.Host/FormalDevScenario.cs`、`net-the-spire/Net.TheSpire.Host/Program.cs`、`net-the-spire/Net.TheSpire.Tests/FormalPathChecks.cs`、`net-the-spire/client/test_map_host.py`（新）、`net-the-spire/formal-dev-scopes.md`（新）、本文与[主清单 T03 行](../../tofix/README.md#env-t03)；`docs/contracts/net/0.2/wire.schema.json` 本包期间未改（mtime 16:38）。汇总现场记录见 [final-checks-2026-10-08.txt](../../../../.audit/dsh-env-t03-map-host-2026-10-08/final-checks-2026-10-08.txt)。
