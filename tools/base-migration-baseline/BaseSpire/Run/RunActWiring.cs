using BaseSpire.Slice.Rooms;
using BaseSpire.SinglePlayer;
using BaseSpire.Slice.Compilation;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice;

/// <summary>Compiles the default act plan; all mutable queues, cursors and assignments live in Engine state.</summary>
internal sealed class RunActWiring
{
    private readonly RunActPlan plan;
    private readonly MapRoomWiring map;
    private readonly EntityTypeSymbol entry;
    private readonly FieldSymbol act, index, id, boss, ancient;
    private readonly CollectionSymbol metadata, normal, elite, events, shared;
    public FieldSymbol ActId { get; }

    public RunActWiring(EnginePackageBuilder p, EntityTypeSymbol run, MapRoomWiring map, RunActPlan plan)
    {
        this.plan = plan; this.map = map;
        map.RunActIndex = run.AddField("CurrentActIndex", FieldTypeSchema.Int(0, plan.Acts.Count - 1));
        ActId = run.AddField("ActId", FieldTypeSchema.String(64));
        run.AddField("ActCount", FieldTypeSchema.Int(1, plan.Acts.Count), Value.Of((long)plan.Acts.Count));
        map.RunCompletedFloors = run.AddField("CompletedActFloors", FieldTypeSchema.Int(0, 9999), visibility: FieldVisibility.Hidden);
        map.RunTotalFloor = run.AddField("TotalFloor", FieldTypeSchema.Int(0, 9999));
        map.AncientPending = run.AddField("ActAncientPending", FieldTypeSchema.Bool(), visibility: FieldVisibility.Hidden);
        entry = p.EntityType("RunActEntry");
        act = entry.AddField("ActIndex", FieldTypeSchema.Int(0, plan.Acts.Count - 1), visibility: FieldVisibility.Hidden);
        index = entry.AddField("Index", FieldTypeSchema.Int(0, 999), visibility: FieldVisibility.Hidden);
        id = entry.AddField("SourceId", FieldTypeSchema.String(64), visibility: FieldVisibility.Hidden);
        boss = entry.AddField("Boss", FieldTypeSchema.String(64), visibility: FieldVisibility.Hidden);
        ancient = entry.AddField("Ancient", FieldTypeSchema.String(64), visibility: FieldVisibility.Hidden);
        metadata = p.Collection("RunActMetadata", entry); normal = p.Collection("RunActNormalQueue", entry);
        elite = p.Collection("RunActEliteQueue", entry); events = p.Collection("RunActEventQueue", entry);
        shared = p.Collection("RunActSharedAncients", entry);
        foreach (var collection in new[] { metadata, normal, elite, events, shared })
        { collection.IdentityVisible = false; collection.OrderVisible = false; }
    }

