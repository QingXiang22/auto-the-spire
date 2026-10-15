# OriMod 核心内容的可移植性评估（2026-10-01）

> 历史证据（2026-10-03整理）：正文保留原时点结果，历史“下一步”不再作为当前派单指令。本报告归属暂不派单的后续研究范围，未结项转入[当前清单](../../tofix/README.md)；移入records不表示修复或验收通过。逐份归属见[记录索引](../README.md)。

> 评估对象：`reference/orimod/src`（创艺工坊发布版 OriMod `1.0.0` 的反编译源码）与当前 `core-the-spire` / `mod-the-spire` 的 DSL 及引擎能力。
>
> 回答两个问题：**（1）OriMod 的核心源码有没有混淆；（2）当前 DSL 规则是否完善到能把 OriMod 的核心内容注册进 mod。**
>
> 结论均为可复核的实测判断，测量命令与计数方式写在正文中。本文只做评估，未改动生产代码。

---

## 0. 结论摘要

**（1）没有混淆。** OriMod 与游戏本体 `sts2.dll` 都是普通 Release 程序集：类型名、方法名、字段名、字符串字面量全部可读，没有混淆器特征。反编译产物重新编译后类型集合与原始程序集一致。但"没有混淆"不等于"拿回原始源码"——局部变量名、编译器生成编号、文件划分和注释不可恢复，`OriMod.pck` 里的资源也未解包。

**（2）在旧 DSL 后端上不够；在通用引擎上属于设计内的常规工作。** 本报告初稿只看了旧 DSL 后端（`Content.cs` / `State.cs` / `Effects.cs` / `Environment.cs`），据此得出的"要动内核才能承载 OriMod"的结论**已在 §0.1 更正**。要点是：core-the-spire 现在有两套后端，旧 DSL 后端确实有三处模型层面的硬缺口，而这些缺口在同期落地的通用引擎（`Engine/`）中**全部不成立**。

| 差距 | 旧 DSL 后端（base-spire 当前使用） | OriMod 需要 |
| --- | --- | --- |
| 自定义状态的作用域 | 会话/整局级的 `int` 键值表（`state` / `ReadModInt`） | **逐实体**数值：每名敌人的高度、每名玩家的浮空/动量、每个场地实例的高度区间与存量 |
| 战斗中的对象 | 玩家 + 敌人 + Power + 牌 | 额外的**世界对象**（29 种场地/平台实例，带独立 ID、层高、范围、排序） |
| 触发面 | 6 个卡牌钩子、11 个 Power 触发器、10 个 `IRuleHook` 方法 | 对游戏内部方法的**开放拦截**：123 处 Harmony 补丁，覆盖 `Hook.*`、`CombatState.AddPlayer`、`CreatureCmd.Damage`、`RunState.FromSerializable` 等 |

> ⚠️ **本节的"差距"针对旧 DSL 后端。** core-the-spire 现已存在第二套后端（`Engine/`，通用规则引擎），上面这三条差距在那套后端里**全部不成立**。判断 OriMod 内容该怎么落地之前，先读下面的 §0.1。

### 0.1 重要更正：core-the-spire 现在有两套后端

本报告 §3 的能力面、以及本文初稿的"DSL 不够用"结论，描述的都是**旧 DSL 后端**（`Content.cs` / `State.cs` / `Effects.cs` / `Environment.cs` + `.spire`）。工作区里同时存在一套**通用规则引擎** `core-the-spire/Core.TheSpire/Engine/`，它已经实现了本报告认为缺失的三个扩展点：

