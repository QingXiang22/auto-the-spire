# Anger 收口与当前卡牌整批安排复核 — 2026-10-09

Codex 负责人完成 Anger 战斗复制叶的受控正式源验收，修正两个读取漏洞并迁移默认身份；按用户“这一次直接把所有卡先补上”的要求，亲自制定当前 Ironclad 卡牌整批规格，供 DSH 连续实现。当前认领、派发结果、排期和下一机制只见[主清单 ENV-T05](../../tofix/README.md#env-t05)。本记录保存本次验收证据，不表示卡牌整批已完成。

## 接手与范围

执行者先读[整批规格与有限机制扩展契约](../../../design/mod-the-spire/ironclad-card-completion.md)，核对主清单和本批次派发记录，生成当前卡源覆盖矩阵，再按共享机制连续实施。Anger 已交付，不重做；每族检查点写同一综合交接，不等待逐卡再派单。全部指当前 Ironclad 阶段现有单人适用卡资源、普通/升级/hooks 和必要 Power，包括池外/状态牌对账；其余四角色、多人与新卡不在本次范围。

负责人负责职责决策、集成与验收；A 只按规格扩展有限节点、共享过程、内容与检查。新结构进入不可变领域程序，状态进 Engine 类型化存储；出牌历史使用可重复事件而非唯一实例集合，自动出牌共用生命周期，终局仍在外层完整动作尾。核心 engine/ir@0.3 保持冻结。父项完整合同不变。

## 复核与窄修正

旧远程任务 `ENV-T05-anger-clone-pool-20261009` / `session-48f37248-c0b5-4661-9174-b87dba1e5f3c` 已停止运行，固定原交接已移入[records](dsh-env-t05-anger-clone-handoff-2026-10-09-48f37248.md)。负责人直接阅读前端、兼容叶、共享复制过程和判别案例，对照 `sts2/src/Core/Models/CardModel.cs` 的 CreateClone 与原版 Anger。

复制策略符合当前可表达字段：普通6/升级8、一次目标选择、同定义的新实例，保留创建时变量/升级/临时费用，隐藏 CloneOf 指向本次源，LongTermSource 留空，长期牌组和跨房副本不会携带克隆；致命攻击之后复制及出牌尾继续。未建模附魔/折磨/ExhaustOnNextPlay 等不升格为完整 clone 支持，本次也没有独立双侧比较。

发现 `CloneCardStep` 用 OfType 过滤块成员，可能忽略嵌套声明；重复 pile 会按最后值覆盖，非法目的牌区可能被后续 discard 掩盖。负责人修正为逐成员校验并拒绝重复字段，在既有非法形状案例加入两个反例；不改有效 Anger 原文。规则身份检查保留且据实际工厂迁移，未删除门槛。

默认 dev 新身份：ruleset `b4d85a04422074c7`，PublicBuildId `5b86ab40c7f210a3`，SemanticsDigest `983a2268d49ce4db`；旧值分别 `f87614af13d18e0c`、`dc9325c6b9b5cca9`、`9d92f078cdb3ff5f`。新增隐藏字段改变规则身份，Anger 新注册改变公开内容语义/构建摘要；源 ContentDigest 仍为 `c0e34fb02899bff4`。实际工厂 Describe 见 identity.json。此后修正非法形状不改变正式有效模型。

改前哈希与当前核对见 review-hashes.json：正式 Anger 源、三个数字领域程序/编译文件和 RewardWiring 相对派单输入未变化。负责人生产改动仅 `SpireCardSource.cs` 读器、`CloneCardChecks.cs` 两个反例、`SliceMapRoomChecks.cs` 身份基线；其他交付归执行者原包，未重置共享工作区。

## 实际验证

工作目录为仓库根；全部构建与规则检查串行，最终带修正版本的命令/返回码/耗时写在[gate 清单](../../../../.audit/card-completion-review-2026-10-09/gates.json)。完整输出在同目录 name.log。正式源受控夹具 seed `20261001`，配置为默认 dev；地图工厂单独检查，没有缩池或跳过其门槛。

| 命令/门槛 | 最终实际结果 | 证明范围 |
| --- | --- | --- |
| `dotnet build AutoTheSpire.sln --nologo` | rc0，0错误、3既有警告 | 编译 |
| `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests --no-build` | rc0，95/95 | 冻结核心回归 |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests --no-build -- --effects-only` | rc0，25条 | 15既有效果 + 10条复制检查；执行者报告称9组，实际10条 Check.Run |
| 同项目 `--combat-only --report .audit/card-completion-review-2026-10-09/content-report` | rc0，375/375 | 默认身份及非地图规则检查，含全部本包案例 |
| 同项目完整运行 | rc3762504530（有符号 -532462766），340条 ok 后异常 | 首次地图工厂源85项池仍缺24卡；后续地图检查未执行 |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests --no-build` | rc1，410 passed / 5 failed / 415 executed | G-01～05同24卡对账失败；默认公开侧仍通过 |
| `dotnet build tools/base-spire-dev/BaseSpire.Dev.csproj --nologo` 后 `python -X utf8 tools/base-spire-dev/verify_examples.py` | 两项rc0，作者9/9 | 刷新实际DLL后独立卡文件工作流 |

内容实际加载：Ironclad94声明/94解析/68降级/26未支持；status2/2/1/1，正式单人池85项中61可执行，24仍缺。default factory CardCount69是合并资源数，不能代替池对账或行为覆盖。报告实际写出53条已验证内容ID（既有有限列表），未把每张声明自动计行为通过；完整覆盖矩阵是后续批次交付要求。

文档检查的实际日志见 audit 的 doc-check.log、diff-check.log；纯文档导航检查不替代规则/双侧/正常Run。未执行独立原版双侧、正常完整地图奖励闭环、真实客户端与四幕胜利。

## 后继任务与完成边界

整批任务提示与派发身份在[专属 audit](../../../../.audit/dsh-ironclad-all-cards-2026-10-09/task.txt)。派发前查询实际项目会话没有运行者，避免同一 A 文件并发写入。用户本轮覆盖此前“每次只派一张”调度，改为一次批次、逐机制实现/验证/检查点。遇到冻结核心或未授权边界，只阻塞关联族，完成所有其余就绪族后交准确部分结果，不能把部分完成写成全部完成。

成功接受派单只证明已安排，不证明新卡完成。最终卡牌池归零之后还要真实药水、奖励领取与容量/幂等、长期写回、地图后继及 B/C 双侧/公开证据；T05/M01、完整 Ironclad 和五角色均不在本次关闭。

派发实际返回 accepted：`ENV-T05-ironclad-all-cards-20261009` / `session-e6847f17-7dd8-4229-a7b5-017cd626a5fd` / request `91e69ceb-7fdd-457f-a84a-0a75f6cde043`；身份与提示词摘要见[派发记录](../../../../.audit/dsh-ironclad-all-cards-2026-10-09/task-record.json)。本轮未等待或宣称该批次完成。
