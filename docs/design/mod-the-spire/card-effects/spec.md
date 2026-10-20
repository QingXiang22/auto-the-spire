# 可组合卡牌效果与八张 Ironclad 普通牌

关联 CORE-AUTHOR-05／**DSL-CARD-EFFECTS-01**；实施与认领见[主清单](../../../review/tofix/README.md#当前交付）。本包接在出牌核心之后，按[纯 DSL 规格](../engine-dsl-migration/spec.md)和[机制迁移指南](../../../development/base-spire/how-to/migrate-mechanism.md)编写；状态只在 GitHub Issues 维护。

## 已核实来源与范围

把上一包的“一张牌一个模板”改成组合式：内容文件用一次 `CardDefinition`（身份、费用、升级费用、是否有目标）加若干效果模板，每个效果按 `Card.DefinitionId` 过滤，因此同一张牌可以有多个效果。`PlayAttackCard` 先发 `CardPlayed` 再发 `CardPlayedOnTarget`，与源 `IronWave.cs OnPlay` 的“先格挡后伤害”一致；`CardUpgraded` 让定义层升级（当前只有费用）在升级后生效。

本包迁移六张新卡，覆盖多段伤害、抽牌、格挡与伤害组合、伤害等于格挡、克隆进弃牌堆与升级降低费用：

| 内容 | 源 | 行为 |
| --- | --- | --- |
| `base-spire:twin-strike` | `TwinStrike.cs` | cost 1，`DamageVar(5, Move)` ＋ `WithHitCount(2)`，升级每段 +2 |
| `base-spire:pommel-strike` | `PommelStrike.cs` | cost 1，9 伤害＋抽 1；升级 +1 伤害、+1 抽牌 |
| `base-spire:shrug-it-off` | `ShrugItOff.cs` | cost 1，8 格挡＋抽 1；升级只 +3 格挡 |
| `base-spire:iron-wave` | `IronWave.cs` | cost 1，先 5 格挡再 5 伤害；升级两者各 +2 |
| `base-spire:anger` | `Anger.cs` | cost 0，6 伤害，之后把该牌的一份克隆放进弃牌堆；升级 +2 伤害 |
| `base-spire:body-slam` | `BodySlam.cs` | cost 1，伤害等于当前格挡；升级费用 -1 |

不迁移：原版目标选择、X 费与费用修改（除定义层升级费用）、消耗／虚无／保留／固有、卡牌类型与稀有度字段、Power 牌、卡牌获得途径、死亡与胜负。多段伤害逐段结算格挡，但原版同一张牌内多个效果的完整排序约定仍未逐卡核对。

## 行为要求与设计

| ID | 契约 | 判别 |
| --- | --- | --- |
| CE01 | 内容由一次 CardDefinition 与若干效果模板组成；效果只按 DefinitionId 过滤，不按卡 ID 分支 | 同一张牌同时有格挡与伤害效果时两者都生效 |
| CE02 | 有目标与无目标卡由不同公开动作提供候选；有目标卡在无敌人时没有攻击候选 | 自目标牌不出现在 PlayAttackFromHand 候选，攻击牌不出现在 PlayCardFromHand 候选 |
| CE03 | 多段伤害逐段扣格挡与生命 | 目标 3 格挡时 Twin Strike 先消耗格挡再掉 2，第二段完整掉 5 |
| CE04 | 抽牌效果复用 DrawCards→DrawOne，且在牌进入弃牌堆之前结算 | 抽牌时弃牌堆尚未包含本牌，结算后才移入 |
| CE05 | 升级在实例上生效并触发定义层调整；克隆保留升级状态 | Body Slam 升到 0 费；Anger 的克隆与原牌同身份同升级状态 |
| CE06 | 内容文件仅 use 模板参数，源资格、定义与 578 源分母不变 | catalog 只新增六个非空 definition，资格来自本轮审阅 |

## 实施与验证顺序

1. 先把 Strike／Defend 改到组合式模板并保持公开动作名不变，确认原有检查仍通过。
2. 依次加入多段、抽牌、格挡＋伤害、伤害等于格挡与克隆效果，每步只加一个能力。
3. 在包外为六张新卡各自生成见证，并补组合、候选分离、逐段格挡与顺序判别。
4. 通过真实 stdio 验证 Iron Wave 的公开轨迹，更新共享操作参考与领域资料，最后运行 solution 构建、包行为／覆盖、受影响 net 与文档检查。

## 后续包：全员攻击与四张内容（DSL-CARD-BATCH-02）

在同一批模板上新增 `AttackAllEnemiesEffect`（CardPlayed，对每个 `Hp > 0` 的开发目标逐段调用 DamageEnemyPowered），并迁移四张牌：

| 内容 | 源 | 行为 |
| --- | --- | --- |
| `base-spire:taunt` | `Taunt.cs` | 1 费有目标技能：7／8 格挡后对目标施加 Vulnerable 1／2 |
| `base-spire:tremble` | `Tremble.cs` | 1 费有目标技能、消耗：对目标施加 Vulnerable 3／4 |
| `base-spire:thunderclap` | `Thunderclap.cs` | 1 费全体攻击：对每个敌人 4／7 伤害，之后对每个敌人施加 Vulnerable 1 |
| `base-spire:blood-wall` | `BloodWall.cs` | 2 费：先失去 2 生命，再获得 16／20 格挡 |

判别的重点是“全体伤害先于全体 debuff”以及“有目标技能同时含格挡与 debuff 时的顺序”。有目标技能目前仍由公开动作 `PlayAttackFromHand` 打出（该动作实际含义是“打出 `Targeted=true` 的牌”），命名债已登记在交付记录里。

## 后续包：回合内历史标志与五张内容（DSL-CARD-BATCH-03）

新增两个回合内历史标志（源 `CombatManager.History` 的 `HappenedThisTurn` 查询）：`Player.LostHpThisTurn`（受到无格挡伤害或失去生命时置真）与 `Player.ExhaustedCardThisTurn`（`CardExhausted` 时置真），两者在回合开始时复位。新增两个按标志分支的效果模板：

| 模板 | 变化 | 内容 |
| --- | --- | --- |
| FlaggedHitsAttackEffect | 命中段数由 flag（`lost-hp`／`exhausted-card`）决定 | Spite（0 费 5 点，1 段；失血后 2 段，升级后 3 段） |
| FlaggedBlockMultiplierEffect | 标志为真时获得两次格挡 | EvilEye（1 费 8／11，翻倍为 16／22） |

同批迁移三张不需要新机制的牌：Break（1 费 20／30 伤害＋Vulnerable 5／7）、Howl From Beyond（3 费全体 16／21 伤害＋消耗，源里“被消耗时自动打出”的附加效果未迁移）、Hemokinesis（1 费失 2 生命＋15／20 伤害）。
