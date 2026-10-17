using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Rooms;

/// <summary>
/// 事件模块的**唯一注册处**：这里只列模块，不放任何事件效果、选项或按 ID 分派的手写 switch。
///
/// 新增一个"已有能力"事件 = 在 `Rooms/Events/` 加一个实现 <see cref="IEventRoomModule"/> 的文件
/// + 在本列表里加一项；分派由 <see cref="EventRoomCatalog"/> 按源身份完成，事件池顺序与赔率不受影响。
/// 源池身份与模块 ID 的转换点见 <see cref="EventSourceIdentity"/>。
///
/// 尚未实现的源事件**不在这里登记**：`RunEventQueue` 仍然按源池全量生成并洗牌，选到未实现身份时
/// `EventRoomCatalog.Enter` 显式失败（不缩池、不换事件）。
/// </summary>
public static class EventModules
{
    /// <summary>本构建登记的可执行事件模块（顺序即动作登记顺序，不是事件池顺序）。</summary>
    public static IReadOnlyList<IEventRoomModule> Create(EventVisitState state, EnginePackageBuilder package) =>
    [
        new SunkenStatueEvent(state),
        new AromaOfChaosEvent(),
        new SlipperyBridgeEvent(package),
        new TabletOfTruthEvent(),
        new RoomFullOfCheeseEvent(package),
        new JungleMazeAdventureEvent(package),
        new MorphicGroveEvent(),
        new SapphireSeedEvent(),
    ];
}
