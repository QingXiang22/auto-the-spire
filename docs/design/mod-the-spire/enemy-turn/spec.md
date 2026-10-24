# 敌方阶段与招式图行为要求

关联 CORE-AUTHOR-05／**DSL-ENEMY-TURN-01**；实施与认领见[GitHub 任务清单](https://github.com/QingXiang22/auto-the-spire/issues/28)。本包接在战斗结束之后，按[纯 DSL 规格](../base-architecture.md)和[机制迁移指南](../../../development/base-spire/how-to/migrate-mechanism.md)编写；状态只在 GitHub Issues 维护。

## 已核实来源与范围

从锁定的 `reference/sts2-sim/src/Sts2Sim.Core/Models/Monsters/FuzzyWurmCrawler.cs` 核对：Ascension 0 的初始 HP 55..57；招式为 `FIRST_ACID_GOOP`（4 点攻击）→ `INHALE`（对自身施加强度 7）→ `ACID_GOOP`（4 点攻击）→ 回到 `FIRST_ACID_GOOP`，三个状态的后继关系确定，没有随机分支。两个 goop 状态伤害与意图相同，因此用 `MoveIndex` 0／1／2 表示这三个招位。

`TwigSlimeM.cs` 的 A0 初始 HP 为26..28，攻击 `POKEY_POUNCE_MOVE` 造成11点伤害，状态招 `STICKY_SHOT_MOVE` 向每个目标的弃牌堆加入一张Slimed，首招固定为状态招。源码 `AddBranch(moveState, 2)` 选择最多连续两次的重载，默认权重1；状态招使用CannotRepeat，同样权重1，冷却均为0。两个分支保持攻击、状态的源顺序。Slimed花1能量，抽1张后耗尽，不能升级。

敌方阶段在玩家回合结束后按可命中敌人各发一次 `EnemyTurnStarted`，随后发 `AfterEnemyTurnEnded`。共享图消费者执行已经保存的招式；敌方阶段结束、持续时间更新后，为存活敌人选择下一招。`DamagePlayerPowered` 复用攻击链，公开 `Enemy.MoveId`、`MoveIndex` 和 `Intent` 表示同一份已选结果。观察不选招。

图消费者包括FuzzyWurmCrawler、TwigSlimeM、SlimesWeak四类史莱姆、BattleFriend三档、Wriggler及下述七个固定轮转敌人。FuzzyWurmCrawler与TwigSlimeM的开发生成使用源最小HP，Wriggler按下述源候选抽样。完整遭遇队列、意图伤害数值、动态权重函数、通用强制迁移钩子及完整正常Run不在此实现范围。原来源目录与遭遇池完整保留，不能据此宣称ENV-T01全部完成。

### 固定轮转与完整敌人攻击

七个既有敌人按锁定参考的节点和后继登记同一共享图，正文由各自内容模块提供，不再由内容订阅EnemyTurnStarted自行改MoveIndex。Assassin的KILLSHOT_MOVE、BowlbugEgg的BITE_MOVE及Stabbot的STAB_MOVE各自循环；Axe为SWING_1→SWING_2→BIG_SWING→SWING_1；Brute为BEAT_MOVE与ROAR_MOVE交替；Crossbow从RELOAD_MOVE开始与FIRE_MOVE交替；Tracker从TRACK_MOVE进入HOUNDS_MOVE并保持。固定后继不消费monster_ai，首次生成、跨战重置和StunEnemy沿用已有共享图操作。

Axe的两次普通挥击及BowlbugEgg公布attack,defend，Stabbot公布attack,debuff，顺序与源Intents一致。开发Spawn保留原动作ID和A0最小HP；这不包含正常编队的出生抽样或选招时点验收。

EnemyAttack接收attacker、defender、amount和hits，拒绝负命中数，一次创建Attack再调用ExecuteAttack；DamagePlayerPowered作为单击调用复用它。BeforeAttack／AfterAttack各一次，逐击保留独立DamageResult与伤害反应；攻击者在反击中死亡后当前命中按已有伤害操作完成，后续命中停止，目标死亡也停止。普通非结束中的攻击由Vigor在整次攻击结束后消费开始层数，不能用多次单击或合并总伤害替代。Tracker的1×8与Vigor3应产生八次4点伤害、一次Attack；反击死亡与玩家致死分别验证命中边界和一次终局。玩家死亡按[死亡约定](../combat-end/spec.md)清理自身Power；ending期间的共享Power命令拒绝改量，因此致死攻击保留敌人Vigor3。组件比较同时核对命中与Power生命周期，不以命中数相同替代全部维度。

怪物正文施加Power使用ApplyPowerWithSource，applierEnemy为真实怪物，card／play为空；自我强化仍保留自身施加者。已有逐实例历史、公开查询只读、击晕恢复、死亡清理与正常首战检查继续适用。独立组件比较使用真实参考怪物、空永久牌组和明确设置的生命／Power，比较逐回合节点、意图、历史、生命、格挡、逐击结果及AI随机状态；人工状态不证明正常遭遇可达或完整Run符合。

## 行为要求与设计

| ID | 约定 | 判别 |
| --- | --- | --- |
| EN01 | 玩家回合结束后依次弃手牌、发 AfterTurnEnded、跑敌方阶段（每个可命中敌人一次）、发 AfterEnemyTurnEnded，最后关闭回合门 | 敌人已死亡时跳过；结束后仍可开下一回合 |
| EN02 | 敌方攻击按源顺序结算：攻击者 Strength 加性、攻击者 Weak ×0.75、防守方 Vulnerable ×1.5，整数向下取整，再格挡→生命 | 7 强度的 goop 打 11；1 层 Weak 的 4 点打 3；有 10 格挡时只掉 4 点格挡里的 4 并与剩余格挡共存 |
| EN03 | 共享图执行保存的招式，内容按DefinitionId提供动作正文；未迁移敌人继续用原模板 | 三个招位按源顺序轮转，MoveId／MoveIndex／Intent一致 |
| EN04 | debuff 减层发生在敌方阶段结束后 | Bash 施加 2 层 Vulnerable，结束玩家回合后变 1 层 |
| EN05 | 独有图和正文保留在敌人内容文件，共用选招放领域规则；敌人目录121项来源总数不变 | 新增实际消费者不改变原遭遇池、未支持项或来源SHA256 |

## 共享招式图目标要求

### Wriggler槽位条件与实例寿命

锁定Wriggler在A0按niche从17..21的尚未使用最大生命候选中抽取；按当前活动敌人出生顺序排除已用值，候选耗尽才按原范围抽样，不重设流或补抽。四槽wriggler1／3初始NASTY_BITE_MOVE（6点加成攻击），wriggler2／4初始WRIGGLE_MOVE（给目标玩家弃牌堆生成一张Infection，再获得2层Strength），两招固定交替。StartStunned=true先执行一次SPAWNED_MOVE空招，再按槽位进入上述初招。没有AI随机消费；未知槽位明确拒绝，不使用注释中的无槽回退。开发创建入口在抽HP前验证这项支持前置。

共享图的conditional节点通过定义分派返回下一节点，递归解析至真实move，不记入已选行动历史，也不抽随机。缺分派／静态未知节点在装配时拒绝，无合法条件在执行时失败，循环仍受既有预算保护。实例保留真实槽名与StartStunned；重置行动图不能改写它们。WRIGGLE的复合意图按源顺序发布为buff,status，具体状态牌数量以真实生成结果验证；通用意图伤害数值仍未迁移。

内容私有状态由Wriggler模块拥有；RemoveCombatEnemy先清理Power与招式状态、发布EnemyRemoving供所属模块清理，再销毁实例。正常访问结束使用同一入口，不能在尚有非可选引用时直接destroy。死亡和逃离保留出生实例的边界沿用战斗结束约定；召唤／复活和父事件入口不由本组件恢复。

以下 MOVE-01～05 是ENV-T01的完整迁移要求，当前实现范围见下节。招式图与执行规则在base DSL中表达，core提供通用查询、过程、事件与随机；固定轮转复用共享节点。

| 要求 | 状态与行为 | 判别 |
| --- | --- | --- |
| MOVE-01 定义与实例 | 不可变图定义包含节点、初始节点及源顺序；每个敌人独立保存当前节点、首招是否执行、已选下一招和必要的招式历史 | 同定义两个敌人不共用记忆；新战重置，观察不改变节点 |
| MOVE-02 选招与执行 | 按源时点解析确定／条件／随机分支并保存下一招；意图从该招及当前公开修正投影。执行使用保存结果，执行后再按源规则迁移 | 固定轮转、首招例外和多个分支一致；反复观察意图不重选、不消费 RNG |
| MOVE-03 随机与历史 | 区分权重、冷却及最多连续次数，按原候选顺序计算动态权重与历史限制；保留真实分数权重和数值语义 | 禁连招、仅用一次、冷却及单候选的源消费均有对照；不能把不同源重载的整数参数统一解释为权重 |
| MOVE-04 强制迁移 | 明确覆盖下一招、跳过行动及恢复的时点，保留源 enter／exit 效果；状态、意图和行动队列保持一致 | 用实际行动控制消费者验证强制换招／跳过后续接；召唤／复活族仍按原暂缓决定处理，不由本要求恢复 |
| MOVE-05 失败与寿命 | 图装配检查未知节点、重复节点及缺后继；运行时无合法分支具名失败，不补抽或默选。迁移循环受既有执行预算保护；目标及成员资格在源规定时点复核 | 非法图、无分支、目标死亡及两局隔离可定位；故障沿 core 约定，不吞错继续运行 |

首批迁移复用 FuzzyWurmCrawler 或 Raider 的固定轮转，并选择一个经源码核对的随机限制消费者；强制迁移使用 Whistle 所需行动控制或另一项明确源消费者。各消费者分别核对公开意图、动作轨迹、历史、随机调用和同定义实例隔离。共享图完成不等于全部敌人／遭遇、完整出生 HP 或正常 Run 已恢复。

### Whistle击晕与恢复

锁定Whistle为2费Ancient Attack，基础33／升级44伤害并耗尽，先完成攻击再调用CreatureCmd.Stun。目标死亡或战斗已结束时不击晕。仍存活的目标必须使用已迁移招式图；未迁移图明确报unsupported_capability，不静默忽略。

StunEnemy保存当前已选真实节点，以公开STUNNED／stun替换下一行动。此瞬态不加入EnemyMoveHistory，不改变原节点连续次数，不抽随机。必须实际执行一次空招后才能恢复；执行前再次击晕不重置或提前推进。恢复时将原节点本身再次记入已选历史并发布其原意图；首招前恢复首招，随机节点已解析的招式直接复用，不重新抽分支。随后真实执行该招，再按通常后继选招。多个敌人各自保存状态，新战及完整移除清理击晕标志；STUNNED期间MoveIndex仍为底层保存节点的索引，具体行动以MoveId及Intent为准。

该接口只承接Whistle默认follow-up的真实消费者，不提供任意nextMoveId、回调正文或尚未迁移的enter／exit钩子。受控两侧比较单列卡牌成本／伤害／耗尽、招式／历史／意图、回合资源和随机消费，不替代正常Run或全部行动控制验收。

## 共享图的当前操作与失败方式

定义位于[敌人schema](../../../../mod-the-spire/base-spire/enemies/schema.spire)，注册、校验、选招和历史位于[共享规则](../../../../mod-the-spire/base-spire/enemies/rules/move-graph.spire)。启动时先登记图、再按源顺序登记节点和分支，最后检查初始节点、后继、重复定义／节点／分支及动作分派。定义表仅在启动时构造，逐实例状态由Engine保存；不存在C#敌人调度器。

`EnemyMoveState`保存敌人、所属图、当前已选节点、Ready、FirstPerformed、连续次数及历史序号，`EnemyMoveHistory`保存该实例每次已选招式。初始招式即入历史，第一次固定首招不抽样。`ResetEnemyMoves`供生成入口调用，`BeforeCombatStart`重新初始化每个敌人的历史，保留局内随机流位置。`PerformEnemyMove`复核目标生命并执行保存结果；`SelectNextEnemyMove`只处理尚未准备且存活的实例。内容生成和`SpawnDevelopmentEnemy`使用同一初始化操作。

随机节点根据实例历史计算有效权重：MaxRepeats=0为无限制，正值限制连续次数；Cooldown检查最近指定数量的已选招式；Once排除曾选过的目标。权重以源binary32位型保存，总权重按LINQ Sum(float)的顺序用double累加，最后转binary32。当前静态权重参数为Fixed六位小数，转换成binary32后执行；动态权重函数仍未支持。TwigSlimeM的两个真实分支权重均为1，整数参数2不会变成权重。

取样使用 `base-spire:run:monster_ai` 的一次原始double，在double上按总权重缩放后转binary32；随后按源顺序逐项binary32相减，第一次小于等于0即选中。只有一个有效分支仍抽一次，零权重分支保留原顺序；总权重不大于0具名失败，不补抽。分支到分支的迁移使用既有调用／指令预算，循环耗尽预算后故障，不截断成默认招式。步骤故障遵循core的指令原子性，不承诺整个动作回滚。

公开投影只新增已选招式ID；图定义、分支、历史和首招标志均隐藏。测试通过逐次随机诊断读取原始操作／结果／游标，并用独立源向量核对候选顺序及有效权重。该检查只覆盖受控开发战斗；这两个敌人的完整模拟器双侧适配尚未接入，继续关联[独立比较子任务#31](https://github.com/QingXiang22/auto-the-spire/issues/31)，不据此将严格测试集改为通过。

## 验证

```powershell
dotnet run --project tools/base-spire-dev -c Release -- check --enemy-moves-only
```

定向检查包括两个真实消费者、Slimed执行、同定义双实例／双会话、跨战重置、查询只读、暂停续接及过期答案、非法图、无合法分支和迁移循环预算。固定源向量的生成入口见[随机神谕](../../../../tools/rng-oracle/README.md)。完整包检查另核对原来源条目、全部已声明卡牌及既有敌人行为。

公共CI检查core、base、net、migration及docs；正常Run和完整参考模拟器行为仍按各自工单验收。
