# 按能力成组交付事件层

本轮把事件开发与某条正常地图路线解耦：直接使用生产事件目录进入并执行事件，补齐JungleMazeAdventure、MorphicGrove的全部选项。归属[ENV-T09房间与事件](../../tofix/README.md#env-t09)，服务于第一幕正常通关；没有关闭整局目标。

用户指出此前逐个追踪固定seed首错，导致工作在事件、敌人和公开协议之间跳转。现有代码已经分离问号房判定、源队列／资格和具体事件模块，本轮保留这些职责边界，改进的是实现组织和验证入口。Codex `/root`只修改事件模块、RunDeck多张选择、事件随机纯计算、工具验证及相关文档；未补敌人、改协议、改core／参考或恢复召唤／复活，未提交Git。

## 接续方式

下一轮从[主清单](../../tofix/README.md#env-t09)选择一个共享能力组，完整实现它的真实消费者和全部选项，再统一检查／接回正常Run。不要为了让某个seed多走一步而中途转去补另一领域。现有事件能力分组与源分母在本次交付时点为：

| 交付组 | 对应第一幕事件／依赖 |
| --- | --- |
| 资源与永久牌组选择 | AromaOfChaos、TabletOfTruth、SlipperyBridge已有完整选项；本轮增加JungleMazeAdventure、MorphicGrove完整选项 |
| 永久附着 | SapphireSeed、WoodCarvings、SelfHelpBook；共享附着状态、资格、生命周期和战斗副本保留须一起实现 |
| 诅咒获得／遗物与替换 | UnrestSite、Wellspring、LuminousChoir、ThisOrThat；既有SunkenStatue的SwordOfStone、Cheese的ChosenCheese也依此组 |
| 自定义奖励与事件内库存交互 | WhisperingHollow、BrainLeech、TheLegendsWereTrue、TheFutureOfPotions；奖励容器和领取／跳过／持有实例由共享操作完成 |
| 事件战斗 | DenseVegetation等；挂起、战斗结算及是否返回原事件不能假装普通Finish |
| 事件宠物与指定遗物 | ByrdonisNest、TeaMaster；资格和获得后生命周期需要真实内容支持 |

源第一幕队列仍为完整31身份；其中10项按源幕限制不可用，余21项包括当前注册的7个模块及尚未完成的内容。模块注册不等于所有分支已完成：SunkenStatue、RoomFullOfCheese仍保留明确失败分支。ByrdonisNest、LuminousChoir资格仍缺，不能以默认允许填补。当前状态及排期只维护主清单，以上为本次证据快照。

从仓库根目录执行以下命令可直接复查本轮模块，无需先完成地图和战斗：

```powershell
dotnet build tools/differential/Differential.csproj --artifacts-path .artifacts/event-layer-20261017
dotnet .artifacts/event-layer-20261017/bin/Differential/debug/Differential.dll event-modules --event MorphicGrove --output .audit/event-layer-20261017/morphic-final
```

预期18项通过、rc0。省略`--event`会执行全部7模块的进入检查与本轮完整行为检查；Jungle独行有具名参考差异，因此完整命令预期rc1，不是正常通关门槛替代品。工具用法见[独立事件模块验证](../../../../tools/differential/README.md#独立事件模块验证)，语义与源顺序见[事件契约](../../../design/mod-the-spire/event-domain/spec.md)。

## 生产改动

`JungleMazeAdventureEvent`在Enter一次抽出两笔float32波动，分别用于150／50金币，再按源decimal加法和整数截断缓存。JOIN_FORCES直接给缓存金币；SOLO_QUEST先保留三项视觉列表洗牌的两次事件流消费，再损失18生命、给金币并沿共享编译器关闭／死亡。致死余效不会丢失金币。

`MorphicGroveEvent`的LONER增加5最大生命并回复5；GROUP先失去全部金币，再通过一次Engine决策同时选择两张永久牌，按答案顺序逐张随机变换，新实例追加。`RunDeck.SelectMany`复用已有SelectOne、CanTransform和TransformToRandom；候选≤请求数自动处理全部，Engine快照使替换出来的新牌不会被再次选取。资格为金币≥100且可变换永久牌≥2，独立于效果注册。

新增的`EventFloatGoldAsset`只做纯数值换算，状态和随机消费仍由Engine IR拥有；没有运行时C#游戏状态或逐事件net分派。问号概率／源队列／地图编排未改。

## 独立访问与比较

`IsolatedEventSession`是tools层受控夹具，加载正式ContentSource与版本元数据，组合生产CardDefinitions、RunDeck、RunResources、EventModules和EventRoomCatalog。没有地图生成、敌人或Host依赖，初态随机消费为零。通过目录真实Enter初始化随机、预览和页面字段；没有每事件手写初始页的替代实现。既有Tablet／Cheese对照迁移到同一入口；Wood资格两例仍保留正式工厂／问号入口，作为独立的集成验证。

三组固定seed的完整模块检查有39项通过：7模块×3次进入，Morphic两分支及0／1／2／10张牌选择，Jungle合作资源／随机。Morphic检查一次两张的真实候选／顺序、重复答案拒绝且状态／随机不变、支付先于选择、未选同定义实例保留、自动选择不重复、100金币资格边界；永久结果和随机指纹由原生参考独立执行比较。

Jungle独行的存活／致死两种初态共6例：资源与原生结果一致，生产事件流4抽、参考2抽。参考源码已明确登记偏离#84，省略三项视觉列表洗牌的两次抽数。本轮没有修改参考或在其模型额外补抽；严格差异保存在`known_reference_divergences`并使完整命令rc1。另用独立源方法向量验证两笔NextFloat加三项Shuffle的最终指纹，不把向量证据记为严格原生通过。

## 集成验证与未验证

共同产物为`.artifacts/event-layer-20261017/`，证据为`.audit/event-layer-20261017/`及相同前缀日志。`event-content`原12组对照仍rc0，`event-modules --event MorphicGrove`18项rc0，完整`event-modules`为39passed＋6具名参考RNG偏离、rc1，`error=null`。

正常`normal-tablet`仍是38动作／39边界双侧通过；同一正式构建的公开Host重放55次提交／39边界通过，保留页面请求重放和旧观察拒绝。该轨迹证明本轮没有破坏已有正常事件／死亡闭环，不宣称新事件已经在正常地图和公开协议全程验收。Morphic的GROUP在Engine层保持真实二选，公开0.2多选缺口仍归T03。

solution构建rc0。Engine99、net421及Python12通过；严格regression、normal-tablet、normal-aroma、normal-post-event、normal-potion-capacity均rc0；regression为16双侧＋1单侧接线自检。原ironclad-act1仍1passed＋1fault、rc1，Phrog召唤暂缓原因不变。三种子／三幕act-transition仍rc0。完整Slice与文档结果在本报告收尾时补齐。

原`dev`规则／build保持`a1eb707babc8ae47`／`10734bae132a3f17`；`dev-map`随新事件IR迁移为`7a624ae870932789`／`c53ecd7f586c0206`；本轮`dev-run`build为`46a9b37e4f5af5c4`。参考HEAD仍为`2089659bd07920088b93226136c72b6b6ee590ad`且工作区干净。聚合身份、实际返回码与证据索引见`delivery-summary.json`；当前任务状态只回写主清单，历史身份不改写。
