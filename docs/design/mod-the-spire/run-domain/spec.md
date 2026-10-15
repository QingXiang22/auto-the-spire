# 整局公共操作契约

归属ENV-T09资源/遗物和ENV-T06永久牌组，排期只查[主清单](../../../review/tofix/README.md)。这些编译期领域操作生成Engine IR，所有可变状态仍在Engine；组包一次提供相同资源能力给各来源，不在内容叶建立默认空hook的副本。

| 操作 | 前置条件与顺序 | 范围及依据 |
| --- | --- | --- |
| GainGold | 输入快照一次，Modify→AfterModifying→正值才写金币→AfterGained | 沿用[资源契约](../event-domain/spec.md)，奖励/宝箱/遗物共用同一服务 |
| LoseGold | 非负输入快照一次，金币最低0；购买调用方先校验够钱 | PlayerCmd.LoseGold；当前不建地图历史，Spent/Lost/Stolen历史分类仍未支持，不能以负GainGold实现 |
| HealLivingPlayer | 非负输入快照一次，存活且回复最多MaxHp | 营火保留源回复量计算；不实现复活或未登记治疗参与者 |
| GainMaxHp | 非负输入快照一次，先增加MaxHp，再按实际增量回复且封顶 | CreatureCmd.GainMaxHp；当前整数范围和存活玩家，无上限修正参与者 |
| ObtainRelic | 解析受支持身份→创建持有实例→移除玩家/共享袋同身份→按声明顺序AfterObtained | RelicCmd.Obtain；支持现有不可叠加声明，未知效果明确失败，不伪造成功；抽取、商品支付及访问收尾由来源拥有 |
| AcquireCard / CreateCard / RemoveCard | 已有实例保留身份、加入永久牌组；新实例用CardDefinitions配方；移除前校验永久成员、BeforeRemoved→移除状态；获得AfterChangedPiles在入组后 | CardPileCmd.Add/RemoveFromDeck；当前支持内容无永久获得替换参与者，替换、历史和全部hook参与者仍未支持，不用战斗移动代替 |
| AcquirePotion | `PotionInventoryRules.Acquire(scope, potion, source)`捕获实例，选择首个空槽；有容量、非已持有且ShouldAcquire允许时才写槽位/移出来源/按槽序入栏/AfterAcquired | PotionCmd.TryToProcure默认slotIndex=-1；满栏或拒绝不变来源/槽位、不发获得hook、不抽随机。source可省略，创建及失败实物清理由来源负责；奖励Acquire(scope)适配相同操作，商店不再借用RewardPotions暂存。指定槽位、全部参与者及历史仍未支持 |

遗物池抽取不属于Obtain：商店生成商品、宝箱进入已消耗相应袋项，不改变随机流或将生成挪到领取。已不存在的袋项移除为无操作。受支持身份名单与获得实现使用同一判据，避免商店说可买但获得时无效果。

永久牌组的BeforeRemoved/AfterChangedPiles编译接缝作用于当前实例，默认空仅适用当前内容。奖励候选集合不是战斗牌堆；领取时移出候选后获得，未选候选由奖励模块清理。商店保留源购买/删牌确认和扣款顺序。

判别复用正式房间检查，另覆盖资源输入只读一次、共享金币hook穿透遗物、获得hook观察已完成袋移除、永久牌组实例身份与移除前hook。未知遗物领取显式失败，正常买不起/满栏由Requires拒绝，不改会话状态。
