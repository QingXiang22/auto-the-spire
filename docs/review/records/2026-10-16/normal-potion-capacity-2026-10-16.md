# 正常满药水栏、精英奖励与路线阻塞修复

本包为[ENV-T05 地图、战斗、奖励往返](../../tofix/README.md#env-t05)补齐真实满药水栏证据。接手时先查主清单，再复用`normal-potion-capacity`两例及公开回放；不重做已交付的AttackPotion、Aroma或事件后第三战。下一包仍须按原完成条件处理其他适用药水、后续房间和未验证内容；验收人为Codex负责人，本报告不关闭ENV父项。

## 本次交付

原seed `ENV-T04-RNG-SHUFFLE-001`，Ironclad/A0/全解锁/单人/dev-map、`sts2-v0.109-xxhash64@1`。setup为空，整个正常流程没有注入库存、HP或清场，没有换seed、删case、缩池或修改参考玩法。

路线：起点→Monster(1,1)→Monster(0,2)→Unknown/Aroma(0,3)→Monster(1,4)→Monster(1,5)→Monster(0,6)→Elite/Byrdonis(0,7)。这是五场Monster加一场真实Elite；旧字段`normal_combat_count=6`计战斗房间总数，不把精英称为第六场普通怪物战。

第1/3/5战实际取得AttackPotion、BlockPotion、StrengthPotion，槽位0/1/2均有真实实例。精英再掉落SpeedPotion时：

- skip例保留全部持有实例，显式跳过奖励瓶。
- discard例丢弃槽位1，保留同一奖励实例与槽位0/2，再将原奖励实例填入槽位1。
- 两例比较全部动作、决策、资源、牌区顺序/来源、Power、奖励、永久库存和随机流；领取/跳过/丢弃不额外抽数。
- 精英实际生成RippleBasin遗物，显式跳过后领取39金币，完整关闭奖励，最终HP26、金币223并恢复合法地图后继。

策略只读公开信息：Aroma选择升级Bash；优先FlameBarrier、公开弃牌含FlameBarrier时的Headbutt、事件后的Power及Bash/Strike/Defend，目标为公开最低HP。不用隐藏实例来源或随机流决定动作，隐藏状态只供诊断比较。

## 必要实现与修复

1. RewardWiring保留实际Elite的药水加成、35～45金币及卡牌稀有度；卡牌后在Player.Rewards抽遗物稀有度，从玩家袋前端取身份并移出共享袋。遗物领取复用RunRelics.Obtain，未知效果明确失败；未解决遗物阻止奖励关闭。客户端支持战斗奖励遗物策略，默认skip。
2. 事件后遭遇派生流错误地使用战斗场次作为楼层，导致第四战史莱姆编队首差。地图范围改取已访问节点数，预种首幕1～16层派生流；未改幕转换或MapRoomWiring。
3. Inklet正常HP与高进阶HP原先颠倒，按源修为正常11～17、tough12～18。
4. 遭遇数据已有`startingActionsByEnemyIndex`却未进入出生编译。传递固定阵容及编队的位置配置，复用状态机初值初始化；中间墨仔从三连击开始，其余从Jab开始，未声明行动具名失败。
5. Slippery耗尽后遗留零层实例，现有未格挡伤害才耗层，零层立即销毁；连击后续命中重新判断持有状态。
6. 最后一敌死亡后牌区选择仍开启，以及Headbutt单候选没有自动选择。牌区选择共享守卫抑制战斗结束后的选牌；Headbutt普通/升级声明autoSelect=true。其他末杀余效和生成候选选择继续沿原流程。

主要代码：`BaseSpire/Slice/RewardWiring.cs`、`Domain/Run/RunRelics.cs`、`Compilation/BattleRoomCompilation.cs`、`CardCompilation.cs`、`Orchestration/PowerRuleRuntime.cs`、`SinglePlayer/EncounterContent.cs`及相应ContentSource。core和参考目录没有改动。

参考适配执行原生Elite房间及完整奖励，遇到未适配ExtraRewards明确失败；Headbutt通过原生选牌回调消费共同决定。Power投影用静态定义名/ID对应替代一律去掉_POWER后缀的规则，未共享base的伤害、选择、随机数或结算结果；这不是对规范ID的独立审计。

探索过另一可见Unknown支路，实际为尚未实现的TheFutureOfPotions。曾按错误队列位置误判为TheLegendsWereTrue；实际选择诊断纠正后已撤回全部临时事件/自定义奖励实现。正式验收沿原Monster→Elite路线，没有用事件替换绕过精英奖励。

## 身份与证据

本地产物：`.audit/capacity-20261016/`，共同构建`.artifacts/capacity-20261016/`。参考HEAD=`2089659bd07920088b93226136c72b6b6ee590ad`，实际加载程序集摘要保留在严格产物中，参考保持只读。

默认历史布局golden迁移来自实际`--describe-build`，不是放宽行为断言：

| Scope | 旧rules / build | 新rules / build |
| --- | --- | --- |
| dev | 2df4b453b15febbf / b8d3a04a74271921 | aad01bb0852d0d7d / 48e4a0d6e545db41 |
| dev-map | 1f864e09b839c59b / 23b8f78062613886 | 59937585291f40b1 / b91254a8b3d86d80 |

本次正常v0.109轨迹为rules=`f063db523973fff5`、build=`52cd44d4e52e5ef1`；Host SHA256=`a3f9cc84f4cef2638ca780da2c3658a8407613d3a282dc1af2cd6ffa9653bb13`。布局不同的身份分开记录。

严格容量两例分别106/107个共同动作、107/108个边界双侧通过。公开Host回放分别149/150次提交，逐边界核对公开动作、资源、永久牌组句柄来源、药水槽位/来源和真实遗物。公开弃牌列表按可见定义/升级匹配，未假定公开展示顺序等于Engine内部牌区顺序。满栏时复用旧领取候选被拒且观察不变；skip/discard/take的request重放相同、过期候选拒绝；discard后的奖励句柄与填槽后的句柄相同，外侧两瓶句柄不变。

公开证据：`public-skip/summary.json`、`public-discard/summary.json`和各自`frames.ndjson`；本地复现脚本`public_capacity.py`只经describe/catalog/start/step/observe操作。正常严格证据为`final-capacity/`及`verified-normal-potion-capacity/`；早期`recovery`、`floor-fixed`、`inklet-fixed`、`identities`、`inklet-mechanism`保留实际首差，不算通过。

## 验证命令与范围

```powershell
dotnet build AutoTheSpire.sln --artifacts-path .artifacts/capacity-20261016
dotnet .artifacts/capacity-20261016/bin/Core.TheSpire.Engine.Tests/debug/Core.TheSpire.Engine.Tests.dll
dotnet .artifacts/capacity-20261016/bin/BaseSpire.Slice.Tests/debug/BaseSpire.Slice.Tests.dll
dotnet .artifacts/capacity-20261016/bin/Net.TheSpire.Tests/debug/Net.TheSpire.Tests.dll
dotnet .artifacts/capacity-20261016/bin/BaseSpire.Slice.Tests/debug/BaseSpire.Slice.Tests.dll --capacity-prerequisites-only
dotnet .artifacts/capacity-20261016/bin/Differential/debug/Differential.dll test --suite normal-potion-capacity --output .audit/capacity-20261016/verified-normal-potion-capacity
python -X utf8 .audit/capacity-20261016/public_capacity.py skip
python -X utf8 .audit/capacity-20261016/public_capacity.py discard
```

新增4项受控检查区分墨仔三连击逐次反伤、完全格挡不耗滑溜、耗尽后不封顶、Headbutt单候选/末杀选择。旧精英消费者检查现在要求真实遗物与正确金币，并验证其他奖励解决后必须处理遗物才恢复旅行；受控清场不冒充正常通关证据。

共同solution构建rc0，0 warning/0 error；Engine99/99及net419/419通过。八组严格suite均rc0：regression为16双侧＋1单侧自检，capacity/post-event/aroma/potion-use/potion-reward各2双侧，held-potion/two-combats各1双侧；所有自检通过，新增药水实例来源缺失和精英遗物遗漏反例均被拒绝。最终完整Slice855/855通过（suite=all），日志为engine.log、slice-verified.log、net.log及verified-*.log。文档检查604份、0问题，git diff --check为rc0。

首次全量Slice暴露旧精英测试只解决金币/卡牌/药水，新增遗物未处理使MapPhase停在3；已将完整遗物关闭门槛加入该检查并局部通过，不修改生产守卫接受提前旅行。随后Boss受控奖励初态也补上“没有遗物、遗物已解决”，三个边界局部通过；该夹具没有执行正常开奖，必须完整声明新增奖励状态。早期本地受控夹具的空弱池/遗漏Inklet构造失败保留日志，修正夹具后4项通过；不计为正常Run证据。

本包不证明全部遗物IsAllowed过滤、空袋回退、其余Elite行为或完整四幕；参考RelicGrabBag空桶回退与真实源码存在已知差异，本路线首次精英的非空桶不触发它。跨幕累计楼层、Feed末杀、其余药水效果、五角色整局与训练仍未在此验收。
