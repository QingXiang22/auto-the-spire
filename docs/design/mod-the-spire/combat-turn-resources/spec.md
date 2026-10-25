# 开发态回合生命周期与生命／能量资源

关联 CORE-AUTHOR-05／**DSL-COMBAT-TURN-01**；实施与认领见[GitHub 任务清单](https://github.com/QingXiang22/auto-the-spire/issues/28)。本包基于前一批战斗开始格挡交付，按[纯 DSL 规格](../base-architecture.md)、[目录设计](../base-architecture.md)和[机制迁移指南](../../../development/base-spire/how-to/migrate-mechanism.md)编写；状态只在 GitHub Issues 维护。

## 已核实来源与范围

当前顺序以[锁定 sts2-sim](../../../../reference/README.md)的 `CombatEngine.StartTurnAsync`／`SetupPlayerTurnAsync`、`Hook` 和对应遗物为依据。本页规定玩家生命、能量与回合号；战斗全流程及待修改差异见[战斗内部对照](../../../development/base-spire/explanation/combat-engine-comparison.md#差异与修改方案)。四件资源消费者如下：

| 内容 | 源 | 触发点与行为 |
| --- | --- | --- |
| Lantern | `Lantern.cs` | 本方回合开始，`TurnNumber <= 1` 时获得 1 点能量 |
| BloodVial | `BloodVial.cs` | 玩家回合开始后期，`TurnNumber <= 1` 时回复 2 点生命 |
| HornCleat | `HornCleat.cs` | 第二玩家回合清格挡后、回合号增加前，源读取旧 `TurnNumber == 1`，获得 14 点无加成格挡；DSL 模板参数 turn=2 在内部减一比较 |
| HappyFlower | `HappyFlower.cs` | 每次本方回合开始把实例计数器加一取模 3，为 0 时获得 1 点能量；计数跨战斗保留 |

伤害、Power、牌区和访问各有领域约定，本页不重复定义。当前已接入多个原生阶段，但不代表参考的全部 Hook 已迁移；敌人选招、额外回合和角色专属阶段仍须按对照页的边界核对。实例排序遵循[通知与修正监听约定](../effect-listeners.md)，不能把同一 Session 执行误写成所有监听只按声明顺序执行。

## 行为要求与设计

| ID | 约定 | 判别 |
| --- | --- | --- |
| TC01 | Player 增加 Hp／MaxHp／Energy／MaxEnergy／TurnNumber／TurnOpen；开始战斗置 TurnNumber=0、Energy=0、Block=0、TurnOpen=false 后再发 BeforeCombatStart | 新周期从 0 回合 0 能量开始；监听器观察到的仍是已活跃状态；HP 不在开始战斗时重置 |
| TC02 | BeginDevelopmentTurn 只在活跃战斗且 TurnOpen=false 时允许；BeforePlayerSideTurnStart → 非首回合按 ShouldClearBlock 清格挡 → AfterBlockCleared → ResetTurnEnergy → AfterEnergyReset → 回合号加一 → BeforeHandDraw／抽牌 → 玩家开始 Early／普通／Late → AfterPlayerSideTurnStart | 首回合保留开场格挡；清格挡及能量通知读旧回合号，起手和后续通知读新回合号；HornCleat 在第二玩家回合触发 |
| TC03 | 开始回合置 TurnOpen=true；EndDevelopmentTurn 只在活跃且 TurnOpen 时允许，按阶段处理玩家末效果与手牌，再执行 RunEnemyTurn，关闭 TurnOpen，退出外层效果后检查胜利；下一玩家开始仍另行调用 | 未开始不能结束，结束后不能重复结束；一次结束只执行一次敌方行动；战斗结束不再开放下一开始动作；回合末 Flush 不冒充主动弃牌 |
| TC04 | Heal(owner, amount) 增加 Hp 并按 MaxHp 截断；不处理复活、治疗修正或死亡 | 满血回复不变；超过上限只到 MaxHp |
| TC05 | GainEnergy 经 ShouldGainEnergy 增加 Energy，可超过 MaxEnergy，但保留开发 schema 上限；ResetTurnEnergy 按 ModifyMaxEnergy 与 ShouldResetEnergy 选择重置或追加，两者不是同一命令 | Lantern 在 3 能量上限上得到 4；保留能量时只追加本回合最大能量 |
| TC06 | 四个模板按持有者、DefinitionId、IsMelted 过滤，只调用本批资源操作；HappyFlower 用实例 Counter，跨战斗保留 | 无遗物／已移除／熔化／别的持有者不触发；两件同时持有各自触发一次 |
| TC07 | 公开轨迹用同一 Host 从合法动作执行开始战斗与两个回合，观察 Hp／Energy／TurnNumber／Block 与合法候选 | 真实 stdio 上第一回合 4 能量 0 格挡、第二回合 3 能量 14 格挡 |
| TC08 | 共享资源与阶段在 DSL；具体消费者使用所属模板与实例监听；来源范围、池顺序及必测用例不因局部迁移缩小，不新增 C# 效果分支 | 四个真实 definition 与公开登记对应，定义存在与实际行为执行分别检查 |

字段与资源过程放 `combat`，回合过程与事件放 `combat/lifecycle.spire`，四个模板放 `relics/rules`，具体配置各放 `relics/shared` 单文件。公开动作显式命名 `BeginDevelopmentTurn`／`EndDevelopmentTurn`，不得命名为正常回合或回合结束结算。

## 实施与验证顺序

1. 先补共享状态与资源过程，再写回合生命周期，最后接模板与内容；每步用最小复现确认现有 Engine 表达足够。
2. 在包外追加有判别力的回合／资源／隔离／组合检查，并为四个遗物各自生成执行结果，不复用其它遗物的场景。
3. 通过真实 stdio 验证公开轨迹，并更新共享操作参考与领域资料。
4. 按[验证指南](../../../development/base-spire/how-to/verify-and-debug.md)执行受影响组及对应双侧阶段比较；只改文字时运行文档组。实际运行范围单列，不把开发探针当作正常流程或完整游戏验证。
