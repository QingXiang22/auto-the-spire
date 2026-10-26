# net 开发入口

net 加载同一规则包，发布公开观察和合法候选，并提供 Host、协议与客户端。支持范围和隐藏信息约束按当前实现及相应检查核对。

| 任务 | 入口 |
| --- | --- |
| 确认终端菜单能否用于当前构建 | [手动试玩的当前边界](how-to/play-base-game.md) |
| 启动公开 Host 或编写客户端 | [公开运行指南](how-to/run-public-game.md) |
| 查客户端调用和协议字段 | [客户端参考](reference/public-client.md)、[协议参考](reference/protocol.md) |
| 核对提供方行为、规范化与 schema | [net 规格](spec/overview.md)、[协议资产要求](spec/contract-artifacts.md)、[不变量](../../specs/net/0.2/invariants.md) |
| 理解 Host、公开状态和请求恢复的设计 | [net 架构](../../understand/net-design.md) |

运行入口见公开运行指南，验证范围见 [CI 说明](../../../tools/ci/README.md)。模块边界遵循 [AGENTS.md](../../../net-the-spire/AGENTS.md)；接口变化须同步 Python、C# 与共享协议资产。
