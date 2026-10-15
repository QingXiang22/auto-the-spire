using CoreTheSpire.Engine;
using BaseSpire.Slice.Effects;
using BaseSpire.Slice.Orchestration;
using BaseSpire.Slice.Rooms;
using BaseSpire.Slice.Domain.Run;
using static BaseSpire.Slice.Compilation.CompilationCallbacks;
using static BaseSpire.Slice.CombatSlicePackage;

namespace BaseSpire.Slice.Compilation;

internal static class RoomCompilation
{
    internal static RoomServicesBindings RoomServices(
        PackageBuildOptions options,
        ContentBindings contentBindings,
        EventsBindings eventsBindings,
        CardsBindings cardsBindings,
        PotionsBindings potionsBindings,
        RunStateBindings runStateBindings,
        RelicsBindings relicsBindings,
        RunCollectionsBindings runCollectionsBindings)
    {

        // T05：地图/房间子包（默认关）。开启时地图状态是**引擎实体/字段/关系/集合**：
        //   * `MapNode` + `MapNodes` + `MapChildren`（源 `MapPoint` / 其 `Children`），进观看者投影；
        //   * `Run.CurrentNode`（源 `State.CurrentMapPoint`）、`MapPhase`、`MapFloor`、`MapNodeCount`、`MapDraws`；
        //   * `ChooseMapPoint` 动作（候选 = 当前节点的 `MapChildren`，守卫 = 阶段/末尾）。
        // 关闭时（受控切片、既有夹具、`BaseSpireScope.Dev`）不声明任何东西，包形状逐字节不变。
        MapRoomWiring? actMap = null;

        if (options.ActMapEnabled && runStateBindings.HasRun)
        {
            actMap = MapRoomWiring.Declare(contentBindings.Package, eventsBindings.Projection, runStateBindings.RunEntity!);
        }

        RunInitializationWiring? runInitialization = runStateBindings.HasRun && (options.InitializeNewRun || actMap is not null)
            ? new RunInitializationWiring(contentBindings.Package, runStateBindings.RunEntity!) : null;

        RoomCompileContext? rooms = actMap is not null
            ? new(runStateBindings.RunEntity!, runCollectionsBindings.RunStates!, runStateBindings.RunHp!, runStateBindings.RunMaxHp!, runStateBindings.RunGold!, runCollectionsBindings.LongTermDeck!, actMap) : null;

        RunResources? resources = rooms is not null
            ? new RunResources(rooms, eventsBindings.Defeat, eventsBindings.DefeatTurn) : null;
        RunDeck? deckCommands = rooms is not null ? new RunDeck(cardsBindings.CardDefinitions, rooms.LongTermDeck) : null;
        RunRelics? relicCommands = resources is not null && runInitialization is not null && relicsBindings.HasRelics
            ? new RunRelics(new(relicsBindings.Relic!, relicsBindings.RelicDefinition!,
                relicsBindings.RelicRarity!, relicsBindings.Relics!), runInitialization, resources, options.RelicLibrary ?? [])
            : null;
        TreasureRoomWiring? treasure = rooms is not null && runInitialization is not null && relicCommands is not null
            ? new TreasureRoomWiring(contentBindings.Package, rooms, runInitialization, resources!, relicCommands) : null;

        // ENV-T09.2：商店与营火/宝箱同属地图房间。它复用**已有**的卡牌配方/药水库存实例
        // （不重新声明 `Card`/`Potion`/`PotionBelt`/`RewardPotions`），只补商店自己的实体、集合、
        // 计算资产与过程；正式接缝是 `RoomCompileContext` + 遗物袋 + 药水栏。
        ShopRoomWiring? shop = actMap is not null && runInitialization is not null
            && relicsBindings.HasRelics && potionsBindings.HasPotions
            ? new ShopRoomWiring(contentBindings.Package, rooms!, deckCommands!,
                runInitialization, potionsBindings.Potions!, resources!, relicCommands!)
            : null;

        // ENV-T09-unknown-room-modules-20261012：问号房与事件。三个共享模块各有归属：
        //   * `UnknownRoomWiring`：源未知概率（`UnknownMapPointOdds`）+ 源黑名单 + 房型判定；
        //   * `EventVisitLifecycle`：源事件池队列消费/资格/访问计数（源 `RoomSet`/`PullNextEvent`）；
        //   * `EventModules`：每个具体事件一个文件，注册处只列模块。
        // 事件访问状态的字段/集合由生命周期模块声明，`EventCompileContext` 只拿符号；
        // 默认 scope（没有地图）下这三者都不存在，包形状与身份逐字节不变。
        UnknownRoomsBindings? unknownRooms = null;
        if (actMap is not null && runInitialization is not null && rooms is not null)
        {
            var odds = new UnknownRoomWiring(contentBindings.Package, runStateBindings.RunEntity!, actMap);
            var eventState = EventVisitLifecycle.Declare(contentBindings.Package, runStateBindings.RunEntity!,
                runInitialization);
            var eventContext = new EventCompileContext(rooms, eventState.CurrentEventId, MapRoomWiring.EventPhase)
            {
                Resources = resources,
            };
            var modules = EventModules.Create(eventState);
            var catalog = new EventRoomCatalog(modules);
            unknownRooms = new UnknownRoomsBindings(odds, eventState, eventContext,
                new EventVisitLifecycle(eventState, eventContext, modules), catalog);
        }

        return new RoomServicesBindings(actMap, runInitialization, rooms, resources, relicCommands, deckCommands, treasure, shop, unknownRooms);
    }

