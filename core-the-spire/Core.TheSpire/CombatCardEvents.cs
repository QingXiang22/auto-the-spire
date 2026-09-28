namespace CoreTheSpire;

public sealed record CardPlayListener(EffectNode Effect, CardType? WhenCardType = null);
public enum CardPlayHistoryPhase { Started, Finished }

internal sealed record CardListenerEffect(string InstanceId, bool AfterPlay, CardType PlayedType) : EffectNode;
internal sealed record CardEnteredCombatEffect(string InstanceId) : EffectNode;
internal sealed record EmitGameEventEffect(string Type, string Source, string Target, int Amount) : EffectNode;

internal sealed class CardPlayHistory
{
    private readonly Dictionary<CardType, int> _started = [];
    private readonly Dictionary<CardType, int> _finished = [];

    internal void Record(CardType type, CardPlayHistoryPhase phase)
    {
        var counts = phase == CardPlayHistoryPhase.Started ? _started : _finished;
        counts[type] = checked(counts.GetValueOrDefault(type) + 1);
    }

    internal int Count(CardType? type, CardPlayHistoryPhase phase)
    {
        var counts = phase == CardPlayHistoryPhase.Started ? _started : _finished;
        return type is { } cardType ? counts.GetValueOrDefault(cardType) : counts.Values.Sum();
    }

    internal void Reset() { _started.Clear(); _finished.Clear(); }
}

internal static class CombatCardEvents
{
    // Source PlayerCombatState.AllPiles order, independent of the CardPile enum's order.
    private static readonly CardPile[] ListenerPiles = [CardPile.Hand, CardPile.Draw, CardPile.Discard, CardPile.Exhaust, CardPile.Play];

    internal static void QueueInitialEntries(GameSession session)
    {
        var state = session.State;
        state.PendingEffects.Add(new(state.Player.Creature,
            new EmitGameEventEffect("combat_started", state.Player.Id, state.Player.Id, 0), null, null));
        var cards = ListenerPiles.SelectMany(state.GetPile).ToArray();
        for (var i = cards.Length - 1; i >= 0; i--) QueueEntry(state, cards[i]);
    }

    internal static void QueueAfterAutoPostPlay(GameState state)
    {
        var cards = ListenerPiles.SelectMany(state.GetPile).Where(card =>
        {
            var definition = state.Definition.Cards[card.CardId];
            return definition.AfterAutoPostPlay is not null || definition.UpgradedAfterAutoPostPlay is not null;
        }).ToArray();
        for (var i = cards.Length - 1; i >= 0; i--)
            state.PendingEffects.Add(new(state.Player.Creature,
                new CardAfterAutoPostPlayEffect(cards[i].InstanceId), null, cards[i]));
    }

    internal static void QueueEntry(GameState state, CardInstance card)
    {
        var definition = state.Definition.Cards[card.CardId];
        if (definition.OnEnterCombat is null && definition.UpgradedOnEnterCombat is null) return;
        state.PendingEffects.Add(new(state.Player.Creature, new CardEnteredCombatEffect(card.InstanceId), null, card));
    }

    internal static void Enter(GameSession session, string instanceId)
    {
        var state = session.State;
        if (state.FindCombatCard(instanceId) is { } card && state.Definition.Cards[card.CardId].ResolveEnterEffect(card) is { } effect)
            state.PendingEffects.Add(new(state.Player.Creature, effect, null, card));
    }

    internal static void QueuePlayListeners(GameState state, CardPlayFrame frame, bool afterPlay)
    {
        if (!(afterPlay ? state.Definition.HasAfterCardPlayListeners : state.Definition.HasBeforeCardPlayListeners)) return;
        var type = state.Definition.Cards[frame.Card.CardId].Type;
        var listeners = ListenerPiles.SelectMany(state.GetPile).Where(card =>
        {
            var definition = state.Definition.Cards[card.CardId];
            return afterPlay ? definition.AfterCardPlayed is not null || definition.UpgradedAfterCardPlayed is not null
                : definition.BeforeCardPlayed is not null || definition.UpgradedBeforeCardPlayed is not null;
        }).ToArray();
        for (var i = listeners.Length - 1; i >= 0; i--)
            state.PendingEffects.Add(new(state.Player.Creature,
                new CardListenerEffect(listeners[i].InstanceId, afterPlay, type), frame.Target, listeners[i]));
    }

    internal static void ExecuteListener(GameSession session, CardListenerEffect callback, EntityState? target)
    {
        var state = session.State;
        if (state.FindCombatCard(callback.InstanceId) is not { } card) return;
        var listener = state.Definition.Cards[card.CardId].ResolvePlayListener(card, callback.AfterPlay);
        if (listener is null || listener.WhenCardType is { } type && type != callback.PlayedType) return;
        state.PendingEffects.Add(new(state.Player.Creature, listener.Effect, target, card));
    }
}
