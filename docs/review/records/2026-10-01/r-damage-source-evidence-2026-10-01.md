# R-DAMAGE 只读来源证据：伤害取整位置与属性条件

> 历史证据（2026-10-03整理）：正文保留原时点结果，历史“下一步”不再作为当前派单指令。本报告归属W-06，未结项转入[当前清单](../../tofix/README.md)；移入records不表示修复或验收通过。逐份归属见[记录索引](../README.md)。

| 项目 | 内容 |
| --- | --- |
| 状态与日期 | 2026-10-01；**只读证据阶段，未修改任何生产代码，未运行任何 `dotnet` 命令** |
| 负责范围 | 以 `sts2/` 恢复工程确定"伤害取整位置与属性条件"的事实；对照旧后端 `Legacy/State.cs` 与受控切片 `CombatSlicePackage.cs`；不改代码、不选实现方案 |
| 权威来源 | `sts2/src/**`（恢复工程源码）。`reference/sts2-sim/` **未使用**（仓库既有记录明确其不具权威证据身份） |
| 上游输入 | [review-followup-2026-10-01.md](review-followup-2026-10-01.md) §2 | 来源报告 D 行、§3 R-DAMAGE 行；[sts2-slice-value-extraction-2026-10-01.md](sts2-slice-value-extraction-2026-10-01.md) 差异 C / 差异 D 与"未找到"第 3 项 |
| 本次新结论 | ① 最终取整位置**已定位**：`Creature.cs:449` 的显式 `(int)` 转换（截断），全链路仅此一次；② 规划者反例在恢复源码的真实结果是 **5**（不是 4），在旧后端是 **4**——反例成立，且"中途取整"确实被证伪；③ 上游"Power 0 层施加两侧不同"的前提**不成立**（`PowerCmd.cs:103` 有 `amount == 0m` 提前返回），但另有一处真实可见差异（叠加时是否重新置位 skip）；④ 旧后端 Floor 是**三处**（`State.cs:598/:609/:627`），此前记录漏了 `:627` |
| Git | 未 `add`/`commit`/`clean`；工作区中他人在途改动一律未触碰，本次只新增本报告文件 |

## 0. 结论摘要

1. **最终取整位置唯一，且是末端单次截断**：decimal 总量在 `sts2/src/Core/Entities/Creatures/Creature.cs:449` 由 `int num = (int)Math.Min(amount, 999999999m);` 转成实际扣除的整数 HP。C# 的 `decimal`→`int` 显式转换是**向零截断**（等价 `decimal.Truncate`），不是 `Floor`、不是 `Round`。全链路没有第二个生效的 decimal→int。
2. **反例真实结果 = 5**（基础 5 + Weak ×0.75 + Vulnerable ×1.5）：`5 × 0.75 = 3.75`、`3.75 × 1.5 = 5.625`，末端 `(int)5.625 = 5`。中途 Floor 会得 4 —— 旧后端正是 4（`State.cs:598` 先 Floor 成 3，再 `×1.5 = 4.5`，`:609` 再 Floor 成 4）。**"整数比倍率不会产生不同取整结果"这一原表述被来源证据明确证伪。**
3. **Power 0 层**：`PowerCmd.Apply`（`PowerCmd.cs:101-106`）在 `amount == 0m` 时直接 `return`，旧后端 `State.cs:553` 要求 `amount != 0`；两侧**都不置位** skip，下一次阵营回合结束**都会递减**，此场景**无可见差异**。上游差异 C 里"层数 0 的施加在两侧行为不同"的说法不成立。但发现一处**真实可见差异**：sts2 只在**新建 power 实例**时置位 skip，叠加到已有实例走 `ModifyAmount`（`PowerCmd.cs:84`、`:215-271`）**不重新置位**；旧后端 `ApplyPower` 每次 `amount != 0` 都置位。后果：旧后端的易伤/虚弱可能比恢复源码多持续一个回合。
4. **受控切片走整数运算**，在它自己的覆盖包络内与恢复源码一致，但**无法表达**非整数比组合、Weak/Shrink 出手倍率、Unpowered、玩家侧易伤——即规划者反例落在切片的覆盖范围之外。

## 1. Q1 最终取整位置

### 1.1 完整调用链（`文件:行`）

| 步 | 位置 | 说明 |
| --- | --- | --- |
| 1 | `sts2/src/Core/Commands/DamageCmd.cs:14-17` | `public static AttackCommand Attack(decimal damagePerHit)` → `new AttackCommand(damagePerHit)`（或 `:26-29` 的 `CalculatedDamageVar` 重载） |
| 2 | `sts2/src/Core/Commands/Builders/AttackCommand.cs:653` | 每次命中调用 `CreatureCmd.Damage(...)`，`props: DamageProps`、`dealer: Attacker` |
| 3 | `sts2/src/Core/Commands/CreatureCmd.cs:240` | `public static async Task<IEnumerable<DamageResult>> Damage(PlayerChoiceContext choiceContext, IEnumerable<Creature> targets, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)` |
| 4 | `CreatureCmd.cs:261` | `decimal modifiedAmount = Hook.ModifyDamage(..., ModifyDamageHookType.All, CardPreviewMode.None, out modifiers);` ← 加法/乘法/Cap 全在 decimal 域完成 |
| 5 | `sts2/src/Core/Hooks/Hook.cs:2511-2557` | `ModifyDamageInternal`：`Additive`（`:2515-2526`）→ `Multiplicative`（`:2527-2538`）→ `Cap`（`:2539-2555`）；全程 `decimal`，无取整 |
| 6 | `Hook.cs:1565` | `return Math.Max(0m, num);` ← 下限 0，**仍是 decimal**（此前只找到这里） |
| 7 | `CreatureCmd.cs:265` | `decimal blockedDamage = creature.DamageBlockInternal(modifiedAmount, props);` |
| 8 | `CreatureCmd.cs:266` | `decimal unblockedDamage = Hook.ModifyHpLost(..., Math.Max(modifiedAmount - blockedDamage, 0m), ...);` ← 仍是 decimal |
| 9 | `CreatureCmd.cs:271` | `DamageResult unblockedDamageResult = unblockedDamageTarget.LoseHpInternal(unblockedDamage, props);` |
| 10 | **`Creature.cs:445-450`** | **`int num = (int)Math.Min(amount, 999999999m); CurrentHp = Math.Max(CurrentHp - num, 0);` ← 最终取整位置** |

