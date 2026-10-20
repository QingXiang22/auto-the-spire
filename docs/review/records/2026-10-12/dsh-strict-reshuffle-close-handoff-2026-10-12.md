# ENV-T04 当前共同闭包重洗严格结算交接 — 2026-10-12

> 2026-10-19 整理：切换前历史证据：旧 C# 路径和会话已经退出当前生产；保留当时交付与未验收范围，不按本文继续派单。 当前任务见[主清单](../../tofix/README.md)，后续架构见[内容与行为重构目标](../../../design/mod-the-spire/content-behavior-refactor-goals.md)。


本包把 `random-reshuffle-unsupported` 在**原 ID/seed/动作/终点/维度不变**的前提下原地升级为现有
`vanilla-compatible@1`，并在负责人**固定共同闭包**上完成一次完整原 17 严格执行：**16 双侧 `passed` + 1
`self_checked`、strict rc0、比较器自检 0 失败**。仍未完成：**正常 Run / 奖励 / 四幕双侧没有执行**，
其余 6 条未核对流键与参考侧动态槽位跨进程稳定性仍未验证；本次**不关闭** ENV-T04/M01 或整个 Profile。

- task_id: `ENV-T04-strict-reshuffle-close-20261012`（主清单 ENV-T04 下 2026-10-12 横向扩展后新包认领行）
- dispatch_id: `ENV-T04-strict-reshuffle-close-20261012`（= 派发记录 `task_id`）
- session_id: `session-0eef0705-b034-4a87-8bfb-70880010f1a7`
- request_id: `a25173be-5d7c-4c64-91ab-906fa90fbd56`
- dispatch_record: [派发记录](../../../../.audit/dsh-strict-reshuffle-close-20261012/task-record.json)（[派单原文](../../../../.audit/dsh-strict-reshuffle-close-20261012/task.txt)）
- delivery_status: `ready_for_review`（**不是验收**）
- updated_at: 2026-10-12T10:05:00+08:00

