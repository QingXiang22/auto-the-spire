# 公开整局运行器 `run_game.py`

编写新策略前可先运行[最小客户端教程](../../docs/design/net-the-spire/how-to-run-public-game.md)。本运行器的实体、字段、集合、牌堆和地图关系访问复用[public_snapshot.py](public_snapshot.py)，调用签名见[客户端参考](../../docs/design/net-the-spire/public-client-reference.md)。

`net-the-spire/client/run_game.py` 用**公开 NDJSON 协议**（`NetClient`）把一整局 Spire 从正常
`start` 推到**整局结论**（Victory / Death / 其它终局）或**明确的开发边界**。它替代"每次真实
初始化或房间变化后手工重做旧 `candidate_id` 回放"的做法：候选一律从当前快照的公开
`legal_actions` 里现场挑，不写死任何 `candidate_id`、不写死任何 seed 的脚本。

它**不是** AI，也**不是**搜索器：没有评估函数、没有多 seed 搜索、不推断隐藏牌堆或随机数、
不读 Engine 内部状态。所有输入都来自宿主公开的 `describe` / `get_catalog` / `start` / `step`
与实时快照字段。

商店（`MapPhase=7`）同样只消费公开契约：`ShopOffers` 序列上的五个公开动作
（`BuyShopCard` / `BuyShopRelic` / `BuyShopPotion` / `RemoveShopCard` / `LeaveShop`）与每个候选
自带的 `Offer` 实体公开字段。买不买得起、能不能删、有没有容量都由 Engine 的**候选域与守卫**决定，
客户端不重判规则、只按公开 `Price`/`Slot` 做确定性挑选；商品字段缺失时记录可复查原因并停下，
不猜值、不清空货架。

## 快速开始

```powershell
# 只读的不可变公开 Host（本包验证所用，SHA256 805C123D9941454E0B15E704CD48EB107A2E2A26DCC4555DDE8617513D0C1F6D）
python -X utf8 -u net-the-spire/client/run_game.py `
  --host-dll .artifacts/parallel/dsh-core-author-04/bin/Net.TheSpire.Host/debug/Net.TheSpire.Host.dll `
  --scope dev-map --seed ACT1-BASIC-10 `
  --output .audit/dsh-public-run-driver-2026-10-11/runs/example `
  --max-actions 600 --use-potions
```

`--host-dll` 可省略：省略时沿用 `NetClient.host_command()` 的既有约定（优先环境变量
`NET_THE_SPIRE_HOST_DLL`，否则用仓库内默认宿主命令）。`-u` 建议保留，否则进度打印会被缓冲。

## 命令行参数

