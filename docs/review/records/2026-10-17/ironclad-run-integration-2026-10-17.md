# Ironclad连续Run集成与当前整局边界

本轮按用户“提高overview、按工程交付推进”的要求，将[当前主线](../../tofix/README.md#env-t05)组织为正常首幕结束→真实跨幕→Ironclad完整胜负。Feed、药水、随机和永久状态继续作为同一主线的验收点。原ENV ID、M01/M02门槛和暂停项保留，不按每个案例另立任务。

接手时先复用下面的连续入口和现有正常轨迹。当前原seed在地图(0,12)的PhrogParasite精英入口受敌人召唤机制暂缓限制；不能换怪、缩池或把前缀一致视为首幕完成。幕转换是另一项结构性工作，按主清单确认原认领与公共文件窗口后接续。本轮维护与集成由Codex `/root`完成，父项未关闭。

## 可运行结果与剩余边界

配置固定为Ironclad/A0/all-unlock/singleplayer/dev-map，原seed `ENV-T04-RNG-SHUFFLE-001`，随机布局 `sts2-v0.109-xxhash64@1`。没有注入生命/库存/清场，没有替换seed、源池或怪物；core、MapRoomWiring和参考玩法均未修改。

| 整局目标 | 本轮实测 | 验收结论 |
| --- | --- | --- |
| 真实失败终点 | 原固定策略走到第七场战斗并真实死亡；121个共同动作、122个边界双侧一致；同Host172次公开提交至loss | 死亡流程通过，不能代替胜利路线 |
| 首幕连续推进 | 公开防守策略完成第七场Nibbits，经过宝箱(0,9)、营火(0,10)、SlipperyBridge(0,11)，HP33；137个共同动作、138个稳定边界与独立参考一致 | 继续旅行到(0,12)时PhrogParasite具名失败，首幕目标仍失败 |
| 公开同构建出口 | 防守路线191次成功公开提交，逐边界核对后实际尝试下一精英，得到同一phrog-parasite失败；run_complete=false | 阻塞位置公开可复现，未伪造终局或宣称通关 |

`ironclad-act1`严格suite保留两个必测ID：原seed死亡控制例passed；首幕完成例fault，整体rc1。未删失败case、改变预期终点或以零差异前缀包装成passed。失败的精英动作不在参考侧伪造执行；参考只重放它之前已经成功的共同动作。

此前已通过的六战容量/Aroma/事件永久状态等作为回归复用，本轮的交付单位是连续Run入口、沿途实际规则修复和同Host闭环。

## 一体实现

### 连续运行与独立参考

新增`tools/differential/NormalRunEntry.cs`和`ironclad-act1` suite，持续运行至实际死亡、首幕完成或具名失败。场次数只参与公开策略，不能作为成功停止条件；2000步只作保护。保持旧正常前缀入口和既有case。

策略只读取公开定义、HP、牌区和地图类型：沿用FlameBarrier/Headbutt/Power等可见优先级；defensive变体在低于等于20HP时提高Defend优先级。死亡控制例保留既有六战路线和后续使用药水；防守例保留相同seed，目标仍要求首幕完成。

参考侧原生执行MapTravel、RoomFactory、CombatRoom、奖励、事件及RestSiteRoom/Heal/Smith。原生TreasureRoom.Enter会自动取得遗物，无法表达恢复源码的进入/开箱/跳过边界；`NormalRunReferenceRooms.cs`按源顺序组合原生RNG、共享遗物袋和金币/遗物命令，明确注明这一适配，没有使用base玩法结果。当前正常轨迹验证的是持有BurningBlood的这些边界，不能外推全部房间hook/物品资格；商店等未适配范围仍明确失败。

base失败但存在成功计划时，TestCommand允许参考核验该前缀，保留首差能力。normal-run的reward-closure检查历史上的真实奖励→合法旅行；旧前缀suite仍要求最后边界关闭。真实死亡也有Terminal/Defeat和空动作验证。新增反例确保“前缀完全一致但整体失败”仍返回fault。

### 沿实际路径发现的规则修复

- Nibbits路径首差为敌方旧格挡跨自身回合保留：共享EnemyTurn在Poison/行动前清理本侧格挡，沿用Barricade等preventsBlockClear谓词。
- 玩家真实死亡仍保留Strength/Poison：共享结算收尾在确认HP归零时清除能力，再提交Defeat。中毒致死沿相同路径；修正旧检查把“死亡后不递减”误解为“仍保留Power”的期望。
- 第二次真实事件访问遇到SlipperyBridge资格缺口：按源TotalFloor>6及存在可移除永久牌判定，首幕楼层使用已访问节点数。
- 接通SlipperyBridge完整访问：预览非基础可移除永久实例；没有候选则回退全部可移除牌。Overcome移除同一实例并结束；HoldOn支付3、4、5……HP、维护跳过实例集合、排除上次定义后重抽，空域按源回退；致死也先完成本次效果再结束整局。复用RunDeck.Remove、共享随机永久选择和RunResources伤害。

所有可变状态在Engine；模块只保存编译符号。预览是真实永久牌引用，内部跳过集合具名不发布；随机选择恰好一抽，源实例/事件随机在参考侧独立核对。未恢复召唤/复活，未开放完整Ironclad身份。

### 通用公开映射修复

SlipperyBridge预览字段在事件外为可选None，原投影器直接按entity_ref编码导致Host开局失败。现按既有0.2字段契约省略当前为空的optional字段，记录N-02-OPTIONAL-ABSENT；有值时发布known及闭合句柄，清空后再次省略。required字段不享受此省略。

没有新增逐事件net分支，也没有给字段输出仅属于content_state的not_applicable。协议已有optional省略语义，C#/Python/wire形状不变；新增通用“空→实际引用→清空”检查，公开重放覆盖实际预览与移除。

## 构建、命令与证据

开发开始于2026-10-16并跨日完成，证据目录保留`.audit/act1-run-20261016/`，共同构建为`.artifacts/act1-run-20261016/`。参考HEAD=`2089659bd07920088b93226136c72b6b6ee590ad`，工作区只读、无改动；实际DLL摘要在每份参考身份记录中。

| 配置 | 旧rules/build | 新rules/build |
| --- | --- | --- |
| 默认dev | aad01bb0852d0d7d / 48e4a0d6e545db41 | a1eb707babc8ae47 / 10734bae132a3f17 |
| 默认dev-map | 59937585291f40b1 / b91254a8b3d86d80 | f7b89af74f7f4441 / e3314be0a99e8d90 |

迁移依据为实际`--describe-build`及修复前后的真实首差。正常v0.109配置另为rules=`75676426f3a7c709`、build=`a703ccfa499dc17a`。本轮Host SHA256=`f7b9a0c2ecde6c96b75f49c4ec7b45c9fd1f18b6c77ff9fe1e07772b7ccede30`。

```powershell
dotnet build AutoTheSpire.sln --artifacts-path .artifacts/act1-run-20261016
dotnet .artifacts/act1-run-20261016/bin/BaseSpire.Slice.Tests/debug/BaseSpire.Slice.Tests.dll --run-lifecycle-only
dotnet .artifacts/act1-run-20261016/bin/Differential/debug/Differential.dll test --suite ironclad-act1 --output .audit/act1-run-20261016/verified-ironclad-act1
python -X utf8 .audit/act1-run-20261016/public_run.py original-seed
python -X utf8 .audit/act1-run-20261016/public_run.py continuation
```

上列新suite预期仍为rc1，原因是当前保留的首幕完成失败例；不能将该返回码隐藏。两次公开脚本rc0表示声明的死亡/阻塞重放和断言成立，其中continuation不是通关成功。

| 验证 | 实际结果 | 原始证据 |
| --- | --- | --- |
| solution | rc0；最终增量构建0 warning/0 error | final-build.log，后续net/Host构建日志 |
| Engine / Slice | 99 / 860通过，均rc0 | engine.log、slice.log |
| net | 420执行、420通过、0失败，rc0 | net-final.log |
| 既有九组严格suite | 全部rc0；regression为16双侧＋1单侧自检，其余正常suite实际双侧通过，所有自检通过 | verified-regression及verified-normal-* |
| 新连续suite | 2例实际执行，1passed＋1fault，rc1；失败前138边界参考一致，所有自检通过 | verified-ironclad-act1/regression.json及双侧records |
| 公开Run | 死亡122边界/172提交；防守138边界/191成功提交并具名失败 | public-original-seed、public-continuation的summary.json/frames.ndjson |
| 定向机制检查 | 5项Run lifecycle、8项Poison、12项Unknown通过 | lifecycle-final.log、poison-checks.log、unknown-final.log |
| 文档与补丁检查 | 623份Markdown，0问题；git diff --check rc0 | docs-check.log、diff-check.log |

首次严格运行保留了Nibbit格挡和死亡能力残留首差；后续SlipperyBridge资格/模块和可选引用Host失败也保留原日志。已更正编译与局部夹具问题；不使用旧DLL运行结果冒充最后构建，最终以verified-*及net-final/public重放为准。

## 未完成与接续边界

首幕通关、Feed最大生命跨房、完整遗物参与者/资格、全部事件与商店独立适配、全部精英及跨幕仍未验收。实际PhrogParasite依Infested死亡后生成四只Wriggler并延后胜利，仍属用户暂缓的T06-PR15；不能只补攻击声明或把它换成Byrdonis。

幕转换原DSH任务的上次确认状态为停止，本轮只读查询未建立登录会话，没有新的可验收交付；不将accepted、停止或登录失败解释为实现完成。本轮没有修改MapRoomWiring、恢复旧会话或派发新远端任务。当前状态和下一工程主线只维护主清单，历史报告不另作派单。
