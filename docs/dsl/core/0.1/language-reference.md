# core/0.1 语言参考

本页供规则作者查询 core/0.1 的基础语义。使用 imports/template/use 时，查 [core/0.2 规则库与配置](../0.2/authoring-guide.md)；底层原语继续以本页为准。

2026-10-04 补充：默认作者入口采用 core/0.2。

状态：2026-10-04 已实现的首版通用配置语言。权威语法是 [CoreSpire.g4](../../../../core-the-spire/Core.TheSpire/Engine/Compilation/CoreSpire.g4)，语义入口是 [EngineDslCompiler](../../../../core-the-spire/Core.TheSpire/Engine/Compilation/EngineDslCompiler.cs)。执行语义复用 Engine；目标设计稿中的语法不自动属于本版。先读 [作者入门](../README.md) 与完整 [counter.spire](../../../examples/core-choice-counter/counter.spire)。

## 1. 基本形式与版本

每个文件定义一个包。两种等价外壳：

```text
using namespace mygame;
dsl_version = "core/0.1"
version = "1.0.0"
start = [Setup]
```

```text
package mygame {
    dsl_version = "core/0.1"
    version = "1.0.0"
    start = [Setup]
}
```

根属性：`dsl_version` 必填且必须精确匹配；`version` 默认 0.1.0；`dependencies`、`overrides`、`extends`、`start` 默认为空列表。依赖列表采用既有包 ID/版本约束，覆盖与扩展许可仍由共同链接器检查。每包普通名字限定为 `mygame:Name`，引用依赖时使用 `library:Name`，字段写 `Entity.Field` 或 `library:Entity.Field`。

标识符以 ASCII 字母或下划线起始，其后允许字母、数字、下划线及 `.:@/-`。字符串支持中文等 Unicode 文字及 `\"`、`\\`、`\n`、`\r`、`\t` 转义。整数为 Int64，带小数点的数值为 decimal；不接受指数形式。注释从 `#` 到行尾。

属性采用 `名字 = 值`；分号可省略。块用 `{}`，列表用 `[a, b]`，列表及调用允许末尾逗号。调用既可按位置传参，也可 `name = value`；重复、未知和多余参数都报错。body 里可以重复同一命令，其余属性块禁止重复属性。一般表达式使用 `add(a, b)` 等调用形式，没有中缀运算符。

## 2. 类型与字段

| 写法 | 含义 |
| --- | --- |
| `Int`、`Int(min = 0, max = 100)` | 整数及可选闭区间，默认 Int64 全范围 |
| `Bool` | 布尔 |
| `Fixed`、`Fixed(scale = 2, min = 0, max = 100)` | 固定精度小数，默认 scale=2 |
| `String`、`String(length = 128)` | 有界字符串，默认最大长度 128 |
| `Ref(Type)`、`Ref` | 特定实体引用、任意实体引用 |
| `Enum(Type)` | 声明枚举的序号 |
| `Participant` | 参与者编号 |
| `Binding` | 执行帧内通用绑定值，例如实体/作用域/决策结果 |
| `List(element, length = 256)` | 有界列表；实际存储仅支持现有 IR/Value 允许的元素类型 |

```text
entity Unit {
    owner = Owner
    extensible = false
    fields = {
        Hp = { type = Int(min = 0, max = 100) default = 10 visibility = Public }
        Owner = { type = Participant default = participant(1) }
        Target = { type = Ref(Unit) optional = true visibility = Hidden }
    }
}
enum Mode { members = [Ready, Done] }
scope Battle {}
```

实体字段属性：`type` 必填，`default`、`visibility`、`optional` 可选。默认可见性 Public，可用 OwnerPrivate/Hidden；实体 `owner` 指定其 Owner 字段。默认值仅接受字面量、`none`、`participant(id)` 和 `enum(Type, Member)`，不执行规则查询。实体字段为持久状态；局部量、事件/终局和 compute 形状复用执行值类型。

必需引用必须在构造期间赋值；不得带着缺失引用到达公开/暂停/命令结束边界。`Binding` 用于局部槽，不作为逃避持久字段类型检查的手段。数字越界、不支持的列表形状、字段生命周期等仍由共用链接/类型检查器拒绝。

## 3. 声明目录

| 声明 | 属性（括号中为默认值） |
| --- | --- |
| `entity Name` | `fields`、`owner`、`extensible(false)` |
| `enum Name` | `members` 列表必填 |
| `scope Name` | 可选 `parent = ScopeType` |
| `collection Name` | `element` 必填；`capacity(Int32.MaxValue)`、`ordered(true)`、`group`、`identity_visible(true)`、`order_visible(true)`、`size_visible(true)` |
| `relation Name` | `target` 必填；`many(true)`、`public(true)` |
| `event Name` | `payload = { name = Type }` |
| `terminal Name` | `fields = { name = Type }` |
| `procedure Name` | `body` 必填；可选 `parameters`、`locals`、`pure(false)`、`result` |
| `action Name` | `body` 必填；可选 `parameters`、`locals`、`role`、`requires`、`candidates`、`choices` |
| `subscription Name` | `event`、`reaction` 必填；`priority(0)`、`recheck`、`scope_payload` |
| `projection Name` | `role`、`types` 必填；`collections(true)`、`relations(true)`、`reveals` |
| `compute Name` | `backend`、`source_path`、`source_hash` 必填；`entry`、`inputs`、`outputs`、`limits` |

