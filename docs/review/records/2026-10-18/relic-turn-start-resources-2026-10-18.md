# 五件回合开始资源遗物 — 2026-10-18

本批按共同回合站点实现五件遗物，复用指定回合回能与跨战计数，并为Sai补上每回合无加成格挡模板。

归属：**CORE-AUTHOR-05／DSL-RELIC-CONTENT-06／RELIC-G04**，负责Codex `/root`。基线`4061360`／[PR #23](https://github.com/QingXiang22/auto-the-spire/pull/23)，沿`codex/relic-chemical-x-20261018`接续。当前状态与下一认领见[主清单](../../tofix/README.md#当前交付)，分组方法见[开发计划](../../../design/mod-the-spire/relic-combat-start/development-plan.md)。

## 接手后先做什么

1. 核对主清单与PR，复用五份use配置及三种模板，按[回合资源契约](../../../design/mod-the-spire/relic-combat-start/content-lifecycle.md#回合开始资源dsl-relic-content-06relic-g04)理解时点／计数；不要重做能量重置、监听调度或格挡操作。
2. 在仓库根目录运行`dotnet run --project tools/base-spire-dev -- check --relic-content-only`；其CombatTurnRelicChecks包含五件实际触发、跨战、持有者／实例隔离、失效、NoEnergyGain及无加成格挡判别。逐件覆盖继续消费完整check报告，定向通过不代替独立原版比较。
3. 下一候选为RELIC-G05攻击与格挡修正；接手时先读Akabeko／PenNib／MiniatureCannon等完整源方法，按一次消耗、多段／重复、加法／乘法阶段继续拆子包，不能只按“修改伤害”合并。需要新共享机制先补契约，暂停项不随本批恢复。

## 源行为与交付

| 源类／内容 | AfterSideTurnStart源行为 | 模板 |
| --- | --- | --- |
| Candelabra／candelabra | 本持有者第2回合回能2 | TurnStartEnergyRelic |
| Chandelier／chandelier | 本持有者第3回合回能3 | TurnStartEnergyRelic |
| VeryHotCocoa／very-hot-cocoa | 本持有者TurnNumber<=1时回能4 | TurnStartEnergyRelic，throughTurn=true |
| FakeHappyFlower／fake-happy-flower | 每次本持有者回合将TurnsSeen加1模5，归零回能1；计数跨战保存 | EveryTurnsEnergyRelic |
| Sai／sai | 每次本持有者回合获得7无加成格挡 | TurnStartBlockRelic |

TurnStartEnergyRelic新增throughTurn=false默认参数，已有Lantern仍为精确第1回合；Cocoa保留源<=1条件，实际入口从第1回合开始。所有回能调用GainEnergy，能量重置／AfterEnergyReset先完成，再打开回合门并发AfterSideTurnStarted。NoEnergyGain只阻止奖励，不阻止能量重置或花朵计数。

Sai的AfterSideTurnStarted不能换成AfterBlockCleared，因为首回合不清格挡。实测首回合Anchor10+Sai7=17，第2回合先清旧格挡再HornCleat14+Sai7=21，之后每回合7；敏捷20和脆弱实际参与普通格挡计算时，Sai仍固定7。共享资源上限保持999。

两种回能模板和新格挡模板均绑定有效、未熔化、Holder匹配的库存实例，并按真实库存顺序运行；统一调度在反应执行前重查资格。开发获得守卫改用本持有者HasOwnedRelic，其他持有者不会阻止本玩家获得；直接ObtainRelic仍支持独立实例。花朵按各自实例计数，移除／熔化不再推进，熔化期间冻结；新获得实例从0开始。公开身份新增五项，源池与资格未改，未给Host／core加入遗物ID效果分支。

## 验证与证据

源基线`local-sts2-source-2026-10-18`，五类完整文件均已读取，原始字节SHA256逐项与catalog一致，见`.artifacts/relic-turn-start/source-hashes.json`。数值期望来自源CanonicalVars与完整触发方法；本批不新增随机消费或随机神谕。

base版本`0.6.0-dsl.37`，继承engine/ir@0.6、vanilla-uniform@1与`base-spire-dsl-relic-draw-dev-v1`。定向／整包场景seed为数值`20261018`，真实stdio为字符串`BASE-DSL-STDIO-00`。环境Windows／PowerShell、根global.json的.NET SDK 9.0.306及Python 3.12。

| 实际命令（仓库根目录） | 实际结果 | 证据与边界 |
| --- | --- | --- |
| `dotnet run --project tools/base-spire-dev -- validate` | 0 | `validate.log`，真正加载并启动包 |
| `dotnet run --project tools/base-spire-dev -- check --relic-content-only` | 0；77项 | `focused.log`；复用原回合检查并新增7项有区分能力的判别，包含五件逐项见证 |
| `dotnet run --no-build --project tools/base-spire-dev -- check --report .artifacts/relic-turn-start/behavior` | 0；385项 | `package-check.log`及`behavior/package-behavior.json` |
| `python -X utf8 tools/ci/check_relic_coverage.py .artifacts/relic-turn-start/behavior/package-behavior.json` | 0；52件见证／297来源／244未实现／1源排除 | `relic-coverage.log`；摘要匹配，independent_verified=false |
| `python -X utf8 tools/ci/check_card_coverage.py .artifacts/relic-turn-start/behavior/package-behavior.json --require-ironclad-complete` | 0；85/85铁甲、2项多人排除 | `card-coverage.log`；87已声明卡定义有行为，全卡578来源保留 |
| `dotnet build AutoTheSpire.sln --nologo` | 0；0警告／0错误 | `build.log` |
| `dotnet run --no-build --project net-the-spire/Net.TheSpire.Tests -- --base-dsl-only` | 0；38项／0失败 | `net-check.log`，真实Host子进程 |

全部长日志在`.artifacts/relic-turn-start/`。包摘要`b6e941643fa232df54590d3e84a51eb95db916c1ef951369df38531dda869f21`，ruleset `6b42f1520a6950f0`。文档检查扫描785份Markdown、0问题，`git diff --check`返回0，记录在同目录的`doc-check.log`／`diff-check.log`；完整Engine／net／迁移回归由本次提交对应Actions执行，不以旧提交CI代替。

公开stdio先真实获得五件，第1至4回合能量依次为7／5／6／3、格挡每回合7，再手动结束并开始新战斗；第5次观察到的回合同时触发花朵与Cocoa，能量8、格挡7。没有注入资源结果。受控两持有者、同定义两实例、NoEnergyGain与格挡修正探针另列机制证据，不冒充正常Run。

## 未验证与接手自查

正常Event／Ancient来源、商店价格／库存、正常Neow／地图／Run、完整原版独立双侧及全遗物仍未验收；纯视觉动画／展示状态未作为玩法恢复。来源297项、原ENV/W、严格suite与其他角色／召唤／复活／训练暂停保持。父项仍按[迁移规格](../../../design/mod-the-spire/engine-dsl-migration/spec.md)及主清单验收，当前行为见证不关闭父项。

修改边界为base三模板／五内容／manifest／公开元数据、tools回合参数化行为／见证及net公开检查、契约／计划／手册和本记录；未改core、参考、随机算法或源池。文档核对还发现操作参考仍称“只发一个回合时点”，已按现实现更正为AfterEnergyReset→AfterSideTurnStarted→起手抽牌→AfterPlayerTurnStart。

本文及直接链接可定位目标／当前状态、可复用成果、第一步、验收条件／负责人、未验证与维护位置；本批由Codex `/root`接手自查，未派出独立agent。新记录先进入tofix，开放项并入主清单后由负责人转records；移入records不表示完整验收。
