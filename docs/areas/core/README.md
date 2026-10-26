# core 开发入口

core 维护通用 DSL、链接、执行与 Session API。先按要完成的任务选入口；原版玩法属于 base，不能为某张牌增加内核分支。

| 任务 | 入口 |
| --- | --- |
| 第一次编写规则或宿主 | [规则教程](../../start/first-rule.md)、[宿主教程](../../start/first-host.md) |
| 导入规则库和配置实例 | [DSL 作者入口](../../reference/dsl/README.md)、[配置指南](how-to/configure-game.md) |
| 提交带参数的动作与回答选择 | [参数提交指南](how-to/submit-parameters.md)、[Session API](reference/session-api.md) |
| 查类型、语法、模板与包格式 | [DSL 参考](../../reference/dsl/README.md)、[开发者指南](reference/developer-guide.md) |
| 理解规则、宿主和状态归属 | [规则与宿主](../../design/core-the-spire/rules-and-host.md)、[运行态模型](../../design/core-the-spire/runtime-state-model.md) |
| 核对核心要求和架构决定 | [核心接口要求](../../contracts/core-engine-baseline.md)、[游戏建模规格](../../design/core-the-spire/spec.md)、[核心决定](../../design/core-the-spire/core-architecture-decisions.md) |

构建和定向检查见 [core 模块说明](../../../core-the-spire/README.md)与 [CI 说明](../../../tools/ci/README.md)。修改公共语义前，遵循模块 [AGENTS.md](../../../core-the-spire/AGENTS.md)，提供最小通用程序和受影响消费者。
