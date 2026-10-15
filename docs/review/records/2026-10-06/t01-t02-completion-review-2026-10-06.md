# ENV-T01/T02 完成声明独立复核（2026-10-06）

本报告按用户“审一下T01和T02的完成度是否如文档所述”派单完成。当前状态与接续唯一维护于[主清单](../../tofix/README.md)；本报告保存复核证据，不接管生产编码，不关闭父项或开放身份。

## 1. 结论与声明核对

| 任务 | 文档原声明 | 独立核对 |
| --- | --- | --- |
| ENV-T01 | 源 float32 分桶、53位一次抽取、小数权重、4项判别检查和后端身份已落盘；Describe/限制文案需收口 | 算术、IR、工厂后端注入与4项检查成立；Describe/NotImplementedReason仍误报已支持能力。真实分数敌人TwoTailedRat仍未加载；受控小数权重运行不等于该敌人生产可用。B独立双侧证据及B/C宿主接线未交付，不能验收 |
| ENV-T02 | 实现已落盘、4套验证全绿，只待B双侧；余效不会被终局截断，长期实例边界明确 | Hp/MaxHp提交、Feed跨房和现有5项检查成立，验证数字可复现；但效果步骤表末尾仍早于完整出牌动作，终局会截断后续钩子和结果区处理。稳定长期来源与同定义不同实例永久变化合同尚未完成，不能称仅待B |

审阅结论为两项均保持进行中。T01须收口描述并补对应集成证据；T02先补生产边界与实例来源，再交双侧验证。未按检查数量换算百分比。

## 2. 发现：T02仍在完整出牌动作结束之前设终局

这是代码行为缺陷。`CombatSlicePackage.cs:2988` 在每份 `StepInstructions` 末尾调用 `BattleEndTail`；它最终进入 `VictoryTail`，在 `:956` 设置终局。但出牌动作在效果链之后还有 `PlayedHookChain("after")`、能力出牌后触发、`GrowChain`、结果区处理（`:4036-4063`）。条件尾也在主步骤表之后组装（`:4558-4562`）。因此“步骤表末尾”不等于“父动作全部余效结束”。

引擎 `Session.Execution.cs:14` 只在 Running 时继续执行，`SetTerminal` 是冻结契约的稳定终点。不能依靠终局之后再运行 `CommitIfSettled` 修补这个问题。

独立隔离探针实际结果：

```text
formal-final-feed: status=Terminal inPlay=True exhausted=False runHp=86
after-hook lastRoom=False: status=Running playerHp=60 runHp=60 expected=60
after-hook lastRoom=True: status=Terminal playerHp=53 runHp=53 expected=60
```

第一条使用未修改正式源Feed：第一房清场→进入第二房→Strike击杀前排→Feed击杀最后敌人；Feed本体MaxHp余效已写回，但终局时Feed还在InPlay，没有完成进入消耗区。后两条只在隔离模型为Feed添加 `after_card_played = heal:7`，将战斗Hp设为50；Feed余效先加3，出牌后回血应再加7。非末房得到60，末房停在53，明确区分了“本体余效已修”与“完整父动作未修”。

复现命令：`dotnet run --project .audit/t01-t02-review-2026-10-06/probe/Probe.csproj`，rc=0代表探针执行成功，其输出**证实缺陷**，不代表规则通过。代码及输出见[探针源码](../../../../.audit/t01-t02-review-2026-10-06/probe/Program.cs)与[原始结果](../../../../.audit/t01-t02-review-2026-10-06/probe.log)。

边界：该探针复用 `FormalSpirePackage` / 正式Feed降级与生产IR，直接设置目标Hp构造受控末杀；非卡数据沿用 `.author` 诊断模型及两房终局。这不是正常SinglePlayer四幕胜利，也不是双侧或客户端证据。当前dev工厂关闭按房计数胜利，因而上述末房终局缺陷不能表述为已在正常dev四幕轨迹发生；它直接推翻了T02已宣称通过的“最后一房余效不会截断”范围。

必要修复方向：把胜利/终局判断放到能够保证父动作、条件、出牌钩子、实例变量、结果区及相应生命周期全部结束的边界；不能只把Feed特判为已通过。补末杀后的钩子、条件、结果区/消耗反应判别检查，再由B执行 `last-kill-before-terminal`。

## 3. 发现：T02稳定来源与重复定义实例合同未完成

这是完成范围缺口。[实施约束](../../../design/environment-implementation-constraints.md) 的 `same-content-card-permanent-change` 要求两张同定义长期牌、只改变指定实例，并使临时副本/升级/移除的来源关系可追踪；[工作包](../../../design/mod-the-spire/base-spire-implementation-plan.md)明确要求长期牌→战斗副本稳定来源。

