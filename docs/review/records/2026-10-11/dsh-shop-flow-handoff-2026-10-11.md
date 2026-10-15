# ENV-T09 商店流程（隔离机制包）交接 — 2026-10-11

- task_id: `ENV-T09-shop-flow-parallel-20261011`
- dispatch_record: [.audit/dsh-shop-flow-2026-10-11/task-record.json](../../../../.audit/dsh-shop-flow-2026-10-11/task-record.json)
- session_id: `session-7fab171b-624d-4018-8a74-2c97e4fc8959`
- request_id: `9b58442d-a81c-449f-b56e-8792ec191ec3`
- delivery_status: `待验证`（本包执行结果与证据见下；主清单由 Codex 单写）
- updated_at: `2026-10-11T00:00:00+08:00`

接手第一步：读[验证记录](../../../../.audit/dsh-shop-flow-2026-10-11/verification.md)（含两条现场命令、
冻结二进制哈希、被测文件哈希、13 条检查的判别点、抽数对账），再按下面《正式整合清单》接三处，
最后重跑同一条验证命令复核。**本包不修改任何既有文件**：全部新增物都在
`mod-the-spire/base-spire/workpacks/shop-flow/`，公共注册与 `MapPhase` 扩界留给 Codex 串行收口。

## 这一包交付什么

商店是 `sts2/src/Core/Rooms/MerchantRoom.cs` + `Core/Entities/Merchant/*` 的**完整隔离机制包**：
真实货架生成（源顺序与源抽数顺序）、源价格与折扣、购买写回、删牌、离店，全部落在 Engine 类型化存储里，
没有第二个调度器、没有 C# 可变 RunState。

| 新增物 | 作用 |
| --- | --- |
| [ShopRoomWiring.cs.frozen-20261011](../../../../mod-the-spire/base-spire/workpacks/shop-flow/ShopRoomWiring.cs.frozen-20261011) | 生产模块：`ShopOffer` 货架实体 + 14 格生成 + 5 个动作 + 4 个过程（**已迁移进 `BaseSpire/Slice/ShopRoomWiring.cs`，此处为冻结留档**） |
| [ShopCompute.cs.frozen-20261011](../../../../mod-the-spire/base-spire/workpacks/shop-flow/ShopCompute.cs.frozen-20261011) | 纯计算条目（9 个资源/Entry）：稀有度、池计数/选取、价格、升级 roll、遗物稀有度/价格、药水稀有度/价格（**已迁移进 `BaseSpire/Slice/ShopCompute.cs`**） |
| [ShopFlowChecks/](../../../../mod-the-spire/base-spire/workpacks/shop-flow/ShopFlowChecks) | 独立验证工程：只编译上面两个文件，引用冻结二进制，跑真实 Engine 会话（现行检查见 `BaseSpire.Slice.Tests/ShopFlowChecks.cs`） |
| [source-facts.md](../../../../.audit/dsh-shop-flow-2026-10-11/source-facts.md) | 源行号与逐字常量（流、阈值、价格、库存顺序、购买写回、离店、选卡细节） |
| [verification.md](../../../../.audit/dsh-shop-flow-2026-10-11/verification.md) | 命令/返回码/哈希/13 条检查判别点/抽数对账 |

机制与源的对应（逐条依据见 source-facts.md）：

- 商品顺序：`Shops.NextInt(5)` 抽折扣格 → 5 张彩卡（`_coloredCardTypes = [Attack,Attack,Skill,Skill,Power]`）
  → 2 张无色（固定 uncommon/rare，无稀有度 roll）→ 3 件遗物（**先**抽两次稀有度，第三格固定 `Shop`）
  → 3 瓶药水（先 3×(稀有度 + pick)，再 3 次价格抽）→ 1 个删牌位。
- 彩卡抽数：`Rewards` 稀有度 roll（`0.09/0.37 + 持久 offset`，**不回写**）→ `Shops` 选卡 →
  `Rewards` 升级 roll（源恒不升级但无条件消费一笔）→ `Shops` 价格 →（在售格 `SetOnSale()` 再抽一次价格并整除 2）。
- 价格：彩卡 150/75/50 ×[0.95,1.05)；无色再 ×1.15（`RoundToInt`）；遗物 175/225/275/200 ×[0.85,1.15)
  （Ancient/Event/Starter = 999999999，回退遗物 Circlet = 1）；药水 100/75/50 ×[0.95,1.05)。
- 遗物取货：源 `RelicFactory.PullNextRelicFromBack` + `RelicGrabBag.GetAvailableDeque` 的
  `Shop→Common→Uncommon→Rare→None` 回退链，**袋尾取货**，全空回退 `base-spire:circlet`。
- 购买顺序照源：彩卡先建牌后扣金；遗物先扣金再建持有实例并执行 `AfterObtained` 效果
  （`GainMaxHp` 加 MaxHp 并回同样多的血、`GainGold` 加金币）；药水先查金 → 查空栏 → 建药水 → 扣金；
  删牌查金 → 扣金 → 删掉**被选中的那个实例** → `ShopCardRemovalsUsed += 1`。
