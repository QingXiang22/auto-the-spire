# mod-the-spire 协作约定

本文件适用于 `mod-the-spire/` 及其子目录，在根 [AGENTS.md](../AGENTS.md) 之上补充本模块更具体的约定。

## 任务认领与交接

必须执行根[任务与文档管理强制规则](../AGENTS.md#任务与文档管理全仓库强制)。从[总入口](../docs/README.md)读取[主清单](../docs/review/tofix/README.md)本线已就绪任务/P优先级，只认领一个完整机制包；登记会话/目录/下一可验成果，核对已有交付，不从历史报告的待修表重做。base实现方法按[工作包](../docs/design/mod-the-spire/base-spire-implementation-plan.md)及对应源依据，当前状态不复制到模块README或AGENTS。

本线负责内容、领域前端/IR、正式工厂与Run，B/C同步提供对拍与公开接线；不自行修改其他线生产文件。每包一体交付行为/检查/文档，回写主清单对应ID及综合交接；缺参考只标对应比较未验证，继续独立实现。父项关闭、完整身份、core缺口和优先级调整交负责人，其他mod需用户明确指定范围。

## 实现边界

当前ENV实施必须遵守[实现约束](../docs/design/environment-implementation-constraints.md)：SinglePlayer统一正式组包/会话工厂，ContentSource为正式内容单一来源；状态在Engine类型化存储，禁止另写调度器或隐藏的C#RunState。声明/加载/行为/对拍/公开出口分开结算；未支持内容不能静默省略、回退.author/Legacy或写空效果。内容包设计与覆盖情况见 [mod-the-spire spec](../docs/design/mod-the-spire/spec.md)。

## 领域与规格驱动开发

新增共享机制先写或补充对应机制规格：领域术语、输入/前置条件、Engine状态归属、执行与hook/随机顺序、公开结果、未支持范围和能区分错误实现的案例。实现和判别引用同一契约；一族机制一份规格，组合已有能力的内容引用它，不逐卡/事件新建规格、任务表或镜像测试。

内容作者使用完整领域操作；普通事件优先继承 `ChoiceEventModule`，只描述选项、可用条件和效果，复用 `Domain/Run/RunResources`。Role、当前事件守卫、phase、关闭与死亡归共享编译器。新增已有能力内容不重复写金币/生命字段、调度、随机算法或收尾；缺共享能力时先给对应主责最小源案例。领域对象仅存编译符号/定义，局内可变状态仍属于Engine。参见[事件领域契约](../docs/design/mod-the-spire/event-domain/spec.md)。

## 优先复用已有领域能力

- 开工先从[公共能力入口](../docs/design/mod-the-spire/room-compile-reference.md#按操作查找已有公共能力)查本次操作；按需要读取对应方法和契约，不为找一个方法通读全仓库。已有能力覆盖所需语义时必须复用，不在内容叶另写一套字段更新、实例复制、牌堆迁移或hook分派。
- 判断覆盖的是完整行为及其适用范围。`CardDefinitions.New`只提供新实例字段，`PotionInventoryRules.Acquire`只领取该模块的奖励药水，`CloseVisit`只恢复旅行阶段；不能把它们当作完整的获得卡牌、任意药水获得或房间结算。能力缺少必要语义时向对应机制主责补充契约和实现，再迁移真实调用方；不复制旧实现，也不使用范围不符的方法凑合。
- 公共方法按领域操作归属，例如获得遗物、获得金币、升级卡牌。封装一起维护的状态、顺序和hook；调用方保留自己的选项资格、价格/奖励计算及访问收尾。不新增汇集所有玩法的`Utils`或含大量房型开关的万能方法。
- 新共享能力一并登记准确调用入口、前置条件、状态归属、已支持与未支持范围，并用真实调用方验证。必要判别放在共享契约层；组合已有能力的内容不逐项新增重复检查。初始化和战斗永久提交等状态装配可以直接写字段，但不能用“初始化”绕过本应发生的获得/移除行为。

## 内容横向扩展

普通卡牌的效果、normal/upgraded与hooks优先组合已有数值、控制流、选择、实例操作和Power规则。`chooseCard.onSelected`可顺序组合升级、复制和移动；新组合不新增AuthorStep、按卡ID的C#分支或专用执行器。确有共享语义不足的特殊机制，先在[原语契约](../docs/design/mod-the-spire/card-primitives/spec.md)说明源顺序及缺口，再交付独立领域能力；不要为复用而改变随机流、决策或hook顺序。

卡牌按 `ContentSource/*-cards.spire`、怪物按 `ContentSource/enemies*.spire` 自动发现，新增内容可放独立文件；使用已有机制时，不在组包器、net或运行器添加逐卡/逐怪物ID分支。源卡池与遭遇池的资格、顺序和行为定义分开维护，自动加载不等于自动进入原版池。

地图房型通过 `Slice/Rooms/MapRoomEntry` 组合进入处理器；问号节点判定出的实际房型与原地图节点类型分开保存，处理器不改写原节点类型。具体事件分别实现 `IEventRoomModule`，由 `EventRoomCatalog` 显式组合；注册处只列模块，不堆各事件效果、选项或按ID分派的手写switch。问号概率/事件池选择、事件访问生命周期和各事件内容分别归属对应模块。新增已有能力事件只添加独立模块和组合登记；新增共享能力再修改相应机制。源池选到尚未实现的事件必须明确报告，不以缩池、空事件或替代事件绕过。

接口与可运行的两个独立事件例子见[房间接口参考](../docs/design/mod-the-spire/room-compile-reference.md#横向注册地图入口与事件)和[接入指南](../docs/design/mod-the-spire/how-to-add-a-room.md#横向扩展问号事件)。