| 本报告说"缺口" | 通用引擎的实际状态 | 证据 |
| --- | --- | --- |
| 无法注册新的外部动作 | ✅ `EnginePackageBuilder.Action(name)`；`ActionSymbol` 带 `ParticipantRole` / `Guard` / `CandidateQuery` / `Choices` / `Body`；`LegalActions` 遍历 `RuleSet.Actions` 通用枚举，**没有逐动作硬编码分支** | [Schema.cs:321](../../../../core-the-spire/Core.TheSpire/Engine/Schema.cs#L321)、[PackageBuilder.cs:95](../../../../core-the-spire/Core.TheSpire/Engine/PackageBuilder.cs#L95)、[Scheduler.cs:204-234](../../../../core-the-spire/Core.TheSpire/Engine/Scheduler.cs#L204-L234) |
| 无法表达逐实体自定义状态 | ✅ 实体类型可有字段；`ExtensibleFields = true` 时**后加载的包可以追加字段**（声明在包 `Extends` 里），链接期合并 | [Schema.cs:64-98](../../../../core-the-spire/Core.TheSpire/Engine/Schema.cs#L64-L98)、[RuleSet.cs:229-249](../../../../core-the-spire/Core.TheSpire/Engine/RuleSet.cs#L229-L249) |
| 触发面是固定方法列表 | ✅ 包可声明事件与订阅，带优先级、重检谓词、作用域绑定 | [Schema.cs:221-249](../../../../core-the-spire/Core.TheSpire/Engine/Schema.cs#L221-L249)、[Schema.cs:370-391](../../../../core-the-spire/Core.TheSpire/Engine/Schema.cs#L370-L391) |
| —（本报告未列）目标查询无法排序/筛选 | ✅ `Query` 已支持 `Where` / `Sort` / `Take` / `Skip` / `First` / `Count` / `Exists` / `Aggregate`；算术含 `Minimum` / `Maximum` | [PackageBuilder.cs:422-436](../../../../core-the-spire/Core.TheSpire/Engine/PackageBuilder.cs#L422-L436)、[PackageBuilder.cs:357-359](../../../../core-the-spire/Core.TheSpire/Engine/PackageBuilder.cs#L357-L359) |

而且这套引擎**不是纸面设计**：`Engine` 检查 32/32、`base-spire` 切片检查 21/21 通过，其中已经有一条检查直接验证"第二个包按 qualified id 扩展 `base-spire` 的 `Player` 类型并追加字段"[SliceChecks.cs:150-162](../../../../mod-the-spire/base-spire/BaseSpire.Slice.Tests/SliceChecks.cs#L150-L162)，而 `Monster` / `Player` / `Power` 都已经标了 `ExtensibleFields`[CombatSlicePackage.cs:40-76](../../../../mod-the-spire/base-spire/BaseSpire/Slice/CombatSlicePackage.cs#L40-L76)。

**因此本文的结论需要改写为：**

- 把 OriMod 的高度/技能/场地机制注册成内容，**在通用引擎上是设计内的常规工作**，不是内核改造；
- 真正的门槛转移到**别的**问题上：内容要在哪套后端落地（两套并存，base-spire 的 122 张卡在旧后端）、通用引擎的作者前端（`.spire` 目前只喂旧后端）、以及通用引擎的两处已知表达力缺口（见 §0.2）。

### 0.2 通用引擎的两处已知缺口（对本需求有直接影响）

来自同期审计 [core-the-spire-completeness-audit-2026-10-01.md](core-the-spire-completeness-audit-2026-10-01.md)：

- **动作只能有一个候选域**（§3 缺口 A）。`ActionSymbol.CandidateParameter` / `CandidateQuery` 是单数。对高度/技能的影响：一个能力若要"先选模式再选目标"这类**两个参数域**，不能直接声明，只能把第二个域放进过程体用 `RequestDecision` 表达。单域（如"选一个敌人"）不受影响。
- **没有会话级整体快照/恢复**（§3 缺口 B）。存储层有按指令原子回滚与 `StateHash()`，随机服务有 `Capture`/`Restore`，但 `SessionState` 本身没有快照。训练侧的 fork/重放需要它。

另有一条与高度伤害直接相关的待决项：K-05 记录"切片走整数运算、未使用 `NumericProfile`"，且旧后端"双重 `Floor`"与 sts2"末端单次截断"的取整差异**尚未选定**（审计 §4）。距离衰减是乘法阶段，取整点会影响结果，落地前必须先定这条。

**耦合度的意义**（与后端选择无关，这部分结论不受更正影响）：角色记录、卡池、遗物/药水数据可以直接搬；**122 张具体卡牌里 64 张（52%）直接引用 mod 自有系统**，另有 35 张只引用 mod 自有 Power/关键字，只有 23 张两者都不碰，而被引用的那些 Power 本身又挂在自有系统上（例如 `AfterimagePower` 实现 `IAloftChangedListener`）。也就是说：**无论用哪套后端，"先做高度/能力底座、再搬卡牌"的顺序都不变**，能单独搬的卡牌只有约五分之一。

---

## 1. 混淆判定

### 1.1 判定方法与结果

| 检查项 | 命令/方法 | 结果 |
| --- | --- | --- |
| 类型名可读性 | `ilspycmd -l c reference/orimod/OriMod.dll` | 813 个类型全部是 `Ori.Cards.Feathers`、`AloftSystem` 这类有意义的名字 |
| 可疑短名 | 匹配 1–2 字符类名 | 0 个 |
| 非 ASCII / 控制字符标识符 | 正则扫描全部 570 个 `.cs` | 0 处 |
| 混淆器特征 | 扫描 `ConfusedByAttribute`、`Dotfuscator`、`Eazfuscator`、`Babel`、`SmartAssembly`、`DecryptString` 等 | 0 处 |
| 字符串加密 | 直接阅读源码中的字符串字面量 | 全部为明文，例如 `"Invalid Ori learning plan; refusing to replace saved assignments."` |
| 反编译失败点 | 扫描 `ISSUE:` / "unable to decompile" | 0 处 |
| 往返一致性 | 反编译源码 `-c Release` 重编译后比对类型集合 | 813 vs 813，对称差集 52 项全是编译器生成名 |

核对细节与原始清单见 [`tools/orimod-decompile/verify/`](../../../../tools/orimod-decompile/verify/README.md)。

### 1.2 顺带确认游戏本体

为排除"核心源码"指的是游戏本体，也检查了 `data_sts2_windows_x86_64\sts2.dll`：6641 个类型，抽样全部可读（`MegaCrit.Sts2.Core.*`、`NDefectVfx`、Roslyn 生成的 `RegexGenerator_*` 等），1–2 字符类名 0 个。**游戏本体同样没有混淆。**

### 1.3 边界（不要过度解读）

- **局部变量名是反编译器重造的**（`num`、`num2`、`text`、`array`），字段/方法/类型名才是原始的。
- **编译器降级痕迹被固化**：`async` 状态机、闭包类、`<>z__ReadOnly*` 集合辅助类型、`foreach` 展开成 `for`。
- **Godot 源生成器产物混在源码里**：`MethodName` / `InvokeGodotClassMethod` / `GetGodotMethodList` 等成员是编译期生成的，反编译后变成手写代码。
- **`OriMod.pck`（75 MB）没有解包**：场景、贴图、音频、本地化表都还在包里。本次只做 C# 源码。

---

## 2. OriMod 内容清单（实测）

对 `reference/orimod/src/Ori/**` 按目录逐文件统计具体类（排除 `abstract class`）：

| 内容域 | 文件数 | 具体类数 | 说明 |
| --- | --- | --- | --- |
| 卡牌 `Ori/Cards` | 131 | **122** | 另 6 个抽象基类 + 3 个非卡牌文件（`OriCardTags`、`OriKeywords`、`OriPlacementHelper`） |
| Power `Ori/Powers` | 38 | **32** | 另 6 个是面板渲染辅助类型 |
| 遗物 `Ori/Relics` | 23 | **19** | 另 2 个抽象基类 + 2 个网络 Action |
| 能力 `Ori/Abilities` | 96 | **63** | 玩法系统，不是内容表格意义上的"能力" |
| 场地 `Ori/Placements` | 51 | **29** | 玩法系统 |
| 鬼火 `Ori/Wisps` | 11 | **8** | 玩法系统 |
| 怪物 `Ori/Monsters` | 5 | **0** | 只有 1 个抽象基类 + 4 个辅助类型，**没有具体怪物** |
| 药水 | 1 | **0** | 只有空的 `OriPotionPool`，**没有具体药水** |
| 事件 | — | **1** | `TemperamentEvent`（`CustomEventModel`） |
| 角色 | 1 | **1** | `OriCharacter` |

角色数据（[OriCharacter.cs:25-111](../../../../reference/orimod/src/Ori/Characters/OriCharacter.cs#L25-L111)）：`PlaceholderCharacterModel`，起始 65 生命，起始牌组 4×`StrikeOri` + 4×`DefendOri` + `Lightness` + `AncientForce`，起始遗物 `KurosFeather`，三个池分别是 `OriCardPool` / `OriRelicPool` / `OriPotionPool`。

**注意：OriMod 没有具体怪物和具体药水。** 它的内容重心是 1 个角色 + 122 张牌 + 32 个 Power + 19 个遗物，以及一整套自建玩法层（能力 / 浮空 / 场地 / 鬼火）。

---

## 3. 引擎与 DSL 当前能力面（实测）

> ⚠️ **本节只描述旧 DSL 后端**（`.spire` + `Content.cs`/`State.cs`/`Effects.cs`/`Environment.cs`），也就是 base-spire 122 张卡当前走的那条路。通用引擎（`Engine/`）的能力面见 §0.1 对照表。

### 3.1 内容声明种类

| 种类 | 入口 | 状态 |
| --- | --- | --- |
| `card` / `cardPool` | [SpireCombatDslCompiler.cs:19](https://github.com/QingXiang22/auto-the-spire/blob/0aca7d1a26f3ad42d5b8657bc794cd0b12f85ba8/archive/legacy-sts/core-the-spire/Core.TheSpire.Legacy/Legacy/SpireCombatDslCompiler.cs#L19) | DSL |
| `potion` | [SpireCombatDslCompiler.cs:54](https://github.com/QingXiang22/auto-the-spire/blob/0aca7d1a26f3ad42d5b8657bc794cd0b12f85ba8/archive/legacy-sts/core-the-spire/Core.TheSpire.Legacy/Legacy/SpireCombatDslCompiler.cs#L54) | DSL |
| `power` / `enemy` / `affliction` | [SpireCombatBehaviorDslCompiler.cs:41-48](https://github.com/QingXiang22/auto-the-spire/blob/0aca7d1a26f3ad42d5b8657bc794cd0b12f85ba8/archive/legacy-sts/core-the-spire/Core.TheSpire.Legacy/Legacy/SpireCombatBehaviorDslCompiler.cs#L41-L48) | DSL（旧语法） |
| `state` | [StateVariableDefinition.cs:31](https://github.com/QingXiang22/auto-the-spire/blob/0aca7d1a26f3ad42d5b8657bc794cd0b12f85ba8/archive/legacy-sts/core-the-spire/Core.TheSpire.Legacy/Legacy/StateVariableDefinition.cs#L31) | DSL，**仅 `int`** |
| 角色 | [Runs.cs:192](https://github.com/QingXiang22/auto-the-spire/blob/0aca7d1a26f3ad42d5b8657bc794cd0b12f85ba8/archive/legacy-sts/core-the-spire/Core.TheSpire.Legacy/Legacy/Runs.cs#L192) `CharacterDefinition` | **仅 C#** |
| 遗物 | [Content.cs:186](https://github.com/QingXiang22/auto-the-spire/blob/0aca7d1a26f3ad42d5b8657bc794cd0b12f85ba8/archive/legacy-sts/core-the-spire/Core.TheSpire.Legacy/Legacy/Content.cs#L186) `RelicDefinition` | **仅 C#，且只有数据没有行为** |
| 遭遇 / Act 池 | `ContentDsl.cs` | JSON |

**遗物是纯数据**：`RelicDefinition` 只有 `Id` / `Rarity` / `InSharedPool` / `IsFallback` / `IsStackable` / `CharacterPoolIds`，**没有任何行为字段**。行为全部靠 C# `IRunHook` / `IRuleHook` 实现，例如 [BaseSpirePackage.cs:411-537](https://github.com/QingXiang22/auto-the-spire/blob/0aca7d1a26f3ad42d5b8657bc794cd0b12f85ba8/archive/legacy-sts/mod-the-spire/base-spire/BaseSpire.Legacy/BaseSpirePackage.cs#L411-L537) 里的 `BurningBloodRunHook`、`CharonsAshesHook`、`RedSkullHook`。

### 3.2 触发与扩展面

| 面 | 数量 | 位置 |
| --- | --- | --- |
| 卡牌钩子 | **6**：`onExhaust` / `turnEndInHand` / `onEnterCombat` / `beforeCardPlayed` / `afterCardPlayed` / `afterAutoPostPlay` | [Content.cs:225-239](https://github.com/QingXiang22/auto-the-spire/blob/0aca7d1a26f3ad42d5b8657bc794cd0b12f85ba8/archive/legacy-sts/core-the-spire/Core.TheSpire.Legacy/Legacy/Content.cs#L225-L239) |
| Power 生命周期触发器 | **11** | [Content.cs:1131-1133](https://github.com/QingXiang22/auto-the-spire/blob/0aca7d1a26f3ad42d5b8657bc794cd0b12f85ba8/archive/legacy-sts/core-the-spire/Core.TheSpire.Legacy/Legacy/Content.cs#L1131-L1133) |
| 战斗规则事件名 | 约 **46** 个（`damage`、`card_exhausted`、`power`、`hp_loss` …） | `State.cs` / `Effects.cs` 中的 `Emit(session, "…")` |
| `IRuleHook` | 固定 **10** 个方法 | [Content.cs:2420-2438](https://github.com/QingXiang22/auto-the-spire/blob/0aca7d1a26f3ad42d5b8657bc794cd0b12f85ba8/archive/legacy-sts/core-the-spire/Core.TheSpire.Legacy/Legacy/Content.cs#L2420-L2438) |
| `IRunHook` | 固定 **11** 个方法 | [Runs.cs:339-355](https://github.com/QingXiang22/auto-the-spire/blob/0aca7d1a26f3ad42d5b8657bc794cd0b12f85ba8/archive/legacy-sts/core-the-spire/Core.TheSpire.Legacy/Legacy/Runs.cs#L339-L355) |
| `ICustomEffectHandler` 可用命令 | **4** 个：`Execute(effect)` / `DealDamage` / `GainBlock` / `ApplyPower`，加 mod 级 `int` 读写 | [EffectExecutionContext.cs:41-93](https://github.com/QingXiang22/auto-the-spire/blob/0aca7d1a26f3ad42d5b8657bc794cd0b12f85ba8/archive/legacy-sts/core-the-spire/Core.TheSpire.Legacy/Legacy/EffectExecutionContext.cs#L41-L93) |

### 3.3 效果原语与目标

- 效果原语约 60 个，其中 **25 个在新版卡牌语法里写不出来**（缺 `PositionalFields` 映射），见 [primitives.md](../../../dsl/0.0.1/primitives.md)。作者侧的关键缺项：`addCardEnchantment`、`removeCardEnchantment`、`expirePower`、`loseMaxHp`、`cardHasEnchantment`、`setEnemyAction`。
- 目标选择器 **7 个**：`None / Self / FirstEnemy / SelectedTarget / AllOpponents / RandomEnemy / OtherAllies`（[Effects.cs:269](https://github.com/QingXiang22/auto-the-spire/blob/0aca7d1a26f3ad42d5b8657bc794cd0b12f85ba8/archive/legacy-sts/core-the-spire/Core.TheSpire.Legacy/Legacy/Effects.cs#L269)）。
- 计数源 **12 个**、谓词 **12 个**。
- 关键词是固定 flags 枚举（`Exhaust / Ethereal / Unplayable / Retain / Innate`，[Content.cs:1119](https://github.com/QingXiang22/auto-the-spire/blob/0aca7d1a26f3ad42d5b8657bc794cd0b12f85ba8/archive/legacy-sts/core-the-spire/Core.TheSpire.Legacy/Legacy/Content.cs#L1119)）；卡牌类型 4 个、稀有度 11 个也是封闭枚举。

### 3.4 状态模型

自定义状态只有 `(modId, key) -> int` 一张表，作用域是**整局**或**单场战斗**，**没有实体维度**：

- 战斗态：[State.cs:296](https://github.com/QingXiang22/auto-the-spire/blob/0aca7d1a26f3ad42d5b8657bc794cd0b12f85ba8/archive/legacy-sts/core-the-spire/Core.TheSpire.Legacy/Legacy/State.cs#L296)、[State.cs:689-692](https://github.com/QingXiang22/auto-the-spire/blob/0aca7d1a26f3ad42d5b8657bc794cd0b12f85ba8/archive/legacy-sts/core-the-spire/Core.TheSpire.Legacy/Legacy/State.cs#L689-L692)
- 整局态：[Runs.cs:402](https://github.com/QingXiang22/auto-the-spire/blob/0aca7d1a26f3ad42d5b8657bc794cd0b12f85ba8/archive/legacy-sts/core-the-spire/Core.TheSpire.Legacy/Legacy/Runs.cs#L402)、[Runs.cs:571-573](https://github.com/QingXiang22/auto-the-spire/blob/0aca7d1a26f3ad42d5b8657bc794cd0b12f85ba8/archive/legacy-sts/core-the-spire/Core.TheSpire.Legacy/Legacy/Runs.cs#L571-L573)
- DSL 声明：`state Name { scope = combat|run type = int default = 0 }`

---

## 4. OriMod 到底需要什么

### 4.1 逐实体自定义状态：浮空（Aloft）系统

[AloftCombatState.cs:12-30](../../../../reference/orimod/src/Ori/Aloft/AloftCombatState.cs#L12-L30) 用一个以战斗状态为键的 `ConditionalWeakTable` 保存：

```csharp
class PlayerState { int Aloft; int Momentum; ulong MovementRevision; }
class CombatStates {
    Dictionary<Player, PlayerState> Players;
    HashSet<Player> EndedPlayers;
    Dictionary<Creature, int> EnemyHeights;      // 每名敌人的高度
}
```

高度是战斗的核心轴：卡牌按高度算伤害、按高度差选目标、Power 监听高度变化。例如 [Feathers.cs:21-31](../../../../reference/orimod/src/Ori/Cards/Feathers.cs#L21-L31) 的伤害是 `AloftSystem.GetAloft(owner) / max(1, AloftStep)`；[AfterimagePower.cs:20-45](../../../../reference/orimod/src/Ori/Powers/AfterimagePower.cs#L20-L45) 在浮空变化时筛出路径穿过的敌人、**按高度排序**后逐个结算。

> **我们的引擎无法表达**：状态没有实体维度；`scaled` 只有 `base + perCount × count`，12 个计数源里没有"某实体的自定义数值"；目标选择器无法表达"路径穿过的敌人按高度排序"。

**34 张卡引用 `AloftSystem`，6 张引用 `EnemyAloftSystem`，27 张引用场地。**

### 4.2 战斗中的世界对象：场地/平台

[AbstractOriPlacementState.cs:22-70](../../../../reference/orimod/src/Ori/Placements/AbstractOriPlacementState.cs#L22-L70) 显示场地是**独立实例**：`InstanceId`、`PlacementKindId`、`Scope`、`Height`、`Amount`、`InitialAmount`、`EffectiveRange`、`BottomHeight`/`TopHeight`、`IsUniqueInScope`、`CanBeReached`、`CanBeBashed`、渲染优先级与配色。有 29 个具体场地状态和专门的 `OriPlacementIdentityAllocator` / `OriPlacementCollection` / `OriPlacementOrdering`。

> **我们的引擎没有这个概念。** 战斗状态里只有玩家、敌人、Power、牌区。要加就得引入"世界对象"实体类别、身份分配、顺序与快照语义。

### 4.3 逐玩家持久进度：能力与学习树

[OriAbilityPlayerRunSaveData.cs](../../../../reference/orimod/src/Ori/Abilities/OriAbilityPlayerRunSaveData.cs) 的存档结构：`UnlockedMask`、`WispMask`、`PreferredWeapon`、一组 `OriAbilityPersistentSnapshotEntry`，并且实现 `IPacketSerializable`（多人网络同步）。[OriAbilitySystem.cs:96-106](../../../../reference/orimod/src/Ori/Abilities/OriAbilitySystem.cs#L96-L106) 显示持久解锁要走主机权威的 `OriAbilityNetworkBridge`。

配套还有：学习树里程碑/计划（`OriAbilityLearningPlan`、`OriAbilityLearningMilestone`）、地图节点事件、专门的三棵 UI 界面，以及存档补丁（`RunManager.ToSave`、`RunState.FromSerializable`）。

> **我们的引擎**：单个 mod 状态是 `int` 键值表，没有逐玩家的结构化持久状态，没有 mod 存档格式契约，没有主机权威概念。

### 4.4 开放拦截面：123 处 Harmony 补丁

`OriMod/OriModCode/Patches/**` 共 34 个补丁类、**123 个 `[HarmonyPatch]`**。按语义分类：

| 类别 | 补丁类数 | 典型目标 |
| --- | --- | --- |
| 规则语义 | ~20 | `Hook.AfterCardPlayed` / `Hook.BeforeDamageReceived` / `Hook.ModifyHpLost` / `Hook.AfterSideTurnEnd` / `Hook.ModifyPowerAmountReceived` / `Hook.ShouldAllowHitting` / `Hook.ShouldDie` / `Hook.ModifyMerchantCardPool` / `Hook.ShouldAddToDeck`、`CombatState.AddPlayer`、`CombatManager.Reset`、`CreatureCmd.Damage`、`Creature.DamageBlockInternal`、`AttackCommand.GetPossibleTargets`、`CardFactory.GetForCombat`、`ChecksumTracker.GenerateChecksum`、`RunState.CreateForNewRun` / `FromSerializable`、`RunManager.GenerateMap` … |
| 表现/UI | ~12 | 字体替换、标题音乐、运行音乐、光标、暂停菜单侧栏、Boss 血条、标题图标、UI 音效、Spine 动画 |
| 其他 | ~3 | 调试可见性、攻击动画 |

> 项目 spec 明确把支持范围限定在"能映射到内容注册、DSL 命令和受控 hook 的 mod"，超出范围的补丁需要适配或新增内核能力（[spec.md](../../../design/spec.md) 第 138 行附近）。OriMod 的规则语义补丁大量落在内核内部方法上——**这些补丁在我们的引擎里没有对应接缝，也不应该有**。

### 4.5 伤害/格挡公式绑定运行时状态

91 张卡引用游戏的 `Localization.DynamicVars`（含 `CalculationBaseVar` / `CalculatedDamageVar.WithMultiplier(lambda)` / `ExtraDamageVar`），即"伤害 = 任意 C# 表达式读任意运行时状态"。

> **我们的引擎**只有 `scaled = { base, perCount, count = {12 个计数源} }`。缺 `min`/`clamp` 这类值原语，也没有"读取某实体自定义数值"的计数源。

### 4.6 单人/多人边界

OriMod 是多人感知的：`OriCombatChecksumPatch`（战斗校验和）、`OriCombatStateDivergencePatch`（状态分歧处理）、多个 `Net*Action` / `*Message` 类、`CardMultiplayerConstraint.MultiplayerOnly` 卡。我们的引擎是单人优先，`MultiplayerOnly` 卡当前"注册但不进单人卡池"。

> 移植时应**丢弃**这部分，而不是适配。

---

## 5. 逐类映射判断

| OriMod 内容 | 今天能否注册 | 判断 |
| --- | --- | --- |
| 角色 `OriCharacter`（生命/牌组/起始遗物/卡池） | ✅ 数据可映射 | 但只能写 C# `CharacterDefinition`，DSL 无角色声明；`AfterRoomEntered`（鬼火进度）等覆写无对应钩子 |
| 3 个卡池 + 遗物池 + 药水池 | ✅ | `cardPool` 有 DSL；遗物/药水池目前是 C# 数据 |
| 起始牌 `StrikeOri` / `DefendOri` | ✅ | 纯伤害/格挡，标准形状 |
| 约 23 张"不引用自有系统和自有 Power"的卡 | ⚠️ 部分 | 仍需逐张核对；抽查已发现多重障碍，见下 |
| 其余 99 张卡 | ❌ | 依赖浮空/场地/能力系统或自有 Power |
| 32 个 Power | ⚠️ 少数 | 简单数值型 Power 可用 power DSL；挂在自有系统上的（如 `AfterimagePower`）不行 |
| 19 个遗物 | ⚠️ 数据可以，行为不行 | 遗物无 DSL；行为要写 C# `IRuleHook`，而 `IRuleHook` 只有 10 个固定方法 + 约 46 个事件名，覆盖不了浮空/场地/能力语义 |
| 能力系统（63 类）+ 学习树 | ❌ | 需要逐玩家结构化持久状态、存档、学习计划、UI |
| 浮空系统（26 文件） | ❌ | 需要逐实体数值状态 + 排序目标查询 |
| 场地系统（51 文件） | ❌ | 需要新的"世界对象"实体类别 |
| 鬼火系统（11 文件） | ❌ | 改卡牌奖励池/商店库存可用性（`ModifyMerchantCardPool` 等），需要对应钩子 |
| 1 个事件 `TemperamentEvent` | ⚠️ | run event DSL 存在，但要核对所需条件/效果 |
| 怪物 / 药水 | — | OriMod 没有具体怪物和药水 |
| 表现层（字体/音乐/光标/血条/动画） | ❌ 且不需要 | 训练环境不含 UI/音频 |

### 抽查"最干净"的卡也会踩坑

从 23 张候选里抽查：

- [Reflux.cs:29-40](../../../../reference/orimod/src/Ori/Cards/Reflux.cs#L29-L40)：`min(牌数, 弃牌堆数)` 夹取后从弃牌堆选牌回手、再把手牌置顶。**缺 `min` 值原语**。
- [HandToHand.cs:18-72](../../../../reference/orimod/src/Ori/Cards/HandToHand.cs#L18-L72)：多人专用、`TargetType.AnyAlly`、动态 `IsPlayable`（读手牌）、覆写 `GetResultLocationForCardPlay` 改打出后去向、自定义筛选谓词、调用 mod 自有 `OriCardTransferCmd`。
- [Spark.cs:20-36](../../../../reference/orimod/src/Ori/Cards/Spark.cs#L20-L36)：只 apply 两个 Power——但那是 mod 自有 Power（`AimPower` / `AimRetainPower`）。
- [Glimmering.cs:14-27](../../../../reference/orimod/src/Ori/Cards/Glimmering.cs#L14-L27)：用 `OriKeywords.MeleeAttack` 这个**带规则语义的自定义关键词**（我们的关键词是封闭 flags 枚举）。

---

## 6. 如果要做，按代价排序的最小内核扩展

**必须做，且改的是状态模型而不是 DSL 表面：**

1. **逐实体自定义状态**：把 `state` 从会话级 `int` 表扩展为带实体维度的数值状态（或至少允许 `(scope, entityId, key) -> int`）。这是浮空、场地、能力的地基。
2. **世界对象实体类别**：战斗状态里引入带 ID、位置/层高、数值、顺序和快照语义的对象集合。
3. **逐玩家结构化持久状态 + 存档契约**：能力/学习/鬼火进度需要它，且必须与随机流、快照/恢复契约一起设计。
4. **有序/属主筛选的目标查询**：把 `TargetSelector` 从 7 个封闭枚举升级为"查询 + 选择契约"（评估报告 §3 已把这条列为结构问题）。
5. **可组合的数值表达式**：`min`/`clamp`、读实体状态、乘法/条件分支，替代每加一种算法就加一个专用原语的做法。

**收益高、代价低，可以先做：**

6. 补齐 `primitives.md` 里"疑似遗漏的映射"三项（`loseMaxHp`、`cardPlaysThisTurnAtLeast`、`cardHasEnchantment`），纯 `PositionalFields` 补条目。
7. 遗物行为 DSL（或至少给遗物一套声明式触发块），否则任何遗物都要写 C#。
8. 自定义关键词的规则语义注册（关键词 → 效果绑定），而不是封闭 flags 枚举。

**明确不要做：**

9. 不要为 OriMod 引入 Harmony 式开放拦截。spec 划定的支持范围是"能映射到注册/DSL/受控 hook 的 mod"；为个别 mod 开放内核内部接缝会破坏可复现性与训练确定性。
10. 不要移植多人同步、校验和、UI/音频部分。

---

## 7. 更可能正确的路线

如果目标只是"让训练环境里有 OriMod 的内容可用于对照"，那么**逐内容移植是性价比最低的路线**：OriMod 的"内容"本质是贴着游戏运行时写的代码，没有一个可供转换的中间表示。两条更现实的路线：

- **A. 切片移植**：只取角色数据 + 少数纯数值卡 + 简单 Power，做成"OriMod 风味"的对照内容包。诚实标注它不代表 OriMod 的核心玩法。
- **B. 差分对照**：用 `net-the-spire` 的真实游戏后端跑真 OriMod，把观察/动作与我们的引擎做差分。这条路线不需要表达浮空/场地/能力，且保真度上限更高——与本项目已定的"真实游戏后端"设计一致。

若目标是 B，那么当前 DSL 的缺口**不构成阻断**；若目标是 A 里"完整核心内容"，则缺口如 §6 所述，属于内核级工作，不是 DSL 补丁。

---

## 8. 验证方式与未验证项

**已执行的验证：**

- 混淆检查：类型清单、短名统计、混淆器特征扫描、字符串可读性、反编译占位符扫描、往返类型集合比对（813 vs 813）。
- 内容计数：按目录统计具体类/抽象类；卡牌耦合度用正则扫描 122 个具体卡文件（`AloftSystem|EnemyAloftSystem|OriAbilitySystem|OriPlacementStateSystem|OriAbility|Placement|Wisp|OriCombatCustomState|OriDistance` 与 `Ori.Powers|OriKeywords|OriCardTags`）。
- 补丁清点：从 `[HarmonyPatch(typeof(X), "Y")]` 正则抽取目标并按文件名分类规则 vs 表现。
- 引擎能力面：全部来自源码直读（行号已标注）与 `primitives.md`。

**未验证 / 判断的边界：**

- **卡牌分类是正则启发式**，不是逐行语义审计。引用了自有系统的卡一定不能直接移植；**没引用也不等于能移植**（§5 抽查已证明）。真实可移植卡数应低于 23。
- 未逐张读完 122 张卡、32 个 Power、19 个遗物的全部效果体，也未逐条核对 123 处补丁的行为。
- 未评估 `OriMod.pck` 里资源对规则的影响（本地化文本、场景中可能内嵌数值）。
- 未做"移植一张卡"的端到端试验，因此 §6 的扩展项是设计判断，不是已验证的最小集。
- 未运行 `dotnet build AutoTheSpire.sln` 或规则检查——本次没有改动生产代码，判断基于阅读。
