# ENV-T03 公开整局运行器交接 — 2026-10-11

- task_id: `ENV-T03-public-run-driver-parallel-20261011`
- dispatch_record: [.audit/dsh-public-run-driver-2026-10-11/task-record.json](../../../.audit/dsh-public-run-driver-2026-10-11/task-record.json)
- session_id: `session-7bab7f9a-2ad2-4389-a743-510f7bb77ca4`
- request_id: `163fc1be-3b7c-4832-8f73-82b3914da8d2`
- delivery_status: `待验证；本包执行结果与证据见下方及证据索引，主清单由 Codex 单写`
- updated_at: `2026-10-11T00:00:00+08:00`

接手第一步：读[证据索引](../../../.audit/dsh-public-run-driver-2026-10-11/evidence.md)（含全部运行清单、字节数、
四类验证映射），再到 `runs/<RUN>/summary.json` 核对 `status / exit_code / run_complete /
terminal_result / identity / seed_accepted / rooms / policy`，需要原始字节时读同目录 `frames.ndjson`
（双向原始 NDJSON 帧）与 `steps.ndjson`（语义操作 + 实时公开状态视图）。审阅通过后由 Codex 更新
[主清单 T03](README.md#env-t03)。执行者交付后停止，不续派、不提交。

## 交付内容

新增 [公开整局运行器](../../../net-the-spire/client/run_game.py)（唯一新增生产文件）与其
[使用说明](../../../net-the-spire/client/run_game.md)。运行器只用
[NetClient](../../../net-the-spire/client/net_client.py) 的公开面（`describe` / `get_catalog` /
`start` / `step` / `close`），从**实时快照的公开 `legal_actions`** 现场挑候选，用当前
`observation_id` 作 `expected_observation_id`；不写死 `candidate_id`、不写死 seed 脚本、不维护逐张牌
白名单、不读隐藏牌堆/随机数/Engine 内部状态、不做 AI 评估、不做多 seed 搜索。

**未修改**：`net_client.py`、`test_potion_host.py`、[client README](../../../net-the-spire/client/README.md)、
Host/C#、协议与 schema、base/core/tools/参考、AI、主清单与阶段。没有跨模块接口缺口需要 Codex 先补。

CLI：`--host-dll --scope --seed --output --max-actions`（另有 `--scenario --profile --timeout
--frame-limit --relic-policy --potion-policy --use-potions --step-frontier --step-unsupported-room
--stall-limit --probe-stale-observation`）。换 DLL / 换 seed 只改命令行；决策顺序固定且可读
（终局 → pending 选择 → 幕完成边界 → 地图 → 奖励 → 宝箱 → 营火 → 切片推进 → 战斗 → 未支持边界），
明细见[使用说明](../../../net-the-spire/client/run_game.md)。

## Host 与身份

只读不可变 Host：[.artifacts/parallel/dsh-core-author-04/bin/Net.TheSpire.Host/debug/Net.TheSpire.Host.dll](../../../.artifacts/parallel/dsh-core-author-04/bin/Net.TheSpire.Host/debug/Net.TheSpire.Host.dll)，
SHA256 `805C123D9941454E0B15E704CD48EB107A2E2A26DCC4555DDE8617513D0C1F6D`（每份 summary 的
`host.dll_sha256` 逐次一致，未构建或写入该目录）。没有用当前 A 源码构建任何东西，也没有编译
base/net 的 C#（只用 `python -m py_compile` 检查运行器语法）。

dev-map 实测身份（每份 summary 的 `identity`）：`net:backend:simulation-0.2-350911d86bad900d` /
`net:catalog:b400d7f332f590cc` / `net:ruleset:bf54a1e5d279a86b` / protocol `0.2`，profile
`base-spire-singleplayer-dev-map-v1`；与 [act1_smoke 夹具](../../../tools/base-spire-dev/fixtures/act1-basic-10.json)
的 expected_identity 一致，但**运行器不固化该身份**——每局按 `describe` 实测并要求与自身观测一致。
`dev-map` 的 `limit` 为 entities 256 / legal_actions 256 / sequences 10 / frame_bytes 262144。

## 实际命令与结果

```powershell
python -X utf8 -u net-the-spire/client/run_game.py `
  --host-dll .artifacts/parallel/dsh-core-author-04/bin/Net.TheSpire.Host/debug/Net.TheSpire.Host.dll `
  --scope dev-map --seed <SEED> --output .audit/dsh-public-run-driver-2026-10-11/runs/<RUN> `
  --max-actions 700 --use-potions
```

代表轨迹（全部 rc 由运行器自行给出；宿主进程退出码均为 0，`close_ok=true`，`frame_diagnostics`
无迟到帧/传输失败）：

| 运行 | seed | 结果 | 退出码 | 说明 |
| --- | --- | --- | ---: | --- |
| `runs/seed-K` | `ENV-T03-SEED-K` | 315 步 `death` | 0 | **最深真实轨迹**：从正常 `start` 走通地图选点、战斗、奖励、宝箱（跳过遗物）、营火，推进到 **Boss 房** `base-spire:vantom-boss`（floor 17、room 12、gold 302），`terminal_result.outcome=loss`、`Defeat.Turn=6`；`run_complete=true` |
| `runs/seed-K-repeat` | `ENV-T03-SEED-K` | 315 步 `death` | 0 | **可复现**：与 `runs/seed-K` 的 `steps.ndjson` 逐行相同（315 个 `action` / `candidate_id` / `observation_id` 全等，末观测 `12972`，两个文件字节数也一致） |
| `runs/seed-E` | `ENV-T03-SEED-E` | 240 步 `unsupported_map_room` | 4 | floor 13、Hp 70/80，地图节点只提供 `Unknown`（事件房未接线）→ 推之前停下，地图节点保留、状态不变，`run_complete=false` |
| `runs/act1-full-A2` | `ACT1-BASIC-10` | 179 步 `death` | 0 | 同一公开 seed 的正常驱动整局死亡 |
| `runs/seed-B` / `runs/seed-G` | `ENV-T03-SEED-B` / `-G` | 138 / 182 步 `death` | 0 | 另一固定 seed 的真实开局与步骤 |
| `runs/smoke-6` | `ACT1-BASIC-10` | 6 步 `step_limit` | 3 | `--max-actions 6`：步数截止不是整局通过 |
| `runs/act1-full-A` | `ACT1-BASIC-10` | 51 步 `fault_host_rejected` | 4 | 实地把商店房推给宿主 → `backend_fault`（商店房未实现）；故障不是整局通过 |

4 个 run 目录的 `summary.json` 完整列出 `rooms[]`（各房间长期状态：`Run.Hp/MaxHp/Gold/RoomCount/
CurrentRoom/EncounterId`、长期牌堆、遗物、药水带）与 `seed_requested` / `seed_accepted`（原样字符串，
驱动器不做 seed 标准化）。`frames.ndjson` 保留双向原始帧（单局 6–30 MB，seed-K 为 29423804 B）。

## 四类验证要求

1. **给定原 seed 正常公开驱动**：`runs/act1-full-A2`（179 步死亡）、`runs/act1-potions-A3`、`runs/smoke-6`
   （同一 seed 的步数截止分类）。不是零步入口检查。
2. **另一固定 seed 实际开局/步骤**：11 个不同 seed 的实跑（K/E/G/J/F/D/M/L/I/B 等），其中 seed-K 推进到 Boss 房。
3. **更换合法候选必须用当前观察**：每一步都断言候选出现在**当前**快照的 `legal_actions`，并用当前
   `observation_id` 发送（[run_game.py:705-724](../../../net-the-spire/client/run_game.py)）。负对照
   `runs/smoke-6/stale-observation-probe.json`：用过期 obs `1426` + 旧候选重发，宿主回
   `error.code=stale_observation`、`execution_state=not_started`、`retry_mode=query_request`、
   `message="expected_observation_id=1426 已过期（当前 1689）：必须重新 observe"`。
4. **ActCompleted / 步数截止 / 故障不得写成整局通过**：`run_complete` 只在拿到 `win`/`loss`/`other`
   终局时为 `true`；`step_limit`→退出码 3、`fault_host_rejected` / `unsupported_map_room` /
   `unsupported_action` / `no_legal_actions` / `stalled` / `uncertain`→退出码 4，全部 `run_complete=false`。
   只有真终局的 run（seed-K、seed-B、seed-G、act1-full-A2）写 `whole_run_outcome`。

## 未覆盖与后续

- **`ActCompleted`（`MapPhase==6`）未在实跑中到达**：固定策略目前要么在幕 1 末段战斗死亡（jaxfruit/
  inklets），要么先撞上只提供未接线房型的地图节点。因此"第一幕过关不得当整局胜利"目前**只有代码与
  协议层依据**：`act_completed_dev_boundary` 分类、`summary.act_completed_observed`、以及默认不推
  `ContinueToNextAct`（`--step-frontier` 才实地推进，宿主会具名 `Fail("act transition is not implemented
  in dev-map scope")`）。**如实记为未覆盖**，没有注入 Engine 状态造正常轨迹。
- 未接线范围：`Unknown`（事件）/ `Shop`（商店）/ `Elite`（精英）房型、幕转换、部分宝箱遗物与药水效果；
  运行器在这些边界**选之前**停成 `unsupported_map_room` / `unsupported_action`（`--step-unsupported-room`
  可实地取回宿主的具名失败）。
- 本包不是通关能力声明：没有多 seed 搜索、没有训练、没有 AI；`seed-K` 是"能推进到 Boss 房"的真实
  证据，不是"必然通关"。
- 新初始化 DLL 上的整局复放由 Codex 在 A 最终构建后协调：届时只需换 `--host-dll` / `--scope` / `--seed`
  重跑，运行器会按新 `describe` 的身份与本包未固化的一致性断言自行校验；旧不可变 Host 的证据**不能**
  升级为新输入验证。幕转换或房型接线后，`--step-frontier` / 新增房型可让同一运行器继续推进到
  `ActCompleted` 甚至整局 `victory`。

## 文档与仓库检查

新增两份文档（本交接 + [运行器使用说明](../../../net-the-spire/client/run_game.md)）与
[证据索引](../../../.audit/dsh-public-run-driver-2026-10-11/evidence.md)；纯文档改动按根约定跑
`python -X utf8 tools/doc-check/check_docs.py` 与 `git diff --check`，结果见
[验证记录](../../../.audit/dsh-public-run-driver-2026-10-11/validation.json)。
