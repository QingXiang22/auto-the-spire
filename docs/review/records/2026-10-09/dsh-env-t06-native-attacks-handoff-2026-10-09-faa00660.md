---
task_id: ENV-T06
dispatch_id: ENV-T06-native-attack-sequences-20261009
session_id: session-faa00660-0654-4a7b-bfae-4b852b21f288
dispatch_record: .audit/dsh-env-t06-native-attack-sequences-2026-10-09/task-record.json
delivery_status: ready_for_review
updated_at: 2026-10-09T16:56:00+08:00
---

# ENV-T06 原版攻击序列族（TwinStrike / Bash / Thunderclap）源核验与正式行为案例 — 交接

负责人2026-10-09复核：本包11项受控正式源行为已限定验收，证据移入records；完整范围和后继认领见主清单。本文原称“引擎侧”的重放故障经独立复现裁定为base的 `PlayedThisTurn` 计数建模问题，core集合拒绝符合冻结契约，详见[负责人验收与工程量记录](base-readiness-review-2026-10-09.md)。本报告的测试自报与原始过程保留。

本包用**同版本 sts2 原版源码**独立核对了 TwinStrike / Bash / Thunderclap 的声明、目标域、逐击语义、施加易伤的
顺序与升级数值，并在**正式内容源**（不是新建测试卡、不是 IR 字符串比较）上补了 11 条可判别的普通/升级行为检查。
**结论：三张牌的正式声明与原版逐项一致，本包没有发现需要修正的内容偏差**，因此没有改动任何生产文件；
11 条检查全部通过（非地图 Slice 354 → 365），默认路径身份未变。仍未完成的范围：vanilla 双侧对拍、
`HittableEnemies` vs `Alive` 的目标域细分、以及一个构建判别手段时撞到的引擎侧契约缺口（见「差异」）。

