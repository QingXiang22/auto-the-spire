# ENV-T09.2 商店正式集成交接 — 2026-10-11 wave

- task_id: `ENV-T09-shop-integration-20261011`
- dispatch_id: 派发记录未给单独字段；以 `url` + `request_id` 作为本包派发标识
- session_id: `session-476a4e3f-6c45-4b2c-974c-91c215c7da85`
- request_id: `ab1bbac0-dd04-4975-b16c-3a0ca57d65ce`
- dispatch_record: [.audit/dsh-shop-integration-20261011/task-record.json](../../../../.audit/dsh-shop-integration-20261011/task-record.json)、[派单正文](../../../../.audit/dsh-shop-integration-20261011/task.txt)
- delivery_status: `ready_for_review`
- updated_at: `2026-10-11 23:58 +08:00`

> `ready_for_review` 不等于验收：本包不关闭父项 ENV-T09、不启动下一包。主清单由 Codex 单写；
> 本文件是本包唯一交接，进度/问题都写在这里。

## 第一步（接手先做）

1. 读本节与《已验证》《未执行 / 受阻》《首差》，确认三件事：`dev-map` 现在**从正式地图 `ChooseMapPoint`
   的 Shop 分支进店**；普通拒绝不 Fault 整局；删牌走正式 `Decide`。
2. 复跑本包门槛（命令与 rc 见《实际命令与 rc》）：`--shop-flow-only` 11 项、完整 Slice 770 项。
3. 只在本包边界内继续：`mod-the-spire/base-spire/BaseSpire/Rooms/Shop/ShopRoomWiring.cs`、`ShopCompute.cs`、
   `ColorlessShopPool.cs` 及《改动与接口》列出的接缝；**不要**重开 core 重构、不补卡牌、不动 B/C 文件。

## 已有复用（不重做）

- 隔离交付：`mod-the-spire/base-spire/workpacks/shop-flow/` 的 `ShopRoomWiring.cs`/`ShopCompute.cs`/
  `ShopFlowChecks`。本包已把它们迁进正式生产树，旧文件改名为 `*.cs.frozen-20261011` 冻结留档
  （见 [workpacks/shop-flow/README.md](../../../../tools/base-migration-baseline/workpacks/shop-flow/README.md)）；
  旧 13 项检查的主机是**旧的两瓶药水池**，不能作为现行池验收。
- 源依据：[source-facts.md](../../../../.audit/dsh-shop-flow-2026-10-11/source-facts.md)（流归属、阈值、价格、
  库存顺序、购买写回、离店、选卡细节）。
- 既有正式设施：`RoomCompileContext`（Camp/Treasure 已用）、`RunInitializationWiring` 的玩家/共享遗物袋、
  `PotionInventoryRules`（48 瓶源池）、`CardDefinitions`（`New`/`Schema`）、`RewardWiring` 的
  `RewardCardOdds` 持久赔率字段、`ActionCompilation.InCombat` 之外的相位守卫模式。
- 未重新补药水/遗物/卡牌效果（按派单要求）；商店只用**当前**内容池与效果表。

## 改动与接口

### 新增生产文件

| 文件 | 内容 |
| --- | --- |
| `BaseSpire/Rooms/Shop/ShopRoomWiring.cs` | 正式商店机制：`ShopOffer`（含 `SourceName`）/`ShopOffers`、`Enter` 生成 14 格、5 个动作、4 个过程、遗物袋取货与可买判据 |
| `BaseSpire/Rooms/Shop/ShopCompute.cs` | 9 个纯计算 Entry（`shop-card-rarity` 多出一个 `Available` 输出）+ 对应 IR 记符号 |
| `BaseSpire/Cards/Pools/ColorlessShopPool.cs` | 无色**源顺序清单 + 源稀有度**（64 项：Uncommon 39 / Rare 25） |

### 修改的接缝（括号内是本包改动范围）

