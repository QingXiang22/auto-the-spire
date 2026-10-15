# core/base与严格对拍CI — 2026-10-15

本包落实用户指定的core、base和独立对拍CI；contracts-python只提供说明，不在本轮实施。当前状态与认领见[CI-01](../../tofix/README.md#ci-01)，运行方式见[检查入口](../../../../tools/ci/README.md)。本报告不关闭环境/版本父项。

负责会话：Codex `/root`，分支 `codex/core-base-ci`。边界为工作流、共用命令入口、base检查分组、参考获取及文档；未修改core/base游戏规则、独立参考玩法、net/AI生产代码或训练。

## 参考升级

按用户要求拉取上游main当时最新 `2089659bd07920088b93226136c72b6b6ee590ad`，tree `8eb770da4e87677c54799801488b71a8ccba687b`。纯上游直接编译并运行现有严格regression通过，不需要修改适配器或应用本地补丁。

当前Git只登记仓库URL、完整commit/tree及获取脚本；参考源码位于被忽略的 `reference/sts2-sim`，不作为submodule或vendor进入本仓库。CI按SHA从GitHub拉取，缺失、身份不符或本地改动都明确失败；后续上游升级单独验证再更新锁定清单，不浮动使用main。

旧121项本地改动的完整工作树已保留在 `F:/Scylier-Project/backups/sts2-sim-pre-upstream-20261015/`，原补丁/许可证仍可从已推送的 `dcc7d60` 历史读取。当前目录是干净的2089659；旧证据不冒用新参考身份。

## 检查边界

三个独立Windows job分别运行 `python tools/ci/check.py core|base|differential`，默认Release。SDK由global.json固定9.0.306，Python为3.12；官方Action固定SHA。push、PR和手动触发，任一命令失败保留非零；始终尝试上传该组日志/result.json，严格对拍另保存实际双侧记录。

base将现有药水源元数据核对抽为source组；没有删除判别、改变源期望或减少默认完整suite。显式runtime组不读取原版源码，报告source_checks_executed=false；默认all仍包含该项。source组缺少当前仓库sts2时失败，不能借用祖先工作区。原版源码保持本地，不作为Actions输入或artifact上传。

## 本地实际验证

结算：[机器可读结果](core-base-ci-local-results-20261015.json)；原始命令/日志：[证据包](core-base-ci-local-evidence-20261015.zip)。验证副本先由Git导出再同步本包更改，未复用主工作区bin/obj，最后保留在 `F:/Scylier-Project/backups/ci-core-base-20261015-workspace/`。

| 命令或判别 | 实际结果 |
| --- | --- |
| `python tools/ci/check.py core` | rc0，99项通过 |
| `python tools/ci/check.py base` | rc0，runtime841项与作者工具15次调用通过；报告明确未执行source组 |
| `python tools/ci/check.py differential` | rc0，纯上游参考，16双侧passed＋1工厂接线self_checked；原17 case/维度/seed保留 |
| `dotnet build AutoTheSpire.sln -c Release` | rc0；7条已有nullable警告；源码与适配器均可编译 |
| 本地 `--suite source` | rc0，1项源核对通过；841＋1保留原842项范围 |
| 未提供当前仓库源码的 `--suite source` | 明确失败，不能借祖先目录原版源码 |
| 非法suite、suite缺值 | 均rc2 |
| 参考GitHub下载/复用/脏树 | 固定commit/tree下载和复用验证通过；故意新增文件后拒绝覆盖，文件保留 |
| CI子进程故障注入 | 子进程rc7原样返回，后续步骤不执行，失败日志与JSON仍保存 |

GitHub首次实际运行[38686046899](https://github.com/QingXiang22/auto-the-spire/actions/runs/38686046899)对应实现提交 `fc3c7365dc6cdbb523154ca002d8b0964360dad4`，core、base、differential三个托管Windows job均success，日志artifact上传成功。CI分支待用户审阅并入主线；本包不自行修改仓库默认分支或合并保护规则。后续若仅更新本报告，其运行结果不替代上述实现提交的证据。

## 保留范围

本轮没有接入contracts-python、完整文档源链接、依赖检查器同名报警或旧商店重放首差。后两项既有失败仍保留，不用continue-on-error伪装通过；它们不在用户本轮指定的三个CI组内。

严格regression仅代表实际登记案例，不代表完整四幕、五角色或新角色原语双侧验收；正常奖励prefix两个case、真实游戏客户端、最终120对、训练与发布均未在本包重验。后续contracts-python的现有命令、错误判别与依赖说明位于检查入口的提案节。
