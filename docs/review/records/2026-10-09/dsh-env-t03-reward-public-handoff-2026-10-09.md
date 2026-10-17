# ENV-T03 公开奖励轨迹包 — 交接（in_progress）

| 字段 | 值 |
| --- | --- |
| `task_id` | ENV-T03 |
| `dispatch_id` | ENV-T03-reward-public-trajectory-20261009 |
| `session_id` | session-c1cf5cb4-4b67-4138-8b05-a32a5bf901c9 |
| `delivery_status` | `in_progress` |
| `updated_at` | 2026-10-09 01:03:14 +08:00 |
| `dispatch_record` | `.audit/dsh-env-t03-reward-public-2026-10-09/task-record.json` |

关联：主清单 [ENV-T03/T04/T05](../../tofix/README.md#env-t03)；设计 [net-the-spire 设计 §7.3](../../../design/net-the-spire/design.md#73-env-t03内的实施与完成条件)；
本包记录 [`.audit/dsh-env-t03-reward-public-2026-10-09/`](../../../../.audit/dsh-env-t03-reward-public-2026-10-09)；
前置 A `.audit/dsh-env-t05-real-rewards-2026-10-09/task-record.json`、B `.audit/dsh-env-t04-map-resume-2026-10-09/task-record.json`。

## 状态摘要

**准备期（in_progress）**。共享编译顺序为 B 地图对拍 → A 奖励闭环 → C 公开奖励轨迹；
本包 `edit_compiled_sources_after_records` / `build_after_records` 均为 [B 记录, A 记录]。
准备期实测（`dsh_client.py status --record`，rc=0）：B 会话 `session-8cf3be04-54db-46da-84c6-c97820fb95db`
与 A 会话 `session-f6bce9bc-4f1f-4d1e-9375-af382f545d5d` **均 `running: true`**（`asOfSeq` 282 / 293）。
⇒ 尚未解闸：**未**改任何参与编译的 net 生产文件，**未**构建，**未**运行最终轨迹。

## 实际改动（仅本包 audit）

| 路径 | 内容 |
| --- | --- |
| `.audit/dsh-env-t03-reward-public-2026-10-09/prep-notes.md` | 闸门实测、输入事实、本轮决定 D1–D6、解闸后步骤 |
| `.audit/dsh-env-t03-reward-public-2026-10-09/reward-client-policy.md` | 声明派生的奖励客户端策略 R1–R10 + 载荷检查同批清单 + blocked 收尾口径 |
| `.audit/dsh-env-t03-reward-public-2026-10-09/discover_reward.py` | 只读发现探针（**不是证据脚本**）：摊开真实奖励阶段的公开声明/实体种类/合法动作；`py_compile` rc=0 |

未改：`net-the-spire/**` 生产源码、`docs/contracts/net/**`、`docs/review/tofix/README.md`（主清单归负责人）、core、reference、其他线生产、`solution`。

## 源依据

- 派单 `.audit/dsh-env-t03-reward-public-2026-10-09/task.txt`；设计 `docs/design/net-the-spire/design.md:114-121`（§7.3 完成条件）、`docs/design/net-the-spire/protocol.md` §4–§7、`docs/design/agent-goals.md:37-45`（C 线合同）。
- 前置接缝 `docs/review/records/2026-10-08/dsh-env-t05-map-room-handoff-2026-10-08.md:44`（奖励阶段插在 `MapPhase=2` 之前）。
- base 现成钩子：`Composition/CombatSlicePackage.cs:324-342`（Run 账本字段）、`:179-186`（长期牌权威）、`Rooms/MapRoomWiring.cs:234-247,441-478`、`Randomness/VanillaRandomLayout.cs:45`（`Rewards` 流）。
- 既有公开证据与可复用技术：`net-the-spire/client/test_map_host.py`（710 行，5 组）、`.audit/dsh-env-t03-map-host-2026-10-08/probe_map.py`（274 行）。
- 范围身份参考 `net-the-spire/formal-dev-scopes.md:22-29`（**待更新为最终输入；旧值保留为历史，不追改**）。

## 源码 / DLL 摘要、config、seed、run_id、命令返回码

**尚未采集（闸门未开）**——本轮没有任何构建或轨迹运行，故不填写不存在的摘要与返回码。
已有历史值仅供参考：准备期主清单记 dev `ruleset 6ac4cb3cb2b696c6` / build `f291cc66cf6a9050`，
dev-map `ruleset a1d8aa9faa64b143` / build `6b25fa16e5b567f4`；这些**不是**最终值，A 落盘后需重测。
计划采集：根构建 rc、Engine/Slice/net 检查 rc 与 strict 分母、公开构建 id/ruleset/catalog/backend 摘要、
两个 seed（`t05-reward-alpha`、`t05-reward-beta`）连同旧 `t05-map-alpha/beta` 的回归、run_id 与命令返回码。

## 真实证据

**无（本轮无运行）**。计划的证据形态：真实 Host 子进程 NDJSON 全帧日志 + 规范字节 +
`docs/contracts/net/0.2/wire.schema.json` 校验结果 + C# 进程内同批检查输出。

## 首个差异

**尚未取得**（未运行）。已知并须分别核实的历史首差：`base-spire:PowerStacksOf` pure-procedure
（`Composition/CombatSlicePackage.cs:281-295`，原文复现 seed 有 IRCLAD/IRONCLAD 拼写差，两种拼写分别核实）。

## 未执行 / 未验证

- 未改 `net-the-spire/**` 生产源码、未改 `docs/contracts/net/0.2/**`、未构建、未跑公开轨迹。
- `formal-dev-scopes.md` 身份表**未**更新为最终输入（等 A 停止后按最终实测更新，旧值保留为历史）。
- `reward-client-policy.md` 中标 `【待实测】` 的项（卡牌种类 ID、奖励范围 profile、每房奖励账本字段、
  奖励阶段号）**未**由真实声明确认——这正是解闸后第一步。
- 未做 EncounterPool/Queue 的公开摘要问题（仍开放，本包不降可见性、不补造）。

## 下一步（交接口径）

1. 复测 B/A `running`；两者都为 `false` 且前置交接已结束后解闸。
2. 跑 `discover_reward.py` 复核策略分类在真实声明上成立，回填实测 ID。
3. 改 net 侧：C# 奖励检查组 + Python `client/test_reward_public.py` + 必要 schema/契约证据；
   更新 `formal-dev-scopes.md` 身份表（保留旧值为历史）。
4. 串行一次必要验证：根构建 → Engine/Slice/net → 真实 Host 公开轨迹（多 seed / 规则身份 /
   过期与非法候选拒绝 / `request_id` 重发幂等含「不得二次入账」/ C#-Python-schema 同批载荷）。
5. 本交接收在 `ready_for_review`；若 A 奖励闭环确被阻塞，则收在 `blocked` 并给出最后正常边界与精确缺口。

**父项不得据此关闭**：`accepted` / `running=false` / 绿色总数都不是关闭依据；本包只交付 C 线一个完整机制包。
