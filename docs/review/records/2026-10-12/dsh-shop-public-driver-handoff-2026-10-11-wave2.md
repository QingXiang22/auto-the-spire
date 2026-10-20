# ENV-T03 公开整局运行器商店驱动交接 — 2026-10-11 wave 2

本包让现有公开整局运行器**处理商店房间**：走进商店之前先按宿主公开目录判断是否接线、进店后能在
实时公开候选上买一件/删一张/安全离店，并把删牌 decision 与战斗目标、营火升级区分开。**真实公开商店
轨迹仍缺**：本包没拿到"商店已接线"的同构建 Host，因此进店后的现场证据依赖 ENV-T09 最终 Host。

- task_id: `ENV-T03-shop-public-driver-20261011`
- dispatch_id（= 派发记录 task_id）: `ENV-T03-shop-public-driver-20261011`
- session_id: `session-a1811025-b9e4-44d0-a2f2-2f7949935ade`
- request_id: `e1cc6a92-f19b-4d6c-85e9-9813ba5b4851`
- dispatch_record: [.audit/dsh-shop-public-driver-20261011/task-record.json](../../../../.audit/dsh-shop-public-driver-20261011/task-record.json)（`status: accepted`）
- delivery_status: `ready_for_review`（客户端生产实现与离线/固定 Host 证据已交；真实商店公开轨迹按 §5 依赖 ENV-T09，未验证）
- updated_at: `2026-10-11T23:52:50+08:00`
- 主清单对应项：[T03](../../tofix/README.md#env-t03)（本包不修改主清单，状态与认领由 Codex 单写）

接手第一步：读[证据索引](../../../../.audit/dsh-shop-public-driver-20261011/validation.md)（固定输入
SHA/身份、逐条命令与返回码、2340 步复算结果、固定 Host 边界），再重跑
`python -X utf8 -u net-the-spire/client/test_shop_public.py`（离线，rc 0）与
`python -X utf8 -u .audit/dsh-shop-public-driver-20261011/tools/compare-runs.py` 复核 §3 的 A/B 结论。

## 1. 交付内容与接口变化

唯一生产文件是 [run_game.py](../../../../net-the-spire/client/run_game.py)（`FILE_VERSION =
run_game.py/2026-10-11-shop`），同目录新增唯一机制检查
[test_shop_public.py](../../../../net-the-spire/client/test_shop_public.py)。新增能力：

1. **商店分支**（决策顺序插在地图选点之前、幕完成边界之后）：公开 `Run.MapPhase=7` 或候选里出现
   商店动作时，按显式 CLI 走 **买一件（`--shop-policy=buy`）→ 删一张（`--shop-remove`）→
   `LeaveShop`**。默认 `--shop-policy=leave`：进店即安全离店。
2. **只消费公开契约**：动作名 `BuyShopCard`/`BuyShopRelic`/`BuyShopPotion`/`RemoveShopCard`/
   `LeaveShop`（与 base 侧 `ShopRoomWiring` 的公开常量一致），货架读公开 `ShopOffers` 序列，
   商品事实只读候选自带的 `Offer` 实体公开字段
   （`Slot`/`Kind`/`DefinitionId`/`SourceName`/`Rarity`/`OnSale`/`Price`/`Sold`/`Unsupported`/`UnsupportedReason`）。
   买不起、满药水栏、已售、不可买**都由 Engine 的候选域与守卫决定**，客户端不重算价格/容量/可删性。
3. **确定性挑选**：在候选里跳过 `Sold=true`、`Unsupported=true`、必需字段未公开的条目（各留可复查
   原因），其余按 `(公开 Price, 公开 Slot, 公开顺序)` 最小者挑唯一一个；一次访问最多买一件、最多删一张，
   不重复购买已买到手或售罄的商品。可选 `--shop-buy-slot N` 指定公开 Slot。
4. **删牌 decision 与其它选择分开**：提交 `RemoveShopCard` 后 Engine 会暂停在 `EntityRef` decision 上，
   候选是公开 `LongTermDeck` 实例。只有"刚提交过删牌"这个内部标记会走删牌分支，并按
   `--shop-remove-card N`（候选下标）/`--shop-remove-card-id ID`（公开卡 id 短名，可省略 `-` 后缀）
   选**具体长期实例**；候选域不是公开长期牌组时判 `shop_removal_decision_domain_mismatch`，绝不把它当
   战斗目标或营火升级处理。其它 decision（战斗目标、手牌选择）规则**未改**。
5. **进店许可 `--shop-room {auto,enter,skip}`**（默认 `auto`）：`auto` 在**进入地图节点之前**读宿主
   公开目录 `get_catalog` 的 `declarations.action_types`，五个商店动作都声明才把 `Shop` 当已接线房型；
   声明不全或目录失败就**不进店**、保持原来的 `unsupported_map_room` 边界（理由写进
   `summary.shop_room_entry.note`，不猜、不降级）。`enter` 强制进店（用于对未接线宿主取回具名失败），
   `skip` 从不进店。三种模式都会把目录声明记进 `summary.shop_catalog`。
6. **证据字段**：`steps.ndjson` 的实时公开状态视图新增 `long_term_deck`、`shop_offers`、
   `shop_offers_published`；`summary.json` 新增 `shop_catalog`、`shop_room_entry`、`shop_events[]`
   （`shop_enter` 带整条公开货架、`shop_buy`、`shop_remove_request`、`shop_remove_answer`、
   `shop_leave`、`shop_exit`、以及买/删不可用的具名原因）、`shop_shelf_final`。分配、退出码语义、
   `run_complete` 规则**未变**。

未修改：`net_client.py`、`public_snapshot.py`、`test_potion_host.py`、任何 C#/Host/协议/schema、
`mod-the-spire/**`（含 A 正在收口的商店与组包器）、`tools/**`（B 线）、`ai-the-spire/**`、主清单。
没有构建 base/solution，没有复制或覆盖任何构建产物，没有 reset/clean/commit/push/tag。

文档：[run_game.md](../../../../net-the-spire/client/run_game.md)（CLI 表、决策顺序第 4 步、输出、
边界、两波证据）、[client README](../../../../net-the-spire/client/README.md)（新的"公开商店驱动检查"一节）、
[公开运行教程第 6 节](../../../design/net-the-spire/how-to-run-public-game.md)（商店的实际用法与只读快照读法）。

## 2. 验证与证据

固定输入：Codex 固定 Host `.artifacts/parallel/owner-flow-review-next-20261011/bin/OwnerHost/debug/Net.TheSpire.Host.dll`，
SHA256 `78F2CDF57B5D04CDE0FE814933B21ABB041BC9024A41BC4AED285E2F55462B0A`，
身份 ruleset `net:ruleset:4fa58ee1b2f76e3f` / catalog `net:catalog:b400d7f332f590cc` /
backend `net:backend:simulation-0.2-350911d86bad900d` / protocol `0.2` / profile
`base-spire-singleplayer-dev-map-v1` / `build=7fda0b085ec671b8`。scope 一律 `dev-map`。

| 检查 | 命令 | 返回码 / 结果 | 证明范围 |
| --- | --- | --- | --- |
| 商店机制检查（政策/删牌/进店/复算四组） | `python -X utf8 -u net-the-spire/client/test_shop_public.py` | rc 0，`passed=672 failed=0` | 合成公开快照上的店内固定规则、删牌 decision 区分、进店许可判定；**不含**真实商店 Host |
| wave-1 记录逐快照复算 | `python ... test_shop_public.py --only replay --run-dir .audit/dsh-public-run-driver-2026-10-11/runs/<RUN>`（17 个 run） | 17/17 rc 0；2340 步全部候选闭合，1844 步与记录动作+`candidate_id` 逐字一致 | 改动没有移动旧战斗/奖励/地图/营火/宝箱/药水/decision 分支（含 seed-K 315 步、seed-K-repeat 315 步） |
| 固定 Host 正常前缀（改动前/后 A/B，同一 Host+seed+scope） | `python -X utf8 -u net-the-spire/client/run_game.py --scope dev-map --seed ENV-T03-SEED-L --output <基线/新> --max-actions 80 --use-potions` | 两者 rc 4、19 步、`unsupported_map_room`；`compare-runs.py` 报 `语义差异=0 仅句柄不同=19` | 新脚本在固定 Host 上与原脚本**逐步同轨迹**；句柄差异是会话绑定（跨会话预期） |
| 固定 Host 正常前缀（更长） | 同命令，seed `K`/`J`/`E`/`M` | K 127 步、J 94 步、E 105 步后撞**宿主内容缺口**；M 46 步 `unsupported_map_room` | 战斗、奖励、旅行相位走通；本 Host 上 treasure/rest/shop 相位不可达 |
| 强制进店负对照 | 同命令加 `--shop-room enter --shop-policy buy --shop-remove`（seed L） | rc 4，`fault_host_rejected`，宿主 `backend_fault`：`房间类型 商店 尚未实现`，19 步，`run_complete=false` | 进店通道真实可用；商店在该 Host **未接线**，**没有**伪造成通过 |
| 旧 Host（`805C123D…`）记录的分支分布 | `python ... tools/branch-coverage.py` | treasure=4、campfire=10、reward=194、map=56、decision=750、combat=500（当前规则）等 | 复算覆盖到宝箱/营火决策，不只是战斗 |
| Python 语法 | `python -m py_compile net-the-spire/client/run_game.py` | rc 0 | — |

**未运行**：`dotnet build`、Engine/Slice/net C# 套件、`tools` 冒烟工具、药水套件、任何训练/性能套件。
逐条命令、SHA、返回码与原始产物见[证据索引](../../../../.audit/dsh-shop-public-driver-20261011/validation.md)，
原始帧在 `.audit/dsh-shop-public-driver-20261011/runs/<RUN>/{frames,steps}.ndjson`。

## 3. 首差与阻塞

* **改动本身没有首差**：固定 Host 上改动前后的 seed-L 前缀逐步同轨迹（只有会话内句柄不同）；
  wave-1 记录里当前规则写明的步骤全部逐字复现。
* **复算时发现的旧记录首差**（记录自报是 wave-1 试过的旧规则变体，不是本包回归）：
  `seed-B` step 9 记录选 `strike`（旧"同类里费用低者先"）而当前规则选 `bash`；`seed-G` step 133
  记录选 `defend`（旧"低血先格挡"）；`act1-potions-A3` step 73 在 Hp 66/80 记录用药水（旧无门槛药水规则）；
  `smoke-6`/`act1-full-A` step 1 记录按旧"只挑房型偏好"选了另一个 `Monster` 节点。
  核对时按 `reason` 前缀区分（`test_shop_public.py` 的 `LEGACY_REASON_PREFIXES`），这些步骤只核对
  候选闭合与记录自洽。
* **固定 Host 的内容缺口**（非商店、非驱动器）：`seed-K` 127 步后 `StartNextRoom` 报
  `missing enemy behavior base-spire:fogmog`，`seed-J`/`seed-E` 报 `base-spire:cubex-construct`，
  都在 `MapPhase=2` 推下一房间时发生，退出码 4、`run_complete=false`。受影响范围是"本 Host 上跑不出
  更长前缀"，不影响商店分支本身；补内容由 A/负责人排期。
* **依赖 ENV-T09 最终 Host**：真实商店轨迹（进店 → 公开商品选择/删牌 decision → 离店 → 回地图）
  的证据缺失在"商店已接线的同构建 Host"这一侧，本包**不循环等待**、不自建 base。

## 4. 接手后先做什么

1. 复核本包已交付部分（§1）与离线检查（§2 第一条），避免重做商店分支或重复药水/整局套件。
2. 拿到 ENV-T09 最终 Host（`ShopRoomWiring` 已接线、目录声明五个商店动作）后，用**同一条**脚本取证：

   ```powershell
   $env:NET_THE_SPIRE_HOST_DLL='<最终 Host.dll>'
   python -X utf8 -u net-the-spire/client/run_game.py --scope dev-map --seed <SEED> `
     --output .audit/<新目录>/runs/shop-buy-remove --max-actions 400 --use-potions `
     --shop-room auto --shop-policy buy --shop-buy-kind card --shop-remove --shop-remove-card-id strike
   ```

   预期：`summary.shop_room_entry.enter_shop_nodes=true`（目录已声明）、`shop_events` 依次出现
   `shop_enter`（含整条公开货架）→ `shop_buy` → `shop_remove_request` → `shop_remove_answer` →
   `shop_leave`，`rooms[]` 里能看到 `MapPhase=7` 与回到旅行相位的长期状态（金币、长期牌堆、
   `ShopCardRemovalsUsed`），并保留 `frames.ndjson` 原始帧。失败时按 `stop.reason` 分流：宿主拒绝
   （`fault_host_rejected`）是集成/内容问题，`shop_no_decision` 等是本驱动器的具名边界。
3. 换 Host/seed 只改命令行；`test_shop_public.py --only replay --run-dir <新 run>` 可直接把新轨迹
   当成"当前规则记录"复算。
4. 复核通过后由 Codex 更新[主清单 T03](../../tofix/README.md#env-t03)行；若届时判定需要把
   `--shop-room` 默认改成 `enter`，那是一行默认值变更（`run_game.py` 的 `build_parser`），
   由文件主责在本包外决定。

## 5. 未验证范围

* 真实商店 Host 上的购买/删牌/离店现场证据（依赖 ENV-T09 最终 Host）。
* `ActCompleted`/幕转换、treasure/rest/shop 相位在固定 Host 上的实际到达（离线复算里
  seed-K/E/J 的 treasure/campfire 决策已按当前规则逐字复现）。
* 满药水栏购买、金币不足购买、删牌涨价（第二次进店）等 Engine 守卫拒绝的真实现场证据；本包只用
  合成快照核对客户端的跳过与留证行为，规则本身由 A 的候选域与守卫负责。
* 本包不是通关能力声明，也不是商店机制的规则验证：商店规则由 A（`ShopRoomWiring`）负责，
  公开契约最终以同构建 Host 的实际快照为准。

## 6. 只读契约复核与文档检查

* **A 侧公开契约只读复核**（不改 A 的文件）：`mod-the-spire/base-spire/BaseSpire/Rooms/Shop/ShopRoomWiring.cs`
  在本包工作期间被 A 持续编辑（复核时 SHA256 `9EE5E0924FC50E31B46183E221773DAC376BB012C72E8111F4F946AEB4D83847`）。
  本包消费的符号未变：五个动作名、`ShopOffers`、`ShopCardRemovalsUsed`、`ShopOffer` 的 12 个公开字段、
  删牌用 `DecisionResponseKind.EntityRef`；`BuyableOffers`（候选域）与 `BuyGuard`（守卫）仍是同一组条件
  （种类 / 未售 / 非 `Unsupported` / 金币足够 / 药水栏有位置），因此"候选就是当前可买的"这一前提成立。
* **并行改动观察**：`mod-the-spire/base-spire/workpacks/shop-flow/*.cs` 已被 A 改名为 `*.cs.frozen-20261011`，
  于是 [`dsh-shop-flow-handoff-2026-10-11.md`](../2026-10-11/dsh-shop-flow-handoff-2026-10-11.md)
  里两个 workpack 文件链接失效（`doc-check` 会报）。本包未改这些文件，仅记录，交负责人/文件主责处理。
* 文档检查：`python -X utf8 tools/doc-check/check_docs.py` → 扫描 421 个 Markdown，报 **6 个既有失败**
  （上述 2 个 workpack 链接 + `dsh-power-fixture-handoff-2026-10-11.md`、
  `dsh-startup-alignment-handoff-2026-10-11-f68b943d.md` 的行锚点链接）；本包新增/改动的 5 份文档无问题。
  `git diff --check` → rc 0（本包改动都在未跟踪目录，该命令只覆盖已跟踪文件）。

## 7. 维护位置

* 任务状态/认领/依赖：[主清单 T03](../../tofix/README.md#env-t03)（Codex 单写；本包未改）。
* 方法与用法：`run_game.md`、`client/README.md`、教程第 6 节。
* 本包证据与长日志：`.audit/dsh-shop-public-driver-20261011/`（`validation.md` 是索引，
  `runs/` 是原始帧，`tools/` 是只读探针脚本）。
* 历史：wave-1 交接 [dsh-public-run-driver-handoff-2026-10-11.md](../2026-10-11/dsh-public-run-driver-handoff-2026-10-11.md)
  与 `.audit/dsh-public-run-driver-2026-10-11/`（旧 Host `805C123D…` 的记录，本包按 §3 复算，未改写）。
