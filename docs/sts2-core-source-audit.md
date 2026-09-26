# STS2 规则源码审计索引

本文件记录 `sts2/src` 中与游戏语义有关的源码阅读进度，并把观察映射到 `core-the-spire` / `base-spire` 的实现。`sts2/README-for-browsing.md` 说明该工程由本机数据恢复，不是开发者发布的原始源码。仓库当前有 3,405 个 C# 源文件和 26 个 GDScript 文件；这里按规则域审阅，不把表现层资源计作规则源码。

状态含义：**首轮已读**表示围绕当前切片读过关键实现和调用关系，不表示逐行审计了该域全部文件；**待扩展**表示已定位入口，后续扩展前需要继续追踪调用方、顺序和边界情况。

## 已读与当前实现依据

| 规则域 | 主要源码 | 源码观察 | 当前映射 | 状态 |
| --- | --- | --- | --- | --- |
| RNG 算法 | `Core/Random/Rng.cs`, `MegaRandom.cs`, `RunRngSet.cs`, `PlayerRngSet.cs`, `Core/Helpers/StringHelper.cs` | `Rng` 包装 Xoshiro256** / Splitmix64；运行种子与命名流种子由确定性字符串哈希派生；各 API 调用计数器。游戏源码存档可保存流计数器。 | `RandomStreams.cs` 实现 Xoshiro256**、Splitmix64、原版哈希及字符串 run seed 初始化、命名流、均匀/高斯整数与 shuffle；`act_1_map` 等命名流对照源算法。种子 `0` 的 `Next(1000)` 向量 `601,747,103,416` 与高斯整数向量已有回归检查；玩家槽及存档恢复仍未实现。 | 算法首轮已读；运行随机流部分已映射 |
| 动作与暂停 | `Core/GameActions/GameAction.cs`, `Core/GameActions/Multiplayer/PlayerChoiceContext.cs`, `Core/Commands/Cmd.cs` | 外部玩家动作与内部命令分离；动作可在选择期间暂停并恢复。 | `PlayerAction` 与 `EffectNode` 分离；选择会保留待执行效果栈及结束回合阶段，并在提交选择后继续。 | 首轮已读；最小语义已实现 |
| 战斗运行态 | `Core/Combat/CombatState.cs`, `Core/Entities/Players/PlayerCombatState.cs` | 战斗侧轮、玩家回合、行动阶段是不同概念；战斗牌区、资源和参与者状态分开保存。 | `GameState`、`PlayerCombatState`、`EntityState` 和战斗观察；支持有序多敌实例、按敌行动序列轮流行动、逐敌意图观察与显式选敌。Hook/状态枚举仍只覆盖切片。 | 多敌基础模型已实现；回合和内容覆盖待扩展 |
| 仪式与力量 | `Core/Models/Powers/RitualPower.cs`, `StrengthPower.cs` | 敌方刚获得仪式时设置标记；阵营回合结束先跳过一次，后续触发力量。力量通过伤害修正参与攻击。 | `PowerDefinition` 触发阶段和跳过首次触发标志；敌方仪式测试验证第 2 次攻击 3、第 3 次攻击 8。 | 首轮已读；切片已实现 |
| 祭仪教徒遭遇 | `Core/Models/Encounters/CultistsNormal.cs`, `Core/Models/Monsters/CalcifiedCultist.cs`, `DampCultist.cs` | `CultistsNormal` 按 Calcified Cultist、Damp Cultist 顺序生成两只怪。Calcified 初始 HP 普通 38–41 / ToughEnemies 39–42，行动是 Ritual(2) 后循环攻击 9 / DeadlyEnemies 11；Damp 普通 HP 52–54 / ToughEnemies 51–53，Ritual(5) 后循环攻击 1 / DeadlyEnemies 3。各自 Ritual 跳过施加时的 enemy side end，首次力量在下一次 enemy side end 生效。 | `base-spire` 注册两个怪物和原版 `CultistsNormal` encounter；`GameState` 保存有序多敌实例，各怪物轮流执行自己的行动序列，敌方回合结束后逐个触发其 powers；观察投影包含各敌生命/意图，选敌动作可指定实例。普通三回合总受伤 0/10/17，Deadly + Tough 的 HP 范围与攻击意图已验证。尚未覆盖其余原版怪物 AI/钩子。 | 此真实 encounter 首轮行为已映射；怪物库待扩展 |
| 敌人动作图与 Overgrowth 弱遭遇 | `Core/MonsterMoves/MonsterMoveStateMachine/*`, `Core/Models/Monsters/FuzzyWurmCrawler.cs`, `Core/Models/Encounters/FuzzyWurmCrawlerWeak.cs` | Fuzzy Wurm 的移动状态环为首个 Acid Goop → Inhale → Acid Goop → 首个 Acid Goop；Goop 普通/Deadly 伤害 4/6，Inhale 获得 7 Strength；HP 普通 55–57 / ToughEnemies 58–59。`IsPuffed` 只影响表现，不是训练规则状态。 | `EnemyActionDefinition.NextActionId` 与 `EnemyDefinition` 内的动作 ID 索引组成有向动作图，构造时拒绝悬空边；敌方行动后沿图边切换。`base-spire` 注册 `FuzzyWurmCrawlerWeak` 和 Crawler tag，普通与 Deadly + Tough 参数已按四回合伤害/意图测试。条件分支、随机动作选择和怪物局部记忆还未建模。 | 一个真实 Overgrowth Encounter 已映射；其余内容/AI 待扩展 |
| Nibbit 弱遭遇 | `Core/Models/Monsters/Nibbit.cs`, `Core/Models/Encounters/NibbitsWeak.cs` | Alone 变体初始移动为 Butt(12)，随后 Slice(6/7 Deadly，同时获得 5/6 Tough Block)，Hiss 获得 2/3 Deadly Strength，之后回到 Butt；HP 普通 42–46 / ToughEnemies 44–48。多人遭遇的前后位条件分支不属于此弱遭遇切片。 | `base-spire` 注册 Nibbit 与 `NibbitsWeak`，用动作图、组合攻击/格挡效果和 Strength Power 表达循环；测试覆盖循环行为和 Deadly/Tough 参数。 | 一个真实 Overgrowth 弱遭遇已映射；NibbitsNormal 的双体位置分支待实现 |
| Shrink 状态与 OvergrowthCrawlers | `Core/Models/Monsters/ShrinkerBeetle.cs`, `Core/Models/Powers/ShrinkPower.cs`, `Core/Models/Encounters/ShrinkerBeetleWeak.cs`, `OvergrowthCrawlers.cs` | Shrinker Beetle HP 普通 38–40 / ToughEnemies 40–42；先给玩家施加无限 Shrink，再循环 Chomp(7/8 Deadly) 与 Stomp(13/14 Deadly)。Shrink 让施加者的 powered attack 伤害乘 0.7，且施加者死亡时移除。`OvergrowthCrawlers` 有序生成 Shrinker Beetle、Fuzzy Wurm，并带 Shrinker/Crawler 标签。 | `PowerDefinition` 声明出手伤害倍率及施加者死亡移除；运行实例记录 applier，伤害统一经过通用倍率计算，来源死亡时清理相关 Power。`base-spire` 注册 Shrinker 弱遭遇与真实双怪 `OvergrowthCrawlers`；验证 Shrink 对 6 点 Strike 取整为 4、敌方循环伤害、来源死亡解除及多敌顺序。 | Shrink 的单人 combat 语义已映射；Artifact、条件减免等完整 Damage Hook 仍待实现 |
| Act 房间内容生成 | `Core/Models/ActModel.cs`, `Core/Helpers/GrabBag.cs`, `Core/Rooms/RoomSet.cs`, `Core/Runs/RunManager.cs` | Run 先洗牌共享 Ancient 并按随机数量分给后续 Acts；每幕依次洗牌 act/shared Event 列表、生成 weak/regular/elite 队列、抽 Boss、抽本幕 Ancient。grab bag 等权抽取并移除，优先回避与前一遭遇重复或共享 tag，无可选项时回退普通抽取。RoomSet 按访问计数循环房间队列；Event 优先选未访问项，耗尽后允许重复。 | `ActRoomContentState` 集中持有每幕 Event/Ancient/Encounter 选择，并按该 RNG 调用顺序生成。registry 校验 event/ancient ID 类型和 encounter 池分类；房间视图暴露选中的 Event/Ancient/Encounter ID。合成测试验证事件唯一优先、耗尽后循环、同 seed 重放，以及含共享 Ancient、Event、elite bag、Boss 和 act Ancient 时 `up_front` 的调用数。缺完整 base-spire 池、Event 条件与效果、第二 Boss 规则。 | 配置驱动的房间内容生成框架已实现；原版内容和逐 seed 对照待做 |
| 铁甲战士起始内容 | `Core/Models/Characters/Ironclad.cs`, `Core/Models/Cards/StrikeIronclad.cs`, `DefendIronclad.cs`, `Bash.cs`, `Core/Models/Relics/BurningBlood.cs` | 铁甲战士开局生命 80、金币 99，卡组为 5 Strike、4 Defend、1 Bash，起始遗物为 Burning Blood；胜利后若存活则回复 6 生命。基础卡数值分别为 Strike 6、Defend 5、Bash 8 + Vulnerable 2。 | `base-spire` 注册完整起始数据及 Burning Blood；战斗胜利后的回血由 `IRunHook` 调用 `RunHookContext.Heal` 实现，并验证死亡时不触发、治疗不超过最大生命。 | 初始角色规则首轮已读；起始遗物切片已实现 |
| 伤害与易伤 | `Core/Commands/DamageCmd.cs`, `Core/Commands/PowerCmd.cs`, `Core/Models/Powers/VulnerablePower.cs`, `Core/MonsterMoves/Intents/AttackIntent.cs` | 伤害由命令及修正 Hook 结算；易伤是 powered attack 的 1.5 倍乘算并按阵营阶段衰减；意图伤害属于当前状态的投影。 | 内核切片实现 Strength 加算、Vulnerable 乘算和敌方意图预览。尚未实现完整伤害属性、格挡顺序、取整与所有 Hook。 | 首轮已读；需精确对照 |
| 敌人初始生命 | `Core/Entities/Creatures/Creature.cs` | 怪物初始化先设初值，`SetUniqueMonsterHpValue` 再依据传入 RNG 从 `[min,max]` 选择，并尝试避免同侧怪物生命重复。 | `CombatEnvironment.Start` 允许夹具指定 HP；未指定时用会话的 `encounter-hp` 流在闭区间选择。该流名尚未证明等于原版调用方所传流。 | 首轮已读；部分映射 |
| 内容定义与实例 | `Core/Models/AbstractModel.cs`, `Core/Models/ModelDb.cs` | 原版以模型 ID 查找内容，定义概念与运行实例可变数据分开。 | `GameDefinition` 持有不可变规则定义视图；不可观察的 `GameState` 持有战斗实例；包重复 ID 拒绝，覆盖需显式调用 Override；失败注册回滚。 | 首轮已读；最小语义已实现 |
| mod 入口与运行扩展 | `Core/Modding/ModManager.cs`, `Core/Models/ModelDb.cs` | 原版 mod 可加载程序集、初始化模型并应用 Harmony 补丁；补丁可超出数据规则边界。 | 首个内核允许内容覆盖、新增 JSON 效果节点和受控 C# effect handler；战斗事件 Hook 不直接取得可变运行态，扩展数据存于 `GameState` 的按 mod ID 隔离命名空间。独立 `IRunHook` 只提供受限整局查询/命令（当前含遗物检查与治疗）。任意 Harmony 补丁不在兼容范围。 | 最小扩展已实现；原版 mod 清单待审计 |
| 运行与地图 | `Core/Runs/RunState.cs`, `RunManager.cs`, `Core/Map/StandardActMap.cs`, `MapPathPruning.cs`, `MapPostProcessing.cs`, `MapPoint.cs`, `MapPointType.cs`, `Core/Rooms/RoomType.cs`, `Core/Odds/UnknownMapPointOdds.cs` | 已确认运行态保存玩家、地图、幕索引、楼层、访问历史、当前房间栈及多种运行配置；标准地图按路径生成、节点类型分配/限制、重复路线剪枝与配额修复、居中/扩散/拉直的顺序处理。`MapPointType` 与 `RoomType` 不同：Unknown 使用运行级随机流、动态赔率和 Shop 禁用条件，Ancient 解析为 Event。每幕起点均为 Ancient 事件；最后一幕 Boss 后还进入 The Architect 事件，选择胜利选项才结束运行。 | `Runs.cs` 提供通用地图图、合法选点、可观察访问历史、Ancient 开幕事件、赔率运行态和通用嵌套房间栈；子房间完成后恢复父房间，嵌套战斗获胜仍触发整局战斗钩子。`base-spire/StandardActMapGenerator.cs` 移植四个 Act 的单人参数及生成管线。原版 Unknown 基础权重、累计/每幕重置、“上个访问房间为 Shop”/“当前 Unknown 后续节点全为 Shop”黑名单，以及修改候选房间类型和未抽中赔率增量的有序 hook 已移植。`RunEnvironment.Start(string)` 复现字符串种子 hash 入口。遭遇池框架现按 `up_front` 流生成并在房间观察中记录遭遇选择；`CombatEnvironment.StartCurrentEncounterForRun` 可按选择建立多敌战斗，`CompleteCombat` 将结果回写。仍待原版运行时地图逐点差分、运行级 hook listener 收集等价性、真实 Act 遭遇池和敌人初始化。 | 地图、访问历史、基础赔率、赔率 hook、房间栈及遭遇队列框架已实现；地图逐点差分和真实池数据待验 |

