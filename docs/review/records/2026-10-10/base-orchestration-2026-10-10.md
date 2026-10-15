# Base 卡牌与战斗核心编排实施复核

日期：2026-10-10。任务包 `ENV-T06-base-orchestration-20261010`，执行与复核为 Codex `/root`。用户要求亲自参考本地 sim 的职责分工重构 base，允许替换内部实现；本轮没有恢复或派发 DSH 生产。阶段、剩余范围与认领只维护[主清单 T06](../../tofix/README.md#env-t06)，完整卡牌与奖励闭环见[T05](../../tofix/README.md#env-t05)。本记录是本日期证据，不另设待办。

## 实施与职责

[spec / plan / tasks](../../../design/mod-the-spire/base-orchestration/spec.md)先定义成功条件和反例，再实施。参考 `reference/sts2-sim` 的 CardModel、CardCmd、AutoPlayCmd、CombatEngine 分工；变形、Pillage、Havoc及生成池由 `sts2/` 对应源码核对。未引用 sim 运行时，未改 core 或参考规则。

生产落点是 `BaseSpire/Slice/Orchestration/` 的15个编译期模块、`Slice/Effects/` 的领域节点/词法环境/编译器及组包绑定。它们生成冻结 Engine IR，不在 C# 对象里存储会话游戏状态。

- `CardSchema` / `CardDefinitions` / `CardPileProcedures` 区分新定义实例、长期战斗副本、克隆和变形替代。变形创建独立实体并保留原位置；原实体可供历史引用，在换房边界清理。克隆保留实例变量及当前费用状态，临时牌不带长期来源。
- `CardPlayPipeline` 是手动、自动及重复执行共用的 `base-spire:ExecuteCardPlay`。付款、X快照、正式目标各一次；每次执行经过 before/start/body/finish/after；结果区一次。仅最外层动作调用战斗结束与永久提交，末杀不会截断父余效和结果区。
- `AutoPlayProcedures` 先把所选批次全部移入在场区，再逐张调用共享流程；批次/待执行项由 Engine 实体隔离嵌套调用。当前支持抽牌堆顶部免费出牌及可选强制消耗。末杀后的剩余牌不执行牌体，但完成结果区处理。
- `CardDrawProcedures` 统一抽牌、重洗、NoDraw、手牌容量和终止条件；`CardGenerationRules` 支持显式完整候选池、升级初始化、临时免费及满手转弃牌。角色全池未完成，省略完整池仍明确拒绝。
- `CardPlayHistory` 按每次执行记录开始/完成，同一实例重复打出不再发生集合冲突；`CombatLifecycle` 管回合历史与战斗计数清理。公开 `PlayedThisTurn` 保留唯一实例及首次出现顺序，包括目标决策暂停时可见性；内部事件历史不进入公开投影。
- `EffectCompileScope` 和源校验器拥有词法计数与当前牌绑定，遮蔽恢复、兄弟分支隔离及块外诊断集中处理。正式程序不写卡级共享 Amount，旧 `.author` 走显式兼容适配器。

net 仅协调修改 `Projection/SlicePublicProfile.cs`：旧诊断夹具明确跳过 formal-only 的掉血计数及未公开的历史集合。正式声明派生投影保留其实际语义，没有逐卡注册、协议或玩法修改。

此前[审阅](dsh-context-and-sim-control-review-2026-10-10.md)复现的三处问题已修复并有持久检查：withCount 遮蔽/泄漏、HpLossCount 跨战斗不归零、PrimalForce 改旧实例。原六卡局部检查重新执行；PrimalForce 检查按源码要求验证独立替代实例、位置和长期隔离，没有把旧错误保留为预期。

## 内容、身份与实际加载

本轮没有修改既有 `ContentSource` 文件，复用了此前声明的 Pillage/Havoc/Cascade并补齐其降级/运行路径。Ironclad为 **94声明 / 94解析 / 77降级 / 17未支持**；默认正式工厂 Describe 为 **78卡 / 29能力 / 35敌人**。数量说明组包及注册范围，不代表每项都有完整原版行为证据。

默认 `dev` 身份：RulesetId `4d2653cbf9e1621c`，PublicBuildId `65f9d22c08a9e6ac`，SemanticsDigest `f236a3b6c83a557f`。共享过程/隐藏状态会改变规则身份，消费者不能沿用旧身份。身份检查在语义验证后重基线，没有通过改 golden 绕过首差。

源单机奖励池85项中，70项有可加载定义，仍缺15项：aggression、crimson-mantle、dark-embrace、drum-of-battle、hellraiser、howl-from-beyond、infernal-blade、inferno、juggling、one-two-punch、rupture、stampede、stoke、stomp、thrash。`dev-map` 工厂明确抛奖励池对账失败；没有缩池、过滤未支持成员或造空效果。

范围/身份探针使用 `RunConfiguration.Ironclad(0, Dev/DevMap)`。局部行为使用正式源及受控初态，多数固定seed `20261001`；生成示例也是该seed。记录内容见[metadata日志](../../../../.audit/base-orchestration-2026-10-10/content-metadata.log)。

## 实际验证

验证按项目串行执行，避免并发写 obj/bin。完整命令、退出码及耗时见[集成记录](../../../../.audit/base-orchestration-2026-10-10/verification.json)和[最终构建/非地图记录](../../../../.audit/base-orchestration-2026-10-10/final-verification.json)。最终补跑只针对收尾改动，不重复无关全套。

| 验证入口 | 结果与限制 |
| --- | --- |
| `dotnet build AutoTheSpire.sln` | 成功；冷构建保留既有3条warning，未新增warning |
| `dotnet run --no-build --project core-the-spire/Core.TheSpire.Engine.Tests` | 95/95，rc0 |
| Slice `--orchestration-only` | 21/21，rc0；15项新编排分组与6项既有卡族检查 |
| Slice `--combat-only` | 396/396，rc0；包括新编排、实际Pillage/Havoc/Cascade普通/升级及公开语义检查，排除地图 |
| 完整 Slice 默认入口 | 非零；首个地图工厂检查被上述15项缺卡阻断，未记为完整通过 |
| `dotnet run --no-build --project net-the-spire/Net.TheSpire.Tests` | 410 passed / 5 failed / 415 executed，rc1；5项G01～G05地图检查均受同一奖励池门槛阻断 |
| `dotnet build tools/base-spire-dev/BaseSpire.Dev.csproj` | 成功；用于外部文件作者工作流 |
| `python tools/base-spire-dev/verify_examples.py` | 最终12次调用全部通过；包括新增双卡生成例的check、普通及升级play，以及既有成功/拒绝分支 |

[编排日志](../../../../.audit/base-orchestration-2026-10-10/orchestration.log)、[最终非地图日志](../../../../.audit/base-orchestration-2026-10-10/combat-final.log)、[完整Slice首差](../../../../.audit/base-orchestration-2026-10-10/slice.log)、[net日志](../../../../.audit/base-orchestration-2026-10-10/net.log)、[最终作者12次日志](../../../../.audit/base-orchestration-2026-10-10/author-examples.log)保留原输出。集成记录中的作者耗时对应较早执行；最终作者数量以12次日志为准。

新案例覆盖同实例重放事件计数、独立Engine包调用共享过程的三次执行/一次付款/一次目标、批次选取先于子效果、嵌套末杀父余效、未可打状态牌、X快照、抽牌停止、词法拒绝、真实变形、生成升级/费用/来源/命名流消费和回合归零。它们能区分旧错误和新语义。没有运行本包独立 sim 双侧差分、正常完整地图奖励闭环、Release性能或训练吞吐测量。

## 维护成本与指南

[结构记录](../../../../.audit/base-orchestration-2026-10-10/structure.json)按开工前138文件快照核对本轮修改，确认既有内容源未改。组包器5054→4185行；新职责模块分别14～77行。价值在于共享行为和接口可局部阅读，行数本身不能换算为token节省。

[外部编排例](../../../examples/base-card-mod/orchestration.spire)只添加DSL文件即可生成普通/升级 Spark，12次作者检查证明已有能力的内容组合不需要修改C#。付款修改可定位51行的 CardPaymentRules，自动批次可定位47行的 AutoPlayProcedures；只有新增依赖或命令才进入组包绑定。开发者不必为每张卡重新实现历史、牌区、目标或结算尾。

已更新[流程职责说明](../../../design/mod-the-spire/control-flow-modules.md)、[base开发指南](../../../design/mod-the-spire/base-development-guide.md)、[可维护规则集指南](../../../design/mod-the-spire/maintainable-rule-sets.md)、[modding指南](../../../design/mod-the-spire/modding-guide.md)、[DSL作者约定](../../../dsl/0.0.1/dsl-usage.md)与[工具说明](../../../../tools/base-spire-dev/README.md)。`python tools/doc-check/check_docs.py` 扫描333份Markdown、0问题、rc0，`git diff --check` rc0；输出保存在[文档检查日志](../../../../.audit/base-orchestration-2026-10-10/docs.log)及[diff检查日志](../../../../.audit/base-orchestration-2026-10-10/diff-check.log)。

组包器仍含敌人/Power/Run/房间/奖励接线和部分旧步骤适配；全角色生成池、更多能力事件、paid/prepaid自动策略及完整原版环境未由本包完成。共享基础已落地，剩余卡族可沿对应职责扩展；T05/T06父项仍按主清单保持开放。
