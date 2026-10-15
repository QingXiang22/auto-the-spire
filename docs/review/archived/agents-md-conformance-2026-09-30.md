# AGENTS.md 合规审计（2026-09-30）

> 2026-10-01 归档说明：V1至V7已在当前内容/DSL说明与190项完整规则检查中处理，关闭证据见评审处置；本文历史统计保留，不代表当前失败。 详见[评审处置与剩余任务](../records/2026-10-01/review-followup-2026-10-01.md)。

- **审计对象：** 仓库根 [`AGENTS.md`](../../../AGENTS.md)（未跟踪文件，2026-09-30 版本）
- **审计范围：** `core-the-spire`、`mod-the-spire/base-spire`、`docs/`
- **工作区基线：** HEAD `3ad3fec`；`git status --short` 显示 10 项已修改 + 5 项未跟踪，全部未提交
- **审计方式：** 只读检查，加上仓库外一次性反射 runner（未修改仓库内任何文件，未提交）
- **结论：** 大体遵守；**4 处硬性未遵守（V1–V4）+ 3 处流程未落实（V5–V7）**

---

> 历史定位说明：下文标签中的行号是 2026-09-30 审计时的位置；链接现指向文件本身，不将 #L 行号当作 Markdown 标题锚点。190 项/11 失败是原时点记录，本轮未重跑规则检查。

## 1. 执行过的验证命令与结果

| # | 命令 | 结果 | 阻断范围 |
| --- | --- | --- | --- |
| 1 | `dotnet build AutoTheSpire.sln` | 通过（0 警告 0 错误） | — |
| 2 | `dotnet run --project core-the-spire/Core.TheSpire.Tests` | **失败** | 首个错误 `CombatSliceTests.cs:498`；进程以未捕获异常退出 |
| 3 | `git diff --check` | 干净 | — |
| 4 | 变更文档相对链接校验 | 全部可解析 | — |
| 5 | `git submodule status` | 两个子模块均停在原提交 | — |
| 6 | 反射逐测试清点（仓库外临时 runner） | 190 声明 / 179 通过 / 11 失败 | 11 个失败全在 `CombatSliceTests` |

命令 2 的原始输出（首个错误）：

```text
Unhandled exception. System.InvalidOperationException:
  Expected 'AttackEffect { Amount = 42, Target = SelectedTarget, Properties = Move, OnFatal =  }',
  got 'SequenceEffect { Effects = System.Collections.ObjectModel.ReadOnlyCollection`1[CoreTheSpire.EffectNode] }'
   at Core.TheSpire.Tests.Assert.Equal[T]                                              CombatSliceTests.cs:7108
   at Core.TheSpire.Tests.CombatSliceTests.BasePackageRegistersVanillaStartingDeckAndDeadlyCultistValues()
                                                                                       CombatSliceTests.cs:498
   at Core.TheSpire.Tests.Program.Main()                                               CombatSliceTests.cs:7157
