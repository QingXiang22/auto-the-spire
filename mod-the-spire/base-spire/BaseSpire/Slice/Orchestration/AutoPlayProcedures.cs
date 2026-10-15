using BaseSpire.Slice.Effects;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Orchestration;

/// <summary>Collects a batch before playing it, including nested batches, then calls the ordinary play pipeline.</summary>
public sealed class AutoPlayProcedures
{
    private readonly EntityTypeSymbol batch;
    private readonly EntityTypeSymbol pending;
    private readonly FieldSymbol batchOf;
    private readonly FieldSymbol cardOf;
    private readonly CardPileSymbols piles;
    private readonly CardDrawProcedures draw;
    private readonly ProcedureSymbol play;
    private readonly Func<IrScope, Expr> alive;
    private readonly Func<IrScope, Expr> active;

    public AutoPlayProcedures(EnginePackageBuilder package, CardSchema cards, CardPileSymbols piles,
        CardDrawProcedures draw, ProcedureSymbol play, Func<IrScope, Expr> alive, Func<IrScope, Expr> active)
    {
        this.piles = piles; this.draw = draw; this.play = play; this.alive = alive; this.active = active;
        batch = package.EntityType("AutoPlayBatch");
        batch.AddField("Active", FieldTypeSchema.Bool(), visibility: FieldVisibility.Hidden);
        pending = package.EntityType("AutoPlayPending");
        batchOf = pending.AddField("Batch", FieldTypeSchema.RefTo(batch), visibility: FieldVisibility.Hidden);
        cardOf = pending.AddField("Card", FieldTypeSchema.RefTo(cards.Type), visibility: FieldVisibility.Hidden);
    }

    public Inst[] FromTop(IrScope s, Expr owner, Expr count, bool forceExhaust)
    {
        var token = s.Local("autoPlayBatch", FieldTypeSchema.RefTo(batch));
        var selected = s.Local("autoPlayCard", FieldTypeSchema.Binding());
        var queued = s.Local("autoPlayPending", FieldTypeSchema.RefTo(pending));
        return [s.If(active(s), [
            s.CreateEntity(batch, s.EntityScope(owner), token, [(batch.Fields[0], s.Const(true))]),
            s.Repeat(count, draw.Reshuffle(s), s.SetLocal(selected, s.First(s.Collection(piles.Draw))),
                s.If(s.Not(s.IsNone(s.LocalRef(selected))),
                    s.RemoveFromCollection(piles.Draw, s.LocalRef(selected)),
                    s.AddToCollection(piles.InPlay, s.LocalRef(selected)),
                    s.CreateEntity(pending, s.EntityScope(owner), queued, [(batchOf, s.LocalRef(token)), (cardOf, s.LocalRef(selected))]))),
            s.ForEach(s.Where(s.Type(pending), item => s.Equal(s.Field(item, batchOf), s.LocalRef(token))), item => [
                // A pending last kill doesn't settle the outer action. Unplayable cards still reach their result pile.
                s.If(alive(s), s.Call(play, s.Field(item, cardOf), s.Const((long)CardEffectPile.InPlay), s.Const(true), s.Const(forceExhaust), s.Const(1L))),
                s.Destroy(item)]), s.Destroy(s.LocalRef(token))])];
    }
}
