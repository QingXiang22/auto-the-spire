# 受控战斗切片 7 项数值来源核对（sts2 为准）

> 历史证据（2026-10-03整理）：正文保留原时点结果，历史“下一步”不再作为当前派单指令。本报告归属W-06，未结项转入[当前清单](../../tofix/README.md)；移入records不表示修复或验收通过。逐份归属见[记录索引](../README.md)。

| 项目 | 内容 |
| --- | --- |
| 状态与日期 | 2026-10-01；只读核对，**未修改任何文件** |
| 用途 | B-01 切片冻结来源清单（[SliceManifest.cs](../../../../tools/base-migration-baseline/BaseSpire/Composition/SliceManifest.cs)）的证据；B-02 至 B-06 的行为对照基线 |
| 权威来源 | `sts2/` 恢复工程；现有 base-spire 内容与旧测试作为对照 |
| 路径说明 | 本文中的 `State.cs`/`Content.cs`/`Environment.cs`/`Effects.cs` 等旧后端文件均指 `core-the-spire/Core.TheSpire/Legacy/`（2026-10-01 路径迁移） |
| 缺陷状态 | DampCultist HP 已按本文权威值修复（`enemies.spire` 现为 `default=range(51,53), tough=range(52,54)`）并归档 |
| **更正（2026-10-01 评审）** | 差异 D 原写"整数比倍率不会产生不同取整结果"，**该表述不成立**。规划者给出数学反例：基础伤害 5、Weak ×0.75、Vulnerable ×1.5 —— 中途 Floor 得 4，末端一次截断得 5。因此"取整点不同"是**实际可观测差异**，须由 R-DAMAGE 以 `sts2/` 与真实行为反例确认最终取整位置，不得继承中途取整后称来源等价 |
| 相关缺陷 | [DampCultist HP 范围写反](../../archived/damp-cultist-hp-range-inversion-2026-10-01.md)（已修复归档） |

## 逐项对照

| 项 | 权威来源（sts2/） | 权威值 | 现有 base-spire | 判定 |
| --- | --- | --- | --- | --- |
| StrikeIronclad 费用/类型/稀有度/目标 | `Core/Models/Cards/StrikeIronclad.cs:25` | 1 能 / Attack / Basic / AnyEnemy | `ironclad-cards.spire:1394-1396` 一致 | ✅ |
| StrikeIronclad 伤害 | `StrikeIronclad.cs:19`（`DamageVar(6m, ValueProp.Move)`）/ `:39`（升级 +3） | 6 / 9 | `:1400` 6、`:1415` 9 | ✅ |
| StrikeIronclad 卡池与 tag | `CardPools/IroncladCardPool.cs:100`、`StrikeIronclad.cs:17` | IroncladCardPool / `CardTag.Strike` | `:1389` 池、`:1390` tag | ✅ |
| DefendIronclad | `DefendIronclad.cs:17,20,31`（`BlockVar(5m)`、升级 +3、TargetType.Self） | 1 能，格挡 5 / 8 | `:1430-1435`、`:1450` | ✅ |
| Bash | `Bash.cs:18-25,40-41`（`DamageVar(8m)`、`PowerVar<VulnerablePower>(2m)`，先伤害后易伤） | 2 能，8 + 易伤 2；升级 10 + 易伤 3 | `:1465-1476`、`:1489-1495` | ✅ |
| Headbutt 数值 | `Headbutt.cs:16,19,38` | 1 能，9；升级 12 | `:2021`、`:2026`、`:2047` | ✅ |
| Headbutt 选择语义 | `Headbutt.cs:29,32`（`CardSelectCmd.FromCombatPile(pile: Discard, 1)` → `CardPileCmd.Add(..., Draw, Top)`；空则不请求） | 弃牌堆精确选 1 张 → 抽牌堆顶 | `:2028-2032` 语义一致，提示文案不同 | ⚠️ B |
| Strength | `Powers/StrengthPower.cs:11-25`、`ValueProps/ValuePropExtensions.cs:5-12` | 每层 **+1 加法**，仅出手者自身的 powered attack（`Move` 且非 `Unpowered`，含敌人攻击），可为负 | `State.cs:585`、`shared-powers.spire:2` | ✅ |
| Vulnerable | `Powers/VulnerablePower.cs:22`（`DamageIncrease = 1.5m`）、`:27-36`、`:59-65` | 受 powered attack **×1.5**；敌方阵营回合结束递减 1 层 | `State.cs:601`、`shared-powers.spire:17-21` | ✅（递减机制见 C） |
| 伤害结算顺序 | `Hooks/Hook.cs:2515-2538`（Additive → Multiplicative → Cap）、`:1565`（下限 0） | **加法（力量）→ 乘法（易伤/虚弱）→ Cap → 下限 0** | `State.cs:585 → 588-589 → 601-609 → 611-627` 顺序等价 | ⚠️ 取整点见 D |
| DampCultist HP | `Monsters/DampCultist.cs:28,30` | 普通 **51–53**；ToughEnemies **52–54** | `enemies.spire:222` 两档**写反** | ❌ A |
| DampCultist 行动序列 | `DampCultist.cs:69-74` | 首招 Incantation（Buff 意图）→ 之后**永久循环** DarkStrike | `enemies.spire:227` `startWith(...) + loop(...)` | ✅ |
| DampCultist 伤害与首招 Power | `DampCultist.cs:32,34,83` | 出生**无** Power；首招施加 **Ritual 5**（Deadly 6）；攻击 1（Deadly 3） | `enemies.spire:224-225` | ✅ |
| Ritual 效果 | `Powers/RitualPower.cs:45-57` | 己方回合结束获得等于层数的 Strength，**施加当次跳过** | `enemies.spire:196-200`、`CombatSliceTests.cs:709` | ✅ |
| Cultists 遭遇编成 | `Models/Encounters/CultistsNormal.cs:11-12,23-24` | CalcifiedCultist 在**前**，DampCultist 在后 | `Enemies/Content/base-spire.encounters.json:170-172` 同序 | ✅ |

