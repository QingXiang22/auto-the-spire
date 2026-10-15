# R-DAMAGE 实现阶段：定点伤害链、Weak 与叠加置位裁定

> 历史证据（2026-10-03整理）：正文保留原时点结果，历史“下一步”不再作为当前派单指令。本报告归属W-06，未结项转入[当前清单](../../tofix/README.md)；移入records不表示修复或验收通过。逐份归属见[记录索引](../README.md)。

| 项目 | 内容 |
| --- | --- |
| 状态与日期 | 2026-10-01；**实现阶段已交付**，四条验收命令全绿（数字见 §4） |
| 任务 | [review-followup-2026-10-01.md](review-followup-2026-10-01.md) §3 **R-DAMAGE** 行："以 `sts2/` 确认最终取整位置与属性条件，覆盖 5 点伤害叠加 Weak/Vulnerable 等实际反例及 Power 0 层施加差异；区分受控切片已覆盖和未覆盖" |
| 上游证据 | [r-damage-source-evidence-2026-10-01.md](r-damage-source-evidence-2026-10-01.md)（§1 取整链、§3 反例、§4 旧后端对照、§5.3 叠加置位差异、§6 切片覆盖、§8 十项裁定清单） |
| 负责范围 | `mod-the-spire/base-spire/BaseSpire/Slice/`、`mod-the-spire/base-spire/BaseSpire.Slice.Tests/`，以及 `Legacy/State.cs` 的**一处 bug 修复**与其回归断言 |
| 未触碰 | 未改 `core-the-spire/Core.TheSpire/Engine/`、`docs/contracts/`、`net-the-spire/`、`ai-the-spire/`、`sts2/`、`reference/`；未 `git add`/`commit`/`clean`；未移动任何他人文件 |
| 旧后端边界 | 只修 `SkipNextPowerTriggers` 残留缺陷；**三处 Floor（`State.cs:598/:609/:627`）原样保留**（行为变更，不属 bug 修复） |

## 1. 交付与改动文件

| 文件 | 改动 |
| --- | --- |
| `mod-the-spire/base-spire/BaseSpire/Slice/CombatSlicePackage.cs` | 伤害链改为定点中间量 + 末端单次 `FixedToInt(Truncate)`；新增出手者 Weak ×0.75；`StrengthOf`/`VulnerableStacksOf` 合并为通用纯过程 `PowerStacksOf(Owner, PowerId)`；`ApplyPower` 叠加分支不再重新置位 `SkipNextTick`；类注释同步覆盖范围 |
| `mod-the-spire/base-spire/BaseSpire/Slice/SliceManifest.cs` | 新增 `WeakId`、`WeakDamageMultiplier = 0.75m`；`VulnerableDamagePercent = 150`（int）改为 `VulnerableDamageMultiplier = 1.5m`（decimal，单一数值来源）；`DamageOrder` 注释写入实现决定、Truncate 依据与未覆盖项；`Powers` 表新增 Weak（2 → 3 项）；修正 Vulnerable 条目"每层 +50%"的旧文字 |
| `mod-the-spire/base-spire/BaseSpire.Slice.Tests/SliceChecks.cs` | 新增 4 项检查（21 → 25）；新增 `DamageScenario`/`GivePower`/`PlayStrikeFor`/`TargetHp` 受控场景辅助；清单检查同步（3 powers、1.5m/0.75m、Weak 在册） |
| `core-the-spire/Core.TheSpire/Legacy/State.cs` | **bug 修复**：`ApplyPower` 的置位条件加 `newAmount != 0`，使"把最后一层减到 0"的施加不再把已移除的 power 重新塞回 `SkipNextPowerTriggers` |
| `core-the-spire/Core.TheSpire.Tests/CombatSliceTests.cs` | 在既有测试 `PlayerDebuffSkipsItsApplicationSideTurnEndAndTicksAfterEnemySide` 内补 2 条断言作为回归（**不新增测试方法**，故执行数仍为 190） |
| `docs/review/tofix/sts2-slice-value-extraction-2026-10-01.md` | 按证据 §8.10 给差异 D 的**旧表述段落**加一行"已被上一条更正取代，保留原样"标记（不改历史内容） |

## 2. 关键裁定与理由

### 2.1 取整点唯一化（证据 §8.1、§8.2、§8.3）