- 删牌价 = `75 + 25 × ShopCardRemovalsUsed`（进店时算好，无抽数）；一次一间只能删一张。

## 模块与接缝（Codex 要接的三处）

模块通过四个小 record 消费宿主符号，构造函数签名：

```csharp
new ShopRoomWiring(package, runSeam, mapSeam, cardSeam, relicSeam, potions, backendId = BaseSpireComputeBackend.BackendId)
```

| 接缝 | 传什么 |
| --- | --- |
| `ShopRunSeam(RunType, Runs, Gold, Hp, MaxHp, CardRarityOffset?, View?)` | `CombatSlicePackage` 已有的 `runEntity` / `RunStates` / `Gold` / `Hp` / `MaxHp`；`CardRarityOffset` 传**奖励侧持久赔率字段**（`RewardWiring` 的 `cardOdds`），不传则按 0 处理 |
| `ShopMapSeam(MapPhase, CurrentNode, NodeType, PointType, ShopPhase, TravelPhase)` | `MapRoomWiring.RunMapPhase` / `RunCurrentNode` / `NodeType` / `PointType`；`ShopPhase` 传**新的相位值 7**，`TravelPhase` 传 2 |
| `ShopCardSeam(CardType, LongTermDeck, Cards, CardFields)` | `cardSchema.Type` / `LongTermDeck` / `cardDefinitions.Compiled`；`CardFields` 直接传 `cardDefinitions.New`（方法组即可） |
| `ShopRelicSeam(RelicType, Definition, Rarity, Owned, BagEntryId, PlayerBags, SharedBags?, Library)` | `Relic` 实体与其 `DefinitionId`/`Rarity`、`Relics` 集合、袋成员 id 字段（`RunInitializationWiring.SharedId`）、`RunInitializationWiring.PlayerBags`、`SharedBags`、`SpireRelics.All` |

模块对外只多声明一个 Run 字段 `ShopCardRemovalsUsed`（Int 0..999）和一个集合 `ShopOffers`
（`ShopOffer` 实体，容量 16，字段 `Slot/Kind/DefinitionId/Rarity/PoolKind/PoolIndex/OnSale/Price/Sold/Unsupported/UnsupportedReason`）。
`Unsupported` + `UnsupportedReason` 是**可观察的裁决位**：没有正式效果声明的商品照源仍出现在货架上，
但被明确标成不可买，购买会被具名拒绝——不写空效果假装可买。

## 已验证（真实 Engine 会话 + 冻结 0.4 二进制）

```powershell
dotnet build mod-the-spire/base-spire/workpacks/shop-flow/ShopFlowChecks/ShopFlowChecks.csproj `
  -c Release --artifacts-path .artifacts/parallel/dsh-shop-flow     # 已成功生成（0 warning / 0 error）
