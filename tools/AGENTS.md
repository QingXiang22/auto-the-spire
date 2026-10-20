# tools 开发约定

遵循根 [AGENTS.md](../AGENTS.md)、[开发流程](../CONTRIBUTING.md)和[术语规范](../docs/terminology.md)。任务从[统一入口](../docs/roadmap.md)选择执行工单。

## 工具管理

- Git 只维护[工程工具清单](README.md)登记的可复用工具。一次性修复、提取、实验脚本和生成报告放本地临时目录；新增维护目录时同步登记并调整 `.gitignore`。
- `.artifacts/`、`artifacts/` 和 `.audit/` 是本地输出目录，不提交或作为正式文档链接。确有自动检查消费者的固定来源数据可以纳入 Git。

## 差异测试边界

- 遵守[环境范围](../docs/design/environment-completion-and-differential-v1.md)和[实现约束](../docs/design/environment-implementation-constraints.md)。比较工具加载同一生产 base 包，参考实现独立执行并默认只读；参考依赖仅在工具层。
- 不能把参考答案或工具补偿加入生产玩法，也不能恢复旧 C# base 的生产依赖。
- 严格测试集中，缺用例、零执行、缺参考、未支持、异常、超时、结果不同或缺比较维度均返回非零。旧 compare 返回零不能代替当前验收。
- 保留必测用例总数、来源条目范围及排除依据；不能删除 seed、换成较弱用例、额外抽取随机数或注入清场制造通过。
- 正常 Run 和人工构造的测试场景分别报告。共用构建输出按工单协调；来源范围、阶段完成和通用核心变更由相关负责人评审。
