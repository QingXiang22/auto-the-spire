# 故障机器人卡牌校对清单（2026-10-12）

原版 [DefectCardPool](../../../../../sts2/src/Core/Models/CardPools/DefectCardPool.cs) 共 **88** 张。

[返回清单总览](README.md)。这是源快照与实现证据的日期记录；列含义、符号及未验证范围见总览。普通/升级描述来自同一条中文本地化模板，数值只取原版源码；不会根据 base 的实现反推描述。

| # / 卡牌 / 原版标识 | 类型、稀有度与费用（普通→升级） | 原版普通描述 | 原版升级描述 | base 实现证据 | 专属/共享机制线索 |
| --- | --- | --- | --- | --- | --- |
| 1. **适应打击**<br>[`AdaptiveStrike`](../../../../../sts2/src/Core/Models/Cards/AdaptiveStrike.cs) | 攻击 / 稀有<br>2→2 能量 | 造成18点伤害。<br>将这张牌的一张0[能量]复制品添加到你的弃牌堆。 | 造成23点伤害。<br>将这张牌的一张0[能量]复制品添加到你的弃牌堆。 | 未声明 | — |
| 2. **万物一心**<br>[`AllForOne`](../../../../../sts2/src/Core/Models/Cards/AllForOne.cs) | 攻击 / 稀有<br>2→2 能量 | 造成10点伤害。<br>将你弃牌堆中的所有0[能量]牌放入你的手牌。 | 造成14点伤害。<br>将你弃牌堆中的所有0[能量]牌放入你的手牌。 | 未声明 | — |
| 3. **球状闪电**<br>[`BallLightning`](../../../../../sts2/src/Core/Models/Cards/BallLightning.cs) | 攻击 / 普通<br>1→1 能量 | 造成7点伤害。<br>生成1个闪电充能球。 | 造成10点伤害。<br>生成1个闪电充能球。 | 未声明 | 充能球/槽位/触发 |
| 4. **弹幕齐射**<br>[`Barrage`](../../../../../sts2/src/Core/Models/Cards/Barrage.cs) | 攻击 / 普通<br>1→1 能量 | 当前每有一个充能球，造成5点伤害。 | 当前每有一个充能球，造成7点伤害。 | 未声明 | 充能球/槽位/触发 |
| 5. **光束射线**<br>[`BeamCell`](../../../../../sts2/src/Core/Models/Cards/BeamCell.cs) | 攻击 / 普通<br>0→0 能量 | 造成3点伤害。<br>给予1层易伤。 | 造成4点伤害。<br>给予2层易伤。 | 已注册；有本地行为证据<br>[base:80](../../../../../mod-the-spire/base-spire/BaseSpire/ContentSource/defect-cards.spire)<br>[本地证据:260](../../../../../.audit/owner-shared-run-20261012/slice-final.log) | — |
| 6. **偏差认知**<br>[`BiasedCognition`](../../../../../sts2/src/Core/Models/Cards/BiasedCognition.cs) | 能力 / 先古<br>1→1 能量 | 获得4点集中。<br>在你的回合开始时，失去1点集中。 | 获得5点集中。<br>在你的回合开始时，失去1点集中。 | 未声明 | 充能球/槽位/触发、集中与充能球数值 |
| 7. **高速脱离**<br>[`BoostAway`](../../../../../sts2/src/Core/Models/Cards/BoostAway.cs) | 技能 / 普通<br>0→0 能量 | 获得6点格挡。<br>将一张晕眩添加到你的弃牌堆中。 | 获得9点格挡。<br>将一张晕眩添加到你的弃牌堆中。 | 未声明 | — |
| 8. **启动流程**<br>[`BootSequence`](../../../../../sts2/src/Core/Models/Cards/BootSequence.cs) | 技能 / 罕见<br>0→0 能量 | 获得10点格挡。<br>关键字：固有、消耗。 | 获得13点格挡。<br>关键字：固有、消耗。 | 已注册；有本地行为证据<br>[base:12](../../../../../mod-the-spire/base-spire/BaseSpire/ContentSource/defect-cards.spire)<br>[本地证据:227](../../../../../.audit/owner-shared-run-20261012/slice-final.log) | — |
| 9. **缓冲**<br>[`Buffer`](../../../../../sts2/src/Core/Models/Cards/Buffer.cs) | 能力 / 稀有<br>2→2 能量 | 阻止下1次你受到的生命值损伤。 | 阻止下2次你受到的生命值损伤。 | 未声明 | — |
| 10. **暴涨**<br>[`BulkUp`](../../../../../sts2/src/Core/Models/Cards/BulkUp.cs) | 能力 / 罕见<br>2→2 能量 | 失去1个充能球栏位。<br>获得2点力量。<br>获得2点敏捷。 | 失去1个充能球栏位。<br>获得3点力量。<br>获得3点敏捷。 | 未声明 | 充能球/槽位/触发 |
| 11. **扩容**<br>[`Capacitor`](../../../../../sts2/src/Core/Models/Cards/Capacitor.cs) | 能力 / 罕见<br>1→1 能量 | 获得2个充能球栏位。 | 获得3个充能球栏位。 | 未声明 | 充能球/槽位/触发 |
| 12. **混沌**<br>[`Chaos`](../../../../../sts2/src/Core/Models/Cards/Chaos.cs) | 技能 / 罕见<br>1→1 能量 | 生成1个随机充能球。 | 生成2个随机充能球。 | 未声明 | 充能球/槽位/触发 |
| 13. **充电**<br>[`ChargeBattery`](../../../../../sts2/src/Core/Models/Cards/ChargeBattery.cs) | 技能 / 普通<br>1→1 能量 | 获得7点格挡。<br>在下个回合获得1点能量。 | 获得10点格挡。<br>在下个回合获得1点能量。 | 未声明 | — |
| 14. **冰寒**<br>[`Chill`](../../../../../sts2/src/Core/Models/Cards/Chill.cs) | 技能 / 罕见<br>0→0 能量 | 当前每有一名敌人，就生成1个冰霜充能球。<br>关键字：消耗。 | 当前每有一名敌人，就生成1个冰霜充能球。 | 未声明 | 充能球/槽位/触发 |
| 15. **爪击**<br>[`Claw`](../../../../../sts2/src/Core/Models/Cards/Claw.cs) | 攻击 / 普通<br>0→0 能量 | 造成3点伤害。<br>本场战斗中所有爪击卡牌的伤害增加2点。 | 造成4点伤害。<br>本场战斗中所有爪击卡牌的伤害增加3点。 | 未声明 | — |
| 16. **寒流**<br>[`ColdSnap`](../../../../../sts2/src/Core/Models/Cards/ColdSnap.cs) | 攻击 / 普通<br>1→1 能量 | 造成6点伤害。<br>生成1个冰霜充能球。 | 造成9点伤害。<br>生成1个冰霜充能球。 | 未声明 | 充能球/槽位/触发 |
| 17. **压缩**<br>[`Compact`](../../../../../sts2/src/Core/Models/Cards/Compact.cs) | 技能 / 罕见<br>1→1 能量 | 获得6点格挡。<br>将你手牌中的全部状态牌变化为燃料。 | 获得7点格挡。<br>将你手牌中的全部状态牌变化为燃料+。 | 未声明 | — |
| 18. **编译冲击**<br>[`CompileDriver`](../../../../../sts2/src/Core/Models/Cards/CompileDriver.cs) | 攻击 / 普通<br>1→1 能量 | 造成7点伤害。<br>你每有一种不同的充能球，就抽一张牌。 | 造成10点伤害。<br>你每有一种不同的充能球，就抽一张牌。 | 未声明 | 充能球/槽位/触发 |
| 19. **吞噬暗影**<br>[`ConsumingShadow`](../../../../../sts2/src/Core/Models/Cards/ConsumingShadow.cs) | 能力 / 稀有<br>2→2 能量 | 生成2个黑暗充能球。<br>在你的回合结束时，激发你最左侧的充能球。 | 生成3个黑暗充能球。<br>在你的回合结束时，激发你最左侧的充能球。 | 未声明 | 充能球/槽位/触发 |
| 20. **冷却剂**<br>[`Coolant`](../../../../../sts2/src/Core/Models/Cards/Coolant.cs) | 能力 / 稀有<br>1→1 能量 | 在你的回合开始时，你每有一种不同的充能球，就获得2点格挡。 | 在你的回合开始时，你每有一种不同的充能球，就获得3点格挡。 | 未声明 | 充能球/槽位/触发 |
| 21. **冷静头脑**<br>[`Coolheaded`](../../../../../sts2/src/Core/Models/Cards/Coolheaded.cs) | 技能 / 普通<br>1→1 能量 | 生成1个冰霜充能球。<br>抽1张牌。 | 生成1个冰霜充能球。<br>抽2张牌。 | 未声明 | 充能球/槽位/触发 |
| 22. **创造性AI**<br>[`CreativeAi`](../../../../../sts2/src/Core/Models/Cards/CreativeAi.cs) | 能力 / 稀有<br>3→2 能量 | 在你的回合开始时，将一张随机能力牌加入你的手牌。 | 在你的回合开始时，将一张随机能力牌加入你的手牌。 | 未声明 | — |
| 23. **漆黑**<br>[`Darkness`](../../../../../sts2/src/Core/Models/Cards/Darkness.cs) | 技能 / 罕见<br>1→1 能量 | 生成1黑暗充能球。<br>触发所有黑暗充能球的被动。 | 生成1黑暗充能球。<br>触发所有黑暗充能球的被动两次。 | 未声明 | 充能球/槽位/触发 |
| 24. **防御**<br>[`DefendDefect`](../../../../../sts2/src/Core/Models/Cards/DefendDefect.cs) | 技能 / 基础<br>1→1 能量 | 获得5点格挡。 | 获得8点格挡。 | 已注册；有本地行为证据<br>[base:50](../../../../../mod-the-spire/base-spire/BaseSpire/ContentSource/defect-cards.spire)<br>[本地证据:200](../../../../../.audit/owner-shared-run-20261012/slice-final.log) | — |
| 25. **碎片整理**<br>[`Defragment`](../../../../../sts2/src/Core/Models/Cards/Defragment.cs) | 能力 / 稀有<br>1→1 能量 | 获得1点集中。 | 获得2点集中。 | 未声明 | 充能球/槽位/触发、集中与充能球数值 |
| 26. **双倍能量**<br>[`DoubleEnergy`](../../../../../sts2/src/Core/Models/Cards/DoubleEnergy.cs) | 技能 / 罕见<br>1→0 能量 | 将你的能量翻倍。<br>关键字：消耗。 | 将你的能量翻倍。<br>关键字：消耗。 | 未声明 | — |
| 27. **双重释放**<br>[`Dualcast`](../../../../../sts2/src/Core/Models/Cards/Dualcast.cs) | 技能 / 基础<br>1→0 能量 | 激发你最右侧的充能球两次。 | 激发你最右侧的充能球两次。 | 未声明 | 充能球/槽位/触发 |
| 28. **回响形态**<br>[`EchoForm`](../../../../../sts2/src/Core/Models/Cards/EchoForm.cs) | 能力 / 稀有<br>3→3 能量 | 你每回合打出的第一张牌会被打出两次。<br>关键字：虚无。 | 你每回合打出的第一张牌会被打出两次。 | 未声明 | — |
| 29. **能量涌动**<br>[`EnergySurge`](../../../../../sts2/src/Core/Models/Cards/EnergySurge.cs)<br>**仅多人** | 技能 / 罕见<br>1→1 能量 | 所有玩家获得2点能量。<br>关键字：消耗。 | 所有玩家获得3点能量。<br>关键字：消耗。 | 未声明 | 跨玩家效果 |
| 30. **野性**<br>[`Feral`](../../../../../sts2/src/Core/Models/Cards/Feral.cs) | 能力 / 罕见<br>2→1 能量 | 你每回合打出的第一张<br>耗能为0[能量]的攻击牌，<br>会放回你的手牌。 | 你每回合打出的第一张<br>耗能为0[能量]的攻击牌，<br>会放回你的手牌。 | 未声明 | — |
| 31. **强撑**<br>[`FightThrough`](../../../../../sts2/src/Core/Models/Cards/FightThrough.cs) | 技能 / 罕见<br>1→1 能量 | 获得13点格挡。<br>将2张伤口加入你的弃牌堆。 | 获得17点格挡。<br>将2张伤口加入你的弃牌堆。 | 未声明 | — |
| 32. **散射炮**<br>[`FlakCannon`](../../../../../sts2/src/Core/Models/Cards/FlakCannon.cs) | 攻击 / 稀有<br>2→2 能量 | 消耗你所有的状态牌。<br>每有一张被消耗的牌，就随机对敌人造成8点伤害。 | 消耗你所有的状态牌。<br>每有一张被消耗的牌，就随机对敌人造成11点伤害。 | 未声明 | — |
| 33. **集中打击**<br>[`FocusedStrike`](../../../../../sts2/src/Core/Models/Cards/FocusedStrike.cs) | 攻击 / 普通<br>1→1 能量 | 造成9点伤害。<br>在本回合获得1点集中。 | 造成11点伤害。<br>在本回合获得2点集中。 | 未声明 | 充能球/槽位/触发、集中与充能球数值 |
| 34. **超越光速**<br>[`Ftl`](../../../../../sts2/src/Core/Models/Cards/Ftl.cs) | 攻击 / 罕见<br>0→0 能量 | 造成5点伤害。<br>如果你在这回合打出的牌数小于3张，抽1张牌。 | 造成6点伤害。<br>如果你在这回合打出的牌数小于4张，抽1张牌。 | 未声明 | — |
| 35. **聚变**<br>[`Fusion`](../../../../../sts2/src/Core/Models/Cards/Fusion.cs) | 技能 / 罕见<br>1→1 能量 | 生成1个等离子充能球。<br>关键字：消耗。 | 生成1个等离子充能球。 | 未声明 | 充能球/槽位/触发 |
| 36. **遗传算法**<br>[`GeneticAlgorithm`](../../../../../sts2/src/Core/Models/Cards/GeneticAlgorithm.cs) | 技能 / 稀有<br>1→1 能量 | 获得1点格挡。<br>每打出一次，这张牌在本局游戏中的格挡值永久增加3点。<br>关键字：消耗。 | 获得1点格挡。<br>每打出一次，这张牌在本局游戏中的格挡值永久增加4点。<br>关键字：消耗。 | 未声明 | —<br>数值说明：显示新牌初始值；已成长的实例按保存值显示 |
| 37. **冰川**<br>[`Glacier`](../../../../../sts2/src/Core/Models/Cards/Glacier.cs) | 技能 / 罕见<br>2→2 能量 | 获得6点格挡。<br>生成2个冰霜充能球。 | 获得9点格挡。<br>生成2个冰霜充能球。 | 未声明 | 充能球/槽位/触发 |
| 38. **玻璃工艺**<br>[`Glasswork`](../../../../../sts2/src/Core/Models/Cards/Glasswork.cs) | 技能 / 罕见<br>1→1 能量 | 获得5点格挡。<br>生成1个玻璃充能球。 | 获得8点格挡。<br>生成1个玻璃充能球。 | 未声明 | 充能球/槽位/触发 |
| 39. **眼部攻击**<br>[`GoForTheEyes`](../../../../../sts2/src/Core/Models/Cards/GoForTheEyes.cs) | 攻击 / 普通<br>0→0 能量 | 造成3点伤害。<br>如果敌人的意图是攻击，则给予1层虚弱。 | 造成4点伤害。<br>如果敌人的意图是攻击，则给予2层虚弱。 | 未声明 | — |
| 40. **污秽攻击**<br>[`GunkUp`](../../../../../sts2/src/Core/Models/Cards/GunkUp.cs) | 攻击 / 普通<br>1→1 能量 | 造成4点伤害3次。<br>在你的弃牌堆中加入一张黏液。 | 造成5点伤害3次。<br>在你的弃牌堆中加入一张黏液。 | 未声明 | — |
| 41. **冰雹风暴**<br>[`Hailstorm`](../../../../../sts2/src/Core/Models/Cards/Hailstorm.cs) | 能力 / 罕见<br>1→1 能量 | 在你的回合结束时，如果你有冰霜充能球，则对所有敌人造成6点伤害。 | 在你的回合结束时，如果你有冰霜充能球，则对所有敌人造成8点伤害。 | 未声明 | 充能球/槽位/触发 |
| 42. **螺旋钻击**<br>[`HelixDrill`](../../../../../sts2/src/Core/Models/Cards/HelixDrill.cs) | 攻击 / 稀有<br>0→0 能量 | 在本回合中，每个被使用的[能量]，都会使此牌造成3点伤害一次。 | 在本回合中，每个被使用的[能量]，都会使此牌造成5点伤害一次。 | 未声明 | — |
| 43. **全息影像**<br>[`Hologram`](../../../../../sts2/src/Core/Models/Cards/Hologram.cs) | 技能 / 普通<br>1→1 能量 | 获得3点格挡。<br>将弃牌堆中的一张牌放入你的手牌。<br>关键字：消耗。 | 获得5点格挡。<br>将弃牌堆中的一张牌放入你的手牌。 | 已注册；有本地行为证据<br>[base:16](../../../../../mod-the-spire/base-spire/BaseSpire/ContentSource/selection-cards.spire)<br>[本地证据:64](../../../../../.audit/owner-shared-run-20261012/slice-final.log) | — |
| 44. **热修复**<br>[`Hotfix`](../../../../../sts2/src/Core/Models/Cards/Hotfix.cs) | 技能 / 普通<br>0→0 能量 | 在本回合获得2点集中。<br>关键字：消耗。 | 在本回合获得2点集中。 | 未声明 | 充能球/槽位/触发、集中与充能球数值 |
| 45. **超能光束**<br>[`Hyperbeam`](../../../../../sts2/src/Core/Models/Cards/Hyperbeam.cs) | 攻击 / 稀有<br>2→2 能量 | 对所有敌人造成28点伤害。<br>失去3点集中。 | 对所有敌人造成36点伤害。<br>失去3点集中。 | 未声明 | 充能球/槽位/触发、集中与充能球数值 |
| 46. **冰之长枪**<br>[`IceLance`](../../../../../sts2/src/Core/Models/Cards/IceLance.cs) | 攻击 / 稀有<br>3→3 能量 | 造成19点伤害。<br>生成3个冰霜充能球。 | 造成24点伤害。<br>生成3个冰霜充能球。 | 未声明 | 充能球/槽位/触发 |
| 47. **引火**<br>[`Ignition`](../../../../../sts2/src/Core/Models/Cards/Ignition.cs)<br>**仅多人** | 技能 / 稀有<br>1→1 能量 | 使另一名玩家生成等离子充能球。<br>关键字：消耗。 | 使另一名玩家生成等离子充能球。 | 未声明 | 充能球/槽位/触发、跨玩家效果 |
| 48. **迭代**<br>[`Iteration`](../../../../../sts2/src/Core/Models/Cards/Iteration.cs) | 能力 / 罕见<br>1→1 能量 | 每回合你第一次抽到状态牌时，抽2张牌。 | 每回合你第一次抽到状态牌时，抽3张牌。 | 未声明 | — |
| 49. **飞跃**<br>[`Leap`](../../../../../sts2/src/Core/Models/Cards/Leap.cs) | 技能 / 普通<br>1→1 能量 | 获得9点格挡。 | 获得12点格挡。 | 已注册；有本地行为证据<br>[base:4](../../../../../mod-the-spire/base-spire/BaseSpire/ContentSource/defect-cards.spire)<br>[本地证据:133](../../../../../.audit/owner-shared-run-20261012/slice-final.log) | — |
| 50. **引雷针**<br>[`LightningRod`](../../../../../sts2/src/Core/Models/Cards/LightningRod.cs) | 技能 / 普通<br>1→1 能量 | 获得4点格挡。<br>在下2个回合开始时,生成1个闪电充能球。 | 获得7点格挡。<br>在下2个回合开始时,生成1个闪电充能球。 | 未声明 | 充能球/槽位/触发 |
| 51. **循环**<br>[`Loop`](../../../../../sts2/src/Core/Models/Cards/Loop.cs) | 能力 / 罕见<br>1→1 能量 | 在你的回合开始时，触发你最右侧的一个充能球的被动能力。 | 在你的回合开始时，触发你最右侧的一个充能球的被动能力2次。 | 未声明 | 充能球/槽位/触发 |
| 52. **机器学习**<br>[`MachineLearning`](../../../../../sts2/src/Core/Models/Cards/MachineLearning.cs) | 能力 / 稀有<br>1→1 能量 | 在你的回合开始时，额外抽1张牌。 | 在你的回合开始时，额外抽1张牌。<br>关键字：固有。 | 未声明 | — |
| 53. **陨石打击**<br>[`MeteorStrike`](../../../../../sts2/src/Core/Models/Cards/MeteorStrike.cs) | 攻击 / 稀有<br>5→5 能量 | 造成24点伤害。<br>生成3个等离子充能球。 | 造成30点伤害。<br>生成3个等离子充能球。 | 未声明 | 充能球/槽位/触发 |
| 54. **模组改造**<br>[`Modded`](../../../../../sts2/src/Core/Models/Cards/Modded.cs) | 技能 / 稀有<br>0→0 能量 | 获得1个充能球栏位。<br>抽1张牌。这张牌的耗能加1。 | 获得1个充能球栏位。<br>抽2张牌。这张牌的耗能加1。 | 未声明 | 充能球/槽位/触发 |
| 55. **趁势打击**<br>[`MomentumStrike`](../../../../../sts2/src/Core/Models/Cards/MomentumStrike.cs) | 攻击 / 普通<br>1→1 能量 | 造成10点伤害。<br>这张牌的耗能降为0[能量]。 | 造成13点伤害。<br>这张牌的耗能降为0[能量]。 | 未声明 | — |
| 56. **多重释放**<br>[`MultiCast`](../../../../../sts2/src/Core/Models/Cards/MultiCast.cs) | 技能 / 稀有<br>X→X 能量 | 激发你最右侧的充能球X次。 | 激发你最右侧的充能球X+1次。 | 未声明 | 充能球/槽位/触发 |
| 57. **空值**<br>[`Null`](../../../../../sts2/src/Core/Models/Cards/Null.cs) | 攻击 / 罕见<br>2→2 能量 | 造成10点伤害。<br>给予2层虚弱。<br>生成1个黑暗充能球。 | 造成13点伤害。<br>给予3层虚弱。<br>生成1个黑暗充能球。 | 未声明 | 充能球/槽位/触发 |
| 58. **超频**<br>[`Overclock`](../../../../../sts2/src/Core/Models/Cards/Overclock.cs) | 技能 / 罕见<br>0→0 能量 | 抽2张牌。<br>将一张灼伤加入你的弃牌堆。 | 抽3张牌。<br>将一张灼伤加入你的弃牌堆。 | 未声明 | — |
| 59. **四重释放**<br>[`Quadcast`](../../../../../sts2/src/Core/Models/Cards/Quadcast.cs) | 技能 / 先古<br>1→0 能量 | 激发你最右侧的充能球4次。 | 激发你最右侧的充能球4次。 | 未声明 | 充能球/槽位/触发 |
| 60. **彩虹**<br>[`Rainbow`](../../../../../sts2/src/Core/Models/Cards/Rainbow.cs) | 技能 / 稀有<br>2→2 能量 | 生成1个闪电充能球。<br>生成1个冰霜充能球。<br>生成1个黑暗充能球。<br>关键字：消耗。 | 生成1个闪电充能球。<br>生成1个冰霜充能球。<br>生成1个黑暗充能球。 | 未声明 | 充能球/槽位/触发 |
| 61. **重启**<br>[`Reboot`](../../../../../sts2/src/Core/Models/Cards/Reboot.cs) | 技能 / 稀有<br>0→0 能量 | 将你的所有未消耗的卡牌重新洗牌放入抽牌堆。<br>抽4张牌。<br>关键字：消耗。 | 将你的所有未消耗的卡牌重新洗牌放入抽牌堆。<br>抽6张牌。<br>关键字：消耗。 | 未声明 | — |
| 62. **折射**<br>[`Refract`](../../../../../sts2/src/Core/Models/Cards/Refract.cs) | 攻击 / 罕见<br>3→3 能量 | 造成9点伤害两次。<br>生成2个玻璃充能球。 | 造成12点伤害两次。<br>生成2个玻璃充能球。 | 未声明 | 充能球/槽位/触发 |
| 63. **火箭飞拳**<br>[`RocketPunch`](../../../../../sts2/src/Core/Models/Cards/RocketPunch.cs) | 攻击 / 罕见<br>2→2 能量 | 造成13点伤害。<br>抽1张牌。<br>每当你生成状态牌时，此牌的耗能将在下一次打出前降为0[能量]。 | 造成14点伤害。<br>抽2张牌。<br>每当你生成状态牌时，此牌的耗能将在下一次打出前降为0[能量]。 | 未声明 | — |
| 64. **内存清理**<br>[`Scavenge`](../../../../../sts2/src/Core/Models/Cards/Scavenge.cs) | 技能 / 罕见<br>1→1 能量 | 消耗一张牌。<br>在下个回合获得2点能量。 | 消耗一张牌。<br>在下个回合获得3点能量。 | 未声明 | — |
| 65. **刮削**<br>[`Scrape`](../../../../../sts2/src/Core/Models/Cards/Scrape.cs) | 攻击 / 罕见<br>1→1 能量 | 造成7点伤害。<br>抽4张牌。<br>丢弃抽到的牌中耗能不为0[能量]的牌。 | 造成10点伤害。<br>抽5张牌。<br>丢弃抽到的牌中耗能不为0[能量]的牌。 | 未声明 | — |
| 66. **暗影之盾**<br>[`ShadowShield`](../../../../../sts2/src/Core/Models/Cards/ShadowShield.cs) | 技能 / 罕见<br>2→2 能量 | 获得11点格挡。<br>生成1个黑暗充能球。 | 获得15点格挡。<br>生成1个黑暗充能球。 | 未声明 | 充能球/槽位/触发 |
| 67. **打碎**<br>[`Shatter`](../../../../../sts2/src/Core/Models/Cards/Shatter.cs) | 攻击 / 稀有<br>1→1 能量 | 对所有敌人造成7点伤害。<br>激发你的所有充能球两次。 | 对所有敌人造成11点伤害。<br>激发你的所有充能球两次。 | 未声明 | 充能球/槽位/触发 |
| 68. **信号增强**<br>[`SignalBoost`](../../../../../sts2/src/Core/Models/Cards/SignalBoost.cs) | 技能 / 稀有<br>1→0 能量 | 你的下一张能力牌会额外打出一次。<br>关键字：消耗。 | 你的下一张能力牌会额外打出一次。<br>关键字：消耗。 | 未声明 | — |
| 69. **快速检索**<br>[`Skim`](../../../../../sts2/src/Core/Models/Cards/Skim.cs) | 技能 / 罕见<br>1→1 能量 | 抽3张牌。 | 抽4张牌。 | 已注册；有本地行为证据<br>[base:96](../../../../../mod-the-spire/base-spire/BaseSpire/ContentSource/defect-cards.spire)<br>[本地证据:284](../../../../../.audit/owner-shared-run-20261012/slice-final.log) | — |
| 70. **烟囱**<br>[`Smokestack`](../../../../../sts2/src/Core/Models/Cards/Smokestack.cs) | 能力 / 罕见<br>1→1 能量 | 每当你生成一张状态牌时，对所有敌人造成5点伤害。 | 每当你生成一张状态牌时，对所有敌人造成7点伤害。 | 未声明 | — |
| 71. **旋转工艺**<br>[`Spinner`](../../../../../sts2/src/Core/Models/Cards/Spinner.cs) | 能力 / 稀有<br>1→1 能量 | 在你的回合开始时，生成1个玻璃充能球。 | 生成1个玻璃充能球。<br>在你的回合开始时，生成1个玻璃充能球。 | 未声明 | 充能球/槽位/触发 |
| 72. **雷暴**<br>[`Storm`](../../../../../sts2/src/Core/Models/Cards/Storm.cs) | 能力 / 罕见<br>1→1 能量 | 每当你打出一张能力牌时，生成1个闪电充能球。 | 每当你打出一张能力牌时，生成2个闪电充能球。 | 未声明 | 充能球/槽位/触发 |
| 73. **打击**<br>[`StrikeDefect`](../../../../../sts2/src/Core/Models/Cards/StrikeDefect.cs) | 攻击 / 基础<br>1→1 能量 | 造成6点伤害。 | 造成9点伤害。 | 已注册；有本地行为证据<br>[base:21](../../../../../mod-the-spire/base-spire/BaseSpire/ContentSource/defect-cards.spire)<br>[本地证据:197](../../../../../.audit/owner-shared-run-20261012/slice-final.log) | — |
| 74. **子程序**<br>[`Subroutine`](../../../../../sts2/src/Core/Models/Cards/Subroutine.cs) | 能力 / 罕见<br>1→0 能量 | 当你打出一张能力牌时，获得[能量]。 | 当你打出一张能力牌时，获得[能量]。 | 未声明 | — |
| 75. **分离**<br>[`Sunder`](../../../../../sts2/src/Core/Models/Cards/Sunder.cs) | 攻击 / 罕见<br>3→3 能量 | 造成24点伤害。<br>如果这张牌击杀了敌人，则获得3点能量。 | 造成32点伤害。<br>如果这张牌击杀了敌人，则获得3点能量。 | 未声明 | — |
| 76. **超临界态**<br>[`Supercritical`](../../../../../sts2/src/Core/Models/Cards/Supercritical.cs) | 技能 / 稀有<br>0→0 能量 | 获得4点能量。<br>关键字：消耗。 | 获得6点能量。<br>关键字：消耗。 | 已注册；有本地行为证据<br>[base:78](../../../../../mod-the-spire/base-spire/BaseSpire/ContentSource/ready-cards.spire)<br>[本地证据:106](../../../../../.audit/owner-shared-run-20261012/slice-final.log) | — |
| 77. **扫荡射线**<br>[`SweepingBeam`](../../../../../sts2/src/Core/Models/Cards/SweepingBeam.cs) | 攻击 / 普通<br>1→1 能量 | 对所有敌人造成6点伤害。<br>抽1张牌。 | 对所有敌人造成9点伤害。<br>抽1张牌。 | 已注册；有本地行为证据<br>[base:88](../../../../../mod-the-spire/base-spire/BaseSpire/ContentSource/defect-cards.spire)<br>[本地证据:281](../../../../../.audit/owner-shared-run-20261012/slice-final.log) | — |
| 78. **同步**<br>[`Synchronize`](../../../../../sts2/src/Core/Models/Cards/Synchronize.cs) | 技能 / 罕见<br>1→1 能量 | 你每有一种不同的充能球，就在本回合获得2点集中。<br>关键字：消耗。 | 你每有一种不同的充能球，就在本回合获得2点集中。 | 未声明 | 充能球/槽位/触发、集中与充能球数值 |
| 79. **人工合成**<br>[`Synthesis`](../../../../../sts2/src/Core/Models/Cards/Synthesis.cs) | 攻击 / 罕见<br>2→2 能量 | 造成14点伤害。<br>你打出的下一张能力牌耗能变为0[能量]。 | 造成20点伤害。<br>你打出的下一张能力牌耗能变为0[能量]。 | 未声明 | — |
| 80. **暴风雨**<br>[`Tempest`](../../../../../sts2/src/Core/Models/Cards/Tempest.cs) | 技能 / 罕见<br>X→X 能量 | 生成X个闪电充能球。 | 生成X+1个闪电充能球。 | 未声明 | 充能球/槽位/触发 |
| 81. **特斯拉线圈**<br>[`TeslaCoil`](../../../../../sts2/src/Core/Models/Cards/TeslaCoil.cs) | 攻击 / 罕见<br>0→0 能量 | 造成3点伤害。<br>对该敌人触发你的所有闪电充能球的被动。 | 造成4点伤害。<br>对该敌人触发你的所有闪电充能球的被动 两次。 | 未声明 | 充能球/槽位/触发 |
| 82. **雷霆**<br>[`Thunder`](../../../../../sts2/src/Core/Models/Cards/Thunder.cs) | 能力 / 罕见<br>1→1 能量 | 每当你激发闪电充能球时，对被命中的敌人造成6点伤害。 | 每当你激发闪电充能球时，对被命中的敌人造成8点伤害。 | 未声明 | 充能球/槽位/触发 |
| 83. **化废为宝**<br>[`TrashToTreasure`](../../../../../sts2/src/Core/Models/Cards/TrashToTreasure.cs) | 能力 / 稀有<br>1→1 能量 | 每当你生成状态牌的时候，随机生成一个充能球。 | 每当你生成状态牌的时候，随机生成一个充能球。<br>关键字：固有。 | 未声明 | 充能球/槽位/触发 |
| 84. **内核加速**<br>[`Turbo`](../../../../../sts2/src/Core/Models/Cards/Turbo.cs) | 技能 / 普通<br>0→0 能量 | 获得2点能量。<br>将一张虚空加入你的弃牌堆。 | 获得3点能量。<br>将一张虚空加入你的弃牌堆。 | 未声明 | — |
| 85. **骚动**<br>[`Uproar`](../../../../../sts2/src/Core/Models/Cards/Uproar.cs) | 攻击 / 普通<br>2→2 能量 | 造成6点伤害两次。<br>随机打出你的抽牌堆中的1张攻击牌。 | 造成8点伤害两次。<br>随机打出你的抽牌堆中的1张攻击牌。 | 未声明 | — |
| 86. **电流相生**<br>[`Voltaic`](../../../../../sts2/src/Core/Models/Cards/Voltaic.cs) | 技能 / 稀有<br>3→3 能量 | 生成等量于你在这场战斗中生成过的闪电充能球数量的闪电充能球。<br>关键字：消耗。 | 生成等量于你在这场战斗中生成过的闪电充能球数量的闪电充能球。 | 未声明 | 充能球/槽位/触发 |
| 87. **白噪声**<br>[`WhiteNoise`](../../../../../sts2/src/Core/Models/Cards/WhiteNoise.cs) | 技能 / 罕见<br>1→0 能量 | 将一张随机能力牌加入你的手牌。这张牌在本回合内免费打出。<br>关键字：消耗。 | 将一张随机能力牌加入你的手牌。这张牌在本回合内免费打出。<br>关键字：消耗。 | 未声明 | — |
| 88. **电击**<br>[`Zap`](../../../../../sts2/src/Core/Models/Cards/Zap.cs) | 技能 / 基础<br>1→0 能量 | 生成1个闪电充能球。 | 生成1个闪电充能球。 | 未声明 | 充能球/槽位/触发 |

“—”仅表示没有命中本次专属机制检索标签，不能据此认定该牌只需要简单原语。源标识链接指向原版卡类；base 链接中的数字是声明行，本地证据数字是对应日志/检查源码行。
