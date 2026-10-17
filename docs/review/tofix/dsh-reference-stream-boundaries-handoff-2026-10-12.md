# ENV-T04 参考侧逐边界随机流交接 — 2026-10-12

- task_id: ENV-T04-reference-stream-boundaries-20261012
- dispatch_id: ENV-T04-reference-stream-boundaries-20261012（= 派发记录 `task_id`）
- session_id: session-df65ab58-df64-4989-b62c-fa24eaf92df6
- request_id: 604289cf-8214-49f6-bd58-6c6121bd498c
- dispatch_record: [派发记录](../../../.audit/dsh-reference-stream-boundaries-20261012/task-record.json)（[派单原文](../../../.audit/dsh-reference-stream-boundaries-20261012/task.txt)、[dispatch-meta](../../../.audit/dsh-reference-stream-boundaries-20261012/dispatch-meta.json)）
- delivery_status: ready_for_review
- updated_at: 2026-10-12T08:18:00+08:00

本包补齐**参考侧受控机制逐稳定边界的真实随机流快照**，让真实重洗第一次可以按**完整维度**独立比较。
`random-reshuffle-unsupported` 的参考记录不再是 `missing_dimensions=[streams(reference)]`：边界 3 两侧现在有
同键、同单位的 `base-spire:combat-draw`（`draws=9`、指纹 `0x268d319817e8d9b3`），而**首个差异仍是**
`boundary[3].pile.draw.initial-slots`（base 缺源 `StableShuffle` 预排序，A 主责未交）。
本包**不改**该 case 的 `unsupported_rng_profile` 声明、不改 17 分母/seed/预期终点/维度含义、不关闭父项 ENV-T04，
也没有动 base/core/net/reference 生产。ready_for_review **不是验收**。

