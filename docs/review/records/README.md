# review 历史证据索引

这里按主题查找交付证据。当前工作从[主清单](../tofix/README.md#当前交付)进入，后续重构读[内容与行为目标](../../design/mod-the-spire/content-behavior-refactor-goals.md)，当前代码结构见[base 组织说明](../../design/mod-the-spire/base-architecture.md)。记录中的测试数字、缺口和下一步只适用于其日期与构建，不覆盖当前实现。

## 最近交付与本轮维护

| 主题 | 证据与阅读边界 |
| --- | --- |
| 内容与行为目标、review 清理 | [2026-10-19 本轮记录](2026-10-19/behavior-refactor-review-2026-10-19.md)：直接读现行 DSL、锁定参考与原版，保存搬移映射及实际文档检查；没有生产重构 |
| 完整 DSL 内容模块与 Power 模型迁移 | [2026-10-19 交付](2026-10-19/content-model-migration-2026-10-19.md)：定义边界、私有状态、25 个独有卡牌效果、普通攻击数值和 SelfFormingClay 实际开发实验；含首次失败与剩余边界 |
| 检查筛选与 CI 去重 | [2026-10-19 工作流审阅](2026-10-19/base-completion-workflow-review-2026-10-19.md)：仅查当时工具／流程证据；其中重构候选已有上述迁移与新目标承接，勿照旧缺陷重新派单 |

## 当前 DSL 能力的来源记录

| 要核对的能力 | 交付证据 |
| --- | --- |
| 纯包切换、旧检查去向 | [首批切换](2026-10-18/base-dsl-native-cutover-2026-10-18.md)、[冻结检查清单](2026-10-18/base-dsl-check-disposition.json) |
| 实例通知与 pure 修正 | [统一监听](2026-10-18/effect-listener-unification-2026-10-18.md) |
| Ironclad 卡牌机制 | [完整单人卡牌](2026-10-18/ironclad-full-cards-2026-10-18.md)；这份记录早于内容模型迁移，不能据其写法推断当前内部结构 |
| 支付 X 与效果 X | [ChemicalX](2026-10-18/relic-chemical-x-2026-10-18.md) |
| 永久牌组与战斗副本 | [永久牌与营火锻造](2026-10-18/permanent-deck-smith-2026-10-18.md) |
| 角色初态与奖励 | [Ironclad 初态](2026-10-18/ironclad-character-start-2026-10-18.md)、[战斗卡牌奖励](2026-10-18/ironclad-card-rewards-2026-10-18.md) |
| 随机机制 | [原版随机恢复](2026-10-18/vanilla-rng-restore-2026-10-18.md)；正常 Run 前置消费仍需各自证据 |
| 遗物攻击修正 | [无状态修正](2026-10-18/relic-stateless-attack-modifiers-2026-10-18.md)；不是完整攻击命令边界 |
| 人类开发指南与技能 | [手册首版](2026-10-18/base-development-manual-skill-2026-10-18.md)、[模型交付中的手册同步](2026-10-19/content-model-migration-2026-10-19.md#合并与手册同步同日) |

准确操作以[开发手册](../../development/base-spire/README.md)和当前 DSL 为准，历史交付不作为 API 参考。正常 Run、来源、独立比较、公开接口和完整环境分别结算。

## 切换前证据与完整历史

2026-10-18 以前的 C# base 交付均为切换前历史，不证明当前 DSL 已恢复对应能力。旧会话和交接不得自动续接，迁移验收意图仍保留。

- [整理前的完整历史索引](index-before-2026-10-19.md)：只用于查日期／任务／原证据；包含本次已迁移的引用，不作为日常加载入口。
- [2026-10-17 连续 Run 证据](2026-10-17/ironclad-run-integration-2026-10-17.md)：切换前能力与缺口。
- [2026-10-17 跨幕证据](2026-10-17/default-act-transition-2026-10-17.md)：切换前开发配置，不能照旧命令证明当前正常 Run。
- [过时／撤销材料](../deprecated/README.md)：已被替代的比较与无效结果，单独保留失效原因。

本索引维护查证路径，当前状态与认领只维护在主清单。
