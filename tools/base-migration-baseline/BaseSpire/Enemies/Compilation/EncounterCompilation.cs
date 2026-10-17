using CoreTheSpire.Engine;
using BaseSpire.Slice.Effects;
using BaseSpire.Slice.Orchestration;
using static BaseSpire.Slice.Compilation.CompilationCallbacks;
using static BaseSpire.Slice.CombatSlicePackage;

namespace BaseSpire.Slice.Compilation;

internal static class EncounterCompilation
{
    internal static EncounterPoolBindings EncounterPool(PackageBuildOptions options, ContentBindings contentBindings)
    {

        // BASE-01：遭遇池本身作为**包内实例**声明（每个池成员一个实例）；整幕队列在幕开始时一次生成�?
        // 池是规则**内容**（可共享），队列�?*局内状�?*（每局一份）——两者必须分开�?
        var poolEntry = options.EncounterPool is not null ? contentBindings.Package.EntityType("EncounterPoolEntry") : null;

        var poolEncounterId = poolEntry?.AddField("EncounterId", FieldTypeSchema.String(64));

        var poolKind = poolEntry?.AddField("Kind", FieldTypeSchema.String(16));

        var poolEntries = poolEntry is not null ? contentBindings.Package.Collection("EncounterPool", poolEntry) : null;

        // 整幕队列用的抓取状态：`BagUsed` = 当前这一袋里"还没被取�?（空袋时按池顺序重新装袋）；
        // `QueueIndex` = 这个成员被排进整幕队列的第几间房�?1 = 没进队）�?
        // 两者都�?*内部状�?*（隐藏字段），公开事实仍是"这一间房遇到�?�?
        var poolBagUsed = poolEntry?.AddField("BagUsed", FieldTypeSchema.Bool(), visibility: FieldVisibility.Hidden);

        // **整幕队列**：一个槽一个实例（`Index` = 第几间房，从 0 起）。池成员可以被重复排进队�?
        // （袋子会重新装填），因此队列不能记在池成员上——那�?袋子状�?，不是队列�?
        var queueEntry = options.EncounterPool is not null ? contentBindings.Package.EntityType("EncounterQueueEntry") : null;

        var queueIndex = queueEntry?.AddField("Index", FieldTypeSchema.Int(0, 999), visibility: FieldVisibility.Hidden);

        var queueEncounterId = queueEntry?.AddField("EncounterId", FieldTypeSchema.String(64));

        var encounterQueue = queueEntry is not null
            ? contentBindings.Package.Collection("EncounterQueue", queueEntry)
            : null;

        // **精英队列**：与普通队列结构相同但**完全独立**（源 `RoomSet.normalEncounters` /
        // `eliteEncounters` 是两个列表，各有自己的 `NextXEncounter` per-type 计数与"上一个"）。
        // 只在 `initializeNewRun`（工厂的整局初始化）时声明：受控切片与既有夹具的包形状不变。
        var eliteQueueEntry = options.EncounterPool is not null && options.InitializeNewRun
            ? contentBindings.Package.EntityType("EliteEncounterQueueEntry")
            : null;

        var eliteQueueIndex = eliteQueueEntry?.AddField("Index", FieldTypeSchema.Int(0, 999),
            visibility: FieldVisibility.Hidden);

        var eliteQueueEncounterId = eliteQueueEntry?.AddField("EncounterId", FieldTypeSchema.String(64));

        var eliteEncounterQueue = eliteQueueEntry is not null
            ? contentBindings.Package.Collection("EliteEncounterQueue", eliteQueueEntry)
            : null;

        // 标签回避：把每个遭遇的标签集展开�?*构建期常�?*布尔字段（源�?`Tags.Intersect(...).Any()`）�?
        var poolTagIds = options.EncounterPool is null ? (IReadOnlyList<string>)[] : options.EncounterPool.Tags;

        var poolTags = poolEntry is null
            ? new Dictionary<string, FieldSymbol>(StringComparer.Ordinal)
            : poolTagIds.ToDictionary(tag => tag,
                tag => poolEntry.AddField($"Tag_{Identifier(tag)}", FieldTypeSchema.Bool(),
                    visibility: FieldVisibility.Hidden),
                StringComparer.Ordinal);

        return new EncounterPoolBindings(poolEntry, poolEncounterId, poolKind, poolEntries, poolBagUsed, queueEntry, queueIndex, queueEncounterId, encounterQueue, eliteQueueEntry, eliteQueueIndex, eliteQueueEncounterId, eliteEncounterQueue, poolTagIds, poolTags);
    }

