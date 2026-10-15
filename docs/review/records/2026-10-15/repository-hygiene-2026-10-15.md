# 仓库目录整理 — 2026-10-15

用户明确要求删除根archive，审查artifacts/reference和临时tools是否应进入主线。Codex `/root`从`codex/core-base-ci`接续，在`codex/repository-hygiene-20261015`交付，承接[CI-01](../../tofix/README.md#ci-01)；不修改玩法、公开契约、严格suite或恢复训练。直接推送原CI分支被GitHub规则GH013拒绝，理由是必须通过PR；保持保护规则，改用新分支PR接入原CI分支，再由既有PR #1进入默认分支。

## 文件处置

| 对象 | 当前树处置 |
| --- | --- |
| 根`archive/` | 删除72个已跟踪文件；整个目录移出仓库，旧代码保留在Git历史及仓库外备份 |
| `artifacts/` | 15个早期冻结日志/结果退出Git跟踪，本地原文件保留；整个输出目录继续忽略 |
| `reference/` | 仅跟踪README及sts2-sim拉取源、完整commit/tree身份清单；纯上游2089659获取脚本不变 |
| 临时/退休tools | 46项退出跟踪，工作树保留并忽略；同时删除.gitmodules |
| 维护中的tools | 保留[工程工具清单](../../../../tools/README.md)登记的九个目录，实际消费者与用途逐项注明 |

退出跟踪的工具为LuaBackendProbe、sts2-core-extract、orimod-decompile、Sts2Repairer及旧Legacy的content-manifest、coverage-check、dependency-check。旧覆盖工具绑定退休注册器和早期迁移分母；旧依赖工具扫描Legacy类型并产生同名报警，不能在删除目录后将零扫描算通过。它们退出当前维护范围不等于过往失败被修复。活动Engine/Slice/net检查、随机向量、严格正负例和17项suite定义均保留。

旧`sts2-sim-local.patch`曾用于恢复旧参考本地修改，当前纯上游不需要它，上一包已删除。用户链接的旧基线分支仍是历史快照；本包不重写旧提交，不自行切换默认分支或合并。

根.gitignore改为tools白名单，reference/baselines仅允许拉取信息JSON；新临时目录、补丁和产物默认忽略。87个历史链接改到不可变提交，逐个用`git cat-file`确认目标是实际blob/tree；更新当前README/协作约定中的已退休路径。旧文档内历史命令仅适用于旧提交。

## 实际验证与边界

从暂存树`a9987ca02282c90b31d6fe163b7c16f0ad17977c`导出独立副本，确认无archive、artifacts、sts2或参考克隆后执行：

| 命令 | 结果 |
| --- | --- |
| `dotnet build AutoTheSpire.sln -c Release` | rc0；4条已有base警告；无原版/独立参考仍可构建 |
| `python tools/contract-check/check_contracts.py` | rc0，210项，仍仅代表既有net/0.1覆盖 |
| `python ai-the-spire/tests/run_checks.py` | rc0，604项离线检查；未运行真实采样或训练实验 |
| 本地doc-check及diff-check | 最终491份Markdown无问题，diff-check通过 |
| 忽略边界探针 | 临时tools、输出、参考源码及旧patch均被忽略；CI入口和依赖信息可纳管 |

本包在[整理分支Actions](https://github.com/QingXiang22/auto-the-spire/actions?query=branch%3Acodex%2Frepository-hygiene-20261015)重新执行三个现有组；先前[首次运行](https://github.com/QingXiang22/auto-the-spire/actions/runs/38686046899)只证明整理前实现。托管结果须按具体提交核对，不能以本地构建冒充规则/对拍通过。

未新增运行时测试；未改规则、内容数值或DSL。原版源码核对、完整net客户端流程、全量四幕五角色/独立候选验收和训练不在本次验证范围。完整文档源链接检查仍需本地原版及历史证据，不因为清理而降低检查。

仓库外备份为`F:/Scylier-Project/backups/repository-hygiene-20261015/`：tracked-before.zip及逐文件SHA256清单保留133个普通文件与134项原跟踪路径（含旧gitlink），archive目录保留删除前完整目录，historical-links.json记录链接迁移；workspace及旁置build/协议/AI日志保存干净副本验证。既有完整工作区备份与旧参考备份不变。
