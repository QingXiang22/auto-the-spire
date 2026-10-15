# base公共领域方法复用审阅

日期：2026-10-12。负责人：Codex `/root`。任务归属：[ENV-T09](../../tofix/README.md#env-t09)共享方法审阅与规范维护；永久牌组命令涉及[ENV-T06](../../tofix/README.md#env-t06)。本次只读实现并维护规范、开发入口和本记录，未实施生产重构、派发会话或修改版本。当前任务状态、认领与排期只在主清单。

## 判断依据

复用已有完整领域操作可以减少开发者需要记住的规则顺序，也使后续规则修正只维护一处。目前的缺口同时包括“还没有完整公共操作”和“已有操作尚未统一采用”。因此不能只增加方法数量；交付需要有真实调用方，并让新增内容从接口参考直接找到入口。

当前已有Run资源、普通事件选项、卡牌配方、战斗牌堆过程、药水库存与访问上下文。准确入口及边界集中在[公共能力表](../../../design/mod-the-spire/room-compile-reference.md#按操作查找已有公共能力)；规范见[mod AGENTS](../../../../mod-the-spire/AGENTS.md#优先复用已有领域能力)。卡牌配方不等于获得卡牌，字段初始化不等于获得遗物，恢复旅行阶段不等于完整房间关闭。

## 实际重复与封装方向

### 获得遗物：已有两个真实使用方

[商店RegisterActions](../../../../mod-the-spire/base-spire/BaseSpire/Slice/ShopRoomWiring.cs)与[宝箱RegisterActions](../../../../mod-the-spire/base-spire/BaseSpire/Slice/TreasureRoomWiring.cs)都创建遗物，再各自遍历AfterObtained并实现GainMaxHp/GainGold。商店写了回复封顶分支，宝箱直接增加当前生命；在当前合法Hp≤MaxHp且增量相同的状态下，这个写法差别不证明已有行为错误，但已存在两处维护同一领域规则的负担。

源[RelicCmd.Obtain](../../../../sts2/src/Core/Commands/RelicCmd.cs)包括加入玩家遗物、非可叠加遗物从玩家与共享袋移除，然后AfterObtained。[CreatureCmd.GainMaxHp](../../../../sts2/src/Core/Commands/CreatureCmd.cs)使用实际上限增量并调用Heal；[PlayerCmd.GainGold](../../../../sts2/src/Core/Commands/PlayerCmd.cs)使用金币hook阶段。

建议按这条源顺序补一个遗物获得领域命令，拥有实例创建、袋移除及AfterObtained分派，并复用Run资源命令。领取方只保留来源身份、资格、商店扣款顺序、已领取/已售和访问关闭。获得前的抽取与获得后的效果应分开：当前商店生成商品已经移除相应袋成员，宝箱进入已消费共享袋，宝箱领取才移除玩家袋；不能为了统一把这些抽取动作和RNG挪到领取时。当前受支持不可叠加遗物可以先成为明确范围，未支持的叠加/替换/移除/hook参与者仍须显式说明。

### Run资源：公共命令存在，但调用方尚未统一

[RunResources.GainGold](../../../../mod-the-spire/base-spire/BaseSpire/Slice/Domain/Run/RunResources.cs)已统一输入快照、Modify、AfterModifying、正值写回和AfterGained。[RewardWiring.RegisterTakeGold](../../../../mod-the-spire/base-spire/BaseSpire/Slice/RewardWiring.cs)、宝箱开箱及商店/宝箱OldCoin效果仍直接增加Run.Gold。当前支持内容没有金币修正参与者，不能据此宣称已观察到金币错误；新增参与者时这些入口却会绕过同一机制。

下一实现应把这些入口接到同一个已配置资源服务，而不是各自创建默认无hook的实例。营火保留其源回复量计算，再调用共享回复；遗物增加上限应补GainMaxHp完整操作。商店四种支付仍直接减金币，适合补源LoseGold/Spent契约，但不能用负数GainGold替代：源LoseGold有下限和不同损失类型，购买资格及“先给物品还是先付款”的顺序仍归各商品操作。

### 永久牌组：配方已共享，获得与移除尚未封装

[CardDefinitions](../../../../mod-the-spire/base-spire/BaseSpire/Slice/Orchestration/CardDefinitions.cs)已在新实例和升级中维护变量、字段及源排序键。[奖励领牌](../../../../mod-the-spire/base-spire/BaseSpire/Slice/RewardWiring.cs)将候选实例加入长期牌组，商店创建长期实例，商店删牌直接Destroy所选实例。奖励还拥有未选候选清理；删牌还拥有正式Decide、费用和次数。

建议补永久牌组获得/移除领域命令，明确接收已有实例与创建新实例的区别，集中永久成员关系及对应获得/移除hook。调用方保留奖励候选清理、选牌决策和费用；不能把战斗牌堆CardPileProcedures当作永久牌组命令，也不能为了统一创建而丢失玩家已选实例身份。与事件/古代人新增长期牌组行为一同交付，不先造尚无消费者的通用框架。

### 药水：继续复用库存，按新来源需要扩展

[PotionInventoryRules](../../../../mod-the-spire/base-spire/BaseSpire/Slice/PotionInventoryRules.cs)已经提供容量、首空槽选择和奖励药水迁移；奖励和商店复用了Acquire。尚不支持从任意事件来源直接领取已有实例；新增这类消费者时可将首空槽及入栏操作提为接收明确实例的能力，奖励Acquire保留适配。不能将当前“取Rewards首项”当成任意来源的公共方法，也无需在当前没有新消费者时预建所有药水接口。

## 保留的模块责任

商店、奖励、事件和营火的关闭条件不同，继续由各自模块拥有，复用窄的CloseVisit。金币增益/损失、战斗副本/永久牌组、手动弃牌/普通移动分别有不同语义。公共方法按领域和生命周期归属，不建立集中所有操作的Utils，不通过房型开关决定行为，不把初始化状态装配与真实获得混用。

共享机制检查验证可区分的输入快照、顺序、容量、实例身份及hook；内容组合沿用这些证据，不为简单转发方法或每个数值另建重复检查。以后新增能力时同时维护准确签名和范围，让作者只需阅读所用能力的契约与本内容源方法。

## 本次修改与验证

修改mod AGENTS复用规范、base开发指南开工路径、房间接口参考的公共能力表及主清单对应维护条目；本记录由记录索引导航。生产代码保持原有实现，本记录不表示上述重构已完成。

`python tools/doc-check/check_docs.py`扫描449份Markdown，返回1，仅报告两份既有交接中的4处`#L7`/`#L181`/`#L21`/`#L25`无效锚点，本次新增链接未报错。`git diff --check`返回0。未运行构建、Engine/Slice/net规则套件或新正常Run。本次依据是当前工作树与源规则的静态审阅，不增加运行时一致性或性能声明。