    internal static RunEncountersBindings RunEncounters(
        RunStateBindings runStateBindings,
        EncounterPoolBindings encounterPoolBindings)
    {
        // 队列生成过程中的"上一个入队遭�?（源�?`encounters.LastOrDefault()`）：标签回避要拿它比�?
        // 生成只在幕开始时跑一次，但两个抓取程序之间要共享它，因此放在 Run 上（隐藏字段）�?
        var runHasLast = runStateBindings.RunEncounter is not null && encounterPoolBindings.PoolEntry is not null
            ? runStateBindings.RunEntity!.AddField("HasLastEncounter", FieldTypeSchema.Bool(), visibility: FieldVisibility.Hidden)
            : null;

        var runLastEncounter = runStateBindings.RunEncounter is not null && encounterPoolBindings.PoolEntry is not null
            ? runStateBindings.RunEntity!.AddField("LastEncounterId", FieldTypeSchema.String(64), visibility: FieldVisibility.Hidden)
            : null;

        var runLastTags = runStateBindings.RunEncounter is not null && encounterPoolBindings.PoolEntry is not null
            ? encounterPoolBindings.PoolTagIds.ToDictionary(tag => tag,
                tag => runStateBindings.RunEntity!.AddField($"LastTag_{Identifier(tag)}", FieldTypeSchema.Bool(),
                    visibility: FieldVisibility.Hidden),
                StringComparer.Ordinal)
            : new Dictionary<string, FieldSymbol>(StringComparer.Ordinal);

        // **per-type 访问计数**（源 `RoomSet.normalEncountersVisited` / `eliteEncountersVisited`）：
        // 消费是 `队列[visited % count]`，不是"用全局房间号查表"。两者独立推进：精英房不动普通计数，
        // 普通战斗房不动精英计数；BOSS/营火/宝箱/事件都不推进（源 `MarkVisited` 的分支）。
        var runNormalVisited = runStateBindings.RunEncounter is not null
            ? runStateBindings.RunEntity!.AddField("NormalEncountersVisited", FieldTypeSchema.Int(0, 999),
                visibility: FieldVisibility.Hidden)
            : null;

        var runEliteVisited = encounterPoolBindings.EliteEncounterQueue is not null
            ? runStateBindings.RunEntity!.AddField("EliteEncountersVisited", FieldTypeSchema.Int(0, 999),
                visibility: FieldVisibility.Hidden)
            : null;

        return new RunEncountersBindings(runHasLast, runLastEncounter, runLastTags, runNormalVisited, runEliteVisited);
    }

    internal static EncounterQueuesBindings EncounterQueues(
        PackageBuildOptions options,
        EncounterPoolBindings encounterPoolBindings,
        EnqueueProcedureCallback EnqueueProcedure)
    {

        var enqueueWeak = options.EncounterPool is null || encounterPoolBindings.EncounterQueue is null || encounterPoolBindings.QueueEntry is null
            || encounterPoolBindings.QueueIndex is null || encounterPoolBindings.QueueEncounterId is null
            ? null
            : EnqueueProcedure(SliceEncounterPool.WeakKind, encounterPoolBindings.EncounterQueue, encounterPoolBindings.QueueEntry, encounterPoolBindings.QueueIndex, encounterPoolBindings.QueueEncounterId);

        var enqueueRegular = options.EncounterPool is null || encounterPoolBindings.EncounterQueue is null || encounterPoolBindings.QueueEntry is null
            || encounterPoolBindings.QueueIndex is null || encounterPoolBindings.QueueEncounterId is null
            ? null
            : EnqueueProcedure(SliceEncounterPool.NormalKind, encounterPoolBindings.EncounterQueue, encounterPoolBindings.QueueEntry, encounterPoolBindings.QueueIndex, encounterPoolBindings.QueueEncounterId);

        // 精英段的抓取程序与普通段是**两个独立程序**：各自的成员集合（按 `Kind` 过滤）、各自的袋子、
        // 各自的入队集合。它们复用 Run 上的"上一个"字段，但按源的相位顺序（普通两段跑完再跑精英段）
        // 由调用方在精英段开始前清空它（源里 `_rooms.eliteEncounters` 在自己这一段开始时也是空的）。
        var enqueueElite = options.InitializeNewRun && encounterPoolBindings.EliteEncounterQueue is not null && encounterPoolBindings.EliteQueueEntry is not null
            && encounterPoolBindings.EliteQueueIndex is not null && encounterPoolBindings.EliteQueueEncounterId is not null
            ? EnqueueProcedure(SliceEncounterPool.EliteKind, encounterPoolBindings.EliteEncounterQueue, encounterPoolBindings.EliteQueueEntry,
                encounterPoolBindings.EliteQueueIndex, encounterPoolBindings.EliteQueueEncounterId)
            : null;

        return new EncounterQueuesBindings(enqueueWeak, enqueueRegular, enqueueElite);
    }

