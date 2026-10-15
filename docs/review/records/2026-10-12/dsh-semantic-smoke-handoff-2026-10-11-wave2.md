# ENV-T04 语义重放冒烟交接（wave2）— 2026-10-11

`tools/base-spire-dev/act1_smoke.py` 现在按**公开动作语义**重放真实 Run，字面 `candidate_id` 只作原始帧证据：
同一固定 Host/scope/seed 的两次独立开局候选 ID 完全不同，仍能逐步唯一匹配并复现同一公开事实；只记
`candidate_ids` 的旧夹具被明确拒绝并给出迁移入口，不再有"字面 ID 悄悄退化成任意候选"的路径。

**当前固定构建的完整第一幕冒烟尚缺交付**：本包只交付到公开可达前缀（58 步）+ 已知 fogmog 故障的证据。

- 任务：`ENV-T04-semantic-run-smoke-20261011`（父项 `ENV-T04`，本包归 B 线 tools 冒烟工具）
- dispatch_id：`ENV-T04-semantic-run-smoke-20261011`（派发记录 `.audit/dsh-semantic-smoke-20261011/task-record.json` 的 `task_id`；
  `dispatch-meta.json` 另记 `parent=ENV-T04`，无独立 dispatch_id 字段）
- session_id：`session-26e42e29-9e84-4751-b131-524816932c9d`；request_id：`1a24e2d9-534b-4367-9a27-20bb0d52e521`
- dispatch_record：`.audit/dsh-semantic-smoke-20261011/task-record.json`（任务原文 `.audit/dsh-semantic-smoke-20261011/task.txt`）
- delivery_status：`ready_for_review`
- updated_at：`2026-10-11T23:53:00+08:00`
- 主清单项：`ENV-T04`（本轮认领/排期由 Codex 单写，本交接不改主清单；派发 accepted 与 `ready_for_review` 都不等于验收）

## 接手后先做什么

1. 读本条与派发记录，核对文件边界（只动 `tools/base-spire-dev/`）与本包已交付部分，避免重做：
   语义 helper、`act1_smoke.py` 语义重放/记录、当前构建前缀夹具、反例与两次独立会话证据都在工作区（未提交）。
2. 复现本包结论只需一条命令（前置：不重编任何工程，固定 Host 已存在）：

   ```powershell
   python -X utf8 .audit/dsh-semantic-smoke-20261011/verify_two_sessions.py
   ```

   预期：rc0，`two-session-verification.json` 的 14 项检查全 `true`，`candidate_id_intersection_across_sessions`
   与 `recorded_vs_live_candidate_id_intersection` 均为空数组。完整命令与返回码见
   `.audit/dsh-semantic-smoke-20261011/commands-and-results.md`。
3. 完整第一幕冒烟的原完成条件保留在 `mode=full-act` 分支（开场 80 HP/99 金币/10 张起手、Boss 与宝物房间、
   `ActCompleted` + `MapPhase 6`、存活 HP、恰好一个 `ContinueToNextAct`、药水动作、不得发布整局胜利）。
   当前构建走不到那里：**商店共同构建落地后由 Codex 协调在同一构建上重录 full-act 夹具，再跑该条件**，
   本条不等待 A 重复重录、不缩池、不换 seed、不注入血量制造假通过。
4. 需要再接续时只更新 `ENV-T04` 对应行与本条；父项与完整身份由 Codex 验收。

## 已交付与接口变化

