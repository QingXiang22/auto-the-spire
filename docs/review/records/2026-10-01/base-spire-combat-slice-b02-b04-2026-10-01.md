# base-spire 受控战斗切片 B-02 至 B-04 交接

> 历史证据（2026-10-03整理）：正文保留原时点结果，历史“下一步”不再作为当前派单指令。本报告归属W-01、W-06，未结项转入[当前清单](../../tofix/README.md)；移入records不表示修复或验收通过。逐份归属见[记录索引](../README.md)。

| 项目 | 内容 |
| --- | --- |
| 状态与日期 | 2026-10-01；**B-01 至 B-04 全部交付**，共同验收的样例侧条件已具备 |
| 负责范围 | `mod-the-spire/base-spire/BaseSpire/Slice/`、`BaseSpire.Slice.Tests/`、`core-the-spire/Core.TheSpire/Engine/`（4 处新增原语/规则）、`Core.TheSpire.Engine.Tests/`（1 项回归） |
| 前置交接 | [B-01](base-spire-combat-slice-b01-2026-10-01.md)、[数值来源核对](sts2-slice-value-extraction-2026-10-01.md)、[DampCultist HP 缺陷](../../archived/damp-cultist-hp-range-inversion-2026-10-01.md) |
| 未触碰 | 旧 DSL 内容与旧后端、`net-the-spire/`、`ai-the-spire/`、他人未提交改动；未 git commit |

## 1. 执行命令与结果

| 命令 | 返回码 | 结果 |
| --- | --- | --- |
| `dotnet build AutoTheSpire.sln` | 0 | 0 警告 / 0 错误 |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | 0 | **All 21 slice checks passed.**（B-01 9 项 + B-02 6 项 + B-03 2 项 + B-04 4 项） |
| `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` | 0 | **All 32 engine checks passed.** |
| `python tools/doc-check/check_docs.py .` | 0 | 0 problem |

## 2. B-02 交付（数值与战斗结算）

- **费用/能量**：`PlayCard` 的合法性 = 当前能量 ≥ 卡牌费用；合法候选未变时提交才会执行。
- **伤害**：按恢复出的顺序实现——**加法（力量）→ 乘法（易伤）→ 护甲吸收 → 生命，末端单次截断**。
  证据：`sts2/src/Core/Hooks/Hook.cs:2515-2538`（Additive → Multiplicative → Cap）、`:1565`（下限 0）；
  `StrengthPower.cs:15-26`（+1/层，仅出手者的 powered attack）；`VulnerablePower.cs:22`（`DamageIncrease = 1.5m`）。
- **易伤是固定 ×1.5，层数只表示持续回合**（踩坑记录：我最初写成"每层 +50%"，被 `B-02 Bash applies Vulnerable…` 检查抓出，见第 5 节）。
- **护甲**：吸收量 = min(护甲, 伤害)，剩余进入生命；生命下限 0。
- **Power**：`ApplyPower`（不存在则创建、存在则叠层并刷新标记）、`GainStrength`、
  `ResolveOwnerTurnEnd`（Ritual 在**其拥有者回合结束**按层数授予力量，且**施加当次跳过**——`RitualPower.cs:45-57`；可 tick 的 Power 减 1 层）。
- **敌人行为**：两个邪教徒数据驱动（`FirstMoveRitual` + `StrikeDamage` 字段），首招 Incantation（Buff）→ 之后永久 DarkStrike；
  遭遇顺序 Calcified 在前（`CultistsNormal.cs:11-12,23-24`）。
- **洗牌**：抽牌堆空时把弃牌堆整体移回并用**包声明的命名流** `base-spire:combat-draw` 洗牌（确定性，进身份）。
- **胜负**：单个怪物死亡即移出存活集合（不再成为目标），**最后一个死亡**才设 `Victory`；玩家生命归零设 `Defeat`。终局是声明的 terminal，不是故障。
- **回合开始**：回合数 +1 → **清空本回合出牌历史**（`Environment.cs:760-772` 的顺序）→ 能量回满 → 护甲清零 → 抽牌 → 发 `TurnStarted`。

## 3. B-03 交付（选择与牌生命周期）

- **出牌区（`InPlay`）**：打出的牌先离开手牌进入出牌区，效果全部结算完才落到最终区域。
  因此 Headbutt 在自身选择期间**不可能选中自己**（检查项 `B-03 …`），且"当前弃牌堆里有什么"在选择时是良定义的。
- **Headbutt**：伤害 → 若弃牌堆非空则 `RequestDecision(EntityRef, 候选=弃牌堆, 1..1)` → 把选中实例移到抽牌堆**顶**（`InsertPosition.Start`），
  弃牌堆空则**不发起选择**（`Headbutt.cs:29,32` 的 null 检查）。选择提示文案采用恢复字符串（`localization/eng/cards.json:531`）。
- **续接语义**：暂停点在指令之后，应答只写入结果槽，**不重放已结算的伤害与已扣的费用**（检查项断言能量与生命都不变）。
- **实例身份保留**：被置顶的是同一个 `InstanceId`，不是副本。

## 4. B-04 交付（当前公开事实）

新增两个引擎原语与一条投影规则：

| 变更 | 理由 |
| --- | --- |
| `expr.collectionIndex`（`CollectionIndex(collection, item)` → Int，非成员返回 -1） | 包要能声明"我把它放到了位置 0"这种**绝对位置**，而引擎不该替包猜玩家知道什么 |
| `expr.entityScope`（`EntityScope(entity)` → Scope） | 创建依附于某实例的对象（如 Power）时，之前的 IR **无法取得实例所在作用域**，只能落到当前帧作用域 |
| `FieldTypeSchema.AnyRef()` | 让"接受任意实体引用"的共享助手（Power 助手要同时接受玩家与怪物）可以表达；检查器已有 `TargetId < 0` 语义 |
| 投影规则：**只经由"身份不可见"区域可达的实例不进投影** | 否则 draw pile 里的卡会作为实例列出，**泄露隐藏区域的成员与顺序** |

