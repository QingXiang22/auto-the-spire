# 事件访问与资源行为要求

本规格保留 ENV-T09 的源事件资格、资源顺序和访问行为要求；不保存旧开发计划或实现状态。未恢复的正常 Run 和源事件须明确失败；当前 DSL 接口见[访问指南](../../../areas/base/how-to/run-and-rooms.md)与[行为参考](../../../areas/base/reference/card-behavior.md)。下列旧接口名称仅表示概念操作，不承诺同名 API 已存在。

## 领域边界与语言

问号节点判定出的房型与具体事件的执行分开验收：Unknown层只负责概率、房型和入口复用；EventVisitLifecycle负责完整源队列、资格与访问计数；EventRoomCatalog负责具体事件Enter和选项。事件内容可以在已声明的Run资源／牌组初态下，直接经真实目录Enter执行并与独立参考比较，不依赖先完成地图路径或战斗。此类验证记录须标注受控访问，不代替正常Run；整局轨迹用于最后集成验证，不决定事件内容的逐项开发顺序。

| 领域 | 本包拥有的事实和操作 | 交给其他领域 |
| --- | --- | --- |
| 整局资源 | Run金币与生命权威；获得金币、脱战不可格挡/无加成伤害、动作末尾死亡结算 | 战斗伤害/格挡/Power；牌组、遗物获得等后继能力 |
| 事件访问 | 当前事件标识、选择合法性、一次选择、选择后继续或结束 | 问号赔率、源队列、资格和随机抽取；其既有模块保持主责 |
| 内容 | 初始化变量、选项标识/可用条件、按顺序组合领域操作 | Role、当前事件守卫、phase数值、重复选择拒绝、死亡终局 |

模块和内容定义只存编译期符号/函数，所有局内数值、暂停与续接都由Engine维护。领域命令生成IR，不运行C#玩法，不引用sim运行时。

## 规格到行为的对应

| 约定ID | 前置/输入 | 可观察结果/顺序 | 来源与检查 |
| --- | --- | --- | --- |
| EVT-CHOICE-01 | 已进入当前事件，提供独立选项定义 | 统一编译Role及当前事件/phase守卫；只有本事件可用选项合法 | sim EventModel.ChooseOption/CurrentOptions；两个独立事件互不串选 |
| EVT-CHOICE-02 | 选项效果按顺序完成，模式为Finish | 清当前事件、恢复旅行；旧选项再次提交被拒 | EventModel.Finish + EventRoom.Exit；关闭后选择不再合法 |
| EVT-CHOICE-03 | 选项模式为Continue | 保留访问，由后继选项/页面继续；致死仍结束整局 | EventModel.SetOptions/IsFinished；不把所有选项强制关闭 |
| RUN-GOLD-01 | Expr形式的整数金币变化，现有全解锁Ironclad支持内容 | 请求值求值一次；Modify→AfterModifying→正值才加金币→AfterGained；零/负最终值不扣金币 | sts2 PlayerCmd.GainGold；共享编译hook以Engine指令表达，不在叶中重复接入 |
| RUN-DAMAGE-01 | 现有非战斗事件，对Run造成不可格挡/无加成的非负整数伤害 | 输入求值一次，生命最低0；不得读取旧战斗格挡/Power或回写旧Player生命覆盖Run | SunkenStatue.DiveIntoWater/CreatureCmd.Damage；7点基础损失及过量损失 |
| RUN-HEAL-01 | 活着的Run玩家，非负整数治疗 | 输入求值一次，最多MaxHp；负值和尝试复活明确拒绝 | CreatureCmd.Heal/Creature.HealInternal的存活正值范围；不是营火专用倍率或复活实现 |
| RUN-DEATH-01 | 本次选择全部效果后Run.Hp为0 | 关闭事件访问，再发布既有Defeat/loss，非战斗Turn=0；终局不提供旅行/选项 | 源CreatureCmd.Damage/死亡路径；致死不以旅行phase2冒充继续游戏 |
| RUN-AUTH-01 | 使用已有能力新增普通事件 | 事件文件只提供选项/效果，组合处仅列模块；不修改地图分派、Composer或逐事件net表 | 独立真实事件与示例共用编译器；文档按接口直接接手 |

金币操作保持源 ModifyGoldGained→AfterModifyingGoldGained→正值提交→AfterGoldGained 顺序。参与者未实现时不能宣称完整修正链已支持；叶事件不按遗物 ID 分支。

本包脱战伤害仅覆盖已声明的Unblockable|Unpowered、当前无相应常驻修正参与者的范围；完整ModifyDamage/两阶段HpLost/死亡阻止等参与者随对应能力交付，不能把本命令用作所有战斗伤害或复活。召唤/复活仍按主清单暂缓。后续伤害修正接入时扩展这一命令的真实源阶段，而非在事件中复制伤害算法。

遗物获得、永久牌组操作由后继[整局公共操作约定](../run-domain/spec.md)拥有，依赖按事件需要显式传入；普通事件资源服务与其他房间来源共用，不能在事件内新建默认无hook副本。复杂事件战斗与跨幕仍非本包范围。SunkenStatue的SwordOfStone选项依赖继续明确报告，不缩源池、不伪造选项效果；不能用通用服务容器把全部状态塞给事件作者。

## 源事件资格（ENV-T09-PR05）

