# 伤害与格挡吸收

关联 CORE-AUTHOR-05／**DSL-COMBAT-DAMAGE-01**；实施与认领见[GitHub 任务清单](https://github.com/QingXiang22/auto-the-spire/issues/28)。本包接在回合生命周期之后，按[纯 DSL 规格](../base-architecture.md)和[机制迁移指南](../../../development/base-spire/how-to/migrate-mechanism.md)编写；状态只在 GitHub Issues 维护。

## 已核实来源与范围

当前依据为[锁定 sts2-sim](../../../../reference/README.md)的 `AttackCommand.Execute` 与 `CreatureCmd.Damage`／`DamageSingle`。本页规定基本挡伤与失血；完整来源、攻击／命中批次和外层提交见[行为要求 G03～G05](../content-behavior-migration.md)，死亡清理见[战斗结束约定](../combat-end/spec.md)。当前实现已包含部分有加成攻击、不可格挡失血和普通死亡通知；完整 `ModifyHpLost`、承伤转移及防死链仍有缺口，不能继续按旧无加成探针范围解释整个包。

`Enemy` 已由开发目标、具名敌人与部分正常遭遇共用，定义存在不等于完整行为支持。MercuryHourglass 在 `AfterPlayerTurnStart` 对合资格敌人造成 3 点无加成伤害，当前通过完整群伤操作先提交全批，再通知和处理死亡。阶段对照与后续修改方案见[伤害与战斗结束的对照说明](../../../development/base-spire/explanation/combat-engine-comparison.md#一次攻击怎样处理多个目标)。

## 行为要求与设计

| ID | 约定 | 判别 |
| --- | --- | --- |
| DD01 | `Enemy` 保存实例定义、HP、格挡及生命周期字段；开发目标与具名敌人复用共享伤害操作，出生和行动顺序另按敌人领域规定 | 公开状态对应真实实例；不能把仅有目标实体计为完整敌人支持 |
| DD02 | `DamageEnemy` 是无加成、可格挡的兼容入口，转入统一结果管线；格挡取 `min(Block, amount)`，生命不低于 0，结果通知及普通死亡清理按完整操作执行 | 全挡生命不变；不足只扣差额；重复伤害不重复死亡；最终失血与超杀分开 |
| DD03 | `DamagePlayer` 对玩家执行同一结算（Block 与 Hp），不改 MaxHp | 玩家受伤同样先扣格挡；无格挡时直接掉血 |
| DD04 | 当前顺序为 BeforePlayerSideTurnStart → 清格挡／AfterBlockCleared → 能量重置／AfterEnergyReset → 回合号加一 → BeforeHandDraw／抽牌 → AfterPlayerTurnStartEarly／普通／Late → AfterPlayerSideTurnStart | 起手已完成后才执行 MercuryHourglass 与 BloodVial 的对应通知；它们读新回合号，清格挡与能量通知读旧号 |
| DD05 | `TurnStartDamageAllEnemiesRelic` 筛选有效实例后调用群伤操作；先固定合资格目标并提交全部结果，再发布结果与死亡通知；空域不消费随机 | MercuryHourglass 对合资格目标各扣 3；同批首个目标死亡不提前改变后续目标的本批扣血；移除／熔化后不触发 |
| DD06 | 内容在所属 DSL 模块维护，共享伤害过程不按内容 ID 分支；来源范围、池顺序及必测用例不因局部迁移缩小 | catalog、已加载 definition、公开登记和实际执行结果分别检查 |
| DD07 | 公开轨迹通过真实 stdio 创建目标、开始战斗与回合，观察目标 Hp 与玩家 Block | 目标 40 → 37，玩家格挡 0；候选随相位变化 |

未支持边界按当前操作及对应内容声明，完整修正、数值精度和死亡链不能由基本挡伤检查推导。攻击、直接伤害和失血保留各自的属性及来源，不从 `CurrentPlay` 自动补卡来源。

## 实施与验证顺序

1. 先补实体、集合与两个伤害过程，用最小复现确认截断与 0 下限。
2. 再补回合开始中段时点与伤害模板，接 MercuryHourglass 配置并登记 catalog／公开元数据／索引。
3. 在包外追加格挡边界、目标数量、过滤与顺序检查；为 MercuryHourglass 单独生成执行结果，不复用其它遗物场景。
4. 按[验证指南](../../../development/base-spire/how-to/verify-and-debug.md)选择受影响 base／net／migration 组与双侧场景，比较逐击结果、通知顺序和随机；只改文字时运行文档组。公开路径、受控组件与完整正常 Run 分别报告，不由基本挡伤结果宣称全部符合。
