# ENV-T12 解阻卡内容交接（待验证）

任务：ENV-T12-unblocked-card-families-20261010；执行会话：session-f9cd8a61-d601-461f-9a07-d845a82c8e54。新DSH会话，未恢复此前已暂停goal。本地检查通过不关闭T12父项；负责人Codex独立复核及集成后决定接收。完整577项源分母与单人资格保留在[综合矩阵](../../../../.audit/dsh-card-boundaries-content-2026-10-10/coverage-matrix.md)，不声称全部卡或其余四角色完整环境完成。

## 交付能力与原生依据

四张卡复用公共包已解阻的Power结果None、真实空play/selectNone、共享Dexterity资源；没有新增公共机制或重复定义Power。正式受控Deck测试不依赖全包每张一份的数量。

| 卡 | 原生源与池 | 普通 / 升级 | 独立后续判别 |
|---|---|---|---|
| Footwork | [Footwork.cs](../../../../sts2/src/Core/Models/Cards/Footwork.cs#L14)、[SilentCardPool](../../../../sts2/src/Core/Models/CardPools/SilentCardPool.cs#L66) | cost1；Dexterity2 / 3；Power None | DefendSilent增加格挡，跨两回合继续生效；无瞬时格挡 |
| Prowess | [Prowess.cs](../../../../sts2/src/Core/Models/Cards/Prowess.cs#L20)、[ColorlessCardPool](../../../../sts2/src/Core/Models/CardPools/ColorlessCardPool.cs#L70) | cost1；Strength与Dexterity各1 / 2；Power None | StrikeSilent6+1/2伤害与DefendSilent5+1/2格挡分别判别 |
| EternalArmor | [EternalArmor.cs](../../../../sts2/src/Core/Models/Cards/EternalArmor.cs#L20)、[ColorlessCardPool](../../../../sts2/src/Core/Models/CardPools/ColorlessCardPool.cs#L43)、[PlatingPower](../../../../sts2/src/Core/Models/Powers/PlatingPower.cs#L61) | cost3；Plating9 / 12；Power None | 下一回合8/11层，再7/10与6/9；与未打出的独立对照比较回合末格挡减伤 |
| Debris | [Debris.cs](../../../../sts2/src/Core/Models/Cards/Debris.cs#L10)、[StatusCardPool](../../../../sts2/src/Core/Models/CardPools/StatusCardPool.cs#L25) | cost1；Status；Exhaust；不可升级；真正空play/selectNone | 付款、PlayedThisTurn记录、无选择决策、真实Exhaust触发FeelNoPain；Armaments不能升级；未打出正常弃牌而非Ethereal |

三个Power分别执行normal、created-upgraded、armaments-upgraded；Debris只执行真实普通/不可升级/关键词时序，不虚报三种升级模式。预期值来自上述原生源的独立常数，尚未运行原生/client双端可执行比较。

## 文件清单

- 卡声明：[silent-cards.spire](../../../../tools/base-migration-baseline/BaseSpire/Cards/Content/silent-cards.spire)、[colorless-cards.spire](../../../../tools/base-migration-baseline/BaseSpire/Cards/Content/colorless-cards.spire)、[status-cards.spire](../../../../tools/base-migration-baseline/BaseSpire/Cards/Content/status-cards.spire)。跨文件复用现有Dexterity/Strength/PlatingPower。
- 数值检查：[RemainingCardFamiliesChecks.cs](../../../../tools/base-migration-baseline/BaseSpire.Slice.Tests/Cards/RemainingCardFamiliesChecks.cs)，仅追加四张Case与明确ResultNone字段，旧断言未放宽。
- 独立收益/时序和真实分层证据：[UnblockedCardFamiliesChecks.cs](../../../../tools/base-migration-baseline/BaseSpire.Slice.Tests/Cards/UnblockedCardFamiliesChecks.cs)；最小入口：[Program.cs](../../../../tools/base-migration-baseline/BaseSpire.Slice.Tests/Program.cs)。
- 综合证据：[audit目录](../../../../.audit/dsh-card-boundaries-content-2026-10-10/coverage-matrix.md)；只维护主清单本次执行者认领段。

## 实际命令与结果

以下路径均相对工作区；使用实际DLL，串行构建/测试，无仍运行Host或测试持有DLL时重建。

| 命令 | 退出码 | 日志与结果 |
|---|---:|---|
| `dotnet build mod-the-spire/base-spire/BaseSpire.Slice.Tests/BaseSpire.Slice.Tests.csproj` | 0 | [最终四卡构建](../../../../.audit/dsh-card-boundaries-content-2026-10-10/build-unblocked01-final.log) |
| `dotnet mod-the-spire/base-spire/BaseSpire.Slice.Tests/bin/Debug/net9.0/BaseSpire.Slice.Tests.dll --remaining-card-families-only --card footwork` | 0 | [footwork](../../../../.audit/dsh-card-boundaries-content-2026-10-10/card-footwork.log)，3模式 |
| 同DLL `--remaining-card-families-only --card prowess` | 0 | [prowess](../../../../.audit/dsh-card-boundaries-content-2026-10-10/card-prowess.log)，3模式 |
| 同DLL `--remaining-card-families-only --card eternal-armor` | 0 | [eternal-armor](../../../../.audit/dsh-card-boundaries-content-2026-10-10/card-eternal-armor.log)，3模式 |
| 同DLL `--remaining-card-families-only --card debris` | 0 | [debris](../../../../.audit/dsh-card-boundaries-content-2026-10-10/card-debris.log)，普通1项 |
| 同DLL `--unblocked-card-families-only` | 0 | [独立收益最终日志](../../../../.audit/dsh-card-boundaries-content-2026-10-10/unblocked01-benefits-final.log)，11项；先执行此入口，再执行完整本族 |
| 同DLL `--remaining-card-families-only` | 0 | [完整本族](../../../../.audit/dsh-card-boundaries-content-2026-10-10/remaining-unblocked01-full.log)，139项（旧43张129项+新四张10项） |
| 最终构建后的同DLL `--unblocked-card-coverage` | 0 | [最终构建](../../../../.audit/dsh-card-boundaries-content-2026-10-10/build-slice-final.log)、[真实分层日志](../../../../.audit/dsh-card-boundaries-content-2026-10-10/runtime-coverage.log)，144声明/解析、142降级/注册，包含并发负责人内容；显式Wound Deck链接IR，逐声明打印解析/降级/正式定义注册，不将声明数量当行为通过 |

过程中两个测试夹具错误已修复：HP1000超过Player上限999，改为900；Card实例没有Exhaust字段，改为正式定义NormalExhaustsSelf/UpgradedExhaustsSelf及实际消耗时序。初次失败日志保留，最终成功依据以上最终日志。

## 后续完整机制包：Skewer

四卡局部检查与本交接已完成后继续Skewer。原生[Skewer.cs:16–37](../../../../sts2/src/Core/Models/Cards/Skewer.cs#L16)与[SilentCardPool:97](../../../../sts2/src/Core/Models/CardPools/SilentCardPool.cs#L97)：X费用、AnyEnemy、每击8→11、已付X次攻击、无Exhaust。声明使用现有repeatForX，未新增公共机制。

新增[XCardFamilyChecks.cs](../../../../tools/base-migration-baseline/BaseSpire.Slice.Tests/Cards/XCardFamilyChecks.cs)，三种模式分别运行X=0/1/3（共9种数值场景，按模式聚合为3项检查），验证实际支付、一次目标决策、仅选中敌人受击、弃牌结果和X=0记录。另三种模式的独立逐击检查使用Strength+2、Weak逐击取整、目标Block5：普通每击floor((8+2)×0.75)=7，三击扣除Block后HP损失16；升级每击floor((11+2)×0.75)=9，损失22，判别逐击计算与一击乘X的区别。

实际执行：`dotnet build mod-the-spire/base-spire/BaseSpire.Slice.Tests/BaseSpire.Slice.Tests.csproj` exit0（[构建日志](../../../../.audit/dsh-card-boundaries-content-2026-10-10/build-skewer.log)）；同实际DLL `--x-card-family-only --benefits-only` exit0 / 3项（[逐击收益](../../../../.audit/dsh-card-boundaries-content-2026-10-10/skewer-benefits.log)），先于同DLL `--x-card-family-only` exit0 / 3项、内部9种X场景（[完整族](../../../../.audit/dsh-card-boundaries-content-2026-10-10/skewer-values.log)）。最终默认Slice运行也实际先执行了四卡139+11以及Skewer3+3，全部成功输出，随后才进入旧检查失败。

## 最终串行集成与首差

生产内容稳定后实际运行：

| 命令 | 退出码 | 日志 / 结论 |
|---|---:|---|
| `dotnet build core-the-spire/Core.TheSpire.Engine.Tests/Core.TheSpire.Engine.Tests.csproj` | 0 | [Engine构建](../../../../.audit/dsh-card-boundaries-content-2026-10-10/build-engine-final.log) |
| `dotnet core-the-spire/Core.TheSpire.Engine.Tests/bin/Debug/net9.0/Core.TheSpire.Engine.Tests.dll` | 0 | [Engine](../../../../.audit/dsh-card-boundaries-content-2026-10-10/engine-final.log)，95/95 |
| `dotnet build mod-the-spire/base-spire/BaseSpire.Slice.Tests/BaseSpire.Slice.Tests.csproj` | 0 | [Slice构建](../../../../.audit/dsh-card-boundaries-content-2026-10-10/build-slice-final.log) |
| `dotnet mod-the-spire/base-spire/BaseSpire.Slice.Tests/bin/Debug/net9.0/BaseSpire.Slice.Tests.dll` | -532462766（Windows进程3762504530） | [Slice最终](../../../../.audit/dsh-card-boundaries-content-2026-10-10/slice-final.log)，新检查156项成功输出后，旧正式源固定数量断言失败 |

首差原文：`W-06 正式源：声明/解析/降级/未支持**四态分开**，降级集确定 [真实]: 状态牌源声明 2 张: expected '2', got '3'`，栈定位[SliceFormalSourceChecks.cs:104](../../../../tools/base-migration-baseline/BaseSpire.Slice.Tests/Cards/SliceFormalSourceChecks.cs#L104)。Debris合法新增让旧Status资源固定数量从2变3；没有编辑旧Slice检查或放宽断言。完整Slice在这处提前结束，其后检查（含公开固定身份）未执行，不能把未到达的身份门槛说成实际身份首差。数量基线及随内容变化的公开身份迁移均交Codex集成。未跑全solution/net/公开Act1或严格双端/client环境。

## 剩余机制与下一可继续族

BandageUp在当前权威Cards及Colorless池中没有源卡，不以旧游戏印象创造声明。NotYet的heal10/13已有Ironclad声明，未重复实现。已源核对的下一独立就绪候选是[LegSweep.cs:20–42](../../../../sts2/src/Core/Models/Cards/LegSweep.cs#L20)：cost2、先给自己Block11→14再给选中敌人Weak2→3，现有gainBlock/applyPower可以组合，后续包需真实池核对和敌人攻击收益/Weak时序判别。本文不把候选记成已交付。

需负责人Codex处理的最小源卡与具体接口边界：

| 最小原生卡 | 具体公共依赖 |
|---|---|
| [Acrobatics.cs:21](../../../../sts2/src/Core/Models/Cards/Acrobatics.cs#L21) | draw3→4之后从实际手牌选择1张Discard，需选择候选、最小/最大数、续接与触发，不能以随机弃牌代替 |
| [Outmaneuver.cs:19](../../../../sts2/src/Core/Models/Cards/Outmaneuver.cs#L19) / [EnergyNextTurnPower.cs:18](../../../../sts2/src/Core/Models/Powers/EnergyNextTurnPower.cs#L18) | cost1 Event，施加下回合能量2→3；AfterEnergyReset后GainEnergy并移除，仅增即时能量不等价 |
| [DoubleEnergy.cs:16](../../../../sts2/src/Core/Models/Cards/DoubleEnergy.cs#L16) | cost1→0、Exhaust；读取付款后的实时Energy并再GainEnergy同量，需运行期资源读取，不能写死+N |
| [AscendersBane.cs:10](../../../../sts2/src/Core/Models/Cards/AscendersBane.cs#L10) | cost-1、Curse类型/稀有度、不可升级、Eternal/Unplayable/Ethereal；负费用及真实Curse与Eternal语义不能改为Status/cost0 |
| [Zap.cs:19](../../../../sts2/src/Core/Models/Cards/Zap.cs#L19) | cost1→0、Channel LightningOrb，需Orb槽、被动/激发与Focus时序，不能即时伤害替代 |
| [DeadlyPoison.cs:20](../../../../sts2/src/Core/Models/Cards/DeadlyPoison.cs#L20) / [PoisonPower.cs:55](../../../../sts2/src/Core/Models/Powers/PoisonPower.cs#L55) | cost1、Poison5→7；所影响方回合开始Unblockable/Unpowered伤害、存活后减层，并保留Accelerant触发次数依赖 |
| [Unleash.cs:20](../../../../sts2/src/Core/Models/Cards/Unleash.cs#L20) | cost1、OstyAttack标签，基础6→9加活Osty当前HP；Osty缺失不攻击，真实攻击来源Osty与召唤体状态不可借玩家攻击冒充 |
| [Venerate.cs:12](../../../../sts2/src/Core/Models/Cards/Venerate.cs#L12) | cost1、Stars2→3，需独立Stars资源与消费，不可并入Energy |

其他已读源边界：ThrummingHatchet/Bolas依赖BeforeHandDraw结合上一玩家回合卡实例历史回手；Rend需要目标非临时debuff计数；Fisticuffs需要本次实际伤害含overkill作为后续格挡量；Splash依赖其他角色攻击生成、选择和本回合freecost；Fasten需要Defend-tag限定的独立Power；Lift多人专属，单人资格排除但保留源分母。公共机制不得在本内容线扩范围。

## 文档与证据自检

实际`python tools/doc-check/check_docs.py` exit0（[日志](../../../../.audit/dsh-card-boundaries-content-2026-10-10/doc-check-final.log)，344份Markdown、0问题）；`git diff --check` exit0（[日志](../../../../.audit/dsh-card-boundaries-content-2026-10-10/diff-check-final.log)）。共享树包含大量开工前既有未跟踪/修改文件，未清理、暂存或提交；对本包8个卡DSL/检查/交接文件另做实际尾空白检查，覆盖未跟踪文件（[结果](../../../../.audit/dsh-card-boundaries-content-2026-10-10/scoped-whitespace-final.log)）。综合矩阵由`python .audit/dsh-card-boundaries-content-2026-10-10/build-matrix.py` exit0生成（[日志](../../../../.audit/dsh-card-boundaries-content-2026-10-10/matrix-generation.log)）：实际48张数值证据（旧43+本次5）及5张独立源值收益证据；随后显式UTF-8读取JSON核查仍为577行（[结果](../../../../.audit/dsh-card-boundaries-content-2026-10-10/ledger-final.log)）。这些是执行者自检，负责人独立验收仍待进行。

## 负责人接手步骤

先读综合矩阵和四卡/Skewer最终收益日志，源核对五张及结果区；重跑`--unblocked-card-families-only`再`--remaining-card-families-only`，Skewer重跑`--x-card-family-only --benefits-only`再`--x-card-family-only`。本次必要Engine/Slice实际集成已记录在上文；负责人先决定并处理合法Status新声明带来的旧固定数量基线迁移，再继续后续公开身份及全部Slice门槛，不放宽身份断言。下一内容候选LegSweep可沿现有原语另开完整包；公共依赖按上表拆派。本交接和T12父项均待验证，未形成独立读者验收结论。
