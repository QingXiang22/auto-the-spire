# AromaOfChaos 与永久牌组选择交付 — 2026-10-16

> 同日后续：[事件后跨战斗保存与Shrink修复](post-event-carryover-2026-10-16.md)已把两个分支带入第三场正常战斗并独立验证实际出牌和结算，也修复了当时后续路线中的Shrink负无限持续缺陷。本文保留事件包原时点与174提交轨迹；新构建173提交轨迹见后续记录，不能混为同一次验收。

原 seed 现在可以进入 AromaOfChaos，选择升级或随机变换永久牌组中的一张牌，然后返回地图。两个分支均通过独立双侧比较和公开 Host 重放；正式客户端进一步运行到第8层自然死亡，原事件入口阻塞已消除。

归属：[ENV-T09-PR01 永久牌组选取／变换](../../tofix/README.md#env-t09)，同步 ENV-T04/05 与事件子流程。Codex `/root` 按用户继续实现指令交付本包。本文保存日期证据，当前状态与下一步只查主清单。

## 接手后先做

1. 复用 `RunDeck.SelectForUpgrade`、`SelectForTransform` 和已登记的 AromaOfChaos 模块，不重做旧记录中的事件缺失。严格复现入口为 `test --suite normal-aroma`，原 seed 为 `ENV-T04-RNG-SHUFFLE-001`。
2. 按主清单继续正常满容量前置路线、剩余源资格及后续房间。可从本包正式客户端轨迹复用真实资源和候选策略；本轮第8层终点为真实战斗死亡，不能将其当成内容阻塞或完整胜利证据。
3. 后续扩展永久变换前核对[整局公共操作契约](../../../design/mod-the-spire/run-domain/spec.md#永久牌组选择与变换)：当前只支持 Ironclad 普通池来源，其他卡池明确失败。附着、全部获得替换/变换参与者及其他角色仍按 ENV-T09-PR01 原范围完成；幕转换保持原主责。

## 能力与实现

- `Domain/Run/RunDeck.cs` 新增共享永久牌组选取。候选按永久牌组顺序保留真实实例；空域不提问，唯一候选自动执行，多候选由 Engine 暂停并选一张。升级复用 CardDefinitions，保留实例、位置及升级增量。
- 随机变换先解析完整 Ironclad 源池，排除原定义，按源稀有度与单人资格过滤；使用调用方指定的事件流恰好抽一次，不滚奖励稀有度或升级。新普通实例追加到永久牌组末尾，旧实例销毁，其他重复牌不受影响。未支持来源在抽数和写回前明确失败。
- `Rooms/Events/AromaOfChaosEvent.cs` 只组合两个源选项：LET_GO 变换，MAINTAIN_CONTROL 升级。`EventCompileContext.Deck` 注入已有 RunDeck，RoomCompilation 只提供同一能力，EventModules 只登记模块。事件关闭、存活守卫和暂停续接继续由共享层处理。
- tools 的正常前缀适配延伸到真实 Unknown→事件选项→永久结果→旅行。参考使用原生 RoomFactory/EventRoom/EventModel.ChooseOption；公开 RunDriver 构造只安装外部决策源，不运行另一条流程。参考源码及行为未修改。
- 两侧 `deck.origins.order` 分别记录本地原生实例首次出现编号，删除后不复用，不按结果定义重新匹配。候选仍用当前永久牌组位置表达选择；来源事实验证升级保留身份、变换创建新实例。事件拥有的 Rng 对象按原生事件身份显式映射，比较抽数与指纹。
- 新增 AromaOfChaosChecks，检查重复牌隔离、空域/唯一候选、选择前不抽数、变换一次抽取、普通新实例、牌数/顺序、关闭后拒绝重放。比较器负例验证实例来源与事件随机流错误会失败。

源码依据为本地 `sts2/src/Core/Models/Events/AromaOfChaos.cs`、CardSelectCmd 的永久升级/变换选择、CardFactory.GetDefaultTransformationOptions、CardCmd.Transform。源永久 Deck 变换追加末尾；保留原下标适用于其他牌区，不能用于永久 Deck。普通移除的 BeforeRemoved 与变换生命周期不能混用。

## 验证与身份

工作目录均为仓库根。隔离构建根 `.artifacts/aroma-20261016`，原始产物在 [.audit/aroma-20261016](../../../../.audit/aroma-20261016/)。Ironclad、进阶0、全解锁、单人、dev-map，正常比较使用 `sts2-v0.109-xxhash64@1`。参考 HEAD `2089659bd07920088b93226136c72b6b6ee590ad`，工作树干净。

| 检查 | 实际命令/结果 | 证据与证明范围 |
| --- | --- | --- |
| 共同构建 | `dotnet build AutoTheSpire.sln --artifacts-path .artifacts/aroma-20261016`，最终rc0、0警告/0错误 | `final-build.log`；首次完整编译保留既有7警告 |
| Engine | `dotnet .artifacts/aroma-20261016/bin/Core.TheSpire.Engine.Tests/debug/Core.TheSpire.Engine.Tests.dll`，rc0、99项 | `final-engine.log` |
| 完整Slice | `dotnet .artifacts/aroma-20261016/bin/BaseSpire.Slice.Tests/debug/BaseSpire.Slice.Tests.dll`，rc0、850项 | `final-slice.log`，含本地源检查、事件与身份门槛 |
| net | `dotnet .artifacts/aroma-20261016/bin/Net.TheSpire.Tests/debug/Net.TheSpire.Tests.dll`，rc0、419项 | `final-net.log`，无需新增 net 生产代码或协议字段 |
| 正常事件双侧 | Differential DLL执行 `test --suite normal-aroma --output .audit/aroma-20261016/final-normal-aroma`，rc0、2/2双侧、各33边界 | `final-normal-aroma/regression.json`；11项比较器自检均通过 |
| 既有严格回归 | 同DLL执行 `test --suite regression --output .audit/aroma-20261016/final-regression`，rc0、16双侧+1工厂自检 | 原17项保留，22项自检通过 |
| 药水与连续战斗回归 | 同DLL执行 `test --suite normal-potion-use`、`test --suite normal-two-combats`，输出到本包对应 `final-` 目录；均rc0、2双侧/1双侧 | 受影响正常前缀保持原维度 |
| 公开事件重放 | `python -X utf8 .audit/aroma-20261016/public_upgrade.py` 与 `public_transform.py`，均rc0 | 每条49次提交、33个边界；回答重发原响应、过期候选拒绝且 observe 不变 |

Differential DLL 为 `.artifacts/aroma-20261016/bin/Differential/debug/Differential.dll`。正常事件 run_id 为 `normal-aroma@normal-aroma@1+20261016T040647Z`。正常 base BuildId 为 `cabc31a4ce0466d1`；公开 ruleset 为 `net:ruleset:0a9014d4fa9882ca`，catalog 为 `net:catalog:84f5f969048a0fbc`，Host SHA256 为 `7d65732c8105c11a255d7593714f01817a35b1e82fad1262765fc9741efc02b1`。最终重放使用 `final-normal-aroma` 的记录。

升级最终保留 origin:0 并变为升级形态；变换移除 origin:0，新增 origin:12，永久牌数均为12。变换实际消费 `base-spire:event:AROMA_OF_CHAOS` 一次，状态指纹 `0x54e918a54ac2bc9e`；升级不消费该流。其余结果与轨迹从逐边界记录查证，不把一个 seed 的通过扩写成全池/全机制行为证明。

冻结内容配置的 dev 规则与构建身份保持 `b6472f08ed1c4f87` / `4b14a623a7f07d57`；dev-map 随事件新增动作和指令迁移为 `31802bab1575fbaf` / `3f2336d912772e4f`。实测描述保存在 `build-descriptions.jsonl`，与正常默认内容配置分别登记。

## 正式客户端接续

实际执行：

```powershell
python -X utf8 net-the-spire/client/run_game.py --host-dll .artifacts/aroma-20261016/bin/Net.TheSpire.Host/debug/Net.TheSpire.Host.dll --scope dev-map --seed ENV-T04-RNG-SHUFFLE-001 --output .audit/aroma-20261016/normal-public --max-actions 220 --potion-policy take
```

返回0，174次公开提交后在第8层 Byrdonis 精英战斗自然死亡，HP0/80、金币184，发布 Defeat/loss，未达到动作上限。AromaOfChaos 已实际进入并完成选牌，再继续后续战斗；无需改客户端或 net 逐项注册。原始 frames、steps、事件记录与 summary 均保留。该策略与严格两战前缀不同，不能拼接成同一双侧整局；自然死亡不能替代规定的四幕胜利/死亡脚本或完整环境验收。

## 未完成范围与接续位置

本包关闭 AromaOfChaos 两分支及当前 Ironclad 普通来源牌的窄缺口。其他角色/无色/状态/诅咒等源池、附着迁移、全部获得替换和变换参与者、其他永久牌组操作仍未完成；第一幕资格表的未支持身份仍明确失败。正常满容量、事件战斗返回、幕转换、四幕、五角色与最终120对继续开放。

未改 core 或参考，未触及幕转换文件，未派新会话、启动训练或提交共享改动。当前主清单已合入本包结论；本记录已从tofix转records，目录移动不表示父项关闭。接手自查可定位成果、复现、剩余范围与维护位置，未做独立读者审阅。
