// 本文件由 tools/rng-oracle/vanilla_rng_oracle.py 生成——不要手改。
// 重新生成：
//   python tools/rng-oracle/vanilla_rng_oracle.py --cs-layout mod-the-spire/base-spire/BaseSpire.Slice.Tests/VanillaSeedLayoutVectors.cs
//
// 只按只读原版源码的**独立重写**生成：
//   - sts2/src/Core/Runs/RunRngSet.cs:104-119
//   - sts2/src/Core/Random/PlayerRngSet.cs:33-47
//   - sts2/src/Core/Entities/Players/Player.cs:324-328
//   - sts2/src/Core/Random/Rng.cs:47-55
//   - sts2/src/Core/Helpers/StringHelper.cs:GetDeterministicHashCode/SnakeCase

namespace BaseSpire.Slice.Tests;

/// <summary>ENV-01 的 base 侧已知向量：字符串 seed → run/player/内容流种子，以及该布局下的真实读数。</summary>
internal static class VanillaSeedLayoutVectors
{
    internal sealed record LayoutVector(
        string StringSeed,
        uint RunSeed,
        uint PlayerSeed,
        (string Type, uint Seed)[] RunStreams,
        (string Type, uint Seed)[] PlayerStreams,
        uint[] ShuffleStreamFirst4,
        string[] ShuffleStreamDeck,
        int ShuffleStreamCounter);

    public static readonly LayoutVector[] Layout =
    [
        new("", 757602046U, 757602046U, [("CombatCardGeneration", 3763028402U), ("CombatCardSelection", 1129473910U), ("CombatEnergyCosts", 3536231404U), ("CombatOrbs", 2005551175U), ("CombatPotionGeneration", 1606589377U), ("CombatTargets", 2283533281U), ("MonsterAi", 2461504657U), ("Niche", 1255068767U), ("Shuffle", 3065882721U), ("TreasureRoomRelics", 3158405114U), ("UnknownMapPoint", 359370690U), ("UpFront", 3817822297U)], [("Rewards", 298554008U), ("Shops", 3177397465U), ("Transformations", 625656670U)], [26U, 47U, 55U, 97U], ["card-01", "card-04", "card-08", "card-05", "card-09", "card-03", "card-06", "card-00", "card-02", "card-07"], 13),
        new("1", 3452614541U, 3452614541U, [("CombatCardGeneration", 2163073601U), ("CombatCardSelection", 3824486405U), ("CombatEnergyCosts", 1936276603U), ("CombatOrbs", 405596374U), ("CombatPotionGeneration", 6634576U), ("CombatTargets", 683578480U), ("MonsterAi", 861549856U), ("Niche", 3950081262U), ("Shuffle", 1465927920U), ("TreasureRoomRelics", 1558450313U), ("UnknownMapPoint", 3054383185U), ("UpFront", 2217867496U)], [("Rewards", 2993566503U), ("Shops", 1577442664U), ("Transformations", 3320669165U)], [1U, 5U, 23U, 78U], ["card-07", "card-06", "card-03", "card-05", "card-00", "card-01", "card-02", "card-04", "card-09", "card-08"], 13),
        new("ABC", 2549457738U, 2549457738U, [("CombatCardGeneration", 1259916798U), ("CombatCardSelection", 2921329602U), ("CombatEnergyCosts", 1033119800U), ("CombatOrbs", 3797406867U), ("CombatPotionGeneration", 3398445069U), ("CombatTargets", 4075388973U), ("MonsterAi", 4253360349U), ("Niche", 3046924459U), ("Shuffle", 562771117U), ("TreasureRoomRelics", 655293510U), ("UnknownMapPoint", 2151226382U), ("UpFront", 1314710693U)], [("Rewards", 2090409700U), ("Shops", 674285861U), ("Transformations", 2417512362U)], [49U, 49U, 65U, 25U], ["card-00", "card-06", "card-07", "card-03", "card-09", "card-04", "card-08", "card-05", "card-01", "card-02"], 13),
        new("ENV-IRONCLAD-001", 968783790U, 968783790U, [("CombatCardGeneration", 3974210146U), ("CombatCardSelection", 1340655654U), ("CombatEnergyCosts", 3747413148U), ("CombatOrbs", 2216732919U), ("CombatPotionGeneration", 1817771121U), ("CombatTargets", 2494715025U), ("MonsterAi", 2672686401U), ("Niche", 1466250511U), ("Shuffle", 3277064465U), ("TreasureRoomRelics", 3369586858U), ("UnknownMapPoint", 570552434U), ("UpFront", 4029004041U)], [("Rewards", 509735752U), ("Shops", 3388579209U), ("Transformations", 836838414U)], [53U, 40U, 63U, 90U], ["card-02", "card-07", "card-05", "card-09", "card-00", "card-08", "card-03", "card-06", "card-04", "card-01"], 13),
        new("SEED-123", 678330858U, 678330858U, [("CombatCardGeneration", 3683757214U), ("CombatCardSelection", 1050202722U), ("CombatEnergyCosts", 3456960216U), ("CombatOrbs", 1926279987U), ("CombatPotionGeneration", 1527318189U), ("CombatTargets", 2204262093U), ("MonsterAi", 2382233469U), ("Niche", 1175797579U), ("Shuffle", 2986611533U), ("TreasureRoomRelics", 3079133926U), ("UnknownMapPoint", 280099502U), ("UpFront", 3738551109U)], [("Rewards", 219282820U), ("Shops", 3098126277U), ("Transformations", 546385482U)], [27U, 28U, 93U, 98U], ["card-07", "card-08", "card-09", "card-04", "card-00", "card-02", "card-01", "card-03", "card-05", "card-06"], 13),
        new("abc", 3974378154U, 3974378154U, [("CombatCardGeneration", 2684837214U), ("CombatCardSelection", 51282722U), ("CombatEnergyCosts", 2458040216U), ("CombatOrbs", 927359987U), ("CombatPotionGeneration", 528398189U), ("CombatTargets", 1205342093U), ("MonsterAi", 1383313469U), ("Niche", 176877579U), ("Shuffle", 1987691533U), ("TreasureRoomRelics", 2080213926U), ("UnknownMapPoint", 3576146798U), ("UpFront", 2739631109U)], [("Rewards", 3515330116U), ("Shops", 2099206277U), ("Transformations", 3842432778U)], [21U, 80U, 7U, 12U], ["card-09", "card-07", "card-01", "card-05", "card-04", "card-02", "card-03", "card-08", "card-06", "card-00"], 13),
        new("seed", 3305619111U, 3305619111U, [("CombatCardGeneration", 2016078171U), ("CombatCardSelection", 3677490975U), ("CombatEnergyCosts", 1789281173U), ("CombatOrbs", 258600944U), ("CombatPotionGeneration", 4154606442U), ("CombatTargets", 536583050U), ("MonsterAi", 714554426U), ("Niche", 3803085832U), ("Shuffle", 1318932490U), ("TreasureRoomRelics", 1411454883U), ("UnknownMapPoint", 2907387755U), ("UpFront", 2070872066U)], [("Rewards", 2846571073U), ("Shops", 1430447234U), ("Transformations", 3173673735U)], [23U, 24U, 64U, 46U], ["card-06", "card-03", "card-00", "card-02", "card-07", "card-09", "card-05", "card-08", "card-04", "card-01"], 13),
        new("斩杀尖塔2", 617808221U, 617808221U, [("CombatCardGeneration", 3623234577U), ("CombatCardSelection", 989680085U), ("CombatEnergyCosts", 3396437579U), ("CombatOrbs", 1865757350U), ("CombatPotionGeneration", 1466795552U), ("CombatTargets", 2143739456U), ("MonsterAi", 2321710832U), ("Niche", 1115274942U), ("Shuffle", 2926088896U), ("TreasureRoomRelics", 3018611289U), ("UnknownMapPoint", 219576865U), ("UpFront", 3678028472U)], [("Rewards", 158760183U), ("Shops", 3037603640U), ("Transformations", 485862845U)], [95U, 86U, 13U, 60U], ["card-00", "card-09", "card-03", "card-04", "card-07", "card-05", "card-01", "card-06", "card-02", "card-08"], 13),
    ];

