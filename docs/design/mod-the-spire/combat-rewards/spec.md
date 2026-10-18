# 开发态战斗金币奖励

关联 **CORE-AUTHOR-05／DSL-REWARD-CLAIM-01**。本文规定金币奖励的生成、领取与关闭；认领和状态见[主清单](../../../review/tofix/README.md#当前交付)。遵守[纯 DSL 边界](../engine-dsl-migration/spec.md)。

## 来源与边界

`sts2/src/Core/Rewards/GoldReward.cs` 的 `Populate` 用 Rewards RNG 生成金额，`OnSelect` 才调用 `PlayerCmd.GainGold`。`Reward.SelectUnsynchronized` 记录成功选择，`RewardsSet.Offer` 在玩家活着时提供奖励。`EncounterModel.MinGoldReward/MaxGoldReward` 的 A0 Monster 区间为 10..20。上一包“胜利即到账”是开发简化，本包改为显式领取。

只恢复开发态 Monster 金币；继续使用 `base-spire:development-reward`，不宣称等同原版 RNG。Elite／Boss、逃脱比例、进阶、金币修正参与者、卡牌／药水／遗物奖励、地图／永久 Run 和正常战斗最终余效的完整顺序均未迁移。本包不扩展 Engine 或 Host，也不建立未来奖励种类的通用框架。

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
