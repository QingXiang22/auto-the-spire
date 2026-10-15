# DSH 部分交付、开发成本与 sim 控制流程复核 — 2026-10-10

负责人核对整批 DSH 的完整历史和当前工作区，直接执行新增六条检查，并发现三处可复现语义缺口。已有逐效果表示减少了部分接线，但开发仍需要理解过大的组包作用域；下一步应优先建立牌区、出牌与事件的共享契约。当前任务/状态/接续只见[主清单 T05](../../tofix/README.md#env-t05)，模块设计见[控制流程与上下文边界](../../../design/mod-the-spire/control-flow-modules.md)。

## 1. 实际工作状态

任务 `ENV-T05-ironclad-all-cards-20261009`、会话 `session-e6847f17-7dd8-4229-a7b5-017cd626a5fd`。本次 status 明确 `running=false`；完整历史固定到 seq2322，2323条记录，分页确认为无剩余。第一轮 2026-10-09 22:39:02 至 2026-10-10 00:15:45，约96.72分钟，结束原因 user aborted；随后第二轮 00:16:10～00:18:20完成汇报，用户消息为“说明一下你任务的完成度好了，我让gpt去做这些”。不是执行者自行认定整批完成。本轮没有恢复该会话或重派整批。

注册层面已新增 Whirlwind、TearAsunder、PerfectedStrike、FiendFire、SecondWind、PrimalForce 六卡；Ironclad74降级/20未支持，status1降级/1未支持。第三族 generate/drawUntil 只有部分领域节点与编译分派，未有源读取/运行接线。负责人实际工厂 Describe 为 RulesetId `baec5147cdaf7503`、PublicBuildId `a96507651c7a51d9`、SemanticsDigest `4f493c5b45682792`、CardCount75/PowerCount29/EnemyCount35；dev-map 实际构造仍报85项池缺18卡。未更新 golden，未开放完整 scope。

## 2. 实测成本

完整历史、逐调用统计和源记录在[本次 audit](../../../../.audit/dsh-context-review-2026-10-10/cost-summary.json)：history.json、page-*.json、tool-calls.json、user-steering.json、status.json、result.json。统计只按事件 data.usage 计一次，未把 stream 内重复 usage 再累计；分析脚本 analyze_history.py 可复算。

| 指标 | 实际值 |
| --- | ---: |
| 有 usage 的模型响应（两轮合计） | 406 |
| read / grep / pwsh / edit / compress | 241 / 77 / 96 / 95 / 15 |
| read 返回字符合计 | 1,116,784 |
| 模型输入上下文中位数 / 最大值（输入+缓存读取） | 88,117 / 151,828 token |
| Harness input / output / cacheRead | 1,494,511 / 317,784 / 33,696,384 token |
| Harness totalTokens | 35,508,679；含缓存读取，不能作为新增账单或独立上下文大小 |
| 含 dotnet build/run/test 的工具调用 | 34；工具调用经过时间合计496.39秒，约8.27分钟 |
| 全部工具调用经过时间合计 | 约576.56秒；存在并发/工具返回边界，不能精确等同关键路径等待 |
| Ironclad源 / 组包器 / 卡前端读取次数 | 46 / 37 / 29 |

读取常常是不同片段，不把每次读取都称重复劳动。工具耗时和模型回合之间的剩余时间不能严格归到单一原因，包含推理/生成/网络/排队；但是模型输入持续很大、细碎接线产生大量回合、测试重复触及相同前置门槛都有直接证据。此次慢不能主要解释为编译执行等待，也不能用这些统计保证换模型或重构后节省多少时间/token。

派单也有问题：上一轮虽要求局部验证，实际没有为新增族提供不受身份 gate 阻断的标准执行入口；完整批次目标与多个设计入口叠加，而共享能力仍落在巨型作用域。一次接下全部卡可以作为排期目标，但每族需要单独的小上下文和明确已实现的能力契约。

## 3. 独立检查与发现

执行者报告 `--combat-only` 345条 ok 后身份首差。`SliceChecks.cs` 先调用 DefaultIdentity，再调用新增族检查，因此该数字不能证明后面的新检查执行过。负责人用自有 ReviewProbe 链接原检查类直接执行，六条均通过；随后新增标准 `--card-completion-only` 入口再次实际运行六条，保留默认所有身份/地图 gate。

检查全绿仍遗漏以下错误，不能验收两族：

| 缺口 / 落点 | 独立反例 | 源与契约依据 |
| --- | --- | --- |
| `withCount` 词法环境；Effects/CardEffectCompiler.cs 与 CombatSlicePackage.CompileEffects 的 savedCounts | 外层h=1，消耗手牌，内层同名h=0，退出内层后应读外层1，实际伤害0；块外 savedCount h 应拒绝，实际接受并造成1 | 整批规格要求词法范围、允许遮蔽且不得泄漏；当前字典登记后没有退出/恢复，分支也共用 |
| `Player.HpLossCount` 战斗寿命 | Bloodletting实际掉血后计数1，正常清场并 EnterNextRoom，期望0、实际仍1 | TearAsunder按本场战斗掉血事件计数；字段自增却没有下一战斗重置，回合内检查不足 |
| PrimalForce 的 TransformCardSteps / 新检查中的“就地”预期 | 原 Strike 实例 ID 仍在使用，DefinitionId 被改成 GiantRock | sim CardCmd.Transform(:244起)创建/接入 replacement、移除旧实例并保留位置；原版 sts2 CardCmd.Transform(:406起)亦插入 replacement、记录生成及入战事件。不是原实例字段改写 |

复现代码与输出分别为 ReviewProbe/Program.cs、scope-probe.jsonl、history-reset-probe.jsonl、transform-probe.jsonl。都是受控初态/正式 base IR 执行；变形的源比较采用真实方法源码，本轮没有跑 sim 双侧适配器。原版源定位与本地 sim 位置一并核对，未改参考实现。

## 4. 可借鉴的 sim 结构

sim 的规则内容调用 CardPileCmd/CardCmd/AutoPlayCmd，CardModel 统一拥有付款、资源快照、重复出牌、before/start/body/finish/after、结果区和一次性费用清理；CombatEngine 承担外部动作/回合边界，RunEngine 处理房间/奖励。真正可借鉴的是这些共享操作的契约、调用图与时序，不是把 sim 的 C# 状态对象和 async 调度引入 core。

当前 CombatSlicePackage 5,054行、SpireCardSource 2,042行、SourceEffectPrograms 480行，新增牌区操作仍在组包函数内闭包引用大量符号。仅分文件不足以降低心智负担；需要按能力分组的只读符号、Declare/Bind两步组装、词法编译环境、牌区共享过程和出牌/事件生命周期边界。设计和实施顺序见[模块设计](../../../design/mod-the-spire/control-flow-modules.md)。

先修上述三个反例并用真实 Transform 抽取牌区模块，然后实现共用出牌管线/事件，再让卡族复用。core 保持冻结；现有实体、查询、局部槽位和 procedure 足以启动这些 base 改良。不要把未实施方案写成已完成，不在共享运行路径复用参考以失去双侧独立性。sim README 的 v0.111.0、顺序/重键模式及有意偏离要逐方法核对，不能无条件把全部 sim 输出当原版裁决。

## 5. 本次已实施的反馈改良

生产改动只在 BaseSpire.Slice.Tests/Program.cs 添加 `--card-completion-only`，未修改 DSH 卡牌规则、身份 golden 或 core；base开发指南给出构建后直接DLL的局部命令、真实执行判据和按机制准备小上下文的方法。模块拆分仅完成设计，尚未生产实施。

实际串行验证：ReviewProbe构建及六条族检查rc0；反例探针rc0且输出上述错误；Slice.Tests构建rc0，局部入口六条全部执行通过；现有Engine DLL 95/95 rc0。构建/局部/Engine日志为 probe-build.log、family-checks.log、local-entry-build.log、local-entry.log、engine.log。文档检查见doc-check.log及diff-check.log。

本轮没有重跑完整Slice/net或更新身份，它们不列为通过；正常完整Run、独立双侧、真实客户端未执行。复核留下三处待修，没有把“局部六条通过”作为原版卡牌验收。后续负责人先做共享能力修正，再安排剩余卡；本次未恢复被用户停止的远程生产。
