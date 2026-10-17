# ENV-T04 正常首战奖励闭环 — 2026-10-15

> 同日两场战斗接续：此前专项中的“怪物独立AI流”结论已撤回并修正为共享MonsterAi。首战两例仍在纠正后的生产状态通过；第二场新增证据与规则摘要以最新接续报告为准。本报告保留交付时点。

两条原seed已在同一语义动作计划下完成地图选点、真实首战、奖励领取或跳过、永久状态提交并返回可旅行地图，独立参考逐边界一致。参考sts2-sim全程只读。当前排期及完整完成条件见[主清单ENV-T04](README.md#env-t04)，本次不关闭多房间、公开完整轨迹或完整环境父项。

## 接手后先做

复用本包共同动作计划与真实奖励闭环，继续第二次正常选点和下一场战斗，比较长期HP、金币、长期牌到战斗副本来源及随机游标。不得换seed、注入清场或删除比较维度；尚需正常药水领取/容量、同Host公开轨迹及多房间证据。

## 修复与源依据

- 敌人格挡倍率只在自身持有相应正层数能力时生效。此前遍历已注册倍率声明就无条件应用，导致没有Frail的Nibbit获得5点格挡却仅留下3点。修复位于EnemyCompilation.EnemyBlockSteps，保留已有敌人Frail的10→7回归。
- 声明为时长衰减的能力降到零层时销毁实体，保持Powers关系与可见能力集合正确；此前只减层数，留下零层易伤。修复位于TurnCompilation的声明Tick分支。
- 参考适配器在出牌完整返回后调用公开CheckWinCondition，再进入原生ResolveOutcomeAsync，避免额外伪造EndTurn才能触发胜利。未修改参考源码或效果。
- 奖励流次数与指纹原本已相同，卡候选差异来自内容版本：Bloodletting改Uncommon、Taunt改Common、Dominate改Rare、Cruelty改Uncommon。SinglePlayerContentVersions显式选择当前参考2089659内容元数据，独立于随机布局；奖励/商店/生成牌共用定义，内容版本进入PublicBuildId。FrozenIronclad保留旧元数据。独立审计完整85项单人池的身份、顺序和普通/升级稀有度通过。
- 无药水掉落时reward.potion.resolved投影为true，与生产关闭守卫的实际语义一致。
- semantic_actions记录一条可见候选策略产生的动作计划；参考独立执行相同动作、牌槽和目标。提交前核对完整合法候选、可见牌定义及奖励候选，目标决策核对候选/数量/选择。错误候选、错误牌定义、错误目标均在提交前拒绝，不用参考首项替代。

## 实际验证

工作目录为仓库根。solution构建使用`.artifacts/env-t04-combat-final`，rc0；工具最终构建使用`.artifacts/env-t04-common-actions`，rc0。原有警告保留。

最终工具入口为`.artifacts/env-t04-common-actions/bin/Differential/debug/Differential.dll`：

| 检查 | 结果与证据 |
| --- | --- |
| `test --suite normal-reward-prefix --output .audit/env-t04-combat-continuation/delivery-normal` | rc0，2/2双侧passed，9项自检通过，含3项共同动作拒绝；[机器报告](../../../.audit/env-t04-combat-continuation/delivery-normal/regression.json) |
| `test --suite regression --output .audit/env-t04-combat-continuation/common-regression` | rc0，17执行，16双侧passed、1工厂自检，22项比较器自检通过；[机器报告](../../../.audit/env-t04-combat-continuation/common-regression/regression.json) |
| v109-rng及奖励池审计 | 6个seed、2例怪物独立流及完整单人奖励池通过；[报告](../../../.audit/env-t04-combat-continuation/final-rng/v109-rng.json)，该专项工具构建早于共同动作接线，生产行为与最终状态相同 |
| Slice runtime | rc0，841项；[日志](../../../.audit/env-t04-combat-continuation/slice.log) |
| net | rc0，418项；[日志](../../../.audit/env-t04-combat-continuation/net.log) |
| 无参考构建 | `dotnet build tools/differential/Differential.csproj --artifacts-path .artifacts/env-t04-no-reference -p:ReferenceSimAvailable=false` rc0 |

领取路线12个稳定边界，双方最终68HP、119金币、11张长期牌；跳过路线17个边界，双方最终76HP、117金币、10张长期牌。两例都回到travel-ready，保留真实合法后继；领取例没有掉落药水，跳过例掉落AttackPotion并真实跳过，因此不证明药水领取/满容量处理已双侧验收。

规则修复改变历史配置规则摘要，已核对行为证据后迁移SliceMapRoomChecks基线：dev=d28e057cd3eed262/4e6d9def8d345c49；dev-map=ea03f6e9a26fef27/10f50b80dfef7c96。此为实际规则修复，不是通过抄摘要替代行为验证。

文档检查扫描517篇、0问题，git diff --check通过，参考git status --porcelain为空。本包为接手自查，没有独立审阅者验收。未修改core或参考、未提交、未恢复远端会话或训练。目标继续保持active。
