# 项目架构总览

文档类型：解释（Explanation）。本页面向需要判断修改归属的贡献者，说明规则、执行、公开接口与工具如何连接。产品要求见[项目规格](../specs/project.md)，准确核心行为见[核心接口要求](../specs/core-engine-baseline.md)，当前支持范围以包 manifest 和对应提交的 CI 为准。

## 从规则到公开动作

```mermaid
flowchart LR
    Base[base DSL 与静态 JSON] --> Core[core 编译、链接与 Session 执行]
    Core --> Net[net 公开观察与合法动作]
    Net --> AI[AI 客户端、编码与策略]
    AI -->|提交公开候选| Net
    Net -->|推进同一 Session| Core
    Tools[tools 构建、检查与独立参考比较] -.验证.-> Base
    Tools -.验证.-> Core
    Tools -.验证.-> Net
```

base 声明游戏规则；core 执行声明；net 为外部调用者发布当前公开状态并把候选提交回同一会话；AI 消费这份公开协议。独立参考依赖留在 tools，生产规则不借用参考答案。

| 要改变什么 | 所属模块与阅读入口 |
| --- | --- |
| 卡牌、伤害、Power、遗物、敌人、房间和 Run 的行为 | `mod-the-spire/base-spire/`；[base 开发手册](../areas/base/README.md) |
| 通用类型、DSL 编译、IR、执行、选择续接、随机与观察能力 | `core-the-spire/`；[core 开发入口](../areas/core/README.md) |
| Host、公开观察、合法动作、协议与客户端 | `net-the-spire/`；[net 开发入口](../areas/net/README.md) |
| 公开输入编码、概率输出、采样与评价 | `ai-the-spire/`；[AI 开发入口](../areas/ai/README.md) |
| 构建、CI、开发辅助和独立参考比较 | `tools/`；[工具清单](../../tools/README.md) |

## 维护边界

通用内核不按卡牌、遗物或房间 ID 执行玩法分支。规则包构建后不可变，每局状态由 Session 独立管理；游戏实例关系、生命周期、事件顺序和随机流由规则与共享操作明确组织。细节分别查[base 架构](base-architecture.md)、[控制流程](base-control-flow.md)及[核心决定](decisions/core.md)。

公开投影和候选是外部接口的依据。内部 State、RNG 和诊断信息不自动成为 AI 输入；局部行为检查、独立比较与正常整局分别验收，要求见[完整环境规格](../specs/environment.md)。

## 文档组织决定

文档以贡献者任务提供入口：`start/` 组织入门，`areas/` 汇集模块操作、参考和领域要求，`reference/` 提供共享查询，`understand/` 解释架构，`specs/` 保存跨模块要求，`meta/` 保存术语与稳定任务入口。这样既可从任务直达内容，也可在模块内找到相关操作与要求。tools 的已有说明与 `docs/examples/` 保持原地，避免复制命令或移动可执行示例。

内容页按教程、操作指南、参考或解释选择主要读者任务；规范表示权威职责，不能当作第五种类型。README 保留必要的局部索引，全局入口允许直达常用页面。旧目录不保留别名或历史映射，路径变更须同时更新消费者、资源与检查配置；历史从 Git／PR 查询。此决定遵循 [CONTRIBUTING](../../CONTRIBUTING.md#哪些内容进入正式文档)，不另建任务状态副本。