程序参数和局部量写 `parameters = { target = Ref(Unit) }`、`locals = { result = Binding count = Int }`。参数在前、局部槽在后；局部名字可在表达式中直接读取。`into` 必须是已声明的局部量；不能向参数或查询别名写入。参数域与返回类型由共用检查器验证。

动作的引用候选采用 `candidates = { parameter = target query = type(Unit) }`；当前每动作一个候选查询绑定。离散值域写 `choices = { mode = [enum(Mode, Ready), enum(Mode, Done)] }`。未绑定的自由参数由宿主提供，`LegalActions` 的 None 位置不是一个可直接提交的合法参数。`requires` 在枚举/提交时求值，必须是纯布尔条件。

监听器按 priority 升序、再按声明顺序执行。reaction 的参数按 event payload 位置匹配；`recheck` 可用 reaction 参数，`scope_payload` 可从载荷中的实体决定监听器作用域。事件/监听器与原动作使用同一执行栈。

投影例子：

```text
projection PlayerView {
    role = player
    types = { Unit = { include = true hidden = false } }
    collections = true
    relations = true
}
```

每个类型可设 `include(true)`、`owner` 字段名和 `hidden(false)`。`reveals = [{ authority = Knowledge relation = Known }]` 显式发布授权识别。`hidden=true` 是包作者授予该投影读取隐藏字段的权限；必须符合游戏的公开信息契约，不能当作默认便捷选项。已知位置需另有公开事实，授权识别不自动公开完整牌序。

compute 的 `limits` 可设 `instructions`、`memory`、`input_values`、`output_values`、`call_depth`；未给定值复用 `ComputeLimits.Default`。compute 仅接收声明输入并返回声明输出，不直接接管 Session；运行时必须配置对应后端。

## 4. 表达式和查询目录

下表使用位置参数；对应命名参数与实现签名一致。局部量、参数、查询别名以及字面量可直接用于表达式。

| 表达式 | 用途 |
| --- | --- |
| `participant(id)`、`enum(Type, Member)` | 类型化常量 |
| `field(target, Entity.Field)` | 读字段 |
| `frameScope()`、`entityScope(entity)` | 当前执行作用域、实体所属作用域 |
| `add/subtract/multiply/divide/modulo/minimum/maximum(left, right)` | 数值计算；这里斜线表示各个独立函数名 |
| `equal/notEqual/less/lessOrEqual/greater/greaterOrEqual(left, right)` | 比较 |
| `and/or(left, right)`、`not(value)` | 布尔运算 |
| `intToFixed(value)`、`fixedToInt(value, rounding = Truncate)` | 显式数值转换 |
| `count(query)`、`exists(query)`、`first(query)` | 查询归约；first 无候选返回 None |
| `isNone(value)`、`collectionIndex(collection, item)` | 检查缺省、成员位置 |
| `concat(a, b, ...)` | 字符串组合 |
| `pureCall(Procedure, arg, ...)` | 纯程序调用 |
| `aggregate(query, operation, item, value)` | 聚合；operation 为既有 AggregateOp（Count/Sum/Minimum/Maximum） |

Count 可省略 item/value；需要值表达式的聚合须提供绑定。固定小数和整数不隐式互转；舍入、溢出、除零按 Engine 数值 profile 执行。

| 查询 | 用途 |
| --- | --- |
| `type(Type, scope = ScopeType)` | 按类型及可选作用域类型查询 |
| `collection(Name, scope = ScopeType)` | 保留集合顺序，按可选作用域筛选 |
| `relation(Name, owner)` | 关系目标 |
| `refList(local)` | 引用列表局部量 |
| `where(query, item, predicate)` | 筛选 |
| `sort(query, item, key, descending = false)` | 显式排序 |
| `take(query, count)`、`skip(query, count)` | 截取 |

`item` 是该谓词/排序键/聚合/循环内的临时绑定，不可遮蔽已有参数或局部量。作用域参数是类型筛选，不是把全局集合复制成每作用域一份；多个玩家/战斗的成员隔离仍由游戏规则表达。

## 5. 指令目录

命令统一写 `命令 = { 属性 = 值 ... }`。下表列出的名字即属性名，`?` 表示可选。

