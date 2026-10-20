# 永久牌组、战斗副本与营火锻造

关联 **CORE-AUTHOR-05／DSL-DECK-SMITH-01**。按[纯 DSL 规格](../base-architecture.md)扩展卡牌实例与一个真实消费者；认领和状态只见[GitHub 任务清单](https://github.com/QingXiang22/auto-the-spire/issues/28)。

## 来源与支持边界

`sts2/src/Core/Entities/RestSite/SmithRestSiteOption.cs`：只有 `Owner.Deck.UpgradableCardCount != 0` 才可用；从永久牌组选择，允许取消；取消返回 false，成功后逐张升级再发 `AfterRestSiteSmith`。本批支持默认 SmithCount=1，保留真实手动选择，包括单候选；不实现 SmithCount 修正与遗物消费者。`CardModel.IsUpgradable` 检查升级次数，已迁移牌均按一次升级处理。没有行为定义的开发占位牌不可升级。

本包将永久 Card 实例与战斗副本分离。已迁移 CardDefinition 模板生成明确的开发获得入口；不会缩减源池后随机发奖励。原版牌池／资格、获得修正、附着、生成或复制完整规则、永久成长、正常初态、原版洗牌、自动回合抽牌和完整 Run 仍未支持。现有三张开发占位牌的默认 setup 保留；永久牌组由显式动作建立，不能当作正常 Ironclad 开局。

## 状态与准确操作

| 声明／操作 | 约定 |
| --- | --- |
| `RunDeck` | 有序永久 Card 集合，独立于所有战斗牌区，公开为 group；当前仍限单玩家开发 scope |
| `Card.CanUpgrade` | 默认 false；实际 CardDefinition 创建的牌为 true；可升级资格还要求 Upgraded=false |
| `Card.PermanentSource` | 隐藏可选 Ref(Card)；战斗副本指向本次复制的永久实例；永久实例和普通临时牌为空 |
| `Player.UsesRunDeck` | 默认 false；显式获得永久牌后为 true；使后续战斗采用永久牌组，不回退到开发占位牌 |
| `ObtainPermanentCard` | 当前只允许存活、非战斗、Room=none 的玩家；创建并登记一个真实定义的永久实例，再发 CardObtained；由 CardDefinition 生成的具名入口调用，不在 C# 按 ID 分派 |
| `CardDefinition` 生成的 `Obtain` 过程／动作 | 用该定义的字段调用共享获得过程；同定义多次获得是不同实例；开发动作不是随机奖励或正常角色初始化 |
| `BuildCombatDeck` | 新战斗开始时，仅对 UsesRunDeck=true：销毁该玩家全部非永久 Card 实例（包括旧战斗生成、消耗和 Power 牌），按永久集合顺序创建新副本到 DrawPile，保留已支持字段和升级；不写回永久牌、不抽 RNG。初始洗牌和自动抽牌未迁移，开发者使用既有 Draw |
| `UpgradeCard` | 拒绝 CanUpgrade=false 或已升级的实例；已有 CardUpgraded 负责定义层费用变化；战斗副本升级只改副本 |
| `ForgeAtDevelopmentCampfire` | 存活、非战斗、当前 rest-site、尚未用选项、有本玩家可升级永久牌；置 PendingForge，向 Engine 发布 EntityRefList 决策，min=0、max=1，候选固定；空回答取消，不消耗选项；选择后升级所选永久实例，置 RoomUsed，再发 AfterRestSiteSmith，最后清 PendingForge |

移除原稿 `ForgeCardInHand`：它未交付验证且使用错误牌区。本包的选择由通用 `net.answer_decision` 应答，空字段候选为取消；不增加专用 Host／wire 接口。暂停期间 Engine 只接受合法决策应答，旧候选、错误实例和重复应答不得升级或消耗选项。

## 房间与执行顺序

进入营火只在 Room=none 且非战斗、玩家存活时允许；不能重复进入同次访问重置使用标记。休息与锻造共享一次成功选项；取消锻造后仍可休息。离开营火要求成功用过选项；空可升级牌组仍有休息选项。开始战斗也只在 Room=none，不能跳过营火或奖励收尾。这些限制在公开 action 与对应 procedure 两层检查。

开发战斗失败时关闭房间（Room=none），但 HP=0 的玩家没有战斗／营火／永久获得候选；仍不宣称整局 terminal。手动结束开发战斗同时关闭回合门。

战斗副本在下一次开始时清理，避免最后击杀的未完卡牌效果访问已销毁实例。永久牌仅由显式永久操作修改；同定义其他实例、战斗副本和生成牌不得混为一张牌。当前不宣称完整战斗结束的清理和写回顺序已迁移。

## 判别与完成验证记录

角色基础初态与自动抽牌由后续[角色初态约定](../character-combat/ironclad-start.md)扩展：BuildCombatDeck 本身仍只复制，显式 Ironclad 初始化的消费者在开战时追加开发流洗牌、回合追加自动抽牌；原版随机与正常 Run 仍未恢复。上文手动探针的未支持边界不排除该显式消费者。

1. 从显式 Obtain 建立两个同定义永久实例；与旧开发牌区分离、标识不合并，非战斗来源守卫拒绝非法获得。
2. 空永久牌组或全部升级后不开放锻造；有手牌但没有永久候选也不可用。
3. 选择第二个同定义永久实例，只升级该实例；取消不升级、不消耗房间选项；同次访问不能反复进入或使用两次成功选项。
4. 两次真实战斗从同一永久实例创建各自新副本；升级效果和费用生效，临时副本升级不反写，前次临时／消耗／Power 牌不混入下一战。
5. 错误实例、过期／重复回答、暂停时跨房或出牌被拒绝，合法选择保持；过程守卫覆盖绕开 action 的调用。
6. 固定 seed 20261018 本地行为检查与真实 stdio 的选牌／取消轨迹通过；公开永久牌组和升级事实来自同一 DSL。原版独立参考比较未运行则保留未验证。
