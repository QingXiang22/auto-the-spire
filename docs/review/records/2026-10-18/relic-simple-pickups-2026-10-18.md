# 五件简单拾取资源遗物 — 2026-10-18

本批复用两种DSL模板实现五件遗物的永久生命／金币拾取，并让拾取监听在实际执行前检查当前实例是否仍有效。

归属：**CORE-AUTHOR-05／DSL-RELIC-CONTENT-05／RELIC-G19**，负责Codex `/root`。基线`7c8dd04`／[PR #23](https://github.com/QingXiang22/auto-the-spire/pull/23)，沿`codex/relic-chemical-x-20261018`接续，未合并前批PR。当前状态及下一认领只看[主清单](../../tofix/README.md#当前交付)，分组方法见[开发计划](../../../design/mod-the-spire/relic-combat-start/development-plan.md)。

## 接手后先做什么

1. 核对主清单、工作区与PR，复用本批五份内容、两种模板及已交G01随机永久升级；不重做资源获得流程。准确语义见[生命周期契约](../../../design/mod-the-spire/relic-combat-start/content-lifecycle.md#状态接口与顺序)，用法见[遗物指南](../../../development/base-spire/how-to/powers-relics-potions.md)。
2. 在仓库根目录运行`dotnet run --project tools/base-spire-dev -- check --relic-content-only`，应返回0，并包含本批五件逐项资源数值、早期失效及直接获得／持有者隔离判别。全包覆盖仍消费完整check报告，定向通过不冒充独立原版双侧。
3. 下一子包沿RELIC-G04核对Candelabra／Chandelier／VeryHotCocoa／FakeHappyFlower／Sai源方法，按实际回合时机和状态寿命选同类消费者；复用AfterSideTurnStarted、GainEnergy／GainUnpoweredBlock和既有实例计数，先确认条件再合批。需要新生命周期时先补契约；其他角色／召唤等暂停项不随本批恢复。

## 源行为与已交实现

| 源类／DSL内容 | 完整AfterObtained行为 | 复用模板 |
| --- | --- | --- |
| FakeMango／fake-mango | 最大生命+3并治疗3 | ObtainMaxHpRelic |
| LoomingFruit／looming-fruit | 最大生命+31并治疗31 | ObtainMaxHpRelic |
| NutritiousOyster／nutritious-oyster | 最大生命+11并治疗11 | ObtainMaxHpRelic |
| SignetRing／signet-ring | 获得999金币 | PickupResourceRelic |
| GoldenPearl／golden-pearl | 获得150金币 | PickupResourceRelic |

五个具体内容文件只用模板use；主流程与Host没有遗物ID效果分支。FakeMango的源MerchantCost=50保留为来源事实，当前商店未据此验收；LoomingFruit的存档UniqueId只影响图标，本批没有将其写入玩法或随机流。

两模板以前是未绑定实例的RelicObtained通知处理器，不能在早期通知移除／熔化后取消执行；现在用instances绑定本次relic及持有者，按库存顺序并在执行前复核有效性。ObtainMaxHpRelic的开发守卫同时改用本持有者HasOwnedRelic，其他玩家持有同定义不再拦截本玩家获得。旧Strawberry／Pear／Mango／LeesWaffle／OldCoin／FakeLeesWaffle的正常数值沿原检查保护。

直接ObtainRelic和开发动作共用一次拾取；同定义旧实例不重发，移除不撤销最大生命、治疗或金币，移除后真实获得新实例再奖励一次。熔化但仍在库存时阻止重复开发获得；战斗开始／结束不重放拾取，也不消费随机。实际资源操作仍复用IncreaseMaxHp／Heal／GainGold，不扩大未迁移修正者的支持承诺。

## 验证与证据

源基线`local-sts2-source-2026-10-18`，五类完整文件均已读取，原始字节SHA256逐项与relics/catalog.json一致，核对结果保存于`.artifacts/relic-pickup-resources/source-hashes.json`。本批无需新增随机神谕，资源表期望取自源CanonicalVars和AfterObtained，现有源向量仍随整包执行。

base版本`0.6.0-dsl.36`，继承engine/ir@0.6、vanilla-uniform@1与`base-spire-dsl-relic-draw-dev-v1`。定向包场景seed为数值`20261018`，公开stdio为字符串`BASE-DSL-STDIO-00`。工具环境为Windows／PowerShell、根global.json的.NET SDK 9.0.306及Python 3.12。

| 实际命令（仓库根目录） | 实际结果 | 证据与边界 |
| --- | --- | --- |
| `dotnet run --project tools/base-spire-dev -- validate` | 0 | 真正加载并启动包，不能替代行为 |
| `dotnet run --project tools/base-spire-dev -- check --relic-content-only` | 0；60项 | `.artifacts/relic-pickup-resources/focused.log`；在原参数化表中增加五件见证，新增两项共享机制判别 |

| `dotnet run --no-build --project tools/base-spire-dev -- check --report .artifacts/relic-pickup-resources/behavior` | 0；378项 | `package-check.log`与`behavior/package-behavior.json`，全部当前包行为 |
| `python -X utf8 tools/ci/check_relic_coverage.py .artifacts/relic-pickup-resources/behavior/package-behavior.json` | 0；47件见证／297源／249未实现／1源排除 | 当前包摘要匹配；`independent_verified=false` |
| `python -X utf8 tools/ci/check_card_coverage.py .artifacts/relic-pickup-resources/behavior/package-behavior.json --require-ironclad-complete` | 0；85/85铁甲单人、2项多人排除 | 全卡578来源保留；87已声明定义有行为 |
| `dotnet build AutoTheSpire.sln --nologo` | 0；0警告／0错误 | `build.log` |

包摘要`a5de64ad430712d14dcc97838b452c1ac0e97ff2ec4ac1fb8af9f0992c232146`，ruleset `82d96f9806c3172b`，见`.artifacts/relic-pickup-resources/describe.json`。公开路径`dotnet run --no-build --project net-the-spire/Net.TheSpire.Tests -- --base-dsl-only`返回0，37项通过／0失败，证据为`net-check.log`。真实stdio逐件获得五件资源遗物并逐件移除，最大生命／生命／金币达到125／125／1248，移除后保持不变；未向生产状态注入资源答案。文档检查扫描784份Markdown、0问题，`git diff --check`返回0；完整Engine／net／迁移回归由Actions执行，正常Run与原版独立双侧未运行。

## 未验证与接手自查

正常Event／Ancient来源、商店价格与库存、完整资源获得／治疗修正者、Neow／地图／正常Run、原版独立双侧和全遗物仍未验收。两持有者和拾取前变更场景是明确受控夹具，不冒充正常单人流程；正常公开拾取和移除另经stdio验证。完整来源、原ENV/W、严格suite与暂停项保留；父项完成条件仍按[迁移规格](../../../design/mod-the-spire/engine-dsl-migration/spec.md)与主清单验收。

本批不改core、独立参考、随机算法、源池顺序或冻结旧源。修改边界为base两模板／五内容／manifest／公开元数据、包外参数化行为及公开检查、契约／计划／手册和本记录。

本文及直接链接可定位目标／当前状态、可复用成果、第一步、验收条件／负责人、未验证与维护位置。当前状态只回写主清单；规则方法回写契约和人类手册；逐批证据保留本记录，长日志留本地artifacts。这是接手自查，未派出独立agent评审。
