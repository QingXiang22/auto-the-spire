# 药水生成与栏位约定

ENV-T05-potion-flow-20261011 的机制规格；状态、认领与排期见[行为要求](../../environment-completion-and-differential-v1.md#env-t05)。

## 行为与边界

- 正式 Ironclad、全解锁、单人池按源角色池的 BloodPotion / SoldiersStew / Ashwater，随后 SharedPotionPool 的 45 项排列。未实现使用效果的成员仍参与生成，保留真实 ID、稀有度、用法和目标，并公开未支持原因；禁止空效果或缩池。
- 正常战斗开奖依次掉落判定、金币、若掉落则稀有度和池内下标、三张卡候选。药水实际生成消费两笔 Player.Rewards，不补抽。
- PotionInventoryRules 拥有类型、集合、实例初值、栏位分配、奖励生成及丢弃。所有可变状态在 Engine，正式效果复用已有逐效果程序和编译器；规则包构建入口只负责连接目标选择、共享结算及动作结束处理。
- 栏位容量三，SlotIndex 为 0..2，奖励候选为 -1。消耗或丢弃留下空槽，不移动其余瓶的槽号。领取使用第一空槽，保持同一实例。
- 满栏时不能领取，奖励保留；玩家先 DiscardPotion 再 TakeRewardPotion，或者 SkipRewardPotion。源没有自动替换选择器，取消丢弃等同于不提交动作。
- 支持的战斗药水不付能量；敌人目标先选目标，再销毁库存实例、执行效果，最后运行 BattleEndTail。效果内部的选牌暂停时药水已经消耗，取消效果选择不返还瓶子。未支持效果不进入 UsePotion 候选，避免空用后消耗。奖励关闭后才能继续地图。
- AttackPotion复用生成候选能力：完整Ironclad攻击生成池洗牌取三张，允许选择一张或取消；所选牌本回合免费，满手进入弃牌堆，未选候选清理。准确语法与随机/实例约定见[生成候选](../card-primitives/spec.md#生成候选并选择env-t06-pr05)。当前单人目标绑定自己，其余角色池尚未注册。

## 源依据

`sts2/src/Core/Factories/PotionFactory.cs`、`Models/PotionPools/SharedPotionPool.cs`、`Timeline/Epochs/Ironclad4Epoch.cs`、`Entities/Players/Player.cs`、`Commands/PotionCmd.cs`、`Rewards/PotionReward.cs` 及 `Models/Potions/*.cs`。稀有度为 NextFloat <= 0.1 rare、<= 0.35 uncommon、其余 common，随后同稀有度成员中的 NextItem；AnyPlayer 在本单人范围绑定自己。

## 完成条件

精英奖励保持实际Elite房型：药水掉落阈值加入源eliteBonus×0.5，金币35～45，卡牌按Elite的0.5/0.4/0.1基础稀有度与现有offset滚动；随后用Player.Rewards抽遗物稀有度并从玩家袋前端取真实标识，同时移出共享袋。遗物必须领取或明确跳过后才能关闭整组奖励；未实现遗物效果不得伪装获得。满栏策略不使用精英普通化或省略遗物制造通过。

正式源实际加载及池顺序/48项元数据对账；生成11/13笔随机时序；真实领取、满栏保留、丢弃再领取、空槽不挤压、跨房间保留、目标使用/消耗/末次击杀、跳过后奖励关闭的针对性检查；正常公开路径领取与使用验证记录。独立原版双侧验证记录与尚未实现的药水效果分别记录，本包不关闭全部 ENV-T05。
