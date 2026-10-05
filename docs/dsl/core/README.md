# core 规则作者入门：导入规则库，填写配置

更新：2026-10-04。普通作者写 `.spire` 配置；规则库作者也写 `.spire`。`core/0.2` 新增文件导入和参数化规则模板，保留 `core/0.1` 的底层声明与过程。两者编译到同一 IR、链接器和 Session，不增加执行后端。

## 1. 先运行，再修改配置

需要 .NET 9 SDK。仓库根运行：

```powershell
dotnet run --project docs/examples/core-choice-counter/CoreChoiceCounter.csproj
```

默认读取带中文注释的 [game.spire](../../examples/core-choice-counter/game.spire)。有效配置如下：

```text
using namespace tutorial;
dsl_version = "core/0.2"
imports = ["rules/score-race.spire"]

# 使用计分竞赛规则库。Game 是这次实例的名字。
use Game {
    template = counters:ScoreRace
    args = {
        target = 3             # 达到多少分结束
        increments = [1, 2]    # 每次可以选择增加多少分
    }
}
```

宿主默认按下标 `1,0` 选择动作参数，也就是先 +2 再 +1：Score 从 0 → 2 → 3，最后进入 Terminal。合法操作由规则库枚举，作者不用定义选项实体或增加一次动作内选择。

把 `target` 改为 5、`increments` 改为 `[2, 4]`，再执行：

```powershell
dotnet run --no-build --project docs/examples/core-choice-counter/CoreChoiceCounter.csproj -- docs/examples/core-choice-counter/game.spire "1,0"
```

Score 将从 0 → 4 → 6 后结束。初始化、动作合法性和终局都使用同一目标参数，作者不用在多个规则块同步修改条件。首次运行需要构建宿主；后续配置改动无需重新编译。复制示例时一并复制 `rules/`，导入路径相对于配置文件。

## 2. 谁写什么

| 使用者 | 维护内容 | 是否需要 C# |
| --- | --- | --- |
| 使用已有规则的作者 | `imports`、`use` 和具名参数 | 不需要 |
| 规则库作者 | DSL `procedure`、`template` 及底层规则声明 | 不需要；可表达性以已交付原语为准 |
| 引擎/宿主开发者 | 新通用原语、外部计算后端、文件加载与交互 | 需要 SDK 或相应扩展接口 |

实际规则库是 [rules/score-race.spire](../../examples/core-choice-counter/rules/score-race.spire)，已加中文注释。`procedure` 封装执行时调用的函数，`template` 封装编译时生成的一组声明；`use` 配置一次实例。这里的库包括计分状态、初始化、合法动作、加分、终局和公开观察。它不在 C# 中隐藏游戏规则。

`use Left`、`use Right` 可以实例化同一个模板。各自产物拥有 `Left.Counter`、`Right.Counter` 等前缀，引用自动绑定到所属实例。独立计分不等于自动组成多玩家游戏：轮流行动、共享状态和组合胜负仍由规则库明确描述。

## 3. 想定义新规则时再往下一层读

先读 [core/0.2 规则库指南](0.2/authoring-guide.md)，理解导入、参数、实例作用域和组合；写库里的实体、动作、事件、查询、选择等时查 [core/0.1 基础原语参考](0.1/language-reference.md)。新增作者层不要求重写已可运行的底层文件。

带中文注释的 [counter.spire](../../examples/core-choice-counter/counter.spire) 保留为进阶例子，展示动作内 `decide` 暂停/续接和事件反应。显式选择它运行：

```powershell
dotnet run --project docs/examples/core-choice-counter/CoreChoiceCounter.csproj -- docs/examples/core-choice-counter/counter.spire "1,0"
```

这个进阶例子会出现 AwaitingDecision，最后 Score=3、Attempts=2、Applied=2。普通入口已经不要求作者理解这些执行细节。开发宿主时再读 [引擎开发者指南](../../design/core-the-spire/developer-guide.md)。

## 4. 游戏库归属与交付范围

core 提供通用语言及原语。卡牌、伤害、回合、原版随机布局等由 mod 的 DSL 规则库描述；常见效果应复用库，不让每个内容作者拼低层指令。base 现有 [STS 0.0.1 前端](../0.0.1/dsl-usage.md) 仍可运行；本次未自动迁移完整原版内容。

确实无法用现有原语表达的新能力才通过 `IEngineDslExtension` 或新增通用原语接入同一 IR。模板当前只包含核心声明和嵌套 `use`，不能直接包入游戏自定义声明。完整原版规则库、编辑器插件与生产级 Lua 后端仍有各自验收范围。交付依据见 [规则库作者层报告](../../review/archived/dsl-rule-library-authoring-2026-10-04.md)。
