namespace CoreTheSpire;

internal sealed record PlaceGeneratedCardEffect(CardInstance Card, CardPile Pile) : EffectNode;

internal static class CombatCardGeneration
{
    internal static void Queue(GameSession session, EntityState owner, GenerateCombatCardsEffect effect, int count)
    {
        var state = session.State;
        if (owner != state.Player.Creature)
            throw new InvalidOperationException("Combat cards can only be generated for the player.");
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        effect.Cost?.Validate();
        if (count == 0 && !effect.Distinct) return;
        var characterId = state.Player.CharacterId
            ?? throw new InvalidOperationException("Combat card generation requires an explicit character identity.");
        var character = state.Definition.Characters[characterId];
        var options = character.CardPools.SelectMany(pool => state.Definition.CardPools[pool].Cards)
            .Distinct().Select(id => state.Definition.Cards[id])
            .Where(card => card.CanBeGeneratedInCombat
                && card.Rarity is not (CardRarity.Basic or CardRarity.Ancient or CardRarity.Event)
                && (effect.Type is null || card.Type == effect.Type)).ToArray();
        if (options.Length == 0)
        {
            if (effect.Distinct) return;
            throw new InvalidOperationException($"Character '{characterId}' has no eligible combat-generation cards.");
        }
        var random = session.Random.Get("combat-card-generation");
        if (effect.Distinct)
        {
            random.Shuffle(options);
            count = Math.Min(count, options.Length);
        }
        var generated = new CardInstance[count];
        for (var i = 0; i < count; i++)
        {
            var card = effect.Distinct ? options[i] : options[random.NextInt(options.Length)];
            var instance = state.Player.CreateGeneratedCard(card.Id);
            if (effect.Upgraded && state.IsUpgradeable(instance))
                instance = new CardInstance(instance.InstanceId, instance.CardId, 1);
            if (effect.Cost is { } cost)
                instance = new CardInstance(instance.InstanceId, instance.CardId, instance.UpgradeLevel, [cost]);
            generated[i] = instance;
        }
        for (var i = generated.Length - 1; i >= 0; i--)
            state.PendingEffects.Add(new(owner, new PlaceGeneratedCardEffect(generated[i], effect.Pile), null, generated[i]));
    }

    internal static void Place(GameSession session, PlaceGeneratedCardEffect effect)
    {
        var state = session.State;
        if (state.Player.Creature.Hp <= 0 || state.FirstLivingEnemy is null) return;
        state.GetPile(effect.Pile).Add(effect.Card);
        state.Emit(session, "card_generated", state.Player.Id, effect.Card.InstanceId, 1);
    }
}
