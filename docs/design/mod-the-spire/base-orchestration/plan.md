# Base 编排实施计划

先将语义权威从巨型局部作用域拆为可组合的能力模块，保持Engine生成IR、Session执行IR。

1. 建立效果词法编译环境；前端校验引用范围，编译器保存快照槽位，移除规则包构建中的savedCounts字典。
2. 提取卡schema/建卡与牌区过程，按少量类型化符号接入；真实替代实例、固定位置与克隆来源分列。
3. 建立战斗/回合事件寿命与出牌开始/完成记录，修复当前重置/唯一集合问题。
4. 将完整出牌流程移到共享pipeline，以显式上下文/策略生成手动与自动IR；叶效果只获得请求能力。
5. 接通结构化生成/抽牌/自动出牌及对应源卡，验证新接口能局部扩展；现有敌人/奖励/房间接入独立保留。
6. 串行局部→非地图→完整门槛，按实际新身份迁移golden并记录首个差异；更新作者/维护指南、模块职责和证据。

维护入口：能力实现仍在`Effects`和`Orchestration`；编译绑定按[模块参考](../package-compilation-reference.md)进入`Cards/Compilation/CardCompilation`、`TurnCompilation`等领域模块。`CombatPackageComposer`负责声明/Body顺序和参数转发，原`CombatSlicePackage`只保留兼容入口。

模块的C#对象只持构建期符号与源内容，不持运行态。接口按Card/Pile/Event/Play能力分组，不用万能context承载整个Run。复用现有声明顺序和公共出口，必要语义变化以源案例裁决。

依据为本地sim的CardModel/CombatEngine/CardPileCmd/CardCmd/AutoPlayCmd及同版本sts2源码；两者差异不以base输出反推期望。

下一回合能量增量：在PowerRules注册布尔机制，PowerRuleRuntime提供AfterEnergyReset过程，`Combat/Compilation/TurnCompilation.PlayerTurn`在能量重置后、BeforeHandDraw前调用。通过现有能量收益门控按层数获能并销毁实例，不引入逐卡状态或core新hook。正式Power/Outmaneuver独立源文件与9项机制检查分别交付，24项现有编排检查作为受影响回归；与内容线使用独立artifacts-path并发构建，公开身份及完整门槛留批次集成，未执行范围入证据。
