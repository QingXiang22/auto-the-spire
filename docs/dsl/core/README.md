# core DSL 文档入口

core DSL 在 `.spire` 文件中描述状态和规则。宿主加载文件并驱动会话。第一次使用从教程开始；实际任务、语法查询和概念理解分别看下表。

| 阅读需要 | 文档类型 | 入口 |
| --- | --- | --- |
| 第一次写规则 | 教程 | [做一个三分结束的小游戏](../../start/first-rule.md) |
| 第一次从程序运行规则 | 教程 | [写一个C#宿主](../../start/first-host.md) |
| 用已有规则库修改目标和增量 | 操作指南 | [配置计分小游戏](how-to-configure-game.md) |
| 给自己的宿主接上参数化动作 | 操作指南 | [提交动作参数](../../design/core-the-spire/how-to-submit-parameters.md) |
| 查实体、程序、动作、查询、选择的写法 | 参考 | [core/0.3 基础语言参考](0.3/language-reference.md) |
| 查同包宿主绑定、扩展加载与具名调用 | 参考 | [core/0.3 作者参考](0.3/authoring-reference.md) |
| 查导入、模板参数、实例名称和组合约束 | 参考 | [core/0.3 规则库指南](0.3/authoring-guide.md) |
| 查宿主API字段、参数和错误 | 参考 | [宿主交互API参考](../../design/core-the-spire/session-api-reference.md) |
| 理解规则作者、规则库作者和宿主的关系 | 解释 | [规则、会话与宿主](../../design/core-the-spire/rules-and-host.md) |

## 1. 先运行，再修改配置

已有配置示例在仓库根目录运行：

```powershell
dotnet run --project docs/examples/core-choice-counter/CoreChoiceCounter.csproj
```

默认加载 [game.spire](../../examples/core-choice-counter/game.spire)，分数0→2→3后结束。具体配置修改步骤见[计分配置操作指南](how-to-configure-game.md)。第一次写新规则请从上面的教程进入，不需要先通读语言参考。

## 示例与相关入口

- 最小规则：[first-counter.spire](../../examples/core-choice-counter/first-counter.spire)。
- 带动作内选择和事件反应的规则：[counter.spire](../../examples/core-choice-counter/counter.spire)，运行路径在[宿主教程](../../start/first-host.md)。
- 可导入的DSL规则库：[score-race.spire](../../examples/core-choice-counter/rules/score-race.spire)。
- 原版卡牌内容使用相同core/0.3 DSL，具体领域操作查[base开发手册](../../areas/base/README.md)。base/0.0.1和base/0.0.2为退出生产的旧前端，不用于当前包。

四种文档的写作与验证要求见[开发与文档约定](../../../CONTRIBUTING.md)。