| 项 | 内容 |
| --- | --- |
| 新增语义层 | `tools/base-spire-dev/semantic_actions.py`：语义 = `{action_type, fields}`（`fields` 按 `field_id` 排序）；每个字段的 key 是会话无关规范值——公开实体引用取"公开身份摘要（`kind_id`+`content_id`+全部 known 字段）+ 公开集合定位（`sequences` 元素下标 / `members_complete` 的 `piles.visible_members` 下标 / 关系 source·target）"，其它标量取规范化 JSON，非 known 取状态标记 |
| 新能力（重放） | `act1_smoke.py --host-dll PATH --fixture FILE --output DIR`：每步在**实时** `legal_actions` 里按 key 找唯一候选，再提交它**当前**的 `candidate_id` + `observation_id`；随后逐步核对记录的公开游戏事实 |
| 新能力（记录） | `--record-from TRAJECTORY --fixture-out FILE [--scope dev-map]`：从公开轨迹提取语义动作。支持 `run_game.py` 的 `frames.ndjson`（原始双向帧）与旧冒烟 `public-trajectory.jsonl`（迁移入口）；不自己寻找游戏路线 |
| Host 选择 | `--host-dll` 显式指定（或既有 `NET_THE_SPIRE_HOST_DLL`）；**不自动构建、不回退到仓库内共享旧 DLL**，缺失时 rc2 |
| 严格拒绝 | 匹配 `missing` / `ambiguous` / `identity_mismatch` 一律带首差停止（`first-difference.json` + `semantic-steps.jsonl`），不提交坏步、不取第一个替代；每步公开事实差异同样是首差 |
| 旧夹具 | `fixtures/act1-basic-10.json` 原样保留可追溯；`act1_smoke.py` 读到只含 `candidate_ids` 的输入时 rc2 并打印迁移入口（`run_game.py … ` + `--record-from …`），不把它当候选池 |
| 新夹具 | `fixtures/act1-semantic-dev-map-prefix.json`（`schema=base-spire-dev/semantic-actions@1`，`mode=prefix`，58 步 + 已知故障）。`recorded.candidate_id` 只是录制会话的原始帧证据 |
| 退出码 | `0` 复放完成（`mode=prefix` 只证明前缀重放器，`summary.not_claimed` 明确不声明 `ActCompleted`/整局胜利）；`1` 首差；`2` 用法/夹具/Host；`3` 未满足第一幕完成条件（含把前缀夹具当整幕跑） |
| 未改动 | `net-the-spire/**`（含 `net_client.py`/`public_snapshot.py`/`run_game.py` 只读复用）、`tools/differential`、core/base/reference/AI 生产文件、旧夹具、主清单 |

`run_game.py` 的 `--host-dll` 语义（只设置 `NET_THE_SPIRE_HOST_DLL`）被沿用，客户端协议与 Host 参数未变，
所以本次没有协议/schema/客户端接口变化。

## 验证与证据

固定输入（未重编任何工程；Host 由 Codex 预编译，含 C 的会话绑定，不含 A 正在收口的商店）：

| 输入 | SHA256 |
| --- | --- |
| `.artifacts/parallel/owner-flow-review-next-20261011/bin/OwnerHost/debug/Net.TheSpire.Host.dll` | `78F2CDF57B5D04CDE0FE814933B21ABB041BC9024A41BC4AED285E2F55462B0A` |
| 同目录 `Core.TheSpire.dll` | `9071EC93929671351294A668A31B0450466EDDD248E1C23344A672892F48F1E3` |
| 同目录 `BaseSpire.dll` | `44F4796BDD65E5BCBAC03A031491A08A35A5890A42AF8ECC1A26A37B7D8CE976` |
| `tools/base-spire-dev/act1_smoke.py` / `semantic_actions.py` | `92EAFEA52AFB8FC493B6AC031BB7C8CCF2EC58CBB9DE2F9B13B9AF11B9894D9B` / `F5B65AD1DBE487FB7D203DBF568495AE68FDCCC102CD91131017678CB5807D9F` |
| `fixtures/act1-semantic-dev-map-prefix.json` | `5E691DD6C4DBB8019F74771CB4DF86A620A9EAC5EEF856150CBCA9B5A0FB6B90` |

实际 scope/seed/identity：scope `dev-map`，scenario profile `base-spire-singleplayer-dev-map-v1`，seed 字符串
`ACT1-BASIC-10`，identity `net:backend:simulation-0.2-350911d86bad900d` / `net:catalog:b400d7f332f590cc` /
`0.2` / `net:ruleset:4fa58ee1b2f76e3f`（两次独立会话一致）。

