# 事件后跨战斗保存与Shrink修复 — 2026-10-16

AromaOfChaos升级或变换后的永久牌现在已实际带入第三场正常战斗、打出并完成奖励结算，两条路线分别45/53个边界独立双侧一致。本轮同时修复了第三场暴露的Shrink无限持续不生效问题；完整ENV-T02仍需Feed等原完成条件。

归属：[ENV-T02 战斗结果跨房间保存](../../tofix/README.md#env-t02)，同步ENV-T04/05/06。Codex `/root` 按用户“检查并继续推进任务完成”指令实施；当前排期、状态与认领只维护主清单。

## 接手后先做

1. 复用`normal-post-event`及既有`normal-aroma`，不要回到旧日志重做事件或Shrink。根目录运行下表命令，预期两个case双侧passed，且实际进入第三场、打出事件改过的牌并关闭奖励。
2. 按ENV-T02原条件接Feed最大生命跨房和适用永久写回矩阵；本轮只验收事件升级/变换的正常跨战斗子集。正常满容量仍按ENV-T05推进，当前三场路线最终只持两瓶药水，不满足满栏前置。
3. 后续改动先核对本轮规则身份与原seed；不能省略Shrink负层数、战斗副本来源、未知使用候选或源池。幕转换文件仍由原登记主责负责，召唤/复活与训练保持暂缓。

## 已交付能力

- `normal-post-event`从原seed正常开局，完成两场战斗、AromaOfChaos升级/变换，再选择下一个合法Monster点并完成第三场。事件后优先公开Power牌，确保变换得到的DarkEmbrace真正打出；其他选择仍沿Bash/Strike/Defend可见策略，隐藏来源只用于诊断比较。
- 两侧分别读取`LongTermSource`与`DeckVersion`，在手牌、抽牌、弃牌、消耗与执行中牌区保留永久来源编号；临时牌标为temporary，不按牌名重新匹配。第三场入口要求每张永久牌恰有一个副本，事件改过的实例必须实际打出，结算后原永久实例和形态必须保留。`combat-copy-origins`为必需维度，缺牌区来源即失败。
- Shrink源声明新增`negativeStacksAreInfinite=true`，经正式前端进入共享作者模型和IR。负层数仍应用固定0.7倍率，回合末不衰减；正层数继续衰减，零层数不应用倍率。未声明此属性的其他能力保持原门控。未改通用Engine，也未按怪物ID增加玩法分支。
- 工具身份映射修正DarkEmbracePower：正式能力ID保留`-power`后缀，卡牌DarkEmbrace不带后缀；原统一删除后缀会混淆二者。本轮只补源码已核对的对应，不更改参考效果。
- 比较器在一侧后续faulted时，仍检查此前已产生的共同边界，保留最早差异与最后一致边界；最终判定继续为fault，不把失败记passed。自检覆盖同定义牌交换来源、事件随机消费、永久来源与下游故障保留首差。

修改范围为base共享能力声明/伤害倍率/衰减与Shrink检查、tools正常策略/参考适配/比较器/suite及相关契约。未改core、参考、net生产或幕转换实现；使用既有公开客户端完成验证。

## 首差、源码依据与纠正

原失败记录在`.audit/post-event-20261016/first/`和`second/`，两例均非零。第三场ShrinkerBeetle施加Shrink=-1后，双方Power层数一致，但base忽略负层数倍率：升级路线boundary38的Bash造成8伤害，参考为5，敌人HP17/20。后续参考未结束战斗，工具才在TakeRewardCard报错；该动作报错是后果。

依据只读`sts2/src/Core/Models/Powers/ShrinkPower.cs`的IsInfinite、ModifyDamageMultiplicative和AfterSideTurnEnd，负层数表示无限持续。固定倍率源值为0.7。该属性不表示所有能力的负层数合并、覆盖和完整hook已实现。

将策略加强为实际打出变换牌后，boundary34又暴露工具投影`dark-embrace-power`/`dark-embrace`身份差异。核对`Models/Powers/DarkEmbracePower.cs`、本地正式声明与独立参考确认是映射问题，修正后全量能力事实保持比较。

临时审计入口`dotnet run --project .audit/post-event-20261016/compare-saved/CompareSaved.csproj --artifacts-path .artifacts/post-event-saved-20261016`返回0，仅重新比较保留的原始失败JSON。输出`saved-failure-upgrade.json`/`saved-failure-transform.json`均保持Verdict=fault，并恢复boundary38敌人HP首差17/20、22/24；不重新执行玩法，不覆盖原失败记录。

## 构建与实际验证

工作目录均为仓库根。隔离构建根`.artifacts/post-event-20261016`，证据根[.audit/post-event-20261016](../../../../.audit/post-event-20261016/)。配置为Ironclad、进阶0、全解锁、单人、dev-map、`sts2-v0.109-xxhash64@1`，原seed始终为`ENV-T04-RNG-SHUFFLE-001`。参考HEAD为`2089659bd07920088b93226136c72b6b6ee590ad`，工作树干净，未改参考。

| 检查 | 实际入口与结果 | 证据 |
| --- | --- | --- |
| 共同构建 | `dotnet build AutoTheSpire.sln --artifacts-path .artifacts/post-event-20261016`，最终rc0、0警告/0错误 | `final-build.log`；初次编译保留7条既有警告 |
| Engine | `dotnet .artifacts/post-event-20261016/bin/Core.TheSpire.Engine.Tests/debug/Core.TheSpire.Engine.Tests.dll`，rc0、99项 | `final-Core.TheSpire.Engine.Tests.log` |
| 完整Slice | `dotnet .artifacts/post-event-20261016/bin/BaseSpire.Slice.Tests/debug/BaseSpire.Slice.Tests.dll`，rc0、851项 | `final-BaseSpire.Slice.Tests.log`，含本地源检查 |
| net | `dotnet .artifacts/post-event-20261016/bin/Net.TheSpire.Tests/debug/Net.TheSpire.Tests.dll`，rc0、419项 | `final-Net.TheSpire.Tests.log` |
| Shrink专项 | Slice DLL追加`--shrink-only`，rc0、5项 | `shrink-checks.log`，含负无限、零层、有限到期 |
| 第三场严格比较 | Differential DLL执行`test --suite normal-post-event --output .audit/post-event-20261016/final-normal-post-event`，rc0、2双侧、13自检通过 | `final-normal-post-event/regression.json` |
| 原严格回归 | 同DLL执行`test --suite regression --output .audit/post-event-20261016/final-regression`，rc0、16双侧+1工厂自检、22自检通过 | 原17项保留 |
| 相关正常前缀 | 同DLL分别执行`test --suite normal-aroma`、`normal-held-potion`、`normal-potion-use`、`normal-potion-reward`、`normal-two-combats`；输出对应`final-`目录 | 均rc0，分别2/1/2/2/1双侧，自检无失败 |
| 公开重放 | `python -X utf8 .audit/post-event-20261016/public_upgrade.py`与`public_transform.py`，均rc0 | 分别66/76次提交，45/53边界；原始帧和摘要在`public-upgrade`/`public-transform` |

工具DLL为`.artifacts/post-event-20261016/bin/Differential/debug/Differential.dll`。最后一次共同构建只重编后续工具改动；base/core/net未再改变，未因此重复全量检查。正常suite的run_id为`normal-post-event@normal-post-event@1+20261016T124721Z`，其他run_id逐项保存在各regression.json。

| 路线 | 第三场结束后的双侧结果 | 比较边界 |
| --- | --- | --- |
| 永久升级 | HP61/80、金币156、13张永久牌；原升级实例保留，实际打出 | 45 |
| 永久变换 | HP53/80、金币156、13张永久牌；新DarkEmbrace实例保留，实际打出 | 53 |

两例均在`map:1,4`回到travel-ready，持AttackPotion与BlockPotion两个稳定槽位，奖励完整关闭。不同分支资源不要求彼此相等，要求各自与独立参考一致。

正常base PublicBuildId为`98868aea64a3bbef`；公开ruleset为`net:ruleset:e31571a57bd4e20f`，catalog为`net:catalog:84f5f969048a0fbc`，Host SHA256为`6ea639dd87ecb19a962147a671ea28977f208a887a3c2dd32d12ffb4f82ba8f1`。冻结内容配置的dev ruleset/build为`2df4b453b15febbf`/`b8d3a04a74271921`，dev-map为`1f864e09b839c59b`/`23b8f78062613886`；描述保存在`build-descriptions.jsonl`。迁移依据为Shrink真实语义修复，不是改常量掩盖差异。

公开重放逐边界核对资源、完整合法候选、可见手牌、永久牌组形态及永久句柄首次出现编号，并保留事件回答重发幂等和过期拒绝。重放脚本最初仅按牌名绑定，遇到普通/升级Strike并存时正确停止；修正为按公开升级状态和费用匹配，仍拒绝把公开字段不同的实例当作可交换。公开永久句柄连续性可验证，战斗隐藏LongTermSource/DeckVersion仅在工具诊断中比较，不声称公开接口泄露或提供此字段。

## 正常公开终点与未完成范围

正式客户端命令为`python -X utf8 net-the-spire/client/run_game.py --host-dll .artifacts/post-event-20261016/bin/Net.TheSpire.Host/debug/Net.TheSpire.Host.dll --scope dev-map --seed ENV-T04-RNG-SHUFFLE-001 --output .audit/post-event-20261016/normal-public --max-actions 220 --potion-policy take`。实际rc0，173次成功提交后在第8层Byrdonis自然死亡，HP0/80、金币184，发布Defeat/loss，未达到动作上限。修复前174提交的日期证据保留，不能拼接到本轮构建。

本包关闭事件永久结果进入下一战斗的窄验收与Shrink负无限伤害缺陷，不关闭ENV-T02/04/05父项。Feed最大生命跨房、最后击杀及完整写回矩阵的最终双侧验收、正常满容量、其余卡池/资格/生命周期、事件战斗返回、幕转换、完整四幕和五角色、最终120对与真实性/成本仍未完成。正常公开终点和严格三场策略不同，不能拼成双侧整局证据。

当前状态与下一成果已回写主清单，本记录已从tofix移入records；移动不等于父项验收。原失败/通过记录与脚本均留.audit，未提交共享改动、派新会话或恢复训练。接手自查可定位目标、成果、命令、完整条件与剩余范围，未做独立读者审阅。

工具与Host加载的BaseSpire.dll SHA256均为`9b484443ed43fe8fab5486d77f186d60cf1c4a1cd86ad82d5913bbff1daad367`。文档检查扫描581份Markdown、0问题、rc0；`git diff --check`返回0，日志见`.audit/post-event-20261016/docs-final.log`。
