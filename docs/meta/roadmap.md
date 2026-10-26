# 总任务入口与产品里程碑

文档类型：参考（Reference）。当前产品目标是五角色完整单人环境及其公开接口。实际版本和可运行范围以 [base manifest](../../mod-the-spire/base-spire/package.json)、[核心约定](../specs/core-engine-baseline.md)及当前提交的 CI 为准。开发场景已支持部分卡牌、Power、遗物和房间机制，另有含 Neow 的有限正常开局至首战奖励返回地图路径；完整 Run、完整奖励来源及完整独立参考比较尚未验收。各项检查的实际范围见 [CI 说明](../../tools/ci/README.md)。

当前任务统一从 [GitHub 总任务 #28](https://github.com/QingXiang22/auto-the-spire/issues/28)进入，通过原生 sub-issues 连接子任务和执行工单。优先级、负责人、依赖、进度及暂停条件只在 Issue 维护。本页保留稳定入口和产品阶段定义，不同步任务状态。

## 近期队列

- [总任务及子任务树](https://github.com/QingXiang22/auto-the-spire/issues/28)：从产品目标进入能力子任务，再打开执行工单。
- [可认领执行工单](https://github.com/QingXiang22/auto-the-spire/issues?q=is%3Aissue+is%3Aopen+label%3A%22type%3Awork%22+label%3A%22status%3Aready%22)：按优先级选择，核对依赖后认领。
- [全部执行工单](https://github.com/QingXiang22/auto-the-spire/issues?q=is%3Aissue+is%3Aopen+label%3A%22type%3Awork%22)：查看执行、评审、阻塞及候选项。

派发流程、标签含义和关闭条件见 [开发约定](../../CONTRIBUTING.md#任务拆分与派发)。任务名称及旧 evidence 不能证明功能已经实现，认领时核对当前定义、PR 和实际检查范围。

产品范围见 [项目规格](../specs/project.md)，完整行为条件见 [环境验收规格](../specs/environment.md)。[遗物机制索引](../areas/base/spec/relic-combat-start.md#遗物机制索引)只用于查找共享机制，不代表当前派单顺序。

## 里程碑

| 阶段 | 完成条件 |
| --- | --- |
| M01 | 正常公开开局，地图→战斗→真实奖励→长期提交→返回地图；独立逐步比较成立 |
| M02 | 完整 Ironclad 四幕胜利与真实死亡，包含正常开局、各类房间、长期状态和跨幕 |
| M03 | 五角色、全部支持进阶与完整单人可达内容；各角色正常胜负可运行 |
| M04 | 全量覆盖、最终 120 对矩阵、差异裁决、公开全程和 Release 成本通过审查 |

开发开局首幕可作中间检查，正常首幕须包含 Neow。局部机制、内部行为测试和声明数量各自证明对应范围，整体完成仍按环境规格验收。

阶段关闭由总任务关联的实际验收决定。普通交付使用 PR 与 CI；不记录聊天会话、逐批日志或历史检查数字。
