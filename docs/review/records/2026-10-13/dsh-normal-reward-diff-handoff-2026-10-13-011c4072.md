---
task_id: ENV-T04
dispatch_id: ENV-T04-normal-reward-diff-20261013
session_id: session-011c4072-7363-48da-bc48-160af934e6ab
delivery_status: partial-delivery-with-divergence
delivered_at: 2026-10-13T11:34:00+08:00
timezone: Asia/Shanghai
dispatch_record: .audit/dsh-normal-reward-diff-20261013/task-record.json
---

# 正常首战奖励独立对拍交接 — 2026-10-13

本包交付可执行的正常首战→真实胜利奖励→领取或跳过→永久资源提交与旅行就绪入口；两例均实际双侧执行，均有初始地图首差，**不是原版一致性通过，也没有验证后继旅行提交或下一战斗初始化**。

归属为 ENV-T04-normal-reward-diff-20261013，证据关联 ENV-T02/T05。当前认领、排期和验收以[主清单 ENV-T04](../../tofix/README.md#env-t04)为准；本报告是执行者日期快照，不关闭 T02/T04/T05/M01。范围及原完成条件见[准确派单](../../../../.audit/dsh-normal-reward-diff-20261013/task.txt)，方法见[B 严格对拍合同](../../../design/post-core-agent-assignments.md#4-bdiff-0102日常严格对拍)。主清单由负责人单写，本会话未改主清单。

## 接手后先做什么

1. 核对[派发记录](../../../../.audit/dsh-normal-reward-diff-20261013/task-record.json)和[固定输入验证](../../../../.audit/dsh-normal-reward-diff-20261013/fixed-input-verification.json)，保留共享未提交工作。复用本包入口，不重做受控17，也不构建开发中的 base/reference。
2. 查看[新套件严格结果](../../../../.artifacts/parallel/dsh-normal-reward-diff-20261013/final/normal-reward-prefix/regression.json)的 boundary[0] 地图候选首差。负责人先裁决正常参考根 seed/地图生成及玩家流的源兼容边界；不要私有字段注入地图、换 seed 或补抽来对齐。
3. 在同一固定闭包下用下节命令复现。预期旧17 rc0、新套件 rc1；若要验证后继旅行和下一战斗，先解决正常初始化首差并登记准确同一语义动作前缀，再扩展本入口，不能把本包 travel-ready 当成已旅行。

验收者为负责人。原完成条件仍包括真实同 seed 独立执行、完整奖励/长期资源/真实随机证据、严格缺参考/异常/零执行/缺维度/首差非零，以及最终原17与新suite串行运行。本包未完成正常路径一致性和尽量验证的下一战斗。

## 已交付与接口变化

- [NormalRewardEntry](../../../../tools/base-migration-baseline/differential/NormalRewardEntry.cs)：正常 `BaseSpireRuntimeFactory(RunConfiguration.Ironclad(0,dev-map)) → CreateSession(gameSeed) → Start` 一次；只提交合法动作，不设初始牌区、HP、金币、敌人或强制清场。
- [NormalRewardReference](../../../../tools/base-migration-baseline/differential/NormalRewardReference.cs)：独立正常 RunState/Overgrowth/Player/MapTravel/RoomFactory/PushRoom/Enter；真实战胜后 `ResolveOutcomeAsync(true)`，读取 GeneratedRewards，真实 `SelectOption/Take/Skip` 消费，最后正常 Exit/Pop。
- [独立严格 suite](../../../../tools/base-migration-baseline/differential/suites/normal-reward-prefix.json)：两个固定 case，Ironclad/A0/dev-map；take seed=`ENV-T03-SEED-L`，skip seed=`ENV-T04-RNG-SHUFFLE-001`，`vanilla-compatible@1`。两侧采用同一可见候选策略，但路径分叉后展开动作不同，**不能称作相同具体动作前缀的成功对拍**。完整动作在每侧 kind/label 中保留。
- [FixtureSuite](../../../../tools/base-migration-baseline/differential/FixtureSuite.cs) 增加 normal-reward kind 与策略动作；[TestCommand](../../../../tools/base-migration-baseline/differential/TestCommand.cs) 增加 rewards/permanent-inventory/reward-closure 实测门槛。记录金币/Hp/MaxHp、永久牌组逐位置实例/定义/升级、药水库存/容量、奖励牌序/稀有度/升级/金币/药水存在与解决状态、地图节点/连线/坐标/阶段、真实合法候选及随机读数。
- [Comparator](../../../../tools/base-migration-baseline/differential/Comparator.cs) 先比较共同前缀，再报告边界数量，避免总数差遮住更早首差。原17定义未改；[ComparatorSelfTest](../../../../tools/base-migration-baseline/differential/ComparatorSelfTest.cs) 仅修复既有可空警告。[ReferenceBackend](../../../../tools/base-migration-baseline/differential/ReferenceBackend.cs) 最小开放工具内部投影辅助方法；两侧分派增添新 kind。
- [工具用法](../../../../tools/base-migration-baseline/differential/README.md#正常首战奖励前缀) 明确正常入口与旧受控 run 命令分列。永久牌组位置只适用于本包追加/跳过，不是移除/变形/跨战斗复制的通用稳定实例身份。

修改边界仅 tools/differential、本包 .audit/.artifacts 和本交接；未修改 base/core/net/reference 玩法、生产工程或旧证据，未 reset/clean/提交，未派其他会话。

## 验证与证据

固定 DLL 摘要如下；[完整交付摘要](../../../../.audit/dsh-normal-reward-diff-20261013/delivery-hashes.json)同时保存工具文件与旧 suite SHA256。

| DLL | SHA256 |
| --- | --- |
| BaseSpire | E49A7BFFA9254912876143A51A74F6C75D6EDCE0FA247107F64ABBEDB3A56F67 |
| Core.TheSpire | DF111D79DAAA90EF8913F4DB01780FE93C98A58644E29138D178998289294659 |
| Sts2Sim.Core | BA817F77F8C1EBF85BC700AE1FC11559DC3A14307FE1ECFEDF5028BC306D0EDE |

使用 .NET SDK 9.0.306/net9.0/C#13，隔离工程仅编译当前 flat tools 源码、REFERENCE_SIM，引用固定 DLL，不使用生产 ProjectReference/NoWarn。仓库根目录实际命令如下：

```powershell
dotnet build .audit/dsh-normal-reward-diff-20261013/verify/DifferentialVerify.csproj -v minimal
dotnet .artifacts/parallel/dsh-normal-reward-diff-20261013/verify/bin/Debug/net9.0/Differential.dll test --suite regression --output .artifacts/parallel/dsh-normal-reward-diff-20261013/final/regression
dotnet .artifacts/parallel/dsh-normal-reward-diff-20261013/verify/bin/Debug/net9.0/Differential.dll test --suite normal-reward-prefix --output .artifacts/parallel/dsh-normal-reward-diff-20261013/final/normal-reward-prefix
powershell -NoProfile -ExecutionPolicy Bypass -File .audit/dsh-normal-reward-diff-20261013/check-record-negatives.ps1
```

最终先完整旧17、后完整新suite；[串行退出码](../../../../.audit/dsh-normal-reward-diff-20261013/final-exits.json)保存实际顺序。

| 检查 | 实际结果 | 证据 |
| --- | --- | --- |
| 最终隔离构建 | rc0，0 warnings/0 errors | [构建日志](../../../../.audit/dsh-normal-reward-diff-20261013/build-final.log.utf8.txt) |
| 原完整 regression17 | base执行17；reference执行16；16双侧passed、1接线self_checked、0failed、自检0失败，rc0 | [严格结果](../../../../.artifacts/parallel/dsh-normal-reward-diff-20261013/final/regression/regression.json)，run_id=`regression@env-t04-regression@1+20261013T033101Z` |
| 新完整2例 | base/reference各执行2；0passed、2diverged、自检0失败，rc1；两侧 required dimensions 无缺项 | [严格结果](../../../../.artifacts/parallel/dsh-normal-reward-diff-20261013/final/normal-reward-prefix/regression.json)，run_id=`normal-reward-prefix@normal-reward-prefix@1+20261013T033111Z` |
| 外部 records 比较器探针 | identity rc0；奖励牌/永久牌组/缺奖励维度/参考异常/缺参考各rc1 | [机器汇总](../../../../.artifacts/parallel/dsh-normal-reward-diff-20261013/negatives/checks.json) |
| 零执行 | `--only nonexistent` rc2 | 真实命令输出拒绝零执行；没有生成成功报告 |

外部 records 的身份对照与变异均从实际 base 记录制作，明确标记 synthetic，同侧比较器探针不是独立参考证据，`reference_ran=0`。奖励牌首差为 boundary[11].reward.cards.order；永久写回首差为 boundary[13].deck.instance:10。缺奖励事实记录同时产生差异及 `rewards(reference)` 缺维度，未放宽门槛。

### 实际生命周期边界

[生命周期汇总](../../../../.artifacts/parallel/dsh-normal-reward-diff-20261013/final/lifecycle-summary.json)来自本轮实际记录，不是源推断。所有末态均 travel-ready，有合法后继地图候选。

| case | base/reference边界 | 最终Gold | 最终Hp/MaxHp | 永久Deck实例数 | 药水 |
| --- | --- | --- | --- | --- | --- |
| normal-first-reward-take | 14 / 15 | 111 / 119 | 80/80 / 80/80 | 11 / 11 | 两侧均未掉落；未实际执行take potion |
| normal-first-reward-skip | 16 / 18 | 118 / 117 | 80/80 / 71/80 | 10 / 10 | base未掉落；reference掉落Attack Potion并真实Skip，末库存为空 |

take 永久追加牌分别为 Forgotten Ritual / Bloodletting；skip 两侧永久牌组保持10实例。两侧均真实金币消费。只有参考skip例实测药水跳过，没有任何本轮实际领取药水证据。

## 首差、参考限制与未验证

两例的严格比较都在 boundary[0] 停止，没有最后一致边界。take 的 ChooseMapPoint 候选为 base `[map:1,1,map:2,1,map:5,1]`、reference `[map:2,1,map:3,1,map:5,1]`；skip 为 base `[map:0,1,map:2,1,map:5,1]`、reference `[map:1,1,map:2,1,map:3,1,map:4,1,map:5,1]`。后续实际执行并保留奖励结果，但因已分叉，不能把后来奖励不同直接裁定为奖励算法缺陷。

源依据与边界：

- [原游戏 CombatRoom:261–272](../../../../sts2/src/Core/Rooms/CombatRoom.cs#L261-L272) 从房间结束生成并offer；[RewardsSet:206–254](../../../../sts2/src/Core/Rewards/RewardsSet.cs#L206-L254)定义金币/药水/牌及随机顺序；[RunManager:724–755](../../../../sts2/src/Core/Runs/RunManager.cs#L724-L755)是地图生成上下文。
- [参考 RunState:175–190](../../../../reference/sts2-sim/src/Sts2Sim.Core/Runs/RunState.cs#L175-L190)构造时已生成地图；[GenerateCurrentMap:373–385](../../../../reference/sts2-sim/src/Sts2Sim.Core/Runs/RunState.cs#L373-L385)用 RunRngSet.Seed 派生地图 RNG。[RngSourceCompat.Apply:328–353](../../../../tools/base-migration-baseline/differential/RngSourceCompat.cs#L328-L353)只替换尚未消费的run具名流，不改变根Seed/已生成地图，也不改变玩家Rewards RNG。公开GenerateMap仍使用原生根Seed，没有本包已确认的正常源兼容根Seed注入API。
- [参考真实胜利:376–399](../../../../reference/sts2-sim/src/Sts2Sim.Core/Rooms/CombatRoom.cs#L376-L399)完成战斗、胜利hooks并生成奖励；本包实际调用该生命周期，未直接伪造奖励。

随机证据没有补抽或删除域：边界Streams为当时累计状态；ReferenceStreams另留初始化/实际触及增量。base单位generator words，reference单位native counter increments；未映射的原生地图、玩家和动态流保留observed证据，不宣称同单位。源版本兼容范围之外的正常根seed/玩家流仍待负责人窄裁决。固定DLL身份经摘要验证；源码语义定位不等于所有当前源码与DLL逐文件等价。

**未验证**：相同具体动作前缀贯穿奖励、正常奖励一致性、两侧药水领取与库存非空写回、后继旅行提交、下一战斗长期牌复制初始化、奖励重掷/替代项/满药槽、多房间/多幕/死亡及新角色。不外推全量环境或关闭父项。

未运行 solution/Engine/Slice/net、生产base/reference构建，按派单留给负责人收口。本包仅报告工具编译、真实正常双侧执行和限定的严格反例。

## 维护与接手自查

用法维护在[工具说明](../../../../tools/base-migration-baseline/differential/README.md#正常首战奖励前缀)，原始证据在本包.audit/.artifacts，当前任务状态仅由[主清单](../../tofix/README.md#env-t04)维护。正文自查可定位目标/当前入口、复用成果、第一步、原完成条件/验收者、首差/未验证及维护位置；这是作者自查，不是独立读者审阅。实际文档检查 `python tools/doc-check/check_docs.py` 扫描479篇 Markdown、0问题、rc0；`git diff --check` rc0，共享已有文件仅提示LF/CRLF转换警告。
