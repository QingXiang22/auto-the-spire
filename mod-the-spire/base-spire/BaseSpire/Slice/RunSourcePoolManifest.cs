using BaseSpire.SinglePlayer;

namespace BaseSpire.Slice;

/// <summary>Frozen Overgrowth source identity pool, independent of executable enemy support.
/// Source: sts2/src/Core/Models/Acts/Overgrowth.cs GenerateAllEncounters.
/// Existing encounter declarations supply formations/tags; missing boss identities are retained.
/// Factory supports all unlocks only; no epoch filter is applied for that policy.</summary>
public static class RunSourcePoolManifest
{
    /// <summary>本包冻结的幕池声明（`Content/base-spire.acts.json` 的 Overgrowth 条目）。精英池与精英
    /// 队列长度都取自它：源 `AllEliteEncounters` 与 `GenerateRooms` 的固定 15 次循环。</summary>
    private static ActPoolDeclaration Overgrowth => EncounterContent.LoadActPools()
        .Single(a => a.ActId == "base-spire:overgrowth-act");

    /// <summary>精英遭遇池：源 `AllEliteEncounters` 的声明顺序
    /// （BygoneEffigyElite, ByrdonisElite, PhrogParasiteElite）。它与弱/普通池是独立库存。</summary>
    public static IReadOnlyList<SliceEncounter> Elites { get; } =
        Load(Overgrowth.EliteEncounters, SliceEncounterPool.EliteKind);
    // Overgrowth.AllAncients (Neow) after the epoch filter; the factory runs with all unlocks and
    // also draws shared ancients, but the act pool itself is this single identity (ModelDb.AllSharedAncients
    // only supplies Darv for later acts). Order is the act's own declaration order, not the shared list.
    public static IReadOnlyList<string> Ancients { get; } = ["Neow"];
    public static SliceEncounterPool Complete(SliceEncounterPool supported)
    {
        var act = Overgrowth;
        return supported with
        {
            WeakEncounters = Load(act.WeakNormalEncounters, SliceEncounterPool.WeakKind),
            RegularEncounters = Load(act.RegularEncounters, SliceEncounterPool.NormalKind),
            BossEncounters = Load(["base-spire:ceremonial-beast-boss", "base-spire:the-kin-boss", "base-spire:vantom-boss"], "boss"),
            // 精英库存与它的队列长度同样来自幕声明；这条接入不是"额外内容"，是源本来就在
            // GenerateRooms 里预生成的独立列表（此前被错误地追加进普通队列）。
            EliteEncounters = Elites,
            EliteRoomCount = act.EliteQueueCount,
        };
    }
    private static IReadOnlyList<SliceEncounter> Load(IReadOnlyList<string> ids, string kind)
    {
        var declarations = EncounterContent.LoadEncounters().ToDictionary(e => e.Id, StringComparer.Ordinal);
        return ids.Select(id => declarations.TryGetValue(id, out var d)
            ? new SliceEncounter(id, kind, d.EnemyIds, d.Tags, d.Formations, d.Slots)
            : new SliceEncounter(id, kind, [], [], [], [])).ToArray();
    }
    // Overgrowth.AllEvents followed by ModelDb.AllSharedEvents, source enumeration order.
    public static IReadOnlyList<string> Events { get; } =
    [
        "AromaOfChaos", "ByrdonisNest", "DenseVegetation", "JungleMazeAdventure", "LuminousChoir",
        "MorphicGrove", "SapphireSeed", "SunkenStatue", "TabletOfTruth", "UnrestSite", "Wellspring",
        "WhisperingHollow", "WoodCarvings", "BrainLeech", "CrystalSphere", "DollRoom", "FakeMerchant",
        "PotionCourier", "RanwidTheElder", "RelicTrader", "RoomFullOfCheese", "SelfHelpBook", "SlipperyBridge",
        "StoneOfAllTime", "Symbiote", "TeaMaster", "TheFutureOfPotions", "TheLegendsWereTrue", "ThisOrThat",
        "WarHistorianRepy", "WelcomeToWongos",
    ];
}
