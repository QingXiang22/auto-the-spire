# Run与房间编译接口

文档类型：参考（Reference）。适用于base地图房间的C#编译接口。实例调用见[房间操作指南](how-to-add-a-room.md)。本页不维护任务状态。

## 共用上下文

[RoomCompileContext](../../../mod-the-spire/base-spire/BaseSpire/Slice/RoomCompileContext.cs)的完整构造签名：

```csharp
RoomCompileContext(EntityTypeSymbol RunType, CollectionSymbol Runs,
    FieldSymbol Hp, FieldSymbol MaxHp, FieldSymbol Gold,
    CollectionSymbol LongTermDeck, MapRoomWiring Map)
```

由[RoomCompilation.RoomServices](../../../mod-the-spire/base-spire/BaseSpire/Slice/Compilation/RoomCompilation.cs)创建一次，`RoomCompilation.Rooms`传给具体房间。所有字段/集合必须来自同一个正在编译的包；它们是符号，不能当作可变C#游戏对象。

| 成员 | 用途/前置条件 |
| --- | --- |
| `CurrentRun(IrScope scope) → Expr` | 生成读取Run集合首实例的表达式；正式组包保证Run单例 |
| `At(IrScope scope, Expr run, int phase, string pointType) → Expr` | 同时比较访问阶段与房型；房型取实际房型（问号房判定结果）或节点枚举成员，见 `MapRoomWiring.AtRoomType`。`pointType`使用源枚举名，如`Treasure` |
| `CloseVisit(IrScope scope, Expr run) → Inst` | 生成将访问阶段写为`TravelReadyPhase`的指令；调用者先完成本房间提交和选择 |
| `Hp / MaxHp / Gold` | 长期Run字段；通过`scope.Field`读取，通过`scope.SetField`生成写入 |
| `LongTermDeck` | 长期卡实例集合；战斗牌副本及临时牌不在该集合中 |
| `RunType / Runs / Map` | 声明房间字段、定位Run及接入地图所需的编译期符号 |

`CloseVisit`只恢复旅行阶段，不清理候选、不标记遗物已领取、不提交战斗副本、不判整局胜利。这些操作由相应模块拥有。

## 横向注册地图入口与事件

相关接口位于 `BaseSpire/Slice/Rooms/`，都是编译期对象：

