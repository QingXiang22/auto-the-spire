# 工程工具与本地临时文件

Git仅维护可复用、具有实际消费者的工程工具。一次性修复、提取脚本、实验探针与生成报告保留本地；新增工具目录默认忽略，确认维护用途后再登记白名单。

| 目录 | 维护用途 |
| --- | --- |
| [ci](ci/README.md) | 本地与GitHub Actions共用检查入口 |
| [differential](differential/README.md) | 独立参考适配、严格对拍、有效正负例与suite |
| [base-spire-dev](base-spire-dev/README.md) | DSL作者诊断、示例验证与公开游戏驱动 |
| [contract-check](contract-check/check_contracts.py) | 协议/编码检查及AI当前导入的共享协议实现与fixtures |
| [doc-check](doc-check/check_docs.py) | 文档链接、需求映射与历史证据恢复 |
| [dsl-generate](dsl-generate/generate-core.ps1) | ANTLR解析代码生成，供核心维护复现 |
| [rng-oracle](rng-oracle/README.md) | 独立随机数向量与对拍依据 |
| [source-manifest](source-manifest/README.md) | 本地原版建局与完整遗物清单的只读取证，不进入无源码CI |
| [collect](collect/collect.py) | AI既有有界采样入口；保留工具不代表恢复训练 |

`LuaBackendProbe`、`sts2-core-extract`、`orimod-decompile`、`Sts2Repairer`及旧Legacy的`content-manifest`、`coverage-check`、`dependency-check`不再纳管。旧清单/覆盖/依赖工具绑定退休实现与早期迁移分母，不能用作当前Engine验收；不把删除退休目录后扫描到零项当作通过。需要复查时查看[整理前历史](https://github.com/QingXiang22/auto-the-spire/tree/0aca7d1/tools)。活动Engine/Slice/net检查与严格对拍case均保留。

`artifacts/`、`.artifacts/`和`.audit/`保存本地构建、测试、轨迹、比较与诊断输出，均不跟踪；不能把生成报告当作工具源码提交。必要验收证据由报告链接到对应GitHub Actions运行或明确标注的历史证据。