| 参数 | 默认 | 说明 |
| --- | --- | --- |
| `--host-dll PATH` | 无 | 指定 `Net.TheSpire.Host.dll`，仅设置 `NET_THE_SPIRE_HOST_DLL`，不改 `net_client.py` |
| `--scope NAME` | `dev-map` | 传给宿主的 `--scope`（`formal-dev` 只认 `dev` / `dev-map`，未知名字宿主自己拒绝） |
| `--seed TEXT` | 必填 | **原样**作为字符串发送（驱动器不做任何 seed 标准化/推导） |
| `--output DIR` | 必填 | 输出目录（不存在则创建） |
| `--max-actions N` | 400 | 步数上限；到达即 `step_limit`（退出码 3，绝不算整局通过） |
| `--scenario NAME` | `formal-dev` | 宿主场景名 |
| `--profile ID` | 无 | scenario profile；默认取 `describe` 返回的第一个 |
| `--timeout SEC` | 60 | 单次请求超时 |
| `--frame-limit N` | 无 | 转发给宿主（宿主帧上限） |
| `--relic-policy {skip,take}` | `skip` | 宝箱遗物：默认跳过（跳过一定成功；拿取可能被未支持遗物拒绝） |
| `--potion-policy {take,skip}` | `take` | 奖励药水是否收下 |
| `--use-potions` | 关 | 战斗中允许用药水；且只在公开 Hp 不高于一半时使用 |
| `--step-frontier` | 关 | 允许实地推进 `ContinueToNextAct`（幕转换）；默认停在幕完成边界，不推进 |
| `--step-unsupported-room` | 关 | 默认遇到未接线房型**不推进**；打开后实地推进一次以取得宿主的具名失败 |
| `--shop-room {auto,enter,skip}` | `auto` | 地图是否走进 `Shop` 节点：`auto` 读宿主公开目录（`get_catalog`）声明的商店动作，五个都声明才进店；`enter` 强制进店；`skip` 从不进店 |
| `--shop-policy {leave,buy}` | `leave` | 店内动作：`leave` 进店后直接 `LeaveShop`（安全）；`buy` 先按下面的类别买一件，再（可选）删一张，然后离店 |
| `--shop-buy-kind {card,relic,potion}` | `card` | `--shop-policy=buy` 买哪一类（映射到公开动作 `BuyShopCard`/`BuyShopRelic`/`BuyShopPotion`） |
| `--shop-buy-slot N` | 无 | 只买公开 `Slot` 等于 N 的商品；不传则按公开 `(Price, Slot)` 最小者挑一件 |
| `--shop-remove` | 关 | 在店里删一张：提交公开 `RemoveShopCard`，再回答随后的删牌 decision |
| `--shop-remove-card N` | `0` | 删牌 decision 里要选的公开长期牌候选下标（0 = 宿主公开顺序第一张） |
| `--shop-remove-card-id ID` | 无 | 按公开卡 id 选要删的那张长期牌实例（短名匹配，可省略 `-` 后缀，如 `strike` 匹配 `strike-ironclad`）；优先于 `--shop-remove-card` |
| `--event-choice TEXT` | 无 | 当前事件按完整公开 `action_type` 精确匹配，或按不含冒号的短名唯一后缀匹配；缺失/歧义退出码4，不选替代项 |
| `--stall-limit N` | 30 | 连续 N 步"动作名 + 公开字段"完全相同即判 `stalled`（退出码 4） |
| `--probe-stale-observation` | 关 | 额外做一次负对照：用上一步的 `observation_id` / `candidate_id` 重发，验证宿主拒绝过期观测 |
| `--probe-rejected-candidate` | 关 | 额外做一次负对照：**第一次真实买卡后**，用刚消费掉的旧 `candidate_id` 配**当前** `observation_id` 重发，验证宿主拒绝、拒绝不推进公开状态、且当前快照仍提供 `LeaveShop`（需能买到卡，一般配 `--shop-policy=buy`）。宿主接受或状态推进即按故障停止 |

## 固定决策顺序

每一步都从**当前**快照的 `legal_actions` 里按下面的固定顺序取第一个可判定的候选；判不出就
**精确停在未支持边界**（退出码 4，附全部未知 action / 字段 JSON），绝不猜、绝不复用旧观测。

1. **整局终局**：`status=terminal` → 记录 `terminal_result`，退出码 0。
2. **pending 选择**：候选只有 `net:answer_decision` 时先回答。
   - 刚提交过公开 `RemoveShopCard`（内部 `shop_removal_pending`）时，候选必须**全部**是公开
     `LongTermDeck` 实例，否则判 `shop_removal_decision_domain_mismatch`；挑哪一张由
     `--shop-remove-card` / `--shop-remove-card-id` 决定（具体长期实例，理由里写出公开牌组）。
   - 其它选择保持原规则：目标是公开怪兽（`net:kind:base-spire:Monster`）时选**公开 Hp 最低**者，
     同 Hp 保持宿主给的公开顺序；其它目标保持公开顺序（手牌选择、战斗目标都走这条）。
3. **幕完成边界**：公开 `Run.ActCompleted=true`（`MapPhase==6`）→ 默认 `act_completed_dev_boundary`
   （退出码 3，`run_complete=false`，**不是整局胜利**）；只有 `--step-frontier` 才推 `ContinueToNextAct`。
4. **普通事件**：仅在公开 `MapPhase=8` 且 `CurrentEventId` 非空时选择。读取全部当前候选，只排除既有策略明确不主动执行的完整公开动作 `net:action:base-spire:DiscardPotion`。其余未知动作保留为选项，不根据事件ID、动作名前缀或内容支持性过滤。默认按 `(完整 action_type, sort_keys=True 的公开 fields JSON, 当前发布顺序)` 稳定排序；`--event-choice` 必须唯一命中。混入已知地图/战斗/商店等非事件动作时具名停止，不当作事件执行。中途 decision 仍走第2步。
   - 相位与当前事件ID不一致：`event_access_inconsistent`；无选项：`event_no_options`。
   - 显式选择缺失/歧义：`event_choice_not_found` / `event_choice_ambiguous`，退出码4，不回退。
   - 公开元数据没有选项所属事件标签。客户端信任Host当前候选域；无法独立确认任意未知动作属于哪个事件，因此不宣称任意mod的选项归属校验。异常已知跨房间动作以 `event_candidate_domain_mismatch` 停止。
