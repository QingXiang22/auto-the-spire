using System.Text.Json;
using System.Text.Json.Serialization;
using BaseSpire.SinglePlayer;

namespace BaseSpire.Slice;

/// <summary>遗物效果的一个**声明**（触发点 + 种类 + 数值）。</summary>
public sealed record SliceRelicEffect(string Trigger, string Kind, int Amount)
{
    public string PowerId { get; init; } = "";
    public string Target { get; init; } = "owner";
    public string RoomType { get; init; } = "";
    public int OnTurn { get; init; }
    public int MinimumTurn { get; init; }
    public int MaximumTurn { get; init; }
    public int CounterThreshold { get; init; }
    public string ReplaceWith { get; init; } = "";
    public int CounterModulo { get; init; }
    public int CounterEquals { get; init; } = -1;
    public int HpAtMostPercent { get; init; }
    public string MapPointType { get; init; } = "";
    public string AmountSource { get; init; } = "constant";
    public int Divisor { get; init; } = 1;
    public string OrbKind { get; init; } = "";
    /// <summary>牌型过滤（空＝任意牌）；当前只用于 <see cref="RelicTriggers.AfterCardPlayed"/>。</summary>
    public string CardKind { get; init; } = "";
    /// <summary>手牌数至少（-1＝不作为条件；来源 `PileType.Hand … Count != 0`）。</summary>
    public int HandAtLeast { get; init; } = -1;
    /// <summary>手牌数至多（-1＝不作为条件；来源 `…IsEmpty`）。</summary>
    public int HandAtMost { get; init; } = -1;
    /// <summary>当前格挡至少（-1＝不作为条件；来源 `creature.Block >= n`）。</summary>
    public int BlockAtLeast { get; init; } = -1;
    /// <summary>当前格挡至多（-1＝不作为条件；来源 `creature.Block == 0` 等）。</summary>
    public int BlockAtMost { get; init; } = -1;
    /// <summary>伤害来源牌必须带该标签（空＝不按标签过滤；来源 `cardSource.Tags.Contains(CardTag.X)`）。</summary>
    public string CardTag { get; init; } = "";
    /// <summary>伤害来源牌必须是升级版本（来源 `cardSource.IsUpgraded`）。</summary>
    public bool CardUpgradedOnly { get; init; }
    /// <summary>伤害来源牌必须带附魔（来源 `cardSource.Enchantment != null`）。</summary>
    public bool CardEnchantedOnly { get; init; }
}

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
    int SourceLine)
{
    public bool ShowCounter { get; init; }
    public bool IsStackable { get; init; }
    public int? MerchantCost { get; init; }
    public string NoGameplayEffectReason { get; init; } = "";
}

/// <summary>运行时实现的遗物触发点（未实现的触发点在构建期显式失败，不静默丢弃）。</summary>
public static class RelicTriggers
{
    /// <summary>弃牌堆重洗完成，抽牌或自动打出下一张牌之前（源 Hook.AfterShuffle）。</summary>
    public const string AfterShuffle = "after-shuffle";
    /// <summary>战斗胜利之后（来源 `RelicModel.AfterCombatVictory`）。</summary>
    public const string AfterCombatVictory = "after-combat-victory";

    public const string AfterObtained = "after-obtained";
    /// <summary>一张牌打出之后（来源 `RelicModel.AfterCardPlayed`）。</summary>
    public const string AfterCardPlayed = "after-card-played";

    /// <summary>一张牌被消耗之后（来源 `RelicModel.AfterCardExhausted`）。</summary>
    public const string AfterCardExhausted = "after-card-exhausted";

    /// <summary>玩家真正吃到伤害之后（`result.UnblockedDamage > 0`，来源 `RelicModel.AfterDamageReceived`）。</summary>
    public const string AfterDamageReceived = "after-damage-received";

    /// <summary>一张牌被弃掉之后（来源 `RelicModel.AfterCardDiscarded`）。</summary>
    public const string AfterCardDiscarded = "after-card-discarded";

