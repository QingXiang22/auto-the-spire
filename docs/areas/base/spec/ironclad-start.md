# 纯 DSL 铁甲战士初态与开发战斗起手

文档类型：参考（Reference）。关联 CORE-AUTHOR-05／DSL-IRONCLAD-START-01；状态见[GitHub 任务清单](https://github.com/QingXiang22/auto-the-spire/issues/28)。适用于现有开发 scope，不建立正常 Run 标识。

## 来源和初态

`Characters/Ironclad.cs` 定义 80 最大／当前 HP、99 金币、有序 5 StrikeIronclad／4 DefendIronclad／1 Bash 和 BurningBlood。`Relics/BurningBlood.cs.AfterCombatVictory` 仅在胜利且持有者存活时治疗 6。来源目录的 SHA256 必须与本地源逐项核对，不更换来源条目总数。

`Ironclad.Initialize` 只在未选择角色、未建立永久牌组、未开始战斗且房间外的干净开发初态提供；已有真实卡、物品、敌人、Power、资源变化或抽牌后拒绝，不覆盖现有进度。清除三个开发占位卡，复用具名 Obtain 建立十张不同的永久实例，再获得起始遗物。`Player.CharacterId` 公开角色标识，`Player.AutoDrawPerTurn` 控制此角色的自动抽牌。重复初始化既无合法候选，直接调用也失败。

## 战斗与回合顺序

复用 BuildCombatDeck 建立带隐藏 PermanentSource 的独立副本；只对已初始化角色初始洗牌，使用原版布局的 base-spire:run:shuffle 流，随后 BeforeCombatStart。源 CardPile.RandomizeOrderInternal 不发 AfterShuffle，故此处也不发；TheAbacus 只在 DrawOne 所需的弃牌重洗时触发。初始洗牌使用源 UnstableShuffle，重洗另按源条目与升级排序后使用同一流；永久实例次序不变。布局、独立向量及未恢复的整局前置消费见[随机约定](randomness.md)。

回合重置／清格挡及早期 Power、能量和 side 钩子之后，使用 DrawCardsWithSource(owner,count,fromHandDraw=true) 正常抽 5，再发 AfterPlayerTurnStart 与 Late。首次抽牌先把 Innate 副本逐张移到顶部，至少抽 Innate 数量且至多十张；符合源 CombatManager.StartPlayerTurn 顺序。整次DrawCardsWithSource在入口征询一次来源资格，每张经DrawNextCard的满手、ending、重洗与真实抽牌事件；不能用直接入手冒充抽牌。手动开发 Draw 与卡牌、药水、遗物效果使用 fromHandDraw=false。

`ShouldDraw(value,owner,fromHandDraw)` 是纯资格查询；NoDraw 只否决持有者的效果抽牌，允许回合手牌；非活跃战斗不分派战斗监听，与原生 Hook 生命周期资格一致。`CanDrawCards(owner,fromHandDraw)` 复用该查询。公开 `Player.DrawBlocked` 表示效果抽牌查询被否决，不含满手、空牌堆或战斗结束条件；它是查询结果的发布值，不是独立的外部开关。战斗活跃状态改变及 Power 创建、改量、移除后通过 `RefreshDrawPermissions` 同步该值；新增依赖其他可变状态的资格监听时，也必须在其状态改变后刷新。刷新与资格查询不抽随机、不移动卡、不发布抽牌事件。

此语义替代旧 DrawBlocked 外部开关；开发检查应获得真实 NoDraw，不能设置公开值来控制抽牌。ShouldDraw 监听增加 fromHandDraw 参数，已有消费者须同步签名。锁定参考直接调用 Hook.ShouldDraw 获取对应状态；正常共同边界与受控 NoDraw／回合手牌检查分别报告。

BurningBlood 复用胜利治疗模板：关闭战斗后只给存活持有者治疗 6，封顶、熔化／移除失效，失败和手动终止不触发。无原版完整治疗修正参与者。第二场从同一永久实例重建新副本，临时状态不反写；生命／金币／遗物延续。

## 判别和边界

检查真实 5／4／1 元数据与独立实例、初始化拒绝、开战副本、初始洗牌不误发重洗事件、能量及抽牌先于玩家回合钩子、Innate 超过五张、NoDraw／满手、跨战隔离；胜利治疗封顶／失败不复活／熔化与移除不触发。公开入口必须实际观察十张永久牌、起始遗物、自动五张起手及隐藏来源不泄漏。

源 RNG 机制和已迁移消费者已恢复并有独立源向量；完整原版差异测试、Neow／地图／正常 Run、附着排序与全部手牌抽取修正仍未恢复；本批通过不关闭正常首幕、M01／M02 或角色完整环境合同。
