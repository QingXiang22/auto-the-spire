namespace BaseSpire.Slice;

/// <summary>
/// ENV-T05：**冻结源**的单机 Ironclad 奖励牌池顺序（`sts2/src/Core/Models/CardPools/IroncladCardPool.cs`
/// 的 `GenerateAllCards()` 逐项顺序；CamelCase → kebab-case 与 `SpireCardSource.ToId` 同口径）。
///
/// 这张表是奖励候选的**顺序依据**：源 `CardFactory.CreateForReward` 在
/// `options2.Except(blacklist).Where(c =&gt; c.Rarity == selectedRarity)` 之后用 `rng.NextItem(items)`
/// 取的是这个列表里的**下标**——池顺序变了，同一个随机数取到的牌就变了。
/// 内容侧 `ironclad-cards.spire` 的声明顺序（94 项、含 status/curse）**不是**这个顺序，
/// 因此成员与顺序都以本表为准，并在 `RewardPoolTable` 里与内容逐项对账（缺项/多项都具名失败）。
///
/// 本文件由 `.audit/dsh-env-t05-rewards-resume-2026-10-09/emit-pool-order.py` 从源生成，不要手改。
/// </summary>
public static class IroncladRewardPool
{
    /// <summary>`GenerateAllCards()` 的 87 项（含源侧 multiplayerOnly 的 2 项）。</summary>
    public static readonly IReadOnlyList<string> SourceOrder =
    [
        "aggression",
        "anger",
        "armaments",
        "ashen-strike",
        "barricade",
        "bash",
        "battle-trance",
        "blood-wall",
        "bloodletting",
        "bludgeon",
        "body-slam",
        "brand",
        "break",
        "breakthrough",
        "bully",
        "burning-pact",
        "cascade",
        "cinder",
        "colossus",
        "conflagration",
        "corruption",
        "crimson-mantle",
        "cruelty",
        "dark-embrace",
        "defend-ironclad",
        "demon-form",
        "demonic-shield",
        "dismantle",
        "dominate",
        "drum-of-battle",
        "evil-eye",
        "expect-a-fight",
        "feed",
        "feel-no-pain",
        "fiend-fire",
        "fight-me",
        "flame-barrier",
        "forgotten-ritual",
        "havoc",
        "headbutt",
        "hellraiser",
        "hemokinesis",
        "howl-from-beyond",
        "impervious",
        "infernal-blade",
        "inferno",
        "inflame",
        "iron-wave",
        "juggernaut",
        "juggling",
        "mangle",
        "molten-fist",
        "not-yet",
        "offering",
        "one-two-punch",
        "pacts-end",
        "perfected-strike",
        "pillage",
        "pommel-strike",
        "primal-force",
        "pyre",
        "rage",
        "rampage",
        "rupture",
        "second-wind",
        "setup-strike",
        "shrug-it-off",
        "spite",
        "stampede",
        "stoke",
        "stomp",
        "stone-armor",
        "strike-ironclad",
        "sword-boomerang",
        "tank",
        "taunt",
        "tear-asunder",
        "thrash",
        "thunderclap",
        "tremble",
        "true-grit",
        "twin-strike",
        "unmovable",
        "unrelenting",
        "uppercut",
        "vicious",
        "whirlwind",
    ];

    /// <summary>
    /// 源侧 `multiplayerOnly`（单机池不含）的两项。内容侧同样标 `multiplayerOnly = true`：
    /// 这是「源清单 87 − 内容池 85」的**唯一**差额来源（对账在 `RewardPoolTable.Build`）。
    /// </summary>
    public static readonly IReadOnlyList<string> MultiplayerOnly =
    [
        "demonic-shield",
        "tank",
    ];
}
