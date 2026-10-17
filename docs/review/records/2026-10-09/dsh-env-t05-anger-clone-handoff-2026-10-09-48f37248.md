---
task_id: ENV-T05
dispatch_id: ENV-T05-anger-clone-pool-20261009
session_id: session-48f37248-c0b5-4661-9174-b87dba1e5f3c
dispatch_record: .audit/dsh-env-t05-anger-clone-pool-2026-10-09/task-record.json
delivery_status: ready_for_review
updated_at: 2026-10-09T23:40:00+08:00
---

> 负责人 2026-10-09 复核：本复制叶按受控正式源范围已验收；已修正重复 pile/嵌套声明被忽略的读取缺口并迁移默认身份。独立串行 build0、Engine95/95、效果25/25、非地图Slice375/375、作者工作流9/9；地图/net仍因其余24卡阻断。原报告“9组”实际调用10条 Check.Run，下面保留执行者原时点证据。当前状态与后继整批只看[主清单](../../tofix/README.md#env-t05)，负责人证据见[收口记录](anger-review-and-card-batch-2026-10-09.md)。

# ENV-T05 Anger 战斗复制机制包 — 交接（ready_for_review，**待验证**）

关联：主清单 [ENV-T05](../../tofix/README.md#env-t05)（本包只补了该条目的本包状态/证据，未改排期或父项）；
约束 [环境实现约束](../../../design/environment-implementation-constraints.md)；
依据 [base readiness 复核](base-readiness-review-2026-10-09.md)（第 70 行 Anger `cloneCard` 一句）、
[逐效果表示设计](../../../design/mod-the-spire/effect-ir/plan.md)、
作者说明 [内容 DSL 使用与设计约定 §7](../../../dsl/0.0.1/dsl-usage.md#7-对照当前实现)；
本包证据与原始日志
[`.audit/dsh-env-t05-anger-clone-pool-2026-10-09/`](../../../../.audit/dsh-env-t05-anger-clone-pool-2026-10-09/task-record.json)
（`task.txt`、`task-record.json`、`final-verify.log`、`build-baseline.log`、`build-impl1.log`、`slice-combat-before.log`、`slice-combat-impl1.log`、`effects-after-impl.log`、`effects-clone1..4.log`、`effects-final.log`、`source-before.json`、`source-after.json`）。

**状态**：本包生产实现、局部判别与全量串行验证已完成；交付状态 `ready_for_review`，**登记为待验证**——
正式规则身份发生预期迁移（新增隐藏字段），本包**未删门槛、未改 golden**，身份/跨侧复核留给 Codex。
**不据此关闭 ENV-T05 或父项，不解锁 dev-map。**

## 1. 交付能力（一个完整机制）

正式内容源里 Anger 的 `cloneCard = { pile = discard }` 从「未支持、整张卡降级」变成「**受限但真实的战斗复制**」：
打出 Anger 时在弃牌堆创建一个**同定义的独立战斗实例**，保存打出时刻的升级形态、实例变量与临时费用修正，
且不进入长期牌组。

- **正式 Anger 原文未改**：`mod-the-spire/base-spire/BaseSpire/Cards/Content/ironclad-cards.spire:1300-1339`
  （normal `:1313`、upgraded `:1331` 各一处 `cloneCard = { pile = discard }`）。同版本源无实际偏差，
  SHA256 前后一致（`bd32a7ba709f78908d724231ea1ff65ec87b99f30753060c55cf43c782d2cc5e`），
  未重排 `ironclad-cards.spire`、未动 IroncladCardPool、未删该效果。
- **只支持这一叶形状**：当前牌（正在打出的实例）→ `discard`。`pile = hand/exhaust/draw`、缺 `pile`、
  未知字段、写在 `hooks` 里、批量 `xN` 都**带源位置显式报错**，不静默降级、不泛化未知字符串。

## 2. 源位置与接口变化

| 落点 | 变更 |
| --- | --- |
| `mod-the-spire/base-spire/BaseSpire/Cards/SpireCardSource.cs` | 新增 `Primitive("clone-card", 0, 1, "", "", 0, "", 0, 0, AuthorSteps.CloneCardToDiscard)` 叶读取器 `CloneCardStep`（调用点 `:1503` 注释 / `:1506` 调用）；`PrimitivesOf` 在 `chooseCard` 分支后新增 `cloneCard` 分支；`StepsFrom` 新增 `case "clone-card"` |
| `mod-the-spire/base-spire/BaseSpire/Authoring/SliceAuthorFrontend.cs` | 新具名作者步骤 `public const string CloneCardToDiscard = "clone_card_to_discard"`；登记进 `AuthorSteps.All`（否则 `.author` 报 `unknown_step`）与 `NonRepeatable`（配 `NonRepeatableReason`）；**不**登记进 `HookSteps` ⇒ hook 内复制继续被 `ValidateHookProgram` 拒绝 |
| `mod-the-spire/base-spire/BaseSpire/Cards/Effects/SourceEffectPrograms.cs` | `ReadEffectProgram` 的 `choose-card` 显式适配旁新增 `clone-card → new LegacyCardEffect(new(primitive.StepKind, primitive.Count), property.Location)`；沿用既有 `LegacyCardEffect` 接缝，**未改** `CardEffectProgram` / `CardEffectCompiler` / `CardEffectExpressions` 的数字语义或接口 |
| `mod-the-spire/base-spire/BaseSpire/Composition/CombatSlicePackage.cs` | (a) `:195` 新增隐藏可选引用字段 `Card.CloneOf`（`FieldTypeSchema.RefTo(card)`，`visibility: Hidden`，`optional: true`，比照既有 `Card.LongTermSource` 的发布边界）；(b) `:2601` 新增具名共享复制过程 `CloneCardFields(IrScope, Expr source)`；(c) `:2956` `StepInstructions` 新增 `case AuthorSteps.CloneCardToDiscard` |

`Legacy` 槽由既有 `CardEffectCompiler` 的 `bindings.Legacy(step)` 接到 `StepInstructions`，
所以**新叶穿过同一条 Engine IR 执行路径**，没有新的调度器、没有在 C# 里驱动 Session、
没有隐藏运行态容器；冻结 core/RandomService 未动。

## 3. 复制矩阵（`CloneCardFields` 的复制/重置策略）

| Card 字段 | 克隆取值 | 依据 |
| --- | --- | --- |
| `DefinitionId` / `Kind` / `Amount` / `PowerId` / `PowerStacks` | 复制源的当前值 | 克隆是同定义的战斗实例，不是回到初始普通定义 |
| `Upgraded` | **复制** | 原版 `CreateClone` 保留升级形态；升级 Anger 的克隆仍打出 8 |
| `Unplayable` / `Ethereal` | 复制 | 实例状态 |
| `Cost` | 复制 | 当前费用 |
| `CostModifier` | **复制（保留）** | 与 `StartNextRoom` 的 `CopyCardFields` **相反**（那里清零）：克隆是战斗内临时实例，创建时刻适用的临时费用状态必须一并快照 |
| `Variable_*`（实例变量） | 复制源的当前值 | 创建时刻快照；此后改源不污染克隆（已判别） |
| `CloneOf` | 设为**本次源实例**引用 | 表达"同源"来源字段；隐藏且可选，未设置时为 `ValueKind.None` |
| `LongTermSource` | **不复制（保持空）** | 克隆没有长期权威，不写回 `LongTermDeck`；已验证长期牌组不增长、进入下一场战斗不被带过去 |
| `ExhaustOnNextPlay`（原版 `CreateClone` 会清） | **未建模，明确未支持** | 当前 Card 实体没有该字段，不在本包范围；不声称"全原版 clone" |

**未直接套用** `StartNextRoom` 的 `CopyCardFields`（长期→战斗副本：清费用修正、设长期来源），
而是小范围提取了独立具名过程并显式选择复制策略。

## 4. 已读 / 已改文件

**已改（允许写清单内，全部在清单范围内）**：上表 4 个生产文件；
`mod-the-spire/base-spire/BaseSpire.Slice.Tests/Cards/CloneCardChecks.cs`（新建，9 组判别）；
`BaseSpire.Slice.Tests/Authoring/SliceChecks.cs`（**仅**注册 `CloneCardChecks.Run();`，紧邻既有 `ConsecutiveAttackChecks.Run();` 之后，即 `:98`）；
`BaseSpire.Slice.Tests/Program.cs`（**仅**在现有 `--effects-only` 入口注册，不新增调度器）；
`docs/dsl/0.0.1/dsl-usage.md`（作者说明，新增 §7 内的 `cloneCard` 支持形状段）；
`docs/review/tofix/README.md`（本包状态/证据一段）。
改前/改后 SHA256 逐文件见 `source-before.json` / `source-after.json`。

**已读（关键）**：`docs/design/mod-the-spire/base-development-guide.md`、`modding-guide.md`、`effect-ir/plan.md`、
`base-spire-implementation-plan.md`（T05/T02）、`spec.md`、`docs/dsl/0.0.1/content-dsl.md`（`:192` cloneCard 规范句）、
`docs/review/records/2026-10-09/base-readiness-review-2026-10-09.md`；原版只读依据
`sts2/src/Core/Models/Cards/Anger.cs`、`Core/Models/CardModel.cs`（`CreateClone`）、`Core/Combat/CombatState.cs`（`CloneCard`）。
**未改** core、net、tools、reference、sts2、SinglePlayer、RewardWiring/RewardCompute、其他卡/Power/敌人生产、身份 golden。

## 5. 构建 / Profile / seed

- 构建：`dotnet build AutoTheSpire.sln`（本包结束时用 `--no-incremental` 串行跑一次）→ **exit 0**，0 错误、3 警告；
  3 个警告全是既有且与新增代码无关（`Maps/Generation/ActMap.cs(42,37)` CS8600、`ActMap.cs(60,33)` CS8600、`Powers/SpirePowerSource.cs(1241,39)` CS8604）。
- Profile：默认 dev 规则集（未切 `dev-map`，未缩池、未加占位卡）。
- seed：正式源夹具固定 `20261001UL`（`FormalSliceFixture.NewFormal()`），与本包所有判别一致。

## 6. 实际命令 / 返回码 / 实际计数

全部原始输出在 `final-verify.log`（311290 字节）；下面是该文件里的定位与计数。

| # | 命令 | 退出码 | 实际计数 / 首差 |
| --- | --- | --- | --- |
| 1 | `dotnet build AutoTheSpire.sln --no-incremental` | 0 | 0 错误 / 3 既有警告 |
| 2 | `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests --no-build` | 0 | `All 95 engine checks passed.`（= 复核基线 95/95） |
| 3 | `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests --no-build -- --effects-only` | 0 | `All effect program checks passed.`；ENV-T05 **9 组全绿**（含 7 条 `[真实]`），与既有 15 组效果程序同批 |
| 4 | `$env:W06_DUMP_UNSUPPORTED=1; … --no-build -- --combat-only --report .audit/dsh-env-t05-anger-clone-pool-2026-10-09/content-report` | **-532462766** | 339 条 `ok`、0 条 FAIL，随后**首差**；`ironclad-cards.spire: 声明 94 / 解析 94 / 降级 68 / 未支持 26`（原 67/27），未支持名单里 Anger 已消失。`--report` 产物**未写出**（`Program.cs` 在 `SliceChecks.RunAll` 之后才写，抛出即止） |
| 5 | `… --no-build -- --report .audit/dsh-env-t05-anger-clone-pool-2026-10-09/content-report-full`（完整 Slice，含地图） | **-532462766** | 同样 339 条 `ok` 后停在**同一首差**；地图检查未执行（门槛在 `SliceChecks.cs:94`，早于 `if (includeMap)`） |
| 6 | `dotnet run --project net-the-spire/Net.TheSpire.Tests --no-build` | 1 | `410 passed / 5 failed / 415 executed`，失败为 G-01…G-05，原因 `InvalidOperationException: base-spire: 奖励牌池对账失败…`——**与复核基线同缺口同数**，本包未引入新失败 |
| 7 | `python tools/doc-check/check_docs.py` | 0 | — |
| 8 | `git diff --check` | 0 | 注意：本包改动落在仓库未跟踪目录（`mod-the-spire/…`、`docs/review/tofix/`），该命令看不到差异，只作为格式门槛记录 |

**首差（预期身份迁移）**：`BaseSpire.Slice.Tests.CheckFailedException: T06 身份：dev 默认规则与公开构建摘要匹配逐效果 IR 基线 [真实]: verified dev ruleset identity: expected 'f87614af13d18e0c', got 'b4d85a04422074c7'`，
位置 `SliceMapRoomChecks.DefaultIdentity()`（`SliceMapRoomChecks.cs:42`）经 `SliceChecks.cs:94` 调用。
原因＝新增隐藏字段 `Card.CloneOf` 进入规则身份（默认 dev 规则集身份逐字节变化）。
`SliceMapRoomChecks.cs` **不在本包允许写清单内**，故未改该断言/门槛；旧值 `f87614af13d18e0c`、新值 `b4d85a04422074c7` 与失败位置一并交 Codex 做身份/跨侧复核。

## 7. 本包局部判别（`--effects-only`，9 组）

1. 正式源 `cloneCard` 叶被降级、`anger` 不再出现在未支持名单；
2. 只支持 `pile = discard`；别的牌区/缺 `pile`/额外字段带**源位置**失败；
3. **正式 Anger 普通**：费用 0、扣血 **6**、**只问一次目标**、弃牌堆多出一个同 `DefinitionId` 的独立克隆 `[真实]`；
4. **正式 Anger 升级**：扣血 **8**、克隆保留升级形态与升级数值；
5. 克隆保存**创建时刻**的实例变量与临时费用状态，之后改源不污染克隆 `[真实]`；
6. **最后击杀之后复制仍然执行**，原牌走完出牌尾 `[真实]`；
7. 克隆**没有长期来源**、长期牌组不增长、进入下一场战斗也不会被带过去 `[真实]`；
8. hook 里复制继续被拒绝（步骤不在 hook 允许面内）；
9. `clone_card_to_discard` 在词表里、可解析、不可批次、不在 hook 允许面内；`.author` 路径打出后弃牌堆多出一个同定义克隆 `[真实]`。

夹具分列：本包判别全部用**正式 `SpireCardSource` 资源/实际 Model** 与正常 Run 夹具（`FormalSliceFixture`，
含 `RunStates`/`LongTermDeck`）；`.author` 合成场景只用于第 9 组的作者步骤可达性，不替代正式源证据。

## 8. 未验证 / 已知边界（交复核）

- **身份门槛未通过**（见 §6 首差），因此「非地图 Slice 365/365 + 本包 9 组」的合计计数**本包拿不到**：
  门槛在 `SliceChecks.cs:94`，本包注册点在 `:98`，`RunAll` 抛出即止。局部证据由 `--effects-only` 提供。
- **未在真实 Host/地图路径执行**：dev-map 仍因其余 24 张缺卡拒绝，reward pool 对账仍失败（net G-01…G-05），
  本包不据此解锁地图或关闭 T05。
- **不是完整原版 clone**：星费快照、附魔（enchantment）、折磨（affliction）、克隆来源链（`selectedCardIsClone` 家族）、
  `ExhaustOnNextPlay`、`pile = hand` 等形状**明确保留未支持**，不做静默解释。
- **同实例同回合二次出牌**仍是 `PlayedThisTurn` MembershipConflict（T06 已独立定位），本包**没有**把它当前置、
  也没有改集合契约；需要重放时使用新的克隆实例（第 6/7 组即如此）。
- 未逐字段对拍原版 `AbstractModel.DeepCloneFields` 全字段集合；本包按"可表达字段"列矩阵（§3），未建模字段如实记录。
- 未验收：本包**未**自行验收 T05/M01/完整环境。降级 67→68、奖励池缺 25→24 是**记账**，不是环境验收。

## 9. 下一已就绪成果

主清单 ENV-T05 的父项目标仍是「第一条真实地图战斗奖励闭环」：源真实普通战斗奖励生成/领取/选牌或跳过/容量、
幂等 → 长期状态提交 → 地图合法后继，阶段门控阻止战斗/奖励未完成时旅行。
本包交付后奖励池缺牌 25→24，下一包按缺牌七族由负责人选族接续（Anger 所属机制族已闭合）。
**请复核者先做**：核对新增隐藏字段带来的身份迁移（旧 `f87614af13d18e0c` → 新 `b4d85a04422074c7`）是否可以入册，
再决定后续包的计数基线。
