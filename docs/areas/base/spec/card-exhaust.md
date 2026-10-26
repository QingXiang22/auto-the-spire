# 消耗堆、失去生命与四张 Ironclad 牌

文档类型：参考（Reference）。关联 CORE-AUTHOR-05／**DSL-CARD-EXHAUST-01**；实施与认领见[GitHub 任务清单](https://github.com/QingXiang22/auto-the-spire/issues/28)。本包接在可组合效果之后，按[纯 DSL 规格](../../../design/mod-the-spire/base-architecture.md)和[机制迁移指南](../how-to/migrate-mechanism.md)编写；状态只在 GitHub Issues 维护。

## 已核实来源与范围

新增两个共享资源能力：**消耗**（`Card.Exhausts` 对应源 `CardKeyword.Exhaust`，结算后进入新的 `ExhaustPile` 并发 `CardExhausted`）与**绕过格挡的失去生命**（`LosePlayerHp`，对应源 Offering／Bloodletting 使用的 `Unblockable | Unpowered` 伤害）。效果模板新增 EnergyEffect 与 LoseHpEffect，CardDefinition 增加 `exhausts` 参数。

迁移四张新卡：

| 内容 | 源 | 行为 |
| --- | --- | --- |
| `base-spire:impervious` | `Impervious.cs` | cost 2，30 格挡，`CardKeyword.Exhaust`；升级 +10 格挡 |
| `base-spire:offering` | `Offering.cs` | cost 0，失去 6 生命、+2 能量、抽 3，Exhaust；升级抽牌 +2 |
| `base-spire:bloodletting` | `Bloodletting.cs` | cost 0，失去 3 生命、+2 能量，无关键词；升级 +1 能量 |
| `base-spire:bludgeon` | `Bludgeon.cs` | cost 3，32 点单体伤害；升级 +10 |

不迁移：其它消耗来源（手动消耗、弃牌消耗、能力触发消耗）、消耗时的反应钩子、虚无／保留／固有、X 费与费用修改（定义层升级费用除外）、死亡与胜负、原版目标选择。失去生命不触发受击反应，也不应用伤害修正。

## 行为要求与设计

| ID | 约定 | 判别 |
| --- | --- | --- |
| CX01 | 结算完成后按 `Exhausts` 分流：为真进 ExhaustPile 并发 CardExhausted，为假进 DiscardPile | 消耗牌不在弃牌堆；订阅 CardExhausted 时该卡已在消耗堆 |
| CX02 | 消耗不影响效果本身，牌的标识、费用与升级状态在消耗堆中保留 | 消耗牌仍按其数值结算，消耗堆实例可读出 DefinitionId 与 Cost |
| CX03 | `LosePlayerHp` 绕过格挡直接扣生命，并保留生命下限 0；不应用伤害修正 | 有 5 格挡时失去 3 生命得到 77 且格挡仍为 5 |
| CX04 | EnergyEffect 复用 GainEnergy，可以超过 MaxEnergy | 0 费 Bloodletting 在 3 能量基础上得到 5 |
| CX05 | 克隆同时保留 Exhausts 与升级状态 | Anger 的克隆与本体同关键词状态 |
| CX06 | 内容文件仅 use 模板参数，源资格、定义与 578 来源条目总数不变 | catalog 只新增四个非空 definition |

## 实施与验证顺序

1. 先加牌面 `Exhausts`、ExhaustPile 与 CardExhausted，把分流接在结算之后，确认未消耗牌行为不变。
2. 再加 LosePlayerHp 与两个效果模板，最后接四张内容与目录、公开元数据。
3. 在包外补分流、顺序、绕过格挡、能量上限与逐卡数值检查，并各生成执行结果。
4. 通过真实 stdio 验证消耗技能的公开轨迹（格挡与能量），更新共享操作参考与领域资料，最后运行 solution 构建、包行为／覆盖、受影响 net 与文档检查。
