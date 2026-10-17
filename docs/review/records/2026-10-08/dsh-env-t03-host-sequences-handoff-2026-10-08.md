---
task_id: ENV-T03
task_name: C 线通用公开 Sequence 运行时 owner 子包（声明派生 Sequence + 真实 Host 交接）
dispatch_id: ENV-T03-host-public-sequences-20261008
session_id: session-9b410fa1-95dc-4e37-bdb2-43d8feb14db0
dispatch_record: .audit/dsh-env-t03-host-sequences-2026-10-08/task-record.json
delivery_status: ready_for_review
updated_at: 2026-10-08T18:52:00+08:00
---

# ENV-T03 C 线通用公开 Sequence 运行时 owner 子包交接 — 2026-10-08

本包把 net 公开映射层的 `sequence` 发布从「必须写死一个公开 owner 引用」改成「按元数据的 owner 种类在本快照里唯一解析可见实例」，并由 `FormalDevMetadata` 把 A 线（base 领域 Sequence 元数据）转换进来，使规则声明的有序公开集合不再被 `N-02-SEQUENCE-UNSUPPORTED` 拒绝；旧 0.1 固定 `OwnerPublicRef` 路径保留。本包**不**关闭 ENV-T03 父项，也不代表「只改规则包新增字段/Action」的扩展证据已完成。

