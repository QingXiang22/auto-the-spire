# 开发态战斗奖励

关联 **CORE-AUTHOR-05／DSL-REWARD-CLAIM-01**。本文规定金币奖励的生成、领取与关闭；认领和状态见[主清单](../../../review/tofix/README.md#当前交付)。遵守[纯 DSL 边界](../engine-dsl-migration/spec.md)。

## 来源与边界

`sts2/src/Core/Rewards/GoldReward.cs` 的 `Populate` 用 Rewards RNG 生成金额，`OnSelect` 才调用 `PlayerCmd.GainGold`。`Reward.SelectUnsynchronized` 记录成功选择，`RewardsSet.Offer` 在玩家活着时提供奖励。`EncounterModel.MinGoldReward/MaxGoldReward` 的 A0 Monster 区间为 10..20。上一包“胜利即到账”是开发简化，本包改为显式领取。

金币使用原版布局的 `base-spire:player:rewards` 与 double 截断整数映射。卡牌按下节恢复；Elite／Boss、逃脱比例、进阶、金币修正参与者、药水／遗物奖励、地图／正常 Run 和完整奖励前置消费仍未迁移，不建立未来奖励种类的通用框架。

## 状态与过程

状态均在 Engine 的 `Player` 实例：`Room="rewards"` 表示奖励界面仍打开；`GoldRewardPending: Bool`、`GoldRewardAmount: Int(0..99999)` 表示唯一金币条目，默认 false／0。`Gold` 继续是当前金币。

| 输入／操作 | 前置与顺序 | 公开结果 |
| --- | --- | --- |
| `CombatEnded(owner, victory)` | victory 为 true、owner 仍存活，且未打开上一奖励；一次 `sampleInt(10,21)`，保存金额，置 pending，再进入 rewards；失败不生成、不消费随机 | Gold 不变，金额可见，开放领取和关闭动作 |
| `ClaimDevelopmentGoldReward(owner)` | Room 为 rewards 且 pending；将保存金额加进 Gold，沿用 99999 上限，然后清空条目；不抽随机 | 金币增加一次，领取候选消失，界面保持打开 |
| `CloseDevelopmentRewards(owner)` | Room 为 rewards；丢弃未领取条目，清空金额／pending，Room 置 none，清掉房间选项状态；不抽随机 | 可以进入下一开发战斗／营火；不能补领或重复关闭 |

公开动作与对应过程均检查前置条件。奖励未关闭时，`BeginDevelopmentCombat`、`EnterDevelopmentRestSite`、`LeaveDevelopmentRoom` 拒绝跨过奖励；后者不能充当跳过奖励的另一出口。过程与候选守卫使用同一条件。没有奖励时领取／关闭不合法。

## 判别与验证

1. 固定 seed 20261018／777／1234／99：胜利后 Gold 仍为 99，金额在 10..20，同 seed 金额及随机游标一致；失败不生成、不抽样。
2. 领取用保存金额，只到账一次；再提交原动作被拒绝，会话、金币和随机游标不变；金币封顶沿用现有开发契约。
3. 关闭未领奖励不增加 Gold；关闭已领奖励不重复增加。关闭后新战斗能生成另一份奖励，第一份不残留。
4. 奖励打开时三种跨房动作无候选，直接调用过程也拒绝；关闭后恢复可用。
5. 通过同一 DSL 公开 Host 执行真实击杀，观察待领奖励、领取／关闭候选和 Gold，并覆盖领取与跳过两条路径。该证据是受控开发态，不能替代正常 Run 或原版独立对拍。

## 铁甲战士首幕普通战斗卡牌奖励

关联 CORE-AUTHOR-05／DSL-CARD-REWARD-01。源依据为 CardReward.Populate／OnSelect、CardFactory.CreateForReward／RollForUpgrade、CardCreationOptions.ForRoom、CardRarityOdds.Roll 和 IroncladCardPool。只消费已显式初始化的 Ironclad，首幕 A0、全部解锁、普通战斗且无奖励修改者；不恢复 Elite／Boss、后幕升级比例、奖励替代项、重抽、多选和获得修正。金币探针未选择角色时保持原行为。

### 生成与状态

本节扩展上文首批金币的历史支持边界；卡牌已按本节恢复，药水／遗物奖励和其余明确未支持部分保持。

胜利后的同一奖励流程先生成金币，再生成三张卡。源单人 85 项保留源顺序；奖励按 Common／Uncommon／Rare 过滤掉三张 Basic 和两张 Ancient，共 80 项，Feed／NotYet 可以作为奖励，不复用其战斗生成禁用条件。CardBlueprint.Rarity 与 Card.Rarity 来自内容模板，冻结源快照独立核验全部稀有度。未知定义或不足三张候选必须在抽样前失败，不缩池或用空效果代替。

Player.CardRarityOffset 以隐藏 Int 保存 binary32 位型，初值 -0.05f（3175926989），上限0.4f，跨奖励／战斗保留。每张先抽源 NextFloat：小于 binary32(0.03f+offset) 为 Rare，小于 binary32(0.37f+binary32(0.03f+offset)) 为 Uncommon，其余 Common；Rare 后重置 -0.05f，其他结果按 binary32 加0.01f并封顶0.4f。偏移更新先于稀有度回退。当前稀有度无可用牌时按 Common→Uncommon→Rare→Common 回退，不能静默缩小来源表。再在该稀有度的剩余源顺序中随机选一张，同组三张定义互异；最后消费一次升级抽样。首幕基础升级概率为零，保留源 `<=` 的零边界。

金币、稀有度、候选与升级共用 `base-spire:player:rewards`，采用源流布局、有界整数和精确 NextFloat 映射；不再使用24-bit开发网格。独立源向量证明已迁移的金币／卡牌操作序列及游标一致；原 RewardsSet 在生成奖励列表时先进行药水掉落判定，这条流程尚未迁移，不能据当前结果宣称正常战斗整份奖励逐步一致。第一幕以外仍未接入；准确边界见[随机契约](../run-domain/randomness.md)。

CardRewardOptions 是待领 Card 集合，CardRewardPending 是 Player 标记；候选为未入战斗的真实 Card 实例，不进入 RunDeck、手牌或出牌历史，不发 CardEnteredCombat。先创建定义元数据再判断升级，候选冻结后观察／无效领取均不重抽。CardLibrary／偏移不发布隐藏未来信息。

### 领取与关闭

ClaimDevelopmentCardReward(owner, card) 要求存活、非战斗、Room=rewards、pending，且 card 是本玩家真实待领实例。调用卡牌领域的 ObtainCardReward 将该同一实例从候选集合移动至 RunDeck，UsesRunDeck=true，然后发 CardObtained；保持候选升级与身份，永久来源为空。不调用战斗创建／复制来代替获得。清掉 pending 并销毁所有未选实例；金币条目互不消耗，领取后仍需显式关闭房间。

SkipDevelopmentCardReward 清空 pending 和所有候选，不给永久牌、不改金币或随机游标。CloseDevelopmentRewards 同时丢弃未领金币和卡牌，回到房间外。重复／过期／错误实例被 action 及过程守卫拒绝；关闭前不能跨房。第二次胜利重新生成，不能带入前次候选。下一战仅从 RunDeck 重建已领牌的独立副本。

### 验证

独立冻结的 87 项源稀有度／两项多人排除／80 项奖励资格及顺序；手工核对的稀有度阈值边界、偏移上限／重置、空稀有度回退；三张去重、同 seed 重放、失败／观察／无效输入不抽样；实际领取同一实例、获得事件顺序、未选销毁、跳过／关闭不获得、金币独立、下一场副本和真实 stdio 三选一。原版双侧未执行就保留未验证。