5. **商店**（公开 `MapPhase==7` 或候选里出现商店动作）：按显式 CLI 处理，顺序固定为
   **买一件（`--shop-policy=buy`）→ 删一张（`--shop-remove`）→ `LeaveShop`**。
   - 挑商品：只用候选自带的 `Offer` 实体公开字段，跳过 `Sold=true` / `Unsupported=true` / 必需字段
     未公开（`Slot`/`Kind`/`Price`/`Sold`/`Unsupported`）的候选并留下可复查原因；在剩下的里面按
     `(公开 Price, 公开 Slot, 公开顺序)` 最小者挑唯一一个。买不起、满药水栏、已售、不可买都由 Engine
     的候选域与守卫决定，客户端不重判。
   - 一次访问最多买一件、最多删一张；买过就转删牌/离店，不会重复购买售罄或已买到手的商品。
   - 进店时把公开货架（`ShopOffers` 序列的每个商品及其全部公开字段）写进 `shop_events`；货架序列
     为空但相位是 7 时如实记录"没有元素"，不猜、不清空。
   - 相位 7 但既没有可执行的显式策略也没有 `LeaveShop` → `shop_no_decision`（退出码 4）。
6. **地图选点**（`ChooseMapPoint`）：只用公开地图（`relations` 里的 `MapChildren` 边 + `MapNode.Type`）。
   候选排序键 `(是否仍能连通到 Boss, 房型偏好, 公开顺序)`：
   - "仍能连通到 Boss"= 从该节点出发只穿过**已接线房型**能到达 `Boss`（DFS，纯公开图）；
   - 房型偏好 `Monster < Boss < Treasure < RestSite/Campfire`，低血（`Hp*2 < MaxHp`）时营火最优先；
   - 已消费地图类型 = `Monster, Boss, Treasure, RestSite, Campfire, Rest, Unknown, Elite`（不保证Host对应内容完整）；`--shop-room` 允许进店时
     追加 `Shop`（排在最后，因此有别的可走房型时不会专门拐进商店）；
   - 仅当**所有**候选都落在未接线房型（例如 `Ancient`，或商店未获许可时的 `Shop`）时判
     `unsupported_map_room`（退出码 4，地图节点保留、状态不变）。
7. **奖励**：`TakeRewardGold` → `TakeRewardCard` → `TakeRewardPotion`（`--potion-policy=take`）→
   `SkipRewardPotion` → `SkipRewardCard`。
8. **宝箱**：`OpenTreasureChest` → `--relic-policy=skip` 时 `SkipTreasureRelic`，`take` 时试 `TakeTreasureRelic`。
9. **营火**：`Hp < MaxHp` → `RestAtCampfire`；满血且有 `Upgraded=false` 的长期牌 → `SmithAtCampfire`。
10. **开发切片推进**：`UpgradeLongTermCard`（选公开顺序第一张未升级长期牌）/ `EnterNextRoom`。
11. **战斗**：`--use-potions` 且公开 `Hp*2 <= MaxHp` 时先用公开 `UseSupported=true` 的药水；否则
    打 `PlayCard`——只挑公开 `Unplayable=false` 且 `Cost+CostModifier <= Player.Energy` 的牌，
    排序 `(Kind 排名 Attack<Skill<Power, 公开 Card.Amount 从大到小, 有效费用, 字段是否全 known, 公开顺序)`；
    没有可出的牌 → `EndTurn`。
12. **其它**：`Stop("unsupported_action", …)`，退出码 4。

这套顺序里没有任何逐张牌白名单、没有按 seed/scope 分支、没有隐藏信息。

## 输出

`--output` 目录下三个文件：

* **`frames.ndjson`** —— 与宿主的**全部双向原始帧**，逐行 `{"seq", "dir", "frame"}`，
  `dir` 为 `client->host` / `host->client`。含每一步的完整 snapshot，可离线复算。
