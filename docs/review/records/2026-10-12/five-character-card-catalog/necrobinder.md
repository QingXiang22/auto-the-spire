# 亡灵契约师卡牌校对清单（2026-10-12）

原版 [NecrobinderCardPool](../../../../../sts2/src/Core/Models/CardPools/NecrobinderCardPool.cs) 共 **88** 张。亡灵契约师对应用户提到的“亡灵召唤师”。

[返回清单总览](README.md)。这是源快照与实现证据的日期记录；列含义、符号及未验证范围见总览。普通/升级描述来自同一条中文本地化模板，数值只取原版源码；不会根据 base 的实现反推描述。

| # / 卡牌 / 原版标识 | 类型、稀有度与费用（普通→升级） | 原版普通描述 | 原版升级描述 | base 实现证据 | 专属/共享机制线索 |
| --- | --- | --- | --- | --- | --- |
| 1. **来生**<br>[`Afterlife`](../../../../../sts2/src/Core/Models/Cards/Afterlife.cs) | 技能 / 普通<br>1→1 能量 | 召唤6。<br>关键字：消耗。 | 召唤9。<br>关键字：消耗。 | 未声明 | 奥斯提/召唤/独立生物 |
| 2. **女妖之嚎**<br>[`BansheesCry`](../../../../../sts2/src/Core/Models/Cards/BansheesCry.cs) | 攻击 / 稀有<br>9→7 能量 | 对所有敌人造成33点伤害。<br>本场战斗中每打出过一张虚无牌，此牌的耗能就减少2点能量。 | 对所有敌人造成33点伤害。<br>本场战斗中每打出过一张虚无牌，此牌的耗能就减少2点能量。 | 未声明 | — |
| 3. **荒疫打击**<br>[`BlightStrike`](../../../../../sts2/src/Core/Models/Cards/BlightStrike.cs) | 攻击 / 普通<br>1→1 能量 | 造成8点伤害。<br>给予等量于所造成伤害的灾厄。 | 造成10点伤害。<br>给予等量于所造成伤害的灾厄。 | 未声明 | 灾厄结算 |
| 4. **护卫**<br>[`Bodyguard`](../../../../../sts2/src/Core/Models/Cards/Bodyguard.cs) | 技能 / 基础<br>1→1 能量 | 召唤5。 | 召唤7。 | 未声明 | 奥斯提/召唤/独立生物 |
| 5. **碎骨**<br>[`BoneShards`](../../../../../sts2/src/Core/Models/Cards/BoneShards.cs) | 攻击 / 罕见<br>1→1 能量 | 如果奥斯提存活，则他对所有敌人造成9点伤害并且你获得9点格挡。<br>然后奥斯提死去。 | 如果奥斯提存活，则他对所有敌人造成12点伤害并且你获得12点格挡。<br>然后奥斯提死去。 | 未声明 | 奥斯提/召唤/独立生物 |
| 6. **预借时间**<br>[`BorrowedTime`](../../../../../sts2/src/Core/Models/Cards/BorrowedTime.cs) | 技能 / 罕见<br>1→1 能量 | 获得4点能量。<br>所有卡牌在本回合耗能增加1点能量。 | 获得6点能量。<br>所有卡牌在本回合耗能增加1点能量。 | 未声明 | — |
| 7. **埋葬**<br>[`Bury`](../../../../../sts2/src/Core/Models/Cards/Bury.cs) | 攻击 / 罕见<br>4→4 能量 | 造成52点伤害。 | 造成63点伤害。 | 已注册；有本地行为证据<br>[base:78](../../../../../mod-the-spire/base-spire/BaseSpire/ContentSource/necrobinder-cards.spire)<br>[本地证据:293](../../../../../.audit/owner-shared-run-20261012/slice-final.log) | — |
| 8. **钙化**<br>[`Calcify`](../../../../../sts2/src/Core/Models/Cards/Calcify.cs) | 能力 / 罕见<br>1→1 能量 | 奥斯提的攻击额外造成4点伤害。 | 奥斯提的攻击额外造成6点伤害。 | 未声明 | — |
| 9. **虚空之唤**<br>[`CallOfTheVoid`](../../../../../sts2/src/Core/Models/Cards/CallOfTheVoid.cs) | 能力 / 稀有<br>1→1 能量 | 在你的回合开始时，将1张随机牌添加到你的手牌中。添加的牌会获得虚无。 | 在你的回合开始时，将1张随机牌添加到你的手牌中。添加的牌会获得虚无。<br>关键字：固有。 | 未声明 | — |
| 10. **捕捉灵魂**<br>[`CaptureSpirit`](../../../../../sts2/src/Core/Models/Cards/CaptureSpirit.cs) | 技能 / 罕见<br>1→1 能量 | 敌人失去3点生命。<br>将3张灵魂加入你的抽牌堆。 | 敌人失去4点生命。<br>将4张灵魂加入你的抽牌堆。 | 未声明 | — |
| 11. **洁净**<br>[`Cleanse`](../../../../../sts2/src/Core/Models/Cards/Cleanse.cs) | 技能 / 罕见<br>1→1 能量 | 召唤3。<br>从你的抽牌堆中选择一张牌将其消耗。 | 召唤5。<br>从你的抽牌堆中选择一张牌将其消耗。 | 未声明 | 奥斯提/召唤/独立生物 |
| 12. **倒数计时**<br>[`Countdown`](../../../../../sts2/src/Core/Models/Cards/Countdown.cs) | 能力 / 罕见<br>1→1 能量 | 在你的回合开始时，给予随机敌人6层灾厄。 | 在你的回合开始时，给予随机敌人9层灾厄。 | 未声明 | 灾厄结算 |
| 13. **死亡之舞**<br>[`DanseMacabre`](../../../../../sts2/src/Core/Models/Cards/DanseMacabre.cs) | 能力 / 罕见<br>1→1 能量 | 每当你打出一张耗能大于等于2点能量的牌时，获得4点格挡。 | 每当你打出一张耗能大于等于2点能量的牌时，获得6点格挡。 | 未声明 | — |
| 14. **死亡行军**<br>[`DeathMarch`](../../../../../sts2/src/Core/Models/Cards/DeathMarch.cs) | 攻击 / 罕见<br>1→1 能量 | 造成【动态伤害】点伤害。<br>你在回合进行中每抽到一张牌，都会使其额外造成4点伤害。 | 造成【动态伤害】点伤害。<br>你在回合进行中每抽到一张牌，都会使其额外造成6点伤害。 | 未声明 | —<br>数值说明：基础伤害8→9；实际值随描述中的条件变化 |
| 15. **死亡使者**<br>[`Deathbringer`](../../../../../sts2/src/Core/Models/Cards/Deathbringer.cs) | 技能 / 罕见<br>2→2 能量 | 给予所有敌人21层灾厄和1层虚弱。 | 给予所有敌人26层灾厄和1层虚弱。 | 未声明 | 灾厄结算 |
| 16. **死亡之门**<br>[`DeathsDoor`](../../../../../sts2/src/Core/Models/Cards/DeathsDoor.cs) | 技能 / 罕见<br>1→1 能量 | 获得6点格挡。<br>如果你在本回合中曾给予过灾厄，则额外获得2次格挡。 | 获得7点格挡。<br>如果你在本回合中曾给予过灾厄，则额外获得2次格挡。 | 未声明 | 灾厄结算 |
| 17. **摧残**<br>[`Debilitate`](../../../../../sts2/src/Core/Models/Cards/Debilitate.cs) | 攻击 / 罕见<br>1→1 能量 | 造成10点伤害。<br>在接下来的2回合内，该敌人身上的易伤与虚弱效果翻倍。 | 造成12点伤害。<br>在接下来的3回合内，该敌人身上的易伤与虚弱效果翻倍。 | 未声明 | — |
| 18. **防御**<br>[`DefendNecrobinder`](../../../../../sts2/src/Core/Models/Cards/DefendNecrobinder.cs) | 技能 / 基础<br>1→1 能量 | 获得5点格挡。 | 获得8点格挡。 | 已注册；有本地行为证据<br>[base:34](../../../../../mod-the-spire/base-spire/BaseSpire/ContentSource/necrobinder-cards.spire)<br>[本地证据:206](../../../../../.audit/owner-shared-run-20261012/slice-final.log) | — |
| 19. **玷污**<br>[`Defile`](../../../../../sts2/src/Core/Models/Cards/Defile.cs) | 攻击 / 普通<br>1→1 能量 | 造成13点伤害。<br>关键字：虚无。 | 造成17点伤害。<br>关键字：虚无。 | 已注册；有本地行为证据<br>[base:86](../../../../../mod-the-spire/base-spire/BaseSpire/ContentSource/necrobinder-cards.spire)<br>[本地证据:278](../../../../../.audit/owner-shared-run-20261012/slice-final.log) | — |
| 20. **违逆**<br>[`Defy`](../../../../../sts2/src/Core/Models/Cards/Defy.cs) | 技能 / 普通<br>1→1 能量 | 获得6点格挡。<br>给予1层虚弱。<br>关键字：虚无。 | 获得9点格挡。<br>给予1层虚弱。<br>关键字：虚无。 | 已注册；有本地行为证据<br>[base:63](../../../../../mod-the-spire/base-spire/BaseSpire/ContentSource/necrobinder-cards.spire)<br>[本地证据:269](../../../../../.audit/owner-shared-run-20261012/slice-final.log) | — |
| 21. **拖延**<br>[`Delay`](../../../../../sts2/src/Core/Models/Cards/Delay.cs) | 技能 / 罕见<br>2→2 能量 | 获得11点格挡。<br>在下个回合，<br>获得1点能量。 | 获得13点格挡。<br>在下个回合，<br>获得2点能量。 | 未声明 | — |
| 22. **领域**<br>[`Demesne`](../../../../../sts2/src/Core/Models/Cards/Demesne.cs) | 能力 / 稀有<br>3→2 能量 | 在你的回合开始时，获得1点能量并额外多抽1张牌。<br>关键字：虚无。 | 在你的回合开始时，获得1点能量并额外多抽1张牌。<br>关键字：虚无。 | 未声明 | — |
| 23. **吞噬生命**<br>[`DevourLife`](../../../../../sts2/src/Core/Models/Cards/DevourLife.cs) | 能力 / 稀有<br>1→1 能量 | 每当你打出一张灵魂时，召唤1。 | 每当你打出一张灵魂时，召唤2。 | 未声明 | 奥斯提/召唤/独立生物 |
| 24. **挽歌**<br>[`Dirge`](../../../../../sts2/src/Core/Models/Cards/Dirge.cs) | 技能 / 罕见<br>X→X 能量 | 召唤3X次。<br>将X张灵魂添加到你的抽牌堆中。<br>关键字：消耗。 | 召唤4X次。<br>将X张灵魂+添加到你的抽牌堆中。<br>关键字：消耗。 | 未声明 | 奥斯提/召唤/独立生物 |
| 25. **能量汲取**<br>[`DrainPower`](../../../../../sts2/src/Core/Models/Cards/DrainPower.cs) | 攻击 / 普通<br>1→1 能量 | 造成10点伤害。<br>随机升级你弃牌堆中的2张牌。 | 造成12点伤害。<br>随机升级你弃牌堆中的3张牌。 | 未声明 | — |
| 26. **清淤**<br>[`Dredge`](../../../../../sts2/src/Core/Models/Cards/Dredge.cs) | 技能 / 罕见<br>1→1 能量 | 将3张牌从弃牌堆加入你的手牌。<br>关键字：消耗。 | 将3张牌从弃牌堆加入你的手牌。<br>关键字：消耗、保留。 | 未声明 | — |
| 27. **幻景**<br>[`Eidolon`](../../../../../sts2/src/Core/Models/Cards/Eidolon.cs) | 技能 / 稀有<br>2→1 能量 | 消耗所有手牌。<br>如果有9张牌被消耗，则获得1层无实体。 | 消耗所有手牌。<br>如果有9张牌被消耗，则获得1层无实体。 | 未声明 | — |
| 28. **末日降临**<br>[`EndOfDays`](../../../../../sts2/src/Core/Models/Cards/EndOfDays.cs) | 技能 / 稀有<br>3→3 能量 | 给予所有敌人29层灾厄。<br>杀死所有灾厄大于等于当前生命值的敌人。 | 给予所有敌人37层灾厄。<br>杀死所有灾厄大于等于当前生命值的敌人。 | 未声明 | 灾厄结算 |
| 29. **弱化之触**<br>[`EnfeeblingTouch`](../../../../../sts2/src/Core/Models/Cards/EnfeeblingTouch.cs) | 技能 / 罕见<br>1→1 能量 | 让一名敌人在本回合内失去8点力量。<br>关键字：虚无。 | 让一名敌人在本回合内失去11点力量。<br>关键字：虚无。 | 未声明 | — |
| 30. **根除**<br>[`Eradicate`](../../../../../sts2/src/Core/Models/Cards/Eradicate.cs) | 攻击 / 稀有<br>X→X 能量 | 造成11点伤害X次。<br>关键字：保留。 | 造成14点伤害X次。<br>关键字：保留。 | 已注册；有本地行为证据<br>[base:4](../../../../../mod-the-spire/base-spire/BaseSpire/ContentSource/necrobinder-cards.spire)<br>[本地证据:139](../../../../../.audit/owner-shared-run-20261012/slice-final.log) | — |
| 31. **恐惧**<br>[`Fear`](../../../../../sts2/src/Core/Models/Cards/Fear.cs) | 攻击 / 普通<br>1→1 能量 | 造成7点伤害。<br>给予1层易伤。<br>关键字：虚无。 | 造成8点伤害。<br>给予2层易伤。<br>关键字：虚无。 | 已注册；有本地行为证据<br>[base:11](../../../../../mod-the-spire/base-spire/BaseSpire/ContentSource/necrobinder-cards.spire)<br>[本地证据:136](../../../../../.audit/owner-shared-run-20261012/slice-final.log) | — |
| 32. **取回**<br>[`Fetch`](../../../../../sts2/src/Core/Models/Cards/Fetch.cs) | 攻击 / 罕见<br>0→0 能量 | 奥斯提造成3点伤害。<br>如果这是这张牌第一次在本回合被打出，则抽1张牌。 | 奥斯提造成6点伤害。<br>如果这是这张牌第一次在本回合被打出，则抽1张牌。 | 未声明 | 奥斯提/召唤/独立生物 |
| 33. **重压**<br>[`Flatten`](../../../../../sts2/src/Core/Models/Cards/Flatten.cs) | 攻击 / 普通<br>2→2 能量 | 奥斯提造成12点伤害。<br>如果奥斯提本回合攻击过，则这张牌的耗能变为0[能量]。 | 奥斯提造成16点伤害。<br>如果奥斯提本回合攻击过，则这张牌的耗能变为0[能量]。 | 未声明 | 奥斯提/召唤/独立生物 |
| 34. **禁忌魔典**<br>[`ForbiddenGrimoire`](../../../../../sts2/src/Core/Models/Cards/ForbiddenGrimoire.cs) | 能力 / 先古<br>2→1 能量 | 在战斗结束时，你可以从你的牌组中选一张牌移除。<br>关键字：永恒。 | 在战斗结束时，你可以从你的牌组中选一张牌移除。<br>关键字：永恒。 | 未声明 | — |
| 35. **友谊**<br>[`Friendship`](../../../../../sts2/src/Core/Models/Cards/Friendship.cs) | 能力 / 罕见<br>1→1 能量 | 失去2点力量。<br>在每个回合开始时获得1点能量。 | 失去1点力量。<br>在每个回合开始时获得1点能量。 | 未声明 | — |
| 36. **彼岸一瞥**<br>[`GlimpseBeyond`](../../../../../sts2/src/Core/Models/Cards/GlimpseBeyond.cs)<br>**仅多人** | 技能 / 稀有<br>1→1 能量 | 在所有玩家的抽牌堆中加入3张灵魂。<br>关键字：消耗。 | 在所有玩家的抽牌堆中加入4张灵魂。<br>关键字：消耗。 | 未声明 | 跨玩家效果 |
| 37. **守墓人**<br>[`GraveWarden`](../../../../../sts2/src/Core/Models/Cards/GraveWarden.cs) | 技能 / 普通<br>1→1 能量 | 获得8点格挡。<br>将一张灵魂放入你的抽牌堆中。 | 获得11点格挡。<br>将一张灵魂放入你的抽牌堆中。 | 未声明 | — |
| 38. **坟冢爆射**<br>[`Graveblast`](../../../../../sts2/src/Core/Models/Cards/Graveblast.cs) | 攻击 / 普通<br>1→1 能量 | 造成4点伤害。<br>将弃牌堆中的一张牌放入你的手牌。<br>关键字：消耗。 | 造成6点伤害。<br>将弃牌堆中的一张牌放入你的手牌。 | 已注册；有本地行为证据<br>[base:49](../../../../../mod-the-spire/base-spire/BaseSpire/ContentSource/ready-cards.spire)<br>[本地证据:94](../../../../../.audit/owner-shared-run-20261012/slice-final.log) | — |
| 39. **吊杀**<br>[`Hang`](../../../../../sts2/src/Core/Models/Cards/Hang.cs) | 攻击 / 稀有<br>1→1 能量 | 造成10点伤害。<br>让所有“吊杀”牌对这名敌人造成的伤害翻倍。 | 造成13点伤害。<br>让所有“吊杀”牌对这名敌人造成的伤害翻倍。 | 未声明 | — |
| 40. **纠缠**<br>[`Haunt`](../../../../../sts2/src/Core/Models/Cards/Haunt.cs) | 能力 / 罕见<br>1→1 能量 | 每当你打出一张灵魂时，随机一名敌人失去6点生命。 | 每当你打出一张灵魂时，随机一名敌人失去8点生命。 | 未声明 | — |
| 41. **击掌**<br>[`HighFive`](../../../../../sts2/src/Core/Models/Cards/HighFive.cs) | 攻击 / 罕见<br>2→2 能量 | 奥斯提对所有敌人<br>造成11点伤害并给予它们<br>2层易伤。 | 奥斯提对所有敌人<br>造成13点伤害并给予它们<br>3层易伤。 | 未声明 | 奥斯提/召唤/独立生物 |
| 42. **唤起**<br>[`Invoke`](../../../../../sts2/src/Core/Models/Cards/Invoke.cs) | 技能 / 普通<br>1→1 能量 | 在下个回合召唤2并获得2点能量。 | 在下个回合召唤3并获得3点能量。 | 未声明 | 奥斯提/召唤/独立生物 |
| 43. **骸骨军团**<br>[`LegionOfBone`](../../../../../sts2/src/Core/Models/Cards/LegionOfBone.cs)<br>**仅多人** | 技能 / 罕见<br>2→2 能量 | 所有玩家召唤6。<br>关键字：消耗。 | 所有玩家召唤8。<br>关键字：消耗。 | 未声明 | 奥斯提/召唤/独立生物、跨玩家效果 |
| 44. **致死性**<br>[`Lethality`](../../../../../sts2/src/Core/Models/Cards/Lethality.cs) | 能力 / 罕见<br>1→1 能量 | 每回合的第一张攻击牌会造成50%额外伤害。<br>关键字：虚无。 | 每回合的第一张攻击牌会造成75%额外伤害。<br>关键字：虚无。 | 未声明 | — |
| 45. **忧郁**<br>[`Melancholy`](../../../../../sts2/src/Core/Models/Cards/Melancholy.cs) | 技能 / 罕见<br>3→3 能量 | 获得13点格挡。<br>每当有任何生物死亡时，这张牌的耗能减少1点能量。 | 获得17点格挡。<br>每当有任何生物死亡时，这张牌的耗能减少1点能量。 | 未声明 | — |
| 46. **苦难**<br>[`Misery`](../../../../../sts2/src/Core/Models/Cards/Misery.cs) | 攻击 / 稀有<br>0→0 能量 | 造成7点伤害。<br>给予其他敌人该名敌人身上的所有负面效果。 | 造成9点伤害。<br>给予其他敌人该名敌人身上的所有负面效果。<br>关键字：保留。 | 未声明 | — |
| 47. **亡灵精通**<br>[`NecroMastery`](../../../../../sts2/src/Core/Models/Cards/NecroMastery.cs) | 能力 / 稀有<br>2→2 能量 | 召唤5。<br>每当奥斯提失去生命值时，<br>所有敌人失去等量生命值。 | 召唤8。<br>每当奥斯提失去生命值时，<br>所有敌人失去等量生命值。 | 未声明 | 奥斯提/召唤/独立生物 |
| 48. **负能量脉冲**<br>[`NegativePulse`](../../../../../sts2/src/Core/Models/Cards/NegativePulse.cs) | 技能 / 普通<br>1→1 能量 | 获得5点格挡。<br>给予所有敌人7层灾厄。 | 获得6点格挡。<br>给予所有敌人11层灾厄。 | 未声明 | 灾厄结算 |
| 49. **精神过载**<br>[`Neurosurge`](../../../../../sts2/src/Core/Models/Cards/Neurosurge.cs) | 能力 / 稀有<br>0→0 能量 | 获得3点能量。<br>抽2张牌。<br>在你的回合开始时，给予自身3层灾厄。 | 获得4点能量。<br>抽2张牌。<br>在你的回合开始时，给予自身3层灾厄。 | 未声明 | 灾厄结算 |
| 50. **无处可逃**<br>[`NoEscape`](../../../../../sts2/src/Core/Models/Cards/NoEscape.cs) | 技能 / 罕见<br>1→1 能量 | 给予10层灾厄，敌人身上每有10层灾厄，则额外给予这名敌人5层灾厄。 | 给予15层灾厄，敌人身上每有10层灾厄，则额外给予这名敌人5层灾厄。 | 未声明 | 灾厄结算 |
| 51. **湮灭**<br>[`Oblivion`](../../../../../sts2/src/Core/Models/Cards/Oblivion.cs) | 技能 / 稀有<br>0→0 能量 | 打出此牌后，你在本回合内每打出一张牌，就给予该敌人3层灾厄。 | 打出此牌后，你在本回合内每打出一张牌，就给予该敌人4层灾厄。 | 未声明 | 灾厄结算 |
| 52. **书页风暴**<br>[`Pagestorm`](../../../../../sts2/src/Core/Models/Cards/Pagestorm.cs) | 能力 / 罕见<br>1→0 能量 | 每当你抽到一张虚无牌时, 抽1张牌。 | 每当你抽到一张虚无牌时, 抽1张牌。 | 未声明 | — |
| 53. **领会**<br>[`Parse`](../../../../../sts2/src/Core/Models/Cards/Parse.cs) | 技能 / 罕见<br>1→1 能量 | 抽3张牌。<br>关键字：虚无。 | 抽4张牌。<br>关键字：虚无。 | 已注册；有本地行为证据<br>[base:49](../../../../../mod-the-spire/base-spire/BaseSpire/ContentSource/necrobinder-cards.spire)<br>[本地证据:218](../../../../../.audit/owner-shared-run-20261012/slice-final.log) | — |
| 54. **戳击**<br>[`Poke`](../../../../../sts2/src/Core/Models/Cards/Poke.cs) | 攻击 / 普通<br>0→0 能量 | 奥斯提造成6点伤害。 | 奥斯提造成9点伤害。 | 未声明 | 奥斯提/召唤/独立生物 |
| 55. **守护者**<br>[`Protector`](../../../../../sts2/src/Core/Models/Cards/Protector.cs) | 攻击 / 先古<br>1→0 能量 | 奥斯提造成【动态伤害】点伤害。<br>额外造成等量于奥斯提最大生命值的伤害。 | 奥斯提造成【动态伤害】点伤害。<br>额外造成等量于奥斯提最大生命值的伤害。 | 未声明 | 奥斯提/召唤/独立生物<br>数值说明：基础伤害10→15；实际值随描述中的条件变化 |
| 56. **吸引仇恨**<br>[`PullAggro`](../../../../../sts2/src/Core/Models/Cards/PullAggro.cs) | 技能 / 普通<br>2→2 能量 | 召唤4。<br>获得7点格挡。 | 召唤5。<br>获得9点格挡。 | 未声明 | 奥斯提/召唤/独立生物 |
| 57. **亡魂牵引**<br>[`PullFromBelow`](../../../../../sts2/src/Core/Models/Cards/PullFromBelow.cs) | 攻击 / 罕见<br>1→1 能量 | 本场战斗中每打出过一张虚无牌，此牌就造成5点伤害一次。 | 本场战斗中每打出过一张虚无牌，此牌就造成7点伤害一次。 | 未声明 | — |
| 58. **腐败**<br>[`Putrefy`](../../../../../sts2/src/Core/Models/Cards/Putrefy.cs) | 技能 / 罕见<br>1→1 能量 | 给予2层虚弱。<br>给予2层易伤。<br>关键字：消耗。 | 给予3层虚弱。<br>给予3层易伤。<br>关键字：消耗。 | 已注册；有本地行为证据<br>[base:70](../../../../../mod-the-spire/base-spire/BaseSpire/ContentSource/necrobinder-cards.spire)<br>[本地证据:272](../../../../../.audit/owner-shared-run-20261012/slice-final.log) | — |
| 59. **猛晃**<br>[`Rattle`](../../../../../sts2/src/Core/Models/Cards/Rattle.cs) | 攻击 / 罕见<br>1→1 能量 | 奥斯提造成7点伤害。<br>本回合每使用过一次奥斯提攻击牌，此牌就额外造成一次伤害。 | 奥斯提造成9点伤害。<br>本回合每使用过一次奥斯提攻击牌，此牌就额外造成一次伤害。 | 未声明 | 奥斯提/召唤/独立生物 |
| 60. **死者苏生**<br>[`Reanimate`](../../../../../sts2/src/Core/Models/Cards/Reanimate.cs) | 技能 / 稀有<br>3→3 能量 | 召唤20。<br>关键字：消耗。 | 召唤25。<br>关键字：消耗。 | 未声明 | 奥斯提/召唤/独立生物 |
| 61. **收割**<br>[`Reap`](../../../../../sts2/src/Core/Models/Cards/Reap.cs) | 攻击 / 普通<br>3→3 能量 | 造成27点伤害。<br>关键字：保留。 | 造成33点伤害。<br>关键字：保留。 | 已注册；有本地行为证据<br>[base:85](../../../../../mod-the-spire/base-spire/BaseSpire/ContentSource/ready-cards.spire)<br>[本地证据:109](../../../../../.audit/owner-shared-run-20261012/slice-final.log) | — |
| 62. **死神形态**<br>[`ReaperForm`](../../../../../sts2/src/Core/Models/Cards/ReaperForm.cs) | 能力 / 稀有<br>3→3 能量 | 每当你的攻击造成伤害时，同时给予等量的灾厄。 | 每当你的攻击造成伤害时，同时给予等量的灾厄。<br>关键字：保留。 | 未声明 | 灾厄结算 |
| 63. **剥夺**<br>[`Reave`](../../../../../sts2/src/Core/Models/Cards/Reave.cs) | 攻击 / 普通<br>1→1 能量 | 造成9点伤害。<br>将一张灵魂加入你的抽牌堆。 | 造成11点伤害。<br>将一张灵魂+加入你的抽牌堆。 | 未声明 | — |
| 64. **得力助手**<br>[`RightHandHand`](../../../../../sts2/src/Core/Models/Cards/RightHandHand.cs) | 攻击 / 罕见<br>0→0 能量 | 奥斯提造成4点伤害。<br>每当你打出耗能为2点能量或以上的牌，将此牌从弃牌堆放回你的手牌。 | 奥斯提造成6点伤害。<br>每当你打出耗能为2点能量或以上的牌，将此牌从弃牌堆放回你的手牌。 | 未声明 | 奥斯提/召唤/独立生物 |
| 65. **牺牲**<br>[`Sacrifice`](../../../../../sts2/src/Core/Models/Cards/Sacrifice.cs) | 技能 / 稀有<br>1→0 能量 | 如果奥斯提存活，则他死去，然后你获得等量于其双倍最大生命值的格挡。<br>关键字：保留。 | 如果奥斯提存活，则他死去，然后你获得等量于其双倍最大生命值的格挡。<br>关键字：保留。 | 未声明 | 奥斯提/召唤/独立生物 |
| 66. **鞭打**<br>[`Scourge`](../../../../../sts2/src/Core/Models/Cards/Scourge.cs) | 技能 / 普通<br>1→1 能量 | 给予13层灾厄。<br>抽1张牌。 | 给予16层灾厄。<br>抽2张牌。 | 未声明 | 灾厄结算 |
| 67. **雕琢打击**<br>[`SculptingStrike`](../../../../../sts2/src/Core/Models/Cards/SculptingStrike.cs) | 攻击 / 普通<br>1→1 能量 | 造成9点伤害。<br>为一张手牌添加虚无。 | 造成12点伤害。<br>为一张手牌添加虚无。 | 未声明 | — |
| 68. **降灵**<br>[`Seance`](../../../../../sts2/src/Core/Models/Cards/Seance.cs) | 技能 / 稀有<br>1→0 能量 | 将你抽牌堆中的一张牌变化为灵魂。<br>关键字：虚无。 | 将你抽牌堆中的一张牌变化为灵魂。<br>关键字：虚无。 | 未声明 | — |
| 69. **哨卫模式**<br>[`SentryMode`](../../../../../sts2/src/Core/Models/Cards/SentryMode.cs) | 能力 / 稀有<br>2→1 能量 | 在你的回合开始时，将1张扫荡凝视加入你的手牌。 | 在你的回合开始时，将1张扫荡凝视加入你的手牌。 | 未声明 | — |
| 70. **切断**<br>[`Severance`](../../../../../sts2/src/Core/Models/Cards/Severance.cs) | 攻击 / 罕见<br>2→2 能量 | 造成13点伤害。<br>将一张灵魂分别加入你的抽牌堆，手牌和弃牌堆中。 | 造成18点伤害。<br>将一张灵魂分别加入你的抽牌堆，手牌和弃牌堆中。 | 未声明 | — |
| 71. **命运同担**<br>[`SharedFate`](../../../../../sts2/src/Core/Models/Cards/SharedFate.cs) | 技能 / 稀有<br>0→0 能量 | 失去2点力量。<br>敌人失去2点力量。<br>关键字：消耗。 | 失去2点力量。<br>敌人失去3点力量。<br>关键字：消耗。 | 已注册；有本地行为证据<br>[base:18](../../../../../mod-the-spire/base-spire/BaseSpire/ContentSource/necrobinder-cards.spire)<br>[本地证据:296](../../../../../.audit/owner-shared-run-20261012/slice-final.log) | — |
| 72. **厄运之衣**<br>[`Shroud`](../../../../../sts2/src/Core/Models/Cards/Shroud.cs) | 能力 / 罕见<br>1→1 能量 | 每当你给予灾厄时，获得2点格挡。 | 每当你给予灾厄时，获得3点格挡。 | 未声明 | 灾厄结算 |
| 73. **紧追不放**<br>[`SicEm`](../../../../../sts2/src/Core/Models/Cards/SicEm.cs) | 攻击 / 罕见<br>1→1 能量 | 奥斯提造成5点伤害。<br>在本回合内，每当奥斯提攻击这名敌人时，召唤3。 | 奥斯提造成6点伤害。<br>在本回合内，每当奥斯提攻击这名敌人时，召唤4。 | 未声明 | 奥斯提/召唤/独立生物 |
| 74. **血肉戏法**<br>[`SleightOfFlesh`](../../../../../sts2/src/Core/Models/Cards/SleightOfFlesh.cs) | 能力 / 罕见<br>2→2 能量 | 每当你给予一个敌人负面状态时，使其受到9点伤害。 | 每当你给予一个敌人负面状态时，使其受到13点伤害。 | 未声明 | — |
| 75. **响指**<br>[`Snap`](../../../../../sts2/src/Core/Models/Cards/Snap.cs) | 攻击 / 普通<br>1→1 能量 | 奥斯提造成7点伤害。<br>给手牌中的一张牌添加保留。 | 奥斯提造成10点伤害。<br>给手牌中的一张牌添加保留。 | 未声明 | 奥斯提/召唤/独立生物 |
| 76. **灵魂风暴**<br>[`SoulStorm`](../../../../../sts2/src/Core/Models/Cards/SoulStorm.cs) | 攻击 / 稀有<br>1→1 能量 | 造成【动态伤害】点伤害。<br>你的消耗牌堆中每有一张灵魂，伤害增加2。 | 造成【动态伤害】点伤害。<br>你的消耗牌堆中每有一张灵魂，伤害增加3。 | 未声明 | —<br>数值说明：基础伤害9→9；实际值随描述中的条件变化 |
| 77. **播种**<br>[`Sow`](../../../../../sts2/src/Core/Models/Cards/Sow.cs) | 攻击 / 普通<br>1→1 能量 | 对所有敌人造成8点伤害。<br>关键字：保留。 | 对所有敌人造成11点伤害。<br>关键字：保留。 | 已注册；有本地行为证据<br>[base:42](../../../../../mod-the-spire/base-spire/BaseSpire/ContentSource/necrobinder-cards.spire)<br>[本地证据:215](../../../../../.audit/owner-shared-run-20261012/slice-final.log) | — |
| 78. **灰烬之灵**<br>[`SpiritOfAsh`](../../../../../sts2/src/Core/Models/Cards/SpiritOfAsh.cs) | 能力 / 稀有<br>1→1 能量 | 每当你打出一张虚无牌时，获得4点格挡。 | 每当你打出一张虚无牌时，获得5点格挡。 | 未声明 | — |
| 79. **增生**<br>[`Spur`](../../../../../sts2/src/Core/Models/Cards/Spur.cs) | 技能 / 罕见<br>1→1 能量 | 召唤3。<br>奥斯提回复5点生命。<br>关键字：保留。 | 召唤5。<br>奥斯提回复7点生命。<br>关键字：保留。 | 未声明 | 奥斯提/召唤/独立生物 |
| 80. **榨取**<br>[`Squeeze`](../../../../../sts2/src/Core/Models/Cards/Squeeze.cs) | 攻击 / 稀有<br>3→3 能量 | 奥斯提造成【动态伤害】点伤害。<br>你每有一张奥斯提的攻击牌，这张牌就额外造成5点伤害。 | 奥斯提造成【动态伤害】点伤害。<br>你每有一张奥斯提的攻击牌，这张牌就额外造成6点伤害。 | 未声明 | 奥斯提/召唤/独立生物<br>数值说明：基础伤害25→30；实际值随描述中的条件变化 |
| 81. **打击**<br>[`StrikeNecrobinder`](../../../../../sts2/src/Core/Models/Cards/StrikeNecrobinder.cs) | 攻击 / 基础<br>1→1 能量 | 造成6点伤害。 | 造成9点伤害。 | 已注册；有本地行为证据<br>[base:26](../../../../../mod-the-spire/base-spire/BaseSpire/ContentSource/necrobinder-cards.spire)<br>[本地证据:203](../../../../../.audit/owner-shared-run-20261012/slice-final.log) | — |
| 82. **巨镰**<br>[`TheScythe`](../../../../../sts2/src/Core/Models/Cards/TheScythe.cs) | 攻击 / 稀有<br>2→2 能量 | 造成13点伤害。<br>这张牌在本局游戏中的伤害永久性增加4。<br>关键字：消耗。 | 造成13点伤害。<br>这张牌在本局游戏中的伤害永久性增加5。<br>关键字：消耗。 | 未声明 | —<br>数值说明：显示新牌初始值；已成长的实例按保存值显示 |
| 83. **大限已至**<br>[`TimesUp`](../../../../../sts2/src/Core/Models/Cards/TimesUp.cs) | 攻击 / 稀有<br>2→2 能量 | 造成等量于该敌人身上的灾厄层数的伤害。<br>关键字：消耗。 | 造成等量于该敌人身上的灾厄层数的伤害。<br>关键字：消耗、保留。 | 未声明 | 灾厄结算 |
| 84. **重构**<br>[`Transfigure`](../../../../../sts2/src/Core/Models/Cards/Transfigure.cs) | 技能 / 稀有<br>1→1 能量 | 给一张手牌添加重放。<br>其耗能增加11点能量。<br>关键字：消耗。 | 给一张手牌添加重放。<br>其耗能增加11点能量。 | 未声明 | 牌实例持久修改/重放 |
| 85. **不死**<br>[`Undeath`](../../../../../sts2/src/Core/Models/Cards/Undeath.cs) | 技能 / 稀有<br>0→0 能量 | 获得7点格挡。<br>在弃牌堆放入一张此牌的复制品。 | 获得9点格挡。<br>在弃牌堆放入一张此牌的复制品。 | 未声明 | — |
| 86. **出击**<br>[`Unleash`](../../../../../sts2/src/Core/Models/Cards/Unleash.cs) | 攻击 / 基础<br>1→1 能量 | 奥斯提造成【动态伤害】点伤害。<br>这张牌额外造成等量于奥斯提当前生命值的伤害。 | 奥斯提造成【动态伤害】点伤害。<br>这张牌额外造成等量于奥斯提当前生命值的伤害。 | 未声明 | 奥斯提/召唤/独立生物<br>数值说明：基础伤害6→9；实际值随描述中的条件变化 |
| 87. **刺破帷幕**<br>[`Veilpiercer`](../../../../../sts2/src/Core/Models/Cards/Veilpiercer.cs) | 攻击 / 罕见<br>1→1 能量 | 造成10点伤害。<br>你打出的下一张虚无牌耗能变为0[能量]。 | 造成13点伤害。<br>你打出的下一张虚无牌耗能变为0[能量]。 | 未声明 | — |
| 88. **鬼火**<br>[`Wisp`](../../../../../sts2/src/Core/Models/Cards/Wisp.cs) | 技能 / 普通<br>0→0 能量 | 获得1点能量。<br>关键字：消耗。 | 获得1点能量。<br>关键字：消耗、保留。 | 已注册；有本地行为证据<br>[base:56](../../../../../mod-the-spire/base-spire/BaseSpire/ContentSource/necrobinder-cards.spire)<br>[本地证据:221](../../../../../.audit/owner-shared-run-20261012/slice-final.log) | — |

“—”仅表示没有命中本次专属机制检索标签，不能据此认定该牌只需要简单原语。源标识链接指向原版卡类；base 链接中的数字是声明行，本地证据数字是对应日志/检查源码行。
