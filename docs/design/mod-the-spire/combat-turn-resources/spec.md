# 开发态回合生命周期与生命／能量资源

关联 CORE-AUTHOR-05／**DSL-COMBAT-TURN-01**；实施与认领见[GitHub 任务清单](https://github.com/QingXiang22/auto-the-spire/issues/28)。本包基于前一批战斗开始格挡交付，按[纯 DSL 规格](../engine-dsl-migration/spec.md)、[目录设计](../engine-dsl-migration/plan.md)和[机制迁移指南](../../../development/base-spire/how-to/migrate-mechanism.md)编写；状态只在 GitHub Issues 维护。

## 已核实来源与范围

本批新增玩家生命、能量与回合计数三个共享资源，并从 `sts2/src/Core/Combat/CombatManager.cs`、`Creature.AfterTurnStart`、`PlayerCombatState.ResetEnergy` 核对实际回合顺序，迁移四件只依赖这些资源的原版遗物：

| 内容 | 源 | 触发点与行为 |
| --- | --- | --- |
| Lantern | `Lantern.cs` | 本方回合开始，`TurnNumber <= 1` 时获得 1 点能量 |
| BloodVial | `BloodVial.cs` | 玩家回合开始后期，`TurnNumber <= 1` 时回复 2 点生命 |
| HornCleat | `HornCleat.cs` | 格挡被清空时，`TurnNumber == 2` 获得 14 点无加成格挡 |
| HappyFlower | `HappyFlower.cs` | 每次本方回合开始把实例计数器加一取模 3，为 0 时获得 1 点能量；计数跨战斗保留 |

不迁移：伤害与敌人、卡牌出牌与费用、Power、正常战斗初始化／胜负、跨房写回。本包的时间点是**开发态**事件，在同一执行栈内按声明顺序派发；原版 `AfterSideTurnStart`／`AfterPlayerTurnStartLate`／`AfterBlockCleared`／`AfterEnergyReset` 的完整钩子划分与修正链未迁移，报告与文档必须保留该区别。

## 行为要求与设计

| ID | 约定 | 判别 |
| --- | --- | --- |
| TC01 | Player 增加 Hp／MaxHp／Energy／MaxEnergy／TurnNumber／TurnOpen；开始战斗置 TurnNumber=0、Energy=0、Block=0、TurnOpen=false 后再发 BeforeCombatStart | 新周期从 0 回合 0 能量开始；监听器观察到的仍是已活跃状态；HP 不在开始战斗时重置 |
| TC02 | BeginDevelopmentTurn 只在活跃战斗允许，先按源顺序自增回合号，再在回合号 >1 时清空格挡并发 AfterBlockCleared（源：`TurnNumber == 1` 跳过清空），随后置 Energy=MaxEnergy，再依次发 AfterTurnStarted 与 AfterPlayerTurnStartLate | 第一回合保留开始战斗获得的格挡；第二回合开始时格挡先被清空、监听器再补格挡；能量在监听器之前重置 |
| TC03 | BeginDevelopmentTurn 置 TurnOpen=true 并把同回合的再次开始拒绝；EndDevelopmentTurn 只在 TurnOpen 时允许，弃掉当前手牌并置 TurnOpen=false；不结算敌方回合、不发胜利、不做逐张弃牌钩子 | 未开始回合不能结束；结束后不能重复结束；两次结束之间必须有一个开始；拒绝在守卫处发生且无状态变化 |
| TC04 | Heal(owner, amount) 增加 Hp 并按 MaxHp 截断；不处理复活、治疗修正或死亡 | 满血回复不变；超过上限只到 MaxHp |
| TC05 | GainEnergy(owner, amount) 增加 Energy，可超过 MaxEnergy；回合开始的重置仍按 MaxEnergy | Lantern 在 3 能量上限上得到 4 |
| TC06 | 四个模板按持有者、DefinitionId、IsMelted 过滤，只调用本批资源操作；HappyFlower 用实例 Counter，跨战斗保留 | 无遗物／已移除／熔化／别的持有者不触发；两件同时持有各自触发一次 |
| TC07 | 公开轨迹用同一 Host 从合法动作执行开始战斗与两个回合，观察 Hp／Energy／TurnNumber／Block 与合法候选 | 真实 stdio 上第一回合 4 能量 0 格挡、第二回合 3 能量 14 格挡 |
| TC08 | 内容文件仅 use 模板参数；源资格、ID、hash、池和 297 项来源条目总数不变；不新增 C# 效果分支 | catalog 只新增四个非空 definition；公开目录新增四个真实 ID |

字段与资源过程放 `combat`，回合过程与事件放 `combat/lifecycle.spire`，四个模板放 `relics/rules`，具体配置各放 `relics/shared` 单文件。公开动作显式命名 `BeginDevelopmentTurn`／`EndDevelopmentTurn`，不得命名为正常回合或回合结束结算。

## 实施与验证顺序

1. 先补共享状态与资源过程，再写回合生命周期，最后接模板与内容；每步用最小复现确认现有 Engine 表达足够。
2. 在包外追加有判别力的回合／资源／隔离／组合检查，并为四个遗物各自生成执行结果，不复用其它遗物的场景。
3. 通过真实 stdio 验证公开轨迹，并更新共享操作参考与领域资料。
4. 最后运行 solution 构建、包行为／覆盖、受影响 net 与迁移边界、文档检查；不改 Engine 语义，通用 Engine 全套由 Actions 验证。不运行独立参考或完整游戏矩阵，不把开发探针当作正常流程验证记录。
