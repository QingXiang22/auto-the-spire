# 开发和修正纯 DSL base 规则

文档类型：操作指南（How-to）。本页帮助开发者按领域定位规则、修改纯 DSL 包并验证行为。任务、认领与剩余范围只维护在[主清单](../../review/tofix/README.md#当前交付)；先核对[规格](engine-dsl-migration/spec.md)、[目录与接口](engine-dsl-migration/plan.md)及[实施任务](engine-dsl-migration/tasks.md)。

## 找到定义与共享机制

从 [package.json](../../../mod-the-spire/base-spire/package.json) 找包入口、支持 scope 和源目录，再从 [base.spire](../../../mod-the-spire/base-spire/base.spire) 进入领域索引。`cards/`、`powers/`、`relics/` 等目录拥有本领域内容；索引显式决定组合顺序，不靠扫描目录自动注册。

具体内容按来源组一项一个 `.spire` 文件，例如 [TheAbacus](../../../mod-the-spire/base-spire/relics/shared/the-abacus.spire)。该文件只允许模板 `use`，参数是既有 DSL 支持的有界字面量；过程、动作、订阅和类型放到共享 `rules/` 或 `schema.spire`。模板仍由 Engine 原生编译器展开为同一 IR，无 C# 领域效果分支。

未迁移内容先查对应 `catalog.json` 的来源与 `definition: null`。目录中的源清单不是可执行支持清单。源池、资格和原检查意图必须保留，不能因为当前能力不足缩池或建立空效果定义。

## 修改并验证

在仓库根目录使用固定 SDK；正常构建和以下开发检查不依赖本地 `sts2/`。

1. 在主清单认领一个完整机制包，记录规则依据、依赖和预期结果。DSL 表达不足时先提供通用最小复现，再修改 Engine；游戏规则继续写 DSL。
2. 修改共享规则或内容配置，并通过领域 `index.spire` 显式包含。新增可执行源内容时更新静态 `definition` 路径与公开元数据。包内仅允许 `.spire` 与静态 `.json`，文档和检查留在包外。
3. 执行实际加载和受影响行为检查：

```powershell
dotnet run --project tools/base-spire-dev -- validate
dotnet run --project tools/base-spire-dev -- check
```

4. 跨模块集成时按 [CI 指南](../../../tools/ci/README.md) 检查 Engine、base、net 与迁移边界。更新规格／任务追溯与实际证据，注明未迁移能力和未执行独立比较。

当前行为检查针对遗物抽牌开发 scope。恢复其他机制时按承诺的状态变化增加有判别力的检查，不把所有新内容都算作 TheAbacus 的现有见证。正常 Run、原版随机布局与自动出牌仍不支持；未支持请求明确失败。

## 查旧实现

旧 C#、Slice 检查和作者工具位于[迁移对照](../../../tools/base-migration-baseline/README.md)，逐组去向见[检查清单](../../review/records/2026-10-18/base-dsl-check-disposition.json)。它们是规则与回归意图的依据，不能重新加入生产引用。

[旧卡牌教程](first-base-card.md)与[旧房间教程](first-base-room.md)尚未迁移，只供理解原验收意图，不能作为当前可执行入门路径。通用 DSL 新手可先走[第一份规则教程](../../dsl/core/first-rule.md)，加载组合的准确接口见[包格式参考](../../dsl/core/0.3/package-reference.md)。
