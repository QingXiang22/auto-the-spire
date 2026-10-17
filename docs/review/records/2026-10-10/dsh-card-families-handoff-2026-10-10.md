# ENV-T12 卡牌内容族交接（2026-10-10）

本交接记录 `ENV-T12-card-content-families-20261010` 在 DSH 会话 `session-70c7f4c9-c106-4fbc-9fe6-c28ceccd75bf` 的内容交付，供负责人复核和后续接续；不是负责人验收，也不把四角色整局环境标为完成。

用户已要求交接给其他 agent；本会话停止新增实现，自动目标已暂停。最后已验证内容为 family01–05：43 张新卡、129 项正式源检查退出 0。第六族只做源和接口核对，未认领、未新增 DSL 或检查。没有运行中的后台作业，没有提交、重置或清理工作区。后续应在同一共享工作区接续，勿以全仓 diff 认定本会话修改范围。

主池当前 533 条中有 134 条内容声明、399 条未声明；衍生池 44 条中有 5 条声明、39 条未声明。本会话 43 张新卡由 40 张主池卡和 3 张 Token 卡组成；这不代表其他已有声明都已经过行为核验。

## 接手入口

先读主清单 [`docs/review/tofix/README.md`](../../tofix/README.md) 的 ENV-T12 认领段，再读 [覆盖矩阵](../../../../.audit/dsh-card-families-2026-10-10/coverage-matrix.md) 和 [机制索引](../../../../.audit/dsh-card-families-2026-10-10/mechanism-index.txt)。接手后的第一步是核对当前工作区与正式内容加载状态，然后从矩阵中选择下一个尚未实现且不依赖新共享机制的 8～12 张族；当前只登记为待验证。

## 源分母与当前范围

冻结 `sts2/src/Core/Models/CardPools/*CardPool.cs` 的主池分母为 533：Ironclad 87、Silent 88、Defect 88、Necrobinder 88、Regent 88、Colorless 64、Status 12、Curse 18。Token/Event/Quest 是衍生范围，分别为 14/27/3，不计入角色池完成数。矩阵逐条保留源文件、正式声明、升级资格、多人限制和战斗生成资格；声明、解析/降级、正式注册、普通升级行为及独立对拍分开记录。

当前内容声明库存（不等于注册/行为完成）为：Ironclad 87、Silent 14、Defect 6、Necrobinder 10、Regent 3、Colorless 8、Status 6、Curse 0；衍生池 Token 4、Event 1、Quest 0。矩阵将声明、解析、降级、正式注册、普通/升级行为与独立对拍列分开；先前负责人内容在本会话尚未逐卡复核，不从声明推断注册。Ironclad 现有内容来自先前工作，本会话没有重写其既有牌；已有 `WellLaidPlans` 等跨角色内容需继续避免重复注册。

## 本次交付

- family01：StrikeSilent、DefendSilent、Slice、Deflect、Backflip、Dash、DaggerSpray、Adrenaline、StrikeDefect、DefendDefect，共 10 张。
- family02：StrikeNecrobinder、DefendNecrobinder、StrikeRegent、DefendRegent、Sow、Parse、Wisp、Backstab、BootSequence，共 9 张。
- family03：无色/共用可直接表达族 DramaticEntrance、Finesse、FlashOfSteel、MasterOfStrategy、Production、Prophesize、Fuel、Luminesce，共 8 张新牌。`GiantRock` 源上属于 Token，但已在 `ironclad-cards.spire` 正式存在，本次没有重复注册。
- family04：Neutralize、SuckerPunch、BeamCell、Assassinate、Scare、Defy、Putrefy、Shockwave，共 8 张。使用正式已链接 Weak/Vulnerable，攻击后施加减益；Defy 自身格挡后施加选定敌人 Weak；Scare 升级取消 Exhaust。普通/创建升级/Armaments 升级共 24 项通过。
- family05：Defile、SweepingBeam、Skim、UltimateStrike、UltimateDefend、Bury、SharedFate、Soul，共8张；普通/创建升级/Armaments升级24项通过。Defile池为Necrobinder，Soul池为Token；UltimateStrike/UltimateDefend保留Strike/Defend标签。Bury以4点能量、1000HP非致死目标隔离52→63伤害；SharedFate先自身Strength -2，再选定敌人 -2→-3。累计43张129项，作业 `pwsh-284` 退出0，首差无；独立对拍未执行。
- 所有新增内容采用 `using namespace base-spire;`、完整 `normal`/`upgraded`、池元数据、目标、关键词、具名 play 和 hooks 块。family01–05 的源检查覆盖普通、直接创建升级和实际 Armaments 升级。

