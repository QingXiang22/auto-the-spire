# 第一幕接续：负责人复核与正常公开流程证据

日期：2026-10-10。负责人：Codex `/root`。任务：ENV-T05-act1-basic-flow-20261010，关联T06/T08/T09/T10。本文保存本次验收事实；当前状态、认领及下一包只维护[主清单](../../tofix/README.md#env-t05)。

## 结论和交接裁决

最小目标“正常Ironclad开局打完第一幕Boss”已达到。seed `ACT1-BASIC-10` 从80HP、99金币、10张起始牌开始，使用完整85项单人奖励池，经过16个地图节点，458次公开合法动作/决策应答，击败173HP、Slippery8的Vantom并关闭Boss奖励，最终32HP、404金币，`ActCompleted=true`、`MapPhase=6`、会话Running、无整局Victory。两次独立NDJSON Host运行的459个公开快照仅除session_id后完全一致。

[DSH输入交接](act1-flow-handoff-2026-10-10.md)和[其局部证据](act1-flow-boundaries-2026-10-10.md)可复用Boss派发、营火与纯随机袋类；不能据此验收完整第一幕。原实现把第一幕Boss奖励结束提交为整局Victory；宝箱只使用三个代表遗物、复用Rewards抽取同时决定稀有度和金币，纯bag尚未接Engine。负责人按职责和源行为重做上述接缝，没有沿用其完成判断。

实际轨迹在第9行进入真实Treasure，开箱生成真实offer及金币后合法跳过遗物，在7/12/15行休息。药水奖励也合法跳过。跳过没有删候选或缩源池，符合可选择的奖励流程；它不能证明未实现遗物的领取效果或药水取得已经完成。Pear/Mango/OldCoin领取与持有实例另用受控初态检查，不混作正常Run证据。

## 设计与修正

- **房间职责**：营火从根组包器提取到 `RestSiteWiring`，升级调用共享CardDefinitions；`TreasureRoomWiring` 拥有Engine内随机袋/offer/动作/持有实例；`MapRoomWiring` 负责进入房型与旅行门控；`RewardWiring` 负责奖励生成和关闭。卡牌无需知道地图相位或宝箱字段。
- **真实宝箱随机袋**：按 `SharedRelicPoolManifest` 的118项源身份及源顺序分桶，在Engine集合中持久存储，包含Shop/Event/Ancient等桶；在UpFront按源降序洗牌。TreasureRoomRelics独立抽稀有度，使用native float32的0.5f/0.83f阈值；进入时消耗bag头，Rewards独立抽42～52金币。跳过也不退回bag。空桶按Common→Uncommon→Rare→Circlet处理。未实现的offer领取具名失败，不缩成已实现的小池。
- **遗物取得**：声明式AfterObtained支持GainMaxHp/GainGold，Pear +10最大HP且加10HP、Mango +14、OldCoin +300金币；取得创建真实持有Relic实例，不能重复领取/开箱。118身份候选和4项实际效果注册（含起始BurningBlood）分别计数，不能把身份列表当效果覆盖率。
- **Boss奖励与幕边界**：Boss金币100，选牌使用Boss稀有度路径而非普通战斗概率；保留源抽取与后续rare offset。全部奖励处理后写独立ActCompleted，不提交整局Victory。当前下一幕未实现，唯一ContinueToNextAct入口明确Fail，防止虚假的游戏胜利，也保持0.2非终局快照必须有合法动作的契约。
- **连续击杀**：重复攻击首击杀死目标后，不再重复移除同一怪；随机攻击在空目标时跳过采样并保留父效果。独立回归区分末杀后的格挡与后续空抽样。
- **公开历史闭合**：合法正常路线中，已打Defend在ShrugItOff触发重洗后隐藏于DrawPile，但PlayedThisTurn仍引用它，原Host因此报N-02-SEQUENCE-CLOSURE。现在本回合已打牌获身份授权，不公开抽牌位置；回合/战斗结束撤销。net分别校验完整成员/授权成员后取并集，保留单个来源内重复拒绝，避免同一实例双计。旧0.1切片Profile显式排除新内部PowerCardSelection与PlayedReveal；正式0.2仍从声明派生。
- **作者契约**：布尔Power rules只能true、幅度来自层数；extraAttackPlays/hpLossCap允许固定1～99。Stomp克隆保留费用修饰并继续接收后续攻击减费。后一项没有专门克隆时序的独立对拍，不据构建升格验收全族。

源依据：`sts2/src/Core/Factories/RelicFactory.cs`、共享遗物Pool、RelicBag.Populate/Pull及对应Rooms/TreasureRoom、RestSite、CardReward、GoldReward；Vantom及Slippery源定义。生产不引用sim运行时，core/参考没有修改。随机袋检查独立翻译源分桶与降序洗牌，但仍不是完整源开局消费的独立双侧Run。

## 身份和范围

| 构建 | ruleset | base PublicBuildId |
| --- | --- | --- |
| dev | `dd189e446258a9b3` | `ad13462702f391a7` |
| dev-map | `174146637558ff57` | `2f3f041653d7fb5f` |

内容digest `22232b2f22a0f203`，实际卡93、Power41、敌人38，普通遭遇13（弱4/普通9）；这些不是完整原版分母。公开Host identity：ruleset `net:ruleset:1bc6190680d892d1`、catalog `net:catalog:f7637ccdeb19c732`、backend `net:backend:simulation-0.2-397a8ffa7f156cfb`，协议0.2。静态身份不含seed。

原始身份见[identity](../../../../.audit/act1-resume-2026-10-10/identity.jsonl)；正常轨迹输入/Engine状态hash/内部执行记录见[engine-trace](../../../../.audit/act1-resume-2026-10-10/engine-trace.json)。独立Host证据：[第一轮摘要](../../../../.audit/act1-resume-2026-10-10/public-host/summary.json)、[完整公开轨迹](../../../../.audit/act1-resume-2026-10-10/public-host/public-trajectory.jsonl)、[第二轮摘要](../../../../.audit/act1-resume-2026-10-10/public-host-repeat/summary.json)、[第二轮轨迹](../../../../.audit/act1-resume-2026-10-10/public-host-repeat/public-trajectory.jsonl)。

夹具存实际发布candidate_id，固定seed及公开构建身份；每步必须在新快照的合法列表中找到该候选。没有从Engine序号推导句柄，没有状态注入，也不在候选不存在时猜一个近似牌。首次探索使用正常规则Session与源地图事实；录制时调用真实生产投影，随后独立Host重新执行相同候选。该轨迹是当前开发构建的回归，不是独立原版游戏侧对照。

## 验证

在仓库根目录执行，实际退出码均为0：

| 命令 | 结果/证据 |
| --- | --- |
| `dotnet build AutoTheSpire.sln --no-restore` | 0错误；[最终日志](../../../../.audit/act1-resume-2026-10-10/build.log) |
| `dotnet run --no-build --project core-the-spire/Core.TheSpire.Engine.Tests` | 95/95；[日志](../../../../.audit/act1-resume-2026-10-10/engine-checks.log) |
| `dotnet run --no-build --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | 418/418；[日志](../../../../.audit/act1-resume-2026-10-10/slice.log) |
| `dotnet run --no-build --project net-the-spire/Net.TheSpire.Tests` | 416/416；[日志](../../../../.audit/act1-resume-2026-10-10/net.log) |
| `python -X utf8 tools/base-spire-dev/verify_examples.py` | 15/15作者工作流；[日志](../../../../.audit/act1-resume-2026-10-10/author-tool.log) |
| `python -X utf8 tools/base-spire-dev/act1_smoke.py --output DIR` | 两次各458提交，459快照一致，32HP/404Gold |

首差并非隐藏：曾发现重复击杀MembershipConflict、空SamplePick、奖励动作在提取中漏注册、旧切片新增内部集合/授权边未处置、重洗历史引用闭合错误，均修正并补相应回归。中途并行Host/构建导致DLL复制文件锁，已等Host退出后串行重建，最终构建日志覆盖该假失败；当时日志保留在net-build.log。原版独立双侧、药水领取/容量、全部Power时序和全量房型/后续幕未执行。

## 降低编码负担与后续工程量判断

本次把房间编排从卡牌/战斗层隔离。普通新卡只读DSL作者约定、一个已验证同机制样例和本牌原版源，通过check/play反馈；Power复用注册，升级/外部消耗共享契约已交付。指南明确新Power机制先由公共模块主责实现，内容执行者不能随卡增补跨层补丁。相同目标的一次物理牌仍可能涉及多个实例，不能以定义ID取代实例，历史可见性问题说明必须从公开Host验收编排。

根组包器仍约四千行，Power时序、作者前端与特殊目标仍有跨层接缝。现有模块减少了常规卡需要读取的上下文，不证明所有复杂卡仅DSL可完成，也没有“小模型token节省百分比”的对照证据。后继分配采用8～12张同机制族，复用一份能力接口和独立行为矩阵；新增球/毒/星星/召唤等机制回Codex，不让执行者读完整Run后自建实现。剩余上下文负担与运行时性能分别测量，不能把开发耗时直接推断成AI训练吞吐。

最小Boss流程已不用再补一个前置工程包。完整可用Ironclad环境仍需真实药水取得/容量、共享及角色/古代人遗物完整池与效果/源初始化消费、商店/事件/精英及其余Boss、后续幕/整局终局、剩余卡Power与必要数值时序、独立对拍/公开候选矩阵等机制包；五角色资源体系和全量内容继续在T12/T13。现阶段不承诺可信工时或全量卡数完成率。下一共享包选药水奖励闭环；DSH只先行卡内容，父项M01/M02不关闭。

本轮未解决旧MapDraws随机重放payload仍进入公开Run字段的问题，已交T03核对训练观察/诊断边界。完整源前置消费仍缺，宝箱袋接入不能证明vanilla seed完全一致。

指南落点：[base开发](../../../design/mod-the-spire/base-development-guide.md)、[规则集维护](../../../design/mod-the-spire/maintainable-rule-sets.md)、[modding](../../../design/mod-the-spire/modding-guide.md)、[实际职责模块](../../../design/mod-the-spire/control-flow-modules.md)。卡内容派单由[dsh-remote-operator skill](C:/Users/26877/.codex/skills/dsh-remote-operator/SKILL.md)执行，提示词及记录在 `.audit/dsh-card-families-2026-10-10/`；accepted仅为已派发，结果须负责人复核。

本轮已派发：`ENV-T12-card-content-families-20261010` / `session-70c7f4c9-c106-4fbc-9fe6-c28ceccd75bf`，accepted=true。[固定派发记录](../../../../.audit/dsh-card-families-2026-10-10/task-record.json)保存输入摘要与request身份；Harness当前默认xuseny/gpt-6.1-sol，未擅改模型。只派发，不把accepted写作卡牌完成；本轮没有继续轮询或取回DSH结果。
