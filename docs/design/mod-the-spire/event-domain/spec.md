# 事件选项与整局资源领域契约

本规格定义普通事件的领域接口与首批整局资源命令，归属[ENV-T09](../../../review/tofix/README.md#env-t09)，复用当前问号访问与Engine IR。阶段、认领和验收只维护主清单。它是实现和检查的共同依据，不将设计目标写成已交能力。

## 领域边界与语言

| 领域 | 本包拥有的事实和操作 | 交给其他领域 |
| --- | --- | --- |
| 整局资源 | Run金币与生命权威；获得金币、脱战不可格挡/无加成伤害、动作末尾死亡结算 | 战斗伤害/格挡/Power；牌组、遗物获得等后继能力 |
| 事件访问 | 当前事件身份、选择合法性、一次选择、选择后继续或结束 | 问号赔率、源队列、资格和随机抽取；其既有模块保持主责 |
| 内容 | 初始化变量、选项身份/可用条件、按顺序组合领域操作 | Role、当前事件守卫、phase数值、重复选择拒绝、死亡终局 |

模块和内容定义只存编译期符号/函数，所有局内数值、暂停与续接都由Engine维护。领域命令生成IR，不运行C#玩法，不引用sim运行时。

## 规格到行为的对应

| 契约ID | 前置/输入 | 可观察结果/顺序 | 来源与判别 |
| --- | --- | --- | --- |
| EVT-CHOICE-01 | 已进入当前事件，提供独立选项定义 | 统一编译Role及当前事件/phase守卫；只有本事件可用选项合法 | sim EventModel.ChooseOption/CurrentOptions；两个独立事件互不串选 |
| EVT-CHOICE-02 | 选项效果按顺序完成，模式为Finish | 清当前事件、恢复旅行；旧选项再次提交被拒 | EventModel.Finish + EventRoom.Exit；关闭后选择不再合法 |
| EVT-CHOICE-03 | 选项模式为Continue | 保留访问，由后继选项/页面继续；致死仍结束整局 | EventModel.SetOptions/IsFinished；不把所有选项强制关闭 |
| RUN-GOLD-01 | Expr形式的整数金币变化，现有全解锁Ironclad支持内容 | 请求值求值一次；Modify→AfterModifying→正值才加金币→AfterGained；零/负最终值不扣金币 | sts2 PlayerCmd.GainGold；共享编译hook以Engine指令表达，不在叶中重复接线 |
| RUN-DAMAGE-01 | 现有非战斗事件，对Run造成不可格挡/无加成的非负整数伤害 | 输入求值一次，生命最低0；不得读取旧战斗格挡/Power或回写旧Player生命覆盖Run | SunkenStatue.DiveIntoWater/CreatureCmd.Damage；7点基础损失及过量损失 |
| RUN-HEAL-01 | 活着的Run玩家，非负整数治疗 | 输入求值一次，最多MaxHp；负值和尝试复活明确拒绝 | CreatureCmd.Heal/Creature.HealInternal的存活正值范围；不是营火专用倍率或复活实现 |
| RUN-DEATH-01 | 本次选择全部效果后Run.Hp为0 | 关闭事件访问，再发布既有Defeat/loss，非战斗Turn=0；终局不提供旅行/选项 | 源CreatureCmd.Damage/死亡路径；致死不以旅行phase2冒充继续游戏 |
| RUN-AUTH-01 | 使用已有能力新增普通事件 | 事件文件只提供选项/效果，组合处仅列模块；不修改地图分派、Composer或逐事件net表 | 独立真实事件与示例共用编译器；文档按接口直接接手 |

金币hook的编译期接缝严格保持源`ModifyGoldGained`、`AfterModifyingGoldGained`、`AfterGoldGained`顺序。当前正式可执行遗物为BurningBlood/Pear/Mango/OldCoin，没有相关金币修正参与者，正式默认链为恒等/空；不据此宣称其余遗物或所有hook已实现。具备相应规则后在能力装配处接入，不让事件按遗物ID分支。

本包脱战伤害仅覆盖已声明的Unblockable|Unpowered、当前无相应常驻修正参与者的范围；完整ModifyDamage/两阶段HpLost/死亡阻止等参与者随对应能力交付，不能把本命令用作所有战斗伤害或复活。召唤/复活仍按主清单暂缓。后续伤害修正接入时扩展这一命令的真实源阶段，而非在事件中复制伤害算法。

遗物获得、永久牌组操作由后继[整局公共操作契约](../run-domain/spec.md)拥有，依赖按事件需要显式传入；普通事件资源服务与其他房间来源共用，不能在事件内新建默认无hook副本。复杂事件战斗与跨幕仍非本包范围。SunkenStatue的SwordOfStone选项依赖继续明确报告，不缩源池、不伪造选项效果；不能用通用服务容器把全部状态塞给事件作者。

## 交付条件

1. 使用不可变选项定义和共享编译器迁移SunkenStatue以及两个独立例子；实际作用于Engine Run资源。内容叶不直接设置金币/生命、Role、访问phase或Terminal。
2. 判别覆盖Gold修正/顺序与一次求值、零/负金币、不同行为事件隔离、Continue/Finish、非致死与致死伤害及关闭后再次选择拒绝；复用现有检查，不逐常量堆镜像测试。
3. 正常seed `ENV-T03-SEED-L`通过真实SunkenStatue选择并继续；保持其106金币/-7生命源值和随机消费，不把受控致死case当正常死亡整局轨迹。
4. 串行当前共同构建、Engine/Slice/net及严格17，身份变化仅迁移有依据的受影响基线；独立对拍缺口仍分列。内容已加载、行为已验证、公开可用、独立原版双侧分别汇报。