## 实际修改文件

- [silent-cards.spire](../../../../tools/base-migration-baseline/BaseSpire/Cards/Content/silent-cards.spire)
- [defect-cards.spire](../../../../tools/base-migration-baseline/BaseSpire/Cards/Content/defect-cards.spire)
- [necrobinder-cards.spire](../../../../tools/base-migration-baseline/BaseSpire/Cards/Content/necrobinder-cards.spire)
- [regent-cards.spire](../../../../tools/base-migration-baseline/BaseSpire/Cards/Content/regent-cards.spire)
- [colorless-cards.spire](../../../../tools/base-migration-baseline/BaseSpire/Cards/Content/colorless-cards.spire)
- [RemainingCardFamiliesChecks.cs](../../../../tools/base-migration-baseline/BaseSpire.Slice.Tests/Cards/RemainingCardFamiliesChecks.cs)
- [Program.cs](../../../../tools/base-migration-baseline/BaseSpire.Slice.Tests/Program.cs)：新增 `--remaining-card-families-only` 和默认入口中的实际调用；新增检查先于旧 SliceChecks 执行，未改旧断言或身份 golden。
- [主清单](../../tofix/README.md)：仅本会话 ENV-T12 执行者认领段；本固定交接报告。
- [inventory.py](../../../../.audit/dsh-card-families-2026-10-10/inventory.py)、[matrix.py](../../../../.audit/dsh-card-families-2026-10-10/matrix.py)、[coverage-matrix.md](../../../../.audit/dsh-card-families-2026-10-10/coverage-matrix.md)

未修改 core、CombatSlicePackage、Effects、Orchestration、SinglePlayer、PowerRules、SpireCardSource、网络协议或身份 golden；没有新增 C# 内容 ID 白名单，没有重复写角色生成池调用。

## 验证证据

命令均在仓库根目录 `F:\Scylier-Project\active\auto-the-spire` 执行：

```text
dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests -- --remaining-card-families-only
```

最新族检查实际退出码 0；输出 `All 129 remaining card family checks passed (controlled formal source; no independent reference comparison).` 129 项是 43 张牌 × 3 模式（normal、created-upgraded、armaments-upgraded），作业 `pwsh-284`。首差无。之前 family04 作业 `pwsh-272` 为 105 项、退出 0。前一个作业 `pwsh-271` 退出 1，在 Footwork 首例构建时报 `InvalidOperationException: Sequence contains no matching element`；定位为敌人源里的 Dexterity 不在卡牌正式链接集合。已移除仅本会话新写的 Footwork/Prowess 草案，换成独立就绪的 Defy/Putrefy，未改共享加载器。构建过程中仍有既有警告：`ActMap.cs:42/60 CS8600`、`SpirePowerSource.cs:1216 CS8604`。早先中间检查因错误直读不存在的 `Card.Retain` 字段退出 1；已移除错误夹具访问，保留正式源检查，重跑通过。

family01 的关键修复包括使用 `draw = { amount = ... }`、`modifyEnergy`、攻击叶上的 `target = allEnemies`；这些均由正式运行时检查暴露并修正。family02/03 的牌均通过费用、目标、伤害/格挡、能量、抽牌、出牌堆检查；Parse 的 Ethereal 直接状态检查已通过。Retain 由 `CombatSlicePackage.cs` 回合结束编排读取 `NormalRetain/UpgradedRetain`，不是 Card 实体公开字段；当前只保留声明/解析和源证据，尚未单独完成回合结束行为检查。

