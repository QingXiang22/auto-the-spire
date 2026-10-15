---
task_id: ENV-T03
dispatch_id: ENV-T03-public-event-policy-20261013
session_id: session-a762b7b0-79e5-4d34-b331-23a88514f140
delivery_status: delivered_pending_owner_review
delivered_at: 2026-10-13T11:26:25+08:00
timezone: Asia/Shanghai
dispatch_record: .audit/dsh-public-event-policy-20261013/task-record.json
---

# ENV-T03 公开事件策略消费者交接 — 2026-10-13

正式Python运行器现可提交问号/精英当前候选、选择普通事件选项，并在事件关闭后继续商店和战斗。固定Host最终72次成功提交，第73次Elite进入被Byrdonis内容缺失具名拒绝；不是整局通过，父ENV-T03/M01未关闭。

任务与有效状态查[主清单ENV-T03](../../tofix/README.md#env-t03)。本包边界与完成条件见[短工作包](../../../../.audit/dsh-public-event-policy-20261013/task.txt)，实际会话及唯一交接见[派发记录](../../../../.audit/dsh-public-event-policy-20261013/task-record.json)。主清单由Codex单写，本会话未改。

## 接手第一步与验收权限

1. 负责人先核对本包[最终运行摘要](../../../../.audit/dsh-public-event-policy-20261013/runs/seed-L/summary.json)和[步骤](../../../../.audit/dsh-public-event-policy-20261013/runs/seed-L/steps.ndjson)，确认72次成功及第73次当前Elite候选拒绝，不把rc4记作通关。
2. 在仓库根运行 `python -X utf8 net-the-spire/client/test_event_public.py`，再运行 `python -X utf8 net-the-spire/client/test_shop_public.py --only replay --run-dir .audit/dsh-public-event-policy-20261013/runs/seed-L`，预期全部判别通过，73次提交与记录逐字一致。不要重做已交通用事件策略。
3. A的新精英Host集成与ENV-T10幕转换由负责人裁定、另派下一包。没有授权就不改固定DLL、不构建.NET或替换为默认Host。本消费者包不自认下一包，不关闭父项；完整游戏规则、阶段门控和整局结论均属Host。

## 已交实现与接口变化

生产改动仅以下文件：

- [run_game.py](../../../../net-the-spire/client/run_game.py)：Unknown/Elite加入客户端可消费地图类型（不是内容完整声明）；增加 `--event-choice`、事件进入/选项/退出证据与 `host_fault_attribution`。Host拒绝保持 `fault_host_rejected`、rc4，保留原错误和公开节点类型，与客户端 `unsupported_map_room` 区分。
- [event_policy.py](../../../../net-the-spire/client/event_policy.py)：只读当前 `Run.MapPhase=8` 与非空 `CurrentEventId`，不按事件ID或动作名前缀映射。默认按完整action_type、规范公开字段JSON、发布順序排序。除明确不主动执行的完整公开DiscardPotion外，未知选项全部保留；已知地图/商店/战斗动作混入时具名停止。显式选择完整ID精确匹配或短名唯一后缀；缺失/歧义非零停止、不选替代项。
- [test_event_public.py](../../../../net-the-spire/client/test_event_public.py)：复用商店/map/decision公开夹具，11项判别覆盖不同事件ID/不带事件前缀的选项、未知选项不被过滤、当前候选更换、缺失/歧义选择、异常跨房间动作隔离、事件关闭后不滞留、decision通用路径、Unknown/Elite/Ancient边界、商店跨访问状态和幕完成边界。
- [test_shop_public.py](../../../../net-the-spire/client/test_shop_public.py)：记录event_choice后重建策略；按每次商店访问验证原有货架/买/删/离店断言；复算最终无snapshot的拒绝提交，不再漏最后一步。
- [运行器说明](../../../../net-the-spire/client/run_game.md)：参数/策略/真实证据/限制。未修改共享public_snapshot、net_client、C#Host/schema/协议、base/core/reference、tools/base-spire-dev、B的tools/differential或共享清单。

### 实际发现并修复的消费者缺陷

首轮正式运行70次成功后被Elite拒绝，但既有复算器两项断言失败：它假设整局仅一次商店访问。改为逐次访问后仍有一项失败，定位到运行器的 `shop_bought` 标记跨访问泄漏，第二店未买。现在离店时清买/删/pending访问标记，并保留判别；相同固定Host、seed及CLI重跑后第二店真实购买，最终72次成功。没有降低购买断言、换seed或改状态。

修复前原始轨迹保存在[修复前运行摘要](../../../../.audit/dsh-public-event-policy-20261013/runs/seed-L-before-shop-visit-reset/summary.json)；[初次复算失败](../../../../.audit/dsh-public-event-policy-20261013/replay.log)和[进一步定位日志](../../../../.audit/dsh-public-event-policy-20261013/replay-final.log)均保留，不覆盖他人证据。

## 固定公开边界与真实证据

闭包来自[固定SHA清单](../../../../.audit/dsh-wave-20261013/fixed-closure-sha256.json)，8个Host闭包文件逐项匹配见[闭包检查](../../../../.audit/dsh-public-event-policy-20261013/fixed-host-check.json)。Host SHA256 `A296C2672305B8C96092D8273464B761D98930CFA93454D2A7BC2D6440483B75`。

公开身份：protocol `0.2`；ruleset `net:ruleset:18347a3ee9e37d37`；catalog `net:catalog:5e415196b73a8b21`；backend `net:backend:simulation-0.2-ac5d3041995c12da`；profile `base-spire-singleplayer-dev-map-v1`。实际游戏session和接受seed写在各摘要中，候选身份仅用于本次会话，不沿用旧记录。

主运行命令（工作目录仓库根）：

```powershell
python -X utf8 -u net-the-spire/client/run_game.py --host-dll F:/Scylier-Project/active/auto-the-spire/.artifacts/parallel/owner-dsh-wave-20261013/host/Net.TheSpire.Host.dll --scope dev-map --seed ENV-T03-SEED-L --max-actions 90 --use-potions --shop-room auto --shop-policy buy --shop-buy-kind card --shop-remove --event-choice SunkenStatueDiveIntoWater --output .audit/dsh-public-event-policy-20261013/runs/seed-L
```

- 第46步当前问号节点进入事件：原MapNode.Type仍Unknown，公开ActualRoomType=Event、CurrentEventId=SunkenStatue、MapPhase=8。
- 第47步当前选项提交：Gold73→179（+106）、HP67→60（-7）、MapPhase8→2、CurrentEventId清空。
- 第48步进下一商店；第49步BuyShopCard，第50步RemoveShopCard，第51步通用decision，第52步LeaveShop；第53步第三场战斗，第70–72步正常奖励。
- 第73次提交来自当前公开候选的Elite节点e23，Host错误 `backend_fault`，原消息 `Source encounter base-spire:byrdonis-elite cannot execute: missing enemy behavior base-spire:byrdonis`。execution_state=completed、retry_mode=stop；不盲目重发，不声称失败回滚。摘要保留最后成功快照，完整原错误在帧与拒绝步骤中。
- 实际rc4、step_count=72、run_complete=false、close_ok=true、无Victory。原始[frames](../../../../.audit/dsh-public-event-policy-20261013/runs/seed-L/frames.ndjson)、[steps](../../../../.audit/dsh-public-event-policy-20261013/runs/seed-L/steps.ndjson)、[summary](../../../../.audit/dsh-public-event-policy-20261013/runs/seed-L/summary.json)完整保留。

同命令仅改 `--event-choice GrabSword` 和输出到 `runs/seed-L-grab-sword`：46次成功后第47次提交由Host以SwordOfStone获得与效果未实现具名拒绝，rc4、run_complete=false、close_ok=true；见[摘要](../../../../.audit/dsh-public-event-policy-20261013/runs/seed-L-grab-sword/summary.json)、[原始帧](../../../../.audit/dsh-public-event-policy-20261013/runs/seed-L-grab-sword/frames.ndjson)、[步骤](../../../../.audit/dsh-public-event-policy-20261013/runs/seed-L-grab-sword/steps.ndjson)。未替换事件、未隐式选择DiveIntoWater。

## 检查与未执行

所有命令从仓库根执行，Host相关Python检查设置 `NET_THE_SPIRE_HOST_DLL` 为上述固定Host；没有构建.NET。

| 检查 | 命令 | 实际结果与证据 |
| --- | --- | --- |
| 事件/访问隔离 | `python -X utf8 net-the-spire/client/test_event_public.py` | 11项通过、rc0；[日志](../../../../.audit/dsh-public-event-policy-20261013/event-final2.log) |
| 商店/decision/map策略 | `python -X utf8 net-the-spire/client/test_shop_public.py --only policy`；分别换decision/map | 21/11/7项通过、rc0；[policy](../../../../.audit/dsh-public-event-policy-20261013/shop-policy.log)、[decision](../../../../.audit/dsh-public-event-policy-20261013/shop-decision.log)、[map](../../../../.audit/dsh-public-event-policy-20261013/shop-map.log)，访问标记修复后同组三项复跑通过 |
| 当前公开候选复算 | `python -X utf8 net-the-spire/client/test_shop_public.py --only replay --run-dir .audit/dsh-public-event-policy-20261013/runs/seed-L` | rc0、73次提交逐字一致，含最终拒绝；[日志](../../../../.audit/dsh-public-event-policy-20261013/replay-final2.log) |
| 固定Host客户端组 | `python -X utf8 -u net-the-spire/client/test_client.py` | 103 passed、rc0；[日志](../../../../.audit/dsh-public-event-policy-20261013/client-fixed.log) |
| 固定Host地图组 | `python -X utf8 -u net-the-spire/client/test_map_host.py --log .audit/dsh-public-event-policy-20261013/map-frames.ndjson` | 213 passed、rc0；[日志](../../../../.audit/dsh-public-event-policy-20261013/map-fixed.log)、[原始帧](../../../../.audit/dsh-public-event-policy-20261013/map-frames.ndjson) |

| 文档/语法/空白 | `python -X utf8 tools/doc-check/check_docs.py`；`git diff --check`；`python -X utf8 -m py_compile net-the-spire/client/run_game.py net-the-spire/client/event_policy.py net-the-spire/client/test_event_public.py net-the-spire/client/test_shop_public.py` | 均rc0；[结果](../../../../.audit/dsh-public-event-policy-20261013/final-check-results.json)、[文档日志](../../../../.audit/dsh-public-event-policy-20261013/doc-check-final.log)、[空白日志](../../../../.audit/dsh-public-event-policy-20261013/diff-check.log)。客户端原本整体未跟踪，因此git diff --check不覆盖新文件；另做本包五文件直接空白检查 |

公开轨迹复算不是独立原版规则对拍，合成判别不是实地游戏规则验证。未执行solution/Engine/Slice/C#net构建或全.NET测试、训练、搜索换seed、A新精英Host集成、真实幕转换及整局胜利。幕完成和step-frontier仅合成消费者判别，未宣称实地到达。

## 准确接口限制与文档自查

公开wire没有“选项归属当前事件”元数据，ChoiceEventModule也未强制动作名以事件ID开头。因此本包不使用前缀猜测，信任Host对当前事件的合法动作守卫；独立客户端无法识别被错误发布的任意未知外事件选项。已知跨房间候选混入可以判别，未知选项不会被静默过滤。若要求一般mod的独立选项归属验证，需负责人另裁公开元数据接口；本包未改Host/schema补特例。

Diátaxis复核：运行器说明未显式声明类型，实际以参数/策略/输出查阅为主，归Reference；已有历史证据混入，影响快速查阅，本包仅用带日期小节明确范围，不迁移他人历史记录。交接属于项目交付证据，沿用handoff模板。技术验证与风格分类分开结算。

接手自查已覆盖目标/唯一状态入口、可复用实现、第一步、原完成条件及负责人权限、实际公开边界与未执行、唯一维护位置。不是独立读者验证。本包交付待负责人复核，不另造当前状态表。
