# ENV-T04/05：真实丢弃、公开幂等与后续首差 — 2026-10-15

本轮新增正常奖励药水领取后丢弃的双侧证据，以及领取卡牌、领取药水、丢弃药水、领取金币四类公开操作的请求幂等证据；修复参考适配器遗漏战斗药水使用候选的问题，使未实现的使用效果能被严格比较发现。

归属：[ENV-T04](../../tofix/README.md#env-t04)、[ENV-T05](../../tofix/README.md#env-t05)。Codex `/root` 按用户继续推进指令执行；代码修改仅在 `tools/differential`，其余为本线文档及 `.audit` 本地产物。未改 base/net/core 或参考源码，未覆盖远端幕转换主责。

## 接手先做

1. 复用 `normal-potion-reward@2` 的领取与领取后丢弃两个正常用例，两侧均使用 v0.109 随机布局，原 seed 为 `ENV-T04-RNG-SHUFFLE-001`。
2. 执行 `normal-held-potion`：它当前应明确非零，在第二场入口的边界17暴露参考 `UsePotion(potion:0)` 而 base 无此候选。不要删掉参考候选制造通过；修复需先完成 ENV-T06-PR05 的生成候选→选择→获得机制，并让 AttackPotion 的真实效果、可取消选择、临时费用及清理进入生产路径。
3. 后继问号房的正常公开尝试已失败。源队列首项 PotionCourier 仅允许 CurrentActIndex > 0，下一项 StoneOfAllTime 仅允许第二幕且所有玩家持有药水。当前资格判定依附已实现模块，未实现事件被当作恒 true；应按 ENV-T09-PR05 将源资格与效果模块拆开维护，先独立核对选中事件，再补真实模块。不要靠实现错误入选的 PotionCourier 绕过资格差异。

满容量需要真实取得三瓶并遇到后续掉落，本轮未到达；丢弃证据不替代满栏保留、腾位再领、空槽不挤压及完整使用验收。父项仍按主清单原完成条件开放。

## 修改与适用边界

- `NormalRewardEntry` 新增 `normal_reward_take_discard` 可见候选策略，正常战胜→领取卡牌→领取真实药水→丢弃→金币关闭奖励；完成条件要求实际 Take 在 Discard 之前、丢弃前库存非空、最终库存为空。无药水掉落时必须失败。
- `NormalRewardReference` 通过公开 `PotionCmd.Discard` 独立执行丢弃；战斗候选通过参考 `PotionCmd.CanUseManually` 枚举实际可用药水，不按 base 的 UseSupported 白名单删候选。尚未实现参考 UsePotion 执行及其选择适配，策略也不提交它；当前首差在提交前即阻止继续。
- `FixtureSuite` 接受新策略，`normal-potion-reward` 版本升级到2，保留原领取用例；新增 `normal-held-potion` 严格失败复现，未加入绿色regression替代原用例。

**纠正本轮临时结论**：早期 `.audit/env-t04-potion-continuation/two-take-probe` 显示两场领取路线通过，但当时参考遗漏 UsePotion 候选，此结论撤回，不计验收。修正后的 `.audit/env-t04-potion-discard/delivery-held-boundary` 明确失败。两场跳过路线不持有药水，已在修正后的适配器重新验证通过。上一轮首战领取/公开重放没有进入持药战斗，该限定范围仍成立。

## 构建与验证

所有命令工作目录为仓库根。随机布局 `sts2-v0.109-xxhash64@1`，Ironclad/进阶0/dev-map；内容版本仍单独标识，正常 base BuildId 为 `46cf733dd46fe61f`。参考 HEAD `2089659bd07920088b93226136c72b6b6ee590ad`，前后工作树均干净。

构建：

```powershell
dotnet build tools/differential/Differential.csproj --artifacts-path .artifacts/env-t04-potion-discard -p:ReferenceSimAvailable=true
dotnet build net-the-spire/Net.TheSpire.Host/Net.TheSpire.Host.csproj --artifacts-path .artifacts/env-t04-potion-discard
```

均rc0；工具首次保留7处既有警告，后续增量和Host构建0警告。生产源未变，但不同隔离路径重编的DLL摘要不同；公开幂等最终使用本轮Host重新执行，早期旧Host尝试不作为最终同构建证据。

下表工具 DLL 为 `.artifacts/env-t04-potion-discard/bin/Differential/debug/Differential.dll`，证据根 `.audit/env-t04-potion-discard/`。

| 检查 | 实际命令或入口 | 结果及证据 |
| --- | --- | --- |
| 领取与丢弃 | `dotnet <DLL> test --suite normal-potion-reward --output .audit/env-t04-potion-discard/delivery-reward` | rc0，2/2双侧通过，9自检通过；run_id `normal-potion-reward@normal-potion-reward@2+20261015T130819Z` |
| 原严格回归 | `dotnet <DLL> test --suite regression --output .audit/env-t04-potion-discard/delivery-regression` | rc0，16双侧+1工厂自检，22比较器自检通过 |
| 两场跳过 | `dotnet <DLL> test --suite normal-two-combats --output .audit/env-t04-potion-discard/delivery-two-skip` | rc0，1/1双侧，9自检通过 |
| 持药第二场首差 | `dotnet <DLL> test --suite normal-held-potion --output .audit/env-t04-potion-discard/delivery-held-boundary` | rc1，0双侧通过，9自检通过；参考边界17多出UsePotion，后续PlayCard提交前停止 |
| 无掉落不能冒充丢弃 | `dotnet <DLL> test --suite .audit/env-t04-potion-discard/no-drop-negative.json --output .audit/env-t04-potion-discard/no-drop-negative` | rc1，保留领取正例通过；seed L无掉落的丢弃例被Required normal potion take/discard sequence did not occur拒绝，9自检通过 |
| 公开奖励幂等 | `python -X utf8 .audit/env-t04-potion-discard/replay_public_idempotence.py` | rc0，25次正常提交、18边界核对；四类操作分别验证同request重发原响应、不同载荷request_conflict、已消费候选illegal_candidate；observe确认拒绝后快照逐项相同 |

丢弃用例最终76/80 HP、117金币、11张长期牌、空药水栏，正常travel-ready。领取/丢弃不额外消费随机；最终Shuffle18、Player.Rewards13、Act1Map276、Niche1、UpFront298及指纹双侧一致。公开最终原始帧、四组拒绝响应及summary保存在 `public-idempotence/`；Host SHA256 `5701072450f02f42f02eee63832ad104a9b234774f0e6e6b1a6f16ad5e274d2b`。

公开脚本沿用上一轮记录中的可见内容与完整字段核对，不将无序visible_members解释成牌槽。重复卡牌只有全部公开字段相同才按公开句柄选择；不声称跨入口内部实例一一对应，不读取隐藏牌序/RNG。

## 正常后继与未完成事项

既有公开驱动命令：

```powershell
python -X utf8 net-the-spire/client/run_game.py --host-dll .artifacts/env-t04-potion-take/bin/Net.TheSpire.Host/debug/Net.TheSpire.Host.dll --scope dev-map --seed ENV-T04-RNG-SHUFFLE-001 --output .audit/env-t04-potion-discard/normal-continuation --max-actions 180 --potion-policy take
```

该次使用上一轮固定Host，46次成功提交后，第47次尝试进入唯一后继Unknown节点 `map:0,3` 被backend_fault拒绝，驱动声明exit4、外层工具记录1。最后成功状态71/80 HP、136金币，仍持有AttackPotion；此策略目标/奖励顺序与严格工具不同，不将其结果拼成双侧证据。队列身份来自正式工具诊断，源资格依据来自只读 `Models/Events/PotionCourier.cs`、`StoneOfAllTime.cs` 及本地sts2对应源码；没有运行完整参考事件选择，不将源码判断写成事件双侧通过。

本轮未改生产规则，未重跑Engine/Slice/net全量；未恢复召唤/复活、训练或远端会话。未提交任何更改。当前状态与后续主责仅回写主清单，原始失败/通过产物均保留，容量和完整环境不能据此关闭。

交接并入主清单后移入records。最终差分工具与Host的BaseSpire.dll SHA256均为`81c2a1754f83c2fd07ed28296d1526482975c107eb7ca52ee7fc69af82e47d02`；文档检查扫描541份Markdown、0问题，`git diff --check`通过，参考工作树干净。接手自查可以定位结果、入口、原完成条件与剩余依赖，未进行独立读者审阅。
