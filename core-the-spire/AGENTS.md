# core 开发约定

遵循根 [AGENTS.md](../AGENTS.md)、[开发流程](../CONTRIBUTING.md)和[术语规范](../docs/terminology.md)，任务见[统一入口](../docs/roadmap.md)。本文件只补充模块边界。

- core 提供通用类型化状态、编译、选择续接、随机和唯一执行器；游戏实体、回合、伤害和内容语义属于 base DSL。
- 当前接口、版本及已支持边界以 [活动契约](../docs/contracts/core-engine-baseline.md)和实现为准。通用缺口先给最小语法／SDK 复现、预期与实际；语义变化同步迁移真实消费者。
- DSL 与 C# 构建器生成相同 IR，调用同一个链接器与 Session。不得新增替代调度器、玩法回退或强行吞掉未支持项。
- RuleSet 构建后不可变，Session 状态独立。随机取样保留包提供的候选顺序，原版特定策略由游戏／宿主配置。
- grammar 变化后运行 `tools/dsl-generate/generate-core.ps1`；正常构建使用纳管的生成代码。
- 按影响运行 `python tools/ci/check.py core`，影响文本包或公开契约时补相应 base／net 组。PR 记录结果；不生成额外交接文档。