### Act encounter pool 迁移清单

以下数量和 Encounter 类型来自各 Act 的 `GenerateAllEncounters()`。weak 数量是每幕常规队列的前缀长度；normal 房间总数不含 boss/ancient；elite 队列固定生成 15 项。当前 `base-spire` 尚未将这些清单注册为可运行遭遇定义。

| Act | Normal 房间数 / weak 前缀 | Weak Encounter | Regular Encounter | Elite Encounter | Boss Encounter |
|---|---:|---|---|---|---|
| Overgrowth | 15 / 3 | FuzzyWurmCrawlerWeak, NibbitsWeak, ShrinkerBeetleWeak, SlimesWeak | CubexConstructNormal, FlyconidNormal, FogmogNormal, InkletsNormal, MawlerNormal, NibbitsNormal, OvergrowthCrawlers, RubyRaidersNormal, SlimesNormal, SlitheringStranglerNormal, SnappingJaxfruitNormal, VineShamblerNormal | BygoneEffigyElite, ByrdonisElite, PhrogParasiteElite | CeremonialBeastBoss, TheKinBoss, VantomBoss |
| Hive | 14 / 2 | BowlbugsWeak, ExoskeletonsWeak, ThievingHopperWeak, TunnelerWeak | ChompersNormal, ExoskeletonsNormal, HunterKillerNormal, LouseProgenitorNormal, MytesNormal, OvicopterNormal, SlumberingBeetleNormal, SpinyToadNormal, TheObscuraNormal | DecimillipedeElite, EntomancerElite, InfestedPrismsElite | KaiserCrabBoss, KnowledgeDemonBoss, TheInsatiableBoss |
| Glory | 13 / 2 | DevotedSculptorWeak, ScrollsOfBitingWeak, TurretOperatorWeak | AxebotsNormal, ConstructMenagerieNormal, FabricatorNormal, FrogKnightNormal, GlobeHeadNormal, OwlMagistrateNormal, ScrollsOfBitingNormal, SlimedBerserkerNormal, TheLostAndForgottenNormal | KnightsElite, MechaKnightElite, SoulNexusElite | AeonglassBoss, QueenBoss, TestSubjectBoss |
| Underdocks | 15 / 3 | CorpseSlugsWeak, SeapunkWeak, SludgeSpinnerWeak, ToadpolesWeak | CorpseSlugsNormal, CultistsNormal, FossilStalkerNormal, GremlinMercNormal, HauntedShipNormal, LivingFogNormal, PunchConstructNormal, SeapunkNormal, SewerClamNormal, TwoTailedRatsNormal | PhantasmalGardenersElite, SkulkingColonyElite, TerrorEelElite | LagavulinMatriarchBoss, SoulFyshBoss, WaterfallGiantBoss |

