# base-spire架构审阅（2026-10-13）

本次回答base后续扩展需要调整哪些边界。审阅人为Codex `/root`；依据为当前共享工作区源码和本地sts2源代码，包含DSH本轮正在形成的改动，不代表这些改动已经验收。当前认领、阶段和排期只见[主清单](../../tofix/README.md)，本文记录观察和建议，不授权新生产包。

结论：保留“内容／领域编译→Engine IR→Session”主结构。最值得调整的是Power调用上下文、整局配置与幕定义、Power类型化表示、角色机制与生命周期组装。按后续真实流程逐包处理，避免把所有内容开发挂在一次全面重构之后。

## 已有结构应当保留

- `BaseSpireRuntimeFactory`提供唯一正式构建与建局入口；公开Host和对拍消费同一工厂。规则与每局seed分离，状态由Engine持有。
- 卡牌已通过类型化效果程序复用付款、选择、牌区和hook编排。不要重做已交付的卡牌作者重构。
- `RunResources`、`RunDeck`、`RunRelics`按领域提供操作；`ChoiceEventModule`共享选项守卫、关闭与死亡处理。新内容应沿这些边界扩展。
- 源池身份与行为是否可执行分开；未支持身份显式失败。这一规则在幕参数化后仍须保留。
- 根组包已按Compilation模块拆开。下面的问题是部分新机制重新进入根函数，以及若干语义仍跨模块分散，不能仅凭文件行数判断架构失败。

## 1. Power公共命令缺少真实来源上下文

**影响：高；关联ENV-T06。** [PowerCompilation](../../../../tools/base-migration-baseline/BaseSpire/Powers/Compilation/PowerCompilation.cs)约第50～69行声明的`ApplyPower`只有Owner、PowerId、Stacks、Ticks、SkipNextTick；`ResolvePowerApplied`只有PowerId和Owner。[CardCompilation](../../../../tools/base-migration-baseline/BaseSpire/Cards/Compilation/CardCompilation.cs)约第125～143行以“目标不是玩家”近似“玩家施加”，随后查玩家身上的触发Power并抽牌。

本地原版[ViciousPower](../../../../sts2/src/Core/Models/Powers/ViciousPower.cs)的`AfterPowerAmountChanged`明确检查实际变化量大于零、`applier == Owner`以及目标Power种类。现有公共调用无法表达这些完整事实。该发现是静态契约差异；本轮未执行反例，也不据此声称所有当前Vicious路径都失败。

建议先明确Power施加／层数变化事件的最小语义：目标拥有者、施加者（允许无来源）、来源卡、请求量与实际变化量，以及挂载／叠层／移除时序。以编译期类型化绑定传给Engine过程，不建立C#运行时事件总线。Vicious消费真实来源和变化量；具体签名须由负责人结合现有调用方裁决。

验收应区分同一目标收到玩家、其他敌人、无来源施加，以及零变化与正变化；触发者自己的层数、抽牌次数和暂停续接仍准确。先迁移一个真实触发机制及其全部生产调用方，再扩其他hook。

## 2. 正式配置还没有贯穿内容、幕池和地图

**影响：高；关联ENV-T10，角色部分归ENV-T12。** [工厂](../../../../tools/base-migration-baseline/BaseSpire/SinglePlayer/BaseSpireRuntimeFactory.cs)约第34～35行解析配置后仍取无配置键的`SinglePlayerContentSource.Shared`；[内容组装](../../../../tools/base-migration-baseline/BaseSpire/SinglePlayer/SinglePlayerContent.cs)约第63～67行缓存唯一结果、第140行取第一幕声明、第207行固定Ironclad起始牌组。[RunSourcePoolManifest](../../../../tools/base-migration-baseline/BaseSpire/Run/RunSourcePoolManifest.cs)固定Overgrowth、古代人、Boss与事件池；[MapRoomWiring](../../../../tools/base-migration-baseline/BaseSpire/Rooms/MapRoomWiring.cs)固定第一幕流，`ContinueToNextAct`仍明确Fail。

当前工厂显式拒绝其他角色和未支持范围，因此这不是已经发生的“选Silent却开Ironclad”故障。但只放开角色开关或只实现推进动作，会让配置标签与实际规则脱节。

建议分开可共享的不可变内容目录与按配置解析的开局计划。以角色定义提供初始资源、牌组、遗物和所需机制；以幕定义提供地图参数、完整遭遇／事件／古代人池、队列和随机域。多幕规则在构建时声明，当前幕、访问、库存和游标仍存在Engine；不能在跨幕后换一套隐藏C# RunState。缓存如保留，按真实影响构建的配置和内容身份建键。

T10的第一步应当让已实现第一幕走显式幕定义，再接第二幕生命周期；角色参数化在T12正式开局时完成。保留第一幕合法轨迹与随机语义，验证跨幕长期牌组／HP／遗物保留、幕内队列与事件访问正确重建、不同幕流互不串用。身份若因IR声明调整变化，必须解释迁移，不能拿旧hash强行当行为要求。

## 3. Power仍有多套内部表达