### 1.2 取整语句逐字引用

`sts2/src/Core/Entities/Creatures/Creature.cs:445-450`：

```csharp
	public DamageResult LoseHpInternal(decimal amount, ValueProp props)
	{
		bool flag = CurrentHp > 0 && amount >= (decimal)CurrentHp;
		int currentHp = CurrentHp;
		int num = (int)Math.Min(amount, 999999999m);
		CurrentHp = Math.Max(CurrentHp - num, 0);
```

`Creature.cs:449` 的 `(int)` 是**显式转换**，C# 语义为**向零截断**（`decimal.Truncate`），非 `Math.Floor`、非 `Math.Round`、非隐式赋值转换。因为进入该函数的金额已被 `Hook.cs:1565`（`Math.Max(0m, …)`）与 `CreatureCmd.cs:266`（`Math.Max(modifiedAmount - blockedDamage, 0m)`）两次夹到 ≥0，实际值非负，所以此处 Truncate 与 Floor 结果相同——但**代码写的是 Truncate**。

实际扣除的整数值随后成为 `DamageResult.UnblockedDamage`（`sts2/src/Core/Entities/Creatures/DamageResult.cs:41` `public int UnblockedDamage { get; init; }`，由 `Creature.cs:453` 的 `UnblockedDamage = currentHp - CurrentHp` 赋值），所以对外可见的伤害数字就是这一次截断的结果。

### 1.3 伤害路径上所有 decimal→int 点（穷举）

在 `sts2/src/Core/Commands/*.cs`、`sts2/src/Core/Entities/Creatures/*.cs`、`Core/Hooks/Hook.cs`、`Core/Models/PowerModel.cs` 中检索 `\(int\)` 的全部命中，与本链路相关的只有：

| 位置 | 语句 | 是否影响实际 HP 扣除 |
| --- | --- | --- |
| `Creature.cs:449` | `int num = (int)Math.Min(amount, 999999999m);` | **是**（唯一的 HP 扣除取整） |
| `Creature.cs:433` | `Block -= (int)num;`（`DamageBlockInternal`） | 否（只决定格挡扣减的记账整数；被吸收量 `num` 本身仍是 decimal 并参与 `modifiedAmount - blockedDamage`） |
| `CreatureCmd.cs:274` | `(int)unblockedDamage == 0` | 否（仅"完全格挡"判定） |
| `CreatureCmd.cs:277`、`:286` | `BlockedDamage = (int)blockedDamage;` | 否（仅 DamageResult 记账字段，`DamageResult.cs:30` 是 int 属性） |
| `Hook.cs:1727,1736,1748,1757` | `if (decimal.Truncate(d) != decimal.Truncate(num))` | 否（**仅用于判断 hook 是否改变了数值**，返回值 `num` 未截断） |
| `Hook.cs:1542` | `else if ((int)num3 != (int)num2.Value)` | 否（多目标预览一致性比较，真实伤害路径 `target != null` 不走该分支） |
| `Creature.cs:465,474,490,499` | Block/HP 上限与直接赋值 | 否（格挡增减、治疗/最大 HP 路径，不是攻击伤害路径） |
| `PowerCmd.cs:236,243` | `int newAmount = power.Amount + (int)modifiedOffset;` | 否（power 层数，另一条链路） |
| `PowerModel.cs:570` | `SetAmount((int)amount, silent);` | 否（power 层数） |

**结论：伤害的 decimal→int 只在 `Creature.cs:449` 发生一次，位于加法→乘法→Cap→下限 0→格挡吸收→`ModifyHpLost` hooks 之后。全程没有中途取整。**

### 1.4 "没有 Floor/Round"的检索证据

在 `sts2/src/Core/Commands/*.cs`、`sts2/src/Core/Entities/Creatures/*.cs`、`Core/Hooks/Hook.cs`、`Core/Models/PowerModel.cs` 检索 `Math.Floor|Math.Round|decimal.Floor|decimal.Round|decimal.Truncate|Math.Truncate`：命中只有 `Hook.cs:1727/1736/1748/1757` 的 `decimal.Truncate`（上文已说明用于变更检测），**没有任何 `Math.Floor`、`Math.Round`**。

`DamageCmd.cs` 本身只有两个工厂方法（`:6-30`），不含算术；其文档注释反而点明了设计意图（`DamageCmd.cs:22`）：

```csharp
	/// This uses special logic to ensure that intermediate values are floored at the appropriate time.
```

即 `CalculatedDamageVar` 重载负责"在恰当位置取整"，而"恰当位置"就是末端（见第 3 节逐步演算）。

**未找到项**：仓库 `sts2/src/Core/Commands/` 下**不存在 `HealthCmd.cs`**（该目录文件为 `CardCmd, CardPileCmd, CardSelectCmd, Cmd, CreatureCmd, DamageCmd, ForgeCmd, MapCmd, OrbCmd, OstyCmd, PlayerCmd, PotionCmd, PowerCmd, RelicCmd, RelicSelectCmd, RewardsCmd, SfxCmd, TalkCmd, ThinkCmd, VfxCmd`）。HP 损失入口统一是 `CreatureCmd.Damage` → `Creature.LoseHpInternal`。

## 2. Q2 属性判定条件与出处

### 2.1 ValueProp / IsPoweredAttack（`Unpowered` 的定义处）

`sts2/src/Core/ValueProps/ValueProp.cs:5-18` 逐字：

