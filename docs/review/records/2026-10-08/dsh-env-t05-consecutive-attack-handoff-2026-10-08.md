---
task_id: ENV-T05
dispatch_id: ENV-T05-consecutive-attack-repair-20261008
session_id: session-20bb6663-e012-4391-bffb-64516cc5a5df
dispatch_record: .audit/dsh-env-t05-consecutive-attack-2026-10-08/task-record.json
delivery_status: ready_for_review
updated_at: 2026-10-08T23:46:53+08:00
---

# ENV-T05 A 线「连续两张攻击牌」故障修复交接 — 2026-10-08

> `delivery_status: ready_for_review`。本包只交**一个故障小包**：正式内容路径下第二张攻击牌把会话打成 `backend_fault`。已完成「复现（修复前）→ 首个失败点 → base 侧合法 IR 修复 → 真实 Host 复跑（修复后）→ 有限针对性行为检查 → 串行构建与既有检查」。**不重做地图、不开下一包、不宣称父项 ENV-T05 验收、父项仍开放。**

## 一句话

PlayCard 守卫在执行**只读查询**（`Session.LegalActions`）时对"能量付不起的牌"会求值 `CardRuleCostAllows`，其中含纯过程调用 `CallPure(base-spire:PowerStacksOf)`；会话此刻正挂起在第二张攻击牌的目标决策上（`AwaitingDecision`），`InvokePure` 无法 drain ⇒ `SessionStateInvalid`，`Fatal` 再把只读查询升级为**会话级 `Faulted`**。修复=把这个守卫用的纯过程调用**内联为等价查询表达式**（同一 relation、同一选序、同一"未找到即 0"语义），只改 `Composition/CombatSlicePackage.cs` 一处；core 一行未动。

## 归属

