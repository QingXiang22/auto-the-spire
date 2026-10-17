---
task_id: ENV-T04
dispatch_id: ENV-T04-source-rng-20261008
session_id: session-7af665cf-696b-4a18-afd4-4aa0418c9ee1
delivery_status: in_progress
updated_at: 2026-10-08T18:20:00+08:00
dispatch_record: .audit/dsh-env-t04-rng-2026-10-08/task-record.json
---

# ENV-T04 源随机包：rng-initial-shuffle 双侧入口与窄源版本派生对齐 — 2026-10-08

本包为 B 线 `rng-initial-shuffle` 原地接通**独立双侧入口**：参考侧按同一原始字符串 seed 执行它自己的运行/洗牌逻辑，
并在工具层实现登记在案的**窄源版本兼容 Profile**（只替换已确认的派生差异）。本文件在交付完成前每阶段更新；
`delivery_status` 在结束时为 `ready_for_review` 或 `blocked`。

归属：主清单 [ENV-T04](../../tofix/README.md#env-t04)「严格regression接唯一工厂和独立参考」，B 线 `tools/differential/`。
接手入口：[派单记录](../../../../.audit/dsh-env-t04-rng-2026-10-08/task-record.json)、
[ENV 实现约束 §10 与「ENV-T04初始洗牌的派生对齐边界（2026-10-08）」](../../../design/environment-implementation-constraints.md#env-t04初始洗牌的派生对齐边界2026-10-08)、
[工具 README](../../../../tools/base-migration-baseline/differential/README.md)、[源输入摘要](../../../../.audit/dsh-supervisor/review-2026-10-08-1639/rng-source-inputs.json)。
交付范围：只写 `tools/differential/`、`tools/rng-oracle/`、本交接与 `.audit/dsh-env-t04-rng-2026-10-08/`、`artifacts/environment/tests/`。
`reference/sts2-sim/`、`sts2/`、`core/`、`base/`、`net/`、`AI/`、solution 与主清单只读；core 冻结。

## 接手后先做什么

1. 先核对**已交付且本包复用、未改写**的部分：`test --suite regression`、`suites/regression.json`（15 必测分母）、
   `test-env.ps1`、`FixtureSuite` 严格注册校验、`Comparator`、`ComparatorSelfTest`、`Records`、
   `BaseBackend`（`formal-dev-factory` / `controlled-slice-v1` 双来源）。这些在
   [T04 前一包交接](dsh-env-t04-handoff-2026-10-08.md) 与
   [门槛修复交接](dsh-env-t04-gates-handoff-2026-10-08.md) 已有证据，不重做。
2. 本包第一步见下节「本包目标」；完成情况与实测证据在「验证与证据」一节按阶段追加。

## 已有成果（本包开始时的工作区事实）

- 正式 suite 分母 15 项，判定分布为 8 双侧 `passed` + 1 `self_checked`（工厂接线）+ 5 个 unsupported 原 ID +
  `rng-initial-shuffle`（`blocked_compare`），比较器自检 6/6，严格 suite rc=1（诚实红态）。
- `rng-initial-shuffle` base 侧**实际可跑**：`BaseSpireRuntimeFactory` + `RunConfiguration.Ironclad(0, dev)` →
  `CreateSession("ENV-T04-RNG-SHUFFLE-001")` → `Start()` 一次，落盘
  `artifacts/environment/tests/regression/records/base/rng-initial-shuffle.json`：start 边界只有 1 个，
  事实为玩家 80/80、能量 3、手牌 5 张、抽牌堆 5 张、弃牌/消耗 0，流为
  `base-spire:combat-draw`（draws=9）、`base-spire:run:niche`（draws=1）、`base-spire:run:up_front`（draws=18）。
- 参考侧当时无「同一字符串游戏 seed 建局并观察流布局/首次洗牌」的运行层入口，且
  `reference/sts2-sim` 检出领先 `sts2/` 冻结基线，故记 `blocked_compare`，不计通过。

## 本包目标

1. 核对是否有**可用匹配冻结参考**；不可得则在工具层实现显式、只覆盖已确认派生差异的源版本兼容 Profile。
2. 参考侧用**同一原始字符串 seed** 独立执行它自己的运行/洗牌逻辑，产出与 base 侧同构的规范化必需维度
   （actions / facts / pile-order / streams），不复制 base 记录。
3. 用**独立源向量**验证每个涉及流的 seed / 32 位回绕与抽样调用，记录前置消费及第一次洗牌/抽牌顺序与计数。
4. 纠正现有材料把参考**默认顺序路径**误述为「语义重键」的问题，区分**源码差异**与**推断**。
5. 保留原 15 项分母、7+1 双侧通过、5 个 unsupported 原 ID、正常 factory `self_checked`；不削弱维度/终点/严格非零，
   不换弱 case。若源随机缺口仍在，T04 保持待验证。

（以下各节随阶段更新。）

## 17:39负责人接收说明

实际会话 session-7af665cf-696b-4a18-afd4-4aa0418c9ee1 的完整结果取齐至 seq 719、hasMore=false，结束原因 INVALID_REQUEST。此说明由负责人追加，原执行者 delivery_status 保留；转入 records 只保存已接收的日期证据，不表示验收。实际核验与接续见[当日负责人复核](dsh-supervisor-review-2026-10-08.md#1739复核与剩余目标接续)。
报告停在初稿，updated_at=18:20 早于本轮实际时刻，不能作完成时间证据。负责人 root build 实际失败于工具的 CS0029/CS1061；派生适配及校准代码只是部分交付，原 rng-initial-shuffle 仍缺完整独立双侧。
