# base/0.0.2 作者语言参考

文档类型：参考（Reference）。本页定义活动卡牌前端的共享变体与数值语法；原有领域效果见[0.0.1 原语目录](../0.0.1/primitives.md)。运行入口见[卡牌开发工具](../../../tools/base-spire-dev/README.md)。

## 版本与兼容输入

文件头写 `using namespace mymod;` 和 `dsl_version = "base/0.0.2"`。未知版本拒绝。无版本或显式 `base/0.0.1` 是兼容输入，仍编译到同一 typed program，不另建执行器；common/values 必须显式使用新版本。旧 `.author` 仅保留诊断输入适配，正式内容不回退归档。

## 完整变体与整字段替换

卡级 `common` 提供共享字段；`normal` / `upgraded` 按字段整体替换它。省略字段继承 common；空 `play = {}`、`hooks = {}`、`keywords = {}` 分别清空对应字段。替换 hooks 不保留 common 的其他 hook，替换 play 不追加效果。无隐式深合并。配置字段不得重复；执行块内重复效果仍按源码顺序执行。

`values = { hit = { normal = 5 upgraded = 8 } }` 声明编译期整数；变体及 common 内的 `values.hit` 在输入阶段替换为相应整数。两形态都必须声明，未知名字、非整数和重复配置带原始源位置报错。values 不是运行时变量。

```text
using namespace mymod;
dsl_version = "base/0.0.2"
card Sweep {
  values = { hit = { normal = 5 upgraded = 8 } }
  common = {
    type = attack rarity = uncommon cost = X target = { selectAll = {} }
    keywords = {}
    play = { repeat = { count = PaidX effect = { attack = { damage = values.hit target = allEnemies } } } }
    hooks = {}
  }
  normal = {}
  upgraded = {}
}
```

正式迁移入口包括 [Whirlwind、Stoke 等](../../../tools/base-migration-baseline/BaseSpire/Cards/Content/ironclad-cards.spire)与 [Predator、Outmaneuver](../../../tools/base-migration-baseline/BaseSpire/Cards/Content/next-turn-cards.spire)。示例只说明语法，不单独证明整包原版等价。

## 组合数值与读取时点

数值效果字段接受整数、`PaidX`、`PaidStarX`、`context.owner.block`、`variables.NAME`、`locals.NAME`，以及二元 `add(a,b)`、`subtract(a,b)`、`multiply(a,b)`、`divide(a,b)`、`minimum(a,b)`、`maximum(a,b)`。除法使用 Engine 整数运算，不是浮点计算。

- `pileCount(pile=hand)`：具名 pile 可为 hand/draw/discard/exhaust。
- `powerAmount(target=context.target,power=POWER)`：显式目标与已链接能力；无选中目标的内容不能使用它。
- `enemyPowerSum(power=POWER)`：实时汇总存活敌人的已链接能力层数，不请求选择目标；Mirage组合此数值与正常格挡修正。
- `variables.NAME`：已声明卡牌实例变量；未知变量拒绝。
- `PaidX`：支付阶段、扣除能量前捕获的 X；效果之后改变能量不会改变该快照。
- `let = { name = n value = pileCount(pile=hand) effect = { ... } }`：进入时求值一次；body 用 `locals.n`，离开后不可见，内层同名不覆盖外层。

一般表达式在效果执行位置实时读取。动态 repeat 次数在进入循环时读一次，循环体各效果仍实时读取。旧 scaled/withCount/repeatForX 在输入层适配；无需为一般表达式套多层旧块。算术不引入第二套状态或调度器。选牌、生成等原语的其他范围仍以领域原语支持为准。

## 上下文与生命周期规则

### Osty 宠物基础操作

`summonOsty = { amount = 6 }` 首次创建宠物，活着时同时增长最大/当前生命，死亡后复用身份并按本次金额重设生命。`healOsty = { amount = 3 }` 只治疗存活宠物并封顶；`loseOstyHp = { amount = 3 }` 为直接生命损失、死亡保留身份；`ostyAttack = { amount = 7 }` 需要已选敌人且宠物存活，当前基础攻击不借用玩家Strength，也未实现完整宠物攻击修正。各操作只接受一个amount，复用共享数值表达式，负值运行时明确失败。

