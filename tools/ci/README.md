# core、纯 DSL base、net 与迁移检查

规则目标见[锁定 sts2-sim](../../reference/README.md)。现有 base 来源清单、随机向量和受控比较仍验证其原来源；各组通过不代表已经迁移到新目标，也不证明客户端保真。CI 报告分别保留目标锁定清单与实际检查范围。

本页给出 [GitHub Actions](../../.github/workflows/core-base.yml) 的检查入口。在仓库根目录执行，需要 Python 3.12 和 Git；运行时检查另需根 `global.json` 指定的 .NET SDK，文档组另需 [Vale 3.24.0](../doc-check/README.md)。默认 Release，可用 `--configuration Debug`。

功能分支由PR的创建／更新触发五组检查，main在push后验证，另保留手动触发；同一功能分支提交不再同时运行push和PR两套矩阵。尚未创建PR时，分支push不会自动运行本工作流。

```powershell
python tools/ci/check.py core
python tools/ci/check.py base
python tools/ci/check.py net
python tools/ci/check.py migration
python tools/ci/check.py docs
```

| 组 | 实际执行 | 结论边界 |
| --- | --- | --- |
| core | 构建并执行通用 Engine 检查，包括同包文件组合 | 不等于原版玩法验收 |
| base | 通用加载并启动纯 DSL 包、执行当前机制行为、正常开局及固定源地图／队列比较、核对 DSL 遗物及卡牌执行结果 | 机制 scope及部分正常开局；铁甲战士卡牌检查要求源池 85 张单人铁甲战士完整且有实际执行结果，保留 87 项来源及两项多人排除 |
| net | 通用 wire／text-rules 检查和纯 DSL 公开路径，包含真实 stdio 子进程 | 含seed 777的真实Neow至首战奖励领取／跳过及地图返回；完整正常Run的snapshot／control／formal检查尚未迁移 |
| docs | 检查 Git 文档（含共享 AGENTS.md）的链接、锚点、JSON 示例、需求引用、Vale 项目术语和差异格式 | CI 安装固定版本 Vale 并校验 SHA256；检查不读取本地原版源码或历史产物 |
| migration | 准备锁定参考，校验构建依赖隔离、153 个固定版本的源代码组与 11 份原 suite；执行 Strike／重洗双侧、逐次随机诊断、正常开局首差预检及严格失败检查 | Strike 通过，重洗与正常开局预检保留真实首差，其余原用例未支持；不代表正常 Run 或完整差异测试验收 |

首次非零使当前组失败。原始日志、退出码及 `result.json` 写入 `.artifacts/ci/<组名>/`，Actions 上传各组 artifact，保留 14 天。base 的行为报告标记 `independent_verified: false`；迁移清单中的原检查均标记未执行。固定版本的文本 SHA256 采用 UTF-8 与 LF，跨检出换行保持一致。

base组的 `check --normal-run-only` 保留10组固定字符串seed，比较完整前置遗物袋、三幕房间队列、Neow候选、Gaussian拒绝抽样，以及首幕最终节点／房型／父子边和随机游标；源向量的[生成入口](../rng-oracle/README.md#正常开局与首幕地图)与生产规则隔离。公开正向路径从Neow实际获得遗物到首个真实战斗，不注入敌人、清场或奖励。未支持选项／遭遇保留在源池并验证失败无额外状态／随机消费；同组还验证正常首战领取／跳过返回地图、自然死亡与受控末杀／余效顺序；完整整局及逐步双侧比较仍不在本组验收范围。

铁甲战士卡牌检查可单独复查：

```powershell
python tools/ci/check_card_coverage.py .artifacts/ci/base/behavior/package-behavior.json --require-ironclad-complete
```

它校验当前包摘要、578 项全卡来源、固定版本的铁甲战士卡池及源 SHA、普通／升级元数据对应的行为报告和全部已声明卡牌执行结果；不将局部 Ironclad 检查解释为全卡或原版独立比较通过。固定来源元数据来自主源快照，CI 不需要本地 `sts2/`。

完整来源固定数据位于[包外工具 fixture](../base-spire-dev/fixtures/source-catalogs.json)，共10个领域1516项，保留来源路径、原始字节SHA256、池及成员顺序和源排除依据。base工具嵌入该文件，Python覆盖检查与定位助手读取同一份已纳管数据；生产目录只登记已加载定义。来源条目总数和实际执行结果分别校验，干净检出无需本地原版源码或历史报告。

## 原完整差异测试与覆盖缺口

```powershell
python tools/ci/check.py differential
python tools/ci/check_relic_coverage.py .artifacts/ci/base/behavior/package-behavior.json --require-complete
```

当前这两条命令分别返回 3 和 1。原 regression 的 strike-normal 通过原 actions／decisions／facts／pile-order 比较；random-reshuffle-unsupported 实际执行双方原三次结束回合，各抽取 9 次洗牌随机调用，并保留流状态与玩法首差。其余 15 个 case 明确未支持；测试集仍非零且 `independent_verified: false`。完整源遗物覆盖仍缺，不能以局部行为测试结果关闭完整验收。Actions 当前矩阵采用 `migration`，校验两个原用例、源流独立向量，以及缺参考、缺维度、零执行、错误终点、候选错序、参数差异、记录丢失和首差等失败路径，没有以 `continue-on-error` 吞掉完整差异测试失败。

旧 C# Slice、作者探针、Net 游戏专用组与独立适配器保留在[迁移对照](../base-migration-baseline/README.md)。逐组源标识、原场景名和恢复去向见[CI 迁移数据](fixtures/legacy-dsl-checks.json)。本次用户授权直接切换及功能回退，不代表原判断意图消失。

## 其他检查

文档组在 PR CI 执行；本地可运行 `python tools/ci/check.py docs`。忽略文件不能作为正式链接目标。本地原版源码核对、Python 客户端完整轨迹、AI 离线检查、完整正常 Run、完整差异测试／120 对矩阵及性能均不包含在上述行为组中。相关要求仍见[任务入口](../../docs/roadmap.md)、[ENV 范围](../../docs/design/environment-completion-and-differential-v1.md)和[参考版本](../../reference/README.md)。不得从开发检查通过推导这些项目已经验收。