```csharp
/// <summary>
/// Aka damage type:
///   - Unblockable = HP loss like Poison
///   - Unpowered = Damage from relics, potions, and powers
///   - Move = Attack damage from Attack cards and Enemy creatures attacking
/// </summary>
[Flags]
public enum ValueProp
{
	Unblockable = 2,
	Unpowered = 4,
	Move = 8,
	SkipHurtAnim = 0x10
}
```

`sts2/src/Core/ValueProps/ValuePropExtensions.cs:5-12` 逐字：

```csharp
	public static bool IsPoweredAttack(this ValueProp props)
	{
		if (props.HasFlag(ValueProp.Move))
		{
			return !props.HasFlag(ValueProp.Unpowered);
		}
		return false;
	}
```

语义：**powered attack = 带 `Move` 且不带 `Unpowered`**。`Unpowered` 表示"来自遗物/药水/Power 的伤害"（如 Burn），`Move` 表示"攻击牌的伤害与敌人攻击"（`AttackCommand.cs:121` `public ValueProp DamageProps { get; private set; } = ValueProp.Move;`，注释见 `:118-120`）。攻击命令可用 `AttackCommand.Unpowered()` 加上该标志（`AttackCommand.cs:336-340`）：

```csharp
	public AttackCommand Unpowered()
	{
		DamageProps |= ValueProp.Unpowered;
		return this;
	}
```

其文档注释（`AttackCommand.cs:330-335`）说明：`Unpowered` 伤害 99% 情况下不应算作 attack（如 `Burn`），但偶尔要做跳过 power 的真实攻击（如 `Omnislice`）。另一处确认性用法：`CreatureCmd.cs:825` 用 `ValueProp.Unblockable | ValueProp.Unpowered`（最大 HP 损失不使用 war 属性修正）。

### 2.2 Strength

`sts2/src/Core/Models/Powers/StrengthPower.cs:13-26` 逐字：

```csharp
	public override bool AllowNegative => true;

	public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
	{
		if (base.Owner != dealer)
		{
			return 0m;
		}
		if (!props.IsPoweredAttack())
		{
			return 0m;
		}
		return base.Amount;
	}
```

- 条件：**该 power 的持有者 == 出手者**（`base.Owner != dealer` → 0），**且** `props.IsPoweredAttack()`。
- 效果：每层 `+1`（`base.Amount`，decimal），`AllowNegative => true`（可为负）。
- 阶段：`Hook.cs:2515-2526` 的 Additive 阶段，`num += num2`。

### 2.3 Vulnerable

`sts2/src/Core/Models/Powers/VulnerablePower.cs:22,27-36` 逐字：

```csharp
	protected override IEnumerable<DynamicVar> CanonicalVars => new global::_003C_003Ez__ReadOnlySingleElementList<DynamicVar>(new DynamicVar("DamageIncrease", 1.5m));
...
	public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
	{
		if (target != base.Owner)
		{
			return 1m;
		}
		if (!props.IsPoweredAttack())
		{
			return 1m;
		}
```

- 条件：**受击者持有该 power**（`target != base.Owner` → 1），**且** `props.IsPoweredAttack()`。
- 效果：`×1.5`（`DamageIncrease = 1.5m`，**固定倍率，与层数无关**；层数是持续时间，`:20` `PowerStackType.Counter`，`:16` `PowerType.Debuff`）。
- 递减：`:59-65` `AfterSideTurnEnd` 中 `if (side == CombatSide.Enemy) await PowerCmd.TickDownDuration(this);`
- 可被 `PaperPhrog`（`:40-44`）、`CrueltyPower`（`:45-50`）、`DebilitatePower`（`:51-55`）改写倍率——这是"倍率可被调整"的出处，但均不改取整位置。

### 2.4 Weak

`sts2/src/Core/Models/Powers/WeakPower.cs:22,24-33` 逐字：

```csharp
	protected override IEnumerable<DynamicVar> CanonicalVars => new global::_003C_003Ez__ReadOnlySingleElementList<DynamicVar>(new DynamicVar("DamageDecrease", 0.75m));

	public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
	{
		if (dealer != base.Owner)
		{
			return 1m;
		}
		if (!props.IsPoweredAttack())
		{
			return 1m;
		}
```

- 条件：**出手者持有该 power**（`dealer != base.Owner` → 1），**且** `props.IsPoweredAttack()`。
- 效果：`×0.75`（固定，与层数无关，层数同样是持续时间）。
- 递减：`:48-54` 同样在 `side == CombatSide.Enemy` 时 `TickDownDuration`。
- Shrink（同类出手倍率，供第 6 节对照）：`sts2/src/Core/Models/Powers/ShrinkPower.cs:18` `public const decimal damageDecrease = 30m;`、`:24` `Type => PowerType.Debuff`、`:95-106` `if (base.Owner != dealer) return 1m; if (!props.IsPoweredAttack()) return 1m; return (100m - base.DynamicVars["DamageDecrease"].BaseValue) / 100m;` → `0.7`。

### 2.5 阶段与顺序

`Hook.cs:2511-2557`：Additive（`:2515-2526`，`num += num2`）→ Multiplicative（`:2527-2538`，`num *= num3`）→ Cap（`:2539-2555`，取最小上限）→ `Hook.cs:1565` 下限 0。全部为 decimal 连算，**阶段之间不取整**。这也解释了 `IsPoweredAttack()` 为何在 64 处被复用（`Select-String -Pattern "IsPoweredAttack\(\)"` 命中 `sts2/src/**` 64 处，含 `VulnerablePower.cs:33`、`WeakPower.cs:30`、`StrengthPower.cs:21`、`ShrinkPower.cs:101` 等）。

### 2.6 旧后端的对应条件（供对照）

