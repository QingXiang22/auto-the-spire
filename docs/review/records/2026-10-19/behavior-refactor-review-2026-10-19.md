# 内容与行为目标及 review 整理记录

2026-10-19，Codex `/root`，CORE-AUTHOR-05 负责人文档维护。用户要求清理 review 过时文档，重新读取实现并整理覆盖攻击和其他卡牌行为的重构目标。当前状态及认领只见[主清单](../../tofix/README.md#当前交付)。

## 交付与边界

本次交付[内容与行为重构目标](../../../design/mod-the-spire/content-behavior-refactor-goals.md)，形成 G01～G10 与 A01～A17：卡牌正文、统一出牌与上下文、攻击、伤害结果、数值／费用、牌区／选择、复制／附着、Power 来源、回合／Run 和登记。文档区分已交付基础、代码观察、后续目标及未执行验收，不把 Power 定义迁移重复列为新重构。

当前架构和控制流程入口已按纯 DSL 修订，移除 use-only 及旧 C# 模块作为现行接口的描述。迁移设计保留原包边界，并将后续行为目标指向新文档。没有修改 Engine、base DSL、Host、工具实现、参考、源目录分母或完整 ENV／W 合同；没有派出 agent 或恢复暂停项。

## 阅读依据

本仓库起点为 `45c34c5`；base `0.6.1-dsl.39`。锁定 sim 为 `2089659bd07920088b93226136c72b6b6ee590ad`，tree 为 `8eb770da4e87677c54799801488b71a8ccba687b`，本次开始核对工作树干净。直接读取当前 DSL、锁定参考和原版方法，不依据旧对比推断当前缺陷。读取／定位的 50 份来源文件字节 SHA256 保存在本地 `.artifacts/behavior-refactor-review-20261019/source-identity.json`，不提交原版源码或大型原始产物。

重点走查攻击逐击目标与批量伤害、CardModel 的正文／附着／完成阶段、Cascade 的预取批次、PenNib 的源引用和预览差异、Vigor 的开始层数与来源、现行 Card 私有字段／复制及 Power 调用。源码结构观察不是已执行行为反例，A01～A17 均需在后续生产切片中实际验证。

## review 清理映射

17 份切换前交接／核验移入原日期 records，1 份已撤销结果及 2 份过时参考比较移入 deprecated。所有原正文保留并加历史边界，相关 Markdown 引用与迁移后的相对链接同步修复。移动的原始字节摘要、原副本及机器映射保存在本地 `.artifacts/behavior-refactor-review-20261019/`。

records 入口改为按主题查证，旧完整索引另存[index-before-2026-10-19.md](../index-before-2026-10-19.md)，不再要求接手者顺序读取多代报告。混合多个旧基线的工作流审阅保留历史正文，并在顶部指向正式迁移及新目标。

| 原位置（docs/review 下） | 新位置 |
| --- | --- |
| `tofix/dsh-env-t05-rewards-resume-handoff-2026-10-09.md` | [records/2026-10-09/dsh-env-t05-rewards-resume-handoff-2026-10-09.md](../2026-10-09/dsh-env-t05-rewards-resume-handoff-2026-10-09.md) |
| `tofix/dsh-host-declarations-close-handoff-2026-10-12.md` | [records/2026-10-12/dsh-host-declarations-close-handoff-2026-10-12.md](../2026-10-12/dsh-host-declarations-close-handoff-2026-10-12.md) |
| `tofix/dsh-ironclad-all-cards-handoff-2026-10-09.md` | [records/2026-10-09/dsh-ironclad-all-cards-handoff-2026-10-09.md](../2026-10-09/dsh-ironclad-all-cards-handoff-2026-10-09.md) |
| `tofix/dsh-normal-initialization-audit-handoff-2026-10-13-e524322c.md` | [records/2026-10-13/dsh-normal-initialization-audit-handoff-2026-10-13-e524322c.md](../2026-10-13/dsh-normal-initialization-audit-handoff-2026-10-13-e524322c.md) |
| `tofix/dsh-potion-public-handoff-2026-10-11.md` | [records/2026-10-11/dsh-potion-public-handoff-2026-10-11.md](../2026-10-11/dsh-potion-public-handoff-2026-10-11.md) |
| `tofix/dsh-potion-reuse-handoff-2026-10-11.md` | [records/2026-10-11/dsh-potion-reuse-handoff-2026-10-11.md](../2026-10-11/dsh-potion-reuse-handoff-2026-10-11.md) |
| `tofix/dsh-power-fixture-handoff-2026-10-11.md` | [records/2026-10-11/dsh-power-fixture-handoff-2026-10-11.md](../2026-10-11/dsh-power-fixture-handoff-2026-10-11.md) |
| `tofix/dsh-public-run-driver-handoff-2026-10-11.md` | [records/2026-10-11/dsh-public-run-driver-handoff-2026-10-11.md](../2026-10-11/dsh-public-run-driver-handoff-2026-10-11.md) |
| `tofix/dsh-reference-stream-boundaries-handoff-2026-10-12.md` | [records/2026-10-12/dsh-reference-stream-boundaries-handoff-2026-10-12.md](../2026-10-12/dsh-reference-stream-boundaries-handoff-2026-10-12.md) |
| `tofix/dsh-shop-public-contract-handoff-2026-10-12.md` | [records/2026-10-12/dsh-shop-public-contract-handoff-2026-10-12.md](../2026-10-12/dsh-shop-public-contract-handoff-2026-10-12.md) |
| `tofix/dsh-source-stable-shuffle-handoff-2026-10-12.md` | [records/2026-10-12/dsh-source-stable-shuffle-handoff-2026-10-12.md](../2026-10-12/dsh-source-stable-shuffle-handoff-2026-10-12.md) |
| `tofix/dsh-startup-alignment-handoff-2026-10-11-f68b943d.md` | [records/2026-10-11/dsh-startup-alignment-handoff-2026-10-11-f68b943d.md](../2026-10-11/dsh-startup-alignment-handoff-2026-10-11-f68b943d.md) |
| `tofix/dsh-strict-reshuffle-close-handoff-2026-10-12.md` | [records/2026-10-12/dsh-strict-reshuffle-close-handoff-2026-10-12.md](../2026-10-12/dsh-strict-reshuffle-close-handoff-2026-10-12.md) |
| `tofix/dsh-unknown-room-modules-handoff-2026-10-12.md` | [records/2026-10-12/dsh-unknown-room-modules-handoff-2026-10-12.md](../2026-10-12/dsh-unknown-room-modules-handoff-2026-10-12.md) |
| `tofix/env-t04-first-combat-closure-2026-10-15.md` | [records/2026-10-15/env-t04-first-combat-closure-2026-10-15.md](../2026-10-15/env-t04-first-combat-closure-2026-10-15.md) |
| `tofix/env-t04-rng-bootstrap-2026-10-15.md` | [deprecated/env-t04-rng-bootstrap-2026-10-15.md](../../deprecated/env-t04-rng-bootstrap-2026-10-15.md) |
| `tofix/env-t04-v109-random-2026-10-15.md` | [records/2026-10-15/env-t04-v109-random-2026-10-15.md](../2026-10-15/env-t04-v109-random-2026-10-15.md) |
| `tofix/env-t04-verification-2026-10-08.md` | [records/2026-10-08/env-t04-verification-2026-10-08.md](../2026-10-08/env-t04-verification-2026-10-08.md) |
| `records/2026-10-12/sts2-sim-flow-extension-comparison-2026-10-12.md` | [deprecated/sts2-sim-flow-extension-comparison-2026-10-12.md](../../deprecated/sts2-sim-flow-extension-comparison-2026-10-12.md) |
| `records/2026-10-01/sts2-sim-补全与保真差异-2026-10-01.md` | [deprecated/sts2-sim-补全与保真差异-2026-10-01.md](../../deprecated/sts2-sim-补全与保真差异-2026-10-01.md) |

tofix 仅保留唯一主清单。任务详情中的历史证据引用仍保留，搬移并不表示父项验收；原日志里的下一步与 delivery_status 均不作为当前指令。

## 验证

`python -X utf8 tools/doc-check/check_docs.py` 扫描 797 份 Markdown，0 问题；`git diff --check` 通过。搬移检查确认 20 份原正文除历史提示及链接外保留，50 份来源字节未变，主清单任务详情至原 ENV／W、暂停项合同除迁移链接外保持。目标编号 G01～G10、验收 A01～A17 连续且唯一，tofix 仅留主清单。结果在本地 `content-preservation.json`。

首轮文档检查发现本地原文备份以 `.md` 扩展名落在扫描范围，以及完整索引／旧控制流章节与一处本地证据的旧链接。已把原始备份改为 `.md.original`，修复对应引用后复验；暂存差异检查还检出搬移文件中的混合换行，已规范化。没有修改检查器或忽略实际文档错误。没有运行生产构建、玩法检查、独立对拍、正常 Run 或参考测试；本次是文档交付，不新增玩法验收结论。

发布身份检查中，`gh auth status --hostname github.com` 报告当前 QingXiang22Bot 的 keyring 凭据无效，`gh api user` 返回连接 EOF，尚未确认可用发布身份。本轮不使用其他账号替代，也不以提交作者设置代替认证；推送和 PR 需在该账号认证与连接可用后继续。

本轮维护完成后，下一次实施先在主清单认领具体机制切片，补接口、状态／来源时序和最小判别；本报告不新增生产派单。
