using CoreTheSpire.Engine;

namespace BaseSpire.Slice;

/// <summary>Run-owned inventories, populated by the Engine Start program, never by a room.
/// Source: RunManager.InitializeNewRun; RelicGrabBag.Populate. Current factory policy is
/// Ironclad, all unlocks, single player. Identity inventory is independent of effect support.</summary>
public sealed class RunInitializationWiring
{
    public EntityTypeSymbol SharedEntry { get; }
    public FieldSymbol SharedId { get; }
    public IReadOnlyDictionary<string, CollectionSymbol> SharedBags { get; }
    public EntityTypeSymbol PlayerEntry { get; }
    public FieldSymbol PlayerId { get; }
    public IReadOnlyDictionary<string, CollectionSymbol> PlayerBags { get; }
    public FieldSymbol Boss { get; }
    public FieldSymbol Ancient { get; }
    public CollectionSymbol Events { get; }
    public CollectionSymbol SharedAncients { get; }

    /// <summary>
    /// 队列条目实体种类（源 `RoomSet.events` 里装的是事件模型，其身份就是 `ModelId`）。
    /// ENV-T09-unknown-room-modules-20261012：事件访问生命周期模块用**同一种**身份实体记录
    /// 已访问事件（源 `RunState._visitedEventIds`），因此这里暴露符号，不另造一套身份类型。
    /// </summary>
    public EntityTypeSymbol RoomIdentity => roomIdentity;

    /// <summary>队列条目上的源身份字段（源 `ModelId.Entry` 对应的类名形态）。</summary>
    public FieldSymbol RoomId => roomId;

    private readonly EntityTypeSymbol roomIdentity;
    private readonly FieldSymbol roomId;
    private readonly FieldSymbol populated;
    // IroncladRelicPool.GenerateAllRelics source order; BurningBlood is Starter and
    // excluded by RelicGrabBag._rarities, not by effect availability.
    private static readonly SharedRelicPoolEntry[] CharacterRelics =
    [
        new("Brimstone", "Shop"), new("CharonsAshes", "Rare"),
        new("DemonTongue", "Rare"), new("PaperPhrog", "Uncommon"),
        new("RedSkull", "Common"), new("RuinedHelmet", "Rare"),
        new("SelfFormingClay", "Uncommon"),
    ];
    private static readonly IReadOnlyList<SharedRelicPoolEntry> PlayerPool = SharedRelicPoolManifest.Entries
        .Concat(CharacterRelics).Where(r => r.Rarity is "Common" or "Uncommon" or "Rare" or "Shop").ToArray();

    public RunInitializationWiring(EnginePackageBuilder package, EntityTypeSymbol run)
    {
        Boss = run.AddField("GeneratedBoss", FieldTypeSchema.String(64), visibility: FieldVisibility.Hidden);
        Ancient = run.AddField("GeneratedAncient", FieldTypeSchema.String(64), visibility: FieldVisibility.Hidden);
        roomIdentity = package.EntityType("RunRoomIdentity");
        roomId = roomIdentity.AddField("SourceId", FieldTypeSchema.String(64), visibility: FieldVisibility.Hidden);
        Events = package.Collection("RunEventQueue", roomIdentity);
        SharedAncients = package.Collection("RunSharedAncients", roomIdentity);
        populated = run.AddField("RelicBagsPopulated", FieldTypeSchema.Bool(), visibility: FieldVisibility.Hidden);
        SharedEntry = package.EntityType("SharedRelicBagEntry");
        SharedId = SharedEntry.AddField("RelicId", FieldTypeSchema.String(64), visibility: FieldVisibility.Hidden);
        SharedBags = Declare("Shared", SharedEntry, SharedRelicPoolManifest.Entries);
        PlayerEntry = package.EntityType("PlayerRelicBagEntry");
        PlayerId = PlayerEntry.AddField("RelicId", FieldTypeSchema.String(64), visibility: FieldVisibility.Hidden);
        PlayerBags = Declare("Player", PlayerEntry, PlayerPool);

        IReadOnlyDictionary<string, CollectionSymbol> Declare(string prefix, EntityTypeSymbol entry,
            IReadOnlyList<SharedRelicPoolEntry> pool) => pool.Select(r => r.Rarity).Distinct(StringComparer.Ordinal)
            .ToDictionary(r => r, r => package.Collection(prefix + "RelicBag" + r, entry), StringComparer.Ordinal);
    }

