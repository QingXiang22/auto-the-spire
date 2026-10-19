# 三件无状态攻击修正遗物 — 2026-10-18

本批通过共享加法阶段与Power的倍率征询，恢复MiniatureCannon、PaperPhrog、PaperKrane。归属 **CORE-AUTHOR-05／DSL-RELIC-CONTENT-07／RELIC-G05首子包**，负责Codex `/root`；基线`8a56af7`／[PR #23](https://github.com/QingXiang22/auto-the-spire/pull/23)，沿`codex/relic-chemical-x-20261018`接续。当前状态只看[主清单](../../tofix/README.md#当前交付)，拆分依据见[开发计划](../../../design/mod-the-spire/relic-combat-start/development-plan.md#g05首子包无状态攻击修正)。

## 接手后先做什么

1. 核对主清单／PR与本地改动，复用UpgradedAttackDamageRelic、StatusMultiplierRelic和两个pure倍率站点；准确输入、状态资格、顺序与范围见[无状态攻击修正契约](../../../design/mod-the-spire/relic-combat-start/content-lifecycle.md#无状态攻击修正dsl-relic-content-07relic-g05)，用法见[操作参考](../../../development/base-spire/reference/operations.md)。
2. 在仓库根目录运行`dotnet run --project tools/base-spire-dev -- check --relic-content-only`；新增AttackModifierRelicChecks的十项判别与已有遗物组一起执行。正式逐件见证继续用完整check报告，定向通过不代替独立原版双侧。
3. G05剩余优先核对PenNib的BeforeCardPlayed计数／当前翻倍牌与AfterCardPlayed清理，明确多段、重复和嵌套的牌身份后再认领。Akabeko需Vigor的AttackCommand生命周期，MysticLighter需附魔，不能复用整张牌消费或空字段近似；需要新共享能力先补契约，不恢复暂停域。

## 源行为与实现

| 内容 | 完整来源与真实消费者 | 已交行为 |
| --- | --- | --- |
| MiniatureCannon／miniature-cannon | MiniatureCannon.ModifyDamageAdditive | 有牌源、已升级且加成攻击，攻击者或牌拥有者属于持有者时加3；每段分别参与 |
| PaperPhrog／paper-phrog | PaperPhrog.ModifyVulnerableMultiplier，由VulnerablePower.ModifyDamageMultiplicative查询攻击者遗物 | 有正层目标易伤时，将1.5加0.25；与Cruelty百分数点相加 |
| PaperKrane／paper-krane | PaperKrane.ModifyWeakMultiplier，由WeakPower.ModifyDamageMultiplicative查询目标遗物 | 有正层攻击者虚弱时，将0.75减0.15；只作用于持有者受到的加成攻击 |

Cannon采用显式攻击者／牌源与Holder判断，保留源OR资格；受控跨拥有者牌源只用于检验谓词，不意味着召唤物体系已恢复。两种纸偶原方法不是override，必须追到Power的实际查询，不能仅凭遗物方法名注册支持。源码中的GetRelic查询在DSL中改为pure实例倍率征询，Power和伤害主流程没有新增遗物ID分支；内容叶只用模板use。

ModifyVulnerableMultiplier(value,owner,card,target)以150百分数点起算，目标Vulnerable确认正层后才征询，再加Cruelty；ModifyEnemyWeakMultiplier(value,owner,attacker)在攻击者Weak确认正层后以75征询，owner表示防守玩家。Power原有层数／时长不变。各模板绑定有效、未熔化实例并按库存顺序贡献；Cannon在reaction根据攻击者OR牌主判断，纸偶在instances按持有者过滤。开发Obtain只按本持有者去重。

加法在力量／Strike／升级牌阶段累计，后续倍率沿已有10000定点链保留精度，只在扣格挡前向下取整。实测floor((9+3+1)*0.75*(1.5+0.25+0.20))-2=17点生命损失；敌方floor((13+1)*0.60*1.5)-2=10。无对应状态、错误侧／其他敌人的状态以及无加成伤害不会套用纸偶倍率；移除／熔化立即恢复通常倍率。多次预览不修改状态、版本或随机游标，Thrash无目标吸收使用同一升级牌加法。

直接ObtainRelic开发夹具允许同定义多实例，仍逐件折叠；弱化倍率下限0，防止重复开发实例使负伤害变成加血／加格挡。这是显式开发合同，不冒充原版GetRelic只取一件的独立比较。

## 验证与证据

来源基线`local-sts2-source-2026-10-18`。三件完整源文件及VulnerablePower／WeakPower／CrueltyPower消费者已读取；遗物原始字节SHA256逐项与catalog一致，消费者hash一并记录于`.artifacts/relic-attack-modifiers/source-hashes.json`。PaperPhrog／PaperKrane经完整复核从unreviewed标为singleplayer，来源条目、池成员与原hash保持；未改源资格分母。

base版本`0.6.0-dsl.38`，继承engine/ir@0.6、vanilla-uniform@1和`base-spire-dsl-relic-draw-dev-v1`。包检查seed为数值`20261018`，真实stdio为字符串`BASE-DSL-STDIO-00`；环境Windows／PowerShell、.NET SDK 9.0.306、Python 3.12.0。

| 实际命令（仓库根目录） | 实际结果 | 证据与边界 |
| --- | --- | --- |
| `dotnet run --project tools/base-spire-dev -- validate` | 0 | `validate.log`；真实加载并启动包 |
| `dotnet run --project tools/base-spire-dev -- check --relic-content-only` | 0；87项 | `focused.log`；十项新增机制判别，三件逐项实际见证 |
| `dotnet run --no-build --project tools/base-spire-dev -- check --report .artifacts/relic-attack-modifiers/behavior` | 0；395项 | `package-check.log`和`behavior/package-behavior.json` |
| `python -X utf8 tools/ci/check_relic_coverage.py .artifacts/relic-attack-modifiers/behavior/package-behavior.json` | 0；55件见证／297来源／241未实现／1排除 | `relic-coverage.log`；摘要匹配，independent_verified=false |
| `python -X utf8 tools/ci/check_card_coverage.py .artifacts/relic-attack-modifiers/behavior/package-behavior.json --require-ironclad-complete` | 0；85/85铁甲、2项多人排除 | `card-coverage.log`；87卡定义有行为，全卡578来源保留 |
| `dotnet build AutoTheSpire.sln --nologo` | 0；0警告／0错误 | `build.log` |
| `dotnet run --no-build --project net-the-spire/Net.TheSpire.Tests -- --base-dsl-only` | 0；40通过／0失败 | `net-check.log`；真实Host子进程 |

日志均在`.artifacts/relic-attack-modifiers/`。包摘要`29a92115b3b788bca0bca81e82f62370db6b3943ea41845fbb24a9720fcc9f77`，ruleset `49b52118a93f58fe`。文档检查扫描786份Markdown、0问题，差异检查返回0，见同目录`doc-check.log`／`diff-check.log`；完整Engine／net／迁移回归由本次提交对应Actions执行，不以旧提交CI代替。

公开cannon-phrog从Strike永久获得、Whetstone真实升级开始，战斗中普通Bash造成8伤并施加易伤，随后升级Strike计算(9+3)*1.75=21，目标生命40→32→11。paper-krane从实际获得、FuzzyWurmCrawler.Spawn和Shockwave施加状态开始，敌方攻击／增益／攻击三回合玩家生命80→78→78→72，第三次使用敌方力量7；无资源或伤害结果注入。

## 未验证与接手自查

完整正常来源、商店／库存资格、Neow／地图／正常Run、独立原版双侧、全遗物及父项仍未验收；开发夹具和真实公开路径分列。召唤物／其他角色／复活与训练暂停保持。G05尚余PenNib、Akabeko和MysticLighter的完整机制，不能因本批三件通过而关闭整组。

修改范围为base修正站点／Power消费者／两个遗物模板／三内容及元数据、tools实际行为见证／net公开检查、契约／计划／手册／记录；未改core、独立参考、随机算法或源池顺序。准确完成条件仍按[迁移规格](../../../design/mod-the-spire/engine-dsl-migration/spec.md)及主清单。

本文和直接链接可定位目标／当前状态、可复用能力、接手第一步、验收条件／负责人、未验证与唯一维护位置；由Codex `/root`接手自查，未派出独立agent。记录先交tofix，开放项并入主清单后移records；转移不表示全范围验收。
