using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Orchestration;

/// <summary>One Engine entity per execution, including replays of the same physical card.</summary>
public sealed record CardPlayHistory(EntityTypeSymbol Entry, CollectionSymbol Entries,
    FieldSymbol Card, FieldSymbol Kind, FieldSymbol Finished, FieldSymbol Auto)
{
    public static CardPlayHistory Declare(EnginePackageBuilder package, CardSchema card, EnumTypeSymbol kind)
    {
        var entry = package.EntityType("CardPlayEntry");
        var cardRef = entry.AddField("Card", FieldTypeSchema.RefTo(card.Type), visibility: FieldVisibility.Hidden);
        var cardKind = entry.AddField("Kind", FieldTypeSchema.EnumOf(kind), visibility: FieldVisibility.Hidden);
        var finished = entry.AddField("Finished", FieldTypeSchema.Bool(), visibility: FieldVisibility.Hidden);
        var auto = entry.AddField("Auto", FieldTypeSchema.Bool(), visibility: FieldVisibility.Hidden);
        var entries = package.Collection("CardPlayHistory", entry);
        entries.IdentityVisible = false;
        entries.OrderVisible = false;
        return new(entry, entries, cardRef, cardKind, finished, auto);
    }

    public Inst Start(IrScope scope, Expr card, FieldSymbol kind, Expr auto, int slot) =>
        scope.CreateEntity(Entry, scope.EntityScope(card), slot,
            [(Card, card), (Kind, scope.Field(card, kind)), (Finished, scope.Const(false)), (Auto, auto)], Entries);

    public Inst Finish(IrScope scope, int slot) => scope.SetField(scope.LocalRef(slot), Finished, scope.Const(true));

    public Expr FinishedCount(IrScope scope, EnumTypeSymbol kind, string filter) =>
        scope.Count(scope.Where(scope.Collection(Entries), entry => scope.And(scope.Field(entry, Finished),
            scope.Equal(scope.Field(entry, Kind), scope.EnumConst(kind, filter)))));

    public Inst Clear(IrScope scope) => scope.ForEach(scope.Collection(Entries), entry => [scope.Destroy(entry)]);
}