    internal Inst[] Initialize(IrScope s, Expr run, RunInitializationWiring init, EncounterPoolBindings pools,
        RunEncountersBindings visits, EncounterQueuesBindings enqueue)
    {
        var created = s.Local("actEntry", FieldTypeSchema.Binding());
        var used = s.Local("sharedAncientsAssigned", FieldTypeSchema.Int(0, 999));
        var count = s.Local("sharedAncientSubset", FieldTypeSchema.Int(0, 999));
        var cursor = s.Local("sharedAncientCursor", FieldTypeSchema.Int(0, 999));
        var steps = new List<Inst>(init.GenerateEventQueue(s, run, [], initializeShared: true));
        steps.Add(s.SetLocal(used, s.Const(0L)));
        // RunManager allocates ALL shared subsets before generating ANY act's rooms.
        foreach (var definition in plan.Acts.Skip(1))
        {
            steps.Add(s.SampleInt(VanillaRandomLayout.EngineStreamName("UpFront"), s.Const(0L),
                s.Add(s.Subtract(s.Count(s.Collection(init.SharedAncients)), s.LocalRef(used)), s.Const(1L)), count));
            steps.Add(s.SetLocal(cursor, s.Const(0L)));
            steps.Add(s.ForEach(s.Collection(init.SharedAncients), candidate => [
                s.If(s.And(s.GreaterOrEqual(s.LocalRef(cursor), s.LocalRef(used)),
                    s.Less(s.LocalRef(cursor), s.Add(s.LocalRef(used), s.LocalRef(count)))),
                    s.CreateEntity(entry, s.EntityScope(run), created,
                        [(act, s.Const((long)definition.Index)), (id, s.Field(candidate, init.RoomId))], shared)),
                s.SetLocal(cursor, s.Add(s.LocalRef(cursor), s.Const(1L)))]));
            steps.Add(s.SetLocal(used, s.Add(s.LocalRef(used), s.LocalRef(count))));
        }
        foreach (var definition in plan.Acts)
        {
            var pool = plan.Pool(definition);
            steps.Add(s.ForEach(s.Collection(pools.PoolEntries!), item => [s.Destroy(item)]));
            foreach (var encounter in pool.Encounters.Concat(pool.Elites))
                steps.Add(s.CreateEntity(pools.PoolEntry!, s.EntityScope(run), created,
                    [(pools.PoolEncounterId!, s.Const(encounter.EncounterId)), (pools.PoolKind!, s.Const(encounter.Kind)),
                     (pools.PoolBagUsed!, s.Const(false)), .. pools.PoolTagIds.Select(tag =>
                        (pools.PoolTags[tag], (Expr)s.Const(encounter.Tags.Contains(tag, StringComparer.Ordinal))))], pools.PoolEntries!));
            steps.AddRange(init.GenerateEventQueue(s, run, definition.Events, initializeShared: false));
            steps.Add(s.SetField(run, visits.RunHasLast!, s.Const(false)));
            for (var slot = 0; slot < pool.RoomCount; slot++)
                steps.Add(s.Call((slot < pool.WeakRoomCount ? enqueue.EnqueueWeak : enqueue.EnqueueRegular)!, s.Const((long)slot)));
            steps.Add(s.SetField(run, visits.RunHasLast!, s.Const(false)));
            for (var slot = 0; slot < pool.EliteRoomCount; slot++) steps.Add(s.Call(enqueue.EnqueueElite!, s.Const((long)slot)));
            steps.AddRange(init.GenerateBossAndAncient(s, run, pool, (scope, owner) => PickAncient(scope, owner, init, definition)));
            steps.Add(s.CreateEntity(entry, s.EntityScope(run), created,
                [(act, s.Const((long)definition.Index)), (id, s.Const(definition.Id)),
                 (boss, s.Field(run, init.Boss)), (ancient, s.Field(run, init.Ancient))], metadata));
            Archive(pools.EncounterQueue!, pools.QueueIndex!, pools.QueueEncounterId!, normal);
            Archive(pools.EliteEncounterQueue!, pools.EliteQueueIndex!, pools.EliteQueueEncounterId!, elite);
            Archive(init.Events, null, init.RoomId, events);

            void Archive(CollectionSymbol source, FieldSymbol? position, FieldSymbol identity, CollectionSymbol target)
            {
                steps.Add(s.SetLocal(cursor, s.Const(0L)));
                steps.Add(s.ForEach(s.Collection(source), item => [s.CreateEntity(entry, s.EntityScope(run), created,
                    [(act, s.Const((long)definition.Index)), (index, position is null ? s.LocalRef(cursor) : s.Field(item, position)),
                     (id, s.Field(item, identity))], target), s.SetLocal(cursor, s.Add(s.LocalRef(cursor), s.Const(1L))), s.Destroy(item)]));
            }
        }
        steps.AddRange(Activate(s, run, 0, init, pools));
        return [.. steps];
    }

    private Inst[] PickAncient(IrScope s, Expr run, RunInitializationWiring init, RunActDefinition definition)
    {
        var assigned = s.Where(s.Collection(shared), item => s.Equal(s.Field(item, act), s.Const((long)definition.Index)));
        var draw = s.Local("actAncientDraw", FieldTypeSchema.Int(0, 999));
        var cursor = s.Local("actAncientCursor", FieldTypeSchema.Int(0, 999));
        List<Inst> steps = [s.SampleInt(VanillaRandomLayout.EngineStreamName("UpFront"), s.Const(0L),
            s.Add(s.Const((long)definition.Ancients.Count), s.Count(assigned)), draw)];
        for (var i = 0; i < definition.Ancients.Count; i++) steps.Add(s.If(s.Equal(s.LocalRef(draw), s.Const((long)i)),
            s.SetField(run, init.Ancient, s.Const(definition.Ancients[i]))));
        steps.Add(s.SetLocal(cursor, s.Const((long)definition.Ancients.Count)));
        steps.Add(s.ForEach(assigned, item => [s.If(s.Equal(s.LocalRef(draw), s.LocalRef(cursor)),
            s.SetField(run, init.Ancient, s.Field(item, id))), s.SetLocal(cursor, s.Add(s.LocalRef(cursor), s.Const(1L)))]));
        return [.. steps];
    }

