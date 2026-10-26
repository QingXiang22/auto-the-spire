# core/0.3：DSL 规则库与作者配置

文档类型：参考（Reference）。本文供你查询 core/0.3 的导入、模板、参数和实例配置。基础声明、表达式、查询和命令沿用 [core/0.3 参考](language-reference.md)。

初次使用时，从[作者入门](../README.md)开始，再对照带注释的[配置入口](../../../examples/core-choice-counter/game.spire)和[规则库](../../../examples/core-choice-counter/rules/score-race.spire)。同包绑定与具名调用见[作者参考](authoring-reference.md)。

## 1. 三种复用单位

| 写法 | 复用内容 | 发生阶段 |
| --- | --- | --- |
| `procedure` | 带参数的计算或执行过程 | 游戏运行时，同一执行栈调用 |
| `template` | 类型、过程、动作、事件、观察等一组声明 | 编译时展开并绑定 |
| `use` | 某模板的一次具名实例及配置值 | 编译时选库、校验参数、生成规则 |

模板在编译时展开，执行共用同一 Session。声明和配置统一使用 `dsl_version = "core/0.3"`，旧版输入明确拒绝。

## 2. 文件导入与包依赖

```text
using namespace tutorial;
dsl_version = "core/0.3"
imports = ["rules/score-race.spire"]
use Game { template = counters:ScoreRace }
```

`imports` 是字符串路径列表，路径相对于**声明该导入的文件**解析；库可以继续导入其他库。`LoadFiles("game.spire")` 递归加载依赖，按规范化路径去重，循环导入、缺失文件、多个文件声明同一包 ID 都报错。导入的库自动进入当前包依赖并参与链接。命名空间是包 ID，文件路径不充当内容 ID。

`LoadFiles(params string[] paths)` 加载核心语言文件；需要自定义声明扩展时，使用 `LoadFiles(paths, extensions, dependencies)` 重载，返回已链接的 `RuleSet`。单份文本可用 `Compile(..., dependencies: ..., extensions: ...)`：自己取得依赖包，DSL 用 `dependencies = [counters]` 显式声明依赖，编译后统一交给 `RuleSetBuilder`。`Compile` 不读取磁盘，含 `imports` 时明确拒绝；不暗中按 sourceName 加载文件。签名见 [EngineDslCompiler.cs](../../../../core-the-spire/Core.TheSpire/Engine/Compilation/EngineDslCompiler.cs)。

依赖包导出的模板可以复用；模板内部使用的库依赖按传递闭包解析。最终链接仍须提供全部依赖包。导入表示可用的定义与依赖，不保证初始化按导入书写顺序执行。

## 3. 声明模板和参数

```text
using namespace counters;
dsl_version = "core/0.3"
template ScoreRace {
    parameters = {
        target = { type = Int(min = 1, max = 10000) default = 3 }
        increments = { type = List(Int(min = 1, max = 100), length = 16) default = [1, 2] }
    }
    body = {
        # 在这里写 entity / procedure / action / projection 等声明。
        # 完整可运行定义见 rules/score-race.spire。
    }
}
```

`parameters` 可省略；每个参数必须有 `type`，`default` 可省略。没有默认值的参数必须由 `use.args` 提供。模板参数当前支持 **Int、Bool、String、Fixed、List(Int)** 字面量；复用已有形状及范围/长度检查，不允许实体引用、运行时表达式、可执行代码或任意对象作为配置参数。

`body` 必须是声明块，支持核心声明和嵌套 `use`；可以有 `start` 初始化入口列表。不能在 body 内改命名空间、语言版本、依赖、导入或定义模板。body 可使用已注册的 `IEngineDslExtension` 声明，加载方式见[作者参考](authoring-reference.md)。模板及模板内声明、实例名使用简单名称，不含 `:` 或 `.`。

模板发布时验证参数形状、默认值、结构和 `arg` 占位符；未实例化的 body 不保证已经完成全部名称/类型/行为检查。只有实例化、链接、运行的验证记录才能分别证明那些阶段，不能用“库文件可解析”代替验证。

## 4. 配置实例与占位符

```text
use Game {
    template = counters:ScoreRace
    args = { target = 5 increments = [2, 4] }
}
```

