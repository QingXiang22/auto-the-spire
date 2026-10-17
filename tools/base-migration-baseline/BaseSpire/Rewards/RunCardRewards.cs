using BaseSpire.Slice.Effects;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Domain.Run;

/// <summary>Source ForNonCombatWithUniformOdds: distinct ordinary offers, NoUpgradeRoll, no rarity-odds mutation.</summary>
public sealed class RunCardRewards(EnginePackageBuilder package, RunDeck deck)
{
    private CollectionSymbol? offers;

    public Inst[] Choose(IrScope s, Expr run, IReadOnlyList<CardDefinition> pool, int count, int take)
    {
        if (pool.Count < count || take > count || take < 1)
            throw new ArgumentException("Reward pool cannot satisfy the requested distinct choices");
        offers ??= package.Collection("EventCardOffers", deck.Definitions.Schema.Type);
        var schema = deck.Definitions.Schema;
        var pick = s.Local("eventRewardPick", FieldTypeSchema.Int(0, pool.Count));
        var cursor = s.Local("eventRewardCursor", FieldTypeSchema.Int(0, pool.Count));
        var created = s.Local("eventRewardCard", FieldTypeSchema.RefTo(schema.Type));
        var chosen = s.Local("eventRewardChosen", FieldTypeSchema.ListOf(FieldTypeSchema.RefTo(schema.Type)));
        var stream = VanillaRandomLayout.EnginePlayerStreamName("Rewards");
        return [s.ForEach(s.Collection(offers), card => [s.Destroy(card)]),
            s.Repeat(s.Const((long)count), [
                s.SampleInt(stream, s.Const(0L), s.Subtract(s.Const((long)pool.Count), s.Count(s.Collection(offers))), pick),
                s.SetLocal(cursor, s.Const(0L)),
                .. pool.Select(card => s.If(s.Not(s.Exists(s.Where(s.Collection(offers), offer =>
                    s.Equal(s.Field(offer, schema.Definition), s.Const(card.Id))))),
                    [s.If(s.Equal(s.LocalRef(cursor), s.LocalRef(pick)),
                        [s.CreateEntity(schema.Type, s.EntityScope(run), created, deck.Definitions.New(s, card, false), offers)]),
                     s.SetLocal(cursor, s.Add(s.LocalRef(cursor), s.Const(1L)))]))]),
            s.Decide(s.Const(Value.OfParticipant(SliceManifest.Player)), DecisionResponseKind.EntityRefList,
                "Choose permanent reward cards", chosen, s.Collection(offers), s.Const((long)take), s.Const((long)take)),
            s.ForEach(new RefListQuery(chosen), card => [s.RemoveFromCollection(offers, card), .. deck.Acquire(s, card)]),
            s.ForEach(s.Collection(offers), card => [s.Destroy(card)])];
    }
}
