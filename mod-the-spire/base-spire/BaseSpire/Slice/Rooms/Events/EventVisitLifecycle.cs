using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Rooms;

/// <summary>
/// 源 <c>EventModel.IsAllowed(runState)</c> 的**可选**覆盖声明。源里有多个事件覆盖了它
/// （例如 `UnrestSite` 要求生命 ≤ 70%、`RelicTrader` 要求第二幕且持有 5 件可交易遗物）。
///
/// 只有已经实现的事件模块才可能声明它；未实现的源身份按源**默认实现**（恒等 true）参与资格判定，
/// 随后在 `EventRoomCatalog.Enter` 的未注册分支显式失败。这条边界是明确的：本构建不假装知道
/// 未实现事件的资格规则，也不缩池来绕开它。
/// </summary>
public interface IEventEligibility
{
    Expr IsAllowed(IrScope scope, EventCompileContext context, Expr run);
}

/// <summary>
/// 事件访问生命周期模块（二）：**源池队列消费 + 资格 + 访问计数**。
///
/// 逐行照抄下列源（`sts2/src/Core/`）：
/// <list type="bullet">
/// <item>`Rooms/RoomSet.cs:EnsureNextEventIsValid`：`for i in 0..events.Count`，
///       `NextEvent.IsAllowed(state) &amp;&amp; !VisitedEventIds.Contains(NextEvent.Id)` 则停，
///       否则 `eventsVisited++`；走满一轮即源里的 "All unique events exhausted, allowing repetition"。</item>
/// <item>`Rooms/RoomSet.cs:NextEvent` = `events[eventsVisited % events.Count]`（列表顺序即 Start 时
///       用 `up_front` 流洗好的源池顺序 —— 本模块**消费**它，不重洗、不按可执行模块筛选）。</item>
/// <item>`Models/ActModel.cs:PullNextEvent` → `EnsureNextEventIsValid` → `Hook.ModifyNextEvent`（默认恒等）
///       → `runState.AddVisitedEvent(eventModel)`。</item>
/// <item>`Models/ActModel.cs:MarkRoomVisited(RoomType)` → `RoomSet.MarkVisited`：事件房进入时
///       `eventsVisited++`（战斗房的两个 per-type 计数由战斗房模块自己拥有，本模块不重复计）。</item>
/// </list>
///
/// **不**拥有的东西：房型判定与赔率（<see cref="UnknownRoomWiring"/>）、具体事件的选项与效果
/// （`Slice/Rooms/Events/&lt;Name&gt;.cs`）、事件战斗的暂停/返回（尚未接线）。
/// </summary>
public sealed class EventVisitLifecycle
{
    private readonly EventVisitState state;
    private readonly EventCompileContext context;
    private readonly IReadOnlyList<IEventRoomModule> modules;

    public EventVisitLifecycle(EventVisitState state, EventCompileContext context,
        IReadOnlyList<IEventRoomModule> modules)
    {
        this.state = state ?? throw new ArgumentNullException(nameof(state));
        this.context = context ?? throw new ArgumentNullException(nameof(context));
        this.modules = modules ?? throw new ArgumentNullException(nameof(modules));
    }

    /// <summary>
    /// 声明事件访问状态的 Engine 字段/集合（都由**本模块**拥有；默认值就是源初值，Start 侧不需要再写）。
    /// 队列成员种类与 `SourceId` 字段复用 `RunInitializationWiring` 的 `RunRoomIdentity`：
    /// 源里事件队列与"已访问事件 ID"集合装的是同一种 `ModelId`。
    /// </summary>
    public static EventVisitState Declare(EnginePackageBuilder package, EntityTypeSymbol run,
        RunInitializationWiring runInitialization)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(runInitialization);

