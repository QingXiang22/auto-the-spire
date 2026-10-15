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
