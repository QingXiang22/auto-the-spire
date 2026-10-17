# 开发和修正纯 DSL base 规则

本页导航至[base-spire 开发手册](../../development/base-spire/README.md)。开发者先走[配置遗物教程](../../development/base-spire/tutorials/first-relic.md)与[共享规则教程](../../development/base-spire/tutorials/shared-rule.md)，再按任务进入卡牌、Power／物品、敌人、角色／房间／Run 等领域指南。

查准确接口用[共享操作参考](../../development/base-spire/reference/operations.md)与[包格式参考](../../dsl/core/0.3/package-reference.md)；新机制按[迁移方法](../../development/base-spire/how-to/migrate-mechanism.md)写契约和验证。当前状态与认领仍只维护在[主清单](../../review/tofix/README.md#当前交付)，本页不复制支持清单。

Agent 使用[项目 skill](../../../.agents/skills/base-spire-development/SKILL.md)按任务定位，并只读取所需文档与源片段；DSH 可显式读取同一 SKILL.md，不依赖自动安装。

旧[卡牌教程](first-base-card.md)、[房间教程](first-base-room.md)和[旧开发指南快照](../../../tools/base-migration-baseline/docs/base-development-guide.md)保留迁移依据，不是当前可执行步骤。长期目录与纯 DSL 边界见[迁移设计](engine-dsl-migration/plan.md)。
