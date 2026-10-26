# 第一次写规则：做一个三分结束的小游戏

文档类型：教程（Tutorial）。

我们一起写出一份完整的 `.spire` 文件并运行它。游戏只有一个计分器：开局零分，玩家每操作一次加一分，达到三分就结束。最后再把目标改成五分。

需要 .NET 9 SDK 和本仓库。以下命令都从**仓库根目录**执行；可以用 `dotnet --version` 确认 SDK 版本。这里使用通用 core，不需要启动原版游戏。预计用时约 15 分钟。

## 1. 先看一次完整运行

仓库已经放好了[完整规则文件](../examples/core-choice-counter/first-counter.spire)，先运行：

```powershell
dotnet run --project docs/examples/core-first-host/CoreFirstHost.csproj
```

你会看到：

```text
start: Running
  tutorial:Counter: Score=0
submit: Running
  tutorial:Counter: Score=1
submit: Running
  tutorial:Counter: Score=2
submit: Terminal
  tutorial:Counter: Score=3
result: tutorial:ReachedTarget
  Score=3
```

每一行 `submit` 都表示宿主提交了一次玩家动作。这份演示宿主自动操作，方便你检查规则；以后可以把操作来源换成按钮、命令行输入或 AI。

接下来亲手写出这份规则。新建文件 `docs/examples/core-choice-counter/my-counter.spire`，按下面的顺序把代码块追加进去。第 3 步先观察零分开局，第 4 步再运行完整游戏。

## 2. 给规则取名，定义需要保存的状态

先写文件头：

```text
using namespace tutorial;
dsl_version = "core/0.3"
version = "1.0.0"
start = [Setup]
```

`tutorial` 是这份规则的名字前缀，例如后面的计分器叫 `tutorial:Counter`。`dsl_version` 指定文件使用的语言；这里从基础声明写起，所以用 `core/0.3`。它仍由当前引擎运行。`version` 是你自己的规则包版本。`start = [Setup]` 告诉引擎：开局时执行我们稍后定义的 `Setup`。

加上计分器的定义：

```text
entity Counter {
    fields = { Score = { type = Int(min = 0, max = 100) default = 0 } }
}
```

把 `entity` 理解为“定义一种要保存的东西”。`Counter` 有一个整数属性 `Score`，有效范围是 0 到 100，新建时为 0。这个范围是存储约束；达到三分结束的玩法稍后另外定义。

这里还没有真正的计分器。就像写出了表格的列名，你还需要创建一行实际数据。

写法上，`{ ... }` 是一个块，`[ ... ]` 是列表，`名字 = 值` 设置属性。名字区分大小写；`#` 可以写行内注释。

## 3. 在开局时创建一个计分器

继续追加：

```text
procedure Setup {
    locals = { counter = Binding }
    body = { create = { type = Counter scope = frameScope() into = counter } }
}
```

`procedure` 是可以执行的一段程序。我们在文件头把它登记为开局程序。`body` 里的 `create` 创建一个 `Counter` 实例，`Score` 会采用刚才的默认值 0。

`counter` 是这段程序的临时变量。`Binding` 让它保存创建出来的实例引用，`into = counter` 指定结果写到哪里。`frameScope()` 使用当前执行作用域；在本例的开局程序中，就是会话的根作用域。

注意大小写：`Counter` 是类型，`counter` 是临时变量。临时变量在程序结束后不作为游戏状态保存；创建出来的实体及其 `Score` 留在会话中。

为观察刚创建的计分器，继续追加：

```text
projection PlayerView {
    role = player
    types = { Counter = {} }
}
```

这份视图让玩家看到计分器的公开字段。保存文件，在仓库根目录运行：

```powershell
dotnet run --no-build --project docs/examples/core-first-host/CoreFirstHost.csproj -- docs/examples/core-choice-counter/my-counter.spire --observe-only
```

应当得到：

```text
start: Running
  tutorial:Counter: Score=0
```

`--observe-only` 是教学宿主的开局检查选项：只加载、开局并显示观察。这时我们还没写玩家动作，先确认计分器确实被创建出来。如果找不到计分器，检查文件头是否登记了 `start = [Setup]`，以及上面的视图是否已经追加。

## 4. 定义玩家操作和结束条件

先声明一种结果：

```text
terminal ReachedTarget { fields = { Score = Int } }
```

它表示“到达目标分数”，并携带最终分数。声明结果不会结束游戏，我们还要在动作里使用它。

接着追加动作：