## 差异清单

**A. DampCultist HP 两档写反** —— 唯一的数值性冲突，已单独报告：
[damp-cultist-hp-range-inversion-2026-10-01.md](../../archived/damp-cultist-hp-range-inversion-2026-10-01.md)。

**B. Headbutt 选择提示文案不同（无规则影响）**：base-spire `ironclad-cards.spire:2031`
`"Choose a card from discard"`；sts2 `localization/eng/cards.json:531`
`"Choose a card to put on top of your Draw Pile."`。行为（弃牌堆精确选 1 张 → 抽牌堆顶）一致。

**C. ~~易伤"施加当回合不递减"实现层次不同、语义等价~~**（**已由 [R-DAMAGE 证据](r-damage-source-evidence-2026-10-01.md) §5 修正**）：
本文原写"sts2 在命令层对任何施加到玩家侧的 Debuff 通用设置 `SkipNextDurationTick`（不看层数）"。**该前提不成立**——`Core/Commands/PowerCmd.cs:103` 在 `amount == 0m` 时直接 `return`，两侧都不置位，**"层数 0 的施加"两侧无可见差异**。
但**存在另一处真实可见差异**：sts2 只在**新建 power 实例**时置位 skip，叠加到已有实例走 `ModifyAmount`（`PowerCmd.cs:78,84,215-271`）**不重新置位**；旧后端 `State.cs:553-555` 每次 `amount != 0` 都置位。后果：旧后端的易伤/虚弱在"叠加"场景下会比恢复源码**多持续一个回合且多 1 层**（逐步场景见 R-DAMAGE 证据 §5.3）。B 批迁移必须显式选择并写行为检查。

