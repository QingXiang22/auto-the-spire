---
task_id: ENV-T04
dispatch_id: ENV-T04-strict-success-gates-20261008
session_id: session-7e3c951d-f007-43d7-87c7-27daceb1d452
delivery_status: ready_for_review
updated_at: 2026-10-08T16:31:00+08:00
dispatch_record: .audit/dsh-env-t04-gates-2026-10-08/task-record.json
---

# ENV-T04 门槛覆盖 self_checked 接续修复交接 — 2026-10-08

本包修掉严格入口的一个**假通过**：必需维度/预期终点门槛原先只在 `verdict == "passed"` 之后执行，
`kind=wiring` 的工厂自检记为 `self_checked` 后直接绕过门槛——把工厂 case 的 `required_dimensions` 加上
`terminal`、或把 `expected_terminal` 错登记为 `defeat`，整批 9 个 case 仍返回 **rc 0**。现在门槛覆盖
**所有可计成功结果**（`passed` 与 `self_checked`），并新增两项**完整注册 suite**负例把"工厂缺维度"与
"工厂错终点"分开判别：修复前两项都 rc 0，修复后分别 rc 1 / `missing_dimension` 与 rc 1 / `wrong_terminal`，
其余 8 个正常双侧 case 仍 `passed`、比较器自检 6/6。正式 `regression` 15 项保持原红态（8 `passed` /
1 `self_checked` / 6 不计通过，rc 1），工厂 case 正常声明下仍 `self_checked`。
仍未完成的关键范围不变：`rng-initial-shuffle` 的**双侧**随机比较仍 `blocked_compare`，5 个 unsupported
原 ID 仍红；本包不关闭 T04，也不构成 M01 或父项验收。

