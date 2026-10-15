# 五角色公共原语阶段记录（2026-10-12）

本记录在追加Poison续包时遭外部中断截断；以下按同日已核对日志与对话交付信息重建，不声明逐字恢复原文。任务ENV-T06-five-character-primitives-20261012，当前状态只维护于[主清单](../../tofix/README.md#env-t06)。Codex亲自实施，不派DSH。

## 已有阶段交付与证据恢复

已交任意来源药水获得及商店迁移、星星资源/固定费用/X快照、锻造生成与实例成长、五种充能球/槽位/Focus/回合触发和公开投影。准确行为及未支持范围见[角色战斗契约](../../../design/mod-the-spire/character-combat/spec.md)。规则归base，局内状态归Engine，不按卡ID写执行特例。

已有证据目录`.audit/owner-five-character-primitives-20261012/`保存build-final.log（solution通过）、engine-final.log（99通过）、slice-complete.log（818通过）、net-complete.log（418通过）、regression.log（既有严格16双侧通过+1接线自检）。新增角色机制并无对应独立双侧case，旧suite不能替代新机制验收。docs-final.log记录4处既有DSH交接锚点问题，空白检查通过。

已核对身份迁移：dev由f568b3218da80445/89667f311bbaa06d变为ab5a52d6ad74e9bc/77b7dc8861d45266；dev-map由ded56b348ee3a7f6/cedae31020fea3b5变为b9d64d38cdc95b37/57f3cf0b00f4ff25。身份依据为新增资源/队列/回合IR及公开投影，描述在descriptors.jsonl。未执行新正常公开五角色整局，不注册半实现源卡到原版池。

Doom基础7项检查验证阈值相等/不足/零层数、绕过格挡、批量死亡/清理、敌方回合末、玩家失败与非法参数拒绝。仅配置Doom时生成接线，默认身份不变。死亡防止、复活/Reattach、UndyingSigil与完整AfterDiedToDoom参与者仍未交付。

## Poison 基础生命周期续包

交付CombatPoison、UnblockableDamageCompilation、SpirePowerSource的Poison/Accelerant支持及两侧开始接线。类型化层数/生命/局部次数归Engine，汇总存活对手催化后捕获次数，逐击实时读取中毒、存活后递减、耗尽删除；死亡敌人不行动，清场不触发玩家中毒。伤害绕过格挡/力量但处理生命损失上限、既有掉血计数和失败结算，不等价于完整CreatureCmd.Damage。

8项判别覆盖实际DSL施加/声明链接、生命损失上限、死亡对手排除、5/4/3源向量、次数封顶与删除、敌方致死清场、玩家对手汇总/掉血次数及致死不递减。引用声明通过SpireCardSource.Parse第三参数传入名称/ID表，不放宽未知引用拒绝。

证据同目录：poison-local-final.log最终8项通过；poison-build-final.log最终solution退出0；poison-engine.log为99通过；poison-slice.log为833通过，随后只补清场守卫和同用例断言，最终局部8再次通过，未重复全量Slice；poison-net.log最终418/418；poison-descriptors.jsonl记录身份。修改范围仅base生产/检查及文档，未修改core/net/参考或正式卡池。

## Poison交付时的未验证与接续

以下为Poison交付时的范围与接续，随后Osty基础续包见下节；本段“未支持Osty目标”和后续“宠物Power未支持”仍成立，但不表示Osty身份/召唤/复活尚未交付。当前认领与下一包只查[主清单T06](../../tofix/README.md#env-t06)。

完整伤害hook、死亡防止/复活、Osty目标未支持；星星费用修正/重复出牌资源继承、Forge身份/完整SovereignBlade、Defect正式开局和全部球hook仍开放。Poison仅本地基础包完成，无新独立双侧或正常公开整局结论。下一就绪包为Osty身份/召唤/MaxHP/复活，实施前核对OstyCmd并在主清单细化认领；代受伤按真实依赖安排，不半实现。


## Osty 基础原语续包

用户追加派单，Codex亲自实施，任务归属ENV-T06-five-character-primitives-20261012。交付用途为组合宠物召唤与生命操作，当前状态只维护主清单，规则见角色战斗契约Osty小节。

新增CombatOsty及OstyCardEffect，前端/数值链接/验证/IR/编译完整接入summonOsty、healOsty、loseOstyHp、ostyAttack。Entity Owner/Hp/MaxHp和OstyPets归Engine；零召唤无操作，活着增长最大与当前生命，死亡身份保留，复活重设最大与当前生命，治疗不复活。公开投影与base公共集合语义接入，换战斗清理接入CombatLifecycle。宠物不进敌人队列。首次声明按所有牌/药水/嵌套程序及hook检测，在伤害/房间装配前创建符号；无Osty内容包保留既有默认身份。

DieForYou基础行为通过DamageCompilation玩家格挡后独立分支接入：powered正剩余伤害由存活宠物承担，整击过量不穿透玩家，宠物死亡后后续击正常命中玩家；不发玩家掉血反应/计数。非powered和直接玩家HP损失不转移。宠物基础攻击不借用玩家Strength，但完整宠物/目标伤害修正未支持，不宣称原版攻击完整。

修改范围base Slice/Domain/Combat、Effects、Compilation、SinglePlayer/BasePublicSemantics、Slice.Tests及文档；未改core/net/独立参考或批量卡牌。源依据OstyCmd.Summon、CreatureCmd.GainMaxHp和DieForYouPower。局部6项涵盖普通/升级6/9向量、增长不满血、治疗封顶/不复活、同身份复活、零值、不存在/死亡宠物攻击、玩家Strength隔离、非法字段及单敌人代受伤致死不穿透/玩家计数隔离/死亡后失去保护。

最终证据目录`.audit/owner-five-character-primitives-20261012/`：osty-build.log构建退出0（1项既有differential警告）；osty-engine.log为99通过；osty-slice.log为839通过；osty-net.log为418/418。局部命令dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests -- --osty-only为6通过。未执行新独立双侧、正式Necrobinder开局/整局、独立公开宠物轨迹或换战斗清理定向case。

完成度：基础身份/召唤/成长/治疗/生命损失/死亡保留/复活本地行为完成；基础powered代受伤本地完成；攻击仅基础；召唤与复活事件仅接缝；宠物Power/目标/攻击修正、ModifySummonAmount及完整hook、死亡防止、正式角色开局、独立原版验收未完成。不能用6项本地检查折算全Osty百分比。下一任务按主清单依赖收敛完整宠物伤害/Power及初始化，不据本文自动认领。
