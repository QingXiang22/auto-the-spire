# 衍生卡（共享 TokenCardPool）卡牌校对清单（2026-10-12）

原版 [TokenCardPool](../../../../../sts2/src/Core/Models/CardPools/TokenCardPool.cs) 共 **14** 张。

[返回清单总览](README.md)。这是源快照与实现证据的日期记录；列含义、符号及未验证范围见总览。普通/升级描述来自同一条中文本地化模板，数值只取原版源码；不会根据 base 的实现反推描述。

| # / 卡牌 / 原版标识 | 类型、稀有度与费用（普通→升级） | 原版普通描述 | 原版升级描述 | base 实现证据 | 专属/共享机制线索 |
| --- | --- | --- | --- | --- | --- |
| 1. **瓦解**<br>[`Disintegration`](../../../../../sts2/src/Core/Models/Cards/Disintegration.cs) | 状态 / 状态<br>不可打出→不可打出 能量<br>不可升级 | 在你的回合结束时，受到6点伤害。 | 不可升级；不适用 | 未声明 | — |
| 2. **燃料**<br>[`Fuel`](../../../../../sts2/src/Core/Models/Cards/Fuel.cs) | 技能 / 衍生<br>0→0 能量 | 获得1点能量。<br>抽1张牌。<br>关键字：消耗。 | 获得1点能量。<br>抽2张牌。<br>关键字：消耗。 | 已注册；有本地行为证据<br>[base:148](../../../../../mod-the-spire/base-spire/BaseSpire/ContentSource/colorless-cards.spire)<br>[本地证据:248](../../../../../.audit/owner-shared-run-20261012/slice-final.log) | — |
| 3. **巨石**<br>[`GiantRock`](../../../../../sts2/src/Core/Models/Cards/GiantRock.cs) | 攻击 / 衍生<br>1→1 能量 | 造成16点伤害。 | 造成20点伤害。 | 已注册；有本地行为证据<br>[base:658](../../../../../mod-the-spire/base-spire/BaseSpire/ContentSource/ironclad-cards.spire)<br>[本地证据:21](../../../../../mod-the-spire/base-spire/BaseSpire.Slice.Tests/SliceFormalSourceChecks.cs) | — |
| 4. **冷光**<br>[`Luminesce`](../../../../../sts2/src/Core/Models/Cards/Luminesce.cs) | 技能 / 衍生<br>0→0 能量 | 获得2点能量。<br>关键字：消耗、保留。 | 获得3点能量。<br>关键字：消耗、保留。 | 已注册；有本地行为证据<br>[base:172](../../../../../mod-the-spire/base-spire/BaseSpire/ContentSource/colorless-cards.spire)<br>[本地证据:251](../../../../../.audit/owner-shared-run-20261012/slice-final.log) | — |
| 5. **心灵腐化**<br>[`MindRot`](../../../../../sts2/src/Core/Models/Cards/MindRot.cs) | 状态 / 状态<br>不可打出→不可打出 能量<br>不可升级 | 每回合少抽1张牌。 | 不可升级；不适用 | 未声明 | — |
| 6. **仆从俯冲**<br>[`MinionDiveBomb`](../../../../../sts2/src/Core/Models/Cards/MinionDiveBomb.cs) | 攻击 / 衍生<br>0→0 能量 | 造成13点伤害。<br>关键字：消耗。 | 造成16点伤害。<br>关键字：消耗。 | 已注册；有本地行为证据<br>[base:137](../../../../../mod-the-spire/base-spire/BaseSpire/ContentSource/ready-cards.spire)<br>[本地证据:148](../../../../../.audit/owner-shared-run-20261012/slice-final.log) | 仆从衍生牌/变化 |
| 7. **仆从捐躯**<br>[`MinionSacrifice`](../../../../../sts2/src/Core/Models/Cards/MinionSacrifice.cs) | 技能 / 衍生<br>0→0 能量 | 获得8点格挡。<br>关键字：消耗。 | 获得11点格挡。<br>关键字：消耗。 | 已注册；有本地行为证据<br>[base:106](../../../../../mod-the-spire/base-spire/BaseSpire/ContentSource/ready-cards.spire)<br>[本地证据:118](../../../../../.audit/owner-shared-run-20261012/slice-final.log) | 仆从衍生牌/变化 |
| 8. **仆从打击**<br>[`MinionStrike`](../../../../../sts2/src/Core/Models/Cards/MinionStrike.cs) | 攻击 / 衍生<br>0→0 能量 | 造成6点伤害。<br>抽1张牌。<br>关键字：消耗。 | 造成9点伤害。<br>抽1张牌。<br>关键字：消耗。 | 已注册；有本地行为证据<br>[base:129](../../../../../mod-the-spire/base-spire/BaseSpire/ContentSource/ready-cards.spire)<br>[本地证据:145](../../../../../.audit/owner-shared-run-20261012/slice-final.log) | 仆从衍生牌/变化 |
| 9. **小刀**<br>[`Shiv`](../../../../../sts2/src/Core/Models/Cards/Shiv.cs) | 攻击 / 衍生<br>0→0 能量 | 造成4点伤害。<br>关键字：消耗。 | 造成6点伤害。<br>关键字：消耗。 | 未声明 | —<br>数值说明：显示无能力修正的新牌；FanOfKnives改为全体目标 |
| 10. **懒惰**<br>[`Sloth`](../../../../../sts2/src/Core/Models/Cards/Sloth.cs) | 状态 / 状态<br>不可打出→不可打出 能量<br>不可升级 | 你在每个回合不能打出超过3张牌。 | 不可升级；不适用 | 未声明 | — |
| 11. **灵魂**<br>[`Soul`](../../../../../sts2/src/Core/Models/Cards/Soul.cs) | 技能 / 衍生<br>0→0 能量 | 抽2张牌。<br>关键字：消耗。 | 抽3张牌。<br>关键字：消耗。 | 已注册；有本地行为证据<br>[base:223](../../../../../mod-the-spire/base-spire/BaseSpire/ContentSource/colorless-cards.spire)<br>[本地证据:299](../../../../../.audit/owner-shared-run-20261012/slice-final.log) | — |
| 12. **君王之剑**<br>[`SovereignBlade`](../../../../../sts2/src/Core/Models/Cards/SovereignBlade.cs) | 攻击 / 衍生<br>2→1 能量 | 造成10点伤害。<br>关键字：保留。 | 造成10点伤害。<br>关键字：保留。 | 未声明 | 铸造/君主之剑<br>数值说明：显示无铸造/能力修正的新牌；SeekingEdge改变目标，Parry追加动态格挡，铸造改变伤害 |
| 13. **扫荡凝视**<br>[`SweepingGaze`](../../../../../sts2/src/Core/Models/Cards/SweepingGaze.cs) | 攻击 / 衍生<br>0→0 能量 | 奥斯提对随机一名敌人造成10点伤害。<br>关键字：虚无、消耗。 | 奥斯提对随机一名敌人造成15点伤害。<br>关键字：虚无、消耗。 | 未声明 | 奥斯提/召唤/独立生物 |
| 14. **衰朽**<br>[`WasteAway`](../../../../../sts2/src/Core/Models/Cards/WasteAway.cs) | 状态 / 状态<br>不可打出→不可打出 能量<br>不可升级 | 每回合失去1点[能量]。 | 不可升级；不适用 | 未声明 | — |

