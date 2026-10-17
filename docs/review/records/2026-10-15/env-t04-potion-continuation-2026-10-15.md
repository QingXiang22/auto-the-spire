# ENV-T04/05：连续战斗与正常药水领取交接 — 2026-10-15

本次让正常战斗奖励的实际药水领取具备独立双侧证据，并通过同构建 Python 公开客户端完成领取与返回地图；同时补记上一轮两场连续战斗的最终验证。

归属：[ENV-T04 随机初始化](../../tofix/README.md#env-t04)、[ENV-T05 地图战斗奖励往返](../../tofix/README.md#env-t05)。Codex `/root` 按用户恢复推进指令接续；本轮代码只改 `tools/differential`，未修改 base、net、core 或参考源码。当前排期与剩余工作以主清单为准。

## 接手先做

复用 `normal-potion-reward`、`normal-reward-prefix`、`normal-two-combats` 三套正常 Run suite。药水领取已验证，满容量仍缺正常双侧证据；下一步沿现有 seed 的真实流程推进，记录容量前置来源和首次受阻房间，不换 seed、缩池或注入药水。扩展参考战斗库存动作时须先补实际 `UsePotion` 合法性与独立执行，不把当前只验证领取的投影当作完整药水使用适配。

本报告未关闭 ENV-T04/05 或 M01。原完成条件仍包括容量、奖励幂等、正常公开操作及永久状态比较；ENV-T02 的 Feed、同定义实例永久变化等条件也未由普通两场战斗替代。

## 已交付与修复

- 新增 `tools/differential/suites/normal-potion-reward.json`，沿用 `ENV-T04-RNG-SHUFFLE-001` 正常开局，实际战胜后领取首张卡、AttackPotion 和金币；空 setup，不写运行时初值。
- `FixtureCase.RequirePotionAcquisition` 要求共同动作包含真实 `TakeRewardPotion`，且奖励关闭后的库存非空；两侧分别检查，不能用无掉落或跳过替代。
- 首次执行在药水领取后的合法动作比较失败：base 提供 DiscardPotion，参考适配器遗漏。按参考 `Commands/PotionCmd.cs` 的 `CanUseOrRemovePotions` 与实际库存补全候选投影，未修改参考。该用例没有提交丢弃动作，不据此宣称丢弃全流程验收。
- 修正 base 记录里“两场用例仍称未进入下一战斗”的过期说明。

两场连续战斗的生产修复属于上一轮：唯一组编队首次选中后不再被后续候选覆盖；敌人状态机保持共享 run 级 MonsterAi。独立怪物 AI 流旧判断已撤回，不能重新作为实际消费证据。

## 身份与证据

工作目录均为仓库根。采用 `sts2-v0.109-xxhash64@1`，Ironclad/进阶0/dev-map；随机版本与内容版本分开，内容仍有冻结基线及显式当前元数据迁移，不声称全部内容等于 v0.109。参考 HEAD 为 `2089659bd07920088b93226136c72b6b6ee590ad`，本轮前后 `git status --porcelain` 为空。

本轮构建目录 `.artifacts/env-t04-potion-take`，公开与差分输出中的 BaseSpire.dll SHA256 同为 `ebd58b9f0a331a03a0fe5ca491113edd77b4f2e9a83e1e9f12198aa800c42ea4`；正常 base BuildId 为 `46cf733dd46fe61f`。Host 摘要、公开身份与原始帧另存公开 summary 和 frames。

| 检查 | 实际命令（仓库根） | 结果与证据 |
| --- | --- | --- |
| 工具构建 | `dotnet build tools/differential/Differential.csproj --artifacts-path .artifacts/env-t04-potion-take -p:ReferenceSimAvailable=true` | rc0，首次保留7处既有警告；后续增量构建0警告 |
| Host 构建 | `dotnet build net-the-spire/Net.TheSpire.Host/Net.TheSpire.Host.csproj --artifacts-path .artifacts/env-t04-potion-take` | rc0 |
| 正常领取 | `dotnet .artifacts/env-t04-potion-take/bin/Differential/debug/Differential.dll test --suite normal-potion-reward --output .audit/env-t04-potion-continuation/delivery-take` | rc0，1/1双侧，9自检通过；run_id `normal-potion-reward@normal-potion-reward@1+20261015T124657Z` |
| 首战两例 | 同一 DLL：`test --suite normal-reward-prefix --output .audit/env-t04-potion-continuation/delivery-first-combat` | rc0，2/2双侧，9自检通过 |
| 连续两场 | 同一 DLL：`test --suite normal-two-combats --output .audit/env-t04-potion-continuation/delivery-two-combats` | rc0，1/1双侧，9自检通过；35边界 |
| 原严格回归 | 同一 DLL：`test --suite regression --output .audit/env-t04-potion-continuation/delivery-regression` | rc0，16双侧+1工厂自检，22自检通过 |
| 必须领取负例 | 同一 DLL：`test --suite .audit/env-t04-potion-continuation/negative-required-take.json --output .audit/env-t04-potion-continuation/negative-required-take` | rc1，跳过药水被 `Required normal potion acquisition did not occur or persist to reward closure` 拒绝；该故障记录另引发8项比较器自检失败，不能描述为自检全绿 |
| 无参考构建 | `dotnet build tools/differential/Differential.csproj --artifacts-path .artifacts/env-t04-potion-no-reference -p:ReferenceSimAvailable=false` | rc0，4处既有base警告 |
| 无参考严格拒绝 | `dotnet .artifacts/env-t04-potion-no-reference/bin/Differential/debug/Differential.dll test --suite normal-potion-reward --output .audit/env-t04-potion-continuation/no-reference` | 日志及regression.json明确exit_code=3，0双侧passed；外层工具进程记录为1，按此保留包装差异 |

正常领取17个边界最终双方均为76/80 HP、117金币、11张长期牌、slot0 AttackPotion，回到 `map:1,1` / MapFloor2 / travel-ready。两场跳过路线最终62/80 HP、136金币、10张长期牌、空药水栏，回到 `map:0,2` / MapFloor3；随机计数与指纹均逐项比较。

上一轮 `.artifacts/env-t04-two-combat-final` 的最终集成：solution rc0、Slice 841全部通过、net 418/0；原始日志在 `.audit/env-t04-combat-continuation/two-final-slice.log` 与 `two-final-net.log`，6种子随机专项在 `delivery-rng/v109-rng.json`。本轮未改生产规则，因此没有重跑 Engine/Slice/net 全量，不把上述历史门槛冒称本轮新运行。

## Python 公开入口

本地验收脚本 `.audit/env-t04-potion-continuation/replay_public.py` 通过既有 RecordingClient 和公开 Host 执行，原始帧与核对摘要位于同目录 `public/`。实际24次公开提交，对应16个共同动作及8次目标回答；17个边界核对长期资源、牌组、药水栏、可见手牌构成和完整合法动作数量/候选语义。

公开 `visible_members` 按协议是无牌序的成员集合，不能当内部手牌槽。首次脚本错误地按数组下标解释牌槽，已纠正；没有发现生产洗牌不一致。公开侧以卡定义匹配，仅当重复实例全部公开字段相同时按公开句柄确定选择；该验证不声称跨入口重复卡牌的内部实例一一对应，也不比较隐藏抽牌堆顺序。严格双侧工具仍按原槽位比较，没有放宽。

使用现有工具从原始帧生成语义夹具并在新会话重放：

```powershell
python -X utf8 tools/base-spire-dev/act1_smoke.py --record-from .audit/env-t04-potion-continuation/public/frames.ndjson --fixture-out .audit/env-t04-potion-continuation/public/semantic-fixture.json --scope dev-map --host-dll .artifacts/env-t04-potion-take/bin/Net.TheSpire.Host/debug/Net.TheSpire.Host.dll
python -X utf8 tools/base-spire-dev/act1_smoke.py --host-dll .artifacts/env-t04-potion-take/bin/Net.TheSpire.Host/debug/Net.TheSpire.Host.dll --fixture .audit/env-t04-potion-continuation/public/semantic-fixture.json --output .audit/env-t04-potion-continuation/public-replay
```

两条命令均rc0；24个候选句柄均重新绑定，最终公开事实一致，真实奖励药水实例保留在栏位。仅证明该首战前缀，不是完整第一幕、整局或独立原版公开对拍。AttackPotion当前只支持元数据和领取，`UseSupported=false`；本轮不证明使用效果。

## 未验证与维护位置

满容量领取拒绝及腾出槽位后的继续领取、持有药水进入后续战斗的完整动作投影、第三场及商店/事件/精英参考流程仍待验收。远端幕转换会话本轮未查询或覆盖，暂缓召唤/复活与训练未恢复。变更未提交；临时脚本和产物只留 `.audit`。当前状态、认领与下一成果回写主清单，历史证据保留本报告及原始产物。

交接已并入主清单并移入records；接手自查可定位成果、入口、剩余条件和证据。文档检查扫描532份Markdown、0问题，`git diff --check`通过。未进行独立读者审阅。
