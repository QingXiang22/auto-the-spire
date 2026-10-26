# base-spire 开发手册

文档类型：参考（Reference）；按读者任务导航。

规则核对先恢复[锁定 sts2-sim](../../../reference/README.md)，从 `reference/sts2-sim/src/Sts2Sim.Core/` 查当前行为；本地 `sts2/` 不再作为裁决依据。工具固定来源目录和已有向量仍记录旧 DSL 的来源，不能据此宣称新版内容已迁移。

这份手册面向维护原版内容与共享玩法的开发者。初次学习可先完成两篇教程；修改已有内容直接进入修改指南，新增独有效果进入内容模块指南。你不需要先通读旧组包器、Host 或全部恢复源码。

适用基线为纯 DSL base 包，语言为 core/0.3。包入口是 [package.json](../../../mod-the-spire/base-spire/package.json) 和 [base.spire](../../../mod-the-spire/base-spire/base.spire)。已有显式角色初态、永久牌组／战斗副本、开发战斗与卡牌选择、随机机制、奖励和营火等可复用操作；遗物与Power通过实例通知和只读修正监听参与流程。准确接口与限制查[操作参考](reference/operations.md)，实际定义及已执行执行结果查包目录和行为报告；任务认领、剩余内容及验证状态只看[GitHub 任务清单](https://github.com/QingXiang22/auto-the-spire/issues/28)。开发scope不代表含Neow的正常开局、地图和完整Run已经恢复，各领域指南也不构成全内容可玩证明。

## 从教程开始

需要仓库 `global.json` 指定的 .NET SDK 9.0.306、Python 3.12 和 PowerShell。教程在仓库根目录操作，在 `.artifacts/tutorials/` 创建隔离副本，正式包保持原规则。

1. [配置第一个遗物](../../start/first-relic.md)：加载真实包，把练习数值从 6 改为 8，通过公开 Host 观察实际结果。
2. [编写共享触发规则](../../start/shared-rule.md)：用完整 DSL 创建一个抽牌触发模板，用具体use配置调用它，观察逐抽 2／4／6 和空堆不触发。

通用 DSL 尚不熟悉时，先完成[第一份规则](../../start/first-rule.md)。本手册的命令和示例均以当前纯 DSL 包为准。

## 按开发任务查找

| 你要开发什么 | 阅读入口 |
| --- | --- |
| 修改单个卡牌数值、Power公式／私有状态或遗物收益 | [修改单个内容](how-to/edit-content.md) |
| 新增拥有独有效果的遗物／Power或卡牌模块 | [新增内容模块](how-to/add-content-module.md) |
| 已有机制下新增内容、登记源标识与公开目录 | [扩展内容](how-to/extend-content.md) |
| 卡牌、费用、目标、普通／升级、关键词、附着、伤害、回合 | [卡牌与战斗](how-to/cards-and-combat.md) |
| Power 生命周期、遗物实例与触发、药水容量与使用 | [Power、遗物与药水](how-to/powers-relics-potions.md) |
| 为遗物／Power编写通知、伤害／费用等修正，或迁移旧反应 | [编写通知与修正监听](how-to/write-effect-listeners.md) |
| 敌人、意图、行动、编队、遭遇和召唤依赖 | [敌人与遭遇](how-to/enemies-and-encounters.md) |
| 角色、进阶、开局、地图、各类房间、事件、奖励、跨幕和终局 | [角色与完整 Run](how-to/run-and-rooms.md) |
| 新增共享机制或恢复旧行为 | [迁移完整机制](how-to/migrate-mechanism.md) |
| 包加载、公开结果、错误与验证选择 | [验证与排障](how-to/verify-and-debug.md) |

## 查准确接口与理解架构

| 需要 | 入口 |
| --- | --- |
| 查当前可用 DSL 状态、参数、事件顺序和局限 | [共享操作参考](reference/operations.md) |
| 查有序正文、攻击结果、费用、复制、附魔与访问命令 | [行为操作参考](reference/card-behavior.md) |
| 查Power定义标志、施加／合并／移除与私有状态事件 | [Power生命周期参考](reference/powers.md) |
| 查所有领域的落点与前置依赖 | [领域目录参考](reference/domains.md) |
| 查includes、manifest、静态JSON和内容模块边界 | [包格式参考](../../reference/dsl/0.3/package-reference.md) |
| 理解源目录、内容自有规则与共享操作的关系 | [开发模型](../../development/base-spire/explanation/development-model.md) |
| 对照sts2-sim战斗阶段、遗物／Power生命周期与复用边界 | [战斗引擎图文对照](../../development/base-spire/explanation/combat-engine-comparison.md) |
| 审阅后续出牌、攻击、牌区、实例能力与 Run 重构 | [内容行为约定](spec/behavior.md)，目标设计不等于现有接口 |
| 用 agent 做开发，减少重复检索 | [项目 skill](../../../.agents/skills/base-spire-development/SKILL.md) |

本手册由 base 规则维护者随共享机制维护。新增一个机制时，更新准确操作参考、对应领域指南与必要教程；不逐个内容复制共享规则，也不把运行结果数量固化到 skill。交付要求见[开发约定](../../../CONTRIBUTING.md)。
