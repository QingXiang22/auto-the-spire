# ENV-T04-controlled-init-20261011

task_id: ENV-T04-controlled-init-20261011

dispatch_record: `.audit/dsh-controlled-init-2026-10-11/task-record.json`

session_id: session-d1e71cd4-9260-48ae-add0-87454e584774

request_id: cb89f1ea-744a-43fb-aa4c-c3d2d9ca350d

delivery_status: ready_for_review

timezone: Asia/Shanghai

delivered_at: 2026-10-11T01:55:48+08:00

## 目的与入口

本包实现 ENV-T04 的完整受控初始化契约。入口是 `tools/differential` 的 `FixtureSetup.InitialPiles` 与 `controlled-state-no-random@1` 比较门槛；普通未声明 case 保留旧路径。两侧只在显式初始化阶段从各自预登记来源构造牌区，脚本路径没有设牌、补抽或重置随机的操作。

## 修改边界与接口

- `tools/differential/ControlledInit.cs`：新增 `ordered-starting-piles@1`、`InitialCard`、`InitialPiles`、`InitialStateProof`、`RandomMeasurement` 及总集合、槽位唯一性、升级形态、legacy hand/discard 一致性校验。
- `tools/differential/FixtureSuite.cs`、`Records.cs`、`Comparator.cs`：接入声明、双侧记录和 `controlled-state-no-random@1` 门槛；旧 `controlled-no-random@1` 门槛保持。
- `tools/differential/BaseBackend.cs`：从本侧注册牌组独立构造完整有序牌区，保留实例槽位，记录 Session.Random 的初始化与脚本前后读数。
- `tools/differential/ReferenceBackend.cs`、`ReferenceRandomProbe.cs`、`ReferenceRunEntry.cs`：使用原 `game_seed` 和显式 `source-compat-sts2-frozen@1`，参考侧独立构造；scoped `RngDiagnostics.DrawObserver` 读取实际所有原生 Rng 调用并恢复 observer，记录参考身份。
- `tools/differential/suites/regression.json`：只将四个授权原 case 接入完整声明和新 profile；`random-reshuffle-unsupported` 保持 unsupported，17 个 ID 和分母不变。`.audit/.../inputs-after.json` 对照确认其余 13 个 case 语义定义未变。
- `tools/differential/ComparatorSelfTest.cs`：新增重复槽位、缺牌、不完整声明、随机增量、unknown、缺声明及有序槽位变异负例；最终 14/14 通过。
- `tools/differential/README.md`：记录声明版本、测量单位、独立零增量门槛和重洗 unsupported 语义。
- `.audit/dsh-controlled-init-2026-10-11/`：保存构建、单 case、完整 suite、负例、源/参考身份、输入摘要和结果摘要；未覆盖 `task.txt`、`task-record.json`、`inputs-before.json`、`regression-before.json`。

未修改 base/core/net/AI/reference 生产实现、MapDraws 公开语义、主清单或其他任务文件。reference 源仅用于只读身份与方法定位，未写入 reference 工程。

## 实际验证

独立构建命令：

`dotnet build tools/differential --artifacts-path F:/Scylier-Project/active/auto-the-spire/.artifacts/parallel/dsh-controlled-init`

最终返回码为 `0`；产物位于 `.artifacts/parallel/dsh-controlled-init/bin/Differential/debug/Differential.dll`。第一次构建曾因 `tools/differential/BaseBackend.cs(525)` 的 `AsBool` 方法组调用失败返回 `1`，已修正为 `AsBool()`；后续 build-delivery 返回 `0`。

Vulnerable 单 case 命令：

`dotnet .artifacts/parallel/dsh-controlled-init/bin/Differential/debug/Differential.dll test --suite regression --only vulnerable-decay-via-bash --output .audit/dsh-controlled-init-2026-10-11/vulnerable`

