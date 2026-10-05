# 内容 DSL 使用与设计约定

本文记录新版 DSL 约定和已迁移的卡牌语法。解析器和卡牌编译器现已支持新版文件头、花括号块、基础/升级版本及具名效果；Ironclad 卡牌和状态牌源码已采用该格式。敌人、Power、药水和整局配置仍使用旧语法。对照旧实现时请阅读 [旧语法参考](./dsl-usage-current.md)。

本文是 STS 内容作者语言 0.0.1；[通用引擎设计](../../design/core-the-spire/generic-tabletop-engine-design.md)中的 schema/procedure/ComputeAsset 是另一份未实现的目标契约，不是本版已接受语法。领域关键字以后由游戏包兼容前端提供，不作为通用 core 内置规则。

| 状态 | 本版边界 |
| --- | --- |
| 设计目标 | 完整 normal/upgraded、具名效果与 hooks；以本文为作者约定 |
| 解析器接受 | 当前文件头/花括号卡牌已有实现；具体原语支持见 primitives，不能推断所有设计表达式都可写 |
| 内容包能注册 | 已有内容加载记录；本轮只改文档未重跑，范围见[架构审阅](../../review/records/2026-10-01/core-base-architecture-dsl-2026-10-01.md) |
| 规则行为已验证 | 仅适用已执行的具体检查；目标域、升级空 hooks、X 费和编译形状仍需核对，不以源码迁移或构建成功宣称整包等价 |

## 1. 内容入口和命名空间

基础内容入口为 `card`、`enemy`、`player`。文件顶部使用命名空间声明：

```text
using namespace base-spire;
```

此语句在本 DSL 中指定本文件内容的归属命名空间。扫描到的内容归入对应 mod，名称相同但命名空间不同的内容可以共存。内容 ID 和注册归属由声明决定。

命名空间用于区分 mod；红色卡、角色归属、卡池成员关系分别由内容分类与 `pools` 描述。一个 mod 可以提供多个角色的内容，其他 mod 也可以扩展战士卡池。

设计中的内容域包括卡牌、敌人与玩家角色。当前解析器尚无 `player` 声明；现有声明形式为 `card`、`cardPool`、`potion`、`power`、`enemy`、`affliction` 和 `state`。玩家当前血量、战斗能量等数据属于运行实例。跨命名空间引用的具体拼写仍待定。

## 2. 统一使用花括号

集合、配置、表达式和执行内容统一使用 `{}`，新版 DSL 不使用方括号集合。效果原语使用具名块：

```text
pools = { IroncladCardPool }
tags = { Strike }
keywords = { exhaust }

play = {
    attack = {
        target = context.target
        damage = 6
    }
}
```

同一种花括号根据所在字段承担不同语义：

| 所在位置 | 含义 |
| --- | --- |
| `pools`、`tags`、`keywords` | 集合内容 |
| `normal`、`upgraded` 及效果参数 | 配置 |
| `target` | 计算目标规则的表达式 |
| `play`、事件处理体 | 按顺序执行效果 |
| `hooks` | 事件订阅配置 |

这种写法以内容块和具名参数组织规则。条件已在实现中：`if = { condition = { … } then = { … } else = { … } }`，其中 `condition` 是一个谓词块，`else` 可省略。循环/重复由 `repeat`、`repeatForCount`、`repeatForX`、`forEachCard` 等原语表达，没有通用 `while` 结构。

参数值本身是调用表达式时，必须放进该参数的花括号块里（见 §5）。**哪些原语能在新语法里书写、每个原语要写哪些字段，以[效果原语目录](primitives.md)为准**——该目录从编译器代码推导，并标明当前在新语法里写不出来的原语。

## 3. 后读覆盖前值

配置项允许重复赋值，以最后读到的值为准。不同配置字段可以自由排列；不同声明块的放置顺序不影响其含义，出现同名配置时才由读取顺序决定最终值。

- 同名标量配置：后值覆盖前值。
- 同名集合配置：后面的集合整体替换前面的集合。
- 同名配置块：后面的块整体替换前面的块，不做隐式递归合并。

```text
cost = 2
cost = 1

play = {
    attack = { target = context.target damage = 6 }
}

play = {
    draw = { amount = 2 }
}
```

最终费用为 1，打出效果仅为抽两张牌。第二个 `play` 替换第一个，两个块的效果不会自动拼接。若重复定义 `normal`，后面的整个基础版本会替换前面的基础版本；若只想改费用，则在同一个基础版本配置中重复写 `cost`。

**执行块内部保留顺序和重复效果。** 下面的一次打出会攻击两次：

