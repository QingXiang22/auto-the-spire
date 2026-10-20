# `.spire` 效果原语目录（DSL 0.0.1）

2026-10-15说明：本文基于已退休的Legacy编译器，源码链接指向删除前历史，不代表当前base实现能力；当前作者约定见[base/0.0.2参考](../0.0.2/authoring-reference.md)。
本文件列出**战斗效果编译器**当前接受的全部原语，并标明哪些能在新版卡牌语法里书写。

- **适用范围：** 卡牌 `play`/`hooks`、药水 `use`、敌人 `moves` 的效果表达式共用同一套原语与同一个编译器。整局事件效果是另一套入口（`RunEffect`），不在本目录内。
- **来源：** [`SpireCombatDslCompiler.cs`](https://github.com/QingXiang22/auto-the-spire/blob/0aca7d1a26f3ad42d5b8657bc794cd0b12f85ba8/archive/legacy-sts/core-the-spire/Core.TheSpire.Legacy/Legacy/SpireCombatDslCompiler.cs) 中 `CompileEffect` / `CompilePredicate` / `CompileCombatCount` 的 `case` 与紧随其后的 `CheckShape(...)`，以及决定新语法参数顺序的 `PositionalFields` 表。本目录由脚本从该文件推导，再人工抽查实测（见文末「维护方式」）。
- **状态：** 与代码同步的**实现状态**，不是设计目标。原语的含义与运行时语义见 [content-dsl.md](content-dsl.md)。

## 1. 位置参数规则（决定「能不能写」）

新版卡牌语法里，效果一律写成 `名称 = { 具名字段 = 值 … }`。原语内部仍按**位置参数**接收数据，因此编译器用 `PositionalFields` 表把具名字段翻译成位置参数：

```text
play = {
    attack = { damage = 6 target = context.target }   # damage -> 位置 0；target -> 具名参数
    applyPower = { power = Vulnerable stacks = 2 }      # power -> 位置 0；stacks -> 位置 1
}
```

由此得到一条硬规则：

> **需要位置参数、但 `PositionalFields` 没有对应字段名的原语，在新版卡牌语法里无法书写。**

旧语法用调用形式（`attack(6)`、`stealGold(5, power, variable)`）不受此限制——敌人、Power、药水等旧语法内容照常使用。下面各表的「新语法」列标出该原语当前是否可以写成花括号形式。

## 2. 效果原语

「位置字段」= 该原语在新语法里必须写的具名字段（按顺序占位）；「其他具名字段」= 可选的具名参数。

| 原语 | 位置字段 | 其他具名字段 | 新语法 |
| --- | --- | --- | --- |
| `sequence` | （成员即子效果，见 §5） | — | ✅ |
| `if` | （特殊形式，见 §5） | — | ✅ |
| `attack` | `damage` | `target` `properties` `onFatal` | ✅ |
| `attackVariable` | `variable` | `target` `hits` `properties` | ✅ |
| `scaledAttack` | `base` `perCount` | `count` `target` `properties` `onFatal` | ✅ |
| `attackUsingBlock` | — | `target` | ✅ |
| `randomAttack` | `damage` | `properties` | ✅ |
| `applyPower` | `power` `stacks` | `target` | ✅ |
| `applyScaledPower` | `power` `base` `perCount` | `count` `target` `onlyEnemy` | ✅ |
| `gainBlock` | `amount` | `perPowerStack` `properties` `target` `onlyEnemy` | ✅ |
| `gainBlockFromCurrentBlock` | — | `multiplier` `target` | ✅ |
| `loseHp` | `amount` | `target` `properties` | ✅ |
| `heal` | `amount` | `target` | ✅ |
| `gainMaxHp` | `amount` | — | ✅ |
| `loseMaxHp` | ⛔ 无映射 | `target` | ⛔ |
| `draw` | `amount` | — | ✅ |
| `drawUntil` | — | `type` | ✅ |
| `modifyEnergy` | `amount` | — | ✅ |
| `repeat` | `count` `effect` | — | ✅ |
| `repeatForX` | `effect` | — | ✅ |
| `repeatForCount` | `base` `perCount` | `count` `effect` | ✅ |
| `withCount` | `name` | `count` `effect` | ✅ |
| `addCards` | `card` | `pile` `count` `target` `inheritUpgradeFromPlayedCard` `position` | ✅ |
| `generateCards` | — | `count` `pile` `upgraded` `type` `distinct` `cost` | ✅ |
| `cloneCard` | — | `card` `pile` `count` | ✅ |
| `transformSelectedCard` | `card` | `from` `upgraded` | ✅ |
| `upgradeCards` | — | `pile` | ✅ |
| `upgradeSelectedCard` | — | — | ✅ |
| `downgradeSelectedCard` | — | — | ✅ |
| `moveSelectedCard` | — | `from` `to` `destinationPosition` | ✅ |
| `retainSelectedCard` | — | — | ✅ |
| `chooseCard` | — | `from` `to` `prompt` `destinationPosition` `filter` `onSelected` | ✅ |
| `chooseCards` | — | `from` `prompt` `minChoices` `maxChoices` `filter` `onSelected` | ✅ |
| `choose` | — | `prompt` `options` | ✅ |
| `randomMoveCard` | — | `from` `to` `filter` `after` | ✅ |
| `randomMoveCards` | — | `from` `to` `count` `filter` `after` | ✅ |
| `forEachCard` | — | `from` `filter` `effect` | ✅ |
| `autoPlay` | — | `from` `count` `position` `forceExhaust` | ✅ |
| `autoPlayRandomCards` | — | `from` `count` `type` `forceExhaust` | ✅ |
| `autoPlaySelectedCard` | — | `from` `forceExhaust` | ✅ |
| `modifySelectedCardCost` | `modifier` | — | ✅ |
| `modifyCardVariable` | `variable` `amount` | `card` `operation` | ✅ |
| `modifyPowerVariable` | `power` `variable` `amount` | `target` `operation` | ✅ |
| `modifyEnemyVariable` | `variable` `amount` | `operation` | ✅ |
| `modifyCombatState` | `variable` `amount` | `operation` | ✅ |
| `accumulateCardDamage` | `variable` | `target` `source` | ✅ |
| `addCardEnchantment` | ⛔ 无映射 | `card` | ⛔ |
| `removeCardEnchantment` | ⛔ 无映射 | `card` | ⛔ |
| `decrementPower` | `power` | — | ✅ |
| `removePower` | `power` | — | ✅ |
| `removePowers` | — | `debuffsOnly` `includeTemporary` | ✅ |
| `expirePower` | ⛔ 无映射 | — | ⛔ |
| `setEnemyAction` | ⛔ 无映射 | — | ⛔ |
| `stun` | — | `target` | ✅ |
| `kill` | — | `target` | ✅ |
| `escape` | — | `target` | ✅ |
| `queueRevival` | — | `intent` | ✅ |
| `stealGold` | ⛔ 无映射 | `target` | ⛔ |
| `addGoldReward` | ⛔ 无映射 | `count` | ⛔ |
| `setGoldRewardPercent` | ⛔ 无映射 | — | ⛔ |
| `stealRunCard` | ⛔ 无映射 | `from` `priorities` | ⛔ |
| `returnHeldRunCardReward` | ⛔ 无映射 | `sourceEncounter` | ⛔ |
| `summonEnemy` | ⛔ 无映射 | `count` `startingAction` `startingPowers` `additionalPowers` `maxLivingEnemies` `maxEnemies` `slot` | ⛔ |
| `summonRandomEnemy` | ⛔ 无映射 | `startingPowers` `additionalPowers` `maxLivingEnemies` `avoidPrevious` `slot` | ⛔ |
| `respawnSelf` | ⛔ 无映射 | `startingAction` | ⛔ |
| `setEnemyHealth` | ⛔ 无映射 | — | ⛔ |
| `temporarilyDowngradeCards` | ⛔ 无映射 | `target` | ⛔ |
| `restoreTemporarilyDowngradedCards` | ⛔ 无映射 | `target` | ⛔ |
| `registerPowerApplier` | ⛔ 无映射 | `target` | ⛔ |
| `afflictCards` | ⛔ 无映射 | `type` `target` `onlyIfUnafflicted` | ⛔ |
| `clearAffliction` | ⛔ 无映射 | `type` `target` | ⛔ |

## 3. 谓词（用于 `if` 的 `condition` 等条件位置）

| 谓词 | 位置字段 | 其他具名字段 | 新语法 |
| --- | --- | --- | --- |
| `targetAlive` | — | — | ✅ |
| `hasPower` | `power` | `target` | ✅ |
| `powerAmountAtLeast` | `power` `amount` | `target` | ✅ |
| `energyAtLeast` | `amount` | — | ✅ |
| `cardExhaustedThisTurn` | — | — | ✅ |
| `playerLostHpThisTurn` | — | — | ✅ |
| `selectedCardIsClone` | — | — | ✅ |
| `pileCountAtLeast` | `pile` `count` | — | ✅ |
| `cardPlaysThisTurn` | `count` | `type` `phase` | ✅ |
| `cardPlaysThisTurnAtLeast` | ⛔ 无映射 | `type` `phase` | ⛔ |
| `cardHasEnchantment` | ⛔ 无映射 | `card` | ⛔ |

> 注意 `cardPlaysThisTurn` 同时是谓词和计数源：谓词形式要求 `count`，计数源形式不要求（见下节）。

## 4. 计数源（用于 `scaled`/`scaledAttack`/`applyScaledPower`/`repeatForCount` 的 `count`）

| 计数源 | 位置字段 | 其他具名字段 | 新语法 |
| --- | --- | --- | --- |
| `pileCount` | `pile` | `type` | ✅ |
| `playerHpLossCount` | — | — | ✅ |
| `energyX` | — | — | ✅ |
| `block` | — | `target` | ✅ |
| `savedCount` | `name` | — | ✅ |
| `cardVariable` | `variable` | `card` | ✅ |
| `cardsWithTag` | `tag` | — | ✅ |
| `targetPowerAmount` | `power` | `target` | ✅ |
| `cardPlaysThisTurn` | — | `type` `phase` | ✅ |
| `powerVariable` | ⛔ 无映射 | `target` | ⛔ |
| `takePowerEventAmount` | ⛔ 无映射 | `card` | ⛔ |
| `takePowerTurnEventAmount` | ⛔ 无映射 | — | ⛔ |
| `product` | ⛔ 无映射 | — | ⛔ |

## 5. 特殊形式

这几个不是普通原语，新语法里有专门写法：

| 形式 | 新语法写法 | 说明 |
| --- | --- | --- |
| `sequence` | 直接把多个效果并列写在同一个块里 | 执行块内部**保留顺序与重复**，不需要显式 `sequence` |
| `if` | `if = { condition = { … } then = { … } else = { … } }` | `else` 可省略；`condition` 是一个谓词块 |
| `scaled` | `scaled = { base = 0 perCount = 1 count = { … } }` | 计算值，`count` 省略时按 0 计 |
| `variable` | `variable = { name = ExtraDamage default = 0 upgraded = 5 preserveOnDowngrade = true }` | 卡牌变量声明；`upgraded`、`preserveOnDowngrade` 可选 |
| `costModifier` | `costModifier = { amount = -1 operation = add expires = { turnEnd played } reduceOnly = false }` | `expires` 必需且非空 |
| `priority` | `priority = { rarities = { Common } requireEnchantments = { … } matchAny = false }` | 仅用于 `stealRunCard` 的 `priorities` |
| `cardEvent` | 旧语法专用：`beforeCardPlayed = cardEvent(effect=…, whenCardType=attack)` | 新语法在 `hooks` 里直接写 `beforeCardPlayed = { whenCardType = attack effect = { … } }`，不用 `cardEvent` |

## 6. 当前在新语法里不可书写的原语（25 个）

上表标 ⛔ 的原语共 25 个。它们**并非未实现**——旧调用形式可用、敌人与 Power 内容正在使用——只是缺少 `PositionalFields` 映射，因此新版卡牌语法写不出来。

| 分类 | 原语 |
| --- | --- |
| 卡牌作者会用到 | `addCardEnchantment`、`removeCardEnchantment`、`expirePower`、`setEnemyAction`、`loseMaxHp`、`cardHasEnchantment`、`cardPlaysThisTurnAtLeast` |
| 敌人与召唤（内容仍是旧语法，暂不受影响） | `summonEnemy`、`summonRandomEnemy`、`respawnSelf`、`setEnemyHealth`、`afflictCards`、`clearAffliction`、`temporarilyDowngradeCards`、`restoreTemporarilyDowngradedCards`、`registerPowerApplier` |
| 金币与整局牌 | `stealGold`、`addGoldReward`、`setGoldRewardPercent`、`stealRunCard`、`returnHeldRunCardReward` |
| Power 事件数值 | `powerVariable`、`takePowerEventAmount`、`takePowerTurnEventAmount`、`product` |

**疑似遗漏的映射**（同一族里有的有、有的没有，判断为疏漏而非设计）：

| 有映射 | 缺映射 | 建议 |
| --- | --- | --- |
| `gainMaxHp` → `amount` | `loseMaxHp` | 补 `["amount"]` |
| `cardPlaysThisTurn` → `count` | `cardPlaysThisTurnAtLeast` | 补 `["count"]` |
| `cardVariable` → `variable` | `cardHasEnchantment` | 补对应的 id 字段 |

要开放这些原语，在 `PositionalFields` 里补一条即可，无需改语义。

## 7. 维护方式

本目录的表格由脚本从 `SpireCombatDslCompiler.cs` 推导：

1. 抽取 `CompileEffect` / `CompilePredicate` / `CompileCombatCount` 中每个 `case` 及其紧随的 `CheckShape(call, 最少位置参数, 最多位置参数, 具名字段…)`；
2. 抽取 `PositionalFields` 表的「原语 → 位置字段」映射；
3. 判定规则：**最少位置参数为 0，或位置字段数量 ≥ 最少位置参数 → 可写**；`sequence` 与 `if` 由 `LowerNamedCall` 特判，单列为可写；
4. 对判定结果人工抽查实测（例如 `removePowers`/`kill` 可写，`addCardEnchantment`/`expirePower`/`summonEnemy` 报 `has an invalid number of positional arguments`）。

新增或调整原语、修改 `CheckShape` 签名、或改动 `PositionalFields` 时，本文件应同步更新——否则会出现「文档写了、编译器不认」的情况。
