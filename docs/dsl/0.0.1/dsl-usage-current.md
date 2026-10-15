# `.spire` 旧语法使用说明（当前实现）

本文供你对照旧语法的编译器与内容实现。编写新卡牌时，使用 [`dsl-usage.md`](./dsl-usage.md) 中的新版作者约定。下文的 `package`、方括号集合、函数调用和分散升级字段属于仍有代码使用的旧语法。

本文面向编写或审阅 `base-spire`、其他 mod 内容的人。示例依据当前仓库中的解析器与编译器；DSL 仍在演进，具体能力以编译器实际接受的字段和表达式为准。

## 1. DSL 适合描述什么

`.spire` 用来声明内容数据与规则效果，例如卡牌、Power、敌人行动、状态牌、卡池和存储变量。编译器在加载内容包时将它们转换为强类型定义；战斗过程中执行已编译效果，不解析 DSL 文本。

回合阶段、合法动作校验、随机数流、牌区移动、暂停等待玩家选择及恢复结算由 `core-the-spire` 负责。DSL 作者描述“执行什么效果”和“何时触发/选择哪个动作”，不实现这些通用调度器。

目前 DSL 主要覆盖战斗内容。地图、遭遇、Act 配置等部分使用 JSON 或 C# 配置；不要假设所有游戏数据都已经改为 `.spire`。

## 2. 文件与基本语法

一个源文件属于一个 package。package 名成为其中未限定内容 ID 的命名空间：`package base-spire` 中的 `Bash` 对应 `base-spire:bash`。

```text
package training-mod {
  # 这是注释
  card Example {
    type = attack
    rarity = common
    cost = 1
    target = enemy
    play = attack(6)
  }
}
```

语法要点：

- 使用 `{}` 定义 package 和内容声明，使用 `name = value` 赋值。
- 数值、布尔值、名称、字符串、列表和函数调用是受支持的值类型；函数调用可以换行。
- 函数参数可以按位置传入，也可以使用 `name=value`。一个调用可同时包含两种参数，但具体顺序和名称由该操作定义。
- 标识符不区分大小写与否由对应字段的枚举解析决定；内容 ID 引用应使用已声明名称。
- 字符串用双引号；支持 `\"`、`\\`、`\n`、`\r`、`\t` 转义。`#` 到行尾是注释。
- 解析器不支持一般算术、变量插值、宏、用户定义函数或任意 C# 表达式。动态数值要用内置计数/缩放表达式。
- 未知字段、未知操作、错误参数和无法解析的内容引用会在编译时失败，并报告源文件位置。

引用本 package 的内容通常写名字；引用其他 package 时，使用带引号的完整 ID，例如 `"base-spire:ironclad-card-pool"`。完整 ID 的具体支持位置取决于字段类型。

## 3. 声明一张卡

最小卡牌声明需要 `type`、`rarity`、`cost`、`target` 和 `play`：

```text
package training-mod {
  card QuickJab {
    type = attack
    rarity = common
    cost = 1
    target = enemy
    play = attack(6)
    upgradedPlay = attack(9)
  }
}
```

常用字段：

| 字段 | 用途 |
| --- | --- |
| `type` | `attack`、`skill`、`power`、`status` 等卡牌类型 |
| `rarity` | 卡牌稀有度；稀有度本身不决定卡牌属于哪个奖励池 |
| `cost` / `upgradedCost` | 基础费用和升级费用 |
| `costKind = X` | X 费牌；X 费牌的 `cost` 必须为 0 |
| `target` | 卡牌动作的目标契约，如 `enemy`、`self`、`allEnemies`、`none` |
| `keywords` | 基础关键字，如 `[exhaust, ethereal]` |
| `upgradedKeywords` / `removedUpgradedKeywords` | 升级时增删的关键字 |
| `play` / `upgradedPlay` | 打出时效果；升级字段省略时沿用基础效果 |
| `pools` | 所属卡池，例如 `[IroncladCardPool]` |
| `tags` | 可被效果查询的内容标签，例如 `[Strike]` |
| `onExhaust` | 牌被消耗时执行的效果 |
| `turnEndInHand` | 玩家回合结束时仍在手牌中的效果 |
| `canGenerateInCombat` | 是否可由战斗内生成规则选中 |

