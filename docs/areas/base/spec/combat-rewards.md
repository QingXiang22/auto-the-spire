# 首幕普通战斗奖励

文档类型：参考（Reference）。关联 **CORE-AUTHOR-05／DSL-REWARD-CLAIM-01**。本文规定首幕普通战斗奖励的上下文、生成、领取与关闭；认领和状态见[GitHub 任务清单](https://github.com/QingXiang22/auto-the-spire/issues/28)。遵守[纯 DSL 边界](../../../design/mod-the-spire/base-architecture.md)。

## 来源与边界

`sts2/src/Core/Rewards/GoldReward.cs` 的 `Populate` 用 Rewards RNG 生成金额，`OnSelect` 才调用 `PlayerCmd.GainGold`。`Reward.SelectUnsynchronized` 记录成功选择，`RewardsSet.Offer` 在玩家活着时提供奖励。`EncounterModel.MinGoldReward/MaxGoldReward` 的 A0 Monster 区间为 10..20。上一包“胜利即到账”是开发简化，本包改为显式领取。

金币使用原版布局的 `base-spire:player:rewards` 与 double 截断整数映射。卡牌按下节恢复；Elite／Boss、逃脱比例、进阶、金币修正参与者、遗物奖励与完整正常 Run 仍未迁移，不建立未来奖励种类的通用框架。

## 奖励来源上下文目标要求

本节 REWARD-01～05 规定共享奖励要求，首幕 A0 普通来源已按后文实现，其余来源保持未支持。复用已实现的生成、领取与获得操作；共享上下文属于 base DSL，不能由 Host 或工具替玩法选择随机流。

| 要求 | 输入与顺序 | 失败及验证 |
| --- | --- | --- |
| REWARD-01 来源上下文 | 捕获奖励来源／访问、角色与幕／进阶、池资格、数量、稀有度策略、升级策略、适用修正及源随机流；来源特有步骤显式保留 | 普通／精英／Boss／事件不能仅改标签共用错误概率；当前未知来源在生成抽样前明确拒绝 |
| REWARD-02 候选与生成 | 构造源顺序候选并按来源过滤、去重；候选查询只读。按源码执行掉落判定、稀有度与偏移、取牌、升级和结果修正，不为统一 API 合并或重排抽取 | 比较有序候选、资格、每次抽取参数／结果和游标；变换、战斗生成与奖励池分别处理，不能过滤为已实现子集 |
| REWARD-03 固定条目 | 一次生成创建该访问拥有的奖励容器／条目及真实候选实例，保留已生成状态；需要选择时暂停同一流程 | 观察、非法输入及续接不重抽；多种奖励共存、不同访问不串条目 |
| REWARD-04 获得与关闭 | 领取调用卡牌／药水／遗物领域获得操作，按源支付、容量和获得通知顺序处理；跳过／关闭清理未选实例，在规定边界返回访问 | 重复领取不重复提交；容量不足及取消不提前关闭，未选实例与来源引用正确清理 |
| REWARD-05 分来源验证 | 先恢复普通战斗完整前置消费，再接精英／Boss、幕升级率与实际事件来源；每种策略与真实调用者同时交付 | 同 seed 的不同合法来源在应有步骤产生差异；完整奖励和正常可达验证分别报告，不以开发奖励代替 |

独立比较覆盖生成前后、发布候选、选择暂停／续接、领取／跳过及返回边界；保留完整池和未支持失败。此模型只抽取已经有原版消费者的共同行为，不预建所有未来奖励种类。

### 普通战斗上下文与药水

`OfferNormalCombatRewards(owner, visitId, source, actIndex, ascension)` 接受唯一正整数访问标识，仅支持全部解锁、无奖励修正的 Ironclad 首幕 A0 Monster。角色、来源、幕、进阶、已有奖励和重复访问均在抽样前检查。`CombatRewardSet` 保存这些输入及 Generated／Closed；关闭后保留访问凭据，拒绝同一访问再次生成。开发胜利使用独立递增访问号，正常访问由调用者传入，不由本操作推进地图。正常首战沿源战斗结束／胜利通知和清理顺序完成后调用本操作，奖励容器关联实际 RunVisit；共享关闭动作清理未选实例后关闭访问并返回合法地图后继。

源 RewardsSet 构造列表时先执行 PotionRewardOdds.Roll，再按列表顺序 Populate：金币→适用药水→三张卡牌。药水初始掉落率为 binary32(0.4)，严格小于时掉落并减 binary32(0.1)，否则加同量；不封顶或归零。药水 Populate 先抽稀有度，`<=0.1f` 为 Rare、`<=0.35f` 为 Uncommon，其余 Common，再从该稀有度的源顺序候选中 NextItem。卡牌继续复用现有偏移、去重和升级操作。

药水候选为 Ironclad4Epoch 的三项，接 SharedPotionPool 的45项，全部解锁，共48项；不能按效果支持情况过滤。静态 `PotionBlueprint` 只承载候选元数据。待领药水为真实 `Potion`，绑定当前奖励容器；获得调用 `ObtainPotion`，将同一实例放入第一个空槽后通知 `PotionObtained`。满三栏时领取不改变待领条目，玩家可丢弃已有药水后再次领取，也可跳过或关闭；没有自动替换或额外取消选择。观察、满栏重试、领取、跳过和关闭均不抽样。关闭清理未领药水与卡牌，互不消耗其他已生成条目。

药水获得支持与效果支持分别记录：48项可生成及获得，只有已有六种效果可使用；其余条目明确标记 EffectSupported=false，不发布使用候选，直接调用使用过程报告 unsupported_capability。未知定义明确失败。此范围不包含药水修正遗物、额外槽位、其他来源或完整独立模拟器比较。正常首幕普通访问已由[Run 结算](run-domain.md#正常开局首段)接入相同生成及获得操作。

## 状态与过程

金币与卡牌待领标记保存在 Engine 的 `Player` 实例，访问与药水条目由 `CombatRewardSet` 保存：`Room="rewards"` 表示奖励界面仍打开；`GoldRewardPending: Bool`、`GoldRewardAmount: Int(0..99999)` 表示唯一金币条目，默认 false／0。`Gold` 继续是当前金币。

| 输入／操作 | 前置与顺序 | 公开结果 |
| --- | --- | --- |
| `CombatEnded(owner, victory)` | victory 为 true、owner 仍存活，且未打开上一奖励；显式 Ironclad 使用完整普通奖励上下文；未选角色的金币探针仅 `sampleInt(10,21)`；失败不生成、不消费随机 | Gold 不变，金额可见，开放领取和关闭动作 |
| `ClaimDevelopmentGoldReward(owner)` | Room 为 rewards 且 pending；将保存金额加进 Gold，沿用 99999 上限，然后清空条目；不抽随机 | 金币增加一次，领取候选消失，界面保持打开 |
| `CloseDevelopmentRewards(owner)` | Room 为 rewards；丢弃未领取条目，清空金额／pending，Room 置 none，清掉房间选项状态；不抽随机 | 可以进入下一开发战斗／营火；不能补领或重复关闭 |

公开动作与对应过程均检查前置条件。奖励未关闭时，`BeginDevelopmentCombat`、`EnterDevelopmentRestSite`、`LeaveDevelopmentRoom` 拒绝跨过奖励；后者不能充当跳过奖励的另一出口。过程与候选守卫使用同一条件。没有奖励时领取／关闭不合法。

## 判别与验证

1. 固定 seed 20261018／777／1234／99：胜利后 Gold 仍为 99，金额在 10..20，同 seed 金额及随机游标一致；失败不生成、不抽样。
2. 领取用保存金额，只到账一次；再提交原动作被拒绝，会话、金币和随机游标不变；金币封顶沿用现有开发约定。
3. 关闭未领奖励不增加 Gold；关闭已领奖励不重复增加。关闭后新战斗能生成另一份奖励，第一份不残留。
4. 奖励打开时三种跨房动作无候选，直接调用过程也拒绝；关闭后恢复可用。
5. 通过同一 DSL 公开 Host 执行真实击杀，观察待领奖励、领取／关闭候选和 Gold，并覆盖领取与跳过两条路径。该验证记录是受控开发态，不能替代正常 Run 或原版独立参考比较。

## 铁甲战士首幕普通战斗卡牌奖励

关联 CORE-AUTHOR-05／DSL-CARD-REWARD-01。源依据为 CardReward.Populate／OnSelect、CardFactory.CreateForReward／RollForUpgrade、CardCreationOptions.ForRoom、CardRarityOdds.Roll 和 IroncladCardPool。只消费已显式初始化的 Ironclad，首幕 A0、全部解锁、普通战斗且无奖励修改者；不恢复 Elite／Boss、后幕升级比例、奖励替代项、重抽、多选和获得修正。金币探针未选择角色时保持原行为。

### 生成与状态

卡牌与药水共享上文普通奖励容器。遗物奖励与其他明确未支持来源保持。

胜利后的同一奖励流程先判定药水掉落，再填充金币、适用药水和三张卡。源单人 85 项保留源顺序；奖励按 Common／Uncommon／Rare 过滤掉三张 Basic 和两张 Ancient，共 80 项，Feed／NotYet 可以作为奖励，不复用其战斗生成禁用条件。CardBlueprint.Rarity 与 Card.Rarity 来自内容模板，固定源快照独立核验全部稀有度。未知定义或不足三张候选必须在抽样前失败，不缩池或用空效果代替。

Player.CardRarityOffset 以隐藏 Int 保存 binary32 位型，初值 -0.05f（3175926989），上限0.4f，跨奖励／战斗保留。每张先抽源 NextFloat：小于 binary32(0.03f+offset) 为 Rare，小于 binary32(0.37f+binary32(0.03f+offset)) 为 Uncommon，其余 Common；Rare 后重置 -0.05f，其他结果按 binary32 加0.01f并封顶0.4f。偏移更新先于稀有度回退。当前稀有度无可用牌时按 Common→Uncommon→Rare→Common 回退，不能静默缩小来源表。再在该稀有度的剩余源顺序中随机选一张，同组三张定义互异；最后消费一次升级抽样。首幕基础升级概率为零，保留源 `<=` 的零边界。

金币、稀有度、候选与升级共用 `base-spire:player:rewards`，采用源流布局、有界整数和精确 NextFloat 映射；不再使用24-bit开发网格。独立源向量分别验证金币／卡牌局部操作与完整普通奖励上下文，后者包含药水掉落、适用药水稀有度和候选抽取的参数、结果、有序候选及游标。这些仍是受控访问验证，不能据此宣称正常 Run 可达或完整模拟器双侧一致。第一幕以外仍未接入；准确边界见[随机约定](randomness.md)。

CardRewardOptions 是待领 Card 集合，CardRewardPending 是 Player 标记；候选为未入战斗的真实 Card 实例，不进入 RunDeck、手牌或出牌历史，不发 CardEnteredCombat。先创建定义元数据再判断升级，候选固定后观察／无效领取均不重抽。CardLibrary／偏移不发布隐藏未来信息。

### 领取与关闭

ClaimDevelopmentCardReward(owner, card) 要求存活、非战斗、Room=rewards、pending，且 card 是本玩家真实待领实例。调用卡牌领域的 ObtainCardReward 将该同一实例从候选集合移动至 RunDeck，UsesRunDeck=true，然后发 CardObtained；保持候选升级与标识，永久来源为空。不调用战斗创建／复制来代替获得。清掉 pending 并销毁所有未选实例；金币与药水条目互不消耗，领取后仍需显式关闭房间。

SkipDevelopmentCardReward 清空 pending 和所有候选，不给永久牌、不改金币或随机游标。CloseDevelopmentRewards 同时丢弃未领金币、卡牌和药水，回到房间外。重复／过期／错误实例被 action 及过程守卫拒绝；关闭前不能跨房。第二次胜利重新生成，不能带入前次候选。下一战仅从 RunDeck 重建已领牌的独立副本。

### 验证

独立固定的 87 项源稀有度／两项多人排除／80 项奖励资格及顺序；手工核对的稀有度阈值边界、偏移上限／重置、空稀有度回退；三张去重、同 seed 重放、失败／观察／无效输入不抽样；实际领取同一实例、获得事件顺序、未选销毁、跳过／关闭不获得、金币独立、下一场副本和真实 stdio 三选一。原版双侧未执行就保留未验证。