精确 seed 对齐依赖传入与原版相同且顺序一致的完整内容池。核心现在复现共享 Ancient 分配与每幕 Event → Encounter 队列 → Boss → Ancient 的调用顺序；但 `base-spire` 尚未填充源清单中的全部内容，且未实现解锁/可用性筛选、教程发现顺序和第二 Boss，因此实际原版遭遇 ID 仍不能 seed 对齐。当前合成测试验证的是配置队列顺序与随机调用计数，不是完整源清单的差分。

## 后续深读顺序

1. **战斗结算闭环**：逐个追踪 `CombatManager`、`DamageCmd`、`PowerCmd`、`CreatureCmd` 和 Hook 监听对象收集顺序；记录伤害计算阶段、力量/易伤/格挡的先后、取整和击杀事件。
2. **玩家决策语义**：继续阅读所有常用 `GameAction`、选择上下文、卡牌目标和取消规则；为合法动作生成补上目标、能量、状态和终局条件。
3. **实体与内容生命周期**：阅读 Creature、CardModel、PowerModel、MonsterModel 和模型注册；验证复制、变形、附加效果和升级实例状态怎样进入运行态。
4. **随机流兼容**：逐项读取 `RunRngType` / `PlayerRngType` 和存档恢复；复现原版种子字符串哈希、每槽玩家种子与所有流命名后，通过已知向量比对。
5. **整局流程**：继续追踪 RunManager 与运行状态写入调用方；逐项审阅地图路径生成/节点类型分配、房间栈、遭遇与奖励生成、事件、商店、休息、幕切换及地图 Hook，再在 core/base-spire 逐域实现。
6. **mod 扩展边界**：对照 ModManager、模型发现、初始化器和 Harmony 补丁，形成可由 DSL 表达、需 C# 扩展、只能真实游戏运行三类能力清单。

