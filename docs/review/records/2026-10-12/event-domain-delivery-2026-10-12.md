# 事件领域接口与规格实施交付 — 2026-10-12

普通事件现在描述选项和领域效果，共享编译器拥有动作合法性、继续/结束访问与致死结算。负责人先建立[事件领域契约](../../../design/mod-the-spire/event-domain/spec.md)，再实施`RunResources`和`ChoiceEventModule`并迁移真实SunkenStatue及两个独立例子；不修改core、不引用sim运行时。当前任务状态只查[主清单T09](../../tofix/README.md#env-t09)。

## 接手入口与实现

新增已有能力事件从[作者步骤](../../../design/mod-the-spire/how-to-add-a-room.md#新增一个具体事件已有能力)进入，准确接口见[房间参考](../../../design/mod-the-spire/room-compile-reference.md#横向注册地图入口与事件)。组合已有能力引用现有契约，不逐事件新建规格/工作包/镜像测试。缺共享机制先给对应领域主责源方法与最小案例。

| 模块 | 本次交付的职责 |
| --- | --- |
| [Domain/Run/RunResources](../../../../tools/base-migration-baseline/BaseSpire/Run/RunResources.cs) | 整局金币、当前支持范围的脱战不可格挡/无加成伤害、存活治疗与Defeat声明；输入快照和边界只实现一处 |
| `GoldGainHooks` | 编译期Modify、AfterModifying、AfterGained适配；正式当前参与者为空/恒等，测试用Engine指令验证源顺序和一次求值 |
| [EventChoices](../../../../tools/base-migration-baseline/BaseSpire/Rooms/EventChoices.cs) | 不可变选项定义、可选资格、Continue/Finish；生成Role、身份/phase/存活守卫，全部效果后清理访问并处理死亡 |
| `EventCompileContext.Resources` / `RoomCompilation` | 明确装配Run资源能力与既有Defeat符号；特殊旧模块接口保留，普通新模块缺能力时建包期失败 |
| [SunkenStatueEvent](../../../../tools/base-migration-baseline/BaseSpire/Rooms/Events/SunkenStatueEvent.cs) | 选项只调用GainGold/DamageOutsideCombat；不再手写Role、守卫、金币/生命写入或死亡尾；源进入变量仍由本事件拥有 |
| [GoldEvent](../../../examples/base-events/GoldEvent.cs) / [HealEvent](../../../examples/base-events/HealEvent.cs) | 两个独立文件只写身份和选项效果，复用相同编译器；原可见结果保持 |

DDD在此落为资源、事件访问、源选择和内容的明确边界；SDD落为输入/状态归属/时序/失败/公开结果和判别案例的同一契约。对象只持编译符号/定义，实际游戏状态与暂停/恢复继续由Engine管理。

## 独立共同验证

A/B/C已核对停止seq1390/495/750，其源码交付保持；本次串行集成，不恢复旧会话。固定共同输出为`.artifacts/parallel/owner-event-domain-20261012`，工作目录仓库根。[机器可读结算](../../../../.audit/owner-event-domain-verification-20261012.json)与下列日志可复算：

| 实际命令/范围 | 结果 | 证据 |
| --- | --- | --- |
| `dotnet build AutoTheSpire.sln --artifacts-path <上述绝对目录>` | rc0，0错误；最终增量构建1个既有nullable警告 | [构建](../../../../.audit/owner-event-domain-solution-20261012.log)，先行base构建的4个既有警告见对应日志 |
| 同一闭包Engine / Slice / net DLL串行 | rc0；99 / 789 / 418全部通过 | [Engine](../../../../.audit/owner-event-domain-engine-20261012.log)、[Slice](../../../../.audit/owner-event-domain-slice-20261012.log)、[net](../../../../.audit/owner-event-domain-net-20261012.log) |
| `Slice.Tests.dll --event-domain-only` | rc0，4项契约判别通过 | [局部](../../../../.audit/owner-event-domain-checks-20261012.log)：金币hook/快照/连续调用/零负值、Continue/Finish与有界治疗、实际Sunken非致死/致死与清理顺序 |
| `dotnet run --project docs/examples/base-events --artifacts-path <独立目录>` | rc0，两个文件例子与重复ID/未知ID反例通过 | [例子](../../../../.audit/owner-event-domain-examples-20261012.log)；受控，不是原版完整事件验收 |
| `Differential.dll test --suite regression --output .audit/owner-event-domain-regression-20261012` | rc0；原17全执行，16双侧passed+1self_checked，自检0失败 | [严格结果](../../../../.audit/owner-event-domain-regression-20261012/regression.json)，使用B已交的原重洗ID源兼容升级，不删原分母 |
| A已交只读公开包装`drive_unknown.py`，新共同Host，seed ENV-T03-SEED-L/dev-map/max48 | rc3，仅step_limit，48次正常合法提交 | [公开日志](../../../../.audit/owner-event-domain-public-20261012.log)、[完整帧与摘要](../../../../.audit/owner-event-domain-public-20261012/) |

正常公开尾：第46步进入Unknown，源判定Event/SunkenStatue；第47步DiveIntoWater使金币73→179（+106）、生命67→60（-7），清当前事件并phase8→2；第48步继续选择下一商店，phase7。没有注入生命、金币、事件或清场；包装只选择当前公开候选。整局未完成，步数上限不算通关。

致死案例使用显式受控初态Hp=7/3和本页金币106，实际调用生产Sunken模块；共享尾在资源效果后清当前事件/关闭访问，再发布Defeat/Turn0，状态Terminal且无合法动作。它验证机制，不能代替正常整局死亡轨迹。

身份：dev仍`2dab0c5b3c3a4034`/`4a08d5bbaf4270be`；dev-map由A交付的`e19e4e5b63d0dd01`/`110a5b7867698e0c`迁为`639ca07b5a486fbd`/`6127d4d3959c048c`。原因是事件选项守卫、金币输入快照和致死尾的IR变化；只迁移对应地图golden，普通dev不含事件、身份不变。[实测身份](../../../../.audit/owner-event-domain-identities-20261012.jsonl)。

## 范围与后继

原版金币hook参与者、完整脱战伤害修正/死亡阻止、复活、SwordOfStone获得及效果、复杂事件战斗与跨幕仍未完成，当前支持范围在规格中明确。公开原包装通过Sunken后仍缺通用客户端事件策略；不把audit包装当正式完整运行器。真实事件第二选项尚未支持，不省略它或缩源池。

后继沿真实流程提取长期牌组/遗物等完整领域命令，再接事件或幕转换；新增已有能力事件只写自身内容和组合/池资格。未启动AI、批量卡牌或召唤复活，没有重构core架构。独立正常事件/奖励原版双侧、完整多幕胜负和真实客户端仍未执行；当前局部/原17不能关闭M01/T09完整父项。

mod AGENTS加入机制规格/领域操作约定，导航、操作指南、接口参考和组织解释同步；按Diátaxis分工与NBJ写作原则维护。文档检查扫描448篇，返回4仅既有两份交接四处`#L`锚点，新链接没有报错；diff-check返回0。[文档日志](../../../../.audit/owner-event-domain-doc-check-20261012.log)与[diff日志](../../../../.audit/owner-event-domain-diff-check-20261012.log)，未用文档取代行为验证。