- `Rooms/Compilation/RoomCompilation.cs`：`RoomServices` 增 `CardsBindings`/`PotionsBindings`/`RelicsBindings`
  参数并构造 `ShopRoomWiring`；`Rooms` 里 `RegisterChooseMapPoint` 传 shop、`shop.RegisterActions`。
- `Composition/CompilationBindings.cs`：`RoomServicesBindings` 增 `ShopRoomWiring? Shop`。
- `Composition/CombatPackageComposer.cs`：只改 `RoomServices` 的调用参数。
- `Rooms/MapRoomWiring.cs`：新增 `ShopPhase = 7` 常量、`MapPhase` 字段域 0..7、`Projection` 只读属性、
  `Shop` 分支（进店 → `shop.Enter`）、Shop 从"未实现房型"列表移除、`RegisterChooseMapPoint` 增 shop 参数。
- `Maps/SourceActMapCompute.cs`：`BaseSpireComputeBackend.Execute` 默认分支先分派 `ShopCompute.TryExecute`。
- `Cards/Runtime/CardDefinitions.cs`：只加只读 `Schema` 属性（商店复用同一卡实体/配方）。
- `Composition/SliceManifest.cs`：只加 `RemoveCardChoicePrompt`。
- `SinglePlayer/BasePublicSemantics.cs`：`ShopOffers` 具名发布为 `Sequence`（owner `Run`）。
- `Rooms/Shop/ShopRoomWiring.cs` 内把 `ShopOffer` 加进观看者投影（经 `map.Projection`）。

### 公共接口变更（需负责人注意）

1. **`MapPhase` 字段域 0..6 → 0..7**，新相位 `MapRoomWiring.ShopPhase = 7`；其余相位不变。
2. **`dev-map` 规则/构建身份变更**：`ruleset=c46704e1392e253e`、`build=fd9e605af013e515`
   （`dev` 范围无商店，身份仍是 `3224ed9fcee52136` / `35a160593789e880`）。已按合法迁移写进
   `SliceMapRoomChecks` 的 `DevMapGolden*` 常量。
3. **新增公开集合 `ShopOffers`**（`Sequence`，owner `Run`）与商品字段
   `Slot/Kind/DefinitionId/SourceName/Rarity/PoolKind/PoolIndex/OnSale/Price/Sold/Unsupported/UnsupportedReason`；
   新增 Run 字段 `ShopCardRemovalsUsed`。公开出口（net）尚未接线，本包不写 net。
4. **新动作 5 个**：`BuyShopCard`/`BuyShopRelic`/`BuyShopPotion`/`RemoveShopCard`/`LeaveShop`。
5. `MapRoomWiring.RegisterChooseMapPoint` 与 `RoomCompilation.RoomServices` 的签名变化只在本包内闭合。

### 修掉的两处源语义错误（本轮重点）

1. **无色抽取范围（Codex 本轮指出）**：旧实现把整条 64 项当抽样上界、再把稀有度不符的成员标不可买。
   源 `CardFactory.cs:69-79` 的固定稀有度 `CreateForMerchant` **先按 `c.Rarity == rarity` 过滤**，
   再对过滤后的 items 执行 `Shops.NextItem`。现行实现按源稀有度分桶抽取（Uncommon 39 / Rare 25），
   过滤**只按源稀有度**，不按"内容里有没有 DSL/效果"过滤；抽到没进内容的源成员仍保留完整源身份
   （`SourceName` = 源 kebab 名）并写 `UnsupportedReason`，不换卡、不删成员。
   稀缺度元数据从各卡源构造取得（如 `sts2/src/Core/Models/Cards/Alchemize.cs:17` = `CardRarity.Rare`），
   写进 `ColorlessShopPool.Entries`；内容声明进 `ColorlessCardPool` 但不在源清单、或内容稀有度与源不符
   都**具名失败**。