```

由于 harness 没有逐测试隔离，命令 2 只能观测到**1 个**失败。命令 6 按类逐个调用才有全量分布：

```text
CombatSliceTests        164 methods,  153 passed,  11 failed
CardInstanceDslTests      9 methods,    9 passed,   0 failed
CardEventDslTests        10 methods,   10 passed,   0 failed
CardVariableDslTests      7 methods,    7 passed,   0 failed
TOTAL declared: 190   passed: 179   failed: 11
```

> 命令 6 的 runner 是仓库外的一次性工具，不属于本仓库交付物；它的存在只是因为原生命令报不出阻断范围（见 V6）。

---

## 2. 违反项

### V1 — `cost = X` 未落实（[AGENTS.md:17](../../../AGENTS.md)）

- **现象：** HEAD 有 2 张 X 费牌（`Cascade`、`Whirlwind`，旧语法 `costKind = x`）。迁移后只有 Whirlwind 写成 `cost = X`，Cascade 仍是 `cost = 0`。
- **位置：** [`ironclad-cards.spire:987`](../../../mod-the-spire/base-spire/BaseSpire/ContentSource/ironclad-cards.spire#L987)（Cascade `cost = 0`）对照 [`ironclad-cards.spire:2671`](../../../mod-the-spire/base-spire/BaseSpire/ContentSource/ironclad-cards.spire#L2671)（Whirlwind `cost = X`）
- **复现：**
  - `git show HEAD:mod-the-spire/base-spire/BaseSpire/ContentSource/ironclad-cards.spire | Select-String 'costKind = x'` → 2 处
  - `Select-String -Path mod-the-spire/base-spire/BaseSpire/ContentSource/*.spire -Pattern 'cost = X'` → 仅 Whirlwind 的 2 行
  - 编译结果：`base-spire:cascade cost=0 kind=Fixed HasEnergyXCost=False`，`base-spire:whirlwind kind=X HasEnergyXCost=True`
- **影响：** Cascade 的效果 `count = scaled(0, 1, count = energyX())` 恒为 0，战斗里不自动打出任何牌。实测该场景 `energy=3 enemyHp=53 auto_play=0`（测试期望 `enemyHp=38 block=5 energy=0 auto_play=3`）。直接导致 2 个测试失败。

### V2 — 公共契约变更未说明（[AGENTS.md:24](../../../AGENTS.md)）

- **变更：** 新版卡牌编译器把**单效果 `play` 也包成 `SequenceEffect`**，改变了 `CardDefinition.Effect` / `ResolveEffect` 的公开形状（旧语法下单效果产出裸节点）。
- **位置：** [`SpireCombatDslCompiler.cs:312`](../../../archive/legacy-sts/core-the-spire/Core.TheSpire.Legacy/Legacy/SpireCombatDslCompiler.cs#L312)（`CompileVersionEffect` 无条件 `new SequenceEffect(effects)`）
- **证据：** `docs/*.md` 全文搜索 `SequenceEffect` / 效应树形状 = **0 命中**；没有新旧行为对照、没有受影响文件清单、没有迁移要求。
- **影响：** 7 个断言失败（期望裸 `AttackEffect` / `GainBlockEffect` / `ApplyPowerEffect` / `IfEffect` / `HealEffect`）。

### V3 — 随机数语义变更未说明（[AGENTS.md:31](../../../AGENTS.md)）

- **变更：** Cascade 由 X 费退化为固定 0 费，同一 seed 下自动打牌张数 3 → 0，`card-targets` / `shuffle` 的调用次数与时点随之改变。
- **证据：** 同 V1 的实测（`auto_play=0`）。
- **说明：** 这是 V1 的连带后果，而且是**静默**变更——仓库内没有任何关于相同 seed 影响的记录。

### V4 — 四种状态未标注（[AGENTS.md:16](../../../AGENTS.md)）

- **现象：** "设计目标 / 解析器接受 / 内容包能注册 / 规则行为已验证"这套用语**只出现在 AGENTS.md 自身**，`docs/` 与现有报告里 0 命中。
- **影响：** [`spec.md:19`](../../design/spec.md) 与 [`dsl-usage.md:3`](../../dsl/0.0.1/dsl-usage.md) 写"Ironclad 卡牌和状态牌已迁移"，但规则检查 11 项失败。迁移实际只到"解析器接受 + 内容包能注册"，**未到"规则行为已验证"**，文档没有这个限定。

### V5 — 文档歧义未修正（[AGENTS.md:15](../../../AGENTS.md)）

- [`content-dsl.md:46`](../../dsl/0.0.1/content-dsl.md) 已加指向 `dsl-usage.md` 的提示，但 [`content-dsl.md:48`](../../dsl/0.0.1/content-dsl.md) 仍以旧方括号写法讲**卡牌**卡池归属：`pools = [IroncladCardPool]`、`cardPool IroncladCardPool { cards = [Aggression, Anger, Armaments] }`。新语法为 `pools = { … }` / `cards = { … }`。
- 同类：[`content-dsl.md:184`](../../dsl/0.0.1/content-dsl.md) 的 `variables = [variable(ExtraDamage, 0, …)]` 讲的是卡牌变量，新语法见 [`ironclad-cards.spire:3855`](../../../mod-the-spire/base-spire/BaseSpire/ContentSource/ironclad-cards.spire#L3855) 的 `variables = { variable = { … } }`。
- **可复现：** 用新语法编译 `tags = [Strike]` 报 `tags must use a brace block`；`tags = { Strike }` 通过。

### V6 — 失败时未给出阻断范围（[AGENTS.md:40](../../../AGENTS.md)）

- [`CombatSliceTests.cs:7142`](../../../archive/legacy-sts/core-the-spire/Core.TheSpire.Tests/CombatSliceTests.cs#L7142) 的 `Program.Main` 顺序调用测试，无逐测试隔离；任一断言失败即以未捕获异常终止。
- **影响：** 190 个测试中 11 个失败，原生命令只能报出第 1 个，无法给出阻断范围；这也是本节命令 6 必须存在的原因。

### V7 — 有测试从未被规则检查执行（[AGENTS.md:38](../../../AGENTS.md)）

- `SlipperyPowerDecrementsOnlyAfterPositiveUnblockedDamage`（[`CombatSliceTests.cs:400`](../../../archive/legacy-sts/core-the-spire/Core.TheSpire.Tests/CombatSliceTests.cs#L400)）已声明，但 `Program.Main` 未注册：**190 个声明方法 vs 189 次调用**。
- 单独运行该测试是通过的，但它永远不在规则检查的覆盖范围内。

### 失败的 11 项与根因对应

| 测试 | 根因 |
| --- | --- |
| `CascadeReservesItsWholeBatchBeforeCardDrawsAndSupportsZeroXUpgrade` | V1 |
| `AutomaticPlayShufflesDuringReservationAndCapturesXWithoutSpendingEnergy` | V1 |
| `ScaledAttacksAndTargetPredicatesUseLiveCombatState` | V2（`SequenceEffect` 无结构化相等，round-trip 不成立） |
| `PerfectedStrikeCountsTaggedCardsAcrossAllFiveCombatPiles` | V2（同上） |
| `BasePackageRegistersVanillaStartingDeckAndDeadlyCultistValues` | V2 |
| `SpireCardDslCompilesTheCurrentIroncladCardSet` | V2 |
| `SourceBackedIroncladCardsUseSharedEffectsInCombat` | V2 |
| `SourceBackedConditionalCardsUseTurnHistoryAndPileCounts` | V2 |
| `ImperviousGrantsBlockAndExhaustsItself` | V2 |
| `NotYetHealsAndKeepsItsCombatGenerationRestriction` | V2 |
| `SpiteRepeatsAfterPlayerHpLossAndResetsOnTheNextTurn` | V2 |

补充证据（V2 的深层缺陷）：`SequenceEffect` 用 record 默认相等，而 `Effects` 是 `IReadOnlyList<EffectNode>`（[`Effects.cs:80`](../../../archive/legacy-sts/core-the-spire/Core.TheSpire.Legacy/Legacy/Effects.cs#L80)），构造时 `Array.AsReadOnly(effects.ToArray())` 每次新建集合。实测：

```text
new SequenceEffect([Attack(5)]) == new SequenceEffect([Attack(5)])   ->  False
EffectDsl.Parse(EffectDsl.Serialize(x)) == x                          ->  False
```

即 `EffectDsl` 的 round-trip 等价性对任何序列块都不成立，不只是测试写错。

---

## 3. 符合项

| 条款 | 证据 |
| --- | --- |
| 内核不硬编码原版内容 | `core-the-spire` 内搜索 `"base-spire:"` 内容 ID、`Whirlwind` / `Ironclad` / `DampCultist` / `BurningBlood` 均 0 命中；原版 relic hook 都在 `base-spire` |
| 不把 sts2 / reference 当运行时依赖 | [`AutoTheSpire.sln`](../../../AutoTheSpire.sln) 只含 `core-the-spire`、`Core.TheSpire`、`Core.TheSpire.Tests`、`BaseSpire`、`mod-the-spire`、`test-hook-mod` |
| 未改动子模块 | `git status` 无 `sts2` / `reference` 条目；`git submodule status` 停在 `d231e9e5`、`742a1195` |
| DSL 在注册时编译 | `LoadCards` / `LoadEnemyBehavior` / `LoadPotions` 均在 `IRulePackage.Register` 路径；战斗 `step` 不解析文本 |
| 旧语法兼容 | 解析器保留 `package` 分支；17 个旧语法 `.spire` 未动；89 个 `enemy`、81 个 `power` 声明正常注册 |
| 嵌套调用约定 | [`dsl-usage.md:180`](../../dsl/0.0.1/dsl-usage.md) 起已文档化 `count = { pileCount = { pile = hand } }`；内容源 0 处链式赋值 |
| `player` 域与声明区分 | [`spec.md:19`](../../design/spec.md)、[`dsl-usage.md:17`](../../dsl/0.0.1/dsl-usage.md) 均写明"当前尚无 `player` 声明" |
| 未把构建成功当功能完成 | 无文档声称规则检查通过；评审稿带"不代表已完成全部原版规则"限定 |
| 文档链接与空白 | `git diff --check` 干净；变更文档的相对链接全部可解析 |

---

## 4. 未执行 / 无法判定

- 未做真实游戏差分；未逐项验证 89 个敌人行为；未做性能测量。
- 未验证"分工与协调"（[AGENTS.md:23](../../../AGENTS.md)）是否执行——仓库状态无法反推。
- `net-the-spire` / `ai-the-spire` 目录尚不存在，相关条款无法判定。

---

## 5. 元发现：AGENTS.md 原先未指定反馈地点

[AGENTS.md:25](../../../AGENTS.md) 与 [AGENTS.md:40](../../../AGENTS.md) 只规定报告的**内容**（改动文件、关键决策、验证命令与结果、未验证风险、首个错误、阻断范围），未指定**归档路径**。全仓搜索（含各子目录的 `AGENTS.md`）无任何 `feedback` / 反馈 / 交接路径约定，因此审计结论此前无处落盘。

已在本轮为 AGENTS.md 补充 `docs/review/tofix`（未修复）与 `docs/review/archived`（已修复）作为报告归档地点。

---

## 6. 建议收口顺序

1. **Cascade 补 `cost = X`** —— AGENTS.md 明文条款，且是 2 个失败的根因。
2. **给 `SequenceEffect` 加结构化相等** —— 修掉 2 个 round-trip 失败，让"效应树相等"契约真正成立。
3. **决定单效果 `play` 是否解包**，无论选哪个都把形状契约写进 `dsl-usage.md` 并同步 7 处断言（满足 [AGENTS.md:24](../../../AGENTS.md)）。
4. **记录 Cascade 的 seed 影响**（满足 [AGENTS.md:31](../../../AGENTS.md)）。
5. **收掉 `content-dsl.md:48` / `:184` 的卡牌旧写法**（满足 [AGENTS.md:15](../../../AGENTS.md)）。
6. **harness 加逐测试隔离与汇总**，并注册 `SlipperyPower…`（满足 [AGENTS.md:38](../../../AGENTS.md) / [:40](../../../AGENTS.md)）。
7. **文档落实四态标注**，至少把卡牌迁移标为"解析器接受 + 内容包能注册，行为验证未完成"（满足 [AGENTS.md:16](../../../AGENTS.md)）。
