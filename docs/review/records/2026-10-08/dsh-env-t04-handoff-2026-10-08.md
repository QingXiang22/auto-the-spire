---
task_id: ENV-T04
dispatch_id: ENV-T04-strict-regression-resume-20261008
session_id: session-2e21e798-e835-4b05-b899-ac0177e38ada
delivery_status: ready_for_review
updated_at: 2026-10-08T16:04:00+08:00
dispatch_record: .audit/dsh-env-t04-resume-2026-10-08/task-record.json
---

# ENV-T04 严格 regression 接唯一工厂和独立参考交接 — 2026-10-08

本包把 B 线唯一工厂（`BaseSpireRuntimeFactory`）与独立参考适配接到**严格 regression 入口**上：新增 `test --suite regression`、版本化 suite `tools/differential/suites/regression.json` 与薄脚本 `tools/differential/test-env.ps1`；实际双侧执行了原七个可执行 case（`passed` 7）并新增执行 `last-kill-before-terminal`（`passed`，共 8 个双侧通过），保留五个 unsupported 原 ID，并用 13 项有限失败反例证明缺 case/未注册 case/零执行/unsupported/异常/超时/首差/错误终点/缺维度/参考缺失/参考不可用都会让严格入口返回非零。仍未完成的关键范围：`rng-initial-shuffle` 的**双侧**随机比较仍 `blocked_compare`（参考无运行层/字符串 seed 入口，且参考检出领先冻结基线），因此严格 suite 当前是**诚实红态**（rc=1）。本包请求核验；父项 M01 与 ENV-T04 验收仍由负责人决定。