目标字段决定外部打牌动作是否要求目标。效果中的 `target` 默认使用这次打牌所选的敌人；`self`、`allEnemies` 等则显式覆盖它。例如：

```text
card Bash {
  type = attack
  rarity = basic
  cost = 2
  target = enemy
  play = sequence(
    attack(8),
    applyPower(Vulnerable, 2, target=target)
  )
  upgradedPlay = sequence(
    attack(10),
    applyPower(Vulnerable, 3, target=target)
  )
}
```

`sequence(...)` 按顺序完成子效果。攻击伤害会进入内核通用伤害管线；对易伤、力量、格挡等规则的结算不在卡牌 DSL 中重复编写。

## 4. 效果表达式

一个效果通常是函数调用；效果可嵌套：

```text
play = sequence(
  gainBlock(5),
  applyPower(Strength, 2, target=self),
  if(energyAtLeast(2), attack(8), else=attack(4))
)
```

当前常用效果族：

| 需求 | 常见表达式 |
| --- | --- |
| 顺序、分支、重复 | `sequence(...)`、`if(predicate, then, else=...)`、`repeat(n, effect)` |
| 攻击和格挡 | `attack(n)`、`scaledAttack(base, perCount, count=...)`、`attackUsingBlock()`、`gainBlock(n)` |
| 生命与资源 | `loseHp(n)`、`draw(n)`、`modifyEnergy(n)` |
| Power | `applyPower(Power, n, target=...)`、`removePower(Power)`、`decrementPower(Power)` |
| 牌堆和牌实例 | `addCards(...)`、`chooseCard(...)`、`chooseCards(...)`、`moveSelectedCard(...)`、`upgradeSelectedCard()` |
| 批量处理 | `forEachCard(...)`、`randomMoveCard(...)`、`upgradeCards(...)`、`transformSelectedCard(...)` |
| 自动打牌与生成 | `autoPlay(...)`、`generateCards(...)`、`cloneCard(...)` |
| 暂存计数 | `withCount("name", count=..., effect=...)`、`savedCount("name")` |

操作名相同不代表参数可任意组合；每个操作有严格签名。较复杂用法及完整语义请参阅 [`content-dsl.md`](./content-dsl.md) 和 [`combat-dsl-design.md`](./combat-dsl-design.md)。

### 动态数量

动态表达式通过内置数据读取器获取运行态数值，再交给支持动态值的操作：

```text
scaledAttack(6, 3, count=pileCount(exhaust), target=target)
```

表示基础伤害 6，每张消耗牌额外增加 3。当前可用计数源包括牌堆数量、Power 层数、带标签卡牌数、能量 X 值、卡牌出牌历史和战斗摘要等。并非每个参数位置都支持动态数值；编译器会限制支持的位置。

若后续多个效果必须使用同一时点读取的数量，使用 `withCount` 捕获一次：

```text
withCount("handSize", count=pileCount(hand), effect=sequence(
  forEachCard(from=hand, effect=moveSelectedCard(from=hand, to=exhaust)),
  repeatForCount(0, 1, count=savedCount("handSize"), effect=attack(7))
))
```

这里先捕获原手牌数，再逐张消耗原手牌，最后按捕获的数量攻击。效果触发的抽牌不会改变已捕获的数量。`savedCount` 只能在对应 `withCount` 效果范围中使用。

### 选择与暂停

选择表达式会暂停当前效果，向外部暴露待选择项；提交合法选择后，内核恢复原来的效果序列：

```text
play = chooseCard(
  from=hand,
  to=exhaust,
  prompt="Choose a card to exhaust"
)
```

多选和后续效果：

```text
chooseCards(
  from=hand,
  prompt="Choose cards to retain",
  minChoices=0,
  maxChoices=scaled(0, 1, count=targetPowerAmount(WellLaidPlansPower, target=self)),
  filter=notRetained,
  onSelected=retainSelectedCard()
)
```

允许选零张时设置 `minChoices=0`。选择的候选牌使用实例 ID 区分重复卡牌。效果 DSL 声明选择和续接效果，动作是否合法、选择如何呈现和如何恢复由核心处理。

## 5. 声明 Power 与触发

