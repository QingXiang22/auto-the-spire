# ENV-T05 Ironclad 卡牌完整批次 — 综合交接

> 负责人2026-10-10复核：原会话已被用户中断并停止；本报告保留执行者当时的 in_progress 和记账。负责人直接执行六条局部检查通过，但发现 withCount 词法范围、掉血计数跨战斗重置、PrimalForce替代实例三处反例，尚未验收两族。新增标准入口 `--card-completion-only`；实际证据与后继边界见[复核记录](../records/2026-10-10/dsh-context-and-sim-control-review-2026-10-10.md)，当前状态只看[主清单](README.md#env-t05)。


```yaml
task_id: ENV-T05
task_name: ENV-T05-ironclad-all-cards-20261009
dispatch_id: ENV-T05-ironclad-all-cards-20261009
session_id: session-e6847f17-7dd8-4229-a7b5-017cd626a5fd
dispatch_record: .audit/dsh-ironclad-all-cards-2026-10-09/task-record.json
delivery_status: in_progress
updated_at: 2026-10-10T00:06:51+08:00
```

**一句话交付结果**：把 `ironclad-cards.spire` 里原本「声明得到、降级不了」的卡按机制族逐族接上真实的领域程序与运行时能力；当前已落地两族（**类型化动态 repeat + 计数来源**、**`withCount` 词法快照 + `forEachCard` 绑定**），Ironclad 降级数从 **68 → 74**（未支持 26 → 20），源单人奖励池缺口从 **24 → 18**。

## 接手入口与第一步

1. 主清单任务行：`docs/review/tofix/README.md` 的 ENV-T05（状态、当前机制、下一成果、证据链接）。
2. 规格与机制顺序：`docs/design/mod-the-spire/ironclad-card-completion.md`（§3 机制方向、§4 顺序）。
3. 覆盖矩阵（本次开工快照）：`.audit/dsh-ironclad-all-cards-2026-10-09/coverage-matrix-开工.md`，
   生成器 `.audit/dsh-ironclad-all-cards-2026-10-09/build_coverage_matrix.py`（收尾时用 `--label 最终` 重跑）。
4. 第一步动作：按矩阵的「缺口」列挑下一族（当前下一族是 §4 的**生成/抽牌族**），落地后跑
   `dotnet build mod-the-spire/base-spire/BaseSpire.Slice.Tests/BaseSpire.Slice.Tests.csproj`，再跑
   `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests --no-build -- --combat-only`
   看 `ironclad-cards.spire: 声明 94 / 解析 94 / 降级 N / 未支持 N`。

## 已交付能力

### 家族 1：类型化动态次数 + 计数来源（3 张）

- **真实源形状**（`mod-the-spire/base-spire/BaseSpire/ContentSource/ironclad-cards.spire`）：
  - `Whirlwind`（:2665）：`cost = X`、`target = { selectAll = {} }`、`play = { repeatForX = { effect = { attack = { damage = 5 target = allEnemies } } } }`（升级 8）。
  - `TearAsunder`（:1251）：`repeatForCount = { base = 1 perCount = 1 count = { playerHpLossCount = {} } effect = { attack = { damage = 5 } } }`（升级 7）。
  - `PerfectedStrike`（:3263）：`tags = { Strike }`、`scaledAttack = { base = 6 perCount = 2|3 count = { cardsWithTag = { tag = Strike } } }`。
- **节点/共享过程**：`DynamicRepeatCardEffect`（新增）、`CardAmountSource.PaidX / CardsWithTag / PlayerHpLossCount`、
  `CardEffectExpressions.Amount` 新增 `PlayerHpLossCount` / `CardsWithTag`（`TaggedCount` 求和 hand/draw/discard/exhaust/inPlay）、
  `SpireCardSource.CountSourceOf` 新增 `cardsWithTag` / `playerHpLossCount` / `energyX`。
- **首个判别案例**：`mod-the-spire/base-spire/BaseSpire.Slice.Tests/SliceFrontendIroncladCompletionChecks.cs` 的
  「Whirlwind `repeatForX` —— X 只作用于**次数**，每击是字面 5（升级 8）」，用 `(spent, x) ∈ {(0,3),(1,2)}` 构造两个不同的 X 值：
  若 X 泄漏进数值叶，X=3 会得到每击 3 点/总共 9 而非 15。**这条同时是 X 不泄漏的回归守卫。**

### 家族 2：`withCount` 词法快照 + `forEachCard` 绑定（4 张）

- **真实源形状**：
  - `FiendFire`（:863）：`keywords = { exhaust }`；`play = { withCount = { name = "handSize" count = { pileCount = { pile = hand } } effect = { forEachCard = { from = hand effect = { moveSelectedCard = { from = hand to = exhaust } } } repeatForCount = { base = 0 perCount = 1 count = { savedCount = { name = "handSize" } } effect = { attack = { damage = 7|10 } } } } } }`。
  - `SecondWind`（:2348）：`play = { forEachCard = { from = hand filter = nonAttack effect = { moveSelectedCard = { from = hand to = exhaust } gainBlock = { amount = 5|7 } } } }`，**无 `exhaust` 关键词 ⇒ 自己进弃牌堆**。
  - `PrimalForce`（:711）：`play = { forEachCard = { from = hand filter = attack effect = { transformSelectedCard = { card = GiantRock from = hand } } } }`，升级变体多 `upgraded = true`（:747）。
  - `Stoke`（:794）：同 `withCount` 形状，但体内还有 `generateCards`（**待生成族**，见「未验证范围」）。
- **节点/共享过程**：`WithCountCardEffect` / `ForEachCardEffect` / `MoveSelectedCardEffect` / `TransformSelectedCardEffect`、
  `CardEffectPile` / `CardEffectCardFilter` 枚举、`CardAmountSource.SavedCount`；
  编译器侧 `CardEffectCompiler.Compile(..., Expr? selectedCard = null)`（`ForEachCardEffect` 用 `scope.ForEach` 把当前卡绑进 `selectedCard`）、
  运行时侧 `CombatSlicePackage.CompileEffects` 的 `savedCounts` 字典 + `PileCollection` / `MoveCardSteps` / `TransformCardSteps`。
  `MoveCardSteps` 走 Exhaust 时复用 `exhaust_from_hand` 口径（置 `ExhaustedCardThisTurn` + `Call(triggerAfterExhaust)`），
  `TransformCardSteps` 复用 `becomes:` 口径（同时改写 Instance 的定义/种类/费用/数值/能力/变量与 `Upgraded`）。
- **首个判别案例**：「FiendFire 的快照**先于**消耗 —— 命中数取打出时的手牌张数」。
  受控手牌 4 张 ⇒ 打出的牌先离开手牌 ⇒ 命中 3 次；若快照读晚（或被消耗改变），命中数会变成 0/4。
  另三条覆盖 `filter = nonAttack` 的筛选（攻击牌留手）、`transformSelectedCard` 的**就地改写同一实例**（变形后还能当攻击牌打出去并结算 GiantRock 的 16/20 点）。

## 实际执行命令与结果

工作目录：`F:\Scylier-Project\active\auto-the-spire`

| 命令 | 退出码 | 结果 |
| --- | --- | --- |
| `dotnet build mod-the-spire/base-spire/BaseSpire.Slice.Tests/BaseSpire.Slice.Tests.csproj` | 0 | 已成功生成，0 错误 / 3 警告（既有 `Slice/SourceMap/ActMap.cs(42,37)`、`(60,33)` CS8600、`Slice/SpirePowerSource.cs(1241,39)` CS8604） |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests --no-build -- --combat-only` | 1 | **345 条 ok / 0 条 fail**；唯一失败是身份 gate（见「首差」）。输出留档 `.audit/dsh-ironclad-all-cards-2026-10-09/run-interim.txt` |
| `W06_DUMP_UNSUPPORTED=1` + 同命令 | 1 | 同上，并打印 20 条 `DIAG`（每张未支持卡的**逐字**原因）与四态摘要 |

四态实测（同一构建）：

```
base-spire/ironclad-cards.spire: 声明 94 / 解析 94 / 降级 74 / 未支持 20
base-spire/status-cards.spire:   声明 2  / 解析 2  / 降级 1  / 未支持 1
base-spire/ironclad-powers.spire: 声明 27 / 解析 27 / 降级 17 / 未支持 10
base-spire/potion-powers.spire:  声明 1  / 解析 1  / 降级 1  / 未支持 0
base-spire/shared-powers.spire:  声明 7  / 解析 7  / 降级 7  / 未支持 0
BASE-01 构建：ruleset=baec5147cdaf7503 卡=75 能力=29 敌人=35 起始牌组=10 …未支持=127
```

## 覆盖矩阵分母与 85 池对账

- 源单机奖励池 **85 项**；`ironclad-cards.spire` 里写 `pools = { IroncladCardPool }` 的正是 **85 张**（矩阵已逐张对账）。
- 池缺口（声明了池但**尚未真降级**）**18 张**：`stomp, juggling, rupture, dark-embrace, pillage, aggression, infernal-blade, stoke, drum-of-battle, cascade, havoc, one-two-punch, crimson-mantle, inferno, hellraiser, thrash, stampede, howl-from-beyond`。
- 池外 11 张按源保留真实身份、不凑池：`demonic-shield`、`tank`（源标 `MultiplayerOnly`）、`toxic`、`infection`、`giant-rock`、`entrench`、`slimed`、`wound`、`well-laid-plans`、`dazed`、`beckon`（`status-cards.spire`）。
- 池对账只统计**成功降级**的卡（`RewardWiring.cs` 的 `BuildPool`），所以「让缺口真降级」与「修好池对账」是同一件事——不做随机补抽、不缩池。

## 首差与未验证范围

- **唯一首差（身份 gate，禁止自行修改）**：
  ```text
  BaseSpire.Slice.Tests.CheckFailedException: T05 身份：dev 默认规则与公开构建摘要匹配已复核 Anger 基线 [真实]:
  verified dev ruleset identity: expected 'b4d85a04422074c7', got 'baec5147cdaf7503'
  ```
  触发点 `mod-the-spire/base-spire/BaseSpire.Slice.Tests/SliceChecks.cs` 里的 `SliceMapRoomChecks.DefaultIdentity()`。
  负责人基线：dev 规则 `b4d85a04422074c7`、公开构建 `5b86ab40c7f210a3`、公开语义 `983a2268d49ce4db`。
  **按派单要求：不改 `SliceMapRoomChecks.cs` 与身份 golden、不把实际输出写成预期，留负责人一次性迁移。**
  本批新增内容会使身份继续变化，最终值以收尾时那次实测为准。
- **未验证范围（如实记，不得据此宣称完成）**：
  - 完整 `BaseSpire.Slice.Tests`（含地图）**未跑通**：被上面的身份 gate 拦在它之前的检查之后，且地图工厂仍会因池缺 18 张失败。
  - `dotnet build AutoTheSpire.sln`、`Engine.Tests`、`net.Tests`、`tools/base-spire-dev`（`verify_examples.py`）、`doc-check`、`git diff --check` **尚未在本批次串行跑过**。
  - 正常完整 Run / B 双侧 / 公开 Host：**未执行**。
  - `Stoke` 的 `generateCards`、`Pillage` 的 `drawUntil`、`InfernalBlade` 的 `generateCards` 属生成/抽牌族，**尚未实现**。

## 下一步（具体动作）

1. **生成/抽牌族**：在 `mod-the-spire/base-spire/BaseSpire/Slice/Effects/SourceEffectPrograms.cs` 的 `ReadEffectProgram` 里加 `generateCards` 与 `drawUntil` 结构分支（与家族 2 同形：新领域节点 + `CardEffectPile` 复用 + 链接/校验递归），在 `CardEffectCompiler.cs` / `CombatSlicePackage.cs` 接上「生成到指定牌区」的共享过程。落地后 `Stoke`（池缺口 18 → 17）、`InfernalBlade`、`Pillage` 应转为已降级。
2. **能力族**：`ironclad-powers.spire` 的 10 个未降级能力是 8 张卡「能力引用无法链接」的**真正根因**（`SpirePowerSource.RuntimeImplemented` 白名单缺这些 id）。逐条原因已在 `W06_DUMP_UNSUPPORTED` 输出里（`OneTwoPunchPower` 缺 `extraPlays`/`consumeOnPlayCount`、`JugglingPower` 的 `if(cardPlaysThisTurn(...), cloneCard(...))`、`RupturePower`/`DarkEmbracePower` 的 `applyScaledPower`/`draw` + 多 `events` 未支持等）。**落地能力后必须同步改 `SliceFormalSourceChecks.cs` 里断言 Juggling「无法链接」的检查，以及因支持 X 费而失效的断言。**
3. 每族落地后照家族 1/2 的样子补 `SliceFrontendIroncladCompletionChecks.cs` 的判别，并在本文件追加一小段（真实源形状 / 节点与共享过程 / 首个判别案例）。
4. 收尾：按派单串行跑门槛命令并把命令、退出码、真实执行数写进本文件；重跑矩阵 `--label 最终`。

## 未关闭范围（明确不包含在本批）

- 其余四角色资源、多人环境、未声明卡：不扩展。多人专用项（`demonic-shield`、`tank`）按源排除，**不拿它们当单人缺口**。
- `Toxic` 的 `attack against the owner is not supported by the enemy damage procedure` 与 `Stomp` 的 `selectedCardIsClone` / `modifySelectedCardCost` / `whenCardType` / `effect` 是独立缺口，按「先完成所有其他族」的原则排在后面。
- 本批本地卡牌转绿**不关闭** ENV-T05 父项，也不表示完整环境完成。
