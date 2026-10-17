# B-05c：完整前端覆盖（第二十批：药水首批，2026-10-02）

> 历史证据（2026-10-03整理）：正文保留原时点结果，历史“下一步”不再作为当前派单指令。本报告归属W-06、W-01，未结项转入[当前清单](../../tofix/README.md)；移入records不表示修复或验收通过。逐份归属见[记录索引](../README.md)。

| 项目 | 内容 |
| --- | --- |
| 任务 ID | **B-05c**（第二十批）——**药水首批**（`potion` 声明 + 药水栏 + `UsePotion` 动作） |
| 权威 | [开工清单 §14](../../../design/implementation-kickoff.md#14-m1后阶段二sts作者前端与内容迁移)、[药水与 run 的范围说明](../../../design/mod-the-spire/potion-run-scope-2026-10-02.md) |
| 负责范围 | `Authoring/SliceAuthorFrontend.cs`（`potion`/`potions` 解析与校验）、`Composition/CombatSlicePackage.cs`（药水类型/栏位/使用动作；步骤降级参数化）、`BaseSpire.Slice.Tests/Potions/SliceFrontendPotionChecks.cs`（新增 4 项） |
| 状态 | **部分完成（二十批）**：药水首批已交付（3 张声明可跑：格挡/力量/火焰）；B-05c 其余子项仍未开始（见 §5） |
| 前置 | [批1](b-05c-frontend-batch1-2026-10-02.md) … [批19](../../../design/mod-the-spire/potion-run-scope-2026-10-02.md) |

## 1. 交付内容（全部落在切片里，**未改 `Engine/`**）

```
potions { block-potion  strength-potion  fire-potion }        // 药水栏（与卡组同一套 xN 写法）

potion block-potion {
  name = BlockPotion  rarity = common  usage = combat-only  target = self
  use { amount = 12  steps = [block] }
}
```

- **药水效果复用牌的效果步骤词表**：`use { … steps = […] }` 走的是同一个 `StepInstructions`，
  因此自动继承批 1〜18 建立的全部步骤语义与校验（未知步骤、目标域唯一、批次数、`amount-source`、…）。
  为此把步骤降级里的 **数值/能力/层数字段参数化**（牌传卡牌字段、药水传药水字段）。
- **药水栏**：`Potion` 实体（`DefinitionId`/`Rarity`/`Target`/`Amount`/`PowerId`/`PowerStacks`）+ `PotionBelt` 集合，
  开局按 `potions { … }` 建实例。
- **`UsePotion` 动作**：候选＝药水栏、guard＝栏位非空、**不消耗能量**；用后把该瓶移出栏位（消耗）。
  敌人目标药水由它自己的 `target` 步骤**提问**（与牌完全同形）。
- **可选的包形状**：只有作者文本声明了药水/栏位时才创建药水类型、栏位与动作；
  冻结作者文本与程序化路径**没有**这些声明 → 包形状与之前**逐字节一致**（net 的 383 项因此不回归）。

## 2. 退出证据

| 判据 | 检查 |
| --- | --- |
| 语法 | 3 条声明与栏位解析；稀有度/用法/目标/`use` 未知键/未知步骤/**self 目标却声明 damage** 分别被拒绝（`invalid_value`/`unknown_key`/`unknown_step`/`unsupported_combination`，带路径） |
| 运行（自我目标） | `自我目标药水生效、离开栏位、且不消耗能量 [真实]`：格挡 **+12**、瓶数 3→2、**能量不变**；力量药水给玩家 **+2 力量**，随后打击造成 3+2=**5** |
| 运行（敌人目标） | `敌人目标药水先提问再结算，且只消耗那一瓶 [真实]`：使用后**挂起决策**，答"前排"→ 前排 120→**100**、后排不变；只用掉那一瓶（另一瓶仍在栏位） |
| 默认路径 | `冻结作者文本没有药水`：冻结模型 0 药水/0 栏位；**默认包里没有 `Potion` 类型、没有 `UsePotion` 动作** |

## 3. 本轮修正的缺陷（都在我自己的实现里）

1. **动作参数绑定**：`Parameter(...)` 返回的是声明，需要用 `Param(declaration.DeclarationIndex)` 取表达式
   （与牌路径一致）——第一版直接当表达式用，编译期报错。
2. **两个分支共用一个目标槽导致类型错**：引擎的类型检查拒绝把 `ref<Player>` 写进推断为 `ref<Monster>` 的槽。
   改为**按分支各自声明类型正确的目标槽**（`potionEnemy` = ref\<Monster\>、`potionSelf` = ref\<Player\>），
   并且**按目标种类过滤**分派（敌人分支不会跑到自我目标的药水）。
3. **重复提问**：我最初在动作层先问一次目标、药水自己的 `target` 步骤又问一次（第二次决策没人回答，
   于是伤害没结算）。改为**只由药水自己的 `steps` 决定提问时机**——与牌完全同形，也让"目标确定方式唯一"这条既有校验继续生效。

## 4. 运行结果（本轮实跑）

| 命令 | 返回码 | 结果 |
| --- | --- | --- |
| `dotnet build AutoTheSpire.sln` | 0 | 0 错误 0 警告 |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | 0 | **All 106 slice checks passed**（25 既有 + 4 B-05b + 77 项 B-05c） |
| `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` | 0 | 64 passed |
| `dotnet run --project core-the-spire/Core.TheSpire.Tests` | 0 | 190 passed / 0 failed |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | 0 | 383 passed（**包形状未变**，不回归） |
| `python ai-the-spire/tests/run_checks.py` | 0 | 503 passed（不回归） |
| `python ai-the-spire/tests/j01_episode.py` | 0 | 150 passed（不回归） |
| `python tools/contract-check/check_contracts.py` | 0 | 210 passed（不回归） |
| `python net-the-spire/client/test_client.py` | 0 | 50 passed（不回归） |

## 5. B-05c 剩余子项（仍未完成）

- **`FirePotion` 的 `properties=[unpowered]`**：切片伤害链没有出手方属性门控（本批的 `fire-potion`
  是**普通伤害**，不是来源里的 unpowered 语义）——下一批补门控后才算与来源一致；
- **`BeetleJuice`（`Shrink` 出手倍率）**、**`FlexPotion`（临时能力的 `onStackChange`/`onExpire`）**、`BlessingOfTheForge`（可用 `upgrade_hand`，未做）；
- **药水栏是否进公开快照**（net 投影）——需规划者决定（范围说明 §7.2）；
- run 内容声明（等 §15 B-07 定义流程与所需引擎能力）；
- 生成时的随机选择、变量的跨战斗语义。

## 6. 修改文件

- `BaseSpire/Authoring/SliceAuthorFrontend.cs`（`AuthorPotion`/`AuthorBeltEntry`、`PotionRarities/Usages/Targets`、`ReadPotion`、`potions` 块、模型两种新字段）
- `BaseSpire/Composition/CombatSlicePackage.cs`（药水类型/栏位/`UsePotion`、`PotionEffectChain`、步骤降级字段参数化）
- `BaseSpire.Slice.Tests/Potions/SliceFrontendPotionChecks.cs`（新增 4 项）、`SliceChecks.cs`（注册）
- `docs/review/tofix/long-task-progress-2026-10-02.md`（状态）

未提交、未清理共享工作区。

## 7. 下一批建议

**B-05c 第二十一批：出手方属性门控（`unpowered`）**——它同时解锁 `FirePotion` 的**来源语义**
与 R-DAMAGE 证据里的一个未覆盖区（"三次未覆盖"之一）。按纪律先在 `Engine/`/切片的伤害链里把
"这次攻击是否受出手方修正影响"表达出来，再让药水/牌声明它，并补检查。