- `Legacy/State.cs:583-585`：`var isPoweredMove = (properties & DamageProperties.Move) != 0 && (properties & DamageProperties.Unpowered) == 0;` / `var damage = isPoweredMove ? Math.Max(0m, baseDamage + dealer.Strength) : Math.Max(0m, baseDamage);` ← **注意旧后端在加法阶段后就 `Math.Max(0m,…)` 夹一次**，sts2 只在末端 `Hook.cs:1565` 夹。
- `State.cs:586-589`：出手者的 `OutgoingDamageMultiplier`（Weak `shared-powers.spire:29` = 0.75、Shrink `:39` = 0.7），条件是 `amount != 0`。
- `State.cs:599`：`if (isPoweredMove && HasPowerRole(target, PowerRole.Vulnerable))`；`HasPowerRole` 定义在 `State.cs:679` `internal bool HasPowerRole(EntityState entity, PowerRole role) => entity.Powers.Keys.Any(id => Definition.Powers[id].Role == role);` ← **只看"存在该 role"，不检查层数 > 0**（对比 sts2 的 `target != base.Owner` + 旧后端自己的 `:614 if (amount <= 0) continue;`）。
- `State.cs:611-627`：受击者 `IncomingDamageModifiers`，此处才有 `amount <= 0` 守卫。

## 3. Q3 规划者反例在恢复源码里的真实结果

**题目**：基础伤害 5、攻击者 Weak（×0.75）、受击者 Vulnerable（×1.5）同时生效，且受击者 Block = 0、无其他修正。

### 3.1 恢复源码（sts2）逐步演算 → **5**

| 步 | 位置 | 算式 | 值 |
| --- | --- | --- | --- |
| 1 | `AttackCommand.cs:653` → `CreatureCmd.cs:240` | 进入 `Damage(..., amount: 5m, props: ValueProp.Move, ...)` | `5` |
| 2 | `Hook.cs:2515-2526`（Additive） | 无 Strength 层数 → `num += 0m` | `5` |
| 3 | `Hook.cs:2527-2538`（Multiplicative，逐 listener `num *= num3`） | `WeakPower.cs:22` 给 `0.75m`：`5m * 0.75m = 3.75m`——**此处不取整** | `3.75` |
| 4 | 同上 | `VulnerablePower.cs:22` 给 `1.5m`：`3.75m * 1.5m = 5.625m`——**此处不取整** | `5.625` |
| 5 | `Hook.cs:2539-2555`（Cap） | 无 `ModifyDamageCap` | `5.625` |
| 6 | `Hook.cs:1565` | `Math.Max(0m, 5.625m)` | `5.625` |
| 7 | `CreatureCmd.cs:265` → `Creature.cs:430-435` | Block = 0 → `blockedDamage = Math.Min(0, 5.625) = 0m`（`Block -= (int)0m`，格挡记账不受影响） | `blocked = 0` |
| 8 | `CreatureCmd.cs:266` | `Math.Max(5.625m - 0m, 0m)` → `ModifyHpLost`（无 hook 改动） | `5.625` |
| 9 | `CreatureCmd.cs:271` → **`Creature.cs:449`** | `(int)Math.Min(5.625m, 999999999m)` = `(int)5.625m` = **截断 → 5** | **`5`** |
| 10 | `Creature.cs:450,453` | `CurrentHp = Max(HP - 5, 0)`；`UnblockedDamage = 5` | 扣 **5** 点 HP |

乘法顺序不影响本例（两序都精确）：`5 × 0.75 = 3.75`、`× 1.5 = 5.625`；反向 `5 × 1.5 = 7.5`、`× 0.75 = 5.625`。

**结果 = 5**（20 HP → 15 HP）。这是**末端单次截断**。

### 3.2 旧后端（Legacy）逐步演算 → **4**

| 步 | 位置 | 算式 | 值 |
| --- | --- | --- | --- |
| 1 | `State.cs:585` | `isPoweredMove` 真 → `Math.Max(0m, 5 + 0) = 5` | `5` |
| 2 | `State.cs:586-589` | `Weak` 的 `OutgoingDamageMultiplier = 0.75`（`shared-powers.spire:29`）→ `5 * 0.75 = 3.75`（仍 decimal） | `3.75` |
| 3 | **`State.cs:598`** | `var damage = (int)decimal.Floor(ModifyCardDamageValue(...));` → `Floor(3.75) = 3` ← **第一次中途 Floor** | **`3`** |
| 4 | `State.cs:599-608` | 受击者有 `PowerRole.Vulnerable`（`HasPowerRole`，`State.cs:679`）→ `vulnerableMultiplier = 1.5m`（无 relic/power 调整） | `1.5` |
| 5 | **`State.cs:609`** | `damage = (int)decimal.Floor(3 * 1.5) = (int)decimal.Floor(4.5) = 4` ← **第二次中途 Floor** | **`4`** |
| 6 | `State.cs:611-627` | `incomingMultiplier = 1m`（无 `IncomingDamageModifiers`）→ `(int)Floor(4 * 1) = 4`（第三处 Floor，本例为恒等） | `4` |
| 7 | `State.cs:628` | `LimitDamageAmount(target, 4)` → 4 | **`4`** |

**结果 = 4**。

### 3.3 判定

- 规划者反例**成立**：同一局面两侧差 1（恢复源码 **5**，旧后端 **4**）。
- 差异的**唯一来源**是取整点：旧后端在出手倍率之后（`State.cs:598`）和受击倍率之后（`State.cs:609`）各 Floor 一次，恢复源码只在末端（`Creature.cs:449`）截断一次。
- 因此 [sts2-slice-value-extraction-2026-10-01.md](sts2-slice-value-extraction-2026-10-01.md) 差异 D 的原始表述"整数比倍率不会产生不同取整结果"**被来源证据明确证伪**——`0.75` 与 `1.5` 都是整数比（3/4、3/2），但组合后仍出差 1。评审 §2 的判决正确。
- **不得继承旧后端的双重 Floor**；实现阶段必须按 `Creature.cs:449` 的末端单次截断。

## 4. Q4 旧后端取整行对照

