using BaseSpire.Slice.Effects;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Orchestration;

/// <summary>Explicit, fully registered pools only. Never substitutes the currently loadable subset of a character pool.</summary>
public sealed class CardGenerationRules(CardSchema schema, CardDefinitions definitions, CardPileSymbols piles,
    Func<IrScope, Expr, Inst[]> entered)
{
    public Inst[] Emit(IrScope scope, Expr owner, Expr count, GenerateCardsEffect effect)
    {
        if (effect.Pool.Length == 0 && effect.CharacterPool.Length == 0) throw Faults.Schema($"{effect.Source}: generation requires a complete explicit pool");
        var ids = effect.CharacterPool.Length == 0 ? effect.Pool.ToArray() : CharacterPool(effect.CharacterPool);
        var options = ids.Distinct(StringComparer.Ordinal).Select(definitions.Definition)
            .Where(c => effect.CharacterPool.Length == 0 || c.CanGenerateInCombat)
            .Where(c => effect.Filter == CardEffectCardFilter.Any ||
                (c.Kind == "Attack") == (effect.Filter == CardEffectCardFilter.Attack)).ToArray();
        if (options.Length == 0) return [];
        if (effect.Choose) return Offer(scope, owner, effect, options.Where(c => !c.MultiplayerOnly
            && c.Normal.Rarity is not ("basic" or "ancient" or "event")).ToArray());
        var pick = scope.Local("generatedDefinition", FieldTypeSchema.Int(0, options.Length - 1));
        var created = scope.Local("generatedCard", FieldTypeSchema.RefTo(schema.Type));
        return [scope.Repeat(count, [
            scope.SampleInt(SliceManifest.CardGenerationStream, scope.Const(0L), scope.Const((long)options.Length), pick),
            .. options.Select((card, index) => scope.If(scope.Equal(scope.LocalRef(pick), scope.Const((long)index)),
                scope.CreateEntity(schema.Type, scope.EntityScope(owner), created, definitions.New(scope, card, effect.Upgraded)),
                scope.If(scope.Const(effect.FreeThisTurn),
                    scope.SetField(scope.LocalRef(created), schema.CostModifier, scope.Subtract(scope.Const(0L), scope.Field(scope.LocalRef(created), schema.Cost))),
                    scope.SetField(scope.LocalRef(created), schema.FreeUntilPlayed, scope.Const(true))),
                scope.IfElse(scope.Less(scope.Count(scope.Collection(piles.Hand)), scope.Const((long)SliceManifest.MaxHandSize)),
                    [scope.AddToCollection(piles.Hand, scope.LocalRef(created))], [scope.AddToCollection(piles.Discard, scope.LocalRef(created))]),
                scope.If(scope.Const(true), entered(scope, scope.LocalRef(created)))))])];
    }
    private Inst[] Offer(IrScope scope, Expr owner, GenerateCardsEffect effect, CardDefinition[] options)
    {
        if (options.Length == 0) return [];
        var package = scope.Package;
        var indexType = package.Declarations.OfType<EntityTypeSymbol>().SingleOrDefault(t => t.Name == "GenerationPoolEntry");
        FieldSymbol indexField;
        CollectionSymbol pool, offers;
        if (indexType is null)
        {
            indexType = package.EntityType("GenerationPoolEntry");
            indexField = indexType.AddField("Index", FieldTypeSchema.Int(0, 9999), visibility: FieldVisibility.Hidden);
            pool = package.Collection("GenerationPool", indexType);
            pool.IdentityVisible = false; pool.OrderVisible = false; pool.SizeVisible = false;
            offers = package.Collection("GeneratedCardOffers", schema.Type);
        }
        else
        {
            indexField = indexType.Fields.Single(f => f.Name == "Index");
            pool = package.Declarations.OfType<CollectionSymbol>().Single(c => c.Name == "GenerationPool");
            offers = package.Declarations.OfType<CollectionSymbol>().Single(c => c.Name == "GeneratedCardOffers");
        }
        var entry = scope.Local("generationPoolEntry", FieldTypeSchema.RefTo(indexType));
        var created = scope.Local("offeredCard", FieldTypeSchema.RefTo(schema.Type));
        var position = scope.Local("offerPosition", FieldTypeSchema.Int(0, 9999));
        var selected = scope.Local("chosenGeneratedCards", FieldTypeSchema.ListOf(FieldTypeSchema.RefTo(schema.Type)));
        var steps = new List<Inst>
        {
            scope.ForEach(scope.Collection(pool), x => [scope.Destroy(x)]),
            scope.ForEach(scope.Collection(offers), x => [scope.Destroy(x)]),
        };
        for (var i = 0; i < options.Length; i++)
            steps.Add(scope.CreateEntity(indexType, scope.EntityScope(owner), entry, [(indexField, scope.Const((long)i))], pool));
        steps.Add(scope.ShuffleCollection(pool, SliceManifest.CardGenerationStream));
        steps.Add(scope.SetLocal(position, scope.Const(0L)));
        steps.Add(scope.ForEach(scope.Collection(pool), x =>
        [
            scope.If(scope.Less(scope.LocalRef(position), scope.Const(effect.Count.Constant)),
                [.. options.Select((card, i) => scope.If(scope.Equal(scope.Field(x, indexField), scope.Const((long)i)),
                    scope.CreateEntity(schema.Type, scope.EntityScope(owner), created, definitions.New(scope, card, effect.Upgraded), offers)))]),
            scope.SetLocal(position, scope.Add(scope.LocalRef(position), scope.Const(1L))),
            scope.Destroy(x),
        ]));
        steps.Add(scope.Decide(scope.Const(Value.OfParticipant(SliceManifest.Player)), DecisionResponseKind.EntityRefList,
            "Choose a generated card", selected, scope.Collection(offers), scope.Const(effect.AllowCancel ? 0L : 1L), scope.Const(1L)));
        steps.Add(scope.ForEach(new RefListQuery(selected), card =>
        [
            scope.RemoveFromCollection(offers, card),
            .. effect.FreeThisTurn ? new Inst[] { scope.SetField(card, schema.CostModifier,
                scope.Subtract(scope.Const(0L), scope.Field(card, schema.Cost))) } : [],
            scope.IfElse(scope.Less(scope.Count(scope.Collection(piles.Hand)), scope.Const((long)SliceManifest.MaxHandSize)),
                [scope.AddToCollection(piles.Hand, card)], [scope.AddToCollection(piles.Discard, card)]),
            .. entered(scope, card),
        ]));
        steps.Add(scope.ForEach(scope.Collection(offers), x => [scope.Destroy(x)]));
        return [.. steps];
    }

    private string[] CharacterPool(string name)
    {
        if (name != "IroncladCardPool") throw Faults.Schema($"Unregistered character generation pool '{name}'");
        // Source order is complete. Resolve every definition before applying generation exclusions/type.
        return IroncladRewardPool.SourceOrder.Where(id => !IroncladRewardPool.MultiplayerOnly.Contains(id,StringComparer.Ordinal)).Select(id => {
            var qualified = "base-spire:" + id;
            var card = definitions.Definition(qualified);
            if (!card.Pools.Contains(name, StringComparer.Ordinal)) throw Faults.Schema($"Generation pool member '{qualified}' is missing its pool declaration");
            return qualified;
        }).ToArray();
    }

}