源池标识、资格和可执行效果分别维护。资格按源 `IsAllowed` 用 DSL 表达，不依赖效果模块是否已注册；覆盖规则须显式声明。只有源码默认允许的标识才能返回 true；尚未实现的资格必须以源标识明确失败，不能默认放行或跳到下一个事件。完整队列、已访问跳过、计数与随机顺序保持。

当前表限于 Overgrowth/第一幕单人范围：按幕限制排除 PotionCourier、StoneOfAllTime 等；UnrestSite 使用 Hp×10≤MaxHp×7，WhisperingHollow/TeaMaster 使用源金币阈值，TheFutureOfPotions 使用真实药水栏数量。WoodCarvings要求存在可移除基础永久牌，SlipperyBridge要求实际楼层超过6且有可移除牌；MorphicGrove要求至少100金币和两张可变换永久牌。ByrdonisNest、LuminousChoir 的资格尚未实现，触及时明确失败。多幕配置读取真实幕索引，后续幕资格尚未补齐时明确失败，不能套用第一幕规则。

判别复用 UnknownRoomChecks：无效果模块仍跳过第一幕禁用事件；56/80 与57/80区分70%生命边界；未支持的资格不记访问并明确失败。受控队列检查只证明选择语义，不能替代正常 Run 的事件效果、独立比较或完整通关。

## 第一幕多页与卡牌奖励

JungleMazeAdventure进入时按事件流依序抽两次NextFloat(-15,15)，分别加150／50并按源decimal→整数金币截断，结果缓存为Run字段。JOIN_FORCES直接得缓存金币；SOLO_QUEST先消费三项视觉列表StableShuffle的两次事件流抽取，再损失18生命、获得缓存金币，最后结束／死亡。即使不渲染视觉，也不能漏掉共享事件流消费。原生参考已登记省略SOLO的两次视觉抽数，必须单列该已知参考偏离，不修改参考或额外补抽伪造双侧一致。

MorphicGrove资格为至少100金币且至少两张可变换永久牌。LONER增加5最大生命并回复5；GROUP先失去全部金币，再同时选两张可变换永久牌，按答案顺序依次使用事件流变换，新实例追加。候选不超过所需数量时沿源自动选全部（包括空域）；足够多时只发布一次恰选两张的Engine决策，不将其拆成两个单选。公开多选缺口仍按T03处理。

TabletOfTruth 的页计数和缓存费用属于 Engine Run 字段。初页可回复20生命并结束，或解读；解读按3、6、12、24、当前MaxHp−1扣生命上限。前四页仅在存在可升级永久牌时用事件流随机升级一张（单候选仍抽），第五页升级全部剩余牌且不抽数。费用达到生命上限时先将上限降为1、击杀玩家，不升级。解读后仅保留继续／放弃，第五次或死亡关闭访问；回复和放弃不抽随机数。

`RunResources.LoseMaxHp(scope, run, amount)` 快照非负输入，先以脱战伤害扣除超过新上限的当前生命，再将MaxHp最低设为1。仅覆盖当前无相关修正／死亡阻止参与者的非卡牌来源；历史和完整hook仍属后继范围。

RoomFullOfCheese 的 Gorge 使用完整Ironclad池中的单人普通牌，按源顺序无放回生成8个候选。每张用玩家Rewards选定义；源ForNonCombatWithUniformOdds自带NoUpgradeRoll，不掷升级、不改变稀有度偏移，共8次抽数。Engine以真实候选实例执行恰好二选，选中实例按答案顺序加入永久牌组，未选候选销毁后关闭访问。Search的ChosenCheese获取与AfterCombatEnd尚未支持时保留选项并明确失败，不能伪造遗物获得。

WoodCarvings 的第一幕资格为存在可移除基础永久牌，与可执行效果独立；本包不据资格通过宣称其附魔／定向变换效果已支持。

## 交付条件

1. 使用不可变选项定义和共享编译器迁移SunkenStatue以及两个独立例子；实际作用于Engine Run资源。内容叶不直接设置金币/生命、Role、访问phase或Terminal。
2. 检查覆盖Gold修正/顺序与一次求值、零/负金币、不同行为事件隔离、Continue/Finish、非致死与致死伤害及关闭后再次选择拒绝；复用现有检查，不逐常量堆镜像测试。
3. 正常seed `ENV-T03-SEED-L`通过真实SunkenStatue选择并继续；保持其106金币/-7生命源值和随机消费，不把受控致死case当正常死亡整局轨迹。
4. 串行当前共同构建、Engine/Slice/net及严格17，标识变化仅迁移有依据的受影响基线；独立参考比较缺口仍分列。内容已加载、行为已验证、公开可用、独立原版双侧分别汇报。

## SlipperyBridge连续访问

SlipperyBridge按源IsAllowed要求TotalFloor>6且存在可移除永久牌；第一幕TotalFloor由已访问节点数取得，不能直接用MapFloor的Row+1替代。进入时在可移除的非基础牌中均匀预览一张，没有候选则退回全部可移除牌；预览公开真实永久实例。每次抽取使用该事件的独立流，单候选也消费一次。

Overcome复用RunDeck.Remove移除预览的同一实例并结束访问。HoldOn按3、4、5……支付无格挡/无加成伤害，记住上一预览的实例，再从未跳过且定义不同的可移除牌中重抽；为空则退回全部可移除牌。跳过集合保持实例集合语义，仅作内部过滤，不发布；费用次数与当前预览公开。HoldOn保持事件访问，致死时仍完成源声明的重抽，再沿共享事件收尾发布Defeat。普通移除、随机选牌和外部伤害均调用共享领域能力。
