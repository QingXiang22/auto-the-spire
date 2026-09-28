namespace CoreTheSpire;

public enum CardContext { Played, Selected }
public enum CardVariableOperation { Add, Set }
public sealed record CardVariableDefinition(ContentId Id, decimal InitialValue,
    decimal? UpgradedValue = null, bool PreserveOnDowngrade = false);
public readonly record struct CardVariableOffset(ContentId Variable, decimal Offset);

internal static class CombatCardVariables
{
    internal static CardInstance ResolveCard(GameState state, CardContext context, CardInstance? selected,
        CardInstance? playedOverride = null)
    {
        var instanceId = context switch
        {
            CardContext.Played => playedOverride?.InstanceId ?? state.CurrentCardPlay?.Card.InstanceId,
            CardContext.Selected => selected?.InstanceId,
            _ => throw new InvalidOperationException("Unsupported card context.")
        };
        if (instanceId is null) throw new InvalidOperationException($"Card context '{context}' is not available.");
        return state.FindCombatCard(instanceId)
            ?? throw new InvalidOperationException($"Card '{instanceId}' is no longer in combat.");
    }

    internal static decimal Read(GameState state, ContentId variable, CardContext context, CardInstance? selected,
        CardInstance? playedOverride = null)
    {
        var card = ResolveCard(state, context, selected, playedOverride);
        return state.Definition.Cards[card.CardId].ResolveDecimalVariable(card, variable);
    }

    internal static void Modify(GameSession session, CardInstance card, ContentId variable,
        CardVariableOperation operation, decimal amount)
    {
        var state = session.State;
        var definition = state.Definition.Cards[card.CardId];
        var previous = definition.ResolveDecimalVariable(card, variable);
        var next = operation == CardVariableOperation.Add ? previous + amount : amount;
        if (next == previous) return;
        var existingOffset = card.VariableOffsets.FirstOrDefault(value => value.Variable == variable).Offset;
        var newOffset = existingOffset + (next - previous);
        var offsets = card.VariableOffsets.Where(value => value.Variable != variable).ToList();
        if (newOffset != 0) offsets.Add(new(variable, newOffset));
        Replace(state, card, card.WithVariableOffsets(offsets));
        state.Emit(session, "card_variable_modified", variable.Value, card.InstanceId,
            decimal.ToInt32(decimal.Truncate(next - previous)), exactAmount: next - previous);
    }

    internal static void Downgrade(GameSession session, CardInstance card)
    {
        if (card.UpgradeLevel == 0) return;
        var definition = session.State.Definition.Cards[card.CardId];
        var offsets = card.VariableOffsets.Where(value =>
            definition.Variables.Single(variable => variable.Id == value.Variable).PreserveOnDowngrade).ToArray();
        Replace(session.State, card, card.WithUpgradeLevel(0).WithVariableOffsets(offsets));
        session.State.Emit(session, "card_downgraded", session.State.Player.Id, card.InstanceId, card.UpgradeLevel);
    }

    private static void Replace(GameState state, CardInstance current, CardInstance replacement)
    {
        foreach (var pile in GameState.CombatPiles)
        {
            var cards = state.GetPile(pile);
            var index = cards.FindIndex(card => card.InstanceId == current.InstanceId);
            if (index < 0) continue;
            cards[index] = replacement;
            return;
        }
        throw new InvalidOperationException($"Card '{current.InstanceId}' is no longer in combat.");
    }
}
