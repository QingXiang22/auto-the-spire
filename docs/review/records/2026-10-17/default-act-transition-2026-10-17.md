# 默认三幕计划与跨幕生命周期交付

本轮继续[Ironclad整局主线](../../tofix/README.md#env-t10)，交付显式默认三幕计划、开局预生成和真实切幕命令。接手时复用`dev-run`与`act-transition`检查，继续补正常流程遇到的事件/古代人/敌人，而不是重做初始化或另拆逐例任务。当前单幕原路线的召唤暂停继续有效，完整首幕胜利和四幕胜负尚未验收。

负责人Codex `/root`在主清单登记本地接续旧T10任务。旧DSH会话上次已确认停止，本轮只读查询仍未建立登录会话，也没有固定交接可验收；没有恢复旧会话、重发远端任务或修改派发记录。共享工作区原有未提交成果保留，没有提交Git、修改core或参考玩法。

## 能力交付与接续边界

新增显式`dev-run`，范围身份为`base-spire-singleplayer-dev-run-v1`，只接受Ironclad/A0/all-unlock/singleplayer及v0.109随机/内容元数据。`dev`、`dev-map`继续使用单幕配置，其默认规则和构建身份没有迁移；完整Ironclad/full身份没有开放。

默认地图幕序列是Overgrowth→Hive→Glory，之后是TheArchitect最终事件。原完整四幕胜负合同保留，不能把三个地图幕结束发布为整局Victory。

- Start时一次预生成所有幕：先分配共享古代人子集，再按源顺序生成每幕事件、普通/精英队列、Boss和古代人。完整身份池不按可执行性缩小。
- 首两次合法ContinueToNextAct启用下一幕预生成库存，重建对应地图、重置本幕访问/问号赔率；不重洗遗物袋、不重复抽UpFront。
- HP/MaxHp/Gold、永久卡/遗物/药水真实实例和空槽保留；CurrentRoom/SettledRoom继续作为整局结算键。旧地图、临时牌、存活/死亡怪物与能力、战斗历史/球及战斗级资源清理。
- 各幕地图使用自己的随机流和源分布，TotalFloor跨幕累计，遭遇派生按累计楼层种流；奖励升级概率读取实际幕索引。
- 下一幕先提供实际古代人起点，效果未实现时按身份明确失败；不能直接跳到普通房间。第三幕后仍明确停在未实现的TheArchitect。

新版幕数据只拥有版本化身份/标签；已有编队从base-spire.encounters.json复用，不维护第二份可编辑出生程序。完整方法与状态归属见[跨幕生命周期契约](../../../design/mod-the-spire/act-transitions.md)。生产主要修改base的SinglePlayer、RunActWiring、Map/Encounter/NewRun/Room编排与共享地图计算，Host只注册scope，客户端按公开幕索引/数量消费当前ContinueToNextAct。游戏规则与运行状态仍在Engine，不引入另一套C# RunState。

## 独立参考验证

`tools/differential/ActTransitionChecks.cs`正常创建正式dev-run Session和独立原生`RunState(Overgrowth,Hive,Glory)`，使用原生初始化与随机数。参考私有预生成库存仅通过只读诊断读取，不用base结果构造参考库存，不修改参考源码或随机流。

三组固定seed分别逐幕核对：完整事件/普通/精英队列、Boss和古代人、地图节点/类型/连边，以及地图流消费和最终指纹。全部通过：

| seed | 开局UpFront（双侧一致） | Overgrowth地图抽数 | Hive地图抽数 | Glory地图抽数 |
| --- | --- | --- | --- | --- |
| ENV-T04-RNG-SHUFFLE-001 | 415 | 276 | 267 | 232 |
| ACT1-BASIC-10 | 415 | 270 | 251 | 281 |
| ENV-T03-SEED-L | 418 | 276 | 248 | 223 |

首轮差异来自版本化Overgrowth标签未带入多幕数据；补入与既有v0.109迁移相同的Nibbits/SlitheringStrangler/SnappingJaxfruit元数据后，完整UpFront消费和逐幕库存对齐。静态身份/标签来源在版本化数据中明示，不能称为对这些静态身份的独立审计；随机选择、队列生成和地图仍由双方各自执行。未改原参考或现有单幕基线来适配新路径。

切幕机制检查显式预置双方已访问节点，并在base预置幕结束、战斗残留和两个有空隙的库存瓶，再提交真实ContinueToNextAct。结果标记`controlledTransition=true, normalVictoryEvidence=false`：

1. 奖励未关闭或未到幕完成时提交被拒，状态和随机数不变。
2. 仅前进一步幕索引，旧地图节点销毁，HP/MaxHp/Gold、永久牌/遗物/药水实例、槽位0/2及全局结算键保留。
3. 本幕访问计数与战斗级资源复位，死亡怪物、Power和临时战斗牌不残留。
4. 预生成库存与UpFront不变；九张地图均与原生参考同节点/连边/随机指纹。
5. 第三幕后明确拒绝TheArchitect缺口，不发布整局Victory。

这证明默认计划与切幕机制，不是正常赢下Boss或完整四幕对拍。后续幕源事件资格/古代人效果尚缺时仍明确失败，不套第一幕资格。

## 公开Host与正常Run

新增通用多幕客户端判别：公开CurrentActIndex/ActCount表明仍有下一幕时消费当前ContinueToNextAct；计划末尾仍保留开发边界。12项Python事件/边界检查通过。

真实ControlHost检查受控幕结束后的公开切幕：进入Hive、保留37HP/234金币和库存实例、旧地图句柄退出、新地图候选先为Ancient、请求重放逐字相同、旧候选拒绝；尝试古代人得到明确缺失效果错误。该检查标记受控，不伪称无注入通关。

此外从全新Host进程正常启动三组固定seed，未改血、注入库存、换seed、缩池或跳过未实现内容：

| seed | 实际公开成功提交 | 真实停止原因 | 返回码 |
| --- | --- | --- | --- |
| ENV-T04-RNG-SHUFFLE-001 | 42 | TabletOfTruth效果未实现 | 4 |
| ACT1-BASIC-10 | 22 | WoodCarvings源资格未实现 | 4 |
| ENV-T03-SEED-L | 28 | RoomFullOfCheese效果未实现 | 4 |

多幕配置会在Start预生成后续幕，消费与单幕基线不同，因此这些seed不能套用单幕旧路线结论。以上是实际阻塞记录，不计正常通关通过。单幕`ironclad-act1`保留原两例：真实死亡passed，PhrogParasite暂停依赖仍fault，整体rc1。

## 构建、命令与原始证据

共同产物位于`.artifacts/act-transition-20261017/`，证据位于`.audit/act-transition-20261017/`。原生参考HEAD仍为`2089659bd07920088b93226136c72b6b6ee590ad`，目录无修改。dev-run build=5c5b154b8c4c3aef；最终Host SHA256=474bd7ffdea1d338dea5798129bd461c93d6697bd315b86611d5c2244fc293c8。默认单幕身份维持：dev规则`a1eb707babc8ae47`/build`10734bae132a3f17`，dev-map规则`f7b89af74f7f4441`/build`e3314be0a99e8d90`。

```powershell
dotnet build AutoTheSpire.sln --artifacts-path .artifacts/act-transition-20261017
dotnet .artifacts/act-transition-20261017/bin/Differential/debug/Differential.dll act-transition --output .audit/act-transition-20261017/act-transition-evidence-final
python -X utf8 -m unittest discover -s net-the-spire/client -p test_event_public.py
python -X utf8 net-the-spire/client/run_game.py --host-dll .artifacts/act-transition-20261017/bin/Net.TheSpire.Host/debug/Net.TheSpire.Host.dll --scope dev-run --seed ENV-T04-RNG-SHUFFLE-001 --output .audit/act-transition-20261017/public-complete-0 --max-actions 1600 --use-potions --step-frontier --step-unsupported-room
```

共同solution构建rc0、0错误，4项既有编译警告；集成检查Engine99/Slice860/net421通过，Python12项通过。九组原有严格suite均rc0：regression为16双侧＋1单侧自检，其余正常套件全部实际双侧通过；新连续首幕suite保持1passed＋1fault、rc1，所有自检通过。最后将新版幕身份/标签与原编队声明解耦后，三幕独立检查和正常Host从同一构建复验，仍保持上述库存/地图/RNG与真实内容边界。完整最终复验日志见engine-complete.log、slice-complete.log、net-complete.log。文档检查645份、0问题，git diff --check rc0；最终闭包与真实失败分列于delivery-summary.json。关键证据：`act-transition-evidence-final/result.json`、`act-transition-evidence-final.log`、`net-complete.log`、`public-complete-0/1/2`中的summary/frames/steps及`delivery-*.log`。正常公开命令仍返回4，不能将其包装成rc0成功。初次编译、字段名诊断和版本标签首差日志保留，最终以交付构建和上述实测结果为准。

## 未完成与下一主线

T10完整合同及M01/M02继续开放。默认三幕预生成/生命周期可复用；接续应围绕dev-run的正常首幕通关与后续古代人，成批补真实可达的事件/房间效果和相应独立比较。第一幕现有Neow起始流程、其他Boss/精英与未导入编队、后续幕事件资格/效果、全部AfterActEntered参与者和TheArchitect仍未完整覆盖。

PhrogParasite的Infested死亡后召唤四只Wriggler依旧属于用户暂缓的T06-PR15，本轮没有恢复该族。既有完整Ironclad/五角色/进阶/训练及发布验收范围均未删减。当前目标、状态与文件认领只维护主清单，日期报告不另作派单。
