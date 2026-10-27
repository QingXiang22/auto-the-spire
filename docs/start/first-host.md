# 第一次写宿主：加载规则，操作游戏，读取结果

文档类型：教程（Tutorial）。

我们一起创建一个 C# 控制台程序，让它运行计分小游戏：先看到零分开局，再提交一次加分，接着运行到终局，最后回答动作中出现的选择。每次修改后都运行程序，观察刚加上的代码产生了什么结果。

需要 .NET 9 SDK、本仓库和基本 C# 知识。以下命令都从**仓库根目录**执行。规则文件已经提供，不需要在这篇教程中编写 DSL。预计用时约 20 分钟。

## 1. 创建控制台项目

运行：

```powershell
dotnet new console --framework net9.0 --output .artifacts/tutorials/core-api-lesson
dotnet add .artifacts/tutorials/core-api-lesson/core-api-lesson.csproj reference core-the-spire/Core.TheSpire/Core.TheSpire.csproj
dotnet run --project .artifacts/tutorials/core-api-lesson/core-api-lesson.csproj
```

前两条命令创建项目并添加内核引用，最后一条应输出 `Hello, World!`。接下来的代码都写入 `.artifacts/tutorials/core-api-lesson/Program.cs`，每次都用相同的运行命令检查。

如果项目目录已经存在，接着使用其中的项目；后面的步骤会替换这个练习项目的 `Program.cs`。不要将这些练习代码写入生产宿主。

## 2. 加载规则，看见零分开局

把 `Program.cs` 的内容替换为：

```csharp
using CoreTheSpire.Engine;

var rules = EngineDslCompiler.LoadFiles(
    "docs/examples/core-choice-counter/first-counter.spire");
var session = new Session(rules, seed: 42, participantRoles: ["player"]);
var player = session.Participant(0);
var projection = session.ProjectionsFor(player).Single();
session.Start();
Print("start");

void Print(string command)
{
    var view = session.Observe(player, projection);
    Console.WriteLine($"{command}: {session.Status}");
    foreach (var instance in view.Instances)
        Console.WriteLine($"  {instance.TypeId}: " + string.Join(", ",
            instance.Fields.Select(field => $"{field.Name}={field.Value}")));
}
```

再次运行：

```powershell
dotnet run --project .artifacts/tutorials/core-api-lesson/core-api-lesson.csproj
```

应当得到：

```text
start: Running
  tutorial:Counter: Score=0
```

注意零分计分器已经出现。`LoadFiles` 加载规则，`Session` 保存这一局，`Start()` 执行开局程序；`Print` 中的 `Observe` 读取玩家视图。

这里的参与者角色是 `player`，与规则中的 `role = player` 一致。`Participant(0)` 取第一位参与者；本例只有一个玩家、一个投影，所以使用 `Single()`。

若提示规则文件不存在，检查终端是否仍在仓库根目录。程序中的路径相对于当前工作目录。

## 3. 提交一次加分

在文件末尾追加：

```csharp
var action = session.LegalActions(player).Single();
session.Submit(action.Definition, player, []);
Print("submit");
```

再次运行同一命令，应当得到：

```text
start: Running
  tutorial:Counter: Score=0
submit: Running
  tutorial:Counter: Score=1
```

这次新增的输出是 `Score=1`。`LegalActions` 找到当前可用的动作，`Submit` 提交它。本例的动作没有参数，因此传入 `[]`。

再运行一次。你仍会看到从零分到一分：每次启动程序都会创建一局新的会话。

## 4. 连续操作到游戏结束

把刚才追加的三行代码替换为：

```csharp
while (session.Status == SessionStatus.Running)
{
    var action = session.LegalActions(player).Single();
    session.Submit(action.Definition, player, []);
    Print("submit");
}

var outcome = session.Terminal!;
Console.WriteLine($"result: {outcome.Definition.QualifiedId}");
Console.WriteLine($"  Score={outcome.Fields[0]}");
```

再次运行。输出应从零分依次增加到三分，末尾是：

```text
submit: Terminal
  tutorial:Counter: Score=3
result: tutorial:ReachedTarget
  Score=3
```

注意第三次提交后状态变成了 `Terminal`，循环退出。结果由规则产生，我们从 `session.Terminal` 读出它。

## 5. 回答动作中的选择

接下来让同一个宿主运行一个会追问玩家的规则。先把加载路径中的 `first-counter.spire` 改成 `counter.spire`。

再把第 4 步写入的循环和结果输出替换为下面的代码。第 2 步的加载、开局和 `Print` 保持原样：

```csharp
var selections = new[] { 1, 0 }; // 这次练习固定先选加二，再选加一。
var selectionIndex = 0;

while (session.Status is SessionStatus.Running or SessionStatus.AwaitingDecision)
{
    if (session.Status == SessionStatus.Running)
    {
        var action = session.LegalActions(player).Single();
        session.Submit(action.Definition, player, []);
        Print("submit");
    }
    else
    {
        var request = session.PendingDecision!;
        Console.WriteLine($"decision: {request.Kind} {request.Prompt}");
        var selected = request.Candidates[selections[selectionIndex++]];
        session.Answer(new DecisionAnswer(request.RequestId, Instances: [selected]));
        Print("answer");
    }
}

var outcome = session.Terminal!;
Console.WriteLine($"result: {outcome.Definition.QualifiedId}");
Console.WriteLine($"  Score={outcome.Fields[0]}");
```

保存并运行同一命令。输出中还会出现两个 `Option` 实例，分别显示 `Delta=1` 和 `Delta=2`。重点观察第一轮操作中的计分器：

```text
submit: AwaitingDecision
  tutorial:Counter: Score=0, Attempts=1, Applied=0
```

随后出现 `decision: EntityRef Choose an increment`，回答后的计分器变为：

```text
answer: Running
  tutorial:Counter: Score=2, Attempts=1, Applied=1
```

动作先记录了一次操作，然后停下来等待选择。`Answer` 让它从暂停的位置继续，因此 `Attempts` 仍为一，分数增加了二。

第二轮固定选择加一，最后应看到：

```text
answer: Terminal
  tutorial:Counter: Score=3, Attempts=2, Applied=2
```

程序最后仍输出 `tutorial:ReachedTarget` 和 `Score=3`。现在我们的宿主已经完成了加载、观察、提交、等待回答和终局读取。

## 6. 完成后的阅读

这份练习固定使用一个参与者、一个投影、一个无参数动作及两次实体单选。它让我们看清交互过程；接入其他规则时，按相应操作指南处理具体要求。

- 给已有宿主接入带参数的动作：读[提交玩家选定的动作参数](../areas/core/how-to/submit-parameters.md)。
- 查状态、参数组合、回答类型和错误语义：读[宿主交互 API 参考](../areas/core/reference/session-api.md)。
- 理解规则、会话和玩家观察的关系：读[引擎运行模型](../areas/core/reference/developer-guide.md#1-先理解引擎在执行什么)。
- 想编写自己的规则：做[第一次写规则](first-rule.md)。

本教程使用进程内 C# SDK。跨进程游戏环境的接口由 net 提供，另见[公开协议](../areas/net/reference/protocol.md)。可运行的[教学宿主](../examples/core-first-host/Program.cs)保留在仓库中，方便对照。
