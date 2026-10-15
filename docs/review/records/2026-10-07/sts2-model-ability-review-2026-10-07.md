# STS2内核表达能力与模型一致性审阅

日期：2026-10-07。按用户要求检查STS2规则能否映射到现有内核，以及当前base建模是否偏离 [通用游戏模型](../../../design/core-the-spire/spec.md#游戏建模与支持判定)。本次关注单人规则的表达能力，生产源码只读；当前任务与排期只看 [主清单](../../tofix/README.md)，不要求实现另一套游戏来证明ability。

## 1. 结论

现有内核提供STS2单人规则所需的主要表达机制：类型化状态、集合/关系、条件/查询、程序与事件、决策续接、随机、纯计算扩展和观察。现有base已通过同一IR执行实际STS2规则，本次检查的地图/阶段、资源/挂载、召唤/复活等未交付机制也有通用组合路径。未发现必须把STS2专用概念放进core才能表达的机制。

当前建模的架构主线符合模型：玩法状态在Engine，规则由base生成IR，宿主提供控制入口，观察由projection提供。偏离主要在**具体STS2状态与转移的简化或数值语义**，不是内核缺少卡牌专用方法。发现一条已加载能力组合的伤害数值反例；其余线性房间/奖励、死亡保留与角色机制主要属于已登记的游戏包范围缺口。

## 2. 模型到内核与STS2的映射

| 模型 | STS2需要表达的内容 | 当前落点与能力判断 |
| --- | --- | --- |
| P | 玩家控制身份；敌人自动行动 | 工厂创建单人participant，动作按role校验，敌方行动由规则程序执行；敌人实体不必对应外部participant |
| S | Run、战斗、牌区、角色属性、效果及历史事实 | [CombatSlicePackage](../../../../mod-the-spire/base-spire/BaseSpire/Slice/CombatSlicePackage.cs)声明实体/字段/集合/关系；`MoveHistory*`、一次性行动标记、`LostHpThisTurn`、`LongTermSource`把影响后续规则的事实存进Engine |
| I | 内容与配置、开局状态、随机流 | [BaseSpireRuntimeFactory](../../../../mod-the-spire/base-spire/BaseSpire/SinglePlayer/BaseSpireRuntimeFactory.cs)构建共享RuleSet并为每局建独立Session；开局前种流，初始化由`SetupBattle`/`StartNextRoom`执行 |
| M,L | 当前允许操作、费用/目标、选牌/响应 | `PlayCard`/`EndTurn`等声明候选与guard；目标与多选由决策指令询问，等待时保存执行帧。复杂候选仍可由查询与条件组合 |
| T | 费用支付、牌移动、钩子、修正、伤害、结算及永久提交 | base构造程序/事件IR，由唯一Session调度器执行；复杂数值可使用既有Compute接口，不能让计算后端暗中改变状态或消费随机 |
| O | 手牌/弃牌、牌库数量、合法看到的牌、敌人意图 | `PlayerView`与`KnownCard`/reveal定义当前公开事实，隐藏牌库身份和顺序；已知信息由包维护，不依靠AI补算隐藏状态 |
| F | 战斗结束与整局胜负的不同边界 | `SettleBattle`处理战斗写回，游戏包发出`SetTerminal`；正式dev工厂关闭“按房间数设胜利”，完整整局胜负由后续规则包流程交付 |

这些映射覆盖机制类型，不把未实现内容计为已交付。对其余角色也能说明组合路径：原版 [OrbQueue](../../../../sts2/src/Core/Entities/Orbs/OrbQueue.cs)对应有序实体集合与容量属性， [OstyCmd.Summon](../../../../sts2/src/Core/Commands/OstyCmd.cs)对应创建/保留实体、所有者关系和生命变化， [ForgeCmd](../../../../sts2/src/Core/Commands/ForgeCmd.cs)对应查询/创建牌实例、修改属性与触发程序。它们尚未进入当前完整角色规则包，不据此要求新内核框架。

## 3. 架构符合项

- **实体持有属性，行为独立执行。** Engine中的Card/Player/Power只有字段；行为在Procedure/Action中。`AuthorCard.NormalSteps`及hooks是编译输入的程序数据，不是运行态卡实体的方法。
- **状态只有一份运行权威。** Run与战斗副本都在Engine存储；`CommitPermanent`定义写回，`LongTermSource`按实例区分来源。编译期的`SliceAuthorModel`及只读构建描述不构成另一份局内RunState。
- **结算仍由唯一调度器推进。** `CombatSlicePackage`用C#构造IR属于游戏前端/组包职责；C#文件较长不等于另建运行时。当前文本规则库尚未充分承载这些程序，是作者组织方式的交付问题。
- **随机计算职责分开。** `SampleInt`消费显式流， [SourceWeightedPickBackend](../../../../mod-the-spire/base-spire/BaseSpire/Slice/SourceWeightedPick.cs)只根据输入计算结果，返回后由IR推进。原版float32算法不用写成core内置游戏分支。

## 4. 实际偏离与缺口

### 4.1 数值转移有实际反例（ENV-T06）

工厂使用 [RuleSetBuilder默认配置](../../../../mod-the-spire/base-spire/BaseSpire/SinglePlayer/BaseSpireRuntimeFactory.cs)： [NumericProfile.Default](../../../../core-the-spire/Core.TheSpire/Engine/Value.cs)保留两位小数，`AddFixed`/`MultiplyFixed`等每步量化。伤害IR使用这些运算；“只在最后转整数”没有消除中间的小数量化。

原版 [Hook.ModifyDamageInternal](../../../../sts2/src/Core/Hooks/Hook.cs)用decimal累计修正， [Creature.LoseHpInternal](../../../../sts2/src/Core/Entities/Creatures/Creature.cs)最终转整数。隔离探针使用真实dev工厂、正式Strike战斗副本和已加载的Strength/Shrink/Cruelty/Vulnerable：

```text
基础伤害6，Strength=-4，Shrink=1，Cruelty=64，目标Vulnerable=1、Block=0
源： (6-4) × 0.7 × (1.5+64/100) = 2.996 → 最终2点伤害
现有IR：中间量化得到3.00 → 实际3点伤害
```

复现命令：`dotnet run --project .audit/sts2-model-review-2026-10-07/Probe.csproj`。 [探针](../../../../.audit/sts2-model-review-2026-10-07/Program.cs)退出0表示反例成功复现； [输出与源定位](../../../../.audit/sts2-model-review-2026-10-07/results.txt)保存原证据。场景由诊断写状态构造，不代表正常整局已经出现该组合，也不是客户端/双侧实测。

这是T的源规则一致性偏离，修正落点先在base：选择符合源的数值实现，必要时走已有纯Compute资产，并重建相应规则身份。仅提高有限的小数精度不能直接宣称覆盖全部源decimal语义；本次不修改冻结core或生产算法。

### 4.2 流程状态仍是线性战斗切片（ENV-T05/09/10）

`Run.CurrentRoom`、`SettledRoom`、`RewardTaken`足以区分当前简化流程，但 [EnterNextRoom/UpgradeLongTermCard](../../../../mod-the-spire/base-spire/BaseSpire/Slice/CombatSlicePackage.cs)还表示连续战斗与每房一次升级，缺少真实地图后继、房间事件、战斗奖励和幕转换。 [工厂](../../../../mod-the-spire/base-spire/BaseSpire/SinglePlayer/BaseSpireRuntimeFactory.cs)也明确用房间号替代地图楼层进行遭遇派生。

这是S/I/L/T相对完整STS2的范围简化，已经按dev身份说明；不能把这份切片状态定义当作最终STS2模型。已有字段/关系、候选与程序可表达正式流程，不需要为了补地图先改变内核架构。

### 4.3 死亡、可选目标与留场仍有绑定（ENV-T06）

当前`DamageMonster`在Hp归零后从`Monsters`移除，`BattleEndTail`用该集合为空判断战斗结束；玩家Hp归零时直接结算并发出Defeat。原版 [CreatureCmd](../../../../sts2/src/Core/Commands/CreatureCmd.cs)区分死亡前、是否应死、是否离场与死亡后反应； [IllusionPower](../../../../sts2/src/Core/Models/Powers/IllusionPower.cs)可以死亡后保留对象并安排复活行动。

现有绑定适用于当前普通战斗子集，不能直接承载死亡保留/复活族。这是游戏包的S/T/F仍需展开的生命周期定义，core的实体与Scope没有写死“Hp=0即销毁”。可用属性、查询及独立过程区分这些事实；用户此前决定本轮不实现该族，继续保留其未支持记录，不新增实施要求。

### 4.4 作者文件组织尚未到新DSL目标

当前文本仍在卡牌normal/upgraded的play/hooks中书写效果，lowering与规则程序主要由C#组装；独立具名代码块的作者工作流尚未迁入生产。这与新DSL的文件组织方向有距离，但现有运行态已分离实体属性与规则程序，不能据此判断抽象模型或内核能力失败。后续在STS2规则包/文本规则库工作流中处理，不先重写全语言。

### 4.5 mod spec含迁移前陈述

 [mod spec第4节](../../../design/mod-the-spire/spec.md#4-当前实现与迁移)仍称base使用core的STS类型、其他游戏包用于证明通用性。这与当前Engine路径及用户“只需表达能力、实际聚焦STS2”的决定不一致；本次报告指出文档过期，不把它当作当前代码架构事实。

## 5. 接续与验证边界

数值反例并入现有ENV-T06；正式阶段/奖励继续ENV-T05/09/10，文本程序库继续ENV-T07，其余角色继续ENV-T12。现有排期与用户“不实现死亡复活族”的决定保留，不新增卡牌或其他游戏任务。

本轮实际执行新数值探针，生产base/core依赖构建成功，确认源期望2与实际3。未重跑全仓构建、Engine/Slice/net套件或严格对拍；既有95/324等证据只对应原日期与范围。报告不是完整STS2行为验收，当前工作区未提交。
