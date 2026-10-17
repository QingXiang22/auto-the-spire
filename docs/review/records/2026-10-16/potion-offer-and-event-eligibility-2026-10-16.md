# AttackPotion 集成与第一幕事件资格交付 — 2026-10-16

> 同日后续：[AromaOfChaos与永久牌组选择交付](aroma-of-chaos-2026-10-16.md)已补齐下文当时缺少的两个事件效果，并完成独立比较和公开重放；本文保留原交付时点与失败证据，当前任务查主清单。

原 seed 的持药续战、AttackPotion 使用和取消已通过独立比较及公开重放。事件资格现在独立于效果模块：正常两战后，base 与独立参考都选中 AromaOfChaos；该事件效果尚未实现。

归属：[ENV-T06-PR05](../../tofix/README.md#env-t06)、[ENV-T09-PR05](../../tofix/README.md#env-t09)，同步 [ENV-T04](../../tofix/README.md#env-t04) 与 [ENV-T05](../../tofix/README.md#env-t05)。Codex `/root` 按用户继续推进指令完成本轮接续；本记录保存交付时点，当前排期与认领仍查主清单。

## 接手后先做

1. 复用已验收的 AttackPotion 机制，不重做旧日志里的 UsePotion 缺失。严格入口是 `normal-held-potion`、`normal-potion-use`、`normal-potion-reward` 与 `normal-two-combats`。
2. 按 ENV-T09 核对文件主责，补 AromaOfChaos 的事件效果。源码 `sts2/src/Core/Models/Events/AromaOfChaos.cs` 的两个选项分别需要永久牌组变换和升级选择；先核对 [Run 领域契约](../../../design/mod-the-spire/run-domain/spec.md)及已有选择/升级能力，再接真实选项与独立参考。不得通过删除选项或换 seed 绕过缺口。
3. 保留完整事件池和独立资格表。该表限于现有 Overgrowth/第一幕范围，跨幕接入时须使用真实幕索引；未支持的资格继续明确失败。正常满容量、其他药水、完整事件子流程和四幕仍按主清单原条件验收。

## 本轮改动

- 修复 `PublicSemanticsChecks` 的过期总数断言。原失败为不发布集合预期17、实际18；新增项是完整生成池的隐藏临时洗牌状态。检查改为逐项验证候选成员/顺序/大小公开，完整生成池的身份/顺序/大小全部隐藏，并保留全量集合用途闭合检查。
- 新增 `Slice/Rooms/Events/SourceEventEligibility.cs`，并接入 `EventVisitLifecycle`。源身份资格与可执行效果分开；第一幕禁用事件正常跳过，不再因模块缺失默认允许。生命/金币/药水数量等已核对条件编译到 Engine IR，不另存 C# 局内状态。
- `UnknownRoomChecks` 复用受控队列，补无模块时的第一幕禁用、56/80与57/80生命阈值及未支持资格明确失败。旧未实现事件检查先安排满足真实资格的初态，继续验证未注册效果明确失败。
- 更新事件契约、接口参考、接入指南及有源证据的 dev-map 规则身份。未修改 core、参考源码、幕转换或公共房间装配文件。

原有 AttackPotion 实现、net 可选单选和工具适配来自接续前工作区；本轮核对、修复集成检查并完成验收，不将全部存量改动归为本轮新写。

## 构建、配置与验证

命令工作目录均为仓库根。隔离构建根 `.artifacts/env-progress-20261016`，日志和机器产物根 [.audit/env-progress-20261016](../../../../.audit/env-progress-20261016/)。Ironclad、进阶0、dev-map、全解锁、单人；正常比较使用 `sts2-v0.109-xxhash64@1` 与原 seed `ENV-T04-RNG-SHUFFLE-001`。参考 HEAD 为 `2089659bd07920088b93226136c72b6b6ee590ad`，工作树检查干净，未修改参考。

| 检查 | 实际入口与结果 | 证明范围 |
| --- | --- | --- |
| 共同构建 | `dotnet build AutoTheSpire.sln --artifacts-path .artifacts/env-progress-20261016`；最终rc0，0警告/0错误，`final-build.log` | 最终共同状态可构建；初次全量构建保留7条既有警告 |
| Engine | `dotnet .artifacts/env-progress-20261016/bin/Core.TheSpire.Engine.Tests/debug/Core.TheSpire.Engine.Tests.dll --suite runtime`；rc0，99项，`engine.log` | 本轮首批集成；之后未改core，不冒称最终再次执行 |
| 完整Slice | `dotnet .artifacts/env-progress-20261016/bin/BaseSpire.Slice.Tests/debug/BaseSpire.Slice.Tests.dll`；rc0，847项，`final-slice.log` | 包含本地源核对、药水行为、事件资格及身份门槛 |
| net | `dotnet .artifacts/env-progress-20261016/bin/Net.TheSpire.Tests/debug/Net.TheSpire.Tests.dll`；rc0，419项，`final-net.log` | 通用可选单选、wire与公开投影 |
| 事件专项 | Slice DLL追加 `--unknown-room-only`；rc0，12项，`event-checks.log` | 源资格/计数/访问/实际房型局部行为，含受控队列 |
| 最终严格回归 | Differential DLL执行 `test --suite regression --output .audit/env-progress-20261016/final-regression`；rc0，16双侧+1工厂自检 | 原17项未删减，比较器自检无失败 |
| 持药续战 | 同DLL执行 `test --suite normal-held-potion --output .audit/env-progress-20261016/final-normal-held-potion`；rc0，1双侧，31边界 | 完整UsePotion候选保留，正常第二场与奖励关闭 |
| 实际使用/取消 | 同DLL执行 `test --suite normal-potion-use --output .audit/env-progress-20261016/final-normal-potion-use`；rc0，2双侧，36/32边界 | 原掉落药水真实使用，生成候选、选择/取消、流与费用均比较 |
| 领取/丢弃与两场跳过 | 同DLL分别执行 `test --suite normal-potion-reward`、`test --suite normal-two-combats`；输出到对应 `final-` 目录；均rc0，2双侧/1双侧 | 正常奖励与跨房前缀回归 |
| Python公开重放 | `python -X utf8 .audit/env-progress-20261016/replay_public.py` 与 `replay_public_cancel.py`；均rc0 | 最终同构建分别53/48提交、36/32边界；候选三张、可取消、先消耗瓶、隐藏池、回答重发幂等、过期拒绝、未选清理 |

工具 DLL 为 `.artifacts/env-progress-20261016/bin/Differential/debug/Differential.dll`。最终使用 suite 的 run_id 为 `normal-potion-use@normal-potion-use@1+20261016T034338Z`，其他 run_id 逐项保存在各 `regression.json`。正常 base PublicBuildId 为 `b350ec8a0a8b3950`；公开 ruleset 为 `net:ruleset:42ae39c0fcc45188`，catalog 为 `net:catalog:e6d7067dfdf8c774`。Host SHA256 为 `1b6ebb85a3d673fffe4aa19fa6b003048b2ae6cd6213bd8b1d3f14bde4170447`；两条公开摘要均引用最终记录的 base BuildId。

冻结内容配置的 dev 身份未变：ruleset `b6472f08ed1c4f87` / build `4b14a623a7f07d57`；dev-map 因资格规则变化迁移到 `a133fa4e9df5fee9` / `e50d081c3ca5a7a6`。描述见 `event-build-descriptions.jsonl`，正常默认内容与冻结内容配置的身份分别保留。

公开重放以可见牌定义及全部公开字段核对重复牌可交换性，不把无序 visible_members 当内部槽位，不声称跨入口隐藏实例一一对应。它证明指定正常前缀，未证明完整第一幕或四幕。

## 后续事件选择与剩余缺口

独立核对命令为 `dotnet run --project .audit/env-progress-20261016/EventProbe.csproj --artifacts-path .artifacts/env-event-probe-20261016`，最终rc0，结果见 `event-independent-probe.log`。探针复用已登记的30个语义动作，分别通过现有base/参考适配器独立执行两场正常战斗；随后参考原生 `RunState.PullNextEvent()` 与base实际下一地图动作均选中AromaOfChaos。base最终因该效果未注册而明确失败。临时脚本仅在.audit，未修改参考、注入状态或作为新增严格suite计通过；它只证明事件身份，未比较事件效果/选项。首次探针编译的变量重名错误已修正。

正式客户端命令为 `python -X utf8 net-the-spire/client/run_game.py --host-dll .artifacts/env-progress-20261016/bin/Net.TheSpire.Host/debug/Net.TheSpire.Host.dll --scope dev-map --seed ENV-T04-RNG-SHUFFLE-001 --output .audit/env-progress-20261016/event-normal-public --max-actions 180 --potion-policy take`。实际rc4，46次成功提交后第47次下一Unknown入口被backend_fault拒绝，原始帧与summary保留；两场正常战斗已结束，但没有AromaOfChaos的效果模块。此客户端策略与严格比较不同，不拼接成同一轨迹证据。

第一幕资格表仍有ByrdonisNest、LuminousChoir、MorphicGrove、WoodCarvings、SlipperyBridge的明确未支持项。满容量正常前置路线、其他药水生命周期、事件战斗返回、完整Ironclad四幕、五角色、最终120对和真实性/成本验收均未完成；不关闭ENV父项或恢复训练。

## 主责与维护

通过[dsh-remote-operator技能](C:/Users/26877/.codex/skills/dsh-remote-operator/SKILL.md)只读查询已登记ENV-T10会话：`session-fa7f87ac-e285-452a-801a-ad4604e6c431` 为running=false、agentAvailable=false、asOfSeq=56。未向其发送消息、恢复或派新会话，不把停止当交付；幕转换文件与原认领保留。

当前行动与完成证据已回写主清单；本记录已并入开放项并从tofix转records，移动不表示ENV父项验收。未提交工作区改动，参考保持只读；接手自查可定位目标、成果、下一步、验收条件、缺口及唯一维护位置，没有独立读者审阅。

文档检查扫描561份Markdown，0问题，rc0；`git diff --check`返回0。记录索引补齐后再次执行同一门槛，实际结果保存在`.audit/env-progress-20261016/docs-final.log`。