返回码 `1`，原因是 `--only` 子集不能满足完整 suite 门槛且 stream 变异目标不足；该 case 本身双侧 `passed`。两侧实际轨迹是无能力 → Bash 后 Vulnerable `2` → `end_turn` 后 `1`，且 power/facts/actions/decisions/牌区顺序均保留。base `script_random` 为 `0→0`，reference 为 `509→509`；reference 的 `509` 是初始化消费，单列为 `initialization_random_count`，不是脚本消费。

完整 17 case 命令：

`dotnet .artifacts/parallel/dsh-controlled-init/bin/Differential/debug/Differential.dll test --suite regression --output .audit/dsh-controlled-init-2026-10-11/full-regression`

返回码 `1`，但执行完整分母：`expected=17, selected=17, executed=17, reference_ran=16, passed=13, self_checked=1, failed=3, self_test_failures=0`。分类为 `13 passed / 1 self_checked / 2 diverged / 1 unsupported_rng_profile`。完整结果与每侧记录在 `.audit/dsh-controlled-init-2026-10-11/full-regression/regression.json` 和 `records/base|reference/`。

- `enemy-death-aftermath`：由原 unsupported 升为 passed；两侧脚本随机均 `0→0`，参考初始化计数 `503`。
- `vulnerable-decay-via-bash`：由原 unsupported 升为 passed；参考初始化计数 `509`，脚本 `509→509`。
- `power-turn-end-decay`：保留 diverged，首差为 `boundary[2].power.enemy.base-spire:calcified-cultist.0.stacks: 2 ≠ 1`，准确归因于原 `setup.powers` 的 Ticks 表达，不修改玩法强行通过。
- `random-reshuffle-unsupported`：保留 unsupported；base 脚本 `0→9`，reference `508→517`，首个证据为 `boundary[3].pile.draw.initial-slots` 不同。没有同源算法、位置和抽样证据，因此没有套用新无随机 profile。
- `rng-initial-shuffle`：保留正式 Run 范围内原首差，未追修生产或参考路径。

负例命令对 `suite-init-duplicate-slot.json`、`suite-init-missing-card.json`、`suite-init-incomplete.json` 均返回 `2`，分别拒绝重复槽位、源集合缺牌和未完整列出四个牌区；对应日志在 audit 目录。`result-summary.json` 汇总了计数、首差、轨迹和双方随机测量。

## 身份与证据

- 工作区 HEAD：`.audit/dsh-controlled-init-2026-10-11/workspace-head.txt`，值 `92ccd346b708b0f552ee074dc7ece446a2908a11`。
- reference HEAD：`.audit/dsh-controlled-init-2026-10-11/reference-head.txt`，值 `2cdaea3b6dbc20cfa340662fa73640f20bb6fb00`。
- 实际源/reference/配置文件 SHA256 与存在性在 `.audit/dsh-controlled-init-2026-10-11/inputs-after.json`；输入前摘要在受保护的 `inputs-before.json`。
- suite 前后对照及 17 分母保全在 `.audit/dsh-controlled-init-2026-10-11/delivery-evidence.py` 的输出与 `inputs-after.json`。
- 参考计数单位是 `native-counter-increments`，base 计数单位是 `Session.Random` generator words；绝对值不跨侧比较，只分别判断脚本增量是否实测为零，unknown 不转为零。

## 负责人接续

请负责人复核 `full-regression/regression.json`、双侧记录、`result-summary.json`、负例 rc 和本包修改边界；将严格 rc=1 按 `13 passed / 1 self_checked / 2 diverged / 1 unsupported` 纳入主清单，保留 power Ticks 首差、random reshuffle unsupported 和正式 Run 首差。确认后由负责人决定是否更新主清单及综合交接。本包未运行训练、AI、插件、reset、cleanup 或 commit，交付后停止。

维护位置：实现与后续证据均在 `tools/differential` 和 `.audit/dsh-controlled-init-2026-10-11/`；固定交接文件为本文件。
