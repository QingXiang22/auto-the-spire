namespace CoreTheSpire;

// These continuation nodes are created by the lifecycle, never by content packages.
internal enum CardPlayPhase { Initialize, BeforePlay, Effects, AfterPlay, Complete }
internal sealed record CardPlayPhaseEffect(CardPlayFrame Frame, CardPlayPhase Phase) : EffectNode;
internal sealed record BeginAutoCardEffect(CardInstance Card, bool ForceExhaust) : EffectNode;

internal static class CombatCardPlay
{
    public static void QueueManual(GameSession session, CardInstance card, EntityState? target)
    {
        var state = session.State;
        var preview = CombatCardRules.Preview(state, card);
        if (!state.Player.MutableHand.Remove(card)) throw new InvalidOperationException("The played card is not in hand.");
        state.Player.Energy -= preview.EnergyToSpend;
        state.Player.MutablePlayPile.Add(card);
        QueueFrame(state, new CardPlayFrame(card, preview.XEnergy, target, preview.ResultPile, false));
    }

    public static void QueueAutomatic(GameSession session, CardInstance card, bool forceExhaust)
    {
        var state = session.State;
        if (state.Player.Creature.Hp <= 0 || state.FirstLivingEnemy is null) return;
        var definition = state.Definition.Cards[card.CardId];
        if ((definition.Keywords & CardKeywords.Unplayable) != 0)
        {
            PlaceCard(session, card, forceExhaust || (definition.Keywords & CardKeywords.Exhaust) != 0
                ? CardPile.Exhaust : CardPile.Discard);
            return;
        }
        var target = definition.Target switch
        {
            TargetSelector.Self => state.Player.Creature,
            TargetSelector.SelectedTarget => RandomOpponent(session),
            TargetSelector.FirstEnemy => state.FirstLivingEnemy,
            TargetSelector.AllOpponents or TargetSelector.RandomEnemy => null,
            _ => throw new InvalidOperationException("Unsupported automatic card target.")
        };
        var preview = CombatCardRules.Preview(state, card);
        var result = forceExhaust && definition.Type != CardType.Power ? CardPile.Exhaust : preview.ResultPile;
        QueueFrame(state, new CardPlayFrame(card, preview.XEnergy, target, result, true));
    }

    private static EntityState RandomOpponent(GameSession session)
    {
        var opponents = session.State.Enemies.Select(enemy => enemy.Creature).Where(enemy => enemy.Hp > 0).ToArray();
        return opponents[session.Random.Get("card-targets").NextInt(opponents.Length)];
    }

    private static void QueueFrame(GameState state, CardPlayFrame frame)
    {
        state.CardPlayFrames.Add(frame);
        QueuePhase(state, frame, CardPlayPhase.Initialize);
    }

    private static void QueuePhase(GameState state, CardPlayFrame frame, CardPlayPhase phase) =>
        state.PendingEffects.Add((state.Player.Creature, new CardPlayPhaseEffect(frame, phase), frame.Target, frame.Card));

    public static void Continue(GameSession session, CardPlayPhaseEffect continuation)
    {
        var state = session.State;
        var frame = continuation.Frame;
        if (!ReferenceEquals(state.CurrentCardPlay, frame))
            throw new InvalidOperationException("Card-play continuations must complete in nested order.");
        switch (continuation.Phase)
        {
            case CardPlayPhase.Initialize:
                var (count, consumedPowers) = CombatCardRules.ResolvePlayCount(state, frame.Card);
                QueuePhase(state, frame, CardPlayPhase.Complete);
                for (var i = 0; i < count; i++)
                {
                    QueuePhase(state, frame, CardPlayPhase.AfterPlay);
                    QueuePhase(state, frame, CardPlayPhase.Effects);
                    QueuePhase(state, frame, CardPlayPhase.BeforePlay);
                }
                foreach (var power in consumedPowers)
                {
                    var delta = state.DecrementPower(session, state.Player.Creature, power);
                    if (delta != 0) state.Emit(session, "power", state.Player.Id, state.Player.Id, delta);
                }
                break;
            case CardPlayPhase.BeforePlay:
                CombatCardRules.BeforePlay(session, frame.Card);
                state.Emit(session, frame.IsAutoPlay ? "auto_play" : "play", state.Player.Id,
                    frame.Target?.Id ?? state.FirstLivingEnemy?.Id ?? state.Enemy.Id, 0);
                break;
            case CardPlayPhase.Effects:
                var current = state.CardInPlay(frame);
                state.PendingEffects.Add((state.Player.Creature, state.Definition.Cards[current.CardId]
                    .ResolveEffect(current), frame.Target, current));
                break;
            case CardPlayPhase.AfterPlay:
                EffectExecutor.QueuePowerTriggerEffects(session, state.Player.Creature,
                    PowerTrigger.AfterCardPlayed, frame.Target, state.CardInPlay(frame));
                break;
            case CardPlayPhase.Complete:
                state.CardPlayFrames.RemoveAt(state.CardPlayFrames.Count - 1);
                PlaceCard(session, frame.Card, frame.ResultPile);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(continuation.Phase));
        }
    }

    private static void PlaceCard(GameSession session, CardInstance original, CardPile? result)
    {
        var state = session.State;
        var current = state.Player.MutablePlayPile.SingleOrDefault(card => card.InstanceId == original.InstanceId);
        if (current is null) return;
        state.Player.MutablePlayPile.Remove(current);
        if (result is { } pile) state.GetPile(pile).Add(current);
        if (result == CardPile.Exhaust) EffectExecutor.QueueCardExhaustion(session, state.Player.Creature, current);
    }
}