| 接口 | 语义 |
| --- | --- |
| `MapRoomEntry(string PointType, Func<IrScope, Expr, Expr, Inst[]> Enter)` | 按地图类型登记进入编译器；回调参数为scope、Run和目标节点 |
| `MapRoomEntry.Standard(map, startNextRoom, treasure, shop)` | 按现有顺序提供营火、普通战斗、精英、宝箱、商店和Boss入口 |
| `MapRoomEntry.Visit(map, scope, run, node, int? phase = null)` | 生成当前节点、已访问和楼层更新；有phase时一并写入访问阶段 |
| `MapRoomWiring.RegisterChooseMapPoint(package, runs, entries)` | 编译所有入口，重复类型拒绝，未登记类型显式失败；进入前按 `DirectRoomType` 写 `Run.ActualRoomType`；旧五参数重载调用Standard |
| `MapRoomWiring.AtRoomType(scope, run, pointType)` | "本次访问是不是这个房型"：`Run.ActualRoomType` 命中（问号房判定结果）**或**地图节点类型命中；`RoomCompileContext.At` 用它，营火/商店/宝箱/战斗守卫因此都认实际房型 |
| `UnknownRoomWiring(package, run, map)` | 问号概率模块：声明四个非事件赔率字段（float32 位型，默认值即源初值）与判定资产 `base-spire:UnknownMapPointOddsRoll` |
| `UnknownRoomWiring.BlacklistShop(scope, run)` | 源 `RunManager.BuildRoomTypeBlacklist`（上一间房是 Shop 或当前节点子节点全是 Shop）；必须在 `Visit` 之前求值 |
| `UnknownRoomWiring.RollRoomType(scope, run, blacklistShop)` | 抽一笔 `base-spire:run:unknown_map_point`、纯计算判定并写回赔率；返回命中房型的 local slot |
| `UnknownRoomEntry.Create(map, standard, odds, events, context, catalog)` | 问号入口：黑名单 → 判定 → 写 `Run.ActualRoomType` → 按实际房型复用 Standard 入口或走事件路径；未接线房型显式失败 |
| `IEventRoomModule.Id / RegisterActions / Enter` | 一个具体事件的身份、选项注册和进入指令；各事件可放独立文件 |
| `ChoiceEventModule.Id / Enter / Choices` | 普通事件作者入口；只描述初始化及选项，共享RegisterActions生成Role、身份/phase/存活守卫和选择尾 |
| `EventChoice(actionName, effects, available = null, completion = Finish)` | 不可变选项定义；效果按序编译，可选资格谓词只影响本选项 |
| `EventEffectContext.GainGold / DamageOutsideCombat / HealLivingPlayer` | 调用Run领域命令生成Engine IR；输入快照、资源边界与尾部死亡由共享层负责 |
| `EventCompletion.Finish / Continue` | 存活时结束或保留访问；致死时两种模式都先清理访问再发布Defeat |
| `EventCompileContext.Resources` | 正式装配提供Domain/Run/RunResources，含Run字段、已有Defeat符号和可选GoldGainHooks；普通事件缺此能力时建包期失败 |
| `EventSourceIdentity.ModuleId(源类名)` | 源事件身份（`RunEventQueue.SourceId`）↔ 注册模块 ID 的唯一转换点；不在源池里的名字建包期失败 |
| `IEventEligibility.IsAllowed(scope, context, run)` | 可选：源 `EventModel.IsAllowed` 的覆盖声明，供队列资格跳过使用 |
| `EventRoomCatalog(modules)` | 按提供顺序组合模块；空ID或重复ID拒绝。`Ids`只代表可执行模块，不能冒充完整源事件池 |
| `catalog.RegisterActions(package, context)` | 依次登记各事件的公开动作，不复制中央动作表 |
| `catalog.Enter(scope, context, run, selectedId)` | 根据已由源选择器选定的ID进入模块，写当前事件和phase；未登记ID显式失败 |
| `EventVisitLifecycle.Declare(package, run, runInitialization)` | 声明事件访问状态：`Run.CurrentEventId`（公开）、`EventsVisited`/`EventPageGold`（隐藏）、`RunVisitedEvents` 集合、事件级随机资产 `base-spire:SourceEventNextInt` |
| `EventVisitLifecycle.Pull(scope, run)` | 源 `ActModel.PullNextEvent`：`eventsVisited % Count` 取 `NextEvent`，按资格与已访问跳过、命中后加入已访问集合；返回选中源身份的 local slot |
| `EventVisitLifecycle.MarkVisited(scope, run)` | 源 `RoomSet.MarkVisited(RoomType.Event)`：事件房进入时 `EventsVisited` +1 |
| `EventRandomAsset.NextInt(scope, 源类名, min, max)` | 源事件级 `Rng.NextInt`：一笔 `SampleInt(base-spire:event:<SLUG>)` + 纯计算换算 |
| `EventCompileContext(room, currentEventId, phase)` | 事件运行状态字段和阶段由正式事件服务（`EventVisitLifecycle.Declare` + `MapRoomWiring.EventPhase`）声明并初始化；此对象只传符号 |
| `context.Active(scope, run, id)` | 同时检查当前事件ID和访问阶段，用于本事件各选项的守卫 |
| `context.Close(scope, run)` | 清当前事件ID并恢复旅行；模块先完成必要选择与永久写回 |

`PointType`使用地图枚举名；进入处理器不改写节点原类型。问号判定的实际房间类型另存 `Run.ActualRoomType`
（源 `MapPointHistoryEntry.RoomType`），营火/商店/宝箱/战斗的适用守卫经 `AtRoomType` 读它。
事件池、概率、资格和源顺序由问号/生命周期模块维护；目录只负责已选身份的执行分派。
额外牌组、药水、遗物、战斗返回等依赖由本事件或事件服务显式传入，不为每个事件修改通用上下文。

