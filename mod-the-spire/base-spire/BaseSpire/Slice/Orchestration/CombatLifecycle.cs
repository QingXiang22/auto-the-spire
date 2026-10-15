using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Orchestration;

/// <summary>Reset ownership: turn facts here, battle counters at room entry, persistent HP/deck in Run.</summary>
public sealed record CombatLifecycle(CardSchema Card, CardPlayHistory History, CollectionSymbol Played,
    FieldSymbol LostHp, FieldSymbol ExhaustedCard, FieldSymbol BlockGains, FieldSymbol HpLossCount,
    RelationSymbol? PlayedReveal = null)
{
    public Func<IrScope, Inst[]> ResetAdditional { get; set; } = _ => [];
    public Inst[] BeginTurn(IrScope scope, Expr owner) =>
    [
        History.Clear(scope),
        .. PlayedReveal is null ? [] : new Inst[] { scope.ClearRelation(PlayedReveal, owner) },
        scope.ForEach(scope.Collection(Played), card => [scope.RemoveFromCollection(Played, card)]),
        scope.SetField(owner, LostHp, scope.Const(false)),
        scope.SetField(owner, ExhaustedCard, scope.Const(false)),
        scope.SetField(owner, BlockGains, scope.Const(0L)),
    ];

    public Inst[] EndBattle(IrScope scope, Expr owner, ScopeTypeSymbol battle, CollectionSymbol longTermDeck) =>
    [
        History.Clear(scope),
        .. PlayedReveal is null ? [] : new Inst[] { scope.ClearRelation(PlayedReveal, owner) },
        // Includes retired transform sources outside the five piles; deck membership owns persistent cards.
        scope.ForEach(scope.Where(scope.Type(Card.Type, battle), card =>
            scope.Less(scope.CollectionIndex(longTermDeck, card), scope.Const(0L))), card => [scope.Destroy(card)]),
        scope.SetField(owner, HpLossCount, scope.Const(0L)),
        .. ResetAdditional(scope),
    ];
}
