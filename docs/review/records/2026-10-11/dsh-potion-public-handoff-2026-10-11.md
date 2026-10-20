# ENV-T03 公开药水交接 — 2026-10-11

> 2026-10-19 整理：切换前历史证据：旧 C# 路径和会话已经退出当前生产；保留当时交付与未验收范围，不按本文继续派单。 当前任务见[主清单](../../tofix/README.md)，后续架构见[内容与行为重构目标](../../../design/mod-the-spire/content-behavior-refactor-goals.md)。


- task_id: `ENV-T03-potion-public-20261011`
- dispatch_record: [.audit/dsh-potion-public-2026-10-11/task-record.json](../../../../.audit/dsh-potion-public-2026-10-11/task-record.json)
- session_id: `session-9f30503c-6ece-4270-b179-c8cb24534f47`
- request_id: `d4f3298d-12a1-4696-8d5e-09a112b8a5cd`
- delivery_status: `待验证；本包执行结果见下方最终汇总，主清单由 Codex 单写`
- updated_at: `2026-10-11T03:02:17+08:00`

接手第一步：读取[最终实际汇总](../../../../.audit/dsh-potion-public-2026-10-11/final.summary.json)及[原始帧](../../../../.audit/dsh-potion-public-2026-10-11/final.frames.jsonl)，逐组核验 `status/rc/assertions/steps/branches`。审阅通过后由 Codex 更新[主清单 T03](../../tofix/README.md#env-t03)。执行者交付后停止，不续派、不提交。

本包新增[药水 Python 检查](../../../../net-the-spire/client/test_potion_host.py)与[测试使用说明](../../../../net-the-spire/client/README.md)。标准 NetClient、Host/C#、base/core/tools/reference/sts2/AI、MapRoomWiring、公共 Boss 回放数据及主清单均未修改。检查走 describe/catalog/start/observe/公开候选/step，按运行期声明读取内容和容量。没有生产客户端缺口或跨模块修复。

## Host 与来源

使用[不可变 Host DLL](../../../../.artifacts/parallel/owner-potion-flow/author-final/bin/Net.TheSpire.Host/debug/Net.TheSpire.Host.dll)，通过 `NET_THE_SPIRE_HOST_DLL` 绑定。SHA256：`d4f21287da9946a51dbcbd2e004e8f2d81e2bb7c19e8f66b6388836f3a0aa6d5`。没有构建或写入该目录。

正式 describe 身份：protocol `0.2`；ruleset `net:ruleset:1f29fde3c9a9ab3a`；catalog `net:catalog:b400d7f332f590cc`；backend `net:backend:simulation-0.2-350911d86bad900d`。运行期 catalog 实际 Potion 内容为 48 项；11 使用程序是[负责人 frozen 构建专项证据](../../../../.audit/owner-potion-flow-2026-10-11/potion-checks-final.log)的基线，不是本包从当前 A 源码推导或测得的程序覆盖数。

正常场景 seed `ACT1-BASIC-10`，profile `base-spire-singleplayer-dev-map-v1`；[负责人公开轨迹](../../../../.audit/owner-potion-flow-2026-10-11/public-replay-author-final/public-trajectory.jsonl)身份逐字核对，按公开 instruction action/fields 在实时合法候选中匹配，提交实际 candidate_id。最长只到第 208 步满栏边界，不把完整 Boss 通关作为前置，不搜索/训练，不读取隐藏状态/随机数/内部 handle，不注入初始状态。

受控 `target` 是既有作者文本场景：protocol `0.1`；ruleset/catalog `net:ruleset:base-spire-author-content-v3` / `net:catalog:base-spire-author-content-v3`；backend `net:backend:generic-engine-runtime-v1`；profile `base-spire-combat-slice-v1`；seed `1`。真实持有 beetle-juice，公开目标选择暂停。该证据与 formal 正常获得分列；不声称正式敌人目标瓶正常掉落已覆盖。

## 分支与帧证据

[最终汇总](../../../../.audit/dsh-potion-public-2026-10-11/final.summary.json)的每组 `branches[].through_frame` 定位[原始帧](../../../../.audit/dsh-potion-public-2026-10-11/final.frames.jsonl)的同组 `frame_number`；每行保留方向及发送/接收的 `raw_text`。帧号从每组开始独立计数。所有普通帧检查没有 MapDraws；正式请求及响应过既有 0.2 schema，受控作者 0.1 请求及响应过既有 0.1 schema，未伪造协议版本。

| 组 | 能力及拒绝证据 |
| --- | --- |
| catalog | 实际 describe/catalog/start/observe、48 项 Potion 与 PotionBelt/RewardPotions/动作/字段声明；零内容报失败 |
| normal | 同 offered 实例领取到第一空槽；真实 soldiers-stew `UseSupported=false` 且具名原因，没有 Use 候选、仍可丢弃；Dexterity 使用销毁瓶、不付能量，其他实例槽号不变；领取/使用/丢弃同 request_id 响应一致且观察无额外变化；旧 observation 与消费 candidate 拒绝，拒绝前后完整 snapshot 相等 |
| full | 正常前缀填满运行期容量，仍保留 reward；Take 无合法候选，对非候选 token 拒绝且快照不变；显式丢弃真实持有瓶后保留 reward，同实例领取回第一空槽；discard/take 去重及拒绝 |
| skip | 正常首次奖励跳过关闭 RewardPotions 及 Take/Skip 候选，持有栏不变；skip 去重及拒绝 |
| target | 作者受控敌人目标使用：pending 前后瓶保留且字段不变，回答完成才销毁，其他瓶槽号不变，Power 有公开变化，无能量付款；Use/Answer 去重及旧候选拒绝 |

目标选择初次 pending 保持作者场景 observation_id `363`，旧 Use 候选被替换，以 `illegal_candidate` 拒绝；不把它误报为 observation 已变化的 `stale_observation`。真正 observation 变化时，脚本要求 `stale_observation`。满栏 Take 与未支持 Use 没有可提交的公开 action payload，负例提交非候选 token；没有另造 RPC 或注册逐瓶特例。

## 实际命令与结果

最终完整包命令（DLL 环境变量如上）：

```powershell
python -X utf8 -u net-the-spire/client/test_potion_host.py --log .audit/dsh-potion-public-2026-10-11/final.frames.jsonl --summary .audit/dsh-potion-public-2026-10-11/final.summary.json
```

最终完整包实际 rc0，五组全通过，共 2346 个断言、782 个收发帧、301 次合法步骤；每个 Host 退出码均为 0。

| 组 | seed | 合法步骤 | 断言 | 收发帧 |
| --- | --- | ---: | ---: | ---: |
| catalog | ACT1-BASIC-10 | 0（入口检查） | 32 | 8 |
| normal | ACT1-BASIC-10 | 63 | 562 | 188 |
| full | ACT1-BASIC-10 | 208 | 1386 | 462 |
| skip | ACT1-BASIC-10 | 28 | 237 | 80 |
| target（作者受控 0.1） | 1 | 2 | 129 | 44 |

这些是本包实际计数，逐分支帧位置以[最终实际汇总](../../../../.audit/dsh-potion-public-2026-10-11/final.summary.json)为准。单组定位用 `--only normal` / `--only full` / `--only skip` / `--only target` / `--only catalog`。新构建用 `--trace` 提供相应公开轨迹，必须保留实际身份校验；内容数量与 catalog/ruleset hash 不写死。

开发期 `--only normal` 实际 rc0、562 断言、63 合法步骤，见[开发期汇总](../../../../.audit/dsh-potion-public-2026-10-11/normal-dev.summary.json)。首版错误地复用不同领取的 request_id 导致 `request_conflict`，已修为每个操作唯一 ID；原错误帧留[早期记录](../../../../.audit/dsh-potion-public-2026-10-11/potion-public-host.log)。修正版首次完整组 rc1：目录选择写错 `content_entries` 与作者 pending 错误期待 stale；normal/full/skip 通过，catalog/target 失败，见[保留失败汇总](../../../../.audit/dsh-potion-public-2026-10-11/summary.json)。两处是检查脚本问题，已改用 runtime `content_types/kind_ids` 和实际 observation 边界，不修改 Host 规则。

文档检查首次 `python -X utf8 tools/doc-check/check_docs.py` 实际 rc0，376 份 Markdown、0 问题。交接落盘后的最终仓库检查实际 rc1，扫描380份，只有并行的[Power fixture交接](dsh-power-fixture-handoff-2026-10-11.md)两处缺失锚点（第21行 `../../../tools/differential/README.md#L7`；第25行 `../../design/environment-implementation-constraints.md#L181`），不跨边界修改。用同一 `check_docs.check_file` 单独验证本包两份文档实际 rc0、0问题；原错误及命令结果记入[验证记录](../../../../.audit/dsh-potion-public-2026-10-11/validation.json)。`git diff --check` 实际 rc0，只有已有文件换行警告。没有重复 Engine/Slice/net 全量检查；生产客户端未改，无受影响现有 Python 客户端回归要求。

## 未覆盖与后续

- 正常 formal 掉落敌人目标瓶未覆盖；目标决策仅有作者受控补证且协议为 0.1。不会把它计入正常正式 0.2 分母。
- 本包不是 48 项使用效果全覆盖；11 程序基线属负责人已有证据。剩余效果、完整角色/房型/幕、独立原版双侧对拍未验证。
- 本包零步 catalog 只代表入口声明检查成功，不能计成药水流程执行；normal/full/skip/target 必须真实步骤及对应分支都通过。历史 C213 不纳入实测。
- 如新 DLL 身份与旧轨迹不一致，应先由负责人提供该构建正常公开前缀，再运行 `--trace`；身份不匹配、missing branch、Host 错误一律失败，不能删除身份断言绕过。
