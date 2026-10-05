# auto-the-spire 协作约定

本文件适用于仓库根目录及其子目录。子目录若有自己的 `AGENTS.md`，在其范围内同时遵守更具体的约定。本文是多 agent 协作入口；游戏规则和 DSL 细节以链接的规范为准。

## 项目目标与边界

- 项目为 AI 构建可复现、可观测、可控且可扩展的《杀戮尖塔 2》训练环境。整体分为四个部分：
- `core-the-spire` 是独立的 C#/.NET 9 规则内核。通用状态、动作、结算、随机数、内容注册和 hook 放在这里。
  - **当前只使用通用 Engine 后端。** 2026-10-04 用户决定将旧 STS 专用后端及消费者移到 `archive/legacy-sts/`；活动工程不再引用或编译归档，旧测试可单独运行。详见 [core-the-spire/AGENTS.md](./core-the-spire/AGENTS.md) 与[归档说明](./archive/legacy-sts/README.md)。归档不等于新环境已实现全部旧内容。
- `mod-the-spire/base-spire` 是使用同一套内容接口实现原版规则的内容包；其他 mod 也通过该接口组合。`net-the-spire` 隔离外部动作与观察，`ai-the-spire` 消费该接口。
- `net-the-spire` 对`core-the-spire`按照和原版游戏相同的方式提供对外的接口以供外部进行游戏运行时相关信息的提供与相关的交互。
- `ai-the-spire` 是基于`net-the-spire`提供的接口进行的机器学习项目。
- `sts2/` 是恢复得到的杀戮尖塔2源码浏览工程，base-spire 的原版规则以对应版本的真实游戏与源码为依据；core 不包含原版玩法。
- `tools/` 是一些临时用到的工具，临时性的工具放在该目录下。
- `docs/` 统一存放项目所有涉及到的文档内容。

## 规范与实现状态

- [docs/design/spec.md](./docs/design/spec.md) 是项目目标、范围与规则契约的主规范；[docs/dsl/0.0.1/dsl-usage.md](./docs/dsl/0.0.1/dsl-usage.md) 是新版卡牌 DSL 的作者约定。
- [docs/dsl/0.0.1/content-dsl.md](./docs/dsl/0.0.1/content-dsl.md) 和 [docs/dsl/0.0.1/combat-dsl-design.md](./docs/dsl/0.0.1/combat-dsl-design.md) 记录现有实现及旧语法示例。遇到卡牌写法冲突时，以 `dsl-usage.md` 为准，并修正文档中的歧义。
- 区分“设计目标”“解析器接受”“内容包能注册”“规则行为已验证”四种状态。不能仅凭源码存在或 solution 构建成功宣称功能已实现。
- 新卡牌使用 `using namespace modname;`、花括号块、完整的 `normal` / `upgraded` 版本、`hooks` 和具名效果。嵌套调用写成 `count = { pileCount = { pile = hand } }`；X 费写成 `cost = X`。敌人、Power、药水等尚有旧语法内容，迁移前保持其兼容性。
- 设计中的 `player` 内容域不等于已实现的 `player` DSL 声明。新增声明、原语或 hook 时，先明确语法、编译结果、运行时语义和文档状态。
- **2026-10-05 core 冻结：** engine/ir@0.3，core/0.1 基础语言与 core/0.2 规则库配置已交付；Engine 95/95，工作区 Slice 264/264、net 393/393。冻结提交只收录核心及必要归档/配套，不代表下游实现也已提交；独立检出的范围与验证见 [Git 冻结交接](./docs/contracts/core-git-checkpoint.md)。活动模块不得自行重开核心架构；兼容维护与语义升级按 [冻结契约](./docs/contracts/core-engine-baseline.md)处理。原 Slice 268 中 4 项兼容检查移入归档；归档旧规则 190 passed / 0 failed / 190 executed。这些仍不代表完整原版战斗或整局覆盖。

## 多 agent 共享工作区
1. 开始工作前查看 `git status --short` 和相关文件。已有未提交改动可能属于用户或其他 agent；不要重置、覆盖或顺手整理不在任务范围内的改动。
2. 分工时写清负责的目录或文件、接口边界及验收条件。尽量让并行任务编辑不同文件；需要同时改一个文件时，先由负责人协调修改顺序。
3. 修改公共契约（DSL 语法、类型化效果、内容 ID、hook、状态视图、随机数调用顺序）时，及时向相关 agent 说明新旧行为、受影响文件和迁移要求。不要只发送“已完成”。
4. 交接时报告：改动文件、关键决策、执行过的验证命令及结果、尚未验证的风险。发现规范与代码冲突时提供具体位置和可复现现象。报告提交到 `docs/review/tofix`，文件名为 `主题-YYYY-MM-DD.md`。报告中涉及的问题被解决了以后归档到`docs/review/archived`
5. 不重置、清理、提交或移动他人的工作；共享 Git 状态由任务负责人协调，提交遵循用户对当前任务的授权。避免并行构建写入同一项目的 `obj` / `bin`，以免文件锁造成假失败。


## 实现原则
`core-the-spire`和`mod-the-spire`里会明确说明需要遵守的规则。

## 验证与汇报

- 从仓库根目录构建：`dotnet build AutoTheSpire.sln`。
- 规则、DSL、内容包或注册路径变化后，运行：`dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` 与 `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests`。仅构建成功不代表 `.spire` 文件可以解析、编译和注册。旧规则检查另在 `archive/legacy-sts/core-the-spire/Core.TheSpire.Tests`，不作为活动门槛。
- 对 DSL 迁移至少核对内容包实际加载、声明与卡池数量、代表性基础/升级效果，以及 X 费、目标和钩子等受影响语义。能区分预期行为的检查优先于重复实现逻辑的检查。
- 文档改动可用链接和 `git diff --check` 核对。汇报中分别写明构建、规则检查、运行时内容加载和未执行项目；失败时给出首个错误及阻断范围，不把“测试数为零”写成通过。
- 当前实现尚非完整原版。任何覆盖率、原版一致性或性能声明都应附可核对的内容范围与验证证据。