`core-the-spire/Core.TheSpire/Legacy/State.cs` 的伤害计算中共有 **三处** `decimal.Floor`，不是两处：

| 行 | 逐字引用 | 位置语义 |
| --- | --- | --- |
| `State.cs:598` | `var damage = (int)decimal.Floor(ModifyCardDamageValue(session, dealer, baseDamage, properties));` | 在 `ModifyCardDamageValue`（含 Strength 加法 + 出手者 `OutgoingDamageMultiplier`：Weak 0.75 / Shrink 0.7，`:585-589`）之后 → **中途 Floor #1** |
| `State.cs:609` | `damage = (int)decimal.Floor(damage * vulnerableMultiplier);` | 在 Vulnerable ×1.5 之后 → **中途 Floor #2** |
| `State.cs:627` | `damage = (int)decimal.Floor(damage * incomingMultiplier);` | 在受击者 `IncomingDamageModifiers` 连乘之后 → **中途 Floor #3**（无此类 power 时为 `×1m` 恒等，故此前未被注意） |

**与恢复源码的差异**：

| 维度 | 恢复源码（sts2） | 旧后端（Legacy/State.cs） |
| --- | --- | --- |
| 取整点数量 | **1 次**（末端） | **3 处**（`:598`、`:609`、`:627`），其中 `:598`/`:609` 在反例中生效 |
| 取整语义 | `(int)` 显式转换 = **向零截断**（`Creature.cs:449`） | `decimal.Floor` 后 `(int)`（先 Floor 再转 int）；因有 `Math.Max(0m,…)` 夹负，Floor 与 Truncate 等价 |
| 取整相对格挡/HP 损失 hooks 的位置 | 在格挡吸收与两阶段 `ModifyHpLost` **之后**（`CreatureCmd.cs:265/266/269` → `Creature.cs:449`） | 在格挡处理 **之前**（`CalculateDamage` 返回 int，格挡在别处扣） |
| 下限 0 | 只在末端一次（`Hook.cs:1565` + `CreatureCmd.cs:266`） | 加法阶段就先夹一次（`:585` `Math.Max(0m, …)`），末端 `LimitDamageAmount`（`:631-643`）再 `Math.Max(0, damage)` |
| Cap | 在乘法之后、下限之前（`Hook.cs:2539-2555`），作用于 decimal | `LimitDamageAmount`（`:631-643`）作用于 **int**，在全部 Floor 之后 |
| 数值后果 | 基础 5 + Weak + Vulnerable → **5** | 同局面 → **4** |

**修正上游记录**：[sts2-slice-value-extraction-2026-10-01.md](sts2-slice-value-extraction-2026-10-01.md) 差异 D 与 `SliceManifest.cs:151-152` 注释写"旧后端 Floor 两次（`State.cs:598` 与 `:609`）"，实际是**三处**（漏 `:627`）；"中途 Floor"这一定性仍然正确。

## 5. Q5 Power 0 层施加差异

### 5.1 两侧的置位点

**恢复源码**：置位语句在 `sts2/src/Core/Commands/PowerCmd.cs:144-147`（在 `Apply(choiceContext, power, target, amount, …)`，方法体起于 `:101`）：

```csharp
			if (target.Side == CombatSide.Player && power.Type == PowerType.Debuff)
			{
				power.SkipNextDurationTick = true;
			}
```

但**同一方法开头 `:103` 有**：

```csharp
		if (CombatManager.Instance.IsEnding || amount == 0m || !target.CanReceivePowers)
		{
			return;
		}
```

**旧后端**：`Legacy/State.cs:553-555`：

```csharp
        if (amount != 0 && (owner.IsEnemy && definition.SkipFirstTriggerWhenAppliedToEnemy
            || !owner.IsEnemy && definition.SkipFirstTriggerWhenAppliedToPlayer))
            owner.SkipNextPowerTriggers.Add(power);
```

消费点：sts2 `PowerCmd.cs:190-200`（`TickDownDuration`，由 `VulnerablePower.cs:63`、`WeakPower.cs:52`、`FrailPower.cs:39` 调用）；旧后端 `Legacy/Effects.cs:399` `if (owner.SkipNextPowerTriggers.Remove(powerId)) continue;`（跳过该 power 本次全部 trigger 效果，含 `decrementOnTrigger`）与 `Legacy/Environment.cs:834-835`。

### 5.2 对"施加 0 层"这一具体场景的判定：**两侧无可见差异**

- **sts2**：`amount == 0m` 在 `PowerCmd.cs:103` 直接 `return` → 不创建实例、不置位 `SkipNextDurationTick`。若玩家已有易伤/虚弱，该实例**完全未被触碰**，`SkipNextDurationTick` 保持 `false` → 下一个阵营回合结束时 `VulnerablePower.cs:59-65` → `TickDownDuration`（`PowerCmd.cs:190-200`）→ `SkipNextDurationTick` 为假 → 走 `else` 分支 `await Decrement(power)` → **递减**。
- **旧后端**：`amount = 0` → `:553` 条件假 → 不加入 `SkipNextPowerTriggers`；`newAmount`（`:533`）等于原值。回合结束触发时 `Effects.cs:399` 的 `Remove` 返回 false → 不跳过 → `decrementOnTrigger` 生效 → **递减**。
- 边界情形（输入层数非 0、被 hook 改成 0）：sts2 `ApplyInternal(target, 0m)` 在 `PowerModel.cs:566` `if (!(amount == 0m))` 判空而**不挂载**该实例，`:144-147` 只是把 skip 设在一个**从未挂载的临时实例**上，对已有 power 无影响；旧后端 `:519` 已把 `amount` 改成 0，`:534` 分支移除该 power。两侧仍无可见差异。

**∴ 上游差异 C 中"层数 0 的施加在两侧行为不同"的说法，在恢复源码里找不到支持**：`PowerCmd.cs` 的通用 debuff 规则**并不是"不看 amount"**，它有一条 `amount == 0m` 的入口守卫。