    public Inst[] Setup(IrScope s, Expr run)
    {
        var steps = new List<Inst>();
        steps.Add(s.If(s.Field(run, populated), s.Fail("New Run relic bags were already populated")));
        Populate(SharedEntry, SharedId, SharedBags, SharedRelicPoolManifest.Entries, "sharedRelicEntry");
        Populate(PlayerEntry, PlayerId, PlayerBags, PlayerPool, "playerRelicEntry");
        steps.Add(s.SetField(run, populated, s.Const(true)));
        return [.. steps];

        void Populate(EntityTypeSymbol entry, FieldSymbol id, IReadOnlyDictionary<string, CollectionSymbol> bags,
            IReadOnlyList<SharedRelicPoolEntry> pool, string local)
        {
            var created = s.Local(local, FieldTypeSchema.RefTo(entry));
            foreach (var (rarity, bag) in bags)
            {
                foreach (var relic in pool.Where(r => r.Rarity == rarity))
                    steps.Add(s.CreateEntity(entry, s.EntityScope(run), created,
                        [(id, s.Const(TreasureRoomWiring.ContentId(relic)))], bag));
                steps.Add(s.ShuffleCollection(bag, VanillaRandomLayout.EngineStreamName("UpFront")));
            }
        }
    }

    public Inst[] GenerateEventQueue(IrScope s, Expr run)
    {
        var created = s.Local("runRoomIdentity", FieldTypeSchema.Binding());
        var steps = new List<Inst>
        {
            // RunManager.GenerateRooms shuffles shared ancients first. Darv is only assigned
            // to later acts, not Overgrowth; a singleton shuffle naturally consumes zero.
            s.CreateEntity(roomIdentity, s.EntityScope(run), created, [(roomId, s.Const("Darv"))], SharedAncients),
            s.ShuffleCollection(SharedAncients, VanillaRandomLayout.EngineStreamName("UpFront")),
        };
        foreach (var id in RunSourcePoolManifest.Events)
            steps.Add(s.CreateEntity(roomIdentity, s.EntityScope(run), created, [(roomId, s.Const(id))], Events));
        steps.Add(s.ShuffleCollection(Events, VanillaRandomLayout.EngineStreamName("UpFront")));
        return [.. steps];
    }

    /// <summary>`ActModel.GenerateRooms` picks the boss and the act ancient with one bounded draw each,
    /// both on the run's `up_front` stream, in that order. The draw index selects an identity from the
    /// source pool; the drawn value is never discarded and the pool is never filtered by effect support.</summary>
    public Inst[] GenerateBossAndAncient(IrScope s, Expr run, SliceEncounterPool pool)
    {
        var steps = new List<Inst>();
        Pick(pool.PoolStream, [.. pool.Bosses.Select(boss => boss.EncounterId)], Boss, "runBossIndex", "boss");
        Pick(pool.PoolStream, RunSourcePoolManifest.Ancients, Ancient, "runAncientIndex", "ancient");
        return [.. steps];

        void Pick(string stream, IReadOnlyList<string> identities, FieldSymbol target, string local, string kind)
        {
            if (identities.Count == 0)
            {
                steps.Add(s.Fail($"The new Run has no {kind} identities to draw from: the source pool is empty"));
                return;
            }

            var drawn = s.Local(local, FieldTypeSchema.Int(0, 999));
            steps.Add(s.SampleInt(stream, s.Const(0L), s.Const((long)identities.Count), drawn));
            for (var i = 0; i < identities.Count; i++)
                steps.Add(s.If(s.Equal(s.LocalRef(drawn), s.Const((long)i)),
                    s.SetField(run, target, s.Const(identities[i]))));
        }
    }

    /// <summary>Remove an obtained identity from the player's authoritative inventory.
    /// Shared treasure pulls already remove their own entry; player and shared bags are distinct in source.</summary>
    public Inst[] RemoveFromPlayer(IrScope s, Expr id) => PlayerBags.Values.Select(bag =>
        s.ForEach(s.Where(s.Collection(bag), entry => s.Equal(s.Field(entry, PlayerId), id)),
            entry => new[] { s.Destroy(entry) })).ToArray();

    public Inst[] RemoveFromShared(IrScope s, Expr id) => SharedBags.Values.Select(bag =>
        s.ForEach(s.Where(s.Collection(bag), entry => s.Equal(s.Field(entry, SharedId), id)),
            entry => new[] { s.Destroy(entry) })).ToArray();
}
