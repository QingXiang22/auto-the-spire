# B-05c：完整前端覆盖（第二十二批：出手倍率型能力，2026-10-02）

> 历史证据（2026-10-03整理）：正文保留原时点结果，历史“下一步”不再作为当前派单指令。本报告归属W-06、W-01，未结项转入[当前清单](../../tofix/README.md)；移入records不表示修复或验收通过。逐份归属见[记录索引](../README.md)。

| 项目 | 内容 |
| --- | --- |
| 任务 ID | **B-05c**（第二十二批）——**出手倍率型能力（`Shrink`）**，补齐 R-DAMAGE 证据里的"出手倍率"未覆盖区，并解锁 `BeetleJuice` |
| 权威 | 来源：`sts2/src/Core/Models/Powers/ShrinkPower.cs`（本批逐条核对）；[r-damage-source-evidence](../2026-10-01/r-damage-source-evidence-2026-10-01.md) §6/§8.5；[开工清单 §14](../../../design/implementation-kickoff.md#14-m1后阶段二sts作者前端与内容迁移) |
| 负责范围 | `Authoring/Examples/slice-content.author`（`power shrink` + `potion beetle-juice` + 栏位）、`Authoring/SliceAuthorFrontend.cs`（药水/栏位的双向可达性校验）、`BaseSpire.Slice.Tests/Powers/SliceFrontendShrinkChecks.cs`（新增 4 项） |
| 状态 | **部分完成（二十二批）**：Shrink 与 BeetleJuice 已交付；B-05c 其余子项仍未开始（见 §5） |
| 前置 | [批1](b-05c-frontend-batch1-2026-10-02.md) … [批21](b-05c-frontend-batch21-2026-10-02.md) |

## 1. 来源核对（先看来源，再动手）

`ShrinkPower.cs`：

| 事实 | 值 |
| --- | --- |
| 减伤常量 | `public const decimal damageDecrease = 30m` |
| 乘性修正 | `ModifyDamageMultiplicative` 返回 `(100m - 30m) / 100m = 0.70m` —— **与层数无关的固定倍率** |
| 类型 | `PowerType.Debuff`，按 `PowerCmd.Decrement(this)` 递减（与 Weak/Vulnerable 同一套时长语义） |
| 门控 | 与其它出手修正一样以 `IsPoweredAttack()` 为必要条件（证据 §6 的 64 处复用之一） |

**结论：`Shrink` 在切片里可以直接用既有能力声明表达**——`damage = multiply-outgoing:0.7` +
`owner-turn-end = tick`，**不需要新代码**。本批因此是"把来源事实落成内容声明 + 补检查"，
而不是新增机制（这也是它便宜的原因）。

## 2. 交付内容

- 冻结作者文本新增：

  ```
  power shrink {
    name = Shrink  role = Shrink  effect = "its owner's powered damage x0.70 (stacks are a duration, not a multiplier)"
    source = sts2/src/Core/Models/Powers/ShrinkPower.cs  line = 5
    owner-turn-end = tick
    damage = multiply-outgoing:0.7
  }

  potion beetle-juice {
    name = BeetleJuice  rarity = rare  usage = combat-only  target = enemy
    use { amount = 0  power = shrink  stacks = 4  steps = [target, apply_power] }
  }
  potions { beetle-juice }
  ```
- 前端新增**双向可达性校验**：药水栏引用了未声明的药水 → `dangling_reference`；
  声明了药水却不在栏位里 → **`unreachable_potion`**（与本会话对行动/变量的一贯纪律一致：不留不可达声明）。

## 3. 退出证据

| 判据 | 检查 |
| --- | --- |
| 声明与来源一致 | `Shrink 按来源声明为固定 ×0.70（不是按层数）[真实]`：类型是出手方乘性、值 `0.7`、回合末 `tick` |
| **层数不是倍率** | 敌人暗击 10 点：无 Shrink → **10**；**1 层 → 7**；**4 层 → 7**（层数只影响持续时间） |
| 与 Weak 叠乘 | Shrink + Weak：10 × 0.75 × 0.70 = 5.25 → **5**（阶段之间不取整） |
| BeetleJuice 可用 | 药水给**所选敌人**上 4 层 Shrink、另一个敌人 0 层、药水被消耗（先提问再结算） |
| 冻结文本 | Shrink 的值 `0.7`、BeetleJuice 上 4 层且 `target = enemy` |

> 注：`Shrink` 同样在 `unpowered` 门控之内（它在乘性阶段里，批 21 的门控整体跳过该阶段），
> 因此"非 powered 攻击不受 Shrink 影响"已由既有的门控检查覆盖，不再重复断言。

## 4. 本轮修正的缺陷（都是我自己引入的）

**内容插入锚点命中了文件头部的注释示例**：我用 `deck {` / `card strike-ironclad {` 作为插入锚点，
而这两行在文件顶部的**语法说明注释**里也存在（`//   deck { ... }`），于是插入落在了注释块里，
产生"注释外的 `beetle-juice` / `card strike-ironclad {`"——解析器如实报
`未知块 'beetle-juice'`、随后报 `card strike-ironclad.potions… 处期望 '='`。
修法是逐个把头部注释恢复成注释、并确认真正的声明只出现一次。**教训：往文档化文件里插内容时，
锚点必须带唯一前缀（例如行首无 `//`），否则会插进示例里。**

## 5. B-05c 剩余子项（仍未完成）

- **`FlexPotion`**：需要临时能力的 `onStackChange`/`onExpire` 触发点（引擎能力缺口）；
- **`BlessingOfTheForge`**：可用既有 `upgrade_hand` 表达，未做；
- **药水栏是否进公开快照**（net 投影）——需规划者决定；
- R-DAMAGE 其余未覆盖区（玩家侧易伤组合、非整数比组合）；
- run 内容声明（等 §15 B-07）；生成时的随机选择；变量的跨战斗语义。

## 6. 运行结果（本轮实跑）

| 命令 | 返回码 | 结果 |
| --- | --- | --- |
| `dotnet build AutoTheSpire.sln` | 0 | 0 错误 0 警告 |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | 0 | **All 114 slice checks passed**（25 既有 + 4 B-05b + 85 项 B-05c） |
| `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` | 0 | 64 passed |
| `dotnet run --project core-the-spire/Core.TheSpire.Tests` | 0 | 190 passed / 0 failed |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | 0 | 383 passed（不回归） |
| `python ai-the-spire/tests/run_checks.py` | 0 | 503 passed（不回归） |
| `python ai-the-spire/tests/j01_episode.py` | 0 | 150 passed（不回归） |
| `python tools/contract-check/check_contracts.py` | 0 | 210 passed（不回归） |
| `python net-the-spire/client/test_client.py` | 0 | 50 passed（不回归） |

## 7. 下一批建议

**B-05c 第二十三批：`BlessingOfTheForge`（升级整手牌的药水）**——纯内容（复用批 16 的 `upgrade_hand`），
或**药水栏进公开快照**（需先取得规划者决定）。前者无依赖、可立即做；后者是跨模块契约，建议先确认再动。