- **决定**：伤害链全程在引擎定点域（`FieldTypeSchema.Fixed` + `NumericProfile`）完成；**只在链路末端转换一次**，规则显式写 `FixedRounding.Truncate`。
- **理由**：来源全链路只有 `Creature.cs:449` 一次 `(int)`（向零截断）；因上游两次 `Math.Max(0m, …)` 夹到非负，Truncate 与 Floor 在此等价，代码里写 Truncate 与来源字面一致。切片无 Cap/`ModifyHpLost` 等价物，故"末端"在切片里就是乘法之后、格挡与扣血之前；对整数格挡，`truncate(a) - b` 与 `truncate(a - b)` 相等，这是该位置可用的理由（下文 §3 未覆盖项记录了它与来源的完整顺序差）。
- **实现形态**：`base → IntToFixed → + IntToFixed(Strength) → ×[Weak] → ×[Vulnerable] → Max(0m) → FixedToInt(Truncate)`，中间**没有**任何 int 往返；`IntToFixed(FixedToInt(x))` 这种中途取整只出现在我的变异实验里（§5）。

### 2.2 Weak 纳入切片（证据 §6.2 未覆盖项）

- 新增 `base-spire:weak`（来源 `shared-powers.spire:25-29`，`WeakPower.cs:22,24-33`），出手者 ×0.75，仅在**出手者**持有且层数 > 0 时生效——与 Strength（出手者）和 Vulnerable（受击者）的属性门控方向一致。
- **未补 Shrink ×0.7**（交付物标为可选）：它是同一形态的第二条出手倍率，源码出处已记录（`shared-powers.spire:38-39`、`ShrinkPower.cs:95-106`），列入 §3 未覆盖项而非用半个实现充数。加入时的唯一额外风险见 §6 残余风险第 2 条（与 Weak 同时存在且宿主改过 `NumericProfile` 中点规则时，定点量化可能翻过整数边界）。

### 2.3 叠加时是否重新置位 skip（证据 §8.7b，**必须显式选边**）

- **选择：跟随恢复源码 sts2——叠加到已有实例不重新置位。**
- **理由**：`PowerCmd.cs:78-84` 在已有实例时走 `ModifyAmount`（`:215-271`），该路径**全程没有** `SkipNextDurationTick` 赋值（全仓库该字段只有 `PowerCmd.cs:146/192/194` 与 `Doubt.cs:35`、`Shame.cs:37` 五处命中）；`RitualPower` 的 `_wasJustAppliedByEnemy` 也只在 `AfterApplied`（`RitualPower.cs:36-43`，仅由 attach 路径 `PowerCmd.cs:155` 调用）里置位。旧后端 `State.cs:553-555` 每次 `amount != 0` 都置位，后果是易伤/虚弱比来源**多持续一个回合且多 1 层**（证据 §5.3）。
- **检查**：`R-DAMAGE stacking a power does not re-arm its skipped first turn end`。切片里唯一带 skip 的回合结束行为是 Ritual，因此受控场景把 DampCultist 的 `MoveIndex` 置回 0 重放首次 Incantation：叠加后 Ritual 2×5=10 层，**当回合结束必须按 10 层授予 Strength**；旧行为会再次置位 skip 而授予 0 层。
- 变异验证：把 `SetField(powerSkip, Param(SkipNextTick))` 加回叠加分支后该检查失败（`expected '10', got '0'`），证明检查确实区分这两侧。
- **仍未覆盖的相邻裁定（明确记录，不在本次改动内）**：(a) skip 的**置位来源**在切片里仍是"每次施加由调用方显式传入"（内容声明式，旧后端形态），而非 sts2 的命令层通用规则（`PowerCmd.cs:144` 玩家侧 Debuff）；现有内容上两者重合（卡片施加到怪物一律 `skip=false`，Ritual 自带 skip），一旦加入**玩家侧持续型 Debuff**，调用方必须按 `PowerCmd.cs:144` 传 `skip=true`。(b) skip 的**范围**在切片里只覆盖"当次回合结束行为"，sts2 也只跳 `TickDownDuration`，而旧后端 `Effects.cs:399` 跳过该 power 当次全部 trigger——两者在 Vulnerable/Weak/Ritual 上等价。
- **附：任务行点名的"Power 0 层施加差异"本轮据证据 §5.2 判定为无可见差异**：sts2 在 `PowerCmd.cs:103` 对 `amount == 0m` 直接返回（不建实例、不置位），旧后端 `State.cs:553` 的 `amount != 0` 守卫使之同样不置位、下一次回合结束两侧都会递减。因此**不为该场景新增行为检查**；仅记录形状差异——切片的 `ApplyPower(Stacks=0)` 会创建一个 0 层实例（§3 末行），对伤害无影响。