归属与接手入口：归属[主清单 ENV-T04](../../tofix/README.md#env-t04)（主清单由负责人独自修改，本会话只写本交接与自有
证据）。本包可写目录只有 `tools/differential/`（本 case 的 Profile 声明、必要结算文案、README）与
[.audit/dsh-strict-reshuffle-close-20261012](../../../../.audit/dsh-strict-reshuffle-close-20261012)；输出在
`.artifacts/parallel/dsh-strict-reshuffle-close-20261012`。没有改 core / base / net / `reference` / AI，
没有构建任何生产项目，**比较器没有改**（没有发现实际漏洞）。

## 接手后先做什么

1. 先核对[主清单 ENV-T04](../../tofix/README.md#env-t04) 与共享 `git status --short`。本包**已交付**：
   该 case 的 Profile 原地升级、一次完整 17 严格 rc0、两个完整 suite 反例、一条 base 记录 note 文案修正、
   README 的对应章节。**不要**重做已交的排序（ENV-T06）与参考侧逐边界流包
   （[参考流交接](dsh-reference-stream-boundaries-handoff-2026-10-12.md)），也不重做商店前缀录制。
2. 想直接看结论：读[完整 17 产物](../../../../.artifacts/parallel/dsh-strict-reshuffle-close-20261012/full-regression/regression.json)
   与其中 `random-reshuffle-unsupported` 一行（`verdict=passed`、4 边界、`missing_dimensions=[]`），
   再看[两个反例](#两个完整-suite-反例)的 `regression.json`。
3. 想复放：用本包临时工程构建后跑「执行记录」表里的三条命令（前置条件＝
   `.artifacts/parallel/owner-horizontal-extension-20261012/bin/Differential/debug` 固定闭包仍在，且
   `tools/differential/suites/regression.json` 的 `suite_sha256` 仍是
   `sha256:cc23a6060e3c3ea01c25408d3c34f6e90d6c79a4bfe0390d9e86d027dd34a415`）。
4. 需要负责人裁决的开放问题：该 case 的**旧 ID 名**（`random-reshuffle-unsupported`）与现在 `passed` 的判定
   已经语义不符。本包按派单要求**保留原 17 ID**（改名会动分母/证据链），是否改名由负责人决定。
5. 派单里点名的两个**不算本包清债**的范围继续开放：正常 Run/奖励/四幕双侧，以及
   `RngSourceCompat.UnverifiedKeyNames` 的 6 条合成键（见「差异、阻塞与未验证」）。

## 本包实际改了什么

| 文件 | 变化 | 是否影响判定 |
| --- | --- | --- |
| [suites/regression.json](../../../../tools/base-migration-baseline/differential/suites/regression.json) | `random-reshuffle-unsupported` 的 `rng_profile`：`unsupported_rng_profile` → `vanilla-compatible@1`；`title`/`provenance`（source 改为真实源定位）/`reference_source` 与 suite 级 `note` 如实改写。ID、`game_seed`、`setup`、3 个 `end_turn`、`expected_terminal`、`contents`/`variants`、四个必需维度（`actions`/`facts`/`pile-order`/`streams`）**全部未动**，分母仍 17 | **是**（这是升级本体） |
| [BaseBackend.cs](../../../../tools/base-migration-baseline/differential/BaseBackend.cs) | 「start 之后消费了新随机数」这条 base 记录 **note 文案**按 Profile 分流：原版兼容 → 「两侧具名流逐边界参与比较」；其余 → 保留「按 `unsupported_rng_profile` 处理，不计通过」。升级前该 note 对本 case 是**错的**（记录会写"不计通过"而严格判定是 `passed`） | **否**（note 不参与比较；实测只有该 case 的 base 记录多/少这一行） |
| [README.md](../../../../tools/base-migration-baseline/differential/README.md) | 新增「重洗严格升级（2026-10-12）」章节（口径核实表、两次完整 17、两个反例）；修正 3 处升级后失效的表述（第 9/25 行与「重洗比较（2026-10-11）」开头），并在参考流包实测处加「已被下一节取代」指针 | 否（文档） |
| [.audit/…/verify/DifferentialVerify.csproj](../../../../.audit/dsh-strict-reshuffle-close-20261012/verify/DifferentialVerify.csproj) | 本包临时验证工程：只编译当前 `tools/differential/*.cs`，依赖全部指向固定闭包 DLL，保留 `REFERENCE_SIM`；输出到本包 `.artifacts`；**不**经 ProjectReference 重编 base，**不**写任何生产 csproj/DLL | 否（工具构建方式） |
| [.audit/…/verify-reshuffle-stream-identity.py](../../../../.audit/dsh-strict-reshuffle-close-20261012/verify-reshuffle-stream-identity.py)、[build-negatives.py](../../../../.audit/dsh-strict-reshuffle-close-20261012/build-negatives.py)、[fixed-closure-sha256.txt](../../../../.audit/dsh-strict-reshuffle-close-20261012/fixed-closure-sha256.txt) 及各 `*.log` | 本包证据（独立核实脚本、反例构造、日志、固定 DLL SHA） | 否（证据） |

`fixtures/negative/suite-init-*.json` 里那三份 `note` 仍是旧文案的副本、`fixtures/fixtures-v1.json` 仍写
`unsupported_rng_profile`（探索性 suite 的历史记录），都**未动**：它们不属于本 case 的 Profile 结算，
改了也不改变任何判定；README 里对应的历史表（`compare --suite fixtures-v1` 的判定表）同样保留为当时证据。

## 源条件核实（映射 / Restore / 单位，不按同 seed 猜流）

升级的前置是派单要求的「确认完整源条件」。下表每条都同时给**只读源定位**与**实际边界读数**，并由
[verify-reshuffle-stream-identity.py](../../../../.audit/dsh-strict-reshuffle-close-20261012/verify-reshuffle-stream-identity.py)
（rc0，含 28 条断言）与**另一份既有独立实现** `tools/rng-oracle` 交叉复算，不是"同 seed 就当成同一条流"。

| 口径 | 只读源依据 | 实际边界核实 |
| --- | --- | --- |
| **Shuffle 映射** | 源 `sts2/src/Core/Commands/CardPileCmd.cs:876` 与参考 `reference/sts2-sim/.../Commands/CardPileCmd.cs:387` 的重洗都取 run 级 `RunState.Rng.Shuffle`；base 把该流具名为 `SliceManifest.DrawStream` = `base-spire:combat-draw`（`Randomness/VanillaRandomLayout.cs:107` 把 `"Shuffle"` 映射到它）；`RngSourceCompat.EngineStreamName("Shuffle")` 直接引用该常量 | 边界 3 的具名流两侧都只有 `base-spire:combat-draw`；参考侧另有 `observed:4:unnamed`（工具侧换对象后的临时槽位）**不并入**具名流 |
| **单位** | base `core-the-spire/.../Engine/RandomService.cs:292` 每次 `Next()` 使 `Draws++`（raw generator word）；参考单位是 `Rng.Counter`（`reference/.../Random/Rng.cs` 的原生调用计数，记录里写 `native-counter-increments`）；10 张牌降序 Fisher–Yates = **9** 次 | 两侧边界 3 都是 `draws=9`、指纹 `0x268d319817e8d9b3`；本包模型与 `tools/rng-oracle` 独立算出的"9 次后的状态指纹"逐值相同 |
| **Restore** | `reference/.../Runs/RunRngSet.cs:265-268` 的 `MockRng` 是**换对象**（`_rngs[type] = new Rng(seed)`，计数器归零）；`ReferenceRandomProbe.Restore` 换完必须**重挂基线**（`Restore` 内调 `Baseline()`），前后读数写进 `reference_streams.restore_before/restore_after` | `restore_before.combat-draw = 9` → `restore_after = 0`；初始化期那条同 seed 的临时流留在 `observed:3`（指纹同 `0x268d…`、`Comparable=false`），按**对象身份**分列，没有被当成同一条流 |
| **实际边界（洗牌输入/输出）** | 源 `CardPileCmd.Shuffle` = `discard + drawPile` → `List.Sort`（`CardModel.CompareTo`：`ModelId.Entry` 序、同级按升级等级）→ 一次 Fisher–Yates | 独立复算得到洗后 10 张 = `04,05,06,08,01,00,09,03,07,02`，与两侧边界 3 的 `pile.hand.initial-slots = start:04,05,06,08,01` / `pile.draw.initial-slots = start:00,09,03,07,02` **逐值一致**；抽牌堆内容序也与 `pile.draw.order` 一致 |

结论：**没有发现源语义首个差异**——不需要保留红态，也没有为了变绿改任何其他 case。

## 构建与身份

- 固定闭包（**只读输入**，全部 DLL SHA 见 [fixed-closure-sha256.txt](../../../../.audit/dsh-strict-reshuffle-close-20261012/fixed-closure-sha256.txt)）：
  `.artifacts/parallel/owner-horizontal-extension-20261012/bin/Differential/debug`（Engine99 / Slice775）
  - `Core.TheSpire.dll` `afee26472d062074b681ef4c59b61f88994aa72a252f4e0a0c81e3baf9d223a2`
  - `BaseSpire.dll` `fdb4d02b7a33cbf985ba9270a35957ebad81d516a2c991bd46623cd86570615c`
  - `Sts2Sim.Core.dll` `e41c347699e616ca6e52243709e5f757d6cf20dc7750b51ecdf870e57515b1ea`
  - `Differential.dll`（负责人闭包内那份）`add9d89a8c16c2db5f543194b1e3d614788255830bb44a171dd57c8ce3effb02`
  - `Antlr4.Runtime.Standard.dll` `0440e3d2c5906326e04df87d6a5272192b7d29099901ba4215ca9e0f5dca7591`、
    `System.IO.Hashing.dll` `fbe8c2fcb4adc01da5c3c8ed0d727903b8fda1e6b84da3c447c9794615f30664`
- 本包临时工程（rc0，**0 错误 / 1 警告**，警告是 `MSB3539`（`BaseIntermediateOutputPath` 设置时机），非行为问题，
  日志 [verify-build.log](../../../../.audit/dsh-strict-reshuffle-close-20261012/verify-build.log)）：
  `dotnet build .audit/dsh-strict-reshuffle-close-20261012/verify/DifferentialVerify.csproj`
  → `.artifacts/parallel/dsh-strict-reshuffle-close-20261012/verify/bin/Debug/net9.0/Differential.dll`
  `a935b69dcaa4f75dfc4797dfe7d959b89c06ba35f01f054d4e87238e68a71bd9`；同目录下拷入的
  `Core.TheSpire/BaseSpire/Sts2Sim.Core/Antlr/System.IO.Hashing` 五个 DLL 与固定闭包 **SHA 完全相同**。
- suite 摘要：升级前 `sha256:5cca423f8e0d298f8229d5b5ad14cbe0fb104515645c796db4a858d31af223a2`
  （负责人基线）→ 升级后 `sha256:cc23a6060e3c3ea01c25408d3c34f6e90d6c79a4bfe0390d9e86d027dd34a415`（本包四次运行都相同）。
- 构建身份/Scope：`base-spire-singleplayer-dev-v1`、Ironclad、ascension 0、`sts2-frozen-2026-10-05`；
  该 case 仍是 `controlled-slice-v1` 受控切片 + 与唯一工厂相同的公开随机资产，`game_seed` 仍是
  `ENV-T04-CTRL-random-reshuffle-unsupported`（未换 seed）；参考侧仍只替换已裁定的具名流种子派生
  （`source-compat-sts2-frozen@1`）。
- **没有身份迁移**需要做：dev `4a08d5bbaf4270be` / dev-map `9d9f43c0d1b58809` 已是 ENV-T06 源预排序后的新值，
  本包未改任何内容/规则身份；`tools/differential` 里没有硬编码旧身份基线。

## 执行记录（命令 / 原 rc / 实际执行数）

工作目录均为仓库根。`$fixed` = 上面固定闭包的 `Differential.dll`；`$this` = 本包临时工程的 `Differential.dll`。
报告 `regression.json` 的 `command` 字段是工具自写的**规范形式**（`dotnet run --project tools/differential …`），
下表是**实际执行**的命令。

| # | 实际命令 | 原 rc | 实际执行数 | 结果 |
| --- | --- | --- | --- | --- |
| 0 | `dotnet $fixed test --suite regression --output .audit/dsh-strict-reshuffle-close-20261012/baseline-recompute` | **1** | expected 17 / executed 17 | 升级**前**复算：`passed 15 / self_checked 1 / unsupported_rng_profile 1`，自检 22 项 0 失败；**17 个 base + 16 个 reference 记录与负责人基线逐字节相同**（证明固定闭包与负责人那次一致） |
| 1 | `dotnet $fixed test --suite regression --output .artifacts/parallel/dsh-strict-reshuffle-close-20261012/full-regression-fixed-dll` | **0** | expected 17 / selected 17 / executed 17 | `passed 16 / self_checked 1 / 不计通过 0`，自检 22 项 0 失败，`complete=true`；`random-reshuffle-unsupported` = `passed`（4 边界、`divergences=[]`、`missing_dimensions=[]`）。**只改 suite 声明、用固定 DLL** 就能到 rc0 |
| 2 | `dotnet build .audit/dsh-strict-reshuffle-close-20261012/verify/DifferentialVerify.csproj` | **0** | — | 0 错误 / 1 警告（MSB3539） |
| 3 | `dotnet $this test --suite regression --output .artifacts/parallel/dsh-strict-reshuffle-close-20261012/full-regression` | **0** | expected 17 / executed 17 | 与 #1 同判定同计数（`run_id=…+20261012T015439Z`）；与 #1 的记录差异**只有** `random-reshuffle-unsupported` 的 base 记录多/少一条 note 文案（对比见下方），其余 16 base + 16 reference 逐字节相同 |
| 4 | 两个反例（构造见 [build-negatives.py](../../../../.audit/dsh-strict-reshuffle-close-20261012/build-negatives.py)，rc0） | **0** | — | 生成 `negative-pile-order` / `negative-missing-streams` 两套外部参考记录 |
| 5 | `dotnet $this test --suite regression --records .audit/dsh-strict-reshuffle-close-20261012/negative-pile-order/records --output .artifacts/parallel/dsh-strict-reshuffle-close-20261012/negative-pile-order` | **1** | expected 17 / executed 17 | 见下节 N1 |
| 6 | `dotnet $this test --suite regression --records .audit/dsh-strict-reshuffle-close-20261012/negative-missing-streams/records --output .artifacts/parallel/dsh-strict-reshuffle-close-20261012/negative-missing-streams` | **1** | expected 17 / executed 17 | 见下节 N2 |
| 7 | `python .audit/dsh-strict-reshuffle-close-20261012/verify-reshuffle-stream-identity.py` | **0** | 28 条断言全 OK | 映射/Restore/单位/实际边界全部通过（含 `tools/rng-oracle` 交叉复算） |
| 8 | `python tools/doc-check/check_docs.py` | **4** | 442 篇 | 4 个问题**全部**是既有文件（`dsh-power-fixture-handoff-2026-10-11.md` 2 个、`dsh-startup-alignment-handoff-2026-10-11-f68b943d.md` 2 个）；本包改动/新增文件（含本交接）0 个（[日志](../../../../.audit/dsh-strict-reshuffle-close-20261012/doc-check.txt)） |
| 9 | `git diff --check` | **0** | — | 只有既有共享文件的 LF/CRLF 提示；本包改动的 3 个 `tools/differential` 文件未跟踪，该命令不覆盖它们 |

产物：[升级前复算](../../../../.audit/dsh-strict-reshuffle-close-20261012/baseline-recompute/regression.json)、
[完整 17（固定 DLL）](../../../../.artifacts/parallel/dsh-strict-reshuffle-close-20261012/full-regression-fixed-dll/regression.json)、
[完整 17（本包构建）](../../../../.artifacts/parallel/dsh-strict-reshuffle-close-20261012/full-regression/regression.json)、
两个反例的 `regression.json`，以及各自同目录的 `records/{base,reference}/*.json` 与 `*.log`。

base 记录的**唯一**差异（`notes` 数组，对比 #1 与 #3）：

```
- 本 case 在 start 边界之后消费了新的随机数：按 unsupported_rng_profile 处理，不计通过。
+ 本 case 在 start 边界之后消费了新的随机数：声明为原版兼容 Profile，两侧具名流逐边界参与比较。
```

## 两个完整 suite 反例

两个反例都**复用现有** `--records <外部参考记录目录>` 通路与**同一个** Comparator：没有新增 suite case、
没有改比较器、没有加历史矩阵。两次都是**完整 17**（`complete=true`，不是 `--only`），因此非零不是子集运行的
副产物——若被改的那处没有被真的比较，这两次都会是 rc0。

| 反例 | 只改参考记录的这一处 | 原 rc | 判定 / 首个差异 / 维度 |
| --- | --- | --- | --- |
| **N1 牌序变化** | `random-reshuffle-unsupported` 的 `boundary[3].pile.draw.order` 换一个**纯置换**：前两项对调（多重集、条数、`sorted` 集合都不变），即"牌序变了、构成没变" | **1** | `verdict=diverged`，唯一首个差异 `boundary[3].pile.draw.order`（kind `collection`），最后一致边界 `2:step:1:end_turn`；`passed 15 / self_checked 1 / 不计通过 1`、自检 0 失败 |
| **N2 参考 Shuffle 边界流缺失** | 同一 case 的 `boundaries[*].streams` 清空（参考侧不再有真实具名流读数） | **1** | `verdict=diverged`，首个差异 `boundary[3].streams[base-spire:combat-draw].present`（`存在 ≠ 缺失`），并**如实**列出 `missing_dimensions=["streams(reference)"]`；其余计数同 N1 |

沿用既有自检：完整 17 的 `self_test` 22 项 0 失败里仍包含本 case 的 `identical/`（现在 `verdict=passed`）、
`mutant/collection`（`pile.hand.order` 截断）与 `mutant/stream`（`combat-draw` `draws 10≠9`），即
"牌序/流"两类判别在升级后仍然活着，没有被 Profile 变化绕过。

## 差异、阻塞与未验证

- **首个差异：无。** 完整 17 的 `random-reshuffle-unsupported` 两侧 4 个边界逐条一致（含 `pile.*.initial-slots`
  与具名流 `draws`/指纹），`divergences=[]`。最后一致边界 = 该 case 的最后一个边界。
- **未验证（本包不声称）**：正常 Run、奖励、四幕推进的**双侧比较没有执行**；本 case 仍是**受控单场机制**对拍，
  不构成正常开局胜负或整局证据。真实重洗之外的 6 条合成流键
  （`UnknownMapPoint`/`CombatPotionGeneration`/`CombatEnergyCosts`/`MonsterAi`/`CombatOrbs`/`TreasureRoomRelics`）
  尚未与 base 实际消费点核对（`RngSourceCompat.UnverifiedKeyNames`，**记录即止**）。
- **未验证**：参考侧动态/临时 RNG 的槽位序在**跨进程重放**中是否稳定（本包只要求同一进程内可比）。
- **未验证**：本次没有跑 `dotnet build AutoTheSpire.sln`、Engine、Slice、net 套件（未触及 core/base/net 生产文件）；
  没有做 token 统计；没有提交/推送/打 tag；没有 reset/clean/覆盖任何他人的未提交成果。
- **观察（不属于本包清债，未改）**：`tools/rng-oracle/vanilla_rng_oracle.py` 的 `ENGINE_STREAM_NAMES` 仍把
  `CombatTargets`/`CombatCardSelection` 写成 `base-spire:random-target`/`base-spire:random-card`，与 base 实际
  常量 `base-spire:combat-random-target`/`base-spire:combat-random-card` 不同（`RngSourceCompat.cs` 已在参考流包
  修正）。今天没有 case 消费这两条流，所以不显形；该文件不在本包可写范围，只登记。
- **观察（未改）**：`fixtures/negative/suite-init-*.json` 里 suite `note` 的三份旧文案副本。
- **依赖**：无新增上游依赖。本包用负责人不可变闭包，不依赖 A 正在改的 base。
- **验收者**：负责人（Codex）。`ready_for_review` 不是验收；本包不关闭 ENV-T04/M01、不关闭整个 Profile、
  不改主清单状态。

## 维护位置与自查

- 任务状态/认领/下一成果只回写[主清单 ENV-T04](../../tofix/README.md#env-t04)（本轮由负责人独自修改）；
  工具用法与口径写 [tools/differential/README.md](../../../../tools/base-migration-baseline/differential/README.md)（新增「重洗严格升级（2026-10-12）」）；
  长日志与原始证据在 [.audit/dsh-strict-reshuffle-close-20261012](../../../../.audit/dsh-strict-reshuffle-close-20261012)，
  运行产物在 `.artifacts/parallel/dsh-strict-reshuffle-close-20261012`。
- 接手自查（作者视角，**不是**独立读者验证）：目标与有效状态＝主清单 ENV-T04 行；可复用成果＝固定闭包复算 +
  本包临时工程 + suite 的原地升级；第一步＝「接手后先做什么」第 1/3 条；完成条件＝派单原文「一次完整原 17、
  16+1、rc0、自检 0 失败 + 两个反例」（已满足，验收权在负责人）；阻塞/未验证＝上一节；维护位置＝本节。
- 本会话未提交、未推送、未打 tag，未 reset/clean，未改游戏模型/参考效果/源派生范围，未在文件或日志里写入凭据。
