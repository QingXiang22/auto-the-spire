# 永久牌组、战斗副本与营火锻造交付

日期：2026-10-18。Codex `/root` 按用户“继续补全游戏相关内容”完成 **CORE-AUTHOR-05／DSL-DECK-SMITH-01** 的本地实现与检查，让营火升级实际永久实例并进入后续战斗；完整环境和原版对拍仍未恢复。当前状态与下一工作见[主清单](../../tofix/README.md#当前交付)，接口与判断条件见[机制规格](../../../design/mod-the-spire/permanent-deck-smith/spec.md)。

## 接手入口

本包依赖尚未合入的 PR #12／#13，分支 `codex/permanent-deck-smith-20261018` 从 `d8901d6` 接续；没有覆盖本地 AGENTS.md。不要从 main 单独拣这次改动后声称已有全部前置。后续优先复用本包永久实例与获得事件恢复真实 Ironclad 开局来源、起始遗物或卡牌奖励来源；源随机布局和正常 Run 应另作完整包，不把开发 `.Obtain` 动作当作正式随机奖励。

## 已交付能力

- `RunDeck` 保存独立永久 Card 实例，`Card.CanUpgrade` 区分真实定义与无效果开发占位牌，`PermanentSource` 为隐藏的具体来源引用。30 个已迁移 CardDefinition 均生成具名 `Obtain` 入口，调用纯 DSL `ObtainPermanentCard`，实际入组后才发 CardObtained。
- 采用永久牌组的战斗在开始时调用 BuildCombatDeck：删除该玩家旧非永久牌，从 RunDeck 按序生成新副本；普通／升级、费用和已有关键词保留。战斗内临时升级、生成、消耗和 Power 牌不反写永久实例，不进入下一场永久牌组。原版初始洗牌与自动抽牌未实现，Draw 仍显式调用。
- 营火锻造从本玩家永久可升级候选发布 Engine 原生的 0..1 实例决策；公开仍使用 net.answer_decision。取消不升级也不用掉选项，成功升级精确实例后用掉选项并发 AfterRestSiteSmith。单候选也保留手动选择，原稿 ForgeCardInHand 已移除。
- 营火不能在战斗、奖励或同次访问中重复进入；开始战斗不能绕过营火关闭。取消后能休息，成功后显式离场；失败关闭开发房间但不产生整局 terminal。动作与共享过程守卫一致。
- Anger 的生成副本保留 Owner／CanUpgrade 等已有元信息。没有修改 Engine／Host／wire 的生产玩法，公开元数据只增加 RunDeck 分组。

## 来源与实际验证

来源：`sts2/src/Core/Entities/RestSite/SmithRestSiteOption.cs`（永久牌组、可用资格、允许取消、成功后 AfterRestSiteSmith）；`CardModel.IsUpgradable`（一次升级边界）；已迁移卡牌各自的普通／升级行为继续复用原源规则；逐个检查了 30 个已迁移源类，均没有 MaxUpgradeLevel 覆写，沿用 CardModel 的默认 1。本包不是原版全量克隆、获得修正或附着保留的证明。

所有命令在仓库根目录执行，Windows PowerShell／.NET SDK 9.0.306，Debug。包 `0.5.0-dsl.26`，scope `base-spire-dsl-relic-draw-dev-v1`，random_profile `engine-default`；本地 seed 20261018，真实 stdio seed `BASE-DSL-STDIO-00`。最终包摘要 `0dc389813539c18b10c0842c2342ee284a62180df6098443223859afa38c51c4`，ruleset `0ba232e83d5a3541`。

| 命令 | 实际结果 |
| --- | --- |
| `dotnet run --project tools/base-spire-dev -- validate` | rc0，实际解析、加载与启动；新共享机制没有语言表达缺口 |
| `dotnet run --no-build --project tools/base-spire-dev -- check --report .artifacts/base-dsl/permanent-deck-smith` | rc0，冻结包输入后的最终 152 项通过；包括 8 项永久牌组／营火判别；此前已编译并实际执行该组 |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests -- --base-dsl-only` | rc0，20 项通过；真实 stdio 选牌／取消，永久升级进入战斗分别观察 9／6 点伤害 |
| `python -X utf8 tools/ci/check_relic_coverage.py .artifacts/base-dsl/permanent-deck-smith/package-behavior.json` | rc0，17 件遗物见证，源 297、未实现 279，`independent_verified=false` |
| `python -X utf8 tools/doc-check/check_docs.py` | rc0，759 份 Markdown、0 问题 |
| `git diff --check` | rc0，无空白差异错误 |

本地报告目录 `.artifacts/base-dsl/permanent-deck-smith/`；最终包检查日志 `.audit/permanent-deck-smith-check.log`。最初检查发现两处新守卫与旧开发生命周期不相容：重复开始战斗的具名诊断被房间守卫抢先覆盖；失败后房间未关闭、恢复生命后的开发新战斗无候选。已调整守卫顺序和开发失败收尾，保留原断言并复跑通过。公开检查第一次构建因 GroupDto 字段误写为 GroupTypeId 失败，改用真实 TypeId 后通过。

行为报告首次因检查期间修改了包注释／公开元数据说明而与当前包摘要不符，覆盖检查拒绝了该报告；最终冻结包输入后重建报告并检查，不手改报告中的身份或通过场景。

## 未验证与后续边界

本地未执行完整 Engine／net／migration 回归；广覆盖由本分支 Actions 执行，结果以 PR 检查为准。没有运行原版独立双侧、正常开局、地图／完整 Run、全量矩阵或性能，`independent_verified` 继续 false；父项不关闭。永久组目前仍是单玩家开发范围，无随机卡池、获得修正、附着、永久成长反写、正常角色初态和源初始洗牌。SmithCount 多目标、遗物增加／禁止的营火选项与其消费者仍未迁移。

接手自查：主清单定位目标／认领；本记录与规格定位能力、来源、命令与缺口；下一步从永久牌组的实际消费者接续。这里没有进行独立 agent 或真实客户端评审。
