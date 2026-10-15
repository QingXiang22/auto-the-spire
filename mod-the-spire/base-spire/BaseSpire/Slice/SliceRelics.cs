namespace BaseSpire.Slice;

/// <summary>遗物效果的一个**声明**（触发点 + 种类 + 数值）。</summary>
public sealed record SliceRelicEffect(string Trigger, string Kind, int Amount);

/// <summary>
/// 一个遗物声明：内容 id、名字、稀有度与它声明的效果。
///
/// 为什么是数据而不是每件遗物一个 C# 效果类：遗物效果是**内容**，运行时按声明的触发点/种类统一展开
/// （与能力、敌人行动同一套做法）。加一件遗物＝加一条声明 + 需要时补一个通用效果种类，
/// 而不是再写一个 `XxxRelic : RelicModel`。
/// </summary>
public sealed record SliceRelic(
    string ContentId,
    string Name,
    string Rarity,
    IReadOnlyList<SliceRelicEffect> Effects,
    string SourcePath,
    int SourceLine);

/// <summary>运行时实现的遗物触发点（未实现的触发点在构建期显式失败，不静默丢弃）。</summary>
public static class RelicTriggers
{
    /// <summary>战斗胜利之后（来源 `RelicModel.AfterCombatVictory`）。</summary>
    public const string AfterCombatVictory = "after-combat-victory";

    public const string AfterObtained = "after-obtained";
    public static readonly IReadOnlyList<string> All = [AfterCombatVictory, AfterObtained];
}

/// <summary>运行时实现的遗物效果种类。</summary>
public static class RelicEffectKinds
{
    /// <summary>回血；**被最大生命截断**（来源 `CreatureCmd.Heal:698` + `HealInternal:477-486`）。</summary>
    public const string Heal = "heal";

    public const string GainMaxHp = "gain-max-hp";
    public const string GainGold = "gain-gold";
    public static readonly IReadOnlyList<string> All = [Heal, GainMaxHp, GainGold];
}

/// <summary>
/// 已实现遗物效果的正式声明表；候选池身份单独维护。
///
/// 包括 Ironclad 起始遗物和可复用的获得效果。完整池由 SharedRelicPoolManifest 维护。
/// 表里每一条都带来源定位；`BaseSpireCharacters` 的 `Relics` 只能引用这里登记过的 id。
/// </summary>
public static class SpireRelics
{
    public const string BurningBloodId = "base-spire:burning-blood";

    public static IReadOnlyList<SliceRelic> All { get; } =
    [
        // `BurningBlood.cs:14` `CanonicalVars => [new HealVar(6m)]`；
        // `:16-22` `AfterCombatVictory`：拥有者**没死**才回血，数值取 HealVar 的基础值。
        // `CreatureCmd.Heal:698` 把回血量截断到 `MaxHp - CurrentHp`。
        new(BurningBloodId, "BurningBlood", "starter",
            [new SliceRelicEffect(RelicTriggers.AfterCombatVictory, RelicEffectKinds.Heal, 6)],
            "sts2/src/Core/Models/Relics/BurningBlood.cs", 14),
        new("base-spire:pear", "Pear", "Uncommon",
            [new SliceRelicEffect(RelicTriggers.AfterObtained, RelicEffectKinds.GainMaxHp, 10)],
            "sts2/src/Core/Models/Relics/Pear.cs", 15),
        new("base-spire:mango", "Mango", "Rare",
            [new SliceRelicEffect(RelicTriggers.AfterObtained, RelicEffectKinds.GainMaxHp, 14)],
            "sts2/src/Core/Models/Relics/Mango.cs", 15),
        new("base-spire:old-coin", "OldCoin", "Rare",
            [new SliceRelicEffect(RelicTriggers.AfterObtained, RelicEffectKinds.GainGold, 300)],
            "sts2/src/Core/Models/Relics/OldCoin.cs", 17),
    ];

    public static SliceRelic ById(string contentId) =>
        All.FirstOrDefault(relic => string.Equals(relic.ContentId, contentId, StringComparison.Ordinal))
        ?? throw new ArgumentException(
            $"遗物 '{contentId}' 没有正式声明（已登记：{string.Join(", ", All.Select(relic => relic.ContentId))}）",
            nameof(contentId));

    /// <summary>构建期校验一条声明的触发点/种类都在实现范围内；返回具体原因（空串＝可以实现）。</summary>
    public static string UnsupportedReason(SliceRelic relic)
    {
        foreach (var effect in relic.Effects)
        {
            if (!RelicTriggers.All.Contains(effect.Trigger, StringComparer.Ordinal))
            {
                return $"{relic.Name} 的触发点 '{effect.Trigger}' 未实现"
                    + $"（已实现：{string.Join(" / ", RelicTriggers.All)}）";
            }

            if (!RelicEffectKinds.All.Contains(effect.Kind, StringComparer.Ordinal))
            {
                return $"{relic.Name} 的效果 '{effect.Kind}' 未实现"
                    + $"（已实现：{string.Join(" / ", RelicEffectKinds.All)}）";
            }
        }

        return string.Empty;
    }
}