Power 可以通过字段描述固定规则，也可以在指定生命周期或事件发生时执行效果：

```text
package training-mod {
  power Ritual {
    trigger = afterEnemyTurnEnd
    effect = grantPower(Strength, sourceStacks)
    skipFirstWhenAppliedToEnemy = true
  }

  power RagePower {
    trigger = afterCardPlayed
    whenCardType = attack
    effect = gainBlock(1, perPowerStack=RagePower)
    removeAfterPlayerTurnEnd = true
  }
}
```

触发时间由 `trigger`、`events` 或 `on` 中的声明指定；`effect` 是相应效果。事件可以按类型、事件所有者、伤害属性、卡牌类型或标签等过滤，支持哪些过滤项由具体事件定义决定。Power 的若干固定通用能力可用字段声明，例如 `preventsBlockClear`、`blockGainMultiplier`、`outgoingDamageMultiplier` 和 `cardRule`。

这里的 `grantPower(Strength, sourceStacks)` 是特定 Power 生命周期触发支持的形式，表示按当前 Power 层数授予 Strength；它不是任意效果都能使用的通用表达式。

## 6. 声明敌人与行动

敌人声明把**行动效果**和**行动选择规则**分开。行动在 `moves = actions(...)` 中命名，`behavior` 再组合这些名称：

```text
package training-mod {
  power Ritual {
    trigger = afterEnemyTurnEnd
    effect = grantPower(Strength, sourceStacks)
    skipFirstWhenAppliedToEnemy = true
  }

  enemy ExampleCultist {
    hp = variants(default=range(51, 53), tough=range(52, 54))
    moves = actions(
      chant = applyPower(Ritual, 5, target=self),
      strike = attack(3, target=player)
    )
    behavior = startWith(chant, then=loop(strike))
  }
}
```

这个定义表示：敌人初始 HP 从 51 到 53 中按规则抽取；`tough` 配置下改用 52 到 54。敌人先执行一次 `chant`，之后每个敌方行动阶段执行 `strike`。`Ritual` 在应用后跳过第一次敌方回合结束触发，之后每次为持有者增加与其 Ritual 层数相同的力量。

常见行为组合：

- `startWith(move, then=...)`：一次开场动作，随后转入指定行为。
- `loop(move)`：反复执行一个动作。
- `cycle(a, b, c)`：按顺序循环多个动作。
- `startWithChoice(choice, then=...)`：开场从一组候选中选一次，之后转入另一行为。
- `chooseWithLimits(...)`、`chooseByCondition(...)`、`graph(...)`：表达带权随机、条件或显式图的转移。

难度值使用 `variants(default=..., deadly=..., tough=..., deadlyTough=...)`；范围用 `range(min, max)`。具体随机抽取的时点与随机流属于运行时规则。

### 6.1 生成的状态机与运行时语义（BASE-02 批 1，2026-10-05）

`behavior` 不是"序列 + 循环起点"的简写，而是**状态图**：它按冻结源码的
`MonsterMoveStateMachine`（`MonsterMoves/MonsterMoveStateMachine/`）降级，状态、行动日志、冷却窗口与
已选意图都放在 Engine 状态里，由**同一份 IR** 执行。已在内容里使用并被检查的形态：

| 作者写法 | 生成的状态 | 语义（来源） |
| --- | --- | --- |
| `loop(x)` | 行动 x，后继 = x | `SnappingJaxfruit.cs:30-33` |
| `cycle(a,b,c)` | a→b、b→c、c→**a**（从列表开头循环，不是最后一个） | `FuzzyWurmCrawler.cs:35-38` |
| `startWith(a, then=B)` | 初始状态 = a，a 的后继 = B 的入口 | `Mawler.cs:35,43` |
| `startWithChoice(C, then=B)` | 初始状态 = **分支** C（开局就掷点），C 的候选与 B 池内行动的后继都指向 B 的入口 | `Flyconid.cs:34-49` |
| `graph(start=X, edges=[edge(从, 到)])` | 每个"从"行动的后继显式给出（到可以是行动或分支） | `Inklet.cs:69-83` |
| `chooseWithLimits(candidate(…))` | 加权随机分支（`RandomBranchState`） | `RandomBranchState.cs:115-167` |
| `chooseWithoutRepeat(m1,m2,…)` | 每个候选 `CannotRepeat` + 权重 1 的随机分支 | `SoulNexus` / `SludgeSpinner` 等 5 只敌人 |
| `chooseByCondition(candidate(m, when=…))` | **有序**条件分支（`ConditionalBranchState`），不消耗随机数 | `ConditionalBranchState.cs:44-54` |

