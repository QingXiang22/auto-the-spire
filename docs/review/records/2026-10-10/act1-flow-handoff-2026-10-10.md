# ENV-T05 / Act 1 流程交接报告

日期：2026-10-10
范围：`mod-the-spire/base-spire`

## 总体结论

当前任务未整体完成。正式内容和 Engine 接线已覆盖普通战斗奖励、Boss 房间进入、Boss 奖励后的延迟 Victory、营火 Rest/Smith，以及 Treasure 房间进入、开箱、金币写入和跳过动作。共享宝箱遗物池元数据与纯规则随机袋已经建立并有独立测试。

尚未取得“固定 seed、真实初始牌组和资源、只使用合法公开动作、经过宝箱并击败第一幕 Boss”的完整轨迹证据。因此不能关闭 ENV-T05，也不能声称第一幕流程已经完成。角色颜色、无色、状态、诅咒分类的剩余卡牌批量实现也尚未开始。

## 已交付

### 作者契约

前序工作已完成并有记录：

- Power 可注册并跨文件复用；
- base namespace 与自定义 namespace 分离；
- 外部消耗卡牌可触发 `onExhaust` / `on_exhaust`；
- `upgraded` 支持完整初始化、增量成长和 `CostModifier` 保留；
- 作者工具已修正并通过对应检查。

### 普通战斗与奖励

- 正式奖励声明在战斗结算前接入，真实胜利后进入 `MapPhase=3`；
- 金币、卡牌领取/跳过、药水领取/跳过通过公开动作关闭奖励；
- 普通奖励关闭后恢复地图选择；
- Boss 奖励关闭后才执行 `Victory(Turn)`，避免在奖励动作前提前终局。

关键位置：

- `mod-the-spire/base-spire/BaseSpire/Rewards/RewardWiring.cs`
- `mod-the-spire/base-spire/BaseSpire/Composition/CombatSlicePackage.cs`

### Boss 房间

- Vantom 已能从正式 `enemies.spire` 加载；
- 支持其 `startingPowers` 数值层数和 `variants(default=...)`；
- Boss encounter 已进入正式内容和 encounter pool；
- 地图包含 Boss 节点，Boss 分支可 dispatch；
- Boss 终局回调延迟到奖励关闭之后。

### Rest / Smith

- Rest 为 `floor(MaxHp * 0.3)`，有 MaxHp 上限；
- Smith 只允许选择未升级且源卡可升级的实例；
- 保留升级成长和费用修正；
- 重复提交和非法选择会被拒绝。

### Treasure

已有正式接线：

- Treasure 房间进入并进入 Treasure phase；
- `OpenTreasureChest` 公开动作；
- 金币写入 Run；
- Common/Uncommon/Rare 候选稀有度生成；
- `SkipTreasureRelic` 公开动作，完成后回到 MapPhase 2；
- Treasure 状态字段防止重复开箱/重复跳过。

共享池元数据：

- `mod-the-spire/base-spire/BaseSpire/Relics/SharedRelicPoolManifest.cs`
- 当前记录 118 个共享遗物身份和稀有度；Treasure 池筛选 Common/Uncommon/Rare。

纯规则袋：

- `mod-the-spire/base-spire/BaseSpire/Relics/TreasureRelicBag.cs`
- 当前实现按稀有度分袋、袋头取出并移除、稀有度回退、池外校验。

测试：

- `mod-the-spire/base-spire/BaseSpire.Slice.Tests/Relics/TreasureRelicBagChecks.cs`
- 入口：`--treasure-bag-only`

## 最终验证

命令：

```text
dotnet build mod-the-spire/base-spire/BaseSpire.Slice.Tests/BaseSpire.Slice.Tests.csproj --no-restore
dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests --no-build -- --treasure-bag-only
dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests --no-build -- --boss-reward-only
dotnet run --project mod-the-spire/BaseSpire.Slice.Tests --no-build -- --rest-site-only
```

结果：

- 构建：0 errors；此前正式 BaseSpire 构建有 3 个既有 nullable warnings，新增袋测试构建为 0 warnings/0 errors；
- Treasure bag：2/2 passed；
- Boss reward boundary：3/3 passed；明确标注为 controlled initial state，不是正常通关轨迹；
- Rest/Smith：2/2 passed；明确标注为 controlled cursor/HP/card state，不是正常通关轨迹；
- 文档检查：337 Markdown files scanned，0 problems；`git diff --check` exit 0。

## 已知阻塞

当前物化地图把 Treasure 固定在第 9 行，通往第 16 行 Boss 的前向路径必须经过该节点。若 Treasure 只提供代表性遗物 ID而没有真正的遗物获得动作和持久遗物实例，不能执行完整正常路线。当前 Engine 仍需完成：

1. 将随机袋顺序/状态接入 Run 的正式 Engine 字段；
2. 从袋中生成候选并按袋头移除；
3. 增加合法遗物领取动作，写入持久 relic collection；
4. 增加跳过动作与领取动作的互斥、重复拒绝和 map continuation；
5. 用真实 fixed seed 驱动开局到 Treasure 到 Boss 的合法动作轨迹；
6. 确认 Vantom 战斗可由真实牌组/资源击败，并确认奖励关闭后终端 Victory；
7. 把实际轨迹、seed、ruleset/build identity、首差和未验证范围写回主清单和证据记录。

## 不要误判

- `BossRewardBoundary()` 不是 Boss 通关；它安排了受控奖励初态。
- `RestSiteBoundary()` 不是正常地图轨迹；它安排了游标、HP 和牌面。
- `TreasureRelicBagChecks` 只证明纯规则袋，不证明 Engine 随机袋接线。
- 地图上出现 `Boss:1` 不等于已击败 Boss。
- 不能通过直接改 HP、清空敌人、删除奖励或伪造房间来补齐证据。

## 下一位 Agent 建议顺序

1. 先读本报告、`docs/review/tofix/README.md`、`docs/review/records/2026-10-10/act1-flow-boundaries-2026-10-10.md` 和相关 AGENTS；
2. 检查当前未提交 diff，不回滚共享工作区其他改动；
3. 优先完成 Treasure 持久 relic collection 与真实袋接线；
4. 增加正常 fixed-seed runner/test，拒绝受控 HP/游标注入；
5. 完成并记录 Act 1 Boss 证据后，再按 red/green/blue/purple、colorless、status、curse 分组处理剩余卡牌；
6. 每个完整机制包更新主清单，不创建第二套任务表。

## 当前状态

任务状态：未完成，等待后续 Agent 接替。
