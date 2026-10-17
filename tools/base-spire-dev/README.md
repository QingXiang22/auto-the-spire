# 验证与查看纯 DSL base 包

本工具在仓库根目录加载 Engine 的纯文本包，不调用旧 C# 玩法。需要根 `global.json` 指定的 .NET SDK。省略 manifest 时加载 `mod-the-spire/base-spire/package.json`。

```powershell
dotnet run --project tools/base-spire-dev -- validate
dotnet run --project tools/base-spire-dev -- describe
dotnet run --project tools/base-spire-dev -- check --report .artifacts/base-dsl/behavior
```

`validate` 检查文件边界、引用、严格静态数据与公开元数据，并实际启动 DSL Session。`describe` 输出包摘要、规则身份、scope 和源／DSL 定义计数。`check` 执行当前 base 的代表性行为、边界与非法输入检查；它是包外的 C# 检查脚手架，不提供玩法实现。`--report` 写入实际通过场景及 TheAbacus 的行为见证，供 [DSL 遗物覆盖检查](../ci/check_relic_coverage.py) 消费。

可在命令后传入其他 manifest 路径，再使用 `--scope <id>`。本版 `check` 用于当前 base 开发范围；仅通用加载验证使用 `validate`。退出码 0 为当前命令完成，2 为格式／加载失败，3 为未支持。旧 `play`、`probe` 和直接传卡牌 `.spire` 的入口明确返回 3。

当前 `base-spire-dsl-relic-draw-dev-v1` 只实现遗物实例、抽牌／重洗和 TheAbacus 触发链。未迁移卡牌、Power 和 Run 不能通过旧命令执行；[旧作者工具源码](../base-migration-baseline/base-spire-dev/Program.cs)仅作迁移依据。目录及后续恢复见[规格与设计](../../docs/design/mod-the-spire/engine-dsl-migration.md)。