    internal static ProcedureSymbol? EnqueueProcedure(
        PackageBuildOptions options,
        ContentBindings contentBindings,
        EncounterPoolBindings encounterPoolBindings,
        RunEncountersBindings runEncountersBindings,
        RunCollectionsBindings runCollectionsBindings,
        string kind,
        CollectionSymbol queue,
        EntityTypeSymbol queueEntryType,
        FieldSymbol queueIndexField,
        FieldSymbol queueEncounterIdField)
    {
        const int BagRetryLimit = 256;
        if (options.EncounterPool is null || encounterPoolBindings.PoolEntries is null || encounterPoolBindings.PoolKind is null || runCollectionsBindings.RunStates is null
            || encounterPoolBindings.PoolBagUsed is null
            || encounterPoolBindings.PoolEncounterId is null || runEncountersBindings.RunHasLast is null
            || runEncountersBindings.RunLastEncounter is null)
        {
            return null;
        }

        var procedure = contentBindings.Package.Procedure($"EnqueueEncounter_{Identifier(kind)}");
        var roomIndex = procedure.Param("RoomIndex", FieldTypeSchema.Int(0, 999));
        var run = procedure.First(procedure.Collection(runCollectionsBindings.RunStates));
        // **这一相位的池**：袋子只装本相位（弱 / 普通）的成员——两个相位各有自己的袋子
        // （`ActModel.GenerateRooms` �?grabBag �?grabBag2 是两个独立实例）�?
        var members = procedure.Where(procedure.Collection(encounterPoolBindings.PoolEntries),
            entry => (Expr)procedure.Equal(procedure.Field(entry, encounterPoolBindings.PoolKind), procedure.Const(kind)));
        var bagged = procedure.Where(members,
            entry => (Expr)procedure.Not(procedure.Field(entry, encounterPoolBindings.PoolBagUsed)));
        var created = procedure.Local("bagCreated", FieldTypeSchema.Binding());
        var total = procedure.Local("bagTotal", FieldTypeSchema.Int(0, 999));
        var cursor = procedure.Local("bagCursor", FieldTypeSchema.Int(0, 999));
        var drawn = procedure.Local("bagDrawn", FieldTypeSchema.Int(0, 999));
        var candidate = procedure.Local("bagCandidate", FieldTypeSchema.Binding());
        var picked = procedure.Local("bagPicked", FieldTypeSchema.Binding());
        var eligible = procedure.Local("bagEligible", FieldTypeSchema.Bool());
        var found = procedure.Local("bagHasEligible", FieldTypeSchema.Bool());
        var done = procedure.Local("bagDone", FieldTypeSchema.Bool());

        /// <summary>`Tags.Intersect(other.Tags).Any()`：构建期常量比较，不需�?bit 运算�?/summary>
        Expr SharesTags(Expr entry) => encounterPoolBindings.PoolTagIds.Count == 0
            ? procedure.Const(false)
            : encounterPoolBindings.PoolTagIds
                .Select(tag => (Expr)procedure.And(procedure.Field(entry, encounterPoolBindings.PoolTags[tag]),
                    procedure.Field(run, runEncountersBindings.RunLastTags[tag])))
                .Aggregate(procedure.Or);

        /// <summary>谓词求值：把结果写�?`eligible`�?上一�?为空时谓词恒真）�?/summary>
        IEnumerable<Inst> SetEligible(Expr entry)
        {
            yield return procedure.SetLocal(eligible, procedure.Const(true));
            yield return procedure.If(procedure.Field(run, runEncountersBindings.RunHasLast),
                procedure.SetLocal(eligible, procedure.And(
                    procedure.NotEqual(procedure.Field(entry, encounterPoolBindings.PoolEncounterId),
                        procedure.Field(run, runEncountersBindings.RunLastEncounter)),
                    procedure.Not(SharesTags(entry)))));
        }

        /// <summary>走到袋里�?`drawn` �?还没取走"的成员（权重�?1 �?下标就是它）�?/summary>
        IEnumerable<Inst> WalkToDrawn()
        {
            yield return procedure.SetLocal(cursor, procedure.Const(0L));
            yield return procedure.ForEach(bagged, entry => new Inst[]
            {
                    procedure.If(procedure.Equal(procedure.LocalRef(cursor), procedure.LocalRef(drawn)),
                        procedure.SetLocal(candidate, entry)),
                    procedure.SetLocal(cursor,
                        procedure.Add(procedure.LocalRef(cursor), procedure.Const(1L))),
            });
        }

        var retryBody = new List<Inst>
            {
                procedure.SampleInt(options.EncounterPool.PoolStream, procedure.Const(0L), procedure.LocalRef(total), drawn),
            };
        retryBody.AddRange(WalkToDrawn());
        retryBody.AddRange(SetEligible(procedure.LocalRef(candidate)));
        retryBody.Add(procedure.If(procedure.LocalRef(eligible),
            procedure.SetLocal(picked, procedure.LocalRef(candidate)),
            procedure.SetLocal(done, procedure.Const(true))));

        var unordered = new List<Inst>
            {
                procedure.SampleInt(options.EncounterPool.PoolStream, procedure.Const(0L), procedure.LocalRef(total), drawn),
            };
        unordered.AddRange(WalkToDrawn());
        unordered.Add(procedure.SetLocal(picked, procedure.LocalRef(candidate)));

        var body = new List<Inst>
            {
                // �?空袋 �?按池的枚举顺序重新装袋（权重 1.0，不洗牌、不抽数）�?
                procedure.If(procedure.IsNone(procedure.First(bagged)),
                    procedure.ForEach(members, entry => new[]
                    {
                        procedure.SetField(entry, encounterPoolBindings.PoolBagUsed, procedure.Const(false)),
                    })),
                // �?袋里的成员数 = 下标映射的上界（`GrabBag._totalWeight`，权重全 1）�?
                procedure.SetLocal(total, procedure.Count(bagged)),
                // �?谓词预扫：没有任何成员满�?�?这次抓取一次也不抽�?
                procedure.SetLocal(found, procedure.Const(false)),
                procedure.ForEach(bagged, entry => SetEligible(entry).Append(
                    procedure.If(procedure.LocalRef(eligible), procedure.SetLocal(found, procedure.Const(true))))),
                procedure.SetLocal(done, procedure.Const(false)),
                procedure.SetLocal(picked, procedure.Const(Value.None)),
                // �?重抽（每次尝试一次抽取）或（预扫失败时）一次无约束抽取�?
                procedure.IfElse(
                    procedure.LocalRef(found),
                    new[]
                    {
                        procedure.Repeat(procedure.Const(BagRetryLimit),
                            procedure.If(procedure.Not(procedure.LocalRef(done)), retryBody.ToArray())),
                    },
                    unordered.ToArray()),
                procedure.If(procedure.IsNone(procedure.LocalRef(picked)),
                    procedure.Fail($"整幕队列：{kind} 池在 {BagRetryLimit} 次重抽内没有取到可用遭遇"
                        + "（预扫保证至少一个可用成员，走到这里说明状态与池不一致）")),
                // �?出袋 + 入队（一个槽一个实例）+ 记住"上一�?（标签集一并记下，供下一次谓词比较）�?
                procedure.SetField(procedure.LocalRef(picked), encounterPoolBindings.PoolBagUsed, procedure.Const(true)),
                procedure.CreateEntity(queueEntryType, procedure.EntityScope(run), created,
                    [
                        (queueIndexField, procedure.Param(roomIndex)),
                        (queueEncounterIdField, procedure.Field(procedure.LocalRef(picked), encounterPoolBindings.PoolEncounterId)),
                    ], queue),
                procedure.SetField(run, runEncountersBindings.RunHasLast, procedure.Const(true)),
                procedure.SetField(run, runEncountersBindings.RunLastEncounter,
                    procedure.Field(procedure.LocalRef(picked), encounterPoolBindings.PoolEncounterId)),
            };
        body.AddRange(encounterPoolBindings.PoolTagIds.Select(tag => procedure.SetField(run, runEncountersBindings.RunLastTags[tag],
            procedure.Field(procedure.LocalRef(picked), encounterPoolBindings.PoolTags[tag]))));
        procedure.Body(body.ToArray());
        return procedure.Symbol;
    }

}