关联：主清单 [ENV-T06](../../tofix/README.md#env-t06)（父项，**由负责人维护，本包只在维护区补本次证据**）；
方法 [base-development-guide.md](../../../design/mod-the-spire/base-development-guide.md)、
[modding-guide.md](../../../design/mod-the-spire/modding-guide.md)、
[dsl-usage.md](../../../dsl/0.0.1/dsl-usage.md)、
[effect-ir/plan.md](../../../design/mod-the-spire/effect-ir/plan.md)；
原版依据 `sts2/src/Core/Models/Cards/{TwinStrike,Bash,Thunderclap}.cs`、
`sts2/src/Core/Commands/Builders/AttackCommand.cs`、
`sts2/src/Core/Combat/CombatState.cs:142`；
本包证据 [`.audit/dsh-env-t06-native-attack-sequences-2026-10-09/`](../../../../.audit/dsh-env-t06-native-attack-sequences-2026-10-09/)
（`task-record.json`、`task.txt`、`gate-*.log`、`replay-same-instance-membership-conflict.md`）。

本报告记录的是 2026-10-09 的交付；当前排期、状态与认领以主清单为准。**本包不关闭 T06/M01，也不代表完整 Ironclad。**

## 接手后先做什么

1. 核对主清单 ENV-T06 行的认领与依赖；本包已交付的部分**不要重做**：声明/降级契约核对、`repeat` 两击判别、
   Bash 先伤害后易伤（含正对照）、Thunderclap 全敌人域与不提问，均已有行为检查。
2. 直接复跑本包判别（工作目录 `F:\Scylier-Project\active\auto-the-spire`）：
   `dotnet run --no-build --project mod-the-spire/base-spire/BaseSpire.Slice.Tests -- --combat-only`
   预期 `All 365 slice checks excluding map checks passed (--combat-only).`、返回码 0，其中 11 条以
   `ENV-T06 攻击序列族：` 开头（见下表）。失败时先看异常里是哪一条，不要改 golden 或删卡池。
3. 需要负责人/Codex 决策的两项：①「差异」节里的 `PlayedThisTurn` 契约缺口要不要单开一个机制包（本包无权改
   `CombatSlicePackage.cs`）；②本包是否判 `ready_for_review` 之后的收口。
4. 本包**没有**待续的机制实现；按主清单接续顺序，A 线下一项是 ENV-T05 真实奖励与地图返回（本包不涉及）。

## 已交付：源定位与普通/升级契约（已核对，无偏差）

| 卡 | 正式声明位置 | 原版依据 | 普通契约 | 升级契约 | 结论 |
| --- | --- | --- | --- | --- | --- |
| TwinStrike | `ironclad-cards.spire:2438-2478`（普通 2442-2459 / 升级 2460-2477） | `TwinStrike.cs`：费用 1、Attack、Common、AnyEnemy、`DamageVar(5m)`、`.WithHitCount(2)`、`OnUpgrade` 伤害 +2 | 费用 1、`selectOne`/alive、`repeat = { count = 2, effect = { attack = { damage = 5 } } }` | 费用 1、每击 7 | 一致 |
| Bash | `ironclad-cards.spire:1459-1502`（普通 1462-1481 / 升级 1482-1501） | `Bash.cs`：费用 2、Attack、Basic、AnyEnemy、`DamageVar(8m)`+`PowerVar<Vulnerable>(2m)`、OnPlay **先** `DamageCmd` **后** `PowerCmd.Apply<VulnerablePower>`、升级 +2/+1 | 费用 2、`selectOne`/alive、先 `attack 8` 后 `applyPower Vulnerable 2`（目标＝所选目标） | 费用 2、先 10 后 3 层 | 一致 |
| Thunderclap | `ironclad-cards.spire:2535-2580`（普通 2538-2557 / 升级 2559-2579） | `Thunderclap.cs`：费用 1、Attack、Common、AllEnemies、`DamageVar(4m)`+`PowerVar<Vulnerable>(1m)`、OnPlay **先** `TargetingAllOpponents(...)` **后** 对 `HittableEnemies` 各 1 层、升级伤害 +3 | 费用 1、`selectAll = {}`、`attack 4 target = allEnemies` 后 `applyPower Vulnerable 1 target = allEnemies` | 费用 1、7 / 1 层 | 一致 |

三张牌的 `pools = { IroncladCardPool }` 与池行 `ironclad-cards.spire:4319` 也一并核对；TwinStrike 的
`tags = { Strike }` 与原版 `CanonicalTags` 一致。**逐项差异为 0 ⇒ 无内容修正，卡块未被本包改动。**

### 本包写入的两个文件（都在测试侧，生产/组包器改动 = 0）

- 新增 `mod-the-spire/base-spire/BaseSpire.Slice.Tests/Combat/NativeAttackSequenceChecks.cs`（303 行，11 条 `Check.Run`）：
  `internal static class NativeAttackSequenceChecks`，只复用 `FormalSliceFixture`，不复制运行调度器；
  受控写只限 `FormalSliceFixture.GivePower`（力量/易伤）与本地 `MarkUpgraded`（只写卡实例的 Bool `Upgraded`）。
- 修改 `mod-the-spire/base-spire/BaseSpire.Slice.Tests/Authoring/SliceChecks.cs`：仅 +1 行注册
  `NativeAttackSequenceChecks.Run();`（在 `ConsecutiveAttackChecks.Run();` 之后）。
- **未修改** `ironclad-cards.spire`（其 `LastWriteTime` 为 2026-10-01 23:14，本会话 2026-10-09 16:xx 未触碰）。

### 复用已有交付（未重做）

- 每效果独立参数 / 条件与 repeat / 显式 X 付款 / 随机目标槽、`NormalProgram`/`UpgradedProgram` 作为执行依据、
  `CardEffectExpressions` 抽取、动作收尾在外层：均为前序包已交付，本包只**消费**。
- 已有检查（未重复）：`SliceFormalSourceChecks.cs:461-484` 已用 8 点证明 Bash 先伤害后易伤；
  `:1079-1088` 已证明 TwinStrike 总伤害 10；`SliceFrontendApplyPowerAllChecks.cs` 已用**作者文本**证明
  Thunderclap 全体 4/1 与升级 7。本包补的是它们缺的那一半：逐击判别、正对照、升级行为跑在**正式源**上。

### 本次新增的 11 条检查（`--combat-only` 里搜 `ENV-T06 攻击序列族：`）

1. 受控初态无无关能力 + 三张牌由正式包真实注册（从**会话状态**读实例，不靠声明自证）。
2. 声明→解析→降级→逐效果程序四态与原版卡级契约对齐（费用/目标域/能力名/层数/`Pools`/`Upgradeable`，
   `NormalProgram` 与 `UpgradedProgram` 均非 null；不静默省略）`[真实·声明/降级层]`。
3. TwinStrike 普通：同一目标 10、单目标、费用 1、弃牌堆、不施加易伤、只提问一次目标。
4. TwinStrike 逐击判别：受控挂 2 层力量 ⇒ **(5+2)×2 = 14**（若两击被并成一次 10 点只会是 12）——这是
   "两击 ≠ 一次同总伤"的判别证据，依据 `AttackCommand.Execute` 每一击各调一次 `CreatureCmd.Damage`。
5. TwinStrike 升级：只置 `Upgraded` 标记（不写 Amount/PowerStacks/Cost）⇒ 7×2 = 14 ⇒ 证明升级数值来自
   `UpgradedProgram`（`CombatSlicePackage.cs:4697` 按 `Upgraded` 字段选变体），而不是卡级显示槽。
6. Bash 普通：先 8（不是 12）后 2 层、另一敌人 0 层、费用 2、弃牌堆、随后打击 6 → 9。
7. Bash 正对照：受控给目标 2 层易伤 ⇒ 伤害**确实**是 12、层数 2+2=4 —— 它排除"引擎根本不会用易伤放大伤害"
   这一解释，使第 6 条的 8 只能来自"先伤害后易伤"。
8. Bash 升级：10 后 3 层、费用仍 2。
9. Thunderclap 普通：两个敌人各 −4、各 +1 层、**目标提问 0 次**（`selectAll`）、费用 1、弃牌堆。
10. Thunderclap 顺序与后排：先各 −4 后各 +1；再用一张 8 点攻击打**后排** ⇒ 12（8×1.5）⇒ 后排的易伤真的挂着、
    且第一次伤害没被自己刚上的易伤放大。
11. Thunderclap 升级：各 −7、各 1 层（层数不升）。

## 验证与证据

工作目录一律 `F:\Scylier-Project\active\auto-the-spire`；序列跑一次，未循环重跑不变门槛。

| 检查 | 实际命令 | 返回码/结果 | 证据与证明范围 |
| --- | --- | --- | --- |
| 解决方案构建 | `dotnet build AutoTheSpire.sln` | 0；0 警告 0 错误 | `gate-1-build.log`；只证明编译通过，**不**代表 .spire 解析/注册/行为 |
| Engine 测试 | `dotnet run --no-build --project core-the-spire/Core.TheSpire.Engine.Tests` | 0；`All 95 engine checks passed.` | `gate-2-engine-tests.log`；与基线 95/95 一致 |
| 非地图 Slice | `dotnet run --no-build --project mod-the-spire/base-spire/BaseSpire.Slice.Tests -- --combat-only` | 0；`All 365 slice checks excluding map checks passed`（`ok` 行 365；基线 354 + 本包 11） | `gate-3-slice-combat-only.log` |
| 完整 Slice（含地图） | `dotnet run --no-build --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | **非 0**（`-532462766`）；跑到 340 条 `ok` 后在 `SliceMapRoomChecks.Identity()` 抛 `base-spire: 奖励牌池对账失败：源单机清单有 25 项没有进内容池…`（`aggression, anger, cascade, …`，出自 `Rewards/RewardWiring.cs:692`） | `gate-4-slice-full.log`；**与基线同一个 ENV-T05 缺口（25 张奖励池卡）**，名单里没有本族三张牌；本包未删池/改门槛/改 golden |
| 纯文档门槛 | `python tools/doc-check/check_docs.py` | 0；`scanned 320 Markdown files / 0 problem(s)` | `gate-5-doc-check.log` |
| 空白/行尾 | `git diff --check` | 0（stderr 只有工作区既有的 LF→CRLF 警告） | `gate-6-git-diff-check.log`；注意 `BaseSpire.Slice.Tests/` 是未跟踪目录，`git diff --check` **看不到**本包两个文件，另行人工核对新文件尾随空白 = 0 行 |

运行身份（`gate-3` 日志原文）：`BASE-01 构建：ruleset=f87614af13d18e0c 卡=68 能力=29 敌人=35 …`、
`ENV-T03 元数据：semantics=9d92f078cdb3ff5f build=dc9325c6b9b5cca9 ruleset=f87614af13d18e0c` —— 与 Codex
基线**逐字相同**；`T06 身份：dev 默认规则与公开构建摘要匹配逐效果 IR 基线 [真实]` 也通过 ⇒ 本包对默认规则身份影响为 0。
构建 Profile/seed：`FormalSpirePackage.BuildRuleSet`（`formal-source-subset-v1`）+ `seed 20261001` +
`SourceWeightedPickBackend`（`FormalSliceFixture.NewFormal()` 的生产入口）。

## 差异、阻塞与未验证

- **生产首差：无**。三张牌的声明/目标域/逐击/顺序/升级与原版逐项一致，卡块一字未改。
- **测试侧首个差异（已修正，记录以便复核维护成本）**：最初写的 "两次打出 Thunderclap" 判别手段撞到引擎侧
  `unexpected EngineFaultException: MembershipConflict: #120 is already a member of 'base-spire:PlayedThisTurn'`
  —— 同一卡实例在同一回合二次打出（从弃牌堆拿回手牌再打）被 `Composition/CombatSlicePackage.cs:239` 的集合成员资格拒绝。
  判别手段因此改为"Thunderclap + 一张 8 点攻击打后排"（第 10 条）。完整最小复现、异常原文、原版依据与所需语义见
  [`replay-same-instance-membership-conflict.md`](../../../../.audit/dsh-env-t06-native-attack-sequences-2026-10-09/replay-same-instance-membership-conflict.md)。
- **需 Codex 处理的明确契约缺口**：`PlayedThisTurn` 不能用"实例集合"承载"本回合已完成的出牌计数"。原版按**历史事件**计数
  （`sts2/src/Core/Models/Cards/Normality.cs:33`、`Fetch.cs:30`），没有任何"同实例一回合只能打一次"的守卫，而原版允许
  经 Headbutt/抽牌把同一实体牌一回合打两次。需要的语义：计数改成事件/计数器口径，或让该集合允许重复成员并按次数计。
  **本包未改** `CombatSlicePackage.cs`（禁止写入范围），也未伪造回退路径。
- **未验证边界**：① Thunderclap 伤害侧按 `IsAlive` 逐击刷新、易伤侧按 `HittableEnemies`
  （`CombatState.cs:142`）——两者在正常两敌人盘面上重合，本夹具无法区分，未主张已判别；
  ② 本包全部结论都是在**受控初态**（自己写力量/易伤/升级标记/把牌拿进手牌）下取得的，**不代表**正常整局，
  也不是独立双侧对拍；③ 未运行 `tools/base-spire-dev`（本包判别都走正式源路径，不需要独立命名空间卡文件）；
  ④ 完整 Slice/net 地图门槛仍被 ENV-T05 的 25 张奖励池卡阻塞，与基线一致。
- **依赖**：ENV-T05（奖励池对账）未完成前，完整 Slice/公开出口门槛不能作为本包验收依据；本包只用 `--combat-only` 判本族。

## 本地反馈步骤与维护成本信息（给 Codex 复核，未推测 token 节省）

1. `dotnet build mod-the-spire/base-spire/BaseSpire.Slice.Tests/BaseSpire.Slice.Tests.csproj -v q`（2.2–4.5s）先过编译。
2. `dotnet run --no-build --project mod-the-spire/base-spire/BaseSpire.Slice.Tests -- --combat-only`，
   用 `Select-String '攻击序列族|All |Exception'` 只看本族与总行；本族 11 条在 `RunAll` 末尾，
   单次整轮约几秒，无需为单卡再写调度器。
3. 编译期来回 3 处**测试侧**写法问题（都不是内容偏差）：`SpireCardSource.Declaration.Id` 是**源级** id（`twin-strike`）
   而 `AuthorCard.Id` 带包前缀（`base-spire:twin-strike`）；声明层 `Variant.PowerId` 保留**能力名**（`Vulnerable`）
   而运行时是 `base-spire:vulnerable`；`FormalSliceFixture.SetNumber` 只写 `long`，Bool `Upgraded` 需自己用 `Value.Of(true)`。
4. 运行期首个差异即上面的 `MembershipConflict`（1 次），改判别手段后 11/11 通过。
5. 进入组包器（packager）的生产改动：**0**（本包只加测试文件与 1 行注册）。读到的生产文件：
   `Sources/ironclad-cards.spire`（三块与池行）、`Cards/SpireCardSource.cs`（`Source`/`Declaration`/`Variant`、
   `LoadResource`/`Parse`/`Lower`、目标域映射 `:1650-1697`）、`Composition/FormalSpirePackage.cs`、`Composition/SliceManifest.cs`
   （`:144-164` 常量、`:349` Bash 清单项、`:357` 力量摘要）、`Composition/CombatSlicePackage.cs`
   （`:167`/`:239`/`:238`/`:4441-4448`/`:4666`/`:4697`）、`sts2` 侧 `TwinStrike.cs`/`Bash.cs`/`Thunderclap.cs`/
   `DamageCmd.cs`/`AttackCommand.cs`/`PowerCmd.cs`/`CombatState.cs:142`/`Normality.cs:33`/`Fetch.cs:30`。

## 维护位置

- 任务状态/认领：主清单 ENV-T06 项（本包已在维护区补本次证据，未改其他行或排期）。
- 方法与契约：`docs/design/mod-the-spire/base-development-guide.md`、`effect-ir/plan.md`（本包未改，无需回写）。
- 本包证据：`.audit/dsh-env-t06-native-attack-sequences-2026-10-09/`；本交接
  `docs/review/records/2026-10-09/dsh-env-t06-native-attacks-handoff-2026-10-09-faa00660.md`（负责人并入清单后移动）。