`candidate(move, weight=, cooldown=, maxConsecutive=, maxUsesPerCombat=, when=)` 的参数逐条对应源码：

- `weight`（默认 1）= `AddBranch(..., float weight)`；全部候选 weight 为 0 时**仍然抽一次**并命中**第一个**候选
  （`max = 0` ⇒ `NextFloat(0)` ⇒ `num <= 0`，`RandomBranchState.cs:117-126`）。
- `maxConsecutive=n` = `CannotRepeat`（n = 1）/ `CanRepeatXTimes(n)`：该行动在日志**尾部连续**出现 n 次后权重 0。
  注意：源码 `AddBranch(state, 2)` 的整数参数是**连续上限**，不是权重。
- `maxUsesPerCombat=1` = `UseOnlyOnce`：该行动**出现过**（含开局进日志的初始行动）就权重 0。`> 1` 未支持
  （原版没有"整场最多 n 次"）。
- `cooldown=c`：该行动出现在日志**最后 c 个行动**里就权重 0（`RandomBranchState.cs:158-165`）。
- `when=`：`metric(EnemyHp, LessThan, n)` / `metric(Turn, AtLeast, n)` 与 `all`/`any`/`not` 组合。

随机与调用点（运行时契约）：

- 流是 `monster_ai`（`RunRngType.MonsterAi`）；**每次落到随机分支恰好一次**抽取；行动→行动的固定转移与
  有序条件分支**不抽**。
- 选取发生在**出生时**与**每个玩家回合开始**（`CombatManager.AfterCreatureAdded` → `RollMove`；
  玩家侧回合开始 → `PrepareForNextTurn`）；当前状态是"还没执行过的行动"时**不转换也不抽**（首动不掷，
  `MonsterMoveStateMachine.cs:60-63`），因此两次调用幂等。
- 公开事实只有"已选意图"（`NextMove`）；状态机的当前状态、日志与冷却窗口是隐藏字段，不进观看者投影。
- 加权选取（ENV-T01）：抽取合并成**一次** `SampleInt(monster_ai, 0, 2^53)`（53 位），再由纯计算资产
  `base-spire:SourceWeightedPick`（后端 `base-spire-source-float32@1`）按原版 `RandomBranchState.cs:115-128`
  做 `Sum` 的 double 累加器 → `NextFloat(max)` 的 double 乘法后一次转 float32 → 按声明顺序逐项 float32
  减法、`<= 0f` 命中；门槛被挡住的候选按原版置 0 权重。候选权重可以是小数，`enemies.spire` 里两份带分数
  权重的声明按源写成 `weight=0.4`/`0.6`（Fogmog，`Fogmog.cs:46-47`）与 `weight=0.083333336`
  （= `1f/12f`）/`0.75`（Two-Tailed Rat，`TwoTailedRat.cs:125-128`），整套算术与原版逐位一致。
  **注意**：这两只敌人**当前都未进包**（行动里用到 `summonEnemy` 等未实现原语，逐条记在未支持清单与其
  遭遇排除列表里）；分数权重的运行时判别证据来自受控敌人文本（`SliceEnemyStateMachineChecks`），
  内容侧的权重字面量与未加载原因也由同一检查钉住。
- 已支持：`addCards(<卡名>, count=, pile=hand|draw|discard, position=bottom|top|random, target=player)`
  与 `startingPowers = [power(<能力>, <层数>)]`（检查见 `SliceEnemyAddCardsChecks`）。
- 未支持：具名条件 `condition(Name)`、`metric(LivingAllies/…)`、`power(...)`、`allyAction(...)`、
  `escape`/`summonEnemy`、敌人 `variables`/`conditions` 声明表与 `modifyEnemyVariable`/`modifyCombatState`，
  以及依赖**遭遇位次**的开场条件——这些敌人整条记为未支持，不产生近似行为。

