# 遗物下一批候选评估（2026-10-18）

任务：**ENV-T09-PR02-CONTENT**。本记录只登记“下一批选谁、各缺什么”，供接续时直接开工；不改变主清单状态（仍为 90/90 可执行、未实现 206）。

## 候选与依赖

| 候选 | 源行为 | 现有能力是否够 | 缺什么 |
| --- | --- | --- | --- |
| UnceasingTop（Rare） | 手牌变空时抽 1（排除抽牌前/回合末清手阶段） | 否 | 新的“手牌变空”时点：需在出牌/弃牌路径后统一派发，并区分阶段 |
| TheAbacus（Shop） | 洗牌时获得 6 格挡 | 否 | 新的“洗牌”时点：接在抽牌过程的重洗分支，注意抽空重洗与主动洗牌是两件事 |
| BurningSticks（Shop） | 每场一次：消耗 **Skill** 时把该牌的克隆加入手牌 | 否 | 需要“按被消耗的牌克隆到手上”的效果种类，以及 `after-card-exhausted` 传递牌实例（当前派发不带牌） |
| Akabeko（Uncommon） | 首回合开始施加 `VigorPower` 8（下一次攻击 +8 并消耗） | 否 | 需要 `VigorPower`：源是 `BeforeAttack` 钩子修改下一条攻击指令；本引擎没有对应能力钩子，需先补“下一次攻击加伤并消耗”的 Power 语义 |
| ParryingShield（Uncommon） | 回合结束时有 ≥10 格挡则对随机敌人造成 6 点无加成伤害 | 部分 | 已有格挡条件与无加成伤害；缺“随机单体目标伤害”效果种类（现有是全体） |

## 建议顺序

1. **TheAbacus**（洗牌时点）——若重洗在抽牌过程里是单点，接法与现有 `RelicAfterDamageReceived` 同形，风险最低。
2. **随机单体伤害**（ParryingShield、Tingsha、ForgottenSoul 共用）——一个效果种类解锁三件。
3. **UnceasingTop**（手牌变空）——需要选清楚派发点，避免抽牌前/清手阶段误触发。
4. **VigorPower／BurningSticks** 留到最后（前者要新 Power 钩子，后者要“克隆被消耗的牌”）。

## 边界

本轮没有代码改动：尝试过给“按牌来源加算伤害”加 `CounterEquals` 条件（为 Akabeko 铺路），确认 Akabeko 真正的机制是 `VigorPower` 的 `BeforeAttack`，该条件没有消费者，已回退，仓库与 `c35d27b` 一致。