### 5.3 但存在另一处**真实可见**的差异：叠加时是否重新置位

| | 恢复源码 | 旧后端 |
| --- | --- | --- |
| 新建实例 | `PowerCmd.cs:78-83`：`power == null` → `Apply(...)`（`:101`）→ `:144-147` **置位** | `State.cs:545-555`：`amount != 0` 且定义有 flag → **置位** |
| 叠加到已有实例 | `PowerCmd.cs:78,84`：`FindExistingInstanceForStacking` 返回非 null（`VulnerablePower`/`WeakPower` 用默认 `PowerInstanceType.None`，见 `PowerModel.cs:144`）→ `ModifyAmount(...)`（`:215-271`）→ **全程没有 `SkipNextDurationTick` 赋值**（全仓库 `SkipNextDurationTick` 只有 `PowerCmd.cs:146/192/194` 与 `Doubt.cs:35`、`Shame.cs:37` 五处） | `State.cs:514-557` 是唯一入口，**每次 `amount != 0` 都 `Add`** → **置位** |

**具体可见场景（易伤，玩家侧）**：

1. 敌方回合 N：怪物对玩家施加易伤 2。两侧都置位 skip。
2. 回合 N 结束（`side == CombatSide.Enemy`）：两侧都跳过递减 → 玩家易伤仍为 **2**，skip 标志被消费。
3. 敌方回合 N+1：同一怪物再对玩家施加易伤 1（叠加）。
   - 恢复源码：走叠加路径 `ModifyAmount`，**不置位** → 玩家易伤 3，skip = false。
   - 旧后端：`ApplyPower` 中 `amount = 1 != 0` 且 `Vulnerable` 的 DSL 声明有 `skipFirstWhenAppliedToPlayer = true`（`shared-powers.spire:20`）→ **置位**。
4. 回合 N+1 结束：
   - 恢复源码：`TickDownDuration` 不跳过 → 递减 → 易伤 **2**。
   - 旧后端：`Effects.cs:399` 跳过 → 不递减 → 易伤 **3**。

**用户可见后果：旧后端的易伤（以及同样只在"施加当次"跳过的虚弱）会比恢复源码多持续一个回合，层数也多 1；在后续回合里那 1 层会继续把伤害从 ×1.5 生效到失效，因此总伤害与回合数都会被放大。** 这是 B 批迁移必须显式决定的行为差。

### 5.4 另外两处结构差异（不改变 5.2/5.3 的结论，但需记录）

1. **条件来源不同**：sts2 是命令层通用规则（`target.Side == CombatSide.Player && power.Type == PowerType.Debuff`，`PowerCmd.cs:144`），旧后端是每个 power 的 DSL 声明（`definition.SkipFirstTriggerWhenAppliedToPlayer`）。**在现有 base-spire 内容上两者恰好重合**：声明 flag 的只有 `Vulnerable`（`shared-powers.spire:20`）、`Weak`（`:28`）、`Frail`（`enemies.spire:191`），而 sts2 中只有 `FrailPower`、`VulnerablePower`、`WeakPower` 调用 `TickDownDuration`（`ShrinkPower.cs:83` 用 `PowerCmd.Decrement`、`NoDrawPower.cs:37` 用 `PowerCmd.Remove`，二者**都不读** `SkipNextDurationTick`）。所以差异只在"叠加"路径上显形（5.3），以及将来任何**新增**的玩家侧持续型 Debuff 上。
2. **跳过范围不同**：sts2 的 `SkipNextDurationTick` 只影响 `TickDownDuration`（即只跳过递减）；旧后端的 `SkipNextPowerTriggers` 在 `Effects.cs:399` 跳过该 power 当次的**全部** trigger 效果（含 `effect`/`triggerRuleEffect`）。对 `Vulnerable`/`Weak` 恰好只有 `decrementOnTrigger`，故等价；对同时有其它 trigger 效果的 power 不等价。

## 6. Q6 受控切片当前覆盖到哪

实现位置：`mod-the-spire/base-spire/BaseSpire/Slice/CombatSlicePackage.cs` 的 `DamageMonster`（`:223-265`）与 `DamagePlayer`（`:267-290`）。所有中间量都是 `FieldTypeSchema.Int`（`:228-231`、`:271-273`），即**一进入伤害计算域就是整数**。

### 6.1 已覆盖

| 情形 | 覆盖证据 |
| --- | --- |
| 加法阶段：出手者 Strength 每层 +1（玩家攻击怪物） | `:235-236` `SetLocal(hitDamage, Add(Param(hitAmount), CallPure(strengthOf, Param(hitAttacker))))`；`StrengthOf` 定义 `:148-161` |
| 加法阶段：出手者 Strength（怪物攻击玩家） | `:276-277` 同样结构（`strengthOf(struckAttacker)`） |
| 乘法阶段：受击者存在 Vulnerable 时 ×150% | `:239-245`：`If(Greater(hitVulnerable, 0), SetLocal(hitDamage, Divide(Multiply(hitDamage, 150), 100)))`；常数为 `SliceManifest.cs:139 VulnerableDamagePercent = 150` |
| 受击者易伤层数**只判存在**、不按层数放大 | `:241` 只做 `Greater(..., 0)`；`SliceManifest.cs:130-138` 注释明确"stacks are a duration that ticks down, not a magnitude"，并警告不得把旧后端 `VulnerableMultiplierPercentPerStack` 读成"每层 50%" |
| 格挡先吸收、再扣 HP、钳到 0 | `:246-254`（怪物侧）、`:278-286`（玩家侧） |
| 单次截断（在其整数包络内） | 唯一的除法/截断点是 `:242-245` 的 `Divide(..., 100)`；因底数（`Amount` + 整数 Strength）与 Block 均为整数，`floor(a) - b` 与 `floor(a - b)`（b 为整数）相等，故与恢复源码"末端单次截断"结果一致 |
| 死亡/胜利/失败 | `:257-265`、`:289-290` |
| Power 层数与 turn-end tick / 施加当次跳过 | `:334-361`（`ResolveOwnerTurnEnd` 用 `powerSkip` 跳过当次 `Ticks` 或 Ritual 授予）、`:179-220`（`ApplyPower`，含 `SkipNextTick` 形参） |