```text
play = {
    attack = { target = context.target damage = 6 }
    attack = { target = context.target damage = 4 }
}
```

`play` 中的两个 `attack` 是两条执行指令；每条指令的参数则是配置，例如同一条攻击里重复写 `damage` 时取最后一个值。这里不能把整个文件先变成普通键值字典，否则会丢失重复效果及其顺序。

“攻击后施加易伤”与“施加易伤后攻击”保留各自的结果差异。集合的成员顺序也不能任意打乱，例如固定种子抽取时使用的卡池顺序。配置覆盖与执行结算是两个独立阶段。

这些规则作用于一次内容声明内部的配置项和配置块。不同内容声明的同 ID 冲突及跨 mod 覆盖仍由内容注册/组合契约决定，不从字段覆盖规则推导。

### 编译结果与兼容性

旧后端的具名执行块包含一条效果时编译为该效果节点；零条或多条编译为 `SequenceEffect`，多条按源码顺序保留。显式的旧 `seq(...)` 仍保留序列节点。序列相等逐项比较效果树，顺序不同不相等，结构相等的序列有相同哈希；序列不因列表实例不同而不相等。这恢复旧调用语法与新卡牌语法的单效果形状一致性，不改变结算顺序。

2026-10-01 修复 Cascade 基础/升级的 `cost = X`：此前误写固定 0 费，导致捕获的 energyX 为 0。修复后按本次出牌的 X 捕获张数，恢复对应自动打牌与洗牌/目标随机流消费；同 seed 的轨迹可能与错误版本不同，错误轨迹不能作为修复后的 golden。所有形状、数值和覆盖验证见[本轮评审收口](../../review/records/2026-10-01/review-followup-2026-10-01.md)。

## 4. 卡牌采用完整的基础版与升级版

卡牌共有归属信息放在外层；版本相关配置分别完整写入 `normal` 和 `upgraded`。不再使用散落的 `upgradedCost`、`upgradedPlay` 等字段，也不从基础版隐式继承遗漏字段。

以下结构已经由卡牌编译器支持。当前目标表达式和具名效果原语仅覆盖源码迁移所需的子集，完整的表达式语言与原语目录仍需继续扩展。

```text
using namespace base-spire;

card Bash {
    pools = { IroncladCardPool }
    tags = {}
    canGenerateInCombat = true

    normal = {
        type = attack
        rarity = basic
        cost = 2
        keywords = {}

        target = {
            selectOne = {
                from = context.opponents
                where = alive
            }
        }

        play = {
            attack = {
                target = context.target
                damage = 8
            }
            applyPower = {
                target = context.target
                power = Vulnerable
                stacks = 2
            }
        }

        hooks = {}
    }

    upgraded = {
        type = attack
        rarity = basic
        cost = 2
        keywords = {}

        target = {
            selectOne = {
                from = context.opponents
                where = alive
            }
        }

        play = {
            attack = {
                target = context.target
                damage = 10
            }
            applyPower = {
                target = context.target
                power = Vulnerable
                stacks = 3
            }
        }

        hooks = {}
    }
}
```

`pools` 保留卡池归属，`tags` 保留内容标签。稀有度和 mod 命名空间都不自动决定卡池成员关系。当前实现用 `upgradeable = false` 标记不能升级的卡；多次升级的版本组织方式尚待讨论。

### 费用

作者直接使用 `cost = 1` 或 `cost = X`，不再单独声明 `costKind`。编译器负责识别数字费用和 X 费，并交给运行时完成费用支付及本次执行的 X 值保存。

### 关键字与生成提示

`keywords` 保留，用于 tooltip 及关键词说明。关键词与实际规则效果如何关联还未确定，不把当前实现根据关键字 flags 自动执行的行为直接当成新设计结论。

`canGenerateInCombat` 保留为内容元数据提示。声明它不会自行生成卡牌或触发执行。具体生成规则是否读取、如何读取该提示，需在生成规则本身明确描述；不能把提示字段等同于所有生成操作都必须遵守的硬限制。

`multiplayerOnly` 标记仅限多人模式的卡牌定义。当前单人规则集仍注册这些卡牌，但不会把它们加入单人卡池。

### 卡牌级字段（写在 `normal` / `upgraded` 之外，与版本无关）

