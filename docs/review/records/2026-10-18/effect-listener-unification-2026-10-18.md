# 遗物与Power统一监听交付

2026-10-18，Codex /root 按用户要求完成 CORE-AUTHOR-05／DSL-EFFECT-LISTENERS-01。已有遗物和Power统一为实例通知监听与只读修正监听，主流程只提供领域时机及计算阶段。当前状态见[主清单](../../tofix/README.md#当前交付)，准确契约见[统一监听](../../../design/mod-the-spire/effect-listeners.md)。来源分母保持，仍为38件遗物行为见证、297件来源、258件未实现和1件源废弃排除。

## 接手入口与完成条件

分支codex/effect-listener-unification-20261018基于a48ef1f／PR #20；前置PR的core、base、net、migration两轮CI均已通过。接手先核对主清单和本批PR，再运行dotnet run --project tools/base-spire-dev -- check --relic-content-only，预期33项通过。完整覆盖必须使用check --report，不能只凭定向检查结算。

从combat/modifier-hooks.spire查看领域接口，从relics/rules/after-shuffle-block.spire查看通知，从relics/rules/combat-modifiers.spire查看修正与私有状态；Power在powers/reactions.spire和powers/modifiers.spire。完整签名见[作者参考](../../../dsl/core/0.3/authoring-reference.md#实例订阅与只读修正)与[操作参考](../../../development/base-spire/reference/operations.md)。本包完成条件为已有消费者迁移、实例顺序和有效性、嵌套与暂停、查询纯性及原行为／公开回归。负责人和用户结合证据审阅；独立原版比较和正常Run仍缺，不关闭父项。

## 实现与行为变化

通知通过emit，修正通过foldEvent，共用subscription。instances声明有效实例查询，reaction接收完整载荷及末尾listener；instance_order按实际有序集合位置派发，priority表达早晚阶段。入口冻结候选和排序键，执行前重查资格；新增实例不追溯参与，移除或熔化可取消尚未执行的响应。通知沿原Session栈嵌套、暂停和续接，不另建调度器。

foldEvent依次调用pure监听者，将返回值作为下一监听者首参；没有有效监听者返回初值。链接时核验纯性、参数和返回类型，运行时缺返回值明确失败；禁止写实体、消费随机、发事件或请求决策。订阅求值使用独立临时变量帧，跨包查询与reaction自身上下文隔离。通用core不识别遗物、Power或游戏时机。

伤害、格挡、费用、最大能量、能量重置与获得、抽牌、休息、重复出牌、强制耗尽及Power叠加策略均改为监听修正。公共Relic移除StrikeDamageBonus、RetainEnergy、FirstTurnDrawBonus、RestHealBonus；ArtOfWar私有标志转到ArtOfWar.State，获得时创建、移除时销毁。Power反应、施加配置、衰减和到期迁入powers；原combat/ironclad-powers.spire移除，卡牌自身反应归cards/rules/reactions.spire。Power.Kind仍为共享能力角色，目录身份和层数保持实例状态。

获得顺序替代旧定义顺序，产生有意的可观察差异：35／80生命下先MeatOnTheBone再BlackBlood，胜利后59；反向顺序47。FeelNoPain先于DarkEmbrace时，耗尽引发抽牌的回调看到33格挡；反向看到30，最终均33。Thrash吸收预览改走与实际攻击相同的修正链，修复原先漏Strike遗物加成：6基础＋2力量＋3遗物，吸收11。卡牌自身查询特定Power层数仍归该卡规则，不冒充通用修正。

## 验证与身份

仓库根目录，PowerShell、.NET SDK9.0.306、Debug。core0.6.0／engine/ir@0.6、base0.6.0-dsl.33，文本core/0.3；scope为base-spire-dsl-relic-draw-dev-v1，random_profile为vanilla-uniform@1。旧engine/ir@0.5规则产物须重建，旧回放保留身份。包digest为43c88cbf48cbdd798a0cbb0ce56a4bbab848fa2a6afef468c39f14f761ec5d70，ruleset为6c526a4d984094db；行为seed20261018，公开BASE-DSL-STDIO-00，教学BASE-DSL-LESSON-00。

| 命令／检查 | 实际结果与本地证据 |
| --- | --- |
| dotnet build AutoTheSpire.sln --no-restore | rc0，0警告／0错误；.audit/listener-solution-build.log |
| dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests | rc0，118项，含7项监听边界；.audit/listener-core.log |
| dotnet run --project tools/base-spire-dev -- validate | rc0，实际加载启动；.audit/listener-validate.log |
| dotnet run --project tools/base-spire-dev -- check --relic-content-only | rc0，33项；.audit/listener-relic-focused.log |
| dotnet run --project tools/base-spire-dev -- check --ironclad-only | rc0，139项；.audit/listener-ironclad.log，最终包检查再次覆盖 |
| dotnet run --project tools/base-spire-dev -- check --report .audit/listener-check | rc0，351项；.audit/listener-check.log与同目录package-behavior.json |
| dotnet run --project net-the-spire/Net.TheSpire.Tests -- --base-dsl-only | rc0，32 passed／0 failed；.audit/listener-net.log |
| python -X utf8 tools/ci/check_relic_coverage.py .audit/listener-check/package-behavior.json | rc0，38见证、297来源、258未实现，independent_verified=false |
| python -X utf8 tools/ci/check_card_coverage.py .audit/listener-check/package-behavior.json --require-ironclad-complete | rc0，87见证，铁甲85/85、两项多人排除保留 |
| 更新教学模板后，通过run_relic_lesson.py真实stdio执行 | rc0，单独2／4／6／6；与8点算盘组合10／12／14；.audit/listener-lesson-beads.json与listener-lesson-combined.json |
| python -X utf8 tools/doc-check/check_docs.py | rc0，777份Markdown、0问题；.audit/listener-doc-check.log |
| git diff --check | rc0，无空白错误 |

监听边界覆盖顺序反转、同次新增不补入、销毁／失效跳过、嵌套通知、暂停后仅执行剩余监听者、跨包查询临时变量、空监听、嵌套fold、非法纯性／类型／签名和递归预算。重复LegalActions与Observe保持状态hash、版本及随机消费。Power顺序和真实伤害修正查询的强化断言经33项定向再次通过，生产包未因此变化。

开发中修复订阅查询临时槽未分配及跨包求值帧隔离问题，两者已有实际回归；规则括号遗漏在加载阶段修正。最终结果不依赖旧C#玩法、Host补偿或参考答案。

## 未验证与维护

独立原版两侧执行0，首差不适用；正常Run、多人、未迁移内容、替换与正式熔化操作仍开放。Power目录260项仍保留definition=null，本批迁移的是已有能力消费者，不计为260种内容实现。广覆盖和迁移检查由Actions承担，本地未重复训练、性能或正常整局套件。

交接按auto-the-spire-handoff接手自查，未派独立agent；状态只回写主清单，接口维护契约和作者参考，日志留.audit不提交。本地AGENTS.md不提交。