2. **在售格重复抽价**：旧实现给在售格额外抽一笔价格。源 `MerchantCardEntry` 构造时 `CalcCost()` 抽一次，
   `SetOnSale()` 只把算好的 `_cost /= 2`（不再抽样）。现行实现每格恰好一笔价格抽数，
   正常路径抽数 **`Shops` 27 / `Rewards` 14**（旧常量 28 是错的，已改并写进注释）。

## 实际命令与 rc（原进程返回码，未经过滤）

| 步骤 | 命令 | rc | 日志 |
| --- | --- | ---: | --- |
| 构建（最终） | `dotnet build mod-the-spire/base-spire/BaseSpire.Slice.Tests --artifacts-path <ABS>/.artifacts/parallel/dsh-shop-integration-20261011-final` | 0 | [build-final.log](../../../../.audit/dsh-shop-integration-20261011/build-final.log) |
| 商店局部组 | `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests --artifacts-path <同上> --no-build -- --shop-flow-only` | 0 | [shop-flow-only-final.log](../../../../.audit/dsh-shop-integration-20261011/shop-flow-only-final.log) |
| 完整 Slice | `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests --artifacts-path <同上> --no-build` | 0 | [slice-full-final.log](../../../../.audit/dsh-shop-integration-20261011/slice-full-final.log) |
| 文档检查 | `python tools/doc-check/check_docs.py` | 4 | [doc-check.log](../../../../.audit/dsh-shop-integration-20261011/doc-check.log) |
| 空白检查 | `git diff --check` | 0 | [diff-check.log](../../../../.audit/dsh-shop-integration-20261011/diff-check.log) |

`<ABS>` = `F:/Scylier-Project/active/auto-the-spire`。构建根是本包独有的
`.artifacts/parallel/dsh-shop-integration-20261011{,-final}`；**未向其他构建根写入**。
另有一份早期构建日志 [build.log](../../../../.audit/dsh-shop-integration-20261011/build.log)（同样 rc=0）。

结果：`All 11 shop flow checks passed (formal dev-map entry).`、`All 770 slice checks passed.`
doc-check 的 4 项**全部是既有问题**（`dsh-power-fixture-handoff-2026-10-11.md` 两处行号锚点及引用它的
`dsh-startup-alignment-handoff-2026-10-11-f68b943d.md` 两处），与本包改动无关；本包改动把原有 6 项降到 4 项
（旧商店隔离交接的两条失效链接已修）。

## 输入 / SHA

- 源自 `sts2/src/Core/Entities/Merchant/MerchantInventory.cs`、`MerchantCardEntry`、`MerchantRelicEntry`、
  `MerchantPotionEntry`、`MerchantCardRemovalEntry`、`Core/Rooms/MerchantRoom.cs`、
  `Core/Factories/CardFactory.cs:69-79`、`Core/Models/CardPools/ColorlessCardPool.cs`、
  `Core/Models/Cards/<无色卡>.cs`（稀有度）。
- 参考：无新增独立参考；判定依据是上列源文件 + `source-facts.md`（原隔离包已逐行核对，本包沿用并修正两处）。
- 被测源 SHA256：
  - `BaseSpire/Rooms/Shop/ShopRoomWiring.cs` = `9EE5E0924FC50E31B46183E221773DAC376BB012C72E8111F4F946AEB4D83847`
  - `BaseSpire/Rooms/Shop/ShopCompute.cs` = `B45460B3CE9F40C57331E2D9681D4CA4DBED0ABB794C624EABB690ADAC8DF213`
  - `BaseSpire/Cards/Pools/ColorlessShopPool.cs` = `6F203FAD89309214A8FBA2D03E43464E5F99567056554E0C187D44B2DD9E144E`
