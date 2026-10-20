# 伤害与格挡吸收

关联 CORE-AUTHOR-05／**DSL-COMBAT-DAMAGE-01**；实施与认领见[GitHub 任务清单](https://github.com/QingXiang22/auto-the-spire/issues/28)。本包接在回合生命周期之后，按[纯 DSL 规格](../engine-dsl-migration/spec.md)和[机制迁移指南](../../../development/base-spire/how-to/migrate-mechanism.md)编写；状态只在 GitHub Issues 维护。

## 已核实来源与范围

从 `sts2/src/Core/Commands/CreatureCmd.cs`、`Creature.DamageBlockInternal`／`LoseHpInternal` 核对伤害结算：先按 `min(Block, amount)` 扣格挡，剩余部分从生命扣除并把生命下限截到 0；`DamageResult.UnblockedDamage` 记录实际掉血。本包只实现无加成、可格挡的伤害，不实现 `ValueProp.Unblockable`、加性／乘性修正、`ModifyHpLost`、格挡清理参与者和死亡结算。

新增开发目标实体 `Enemy` 与集合 `Enemies`，只作为伤害目标存在；它不是任何原版怪物的行为实现，由显式开发动作创建。消费者为 MercuryHourglass：`AfterPlayerTurnStart` 时对全部敌人造成 3 点无加成伤害（源 `DamageVar(3m, ValueProp.Unpowered)`）。同时补上与源同名的玩家回合开始中段时点 `AfterPlayerTurnStart`（`AfterTurnStarted` 改名为 `AfterSideTurnStarted`，对应源 `AfterSideTurnStart`）。

## 行为要求与设计

| ID | 约定 | 判别 |
| --- | --- | --- |
| DD01 | `Enemy` 有 Owner／DefinitionId／Hp／MaxHp／Block；`Enemies` 为有序集合；只有显式开发动作创建目标，不注册任何原版怪物 | 公开目录能看到目标实体与生命字段；未调用创建动作时集合为空 |
| DD02 | `DamageEnemy` 先扣格挡再扣生命：格挡取 `min(Block, amount)`，生命不低于 0；不处理不可格挡、修正、死亡与移除 | 格挡足够时生命不变；格挡不足时只掉差额；生命到 0 后不再下降 |
| DD03 | `DamagePlayer` 对玩家执行同一结算（Block 与 Hp），不改 MaxHp | 玩家受伤同样先扣格挡；无格挡时直接掉血 |
| DD04 | 三个回合开始时点按源顺序派发：清格挡与 `AfterBlockCleared` → 能量重置 → `AfterSideTurnStarted` → `AfterPlayerTurnStart` → `AfterPlayerTurnStartLate` | 同一回合三处监听看到相同回合号；顺序错误会改变 MercuryHourglass 与 BloodVial 的可见结果 |
| DD05 | 模板 `TurnStartDamageAllEnemiesRelic` 按持有者、DefinitionId、IsMelted 过滤，遍历当前 `Enemies` 逐个结算；空集合不报错也不消耗随机 | MercuryHourglass 对每个存活目标各扣 3；无目标时无事发生；移除／熔化／别的持有者不触发 |
| DD06 | 内容文件仅 use 模板参数；源资格、ID、hash、池和 297 项来源条目总数不变；不新增 C# 效果分支 | catalog 只新增 MercuryHourglass 的 definition；公开目录新增真实 ID |
| DD07 | 公开轨迹通过真实 stdio 创建目标、开始战斗与回合，观察目标 Hp 与玩家 Block | 目标 40 → 37，玩家格挡 0；候选随相位变化 |

不迁移：`Unblockable`／`Move` 等属性位、力量／易伤等修正、`AfterDamageReceived` 反应、死亡与战斗胜利、怪物意图与行动、卡牌伤害。伤害数值只来自模板参数。

## 实施与验证顺序

1. 先补实体、集合与两个伤害过程，用最小复现确认截断与 0 下限。
2. 再补回合开始中段时点与伤害模板，接 MercuryHourglass 配置并登记 catalog／公开元数据／索引。
3. 在包外追加格挡边界、目标数量、过滤与顺序检查；为 MercuryHourglass 单独生成执行结果，不复用其它遗物场景。
4. 通过真实 stdio 验证公开轨迹，更新共享操作参考与领域资料，最后运行 solution 构建、包行为／覆盖、受影响 net 与迁移边界、文档检查。不运行独立参考或完整游戏矩阵，不改 Engine 语义。