**D. 伤害取整点不同**（**已由 [R-DAMAGE 证据](r-damage-source-evidence-2026-10-01.md) §1/§3/§4 定位并证实**）：
- **最终取整位置已定位**：decimal 总量在 `sts2/src/Core/Entities/Creatures/Creature.cs:449` 由 `int num = (int)Math.Min(amount, 999999999m);` 转成实际扣除的整数 HP（`(int)` = 向零截断；因上游两次 `Math.Max(0m,…)` 夹到非负，Truncate 与 Floor 在此等价）。全链路**仅此一次**，位于加法→乘法→Cap→下限 0→格挡吸收→`ModifyHpLost` hooks **之后**。
- **旧后端是三处 Floor，不是两处**：`Legacy/State.cs:598`、`:609`、`:627`（此前漏记 `:627`）。
- **规划者的反例成立且已算实**：基础 5 + Weak×0.75 + Vulnerable×1.5 → 恢复源码 `5×0.75=3.75`、`×1.5=5.625`、末端截断 = **5**；旧后端 `Floor(3.75)=3`、`3×1.5=4.5`、`Floor(4.5)` = **4**。相差 1。
- 因此"倍数均为整数比时结果相同"这一说法**不成立**（0.75 与 1.5 都是整数比，组合后仍差 1）。实现阶段必须按 `Creature.cs:449` 的**末端单次截断**，不得继承旧后端的三处 Floor。

**D. 伤害取整点不同（未构造反例）**（*原始表述，已被上一条更正取代，按 §8.10 保留原样*）：sts2 全程 decimal 连乘，末端 `Math.Max(0m, num)` 一次性处理
（`Hooks/Hook.cs:1565`）；base-spire 在 `State.cs:598` 与 `:609` **各 Floor 一次**（力量/出手倍率后一次、易伤后
又一次）。倍数均为整数比（1.5 / 0.75 / 0.7）时结果相同，非整数组合下可能差 1。**B-02 必须选定单一截断点并
给出证据，不要继承双重 Floor。**

## 未找到 / 未确认

1. **"Incantation" 不是独立 Power**：sts2 中 `INCANTATION` 只是 MoveState 名（`DampCultist.cs:69`）与本地化键
   （`:22`），实际施加的是 `RitualPower`（`:83`）；base-spire 亦无（grep `Incantation` 只命中行动名）。
2. **DampCultist 出生 Power：不存在**（两侧都没有；Ritual 只在首招施加）。
3. **sts2 中 decimal→int 的最终取整语句位置未确认**：`Core/Commands/DamageCmd.cs` 内
   `ModifyDamage|int damage|Floor|Truncate` 均无命中，只确认了 Hook 层的加/乘/Cap 顺序与下限 0。因此差异 D
   只标注"取整点不同"，**不**断言最终数值差。
4. **覆盖证明**：关键词 `StrikeIronclad / DefendIronclad / Bash / Headbutt / Strength / Vulnerable / Ritual /
   Cultist / DampCultist / IsPoweredAttack / ModifyDamage(Additive|Multiplicative) / SkipNextDurationTick /
   TickDownDuration / SelectionScreenPrompt / GetValueIfAscension / ToughEnemies / DeadlyEnemies / "tough" /
   "deadly" / CultistsNormal`；路径 `sts2/src/**`、`sts2/localization/eng/cards.json`、
   `ContentSource/{ironclad-cards,shared-powers,ironclad-powers,enemies}.spire`、
   `BaseSpire/{BaseSpirePackage.cs,Enemies/Content/base-spire.encounters.json}`、
   `core-the-spire/Core.TheSpire/{State.cs,SpireCombatDslCompiler.cs}`、`Core.TheSpire.Tests/CombatSliceTests.cs`、
   `docs/{design,review,dsl}/**`。
   `reference/sts2-sim/` 仅对 Headbutt 做了一次旁证读取（`:26-32`），不作为权威证据身份。

## 对后续批次的使用方式

- **B-02** 直接用本表的"伤害结算顺序""Strength""Vulnerable"三行实现伤害与 Power，并按差异 D 选定截断点。
- **B-04/B-06** 用"Ritual""行动序列""遭遇编成"三行做敌人行为与覆盖表。
- 任何声称"与来源一致"的结论都必须能回到本表的 `文件:行`；本表本身是 2026-10-01 的核对快照，不是原版正确性的永久证明。