切片的公开事实（全部由包声明，引擎不推断）：

| 机制 | 字段/载体 | 来源 | 检查 | 证据 |
| --- | --- | --- | --- | --- |
| 资源（生命/护甲/能量） | `Player.Hp/.MaxHp/.Block/.Energy/.MaxEnergy` | 卡片与回合开始规则（`State.cs:318`） | `B-04 a hidden pile leaks…` | 投影里可见 |
| 意图 | `Monster.NextMove`（Incantation/DarkStrike） | `DampCultist.cs:69-74` | 同上 | 首回合即可见 Incantation；行动后置为 DarkStrike |
| Power 与层数 | `Power.DefinitionId/.Stacks/.Ticks/.SkipNextTick` | `RitualPower.cs`、`VulnerablePower.cs` | `B-02 both cultists open with Ritual…` | 投影里可见（含 Ritual 2/5 与后来的 Strength 2/5） |
| 已知成员与**绝对位置** | `KnownCard { CardId, Pile, Position }`，位置由 `CollectionIndex(drawPile, card)` 计算 | `Headbutt.cs:29,32` | `B-04 the first snapshot already states a known absolute position` | Headbutt 之后**第一份**快照即得 `{strike, draw, 0}`，无需历史 |
| 抽牌撤销位置 | `DrawCards` 抽到该卡时 `RevokeKnown` | 同上 | `B-04 drawing a known card revokes its position` | 事实条目归零 |
| 洗牌撤销位置 | `DrawCards` 洗牌分支 `ClearKnownCards` | 同上 | `B-04 a reshuffle voids every known position` | 事实条目归零且命名流计数 > 0 |
| 隐藏牌序不外泄 | `DrawPile.IdentityVisible/OrderVisible = false` + 投影规则 | 设计约束（不透传内部牌序） | `B-04 a hidden pile leaks neither…` | 只报 Size=5，成员为空；draw pile 的卡不出现在实例列表；手牌 5 张全能看见 |

## 5. 过程中发现并修正的问题

1. **引擎：`ForEach` 不是快照**（B-01 时发现，已修 + 回归检查）：`CollectionMembers` 返回内部 `List`，导致"移动整手牌"只处理 5 张中的 3 张。
2. **引擎：`Emit`/`CreateEntity`/`SetTerminal` 的字段引用被误按成员访问拒绝**（已修，拆成 `ResolveFieldRef` / `ResolveEntityField`）。
3. **引擎：数值溢出泄漏裸 `OverflowException`**（已修，统一为 `ArithmeticOverflow` 故障）。
4. **切片：易伤倍率写错**（每层 +50% → 固定 ×1.5）。**这是我自己的实现错误，被检查项抓出**；权威依据见上表。
5. **切片：回合开始未清空出牌历史**，导致同一实例第二次被打出时 `MembershipConflict`（已修，并加了来源注释）。
6. **切片：Power 助手参数被声明为 `RefTo(player)`**，怪物调用时被类型检查拒绝（→ `AnyRef()`）。
7. **内容缺陷（非本批，已报告）**：DampCultist HP 两档写反，见 [damp-cultist-hp-range-inversion-2026-10-01.md](../../archived/damp-cultist-hp-range-inversion-2026-10-01.md)。

## 6. 未做与残余风险

1. **不做的部分**：`ValueProp.Unpowered` 区分、Weak/Shrink 的出手倍率、卡牌升级的运行期表达（本批只冻结了基础/升级数值）、护甲增益修正、抽牌上限溢出、`DeathBlow`/复活、Act/整局流程（属 B-06/B-07）。
2. **切片简化**：`PlayedThisTurn` 与 `Known` 都是**单玩家**语义；多参与者的"谁知道什么"需要按参与者分槽，属 B-07/net N-02。
3. **测试场景开关**：`Build(includeTestHeadbutt: true)` 只用于 B-03 检查；冻结牌组本身仍是来源核对的 5/4/1，默认构建不含 Headbutt。
4. **怪物出生生命**仍是范围最小值（受控场景），范围内的原版随机掷点未实现（需要恢复出的 RNG 调用顺序）。
5. **覆盖规模**：切片是**受控场景**，不等于完整原版战斗；任何覆盖率/一致性声明都必须附本批的检查范围。
6. **未执行**：`net-the-spire`/`ai-the-spire` 的任何命令。旧后端检查在本批时点仍在首个失败中止；**后续状态（2026-10-01 R-LEGACY 复核）**：已修复为 `190 passed / 0 failed / 190 executed`，见 [r-legacy-2026-10-01.md](../../archived/r-legacy-2026-10-01.md)。

## 7. 下一批依赖

- **N-01/02**：`PlayerView` 已经是"机制→字段→来源→检查→证据"齐全的公开事实来源，可直接作为 `PublicSnapshot` 的映射输入；`KnownCard` 就是"已知成员 + 绝对位置"的线协议素材。
- **A-01**：编码侧需要同一份公开事实定义；P-01 第 1 步（`wire.schema.json` + `invariants.md`）已交付，仍未交付规范化/摘要参考实现与正反例。
- **J-01**：样例侧条件（B-04 完成）已满足，但需要有 net 客户端与随机合法基线才能跑闭环。
- **B-05 至 B-07**：作者前端迁移、K-09 职责抽离、内容批次与整局流程。
