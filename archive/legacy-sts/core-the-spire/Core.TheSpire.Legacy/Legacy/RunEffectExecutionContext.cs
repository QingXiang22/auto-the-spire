using System.Text.Json;

namespace CoreTheSpire;

/// <summary>Bounded run-state commands available to registered C# run-effect extensions.</summary>
public sealed class RunEffectExecutionContext
{
    private readonly RunState _state;
    private readonly IReadOnlyDictionary<ContentId, CardDefinition>? _cards;

    internal RunEffectExecutionContext(RunState state, ContentId handlerId,
        IReadOnlyDictionary<ContentId, CardDefinition>? cards)
    {
        _state = state;
        HandlerId = handlerId;
        _cards = cards;
    }

    public ContentId HandlerId { get; }
    public int Hp => _state.Hp;
    public int MaxHp => _state.MaxHp;
    public int Gold => _state.Gold;
    public int GetInt(string key) =>
        _state.ReadModInt(HandlerId.Value[..HandlerId.Value.IndexOf(':')], key);
    public void SetInt(string key, int value) =>
        _state.SetModInt(HandlerId.Value[..HandlerId.Value.IndexOf(':')], key, value);

    public void GainGold(int amount) => RunEffectCommands.GainGold(_state, amount);
    public void Heal(int amount) => RunEffectCommands.Heal(_state, amount);
    public void LoseHp(int amount) => RunEffectCommands.LoseHp(_state, amount);
    public void GainMaxHp(int amount) => RunEffectCommands.GainMaxHp(_state, amount);
    public void LoseMaxHp(int amount) => RunEffectCommands.LoseMaxHp(_state, amount);

    public void AddCardToDeck(ContentId cardId)
    {
        if (_cards is null || !_cards.ContainsKey(cardId))
            throw new InvalidOperationException($"Custom run effect '{HandlerId}' adds unknown card '{cardId}'.");
        RunEffectCommands.AddCardToDeck(_state, cardId);
    }
}

internal static class RunEffectCommands
{
    public static void GainGold(RunState state, int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        state.Gold = checked(state.Gold + amount);
    }

    public static void Heal(RunState state, int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        state.Hp = (int)Math.Min(state.MaxHp, (long)state.Hp + amount);
    }

    public static void LoseHp(RunState state, int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        state.Hp = (int)Math.Max(0L, (long)state.Hp - amount);
    }

    public static void GainMaxHp(RunState state, int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        state.MaxHp = checked(state.MaxHp + amount);
        state.Hp = checked(state.Hp + amount);
    }

    public static void LoseMaxHp(RunState state, int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        state.MaxHp = (int)Math.Max(1L, (long)state.MaxHp - amount);
        state.Hp = Math.Min(state.Hp, state.MaxHp);
    }

    public static void AddCardToDeck(RunState state, ContentId cardId) =>
        state.Deck.Add(new CardInstance($"run-event-card-{state.NextEventCardInstanceId++:D4}", cardId));
}
