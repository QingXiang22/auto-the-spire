---
task_id: ENV-T03
task_name: C 线正常正式 Host 子包（接正式工厂并跑通 0.2 公开开局与动作）
dispatch_id: ENV-T03-formal-host-20261008
session_id: session-3139b591-cfb6-4abb-b933-de8671e50936
dispatch_record: .audit/dsh-env-t03-host-2026-10-08/task-record.json
delivery_status: in_progress
updated_at: 2026-10-08T17:05:00+08:00
---

# ENV-T03 C 线正常正式 Host 子包交接 — 2026-10-08

本包把 `net-the-spire` 的公开 Host 从**程序化冻结切片**（`CombatSlicePackage` + `SlicePublicProfile`）切到**正式 dev 工厂的正常公开路径**：由规则提供方 `BaseSpireRuntimeFactory` 交出已配置、尚未 Start 的独立会话，经 `ISessionSource` 交给 `ControlHost`，只启动一次，并在真实 stdio NDJSON 上跑通 0.2 的 `describe`/`get_catalog`/`start`/`observe`/`step`。旧 0.1 数字 seed 切片场景与既有检查保留。本包**不**关闭 ENV-T03 父项，也不代表「只改规则包新增字段/Action」的扩展证据已完成。

归属：ENV-T03「公开 Host 接唯一工厂；从规则声明派生公开出口」的 C 线正常 Host 子包（主清单 `#### ENV-T03`）。
接手入口：[主清单 ENV-T03](../../tofix/README.md#env-t03)、[net 设计 §7 实施边界](../../../design/net-the-spire/design.md#7-2026-10-07公开语义透传的源码核对与接续)、[协议 §7](../../../design/net-the-spire/protocol.md#7-env正式环境的02扩展)、[C 提示词](../../../design/agent-goals.md#5-cnet-the-spire-编码-agent)。
交付范围：负责会话 `session-3139b591-cfb6-4abb-b933-de8671e50936`；可写 `net-the-spire/**`、`docs/contracts/net/0.2/**` 及必要 net 文档、`.audit/dsh-env-t03-host-2026-10-08/**` 与本交接。`core-the-spire`、`mod-the-spire`、`tools`、`reference`、`ai-the-spire` 只读；主清单不编辑。
依赖：A 线子包「base 不可变公开语义元数据」——派单记录 [.audit/dsh-env-t03-base-metadata-2026-10-08/task-record.json](../../../../.audit/dsh-env-t03-base-metadata-2026-10-08/task-record.json)，交接路径从该记录 `handoff_path` 读取。base 不得依赖 net；Host 只做转换，不复制字段/Action 清单。
本报告记录 2026-10-08 的交付；当前排期、状态与认领以主清单为准。

## 本包剩余目标（认领时登记，逐阶段更新）

1. Host 组合层：新增 `ISessionSource` 的正式实现，包住 `BaseSpireRuntimeFactory`（构造校验、`CreateSession(每局字符串 seed)`、已配置未 Start、`Describe()` 静态身份），由 `ControlHost(ISessionSource)` 走正式路径；`Program.cs` 提供明确的正式 dev 场景开关，默认仍是 0.1 切片场景。
2. 元数据转换：把 A 线 base 侧不可变公开语义元数据（内容 ID 目录、集合用途、终局胜负、已知位置角色四类）转换为 net `PublicBuildMetadata`，交给 `DerivedPublicProfile.Build` 派生 0.2 Profile；不新增逐项 Action/字段登记，不猜名字用途。
3. 身份：`ruleset_id`/`catalog_id`/`backend_profile_id`/`scenario_profile` 与所选 source 的构建/配置/RNG Profile 一致；静态身份**不含**每局 seed；`start` 成功结果恰为 `{session_id, seed, snapshot}`，`seed` 是本次实际接受的原始游戏字符串。
4. 真实 NDJSON 证据：同一 Host 进程开两个不同字符串 seed；`describe`/`get_catalog`/`start`/`observe`/`step` 实际执行至少一条合法出牌或结束回合；不改血/清场/注入状态。
5. 兼容与保障：保留 0.1 数字 seed 旧场景与既有 stdio 检查；错误 `expected_identity`/`scenario_profile` 在创建/启动前拒绝；同 `request_id` 重发返回同一 session/seed/snapshot；observe/step 候选来自同一 Session、不泄漏隐藏状态；未支持结构明确拒绝或声明。
6. 同批更新 C#/Python/schema 与载荷证据（0.2 相关差异）；在最终输入上记录 build/源码摘要、config/seed/run_id。

## 接手后先做什么

1. 先核对主清单 ENV-T03 的认领与工作区：C 线文件已改（见下），A 线 base 元数据已在 `mod-the-spire/base-spire/BaseSpire/SinglePlayer/BasePublicSemantics.cs` 落地。**不要重做**已交付部分。
2. 在仓库根目录按顺序构建（先确认 A/B 会话已停止，不同时构建共享 `obj`/`bin`）：
   `dotnet build AutoTheSpire.sln`，然后 `dotnet run --project net-the-spire/Net.TheSpire.Tests`（期望包含新增 6 条 `F-*` 检查）。
3. 真实 NDJSON 证据（C# 宿主 + Python 客户端两端）：
   `python -X utf8 net-the-spire/client/test_client.py --only formal`
   与 `python -X utf8 net-the-spire/client/test_client.py`（旧 0.1 组必须仍然全绿）。
4. 需要手工核对时直接起宿主：`dotnet run --project net-the-spire/Net.TheSpire.Host -- --scenario formal-dev --game-seed ENV-IRONCLAD-00`，按 UTF-8 NDJSON 逐行收发。
5. 本包**未**交付的部分写在「差异、阻塞与未验证」；不要据本包自判 ENV-T03/M01 验收。

## 已交付与接口变化

（待交付推进后填写）

## 验证与证据

（待交付推进后填写）

## 差异、阻塞与未验证

（待交付推进后填写）

## 维护位置

- 状态与认领：主清单 ENV-T03 条目（负责人维护，本包不编辑）。
- 派单记录：`.audit/dsh-env-t03-host-2026-10-08/task-record.json`。
- 依赖派单记录：`.audit/dsh-env-t03-base-metadata-2026-10-08/task-record.json`。
- 后续证据：本文件；长日志与 NDJSON 轨迹放 `.audit/dsh-env-t03-host-2026-10-08/artifacts/`。

## 17:39负责人接收说明

实际会话 session-3139b591-cfb6-4abb-b933-de8671e50936 的完整结果取齐至 seq 871、hasMore=false，结束原因 INVALID_REQUEST。此说明由负责人追加，原执行者 delivery_status 保留；转入 records 只保存已接收的日期证据，不表示验收。实际核验与接续见[当日负责人复核](dsh-supervisor-review-2026-10-08.md#1739复核与剩余目标接续)。
原 dispatch_id 错填 request_id `d8d8afc5-4a3e-492b-80a4-fa83c9d0791c`；按原派单记录的 task_id 更正，task/session 身份未变。
交接正文未填完即 API 中断。当前 Host/消费者代码已由负责人实际 net399/Python103 检查通过；公开集合语义缺口与完整交接仍待补，旧 NDJSON 的 DLL 摘要不能覆盖其后修改。
