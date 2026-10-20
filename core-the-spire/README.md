# core-the-spire

core-the-spire 是一个用 C#/.NET 9 实现的通用规则引擎。你在 `.spire` 文件中描述游戏状态、玩家操作和结束条件，再由宿主程序加载并运行。一份规则可以运行多局独立会话，宿主通过参与者视图读取状态，也可以接入界面或 AI。

## 从你要做的事开始

| 文档类型 | 你想做什么 | 从这里开始 |
| --- | --- | --- |
| 教程 | 第一次写一份能运行的规则 | [第一次写规则](../docs/dsl/core/first-rule.md) |
| 教程 | 第一次写程序驱动规则 | [第一次写宿主](../docs/design/core-the-spire/first-host.md) |
| 操作指南 | 使用已有规则库，修改计分参数 | [配置目标和增量](../docs/dsl/core/how-to-configure-game.md) |
| 操作指南 | 在已有宿主中接入带参数的动作 | [提交动作参数](../docs/design/core-the-spire/how-to-submit-parameters.md) |
| 参考 | 查询 DSL 声明、表达式与模板 | [core/0.1 语言参考](../docs/dsl/core/0.1/language-reference.md)、[core/0.2 规则库指南](../docs/dsl/core/0.2/authoring-guide.md) |
| 参考 | 查询同包文本绑定、扩展加载与具名调用 | [core/0.3 作者参考](../docs/dsl/core/0.3/authoring-reference.md) |
| 参考 | 查询交互 API 的输入、结果与错误 | [宿主交互 API](../docs/design/core-the-spire/session-api-reference.md) |
| 解释 | 理解规则、会话、动作和玩家观察的关系 | [规则与宿主](../docs/design/core-the-spire/rules-and-host.md) |
| 解释 | 理解内核的设计取舍 | [架构决定](../docs/design/core-the-spire/core-architecture-decisions.md) |

## 先跑一个小游戏

安装根 [global.json](../global.json) 指定的 .NET SDK `9.0.306`，在仓库根目录运行。SDK 配置禁用版本滚动；可先用 `dotnet --version` 确认实际选择的版本。

```powershell
dotnet run --project docs/examples/core-first-host/CoreFirstHost.csproj
```

你会看到分数从 0 → 1 → 2 → 3，最后输出 `tutorial:ReachedTarget`。这是[第一份规则](../docs/examples/core-choice-counter/first-counter.spire)与[教学宿主](../docs/examples/core-first-host/Program.cs)协作的结果：规则负责加分和结束条件，宿主负责加载、操作和显示。两条教程会分别带你写出这两部分。

已有规则库的配置示例仍可以这样运行：

```powershell
dotnet run --project docs/examples/core-choice-counter/CoreChoiceCounter.csproj
```

入口是 [game.spire](../docs/examples/core-choice-counter/game.spire)，其导入的[规则库](../docs/examples/core-choice-counter/rules/score-race.spire)也使用 DSL 编写。第一次构建以后，改 `.spire` 文件可以用 `--no-build` 运行，规则仍会重新加载。

## 范围与维护

活动内核位于 `Core.TheSpire/Engine/`，提供通用声明、状态、动作、调度、随机和参与者观察。卡牌、伤害、回合等游戏规则由内容包提供；原版内容作者使用[base开发手册](../docs/development/base-spire/README.md)及[core/0.3作者参考](../docs/dsl/core/0.3/authoring-reference.md)，不使用旧base/0.0.2前端。旧 STS 后端已从当前树删除，仅保留Git历史，见 [archive/legacy-sts](https://github.com/QingXiang22/auto-the-spire/blob/0aca7d1a26f3ad42d5b8657bc794cd0b12f85ba8/archive/legacy-sts/README.md)，活动工程不引用归档。

当前源码的包版本为 `0.4.0`，运行语义身份为 `engine/ir@0.4`，文本编译器支持 `core/0.1`、`core/0.2` 与 `core/0.3`。版本依据分别为 [项目配置](Core.TheSpire/Core.TheSpire.csproj)、[RuleSetBuilder.EngineVersion](Core.TheSpire/Engine/RuleSet.cs) 和 [EngineDslCompiler](Core.TheSpire/Engine/Compilation/EngineDslCompiler.cs)。新同包绑定与具名调用见 [core/0.3 参考](../docs/dsl/core/0.3/authoring-reference.md)。

`engine/ir@0.3` 是 2026-10-05 的历史冻结基线；[冻结契约](../docs/contracts/core-engine-baseline.md)、[Git 冻结交接](../docs/contracts/core-git-checkpoint.md)及[冻结报告](../docs/review/archived/core-contract-freeze-2026-10-05.md)保留当时的范围与证据。活动升级的验收及冻结状态查 [CORE-AUTHOR-04](../docs/review/tofix/README.md#core-author-04)，不从源码版本号推断已完成发布或验收。其他设计问题按需查[模块 spec](../docs/design/core-the-spire/spec.md)和[架构决定](../docs/design/core-the-spire/core-architecture-decisions.md)。

内核维护者只检查 core 时，从仓库根目录执行：

```powershell
dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests
```

需要记录本地/CI 一致的日志和退出码时，使用 [CI 检查入口](../tools/ci/README.md)。跨模块完整包集成再串行构建 `dotnet build AutoTheSpire.sln` 并运行受影响消费者检查；core 检查通过不表示原版内容或整局流程已验收。
