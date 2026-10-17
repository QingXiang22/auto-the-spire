# 房间与事件

查找或修改房间与事件时从本目录开始。[全部领域](../README.md)。

| 位置 | 负责什么 |
| --- | --- |
| [Compilation/RoomCompilation.cs](Compilation/RoomCompilation.cs) | 房间服务和房型组合入口 |
| [Compilation/BattleRoomCompilation.cs](Compilation/BattleRoomCompilation.cs) | 战斗房进入与战斗／永久实例转换 |
| [MapRoomWiring.cs](MapRoomWiring.cs) | 地图选点与实际房型接入 |
| [RoomCompileContext.cs](RoomCompileContext.cs) | 房间共享的编译期状态和操作 |
| [Events/](Events/) | 各事件独立模块、选择、随机与访问生命周期 |
| [EventRoomCatalog.cs](EventRoomCatalog.cs) | 事件模块组合登记 |
| [Unknown/](Unknown/) | 问号概率与实际房型判定 |
| [Shop/](Shop/) | 商店货架、价格和购买 |
| [RestSite/](RestSite/) | 营火选项与离场 |
| [Treasure/](Treasure/) | 宝箱房进入、开箱与领取 |

已有行为检查见 [BaseSpire.Slice.Tests/Rooms](../../BaseSpire.Slice.Tests/Rooms/)。

组合已有能力时先修改内容或所属领域模块；新增跨领域依赖或改变声明顺序时，再修改 [Composition](../Composition/README.md)。
