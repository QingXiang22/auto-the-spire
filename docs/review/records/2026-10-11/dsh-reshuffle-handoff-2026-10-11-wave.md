# ENV-T04 重洗比较与首差归因交接 — 2026-10-11

本包在固定并发闭包上重新结算原 17 项严格分母，消除受控重洗比较里的**工具侧随机游标缺口**，并把残余首差定位到 base 重洗实现缺少源 `StableShuffle` 预排序这一步；base 修复与「是否把该 case 升级为可计通过」由负责人决定。

- task_id: ENV-T04（父项）／本包 `ENV-T04-reshuffle-followup-20261011`
- dispatch_id: `ENV-T04-reshuffle-followup-20261011`
- session_id: `session-c4edccc4-e229-48f6-a79b-bfc8ad71c64c`
- request_id: `2ab7a201-afb8-490c-8699-596c8397285f`
- dispatch_record: [.audit 派发记录](../../../../.audit/dsh-reshuffle-followup-20261011/task-record.json)
- delivery_status: `ready_for_review`
- updated_at: 2026-10-11T22:30:13+08:00

归属与接手入口：归属[主清单 ENV-T04](../../tofix/README.md#env-t04)（本包行由负责人 Codex 维护，本会话按派单只写本交接与自有证据）。交付范围＝`tools/differential/` 工具侧适配/诊断、对应 README 方法、本包 `.audit` 证据与构建；未改 base/core/net/reference 生产，未改任何生产 csproj，未提交/推送/打 tag。本报告记录的是 2026-10-11 的交付；当前排期、认领与状态以主清单为准，`ready_for_review` 不是验收，也不关闭父项。

## 接手后先做什么

1. 先核对[主清单 ENV-T04](../../tofix/README.md#env-t04)与 `git status --short`。本包**已交付**：原 17 项在固定闭包上的 baseline 与最终结算、受控重洗游标对齐（已独立双侧验证）、重洗首差的源裁定与精确 base 缺口；**不要重做** T08 真实新 Run 初始化、普通/精英队列分离、地图两 seed、Power 初值/受控初始化、正常开局逐次消费诊断（[旧诊断交接](../../tofix/dsh-startup-alignment-handoff-2026-10-11-f68b943d.md)只用于第一次不一致用途，本包未重开）。
2. 负责人按 base 主责派一个机制包，实现下面「base 缺口」并复算。执行者第一步：在本包验证工程上跑定向 case，确认首差位置，再按源语义改 base，最后跑完整 17 一次。命令见「验证与证据」，定向命令为
   `dotnet .artifacts/parallel/dsh-reshuffle-followup-20261011/bin/Debug/net9.0/Differential.dll test --suite regression --only random-reshuffle-unsupported --output <本包新的 .audit 目录>`；
   前置条件＝base 已加预排序；预期结果＝`boundary[3]` 两侧牌序不再有该首差（仍按 Profile 声明判 `unsupported_rng_profile`，rc 仍非零）。
3. 若负责人要**升级**该 case 的比较口径（从 `unsupported_rng_profile` 改为可计通过），需同时补参考侧逐边界 `streams` 投影：现在参考侧缺 `streams(reference)`（见「未验证与缺口」），否则会改判 `missing_dimension`，不能算通过。该升级属 Profile/case 声明变更，本会话未做、也未代负责人决定。
4. 未获派单前不要在 base/core/reference 写生产修复；本包的 `RestoreStream` 只服务受控注入 case，不是 base 的替代。

## 已交付与接口变化

**工具侧新增（受控 case 必要比较接线）**

- `tools/differential/RngSourceCompat.cs`：新增 `RestoreStream(RunRngSet, string runStreamType, string stringSeed)`（`#if REFERENCE_SIM`）。它只把**一条** run 级流按 `source-compat-sts2-frozen@1` 用参考自己的 `RunRngSet.MockRng` 重新装回种子（游标归零），返回 `(Before, After)`；`UsesSemanticKeys=true` 时照旧拒绝。
- `tools/differential/ReferenceBackend.cs`：`Build` 在 `setup.InitialPiles is not null`（即受控牌区注入）时，于 `ApplyOrdered` 之后调用 `RestoreStream(run.Rng, "Shuffle", game_seed)`，并把「游标 Before→After + 只对齐这一条流 + 不改 seed/维度/Profile/牌区声明」写进参考记录 `notes`，不做静默对齐；`RunControlled` 收录该说明。
- 为什么需要：base 受控切片按声明顺序发牌、`Shuffle` 流消费 0；参考侧的战斗开始是它自己的原生流程（`Player.PopulateCombatState → DrawPile.RandomizeOrderInternal → UnstableShuffle`），必然先用 `Shuffle` 流洗一次开局牌堆（10 张＝9 次）。适配器随后的 `ApplyOrdered` 只覆盖**牌序**，游标不会回来，于是脚本开始前参考侧比 base 多 9 次偏移。这是纯工具侧不对齐，不是任何一侧的规则结论。
- 接口/语义变化：只影响参考侧受控单场路径的**随机起始状态**；`RngSourceCompat.cs` 内容变化使 `reference_identity.profile_code_sha256` 由 `sha256:952e57a0…` 变为 `sha256:1830ab9b…`（所有 reference 记录随之变化，属预期身份信号）。base 侧、core/base/net/reference 生产、生产 csproj、suite 定义（`tools/differential/suites/regression.json`，sha256 仍 `5cca423f…`）全部未改；原 17 ID/seed/维度/预期 terminal/Profile 声明逐项未动。
- 影响面实测：对齐后除 `random-reshuffle-unsupported` 之外，其余 16 case 的 `verdict`/`base_status`/`reference_status`/维度/终点/`divergences` **逐项不变**；记录文件层面只有新增的 `notes[2]` 与 `profile_code_sha256` 两项差异（见 `bounded-change-check.txt`、`record-field-diff.txt`）。
- 方法记录写入 [tools/differential/README.md](../../../../tools/base-migration-baseline/differential/README.md#重洗比较2026-10-11)（新增「重洗比较（2026-10-11）」小节，并在工具契约段加指针）。

## 验证与证据

工作目录一律 `F:\Scylier-Project\active\auto-the-spire`。`$fixed` ＝ `.artifacts/parallel/package-decoupling/bin/Differential/debug`（只读并发闭包），`$verify` ＝ `.artifacts/parallel/dsh-reshuffle-followup-20261011/bin/Debug/net9.0`，`$audit` ＝ `.audit/dsh-reshuffle-followup-20261011`。

| 检查 | 实际命令（上述路径展开） | rc / 结果 | 证据与证明范围 |
| --- | --- | --- | --- |
| 固定闭包身份 | `Get-ChildItem $fixed -File \| % { Get-FileHash $_ -Algorithm SHA256 }` | rc0；Core.TheSpire `9071EC93…F1E3`、BaseSpire `44F4796B…E976`、Sts2Sim.Core `05A5228E…DFAE`、旧 Differential.dll `C18F6502…4491` | [闭包与构建 SHA](../../../../.audit/dsh-reshuffle-followup-20261011/fixed-closure-sha256.txt)。闭包只读，未被本包写入 |
| baseline（原 17 分母） | `dotnet $fixed/Differential.dll test --suite regression --output $audit/baseline` | **rc1**；17/17 执行，15 双侧 passed／1 self_checked／1 unsupported_rng_profile，自检 22 项 0 失败，complete=true | [报告](../../../../.audit/dsh-reshuffle-followup-20261011/baseline/regression.json)；suite sha256 `5cca423f…` |
| 验证工程构建 | `dotnet build $audit/verify/DifferentialVerify.csproj` | **rc0**，0 警告 0 错误；输出 `$verify/Differential.dll`，SHA256 `2776C338…C696` | [临时 csproj](../../../../.audit/dsh-reshuffle-followup-20261011/verify/DifferentialVerify.csproj)＋[Directory.Build.props](../../../../.audit/dsh-reshuffle-followup-20261011/verify/Directory.Build.props)；`Reference` 指向固定闭包，`REFERENCE_SIM` 保留，未动生产 csproj、未写其他构建根 |
| 定向重洗（子集） | `dotnet $verify/Differential.dll test --suite regression --only random-reshuffle-unsupported --output $audit/aligned-directed` | rc1；complete=false；首差已变为 `boundary[3].pile.draw.initial-slots` 的牌序差；子集缺 decision 变异目标致 1 项自检失败（子集固有限制） | [定向报告](../../../../.audit/dsh-reshuffle-followup-20261011/aligned-directed/regression.json) |
| 最终完整 17（一次） | `dotnet $verify/Differential.dll test --suite regression --output $audit/final` | **rc1**；17/17 执行，15 passed／1 self_checked／1 unsupported_rng_profile，自检 22 项 0 失败，complete=true；run_id `regression@env-t04-regression@1+20261011T142431Z` | [报告](../../../../.audit/dsh-reshuffle-followup-20261011/final/regression.json)、[日志](../../../../.audit/dsh-reshuffle-followup-20261011/final-regression.txt)。必需 unsupported 保持非零 |
| 独立随机模型复算 | `python $audit/verify-reshuffle-model.py` | rc0；用只读源语义独立重写 `hash32`/xoshiro256**/descending Fisher–Yates，逐值复现两侧实际输出与**引擎指纹** | [模型输出](../../../../.audit/dsh-reshuffle-followup-20261011/reshuffle-model-output.txt)、[脚本](../../../../.audit/dsh-reshuffle-followup-20261011/verify-reshuffle-model.py) |
| 改动影响面 | `python $audit/bounded-change-check.py`、`record-field-diff.py`、`case-verdict-check.py` | rc0；suite sha256 不变；仅 reshuffle 的 `divergences` 值变，其余 16 case 各字段不变；记录层面其余差异仅 `notes[2]` 与 `profile_code_sha256` | [bounded-change](../../../../.audit/dsh-reshuffle-followup-20261011/bounded-change-check.txt)、[field-diff](../../../../.audit/dsh-reshuffle-followup-20261011/record-field-diff.txt)、[verdict-check](../../../../.audit/dsh-reshuffle-followup-20261011/case-verdict-check.txt)。比对输入为 `$audit/baseline` 与 `$audit/aligned`；`aligned` 是同一最终构建在补 README 之前跑的完整 17 项，判定与 `final` 相同 |
| 文档检查 | `python tools/doc-check/check_docs.py` | rc=29；扫描 422 篇，29 个问题**全部**是既有文件（`dsh-candidate-binding-20261011` 的 .artifacts 25 个、[Power 交接](../../tofix/dsh-power-fixture-handoff-2026-10-11.md) 2 个、[开局诊断交接](../../tofix/dsh-startup-alignment-handoff-2026-10-11-f68b943d.md) 2 个），本包新增/修改文件 0 个（含本交接与 README 新锚点） | [日志](../../../../.audit/dsh-reshuffle-followup-20261011/doc-check.txt)。不把全仓文档检查写成通过 |
| 空白检查 | `git diff --check` | rc0；只有既有共享文件的 LF/CRLF 提示 | [日志](../../../../.audit/dsh-reshuffle-followup-20261011/diff-check.txt)。注意本包多数文件未跟踪，该命令不覆盖它们 |

**未运行的门槛**：`dotnet build AutoTheSpire.sln`、Engine、Slice、net、训练与归档套件均未跑——本包没有改玩法、公共编排、DSL 或 net；工具侧只跑了上面的验证工程与严格 suite。正式公开复放与共同构建门槛由负责人串行收口。

## 重洗首差归因（受控 case，与正常 Run 分列）

case `random-reshuffle-unsupported`：受控切片包、`game_seed=ENV-T04-CTRL-random-reshuffle-unsupported`、脚本 `end_turn ×3`、`expected_terminal=running`、`rng_profile=unsupported_rng_profile`（本包未改）。base 侧 `base_source=controlled-slice-v1`，`build_id=controlled-slice-v1:headbutt=False`，`session_identity=dc8c205957ba1e0a|seed=49403ba12e7112c0|random=mega-random/xoshiro256ss-splitmix64@1|draw=vanilla-uniform@1|participants=player`。参考侧独立执行来源＝`reference/sts2-sim` 自己的 `RunState`/`CombatState`/`CombatEngine`/`CardPileCmd` 在工具进程内独立跑（不复制 base 结果），具名流种子经 `RunRngSet.MockRng` 逐流装载；`reference_head=2cdaea3b6dbc20cfa340662fa73640f20bb6fb00`（`reference/sts2-sim/.git/refs/heads/main`），`profile_id=source-compat-sts2-frozen@1`，流映射 `Shuffle → base-spire:combat-draw`。

**重洗前两侧逐实例来源与槽位顺序（boundary 0–2 完全一致）**

| 边界 | 两侧 hand（槽位） | 两侧 draw（槽位） | discard |
| --- | --- | --- | --- |
| 0 start | （空） | start:00…start:09 | （空） |
| 1 end_turn | 00,01,02,03,04 | 05,06,07,08,09 | （空） |
| 2 end_turn | 05,06,07,08,09 | （空，size 0） | 00,01,02,03,04 |

第 3 次 `end_turn`：手牌 05–09 进弃牌堆 → 弃牌堆顺序 `start:00…start:09`（5×Strike、4×Defend、1×Bash）→ 抽牌堆为空触发重洗。两侧在重洗**之前**的全部事实（HP/格挡/能量/能力层数/牌区顺序）逐项一致，首差只出现在重洗之后的牌序。

**派生与样本（同一 `Shuffle` 流）**：`run_seed(hash32)=764289779`、`hash32('shuffle')=2308280675`、`Shuffle 流种子=3072570454`（与参考身份 `source_run_seed=764289779`、`streams[Shuffle].seed=3072570454` 一致）。上下界两侧均为 `10,9,8,7,6,5,4,3,2`（9 次）。

| 侧 / 状态 | 流 Before 指纹 | 样本（NextInt 取值，按上界顺序） | 流 After 指纹 | 重洗后 hand / draw（槽位） |
| --- | --- | --- | --- | --- |
| base（游标 0→9，对齐基线） | `0x6126f8118c0af92f` | `7,3,3,0,5,0,0,2,1` | `0x268d319817e8d9b3` | hand `09,01,02,04,06`／draw `05,00,08,03,07` |
| reference **未对齐**（游标 9→18） | `0x268d319817e8d9b3` | `6,0,6,2,0,0,1,1,0` | `0xdc2712a5020077bd` | hand `02,08,07,05,00`／draw `03,06,04,09,01` |
| reference **本包对齐后**（游标 0→9） | `0x6126f8118c0af92f` | `7,3,3,0,5,0,0,2,1` | `0x268d319817e8d9b3` | hand `04,05,06,08,01`／draw `00,09,03,07,02` |

指纹由独立模型按引擎同一公式算出，并与记录逐值相符：base 记录 `base-spire:combat-draw 0→9 fp 0x268d319817e8d9b3`；未对齐参考记录 `observed:3 9→18 fp 0xdc2712a5020077bd`；对齐后参考记录新增 `observed:4 0→9 fp 0x268d319817e8d9b3`（与 base 前缀相同）。这就是「工具侧偏移被消除」的直接证据，且对齐动作写在参考记录 `notes` 里。

**调用栈（两侧，只读源码定位）**

- base：`CardDrawProcedures.One → Reshuffle`（[CardDrawProcedures.cs:33-37](../../../../tools/base-migration-baseline/BaseSpire/Cards/Runtime/CardDrawProcedures.cs#L33-L37)：弃牌逐张移入抽牌堆 → `scope.ShuffleCollection(piles.Draw, SliceManifest.DrawStream)`）→ core [`StateStore.ShuffleCollection`:455-470](../../../../core-the-spire/Core.TheSpire/Engine/StateStore.cs#L455-L470) → `RandomService.Shuffle(stream, members)`。
- reference：`CardPileCmd.Draw → ShuffleIfNecessary`（[CardPileCmd.cs:365-372](../../../../reference/sts2-sim/src/Sts2Sim.Core/Commands/CardPileCmd.cs#L365-L372)）→ `Shuffle`（[375-407](../../../../reference/sts2-sim/src/Sts2Sim.Core/Commands/CardPileCmd.cs#L375-L407)：`combined = discard + drawPile` → `combined.StableShuffle(shuffleRng)`）→ [`ListExtensions.StableShuffle`:16-27](../../../../reference/sts2-sim/src/Sts2Sim.Core/Helpers/ListExtensions.cs#L16-L27)（先 `Sort(Comparer<T>.Default)` 再不稳定洗牌）→ `Rng.Shuffle`。

### 首差与裁定

- 未对齐（baseline）：`boundary[3].pile.draw.initial-slots` base `start:05,00,08,03,07` ≠ reference `start:03,06,04,09,01`（同边界 `hand` 互补）。**其中混了工具侧游标偏移。**
- 对齐后（本包）：`boundary[3].pile.draw.initial-slots` base `start:05,00,08,03,07` ≠ reference `start:00,09,03,07,02`（`draw.order`/`sorted`、`hand.*` 同边界共 6 条）。**残余首差是玩法侧的洗牌输入顺序不同。**
- 源裁定：源 `CardPileCmd.Shuffle` 取 `discard + drawPile` 后调用 `list.StableShuffle(player.RunState.Rng.Shuffle)`（[sts2 CardPileCmd.cs:864-876](../../../../sts2/src/Core/Commands/CardPileCmd.cs#L864-L876)），而 `StableShuffle` **先排序再洗牌**（[sts2 ListExtensions.cs:22-31](../../../../sts2/src/Core/Extensions/ListExtensions.cs#L22-L31)），排序键是 `CardModel.CompareTo`＝`ModelId`（先 `Category` 后 `Entry`，序数比较，[ModelId.cs:41-49](../../../../reference/sts2-sim/src/Sts2Sim.Core/Models/ModelId.cs#L41-L49)）再按升级等级。参考侧与源一致（`combined.StableShuffle`）；**base 侧缺这一步排序**。
- 独立复算（`verify-reshuffle-model.py`）：base 输入顺序＝弃牌堆顺序 `start:00…09`，参考输入顺序＝按 Id 排序后的 `start:09,05,06,07,08,00,01,02,03,04`（Bash、4×Defend、5×Strike，同级保持原相对次序）。游标对齐到 0 后，两侧仍不相等，说明残余首差确实来自这条输入顺序差，而不是随机数或工具接线。

### 精确 base 缺口（交负责人派包，本会话未改 base）

- 缺口方法：`mod-the-spire/base-spire/BaseSpire/Cards/Runtime/CardDrawProcedures.cs:33-37` 的 `Reshuffle`（core `StateStore.ShuffleCollection` 跟随其语义）没有源 `StableShuffle` 的预排序。
- 输入：抽牌堆为空、弃牌堆 `[start:00…start:09]`（5×Strike、4×Defend、1×Bash）；`Shuffle` 流种子 `3072570454`，游标 0。
- 预期（源语义）：先按内容 Id（源 `ModelId` 定序）再按升级等级排序，再 descending Fisher–Yates，得 `04,05,06,08,01,00,09,03,07,02` → hand `04,05,06,08,01`、draw `00,09,03,07,02`（＝参考侧对齐后实测值）。
- 实际（base）：不排序直接洗牌，得 `09,01,02,04,06,05,00,08,03,07` → hand `09,01,02,04,06`、draw `05,00,08,03,07`。
- 可实施性：core 已有稳定排序查询可用（[PackageBuilder.Sort:435-439](../../../../core-the-spire/Core.TheSpire/Engine/PackageBuilder.cs#L435-L439) → [SortQuery 求值用 `OrderBy`:66-80](../../../../core-the-spire/Core.TheSpire/Engine/Session.Queries.cs#L66-L80)，LINQ `OrderBy` 稳定）；实现时须注意两点：(1) 排序键要复刻源 `ModelId` 的 `Category`+`Entry` 序数序，**不能**直接用 base 内容 id 的字典序（本 seed 三张牌两种序恰好同序，一般牌集不保证）；(2) 源 `List<T>.Sort` 在 16 张以上走内省排序、同级（同 Id 同升级）次序不保证与稳定排序一致，本 case 只有 10 张（插入排序区间）故一致，更大牌堆需另核或按源实现复刻。
- 另一个**本 case 未触发**的源差异（未验证）：源 `Shuffle` 把**现有抽牌堆余牌**并入同一次重洗，base 的 `Reshuffle` 只在抽牌堆为空时触发（此时等价）；牌触发的直接 `Shuffle`（抽牌堆非空，如 `Reboot` 类）两侧尚无比较。

### 原 seed 最小可重放命令

```powershell
# 仓库根目录 F:\Scylier-Project\active\auto-the-spire
# 1) 固定闭包 baseline（本包第一步，工具侧未对齐）
dotnet .artifacts/parallel/package-decoupling/bin/Differential/debug/Differential.dll `
  test --suite regression --only random-reshuffle-unsupported `
  --output .audit/dsh-reshuffle-followup-20261011/baseline-directed
# 2) 本包对齐后的工具构建
dotnet build .audit/dsh-reshuffle-followup-20261011/verify/DifferentialVerify.csproj
dotnet .artifacts/parallel/dsh-reshuffle-followup-20261011/bin/Debug/net9.0/Differential.dll `
  test --suite regression --only random-reshuffle-unsupported `
  --output .audit/dsh-reshuffle-followup-20261011/aligned-directed
```

（baseline 的完整 17 分母报告在 `$audit/baseline`；上面第 1 条只重放同 seed 的单 case。）

## 正常 Run 与受控 case 分列

- **正常 Run**：`rng-initial-shuffle`（`vanilla-compatible@1`，正式工厂 `RunConfiguration.Ironclad(0, dev)` 正常开局 vs 参考运行层入口）本轮 **passed**，复用 T08 已交真实初始化与队列分离，未重做、未改初始流；`map-generated-alpha/beta`（`dev-map` 正常开局 vs 独立地图入口）本轮 passed。
- **受控机制 case**：`enemy-death-aftermath`、`power-turn-end-decay`、`vulnerable-decay-via-bash`、`headbutt-mid-choice` 等本轮 passed；`random-reshuffle-unsupported` 保持 `unsupported_rng_profile` 非零（Profile 声明未改，未把单侧检查或对齐当作双侧 passed）。受控 case 只作机制对拍，不当正常开局胜负证据。

## 未验证与缺口

- 本包**未**修 base，也未验证修复后的行为；上面「预期」是源语义 + 独立模型推算，不是已实测的 base 结果。
- 只对 `Shuffle` 一条流做了受控对齐。参考受控路径其余消费（本 case 实测仅 `Shuffle`；开局钱袋/地图流与脚本无关）未逐流对齐；若将来受控 case 触及别的流，需要同样的显式对齐。
- 参考侧逐边界 `streams` 投影仍缺（最终报告里 `missing_dimensions=['streams(reference)']`，与 baseline 相同）。当前 Profile 下该维度门槛不触发；一旦要把 case 升级为可计通过，必须先补这一项比较信息。
- 未验证 >16 张牌的源排序/重洗一致性和卡牌触发的非空抽牌堆重洗（见 base 缺口末条）。
- 重洗 case 仍 rc 非零，ENV-T04/M01 不因本包关闭；未跑 solution/Engine/Slice/net/训练/归档，未做正式公开复放。

## 依赖与可继续事项

- 可继续（需负责人派单）：base 机制包实现 `StableShuffle` 预排序（含排序键与 >16 张的源排序保真），完成后用本包验证工程跑定向＋完整 17 一次；预期 `boundary[3]` 该首差消失，case 仍按 Profile 声明判 unsupported，是否升级口径由负责人决定（升级须同时补参考侧 `streams` 投影）。
- 依赖：ENV-T04 父项、ENV-T08（已交，本包复用）；本包不启动下一包，也不重启旧会话/AI。
- 可复用：固定闭包、本包验证工程与三个对比脚本、`RestoreStream` 对齐、独立随机模型。受控 init 与其自检未改，可继续复用。

## 维护位置与自查

- 任务状态/认领/下一成果只回写[主清单 ENV-T04](../../tofix/README.md#env-t04)（本轮由负责人 Codex 单写）；方法写 [tools/differential/README.md](../../../../tools/base-migration-baseline/differential/README.md#重洗比较2026-10-11)；本次证据与长日志在 `.audit/dsh-reshuffle-followup-20261011/`，构建在 `.artifacts/parallel/dsh-reshuffle-followup-20261011/`。
- 预期读者能定位：目标与有效状态（主清单 T04）、可复用成果（「已交付」）、第一步（「接手后先做什么」2）、完成条件与验收者（主清单完成条件＋负责人 Codex）、阻塞/未验证（「未验证与缺口」）、维护位置（本节）。
- 以上属作者自查，不是独立读者验证；`ready_for_review` 表示请负责人复核，不等于验收。本会话未提交、未推送、未打 tag、未改其他包构建根。