归属：ENV-T04「严格regression接唯一工厂和独立参考」，B 线（`tools/differential/`）。
接手入口：主清单 [ENV-T04](../../tofix/README.md#env-t04)；[B 完整交付合同 §4](../../../design/post-core-agent-assignments.md#4-bdiff-0102日常严格对拍)；
[工具 README](../../../../tools/differential/README.md)（“严格入口”“失败契约反例”“门槛覆盖 `self_checked`”三节）。
交付范围：本会话是 B 线本包唯一生产执行者；只写 `tools/differential/`（`TestCommand.cs`、两项负例 fixture、README）、
本交接、主清单 T04 行、`.audit/dsh-env-t04-gates-2026-10-08/` 与 `artifacts/environment/tests/gates-2026-10-08/`。
core/base/net/AI/参考玩法与根 solution 未改，未提交/推送/reset/清理，未派生 agent。
派单记录：[.audit/dsh-env-t04-gates-2026-10-08/task-record.json](../../../../.audit/dsh-env-t04-gates-2026-10-08/task-record.json)
（`task_id` = `ENV-T04-strict-success-gates-20261008`，`session_id` 同上）。
前包：[严格 regression 入口交接](dsh-env-t04-handoff-2026-10-08.md)（会话 `session-2e21e798-…`，已 completed）。
本报告记录 2026-10-08 的交付；当前排期、状态与认领以主清单为准。

## 接手后先做什么

1. 先核对**已交付部分**，避免重做：本包复用且**未改写** `BaseBackend`（受控切片包与正式工厂两个来源）、
   `ReferenceBackend`/`ReferenceBackends`、`Comparator`、`ComparatorSelfTest`、`FixtureSuite` 的严格注册校验、
   `suites/regression.json`（15 必测，`suite_sha256` 未变）与 `test-env.ps1`。前包交付的入口层与 8 个双侧
   `passed`、五个 unsupported 原 ID、`blocked_compare` 都原样保留；本包只改判定门槛与新增两项负例。
2. 一个可执行的下一步（前置：先在本机串行构建工具，本机只有 Windows PowerShell 5.1、没有 `pwsh`）：
   在仓库根目录先 `dotnet build tools/differential/Differential.csproj`，再运行
   `dotnet tools/differential/bin/Debug/net9.0/Differential.dll test --suite tools/differential/fixtures/negative/suite-wiring-missing-dimension.json --output <隔离输出目录>`
   与同一命令换 `suite-wiring-wrong-terminal.json`。预期：**都返回 1**，9/9/9 全执行、8 个双侧 `passed`、
   工厂 case 分别判 `missing_dimension`（`missing_dimensions=[terminal]`）与 `wrong_terminal`
   （`missing_dimensions=[]`、终点问题非空），`complete=true`、比较器自检 0 失败。若返回 0 或工厂仍
   `self_checked`，说明门槛又被绕过。
3. 原完成条件仍按主清单与 [B 合同 §4](../../../design/post-core-agent-assignments.md#4-bdiff-0102日常严格对拍)：七 case 双侧、
   五 unsupported 原 ID 保留、六类以上非零反例、源/reference 派生与随机 trace 可核对。本包关掉的是
   "self_checked 绕过必需维度/终点"这一条假通过；`rng-initial-shuffle` 双侧与 5 个 unsupported 仍待接续。

## 已交付与接口变化

| 文件 | 变化 | 语义 |
| --- | --- | --- |
| `TestCommand.cs`（改） | 维度/终点门槛从 `if (verdict == "passed")` 改为 `if (verdict is "passed" or "self_checked")`；同步更新类注释、报告 `notes`、Markdown 小标题 | 所有**可计成功**结果都要过声明的必需维度与预期终点：`self_checked` 的语义只是"不要求双侧比较"，不是免登记断言。判决顺序不变（缺维度优先于错终点）；更具体的失败（`faulted`/`timeout`/`invalid_action`/`invalid_fixture`/`wiring_failed`/`unsupported_*`/`blocked_compare`/`reference_fault`/`missing_reference_record`/`diverged`）保持原判定，不被门槛覆盖；分母不变；缺失维度只如实列出，未提取的维度**不补占位读数** |
| `fixtures/negative/suite-wiring-missing-dimension.json`（新） | 完整注册 suite：8 个正常双侧 case + 工厂 case，`expected_case_ids` 9 项全部预登记，不用 `--only` | 工厂 `required_dimensions=[actions,facts,terminal]`、`expected_terminal=running`（终点登记正确），失败**只**来自缺维度。修复前 rc 0；修复后 rc 1 / `missing_dimension` |
| `fixtures/negative/suite-wiring-wrong-terminal.json`（新） | 同上完整 suite；工厂 `required_dimensions=[actions,facts]`（都可抽取）、`expected_terminal=defeat` | 失败**只**来自错误终点。修复前 rc 0；修复后 rc 1 / `wrong_terminal` |
| `README.md`（改） | “严格入口”新增"门槛覆盖所有可计成功结果"与 `self_checked` 不免除断言两条；失败契约反例表补两项完整 suite 负例与自检说明；新增“门槛覆盖 `self_checked`（2026-10-08 接续包实测）”一节 | 使用者入口与实测证据 |
| `.audit/dsh-env-t04-gates-2026-10-08/{build,verify}-negative-suites.py`（新） | 派生与核对脚本 | 两项负例由 `suites/regression.json`（`sha256 2dccebe9…`）**逐字段**派生：8 个可比较 case 的 `setup`/`script`/`provenance`/维度/终点/seed 等原样复制，唯一变量是工厂 case 的 `required_dimensions`/`expected_terminal`；核对脚本报 `RESULT: OK` |

关键决策与理由：

- **只改门槛适用范围，不改判决顺序、不改分母、不新增弱 case**：`passed` 的原行为因此逐条不变（正式
  regression 判定分布与修前一致），新行为只作用于原先被绕过的 `self_checked`。
- **负例必须"完整且唯一变量"**：单 case 反例（`suite-missing-dimension`/`suite-wrong-terminal`）仍然有效，
  但它们自检会因"没有可改目标"FAIL，且修复前就可能因别的原因非零，不能证明"`self_checked` 绕过门槛"。
  因此两项新负例用完整 suite（含 8 个正常双侧 case，修复前整批 rc 0）、自检 6/6、不用 `--only`，
  失败只能来自工厂 case 的对应字段。
- **缺维度与错终点分开**：缺维度负例把终点登记正确（`running`），错终点负例把维度登记成都能抽取
  （`[actions,facts]`），因此两项判决互不掩盖，能分别定位门槛的两个分支。
- **只在工具层修**：参考侧、base 工厂与规则实现未改；门槛是严格入口的判定口径，不构成规则裁决。

## 验证与证据

工作目录一律为 `F:\Scylier-Project\active\auto-the-spire`。以下均为本会话实际执行，日志在
`.audit/dsh-env-t04-gates-2026-10-08/`。

| 检查 | 实际命令 | 返回码 / 结果 | 证据与证明范围 |
| --- | --- | --- | --- |
| 修复前对照（被复核过的旧二进制） | `dotnet tools/differential/bin/Debug/net9.0/Differential.dll test --suite tools/differential/fixtures/negative/suite-wiring-missing-dimension.json --output .audit/…/prefix/wiring-missing-dimension`（另两项同形） | **0**（三项全是 0） | `Differential.dll` sha256 `c1ae4d3cc4e2dcc8c9ebfcb6dd4a1290084e3ec3ab9cdbcbda8b585c9448a886`，与负责人复核记录 `review-inputs.json` 一致；`TestCommand.cs` sha256 `2686dbfb…`。负例缺维度 run_id `negative-wiring-missing-dimension@env-t04-negative@1+20261008T082211Z`：9/9/9、`passed 8` / `self_checked 1`、工厂 `missing_dimensions=[terminal]`、终点问题为空；错终点 run_id `…+20261008T082212Z`：工厂 `missing_dimensions=[]`、终点问题非空；负责人反例 suite run_id `review-wiring-dimension-terminal@review-negative@1+20261008T082213Z`：同样 rc 0、工厂 `self_checked`。日志 [prefix-runs.log](../../../../.audit/dsh-env-t04-gates-2026-10-08/prefix-runs.log)，产物 `.audit/dsh-env-t04-gates-2026-10-08/prefix/` |
| 工具构建（本包唯一构建） | `dotnet build tools/differential/Differential.csproj` | **0**（0 错误 0 警告） | 日志 [build-tools-final.log](../../../../.audit/dsh-env-t04-gates-2026-10-08/build-tools-final.log)；产物 `Differential.dll` sha256 `18a2c69b0786976ad773f51b750b5ee5182c412f975771d7aa961466782b738d`，`TestCommand.cs` sha256 `d680b6ca560f5917bbd98c9a9c54ef5a6a1b3414ed68d885a234599a2172c97f` |
| 正式严格 suite（薄脚本入口） | `powershell -NoProfile -ExecutionPolicy Bypass -File tools\differential\test-env.ps1 -Suite regression -Output artifacts/environment/tests/gates-2026-10-08/regression -SkipBuild` | **1** | run_id `regression@env-t04-regression@1+20261008T082609Z`；15/15/15 全执行、`complete=true`、无 `--only`；双侧 `passed` **8**、`self_checked` **1**、不计通过 **6**、自检 **0** 失败；工厂 case 仍 `self_checked`（声明 `[actions,facts]` + `running` 都成立）。证明正常工厂与八个可比较 case 按原行为成立、正式分母红态保留 |
| 新负例：工厂缺维度 | `dotnet … Differential.dll test --suite tools/differential/fixtures/negative/suite-wiring-missing-dimension.json --output artifacts/environment/tests/gates-2026-10-08/wiring-missing-dimension` | **1** | run_id `negative-wiring-missing-dimension@env-t04-negative@1+20261008T082611Z`；9/9/9、`complete=true`、自检 0 失败；`passed 8` + 工厂 `missing_dimension`（`missing_dimensions=[terminal]`、终点问题为空，即失败只来自维度）。对比修复前 rc 0 |
| 新负例：工厂错终点 | 同上换 `suite-wiring-wrong-terminal.json` | **1** | run_id `negative-wiring-wrong-terminal@env-t04-negative@1+20261008T082612Z`；9/9/9、自检 0 失败；`passed 8` + 工厂 `wrong_terminal`（`missing_dimensions=[]`、终点问题非空，即失败只来自终点）。对比修复前 rc 0 |
| 负责人反例 suite（原样输入） | `dotnet … Differential.dll test --suite .audit/dsh-supervisor/review-2026-10-08-1606/suite-wiring-invalid.json --output artifacts/environment/tests/gates-2026-10-08/supervisor-wiring-invalid` | **1** | run_id `review-wiring-dimension-terminal@review-negative@1+20261008T082613Z`；输入 sha256 `f2b5c29a…` 未改；同一 suite 修复前 rc 0（`prefix/supervisor-wiring-invalid`，且负责人 `wiring-invalid/regression.json` 记录 rc 0），修复后工厂判 `missing_dimension`、rc 1 |
| 既有单 case 门槛反例（回归守卫） | 同上换 `suite-missing-dimension.json` / `suite-wrong-terminal.json` | **1 / 1** | run_id `negative-missing-dimension@…+20261008T082614Z`（`missing_dimension`）、`negative-wrong-terminal@…+20261008T082615Z`（`wrong_terminal`）；两者自检各 1 失败是单 case 反例的既有记录行为（README 已写明），verdict 未变 |
| 产物汇总 | `python .audit/dsh-env-t04-gates-2026-10-08/summarize-products.py` | **0** | [postfix-product-summary.txt](../../../../.audit/dsh-env-t04-gates-2026-10-08/postfix-product-summary.txt)（六项 run_id/rc/counts/verdict/工厂字段） |
| 负例派生核对 | `python .audit/dsh-env-t04-gates-2026-10-08/verify-negative-suites.py` | **0**（`RESULT: OK`） | 分母 9 项无重复且与 case 集合一致；8 个可比较 case 与 `suites/regression.json` 逐字段一致；工厂只改指定字段 |
| 文档与空白 | `python tools/doc-check/check_docs.py`；`git diff --check` | **0 / 0** | doc-check 扫描 253 份 Markdown、**0 问题**（含本交接与主清单）；`git diff --check` rc 0（注意 `tools/differential/` 与 `docs/review/tofix/` 当前整目录未跟踪，不在其检查范围），另对本包 9 个改动/新增文本文件逐行扫描，行尾空白命中 **0** |

本次**未运行**：全量 Engine/Slice 规则检查（本包只改工具判定门槛，规则/DSL/内容未动）、`compare`/`rng`/`inventory`/`verify`/`run`
（未被本包改动，前包证据仍有效且未重跑）、`ironclad-run`/`full-singleplayer`、Release 成本、公开 Host 轨迹（C 线）、
任何训练；`dotnet build AutoTheSpire.sln` 未跑（net 线在制 `CS0120` 由 C 线处理，本包不构建 root solution）。

## 完成条件对应

| 本包要求 | 实际证据 | 结论 |
| --- | --- | --- |
| 对所有可计成功结果（`passed` 与 `self_checked`）施加声明的必需维度/终点校验 | `TestCommand.cs` 门槛条件改为 `verdict is "passed" or "self_checked"`；两项负例实测工厂 `self_checked` 被改判 | **满足** |
| 保留原异常/unsupported/blocked 等更具体失败及原分母 | 门槛只在 `passed`/`self_checked` 分支执行；正式 regression 15/15/15 全执行、6 个 unsupported/blocked 判定与修前一致、`suite_sha256` 未变。实测反例：`random-reshuffle-unsupported` 报告里 `missing_dimensions=['streams(reference)']`（参考侧确实没抽到该维度），判定仍是 `unsupported_rng_profile`，没有被门槛改写成 `missing_dimension` | **满足** |
| 不给未提取维度伪造占位读数 | `MissingDimensions` 未改；缺维度负例产物只有 `missing_dimensions=[terminal]`，未出现伪造的 terminal 读数（终点点位仍为空、终点问题为空） | **满足** |
| 新增真正判别的负例：工厂缺维度、工厂错终点分开，完整注册 suite 含正常双侧 case，修复前 rc 0、修复后因对应原因非零，不靠 `--only`/无双侧通过/自检失败 | 两项负例均 9/9/9 全执行、无 `--only`、8 个双侧 `passed`、自检 6/6；修复前 rc 0、修复后分别 rc 1 且 verdict 为对应原因 | **满足** |
| 正常工厂 `self_checked` 与八个可比较 case 仍按原行为成立 | 正式 regression：工厂 `self_checked`，8 个双侧 `passed`，分布与修前一致 | **满足** |
| 正式 regression 15 项保持原红态且不删 case | rc 1；8 `passed` / 1 `self_checked` / 6 不计通过；分母 15 未删未换 | **满足** |
| 记录输入/构建/命令/rc/run_id 及实际 verdict | 上表与 [postfix-product-summary.txt](../../../../.audit/dsh-env-t04-gates-2026-10-08/postfix-product-summary.txt)、[prefix-runs.log](../../../../.audit/dsh-env-t04-gates-2026-10-08/prefix-runs.log)、[postfix-runs.log](../../../../.audit/dsh-env-t04-gates-2026-10-08/postfix-runs.log) | **满足** |

## 差异、阻塞与未验证

**本包的首差（缺陷复现与修复判别）**：同一份负责人反例 suite `.audit/dsh-supervisor/review-2026-10-08-1606/suite-wiring-invalid.json`
（sha256 `f2b5c29a…`，未改）修复前 `exit_code=0`、工厂 `verdict=self_checked`、`missing_dimensions=[terminal]`；
修复后同一输入 `exit_code=1`、`verdict=missing_dimension`。本包**没有**产生新的规则/双侧比较首差——
改动只在严格入口的判定门槛，两侧执行与比较算法未动。

阻塞与未验收范围（窄口径，保持与前包一致）：

- **`rng-initial-shuffle` 双侧随机比较仍 `blocked_compare`**：依赖参考运行层/字符串 seed 入口与参考版本冻结
  （或显式窄源版本 Profile），由负责人裁定；已登记在分母里，接通后原地升级。
- **5 个 unsupported 原 ID 仍红**：`headbutt-mid-choice`（`unsupported_decision_model`）与四个
  `unsupported_rng_profile`；保留原 ID、原语义、原比较范围，接通后原地升级，不另造弱 case。
- **T02 的 Run 长期写回 / Feed 余效仍未验收**：本包只覆盖入口判定门槛，不用本次修复升格为 T02 或 M01 通过。
- **跨线**：`net-the-spire/Net.TheSpire.Tests` 的 5 处 `CS0120` 属 C 线在制，本包未改 net，也未构建
  root solution / net 闭包。
- 未验证：公开 Host 全程轨迹、真实房间/奖励闭环、事件/hook 顺序轨迹、120 对矩阵与 Release 成本、任何训练。

## 维护位置

- 任务状态/认领/证据：主清单 [ENV-T04](../../tofix/README.md#env-t04)（本包只回写该条，不自判父项验收）。
- 方法与契约：`docs/design/post-core-agent-assignments.md` §4、`docs/design/environment-implementation-constraints.md` §9/§12、
  [工具 README](../../../../tools/differential/README.md)（本包已更新）。
- 本次证据：`tools/differential/fixtures/negative/suite-wiring-{missing-dimension,wrong-terminal}.json`、
  `artifacts/environment/tests/gates-2026-10-08/`（六项产物）、`.audit/dsh-env-t04-gates-2026-10-08/`
  （派单记录、`prefix-runs.log`、`postfix-runs.log`、`build-tools-final.log`、`postfix-product-summary.txt`、
  派生/核对/汇总脚本）。
- 前序证据：`.audit/dsh-supervisor/review-2026-10-08-1606/`（负责人复核与反例）、
  [前包交接](dsh-env-t04-handoff-2026-10-08.md)、`.audit/dsh-env-t04-resume-2026-10-08/`。
