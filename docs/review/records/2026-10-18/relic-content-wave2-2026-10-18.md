# 遗物伤害与能量修正交付

2026-10-18，Codex `/root` 按用户继续推进遗物实现的指令，完成 **CORE-AUTHOR-05／DSL-RELIC-CONTENT-02** 的四件纯DSL消费者：StrikeDummy、FakeStrikeDummy、IceCream、ArtOfWar。行为见证从34件增至38件，来源297件保留，258件未实现、1件源废弃排除。当前状态与下一成果只维护[主清单](../../tofix/README.md#当前交付)，准确顺序见[遗物生命周期](../../../design/mod-the-spire/relic-combat-start/content-lifecycle.md)。

## 接手入口与完成条件

本批从 `778fc7b`／PR #19 接续，分支 `codex/relic-content-wave2-20261018`。已核对前置PR的core／base／net／migration在push和PR两次运行中均通过。接手先核对主清单和本批PR状态，再运行 `dotnet run --project tools/base-spire-dev -- check --relic-content-only`，预期29项通过；覆盖报告使用完整 `check --report`，不能用定向结果代替。

本批完成条件为四件实际消费者、标签与修正顺序、第一回合与后续回合差别、自动／重复／多段、失效与持有者隔离、跨战清理，以及真实公开stdio。负责人／用户结合源依据与证据审阅；尚无独立原版两侧和正常Run证据，完整遗物合同继续开放。后续可按源接ChemicalX的支付X／效果X分离或拾取永久升级，先补对应契约和实际消费者，不重做本批能力。

## 已交付与接口变化

| 遗物 | 已恢复行为 |
| --- | --- |
| StrikeDummy／FakeStrikeDummy | Strike标签牌的每段加成攻击分别+3／+1；与力量相加，再应用虚弱／易伤，最终一次取整后扣格挡；非Strike及无加成伤害不参与 |
| IceCream | 首回合重置能量；后续回合保留余能再加动态最大能量，战斗结束不跨战保留；沿当前资源域999上限 |
| ArtOfWar | 首回合不奖励；上回合未实际打完攻击牌，下回合能量重置后+1；自动及回合末攻击同样记录，战斗状态按实例清理 |

源为sts2/src/Core/Models/Relics中对应四个类，逐件通过定位助手 `--verify-source` 核对原始SHA256。另核对CombatManager.ResetPlayerEnergy的ResetEnergy／AddMaxEnergyToCurrent分支与AfterEnergyReset顺序，catalog保留源hash、池和资格。源码核对不算独立执行比较。

共享实现全部在base DSL。CombatModifierRelic在RelicObtained配置实例元数据，动态查询过滤Holder／IsMelted。DamageEnemyPowered现要求显式owner、card、target、amount；十处真实调用方已迁移，不从全局当前牌猜来源。当前单人范围owner同时为攻击者和牌拥有者，召唤物／跨拥有者攻击未额外恢复。

ResetTurnEnergy与GainEnergy分开，重置／补足不受禁止获得能量影响，额外奖励仍走GainEnergy。新增AfterEnergyReset先于AfterSideTurnStarted和抽牌。NoAttackTurnEnergyRelic在AfterCardPlayed记录、AfterTurnEnded转交、AfterEnergyReset奖励并清理；BeforeCombatStart及CombatEnded清理跨战标志。未改core、Host生产代码或参考玩法。

## 实际验证与身份

工作目录仓库根，PowerShell／.NET SDK9.0.306／Debug。base `0.5.0-dsl.32`，core0.5.0／engine/ir@0.5，scope `base-spire-dsl-relic-draw-dev-v1`，random_profile `vanilla-uniform@1`。包digest `5f8e6fbd350642caad885b154efc6fe9c531fec60900ce24b9352236b196a355`，ruleset `ae1057d1d1f52a4c`。行为seed20261018，公开stdio seed BASE-DSL-STDIO-00。

| 命令 | 返回码与结果 |
| --- | --- |
| `dotnet run --project tools/base-spire-dev -- check --relic-content-only` | rc0，29项；`.audit/relic-wave2-focused.log` |
| `dotnet run --project tools/base-spire-dev -- check --report .audit/relic-wave2-check` | rc0，347项；`.audit/relic-wave2-check.log`与同目录package-behavior.json |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests -- --base-dsl-only` | rc0，32 passed／0 failed；`.audit/relic-wave2-net.log` |
| `python -X utf8 tools/ci/check_relic_coverage.py .audit/relic-wave2-check/package-behavior.json` | rc0，38件见证，源297，未实现258，independent_verified=false |
| `python -X utf8 tools/ci/check_card_coverage.py .audit/relic-wave2-check/package-behavior.json --require-ironclad-complete` | rc0，87张见证，单人铁甲85/85、两项多人排除保留 |
| `python -X utf8 tools/doc-check/check_docs.py` | rc0，774份Markdown、0问题；`.audit/relic-wave2-doc-check.log` |
| `git diff --check` | rc0，无空白错误 |

定向检查区分多段／重复／嵌套自动、计算基础伤害、力量＋两个Strike加成在虚弱／易伤前参与、无加成伤害绕过、首回合重置／余能补足／动态最大能量、未攻击连奖／攻击后仅下一回合失奖、回合末攻击、开头自动攻击不被旧回合清理吞掉，以及不同持有者隔离与移除／熔化。真实stdio验证Strike造成10伤害，以及IceCream＋ArtOfWar能量3→7、攻击后6→9。

首次加载发现入口版本未同步，已修正；能量限制初始用例将当回合到期Power放在上一回合，不能判别重置站点，已改为结束回合后、重置前施加，最终验证余能补足仍执行、额外奖励受阻。最终相关检查均通过。

## 未验证与维护

独立原版两侧执行0，首差不适用；正常获得来源、随机遗物池与完整Run未恢复。正式熔化操作仍缺，本批仅验证已有熔化状态停止参与。全遗物、召唤物／跨拥有者攻击和其他未迁移修正未关闭。未改core，内核全组和更广回归交Actions；本地未重复无关全量套件。

机制与操作接口维护对应契约、[操作参考](../../../development/base-spire/reference/operations.md)和[物品指南](../../../development/base-spire/how-to/powers-relics-potions.md)。日志／生成报告留本地，不提交。按auto-the-spire-handoff完成接手自查，可定位目标／复用成果／第一步／完成条件／未验证／维护位置；未派独立agent。
