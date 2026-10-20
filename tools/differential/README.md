# 纯 DSL 迁移期间的独立参考比较入口

原独立适配器依赖已退出生产的 C# base，尚未迁移到 DSL。当前入口保留原 11 份 suite 定义及每个 `expected_case_ids`，逐项输出 `unsupported`、`executed: 0`、`independent_verified: false`，并返回退出码 3。

```powershell
dotnet run --project tools/differential -- test --suite regression --output .artifacts/differential/regression
```

这条命令应返回非零，不构造独立比较成功。原源与适配器见[迁移对照](../base-migration-baseline/README.md)，逐组去向见[CI 迁移数据](../ci/fixtures/legacy-dsl-checks.json)。

[迁移检查](../ci/README.md) 验证该失败语义、原 suite 标识以及生产依赖隔离；它不是原完整差异测试验收条件。恢复时必须由同一 DSL 包与真正独立的参考分别执行，保留全部原必测用例、比较维度和失败条件，不按现有可玩子集缩小目标。[参考版本](../../reference/README.md)与原 ENV 合同保持有效；参考玩法未修改。