        // 源 `EventRoom` 的当前事件身份：公开事实（玩家看得见自己进了哪个事件），保留源名字。
        var currentEventId = run.AddField("CurrentEventId", FieldTypeSchema.String(64), Value.Of(string.Empty));
        // 源 `RoomSet.eventsVisited`：队列下标 = 本计数 % 队列长度。
        var eventsVisited = run.AddField("EventsVisited", FieldTypeSchema.Int(0, 100_000), Value.Of(0L),
            FieldVisibility.Hidden);
        // 当前事件页的 `DynamicVars.Gold`（源事件在 `CalculateVars` 里掷出的值），选项效果从它取值。
        var pageGold = run.AddField("EventPageGold", FieldTypeSchema.Int(0, 1_000_000), Value.Of(0L),
            FieldVisibility.Hidden);
        // 源 `RunState._visitedEventIds`：已访问事件身份集合（`EnsureNextEventIsValid` 用它跳过重复）。
        var visited = package.Collection("RunVisitedEvents", runInitialization.RoomIdentity);
        return new EventVisitState(currentEventId, eventsVisited, pageGold, runInitialization.Events, visited,
            runInitialization.RoomIdentity, runInitialization.RoomId, EventRandomAsset.Declare(package));
    }

    /// <summary>
    /// 源 `ActModel.PullNextEvent`：按 `eventsVisited % Count` 取 `NextEvent`，跳过"不允许或已访问"的，
    /// 命中后把该身份加进已访问集合；返回选中的**源身份**所在 local slot（交给
    /// `EventRoomCatalog.Enter` 分派）。
    /// </summary>
    public EventPull Pull(IrScope scope, Expr run)
    {
        var selected = scope.Local("eventSelectedId", FieldTypeSchema.String(64));
        var first = scope.Local("eventFirstId", FieldTypeSchema.String(64));
        var found = scope.Local("eventFound", FieldTypeSchema.Bool());
        var position = scope.Local("eventPosition", FieldTypeSchema.Int(0, QueueSize));
        var cursor = scope.Local("eventCursor", FieldTypeSchema.Int(0, QueueSize));
        var candidate = scope.Local("eventCandidateId", FieldTypeSchema.String(64));
        var eligible = scope.Local("eventEligible", FieldTypeSchema.Bool());
        var visited = scope.Local("eventVisited", FieldTypeSchema.Bool());
        var record = scope.Local("eventVisitedEntry", FieldTypeSchema.Binding());

        List<Inst> steps =
        [
            scope.SetLocal(selected, scope.Const(string.Empty)),
            scope.SetLocal(first, scope.Const(string.Empty)),
            scope.SetLocal(found, scope.Const(false)),
            // 源 `for (int i = 0; i < events.Count; i++)`：轮次上界是**队列的实际长度**（不是源池常量）。
            scope.Repeat(scope.Count(scope.Collection(state.Queue)),
            [
                scope.If(scope.Not(scope.LocalRef(found)),
                [
                    // NextEvent = events[eventsVisited % events.Count]
                    scope.SetLocal(position,
                        scope.Modulo(scope.Field(run, state.EventsVisited), scope.Count(scope.Collection(state.Queue)))),
                    scope.SetLocal(cursor, scope.Const(0L)),
                    scope.SetLocal(candidate, scope.Const(string.Empty)),
                    scope.ForEach(scope.Collection(state.Queue), entry =>
                    [
                        scope.If(scope.Equal(scope.LocalRef(cursor), scope.LocalRef(position)),
                            scope.SetLocal(candidate, scope.Field(entry, state.IdentityId))),
                        scope.SetLocal(cursor, scope.Add(scope.LocalRef(cursor), scope.Const(1L))),
                    ]),
                    // 走满一轮时的兜底条目 = 本轮第一条（源"允许重复"用的就是它）。
                    scope.If(scope.Equal(scope.LocalRef(first), scope.Const(string.Empty)),
                        scope.SetLocal(first, scope.LocalRef(candidate))),
                    .. Eligibility(scope, run, candidate, eligible),
                    .. VisitedCheck(scope, candidate, visited),
                    scope.IfElse(scope.And(scope.LocalRef(eligible), scope.Not(scope.LocalRef(visited))),
                    [
                        scope.SetLocal(found, scope.Const(true)),
                        scope.SetLocal(selected, scope.LocalRef(candidate)),
                    ],
                    [
                        scope.SetField(run, state.EventsVisited,
                            scope.Add(scope.Field(run, state.EventsVisited), scope.Const(1L))),
                    ]),
                ]),
            ]),
            // 源在走满一轮后记一条 warning 并允许重复：此时 eventsVisited 恰好前进 Count，
            // `NextEvent` 与轮初同一下标，因此选第一条即可（不额外消费随机数）。
            scope.If(scope.Equal(scope.LocalRef(selected), scope.Const(string.Empty)),
                scope.SetLocal(selected, scope.LocalRef(first))),
            // 源 `runState.AddVisitedEvent(eventModel)`：抽中的身份进入已访问集合。源用的是 HashSet，
            // 重复身份不新增成员；这里先查一遍再建实体以保持同一语义（只影响"走满一轮允许重复"那一轮）。
            .. VisitedCheck(scope, selected, visited),
            scope.If(scope.Not(scope.LocalRef(visited)),
                scope.CreateEntity(state.Identity, scope.EntityScope(run), record,
                    [(state.IdentityId, scope.LocalRef(selected))], state.VisitedEvents)),
        ];

        return new EventPull([.. steps], selected);
    }

    /// <summary>源 `RoomSet.MarkVisited(RoomType.Event)`：事件房进入时访问计数 +1。整局一次，不重复计。</summary>
    public Inst[] MarkVisited(IrScope scope, Expr run) =>
    [
        scope.SetField(run, state.EventsVisited,
            scope.Add(scope.Field(run, state.EventsVisited), scope.Const(1L))),
    ];

    /// <summary>资格判定：模块声明的源 `IsAllowed` 覆盖，未声明（含未实现事件）按源默认 `true`。</summary>
    private IEnumerable<Inst> Eligibility(IrScope scope, Expr run, int candidate, int eligible)
    {
        yield return scope.SetLocal(eligible, scope.Const(true));
        foreach (var module in modules)
        {
            if (module is not IEventEligibility declared)
            {
                continue;
            }

            yield return scope.If(scope.Equal(scope.LocalRef(candidate), scope.Const(module.Id)),
                scope.SetLocal(eligible, declared.IsAllowed(scope, context, run)));
        }
    }

    /// <summary>源 `runState.VisitedEventIds.Contains(NextEvent.Id)`：逐个身份比较，空集合恒为 false。</summary>
    private IEnumerable<Inst> VisitedCheck(IrScope scope, int candidate, int visited)
    {
        yield return scope.SetLocal(visited, scope.Const(false));
        yield return scope.ForEach(scope.Collection(state.VisitedEvents), entry =>
        [
            scope.If(scope.Equal(scope.Field(entry, state.IdentityId), scope.LocalRef(candidate)),
                scope.SetLocal(visited, scope.Const(true))),
        ]);
    }

    /// <summary>当前事件身份字段（源 `EventRoom` 的模型 id）。</summary>
    public FieldSymbol CurrentEventId => state.CurrentEventId;

    /// <summary>源事件池的队列长度（Start 时按源池全量生成，长度固定）。</summary>
    public static int QueueSize => RunSourcePoolManifest.Events.Count;
}

/// <summary>一次 `PullNextEvent` 的编译结果：指令序列 + 选中源身份所在的 local slot。</summary>
public readonly record struct EventPull(Inst[] Steps, int IdSlot);

/// <summary>
/// 事件访问状态（Engine 字段/集合符号）。本记录只传符号，不持有 Session 或可变 C# 运行状态。
/// </summary>
public sealed record EventVisitState(
    FieldSymbol CurrentEventId,
    FieldSymbol EventsVisited,
    FieldSymbol EventPageGold,
    CollectionSymbol Queue,
    CollectionSymbol VisitedEvents,
    EntityTypeSymbol Identity,
    FieldSymbol IdentityId,
    EventRandomAsset Random);
