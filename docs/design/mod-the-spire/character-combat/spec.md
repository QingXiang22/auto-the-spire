# 角色战斗公共能力契约

本页定义共享机制及当前明确边界；排期和认领只维护[ENV-T06](../../../review/tofix/README.md#env-t06)。所有局内资源与实例仍归Engine，领域对象只生成IR。当前不是完整五角色能力声明。

## 星星

源依据：sts2/src/Core/Commands/PlayerCmd.cs的GainStars/LoseStars/SetStars，Entities/Players/PlayerCombatState.cs的资源操作，Models/CardModel.cs的SpendResources/ResolveStarXValue；GatherLight、FallingStar、Stardust说明获得、固定费和X费消费者。

Player.Stars为战斗级非负整数状态，初始化与换战斗清零，不随回合重置。CombatStars.Change捕获输入一次，战斗结算后不操作；Gain先ShouldGain、写状态再AfterGained，Lose封底0，Set增加走Gain、减少直接设定。作者入口、范围及暂未支持参与者见[语言参考](../../../dsl/0.0.2/authoring-reference.md#星星资源与锻造)。默认空接缝仅适用当前内容，不能代替全部源hook。

手动出牌同时校验能量和星星，先支付能量再支付星星；固定星星费用按实际升级形态选取。星星X快照独立于PaidX，支付后效果读取PaidStarX；零星星X可打出。自动出牌不扣资源。费用修正、AfterStarsSpent完整参与者、ChemicalX、重复出牌资源继承和公开费用元数据仍需后续接续，不把受控原语检查当源独立对拍。

## 锻造

源依据：Commands/ForgeCmd.cs的Forge/GetSovereignBlades/IncreaseSovereignBladeDamage、Models/Cards/SovereignBlade.cs的AddDamage/升级以及RefineBlade.cs的9/13锻造数值。

CombatForge.Emit链接武器定义和两形态已有变量，先捕获数量，非消耗战斗牌区没有武器则生成普通实例，满手回退弃牌；触发进入战斗接缝后，按战斗牌区顺序成长所有该定义实例，包括消耗区，再调用AfterForged接缝。不修改长期牌组、不抽取随机。普通克隆不是源IsDupe；目前没有IsDupe权威状态，不能拿CloneOf当作dupe筛选。

当前只支持实例生成/成长部分；CreatedThroughForge、临时dupe排除、锻造重复数/目标改变、全部AfterForge参与者与SovereignBlade完整牌程序未交。不注册半实现源牌到正式原版池。受控检查复用RefineBlade9/13数值，未冒充其完整EnergyNextTurn效果或公开原版流程。

## 充能球

源依据：Commands/OrbCmd.cs、Entities/Orbs/OrbQueue.cs及Models/Orbs中的LightningOrb/FrostOrb/DarkOrb/PlasmaOrb/GlassOrb。CombatOrbs只生成IR；队列实例、种类、储值与槽位均由Engine状态维护，不保存独立C#运行时队列。

作者入口为channelOrb/evokeOrb/addOrbSlots/removeOrbSlots。队列有序，满槽先激发首球；移除槽位从尾部丢弃，不激发。编译配置SliceAuthorModel.BaseOrbSlots取0～10，初始化与下一战斗恢复该容量。基础槽位为0的角色在零槽充能时自动获得1槽；有基础槽位的角色失去全部槽位后充能不会自动补槽。当前公开工厂仍仅接Ironclad，配置3槽的局部检查不等于完成Defect开局和CrackedCore。

Lightning被动3/激发8，使用RandomTargetStream从当前存活敌人按源集合顺序抽取。Frost被动2/激发5格挡。两者数值受Focus影响且最低0，伤害/格挡为unpowered。Dark初始储值6，回合末增加max(6+Focus,0)，激发以储值攻击当前HP最低敌人，同HP保留集合顺序，激发不再次叠加Focus。Plasma回合开始+1能量、激发+2，不受Focus影响。Glass初始基础4，被动捕获max(基础+Focus,0)后基础减1封底0，再打全体；激发为当前修正值的两倍。修改值不大于0时Glass不衰减。

Plasma在玩家回合开始能量重置及AfterTurnStart之后触发；其他球在结束回合手牌处理之前触发。结算后不继续操作，换战斗销毁队列。局部检查覆盖五种球加载与激发、溢出/移槽、非出队重复激发、Plasma重置顺序、Glass捕获与衰减、正负Focus及Dark最低HP选择。尚未实现全部AfterOrbChanneled/AfterOrbEvoked参与者、触发次数修正、指定球/末球与指定被动目标、完整角色初始化及独立双侧流程；这些边界不能被13项受控检查替代。

## Poison 基础生命周期

来源为 Models/Powers/PoisonPower.cs。Power 使用 role = Poison 或 role = Accelerant；已有 applyPower 负责创建与叠层。每个拥有者所属阵营回合开始时捕获次数 min(正Poison层数, 1 + 存活对手Accelerant层数总和)，随后每次实时读取当前Poison，造成无格挡/无力量修正伤害，仍经过已支持的生命损失上限。伤害后若存活则递减1，耗尽销毁能力；死亡后不继续执行有效伤害或行动。

层数、生命和局部次数归Engine状态。敌方在行动前解析；玩家在能量重置/抽牌及已有AfterTurnStart站点后、充能球之前解析。不抽随机数。死亡走既有MonsterDied/失败结算，战斗胜利仍在动作尾部提交。当前仅支持玩家和存活敌人，不支持Osty目标、死亡防止/复活、全部ModifyDamage/AfterDamage参与者或独立双侧一致性；玩家伤害接缝复用既有掉血计数及ReceivedDamage反应，不能当完整CreatureCmd.Damage实现。

判别用例须覆盖基础5伤害/降为4、Accelerant两层造成5+4+3、对手汇总与死亡过滤、次数不超过层数、耗尽删除、格挡与Strength不改结果、玩家伤害次数、敌人死亡不行动。

## Osty 基础生命周期

依据OstyCmd.Summon与CreatureCmd.GainMaxHp：summonOsty(amount)零值无操作，首次创建归属玩家的独立宠物；存活时同时增加MaxHP/HP，死亡保留身份，复活以本次amount重设MaxHP/HP。healOsty只治疗存活宠物并封顶；loseOstyHp为不经格挡的生命损失，死亡不移除实体；ostyAttack只允许存活宠物攻击当前所选敌人，基础伤害不使用玩家Strength。金额使用共享数值表达式，负值显式拒绝。

状态归Engine的Osty实体与公开OstyPets集合，不加入Monsters队列。换战斗销毁宠物；零金额不发召唤事件，复活事件先于召唤事件。事件接缝不等于完整AfterSummon/AfterOstyRevived参与者。基础DieForYou路由已接：玩家格挡先吸收，powered且剩余伤害为正时由存活Osty承担整次未挡伤害，致死不穿透玩家，后续伤害在Osty死亡后恢复命中玩家；宠物掉血不增加玩家掉血计数/反应。非powered及直接玩家HP损失不转移。尚未支持宠物Power/Focus/Doom/Poison、宠物攻击完整修正、伤害/死亡防止、正式Necrobinder开局和召唤修正；这些必须作为未完成范围报告。

## Doom 基础接缝

来源为 Models/Powers/DoomPower.cs 的 GetDoomedCreatures、DoomKill 与 BeforeSideTurnEnd。Power 显式声明 role = Doom，通过现有 applyPower 挂载；resolveDoom = {} 只允许空块，立即解析敌方阈值，无注册 Doom Power 的包不能编译此效果。

Engine 保存能力层数、生命及隐藏批次实例。阵营回合结束前捕获仍存活、正层数且 HP 不大于 Doom 的单位，再按捕获顺序致死；不走伤害/格挡/Strength 修正，不使用随机流。DiedToDoom 事件携带 Count/Side，为领域接缝而非完整 AfterDiedToDoom 参与者。玩家死亡复用失败结算，敌人走 MonsterDied/集合移除及公开动作尾部胜利结算。判别包括阈值相等/不足/零层数、格挡、多个敌人、清理和玩家失败。

死亡防止、Reattach/复活、UndyingSigil 触发站点改变、宠物阵营与完整源hook未支持。该基础包不能证明 Necrobinder 正常整局或独立双侧一致性。

## 其余角色能力边界

本包身份迁移依据：新增Player.Stars/OrbSlots、星星支付与效果IR、Orb实体/有序队列、回合被动/战斗清理、队列公开投影及商店药水获得路径，均改变规则或公开语义。2026-10-12同一176张注册牌配置的dev身份由f568b3218da80445/89667f311bbaa06d迁移为ab5a52d6ad74e9bc/77b7dc8861d45266；dev-map由ded56b348ee3a7f6/cedae31020fea3b5迁移为b9d64d38cdc95b37/57f3cf0b00f4ff25。描述证据在`.audit/owner-five-character-primitives-20261012/descriptors.jsonl`。受控规则检查及身份快照迁移不证明原版对拍通过；旧回放不能以旧构建身份接受新规则。

Osty须覆盖召唤/增上限/死亡复活/代受伤/目标归属；Doom批量致死与源hook、Poison/Sly手动弃牌行为须按实际生命周期验证。它们及上文明确未支持的参与者是本包继续实施的真实缺口，不表示已加载或行为已支持。
