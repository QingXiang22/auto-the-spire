# ENV-T03 商店公开契约收口（真实帧 × net 0.2）交接 — 2026-10-12

本包用 Codex 当前共同 Host 与负责人已录的两局**正常商店轨迹**，把商店的公开契约收口到「可复算、可核对、
可继续」：真实 request/response/snapshot 帧通过 net 0.2 wire schema 与适用 L2/L3/L4 不变量，商店十四格
库存/不可买原因/买卡永久副本与金币一次扣除/删牌暂停后按已公开实例删对牌/相位 7→2 全部有逐帧证据；
同时补了一个**现场反例**证明「已消费的旧候选配当前观察会被拒绝且不推进，之后仍可离店」。
未完成的关键范围：整局仍在 `Unknown` 节点以 `unsupported_map_room` 停止（不是整局通过），
Source Eternals/未实现遗物效果仍属 base 范围，且发现一处**本包无写权限**的 Host 侧 0.2 声明不一致。

- task_id: `ENV-T03-shop-public-contract-20261012`
- dispatch_id（= 派发记录 `task_id`）: `ENV-T03-shop-public-contract-20261012`
- session_id: `session-6cbdba33-19fc-4a1d-a19d-bb6575710b01`
- request_id: `f2d4e51d-32ba-459c-9b7d-9e0f515e8e78`
- dispatch_record: [.audit/dsh-shop-public-contract-20261012/task-record.json](../../../.audit/dsh-shop-public-contract-20261012/task-record.json)（`status: accepted`）
- delivery_status: `ready_for_review`（公开契约证据与消费者缺口修复已交；完整整局与 Host 侧声明项未收口）
- updated_at: `2026-10-12T07:58:36+08:00`
- 主清单对应项：[T03](README.md#env-t03)（本包不修改主清单；状态与认领由 Codex 独自修改）

## 接手后先做什么

1. **先核本包已交付部分，别重做商店**：商店生产（A 的 `ShopRoomWiring`）、公开语义透传（C# 投影）、
   客户端商店策略与删牌 decision 都已交付。本包只做三件事：帧级契约校验 helper、`replay` 组的两处
   实际消费者缺口修复、一个现场反例。`--shop-*` 语义、`ShopOffers` 字段、五个动作名**未改**。
2. **一条命令复核本包结论**（仓库根目录，只读、不构建、不启动 Host）：

   ```powershell
   python -X utf8 -u .audit/dsh-shop-public-contract-20261012/verify_shop_contract.py --verbose
   ```

   预期：`0.2 wire 门槛（L1/L2/L3/L4）: 0`、`商店公开闭合: 0`、`Host 结构声明（非本包写权限，待负责人裁决）: 84`
   （42 项 × 2 局），退出码 1（有违规）。三类分列就是为了让 Host 侧那一条不掩盖商店收口结论；
   把本包反例局也加进来（多传一个 `--run-dir .audit/dsh-shop-public-contract-20261012/runs/shop-buy-probe`）
   同样是「Host 结构声明 126、其余 0」，并额外校验那条被拒绝的 `step` 不推进。
3. **要接着推进的负责人决定**（见 §4 差异与阻塞）：`capabilities.supported_structures[*].declaration_ids`
   在 `Entity`/`Relation`/`LegalAction`/`TerminalResult` 上用的是机制短名而不是目录声明 ID——
   是修 Host 投影，还是修订 0.1/0.2 的 I-71/I-84 口径，需要负责人裁决；本包**无 C#/Host/schema 写权限**。
4. 完整 T03 与整局验收仍开放：两局真实轨迹都在 `Unknown` 节点停 rc 4 / `run_complete=false`。
   父项关闭、优先级与候选验收由负责人按[主清单 T03](README.md#env-t03)判断。

## 已交付与接口变化

### 1. 只读契约校验 helper（本包 `.audit`，一次性）

[verify_shop_contract.py](../../../.audit/dsh-shop-public-contract-20261012/verify_shop_contract.py)：把已录帧按
L1（0.2 `wire.schema.json`）→ L2（`check_catalog`）→ L3（`check_snapshot`）→ L4（`check_control`）逐层过一遍，
再叠加商店公开接口的一致性检查。它**只读**复用 `tools/contract-check/wire_layers.py`，按 0.2 显式覆盖两处旧 0.1 规则
（0.1 的 `I-81` start 结果形状、`I-76` 的规范 uint64 十进制 seed）——覆盖点在运行时打印，避免用旧限制判 0.2：

| 0.1 规则 | 0.2 行为 | 依据 |
| --- | --- | --- |
| `I-81`：`start` 成功结果恰为 `{session_id, snapshot}` | 恰为 `{session_id, seed, snapshot}` | [protocol.md §7 表](../../design/net-the-spire/protocol.md#7-env正式环境的02扩展)、0.2 schema `StartResult` |
| `I-76`：`start.payload.seed` 必须是规范 uint64 十进制字符串 | 非空**游戏 seed 字符串**（`GameSeedString`），由 base 标准化 | 同上；0.2 schema `GameSeedString` |
| 其余 L1/L2/L3/L4（`I-44`/`I-50`/`I-11`/`I-13`/`I-48`/`I-83`/`I-84`/`I-86`/`I-87`/`I-05`/`I-20`/`I-22`/`I-74`/`I-79`/`I-89`/`I-39`/`I-34`/`I-35`/`I-28` 等） | **原样适用**（0.2 只改 §7 列出的四项差异） | protocol.md §7「0.2 沿用第 1～6 节的结构和生命周期」 |

商店专项（全部纯帧派生，不依赖 `steps.ndjson`，也不重判规则）：

- **候选完整性**：每一步提交的 `candidate_id` 必须在该步**前置快照**的 `legal_actions` 里，且
  `expected_observation_id` 等于该快照的 `observation_id`。
- **拒绝步**：被拒绝的 `step` 必须 `ok=false`、**没有快照**、带具名 `code`/`execution_state`/`retry_mode`
  且 `execution_state ∈ {not_started, completed}`（不得是 `unknown`），并且**下一步的前置快照逐字段不变**（拒绝不推进）。
- **十四格库存**：`ShopOffers` 序列 14 个元素、`Slot` 恰为 `0..13` 且按发布顺序递增、元素 `kind_id` 是
  `ShopOffer`、十二个公开字段全部 `known`；`Unsupported=true` 必须有非空 `UnsupportedReason` 与
  `DefinitionId`/`SourceName` 之一（保留源身份）。实测分布 `card×7 / relic×3 / potion×3 / removal×1`。
- **不可买不会被发布成候选**：同一快照里，`Sold=true` 或 `Unsupported=true` 的公开条目**不得**出现在任何
  候选的 `Offer`/实例字段里。
- **买卡永久副本**：公开 LongTermDeck 恰好 +1、新副本 `content_id` == 商品 `DefinitionId`、该实例在**轨迹末尾**
  仍在公开 LongTermDeck 里。
- **金币一次扣除**：单步 `gold` 差恰等于该商品公开 `Price`；进店→离店净变化恰等于所有付费事件之和。
- **删牌**：`RemoveShopCard` 的下一步必须是**同一观察号**上的 `net:answer_decision`；该快照候选必须全部是
  本快照公开 LongTermDeck 实例；answer 选的实例必须是暂停快照里已公开的候选之一；删除后该实例不在
  LongTermDeck 里、牌组恰好 −1、`ShopCardRemovalsUsed` 恰好 +1、金币恰扣一次、删牌位 `Sold=true`。
- **相位 7→2**：`LeaveShop` 后 `MapPhase` 回到 2，快照仍是 `decision`、候选非空且动作都在目录里声明。
- **隐藏范围**：公开快照里不得出现非 `public` 字段或含 `rng/random/seed/debug/hidden/internal` 的已发布字段
  （本轮本构建目录 `visibility` 全为 `public`，非 public 字段数 0）。

### 2. 修复的两个**实际消费者缺口**（唯一生产改动）

两处都在既有消费者里，未新增商店实现、未改 C#/Host/schema/base/tools/AI：

* [test_shop_public.py](../../../net-the-spire/client/test_shop_public.py) `replay` 组此前按**当前默认**策略复算，
  且断言「整局没有任何商店事件」，因此**任何带商店的记录都无法复算**。现在：
  - 新增 `replay_policy_options(summary)`：按**记录里的策略**重建固定规则（`step_frontier` /
    `step_unsupported_room` / `use_potions` / `relic_policy` / `potion_policy` / `shop_room_entry` /
    `shop_policy` / `shop_buy_kind` / `shop_buy_slot` / `shop_remove` / `shop_remove_card` /
    `shop_remove_card_id`）；旧记录没有这些键时取安全默认，**无商店记录行为与改动前一致**。
  - 新增 `check_replayed_shop(recorder, checker, summary)`：记录里没有商店就保持原来的「没有商店事件」断言；
    有商店则核对进入/退出成对、事件顺序、进店公开货架 14 格且 `Slot` 0..13、不可买条目的源身份与原因、
    「买到一件或如实记 `shop_buy_unavailable`」「删一张或如实记 `shop_remove_unavailable`」。
* [run_game.py](../../../net-the-spire/client/run_game.py)：新增 `--probe-rejected-candidate` 与
  `probe_consumed_candidate(...)`。第一次**真实买卡成功后**立即用刚消费掉的旧 `candidate_id` 配**当前**
  `observation_id` 重发一次 `step`，再用一次 `observe`（公开读、不推进）核对 Run 公开字段与 `legal_actions`
  逐字段不变、当前快照仍提供 `LeaveShop`；宿主接受或状态推进立即按故障停止（`fault_consumed_candidate_accepted`
  / `fault_rejected_candidate_advanced` / `fault_no_leave_after_rejection`）。证据写 `rejected-candidate-probe.json`，
  摘要进 `summary.rejected_candidate_probe`（未触发给 `rejected_candidate_probe_note`）。
  **不生成候选、不猜参数**：反例只用当前公开快照里已有的候选与字段。

`public_snapshot.py` **未改**：商店货架/长期牌组/候选都复用既有 `sequence` / `public_fields` / `field` /
`entities_of` / `find_entity` / `candidate_fields`，没有新增共享访问器（本包未发现缺口）。

### 3. 文档

[run_game.md](../../../net-the-spire/client/run_game.md)（新参数行、输出字段、2026-10-12 证据小节）、
[client README](../../../net-the-spire/client/README.md)（`replay` 现在支持商店记录 + 现场反例）、
[公开运行教程 §6](../../design/net-the-spire/how-to-run-public-game.md)（反例的实际用法与预期字段）。

未改：`net_client.py`、任何 C#/Host/协议/schema/`docs/contracts/net/**`、`mod-the-spire/**`、`tools/**`、
`ai-the-spire/**`、主清单。没有构建 base/solution，没有覆盖固定闭包或并发构建 solution，没有
reset/clean/commit/push/tag。

## 验证与证据

固定输入（只读）：Host
`.artifacts/parallel/owner-project-review-20261012/bin/Net.TheSpire.Host/debug/Net.TheSpire.Host.dll`
SHA256 `F8FACE609121821489719CE8215FE94633CCBB38A4642E9EF2DE3787C1B37F68`；schema
`docs/contracts/net/0.2/wire.schema.json` SHA256 `6299696398B5AA14688E432A8097DFF3F177DE68AC28F87128BC01637DEA46AE`；
身份 `protocol_version=0.2` / ruleset `net:ruleset:8ee60bba81741c21` / catalog
`net:catalog:c3034556e5ff196d` / backend `net:backend:simulation-0.2-8c82cc9602c3181a`；
profile `base-spire-singleplayer-dev-map-v1`；scope `dev-map`；seed `ENV-T03-SEED-L`。

| 检查 | 工作目录与实际命令 | 返回码/结果 | 证据与证明范围 |
| --- | --- | --- | --- |
| 帧级契约校验（3 局：两份负责人轨迹 + 本包反例局） | `python -X utf8 -u .audit/dsh-shop-public-contract-20261012/verify_shop_contract.py --verbose --run-dir .audit/owner-project-review-20261012/shop-buy-remove --run-dir .audit/owner-project-review-20261012/shop-remove --run-dir .audit/dsh-shop-public-contract-20261012/runs/shop-buy-probe` | rc **1**；131 request + 131 response 帧、121 快照全部 L1 通过；L2 逐局目录（三局合计 1416 条声明）的引用均有效；`0.2 wire 门槛=0`、`商店公开闭合=0`、`Host 结构声明=126`；拒绝步 1 条已核 | [verify-shop-contract.log](../../../.audit/dsh-shop-public-contract-20261012/verify-shop-contract.log)、[verify-final.json](../../../.audit/dsh-shop-public-contract-20261012/verify-final.json) |
| 商店机制检查（离线，四组 672 项） | `python -X utf8 -u net-the-spire/client/test_shop_public.py` | rc **0**，`passed=672 failed=0` | [test-shop-public.log](../../../.audit/dsh-shop-public-contract-20261012/test-shop-public.log)。与 wave-2 的 672 项同数，未减少断言 |
| 带商店记录整段复算（两局） | `python … test_shop_public.py --only replay --run-dir .audit/owner-project-review-20261012/shop-buy-remove`（45 步）／`…/shop-remove`（46 步） | 各 rc **0**，45/45 与 46/46 步**逐字一致**，0 个旧规则变体步骤 | [test-shop-public-replay.log](../../../.audit/dsh-shop-public-contract-20261012/test-shop-public-replay.log) |
| 非商店记录回归（最小回代） | `python … --only replay --run-dir .audit/dsh-public-run-driver-2026-10-11/runs/seed-K` | rc **0**，315/315 逐字一致（与改动前相同） | 同上日志。**未**重跑 17 条历史轨迹或其它 seed 矩阵 |
| 现场反例（唯一一次真实负对照） | `python -X utf8 -u net-the-spire/client/run_game.py --host-dll <固定 Host> --scope dev-map --seed ENV-T03-SEED-L --output .audit/dsh-shop-public-contract-20261012/runs/shop-buy-probe --max-actions 26 --use-potions --shop-room auto --shop-policy buy --shop-buy-kind card --probe-rejected-candidate` | rc **3**（`step_limit`，不是故障）：第 21 步买下 `base-spire:havoc`（金币 111→63），反例被拒 `illegal_candidate`/`not_started`/`correct_input`，`advanced=false`、`leave_available=true`，第 22 步 `LeaveShop` 成功、`MapPhase 7→2` | [rejected-candidate-probe.json](../../../.audit/dsh-shop-public-contract-20261012/runs/shop-buy-probe/rejected-candidate-probe.json)、[runs/shop-buy-probe](../../../.audit/dsh-shop-public-contract-20261012/runs/shop-buy-probe/)（含完整 `frames.ndjson`） |
| Python 语法 | `python -m py_compile net-the-spire/client/run_game.py net-the-spire/client/test_shop_public.py` | rc **0** | — |
| 文档检查 | `python -X utf8 tools/doc-check/check_docs.py` | rc **4**，扫描 427 份 Markdown，**4 项既有失败**（`dsh-power-fixture-handoff-2026-10-11.md` 与被它引用的 `dsh-startup-alignment-handoff-2026-10-11-f68b943d.md` 的行锚点），本包新增/改动文档 0 问题 | [doc-check.log](../../../.audit/dsh-shop-public-contract-20261012/doc-check.log) |
| 空白/冲突标记 | `git diff --check` | rc **0**（仅覆盖已跟踪文件；本包改动都在未跟踪路径） | — |

**未运行**：`dotnet build AutoTheSpire.sln`、Engine/Slice/net C# 套件、`tools/differential` 严格 17 条、
17 条历史轨迹复算、额外 seed 矩阵、药水套件、AI/训练/性能套件。这些都不是本包范围（授权明确不重跑已通过的
net416/Engine/Slice 与历史轨迹）。

## 差异、阻塞与未验证

**首个差异（唯一 0.2 门槛未过项，Host 侧、本包无写权限）**：
`capabilities.supported_structures[*].declaration_ids` 在 `Entity`（15）/`Relation`（4）/`LegalAction`（21）/
`TerminalResult`（2）上发布的是**机制短名**（`base-spire:Player`、`base-spire:BuyShopCard` 等），
而同一目录里 `scenario_profiles[*].required_declarations` 用的是完整声明 ID（`net:kind:base-spire:Player`），
同一载荷里 `Pile`（5）/`Sequence`（11）也用的是完整 ID。0.1 的 `I-71`/`I-84` 要求
`declaration_ids` 必须是**本 catalog 已声明 ID**，0.2 只改 §7 的四处差异、其余沿用 §1–6，因此这 42 项
（每局）仍有声明不一致。最小复现：

```powershell
python -X utf8 -u .audit/dsh-shop-public-contract-20261012/verify_shop_contract.py --verbose
# 首个定位：
#   [L4/I-84] shop-buy-remove.capabilities.supported_structures[0].declaration_ids:
#             未声明的 declaration_id: 'base-spire:Player'
```

源定位（只读复核，未改）：[ControlHost.cs:169](../../../net-the-spire/Net.TheSpire/Control/ControlHost.cs#L169)（`Entity` = `_profile.Kinds.Keys`）、
[:170](../../../net-the-spire/Net.TheSpire/Control/ControlHost.cs#L170)（`Relation` = `_profile.Relations.Keys`）、
[:182](../../../net-the-spire/Net.TheSpire/Control/ControlHost.cs#L182)（`LegalAction` = `_profile.Actions.Keys`）、
[:183](../../../net-the-spire/Net.TheSpire/Control/ControlHost.cs#L183)（`TerminalResult` = `_profile.Terminals.Keys`）；
对照 [:171–174](../../../net-the-spire/Net.TheSpire/Control/ControlHost.cs#L171-L174)（`Pile`/`Sequence` 用 `PileTypeId`/`SequenceTypeId`）。
net 侧现有断言只覆盖 `Sequence` 计数（[FormalPathChecks.cs:552](../../../net-the-spire/Net.TheSpire.Tests/FormalPathChecks.cs#L552)）与
`Pile` 逐项（[ControlChecks.cs:240](../../../net-the-spire/Net.TheSpire.Tests/ControlChecks.cs#L240)），
所以 `Entity`/`Relation`/`LegalAction`/`TerminalResult` 的短名从未被检查。这是 2026-10-07「公开语义透传」
迁移引入的，不是商店引入的，**不影响**本包商店结论（商店动作/字段/序列在这四处都能逐项对应）。
裁决要么让 Host 发完整声明 ID，要么由负责人修订 `I-71`/`I-84` 在 0.2 下的口径——本包两条路都不做。

**次个差异（本包已修，留作回归对照）**：修复前 `replay` 组复算两份商店轨迹的**首要定位**是
`shop-remove` 第 20 步 `复算停止=unsupported_map_room 记录停止=unsupported_map_room pair=20/46`
（分类相同但位置不对：`shop_room_entry` 没随记录传入），再加第 21/22 步动作不一致与「整局没有任何商店事件」；
修复后 45/45、46/46 逐字一致。

**依赖与未验证**：

* **完整整局未验收**：两局真实轨迹都在 `Unknown` 节点 `rc 4` / `run_complete=false`；`ActCompleted=false`。
  这是地图房型（`Unknown`）的开发边界，不是商店问题，也不是整局通过。
* **未来共同复放由 Codex 处理**：本包证据绑定固定闭包 Host `F8FACE60…`；A 的新排序不改变这份固定构建证据。
  重新集成构建后的同源门槛（构建/Engine/Slice/net/公开整局）仍按主清单串行收口。
* **商店房型本身只覆盖已支持商品**：Source Eternals、未实现遗物效果（公开 `UnsupportedReason` 已如实标注
  「交 owner 裁决」）仍属 base 范围；本包只证明已支持商品，未在客户端补任何规则。
* **未覆盖的真实现场**：满药水栏购买、金币不足购买、二次进店涨价、`BuyShopRelic`/`BuyShopPotion` 的成功路径
  没有现场轨迹（本轮两局只买了一张卡、删了一张牌；删牌位在 `shop-buy-remove` 里因金币 63 < 公开 75
  而**没有** `RemoveShopCard` 候选，客户端如实记 `shop_remove_unavailable`，未自行补候选）。
* **0.2 分层不变量资产缺失**：`docs/contracts/net/` 下只有 0.1 的 `invariants*.json/md`，0.2 只有
  `wire.schema.json`；本包把 0.2 的两处覆盖写在 helper 里并打印，未新增 0.2 不变量资产（那属契约变更，
  交负责人决定是否补 `docs/contracts/net/0.2/invariants*`）。

## 维护位置

* 任务状态/认领/依赖/下一成果：[主清单 T03](README.md#env-t03)（Codex 独自修改；本包未改）。
* 使用方法与 CLI：`net-the-spire/client/run_game.md`、`net-the-spire/client/README.md`、
  `docs/design/net-the-spire/how-to-run-public-game.md` §6。
* 契约与不变量口径：`docs/design/net-the-spire/protocol.md` §7（0.2 差异）、
  `docs/contracts/net/0.1/invariants.md`（现行 I-71/I-84 口径）。
* 本包证据与一次性 helper：`.audit/dsh-shop-public-contract-20261012/`
  （`verify_shop_contract.py` + `verify-shop-contract.log` + `verify-final.json` / `test-shop-public.log` /
  `test-shop-public-replay.log` / `probe-rejected-candidate.log` / `doc-check.log` / `runs/shop-buy-probe/`；
  `probe_catalog.py` / `probe_declaration_ids.py` / `dump_defs.py` / `dump_response_defs.py` /
  `survey_assets.py` 是定位 0.2 schema 条目的**一次性只读调查**，不是门槛检查）。
* 本包消费的负责人轨迹（只读、未改）：`.audit/owner-project-review-20261012/shop-buy-remove/`（45 步）、
  `…/shop-remove/`（46 步）；固定闭包 `…/bin/Net.TheSpire.Host/debug/Net.TheSpire.Host.dll`。
