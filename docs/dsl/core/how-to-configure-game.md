# 用规则库配置计分目标和可选增量

文档类型：操作指南（How-to guide）。

使用已有 `ScoreRace` 规则库时，你可以在配置文件中修改目标分数和每次可选的增量。

开始前准备 .NET 9 SDK、本仓库和[规则库文件](../../examples/core-choice-counter/rules/score-race.spire)。如果你还没有写过底层规则，先做[规则教程](first-rule.md)。

## 1. 配置入口文件

编辑 [game.spire](../../examples/core-choice-counter/game.spire)：

```text
using namespace tutorial;
dsl_version = "core/0.2"
imports = ["rules/score-race.spire"]

use Game {
    template = counters:ScoreRace
    args = {
        target = 5
        increments = [2, 4]
    }
}
```

`imports` 路径相对于配置文件。复制入口文件时一并复制 `rules/`，或修改导入路径指向实际库文件。`target` 同时用于合法动作和终局条件，不需要修改库里的多个规则块。

参数范围由模板声明决定。本库的目标允许1～10000，增量为1～100的整数列表，最多16项。库和实例化规则的能力约束查 [core/0.2 规则库指南](0.2/authoring-guide.md)。

## 2. 加载并核对配置结果

在仓库根目录运行：

```powershell
dotnet run --project docs/examples/core-choice-counter/CoreChoiceCounter.csproj -- docs/examples/core-choice-counter/game.spire "1,0"
```

宿主按下标1、0选择参数，也就是先加四再加二。应看到 `Score` 从0→4→6，并以 `tutorial:Game.ReachedTarget` 结束。

首次运行需要构建宿主。以后只修改 `.spire` 时，可以加 `--no-build`；规则每次运行仍会重新加载。

若加载失败，先核对导入路径与参数类型/范围。若增量与预期不同，检查宿主选的是哪一个 `Bindings` 组合；参数化提交见[宿主操作指南](../../design/core-the-spire/how-to-submit-parameters.md)。

## 相关文档

- 规则库和宿主分别负责什么：[规则与宿主的关系](../../design/core-the-spire/rules-and-host.md)。
- 导入、模板、嵌套实例和名称绑定：[规则库指南](0.2/authoring-guide.md)。
- 想写库里的状态、动作、查询或选择：查[基础语言参考](0.1/language-reference.md)。
