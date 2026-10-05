# core-the-spire

本次 Git 提交与共享工作区的范围、独立检出后的验证、下一阶段的原版/mod 对拍顺序见 [Git 冻结交接](../docs/contracts/core-git-checkpoint.md)。

2026-10-05：当前通用 core 收口进入冻结基线，运行语义 engine/ir@0.3，保留 core/0.1 与 core/0.2。范围、启动/枚举迁移及维护规则见 [冻结契约](../docs/contracts/core-engine-baseline.md)；已验收关闭证据见 [报告](../docs/review/archived/core-contract-freeze-2026-10-05.md)。后续重心转向游戏 DSL 包、公开接口与环境工具。

活动内核是 `Core.TheSpire/Engine`：通用声明、IR、链接检查、状态、唯一调度器、随机和参与者观察。旧 STS 后端已移至 [archive/legacy-sts](../archive/legacy-sts/README.md)，活动工程不引用归档。

规则作者直接编辑 core/0.2 `.spire` 配置，导入 DSL 规则库并填写参数，先读[作者入门](../docs/dsl/core/README.md)及[语言参考](../docs/dsl/core/0.1/language-reference.md)。Host/扩展开发者读[开发者指南](../docs/design/core-the-spire/developer-guide.md)、[架构决定](../docs/design/core-the-spire/core-architecture-decisions.md)和[模块 spec](../docs/design/core-the-spire/spec.md)。文本和 C# SDK 共用 IR/链接器/Session。

运行配置式小例子：

```powershell
dotnet run --project docs/examples/core-choice-counter/CoreChoiceCounter.csproj
```

作者入口为 [game.spire](../docs/examples/core-choice-counter/game.spire)，规则库也是 [DSL 文件](../docs/examples/core-choice-counter/rules/score-race.spire)，都有中文注释。宿主只加载与交互，模板仍编译到同一 IR。规则库约束见 [core/0.2 指南](../docs/dsl/core/0.2/authoring-guide.md)；原 counter.spire 保留为进阶例子。修改规则文件后可使用 --no-build 运行，无需重新编译宿主。

内核已实现作者对象及子集合冻结、Session 按职责拆分、保留候选顺序的取样、可注入版本化随机 profile 及可关闭的有界调试模式。首版交付证据见[报告](../docs/review/archived/core-engine-followup-2026-10-04.md)。它不代表完整原版环境或编辑器插件已完成。

```powershell
dotnet build AutoTheSpire.sln
dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests
```