| 检查 | 工作目录与实际命令 | 返回码/结果 | 证据与证明范围 |
| --- | --- | --- | --- |
| 当前构建公开前缀录制（只读复用 C 的公开驱动） | 根目录 `python -X utf8 -u net-the-spire/client/run_game.py --host-dll <HOST> --scope dev-map --seed ACT1-BASIC-10 --output .audit/dsh-semantic-smoke-20261011/prefix-run --max-actions 400 --use-potions` | rc4；58 步成功后 `fault_host_rejected`（fogmog） | `prefix-run/frames.ndjson`（4 737 095 B，SHA256 `B4903A34…76FE4E`）、`steps.ndjson`、`summary.json`；证明当前构建的公开可达前缀与故障原文 |
| 提取语义夹具 | 根目录 `python -X utf8 tools/base-spire-dev/act1_smoke.py --record-from .audit/…/prefix-run/frames.ndjson --fixture-out tools/base-spire-dev/fixtures/act1-semantic-dev-map-prefix.json --scope dev-map --host-dll <HOST>` | rc0；58 动作 + 1 已知故障 | 同上夹具；记录模式输出 |
| **两次独立 Start 同语义前缀** | 根目录 `python -X utf8 .audit/dsh-semantic-smoke-20261011/verify_two_sessions.py` | rc0；14/14 检查通过 | `two-session-verification.json`、`replay-a/`、`replay-b/`、`two-session-run.log`；会话 `session-9b517d58eb4e-1` / `session-d60c35c8515b-1`（各自独立 Host 进程） |
| 反例组 | 根目录 `python -X utf8 .audit/dsh-semantic-smoke-20261011/negative_cases.py` | rc0；13 组全通过 | `negative-cases.json`、`negative-*.log`、`negative-out-*/` |
| 默认夹具用法（README 命令） | `act1_smoke.py --host-dll <HOST> --output .audit/dsh-semantic-smoke-20261011/replay-default` | rc0；58 步 + 已知故障复现 | `replay-default/`、`replay-default.log` |
| 空轨迹旧输入 | `act1_smoke.py --record-from .audit/act1-resume-2026-10-10/public-a/public-trajectory.jsonl …` | rc2；可读输入错误，不写夹具 | `commands-and-results.md` 第 10 行 |
| 文档检查 | 根目录 `python tools/doc-check/check_docs.py` / `git diff --check` | rc7 / rc0；7 处失败全在其它会话的文件（`dsh-shop-flow-handoff` 2、`dsh-power-fixture-handoff` 2、`dsh-startup-alignment-handoff` 2、`net-the-spire/client/run_game.md:183` 越出仓库根的 `.audit` 链接），本包新增文件未报错 | `commands-and-results.md`；纯文档门槛，不代替公开复放 |
| 自有轨迹往返 | 记录 + 重放 `roundtrip-from-replay-a.json` | rc0 / rc0 | `roundtrip-record.log`、`roundtrip-replay.log`、`replay-roundtrip/` |
| 旧格式迁移入口 | 对 `.audit/dsh-core-author-04-2026-10-11/act1-public/public-trajectory.jsonl` 跑 `--record-from` | rc0；326 动作 | `migrated-from-legacy-smoke.json` |
| 迁移夹具重放（旧 ruleset） | 对 `migrated-from-legacy-smoke.json` 跑重放 | rc1 `identity_mismatch`（`bf54a1e5d279a86b` vs `4fa58ee1b2f76e3f`），提交 0 步 | `replay-migrated-legacy/`；证明跨构建夹具被拒绝而不是静默继续 |
| Python 语法 | `python -m py_compile tools/base-spire-dev/act1_smoke.py tools/base-spire-dev/semantic_actions.py` | rc0 | 见 `commands-and-results.md` |

关键实测数字（`two-session-verification.json`）：两局 `observation_ids` 同为 2164→…→4128（59 个版本），
活候选 ID 交集 **0**，与录制会话字面 ID 交集 **0**，58 步 `candidate_id_reused` 全 `false`，
逐步公开事实逐字相同且与录制事实相同，前缀结束 `ActCompleted=false`、`terminal_result=null`，
已知故障 `backend_fault` 复现且 `observation_id` 保持 4128（未推进）。前缀路线为 3 个地图点
（Row1/Col3 → Row2/Col3 → Row3/Col4，三场战斗后奖励），`potion_actions=["TakeRewardPotion"]`。
把同一前缀声明为 `full-act` 时，原开场契约仍被检查并通过（`hp=80 / gold=99 / long_term_deck=10`），
随后以 rc3 `act_not_completed`（`map_phase=2, map_floor=4, run_hp=68, seen_boss=false`）拒绝——
前缀结束不会被写成整幕通过。

公开帧证据：`replay-a/public-trajectory.jsonl`、`replay-a/semantic-steps.jsonl`（含每步 offer/refs/事实差异）、
`replay-a/summary.json`、`replay-a/first-difference.json`（仅首差时产生）；原始录制帧 `prefix-run/frames.ndjson`。

