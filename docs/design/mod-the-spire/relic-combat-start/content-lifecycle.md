# 遗物内容接续与生命周期

关联 CORE-AUTHOR-05／DSL-RELIC-CONTENT-01；当前状态见[主清单](../../../review/tofix/README.md#当前交付)。本契约覆盖纯DSL当前单人开发范围，遗物来源目录保留297项；已有定义、行为见证、独立比较与正常Run分开结算。

## 内容与源依据

源为sts2的Models/Relics中相应类，原始SHA256与relics/catalog.json逐项核对。Pear最大生命+10、Mango+14；LeesWaffle最大生命+7后回满，FakeLeesWaffle治疗最大生命10%向下取整；OldCoin获得300金币。BlackBlood胜利治疗12；CaptainsWheel第三回合清格挡后得18无加成格挡。BagOfPreparation首回合基础手牌抽取+2，与Innate数量取max而非多发两次抽牌；RegalPillow营火基础治疗+15后封顶。

Nunchaku每10次攻击打完+1能量，实例计数跨回合／跨战保留。Kunai／Shuriken每回合每3次攻击打完分别+1敏捷／力量；OrnamentalFan同样计数得4无加成格挡；LetterOpener每回合每3次技能打完对所有存活敌人造成5无加成伤害。重复／自动出牌各计实际次数，多段攻击只计一次，拥有者不同不串计数。每回合计数在开场及玩家回合开始的早期站点重置，不能在AfterPlayerTurnStart才重置而吞掉自动打出。StoneCalendar第7个玩家回合结束前对存活敌人造成52无加成伤害。CentennialPuzzle本场第一次正的未挡伤害后抽3，先标已用再抽，下一战恢复；零伤害／全挡不触发。

## 状态、接口与顺序

所有局内状态在Engine实体。Relic.Counter保持独立实例计数，OncePerCombat的使用标志与能力元数据归Relic字段。提供pure HasActiveRelic(owner,definition)与共用有效实例筛选；修正参与者由模板在实例上配置，不在Host按ID分派。

拾取效果在入Relics后发RelicObtained时执行，不能只放开发Obtain动作；模板拾取订阅按本次relic过滤，旧Strawberry迁移到相同接缝，不重复奖励。永久最大生命不会在移除时撤回，治疗和金币不能因移除反向扣除。OldCoin源商店／幕资格保留在来源说明，当前显式开发获得不等于正常池资格已恢复。BlackBlood当前只交付持有效果，不宣称先古之民升级替换BurningBlood已接入。

新增BeforePlayerTurnStart在回合编号增加后、抽牌／自动出牌前发出，供当回合计数重置。新增BeforePlayerTurnEnd在回合末自动出牌后、弃牌前发出，供StoneCalendar；完成当前群体伤害后沿现有胜负判定，终局后不得继续敌方行动。EndDevelopmentCombat手动终止不发胜利治疗。

CentennialPuzzle消费已支持的PlayerHpLost站点，要求存活且战斗活跃，先写本实例已用再DrawCards；来源为当前未挡伤害／牌源掉血，未知伤害修正与死亡防止未额外承诺。移除／熔化使后续触发和动态手牌／营火修正失效；每次获得新实例从默认计数开始。

## 判别与边界

逐件真实获得／触发的数值见证；拾取通过直接ObtainRelic也执行一次；非首次／非法重复／去除后的负例；同定义不同实例／不同持有者隔离；每3次／第10次触发、单回合6次、跨回合重置与跨战保留差别；自动与重复计数、多段只计1；最后击杀后收尾、StoneCalendar群体击杀不提前漏敌人；Bag与Innate／NoDraw／满手，Puzzle在首次伤害后立即抽牌且无递归重触发；RegalPillow封顶与失效。公开真实stdio验证一组新增拾取与一种计数消费者，覆盖门槛要求所有新增definition有实际场景。

玩法全部由base DSL执行，不恢复参考玩法。内容组实际加载触及原256个模板实例上限，必要通用维护将总量增至8192；32层深度、递归与4096个展开成员限制保持，内核回归验证300个真实实例。独立原版两侧未执行保持未验证。完整获得修正、随机遗物来源／资格、升级替换、死亡防止、其他遗物与正常Run仍待恢复，不注册空效果补数量。
