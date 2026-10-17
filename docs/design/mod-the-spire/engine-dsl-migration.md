# Engine DSL与base迁移入口

本次任务为 **CORE-AUTHOR-05**。2026-10-18用户确认：Engine为主体，Spire是完整DSL的子集，base严格使用DSL与必要JSON；直接切换，未迁移玩法明确报未支持。随后用户要求按spec driven design推进，补全设计和事实记录。

| 要看什么 | 唯一入口 |
| --- | --- |
| 已确认要求、范围与验收场景 | [spec.md](engine-dsl-migration/spec.md) |
| 长期目录、装配方式、数据边界与切换契约 | [plan.md](engine-dsl-migration/plan.md) |
| 实施顺序、依赖、要求追溯与检查 | [tasks.md](engine-dsl-migration/tasks.md) |
| 设计前实际改动、通过／失败检查与未完成状态 | [2026-10-18变更记录](../../review/records/2026-10-18/base-dsl-migration-checkpoint-2026-10-18.md) |
| 纯 DSL 首批实际交付与检查 | [2026-10-18交付记录](../../review/records/2026-10-18/base-dsl-native-cutover-2026-10-18.md) |
| 当前状态、认领与下一项工作 | [主清单](../../review/tofix/README.md#当前交付) |

纯 DSL 包的当前范围以 manifest 与实际交付证据为准；旧 C# 数量和历史运行命令不能作为新包支持证明。规格、设计与任务各维护一种事实，当前排期只维护在主清单。