归属：ENV-T03「公开 Host 接唯一工厂；从规则声明派生公开出口」的 C 线通用 Sequence 运行时 owner 子包（主清单 `#### ENV-T03`）。
接手入口：[主清单 ENV-T03](../../tofix/README.md#env-t03)、[net 设计 §7 实施边界](../../../design/net-the-spire/design.md#7-2026-10-07公开语义透传的源码核对与接续)、[net 协议 §2/§7](../../../design/net-the-spire/protocol.md)、[C 提示词](../../../design/agent-goals.md#5-cnet-the-spire-编码-agent)。
交付范围：负责会话 `session-9b410fa1-95dc-4e37-bdb2-43d8feb14db0`；可写 `net-the-spire/**`、`docs/contracts/net/0.2/**` 及必要 net 文档、`.audit/dsh-env-t03-host-sequences-2026-10-08/**` 与本交接。`core-the-spire`、`mod-the-spire`、`tools`、`reference`、`ai-the-spire` 只读；主清单不编辑。
依赖：A 线「base 公开 Sequence 元数据」——派单记录 [.audit/dsh-env-t03-base-sequences-2026-10-08/task-record.json](../../../../.audit/dsh-env-t03-base-sequences-2026-10-08/task-record.json)、固定交接 [dsh-env-t03-base-sequences-handoff-2026-10-08.md](dsh-env-t03-base-sequences-handoff-2026-10-08.md)（原 tofix 路径已由负责人并入 records）。base 不得依赖 net；Host 只做转换，不复制字段/Action/集合逐项清单。
本报告记录 2026-10-08 的交付；当前排期、状态与认领以主清单为准。

## 交付摘要

本包（C 线通用公开 Sequence 运行时 owner 子包）已把三件事做完并留下可复核证据，`delivery_status: ready_for_review`：

1. **net 通用运行时 owner 种类**：`PublicCollectionUse.Sequence` 不再被 `N-02-SEQUENCE-UNSUPPORTED` 拒绝；owner 只声明**公开种类**，投影时按该种类在本快照里解析**唯一**可见实例（沿用 `BuildGroup` 同一条通用路径），缺 owner 或多 owner 明确拒绝，不猜 owner、不写死每局句柄；旧的固定 `OwnerPublicRef` 路径保留。
2. **Host 转换接线**：`net-the-spire/Net.TheSpire.Host/FormalDevMetadata.cs` 的 `FromBase` 增加 `BasePublicCollectionUse.Sequence` 分支，把 A 线元数据的 `OwnerKindEngineTypeId` 翻成 `PublicCollectionRule.Sequence(...)`；无 owner 种类时在转换期抛错，不静默降级成「不发布」。
3. **判别检查 + 最终输入绑定的公开证据**：F-07/F-08/F-09 三条检查重写（顺序反转/丢成员与 owner 错误可区分），根构建 rc=0、net 402/402、Python 103/103，正式 Host NDJSON 双 seed 轨迹绑定最终 DLL（见「验证与证据」）。

未交付项与既有缺陷（base `PowerStacksOf`、`EncounterPool/Queue`、Group 多重性）见「差异、阻塞与未验证」；**本包不关闭 ENV-T03 父项，也不等于「只改规则包新增声明」的扩展证据已完成**。

## 接续位置（最终）

- 侦察期定位的三个缺口已全部落地：`DerivedPublicProfile` 的 `Sequence` 分支、`PublishedSequence` 的 owner 种类路径、`PublicSnapshotProjector.BuildSequence` 的按种类唯一解析。
- A（`ENV-T03-base-public-sequences-20261008`）与 B（`ENV-T04-rng-calibration-recovery-20261008`）均已停止（固定交接 `ready_for_review`，A 交接已并入 `docs/review/records/2026-10-08/dsh-env-t03-base-sequences-handoff-2026-10-08.md`），B→A→C 顺序已满足，构建与实测已按最终输入完成。
- 接手位置：先读本节 +「验证与证据」的表与哈希；若需复核，直接重跑表中的命令（不需要重改源码）。剩余工作（不属于本包）：`PowerStacksOf` 纯度/派生缺陷、`EncounterPool/Queue` 公开投影、T05 地图/奖励、声明扩展子包。

## 接手后先做什么

1. 先核对[主清单 ENV-T03](../../tofix/README.md#env-t03) 的认领与前置状态；本包完成时 A/B 均已停止（A 交接 `ready_for_review`、B 交接 `ready_for_review`），三者共享 `obj`/`bin`，**不得并行构建**。
2. 复跑本包证据（仓库根目录）：`dotnet build AutoTheSpire.sln -v m`、`dotnet run --no-build --project net-the-spire/Net.TheSpire.Tests`、`python -X utf8 -u net-the-spire/client/test_client.py`、`python -X utf8 -u .audit/dsh-env-t03-host-sequences-2026-10-08/artifacts/capture_formal_ndjson.py`；Engine/Slice 门槛在本包按 A 的同一最终 base 输入复用其有效日志（哈希见「验证与证据」）。
3. 注意：正式内容连续出两次**攻击牌**会在引擎层触发 base `PowerStacksOf` 纯度缺陷（见「差异、阻塞与未验证」），F-08 因此以「第二张为技能牌」的合法两连出牌取证；不要把它误判为 net 回归。
4. 本包**未**交付的部分写在「差异、阻塞与未验证」；不要据本包自判 ENV-T03/M01 验收。

## 已交付与接口变化

### 1. net 公开映射层：通用运行时 owner 种类（本包主交付）

- `net-the-spire/Net.TheSpire/Projection/PublicVocabulary.cs`：`PublishedSequence` 同时支持**旧固定公开引用**与**公开种类**两条路径（`OwnerPublicRef`/`OwnerKindEngineTypeId`，`ForOwnerKind(...)` 按种类构造）。sha256 `03f87b7d1d7313f2fe3b9703a257f679392e38f3e929c5226eeffa240d7b0c8a`。
- `net-the-spire/Net.TheSpire/Projection/PublicBuildMetadata.cs`：`PublicCollectionRule` 有 `Sequence(string ownerKindEngineTypeId)` 工厂（与 `Pile`/`Group`/`NotPublished` 并列）。sha256 `4397de31a6d8918defd17f30b00a08aa77faa92a454af96e9c9e3f1a167d43da`。
- `net-the-spire/Net.TheSpire/Projection/DerivedPublicProfile.cs`：`BuildCollections` 的 `PublicCollectionUse.Sequence` 分支不再抛 `N-02-SEQUENCE-UNSUPPORTED`，改为按声明的 owner 种类派生 `PublishedSequence`；派生期缺 owner 种类即抛（不降级）。sha256 `03220678237702ffe583720f228edb141ce10f4946e01f632da1a04f337c80df`。
- `net-the-spire/Net.TheSpire/Projection/PublicSnapshotProjector.cs`：`BuildSequence` 在有 owner 种类、无固定引用时，按种类在本快照里解析**唯一**可见实例（复用 `BuildGroup` 的同一通用路径）；0 个候选（缺 owner）或 ≥2 个候选（多 owner）⇒ `N-02-SEQUENCE-OWNER` 拒绝；`entitiesByKind` 额外收集 sequence 声明的 owner 种类；保留旧固定引用路径。sha256 `26c0e7ee25c8c46ee91903dc1f22799f351db4075a7d5f6047f1dce7517b7966`。
- `net-the-spire/Net.TheSpire/Control/ControlSession.cs`：新增/保留同构建内检查用的测试访问器（`EngineForTests`/`ParticipantForTests`/`ProjectionIdForTests`），用于「发布结果与 ParticipantView/引擎候选同源」对拍。sha256 `a240486c433757ffbc00cd06d0708ad5646dd98ecc941df5c2b4d645c39ffb52`。
- `net-the-spire/Net.TheSpire/Control/ControlHost.cs`：describe 的 `limits`/`supported_structures` 与实际发布的 sequence 种类一致（F-07 逐项对照），本包未改 wire。sha256 `ab002ab6fffe779c3a02bc55d6c9e55c1871d786d1be8ff8630da081de8acdd3`。
- **wire 未变**：`docs/contracts/net/0.2/` 与 `Net.TheSpire.Contracts` 无本包改动（`wire.schema.json` 最后写入 2026-10-08 16:38:06，早于本包；`sequences`/`sequence_types` 已存在），不需要为每个集合新增字段。

### 2. Host 转换接线（本包）

`net-the-spire/Net.TheSpire.Host/FormalDevMetadata.cs`（sha256 `84dd0087c458e7856f9eb9c0ad44280ca98fec8ed60bae3c81b4a8585b120137`）的 `FromBase`：`declared.Use switch` 增加

`BasePublicCollectionUse.Sequence => PublicCollectionRule.Sequence(declared.OwnerKindEngineTypeId ?? throw new InvalidOperationException($"base 集合 '{collectionId}' 声明为 sequence 但没有 owner 种类")),`

保留 Pile/Group/NotPublished 分支与 `_ => throw new NotSupportedException(...)`（不静默降级成「不发布」）。Host 只做元数据转换，不复制 base 字段/Action/集合逐项清单。

### 3. 判别检查（本包重写，`net-the-spire/Net.TheSpire.Tests/FormalPathChecks.cs`，sha256 `dc617816730332adcaf0c572c3b627225b7cc33f6a07f952fa388ad117d7e11f`）

- **F-07** 声明与 describe 一致：目录 wire 顶层为 `{protocol_version, ruleset_id, catalog_id, declarations}`，`sequence_types` 在 `declarations` 内（原实现读顶层 → 已修）。
- **F-08** `PlayedThisTurn` 真实顺序/成员与 `ParticipantView` 对应：同一 seed 最多 6 次、每次**重开新会话**；第一张取公开引用**最大**的手牌候选、第二张取**最小**且未被拒的候选（保证 raw 非升序）；第二张若为 `backend_fault` 则记入 rejected 换候选重开，非 `backend_fault` 的失败直接判失败；成功后逐位比对发布 elements 与 `ParticipantView` 顺序，并断言 raw 非升序（顺序反转/丢成员/多成员都会失败）。
- **F-09** 缺 owner / 多 owner 明确拒绝：派生层 `N-02-UNDECLARED-COLLECTION`、投影层 `N-02-SEQUENCE-OWNER`；旧固定引用（e9）仍正常发布。
- 新增 `DispatchOrFault(...)`：把 `EngineFaultException`（`core-the-spire/Core.TheSpire/Engine/Value.cs:479`）翻成 `("backend_fault", code：message)`，使「引擎拒绝该候选」与「net 回归」在检查里可区分。
- 既有 399 项检查未改动语义；本包新增/重写 3 项 ⇒ net 计数 399 → 402。

### 4. 原完成条件逐项状态（沿用前任包 6 条）

| 原条件 | 状态 | 说明 |
| --- | --- | --- |
| ① Host 组合层 `ISessionSource` 正式实现包住 `BaseSpireRuntimeFactory`，`Program.cs` 提供 formal-dev 开关且默认仍 0.1 切片 | 已交付（前驱包，本包未重写） | `net-the-spire/Net.TheSpire.Host/BaseSpireFormalSource.cs`、`Program.cs` 保持可用；本包只补转换与检查 |
| ② A 线 base 不可变公开语义元数据转 `PublicBuildMetadata` | 本包接通 Sequence 分支 | `FormalDevMetadata.FromBase` 的 `Sequence` 臂 |
| ③ 身份 ruleset/catalog/backend/scenario 一致，静态身份不含每局 seed，start 成功恰为 `{session_id, seed, snapshot}` | 已交付并有 NDJSON 证据 | 见「验证与证据」§NDJSON |
| ④ 真实 NDJSON：同进程两个不同字符串 seed + 至少一条合法出牌或结束回合，不改血/清场/注入 | 本包交付（绑定最终 DLL） | `host-formal.ndjson` / `host-formal-summary.json` |
| ⑤ 保留 0.1 数字 seed 旧场景；`expected_identity`/`scenario_profile` 前置拒绝、同 request_id 重发幂等、候选不泄漏隐藏状态 | 已交付并有 NDJSON 证据 | 0.1 进程对照 + 两条拒因 + 幂等重发 |
| ⑥ 同批更新 C#/Python/schema | 已交付 | C# 402/402、Python 103/103；本包无 wire/schema 变更（已有字段） |

## 验证与证据

所有命令的工作目录＝仓库根 `F:/Scylier-Project/active/auto-the-spire`；日期 2026-10-08。

| # | 检查 | 实际命令 | 返回码与结果 | 证据文件与证明范围 |
| --- | --- | --- | --- | --- |
| 1 | 根构建 | `dotnet build AutoTheSpire.sln -v m` | rc=0，`已成功生成`、0 个警告、0 个错误 | `.audit/dsh-env-t03-host-sequences-2026-10-08/build-final.log`（sha256 `55DADC37387805E9F7C420B4CC8D33413BDDAF01074E10BBB32239B944F4DF11`）：最终 net+base 源码整体可构建 |
| 2 | net 检查 | `dotnet run --no-build --project net-the-spire/Net.TheSpire.Tests` | rc=0，`TOTAL: 402 executed, 402 passed, 0 failed` | `net-final.log`（sha256 `74ED945F6CDDBC3B09005CD506C878A591FE0FCD4D7387EA68E7968299A88C5D`）：F-07/F-08/F-09 与既有 399 项同批通过 |
| 3 | Engine 门槛（复用 A，同一最终 base 输入） | A 包 `03-engine-tests.txt` | rc=0，`All 95 engine checks passed.` | `.audit/dsh-env-t03-base-sequences-2026-10-08/artifacts/03-engine-tests.txt`；最终 `BasePublicSemantics.cs` sha256 `3a09d1297a45dfb4ef796a449543a2da8041d9723122193746b258b01907df4d`、`PublicSemanticsChecks.cs` sha256 `aa6207a02b8a906c2bbdb5782ab7a36087370e581d5a151f9f14fb602f54c676` 与 A 记录一致 ⇒ 复用有效 |
| 4 | Slice 门槛（复用 A，同一最终 base 输入） | A 包 `04-slice-tests.txt` | rc=0，`All 333 slice checks passed.`（含 A 新增第 9 条判别） | 同上目录 `04-slice-tests.txt`；base 项目自身构建 0 错误 / 1 个预存在警告 `Powers/SpirePowerSource.cs(1241,39)` CS8604 |
| 5 | Python 全兼容 | `python -X utf8 -u net-the-spire/client/test_client.py` | rc=0，`wire 28 / transport 7 / recovery 3 / faults 12 / formal 53`，`TOTAL: 103 passed, 0 failed, 103 executed` | `client-all-final.log`（sha256 `D345BACAE913028A430FAD15E04B89AB392259543FB68EAE53A4DB5597948103`） |
| 6 | 正式 Host NDJSON（双 seed + 拒因 + 幂等 + 0.1 对照） | `python -X utf8 -u .audit/dsh-env-t03-host-sequences-2026-10-08/artifacts/capture_formal_ndjson.py` | rc=0 | `artifacts/host-formal.ndjson`（316094B，sha256 `981546D3CCF6E37BDEA6B91E241FD015AD51819BC2FDCB845A949D12D06B7269`）、`artifacts/host-formal-summary.json`（19032B，sha256 `D6C8E9701AA40130E09C20AC10A4CB0AE750AB65C0BFC3B438CBB3C0D891B785`）；`run_id=env-t03-host-sequences-20261008T184609+0800`；绑定最终 `net-the-spire/Net.TheSpire.Host/bin/Debug/net9.0/Net.TheSpire.Host.dll` sha256 `36b85da8213918ae003610765007c9763301925fbf7cc8a628b8864796acc660` |
| 7 | 文档门槛（纯文档改动） | `python -X utf8 tools/doc-check/check_docs.py`、`git diff --check` | doc-check rc=4（263 份 Markdown；**本文件 0 问题**，余 4 条属他线在制交接 `dsh-env-t04-init-diagnosis-handoff-2026-10-08.md`、`dsh-env-t05-map-room-handoff-2026-10-08.md` 的相对链接，本包不改他线文件）；`git diff --check` rc=0 | `doc-check-final.log`；本文件自身链接（主清单/设计/协议/提示词/A 记录）已全部按文件相对路径修正后可解析 |

### NDJSON 关键读数（全部取自最终 DLL 的同一 run_id）

- **0.2 身份**：`backend net:backend:simulation-0.2-ca6767486c903ad4`、`catalog net:catalog:3440d8467b85ae03`、`ruleset net:ruleset:4206675da0ca12f6`、`protocol_version 0.2`、scenario `base-spire-singleplayer-dev-v1`。
- **目录计数**：entity_kinds 12、content_types 129、fields 46、action_types 5、pile_types 5、**sequence_types 6**、terminal_reasons 2；`limits`＝sequences 6、elements_per_sequence 256、groups 0、members_per_group 0、sessions 64；`supported_structures` 的 Sequence 声明为 `net:sequence_type:base-spire:{Known,LongTermDeck,Monsters,PlayedThisTurn,Relics,RunStates}`，Group 声明为空且带 unsupported_constraints。
- **前置拒绝**：错目录 ⇒ `version_mismatch`（`$.expected_identity`、`execution_state: not_started`、`retry_mode: stop`）；0.2 下 legacy 场景 ⇒ `unsupported_capability`（`$.scenario_profile`）。
- **会话一 `ENV-IRONCLAD-00`**：`session-ab40b724208d-1`；start 结果键恰为 `[seed, session_id, snapshot]`；同 request_id 重发返回同会话同 seed；observe 671 → step PlayCard → 675，`PlayedThisTurn [] → ["e14"]`，公开玩家 Hp `80 → 80`；step 同 request_id 重发返回同观察号（幂等）；真实决策 `c-4105951702ade996` 回答 ok。
- **会话二 `ENV-T03-C-02`**：`session-ab40b724208d-2`；observe 721 → 725，`PlayedThisTurn [] → ["e14"]`；`Monsters` owner 为 `e24`、成员 `[e23,e21,e22]`，而会话一 `Monsters` owner 为 `e22`、成员 `[e21]` ⇒ **公开 owner 按种类在运行时解析、跨 seed 自动绑定，未写死每局句柄**；`Known/Monsters/PlayedThisTurn` 解析到 Player 种类实例，`LongTermDeck/Relics/RunStates` 解析到 Run 种类实例。
- **旧 0.1 进程对照**（`--scenario legacy-slice --seed 7`）：identity protocol `0.1`、ruleset/catalog `net:…:base-spire-combat-slice-v1`；start 结果键仅 `[session_id, snapshot]`（旧形状保留，无 `seed` 键）；PlayCard step ok；`sequences {}`（0.1 不发布 sequences）⇒ 旧数字 seed 与固定 `OwnerPublicRef` 路径兼容。
- **Host stderr（就绪行）**：`net-the-spire host ready: scenario=formal-dev protocol=0.2 … projection=base-spire:PlayerView rng_profile=vanilla-uniform@1 default_game_seed=ENV-IRONCLAD-00 frame_limit=1048576`。
- **F-08 实测输出**（`net-final.log`）：`F-08 第1次：第二张 e14 打出后引擎故障（backend_fault）`、`第2次 e15`、`第3次 e2`，随后 `F-08 base-spire:PlayedThisTurn：ParticipantView = e8,e7；发布 elements = e8,e7；被引擎拒绝的第二张候选 3 个`（`e8,e7` 为 ordinal 非升序 ⇒ 反转或丢成员会被检查捕获）。
- **F-09 实测输出**：派生层 `N-02-UNDECLARED-COLLECTION`、投影层 `N-02-SEQUENCE-OWNER`；旧固定引用（e9）仍发布。

## 差异、阻塞与未验证

1. **base/core 既有缺陷（不在本包范围，未改 base/core，仅报告）**：正式内容连续打出**两张攻击牌**时，`Session.LegalActions` 触发
   `SessionStateInvalid: pure procedure 'base-spire:PowerStacksOf' did not complete; pure procedures cannot suspend`
   （响应 `error={"code":"backend_fault","execution_state":"unknown","retry_mode":"stop"}`；栈 `Session.InvokePure (core-the-spire/Core.TheSpire/Engine/Session.Expressions.cs:135) ← Eval ← Scheduler.<>c__DisplayClass54_0.<LegalActions>b__0 (Scheduler.cs:251) ← Fatal (Scheduler.cs:484) ← Session.LegalActions (Scheduler.cs:220) ← ControlSession.Snapshot() (net-the-spire/Net.TheSpire/Control/ControlSession.cs:328) ← Settle (:346) ← Step (:280) ← ControlHost.Step (ControlHost.cs:412) ← Dispatch (:123)`）。
   复现（`.audit/dsh-env-t03-host-sequences-2026-10-08/probe_two.py`，真实 Host NDJSON）：seed `ENV-IRONCLAD-00`，Strike → 回答决策 → 第二张 Strike/Bash 失败；第二张换成 Defend（技能牌）成功并保留 `PlayedThisTurn=['base-spire:strike-ironclad','base-spire:defend-ironclad']`；同批 0.1 `legacy-slice --seed 7` 连续 3 次攻击全部 ok ⇒ 仅正式 base 内容路径触发。声明位置 `mod-the-spire/base-spire/BaseSpire/Composition/CombatSlicePackage.cs:281-295`（`package.Procedure("PowerStacksOf")`、`.Pure().Result(FieldTypeSchema.Int(-99,99))`、体为 `First(Where(Relation(powers, owner), pred))`，关系需重新派生时会挂起）。
   **本包据此把 F-08 的两连出牌取证改为「第二张为技能牌」的合法路径**，攻击牌候选的引擎拒绝在检查中显式计入 rejected 并打印；建议负责人在 T03/后续子包派单给 base/core。
2. **公开集合仍不可完整表达**：`EncounterPool`/`EncounterQueue` 在 base 元数据里仍是 `NotPublished`（A 线裁定：成员种类不在 `base-spire:PlayerView` 投影内）；`limits.groups=0`/`members_per_group=0` ⇒ 公开协议当前不表达重复成员/多重性。**不能据此声称「完整公开 Profile」验收**。
3. **未交付/未验证**：T05 地图/房间/奖励新选择路径；「只改规则包新增字段或 Action、net 不新增逐项注册」的声明扩展证据（原 T03 的下一子包）；跨 Host 进程的长时间对拍与训练；参考玩法未改（冻结 core engine/ir@0.3）。
4. **验收边界**：主清单 2026-10-08 18:46:34 仍记本会话 `session-9b410fa1-95dc-4e37-bdb2-43d8feb14db0` 为 running（seq1008）；本交接为 `ready_for_review`，**accepted 不代表验收，父项 ENV-T03 不关闭**，请负责人按最终源码/DLL/日志哈希自行收口。

## 维护位置

- 状态与认领：主清单 ENV-T03 条目（负责人维护，本包不编辑）。
- 派单记录：`.audit/dsh-env-t03-host-sequences-2026-10-08/task-record.json`。
- 依赖派单记录：`.audit/dsh-env-t03-base-sequences-2026-10-08/task-record.json`。
- 后续证据：本文件；长日志与 NDJSON 轨迹放 `.audit/dsh-env-t03-host-sequences-2026-10-08/artifacts/`。

## 19:09负责人接收说明

实际会话停止，结果取齐至1370，turn/end completed。1370窗口hasMore=true，另取before364历史后hasMore=false、最早seq0；没有把最后窗口当完整交付。当前四个关键net源码/DLL摘要与报告一致，root build/net402/Python103/两seed NDJSON日志复用，不在A运行时重复构建。仅核验通用序列与已执行的正常公开路径；F-08显式避开失败攻击候选的技能路径不能验收全部合法动作。连续攻击PowerStacksOf故障已交主清单A依赖，T03仍缺声明扩展和两集合公开表达，父项不关闭。详见[当日复核](dsh-supervisor-review-2026-10-08.md#1909复核与声明扩展源输入接续)。移入records只接收日期证据，不关闭父项。
