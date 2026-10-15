# B-06a：基础战斗内容批（第十六小批：临时费用修正 + 已打出计数，2026-10-02）

> 历史证据（2026-10-03整理）：正文保留原时点结果，历史“下一步”不再作为当前派单指令。本报告归属W-06、W-01，未结项转入[当前清单](../../tofix/README.md)；移入records不表示修复或验收通过。逐份归属见[记录索引](../README.md)。

| 项目 | 内容 |
| --- | --- |
| 任务 ID | **B-06a 基础内容批**（第十六小批）——**临时费用修正**（`modify_cost:<n>`）+ **本回合已打出计数**（`played-count:<Kind>`） |
| 权威 | [开工清单 §14](../../../design/implementation-kickoff.md)（B-06a 行："能量/费用"）；来源：`ContentSource/ironclad-cards.spire:167`（`Stomp` 的 `modifySelectedCardCost`） |
| 负责范围 | `Slice/SliceAuthorFrontend.cs`（`modify_cost:<n>`（可负）、`played-count:<Kind>`）、`Slice/CombatSlicePackage.cs`（费用修正字段与有效费用、四牌区回合末过期、已打出计数）、`net` 公开映射（新卡牌字段的显式处置）、切片检查（新增 5 项） |
| 状态 | **本小批完成（能力）**；`Stomp` **本身未声明**（见 §4，理由写在报告里） |
| 前置 | 第十五小批（缩放数值） |

## 1. 来源事实

```
beforeCardPlayed = { whenCardType = attack  effect = { modifySelectedCardCost = { modifier =
    { costModifier = { amount = -1  operation = add  expires = { turnEnd } } } } } }
```

即"每打出一张攻击牌，这张牌的费用 −1，直到回合末"。

## 2. 本批能力（都用夹具牌验证）

| 能力 | 说明 |
| --- | --- |
| 卡牌字段 `CostModifier` | **有效费用 = max(0, Cost + CostModifier)**；guard 与实际扣费都走它（下限 0） |
| 步骤 `modify_cost:<n>` | 给这张牌自己叠加修正（可负，−9..9，写入前夹到边界） |
| 来源 `played-count:<Kind>` | 本回合**已完成**的该类牌数量 |
| 回合末过期 | `expires = { turnEnd }`：回合末把**四个牌区**（手牌/抽牌堆/弃牌堆/消耗区）的修正清零 |

两个实现要点：

1. **只在作者文本真的声明了 `modify_cost` 时才替换付款表达式**。否则保留原来的
   `Subtract(energy, Cost)` 逐字形式 ⇒ 既有内容与程序化路径的 IR **一字不变**（这是上一批"默认值保证不回归"的同一条纪律）。
2. **`played-count` 要排除正在打出的那一张**：它此刻已在 `PlayedThisTurn` 里但 `phase` 尚未结束
   （来源 `phase = finished`）——检查第一次就抓到"0 张已完成 → 却打了 2 点"，修正为排除当前牌后语义正确。

## 3. 退出证据

| 判据 | 检查 |
| --- | --- |
| 语法/编译 | `modify_cost:-1`（负参数）被解析；`played-count:Attack` 作为数值来源被接受；来源行无关（夹具牌） |
| 拒绝面 | `modify_cost:-10` → `invalid_value`；`modify_cost:x` → `invalid_value`；`played-count:Spell` → `invalid_value` |
| **逐实例** | 打出鼓 → 它自己留下 −1，**另一张牌不受影响**；同一实例再打一次（放回手牌）→ **不再扣费**（有效费用 0） |
| **下限** | 修正 −9 时有效费用 = max(0, 1−9) = 0（不扣能量，也不会变负） |
| **回合末过期** | 端末后手牌上的修正归零；实现上**四个牌区都扫**（只清手牌会留下"被弃掉的牌带着旧折扣再出现"的陈旧修正） |
| **已打出计数** | 第一次打出（已完成的攻击牌 0 张）→ 伤害 = base 1；第二次（1 张已完成）→ 1 + 1×1 = **2** |
| net 契约 | 新卡牌字段 `CostModifier` **发布**（只发布 `Cost` 会让消费方看到的费用与合法动作不一致） |

## 4. 为什么**没有**声明 `Stomp` 本身（明确未完成）

来源的 `Stomp` 除 `beforeCardPlayed` 之外还有 `onEnterCombat` 里的
`if { condition = { selectedCardIsClone } then = {} else = { modifySelectedCardCost … } }`：
它涉及**克隆/复制**语义与**hook 上的 `whenCardType` 过滤**，两件本批都没做。
在补齐前声明 `Stomp` 会**夸大覆盖**（牌在、语义不全），因此按纪律**不声明**，
把这两件记成下一步的具体缺口（而不是用近似语义凑一张卡）。

## 5. 覆盖分母与剩余

| 口径 | 值 |
| --- | --- |
| 迁移分母（94 张 Ironclad 卡 + 2 张状态牌） | **96** |
| 已迁入并可运行 | **22**（本批**没有新增卡**：只补能力并用夹具验证） |
| 明确未迁 | 其余 74 张 |

下一步的具体缺口：`when-card-type` 在**卡牌 hook** 上的过滤、克隆/复制语义、
`cardsWithTag` 的牌区口径（待裁定）、交互式抽牌、其他触发点与谓词、`BodySlam`（来源缺失）。

## 6. 运行结果（本轮实跑）

| 命令 | 返回码 | 结果 |
| --- | --- | --- |
| `dotnet build AutoTheSpire.sln` | 0 | 0 错误 0 警告 |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | 0 | **All 186 slice checks passed**（25 既有 + 4 B-05b + 87 B-05c + 70 项 B-06a） |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | 0 | 383 passed |
| `python ai-the-spire/tests/j01_episode.py` | 0 | 150 passed |
| `python net-the-spire/client/test_client.py` | 0 | 50 passed |
| `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` | 0 | 64 passed |
| `dotnet run --project core-the-spire/Core.TheSpire.Tests` | 0 | 190 passed / 0 failed |
| AI 检查 / 契约 / 依赖 | 0 | 503 / 210 / 9 |

## 7. 修改文件

- `BaseSpire/Slice/SliceAuthorFrontend.cs`（`ModifyCost`（可负字面量）、`PlayedCountPrefix`）
- `BaseSpire/Slice/CombatSlicePackage.cs`（`CostModifier` 字段、`CostForPayment`、`modify_cost` 步骤、`played-count` 降级、四牌区过期）
- `net-the-spire/Net.TheSpire/Projection/SlicePublicProfile.cs`（`CostModifier` 的公开处置）
- 切片检查：`SliceFrontendCostModifierChecks.cs`（新增 5 项）、`SliceChecks.cs`
- `docs/review/tofix/long-task-progress-2026-10-02.md`（状态）

未提交、未清理共享工作区。

## 8. 下一批建议

**B-06a 第十七小批：卡牌 hook 上的 `when-card-type` 过滤 + 克隆/复制语义**——两件都是声明 `Stomp` 的前置。
建议先做 `when-card-type`（与能力触发点的同名过滤同形，来源字段明确：`whenCardType = attack`），
再做克隆/复制（`selectedCardIsClone`，需要来源里"创建副本"的语义），然后才声明 `Stomp`；
若克隆语义在来源里取不到，就把 `Stomp` 记成明确未完成项并说明差哪一半。