```text
action AddOne {
    role = player
    locals = { counter = Binding }
    requires = less(field(first(type(Counter)), Counter.Score), 3)
    body = {
        let = { into = counter value = first(type(Counter)) }
        set = { target = counter field = Counter.Score value = add(field(counter, Counter.Score), 1) }
        if = {
            condition = greaterOrEqual(field(counter, Counter.Score), 3)
            then = { terminal = { result = ReachedTarget fields = { Score = field(counter, Counter.Score) } } }
        }
    }
}
```

这段代码对应一次操作的全过程：

1. `role = player` 指定谁可以操作。演示宿主创建的参与者使用这个角色名。
2. `requires` 要求当前分数小于三，满足时动作才可提交。
3. `let` 找到计分器，保存引用，供后面的指令使用。
4. `set` 把旧分数加一后写回。
5. `if` 检查新分数，达到三分时设置终局结果。

最容易卡住的是 `requires` 中的嵌套表达式。可以从里面向外读：

| 表达式 | 本例中得到什么 |
| --- | --- |
| `type(Counter)` | 所有计分器实例的查询 |
| `first(type(Counter))` | 第一个计分器的引用；本例只创建一个 |
| `field(first(type(Counter)), Counter.Score)` | 这个计分器当前的分数 |
| `less(..., 3)` | 分数是否小于三 |

`requires` 只决定这个动作现在能不能提交，真正的加分发生在 `body` 中。这里的 `body` 按书写顺序执行，所以结束条件读取的是加一后的分数。

设置 `terminal` 后，会话进入 `Terminal` 并停止后续执行。因此需要完成的修改应放在它前面。

保存文件，执行：

```powershell
dotnet run --no-build --project docs/examples/core-first-host/CoreFirstHost.csproj -- docs/examples/core-choice-counter/my-counter.spire
```

输出应与第 1 步相同。这里的 `--no-build` 复用已经构建好的 C# 宿主；宿主每次运行仍会重新加载并编译 `.spire` 文件，所以规则修改会生效。

注意第三次 `submit` 后状态变成了 `Terminal`，最后显示 `Score=3`。我们写出的结束条件已经生效。

## 5. 把结束目标改成五分

现在把目标改为五分。只需修改动作中的两处：

```text
requires = less(field(first(type(Counter)), Counter.Score), 5)
```

```text
condition = greaterOrEqual(field(counter, Counter.Score), 5)
```

再次运行同一命令。应多出两次 `submit`，最终显示 `Score=5` 和 `Terminal`。

这两处有各自用途：第一处决定动作是否可用，第二处决定何时结束。漏改一处会改变玩法，例如只把结束条件改成五分，三分后就没有合法动作，游戏也尚未结束。后面的规则模板可以把这两个条件绑定到同一个参数。

如果出现问题，先按下面的顺序检查：

| 现象 | 检查什么 |
| --- | --- |
| 文件找不到 | 是否从仓库根运行，参数是否是实际保存的文件路径 |
| 解析或编译错误 | 对照[完整文件](../examples/core-choice-counter/first-counter.spire)，检查花括号、拼写和大小写；错误中的源位置帮助定位 |
| 开局后看不到分数 | 是否登记了 `start = [Setup]`，是否创建了实例，投影是否包含 `Counter` |
| 分数到三以后无法继续，也没有结果 | 动作可用条件和终局条件是否使用同一个目标分数 |

## 6. 完成后的阅读

你已经写过状态、初始化、合法动作、状态修改、终局和观察。现在可以根据下一件要做的事继续：

- 想把目标分数变成配置，复用同一套规则：读[导入规则库与配置参数](../dsl/core/README.md#1-先运行再修改配置)，运行 `game.spire`，再看 [core/0.3 规则库指南](../dsl/core/0.3/authoring-guide.md)。
- 想接自己的界面或 AI：读[第一次写宿主](first-host.md)，用 C# 加载这份文件并驱动它。
- 想在动作中让玩家选加一还是加二：宿主教程的后半部分会运行 [counter.spire](../examples/core-choice-counter/counter.spire)，观察动作的暂停与续接。
- 写规则时遇到某个声明或表达式：查 [core/0.3 语言参考](../dsl/core/0.3/language-reference.md)。它供查阅，第一次做这篇教程不需要从头读完。

这个小游戏使用的是通用 core DSL。原版卡牌内容使用 base 的[内容 DSL 作者约定](../areas/base/README.md)，其卡牌声明和伤害等语义由游戏内容包提供。
