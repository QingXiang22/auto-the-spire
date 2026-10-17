# 第一幕多页事件与非战斗卡牌奖励接续

本轮让正常默认幕计划越过TabletOfTruth，永久升级进入后续战斗并到达真实死亡；同时补入RoomFullOfCheese的规则层八选二和WoodCarvings真实资格。完整第一幕胜利仍未完成。

归属为[ENV-T09房间与事件](../../tofix/README.md#env-t09)，服务于[ENV-T10整局主线](../../tofix/README.md#env-t10)及T03/T04正常公开与参考比较。Codex `/root`负责base事件、共享Run操作、必要公开集合语义、独立比较与文档；没有修改core、参考玩法、net生产协议或恢复召唤／复活。既有共享工作区改动保留，未提交Git。

## 接手后先做什么

从主清单确认当前认领，复用本轮事件能力和既有跨幕结构。下一条工程主线仍是正常第一幕Boss奖励关闭，具体依赖已经由正常Host定位：

1. ACT1-BASIC-10通过WoodCarvings资格后，缺真实事件效果；实现时按源保留基础牌选取、定向变换和附魔，不把参考自动选择首张的简化当成源UI语义。
2. 原seed选择TabletOfTruth的Smash回复分支后，正常推进137次公开提交到BygoneEffigyElite入口，缺该敌人的正式行为。该敌人不依赖暂缓的Phrog召唤，可以独立接续。
3. ENV-T03-SEED-L已进入RoomFullOfCheese，但Gorge暂停时公开0.2不能表达恰选两张；按[T03](../../tofix/README.md#env-t03)和[协议](../../../design/net-the-spire/protocol.md)处理真实多选应答。不能降低Engine数量约束、静默代选或枚举组合冒充通用多选支持。Search的ChosenCheese获得和AfterCombatEnd也未完成。

每项的产品完成条件仍是[主清单](../../tofix/README.md#env-t09)中的正常房间／资源／选择闭环和独立证据。原`ironclad-act1`完成例保留失败，不用新增死亡例替换它。PhrogParasite依赖ENV-T06-PR15，用户暂缓继续有效；后续古代人、TheArchitect和完整四幕胜负仍未交付。

## 已交付能力

TabletOfTruth独立事件模块保留初页回复／解读、后续解读／放弃和第五页自动结束。费用缓存为3、6、12、24、剩余MaxHp−1；前四页从可升级永久牌随机选一张，第五页升级全部剩余牌。单候选仍消费事件随机数，没有可升级牌时不抽。致死选择先把MaxHp降到1，再按源击杀，不升级；访问清理由共享ChoiceEventModule完成。

新增`RunResources.LoseMaxHp`：输入快照、扣除超出新上限的当前生命、MaxHp最低1；复用已有脱战伤害。这里只覆盖当前无相关修正／死亡阻止参与者的非卡牌来源，未声称全量hook或历史已实现。

RoomFullOfCheese的Gorge复用新`RunCardRewards`生成完整Ironclad单人普通池中的8个不同候选，Engine执行恰选两张，所选实际实例按答案顺序加入永久牌组，余项销毁。源`ForNonCombatWithUniformOdds`自带`NoUpgradeRoll`，只消费8笔Rewards、不掷升级或修改稀有度偏移。`EventCardOffers`作为公开Run序列登记，不加入net逐事件映射。Search保留源选项并明确报告缺失遗物生命周期。

WoodCarvings资格独立于效果模块：存在可移除基础永久牌才允许；没有这样的牌则跳过该身份。完整源队列和顺序保留，不按当前可执行内容缩池。当前方法与调用入口已更新至[事件规格](../../../design/mod-the-spire/event-domain/spec.md)、[Run操作契约](../../../design/mod-the-spire/run-domain/spec.md)和[房间接口](../../../design/mod-the-spire/room-compile-reference.md)。

## 正常流程与独立对照

新增严格`normal-tablet`套件，显式`dev-run`，同一原seed、空setup、正常Start与真实事件队列。参考自行创建Overgrowth/Hive/Glory原生RunState，执行实际EventModel、CombatRoom与RewardsSet；没有注入牌、血量、房间或随机答案。

两侧经过五次解读与后续正常战斗至真实死亡：38个语义动作、39个稳定边界，动作／决策／牌堆／永久实例来源／资源／奖励／随机流逐步一致。正常死亡是本例预期终点，不能据此认定首幕完成。

同构建公开Host重放同一语义计划，实际55次提交、39个边界全部核对；验证公开永久句柄连续性、牌组顺序、当前合法动作和生命资源。五页请求原样重放返回原结果，用旧观察版本重新提交被拒且不改状态。证据位于`.audit/act1-events-20261017/public-semantic-tablet/`，可执行脚本为同目录上级的`public_tablet_replay.py`。

另有12组受控独立场景／32个资源和牌组边界：三组固定seed的五页、回复／放弃／致死、Cheese候选和反序二选；原seed增加唯一／空升级域；WoodCarvings通过实际受控问号入口比较有／无基础牌资格。结果明确标记`controlledInitialState=true, normalVictoryEvidence=false`。已消费事件／Rewards流同时比较抽数与原生状态指纹，未消费流只核对零抽数，不为观察补抽。

## 正常公开客户端边界

| 配置与策略 | 成功提交 | 实际结果 |
| --- | ---: | --- |
| dev-run，原seed，默认稳定选项排序 | 82 | Tablet五页后继续战斗，真实死亡，客户端记录rc0 |
| dev-run，原seed，显式TabletOfTruthSmash | 137 | 通过事件与后续普通战斗；BygoneEffigyElite缺敌人行为，客户端记录rc4 |
| dev-run，ACT1-BASIC-10 | 22 | WoodCarvings资格已通过；缺效果模块，客户端记录rc4 |
| dev-run，ENV-T03-SEED-L | 29 | 进入Cheese后Gorge二选被公开映射明确拒绝：TODO-N02-MULTI-SELECT，客户端记录rc4 |

原始`summary.json`和frames/steps分别保存在`public-final-original`、`public-final-smash`、`public-wood`、`public-final-cheese`。拒绝路径在PowerShell工具封装显示进程返回1，客户端自身记录退出码4，均非成功。只有真实死亡轨迹为`run_complete=true`，本轮没有正常Boss胜利。

## 构建与验证

共同构建位于`.artifacts/act1-events-20261017/`，证据位于`.audit/act1-events-20261017/`及同前缀日志。源依据为本地`sts2/src/Core/Models/Events/{TabletOfTruth,WoodCarvings,RoomFullOfCheese}.cs`、`CreatureCmd`与`Runs/CardCreationOptions.cs`。参考HEAD仍为`2089659bd07920088b93226136c72b6b6ee590ad`，工作区干净，没有修改其玩法或元数据。

`dev-run`新build=`0e869827460f0b2c`。冻结单幕`dev`仍为规则`a1eb707babc8ae47`／build`10734bae132a3f17`；`dev-map`因新增事件IR、Run字段及公开序列迁移为规则`b38cc1b627b9a9a5`／build`dd928a0e66ac250a`。历史身份不改写，集中golden仅迁移本次受影响的两项。

```powershell
dotnet build AutoTheSpire.sln --artifacts-path .artifacts/act1-events-20261017
dotnet .artifacts/act1-events-20261017/bin/Differential/debug/Differential.dll event-content --output .audit/act1-events-20261017/event-content-final
dotnet .artifacts/act1-events-20261017/bin/Differential/debug/Differential.dll test --suite normal-tablet --output .audit/act1-events-20261017/delivery-normal-tablet
python -X utf8 .audit/act1-events-20261017/public_tablet_replay.py
```

solution构建rc0、0错误。Engine99、Slice运行检查859＋源码检查1、net421、Python事件检查12及独立事件12组通过。九组原有严格套件和新增normal-tablet均rc0；regression仍为16双侧＋1单侧接线自检。原ironclad-act1保留1passed＋1fault、rc1，原因仍是暂缓Phrog召唤，成功前缀未有差异。act-transition的三种子／三幕独立核对仍rc0。文档检查658份、0问题，`git diff --check`通过（既有CRLF提示不计空白错误）。

完整验证结果与构建／Host摘要汇总至`delivery-summary.json`；逐suite返回码见`suite-results.json`。本报告纳入主清单后进入records，日期记录只保留本次证据，不维护第二份当前任务表。
