# Host 声明闭合与切片后端依赖交接 — 2026-10-12

本包把公开运行构造的两处缺口一起收口：`describe` 的结构声明改用**同一 `get_catalog` 已发布的公开 ID**（I-71/I-84），旧切片/作者场景的宿主构造补上**规则包声明的计算后端**注入缝。修好后 net 检查在原 416 项全通过的基础上新增 2 项闭合判别（418/418），并在新构建的真实 Host 上录得 0.2 契约 **0 违规**的购买/离店与删牌轨迹。完整整局仍未通关，两条轨迹都在 Unknown 节点按 `unsupported_map_room` 停止。

归属：`ENV-T03` / `ENV-T03-host-declarations-close-20261012`（负责人 2026-10-12 裁决：修 Host 实现，沿用 I71/I84，不放宽 schema/契约）。
派发记录：`session-162ee024-d56e-4165-acf9-9f2350d6409d` / 请求 `f7ea0456-d926-4504-9758-b1e0b5001813`，见[本包 task-record](../../../.audit/dsh-host-declarations-close-20261012/task-record.json)与[短工作包](../../../.audit/dsh-host-declarations-close-20261012/task.txt)。
接手入口：[主清单 ENV-T03 项](../tofix/README.md#env-t03)当前认领与完成条件；契约规则见 [net 0.1 不变量 I-71/I-84](../../contracts/net/0.1/invariants.md)（0.2 沿用），实现接缝说明见 [net 设计 §7 新增小节](../../design/net-the-spire/design.md#env-t03公开声明闭合与旧切片后端装配2026-10-12)。
交付范围：`net-the-spire/Net.TheSpire/Control`、`net-the-spire/Net.TheSpire.Tests`、`net-the-spire/Net.TheSpire.Host` 旧切片装配、`docs/design/net-the-spire/design.md`、本包 audit 与 `.artifacts/parallel/dsh-host-declarations-close-20261012`。
本报告记录 2026-10-12 的交付；当前排期、状态与认领只以主清单为准。

## 接手后先做什么

1. 先看[主清单 ENV-T03 项](../tofix/README.md#env-t03)：本包已交付的是「声明闭合 + 旧切片后端注入 + 一条真实商店帧」；**不要**重做 SourceStableShuffle 源预排序、Engine/Slice 套件、17 条对拍或五角色矩阵。
2. 复核算力最小的一步（新共同闭包）：

   ```powershell
   # 仓库根；本包隔离闭包（固定 Core/Base DLL + 当前 net 源码）
   dotnet build .artifacts/parallel/dsh-host-declarations-close-20261012/build/Net.TheSpire.Tests/Net.TheSpire.Tests.csproj
   dotnet .artifacts/parallel/dsh-host-declarations-close-20261012/build/Net.TheSpire.Tests/bin/Debug/net9.0/Net.TheSpire.Tests.dll
   # 期望：TOTAL: 418 executed, 418 passed, 0 failed（原 416 项全过 + 2 项新增闭合判别）
   python -X utf8 -u .audit/dsh-host-declarations-close-20261012/verify_host_declarations.py
   # 期望：全部通过：0 违规
   ```

3. 与 A 并发写入的真实 Unknown 整合：本包**没有**读 A 的半成品重构建 base，帧来自负责人共同闭包 `.artifacts/parallel/owner-horizontal-extension-20261012` 的固定 DLL。整合后的复核（新共同闭包上的 describe/帧）按主清单交负责人/Codex 决定，本包不代做。

## 已交付与接口变化

**1. `describe` 的结构声明改为已发布公开 ID（修复主因）**

`Net.TheSpire/Control/ControlHost.cs` 的 `SupportedStructures` 原本把映射表的**引擎键**（`_profile.Kinds.Keys` 等）当公开声明 ID 发出去；目录发布的是 `net:kind:`/`net:relation:`/`net:action:`/`net:reason:` 形态。现在：

| 结构 | 取值 | 关键行为 |
| --- | --- | --- |
| `Entity` | `KindId` | 15 条公开 kind id（原 15 条引擎键） |
| `Relation` | `Disposition.Published.TypeId` | **过滤 NotPublished**：4 → 2（去掉 `KnownReveal`/`PlayedReveal` 这类未发布关系） |
| `LegalAction` | `ActionTypeId` + `AnswerAction.ActionTypeId` | **补 net 自声明的 `net:action:net.answer_decision`**：21 → 22 |
| `TerminalResult` | `ReasonId` | 2 条公开 reason id |

`Pile`/`Sequence`/`Group` 本来就发公开 ID，未改。新增私有助手 `PublishedIds`：去重 + 按公开 ID 的 UTF-8 字节序排序，使同一目录不因注册/遍历顺序产出不同声明。**没有**用空数组掩盖缺失，也没有改 schema、契约、协议语义或目录发射逻辑（`CatalogProjector` 未改）。

**2. 旧切片/作者场景的组合层补计算后端（注入缝，不是新 factory 框架）**

`ControlHost`/`ControlSession` 的**旧切片构造**各加一个可选 `IComputeBackend?` 参数并转交 `new Session(..., computeBackend:)`；正式 `ISessionSource` 路径不变（后端仍由 base 工厂在建会话时装好，控制入口只 Start 一次、保留正式 RNG/后端）。装配点在组合层（认识 base 包的一侧）：

- `Net.TheSpire.Host/Program.cs` 的 `legacy-slice` 分支传 `BaseSpireComputeBackend.Instance`；
- `Net.TheSpire.Tests` 的三个切片夹具（`AcceptanceChecks`/`ControlChecks`/`MeasurementChecks`）同样注入。

通用控制层不认识任何游戏 ID 或玩法，也不 new 第二份后端；不传时保持旧行为（执行该资产时报 `Unsupported`）。

**3. 新增声明闭合判别（一组，2 项）**

- `Net.TheSpire.Tests/DeclarationClosure.cs`（新）：`Unclosed` / `EngineKeysUsedAsDeclarationIds` 判据 + 0.1 切片真实宿主的正例，并在同一检查里用**引擎键反例**自证判据可区分（把 Entity 换成 `base-spire:Player`/`base-spire:Card`，判据必须报出 2 条）；
- `FormalPathChecks` 新增 `G-06`：0.2 dev-map 宿主真实 `describe` + `get_catalog`，逐条闭合、四类结构非空、ID 是 `net:` 公开形态；
- `Program.cs` 注册新组。判别读的是**两次真实请求的 wire**，不是 Profile 内部清单。

**4. 文档**：`docs/design/net-the-spire/design.md` 新增“ENV-T03 公开声明闭合与旧切片后端装配（2026-10-12）”，记申请 ID 空间、过滤规则与注入缝的实际用法。

## 验证与证据

固定输入（与[负责人闭包 SHA 记录](../../../.audit/owner-horizontal-extension-closure-sha256-20261012.json)一致，已核对本包构建输出逐字相同）：

| 资产 | SHA-256 |
| --- | --- |
| `Core.TheSpire.dll`（固定输入 = 本包输出） | `AFEE26472D062074B681EF4C59B61F88994AA72A252F4E0A0C81E3BAF9D223A2` |
| `BaseSpire.dll`（固定输入 = 本包输出） | `FDB4D02B7A33CBF985BA9270A35957EBAD81D516A2C991BD46623CD86570615C` |
| `Antlr4.Runtime.Standard.dll` | `0440E3D2C5906326E04DF87D6A5272192B7D29099901BA4215CA9E0F5DCA7591` |
| `Net.TheSpire.Host.dll`（本包构建） | `504A7C4B1C2B42F738087908B7A9D38EE2A194A16D84A4E92CE50B61DAD07D53` |
| `Net.TheSpire.dll` / `Net.TheSpire.Contracts.dll` | `48EBA452043216D03D3F77975655A510816FCDE30E5250D82CD1903DE7EBFDCE` / `6C6EA16102E06586B060FE30CD29BED9BB2E5534D55F58CA27A10F080E553C1F` |
| `docs/contracts/net/0.2/wire.schema.json` | `6299696398B5AA14688E432A8097DFF3F177DE68AC28F87128BC01637DEA46AE` |
| `tools/contract-check/wire_layers.py`（只读复用） | `30B43723FAE01EBF4B1B7C67AA9BB52D19E1B2D639F372C072EB3CCC4B84546F` |
| 旧 0.2 方法 `.audit/dsh-shop-public-contract-20261012/verify_shop_contract.py` | `459B1DBE9CBC323515044724879F0B39E6BDFC331EEAE55D3A81D10A7917CE3A` |
| 本包 0.2 方法副本 `.audit/dsh-host-declarations-close-20261012/verify_host_declarations.py` | `9A00A9BFFC93DCE405E1D4A026641C089FF7750BAD09B03298C332331DBBDD9D` |
| 旧 84 违规结果（**未改帧**） | `5478866845235B82BC05C13B71F6E71CAE0A4040D86C66F5E49FEEF589A68E4B` |

| 检查 | 工作目录与实际命令 | 返回码 / 结果 | 证据与证明范围 |
| --- | --- | --- | --- |
| 隔离闭包构建（固定 Core/Base DLL + 当前 net 源码，4 个临时工程） | 仓库根 `dotnet build .artifacts/parallel/dsh-host-declarations-close-20261012/build/Net.TheSpire.Tests/Net.TheSpire.Tests.csproj` | rc 0；0 警告 0 错误 | 临时工程定义在 [build/](../../../.artifacts/parallel/dsh-host-declarations-close-20261012/build/)；只引用固定 DLL，**未构建** solution/core/base/生产 net 工程（生产 net `bin`/`obj` mtime 仍是 2026-10-02/04） |
| net 基线复跑（未改源码） | 同上 dll 直接 `dotnet <dll>` | rc 1；**413 passed / 3 failed / 416 executed** | [net-baseline.log](../../../.artifacts/parallel/dsh-host-declarations-close-20261012/logs/net-baseline.log)；3 条与负责人日志同项同首错：V-03/V-09/V-10 均 `Unsupported: compute asset 'base-spire:SourceStableShuffleOrder' needs a compute backend but none is installed` |
| net 修复后复跑 | 同上 | rc 0；**418 passed / 0 failed / 418 executed** | [net-after-closure-checks.log](../../../.artifacts/parallel/dsh-host-declarations-close-20261012/logs/net-after-closure-checks.log)；原 416 项全过，新增 2 项 `G-06` 与 `I-71/I-84 0.1 切片…反例可区分` 另列，未替换原项 |
| 真实新 Host 帧（0.2 / dev-map / seed `ENV-T03-SEED-L`） | `python -X utf8 -u net-the-spire/client/run_game.py --host-dll <本包 Host.dll> --scope dev-map --seed ENV-T03-SEED-L --output ... --max-actions 90 --use-potions` | rc 4（`unsupported_map_room`，如实边界） | 购买/离店轨迹 [runs/shop-buy-remove](../../../.artifacts/parallel/dsh-host-declarations-close-20261012/runs/shop-buy-remove/)（111→63，`removal_unavailable` 75>63，MapPhase 7→2，45 步）；删牌轨迹 [runs/shop-remove](../../../.artifacts/parallel/dsh-host-declarations-close-20261012/runs/shop-remove/)（111→36，`shop_remove_request`/`answer`，MapPhase 7→2，46 步）。两者都在 Unknown 停止，`run_complete=false`、`whole_run_outcome=null`，**未声称通关** |
| 0.2 契约校验（复用旧包 0.2 方法，只改固定 Host 与轨迹路径两处） | `python -X utf8 -u .audit/dsh-host-declarations-close-20261012/verify_host_declarations.py` | rc 0；**全部通过：0 违规** | [verify-host-declarations.log](../../../.artifacts/parallel/dsh-host-declarations-close-20261012/logs/verify-host-declarations.log)、[verify-host-declarations.json](../../../.audit/dsh-host-declarations-close-20261012/verify-host-declarations.json)；执行数 `l1_request_frames=99, l1_response_frames=99, l1_snapshots=93, l2_catalog_declarations=944, l3_snapshots=93, l4_seed_echo_checked=2, shop_steps=91, shop_visit_snapshots=7`。方法差异仅两处固定路径（`git diff --no-index` 见 [verify-method-diff.log](../../../.artifacts/parallel/dsh-host-declarations-close-20261012/logs/verify-method-diff.log)） |
| 声明 ID 修复前后对比 | `python -X utf8 .audit/dsh-host-declarations-close-20261012/dump_supported_structures.py <新帧> <旧冻结帧>` | rc 0 | [supported-structures-before-after.log](../../../.artifacts/parallel/dsh-host-declarations-close-20261012/logs/supported-structures-before-after.log)：旧帧 `Entity 15 引擎键 + Relation 4 + LegalAction 21 + TerminalResult 2 = 42/局`（两局 84，与负责人裁决一致）；新帧 `15 公开 kind + 2 已发布关系 + 22 动作(含 answer_decision) + 2 reason` |
| 旧切片宿主 stdio 冒烟 | `python -X utf8 .audit/dsh-host-declarations-close-20261012/legacy_slice_stdio_smoke.py` | rc 0 | [legacy-slice-stdio-smoke.log](../../../.artifacts/parallel/dsh-host-declarations-close-20261012/logs/legacy-slice-stdio-smoke.log)：`legacy-slice` 0.1 形状 describe/start/observe/step 全成功（12 步） |
| 固定输入与新旧帧对比 | `python -X utf8 .audit/dsh-host-declarations-close-20261012/compare_new_vs_frozen.py` | rc 0 | [compare-new-vs-frozen.log](../../../.artifacts/parallel/dsh-host-declarations-close-20261012/logs/compare-new-vs-frozen.log)：固定 DLL SHA 与本包输出逐字相同；两局金币总账与商店事件与旧冻结帧一致 |
| 文档检查 | 仓库根 `python tools/doc-check/check_docs.py`、`git diff --check` | doc-check 5→4 problem(s)；`git diff --check` rc 0 | 本包链接补好后仅剩 4 条**与本包无关**的既存锚点问题（`dsh-power-fixture-handoff-2026-10-11.md` 的 `tools/differential/README.md#L7`、`environment-implementation-constraints.md#L181`，及 `dsh-startup-alignment-handoff-2026-10-11-f68b943d.md` 引用它们的两处）。`net-the-spire/` 未被 git 跟踪，`git diff --check` 不覆盖其中文件；已单独核对这些文件无行尾空白、均有结尾换行 |

**本次未运行**（按任务无需重复，也不要当作通过）：`dotnet build AutoTheSpire.sln`；Engine/Slice 套件；17 条对拍；五角色矩阵；生产 net 工程的常规构建；0.1 旧客户端的完整公开全程。

## 差异、阻塞与未验证

**首个差异（可解释，不是本包回归）**：本包新帧的规则集身份是 `net:ruleset:3fb6f72aee4c3a11`，旧冻结帧是 `net:ruleset:8ee60bba81741c21`；`catalog_id`（`net:catalog:c3034556e5ff196d`）与 `backend_profile_id` 相同。原因是旧帧由早一版 base 构建产出，而本包按裁决必须用负责人共同闭包固定 DLL（含源重洗排序）。后果：抽牌顺序/战斗长度不同，两组轨迹前 17 步逐步一致、第 18 步起打战斗多一次「出牌+应答」，其后奖励/地图/商店/离店动作与**金币总账完全一致**（111→63 / 111→36），步数相同（45/46），状态同为 `unsupported_map_room`。这正是「只改身份不改语义」的预期形状；本包 net 改动只触及 `describe` 的声明列表与旧切片构造的可选后端参数，0.2 正式路径的会话/投影/动作执行代码未改。

**历史事实（不改帧）**：`.audit/owner-horizontal-extension-shop-contract-20261012.json` 仍是 84 条 Host 结构声明违规（两局各 42），SHA `54788668…A68E4B` 未变；旧冻结帧 SHA `57337E51…9F7CDC`（buy-remove）、`D03C04C6…11CD198`（remove）未变。本包不重跑、不改写这些帧。

**未验证 / 明确边界**：

- 完整整局仍未通关：两条轨迹都在 Unknown 节点停止（offered=`['Unknown']`），不记整局通过、不伪造胜负。
- Unknown-room 整合：A 正在并发写真实 Unknown（本包工作期间 `base-spire/.../BaseSpireRuntimeFactory.cs` 的 mtime 仍被 A 更新），本包未读其半成品、未重构建 base，所有帧都来自负责人共同闭包的固定 DLL。整合后的新共同闭包复核由 Codex 决定。
- 旧切片 `legacy-slice` 的**计算资产执行路径**只在检查夹具里被真实触发（V-03/V-09/V-10 的 SourceStableShuffleOrder），stdio 冒烟只覆盖 0.1 协议形状与装配，未在 stdio 上跑到重洗。
- 未验证与本体适配器、训练入口或完整 0.2 父项完成条件相关的任何部分。

**依赖**：无阻塞依赖。`ENV-T09-unknown-room-modules-20261012`（A）决定 Unknown 房型能否继续；本次不改动、也不等待它。

## 维护位置

- 任务状态/认领/完成条件：主清单 [`ENV-T03`](../tofix/README.md#env-t03)（**Codex 单写**，本包未改主清单）。
- 规则与实现方法：`docs/contracts/net/0.1/invariants.md`（I-71/I-84，未改）、[net 设计 §7 新增小节](../../design/net-the-spire/design.md#env-t03公开声明闭合与旧切片后端装配2026-10-12)。
- 本包证据：`.audit/dsh-host-declarations-close-20261012/`（脚本 + 校验结果）与 `.artifacts/parallel/dsh-host-declarations-close-20261012/{build,runs,logs}/`；长日志只放这两处，不复制进主清单。
- 修改的生产文件：`net-the-spire/Net.TheSpire/Control/{ControlHost,ControlSession}.cs`、`net-the-spire/Net.TheSpire.Host/Program.cs`、`net-the-spire/Net.TheSpire.Tests/{AcceptanceChecks,ControlChecks,MeasurementChecks,FormalPathChecks,Program}.cs`、新增 `net-the-spire/Net.TheSpire.Tests/DeclarationClosure.cs`；未触碰 base/core/tools/schema/协议语义/公共主清单。
