using BaseSpire.Slice.Rooms;

sealed class HealEvent : ChoiceEventModule
{
    public override string Id => "example-events:heal";
    protected override IReadOnlyList<EventChoice> Choices(EventCompileContext context) =>
        [new("HealInEvent", effects => effects.HealLivingPlayer(5))];
}