`RunResources`只持编译符号与局部名分配计数，不持局内状态。`GainGold`沿Modify、AfterModifying、正值写回、AfterGained顺序；正式默认hook链适用于当前支持内容。`DamageOutsideCombat`限定当前脱战Unblockable|Unpowered范围，伤害修正/死亡阻止等完整参与者尚未支持。`HealLivingPlayer`要求存活且数值非负，最多MaxHp，不实现复活。准确边界及案例ID见[事件领域规格](event-domain/spec.md)。

独立例子在[base-events](../../../docs/examples/base-events/Program.cs)，原版问号流程的实际状态以[主清单](../../review/tofix/README.md#env-t09)为准。

## 房间与阶段

入口定义在[MapRoomWiring](../../../mod-the-spire/base-spire/BaseSpire/Slice/MapRoomWiring.cs)。

| 阶段 | 值 | 归属/合法操作 |
| --- | ---: | --- |
| 开局地图 | 0 | 地图拥有；可选相邻节点 |
| 战斗 | 1 | 战斗与结算链拥有 |
| `TravelReadyPhase` | 2 | 已关闭访问；可选相邻节点 |
| `RewardWiring.RewardPhase` | 3 | 奖励拥有；全部必要选择完成才关闭 |
| `RestSitePhase` | 4 | 营火拥有 |
| `TreasurePhase` | 5 | 宝箱拥有 |
| `ActCompletedPhase` | 6 | 幕完成；不等同于整局终局 |
| `ShopPhase` | 7 | 商店拥有；离店回 `TravelReadyPhase` |
| `EventPhase` | 8 | 事件拥有（`Run.CurrentEventId` 是当前事件身份）；选项完成或关闭后回 `TravelReadyPhase` |

`MapPhase` 的字段域上界随最后一个阶段扩展（当前 `MapRoomWiring.EventPhase` = 8）。新增阶段由地图/流程主责同步更新字段域、守卫、公开语义和运行器消费者。不要在房间叶中分配未协调的数字。

## 动作守卫、候选与故障

| 入口 | 实际语义 |
| --- | --- |
| `ActionBuilder.Requires(Expr)` | 在合法动作枚举和提交前检查守卫；按动作参数绑定后求值。合并房间、库存、金币及容量条件 |
| `ActionBuilder.Candidates(string parameterName, Query)` | 单个实体参数的候选查询；再次调用会覆盖前一次。空域使该动作没有合法绑定 |
| `Choices` | 显式离散参数值域；不等于第二个动态实体集合查询 |
| 决策过程 | 在动作执行中暂停，取得选择后恢复；多步骤选牌等复用已有过程 |
| `IrScope.Fail(string)` | 抛出规则故障，Session成为`Faulted`且不能继续提交；不表达可恢复的普通操作失败 |

实现依据：[候选登记](../../../core-the-spire/Core.TheSpire/Engine/PackageBuilder.cs)、[合法动作与提交](../../../core-the-spire/Core.TheSpire/Engine/Scheduler.cs)。房间作者使用base接缝时同样遵循这些行为；公开候选应来自实际合法绑定，不能只在客户端过滤。

## 资源接缝

### 按操作查找已有公共能力

从本表定位所需能力，再查本页准确签名或链接源码。此处只列现有入口，不表示完整原版行为均已支持。

| 要做的操作 | 现有入口 | 调用时保留的边界 |
| --- | --- | --- |
| 脱战获得金币、伤害、回复 | [RunResources](../../../mod-the-spire/base-spire/BaseSpire/Slice/Domain/Run/RunResources.cs)的`GainGold` / `DamageOutsideCombat` / `HealLivingPlayer` | 生成IR；金币hook链须与当前内容匹配，伤害限定当前Unblockable\|Unpowered范围，回复不复活。选项数值和访问收尾由调用方或事件编译器决定；见[契约](event-domain/spec.md) |
| 支出金币、增加生命上限 | `RunResources.LoseGold(scope, run, amount)` / `GainMaxHp(scope, run, amount)` | 非负输入快照；支出最低0，购买先用Requires校验够钱；增加上限后回复实际增量。历史分类及完整修正参与者未支持 |
| 获得遗物 | [RunRelics.Obtain(scope, run, definition)](../../../mod-the-spire/base-spire/BaseSpire/Slice/Domain/Run/RunRelics.cs) | 受支持不可叠加身份创建→玩家/共享袋移除→AfterObtained复用同一RunResources。Supported是可获得效果名单，抽取和来源收尾另归房间；未知效果明确失败 |
| 永久牌组创建、获得、移除 | [RunDeck.Create / Acquire / Remove](../../../mod-the-spire/base-spire/BaseSpire/Slice/Domain/Run/RunDeck.cs) | Create使用New配方；Acquire保留实例、调用方先移出候选；Remove校验永久成员并先BeforeRemoved。当前无获得替换参与者，源全部hook/历史仍未支持；见[整局契约](run-domain/spec.md) |
| 普通事件选项 | [ChoiceEventModule](../../../mod-the-spire/base-spire/BaseSpire/Slice/Rooms/EventChoices.cs)和`EventChoice` | 共享Role、访问/存活守卫及死亡/结束尾；内容只给初始化、可用条件和效果，事件战斗返回尚非此能力 |
| 新卡字段、复制、升级 | [CardDefinitions](../../../mod-the-spire/base-spire/BaseSpire/Slice/Orchestration/CardDefinitions.cs)的`New` / `Copy` / `Upgrade` | 统一变量、源排序键和升级增量；New/Copy不创建实体或完成长期获得流程，Copy的clone策略与战斗副本策略不同 |
| 战斗牌堆移动、消耗、克隆、变形 | [CardPileProcedures](../../../mod-the-spire/base-spire/BaseSpire/Slice/Orchestration/CardPileProcedures.cs)的`MoveChosen` / `Move` / `Clone` / `Transform` | 仅战斗牌堆。手动选择用MoveChosen保留入手限制、置顶记录与手动弃牌回调；Move不等同手动弃牌，不用于永久牌组移除 |
| 药水容量与奖励入栏 | [PotionInventoryRules](../../../mod-the-spire/base-spire/BaseSpire/Slice/PotionInventoryRules.cs)的`HasSpace` / `Acquire` / `Fields` | Acquire从该模块Rewards取首项，不自行校验所有调用前置条件；调用方先保证有奖励且有空槽。Fields只给实例字段，不完成入栏 |
| 房间适用条件与回到地图 | `RoomCompileContext.At` / `CloseVisit` | At使用本次实际房型；CloseVisit只恢复旅行阶段，房间自己的已领取标记、候选清理、战斗提交或终局判断不能省略 |

正式装配在`RoomCompilation.RoomServices`创建同一个RunResources及RunRelics/RunDeck，商店、宝箱、营火、奖励和事件按需要消费。新增来源传入该服务，不各自创建默认无hook服务。初始审阅见[日期记录](../../review/records/2026-10-12/shared-domain-method-review-2026-10-12.md)，当前范围以[整局公共操作契约](run-domain/spec.md)为准，排期只看主清单。

以下方法生成IR，宿主不直接调用它们。需要本机制时只查对应模块。

| 机制 | 准确调用入口 | 状态归属与限制 |
| --- | --- | --- |
| 营火 | `RestSiteWiring.Register(package, room, resources, recipes)` | 回复量归营火，共享HealLivingPlayer和升级资格；resources为同一个RunResources，recipes为CardDefinitions |
| 宝箱声明 | `new TreasureRoomWiring(package, room, inventory, resources, relicCommands)` | 依赖同一个RunInitializationWiring/RunResources/RunRelics；进入抽取和领取命令分开 |
| 宝箱进入 | `treasure.Enter(IrScope scope, Expr run) → Inst[]` | 重置访问字段、抽稀有度并消耗真实袋成员；金币在开箱动作抽取 |
| 宝箱操作 | `treasure.RegisterActions(package)` | 共用GainGold/Obtain，宝箱仍拥有Opened/Resolved及关闭 |
| 新卡实例 | `CardDefinitions.New(scope, CardDefinition card, bool upgraded)` | 返回字段初始化列表；调用者用`CreateEntity`放入长期牌组 |
| 升级长期牌 | `CardDefinitions.Upgrade(IrScope scope, Expr held) → Inst` | 复用完整升级/增量契约，不能只写`Upgraded=true` |
| 药水容量 | `PotionInventoryRules.HasSpace(IrScope scope) → Expr` | 检查同一个库存模块的`Belt` |
| 领取奖励药水 | `PotionInventoryRules.Acquire(IrScope scope) → IReadOnlyList<Inst>` | 从该模块的`Rewards`领取到首个空槽；不是任意商店药水创建接口 |
| 药水实例字段 | `PotionInventoryRules.Fields(IrScope scope, string id, Expr slot)` | 返回实例字段列表；调用者须提供正确槽位并执行容量守卫 |
| 商店声明与进入 | `new ShopRoomWiring(package, room, deck, inventory, potions, resources, relicCommands, backendId = BaseSpireComputeBackend.BackendId)`；进入为 `shop.Enter(scope, run) → Inst[]` | deck为RunDeck，其他依赖分别为RunInitializationWiring/PotionInventoryRules/RunResources/RunRelics；使用原源池/随机顺序生成库存，购买/删牌复用公共操作 |
| 商店动作 | `shop.RegisterActions(package)` | 买卡/买遗物/买药水/删牌/离店；买不起、满栏、已售、不可买都**不在候选域**且被守卫拒绝（不 Fault）；删牌走正式 `Decide` |

源码：[卡牌配方](../../../mod-the-spire/base-spire/BaseSpire/Slice/Orchestration/CardDefinitions.cs)、[药水库存](../../../mod-the-spire/base-spire/BaseSpire/Slice/PotionInventoryRules.cs)、[宝箱](../../../mod-the-spire/base-spire/BaseSpire/Slice/TreasureRoomWiring.cs)、[初始化库存](../../../mod-the-spire/base-spire/BaseSpire/Slice/RunInitializationWiring.cs)、[商店](../../../mod-the-spire/base-spire/BaseSpire/Slice/ShopRoomWiring.cs)、[商店纯计算](../../../mod-the-spire/base-spire/BaseSpire/Slice/ShopCompute.cs)、[无色源清单](../../../mod-the-spire/base-spire/BaseSpire/Slice/ColorlessShopPool.cs)、[问号赔率](../../../mod-the-spire/base-spire/BaseSpire/Slice/Rooms/Unknown/UnknownMapPointOdds.cs)、[事件生命周期与随机](../../../mod-the-spire/base-spire/BaseSpire/Slice/Rooms/Events/EventVisitLifecycle.cs)。

商店的稀有度/价格计算资产与地图资产共用同一个 `BackendId`，按 `asset.Entry` 在
[`BaseSpireComputeBackend.Execute`](../../../mod-the-spire/base-spire/BaseSpire/Slice/SourceActMapCompute.cs) 里分派（先商店、再奖励、再问号赔率与事件随机、最后加权取样）。

## 接线迁移

营火旧`Register(package, map, runs, hp, maxHp, deck, ...)`改为传入`RoomCompileContext`；宝箱旧构造的Run/map/gold参数也从上下文读取。正式组包器已迁移。额外的卡牌/遗物/药水依赖仍显式传入，不通过全局服务查询。

本次上下文提取保留原指令语义和随机调用。构建身份由实际规则产物派生；其他初始化补丁导致的身份变化按其工作包单独结算。后续机制新增公开状态仍须维护[公开语义](../../../mod-the-spire/base-spire/BaseSpire/SinglePlayer/BasePublicSemantics.cs)。
