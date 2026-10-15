---
task_id: ENV-T04-power-fixture-20261011
dispatch_record: .audit/dsh-power-fixture-2026-10-11/task-record.json
session_id: session-a89e1013-7e22-47b7-8ae2-af638525f30a
request_id: 4963a6ed-bb23-43d2-a560-0defaa1c6fb2
delivery_status: tooling-delivered-strict-regression-red
updated_at: 2026-10-11
---

# ENV-T04 受控 Power 初值机制包交接（B）

本包修复工具的受控初值适配。原 17 项完整执行一次：14 passed / 1 self_checked / 1 diverged / 1 unsupported_rng_profile，严格 rc=1；原 14 项自检保留，新增 8 项实际执行判别全部通过，总计 22/22。Power 原首差消失，但 ENV-T04 整体不能据此关闭。主清单由负责人更新，本会话交付后停止。

派发见[固定记录](../../../.audit/dsh-power-fixture-2026-10-11/task-record.json)。该记录及同目录的[任务原文](../../../.audit/dsh-power-fixture-2026-10-11/task.txt)、[输入摘要](../../../.audit/dsh-power-fixture-2026-10-11/inputs-before.json)保留原样。执行记录见[完整回归](../../../.audit/dsh-power-fixture-2026-10-11/regression/regression.json)、[随机/配置/轨迹/文件摘要证据](../../../.audit/dsh-power-fixture-2026-10-11/delivery-evidence.json)、[正常 Run 阶段诊断](../../../.audit/dsh-power-fixture-2026-10-11/init-upfront/init-upfront-diagnosis.json)。run_id 为 `regression@env-t04-regression@1+20261010T185302Z`；suite SHA256 为 `5cca423f8e0d298f8229d5b5ad14cbe0fb104515645c796db4a858d31af223a2`。机器时间与任务日期不同，保留真实产物时间，不改写 run_id。

## 改变文件与共同初值契约

