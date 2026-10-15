# ENV-T03 跨会话候选绑定交接 — 2026-10-11

F-05 跨会话候选误接受已修复：公开 `candidate_id` 现在是**该会话当前稳定边界**的提交引用，同 seed、同观察版本、
同动作语义的两局不再共享句柄。net 全量检查从 415/416 变为 **416/416**（本条为本地独立构建；完整公开整局复放
与父项验收仍由负责人串行收口）。

- 任务 ID：`ENV-T03-candidate-session-binding-20261011`（父项 `ENV-T03`）
- session_id：`session-2e912a79-fdcd-40cf-9e19-6f08beeba395`
- request_id：`7ad3ed76-4630-4c09-90c8-c96f2b9b48bf`
- 派发记录：`.audit/dsh-candidate-binding-20261011/task-record.json`
- dispatch_id：派发记录与 `.audit/dsh-candidate-binding-20261011/dispatch-meta.json` 均未登记该字段，
  本条按实际字段回报（不编造）。
- delivery_status：`ready_for_review`
- updated_at：`2026-10-11T22:34:44+08:00`
- 主清单项：`ENV-T03`（本轮由 Codex 单写；本交接只作固定交接与证据）

`ready_for_review` 不等于验收，不关闭父项，不启动下一个包。

## 接手后先做什么