“—”仅表示没有命中本次专属机制检索标签，不能据此认定该牌只需要简单原语。源标识链接指向原版卡类；base 链接中的数字是声明行，本地证据数字是对应日志/检查源码行。

## 角色卡与衍生卡的引用关系

以下按原版卡类中的泛型卡引用归属，包含悬浮提示引用；不是生成行为的完整调用图。TokenCardPool 是共享池，不强行计入某一角色的 87/88 张。

| 衍生卡 | 引用它的五角色卡 |
| --- | --- |
| 瓦解 | 五角色卡类中无直接泛型引用；可能由 Power/遗物/敌人等生成 |
| 燃料 | 压缩（Compact） |
| 巨石 | 原始力量（PrimalForce） |
| 冷光 | 五角色卡类中无直接泛型引用；可能由 Power/遗物/敌人等生成 |
| 心灵腐化 | 五角色卡类中无直接泛型引用；可能由 Power/遗物/敌人等生成 |
| 仆从俯冲 | 冲锋！！（Charge） |
| 仆从捐躯 | 护驾！！！（Guards） |
| 仆从打击 | 下去！（Begone） |
| 小刀 | 精准（Accuracy）、墨之刃（BladeOfInk）、刀刃之舞（BladeDance）、斗篷与匕首（CloakAndDagger）、刀扇（FanOfKnives）、隐秘匕首（HiddenDaggers）、无尽刀刃（InfiniteBlades）、刀刃陷阱（KnifeTrap）、先制打击（LeadingStrike）、幻影之刃（PhantomBlades）、钢铁风暴（StormOfSteel）、袖里乾坤（UpMySleeve） |
| 懒惰 | 五角色卡类中无直接泛型引用；可能由 Power/遗物/敌人等生成 |
| 灵魂 | 捕捉灵魂（CaptureSpirit）、吞噬生命（DevourLife）、挽歌（Dirge）、彼岸一瞥（GlimpseBeyond）、守墓人（GraveWarden）、纠缠（Haunt）、剥夺（Reave）、降灵（Seance）、切断（Severance）、灵魂风暴（SoulStorm） |
| 君王之剑 | 招架（Parry）、征召上前（SummonForth）、剑圣（SwordSage） |
| 扫荡凝视 | 哨卫模式（SentryMode） |
| 衰朽 | 五角色卡类中无直接泛型引用；可能由 Power/遗物/敌人等生成 |
