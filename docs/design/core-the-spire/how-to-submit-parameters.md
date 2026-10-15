# 在宿主中提交玩家选定的动作参数

文档类型：操作指南（How-to guide）。

本指南说明如何让界面或策略选择一个带参数的动作，并提交选定的参数组合。

开始前，宿主应已能加载规则、创建参与者并执行 `Start()`。下面沿用变量 `session` 和 `player`。如果你是首次接入，先做[宿主教程](first-host.md)。

## 1. 向玩家提供当前可提交的组合

在 `session.Status == SessionStatus.Running` 时读取：

```csharp
var actions = session.LegalActions(player);
```

界面可以按动作展示 `Definition.QualifiedId`，再展示该动作的 `Bindings`。策略也可以选择某个组合。保留动作和组合的对应关系，提交时使用同一规则集中的定义。

若当前是 `AwaitingDecision`，先完成当前选择；回答字段查[决策接口参考](session-api-reference.md#决策请求与回答)。合法动作为空时，检查是否应由其他参与者操作，以及游戏规则是否安排了推进；不要自行补出终局结果。

## 2. 将选中的组合变成参数列表

取得玩家选定的动作 ID 和组合下标后：

```csharp
var action = actions.Single(a => a.Definition.QualifiedId == selectedActionId);
var arguments = action.Bindings[selectedBindingIndex].ToArray();
```

`selectedActionId` 和 `selectedBindingIndex` 来自界面或策略。它们需要指向刚才提供的候选，而不是来自别的会话的缓存。`Bindings` 按动作参数的声明顺序排列，已经排除了局部槽位。

对于无枚举域的自由输入参数，对应位置可能是 `Value.None`。根据 `action.Definition.Parameters` 的声明收集输入并填入相应位置：整数使用 `Value.Of(1L)`，实体引用使用 `Value.OfRef(instanceId)`。不要把 `Value.None` 当作已经填写的参数。

提交前可检查是否仍有占位值：

```csharp
if (arguments.Any(value => value.Kind == ValueKind.None))
    throw new InvalidOperationException("动作仍有尚未填写的参数。");
```

参数类型、候选与字段范围查[宿主交互 API 参考](session-api-reference.md#合法动作与提交)。

## 3. 提交并刷新界面

选定动作并填好参数后，提交同一规则集中的动作定义：

```csharp
session.Submit(action.Definition, player, arguments);
```

命令返回后，根据最新 `Status` 处理下一步，并重新调用 `Observe` 更新玩家界面。操作可能完成，也可能停在新的决策、终局或故障。

如果抛出 `EngineFaultException`，检查会话状态：输入在执行前被拒绝时，重新读取候选并让玩家重选；会话为 `Faulted` 时停止操作，保存故障与复现信息。完整语义查[错误参考](session-api-reference.md#错误与停止)。

## 核对集成结果

用 [game.spire](../../examples/core-choice-counter/game.spire) 检查一次实际提交。它定义 `tutorial:Game.Add`，提供加一、加二两个参数组合。在已开局的会话中选择这个动作的 `Bindings[1]`，应得到 `Score=2` / `Running`；重新读取候选后选择 `Bindings[0]`，应得到 `Score=3` / `Terminal`。

现有配置宿主提供这条运行路径，可作为对照：

```powershell
dotnet run --project docs/examples/core-choice-counter/CoreChoiceCounter.csproj -- docs/examples/core-choice-counter/game.spire "1,0"
```

候选下标的含义由当前规则的枚举顺序决定。其他规则应展示实际候选值，让玩家或策略选择。
