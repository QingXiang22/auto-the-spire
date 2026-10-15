using BaseSpire.Slice.Effects;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Orchestration;

public sealed record CardPileSymbols(CollectionSymbol Hand, CollectionSymbol Draw, CollectionSymbol Discard,
    CollectionSymbol Exhaust, CollectionSymbol InPlay)
{
    public CollectionSymbol this[CardEffectPile pile] => pile switch
    {
        CardEffectPile.Hand => Hand, CardEffectPile.Draw => Draw, CardEffectPile.Discard => Discard,
        CardEffectPile.Exhaust => Exhaust, CardEffectPile.InPlay => InPlay,
        _ => throw new ArgumentOutOfRangeException(nameof(pile)),
    };
}

/// <summary>Shared card commands generate Engine instructions; callbacks publish card lifecycle events.</summary>
public sealed class CardPileProcedures(CardSchema schema, CardDefinitions definitions, CardPileSymbols piles,
    Func<IrScope, Expr, Inst[]> afterExhaust, Func<IrScope, Expr, Inst[]> afterEnter,
    Func<IrScope, Expr, Inst[]> afterDiscard, Func<IrScope, Expr, Inst[]> recordKnownTop)
{
    public Inst[] MoveChosen(IrScope scope, CardEffectPile from, CardEffectPile to, bool top, Expr card)
    {
        if (to == CardEffectPile.Hand)
            return [scope.If(scope.Less(scope.Count(scope.Collection(piles.Hand)), scope.Const((long)SliceManifest.MaxHandSize)),
                Move(scope, from, to, card))];
        if (top)
            return [scope.RemoveFromCollection(piles[from], card),
                scope.AddToCollection(piles.Draw, card, InsertPosition.Start), .. recordKnownTop(scope, card)];
        return [.. Move(scope, from, to, card),
            .. from == CardEffectPile.Hand && to == CardEffectPile.Discard ? afterDiscard(scope, card) : []];
    }

    public Inst[] RemoveFromCombat(IrScope scope, CardEffectPile from, Expr card) =>
        [scope.RemoveFromCollection(piles[from], card)];

    public Inst[] Move(IrScope scope, CardEffectPile from, CardEffectPile to, Expr card) => from == to ? [] :
    [
        scope.RemoveFromCollection(piles[from], card), scope.AddToCollection(piles[to], card),
        .. to == CardEffectPile.Exhaust ? afterExhaust(scope, card) : [],
    ];

    public Inst[] Clone(IrScope scope, Expr card, CardEffectPile to)
    {
        var created = scope.Local("clone", FieldTypeSchema.RefTo(schema.Type));
        return [scope.CreateEntity(schema.Type, scope.EntityScope(card), created,
            definitions.Copy(scope, card, clone: true), piles[to]), .. afterEnter(scope, scope.LocalRef(created))];
    }

    public Inst[] Transform(IrScope scope, Expr original, CardEffectPile from, string definition, bool upgraded)
    {
        var replacement = definitions.Definition(definition);
        var position = scope.Local("transformPosition", FieldTypeSchema.Int(0, 9999));
        var created = scope.Local("transformReplacement", FieldTypeSchema.RefTo(schema.Type));
        return
        [
            scope.SetLocal(position, scope.CollectionIndex(piles[from], original)),
            scope.RemoveFromCollection(piles[from], original),
            scope.CreateEntity(schema.Type, scope.EntityScope(original), created, definitions.New(scope, replacement, upgraded)),
            scope.AddToCollection(piles[from], scope.LocalRef(created), InsertPosition.AtIndex, scope.LocalRef(position)),
            .. afterEnter(scope, scope.LocalRef(created)),
        ];
    }
}
