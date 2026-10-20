# 工程工具与本地临时文件

迁移资料：[base-migration-baseline](base-migration-baseline/README.md)保留纯DSL切换前的源与检查依据，不是生产规则库、运行回退或当前完整检查入口。该目录纳入Git可见范围以防移动后的资料被忽略丢失；当前迁移状态见[当前开发资料](../docs/design/mod-the-spire/base-architecture.md)。

Git仅维护可复用、具有实际消费者的工程工具。一次性修复、提取脚本、实验探针与生成报告保留本地；新增工具目录默认忽略，确认维护用途后再登记白名单。

| 目录 | 维护用途 |
| --- | --- |
| [ci](ci/README.md) | 本地与GitHub Actions共用检查入口 |
| [differential](differential/README.md) | 保留原 suite 与明确的未支持报告；独立 DSL 适配尚未恢复 |
| [base-spire-dev](base-spire-dev/README.md) | 纯 DSL 包加载、源／支持计数与当前机制行为检查 |
| [contract-check](contract-check/check_contracts.py) | 协议/编码检查及AI当前导入的共享协议实现与fixtures |
| [doc-check](doc-check/check_docs.py) | Git 文档链接、格式与需求映射检查 |
| [dsl-generate](dsl-generate/generate-core.ps1) | ANTLR解析代码生成，供核心维护复现 |
| [rng-oracle](rng-oracle/README.md) | 独立随机数向量与对拍依据 |
| [source-manifest](source-manifest/README.md) | 本地原版建局与完整遗物清单的只读取证，不进入无源码CI |
| [collect](collect/collect.py) | AI既有有界采样入口；保留工具不代表恢复训练 |

`LuaBackendProbe`、`sts2-core-extract`、`orimod-decompile`、`Sts2Repairer`及旧Legacy的`content-manifest`、`coverage-check`、`dependency-check`不再纳管。旧清单/覆盖/依赖工具绑定退休实现与早期迁移来源范围，不能用作当前Engine验收；不把删除退休目录后扫描到零项当作通过。需要复查时查看[整理前历史](https://github.com/QingXiang22/auto-the-spire/tree/0aca7d1/tools)。活动 Engine 与通用 net 检查继续执行；旧 Slice、游戏专用 net 组与独立适配器源码及原测试集的必测用例总数保留在迁移对照，去向见固定版本的迁移数据。

运行产物统一写入 `.artifacts/<任务>/`，任务结束后清理；CI artifact 保留 14 天。日志、来源盘点和覆盖报告不进入正式文档。固定数据只有在存在自动检查消费者时才作为测试数据纳入 Git，流程见 [开发约定](../CONTRIBUTING.md)。
