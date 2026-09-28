namespace CoreTheSpire;

internal enum CardRarityOddsType { RegularEncounter, EliteEncounter, BossEncounter }

/// <summary>Per-run rarity offset used by player card rewards, sharing the player's rewards stream.</summary>
internal sealed class CardRarityOddsState
{
    private float _offset = -0.05f;

    public CardRarity Roll(CardRarityOddsType type, SeededRandom random,
        RunRoomType roomType, IReadOnlyList<IRunHook> hooks, RunObservation observation)
    {
        var rareBase = type switch
        {
            CardRarityOddsType.RegularEncounter => 0.0149f,
            CardRarityOddsType.EliteEncounter => 0.05f,
            CardRarityOddsType.BossEncounter => 1f,
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
        var uncommonBase = type switch
        {
            CardRarityOddsType.RegularEncounter => 0.37f,
            CardRarityOddsType.EliteEncounter => 0.4f,
            CardRarityOddsType.BossEncounter => 0f,
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
        var odds = new CardRewardOdds(Math.Max(0f,
                rareBase + (type == CardRarityOddsType.BossEncounter ? 0f : _offset)),
            uncommonBase);
        foreach (var hook in hooks)
        {
            odds = hook.ModifyCardRewardOdds(observation, roomType, odds);
            if (!float.IsFinite(odds.RareChance) || !float.IsFinite(odds.UncommonChance)
                || odds.RareChance < 0f || odds.UncommonChance < 0f
                || odds.RareChance + odds.UncommonChance > 1f)
                throw new InvalidOperationException($"Run hook '{hook.Id}' returned invalid card reward odds.");
        }

        var roll = (float)random.NextDouble();
        CardRarity rarity;
        if (roll < odds.RareChance) rarity = CardRarity.Rare;
        else if (roll < odds.RareChance + odds.UncommonChance) rarity = CardRarity.Uncommon;
        else rarity = CardRarity.Common;

        _offset = rarity == CardRarity.Rare ? -0.05f : Math.Min(_offset + 0.005f, 0.4f);
        return rarity;
    }
}

internal static class CardRewardGenerator
{
    private const int OptionCount = 3;

    public static IReadOnlyList<CardInstance> Generate(RunState state,
        IReadOnlyDictionary<ContentId, CardDefinition> cards,
        IReadOnlyDictionary<ContentId, CardPoolDefinition> cardPools,
        IReadOnlyList<IRunHook> hooks, RunObservation observation)
    {
        var pool = state.Character.CardPools.SelectMany(id => cardPools[id].Cards)
            .Distinct().Select(id => cards[id]).ToArray();
        var random = state.RewardRandom;
        var blacklist = new HashSet<ContentId>();
        var options = new List<CardInstance>(OptionCount);
        var oddsType = state.CurrentRoom switch
        {
            RunRoomType.Monster => CardRarityOddsType.RegularEncounter,
            RunRoomType.Elite => CardRarityOddsType.EliteEncounter,
            RunRoomType.Boss => CardRarityOddsType.BossEncounter,
            _ => throw new InvalidOperationException("Card rewards require a combat room.")
        };
        var roomType = state.CurrentRoom!.Value;

        for (var i = 0; i < OptionCount; i++)
        {
            var available = pool.Where(card => !blacklist.Contains(card.Id)).ToArray();
            var allowedRarities = available.Select(card => card.Rarity).ToHashSet();
            var rolledRarity = state.CardRarityOdds.Roll(oddsType, random,
                roomType, hooks, observation);
            var selectedRarity = NextAllowedRarity(rolledRarity, allowedRarities);
            var rarityCards = available.Where(card => card.Rarity == selectedRarity).ToArray();
            if (rarityCards.Length == 0)
                throw new InvalidOperationException($"The card pool for '{state.Character.Id}' cannot produce a reward option.");

            var selected = rarityCards[random.NextInt(rarityCards.Length)];
            blacklist.Add(selected.Id);
            var upgradeRoll = (decimal)(float)random.NextDouble();
            var upgradeLevel = 0;
            if (selected.HasUpgrade)
            {
                var upgradeOdds = selected.Rarity == CardRarity.Rare ? 0m : state.ActIndex * 0.125m;
                if (upgradeRoll <= upgradeOdds) upgradeLevel = 1;
            }

            options.Add(new CardInstance($"run-reward-card-{state.NextRewardCardInstanceId++:D4}",
                selected.Id, upgradeLevel));
        }

        return Array.AsReadOnly(options.ToArray());
    }

    private static CardRarity NextAllowedRarity(CardRarity rarity, IReadOnlySet<CardRarity> allowed)
    {
        var first = rarity;
        while (!allowed.Contains(rarity))
        {
            rarity = rarity switch
            {
                CardRarity.Common => CardRarity.Uncommon,
                CardRarity.Uncommon => CardRarity.Rare,
                CardRarity.Rare => CardRarity.Common,
                _ => CardRarity.None
            };
            if (rarity == CardRarity.None || rarity == first)
                throw new InvalidOperationException("The card pool has no common, uncommon, or rare reward cards.");
        }
        return rarity;
    }
}