- 任务：主清单 [第一条真实地图战斗奖励闭环](../../tofix/README.md#env-t05)（`docs/review/tofix/README.md:120` 锚点、`:30` 任务行、`:124` 认领行、`:130` 首差登记）。
- 父任务 `ENV-T05`；本包 `dispatch_id: ENV-T05-consecutive-attack-repair-20261008`；派单原文 `.audit/dsh-env-t05-consecutive-attack-2026-10-08/task.txt`；派单记录同目录 `task-record.json`。
- 前驱：地图/房间包 `docs/review/records/2026-10-08/dsh-env-t05-map-room-handoff-2026-10-08.md`（`ready_for_review`，其 `:107` 首次登记本首差）；C 线序列包 `docs/review/records/2026-10-08/dsh-env-t03-host-sequences-handoff-2026-10-08.md:111-115` 给出真实调用栈与最小复现。
- 顺序门来源：本包记录的 `edit_compiled_sources_after_records` / `build_after_records` 都指向 `.audit/dsh-env-t03-map-host-2026-10-08/task-record.json`（C 地图 Host 包），watcher 监视的正是该包的会话 `session-3baaf675-9539-441a-b208-7ee00280721c`。
- 设计依据：[环境实现约束](../../../design/environment-implementation-constraints.md)、[net 协议](../../../design/net-the-spire/protocol.md)、[角色分工](../../../design/post-core-agent-assignments.md)。
- 证据目录：`.audit/dsh-env-t05-consecutive-attack-2026-10-08/`（源码前后快照、Host NDJSON、日志、探针）。

## 接手后先做什么

1. `dotnet build AutoTheSpire.sln -v m` → 期望 0 错误（本包实测 2.8s / 0 错误）。
2. `dotnet run --no-build --project core-the-spire/Core.TheSpire.Engine.Tests` → `All 95 engine checks passed.`
3. `dotnet run --no-build --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` → `All 344 slice checks passed.`（本包实测 259.38s；339 既有 + 本包新增 5）。
4. 真实 Host（**改了 base 后必须先重建 Host 项目**：Host 的 bin 里带自己的 `BaseSpire.dll` 副本，`--no-build` 不会刷新）：
   `dotnet run --project net-the-spire/Net.TheSpire.Host -- --scenario formal-dev --game-seed ENV-IRONCLAD-00`
   驱动脚本：`python -X utf8 -u .audit/dsh-env-t05-consecutive-attack-2026-10-08/probe/probe_consecutive_attack.py ENV-IRONCLAD-00 formal-dev base-spire:strike-ironclad <dump-dir> [scope]`（`scope` 传 `dev-map` 走地图范围；第二张换成 `base-spire:defend-ironclad` 是对照）。
5. 旧 0.1 兼容：`python -X utf8 -u .audit/dsh-env-t05-consecutive-attack-2026-10-08/probe/probe_legacy_01.py 7`。
6. 顺序门自检：本包在 C 地图 Host 会话 `session-3baaf675-…` **实际停止**（watcher 判据 `C-SESSION-IDLE 2026-10-08T23:09:28+08:00`）后才改 base 生产与构建；接手前请自行确认当前没有别的会话在跑 dotnet（同 obj/bin 禁止并行构建）。

## 完成条件逐项（派单 → 实际）

| 派单完成条件 | 实际 |
| --- | --- |
| 同 seed / 同动作前缀：修复前失败、修复后真实连续两张攻击 | ✅ 修复前 3 条路径全 `backend_fault`（见下"验证与证据"）；修复后同 seed 同前缀四条探针全部 ok，第二张真实造成伤害 |
| 含合法目标决策 | ✅ 第二张攻击 step 与随后的 `answer_decision` step 均 ok（决策候选来自 `PendingDecision` 投影） |
| 稳定快照查询成功 | ✅ 修复后每次 `Settle → Snapshot → Observe + LegalActions` 都正常返回（修复前正是这一步炸） |
| 无/有相关 Power、同定义多实例、第二张技能对照能区分真实堆叠/归属 | ✅ 新检查②（无 Corruption 时 0 费规则**不得**生效）、③（Corruption 时技能牌 0 费真实生效且消耗）、④（同定义两实例 ⇒ `PowerStacks` 与归属语义不变）；Host 侧 Defend 对照路径正常 |
| 重复 observe/LegalActions 不改变玩法/RNG | ✅ 新检查⑤：三次重复 `LegalActions/PendingDecision/Observe/PowerIds` 后 `StateHash`/`Version`/`TotalDraws`/`Status` 全不变 |
| 默认 Dev 与 DevMap 公开正常路径都尝试 | ✅ `dev` 与 `dev-map` 各跑一遍（修复前 + 修复后） |
| 不用 F08 筛 fault 策略 | ✅ 探针按卡牌 `content_id` 选牌、按 `:Monster` 引用选目标，无任何"挑不 fault 的 session / 重试"逻辑 |
| 不吞异常、不删合法候选、不把 pure 改 stateful、不造影子 Power 缓存/第二 RunState、不注入清场或改血 | ✅ 唯一生产改动是守卫表达式的等价内联（下"实际修复"）；无 try/catch、无重试、无候选删减、无缓存、无状态改写 |
| 顺序门（C 停止后才改 base 生产与构建） | ✅ 见上第 6 条；本包所有构建都在 `23:09:28` 之后 |
| 串行 root build → Engine → Slice | ✅ root 0 错（2.8s）→ Engine 95/95（1.08s）→ Slice 344/344（259.38s） |
| net 公开连续攻击 + 旧 0.1 兼容 | ✅ Host 四条探针；0.1 `legacy-slice --seed 7` 连续 3 次攻击全 ok；net 套件 415/415（118.68s） |
| 新增有限针对性行为检查覆盖根因与真实调用链 | ✅ `BaseSpire.Slice.Tests/Combat/ConsecutiveAttackChecks.cs` 5 条，走真实 `base-spire:PlayCard` 动作与真实守卫（见下） |
| 保存真实 NDJSON/config/seed/run_id/状态与诊断前缀 | ✅ 每步完整响应 NDJSON 落在 `.audit/…/logs/{before,after}/<run>/0*.json`；探针 stdout 打印诊断前缀；差分 `run_id` 见下 |
| 边界外依赖明确 blocked、能交的先交 | ✅ 无阻塞；core 缺口以**提案**形式交负责人（不改 core，见"差异、阻塞与未验证"） |

## 故障与根因（首个失败点）

- **复现（修复前，真实 Host NDJSON）**：seed `ENV-IRONCLAD-00`，Strike → 回答目标决策 → 第二张 Strike/Bash ⇒
  `error={"code": "backend_fault", "execution_state": "unknown", "message": "SessionStateInvalid: pure procedure 'base-spire:PowerStacksOf' did not complete; pure procedures cannot suspend", "retry_mode": "stop"}`。
  拼写变体 seed `ENV-IRCLAD-00` 与 `--scope dev-map` 都复现同一条错误（两次原始输入分别核实保留）；第二张换 `defend-ironclad`（技能牌）**成功**。
- **真实调用栈（修复前 Host stderr，与 C 线报告一致）**：
  `Session.Eval (core-the-spire/Core.TheSpire/Engine/Session.Expressions.cs:44)`×3
  ← `Session.<>c__DisplayClass54_0.<LegalActions>b__0 (Engine/Scheduler.cs:251)`
  ← `Fatal (Engine/Scheduler.cs:484)` ← `LegalActions (Engine/Scheduler.cs:220)`
  ← `ControlSession.Snapshot() (net-the-spire/Net.TheSpire/Control/ControlSession.cs:328)`
  ← `Settle (ControlSession.cs:346)` ← `ControlSession.Step` ← `ControlHost.Step (ControlHost.cs:412)`
  ← `ControlHost.Dispatch (ControlHost.cs:123)` ← `Program.Handle (Program.cs:324)`。
- **机制（逐环由源读 + 取证确认）**：
  1. 第二张攻击牌需要选目标 ⇒ 该 step 结束时 `Status == AwaitingDecision`（`_pending` 未清）；net 的 `Settle` 无条件 `Snapshot()`，而 `Snapshot` 一定调 `Session.LegalActions`（`ControlSession.cs:327-333`）。
  2. `LegalActions`（`Scheduler.cs:211-269`）对每个候选求值 `Eval(probe, action.Guard)`（`:251`）；PlayCard 候选域＝整只手牌 ⇒ 手牌里**每一张**都会过守卫。
  3. PlayCard 守卫（`Composition/CombatSlicePackage.cs:4052-4056`）＝ `Or(EnergyIsEnough(), CardRuleCostAllows(...))`，`LogicExpr` 短路（`Session.Expressions.cs:42-48`）⇒ **只有能量付不起的牌**才求值右侧。
  4. 修复前 `CardRuleCostAllows`（`:3939-3960`）右侧含 `Greater(CallPure(PowerStacksOf, …), 0)`（原 `:3948-3951`）⇒ `CallPureExpr → InvokePure`（`Session.Expressions.cs:127-140`）；挂起态下 `DrainTo(depth)` 不推进 ⇒ `:133 if (_stack.Count > depth)` 抛 `Faults.Of(EngineFaultCode.SessionStateInvalid, "pure procedure … cannot suspend")`。**错误文案与真实条件不符：纯过程体本身不会挂起，是会话此刻已挂起。**
  5. `Fatal` 把 `Status` 置 `Faulted` ⇒ 该 step 变成 `backend_fault`，同一 session 后续请求全部失败。
  6. **实测状态完全吻合**：修复前 `logs/before/dev-strike-strike/02-answer-1.json` 显示挂起前玩家 `Energy=2`、手牌 `[strike(1), bash(2), defend(1), defend(1)]`；第二张 Strike 花 1 费后剩 `Energy=1`，手牌 Bash(2) **不可支付** ⇒ 守卫右侧必被求值。（对照：第二张 Defend 不产生目标决策 ⇒ 会话仍 `Running` ⇒ `InvokePure` 正常 drain ⇒ 成功。）
- **base 侧可达性清点**：全 base 30 处 `CallPure` 全在 `CombatSlicePackage.cs`；其中在**守卫**（`Requires`，只读查询期求值）里的只有 `:3949` 这一处（其余在动作体内，`Running` 期执行，安全）。纯过程全 base 只有 `PowerStacksOf` 一个（`:286-300`，`.Pure()` 仅 `:291` 一处）。

## 实际修复（唯一生产改动）

`mod-the-spire/base-spire/BaseSpire/Composition/CombatSlicePackage.cs`，`CardRuleCostAllows` 内，原 `:3948-3951` → 现 `:3948-3958`（`before`/`after` 快照见 `.audit/…/{before,after}/CombatSlicePackage.cs`）：

```diff
             foreach (var rule in rules)
             {
-                var holds = scope.Greater(
-                    scope.CallPure(powerStacksOf.Symbol, scope.First(scope.Type(player, battleScope)),
-                        scope.Const(rule.Id)),
-                    scope.Const(0L));
+                // ENV-T05：守卫是**只读查询期**求值的表达式，不能调用纯过程——会话挂起在决策上时
+                // InvokePure 无法 drain，core 会把 LegalActions 打成 faulted。这里内联 PowerStacksOf
+                // 的过程体：同一 relation、同一 First 选序、同一"未找到即 0"起点，语义与
+                // `Greater(PowerStacksOf(...), 0)` 逐字等价；And 短路保证不存在时不再求 Field。
+                var ruleOwner = scope.First(scope.Type(player, battleScope));
+                var rulePower = scope.First(scope.Where(scope.Relation(powers, ruleOwner),
+                    power => scope.Equal(scope.Field(power, powerDefinition), scope.Const(rule.Id))));
+                var holds = scope.And(scope.Not(scope.IsNone(rulePower)),
+                    scope.Greater(scope.Field(rulePower, powerStacks), scope.Const(0L)));
```

语义等价性依据：`PowerStacksOf` 的过程体（`:292-300`）＝ `SetLocal(value, 0)`、`SetLocal(found, First(Where(Relation(powers, Owner), p => Equal(Field(p, powerDefinition), PowerId))))`、`If(Not(IsNone(found)), SetLocal(value, Field(found, powerStacks)))`、`Return(value)`；因此 `Greater(PowerStacksOf(o,id), 0)` ⇔ `And(Not(IsNone(First(...))), Greater(Field(First(...), powerStacks), 0))`，且 `And` 短路保证"未找到"时不会求值 `Field(None, …)`。过程本体保留给 `Running` 期路径（如 `PayCostSteps :3975-3978`），未删除。

**旧路径不受影响的结构性论据**：循环遍历的是 `author.Powers` 里声明的 `cardRule`；`author is null` 或无此类能力时循环不执行 ⇒ 生成的 IR 与修复前**逐字相同**（0.1 程序化切片、无 Power 的 formal 场景都走这条）。

## 新增检查（`BaseSpire.Slice.Tests/Combat/ConsecutiveAttackChecks.cs`，5 条，已注册）

注册点：`SliceChecks.cs:95 ConsecutiveAttackChecks.Run();`（紧随 `PublicSemanticsChecks.Run();`）。全部走真实生产包 + 真实 `base-spire:PlayCard` 动作与真实守卫，不添加镜像实现断言：

1. `ENV-T05 挂起态 LegalActions 不得报错：第二张攻击牌 + 手牌含不可支付牌`（Strike 打完 → 提交 Bash(2) 停在目标决策、`Energy<1`、手牌留 1 费 Defend）⇒ 挂起态 `session.LegalActions(Player)` **不得抛错**、`Status` 仍 `AwaitingDecision`、回答决策后第二张真实掉血。
2. `有 Corruption 时技能牌 0 费规则真实生效，且挂起态查询安全`。
3. `无 Corruption 时 0 费规则不得生效`：1 能量下 2 费技能 ⇒ `Check.Fault(IllegalAction)` 且拒因来自守卫。
4. `同定义两实例：PowerStacks 与归属语义不变，且挂起态查询安全`。
5. `重复 observe/LegalActions 不改变玩法/RNG`：`StateHash`/`Version`/`TotalDraws`/`Status` 三次查询前后不变。

## 接口/语义变化

- **公开身份必然重基线**（`RuleSet.Identity → RulesetDigest → PublicBuildId`；IR 形状变了，任何合法修法都会如此）：

| 范围 | build（旧 → 新） | ruleset（旧 → 新） |
| --- | --- | --- |
| base `dev` | `52b285cbc40711cc` → `f291cc66cf6a9050` | `04078d3be6c4b66c` → `39246f7e8412b142` |
| base `dev-map` | `29abce0b690a3d22` → `6b25fa16e5b567f4` | `3a57e1e3cd84a183` → `3e7e18a9570ac26f` |
| net `dev` | （同上 build） | `net:ruleset:4206675da0ca12f6` → `net:ruleset:6ac4cb3cb2b696c6` |
| net `dev-map` | （同上 build） | `net:ruleset:508a1c95ddb54d1d` → `net:ruleset:a1d8aa9faa64b143` |

- 本包已更新 **base 侧唯一绝对 golden**：`SliceMapRoomChecks.cs:39-40`（`DevGoldenRuleset="39246f7e8412b142"`、`DevGoldenBuild="f291cc66cf6a9050"`），并加注释说明重基线原因；该检查语义未改。
- **跨线交接项（net 对本包只读，未改）**：`net-the-spire/formal-dev-scopes.md:26-27` 的四个旧值需网线负责人更新；主清单 `docs/review/tofix/README.md:98` 的"`dev` 缺省身份逐字未变（ruleset `4206675da0ca12f6`）"已过期，应由主清单维护者更新。历史记录（`docs/review/tofix/dsh-env-t03-map-host-handoff-2026-10-08.md:62`、`docs/review/records/2026-10-08/dsh-env-t03-{base-metadata,base-sequences,host-sequences}-handoff-*.md`、`dsh-env-t05-map-room-handoff-2026-10-08.md:85/:91`）按历史标注、不追改。
- **行为语义未变**：修复前/后 Host 观测号逐字相同（`675 / 683 / 687 / 1174 / 1182`），出牌顺序、能量、怪物血量与 `PlayedThisTurn` 一致 ⇒ 只有**查询路径**变了。

## 验证与证据

| 检查 | 工作目录与实际命令 | 返回码 / 结果 | 证据与证明范围 |
| --- | --- | --- | --- |
| 修复前复现（dev） | `python -X utf8 -u .audit/…/probe/probe_consecutive_attack.py ENV-IRONCLAD-00 formal-dev base-spire:strike-ironclad .audit/…/logs/before/dev-strike-strike`（修复前 Host 二进制，`--no-build`） | 第一张 + 决策 ok，第二张 `ok=False` `backend_fault` `SessionStateInvalid: pure procedure 'base-spire:PowerStacksOf' did not complete; pure procedures cannot suspend` | `logs/before/dev-strike-strike.log`（含 8 帧真实栈）与逐步 NDJSON |
| 修复前复现（拼写变体 / dev-map / 技能对照） | 同上，seed `ENV-IRCLAD-00`；`--scope dev-map`；第二张 `defend-ironclad` | 两条复现同错误；Defend 对照 ok | `logs/before/{dev-irclad-strike-strike,devmap-strike-strike,dev-strike-defend}.log` |
| 修复后回归（4 条） | 同上 4 条，改在修复后重建的 Host 上跑 | 4 条全 ok；第二张真实造成伤害 | `logs/after/{dev-strike-strike,dev-strike-defend,dev-irclad-strike-strike,devmap-strike-strike}.log` + 各自 `0*.json` |
| 旧 0.1 兼容 | `python -X utf8 -u .audit/…/probe/probe_legacy_01.py 7`（`--scenario legacy-slice --seed 7`） | identity `protocol_version=0.1`、`net:ruleset:base-spire-combat-slice-v1`；`start` 结果键仅 `['session_id','snapshot']`；**连续 3 次攻击**（step1/3/5）与其目标决策（step2/4）全 ok | `logs/after/legacy-01.log` |
| root 构建 | `dotnet build AutoTheSpire.sln -v m` | **0**（2.8s，0 错误） | `logs/after/root-build-final.log` |
| 引擎检查 | `dotnet run --no-build --project core-the-spire/Core.TheSpire.Engine.Tests` | **0**，`All 95 engine checks passed.`（1.08s） | `logs/after/engine.log` |
| 切片检查（含新增 5） | `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | **0**，`All 344 slice checks passed.`（259.38s；含 map 身份检查重基线后通过） | `logs/after/slice-final3.log` |
| net 检查 | `dotnet run --no-build --project net-the-spire/Net.TheSpire.Tests` | **0**，`415 passed / 0 failed / 415 executed`（118.68s） | `logs/after/net.log` |
| 严格差分 suite | `powershell -NoProfile -ExecutionPolicy Bypass -File tools/differential/test-env.ps1 -Suite regression -Output .audit/…/differential/regression` | **1**（严格 suite 诚实红态），`run_id=regression@env-t04-regression@1+20261008T154401Z`，`counts={'expected':15,'selected':15,'executed':15,'passed':8,'self_checked':1,'failed':6,'reference_ran':13,'self_test_failures':0}` | `logs/after/differential.log` + `differential/regression/regression.json`；与基线 `artifacts/environment/tests/regression/regression.json`（`run_id=…T100245Z`）逐 case 判决**完全相同**（对比脚本 `probe/compare_regression.py`）⇒ 6 条非通过是既有边界，非本包回归 |

**产物哈希**

- `mod-the-spire/base-spire/BaseSpire/Composition/CombatSlicePackage.cs` sha256 `8345B0F8F0A94665E3D2FDEDC273C68351ABCC9ED4A303671BFD0B85E3417A26`（306358 B；`before` = `6CF29560405509C01A257E72F730B1D81965E32D0FC268632D0380B284074771`，305677 B）。
- `BaseSpire.Slice.Tests/Combat/ConsecutiveAttackChecks.cs` sha256 `D541815AE8372C2DC167B7F082ECFD0B7F6B08E20FAD1E06DAB2FD00E95CFF66`（11623 B）；`SliceChecks.cs` `B10EE2FAB9DFF1243CF8BE1F419F4FB91B2E3BB4397450FAEE62B20DCC58D524`；`SliceMapRoomChecks.cs` `B1107404252DC471C4943FC9AF5BDE139F854376CEC841AD5E46D4B1F0441DA8`。
- DLL：`Net.TheSpire.Host.dll` `B1A0B73F3D6FC9E5CC5AB4FD6BB15EEEE8739468AF6B989FE1DFA373861038D4`（43008 B，23:17:42）、`Net.TheSpire.dll` `B2BA55134E650E6EA66F61B86B881F495B1B21562DB12A4F568B2C1809F33EE7`、`Net.TheSpire.Tests.dll` `8BFA4D50EB6FE8FBFABB82F4B7AE8C15790E529556886C229453D00BB9400A56`、`BaseSpire.dll` `28A02EDFBB0F6EB7E7740240F528E56FF061D7C2F03A7F270389B9BA48D7C93A`（846336 B，23:20:55）、`BaseSpire.Slice.Tests.dll` `B2D0C01A713A8621127F32FE25AAF7DEF16FC9E2CB6AC0463FF8FA9473EC3BE6`。
- 修复前 Host 二进制（= 复现所用）：`net-the-spire/Net.TheSpire.Host/bin/Debug/net9.0/Net.TheSpire.Host.dll` `LastWriteTime 2026-10-08 22:21:33`。

## 差异、阻塞与未验证

1. **core 缺口（只报告，未改 core）**：`LegalActions` 是**只读查询**，却被宿主在 `AwaitingDecision` 下正常调用（net `ControlSession.Snapshot` 每次都调），而规则 IR 守卫里合法出现的 `CallPure` 在挂起态无法完成，且 `Fatal` 把只读查询升级为**会话级 Fault**——同一处境下 `Session.Step`（`Scheduler.cs:400-401`）只做干净的 `Illegal` 拒绝、会话仍可用。最小 core 复现＝本包同 seed/同前缀。建议（任选其一，交 core 负责人）：① 挂起期 `LegalActions` 安全返回（跳过/空）；② 以可恢复错误（`Illegal` 一类）拒绝而不是 `SessionStateInvalid`+`Faulted`；③ 若确要禁止，则在装配期显式拒绝守卫内的过程调用。受影响范围：任何守卫/投影里的纯过程调用（base 现仅 `PowerStacksOf`）。**base 侧已能合法修好，故本包不是 blocked。**
2. **跨线 re-baseline（必须由网线负责人做）**：`net-the-spire/formal-dev-scopes.md:26-27` 与主清单 `docs/review/tofix/README.md:98` 的旧身份值。net 未被我改动；net 检查用的是相对身份比较，415/415 通过。
3. **身份重基线本身**：见上表。旧值仍散见于历史记录文档（列出处），按历史标注、不追改。
4. **未验证 / 未做的（诚实边界）**：
   - 新增检查的**修复前红态未在切片套件内演示**：本包新检查注册在 `SliceChecks.RunAll()` 末位，而 `SliceMapRoomChecks.Identity()`（`:52`）在前且断言绝对 golden ⇒ 修复前源码下套件在身份检查处即中止（实测），到不了新检查；"修复前必失败"由真实 Host 复现的**同一调用链**与新检查①的断言（挂起态 `LegalActions` 不得抛错）共同保证。首次尝试还踩到**陈旧二进制陷阱**（`Copy-Item` 保留快照旧 `LastWriteTime` ⇒ MSBuild 跳过重编译、实际跑的是修复后代码），该次日志保留为 `logs/before/slice-suite-prefix.log`。
   - 严格差分 suite 仍是**诚实红态 rc=1**（6 条既有边界：1 `unsupported_decision_model`、4 `unsupported_rng_profile`、1 `diverged`），本包不承诺变绿，只证明"逐 case 判决与基线完全相同"。
   - 0.1 兼容只跑到"连续 3 次攻击 + 目标决策"这一层，未覆盖 0.1 的其它操作码。
   - 未做：真实奖励/选牌/跳过/容量→长期提交→地图返回；源初始化前置 112/118/30 与全 16 候选 T08 依赖（派单明确本包不并开）。
5. **仍开放的窄缺口**：`EncounterPool`/`EncounterQueue` 可见事实（声明 `NotPublished` 但投影仍发布成员列表/size/capacity）——本包未动标记、未暴露未来遭遇，维持开放（建议见 `.audit/dsh-env-t05-map-room-2026-10-08/narrow-gap-encounter-publication.md`）。
6. **本包未做的事**：未改 core/net/参考/AI/主清单；未提交、未 push、未清理工作区；未开下一包。

## 维护位置

- 生产改动：`mod-the-spire/base-spire/BaseSpire/Composition/CombatSlicePackage.cs`（`CardRuleCostAllows`，`:3939-3960`）。
- 检查：`mod-the-spire/base-spire/BaseSpire.Slice.Tests/Combat/ConsecutiveAttackChecks.cs`（新增）+ `SliceChecks.cs`（注册）+ `SliceMapRoomChecks.cs:39-40`（golden 重基线）。
- 诊断与证据：`.audit/dsh-env-t05-consecutive-attack-2026-10-08/`
  - `diagnosis.md`（根因分析全文）、`task.txt`/`task-record.json`（派单）；
  - `before/`、`after/`（修复前后源码快照 + 哈希）、`patch/ConsecutiveAttackChecks.cs`；
  - `probe/{probe_consecutive_attack.py,probe_legacy_01.py,compare_regression.py}`；
  - `logs/before/**`、`logs/after/**`（Host NDJSON dump、逐步响应、各检查日志）、`differential/regression/**`。
- 接手第一步：见"接手后先做什么"第 1-4 条；若报告功能验收，请连同本包列出的 net 文档 re-baseline 与 core 缺口提案一并转交负责人。
