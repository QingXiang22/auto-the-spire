# 铁甲战士基础初态与开发战斗起手交付

2026-10-18，Codex `/root` 接续 **CORE-AUTHOR-05／DSL-IRONCLAD-START-01**，让纯 DSL 开发客户端建立源起始牌组和遗物，并使用自动回合抽牌。当前状态只维护在[主清单](../../tofix/README.md#当前交付)，准确输入、顺序和边界见[角色初态契约](../../../design/mod-the-spire/character-combat/ironclad-start.md)。本批不构成正常 Run 或原版独立对拍验收。

## 接手入口与下一步

分支 `codex/ironclad-character-start-20261018` 基于 `9541ebe`，依赖铁甲战士全卡 PR #15 及其永久牌组前置，不应只拣本次角色叶定义。先核对主清单与 PR 检查，再从现有公开客户端的干净开发初态提交 `Ironclad.Initialize`；预期永久牌组十张、BurningBlood 一件、生命 80、金币 99。建立真实遭遇或开发目标后提交 `BeginDevelopmentCombat`、`BeginDevelopmentTurn`，预期能量 3、五张自动起手。旧机制探针不提交初始化时保持手动 Draw。

本包完成条件为源基础资源／5-4-1牌组和遗物、初始化拒绝、自动抽牌及跨战副本隔离、真实公开候选可用和既有机制无回归。负责人／用户结合实际检查审阅，不据本包关闭 CORE-AUTHOR-05、M01／M02 或正常首幕。后续另立完整机制包恢复正常卡牌奖励或源随机初始化，再接地图／Neow；不得用缩池随机卡或当前开发洗牌宣称原版对齐。

## 交付内容与来源

- `characters/ironclad.spire` 只用模板配置；共享初始化拒绝覆盖已有牌、物品、敌人、Power、房间、战斗或资源进度。清除开发占位牌，调用现有具名 Obtain 建立独立永久实例，获得起始遗物。新增公开 CharacterId／AutoDrawPerTurn，不改 Engine／Host 玩法。
- 源 `Characters/Ironclad.cs`：80 HP、99 金币、5 StrikeIronclad／4 DefendIronclad／1 Bash、BurningBlood；角色 SHA256 `d38ef18057517516e7d7e133d8d3b660001c995af763b444894e06263ecb454e` 与目录匹配。卡牌定义及元数据复用前包。
- 源 `Player.PopulateCombatState`／`CardPile.RandomizeOrderInternal`：独立战斗副本再洗牌；初始洗牌不发 AfterShuffle，因此 TheAbacus 不应在开战时加格挡。此处使用已有 engine-default 的 development-shuffle 流，不复现原版 UnstableShuffle／run 流。
- 源 `CombatManager` 的回合抽牌顺序：能量／side 接缝后，首回合逐张将 Innate 移到顶部，至少抽 Innate 数且最多十张；每次调用 DrawOne，之后才发玩家回合钩子。后续回合也自动抽五张。仅显式初始化角色使用此路径。
- 源 `BurningBlood.AfterCombatVictory`：存活持有者胜利治疗 6，封顶，移除／熔化无效。复用 CombatEndHealRelic，补存活守卫，失败及手动终止不触发；源 SHA256 `31fba8ba9a3e64f48c28ea463851fdb33d5f210e181da3a1ee56a53f128020be` 匹配。
- 第二场重建新战斗副本，永久身份和生命／遗物延续，临时成长不回写。公开检查沿真实 stdio 执行初始化和起手，通过实际出牌候选验证五张手牌；保留现有手牌集合 not_published 约定和隐藏 PermanentSource。

## 实际验证

工作目录为仓库根，Windows PowerShell、.NET SDK 9.0.306、Debug。包 `0.5.0-dsl.28`，scope `base-spire-dsl-relic-draw-dev-v1`，random_profile `engine-default`；行为 seed 20261018，真实 stdio seed `BASE-DSL-STDIO-00`。包 digest `8685b811b5f427460320add7f9e23d0d255b5c2a2e3fdad0abc52e7d735095e5`，ruleset `0bae8475f34b3c98`。

| 命令 | 实际结果与范围 |
| --- | --- |
| `dotnet run --project tools/base-spire-dev -- validate` | rc0，解析／链接／实际启动；随后完成时序修正并执行下列全包行为 |
| `dotnet run --project tools/base-spire-dev -- check --report .audit/ironclad-start-check` | 最终 rc0，299 项通过，含 IS01～08 的初态拒绝、5／4／1、抽牌事件顺序、Innate 顺序及上限、满手／禁止抽牌、跨战和治疗边界 |
| `python -X utf8 tools/ci/check_card_coverage.py .audit/ironclad-start-check/package-behavior.json --require-ironclad-complete` | rc0，87 张可执行卡见证，源 578，单人铁甲 85/85，保留两项多人排除 |
| `python -X utf8 tools/ci/check_relic_coverage.py .audit/ironclad-start-check/package-behavior.json` | rc0，18 件行为见证，源 297，未实现 278，非完整遗物门槛 |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests -- --base-dsl-only` | 最终 rc0，25 passed／0 failed，含真实 stdio 角色初态与实际五张起手候选 |
| `python -X utf8 tools/doc-check/check_docs.py` | rc0，扫描 764 份 Markdown，0 问题；日志 `.audit/ironclad-start-doc-check.log` |
| `git diff --check` | rc0，无空白错误 |

原始日志留本地 `.audit/ironclad-start-check.log`、`.audit/ironclad-start-net.log`；行为报告 `.audit/ironclad-start-check/package-behavior.json`。新增源码定位两次均返回 0、hash matches。完整 Actions 留本分支，不能由局部本地成功预写 CI 通过。

执行中修复：初始化守卫括号错误拆成明确分组；直接失败检查改用 EngineFaultException；源复核撤销初始洗牌错误的 AfterShuffle；公开检查修正 GroupMemberDto 句柄访问，并按现有 not_published 手牌约定使用真实出牌候选。最终包行为和公开组均实际重跑通过；没有放宽初始化、抽牌或隐藏信息合同。

## 未验证与维护

独立原版双侧执行为 0，首差不适用。正常 Neow／地图／Run、进阶、原版随机流、附着底部排序、完整手牌抽取和治疗修正参与者未支持或未验证；手动开发结束并非正常胜负。公开手牌完整集合契约仍未恢复，本次只验证真实候选与现有可见实体。五角色、召唤／复活、训练暂停保持。

当前认领只回写主清单；方法更新角色契约、操作参考与领域指南，历史日志保留本地。本次接手自查可由正文和直接链接定位目标、已交付、操作入口、完成条件、未验证与维护位置；未进行独立 agent 审阅。