未知模板、重复实例名、未知参数、缺少必填参数、类型/范围不符都会拒绝。未提供的参数采用默认值。配置值不会自动成为 Session 字段；希望运行时观察到的值需要库显式声明和发布。

模板内的 `arg(target)` 或 `arg(name = target)` 在 AST 上替换为字面量，然后交给普通绑定器。例如 `choices = { delta = arg(increments) }` 产生有限合法动作参数域。字符串不插值，`"arg(target)"` 仍是普通字符串；调用只能引用已声明参数。显式动作 choices 空列表会报错，避免空配置意外退化成自由参数动作。

## 5. 名称、隔离与组合

`tutorial` 包中的 `use Game` 展开库的 `Counter` 为 `tutorial:Game.Counter`、`Add` 为 `tutorial:Game.Add`。模板内部的非限定类型、字段、过程和事件引用优先解析实例自己的声明，随后解析定义模板的库命名空间。程序参数、局部变量、字段名称保持原名。具体游戏的同名函数不能捕获库内部调用。

显式 `counters:BelowTarget` 等限定 ID 仍指相应公共定义。模板外引用实例内容时使用 `Game.Counter`、`Game.Add`；嵌套实例可以形成 `Game.Left.Counter`。例如一个库组合两个计分组件：

```text
using namespace pairs;
dsl_version = "core/0.3"
imports = ["score-race.spire"]
template Pair {
    parameters = { goal = { type = Int(min = 1, max = 10000) default = 10 } }
    body = {
        use Left { template = counters:ScoreRace args = { target = arg(goal) } }
        use Right { template = counters:ScoreRace args = { target = arg(goal) } }
    }
}
```

此段展示组合语法，调用者再导入 pairs 库并 `use Game { template = pairs:Pair }`。这些实例拥有隔离的声明，但仍处于同一个 Session 并共享整局终局；共享玩家、轮次、资源或组合胜负需明确设计。本版未增加“向模板注入另一个实例的实体类型/行为”的参数能力，实际调用方提出需求后再扩展。

模板的 `start` 自动收集到调用包的启动入口。初始化按包链接顺序、包内登记顺序执行；模板按 use 展开顺序登记。一个入口暂停时先完成其续接，再执行剩余入口；有依赖时应明确导入/登记顺序，也可在单个 Setup 中顺序调用。初始化暂停与续接要求见[核心约定](../../../contracts/core-engine-baseline.md)。

## 6. 标识、诊断和资源边界

| 项目 | 约束 |
| --- | --- |
| 版本 | 前端标识含 `core-dsl/0.3`；Engine 版本以[核心约定](../../../contracts/core-engine-baseline.md)和工程配置为准 |
| 库标识 | 模板声明、参数和默认值的规范化 AST 哈希进入编译标识，包括未实例化的模板；依赖包参与最终规则标识 |
| 无语义变化 | 注释、源码路径和源码位置不改变标识；版本/配置/展开后的规则改变标识 |
| 报错来源 | 参数错误定位配置文件；展开后的绑定错误保留库位置，并附实例名及 use 位置 |
| 展开保护 | 禁止递归模板；最多深度32、实例8192、展开成员4096。总实例容量为内容扩展提升，递归与成员限制保持 |
| 数据生命周期 | 导出模板元数据只读，仅编译使用，不进入 Session 可变状态或公开观察 |

库升级、配置变化产生新的规则标识；不要冒名复用旧训练/快照资产。模板只压缩重复声明，不自行推断规则合理性、观察充分性或原版一致性。公开知识仍由规则库在当前状态中表达，投影不能因方便而发布权威隐藏状态。

## 7. 交付验证记录与接续

检查覆盖真实示例加载、只改配置的不同结果、多实例隔离、库函数名称隔离、嵌套传递依赖、模板标识、参数与来源错误、循环/缺失导入。实际结果见对应提交的 CI。

接续由 mod/base 按真实机制把可复用规则放入 DSL 库，内容文件填写参数/组合。完整 STS 内容、整局环境与源规则差异测试继续按 [GitHub 任务清单](https://github.com/QingXiang22/auto-the-spire/issues/28) 推进；本例不关闭这些范围。
