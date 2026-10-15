# 三线交付复核与接续（2026-10-13）

本轮按用户“继续推进项目”核验A精英机制、B正常奖励对拍入口、C公开事件消费者。负责人Codex `/root`完成隔离构建与共同检查，正式客户端正常seed L已自然击败Byrdonis并继续到下一只精英。阶段和认领仍由[主清单](../../tofix/README.md)维护；M01、完整Ironclad和全原版范围没有关闭。

同日用户重新打开DSH后服务已恢复，三个旧会话确认停止、completed历史取齐，A幕转换／B正常初始化两个新专用会话实际accepted。下文503和“尚未派发”是恢复前的复核时点；实际接续见[恢复追加](#dsh恢复与实际接续)，当前认领仍只查主清单。

## 接手入口与本轮结论

优先复用已交机制。A的下一包按既定T10接幕定义与幕转换；B先定位正常源兼容初始化边界；C随A已就绪公开契约取正常轨迹。两份接续任务文本已准备，**尚未派发**。发出前必须确认旧会话停止并登记新认领，不能以本报告当accepted证据。

DSH查询助手报告登录失败；后续限定诊断确认登录HTTP303、已有一个Cookie，但首页和`session/list` RPC均HTTP503。Cookie值、凭据、响应正文均未保存。无法确认三旧会话实际运行状态，也未创建／发送新会话；生产文件主责暂不释放。无需根据助手的笼统错误认定密码失效。接口恢复后按原记录核对状态／结果，再安排新专用会话；不重发含糊请求。

## 交付身份与源码核查

本轮三份固定交接均存在，task／dispatch／session身份与各task-record一致：

| 线 | 会话／交付 | 负责人结论 |
| --- | --- | --- |
| A | `session-2dd01a31-b0c3-4128-b182-fbeca7038dbe`；[Byrdonis交接](dsh-byrdonis-elite-handoff-2026-10-13-2dd01a31.md) | 正式加载、HP、Territorial回合末语义、行动循环和正常公开胜利已核验；完整精英奖励／新怪物独立原版对拍仍未验收 |
| B | `session-011c4072-7363-48da-bc48-160af934e6ab`；[正常奖励交接](dsh-normal-reward-diff-handoff-2026-10-13-011c4072.md) | 入口与严格首差报告已核验，新2例均diverged；后继旅行、下一战斗和真实双侧药水领取未验证 |
| C | `session-a762b7b0-79e5-4d34-b331-23a88514f140`；[公开事件交接](dsh-public-event-policy-handoff-2026-10-13-a762b7b0.md) | 11项事件判别及新共同Host正常105步／106提交复算成立；未知选项归属依赖Host合法候选，未增加协议元数据 |

[摘要核对](../../../../.audit/owner-continuation-20261013/delivery-check.json)验证A提供5项实现摘要和B提供7项源码／输入摘要，12项全部匹配。B最终已将工具投影辅助方法调整为internal，A早期solution的CS0122不再是当前阻塞。重新检查MachineState与NextMove同步写回、OwnSide触发，以及Frail／Constrict只施加一次的时长见证；保留原断言，未用减少suite或吞失败制造通过。

编译后记录2179个生产／参考源文件摘要，完成全部检查后逐项仍匹配，见[复核汇总](../../../../.audit/owner-continuation-20261013/review-summary.json)。这是本轮检查窗口的稳定性证据，不等于已经远端确认会话停止。所有构建输出隔离在`.artifacts/parallel/owner-continuation-20261013/build`；未改生产源码、默认obj/bin或他人证据。

## 共同构建与实际验证

所有命令从仓库根执行。构建命令为`dotnet build AutoTheSpire.sln -p:ArtifactsPath=F:/Scylier-Project/active/auto-the-spire/.artifacts/parallel/owner-continuation-20261013/build --nologo -v:minimal`。之后直接运行该闭包的测试／工具DLL，按构建、Engine、Slice、net顺序串行规则检查；工具套件按regression→normal-reward-prefix执行。

| 范围 | 实际结果 | 证据 |
| --- | --- | --- |
| solution | rc0、0错误、4个编译警告 | [构建日志](../../../../.audit/owner-continuation-20261013/build.log)；警告为CardDefinitions参数捕获、ActMap两处可空、SpirePowerSource一处可空，本轮未改警告源码 |
| Engine／Slice／net | 99／842／418通过，均rc0；net418执行、0失败 | [返回码](../../../../.audit/owner-continuation-20261013/rule-exits.json)，同目录各项目日志 |
| 原完整strict17 | 16双侧passed＋1工厂接线self_checked，0失败，自检0失败，rc0 | [regression](../../../../.audit/owner-continuation-20261013/regression/regression.json)；原suite SHA不变 |
| 正常奖励完整2例 | 双侧各2实际执行，0passed／2diverged，自检0失败，rc1 | [新结果](../../../../.audit/owner-continuation-20261013/normal-reward-prefix/regression.json)；两例首差均boundary0地图候选 |
| Python事件策略 | 11项，rc0 | [事件判别](../../../../.audit/owner-continuation-20261013/event-policy.log) |
| 正式客户端＋新Host | 105次成功提交，106次尝试PhrogParasite被拒，rc4、run_complete=false、close_ok=true | [正常轨迹](../../../../.audit/owner-continuation-20261013/public-run/summary.json)，同目录frames／steps |
| 公开提交复算 | 106／106逐字一致，230个判别通过，rc0 | [复算日志](../../../../.audit/owner-continuation-20261013/public-replay.log)；不是原版独立对拍 |

实际输入闭包摘要见[verified-closure-sha256](../../../../.audit/owner-continuation-20261013/verified-closure-sha256.json)。旧严格17没有新增角色／精英／跨幕case，不能替代这些范围的验收。

## 正常流程的新边界

本轮使用正式`net-the-spire/client/run_game.py`，Host路径指向上述隔离构建；scope=dev-map，seed=`ENV-T03-SEED-L`，max-actions160，use-potions、shop-policy buy／kind card／remove，event-choice=`SunkenStatueDiveIntoWater`。没有包装运行器、换seed、状态注入、清场或缩池。

公开规则身份`net:ruleset:a64a83e3f92610d7`，catalog `net:catalog:adcb9da61a30b527`，backend `net:backend:simulation-0.2-716ace0f01a46472`。

Byrdonis在第68步入场，真实战斗后第98步胜利，Run HP32；99领金币、100领卡／关闭奖励，101～102营火恢复到56HP，103～105宝箱／跳过遗物。106当前合法精英候选e26被Host以`missing enemy behavior base-spire:phrog-parasite`拒绝，执行状态completed、retry_mode stop；没有盲目重发。

源码[InfestedPower](../../../../sts2/src/Core/Models/Powers/InfestedPower.cs)要求死亡后逐个生成4只Wriggler、标记StartStunned并阻止战斗提前结束；[PhrogParasite](../../../../sts2/src/Core/Models/Monsters/PhrogParasite.cs)出生施加该Power。单写一个攻击／塞Infection牌声明不能完成此遭遇。受阻范围归T08精英及T06-PR15暂缓的召唤族；继续T10不解除暂缓，也不把Phrog换成已实现怪物来通路。

## 正常对拍初始化的最小诊断

两例起始候选差异复现：take base `[map:1,1,map:2,1,map:5,1]` vs reference `[map:2,1,map:3,1,map:5,1]`；skip base `[map:0,1,map:2,1,map:5,1]` vs reference `[map:1,1,map:2,1,map:3,1,map:4,1,map:5,1]`。后续奖励不同发生在已分叉路径，不能直接归因为奖励算法。

独立诊断工程只引用本轮Base/Core/参考DLL，没有修改参考或构造游戏状态。首轮引用路径多退一级导致CS0246，修正诊断工程路径后构建rc0／0警告，探针rc0；首轮日志保留。[实际向量](../../../../.audit/owner-continuation-20261013/seed-probe.jsonl)：

| seed | 本源base RunSeed（uint） | reference原生RunSeed（ulong） |
| --- | --- | --- |
| ENV-T03-SEED-L | 1509098584 | 10828846375177304570 |
| ENV-T04-RNG-SHUFFLE-001 | 3850333834 | 800470399952497205 |

源码源基线的StringHelper为32位循环，参考默认为XxHash64；地图／玩家奖励流也使用不同根与名称hash。`NormalRewardReference`先构造RunState（已生成地图）再调用`RngSourceCompat.Apply`；后者只替换尚未消费的run具名流。RunState根Seed只读，接收RunRngSet的构造为private，公开GenerateMap仍使用原生根／名称派生。故现有适配不能声称完整正常源兼容初始化。

下一B包先沿公开API确认可在消费前接入的边界；不足时交最小参考bootstrap接口提案给负责人裁决，不能擅改参考、反射注入或更改字符串seed。还需统一对拍事实：base战斗中Run.Hp是永久提交值，参考取实时玩家HP，二者不应作为同义字段强比；保留战斗HP与提交后长期HP判别，不能删维度。具体文本见下节。

## 可直接接续的任务材料

- [A幕转换短包](../../../../.audit/owner-continuation-20261013/next-act-task.txt)：准确给出MapRoomWiring.SetupSteps／ContinueToNextAct、RewardCompilation、RunSchema／Bindings、幕池／队列、随机种流、源码EnterAct顺序、长期资源与幂等键、测试与正常轨迹要求。
- [B正常初始化短包](../../../../.audit/owner-continuation-20261013/next-reference-task.txt)：给出实际首差／种子向量、已确认公开API缺口、工具边界和严格失败要求；参考接口提案不等于参考生产修改授权。

两份文本明确“尚未派发”，未建立新task-record／session，也未占用新的生产包。DSH服务恢复后的第一步是核实旧会话实际状态，再按主清单登记新专用会话并发送一次。AI、批量补卡、敌人召唤／复活保持原排期；本轮未执行完整多幕／五角色、真实客户端或发布。

## DSH恢复与实际接续

用户同日回复已打开DSH，负责人重新获取session/list成功。A原会话running=false／seq960，B=false／725，C=false／586；随后固定through_seq分页取齐961／726／587个事件，三者最后turn/end均completed。前一阶段源码／交付和共同检查结论可复用，不把running=false单独当完成依据。

| 新包 | 实际新会话 | 目标与边界 |
| --- | --- | --- |
| ENV-T10-act-transition-20261013 | `session-fa7f87ac-e285-452a-801a-ad4604e6c431` | 第一幕显式定义与源默认下一幕生命周期；base单写，长期状态／幂等／队列／随机域保留，召唤复活仍暂缓 |
| ENV-T04-normal-initialization-audit-20261013 | `session-e524322c-614d-4142-b393-2ab4a0c80829` | 工具侧正常初始化兼容矩阵、公开API适配或最小bootstrap提案；只引用本轮固定DLL，不重编A、不改参考 |

两包已按主清单登记目录、目标、下一成果与原完成条件后串行发送，均返回accepted；没有响应含糊或重发。真实提示词／request_id／SHA／handoff_path在各task-record：[A记录](../../../../.audit/dsh-act-transition-20261013/task-record.json)、[B记录](../../../../.audit/dsh-normal-initialization-audit-20261013/task-record.json)。提示词SHA按客户端UTF-8规范化文本计算；原文件换行不改变实际发送的文本身份。

[恢复与派发汇总](../../../../.audit/owner-continuation-20261013/recovery-dispatch-summary.json)保存旧结果去重与新请求身份。两旧包原始交接继续保存，尚未用accepted结算新交付；本轮未查询或等待两个新会话完成。C消费者已交，待A跨幕公开契约就绪后随包，不派一个只等待A的生产包。父项、AI和批量补卡排期不变。
