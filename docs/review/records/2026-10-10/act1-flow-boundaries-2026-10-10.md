# ENV-T05 Boss奖励边界与营火机制证据（2026-10-10）

本记录保存 `ENV-T05-act1-basic-flow-20261010` 的局部交付证据。当前任务状态、下一成果及负责人仍只维护于[主清单 ENV-T05](../../tofix/README.md#env-t05)。本记录不代表固定seed正常第一幕通关。

接手时先核对主清单，再处理真实地图宝箱及其他未支持房间，最终从真实初始牌组只提交合法动作完成Boss战斗与奖励收口。禁止清场、HP注入、修改地图、删奖励池作为正常轨迹证据。

## 已交付机制

- `RewardWiring.Declare` 接受关闭奖励后的IR回调；回调仅在金币、卡牌及已掉落药水全部解决后执行。地图Boss节点提交现有单幕开发范围的 `Victory(Turn)`；普通房间返回选图。不是跨幕/完整游戏通关。
- `ChooseMapPoint` 进入真实RestSite节点后设 `MapPhase=4`，记录节点访问与地图楼层，不启动战斗、不消耗普通遭遇队列、不生成战斗奖励。
- `RestAtCampfire` 仅在营火开放时合法，写权威 `Run.Hp = min(MaxHp, Hp + floor(3*MaxHp/10))`；满血可用。成功后相位2，重复请求被拒。
- `SmithAtCampfire(Card)` 的参数候选为可升级且未升级的长期牌实例。参数提交合并了选择与确认；未提交不消费选项。通过共享 `CardDefinitions.Upgrade` 增量升级所选实例，保留实例成长与费用修正。成功后相位2，不能再Rest。
- `BaseSpireRuntimeFactory.Describe` 的dev-map限制描述同步真实奖励、Boss延迟终局与营火接线，默认dev描述不变。

## 源依据与边界

- `sts2/src/Core/Entities/RestSite/HealRestSiteOption.cs:90-93`：最大生命的0.3m；`sts2/src/Core/Entities/Creatures/Creature.cs:477-490`：生命写入时取整并封顶。
- `sts2/src/Core/Entities/RestSite/SmithRestSiteOption.cs:29,49,56-74`：默认升级1张，有可升级牌才启用，可取消并手动确认；`sts2/src/Core/Commands/CardSelectCmd.cs:440-471`：过滤可升级牌，无随机自动选择。
- `sts2/src/Core/Multiplayer/Game/RestSiteSynchronizer.cs:123-152`：取消不消费选项，默认成功选项清空剩余选项。
- 生产修改局限于base SinglePlayer/Slice及相应检查；core冻结，未修改其实现。
- 当前只有已登记BurningBlood范围；营火修正遗物、额外选项、额外奖励及其hook不在此次行为验收内。

## 实际验证

1. `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests -- --boss-reward-only`：exit 0，实际3项。Boss有药水、Boss无药水、普通节点有药水；金币与卡牌不能绕过未解决奖励，Boss最后奖励才提交Victory，普通节点不提交。
2. 营火接线前完整 `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests`（pwsh-215）：exit 0，410项。该证据只包括Boss奖励边界，不含随后营火代码。
3. `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests -- --rest-site-only`（pwsh-216）：exit 0，实际2组。81最大生命/10当前生命→34；70→81；81→81；重复治疗拒绝且状态不变；Smith将受控成长Strike的20改为23，费用修正-1保留，同定义其他实例不升级。进入营火与两种选项均不增加随机抽数。
4. 营火后的串行门槛（pwsh-219）：完整 `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` exit 0，412项；`dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` exit 0，95项；`dotnet build AutoTheSpire.sln --no-restore` exit 0，0警告/0错误。前段Slice构建仍报告 `SourceMap/ActMap.cs:42,60` 和 `SpirePowerSource.cs:1216` 的3个既有nullable警告；末段solution为增量构建。
5. `python tools/doc-check/check_docs.py` 与 `git diff --check`（pwsh-220）：均exit 0，扫描337份Markdown、0问题；Git仅报告工作树已有LF/CRLF提示。

6. `dotnet run --no-build --project mod-the-spire/base-spire/BaseSpire.Slice.Tests -- --report .artifacts/t05-report`（pwsh-221）：exit 0，Slice412；行为报告 `behavior_verified_count=53`、`not_lowered=[]`。`not_lowered` 只表示自报验证集均已降级，不表示全部正式卡牌都有行为检查；输出中的正式源未支持样例仍包括 `WellLaidPlans` 的能力链接缺失及 `Toxic` 的敌人攻击语义误用，须按正式源语义分别处理。

`sts2/src/Core/Rooms/TreasureRoom.cs:34-48` 在进入时生成候选遗物；`sts2/src/Core/Nodes/Rooms/NTreasureRoom.cs:190-228,263-273` 开箱前禁止继续，开箱后才显示跳过。`sts2/src/Core/Multiplayer/Game/TreasureRoomRelicSynchronizer.cs:87-113,136-172,198-221` 从共享遗物袋按稀有度抽取，Skip提交null选择，不撤销奖励生成。`sts2/src/Core/Multiplayer/Game/OneOffSynchronizer.cs:130-142` 金币取玩家Rewards流 `NextInt(42,53)`，范围42..52。`sts2/src/Core/Models/RelicPools/SharedRelicPool.cs:13-135` 源池118项，解锁过滤见同文件138-161；正式实现须声明所用解锁配置。空宝箱仅由特定 `ShouldGenerateTreasure` hook允许；当前BurningBlood没有该hook。

正式 `SliceRelics.cs:45-57` 只有Starter BurningBlood，不能替换共享奖励池。真实宝箱需要权威遗物袋、候选奖励、开箱金币、生成/移除和获得/跳过行为。此处只读审计，未增加跳过空房、删第9行节点或用起始遗物替代；宝箱仍具名失败。

7. 地图路径边界：`StandardActMap.cs:268-274` 固定把宝箱写入 `GetRowCount() - 7`，当前物化地图输出为第9行；`MapRoomWiring.cs:49-57,500-507` 对宝箱具名失败。源边表从前部行向后部行推进，通往第16行Boss的正常路径必须经过第9行宝箱，不能通过选择另一条同层分支避开。因此真实固定seed Boss轨迹当前被宝箱房完整奖励依赖阻断，不是可用策略问题。

Boss奖励与营火检查都有明确受控初态标签；营火检查安排地图游标/HP/牌面，是机制判别，不能作为正常可达轨迹。既有清房结算检查直接安排HP，也已明确标注为受控。真实开局到Boss的固定seed合法轨迹、公共Host轨迹、参考双侧比较及跨幕尚未验证。宝箱/商店/精英/事件仍具名失败；剩余分类卡牌目标保持开放。