### 2.4 Vulnerable"存在"判定（证据 §8.6）

- **选择：层数 > 0 才算存在**（切片原判定保留）。来源是"实例存在即生效"，而来源侧 `amount == 0` 时根本不会挂载实例（`PowerCmd.cs:103`、`PowerModel.cs:566`），层数到 0 即移除；因此 `> 0` 是来源语义在切片（允许建 0 层实例）下的保守等价写法。
- 与旧后端 `HasPowerRole`（`State.cs:679`，不看层数）的差异**保留不改**：那是旧后端的行为变更，超出 R-DAMAGE 授权。检查里用 0 层实例把这条边界钉住（0 层不乘）。

### 2.5 旧后端 `SkipNextPowerTriggers` 残留：**判定为缺陷，已修**

- **缺陷成立**：`State.cs:534-542` 在 `newAmount == 0` 时把 power 从 `MutablePowers` 与 `SkipNextPowerTriggers` 成对移除（`:538`），但 `:553-555` 只判 `amount != 0`，于是**同一次调用的后半段**又把刚删除的 id 加回集合。
- **严重性（诚实评估）**：在当前旧后端语义下它**行为惰性**——两个消费点都要求 power 存在（`Effects.cs:393` 遍历 `owner.Powers.Keys`；`Environment.cs:827` 先 `TryGetValue`），而旧后端每次施加都会重新置位，所以后续"再次获得该 power"时这个陈旧条目会被幂等的 Add 掩盖。它仍然破坏"trigger 集合只提及存活 power"的不变式，并使等价状态的状态哈希/快照可能不同；一旦施加路径改成 sts2 的"只在新实例置位"，这个陈旧条目就会**错误地跳过下一次真实的回合结束**。属应修的缺陷，且修复面最小。
- **修复**：`if (newAmount != 0 && amount != 0 && (…))`，并加注释说明"每次施加都置位"是保留的行为差、不属 bug 修复范围。
- **回归**：`PlayerDebuffSkipsItsApplicationSideTurnEndAndTicksAfterEnemySide` 末尾追加：把玩家易伤从 1 施加 −1 归零后，断言 power 已移除且 `SkipNextPowerTriggers` 不再包含 Vulnerable。**改前实测失败**（`189 passed / 1 failed / 190 executed`，`System.InvalidOperationException: Expected false.`），**改后 190/190 通过**。
- **未改**：三处 `decimal.Floor`（`State.cs:598/:609/:627`）与"每次施加都置位"的叠加语义——见 §3、§6。

## 3. 覆盖与未覆盖

