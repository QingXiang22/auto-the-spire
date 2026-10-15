namespace BaseSpire.Slice;

/// <summary>Act treasure shared relic identity metadata. Effects are separate from pool identity.</summary>
public sealed record SharedRelicPoolEntry(string SourceName, string Rarity);

/// <summary>
/// Source-order metadata from sts2 SharedRelicPool.GenerateAllRelics (118 entries).
/// The table intentionally contains identity and rarity only; relic effects are declared separately.
/// </summary>
public static class SharedRelicPoolManifest
{
    public static IReadOnlyList<SharedRelicPoolEntry> Entries { get; } =
    [
        new("Akabeko", "Uncommon"), new("AmethystAubergine", "Common"), new("Anchor", "Common"),
        new("ArtOfWar", "Rare"), new("BagOfMarbles", "Common"), new("BagOfPreparation", "Common"),
        new("BeatingRemnant", "Rare"), new("Bellows", "Rare"), new("BeltBuckle", "Shop"),
        new("BloodVial", "Common"), new("BookOfFiveRings", "Common"), new("BowlerHat", "Uncommon"),
        new("Bread", "Shop"), new("BronzeScales", "Common"), new("BurningSticks", "Shop"),
        new("Candelabra", "Uncommon"), new("CaptainsWheel", "Rare"), new("Cauldron", "Shop"),
        new("CentennialPuzzle", "Common"), new("Chandelier", "Rare"), new("ChemicalX", "Shop"),
        new("CloakClasp", "Rare"), new("DingyRug", "Shop"), new("DollysMirror", "Shop"),
        new("DragonFruit", "Shop"), new("EternalFeather", "Uncommon"), new("FestivePopper", "Common"),
        new("FresnelLens", "Event"), new("FrozenEgg", "Rare"), new("GamblingChip", "Rare"),
        new("GamePiece", "Rare"), new("GhostSeed", "Shop"), new("Girya", "Rare"),
        new("GnarledHammer", "Shop"), new("Gorget", "Common"), new("GremlinHorn", "Uncommon"),
        new("HappyFlower", "Common"), new("HornCleat", "Uncommon"), new("IceCream", "Rare"),
        new("IntimidatingHelmet", "Rare"), new("JossPaper", "Uncommon"), new("JuzuBracelet", "Common"),
        new("Kifuda", "Shop"), new("Kunai", "Rare"), new("Kusarigama", "Uncommon"),
        new("Lantern", "Common"), new("LastingCandy", "Uncommon"), new("LavaLamp", "Shop"),
        new("LeesWaffle", "Shop"), new("LetterOpener", "Uncommon"), new("LizardTail", "Rare"),
        new("LoomingFruit", "Ancient"), new("LuckyFysh", "Uncommon"), new("Mango", "Rare"),
        new("MealTicket", "Common"), new("MeatOnTheBone", "Rare"), new("MembershipCard", "Shop"),
        new("MercuryHourglass", "Uncommon"), new("MiniatureCannon", "Uncommon"), new("MiniatureTent", "Shop"),
        new("MoltenEgg", "Rare"), new("MummifiedHand", "Rare"), new("MysticLighter", "Shop"),
        new("Nunchaku", "Uncommon"), new("OddlySmoothStone", "Common"), new("OldCoin", "Rare"),
        new("Orichalcum", "Uncommon"), new("OrnamentalFan", "Uncommon"), new("Orrery", "Shop"),
        new("Pantograph", "Uncommon"), new("ParryingShield", "Uncommon"), new("Pear", "Uncommon"),
        new("PenNib", "Uncommon"), new("Pendulum", "Common"), new("Permafrost", "Uncommon"),
        new("PetrifiedToad", "Uncommon"), new("Planisphere", "Uncommon"), new("Pocketwatch", "Rare"),
        new("PotionBelt", "Common"), new("PrayerWheel", "Rare"), new("PunchDagger", "Shop"),
        new("RainbowRing", "Rare"), new("RazorTooth", "Rare"), new("RedMask", "Common"),
        new("RegalPillow", "Common"), new("ReptileTrinket", "Uncommon"), new("RingingTriangle", "Shop"),
        new("RippleBasin", "Uncommon"), new("RoyalStamp", "Shop"), new("ScreamingFlagon", "Shop"),
        new("Shovel", "Rare"), new("Shuriken", "Rare"), new("SlingOfCourage", "Shop"),
        new("SparklingRouge", "Uncommon"), new("StoneCalendar", "Rare"), new("StoneCracker", "Uncommon"),
        new("Strawberry", "Common"), new("StrikeDummy", "Common"), new("SturdyClamp", "Rare"),
        new("TheAbacus", "Shop"), new("TheCourier", "Rare"), new("TinyMailbox", "Uncommon"),
        new("Toolbox", "Shop"), new("ToxicEgg", "Rare"), new("TungstenRod", "Rare"),
        new("TuningFork", "Uncommon"), new("UnceasingTop", "Rare"), new("UnsettlingLamp", "Rare"),
        new("Vajra", "Common"), new("Vambrace", "Uncommon"), new("VenerableTeaSet", "Common"),
        new("VeryHotCocoa", "Ancient"), new("VexingPuzzlebox", "Rare"), new("WarPaint", "Common"),
        new("Whetstone", "Common"), new("WhiteBeastStatue", "Rare"), new("WhiteStar", "Rare"),
        new("WingCharm", "Shop"),
    ];

    public static IReadOnlyList<SharedRelicPoolEntry> ForTreasure { get; } =
        Entries.Where(entry => entry.Rarity is "Common" or "Uncommon" or "Rare").ToArray();
}