`CombatSlicePackage.cs:14-16` 的类注释自述已交付范围："damage with the recovered additive-then-multiplicative order and a single truncation"。

### 6.2 未覆盖（与取整/属性条件相关的部分）

| 未覆盖情形 | 证据 | 为什么重要 |
| --- | --- | --- |
| **Weak ×0.75 出手倍率** | `CombatSlicePackage.cs:20-21`："the `Unpowered` distinction and the Weak/Shrink outgoing multipliers are not part of the slice and are marked as gaps" | 正是规划者反例的一半；切片无法表达 |
| **Shrink ×0.7 出手倍率** | 同上 | 另一出手倍率；首个非整数比倍率与 Vulnerable 组合时取整点才可能显形 |
| **Unpowered 语义** | 同上；切片所有攻击都是 powered（`AttackCommand` 默认 `ValueProp.Move`） | 没有 `Unpowered` 就无从验证"仅 powered attack 才吃 Strength/Vulnerable/Weak"这条门控 |
| **非整数比组合导致的取整点差异** | 切片只有一个 ÷100 的定点运算（`:242-245`），整数域内不可能产生 `3.75`/`5.625` 这类中间值 | 切片**结构上无法**区分"中途取整"与"末端单次截断"，因此不能用作该判定的证据 |
| **玩家侧 Vulnerable** | `:274-275`："The player has no Vulnerable in the slice, so only the additive stage applies."（`DamagePlayer` 无易伤分支） | 玩家受击的乘法阶段无覆盖 |
| **出手者 Weak 的门控（dealer 侧）** | 切片 `strengthOf(attacker)` 直接取层数（`:235-236`、`:276-277`），没有任何 `IsPoweredAttack` 等价门控 | 属性条件"仅出手者的 powered attack"在切片里不存在，因为切片没有非 powered 攻击 |
| **格挡/HP 损失 hooks 改变金额后取整** | 切片无 `ModifyHpLost` 等价物 | 切片按"乘法后立即截断、再减格挡"实现；恢复源码是"减格挡、过 hooks、再截断"。整数包络内等价（见 6.1），一旦出现非整数中间值或 hook 加减会出现分离 |
| **Cap 阶段** | 切片无 `ModifyDamageCap` 等价物 | `Hook.cs:2539-2555` 的 Cap 在取整之前起作用 |
| **负 Strength** | 切片 `strengthValue` 局部声明为 `Int(0, 99)`（`:151`），而 `powerStacks` 是 `Int(-99, 99)`（`:78`）；`VulnerableStacksOf` 同样 `Int(0, 99)`（`:166`） | 层数为负时局部变量可能越出声明范围，属另一议题但与本清单相邻，实现阶段应确认 |

**结论**：受控切片**已覆盖**"整数输入 + 单次 ×1.5 + 整数格挡"下的加法→乘法顺序与单次截断；**未覆盖** Weak/Shrink 出手倍率、Unpowered、玩家侧易伤、Cap、非整数比组合——**规划者反例正好落在未覆盖区**，因此切片现状不能作为"取整点无差异"的证据，也不能用来关闭 R-DAMAGE。

## 7. 未确认项与搜索范围

### 7.1 未确认 / 未找到

1. **无未定位的关键环节**。第 1 节的最终 decimal→int 位置已定位并给出完整调用链；此前"只找到 `Hook.cs:1565`"的空缺已补齐。
2. **`sts2/src/Core/Commands/HealthCmd.cs` 不存在**——该目录（`CardCmd, CardPileCmd, CardSelectCmd, Cmd, CreatureCmd, DamageCmd, ForgeCmd, MapCmd, OrbCmd, OstyCmd, PlayerCmd, PotionCmd, PowerCmd, RelicCmd, RelicSelectCmd, RewardsCmd, SfxCmd, TalkCmd, ThinkCmd, VfxCmd`）无此文件；HP 损失统一由 `Creature.LoseHpInternal` 承担。**未找到**。
3. **`AttackCommand` 的 `CalculatedDamageVar` 路径未追到具体卡**：`CalculatedDamageVar.Calculate(target)`（`CalculatedDamageVar.cs:60`）实现在基类 `CalculatedVar`（`Core/Localization/DynamicVars/CalculatedVar.cs`），本次未展开逐字读取其乘法取整细节。它只影响 `PerfectedStrike` 一类"动态基础值"卡的**基础值**，不影响本报告结论（该值作为 `amount` 进入 `CreatureCmd.Damage` 后仍走同一取整链）。**标注为未展开，不影响结论**。
4. **未能运行任何行为验证**：按硬性边界禁止 `dotnet`，第 3 节是**源码逐步推演**，不是运行时差分证据。与 [review-followup-2026-10-01.md](review-followup-2026-10-01.md) §2 一致：这是数学/源码反例，运行时差分仍应由实现阶段的行为检查补上。
5. **多人（co-op）与 Osty 重定向路径未核对**：`CreatureCmd.cs:268` 的 `ModifyUnblockedDamageTarget`、`:283-286` 的 Osty 溢出伤害支路沿用同一 `LoseHpInternal`，因此取整位置相同，但未逐行演算。**标注为未核对**。
6. **`reference/sts2-sim/` 未读取**（按要求不作权威），因此本报告不含该目录的任何旁证。

### 7.2 搜索范围（关键词与路径）