未运行最终 solution、Engine、Slice、net 串行全量构建，也未运行独立参考客户端或 sim 双侧对拍；因此不报告独立一致性、公开出口或整局覆盖。

## 未支持机制与最小案例

- Power牌结算：最小源 [EternalArmor](../../../../sts2/src/Core/Models/Cards/EternalArmor.cs#L27) 施加PlatingPower 9→12，层数检查通过，但 `pwsh-277` 退出1，首差 `power card leaves combat piles`。当前Power牌仍在战斗牌区；需负责人修复公共结算并验证Plating回合开始减层/结束格挡。只删除本会话草案，没有用exhaust冒充Power移除，也未降低原检查断言。
- 无目标选择器：最小源 [Debris](../../../../sts2/src/Core/Models/Cards/Debris.cs#L14) cost1、Exhaust、不可升级、OnPlay确实返回Task.CompletedTask；`selectNone` 在正式降级被拒绝，`pwsh-280/281` 退出1，构建首差 `InvalidOperationException: Sequence contains no matching element`。加载源明确报 `normal.target 选择器 'selectNone' 未支持`（由TargetOf分支确定）；需无目标正式选择器接口，不改成self。草案已删除，[完整中间日志](../../../../.audit/dsh-card-families-2026-10-10/family05-check-utf8.log)保留。
- 选牌接口：最小源 [Acrobatics](../../../../sts2/src/Core/Models/Cards/Acrobatics.cs) draw3→4后discard1；Prepared draw/discard1→2；Hologram block3→5后discard→hand。当前ChooseCardStep只支持discard→draw/top、hand→exhaust、hand→none升级，拒绝 `形态未支持（只支持 from=discard/to=draw/top 与 from=hand/to=exhaust）`；需具名玩家选牌及draw/hand/discard移动、数量/类型过滤接口，不能改成随机或整手牌移动。SecretTechnique/SecretWeapon和ThinkingAhead同属待核对接口范围。
- 下回合资源时序：[ChargeBattery](../../../../sts2/src/Core/Models/Cards/ChargeBattery.cs#L30) block7→10后挂EnergyNextTurn1；[Delay](../../../../sts2/src/Core/Models/Cards/Delay.cs#L30) block11→13后挂EnergyNextTurn1→2；[EnergyNextTurnPower](../../../../sts2/src/Core/Models/Powers/EnergyNextTurnPower.cs#L18) 必须AfterEnergyReset给能量再移除。正式卡PowerResources未注册它，不能用当前回合modifyEnergy替代。
- Dexterity 正式卡牌链接阻断：源 [Footwork:23–31](../../../../sts2/src/Core/Models/Cards/Footwork.cs#L23) 单纯给自身施加 Dexterity 2→3；[Prowess:31–41](../../../../sts2/src/Core/Models/Cards/Prowess.cs#L31) 顺序施加 Strength/Dexterity 1→2。Dexterity 已在敌人源声明，但 `PowerResources()` 仅列 `*-powers.spire`，正式卡牌 links 因而不包含它；Footwork 在正式族检查无法降级进入受控牌组（构建首差见上）。需要负责人协调现有声明的资源归属/加载接口，再证明格挡增益时序；本会话不重复注册或改加载器。
- Silent：毒、Sly、弃牌/历史条件、WraithForm 等需要共享 Power、历史或生命周期接口；最小待阻断案例为 `FlickFlack`（Sly 全体攻击）、`Murder`（按本战斗抽牌历史动态伤害）、`WraithForm`（Power 生命周期）。不能降级成固定数值。
- Defect：球、Focus、球位、自动触发和成长机制需要资源及生命周期；最小案例为 Ball Lightning/Cold Snap/Defragment 类。
- Necrobinder：Osty/灵魂与生成/触发时序需要共享生物或资源接口；`Poke` 的 `Attack.FromOsty` 是最小例子。
- Regent：Stars、Forge 与依赖资源的计算需要共享资源接口；`CrescentSpear`（1 星基础费用加全战斗星牌动态伤害）和 `GuidingStar`（星费用/升级抽牌）是最小例子。
- Status/Curse：剩余牌需要精确状态/诅咒生命周期与正式池注册；当前没有用声明数量冒充行为完成。
- Token：`Fuel`、`Luminesce` 已做源内容声明和受控行为检查，但没有声称其生成路径完成；Token 不计入角色池完成数。

源代码证据和 481 行机制摘要保存在 [mechanism-index.txt](../../../../.audit/dsh-card-families-2026-10-10/mechanism-index.txt)，完整源行保存在 `inventory-source.json`/`source-digest.txt`。发现新共享机制时，接手者应先记录源位置、最小卡例、预期顺序、拒绝原因和所需接口，跳到其他独立就绪族。

## 接手时正在核对的下一族（未实现）

以下是第六族候选，尚未证明八张全部就绪；接手者应先验证，重新登记一个完整族后再写内容：

| 卡牌 | 冻结源预期 | 当前核对结果 |
|---|---|---|
| [MinionStrike](../../../../sts2/src/Core/Models/Cards/MinionStrike.cs#L14) | Token、0费、Exhaust、Strike/Minion 标签；6→9伤害后抽1 | 基础叶候选就绪，生成路径另记 |
| [MinionDiveBomb](../../../../sts2/src/Core/Models/Cards/MinionDiveBomb.cs#L15) | Token、0费、Exhaust、Minion 标签；13→16伤害 | 基础叶候选就绪，生成路径另记 |
| [MinionSacrifice](../../../../sts2/src/Core/Models/Cards/MinionSacrifice.cs#L15) | Token、0费、Exhaust、Minion 标签；8→11格挡 | 基础叶候选就绪，生成路径另记 |
| [Skewer](../../../../sts2/src/Core/Models/Cards/Skewer.cs) | X费；每次8→11伤害，次数为本次实际付X | `repeatForX` 已有；需零X/多X、费用与升级分辨证据 |
| [Expertise](../../../../sts2/src/Core/Models/Cards/Expertise.cs) | 1费；抽 max(0,6→7−当前手牌数) | hand-count 可读，但负数/抽牌上限语义仍需验证 |
| [Scrawl](../../../../sts2/src/Core/Models/Cards/Scrawl.cs) | 1费、Exhaust；抽10−当前手牌数；升级加Retain | 同上，且需要Retain回合结束证据 |
| [Finisher](../../../../sts2/src/Core/Models/Cards/Finisher.cs#L26) | 1费；每次6→8伤害，次数为本回合该玩家已完成Attack牌数 | `cardPlaysThisTurn { type=attack phase=finished }` 可映射；需确认不含当前牌且按拥有者计数 |
| [Restlessness](../../../../sts2/src/Core/Models/Cards/Restlessness.cs#L26) | 0费、Retain；没有其他手牌才逐张抽2→3，然后加能量2→3 | 只有hand-count与pileCountAtLeast线索；空手条件及逐张抽牌时序尚未核对 |

另有动态值候选暂不能按现有接口直接实现：[Flechettes](../../../../sts2/src/Core/Models/Cards/Flechettes.cs) 要数手牌Skill，而 [CountSourceOf](../../../../tools/base-migration-baseline/BaseSpire/Cards/SpireCardSource.cs#L477) 只接受 exhaust、hand 或 hand/attack；[MindBlast](../../../../sts2/src/Core/Models/Cards/MindBlast.cs) 要数draw，当前上述分支不接受draw；[DoubleEnergy](../../../../sts2/src/Core/Models/Cards/DoubleEnergy.cs) 要读取支付费用后的当前energy，`energyX` 只是已付X，不可替代。未为这些候选创建假声明。

[SourceEffectPrograms](../../../../tools/base-migration-baseline/BaseSpire/Cards/Effects/SourceEffectPrograms.cs#L114) 中 `repeatForCount` 会先快照次数；`withCount` 有词法快照。不得为了凑足族人数越过接口限制。[Fisticuffs](../../../../sts2/src/Core/Models/Cards/Fisticuffs.cs#L32) 的格挡依赖攻击结果 TotalDamage+OverkillDamage；[GoldAxe](../../../../sts2/src/Core/Models/Cards/GoldAxe.cs#L21) 依赖全战斗已完成出牌数；[GangUp](../../../../sts2/src/Core/Models/Cards/GangUp.cs#L18) 是多人专用且依赖其他玩家攻击命中历史；[Shiv](../../../../sts2/src/Core/Models/Cards/Shiv.cs#L23) 目标和重复次数受Power影响，不能直接当固定4伤害衍生牌。

## 接续操作与工具注意事项

1. 从 `git status --short` 开始；当前全仓存在大量其他负责人的修改/未跟踪文件。不要 reset、clean、提交或覆盖他人文件；只改用户列出的内容源、新族检查、一次性入口、综合矩阵与本认领段。
2. 读 [总入口](../../../README.md)、[主清单](../../tofix/README.md) ENV-T05/T06/T12、相关 AGENTS、[基础开发指南](../../../design/mod-the-spire/base-development-guide.md) 和 [DSL指南](../../../dsl/0.0.1/dsl-usage.md)；先核对已交付ID，登记新会话和一个完整族。不要从历史报告重新做已交付牌。
3. 库存和矩阵可依次运行 `python .audit/dsh-card-families-2026-10-10/inventory.py`、`python .audit/dsh-card-families-2026-10-10/matrix.py`。矩阵脚本将显式families映射当作本会话已通过证据；新增族应先实际通过检查，再更新映射，不能仅靠生成矩阵将行为标pass。
4. `family01.py`、`family04.py`、`family05.py` 等早期生成脚本包含修正前版本或已删除受阻草案，**不要直接重跑**。以当前正式DSL和 [检查预期](../../../../tools/base-migration-baseline/BaseSpire.Slice.Tests/Cards/RemainingCardFamiliesChecks.cs) 为交付事实；audit脚本不是正式源。
5. 串行使用上述专用检查入口；同工作区不得并发写obj/bin。同ID已存在正式包时用正式族检查，避免多文件探针冲突误判语义。初次工具项目 `dotnet build tools/base-spire-dev/BaseSpire.Dev.csproj --nologo` 退出0，但未完成最终全量门禁。
6. 最近文档检查 `python tools/doc-check/check_docs.py`（`pwsh-287`）退出0，扫描341个Markdown文件、0问题。新增内容可能改变公开构建身份，不能更新固定Act1夹具或降低身份门槛；交负责人审阅重录。

本报告及覆盖账本包含源预期和受控执行证据；不能把参考源阅读当作独立参考运行。要称为完成“所有现有机制可表达卡”，仍须逐条筛查399条主池未声明项和39条衍生未声明项，对不能实现的条目补齐最小源/时序/拒绝/接口，而非按角色资源类别笼统判全池受阻。

## 仍未执行与验收边界

- 继续补齐矩阵中所有尚未实现的 Silent、Defect、Necrobinder、Regent、Colorless、Status、Curse 条目；当前没有缩减原池分母。
- 为 retain、innate、exhaust、ethereal 等时序补充独立可区分的回合/进入战斗证据。
- 运行必要的串行 solution/Engine/Slice/net 构建，以及内容加载、角色池和公开出口检查。
- 执行独立参考侧对拍；formal-source check 不能替代双侧验证。
- 负责人复核并决定主清单状态、整角色开局/多幕/胜负证据和是否纳入正式验收。本交接保持待验证。
