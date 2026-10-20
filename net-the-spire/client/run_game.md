# 公开整局运行器 `run_game.py`

当前 `run_game.py` 仍面向旧 formal-dev／dev-map 场景。当前纯 DSL 包尚未恢复正常 Run，本页保留驱动参数和策略参考；可执行的开发场景调用见 [公开接口指南](../../docs/design/net-the-spire/how-to-run-public-game.md)。

编写新策略前可先运行[最小客户端教程](../../docs/design/net-the-spire/how-to-run-public-game.md)。本运行器的实体、字段、集合、牌堆和地图关系访问复用[public_snapshot.py](public_snapshot.py)，调用签名见[客户端参考](../../docs/design/net-the-spire/public-client-reference.md)。

`net-the-spire/client/run_game.py` 用**公开 NDJSON 协议**（`NetClient`）把一整局 Spire 从正常
`start` 推到**整局结论**（Victory / Death / 其它终局）或**明确的开发边界**。它替代"每次真实
初始化或房间变化后手工重做旧 `candidate_id` 回放"的做法：候选一律从当前快照的公开
`legal_actions` 里现场挑，不写死任何 `candidate_id`、不写死任何 seed 的脚本。

它**不是** AI，也**不是**搜索器：没有评估函数、没有多 seed 搜索、不推断隐藏牌堆或随机数、
不读 Engine 内部状态。所有输入都来自宿主公开的 `describe` / `get_catalog` / `start` / `step`
与实时快照字段。

商店（`MapPhase=7`）同样只消费公开约定：`ShopOffers` 序列上的五个公开动作
（`BuyShopCard` / `BuyShopRelic` / `BuyShopPotion` / `RemoveShopCard` / `LeaveShop`）与每个候选
自带的 `Offer` 实体公开字段。买不买得起、能不能删、有没有容量都由 Engine 的**候选域与守卫**决定，
客户端不重判规则、只按公开 `Price`/`Slot` 做确定性挑选；商品字段缺失时记录可复查原因并停下，
不猜值、不清空货架。

## 快速开始

```powershell
# 只读的不可变公开 Host（本包验证所用，SHA256 805C123D9941454E0B15E704CD48EB107A2E2A26DCC4555DDE8617513D0C1F6D）
python -X utf8 -u net-the-spire/client/run_game.py `
  --host-dll net-the-spire/Net.TheSpire.Host/bin/Debug/net9.0/Net.TheSpire.Host.dll `
  --scope dev-map --seed ACT1-BASIC-10 `
  --output .artifacts/public-run/example `
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
10. **开发开发场景推进**：`UpgradeLongTermCard`（选公开顺序第一张未升级长期牌）/ `EnterNextRoom`。
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
  - `identity` 与 `identity_history`（`describe` 与时序上的标识，含会话建立点）；
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
| `1` | 驱动器自身问题（参数、标识校验、协议信封） |
| `3` | **未到整局结论**：`act_completed_dev_boundary`（幕完成）或 `step_limit`（步数上限） |
| `4` | 故障 / 未支持边界：`unsupported_action` / `unsupported_map_room` / `no_legal_actions` / `host_rejected` / `stalled` / `uncertain` |

`summary.run_complete` 只有在退出码 0 且拿到 `win`/`loss`/`other` 终局时为 `true`。**幕完成、
步数上限、故障一律不写成整局通过**；第一幕过关（`ActCompleted=true`）永远只是
`act_completed_dev_boundary`。