1. 读本条与[主清单 ENV-T03](../../tofix/README.md#env-t03)，核对文件边界（net 候选绑定）与本包已交付部分，
   避免重做：绑定修复、F-05 同 seed 判别用例、候选契约说明三者都已在本工作区（未提交）。
2. 负责人串行收口时，用**同一构建**复跑 net 全量并做正式公开整局复放；本条只给独立构建的局部证据。
3. 需要重录的工具/训练夹具见“差异、阻塞与未验证”第 3 条：那是本次句柄派生的必然迁移，不是新的玩法差异。

本次无待续生产小项；下一步由负责人按主清单决定（父项、工具夹具重录与整局复放均不在本包授权内）。

## 已交付与接口变化

| 项 | 内容 |
| --- | --- |
| 新增能力 | 候选提交引用绑定实际会话：外局候选在本会话绑表中不存在 → `illegal_candidate`，`execution_state=not_started`，不推进引擎与观察 |
| 修改文件 | `net-the-spire/Net.TheSpire/Projection/PublicSnapshotProjector.cs`（命名空间令牌 + `BuildEntry`/`CandidateId`/`TextDigest`/`CandidateHandleDigest`）、`net-the-spire/Net.TheSpire.Tests/FormalPathChecks.cs`（F-05 追加同 seed 判别用例） |
| 契约说明 | `docs/design/net-the-spire/protocol.md`（§2 `candidate_id` 行）、`docs/design/net-the-spire/contract-artifacts.md`（§3 追加一段绑定语义） |
| 旧行为 | `candidate_id = "c-" + sha256(动作语义键)[..16]`：同 seed 两局逐字相同，第一局候选可提交第二局（负责人复现 `.audit/base-developer-review-2026-10-11/cross-session.json` 的 `also_in_second_snapshot=true`） |
| 新行为 | `candidate_id = "c-" + sha256("candidate-v1" ‖ sha256("candidate-namespace-v1" ‖ session_id) ‖ 语义键摘要)[..16]`；`_bindings` 仍只装本会话投影发布的候选，`SemanticKeyDigest` 仍是纯 `{action_type, fields}` 摘要 |
| 公开语义键 | 不变：同观察版本/同动作语义仍是同一摘要，跨会话可对齐；会话标识不进语义键、不进 `ruleset_id`/`catalog_id`/`backend_profile_id`/build 身份，也不进任何特征（`SnapshotChecks` 的“candidate_id 不进特征”仍通过） |
| 幂等/过期 | 不变：`ControlHost.Step` 仍先 `ReplayIfKnown`（同 `request_id` 同载荷返回原响应、换载荷 `request_conflict`），再核 `expected_observation_id`（`stale_observation`），再查绑表；拒绝路径不执行引擎 |
| 决策应答 | 覆盖：`BuildDecisionAnswers` 走同一 `BuildEntry`，`net.answer_decision` 候选同样按会话派生句柄 |
| C#/Python/schema | schema 与协议版本无变化（`candidate_id` 仍是“快照内唯一非空不透明字符串”）；Python 侧无代码改动；**没有**改协议载荷字段、Host 组包、客户端或 core/base/tools/AI 生产文件 |

未改动的文件刻意保留：`ControlSession.cs` 的 `_bindings`/`Step`/`Snapshot` 接线、`Contracts/` DTO、
`ControlHost.cs`。会话隔离所需信息（唯一 `session_id`）本就在构造 `PublicSnapshotProjector` 时传入，
因此本包不需要第二套绑定表，也没有新建 RunState/调度器或缓存随机结果。

## 验证与证据

固定玩法输入（**未重编** base/solution，取 `.artifacts/parallel/package-decoupling` 已验证闭包）：

| 输入 | SHA256 |
| --- | --- |
| `Core.TheSpire.dll` | `9071EC93929671351294A668A31B0450466EDDD248E1C23344A672892F48F1E3` |
| `BaseSpire.dll` | `44F4796BDD65E5BCBAC03A031491A08A35A5890A42AF8ECC1A26A37B7D8CE976` |

本包新源码（工作区当前值）：

| 文件 | SHA256 |
| --- | --- |
| `PublicSnapshotProjector.cs` | `C88EBAFC14EBB36EBEABA6EBA42AD6B4BC86AD82AB6F3510835A5019BAA14403` |
| `FormalPathChecks.cs` | `8BAD81F7F2E72C264473D24F13CE79C534304FEC9F9D54533D6E89F584255B6B` |
| `protocol.md` | `45864FDDD6AD3FBDBD0E3CE9B2AA6384D56D2CA5E650402A74855464B3BB21B4` |
| `contract-artifacts.md` | `4C2CB2A93AF7972B2D3A61EFDD6855B302F161CFEA177959E20806B02DA791FD` |

新构建（本包独立输出，不写其他构建根）：`.artifacts/parallel/dsh-candidate-binding-20261011/bin/Debug/net9.0/Verify.Net.TheSpire.Tests.dll`，
SHA256 `35AD468C1582D6A54C4E59083F509D1C6B8F22B60B1C8CAB8F452339D7880FF7`（obj/bin 全删后从零编译；
编译行含全部 net Contracts/Net/Host/Tests 源，`/main:NetTheSpire.Tests.Program`）。

| 检查 | 工作目录与实际命令 | 返回码/结果 | 证据与证明范围 |
| --- | --- | --- | --- |
| net 全量（修复前基线） | 根目录 `dotnet .artifacts\parallel\dsh-candidate-binding-20261011\bin\Debug\net9.0\Verify.Net.TheSpire.Tests.dll` | rc1，415 passed / 1 failed / 416；唯一失败 F-05「期望错误响应」 | `.audit/dsh-candidate-binding-20261011/baseline-net-run.txt`；复现负责人数字 |
| net 全量（修复后，最终） | 同上（obj/bin 先删） | **rc0，416 passed / 0 failed / 416** | `.audit/dsh-candidate-binding-20261011/final-net-run-clean.txt`、`final-build-from-clean.txt`；F-05 本次含同 seed 跨会话判别 |
| net 全量（交接时确认复跑，同一组 SHA256） | 同上 | **rc0，416 passed / 0 failed / 416**；F-05 打印「观察=1824 语义键=6 条一致；candidate_id 交集 0；外局拒绝 code=illegal_candidate」 | `.audit/dsh-candidate-binding-20261011/final-net-run-confirm.txt` |
| 独立最小复现（同 seed 与不同 seed 两组） | `dotnet .artifacts\parallel\dsh-candidate-binding-20261011\probe-bin\Debug\net9.0\Probe.CrossSessionCandidate.dll .audit\dsh-candidate-binding-20261011\cross-session-after.json` | rc0，两 case 全通过 | 修复前 `cross-session-before.json`（两 case 均 `ok=true` 被接受、`state_version` 1824→1884）；修复后 `cross-session-after.json`（`illegal_candidate`、`state_version` 保持 1824、本局候选成功） |
| 判别力（去掉会话绑定后跑同一套） | 沙盒副本 `.artifacts/.../discriminator`（临时，已删）编译当前源 + 旧派生 | rc1，415 passed / 1 failed / 416；**唯一失败即 F-05** | `.audit/dsh-candidate-binding-20261011/discriminator-no-binding-run3.txt`；证明新用例抓的是绑定错误本身，不是断言放宽 |
| 真实公开帧复验（0.2 schema + L1/L3 不变量） | `python -X utf8 .audit\dsh-candidate-binding-20261011\validate_frames.py` | rc0，6 snapshots / 40 checks 全通过 | `.audit/dsh-candidate-binding-20261011/frame-validation.txt`；原始帧 `public-frames-newhandles.jsonl`（describe/start×2/observe×2/step×2/拒绝帧）。该脚本只读 `tools/contract-check`，其中 `check_request` 的 0.1 数值 seed 分支（I-76）按版本排除，0.2 字符串 seed 由同一帧的 0.2 schema 校验 |

关键实测数字（`cross-session-after.json`）：

- 同 seed 两局：`observation_id` 均 1824，`candidate_id` 交集 **0**，动作语义键交集 **6**（逐条一致）。
- 外局候选提交：`ok=false`、`code=illegal_candidate`、目标会话 `state_version` 保持 1824。
- 本局候选提交：`ok=true`，`state_version` 1824→1884；随后同 `request_id` 同载荷重发逐字一致、换载荷 `request_conflict`、
  旧观察版本 `stale_observation`、未知候选 `illegal_candidate`。
- 修复前同两份探针在**不同 seed** 下也 `ok=true`：证实不是 seed 巧合，而是句柄只由语义摘要派生。

seed / Profile / source：seed 为每局游戏字符串 `ENV-IRONCLAD-00`（同 seed 组两局相同）与 `ENV-T03-C-02`（不同 seed 组）；
正式 dev 范围；规则集/目录/后端身份取构建实际值（探针原样记录在 JSON 的 `session_*`/identity 分支）。
未执行：根 `dotnet build AutoTheSpire.sln`、Engine/Slice 套件、`net-the-spire/client` Python 套件、`tools/base-spire-dev/act1_smoke.py`
正式整局、真实客户端与双侧对拍。原因：本包只改 net 候选句柄派生，按根测试取舍用固定闭包跑受影响 net 组；
完整门槛由负责人同源串行收口。

## 差异、阻塞与未验证

1. **首差（已修）**：修复前 `observe` 同版本候选句柄逐字相同，外局 `step` 被接受（返回 `ok=true` 并推进 1824→1884）。
   最小复现即上面的探针；源码定位 `PublicSnapshotProjector.BuildEntry`（旧 `candidateId = "c-" + digest[..16]`）。
2. **未验证范围**：正式公开整局复放（Host 进程、真实 NDJSON）、Python 客户端套件、B 侧独立对拍、真实客户端均未运行；
   本包“416/416”只是同一临时构建上的 net 检查，不能替代公开全程与父项验收。
3. **要求工具/训练侧迁移（需负责人安排，本包不越界）**：句柄派生改变后，任何**按字面记录 `candidate_id`** 的既有夹具
   都会失配（夹具本来就声明“pinned to one seed and base build”、构建不符即报错，行为不变但内容过期）：
   - `tools/base-spire-dev/fixtures/act1-basic-10.json`（`candidate_ids` 序列）→ 正式整局冒烟需重录；
   - `ai-the-spire` 历史轨迹/记录里保存的 `candidate_id`（`data/record.py`、`runner/episode.py` 等）在新构建上重放需按新句柄重录；
   - 语义键对照、`ai` 侧 `candidate_id → 语义键` 的映射逻辑无需改动（仍从实时快照建立）。
   上述目录属 `tools`/AI 线，非本包授权范围，本包未修改任何夹具或脚本。
4. **依赖**：固定输入来自 `.artifacts/parallel/package-decoupling`（A 线包解耦验证闭包）。若 base 之后重编，
   候选句柄值随 `session_id` 命名空间不变、语义键随内容变；重录夹具必须在与最终发布一致的构建上做。
   可继续事项：负责人按主清单安排同源完整 net + 正式公开整局，再决定父项与夹具迁移。
5. **风险与边界**：命名空间只取 `session_id` 的 SHA-256 摘要，公开快照本就发布 `session_id`，不泄漏新信息；
   同一会话内句柄仍只由语义键决定，故 `observation_id` 仍是过期的唯一判据（推进后旧候选按原契约 `stale_observation`）。
   本包不建第二套候选表、不缓存随机结果、不改玩法权威。

## 维护位置

- 任务状态/认领/依赖/完成条件：主清单 [`ENV-T03`](../../tofix/README.md#env-t03)（本轮 Codex 单写）。
- 候选契约语义：`docs/design/net-the-spire/protocol.md` §2、`docs/design/net-the-spire/contract-artifacts.md` §3。
- 本包证据与原始帧：`.audit/dsh-candidate-binding-20261011/`；独立构建与探针输出：`.artifacts/parallel/dsh-candidate-binding-20261011/`。
- 本交接为固定交接件；负责人并入开放项后再按流程转 records，本次不自行迁移。