## 已知实现限制

- 当前战斗内核是用于验证边界的首版切片，未达到原版战斗等价。特定角色遗物、反应式 Hook、多人协作、完整卡牌区域规则、敌人意图选择、Ascension 配置和伤害类型都待实现。
- `base-spire` 中现有状态/效果 primitive 是内核测试语义，不代表可直接无误迁移所有原版机制。
- 目前 `RandomStreams` 复现 RNG 算法与确定性命名流基础，但尚未复现原版全部种子派生、RNG 分类、玩家槽种子和存档计数器语义。
- `Runs.cs` / `ActRoomContent.cs` 提供通用整局流转、可嵌套房间栈、配置驱动的事件/遭遇队列及选择视图；`CombatEnvironment.StartCurrentEncounterForRun` 按房间已选遭遇从 run 牌组及共享 `shuffle` 流构造多敌战斗，`RunEnvironment.CompleteCombat` 将终局生命回写并完成当前战斗房间。尚未实现原版全量池数据、事件选项与结算、运行内容可用性筛选或真实怪物初始化。
- 当前完整性范围是单人玩法到胜负、快照内全部原版内容默认全解锁；账号级元进度、多人、UI/音画和平台服务排除。

## 整局规则迁移队列

| 阶段 | 源码入口 | 迁移目标 | 验收状态 |
| --- | --- | --- | --- |
| 地图定义与寻路 | `Core/Map/StandardActMap.cs`, `MapPathPruning.cs`, `MapPostProcessing.cs`, 各 Act 模型 | 复现种子驱动的地图拓扑、节点类型分配和地图后处理；规则配置归 `base-spire` | 算法及四个 Act 的单人参数已移植；需对照原版运行时固定 seed 结果 |
| 节点与房间转场 | `Core/Runs/RunManager.cs`, `Core/Runs/RunState.cs`, `Core/Rooms/*Room.cs`, `Core/Odds/UnknownMapPointOdds.cs` | 复现 Ancient 入口、选点、map point 到 room 的解析、当前房间栈、房间完成、幕切换及终局事件 | Ancient 每幕 Event、访问历史、Shop 黑名单、原版基础 Unknown 赔率及跨 Act 重置、资源回写、子房间压栈/返回、候选房间类型与赔率增量 hook、Architect 终局事件边界已实现；运行时 hook listener 收集、Architect 的具体选项、原版房间队列和 Encounter 选择待实现 |
| 战斗房间 | `Core/Combat/*`, `Core/Commands/*`, `Core/GameActions/*`, `Core/MonsterMoves/*` | 完成多实体战斗、合法动作、伤害阶段、卡牌区域、AI 意图及结果回写整局 | `EncounterDefinition` 支持有序多敌；敌人按各自动作图逐一行动、观察意图并支持显式选敌；`CultistsNormal`、`FuzzyWurmCrawlerWeak`、`NibbitsWeak` 和 `OvergrowthCrawlers` 已映射并验证关键行为。通用 outgoing multiplier 与 Power applier-death cleanup 已用于 Shrink。可由整局启动注册遭遇并回写胜负生命；真实池全量、条件/随机 AI、怪物 HP 唯一化和完整 Hook 尚待实现 |
| 非战斗房间与奖励 | `Core/Rooms`, `Core/Rewards`, `Core/Events`, `Core/Models/Events` | 迁移宝箱、事件、商店、营火、奖励选择和玩家资源更新 | 未实现 |
| 原版内容库 | `Core/Models/Cards`, `Monsters`, `Powers`, `Relics`, `Potions`, `Encounters`, `Acts`, `Characters` | 默认全解锁，完整注册快照中单人玩法内容 | 仅初始铁甲战士卡牌和咔咔切片 |
