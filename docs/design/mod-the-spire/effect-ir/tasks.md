# ENV-T06 中间表示与职责边界实施清单

本清单是[规格](spec.md)对应单包实施步骤，不另维护生产任务状态/优先级；当前认领见[主清单](../../../review/tofix/README.md#env-t06)。全部由 Codex 亲自串行完成。

- [x] T001 保存源快照，并在 `BaseSpire.Slice.Tests/EffectProgramChecks.cs` 固定语义反例与对照。
- [x] T002 [US1] 在 `BaseSpire/Slice/Effects/` 定义逐效果模型与数值/目标表示，接入 AuthorCard 变体程序。
- [x] T003 [US1] 在 `BaseSpire/Slice/SpireCardSource.cs` 保留逐效果参数与独立引用，不再用卡级槽解释组合。
- [x] T004 [US2] 在领域前端保留顺序/嵌套条件和重复，建立明确的旧步骤适配边界。
- [x] T005 [US1] 在 `BaseSpire/Slice/Effects/` 实现领域程序到现有 IR 的编译器，接入正式出牌路径。
- [x] T006 [US3] 初次在 `BaseSpire/Slice/CombatSlicePackage.cs` 明确效果/动作生命周期接口，保持钩子、末杀、永久提交时序；拆分后的维护入口见[编译模块参考](../package-compilation-reference.md)中的`CardCompilation`、`SettlementCompilation`和`ActionCompilation`。
- [x] T007 [US2] 在 `BaseSpire.Slice.Tests/EffectProgramChecks.cs` 验证升级、动态读值、条件/重复/目标暂停和边界行为。
- [x] T008 更新作者/接手文档与本包证据；串行运行构建、Engine/Slice/net、内容加载及文档检查。

依赖顺序：T001→T002→T003/T004→T005→T006→T007→T008。本包不启用并行 agent；实现中发现的模型选择和实际验证保存在设计/交接证据中。

本包步骤均已执行；勾选不表示完整环境验收。完整 Slice/net 地图门槛受 ENV-T05 奖励池对账阻断，规格 SC-05 尚待补证；局部行为、默认 dev 与隔离前后证据见[交付记录](../../../review/records/2026-10-09/effect-ir-boundaries-2026-10-09.md)。