    /// <summary>内容流种子（`Rng.cs:47-50`）：run seed + 槽位 + 内容 entry 的哈希。</summary>
    internal sealed record ContentVector(string Entry, int Slot, uint Seed);

    public static readonly ContentVector[] Content =
    [
        new("BASH", 0, 4160608670U),
        new("CALCIFIED_CULTIST", 0, 2073774551U),
        new("FEEL_NO_PAIN", 0, 2357203383U),
        new("STRIKE_IRONCLAD", 0, 2441293967U),
    ];

    /// <summary>SnakeCase（流名字用）定点样本：偏差出在命名层还是哈希层，靠它区分。</summary>
    public static readonly (string Input, string Snake)[] SnakeCases =
    [
        ("CombatCardGeneration", "combat_card_generation"),
        ("CombatCardSelection", "combat_card_selection"),
        ("CombatEnergyCosts", "combat_energy_costs"),
        ("CombatOrbs", "combat_orbs"),
        ("CombatPotionGeneration", "combat_potion_generation"),
        ("CombatTargets", "combat_targets"),
        ("MonsterAi", "monster_ai"),
        ("Niche", "niche"),
        ("Rewards", "rewards"),
        ("Shops", "shops"),
        ("Shuffle", "shuffle"),
        ("Transformations", "transformations"),
        ("TreasureRoomRelics", "treasure_room_relics"),
        ("UnknownMapPoint", "unknown_map_point"),
        ("UpFront", "up_front"),
    ];

    public const uint RunSeedOfEnvIronclad001 = 968783790U;
}
