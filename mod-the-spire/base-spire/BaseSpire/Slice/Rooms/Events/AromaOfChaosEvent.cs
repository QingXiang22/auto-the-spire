namespace BaseSpire.Slice.Rooms;

/// <summary>Source AromaOfChaos: transform a chosen deck card or upgrade a chosen deck card, then finish.</summary>
public sealed class AromaOfChaosEvent : ChoiceEventModule
{
    public const string SourceName = "AromaOfChaos";
    public const string LetGoAction = "AromaOfChaosLetGo";
    public const string MaintainControlAction = "AromaOfChaosMaintainControl";
    public override string Id => EventSourceIdentity.ModuleId(SourceName);

    protected override IReadOnlyList<EventChoice> Choices(EventCompileContext context)
    {
        var deck = context.Deck ?? throw new InvalidOperationException("AromaOfChaos requires RunDeck");
        return [new(LetGoAction, e => deck.SelectForTransform(e.Scope, e.Run, EventRandomAsset.StreamName(SourceName))),
            new(MaintainControlAction, e => deck.SelectForUpgrade(e.Scope))];
    }
}
