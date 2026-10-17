using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Rooms;

/// <summary>Source SlipperyBridge: preview a random removable card, remove it or pay to reroll.</summary>
public sealed class SlipperyBridgeEvent(EnginePackageBuilder package) : ChoiceEventModule
{
    public const string SourceName = "SlipperyBridge";
    public const string Overcome = "SlipperyBridgeOvercome";
    public const string HoldOn = "SlipperyBridgeHoldOn";
    public override string Id => EventSourceIdentity.ModuleId(SourceName);
    private FieldSymbol? selected, holds, skippedCard;
    private EntityTypeSymbol? skippedType;
    private CollectionSymbol? skipped;

    private void Declare(EventCompileContext context)
    {
        if (selected is not null) return;
        var card = context.Deck!.Definitions.Schema.Type;
        selected = context.Room.RunType.AddField("SlipperyBridgeCard", FieldTypeSchema.RefTo(card), optional: true);
        holds = context.Room.RunType.AddField("SlipperyBridgeHoldOns", FieldTypeSchema.Int(0, 9999));
        skippedType = package.EntityType("SlipperyBridgeSkippedCard");
        skippedCard = skippedType.AddField("Card", FieldTypeSchema.RefTo(card), visibility: FieldVisibility.Hidden);
        skipped = package.Collection("SlipperyBridgeSkippedCards", skippedType);
        skipped.IdentityVisible = false; skipped.OrderVisible = false;
    }

    public override Inst[] Enter(IrScope scope, EventCompileContext context, Expr run)
    {
        Declare(context);
        return [scope.SetField(run, selected!, scope.Const(Value.None)), scope.SetField(run, holds!, scope.Const(0L)),
            scope.ForEach(scope.Collection(skipped!), item => [scope.Destroy(item)]), .. Pick(scope, context, run)];
    }

    private Inst[] Pick(IrScope scope, EventCompileContext context, Expr run)
    {
        var deck = context.Deck!;
        var previous = scope.Local("bridgePrevious", FieldTypeSchema.RefTo(deck.Definitions.Schema.Type));
        var record = scope.Local("bridgeSkipped", FieldTypeSchema.Binding());
        var candidates = scope.Where(scope.Collection(deck.Cards), card => scope.And(deck.CanRemove(scope, card),
            scope.And(scope.Not(scope.Exists(scope.Where(scope.Collection(skipped!), entry => scope.Equal(scope.Field(entry, skippedCard!), card)))),
                scope.Or(scope.And(scope.IsNone(scope.LocalRef(previous)), scope.Not(deck.HasRarity(scope, card, "basic"))),
                    scope.And(scope.Not(scope.IsNone(scope.LocalRef(previous))), scope.Not(scope.Equal(
                        scope.Field(card, deck.Definitions.Schema.Definition), scope.Field(scope.LocalRef(previous), deck.Definitions.Schema.Definition))))))));
        var fallback = scope.Where(scope.Collection(deck.Cards), card => deck.CanRemove(scope, card));
        Inst[] Choose(Query query) => deck.SelectRandom(scope, query, EventRandomAsset.StreamName(SourceName),
            card => [scope.SetField(run, selected!, card)]);
        return [scope.SetLocal(previous, scope.Field(run, selected!)),
            scope.If(scope.And(scope.Not(scope.IsNone(scope.LocalRef(previous))), scope.Not(scope.Exists(scope.Where(scope.Collection(skipped!),
                item => scope.Equal(scope.Field(item, skippedCard!), scope.LocalRef(previous)))))), scope.CreateEntity(skippedType!, scope.EntityScope(run), record,
                [(skippedCard!, scope.LocalRef(previous))], skipped!)),
            scope.IfElse(scope.Greater(scope.Count(candidates), scope.Const(0L)), Choose(candidates), Choose(fallback))];
    }

    protected override IReadOnlyList<EventChoice> Choices(EventCompileContext context)
    {
        Declare(context);
        return [new(Overcome, e =>
        {
            var deck = context.Deck!;
            var card = e.Scope.Local("bridgeRemoved", FieldTypeSchema.RefTo(deck.Definitions.Schema.Type));
            return [e.Scope.SetLocal(card, e.Read(selected!)), e.Scope.SetField(e.Run, selected!, e.Scope.Const(Value.None)),
                e.Scope.ForEach(e.Scope.Collection(skipped!), item => [e.Scope.Destroy(item)]),
                .. deck.Remove(e.Scope, e.Scope.LocalRef(card))];
        }), new(HoldOn, e => [.. e.DamageOutsideCombat(e.Scope.Add(e.Scope.Const(3L), e.Read(holds!))),
            e.Scope.SetField(e.Run, holds!, e.Scope.Add(e.Read(holds!), e.Scope.Const(1L))), .. Pick(e.Scope, context, e.Run)],
            Completion: EventCompletion.Continue)];
    }
}
