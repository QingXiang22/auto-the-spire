# ENV-T04 严格 regression 交付核验 — 2026-10-08

本次只读核验让接手者不必重复构建或追问上一段会话，就能确认 ENV-T04 的严格 regression 入口**尚未交付**：合同要求的两个入口都不存在，`test` 子命令未实现，唯一可运行的二进制早于派单时间。T01/T02 所需的 B 双侧证据因此仍未解除依赖。

归属：ENV-T04「严格regression接唯一工厂和独立参考」。本报告是一次维护核验，不认领 B 的生产包。
接手入口：主清单 ENV-T04 条目；[B 完整交付合同 §4](../../design/post-core-agent-assignments.md#4-bdiff-0102日常严格对拍)；[工具 README](../../../tools/base-migration-baseline/differential/README.md)。
交付范围：只读。本会话未修改 `tools/differential/**`、A/B/C 生产代码、core 或主清单任务行。
本报告记录 2026-10-08 的核验结果；当前排期、状态与认领以主清单为准。

## 接手后先做什么

1. 先确认本包**已交付部分**，避免重做：`BaseBackend.cs` 已有正式工厂与受控会话路径，`Comparator`/`FixtureSuite`/`ReferenceBackend`/`RngCrossCheck` 已在树内；技能安装已完成且哈希一致（见下表）。
2. 一个可执行的下一步：由负责人确认 B 会话 `session-d15c22bb-09fa-4f1c-9b2e-141b54ba243b` 是否仍在推进。若仍在推进，本包无需他人介入；若已停止，见「差异、阻塞与未验证」的重派建议。
3. 本核验不改变 [ENV-T04 完成条件](../../review/tofix/README.md#env-t04)：七个既有 case 实际双侧执行、五个 unsupported 原 ID 保留、六类反例返回非零、源/reference 派生与随机 trace 可核对。上述合同一条都还没有执行证据。

## 现状核对（已交付 / 未交付）

已交付：

- `tools/differential/` 下的 `BaseBackend.cs`、`FixtureSuite.cs`、`Comparator.cs`、`ReferenceBackend.cs`、`RngCrossCheck.cs`、`CompareCommand.cs`、`VerifyCommand.cs` 等源文件在树内。
- 技能安装：`auto-the-spire-handoff`、`nbj-write-clearly` 装在用户级扫描根 `C:\Users\26877\.dsh\skills`；`documentation-diataxis` 按记录就地复用项目 `.agents/skills`（该根本身已被扫描）。十个文件的 SHA-256 与 `skills-source-manifest.json` 全部一致。
- 派单记录存在：`.audit/dsh-env-t04-implementation-2026-10-07/task-record.json`。

未交付（合同要求的入口层）：

- `tools/differential/test-env.ps1` 不存在；`tools/differential/suites/regression.json` 不存在。
- `tools/differential/Program.cs` 没有 `test` 命令分支；usage 只列 `inventory`、`compare`、`rng`、`verify`、`run`（标注未实现）、`verify --manifest`（标注未实现）。
- 交付记录只到 `status: "accepted"`，没有实现结果、构建身份或运行证据。

## 验证与证据

工作目录一律为 `F:\Scylier-Project\active\auto-the-spire`。以下命令均由本次核验实际执行。

| 检查 | 实际命令 | 返回码 / 结果 | 证据与证明范围 |
| --- | --- | --- | --- |
| 验收入口实跑 | `dotnet tools\differential\bin\Debug\net9.0\Differential.dll test --suite regression` | 退出码 **2**；输出 `未知命令 'test'。` | 证明已构建的二进制没有 `test`。该 DLL 构建于 **2026-10-06 21:32**，早于 2026-10-07 派单，不能代表 T04 工作 |
| 命令表 | `dotnet <同一 DLL> --help` | 输出用法；无 `test` | 与合同 §4 的两个入口对照；`run`、`verify --manifest` 自述「未实现」 |
| 源码命令分支 | `Select-String tools\differential\Program.cs -Pattern 'case "'` | 命中 `inventory`/`compare`/`rng`/`verify`/`run`/`help` | 与二进制一致，不存在 `test` 分支 |
| 合同产物 | `Test-Path tools\differential\test-env.ps1`；`Test-Path tools\differential\suites\regression.json` | 两者均 **False** | 直接命中合同 §4 要求的薄入口与版本化 suite |
| 派单记录 | `Get-Content .audit\dsh-env-t04-implementation-2026-10-07\task-record.json` | `status: "accepted"` | 证明派发被接受；不含交付结论 |
| 技能哈希（独立复算） | `Get-FileHash <十个文件> -Algorithm SHA256` 对照 `skills-source-manifest.json` | **10/10 MATCH** | 本次独立复算确认安装记录，不只是采信其自报；`documentation-diataxis` 在项目根属预期，不是缺失 |
| 记录引用核对 | `Get-ChildItem docs\review\tofix`；`Get-ChildItem . -Recurse -Filter '*t04*'` | `docs/review/tofix/` 只有 `README.md`；全仓库无 `*t04*` 文件 | `skill-installation.json` 的 `usage_in_this_task` 指向 `docs/review/tofix/dsh-env-t04-handoff-2026-10-07.md`，该文件不存在 |

本次未运行：`dotnet build AutoTheSpire.sln`、Engine 检查、Slice 检查、net 检查、任何 `compare`/`test` 双侧执行。未运行的原因是本包入口不存在，且共享工作区正在被其他会话使用（避免并行构建写同一 `obj`/`bin`）。

## 差异、阻塞与未验证

首个差异：

- 预期（合同 §4 的待实现入口）：`pwsh -File tools/differential/test-env.ps1 -Suite regression` 与 `dotnet run --project tools/differential -- test --suite regression --output artifacts/environment/tests/regression`。
- 实际：两个入口都不存在；`test` 未被识别。
- 细节差异：README 第 19 行约定退出码 `3` 表示「未实现或参考不可用」，但调用 `test` 得到的是 **2**（用法错误），因为该子命令根本没有注册。合同想用 `3` 区分的「未实现」状态，在 `test` 上目前不可达。
- 最小复现：上表第一行命令。

阻塞与依赖：

- B 生产包由 `session-d15c22bb-09fa-4f1c-9b2e-141b54ba243b` 持有（主清单记「派发 accepted，安装与实施结果待复核」）。本次核验不认领、不重派、不代写其生产文件。
- 受影响事项：ENV-T01、ENV-T02 的「B 双侧 case」与 ENV-T05 的对拍证据都依赖本入口，因此三条线目前都缺同一层证据。
- 可继续事项（不依赖 B）：把七个既有 case 的原始记录与当前 `compare` 行为对账，只读确认「旧宽松返回码不得当验收成功」这条是否仍成立。

需要负责人决定的开放问题：

1. B 会话是否仍在推进；若已停止，T04 剩余入口是按新包重派，还是由负责人另指定实现者。
2. 记录的悬空引用（`skill-installation.json` 指向不存在的 T04 交接）是否需要按其规则修正，或由负责人并入本次核验。

未验证范围：本包的双侧执行、独立参考、首个差异定位与六类非零反例全部未验证；技能「自动发现」只由安装记录自报，本次只复算了文件哈希，未复现加载器发现过程。

## 维护位置

- 状态与认领：主清单 ENV-T04 条目及 §5 维护记录（本报告）。
- 方法与契约：`docs/design/post-core-agent-assignments.md` §4（本次未改）。
- 后续证据：负责人并入后转 `docs/review/records/2026-10-08/`。
