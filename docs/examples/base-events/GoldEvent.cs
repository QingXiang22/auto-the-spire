using BaseSpire.Slice.Rooms;

sealed class GoldEvent : ChoiceEventModule
{
    public override string Id => "example-events:gold";
    protected override IReadOnlyList<EventChoice> Choices(EventCompileContext context) =>
        [new("TakeEventGold", effects => effects.GainGold(7))];
}
