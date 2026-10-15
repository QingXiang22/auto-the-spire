---
task_id: CORE-AUTHOR-04
dispatch_id: CORE-AUTHOR-04-dsh-continue-20261011
session_id: session-00c5db45-5e96-4b1c-aa57-a6e6771109d5
dispatch_record: .audit/dsh-core-author-04-2026-10-11/task-record.json
delivery_status: ready_for_review
updated_at: 2026-10-11T12:05:25+08:00
---

# 作者运行时重构接续交接 — 2026-10-11

本包交付统一卡牌定义、同包文本生命周期、base/0.0.2 作者输入和完成图复用。实现及最终 Slice 检查已交，请求 Codex 复核；严格回归有两项未通过，不声明完整环境验收。

入口：[CORE-AUTHOR-04 当前详情](../../tofix/README.md#core-author-04)、[派发记录](../../../../.audit/dsh-core-author-04-2026-10-11/task-record.json)、[完整原输入](../../../../.audit/dsh-core-author-04-2026-10-11/task.txt)。Codex负责最终结果复核；未关闭本任务或父项，未提交、推送、打标签，未修改参考生产代码或suite分母。

## 接手第一步

先核对当前清单、共享改动及本包最终日志。不要重做已交core/0.3绑定与新作者前端。复核重点：统一definition的执行权威、真实文本调用和缺上下文拒绝、升级实例增量、来源伤害计算与地图缓存输入隔离。严格回归的最小复现为当前隔离 Differential 产物执行 `test --suite regression --output <目录>`；其两项结果须交ENV-T04/DIFF-02负责人裁决，不能删case或将不可判定计绿。最后完成Codex独立复核；本报告自查不等于独立验收。

## 实际实现与旧新语义

### A：统一定义与输入适配

- [CardDefinition/CardVariant](../../../../mod-the-spire/base-spire/BaseSpire/Slice/Effects/CardDefinition.cs)保存program/hooks、cost/X、结果区、变量、关键词及卡级池/标签/升级资格。正式dispatcher、支付、生成/变形、升级、保留/Innate、休息升级及奖励配方消费规范化定义。
- [CardDefinitions](../../../../mod-the-spire/base-spire/BaseSpire/Slice/Orchestration/CardDefinitions.cs)生成实例初始化全部变量（未声明值为0），升级增加定义差额，保留已有成长与费用修饰。变形不残留原定义变量。
- [LegacyCardAdapter](../../../../mod-the-spire/base-spire/BaseSpire/Slice/Effects/LegacyCardAdapter.cs)仅在输入时将诊断.author变为同一typed program。X回归根因是literal amount=0错误成为常量，而支付已经正确捕获PaidX；修为PaidX表达式，复用原3/2/0和支付时点检查。诊断自目标药水亦在输入层修复。
- 旧步骤执行器已删除，不以空program回退。AuthorCard旧摘要及Amount/PowerId/Stacks实例字段仍供兼容展示，不作为正式效果执行权威。Thrash不再读Amount摘要：使用variant typed DamageValue，经原 outgoing damage modifier后成长再消耗；BodySlam正常/升级的Block7+Strength2判别分别得到13/15。
- 当时`CardCommandCompiler`承担命令，中央CombatSlicePackage保留组包接线；源链接Generate/Becomes内容引用，完整variant统一hooks派发。（2026-10-12链接维护：该类已由[类型化效果编译器](../../../../mod-the-spire/base-spire/BaseSpire/Slice/Effects/CardEffectCompiler.cs)与可组合原语替换，原交付判断对应当时版本。）

### B：真实文本生命周期

- [生命周期文本库](../../../../mod-the-spire/base-spire/BaseSpire/ContentSource/lifecycle-rules.spire)的core/0.3 `GrantDeferredEnergy`接收owner Ref(Player)、power Ref(Power)、amount Int，具名调用同包HostGainDeferredEnergy后destroy power。
- [PowerRuleRuntime](../../../../mod-the-spire/base-spire/BaseSpire/Slice/Orchestration/PowerRuleRuntime.cs)通过BindInto现有EnginePackageBuilder绑定；宿主保留NoEnergyGain检查，受阻仍按原时点移除。正式EnergyNextTurnPower与测试第二份mymod Recovery均由DSL具名调用同过程，不增加PowerRuleKind或第二调度器。
- 编译/链接要求完整且唯一参数；afterEnergyReset只提供owner/power/stacks，card/target不可用。缺power、错误上下文和重复参数按原文件定位拒绝。既有9项生命周期检查通过，包含叠层、NoEnergyGain、升级与命名空间。

### C：base/0.0.2 作者输入

- [CardSourceExpansion](../../../../mod-the-spire/base-spire/BaseSpire/Slice/CardSourceExpansion.cs)按显式版本扩展common及normal/upgraded编译期values；variant逐字段整体替换，无深合并。空play/hooks/keywords明确清空；重复配置、未知values和非整数保留原始位置报错。
- [SourceEffectPrograms](../../../../mod-the-spire/base-spire/BaseSpire/Slice/Effects/SourceEffectPrograms.cs)编译组合数值：PaidX、变量/local、block、pile/power来源与add/subtract/multiply/divide/minimum/maximum。typed IR执行；一般来源实时读，let及repeat次数进入时快照。递归验证未知变量/local和选中目标依赖，不忽略scope。
- 正式[Ironclad内容](../../../../mod-the-spire/base-spire/BaseSpire/ContentSource/ironclad-cards.spire)迁移Whirlwind PaidX次数、Stoke/FiendFire快照与pile算术；[次回合内容](../../../../mod-the-spire/base-spire/BaseSpire/ContentSource/next-turn-cards.spire)迁移Predator/Outmaneuver common/values；既有实例变量/hook/清空合约及[作者示例](../../../examples/base-card-mod/two-hits.spire)实跑。其他0.0.1内容显式兼容适配，不声称全仓源文件都改写为新语法。
- 新参考：[base/0.0.2](../../../dsl/0.0.2/authoring-reference.md)、[core/0.3](../../../dsl/core/0.3/authoring-reference.md)。旧冻结文档保留历史事实。

### D：地图完成结果复用

- [SourceActMapCompute](../../../../mod-the-spire/base-spire/BaseSpire/Slice/SourceActMapCompute.cs)完成结果Cells/Edges为不可变数组；BaseSpireComputeBackend只缓存本实例最后一个Done结果，以实际blob+rooms为键，未完成前缀不缓存。正式工厂每Session创建backend，不缓存可变Session/State，也不改IR随机抽取顺序。
- 两seed严格地图对拍已通过；完整Slice已执行原事实/边/流检查：alpha nodes51 words/draws292；beta nodes61 words/draws265。未测同条件前后耗时，不宣称量化性能提升。

### E：版本、身份与集成

Core.TheSpire/BaseSpire版本0.4.0，Engine身份engine/ir@0.4，core语言core/0.3，base作者base/0.0.2。未建立指向旧提交的版本tag；新版本指当前工作区元数据与隔离重建产物。旧输入继续适配，新产物不冒用旧身份。

[最终内部描述](../../../../.audit/dsh-core-author-04-2026-10-11/integration-build-description.json)：

| 范围 | RulesetId | PublicBuildId | ContentDigest |
| --- | --- | --- | --- |
| dev | 75a9d9345def3426 | 2f0fd07325835101 | 17b137ee8e3f1726 |
| dev-map | bffa765e6d81474b | 5f51e38e0e530897 | 17b137ee8e3f1726 |

两范围176卡/44能力/38敌人，起始牌组10、Hp80、Gold99。Scope包版本仍按既有范围身份合同，不将其混同程序集版本。

[公开Host描述](../../../../.audit/dsh-core-author-04-2026-10-11/public-host-descriptions.json)来自同一隔离DLL；dev-map protocol0.2、backend `net:backend:simulation-0.2-350911d86bad900d`、catalog `net:catalog:b400d7f332f590cc`、rules `net:ruleset:bf54a1e5d279a86b`。内部和net映射身份属于不同命名空间，不直接比较字符串。

先用只更换identity的审计副本跑原公开fixture，成功后才集中迁移Slice golden和正式fixture。未改seed、326候选动作、Hp/Gold/Boss断言或注入清场。

## 实际命令与返回码

工作目录仓库根；构建串行且隔离 `.artifacts/parallel/dsh-core-author-04`。日志均记录实际子进程退出码，而非Out-File返回码。

| 验证 | 实际命令/结果 | 证据 |
| --- | --- | --- |
| solution | `dotnet build AutoTheSpire.sln --artifacts-path .artifacts/parallel/dsh-core-author-04 -m:1` rc0，0错误4既有nullable警告 | [构建日志](../../../../.audit/dsh-core-author-04-2026-10-11/integration-solution-build.log) |
| Engine | 隔离Core.TheSpire.Engine.Tests.dll，99项，rc0 | [Engine](../../../../.audit/dsh-core-author-04-2026-10-11/integration-engine.log) |
| net | 隔离Net.TheSpire.Tests.dll，rc0 | [net](../../../../.audit/dsh-core-author-04-2026-10-11/integration-net.log) |
| 作者工具/例子 | 隔离BaseSpire.Dev构建rc0，`BASE_SPIRE_DEV_DLL`指向该产物运行verify_examples.py，原15次调用rc0 | [构建](../../../../.audit/dsh-core-author-04-2026-10-11/author-tool-build.log)、[例子](../../../../.audit/dsh-core-author-04-2026-10-11/author-examples.log) |
| 完整Slice | golden-only测试重建rc0；隔离BaseSpire.Slice.Tests.dll完整执行rc0，末行 `All 753 slice checks passed.`（入口前置家族检查亦执行） | [构建](../../../../.audit/dsh-core-author-04-2026-10-11/final-slice-build.log)、[Slice](../../../../.audit/dsh-core-author-04-2026-10-11/final-slice.log) |
| 严格regression | 隔离Differential.dll `test --suite regression --output .audit/dsh-core-author-04-2026-10-11/regression` rc1 | [日志](../../../../.audit/dsh-core-author-04-2026-10-11/integration-regression.log)、[完整结果](../../../../.audit/dsh-core-author-04-2026-10-11/regression/regression.json) |
| 正常公开Act1 | `NET_THE_SPIRE_HOST_DLL`指向隔离Host，act1_smoke.py使用审计identity fixture，rc0 | [日志](../../../../.audit/dsh-core-author-04-2026-10-11/integration-act1.log)、[summary](../../../../.audit/dsh-core-author-04-2026-10-11/act1-public/summary.json) |
| 文档 | check_docs.py rc2，384文件，仅其他任务交接两处#L锚点错误 | [doc-check](../../../../.audit/dsh-core-author-04-2026-10-11/final-doc-check.log) |
| 空白 | `git diff --check` rc0，只有CRLF提示 | 2026-10-11T12:01:25+08:00实际执行 |

阶段效果、编排、作者合约、次回合能量/抽牌、Thrash、X家族均通过；阶段绿色不冒充最终全量。solution新增nullable警告已消除，剩余四处为ActMap两处、SpirePowerSource、ComparatorSelfTest既有警告。

## 严格差异与未验证范围

run_id=`regression@env-t04-regression@1+20261011T035938Z`：期望/选中/执行均17；双侧passed14、单侧self_checked1、不计通过2、自检失败0，两seed地图passed。

1. `random-reshuffle-unsupported`：unsupported_rng_profile。首差 `boundary[3].pile.draw.initial-slots`：`start:05,start:00,start:08,start:03,start:07 ≠ start:03,start:06,start:04,start:09,start:01`。随机调用序列不同的证据不作规则结论。
2. `rng-initial-shuffle`：diverged。首差 `boundary[0].enemy.base-spire:fuzzy-wurm-crawler.alive: 缺失 ≠ 存在`。没有本包前后同case对比，不宣称已证实为既有差异；归因及独立参考Profile修正交ENV-T04/DIFF-02负责人，不越界改参考玩法。

公开Act1 seed=ACT1-BASIC-10，326动作，ActCompleted=true，Hp8/Gold401/Boss初始Hp173/Slippery8；无whole-run Victory。这是实际正常公开模拟侧回放，不等于独立原版全程双侧验收。参考侧整幕未执行。

未声称完整原版内容覆盖、全角色/进阶/幕、AI训练或父环境关闭。Codex还应复核伤害元数据推导边界（conditional/let内首击不自动推作卡级DamageValue）、诊断旧药水嵌套目标适配及命令UI提示兼容；本次没有为这些未经区分的疑问另加保护层或修改suite。