| 项 | 状态 | 证据 / 出处 |
| --- | --- | --- |
| 末端单次截断（5 + Weak + Vulnerable = 5，非 4） | **已覆盖** | `R-DAMAGE base 5 with Weak and Vulnerable truncates once at the end, not in the middle`（另用 9 → 10 钉住相反方向，两个方向都排除"中途取整"的两种顺序） |
| 定点中间量（不使用整数老路） | **已覆盖** | 同上：若把任意一处改成整数往返，该检查立即失败（§5 M1） |
| 易伤层数是持续时间、不是倍率 | **已覆盖** | `R-DAMAGE Vulnerable and Weak stacks are a duration, not a multiplier`：2 层与 3 层伤害相同、0 层不乘（Weak 侧同样有 2 层与 0 层对照） |
| 玩家侧 Vulnerable（受击乘法阶段） | **本次补上** | `R-DAMAGE the player takes the same chain…`：前排 9 ×0.75×1.5 = 10.125 → 10；若中途取整则为 9，检查会失败 |
| 出手者 Weak 的 dealer 侧门控 | **已覆盖** | 同上两条检查（玩家攻击怪物、怪物攻击玩家两个方向） |
| 叠加不重新置位 skip（§5.3 差异） | **已覆盖** | `R-DAMAGE stacking a power does not re-arm its skipped first turn end` |
| 旧后端残留 skip 缺陷 | **已修 + 回归** | §2.5 |
| Cap 阶段（`ModifyDamageCap`） | **未覆盖** | 切片无等价物；来源位置在乘法之后、下限之前作用于 decimal（`Hook.cs:2539-2555`），实现时插入点已在 `DamageChain` 注释中标出 |
| `Unpowered` 门控 | **未覆盖** | 切片所有攻击都是 `ValueProp.Move`（powered）；来源 `ValuePropExtensions.cs:5-12`。即"Strength/Weak/Vulnerable 只作用于 powered attack"这条必要条件在切片里仍不可表达 |
| Shrink ×0.7 | **未覆盖（可选）** | 出处 `shared-powers.spire:38-39`、`ShrinkPower.cs:95-106`；与 Weak 同形态，加入成本一行 + 一处清单项 |
| `ModifyHpLost` hooks / 格挡后取整顺序 | **未覆盖** | 来源是"减格挡 → hooks → `LoseHpInternal` 截断"（`CreatureCmd.cs:265-271`），切片是"截断 → 减格挡 → 扣血"。整数格挡下二者等价（`truncate(a) − b == truncate(a − b)`，且 `a<b` 时两侧都记 0 点 HP 损失），非整数格挡或将来加入 hook 后会分离 |
| 负 Strength / 负层数 | **部分改善，未覆盖检查** | `PowerStacksOf` 结果与局部槽位放宽为 `Int(-99, 99)`，负 Strength 现在会进入定点链并由末端 `Max(0m)` 夹住（来源 `Hook.cs:1565`），但切片没有任何施加负 Strength 的路径，因此无行为检查 |
| 玩家侧 power 的回合结束 tick | **未覆盖（既有缺口）** | `ResolveOwnerTurnEnd` 只由 `EndEnemyTurn` 对怪物调用，玩家侧易伤/虚弱不会递减；检查里已用 `"player-side powers do not tick in the slice yet"` 断言把这一事实钉住，避免误读为"已验证" |
| 伤害事件的公开读数（`DamageDealt`/`PlayerDamaged` 载荷） | **未覆盖** | 引擎当前只在 `Scheduler.Emit` 内分发给订阅者，没有可查询的事件账本，切片检查只能以 HP 为观测量；公开事实映射属 N 批（`N-02`） |
| 0 层施加创建实例的差异 | **未覆盖** | 切片 `ApplyPower(Stacks=0)` 会创建 0 层实例，sts2 在 `PowerCmd.cs:103` 直接返回；对伤害无影响（`>0` 判定），但状态形状不同 |

## 4. 命令、返回码与执行数（改前 / 改后）

| 命令 | 改前 | 改后 |
| --- | --- | --- |
| `dotnet build AutoTheSpire.sln` | 0 警告 / 0 错误，exit 0 | 0 警告 / 0 错误，exit 0 |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | `All 21 slice checks passed.` exit 0 | `All 25 slice checks passed.` exit 0（+4 R-DAMAGE） |
| `dotnet run --project core-the-spire/Core.TheSpire.Tests` | `190 passed / 0 failed / 190 executed.` exit 0 | `190 passed / 0 failed / 190 executed.` exit 0（同数：回归断言加在既有测试方法内） |
| `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` | `All 50 engine checks passed.` exit 0 | `All 50 engine checks passed.` exit 0（未改 Engine） |

中途证据（按同一份验收命令跑，失败即预期）：

| 实验 | 命令 | 结果 |
| --- | --- | --- |
| Legacy 回归断言加在修复之前 | `dotnet run --project core-the-spire/Core.TheSpire.Tests` | `FAIL CombatSliceTests.PlayerDebuffSkipsItsApplicationSideTurnEndAndTicksAfterEnemySide: Expected false.` → `189 passed / 1 failed / 190 executed.` exit 1 |
| M1 伤害链插入中途取整（模拟旧后端双重 Floor） | slice 检查 | `R-DAMAGE base 5 …`: `expected '33', got '34'`（即 4 点而非 5 点伤害）exit -532462766 |
| M2 叠加分支恢复重新置位 | slice 检查 | `R-DAMAGE stacking a power…`: `expected '10', got '0'` exit -532462766 |
| M3b 脆弱倍率改为随层数增长（2 层仍 1.5） | slice 检查 | `R-DAMAGE Vulnerable and Weak stacks…`: `three stacks deal exactly what two stacks deal: expected '29', got '28'` exit -532462766 |

> 说明：M1/M2/M3b 是**临时变异**，仅用于证明检查能区分错误，随后已逐字还原（还原后重跑 slice 25/25 通过）。