* **`steps.ndjson`** —— 每一步一行：语义操作（步号、观察 id、动作名、`candidate_id`、reason）+
  该步之后的**实时公开状态视图**（`run` / `player` / `monsters` / `hand` / `potion_belt` /
  `reward_potions` / `long_term_deck` / `shop_offers` / `shop_offers_published` / `piles` /
  `legal_actions` 摘要）。
* **`summary.json`** —— 最终结论，关键字段：
  - `status` / `exit_code` / `run_complete` / `whole_run_outcome` / `stop{reason,detail}`；
  - `identity` 与 `identity_history`（`describe` 与时序上的身份，含会话建立点）；
  - `seed_requested` / `seed_accepted` / `scope` / `scenario_profile` / `session_id`；
  - `step_count` / `observation_ids` / `candidate_ids`（本局实际用过的观察与候选）；
  - `initial_long_term_state` 与 `rooms[]`（按 `room_key` 变化记录的**各房间长期状态**：
    `Run.Hp/MaxHp/Gold/RoomCount/CurrentRoom/EncounterId/MapPhase`、长期牌堆、遗物、药水带）；
  - 商店：`shop_catalog`（宿主目录声明的 `action_types` 数、已声明的商店动作、缺哪些）、
    `shop_room_entry{mode,enter_shop_nodes,note}`（进店许可的实际判定与理由）、
    `shop_events[]`（`shop_enter` 带整条公开货架、`shop_buy`、`shop_remove_request`、
    `shop_remove_answer`（含公开长期牌组与选中实例）、`shop_leave`、`shop_exit`、
    以及"买/删不可用"的具名原因）、`shop_shelf_final`（结束时的公开货架）；
  - `act_completed_observed`（是否真的观察到 ActCompleted）、`terminal_result`（真实终局）；
  - `host.dll` + `host.dll_sha256`、`host_stderr`（宿主诊断）、`frame_diagnostics`、
    `stale_observation_probe`、`rejected_candidate_probe`（如开启负对照；未触发时给
    `rejected_candidate_probe_note`）、`close_ok`、`elapsed_seconds`、`policy`、
    `tool` / `version` / `files` / `max_actions`。

开启 `--probe-rejected-candidate` 时，反例的原始响应、公开事实前后对比与"仍可离店"的当前候选还会另写一份
`rejected-candidate-probe.json`（`response_ok=false` 是预期；`no_advance.advanced=false`、
`leave_available_after_rejection.available=true` 是必须）。

## 退出码与"不得算通过"的约定

| 退出码 | 含义 |
| --- | --- |
| `0` | 拿到**整局**终局：`victory` / `death` / `terminal_other` |
| `1` | 驱动器自身问题（参数、身份校验、协议信封） |
| `3` | **未到整局结论**：`act_completed_dev_boundary`（幕完成）或 `step_limit`（步数上限） |
| `4` | 故障 / 未支持边界：`unsupported_action` / `unsupported_map_room` / `no_legal_actions` / `host_rejected` / `stalled` / `uncertain` |

`summary.run_complete` 只有在退出码 0 且拿到 `win`/`loss`/`other` 终局时为 `true`。**幕完成、
步数上限、故障一律不写成整局通过**；第一幕过关（`ActCompleted=true`）永远只是
`act_completed_dev_boundary`。

## 实跑证据（不可变公开 Host，只读）

### 2026-10-11 wave-1（旧 Host `805C123D…`，本表是当时的原始记录）

命令统一为：

```powershell
python -X utf8 -u net-the-spire/client/run_game.py `
  --host-dll .artifacts/parallel/dsh-core-author-04/bin/Net.TheSpire.Host/debug/Net.TheSpire.Host.dll `
  --scope dev-map --seed <SEED> --output <DIR> --max-actions 700 --use-potions
```