    /// <summary>一个敌人死亡之后（来源 `RelicModel.AfterDeath`）。</summary>
    public const string AfterMonsterDied = "after-monster-died";
    public const string AfterCombatEnd = "after-combat-end";
    public const string AfterCombatRoomEntered = "after-combat-room-entered";
    public const string BeforeCombatStart = "before-combat-start";
    public const string AfterSideTurnStart = "after-side-turn-start";
    public const string AfterPlayerTurnStartLate = "after-player-turn-start-late";
    public const string BeforeSideTurnStart = "before-side-turn-start";
    public const string AfterBlockCleared = "after-block-cleared";
    public const string BeforeHandDraw = "before-hand-draw";
    public const string AfterPlayerTurnStart = "after-player-turn-start";
    public const string AfterEnergyReset = "after-energy-reset";
    public const string Modifier = "modifier";
    public const string BeforeSideTurnEnd = "before-side-turn-end";
    public const string AfterSideTurnEnd = "after-side-turn-end";
    public const string AfterCombatVictoryEarly = "after-combat-victory-early";
    public const string AfterRoomEntered = "after-room-entered";
    public static readonly IReadOnlyList<string> All = [AfterCombatVictory, AfterObtained, AfterCardPlayed,
        AfterCardExhausted, AfterDamageReceived, AfterCardDiscarded, AfterMonsterDied, AfterCombatEnd,
        AfterCombatRoomEntered, BeforeCombatStart, AfterSideTurnStart, AfterPlayerTurnStartLate,
        BeforeSideTurnStart, AfterBlockCleared, BeforeHandDraw, AfterPlayerTurnStart, AfterEnergyReset, Modifier,
        BeforeSideTurnEnd, AfterSideTurnEnd, AfterCombatVictoryEarly, AfterRoomEntered, AfterShuffle];
}

/// <summary>运行时实现的遗物效果种类。</summary>
public static class RelicEffectKinds
{
    /// <summary>回血；**被最大生命截断**（来源 `CreatureCmd.Heal:698` + `HealInternal:477-486`）。</summary>
    public const string Heal = "heal";

    public const string GainMaxHp = "gain-max-hp";
    public const string GainGold = "gain-gold";
    public const string HealToFull = "heal-to-full";
    public const string HealMaxHpPercent = "heal-max-hp-percent";
    public const string GainPotionSlots = "gain-potion-slots";
    public const string CountAndReplace = "count-and-replace";
    public const string ApplyPower = "apply-power";
    public const string GainBlock = "gain-block";
    public const string GainEnergy = "gain-energy";
    public const string LoseEnergy = "lose-energy";
    public const string ModifyHandDraw = "modify-hand-draw";
    public const string ModifyMaxEnergy = "modify-max-energy";
    public const string RetainEnergy = "retain-energy";
    public const string RetainHand = "retain-hand";
    public const string PreventPotionProcurement = "prevent-potion-procurement";
    public const string RetainedBlockLimit = "retained-block-limit";
    public const string AddCounter = "add-counter";
    public const string GainStars = "gain-stars";
    public const string SetCounter = "set-counter";
    /// <summary>对全部存活敌人造成**无加成**伤害（源 `ValueProp.Unpowered`）。</summary>
    public const string DamageAllEnemies = "damage-all-enemies";

    /// <summary>抽 N 张牌（复用共享抽牌过程，含重洗、抽牌阻止与 AfterDraw）。</summary>
    public const string DrawCards = "draw-cards";
    /// <summary>按伤害来源牌加算伤害（源 `ModifyDamageAdditive`）。</summary>
    public const string ModifyDamageAdditive = "modify-damage-additive";
    public const string ModifyRestHeal = "modify-rest-heal";
    public const string ChannelOrb = "channel-orb";
    public const string AddOrbSlots = "add-orb-slots";
    public const string ModifyLightningOrbValue = "modify-lightning-orb-value";
    public static readonly IReadOnlyList<string> All = [Heal, GainMaxHp, GainGold, HealToFull, HealMaxHpPercent,
        GainPotionSlots, CountAndReplace, ApplyPower, GainBlock, GainEnergy, LoseEnergy, ModifyHandDraw, ModifyMaxEnergy,
        RetainEnergy, RetainHand, PreventPotionProcurement, RetainedBlockLimit, AddCounter, GainStars, SetCounter, ModifyRestHeal,
        ChannelOrb, AddOrbSlots, ModifyLightningOrbValue, DamageAllEnemies, DrawCards, ModifyDamageAdditive];
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