有Osty内容的包生成Osty实体及公开OstyPets集合，powered敌方攻击先扣玩家格挡，再把未挡伤害转给存活宠物；非powered伤害与直接玩家HP损失不转移。召唤修正、完整hook、宠物Power/目标及正式角色初始化未交，准确边界见[Osty契约](../../design/mod-the-spire/character-combat/spec.md#osty-基础生命周期)。

### 中毒与催化

Power 声明 `role = Poison` 或 `role = Accelerant`，卡牌通过已有 `applyPower` 挂载/叠层，不增加逐卡执行器。分别解析自定义Power和卡牌时，`SpireCardSource.Parse` 第三个参数必须传入声明名称到ID的映射；只写命名空间不能替代声明表链接。

拥有者阵营回合开始时触发中毒，次数为 `min(Poison层数, 1 + 存活对手Accelerant层数总和)`；每次按当前层数造成无格挡/无力量修正伤害，存活后递减，耗尽销毁。既有生命损失上限仍适用。当前接口只覆盖玩家/敌人、掉血计数与既有死亡结算，不代表完整CreatureCmd.Damage或宠物支持。回合顺序与未支持hook见[中毒契约](../../design/mod-the-spire/character-combat/spec.md#poison-基础生命周期)。

### 中毒内容的触发规则

正式中毒内容的触发能力使用以下`rules`字段，值必须是已链接的Power名称：

| 字段 | 触发与效果 |
| --- | --- |
| `applyPowerOnDraw` | 每张实际抽入手牌的牌触发一次，对存活敌人按拥有者此能力层数施加指定Power；手牌已满或NoDraw阻止时不触发 |
| `applyPowerAtTurnStart` | 玩家回合开始完成抽牌后，对存活敌人按层数施加指定Power |
| `applyPowerOnUnblockedAttack` | 玩家powered攻击造成正的未格挡伤害后，给仍存活的目标按层数施加指定Power；每次命中分别判断 |
| `damageAllEveryThirdPower` | 玩家每成功施加指定Power三次，对存活敌人造成等同自身层数的unpowered伤害；余数跨回合保留，公开为Power的`ApplicationCount` |

卡牌`applyPower`可显式写`target = randomEnemy`，每次施加独立消耗一次CombatTargets抽取，空域不抽。静默猎手正式开局和完整角色范围仍查主清单。

### 奇巧与授予状态

原生`keywords = { sly }`、实例`AddedSly`和`SingleTurnSly`任一生效时，手动弃牌会将该实例排队。整个弃牌批次完成后，按源顺序沿共享自动出牌流程免费打出，普通回合末清手不触发。嵌套弃牌使用独立批次，不能消费外层尚未执行的条目。

`chooseCard`支持`filter = nonSlySkill`，仅接受当前没有奇巧的Skill，排除Power、Status、Attack及已授予状态；它与`nonAttack`不同。选择体`onSelected = { grantSelectedSly = {} }`给当前绑定实例授予本回合奇巧，参数块必须为空，无选择或forEachCard上下文拒绝。HandTrick组合此筛选及效果；空域不问、唯一候选在`autoSelect = true`时自动处理。

Power声明`rules = { grantSlyAfterSkill = true }`在拥有者每次打完Skill后设置该牌的`AddedSly`。本回合状态在回合末清理，本场状态保留到战斗结束；升级保留两者，战斗克隆按创建时状态复制，新实例及变换不继承，永久牌组不从战斗副本写回。实际字段由正式公开Profile发布。

Power声明`rules = { retaliateBeforeAttack = true }`在敌方powered攻击结算格挡／生命前，按自身层数对攻击者造成unpowered反击；零伤害／全挡也触发，unpowered攻击不触发。当前命中在反击击杀攻击者后仍完成，后续命中跳过死者来源。Abrasive使用此规则；它不同于火焰屏障的受击后反击。源顺序与范围见[奇巧牌族契约](../../design/mod-the-spire/character-combat/spec.md#静默猎手奇巧牌族)。

### 灾厄

灾厄基础入口：Power 声明 `role = Doom` 后可通过 `applyPower` 挂载；卡牌 `resolveDoom = {}` 立即解析敌方灾厄。参数块必须为空，无注册 Doom Power 时编译失败。两侧回合结束前也会按 HP ≤ 正灾厄层数批量捕获并致死，不走格挡或伤害修正。`DiedToDoom` 批次事件仅为接口，死亡防止、复活与完整源hook仍未实现，详见[灾厄基础契约](../../design/mod-the-spire/character-combat/spec.md#doom-基础接缝)。

### 充能球

```spire
channelOrb = { kind = Lightning amount = 1 }
evokeOrb = { amount = 1 dequeue = true }
addOrbSlots = { amount = 2 }
removeOrbSlots = { amount = 1 }
```

kind仅接受Lightning/Frost/Dark/Plasma/Glass，amount接受组合数值，缺省1，负数执行失败。evokeOrb默认激发首球并出队，dequeue=false保留同一球，可重复激发；空队列不操作。未知/重复字段、未知种类或非法dequeue值拒绝加载。槽位封顶10、减少封底0，减少时丢弃尾球而不激发。满槽充能先激发首球；后续充能受战斗结束保护。基础槽位来自SliceAuthorModel.BaseOrbSlots编译配置，不是卡牌DSL字段；仅基础槽位为0的角色可在零槽充能时自动补1槽。

数值、Focus、回合时点及未支持hook见[充能球契约](../../../docs/design/mod-the-spire/character-combat/spec.md#充能球)。这不意味着公开工厂已支持Defect或全部依赖球的原版牌。

### 星星资源与锻造

`starCost = N`声明独立于能量费用的非负整数星星费用；`starCost = X`在支付阶段捕获全部现有星星。`PaidStarX`读取该快照，后续获得/失去星星不改变它。普通/升级费用按完整变体展开，手动出牌同时检查两种资源；自动出牌不扣资源。当前不支持星星费用修正、以星星代付能量及X值修正参与者，不能据此声称完整摄政王支持。

`gainStars = { amount = N }`、`loseStars = { amount = N }`、`setStars = { amount = N }`接受组合数值，目标仅为owner；可显式写`target = context.owner`。输入捕获一次、负数失败，失去最低0；设定增加走获得接口，减少直接设定。星星保留到下一回合，进入下一战斗清零。当前声明范围为0～99999。

`forge = { card = Blade variable = Damage amount = N }`链接同源声明的武器及两变体都有的实例变量。非消耗区没有该武器时生成普通实例，满手进入弃牌堆；随后增加所有战斗牌区中该定义实例的变量，包括消耗区。升级/普通数值可用values；数量捕获一次、负数失败、生成不抽随机数。当前未实现临时dupe身份、CreatedThroughForge及全部AfterForge参与者；此入口只交付生成/成长共享机制，不等于完整SovereignBlade特殊Power行为。参见[角色战斗能力契约](../../../docs/design/mod-the-spire/character-combat/spec.md)。

owner、选中敌人 target、卡牌实例变量及 lexical local 是不同上下文。无目标内容拒绝选中目标依赖；未知 local、变量、字段、目标和不可用上下文不能默默忽略。原语没有开放的任意 scope 字段，写未知 scope 会拒绝。

正式能力可声明 `rules = { afterEnergyReset = GrantDeferredEnergy(owner=context.owner, power=context.power, amount=context.power.stacks) }`。参数必须具名、完整且无未知名；本 hook 提供 owner/power/stacks，不提供 card/target。宿主声明类型，文本过程在同一 EnginePackageBuilder 中经 core/0.3 BindInto 绑定，最终统一 Build 冻结。机制体见[生命周期文本库](../../../tools/base-migration-baseline/BaseSpire/Authoring/Content/lifecycle-rules.spire)。能量受阻时仍按原时点移除一次性能力。

## 迁移检查

能力声明可使用 `negativeStacksAreInfinite = true` 配合固定伤害倍率及回合末衰减：负的非零层数仍应用倍率且不会按回合衰减，正层数继续按原时点处理，零层数不应用倍率。Shrink用此声明表达来源的无限持续；未声明的Weak/Vulnerable仍按正持续层数门控。该属性必须为布尔值，当前不表示所有能力的负层数合并、覆盖或其他hook均已支持，见[机制契约](../../design/mod-the-spire/card-primitives/spec.md#负层数无限持续的伤害倍率)。

生成候选选择使用`generateCards = { pool = { character = IroncladCardPool } count = 3 type = attack distinct = true choose = true allowCancel = true cost = 0 }`。当前完整角色池仅注册Ironclad；也可用显式`pool = { card = Name ... }`。`choose=true`要求`distinct=true`及1..999的整数字面量count；`allowCancel`仅可与choose一起使用。完整合法池洗牌一次再取count个候选，选择一个或取消；所选真实实例入手（满手进入弃牌），未选清理，续接不补抽。选择模式的cost=0持续到回合结束；原直接生成模式保持首次打出或回合结束清理。详见[机制契约](../../design/mod-the-spire/card-primitives/spec.md#生成候选并选择env-t06-pr05)。

先用工具 check 检查链接，再 play 普通/升级实际实例。已有实例升级增加定义差额，保留成长与临时费用修饰；生成升级实例使用升级初值，不等同于升级命令。整包身份、地图、公开 Run 与严格对拍须另按对应门槛验证，check 注册成功不能替代行为验收。
