using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Rooms;

/// <summary>
/// 问号房共享模块（一）的**地图入口**：`MapPointType.Unknown` 的进入处理器。
///
/// 与源 `RunManager.EnterMapPointInternal` + `RollRoomTypeFor` + `CreateRoom` 的对应关系：
/// <list type="number">
/// <item>先按源 `BuildRoomTypeBlacklist` 求黑名单（必须在 `MapRoomEntry.Visit` 之前，见
///       <see cref="UnknownRoomWiring.BlacklistShop"/>）；</item>
/// <item>再判定实际房型（<see cref="UnknownRoomWiring.RollRoomType"/>），把它写进
///       `Run.ActualRoomType`；地图节点的 `Type` **仍是 `Unknown`**，不改写节点；</item>
/// <item>最后按实际房型复用**已有房间入口**：普通战斗/精英/宝箱/商店直接调用
///       `MapRoomEntry.Standard(...)` 的同一个 `Enter`（不复制一份房型逻辑）；事件走
///       <see cref="EventVisitLifecycle"/> 的队列消费 + `EventRoomCatalog.Enter` 分派；</item>
/// <item>判定出本构建没有接线的房型（源上不可能，或将来 hook 引入）时显式 `Fail`，不留空房间。</item>
/// </list>
///
/// 源 `RoomType.RestSite` 不在 `UnknownMapPointOdds._nonEventOdds` 的键里，因此问号房**不可能**判成营火；
/// 这里不为它造一个源上没有的分支。营火/商店/宝箱守卫的"实际房型"识别由
/// <see cref="RoomCompileContext.At"/> + <see cref="MapRoomWiring.AtRoomType"/> 统一提供。
/// </summary>
public static class UnknownRoomEntry
{
    /// <summary>地图节点类型成员名（源 `MapPointType.Unknown`）。</summary>
    public const string PointType = "Unknown";

    /// <summary>
    /// 组合一个 `Unknown` 入口。`standard` 必须是 `MapRoomEntry.Standard(...)` 的**同一批**入口记录，
    /// 这样问号房与直接房型走的是同一份进入逻辑（含各自的相位与随机调用）。
    /// </summary>
    public static MapRoomEntry Create(MapRoomWiring map, IReadOnlyList<MapRoomEntry> standard, UnknownRoomWiring odds,
        EventVisitLifecycle events, EventCompileContext eventContext, EventRoomCatalog catalog,
        Func<IrScope, Inst[]>? afterEventEntered = null)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(standard);
        ArgumentNullException.ThrowIfNull(odds);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(eventContext);
        ArgumentNullException.ThrowIfNull(catalog);

        MapRoomEntry Standard(string pointType) => standard.Single(entry =>
            string.Equals(entry.PointType, pointType, StringComparison.Ordinal));

        // 源里"房型名"与"地图点类型名"对 Monster/Elite/Boss/Treasure/Shop/RestSite 是同名；
        // 这里显式核对一遍（改名/错字在建包期失败，不在运行期悄悄换成别的房）。
        MapRoomEntry StandardFor(int roomType)
        {
            var name = UnknownRoomWiring.RoomTypeMember(roomType);
            if (MapRoomWiring.DirectRoomType(name) is null)
            {
                throw new ArgumentException($"源房型 '{name}' 在直接房型节点里没有同名入口");
            }

            return Standard(name);
        }

        return new MapRoomEntry(PointType, (scope, run, node) =>
        {
            // 源 `RunManager.EnterMapPointInternal`：黑名单在 `CreateRoom` 之前求值。
            var blacklist = scope.Local("unknownBlacklistShop", FieldTypeSchema.Bool());
            var roll = odds.RollRoomType(scope, run, scope.LocalRef(blacklist));
            List<Inst> steps =
            [
                scope.SetLocal(blacklist, odds.BlacklistShop(scope, run)),
                .. roll.Steps,
                // 源 `MapPointHistoryEntry.RoomType`：实际房型另存字段，节点类型保持 Unknown。
                scope.SetField(run, map.RunActualRoomType, scope.LocalRef(roll.RoomTypeSlot)),
            ];

            // 源 `CreateRoom` 的分派：Monster/Elite/Boss → 战斗房，Treasure → 宝箱，Shop → 商店，
            // Event → 事件房（这里复用 Standard 的入口；Boss 不在问号房赔率里，走到它就是显式失败）。
            var eventPath = new EventPath(events, eventContext, catalog, map, scope, run, node, afterEventEntered);
            var branches = new (string RoomType, Inst[] Body)[]
            {
                (UnknownRoomWiring.RoomTypeMember(UnknownMapPointOdds.Monster),
                    StandardFor(UnknownMapPointOdds.Monster).Enter(scope, run, node)),
                (UnknownRoomWiring.RoomTypeMember(UnknownMapPointOdds.Elite),
                    StandardFor(UnknownMapPointOdds.Elite).Enter(scope, run, node)),
                (UnknownRoomWiring.RoomTypeMember(UnknownMapPointOdds.Treasure),
                    StandardFor(UnknownMapPointOdds.Treasure).Enter(scope, run, node)),
                (UnknownRoomWiring.RoomTypeMember(UnknownMapPointOdds.Shop),
                    StandardFor(UnknownMapPointOdds.Shop).Enter(scope, run, node)),
                (UnknownRoomWiring.RoomTypeMember(UnknownMapPointOdds.Event), eventPath.Steps),
            };

            Inst dispatch = scope.Fail(
                "base-spire: 问号房判定出的房型没有接线（不伪装空房间、不改写节点类型）");
            for (var index = branches.Length - 1; index >= 0; index--)
            {
                dispatch = scope.IfElse(
                    scope.Equal(scope.LocalRef(roll.RoomTypeSlot), scope.EnumConst(map.RoomType, branches[index].RoomType)),
                    branches[index].Body, [dispatch]);
            }
            steps.Add(dispatch);
            return [.. steps];
        });
    }

    /// <summary>事件分支：进入事件房（相位 = `EventPhase`）→ 源队列消费 → 访问计数 → 目录分派。</summary>
    private sealed class EventPath
    {
        internal EventPath(EventVisitLifecycle events, EventCompileContext context, EventRoomCatalog catalog,
            MapRoomWiring map, IrScope scope, Expr run, Expr node, Func<IrScope, Inst[]>? afterEntered)
        {
            var pull = events.Pull(scope, run);
            Steps =
            [
                .. MapRoomEntry.Visit(map, scope, run, node, MapRoomWiring.EventPhase),
                .. pull.Steps,
                .. events.MarkVisited(scope, run),
                catalog.Enter(scope, context, run, scope.LocalRef(pull.IdSlot)),
                .. afterEntered?.Invoke(scope) ?? [],
            ];
        }

        internal Inst[] Steps { get; }
    }
}
