# 被替代与撤销的 review 材料

这里保留无法继续充当当前设计／验收依据的历史材料。日常只读[主清单](../tofix/README.md)与[内容和行为重构目标](../../design/mod-the-spire/content-behavior-refactor-goals.md)，需要查历史原因时再打开本目录。

| 材料 | 失效原因 | 当前替代入口 |
| --- | --- | --- |
| [2026-10-12 流程和扩展接口对比](sts2-sim-flow-extension-comparison-2026-10-12.md) | 比较切换前的 C# base 与当时带修改的 sim 工作树，不描述当前纯 DSL 实现 | [重构目标](../../design/mod-the-spire/content-behavior-refactor-goals.md) |
| [2026-10-01 sim 补全和保真差异](sts2-sim-补全与保真差异-2026-10-01.md) | 记录曾修改的参考工作树及旧版本结论，不能转作当前锁定只读参考的能力声明 | [锁定参考](../../../reference/baselines/sts2-sim.json)、[参考使用说明](../../../reference/README.md) |
| [2026-10-15 随机初始化接续](env-t04-rng-bootstrap-2026-10-15.md) | 原修改已撤销，原报告明确其修复结论与验收产物无效 | [主清单 ENV-T04](../tofix/README.md#env-t04) |

文件移入本目录不删除尚未完成的产品要求。验收合同及剩余范围仍在主清单；有效的历史交付保存在 records，与已撤销结论分开。