## 7. 状态变量、卡池和药水

### Run/Combat 变量

```text
package training-mod {
  state CardsPlayed {
    scope = combat
    type = int
    default = 0
  }
}
```

当前声明支持 `run`、`combat` 两个作用域和整数值。通过 `modifyCombatState(...)` 修改战斗变量，通过 `combatState(...)` 在支持它的条件中读取。未声明的变量和错误作用域访问会在构建时被拒绝。敌人局部变量和 Power/Card 自有变量是不同存储域，使用各自的声明/API。

### 卡池

```text
package training-mod {
  cardPool TrainingPool {
    cards = [QuickJab, Bash]
  }

  card QuickJab {
    pools = [TrainingPool]
    type = attack
    rarity = common
    cost = 1
    target = enemy
    play = attack(6)
  }
}
```

`cardPool` 中的列表用于显式指定基础顺序；卡牌也要通过 `pools` 声明归属。组合包可扩展已注册卡池，mod 新增卡牌按包注册顺序追加。仅设置 `rarity` 不会自动加入奖励池。

### 战斗药水

```text
package training-mod {
  potion FirePotion {
    rarity = common
    usage = combatOnly
    target = enemy
    source = "本mod的规则依据"
    unpowered = true
    use = { attack = { damage = 20 target = context.target } }
  }
}
```

正式 `SpirePotionSource` 使用具名块效果，并复用卡牌逐效果程序。元数据接受 `usage = combatOnly | anyTime | automatic`，使用效果当前只绑定战斗药水；非战斗/自动效果必须声明非空 `unsupportedUse = "原因"`，不能写空 `use`。ID 使用所属包命名空间；`source` 必填，完整池顺序与玩法支持分开处理。栏位/领取/丢弃及编译边界见[药水契约](../../design/mod-the-spire/potion-inventory/spec.md)和[开发指南](../../design/mod-the-spire/base-development-guide.md#补充药水时只读本机制)。冻结 `.author` 场景的旧步骤语法继续兼容。

## 8. 编译与错误定位

内容包构建时，解析器先把文本转换成带源位置的语法树，再由卡牌、Power、敌人、药水等编译器各自检查允许的声明字段和表达式，最后产出内核执行的类型化定义。因此：

- 文件语法正确但字段不属于该声明类型，仍会编译失败。
- 声明引用的卡牌、Power、卡池或变量需要存在；部分跨包引用需使用完整 ID。
- 同一 package 内内容 ID 重复、卡池出现重复成员或行为引用不存在的动作会被拒绝。
- 错误包含源文件名、行和列，便于定位。

从仓库根目录运行当前 DSL/玩法检查：

```powershell
dotnet run --project core-the-spire/Core.TheSpire.Tests
```

测试程序集包含解析、编译和战斗行为检查。添加新 DSL 语义时，应至少覆盖：能编译的正例、无效引用或参数的负例，以及能区分预期行为的运行时案例。

## 9. 目前的限制

- DSL 不是通用脚本语言：没有任意循环、函数、算术语法或任意代码调用。
- 许多效果字段和事件过滤参数由操作单独定义；不能假定所有表达式都能放进所有字段。
- `.spire` 目前主要服务于战斗内容；遭遇和部分整局配置仍在 JSON/C# 中。
- C# 自定义效果处理器是有限扩展点，不应把它当作绕过 DSL 的默认内容写法。
- 源码行为、Hook 全局顺序、随机数调用和原版完整内容仍在逐项对照。已有 DSL 覆盖不等于对原版或任意 mod 的完整兼容。

## 10. 进一步阅读

- [`content-dsl.md`](./content-dsl.md)：当前 DSL 的操作语义、回合阶段、牌实例、Power 事件与运行时细节。
- [`combat-dsl-design.md`](./combat-dsl-design.md)：战斗 DSL 的设计边界、效果代数和编译模型。
- [`ironclad-dsl-coverage.md`](../../design/mod-the-spire/ironclad-dsl-coverage.md)：Ironclad 卡牌覆盖与未完成项。
- [`base-spire` 内容源](../../../mod-the-spire/base-spire/BaseSpire/ContentSource/)：当前可运行内容示例。