    public const string Resource = "BaseSpire.ContentSource.relics.json";
    public static IReadOnlyList<SliceRelic> All { get; } = Load();

    private static IReadOnlyList<SliceRelic> Load()
    {
        var rows = JsonSerializer.Deserialize<SliceRelic[]>(EncounterContent.ReadText(Resource),
            new JsonSerializerOptions { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
                RespectRequiredConstructorParameters = true })
            ?? throw new InvalidOperationException("Missing formal relic definitions");
        if (rows.Length == 0 || rows.Select(r => r.ContentId).Distinct(StringComparer.Ordinal).Count() != rows.Length
            || rows.Select(r => r.Name).Distinct(StringComparer.Ordinal).Count() != rows.Length)
            throw new InvalidOperationException("Empty or duplicated formal relic definitions");
        foreach (var row in rows)
        {
            if (row.ContentId != "base-spire:" + SpireCardSource.ToId(row.Name)
                || string.IsNullOrEmpty(row.SourcePath) || row.SourceLine < 1)
                throw new InvalidOperationException($"Invalid relic identity/source: {row.Name}");
            var reason = UnsupportedReason(row);
            if (reason.Length > 0) throw new InvalidOperationException(reason);
        }
        return Array.AsReadOnly(rows);
    }

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

            var supported = effect.Trigger switch
            {
                RelicTriggers.AfterShuffle => effect.Kind is RelicEffectKinds.GainBlock,
                RelicTriggers.AfterObtained => effect.Kind is RelicEffectKinds.GainMaxHp or RelicEffectKinds.GainGold
                    or RelicEffectKinds.Heal or RelicEffectKinds.HealToFull or RelicEffectKinds.HealMaxHpPercent or RelicEffectKinds.GainPotionSlots,
                RelicTriggers.AfterCombatEnd => effect.Kind is RelicEffectKinds.GainMaxHp or RelicEffectKinds.Heal,
                RelicTriggers.AfterCombatVictory or RelicTriggers.AfterCombatVictoryEarly => effect.Kind is RelicEffectKinds.Heal or RelicEffectKinds.CountAndReplace,
                RelicTriggers.AfterCardPlayed => effect.Kind is RelicEffectKinds.GainEnergy or RelicEffectKinds.ApplyPower
                    or RelicEffectKinds.GainBlock or RelicEffectKinds.AddCounter or RelicEffectKinds.SetCounter
                    or RelicEffectKinds.DamageAllEnemies,
                RelicTriggers.AfterCardExhausted => effect.Kind is RelicEffectKinds.DamageAllEnemies,
                RelicTriggers.AfterDamageReceived => effect.Kind is RelicEffectKinds.DrawCards or RelicEffectKinds.AddCounter
                    or RelicEffectKinds.SetCounter or RelicEffectKinds.ApplyPower,
                RelicTriggers.AfterCardDiscarded => effect.Kind is RelicEffectKinds.GainBlock,
                RelicTriggers.AfterMonsterDied => effect.Kind is RelicEffectKinds.GainEnergy or RelicEffectKinds.DrawCards,
                RelicTriggers.AfterPlayerTurnStart => effect.Kind is RelicEffectKinds.DamageAllEnemies or RelicEffectKinds.DrawCards
                    or RelicEffectKinds.AddCounter or RelicEffectKinds.SetCounter,
                RelicTriggers.BeforeSideTurnEnd => effect.Kind is RelicEffectKinds.DamageAllEnemies or RelicEffectKinds.GainBlock,
                RelicTriggers.AfterRoomEntered => effect.Kind is RelicEffectKinds.Heal or RelicEffectKinds.SetCounter,
                RelicTriggers.Modifier => effect.Kind is RelicEffectKinds.ModifyHandDraw or RelicEffectKinds.ModifyMaxEnergy
                    or RelicEffectKinds.RetainEnergy or RelicEffectKinds.RetainHand or RelicEffectKinds.PreventPotionProcurement or RelicEffectKinds.RetainedBlockLimit
                    or RelicEffectKinds.ModifyRestHeal or RelicEffectKinds.ModifyLightningOrbValue or RelicEffectKinds.ModifyDamageAdditive,
                _ => effect.Kind is RelicEffectKinds.ApplyPower or RelicEffectKinds.GainBlock or RelicEffectKinds.GainEnergy or RelicEffectKinds.Heal or RelicEffectKinds.LoseEnergy
                    or RelicEffectKinds.AddCounter or RelicEffectKinds.GainStars or RelicEffectKinds.SetCounter or RelicEffectKinds.ChannelOrb or RelicEffectKinds.AddOrbSlots,
            };
            if (!supported) return $"{relic.Name} 的触发点／效果组合 '{effect.Trigger}/{effect.Kind}' 未实现";
            if (effect.Amount < 0 && effect.Kind != RelicEffectKinds.ModifyHandDraw || effect.OnTurn < 0 || effect.CounterThreshold < 0
                || effect.MinimumTurn < 0 || effect.MaximumTurn < 0 || effect.MaximumTurn > 0 && effect.MinimumTurn > effect.MaximumTurn
                || effect.CounterModulo < 0 || effect.CounterEquals < -1 || effect.HpAtMostPercent is < 0 or > 100
                || effect.Target is not ("owner" or "enemies") || effect.RoomType is not ("" or "Elite" or "Boss" or "Monster" or "RestSite" or "Shop" or "Event" or "Treasure")
                || effect.MapPointType is not ("" or "Unknown") || effect.Divisor < 1 || effect.AmountSource is not ("constant" or "permanent-deck" or "hand")
                || effect.CardKind is not ("" or "Attack" or "Skill" or "Power"))
                return $"{relic.Name} 的效果参数不合法";
            if (effect.HandAtLeast < -1 || effect.HandAtMost < -1
                || effect.HandAtLeast >= 0 && effect.HandAtMost >= 0 && effect.HandAtLeast > effect.HandAtMost)
                return $"{relic.Name} 的手牌数量条件不合法";
            if (effect.BlockAtLeast < -1 || effect.BlockAtMost < -1
                || effect.BlockAtLeast >= 0 && effect.BlockAtMost >= 0 && effect.BlockAtLeast > effect.BlockAtMost)
                return $"{relic.Name} 的格挡数量条件不合法";
            if (effect.CardKind.Length > 0 && effect.Trigger != RelicTriggers.AfterCardPlayed)
                return $"{relic.Name} 的触发点 '{effect.Trigger}' 不支持牌型过滤";
            if (effect.Kind == RelicEffectKinds.ChannelOrb && effect.OrbKind is not ("Lightning" or "Frost" or "Dark" or "Plasma" or "Glass")
                || effect.Kind != RelicEffectKinds.ChannelOrb && effect.OrbKind.Length > 0)
                return $"{relic.Name} 的充能球参数不合法";
            if (effect.Kind == RelicEffectKinds.ApplyPower && string.IsNullOrEmpty(effect.PowerId)
                || effect.Kind == RelicEffectKinds.CountAndReplace && (effect.CounterThreshold == 0 || string.IsNullOrEmpty(effect.ReplaceWith)))
                return $"{relic.Name} 的效果缺少能力或替换目标";
        }
        if (relic.Effects.Count == 0 && string.IsNullOrWhiteSpace(relic.NoGameplayEffectReason))
            return $"{relic.Name} 没有效果或明确的源无玩法效果依据";

        return string.Empty;
    }
}
