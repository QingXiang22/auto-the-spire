namespace BaseSpire.Slice;

/// <summary>
/// ENV-T09.2：无色卡的**源顺序清单 + 源稀有度**（`sts2/src/Core/Models/CardPools/ColorlessCardPool.cs` 的
/// `GenerateAllCards()` 逐项顺序；稀有度取自各卡源构造 `base(cost, CardType, CardRarity, TargetType)`，
/// 例如 `Cards/Alchemize.cs:17` = `CardRarity.Rare`）。
///
/// 为什么商店需要这张表（而奖励路径不需要）：奖励池是 `IroncladCardPool` 的颜色池，无色卡不在其中。
/// 源商店的两格无色商品走 `CardFactory.CreateForMerchant(player, ModelDb.CardPool&lt;ColorlessCardPool&gt;(), rarity)`
/// （`CardFactory.cs:69-79`）：**先按固定稀有度过滤源清单**（`options.Where(c =&gt; c.Rarity == rarity)`），
/// 再对过滤后的 `items` 执行一次 `Shops.NextItem`。因此：
/// <list type="bullet">
/// <item>抽取上界是**该稀有度在源清单里的成员数**，不是整条 64 项；顺序就是本表里该稀有度的出现顺序；</item>
/// <item>抽到源清单里**尚未进内容**的成员时，它不是"另一张卡"：商品条目照源保留该成员的身份
///       （`ShopOffer.SourceName` = 源 kebab 名）并标记不可买、写明"尚无内容定义"，
///       不把它替换成内容池里的下一位、也不从清单里过滤掉；</item>
/// <item>过滤只按**源稀有度**，不按"内容里有没有 DSL/效果"过滤（内容支持度不是抽取条件）。</item>
/// </list>
///
/// 内容侧 `colorless-cards.spire` 里 `pools = { ColorlessCardPool }` 的声明与本表逐项对账
/// （声明项不在本表 = 具名失败；本表项没有内容 = 该成员不可买，见 `ShopRoomWiring`）。
/// </summary>
public static class ColorlessShopPool
{
    /// <summary>一个源成员的只读事实：`Name` 是 CamelCase 源名，`SourceId` 是 kebab 名，`Rarity` 是源稀有度。</summary>
    public readonly record struct Entry(string Name, string SourceId, string Rarity);

    /// <summary>`ColorlessCardPool.GenerateAllCards()` 的 64 项（源顺序、含未解锁 epoch 的成员：
    /// 工厂策略是全解锁，`FilterThroughEpochs` 不删任何一项）。</summary>
    public static readonly IReadOnlyList<Entry> Entries =
    [
        new("Alchemize", "alchemize", "rare"),
        new("Anointed", "anointed", "rare"),
        new("Automation", "automation", "uncommon"),
        new("BeaconOfHope", "beacon-of-hope", "rare"),
        new("BeatDown", "beat-down", "rare"),
        new("BelieveInYou", "believe-in-you", "uncommon"),
        new("Bolas", "bolas", "rare"),
        new("Calamity", "calamity", "rare"),
        new("Catastrophe", "catastrophe", "uncommon"),
        new("Coordinate", "coordinate", "uncommon"),
        new("DarkShackles", "dark-shackles", "uncommon"),
        new("Discovery", "discovery", "uncommon"),
        new("DramaticEntrance", "dramatic-entrance", "uncommon"),
        new("Entropy", "entropy", "rare"),
        new("Equilibrium", "equilibrium", "uncommon"),
        new("EternalArmor", "eternal-armor", "rare"),
        new("Fasten", "fasten", "uncommon"),
        new("Finesse", "finesse", "uncommon"),
        new("Fisticuffs", "fisticuffs", "uncommon"),
        new("FlashOfSteel", "flash-of-steel", "uncommon"),
        new("GangUp", "gang-up", "uncommon"),
        new("GoldAxe", "gold-axe", "rare"),
        new("HandOfGreed", "hand-of-greed", "rare"),
        new("HiddenGem", "hidden-gem", "rare"),
        new("HuddleUp", "huddle-up", "uncommon"),
        new("Impatience", "impatience", "uncommon"),
        new("Intercept", "intercept", "uncommon"),
        new("JackOfAllTrades", "jack-of-all-trades", "uncommon"),
        new("Jackpot", "jackpot", "rare"),
        new("Knockdown", "knockdown", "rare"),
        new("Lift", "lift", "uncommon"),
        new("MasterOfStrategy", "master-of-strategy", "rare"),
        new("Mayhem", "mayhem", "rare"),
        new("Mimic", "mimic", "rare"),
        new("MindBlast", "mind-blast", "uncommon"),
        new("Nostalgia", "nostalgia", "rare"),
        new("Omnislice", "omnislice", "uncommon"),
        new("Panache", "panache", "uncommon"),
        new("PanicButton", "panic-button", "uncommon"),
        new("PrepTime", "prep-time", "uncommon"),
        new("Production", "production", "uncommon"),
        new("Prolong", "prolong", "uncommon"),
        new("Prowess", "prowess", "uncommon"),
        new("Purity", "purity", "uncommon"),
        new("Rally", "rally", "rare"),
        new("Rend", "rend", "rare"),
        new("Restlessness", "restlessness", "uncommon"),
        new("RollingBoulder", "rolling-boulder", "rare"),
        new("Salvo", "salvo", "rare"),
        new("Scrawl", "scrawl", "rare"),
        new("SecretTechnique", "secret-technique", "rare"),
        new("SecretWeapon", "secret-weapon", "rare"),
        new("SeekerStrike", "seeker-strike", "uncommon"),
        new("Shockwave", "shockwave", "uncommon"),
        new("Splash", "splash", "uncommon"),
        new("Stratagem", "stratagem", "uncommon"),
        new("TagTeam", "tag-team", "uncommon"),
        new("TheBomb", "the-bomb", "uncommon"),
        new("TheGambit", "the-gambit", "rare"),
        new("ThinkingAhead", "thinking-ahead", "uncommon"),
        new("ThrummingHatchet", "thrumming-hatchet", "uncommon"),
        new("UltimateDefend", "ultimate-defend", "uncommon"),
        new("UltimateStrike", "ultimate-strike", "uncommon"),
        new("Volley", "volley", "uncommon"),
    ];

    /// <summary>源清单里的成员数（64）。</summary>
    public static int Count => Entries.Count;

    /// <summary>该源稀有度在源清单里的成员下标（保序）——`Shops.NextItem` 的抽取上界就是它的长度。</summary>
    public static IReadOnlyList<int> IndicesAtRarity(string rarity) =>
        [.. Enumerable.Range(0, Entries.Count).Where(index => string.Equals(Entries[index].Rarity, rarity, StringComparison.Ordinal))];

    /// <summary>`base-spire:` 前缀之外的短 id（与 `SpireCardSource.ToId` 的 kebab 口径一致）。</summary>
    public static string ShortId(string contentId)
    {
        var separator = contentId.LastIndexOf(':');
        return separator < 0 ? contentId : contentId[(separator + 1)..];
    }
}
