namespace CoreTheSpire;

/// <summary>The current cost and result of a hand card, derived without mutating gameplay state.</summary>
public sealed record CardPlayPreview(string InstanceId, int EnergyToSpend, int XEnergy, CardPile? ResultPile);

internal static class CombatCardRules
{
    public static CardPlayPreview Preview(GameState state, CardInstance instance)
    {
        var card = state.Definition.Cards[instance.CardId];
        var energy = card.HasEnergyXCost ? state.Player.Energy : card.ResolveCost(instance);
        if (!card.HasEnergyXCost)
            foreach (var modifier in instance.CostModifiers) energy = modifier.Apply(energy);
        CardPile? result = card.Type == CardType.Power ? null
            : (card.Keywords & CardKeywords.Exhaust) != 0 ? CardPile.Exhaust : CardPile.Discard;
        foreach (var power in state.Player.Creature.Powers)
        {
            if (power.Value <= 0 || state.Definition.Powers[power.Key].CardRule is not { } rule
                || rule.Type != card.Type) continue;
            if (!card.HasEnergyXCost && rule.Cost is { } cost) energy = cost;
            if (rule.ResultPile is { } pile) result = pile;
        }
        return new CardPlayPreview(instance.InstanceId, Math.Max(0, energy), card.HasEnergyXCost ? energy : 0, result);
    }

    public static void BeforePlay(GameSession session, CardInstance instance)
    {
        var state = session.State;
        var owner = state.Player.Creature;
        var type = state.Definition.Cards[instance.CardId].Type;
        foreach (var power in owner.Powers.ToArray())
            if (power.Value > 0 && state.Definition.Powers[power.Key].CardRule is { ConsumeOnPlay: true } rule
                && rule.Type == type)
            {
                var delta = state.DecrementPower(session, owner, power.Key);
                if (delta != 0) state.Emit(session, "power", owner.Id, owner.Id, delta);
            }
    }

    public static (int Count, IReadOnlyList<ContentId> ConsumedPowers) ResolvePlayCount(GameState state, CardInstance instance)
    {
        var owner = state.Player.Creature;
        var type = state.Definition.Cards[instance.CardId].Type;
        var count = 1;
        var consumed = new List<ContentId>();
        foreach (var power in owner.Powers.ToArray())
            if (power.Value > 0 && state.Definition.Powers[power.Key].CardRule is { ExtraPlays: > 0 } rule
                && rule.Type == type)
            {
                count = checked(count + rule.ExtraPlays);
                if (rule.ConsumeOnPlayCount) consumed.Add(power.Key);
            }
        return (count, consumed);
    }
}
