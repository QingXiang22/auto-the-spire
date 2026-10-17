namespace BaseSpire.Slice.Rooms;

/// <summary>Source MorphicGrove: pay all gold, transform two chosen cards; or gain five maximum/current HP.</summary>
public sealed class MorphicGroveEvent : ChoiceEventModule
{
    public const string SourceName = "MorphicGrove";
    public const string Group = "MorphicGroveGroup";
    public const string Loner = "MorphicGroveLoner";
    public override string Id => EventSourceIdentity.ModuleId(SourceName);

    protected override IReadOnlyList<EventChoice> Choices(EventCompileContext context)
    {
        var deck = context.Deck ?? throw new InvalidOperationException("MorphicGrove requires RunDeck");
        return [new(Loner, e => e.Resources.GainMaxHp(e.Scope, e.Run, e.Scope.Const(5L))),
            new(Group, e => [.. e.Resources.LoseGold(e.Scope, e.Run, e.Read(context.Room.Gold)),
                .. deck.SelectMany(e.Scope, 2, card => deck.CanTransform(e.Scope, card),
                    "Choose two permanent cards to transform", card => deck.TransformToRandom(
                        e.Scope, e.Run, card, EventRandomAsset.StreamName(SourceName)))])];
    }
}
