# 敌方阶段与 FuzzyWurmCrawler

关联 CORE-AUTHOR-05／**DSL-ENEMY-TURN-01**；实施与认领见[主清单](../../../review/tofix/README.md#当前交付）。本包接在战斗结束之后，按[纯 DSL 规格](../engine-dsl-migration/spec.md)和[机制迁移指南](../../../development/base-spire/how-to/migrate-mechanism.md)编写；状态只在主清单维护。

## 已核实来源与范围

从 `sts2/src/Core/Models/Monsters/FuzzyWurmCrawler.cs` 核对：Ascension 0 的初始 HP 55..57；招式为 `FIRST_ACID_GOOP`（4 点攻击）→ `INHALE`（对自身施加强度 7）→ `ACID_GOOP`（4 点攻击）→ 回到 `FIRST_ACID_GOOP`，三个状态的后继关系确定，没有随机分支。两个 goop 状态伤害与意图相同，因此用 `MoveIndex` 0／1／2 表示这三个招位。

本包新增：敌方阶段（玩家回合结束后按可命中敌人各发一次 `EnemyTurnStarted`，随后 `AfterEnemyTurnEnded`）、敌方攻击链 `DamagePlayerPowered`、敌方公开意图名与招位字段，以及第一个原版敌人内容。debuff 减层从玩家回合末移到敌方阶段结束时，回到源的时点。

不迁移：其余 120 个敌人、遭遇编队与队列、意图伤害数值（只公开意图名）、原版 AI 的随机分支与开场条件、初始 HP 随机区间（开发生成用最小值）、死亡钩子与生物移除、奖励与 Run 结算。

## 行为要求与设计

| ID | 契约 | 判别 |
| --- | --- | --- |
| EN01 | 玩家回合结束后依次弃手牌、发 AfterTurnEnded、跑敌方阶段（每个可命中敌人一次）、发 AfterEnemyTurnEnded，最后关闭回合门 | 敌人已死亡时跳过；结束后仍可开下一回合 |
| EN02 | 敌方攻击按源顺序结算：攻击者 Strength 加性、攻击者 Weak ×0.75、防守方 Vulnerable ×1.5，整数向下取整，再格挡→生命 | 7 强度的 goop 打 11；1 层 Weak 的 4 点打 3；有 10 格挡时只掉 4 点格挡里的 4 并与剩余格挡共存 |
| EN03 | 敌人内容模板按 DefinitionId 订阅 EnemyTurnStarted 并维护自己的 MoveIndex／Intent | 三个招位按源顺序轮转，Intent 随下一招更新 |
| EN04 | debuff 减层发生在敌方阶段结束后 | Bash 施加 2 层 Vulnerable，结束玩家回合后变 1 层 |
| EN05 | 内容文件仅 use 模板参数；敌人目录 121 源分母不变，只新增一个 definition | catalog 只改 FuzzyWurmCrawler 一行，其余保持 unreviewed |

## 实施与验证顺序

1. 先加敌方阶段与 `DamagePlayerPowered`，确认无敌人时回合流程不变。
2. 再加敌人字段、内容模板与目录／公开元数据。
3. 在包外补轮转、修正、减层时点与目录分母检查，并通过真实 stdio 验证公开 HP 与意图。
4. 最后运行 solution 构建、包行为／覆盖、受影响 net 与文档检查。