当前认领、状态与依赖只维护在[主清单 ENV-T04](../../review/tofix/README.md#env-t04)；验收者为 Codex 负责人。

## 接手后先做什么

1. 先读[主清单 ENV-T04](../../review/tofix/README.md#env-t04) 与共享 `git status --short`，不要重做已交的商店/语义重放/客户端包，
   也不要恢复旧会话。
2. 想直接看结论：读[完整 17 产物](../../../.audit/dsh-reference-stream-boundaries-20261012/full-regression/regression.json)与
   [参考记录 `random-reshuffle-unsupported`](../../../.audit/dsh-reference-stream-boundaries-20261012/full-regression/records/reference/random-reshuffle-unsupported.json)
   的 `boundaries[*].streams` 与 `reference_streams`。想复放：用下面的构建/运行命令（固定闭包，只读）。
3. 等 A 的 base 排序包落地后，由 Codex 在新的**共同闭包**上重放本包命令：预期 `random-reshuffle-unsupported`
   的 `pile.draw.initial-slots` / `pile.draw.order` 首个差异消失；届时再裁定是否把该 case 升级到现有源兼容 Profile
   （`vanilla-compatible@1`）。**本包不擅自改 Profile 或 unsupported 状态。**
4. 若还要继续本线：优先核对 `RngSourceCompat.UnverifiedKeyNames` 里那 6 条合成键（base 侧消费点/引擎常量），
   它是"下一次消费这两条流时才会显形"的同类隐患；不需要重录冒烟。

## 本包实际改了什么（全部在 `tools/differential/`）

| 文件 | 变化 |
| --- | --- |
| [ReferenceRandomProbe.cs](../../../tools/differential/ReferenceRandomProbe.cs) | 重写为**逐边界**流观察器：安装时保存并最终恢复既有 `RngDiagnostics.DrawObserver`；`Attach` 登记参考自己的 run/player 具名流并把基线对齐到当前计数器；`AtBoundary` 返回与 base 同形状的 `StreamSnapshot`（增量视图）；`Cumulative` 供初始化/Restore 前后/`script_random` 用（全量视图）；`Restore` 内部在重新装种子后**重挂基线**（`MockRng` 换对象）并留下 `restore-before/after` 真实读数 |
| [ReferenceBackend.cs](../../../tools/differential/ReferenceBackend.cs) | `RunControlled`/`Build`/`Add` 串起 probe；`BoundaryRecord.Streams` 不再传空表；新增 `CaseRecord.ReferenceStreams` 证据；`Restore("Shuffle", …)` 经 probe 包装（对齐行为本身未变） |
| [Records.cs](../../../tools/differential/Records.cs) | 新增 `ReferenceStreamSnapshot` / `ReferenceStreamEvidence` / `ReferenceStreamBoundary` 与 `CaseRecord.ReferenceStreams`；新增 `Records.ObservedStreamPrefix` 与 `Records.IsNamedStream`（**具名流 vs 动态槽位**的唯一判据） |
| [RngSourceCompat.cs](../../../tools/differential/RngSourceCompat.cs) | 修 `EngineStreamName`：`CombatTargets`/`CombatCardSelection`/`CombatCardGeneration` 改为直接引用 base `SliceManifest` 常量（原自写键与 base 实际键不同）；新增 `DiagnosticStreamName`/`PlayerStreamNames`；登记 `UnverifiedKeyNames`（其余 6 条合成的 run 键待核对） |
| [Comparator.cs](../../../tools/differential/Comparator.cs) | `vanilla-compatible@1` 下只比**具名流**（动态槽位没有稳定跨后端键，条目集合不同不是规则差异） |
| [TestCommand.cs](../../../tools/differential/TestCommand.cs) | `streams` 维度门槛收紧为"某个边界存在 `Draws > 0` 的具名流"；新增流证据自洽结算（声明消费却无具名流边界读数，或脚本期间**实际增长**的具名流在逐边界覆盖不到 → `missing_dimension`），不补 0、不放宽 |
| [README.md](../../../tools/differential/README.md) | 新增[参考侧逐边界随机流（2026-10-12）](../../../tools/differential/README.md#参考侧逐边界随机流2026-10-12)，写明两类身份、单位、逐边界增量语义、Restore 换对象与门槛 |

### 接口/语义变化（供消费方核对）

- **记录形状**：参考记录新增 `reference_streams`（`unit`/`observation`/`initialization`/`restore_before`/`restore_after`/`boundaries`），
  `boundaries[i].streams` 由 `[]` 变为逐边界真实读数。动态 RNG 的流名形如 `observed:<槽位>:<原始诊断名>`。
- **语义**：逐边界读数是**增量**（该区间内计数器有推进）；`script_random.before/after` 是**累计**（沿用旧语义，只把临时名换成显式映射名）。
- **命名**：参考侧具名流名 = base 引擎流名（`RngSourceCompat.EngineStreamName`）；`Shuffle → base-spire:combat-draw`
  在本次修改前后不变（未回归）。
- **无 API 破坏**：`Comparator`/`TestCommand` 的公开签名与 suite 字段未变；没有新增 suite case、没有改 `suites/regression.json`。

## 源定位与依据

- **逐边界读数单位**：参考 `Rng.Counter`（`RngDiagnostics.DrawObserver` 在每次推进 Counter 的原生调用后回调），
  见 `reference/sts2-sim/src/Sts2Sim.Core/Random/Rng.cs` 与 `RngDiagnostics.cs`（**只读**，未修改）。
- **Restore 换对象**：`reference/sts2-sim/src/Sts2Sim.Core/Runs/RunRngSet.cs` 的 `MockRng`
  （`_rngs[rngType] = new Rng(seed)`），因此对齐后必须重挂基线；本包在调试中实测到"不重挂则新对象在每个边界重复出现"。
- **显式映射的 base 真键**：`mod-the-spire/base-spire/BaseSpire/Slice/SliceManifest.cs`
  （`DrawStream` / `RandomTargetStream` / `RandomCardStream` / `CardGenerationStream`）与
  `.../Slice/VanillaRandomLayout.cs:EngineStreamName`（编译期引用，未修改 base）。
- **参考身份**：`reference/sts2-sim` HEAD 与 `source-compat-sts2-frozen@1` 的派生替换范围仍按原登记；
  本包只改了 `RngSourceCompat.cs`，因此 `reference_identity.profile_code_sha256` 随之改变（这是预期差异，不是行为差异）。

## 构建与身份

- 固定输入闭包（**只读**，14 个文件逐个 SHA 见
  [fixed-closure-sha256.txt](../../../.audit/dsh-reference-stream-boundaries-20261012/fixed-closure-sha256.txt)）：
  `.artifacts/parallel/owner-project-review-20261012/bin/Differential/debug`
  - `Core.TheSpire.dll` `sha256:3271cb3dd64c9837a7a311b11bc05b84c483f0b2b62f6db2c6019a1369c34c5c`
  - `BaseSpire.dll` `sha256:27452612b5fe123995568aadeb3752ea03993e724f460f0c6b3c6caa0899f24d`
  - `Sts2Sim.Core.dll` `sha256:5e99c502c689ccb228e5d92a2dac257d898daf6419c61109618edc32851d30a7`
- 本包临时工程：[DifferentialVerify.csproj](../../../.audit/dsh-reference-stream-boundaries-20261012/verify/DifferentialVerify.csproj)
  （编译当前 `tools/differential/*.cs`，Reference 指向固定闭包，保留 `REFERENCE_SIM`；输出到
  `.artifacts/parallel/dsh-reference-stream-boundaries-20261012`）。**没有**改任何生产 csproj、没有并发构建 solution、没有写回闭包。
- 本包构建产物：`.artifacts/parallel/dsh-reference-stream-boundaries-20261012/bin/Debug/net9.0/Differential.dll`
  `sha256:c0a31c41881658301cd496d64a10e1b68a9897747409de25d27dac85c750d021`（`dotnet 9.0.10`，构建 0 警告 0 错误）。
- 构建身份/Scope：`base-spire-singleplayer-dev-v1`、Ironclad、ascension 0；受控 case 用受控切片包 + 与唯一工厂相同的
  Profile/算法/映射资产；`random-reshuffle-unsupported` 的 seed 仍是 `ENV-T04-CTRL-random-reshuffle-unsupported`。
- 源参考：`source-compat-sts2-frozen@1`（只替换具名流种子派生，未改参考抽样/算法/模型）。

## 执行记录（命令 / 原 rc / 执行数）

```powershell
# 构建（临时工程，输出到本包 .artifacts）
dotnet build .audit/dsh-reference-stream-boundaries-20261012/verify/DifferentialVerify.csproj
# rc=0
```

| # | 命令（`$dll` = 上面的 Differential.dll） | 原 rc | 执行数 | 结果 |
| --- | --- | --- | --- | --- |
| 1 | `dotnet $dll test --suite regression --only random-reshuffle-unsupported --output .audit/dsh-reference-stream-boundaries-20261012/targeted-reshuffle` | **1** | 选中 1 / 执行 1 | `unsupported_rng_profile`，`complete=false`，`missing_dimensions=[]`，首个差异 `boundary[3].pile.draw.initial-slots` |
| 2 | `dotnet $dll test --suite regression --output .audit/dsh-reference-stream-boundaries-20261012/full-regression` | **1** | expected 17 / executed 17 | `passed 15` / `self_checked 1`（factory-wiring）/ `unsupported_rng_profile 1`；自检 22 项 0 失败；reference 执行 16 |
| 3 | `dotnet $dll test --suite regression --only random-reshuffle-unsupported --records .audit/dsh-reference-stream-boundaries-20261012/negative-missing-streams/records --output .audit/dsh-reference-stream-boundaries-20261012/negative-missing-streams` | **1** | 选中 1 / 执行 1 | 负例：`missing_dimensions=[streams(reference)]`（清空逐边界流读数后仍真报缺维度） |

产物：[targeted-reshuffle](../../../.audit/dsh-reference-stream-boundaries-20261012/targeted-reshuffle/regression.json)、
[full-regression](../../../.audit/dsh-reference-stream-boundaries-20261012/full-regression/regression.json)、
[negative-missing-streams](../../../.audit/dsh-reference-stream-boundaries-20261012/negative-missing-streams/regression.json)
与对应 `*.log`（原样重定向输出）。

## 逐边界流证据与剩余差异

`random-reshuffle-unsupported`（4 个稳定边界，两侧边界数一致）：

| 边界 | base 具名流 | reference 具名流 | reference 动态槽位（证据，不比较） |
| --- | --- | --- | --- |
| `b0 start` | （无） | （无） | `observed:0:named.act_1_map`(267)、`observed:1..3:unnamed`(230/2/9) |
| `b1/b2 end_turn` | （无） | （无） | 同上（本区间无新增推进，故不重抄） |
| `b3 end_turn`（触发重洗） | `base-spire:combat-draw` draws=9 fp=`0x268d319817e8d9b3` | 同键 draws=9 fp=`0x268d319817e8d9b3` | 新增 `observed:4:unnamed`(9，同指纹) |

- **可比单位已确认**：具名流的 `draws` 是同一生成器自播种以来的原生调用数，两侧同键可比；动态槽位单位相同但**没有跨后端键**，只作证据。
- **初始化与 Restore 分列**（参考记录 `reference_streams`）：`restore_before` 里 `Shuffle=9`、`niche=2`；
  `restore_after` 里 `Shuffle=0`（工具侧归零，未变）；`initialization` 是建局完成时的真实读数。
  **没有**把任何一个累计末态填到所有边界。
- **首个差异未变**：`boundary[3].pile.draw.initial-slots`（base `start:05,00,08,03,07` ≠ reference `start:00,09,03,07,02`），
  来自 A 主责的 base 缺 `StableShuffle` 预排序；6 条差异全部是牌区顺序类，`streams` 不再产生差异。
- base 记录逐字节与负责人基线一致（16 个 base 记录 SHA256 全部 IDENTICAL）；参考记录差异集中在
  `boundaries[*].streams`、`reference_streams`、`script_random` 具名化与 `profile_code_sha256`。

## 未验证 / 依赖 / 可继续事项

- **未验证**：A 的 base 排序包未交，所以**真实重洗的双侧一致仍未证明**；本包只把参考侧维度补全并证明首个差异仍在牌区顺序。
  本包不声称"重洗已接通"、不把该 case 升级 Profile、不关闭父项。
- **未验证**：`RngSourceCompat.UnverifiedKeyNames` 的 6 条合成键（`UnknownMapPoint`/`CombatPotionGeneration`/
  `CombatEnergyCosts`/`MonsterAi`/`CombatOrbs`/`TreasureRoomRelics`）尚未与 base 实际消费点/引擎常量核对；
  今天没有 case 消费它们，属预防性登记。
- **未验证**：动态/临时 RNG 的槽位序在跨进程重放中是否稳定（本包只要求同一进程内可比，不据此做跨构建身份）。
- **依赖**：A 的 base `StableShuffle` 预排序（新 A 主责）；之后由 Codex 在新共同闭包上复放并裁定 Profile 升级。
- **可继续**：核对上面 6 条键；需要时把逐边界流证据接到新的受控 case（本包未新增 case，也没有放宽任何门槛）。
- **未做**：没有重录冒烟/初始化诊断（按要求），没有做 token 统计，没有提交/推送/打 tag，没有改 core/base/net/reference/AI 生产。
