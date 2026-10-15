# Python公开客户端

第一次编写客户端，从[公开运行教程](../../docs/design/net-the-spire/how-to-run-public-game.md)启动正常Run；准确方法和快照字段访问见[客户端接口参考](../../docs/design/net-the-spire/public-client-reference.md)。共用访问逻辑在[public_snapshot.py](public_snapshot.py)，整局固定策略运行器见[run_game.md](run_game.md)。

## 公开商店驱动检查

从仓库根目录运行 [test_shop_public.py](test_shop_public.py)。它**不需要宿主进程、不触发构建**：在合成公开快照上核对商店固定规则（默认安全离店、`--shop-policy=buy` 按公开 Price/Slot 挑一件、`--shop-remove` 删一张后离店、已售/不可买/字段缺失候选的跳过与留证、一次访问不重复购买），核对删牌 decision 与战斗目标/其它选牌的区分，核对 `--shop-room` 的进店许可判定，并把 wave-1 已记录的公开整局帧逐快照复算（候选必须闭合、旧分支必须一致）。

```powershell
python -X utf8 -u net-the-spire/client/test_shop_public.py
python -X utf8 -u net-the-spire/client/test_shop_public.py --only policy
python -X utf8 -u net-the-spire/client/test_shop_public.py --only replay --run-dir .audit/<RUN_DIR>
```

`--only` 取 `policy` / `decision` / `map` / `replay` 之一；`replay` 默认用 `.audit/dsh-public-run-driver-2026-10-11/runs/seed-K`。退出码 0 表示所选分组全部执行且通过，1 表示断言失败，2 表示 `--run-dir` 缺失。复算时只对记录里写明当前规则的步骤要求 `candidate_id` 逐字一致；wave-1 记录里写明旧规则变体（低血先格挡/费用低者先/无门槛用药水/旧地图偏好）的步骤按动作名与候选闭合核对，具体清单见文件里的 `LEGACY_REASON_PREFIXES`。

`replay` 组按**记录里的策略**重建固定规则（含 `shop_room_entry` 与 `shop_*`），因此**带商店的真实轨迹**也能整段复算：`.audit/owner-project-review-20261012/shop-buy-remove`（45 步，买卡 + 离店）与 `…/shop-remove`（46 步，删牌 + 离店）都要求候选闭合且 `candidate_id` 逐字一致。记录里没有商店时行为与原来相同（非商店记录只多一条"整局没有商店事件"的断言）。进店复算额外核对：公开货架 14 格且 `Slot` 覆盖 0..13、不可买条目保留源身份与非空原因、买到一件或如实记 `shop_buy_unavailable`、删牌提交后一定有 answer 或明确未提交。

现场的**候选闭合负对照**用 [run_game.md](run_game.md) 的 `--probe-rejected-candidate`（配 `--shop-policy=buy`）：第一次真实买卡后用刚消费掉的旧 `candidate_id` 配当前 `observation_id` 重发，宿主必须拒绝、公开状态不得推进、且当前快照仍提供 `LeaveShop`。它只读当前公开快照，不生成候选、不猜参数；证据写到输出目录的 `rejected-candidate-probe.json`。

## 公开药水验证

从仓库根目录运行 [test_potion_host.py](test_potion_host.py)。它使用标准 [NetClient](net_client.py) 启动现有 DLL，不触发构建。运行前必须设置 `NET_THE_SPIRE_HOST_DLL`，路径不存在时退出码为 2。

```powershell
$env:NET_THE_SPIRE_HOST_DLL='F:/Scylier-Project/active/auto-the-spire/.artifacts/parallel/owner-potion-flow/author-final/bin/Net.TheSpire.Host/debug/Net.TheSpire.Host.dll'
python -X utf8 -u net-the-spire/client/test_potion_host.py
python -X utf8 -u net-the-spire/client/test_potion_host.py --only full
```

`--only` 每次只执行一个分组；省略时运行全部五组。`--trace` 可指定所选新构建的公开轨迹，仍强制核对轨迹身份；默认读取下述负责人轨迹。`--log` 指定收发帧证据，`--summary` 指定汇总文件。重复运行前指定不同输出路径可保留前次证据。

| 分组 | 实际边界 |
| --- | --- |
| `catalog` | describe/catalog/start/observe、运行期药水内容及声明；记录实际数量 |
| `normal` | 正常领取、真实未支持瓶、支持瓶使用、丢弃；领取/使用/丢弃去重及过期/消费拒绝 |
| `full` | 正常公开前缀达到满栏；保留奖励、Take 无合法候选、显式丢弃后领取到第一空槽 |
| `skip` | 正常奖励跳过后关闭药水奖励及候选，持有栏不变 |
| `target` | 既有作者文本受控场景：敌人目标先暂停、回答后销毁瓶、没有能量付款及重发/拒绝 |

正常组使用 [负责人公开轨迹](../../.audit/owner-potion-flow-2026-10-11/public-replay-author-final/public-trajectory.jsonl) 的有限前缀，每步在当前 published snapshot 中重新匹配 action 与 fields，再提交真实 candidate_id。开始复放前必须逐字匹配该轨迹的 describe 身份；新构建若身份变化，需要提供该构建的新公开轨迹，不能删掉身份校验。脚本不固定药水目录数量、catalog/ruleset hash 或逐瓶动作注册，不读取当前内容源码来推断被冻结 DLL。

正式组的全部请求/响应过 [0.2 schema](../../docs/contracts/net/0.2/wire.schema.json)，检查普通帧没有 MapDraws。现有作者文本场景公开协议为 0.1，因此 `target` 按 [0.1 schema](../../docs/contracts/net/0.1/wire.schema.json) 独立验证，并在汇总中标明受控证据。它不证明敌人目标瓶在正式流程中正常掉落，也不计入正式 0.2 覆盖。

退出码 0 表示所选组全部有执行且通过；1 表示断言/Host/分支失败；2 表示参数或 DLL 环境缺失。汇总保留每组 seed、公开身份、步骤、断言、原始帧位置及未覆盖范围。未支持 Use 和满栏 Take 的拒绝证据是没有公开合法候选及对非候选 token 的 `illegal_candidate`；协议没有自造 Use/Take payload 的入口。