定点量化的独立核算（PowerShell 脚本，非仓库资产）：在 base+Strength 取遍 `0..999 × 0..30`（31,000 组）下，比较"每步 `Math.Round(…, 2, ToEven)` 量化 + 末端截断"与"精确 decimal 连乘 + 末端截断"：
`weak*vuln`、`shrink*vuln` 与 `weak*shrink*vuln` 均为 0 处不一致；前两组在 `AwayFromZero` 下也 0 处不一致，但 `weak*shrink*vuln + AwayFromZero` 有 403/31,000 处不一致（scale 提到 3 后归零）。这正是切片断言"宿主用 scale 2 + ToEven"的原因（见 §6.2）。

## 5. 契约与行为变化

| 变化 | 影响面 |
| --- | --- |
| 切片包新增过程 `PowerStacksOf`，移除 `StrengthOf`/`VulnerableStacksOf` | 包内私有声明，包身份哈希随内容变化；`base-spire@combat-slice-v1` 的内容哈希因此改变（测试只断言哈希长度为 16，未固定具体值） |
| 切片伤害链由 int 运算改为 Fixed 运算，输出等价于"精确 decimal + 末端截断" | 所有既有伤害断言不变（Strike 6、Bash 8、易伤 ×1.5 = 9、Ritual 加成等 21 项全过） |
| `SliceManifest.VulnerableDamagePercent`（int 150）→ `VulnerableDamageMultiplier`（decimal 1.5m） | 破坏 manifest 的源码级 API；仓库内引用只有切片与其测试，均已同步 |
| `SliceManifest.Powers` 2 → 3 项（新增 Weak） | 清单计数检查同步 |
| 切片 `ApplyPower` 叠加时不再重新置位 `SkipNextTick` | 语义变化，**有行为检查**；对现有内容只影响 Ritual 的重复施加场景 |
| `Legacy/State.cs` 置位条件加 `newAmount != 0` | 旧后端：把 power 减到 0 的施放不再留下 trigger 残留；其余路径（正施加、叠层）逐字不变。`190/190` 复验无回归 |
| 未改：旧后端三处 Floor、旧后端叠加置位、旧后端 `HasPowerRole` 不判层数 | 属行为变更，冻结后端只做 bug 修复，明确不做 |

## 6. 残余风险

1. **切片伤害结果依赖宿主的 `NumericProfile`**（新增检查已钉住当前假设）：`Fixed` 运算每步按 `NumericProfile.FixedScale` + `Rounding` 量化。默认 `(2, ToEven)` 对切片当前倍率集合（1.5 / 0.75）在所有整数 base 与 Strength 上不改变末端截断（§4 核算）；但宿主若改成 `AwayFromZero` 或更粗 scale，量化可能把中间值推过整数边界——**未来加入 Shrink（0.7）时在 `AwayFromZero` 下确有此风险**（核算 403/31,000）。缓解：保持 `(2, ToEven)`，或加入 Shrink 前把 scale 提到 ≥3，或按来源顺序把转换挪到 hooks 之后（Cap/`ModifyHpLost` 落地时一并处理）。
2. **cap 与 `ModifyHpLost` 缺失使"顺序"只在小范围内等价**：当前等价性依赖"格挡为整数且无 hook 改变金额"。加入非整数格挡、cap 或 HP 损失 hook 时必须把转换挪到链路最后（来源 `CreatureCmd.cs:265-271` → `Creature.cs:449`），`DamageChain` 注释已标出插入点。
3. **`Unpowered` 缺失**：属性门控无法在切片中被证伪；"仅 powered attack 才吃 Strength/Weak/Vulnerable"这条必要条件目前只能靠切片"所有攻击都是 powered"的声明成立。
4. **玩家侧持续型 power 的回合结束 tick 缺失**：玩家易伤/虚弱永不递减，任何以玩家侧 debuff 为对象的后续检查都会读到与来源不同的持续时间；`ResolveOwnerTurnEnd` 目前只遍历怪物。
5. **事件载荷不可读**：伤害数值只能通过 HP 观察；`DamageDealt`/`PlayerDamaged` 的 `Amount` 载荷没有查询入口，本轮无法断言"记账金额 == 扣血金额"（实现上两者同源，但无检查）。
6. **旧后端的行为差仍在**：三处 Floor（`:598/:609/:627`）与"叠加即重新置位"都保留。任何把 base-spire 迁到新引擎的内容迁移必须在迁移侧按本报告处置，不能期待旧后端改变。