| 运行 | seed | 结果 | 退出码 | 说明 |
| --- | --- | --- | --- | --- |
| `.audit/dsh-public-run-driver-2026-10-11/runs/act1-full-A2` | `ACT1-BASIC-10` | 179 步 `death`（floor 7 `snapping-jaxfruit-normal`，hp 8→0） | 0 | 真实整局终局 |
| `.audit/dsh-public-run-driver-2026-10-11/runs/seed-B` | `ENV-T03-SEED-B` | 138 步 `death`（floor 6，`Defeat.Turn=5`） | 0 | 真实整局终局 |
| `.audit/dsh-public-run-driver-2026-10-11/runs/seed-G` | `ENV-T03-SEED-G` | 182 步 `death`（floor 8） | 0 | 真实整局终局 |
| `.audit/dsh-public-run-driver-2026-10-11/runs/seed-K` | `ENV-T03-SEED-K` | **315 步**推进到 **Boss 房**（`vantom-boss`，floor 17，room 12）后 `death` | 0 | **最深的真实轨迹**：地图选点、战斗、奖励、宝箱、营火全走通；`Defeat.Turn=6` |
| `.audit/dsh-public-run-driver-2026-10-11/runs/seed-K-repeat` | `ENV-T03-SEED-K` | 与 `seed-K` **逐行相同**（315 个 action / `candidate_id` / `observation_id` 全等） | 0 | 可复现性证据 |
| `.audit/dsh-public-run-driver-2026-10-11/runs/seed-E` | `ENV-T03-SEED-E` | 240 步，floor 13 hp 70/80，止于只提供 `Unknown` 的地图节点 | 4 | **开发边界**（事件房未接线） |
| `.audit/dsh-public-run-driver-2026-10-11/runs/smoke-6` | `ACT1-BASIC-10` | 6 步后 `step_limit` | 3 | 步数上限不算通过 |
| `.audit/dsh-public-run-driver-2026-10-11/runs/act1-full-A` | `ACT1-BASIC-10` | 51 步 `host_rejected`（`backend_fault`：商店房未实现） | 4 | 故障不算通过 |
| `.audit/dsh-public-run-driver-2026-10-11/runs/seed-J` | `ENV-T03-SEED-J` | 175 步止于只提供 `Elite` 的地图节点 | 4 | 开发边界（精英房未接线） |
| `.audit/dsh-public-run-driver-2026-10-11/runs/seed-L` | `ENV-T03-SEED-L` | 17 步止于只提供 `Shop` 的地图节点 | 4 | 开发边界（商店房未接线） |
| `.audit/dsh-public-run-driver-2026-10-11/runs/seed-M` | `ENV-T03-SEED-M` | 46 步止于只提供 `Shop` 的地图节点 | 4 | 开发边界（商店房未接线） |
| `.audit/dsh-public-run-driver-2026-10-11/runs/seed-F` | `ENV-T03-SEED-F` | 78 步止于只提供 `Unknown` 的地图节点 | 4 | 开发边界（事件房未接线） |
| `.audit/dsh-public-run-driver-2026-10-11/runs/seed-I` | `ENV-T03-SEED-I` | 14 步止于只提供 `Unknown` 的地图节点 | 4 | 开发边界（事件房未接线） |
| `.audit/dsh-public-run-driver-2026-10-11/runs/stale-observation-*` | `ACT1-BASIC-10` | 过期观测负对照：宿主回 `stale_observation`（`not_started`） | 4 | 换候选必须用当前观测 |

`ActCompleted`（`MapPhase==6`）分支**未在实跑中到达**：本机固定策略目前要么在幕 1 末段战斗死亡，
要么先撞上"只提供 `Unknown` 事件房"的开发边界。该分支的行为是代码与协议层结论（`--step-frontier`
通路），**如实记为未覆盖**。

### 2026-10-11 wave-2（商店支持，Codex 固定 Host `78F2CDF5…`）

商店支持（本文件的 `--shop-*` 选项与第 4 步）在
[wave-2 证据索引](../../.audit/dsh-shop-public-driver-20261011/validation.md)里带完整证据
（原始帧在 `.audit/dsh-shop-public-driver-20261011/runs/<RUN>/`）：

* **离线策略检查** `python -X utf8 -u net-the-spire/client/test_shop_public.py`（672 项断言，rc 0）：
  合成公开快照上的店内规则 + 删牌 decision 区分 + 进店许可判定 + 对 wave-1 记录的**逐快照复算**。
* **固定 Host 上的正常前缀**（`--host-dll .artifacts/parallel/owner-flow-review-next-20261011/bin/OwnerHost/debug/Net.TheSpire.Host.dll`）：
  该 Host 的公开目录**没有声明任何商店动作**，因此 `--shop-room auto` 判定"不进店"，`seed-L` 19 步、
  `seed-M` 46 步仍按原分类停在只提供 `Shop` 的地图节点（`unsupported_map_room`，退出码 4）；
  `seed-K`/`seed-J`/`seed-E` 分别推进 127/94/105 步（战斗、奖励、旅行相位）后停在**宿主内容缺口**
  （`missing enemy behavior base-spire:fogmog` / `cubex-construct`），不是商店问题。
