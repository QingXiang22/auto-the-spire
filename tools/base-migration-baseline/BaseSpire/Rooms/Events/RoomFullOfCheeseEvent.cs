using BaseSpire.Slice.Domain.Run;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Rooms;

/// <summary>Source RoomFullOfCheese: eight distinct common rewards, choose two permanent instances.</summary>
public sealed class RoomFullOfCheeseEvent(EnginePackageBuilder package) : ChoiceEventModule
{
    public const string SourceName = "RoomFullOfCheese";
    public const string Gorge = "RoomFullOfCheeseGorge";
    public const string Search = "RoomFullOfCheeseSearch";
    public override string Id => EventSourceIdentity.ModuleId(SourceName);

    protected override IReadOnlyList<EventChoice> Choices(EventCompileContext context)
    {
        var deck = context.Deck ?? throw new InvalidOperationException("RoomFullOfCheese requires RunDeck");
        // Resolve the complete source pool first, then apply the event's source rarity filter.
        var pool = IroncladRewardPool.SourceOrder.Where(id => !IroncladRewardPool.MultiplayerOnly.Contains(id))
            .Select(id => deck.Definitions.Definition("base-spire:" + id)).Where(card => card.Normal.Rarity == "common").ToArray();
        var rewards = new RunCardRewards(package, deck);
        return [new(Gorge, e => rewards.Choose(e.Scope, e.Run, pool, 8, 2)),
            new(Search, e => [e.Unsupported("base-spire: RoomFullOfCheese Search requires ChosenCheese acquisition and AfterCombatEnd, not implemented")])];
    }
}