| 命令 | 属性 |
| --- | --- |
| `let` | `into, value` |
| `set` | `target, field, value` |
| `create` | `type, scope, into, fields?, collection?, position?` |
| `createScope` | `type, into, parent?` |
| `destroy`、`destroyScope` | `target`；`scope` |
| `addToCollection`、`moveInCollection` | `collection, item, position?, index?` |
| `removeFromCollection` | `collection, item` |
| `shuffle` | `collection, stream` |
| `addRelation`、`removeRelation` | `relation, owner, target` |
| `clearRelation` | `relation, owner` |
| `sampleInt` | `stream, min, max, into`，范围 `[min, max)` |
| `samplePick` | `stream, candidates, into`，按 query 序列抽索引 |
| `emit` | `event, payload?` |
| `decide` | `participant, kind, prompt, into, candidates?, min?, max?` |
| `call` | `procedure, args?, into?` |
| `if` | `condition, then, else?` |
| `repeat` | `count, body` |
| `forEach` | `query, item, body` |
| `return` | `value?` |
| `terminal` | `result, fields?` |
| `fail` | `message` |
| `compute` | `asset, inputs?, outputs?` |

fields/payload/inputs 都是 `{ 名字 = 表达式 }`。compute outputs 是 `{ 输出名字 = 已声明局部量 }`，call args 是表达式列表。position 默认为 End，可用 Start/AtIndex；AtIndex 需要 index，create 的有序插入使用现有构造指令能力，不支持额外 index 属性。

决策 kind 来自 DecisionResponseKind：EntityRef、EntityRefList、Int、Bool、Enum、Participant、Confirm。候选在请求时冻结为该次请求的列表，过期请求不可回答。min/max 按种类解释；engine/ir@0.3 的 Enum 决策结果必须为具体非空 Enum 槽，默认范围 0..成员数-1，显式 min/max 只能缩小该闭区间。非法回答保留请求，非法声明范围在发布请求前故障。`Repeat` 有限次迭代并受命令预算约束；`ForEach` 使用入口查询快照。终局立即停止，不自动结清尚未运行的监听器。

engine/ir@0.3 按包链接顺序与包内 start 登记顺序初始化，暂停后先完成当前入口再继续剩余入口；亦可登记一个 Setup 并顺序 call。旧 0.2 的 LIFO 安排须按 [冻结基线](../../../contracts/core-engine-baseline.md) 迁移。详细暂停、预算及指令级故障语义见 [开发者指南](../../../design/core-the-spire/developer-guide.md)。

## 6. 加载、扩展和错误

宿主调用 `EngineDslCompiler.LoadFiles(paths...)`，文件按依赖顺序加载并链接。需要游戏声明扩展时，逐个调用 `Compile(source, sourceName, dependencies, extensions)`，再用 `RuleSetBuilder.Add(...).Build()`；不提供另一套运行时。

`IEngineDslExtension.Identity` 必须版本化；DeclarationKinds 不得覆盖 core 或其他扩展。Declare 阶段先引入名称，Bind 阶段在 core 字段/参数形状建立后补全规则，随后绑定 core 程序。两阶段按文件声明顺序执行。扩展须自行验证其属性/引用及来源，把定义转成普通 SDK/IR；它由游戏模块拥有，不要求 core 理解卡牌效果。

语法、未知属性、重复名字、未定义引用等报 `DslSourceException`，包含源文件和从 1 开始的行列。最终 IR 类型/纯性检查仍报 `EngineFaultException`，Fault.Context 带最近源码位置。运行调试轨迹也使用这些位置；C# SDK 创建的 IR 未提供来源时位置可为空。

当前运行身份包括 `engine/ir@0.3`、包内容和 CompilerIdentity（文本入口 `core-dsl/0.1` 及所用扩展版本）。Debug 和文件路径不参与语义身份。升级随机/编译语义时不能复用旧训练资产身份；0.2 已变更 SamplePick 顺序，0.3 收口启动顺序与枚举决策域；语言版本保持 core/0.1。

## 7. 生成工具和未交付范围

ANTLR 工具及 runtime 均固定 4.13.1。生成的 C# 纳入源码，普通构建不运行 Java。修改 grammar 后，维护者使用 Java 执行：

```powershell
./tools/dsl-generate/generate-core.ps1
```

可用 `-Java` 指定 Java 路径。脚本缓存官方 jar 并核对 SHA256；Java/ANTLR 不进入游戏运行环境。工具及 C# runtime 说明见 [ANTLR 官方下载](https://www.antlr.org/download) 和 [官方 runtime 包](https://www.nuget.org/packages/Antlr4.Runtime.Standard/4.13.1)。

本版没有文本 include、模块宏、任意脚本、自由递归控制语法、通用 JSON 状态注入或完整 STS 高级声明。没有已安装的 IDE/LSP 插件；后续高亮、折叠和语言服务必须复用该 grammar/编译元数据，不维护第二份语义。首版验证的是已交付通用路径与例子，不宣称所有 IR 组合、所有游戏包扩展或原版完整内容都已验收。
