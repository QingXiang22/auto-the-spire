# 药水与 run 的范围说明（B-05c 第十九批，2026-10-02）

| 项目 | 内容 |
| --- | --- |
| 任务 ID | **B-05c**（第十九批）——为 B-05c 行里的"药水及 run"**先出范围**再动手 |
| 权威 | [开工清单 §14](../../design/implementation-kickoff.md#14-m1后阶段二sts作者前端与内容迁移)（B-05c 行）、[§15](../../design/implementation-kickoff.md#15-m1后阶段三从战斗到单角色单人整局)（B-07 run 流程）、B-05a 冻结清单 |
| 状态 | **范围已定**：药水做（本批给出首批增量与验收判据）；**run 内容声明推迟到 §15 B-07 之后**，理由见 §3 |
| 前置 | [b-05a](../../review/records/2026-10-02/b-05a-content-manifest-2026-10-02.md)（数量与来源）、B-05c 批 1〜18（步骤词表与运行时能力） |

## 1. 现状事实（来自 B-05a 的机器清单，不是印象）

| 内容域 | 旧后端（冻结，可运行） | 新切片（`Engine/` IR） |
| --- | --- | --- |
| 药水 | **6** 条（`Potions/Content/potions.spire`），另有 1 个药水专用能力（`potion-powers.spire`） | **0**：切片没有药水类型、没有使用动作、没有药水栏 |
| run | **1** 个 `run` 定义 + **63** 个遭遇 + 11 个遗物；`run_events` = **0** | **0**：切片是单场战斗，没有 run/房间/奖励/地图 |
| 遭遇/act 内容 | `Content/*.encounters.json`、`Content/*.acts.json` + `StandardActMapGenerator.cs` | 0（切片只有 2 个写死的邪教徒） |

冻结的药水语法（6 条全部 `usage = combatOnly`）：

```
potion FirePotion    { rarity = common    usage = combatOnly  target = enemy  use = attack(20, target=target, properties=[unpowered]) }
potion BlockPotion   { rarity = common    usage = combatOnly  target = self   use = gainBlock(12) }
potion StrengthPotion{ rarity = common    usage = combatOnly  target = self   use = applyPower(Strength, 2, target=self) }
potion BlessingOfTheForge { rarity = uncommon usage = combatOnly target = self use = upgradeCards(pile=hand) }
potion BeetleJuice   { rarity = rare      usage = combatOnly  target = enemy  use = applyPower(Shrink, 4, target=target) }
potion FlexPotion    { rarity = common    usage = combatOnly  target = self   use = （临时能力，配上 onStackChange/onExpire） }
```

## 2. 药水：**在 B-05c 范围内**，首批增量如下

**关键判断：药水不需要新的引擎能力。** 它需要的三样东西都能用既有原语表达：

| 需要什么 | 用既有原语 |
| --- | --- |
| 药水实例 | 一个实体类型（`Potion`：`DefinitionId`/`Rarity`/`Usage`/`Target`）+ 一个**药水栏集合** |
| 使用动作 | 一个 `UsePotion` 动作（候选＝药水栏，guard＝"战斗中且栏位有药水"） |
| 药水效果 | **复用牌的效果步骤词表**（`target`/`damage`/`block`/`apply_power`/`upgrade_hand`/…）——本会话批 1〜18 已把这些步骤的运行时语义逐个建立并检查过 |

**首批增量（建议作为下一批实现）**：

1. 作者语法：

   ```
   potion fire-potion {
     name = FirePotion  rarity = common  usage = combat-only  target = enemy
     use { amount = 20  steps = [target, damage] }
   }
   ```
   `potion <id> { … use { … } }`；`steps` 复用牌的词表与校验（含"未知步骤→拒绝"、"目标确定方式唯一"等既有规则）。
2. 运行时：`Potion` 实体 + 药水栏集合 + `UsePotion` 动作（用后从栏位移除＝消耗）+ 效果按步骤降级。
3. 验收判据（沿用本会话的取证标准：语法／编译／IR／加载／行为五态）：
   - 语法：药水声明与 `use` 步骤表被解析；非法（未知稀有度/未知 usage/未知步骤/目标域冲突）带路径拒绝；
   - 运行：使用药水后**效果按声明生效**、药水**离开栏位**、且**不消耗能量**（药水与牌的费用语义不同）；
   - 冻结路径：程序化切片与冻结作者文本不受影响（切片当前没有药水）；
   - 与旧后端对照：至少覆盖 `BlockPotion`（+12 格挡）、`StrengthPotion`（+2 力量）、`FirePotion`（20 点**非力量加成**伤害
     —— 注意它带 `properties=[unpowered]`，而切片的伤害链当前**没有** unpowered 门控，见 §4）。
4. **明确不做**（首批）：`BlessingOfTheForge`（需要"升级整手牌"之外的**选择**语义——它作用整堆，本会话批 16 已有
   `upgrade_hand`，可直接覆盖 ✓ 实际可做）、`BeetleJuice`（需要 `Shrink` 能力 = 出手倍率，切片没有）、
   `FlexPotion`（需要临时能力的 `onStackChange`/`onExpire`，切片的能力系统没有这两个触发点）。

## 3. run：**推迟到 §15 B-07 之后**，本轮不做声明

理由（都是硬约束，不是"太大所以不做"）：

1. **flow 属 §15**：开工清单把 run 流程放在 §15 的 **B-07（run flow）**，B-05c 行只写"作者表示迁入同一前端"。
   在 B-07 定义 run 流程需要哪些声明之前，先写 act/encounter/map 声明会变成**不可达内容**——
   本会话的纪律（"不可达声明必须显式处理，不许留不可达声明"）明确禁止这种先声明后找消费者的做法。
2. **没有消费者就没有验收判据**：run 声明的正确性只能由 run 流程来证伪（房间选择、奖励表、地图生成）。
   在没有流程时，我只能断言"能解析"，那不是 B-05c 要求的"语法/编译/运行语义/检查"四态。
3. **run 需要的引擎能力尚未确定**：run 是**跨战斗**的会话（多场战斗、持久状态、地图随机流），
   而当前引擎的会话模型是"一个作用域树 + 一场战斗"。这属于需要新 Engine 能力的判断，
   应当由 B-07 的需求来驱动，而不是由 B-05c 的声明来猜测。

**因此本批对 run 的交付是"范围与依赖说明"**：等 B-07 明确 run 流程的声明面与所需引擎能力后，
再按同样的分批方式（先补引擎能力、再开作者语法、再补检查）把它带入作者前端。
B-05a 记的 run 侧分母（63 遭遇 / 11 遗物 / 1 run / 0 run events）保持不变，**不计入** B-05c 的完成度。

## 4. 本批顺带确认的两个相邻缺口（不属于药水，但会影响药水验收）

| 缺口 | 影响 | 现状 |
| --- | --- | --- |
| `properties=[unpowered]`（不受力量/虚弱影响） | `FirePotion` 的 20 点伤害语义在切片里无法表达 | 切片的伤害链**没有** unpowered 门控（R-DAMAGE 证据 §6 也把它列为未覆盖区） |
| 出手倍率型能力（`Shrink`） | `BeetleJuice` 无法表达 | 切片的乘性阶段只有"承受者侧乘"，没有出手者侧乘（`multiply-outgoing` 是**存在性**乘，不是按层数） |

两者的共同点是：**都需要先补 Engine/切片的伤害语义**，而它们同时是 R-DAMAGE 证据里列出的未覆盖区。
因此药水首批只取"能被现有伤害语义精确表达"的三张（`BlockPotion`/`StrengthPotion`/`FirePotion`——
其中 `FirePotion` 需要先决定 unpowered 怎么表达，见下），其余三张随各自依赖到位后再做。

> **对 `FirePotion` 的处置**：它带 `unpowered`。若首批就要它，必须先给切片补"攻击是否受出手方修正影响"的
> 属性门控（R-DAMAGE §8.5 的判定项之一）。建议**首批只做 `BlockPotion` 与 `StrengthPotion`**
> （零新语义、纯复用），`FirePotion` 留到补完门控之后——这样首批的每条断言都能被现有语义证伪。

## 5. 依赖与后续批次顺序（建议）

1. **B-05c 批 20**：药水首批 = `potion` 声明 + 药水栏 + `UsePotion` 动作 + `BlockPotion`/`StrengthPotion` 两条内容与检查。
2. **B-05c 批 21**：出手方属性门控（`unpowered` 的 IR 表达）→ 解锁 `FirePotion` 与 R-DAMAGE 的一个未覆盖区。
3. **B-05c 批 22**：临时能力触发点（`onStackChange`/`onExpire`）→ 解锁 `FlexPotion`；出手倍率（`Shrink`）→ 解锁 `BeetleJuice`。
4. **§15 B-07 之后**：run 声明面（act/encounter/map/奖励）按 B-07 的需求与所需引擎能力分批判实现。

## 6. 本批交付

- 本文档（范围说明）。
- **未改任何生产代码**：本批是范围与依赖判定，不是实现；因此没有新的检查项，也不改变任何计数。

## 7. 未决 / 需要规划者确认

1. **药水是否要与牌共用 `steps` 词表**（本文档建议共用）：共用能让"药水效果"自动继承全部既有检查，
   但也会让药水的写法与牌完全同构；若规划者希望药水有自己的受限词表，需要现在说明。
2. **药水栏是否进公开快照**（若进，需要 net 侧的公开词典与投影声明；当前 net 只投影战斗）。
3. **run 的范围边界**：B-07 是否包含地图生成与奖励选择（还是只做"战斗→奖励→下一场"的最短闭环）。
