# 能力与伤害修正参与者

关联 CORE-AUTHOR-05／**DSL-POWER-MODIFIERS-01**；实施与认领见[GitHub 任务清单](https://github.com/QingXiang22/auto-the-spire/issues/28)。本包接在消耗之后，按[纯 DSL 规格](../base-architecture.md)和[机制迁移指南](../../../development/base-spire/how-to/migrate-mechanism.md)编写；状态只在 GitHub Issues 维护。

## 已核实来源与范围

新增开发态能力实体 `Power`（持有者可以是玩家或敌人、DefinitionId、层数、以及规则查询用的 `Kind` 修正角色），集合 `Powers`，共享过程 `ApplyPlayerPower`／`ApplyEnemyPower`（同持有者同定义累加层数）。修正链按源顺序接入伤害与格挡：

- 卡牌攻击走新的 `DamageEnemyPowered`：先加攻击者的 `damage-additive`（源 `StrengthPower.ModifyDamageAdditive`），再在目标有 `damage-taken-multiplier` 时乘 1.5 向下取整（源 `VulnerablePower.ModifyDamageMultiplicative`），最后按既有格挡→生命顺序。遗物与 Power 的无加成伤害继续用 `DamageEnemy`，因此修正只作用于 `ValueProp.Move` 的卡牌攻击。
- 卡牌格挡走 `GainBlock`：累加 `block-additive`（源 `DexterityPower.ModifyBlockAdditive`）；遗物的无加成格挡继续用 `GainUnpoweredBlock`。
- `damage-dealt-multiplier`（Weak）只记录层数；敌方可行动作未迁移，因此它对玩家没有可观察效果。

迁移三张新卡：Bash（2 费 8／10 伤害＋2／3 Vulnerable）、Uppercut（2 费 13 伤害＋Weak 与 Vulnerable 各 1／2）、Shockwave（2 费、消耗、对全部敌人 Weak 与 Vulnerable 各 3／5，属无色卡组）。

后续包 **DSL-POWER-DURATION-01** 在此规格上补齐时长与 Power 牌离场：新增回合末时点 `AfterPlayerSideTurnEnd`（先弃手牌再派发），共享过程 `TickDownDebuffs` 把 debuff 角色能力减一层并在归零时移除；`Card.LeavesPlay` 对应 CardType.Power，结算后进入 `RemovedFromCombat`；并迁移 Inflame（1 费 Power 牌，+2／3 Strength）。源依据：VulnerablePower／WeakPower 在敌方回合结束时 `TickDownDuration`，Power 类牌由 `CardPileCmd.RemoveFromCombat` 离开战斗。

不迁移：能力时长与回合末衰减（Vulnerable／Weak 在源里于敌方回合结束时减层）、能力移除与清零、能力类型与正负规则以外的语义、施加／移除反应、按能力的伤害来源区分、敌方攻击与 Weak 的实际作用。库存 260 条能力源标识保持 `definition` 为空：本包把修正角色写在应用它的卡牌内容里，Power 目录不新增定义文件。

## 行为要求与设计

| ID | 约定 | 判别 |
| --- | --- | --- |
| PW01 | Power 由 DefinitionId 与 Kind 描述，层数可正可负；同持有者同定义再次施加只累加，不新建实例 | 连续两次 Uppercut 后 Weak／Vulnerable 各只有一个实例且层数相加 |
| PW02 | 卡牌攻击按“加 Strength→乘 Vulnerable→扣格挡→扣生命”结算，向下取整 | 6 点攻击对 2 层 Vulnerable 造成 9；Bash 自身先造成 8 再施加 Vulnerable |
| PW03 | 修正只作用于卡牌攻击／格挡；遗物与 Power 的无加成数值不受影响 | Strength 3 时 MercuryHourglass 仍打 3；Dexterity 2 时 Anchor 仍给 10 |
| PW04 | `ApplyAllEnemiesPowerEffect` 对每个 `Hp > 0` 的开发目标各施加一次 | Shockwave 让两个目标都拿到 3 层 |
| PW05 | 能力层数在公开观察中可见，持有者与集合成员关系不公开 | 真实 stdio 轨迹能看到 `Power.Stacks`；Powers 为 not_published |
| PW06 | 内容文件仅 use 模板参数，源资格、卡牌来源条目总数不变；Power 目录保持源标识不新增定义 | cards catalog 新增三个 definition，powers 仍 260 条 definition 为空 |

## 实施与验证顺序

1. 先加 Power 状态与施加过程，再按源顺序把修正接进伤害与格挡，确认无能力时数值不变。
2. 再加两个面向敌人的能力效果模板与自增益模板，接三张内容、目录与公开元数据。
3. 在包外补合并层数、乘算取整、无加成隔离、多目标与逐卡数值检查；Strength／Dexterity 用明确标注的开发探针验证。
4. 通过真实 stdio 验证 Bash 的公开轨迹（伤害与能力层数），更新共享操作参考与领域资料，最后运行 solution 构建、包行为／覆盖、受影响 net 与文档检查。