* **进店许可负对照**：`--shop-room enter --shop-policy buy --shop-remove` 实地把 `Shop` 节点推给固定 Host，
  宿主具名拒绝（`backend_fault`：`房间类型 商店 尚未实现`）→ `fault_host_rejected`、退出码 4、
  `run_complete=false`。**没有伪造成商店可达或通过。**

真实商店轨迹（进店 → 公开商品选择 → 删牌 decision → 离店 → 回地图）需要宿主真正接线商店的构建，
本包未拿到，因此记为**公开商店证据依赖 ENV-T09 最终 Host**；`test_shop_public.py --only replay` 已用
wave-1 的 8 个当前规则记录（含 seed-K 315 步）证明旧分支不回归。

### 2026-10-12（商店公开契约收口；Codex 固定 Host `F8FACE60…`）

固定 Host `.artifacts/parallel/owner-project-review-20261012/bin/Net.TheSpire.Host/debug/Net.TheSpire.Host.dll`
（SHA256 `F8FACE609121821489719CE8215FE94633CCBB38A4642E9EF2DE3787C1B37F68`）已真正接线商店，
`--shop-room auto` 判定进店。本轮只消费负责人已录的两局真实轨迹并补一个现场反例，**没有重跑整局矩阵**：

* **两局真实轨迹逐快照复算**：`test_shop_public.py --only replay --run-dir .audit/owner-project-review-20261012/shop-buy-remove`
  （45 步，rc 0）与 `…/shop-remove`（46 步，rc 0）——复算的 `candidate_id` 与记录**逐字一致**，
  含 `BuyShopCard`、删牌 decision 的 `net.answer_decision` 与 `LeaveShop`。复算按**记录里的策略**
  重建固定规则（`shop_room_entry` / `shop_*`），不再假定"记录里没有商店"。
* **商店公开闭合**：14 格公开货架（`Slot` 0..13）、不可买条目的源身份与非空原因、买卡进公开
  `LongTermDeck` 的永久副本、金币每笔只扣一次、`RemoveShopCard` 暂停后按已公开实例删对牌、
  `MapPhase 7 → 2` 与后续合法候选，逐帧核对见
  [本包契约校验日志](../../.audit/dsh-shop-public-contract-20261012/verify-shop-contract.log)。
* **现场反例** `--probe-rejected-candidate`（`--shop-policy buy`，`ENV-T03-SEED-L`，26 步，退出码 3）：
  买下 `base-spire:havoc`（金币 111 → 63）后用刚消费掉的旧 `candidate_id` 配当前 `observation_id` 重发，
  宿主具名拒绝 `illegal_candidate` / `execution_state=not_started` / `retry_mode=correct_input`；
  随后 `observe` 的 Run 公开字段与 `legal_actions` **逐字段不变**，当前快照仍提供 `LeaveShop`，
  紧接着第 22 步 `LeaveShop` 成功、`MapPhase` 回到 2。原始响应见
  `.audit/dsh-shop-public-contract-20261012/runs/shop-buy-probe/rejected-candidate-probe.json`。

## 明确的未支持边界

* **房型**：客户端可提交 `Unknown`（问号）与 `Elite`（精英）当前候选。问号判型、事件池与精英内容由Host决定；未实现内容仍可具名 `backend_fault` 停止，不算通过。`Ancient` 等其它未消费房型仍是 `unsupported_map_room`，不虚报支持。`Shop` 是否进店仍由 `--shop-room` 决定。`host_fault_attribution` 保存提交候选、观察、地图节点类型及原错误；`backend_fault` 本身不是结构化“内容缺失”码，准确根因以Host具名消息为准。失败后摘要保留最后成功快照，不声称引擎已回滚。
* **事件**：无逐事件ID映射。`event_events` 记录进入、全部当前候选、稳定排序、显式匹配和关闭后的公开相位/HP/金币。关闭后从下一快照恢复地图/商店/战斗，不沿用旧候选或旧 ActualRoomType 判断事件访问。选项执行资格、效果、页转换、死亡与关闭都交Host。
* **商店**：客户端只按公开 `Offer` 字段挑候选，不重判买不起/容量/可删；一次访问买一件、删一张。
  货架字段缺失、删牌 decision 的域不是公开长期牌组、相位 7 但无显式策略且无 `LeaveShop` 都会
  **具名停止**（`shop_no_decision` / `shop_removal_decision_domain_mismatch` / …），不猜值。