未执行：`dotnet build AutoTheSpire.sln`、Engine/Slice 套件、`net-the-spire/client` Python 套件、
`tools/differential` 与任何参考对拍、AI、**完整第一幕冒烟**。原因：本包只改 tools 侧重放输入，按根测试取舍
用固定 Host 做公开复放；完整门槛需在含商店的共同构建上重录后再跑。

## 差异、阻塞与未验证

1. **首差（本包正向运行没有差异，只有预期阻塞）**：58 步后选择下一房间时宿主返回
   `code=backend_fault`、`message=… base-spire:fogmog-normal cannot execute: missing enemy behavior base-spire:fogmog`、
   `observation_id` 保持不变。重放会提交这一步并**要求它原样被拒绝**（`summary.known_fault.reproduced=true`）；
   把记录 code 改坏会得到 rc1 `known_fault_not_reproduced`，即 unsupported 故障不会被写成 rc0
   （反例见 `negative-cases.json` → `known_fault_code_changed`）。
2. **未验证范围**：完整第一幕完成条件（当前构建不可达）、商店与共同构建、Boss/宝物房间与药水使用/丢弃、
   非 `ACT1-BASIC-10` 的 seed、其它 scope/房型、独立原版对拍、真实客户端版本。两次独立运行各自起独立 Host
   进程（会话令牌每进程不同），所以候选 ID 交集 0 同时覆盖同进程两局与跨进程两局；同进程内两局的隔离由 C 的
   会话绑定与 F-05/H-03 检查承担，本包未重复验证。
3. **`ambiguous` 分支性质**：本协议面上同一快照的候选 key 两两不同（录制前缀 59 快照 / 215 候选 / 0 重复，
   见反例表），所以 `ambiguous` 是防御分支，只用"真实快照的受控副本（复制同一候选）"判别其分类；
   没有自然可达的多义 case。`identity_mismatch` 有自然可达判别（改公开身份摘要或定位）。
4. **语义键的语义范围**：公开实体引用按当前公开身份+定位比较。公开字段/集合顺序变化（内容数值、卡池位置）
   会使记录语义失效并按首差停止，这是**有意**的严格性；重录入口是 `--record-from`，不是放宽匹配。
   记录里的 `raw` 实例 ID 不参与匹配（反例 `raw_handle_is_not_an_input` 证明 raw 改掉仍唯一匹配）。
5. **依赖与协调事项**：
   - A 的商店尚未进入本固定 Host；商店与后续流程落地后，**由 Codex 协调在同一共同构建上重录 full-act 夹具**
     并跑完整第一幕冒烟，本条不等待重复重录。
   - 其它文档仍按旧用法描述 `act1_smoke.py`（不在本包写入边界，未改，供负责人安排）：
     `docs/design/mod-the-spire/base-development-guide.md:53`（示例命令缺 `--host-dll`）、
     `docs/review/tofix/dsh-public-run-driver-handoff-2026-10-11.md:43`（链接旧字面夹具）、
     `net-the-spire/client/run_game.md:162`（描述旧固定 `candidate_id`）；`docs/review/tofix/README.md:183`
     只提"公开正常流程脚本"链接，无需改。
   - `tools/base-spire-dev/README.md` 已按本包实际用法更新（语义夹具字段、`--host-dll`、记录模式、退出码、
     完整冒烟尚缺）。`tools/doc-check` 当前 7 处失败都在其它会话的文件（含 `net-the-spire/client/run_game.md:183`
     的 `.audit` 链接越出仓库根），不在本包写入边界，未改。
6. **可继续事项（在授权范围内）**：商店共同构建落地后重录 full-act 夹具；把 `--record-from` 用于其它公开轨迹
   （例如未来 full-act 录制或 `run_game.py` 的新 `frames.ndjson`）；`ai-the-spire` 侧历史 `candidate_id` 记录的
   迁移属 AI 线，本包未改。

## 维护位置

- 任务状态/认领/依赖/完成条件：主清单 [`ENV-T04`](../../tofix/README.md#env-t04)（本轮 Codex 单写）。
- 工具用法与夹具字段：`tools/base-spire-dev/README.md`；实现说明见 `act1_smoke.py`/`semantic_actions.py` 模块文档。
- 本包证据与长日志：`.audit/dsh-semantic-smoke-20261011/`（`commands-and-results.md` 为命令与返回码索引）。
- 本交接为固定交接件；负责人并入开放项后再按流程转 records，本次不自行迁移。
