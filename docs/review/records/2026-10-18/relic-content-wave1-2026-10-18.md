# 遗物内容补全首组交付

2026-10-18，Codex `/root` 按用户将重点转回base-spire内容补全的指令，实施 **CORE-AUTHOR-05／DSL-RELIC-CONTENT-01**。本组新增16件有实际玩法的纯DSL遗物，可执行且有行为见证的遗物从18件增至34件。当前状态见[主清单](../../tofix/README.md#当前交付)，准确时序与边界见[遗物生命周期契约](../../../design/mod-the-spire/relic-combat-start/content-lifecycle.md)。297件源目录不缩减，262件仍未实现，另有1件源废弃排除；本批不关闭全遗物任务。

## 接手入口与完成条件

分支 `codex/relic-content-wave1-20261018` 从 `fdf310a`／RNG PR #18 接续，依赖前置纯DSL铁甲卡牌、永久牌组和战斗链。已核对PR #18的core／base／net／migration两轮CI均通过，尚未合并。接手先核对主清单认领和本组PR结果，再用 `dotnet run --project tools/base-spire-dev -- check --relic-content-only` 定位本组具体机制，预期20项定向检查通过；完整覆盖必须运行 `check --report`，不能用定向命令代替。

实际操作入口是各遗物具名Obtain，例如Pear.Obtain／OrnamentalFan.Obtain。拾取效果通过共享ObtainRelic发出的RelicObtained执行，未来奖励和初始化可直接复用。显式初始化Ironclad后获得BagOfPreparation，首回合自动抽7张，后续回合回到5张；若Innate更多则按max处理，不额外加抽。

本包完成条件为16件完整持有效果及实际消费者、逐件行为见证、拾取事件、每回合／跨战计数区别、自动／重复／末杀时序、失效与实例隔离、真实stdio和已有内容无回归。负责人／用户结合这些证据审阅；缺独立原版两侧比较时保持待验证。后续继续遗物内容组，可优先接伤害／费用修正及拾取永久升级机制，复用已有生命周期；缺房间／物品来源的遗物按明确机制补齐，不转移到无关架构扩张，也不注册空效果增加数字。

## 本组新增内容

| 机制 | 内容与实际行为 |
| --- | --- |
| 拾取资源 | Pear最大生命+10；Mango+14；LeesWaffle最大生命+7后回满；FakeLeesWaffle治疗最大生命10%取整；OldCoin获得300金币 |
| 回合／胜利／抽牌修正 | BlackBlood胜利治疗12；CaptainsWheel第三回合清格挡后得18；BagOfPreparation首回合手牌抽取+2；RegalPillow营火治疗+15 |
| 出牌计数 | Nunchaku每10次攻击打完+1能量，计数跨回合／跨战；Kunai／Shuriken每回合每3次攻击分别+1敏捷／力量；OrnamentalFan每3次攻击得4无加成格挡；LetterOpener每3次技能造成5无加成群伤 |
| 战斗内一次／指定回合 | CentennialPuzzle首次正HP损失抽3，本场一次；StoneCalendar第7回合末造成52无加成群伤 |

源依据为sts2/src/Core/Models/Relics中对应16个类及CreatureCmd／Creature.HealInternal、HealRestSiteOption、CombatManager／Hook；已逐件比较catalog中的原始SHA256，16份全部匹配。源行为、资格和版本身份保留在relics/catalog.json，没有缩减池成员或改独立参考。OldCoin的源商店／幕资格和BlackBlood替换获得尚无正常Run消费者，不能据开发获得宣称该部分完整。

共享改动全部在base DSL：Strawberry拾取从开发动作尾部移入RelicObtained，避免直接获得漏效果；新增HasOwnedRelic／HasActiveRelic、动态首回合抽牌／营火治疗查询、共享GainGold和HealToMaximum。新增早期回合计数重置站点与回合末自动出牌后／弃牌前的触发站点；末杀后停止敌方阶段。计数状态在Relic实例，重复／自动各计真实完成次数，多段攻击只计一次。移除／熔化立即停止动态修正；熔化仍占持有身份，不能重新获得OldCoin复制金币。未增加Host或Engine中的遗物ID分派。

唯一必要通用维护是模板实例总量：新增内容加载时命中原256上限，实际错误定位到后续RubyRaidersNormal展开。将总量提高到8192，保留32层深度、递归与4096展开成员保护；新增300实例真实链接回归并运行内核全组。该调整不改变既有游戏执行语义或运行版本。

## 实际检查与身份

工作目录为仓库根，Windows PowerShell、.NET SDK9.0.306、Debug。base `0.5.0-dsl.31`，core0.5.0／engine/ir@0.5，scope `base-spire-dsl-relic-draw-dev-v1`，random_profile `vanilla-uniform@1`。包digest `87b4c5ce8daa2f8004a99ba1f98e5a7241cb27f46a7b83f22c43004f15044f1b`，ruleset `cf4d4cc1fdaf8a6a`。行为seed20261018，既有RNG向量涵盖777等；真实stdio seed BASE-DSL-STDIO-00。

| 命令 | 实际结果与证据 |
| --- | --- |
| `dotnet run --project tools/base-spire-dev -- validate` | rc0，实际加载／启动34个遗物定义；初次加载的模板容量失败已定位修复 |
| `dotnet run --project tools/base-spire-dev -- check --relic-content-only` | 最终rc0，20项定向；`.audit/relic-content-focused.log` |
| `dotnet run --project tools/base-spire-dev -- check --report .audit/relic-content-check` | rc0，338项包行为；每件新增遗物都有真实执行场景，报告为同目录package-behavior.json |
| `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` | rc0，111项；`.audit/relic-content-core.log`，含300实例及原递归保护检查 |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests -- --base-dsl-only` | rc0，30 passed／0 failed；`.audit/relic-content-net.log`，真实stdio拾取达到111最大生命／399金币，以及第三次攻击获得4格挡 |
| `python -X utf8 tools/ci/check_relic_coverage.py .audit/relic-content-check/package-behavior.json` | rc0，34件行为见证，源297，未实现262，1项源废弃排除 |
| `python -X utf8 tools/ci/check_card_coverage.py .audit/relic-content-check/package-behavior.json --require-ironclad-complete` | rc0，87张见证，单人铁甲85/85，两项多人排除保留 |
| `python -X utf8 tools/doc-check/check_docs.py` | rc0，772份Markdown、0问题；`.audit/relic-content-doc-check.log` |
| `git diff --check` | rc0，无空白错误 |

本地日志与产物不提交，广覆盖留本PR Actions。验证覆盖拾取直接共享入口、满血／部分血／百分比取整、跨回合／跨战区别、自动／重复／多段、末杀余效、熔化／移除、持有者与新实例隔离。不是逐件独立原版模拟器对拍，也不是正常Run证据。

## 未验证和维护

独立原版两侧实际执行0，首差不适用；源码核对与实际DSL见证分别保留。随机遗物生成与资格、完整获得／治疗／金币修正、BlackBlood升级替换、死亡防止、其他遗物和正常Run仍缺。CentennialPuzzle依赖当前正HP损失站点，未额外承诺未迁移伤害／死亡防止参与者；不存在正式熔化动作，本组只验证已有熔化状态的失效效果。

任务状态和下一成果只更新主清单，机制／接口维护对应契约和操作参考，历史命令保留本记录。接手自查能定位目标、已交付、第一步、完成条件、缺口及维护位置；未进行独立agent审阅。