**影响：中高；关联ENV-T06／CORE-AUTHOR-04后续边界裁决。** [AuthorPower](../../../../tools/base-migration-baseline/BaseSpire/Authoring/SliceAuthorFrontend.cs)约第471行同时包含Role、Effect、OwnerTurnEnd、DamageModifier、TriggerEffect等字符串和机制字段；另外还有[PowerRule](../../../../tools/base-migration-baseline/BaseSpire/Powers/PowerRules.cs)枚举机制／文本procedure入口。[TurnCompilation](../../../../tools/base-migration-baseline/BaseSpire/Combat/Compilation/TurnCompilation.cs)约第332～405行用switch、前缀和`long.Parse`再次解释`OwnerTurnEnd`，包括当前Territorial的`gain-power-per-stack`编码。

因此新增相似行为仍可能同时增加DSL解析、字符串协议、字段和编译分支。卡牌的类型化程序已经降低了这一成本，Power尚未达到同一程度。

建议旧写法只在兼容前端适配，降级为带源位置的类型化Power定义：明确生命周期触发点、主体条件、数值表达式及效果程序。时点触发、伤害修正、持续状态的语义不同，应有明确类型，不把所有内容塞进一个字符串或巨大通用事件处理器。复用既有卡牌效果中适用于Power的领域操作和已交付文本procedure能力，不要求先扩core语言。

用Territorial／Ritual／临时力量等能区分时序的代表例逐族迁移，保留首次跳过、按当前层数、移除回补、拥有者阵营等行为。第二个同类Power应只改声明，不能再添加运行时字符串分支。当前Territorial交付依现有派单验收，本建议不要求执行者中途扩包。

## 4. 角色机制和生命周期需要更明确的组装边界

**影响：中高；关联ENV-T06／T12。** [CombatPackageComposer](../../../../tools/base-migration-baseline/BaseSpire/Composition/CombatPackageComposer.cs)约第29～52行递归扫描卡牌程序检测Osty，约第86～129行直接构建Poison／Doom／Orbs及其规则回调；这些已经超过纯依赖接线。`UsesOsty`还重复列出条件、循环、选择等程序节点，未来增加组合节点时存在漏扫风险。

[CombatLifecycle](../../../../tools/base-migration-baseline/BaseSpire/Combat/CombatLifecycle.cs)的`ResetAdditional`是可覆盖的单一委托，根组包把Orbs与Osty清理拼接进去；[BattleRoomCompilation](../../../../tools/base-migration-baseline/BaseSpire/Rooms/Compilation/BattleRoomCompilation.cs)约第532～542行另行重置Stars／OrbSlots。当前EndBattle在进入下一战斗时执行，不能把名称理解成胜利动作已立即清理所有战斗状态。

建议各机制拥有声明、所需编译绑定、明确的生命周期贡献与公开投影；内容能力需求由类型化程序统一遍历或链接阶段得出。根只组合这些明确的模块。生命周期用有确定次序的多项贡献取代可覆盖单回调，区分回合开始／结束、战斗结算、离开战斗、进入战斗、幕切换；先定义各时点的源顺序，不使用无序自动扫描注册。

验证至少覆盖两种附加机制同时注册不互相覆盖、连续两战重置、长期状态不丢失，以及决策暂停后不重复执行hook。本次没有发现已发生的委托覆盖事故；这是已存在结构带来的扩展风险。

## 5. 正式构建与受控夹具入口应逐步分开

**影响：中；后续维护随相关机制包处理。** [PackageBuildOptions](../../../../tools/base-migration-baseline/BaseSpire/Composition/PackageBuildOptions.cs)把IncludeTestHeadbutt、SwapEncounterOrder、SameDefinitionEnemyPair、FromAuthorText等夹具选项与正式Run选项放在同一记录。[ContentCompilation](../../../../tools/base-migration-baseline/BaseSpire/Composition/ContentCompilation.cs)仍有无作者模型时的Manifest分支；根组包及NewRun编译同时承担受控场景与正式流程。

正式工厂目前明确传入真实模型及所需开关，不能据此说生产正在回退旧内容。问题是后续增加一个正式能力时还需理解许多无关组合，容易漏接某条路径。

建议保留同一领域编译器，拆出正式构建输入与受控场景适配器：前者要求配置、内容目录和开局计划齐备；后者生成等价的显式编译输入。逐步把夹具差异收敛到入口，避免在每个机制内部延续`author is null`分支。既有夹具的独立判别能力必须保留。

## 建议落实顺序与验证边界

T10启动时先做幕定义和生命周期的最小调整；T06下一个相关Power包优先补真实来源上下文，再迁移该族的类型化表示；T12正式角色开局前收敛角色配置与机制注册。正式／夹具分离后排，随实际修改推进。任务排期和生产认领仍由主清单维护，这不是新的并行派单。

本轮只做源码与契约审阅，没有运行solution、Engine、Slice、内容加载、对拍或公开轨迹，也不评估训练吞吐性能。源码存在与架构建议不等于规则验收。正在形成的DSH实现应另按各包最终源码与证据复核。
