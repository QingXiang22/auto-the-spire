# Core architecture audit probes

本工具用于 2026-10-01 的架构审阅；不修改游戏源码，不加入主 solution。

从仓库根目录运行：

```powershell
$env:DOTNET_TieredCompilation = '0'
dotnet run --project tools/core-architecture-audit -c Release
```

环境变量仅用于减少本次短测量的分层 JIT 波动。若在自己的长期 PowerShell 会话运行，结束后恢复原值。

工具输出实际内容加载数量、DSL 版本/目标契约探针、C# handler 的执行顺序，以及公共 API 的三个分配/耗时样本。它是观察工具，不是通过/失败测试 runner；进程成功不表示规则正确。

性能场景为固定种子、Ironclad 十张起始牌、单个 DampCultist。观察和合法动作在十张手牌的静止状态测量；结束回合在独立会话连续执行 1000 次，不打牌，第一轮后每回合通常有五张手牌。玩家生命放大以避免样本提前终止，其他规则沿用当前内容包。没有网络、序列化、策略推理、状态分支或复杂 Power 组合。分配数是线程累计分配量，不是保留堆大小。

`results-2026-10-01.jsonl` 保存本次 Release、关闭分层 JIT 的原始结果。报告见 [架构与 DSL 审阅](../../../../docs/review/tofix/core-base-architecture-dsl-2026-10-01.md)。