- 最终构建产物 SHA256（`.artifacts/parallel/dsh-shop-integration-20261011-final/bin/.../debug/`）：
  - `BaseSpire.dll` = `E10198EEA6FBFA255D1257FA31DA3FFFABF25ED653325EF0946A3C943C2829C2`（1 538 560 字节）
  - `Core.TheSpire.dll` = `ECB685ABF1F3EBD70ECA559E8455E526205EF0CD6C9352DFEEFC87A1F682B137`（492 032 字节）
  - 注：`.artifacts/parallel/dsh-shop-integration-20261011-final` 连跑三次冷构建得到**同一哈希**
    （`E10198EE…`），因此该构建在本次工作区内可复现。早先对照的另一份 `.artifacts/parallel/
    dsh-shop-integration-20261011` 是在调试期用不同源码中间态构建的，不是同源两次构建；
    正式发布身份仍以 `Describe().RulesetId`/`PublicBuildId` 为准，DLL 哈希只作本次证据。
- 随机输入：seed 字符串 `t09-shop-alpha` / `t09-shop-replay` / `t09-shop-poor` / `t09-shop-fullbelt` /
  `t09-shop-unsupported` / `t09-shop-buy` / `t09-shop-remove` / `t09-shop-remove-poor` /
  `t09-shop-observe` / `t09-shop-walk`；Profile = `VanillaRandomLayout`（原版 12+3(+1 地图) 流、
  `RandomService.VanillaAlgorithmId` + `VanillaDrawProfileId`），seed 处理同正式工厂（不标准化）。
- 源抽数顺序（正常路径，`Shops` 27 / `Rewards` 14）：折扣格 1（Shops）→ 每张彩卡
  [稀有度(Rewards) → 选卡(Shops) → 升级(Rewards) → 价(Shops)]×5 → 每张无色卡
  [按源稀有度过滤后选卡(Shops)]×2 → 遗物稀有度×2(Rewards) → 遗物价×3(Shops) →
  每瓶药水 [稀有度(Shops) → pick(Shops) → 价(Shops)]×3。在售格不重复抽价；退化格（该稀有度桶被抽空）
  不抽 pick，抽数少一笔且该格仍作为完整商品位标不可买。

## 首差（本轮第一条与旧值的差异）

1. **抽数**：旧 `ShopRoomWiring.ExpectedShopsDraws = 28` → 现行 **27**（去掉在售格重复抽价）。
   实测 `Shops=27`、`Rewards=14`，与独立重放一致（`shop-flow-only-final.log`）。
2. **无色抽取上界**：旧实现 64 → 现行 **39（uncommon 槽）/ 25（rare 槽）**；
   旧实现会产出"稀有度不符"的不可买格，现行实现不会（抽到的成员必然同稀有度）。
3. **身份**：`dev-map` 规则/公开构建身份由旧值变为 `ruleset=c46704e1392e253e` /
   `build=fd9e605af013e515`（旧值见 `SliceMapRoomChecks` 的迁移注释与 git 工作区 diff）。

## 已验证（本包实际证据）

`--shop-flow-only` 11 项全绿（`shop-flow-only-final.log`），逐条判别点：

1. 进店：14 格源顺序、彩卡类型序列 = 源 `_coloredCardTypes`、彩卡互不重复、无色固定 uncommon/rare
   且身份 = 源清单同下标成员（并断言其稀有度 = 该槽要求的源稀有度）、唯一折扣格、遗物格稀有度来自袋键、
   删牌价 75、不可买格都写明原因。
2. 流序：进店恰好 `Shops` 27 / `Rewards` 14 抽；彩卡价格落在源区间（`Round(base×[0.95,1.05))`）、
   无色 ×1.15 且高于同稀有度彩卡上限、药水价按掷出稀有度、遗物价按袋键档。
3. 独立重放：另一 `RandomService`（同 seed、同 Profile）按源顺序消费 `Rewards`，逐格复算彩卡稀有度并相等；
   重放消费恰好 14 笔。
4. 买不起：商品不在候选绑定、提交走引擎拒绝（`IllegalAction`）、会话仍 `Running`、状态哈希/金币/牌组/抽数
   全不变、货架不变、仍能离店并回到旅行相位。