- [BaseBackend](../../../tools/differential/BaseBackend.cs#L608)：`GivePower` 由嵌入作者声明 `SliceAuthorFrontend.Load().Powers` 推出 `Ticks = declared.OwnerTurnEnd == PowerTurnEnd.Tick`；没有声明即显式失败。`SkipNextTick=false`。增加仅自检使用的错误初值执行入口，在 Start 后、首个快照前改字段并执行真实脚本。
- [ComparatorSelfTest](../../../tools/differential/ComparatorSelfTest.cs#L88)：保留原 14 项，追加两种错误初值和玩家/敌方各三类已持有 Power 对照。
- [FixtureSuite](../../../tools/differential/FixtureSuite.cs)：注释明确 `setup.powers` 是已持有初值。
- [工具 README](../../../tools/differential/README.md)：补共同契约、时点、方向和实际自检。
- [原 suite](../../../tools/differential/suites/regression.json)：仅原 power-turn-end-decay 的标题和 provenance.note 纠正旧“Ticks=false”描述；没有新增初值字段。17 个 ID、脚本、seed、牌组、目标、profile、比较维度与终点不变。证据提取对照[负责人原报告](../../../.audit/dsh-plan-2026-10-11/owner-regression/regression.json)逐 case 核验可见合同及完整分母一致。
- 本包审计目录新增回归双方原记录、诊断、证据与[提取脚本](../../../.audit/dsh-power-fixture-2026-10-11/collect-evidence.ps1)，以及本交接。

[受控初始化契约](../../design/environment-implementation-constraints.md#env-t04受控power初值边界2026-10-11)规定脚本前已经持有。实际受控 base 走程序化切片；[冻结作者镜像](../../../mod-the-spire/base-spire/BaseSpire/Slice/Content/slice-content.author)的 Strength 为 `none`，Vulnerable/Weak 为 `tick`，不是给所有 Power 同一个布尔值。[base 调度](../../../mod-the-spire/base-spire/BaseSpire/Slice/CombatSlicePackage.cs#L2320)只在 Ticks 成立时处理减层，并消费 SkipNextTick。工具据此构造实际初态，没有专设 Vulnerable 名字分支。

参考独立从自己的模型克隆，[ReferenceBackend](../../../tools/differential/ReferenceBackend.cs#L241)调用 `ApplyInternal(owner, stacks)`；[参考 PowerModel](../../../reference/sts2-sim/src/Sts2Sim.Core/Models/PowerModel.cs#L83)仅设置拥有者/层数及登记，没有新施加的跳过语义。[源 PowerCmd](../../../sts2/src/Core/Commands/PowerCmd.cs#L144)只对运行中新施加给玩家的 Debuff 设置 `SkipNextDurationTick=true`。所以初始持有状态的双方 skip 均 false；施加方向不能替代初始化时点。[源 Vulnerable](../../../sts2/src/Core/Models/Powers/VulnerablePower.cs#L59)及[参考 Vulnerable](../../../reference/sts2-sim/src/Sts2Sim.Core/Models/Powers/VulnerablePower.cs#L53)、[参考 Weak](../../../reference/sts2-sim/src/Sts2Sim.Core/Models/Powers/WeakPower.cs#L43)的持续衰减在 `AfterSideTurnEnd` 检查 `CombatSide.Enemy`，本脚本对玩家/敌方持有者均在敌方 side 结束后衰减；不概括为各自拥有者回合末。

未修改参考源码/适配器、生产 base/core/net/AI、主清单和 A/C 文件；没有用 base 状态制造参考、放宽 profile、过滤 Power/牌区、补抽、重做地图 port 或随机观察器。

## 实际命令与完整分母

全部命令在仓库根执行，构建隔离目录 `.artifacts/parallel/dsh-power-fixture`。

```powershell
dotnet build tools/differential/Differential.csproj -c Release -p:UseArtifactsOutput=true -p:ArtifactsPath=F:/Scylier-Project/active/auto-the-spire/.artifacts/parallel/dsh-power-fixture
# rc=0；初次构建和最终注释/文档配套构建均0；4个既有nullable警告、0错误。
dotnet .artifacts/parallel/dsh-power-fixture/bin/Differential/release/Differential.dll test --suite regression --output .audit/dsh-power-fixture-2026-10-11/regression
# rc=1；原17项完整实际执行一次，附带22项自检。
dotnet .artifacts/parallel/dsh-power-fixture/bin/Differential/release/Differential.dll init-upfront --seed ENV-T04-RNG-SHUFFLE-001 --records .audit/dsh-power-fixture-2026-10-11/regression/records --output .audit/dsh-power-fixture-2026-10-11/init-upfront
# rc=0，仅只读窄归因。
powershell -NoProfile -ExecutionPolicy Bypass -File .audit/dsh-power-fixture-2026-10-11/collect-evidence.ps1
# rc=0；直接执行脚本曾被系统ExecutionPolicy拒绝(rc1)，改为本进程Bypass后完成。
```

报告内 command 是工具生成的等价 `dotnet run` 命令文本；实际执行的是上述隔离 DLL。`complete=true`，expected/selected/executed=17，reference_ran=16（wiring 独立单侧断言）。

|分类|完整原 ID|
|---|---|
|passed（14）|strike-normal；strike-upgraded；defend-normal；defend-upgraded；bash-then-strike-vulnerable；bash-upgraded；candidate-scope-partly-playable；headbutt-mid-choice；enemy-death-aftermath；power-turn-end-decay；vulnerable-decay-via-bash；last-kill-before-terminal；map-generated-alpha；map-generated-beta|
|self_checked（1）|factory-wiring-dev-scope|
|diverged（1）|rng-initial-shuffle|
|unsupported_rng_profile（1）|random-reshuffle-unsupported|

## Power 轨迹、随机计数与负例

|真实执行|base 与参考轨迹|结论|
|---|---|---|
|原 power-turn-end-decay，enemy:0 Vulnerable|boundary0=2、boundary1=2、boundary2=1|原双方首差修复；真实减层|
|原 vulnerable-decay-via-bash|0→2→1|真实 Bash 施加及衰减保持|
|玩家/敌方 Strength 六组对照中的两组|2→2|非衰减者不变|
|玩家/敌方 Vulnerable/Weak 四组|2→1|双方初态与持续轨迹一致，全量 Comparator passed|
|实际错设 Ticks=false|boundary2 stacks 2≠正确1|必要负例 detected=true|
|实际错设 SkipNextTick=true|boundary2 stacks 2≠正确1|必要负例 detected=true|

负例首差均 `boundary[2].power.enemy.base-spire:calcified-cultist.0.stacks`，不是仅篡改最终 JSON。原 14 项包含 Power 字段、牌区/候选/随机 gate 等判别，全部继续执行；8 项新增不计作原 suite case，不缩减17分母。

原 Power seed 为 `ENV-T04-CTRL-power-turn-end-decay`；Bash 为 `ENV-T04-CTRL-vulnerable-decay-via-bash`。两侧 source/profile/configuration 和原 game_seed 已记录：base 为受控切片+正式相同随机配置，reference 为独立 sts2-sim 顺序路径、显式 `source-compat-sts2-frozen@1`（12条 run 具名流；非重键模式）。相关源/参考/作者/适配文件 SHA256、base session_identity、reference_identity 原样摘入证据。

|case|base 初始化 / 脚本 before→after|reference 初始化 / 脚本 before→after|proof|
|---|---|---|---|
|power-turn-end-decay|0 / 0→0|519 / 519→519|双方 applied=true；完整 ordered-starting-piles@1|
|vulnerable-decay-via-bash|0 / 0→0|509 / 509→509|双方 applied=true；完整 ordered-starting-piles@1|
|random-reshuffle-unsupported|0 / 0→9|508 / 508→517|双方 applied=true；仍 unsupported|

base 单位为 raw-generator-words，reference 为 native-counter-increments，不能把初始化总数跨侧直接比较或把 unknown 当0。`controlled-state-no-random@1` 只证明双方脚本实际各自零增量。源初始化消费已单列，不伪称无随机开局。

## 正常 Run 窄归因与未验证范围

`rng-initial-shuffle` 原 seed `ENV-T04-RNG-SHUFFLE-001` 仍为 diverged，首个规范事实差为 `boundary[0].enemy.base-spire:fuzzy-wurm-crawler.alive`：base missing、reference present。本包不改正常 Run。

只读 `init-upfront` 对原 seed 得出 run_seed_u32=3850333834、up_front_stream_seed_u32=2615586789。参考 run_constructed/profile_applied 均0；player_created 累计230；room_entered 累计295。独立 CloneExact 阶段重放：共享遗物袋112 + 角色遗物袋118 + 共享古代人洗牌0 + act0事件洗牌30 + 普通槽16 + 精英槽17 + boss1 + ancient1 =295。原 base UpFront=17、reference=295，差278；参考首遭遇 `FUZZY_WURM_CRAWLER_WEAK`。这与其他旧 seed 的计数不能混用。

最早可定位阶段是遗物袋前置初始化：[源 InitializeNewRun](../../../sts2/src/Core/Runs/RunManager.cs#L445)先共享袋再玩家袋；[源玩家袋](../../../sts2/src/Core/Entities/Players/Player.cs#L363)负责缺失时填充；[参考 CreatePlayerRelicGrabBag](../../../reference/sts2-sim/src/Sts2Sim.Core/Runs/RunState.cs#L242)实现相同顺序。Dev base 的[Setup 队列入口](../../../mod-the-spire/base-spire/BaseSpire/Slice/CombatSlicePackage.cs#L3969)只在存在 treasure wiring 时接前置，其 Dev 场景未接该 wiring，随后直接建遭遇队列；[工厂范围描述](../../../mod-the-spire/base-spire/BaseSpire/SinglePlayer/BaseSpireRuntimeFactory.cs#L193)明确前置阶段/全量池/精英等缺口。Ironclad 其他新范围已有共享袋接线，不能把 Dev 缺口泛化为所有 base 范围。

交 Codex 的最小生产建议：在本 case 实际 Dev 正式工厂路径接上共享/玩家遗物袋的源顺序与真实源池消费，先用 player_created 阶段112/118独立核对；后续事件/整幕生成及池子子集仍单列差异，继续找首差，不能补278次空抽或据该归因宣告随机规则验收。未在本包启动第二生产包。

`random-reshuffle-unsupported` 实际双方消费随机，首条差异证据为 `boundary[3].pile.draw.initial-slots`：base `start:05,start:00,start:08,start:03,start:07` vs reference `start:03,start:06,start:04,start:09,start:01`。共6条差异只作路径证据，不作规则裁决，不升级无随机 profile。

未执行 base 完整地图套件、其他生产测试、训练、提交/reset/清理或子 agent；没有恢复旧会话。双方事件规范化与未支持 Run/RNG 范围仍未验证，不以机制修复关闭整体验收。负责人下一步：核验本包证据、更新 T04 主清单，保留上述两项红态；将正常 Run 首阶段生产缺口交 Codex 独立安排。