## 7. 下一批依赖与交接建议

1. **R-LEGACY / 内容迁移方**：本报告 §2.5 的修复已在 `Legacy/State.cs` 落地（一处条件加 `newAmount != 0` + 回归断言），请按冻结后端约定复核；**不要**顺手把三处 Floor 合成一处——那是行为变更，应由迁移任务在**新引擎侧**完成。
2. **B 批迁移（B-05/B-06）**：切片现在有了定点伤害链的样板；迁移时应把 `Unpowered`、Shrink、cap、`ModifyHpLost` 一并按来源顺序落到新引擎，而不是复制切片的三处简化。
3. **N 批（N-02/N-03）**：公开事实需要伤害事件载荷；引擎当前无事件账本，映射 `DamageDealt`/`PlayerDamaged` 前需先明确"公开读数从状态还是从事件取"。
4. **规划者**：证据 §8 的十项中，第 1、2、3（部分）、5（部分）、6、7b、8、9、10 已在本轮裁定（见 §2）；仍未裁定的是 7a 的**置位来源是否改为命令层通用规则**（当前为内容声明式）——建议与"玩家侧 debuff 进入切片"同一批决定。
5. **文档**：`SliceManifest.DamageOrder` 与差异 D 已同步（§1），差异 D 的旧表述段落只加了取代标记，未改写历史。

## 8. 复用与边界声明

- 本报告的实现与检查均可回到命令与输出重跑；变异实验不是仓库资产，只作为"检查能区分错误"的证据记录。
- 未修改 `docs/contracts/`、`net-the-spire/`、`ai-the-spire/`、`sts2/`、`reference/`；未扩 Engine 能力；未 `git add`/`commit`/`clean`。
- 工作区中的其他未提交改动（含 `Legacy/` 整体、`Engine/`、协议资产等）一律未触碰、未回退；`CombatSliceTests.cs` 在我改动前已是他人在途修改状态，本报告只声明其中"追加 2 条断言"这一处。

## 9. 收尾核正（监督复核项的两点答复）

1. **切片计数：25 是新读数，21 只是"改前"基线列，不是 4 条新检查没注册。**`SliceChecks.RunAll()` 顺序调用 `DeclarationChecks` / `InitialStateChecks` / `CombatChecks`，4 条新检查都在 `CombatChecks` 内，末次运行逐行打印如下（`dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests`，exit 0）：

   ```text
   ok   R-DAMAGE base 5 with Weak and Vulnerable truncates once at the end, not in the middle
   ok   R-DAMAGE Vulnerable and Weak stacks are a duration, not a multiplier
   ok   R-DAMAGE the player takes the same chain: attacking Weak and player Vulnerable
   ok   R-DAMAGE stacking a power does not re-arm its skipped first turn end
   All 25 slice checks passed.
   ```

   §4 那张表是 **改前 / 改后两列**：改前 `All 21 slice checks passed.`，改后 `All 25 slice checks passed.`。

2. **旧后端三处取整点原样保留，本次只做 bug 修复。**当前行号（`Legacy/State.cs`，因修复处新增 5 行注释整体 +5，与预检一致）：

   | 当前行 | 语句 | 状态 |
   | --- | --- | --- |
   | `:603` | `var damage = (int)decimal.Floor(ModifyCardDamageValue(…));` | 未改 |
   | `:614` | `damage = (int)decimal.Floor(damage * vulnerableMultiplier);` | 未改 |
   | `:632` | `damage = (int)decimal.Floor(damage * incomingMultiplier);` | 未改 |

   本次在 `Legacy/State.cs` 的唯一改动是 `ApplyPower` 的置位条件（现 `:558`）：`if (newAmount != 0 && amount != 0 && (…))`，另加 4 行注释说明"每次施加都置位"属保留的行为差。`decimal.Floor` 在 `State.cs` 中还有一处 `:872`，位于 `ModifyBlockGain` 的格挡路径、不在 `CalculateDamage` 伤害链内，同样未改。

3. **四条验收命令在最终代码状态下全绿**（末次运行）：build `0 警告 / 0 错误` exit 0；slice `All 25 slice checks passed.` exit 0；legacy `190 passed / 0 failed / 190 executed.` exit 0；engine `All 50 engine checks passed.` exit 0。文档检查 `python tools/doc-check/check_docs.py .`：82 份 Markdown、0 问题；`git diff --check` exit 0（仅既有 LF/CRLF 提示）。
