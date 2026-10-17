# workpacks/shop-flow（历史留档，已迁移）

ENV-T09.2 商店模块的**隔离交付留档**。2026-10-11 `ENV-T09-shop-integration-20261011` 已把它迁移进正式生产树：

| 旧交付 | 现行位置 |
| --- | --- |
| `ShopRoomWiring.cs`（冻结副本 `ShopRoomWiring.cs.frozen-20261011`） | `BaseSpire/Rooms/Shop/ShopRoomWiring.cs` |
| `ShopCompute.cs`（冻结副本 `ShopCompute.cs.frozen-20261011`） | `BaseSpire/Rooms/Shop/ShopCompute.cs` |
| （无，隔离时无色按整条 64 项抽样） | `BaseSpire/Cards/Pools/ColorlessShopPool.cs` |
| `ShopFlowChecks/`（独立主机 + 13 项检查） | `BaseSpire.Slice.Tests/Rooms/ShopFlowChecks.cs`（11 项，正式 `ChooseMapPoint` Shop 分支进店） |

## 迁移时修掉的两处源语义偏差

1. **无色抽取范围**：旧实现把整条 64 项无色源清单当抽样上界，再把稀有度不符的成员标不可买；
   源 `CardFactory.cs:69-79` 的固定稀有度 `CreateForMerchant` **先按 `c.Rarity == rarity` 过滤源清单**，
   再对过滤后的 items 执行 `Shops.NextItem`。现行实现按源稀有度分桶抽取（Uncommon 39 项 / Rare 25 项），
   抽取范围只按源稀有度过滤，不按"内容里有没有 DSL/效果"过滤。
2. **在售格重复抽价**：旧实现给在售格额外抽一笔价格；源 `MerchantCardEntry.SetOnSale()` 只把算好的
   `_cost /= 2`（`CalcCost()` 在构造时已抽过一次）。现行实现每格恰好一笔价格抽数，
   每间商店的 `Shops` 抽数从 28 降为 27。

## 重新跑旧交付

本目录的 `csproj` 默认**不编译**这两个冻结副本（`Compile` 只含 `ShopFlowChecks/*.cs`）。
需要对照旧行为时，把 `..\ShopRoomWiring.cs.frozen-20261011` 与 `..\ShopCompute.cs.frozen-20261011`
显式加回 `Compile`，并按项目注释引用 `dsh-core-author-04` 冻结二进制。

**旧二进制只代表当时的池（药水 2 瓶等），不能作为现行内容池验收**；现行门槛跑
`BaseSpire.Slice.Tests`（含 `--shop-flow-only` 局部选组）。
