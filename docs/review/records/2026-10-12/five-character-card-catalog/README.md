# 五角色卡牌与实现证据清单（2026-10-12）

这份清单供逐卡校对原版描述、base 实现和公共机制缺口。五角色源卡池共 **439 张**，其中 **143 张实际注册**；这次检阅定位到其中 **110 张的本地行为检查证据**。注册数量与本地检查都不能代替完整原版一致性验收。项目排期、认领及验收只在[主清单 ENV-T12](../../../tofix/README.md#env-t12)维护，本目录是日期快照。

## 按角色查看全部卡牌

每张卡列出中文名、原版类标识及源码链接、类型、稀有度、普通/升级费用、普通/升级描述、base 声明链接、已有行为证据和专属机制线索。卡牌按原版池顺序排列；各角色同名“打击”“防御”保留独立标识。

| 角色清单 | 原版源池 | base 已声明 | 实际注册 | 本次定位到本地行为证据 | 尚未声明 | 仅多人卡（包含在源池中） |
| --- | --- | --- | --- | --- | --- | --- |
| [铁甲战士](ironclad.md) | 87 | 87 | 87 | 54 | 0 | 2 |
| [静默猎手](silent.md) | 88 | 25 | 24 | 24 | 63 | 2 |
| [储君](regent.md) | 88 | 9 | 9 | 9 | 79 | 2 |
| [亡灵契约师（亡灵召唤师）](necrobinder.md) | 88 | 14 | 14 | 14 | 74 | 2 |
| [故障机器人](defect.md) | 88 | 9 | 9 | 9 | 79 | 2 |
| **五角色合计** | **439** | **144** | **143** | **110** | **295** | **10** |
| [共享衍生卡附录](token.md) | 14 | 7 | 7 | 7 | 7 | 0 |

源分母来自各角色的 `GenerateAllCards()`，不是 `*-cards.spire` 的文件名或其中的卡数。铁甲池有 85 张非多人定义；另外四池各有 86 张。它们还包含先古牌等身份，不能直接把这个数量称为奖励池大小。衍生牌单列，不重复计入某个角色。

当前运行时共注册 176 张：五角色 143 张、Token 池 7 张、其他共享池 26 张。这个目录不替代 ENV 的 **577 项完整源卡分母**，也不覆盖所有无色、事件、状态、诅咒等共享牌。本地检查未定位到逐卡证据的 33 张已注册角色卡，仍明确标为“逐卡行为待核对”；这不表示检查一定不存在。

静默猎手的 `WellLaidPlans` 有声明但实际注册受阻：`WellLaidPlansPower` 无法链接。另一个未注册声明 `Toxic` 属于五角色之外的共享内容，不混入角色统计。

## 描述来源与读法

自然语言原文取自本地 [简体中文 cards.json](../../../../../sts2/localization/zhs/cards.json) 的 `<原版标识>.title` / `.description`，角色中文名取自 [characters.json](../../../../../sts2/localization/zhs/characters.json)。原版中文名是“亡灵契约师”，对应用户所说的“亡灵召唤师”。这次没有改写卡牌的效果措辞，也没有用 base DSL 反推原版效果。

本地化文本含动态模板。清单按原版卡类的 `CanonicalVars` 和 `OnUpgrade()` 填入静态数值，并处理 `IfUpgraded`、中文单复数和条件文本；例如打击普通 6、升级 9。原版在描述外追加的[关键字](../../../../../sts2/localization/zhs/card_keywords.json)单独写在描述末尾；升级只改变费用或关键字时，效果正文可以相同。不能升级的卡明确写“不适用”。

- `★` 表示星星；费用栏区分能量费与星星费。`X ★` 与能量 `X` 是两种不同支付资源。
- `[能量]` 代替原版能量图标，如 `0[能量]` 表示零能量耗能；`获得2点能量` 是对应数值的自然语言显示。
- `【动态伤害】`、`【动态铸造值】` 保留随战斗状态变化的数值，并在该行说明基础值与增长条件。共 14 张角色卡保留动态值，未假填为 0。`InCombat` / `IsTargeting` 的额外实时预览行在这份静态清单中不显示。
- `TheScythe` / `GeneticAlgorithm` 显示新牌初始伤害 13 / 格挡 1，已成长实例取保存值。君主之剑、小刀也注明能力修正后的目标/数值变化。

这里是静态卡牌参考，不是运行中每一帧的原版提示渲染。临时费用、附魔、力量、集中和成长实例等上下文修正需要运行时校对。完整原始模板、普通/升级变量、逐卡证据行和源文件 SHA-256 保存于[机器清单 catalog.json](../../../../../.audit/owner-five-character-card-catalog-20261012/catalog.json)，可以直接追查本次填值。

## 实现证据列表示什么

| 清单标记 | 本次检阅的含义 |
| --- | --- |
| 未声明 | 当前正式卡资源中没有该源卡的声明；不表示 DSL 已支持其机制 |
| 已声明；注册受阻 | 正式资源中有声明，解析接受，但未成功降级/注册；该行保留实际原因 |
| 已注册；逐卡行为待核对 | 实际 IR 规则集有该定义；本次没有定位到足以标注该牌的行为证据 |
| 已注册；有本地行为证据 | 在已通过的本地检查中定位到明确逐卡用例，或正式源的人工维护行为证据名单；只证明用例范围 |

注册结果来自当前构建的正式资源解析→降级→`CombatSlicePackage`→`RuleSetBuilder.Build`，[coverage.ndjson](../../../../../.audit/owner-five-character-card-catalog-20261012/coverage.ndjson)逐声明记录 Parsed/Lowered/Registered。共有 178 条声明、176 条注册、2 条受阻；五角色归属随后按原版池对齐。

行为证据复用同构建的 [794 项 Slice 通过日志](../../../../../.audit/owner-shared-run-20261012/slice-final.log)及 [SliceFormalSourceChecks 行为名单](../../../../../mod-the-spire/base-spire/BaseSpire.Slice.Tests/SliceFormalSourceChecks.cs)。名单是有来源的人工维护清单，不能理解成自动逐卡完整验收。普通、创建时升级、Armaments 升级等完整匹配行保存于机器清单；表格只链接首个证据位置。日志中单纯提到卡名的断言不计为该牌行为证据。

已有[严格回归](../../../../../.audit/owner-shared-run-20261012/regression-final/regression.json)为 16 项双侧通过和 1 项接线自检，主要覆盖 Ironclad 的 Strike/Defend/Bash/Headbutt 等机制与公开流程；没有据此把全部 143 张注册角色卡标为“原版一致”。四个角色的牌在受控 Ironclad 会话中可检查，并不表示它们已经具有自己的完整开局、资源和整局行为。

## 三个角色的公共机制缺口

本节是生成清单时的源码与机制缺口快照。随后同日星星/锻造/充能球、Doom/Poison/Osty基础交付见[阶段记录](../five-character-primitives-2026-10-12.md)及[机制契约](../../../../design/mod-the-spire/character-combat/spec.md)；不再将下表整体当作“尚无实现”的当前状态。剩余参与者与认领只查[主清单T06](../../../tofix/README.md#env-t06)。卡牌逐行统计和原快照保留。

目前这三个角色已注册的牌主要复用伤害、格挡、抽弃牌、X 费、普通能力和牌区操作。其下列专属领域仍未在活动 base 的正式作者模型/编译/内容中形成可复用实现。清单逐卡给出线索；线索列通过卡类及其直接引用的 Power 源码抽取，不是所有依赖的完整图，也不是把每个命中机制都判为缺失。

| 角色 / 机制 | 代表源卡 | 公共行为需要对齐什么 | 原版依据 |
| --- | --- | --- | --- |
| 储君：星星资源与费用 | Venerate、Alignment、AstralPulse、Stardust、BlackHole | 获得/扣除星星、可打出判定、独立星星 X 费、获得/花费触发；不能直接借能量替代 | [PlayerCmd](../../../../../sts2/src/Core/Commands/PlayerCmd.cs)、[Stardust](../../../../../sts2/src/Core/Models/Cards/Stardust.cs)、[BlackHolePower](../../../../../sts2/src/Core/Models/Powers/BlackHolePower.cs) |
| 储君：铸造与君主之剑 | Bulwark、BigBang、BeatIntoShape、SeekingEdge、Parry | 必要时生成剑、修改多个相关实例伤害、消耗区规则、AfterForge，以及剑的目标/格挡修正 | [ForgeCmd](../../../../../sts2/src/Core/Commands/ForgeCmd.cs)、[SovereignBlade](../../../../../sts2/src/Core/Models/Cards/SovereignBlade.cs) |
| 储君：仆从衍生牌与变化 | Begone、Charge、Guards | 复用选牌/变化，补衍生牌及升级传播；已注册 MinionSacrifice 不代表整个族完成，也不是奥斯提式独立生物 | [Begone](../../../../../sts2/src/Core/Models/Cards/Begone.cs)、[Charge](../../../../../sts2/src/Core/Models/Cards/Charge.cs)、[Guards](../../../../../sts2/src/Core/Models/Cards/Guards.cs)、[衍生附录](token.md) |
| 亡灵契约师：奥斯提与召唤 | Bodyguard、Afterlife、Unleash、Protector | 自有宠物身份、当前/最大生命、伤害来源、代受伤害、死亡/再召唤、召唤修正与触发；现有 summonEnemy 不等价 | [OstyCmd](../../../../../sts2/src/Core/Commands/OstyCmd.cs)、[Osty](../../../../../sts2/src/Core/Models/Monsters/Osty.cs)、[Unleash](../../../../../sts2/src/Core/Models/Cards/Unleash.cs) |
| 亡灵契约师：灾厄 | Fetch、EndOfDays、Countdown | 灾厄堆叠、生命阈值、阵营回合结算、同批死亡与 AfterDiedToDoom；不能只登记一个 Power 名字 | [DoomPower](../../../../../sts2/src/Core/Models/Powers/DoomPower.cs)、[EndOfDays](../../../../../sts2/src/Core/Models/Cards/EndOfDays.cs) |
| 亡灵契约师：灵魂与历史计数 | Reave、Dirge、SoulStorm | 衍生灵魂、升级传播、抽牌/消耗区与灵魂历史计数；可先复用现有生成/牌区/计数原语，缺实际内容和特定语义时再补 | [Soul](../../../../../sts2/src/Core/Models/Cards/Soul.cs)、[SoulStorm](../../../../../sts2/src/Core/Models/Cards/SoulStorm.cs)、[衍生附录](token.md) |
| 故障机器人：充能球领域 | Zap、Dualcast、Glacier、Darkness、Loop、Defragment | 有序槽位/容量、生成满槽激发、主动/被动触发、集中修正、闪电/冰霜/黑暗/等离子/玻璃的各自规则及随机目标 | [OrbCmd](../../../../../sts2/src/Core/Commands/OrbCmd.cs)、[OrbQueue](../../../../../sts2/src/Core/Entities/Orbs/OrbQueue.cs)、[充能球模型](../../../../../sts2/src/Core/Models/Orbs)、[FocusPower](../../../../../sts2/src/Core/Models/Powers/FocusPower.cs) |

共享原语应承载可复用操作及其顺序，角色卡再组合这些操作。星星、剑、奥斯提和充能球需要明确的领域状态、支付/目标语义及触发边界，不能靠逐卡特例绕过。这个表是缺口参考，未新增实现任务或调整现有排期。

## 本次取证与校验

提取脚本：[build_catalog.py](../../../../../.audit/owner-five-character-card-catalog-20261012/build_catalog.py)。来源是工作区内的冻结源码/中文资源，不代表实时游戏最新版本。源文件、当前资源、取证 DLL 和既有日志的摘要及检查结果见 [validation.json](../../../../../.audit/owner-five-character-card-catalog-20261012/validation.json)。

本次只生成 Markdown 和取证材料，没有修改 core/base 卡牌或公共原语，也没有派发任务。实际运行了正式注册覆盖入口、源池/本地化/行数/升级/动态值检查和文档检查；构建、规则套件及严格回归复用上述既有证据，未重复执行。新增清单校验结果与全仓文档检查的既有失败范围在主清单维护记录中注明。
