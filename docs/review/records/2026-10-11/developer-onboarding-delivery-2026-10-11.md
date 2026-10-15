# 房间与公开客户端开发入口交付，2026-10-11

本包让房间作者通过明确的Run符号与访问流程接口编译规则，让客户端作者通过共用公开快照访问函数提交当前候选。当前状态和剩余验收只维护[主清单T09](../../tofix/README.md#env-t09)与[T03](../../tofix/README.md#env-t03)。用户要求不做token统计，本次未新增这类统计。

## 接手第一步

房间包从[房间操作指南](../../../design/mod-the-spire/how-to-add-a-room.md)运行`BaseRoomExample`，再查[编译接口参考](../../../design/mod-the-spire/room-compile-reference.md)和本包原版方法。客户端包从[公开运行教程](../../../design/net-the-spire/how-to-run-public-game.md)运行`first_action.py`，再查[客户端参考](../../../design/net-the-spire/public-client-reference.md)。主清单仍是任务入口，图只导航，不要求预读历史报告或全部组包器/Host。

## 实际能力与边界

- 新增`RoomCompileContext`，统一Run类型/集合、长期生命/金币/牌组、地图符号、`CurrentRun`、`At`、`CloseVisit`。不持Session或可变C#状态。正式组包已让营火和宝箱消费它；奖励/Boss幕完成与地图进入仍由原主责拥有。
- 营火`Register`和宝箱构造改为接收上下文，旧参数形式需迁移。具体签名与状态归属只维护接口参考。遗物/药水/卡牌配方继续显式依赖，不新增服务定位器。
- 提取`public_snapshot.py`，运行器复用实体、字段、序列、可见牌堆、关系访问。短名适用于所选base范围；种类/集合/关系支持完整公开ID。传输恢复/Host/协议没有修改。
- 两个独立可执行例子分别验证Engine内房间动作及真实公开启动/提交；两条路径都有局部命令、接口参考和小范围Mermaid关系图。总入口、base指南、角色提示词、client入口和根AGENTS已导航到它们。
- 未改商店隔离生产目录、初始化内容/随机算法、core、参考或AI。初始化会话查询为停止seq769、无固定交接；末窗口为`turn/end aborted(user)`。运行器会话停止seq1009且已交固定交接；商店查询时仍运行seq907。停止和自报均不等于验收。

## 实际验证

隔离构建输出为`.artifacts/parallel/codex-developer-guides/`。完整命令/退出码及日志见[验证记录](../../../../.audit/developer-onboarding-2026-10-11/verification.json)。

| 项目 | 本次实际结果 |
| --- | --- |
| `dotnet build AutoTheSpire.sln --artifacts-path .artifacts/parallel/codex-developer-guides` | 0错误，1处既有nullable警告 |
| Engine完整检查 | 99通过，退出0 |
| Slice完整检查 | 未通过；首差为正式队列应15项、实30项，异常退出 |
| 营火局部 | 2通过，退出0；受控边界 |
| 宝箱局部 | 3通过，退出0；受控库存/稀有度/领取边界 |
| net完整检查 | 416执行，415通过、1失败；F-05“期望错误响应” |
| 房间例子 | `Gold 10→17`、`MapPhase 5→2`、剩余合法动作0，退出0 |
| 最小公开客户端 | 旧不可变Host与本次Host各正常启动/提交3步；均退出3（预期步数停止） |
| 本次Host整局运行器有限前缀 | 正常6步，含真实`net.answer_decision`，退出3；不计整局通关 |
| 共用访问提取对照 | 既有seed-K的315步原始帧重新计算策略，所有candidate/observation ID一致；[对照摘要](../../../../.audit/developer-onboarding-2026-10-11/decision-replay.json) |
| Python语法与文档 | 三个Python文件语法通过；doc-check仍报两份旧交接的4处`#L`锚点错误，本包链接未报错；diff检查通过 |

Slice首差对应T08补丁把15个精英槽追加到普通`EncounterQueue`的实现，而旧普通消费者/检查按15项解释。该失败使用默认dev范围，不声明本包RoomCompileContext。net F-05同样使用默认dev范围，检查跨会话/过期绑定的拒绝；未执行Python访问模块。此处仅定位受阻范围，未修初始化机制或放宽检查；net首差的具体请求分支仍待T03复核。

本次Host已使用工作区初始化补丁，与旧冻结Host不同；教程在两种输入上实际执行，不把旧完整轨迹升级成新输入的完整证据。没有重复全跑严格对拍或宣称整局一致性。

## 未验证与后续

本次完成了开发入口、真实消费者与局部行为复核。完整Slice/net门槛保持失败记录，真实房间族与整局仍开放。下一正式房间集成使用本上下文，额外资源接缝由负责人先核对准确调用再派单；商店库存/离店仍需正式接线，奖励和跨幕流程没有被本次全面抽出。

接手路径已按正文和直接链接自查，尚无独立开发者的接手验证。未声称检索量或开发速度已获得量化改善。当前本包无继续编码小项，不续派新agent、不恢复AI。