| 字段 | 用途 |
| --- | --- |
| `pools = { IroncladCardPool }` | 卡池归属；稀有度不决定归属 |
| `tags = { Strike }` | 可被 `cardsWithTag` 等效果查询的内容标签 |
| `upgradeable = true \| false` | 不能升级的卡写 `false`；**仍需写出完整的 `upgraded` 块** |
| `multiplayerOnly = true` | 仅多人模式；注册但排除出单人卡池 |
| `canGenerateInCombat = true \| false` | 战斗内生成的资格提示 |
| `variables = { variable = { name = ExtraDamage default = 0 } }` | 卡牌战斗变量；`variable` 可选 `upgraded`、`preserveOnDowngrade` |
| `damageVariable = Damage` | 源级伤害读取某个卡牌变量 |
| `damageValue = 7` / `damageValue = { scaled = { … } }` | 源级固定伤害值或计算式 |
| `upgradedDamageValue = 10` / `= { scaled = { … } }` | 升级版伤害值 |

### 版本一致性约束

- `normal` 与 `upgraded` 都必须完整包含 `type`、`rarity`、`cost`、`target`、`keywords`、`play`、`hooks` —— 没有钩子也要写空的 `hooks = {}`。
- 下列字段两侧必须一致，否则编译报错：`type`、`rarity`、`target`（`Upgraded card target must match the normal version.`）、以及是否 X 费（`Upgraded card cost kind must match the normal version.`）。
- `upgraded` 块不能省略，`upgradeable = false` 也照样要写。

## 5. 目标块和执行上下文

`target = {}` 接受表达式块，用于形成目标规则，并据此判断外部打牌动作是否需要目标。**当前编译器要求块中恰好有一个选择器表达式**；写两个会直接报 `Target blocks currently require one selection expression.`，不是后读覆盖。示例中的 `selectOne` 表达“从满足条件的敌人中选一个”，不在查询合法动作时执行选牌或随机取数。

已支持的选择器：

| 选择器 | 含义 | 对应目标契约 |
| --- | --- | --- |
| `selectSelf = {}` | 自身 | 自身 |
| `selectNone = {}` | 无目标 | 无目标 |
| `selectOne = { from = … where = … }` | 从候选里选一个；当前只接受 `from = context.opponents` 或 `context.allies` 且 `where = alive` | 由外部动作选择的目标 |
| `selectFirst = {}` | 第一个敌人 | 首个对手 |
| `selectAll = {}` | 所有敌人 | 全体对手 |
| `selectRandom = {}` | 随机敌人 | 随机对手 |

多个效果各自的目标用效果内的 `target = context.target` / `context.owner` 指定，与卡牌的目标契约相互独立。

更复杂的目标表达式（多目标组合、按条件返回不同选择器）仍需进一步定义。目标查询应读取当前状态；提交动作后，本次选择绑定到执行上下文，供效果获取。

`play = {}` 执行其中的效果。攻击原语明确指明目标，例如 `target = context.target`；行动者等信息可从上下文获取，避免只写一个伤害数字而无法判断效果作用于谁。

当某个参数的值本身是调用表达式时，调用必须放在该参数的花括号块中；不能写成 `count = pileCount = { ... }` 这样的连续赋值：

```text
count = {
    pileCount = { pile = hand }
}

count = {
    savedCount = { name = "handSize" }
}

count = {
    cardVariable = { variable = ExtraDamage }
}
```

外层块的唯一调用作为参数值编译。作者使用具名字段；编译器负责按运行时类型化节点所需的顺序传递参数。当前卡牌迁移所需的 `pileCount(pile)`、`savedCount(name)` 和 `cardVariable(variable)` 均按此形式支持。

上下文需要能够区分：

- 行动者与内容持有者。
- 正在执行的卡牌实例。
- 本次动作选择的目标。
- 事件触发时的事件来源、事件目标和事件数据。

这些概念已识别为必要能力；除示例中的 `context.target` 外，完整字段名与绑定时序尚待定稿。暂停等待选择时，需要保存原上下文，并在恢复后继续同一次执行。

## 6. 事件统一放入子块

`onExhaust`、`turnEndInHand` 等生命周期效果归入 `hooks`，为后续扩展留出同一位置：

```text
hooks = {
    onExhaust = {
        draw = { amount = 1 }
    }
    turnEndInHand = {
        loseHp = { target = context.owner amount = 3 }
    }
}
```

每个事件处理体属于执行块。相同事件配置重复声明时，最后一个处理体整体替换前面的处理体。需要执行多个效果时，在最终处理体中顺序写入。

当前卡牌 `hooks` 已支持的子块：

| 子块 | 触发时机 |
| --- | --- |
| `onExhaust = { … }` | 该牌被消耗时 |
| `turnEndInHand = { … }` | 玩家回合结束时该牌仍在手牌中 |
| `onEnterCombat = { … }` | 该牌进入战斗时 |
| `beforeCardPlayed = { whenCardType = attack effect = { … } }` | 作为监听者，在匹配类型的牌被打出前 |
| `afterCardPlayed = { whenCardType = attack effect = { … } }` | 作为监听者，在匹配类型的牌打出后 |
| `afterAutoPostPlay = { … }` | 自动打牌完成后的后置阶段 |