    private Inst[] Activate(IrScope s, Expr run, int actIndex, RunInitializationWiring init, EncounterPoolBindings pools)
    {
        var created = s.Local("activeActEntry", FieldTypeSchema.Binding());
        List<Inst> steps = [s.SetField(run, map.RunActIndex!, s.Const((long)actIndex)),
            s.SetField(run, ActId, s.Const(plan.Acts[actIndex].Id)),
            s.ForEach(s.Where(s.Collection(metadata), item => s.Equal(s.Field(item, act), s.Const((long)actIndex))), item =>
                [s.SetField(run, init.Boss, s.Field(item, boss)), s.SetField(run, init.Ancient, s.Field(item, ancient))])];
        Copy(normal, pools.EncounterQueue!, pools.QueueEntry!, pools.QueueIndex, pools.QueueEncounterId!);
        Copy(elite, pools.EliteEncounterQueue!, pools.EliteQueueEntry!, pools.EliteQueueIndex, pools.EliteQueueEncounterId!);
        Copy(events, init.Events, init.RoomIdentity, null, init.RoomId);
        return [.. steps];
        void Copy(CollectionSymbol source, CollectionSymbol target, EntityTypeSymbol type, FieldSymbol? position, FieldSymbol identity)
        {
            steps.Add(s.ForEach(s.Collection(target), item => [s.Destroy(item)]));
            steps.Add(s.ForEach(s.Where(s.Collection(source), item => s.Equal(s.Field(item, act), s.Const((long)actIndex))), item =>
                [s.CreateEntity(type, s.EntityScope(run), created,
                    [.. position is null ? Array.Empty<(FieldSymbol, Expr)>() : [(position, s.Field(item, index))],
                     (identity, s.Field(item, id))], target)]));
        }
    }

    internal Inst[] Continue(IrScope s, Expr run, RunInitializationWiring init, EncounterPoolBindings pools,
        RunEncountersBindings visits, UnknownRoomsBindings unknown, RunStateBindings state, Func<IrScope, Inst[]> clearCombat)
    {
        var steps = new List<Inst> { s.If(s.Equal(s.Field(run, map.RunActIndex!), s.Const((long)plan.Acts.Count - 1)),
            s.Fail("base-spire: default acts completed; TheArchitect final event is not implemented")) };
        for (var i = plan.Acts.Count - 1; i >= 1; i--)
        {
            var next = i;
            steps.Add(s.If(s.Equal(s.Field(run, map.RunActIndex!), s.Const((long)i - 1)), [
                .. clearCombat(s),
                s.SetField(run, map.RunCompletedFloors!, s.Add(s.Field(run, map.RunCompletedFloors!),
                    s.Count(s.Where(s.Collection(map.Nodes), node => s.Field(node, map.NodeVisited))))),
                s.SetField(run, map.RunCurrentNode, s.Const(Value.None)),
                s.ForEach(s.Collection(map.Nodes), node => [s.Destroy(node)]),
                .. Activate(s, run, next, init, pools),
                s.SetField(run, visits.RunNormalVisited!, s.Const(0L)), s.SetField(run, visits.RunEliteVisited!, s.Const(0L)),
                s.SetField(run, unknown.EventState.EventsVisited, s.Const(0L)), s.SetField(run, unknown.EventState.CurrentEventId, s.Const(string.Empty)),
                s.SetField(run, state.RunEncounter!, s.Const(string.Empty)),
                s.SetField(run, unknown.Odds.MonsterOdds, s.Const((long)UnknownMapPointOdds.BitsOf(UnknownMapPointOdds.BaseMonsterOdds))),
                s.SetField(run, unknown.Odds.EliteOdds, s.Const((long)UnknownMapPointOdds.BitsOf(UnknownMapPointOdds.BaseEliteOdds))),
                s.SetField(run, unknown.Odds.TreasureOdds, s.Const((long)UnknownMapPointOdds.BitsOf(UnknownMapPointOdds.BaseTreasureOdds))),
                s.SetField(run, unknown.Odds.ShopOdds, s.Const((long)UnknownMapPointOdds.BitsOf(UnknownMapPointOdds.BaseShopOdds))),
                s.SetField(run, map.RunMapFloor, s.Const(0L)), s.SetField(run, map.RunTotalFloor!, s.Field(run, map.RunCompletedFloors!)),
                s.SetField(run, state.RunRoomCount!, s.Const((long)plan.Acts[next].NormalRooms + 1)),
                s.SetField(run, map.RunActualRoomType, s.EnumConst(map.RoomType, "Unassigned")),
                s.SetField(run, map.RunActCompleted, s.Const(false)), s.SetField(run, map.AncientPending!, s.Const(true)),
                .. map.SetupSteps(s, unknown.EventContext.Room.Runs, plan.Acts[next].NormalRooms, next),
            ]));
        }
        return [.. steps];
    }

    internal Inst[] AncientBoundary(IrScope s, Expr run, RunInitializationWiring init)
    {
        var names = plan.Acts.SelectMany(a => a.Ancients).Concat(plan.SharedAncients).Distinct();
        return [.. names.Select(name => s.If(s.Equal(s.Field(run, init.Ancient), s.Const(name)),
            s.Fail($"base-spire: ancient event {name} is not implemented"))), s.Fail("base-spire: unknown ancient identity")];
    }
}
