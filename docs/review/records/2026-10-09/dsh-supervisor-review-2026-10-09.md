# DSH 当前交付验收与接续 — 2026-10-09

本轮局部验收地图公开Host和base连续攻击修复，解除B地图独立对拍的前置等待；正常真实奖励闭环、完整M01及父项继续开放。当前认领与下一成果只查[主清单](../../tofix/README.md#当前任务)。负责人=本次用户“验收并继续DSH”会话，修改边界=主清单T03/T04/T05、CORE-LEGAL-PENDING相关证据、records索引与本轮audit；不编辑生产实现。

## 核验结论与证据

Harness完整结果已取齐：C seq1006、A seq1003、B seq841，三者turn/end completed，hasMore=false，实际running=false。A在B结束后完成第二轮落盘，B当时的blocked不再是当前依赖。结果JSON保存在各原派单audit下review-result-2026-10-09.json。

- C地图Host子包局部验收：从规则声明派生地图节点/后继/动作，显式dev/dev-map、共同未Start工厂、身份拒绝/候选/幂等、两seed公开进入战斗成立。负责人在最终A输入重跑`python -X utf8 -u net-the-spire/client/test_map_host.py --log .audit/dsh-supervisor/review-2026-10-09-current/map-host.ndjson`，rc0，182 passed/0 failed/182 executed。原检查允许成功或显式base故障，不能证明正常战斗胜利/奖励；本轮攻击另补实证。
- A连续攻击故障修复子包验收：CardRuleCostAllows中唯一守卫CallPure换成相同relation/定义ID/First顺序与不存在为0的短路查询；生产源码及4个检查相关源码、Host/Base/Slice DLL摘要与交接一致。未改core。复用匹配最终输入的build rc0/Engine95/Slice344/net415与原15strict实际全执行证据，strict仍8双侧passed/1self_checked/5unsupported/1diverged、rc1，无新回归。
- 负责人补跑原攻击探针dev/dev-map rc0，但原探针第二张只提交未回答，不能单独证明第二张实际伤害。增加本audit/verify_attacks.py，真实Host回答每张攻击的目标决策、观察查询并断言两次实际HP下降；结果与原响应保存在verified-dev.log/verified-devmap.log和attacks-dev*.json。无注入、改血、移牌或重试。
- B未交生产实现，只有audit草稿：不验收地图双侧case。现在前置结束且A落盘，按原目标接续；原15不能删，新2不能替代旧unsupported。

输入摘要与独立日志见[本轮audit](../../../../.audit/dsh-supervisor/review-2026-10-09-current)。A多实例检查只断言>=1，不能证明两实例，本次接受守卫修复与已核验行为，不扩大同定义多实例覆盖声明。A新切片检查修复前红态因golden/陈旧构建未成功执行，真实Host修复前故障和本轮修复后路径分列。

## 保留范围与接续依据

DevMap开局MapPhase0同时有PlayCard/EndTurn/ChooseMapPoint，选点后调用StartNextRoom，现有证据不构成源正常第一条地图战斗闭环。A下一奖励包须核对源首节点/初始化和阶段门控，消除可能跳过已开始战斗的路径；地图/战斗/奖励切换由base实现，net不筛候选。未实现非Monster房型、源前置初始化、EncounterPool/Queue的size/opaque身份与NotPublished矛盾均保持开放，不隐藏事实或公开未来遭遇。

挂起时守卫纯过程调用会将只读LegalActions升级为Faulted的通用缺口并入既有CORE-LEGAL-PENDING证据，base已合法绕过本处，不重开冻结core或增加生产core包。T01/T02仍待所需双侧/公开证据，M01不关闭，不开放完整身份/四幕/五角色/候选/训练。

接续按B地图独立对拍→A真实奖励与地图返回→C公开奖励轨迹；每线一个包，生产与构建依赖前置实际停止，audit准备可继续。派发accepted仅表示发送成功；具体新会话见主清单/新task-record。后续验收首先读唯一交接，按最终输入核对逐项能力，strict红态诚实保留。

本轮未重复build/Engine/Slice/net及整套strict，已有证据与输入匹配；未跑Ironclad四幕/五角色/120对/Release成本/真实客户端/AI。纯文档门槛已通过：doc-check rc0，295份Markdown/0问题；git diff --check rc0。原始日志在本轮audit/doc-check-final.log与diff-check-final.log。

负责人实际攻击补证两scope均rc0：第一张57→51，第二张51→45，目标决策全部回答且observe查询稳定。

本轮三包均accepted（未等待完成）：B `session-8cf3be04-54db-46da-84c6-c97820fb95db`、A `session-f6bce9bc-4f1f-4d1e-9375-af382f545d5d`、C `session-c1cf5cb4-4b67-4138-8b05-a32a5bf901c9`。原交接移至records，原task-record.handoff_path已修正并保留original_handoff_path；新包固定交接和构建门已进入对应记录。

## 当前内容复核与进度汇报

本次用户请求验收当前内容并汇报进度。负责人仅修改主清单对应条目、本日记录/索引与独有audit；原有生产文件只读。B地图生成机制子包验收，A/C准备稿不验收生产能力，M01继续开放；具体当前认领只维护主清单。

三会话实际running=false，完整结果hasMore=false：B seq648、A seq543、C seq315均turn/end completed。A当时等待B结束但先收blocked；C最终只交准备，交接仍in_progress，与会话已结束不符。派发accepted从未等于生产完成。结果JSON保存在本轮audit，归档交接保存原声明与负责人备注。

| 负责人实际检查（仓库根目录） | 返回码与结论 | 原证据 |
| --- | --- | --- |
| `dotnet tools/differential/bin/Debug/net9.0/Differential.dll test --suite regression --output .audit/dsh-supervisor/review-2026-10-09-progress/regression` | rc1；17/17实际执行，10双侧passed、1self_checked、5unsupported、1diverged，自检6/6。原6红态分类与seed未变 | audit/regression.log、regression/regression.json |
| 同DLL `test --suite tools/differential/fixtures/negative/suite-map-missing-dimension.json --output …/negative-dimension` | rc1；完整9项，8passed、地图case missing_dimension，维度门成立 | audit/negative-dimension.log |
| 同DLL `test --suite tools/differential/fixtures/negative/suite-map-wrong-terminal.json --output …/negative-terminal` | rc1；完整9项，8passed、地图case wrong_terminal，终局门成立 | audit/negative-terminal.log |
| `dotnet build tools/differential/Differential.csproj --nologo` | rc0，0警告/0错误；重建前后Differential.dll摘要一致，前面独立实跑确是当前生产构建 | audit/build.log、input-hashes.json |

源码核对确认base来自唯一工厂DevMap正常Start读实际节点/关系，参考来自独立StandardActMap/Rng实际生成，没有用SourceActMapCompute产物自证。alpha为51节点/65边/123事实，292底层64位字；beta为61节点/72边/140事实，265字；两侧逐事实及流指纹一致。范围仅幕1 Overgrowth/asc0/无二王的初始地图生成，未比较ChooseMapPoint动作、奖励、正常多房间/四幕。

Base生产源码及DLL摘要仍与上一轮验收相同，原Engine95/Slice344/net415证据可复用，不重跑规则全套；本轮没有新增A/C生产实现，不能报更高游戏覆盖。两条地图case增加的是严格机制证据，不代表全环境从未完成到完成。

### A设计的源裁定

原A `reward-design-2026-10-09.md` §2 的“固定11次、先金币再药水roll”和“第一幕num<=0恒假”不得直接按稿实现。冻结源的具体调用链：

1. `RewardsSet.WithRewardsFromRoom`调用`GenerateRewardsFor`，`RewardsSet.cs:224–255`构造奖励列表时就执行`PotionRewardOdds.Roll`；`PlayerOddsSet.cs:25`使用`rng.Rewards`。之后`GenerateWithoutOffering`的foreach（125–139）才逐个Populate金币、若有的药水、卡牌。正确前缀先药水掉落判定，再金币金额，之后填充。
2. `PotionReward.Populate:59–66`→`PotionFactory.CreateRandomPotion:66–79`在掉落分支多抽稀有度NextFloat和具体药水NextItem。非空完整池/三牌/无额外hooks路径无药水11抽、有药水13抽，不能一律固定11；其他分支按真实源消费记账。
3. `CardFactory.RollForUpgrade:288–305`使用`num<=originalOdds`且经过升级概率hook；`Rng.NextFloat:145–164`可返回0，不能把`<=0`写成永不升级。浮点转换和比较符号必须保留源语义。

原牌池顺序风险继续实查；无需新的用户输入或重开core。A生产尚未开始，以上是设计修正，不宣称发现已运行奖励机制首差。

### 阶段结论与接续

M01仍未验收：当前有地图生成/公开选点进战斗/连续攻击及部分结算能力；缺正常首房阶段修复、真实奖励与永久提交、公开返回地图、必要独立双侧证据。严格原5 unsupported和源开局rng-initial-shuffle差异继续开放。M02完整Ironclad四幕、M03五角色/进阶和M04候选仍后续，AI实验继续暂缓。

上一批同时发送带前置依赖的三包，A/C在门未开时结束，因此生产接续没有自动发生。负责人本次确认B结束并完成验收后，只接续原授权A奖励机制包；C等A实际交付后再派。原准备稿与已验收B能力直接复用，不再重复等待旧B。新派单仅accepted，不声明实现完成。

本次未运行全solution/Engine/Slice/net/四幕/五角色/120对/Release成本/真实客户端或训练。纯文档门已通过：doc-check rc0（307份Markdown/0问题）、git diff --check rc0；日志位于本轮audit/doc-check.log与diff-check.log。

本次A原奖励包接续已accepted：`ENV-T05-real-rewards-resume-20261009`，会话`session-b1e2bcc9-987b-4b03-b3c8-46e98df03480`；新记录/固定交接与源裁定进入主清单T05，未等待完成。C未同时重派，待A真实交付后按原授权接续。
