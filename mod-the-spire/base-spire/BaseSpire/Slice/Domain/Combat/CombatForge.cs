using BaseSpire.Slice.Effects;
using BaseSpire.Slice.Orchestration;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Domain.Combat;

public sealed class CombatForge(CardDefinitions definitions, CardPileSymbols piles,
    IReadOnlyDictionary<string, FieldSymbol> variables, Func<IrScope, Expr, Inst[]> entered)
{
    public Func<IrScope, Expr, Expr, Inst[]> AfterForged { get; init; } = (_, _, _) => [];

    public Inst[] Emit(IrScope scope, Expr owner, ForgeCardEffect effect, Expr amount)
    {
        var definition = definitions.Definition(effect.CardId);
        if (!definition.Normal.Variables.ContainsKey(effect.Variable) || !definition.Upgraded.Variables.ContainsKey(effect.Variable)
            || !variables.TryGetValue(effect.Variable, out var field))
            throw Faults.Schema($"{effect.Source}: forge requires a damage variable on both weapon variants");
        var captured = scope.Local("forgeAmount", FieldTypeSchema.Int(-99999, 99999));
        var created = scope.Local("forgedCard", FieldTypeSchema.RefTo(definitions.Schema.Type));
        Expr Weapon(Expr card) => scope.Equal(scope.Field(card, definitions.Schema.Definition), scope.Const(definition.Id));
        Expr present = scope.Const(false);
        foreach (var pile in Enum.GetValues<CardEffectPile>().Where(pile => pile != CardEffectPile.Exhaust))
            present = scope.Or(present, scope.Exists(scope.Where(scope.Collection(piles[pile]), Weapon)));
        var update = Enum.GetValues<CardEffectPile>().Select(pile => scope.ForEach(scope.Where(scope.Collection(piles[pile]), Weapon),
            card => [scope.SetField(card, field, scope.Add(scope.Field(card, field), scope.LocalRef(captured)))])).ToArray();
        return [scope.SetLocal(captured, amount),
            scope.If(scope.Less(scope.LocalRef(captured), scope.Const(0L)), scope.Fail("Forge amount must not be negative")),
            scope.If(scope.Not(present), [scope.CreateEntity(definitions.Schema.Type, scope.EntityScope(owner), created, definitions.New(scope, definition, false)),
                scope.IfElse(scope.Less(scope.Count(scope.Collection(piles.Hand)), scope.Const((long)SliceManifest.MaxHandSize)),
                    [scope.AddToCollection(piles.Hand, scope.LocalRef(created))], [scope.AddToCollection(piles.Discard, scope.LocalRef(created))]),
                .. entered(scope, scope.LocalRef(created))]), .. update, .. AfterForged(scope, owner, scope.LocalRef(captured))];
    }
}
