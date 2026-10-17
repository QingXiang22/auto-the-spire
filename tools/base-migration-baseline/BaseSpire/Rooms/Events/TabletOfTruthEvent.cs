using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Rooms;

/// <summary>Source TabletOfTruth: heal, or pay cached max-HP costs for up to five decipher pages.</summary>
public sealed class TabletOfTruthEvent : ChoiceEventModule
{
    public const string SourceName = "TabletOfTruth";
    public const string Decipher = "TabletOfTruthDecipher";
    public const string Smash = "TabletOfTruthSmash";
    public const string GiveUp = "TabletOfTruthGiveUp";
    public override string Id => EventSourceIdentity.ModuleId(SourceName);
    private FieldSymbol? count, cost;

    private void Declare(EventCompileContext context)
    {
        count ??= context.Room.RunType.AddField("TabletOfTruthDecipherCount", FieldTypeSchema.Int(0, 5));
        cost ??= context.Room.RunType.AddField("TabletOfTruthDecipherCost", FieldTypeSchema.Int(0, 999999));
    }

    public override Inst[] Enter(IrScope scope, EventCompileContext context, Expr run)
    {
        Declare(context);
        return [scope.SetField(run, count!, scope.Const(0L)), scope.SetField(run, cost!, scope.Const(3L))];
    }

    protected override IReadOnlyList<EventChoice> Choices(EventCompileContext context)
    {
        Declare(context);
        var deck = context.Deck ?? throw new InvalidOperationException("TabletOfTruth requires RunDeck");
        return [new(Smash, e => e.HealLivingPlayer(20), e => e.Scope.Equal(e.Read(count!), e.Scope.Const(0L))),
            new(GiveUp, _ => [], e => e.Scope.Greater(e.Read(count!), e.Scope.Const(0L))),
            new(Decipher, e =>
            {
                var s = e.Scope;
                var candidates = s.Where(s.Collection(deck.Cards), card => deck.Definitions.CanUpgrade(s, card));
                return [s.IfElse(s.GreaterOrEqual(e.Read(cost!), e.Read(context.Room.MaxHp)),
                    [.. e.Resources.LoseMaxHp(s, e.Run, s.Subtract(e.Read(context.Room.MaxHp), s.Const(1L))),
                     .. e.DamageOutsideCombat(e.Read(context.Room.Hp))],
                    [.. e.Resources.LoseMaxHp(s, e.Run, e.Read(cost!)),
                     s.IfElse(s.Equal(e.Read(count!), s.Const(4L)),
                        [s.ForEach(candidates, card => [deck.Definitions.Upgrade(s, card)])],
                        [s.If(s.Greater(s.Count(candidates), s.Const(0L)),
                            deck.SelectRandom(s, candidates, EventRandomAsset.StreamName(SourceName),
                                card => [deck.Definitions.Upgrade(s, card)]))])]),
                    s.SetField(e.Run, count!, s.Add(e.Read(count!), s.Const(1L))),
                    s.IfElse(s.Equal(e.Read(count!), s.Const(5L)), context.Close(s, e.Run),
                        [s.SetField(e.Run, cost!, s.Subtract(e.Read(context.Room.MaxHp), s.Const(1L))),
                         .. new[] { 6L, 12L, 24L }.Select((value, index) =>
                             s.If(s.Equal(e.Read(count!), s.Const((long)index + 1)),
                                 s.SetField(e.Run, cost!, s.Const(value))))])];
            }, Completion: EventCompletion.Continue)];
    }
}
