# 整局公共操作契约

归属ENV-T09资源/遗物和ENV-T06永久牌组，排期只查[主清单](../../../review/tofix/README.md)。这些编译期领域操作生成Engine IR，所有可变状态仍在Engine；规则包构建一次提供相同资源能力给各来源，不在内容叶建立默认空hook的副本。

| 操作 | 前置条件与顺序 | 范围及依据 |
| --- | --- | --- |
| GainGold | 输入快照一次，Modify→AfterModifying→正值才写金币→AfterGained | 沿用[资源契约](../event-domain/spec.md)，奖励/宝箱/遗物共用同一服务 |
| LoseGold | 非负输入快照一次，金币最低0；购买调用方先校验够钱 | PlayerCmd.LoseGold；当前不建地图历史，Spent/Lost/Stolen历史分类仍未支持，不能以负GainGold实现 |
| HealLivingPlayer | 非负输入快照一次，存活且回复最多MaxHp | 营火保留源回复量计算；不实现复活或未登记治疗参与者 |
| GainMaxHp | 非负输入快照一次，先增加MaxHp，再按实际增量回复且封顶 | CreatureCmd.GainMaxHp；当前整数范围和存活玩家，无上限修正参与者 |
| LoseMaxHp | 非负输入快照一次，先损失超过新上限的当前生命，再将MaxHp最低设为1 | CreatureCmd.LoseMaxHp非卡牌来源；复用脱战伤害。历史、全部修正及死亡阻止参与者仍未完整支持，TabletOfTruth的致死选择另按源显式击杀 |
| ObtainRelic | 解析受支持身份→创建持有实例→移除玩家/共享袋同身份→按声明顺序AfterObtained | RelicCmd.Obtain；支持现有不可叠加声明，未知效果明确失败，不伪造成功；抽取、商品支付及访问收尾由来源拥有 |
| AcquireCard / CreateCard / RemoveCard | 已有实例保留身份、加入永久牌组；新实例用CardDefinitions配方；移除前校验永久成员、BeforeRemoved→移除状态；获得AfterChangedPiles在入组后 | CardPileCmd.Add/RemoveFromDeck；当前支持内容无永久获得替换参与者，替换、历史和全部hook参与者仍未支持，不用战斗移动代替 |
| AcquirePotion | `PotionInventoryRules.Acquire(scope, potion, source)`捕获实例，选择首个空槽；有容量、非已持有且ShouldAcquire允许时才写槽位/移出来源/按槽序入栏/AfterAcquired | PotionCmd.TryToProcure默认slotIndex=-1；满栏或拒绝不变来源/槽位、不发获得hook、不抽随机。source可省略，创建及失败实物清理由来源负责；奖励Acquire(scope)适配相同操作，商店不再借用RewardPotions暂存。指定槽位、全部参与者及历史仍未支持 |

遗物池抽取不属于Obtain：商店生成商品、宝箱进入已消耗相应袋项，不改变随机流或将生成挪到领取。已不存在的袋项移除为无操作。受支持身份名单与获得实现使用同一判据，避免商店说可买但获得时无效果。

永久牌组的BeforeRemoved/AfterChangedPiles编译接口作用于当前实例，默认空仅适用当前内容。奖励候选集合不是战斗牌堆；领取时移出候选后获得，未选候选由奖励模块清理。商店保留源购买/删牌确认和扣款顺序。

验证复用正式房间检查，另覆盖资源输入只读一次、共享金币hook穿透遗物、获得hook观察已完成袋移除、永久牌组实例身份与移除前hook。未知遗物领取显式失败，正常买不起/满栏由Requires拒绝，不改会话状态。

## 永久牌组选择与变换

`RunDeck`提供单张永久牌组升级/变换选择：候选保持长期牌组顺序，空集合无选择、唯一候选自动执行，多候选由Engine暂停并选一张；恢复后才升级或抽取变换结果。升级复用CardDefinitions并保留实例。AromaOfChaos两个源选项均保留，选择结束后由共享事件访问层关闭。

多张选取复用同一领域操作：候选数不超过请求数量时直接按原候选顺序处理全部，否则用一次Engine EntityRefList决策返回恰好所需数量。自动处理的候选在执行前快照；变换追加的新实例不能再进入同一次自动选取。手动答案顺序保留，选择域、数量与重复实例由Engine校验。MorphicGrove是同时选择两张的真实消费者。

变换使用原牌所属完整源池、排除原定义、过滤单人资格及源稀有度；当前支持Ironclad普通池来源，其他未支持池明确失败，不用已实现子集替代。AromaOfChaos使用自己的事件Rng，一次有界抽取，不使用奖励流、不额外滚稀有度或升级。新牌为普通形态的新实例，旧牌退出永久牌组，新牌按源Deck规则追加末尾，牌数保持；不继承旧升级/临时费用。后续获得替换、附着和变换参与者须显式接入，不能将此窄范围写成全量Transform已完成。

源码依据为CardSelectCmd.FromDeckForUpgrade/FromDeckForTransformation、CardFactory.GetDefaultTransformationOptions与CardCmd.Transform；永久Deck变换追加末尾，其他战斗牌区才保留旧下标。普通移除的BeforeRemoved不等于变换的AfterTransformedFrom/To，不能混用生命周期。

## 战斗副本来源与结算写回

正常对拍在各侧分别保留永久实例的首次出现编号，删除后不复用。每个战斗牌区逐实例记录永久来源：base读取LongTermSource，独立参考读取DeckVersion；生成的临时牌标为temporary，诊断来源不发布到net或策略输入。同定义不同来源必须保持可区分，不按当前卡名重匹配。

事件后第三场的严格用例要求：当前永久牌每实例恰有一个战斗副本；实际打出事件升级或变换得到的实例；战斗结束后事件前已存在的永久实例和形态保持，奖励新增牌另计。策略只在事件后优先公开Power牌，其余沿原可见候选规则，不能用隐藏来源选择动作。此证据不替代Feed上限跨房、最后击杀余效和全部永久写回矩阵验收。


## 有序永久牌组的随机选择

RunDeck.SelectRandom接收有序候选Query、源流和选中实例回调。非空候选恰好一次有界抽取，保持原牌组顺序定位同一实例，回调在选择完成后执行；空域明确失败。CanRemove按当前已支持内容的Eternal资格过滤，HasRarity按实际普通/升级形态判别。动态关键字及附着资格仍依其机制契约，不能借静态元数据宣称完成全部修正参与者。