* **幕转换**：`ContinueToNextAct` 在 `dev-map` 上由宿主显式 `Fail("act transition is not implemented
  in dev-map scope")`；驱动器默认不推它。
* **宝箱遗物**：`TakeTreasureRelic` 的可用性取决于遗物接线；默认 `skip`。
* **药水**：只使用公开声明 `UseSupported=true` 的瓶子；`DiscardPotion` 永远不主动选。
* 任何未识别的公开 action 名或不可判定字段组合都会停成 `unsupported_action`（退出码 4，且把
  未知 action / 字段集合写进 `stop.detail`）。

## 2026-10-13 固定Host消费者证据

正式入口（不是包装）使用固定Host SHA256 `A296C2672305B8C96092D8273464B761D98930CFA93454D2A7BC2D6440483B75`：

```powershell
python -X utf8 -u net-the-spire/client/run_game.py `
  --host-dll .artifacts/parallel/owner-dsh-wave-20261013/host/Net.TheSpire.Host.dll `
  --scope dev-map --seed ENV-T03-SEED-L --max-actions 90 --use-potions `
  --shop-room auto --shop-policy buy --shop-buy-kind card --shop-remove `
  --event-choice SunkenStatueDiveIntoWater `
  --output .audit/dsh-public-event-policy-20261013/runs/seed-L
```

结果：第46步进入问号事件，第47步HP67→60、金币73→179、phase8→2并清当前事件；第48步进入下一商店，第49步购买，第50–52步删牌decision及离店，随后第三场战斗与奖励。72次成功提交后第73次Elite候选被Host具名拒绝：`byrdonis-elite cannot execute: missing enemy behavior base-spire:byrdonis`。实际rc4、`run_complete=false`、`close_ok=true`，**不是整局通过**。修复了商店访问级买/删标记跨店泄漏，因此与旧70步包装轨迹不同。

同seed将 `--event-choice` 改为 `GrabSword`：46次成功后第47次选项提交被Host以 `SwordOfStone` 获得与效果未实现拒绝，rc4；没有选择替代事件或选项。

原始帧/步骤/公开身份与关闭结果分别在[主运行摘要](../../.audit/dsh-public-event-policy-20261013/runs/seed-L/summary.json)、[主运行原始帧](../../.audit/dsh-public-event-policy-20261013/runs/seed-L/frames.ndjson)、[主运行步骤](../../.audit/dsh-public-event-policy-20261013/runs/seed-L/steps.ndjson)与[GrabSword摘要](../../.audit/dsh-public-event-policy-20261013/runs/seed-L-grab-sword/summary.json)。

检查入口：`python -X utf8 net-the-spire/client/test_event_public.py`（合成公开输入的事件/候选隔离判别）及 `python -X utf8 net-the-spire/client/test_shop_public.py --only replay --run-dir .audit/dsh-public-event-policy-20261013/runs/seed-L`（73次提交逐快照候选复算，包含最后被拒绝的提交；不是游戏规则独立对拍）。

## 与既有产物的关系

* `net-the-spire/client/net_client.py` **未修改**：驱动器只 import 它的 `NetClient` /
  `NetClientError` / `OutcomeUnknown` / `host_command`，并用子类覆写 `_write_frame` / `_read_line`
  记录原始帧（不改协议行为）。
* `tools/base-spire-dev/act1_smoke.py` 只作公开协议参考阅读；其 fixture 的固定 `candidate_id`、
  golden 身份**没有**进入驱动器。
* `net-the-spire/client/README.md` 的药水验证说明**未修改**，本工具不重复其断言；
  [test_shop_public.py](test_shop_public.py) 是商店策略与公开候选闭合的独立检查，不启动 Host、不重复药水套件。
* `public_snapshot.py` **未修改**：商店货架读取按公开 `ShopOffers` 序列 + 公开字段复用它的
  `sequence` / `public_fields` / `field`，没有为商店新增共享访问器。