    internal static void Rooms(
        PackageBuildOptions options,
        ContentBindings contentBindings,
        CardsBindings cardsBindings,
        RunStateBindings runStateBindings,
        RelicsBindings relicsBindings,
        RunCollectionsBindings runCollectionsBindings,
        RoomServicesBindings roomServicesBindings,
        RewardsBindings rewardsBindings,
        RoomTransitionBindings roomTransitionBindings)
    {

        // T05：地图 scope 的房间选择入口。`EnterNextRoom` 在上面被显式跳过（它是非地图 scope 的
        // 兼容入口，地图的"进下一间房"是 `ChooseMapPoint`：候选来自 `MapChildren`，不是遭遇池序号）。
        if (roomServicesBindings.ActMap is not null)
        {
            // 入口组合：直接房型沿用 `MapRoomEntry.Standard` 的原始顺序与实现，问号房是**追加**的一项；
            // 问号房内部复用同一批 Standard 记录进入战斗/精英/宝箱/商店，不复制房型逻辑。
            var standard = MapRoomEntry.Standard(roomServicesBindings.ActMap,
                roomTransitionBindings.StartNextRoom!, roomServicesBindings.Treasure!, roomServicesBindings.Shop!);
            var entries = new List<MapRoomEntry>(standard);
            if (roomServicesBindings.Unknown is { } unknown)
            {
                entries.Add(UnknownRoomEntry.Create(roomServicesBindings.ActMap, standard, unknown.Odds,
                    unknown.Events, unknown.EventContext, unknown.Catalog));
            }

            roomServicesBindings.ActMap.RegisterChooseMapPoint(contentBindings.Package, runCollectionsBindings.RunStates!,
                entries);

            RestSiteWiring.Register(contentBindings.Package, roomServicesBindings.Rooms!, roomServicesBindings.Resources!,
                cardsBindings.CardDefinitions);
            roomServicesBindings.Treasure!.RegisterActions(contentBindings.Package);
            roomServicesBindings.Shop!.RegisterActions(contentBindings.Package);
            // 事件：注册处只列模块（`EventModules`），目录负责按源身份分派；未登记身份显式失败。
            roomServicesBindings.Unknown?.Catalog.RegisterActions(contentBindings.Package,
                roomServicesBindings.Unknown.EventContext);
            rewardsBindings.Rewards!.RegisterActions(contentBindings.Package);
        }

    }
}
