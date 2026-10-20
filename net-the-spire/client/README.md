# Python 公开客户端

当前公开调用从 [公开接口指南](../../docs/design/net-the-spire/how-to-run-public-game.md)开始，准确方法与快照字段见 [客户端接口参考](../../docs/design/net-the-spire/public-client-reference.md)。客户端只消费 Host 发布的观察与合法候选。

## 当前检查

在仓库根目录运行 `python tools/ci/check.py net`。该组构建通用 Host，验证 wire／文本规则与纯 DSL 的真实 stdio 路径。当前正常 Run 尚未恢复；旧药水／商店／地图重放脚本所需场景不计入本组。

[net_client.py](net_client.py)提供 describe、catalog、start、observe、step 和 close；[public_snapshot.py](public_snapshot.py)提供公开快照访问工具。调用者先读取身份与当前候选，再提交 candidate_id；重放不得沿用其他构建或其他状态的候选。

## 整局驱动

[run_game.py](run_game.py)及其 [参数参考](run_game.md)保留原整局策略和输出约定。它仍依赖待恢复的 formal-dev／dev-map 场景。后续迁移以当前包和可运行的自动检查为准，不依赖任何私人 DLL 或本地旧轨迹。

新的轨迹由匹配构建重新生成，临时输出写入调用者指定的 `.artifacts/<任务>/`。运行结果在 PR／CI 查看，过期材料按 [开发约定](../../CONTRIBUTING.md)清理。