归属：ENV-T04「严格regression接唯一工厂和独立参考」，B 线（`tools/differential/`）。
接手入口：主清单 [ENV-T04](../../tofix/README.md#env-t04)；[B 完整交付合同 §4](../../../design/post-core-agent-assignments.md#4-bdiff-0102日常严格对拍)；[工具 README](../../../../tools/base-migration-baseline/differential/README.md)（新增“严格入口（test）”与 2026-10-08 实测两节）；[实现约束 §9/§12](../../../design/environment-implementation-constraints.md#9-严格对拍suite与断言)。
交付范围：本会话是 B 线唯一生产执行者；只写 `tools/differential/`、本交接与 `.audit/dsh-env-t04-resume-2026-10-08/`、`artifacts/environment/tests/`。core/base/net/AI/参考玩法与根 solution 未改。
派单记录：[.audit/dsh-env-t04-resume-2026-10-08/task-record.json](../../../../.audit/dsh-env-t04-resume-2026-10-08/task-record.json)（`task_id` = 本交接 `dispatch_id`，`session_id` 同上）。
本报告记录 2026-10-08 的交付；当前排期、状态与认领以主清单为准。

## 接手后先做什么

1. 先核对**已交付部分**，避免重做：旧会话 `session-d15c22bb-…` 已交付并在本包复用、**未改写**的部分是
   `BaseBackend`（`formal-dev-factory`：工厂 + dev 范围 `RunConfiguration` → `CreateSession(gameSeed)` → `Start()` 一次；
   `controlled-slice-v1`：受控切片包 + 与工厂相同的公开随机/数值/Compute 配置资产）、`FixtureSuite` 的严格注册校验、
   `Comparator`、`ComparatorSelfTest`、`Records`、`RngCrossCheck`。旧包产物见
   [.audit/dsh-env-t04-implementation-2026-10-07/](../../../../.audit/dsh-env-t04-implementation-2026-10-07)。
2. 一个可执行的下一步（前置：本机没有其他生产会话在构建；本机只有 Windows PowerShell 5.1，没有 `pwsh`）：
   在仓库根目录运行 `powershell -NoProfile -ExecutionPolicy Bypass -File tools\differential\test-env.ps1 -Suite regression`。
   预期：先构建 `tools/differential/Differential.csproj`（rc 0），再跑严格 suite 并返回 **1**，落盘
   `artifacts/environment/tests/regression/{regression.json,regression.md,records/}`；逐 case 判定应为
   **8 passed / 1 self_checked / 5 unsupported_* / 1 blocked_compare**，比较器自检 6/6。返回 1 是当前**正确**基线
   （必需 unsupported/blocked 存在时不允许伪装全通过），不是脚本故障。
3. 完成条件仍按主清单原合同（七 case 双侧、五 unsupported 原 ID、六类以上非零反例、源/reference 派生与随机 trace 可核对）。
   本包交付了入口层、8 个双侧 `passed` 与全部失败反例；`rng-initial-shuffle` 的双侧比较仍受阻（见下）。是否验收由负责人按主清单判断。

## 已交付与接口变化

新增/修改文件（全部在 `tools/differential/`）：

| 文件 | 变化 | 语义 |
| --- | --- | --- |
| `TestCommand.cs`（新） | 严格入口 `test --suite <suite>` | 只接受声明 `expected_case_ids` 的严格 suite；定义层失败 rc 2（不可载入/缺 case/未注册 case/注册字段不合法/零执行），执行层失败 rc 1（`diverged`/`unsupported_*`/`blocked_compare`/`missing_reference_record`/`reference_fault`/`fault`/`invalid_fixture`/`invalid_action`/`wiring_failed`/`missing_dimension`/`wrong_terminal`/`timeout`/自检失败/子集），参考适配器不可用 rc 3。复用 `BaseBackend`/`ReferenceBackends`/`Comparator`/`ComparatorSelfTest`，不另写第二套断言；参考侧永远独立执行或读外部记录，不复制 base 结果；外部记录的 `case_id` 与当前 case 不一致时判 `reference_fault`（拒绝按错误记录比较） |
| `Program.cs`（改） | 注册 `test` 子命令与 usage；`--case-timeout-ms` 校验 | 默认 `--suite regression --output artifacts/environment/tests/regression`，默认每 case 120 秒保护 |
| `suites/regression.json`（新） | 版本化严格 suite `env-t04-regression@1` | 必测分母 15 个：原 7 个可执行 case（原样保留 setup/script/provenance）+ 5 个 unsupported 原 ID（原 ID/原语义/原比较范围）+ `last-kill-before-terminal`（新实测）+ `rng-initial-shuffle`（登记为 `blocked_compare`）+ `factory-wiring-dev-scope`（`kind=wiring`）。每个 case 预登记 kind/base_source/字符串 game_seed/初始化来源/内容与变体/语义动作/必需维度/预期终点/独立参考来源；`target_scope` 与 `build_identity` 绑定 dev 范围、Ironclad、进阶 0、`sts2-frozen-2026-10-05` |
| `fixtures/negative/*.json`（新，5 个） | 失败契约反例 suite | `suite-missing-case`/`suite-unregistered-case`/`suite-script-stuck`/`suite-missing-dimension`/`suite-wrong-terminal`。它们是工具验收输入，**不在** `suites/regression.json` 分母里 |
| `test-env.ps1`（新） | 薄脚本 | 默认构建严格 suite 的依赖闭包（`Differential.csproj`）→ 跑 `test`；`-FullSolution` 构建整个 solution，`-RuleChecks` 追加 Engine/Slice 规则检查，`-SkipBuild`/`-Records`/`-Output` 直通。任何子进程非零原样成为脚本退出码。**ASCII-only**：Windows PowerShell 5.1 会把无 BOM 脚本按 ANSI 解码，非 ASCII 字面量会被破坏（首版实测解析失败） |
| `ReferenceBackend.cs`（改） | 终局投影修正 | 参考侧 `LegalActions` 在 `CombatState.IsOverOrEnding()`（参考自己的 API，与 `CheckWinCondition` 同判据、只读）时返回空动作集。理由见「首差」一节：终局边界两侧说的是同一个逻辑边界，不投影会把“终局后无动作”误报成规则差异并挡住牌区/资源事实。**不是**复制 base 结果 |
| `README.md`（改） | 命令表加 `test`；新增“严格入口（`test --suite regression`）”“失败契约反例”“一次严格 regression 实测（2026-10-08）”三节；投影限制补终局一条 | 使用者入口与实测证据 |

关键决策与理由：

- **第 1 组七个 case 的定义逐字沿用**：`fixtures/fixtures-v1.json` **未改**（探索性入口 `compare` 实测仍 rc 0 / `passed` 7），
  `suites/regression.json` 里对应 case 的 `setup`/`script`/`provenance` 原样复制，只补严格 suite 的预登记字段——
  既保留原七个可执行 case 与五个 unsupported 原 ID 的完整比较范围，也不让两条入口的定义漂移。
- **另立 `self_checked`**：`kind=wiring` 的工厂接线自检（dev 范围可用、Ironclad/Full/ascension=1/Silent/未知范围/未知源基线六项在建局前被拒绝、未启动会话零随机零玩家、随机算法/映射/Compute 在 `Start` 前就位、`Start` 恰好一次）的验收标准是自身实测断言，不是双侧比较，因此**不计入双侧 `passed`**、报告单列；rc=0 需要“全部 `passed`/`self_checked` 且至少一个双侧 `passed`”。
- **必需维度两侧都查**：参考侧没有随机流与 trace、也没有终局投影，所以 `streams`/`trace`/`terminal` 只在 base-only case 上声明；`expected_terminal` 是 base 侧断言（参考无胜负投影），报告里写明这条不对称。
- **`--only` 永远非零**：定向子集只报告该子集、`complete=false`，不能替代完整阶段；默认执行全部分母。
- **脚本默认只构建依赖闭包**：共享工作区里其他线的在制品可能正不编译（本次实测：net 线），那不属于本线改动，也不应挡住 B 线证据；`-FullSolution` 可显式要求整 solution。
- **超时用后台任务 + `Wait`**：超时判 `timeout` 并继续下一 case，不伪造终局；超时任务不会被打断（引擎没有取消接口），进程结束时随进程退出，报告如实写明。

## 验证与证据

工作目录一律为 `F:\Scylier-Project\active\auto-the-spire`。以下命令均由本会话实际执行，日志在
`.audit/dsh-env-t04-resume-2026-10-08/`。

| 检查 | 实际命令 | 返回码 / 结果 | 证据与证明范围 |
| --- | --- | --- | --- |
| 工具构建 | `dotnet build tools/differential/Differential.csproj` | **0**（0 错误 0 警告） | 新入口编译；依赖 Core/Base/Sts2Sim.Core |
| 薄脚本入口（契约命令） | `powershell -NoProfile -ExecutionPolicy Bypass -File tools\differential\test-env.ps1 -Suite regression` | **1** | 脚本自身走通：构建依赖闭包 → 跑严格 suite → 原样返回 1。日志 [regression-run.log](../../../../.audit/dsh-env-t04-resume-2026-10-08/regression-run.log) |
| 严格 suite（直接命令） | `dotnet run --project tools/differential -- test --suite regression --output artifacts/environment/tests/regression` | **1**；run_id `regression@env-t04-regression@1+20261008T080103Z`；期望/选中/执行 **15/15/15**；双侧 `passed` **8**、`self_checked` 1、不计通过 6、参考侧实跑 12、比较器自检 6/6 | [regression.json](../../../../artifacts/environment/tests/regression/regression.json) + [regression.md](../../../../artifacts/environment/tests/regression/regression.md) + `records/base/*.json`(15) + `records/reference/*.json`(12)。这证明“原 7 个 case 实际双侧执行 + 新 T02 case 双侧通过 + 五个 unsupported 原 ID 保留且红态” |
| 判定分布 | 同上 | `passed` 7（strike-normal/upgraded、defend-normal/upgraded、bash-then-strike-vulnerable、bash-upgraded、candidate-scope-partly-playable）+ `last-kill-before-terminal` `passed`；`unsupported_decision_model` 1（headbutt-mid-choice）；`unsupported_rng_profile` 4；`blocked_compare` 1（rng-initial-shuffle）；`self_checked` 1 | 严格入口对每个未通过 case 都给出原因与首差；未删分母、未换弱 case |
| 失败契约反例 13 项 | 见 [failure-contract.log](../../../../.audit/dsh-env-t04-resume-2026-10-08/failure-contract/failure-contract.log) | 期望码全部命中：缺 case **2**、未注册 case **2**、非严格 suite **2**、零执行 **2**、unsupported **1**、异常 `invalid_action` **1**、缺维度 `missing_dimension` **1**、错误终点 `wrong_terminal` **1**、超时 `timeout` **1**、缺参考记录 `missing_reference_record` **1**、外部记录 `case_id` 不一致 `reference_fault` **1**、受控变异首差 `diverged` **1**、参考适配器不可用（`-p:ReferenceSimAvailable=false` 构建）**3** | 反例 suite 在 `fixtures/negative/`；产物在 `artifacts/environment/tests/negative/`。单 case 反例里比较器自检会如实报“没有可改目标”而 FAIL，这是 suite 级门槛的正常表现，逐 case verdict 行才是该反例的结论 |
| 旧入口回归 | `dotnet run --project tools/differential -- compare --suite fixtures-v1 --output artifacts/environment/fixtures` | **0**；`passed` 7、`unsupported_rng_profile` 4、`unsupported_decision_model` 1、自检 0 失败 | 终局投影修正没有改变探索性入口的判定 |
| 源随机独立核对（T01 支撑） | `dotnet run --project tools/differential -- rng` | **0**；读数向量 5/5 一致；布局向量不一致 8 条**全部是已登记参考偏离**、未登记 0 | `artifacts/environment/rng-crosscheck.json`。证明 base 侧算法/布局与独立神谕一致；**不等于**与参考双侧一致 |
| 规则检查（未受影响） | `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests`；`dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | **0 / 0**；Engine **95/95**、Slice **324/324** | 日志 [supporting-checks.log](../../../../.audit/dsh-env-t04-resume-2026-10-08/supporting-checks.log)。本包只改工具，规则/内容未受影响 |
| 文档与空白 | `python tools/doc-check/check_docs.py`；`git diff --check` | **0 / 0**（240 份文档 0 问题；无空白错误） | 新增/修改文件另做过行尾空白扫描，无命中 |
| 整 solution 构建 | `dotnet build AutoTheSpire.sln` | **1**（5 个错误，全在 `net-the-spire/Net.TheSpire.Tests`） | **跨线阻塞，非本包改动**，见下节 |

本次**未运行**：net 检查（被同一编译错误挡住）、`inventory`/`verify`/`run`、`ironclad-run`/`full-singleplayer` suite、
Release 成本测量、公开 Host 轨迹（C 线）、任何训练。未运行原因逐条记在上表与下节，不用旧数字代替。

## 完成条件对应

| 原完成条件 | 本包证据 | 结论 |
| --- | --- | --- |
| 既有 7 个 case 实际双侧执行 | 严格 suite 全量运行，7 个 case 全部 `passed`（另加 `last-kill-before-terminal` 也 `passed`，共 8） | **满足** |
| 5 个 unsupported 原 ID 保留 | `headbutt-mid-choice` + 4 个 `unsupported_rng_profile` 原 ID、原语义、原比较范围逐条保留在分母里并红态 | **满足** |
| 缺 case / 零执行 / 缺参考 / unsupported / 首差 / 缺维度非零 | 13 项失败契约反例全部命中期望返回码（并补了未注册 case、异常、超时、错误终点、参考不可用、外部记录串号） | **满足** |
| 源/reference 派生与随机 trace 可核对 | `rng` rc=0（神谕 5/5、布局 8/8 已登记偏离）；base 侧 `rng-initial-shuffle` 实跑（工厂 + 字符串 seed + `vanilla-compatible@1`，记录在 `records/base/rng-initial-shuffle.json`）；**双侧**随机比较未成立 | **部分**：base 侧与独立神谕已核对；双侧 blocked |
| 命令契约两个入口 | `test-env.ps1` 与 `test --suite regression` 都实测通过（本机无 `pwsh`，用 `powershell -ExecutionPolicy Bypass` 等价调用） | **满足**（调用名差异是环境事实） |

## 差异、阻塞与未验证

首个差异（本轮唯一工具侧首差，已按参考自身 API 修正）：`last-kill-before-terminal` 第一次跑出
`boundary[2].actions[base-spire:EndTurn]: 缺失 ≠ 存在`。原因在**工具投影**：终局后 base 会话不再报合法动作，
而参考适配器只按手牌枚举、仍列 `EndTurn`。按参考自己的 `CombatState.IsOverOrEnding()` 投影成空动作集后该 case
双侧 `passed`——修的是投影，不改参考行为、不复制 base 结果。**谁对**仍以 `sts2/` 为准；此处两个状态表达的是
同一逻辑边界（终局后无外部动作），因此不构成规则裁决。

阻塞（窄口径，只阻塞实际受影响的比较）：

- **`rng-initial-shuffle` 双侧随机比较**。依赖：① 参考侧缺少“同一字符串游戏 seed 建局 + 观察流布局/首次洗牌”的
  运行层入口（现有适配器是单场战斗，用数值 seed 新建 `RunState`）；② 参考检出的 seed 派生领先冻结基线
  （`RunRngSet.Seed` 已是 `ulong` + XxHash64、流种子走 `CreateSemanticKeyRequired`，冻结 `sts2/` 是 `uint` + int 循环，
  `sts2/src/Core/Runs/RunRngSet.cs:104-119`）。按 ENV 约束 §10，需要“参考冻结到同一版本”或“显式窄源版本 Profile”，
  两者都由负责人裁定，不在本包范围。最小复现：`dotnet run --project tools/differential -- test --suite regression --only rng-initial-shuffle`
  （rc 1，verdict `blocked_compare`）+ base 侧记录 `records/base/rng-initial-shuffle.json` + `rng` rc 0。
  **可继续事项**：该 case 已登记、已实跑 base 侧，接通参考运行层后原地升级为 `compared`，无需另造新 case。
- **`headbutt-mid-choice` 与 4 个 `unsupported_rng_profile`** 仍为必需红态（选牌决策源 `internal` 不可注入；
  敌方回合/重洗依赖原版兼容随机接线）。它们是**已经登记**的未支持比较，不是本包新增缺口，保留原 ID 等待原地升级。

跨线缺口（只报告，不代改）：

- `dotnet build AutoTheSpire.sln` 当前 **rc 1**：`net-the-spire/Net.TheSpire.Tests` 的
  `AcceptanceChecks.cs:73,286,594`、`ControlChecks.cs:77`、`MeasurementChecks.cs:202` 报 `CS0120`——把
  `ControlHost.SupportedScenarioProfile` 当静态成员用，而 `net-the-spire/Net.TheSpire/Control/ControlHost.cs:78`
  是实例属性。`net-the-spire/` 整目录尚未提交（`git status` 显示 `?? net-the-spire/`），最新改动时间 2026-10-07 22:15，
  属 **C 线在制品**；本包未触碰 net 文件，也不替它归一化。影响：`-FullSolution` 与 net 检查在当前工作区不可用。

未验证范围：公开 Host 全程轨迹（C/NET-01/02）、真实房间/奖励闭环（A/T05）、事件/hook 顺序轨迹比较、
`ironclad-run`/`full-singleplayer`/120 对矩阵与 Release 成本（B/DIFF-02）、任何训练。

行政小缺口（交负责人）：[半小时复核约定](../../../design/deepseek-harness-operations.md#半小时复核交接与接续)要求把交接实际路径写进本包
`task-record.json` 的 `handoff_path`，但该派单记录目前没有这个字段；派单文本已直接指定唯一交接为
`docs/review/tofix/dsh-env-t04-handoff-2026-10-08.md`（与约定的 `dsh-<任务ID>-handoff-<日期>-<会话短ID>.md` 命名不同）。
派单记录属负责人维护范围，本包未改它，也未另建第二份交接。

环境事实：本机只有 Windows PowerShell 5.1（`pwsh` 不存在），因此契约里的 `pwsh -File …` 用
`powershell -NoProfile -ExecutionPolicy Bypass -File …` 等价执行；`test-env.ps1` 保持 ASCII-only 以便在 5.1 下不被 ANSI 解码破坏。

## 维护位置

- 任务状态/认领/证据：主清单 [ENV-T04](../../tofix/README.md#env-t04)（本包只回写该条，不自判父项验收）。
- 方法与契约：`docs/design/post-core-agent-assignments.md` §4、`docs/design/environment-implementation-constraints.md` §9/§12、[工具 README](../../../../tools/base-migration-baseline/differential/README.md)（本包已更新）。
- 本次证据：`artifacts/environment/tests/{regression,negative}/`、`.audit/dsh-env-t04-resume-2026-10-08/`（派单记录、`regression-run.log`、`failure-contract/`、`supporting-checks.log`）。
- 前序证据：`.audit/dsh-env-t04-implementation-2026-10-07/`（旧会话技能安装与派单记录）、
  [2026-10-08 只读核验](../../tofix/env-t04-verification-2026-10-08.md)（指出当时入口尚未交付）。