实际 `CopyCardFields`（`CombatSlicePackage.cs:3055-3069`）只复制DefinitionId、升级/费用/数值/变量等值；换房 `:3526-3530` 创建后没有保存源实例引用。运行时Card字段没有源长期实例字段，关系只有KnownReveal与Powers。能证明逐张复制，却不能在相同字段的重复牌之间从副本反查长期来源，也没有对应永久写回/移除路径。

新第五项检查（`SliceFrontendSettlementChecks.cs:206-250`）只验证实例不重叠、临时费用清零与长期成员保持；使用每卡一张诊断牌组，按DefinitionId找 `longTwin`。它没有覆盖同定义不同实例的永久变化，更不能代替来源关系验收。

占位奖励 `UpgradeLongTermCard` 的参数确实是所选长期实例；按DefinitionId分派是选择变体值，不会因此把所有同定义牌一起升级。本报告不将这条正确路径误报为串改牌缺陷。缺口是战斗副本稳定来源及适用永久变化的映射没有交付。

## 4. 发现：T01的限制描述仍误报已支持能力

`BaseSpireRuntimeFactory.cs:167-168`仍称Dexterity加性格挡未接入，`:180`仍把addCards笼统列为不支持；`RunConfiguration.cs:60-64`仍称遭遇编队与addCards尚缺。

实际运行时 `EnemyBlockSteps`（`CombatSlicePackage.cs:2613-2630`）和玩家格挡链均展开加性修正；`SpireEnemySource.cs:497-565`已支持受限定牌区/目标的addCards；本轮Slice日志中的出生加牌、编队与实际入池检查通过。需要按已支持子集/具体限制描述，避免把整个能力写成缺失。这是T01已明确包含的描述收口，不因算术检查通过而视为完成。

加权数值源码复核本身未发现反例：`SourceWeightedPick.Pick`调用LINQ float Sum（double累加器末尾转float），按源NextFloat的double乘法后一次转float，再顺序float减法与 `<=0`；`EvaluateRandom`一次53位抽取，门槛置零；后端及计算资产进入RuleSet身份，正式工厂已注入后端。4项新增检查与既有状态机/计数检查实际通过。注意工作包概述“float求和”应按这个真实LINQ数值契约理解，不能另改为逐项float累加。

TwoTailedRat作者源分数权重确已改为 `0.083333336` 与 `0.75`；本轮构建仍因variables/conditions/modifyEnemyVariable/summonEnemy/modifyCombatState/具名条件缺失将其列为unsupported。小数权重证据来自受控状态机与独立复算，未扩大到该原版敌人生产覆盖。

## 5. 集成证据及独立验证

B `BaseBackend.cs:125-126` 和C `Control/ControlSession.cs:55` 仍直接new Session且没有计算后端注入，也没有消费共同正式工厂。T01原行已明示此依赖，因此这是未完成证据/接线范围，不是本报告新推断的已验收回归。旧net检查全绿没有证明新敌人随机分支可经Host执行。

| 命令 | 实际结果 / 证据 |
| --- | --- |
| `dotnet build AutoTheSpire.sln` | rc=0，0错误/0警告；[build.log](../../../../.audit/t01-t02-review-2026-10-06/build.log) |
| `dotnet run --no-build --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | rc=0，319/319；[slice.log](../../../../.audit/t01-t02-review-2026-10-06/slice.log) |
| `dotnet run --no-build --project core-the-spire/Core.TheSpire.Engine.Tests` | rc=0，95/95；[engine.log](../../../../.audit/t01-t02-review-2026-10-06/engine.log) |
| `dotnet run --no-build --project net-the-spire/Net.TheSpire.Tests` | rc=0，393/393；[net.log](../../../../.audit/t01-t02-review-2026-10-06/net.log) |
| 隔离探针 | rc=0，上述末房截断实际复现；仅引用已构建DLL，obj/bin隔离，未改生产代码 |

实际dev身份：`base-spire-singleplayer-dev-v1`，build=`999c7555a421328f`，ruleset=`d3d73029a6e9df82`，source=`sts2-frozen-2026-10-05`，content=`2efc070b861f1181`；68卡/29Power/35敌人、10张起始牌、15房/12遭遇、134未支持/8排除。相关源码/构建DLL/原版随机源码12文件SHA-256见[hashes.json](../../../../.audit/t01-t02-review-2026-10-06/hashes.json)。正式构建Profile为vanilla源映射；隔离卡牌探针seed=`20261001`，使用夹具默认Profile，不将其作为源RNG双侧证明。

未执行严格regression、新T01/T02独立参考双侧case、正常真实地图闭环/四幕胜利、120对矩阵、真实客户端或Release性能；严格test/regression入口尚未实现。两项不能仅由319/95/393绿色申请验收。

主清单已合并上述开放项并链接本报告，移入records只是保留证据。文档检查结果见主清单本轮维护记录与审阅目录日志。未修改生产实现、core或参考，未派agent、提交Git或启动训练。
