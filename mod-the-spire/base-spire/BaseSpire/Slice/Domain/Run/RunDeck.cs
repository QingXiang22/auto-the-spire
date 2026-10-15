using BaseSpire.Slice.Effects;
using BaseSpire.Slice.Orchestration;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Domain.Run;

/// <summary>Current supported permanent deck operations; source modifiers/history remain separate capabilities.</summary>
public sealed class RunDeck(CardDefinitions definitions, CollectionSymbol cards)
{
    public CardDefinitions Definitions { get; } = definitions;
    public CollectionSymbol Cards { get; } = cards;
    public Func<IrScope, Expr, Inst[]> BeforeRemoved { get; init; } = (_, _) => [];
    public Func<IrScope, Expr, Inst[]> AfterAdded { get; init; } = (_, _) => [];
    private int nextLocal;

    public Inst[] Create(IrScope scope, Expr run, CardDefinition definition, bool upgraded = false)
    {
        var created = scope.Local($"permanentCard{nextLocal++}", FieldTypeSchema.RefTo(Definitions.Schema.Type));
        return [scope.CreateEntity(Definitions.Schema.Type, scope.EntityScope(run), created,
            Definitions.New(scope, definition, upgraded)), .. Acquire(scope, scope.LocalRef(created))];
    }

    /// <summary>The caller removes any source candidate membership first. The same instance enters the deck.</summary>
    public Inst[] Acquire(IrScope scope, Expr card)
    {
        var selected = scope.Local($"acquiredPermanentCard{nextLocal++}", FieldTypeSchema.RefTo(Definitions.Schema.Type));
        var read = scope.LocalRef(selected);
        return [scope.SetLocal(selected, card), scope.AddToCollection(Cards, read), .. AfterAdded(scope, read)];
    }

    public Inst[] Remove(IrScope scope, Expr card)
    {
        var selected = scope.Local($"removedPermanentCard{nextLocal++}", FieldTypeSchema.RefTo(Definitions.Schema.Type));
        var read = scope.LocalRef(selected);
        return [scope.SetLocal(selected, card), scope.If(scope.Less(scope.CollectionIndex(Cards, read), scope.Const(0L)),
            scope.Fail("Permanent card removal requires a deck member")),
         .. BeforeRemoved(scope, read), scope.Destroy(read)];
    }
}
