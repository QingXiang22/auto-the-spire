# 储君卡牌校对清单（2026-10-12）

原版 [RegentCardPool](../../../../../sts2/src/Core/Models/CardPools/RegentCardPool.cs) 共 **88** 张。

[返回清单总览](README.md)。这是源快照与实现证据的日期记录；列含义、符号及未验证范围见总览。普通/升级描述来自同一条中文本地化模板，数值只取原版源码；不会根据 base 的实现反推描述。

| # / 卡牌 / 原版标识 | 类型、稀有度与费用（普通→升级） | 原版普通描述 | 原版升级描述 | base 实现证据 | 专属/共享机制线索 |
| --- | --- | --- | --- | --- | --- |
| 1. **星位序列**<br>[`Alignment`](../../../../../sts2/src/Core/Models/Cards/Alignment.cs) | 技能 / 罕见<br>0→0 能量<br>3→3 ★ | 获得2点能量。 | 获得3点能量。 | 未声明 | 星星资源/星星费用 |
| 2. **武器库**<br>[`Arsenal`](../../../../../sts2/src/Core/Models/Cards/Arsenal.cs) | 能力 / 稀有<br>1→1 能量 | 每当你生成一张牌，就获得1点力量。 | 每当你生成一张牌，就获得1点力量。<br>关键字：固有。 | 未声明 | — |
| 3. **星界脉冲**<br>[`AstralPulse`](../../../../../sts2/src/Core/Models/Cards/AstralPulse.cs) | 攻击 / 普通<br>0→0 能量<br>3→3 ★ | 对所有敌人造成6点伤害两次。 | 对所有敌人造成8点伤害两次。 | 未声明 | 星星资源/星星费用 |
| 4. **锻打成型**<br>[`BeatIntoShape`](../../../../../sts2/src/Core/Models/Cards/BeatIntoShape.cs) | 攻击 / 稀有<br>1→1 能量 | 造成5点伤害。<br>铸造【动态铸造值】。<br>本回合内，你此前每击中过该敌人一次，这张牌就额外铸造5点。 | 造成7点伤害。<br>铸造【动态铸造值】。<br>本回合内，你此前每击中过该敌人一次，这张牌就额外铸造7点。 | 未声明 | 铸造/君主之剑<br>数值说明：基础铸造5→7；实际值随描述中的条件变化 |
| 5. **下去！**<br>[`Begone`](../../../../../sts2/src/Core/Models/Cards/Begone.cs) | 技能 / 普通<br>1→1 能量 | 选择你手牌中的一张牌，将其变化为仆从打击。 | 选择你手牌中的一张牌，将其变化为仆从打击+。 | 未声明 | 仆从衍生牌/变化 |
| 6. **大爆炸**<br>[`BigBang`](../../../../../sts2/src/Core/Models/Cards/BigBang.cs) | 技能 / 稀有<br>0→0 能量 | 抽1张牌。<br>获得1点能量。<br>获得★。<br>铸造5。<br>关键字：消耗。 | 抽1张牌。<br>获得1点能量。<br>获得★。<br>铸造5。<br>关键字：消耗、固有。 | 未声明 | 星星资源/星星费用、铸造/君主之剑 |
| 7. **黑洞**<br>[`BlackHole`](../../../../../sts2/src/Core/Models/Cards/BlackHole.cs) | 能力 / 罕见<br>1→1 能量 | 每当你花费或获得★时，对所有敌人造成3点伤害。 | 每当你花费或获得★时，对所有敌人造成4点伤害。 | 未声明 | 星星资源/星星费用 |
| 8. **轰击**<br>[`Bombardment`](../../../../../sts2/src/Core/Models/Cards/Bombardment.cs) | 攻击 / 稀有<br>3→3 能量 | 造成18点伤害。<br>你的回合开始时，如果这张牌在你的消耗牌堆中，则将其打出。<br>关键字：消耗。 | 造成24点伤害。<br>你的回合开始时，如果这张牌在你的消耗牌堆中，则将其打出。<br>关键字：消耗。 | 未声明 | — |
| 9. **铸墙**<br>[`Bulwark`](../../../../../sts2/src/Core/Models/Cards/Bulwark.cs) | 技能 / 罕见<br>2→2 能量 | 获得12点格挡。<br>铸造10。 | 获得15点格挡。<br>铸造13。 | 未声明 | 铸造/君主之剑 |
| 10. **新生之喜**<br>[`BundleOfJoy`](../../../../../sts2/src/Core/Models/Cards/BundleOfJoy.cs) | 技能 / 稀有<br>1→1 能量 | 将3张随机无色牌添加到你的手牌。<br>关键字：消耗。 | 将4张随机无色牌添加到你的手牌。<br>关键字：消耗。 | 未声明 | — |
| 11. **天穹之力**<br>[`CelestialMight`](../../../../../sts2/src/Core/Models/Cards/CelestialMight.cs) | 攻击 / 普通<br>2→2 能量 | 造成6点伤害3次。 | 造成6点伤害4次。 | 已注册；有本地行为证据<br>[base:92](../../../../../tools/base-migration-baseline/BaseSpire/Cards/Content/ready-cards.spire)<br>[本地证据:112](../../../../../.audit/owner-shared-run-20261012/slice-final.log) | — |
| 12. **冲锋！！**<br>[`Charge`](../../../../../sts2/src/Core/Models/Cards/Charge.cs) | 技能 / 罕见<br>1→1 能量 | 选择你抽牌堆中的2张牌，将其变化为<br>仆从俯冲。 | 选择你抽牌堆中的2张牌，将其变化为<br>仆从俯冲+。 | 未声明 | 仆从衍生牌/变化 |
| 13. **群星之子**<br>[`ChildOfTheStars`](../../../../../sts2/src/Core/Models/Cards/ChildOfTheStars.cs) | 能力 / 罕见<br>1→1 能量 | 每当你花费★时，每花费一点★，获得2点格挡。 | 每当你花费★时，每花费一点★，获得3点格挡。 | 未声明 | 星星资源/星星费用 |
| 14. **群星斗篷**<br>[`CloakOfStars`](../../../../../sts2/src/Core/Models/Cards/CloakOfStars.cs) | 技能 / 普通<br>0→0 能量<br>1→1 ★ | 获得7点格挡。 | 获得10点格挡。 | 未声明 | 星星资源/星星费用 |
| 15. **碰撞轨迹**<br>[`CollisionCourse`](../../../../../sts2/src/Core/Models/Cards/CollisionCourse.cs) | 攻击 / 普通<br>0→0 能量 | 造成11点伤害。<br>将一张碎屑添加至你的手牌。 | 造成15点伤害。<br>将一张碎屑添加至你的手牌。 | 未声明 | — |
| 16. **彗星**<br>[`Comet`](../../../../../sts2/src/Core/Models/Cards/Comet.cs) | 攻击 / 稀有<br>0→0 能量<br>5→5 ★ | 造成33点伤害。<br>给予3层虚弱。<br>给予3层易伤。 | 造成44点伤害。<br>给予3层虚弱。<br>给予3层易伤。 | 未声明 | 星星资源/星星费用 |
| 17. **征服者**<br>[`Conqueror`](../../../../../sts2/src/Core/Models/Cards/Conqueror.cs) | 技能 / 罕见<br>1→1 能量 | 铸造3。<br>君王之剑在本回合对敌人造成双倍伤害。 | 铸造5。<br>君王之剑在本回合对敌人造成双倍伤害。 | 未声明 | 铸造/君主之剑 |
| 18. **汇流**<br>[`Convergence`](../../../../../sts2/src/Core/Models/Cards/Convergence.cs) | 技能 / 罕见<br>1→1 能量 | 在本回合保留你的手牌。<br>在下个回合，<br>获得1点能量与★。 | 在本回合保留你的手牌。<br>在下个回合，<br>获得1点能量与★★。 | 未声明 | 星星资源/星星费用 |
| 19. **宇宙冷漠**<br>[`CosmicIndifference`](../../../../../sts2/src/Core/Models/Cards/CosmicIndifference.cs) | 技能 / 普通<br>1→1 能量 | 获得6点格挡。<br>将弃牌堆中的一张牌放到你的抽牌堆的顶部。 | 获得9点格挡。<br>将弃牌堆中的一张牌放到你的抽牌堆的顶部。 | 已注册；有本地行为证据<br>[base:56](../../../../../tools/base-migration-baseline/BaseSpire/Cards/Content/ready-cards.spire)<br>[本地证据:97](../../../../../.audit/owner-shared-run-20261012/slice-final.log) | — |
| 20. **迫降**<br>[`CrashLanding`](../../../../../sts2/src/Core/Models/Cards/CrashLanding.cs) | 攻击 / 稀有<br>1→1 能量 | 对所有敌人造成21点伤害。<br>用碎屑填满你的手牌。 | 对所有敌人造成26点伤害。<br>用碎屑填满你的手牌。 | 未声明 | — |
| 21. **新月长矛**<br>[`CrescentSpear`](../../../../../sts2/src/Core/Models/Cards/CrescentSpear.cs) | 攻击 / 普通<br>1→1 能量<br>1→1 ★ | 造成【动态伤害】点伤害。<br>你每有一张拥有★耗能的卡牌，这张牌就额外造成2点伤害。 | 造成【动态伤害】点伤害。<br>你每有一张拥有★耗能的卡牌，这张牌就额外造成3点伤害。 | 未声明 | 星星资源/星星费用<br>数值说明：基础伤害8→8；实际值随描述中的条件变化 |
| 22. **下砸**<br>[`CrushUnder`](../../../../../sts2/src/Core/Models/Cards/CrushUnder.cs) | 攻击 / 普通<br>1→1 能量 | 对所有敌人造成7点伤害。所有敌人在本回合失去1点力量。 | 对所有敌人造成8点伤害。所有敌人在本回合失去2点力量。 | 未声明 | — |
| 23. **抉择，抉择**<br>[`DecisionsDecisions`](../../../../../sts2/src/Core/Models/Cards/DecisionsDecisions.cs) | 技能 / 稀有<br>0→0 能量<br>6→6 ★ | 抽3张牌。<br>选择你手牌中的一张技能牌，并将其打出3次。<br>关键字：消耗。 | 抽5张牌。<br>选择你手牌中的一张技能牌，并将其打出3次。<br>关键字：消耗。 | 未声明 | 星星资源/星星费用 |
| 24. **防御**<br>[`DefendRegent`](../../../../../sts2/src/Core/Models/Cards/DefendRegent.cs) | 技能 / 基础<br>1→1 能量 | 获得5点格挡。 | 获得8点格挡。 | 已注册；有本地行为证据<br>[base:29](../../../../../tools/base-migration-baseline/BaseSpire/Cards/Content/regent-cards.spire)<br>[本地证据:212](../../../../../.audit/owner-shared-run-20261012/slice-final.log) | — |
| 25. **葬送**<br>[`Devastate`](../../../../../sts2/src/Core/Models/Cards/Devastate.cs) | 攻击 / 罕见<br>1→1 能量<br>4→4 ★ | 造成30点伤害。 | 造成40点伤害。 | 未声明 | 星星资源/星星费用 |
| 26. **星灭**<br>[`DyingStar`](../../../../../sts2/src/Core/Models/Cards/DyingStar.cs) | 攻击 / 稀有<br>1→1 能量<br>3→3 ★ | 对所有敌人造成9点伤害。所有敌人在本回合中失去9点力量。<br>关键字：虚无。 | 对所有敌人造成11点伤害。所有敌人在本回合中失去11点力量。<br>关键字：虚无。 | 未声明 | 星星资源/星星费用 |
| 27. **陨星**<br>[`FallingStar`](../../../../../sts2/src/Core/Models/Cards/FallingStar.cs) | 攻击 / 基础<br>0→0 能量<br>2→2 ★ | 造成8点伤害。<br>给予1层虚弱。<br>给予1层易伤。 | 造成12点伤害。<br>给予1层虚弱。<br>给予1层易伤。 | 未声明 | 星星资源/星星费用 |
| 28. **既定事项**<br>[`ForegoneConclusion`](../../../../../sts2/src/Core/Models/Cards/ForegoneConclusion.cs) | 技能 / 稀有<br>1→1 能量 | 在下个回合，从你的抽牌堆中选择2张牌放入你的手牌。 | 在下个回合，从你的抽牌堆中选择3张牌放入你的手牌。 | 未声明 | — |
| 29. **熔炉**<br>[`Furnace`](../../../../../sts2/src/Core/Models/Cards/Furnace.cs) | 能力 / 罕见<br>1→1 能量 | 在你的回合开始时，铸造5。 | 在你的回合开始时，铸造7。 | 未声明 | 铸造/君主之剑 |
| 30. **伽马爆破**<br>[`GammaBlast`](../../../../../sts2/src/Core/Models/Cards/GammaBlast.cs) | 攻击 / 罕见<br>0→0 能量<br>3→3 ★ | 造成13点伤害。<br>给予2层虚弱。<br>给予2层易伤。 | 造成18点伤害。<br>给予2层虚弱。<br>给予2层易伤。 | 未声明 | 星星资源/星星费用 |
| 31. **收集光辉**<br>[`GatherLight`](../../../../../sts2/src/Core/Models/Cards/GatherLight.cs) | 技能 / 普通<br>1→1 能量 | 获得8点格挡。<br>获得★。 | 获得11点格挡。<br>获得★。 | 未声明 | 星星资源/星星费用 |
| 32. **创世纪**<br>[`Genesis`](../../../../../sts2/src/Core/Models/Cards/Genesis.cs) | 能力 / 稀有<br>2→2 能量 | 在你的回合开始时，获得★★。 | 在你的回合开始时，获得★★★。 | 未声明 | 星星资源/星星费用 |
| 33. **微光**<br>[`Glimmer`](../../../../../sts2/src/Core/Models/Cards/Glimmer.cs) | 技能 / 罕见<br>1→1 能量 | 抽3张牌。<br>将你手牌中的1张牌放到抽牌堆顶部。 | 抽4张牌。<br>将你手牌中的1张牌放到抽牌堆顶部。 | 已注册；有本地行为证据<br>[base:42](../../../../../tools/base-migration-baseline/BaseSpire/Cards/Content/ready-cards.spire)<br>[本地证据:91](../../../../../.audit/owner-shared-run-20261012/slice-final.log) | — |
| 34. **流光溢彩**<br>[`Glitterstream`](../../../../../sts2/src/Core/Models/Cards/Glitterstream.cs) | 技能 / 普通<br>2→2 能量 | 获得11点格挡。<br>在下一回合获得5点格挡。 | 获得13点格挡。<br>在下一回合获得7点格挡。 | 未声明 | — |
| 35. **辉光**<br>[`Glow`](../../../../../sts2/src/Core/Models/Cards/Glow.cs) | 技能 / 普通<br>1→1 能量 | 获得★。<br>抽1张牌。<br>下一回合抽1张牌。 | 获得★★。<br>抽1张牌。<br>下一回合抽1张牌。 | 未声明 | 星星资源/星星费用 |
| 36. **护驾！！！**<br>[`Guards`](../../../../../sts2/src/Core/Models/Cards/Guards.cs) | 技能 / 稀有<br>2→2 能量 | 将你手牌中的任意张牌变化为仆从捐躯。<br>关键字：消耗。 | 将你手牌中的任意张牌变化为仆从捐躯+。<br>关键字：消耗。 | 未声明 | 仆从衍生牌/变化 |
| 37. **引导之星**<br>[`GuidingStar`](../../../../../sts2/src/Core/Models/Cards/GuidingStar.cs) | 攻击 / 普通<br>1→1 能量<br>2→2 ★ | 造成12点伤害。<br>抽2张牌。 | 造成13点伤害。<br>抽3张牌。 | 未声明 | 星星资源/星星费用 |
| 38. **锤子时间**<br>[`HammerTime`](../../../../../sts2/src/Core/Models/Cards/HammerTime.cs)<br>**仅多人** | 能力 / 稀有<br>2→1 能量 | 每当你铸造时，所有盟友也都铸造相同的数值。 | 每当你铸造时，所有盟友也都铸造相同的数值。 | 未声明 | 铸造/君主之剑、跨玩家效果 |
| 39. **天际钻头**<br>[`HeavenlyDrill`](../../../../../sts2/src/Core/Models/Cards/HeavenlyDrill.cs) | 攻击 / 稀有<br>X→X 能量 | 造成8点伤害X次。<br>如果X的最终数值为4或以上，则将X的数值翻倍。 | 造成10点伤害X次。<br>如果X的最终数值为4或以上，则将X的数值翻倍。 | 未声明 | — |
| 40. **霸权**<br>[`Hegemony`](../../../../../sts2/src/Core/Models/Cards/Hegemony.cs) | 攻击 / 罕见<br>2→2 能量 | 造成15点伤害。<br>在下个回合获得2点能量。 | 造成18点伤害。<br>在下个回合获得3点能量。 | 未声明 | — |
| 41. **传承之锤**<br>[`HeirloomHammer`](../../../../../sts2/src/Core/Models/Cards/HeirloomHammer.cs) | 攻击 / 稀有<br>2→2 能量 | 造成20点伤害。<br>选择你手牌中的一张无色牌。将这张牌的一张复制品放入你的手牌。 | 造成25点伤害。<br>选择你手牌中的一张无色牌。将这张牌的一张复制品放入你的手牌。 | 未声明 | — |
| 42. **隐秘藏品**<br>[`HiddenCache`](../../../../../sts2/src/Core/Models/Cards/HiddenCache.cs) | 技能 / 普通<br>1→1 能量 | 获得★。<br>在下个回合获得★★★。 | 获得★。<br>在下个回合获得★★★★。 | 未声明 | 星星资源/星星费用 |
| 43. **所向无敌**<br>[`IAmInvincible`](../../../../../sts2/src/Core/Models/Cards/IAmInvincible.cs) | 技能 / 稀有<br>1→1 能量 | 获得10点格挡。<br>在你的回合结束时，如果这张牌位于抽牌堆顶部，则将其打出。 | 获得13点格挡。<br>在你的回合结束时，如果这张牌位于抽牌堆顶部，则将其打出。 | 未声明 | — |
| 44. **王者之踢**<br>[`KinglyKick`](../../../../../sts2/src/Core/Models/Cards/KinglyKick.cs) | 攻击 / 罕见<br>4→4 能量 | 造成27点伤害。<br>每当你抽到这张牌时，这张牌的耗能减少1。 | 造成35点伤害。<br>每当你抽到这张牌时，这张牌的耗能减少1。 | 未声明 | — |
| 45. **王者之拳**<br>[`KinglyPunch`](../../../../../sts2/src/Core/Models/Cards/KinglyPunch.cs) | 攻击 / 罕见<br>1→1 能量 | 造成8点伤害。<br>每当你抽到这张牌时，在这场战斗中其伤害增加4。 | 造成10点伤害。<br>每当你抽到这张牌时，在这场战斗中其伤害增加6。 | 未声明 | — |
| 46. **决胜一击**<br>[`KnockoutBlow`](../../../../../sts2/src/Core/Models/Cards/KnockoutBlow.cs) | 攻击 / 罕见<br>3→3 能量 | 造成30点伤害。<br>如果此牌击杀了敌人，获得★★★★★。 | 造成38点伤害。<br>如果此牌击杀了敌人，获得★★★★★。 | 未声明 | 星星资源/星星费用 |
| 47. **何人僭越**<br>[`KnowThyPlace`](../../../../../sts2/src/Core/Models/Cards/KnowThyPlace.cs) | 技能 / 普通<br>0→0 能量 | 给予1层虚弱。<br>给予1层易伤。<br>关键字：消耗。 | 给予1层虚弱。<br>给予1层易伤。 | 已注册；有本地行为证据<br>[base:99](../../../../../tools/base-migration-baseline/BaseSpire/Cards/Content/ready-cards.spire)<br>[本地证据:115](../../../../../.audit/owner-shared-run-20261012/slice-final.log) | — |
| 48. **慷慨捐助**<br>[`Largesse`](../../../../../sts2/src/Core/Models/Cards/Largesse.cs)<br>**仅多人** | 技能 / 罕见<br>0→0 能量 | 将一张随机的无色牌添加至一位其他玩家的手牌中。 | 将一张随机的升级过的无色牌添加至一位其他玩家的手牌中。 | 未声明 | 跨玩家效果 |
| 49. **月面射击**<br>[`LunarBlast`](../../../../../sts2/src/Core/Models/Cards/LunarBlast.cs) | 攻击 / 罕见<br>0→0 能量 | 本回合中每打出过一张技能牌，此牌额外造成4点伤害一次。 | 本回合中每打出过一张技能牌，此牌额外造成5点伤害一次。 | 已注册；有本地行为证据<br>[base:19](../../../../../tools/base-migration-baseline/BaseSpire/Cards/Content/ready-cards.spire)<br>[本地证据:82](../../../../../.audit/owner-shared-run-20261012/slice-final.log) | — |
| 50. **如此甚好**<br>[`MakeItSo`](../../../../../sts2/src/Core/Models/Cards/MakeItSo.cs) | 攻击 / 稀有<br>0→0 能量 | 造成6点伤害。<br>你每在一回合内打出3张技能牌，就将这张牌放入你的手牌。 | 造成9点伤害。<br>你每在一回合内打出3张技能牌，就将这张牌放入你的手牌。 | 未声明 | — |
| 51. **君权自授**<br>[`ManifestAuthority`](../../../../../sts2/src/Core/Models/Cards/ManifestAuthority.cs) | 技能 / 罕见<br>1→1 能量 | 获得7点格挡。<br>将一张随机无色牌加入你的手牌。 | 获得8点格挡。<br>将一张随机升级过的无色牌加入你的手牌。 | 未声明 | — |
| 52. **流星雨**<br>[`MeteorShower`](../../../../../sts2/src/Core/Models/Cards/MeteorShower.cs) | 攻击 / 先古<br>0→0 能量<br>2→2 ★ | 对所有敌人造成14点伤害。<br>给予所有敌人2层虚弱和易伤。 | 对所有敌人造成21点伤害。<br>给予所有敌人2层虚弱和易伤。 | 未声明 | 星星资源/星星费用 |
| 53. **王之凝视**<br>[`MonarchsGaze`](../../../../../sts2/src/Core/Models/Cards/MonarchsGaze.cs) | 能力 / 稀有<br>2→1 能量 | 每当你攻击敌人的时候，这名敌人在本回合失去1点力量。 | 每当你攻击敌人的时候，这名敌人在本回合失去1点力量。 | 未声明 | — |
| 54. **独白**<br>[`Monologue`](../../../../../sts2/src/Core/Models/Cards/Monologue.cs) | 技能 / 罕见<br>0→0 能量 | 每当你在本回合打出卡牌时，在本回合获得1点力量。 | 每当你在本回合打出卡牌时，在本回合获得1点力量。<br>关键字：保留。 | 未声明 | — |
| 55. **中子护盾**<br>[`NeutronAegis`](../../../../../sts2/src/Core/Models/Cards/NeutronAegis.cs) | 能力 / 稀有<br>1→1 能量<br>5→5 ★ | 获得8层覆甲。 | 获得11层覆甲。 | 未声明 | 星星资源/星星费用 |
| 56. **环绕轨道**<br>[`Orbit`](../../../../../sts2/src/Core/Models/Cards/Orbit.cs) | 能力 / 罕见<br>2→1 能量 | 你每花费[能量][能量][能量][能量]，<br>就获得1点能量。 | 你每花费[能量][能量][能量][能量]，<br>就获得1点能量。 | 未声明 | — |
| 57. **暗淡蓝点**<br>[`PaleBlueDot`](../../../../../sts2/src/Core/Models/Cards/PaleBlueDot.cs) | 能力 / 罕见<br>1→1 能量 | 如果你在一回合内打出了大于等于5张牌，在下个回合开始时抽1张牌。 | 如果你在一回合内打出了大于等于5张牌，在下个回合开始时抽2张牌。 | 未声明 | — |
| 58. **招架**<br>[`Parry`](../../../../../sts2/src/Core/Models/Cards/Parry.cs) | 能力 / 罕见<br>1→1 能量 | 君王之剑现在能让你获得10点格挡。 | 君王之剑现在能让你获得14点格挡。 | 未声明 | 铸造/君主之剑 |
| 59. **粒子墙**<br>[`ParticleWall`](../../../../../sts2/src/Core/Models/Cards/ParticleWall.cs) | 技能 / 罕见<br>0→0 能量<br>2→2 ★ | 获得9点格挡。<br>将此牌返回你的手牌。 | 获得12点格挡。<br>将此牌返回你的手牌。 | 未声明 | 星星资源/星星费用 |
| 60. **星星点点**<br>[`Patter`](../../../../../sts2/src/Core/Models/Cards/Patter.cs) | 技能 / 普通<br>1→1 能量 | 获得8点格挡。<br>获得2点活力。 | 获得10点格挡。<br>获得3点活力。 | 未声明 | — |
| 61. **光子切割**<br>[`PhotonCut`](../../../../../sts2/src/Core/Models/Cards/PhotonCut.cs) | 攻击 / 普通<br>1→1 能量 | 造成10点伤害。<br>抽1张牌。<br>将手牌中的一张牌放到抽牌堆顶部。 | 造成13点伤害。<br>抽2张牌。<br>将手牌中的一张牌放到抽牌堆顶部。 | 已注册；有本地行为证据<br>[base:63](../../../../../tools/base-migration-baseline/BaseSpire/Cards/Content/ready-cards.spire)<br>[本地证据:100](../../../../../.audit/owner-shared-run-20261012/slice-final.log) | — |
| 62. **创世之柱**<br>[`PillarOfCreation`](../../../../../sts2/src/Core/Models/Cards/PillarOfCreation.cs) | 能力 / 罕见<br>1→1 能量 | 每当你生成一张卡牌, 就获得3点格挡。 | 每当你生成一张卡牌, 就获得4点格挡。 | 未声明 | — |
| 63. **预言**<br>[`Prophesize`](../../../../../sts2/src/Core/Models/Cards/Prophesize.cs) | 技能 / 罕见<br>2→2 能量 | 抽6张牌。 | 抽9张牌。 | 已注册；有本地行为证据<br>[base:56](../../../../../tools/base-migration-baseline/BaseSpire/Cards/Content/regent-cards.spire)<br>[本地证据:245](../../../../../.audit/owner-shared-run-20261012/slice-final.log) | — |
| 64. **类星体**<br>[`Quasar`](../../../../../sts2/src/Core/Models/Cards/Quasar.cs) | 技能 / 罕见<br>0→0 能量<br>2→2 ★ | 从3张随机无色牌中选择1张加入你的手牌。 | 从3张随机升级过的无色牌中选择1张加入你的手牌。 | 未声明 | 星星资源/星星费用 |
| 65. **辐射**<br>[`Radiate`](../../../../../sts2/src/Core/Models/Cards/Radiate.cs) | 攻击 / 罕见<br>0→0 能量 | 你在本回合每获得一点★，此牌就对所有敌人造成3点伤害一次。 | 你在本回合每获得一点★，此牌就对所有敌人造成4点伤害一次。 | 未声明 | 星星资源/星星费用 |
| 66. **淬炼刀刃**<br>[`RefineBlade`](../../../../../sts2/src/Core/Models/Cards/RefineBlade.cs) | 技能 / 普通<br>1→1 能量 | 铸造9。<br>在下个回合获得1点能量。 | 铸造13。<br>在下个回合获得1点能量。 | 未声明 | 铸造/君主之剑 |
| 67. **倒映**<br>[`Reflect`](../../../../../sts2/src/Core/Models/Cards/Reflect.cs) | 技能 / 罕见<br>1→1 能量<br>3→3 ★ | 获得15点格挡。<br>在本回合将你格挡掉的攻击伤害反弹给攻击者。 | 获得20点格挡。<br>在本回合将你格挡掉的攻击伤害反弹给攻击者。 | 未声明 | 星星资源/星星费用 |
| 68. **共鸣**<br>[`Resonance`](../../../../../sts2/src/Core/Models/Cards/Resonance.cs) | 技能 / 罕见<br>1→1 能量<br>3→3 ★ | 获得1点力量。所有敌人失去1点力量。 | 获得2点力量。所有敌人失去1点力量。 | 未声明 | 星星资源/星星费用 |
| 69. **胜券在王**<br>[`RoyalGamble`](../../../../../sts2/src/Core/Models/Cards/RoyalGamble.cs) | 技能 / 罕见<br>0→0 能量<br>5→5 ★ | 获得9★。<br>关键字：消耗。 | 获得9★。<br>关键字：消耗、保留。 | 未声明 | 星星资源/星星费用 |
| 70. **王国资产**<br>[`Royalties`](../../../../../sts2/src/Core/Models/Cards/Royalties.cs) | 能力 / 稀有<br>1→1 能量 | 在战斗结束时，获得30金币。 | 在战斗结束时，获得40金币。 | 未声明 | — |
| 71. **追踪之刃**<br>[`SeekingEdge`](../../../../../sts2/src/Core/Models/Cards/SeekingEdge.cs) | 能力 / 稀有<br>1→1 能量 | 铸造7。<br>君王之剑现在会对所有敌人造成伤害。 | 铸造11。<br>君王之剑现在会对所有敌人造成伤害。 | 未声明 | 铸造/君主之剑 |
| 72. **七星**<br>[`SevenStars`](../../../../../sts2/src/Core/Models/Cards/SevenStars.cs) | 攻击 / 稀有<br>2→1 能量<br>7→7 ★ | 对所有敌人造成7点伤害7次。 | 对所有敌人造成7点伤害7次。 | 未声明 | 星星资源/星星费用 |
| 73. **明耀打击**<br>[`ShiningStrike`](../../../../../sts2/src/Core/Models/Cards/ShiningStrike.cs) | 攻击 / 罕见<br>1→1 能量 | 造成8点伤害。<br>获得★★。<br>将这张牌放置于你的抽牌堆顶部。 | 造成11点伤害。<br>获得★★。<br>将这张牌放置于你的抽牌堆顶部。 | 未声明 | 星星资源/星星费用 |
| 74. **太阳打击**<br>[`SolarStrike`](../../../../../sts2/src/Core/Models/Cards/SolarStrike.cs) | 攻击 / 普通<br>1→1 能量 | 造成9点伤害。<br>获得★。 | 造成10点伤害。<br>获得★★。 | 未声明 | 星星资源/星星费用 |
| 75. **光谱偏移**<br>[`SpectrumShift`](../../../../../sts2/src/Core/Models/Cards/SpectrumShift.cs) | 能力 / 罕见<br>2→1 能量 | 在你的回合开始时，将1张随机无色牌添加到你的手牌中。 | 在你的回合开始时，将1张随机无色牌添加到你的手牌中。 | 未声明 | — |
| 76. **战利品**<br>[`SpoilsOfBattle`](../../../../../sts2/src/Core/Models/Cards/SpoilsOfBattle.cs) | 技能 / 普通<br>1→1 能量 | 铸造5。<br>抽2张牌。 | 铸造8。<br>抽2张牌。 | 未声明 | 铸造/君主之剑 |
| 77. **星尘**<br>[`Stardust`](../../../../../sts2/src/Core/Models/Cards/Stardust.cs) | 攻击 / 罕见<br>0→0 能量<br>X→X ★ | 随机对敌人造成5点伤害X次。 | 随机对敌人造成7点伤害X次。 | 未声明 | 星星资源/星星费用 |
| 78. **打击**<br>[`StrikeRegent`](../../../../../sts2/src/Core/Models/Cards/StrikeRegent.cs) | 攻击 / 基础<br>1→1 能量 | 造成6点伤害。 | 造成9点伤害。 | 已注册；有本地行为证据<br>[base:4](../../../../../tools/base-migration-baseline/BaseSpire/Cards/Content/regent-cards.spire)<br>[本地证据:209](../../../../../.audit/owner-shared-run-20261012/slice-final.log) | — |
| 79. **征召上前**<br>[`SummonForth`](../../../../../sts2/src/Core/Models/Cards/SummonForth.cs) | 技能 / 罕见<br>1→1 能量 | 不论何处，将君王之剑放入你的手牌。<br>铸造8。 | 不论何处，将君王之剑放入你的手牌。<br>铸造11。 | 未声明 | 铸造/君主之剑 |
| 80. **超质量体**<br>[`Supermassive`](../../../../../sts2/src/Core/Models/Cards/Supermassive.cs) | 攻击 / 罕见<br>1→1 能量 | 造成【动态伤害】点伤害。<br>你在本场战斗中每生成过一张牌，这张牌就额外造成3点伤害。 | 造成【动态伤害】点伤害。<br>你在本场战斗中每生成过一张牌，这张牌就额外造成4点伤害。 | 未声明 | —<br>数值说明：基础伤害5→5；实际值随描述中的条件变化 |
| 81. **剑圣**<br>[`SwordSage`](../../../../../sts2/src/Core/Models/Cards/SwordSage.cs) | 能力 / 稀有<br>2→1 能量 | 君王之剑获得重放1。 | 君王之剑获得重放1。 | 未声明 | 铸造/君主之剑、牌实例持久修改/重放 |
| 82. **地形改造**<br>[`Terraforming`](../../../../../sts2/src/Core/Models/Cards/Terraforming.cs) | 技能 / 罕见<br>1→1 能量 | 获得6点活力。 | 获得8点活力。 | 未声明 | — |
| 83. **封印王座**<br>[`TheSealedThrone`](../../../../../sts2/src/Core/Models/Cards/TheSealedThrone.cs) | 能力 / 先古<br>1→0 能量<br>3→3 ★ | 你每打出一张牌，获得★。 | 你每打出一张牌，获得★。 | 未声明 | 星星资源/星星费用 |
| 84. **铸剑者**<br>[`TheSmith`](../../../../../sts2/src/Core/Models/Cards/TheSmith.cs) | 技能 / 稀有<br>1→1 能量<br>4→4 ★ | 铸造30。 | 铸造40。 | 未声明 | 星星资源/星星费用、铸造/君主之剑 |
| 85. **暴政**<br>[`Tyranny`](../../../../../sts2/src/Core/Models/Cards/Tyranny.cs) | 能力 / 稀有<br>1→1 能量 | 在你的回合开始时，抽一张牌，并从你的手牌中消耗1张牌。 | 在你的回合开始时，抽一张牌，并从你的手牌中消耗1张牌。<br>关键字：固有。 | 未声明 | — |
| 86. **崇拜**<br>[`Venerate`](../../../../../sts2/src/Core/Models/Cards/Venerate.cs) | 技能 / 基础<br>1→1 能量 | 获得★★。 | 获得★★★。 | 未声明 | 星星资源/星星费用 |
| 87. **虚空形态**<br>[`VoidForm`](../../../../../sts2/src/Core/Models/Cards/VoidForm.cs) | 能力 / 稀有<br>3→3 能量 | 结束你的回合。<br>你可以免费打出每回合的前2张牌。<br>关键字：虚无。 | 结束你的回合。<br>你可以免费打出每回合的前2张牌。 | 未声明 | — |
| 88. **战火铸就**<br>[`WroughtInWar`](../../../../../sts2/src/Core/Models/Cards/WroughtInWar.cs) | 攻击 / 普通<br>1→1 能量 | 造成7点伤害。<br>铸造7。 | 造成9点伤害。<br>铸造9。 | 未声明 | 铸造/君主之剑 |

“—”仅表示没有命中本次专属机制检索标签，不能据此认定该牌只需要简单原语。源标识链接指向原版卡类；base 链接中的数字是声明行，本地证据数字是对应日志/检查源码行。
