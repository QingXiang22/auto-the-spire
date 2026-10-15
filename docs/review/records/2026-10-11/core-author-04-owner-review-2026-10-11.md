# CORE-AUTHOR-04 重构交付复核 — 2026-10-11

重构的五部分实现已落地，负责人定向复核通过；完整任务保留待验证，严格回归的两个原有差异由 ENV-T04 接续处理。当前认领与验收只查[主清单](../../tofix/README.md#core-author-04)。本记录不声明完整原版环境、完整 Ironclad 或版本对拍全部通过。

## 接续入口与结论

复用现有 engine/ir@0.4、core/0.3、base/0.0.2 实现，不重做作者模型或文本绑定。下一生产包聚焦 `rng-initial-shuffle` 正常初始化的首差和随机调用顺序；范围见[接续任务](../../../../.audit/dsh-startup-alignment-2026-10-11/task.txt)。Codex保留参考裁决、最终复核和父项关闭权。

本轮已派发新会话 `session-f68b943d-7ca1-4a97-b2d2-979be7bcc654`，Harness返回accepted；[记录](../../../../.audit/dsh-startup-alignment-2026-10-11/task-record.json)保存请求ID、输入摘要、隔离输出及固定交接。派发前项目会话没有running，不向旧会话插单；未等待或宣称新包完成。

原执行会话 `session-00c5db45-5e96-4b1c-aa57-a6e6771109d5` 已停止，远端最后事件 seq2204 为 turn/end，reason=completed；[原派发](../../../../.audit/dsh-core-author-04-2026-10-11/task-record.json)和[DSH交接](dsh-core-author-04-handoff-2026-10-11-00c5db45.md)保留。取回的[最后历史窗口](../../../../.audit/core-author-04-owner-review-2026-10-11/result-final.json)含最后交付与结束事件，hasMore=true，不宣称已审阅全部2204事件。200消息请求超过客户端8MiB限制，随后缩为20消息取得该窗口；结论还依据实际源码、原始运行日志与独立定向执行。

## 实现完成度

| 原目标 | 核对结果与限制 |
| --- | --- |
| 单一卡牌定义与入口适配 | CardDefinitions规范化完整variant；正式效果派发、支付、结果区、生成/变形、升级、奖励均接入。旧.author只做输入适配，旧标量保留展示用途。X费读取PaidX修复已进入最终实现。 |
| 实际文本机制复用 | lifecycle-rules.spire经BindInto绑定现有宿主符号；正式EnergyNextTurnPower和第二个mymod Recovery复用同过程。没有为第二份内容新增enum分支；NoEnergyGain及消费时序有实际判别。开放范围为已接afterEnergyReset，不能据此声明所有Power hook已文本化。 |
| base/0.0.2与数值/快照 | common整字段覆盖和清空、编译期values、组合数值、let及具名上下文已接实际正式内容；旧输入继续适配。独立复核替换/清空、未知上下文、普通/升级实例、负数/大数和同名嵌套local。 |
| 地图结果复用 | 单backend最后一个完成输入，键包含blob和rooms；正式工厂每Session创建backend，Cells/Edges为不可变数组。没有缓存Session状态或补抽；DSH原两seed地图事实/边/流与严格对拍通过。未新增同条件性能测量，不量化提速。 |
| 版本与消费者 | core/Base程序集0.4.0、engine/ir@0.4、core/0.3、base/0.0.2，版本文档/代表源/fixture身份已迁移。未提交或打指向旧提交的tag；严格回归未全通过，不能把版本元数据改动描述成完整发布验收。 |

未发现须退回重做架构的证据。源码变化与[派发前310文件摘要](../../../../.audit/dsh-core-author-04-2026-10-11/inputs-before.json)核对结果见[变更路径](../../../../.audit/core-author-04-owner-review-2026-10-11/changed-paths.json)，不把仓库整体未提交差异都归给本执行者。

负责人还修正根/core AGENTS中的活动版本与新卡作者入口，区分历史0.3冻结锚点和当前0.4待验收工作区；未将此文档修正扩大为生产改动。

## 负责人实际验证

工作目录为仓库根，输出隔离到 `.artifacts/parallel/core-author-owner-review`；没有改生产代码或重复执行无变化的全量套件。

| 项目 | 命令与真实结果 | 证据 |
| --- | --- | --- |
| 当前作者工具 | `dotnet build tools/base-spire-dev/BaseSpire.Dev.csproj --artifacts-path .artifacts/parallel/core-author-owner-review -m:1`，rc0，3条既有nullable警告 | [构建](../../../../.audit/core-author-04-owner-review-2026-10-11/author-build.log) |
| 作者例子 | `BASE_SPIRE_DEV_DLL`指向该重建工具，执行 `python -X utf8 tools/base-spire-dev/verify_examples.py`，15次调用，rc0 | [当前例子](../../../../.audit/core-author-04-owner-review-2026-10-11/author-examples-current.log) |
| 新语法反例与边界 | 同工具执行negative-let、large-let、shadow-let，实际伤害分别9/10/5，rc0；unknown-local返回2且路径指原.spire，未注册 | [实际输入与输出](../../../../.audit/core-author-04-owner-review-2026-10-11/numeric-probes-current.json) |
| 当前Slice测试构建 | `dotnet build mod-the-spire/base-spire/BaseSpire.Slice.Tests --artifacts-path .artifacts/parallel/core-author-owner-review -m:1`，rc0 | [构建](../../../../.audit/core-author-04-owner-review-2026-10-11/slice-build.log) |
| 作者契约 | 隔离测试DLL `--author-contracts-only`，34项，rc0 | [日志](../../../../.audit/core-author-04-owner-review-2026-10-11/author-contracts.log) |
| 次回合能量 | 同DLL `--next-turn-energy-only`，10项，rc0，含文本复用/缺上下文拒绝 | [日志](../../../../.audit/core-author-04-owner-review-2026-10-11/next-turn-energy.log) |
| 次回合抽牌 | 同DLL `--next-turn-draw-only`，10项，rc0 | [日志](../../../../.audit/core-author-04-owner-review-2026-10-11/next-turn-draw.log) |

DSH作者工具与最终Slice/Host目录中的BaseSpire.dll摘要不同，旧作者例子证据不能单独证明最终实现；负责人已重建当前源码并重新执行例子，补足这一处证据。不同构建目录的二进制摘要本身不证明行为差异。

DSH保存的最终solution rc0、Engine99、完整Slice753、net416、两seed严格地图和正常公开Act1 rc0日志已检查，见其交接；本轮没有全部重跑。Act1为原seed ACT1-BASIC-10/326动作，幕完成、Hp8/Gold401，无整局Victory，只证明模拟侧该路线，参考整幕仍缺。父项与全角色范围继续开放。

## 严格首差的归因与剩余范围

[重构前Power夹具运行](../../../../.audit/dsh-power-fixture-2026-10-11/regression/regression.json)与[重构后运行](../../../../.audit/dsh-core-author-04-2026-10-11/regression/regression.json)的suite SHA256、两失败case的语义输入、判定以及完整差异签名均相同；[逐项比较](../../../../.audit/core-author-04-owner-review-2026-10-11/baseline-differences.json)保存了边界/路径/左右值。此证据支持这些已观察到的失败在此次重构前存在，不证明所有随机路径等价。

- `rng-initial-shuffle`：boundary0敌人FuzzyWurmCrawler缺失/存在；base遇到ShrinkerBeetle。UpFront累计消费base17/reference295。需要定位首次实际消费/调用路径差异，而不能给base补抽或改变敌人/seed制造一致。
- `random-reshuffle-unsupported`：boundary3初始牌槽顺序不同，Profile仍不可判定；不计通过，也不当作玩法规则错误。独立重洗适配范围须按现有授权和调用证据另行接续。

完整17项已执行，14双侧passed、1单侧self_checked、2未通过，rc1，比较器自检无失败。本记录不将失败改成通过、不删分母；CORE-AUTHOR-04保持待验证，ENV-T04/DIFF-02承担剩余严格比较。作者工具证据缺口本轮已补，生产代码无需为其再改一轮。