5. 满药水栏：同上（候选不含该格、拒绝后金币与药水栏不变、仍能离店）。
6. 不可买商品：不在候选域、提交被拒、拒绝后状态不变；把 `MapPhase` 改成战斗相位后 5 个店内动作全不合法，
   改回商店相位又可买。
7. 购买：买卡（真实长期牌 + 扣金一次 + 售罄 + 不可重复买）、买药水（进药水栏 + 定义一致）、
   买已支持遗物（先扣金再结算 `AfterObtained` 的 GainGold/GainMaxHp，建立真实持有实例）。
8. 跨离店：离店回旅行相位、店内动作全不合法；**重走地图 Shop 分支再进店**仍 14 格且无残留售出状态，
   购买结果保留。
9. 删牌（正式 `Decide`）：重放"同定义两实例"，选牌前不扣金/不计数；答案选中的那一件被删、
   同定义另一件仍在、计数 +1、售罄、一间一次；下一间价格 100 且又能删一张。
10. 删牌买不起：候选不含删牌商品、拒绝走引擎 `IllegalAction`、不留下挂起 decision、状态不变、仍能离店。
11. 观察纯净：重复读货架/查询合法动作不改状态哈希、不消费随机数、货架身份逐格不变。
12. 正常可达：从**正常 Start**（`seed=t09-shop-walk`）用公开动作走到真实 Shop 节点
    （`row1:Monster → row2:Shop`），进店 14 格、离店回旅行相位。

完整 Slice 770 项全绿（含既有地图/奖励/营火/宝箱/药水/卡牌族）。

## 未执行 / 受阻

- **未跑**：solution 全量、Engine、net、tools；公开轨迹/公开复放（派单明确由 Codex 串行收口）。
  本包 `git diff --check` rc=0，未提交/未打 tag/未 reset。
- **未接线**：公开出口（net）尚无 `ShopOffers`/5 个动作的映射；公开语义已就绪但需 C 线翻译。
- **受阻（明确边界）**：
  1. 商店只存在于 `BaseSpireScope.DevMap`；`dev` 范围无地图，因此公共最终构建/正式公开轨迹未验证。
  2. `ACT1-BASIC-10` 第 59 步 `Fogmog` 故障是既有战斗边界，本包不换池、不假胜利，也未依赖它通关。
  3. 源袋里多数遗物没有正式效果声明 → 商品保留身份并标 `Unsupported`（购买被守卫拒绝）。
     "剩余遗物效果边界"= 只有已登记 `AfterObtained`（GainMaxHp/GainGold）与 `AfterCombatVictory`
     的遗物可买；`Ancient/Event/Starter` 档价格表已实现但当前内容不会产出这些档。
  4. 卡牌删牌的 `Eternal`（不可删）关键字在本 Slice 尚未作为卡字段存在，因此可删性判据 =
     "是长期牌组的真实实例"（由 decision 候选域保证），已在代码注释与本文件记账。
- **首差未关**：`dev-map` 身份变更需负责人复核确认（已在测试里以新常量固定）。

## 依赖 / 可继续事项

- 依赖：无新增外部依赖；B（tools 冒烟语义重放）与 C（net/client 商店策略）包按派单各写各的，
  **不写本包文件，也不构建 base**；公共模块仍由本包单写。
- 可继续（按优先级，均属父项 ENV-T09/公开出口，不在本包启动）：
  1. C 线把 `ShopOffers` + 5 个动作接进公开出口与客户端策略（本包提供公开语义与稳定名字）。
  2. 仓库级最终门槛（solution/Engine/net/公开轨迹）由 Codex 串行收口，本包不代跑。
  3. 若负责人要求把商店接进 `dev` 之外的正式公共构建，需要先有非 dev-map 的地图入口（属 T05/T10 范围），
     不在本包启动。
