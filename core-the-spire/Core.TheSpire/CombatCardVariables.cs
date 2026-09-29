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
        state.ReplaceCombatCard(card, card.WithVariableOffsets(offsets));
        state.Emit(session, "card_variable_modified", variable.Value, card.InstanceId,
            decimal.ToInt32(decimal.Truncate(next - previous)), exactAmount: next - previous);
    }

    internal static void Downgrade(GameSession session, CardInstance card, ContentId? temporarySource = null)
    {
        if (card.UpgradeLevel == 0) return;
        if (temporarySource is { } source
            && card.TemporaryUpgradeChanges.Any(change => change.SourcePower == source)) return;
        var definition = session.State.Definition.Cards[card.CardId];
        var offsets = card.VariableOffsets.Where(value =>
            definition.Variables.Single(variable => variable.Id == value.Variable).PreserveOnDowngrade).ToArray();
        var replacement = card.WithUpgradeLevel(0).WithVariableOffsets(offsets);
        if (temporarySource is { } temporaryPower)
            replacement = replacement.WithTemporaryUpgradeChange(new(temporaryPower, card.UpgradeLevel));
        session.State.ReplaceCombatCard(card, replacement);
        session.State.Emit(session, "card_downgraded", session.State.Player.Id, card.InstanceId, card.UpgradeLevel);
    }

    internal static void RestoreTemporaryDowngrade(GameSession session, CardInstance card, ContentId sourcePower)
    {
        var change = card.TemporaryUpgradeChanges.SingleOrDefault(candidate => candidate.SourcePower == sourcePower);
        if (change is null) return;
        session.State.ReplaceCombatCard(card, card.WithoutTemporaryUpgradeChange(sourcePower));
        for (var i = 0; i < change.RestoreLevels; i++)
            session.State.UpgradeCard(session, card.InstanceId);
    }

}
