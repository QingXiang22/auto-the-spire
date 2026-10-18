# 铁甲战士首幕普通战斗卡牌奖励交付

2026-10-18，Codex `/root` 完成 **CORE-AUTHOR-05／DSL-CARD-REWARD-01** 的本地实现，让公开开发客户端从真实战斗胜利选择三张奖励之一，并在下一战使用永久副本。当前状态只维护在[主清单](../../tofix/README.md#当前交付)，输入、资格、顺序与验收边界见[奖励契约](../../../design/mod-the-spire/combat-rewards/spec.md#铁甲战士首幕普通战斗卡牌奖励)。本批仍使用开发随机映射，不代表正常 Run 或原版独立对拍通过。

## 接手入口与完成条件

分支 `codex/ironclad-card-rewards-20261018` 从 `fc34ec8` 接续，依赖基础初态 PR #16 及其全卡／永久牌组前置；接手先核对主清单与本分支 CI，不能只拣奖励文件忽略前置。PR #16 的 core／base／net／migration 两轮检查已实际核对全部通过，尚未合并。

从干净开发初态提交 Ironclad.Initialize、TwigSlimeS.Spawn、BeginDevelopmentCombat、BeginDevelopmentTurn，用实际起始牌打败敌人，预期进入 rewards、Gold 不变、CardRewardOptions 三张不同定义、RunDeck 仍为十张。提交所选 ClaimDevelopmentCardReward 候选后，同一实例进入 RunDeck，其他候选销毁；领取金币或跳过后显式 CloseDevelopmentRewards，再进入下一战。完整命令从[开发工具](../../../../tools/base-spire-dev/README.md)进入。

本包完成条件为完整源奖励池、基本稀有度及偏移、三张去重、真实候选领取／跳过／关闭、永久身份及下一战副本、已有机制无回归。验收由负责人／用户结合证据审阅；不关闭 CORE-AUTHOR-05、M01／M02 或原 ENV/W。后续另立机制包恢复源随机布局或地图访问，再接 Neow 和正常 Run；不要继续把开发抽样扩张为原版随机等价声明。

## 交付与来源

- 保留全卡源 578 项、铁甲源池 87 项及两项多人排除。全部解锁单人 85 张中，按 Common／Uncommon／Rare 得到 80 张奖励牌：Common 20、Uncommon 35、Rare 25；三张 Basic、两张 Ancient 排除，Feed／NotYet 保留。战斗生成池的 78 项保持原用途，不用来生成奖励。
- 逐项核验 88 份冻结源元数据（87 池条目及 GiantRock）的原始 SHA256，再核验 Shockwave；为87个可执行定义补充实际 rarity。冻结元数据保留来源与普通／升级等原字段，CI 不依赖本地 sts2。源 CardPoolModel.GetUnlockedCards、CardCreationOptions.ForRoom、CardFactory.CreateForReward 与 CardRarityOdds.Roll 为资格和顺序依据。
- 每张先抽稀有度并更新跨奖励偏移，再从该稀有度剩余源顺序中选牌，最后消费升级抽样；同组三张不同定义。初始偏移 -5%，非 Rare 增长 1%、上限40%，Rare 重置 -5%。首幕升级基础概率零，保留源小于等于零边界；稀有度缺项按源序回退。完整85／80项预检在金币和卡牌抽样之前执行，缺项明确失败。
- 抽样使用 development-card-rarity／development-card-reward，sampleInt 的 24-bit 网格与整数交叉比较；它不同于原版 NextFloat 映射、流种子和游标。只支持首幕 A0、无奖励修正参与者，不宣称后幕升级率或正常 Run。
- 新 CreateCardInstance 只初始化卡实例，CreateCombatCard 复用后才发 CardEnteredCombat，奖励不会误触发战斗入场。Card.Rarity／CardBlueprint.Rarity 由模板配置，副本保留。CardRewardOptions 公开实际候选；临时 CardRewardPool 和未来稀有度偏移不公开。
- 卡牌领域 ObtainCardReward 接受本玩家的真实待领实例，先移入 RunDeck、设置永久牌组，再发 CardObtained。奖励域销毁未选实例、清 pending；跳过／关闭均不获得、不重抽。金币与卡牌条目独立。真实 stdio 用起始牌击败7HP TwigSlimeS，未注入HP或强制清场，领取与跳过后均实际进入下一战。

## 实际检查

仓库根目录，Windows PowerShell、.NET SDK 9.0.306、Debug。包 `0.5.0-dsl.29`，scope `base-spire-dsl-relic-draw-dev-v1`，random_profile `engine-default`。包 digest `be2e922a29537f68269fe60833426706f9150a7dd8b9cebc46a97892fabc569b`，ruleset `f930e4937680b1ab`。行为 seed 20261018／777，真实 stdio seed BASE-DSL-STDIO-00。

| 命令 | 实际结果与证据 |
| --- | --- |
| `dotnet run --project tools/base-spire-dev -- validate` | rc0，实际加载、链接并启动；之后资格预检及版本／元数据接线由全包检查覆盖 |
| `dotnet run --project tools/base-spire-dev -- check --card-rewards-only` | rc0，初次8项定向通过；之后补CR09缺源预检并纳入完整检查 |
| `dotnet run --project tools/base-spire-dev -- check --report .audit/card-rewards-check` | 最终rc0，308项通过；含CR01～09、完整卡牌元数据和既有消费者 |
| `python -X utf8 tools/ci/check_card_coverage.py .audit/card-rewards-check/package-behavior.json --require-ironclad-complete` | rc0，87张行为见证，单人铁甲85/85，两项多人排除保留，源578 |
| `python -X utf8 tools/ci/check_relic_coverage.py .audit/card-rewards-check/package-behavior.json` | rc0，18件见证，源297，未实现278；非完整遗物门槛 |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests -- --base-dsl-only` | rc0，27 passed／0 failed，含真实stdio领取、跳过、下一战与隐藏信息 |
| `python -X utf8 tools/doc-check/check_docs.py` | rc0，扫描766份Markdown，0问题；日志 `.audit/card-rewards-doc-check.log` |
| `git diff --check` | rc0，无空白错误 |

日志在本地 `.audit/card-rewards-focused.log`、`.audit/card-rewards-check.log`、`.audit/card-rewards-net.log`，行为报告在 `.audit/card-rewards-check/package-behavior.json`。广覆盖由本分支 Actions 执行，结果以 PR 为准，不预写通过。

实现中修复：检查的 Observe 调用补准确 projection 参数；冻结元数据按字母排序与源池 BloodWall／Bloodletting 顺序不同，改为依源池顺序匹配元数据，未修改运行池来凑通过。最终完整检查与公开路径均实际运行成功。一次文档补丁未匹配，核对并重发，没有影响玩法验证。

## 未验证与维护

独立原版两侧实际执行为0，首差不适用。原版 RNG、正常地图／Neow／Run、Elite／Boss／后幕升级率、奖励修改者／替代项／重抽／多选／完整获得修正均未恢复；现有敌人开发生成仍用源最小HP，不能算正常遭遇随机。其他角色、敌人召唤／复活及AI训练暂停保持。

状态回写主清单，接口写奖励契约和操作参考，长日志留本地。接手自查可定位目标、可复用内容、首步操作、完成条件、未验证与维护位置；未进行独立agent审阅。