dotnet .artifacts/parallel/dsh-shop-flow/bin/ShopFlowChecks/release/ShopFlowChecks.dll
# exit code 0；passed=13 failed=0
```

覆盖的判别行为：库存 14 格源顺序与彩卡类型序列、彩卡按 identity 不重复、无色固定稀有度、唯一折扣格、
彩卡/无色/遗物/药水价格落在源区间、首张彩卡稀有度可被独立重放、金币不足状态完全不变、买卡/买遗物/
买药水的真实写回、未登记效果的商品被明确拒绝、满药水栏拒绝、删牌删对同定义中的那一个且下一间涨价、
离店回地图相位、离店后与战斗相位都不能购买、重复进店货架重建。

冻结输入：`Core.TheSpire.dll` SHA256 `2EC0B3F9…DCC317`、`BaseSpire.dll` SHA256 `D499BB1C…896BBE`
（`.artifacts/parallel/dsh-core-author-04/bin/BaseSpire/debug/`，未复制回源码树、未写回该目录）。

## 未覆盖 / 内容依赖（交 owner 裁决）

1. **药水池只有 2 瓶**（`rare=1, uncommon=1`，可用 2 瓶），而商店药水按源会以 `0.1/0.35` 的阈值先掷
   稀有度再从该稀有度的桶里取：`common` 桶为空，因此掷到 `common` 的药水格都是**退化格**
   （`Unsupported` + 原因串，不抽那笔 pick；`SHOP-STREAMS-20261011` 上 3 格里有 2 格）。
   源侧在同样的内容下会抛异常，因此这是**内容缺口**而不是模块缺陷；补药水内容后同一条检查自动回到
   正常路径（抽数 28）。需要 owner 决定补内容还是先接受退化格。
2. **无色池没有源顺序清单**：`IroncladRewardPool.SourceOrder` 有对应表，无色池只有内容声明序。
   模块按声明序建池并在构造期把并集下标限制在既有口径（≤ 255）内，但"同一随机数取到哪张无色牌"
   会与源不一致。需要补一份 `ColorlessCardPool.GenerateAllCards()` 顺序清单 + 对账。
3. **遗物效果内容**：`SharedRelicPoolManifest` 只有 117 件遗物的身份与稀有度，`SpireRelics.All` 只有
   4 件（burning-blood/pear/mango/old-coin）。因此源清单袋里绝大多数遗物格被标成 `Unsupported`。
   这是"不规范省略、不写空效果"的直接后果；需要 owner 决定遗物效果内容的补齐顺序。
4. 未建模（都有源依据，均在本包外）：遗物 `IsAllowedInShops` 与"已见"过滤、`Hook.ModifyMerchantPrice`
   折扣（MembershipCard ×0.5 / TheCourier ×0.8）、`Hook.ShouldRefillMerchantEntry` 补货、
   `ModifyMerchantCardCreationResults` 升级（MoltenEgg 等）、`FailureSpace`（牌位上限；
   `LongTermDeck` 现在无界）、删牌的 `IsRemovable`/Eternal 过滤与诅咒排序、
   遗物级 `MerchantCost` 覆盖、`MarkRelicAsSeen` 与 `BoughtColorless|Relics|Potions` 历史、
   `MerchantRoom.Exit` 的地图点历史写入、飞升删牌价变体（100 + 50n）。
5. **没有**跑正式 `--scope dev-map` 公开整局、**没有**双侧/公开协议对拍：`MapRoomWiring` 目前把
   `Shop` 房型判为未实现并具名 `Fail`，公共运行器会在该节点停成 `unsupported_map_room`
   （见 [T03 交接](../../tofix/dsh-public-run-driver-handoff-2026-10-11.md) 的实测记录）。本包的验证是自带最小宿主的
   真实 Engine 会话，**不构成**官方 Run 或整局通过的声明。

## 正式整合清单（Codex 收口，按顺序）

1. **计算后端委派**：在 `BaseSpireComputeBackend.Execute` 里加一行
   `if (ShopCompute.TryExecute(asset.Entry, inputs, outputs)) return;`（模块的 9 个资产都用同一个
   `BackendId`，按 `asset.Entry` 分派）。
2. **MapPhase 扩界**：`MapRoomWiring.RunMapPhase` 的上界从 `Int(0, ActCompletedPhase=6)` 抬到 7，
   并加常量 `ShopPhase = 7`；**不能用 6**——6 等于 `ActCompletedPhase`，会让 `ContinueToNextAct`
   在店内合法。
3. **组包**：在 `CombatSlicePackage.Build` 里 `rewards` 之后构造 `ShopRoomWiring` 并
   `shop.RegisterActions(package)`；把 `projection.Type(shop.Offer.Type)`（也可以直接把 `View` 传进
   `ShopRunSeam`）与 `shop.Offer` 字段暴露给公开视图。
4. **地图分支**：在 `MapRoomWiring.RegisterChooseMapPoint` 的房型分支里加 `Shop`：
   置 `RunCurrentNode` 后调用 `shop.Enter(body, run)`（`Enter` 自己会把相位置成 `ShopPhase`、
   清掉上一轮货架并生成 14 格）；离店由 `LeaveShop` 把相位置回 `TravelReadyPhase`。
5. **奖励赔率接缝**：把 `RewardWiring` 的持久卡稀有度 offset 字段传进 `ShopRunSeam.CardRarityOffset`
   （源的商店稀有度用同一个 `CardRarityOdds`，且**不回写**）。
6. 上面第 1–2 条内容依赖（药水池稀有度覆盖、无色池源顺序清单、遗物效果登记）由 owner 排期；
   三条都属于**内容**，不需要再改模块。

## 一处引擎行为，供 Codex 参考

`Candidates(...)` 的查询结果为空**不会**让动作从 `LegalActions` 里消失：售罄后
`BuyShopCard`/`RemoveShopCard` 仍可被提交（本轮实测 `Domains` 里只剩另一个参数的域）。
因此本模块的"售出/不可买/金币不足/满栏"全部在动作体里用具名 `Fail` 显式拦截，
而不是只依赖候选查询——`EngineFaultCode.SessionStateInvalid` + 消息可被上层区分。
注意：一次 `Fail` 之后该会话即进入 faulted 状态，验证程序因此把"售罄后再买"放在各自场景的末尾。

## 文档与仓库检查

新增：本交接 + [验证记录](../../../../.audit/dsh-shop-flow-2026-10-11/verification.md)
+ [验证输出](../../../../.audit/dsh-shop-flow-2026-10-11/verification-run.txt)（另有先前的
[source-facts.md](../../../../.audit/dsh-shop-flow-2026-10-11/source-facts.md)）。

实际检查结果：`python -X utf8 tools/doc-check/check_docs.py` → 扫描 400 个 Markdown，报 **4 个既有失败**
（`dsh-power-fixture-handoff-2026-10-11.md` 与其被引用的 `dsh-startup-alignment-handoff-2026-10-11-f68b943d.md`
里指向行锚点的链接），本包新增/改动的三份文档**没有**报告问题；`git diff --check` → 干净（exit 0）。

未提交、未 push、未 reset/clean；未修改主清单、未在 BaseSpire 源码树里新增或改动任何 `.cs`、
未改 solution/csproj/共享 MapPhase 常量/工厂/网络/工具。
