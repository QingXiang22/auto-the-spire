# 角色战斗公共能力约定

文档类型：参考（Reference）。本页定义共享机制及当前明确边界；排期和认领只维护[行为要求](../../../specs/environment.md#env-t06)。所有局内资源与实例仍归Engine，领域对象只生成IR。当前不是完整五角色能力声明。

## 阵营回合与确认死亡

敌方回合开始先对本侧存活成员执行格挡清理，再解析Poison及行动；持有声明preventsBlockClear的能力时保留格挡，与玩家沿用同一谓词。不能让上一回合的格挡无条件跨敌方回合累积。

玩家已确认死亡的路径在提交Defeat前清除其Power实例，并提交最终HP/MaxHp；中毒致死同样走此收尾，不把“死后不再递减Poison”误写成“保留死者Poison”。当前清理针对已支持的普通死亡；死亡阻止、保留能力参与者及敌人复活仍依各自机制约定，不能据此声称全部死亡hook已实现。

## 星星

源依据：sts2/src/Core/Commands/PlayerCmd.cs的GainStars/LoseStars/SetStars，Entities/Players/PlayerCombatState.cs的资源操作，Models/CardModel.cs的SpendResources/ResolveStarXValue；GatherLight、FallingStar、Stardust说明获得、固定费和X费消费者。

Player.Stars为战斗级非负整数状态，初始化与换战斗清零，不随回合重置。CombatStars.Change捕获输入一次，战斗结算后不操作；Gain先ShouldGain、写状态再AfterGained，Lose封底0，Set增加走Gain、减少直接设定。作者入口、范围及暂未支持参与者见[语言参考](../how-to/cards-and-combat.md)。默认不执行操作的扩展接口仅适用当前内容，不能代替全部源hook。

手动出牌同时校验能量和星星，先支付能量再支付星星；固定星星费用按实际升级形态选取。星星X快照独立于PaidX，支付后效果读取PaidStarX；零星星X可打出。自动出牌不扣资源。费用修正、AfterStarsSpent完整参与者、ChemicalX、重复出牌资源继承和公开费用元数据仍需后续接续，不把受控原语检查当源独立参考比较。

## 锻造

源依据：Commands/ForgeCmd.cs的Forge/GetSovereignBlades/IncreaseSovereignBladeDamage、Models/Cards/SovereignBlade.cs的AddDamage/升级以及RefineBlade.cs的9/13锻造数值。

CombatForge.Emit链接武器定义和两形态已有变量，先捕获数量，非消耗战斗牌区没有武器则生成普通实例，满手回退弃牌；触发进入战斗接口后，按战斗牌区顺序成长所有该定义实例，包括消耗区，再调用AfterForged接口。不修改长期牌组、不抽取随机。普通克隆不是源IsDupe；目前没有IsDupe权威状态，不能拿CloneOf当作dupe筛选。

当前只支持实例生成/成长部分；CreatedThroughForge、临时dupe排除、锻造重复数/目标改变、全部AfterForge参与者与SovereignBlade完整牌程序未交。不注册半实现源牌到正式原版池。受控检查复用RefineBlade9/13数值，未冒充其完整EnergyNextTurn效果或公开原版流程。

## 充能球

源依据：Commands/OrbCmd.cs、Entities/Orbs/OrbQueue.cs及Models/Orbs中的LightningOrb/FrostOrb/DarkOrb/PlasmaOrb/GlassOrb。CombatOrbs只生成IR；队列实例、种类、储值与槽位均由Engine状态维护，不保存独立C#运行时队列。

作者入口为channelOrb/evokeOrb/addOrbSlots/removeOrbSlots。队列有序，满槽先激发首球；移除槽位从尾部丢弃，不激发。编译配置SliceAuthorModel.BaseOrbSlots取0～10，初始化与下一战斗恢复该容量。基础槽位为0的角色在零槽充能时自动获得1槽；有基础槽位的角色失去全部槽位后充能不会自动补槽。当前公开工厂仍仅接Ironclad，配置3槽的局部检查不等于完成Defect开局和CrackedCore。

Lightning被动3/激发8，使用RandomTargetStream从当前存活敌人按源集合顺序抽取。Frost被动2/激发5格挡。两者数值受Focus影响且最低0，伤害/格挡为unpowered。Dark初始储值6，回合末增加max(6+Focus,0)，激发以储值攻击当前HP最低敌人，同HP保留集合顺序，激发不再次叠加Focus。Plasma回合开始+1能量、激发+2，不受Focus影响。Glass初始基础4，被动捕获max(基础+Focus,0)后基础减1封底0，再打全体；激发为当前修正值的两倍。修改值不大于0时Glass不衰减。

Plasma在玩家回合开始能量重置及AfterTurnStart之后触发；其他球在结束回合手牌处理之前触发。结算后不继续操作，换战斗销毁队列。局部检查覆盖五种球加载与激发、溢出/移槽、非出队重复激发、Plasma重置顺序、Glass捕获与衰减、正负Focus及Dark最低HP选择。尚未实现全部AfterOrbChanneled/AfterOrbEvoked参与者、触发次数修正、指定球/末球与指定被动目标、完整角色初始化及独立双侧流程；这些边界不能被13项受控检查替代。

## Poison 基础生命周期

来源为 Models/Powers/PoisonPower.cs。Power 使用 role = Poison 或 role = Accelerant；已有 applyPower 负责创建与叠层。每个拥有者所属阵营回合开始时捕获次数 min(正Poison层数, 1 + 存活对手Accelerant层数总和)，随后每次实时读取当前Poison，造成无格挡/无力量修正伤害，仍经过已支持的生命损失上限。伤害后若存活则递减1，耗尽销毁能力；死亡后不继续执行有效伤害或行动。

层数、生命和局部次数归Engine状态。敌方在行动前解析；玩家在能量重置/抽牌及已有AfterTurnStart站点后、充能球之前解析。不抽随机数。死亡走既有MonsterDied/失败结算，战斗胜利仍在动作尾部提交。当前仅支持玩家和存活敌人，不支持Osty目标、死亡防止/复活、全部ModifyDamage/AfterDamage参与者或独立双侧一致性；玩家伤害处理接口复用既有掉血计数及ReceivedDamage反应，不能当完整CreatureCmd.Damage实现。

测试用例须覆盖基础5伤害/降为4、Accelerant两层造成5+4+3、对手汇总与死亡过滤、次数不超过层数、耗尽删除、格挡与Strength不改结果、玩家伤害次数、敌人死亡不行动。

## 静默猎手中毒牌族

源池与卡牌直接引用的Power共同确定12张中毒相关牌：Accelerant、BouncingFlask、BubbleBubble、CorrosiveWave、DeadlyPoison、Envenom、Haze、Mirage、NoxiousFumes、Outbreak、PoisonedStab、Snakebite。正式内容位于`silent-cards.spire`与`silent-powers.spire`，保留普通／升级、稀有度及SilentCardPool归属。该内容族不开放Silent正式开局或替代多幕胜负验收。

卡牌及玩家触发能力通过`ApplyOwnedPower`施加，存活目标与战斗有效时才操作。正Poison被Artifact阻止时消耗一层Artifact，不挂毒、不推进Outbreak。成功施加一次计一次，与层数无关；Outbreak每三次对全体存活敌人造成自身层数的unpowered伤害，公开`Power.ApplicationCount`保留0～2余数，跨回合不重置。中毒递减、受控初始状态和敌人行动不冒充玩家施毒。Artifact接入限定此Poison路径，不代表所有debuff阻止规则已迁移。

CorrosiveWave每张实际抽到的牌向各存活敌人施毒，并在拥有者回合结束时移除。NoxiousFumes在下次玩家回合完成抽牌后施毒，此后每回合重复。Envenom逐次检查powered攻击的未格挡伤害，给仍存活目标施毒；全挡、unpowered伤害和死亡目标不触发。伤害先确认死亡再派发存活目标反应，避免连锁Outbreak重复提交死亡事件。BouncingFlask每次反弹独立消费一次CombatTargets，前次触发杀死的目标不参加后次抽取。Mirage实时汇总存活敌人的Poison，再走正常Dexterity／Frail格挡修正。

Haze的永久Sly沿共享手动弃牌流程：先移动整个已选批次并执行弃牌hook，再按顺序免费自动打出奇巧牌。待处理条目为Engine的隐藏SlyDiscardEntry，不构成新的牌区；普通回合末清手不触发。临时授予奇巧、其他角色完整参与者与独立原版差异测试仍按各自合同，不由本族受控检查推断。

定向入口`--poison-only`覆盖实际加载、三种升级路径、生命周期及组合边界；`--suite source`从本地原版池及直接Power引用独立核对完整来源范围。广覆盖回归在整组本地轻量检查后由GitHub Actions执行。

## 静默猎手奇巧牌族

源依据为Cards/Abrasive、FlickFlack、Reflex、Ricochet、Tactician、Untouchable、Haze的CanonicalKeywords，HandTrick.OnPlay、MasterPlannerPower.AfterCardPlayed，以及CardModel.IsSlyThisTurn／EndOfTurnCleanup／DeepCloneFields。全部单人原生奇巧7张，Haze复用已交付；HandTrick与MasterPlanner是另外两张授予奇巧的消费者。多人Sneaky保持原范围，不进入单人牌池。

卡定义的原生Sly与卡实例AddedSly、SingleTurnSly共同决定实际奇巧。HandTrick先获得7／10格挡，再从手牌中选择恰好一张Skill且当前非奇巧的实例，本回合生效；空域不问，单候选自动处理，多候选暂停，不能用nonAttack放入Power／Status。MasterPlanner在每次技能打完后授予该实例本场战斗持续的Sly；普通／升级费用2／1，重复获得不产生重复自动出牌。两种状态在Engine卡实例公开字段中维护，升级保留、战斗克隆复制且隔离，新实例／变换不继承，战斗副本不写回长期牌组。

手动弃牌先捕获奇巧资格、移动并完成整个批次的弃牌hook，再顺序免费自动打出；普通清手不触发。SingleTurnSly在回合末对本场全部实例清理，AddedSly跨回合保留至战斗结束；不影响定义Sly。嵌套弃牌批次各自结算，已排队卡不因后续资格变化重复入队。

Abrasive先给1敏捷再给4／6荆棘。ThornsPower在敌人powered攻击计算伤害后、格挡及生命结算前，对攻击者造成等同层数的unpowered伤害；全挡和零伤害攻击仍触发，直接掉血及unpowered伤害不触发。反击杀死攻击者时当前命中仍完成，下一次命中因来源已死亡而跳过；反击不套玩家力量或敌人易伤，但攻击者格挡仍可吸收。

验证覆盖全部新牌普通／创建升级／实际升级，原生与授予Sly的免费出牌、空域／精确Skill筛选／状态隔离、批次顺序、跨回合清理和复制升级，以及反击的时点与末杀边界。本地只运行受影响定向组；完整回归交给推送后的Actions。

## Osty 基础生命周期

依据OstyCmd.Summon与CreatureCmd.GainMaxHp：summonOsty(amount)零值无操作，首次创建归属玩家的独立宠物；存活时同时增加MaxHP/HP，死亡保留标识，复活以本次amount重设MaxHP/HP。healOsty只治疗存活宠物并封顶；loseOstyHp为不经格挡的生命损失，死亡不移除实体；ostyAttack只允许存活宠物攻击当前所选敌人，基础伤害不使用玩家Strength。金额使用共享数值表达式，负值显式拒绝。

状态归Engine的Osty实体与公开OstyPets集合，不加入Monsters队列。换战斗销毁宠物；零金额不发召唤事件，复活事件先于召唤事件。事件接口不等于完整AfterSummon/AfterOstyRevived参与者。基础DieForYou路由已接：玩家格挡先吸收，powered且剩余伤害为正时由存活Osty承担整次未挡伤害，致死不穿透玩家，后续伤害在Osty死亡后恢复命中玩家；宠物掉血不增加玩家掉血计数/反应。非powered及直接玩家HP损失不转移。尚未支持宠物Power/Focus/Doom/Poison、宠物攻击完整修正、伤害/死亡防止、正式Necrobinder开局和召唤修正；这些必须作为未完成范围报告。

## Doom 基础接缝

来源为 Models/Powers/DoomPower.cs 的 GetDoomedCreatures、DoomKill 与 BeforeSideTurnEnd。Power 显式声明 role = Doom，通过现有 applyPower 挂载；resolveDoom = {} 只允许空块，立即解析敌方阈值，无注册 Doom Power 的包不能编译此效果。

Engine 保存能力层数、生命及隐藏批次实例。阵营回合结束前捕获仍存活、正层数且 HP 不大于 Doom 的单位，再按捕获顺序致死；不走伤害/格挡/Strength 修正，不使用随机流。DiedToDoom 事件携带 Count/Side，为领域接口而非完整 AfterDiedToDoom 参与者。玩家死亡复用失败结算，敌人走 MonsterDied/集合移除及公开动作尾部胜利结算。检查包括阈值相等/不足/零层数、格挡、多个敌人、清理和玩家失败。

死亡防止、Reattach/复活、UndyingSigil 触发站点改变、宠物阵营与完整源hook未支持。该基础包不能证明 Necrobinder 正常整局或独立双侧一致性。

## 其余角色能力边界

角色资源、支付、球队列和公开投影变化必须更新构建标识；兼容性由同提交的公开约定检查确认。

Osty须覆盖召唤/增上限/死亡复活/代受伤/目标归属；Doom批量致死与源hook、Poison/Sly手动弃牌行为须按实际生命周期验证。它们及上文明确未支持的参与者是本包继续实施的真实缺口，不表示已加载或行为已支持。