`beforeCardPlayed` / `afterCardPlayed` 用 `whenCardType` 过滤卡牌类型（省略表示匹配全部），`effect` 是执行块；新语法不使用旧语法的 `cardEvent(...)` 包装。基础版与升级版的钩子分别写在各自的 `hooks` 块里，不通过 `upgradedOnExhaust` 这类字段区分。

这里的 `context.owner` 是待确定的持有者字段示意。事件列表、事件过滤、取消或改写结算的机制，以及多个内容订阅同一事件时的调用顺序，仍需要单独设计。事件的配置覆盖规则不决定这些运行时触发时点。

## 7. 对照当前实现

下表区分已确定的新作者约定与目前源码，便于审阅卡牌：

| 新设计 | 当前实现 |
| --- | --- |
| 文件头 `using namespace modname;` | 卡牌使用新入口；敌人、Power、药水和整局配置仍使用 `package modname { ... }` |
| 花括号集合 | 卡牌新语法使用花括号；其余旧语法仍使用方括号集合 |
| `normal` / `upgraded` 两个完整版本块 | 卡牌已迁移；其他旧内容没有卡牌版本块 |
| `cost = X` | 卡牌新语法支持 X 费；旧内容使用 `costKind = X` 配合 `cost = 0` |
| `target = {}` 目标块 | 已支持 `selectSelf` / `selectNone` / `selectOne` / `selectFirst` / `selectAll` / `selectRandom`（见 §5）；块内必须恰好一个选择器。旧语法使用固定目标枚举 |
| 具名块效果与明确目标 | 卡牌编译器将支持的具名效果编译为现有类型化效果节点；其余旧语法使用函数调用 |
| `hooks` 子块 | 卡牌钩子已迁移；其他旧内容仍使用旧字段或独立行为定义 |
| `multiplayerOnly` | 卡牌编译器读取多人专属标记；当前单人卡池排除标记卡牌 |
| 新卡牌配置后写覆盖前值 | 新卡牌配置读取采用后值覆盖；旧配置字段仍按兼容解析规则处理 |
| `canGenerateInCombat` 内容提示 | 现有生成器将它用作资格筛选 |
| 关键词与 tooltip 的关系待细化 | 当前内置关键词 flags 参与结算 |

主要对照入口：

- [旧语法使用说明](./dsl-usage-current.md)：当前可编译语法与示例。
- [效果原语目录](./primitives.md)：新语法可书写的原语、位置字段映射，以及当前写不出来的原语清单。
- [Ironclad 卡牌源码](../../../mod-the-spire/base-spire/BaseSpire/ContentSource/ironclad-cards.spire)：当前卡牌定义、升级、事件和卡池。
- [Ironclad Power 源码](../../../mod-the-spire/base-spire/BaseSpire/ContentSource/ironclad-powers.spire)：当前 Power 触发和事件行为。
- [敌人源码](../../../mod-the-spire/base-spire/BaseSpire/ContentSource/enemies.spire)：当前行动效果与行为选择。
- [语法解析器](../../../core-the-spire/Core.TheSpire/Engine/DslSource.cs)：当前语法树和词法规则（2026-10-03 **路径级移动**：原在 `Legacy/`，命名空间不变；同一个解析器现在同时服务旧后端与新 Engine 路径）。
- [卡牌/效果编译器](../../../archive/legacy-sts/core-the-spire/Core.TheSpire.Legacy/Legacy/SpireCombatDslCompiler.cs)：当前字段与原语签名。
- [Power/敌人编译器](../../../archive/legacy-sts/core-the-spire/Core.TheSpire.Legacy/Legacy/SpireCombatBehaviorDslCompiler.cs)：当前触发与行为模型。
- [运行时内容定义](../../../archive/legacy-sts/core-the-spire/Core.TheSpire.Legacy/Legacy/Content.cs)：内核消费的强类型定义。

本文描述新卡牌语法及其当前实现边界。原语目录见 [效果原语目录](./primitives.md)；表达式类型、共享 Power、角色与敌人完整写法、跨包覆盖和加载排序还需后续补齐。卡牌源文件已迁移到该语法并被内容包加载，但规则行为尚未全部通过检查——当前失败项与阻断范围见 [AGENTS.md 合规审计](../../review/archived/agents-md-conformance-2026-09-30.md)。
