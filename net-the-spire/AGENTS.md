# net-the-spire 开发约定

遵循根 [AGENTS.md](../AGENTS.md)、[开发流程](../CONTRIBUTING.md)和[术语规范](../docs/terminology.md)。本文件补充公开接口边界。

- 负责 Host、协议、schema、编码和客户端及相应文档；依据 [net 规格](../docs/areas/net/spec/overview.md)、[协议](../docs/areas/net/reference/protocol.md)和[实现约束](../docs/specs/environment-constraints.md)。
- 加载相同规则包，为每局建立独立 Session 并只启动一次；公开观察和完整合法候选来自 Engine 的稳定状态。不在 net 重算玩法、补随机结果或为每项内容重新注册专用处理。
- 保持通用编码、公开句柄、请求去重及版本核对。协议变化同步更新 C#、Python 和 schema，说明兼容性及实际请求／响应结果。
- 隐藏状态、内部随机状态和 debug 字段不进入公开观察；不静默丢字段、截断候选或让 AI 从历史补齐当前应发布的信息。直接序列化内部 State 不能替代公开投影。
- 跨模块缺口通过工单协调文件和接口边界。核心语义变更按当前核心约定处理，参考实现的玩法和 AI 训练不属于本模块任务的默认修改范围。
- 按影响验证编码、幂等、非法动作、会话隔离和真实 stdio 路径。局部协议检查、同一构建的公开全程运行与独立参考比较分别报告。