- **路径**：`sts2/src/Core/Commands/`（含 `Commands/Builders/`）、`sts2/src/Core/Entities/Creatures/`、`sts2/src/Core/Hooks/`、`sts2/src/Core/ValueProps/`、`sts2/src/Core/Models/Powers/`、`sts2/src/Core/Models/`、`sts2/src/Core/Localization/DynamicVars/`、`sts2/src/**`（全文关键词扫）、`core-the-spire/Core.TheSpire/Legacy/{State.cs,Effects.cs,Environment.cs,Content.cs,SpireCombatBehaviorDslCompiler.cs}`、`mod-the-spire/base-spire/BaseSpire/{Slice/CombatSlicePackage.cs,Slice/SliceManifest.cs,ContentSource/*.spire}`。
- **关键词**：`ModifyDamage`、`ModifyDamageInternal`、`ModifyDamageHookType`、`ModifyDamageAdditive`、`ModifyDamageMultiplicative`、`ModifyDamageCap`、`IsPoweredAttack()`、`Unpowered`、`ValueProp`、`DamageBlockInternal`、`LoseHpInternal`、`DamageResult`、`UnblockedDamage`、`BlockedDamage`、`ModifyHpLost`、`HpLossHookPhase`、`CalculatedDamageVar`、`DamageCmd`、`AttackCommand`、`Floor`、`Round`、`Truncate`、`\(int\)`、`SkipNextDurationTick`、`TickDownDuration`、`Decrement`、`SkipFirstTriggerWhenAppliedToPlayer`、`skipFirstWhenAppliedToPlayer`、`skipFirstWhenAppliedToEnemy`、`SkipNextPowerTriggers`、`OutgoingDamageMultiplier`、`VulnerableMultiplierPercentPerStack`、`HasPowerRole`、`decrementOnTrigger`、`NoDraw`、`Shrink`、`Weak`、`VulnerableDamagePercent`、`DamageOrder`。

## 8. 实现阶段必须做出的判定（清单）

> 以下由实现方（.NET 泳道）与规划者决定；本报告只提供来源事实与出处。

1. **取整点唯一化**：伤害的 decimal→int 必须在**末端一次**完成，位置对应 `Creature.cs:449`。不得沿用旧后端 `State.cs:598`/`:609`/`:627` 的三处 Floor。判定产物：新后端选定"单一截断点"的书面决定 + 覆盖反例（基础 5 + Weak + Vulnerable = 5，非 4）的行为检查。
2. **取整语义选择**：来源是 `(int)` 隐式语义的**向零截断**（`Creature.cs:449`）。需明确写出实现用的是 Truncate 还是 Floor，并说明在金额已被夹到 ≥0 的前提下二者等价；同时确认所有上游钳制（`Hook.cs:1565`、`CreatureCmd.cs:266`）位置一致。
3. **取整与格挡/HP 损失 hooks 的先后**：来源顺序是 `ModifyDamage` →（`:1565` 下限 0）→ 格挡吸收（`CreatureCmd.cs:265`）→ `ModifyHpLost` BeforeOsty/AfterOsty（`:266`/`:269`）→ `LoseHpInternal` 截断（`:271`）。新后端必须决定是否保持"截断在 hooks 之后"，否则非整数中间值会分离。
4. **Cap 阶段位置**：来源在乘法之后、末端下限之前作用于 decimal（`Hook.cs:2539-2555`），旧后端作用于 int（`State.cs:631-643`）。需选定与来源一致的位置。
5. **属性门控**：Strength/Vulnerable/Weak 三者都以 `IsPoweredAttack()`（`ValuePropExtensions.cs:5-12` = `Move` 且非 `Unpowered`）为必要条件；Strength 与 Weak 判定**出手者**持有，Vulnerable 判定**受击者**持有。需决定 `Unpowered` 在 DSL/IR 中的表达方式（`AttackCommand.cs:336-340` 是来源侧的开关点）。
6. **Vulnerable 的"存在"判定**：来源是 `target == base.Owner` 且该 power 实例存在（层数只作持续时间）。旧后端 `HasPowerRole`（`State.cs:679`）不检查 `> 0`，而切片用 `Greater(..., 0)`。需统一为一种判定并给出反例（如层数为 0/负时）。
7. **skip-first 语义与范围**：需分别决定两件事——(a) 置位条件取 sts2 的"命令层通用 Debuff+玩家侧"还是旧后端的"per-power DSL flag"（现有内容上二者重合，见 5.4）；(b) **叠加时是否重新置位**（这才是 5.3 的真实可见差异，必须显式选择并写行为检查）。同时决定 skip 是"只跳递减"（sts2）还是"跳过该 power 当次全部 trigger"（旧后端 `Effects.cs:399`）。
8. **旧后端 bug 修复边界**（若 R-LEGACY/实现方要修）：`State.cs:553-555` 在 `newAmount == 0` 时仍会把已移除的 power 留在 `SkipNextPowerTriggers`（`:538` 先删、`:555` 后加）——需确认是否是缺陷，并按冻结后端"只做 bug 修复"的约定处理。
9. **受控切片扩围**：切片若要覆盖规划者反例，必须引入非整数中间量或有理数/定点表示；在此之前，不得用切片现状声称"取整点无差异"或关闭 R-DAMAGE。
10. **文档同步**：`SliceManifest.cs:146-155`（`DamageOrder` 注释）与 [sts2-slice-value-extraction-2026-10-01.md](sts2-slice-value-extraction-2026-10-01.md) 差异 D 需按本报告修正：取整位置已确认为 `Creature.cs:449`，旧后端 Floor 为三处，且差异 D 的"原表述不成立"要保留。

## 9. 复用与边界声明

- 本报告是 **2026-10-01 的只读源码证据快照**，不是运行时差分证据；所有结论可回到文中 `文件:行`。
- 未修改任何生产代码、内容包、测试或 schema；未执行 `dotnet`、未 `git add`/`commit`/`clean`。
- 与 R-DAMAGE 关闭条件的关系：第 1、3、4 项（取整位置、反例真实结果、旧后端对照）已由本报告满足来源侧要求；**运行时行为检查仍属实现阶段**，本报告不替代。
