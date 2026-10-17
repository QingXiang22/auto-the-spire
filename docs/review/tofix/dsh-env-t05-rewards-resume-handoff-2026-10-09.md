---
task_id: ENV-T05
dispatch_id: ENV-T05-real-rewards-resume-20261009
session_id: session-b1e2bcc9-987b-4b03-b3c8-46e98df03480
dispatch_record: .audit/dsh-env-t05-rewards-resume-2026-10-09/task-record.json
delivery_status: in_progress
updated_at: 2026-10-09T13:10:00+08:00
---

# ENV-T05 真实奖励闭环（接续）— 交接（in_progress）

关联：主清单 [ENV-T05](README.md#env-t05)（父项，**由负责人维护，本包未编辑**）；
设计 [环境完成与对拍范围](../../design/environment-completion-and-differential-v1.md)、
[实现约束](../../design/environment-implementation-constraints.md)；
原 A 设计（本包复用并修正）[`.audit/dsh-env-t05-real-rewards-2026-10-09/reward-design-2026-10-09.md`](../../../.audit/dsh-env-t05-real-rewards-2026-10-09/reward-design-2026-10-09.md)；
本包证据 [`.audit/dsh-env-t05-rewards-resume-2026-10-09/`](../../../.audit/dsh-env-t05-rewards-resume-2026-10-09/task-record.json)（`task.txt`、`task-record.json`）；
前置 B `.audit/dsh-env-t04-map-resume-2026-10-09/task-record.json`（`delivery_status=ready_for_review`、实测 `running=false`；其 `handoff_path` 已由负责人移入 `docs/review/records/2026-10-09/`）。

**状态**：本交接在工作开始时即建立（派单要求固定 YAML 头），当前为 `in_progress`；收尾时改为
`ready_for_review` 或 `blocked` 并补齐命令、返回码、证据与首个差异。**未完成前不得据此关闭任何父项。**

## 接手后先做什么

1. **复测编译门**（带 `--record`）：
   `python C:\Users\26877\.codex\skills\dsh-remote-operator\scripts\dsh_client.py status --record .audit/dsh-env-t04-map-resume-2026-10-09/task-record.json`
   要求 `sessions[].running == false`。**本包开工时已实测为 `false`**（2026-10-09 13:02 +08:00），
   且该记录 `delivery_status=ready_for_review`、其 `handoff_path` 指向的交接已由负责人移入
   `docs/review/records/2026-10-09/`（**不再以 tofix 旧路径是否存在为条件**）⇒ 共享编译顺序中的 B 门已开，
   A 可实施；C（公开奖励轨迹）在本包实际交付后接。
2. 读本节下方「已交付/改动」与「源依据」两节，再按需查 `.audit/dsh-env-t05-rewards-resume-2026-10-09/`。

（以下各节在本包完成时补全：已交付能力、命令与返回码、真实轨迹证据、首个差异与未验证范围、下一已就绪任务。）
