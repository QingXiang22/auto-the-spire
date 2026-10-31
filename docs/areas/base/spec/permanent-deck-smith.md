# 永久牌组、战斗副本与营火锻造

文档类型：参考（Reference）。关联 **CORE-AUTHOR-05／DSL-DECK-SMITH-01**。按[纯 DSL 规格](../../../understand/base-architecture.md)扩展卡牌实例与一个真实消费者；认领和状态只见[GitHub 任务清单](https://github.com/QingXiang22/auto-the-spire/issues/28)。

## 来源与支持边界

`sts2/src/Core/Entities/RestSite/SmithRestSiteOption.cs`：只有 `Owner.Deck.UpgradableCardCount != 0` 才可用；从永久牌组选择，允许取消；取消返回 false，成功后逐张升级再发 `AfterRestSiteSmith`。本批支持默认 SmithCount=1，保留真实手动选择，包括单候选；不实现 SmithCount 修正与遗物消费者。`CardModel.IsUpgradable` 检查升级次数，已迁移牌均按一次升级处理。没有行为定义的开发占位牌不可升级。

本包将永久 Card 实例与战斗副本分离。已迁移 CardDefinition 模板生成明确的开发获得入口；不会缩减源池后随机发奖励。正常 Ironclad 初态、洗牌和自动抽牌见[角色初态约定](ironclad-start.md)与[正常 Run 指南](../how-to/run-and-rooms.md)；本页的开发获得入口仍不能代替正常开局。完整 Run、其他角色原池及未接入的获得修正仍未支持。

## 状态与准确操作

| 声明／操作 | 约定 |
| --- | --- |
| `RunDeck` | 有序永久 Card 集合，独立于所有战斗牌区，公开为 group；当前限单玩家 scope |
| `Card.CanUpgrade` | 默认 false；实际 CardDefinition 创建的牌为 true；可升级资格还要求 Upgraded=false |
| `Card.PermanentSource` | 隐藏可选 Ref(Card)；战斗副本指向本次复制的永久实例；永久实例和普通临时牌为空 |
| `Player.UsesRunDeck` | 默认 false；显式获得永久牌后为 true；使后续战斗采用永久牌组，不回退到开发占位牌 |
| `ObtainPermanentCard` | 当前只允许存活、非战斗、Room=none 的玩家；创建并登记一个真实定义的永久实例，再发 CardObtained；由 CardDefinition 生成的具名入口调用，不在 C# 按 ID 分派 |
| `CardDefinition` 生成的 `Obtain` 过程／动作 | 用该定义的字段调用共享获得过程；同定义多次获得是不同实例；开发动作不是随机奖励或正常角色初始化 |
| `BuildCombatDeck` | 新战斗开始时，仅对 UsesRunDeck=true：销毁该玩家全部非永久 Card 实例（包括旧战斗生成、消耗和 Power 牌），按永久集合顺序创建新副本到 DrawPile，保留已支持字段和升级；不写回永久牌、不抽 RNG。洗牌和抽牌由角色及回合调用方负责，不在复制操作内执行 |
| `UpgradeCard` | 拒绝 CanUpgrade=false 或已升级的实例；已有 CardUpgraded 负责定义层费用变化；战斗副本升级只改副本 |
| `ForgeAtDevelopmentCampfire` | 存活、非战斗、当前 rest-site、尚未用选项、有本玩家可升级永久牌；置 PendingForge，向 Engine 发布 EntityRefList 决策，min=0、max=1，候选固定；空回答取消，不消耗选项；选择后升级所选永久实例，置 RoomUsed，再发 AfterRestSiteSmith，最后清 PendingForge |

移除原稿 `ForgeCardInHand`：它未交付验证且使用错误牌区。本包的选择由通用 `net.answer_decision` 应答，空字段候选为取消；不增加专用 Host／wire 接口。暂停期间 Engine 只接受合法决策应答，旧候选、错误实例和重复应答不得升级或消耗选项。

## 事件中的永久牌变换

当前规则依据为[锁定 sts2-sim](../../../../reference/README.md)的 `CardSelectCmd.FromDeckForTransformation`、`CardFactory.GetDefaultTransformationOptions` 与 `CardCmd.TransformToRandom`。AromaOfChaos 和 MorphicGrove 复用 `SelectAndTransformPermanentCards(owner, amount, stream)`；amount 为1或2，stream 为该事件访问的独立随机流。

候选为拥有者当前永久牌组中非 Quest、没有 Eternal 的真实实例，保持牌组顺序。候选不多于所需数量时自动选择全部，空域不抽数；其余情况发布一次恰选 amount 张的决策。两张选择保留回答顺序，不拆成两个单选，暂停和非法回答不重复支付或抽数。

`TransformPermanentCardToRandom` 当前覆盖单人、全解锁 Ironclad 原池：按来源顺序保留全部 Common／Uncommon／Rare，排除被变换的定义，不使用战斗生成资格代替脱战资格。其他原池明确报告未支持，不能缩小候选后抽取。每张实际变换使用传入流抽一次，新实例按原定义的普通状态创建；先移除旧永久实例及附属状态，再通过完整获得操作追加新实例。升级、附魔和旧实例标识不转移，同定义其他实例不变。

正常路径按来源向量核对完整候选、事件流前后状态、替换顺序、永久实例与资源。首战的旧副本不成为新永久牌；后续战斗从更新后的永久牌组重新构建。组件中的空域、自动选全部和人为初态与正常路径分别报告。

## 房间与执行顺序

进入营火只在 Room=none 且非战斗、玩家存活时允许；不能重复进入同次访问重置使用标记。休息与锻造共享一次成功选项；取消锻造后仍可休息。离开营火要求成功用过选项；空可升级牌组仍有休息选项。开始战斗也只在 Room=none，不能跳过营火或奖励收尾。这些限制在公开 action 与对应 procedure 两层检查。

开发战斗失败时关闭房间（Room=none），但 HP=0 的玩家没有战斗／营火／永久获得候选；仍不宣称整局 terminal。手动结束开发战斗同时关闭回合门。

战斗副本在下一次开始时清理，避免最后击杀的未完卡牌效果访问已销毁实例。永久牌仅由显式永久操作修改；同定义其他实例、战斗副本和生成牌不得混为一张牌。当前不宣称完整战斗结束的清理和写回顺序已迁移。

## 判别与完成验证记录

角色基础初态与自动抽牌见[角色初态约定](ironclad-start.md)：BuildCombatDeck 本身只复制，显式 Ironclad 与正常 Run 的调用方负责各自洗牌、回合抽牌和访问生命周期。上文的开发入口限制不代替正常路径约定。

1. 从显式 Obtain 建立两个同定义永久实例；与旧开发牌区分离、标识不合并，非战斗来源守卫拒绝非法获得。
2. 空永久牌组或全部升级后不开放锻造；有手牌但没有永久候选也不可用。
3. 选择第二个同定义永久实例，只升级该实例；取消不升级、不消耗房间选项；同次访问不能反复进入或使用两次成功选项。
4. 两次真实战斗从同一永久实例创建各自新副本；升级效果和费用生效，临时副本升级不反写，前次临时／消耗／Power 牌不混入下一战。
5. 错误实例、过期／重复回答、暂停时跨房或出牌被拒绝，合法选择保持；过程守卫覆盖绕开 action 的调用。
6. 固定 seed 20261018 本地行为检查与真实 stdio 的选牌／取消轨迹通过；公开永久牌组和升级事实来自同一 DSL。原版独立参考比较未运行则保留未验证。

## 正常地图营火

正常 `ChooseMapPoint` 可进入 RestSite，建立同一 RunVisit 并将 Run.Phase 与 Player.Room 置为 `rest-site`；源地图类型、楼层和随机流继续沿用原访问链。休息与锻造复用现有完整操作，取消不消耗房间选项。成功选择后 `LeaveDevelopmentRoom` 提交并关闭当前访问，保留永久实例和升级，恢复真实地图后继；同次访问不能重新获得收益。开发营火没有 Run 时仍返回房间外。当前只接入已有休息／单张升级选项，遗物新增选项仍按其消费者实现。
